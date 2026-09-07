using UnityEngine;

namespace ToolLib
{
    /// <summary>
    /// Shared editable cursor contract implemented by each tool's data manager (CurveManager / TriangleFaceManager).
    /// The cursor is a world-space reference point used both as a mirror/flip center and as a draggable scene gizmo.
    /// 共享可编辑游标契约，由各工具的数据管理器（CurveManager / TriangleFaceManager）实现。
    /// 游标是一个世界空间参考点，既用作镜像/翻转中心，也用作可拖拽的场景 Gizmo。
    /// </summary>
    public interface IToolCursorHost
    {
        /// <summary>Cursor position in world space. / 游标世界坐标。</summary>
        Vector3 CursorPosition { get; set; }

        /// <summary>Whether the whole cursor is locked (not selectable/draggable). / 整个游标是否锁定（不可选中/拖拽）。</summary>
        bool CursorLocked { get; set; }

        /// <summary>Per-axis locks (protect axes during dragging and reset). / 分轴锁（拖拽与重置时保护对应轴）。</summary>
        bool CursorLockX { get; set; }
        bool CursorLockY { get; set; }
        bool CursorLockZ { get; set; }

        /// <summary>When on, the tool's transform center switches from the world origin to the cursor. / 开启时变换中心从世界原点切换到游标。</summary>
        bool CursorReferenceMode { get; set; }
    }

    /// <summary>Shared cursor helpers (extension methods on IToolCursorHost). / 共享游标辅助（IToolCursorHost 扩展方法）。</summary>
    public static class ToolCursorHostUtil
    {
        /// <summary>Resolves the transform center: cursor when reference mode is on, else the world origin. / 解析变换中心：参考系开启时用游标，否则用世界原点。</summary>
        public static Vector3 ResolveCenter(this IToolCursorHost host)
            => host.CursorReferenceMode ? host.CursorPosition : Vector3.zero;

        /// <summary>Resets the cursor position to the origin, honoring per-axis locks. / 将游标位置重置为原点，尊重分轴锁。</summary>
        public static void ResetToOrigin(this IToolCursorHost host)
        {
            Vector3 p = host.CursorPosition;
            if (!host.CursorLockX) p.x = 0f;
            if (!host.CursorLockY) p.y = 0f;
            if (!host.CursorLockZ) p.z = 0f;
            host.CursorPosition = p;
        }
    }

    /// <summary>
    /// Shared transform (flip/mirror) math used by the curve tool and the triangle tool.
    /// 共享变换（翻转/镜像）算法，曲线工具与三角面工具共用。
    /// </summary>
    public static class EditTransformOps
    {
        /// <summary>Mirrors a single point about a center along the given axes. / 以 center 为中心沿指定轴镜像单个点。</summary>
        public static void MirrorPoint(ref Vector3 p, Vector3 center, bool mirX, bool mirY, bool mirZ)
        {
            if (mirX) p.x = 2f * center.x - p.x;
            if (mirY) p.y = 2f * center.y - p.y;
            if (mirZ) p.z = 2f * center.z - p.z;
        }
    }
}
