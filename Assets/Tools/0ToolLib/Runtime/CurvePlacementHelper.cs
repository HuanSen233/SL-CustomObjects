using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Placement calculator for curve segments — shared by CurveObjectBuilder (actual generation)
/// and CurveSceneRenderer (preview wireframes) so "what you preview is what you get".
/// 曲线段放置参数计算器 — 供 CurveObjectBuilder（实际生成）与
/// CurveSceneRenderer（预览线框）共用，保证"预览所见 = 生成所得"。
/// </summary>
public static class CurvePlacementHelper
{
    /// <summary>
    /// Computes the placement parameters (world position, rotation, scale) for one micro-segment.
    /// Returns false when the segment is too short (should be skipped, no object spawned).
    /// 计算某小线段的生成放置参数（世界位置、旋转、缩放）。
    /// 返回 false 表示该段过短（应跳过，不生成物体）。
    /// </summary>
    /// <param name="curve">Target curve / 目标曲线</param>
    /// <param name="i">Micro-segment index (0-based) / 小线段索引（0 起）</param>
    /// <param name="pts">2D sample points (result of SamplePoints(); must stay in sync with pts3d) / 2D 采样点（SamplePoints() 的结果，须与 pts3d 同步）</param>
    /// <param name="pts3d">3D sample points (result of SamplePoints3D(); pass null for non-3D curves) / 3D 采样点（SamplePoints3D() 的结果；非 3D 曲线传 null）</param>
    /// <param name="refs">Precomputed boundary lines for the curve (CurveFitGeometry.ComputeBoundaryLines); pass
    /// CurveFitRefs.Empty when no advanced-fit segments are expected. / 该曲线的预计算边界线
    /// （CurveFitGeometry.ComputeBoundaryLines 的结果）；无进阶段时传 CurveFitRefs.Empty。</param>
    /// <param name="pos">Output: world position (segment center + offset) / 输出：世界位置（段中心 + 偏移）</param>
    /// <param name="rot">Output: world rotation (LookRotation + rotation offset) / 输出：世界旋转（LookRotation + 旋转偏移）</param>
    /// <param name="scale">Output: world scale (BaseScale → FitSegmentLength → RelativeScale) / 输出：世界缩放（BaseScale → FitSegmentLength → RelativeScale）</param>
    public static bool ComputePlacement(BezierCurve curve, int i, List<Vector2> pts, List<Vector3> pts3d,
        CurveFitRefs refs, out Vector3 pos, out Quaternion rot, out Vector3 scale)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        scale = Vector3.one;

        if (curve == null || curve.Segments == null || i < 0 || i >= curve.Segments.Count) return false;
        if (pts == null || i + 1 >= pts.Count) return false;

        Vector3 basePos;
        Vector3 dir;
        float segLen;

        if (curve.Is3D)
        {
            if (pts3d == null || i + 1 >= pts3d.Count) return false;
            Vector3 a = pts3d[i], b = pts3d[i + 1];
            float segLen3d = Vector3.Distance(a, b);
            if (segLen3d < 0.001f) return false;
            segLen = segLen3d;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 d = (b - a).normalized;
            basePos = mid + d * (segLen3d * curve.Segments[i].PositionOffset);
            dir = d;
        }
        else
        {
            Vector2 a = pts[i], b = pts[i + 1];
            segLen = Vector2.Distance(a, b);
            if (segLen < 0.001f) return false;
            Vector2 midPt = (a + b) * 0.5f;
            Vector2 dir2 = (b - a).normalized;
            Vector2 genPt2 = midPt + dir2 * (segLen * curve.Segments[i].PositionOffset);
            basePos = curve.MapToWorld(genPt2);
            dir = curve.MapToWorld(a + dir2) - curve.MapToWorld(a);
            if (dir.sqrMagnitude < 0.0001f) dir = curve.PlaneNormal;
        }

        var seg = curve.Segments[i];

        // Rotation: LookRotation degenerates for vertical 3D segments (dir ∥ PlaneNormal), so fall back to FromToRotation.
        // 旋转（3D 竖直段 dir ∥ PlaneNormal 时 LookRotation 退化，兜底处理）
        if (Mathf.Abs(Vector3.Dot(dir, curve.PlaneNormal)) > 0.999f)
            rot = Quaternion.FromToRotation(Vector3.forward, dir);
        else
            rot = Quaternion.LookRotation(dir, curve.PlaneNormal);
        rot = rot * Quaternion.Euler(seg.RotationOffset);

