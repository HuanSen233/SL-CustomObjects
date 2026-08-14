using System.Collections.Generic;

/// <summary>
/// 简易多语言管理器。通过 SetLanguage 切换，T(key) 获取翻译。
/// 编辑 L10nData 字典即可新增/修改翻译。
/// </summary>
public static class L10n
{
    public enum Lang { EN, ZH }

    public static Lang Current { get; private set; } = Lang.EN;

    private static readonly Dictionary<string, Dictionary<Lang, string>> Data = new()
    {
        { "window_title",       new() { [Lang.EN] = "Curve Tool",      [Lang.ZH] = "曲线工具" } },
        { "edit_tab",           new() { [Lang.EN] = "Edit",                       [Lang.ZH] = "编辑" } },
        { "settings_tab",       new() { [Lang.EN] = "Settings",                   [Lang.ZH] = "设置" } },
        { "curve_edit",         new() { [Lang.EN] = "Curve Edit",                 [Lang.ZH] = "曲线编辑" } },
        { "preview",            new() { [Lang.EN] = "Preview",                    [Lang.ZH] = "预览" } },
        { "handle_control",     new() { [Lang.EN] = "Handle Properties",         [Lang.ZH] = "控制柄属性" } },
        { "handle_type",        new() { [Lang.EN] = "Handle Type",                [Lang.ZH] = "控制柄类型" } },
        { "no_vertex",          new() { [Lang.EN] = "(No vertex selected)",       [Lang.ZH] = "（未选中顶点）" } },
        { "curve_list",         new() { [Lang.EN] = "Curve List",                 [Lang.ZH] = "曲线列表" } },
        { "curve_tools",        new() { [Lang.EN] = "Curve Tools",                [Lang.ZH] = "曲线工具" } },
        { "copy_curve",         new() { [Lang.EN] = "Copy",                       [Lang.ZH] = "复制" } },
        { "cursor_props",       new() { [Lang.EN] = "Cursor Properties",          [Lang.ZH] = "游标属性" } },
        { "cursor_pos",         new() { [Lang.EN] = "Cursor Position",            [Lang.ZH] = "游标位置" } },
        { "reset_pos",          new() { [Lang.EN] = "Reset Position",             [Lang.ZH] = "重置位置" } },
        { "lock_cursor",        new() { [Lang.EN] = "Lock Cursor",                [Lang.ZH] = "锁定游标" } },
        { "seg_props",          new() { [Lang.EN] = "Segment Properties",         [Lang.ZH] = "选中段属性" } },
        { "no_segment",         new() { [Lang.EN] = "(No segment selected)",      [Lang.ZH] = "（未选中任何段）" } },
        { "abs_scale",          new() { [Lang.EN] = "Base Scale",                 [Lang.ZH] = "基础缩放" } },
        { "fit_length",         new() { [Lang.EN] = "Fit Segment Length",         [Lang.ZH] = "适应段长" } },
        { "gen_primitive",      new() { [Lang.EN] = "Primitive",                  [Lang.ZH] = "生成物体" } },
        { "rel_scale",          new() { [Lang.EN] = "Relative Scale",             [Lang.ZH] = "相对缩放" } },
        { "rot_offset",         new() { [Lang.EN] = "Rotation Offset",            [Lang.ZH] = "旋转偏移" } },
        { "center_offset",      new() { [Lang.EN] = "Center Offset",              [Lang.ZH] = "中心点偏移" } },
        { "pos_offset",         new() { [Lang.EN] = "Position Offset",            [Lang.ZH] = "位置偏移" } },
        { "gen_object",         new() { [Lang.EN] = "Generate Objects",           [Lang.ZH] = "物体生成" } },
        { "gen_from_curve",     new() { [Lang.EN] = "Generate from Curve",        [Lang.ZH] = "从选中曲线生成物体" } },
        { "sel_enabled_curve",  new() { [Lang.EN] = "Select an enabled curve",    [Lang.ZH] = "请选中一条已启用的曲线" } },
        { "del_all_curves",     new() { [Lang.EN] = "Delete All Curves",          [Lang.ZH] = "删除全部曲线" } },
        { "del_confirm",        new() { [Lang.EN] = "Delete all {0} curves?",     [Lang.ZH] = "确定删除全部 {0} 条曲线？" } },
        { "ok",                 new() { [Lang.EN] = "OK",                         [Lang.ZH] = "确定" } },
        { "error",              new() { [Lang.EN] = "Error",                      [Lang.ZH] = "错误" } },
        { "ht_auto",            new() { [Lang.EN] = "Auto",                       [Lang.ZH] = "自动" } },
        { "ht_aligned",         new() { [Lang.EN] = "Aligned",                    [Lang.ZH] = "对齐" } },
        { "ht_aligned_length",  new() { [Lang.EN] = "Mirror",                      [Lang.ZH] = "镜像" } },
        { "ht_vector",          new() { [Lang.EN] = "Vector",                     [Lang.ZH] = "矢量" } },
        { "ht_free",            new() { [Lang.EN] = "Free",                       [Lang.ZH] = "自由" } },
        { "confirm",            new() { [Lang.EN] = "Confirm",                    [Lang.ZH] = "确认" } },
        { "cancel",             new() { [Lang.EN] = "Cancel",                     [Lang.ZH] = "取消" } },
        { "size",               new() { [Lang.EN] = "Size",                       [Lang.ZH] = "尺寸" } },
        { "vertex_size",        new() { [Lang.EN] = "Vertex Size",                [Lang.ZH] = "曲线顶点尺寸" } },
        { "handle_size",        new() { [Lang.EN] = "Handle Size",               [Lang.ZH] = "控制柄尺寸" } },
        { "arrow_size",         new() { [Lang.EN] = "Arrow Size",                  [Lang.ZH] = "箭头尺寸" } },
        { "language",           new() { [Lang.EN] = "Language",                   [Lang.ZH] = "语言" } },
        { "reset_default",      new() { [Lang.EN] = "Reset to Default",           [Lang.ZH] = "重置为默认值" } },
        { "err_no_curve",       new() { [Lang.EN] = "Invalid curve or insufficient vertices (need 2+)", [Lang.ZH] = "曲线无效或顶点不足（至少需要 2 个顶点）" } },
        { "err_no_samples",     new() { [Lang.EN] = "Insufficient sample points",    [Lang.ZH] = "采样点不足" } },
        { "gen_success",        new() { [Lang.EN] = "Generated {0} objects along \"{1}\"", [Lang.ZH] = "已沿曲线 \"{1}\" 生成 {0} 个物体" } },
        { "vertex_handle_props",new() { [Lang.EN] = "Vertex & Handle Properties",  [Lang.ZH] = "顶点与控制柄属性" } },
        { "vertex_position",    new() { [Lang.EN] = "Vertex Position",             [Lang.ZH] = "顶点位置" } },
        { "handle_type_a",      new() { [Lang.EN] = "Handle Type A (Left)",        [Lang.ZH] = "控制柄类型A（左柄）" } },
        { "handle_type_b",      new() { [Lang.EN] = "Handle Type B (Right)",       [Lang.ZH] = "控制柄类型B（右柄）" } },
        { "cursor_ref_frame",   new() { [Lang.EN] = "Cursor Reference Frame",      [Lang.ZH] = "游标参考系" } },
        { "flip",               new() { [Lang.EN] = "Flip",                         [Lang.ZH] = "翻转" } },
        { "mirror",             new() { [Lang.EN] = "Mirror",                       [Lang.ZH] = "镜像" } },
        { "handle_position",    new() { [Lang.EN] = "Handle Position",              [Lang.ZH] = "控制柄位置" } },
        { "left_handle",        new() { [Lang.EN] = "Left Handle Position",                  [Lang.ZH] = "左控制柄位置" } },
        { "right_handle",       new() { [Lang.EN] = "Right Handle Position",                 [Lang.ZH] = "右控制柄位置" } },
        { "snap_grid_size",     new() { [Lang.EN] = "Grid Size",                     [Lang.ZH] = "网格吸附步长" } },
        { "snap_increment",     new() { [Lang.EN] = "Increment Snap",                [Lang.ZH] = "增量吸附步长" } },
        { "snap_settings",      new() { [Lang.EN] = "Snapping",                      [Lang.ZH] = "吸附" } },
        { "color",              new() { [Lang.EN] = "Color",                         [Lang.ZH] = "颜色" } },
        { "vertex_pt",          new() { [Lang.EN] = "Vertex",                        [Lang.ZH] = "顶点" } },
        { "handle_end",         new() { [Lang.EN] = "Handle End",                    [Lang.ZH] = "控制柄端点" } },
        { "primitive_sphere",   new() { [Lang.EN] = "Sphere",                        [Lang.ZH] = "球体" } },
        { "primitive_capsule",  new() { [Lang.EN] = "Capsule",                       [Lang.ZH] = "胶囊体" } },
        { "primitive_cylinder", new() { [Lang.EN] = "Cylinder",                      [Lang.ZH] = "圆柱体" } },
        { "primitive_cube",     new() { [Lang.EN] = "Cube",                          [Lang.ZH] = "立方体" } },
        { "primitive_plane",    new() { [Lang.EN] = "Plane",                         [Lang.ZH] = "平面" } },
        { "primitive_quad",     new() { [Lang.EN] = "Quad",                          [Lang.ZH] = "四边形" } },
        { "reset_cursor_pos",   new() { [Lang.EN] = "Reset Cursor Position",         [Lang.ZH] = "重置游标位置" } },
        { "base_primitive",     new() { [Lang.EN] = "Base Primitive",                [Lang.ZH] = "基础物体" } },
        { "gen_color",          new() { [Lang.EN] = "Object Color",                  [Lang.ZH] = "物体颜色" } },
        { "generate",           new() { [Lang.EN] = "Generate",                      [Lang.ZH] = "生成物体" } },
        { "gen_parent_prefix",  new() { [Lang.EN] = "Curve Generated_",              [Lang.ZH] = "曲线生成_" } },
    };

    public static string T(string key)
    {
        if (Data.TryGetValue(key, out var dict) && dict.TryGetValue(Current, out var val))
            return val;
        return key;
    }

    public static string T(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    public static void SetLanguage(Lang lang)
    {
        Current = lang;
    }
}
