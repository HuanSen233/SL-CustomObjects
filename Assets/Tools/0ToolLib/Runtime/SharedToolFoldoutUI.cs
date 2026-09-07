using System;
using UnityEditor;
using UnityEngine;

namespace ToolLib
{
    /// <summary>
    /// Shared foldout UI for the editable cursor and the transform (flip/mirror) section, so the curve tool
    /// and the triangle tool render and behave identically. The tool supplies the cursor host, a localizer,
    /// and the apply-flip/apply-mirror delegates (which know how to reach its own data).
    /// 共享游标与变换（翻转/镜像）折叠区 UI，使曲线工具与三角面工具渲染与行为一致。
    /// 工具提供游标宿主、本地化器与 apply-flip/apply-mirror 委托（由其自身数据实现）。
    /// </summary>
    public static class SharedToolFoldoutUI
    {
        // Shared UI colors (same palette as the curve tool's button/row colors) / 共享 UI 颜色（同曲线工具配色）
        private static readonly Color UiLockedOrange = new Color(0.9f, 0.6f, 0.3f);
        private static readonly Color UiDisabledGray = new Color(0.5f, 0.5f, 0.5f);

        /// <summary>Draws the axis-button row (X/Y/Z), each disabled via its own flag. Returns the three click flags.
        /// 绘制轴向按钮行（X/Y/Z），各自按标志禁用；返回三个点击标志。</summary>
        public static (bool x, bool y, bool z) DrawAxisButtons(bool canX, bool canY, bool canZ)
        {
            EditorGUI.BeginDisabledGroup(!canX);
            bool x = GUILayout.Button("X", GUILayout.Height(22));
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!canY);
            bool y = GUILayout.Button("Y", GUILayout.Height(22));
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!canZ);
            bool z = GUILayout.Button("Z", GUILayout.Height(22));
            EditorGUI.EndDisabledGroup();
            return (x, y, z);
        }

        /// <summary>
        /// Cursor foldout: reference-frame toggle, per-axis position/locks, global lock, and reset.
        /// 游标折叠区：参考系开关、分轴位置/锁、全局锁定、重置。
        /// </summary>
        public static void DrawCursorFoldout(IToolCursorHost host, Func<string, string> T,
            Action undo, Action dirty, Action repaint, bool editMode, float labelWidth)
        {
            if (host == null) return;

            // ---------- Cursor reference frame / 游标参考系 ----------
            EditorGUI.BeginDisabledGroup(!editMode);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T("cursor_ref_frame"), GUILayout.Width(labelWidth));
            bool refMode = host.CursorReferenceMode;
            EditorGUI.BeginChangeCheck();
            refMode = EditorGUILayout.Toggle(refMode);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                undo?.Invoke();
                host.CursorReferenceMode = refMode;
                dirty?.Invoke();
                repaint?.Invoke();
            }

            // ---------- Cursor properties / 游标属性 ----------
            Vector3 p = host.CursorPosition;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T("cursor_props"), GUILayout.Width(labelWidth));
            DrawAxisLock(host, "X", ref p.x, undo, dirty);
            GUILayout.Space(4);
            DrawAxisLock(host, "Y", ref p.y, undo, dirty);
            GUILayout.Space(4);
            DrawAxisLock(host, "Z", ref p.z, undo, dirty);
            EditorGUILayout.EndHorizontal();
            if (p != host.CursorPosition)
            {
                undo?.Invoke();
                host.CursorPosition = p;
                dirty?.Invoke();
                repaint?.Invoke();
            }

            // ---------- Lock cursor / 锁定游标 ----------
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T("lock_cursor"), GUILayout.Width(labelWidth));
            bool locked = host.CursorLocked;
            EditorGUI.BeginChangeCheck();
            locked = EditorGUILayout.Toggle(locked);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                undo?.Invoke();
                host.CursorLocked = locked;
                dirty?.Invoke();
                repaint?.Invoke();
            }

            // ---------- Reset cursor position / 重置游标位置 ----------
            if (GUILayout.Button(T("reset_cursor_pos"), GUILayout.Height(20)))
            {
                undo?.Invoke();
                host.ResetToOrigin();
                dirty?.Invoke();
                repaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.Space(4);
        }

        /// <summary>
        /// Transform foldout: flip / mirror (mirror = duplicate then flip). The center is resolved from the cursor
        /// host, and the actual data mutation is delegated to applyFlip / applyMirror (which know the tool's data).
        /// 变换折叠区：翻转 / 镜像（镜像=先复制再翻转）。中心由游标宿主解析，实际数据变更委托给
        /// applyFlip / applyMirror（它们知道该工具的数据结构）。
        /// </summary>
        public static void DrawTransformFoldout(IToolCursorHost host, Func<string, string> T,
            bool canEdit, float labelWidth,
            (bool x, bool y, bool z) axisCan,
            Action<Vector3, bool, bool, bool> applyFlip,
            Action<Vector3, bool, bool, bool> applyMirror)
        {
            if (host == null) return;
            Vector3 center = host.ResolveCenter();

            // ---------- Flip / 翻转 ----------
            EditorGUI.BeginDisabledGroup(!canEdit);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T("flip"), GUILayout.Width(labelWidth));
            var flip = DrawAxisButtons(axisCan.x, axisCan.y, axisCan.z);
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            if ((flip.x || flip.y || flip.z) && canEdit)
                applyFlip(center, flip.x, flip.y, flip.z);

            GUILayout.Space(4);

            // ---------- Mirror (duplicate, then flip along the axis) / 镜像（复制后沿轴翻转） ----------
            EditorGUI.BeginDisabledGroup(!canEdit);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T("mirror"), GUILayout.Width(labelWidth));
            var mir = DrawAxisButtons(axisCan.x, axisCan.y, axisCan.z);
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            if ((mir.x || mir.y || mir.z) && canEdit)
                applyMirror(center, mir.x, mir.y, mir.z);
            GUILayout.Space(4);
        }

        /// <summary>Per-axis lock button + label + float field (shared by the cursor foldout).
        /// 分轴锁按钮 + 标签 + 浮点输入框（游标折叠区共用）。</summary>
        private static void DrawAxisLock(IToolCursorHost host, string label, ref float value,
            Action undo, Action dirty)
        {
            bool locked = label == "X" ? host.CursorLockX : label == "Y" ? host.CursorLockY : host.CursorLockZ;
            GUI.backgroundColor = locked ? UiLockedOrange : UiDisabledGray;
            if (GUILayout.Button("L", GUILayout.Width(20), GUILayout.Height(18)))
            {
                bool nl = !locked;
                undo?.Invoke();
                if (label == "X") host.CursorLockX = nl;
                else if (label == "Y") host.CursorLockY = nl;
                else host.CursorLockZ = nl;
                dirty?.Invoke();
                GUI.changed = true;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Label(label, GUILayout.Width(12));
            EditorGUI.BeginDisabledGroup(locked);
            value = EditorGUILayout.FloatField(value, GUILayout.MinWidth(30));
            EditorGUI.EndDisabledGroup();
        }
    }
}
