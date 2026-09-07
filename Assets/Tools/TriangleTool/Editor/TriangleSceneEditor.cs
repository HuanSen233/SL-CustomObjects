using ToolLib;
using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// SceneView triangle interaction editor — select and drag a face's three points. Reuses the shared
    /// 0ToolLib drag utilities (ray/plane projection, snap, move-handle gizmo check) so point dragging and
    /// the Unity Move-tool (W) adaptation stay consistent with the curve tool.
    /// SceneView 三角面交互编辑器 — 选中并拖拽一个面的三个点。复用 0ToolLib 共享拖拽工具
    /// （射线/平面投影、吸附、移动手柄 Gizmo 判定），使点拖拽与 Unity 移动工具（W）适配与曲线工具一致。
    /// </summary>
    public static class TriangleSceneEditor
    {
        /// <summary>Point hit radius (world units, scaled by the view). / 点命中半径（世界单位，随视图缩放）</summary>
        private static float PointHitRadius => 0.28f;

        private static bool _isDragging;
        private static bool _undoRecorded;
        private static int _draggedVertexIndex;   // 0..2 / 被拖拽的顶点索引
        private static Vector3 _dragStartPos;     // dragged point at drag start / 拖拽起始时的点位置
        private static Vector3 _dragStartMouse;   // projected mouse at drag start / 拖拽起始投影位置
        private static Vector3 _dragNormal;       // face plane normal at drag start (stable during drag) / 拖拽起始的面法线（拖拽期间稳定）
        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;
            SceneView.duringSceneGui += OnSceneGUI;
            _registered = true;
        }

        public static void Unregister()
        {
            if (!_registered) return;
            SceneView.duringSceneGui -= OnSceneGUI;
            _registered = false;
        }

        /// <summary>SceneView entry point: dispatches events only while the tool is in edit mode.
        /// SceneView 入口：仅当工具处于编辑模式时分发事件。</summary>
        private static void OnSceneGUI(SceneView sv)
        {
            var w = TriangleTool.Instance;
            if (w == null || !w.IsEditMode) { _isDragging = false; _undoRecorded = false; ToolCursorInteraction.Reset(); return; }
            var m = TriangleFaceManager.Instance;
            if (m == null) return;

            Event e = Event.current;
            int cid = GUIUtility.GetControlID(FocusType.Passive);
            bool moveEdit = w.UseMoveTool && Tools.current == Tool.Move;

            // Consume events in edit mode to block default scene selection/orbit, except for move-tool
            // editing where AddDefaultControl would starve the PositionHandle.
            // 编辑模式下消耗事件阻止 Unity 默认场景选择/轨道；仅移动工具编辑例外（AddDefaultControl 会抢占 PositionHandle）。
            if (!moveEdit) HandleUtility.AddDefaultControl(cid);

            switch (e.type)
            {
                case EventType.MouseDown: HandleMouseDown(e, m, w, moveEdit); break;
                case EventType.MouseDrag: HandleMouseDrag(e, m, w, moveEdit); break;
                case EventType.MouseUp: HandleMouseUp(e); break;
            }

            if (moveEdit)
                DrawMoveToolHandle(m, w);
        }

        // ===== Helpers / 辅助 =====

        private static Vector3 FacePoint(TriangleFace face, int v) => v switch
        {
            0 => face.P1,
            1 => face.P2,
            2 => face.P3,
            _ => face.P1,
        };

        private static void SetFacePoint(TriangleFace face, int v, Vector3 p)
        {
            if (v == 0) face.P1 = p;
            else if (v == 1) face.P2 = p;
            else face.P3 = p;
        }

        /// <summary>Computes the face plane normal from its three points (fallback to World Up when degenerate).
        /// 计算面平面法线（三点共线退化时回退到世界 Y）。</summary>
        private static Vector3 FaceNormal(TriangleFace face)
        {
            Vector3 n = Vector3.Cross(face.P2 - face.P1, face.P3 - face.P1);
            return n.sqrMagnitude < 1e-6f ? Vector3.up : n.normalized;
        }

        private static Vector3 SnapStep(TriangleTool w, bool ctrlSnap)
            => SceneDragUtility.GetSnapStep(w.UseEditorSnapSettings, ctrlSnap, w.SnapGridSize, w.SnapIncrementMove);

        // ===== Mouse events / 鼠标事件 =====

        private static void HandleMouseDown(Event e, TriangleFaceManager m, TriangleTool w, bool moveEdit)
        {
            if (e.button != 0 || e.alt) return;

            if (!TryHitPoint(m, e, out int hitFaceIndex, out int hitVertex))
            {
                // Try the shared tool cursor before clearing the selection (empty-space behavior).
                // 先尝试共享工具游标，再走空白点击清空选中逻辑。
                if (ToolCursorInteraction.TryHitCursor(m, e, w.CursorDisplaySize))
                {
                    ToolCursorInteraction.BeginDrag();
                    e.Use();
                    SceneView.RepaintAll();
                    w.Repaint();
                    return;
                }

                // Empty-space click: clear the selection, unless it lands on the move-handle gizmo.
                // 空白点击：清空选中；但落在移动手柄 Gizmo 上时不清空。
                if (moveEdit && m.SelectedFace != null &&
                    SceneDragUtility.IsOnHandleGizmo(FacePoint(m.SelectedFace, m.SelectedVertexIndex), e.mousePosition))
                    return;
                m.ClearSelection();
                _isDragging = false;
                e.Use();
                SceneView.RepaintAll();
                w.Repaint();
                return;
            }

            // Select the face and its vertex.
            // 选中面及其顶点。
            int faceIndex = hitFaceIndex;
            if (m.SelectedFaceIndex != faceIndex) m.Select(faceIndex);
            m.SelectedVertexIndex = hitVertex;
            SceneView.RepaintAll();

            if (moveEdit)
            {
                // Move-tool editing: select only (let the PositionHandle drive the drag); do not consume.
                // 移动工具编辑：仅选中（交给 PositionHandle 驱动拖拽）；不消费事件。
                w.Repaint();
                return;
            }

            // Built-in drag start.
            // 工具自带拖拽开始。
            var face = m.Faces[faceIndex];
            _draggedVertexIndex = hitVertex;
            _dragStartPos = FacePoint(face, hitVertex);
            _dragNormal = FaceNormal(face);
            _dragStartMouse = SceneDragUtility.ProjectMouseOnPlane(e, _dragNormal, _dragStartPos);

            bool snapActive = EditorSnapSettings.gridSnapEnabled || e.control || e.command;
            if (snapActive)
                _dragStartMouse = SceneDragUtility.SnapVector3(_dragStartMouse, SnapStep(w, e.control || e.command));

            _isDragging = true;
            _undoRecorded = false;
            e.Use();
            SceneView.RepaintAll();
            w.Repaint();
        }

        private static void HandleMouseDrag(Event e, TriangleFaceManager m, TriangleTool w, bool moveEdit)
        {
            // Shared tool cursor drag / 共享工具游标拖拽
            if (ToolCursorInteraction.IsDragging)
            {
                Vector3 snapStep = SnapStep(w, e != null && (e.control || e.command));
                ToolCursorInteraction.Drag(m, e, EditorSnapSettings.gridSnapEnabled || (e.control || e.command), snapStep,
                    () => Undo.RecordObject(m, "移动游标"), () => m.MarkDirty());
                w.Repaint();
                return;
            }

            if (!_isDragging) return;
            // Move-tool editing: delegated to the PositionHandle; do not consume.
            // 移动工具编辑：交给 PositionHandle；不消费事件。
            if (moveEdit) return;

            var face = m.SelectedFace;
            if (face == null || _draggedVertexIndex < 0 || _draggedVertexIndex > 2) return;

            Vector3 current = SceneDragUtility.ProjectMouseOnPlane(e, _dragNormal, _dragStartPos);
            bool snapActive = EditorSnapSettings.gridSnapEnabled || e.control || e.command;
            if (snapActive)
                current = SceneDragUtility.SnapVector3(current, SnapStep(w, e.control || e.command));

            Vector3 newPos = _dragStartPos + (current - _dragStartMouse);

            if (!_undoRecorded) { Undo.RecordObject(m, "移动三角面顶点"); _undoRecorded = true; }
            SetFacePoint(face, _draggedVertexIndex, newPos);
            m.MarkDirty();
            e.Use();
            SceneView.RepaintAll();
            w.Repaint();
        }

        private static void HandleMouseUp(Event e)
        {
            if (_isDragging && e.button == 0)
            {
                _isDragging = false;
                _undoRecorded = false;
                e.Use();
            }
            ToolCursorInteraction.EndDrag(e);
        }

        // ===== Move tool adaptation / 移动工具适配 =====

        /// <summary>Draws a PositionHandle for the selected face's vertex and writes the dragged result back.
        /// 为选中面的顶点绘制 PositionHandle，并将拖拽结果写回。</summary>
        private static void DrawMoveToolHandle(TriangleFaceManager m, TriangleTool w)
        {
            var face = m.SelectedFace;
            if (face == null || m.SelectedVertexIndex < 0 || m.SelectedVertexIndex > 2) return;

            Vector3 worldPos = FacePoint(face, m.SelectedVertexIndex);
            Event e = Event.current;
            bool ctrlSnap = e != null && (e.control || e.command);
            bool snapActive = EditorSnapSettings.gridSnapEnabled || ctrlSnap;
            Vector3 snapStep = SnapStep(w, ctrlSnap);

            if (SceneMoveTool.DrawPositionHandle(m, worldPos, Quaternion.identity, snapStep, snapActive,
                    ref _undoRecorded, "移动三角面顶点", out Vector3 newPos))
            {
                SetFacePoint(face, m.SelectedVertexIndex, newPos);
                m.MarkDirty();
                SceneView.RepaintAll();
                w.Repaint();
            }
        }

        // ===== Hit testing / 命中检测 =====

        /// <summary>Hits a visible face's vertex by ray-point distance (returns false on a miss).
        /// Prioritizes the currently-selected face's vertices over other faces', so when two faces overlap
        /// at a shared point, the vertex of the face selected in the list wins.
        /// 命中可见面的某顶点（射线-点距离），未命中返回 false。
        /// 优先当前选中面的顶点，再扫其余面——两面的顶点重叠时，列表选中面的顶点优先。</summary>
        private static bool TryHitPoint(TriangleFaceManager m, Event e, out int faceIndex, out int vertex)
        {
            faceIndex = -1;
            vertex = -1;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            float bestDist = float.MaxValue;

            // Priority: the selected face's vertices first.
            // 优先级：先扫当前选中面的顶点。
            int selIdx = m.SelectedFaceIndex;
            if (selIdx >= 0 && selIdx < m.Faces.Count && m.Faces[selIdx] != null && m.Faces[selIdx].IsVisible)
            {
                if (ScanFaceVertices(m.Faces[selIdx], ray, ref bestDist, out int v))
                {
                    faceIndex = selIdx;
                    vertex = v;
                    return true;
                }
            }

            // Then the rest of the faces (in list order).
            // 再扫其余面（按列表顺序）。
            for (int fi = 0; fi < m.Faces.Count; fi++)
            {
                if (fi == selIdx) continue;
                var f = m.Faces[fi];
                if (f == null || !f.IsVisible) continue;
                if (ScanFaceVertices(f, ray, ref bestDist, out int v))
                {
                    faceIndex = fi;
                    vertex = v;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Scans the three vertices of one face, returning the closest within the hit radius.
        /// Updates bestDist/reference distance. / 扫描单面三个顶点，返回命中半径内最近的一个；更新参考距离。</summary>
        private static bool ScanFaceVertices(TriangleFace face, Ray ray, ref float bestDist, out int vertex)
        {
            vertex = -1;
            int bestV = -1;
            float bestVd = float.MaxValue;
            for (int v = 0; v < 3; v++)
            {
                Vector3 p = FacePoint(face, v);
                float d = SceneDragUtility.RayPointDist(ray, p);
                if (d < PointHitRadius * HandleUtility.GetHandleSize(p) && d < bestVd)
                {
                    bestVd = d;
                    bestV = v;
                }
            }
            if (bestV < 0) return false;
            vertex = bestV;
            bestDist = bestVd;
            return true;
        }
    }
}
