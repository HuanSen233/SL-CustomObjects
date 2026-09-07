using UnityEditor;
using UnityEngine;

namespace ToolLib
{
    /// <summary>
    /// Shared scene-interaction math used by both the curve tool and the triangle tool for
    /// dragging editable points in the Scene view: ray/plane projection, grid & increment
    /// snapping, point hit-testing, and the move-handle gizmo screen check. Centralizing these
    /// keeps "drag with snap + move-tool adaptation" consistent across tools (one source of truth).
    /// 曲线工具与三角面工具共用的场景交互数学：射线/平面投影、网格与增量吸附、点命中检测、
    /// 移动手柄 Gizmo 屏幕判定。集中于此可让各工具的"拖拽+吸附+移动工具适配"保持一致。
    /// </summary>
    public static class SceneDragUtility
    {
        /// <summary>
        /// Snaps a float to the nearest grid point; returns the raw value when the grid is too
        /// small (prevents division by zero). 将浮点数按网格大小吸附到最近网格点；网格过小时返回原值（防除零）。
        /// </summary>
        public static float SnapFloat(float value, float gridSize)
        {
            if (gridSize < 0.0001f) return value;
            return Mathf.Round(value / gridSize) * gridSize;
        }

        /// <summary>
        /// Snaps a Vector3 to the nearest grid point per-component, reusing SnapFloat's zero-guard.
        /// 将 Vector3 按网格大小逐分量吸附到最近网格点（复用 SnapFloat 的除零保护）。
        /// </summary>
        public static Vector3 SnapVector3(Vector3 value, Vector3 gridSize)
        {
            return new Vector3(
                SnapFloat(value.x, gridSize.x),
                SnapFloat(value.y, gridSize.y),
                SnapFloat(value.z, gridSize.z));
        }

        /// <summary>
        /// Closest distance between a ray and a 3D point (used for hit-testing spheres/points).
        /// 射线到 3D 点的最近距离（用于命中球体/点）。
        /// </summary>
        public static float RayPointDist(Ray ray, Vector3 point)
        {
            Vector3 toPoint = point - ray.origin;
            float t = Vector3.Dot(toPoint, ray.direction);
            if (t < 0f) return float.MaxValue;
            return Vector3.Distance(ray.GetPoint(t), point);
        }

        /// <summary>
        /// Closest distance from a 2D point to a 2D line segment (used for hitting curve spans).
        /// 2D 点到 2D 线段的最近距离（用于命中曲线段）。
        /// </summary>
        public static float PointToSegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float ls = ab.sqrMagnitude;
            if (ls < 0.0001f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ls);
            return Vector2.Distance(p, a + t * ab);
        }

        /// <summary>
        /// Projects a ray onto a world plane defined by (planeNormal, anchor). Returns false when the
        /// ray is parallel to the plane. 把射线投影到由 (planeNormal, anchor) 定义的世界平面；射线与该平面平行时返回 false。
        /// </summary>
        public static bool TryRayPlaneHit(Ray ray, Vector3 planeNormal, Vector3 anchor, out Vector3 hit)
        {
            Plane p = new Plane(planeNormal, anchor);
            if (p.Raycast(ray, out float dist))
            {
                hit = ray.GetPoint(dist);
                return true;
            }
            hit = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Projects the mouse ray onto a world plane; on a parallel fallback it projects onto a
        /// camera-facing plane through the anchor. Returns the resulting world point (anchor on failure).
        /// 把鼠标射线投影到世界平面；平行时退路为过 anchor 且面向相机的平面。返回世界点（失败返回 anchor）。
        /// </summary>
        public static Vector3 ProjectMouseOnPlane(Event e, Vector3 planeNormal, Vector3 anchor)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (TryRayPlaneHit(ray, planeNormal, anchor, out Vector3 hit))
                return hit;

            // Fallback: camera-facing plane through the anchor (mirrors the curve editor's behavior).
            // 退路：过 anchor 且面向相机的平面（与曲线编辑器行为一致）。
            Vector3 camFwd = Camera.current != null ? -Camera.current.transform.forward : Vector3.forward;
            if (TryRayPlaneHit(ray, camFwd, anchor, out Vector3 hit2))
                return hit2;

            return anchor;
        }

