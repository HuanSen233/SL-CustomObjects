using System;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// Finds (theta, phi) such that after rotating by -theta around Z and scaling
    /// x/(cos(phi)*F), y/(sin(phi)*F) both half-diagonal vectors end up with equal length.
    /// This is the angle decomposition that lets multiple parallelograms share one "stretch".
    /// 求解 (theta, phi)：绕 Z 旋转 -theta、再按 x/(cos(phi)*F)、y/(sin(phi)*F) 缩放后，
    /// 两个半对角线向量长度相等。这是让多个平行四边形共享同一个 "stretch" 的角度分解。
    ///
    /// Ported from TriangleScpSl / VectorPhiSolver (Foibos, CC-BY-SA 3.0), C# 8 compatible.
    /// 移植自 TriangleScpSl / VectorPhiSolver（Foibos，CC-BY-SA 3.0），C# 8 兼容改写。
    /// </summary>
    public static class VectorPhiSolver
    {
        public const float F = 2f;

        // Tolerance thresholds / 容差阈值
        const double Epsilon = 1e-15;
        const double EpsilonLarge = 1e-12;
        const double TBoundaryTolerance = 1e-9;

        /// <summary>
        /// Rotate XY by -theta (Z unchanged). / 将 XY 旋转 -theta（Z 不变）。
        /// </summary>
        static (double x, double y) RotateXY(Vector3 v, double theta)
        {
            double c = Math.Cos(theta), s = Math.Sin(theta);
            // R(-theta) = [[cos(theta), sin(theta)],[-sin(theta), cos(theta)]]
            return (v.x * c + v.y * s, -v.x * s + v.y * c);
        }

        static bool TrySolveForPhi(Vector3 v1, Vector3 v2, double theta, out double phi)
        {
            phi = 0;
            (double u1X, double u1Y) = RotateXY(v1, theta);
            (double u2X, double u2Y) = RotateXY(v2, theta);

            double a = u1X * u1X - u2X * u2X;
            double b = u1Y * u1Y - u2Y * u2Y;
            double c = (double)v1.z * v1.z - (double)v2.z * v2.z;
            const double F2 = F * F;

            // Quadratic: C * F^2 * t^2 + (a - b - c * f^2)*t - a = 0,  t = cos(phi)^2
            double qa = c * F2;
            double qb = a - b - c * F2;
            double qc = -a;

            double? bestT = null;

            if (Math.Abs(qa) < Epsilon)
            {
                // Linear case / 线性情形
                if (Math.Abs(qb) > Epsilon)
                {
                    double t = -qc / qb;
                    if (t > TBoundaryTolerance && t < 1 - TBoundaryTolerance)
                        bestT = t;
                }
                else if (Math.Abs(qc) < EpsilonLarge)
                {
                    // Any phi works / 任意 phi 均可
                    bestT = 0.5; // phi = pi/4
                }
            }
            else
            {
                double disc = qb * qb - 4.0 * qa * qc;

                if (disc >= 0)
                {
                    double sq = Math.Sqrt(disc);
                    double t1 = (-qb + sq) / (2.0 * qa);
                    double t2 = (-qb - sq) / (2.0 * qa);
                    bool ok1 = t1 > TBoundaryTolerance && t1 < 1 - TBoundaryTolerance;
                    bool ok2 = t2 > TBoundaryTolerance && t2 < 1 - TBoundaryTolerance;

                    if (ok1 && ok2)
                        bestT = Math.Abs(t1 - 0.5) < Math.Abs(t2 - 0.5) ? t1 : t2;
                    else if (ok1)
                        bestT = t1;
                    else if (ok2)
                        bestT = t2;
                }
            }

            if (bestT == null) return false;
            phi = Math.Acos(Math.Sqrt(Math.Min(1.0, bestT.Value)));
            return true;
        }

        static double FindOptimalTheta(Vector3 v1, Vector3 v2)
        {
            double a = (double)v1.x * v1.x - (double)v2.x * v2.x;
            double b = (double)v1.y * v1.y - (double)v2.y * v2.y;
            double e = (a - b) / 2.0;
            double g = (double)v1.x * v1.y - (double)v2.x * v2.y;
            double h = Math.Sqrt(e * e + g * g);

            if (h < Epsilon) return Math.PI / 4;

            // Minimum when cos(2 * theta + alpha) = -1, i.e. 2 * theta + alpha = pi
            double alpha = Math.Atan2(g, e);
            return (Math.PI - alpha) / 2.0;
        }

        /// <summary>
        /// Returns false when no solution exists.
        /// 无解时返回 false。
        /// </summary>
        public static bool TrySolve(Vector3 v1, Vector3 v2, out float theta, out float phi)
        {
            theta = 0f;
            phi = 0f;

            // Try a small set of candidate thetas first: covers almost all cases cheaply.
            // 先尝试少量候选 theta：低成本覆盖几乎所有情况。
            double[] candidates =
            {
                0,
                FindOptimalTheta(v1, v2),
                Math.PI / 4,
                Math.PI / 2,
                3 * Math.PI / 4,
            };

            foreach (double thetaD in candidates)
            {
                if (TrySolveForPhi(v1, v2, thetaD, out double phiD))
                {
                    theta = (float)thetaD;
                    phi = (float)phiD;
                    return true;
                }
            }

            // Brute-force fallback for the rare remaining cases.
            // 针对极少数剩余情况的暴力兜底。
            for (int i = 1; i <= 360; i++)
            {
                double thetaD = i * Math.PI / 360.0;

                if (TrySolveForPhi(v1, v2, thetaD, out double phiD))
                {
                    theta = (float)thetaD;
                    phi = (float)phiD;
                    return true;
                }
            }

            return false;
        }
    }
}
