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
        private bool _foldoutTransform = true;
        private bool _foldoutCursor = true;
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

            // ~ Transform (flip/mirror on the selected faces) / 变换（翻转/镜像，作用于选中面）
            _foldoutTransform = EditorGUILayout.Foldout(_foldoutTransform, TriangleL10n.T("transform"), true);
            if (_foldoutTransform) DrawTransformFoldoutT();

            // ~ Cursor (global reference frame) / 游标（全局参考系）
            _foldoutCursor = EditorGUILayout.Foldout(_foldoutCursor, TriangleL10n.T("cursor_section"), true);
            if (_foldoutCursor) DrawCursorFoldoutT();

            // ~ Generate model / 生成模型
            _foldoutGen = EditorGUILayout.Foldout(_foldoutGen, TriangleL10n.T("generate_section"), true);
            if (_foldoutGen) DrawGenerateButton();

            // ~ Statistics / 统计
            _foldoutStats = EditorGUILayout.Foldout(_foldoutStats, TriangleL10n.T("statistics"), true);
            if (_foldoutStats) DrawStatsSection();

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
                // Use the face's multi-select flag (not SelectedFaceIndex, which only tracks the anchor) so
                // every selected row highlights, not just the last-clicked one. / 用面的多选标记（而非仅指向
                // 锚点的 SelectedFaceIndex），使每个已选行都高亮，而非只有最后点击那行。
                bool isSel = face.IsSelected;
                if (isSel) GUI.backgroundColor = UiSelectedBg;

                EditorGUILayout.BeginHorizontal();

                // Select button: ○ selected, × not; Shift/Ctrl makes it additive multi-select. The
                // last-clicked (anchor) button is bright green; other selected buttons use the original
                // dark green; unselected are gray. / 选择按钮：○ 已选，× 未选；Shift/Ctrl 为多选。
                // 最后点击的按钮（锚点）亮绿；其余已选保持原暗绿；未选灰。
                string selLabel = isSel ? "○" : "×";
                bool isAnchor = _manager.AnchorFaceIndex == i;
                GUI.backgroundColor = isAnchor ? UiAnchorGreen
                    : isSel ? UiCreateGreen
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

            // Row-button legend (mirrors the curve list, placed at the bottom of the list section).
            // 行按钮图例（同曲线列表，置于列表区底部）。
            EditorGUILayout.LabelField(TriangleL10n.T("tri_row_legend"), EditorStyles.miniLabel);
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

        // ---------- Transform (flip/mirror) + Cursor / 变换（翻转/镜像）与游标 ----------

        /// <summary>Transform foldout: flip/mirror the selected face(s) about the origin or cursor (shared UI).
        /// 变换折叠区：翻转/镜像选中面（原点/游标为中心），使用共享 UI。</summary>
        private void DrawTransformFoldoutT()
        {
            if (_manager == null) return;
            bool canEdit = SelectedEditableFaces().Count > 0;
            float labelW = EditorGUIUtility.currentViewWidth * 0.3f;
            SharedToolFoldoutUI.DrawTransformFoldout(
                _manager, k => TriangleL10n.T(k), canEdit, labelW, (true, true, true),
                ApplyFlipToFaces, DuplicateThenMirrorFaces);
        }

        /// <summary>Cursor foldout: reference frame, per-axis position/locks, lock, reset (shared UI).
        /// 游标折叠区：参考系、分轴位置/锁、锁定、重置（共享 UI）。</summary>
        private void DrawCursorFoldoutT()
        {
            if (_manager == null) return;
            float labelW = EditorGUIUtility.currentViewWidth * 0.3f;
            SharedToolFoldoutUI.DrawCursorFoldout(
                _manager, k => TriangleL10n.T(k),
                () => Undo.RecordObject(_manager, "游标"), () => _manager.MarkDirty(),
                () => SceneView.RepaintAll(), _editMode, labelW);
        }

        /// <summary>Selected, unlocked faces (falls back to the single selected face when none are multi-selected).
        /// 选中的未锁定面（无多选时回退到当前选中的单个面）。</summary>
        private System.Collections.Generic.List<TriangleFace> SelectedEditableFaces()
        {
            var list = new System.Collections.Generic.List<TriangleFace>();
            if (_manager == null) return list;
            bool anySel = _manager.HasSelection();
            if (anySel)
            {
                foreach (var f in _manager.Faces)
                    if (f != null && !f.IsLocked && f.IsSelected) list.Add(f);
            }
            else
            {
                var sf = _manager.SelectedFace;
                if (sf != null && !sf.IsLocked) list.Add(sf);
            }
            return list;
        }

        /// <summary>Mirrors the three face points about a center along the given axes. / 以 center 为中心沿指定轴镜像面三点。</summary>
        private void MirrorFacePoints(TriangleFace face, Vector3 center, bool mx, bool my, bool mz)
        {
            Vector3 p1 = face.P1; EditTransformOps.MirrorPoint(ref p1, center, mx, my, mz); face.P1 = p1;
            Vector3 p2 = face.P2; EditTransformOps.MirrorPoint(ref p2, center, mx, my, mz); face.P2 = p2;
            Vector3 p3 = face.P3; EditTransformOps.MirrorPoint(ref p3, center, mx, my, mz); face.P3 = p3;
        }

        /// <summary>Flip: mirror the selected face(s) in place. / 翻转：就地镜像选中面。</summary>
        private void ApplyFlipToFaces(Vector3 center, bool mx, bool my, bool mz)
        {
            if (_manager == null) return;
            var targets = SelectedEditableFaces();
            if (targets.Count == 0) return;
            Undo.RecordObject(_manager, "翻转顶点");
            foreach (var f in targets) MirrorFacePoints(f, center, mx, my, mz);
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        /// <summary>Mirror: duplicate the selected face(s), then mirror the duplicates (like the curve tool's Mirror).
        /// 镜像：复制选中面，再镜像副本（同曲线工具的"镜像"）。</summary>
        private void DuplicateThenMirrorFaces(Vector3 center, bool mx, bool my, bool mz)
        {
            if (_manager == null) return;
            var targets = SelectedEditableFaces();
            if (targets.Count == 0) return;
            Undo.RecordObject(_manager, "镜像三角面");
            int firstDup = -1;
            foreach (var src in targets)
            {
                if (src == null) continue;
                var clone = JsonUtility.FromJson<TriangleFace>(JsonUtility.ToJson(src));
                clone.IsSelected = false;
                clone.Name = NameUtil.Deduplicate(src.Name + "Mirror", n => _manager.Faces.Exists(f => f.Name == n));
                MirrorFacePoints(clone, center, mx, my, mz);
                _manager.Faces.Add(clone);
                if (firstDup < 0) firstDup = _manager.Faces.Count - 1;
            }
            if (firstDup >= 0)
            {
                _manager.ClearSelection();
                for (int i = firstDup; i < _manager.Faces.Count; i++) _manager.Faces[i].IsSelected = true;
                _manager.SelectedFaceIndex = firstDup;
                _manager.AnchorFaceIndex = firstDup;
            }
            _manager.MarkDirty();
            SceneView.RepaintAll();
        }

        // ---------- Generate model / 生成模型 ----------

        /// <summary>Build-mode selector (V1/V2/V3) above the Generate button, plus the Generate button itself.
        /// The mode is persisted with the settings; selection drives the parallelization cost trade-off.
        /// 生成按钮上方的构建模式选择（V1/V2/V3）+ 生成按钮本身。模式随设置持久化，决定物体数量/精度权衡。</summary>
        private void DrawGenerateButton()
        {
            DrawBuildModeBar(Mode, m => { Mode = m; SaveSettings(); SceneView.RepaintAll(); }, TriangleL10n.T("mode"));
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
        /// Shared by the Edit tab (Mode) and the Model Import tab (ObjMode). / 三键构建模式栏（V1/V2/V3），
        /// 当前模式绿色高亮。编辑页（Mode）与模型导入页（ObjMode）共用。</summary>
        private void DrawBuildModeBar(TriangleBuildMode current, System.Action<TriangleBuildMode> onSet, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            float w = (EditorGUIUtility.currentViewWidth - 8f) / 3f;
            float h = 26f;

            GUI.backgroundColor = current == TriangleBuildMode.Exact ? UiCreateGreen : Color.white;
            bool v1 = GUILayout.Toggle(current == TriangleBuildMode.Exact, TriangleL10n.T("mode_v1"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v1 && current != TriangleBuildMode.Exact) onSet(TriangleBuildMode.Exact);

            GUI.backgroundColor = current == TriangleBuildMode.StretchClustered ? UiCreateGreen : Color.white;
            bool v2 = GUILayout.Toggle(current == TriangleBuildMode.StretchClustered, TriangleL10n.T("mode_v2"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v2 && current != TriangleBuildMode.StretchClustered) onSet(TriangleBuildMode.StretchClustered);

            GUI.backgroundColor = current == TriangleBuildMode.Hierarchical ? UiCreateGreen : Color.white;
            bool v3 = GUILayout.Toggle(current == TriangleBuildMode.Hierarchical, TriangleL10n.T("mode_v3"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v3 && current != TriangleBuildMode.Hierarchical) onSet(TriangleBuildMode.Hierarchical);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(TriangleL10n.T("mode_legend"), EditorStyles.miniLabel);
        }

        // ---------- Statistics / 统计 ----------

        private void DrawStatsSection()
        {
            // Stats only for the model generated by THIS tab's Generate. / 仅统计编辑页"生成"产生的模型。
            TriangleModelBuilder b = FaceStatsBuilder;

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
