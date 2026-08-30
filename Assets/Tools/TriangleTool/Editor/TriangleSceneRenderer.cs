using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// SceneView triangle renderer — draws triangle-face outlines and, in edit mode, the selected face's
    /// three draggable point handles.
    /// Edit mode: wireframe + 3 point spheres (highlight the selected vertex). Preview: highlight the face
    /// outline in its own color (non-interactive presentation), mirroring the curve editor's both-off guard.
    /// SceneView 三角面渲染器 — 绘制三角面轮廓；编辑模式下绘制选中面的三个可拖拽点手柄。
    /// 编辑模式：线框 + 3 个点球（高亮选中的顶点）；预览：按面颜色高亮轮廓（非交互展示），
    /// 与曲线编辑器的"双关即不画"守卫一致。
    /// </summary>
    public static class TriangleSceneRenderer
    {
        private static Color OutlineColor => TriangleTool.Instance?.FaceColor ?? TriangleTool.DefaultFaceColor;
        private static Color LockedColor => new Color(1f, 0.6f, 0.2f);
        private static Color SelectedColor => Color.yellow;
        private static Color P1Color => Color.red;
        private static Color P2Color => Color.green;
        private static Color P3Color => Color.blue;
        private static float PointSize => 0.18f;
        private static float SelectedPointSize => 0.24f;

        public static void Register() => SceneView.duringSceneGui += OnSceneGUI;
        public static void Unregister() => SceneView.duringSceneGui -= OnSceneGUI;

        private static void OnSceneGUI(SceneView sv)
        {
            var w = TriangleTool.Instance;
            if (w == null) return;
            var m = TriangleFaceManager.Instance;
            if (m == null) return;

            // Draw only when edit or preview is active (both-off → nothing).
            // 仅在编辑或预览开启时绘制（都关→不画）。
            if (!w.IsEditMode && !w.IsPreviewOn) return;

            foreach (var face in m.Faces)
            {
                if (face == null || !face.IsVisible) continue;
                DrawFaceOutline(face, w);
                if (w.IsEditMode && face.IsSelected && !face.IsLocked)
                    DrawFacePoints(face, m);
            }
        }

        /// <summary>Draws the three edges of a face, colored by preview/lock/selection state.
        /// 绘制面三条边，按预览/锁定/选中状态着色。</summary>
        private static void DrawFaceOutline(TriangleFace face, TriangleTool w)
        {
            Color c;
            if (face.IsSelected && !face.IsLocked) c = SelectedColor;
            else if (w.IsPreviewOn) c = face.Color;
            else if (face.IsLocked) c = LockedColor;
            else c = OutlineColor;

            Handles.color = c;
            Handles.DrawLine(face.P1, face.P2, 2.2f);
            Handles.DrawLine(face.P2, face.P3, 2.2f);
            Handles.DrawLine(face.P3, face.P1, 2.2f);
        }

        /// <summary>Draws the three draggable point spheres of the selected face; the actively selected
        /// vertex (manager.SelectedVertexIndex) is drawn larger/highlighted. / 绘制选中面的三个可拖拽点球；
        /// 当前选中的顶点（SelectedVertexIndex）画得更大并高亮。</summary>
        private static void DrawFacePoints(TriangleFace face, TriangleFaceManager m)
        {
            DrawPoint(face.P1, 0, m);
            DrawPoint(face.P2, 1, m);
            DrawPoint(face.P3, 2, m);
        }

        private static void DrawPoint(Vector3 p, int index, TriangleFaceManager m)
        {
            bool isSel = m.SelectedVertexIndex == index;
            Handles.color = isSel ? SelectedColor : (index == 0 ? P1Color : index == 1 ? P2Color : P3Color);
            Handles.SphereHandleCap(0, p, Quaternion.identity, isSel ? SelectedPointSize : PointSize, EventType.Repaint);
        }
    }
}
