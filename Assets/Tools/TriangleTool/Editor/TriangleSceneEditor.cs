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

        private static bool _undoRecorded;
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

        /// <summary>SceneView entry point: dispatches events only while the tool is in edit mode and the
        /// Unity Move tool (W) is active. / SceneView 入口：仅当工具处于编辑模式且 Unity 移动工具(W)激活时分发事件。</summary>
        private static void OnSceneGUI(SceneView sv)
        {
            var w = TriangleTool.Instance;
            if (w == null || !w.IsEditMode) { _undoRecorded = false; return; }
            var m = TriangleFaceManager.Instance;
            if (m == null) return;

            Event e = Event.current;
            // Move-tool-only editing: the tool is inert unless the Unity Move tool (W) is active; the built-in
            // drag path is cut off (deprecated) and only the PositionHandle drives movement.
            // 仅移动工具(W)编辑：非移动工具状态下工具不响应（切断并废弃自带拖拽）；仅 PositionHandle 驱动移动。
            bool moveEdit = w.UseMoveTool && Tools.current == Tool.Move;
            if (!moveEdit) return;

            switch (e.type)
            {
                case EventType.MouseDown: HandleMouseDown(e, m, w); break;
                case EventType.MouseDrag: HandleMouseDrag(e, m, w); break;
                case EventType.MouseUp: HandleMouseUp(e); break;
            }

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

        private static void HandleMouseDown(Event e, TriangleFaceManager m, TriangleTool w)
        {
            if (e.button != 0 || e.alt) return;

            if (!TryHitPoint(m, e, out int hitFaceIndex, out int hitVertex))
            {
                // Try the shared tool cursor before clearing the selection. / 先尝试共享工具游标。
                if (ToolCursorInteraction.TryHitCursor(m, e, w.CursorDisplaySize))
                {
                    ToolCursorInteraction.SetCursorSelected(true);
                    m.ClearSelection();
                    // Do NOT consume the mouse-down: let the cursor PositionHandle grab the drag.
                    // 不消费鼠标按下：让游标 PositionHandle 能够接管拖拽。
                    SceneView.RepaintAll();
                    w.Repaint();
                    return;
                }

                // A click on the move-handle gizmo must NOT clear the selection. / 点击移动手柄 Gizmo 不清空选中。
                if (m.SelectedFace != null &&
                    SceneDragUtility.IsOnHandleGizmo(FacePoint(m.SelectedFace, m.SelectedVertexIndex), e.mousePosition))
                    return;
                ToolCursorInteraction.SetCursorSelected(false);
                m.ClearSelection();
                e.Use();
                SceneView.RepaintAll();
                w.Repaint();
                return;
            }

            // Select the face and its vertex (cursor deselected). / 选中面及其顶点（取消游标选中）。
            ToolCursorInteraction.SetCursorSelected(false);
            int faceIndex = hitFaceIndex;
            if (m.SelectedFaceIndex != faceIndex) m.Select(faceIndex);
            m.SelectedVertexIndex = hitVertex;
            SceneView.RepaintAll();
            w.Repaint();
        }

        private static void HandleMouseDrag(Event e, TriangleFaceManager m, TriangleTool w)
        {
            // Move-tool-only editing: the PositionHandle drives movement; the built-in drag is cut off.
            // 仅移动工具(W)编辑：由 PositionHandle 驱动移动；自带拖拽已切断。
        }

        private static void HandleMouseUp(Event e)
        {
            // Move-tool-only editing: no built-in drag to end. / 仅移动工具(W)编辑：无自带拖拽需要结束。
        }

        // ===== Move tool adaptation / 移动工具适配 =====

        /// <summary>Draws a PositionHandle for the selected face's vertex (or the selected cursor) and writes
        /// the dragged result back. / 为选中面的顶点（或选中游标）绘制 PositionHandle，并将拖拽结果写回。</summary>
        private static void DrawMoveToolHandle(TriangleFaceManager m, TriangleTool w)
        {
            // Cursor selected → PositionHandle for the cursor. / 游标选中 → 为其绘制 PositionHandle。
            if (ToolCursorInteraction.IsCursorSelected)
            {
                Event ce = Event.current;
                bool ctrlSnap = ce != null && (ce.control || ce.command);
                Vector3 csnap = SnapStep(w, ctrlSnap);
                ToolCursorInteraction.DrawMoveHandle(m, csnap,
                    EditorSnapSettings.gridSnapEnabled || ctrlSnap, ref _undoRecorded, () => m.MarkDirty());
                w.Repaint();
                return;
            }

            var face = m.SelectedFace;
            if (face == null || m.SelectedVertexIndex < 0 || m.SelectedVertexIndex > 2) return;

            Vector3 worldPos = FacePoint(face, m.SelectedVertexIndex);
            Event e = Event.current;
            bool eCtrlSnap = e != null && (e.control || e.command);
            bool snapActive = EditorSnapSettings.gridSnapEnabled || eCtrlSnap;
            Vector3 snapStep = SnapStep(w, eCtrlSnap);

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
