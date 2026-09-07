using ToolLib;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SceneView curve interaction editor — mouse/keyboard event handling (drag, select, insert, extend).
/// SceneView 曲线交互编辑器 — 鼠标/键盘事件处理（拖拽、选择、插入、延伸）。
/// </summary>
public static partial class CurveSceneEditor
{
    // ============================================================
    //  Mouse events / 鼠标事件
    // ============================================================

    private static void HandleMouseDown(Event e, CurveManager m, CurveTool w)
    {
        bool shift = e.shift;
        bool alt = e.alt;

        if (e.button == 0)
        {
            if (alt)
            {
                // Alt+Left-click → select the whole Bezier span / Alt+左键 → 选中整条曲线线段（贝塞尔跨度）
                var spanHit = HitTestCurve(m, e, w);
                if (spanHit.curveIndex >= 0 && !m.Curves[spanHit.curveIndex].IsLocked)
                {
                    // Select all micro-segments of that span / 选中该跨度对应的所有小线段
                    var curve = m.Curves[spanHit.curveIndex];
                    int startSeg = spanHit.segmentIndex * curve.SegmentCount;
                    int endSeg = startSeg + curve.SegmentCount - 1;
                    if (!shift) m.ClearSelection();
                    m.SelectedCurveIndex = spanHit.curveIndex;
                    curve.IsSelected = true;
                    for (int si = startSeg; si <= endSeg && si < curve.Segments.Count; si++)
                        curve.Segments[si].IsSelected = true;
                    curve.SelectedSegmentIndex = startSeg;
                    e.Use();
                    SceneView.RepaintAll();
                    w.Repaint();
                    return;
                }
            }
            else
            {
                // Plain left-click → priority: vertex/handle > micro-segment > clear selection / 普通左键 → 优先级: 顶点/曲柄 > 小线段 > 清空
                var hit = HitTest(m, e, w);
                if (hit.curveIndex >= 0)
                {
                    if (m.Curves[hit.curveIndex].IsLocked) { e.Use(); return; }
                    m.Select(hit.curveIndex, hit.vertexIndex, hit.subElement, shift);
                    if (w.UseMoveTool && Tools.current == Tool.Move)
                    {
                        // Move-tool editing: select only (so the PositionHandle shows); do NOT set _isDragging
                        // and do NOT consume the event, letting the PositionHandle take over the drag.
                        // 移动工具编辑：仅选中（使 PositionHandle 显示）；不进入自写拖拽、不消费事件，让 PositionHandle 接管拖拽。
                        w.Repaint();
                        return;
                    }
                    _isDragging = true;
                    _undoRecorded = false;
                    _dragStartMouse = GetMouseWorldPos(e, w);
                    _dragStartMouse3D = m.SelectedVertex != null ? GetMouseWorldPos3D(e, m.SelectedVertex.Height) : Vector3.zero;
                    _dragStartValue = GetElementValue(m);
                    // Save the start handle 3D offset (for 3D curves) / 保存起始控制柄 3D 偏移（3D 曲线用）
                    if (m.SelectedVertex != null && m.SelectedCurve != null && m.SelectedCurve.Is3D)
                    {
                        var sv = m.SelectedVertex;
                        _dragStartHandle3DOffset = sv.SelectedSubElement switch
                        {
                            1 => sv.LeftHandleOffsetV3,
                            2 => sv.RightHandleOffsetV3,
                            _ => Vector3.zero,
                        };
                    }
                    // Save the screen position (for Y-axis dragging) / 保存屏幕位置（Y 轴拖拽用）
                    _dragStartScreenY = e.mousePosition.y;
                    // Save the start height for Y-axis dragging / 保存 Y 轴拖拽起始高度
                    if (m.SelectedVertex != null)
                    {
                        var sv = m.SelectedVertex;
                        _dragStartArrowHeight = sv.SelectedSubElement switch
                        {
                            4 => sv.Height,
                            5 => sv.LeftHandleHeight,
                            6 => sv.RightHandleHeight,
                            _ => _dragStartMouse3D.y,
                        };
                    }
                    // Save the initial values of all multi-selected vertices / 保存所有多选顶点的初始值
                    SaveDragStartValues(m);
                    SnapDragStartIfNeeded(e, m);
                    e.Use();
                    w.Repaint();
                }
                else
                {
                    var segHit = HitTestSegment(m, e, w);
                    if (segHit.curveIndex >= 0 && !m.Curves[segHit.curveIndex].IsLocked)
                    {
                        m.SelectSegment(segHit.curveIndex, segHit.segmentIndex, shift);
                        e.Use();
                        SceneView.RepaintAll();
                        w.Repaint();
                    }
                    else if (!shift)
                    {
                        // Try to hit the cursor / 尝试命中游标
                        if (ToolCursorInteraction.TryHitCursor(m, e, CurveTool.Instance?.CursorDisplaySize ?? 0.15f))
                        {
                            ToolCursorInteraction.BeginDrag();
                            e.Use();
                        }
                        else
                        {
                            // Move-tool editing: a click that lands ON the move-handle gizmo (its axis arrows /
                            // center) must NOT clear the selection, otherwise the move handle disappears and its
                            // arrows can never be clicked — leave it to the PositionHandle. A click on truly empty
                            // space clears the selection, matching the built-in drag behavior.
                            // 移动工具编辑：点在移动手柄 Gizmo（轴箭头/中心）上不能清空选中，否则手柄消失、
                            // 轴箭头永远无法点选——交给 PositionHandle；点在真正空白处则清空选中，与自带拖拽一致。
                            if (w.UseMoveTool && Tools.current == Tool.Move)
                            {
                                if (IsOnMoveHandleGizmo(m, e.mousePosition)) return;
                                m.ClearSelection();
                                _isDragging = false;
                                e.Use();
                                SceneView.RepaintAll();
                                w.Repaint();
                                return;
                            }
                            m.ClearSelection();
                            _isDragging = false;
                            e.Use();
                            SceneView.RepaintAll();
                            w.Repaint();
                        }
                    }
                }
            }
        }
        else if (e.button == 1 && alt)
        {
            // Alt+Right-click → insert a vertex on the curve span / Alt+右键 → 在曲线段上插入顶点
            var ch = HitTestCurve(m, e, w);
            if (ch.curveIndex >= 0 && ch.segmentIndex >= 0 && !m.Curves[ch.curveIndex].IsLocked)
            {
                var curve = m.Curves[ch.curveIndex];
                Vector2 ip = GetMouseWorldPos(e, w, curve.Plane);
                // Clear the curve selection state first / 先清理曲线选中状态
                curve.IsSelected = false;
                foreach (var v in curve.Vertices) { v.IsSelected = false; v.SelectedSubElement = 0; }
                Undo.RecordObject(m, "插入顶点");
                // 3D curves: place the new vertex on the curve itself, not at the ray-ground intersection / 3D 曲线：将新顶点放在曲线本身上，而非射线与地面的交点
                CurveVertex nv;
                if (curve.Is3D)
                {
                    var pts3 = curve.SamplePoints3D();
                    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    int startSeg = ch.segmentIndex * curve.SegmentCount;
                    int endSeg = Mathf.Min(startSeg + curve.SegmentCount, pts3.Count - 1);
                    float bestDist = float.MaxValue;
                    Vector3 bestPoint = Vector3.zero;
                    for (int i = startSeg; i < endSeg; i++)
                    {
                        Vector3 mid = (pts3[i] + pts3[i + 1]) * 0.5f;
                        float d = RayPointDist(ray, mid);
                        if (d < bestDist) { bestDist = d; bestPoint = mid; }
                    }
                    nv = new CurveVertex(curve.MapFromWorld(bestPoint)) { Height = bestPoint.y };
                }
                else
                {
                    nv = new CurveVertex(ip);
                }
                int insertAt = ch.segmentIndex + 1;
                if (insertAt > curve.Vertices.Count) insertAt = curve.Vertices.Count;
                curve.Vertices.Insert(insertAt, nv);
                curve.RebuildSegments();
                curve.RecalculateHandles();
                m.Select(ch.curveIndex, insertAt, 3);
                m.MarkDirty();
                e.Use();
                SceneView.RepaintAll();
                w.Repaint();
            }
        }
        else if (e.button == 1 && e.control)
        {
            // Ctrl+Right-click → extend a new vertex from the selected endpoint / Ctrl+右键 → 从选中端点延伸新顶点
            var sv = m.SelectedVertex;
            var sc = m.SelectedCurve;
            if (sv != null && sc != null && !sc.IsLoop)
            {
                int idx = m.SelectedVertexIndex;
                bool isEndpoint = (idx == 0 || idx == sc.Vertices.Count - 1);
                if (isEndpoint)
                {
                    Vector2 ip = GetMouseWorldPos(e, w);
                    Undo.RecordObject(m, "延伸顶点");
                    var nv = new CurveVertex(ip);
                    if (idx == 0) sc.Vertices.Insert(0, nv);
                    else sc.Vertices.Add(nv);
                    sc.RebuildSegments();
                    sc.RecalculateHandles();
                    m.Select(m.SelectedCurveIndex, idx == 0 ? 0 : sc.Vertices.Count - 1, 3);
                    m.MarkDirty();
                    e.Use();
                    SceneView.RepaintAll();
                    w.Repaint();
                }
            }
        }
    }

