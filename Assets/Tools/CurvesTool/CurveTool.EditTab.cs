using UnityEditor;
using UnityEngine;

/// <summary>
/// 曲线工具 — 编辑 Tab（曲线列表、段属性、曲柄控制、生成）
/// </summary>
public partial class CurveTool
{
    // ============================================================
    //  编辑 Tab
    // ============================================================
    private void TabEdit()
    {
        DrawModeBar();
        GUILayout.Space(4);

        // ~ 曲线工具
        _foldoutCurveTools = EditorGUILayout.Foldout(_foldoutCurveTools, L10n.T("curve_tools"), true);
        if (_foldoutCurveTools) DrawCurveToolsFoldout();

        // ~ 曲线列表
        _foldoutCurveList = EditorGUILayout.Foldout(_foldoutCurveList, L10n.T("curve_list"), true);
        if (_foldoutCurveList) DrawCurveList();

        // ~ 顶点与控制柄属性
        _foldoutVertexProps = EditorGUILayout.Foldout(_foldoutVertexProps, L10n.T("vertex_handle_props"), true);
        if (_foldoutVertexProps) DrawVertexAndHandleProps();

        // ~ 选中段属性
        _foldoutSegmentProps = EditorGUILayout.Foldout(_foldoutSegmentProps, L10n.T("seg_props"), true);
        if (_foldoutSegmentProps) DrawSegmentProps();

        // ~ 物体生成
        _foldoutGenObject = EditorGUILayout.Foldout(_foldoutGenObject, L10n.T("gen_object"), true);
        if (_foldoutGenObject) DrawGenerateButton();
    }

    /// <summary>曲线工具折叠区间：翻转/镜像/游标参考系/游标属性/锁定/重置</summary>
    private void DrawCurveToolsFoldout()
    {
        // ---------- 翻转 ----------
        var sel = _manager.SelectedCurve;
        EditorGUI.BeginDisabledGroup(sel == null || sel.IsLocked);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("flip"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));

        bool canFlipX = sel == null || sel.UpAxis != UpAxis.X || (sel != null && sel.Is3D);
        bool canFlipY = sel == null || sel.UpAxis != UpAxis.Y || (sel != null && sel.Is3D);
        bool canFlipZ = sel == null || sel.UpAxis != UpAxis.Z || (sel != null && sel.Is3D);

