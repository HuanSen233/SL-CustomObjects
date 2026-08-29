using System.Collections.Generic;
using UnityEngine;

/// <summary>Precomputed boundary reference lines (reflex/perpendicular) at each sample-point junction of a 2D
/// curve. Shared by placement and rendering so preview == generated. / 一条 2D 曲线在每个采样点交界处的预计算
/// 边界参考线（反射/垂线）。供放置与渲染共用，保证"预览 = 生成"。</summary>
public struct CurveFitRefs
{
    public Vector3[] Origin;
    public Vector3[] Dir;
    public bool[] Has;
    public static CurveFitRefs Empty => new CurveFitRefs();
}

/// <summary>
/// Shared advanced-fit geometry: computes the reflex/perpendicular boundary lines of a curve once, and the
/// advanced-fit footprint rectangle per micro-segment. Used by both CurvePlacementHelper (object scale) and
/// CurveSceneRenderer (preview wireframes) so they stay consistent.
/// 共享的进阶适应几何：一次性计算曲线的反射/垂边界线，并逐小段计算进阶足迹矩形。
/// 供 CurvePlacementHelper（物体缩放）与 CurveSceneRenderer（预览线框）共用，保持一致。
/// </summary>
public static class CurveFitGeometry
{
    /// <summary>Precomputes the boundary line (origin + in-plane dir) at every sample-point junction of a 2D curve:
    /// reflex bisector at a bend, perpendicular at a straight junction or open-curve endpoint (loops wrap at index 0).
    /// 预计算 2D 曲线每个采样点交界的边界线（原点+平面内方向）：弯折处为反射角线，直线交界或开放端点为垂线
    /// （闭环在索引 0 处环绕）。</summary>
    public static CurveFitRefs ComputeBoundaryLines(BezierCurve curve, List<Vector2> pts)
    {
        int n = pts != null ? pts.Count : 0;
        var refs = new CurveFitRefs { Origin = new Vector3[n], Dir = new Vector3[n], Has = new bool[n] };
        if (curve == null || n == 0) return refs;
        Vector3 normal = curve.PlaneNormal;
        bool isLoop = curve.IsLoop;
        for (int i = 0; i < n; i++)
        {
            if (isLoop && i == n - 1) continue; // wrap dup == pts[0] / 环绕点即 pts[0]
            bool endpoint = !isLoop && (i == 0 || i == n - 1);
            Vector3 b = curve.MapToWorld(pts[i]);
            if (endpoint)
            {
                int adj = i == 0 ? 1 : n - 2;
                Vector3 sd = SafeNormalize3(curve.MapToWorld(pts[adj]) - b);
                if (sd.sqrMagnitude < 1e-10f) continue;
                Vector3 perp = SafeNormalize3(Vector3.Cross(normal, sd));
                if (perp.sqrMagnitude < 1e-10f) continue;
                refs.Origin[i] = b; refs.Dir[i] = perp; refs.Has[i] = true;
            }
            else
            {
                Vector3 a, c;
                if (isLoop && i == 0) { a = curve.MapToWorld(pts[n - 2]); c = curve.MapToWorld(pts[1]); }
                else { a = curve.MapToWorld(pts[i - 1]); c = curve.MapToWorld(pts[i + 1]); }
                Vector3 uIn = SafeNormalize3(b - a);
                Vector3 vOut = SafeNormalize3(c - b);
                if (uIn.sqrMagnitude < 1e-10f || vOut.sqrMagnitude < 1e-10f) continue;
                Vector3 bd = Vector3.Dot(uIn, vOut) > 0.9999f
                    ? SafeNormalize3(Vector3.Cross(normal, uIn))   // straight → perpendicular / 直线贯通→垂线
                    : SafeNormalize3(uIn - vOut);                  // reflex bisector / 反射角线
                if (bd.sqrMagnitude < 1e-10f) continue;
                refs.Origin[i] = b; refs.Dir[i] = bd; refs.Has[i] = true;
            }
        }
        return refs;
    }

    /// <summary>Computes the advanced-fit footprint rectangle for micro-segment i using the precomputed boundary
    /// lines: `length` = along-segment extent, `width` = 2 × FitSizeScale, `alongCenter` = footprint center offset
    /// along the segment (to align the object so it fills the bend-side gap without overhanging the endpoint side).
    /// 用预计算边界线计算进阶小线段 i 的足迹矩形：length=沿段跨度，width=2×FitSizeScale，
    /// alongCenter=足迹中心沿段偏移（对齐物体以填弯折侧缺口、端点侧不凸出）。</summary>
    public static bool ComputeAdvancedRect(BezierCurve curve, List<Vector2> pts, int i, CurveFitRefs refs,
        out Vector3 center, out Vector3 segDir, out float length, out float width, out float alongCenter)
    {
        center = segDir = Vector3.zero; length = 0f; width = 0f; alongCenter = 0f;
        if (curve == null || pts == null || curve.Segments == null || i < 0 || i + 1 >= pts.Count
            || i >= curve.Segments.Count || refs.Has == null) return false;
        var seg = curve.Segments[i];
        float d = Mathf.Max(0f, seg.FitSizeScale);
        width = 2f * d;
        Vector3 normal = curve.PlaneNormal;
        Vector3 a = curve.MapToWorld(pts[i]);
        Vector3 b = curve.MapToWorld(pts[i + 1]);
        segDir = SafeNormalize3(b - a);
        if (segDir.sqrMagnitude < 1e-10f) return false;
        center = (a + b) * 0.5f;
        float segLen = Vector3.Distance(a, b);
        int n = refs.Origin.Length;
        int startIdx = i;
        int endIdx = (curve.IsLoop && i + 1 == n - 1) ? 0 : i + 1; // loop wrap end → joint at 0 / 闭环环绕末端→连接点0

        float minA = float.MaxValue, maxA = float.MinValue;
        Vector3 nrm = SafeNormalize3(Vector3.Cross(normal, segDir));
        if (nrm.sqrMagnitude < 1e-10f) return false;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 po = center + nrm * (d * s);
            if (refs.Has[startIdx] && IntersectLines(po, segDir, refs.Origin[startIdx], refs.Dir[startIdx], normal, out Vector3 ip0))
                minA = Mathf.Min(minA, Vector3.Dot(ip0 - center, segDir));
            if (refs.Has[endIdx] && IntersectLines(po, segDir, refs.Origin[endIdx], refs.Dir[endIdx], normal, out Vector3 ip1))
                maxA = Mathf.Max(maxA, Vector3.Dot(ip1 - center, segDir));
        }
        if (minA > maxA) { minA = -segLen * 0.5f; maxA = segLen * 0.5f; }
        length = maxA - minA;
        alongCenter = (minA + maxA) * 0.5f;
        return length > 1e-6f;
    }

    private static Vector3 SafeNormalize3(Vector3 v)
    {
        float len = v.magnitude;
        return len < 1e-6f ? Vector3.zero : v / len;
    }

    private static bool IntersectLines(Vector3 o1, Vector3 d1, Vector3 o2, Vector3 d2, Vector3 normal, out Vector3 point)
    {
        point = Vector3.zero;
        float denom = Vector3.Dot(Vector3.Cross(d1, d2), normal);
        if (Mathf.Abs(denom) < 1e-8f) return false;
        float t = Vector3.Dot(Vector3.Cross(o2 - o1, d2), normal) / denom;
        point = o1 + d1 * t;
        return true;
    }
}
