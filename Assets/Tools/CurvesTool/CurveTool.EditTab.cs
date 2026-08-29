using UnityEditor;
using UnityEngine;

/// <summary>
/// Curve Tool — Edit tab (curve list, segment properties, handle control, generation).
/// 曲线工具 — 编辑 Tab（曲线列表、段属性、曲柄控制、生成）
/// </summary>
public partial class CurveTool
{
    // ============================================================
    //  Edit Tab / 编辑 Tab
    // ============================================================
    private void TabEdit()
    {
        DrawModeBar();
        GUILayout.Space(4);

        // Overall edit-tab scrolling: lets the user scroll when all foldouts are expanded beyond the window.
        // 编辑页整体滚动：折叠区全部展开时内容超出窗口可滚动查看
        _scrollEdit = EditorGUILayout.BeginScrollView(_scrollEdit);

        // ~ Curve list (create/select, first) / 曲线列表（创建/选择，最先）
        _foldoutCurveList = EditorGUILayout.Foldout(_foldoutCurveList, L10n.T("curve_list"), true);
        if (_foldoutCurveList) DrawCurveList();

        // ~ Transform (flip/mirror on the selected curve) / 变换（翻转/镜像，作用于选中曲线）
        _foldoutCurveTools = EditorGUILayout.Foldout(_foldoutCurveTools, L10n.T("transform"), true);
        if (_foldoutCurveTools) DrawTransformFoldout();

        // ~ Cursor (global reference frame) / 游标（全局参考系）
        _foldoutCursor = EditorGUILayout.Foldout(_foldoutCursor, L10n.T("cursor_section"), true);
        if (_foldoutCursor) DrawCursorFoldout();

        // ~ Selected vertex & handle properties / 选中顶点与控制柄属性
        _foldoutVertexProps = EditorGUILayout.Foldout(_foldoutVertexProps, L10n.T("vertex_handle_props"), true);
        if (_foldoutVertexProps) DrawVertexAndHandleProps();

        // ~ Selected segment properties / 选中段属性
        _foldoutSegmentProps = EditorGUILayout.Foldout(_foldoutSegmentProps, L10n.T("seg_props"), true);
        if (_foldoutSegmentProps) DrawSegmentProps();

        // ~ Generate objects / 物体生成
        _foldoutGenObject = EditorGUILayout.Foldout(_foldoutGenObject, L10n.T("gen_object"), true);
        if (_foldoutGenObject) DrawGenerateButton();

        EditorGUILayout.EndScrollView();
    }

