using System;
using UnityEngine.SceneManagement;

namespace ToolLib
{
    /// <summary>
    /// Name de-duplication helpers shared by tools (curve / triangle). A name is made unique by appending
    /// an incrementing counter when a collision is found, matching the curve tool's existing scheme
    /// ("NewCurve", "NewCurve1", ...). Used both for in-list names (faces/curves) and for generated
    /// GameObject root names.
    /// 工具共用的名字去重辅助（曲线/三角面）。遇重名时追加递增计数使其唯一，与曲线工具现有方案一致
    /// （"NewCurve"、"NewCurve1"...）。用于列表内名称（面/曲线）与生成的 GameObject 根名。
    /// </summary>
    public static class NameUtil
    {
        /// <summary>
        /// Makes baseName unique against an existing-check predicate (returns the first collision-free name).
        /// 使 baseName 对给定的是否已存在判定唯一（返回第一个不冲突的名字）。
        /// </summary>
        public static string Deduplicate(string baseName, Func<string, bool> nameExists)
        {
            string finalName = string.IsNullOrEmpty(baseName) ? "New" : baseName;
            int dedup = 1;
            while (nameExists(finalName))
                finalName = $"{baseName}{dedup++}";
            return finalName;
        }

        /// <summary>
        /// Makes baseName unique against the active scene's root GameObjects (for generated parent objects).
        /// 使 baseName 对当前场景根 GameObject 唯一（用于生成的父对象）。
        /// </summary>
        public static string DeduplicateObjectName(string baseName)
        {
            return Deduplicate(baseName, n =>
            {
                var scene = SceneManager.GetActiveScene();
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == n) return true;
                return false;
            });
        }
    }
}
