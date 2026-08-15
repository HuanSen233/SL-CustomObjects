using System.Collections.Generic;

/// <summary>
/// Simple localization manager. Switch via SetLanguage, fetch translations with T(key).
/// 简易多语言管理器。通过 SetLanguage 切换，T(key) 获取翻译。
/// Add or edit translations by editing the L10nData dictionary.
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
        { "handle_type",        new() { [Lang.EN] = "Handle Type",                [Lang.ZH] = "控制柄类型" } },
        { "curve_list",         new() { [Lang.EN] = "Curve List",                 [Lang.ZH] = "曲线列表" } },
        { "transform",          new() { [Lang.EN] = "Transform",                   [Lang.ZH] = "变换" } },
        { "cursor_section",     new() { [Lang.EN] = "Cursor",                      [Lang.ZH] = "游标" } },
        { "curve_row_legend",   new() { [Lang.EN] = "○ select · D display · L lock · R loop · C copy · ✕ delete", [Lang.ZH] = "○ 选择 · D 显示 · L 锁定 · R 闭环 · C 复制 · ✕ 删除" } },
        { "cursor_props",       new() { [Lang.EN] = "Cursor Properties",          [Lang.ZH] = "游标属性" } },
        { "lock_cursor",        new() { [Lang.EN] = "Lock Cursor",                [Lang.ZH] = "锁定游标" } },
        { "seg_props",          new() { [Lang.EN] = "Segment Properties",         [Lang.ZH] = "选中段属性" } },
        { "abs_scale",          new() { [Lang.EN] = "Base Scale",                 [Lang.ZH] = "基础缩放" } },
        { "fit_length",         new() { [Lang.EN] = "Fit Segment Length",         [Lang.ZH] = "适应段长" } },
        { "rel_scale",          new() { [Lang.EN] = "Relative Scale",             [Lang.ZH] = "相对缩放" } },
        { "rot_offset",         new() { [Lang.EN] = "Rotation Offset",            [Lang.ZH] = "旋转偏移" } },
        { "center_offset",      new() { [Lang.EN] = "Center Offset",              [Lang.ZH] = "中心点偏移" } },
        { "pos_offset",         new() { [Lang.EN] = "Position Offset",            [Lang.ZH] = "位置偏移" } },
        { "gen_object",         new() { [Lang.EN] = "Generate Objects",           [Lang.ZH] = "物体生成" } },
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
        { "vertex_handle_props",new() { [Lang.EN] = "Selected Vertex & Handle Properties",  [Lang.ZH] = "选中顶点与控制柄属性" } },
        { "vertex_position",    new() { [Lang.EN] = "Vertex Position",             [Lang.ZH] = "顶点位置" } },
        { "cursor_ref_frame",   new() { [Lang.EN] = "Cursor Reference Frame",      [Lang.ZH] = "游标参考系" } },
        { "flip",               new() { [Lang.EN] = "Flip",                         [Lang.ZH] = "翻转" } },
        { "mirror",             new() { [Lang.EN] = "Mirror",                       [Lang.ZH] = "镜像" } },
        { "left_handle",        new() { [Lang.EN] = "Left Handle Position",                  [Lang.ZH] = "左控制柄位置" } },
        { "right_handle",       new() { [Lang.EN] = "Right Handle Position",                 [Lang.ZH] = "右控制柄位置" } },
        { "snap_grid_size",     new() { [Lang.EN] = "Grid Size",                     [Lang.ZH] = "网格吸附步长" } },
        { "snap_increment",     new() { [Lang.EN] = "Increment Snap",                [Lang.ZH] = "增量吸附步长" } },
        { "snap_settings",      new() { [Lang.EN] = "Snapping",                      [Lang.ZH] = "吸附" } },
        { "snap_use_editor",    new() { [Lang.EN] = "Use Editor Snap Settings",      [Lang.ZH] = "使用编辑器吸附设定" } },
        { "snap_use_editor_tip",new() { [Lang.EN] = "Follow Unity's snap toggle (Scene View Grid Snap) and Edit > Snap Settings increments; disable to use tool-local values.", [Lang.ZH] = "跟随 Unity 编辑器吸附开关（场景视图网格吸附按钮）与 Edit > Snap Settings 步长；关闭后可自定义工具自身步长。" } },
        { "snap_editor_ctrl",   new() { [Lang.EN] = "Editor",                           [Lang.ZH] = "编辑器" } },
        { "snap_tool_ctrl",     new() { [Lang.EN] = "Tool",                              [Lang.ZH] = "工具" } },
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
