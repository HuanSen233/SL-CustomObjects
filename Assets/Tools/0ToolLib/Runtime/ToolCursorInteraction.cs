using System;
using UnityEditor;
using UnityEngine;

namespace ToolLib
{
    /// <summary>
    /// Shared cursor scene interaction: hit-testing and dragging the tool cursor in the Scene view.
    /// Encapsulates the drag state so both the curve tool and the triangle tool behave identically.
    /// 共享游标场景交互：在场景视图中命中/拖拽工具游标。封装拖拽状态，使曲线与三角面工具行为一致。
    /// </summary>
    public static class ToolCursorInteraction
    {
        private static bool _dragging;
        private static bool _undoDone;

        /// <summary>Whether a cursor drag is in progress. / 是否正在拖拽游标。</summary>
        public static bool IsDragging => _dragging;

        /// <summary>Reset drag state (call when the tool leaves edit mode or a drag is cancelled). / 重置拖拽状态。</summary>
        public static void Reset()
        {
            _dragging = false;
            _undoDone = false;
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

        /// <summary>Begins a cursor drag. / 开始拖拽游标。</summary>
        public static void BeginDrag()
        {
            _dragging = true;
            _undoDone = false;
        }

        /// <summary>Applies a cursor drag. The caller supplies the snap step (from its own snap settings) and the
        /// undo/dirty callbacks (reaching the tool's data manager). / 应用游标拖拽。调用方提供吸附步长（取自其吸附设置）
        /// 与 undo/dirty 回调（作用到该工具的数据管理器）。</summary>
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

                // Per-axis locks: keep locked axes unchanged / 分轴锁：保持被锁轴的值不变
                if (host.CursorLockX) hit.x = host.CursorPosition.x;
                if (host.CursorLockY) hit.y = host.CursorPosition.y;
                if (host.CursorLockZ) hit.z = host.CursorPosition.z;

                // Record Undo once per drag / 每次拖拽只记录一次 Undo
                if (!_undoDone) { undo?.Invoke(); _undoDone = true; }
                host.CursorPosition = hit;
            }
            dirty?.Invoke();
            e.Use();
            SceneView.RepaintAll();
        }

        /// <summary>Ends a cursor drag. / 结束游标拖拽。</summary>
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
