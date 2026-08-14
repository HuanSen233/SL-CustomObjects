using UnityEditor;
using UnityEngine;

/// <summary>
/// 曲线工具 — EditorWindow 主骨架（字段、生命周期、持久化、业务逻辑）。
/// UI 面板拆分至 CurveTool.EditTab.cs 和 CurveTool.SettingsTab.cs。
/// </summary>
public partial class CurveTool : EditorWindow
{
    private CurveManager _manager;
    private bool _editMode;
    private bool _previewMode;

    private int _selectedTab;
    private string[] _tabs;
    private string[] Tabs => _tabs ??= new[] { L10n.T("edit_tab"), L10n.T("settings_tab") };

    private Vector2 _scrollCurves;
    private string _newCurveName = "NewCurve";
    private int _defaultSegmentCount = 16;

    // ===== 线段属性编辑缓冲 =====
    private Vector3 _segBaseScale = Vector3.one;
    private Vector3 _segRelativeScale = Vector3.one;
    private float _segPositionOffset = 0f;
    private Vector3 _segPositionOffset3D = Vector3.zero;
    private Vector3 _segRotationOffset = Vector3.zero;
    private PrimitiveType _segPrimitiveType = PrimitiveType.Cube;
    private bool _segFitSegmentLength = true;
    private int _segFitAxis = 2;

    // ===== 游标/顶点/控制柄编辑缓冲 =====
    private Vector2 _vertexPosition;
    private float _vertexHeight;
    private bool _vertexLockX, _vertexLockY, _vertexLockZ;
    private HandleType _vertexHandleType = HandleType.Auto;

    // ===== 本地化/选项缓存（避免每帧分配新数组）=====
    private string[] _htNames;
    private string[] _primNames;
    private static readonly string[] AxisNames = { "X", "Y", "Z" };
    private static readonly string[] LangNames = { "English", "简体中文" };

    // ===== 控制柄位置编辑缓冲 =====
    private Vector2 _leftHandlePos;
    private float _leftHandleHeight;
    private bool _leftHandleLockX, _leftHandleLockY, _leftHandleLockZ;
    private Vector2 _rightHandlePos;
    private float _rightHandleHeight;
    private bool _rightHandleLockX, _rightHandleLockY, _rightHandleLockZ;

    // ===== 公共属性 =====
    public bool IsEditMode => _editMode;
    public bool PreviewMode => _previewMode;
    public int DefaultSegmentCount => _defaultSegmentCount;

    // ===== 可配置属性 =====
    public float VertexSize = 0.2f;
    public float HandleEndSize = 0.12f;
    public Color CurveLineColor = Color.white;
    public Color VertexPointColor = Color.black;
    public Color HandleLineColor = Color.green;
    public Color HandleEndPointColor = Color.red;
    public Color SelectedColor = Color.yellow;
    public Color SelectedSegmentColor = new Color(0.3f, 0.5f, 1f, 0.8f);
    public Color PreviewWireColor = new Color(1f, 1f, 1f, 0.25f);
    public Color GenerationColor = Color.white;
    public float CursorDisplaySize = 0.15f;
    public float ArrowSize = 0.12f;

    // ===== Edit Tab 折叠状态 =====
    private bool _foldoutCurveTools = true;
    private bool _foldoutCurveList = true;
    private bool _foldoutVertexProps = true;
    private bool _foldoutSegmentProps = true;
    private bool _foldoutGenObject = true;

    // ===== 吸附设置 =====
    public Vector3 SnapGridSize = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 SnapIncrementMove = Vector3.one;

    // ===== 窗口生命周期 =====

    [MenuItem("Tools/Curve Tool")]
    public static void OpenWindow()
    {
        var w = GetWindow<CurveTool>("Curve Tool");
        w.minSize = new Vector2(340, 460);
        w.Show();
    }

    private void UpdateTitle() { titleContent.text = L10n.T("window_title"); _tabs = null; _htNames = null; _primNames = null; }

    public static CurveTool Instance { get; private set; }

    private void OnEnable()
    {
        Instance = this;
        _manager = CurveManager.Instance;
        LoadSettings();
        CurveSceneRenderer.Register();
        CurveSceneEditor.Register();
        UpdateTitle();
    }

    private void OnDisable()
    {
        SaveSettings();
        if (Instance == this) Instance = null;
        _editMode = false;
        CurveSceneRenderer.Unregister();
        CurveSceneEditor.Unregister();
        SceneView.RepaintAll();
    }

    // ===== 主 GUI =====

    private void OnGUI()
    {
        if (_manager == null) _manager = CurveManager.Instance;
        DrawHeader();
        // 防止历史选中索引越界（如移除页签后残留的 _selectedTab）
        _selectedTab = Mathf.Clamp(_selectedTab, 0, Tabs.Length - 1);
        _selectedTab = GUILayout.Toolbar(_selectedTab, Tabs);
        GUILayout.Space(6);
        if (_selectedTab == 0) TabEdit();
        else if (_selectedTab == 1) TabSettings();
    }

