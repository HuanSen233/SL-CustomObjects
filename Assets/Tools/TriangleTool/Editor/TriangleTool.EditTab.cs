using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// Triangle Tool — Edit tab (mode bar, single triangle, OBJ model, statistics).
    /// UI 结构参考 CurvesTool.EditTab：模式栏 + 折叠区 + 彩色按钮行 + 图例小字。
    /// 三角面工具 — 编辑 Tab（模式栏、单三角形、OBJ 模型、统计）。
    /// </summary>
    public partial class TriangleTool
    {
        // ============================================================
        //  Edit Tab / 编辑 Tab
        // ============================================================
        private void TabEdit()
        {
            DrawModeBar();
            GUILayout.Space(4);

            Vector2 scroll = Vector2.zero;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            // ~ Single triangle / 单三角形
            _foldoutSingle = EditorGUILayout.Foldout(_foldoutSingle, TriangleL10n.T("single_triangle"), true);
            if (_foldoutSingle) DrawSingleTriangle();

            // ~ OBJ model / OBJ 模型
            _foldoutObj = EditorGUILayout.Foldout(_foldoutObj, TriangleL10n.T("obj_model"), true);
            if (_foldoutObj) DrawObjModel();

            // ~ Statistics / 统计
            _foldoutStats = EditorGUILayout.Foldout(_foldoutStats, TriangleL10n.T("statistics"), true);
            if (_foldoutStats) DrawStatsSection();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(TriangleL10n.T("mode_legend"), EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Mode bar: three build-mode toggles (V1/V2/V3), active mode highlighted green.
        /// 模式栏：三个构建模式开关（V1/V2/V3），当前模式绿色高亮。</summary>
        private void DrawModeBar()
        {
            EditorGUILayout.BeginHorizontal();

            float w = (EditorGUIUtility.currentViewWidth - 8f) / 3f;
            float h = 26f;

            GUI.backgroundColor = Mode == TriangleBuildMode.Exact ? UiCreateGreen : Color.white;
            bool v1 = GUILayout.Toggle(Mode == TriangleBuildMode.Exact, TriangleL10n.T("mode_v1"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v1 && Mode != TriangleBuildMode.Exact) { Mode = TriangleBuildMode.Exact; _objError = null; }

            GUI.backgroundColor = Mode == TriangleBuildMode.StretchClustered ? UiCreateGreen : Color.white;
            bool v2 = GUILayout.Toggle(Mode == TriangleBuildMode.StretchClustered, TriangleL10n.T("mode_v2"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v2 && Mode != TriangleBuildMode.StretchClustered) { Mode = TriangleBuildMode.StretchClustered; _objError = null; }

            GUI.backgroundColor = Mode == TriangleBuildMode.Hierarchical ? UiCreateGreen : Color.white;
            bool v3 = GUILayout.Toggle(Mode == TriangleBuildMode.Hierarchical, TriangleL10n.T("mode_v3"), "Button", GUILayout.Height(h), GUILayout.Width(w));
            GUI.backgroundColor = Color.white;
            if (v3 && Mode != TriangleBuildMode.Hierarchical) { Mode = TriangleBuildMode.Hierarchical; _objError = null; }

            EditorGUILayout.EndHorizontal();
        }

        // ---------- Single triangle / 单三角形 ----------

        private void DrawSingleTriangle()
        {
            _p1 = EditorGUILayout.Vector3Field(TriangleL10n.T("vertex_p1"), _p1);
            _p2 = EditorGUILayout.Vector3Field(TriangleL10n.T("vertex_p2"), _p2);
            _p3 = EditorGUILayout.Vector3Field(TriangleL10n.T("vertex_p3"), _p3);

            if (GUILayout.Button(TriangleL10n.T("read_selection")))
                ReadFromSelection();

            EditorGUILayout.Space(4);
            _scale = EditorGUILayout.FloatField(TriangleL10n.T("scale"), _scale);
            FlipWinding = EditorGUILayout.Toggle(TriangleL10n.T("flip_winding"), FlipWinding);
            UseRectangleOptimization = EditorGUILayout.Toggle(TriangleL10n.T("rect_opt"), UseRectangleOptimization);
            using (new EditorGUI.DisabledScope(!UseRectangleOptimization))
                RectangleTolerance = EditorGUILayout.FloatField(TriangleL10n.T("rect_tolerance"), RectangleTolerance);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = UiActionGreen;
            if (GUILayout.Button(TriangleL10n.T("build_update"), GUILayout.Height(26)))
                BuildSingleTriangle();
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = UiDeleteRed;
            if (GUILayout.Button(TriangleL10n.T("clear"), GUILayout.Height(26)))
                ClearSingleModel();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // ---------- OBJ model / OBJ 模型 ----------

        private void DrawObjModel()
        {
            EditorGUILayout.BeginHorizontal();
            _objPath = EditorGUILayout.TextField(TriangleL10n.T("obj_path"), _objPath);
            GUI.backgroundColor = UiCopyBlue;
            if (GUILayout.Button(TriangleL10n.T("browse"), GUILayout.Width(80f)))
                BrowseObj();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            _objForceColor = EditorGUILayout.Toggle(TriangleL10n.T("force_fallback"), _objForceColor);
            using (new EditorGUI.DisabledScope(!_objForceColor))
                FallbackColor = EditorGUILayout.ColorField(TriangleL10n.T("fallback_color"), FallbackColor);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = UiActionGreen;
            if (GUILayout.Button(
                    _objSession != null && _objSession.IsRunning ? TriangleL10n.T("building") : TriangleL10n.T("load_build"),
                    GUILayout.Height(26)))
            {
                if (_objSession == null || !_objSession.IsRunning)
                    LoadObj();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button(TriangleL10n.T("cancel"), GUILayout.Height(26)))
                CancelObj();

            GUI.backgroundColor = UiDeleteRed;
            if (GUILayout.Button(TriangleL10n.T("clear"), GUILayout.Height(26)))
                ClearObjModel();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_objSession != null)
            {
                var progressRect = GUILayoutUtility.GetRect(200f, 20f);
                EditorGUI.ProgressBar(progressRect, _objSession.Progress,
                    _objSession.TrianglesBuilt + " / " + _objSession.TotalTriangles + " triangles");
            }

            if (!string.IsNullOrEmpty(_objError))
                EditorGUILayout.HelpBox(_objError, MessageType.Error);
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
