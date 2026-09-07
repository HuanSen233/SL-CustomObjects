using DONT_TOUCH.Scripts.BlockComponents;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// Factory that spawns the project's OWN block prefabs instead of built-in primitives,
    /// following the same pattern as CurvesTool (PrimitiveComponent.Create from
    /// Assets/Resources/Blocks/...). Empty containers come from Empty.prefab, faces from
    /// Primitives/Quad.prefab, markers from Primitives/Sphere.prefab.
    /// 项目自身块预制体工厂：用项目自己的 Block 预制体替代内置基本体，模式与 CurvesTool 一致
    /// （从 Assets/Resources/Blocks/... 经 PrimitiveComponent.Create 实例化）。
    /// 空容器用 Empty.prefab，面用 Primitives/Quad.prefab，标记用 Primitives/Sphere.prefab。
    /// </summary>
    public static class ProjectBlockFactory
    {
        public const string BlocksPath = "Assets/Resources/Blocks/";
        public const string PrimitivesPath = BlocksPath + "Primitives/";
        public const string EmptyPrefabPath = BlocksPath + "Empty.prefab";

        /// <summary>
        /// Create the project's Empty block (Transform-only container, no renderer).
        /// 创建项目的 Empty 块（仅 Transform 的容器，无渲染器）。
        /// </summary>
        public static EmptyComponent CreateEmpty(string objectName = null)
        {
            EmptyComponent comp = EmptyComponent.Create<EmptyComponent>(EmptyPrefabPath);
            if (comp == null)
            {
                Debug.LogError("[TriangleTool] Empty prefab not found: " + EmptyPrefabPath);
                return null;
            }

            if (!string.IsNullOrEmpty(objectName))
                comp.gameObject.name = objectName;

            return comp;
        }

        /// <summary>
        /// Create a project primitive block (Quad/Sphere/... prefab from Primitives folder)
        /// with the given color and visibility, like CurvesTool does.
        /// 创建项目基本体块（Primitives 目录下 Quad/Sphere/... 预制体）并设置颜色与可见性，同 CurvesTool。
        /// </summary>
        public static PrimitiveComponent CreatePrimitive(PrimitiveType type, string objectName = null,
            Color color = default(Color), bool visible = true, bool collidable = false)
        {
            string path = PrimitivesPath + type + ".prefab";
            PrimitiveComponent comp = PrimitiveComponent.Create<PrimitiveComponent>(path);
            if (comp == null)
            {
                Debug.LogError("[TriangleTool] Primitive prefab not found: " + path);
                return null;
            }

            if (!string.IsNullOrEmpty(objectName))
                comp.gameObject.name = objectName;

            comp.Color = color;
            comp.Visible = visible;
            comp.Collidable = collidable;
            return comp;
        }
    }
}
