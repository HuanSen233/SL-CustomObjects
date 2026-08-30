using System.Collections.Generic;
using ToolLib;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SceneView curve interaction editor — select, drag, add/remove vertices and segments.
/// Supports Shift multi-select, Alt+Right-click insert, Alt+Left-click span select, grid snapping and axis awareness.
///
/// Split across files (partial class):
///   CurveSceneEditor.MouseEvents.cs  — HandleMouseDown/Drag/Up, HandleKeyDown
///   CurveSceneEditor.HitTest.cs       — hit testing (vertices/handles/curve spans/cursor)
///   CurveSceneEditor.Utility.cs       — helpers (coordinate conversion, snapping, math)
/// SceneView 曲线交互编辑器 — 选择、拖拽、增删顶点/线段。
/// 支持 Shift 多选、Alt+右键插入、Alt+左键选段、网格吸附、轴向感知。
///
/// 拆分文件（partial class）：
///   CurveSceneEditor.MouseEvents.cs  — HandleMouseDown/Drag/Up, HandleKeyDown
///   CurveSceneEditor.HitTest.cs       — 命中检测（顶点/曲线段/游标拾取）
///   CurveSceneEditor.Utility.cs       — 辅助方法（坐标转换、吸附、数学工具）
/// </summary>
public static partial class CurveSceneEditor
{
    /// <summary>Vertex hit radius (vertex size + margin). / 顶点命中半径（顶点尺寸 + 余量）</summary>
    private static float VertexHitRadius => (CurveTool.Instance?.VertexSize ?? 0.2f) + 0.1f;
    /// <summary>Handle hit radius (handle size + margin). / 控制柄命中半径</summary>
    private static float HandleHitRadius => (CurveTool.Instance?.HandleEndSize ?? 0.12f) + 0.1f;
    /// <summary>Curve span hit radius. / 曲线段命中半径</summary>
    private const float CurveHitRadius = 0.25f;
    /// <summary>Micro-segment hit radius. / 小线段命中半径</summary>
    private const float SegmentHitRadius = 0.3f;
    /// <summary>Cursor hit radius (twice the display size). / 游标命中半径（显示尺寸 ×2）</summary>
    private static float CursorHitRadius => (CurveTool.Instance?.CursorDisplaySize ?? 0.15f) * 2f;

    private static bool _isDragging;
    private static bool _isDraggingCursor;
    /// <summary>Whether the current drag already recorded Undo (recorded once per drag to avoid Undo history explosion).
    /// 当前拖拽是否已记录 Undo（只在第一次 MouseDrag 时记录一次，避免 Undo 历史爆炸）</summary>
    private static bool _undoRecorded;
    private static Vector3 _dragStartCursorPos;
    private static Vector2 _dragStartMouse;
    private static Vector2 _dragStartValue;
    /// <summary>3D drag start mouse position (for 3D curves). / 3D 拖拽起始鼠标位置（3D 曲线用）</summary>
    private static Vector3 _dragStartMouse3D;
    /// <summary>3D drag start handle offset (for 3D curves). / 3D 拖拽起始控制柄偏移（3D 曲线用）</summary>
    private static Vector3 _dragStartHandle3DOffset;
    /// <summary>Start screen Y for Y-axis dragging. / Y 轴拖拽起始屏幕 Y 位置</summary>
    private static float _dragStartScreenY;
    /// <summary>Start height value for Y-axis dragging (vertex/handle). / Y 轴拖拽起始高度值（vertex/handle）</summary>
    private static float _dragStartArrowHeight;
    /// <summary>Start values of all multi-selected vertices (vertex index → start position).
    /// 批量拖拽时各顶点的起始值（顶点索引→起始位置）</summary>
    private static Dictionary<int, Vector2> _dragStartValues;
    private static bool _registered;

    /// <summary>Registers the SceneView GUI hook (idempotent). / 注册 SceneView 钩子（幂等）</summary>
    public static void Register()
    {
        if (_registered) return;
        SceneView.duringSceneGui += OnSceneGUI;
        _registered = true;
    }

