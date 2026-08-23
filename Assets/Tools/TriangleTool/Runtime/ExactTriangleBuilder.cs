using System.Collections.Generic;
using DONT_TOUCH.Scripts.BlockComponents;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// V1 (Exact) assembly: builds the full quad hierarchy for a list of triangles.
    /// Every triangle becomes 3 parallelograms; each parallelogram is 2 Quads
    /// (base + visible child) unless the rectangle optimization kicks in (1 Quad).
    /// All built pieces are children of Root (a plain container GameObject).
    /// V1 (Exact) 装配：为三角形列表构建完整 Quad 层级。每个三角形变为 3 个平行四边形；
    /// 每个平行四边形默认 2 个 Quad（父 + 子），矩形优化生效时为 1 个 Quad。
    /// 所有构建物均为 Root（普通容器 GameObject）的子物体。
    ///
    /// Ported from TriangleScpSl / ExactModel + ModelBase (Foibos, CC-BY-SA 3.0).
    /// 移植自 TriangleScpSl / ExactModel + ModelBase（Foibos，CC-BY-SA 3.0）。
    /// </summary>
    public class ExactTriangleBuilder
    {
        readonly List<GameObject> _built = new List<GameObject>();

        /// <summary>Root container GameObject holding all quads / 容纳全部 Quad 的根容器。</summary>
        public GameObject Root { get; private set; }

        /// <summary>Number of parallelograms / 平行四边形数量。</summary>
        public int ParallelogramCount { get; private set; }

        /// <summary>Number of visible Quad blocks created (one per parallelogram) / 创建的可见 Quad 块数量（每平行四边形一个）。</summary>
        public int QuadCount { get; private set; }

        /// <summary>Number of parallelograms collapsed to a single Quad (no Empty parent) / 合并为单个 Quad 的矩形数（无 Empty 父对象）。</summary>
        public int RectangleCount { get; private set; }

        /// <summary>Total renderable primitives (visible Quad blocks only) / 可渲染 primitive 总数（仅可见 Quad 块）。</summary>
        public int PrimitiveCount => QuadCount;

        /// <summary>Total spawned block GameObjects = visible quads + empty parents + root / 生成的块总数 = 可见 Quad + Empty 父块 + 根。</summary>
        public int TotalBlockCount => QuadCount + (ParallelogramCount - RectangleCount) + (Root != null ? 1 : 0);

        /// <summary>
        /// Build a V1 Exact triangle model at world coordinates.
        /// 在世界坐标构建 V1 Exact 三角面模型。
        /// </summary>
        /// <param name="triangles">Triangles to render / 待渲染的三角形。</param>
        /// <param name="rootName">Root object name / 根对象名。</param>
        /// <param name="color">Face color / 面颜色。</param>
        /// <param name="material">Optional material override (null = colored unlit per quad) / 可选材质。</param>
        /// <param name="useRectangleOptimization">Merge rectangles into 1 Quad / 矩形合并为 1 个 Quad。</param>
        /// <param name="rectangleTolerance">Half-diagonal length tolerance for rectangle test / 矩形判定半对角线长度容差。</param>
        /// <param name="flipWinding">Flip triangle winding (render from the other side) / 翻转三角形绕序。</param>
        public static ExactTriangleBuilder Build(
            IList<Vector3[]> triangles,
            string rootName = "TriangleV1_Exact",
            Color? color = null,
            Material material = null,
            bool useRectangleOptimization = true,
            float rectangleTolerance = 1e-4f,
            bool flipWinding = false)
        {
            var builder = new ExactTriangleBuilder();
            builder.EnsureRoot(rootName);
            builder.BuildAll(triangles, color ?? Color.magenta, material, useRectangleOptimization, rectangleTolerance, flipWinding);
            return builder;
        }

        /// <summary>
        /// Build a V1 Exact model from a single triangle.
        /// 由单个三角形构建 V1 Exact 模型。
        /// </summary>
        public static ExactTriangleBuilder Build(
            Vector3 p1, Vector3 p2, Vector3 p3,
            string rootName = "TriangleV1_Exact",
            Color? color = null,
            Material material = null,
            bool useRectangleOptimization = true,
            float rectangleTolerance = 1e-4f,
            bool flipWinding = false)
        {
            return Build(new[] { new[] { p1, p2, p3 } }, rootName, color, material,
                useRectangleOptimization, rectangleTolerance, flipWinding);
        }

        /// <summary>
        /// Build a V1 Exact model from a list of triangles with per-triangle colors.
        /// 由带逐三角形颜色的列表构建 V1 Exact 模型。
        /// </summary>
        public static ExactTriangleBuilder Build(
            IList<TriangleData> triangles,
            string rootName = "TriangleV1_Exact",
            Material material = null,
            bool useRectangleOptimization = true,
            float rectangleTolerance = 1e-4f,
            bool flipWinding = false)
        {
            var builder = new ExactTriangleBuilder();
            builder.EnsureRoot(rootName);

            foreach (TriangleData tri in triangles)
                builder.BuildOneTriangle(tri, material, useRectangleOptimization, rectangleTolerance, flipWinding);

            return builder;
        }

        /// <summary>
        /// Destroy all built GameObjects (and the root).
        /// 销毁全部构建的 GameObject（含根）。
        /// </summary>
        public void Destroy()
        {
            foreach (GameObject go in _built)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            _built.Clear();

            if (Root != null)
                Object.Destroy(Root);
            Root = null;

            ParallelogramCount = 0;
            QuadCount = 0;
        }

        /// <summary>
        /// Destroy immediately (edit-time safe).
        /// 立即销毁（编辑器环境下安全）。
        /// </summary>
        public void DestroyImmediate()
        {
            foreach (GameObject go in _built)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            _built.Clear();

            if (Root != null)
                Object.DestroyImmediate(Root);
            Root = null;

            ParallelogramCount = 0;
            QuadCount = 0;
        }

        void BuildAll(IList<Vector3[]> triangles, Color color, Material material,
            bool useRectangleOptimization, float rectangleTolerance, bool flipWinding)
        {
            foreach (Vector3[] tri in triangles)
            {
                if (tri == null || tri.Length < 3)
                    continue;

                Vector3 p1 = tri[0];
                Vector3 p2 = tri[1];
                Vector3 p3 = tri[2];

                BuildOneTriangle(p1, p2, p3, color, material, useRectangleOptimization, rectangleTolerance, flipWinding);
            }
        }

        /// <summary>
        /// Ensure the root container exists (creates it lazily for batched builds).
        /// Uses the project's Empty block; falls back to a plain GameObject if the prefab is missing.
        /// 确保根容器存在（分帧构建时惰性创建）。使用项目 Empty 块；预制体缺失时回退普通 GameObject。
        /// </summary>
        public void EnsureRoot(string rootName = "TriangleV1_Exact")
        {
            if (Root != null)
                return;

            EmptyComponent empty = ProjectBlockFactory.CreateEmpty(rootName);
            Root = empty != null ? empty.gameObject : new GameObject(rootName);
        }

        /// <summary>
        /// Build one triangle into the existing root (used by batched OBJ builds).
        /// 把单个三角形构建进已有根（供分帧 OBJ 构建使用）。
        /// </summary>
        public void BuildOneTriangle(Vector3 p1, Vector3 p2, Vector3 p3, Color color,
            Material material = null, bool useRectangleOptimization = true,
            float rectangleTolerance = 1e-4f, bool flipWinding = false)
        {
            EnsureRoot();

            if (flipWinding)
            {
                Vector3 tmp = p2;
                p2 = p3;
                p3 = tmp;
            }

            ParallelogramData[] paras = TriangleParallelogramDecomposer.Decompose(p1, p2, p3);

            foreach (ParallelogramData para in paras)
            {
                ParallelogramCount++;

                bool isRect = useRectangleOptimization &&
                              TriangleParallelogramDecomposer.IsRectangle(para, rectangleTolerance);

                ParallelogramQuadResult result = isRect
                    ? ParallelogramQuadBuilder.BuildRectangle(para.VLeft, para.VUp, para.Center, color, material)
                    : ParallelogramQuadBuilder.Build(para.VUp, para.VLeft, para.Center, color, material);

                result.Base.transform.SetParent(Root.transform, true);
                _built.Add(result.Base);

                // The child was parented to Base already; keep a reference for cleanup only.
                // 子对象已挂到 Base 下；这里仅登记引用便于清理。
                if (result.Child != result.Base)
                    _built.Add(result.Child);

                // One visible Quad block per parallelogram; the parent is an Empty block
                // (no renderer), so it is not counted as a renderable primitive.
                // 每个平行四边形恰好一个可见 Quad 块；父对象是 Empty 块（无渲染器），不计入渲染 primitive。
                QuadCount++;
                if (result.UsedRectangleOptimization)
                    RectangleCount++;
            }
        }

        /// <summary>
        /// Build one triangle from TriangleData (per-triangle color support).
        /// 从 TriangleData 构建单个三角形（支持逐三角形颜色）。
        /// </summary>
        public void BuildOneTriangle(in TriangleData tri,
            Material material = null, bool useRectangleOptimization = true,
            float rectangleTolerance = 1e-4f, bool flipWinding = false)
        {
            BuildOneTriangle(tri.P1, tri.P2, tri.P3, tri.Color, material,
                useRectangleOptimization, rectangleTolerance, flipWinding);
        }
    }
}
