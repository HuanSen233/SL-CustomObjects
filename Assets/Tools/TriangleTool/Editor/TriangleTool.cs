using System.Collections.Generic;
using DONT_TOUCH.Scripts.BlockComponents;
using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// Triangle Tool — EditorWindow skeleton (fields, lifecycle, persistence, business logic).
    /// UI 重构参考 CurvesTool：partial class EditorWindow + 双标签页 + 折叠区 + L10n 双语 + JSON 设置持久化。
    /// UI panels are split into TriangleTool.EditTab.cs and TriangleTool.SettingsTab.cs.
    /// UI 面板拆分至 TriangleTool.EditTab.cs 和 TriangleTool.SettingsTab.cs。
    /// </summary>
    public partial class TriangleTool : EditorWindow
    {
        // ===== UI color constants (unified button/row colors) / UI 颜色常量（统一按钮配色） =====
        public static readonly Color UiActionGreen = new Color(0.4f, 0.85f, 0.4f);
        public static readonly Color UiCreateGreen = new Color(0.3f, 0.8f, 0.3f);
        public static readonly Color UiCreateBlue = new Color(0.3f, 0.5f, 0.9f);
        public static readonly Color UiCopyBlue = new Color(0.5f, 0.75f, 1f);
        public static readonly Color UiDeleteRed = new Color(0.9f, 0.3f, 0.3f);
        public static readonly Color UiSelectedBg = new Color(0.3f, 0.6f, 1f, 0.3f);
        public static readonly Color UiDisabledGray = new Color(0.5f, 0.5f, 0.5f);

        // ===== Default value constants (single source shared by field init and Reset) / 默认值常量（唯一来源） =====
        public const float DefaultAccuracy = 0.001f;
        public const int DefaultOptimizationPasses = 3;
        public const float DefaultRectangleTolerance = 1e-4f;
        public static readonly Color DefaultFaceColor = new Color(1f, 0.3f, 0.7f, 1f);
        public static readonly Color DefaultFallbackColor = Color.white;

        // ===== Configurable properties (persisted) / 可配置属性（持久化） =====
        public TriangleBuildMode Mode = TriangleBuildMode.Exact;
        public float Accuracy = DefaultAccuracy;
        public int OptimizationPasses = DefaultOptimizationPasses;
        public float RectangleTolerance = DefaultRectangleTolerance;
        public bool UseRectangleOptimization = true;
        public bool FlipWinding;
        public bool Collidable;
        public Color FaceColor = DefaultFaceColor;
        public Color FallbackColor = DefaultFallbackColor;

        // ===== Window state / 窗口状态 =====
        public static TriangleTool Instance { get; private set; }
        private int _selectedTab;
        private string[] _tabs;
        private string[] Tabs => _tabs ??= new[] { TriangleL10n.T("edit_tab"), TriangleL10n.T("settings_tab") };

        // ===== Edit-tab foldout state / 编辑页折叠状态 =====
        private bool _foldoutSingle = true;
        private bool _foldoutObj = true;
        private bool _foldoutStats = true;

        // ===== Single-triangle edit buffers / 单三角形编辑缓冲 =====
        private Vector3 _p1 = new Vector3(0f, 0f, 0f);
        private Vector3 _p2 = new Vector3(2f, 0f, 0f);
        private Vector3 _p3 = new Vector3(0.3f, 1.6f, 0f);
        private float _scale = 1f;
        private GameObject _markersRoot;
        private readonly List<GameObject> _markers = new List<GameObject>();

        // ===== OBJ section state / OBJ 面板状态 =====
        private string _objPath;
        private ObjBuildSession _objSession;
        private string _objError;
        private bool _objForceColor;

        // ===== Last build (for statistics) / 最近一次构建（供统计显示） =====
        private TriangleModelBuilder _lastBuilder;

        // ===== Window lifecycle / 窗口生命周期 =====

        [MenuItem("Tools/Triangle Tool/Open Window")]
        public static void OpenWindow()
        {
            var w = GetWindow<TriangleTool>("Triangle Tool");
            w.minSize = new Vector2(360f, 520f);
            w.Show();
        }

        /// <summary>Menu: create a quick example triangle in the scene for verification.
        /// 菜单：在场景中创建示例三角形用于验证。</summary>
        [MenuItem("Tools/Triangle Tool/Create Example Triangle")]
        public static void CreateExample()
        {
            var w = GetWindow<TriangleTool>("Triangle Tool");
            w._p1 = new Vector3(0f, 0f, 0f);
            w._p2 = new Vector3(2f, 0f, 0f);
            w._p3 = new Vector3(0.3f, 1.6f, 0f);
            w.BuildSingleTriangle();
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
            LoadSettings();
            UpdateTitle();
        }

        private void OnDisable()
        {
            SaveSettings();
            if (Instance == this) Instance = null;
        }

        private void UpdateTitle()
        {
            titleContent.text = TriangleL10n.T("window_title");
            _tabs = null;
        }

        // ===== Main GUI / 主 GUI =====

        private void OnGUI()
        {
            DrawHeader();
            _selectedTab = Mathf.Clamp(_selectedTab, 0, Tabs.Length - 1);
            _selectedTab = GUILayout.Toolbar(_selectedTab, Tabs);
            GUILayout.Space(6);
            if (_selectedTab == 0) TabEdit();
            else if (_selectedTab == 1) TabSettings();
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

        /// <summary>Create a fresh TriangleModelBuilder configured from the current window settings.
        /// 用当前窗口设置创建新的 TriangleModelBuilder。</summary>
        private TriangleModelBuilder CreateConfiguredBuilder()
        {
            return new TriangleModelBuilder
            {
                Mode = Mode,
                Accuracy = Accuracy,
                OptimizationPasses = OptimizationPasses,
                UseRectangleOptimization = UseRectangleOptimization,
                RectangleTolerance = RectangleTolerance,
                FlipWinding = FlipWinding,
                Collidable = Collidable,
            };
        }

        /// <summary>Build (or rebuild) the single triangle with markers. / 构建（或重建）单三角形与标记。</summary>
        private void BuildSingleTriangle()
        {
            ClearSingleModel();

            Vector3 centroid = (_p1 + _p2 + _p3) / 3f;
            float s = Mathf.Max(_scale, 1e-6f);
            Vector3 p1 = centroid + (_p1 - centroid) * s;
            Vector3 p2 = centroid + (_p2 - centroid) * s;
            Vector3 p3 = centroid + (_p3 - centroid) * s;

            var builder = CreateConfiguredBuilder();
            builder.EnsureRoot("TriangleModel (Tool)");
            builder.BuildOneTriangle(new TriangleData(p1, p2, p3, FaceColor));
            builder.Finish();
            _lastBuilder = builder;

            CreateMarkers(p1, p2, p3);
            Selection.activeGameObject = builder.Root;
            SceneView.RepaintAll();
        }

        /// <summary>Clear the single-triangle model and markers. / 清除单三角形模型与标记。</summary>
        private void ClearSingleModel()
        {
            if (_lastBuilder != null && _lastBuilder.Root != null &&
                _lastBuilder.Root.name == "TriangleModel (Tool)")
            {
                _lastBuilder.DestroyImmediate();
                _lastBuilder = null;
            }

            foreach (GameObject marker in _markers)
            {
                if (marker != null)
                    DestroyImmediate(marker);
            }
            _markers.Clear();

            if (_markersRoot != null)
            {
                DestroyImmediate(_markersRoot);
                _markersRoot = null;
            }

            SceneView.RepaintAll();
        }

        /// <summary>Read the first 3 selected transforms as triangle vertices. / 读取选中前 3 个 Transform 作为三角形顶点。</summary>
        private void ReadFromSelection()
        {
            var selected = Selection.transforms;
            if (selected == null || selected.Length < 3)
            {
                EditorUtility.DisplayDialog(TriangleL10n.T("window_title"),
                    TriangleL10n.T("read_selection"), TriangleL10n.T("ok"));
                return;
            }

            _p1 = selected[0].position;
            _p2 = selected[1].position;
            _p3 = selected[2].position;
            Repaint();
        }

        /// <summary>Create small colored sphere blocks (project Sphere.prefab) marking the three vertices.
        /// 创建标记三个顶点的小彩球（项目 Sphere.prefab）。</summary>
        private void CreateMarkers(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            EmptyComponent empty = ProjectBlockFactory.CreateEmpty("TriangleModel Markers");
            _markersRoot = empty != null ? empty.gameObject : new GameObject("TriangleModel Markers");

            _markers.Add(CreateMarker(p1, Color.red));
            _markers.Add(CreateMarker(p2, Color.green));
            _markers.Add(CreateMarker(p3, Color.blue));

            foreach (GameObject marker in _markers)
                marker.transform.SetParent(_markersRoot.transform, true);
        }

        private static GameObject CreateMarker(Vector3 position, Color color)
        {
            PrimitiveComponent sphere = ProjectBlockFactory.CreatePrimitive(
                PrimitiveType.Sphere, "VertexMarker", color, visible: true, collidable: false);

            GameObject go = sphere != null ? sphere.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.08f;
            return go;
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
                Mode, Accuracy, OptimizationPasses, Collidable,
                UseRectangleOptimization, RectangleTolerance, FlipWinding,
                batchSize: 120, onComplete: OnObjBuildComplete);

            _objSession.Start(triangles);
            Debug.Log("[TriangleTool] Parsed '" + fileName + "': " + triangles.Count + " triangles, mode=" + Mode + ".");
            Repaint();
        }

        private void OnObjBuildComplete()
        {
            if (_objSession == null || _objSession.Builder == null)
                return;

            _lastBuilder = _objSession.Builder;
            Debug.Log("[TriangleTool] OBJ build finished: quads=" + _lastBuilder.QuadCount +
                      " stretches=" + _lastBuilder.StretchCount + ".");

            if (_lastBuilder.Root != null)
                Selection.activeGameObject = _lastBuilder.Root;

            Repaint();
        }

        private void CancelObj()
        {
            if (_objSession != null)
                _objSession.Cancel();
        }

        private void ClearObjModel()
        {
            if (_objSession != null)
            {
                _objSession.Cancel();
                if (_objSession.Builder != null)
                    _objSession.Builder.DestroyImmediate();
                _objSession = null;
            }

            if (_lastBuilder != null && _lastBuilder.Root != null &&
                _lastBuilder.Root.name == "TriangleModel_Obj (Tool)")
                _lastBuilder = null;

            SceneView.RepaintAll();
        }

        // ===== Statistics / 统计 =====

        private TriangleModelBuilder ActiveStatsBuilder =>
            _objSession != null && _objSession.Builder != null ? _objSession.Builder : _lastBuilder;

        // ===== Settings persistence / 设置持久化 =====

        private static string SettingsPath =>
            System.IO.Path.Combine(Application.dataPath, "Tools", "TriangleTool", "TriangleToolSettings.json");

        [System.Serializable]
        private class ToolSettings
        {
            public int Mode = (int)TriangleBuildMode.Exact;
            public float Accuracy = DefaultAccuracy;
            public int OptimizationPasses = DefaultOptimizationPasses;
            public float RectangleTolerance = DefaultRectangleTolerance;
            public bool UseRectangleOptimization = true;
            public bool FlipWinding;
            public bool Collidable;
            public Color FaceColor = DefaultFaceColor;
            public Color FallbackColor = DefaultFallbackColor;
            public int Language = 0; // 0=EN, 1=ZH
        }

        private void SaveSettings()
        {
            try
            {
                var s = new ToolSettings
                {
                    Mode = (int)Mode,
                    Accuracy = Accuracy,
                    OptimizationPasses = OptimizationPasses,
                    RectangleTolerance = RectangleTolerance,
                    UseRectangleOptimization = UseRectangleOptimization,
                    FlipWinding = FlipWinding,
                    Collidable = Collidable,
                    FaceColor = FaceColor,
                    FallbackColor = FallbackColor,
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
                Accuracy = s.Accuracy;
                OptimizationPasses = s.OptimizationPasses;
                RectangleTolerance = s.RectangleTolerance;
                UseRectangleOptimization = s.UseRectangleOptimization;
                FlipWinding = s.FlipWinding;
                Collidable = s.Collidable;
                FaceColor = s.FaceColor;
                FallbackColor = s.FallbackColor;
                TriangleL10n.SetLanguage((TriangleL10n.Lang)s.Language);
            }
            catch { /* 忽略损坏的配置文件 */ }
        }
    }
}
