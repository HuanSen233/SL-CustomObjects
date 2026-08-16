using UnityEditor;
using UnityEngine;

/// <summary>
/// Curve Tool — EditorWindow skeleton (fields, lifecycle, persistence, business logic).
/// 曲线工具 — EditorWindow 主骨架（字段、生命周期、持久化、业务逻辑）。
/// UI panels are split into CurveTool.EditTab.cs and CurveTool.SettingsTab.cs.
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
    /// <summary>Edit-tab overall scroll position (scrollable when foldout content exceeds the window).
    /// 编辑页整体滚动位置（折叠区内容超出窗口时可滚动查看）</summary>
    private Vector2 _scrollEdit;
    private string _newCurveName = "NewCurve";
    private int _defaultSegmentCount = 16;

    // ===== Segment property edit buffers / 线段属性编辑缓冲 =====
    private Vector3 _segBaseScale = Vector3.one;
    private Vector3 _segRelativeScale = Vector3.one;
    private float _segPositionOffset = 0f;
    private Vector3 _segPositionOffset3D = Vector3.zero;
    private Vector3 _segRotationOffset = Vector3.zero;
    private PrimitiveType _segPrimitiveType = PrimitiveType.Cube;
    private bool _segFitSegmentLength = true;
    private int _segFitAxis = 2;

    // ===== Cursor/vertex/handle edit buffers / 游标/顶点/控制柄编辑缓冲 =====
    private Vector2 _vertexPosition;
    private float _vertexHeight;
    private bool _vertexLockX, _vertexLockY, _vertexLockZ;
    private HandleType _vertexHandleType = HandleType.Auto;

    // ===== Localization/option caches (avoid per-frame allocations) / 本地化/选项缓存（避免每帧分配新数组）=====
    private string[] _htNames;
    private string[] _primNames;
    private static readonly string[] AxisNames = { "X", "Y", "Z" };
    private static readonly string[] LangNames = { "English", "简体中文" };

    // ===== Handle position edit buffers / 控制柄位置编辑缓冲 =====
    private Vector2 _leftHandlePos;
    private float _leftHandleHeight;
    private bool _leftHandleLockX, _leftHandleLockY, _leftHandleLockZ;
    private Vector2 _rightHandlePos;
    private float _rightHandleHeight;
    private bool _rightHandleLockX, _rightHandleLockY, _rightHandleLockZ;

    // ===== Public properties / 公共属性 =====
    public bool IsEditMode => _editMode;
    public bool PreviewMode => _previewMode;
    public int DefaultSegmentCount => _defaultSegmentCount;

    // ===== Default value constants (single source shared by field init, ToolSettings and Reset to avoid drift) / 默认值常量（唯一来源：字段初始化、ToolSettings、重置按钮共用，避免漂移）=====
    public const float DefaultVertexSize = 0.2f;
    public const float DefaultHandleEndSize = 0.12f;
    public const float DefaultArrowSize = 0.15f;
    public const float DefaultCursorDisplaySize = 0.15f;
    public static readonly Vector3 DefaultSnapGridSize = new Vector3(0.5f, 0.5f, 0.5f);
    public static readonly Vector3 DefaultSnapIncrementMove = Vector3.one;
    public static readonly Color DefaultGenerationColor = Color.white;
    public static readonly Color DefaultVertexPointColor = Color.black;
    public static readonly Color DefaultHandleEndPointColor = Color.red;

    // ===== UI color constants (unified button/row colors, no scattered inline values) / UI 颜色常量（按钮/行背景统一配色，避免散落内联）=====
    public static readonly Color UiLockedOrange = new Color(0.9f, 0.6f, 0.3f);
    public static readonly Color UiSelectedBlue = new Color(0.3f, 0.6f, 1f);
    public static readonly Color UiSelectedBg = new Color(0.3f, 0.6f, 1f, 0.3f);
    public static readonly Color UiDisabledGray = new Color(0.5f, 0.5f, 0.5f);
    public static readonly Color UiPlaceholderGray = new Color(0.4f, 0.4f, 0.4f);
    public static readonly Color UiActionGreen = new Color(0.4f, 0.85f, 0.4f);
    public static readonly Color UiCreateGreen = new Color(0.3f, 0.8f, 0.3f);
    public static readonly Color UiCreateBlue = new Color(0.3f, 0.5f, 0.9f);
    public static readonly Color UiCopyBlue = new Color(0.5f, 0.75f, 1f);
    public static readonly Color UiDeleteRed = new Color(0.9f, 0.3f, 0.3f);
    public static readonly Color UiDeleteAllRed = new Color(0.85f, 0.3f, 0.3f);
    public static readonly Color UiPreviewBlue = new Color(0.4f, 0.7f, 1f);

    // ===== Configurable properties / 可配置属性 =====
    public float VertexSize = DefaultVertexSize;
    public float HandleEndSize = DefaultHandleEndSize;
    public Color CurveLineColor = Color.white;
    public Color VertexPointColor = DefaultVertexPointColor;
    public Color HandleLineColor = Color.green;
    public Color HandleEndPointColor = DefaultHandleEndPointColor;
    public Color SelectedColor = Color.yellow;
    public Color SelectedSegmentColor = new Color(0.3f, 0.5f, 1f, 0.8f);
    public Color GenerationColor = DefaultGenerationColor;
    public float CursorDisplaySize = DefaultCursorDisplaySize;
    public float ArrowSize = DefaultArrowSize;

    // ===== Edit-tab foldout state / Edit Tab 折叠状态 =====
    private bool _foldoutCurveTools = true;
    private bool _foldoutCursor = true;
    private bool _foldoutCurveList = true;
    private bool _foldoutVertexProps = true;
    private bool _foldoutSegmentProps = true;
    private bool _foldoutGenObject = true;

    // ===== Snapping settings / 吸附设置 =====
    /// <summary>Use the editor's snap settings (Scene View Grid Snap toggle + Edit > Snap Settings increments); disable to use tool-local values.
    /// 使用编辑器吸附设定（场景视图 Grid Snap 开关 + Edit > Snap Settings 步长）；关闭则用工具自身步长</summary>
    public bool UseEditorSnapSettings = true;
    public Vector3 SnapGridSize = DefaultSnapGridSize;
    public Vector3 SnapIncrementMove = DefaultSnapIncrementMove;

    // ===== Window lifecycle / 窗口生命周期 =====

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

    // ===== Main GUI / 主 GUI =====

    private void OnGUI()
    {
        // Rebind after scene switches: the cached _manager belongs to the scene active when the window opened.
        // 场景切换后重新绑定：缓存的 _manager 属于窗口打开时的场景，可能已过期
        if (_manager == null || _manager != CurveManager.Instance) _manager = CurveManager.Instance;
        DrawHeader();
        // Clamp the tab index to prevent out-of-range access (e.g. stale _selectedTab after tabs were removed).
        // 防止历史选中索引越界（如移除页签后残留的 _selectedTab）
        _selectedTab = Mathf.Clamp(_selectedTab, 0, Tabs.Length - 1);
        _selectedTab = GUILayout.Toolbar(_selectedTab, Tabs);
        GUILayout.Space(6);
        if (_selectedTab == 0) TabEdit();
        else if (_selectedTab == 1) TabSettings();
    }

    /// <summary>Draws the header (the window title bar already shows the tool name; the header keeps the localized title).
    /// 绘制标题头（窗口标题栏已显示工具名，页头仅保留语言化标题）</summary>
    private void DrawHeader()
    {
        GUILayout.Space(6);
        var ts = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField(L10n.T("window_title"), ts);
        GUILayout.Space(4);
    }

    // ===== Mode bar / 模式栏 =====

    /// <summary>Mode bar: Curve Edit takes 4/5 of the row, Preview takes 1/5.
    /// 模式栏：曲线编辑占行宽 4/5，预览占 1/5</summary>
    private void DrawModeBar()
    {
        EditorGUILayout.BeginHorizontal();

        // Split the row by ratio: Curve Edit 80%, Preview 20% (4px margin reserved to avoid overflow).
        // 按行宽分配占比：曲线编辑 80%，预览 20%（预留 4px 边距防止溢出）
        float previewW = EditorGUIUtility.currentViewWidth * 0.2f;
        float editW = EditorGUIUtility.currentViewWidth * 0.8f - 4f;

        // Curve Edit (4/5) / 曲线编辑（4/5）
        GUI.backgroundColor = _editMode ? UiActionGreen : Color.white;
        bool newEdit = GUILayout.Toggle(_editMode, $" {L10n.T("curve_edit")}", "Button", GUILayout.Height(28), GUILayout.Width(editW));
        GUI.backgroundColor = Color.white;
        if (newEdit != _editMode) { _editMode = newEdit; SceneView.RepaintAll(); }

        // Preview (1/5, a view mode alongside Curve Edit) / 预览（1/5，视图模式与曲线编辑并列）
        GUI.backgroundColor = _previewMode ? UiPreviewBlue : Color.white;
        bool newPrev = GUILayout.Toggle(_previewMode, $" {L10n.T("preview")}", "Button", GUILayout.Height(28), GUILayout.Width(previewW));
        GUI.backgroundColor = Color.white;
        if (newPrev != _previewMode) { _previewMode = newPrev; SceneView.RepaintAll(); }

        EditorGUILayout.EndHorizontal();
    }

    // ===== Business logic / 业务逻辑 =====

    /// <summary>Creates a new curve with the given up axis. / 用指定轴向创建新曲线</summary>
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

    /// <summary>Creates a 3D curve. / 创建 3D 曲线</summary>
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

    /// <summary>Deep-copies a curve, deduplicates its name, then inserts it into the list.
    /// 深拷贝曲线并去重命名后插入列表</summary>
    private void DuplicateCurve(BezierCurve source)
    {
        if (source == null) return;
        // JSON deep copy / JSON 深拷贝
        var clone = JsonUtility.FromJson<BezierCurve>(JsonUtility.ToJson(source));
        // Deduplicate name: baseName + Copy + number / 去重命名：原名+Copy+数字
        string baseName = clone.Name + "Copy";
        clone.Name = baseName;
        int dedup = 1;
        while (_manager.Curves.Exists(c => c.Name == clone.Name))
            clone.Name = $"{baseName}{dedup++}";
        // Reset non-serialized fields / 重置非序列化字段
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

    // ===== Settings persistence / 设置持久化 =====
    /// <summary>Settings file path (Assets-relative, inside the tool directory; follows folder moves).
    /// 设置文件路径（相对 Assets，位于工具目录内，随工具目录移动自动跟随）</summary>
    private static string SettingsPath => $"{CurveManager.ToolDirectory}/CurveToolSettings.json";

    [System.Serializable]
    private class ToolSettings
    {
        public float VertexSize = DefaultVertexSize;
        public float HandleEndSize = DefaultHandleEndSize;
        public float ArrowSize = DefaultArrowSize;
        public Color GenerationColor = DefaultGenerationColor;
        public Color VertexPointColor = DefaultVertexPointColor;
        public Color HandleEndPointColor = DefaultHandleEndPointColor;
        public float CursorDisplaySize = DefaultCursorDisplaySize;
        public bool UseEditorSnapSettings = true;
        public Vector3 SnapGridSize = DefaultSnapGridSize;
        public Vector3 SnapIncrementMove = DefaultSnapIncrementMove;
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
            UseEditorSnapSettings = UseEditorSnapSettings,
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
            UseEditorSnapSettings = s.UseEditorSnapSettings;
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