    /// <summary>Unregisters the SceneView GUI hook (idempotent). / 注销 SceneView 钩子（幂等）</summary>
    public static void Unregister()
    {
        if (!_registered) return;
        SceneView.duringSceneGui -= OnSceneGUI;
        _registered = false;
    }

    /// <summary>SceneView entry point: dispatches events only while the tool is in edit mode.
    /// SceneView 入口：仅当工具处于编辑模式时分发事件</summary>
    private static void OnSceneGUI(SceneView sv)
    {
        var w = CurveTool.Instance;
        if (w == null || !w.IsEditMode) { _isDragging = false; _isDraggingCursor = false; _undoRecorded = false; return; }
        var m = CurveManager.Instance;
        if (m == null) return;

        Event e = Event.current;
        int cid = GUIUtility.GetControlID(FocusType.Passive);
        bool moveEdit = w.UseMoveTool && Tools.current == Tool.Move;

        // Consume events in edit mode to block default scene selection/orbit operations.
        // 编辑模式下始终消耗事件，阻止 Unity 默认场景选择/轨道操作。
        // Move-tool editing is an exception: AddDefaultControl would starve the PositionHandle,
        // so skip it there and let the Move tool gizmo take the interaction.
        // 移动工具编辑例外：AddDefaultControl 会抢占 PositionHandle，故此处跳过，让移动工具 Gizmo 接管交互。
        if (!moveEdit) HandleUtility.AddDefaultControl(cid);

        switch (e.type)
        {
            case EventType.MouseDown: HandleMouseDown(e, m, w); break;
            case EventType.MouseDrag: HandleMouseDrag(e, m, w); break;
            case EventType.MouseUp: HandleMouseUp(e); break;
            case EventType.KeyDown: HandleKeyDown(e, m); break;
            case EventType.ScrollWheel: break;
        }

        // Move-tool editing: when enabled and the Move tool (W) is active, drive the selected vertex/handle via a PositionHandle.
        // 移动工具编辑：启用且处于移动工具（W）时，用 PositionHandle 驱动选中顶点/控制柄
        if (moveEdit)
            DrawMoveToolHandles(m);
    }

    /// <summary>Which element the move handle is targeting. / 移动手柄目标元素</summary>
    private enum MoveHandleTarget { Vertex, LeftHandle, RightHandle }

    /// <summary>World position of the element the move handle currently targets (vertex/handle, 2D or 3D).
    /// 移动手柄当前目标元素（顶点/控制柄，2D/3D）的世界坐标。</summary>
    private static Vector3 GetMoveHandleWorldPos(BezierCurve curve, CurveVertex vertex, bool is3d)
    {
        return vertex.SelectedSubElement switch
        {
            1 => is3d ? vertex.PositionV3 + vertex.LeftHandleOffsetV3 : curve.MapToWorld(vertex.LeftHandlePosition),
            2 => is3d ? vertex.PositionV3 + vertex.RightHandleOffsetV3 : curve.MapToWorld(vertex.RightHandlePosition),
            _ => is3d ? vertex.PositionV3 : curve.MapToWorld(vertex.Position),
        };
    }

    /// <summary>Whether the given screen position is on the move-handle gizmo (center + axis arrows), so an
    /// empty-space click there must NOT clear the selection. Uses a screen-radius approximation around the
    /// projected handle position. / 判断给定屏幕位置是否落在移动手柄 Gizmo（中心+轴箭头）上——点击此处时不应清空选中。
    /// 用投影到手柄位置周围的屏幕半径近似判断。</summary>
    private static bool IsOnMoveHandleGizmo(CurveManager m, Vector2 mousePos)
    {
        var vertex = m.SelectedVertex;
        var curve = m.SelectedCurve;
        if (curve == null || vertex == null) return false;
        Vector3 world = GetMoveHandleWorldPos(curve, vertex, curve.Is3D);
        // Delegates to the shared SceneDragUtility screen-radius check (camera guard + axis-arrow coverage).
        // 转发到共享 SceneDragUtility 的屏幕半径判定（含无相机的保护与轴箭头覆盖）。
        return SceneDragUtility.IsOnHandleGizmo(world, mousePos);
    }

