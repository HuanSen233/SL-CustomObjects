using System.Collections.Generic;
using System.IO;
using ToolLib;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TriangleTool
{
    /// <summary>
    /// Triangle-face data manager — in-scene data carrier (MonoBehaviour on the hidden scene object
    /// __TriangleToolData__). Faces are saved/loaded with the scene (persisted via Ctrl+S); selection
    /// is session state (NonSerialized). Undo is supported natively via Undo.RecordObject on this component.
    /// 三角面数据管理器 — 场景数据载体（MonoBehaviour，挂在场景隐藏对象 __TriangleToolData__ 上）。
    /// 三角面随场景保存/加载（Ctrl+S 落盘）；选中态为会话状态（NonSerialized）。
    /// 通过 Undo.RecordObject 直接作用于该组件实现 Undo。
    /// </summary>
    public class TriangleFaceManager : MonoBehaviour, IToolCursorHost
    {
        /// <summary>Fixed name of the carrier object (hidden in the scene, serialized with it).
        /// 载体对象的固定名称（场景内隐藏对象，随场景序列化）</summary>
        public const string HostObjectName = "__TriangleToolData__";

        /// <summary>All triangle faces managed by the tool. / 工具管理的全部三角面</summary>
        public List<TriangleFace> Faces = new List<TriangleFace>();

        /// <summary>Selected face index (session state). / 选中面索引（会话状态）</summary>
        [System.NonSerialized] public int SelectedFaceIndex = -1;
        /// <summary>Selected vertex index within the selected face (0..2; -1 = none) (session state).
        /// 选中面内选中的顶点索引（0..2；-1=无）（会话状态）</summary>
        [System.NonSerialized] public int SelectedVertexIndex = -1;
        /// <summary>Last-clicked (anchor) face index — its list select button is drawn bright green.
        /// 最后点击（锚点）的面索引 — 其列表选择按钮高亮为亮绿色。</summary>
        [System.NonSerialized] public int AnchorFaceIndex = -1;

        // ===== Cursor (world-space reference point, shared with the curve tool) / 游标（世界空间参考点，与曲线工具共享） =====
        /// <summary>Cursor position in world space. / 游标世界坐标。</summary>
        public Vector3 CursorPosition;
        /// <summary>Whether the whole cursor is locked (not draggable). / 整个游标是否锁定（不可拖拽）。</summary>
        public bool CursorLocked;
        /// <summary>Per-axis cursor locks. / 游标分轴锁。</summary>
        public bool CursorLockX, CursorLockY, CursorLockZ;
        /// <summary>When on, transform center switches from the world origin to the cursor. / 开启时变换中心切换到游标。</summary>
        public bool CursorReferenceMode;

        // Explicit interface implementation maps the shared cursor contract onto the serialized fields above,
        // so the scene-persisted cursor data stays intact while the shared UI/render/interaction can drive it.
        // 显式接口实现把共享游标契约映射到上面这些可序列化字段上——场景持久化数据保持不变，
        // 而共享 UI/渲染/交互可直接驱动它们。
        Vector3 IToolCursorHost.CursorPosition { get => CursorPosition; set => CursorPosition = value; }
        bool IToolCursorHost.CursorLocked { get => CursorLocked; set => CursorLocked = value; }
        bool IToolCursorHost.CursorLockX { get => CursorLockX; set => CursorLockX = value; }
        bool IToolCursorHost.CursorLockY { get => CursorLockY; set => CursorLockY = value; }
        bool IToolCursorHost.CursorLockZ { get => CursorLockZ; set => CursorLockZ = value; }
        bool IToolCursorHost.CursorReferenceMode { get => CursorReferenceMode; set => CursorReferenceMode = value; }

        /// <summary>Currently selected face (null when none selected). / 当前选中的面（无选中时返回 null）</summary>
        public TriangleFace SelectedFace =>
            SelectedFaceIndex >= 0 && SelectedFaceIndex < Faces.Count ? Faces[SelectedFaceIndex] : null;

        /// <summary>Tool directory (Assets-relative path) — derived from the script location, follows folder moves.
        /// 工具目录（Assets 相对路径）— 基于脚本位置动态推导，目录移动后自动跟随。</summary>
        public static string ToolDirectory
        {
            get
            {
                // Locate the directory via the script asset (script name is unique in the project).
                // 通过脚本名查找脚本资产定位目录（脚本名在项目内唯一）。
                var guids = AssetDatabase.FindAssets("TriangleFaceManager t:MonoScript");
                return guids.Length > 0
                    ? Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guids[0])).Replace('\\', '/')
                    : "Assets/Tools/TriangleTool";
            }
        }

        // ===== Singleton binding / 单例绑定 =====

        static TriangleFaceManager()
        {
            // After a scene switch the cached instance is stale; rebind on next access (each scene owns its data).
            // 场景切换后旧缓存失效，下次访问时重绑到新场景（各场景持有自己的三角面数据）。
            EditorSceneManager.sceneOpened += (_, _) => _instance = null;
        }

        /// <summary>Triangle-face data carrier of the active scene (finds an existing component, creates a hidden carrier otherwise).
        /// 当前活动场景的三角面数据载体（查找已有组件，不存在则创建隐藏载体）</summary>
        public static TriangleFaceManager Instance
        {
            get
            {
                if (_instance == null || _instance.gameObject == null ||
                    _instance.gameObject.scene != SceneManager.GetActiveScene())
                    BindToActiveScene();
                return _instance;
            }
        }
        private static TriangleFaceManager _instance;

        /// <summary>Binds to a saved carrier component in the active scene, or creates a hidden carrier object.
        /// 在活动场景中查找已保存的载体组件；找不到则创建隐藏载体对象</summary>
        private static void BindToActiveScene()
        {
            _instance = null;
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    var found = root.GetComponentInChildren<TriangleFaceManager>(true);
                    if (found != null) { _instance = found; break; }
                }
            }

            if (_instance == null)
            {
                // Hidden from the hierarchy but still serialized with the scene.
                // 隐藏于层级面板，但仍随场景序列化保存。
                var go = new GameObject(HostObjectName);
                go.hideFlags = HideFlags.HideInHierarchy;
                _instance = go.AddComponent<TriangleFaceManager>();
            }
        }

        // ===== Data operations / 数据操作 =====

        /// <summary>Adds a new default triangle face (name de-duplicated) and selects it.
        /// 添加一个默认三角面（名称去重）并选中。</summary>
        public TriangleFace AddFace(string name = "NewTriangle")
        {
            Undo.RecordObject(this, "添加三角面");
            string finalName = NameUtil.Deduplicate(name, n => Faces.Exists(f => f.Name == n));
            var face = TriangleFace.CreateDefault(finalName);
            Faces.Add(face);
            Select(Faces.Count - 1);
            MarkDirty();
            return face;
        }

        /// <summary>Adds a given triangle face (with deduplicated name) and selects it. / 添加指定三角面（名称去重）并选中。</summary>
        public TriangleFace AddFace(TriangleFace face)
        {
            if (face == null) return null;
            Undo.RecordObject(this, "添加三角面");
            face.Name = NameUtil.Deduplicate(face.Name, n => Faces.Exists(f => f.Name == n));
            Faces.Add(face);
            Select(Faces.Count - 1);
            MarkDirty();
            return face;
        }

        /// <summary>Removes a face by index. / 删除面</summary>
        public void RemoveFace(int index)
        {
            if (index < 0 || index >= Faces.Count) return;
            Undo.RecordObject(this, "删除三角面");
            Faces.RemoveAt(index);
            ClearSelection();
            MarkDirty();
        }

        /// <summary>Selects a face. additive=true toggles the face into the multi-select set (Shift/Ctrl in the
        /// list); the selected face becomes the anchor (last-clicked, bright green in the list).
        /// 选中一个面。additive=true 时把该面切换进多选集合（列表内 Shift/Ctrl）；该面成为锚点（最后点击，列表亮绿）。</summary>
        public void Select(int index, bool additive = false)
        {
            if (index < 0 || index >= Faces.Count) return;
            var f = Faces[index];
            if (additive)
                f.IsSelected = !f.IsSelected; // toggle membership / 切换多选成员关系
            else
            {
                foreach (var x in Faces) x.IsSelected = false;
                f.IsSelected = true;
            }
            SelectedFaceIndex = index;
            AnchorFaceIndex = index;
            SelectedVertexIndex = -1;
        }

        /// <summary>Whether any face is currently multi-selected. / 是否已有任一三角面被多选。</summary>
        public bool HasSelection()
        {
            foreach (var f in Faces)
                if (f != null && f.IsSelected) return true;
            return false;
        }

        /// <summary>Clears all selection state. / 清除所有选中</summary>
        public void ClearSelection()
        {
            foreach (var f in Faces)
                f.IsSelected = false;
            SelectedFaceIndex = -1;
            SelectedVertexIndex = -1;
            AnchorFaceIndex = -1;
        }

        /// <summary>Marks data as changed: component dirty + scene dirty (required for Ctrl+S persistence).
        /// 标记数据变更：组件脏 + 场景脏（保证 Ctrl+S 时落盘）</summary>
        public void MarkDirty()
        {
            EditorUtility.SetDirty(this);
            var scene = gameObject != null ? gameObject.scene : default;
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
