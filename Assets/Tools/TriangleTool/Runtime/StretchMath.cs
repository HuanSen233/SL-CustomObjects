using System;
using System.Collections.Generic;
using DONT_TOUCH.Scripts.BlockComponents;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// Math + object helpers for the V2/V3 stretch clustering.
    /// A stretch is an invisible Empty block carrying rotation R(theta) and scale
    /// (cos(phi)*F, sin(phi)*F, 1); visible Quad blocks placed under it inherit the
    /// deformation. All math is a faithful port of TriangleScpSl / ApproximateModelUtils.
    /// V2/V3 拉伸聚类的数学与对象辅助。stretch 是不可见 Empty 块，携带旋转 R(theta) 与缩放
    /// (cos(phi)*F, sin(phi)*F, 1)；挂在其下的可见 Quad 块继承该变形。
    /// 所有数学忠实移植自 TriangleScpSl / ApproximateModelUtils（Foibos，CC-BY-SA 3.0）。
    /// </summary>
    public static class StretchMath
    {
        /// <summary>
        /// Create an invisible stretch Empty block at origin under rootT.
        /// 在原点创建不可见 stretch Empty 块并挂到 rootT 下。
        /// </summary>
        public static GameObject CreateStretch(float theta, float phi, Transform rootT)
        {
            EmptyComponent empty = ProjectBlockFactory.CreateEmpty("Stretch (invisible)");
            if (empty == null)
                return null;

            Transform t = empty.transform;
            t.SetParent(rootT, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
            t.localScale = new Vector3(
                Mathf.Cos(phi) * VectorPhiSolver.F,
                Mathf.Sin(phi) * VectorPhiSolver.F,
                1f);
            return empty.gameObject;
        }

        /// <summary>
        /// Forward phi-transform: rotate -theta, then divide by (cos(phi)*F, sin(phi)*F).
        /// World vector → local vector in stretch space.
        /// 正向 phi 变换：旋转 -theta，再除以 (cos(phi)*F, sin(phi)*F)。世界向量 → stretch 局部向量。
        /// </summary>
        public static Vector3 ForwardTransform(Vector3 v, float theta, float phi)
        {
            double cp = Math.Cos(phi);
            double sp = Math.Sin(phi);

            // Degenerate case, return unchanged.
            if (Math.Abs(cp) < 1e-10 || Math.Abs(sp) < 1e-10)
                return v;

            double c = Math.Cos(theta), s = Math.Sin(theta);
            double rx = v.x * c + v.y * s;
            double ry = -v.x * s + v.y * c;
            const double f = VectorPhiSolver.F;

            return new Vector3(
                (float)(rx / (cp * f)),
                (float)(ry / (sp * f)),
                v.z);
        }

        /// <summary>
        /// Inverse stretch transform: multiply by (cos(phi)*F, sin(phi)*F), rotate +theta.
        /// Local vector → world (matches Unity's child-under-stretch behavior).
        /// 逆向变换：乘以 (cos(phi)*F, sin(phi)*F) 并旋转 +theta。局部向量 → 世界。
        /// </summary>
        public static Vector3 InverseTransform(Vector3 vLocal, float theta, float phi)
        {
            double cp = Math.Cos(phi), sp = Math.Sin(phi);
            const double f = VectorPhiSolver.F;
            double sx = vLocal.x * (cp * f);
            double sy = vLocal.y * (sp * f);
            double c = Math.Cos(theta), s = Math.Sin(theta);

            // Inverse of R(-theta) is R(theta).
            return new Vector3(
                (float)(sx * c - sy * s),
                (float)(sx * s + sy * c),
                vLocal.z);
        }

        /// <summary>
        /// Worst-case world-space vertex displacement if (vLeft, vUp) were rendered with the
        /// candidate stretch instead of an exactly-fitting one. Absolute world units; 0 when exact.
        /// 用候选 stretch 渲染 (vLeft, vUp) 时最坏情况的世界空间顶点位移（绝对世界单位，精确时为 0）。
        /// </summary>
        public static float MaxVertexError(Vector3 vLeft, Vector3 vUp, float candidateTheta, float candidatePhi)
        {
            // Project the half-diagonals into the candidate stretch's local space.
            Vector3 v1C = ForwardTransform(vLeft, candidateTheta, candidatePhi);
            Vector3 v2C = ForwardTransform(vUp, candidateTheta, candidatePhi);

            Vector3 sumLocal = v1C + v2C;
            Vector3 diffLocal = v1C - v2C;
            float a = sumLocal.magnitude;
            float b = diffLocal.magnitude;

            if (a < 1e-12f || b < 1e-12f) return float.MaxValue;

            // Same orientation CreateParallelogram applies: local Y along (v1-v2),
            // local Z along normal, local X = Y × Z.
            Vector3 yAxis = diffLocal / b;
            Vector3 normalLocal = Vector3.Cross(yAxis, sumLocal / a);
            if (normalLocal.sqrMagnitude < 1e-24f) return float.MaxValue;
            normalLocal = normalLocal.normalized;

            Vector3 xAxis = Vector3.Cross(yAxis, normalLocal);

            // The unit quad after scale (a, b, 1) and that rotation has 4 corners at
            // (±a/2)X + (±b/2)Y in candidate-local. Two of them; their negatives give
            // the other two and produce symmetric errors.
            Vector3 cornerA = a * 0.5f * xAxis + b * 0.5f * yAxis;
            Vector3 cornerB = a * 0.5f * xAxis - b * 0.5f * yAxis;

            // Apply the candidate stretch's parent transform: candidate-local -> world.
            Vector3 worldA = InverseTransform(cornerA, candidateTheta, candidatePhi);
            Vector3 worldB = InverseTransform(cornerB, candidateTheta, candidatePhi);

            // cornerA always falls on the ±vUp corner pair, cornerB on the ±vLeft pair.
            float dA = Mathf.Min((worldA - vUp).magnitude, (worldA + vUp).magnitude);
            float dB = Mathf.Min((worldB - vLeft).magnitude, (worldB + vLeft).magnitude);
            return Mathf.Max(dA, dB);
        }

        /// <summary>
        /// Create a visible Quad block under the given stretch, positioned at world `position`
        /// with its parallelogram defined by the stretch-local vectors v1/v2.
        /// 在指定 stretch 下创建可见 Quad 块：世界位置为 position，平行四边形由 stretch 局部向量 v1/v2 定义。
        /// Returns null on failure (caller falls back to the exact 2-block path).
        /// 失败返回 null（调用方回退到精确 2 块路径）。
        /// </summary>
        public static GameObject CreateParallelogram(Vector3 position, Vector3 v1, Vector3 v2,
            Transform stretchT, Color color, bool collidable)
        {
            float a = (v1 + v2).magnitude;
            float b = (v1 - v2).magnitude;

            if (a < 1e-9f || b < 1e-9f)
                return null;

            Vector3 up = (v1 - v2) / b;
            Vector3 normal = Vector3.Cross(up, (v1 + v2) / a);

            PrimitiveComponent quad = ProjectBlockFactory.CreatePrimitive(
                PrimitiveType.Quad, "StretchChildQuad", color, visible: true, collidable: collidable);
            if (quad == null)
                return null;

            Transform t = quad.transform;
            t.SetParent(stretchT, true); // keep current world pos (origin), then move pivot to the parallelogram center
            t.position = position;       // pivot = parallelogram center / 枢轴移到平行四边形中心
            t.localRotation = Quaternion.LookRotation(normal, up);
            t.localScale = new Vector3(a, b, 1f);
            return quad.gameObject;
        }

        /// <summary>
        /// Re-parent an existing quad onto another stretch, rebuilding local rotation/scale
        /// for the new stretch space. The quad's pivot is the parallelogram center, so its
        /// world position is preserved.
        /// 把已有 Quad 改挂到另一 stretch 下，为新 stretch 空间重建局部旋转/缩放。
        /// Quad 的枢轴是平行四边形中心，因此世界位置保持不变。
        /// </summary>
        public static void ReparentToStretch(Transform quadT, Transform stretchT, Vector3 v1, Vector3 v2)
        {
            float a = (v1 + v2).magnitude;
            float b = (v1 - v2).magnitude;

            if (a < 1e-9f || b < 1e-9f)
                return;

            Vector3 up = (v1 - v2) / b;
            Vector3 normal = Vector3.Cross(up, (v1 + v2) / a);

            quadT.SetParent(stretchT, true);
            quadT.localRotation = Quaternion.LookRotation(normal, up);
            quadT.localScale = new Vector3(a, b, 1f);
        }

        /// <summary>
        /// Find the best existing stretch that can render the parallelogram within tolerance.
        /// Nearby stretches first; on a miss, ALL stretches are scanned (a parallelogram has a
        /// whole curve of valid (theta, phi) decompositions).
        /// 找可容忍内渲染该平行四边形的最佳已有 stretch：先查近邻；未命中再全量扫描
        /// （平行四边形有一整条有效的 (theta, phi) 分解曲线）。
        /// </summary>
        public static StretchSpatialIndex.Entry? FindBestStretch(
            StretchSpatialIndex stretches,
            Vector3 vLeft, Vector3 vUp,
            float theta, float phi,
            float toleranceUnits)
        {
            StretchSpatialIndex.Entry? best = null;
            float bestErr = float.MaxValue;

            foreach (StretchSpatialIndex.Entry entry in stretches.QueryNearby(theta, phi))
            {
                float err = MaxVertexError(vLeft, vUp, entry.Theta, entry.Phi);
                if (err <= toleranceUnits && err < bestErr)
                {
                    bestErr = err;
                    best = entry;
                }
            }

            if (best != null)
                return best;

            foreach (StretchSpatialIndex.Entry entry in stretches.All())
            {
                float err = MaxVertexError(vLeft, vUp, entry.Theta, entry.Phi);
                if (err <= toleranceUnits && err < bestErr)
                {
                    bestErr = err;
                    best = entry;
                }
            }

            return best;
        }

        /// <summary>
        /// Drain sparsely-used stretches by rehoming all of their child quads onto other
        /// stretches within tolerance, smallest stretch first. Each fully drained stretch is
        /// one primitive saved. Returns the emptied stretches.
        /// 排空使用率低的 stretch：把其子 Quad 在容差内迁到其他 stretch，最小者优先。
        /// 每个完全排空的 stretch 节省一个 primitive，返回被排空的 stretch 集合。
        /// </summary>
        /// <param name="stretches">The stretch index / stretch 索引。</param>
        /// <param name="quadCount">Total quad count (indices 0..quadCount-1) / Quad 总数。</param>
        /// <param name="stretchOf">Stretch a quad is parented to, or null (rectangles, reparented quads)。</param>
        /// <param name="diagonalsOf">The quad's original world-space half-diagonals / Quad 的原始世界半对角线。</param>
        /// <param name="rehome">Moves a quad onto the target stretch / 把 Quad 迁到目标 stretch。</param>
        /// <param name="toleranceUnits">Max world-space vertex error / 最大世界空间顶点误差。</param>
        public static HashSet<GameObject> ConsolidateStretches(
            StretchSpatialIndex stretches,
            int quadCount,
            Func<int, GameObject> stretchOf,
            Func<int, (Vector3 vLeft, Vector3 vUp)> diagonalsOf,
            Action<int, StretchSpatialIndex.Entry> rehome,
            float toleranceUnits)
        {
            var emptied = new HashSet<GameObject>();
            var entries = new List<StretchSpatialIndex.Entry>(stretches.All());

            if (entries.Count < 2)
                return emptied;

            var childrenOf = new Dictionary<GameObject, List<int>>();

            for (int i = 0; i < quadCount; i++)
            {
                GameObject stretch = stretchOf(i);
                if (stretch == null) continue;

                if (!childrenOf.TryGetValue(stretch, out List<int> list))
                {
                    list = new List<int>(4);
                    childrenOf[stretch] = list;
                }

                list.Add(i);
            }

            int ChildCount(StretchSpatialIndex.Entry e)
                => childrenOf.TryGetValue(e.Stretch, out List<int> l) ? l.Count : 0;

            // Drain the least-used stretches first — cheapest wins, and their children
            // land on popular stretches, making those even better targets.
            entries.Sort((x, y) => ChildCount(x).CompareTo(ChildCount(y)));

            foreach (StretchSpatialIndex.Entry entry in entries)
            {
                if (!childrenOf.TryGetValue(entry.Stretch, out List<int> kids) || kids.Count == 0)
                {
                    emptied.Add(entry.Stretch);
                    continue;
                }

                var moves = new List<(int kid, StretchSpatialIndex.Entry target)>(kids.Count);
                bool allRehomable = true;

                foreach (int kid in kids)
                {
                    (Vector3 vLeft, Vector3 vUp) = diagonalsOf(kid);
                    StretchSpatialIndex.Entry? best = null;
                    float bestErr = float.MaxValue;

                    foreach (StretchSpatialIndex.Entry other in entries)
                    {
                        if (ReferenceEquals(other.Stretch, entry.Stretch)) continue;
                        if (emptied.Contains(other.Stretch)) continue;

                        // Only stretches that keep other children are useful targets.
                        if (ChildCount(other) == 0) continue;

                        float err = MaxVertexError(vLeft, vUp, other.Theta, other.Phi);
                        if (err <= toleranceUnits && err < bestErr)
                        {
                            bestErr = err;
                            best = other;
                        }
                    }

                    if (best == null)
                    {
                        allRehomable = false;
                        break;
                    }

                    moves.Add((kid, best.Value));
                }

                if (!allRehomable) continue;

                foreach ((int kid, StretchSpatialIndex.Entry target) in moves)
                {
                    rehome(kid, target);
                    childrenOf[target.Stretch].Add(kid);
                }

                kids.Clear();
                emptied.Add(entry.Stretch);
            }

            return emptied;
        }
    }
}
