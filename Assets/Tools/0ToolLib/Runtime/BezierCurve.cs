using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A complete Bezier curve — pure virtual data, creates no GameObjects.
/// 一条完整的贝塞尔曲线 — 纯虚拟数据，不产生 GameObject。
/// </summary>
[System.Serializable]
public class BezierCurve : ISerializationCallbackReceiver
{
    public string Name;
    /// <summary>Whether this is a 3D curve (Plane is ignored; vertices use Height).
    /// 是否 3D 曲线（3D 时 Plane 无效，顶点使用 Height）</summary>
    public bool Is3D;
    /// <summary>[Legacy] Kept only for scene-data migration; use Plane instead. / [废弃] 仅为场景数据迁移保留；请用 Plane。</summary>
    [Obsolete("Use Plane instead of UpAxis.")]
    public UpAxis UpAxis = UpAxis.Y;
    /// <summary>World plane the (2D) curve is drawn on. / 2D 曲线所在的世界平面。</summary>
    public CurvePlane Plane = CurvePlane.XZ;
    public List<CurveVertex> Vertices = new List<CurveVertex>();
    public List<SegmentInfo> Segments = new List<SegmentInfo>();
    public int SegmentCount = 16;
    public bool IsLoop;
    public bool IsEnabled = true;
    public bool IsVisible = true;
    public bool IsLocked;

    [NonSerialized]
    public bool IsSelected;
    /// <summary>Selected segment index (sampled segment; -1 = none). / 选中的段索引（采样点段，-1=无）</summary>
    [NonSerialized]
    public int SelectedSegmentIndex = -1;

    public static BezierCurve CreateDefault(string name = "NewCurve")
    {
        var curve = new BezierCurve { Name = name };
        curve.Vertices.Add(new CurveVertex(new Vector2(-5f, 0f)));
        curve.Vertices.Add(new CurveVertex(new Vector2(5f, 0f)));
        curve.RebuildSegments();
        return curve;
    }

    /// <summary>Creates a default 3D curve (vertices on the XZ plane, Height as elevation).
    /// 创建默认 3D 曲线（顶点在 XZ 平面上，Height 为高度）</summary>
    public static BezierCurve CreateDefault3D(string name = "New3DCurve")
    {
        var curve = new BezierCurve { Name = name, Is3D = true };
        curve.Vertices.Add(new CurveVertex(new Vector2(-5f, 0f)) { Height = 0f });
        curve.Vertices.Add(new CurveVertex(new Vector2(5f, 0f)) { Height = 0f });
        curve.RebuildSegments();
        return curve;
    }

    /// <summary>One-time migration: lift a non-default legacy UpAxis (Y/Z/X) into Plane (XZ/XY/YZ).
    /// The legacy field is kept untouched for re-serialization safety.
    /// 一次性迁移：把非默认的旧 UpAxis（Y/Z/X）提升到 Plane（XZ/XY/YZ）。旧字段保持不变以保证重新序列化安全。</summary>
    public void OnAfterDeserialize()
    {
#pragma warning disable 0618 // UpAxis is kept only for serialization migration
        if (Plane == CurvePlane.XZ && UpAxis != UpAxis.Y)
            Plane = (CurvePlane)(int)UpAxis;
#pragma warning restore 0618
    }

    public void OnBeforeSerialize() { }

    /// <summary>Rebuilds the Segments list from the current vertices and SegmentCount.
    /// 根据当前顶点和 SegmentCount 重建 Segments 列表</summary>
    public void RebuildSegments()
    {
        int total = Mathf.Max(0, TotalSegmentCount);
        while (Segments.Count < total) Segments.Add(SegmentInfo.Default);
        if (Segments.Count > total) Segments.RemoveRange(total, Segments.Count - total);
    }

    /// <summary>Total micro-segment count = spans × SegmentCount. / 小线段总数 = 跨段数 × SegmentCount</summary>
    public int TotalSegmentCount
    {
        get
        {
            if (Vertices.Count < 2) return 0;
            int spans = IsLoop ? Vertices.Count : Vertices.Count - 1;
            return spans * SegmentCount;
        }
    }

    /// <summary>Samples all micro-segment endpoints along the curve (including both ends).
    /// 沿曲线采样所有小线段端点（含首尾）</summary>
    public List<Vector2> SamplePoints()
    {
        var points = new List<Vector2>();
        if (Vertices.Count < 2) return points;

        int spanCount = IsLoop ? Vertices.Count : Vertices.Count - 1;
        for (int s = 0; s < spanCount; s++)
        {
            var v0 = Vertices[s];
            var v1 = Vertices[(s + 1) % Vertices.Count];
            Vector2 p0 = v0.Position, p1 = v0.RightHandlePosition;
            Vector2 p2 = v1.LeftHandlePosition, p3 = v1.Position;

            bool isLast = (s == spanCount - 1);
            int endI = isLast && !IsLoop ? SegmentCount : SegmentCount - 1;
            for (int i = 0; i <= endI; i++)
            {
                float t = Mathf.Clamp01((float)i / SegmentCount);
                points.Add(CubicBezier(p0, p1, p2, p3, t));
            }
        }
        if (IsLoop && Vertices.Count >= 2)
            points.Add(Vertices[0].Position);
        return points;
    }

