using DONT_TOUCH.Scripts.BlockComponents;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// Result of building one parallelogram out of the project's block prefabs.
    /// 用项目块预制体构建一个平行四边形后的结果。
    /// </summary>
    public struct ParallelogramQuadResult
    {
        public GameObject Base;   // Invisible Empty block carrying the shear transform / 承载剪切变换的不可见 Empty 块
        public GameObject Child;  // Visible Quad block / 可见 Quad 块（矩形优化时 Child == Base 同一对象）
        public bool UsedRectangleOptimization; // True when only 1 Quad was used / 是否只用了 1 个 Quad
    }

    /// <summary>
    /// Renders an arbitrary parallelogram using the "SetParent deformation trick":
    /// an invisible Empty block (project's Empty.prefab) carries the shear
    /// (non-uniform scale x on local X + rotation), and a visible Quad block
    /// (project's Primitives/Quad.prefab) inherits it via SetParent.
    /// V1 (Exact) renderer for one parallelogram.
    /// 用 "SetParent 变形技巧" 渲染任意平行四边形：不可见 Empty 块（项目 Empty.prefab）携带剪切
    /// （局部 X 非均匀缩放 x + 旋转），可见 Quad 块（项目 Primitives/Quad.prefab）通过 SetParent 继承。
    /// 这是 V1 (Exact) 的单平行四边形渲染器。
    ///
    /// Ported from TriangleScpSl / ParallelogramHelpUtils + ParallelogramPrimitive (Foibos, CC-BY-SA 3.0).
    /// 移植自 TriangleScpSl / ParallelogramHelpUtils + ParallelogramPrimitive（Foibos，CC-BY-SA 3.0）。
    /// </summary>
    public static class ParallelogramQuadBuilder
    {
        /// <summary>
        /// Build a parallelogram from half-diagonals vUp/vLeft centered at center.
        /// 由半对角线 vUp/vLeft 与中心 center 构建平行四边形。
        /// </summary>
        public static ParallelogramQuadResult Build(
            Vector3 vUp, Vector3 vLeft, Vector3 center,
            Color color, Material material = null)
        {
            // Make the longer half-diagonal the up axis: GetAffineComponents needs
            // |Dot(vLeft, vUp)| strictly below vUp.sqrMagnitude with margin, and
            // longer-up maximizes that margin.
            // 让较长半对角线作为 up 轴：GetAffineComponents 要求 |Dot(vLeft,vUp)| 严格小于
            // vUp.sqrMagnitude 并留有裕量，取较长者作 up 轴裕量最大。
            if (vUp.sqrMagnitude < vLeft.sqrMagnitude)
            {
                Vector3 tmp = vUp;
                vUp = vLeft;
                vLeft = -tmp;
            }

            // Affine decomposition: inner rectangle a x b and shear factor x.
            // 仿射分解：内接矩形 a x b 与剪切因子 x。
            GetAffineComponents(vUp, vLeft, out float a, out float b, out float x);
            float angleDeg = Mathf.Atan2(b, a) * Mathf.Rad2Deg;
            Vector3 vNormal = Vector3.Cross(PerpSameHalfPlane(vUp, vLeft), vUp.normalized);

            // --- Invisible base: project Empty block (transform holder, no renderer) ---
            // 不可见父对象：项目 Empty 块（仅承载变换，无渲染器）
            EmptyComponent baseEmpty = ProjectBlockFactory.CreateEmpty("ParallelogramBase (invisible)");
            if (baseEmpty == null)
                return default(ParallelogramQuadResult);

            Transform baseT = baseEmpty.transform;
            baseT.position = center;
            baseT.rotation = Quaternion.LookRotation(vNormal, vUp);
            baseT.localScale = new Vector3(x, 1f, 1f);

            // --- Visible child: project Quad block, inherits the parent shear via SetParent ---
            // 可见子对象：项目 Quad 块，通过 SetParent 继承父级剪切
            PrimitiveComponent child = ProjectBlockFactory.CreatePrimitive(
                PrimitiveType.Quad, "ParallelogramChild", color, visible: true, collidable: false);
            if (child == null)
                return default(ParallelogramQuadResult);

            Transform childT = child.transform;
            childT.SetParent(baseT, false);
            childT.localPosition = Vector3.zero;
            childT.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
            childT.localScale = new Vector3(b, a, 1f);

            return new ParallelogramQuadResult
            {
                Base = baseEmpty.gameObject,
                Child = child.gameObject,
                UsedRectangleOptimization = false,
            };
        }

        /// <summary>
        /// Build a rectangle (equal half-diagonals) with a single Quad block — 1 primitive saved.
        /// 构建矩形（半对角线等长）只需 1 个 Quad 块 —— 节省 1 个 primitive。
        /// </summary>
        public static ParallelogramQuadResult BuildRectangle(
            Vector3 vLeft, Vector3 vUp, Vector3 center,
            Color color, Material material = null)
        {
            // VLeft/VUp are half-diagonals; edges are (VLeft+VUp) and (VLeft-VUp).
            // 半对角线是 VLeft/VUp；边为 (VLeft+VUp) 与 (VLeft-VUp)。
            Vector3 edgeA = vLeft + vUp;
            Vector3 edgeB = vLeft - vUp;
            float width = edgeB.magnitude;
            float height = edgeA.magnitude;
            Vector3 forward = Vector3.Cross(edgeB, edgeA).normalized;

            if (forward.sqrMagnitude < 1e-6f || width < 1e-7f || height < 1e-7f)
            {
                // Degenerate -> fall back to the 2-block shear path.
                // 退化情形 -> 回退到 2-块剪切路径。
                return Build(vUp, vLeft, center, color, material);
            }

            Quaternion rotation = Quaternion.LookRotation(forward, edgeA.normalized);

            PrimitiveComponent quad = ProjectBlockFactory.CreatePrimitive(
                PrimitiveType.Quad, "RectangleQuad", color, visible: true, collidable: false);
            if (quad == null)
                return default(ParallelogramQuadResult);

            Transform quadT = quad.transform;
            quadT.position = center;
            quadT.rotation = rotation;
            quadT.localScale = new Vector3(width, height, 1f);

            return new ParallelogramQuadResult
            {
                Base = quad.gameObject,
                Child = quad.gameObject,
                UsedRectangleOptimization = true,
            };
        }

        /// <summary>
        /// Project vLeft onto the plane perpendicular to vUp, keeping the same half-plane
        /// as vLeft. Used to build the parallelogram plane normal.
        /// 把 vLeft 投影到垂直于 vUp 的平面并保持与 vLeft 同侧，用于构建平行四边形平面法线。
        /// </summary>
        static Vector3 PerpSameHalfPlane(Vector3 vUp, Vector3 vLeft)
        {
            Vector3 res = Vector3.ProjectOnPlane(vLeft, vUp.normalized).normalized;
            if (Vector3.Dot(res, vLeft) < 0f)
                res = -res;
            return res;
        }

        /// <summary>
        /// Affine components of the parallelogram: inner rectangle a x b and shear x,
        /// derived from the half-diagonal geometry.
        /// 平行四边形的仿射分量：内接矩形 a x b 与剪切因子 x，由半对角线几何推导。
        /// </summary>
        static void GetAffineComponents(Vector3 vUp, Vector3 vLeft, out float a, out float b, out float x)
        {
            float upLen = vUp.magnitude;

            if (upLen <= Mathf.Epsilon)
            {
                a = 1f; b = 1f; x = 1f;
                return;
            }

            Vector3 upN = vUp / upLen;
            float leftY = Mathf.Clamp(Vector3.Dot(vLeft, upN), -upLen, upLen);
            float leftX = Vector3.ProjectOnPlane(vLeft, upN).magnitude;

            a = Mathf.Sqrt(Mathf.Max(2f * upLen * (upLen + leftY), Mathf.Epsilon));
            b = Mathf.Sqrt(Mathf.Max(2f * upLen * (upLen - leftY), Mathf.Epsilon));
            x = leftX * 2f * upLen / Mathf.Max(a * b, Mathf.Epsilon);
        }
    }
}