        EditorGUI.BeginDisabledGroup(!canFlipX);
        bool flipX = GUILayout.Button("X", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!canFlipY);
        bool flipY = GUILayout.Button("Y", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!canFlipZ);
        bool flipZ = GUILayout.Button("Z", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        if ((flipX || flipY || flipZ) && sel != null && !sel.IsLocked)
        {
            Undo.RecordObject(_manager, "翻转顶点");
            Vector3 center = _manager.CursorReferenceMode ? _manager.CursorPosition : Vector3.zero;
            foreach (var v in sel.Vertices)
            {
                if (sel.Is3D)
                {
                    Vector3 vw = v.PositionV3;
                    Vector3 lhw = v.PositionV3 + v.LeftHandleOffsetV3;
                    Vector3 rhw = v.PositionV3 + v.RightHandleOffsetV3;
                    if (flipX) { vw.x = 2f * center.x - vw.x; lhw.x = 2f * center.x - lhw.x; rhw.x = 2f * center.x - rhw.x; }
                    if (flipY) { vw.y = 2f * center.y - vw.y; lhw.y = 2f * center.y - lhw.y; rhw.y = 2f * center.y - rhw.y; }
                    if (flipZ) { vw.z = 2f * center.z - vw.z; lhw.z = 2f * center.z - lhw.z; rhw.z = 2f * center.z - rhw.z; }
                    v.Position = new Vector2(vw.x, vw.z);
                    v.Height = vw.y;
                    v.LeftHandle = new Vector2(lhw.x - vw.x, lhw.z - vw.z);
                    v.LeftHandleHeight = lhw.y - vw.y;
                    v.RightHandle = new Vector2(rhw.x - vw.x, rhw.z - vw.z);
                    v.RightHandleHeight = rhw.y - vw.y;
                }
                else
                {
                    Vector3 vw = sel.MapToWorld(v.Position);
                    Vector3 lhw = sel.MapToWorld(v.LeftHandlePosition);
                    Vector3 rhw = sel.MapToWorld(v.RightHandlePosition);
                    if (flipX) { vw.x = 2f * center.x - vw.x; lhw.x = 2f * center.x - lhw.x; rhw.x = 2f * center.x - rhw.x; }
                    if (flipY) { vw.y = 2f * center.y - vw.y; lhw.y = 2f * center.y - lhw.y; rhw.y = 2f * center.y - rhw.y; }
                    if (flipZ) { vw.z = 2f * center.z - vw.z; lhw.z = 2f * center.z - lhw.z; rhw.z = 2f * center.z - rhw.z; }
                    v.Position = sel.MapFromWorld(vw);
                    Vector2 nLH = sel.MapFromWorld(lhw);
                    Vector2 nRH = sel.MapFromWorld(rhw);
                    v.LeftHandle = nLH - v.Position;
                    v.RightHandle = nRH - v.Position;
                }
            }
            sel.RecalculateHandles();
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        EditorGUI.EndDisabledGroup();

        // ---------- 镜像 ----------
        EditorGUI.BeginDisabledGroup(sel == null || sel.IsLocked);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("mirror"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));

        bool canMirX = sel == null || sel.UpAxis != UpAxis.X || (sel != null && sel.Is3D);
        bool canMirY = sel == null || sel.UpAxis != UpAxis.Y || (sel != null && sel.Is3D);
        bool canMirZ = sel == null || sel.UpAxis != UpAxis.Z || (sel != null && sel.Is3D);

        EditorGUI.BeginDisabledGroup(!canMirX);
        bool mirX = GUILayout.Button("X", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!canMirY);
        bool mirY = GUILayout.Button("Y", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!canMirZ);
        bool mirZ = GUILayout.Button("Z", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if ((mirX || mirY || mirZ) && sel != null && !sel.IsLocked)
        {
            DuplicateCurve(sel);
            var dup = _manager.SelectedCurve;
            if (dup != null)
            {
                Undo.RecordObject(_manager, "镜像曲线");
                Vector3 center = _manager.CursorReferenceMode ? _manager.CursorPosition : Vector3.zero;
                foreach (var v in dup.Vertices)
                {
                    if (dup.Is3D) { /* same flip logic */ 
                        Vector3 vw = v.PositionV3;
                        Vector3 lhw = v.PositionV3 + v.LeftHandleOffsetV3;
                        Vector3 rhw = v.PositionV3 + v.RightHandleOffsetV3;
                        if (mirX) { vw.x = 2f * center.x - vw.x; lhw.x = 2f * center.x - lhw.x; rhw.x = 2f * center.x - rhw.x; }
                        if (mirY) { vw.y = 2f * center.y - vw.y; lhw.y = 2f * center.y - lhw.y; rhw.y = 2f * center.y - rhw.y; }
                        if (mirZ) { vw.z = 2f * center.z - vw.z; lhw.z = 2f * center.z - lhw.z; rhw.z = 2f * center.z - rhw.z; }
                        v.Position = new Vector2(vw.x, vw.z);
                        v.Height = vw.y;
                        v.LeftHandle = new Vector2(lhw.x - vw.x, lhw.z - vw.z);
                        v.LeftHandleHeight = lhw.y - vw.y;
                        v.RightHandle = new Vector2(rhw.x - vw.x, rhw.z - vw.z);
                        v.RightHandleHeight = rhw.y - vw.y;
                    }
                    else
                    {
                        Vector3 vw = dup.MapToWorld(v.Position);
                        Vector3 lhw = dup.MapToWorld(v.LeftHandlePosition);
                        Vector3 rhw = dup.MapToWorld(v.RightHandlePosition);
                        if (mirX) { vw.x = 2f * center.x - vw.x; lhw.x = 2f * center.x - lhw.x; rhw.x = 2f * center.x - rhw.x; }
                        if (mirY) { vw.y = 2f * center.y - vw.y; lhw.y = 2f * center.y - lhw.y; rhw.y = 2f * center.y - rhw.y; }
                        if (mirZ) { vw.z = 2f * center.z - vw.z; lhw.z = 2f * center.z - lhw.z; rhw.z = 2f * center.z - rhw.z; }
                        v.Position = dup.MapFromWorld(vw);
                        Vector2 nLH = dup.MapFromWorld(lhw);
                        Vector2 nRH = dup.MapFromWorld(rhw);
                        v.LeftHandle = nLH - v.Position;
                        v.RightHandle = nRH - v.Position;
                    }
                }
                dup.RecalculateHandles();
                _manager.MarkDirty();
                SceneView.RepaintAll();
            }
        }

        GUILayout.Space(4);

        // ---------- 游标参考系 ----------
        EditorGUI.BeginDisabledGroup(!_editMode);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("cursor_ref_frame"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        bool refMode = _manager.CursorReferenceMode;
        EditorGUI.BeginChangeCheck();
        refMode = EditorGUILayout.Toggle(refMode);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_manager, "切换游标参考系");
            _manager.CursorReferenceMode = refMode;
            _manager.MarkDirty();
        }

        // ---------- 游标属性 ----------
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("cursor_props"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        DrawVertexAxisLock("X", ref _manager.CursorLockX, ref _manager.CursorPosition.x);
        GUILayout.Space(4);
        DrawVertexAxisLock("Y", ref _manager.CursorLockY, ref _manager.CursorPosition.y);
        GUILayout.Space(4);
        DrawVertexAxisLock("Z", ref _manager.CursorLockZ, ref _manager.CursorPosition.z);
        EditorGUILayout.EndHorizontal();
        if (GUI.changed)
        {
            Undo.RecordObject(_manager, "移动游标");
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        // ---------- 锁定游标 ----------
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("lock_cursor"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        bool locked = _manager.CursorLocked;
        EditorGUI.BeginChangeCheck();
        locked = EditorGUILayout.Toggle(locked);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_manager, "锁定游标");
            _manager.CursorLocked = locked;
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        // ---------- 重置游标位置 ----------
        if (GUILayout.Button(L10n.T("reset_cursor_pos"), GUILayout.Height(20)))
        {
            Undo.RecordObject(_manager, "重置游标");
            Vector3 cp = _manager.CursorPosition;
            if (!_manager.CursorLockX) cp.x = 0f;
            if (!_manager.CursorLockY) cp.y = 0f;
            if (!_manager.CursorLockZ) cp.z = 0f;
            _manager.CursorPosition = cp;
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }
        EditorGUI.EndDisabledGroup();
        GUILayout.Space(4);
    }
    private void DrawVertexAndHandleProps()
    {
        var vertex = _manager.SelectedVertex;
        EditorGUI.BeginDisabledGroup(!_editMode || vertex == null);

        // 回读缓冲区
        if (vertex != null)
        {
            _vertexPosition = vertex.Position;
            _vertexHeight = vertex.Height;
            _vertexLockX = vertex.LockX;
            _vertexLockY = vertex.LockY;
            _vertexLockZ = vertex.LockZ;
            // 根据选中的子元素决定显示的 HandleType
            _vertexHandleType = vertex.SelectedSubElement switch
            {
                1 => vertex.HandleTypeA,
                2 => vertex.HandleTypeB,
                5 => vertex.HandleTypeA,
                6 => vertex.HandleTypeB,
                _ => vertex.HandleTypeA,
            };
        }
        else
        {
            // 无选中时缓冲区归零，避免显示过期值
            _vertexPosition = Vector2.zero;
            _vertexHeight = 0f;
            _vertexLockX = _vertexLockY = _vertexLockZ = false;
            _vertexHandleType = HandleType.Auto;
        }

        // 顶点位置 + 轴锁
        bool is3dCurve = vertex != null && _manager.SelectedCurve != null && _manager.SelectedCurve.Is3D;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("vertex_position"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        DrawVertexAxisLock("X", ref _vertexLockX, ref _vertexPosition.x);
        GUILayout.Space(4);
        // Y 轴（高度/Height）：2D 灰显，3D 解锁
        if (is3dCurve)
            DrawVertexAxisLock("Y", ref _vertexLockZ, ref _vertexHeight);
        else
        {
            GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            GUILayout.Label("L", GUILayout.Width(20), GUILayout.Height(18));
            GUI.backgroundColor = Color.white;
            GUILayout.Label("Y", GUILayout.Width(12));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("2D", GUILayout.MinWidth(30));
            EditorGUI.EndDisabledGroup();
        }
        GUILayout.Space(4);
        // Z 轴（平面）
        DrawVertexAxisLock("Z", ref _vertexLockY, ref _vertexPosition.y);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck() && vertex != null)
        {
            Undo.RecordObject(_manager, "修改顶点位置");
            vertex.Position = _vertexPosition;
            if (is3dCurve) vertex.Height = _vertexHeight;
            vertex.LockX = _vertexLockX;
            vertex.LockY = _vertexLockY;
            vertex.LockZ = _vertexLockZ;
            _manager.SelectedCurve?.RecalculateHandles();
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        // 控制柄类型
        var htNames = _htNames ??= new[] { L10n.T("ht_auto"), L10n.T("ht_aligned"), L10n.T("ht_aligned_length"), L10n.T("ht_vector"), L10n.T("ht_free") };
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("handle_type"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        EditorGUI.BeginChangeCheck();
        int htIdx = EditorGUILayout.Popup((int)_vertexHandleType, htNames);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck() && vertex != null)
        {
            Undo.RecordObject(_manager, "修改控制柄类型");
            var newType = (HandleType)htIdx;
            if (vertex.SelectedSubElement == 1 || vertex.SelectedSubElement == 5)
                vertex.HandleTypeA = newType;
            else if (vertex.SelectedSubElement == 2 || vertex.SelectedSubElement == 6)
                vertex.HandleTypeB = newType;
            else
            {
                // 选中顶点本身或顶点Y箭头时，同时设置两侧
                vertex.HandleTypeA = newType;
                vertex.HandleTypeB = newType;
            }
            // 镜像/对齐：立即从对侧同步值
            if (newType == HandleType.AlignedLength || newType == HandleType.Aligned)
            {
                bool isLeft = vertex.SelectedSubElement == 1 || vertex.SelectedSubElement == 5;
                bool isRight = vertex.SelectedSubElement == 2 || vertex.SelectedSubElement == 6;
                if (newType == HandleType.AlignedLength)
                {
                    if (!isRight) vertex.LeftHandle = -vertex.RightHandle;
                    if (!isLeft) vertex.RightHandle = -vertex.LeftHandle;
                }
                else // Aligned
                {
                    if (!isRight && vertex.RightHandle.magnitude > 0.0001f)
                        vertex.LeftHandle = -vertex.RightHandle.normalized * vertex.LeftHandle.magnitude;
                    if (!isLeft && vertex.LeftHandle.magnitude > 0.0001f)
                        vertex.RightHandle = -vertex.LeftHandle.normalized * vertex.RightHandle.magnitude;
                }
            }
            _manager.SelectedCurve?.RecalculateHandles();
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        EditorGUI.EndDisabledGroup();
        GUILayout.Space(2);

        // 控制柄位置（左右柄 XYZ 带锁）
        DrawHandlePosition(vertex, is3dCurve);

        GUILayout.Space(4);
    }

    /// <summary>控制柄位置：左右柄 XYZ 分轴锁编辑</summary>
    private void DrawHandlePosition(CurveVertex vertex, bool is3d)
    {
        EditorGUI.BeginDisabledGroup(vertex == null);

        if (vertex != null)
        {
            // 回读控制柄缓冲区
            _leftHandlePos = vertex.LeftHandle;
            _leftHandleHeight = vertex.LeftHandleHeight;
            _leftHandleLockX = vertex.LeftHandleLockX;
            _leftHandleLockY = vertex.LeftHandleLockY;
            _leftHandleLockZ = vertex.LeftHandleLockZ;
            _rightHandlePos = vertex.RightHandle;
            _rightHandleHeight = vertex.RightHandleHeight;
            _rightHandleLockX = vertex.RightHandleLockX;
            _rightHandleLockY = vertex.RightHandleLockY;
            _rightHandleLockZ = vertex.RightHandleLockZ;
        }

        // 左柄
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("left_handle"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        DrawVertexAxisLock("X", ref _leftHandleLockX, ref _leftHandlePos.x);
        GUILayout.Space(4);
        // Y 轴（高度偏移）
        if (is3d)
            DrawVertexAxisLock("Y", ref _leftHandleLockZ, ref _leftHandleHeight);
        else
        {
            GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            GUILayout.Label("L", GUILayout.Width(20), GUILayout.Height(18));
            GUI.backgroundColor = Color.white;
            GUILayout.Label("Y", GUILayout.Width(12));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("2D", GUILayout.MinWidth(30));
            EditorGUI.EndDisabledGroup();
        }
        GUILayout.Space(4);
        // Z 轴（平面偏移）
        DrawVertexAxisLock("Z", ref _leftHandleLockY, ref _leftHandlePos.y);
        if (EditorGUI.EndChangeCheck() && vertex != null)
        {
            Undo.RecordObject(_manager, "修改左柄位置");
            if (vertex.HandleTypeA == HandleType.AlignedLength || vertex.HandleTypeA == HandleType.Aligned)
                vertex.HandleTypeA = HandleType.Free;
            vertex.LeftHandle = _leftHandlePos;
            vertex.LeftHandleHeight = _leftHandleHeight;
            vertex.LeftHandleLockX = _leftHandleLockX;
            vertex.LeftHandleLockY = _leftHandleLockY;
            vertex.LeftHandleLockZ = _leftHandleLockZ;
            vertex.ApplyHandleType(1, is3d);
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        // 右柄
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("right_handle"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        DrawVertexAxisLock("X", ref _rightHandleLockX, ref _rightHandlePos.x);
        GUILayout.Space(4);
        // Y 轴（高度偏移）
        if (is3d)
            DrawVertexAxisLock("Y", ref _rightHandleLockZ, ref _rightHandleHeight);
        else
        {
            GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            GUILayout.Label("L", GUILayout.Width(20), GUILayout.Height(18));
            GUI.backgroundColor = Color.white;
            GUILayout.Label("Y", GUILayout.Width(12));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("2D", GUILayout.MinWidth(30));
            EditorGUI.EndDisabledGroup();
        }
        GUILayout.Space(4);
        // Z 轴（平面偏移）
        DrawVertexAxisLock("Z", ref _rightHandleLockY, ref _rightHandlePos.y);
        if (EditorGUI.EndChangeCheck() && vertex != null)
        {
            Undo.RecordObject(_manager, "修改右柄位置");
            if (vertex.HandleTypeB == HandleType.AlignedLength || vertex.HandleTypeB == HandleType.Aligned)
                vertex.HandleTypeB = HandleType.Free;
            vertex.RightHandle = _rightHandlePos;
            vertex.RightHandleHeight = _rightHandleHeight;
            vertex.RightHandleLockX = _rightHandleLockX;
            vertex.RightHandleLockY = _rightHandleLockY;
            vertex.RightHandleLockZ = _rightHandleLockZ;
            vertex.ApplyHandleType(2, is3d);
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
        GUILayout.Space(2);
    }

    /// <summary>绘制分轴锁按钮 + 标签 + 浮点输入框（顶点/控制柄/游标共用）</summary>
    private void DrawVertexAxisLock(string label, ref bool locked, ref float value)
    {
        GUI.backgroundColor = locked ? new Color(0.9f, 0.6f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
        if (GUILayout.Button("L", GUILayout.Width(20), GUILayout.Height(18)))
        {
            Undo.RecordObject(_manager, "分轴锁");
            locked = !locked;
            _manager.MarkDirty();
            GUI.changed = true;
        }
        GUI.backgroundColor = Color.white;
        GUILayout.Label(label, GUILayout.Width(12));
        EditorGUI.BeginDisabledGroup(locked);
        value = EditorGUILayout.FloatField(value, GUILayout.MinWidth(30));
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>曲线列表</summary>
    private void DrawCurveList()
    {

        // 新建行：名称 + 线段数 + 2D/3D 创建按钮
        EditorGUILayout.BeginHorizontal();
        _newCurveName = EditorGUILayout.TextField(_newCurveName, GUILayout.MinWidth(60));
        _defaultSegmentCount = Mathf.Clamp(EditorGUILayout.IntField(_defaultSegmentCount, GUILayout.Width(36)), 1, 256);

        // 2D 按钮：直接创建 UpAxis.Y 曲线
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("2D", GUILayout.Width(45)))
            CreateNewCurve(UpAxis.Y);

        // 3D 按钮：创建 3D 曲线
        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);
        if (GUILayout.Button("3D", GUILayout.Width(45)))
            CreateNew3DCurve();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("――――――――――――――――――――――", EditorStyles.centeredGreyMiniLabel);

        _scrollCurves = EditorGUILayout.BeginScrollView(_scrollCurves, GUILayout.Height(210));
        GUI.backgroundColor = Color.white;
        for (int i = 0; i < _manager.Curves.Count; i++)
        {
            var curve = _manager.Curves[i];
            bool isSel = _manager.SelectedCurveIndex == i;
            if (isSel) GUI.backgroundColor = new Color(0.3f, 0.6f, 1f, 0.3f);

            EditorGUILayout.BeginHorizontal();

            // 选择按钮：○ 当前选中，× 未选中，点击即选中该行
            string selLabel = isSel ? "○" : "×";
            GUI.backgroundColor = isSel ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
            if (GUILayout.Button(selLabel, GUILayout.Width(24)))
            {
                _manager.Select(i);
                SceneView.RepaintAll();
            }

            // 可见 D
            GUI.backgroundColor = curve.IsVisible ? Color.white : new Color(0.4f, 0.4f, 0.4f);
            if (GUILayout.Button("D", GUILayout.Width(22)))
            { Undo.RecordObject(_manager, "隐藏"); curve.IsVisible = !curve.IsVisible; _manager.MarkDirty(); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? new Color(0.3f, 0.6f, 1f, 0.3f) : Color.white;

            // 锁定 L
            GUI.backgroundColor = curve.IsLocked ? new Color(0.9f, 0.6f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
            if (GUILayout.Button("L", GUILayout.Width(22)))
            { Undo.RecordObject(_manager, "锁定"); curve.IsLocked = !curve.IsLocked; _manager.MarkDirty(); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? new Color(0.3f, 0.6f, 1f, 0.3f) : Color.white;

            // 名称
            string nn = EditorGUILayout.TextField(curve.Name, GUILayout.MinWidth(44));
            if (nn != curve.Name) { Undo.RecordObject(_manager, "重命名"); curve.Name = nn; _manager.MarkDirty(); }

            // 轴向（3D 曲线灰显为 "3D"）
            EditorGUI.BeginDisabledGroup(!_editMode || curve.Is3D);
            if (curve.Is3D)
            {
                GUILayout.Label("3D", GUILayout.Width(42));
            }
            else
            {
                var np = (UpAxis)EditorGUILayout.EnumPopup(curve.UpAxis, GUILayout.Width(42));
                if (np != curve.UpAxis) { Undo.RecordObject(_manager, "轴向"); curve.UpAxis = np; _manager.MarkDirty(); SceneView.RepaintAll(); }
            }
            EditorGUI.EndDisabledGroup();
            // 线段数（限制 1..256，避免生成海量 SegmentInfo 或越界）
            int nsc = Mathf.Clamp(EditorGUILayout.IntField(curve.SegmentCount, GUILayout.Width(36)), 1, 256);
            if (nsc != curve.SegmentCount)
            { Undo.RecordObject(_manager, "线段数"); curve.SegmentCount = nsc; curve.RebuildSegments(); _manager.MarkDirty(); SceneView.RepaintAll(); }

            // 闭环 R
            GUI.backgroundColor = curve.IsLoop ? new Color(0.3f, 0.6f, 1f) : new Color(0.5f, 0.5f, 0.5f);
            if (GUILayout.Button("R", GUILayout.Width(22)))
            { Undo.RecordObject(_manager, "闭环"); curve.IsLoop = !curve.IsLoop; curve.RebuildSegments(); curve.RecalculateHandles(); _manager.MarkDirty(); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? new Color(0.3f, 0.6f, 1f, 0.3f) : Color.white;

            // 复制 C
            GUI.backgroundColor = new Color(0.5f, 0.75f, 1f);
            if (GUILayout.Button("C", GUILayout.Width(22)))
            { DuplicateCurve(curve); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? new Color(0.3f, 0.6f, 1f, 0.3f) : Color.white;

            EditorGUI.EndDisabledGroup();

            // 删除
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            { _manager.RemoveCurve(i); SceneView.RepaintAll(); GUIUtility.ExitGUI(); }
            GUI.backgroundColor = isSel ? new Color(0.3f, 0.6f, 1f, 0.3f) : Color.white;

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndScrollView();

        if (_manager.Curves.Count > 0)
        {
            GUI.backgroundColor = new Color(0.85f, 0.3f, 0.3f);
            if (GUILayout.Button(L10n.T("del_all_curves"), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog(L10n.T("confirm"), L10n.T("del_confirm", _manager.Curves.Count), L10n.T("ok"), L10n.T("cancel")))
                { Undo.RecordObject(_manager, "删除全部"); _manager.Curves.Clear(); _manager.ClearSelection(); _manager.MarkDirty(); SceneView.RepaintAll(); }
            }
            GUI.backgroundColor = Color.white;
        }
    }

    /// <summary>选中线段属性面板（始终显示控件，无选中时灰显）</summary>
    private void DrawSegmentProps()
    {
        var curve = _manager.SelectedCurve;
        bool hasSelection = false;
        int firstIdx = -1;
        if (curve != null)
        {
            firstIdx = curve.SelectedSegmentIndex;
            if (firstIdx < 0)
                for (int i = 0; i < curve.Segments.Count; i++)
                    if (curve.Segments[i].IsSelected) { firstIdx = i; break; }
            hasSelection = firstIdx >= 0 && firstIdx < curve.Segments.Count;
        }

        // 始终回读缓冲（无选中时用默认值/缓冲值占位）
        if (hasSelection)
        {
            var seg = curve.Segments[firstIdx];
            _segPrimitiveType = seg.PrimitiveType;
            _segBaseScale = seg.BaseScale;
            _segPositionOffset = seg.PositionOffset;
            _segFitSegmentLength = seg.FitSegmentLength;
            _segFitAxis = seg.FitAxis;
            _segRelativeScale = seg.RelativeScale;
            _segRotationOffset = seg.RotationOffset;
            _segPositionOffset3D = seg.PositionOffset3D;
        }

        EditorGUI.BeginDisabledGroup(!hasSelection || !_editMode);

        // 字段顺序：基础物体 → 基础缩放 → 中心点偏移 → 缩放轴|适应段长 → 相对缩放 → 旋转偏移 → 位置偏移
        EditorGUI.BeginChangeCheck();
        string[] primNames = _primNames ??= new[] { L10n.T("primitive_sphere"), L10n.T("primitive_capsule"), L10n.T("primitive_cylinder"), L10n.T("primitive_cube"), L10n.T("primitive_plane"), L10n.T("primitive_quad") };
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("base_primitive"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        _segPrimitiveType = (PrimitiveType)EditorGUILayout.Popup((int)_segPrimitiveType, primNames);
        EditorGUILayout.EndHorizontal();

        // 基础缩放 X/Y/Z 分轴输入
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("abs_scale"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GUILayout.Label("X", GUILayout.Width(12)); _segBaseScale.x = EditorGUILayout.FloatField(_segBaseScale.x, GUILayout.MinWidth(40));
        GUILayout.Label("Y", GUILayout.Width(12)); _segBaseScale.y = EditorGUILayout.FloatField(_segBaseScale.y, GUILayout.MinWidth(40));
        GUILayout.Label("Z", GUILayout.Width(12)); _segBaseScale.z = EditorGUILayout.FloatField(_segBaseScale.z, GUILayout.MinWidth(40));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("center_offset"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        _segPositionOffset = EditorGUILayout.FloatField(_segPositionOffset);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("fit_length"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        _segFitSegmentLength = EditorGUILayout.Toggle(_segFitSegmentLength, GUILayout.Width(16));
        _segFitAxis = EditorGUILayout.Popup(_segFitAxis, AxisNames);
        EditorGUILayout.EndHorizontal();

        // 相对缩放 X/Y/Z 分轴输入
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("rel_scale"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GUILayout.Label("X", GUILayout.Width(12)); _segRelativeScale.x = EditorGUILayout.FloatField(_segRelativeScale.x, GUILayout.MinWidth(40));
        GUILayout.Label("Y", GUILayout.Width(12)); _segRelativeScale.y = EditorGUILayout.FloatField(_segRelativeScale.y, GUILayout.MinWidth(40));
        GUILayout.Label("Z", GUILayout.Width(12)); _segRelativeScale.z = EditorGUILayout.FloatField(_segRelativeScale.z, GUILayout.MinWidth(40));
        EditorGUILayout.EndHorizontal();

        // 旋转偏移 X/Y/Z 分轴输入
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("rot_offset"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GUILayout.Label("X", GUILayout.Width(12)); _segRotationOffset.x = EditorGUILayout.FloatField(_segRotationOffset.x, GUILayout.MinWidth(40));
        GUILayout.Label("Y", GUILayout.Width(12)); _segRotationOffset.y = EditorGUILayout.FloatField(_segRotationOffset.y, GUILayout.MinWidth(40));
        GUILayout.Label("Z", GUILayout.Width(12)); _segRotationOffset.z = EditorGUILayout.FloatField(_segRotationOffset.z, GUILayout.MinWidth(40));
        EditorGUILayout.EndHorizontal();

        // 位置偏移 X/Y/Z 分轴输入
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("pos_offset"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GUILayout.Label("X", GUILayout.Width(12)); _segPositionOffset3D.x = EditorGUILayout.FloatField(_segPositionOffset3D.x, GUILayout.MinWidth(40));
        GUILayout.Label("Y", GUILayout.Width(12)); _segPositionOffset3D.y = EditorGUILayout.FloatField(_segPositionOffset3D.y, GUILayout.MinWidth(40));
        GUILayout.Label("Z", GUILayout.Width(12)); _segPositionOffset3D.z = EditorGUILayout.FloatField(_segPositionOffset3D.z, GUILayout.MinWidth(40));
        EditorGUILayout.EndHorizontal();

        if (hasSelection && EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_manager, "修改段属性");
            for (int i = 0; i < curve.Segments.Count; i++)
            {
                if (!curve.Segments[i].IsSelected) continue;
                curve.Segments[i].PrimitiveType = _segPrimitiveType;
                curve.Segments[i].BaseScale = _segBaseScale;
                curve.Segments[i].PositionOffset = _segPositionOffset;
                curve.Segments[i].FitSegmentLength = _segFitSegmentLength;
                curve.Segments[i].FitAxis = _segFitAxis;
                curve.Segments[i].RelativeScale = _segRelativeScale;
                curve.Segments[i].RotationOffset = _segRotationOffset;
                curve.Segments[i].PositionOffset3D = _segPositionOffset3D;
            }
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>生成按钮</summary>
    private void DrawGenerateButton()
    {

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("gen_color"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GenerationColor = EditorGUILayout.ColorField(GenerationColor);
        EditorGUILayout.EndHorizontal();
        if (GUI.changed) SceneView.RepaintAll();

        EditorGUILayout.BeginHorizontal();
        var genCurve = _manager.SelectedCurve;
        bool canGen = genCurve != null && genCurve.IsEnabled;
        EditorGUI.BeginDisabledGroup(!canGen);
        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button(L10n.T("generate"), GUILayout.Height(34)))
            CurveObjectBuilder.Build(_manager.SelectedCurve);
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        // 预览
        GUI.backgroundColor = _previewMode ? new Color(0.4f, 0.7f, 1f) : Color.white;
        bool newPrev = GUILayout.Toggle(_previewMode, L10n.T("preview"), "Button", GUILayout.Height(34), GUILayout.Width(56));
        GUI.backgroundColor = Color.white;
        if (newPrev != _previewMode) { _previewMode = newPrev; SceneView.RepaintAll(); }
        EditorGUILayout.EndHorizontal();
        if (!canGen && _manager.Curves.Count > 0)
            EditorGUILayout.HelpBox(L10n.T("sel_enabled_curve"), MessageType.Info);
    }
}




