using System;
using UnityEditor;
using UnityEngine;

namespace ToolLib
{
    /// <summary>
    /// Shared cursor scene interaction. In Move-tool-only editing the cursor is moved via a PositionHandle
    /// (DrawMoveHandle) after being selected (SetCursorSelected). The old built-in ray-drag path is kept only
    /// for compatibility and marked obsolete (the editors no longer use it).
    /// 共享游标场景交互。移动工具(W)编辑模式下通过 PositionHandle 移动游标（先选中再绘制拖拽手柄）。
    /// 旧的射线拖拽路径仅为兼容保留并标记为废弃（编辑器已不再使用）。
    /// </summary>
    public static class ToolCursorInteraction
    {
        // ===== Built-in ray-drag (deprecated; editors now use the Move-tool PositionHandle) / 自带射线拖拽（废弃） =====
        /// <summary>Old built-in ray-drag state. / 旧自带射线拖拽状态。</summary>
        private static bool _dragging;
        /// <summary>Old built-in ray-drag state. / 旧自带射线拖拽状态。</summary>
        private static bool _undoDone;

        /// <summary>Whether the cursor is currently selected (shows its PositionHandle). / 游标当前是否被选中（显示其 PositionHandle）。</summary>
        private static bool _selected;

        /// <summary>Whether the cursor is currently selected. / 游标当前是否被选中。</summary>
        public static bool IsCursorSelected => _selected;

        /// <summary>Sets the cursor selection state. / 设置游标选中状态。</summary>
        public static void SetCursorSelected(bool value) => _selected = value;

        /// <summary>Reset drag + selection state (call when the tool leaves edit mode). / 重置拖拽与选中状态。</summary>
        public static void Reset()
        {
            _dragging = false;
            _undoDone = false;
            _selected = false;
        }

        /// <summary>
        /// Draws a PositionHandle for the cursor and writes the dragged result back. Returns true on a move.
        /// The caller supplies its snap settings (from the tool) and an undo/dirty reach to the manager.
        /// 为游标绘制 PositionHandle 并写回拖拽结果；移动时返回 true。调用方提供吸附设置与 undo/dirty 回调。
        /// </summary>
        public static bool DrawMoveHandle(IToolCursorHost host,
            Vector3 snapStep, bool snapActive, ref bool undoRecorded, Action dirty)
        {
            if (host == null) return false;
            Vector3 newPos;
            bool moved = SceneMoveTool.DrawPositionHandle(
                (UnityEngine.Object)host, host.CursorPosition, Quaternion.identity,
                snapStep, snapActive, ref undoRecorded, "移动游标", out newPos);
            if (moved)
            {
                host.CursorPosition = newPos;
                dirty?.Invoke();
                SceneView.RepaintAll();
            }
            return moved;
        }

        /// <summary>Hits the cursor (ray-to-center within the screen-space display radius). / 命中游标（射线到中心在屏幕空间显示半径内）。</summary>
        public static bool TryHitCursor(IToolCursorHost host, Event e, float displaySize)
        {
            if (host == null || host.CursorLocked) return false;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 toCenter = host.CursorPosition - ray.origin;
            float t = Vector3.Dot(toCenter, ray.direction);
            if (t < 0f) return false;
            Vector3 closest = ray.GetPoint(t);
            float dist = Vector3.Distance(closest, host.CursorPosition);
            float hitRadius = displaySize * 2f * HandleUtility.GetHandleSize(host.CursorPosition);
            return dist < hitRadius;
        }

        // ===== Obsolete built-in ray-drag API (kept for compatibility, no longer used) / 废弃自带拖拽 API（兼容保留） =====
        /// <summary>Obsolete: built-in cursor drag is removed. / 废弃：自带游标拖拽已移除。</summary>
        [Obsolete("Built-in cursor drag is removed; use the Move tool (W) PositionHandle.")]
        public static bool IsDragging => _dragging;

        /// <summary>Obsolete: built-in cursor drag is removed. / 废弃：自带游标拖拽已移除。</summary>
        [Obsolete("Built-in cursor drag is removed; use the Move tool (W) PositionHandle.")]
        public static void BeginDrag()
        {
            _dragging = true;
            _undoDone = false;
        }

        /// <summary>Obsolete: built-in cursor drag is removed. / 废弃：自带游标拖拽已移除。</summary>
        [Obsolete("Built-in cursor drag is removed; use the Move tool (W) PositionHandle.")]
        public static void Drag(IToolCursorHost host, Event e, bool snapActive, Vector3 snapStep,
            Action undo, Action dirty)
        {
            if (!_dragging || host == null) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane p = new Plane(-Camera.current.transform.forward, host.CursorPosition);
            if (p.Raycast(ray, out float dist))
            {
                Vector3 hit = ray.GetPoint(dist);
                if (snapActive)
                    hit = SceneDragUtility.SnapVector3(hit, snapStep);

                if (host.CursorLockX) hit.x = host.CursorPosition.x;
                if (host.CursorLockY) hit.y = host.CursorPosition.y;
                if (host.CursorLockZ) hit.z = host.CursorPosition.z;

                if (!_undoDone) { undo?.Invoke(); _undoDone = true; }
                host.CursorPosition = hit;
            }
            dirty?.Invoke();
            e.Use();
            SceneView.RepaintAll();
        }

        /// <summary>Obsolete: built-in cursor drag is removed. / 废弃：自带游标拖拽已移除。</summary>
        [Obsolete("Built-in cursor drag is removed; use the Move tool (W) PositionHandle.")]
        public static void EndDrag(Event e)
        {
            if (_dragging && e.button == 0)
            {
                _dragging = false;
                e.Use();
            }
        }
    }
}
