using UnityEditor;
using UnityEngine;

/// <summary>
/// SceneView curve interaction editor — hit testing (vertices/handles/curve spans/cursor pick).
/// SceneView 曲线交互编辑器 — 命中检测（顶点/控制柄/曲线段/游标拾取）。
/// </summary>
public static partial class CurveSceneEditor
{
    private struct HitResult
    {
        public int curveIndex, vertexIndex, subElement, segmentIndex;
    }

    /// <summary>Hits a vertex or handle end (priority: selected handles > unselected handles > selected vertices > unselected vertices > selected arrows > unselected arrows > locked).
    /// 命中顶点或曲柄端（按优先级：已选曲柄>未选曲柄>已选顶点>未选顶点>已选箭头>未选箭头>锁定）</summary>
    private static HitResult HitTest(CurveManager m, Event e, CurveTool w)
    {
        Vector2 mp = GetMouseWorldPos(e, w);
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float arrowLen = (CurveTool.Instance?.ArrowSize ?? 0.12f) * 3f;

        // Helper: test a single vertex (subType: 0=all, 1=handle, 2=vertex body, 3=Y arrow)
        // 辅助：检测单个顶点（subType: 0=全部, 1=曲柄, 2=顶点体, 3=Y箭头）
        HitResult TestVertex(CurveVertex v, BezierCurve cur, int ci, int vi, int subType = 0)
        {
            bool checkH = subType == 0 || subType == 1;
            bool checkV = subType == 0 || subType == 2;
            bool checkA = subType == 0 || subType == 3;
            if (cur.Is3D)
            {
                Vector3 p = v.PositionV3;
                Vector3 lh = p + v.LeftHandleOffsetV3;
                Vector3 rh = p + v.RightHandleOffsetV3;

                if (checkH && RayPointDist(ray, lh) < HandleHitRadius * HandleUtility.GetHandleSize(lh))
                    return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 1 };
                if (checkH && RayPointDist(ray, rh) < HandleHitRadius * HandleUtility.GetHandleSize(rh))
                    return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 2 };
                if (checkV && RayPointDist(ray, p) < VertexHitRadius * HandleUtility.GetHandleSize(p))
                    return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 3 };
                if (checkA)
                {
                    Vector3 arrowTip = p + Vector3.up * arrowLen;
                    if (RayPointDist(ray, arrowTip) < HandleHitRadius * HandleUtility.GetHandleSize(arrowTip))
                        return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 4 };
                    Vector3 lhArrow = lh + Vector3.up * arrowLen;
                    if (RayPointDist(ray, lhArrow) < HandleHitRadius * HandleUtility.GetHandleSize(lhArrow))
                        return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 5 };
                    Vector3 rhArrow = rh + Vector3.up * arrowLen;
                    if (RayPointDist(ray, rhArrow) < HandleHitRadius * HandleUtility.GetHandleSize(rhArrow))
                        return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 6 };
                }
            }
            else
            {
                // Hit radius scales with the view (same as the 3D branch) / 命中半径随视图缩放（与 3D 分支一致）
                float hScale = HandleUtility.GetHandleSize(cur.MapToWorld(v.Position));
                if (checkH && Vector2.Distance(mp, v.LeftHandlePosition) < HandleHitRadius * hScale)
                    return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 1 };
                if (checkH && Vector2.Distance(mp, v.RightHandlePosition) < HandleHitRadius * hScale)
                    return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 2 };
                if (checkV && Vector2.Distance(mp, v.Position) < VertexHitRadius * hScale)
                    return new HitResult { curveIndex = ci, vertexIndex = vi, subElement = 3 };
            }
            return new HitResult { curveIndex = -1 };
        }

        // 7-pass scan by priority: selected handles → unselected handles → selected vertices → unselected vertices → selected arrows → unselected arrows → locked
        // 7 轮扫描按优先级：已选曲柄 → 未选曲柄 → 已选顶点 → 未选顶点 → 已选箭头 → 未选箭头 → 锁定
        for (int pass = 0; pass < 7; pass++)
        {
            int subType;
            bool selOnly, unselOnly, lockedOnly;
            switch (pass)
            {
                case 0: subType = 1; selOnly = true;  unselOnly = false; lockedOnly = false; break;
                case 1: subType = 1; selOnly = false; unselOnly = true;  lockedOnly = false; break;
                case 2: subType = 2; selOnly = true;  unselOnly = false; lockedOnly = false; break;
                case 3: subType = 2; selOnly = false; unselOnly = true;  lockedOnly = false; break;
                case 4: subType = 3; selOnly = true;  unselOnly = false; lockedOnly = false; break;
                case 5: subType = 3; selOnly = false; unselOnly = true;  lockedOnly = false; break;
                default: subType = 0; selOnly = false; unselOnly = false; lockedOnly = true;  break;
            }

            for (int ci = 0; ci < m.Curves.Count; ci++)
            {
                var c = m.Curves[ci];
                if (!c.IsVisible) continue;
                if (lockedOnly && !c.IsLocked) continue;
                if (unselOnly && c.IsLocked) continue;
                // Project the mouse onto each curve's own editing plane (prevents cross-hits when axes differ)
        // 按当前曲线自身的编辑平面投影鼠标（多曲线轴向不同时命中不串位）
                mp = GetMouseWorldPos(e, w, c.UpAxis);
                for (int vi = 0; vi < c.Vertices.Count; vi++)
                {
                    var v = c.Vertices[vi];
                    if (selOnly && !v.IsSelected) continue;
                    if (unselOnly && v.IsSelected) continue;
                    var result = TestVertex(v, c, ci, vi, subType);
                    if (result.curveIndex >= 0) return result;
                }
            }
        }

        return new HitResult { curveIndex = -1 };
    }

    /// <summary>Hits a curve span (used for Alt+Right-click insert). / 命中曲线段（用于 Alt+右键插入）</summary>
    private static HitResult HitTestCurve(CurveManager m, Event e, CurveTool w)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        for (int ci = 0; ci < m.Curves.Count; ci++)
        {
            var c = m.Curves[ci];
            if (!c.IsVisible) continue;
            if (c.Vertices.Count < 2) continue;
            int spans = c.IsLoop ? c.Vertices.Count : c.Vertices.Count - 1;
            if (c.Is3D)
            {
                var pts3 = c.SamplePoints3D();
                for (int i = 0; i < pts3.Count - 1; i++)
                {
                    int spanIdx = i / c.SegmentCount;
                    if (spanIdx >= spans) break;
                    Vector3 mid = (pts3[i] + pts3[i + 1]) * 0.5f;
                    if (RayPointDist(ray, mid) < CurveHitRadius * HandleUtility.GetHandleSize(mid))
                        return new HitResult { curveIndex = ci, segmentIndex = spanIdx };
                }
            }
            else
            {
                Vector2 mp = GetMouseWorldPos(e, w, c.UpAxis);
                for (int s = 0; s < spans; s++)
                {
                    var v0 = c.Vertices[s];
                    var v1 = c.Vertices[(s + 1) % c.Vertices.Count];
                    Vector2 p0 = v0.Position, p1 = v0.RightHandlePosition;
                    Vector2 p2 = v1.LeftHandlePosition, p3 = v1.Position;
                    for (int i = 0; i < c.SegmentCount; i++)
                    {
                        float t = (float)i / c.SegmentCount;
                        float tn = (float)(i + 1) / c.SegmentCount;
                        Vector2 a = CubicBez(p0, p1, p2, p3, t);
                        Vector2 b = CubicBez(p0, p1, p2, p3, tn);
                        Vector2 mid = (a + b) * 0.5f;
                        if (PointToSegDist(mp, a, b) < CurveHitRadius * HandleUtility.GetHandleSize(c.MapToWorld(mid)))
                            return new HitResult { curveIndex = ci, segmentIndex = s };
                    }
                }
            }
        }
        return new HitResult { curveIndex = -1 };
    }

    /// <summary>Hits a micro-segment (left-click), testing the segment midpoint. / 命中小线段（左键），判断点为线段中心</summary>
    private static HitResult HitTestSegment(CurveManager m, Event e, CurveTool w)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        for (int ci = 0; ci < m.Curves.Count; ci++)
        {
            var c = m.Curves[ci];
            if (!c.IsVisible) continue;
            if (c.Is3D)
            {
                var pts3 = c.SamplePoints3D();
                for (int i = 0; i < pts3.Count - 1 && i < c.Segments.Count; i++)
                {
                    Vector3 mid = (pts3[i] + pts3[i + 1]) * 0.5f;
                    if (RayPointDist(ray, mid) < SegmentHitRadius * HandleUtility.GetHandleSize(mid))
                        return new HitResult { curveIndex = ci, segmentIndex = i };
                }
            }
            else
            {
                var pts = c.SamplePoints();
                Vector2 mp = GetMouseWorldPos(e, w, c.UpAxis);
                for (int i = 0; i < pts.Count - 1 && i < c.Segments.Count; i++)
                {
                    Vector2 mid = (pts[i] + pts[i + 1]) * 0.5f;
                    if (Vector2.Distance(mp, mid) < SegmentHitRadius * HandleUtility.GetHandleSize(c.MapToWorld(mid)))
                        return new HitResult { curveIndex = ci, segmentIndex = i };
                }
            }
        }
        return new HitResult { curveIndex = -1 };
    }

    /// <summary>Hits the cursor sphere (ray-sphere intersection). / 命中游标球体（射线-球体求交）</summary>
    private static bool HitTestCursor(CurveManager m, Event e)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        // Distance from the ray to the sphere center / 射线到球心最近点距离
        Vector3 toCenter = m.CursorPosition - ray.origin;
        float t = Vector3.Dot(toCenter, ray.direction);
        if (t < 0f) return false;
        Vector3 closest = ray.GetPoint(t);
        float dist = Vector3.Distance(closest, m.CursorPosition);
        // Use the screen-space display size as the hit radius / 使用屏幕空间的显示大小作为命中半径
        float hitRadius = CursorHitRadius * HandleUtility.GetHandleSize(m.CursorPosition);
        return dist < hitRadius;
    }
}
