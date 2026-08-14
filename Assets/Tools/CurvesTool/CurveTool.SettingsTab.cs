using UnityEditor;
using UnityEngine;

/// <summary>
/// 曲线工具 — 设置 Tab + 关于 Tab
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
        if (GUILayout.Button(L10n.T("reset_default"), GUILayout.Height(28)))
        {
            VertexSize = 0.2f;
            HandleEndSize = 0.12f;
            VertexPointColor = Color.black;
            HandleEndPointColor = Color.red;
            SnapGridSize = new Vector3(0.5f, 0.5f, 0.5f);
            SnapIncrementMove = Vector3.one;
            L10n.SetLanguage(L10n.Lang.EN);
            SceneView.RepaintAll();
        }
        if (GUI.changed) SaveSettings();
    }
}
