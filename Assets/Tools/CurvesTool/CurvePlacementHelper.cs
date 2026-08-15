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
    /// <param name="pos">Output: world position (segment center + offset) / 输出：世界位置（段中心 + 偏移）</param>
    /// <param name="rot">Output: world rotation (LookRotation + rotation offset) / 输出：世界旋转（LookRotation + 旋转偏移）</param>
    /// <param name="scale">Output: world scale (BaseScale → FitSegmentLength → RelativeScale) / 输出：世界缩放（BaseScale → FitSegmentLength → RelativeScale）</param>
    public static bool ComputePlacement(BezierCurve curve, int i, List<Vector2> pts, List<Vector3> pts3d,
        out Vector3 pos, out Quaternion rot, out Vector3 scale)
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

        // Scale chain: BaseScale → (FitSegmentLength multiplies segment length) → × RelativeScale.
        // 缩放：BaseScale → FitSegmentLength 乘段长 → × RelativeScale
        scale = seg.BaseScale;
        if (seg.FitSegmentLength)
        {
            if (seg.FitAxis == 0) scale.x *= segLen;
            else if (seg.FitAxis == 1) scale.y *= segLen;
            else scale.z *= segLen;
        }
        scale = Vector3.Scale(scale, seg.RelativeScale);

        return true;
    }
}