    /// <summary>Samples all micro-segment endpoints as 3D world positions.
    /// 沿曲线采样所有小线段端点的 3D 世界坐标</summary>
    public List<Vector3> SamplePoints3D()
    {
        var pts2d = SamplePoints();
        var result = new List<Vector3>(pts2d.Count);
        if (!Is3D)
        {
            for (int i = 0; i < pts2d.Count; i++)
                result.Add(MapToWorld(pts2d[i]));
            return result;
        }

        // 3D curves: sample the Z values and combine as (x, z, y) / 3D 曲线：采样 Z 值并组合为 (x, z, y)
        var zs = SamplePointZ();
        for (int i = 0; i < pts2d.Count && i < zs.Count; i++)
            result.Add(new Vector3(pts2d[i].x, zs[i], pts2d[i].y));
        return result;
    }

    /// <summary>Samples the Z values of all micro-segment endpoints (for 3D curves).
    /// 采样所有小线段端点的 Z 值（3D 曲线用）</summary>
    public List<float> SamplePointZ()
    {
        var zs = new List<float>();
        if (Vertices.Count < 2) return zs;
        int spanCount = IsLoop ? Vertices.Count : Vertices.Count - 1;
        for (int s = 0; s < spanCount; s++)
        {
            var v0 = Vertices[s];
            var v1 = Vertices[(s + 1) % Vertices.Count];
            float p0z = v0.Height, p1z = v0.Height + v0.RightHandleHeight;
            float p2z = v1.Height + v1.LeftHandleHeight, p3z = v1.Height;

            bool isLast = (s == spanCount - 1);
            int endI = isLast && !IsLoop ? SegmentCount : SegmentCount - 1;
            for (int i = 0; i <= endI; i++)
            {
                float t = Mathf.Clamp01((float)i / SegmentCount);
                zs.Add(CubicBezier1D(p0z, p1z, p2z, p3z, t));
            }
        }
        if (IsLoop && Vertices.Count >= 2)
            zs.Add(Vertices[0].Height);
        return zs;
    }

    private const float HandleLengthFactor = 1f / 3f;

