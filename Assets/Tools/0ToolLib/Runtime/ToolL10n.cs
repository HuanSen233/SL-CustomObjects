using System.Collections.Generic;

namespace ToolLib
{
    /// <summary>
    /// Registry-style multi-language manager shared by all tools under Assets/Tools.
    /// Each tool registers its own entry table under a scope; lookups go through T(scope, key).
    /// 注册表式多语言管理器（Assets/Tools 下各工具共用）：
    /// 每个工具以 scope 注册自己的词条表，通过 T(scope, key) 查询。
    /// </summary>
    public static class ToolL10n
    {
        /// <summary>Supported languages. / 支持的语言</summary>
        public enum Lang { EN, ZH }

        /// <summary>Currently active language (global). / 当前生效语言（全局）</summary>
        public static Lang Current { get; private set; } = Lang.EN;

        /// <summary>Scope → (key → (lang → text)). / 各作用域的词条表</summary>
        private static readonly Dictionary<string, Dictionary<string, Dictionary<Lang, string>>> Tables =
            new Dictionary<string, Dictionary<string, Dictionary<Lang, string>>>();

        /// <summary>Switches the active language globally. / 全局切换当前语言</summary>
        public static void SetLanguage(Lang lang) => Current = lang;

        /// <summary>Registers (or replaces) a tool's entry table under a scope.
        /// 注册（覆盖）一个工具的词条表到指定作用域</summary>
        public static void Register(string scope, Dictionary<string, Dictionary<Lang, string>> entries)
        {
            Tables[scope] = entries;
        }

        /// <summary>Looks up a key in the given scope (falls back to the key itself).
        /// 查询指定 scope 的词条（找不到时返回 key 本身）</summary>
        public static string T(string scope, string key)
        {
            if (Tables.TryGetValue(scope, out var table)
                && table.TryGetValue(key, out var pair)
                && pair.TryGetValue(Current, out var val))
                return val;
            return key;
        }

        /// <summary>Looks up a key and formats it with args. / 查询词条并用参数格式化</summary>
        public static string T(string scope, string key, params object[] args)
        {
            return string.Format(T(scope, key), args);
        }
    }
}
