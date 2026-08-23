using System.Collections.Generic;
using ToolLib;

/// <summary>
/// CurvesTool localization facade — delegates to the shared registry-style ToolL10n (scope "curve").
/// Public API kept compatible: L10n.T / L10n.Current / L10n.Lang / L10n.SetLanguage.
/// CurvesTool 本地化门面 —— 委托共享的注册表式 ToolL10n（scope "curve"）。
/// 公共 API 保持兼容：L10n.T / L10n.Current / L10n.Lang / L10n.SetLanguage。
/// Add or edit translations by editing the Data dictionary.
/// 编辑 Data 字典即可新增/修改翻译。
/// </summary>
public static class L10n
{
    /// <summary>Compatibility enum (values match ToolL10n.Lang). / 兼容枚举（值与 ToolL10n.Lang 一致）</summary>
    public enum Lang { EN, ZH }

    public static Lang Current => (Lang)(int)ToolL10n.Current;

    private static readonly Dictionary<string, Dictionary<ToolL10n.Lang, string>> Data = new()
    {
        { "window_title",       new() { [ToolL10n.Lang.EN] = "Curve Tool",      [ToolL10n.Lang.ZH] = "曲线工具" } },
        { "edit_tab",           new() { [ToolL10n.Lang.EN] = "Edit",                       [ToolL10n.Lang.ZH] = "编辑" } },
        { "settings_tab",       new() { [ToolL10n.Lang.EN] = "Settings",                   [ToolL10n.Lang.ZH] = "设置" } },
        { "curve_edit",         new() { [ToolL10n.Lang.EN] = "Curve Edit",                 [ToolL10n.Lang.ZH] = "曲线编辑" } },
        { "preview",            new() { [ToolL10n.Lang.EN] = "Preview",                    [ToolL10n.Lang.ZH] = "预览" } },
        { "preview_off",        new() { [ToolL10n.Lang.EN] = "Off",                        [ToolL10n.Lang.ZH] = "关" } },
        { "preview_wire",       new() { [ToolL10n.Lang.EN] = "Wireframe",                  [ToolL10n.Lang.ZH] = "线框" } },
        { "preview_triangles",  new() { [ToolL10n.Lang.EN] = "Triangles",                  [ToolL10n.Lang.ZH] = "三角面" } },
        { "handle_type",        new() { [ToolL10n.Lang.EN] = "Handle Type",                [ToolL10n.Lang.ZH] = "控制柄类型" } },
        { "curve_list",         new() { [ToolL10n.Lang.EN] = "Curve List",                 [ToolL10n.Lang.ZH] = "曲线列表" } },
        { "transform",          new() { [ToolL10n.Lang.EN] = "Transform",                   [ToolL10n.Lang.ZH] = "变换" } },
        { "cursor_section",     new() { [ToolL10n.Lang.EN] = "Cursor",                      [ToolL10n.Lang.ZH] = "游标" } },
        { "curve_row_legend",   new() { [ToolL10n.Lang.EN] = "○ select · D display · E enable · L lock · R loop · C copy · ✕ delete", [ToolL10n.Lang.ZH] = "○ 选择 · D 显示 · E 启用 · L 锁定 · R 闭环 · C 复制 · ✕ 删除" } },
        { "cursor_props",       new() { [ToolL10n.Lang.EN] = "Cursor Properties",          [ToolL10n.Lang.ZH] = "游标属性" } },
        { "lock_cursor",        new() { [ToolL10n.Lang.EN] = "Lock Cursor",                [ToolL10n.Lang.ZH] = "锁定游标" } },
        { "seg_props",          new() { [ToolL10n.Lang.EN] = "Segment Properties",         [ToolL10n.Lang.ZH] = "选中段属性" } },
        { "abs_scale",          new() { [ToolL10n.Lang.EN] = "Base Scale",                 [ToolL10n.Lang.ZH] = "基础缩放" } },
        { "fit_length",         new() { [ToolL10n.Lang.EN] = "Fit Segment Length",         [ToolL10n.Lang.ZH] = "适应段长" } },
        { "rel_scale",          new() { [ToolL10n.Lang.EN] = "Relative Scale",             [ToolL10n.Lang.ZH] = "相对缩放" } },
        { "rot_offset",         new() { [ToolL10n.Lang.EN] = "Rotation Offset",            [ToolL10n.Lang.ZH] = "旋转偏移" } },
        { "center_offset",      new() { [ToolL10n.Lang.EN] = "Center Offset",              [ToolL10n.Lang.ZH] = "中心点偏移" } },
        { "pos_offset",         new() { [ToolL10n.Lang.EN] = "Position Offset",            [ToolL10n.Lang.ZH] = "位置偏移" } },
        { "gen_object",         new() { [ToolL10n.Lang.EN] = "Generate Objects",           [ToolL10n.Lang.ZH] = "物体生成" } },
        { "sel_enabled_curve",  new() { [ToolL10n.Lang.EN] = "Select an enabled curve",    [ToolL10n.Lang.ZH] = "请选中一条已启用的曲线" } },
        { "del_all_curves",     new() { [ToolL10n.Lang.EN] = "Delete All Curves",          [ToolL10n.Lang.ZH] = "删除全部曲线" } },
        { "del_confirm",        new() { [ToolL10n.Lang.EN] = "Delete all {0} curves?",     [ToolL10n.Lang.ZH] = "确定删除全部 {0} 条曲线？" } },
        { "ok",                 new() { [ToolL10n.Lang.EN] = "OK",                         [ToolL10n.Lang.ZH] = "确定" } },
        { "error",              new() { [ToolL10n.Lang.EN] = "Error",                      [ToolL10n.Lang.ZH] = "错误" } },
        { "ht_auto",            new() { [ToolL10n.Lang.EN] = "Auto",                       [ToolL10n.Lang.ZH] = "自动" } },
        { "ht_aligned",         new() { [ToolL10n.Lang.EN] = "Aligned",                    [ToolL10n.Lang.ZH] = "对齐" } },
        { "ht_aligned_length",  new() { [ToolL10n.Lang.EN] = "Mirror",                      [ToolL10n.Lang.ZH] = "镜像" } },
        { "ht_vector",          new() { [ToolL10n.Lang.EN] = "Vector",                     [ToolL10n.Lang.ZH] = "矢量" } },
        { "ht_free",            new() { [ToolL10n.Lang.EN] = "Free",                       [ToolL10n.Lang.ZH] = "自由" } },
        { "confirm",            new() { [ToolL10n.Lang.EN] = "Confirm",                    [ToolL10n.Lang.ZH] = "确认" } },
        { "cancel",             new() { [ToolL10n.Lang.EN] = "Cancel",                     [ToolL10n.Lang.ZH] = "取消" } },
        { "size",               new() { [ToolL10n.Lang.EN] = "Size",                       [ToolL10n.Lang.ZH] = "尺寸" } },
        { "vertex_size",        new() { [ToolL10n.Lang.EN] = "Vertex Size",                [ToolL10n.Lang.ZH] = "曲线顶点尺寸" } },
        { "handle_size",        new() { [ToolL10n.Lang.EN] = "Handle Size",               [ToolL10n.Lang.ZH] = "控制柄尺寸" } },
        { "arrow_size",         new() { [ToolL10n.Lang.EN] = "Arrow Size",                  [ToolL10n.Lang.ZH] = "箭头尺寸" } },
        { "language",           new() { [ToolL10n.Lang.EN] = "Language",                   [ToolL10n.Lang.ZH] = "语言" } },
        { "reset_default",      new() { [ToolL10n.Lang.EN] = "Reset to Default",           [ToolL10n.Lang.ZH] = "重置为默认值" } },
        { "err_no_curve",       new() { [ToolL10n.Lang.EN] = "Invalid curve or insufficient vertices (need 2+)", [ToolL10n.Lang.ZH] = "曲线无效或顶点不足（至少需要 2 个顶点）" } },
        { "err_no_samples",     new() { [ToolL10n.Lang.EN] = "Insufficient sample points",    [ToolL10n.Lang.ZH] = "采样点不足" } },
        { "gen_success",        new() { [ToolL10n.Lang.EN] = "Generated {0} objects along \"{1}\"", [ToolL10n.Lang.ZH] = "已沿曲线 \"{1}\" 生成 {0} 个物体" } },
        { "vertex_handle_props",new() { [ToolL10n.Lang.EN] = "Selected Vertex & Handle Properties",  [ToolL10n.Lang.ZH] = "选中顶点与控制柄属性" } },
        { "vertex_position",    new() { [ToolL10n.Lang.EN] = "Vertex Position",             [ToolL10n.Lang.ZH] = "顶点位置" } },
        { "cursor_ref_frame",   new() { [ToolL10n.Lang.EN] = "Cursor Reference Frame",      [ToolL10n.Lang.ZH] = "游标参考系" } },
        { "flip",               new() { [ToolL10n.Lang.EN] = "Flip",                         [ToolL10n.Lang.ZH] = "翻转" } },
        { "mirror",             new() { [ToolL10n.Lang.EN] = "Mirror",                       [ToolL10n.Lang.ZH] = "镜像" } },
        { "left_handle",        new() { [ToolL10n.Lang.EN] = "Left Handle Position",                  [ToolL10n.Lang.ZH] = "左控制柄位置" } },
        { "right_handle",       new() { [ToolL10n.Lang.EN] = "Right Handle Position",                 [ToolL10n.Lang.ZH] = "右控制柄位置" } },
        { "snap_grid_size",     new() { [ToolL10n.Lang.EN] = "Grid Size",                     [ToolL10n.Lang.ZH] = "网格吸附步长" } },
        { "snap_increment",     new() { [ToolL10n.Lang.EN] = "Increment Snap",                [ToolL10n.Lang.ZH] = "增量吸附步长" } },
        { "snap_settings",      new() { [ToolL10n.Lang.EN] = "Snapping",                      [ToolL10n.Lang.ZH] = "吸附" } },
        { "snap_use_editor",    new() { [ToolL10n.Lang.EN] = "Use Editor Snap Settings",      [ToolL10n.Lang.ZH] = "使用编辑器吸附设定" } },
        { "snap_use_editor_tip",new() { [ToolL10n.Lang.EN] = "Follow Unity's snap toggle (Scene View Grid Snap) and Edit > Snap Settings increments; disable to use tool-local values.", [ToolL10n.Lang.ZH] = "跟随 Unity 编辑器吸附开关（场景视图网格吸附按钮）与 Edit > Snap Settings 步长；关闭后可自定义工具自身步长。" } },
        { "snap_editor_ctrl",   new() { [ToolL10n.Lang.EN] = "Editor",                           [ToolL10n.Lang.ZH] = "编辑器" } },
        { "snap_tool_ctrl",     new() { [ToolL10n.Lang.EN] = "Tool",                              [ToolL10n.Lang.ZH] = "工具" } },
        { "color",              new() { [ToolL10n.Lang.EN] = "Color",                         [ToolL10n.Lang.ZH] = "颜色" } },
        { "vertex_pt",          new() { [ToolL10n.Lang.EN] = "Vertex",                        [ToolL10n.Lang.ZH] = "顶点" } },
        { "handle_end",         new() { [ToolL10n.Lang.EN] = "Handle End",                    [ToolL10n.Lang.ZH] = "控制柄端点" } },
        { "primitive_sphere",   new() { [ToolL10n.Lang.EN] = "Sphere",                        [ToolL10n.Lang.ZH] = "球体" } },
        { "primitive_capsule",  new() { [ToolL10n.Lang.EN] = "Capsule",                       [ToolL10n.Lang.ZH] = "胶囊体" } },
        { "primitive_cylinder", new() { [ToolL10n.Lang.EN] = "Cylinder",                      [ToolL10n.Lang.ZH] = "圆柱体" } },
        { "primitive_cube",     new() { [ToolL10n.Lang.EN] = "Cube",                          [ToolL10n.Lang.ZH] = "立方体" } },
        { "primitive_plane",    new() { [ToolL10n.Lang.EN] = "Plane",                         [ToolL10n.Lang.ZH] = "平面" } },
        { "primitive_quad",     new() { [ToolL10n.Lang.EN] = "Quad",                          [ToolL10n.Lang.ZH] = "四边形" } },
        { "reset_cursor_pos",   new() { [ToolL10n.Lang.EN] = "Reset Cursor Position",         [ToolL10n.Lang.ZH] = "重置游标位置" } },
        { "base_primitive",     new() { [ToolL10n.Lang.EN] = "Base Primitive",                [ToolL10n.Lang.ZH] = "基础物体" } },
        { "gen_color",          new() { [ToolL10n.Lang.EN] = "Object Color",                  [ToolL10n.Lang.ZH] = "物体颜色" } },
        { "generate",           new() { [ToolL10n.Lang.EN] = "Generate",                      [ToolL10n.Lang.ZH] = "生成物体" } },
        { "gen_parent_prefix",  new() { [ToolL10n.Lang.EN] = "Curve Generated_",              [ToolL10n.Lang.ZH] = "曲线生成_" } },
    };

    static L10n() => ToolL10n.Register("curve", Data);

    public static string T(string key) => ToolL10n.T("curve", key);

    public static string T(string key, params object[] args) => ToolL10n.T("curve", key, args);

    public static void SetLanguage(Lang lang) => ToolL10n.SetLanguage((ToolL10n.Lang)(int)lang);
}