        /// <summary>
        /// Resolves the current snap step: editor snap settings when useEditorSnap is true (follows
        /// Edit > Snap Settings), else the tool-local increment (Ctrl) or grid size.
        /// 计算当前吸附步长：useEditorSnap 为 true 时用编辑器吸附设置（跟随 Edit > Snap Settings），
        /// 否则用工具自身的增量（Ctrl）或网格尺寸。
        /// </summary>
        public static Vector3 GetSnapStep(bool useEditorSnap, bool ctrlSnap, Vector3 gridSize, Vector3 incrementMove)
        {
            if (useEditorSnap)
                return EditorSnapSettings.move;
            return ctrlSnap ? incrementMove : gridSize;
        }

        /// <summary>
        /// Whether a screen position falls on the Unity move-handle gizmo (center + axis arrows) of a
        /// world-space target, so an empty-space click there must NOT clear the selection. Uses a
        /// screen-radius approximation: projects a world size onto GUI to estimate the handle scale,
        /// then takes a generous multiple to cover the axis arrows.
        /// 判断屏幕位置是否落在世界目标点的移动手柄 Gizmo（中心+轴箭头）上——点击此处时不应清空选中。
        /// 用屏幕半径近似：把一个世界尺寸投影到 GUI 估算手柄缩放，再取较大倍数覆盖轴箭头。
        /// </summary>
        public static bool IsOnHandleGizmo(Vector3 worldTarget, Vector2 mousePos,
            float sizeMultiplier = 3.5f, float basePad = 25f)
        {
            if (Camera.current == null) return false;
            Vector2 center = HandleUtility.WorldToGUIPoint(worldTarget);
            float hsize = HandleUtility.GetHandleSize(worldTarget);
            Vector2 edge = HandleUtility.WorldToGUIPoint(worldTarget + Camera.current.transform.right * hsize);
            float pxPerSize = Mathf.Max(2f, Vector2.Distance(center, edge));
            float radius = pxPerSize * sizeMultiplier + basePad;
            return Vector2.Distance(mousePos, center) <= radius;
        }
    }

    /// <summary>
    /// Shared adaptation that draws a Unity PositionHandle for one editable scene element and returns
    /// the dragged result (already snapped), recording a single Undo entry per drag. Both the curve tool
    /// and the triangle tool call this for "move tool (W)" editing of their points/handles.
    /// 共享移动工具适配：为某一个可编辑场景元素绘制 Unity PositionHandle，返回拖拽结果（已吸附），
    /// 且每次拖拽只记录一条 Undo。曲线工具与三角面工具均用它做"移动工具（W）"编辑点/手柄。
    /// </summary>
    public static class SceneMoveTool
    {
        /// <summary>
        /// Draws a PositionHandle and captures the dragged world position. Returns true when the handle
        /// moved; on a move it applies snap and records Undo once. The caller is responsible for writing
        /// newPos back to its element and repainting.
        /// 绘制 PositionHandle 并捕获拖拽后的世界坐标。手柄移动时返回 true，并做吸附与一次性 Undo 记录。
        /// 调用方负责把 newPos 写回其元素并重绘。
        /// </summary>
        public static bool DrawPositionHandle(
            Object undoData, Vector3 worldPos, Quaternion rotation,
            Vector3 snapStep, bool snapEnabled,
            ref bool undoRecorded, string undoName, out Vector3 newPos)
        {
            newPos = worldPos;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(worldPos, rotation);
            if (!EditorGUI.EndChangeCheck())
                return false;

            if (snapEnabled)
                moved = SceneDragUtility.SnapVector3(moved, snapStep);

            if (!undoRecorded)
            {
                Undo.RecordObject(undoData, undoName);
                undoRecorded = true;
            }

            newPos = moved;
            return true;
        }
    }
}
