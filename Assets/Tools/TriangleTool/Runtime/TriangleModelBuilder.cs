using System.Collections.Generic;
using DONT_TOUCH.Scripts.BlockComponents;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// Build mode for the triangle renderer.
    /// 三角面渲染器的构建模式。
    /// </summary>
    public enum TriangleBuildMode
    {
        /// <summary>V1 Exact: every parallelogram = Empty base + Quad child (pixel-perfect) / 每个平行四边形 = Empty 父 + Quad 子（像素级精确）。</summary>
        Exact = 0,
        /// <summary>V2 Approximate: parallelograms share invisible stretch transforms via (theta, phi) clustering / 平行四边形通过 (theta, phi) 聚类共享不可见 stretch 变换。</summary>
        StretchClustered = 1,
        /// <summary>V3 Hierarchical: stretch children are re-parented onto visible quads / 把 stretch 子 Quad 重挂到可见 Quad 上。</summary>
        Hierarchical = 2,
    }

    /// <summary>
    /// Unified V1/V2/V3 triangle builder. Feeds triangles one by one (batched builds),
    /// then Finish() runs the post-processing phase (V2 stretch consolidation,
    /// V3 optimization sweeps + consolidation + unused-stretch cleanup).
    /// 统一 V1/V2/V3 三角形构建器。逐三角形喂入（分帧构建），随后 Finish() 运行后处理阶段
    /// （V2 stretch 合并；V3 优化扫描 + 合并 + 清理未用 stretch）。
    ///
    /// Ported from TriangleScpSl / ExactModel + ApproximateModel + HierarchicalModel
    /// (Foibos, CC-BY-SA 3.0).
    /// 移植自 TriangleScpSl / ExactModel + ApproximateModel + HierarchicalModel（Foibos，CC-BY-SA 3.0）。
    /// </summary>
    public class TriangleModelBuilder
    {
        // ---- config / 配置 ----
        public TriangleBuildMode Mode = TriangleBuildMode.Exact;
        public float Accuracy = 0.001f;      // world units tolerance for V2/V3 stretch reuse / V2/V3 拉伸复用的世界单位容差
        public int OptimizationPasses = 3;   // V3 sweep count / V3 优化扫描轮数
        public bool UseRectangleOptimization = true;
        public float RectangleTolerance = 1e-4f;
        public bool FlipWinding;
        public bool Collidable;              // visible quads / 可见 Quad

        // ---- state / 状态 ----
        /// <summary>Root container (project Empty block) / 根容器（项目 Empty 块）。</summary>
        public GameObject Root { get; private set; }

        readonly StretchSpatialIndex _stretches = new StretchSpatialIndex(0.05f, 0.1f);
        readonly List<GameObject> _parallelogramQuads = new List<GameObject>(); // rects, stretch children, hierarchical children
        readonly List<QuadInfo> _quadInfos = new List<QuadInfo>();              // 1:1 with _parallelogramQuads
        readonly List<ParallelogramQuadResult> _fallbacks = new List<ParallelogramQuadResult>(); // V1 pairs / exact fallbacks
        readonly Dictionary<int, int> _hierarchicalParents = new Dictionary<int, int>(); // V3
        readonly Dictionary<int, int> _hierarchyDepths = new Dictionary<int, int>();     // V3
        readonly HashSet<GameObject> _usedStretches = new HashSet<GameObject>();         // V3
        readonly List<GameObject> _all = new List<GameObject>(); // every spawned object for cleanup
        int _stretchesCreated;

        // ---- stats / 统计 ----
        public int ParallelogramCount { get; private set; }
        public int QuadCount => _parallelogramQuads.Count + _fallbacks.Count;
        public int RectangleCount { get; private set; }
        public int FallbackCount => _fallbacks.Count;
        public int StretchCount => _stretches.Count;
        public int ReparentedCount { get; private set; }
        public int StretchesSaved => ReparentedCount + (_stretchesCreated - _stretches.Count);
        public int TotalBlockCount =>
            (Root != null ? 1 : 0) + _stretches.Count + _parallelogramQuads.Count + _fallbacks.Count * 2;

        struct QuadInfo
        {
            public Vector3 VLeft;
            public Vector3 VUp;
            public Vector3 Center;
            public GameObject Stretch;

            public QuadInfo(Vector3 vLeft, Vector3 vUp, Vector3 center, GameObject stretch)
            {
                VLeft = vLeft;
                VUp = vUp;
                Center = center;
                Stretch = stretch;
            }
        }

        /// <summary>
        /// Ensure the root container exists (project Empty block, plain GameObject fallback).
        /// 确保根容器存在（项目 Empty 块，缺失时回退普通 GameObject）。
        /// </summary>
        public void EnsureRoot(string rootName = "TriangleV1_Exact")
        {
            if (Root != null)
                return;

            EmptyComponent empty = ProjectBlockFactory.CreateEmpty(rootName);
            Root = empty != null ? empty.gameObject : new GameObject(rootName);
        }

        /// <summary>
        /// Build one triangle (decomposed into 3 parallelograms).
        /// 构建一个三角形（分解为 3 个平行四边形）。
        /// </summary>
        public void BuildOneTriangle(in TriangleData tri)
        {
            Vector3 p1 = tri.P1;
            Vector3 p2 = tri.P2;
            Vector3 p3 = tri.P3;

            if (FlipWinding)
            {
                Vector3 tmp = p2;
                p2 = p3;
                p3 = tmp;
            }

            ParallelogramData[] paras = TriangleParallelogramDecomposer.Decompose(p1, p2, p3);

            foreach (ParallelogramData para in paras)
                BuildOneParallelogram(in para, tri.Color);
        }

        /// <summary>
        /// Run the mode-specific post-processing phase (consolidation / sweeps / cleanup).
        /// Call once after feeding all triangles.
        /// 运行模式相关的后处理阶段（合并 / 扫描 / 清理）。喂完所有三角形后调用一次。
        /// </summary>
        public void Finish()
        {
            if (Mode == TriangleBuildMode.StretchClustered)
            {
                ConsolidateStretches();
            }
            else if (Mode == TriangleBuildMode.Hierarchical)
            {
                RunOptimizationSweeps();
                ConsolidateStretches();
                MarkUsedStretches();
                DestroyUnusedStretches();
            }
        }

        // ---------- parallelogram creation ----------

        void BuildOneParallelogram(in ParallelogramData para, Color color)
        {
            ParallelogramCount++;

            bool isRect = UseRectangleOptimization &&
                          TriangleParallelogramDecomposer.IsRectangle(para, RectangleTolerance);

            if (isRect)
            {
                CreateRectangle(in para, color);
                return;
            }

            if (Mode == TriangleBuildMode.Exact)
            {
                CreateExactPair(in para, color);
                return;
            }

            CreateStretchOrHierarchical(in para, color);
        }

        void CreateRectangle(in ParallelogramData para, Color color)
        {
            // VLeft/VUp are half-diagonals; edges are (VLeft+VUp) and (VLeft-VUp).
            Vector3 edgeA = para.VLeft + para.VUp;
            Vector3 edgeB = para.VLeft - para.VUp;
            float width = edgeB.magnitude;
            float height = edgeA.magnitude;
            Vector3 forward = Vector3.Cross(edgeB, edgeA).normalized;

            if (forward.sqrMagnitude < 1e-6f || width < 1e-7f || height < 1e-7f)
            {
                // Degenerate rectangle -> route through the mode's parallelogram path.
                if (Mode == TriangleBuildMode.Exact)
                    CreateExactPair(in para, color);
                else
                    CreateStretchOrHierarchical(in para, color);
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(forward, edgeA.normalized);

            PrimitiveComponent quad = ProjectBlockFactory.CreatePrimitive(
                PrimitiveType.Quad, "RectangleQuad", color, visible: true, collidable: Collidable);
            if (quad == null)
                return;

            Transform t = quad.transform;
            t.SetParent(Root.transform, true);
            t.position = para.Center; // world center of the rectangle / 矩形世界中心
            t.rotation = rotation;
            t.localScale = new Vector3(width, height, 1f);

            RegisterQuad(quad.gameObject, in para, null, isRectangle: true);
        }

        /// <summary>V1 exact pair: Empty base (shear) + Quad child. / V1 精确对：Empty 父（剪切）+ Quad 子。</summary>
        void CreateExactPair(in ParallelogramData para, Color color)
        {
            ParallelogramQuadResult result = ParallelogramQuadBuilder.Build(
                para.VUp, para.VLeft, para.Center, color);

            if (result.Base == null)
                return;

            result.Base.transform.SetParent(Root.transform, true);
            _fallbacks.Add(result);
            _all.Add(result.Base);
            if (result.Child != result.Base)
                _all.Add(result.Child);
        }

        /// <summary>V2/V3 path: degenerate check, solver, V3 inline parenting, stretch clustering.
        /// V2/V3 路径：退化检查、角度求解、V3 内联父级、stretch 聚类。</summary>
        void CreateStretchOrHierarchical(in ParallelogramData para, Color color)
        {
            // Near-zero-area parallelograms (degenerate triangles produce them with sizable
            // diagonals) have no reliable orientation — skip.
            float sizeSqr = Mathf.Max(
                (para.VLeft + para.VUp).sqrMagnitude,
                (para.VLeft - para.VUp).sqrMagnitude);
            if (Vector3.Cross(para.VLeft, para.VUp).sqrMagnitude <= sizeSqr * 1e-8f)
                return;

            if (!VectorPhiSolver.TrySolve(para.VLeft, para.VUp, out float theta, out float phi))
            {
                CreateExactPair(in para, color); // exact fallback
                return;
            }

            // V3: try to parent directly under an existing visible quad (no stretch at all).
            if (Mode == TriangleBuildMode.Hierarchical && TryCreateUnderParent(in para, color))
                return;

            StretchSpatialIndex.Entry? best = StretchMath.FindBestStretch(
                _stretches, para.VLeft, para.VUp, theta, phi, Accuracy);

            GameObject stretch;
            float sT, sP;

            if (best != null)
            {
                stretch = best.Value.Stretch;
                sT = best.Value.Theta;
                sP = best.Value.Phi;
            }
            else
            {
                sT = theta;
                sP = phi;
                stretch = StretchMath.CreateStretch(sT, sP, Root.transform);
                if (stretch == null)
                {
                    CreateExactPair(in para, color);
                    return;
                }
                _stretches.Add(sT, sP, stretch);
                _stretchesCreated++;
                _all.Add(stretch);
            }

            Vector3 v1 = StretchMath.ForwardTransform(para.VLeft, sT, sP);
            Vector3 v2 = StretchMath.ForwardTransform(para.VUp, sT, sP);

            GameObject quad = StretchMath.CreateParallelogram(
                para.Center, v1, v2, stretch.transform, color, Collidable);

            if (quad == null)
            {
                CreateExactPair(in para, color);
                return;
            }

            RegisterQuad(quad, in para, stretch, isRectangle: false);
            if (Mode == TriangleBuildMode.Hierarchical)
                _hierarchyDepths[_parallelogramQuads.Count - 1] = 0;
        }

        void RegisterQuad(GameObject quad, in ParallelogramData para, GameObject stretch, bool isRectangle)
        {
            _parallelogramQuads.Add(quad);
            _quadInfos.Add(new QuadInfo(para.VLeft, para.VUp, para.Center, stretch));
            _all.Add(quad);
            if (isRectangle)
                RectangleCount++;
        }

        // ---------- V3: hierarchical parenting ----------

        bool TryCreateUnderParent(in ParallelogramData para, Color color)
        {
            int count = _parallelogramQuads.Count;
            if (count == 0)
                return false;

            Vector3 wc0 = para.Center + para.VUp, wc1 = para.Center + para.VLeft;
            Vector3 wc2 = para.Center - para.VUp, wc3 = para.Center - para.VLeft;

            int bestIdx = -1;
            float bestErr = float.MaxValue;
            Vector3 bestLp = default;
            Quaternion bestLr = default;
            Vector3 bestLs = default;

            for (int pi = 0; pi < count; pi++)
            {
                if (!IsStretchFreeInHierarchy(pi)) continue;

                if (TryFitUnderQuad(_parallelogramQuads[pi].transform, wc0, wc1, wc2, wc3,
                        out Vector3 lp, out Quaternion lr, out Vector3 ls, out float err) && err < bestErr)
                {
                    bestErr = err;
                    bestIdx = pi;
                    bestLp = lp;
                    bestLr = lr;
                    bestLs = ls;
                }
            }

            if (bestIdx < 0)
                return false;

            PrimitiveComponent quad = ProjectBlockFactory.CreatePrimitive(
                PrimitiveType.Quad, "HierarchicalChildQuad", color, visible: true, collidable: Collidable);
            if (quad == null)
                return false;

            Transform t = quad.transform;
            t.SetParent(_parallelogramQuads[bestIdx].transform, false);
            t.localPosition = bestLp;
            t.localRotation = bestLr;
            t.localScale = bestLs;

            int childIdx = _parallelogramQuads.Count;
            _parallelogramQuads.Add(quad.gameObject);
            _quadInfos.Add(new QuadInfo(para.VLeft, para.VUp, para.Center, null));
            _all.Add(quad.gameObject);
            _hierarchicalParents[childIdx] = bestIdx;
            ReparentedCount++;
            _hierarchyDepths[childIdx] = (_hierarchyDepths.TryGetValue(bestIdx, out int pd) ? pd : 0) + 1;
            return true;
        }

        bool IsStretchFreeInHierarchy(int idx)
        {
            int cur = idx;

            while (true)
            {
                if (_quadInfos[cur].Stretch != null)
                    return false;

                if (!_hierarchicalParents.TryGetValue(cur, out int parent))
                    return true;

                cur = parent;
            }
        }

        bool TryFitUnderQuad(Transform parentTransform,
            Vector3 wc0, Vector3 wc1, Vector3 wc2, Vector3 wc3,
            out Vector3 localPos, out Quaternion localRot, out Vector3 localScale, out float error)
        {
            localPos = Vector3.zero;
            localRot = Quaternion.identity;
            localScale = Vector3.one;
            error = float.MaxValue;

            Vector3 lc0 = parentTransform.InverseTransformPoint(wc0);
            Vector3 lc1 = parentTransform.InverseTransformPoint(wc1);
            Vector3 lc2 = parentTransform.InverseTransformPoint(wc2);
            Vector3 lc3 = parentTransform.InverseTransformPoint(wc3);

            Vector3 center = (lc0 + lc1 + lc2 + lc3) * 0.25f;
            Vector3 hd1 = lc0 - center, hd2 = lc1 - center;
            Vector3 e1 = hd1 + hd2, e2 = hd1 - hd2;
            float e1M = e1.magnitude, e2M = e2.magnitude;
            if (e1M < 1e-7f || e2M < 1e-7f) return false;

            Vector3 n = Vector3.Cross(e1, e2);
            if (n.sqrMagnitude < 1e-12f) return false;

            Quaternion rot = Quaternion.LookRotation(n.normalized, e2.normalized);
            var scale = new Vector3(e1M, e2M, 1f);

            float err = MeasureCornerError(parentTransform, center, rot, scale, wc0, wc1, wc2, wc3);
            if (err > Accuracy) return false;

            localPos = center;
            localRot = rot;
            localScale = scale;
            error = err;
            return true;
        }

        static float MeasureCornerError(Transform pt, Vector3 lp, Quaternion lr, Vector3 ls,
            Vector3 t0, Vector3 t1, Vector3 t2, Vector3 t3)
        {
            float max = 0f;

            for (int cx = -1; cx <= 1; cx += 2)
            {
                for (int cy = -1; cy <= 1; cy += 2)
                {
                    Vector3 lc = lp + lr * new Vector3(cx * ls.x * 0.5f, cy * ls.y * 0.5f, 0f);
                    Vector3 wc = pt.TransformPoint(lc);
                    float d0 = (wc - t0).sqrMagnitude, d1 = (wc - t1).sqrMagnitude;
                    float d2 = (wc - t2).sqrMagnitude, d3 = (wc - t3).sqrMagnitude;
                    float m = Mathf.Min(Mathf.Min(d0, d1), Mathf.Min(d2, d3));
                    if (m > max) max = m;
                }
            }

            return Mathf.Sqrt(max);
        }

        // ---------- V3 Phase 2: optimization sweeps ----------

        void RunOptimizationSweeps()
        {
            if (OptimizationPasses <= 0)
                return;

            // Seed: only quads that are stretch-free in their ancestry.
            var newCandidates = new HashSet<int>();
            for (int i = 0; i < _parallelogramQuads.Count; i++)
            {
                if (IsStretchFreeInHierarchy(i))
                    newCandidates.Add(i);
            }

            for (int pass = 0; pass < OptimizationPasses; pass++)
            {
                bool fullScan = pass == 0;

                var reparentedThisPass = new List<int>();
                var newParentsThisPass = new HashSet<int>();

                for (int ci = 0; ci < _parallelogramQuads.Count; ci++)
                {
                    // Only stretch-children are candidates for reparenting.
                    if (_hierarchicalParents.ContainsKey(ci)) continue;
                    if (_quadInfos[ci].Stretch == null) continue;

                    QuadInfo info = _quadInfos[ci];
                    Vector3 wc0 = info.Center + info.VUp, wc1 = info.Center + info.VLeft;
                    Vector3 wc2 = info.Center - info.VUp, wc3 = info.Center - info.VLeft;

                    int bestIdx = -1;
                    float bestErr = float.MaxValue;
                    Vector3 bestLp = default;
                    Quaternion bestLr = default;
                    Vector3 bestLs = default;

                    for (int pi = 0; pi < _parallelogramQuads.Count; pi++)
                    {
                        if (pi == ci) continue;
                        if (!IsStretchFreeInHierarchy(pi)) continue;
                        if (!fullScan && !newCandidates.Contains(pi)) continue;

                        if (TryFitUnderQuad(_parallelogramQuads[pi].transform, wc0, wc1, wc2, wc3,
                                out Vector3 lp, out Quaternion lr, out Vector3 ls, out float err) && err < bestErr)
                        {
                            bestErr = err;
                            bestIdx = pi;
                            bestLp = lp;
                            bestLr = lr;
                            bestLs = ls;
                        }
                    }

                    if (bestIdx < 0) continue;

                    // Reparent in place — no destroy, no recreate.
                    Transform t = _parallelogramQuads[ci].transform;
                    t.SetParent(_parallelogramQuads[bestIdx].transform, true);
                    t.localPosition = bestLp;
                    t.localRotation = bestLr;
                    t.localScale = bestLs;

                    _hierarchicalParents[ci] = bestIdx;
                    ReparentedCount++;
                    _hierarchyDepths[ci] = (_hierarchyDepths.TryGetValue(bestIdx, out int pd) ? pd : 0) + 1;
                    _quadInfos[ci] = new QuadInfo(info.VLeft, info.VUp, info.Center, null);

                    reparentedThisPass.Add(ci);
                    newParentsThisPass.Add(bestIdx);
                }

                if (reparentedThisPass.Count == 0)
                    break;

                // Next pass: only test newly-involved stretch-free quads.
                newCandidates = new HashSet<int>();

                foreach (int pi in newParentsThisPass)
                {
                    if (IsStretchFreeInHierarchy(pi))
                        newCandidates.Add(pi);
                }

                foreach (int ri in reparentedThisPass)
                {
                    if (IsStretchFreeInHierarchy(ri))
                        newCandidates.Add(ri);
                }
            }
        }

        // ---------- consolidation / cleanup ----------

        void ConsolidateStretches()
        {
            HashSet<GameObject> emptied = StretchMath.ConsolidateStretches(
                _stretches,
                _parallelogramQuads.Count,
                i => _quadInfos[i].Stretch,
                i => (_quadInfos[i].VLeft, _quadInfos[i].VUp),
                RehomeQuad,
                Accuracy);

            foreach (GameObject stretch in emptied)
            {
                _stretches.Remove(stretch);
                Object.DestroyImmediate(stretch);
            }
        }

        void RehomeQuad(int index, StretchSpatialIndex.Entry target)
        {
            QuadInfo info = _quadInfos[index];
            Vector3 v1 = StretchMath.ForwardTransform(info.VLeft, target.Theta, target.Phi);
            Vector3 v2 = StretchMath.ForwardTransform(info.VUp, target.Theta, target.Phi);
            StretchMath.ReparentToStretch(_parallelogramQuads[index].transform, target.Stretch.transform, v1, v2);
            _quadInfos[index] = new QuadInfo(info.VLeft, info.VUp, info.Center, target.Stretch);
        }

        void MarkUsedStretches()
        {
            _usedStretches.Clear();

            for (int i = 0; i < _parallelogramQuads.Count; i++)
            {
                if (_hierarchicalParents.ContainsKey(i)) continue;
                GameObject s = _quadInfos[i].Stretch;
                if (s != null)
                    _usedStretches.Add(s);
            }
        }

        void DestroyUnusedStretches()
        {
            var entries = new List<StretchSpatialIndex.Entry>(_stretches.All());

            foreach (StretchSpatialIndex.Entry entry in entries)
            {
                if (!_usedStretches.Contains(entry.Stretch))
                {
                    _stretches.Remove(entry.Stretch);
                    Object.DestroyImmediate(entry.Stretch);
                }
            }
        }

        // ---------- destroy ----------

        /// <summary>Destroy all spawned objects (deferred, runtime-safe) / 销毁全部生成对象（延迟销毁）。</summary>
        public void Destroy()
        {
            foreach (GameObject go in _all)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            _all.Clear();

            if (Root != null)
                Object.Destroy(Root);

            ResetState();
        }

        /// <summary>Destroy all spawned objects immediately (edit-time safe) / 立即销毁全部生成对象。</summary>
        public void DestroyImmediate()
        {
            foreach (GameObject go in _all)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            _all.Clear();

            if (Root != null)
                Object.DestroyImmediate(Root);

            ResetState();
        }

        void ResetState()
        {
            Root = null;
            _stretches.Clear();
            _parallelogramQuads.Clear();
            _quadInfos.Clear();
            _fallbacks.Clear();
            _hierarchicalParents.Clear();
            _hierarchyDepths.Clear();
            _usedStretches.Clear();
            _stretchesCreated = 0;
            ParallelogramCount = 0;
            RectangleCount = 0;
            ReparentedCount = 0;
        }
    }
}