    private static void HandleMouseDrag(Event e, CurveManager m, CurveTool w)
    {
        // Cursor dragging (shared interaction) / 游标拖拽（共享交互）
        if (ToolCursorInteraction.IsDragging)
        {
            bool snapActive = EditorSnapSettings.gridSnapEnabled || e.control || e.command;
            Vector3 snapStep = GetSnapStep(w, e.control || e.command);
            ToolCursorInteraction.Drag(m, e, snapActive, snapStep,
                () => Undo.RecordObject(m, "移动游标"), () => m.MarkDirty());
            CurveTool.Instance?.Repaint();
            return;
        }

        if (!_isDragging) return;
        // Move-tool editing enabled: vertex/handle movement is delegated to the Unity Move tool (PositionHandle).
        // Do not consume the event, so the PositionHandle can receive the drag.
        // 移动工具编辑启用：顶点/控制柄移动交由 Unity 移动工具（PositionHandle）处理；不消费事件，让 PositionHandle 收到拖拽。
        if (w.UseMoveTool && Tools.current == Tool.Move) { return; }
        var vertex = m.SelectedVertex;
        if (vertex == null) return;

        Vector2 raw = GetMouseWorldPos(e, w);
        // Grid/increment snap: Ctrl → increment step, otherwise grid size / 网格吸附/增量吸附：Ctrl→Increment Snap Move，无Ctrl→Grid Size
        bool ctrlSnap = e.control || e.command;
        bool useSnap = EditorSnapSettings.gridSnapEnabled || ctrlSnap;
        Vector3 snapGs = GetSnapStep(w, ctrlSnap);
        var curve = m.SelectedCurve;
        if (curve != null && useSnap)
        {
            Vector3 world3D = curve.MapToWorld(raw);
            world3D = SnapVector3(world3D, snapGs);
            raw = curve.MapFromWorld(world3D);
        }

        Vector2 delta = raw - _dragStartMouse;
        if (!_undoRecorded) { Undo.RecordObject(m, "移动曲线元素"); _undoRecorded = true; }

        // Dragging Auto/Vector/Aligned/Mirror handles auto-switches them to Free (per-side, matching the UI behavior)
        // 拖拽 Auto/Vector/Aligned/Mirror 柄时自动切为 Free（分别检测左右柄类型，与 UI 编辑行为一致）
        if ((vertex.SelectedSubElement == 1 || vertex.SelectedSubElement == 5) &&
            (vertex.HandleTypeA == HandleType.Auto || vertex.HandleTypeA == HandleType.Vector ||
             vertex.HandleTypeA == HandleType.AlignedLength || vertex.HandleTypeA == HandleType.Aligned))
            vertex.HandleTypeA = HandleType.Free;
        if ((vertex.SelectedSubElement == 2 || vertex.SelectedSubElement == 6) &&
            (vertex.HandleTypeB == HandleType.Auto || vertex.HandleTypeB == HandleType.Vector ||
             vertex.HandleTypeB == HandleType.AlignedLength || vertex.HandleTypeB == HandleType.Aligned))
            vertex.HandleTypeB = HandleType.Free;

        bool is3dVertex = m.SelectedCurve != null && m.SelectedCurve.Is3D;
        Vector3 delta3d = is3dVertex ? GetMouseWorldPos3D(e, _dragStartMouse3D.y) - _dragStartMouse3D : Vector3.zero;

        switch (vertex.SelectedSubElement)
        {
            case 3:
            {
                if (is3dVertex)
                {
                    Vector3 startPos3 = new Vector3(_dragStartValue.x, _dragStartMouse3D.y, _dragStartValue.y);
                    Vector3 newPos3 = startPos3 + delta3d;
                    if (useSnap)
                    {
                        newPos3 = SnapVector3(newPos3, snapGs);
                    }
                    if (vertex.LockX) newPos3.x = vertex.Position.x;
                    if (vertex.LockY) newPos3.z = vertex.Position.y;
                    if (vertex.LockZ) newPos3.y = vertex.Height;
                    vertex.Position = new Vector2(newPos3.x, newPos3.z);
                    vertex.Height = newPos3.y;
                }
                else
                {
                    Vector2 newPos = _dragStartValue + delta;
                    if (vertex.LockX) newPos.x = vertex.Position.x;
                    if (vertex.LockY) newPos.y = vertex.Position.y;
                    vertex.Position = newPos;
                }
                break;
            }
            case 1:
                if (is3dVertex)
                {
                    Vector3 lhNew = _dragStartHandle3DOffset + delta3d;
                    if (useSnap)
                    {
                        lhNew = SnapVector3(lhNew, snapGs);
                    }
                    if (vertex.LeftHandleLockX) lhNew.x = vertex.LeftHandle.x;
                    if (vertex.LeftHandleLockY) lhNew.z = vertex.LeftHandle.y;
                    if (vertex.LeftHandleLockZ) lhNew.y = vertex.LeftHandleHeight;
                    vertex.LeftHandle = new Vector2(lhNew.x, lhNew.z);
                    vertex.LeftHandleHeight = lhNew.y;
                }
                else
                {
                    Vector2 newLH = _dragStartValue + delta - vertex.Position;
                    if (vertex.LeftHandleLockX) newLH.x = vertex.LeftHandle.x;
                    if (vertex.LeftHandleLockY) newLH.y = vertex.LeftHandle.y;
                    vertex.LeftHandle = newLH;
                }
                vertex.ApplyHandleType(1, is3dVertex);
                break;
            case 2:
                if (is3dVertex)
                {
                    Vector3 rhNew = _dragStartHandle3DOffset + delta3d;
                    if (useSnap)
                    {
                        rhNew = SnapVector3(rhNew, snapGs);
                    }
                    if (vertex.RightHandleLockX) rhNew.x = vertex.RightHandle.x;
                    if (vertex.RightHandleLockY) rhNew.z = vertex.RightHandle.y;
                    if (vertex.RightHandleLockZ) rhNew.y = vertex.RightHandleHeight;
                    vertex.RightHandle = new Vector2(rhNew.x, rhNew.z);
                    vertex.RightHandleHeight = rhNew.y;
                }
                else
                {
                    Vector2 newRH = _dragStartValue + delta - vertex.Position;
                    if (vertex.RightHandleLockX) newRH.x = vertex.RightHandle.x;
                    if (vertex.RightHandleLockY) newRH.y = vertex.RightHandle.y;
                    vertex.RightHandle = newRH;
                }
                vertex.ApplyHandleType(2, is3dVertex);
                break;
            case 4:
                if (is3dVertex)
                {
                    float screenD = _dragStartScreenY - e.mousePosition.y;
                    float worldPerPix = HandleUtility.GetHandleSize(vertex.PositionV3) * 0.0125f;
                    float newHeight = _dragStartArrowHeight + screenD * worldPerPix;
                    if (useSnap) newHeight = SnapFloat(newHeight, snapGs.y);
                    if (vertex.LockZ) newHeight = vertex.Height;
                    vertex.Height = newHeight;
                }
                break;
            case 5:
                if (is3dVertex)
                {
                    float screenL = _dragStartScreenY - e.mousePosition.y;
                    float wppL = HandleUtility.GetHandleSize(vertex.PositionV3) * 0.0125f;
                    float newLH = _dragStartArrowHeight + screenL * wppL;
                    if (useSnap) newLH = SnapFloat(newLH, snapGs.y);
                    if (vertex.LeftHandleLockZ) newLH = vertex.LeftHandleHeight;
                    vertex.LeftHandleHeight = newLH;
                    vertex.ApplyHandleType(1, is3dVertex);
                }
                break;
            case 6:
                if (is3dVertex)
                {
                    float screenR = _dragStartScreenY - e.mousePosition.y;
                    float wppR = HandleUtility.GetHandleSize(vertex.PositionV3) * 0.0125f;
                    float newRH = _dragStartArrowHeight + screenR * wppR;
                    if (useSnap) newRH = SnapFloat(newRH, snapGs.y);
                    if (vertex.RightHandleLockZ) newRH = vertex.RightHandleHeight;
                    vertex.RightHandleHeight = newRH;
                    vertex.ApplyHandleType(2, is3dVertex);
                }
                break;
        }

        // Batch-move multi-selected vertices / 批量移动多选顶点
        if (m.SelectedVertexIndices.Count > 1 && _dragStartValues != null)
        {
            curve = m.SelectedCurve;
            int sub = vertex.SelectedSubElement;
            foreach (int vi in m.SelectedVertexIndices)
            {
                if (vi == m.SelectedVertexIndex) continue;
                if (!_dragStartValues.TryGetValue(vi, out Vector2 initVal)) continue;
                var ov = curve.Vertices[vi];
                switch (sub)
                {
                    case 3:
                    {
                        if (is3dVertex)
                        {
                            Vector3 mvStart3 = new Vector3(initVal.x, _dragStartMouse3D.y, initVal.y);
                            Vector3 mvNew3 = mvStart3 + delta3d;
                            if (useSnap)
                            {
                                mvNew3 = SnapVector3(mvNew3, snapGs);
                            }
                            if (ov.LockX) mvNew3.x = ov.Position.x;
                            if (ov.LockY) mvNew3.z = ov.Position.y;
                            if (ov.LockZ) mvNew3.y = ov.Height;
                            ov.Position = new Vector2(mvNew3.x, mvNew3.z);
                            ov.Height = mvNew3.y;
                        }
                        else
                        {
                            Vector2 mvNew = initVal + delta;
                            if (ov.LockX) mvNew.x = ov.Position.x;
                            if (ov.LockY) mvNew.y = ov.Position.y;
                            ov.Position = mvNew;
                        }
                        break;
                    }
                    case 1: ov.LeftHandle = initVal + delta - ov.Position; ov.ApplyHandleType(1, is3dVertex); break;
                    case 2: ov.RightHandle = initVal + delta - ov.Position; ov.ApplyHandleType(2, is3dVertex); break;
                }
            }
        }
        m.MarkDirty();
        m.SelectedCurve?.RecalculateHandles();
        e.Use();
        SceneView.RepaintAll();
        w.Repaint();
    }

    private static void HandleMouseUp(Event e)
    {
        if (_isDragging && e.button == 0) { _isDragging = false; _undoRecorded = false; e.Use(); }
        ToolCursorInteraction.EndDrag(e);
    }

    private static void HandleKeyDown(Event e, CurveManager m)
    {
        if (e.keyCode == KeyCode.Delete && m.SelectedVertex != null)
        {
            var curve = m.SelectedCurve;
            if (curve != null && curve.Vertices.Count > 2)
            {
                Undo.RecordObject(m, "删除顶点");
                m.RemoveSelectedVertex();
                m.MarkDirty();
                e.Use();
                SceneView.RepaintAll();
                CurveTool.Instance?.Repaint();
            }
        }
    }
}
