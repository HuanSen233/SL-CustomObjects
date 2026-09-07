using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// Triangle Tool — Settings tab. / 三角面工具 — 设置 Tab。
    /// UI 结构参考 CurvesTool.SettingsTab：分节粗体标签 + 标签宽 30% 的行布局 + 语言下拉 + 重置按钮。
    /// </summary>
    public partial class TriangleTool
    {
        private static readonly string[] LangNames = { "English", "简体中文" };

        // ============================================================
        //  Settings Tab / 设置 Tab
        // ============================================================
        private void TabSettings()
        {
            GUILayout.Space(4);

            // ~ Build / 构建
            EditorGUILayout.LabelField(TriangleL10n.T("mode"), EditorStyles.boldLabel);
            DrawSettingRow(TriangleL10n.T("accuracy"), () =>
            {
                using (new EditorGUI.DisabledScope(Mode == TriangleBuildMode.Exact))
                    Accuracy = EditorGUILayout.Slider(Accuracy, 0.0001f, 0.05f);
            });
            DrawSettingRow(TriangleL10n.T("opt_passes"), () =>
            {
                using (new EditorGUI.DisabledScope(Mode != TriangleBuildMode.Hierarchical))
                    OptimizationPasses = EditorGUILayout.IntSlider(OptimizationPasses, 0, 10);
            });
            if (GUI.changed) SaveSettings();

            GUILayout.Space(12);

            // ~ Performance / 性能
            EditorGUILayout.LabelField(TriangleL10n.T("perf"), EditorStyles.boldLabel);
            DrawSettingRow(TriangleL10n.T("edit_max_blocks_per_frame"), () =>
            { EditMaxBlocksPerFrame = EditorGUILayout.IntField(EditMaxBlocksPerFrame, GUILayout.MinWidth(60)); });
            DrawSettingRow(TriangleL10n.T("import_max_blocks_per_frame"), () =>
            { ImportMaxBlocksPerFrame = EditorGUILayout.IntField(ImportMaxBlocksPerFrame, GUILayout.MinWidth(60)); });
            if (GUI.changed) SaveSettings();

            GUILayout.Space(12);

            // ~ Input / 输入
            EditorGUILayout.LabelField(TriangleL10n.T("input"), EditorStyles.boldLabel);
            DrawSettingRow(TriangleL10n.T("use_move_tool"), () => { UseMoveTool = EditorGUILayout.Toggle(UseMoveTool); });
            DrawSettingRow(TriangleL10n.T("use_editor_snap"), () => { UseEditorSnapSettings = EditorGUILayout.Toggle(UseEditorSnapSettings); });
            if (!UseEditorSnapSettings)
            {
                DrawSettingRow(TriangleL10n.T("snap_grid"), () => { SnapGridSize = EditorGUILayout.Vector3Field(GUIContent.none, SnapGridSize); });
                DrawSettingRow(TriangleL10n.T("snap_inc"), () => { SnapIncrementMove = EditorGUILayout.Vector3Field(GUIContent.none, SnapIncrementMove); });
            }
            DrawSettingRow(TriangleL10n.T("cursor_display"), () => { CursorDisplaySize = EditorGUILayout.Slider(CursorDisplaySize, 0.05f, 0.5f); });
            if (GUI.changed) SaveSettings();

            GUILayout.Space(12);

            // ~ Colors / 颜色
            EditorGUILayout.LabelField(TriangleL10n.T("color"), EditorStyles.boldLabel);
            DrawSettingRow(TriangleL10n.T("face_color"), () => { FaceColor = EditorGUILayout.ColorField(FaceColor); });
            DrawSettingRow(TriangleL10n.T("fallback_color"), () => { FallbackColor = EditorGUILayout.ColorField(FallbackColor); });
            DrawSettingRow(TriangleL10n.T("collidable"), () => { Collidable = EditorGUILayout.Toggle(Collidable); });
            if (GUI.changed) SaveSettings();

            GUILayout.Space(12);

            // ~ Language / 语言
            EditorGUILayout.LabelField(TriangleL10n.T("language"), EditorStyles.boldLabel);
            int langIdx = EditorGUILayout.Popup((int)TriangleL10n.Current, LangNames);
            if ((TriangleL10n.Lang)langIdx != TriangleL10n.Current)
            {
                TriangleL10n.SetLanguage((TriangleL10n.Lang)langIdx);
                UpdateTitle();
                SaveSettings();
            }

            GUILayout.Space(12);

            // ~ Reset / 重置
            GUI.backgroundColor = UiDeleteRed;
            if (GUILayout.Button(TriangleL10n.T("reset_default"), GUILayout.Height(28)))
            {
                Mode = TriangleBuildMode.Exact;
                ObjMode = TriangleBuildMode.Exact;
                Accuracy = DefaultAccuracy;
                OptimizationPasses = DefaultOptimizationPasses;
                RectangleTolerance = DefaultRectangleTolerance;
                UseRectangleOptimization = true;
                FlipWinding = false;
                Collidable = false;
                FaceColor = DefaultFaceColor;
                FallbackColor = DefaultFallbackColor;
                EditMaxBlocksPerFrame = DefaultEditMaxBlocksPerFrame;
                ImportMaxBlocksPerFrame = DefaultImportMaxBlocksPerFrame;
                UseMoveTool = true;
                UseEditorSnapSettings = true;
                SnapGridSize = DefaultSnapGridSize;
                SnapIncrementMove = DefaultSnapIncrementMove;
                CursorDisplaySize = DefaultCursorDisplaySize;
                TriangleL10n.SetLanguage(TriangleL10n.Lang.EN);
                UpdateTitle();
                SaveSettings();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
        }

        /// <summary>Standard settings row: fixed-width label + control. / 标准设置行：固定宽度标签 + 控件。</summary>
        private static void DrawSettingRow(string label, System.Action drawControl)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.45f));
            drawControl();
            EditorGUILayout.EndHorizontal();
        }
    }
}
