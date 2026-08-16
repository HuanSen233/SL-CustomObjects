using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Curve data manager — in-scene data carrier (MonoBehaviour on the hidden scene object __CurvesToolData__).
/// Curve data is saved/loaded with the scene (persisted via Ctrl+S); selection is session state (NonSerialized).
/// Undo supported natively: Undo.RecordObject works directly on the scene component.
/// 曲线数据管理器 — 场景数据载体（MonoBehaviour，挂在场景隐藏对象 __CurvesToolData__ 上）。
/// 曲线数据随场景保存/加载（Ctrl+S 落盘）；选中态为会话状态（NonSerialized）。
/// 支持 Undo：Undo.RecordObject 直接作用于场景组件。
/// </summary>
public class CurveManager : MonoBehaviour
{
    /// <summary>Fixed name of the carrier object (hidden in the scene, serialized with it).
    /// 载体对象的固定名称（场景内隐藏对象，随场景序列化）</summary>
    public const string HostObjectName = "__CurvesToolData__";

    /// <summary>Legacy asset path (one-time migration; deleted afterwards).
    /// 旧资产路径（一次性迁移用，迁移完成后删除）</summary>
    private const string LegacyAssetPath = "Assets/Tools/CurvesTool/CurveManager.asset";

    /// <summary>All curves managed by the tool. / 工具管理的全部曲线</summary>
    public List<BezierCurve> Curves = new List<BezierCurve>();

    /// <summary>Selected curve index (session state). / 选中曲线索引（会话状态）</summary>
    [System.NonSerialized] public int SelectedCurveIndex = -1;
    /// <summary>Selected vertex index (session state). / 选中顶点索引（会话状态）</summary>
    [System.NonSerialized] public int SelectedVertexIndex = -1;
    /// <summary>Shift multi-selected vertex indices (session state).
    /// Shift 多选的顶点索引列表</summary>
    [System.NonSerialized] public List<int> SelectedVertexIndices = new List<int>();

    // ===== Cursor / 游标 =====
    /// <summary>Cursor position in world space (independent reference point).
    /// 游标在世界空间中的位置（独立参考点）</summary>
    public Vector3 CursorPosition;
    /// <summary>When locked, mouse dragging is disabled and the wireframe turns orange.
    /// 锁定游标时禁止鼠标拖拽，线框变橙色</summary>
    public bool CursorLocked;
    /// <summary>Per-axis cursor locks (protect axes during dragging and reset).
    /// 分轴锁定游标位置（拖拽和重置时保护对应轴）</summary>
    public bool CursorLockX, CursorLockY, CursorLockZ;

    /// <summary>Cursor reference mode: the tool's reference point switches from world origin to the cursor.
    /// 游标参考系：启用后曲线工具的参考点从世界原点切换为游标位置</summary>
    public bool CursorReferenceMode;