    /// <summary>绘制标题头</summary>
    private void DrawHeader()
    {
        GUILayout.Space(6);
        var ts = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField(L10n.T("window_title"), ts);
        EditorGUILayout.LabelField("Curve Tool", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(4);
    }

    // ===== 模式栏 =====

    /// <summary>曲线编辑开关</summary>
    private void DrawModeBar()
    {
        EditorGUILayout.BeginHorizontal();

        // 曲线编辑
        GUI.backgroundColor = _editMode ? new Color(0.4f, 0.85f, 0.4f) : Color.white;
        bool newEdit = GUILayout.Toggle(_editMode, $" {L10n.T("curve_edit")}", "Button", GUILayout.Height(28));
        GUI.backgroundColor = Color.white;
        if (newEdit != _editMode) { _editMode = newEdit; SceneView.RepaintAll(); }

        EditorGUILayout.EndHorizontal();
    }

    // ===== 业务逻辑 =====

    /// <summary>用指定轴向创建新曲线</summary>
    private void CreateNewCurve(UpAxis upAxis)
    {
        string baseName = string.IsNullOrWhiteSpace(_newCurveName) ? "NewCurve" : _newCurveName;
        string finalName = baseName;
        int dedup = 1;
        while (_manager.Curves.Exists(c => c.Name == finalName))
            finalName = $"{baseName}{dedup++}";
        var newCurve = _manager.AddNewCurve(finalName, _defaultSegmentCount, upAxis);
        newCurve.RecalculateHandles();
        _newCurveName = "NewCurve";
        SceneView.RepaintAll();
    }

    /// <summary>创建 3D 曲线</summary>
    private void CreateNew3DCurve()
    {
        string baseName = string.IsNullOrWhiteSpace(_newCurveName) ? "New3DCurve" : _newCurveName;
        string finalName = baseName;
        int dedup = 1;
        while (_manager.Curves.Exists(c => c.Name == finalName))
            finalName = $"{baseName}{dedup++}";
        var curve = BezierCurve.CreateDefault3D(finalName);
        Undo.RecordObject(_manager, "创建 3D 曲线");
        _manager.Curves.Add(curve);
        curve.RecalculateHandles();
        _manager.Select(_manager.Curves.Count - 1);
        _manager.MarkDirty();
        _newCurveName = "NewCurve";
        SceneView.RepaintAll();
    }

    /// <summary>深拷贝曲线并去重命名后插入列表</summary>
    private void DuplicateCurve(BezierCurve source)
    {
        if (source == null) return;
        // JSON 深拷贝
        var clone = JsonUtility.FromJson<BezierCurve>(JsonUtility.ToJson(source));
        // 去重命名：原名+Copy+数字
        string baseName = clone.Name + "Copy";
        clone.Name = baseName;
        int dedup = 1;
        while (_manager.Curves.Exists(c => c.Name == clone.Name))
            clone.Name = $"{baseName}{dedup++}";
        // 重置非序列化字段
        clone.IsSelected = false;
        clone.SelectedSegmentIndex = -1;
        foreach (var v in clone.Vertices) { v.IsSelected = false; v.SelectedSubElement = 0; }
        foreach (var s in clone.Segments) s.IsSelected = false;
        Undo.RecordObject(_manager, "复制曲线");
        _manager.Curves.Add(clone);
        _manager.Select(_manager.Curves.Count - 1);
        _manager.MarkDirty();
        SceneView.RepaintAll();
    }

    // ===== 设置持久化 =====
    /// <summary>设置文件路径（相对 Assets，位于工具目录内，随工具目录移动自动跟随）</summary>
    private static string SettingsPath => $"{CurveManager.ToolDirectory}/CurveToolSettings.json";

    [System.Serializable]
    private class ToolSettings
    {
        public float VertexSize = 0.2f;
        public float HandleEndSize = 0.12f;
        public float ArrowSize = 0.15f;
        public Color GenerationColor = Color.white;
        public Color VertexPointColor = Color.black;
        public Color HandleEndPointColor = Color.red;
        public float CursorDisplaySize = 0.15f;
        public Vector3 SnapGridSize = new Vector3(0.5f, 0.5f, 0.5f);
        public Vector3 SnapIncrementMove = Vector3.one;
        public int Language = 0; // 0=EN, 1=ZH
    }

    private void SaveSettings()
    {
        var s = new ToolSettings
        {
            VertexSize = VertexSize,
            HandleEndSize = HandleEndSize,
            CursorDisplaySize = CursorDisplaySize,
            ArrowSize = ArrowSize,
            GenerationColor = GenerationColor,
            VertexPointColor = VertexPointColor,
            HandleEndPointColor = HandleEndPointColor,
            SnapGridSize = SnapGridSize,
            SnapIncrementMove = SnapIncrementMove,
            Language = (int)L10n.Current,
        };
        System.IO.File.WriteAllText(SettingsPath, JsonUtility.ToJson(s, prettyPrint: true));
    }

    private void LoadSettings()
    {
        if (!System.IO.File.Exists(SettingsPath)) return;
        try
        {
            var json = System.IO.File.ReadAllText(SettingsPath);
            var s = JsonUtility.FromJson<ToolSettings>(json);
            if (s == null) return;
            VertexSize = s.VertexSize;
            HandleEndSize = s.HandleEndSize;
            CursorDisplaySize = s.CursorDisplaySize;
            ArrowSize = s.ArrowSize;
            SnapGridSize = s.SnapGridSize;
            SnapIncrementMove = s.SnapIncrementMove;
            GenerationColor = s.GenerationColor;
            VertexPointColor = s.VertexPointColor;
            HandleEndPointColor = s.HandleEndPointColor;
            L10n.SetLanguage((L10n.Lang)s.Language);
        }
        catch { /* 忽略损坏的配置文件 */ }
    }
}

