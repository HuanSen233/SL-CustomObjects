using System.Collections.Generic;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// Simple localization manager for the Triangle Tool (mirrors CurvesTool's L10n pattern).
    /// Switch via SetLanguage, fetch translations with T(key).
    /// 三角面工具的简易多语言管理器（仿 CurvesTool 的 L10n 模式）。通过 SetLanguage 切换，T(key) 获取翻译。
    /// </summary>
    public static class TriangleL10n
    {
        public enum Lang { EN, ZH }

        public static Lang Current { get; private set; } = Lang.EN;

        static readonly Dictionary<string, Dictionary<Lang, string>> Data = new Dictionary<string, Dictionary<Lang, string>>
        {
            { "window_title", new Dictionary<Lang, string> { { Lang.EN, "Triangle Tool" }, { Lang.ZH, "三角面工具" } } },
            { "edit_tab", new Dictionary<Lang, string> { { Lang.EN, "Edit" }, { Lang.ZH, "编辑" } } },
            { "import_tab", new Dictionary<Lang, string> { { Lang.EN, "Model Import" }, { Lang.ZH, "模型导入" } } },
            { "settings_tab", new Dictionary<Lang, string> { { Lang.EN, "Settings" }, { Lang.ZH, "设置" } } },

            // Mode bar / 模式栏
            { "edit_mode", new Dictionary<Lang, string> { { Lang.EN, "Edit Mode" }, { Lang.ZH, "编辑模式" } } },
            { "preview", new Dictionary<Lang, string> { { Lang.EN, "Preview" }, { Lang.ZH, "预览" } } },

            // Triangle-face list / 三角面列表
            { "tri_list", new Dictionary<Lang, string> { { Lang.EN, "Triangle Face List" }, { Lang.ZH, "三角面列表" } } },
            { "new_triangle", new Dictionary<Lang, string> { { Lang.EN, "New" }, { Lang.ZH, "新建" } } },
            { "del_all_faces", new Dictionary<Lang, string> { { Lang.EN, "Delete All Faces" }, { Lang.ZH, "删除全部三角面" } } },
            { "tri_row_legend", new Dictionary<Lang, string> { { Lang.EN, "○ select · D visible · E enabled · L lock · C copy · ✕ delete" }, { Lang.ZH, "○ 选中 · D 可见 · E 启用 · L 锁定 · C 复制 · ✕ 删除" } } },
            { "confirm", new Dictionary<Lang, string> { { Lang.EN, "Confirm" }, { Lang.ZH, "确认" } } },
            { "del_confirm", new Dictionary<Lang, string> { { Lang.EN, "Delete {0} triangle face(s)?" }, { Lang.ZH, "删除 {0} 个三角面？" } } },

            // Triangle-face properties / 三角面属性
            { "tri_props", new Dictionary<Lang, string> { { Lang.EN, "Triangle Face Properties" }, { Lang.ZH, "三角面属性" } } },
            { "sel_face_hint", new Dictionary<Lang, string> { { Lang.EN, "Select a face to edit its properties." }, { Lang.ZH, "请先选中一个面以编辑其属性。" } } },
            { "winding", new Dictionary<Lang, string> { { Lang.EN, "Winding" }, { Lang.ZH, "绕序" } } },
            { "flip_face", new Dictionary<Lang, string> { { Lang.EN, "Flip Face" }, { Lang.ZH, "反转面" } } },

            // Generate / 生成
            { "generate_section", new Dictionary<Lang, string> { { Lang.EN, "Generate Model" }, { Lang.ZH, "生成模型" } } },
            { "generate", new Dictionary<Lang, string> { { Lang.EN, "Generate" }, { Lang.ZH, "生成" } } },

            // Mode bar / 模式栏
            { "mode", new Dictionary<Lang, string> { { Lang.EN, "Build Mode" }, { Lang.ZH, "构建模式" } } },
            { "mode_v1", new Dictionary<Lang, string> { { Lang.EN, "V1 Exact" }, { Lang.ZH, "V1 精确" } } },
            { "mode_v2", new Dictionary<Lang, string> { { Lang.EN, "V2 Approx" }, { Lang.ZH, "V2 近似" } } },
            { "mode_v3", new Dictionary<Lang, string> { { Lang.EN, "V3 Hier" }, { Lang.ZH, "V3 层级" } } },
            { "mode_legend", new Dictionary<Lang, string> { { Lang.EN, "V1 pixel-exact · V2 shared stretches · V3 hierarchical parenting (fewest primitives)" }, { Lang.ZH, "V1 像素级精确 · V2 共享拉伸 · V3 层级父子（Primitive 最少）" } } },

            // Single triangle / 单三角形
            { "single_triangle", new Dictionary<Lang, string> { { Lang.EN, "Single Triangle" }, { Lang.ZH, "单三角形" } } },
            { "vertex_p1", new Dictionary<Lang, string> { { Lang.EN, "Vertex P1 (red)" }, { Lang.ZH, "顶点 P1（红）" } } },
            { "vertex_p2", new Dictionary<Lang, string> { { Lang.EN, "Vertex P2 (green)" }, { Lang.ZH, "顶点 P2（绿）" } } },
            { "vertex_p3", new Dictionary<Lang, string> { { Lang.EN, "Vertex P3 (blue)" }, { Lang.ZH, "顶点 P3（蓝）" } } },
            { "read_selection", new Dictionary<Lang, string> { { Lang.EN, "Read from Selection (3 Transforms)" }, { Lang.ZH, "从选中物体读取（3 个 Transform）" } } },
            { "scale", new Dictionary<Lang, string> { { Lang.EN, "Scale (about centroid)" }, { Lang.ZH, "缩放（绕质心）" } } },
            { "flip_winding", new Dictionary<Lang, string> { { Lang.EN, "Flip Winding" }, { Lang.ZH, "翻转绕序" } } },
            { "rect_opt", new Dictionary<Lang, string> { { Lang.EN, "Rectangle Optimization" }, { Lang.ZH, "矩形优化" } } },
            { "rect_tolerance", new Dictionary<Lang, string> { { Lang.EN, "Rect Tolerance (|VLeft|-|VUp|)" }, { Lang.ZH, "矩形容差（|VLeft|-|VUp|）" } } },
            { "build_update", new Dictionary<Lang, string> { { Lang.EN, "Build / Update" }, { Lang.ZH, "构建 / 更新" } } },
            { "clear", new Dictionary<Lang, string> { { Lang.EN, "Clear" }, { Lang.ZH, "清除" } } },

            // OBJ model / OBJ 模型
            { "obj_model", new Dictionary<Lang, string> { { Lang.EN, "OBJ Model" }, { Lang.ZH, "OBJ 模型" } } },
            { "obj_path", new Dictionary<Lang, string> { { Lang.EN, "OBJ Path" }, { Lang.ZH, "OBJ 路径" } } },
            { "browse", new Dictionary<Lang, string> { { Lang.EN, "Browse…" }, { Lang.ZH, "浏览…" } } },
            { "force_fallback", new Dictionary<Lang, string> { { Lang.EN, "Force Fallback Color" }, { Lang.ZH, "强制回退色" } } },
            { "load_build", new Dictionary<Lang, string> { { Lang.EN, "Load & Build" }, { Lang.ZH, "加载并构建" } } },
            { "building", new Dictionary<Lang, string> { { Lang.EN, "Building…" }, { Lang.ZH, "构建中…" } } },
            { "cancel", new Dictionary<Lang, string> { { Lang.EN, "Cancel" }, { Lang.ZH, "取消" } } },
            { "triangles_built", new Dictionary<Lang, string> { { Lang.EN, "Triangles (built/total)" }, { Lang.ZH, "三角形（已构建/总数）" } } },
            { "err_no_obj_path", new Dictionary<Lang, string> { { Lang.EN, "Enter an OBJ path or file name first." }, { Lang.ZH, "请先输入 OBJ 路径或文件名。" } } },

            // Statistics / 统计
            { "statistics", new Dictionary<Lang, string> { { Lang.EN, "Statistics" }, { Lang.ZH, "统计" } } },
            { "nothing_built", new Dictionary<Lang, string> { { Lang.EN, "Nothing built yet." }, { Lang.ZH, "尚未构建。" } } },
            { "paras", new Dictionary<Lang, string> { { Lang.EN, "Parallelograms" }, { Lang.ZH, "平行四边形" } } },
            { "quads", new Dictionary<Lang, string> { { Lang.EN, "Visible Quads (primitives)" }, { Lang.ZH, "可见 Quad（primitive）" } } },
            { "rectangles", new Dictionary<Lang, string> { { Lang.EN, "Rectangles (no parent)" }, { Lang.ZH, "矩形（无父级）" } } },
            { "stretches", new Dictionary<Lang, string> { { Lang.EN, "Stretches (invisible)" }, { Lang.ZH, "Stretch（不可见）" } } },
            { "reparented", new Dictionary<Lang, string> { { Lang.EN, "Reparented (V3)" }, { Lang.ZH, "重挂数（V3）" } } },
            { "stretches_saved", new Dictionary<Lang, string> { { Lang.EN, "Stretches saved" }, { Lang.ZH, "节省 Stretch" } } },
            { "total_blocks", new Dictionary<Lang, string> { { Lang.EN, "Total spawned blocks" }, { Lang.ZH, "生成总块数" } } },

            // Settings / 设置
            { "color", new Dictionary<Lang, string> { { Lang.EN, "Color" }, { Lang.ZH, "颜色" } } },
            { "accuracy", new Dictionary<Lang, string> { { Lang.EN, "Accuracy (V2/V3, world units)" }, { Lang.ZH, "精度（V2/V3，世界单位）" } } },
            { "opt_passes", new Dictionary<Lang, string> { { Lang.EN, "Optimization Passes (V3)" }, { Lang.ZH, "优化轮数（V3）" } } },
            { "input", new Dictionary<Lang, string> { { Lang.EN, "Input" }, { Lang.ZH, "输入" } } },
            { "use_move_tool", new Dictionary<Lang, string> { { Lang.EN, "Use Move Tool (W)" }, { Lang.ZH, "使用移动工具（W）" } } },
            { "use_editor_snap", new Dictionary<Lang, string> { { Lang.EN, "Use Editor Snap Settings" }, { Lang.ZH, "使用编辑器吸附设定" } } },
            { "snap_grid", new Dictionary<Lang, string> { { Lang.EN, "Snap Grid Size" }, { Lang.ZH, "吸附网格尺寸" } } },
            { "snap_inc", new Dictionary<Lang, string> { { Lang.EN, "Snap Increment (Ctrl)" }, { Lang.ZH, "吸附增量（Ctrl）" } } },
            { "face_color", new Dictionary<Lang, string> { { Lang.EN, "Face Color (default)" }, { Lang.ZH, "面颜色（默认）" } } },
            { "fallback_color", new Dictionary<Lang, string> { { Lang.EN, "Fallback Color (OBJ)" }, { Lang.ZH, "回退色（OBJ）" } } },
            { "collidable", new Dictionary<Lang, string> { { Lang.EN, "Collidable (visible quads)" }, { Lang.ZH, "可碰撞（可见 Quad）" } } },
            { "language", new Dictionary<Lang, string> { { Lang.EN, "Language" }, { Lang.ZH, "语言" } } },
            { "reset_default", new Dictionary<Lang, string> { { Lang.EN, "Reset to Default" }, { Lang.ZH, "重置为默认值" } } },
            { "ok", new Dictionary<Lang, string> { { Lang.EN, "OK" }, { Lang.ZH, "确定" } } },
        };

        public static void SetLanguage(Lang lang) { Current = lang; }

        /// <summary>Fetch a translation; falls back to the key when missing. / 获取翻译；缺失时回退为键名。</summary>
        public static string T(string key)
        {
            if (Data.TryGetValue(key, out Dictionary<Lang, string> entry) &&
                entry.TryGetValue(Current, out string text))
                return text;
            return key;
        }

        /// <summary>Fetch a translation and format it with args. / 获取翻译并用参数格式化。</summary>
        public static string T(string key, params object[] args) => string.Format(T(key), args);
    }
}
