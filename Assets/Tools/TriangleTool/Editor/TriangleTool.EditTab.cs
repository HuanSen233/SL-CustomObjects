using ToolLib;
using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// Triangle Tool — Edit tab (mode bar, triangle-face list, face properties, generate, statistics).
    /// UI 结构参考 CurvesTool.EditTab：模式栏 + 折叠区 + 彩色按钮行 + 图例小字（标签左 1/3、控件右 2/3）。
    /// 三角面工具 — 编辑 Tab（模式栏、三角面列表、三角面属性、生成、统计）。
    /// </summary>
    public partial class TriangleTool
    {
        // ===== Edit-tab foldout + buffer state / 编辑页折叠与缓冲状态 =====
        private bool _foldoutTriList = true;
        private bool _foldoutTriProps = true;
        private bool _foldoutGen = true;
        private string _newFaceName = "NewTriangle";
        private Vector2 _scrollTriList;

        // ============================================================
        //  Edit Tab / 编辑 Tab
        // ============================================================
        private void TabEdit()
        {
            DrawModeBar();
            GUILayout.Space(4);

            Vector2 scroll = Vector2.zero;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            // ~ Triangle-face list (create/select, first) / 三角面列表（创建/选择，最先）
            _foldoutTriList = EditorGUILayout.Foldout(_foldoutTriList, TriangleL10n.T("tri_list"), true);
            if (_foldoutTriList) DrawFaceList();

            // ~ Selected face properties / 选中面属性
            _foldoutTriProps = EditorGUILayout.Foldout(_foldoutTriProps, TriangleL10n.T("tri_props"), true);
            if (_foldoutTriProps) DrawFaceProperties();

            // ~ Generate model / 生成模型
            _foldoutGen = EditorGUILayout.Foldout(_foldoutGen, TriangleL10n.T("generate_section"), true);
            if (_foldoutGen) DrawGenerateButton();

            // ~ Statistics / 统计
            _foldoutStats = EditorGUILayout.Foldout(_foldoutStats, TriangleL10n.T("statistics"), true);
            if (_foldoutStats) DrawStatsSection();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(TriangleL10n.T("tri_row_legend"), EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Mode bar: Edit Mode takes 4/5 of the row, Preview takes 1/5 (highlight triangle outlines).
        /// 模式栏：编辑模式占行宽 4/5，预览占 1/5（高亮三角面轮廓）。</summary>
        private void DrawModeBar()
        {
            EditorGUILayout.BeginHorizontal();

            float previewW = EditorGUIUtility.currentViewWidth * 0.2f;
            float editW = EditorGUIUtility.currentViewWidth * 0.8f - 4f;

            // Edit Mode (4/5) / 编辑模式（4/5）
            GUI.backgroundColor = _editMode ? UiActionGreen : Color.white;
            bool newEdit = GUILayout.Toggle(_editMode, $" {TriangleL10n.T("edit_mode")}", "Button", GUILayout.Height(28), GUILayout.Width(editW));
            GUI.backgroundColor = Color.white;
            if (newEdit != _editMode)
            {
                // Mutual exclusion: turning edit mode on takes ownership and turns the other tool's edit mode off.
                // 互斥：开启编辑模式即取得占用，并关闭另一工具（曲线）的编辑模式。
                EditModeGate.Request("triangle", newEdit, () => { _editMode = false; SceneView.RepaintAll(); });
                _editMode = newEdit;
                SceneView.RepaintAll();
            }

            // Preview (1/5, a view mode alongside Edit Mode; toggles highlight on/off) / 预览（1/5，与编辑模式并列；切换高亮开关）
            GUI.backgroundColor = _previewOn ? UiPreviewBlue : Color.white;
            if (GUILayout.Button($" {TriangleL10n.T("preview")}", "Button", GUILayout.Height(28), GUILayout.Width(previewW)))
            {
                _previewOn = !_previewOn;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // ---------- Triangle-face list / 三角面列表 ----------

        /// <summary>Current manager faces list (nulled when the manager is unbound). / 当前管理的三角面列表（管理器未绑定时为 null）。</summary>
        private System.Collections.Generic.List<TriangleFace> FaceList =>
            _manager != null ? _manager.Faces : null;

        private void DrawFaceList()
        {
            // Create row: name + 新建 button (replaces the curve's 2D/3D buttons). / 新建行：名称 + 新建按钮（替代曲线的 2D/3D 按钮）。
            EditorGUILayout.BeginHorizontal();
            _newFaceName = EditorGUILayout.TextField(_newFaceName, GUILayout.MinWidth(60));
            GUI.backgroundColor = UiCreateGreen;
            if (GUILayout.Button(TriangleL10n.T("new_triangle"), GUILayout.Width(80)))
                CreateNewFace();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("――――――――――――――――――――――――", EditorStyles.centeredGreyMiniLabel);

            if (FaceList == null) return;

            _scrollTriList = EditorGUILayout.BeginScrollView(_scrollTriList, GUILayout.Height(200));
            for (int i = 0; i < FaceList.Count; i++)
            {
                var face = FaceList[i];
                bool isSel = _manager.SelectedFaceIndex == i;
                if (isSel) GUI.backgroundColor = UiSelectedBg;

                EditorGUILayout.BeginHorizontal();

                // Select button: ○ selected, × not; Shift/Ctrl makes it additive multi-select; the
                // last-clicked (anchor) button is bright green. / 选择按钮：○ 已选，× 未选；
                // Shift/Ctrl 为多选；最后点击的按钮（锚点）亮绿。
                string selLabel = isSel ? "○" : "×";
                bool isAnchor = _manager.AnchorFaceIndex == i;
                GUI.backgroundColor = isAnchor ? UiCreateGreen
                    : isSel ? new Color(0.45f, 0.72f, 0.45f)
                    : new Color(0.6f, 0.6f, 0.6f);
                if (GUILayout.Button(selLabel, GUILayout.Width(24)))
                {
                    Event btn = Event.current;
                    bool additive = btn != null && (btn.shift || btn.control || btn.command);
                    _manager.Select(i, additive);
                    SceneView.RepaintAll();
                }

                // Visible D / 可见 D
                GUI.backgroundColor = face.IsVisible ? Color.white : UiPlaceholderGray;
                if (GUILayout.Button("D", GUILayout.Width(22)))
                { Undo.RecordObject(_manager, "隐藏"); face.IsVisible = !face.IsVisible; _manager.MarkDirty(); SceneView.RepaintAll(); }
                GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

                // Enabled E / 启用 E
                GUI.backgroundColor = face.IsEnabled ? Color.white : UiPlaceholderGray;
                if (GUILayout.Button("E", GUILayout.Width(22)))
                { Undo.RecordObject(_manager, "启用"); face.IsEnabled = !face.IsEnabled; _manager.MarkDirty(); SceneView.RepaintAll(); }
                GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

                // Lock L / 锁定 L
                GUI.backgroundColor = face.IsLocked ? UiLockedOrange : UiDisabledGray;
                if (GUILayout.Button("L", GUILayout.Width(22)))
                { Undo.RecordObject(_manager, "锁定"); face.IsLocked = !face.IsLocked; _manager.MarkDirty(); SceneView.RepaintAll(); }
                GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

                // Name / 名称
                string nn = EditorGUILayout.TextField(face.Name, GUILayout.MinWidth(44));
                if (nn != face.Name) { Undo.RecordObject(_manager, "重命名"); face.Name = nn; _manager.MarkDirty(); }

                // Face color / 面颜色
                EditorGUI.BeginChangeCheck();
                Color nc = EditorGUILayout.ColorField(face.Color, GUILayout.Width(44));
                if (EditorGUI.EndChangeCheck())
                { Undo.RecordObject(_manager, "颜色"); face.Color = nc; _manager.MarkDirty(); SceneView.RepaintAll(); }

                // Copy C / 复制 C
                GUI.backgroundColor = UiCopyBlue;
                if (GUILayout.Button("C", GUILayout.Width(22)))
                { DuplicateFace(face); SceneView.RepaintAll(); }
                GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

                // Delete ✕ / 删除 ✕
                GUI.backgroundColor = UiDeleteRed;
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                { _manager.RemoveFace(i); SceneView.RepaintAll(); GUIUtility.ExitGUI(); }
                GUI.backgroundColor = isSel ? UiSelectedBg : Color.white;

                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            if (FaceList.Count > 0)
            {
                GUI.backgroundColor = UiDeleteAllRed;
                if (GUILayout.Button(TriangleL10n.T("del_all_faces"), GUILayout.Height(20)))
                {
                    if (EditorUtility.DisplayDialog(TriangleL10n.T("confirm"), TriangleL10n.T("del_confirm", FaceList.Count), TriangleL10n.T("ok"), TriangleL10n.T("cancel")))
                    { Undo.RecordObject(_manager, "删除全部"); FaceList.Clear(); _manager.ClearSelection(); _manager.MarkDirty(); SceneView.RepaintAll(); }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        /// <summary>Creates a new face from the name buffer and selects it. / 按名称缓冲新建面并选中。</summary>
        private void CreateNewFace()
        {
            string baseName = string.IsNullOrWhiteSpace(_newFaceName) ? "NewTriangle" : _newFaceName;
            _manager.AddFace(baseName);
            _newFaceName = "NewTriangle";
            SceneView.RepaintAll();
        }

        /// <summary>Deep-copies a face (JSON), deduplicates its name, then inserts it into the list.
        /// 深拷贝面（JSON），去重命名后插入列表。</summary>
        private void DuplicateFace(TriangleFace source)
        {
            if (source == null) return;
            var clone = JsonUtility.FromJson<TriangleFace>(JsonUtility.ToJson(source));
            clone.IsSelected = false;
            _manager.AddFace(clone);
            SceneView.RepaintAll();
        }

        // ---------- Selected face properties / 选中面属性 ----------

        /// <summary>Selected face properties: three world-space vertices + color. Editable whenever a face is
        /// selected and unlocked (independent of scene edit mode). 选中面属性：三个世界坐标点 + 颜色。
        /// 只要选中了非锁定的面即可编辑（与场景编辑模式无关）。</summary>
        private void DrawFaceProperties()
        {
            var face = _manager != null ? _manager.SelectedFace : null;
            EditorGUI.BeginDisabledGroup(face == null || face.IsLocked);

            if (face == null)
            {
                EditorGUILayout.LabelField(TriangleL10n.T("sel_face_hint"), EditorStyles.miniLabel);
            }
            else
            {
                float labelW = EditorGUIUtility.currentViewWidth * 0.3f;
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(TriangleL10n.T("vertex_p1"), GUILayout.Width(labelW));
                face.P1 = EditorGUILayout.Vector3Field(GUIContent.none, face.P1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(TriangleL10n.T("vertex_p2"), GUILayout.Width(labelW));
                face.P2 = EditorGUILayout.Vector3Field(GUIContent.none, face.P2);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(TriangleL10n.T("vertex_p3"), GUILayout.Width(labelW));
                face.P3 = EditorGUILayout.Vector3Field(GUIContent.none, face.P3);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(TriangleL10n.T("face_color"), GUILayout.Width(labelW));
                face.Color = EditorGUILayout.ColorField(GUIContent.none, face.Color);
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_manager, "修改三角面");
                    _manager.MarkDirty();
                    SceneView.RepaintAll();
                }

                // Flip face: reverse the winding (swap P2/P3), which flips the face normal.
                // 反转面：反转绕序（交换 P2/P3），从而翻转面法线。
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(TriangleL10n.T("winding"), GUILayout.Width(labelW));
                GUI.backgroundColor = UiCopyBlue;
                if (GUILayout.Button(TriangleL10n.T("flip_face"), GUILayout.ExpandWidth(true)))
                {
                    Undo.RecordObject(_manager, "反转面");
                    Vector3 tmp = face.P2; face.P2 = face.P3; face.P3 = tmp;
                    _manager.MarkDirty();
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.EndDisabledGroup();
        }

        // ---------- Generate model / 生成模型 ----------

        /// <summary>Build-mode selector (V1/V2/V3) above the Generate button, plus the Generate button itself.
        /// The mode is persisted with the settings; selection drives the parallelization cost trade-off.
        /// 生成按钮上方的构建模式选择（V1/V2/V3）+ 生成按钮本身。模式随设置持久化，决定物体数量/精度权衡。</summary>
        private void DrawGenerateButton()
        {
            DrawBuildModeBar();
            EditorGUILayout.Space(4);

            bool any = _manager != null && _manager.Faces.Count > 0;
            EditorGUI.BeginDisabledGroup(!any);
            GUI.backgroundColor = UiActionGreen;
            if (GUILayout.Button(TriangleL10n.T("generate"), GUILayout.Height(34)))
                GenerateFaces();
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>Three-button build-mode bar (V1 Exact / V2 Approx / V3 Hier), active mode green-highlighted.
        /// 三键构建模式栏（V1 精确 / V2 近似 / V3 层级），当前模式绿色高亮。</summary>
        private void DrawBuildModeBar()
        {
            EditorGUILayout.LabelField(TriangleL10n.T("mode"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            float w = (EditorGUIUtility.currentViewWidth - 8f) / 3f;
            float h = 26f;

            GUI.backgroundColor = Mode == TriangleBuildMode.Exact ? UiCreateGreen : Color.white;
            bool v1 = GUILayout.Toggle(Mode == TriangleBuildMode.Exact, TriangleL10n.T("mode_v1"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v1 && Mode != TriangleBuildMode.Exact) { Mode = TriangleBuildMode.Exact; SaveSettings(); SceneView.RepaintAll(); }

            GUI.backgroundColor = Mode == TriangleBuildMode.StretchClustered ? UiCreateGreen : Color.white;
            bool v2 = GUILayout.Toggle(Mode == TriangleBuildMode.StretchClustered, TriangleL10n.T("mode_v2"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v2 && Mode != TriangleBuildMode.StretchClustered) { Mode = TriangleBuildMode.StretchClustered; SaveSettings(); SceneView.RepaintAll(); }

            GUI.backgroundColor = Mode == TriangleBuildMode.Hierarchical ? UiCreateGreen : Color.white;
            bool v3 = GUILayout.Toggle(Mode == TriangleBuildMode.Hierarchical, TriangleL10n.T("mode_v3"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v3 && Mode != TriangleBuildMode.Hierarchical) { Mode = TriangleBuildMode.Hierarchical; SaveSettings(); SceneView.RepaintAll(); }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(TriangleL10n.T("mode_legend"), EditorStyles.miniLabel);
        }

        // ---------- Statistics / 统计 ----------

        private void DrawStatsSection()
        {
            TriangleModelBuilder b = ActiveStatsBuilder;

            if (_objSession != null && _objSession.TotalTriangles > 0)
                EditorGUILayout.LabelField(TriangleL10n.T("triangles_built"),
                    _objSession.TrianglesBuilt + " / " + _objSession.TotalTriangles);

            if (b == null)
            {
                EditorGUILayout.LabelField(TriangleL10n.T("nothing_built"), EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField(TriangleL10n.T("paras"), b.ParallelogramCount.ToString());
            EditorGUILayout.LabelField(TriangleL10n.T("quads"), b.QuadCount.ToString());
            EditorGUILayout.LabelField(TriangleL10n.T("rectangles"), b.RectangleCount.ToString());
            EditorGUILayout.LabelField(TriangleL10n.T("stretches"), b.StretchCount.ToString());

            if (b.Mode == TriangleBuildMode.StretchClustered || b.Mode == TriangleBuildMode.Hierarchical)
            {
                EditorGUILayout.LabelField(TriangleL10n.T("reparented"), b.ReparentedCount.ToString());
                EditorGUILayout.LabelField(TriangleL10n.T("stretches_saved"), b.StretchesSaved.ToString());
            }

            EditorGUILayout.LabelField(TriangleL10n.T("total_blocks"), b.TotalBlockCount.ToString());
        }
    }
}
