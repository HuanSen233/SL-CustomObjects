using System.Collections.Generic;
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
            case 1: target = MoveHandleTarget.LeftHandle; worldPos = is3d ? vertex.PositionV3 + vertex.LeftHandleOffsetV3 : curve.MapToWorld(vertex.LeftHandlePosition); break;
            case 2: target = MoveHandleTarget.RightHandle; worldPos = is3d ? vertex.PositionV3 + vertex.RightHandleOffsetV3 : curve.MapToWorld(vertex.RightHandlePosition); break;
            // 顶点自身及高度箭头（4/5/6）：统一在顶点处显示手柄，Y 轴即调整高度
            default: target = MoveHandleTarget.Vertex; worldPos = vertexWorld; break;
        }

        // Align the handle to the editing plane for 2D curves so it does not pull vertices out of plane.
        // 2D 曲线对手柄对齐编辑平面，避免把顶点拉出平面
        Quaternion rot = is3d ? Quaternion.identity : Quaternion.LookRotation(curve.PlaneNormal);
        EditorGUI.BeginChangeCheck();
        Vector3 newPos = Handles.PositionHandle(worldPos, rot);
        if (EditorGUI.EndChangeCheck())
        {
            if (!_undoRecorded) { Undo.RecordObject(m, "移动曲线元素"); _undoRecorded = true; }
            WriteMoveHandleTarget(curve, vertex, target, newPos, is3d, vertexWorld);
            m.MarkDirty();
            curve.RecalculateHandles();
            SceneView.RepaintAll();
            CurveTool.Instance?.Repaint();
        }
    }

    /// <summary>Writes a move-handle drag result back to vertex/handle data (per-axis locks preserved).
    /// 将移动手柄拖拽结果写回顶点/控制柄数据（保留分轴锁）</summary>
    private static void WriteMoveHandleTarget(BezierCurve curve, CurveVertex vertex, MoveHandleTarget target, Vector3 newPos, bool is3d, Vector3 vertexWorld)
    {
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