using System.Collections.Generic;
using ToolLib;
using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// Triangle Tool — EditorWindow skeleton (fields, lifecycle, persistence, business logic).
    /// UI 重构参考 CurvesTool：partial class EditorWindow + 三标签页（编辑/模型导入/设置） + 折叠区 + L10n 双语 + JSON 设置持久化。
    /// UI panels are split into TriangleTool.EditTab.cs, TriangleTool.ImportTab.cs and TriangleTool.SettingsTab.cs.
    /// UI 面板拆分至 TriangleTool.EditTab.cs、TriangleTool.ImportTab.cs 和 TriangleTool.SettingsTab.cs。
    /// </summary>
    public partial class TriangleTool : EditorWindow
    {
        // ===== UI color constants (unified button/row colors) / UI 颜色常量（统一按钮配色） =====
        public static readonly Color UiActionGreen = new Color(0.4f, 0.85f, 0.4f);
        public static readonly Color UiCreateGreen = new Color(0.3f, 0.8f, 0.3f);
        public static readonly Color UiCreateBlue = new Color(0.3f, 0.5f, 0.9f);
        public static readonly Color UiCopyBlue = new Color(0.5f, 0.75f, 1f);
        public static readonly Color UiDeleteRed = new Color(0.9f, 0.3f, 0.3f);
        public static readonly Color UiDeleteAllRed = new Color(0.85f, 0.3f, 0.3f);
        public static readonly Color UiSelectedBg = new Color(0.3f, 0.6f, 1f, 0.3f);
        public static readonly Color UiDisabledGray = new Color(0.5f, 0.5f, 0.5f);
        public static readonly Color UiPlaceholderGray = new Color(0.4f, 0.4f, 0.4f);
        public static readonly Color UiLockedOrange = new Color(0.9f, 0.6f, 0.3f);
        public static readonly Color UiSelectedBlue = new Color(0.3f, 0.6f, 1f);
        public static readonly Color UiPreviewBlue = new Color(0.4f, 0.7f, 1f);
        public static readonly Color UiAnchorGreen = new Color(0.4f, 1f, 0.4f); // bright green: last-clicked (anchor) select button / 亮绿：最后点击（锚点）选择按钮

        // ===== Default value constants (single source shared by field init and Reset) / 默认值常量（唯一来源） =====
        public const float DefaultAccuracy = 0.001f;
        public const int DefaultOptimizationPasses = 3;
        public const float DefaultRectangleTolerance = 1e-4f;
        public static readonly Color DefaultFaceColor = new Color(1f, 0.3f, 0.7f, 1f);
        public static readonly Color DefaultFallbackColor = Color.white;
        public static readonly Vector3 DefaultSnapGridSize = new Vector3(0.5f, 0.5f, 0.5f);
        public static readonly Vector3 DefaultSnapIncrementMove = Vector3.one;
        /// <summary>Default max spawned blocks per frame for the Edit tab's generate. / 编辑页生成默认每帧最大块数。</summary>
        public const int DefaultEditMaxBlocksPerFrame = 200;
        /// <summary>Default max spawned blocks per frame for the Model Import build. / 模型导入构建默认每帧最大块数。</summary>
        public const int DefaultImportMaxBlocksPerFrame = 100;

        // ===== Configurable properties (persisted) / 可配置属性（持久化） =====
        public TriangleBuildMode Mode = TriangleBuildMode.Exact;
        /// <summary>Build mode used by the Model Import tab's OBJ build (independent of Edit tab's Mode).
        /// 模型导入 Tab 的 OBJ 构建模式（独立于编辑页的 Mode）。</summary>
        public TriangleBuildMode ObjMode = TriangleBuildMode.Exact;
        public float Accuracy = DefaultAccuracy;
        public int OptimizationPasses = DefaultOptimizationPasses;
        public float RectangleTolerance = DefaultRectangleTolerance;
        public bool UseRectangleOptimization = true;
        public bool FlipWinding;
        public bool Collidable;
        public Color FaceColor = DefaultFaceColor;
        public Color FallbackColor = DefaultFallbackColor;
        /// <summary>Max spawned blocks per frame for the Edit tab's generate. / 编辑页生成每帧最大块数。</summary>
        public int EditMaxBlocksPerFrame = DefaultEditMaxBlocksPerFrame;
        /// <summary>Max spawned blocks per frame for the Model Import build. / 模型导入构建每帧最大块数。</summary>
        public int ImportMaxBlocksPerFrame = DefaultImportMaxBlocksPerFrame;

        // ===== Scene data manager (faces) / 场景数据管理器（三角面） =====
        private TriangleFaceManager _manager;
        /// <summary>Edit mode: scene point dragging + wireframes are active. / 编辑模式：场景点拖拽与线框生效。</summary>
        private bool _editMode = true;
        /// <summary>Preview: highlight triangle face outlines (non-editing presentation). / 预览：高亮三角形轮廓（非编辑展示）。</summary>
        private bool _previewOn;

        public bool IsEditMode => _editMode;
        public bool IsPreviewOn => _previewOn;

        // ===== Move-tool / snapping settings / 移动工具与吸附设置 =====
        /// <summary>Move face points with the Unity Move tool (W) instead of the built-in drag; on when true,
        /// the tool's own point drag is suppressed. / 使用 Unity 移动工具（W）移动面的点而非工具自带拖拽；开启时屏蔽工具自身点拖拽。</summary>
        public bool UseMoveTool = true;
        /// <summary>Use the editor's snap settings (EditorSnapSettings); disable to use tool-local values.
        /// 使用编辑器吸附设定；关闭则用工具自身步长。</summary>
        public bool UseEditorSnapSettings = true;
        public Vector3 SnapGridSize = DefaultSnapGridSize;
        public Vector3 SnapIncrementMove = DefaultSnapIncrementMove;

        // ===== Window state / 窗口状态 =====
        public static TriangleTool Instance { get; private set; }
        private int _selectedTab;
        private string[] _tabs;
        private string[] Tabs => _tabs ??= new[] { TriangleL10n.T("edit_tab"), TriangleL10n.T("import_tab"), TriangleL10n.T("settings_tab") };

        // ===== Edit-tab foldout state / 编辑页折叠状态 =====
        private bool _foldoutStats = true;

        // ===== OBJ section state / OBJ 面板状态 =====
        private string _objPath;
        private ObjBuildSession _objSession;
        private string _objError;
        private bool _objForceColor;

        // ===== Edit-tab generate session (framed) / 编辑页生成会话（分帧） =====
        private ObjBuildSession _editSession;

        // ===== Last build (for statistics) / 最近一次构建（供统计显示） =====
        /// <summary>Latest model generated by the Edit tab's Generate (face group). / 编辑页"生成"产生的模型。</summary>
        private TriangleModelBuilder _lastBuilder;
        /// <summary>Latest model generated by the Model Import tab (OBJ). / 模型导入 Tab（OBJ）产生的模型。</summary>
        private TriangleModelBuilder _objBuilder;

        // ===== Window lifecycle / 窗口生命周期 =====

        [MenuItem("Tools/Triangle Tool/Open Window")]
        public static void OpenWindow()
        {
            var w = GetWindow<TriangleTool>("Triangle Tool");
            w.minSize = new Vector2(360f, 520f);
            w.Show();
        }

        /// <summary>Menu: add a quick example triangle face to the list for verification.
        /// 菜单：向列表添加示例三角面用于验证。</summary>
        [MenuItem("Tools/Triangle Tool/Create Example Triangle")]
        public static void CreateExample()
        {
            var w = GetWindow<TriangleTool>("Triangle Tool");
            w.AddExampleFace();
            w.Show();
        }

        /// <summary>Menu: load the bundled Suzanne test model (TestModels/Suzanne/Suzanne.obj).
        /// 菜单：加载自带的 Suzanne 测试模型（TestModels/Suzanne/Suzanne.obj）。</summary>
        [MenuItem("Tools/Triangle Tool/Load Suzanne Test Model")]
        public static void LoadSuzanne()
        {
            var w = GetWindow<TriangleTool>("Triangle Tool");
            w._objPath = System.IO.Path.Combine(ObjModelLoader.ModelsDirectory, "Suzanne", "Suzanne.obj");
            w.LoadObj();
            w.Show();
        }

        private void OnEnable()
        {
            Instance = this;
            _manager = TriangleFaceManager.Instance;
            LoadSettings();
            TriangleSceneRenderer.Register();
            TriangleSceneEditor.Register();
            UpdateTitle();
        }

        private void OnDisable()
        {
            SaveSettings();
            if (Instance == this) Instance = null;
            _editMode = false;
            EditModeGate.Request("triangle", false, null);
            TriangleSceneRenderer.Unregister();
            TriangleSceneEditor.Unregister();
            SceneView.RepaintAll();
        }

        private void UpdateTitle()
        {
            titleContent.text = TriangleL10n.T("window_title");
            _tabs = null;
        }

        // ===== Main GUI / 主 GUI =====

        private void OnGUI()
        {
            // Rebind after scene switches: the cached _manager belongs to the scene active when the window opened.
            // 场景切换后重新绑定：缓存的 _manager 属于窗口打开时的场景，可能已过期。
            if (_manager == null || _manager != TriangleFaceManager.Instance) _manager = TriangleFaceManager.Instance;

            DrawHeader();
            _selectedTab = Mathf.Clamp(_selectedTab, 0, Tabs.Length - 1);
            _selectedTab = GUILayout.Toolbar(_selectedTab, Tabs);
            GUILayout.Space(6);
            if (_selectedTab == 0) TabEdit();
            else if (_selectedTab == 1) TabModelImport();
            else if (_selectedTab == 2) TabSettings();
        }

        /// <summary>Draws the centered window header. / 绘制居中窗口标题头。</summary>
        private void DrawHeader()
        {
            GUILayout.Space(6);
            var ts = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField(TriangleL10n.T("window_title"), ts);
            GUILayout.Space(4);
        }

        // ===== Shared build logic / 共享构建逻辑 =====

        /// <summary>Adds an example triangle face to the selected scene (used by the Create Example menu).
        /// 向选中场景添加一个示例三角面（Create Example 菜单用）。</summary>
        public void AddExampleFace()
        {
            _manager = TriangleFaceManager.Instance;
            if (_manager == null) return;
            _manager.AddFace("ExampleTriangle");
            SceneView.RepaintAll();
        }

        /// <summary>Builds a brand-new generated model on each click, leaving any previously generated object
        /// untouched. The currently-selected faces are grouped together first (falling back to all enabled +
        /// visible faces when nothing is selected). The top-level parent name is de-duplicated in the scene.
        /// Build is framed per EditMaxBlocksPerFrame so a large group does not freeze the editor.
        /// 每次点击都新建一个生成模型，不碰之前生成的物体；把当前选中的面规划为一组再生成。
        /// 顶层父名在场景内去重；构建按 EditMaxBlocksPerFrame 分帧，避免大选区冻结编辑器。</summary>
        private void GenerateFaces()
        {
            if (_manager == null || _manager.Faces.Count == 0) return;

            // Cancel any in-flight edit build (do NOT destroy its model — leave old objects untouched).
            // 取消进行中的编辑构建（不销毁其模型——保留旧物体）。
            if (_editSession != null) _editSession.Cancel();

            string rootName = NameUtil.DeduplicateObjectName("TriangleModel (Tool)");

            // Gather the selected group; a disabled face is never generated (mirrors the curve tool's Enable).
            // 收集选中组；未启用面永不生成（同曲线工具的"启用"按钮）。
            var triangles = new List<TriangleData>();
            bool anySelected = _manager.HasSelection();
            foreach (var face in _manager.Faces)
            {
                if (face == null || !face.IsEnabled) continue;
                if (anySelected && !face.IsSelected) continue;
                triangles.Add(new TriangleData(face.P1, face.P2, face.P3, face.Color));
            }
            if (triangles.Count == 0) return;

            _editSession = new ObjBuildSession(rootName, Mode, Accuracy, OptimizationPasses, Collidable,
                UseRectangleOptimization, RectangleTolerance, FlipWinding,
                maxBlocksPerFrame: Mathf.Max(1, EditMaxBlocksPerFrame), onComplete: OnEditBuildComplete);
            _editSession.Start(triangles);
            SceneView.RepaintAll();
        }

        private void OnEditBuildComplete()
        {
            if (_editSession == null || _editSession.Builder == null) return;
            _lastBuilder = _editSession.Builder;
            if (_lastBuilder.Root != null) Selection.activeGameObject = _lastBuilder.Root;
            Repaint();
        }

        // ===== OBJ business logic / OBJ 业务逻辑 =====

        private void BrowseObj()
        {
            string dir = string.IsNullOrEmpty(_objPath) ? ObjModelLoader.ModelsDirectory : System.IO.Path.GetDirectoryName(_objPath);
            string selected = EditorUtility.OpenFilePanel(TriangleL10n.T("obj_model"), dir, "obj");
            if (!string.IsNullOrEmpty(selected))
            {
                _objPath = selected;
                Repaint();
            }
        }

        private void LoadObj()
        {
            if (string.IsNullOrWhiteSpace(_objPath))
            {
                _objError = TriangleL10n.T("err_no_obj_path");
                return;
            }

            Color fallback = FallbackColor;
            bool forceColor = _objForceColor;

            if (!ObjModelLoader.TryLoadTriangles(_objPath, fallback, forceColor,
                    out List<TriangleData> triangles, out string fileName, out string error))
            {
                _objError = error;
                return;
            }

            _objError = string.Empty;
            ClearObjModel();

            _objSession = new ObjBuildSession("TriangleModel_Obj (Tool)",
                ObjMode, Accuracy, OptimizationPasses, Collidable,
                UseRectangleOptimization, RectangleTolerance, FlipWinding,
                maxBlocksPerFrame: Mathf.Max(1, ImportMaxBlocksPerFrame), onComplete: OnObjBuildComplete);

            _objSession.Start(triangles);
            Debug.Log("[TriangleTool] Parsed '" + fileName + "': " + triangles.Count + " triangles, mode=" + ObjMode + ".");
            Repaint();
        }

        private void OnObjBuildComplete()
        {
            if (_objSession == null || _objSession.Builder == null)
                return;

            _objBuilder = _objSession.Builder;
            Debug.Log("[TriangleTool] OBJ build finished: quads=" + _objBuilder.QuadCount +
                      " stretches=" + _objBuilder.StretchCount + ".");

            if (_objBuilder.Root != null)
                Selection.activeGameObject = _objBuilder.Root;

            Repaint();
        }

        private void CancelObj()
        {
            if (_objSession != null)
                _objSession.Cancel();
        }

        /// <summary>Clears the OBJ build session + model (called automatically before a new OBJ load).
        /// 清除 OBJ 构建会话与模型（加载新 OBJ 前自动调用）。</summary>
        private void ClearObjModel()
        {
            if (_objSession != null)
            {
                _objSession.Cancel();
                if (_objSession.Builder != null)
                    _objSession.Builder.DestroyImmediate();
                _objSession = null;
            }

            if (_objBuilder != null && _objBuilder.Root != null &&
                _objBuilder.Root.name == "TriangleModel_Obj (Tool)")
                _objBuilder = null;

            SceneView.RepaintAll();
        }

        // ===== Statistics / 统计 =====

        /// <summary>Face-group model stats source (Edit tab's Generate only). / 编辑页生成模型的统计来源。</summary>
        private TriangleModelBuilder FaceStatsBuilder => _lastBuilder;

        /// <summary>OBJ model stats source (Model Import tab). / 模型导入 OBJ 模型的统计来源。</summary>
        private TriangleModelBuilder ObjStatsBuilder =>
            _objSession != null && _objSession.Builder != null ? _objSession.Builder : _objBuilder;

        // ===== Settings persistence / 设置持久化 =====

        private static string SettingsPath =>
            System.IO.Path.Combine(Application.dataPath, "Tools", "TriangleTool", "TriangleToolSettings.json");

        [System.Serializable]
        private class ToolSettings
        {
            public int Mode = (int)TriangleBuildMode.Exact;
            public int ObjMode = (int)TriangleBuildMode.Exact;
            public float Accuracy = DefaultAccuracy;
            public int OptimizationPasses = DefaultOptimizationPasses;
            public float RectangleTolerance = DefaultRectangleTolerance;
            public bool UseRectangleOptimization = true;
            public bool FlipWinding;
            public bool Collidable;
            public Color FaceColor = DefaultFaceColor;
            public Color FallbackColor = DefaultFallbackColor;
            public int EditMaxBlocksPerFrame = DefaultEditMaxBlocksPerFrame;
            public int ImportMaxBlocksPerFrame = DefaultImportMaxBlocksPerFrame;
            public bool UseMoveTool = true;
            public bool UseEditorSnapSettings = true;
            public Vector3 SnapGridSize = DefaultSnapGridSize;
            public Vector3 SnapIncrementMove = DefaultSnapIncrementMove;
            public int Language = 0; // 0=EN, 1=ZH
        }

        private void SaveSettings()
        {
            try
            {
                var s = new ToolSettings
                {
                    Mode = (int)Mode,
                    ObjMode = (int)ObjMode,
                    Accuracy = Accuracy,
                    OptimizationPasses = OptimizationPasses,
                    RectangleTolerance = RectangleTolerance,
                    UseRectangleOptimization = UseRectangleOptimization,
                    FlipWinding = FlipWinding,
                    Collidable = Collidable,
                    FaceColor = FaceColor,
                    FallbackColor = FallbackColor,
                    EditMaxBlocksPerFrame = EditMaxBlocksPerFrame,
                    ImportMaxBlocksPerFrame = ImportMaxBlocksPerFrame,
                    UseMoveTool = UseMoveTool,
                    UseEditorSnapSettings = UseEditorSnapSettings,
                    SnapGridSize = SnapGridSize,
                    SnapIncrementMove = SnapIncrementMove,
                    Language = (int)TriangleL10n.Current,
                };
                System.IO.File.WriteAllText(SettingsPath, JsonUtility.ToJson(s, prettyPrint: true));
            }
            catch { /* 忽略写入失败 */ }
        }

        private void LoadSettings()
        {
            if (!System.IO.File.Exists(SettingsPath)) return;
            try
            {
                var s = JsonUtility.FromJson<ToolSettings>(System.IO.File.ReadAllText(SettingsPath));
                if (s == null) return;
                Mode = (TriangleBuildMode)s.Mode;
                ObjMode = (TriangleBuildMode)s.ObjMode;
                Accuracy = s.Accuracy;
                OptimizationPasses = s.OptimizationPasses;
                RectangleTolerance = s.RectangleTolerance;
                UseRectangleOptimization = s.UseRectangleOptimization;
                FlipWinding = s.FlipWinding;
                Collidable = s.Collidable;
                FaceColor = s.FaceColor;
                FallbackColor = s.FallbackColor;
                EditMaxBlocksPerFrame = s.EditMaxBlocksPerFrame;
                ImportMaxBlocksPerFrame = s.ImportMaxBlocksPerFrame;
                UseMoveTool = s.UseMoveTool;
                UseEditorSnapSettings = s.UseEditorSnapSettings;
                SnapGridSize = s.SnapGridSize;
                SnapIncrementMove = s.SnapIncrementMove;
                TriangleL10n.SetLanguage((TriangleL10n.Lang)s.Language);
            }
            catch { /* 忽略损坏的配置文件 */ }
        }
    }
}