    /// <summary>Transform foldout: flip/mirror about the origin or cursor, applied to the selected curve.
    /// 变换折叠区：翻转/镜像（以原点或游标为中心，作用于选中曲线）</summary>
    private void DrawTransformFoldout()
    {
        var sel = _manager.SelectedCurve;
        bool canEdit = sel != null && !sel.IsLocked;
        Vector3 center = _manager.CursorReferenceMode ? _manager.CursorPosition : Vector3.zero;

        // ---------- Flip / 翻转 ----------
        EditorGUI.BeginDisabledGroup(!canEdit);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("flip"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        bool flipX = DrawAxisButtons(sel, out bool flipY, out bool flipZ);
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if ((flipX || flipY || flipZ) && canEdit)
        {
            Undo.RecordObject(_manager, "翻转顶点");
            ApplyAxisMirror(sel, flipX, flipY, flipZ, center);
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        GUILayout.Space(4);

        // ---------- Mirror (duplicate the selected curve, then flip it along the axis) / 镜像（复制选中曲线后沿轴翻转） ----------
        EditorGUI.BeginDisabledGroup(!canEdit);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("mirror"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        bool mirX = DrawAxisButtons(sel, out bool mirY, out bool mirZ);
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if ((mirX || mirY || mirZ) && canEdit)
        {
            DuplicateCurve(sel);
            var dup = _manager.SelectedCurve;
            if (dup != null)
            {
                Undo.RecordObject(_manager, "镜像曲线");
                ApplyAxisMirror(dup, mirX, mirY, mirZ, center);
                _manager.MarkDirty();
                SceneView.RepaintAll();
            }
        }
        GUILayout.Space(4);
    }

    /// <summary>Draws the X/Y/Z axis buttons (disables axes not flippable on the curve's editing plane); returns the X-axis click result.
    /// 绘制 X/Y/Z 三个轴向按钮（按曲线编辑平面禁用不可翻转的轴），返回 X 轴点击结果</summary>
    private bool DrawAxisButtons(BezierCurve sel, out bool clickedY, out bool clickedZ)
    {
        bool canX = sel == null || sel.Plane != CurvePlane.YZ || sel.Is3D;
        bool canY = sel == null || sel.Plane != CurvePlane.XZ || sel.Is3D;
        bool canZ = sel == null || sel.Plane != CurvePlane.XY || sel.Is3D;

        EditorGUI.BeginDisabledGroup(!canX);
        bool x = GUILayout.Button("X", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!canY);
        clickedY = GUILayout.Button("Y", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!canZ);
        clickedZ = GUILayout.Button("Z", GUILayout.Height(22));
        EditorGUI.EndDisabledGroup();
        return x;
    }

    /// <summary>Mirrors vertices and handles along the given axes about center (unified 2D/3D), then recalculates handles.
    /// 以 center 为中心沿指定轴镜像顶点与控制柄（2D/3D 统一处理），随后重算控制柄</summary>
    private void ApplyAxisMirror(BezierCurve curve, bool mirX, bool mirY, bool mirZ, Vector3 center)
    {
        bool is3d = curve.Is3D;
        foreach (var v in curve.Vertices)
        {
            Vector3 vw, lhw, rhw;
            if (is3d)
            {
                vw = v.PositionV3;
                lhw = v.PositionV3 + v.LeftHandleOffsetV3;
                rhw = v.PositionV3 + v.RightHandleOffsetV3;
            }
            else
            {
                vw = curve.MapToWorld(v.Position);
                lhw = curve.MapToWorld(v.LeftHandlePosition);
                rhw = curve.MapToWorld(v.RightHandlePosition);
            }

            if (mirX) { vw.x = 2f * center.x - vw.x; lhw.x = 2f * center.x - lhw.x; rhw.x = 2f * center.x - rhw.x; }
            if (mirY) { vw.y = 2f * center.y - vw.y; lhw.y = 2f * center.y - lhw.y; rhw.y = 2f * center.y - rhw.y; }
            if (mirZ) { vw.z = 2f * center.z - vw.z; lhw.z = 2f * center.z - lhw.z; rhw.z = 2f * center.z - rhw.z; }

            if (is3d)
            {
                v.Position = new Vector2(vw.x, vw.z);
                v.Height = vw.y;
                v.LeftHandle = new Vector2(lhw.x - vw.x, lhw.z - vw.z);
                v.LeftHandleHeight = lhw.y - vw.y;
                v.RightHandle = new Vector2(rhw.x - vw.x, rhw.z - vw.z);
                v.RightHandleHeight = rhw.y - vw.y;
            }
            else
            {
                v.Position = curve.MapFromWorld(vw);
                Vector2 nLH = curve.MapFromWorld(lhw);
                Vector2 nRH = curve.MapFromWorld(rhw);
                v.LeftHandle = nLH - v.Position;
                v.RightHandle = nRH - v.Position;
            }
        }
        curve.RecalculateHandles();
    }

    /// <summary>Cursor foldout: reference-frame toggle / position (per-axis locks) / lock / reset.
    /// 游标折叠区：参考系开关/位置（分轴锁）/锁定/重置</summary>
    private void DrawCursorFoldout()
    {
        // ---------- Cursor reference frame / 游标参考系 ----------
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

        // ---------- Cursor properties / 游标属性 ----------
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

        // ---------- Lock cursor / 锁定游标 ----------
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

        // ---------- Reset cursor position / 重置游标位置 ----------
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

        // Read back the edit buffer / 回读缓冲区
        if (vertex != null)
        {
            _vertexPosition = vertex.Position;
            _vertexHeight = vertex.Height;
            _vertexLockX = vertex.LockX;
            _vertexLockY = vertex.LockY;
            _vertexLockZ = vertex.LockZ;
            // Pick the HandleType to display based on the selected sub-element / 根据选中的子元素决定显示的 HandleType
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
            // Zero the buffers when nothing is selected to avoid stale values / 无选中时缓冲区归零，避免显示过期值
            _vertexPosition = Vector2.zero;
            _vertexHeight = 0f;
            _vertexLockX = _vertexLockY = _vertexLockZ = false;
            _vertexHandleType = HandleType.Auto;
        }

        // Vertex position + axis locks / 顶点位置 + 轴锁
        bool is3dCurve = vertex != null && _manager.SelectedCurve != null && _manager.SelectedCurve.Is3D;
        var selPlane = _manager.SelectedCurve?.Plane ?? CurvePlane.XZ;
        GetPlaneAxisLabels(selPlane, is3dCurve, out string axisA, out string axisNormal, out string axisB);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("vertex_position"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        DrawVertexAxisLock(axisA, ref _vertexLockX, ref _vertexPosition.x);
        GUILayout.Space(4);
        // Normal axis (height): grayed out for 2D, editable for 3D / 法线轴（高度/Height）：2D 灰显，3D 解锁
        if (is3dCurve)
            DrawVertexAxisLock(axisNormal, ref _vertexLockZ, ref _vertexHeight);
        else
        {
            DrawYAxis2DPlaceholder(axisNormal);
        }
        GUILayout.Space(4);
        // Second plane axis / 平面第二轴
        DrawVertexAxisLock(axisB, ref _vertexLockY, ref _vertexPosition.y);
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

        // Handle type / 控制柄类型
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
                // When the vertex itself or its Y arrow is selected, apply the type to both sides / 选中顶点本身或顶点Y箭头时，同时设置两侧
                vertex.HandleTypeA = newType;
                vertex.HandleTypeB = newType;
            }
            // Mirror/aligned: sync the value from the opposite side immediately / 镜像/对齐：立即从对侧同步值
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

        // Handle positions (left/right XYZ with locks) / 控制柄位置（左右柄 XYZ 带锁）
        DrawHandlePosition(vertex, is3dCurve);

        GUILayout.Space(4);
    }

    /// <summary>Handle positions: per-axis locked XYZ editing for the left and right handles.
    /// 控制柄位置：左右柄 XYZ 分轴锁编辑</summary>
    private void DrawHandlePosition(CurveVertex vertex, bool is3d)
    {
        EditorGUI.BeginDisabledGroup(vertex == null);
        var hp = _manager.SelectedCurve?.Plane ?? CurvePlane.XZ;
        GetPlaneAxisLabels(hp, is3d, out string hA, out string hN, out string hB);

        if (vertex != null)
        {
            // Read back the handle edit buffers / 回读控制柄缓冲区
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

        // Left handle / 左柄
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("left_handle"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        DrawVertexAxisLock(hA, ref _leftHandleLockX, ref _leftHandlePos.x);
        GUILayout.Space(4);
        // Normal axis (height offset) / 法线轴（高度偏移）
        if (is3d)
            DrawVertexAxisLock(hN, ref _leftHandleLockZ, ref _leftHandleHeight);
        else
        {
            DrawYAxis2DPlaceholder(hN);
        }
        GUILayout.Space(4);
        // Second plane axis / 平面第二轴
        DrawVertexAxisLock(hB, ref _leftHandleLockY, ref _leftHandlePos.y);
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

        // Right handle / 右柄
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("right_handle"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        DrawVertexAxisLock(hA, ref _rightHandleLockX, ref _rightHandlePos.x);
        GUILayout.Space(4);
        // Normal axis (height offset) / 法线轴（高度偏移）
        if (is3d)
            DrawVertexAxisLock(hN, ref _rightHandleLockZ, ref _rightHandleHeight);
        else
        {
            DrawYAxis2DPlaceholder(hN);
        }
        GUILayout.Space(4);
        // Second plane axis / 平面第二轴
        DrawVertexAxisLock(hB, ref _rightHandleLockY, ref _rightHandlePos.y);
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

    /// <summary>Grayed-out placeholder for the 2D curve's normal (height) axis: no height to edit, gray styling hints at 2D mode.
    /// 2D 曲线的法线（高度）轴灰显占位：无高度轴可编辑，用置灰样式提示 2D 模式</summary>
    private void DrawYAxis2DPlaceholder(string axisName)
    {
        GUI.backgroundColor = UiPlaceholderGray;
        GUILayout.Label("L", GUILayout.Width(20), GUILayout.Height(18));
        GUI.backgroundColor = Color.white;
        GUILayout.Label(axisName, GUILayout.Width(12));
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("2D", GUILayout.MinWidth(30));
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>Plane-aware axis labels: axisA/axisB are the two editable plane axes (map to Position.x/.y);
    /// axisNormal is the plane normal (the grayed "height" for 2D, the editable world-Y height for 3D).
    /// 依平面的轴标签：axisA/axisB 为平面内两个可编辑轴（对应 Position.x/.y）；axisNormal 为平面法线（2D 下灰显高度，3D 下世界 Y 高度）。</summary>
    private void GetPlaneAxisLabels(CurvePlane plane, bool is3d, out string axisA, out string axisNormal, out string axisB)
    {
        if (is3d) { axisA = "X"; axisNormal = "Y"; axisB = "Z"; return; } // 3D：X / 高度Y / Z
        switch (plane)
        {
            case CurvePlane.XY: axisA = "X"; axisNormal = "Z"; axisB = "Y"; break;
            case CurvePlane.YZ: axisA = "Y"; axisNormal = "X"; axisB = "Z"; break;
            default: axisA = "X"; axisNormal = "Y"; axisB = "Z"; break; // XZ
        }
    }

    /// <summary>Draws a lock button + label + float field (shared by vertices/handles/cursor).
    /// 绘制分轴锁按钮 + 标签 + 浮点输入框（顶点/控制柄/游标共用）</summary>
    private void DrawVertexAxisLock(string label, ref bool locked, ref float value)
    {
        GUI.backgroundColor = locked ? UiLockedOrange : UiDisabledGray;
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

    /// <summary>Curve list. / 曲线列表</summary>
    private void DrawCurveList()
    {

        // Create row: name + segment count + 2D/3D buttons / 新建行：名称 + 线段数 + 2D/3D 创建按钮
        EditorGUILayout.BeginHorizontal();
        _newCurveName = EditorGUILayout.TextField(_newCurveName, GUILayout.MinWidth(60));
        _defaultSegmentCount = Mathf.Clamp(EditorGUILayout.IntField(_defaultSegmentCount, GUILayout.Width(36)), 1, 256);

        // 2D button: creates a curve on the XZ plane by default / 2D 按钮：默认在 XZ 平面创建曲线
        GUI.backgroundColor = UiCreateGreen;
        if (GUILayout.Button("2D", GUILayout.Width(45)))
            CreateNewCurve(CurvePlane.XZ);

        // 3D button: creates a 3D curve / 3D 按钮：创建 3D 曲线
        GUI.backgroundColor = UiCreateBlue;
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
            if (isSel) GUI.backgroundColor = UiSelectedBg;

            EditorGUILayout.BeginHorizontal();

            // Select button: ○ selected, × not; click to select the row / 选择按钮：○ 当前选中，× 未选中，点击即选中该行
            string selLabel = isSel ? "○" : "×";
            GUI.backgroundColor = isSel ? UiCreateGreen : new Color(0.6f, 0.6f, 0.6f);
            if (GUILayout.Button(selLabel, GUILayout.Width(24)))
            {
                _manager.Select(i);
                SceneView.RepaintAll();
            }

            // Display D / 可见 D
            GUI.backgroundColor = curve.IsVisible ? Color.white : UiPlaceholderGray;
            if (GUILayout.Button("D", GUILayout.Width(22)))
            { Undo.RecordObject(_manager, "隐藏"); curve.IsVisible = !curve.IsVisible; _manager.MarkDirty(); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

            // Enable E (generation permission; disabled curves cannot generate objects) / 启用 E（生成许可，禁用时不可生成物体）
            GUI.backgroundColor = curve.IsEnabled ? Color.white : UiPlaceholderGray;
            if (GUILayout.Button("E", GUILayout.Width(22)))
            { Undo.RecordObject(_manager, "启用"); curve.IsEnabled = !curve.IsEnabled; _manager.MarkDirty(); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

            // Lock L / 锁定 L
            GUI.backgroundColor = curve.IsLocked ? UiLockedOrange : UiDisabledGray;
            if (GUILayout.Button("L", GUILayout.Width(22)))
            { Undo.RecordObject(_manager, "锁定"); curve.IsLocked = !curve.IsLocked; _manager.MarkDirty(); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

            // Name / 名称
            string nn = EditorGUILayout.TextField(curve.Name, GUILayout.MinWidth(44));
            if (nn != curve.Name) { Undo.RecordObject(_manager, "重命名"); curve.Name = nn; _manager.MarkDirty(); }

            // World plane (3D curves show a grayed-out "3D") / 平面（3D 曲线灰显为 "3D"）
            EditorGUI.BeginDisabledGroup(!_editMode || curve.Is3D);
            if (curve.Is3D)
            {
                GUILayout.Label("3D", GUILayout.Width(42));
            }
            else
            {
                var np = (CurvePlane)EditorGUILayout.EnumPopup(curve.Plane, GUILayout.Width(42));
                if (np != curve.Plane) { Undo.RecordObject(_manager, "平面"); curve.Plane = np; _manager.MarkDirty(); SceneView.RepaintAll(); }
            }
            EditorGUI.EndDisabledGroup();
            // Segment count (clamped 1..256 to avoid huge SegmentInfo lists or overflow) / 线段数（限制 1..256，避免生成海量 SegmentInfo 或越界）
            int nsc = Mathf.Clamp(EditorGUILayout.IntField(curve.SegmentCount, GUILayout.Width(36)), 1, 256);
            if (nsc != curve.SegmentCount)
            { Undo.RecordObject(_manager, "线段数"); curve.SegmentCount = nsc; curve.RebuildSegments(); _manager.MarkDirty(); SceneView.RepaintAll(); }

            // Loop R / 闭环 R
            GUI.backgroundColor = curve.IsLoop ? UiSelectedBlue : UiDisabledGray;
            if (GUILayout.Button("R", GUILayout.Width(22)))
            { Undo.RecordObject(_manager, "闭环"); curve.IsLoop = !curve.IsLoop; curve.RebuildSegments(); curve.RecalculateHandles(); _manager.MarkDirty(); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

            // Copy C / 复制 C
            GUI.backgroundColor = UiCopyBlue;
            if (GUILayout.Button("C", GUILayout.Width(22)))
            { DuplicateCurve(curve); SceneView.RepaintAll(); }
            GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

            EditorGUI.EndDisabledGroup();

            // Delete / 删除
            GUI.backgroundColor = UiDeleteRed;
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            { _manager.RemoveCurve(i); SceneView.RepaintAll(); GUIUtility.ExitGUI(); }
            GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndScrollView();

        if (_manager.Curves.Count > 0)
        {
            GUI.backgroundColor = UiDeleteAllRed;
            if (GUILayout.Button(L10n.T("del_all_curves"), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog(L10n.T("confirm"), L10n.T("del_confirm", _manager.Curves.Count), L10n.T("ok"), L10n.T("cancel")))
                { Undo.RecordObject(_manager, "删除全部"); _manager.Curves.Clear(); _manager.ClearSelection(); _manager.MarkDirty(); SceneView.RepaintAll(); }
            }
            GUI.backgroundColor = Color.white;
        }

        // Row button legend (moved from the removed About tab so new users understand the buttons) / 行按钮图例（原 About 页签内容，防止新用户无法理解按钮含义）
        EditorGUILayout.LabelField(L10n.T("curve_row_legend"), EditorStyles.miniLabel);
    }

    /// <summary>Selected-segment properties panel (controls always drawn, grayed out without selection).
    /// 选中线段属性面板（始终显示控件，无选中时灰显）</summary>
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

        // Always read back the buffer (placeholder defaults when nothing is selected) / 始终回读缓冲（无选中时用默认值/缓冲值占位）
        if (hasSelection)
        {
            var seg = curve.Segments[firstIdx];
            _segPrimitiveType = seg.PrimitiveType;
            _segBaseScale = seg.BaseScale;
            _segPositionOffset = seg.PositionOffset;
            _segFitSegmentLength = seg.FitSegmentLength;
            _segFitAxis = seg.FitAxis;
            _segFitMode = seg.FitMode;
            _segRelativeScale = seg.RelativeScale;
            _segRotationOffset = seg.RotationOffset;
            _segPositionOffset3D = seg.PositionOffset3D;
        }

        EditorGUI.BeginDisabledGroup(!hasSelection || !_editMode);

        // Field order: primitive → base scale → center offset → fit enable|mode → scale axis → relative scale → rotation offset → position offset
        // 字段顺序：基础物体 → 基础缩放 → 中心点偏移 → 适应启用|模式 → 缩放轴 → 相对缩放 → 旋转偏移 → 位置偏移
        EditorGUI.BeginChangeCheck();
        string[] primNames = _primNames ??= new[] { L10n.T("primitive_sphere"), L10n.T("primitive_capsule"), L10n.T("primitive_cylinder"), L10n.T("primitive_cube"), L10n.T("primitive_plane"), L10n.T("primitive_quad") };
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("base_primitive"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        _segPrimitiveType = (PrimitiveType)EditorGUILayout.Popup((int)_segPrimitiveType, primNames);
        EditorGUILayout.EndHorizontal();

        // Base scale X/Y/Z inputs / 基础缩放 X/Y/Z 分轴输入
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

        // Fit row: enable toggle + mode dropdown (Simple/Advanced). The axis dropdown moved to its own row below,
        // since the mode dropdown now occupies this row's popup slot. / 适应行：启用开关 + 模式下拉（简单/进阶）。
        // 缩放轴下拉被模式下拉占用，故移到下一行独立显示。
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("fit_length"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        _segFitSegmentLength = EditorGUILayout.Toggle(_segFitSegmentLength, GUILayout.Width(16));
        _fitModeNames ??= new[] { L10n.T("fit_mode_simple"), L10n.T("fit_mode_advanced") };
        // Advanced gap-filling fit only applies to 2D curves: for 3D curves force Simple and gray the mode dropdown.
        // 进阶填缺口适应模式仅适用于 2D 曲线；3D 曲线强制为简单并灰显模式下拉。
        bool advancedAllowed = curve == null || !curve.Is3D;
        if (!advancedAllowed) _segFitMode = 0;
        EditorGUI.BeginDisabledGroup(!advancedAllowed);
        _segFitMode = EditorGUILayout.Popup(_segFitMode, _fitModeNames);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        // Scale axis dropdown (moved to its own row) / 缩放轴下拉（移到下一行）
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("fit_axis"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        _segFitAxis = EditorGUILayout.Popup(_segFitAxis, AxisNames);
        EditorGUILayout.EndHorizontal();

        // Relative scale X/Y/Z inputs / 相对缩放 X/Y/Z 分轴输入
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("rel_scale"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GUILayout.Label("X", GUILayout.Width(12)); _segRelativeScale.x = EditorGUILayout.FloatField(_segRelativeScale.x, GUILayout.MinWidth(40));
        GUILayout.Label("Y", GUILayout.Width(12)); _segRelativeScale.y = EditorGUILayout.FloatField(_segRelativeScale.y, GUILayout.MinWidth(40));
        GUILayout.Label("Z", GUILayout.Width(12)); _segRelativeScale.z = EditorGUILayout.FloatField(_segRelativeScale.z, GUILayout.MinWidth(40));
        EditorGUILayout.EndHorizontal();

        // Rotation offset X/Y/Z inputs / 旋转偏移 X/Y/Z 分轴输入
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("rot_offset"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GUILayout.Label("X", GUILayout.Width(12)); _segRotationOffset.x = EditorGUILayout.FloatField(_segRotationOffset.x, GUILayout.MinWidth(40));
        GUILayout.Label("Y", GUILayout.Width(12)); _segRotationOffset.y = EditorGUILayout.FloatField(_segRotationOffset.y, GUILayout.MinWidth(40));
        GUILayout.Label("Z", GUILayout.Width(12)); _segRotationOffset.z = EditorGUILayout.FloatField(_segRotationOffset.z, GUILayout.MinWidth(40));
        EditorGUILayout.EndHorizontal();

        // Position offset X/Y/Z inputs / 位置偏移 X/Y/Z 分轴输入
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
                curve.Segments[i].FitMode = _segFitMode;
                curve.Segments[i].RelativeScale = _segRelativeScale;
                curve.Segments[i].RotationOffset = _segRotationOffset;
                curve.Segments[i].PositionOffset3D = _segPositionOffset3D;
            }
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>Generate button. / 生成按钮</summary>
    private void DrawGenerateButton()
    {

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(L10n.T("gen_color"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.3f));
        GenerationColor = EditorGUILayout.ColorField(GenerationColor);
        EditorGUILayout.EndHorizontal();
        if (GUI.changed) SceneView.RepaintAll();

        var genCurve = _manager.SelectedCurve;
        bool canGen = genCurve != null && genCurve.IsEnabled;
        EditorGUI.BeginDisabledGroup(!canGen);
        GUI.backgroundColor = UiActionGreen;
        if (GUILayout.Button(L10n.T("generate"), GUILayout.Height(34)))
            CurveObjectBuilder.Build(_manager.SelectedCurve);
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();
        if (!canGen && _manager.Curves.Count > 0)
            EditorGUILayout.HelpBox(L10n.T("sel_enabled_curve"), MessageType.Info);
    }
}