    /// <summary>Recalculates handles by HandleTypeA/B (Auto/Vector are recomputed; open endpoints mirror the connected side).
    /// 根据 HandleTypeA/B 重新计算各侧控制柄（Auto/Vector 被自动覆盖）。端点断开侧镜像连接侧。</summary>
    public void RecalculateHandles()
    {
        int n = Vertices.Count;
        if (n < 2) return;

        // 2-vertex loop: approximated as a circle (tangent direction + 1/3 chord) / 2 顶点闭环：近似为圆形（切线方向 + 1/3 弦长）
        if (IsLoop && n == 2)
        {
            var v0 = Vertices[0];
            var v1 = Vertices[1];
            Vector2 dir = v1.Position - v0.Position;
            float dh = Is3D ? v1.Height - v0.Height : 0f;
            float dist3D = Mathf.Sqrt(dir.sqrMagnitude + dh * dh);
            float len = dist3D * 2f / 3f;
            // SafeNormalize prevents NaN when two vertices coincide (zero direction) / 用 SafeNormalize 防止两顶点重合时 dir 为零向量导致 NaN
            Vector2 tangent = SafeNormalize(new Vector2(-dir.y, dir.x)) * len;

            // Only Auto gets the circular tangent; Vector gets the single-neighbor direction; other types stay untouched
            // 仅 Auto 类型赋圆形切线值，Vector 赋单邻居方向，其他类型不修改
            if (v0.HandleTypeA == HandleType.Auto)
                v0.LeftHandle = -tangent;
            else if (v0.HandleTypeA == HandleType.Vector)
                v0.LeftHandle = (Vertices[1].Position - v0.Position).normalized * (Vector2.Distance(v0.Position, Vertices[1].Position) / 3f);
            if (v0.HandleTypeB == HandleType.Auto)
                v0.RightHandle = tangent;
            else if (v0.HandleTypeB == HandleType.Vector)
                v0.RightHandle = (Vertices[1].Position - v0.Position).normalized * (Vector2.Distance(v0.Position, Vertices[1].Position) / 3f);
            if (v1.HandleTypeA == HandleType.Auto)
                v1.LeftHandle = tangent;
            else if (v1.HandleTypeA == HandleType.Vector)
                v1.LeftHandle = (Vertices[0].Position - v1.Position).normalized * (Vector2.Distance(v1.Position, Vertices[0].Position) / 3f);
            if (v1.HandleTypeB == HandleType.Auto)
                v1.RightHandle = -tangent;
            else if (v1.HandleTypeB == HandleType.Vector)
                v1.RightHandle = (Vertices[0].Position - v1.Position).normalized * (Vector2.Distance(v1.Position, Vertices[0].Position) / 3f);

            if (Is3D)
            {
                v0.LeftHandleHeight = 0f;
                v0.RightHandleHeight = 0f;
                v1.LeftHandleHeight = 0f;
                v1.RightHandleHeight = 0f;
            }

            // Mirror/aligned: sync values from the opposite side / 镜像/对齐：从对侧同步值
            if (v0.HandleTypeA == HandleType.AlignedLength) v0.LeftHandle = -v0.RightHandle;
            else if (v0.HandleTypeA == HandleType.Aligned && v0.RightHandle.magnitude > 0.0001f)
                v0.LeftHandle = -v0.RightHandle.normalized * v0.LeftHandle.magnitude;
            if (v0.HandleTypeB == HandleType.AlignedLength) v0.RightHandle = -v0.LeftHandle;
            else if (v0.HandleTypeB == HandleType.Aligned && v0.LeftHandle.magnitude > 0.0001f)
                v0.RightHandle = -v0.LeftHandle.normalized * v0.RightHandle.magnitude;
            if (v1.HandleTypeA == HandleType.AlignedLength) v1.LeftHandle = -v1.RightHandle;
            else if (v1.HandleTypeA == HandleType.Aligned && v1.RightHandle.magnitude > 0.0001f)
                v1.LeftHandle = -v1.RightHandle.normalized * v1.LeftHandle.magnitude;
            if (v1.HandleTypeB == HandleType.AlignedLength) v1.RightHandle = -v1.LeftHandle;
            else if (v1.HandleTypeB == HandleType.Aligned && v1.LeftHandle.magnitude > 0.0001f)
                v1.RightHandle = -v1.LeftHandle.normalized * v1.RightHandle.magnitude;

            return;
        }

        for (int i = 0; i < n; i++)
        {
            var v = Vertices[i];
            bool leftAutoVec = v.HandleTypeA == HandleType.Auto || v.HandleTypeA == HandleType.Vector;
            bool rightAutoVec = v.HandleTypeB == HandleType.Auto || v.HandleTypeB == HandleType.Vector;
            if (!leftAutoVec && !rightAutoVec) continue;

            int prevI = i - 1, nextI = i + 1;
            if (IsLoop)
            {
                if (prevI < 0) prevI = n - 1;
                if (nextI >= n) nextI = 0;
            }

            bool hasPrev = IsLoop || i > 0;
            bool hasNext = IsLoop || i < n - 1;
            bool isEndpoint = !IsLoop && (i == 0 || i == n - 1);

            // Left handle (auto-computed only when HandleTypeA is Auto/Vector) / 左柄（仅当 HandleTypeA 为 Auto/Vector 时自动计算）
            if (leftAutoVec)
            {
                if (hasPrev)
                {
                    Vector2 dir;
                    if (v.HandleTypeA == HandleType.Auto && ((IsLoop && prevI != nextI) || (i > 0 && i < n - 1)))
                        dir = SafeNormalize(Vertices[prevI].Position - Vertices[nextI].Position);
                    else
                        dir = SafeNormalize(Vertices[prevI].Position - Vertices[i].Position);
                    float dist = Vector2.Distance(v.Position, Vertices[prevI].Position);
                    v.LeftHandle = dir * (dist * HandleLengthFactor);
                }
                else v.LeftHandle = Vector2.zero;
            }

            // Right handle (auto-computed only when HandleTypeB is Auto/Vector) / 右柄（仅当 HandleTypeB 为 Auto/Vector 时自动计算）
            if (rightAutoVec)
            {
                if (hasNext)
                {
                    Vector2 dir;
                    if (v.HandleTypeB == HandleType.Auto && ((IsLoop && prevI != nextI) || (i > 0 && i < n - 1)))
                        dir = SafeNormalize(Vertices[nextI].Position - Vertices[prevI].Position);
                    else
                        dir = SafeNormalize(Vertices[nextI].Position - Vertices[i].Position);
                    float dist = Vector2.Distance(v.Position, Vertices[nextI].Position);
                    v.RightHandle = dir * (dist * HandleLengthFactor);
                }
                else v.RightHandle = Vector2.zero;
            }

            // Endpoint auto-mirror: the open-side handle aligns with the connected side / 端点自动镜像：断开侧柄对齐连接侧
            if (isEndpoint)
            {
                if (i == 0 && hasNext && leftAutoVec)
                    v.LeftHandle = -v.RightHandle;
                else if (i == n - 1 && hasPrev && rightAutoVec)
                    v.RightHandle = -v.LeftHandle;
            }

            // 3D curves: auto-compute the height-direction handles / 3D 曲线：自动计算高度方向控制柄
            if (Is3D)
            {
                if (leftAutoVec)
                {
                    if (hasPrev)
                    {
                        float hDir;
                        if (v.HandleTypeA == HandleType.Auto && ((IsLoop && prevI != nextI) || (i > 0 && i < n - 1)))
                            hDir = (Vertices[prevI].Height - Vertices[nextI].Height) * 0.5f;
                        else
                            hDir = Vertices[prevI].Height - Vertices[i].Height;
                        v.LeftHandleHeight = hDir * HandleLengthFactor;
                    }
                    else v.LeftHandleHeight = 0f;
                }
                if (rightAutoVec)
                {
                    if (hasNext)
                    {
                        float hDir;
                        if (v.HandleTypeB == HandleType.Auto && ((IsLoop && prevI != nextI) || (i > 0 && i < n - 1)))
                            hDir = (Vertices[nextI].Height - Vertices[prevI].Height) * 0.5f;
                        else
                            hDir = Vertices[nextI].Height - Vertices[i].Height;
                        v.RightHandleHeight = hDir * HandleLengthFactor;
                    }
                    else v.RightHandleHeight = 0f;
                }
                if (isEndpoint)
                {
                    if (i == 0 && hasNext && leftAutoVec) v.LeftHandleHeight = -v.RightHandleHeight;
                    else if (i == n - 1 && hasPrev && rightAutoVec) v.RightHandleHeight = -v.LeftHandleHeight;
                }
            }
        }

        // Mirror/aligned: sync from the opposite side (runs after Auto/Vector computation regardless of the other side's type)
        // 镜像/对齐：从对侧同步值（无论对侧类型，在 Auto/Vector 计算完成后执行）
        for (int i = 0; i < n; i++)
        {
            var v = Vertices[i];
            if (v.HandleTypeA == HandleType.AlignedLength)
                v.LeftHandle = -v.RightHandle;
            else if (v.HandleTypeA == HandleType.Aligned && v.RightHandle.magnitude > 0.0001f)
                v.LeftHandle = -v.RightHandle.normalized * v.LeftHandle.magnitude;
            if (v.HandleTypeB == HandleType.AlignedLength)
                v.RightHandle = -v.LeftHandle;
            else if (v.HandleTypeB == HandleType.Aligned && v.LeftHandle.magnitude > 0.0001f)
                v.RightHandle = -v.LeftHandle.normalized * v.RightHandle.magnitude;
        }
    }