    /// <summary>Draws a Unity PositionHandle for the selected vertex/handle and writes the dragged result back to the data.
    /// 为选中顶点/控制柄绘制 Unity PositionHandle，并将拖拽结果写回数据。</summary>
    private static void DrawMoveToolHandles(CurveManager m)
    {
        var curve = m.SelectedCurve;
        var vertex = m.SelectedVertex;
        if (curve == null || vertex == null) return;
        bool is3d = curve.Is3D;

        Vector3 vertexWorld = is3d ? vertex.PositionV3 : curve.MapToWorld(vertex.Position);
        int sub = vertex.SelectedSubElement;
        Vector3 worldPos;
        MoveHandleTarget target;
        switch (sub)
        {
            case 1: target = MoveHandleTarget.LeftHandle; break;
            case 2: target = MoveHandleTarget.RightHandle; break;
            // 顶点自身及高度箭头（4/5/6）：统一在顶点处显示手柄，Y 轴即调整高度
            default: target = MoveHandleTarget.Vertex; break;
        }
        worldPos = GetMoveHandleWorldPos(curve, vertex, is3d);

        // Handle rotation respects the Unity coordinate system: Global/world axis → identity (no plane tilt, no degenerate
        // LookRotation); Local → a stable plane-facing rotation (tangent forward + plane normal up) that never degenerates.
        // 手柄朝向尊重 Unity 坐标系：世界坐标（Global）→ identity（不随曲线平面转、LookRotation 不退化）；
        // 局部坐标（Local）→ 稳定的贴平面朝向（切线作 forward、平面法线作 up），且避免 LookRotation 退化。
        Quaternion rot = GetMoveHandleRotation(curve, is3d);

        // Snap step resolution + shared move-handle driver (draws the handle, applies snap, records Undo once).
        // 吸附步长解析 + 共享移动手柄驱动（绘制手柄、应用吸附、一次性记录 Undo）。
        var w = CurveTool.Instance;
        Event e = Event.current;
        bool ctrlSnap = e != null && (e.control || e.command);
        Vector3 gs = GetSnapStep(w, ctrlSnap);

        if (SceneMoveTool.DrawPositionHandle(m, worldPos, rot, gs,
                EditorSnapSettings.gridSnapEnabled || ctrlSnap, ref _undoRecorded, "移动曲线元素", out Vector3 newPos))
        {
            WriteMoveHandleTarget(curve, vertex, target, newPos, is3d, vertexWorld);
            m.MarkDirty();
            curve.RecalculateHandles();
            SceneView.RepaintAll();
            CurveTool.Instance?.Repaint();
        }
    }

    /// <summary>Computes the PositionHandle rotation: world axis for Global/3D, a stable plane-facing orientation for Local 2D.
    /// 计算 PositionHandle 的朝向：Global/3D 用世界轴；Local 2D 用贴合平面且不退化的稳定朝向。</summary>
    private static Quaternion GetMoveHandleRotation(BezierCurve curve, bool is3d)
    {
        if (is3d || Tools.pivotRotation == PivotRotation.Global) return Quaternion.identity;
        // Local + 2D: local-forward = first micro-segment tangent, local-up = plane normal.
        // 局部 + 2D：局部 forward = 第一小段切线，局部 up = 平面法线。
        var pts = curve.SamplePoints();
        Vector3 fwd = pts.Count >= 2 ? curve.MapToWorld(pts[1]) - curve.MapToWorld(pts[0]) : Vector3.zero;
        Vector3 n = curve.PlaneNormal;
        if (fwd.sqrMagnitude < 1e-8f) fwd = n;
        // Forward parallel to the plane normal would degenerate LookRotation; fall back to world axis.
        // forward 与平面法线平行会令 LookRotation 退化；回退到世界轴。
        if (Mathf.Abs(Vector3.Dot(fwd.normalized, n)) > 0.999f) return Quaternion.identity;
        return Quaternion.LookRotation(fwd.normalized, n);
    }

