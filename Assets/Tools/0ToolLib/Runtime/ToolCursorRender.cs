using System;
using UnityEditor;
using UnityEngine;

namespace ToolLib
{
    /// <summary>
    /// Shared cursor rendering: a blue/orange wireframe sphere + axis-colored arrows (camera-adaptive size).
    /// Used by both the curve tool and the triangle tool in the Scene view.
    /// 共享游标渲染：蓝/橙线框球体 + 轴色箭头（相机自适应大小）。曲线与三角面工具在场景视图中共用。
    /// </summary>
    public static class ToolCursorRender
    {
        /// <summary>Draws the cursor for the given host. / 绘制指定宿主（工具）的游标。</summary>
        public static void Draw(IToolCursorHost host, float displaySize)
        {
            if (host == null) return;
            float size = displaySize * HandleUtility.GetHandleSize(host.CursorPosition);
            bool locked = host.CursorLocked;
            Color cursorCol = locked
                ? new Color(1f, 0.6f, 0.2f, 0.7f)
                : new Color(0.2f, 0.5f, 1f, 0.7f);

            Handles.color = cursorCol;
            // Wireframe sphere (three rings) / 球体线框（三向圆环）
            Handles.DrawWireDisc(host.CursorPosition, Vector3.right, size);
            Handles.DrawWireDisc(host.CursorPosition, Vector3.up, size);
            Handles.DrawWireDisc(host.CursorPosition, Vector3.forward, size);

            // X axis arrow (red) / X 轴箭头（红）
            Handles.color = Color.red;
            Handles.DrawLine(host.CursorPosition, host.CursorPosition + Vector3.right * size * 2f);
            // Y axis arrow (green) / Y 轴箭头（绿）
            Handles.color = Color.green;
            Handles.DrawLine(host.CursorPosition, host.CursorPosition + Vector3.up * size * 2f);
            // Z axis arrow (blue) / Z 轴箭头（蓝）
            Handles.color = Color.blue;
            Handles.DrawLine(host.CursorPosition, host.CursorPosition + Vector3.forward * size * 2f);
        }
    }
}