    /// <summary>Tool directory (Assets-relative path) — derived from the script location, follows folder moves.
    /// Resolved live on every access (no cache): access frequency is low (settings load/save only),
    /// and a moved folder is picked up immediately.
    /// 工具目录（Assets 相对路径）— 基于脚本位置动态推导，目录移动后自动跟随。
    /// 每次访问实时解析（不做缓存）：访问频率低（仅设置读写时），目录移动后立即生效</summary>
    public static string ToolDirectory
    {
        get
        {
            // Locate the directory via the script asset (script name is unique in the project).
            // 通过脚本名查找脚本资产定位目录（脚本名在项目内唯一）
            var guids = AssetDatabase.FindAssets("CurveManager t:MonoScript");
            return guids.Length > 0
                ? Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guids[0])).Replace('\\', '/')
                : "Assets/Tools/CurvesTool";
        }
    }

    // ===== Singleton binding / 单例绑定 =====

    static CurveManager()
    {
        // After a scene switch the cached instance is stale; rebind on next access (each scene owns its data).
        // 场景切换后旧缓存失效，下次访问时重绑到新场景（各场景持有自己的曲线数据）
        EditorSceneManager.sceneOpened += (_, _) => _instance = null;
    }

    /// <summary>Curve data carrier of the active scene (finds an existing component, creates a hidden carrier otherwise).
    /// 当前活动场景的曲线数据载体（查找已有组件，不存在则创建隐藏载体）</summary>
    public static CurveManager Instance
    {
        get
        {
            if (_instance == null || _instance.gameObject == null ||
                _instance.gameObject.scene != SceneManager.GetActiveScene())
                BindToActiveScene();
            return _instance;
        }
    }
    private static CurveManager _instance;

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
                var found = root.GetComponentInChildren<CurveManager>(true);
                if (found != null) { _instance = found; break; }
            }
        }

        if (_instance == null)
        {
            // Hidden from the hierarchy (HideInHierarchy) but still serialized with the scene.
            // 隐藏于层级面板（HideInHierarchy），但仍随场景序列化保存
            var go = new GameObject(HostObjectName);
            go.hideFlags = HideFlags.HideInHierarchy;
            _instance = go.AddComponent<CurveManager>();
        }

        _instance.MigrateFromLegacyAsset();
    }

    /// <summary>One-time migration: imports data from the legacy ScriptableObject asset into the scene component, then deletes it.
    /// 一次性迁移：从旧 ScriptableObject 资产导入数据到场景组件，成功后删除旧资产</summary>
    private void MigrateFromLegacyAsset()
    {
        // Skip when the component already has data (scene was saved before) to avoid overwriting.
        // 组件已有数据（场景已保存过）时不迁移，避免覆盖
        if (Curves.Count > 0) return;

        var legacy = AssetDatabase.LoadAssetAtPath<Object>(LegacyAssetPath);
        if (legacy == null) return; // No legacy asset, nothing to migrate. / 旧资产不存在，无需迁移

        try
        {
            // Read legacy fields by name via reflection (Object fields were already deserialized on load).
            // 反射按字段名读取旧数据（UnityEngine.Object 字段已在加载时反序列化）
            var t = legacy.GetType();
            var list = t.GetField("Curves")?.GetValue(legacy) as List<BezierCurve>;
            bool imported = list != null && list.Count > 0;

            if (imported)
            {
                Curves = list;
                CursorPosition = ReadField(t, legacy, "CursorPosition", CursorPosition);
                CursorLocked = ReadField(t, legacy, "CursorLocked", CursorLocked);
                CursorLockX = ReadField(t, legacy, "CursorLockX", CursorLockX);
                CursorLockY = ReadField(t, legacy, "CursorLockY", CursorLockY);
                CursorLockZ = ReadField(t, legacy, "CursorLockZ", CursorLockZ);
                CursorReferenceMode = ReadField(t, legacy, "CursorReferenceMode", CursorReferenceMode);
                MarkDirty();
                AssetDatabase.DeleteAsset(LegacyAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CurveTool] Migrated curve data from {LegacyAssetPath} to the scene ({HostObjectName}); legacy asset deleted. / 已将曲线数据从 {LegacyAssetPath} 迁移到当前场景（{HostObjectName}），旧资产已删除");
            }
            else
            {
                // No readable curve data in the legacy asset: keep the asset, do not delete.
                // 旧资产无可读曲线数据：保留资产，不删除
                Debug.LogWarning($"[CurveTool] No curve data found in legacy asset {LegacyAssetPath}; keeping it. / 旧曲线资产 {LegacyAssetPath} 中未读取到曲线数据，保留旧资产");
            }
        }
        catch (System.Exception ex)
        {
            // Migration failure must not block the tool; keep the legacy asset.
            // 迁移失败不阻塞工具，旧资产保留
            Debug.LogWarning($"[CurveTool] Legacy asset migration failed: {ex.Message}; keeping the asset. / 迁移旧曲线资产失败：{ex.Message}，旧资产保留");
        }
    }

    /// <summary>Reads a scalar field from the legacy asset via reflection (falls back to the default on failure).
    /// 反射读取旧资产的标量字段（失败时返回默认值）</summary>
    private static T ReadField<T>(System.Type type, object target, string fieldName, T fallback)
    {
        var f = type.GetField(fieldName);
        if (f == null) return fallback;
        var v = f.GetValue(target);
        return v is T tv ? tv : fallback;
    }

    // ===== Selection accessors / 选中访问器 =====

    /// <summary>Currently selected curve (null when none selected). / 当前选中的曲线（无选中时返回 null）</summary>
    public BezierCurve SelectedCurve =>
        SelectedCurveIndex >= 0 && SelectedCurveIndex < Curves.Count ? Curves[SelectedCurveIndex] : null;

    /// <summary>Currently selected vertex (null when none selected). / 当前选中的顶点（无选中时返回 null）</summary>
    public CurveVertex SelectedVertex
    {
        get
        {
            var c = SelectedCurve;
            if (c == null || SelectedVertexIndex < 0 || SelectedVertexIndex >= c.Vertices.Count) return null;
            return c.Vertices[SelectedVertexIndex];
        }
    }

    // ===== Data operations / 数据操作 =====

    /// <summary>Adds a new curve with 2D/3D initialization. / 添加新曲线，支持 2D/3D 初始化</summary>
    public BezierCurve AddNewCurve(string name = "NewCurve", int segmentCount = 16, UpAxis upAxis = UpAxis.Y)
    {
        Undo.RecordObject(this, "添加曲线");
        var curve = BezierCurve.CreateDefault(name);
        curve.SegmentCount = Mathf.Max(1, segmentCount);
        curve.UpAxis = upAxis;
        curve.RebuildSegments();
        curve.RecalculateHandles();
        Curves.Add(curve);
        // Full selection (IsSelected = true so the new curve highlights in the Scene view too).
        // 完整选中（IsSelected 置位，新曲线在场景视图中同步高亮）
        Select(Curves.Count - 1);
        MarkDirty();
        return curve;
    }

    /// <summary>Removes a curve by index. / 删除曲线</summary>
    public void RemoveCurve(int index)
    {
        if (index < 0 || index >= Curves.Count) return;
        Undo.RecordObject(this, "删除曲线");
        Curves.RemoveAt(index);
        ClearSelection();
        MarkDirty();
    }

    /// <summary>Inserts a vertex after the given vertex index. / 在指定段后插入顶点</summary>
    public void InsertVertex(int curveIndex, int afterVertexIndex, Vector2 position)
    {
        if (curveIndex < 0 || curveIndex >= Curves.Count) return;
        var curve = Curves[curveIndex];
        Undo.RecordObject(this, "插入顶点");
        var v = new CurveVertex(position);
        if (afterVertexIndex < 0 || afterVertexIndex >= curve.Vertices.Count - 1)
            curve.Vertices.Add(v);
        else
            curve.Vertices.Insert(afterVertexIndex + 1, v);
        curve.RebuildSegments();
        curve.RecalculateHandles();
        Select(curveIndex, curve.Vertices.IndexOf(v), 3);
        MarkDirty();
    }

    /// <summary>Removes the selected vertex (curves keep at least 2 vertices). / 删除选中的顶点</summary>
    public void RemoveSelectedVertex()
    {
        var curve = SelectedCurve;
        if (curve == null || SelectedVertexIndex < 0 || curve.Vertices.Count <= 2) return;
        Undo.RecordObject(this, "删除顶点");
        int deletedIdx = SelectedVertexIndex;
        curve.Vertices.RemoveAt(deletedIdx);
        curve.RebuildSegments();
        curve.RecalculateHandles();
        if (SelectedVertexIndex >= curve.Vertices.Count)
            SelectedVertexIndex = curve.Vertices.Count - 1;

        // Clean the multi-select list: drop the deleted index and shift the remaining ones.
        // 清理多选列表：移除被删索引，修正后续索引偏移
        var newIndices = new List<int>();
        foreach (int vi in SelectedVertexIndices)
        {
            if (vi == deletedIdx) continue;
            newIndices.Add(vi > deletedIdx ? vi - 1 : vi);
        }
        SelectedVertexIndices = newIndices;
        MarkDirty();
    }

    /// <summary>Clears all selection state. / 清除所有选中</summary>
    public void ClearSelection()
    {
        foreach (var c in Curves)
        {
            c.IsSelected = false;
            c.SelectedSegmentIndex = -1;
            foreach (var v in c.Vertices) { v.IsSelected = false; v.SelectedSubElement = 0; }
            foreach (var s in c.Segments) s.IsSelected = false;
        }
        SelectedCurveIndex = -1;
        SelectedVertexIndex = -1;
        SelectedVertexIndices.Clear();
    }

    /// <summary>Selects a curve and vertex. additive=true appends to the multi-select list.
    /// 选中曲线和顶点。additive=true 时追加到多选列表。</summary>
    public void Select(int curveIndex, int vertexIndex = -1, int subElement = 0, bool additive = false)
    {
        if (curveIndex < 0 || curveIndex >= Curves.Count) return;

        if (!additive) ClearSelection();

        SelectedCurveIndex = curveIndex;
        var curve = Curves[curveIndex];
        curve.IsSelected = true;

        if (vertexIndex >= 0 && vertexIndex < curve.Vertices.Count)
        {
            SelectedVertexIndex = vertexIndex;
            var v = curve.Vertices[vertexIndex];
            v.IsSelected = true;
            v.SelectedSubElement = subElement;
            if (additive && !SelectedVertexIndices.Contains(vertexIndex))
                SelectedVertexIndices.Add(vertexIndex);
            else if (!additive)
                SelectedVertexIndices = new List<int> { vertexIndex };
        }
    }

    /// <summary>Selects a segment (Alt+Left-click). / 选中线段（Alt+左键）</summary>
    public void SelectSegment(int curveIndex, int segmentIndex, bool additive = false)
    {
        if (curveIndex < 0 || curveIndex >= Curves.Count) return;
        if (!additive) ClearSelection();
        SelectedCurveIndex = curveIndex;
        var curve = Curves[curveIndex];
        curve.IsSelected = true;
        if (segmentIndex >= 0 && segmentIndex < curve.Segments.Count)
        {
            curve.SelectedSegmentIndex = segmentIndex;
            curve.Segments[segmentIndex].IsSelected = true;
        }
    }

    /// <summary>Exports all curves as JSON text. / 导出为 JSON 文本</summary>
    public string SerializeToJson() =>
        JsonUtility.ToJson(new CurveListWrapper { Curves = Curves }, prettyPrint: true);

    /// <summary>Exports all curves to the given file. / 导出到指定文件</summary>
    public void SaveToFile(string filePath) => File.WriteAllText(filePath, SerializeToJson());

    /// <summary>Marks data as changed: component dirty + scene dirty (required for Ctrl+S persistence).
    /// 标记数据变更：组件脏 + 场景脏（保证 Ctrl+S 时落盘）</summary>
    public void MarkDirty()
    {
        EditorUtility.SetDirty(this);
        var scene = gameObject != null ? gameObject.scene : default;
        if (scene.IsValid() && scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    /// <summary>Serialization wrapper for the curve list. / 曲线列表的序列化包装</summary>
    [System.Serializable]
    private class CurveListWrapper { public List<BezierCurve> Curves; }
}