    /// <summary>Writes a move-handle drag result back to vertex/handle data (per-axis locks preserved).
    /// 将移动手柄拖拽结果写回顶点/控制柄数据（保留分轴锁）</summary>
    private static void WriteMoveHandleTarget(BezierCurve curve, CurveVertex vertex, MoveHandleTarget target, Vector3 newPos, bool is3d, Vector3 vertexWorld)
    {
        // Grid/increment snap was already applied by the shared SceneMoveTool.DrawPositionHandle before calling here.
        // 网格/增量吸附已由共享 SceneMoveTool.DrawPositionHandle 在调用前完成。

        switch (target)
        {
            case MoveHandleTarget.Vertex:
                if (is3d)
                {
                    // x→Position.x, z→Position.y (plane-Y), y→Height; locks: LockX / LockY(=plane-Y) / LockZ(=height)
                    // x→Position.x, z→Position.y（平面Y）, y→Height；分轴锁：LockX / LockY(=平面Y) / LockZ(=高度)
                    float nx = vertex.LockX ? vertex.Position.x : newPos.x;
                    float nz = vertex.LockY ? vertex.Position.y : newPos.z;
                    float ny = vertex.LockZ ? vertex.Height : newPos.y;
                    vertex.Position = new Vector2(nx, nz);
                    vertex.Height = ny;
                }
                else
                {
                    Vector2 np = curve.MapFromWorld(newPos);
                    if (vertex.LockX) np.x = vertex.Position.x;
                    if (vertex.LockY) np.y = vertex.Position.y;
                    vertex.Position = np;
                }
                break;
            case MoveHandleTarget.LeftHandle:
            case MoveHandleTarget.RightHandle:
                Vector3 offset = newPos - vertexWorld;
                bool isLeft = target == MoveHandleTarget.LeftHandle;
                // Dragging Auto/Vector/Aligned/Mirror handles auto-switches them to Free (same as the tool's built-in drag),
                // so the automatic recomputation does not override the manual move.
                // 拖拽 Auto/Vector/Aligned/Mirror 柄自动切为 Free（与工具自带拖拽一致），避免自动计算覆盖手动移动。
                if (isLeft)
                {
                    if (vertex.HandleTypeA == HandleType.Auto || vertex.HandleTypeA == HandleType.Vector ||
                        vertex.HandleTypeA == HandleType.AlignedLength || vertex.HandleTypeA == HandleType.Aligned)
                        vertex.HandleTypeA = HandleType.Free;
                }
                else
                {
                    if (vertex.HandleTypeB == HandleType.Auto || vertex.HandleTypeB == HandleType.Vector ||
                        vertex.HandleTypeB == HandleType.AlignedLength || vertex.HandleTypeB == HandleType.Aligned)
                        vertex.HandleTypeB = HandleType.Free;
                }
                if (is3d)
                {
                    if (isLeft)
                    {
                        if (!vertex.LeftHandleLockX) vertex.LeftHandle.x = offset.x;
                        if (!vertex.LeftHandleLockY) vertex.LeftHandle.y = offset.z;
                        if (!vertex.LeftHandleLockZ) vertex.LeftHandleHeight = offset.y;
                        vertex.ApplyHandleType(1, true);
                    }
                    else
                    {
                        if (!vertex.RightHandleLockX) vertex.RightHandle.x = offset.x;
                        if (!vertex.RightHandleLockY) vertex.RightHandle.y = offset.z;
                        if (!vertex.RightHandleLockZ) vertex.RightHandleHeight = offset.y;
                        vertex.ApplyHandleType(2, true);
                    }
                }
                else
                {
                    Vector2 pos2 = curve.MapFromWorld(newPos) - vertex.Position;
                    if (isLeft)
                    {
                        if (!vertex.LeftHandleLockX) vertex.LeftHandle.x = pos2.x;
                        if (!vertex.LeftHandleLockY) vertex.LeftHandle.y = pos2.y;
                        vertex.ApplyHandleType(1, false);
                    }
                    else
                    {
                        if (!vertex.RightHandleLockX) vertex.RightHandle.x = pos2.x;
                        if (!vertex.RightHandleLockY) vertex.RightHandle.y = pos2.y;
                        vertex.ApplyHandleType(2, false);
                    }
                }
                break;
        }
    }
}