using UnityEditor;
using UnityEngine;

/// <summary>
/// Curve Tool — Settings tab + About tab. / 曲线工具 — 设置 Tab + 关于 Tab
/// </summary>
public partial class CurveTool
{
    // ============================================================
    //  设置 Tab
    // ============================================================
    private void TabSettings()
    {
        GUILayout.Space(4);
        EditorGUILayout.LabelField(L10n.T("size"), EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("vertex_size"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        VertexSize = EditorGUILayout.Slider(VertexSize, 0.02f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("handle_size"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        HandleEndSize = EditorGUILayout.Slider(HandleEndSize, 0.02f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("arrow_size"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        ArrowSize = EditorGUILayout.Slider(ArrowSize, 0.02f, 1f);
        EditorGUILayout.EndHorizontal();
        if (GUI.changed) SceneView.RepaintAll();

        GUILayout.Space(12);
        EditorGUILayout.LabelField(L10n.T("snap_settings"), EditorStyles.boldLabel);

        // Snap source toggle: hover the label for the reason; the right side shows the toggle and the active source.
        // 吸附设定来源开关：悬停标签显示原因说明；右侧为开关与当前生效来源状态小字
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(L10n.T("snap_use_editor"), L10n.T("snap_use_editor_tip")),
            GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        EditorGUI.BeginChangeCheck();
        UseEditorSnapSettings = EditorGUILayout.Toggle(UseEditorSnapSettings);
        bool snapSourceChanged = EditorGUI.EndChangeCheck();
        GUILayout.Label(UseEditorSnapSettings ? L10n.T("snap_editor_ctrl") : L10n.T("snap_tool_ctrl"), EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        if (snapSourceChanged) { SaveSettings(); SceneView.RepaintAll(); }

        // In editor mode, disable the tool-local step inputs (grayed out) since the editor controls them.
        // 编辑器模式下禁用工具自身步长输入（灰显），并提示由编辑器控制
        EditorGUI.BeginDisabledGroup(UseEditorSnapSettings);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("snap_grid_size"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GUILayout.Label("X", GUILayout.Width(12)); SnapGridSize.x = EditorGUILayout.FloatField(SnapGridSize.x);
        GUILayout.Label("Y", GUILayout.Width(12)); SnapGridSize.y = EditorGUILayout.FloatField(SnapGridSize.y);
        GUILayout.Label("Z", GUILayout.Width(12)); SnapGridSize.z = EditorGUILayout.FloatField(SnapGridSize.z);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("snap_increment"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GUILayout.Label("X", GUILayout.Width(12)); SnapIncrementMove.x = EditorGUILayout.FloatField(SnapIncrementMove.x);
        GUILayout.Label("Y", GUILayout.Width(12)); SnapIncrementMove.y = EditorGUILayout.FloatField(SnapIncrementMove.y);
        GUILayout.Label("Z", GUILayout.Width(12)); SnapIncrementMove.z = EditorGUILayout.FloatField(SnapIncrementMove.z);
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if (GUI.changed) SceneView.RepaintAll();

        GUILayout.Space(12);
        EditorGUILayout.LabelField(L10n.T("color"), EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("vertex_pt"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        VertexPointColor = EditorGUILayout.ColorField(VertexPointColor);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("handle_end"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        HandleEndPointColor = EditorGUILayout.ColorField(HandleEndPointColor);
        EditorGUILayout.EndHorizontal();
        if (GUI.changed) SceneView.RepaintAll();

        GUILayout.Space(12);
        EditorGUILayout.LabelField(L10n.T("language"), EditorStyles.boldLabel);
        string[] langNames = LangNames;
        int langIdx = EditorGUILayout.Popup((int)L10n.Current, langNames);
        if ((L10n.Lang)langIdx != L10n.Current) { L10n.SetLanguage((L10n.Lang)langIdx); UpdateTitle(); SaveSettings(); }

        GUILayout.Space(12);
        EditorGUILayout.LabelField(L10n.T("interaction"), EditorStyles.boldLabel);
        // Move-tool editing toggle (default on): move vertices/handles via the Unity Move tool (W), suppressing the built-in drag.
        // 移动工具编辑开关（默认开）：使用 Unity 移动工具（W）移动顶点/控制柄，屏蔽工具自带的拖拽。
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(L10n.T("use_move_tool"), L10n.T("use_move_tool_tip")), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.62f));
        bool useMove = UseMoveTool;
        EditorGUI.BeginChangeCheck();
        useMove = EditorGUILayout.Toggle(useMove);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck() && useMove != UseMoveTool)
        {
            UseMoveTool = useMove;
            SaveSettings();
            SceneView.RepaintAll();
        }

        GUILayout.Space(12);
        if (GUILayout.Button(L10n.T("reset_default"), GUILayout.Height(28)))
        {
            // Reset every configurable item (values share the default constants to avoid drift).
            // 全部可配置项复位（值与默认值常量同源，避免漂移）
            VertexSize = DefaultVertexSize;
            HandleEndSize = DefaultHandleEndSize;
            ArrowSize = DefaultArrowSize;
            CursorDisplaySize = DefaultCursorDisplaySize;
            GenerationColor = DefaultGenerationColor;
            VertexPointColor = DefaultVertexPointColor;
            HandleEndPointColor = DefaultHandleEndPointColor;
            UseEditorSnapSettings = true;
            SnapGridSize = DefaultSnapGridSize;
            SnapIncrementMove = DefaultSnapIncrementMove;
            UseMoveTool = true;
            L10n.SetLanguage(L10n.Lang.EN);
            SaveSettings();
            SceneView.RepaintAll();
        }
        if (GUI.changed) SaveSettings();
    }
}
