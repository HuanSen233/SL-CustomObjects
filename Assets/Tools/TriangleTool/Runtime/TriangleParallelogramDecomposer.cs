using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// Data of a parallelogram in half-diagonal parametrization.
    /// 半对角线参数化的平行四边形数据。
    /// Corners are Center +- VLeft and Center +- VUp; edges are VLeft + VUp and VLeft - VUp.
    /// 四个角点为 Center+VLeft / Center+VUp / Center-VLeft / Center-VUp。
    /// </summary>
    public struct ParallelogramData
    {
        public Vector3 VLeft;   // 半对角线 1 (half-diagonal 1)
        public Vector3 VUp;     // 半对角线 2 (half-diagonal 2)
        public Vector3 Center;  // 中心 (center)
    }

    /// <summary>
    /// V1 (Exact) core math: decompose a triangle into exactly 3 parallelograms.
    /// V1 (Exact) 核心数学：把一个三角形分解为恰好 3 个平行四边形。
    ///
    /// Ported from TriangleScpSl / TriangleParallelogramBuilder (Foibos, CC-BY-SA 3.0).
    /// 移植自 TriangleScpSl / TriangleParallelogramBuilder（Foibos，CC-BY-SA 3.0）。
    ///
    /// Geometry: with edge midpoints halfAb/halfAc/halfBc, the parallelogram at vertex A
    /// has center (halfBc + A)/2 and covers {A, halfAb, halfAc, halfBc}. The three
    /// parallelograms tile the triangle; the medial triangle is covered 3 times but is
    /// invisible because all pieces are coplanar with the same color (pixel-exact).
    /// 几何：以三边中点连线，A 顶点的平行四边形中心为 (halfBc+A)/2，覆盖 {A, halfAb, halfAc, halfBc}。
    /// 三个平行四边形铺满三角形；中心 medial 三角形被覆盖 3 次，但同平面同色不可见（像素级精确）。
    /// </summary>
    public static class TriangleParallelogramDecomposer
    {
        /// <summary>
        /// Decompose triangle (a, b, c) into 3 parallelograms; result[0] belongs to a.
        /// 把三角形 (a, b, c) 分解为 3 个平行四边形；result[0] 对应顶点 a。
        /// </summary>
        public static ParallelogramData[] Decompose(Vector3 a, Vector3 b, Vector3 c)
        {
            // Edge midpoints / 三边中点
            Vector3 halfAb = (a + b) / 2f;
            Vector3 halfAc = (a + c) / 2f;
            Vector3 halfBc = (b + c) / 2f;

            // Per-vertex parallelogram centers / 各顶点平行四边形中心
            Vector3 aCenter = (halfBc + a) / 2f;
            Vector3 bCenter = (halfAc + b) / 2f;
            Vector3 cCenter = (halfAb + c) / 2f;

            return new[]
            {
                new ParallelogramData { VLeft = a - aCenter, VUp = halfAc - aCenter, Center = aCenter },
                new ParallelogramData { VLeft = b - bCenter, VUp = halfAb - bCenter, Center = bCenter },
                new ParallelogramData { VLeft = c - cCenter, VUp = halfBc - cCenter, Center = cCenter },
            };
        }

        /// <summary>
        /// Rectangle test for half-diagonal parametrization: equal-length half-diagonals
        /// mean perpendicular edges (|VLeft| == |VUp| -> 1 Quad instead of 2).
        /// 矩形判定：半对角线等长意味着边互相垂直（|VLeft|==|VUp| → 用 1 个 Quad 代替 2 个）。
        /// </summary>
        public static bool IsRectangle(in ParallelogramData p, float tolerance)
        {
            return Mathf.Abs(p.VLeft.magnitude - p.VUp.magnitude) <= Mathf.Max(tolerance, 1e-7f);
        }
    }
}