    private static Vector2 SafeNormalize(Vector2 v)
    {
        float len = v.magnitude;
        return len < 1e-6f ? Vector2.zero : v / len;
    }

    /// <summary>Vector2 → 3D world position. / Vector2 → 3D 世界坐标</summary>
    public Vector3 MapToWorld(Vector2 pt) => Plane switch
    {
        CurvePlane.XZ => new Vector3(pt.x, 0f, pt.y),
        CurvePlane.XY => new Vector3(pt.x, pt.y, 0f),
        CurvePlane.YZ => new Vector3(0f, pt.x, pt.y),
        _ => new Vector3(pt.x, 0f, pt.y),
    };

    /// <summary>3D world position → Vector2. / 3D 世界坐标 → Vector2</summary>
    public Vector2 MapFromWorld(Vector3 w) => Plane switch
    {
        CurvePlane.XZ => new Vector2(w.x, w.z),
        CurvePlane.XY => new Vector2(w.x, w.y),
        CurvePlane.YZ => new Vector2(w.y, w.z),
        _ => new Vector2(w.x, w.z),
    };

    /// <summary>Normal of the editing plane (used for handle orientation). / 编辑平面的法线（用于 Handles 朝向）</summary>
    public Vector3 PlaneNormal => Plane switch
    {
        CurvePlane.XZ => Vector3.up,
        CurvePlane.XY => Vector3.forward,
        CurvePlane.YZ => Vector3.right,
        _ => Vector3.up,
    };

    private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t, tt = t * t, uu = u * u;
        return uu * u * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + tt * t * p3;
    }

    /// <summary>1D cubic Bezier interpolation (used for the Z component of 3D curves).
    /// 1D 三次贝塞尔插值（用于 3D 曲线的 Z 分量）</summary>
    private static float CubicBezier1D(float p0, float p1, float p2, float p3, float t)
    {
        float u = 1f - t, tt = t * t, uu = u * u;
        return uu * u * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + tt * t * p3;
    }
}