        // Position offset is applied in segment-local space.
        // 位置偏移在段本地空间
        pos = basePos + rot * seg.PositionOffset3D;

        // Scale chain: BaseScale → advanced/simple fit → × RelativeScale.
        // 缩放：BaseScale → 进阶/简单适应 → × RelativeScale
        float advancedAlong = 0f; // along-segment shift so the object aligns with the footprint center / 将物体对齐足迹中心的沿段偏移
        scale = seg.BaseScale;
        if (seg.FitSegmentLength)
        {
            if (seg.FitMode == 1 && !curve.Is3D)
            {
                // Advanced gap-filling fit (2D only): scale from the footprint rectangle, honoring the FitAxis
                // selection for the length axis. The width (2 × FitSizeScale) goes to the other in-plane axis,
                // and the thickness (out-of-plane) keeps its BaseScale default.
                // 进阶填缺口适应（仅2D）：按足迹矩形取缩放，并遵循 FitAxis 选择的长度轴。
                // 宽度（2×FitSizeScale）给另一面内轴；厚度（平面法线）保留 BaseScale 默认。
                if (TryComputeAdvancedFitRect(curve, pts, i, refs, out float fitLen, out float fitWidth, out float fitAlong))
                {
                    // Local frame = LookRotation(dir, planeNormal): Z = segment dir, X = in-plane across, Y = plane normal.
                    // 局部坐标系 = LookRotation(dir, planeNormal)：Z = 段方向，X = 面内横向，Y = 平面法线。
                    int lenAxis = Mathf.Clamp(seg.FitAxis, 0, 2); // 0=X, 1=Y, 2=Z
                    int widthAxis, thickAxis;
                    if (lenAxis == 2) { widthAxis = 0; thickAxis = 1; }        // length→Z, width→X, thick→Y (default)
                    else if (lenAxis == 0) { widthAxis = 2; thickAxis = 1; }   // length→X, width→Z, thick→Y
                    else { widthAxis = 0; thickAxis = 2; }                     // length→Y, width→X, thick→Z
                    Vector3 s = scale;
                    s[lenAxis] = fitLen;
                    s[widthAxis] = fitWidth;
                    // s[thickAxis] stays at its BaseScale default. / s[thickAxis] 保留 BaseScale 默认。
                    scale = s;
                    advancedAlong = fitAlong; // shift toward the footprint center / 对齐足迹中心
                }
                else
                {
                    if (seg.FitAxis == 0) scale.x *= segLen;
                    else if (seg.FitAxis == 1) scale.y *= segLen;
                    else scale.z *= segLen;
                }
            }
            else
            {
                if (seg.FitAxis == 0) scale.x *= segLen;
                else if (seg.FitAxis == 1) scale.y *= segLen;
                else scale.z *= segLen;
            }
        }
        scale = Vector3.Scale(scale, seg.RelativeScale);
        // Align advanced-fit objects to the footprint center along the segment direction (fills the bend-side
        // gap without overhanging the endpoint side). / 进阶物体沿段方向对齐到足迹中心（填弯折侧缺口、端点侧不凸出）。
        if (advancedAlong != 0f) pos += dir * advancedAlong;

        return true;
    }

    /// <summary>Advanced-fit footprint rectangle for micro-segment i (via the shared CurveFitGeometry), using the
    /// precomputed boundary lines `refs`; falls back to computing the lines on the fly when `refs` is empty.
    /// 进阶小段 i 的足迹矩形（经由共享 CurveFitGeometry），使用预计算边界线 refs；refs 为空时现场计算兜底。</summary>
    private static bool TryComputeAdvancedFitRect(BezierCurve curve, List<Vector2> pts, int i, CurveFitRefs refs,
        out float length, out float width, out float alongCenter)
    {
        if (refs.Has == null) refs = CurveFitGeometry.ComputeBoundaryLines(curve, pts);
        return CurveFitGeometry.ComputeAdvancedRect(curve, pts, i, refs, out _, out _, out length, out width, out alongCenter);
    }
}