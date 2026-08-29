using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SceneView curve renderer — draws virtual curves and preview wireframes.
/// SceneView 曲线渲染器 — 绘制虚拟曲线及预览线框。
/// Colors: curve=white, vertex=black, handle line=green, handle end=red, selected=yellow, selected segment=blue.
/// 颜色：曲线=白, 顶点=黑, 曲柄线=绿, 曲柄端=红, 选中=黄, 选中线段=蓝。
/// </summary>
public static class CurveSceneRenderer
{
    private static float VertexSize => CurveTool.Instance?.VertexSize ?? 0.2f;
    private static float HandleEndSize => CurveTool.Instance?.HandleEndSize ?? 0.12f;
    private static Color CurveColor => CurveTool.Instance?.CurveLineColor ?? Color.white;
    private static Color VertexColor => CurveTool.Instance?.VertexPointColor ?? Color.black;
    private static Color HandleLineColor => CurveTool.Instance?.HandleLineColor ?? Color.green;
    private static Color HandleEndColor => CurveTool.Instance?.HandleEndPointColor ?? Color.red;
    private static Color SelectedColor => CurveTool.Instance?.SelectedColor ?? Color.yellow;
    private static Color SelectedSegmentColor => CurveTool.Instance?.SelectedSegmentColor ?? new Color(0.3f, 0.5f, 1f, 0.8f);
    private static Color PreviewWireColor => CurveTool.Instance?.GenerationColor ?? CurveTool.DefaultGenerationColor;
    private static float CursorSize => CurveTool.Instance?.CursorDisplaySize ?? 0.15f;

    /// <summary>Returns the handle line color by handle type (high-contrast color scale).
    /// 根据控制柄类型返回线颜色（高对比度色标）</summary>
    private static Color GetHandleLineColor(HandleType type)
    {
        return type switch
        {
            HandleType.Auto => HandleLineColor,
            HandleType.Aligned => Color.blue,
            HandleType.AlignedLength => Color.cyan,
            HandleType.Vector => new Color(1f, 0.5f, 0f),
            HandleType.Free => Color.gray,
            _ => HandleLineColor,
        };
    }

    /// <summary>Color of the advanced-fit center reference line (the reflex/outer-angle bisector guide).
    /// 进阶适应中心参考线颜色（反射角/外角平分线引导线）</summary>
    private static readonly Color CenterLineColor = new Color(1f, 0.45f, 0.1f, 0.95f);

    /// <summary>Color highlighting the primary side of an advanced-fit footprint. / 进阶足迹主侧的高亮颜色</summary>
    private static readonly Color PrimaryLineColor = Color.yellow;

    /// <summary>Safe normalize for Vector3 (returns zero vector when the input is too short, avoiding NaN).
    /// Vector3 安全归一化（过短时返回零向量，避免 NaN）</summary>
    private static Vector3 SafeNormalize3(Vector3 v)
    {
        float len = v.magnitude;
        return len < 1e-6f ? Vector3.zero : v / len;
    }

    /// <summary>Whether the curve has any Advanced-fit micro-segment (FitMode == 1).
    /// 曲线是否含进阶适应小段（FitMode == 1）</summary>
    private static bool HasAdvancedFit(BezierCurve curve)
    {
        if (curve == null || curve.Segments == null) return false;
        for (int i = 0; i < curve.Segments.Count; i++)
            if (curve.Segments[i].FitMode == 1) return true;
        return false;
    }

    public static void Register() => SceneView.duringSceneGui += OnSceneGUI;
    public static void Unregister() => SceneView.duringSceneGui -= OnSceneGUI;

    private static void OnSceneGUI(SceneView sv)
    {
        var w = CurveTool.Instance;
        if (w == null) return;
        if (!w.IsEditMode && w.PreviewStyle == PreviewStyle.Off) return;
        var m = CurveManager.Instance;
        if (m == null) return;

        if (w.IsEditMode)
        {
            // Edit mode: full rendering / 编辑模式：完整渲染
            if (m.Curves.Count > 0)
            {
                foreach (var curve in m.Curves)
                {
                    if (!curve.IsVisible) continue;
                    DrawCurveLine(curve);
                    DrawVerticesAndHandles(curve);
                    if (HasAdvancedFit(curve)) DrawFitReferenceLines(curve);
                    if (w.PreviewStyle == PreviewStyle.Wireframe) DrawPreviewWireframes(curve);
                    else if (w.PreviewStyle == PreviewStyle.Triangles) DrawPreviewTriangleMeshes(curve);
                }
            }
            DrawCursor(m);
        }
        else if (w.PreviewStyle != PreviewStyle.Off)
        {
            // Preview-only mode: render previews only / 仅预览模式：只渲染预览
            foreach (var curve in m.Curves)
            {
                if (!curve.IsVisible) continue;
                if (w.PreviewStyle == PreviewStyle.Wireframe) DrawPreviewWireframes(curve);
                else if (w.PreviewStyle == PreviewStyle.Triangles) DrawPreviewTriangleMeshes(curve);
            }
        }
    }

    /// <summary>Draws the curve polyline, colored by segment selection/lock state.
    /// 绘制曲线折线，按段选中/锁定状态着色</summary>
    private static void DrawCurveLine(BezierCurve curve)
    {
        bool is3d = curve.Is3D;
        if (is3d)
        {
            var pts3 = curve.SamplePoints3D();
            if (pts3.Count < 2) return;
            Color lockedColor = new Color(1f, 0.6f, 0.2f);
            for (int i = 0; i < pts3.Count - 1; i++)
            {
                bool segSel = i < curve.Segments.Count && curve.Segments[i].IsSelected;
                Handles.color = curve.IsLocked ? lockedColor : segSel ? SelectedSegmentColor : CurveColor;
                Handles.DrawLine(pts3[i], pts3[i + 1], 2.5f);
            }
            return;
        }

        var pts = curve.SamplePoints();
        if (pts.Count < 2) return;

        Color lockedColor2 = new Color(1f, 0.6f, 0.2f);

        for (int i = 0; i < pts.Count - 1; i++)
        {
            bool segSel = i < curve.Segments.Count && curve.Segments[i].IsSelected;
            Handles.color = curve.IsLocked ? lockedColor2 :
                            segSel ? SelectedSegmentColor : CurveColor;
            Vector3 a = curve.MapToWorld(pts[i]);
            Vector3 b = curve.MapToWorld(pts[i + 1]);
            Handles.DrawLine(a, b, 2.5f);
        }
    }

    /// <summary>Draws vertices and handles. / 绘制顶点和曲柄</summary>
    private static void DrawVerticesAndHandles(BezierCurve curve)
    {
        bool is3d = curve.Is3D;
        var normal = curve.PlaneNormal;
        for (int i = 0; i < curve.Vertices.Count; i++)
        {
            var v = curve.Vertices[i];
            Vector3 pos = is3d ? v.PositionV3 : curve.MapToWorld(v.Position);
            Vector3 lh = is3d ? v.PositionV3 + v.LeftHandleOffsetV3 : curve.MapToWorld(v.LeftHandlePosition);
            Vector3 rh = is3d ? v.PositionV3 + v.RightHandleOffsetV3 : curve.MapToWorld(v.RightHandlePosition);

            if (!curve.IsLocked)
            {
                // 3D Y-axis height arrows (green, pointing Y+) — rendered before handles and vertices
        // 3D 曲线 Y 轴高度箭头（绿色，指向 Y+）— 在曲柄和顶点之前渲染
                float arrowSize = CurveTool.Instance?.ArrowSize ?? 0.12f;
                float arrowLen = arrowSize * 3f;
                float coneSize = arrowSize * 0.75f;
                if (is3d)
                {
                    Vector3 arrowTip = pos + Vector3.up * arrowLen;
                    bool arrowSel = v.IsSelected && v.SelectedSubElement == 4;
                    Handles.color = arrowSel ? SelectedColor : Color.green;
                    Handles.DrawLine(pos, arrowTip, 2f);
                    Handles.ConeHandleCap(0, arrowTip, Quaternion.LookRotation(Vector3.up), coneSize, EventType.Repaint);

                    Vector3 lhArrow = lh + Vector3.up * arrowLen;
                    bool lhSel = v.IsSelected && v.SelectedSubElement == 5;
                    Handles.color = lhSel ? SelectedColor : Color.green;
                    Handles.DrawLine(lh, lhArrow, 2f);
                    Handles.ConeHandleCap(0, lhArrow, Quaternion.LookRotation(Vector3.up), coneSize, EventType.Repaint);

                    Vector3 rhArrow = rh + Vector3.up * arrowLen;
                    bool rhSel = v.IsSelected && v.SelectedSubElement == 6;
                    Handles.color = rhSel ? SelectedColor : Color.green;
                    Handles.DrawLine(rh, rhArrow, 2f);
                    Handles.ConeHandleCap(0, rhArrow, Quaternion.LookRotation(Vector3.up), coneSize, EventType.Repaint);
                }

                // Handle lines — colored by HandleType (the line keeps its color when selected; only the endpoint changes)
        // 曲柄线 — 按 HandleType 着色（选中时线不变色，仅端点变色）
                Handles.color = GetHandleLineColor(v.HandleTypeA);
                Handles.DrawLine(pos, lh, 2f);
                Handles.color = GetHandleLineColor(v.HandleTypeB);
                Handles.DrawLine(pos, rh, 2f);

                // Handle endpoints (spheres) — default color, yellow when selected / 曲柄端点（球形）— 始终用默认端点色，仅选中时变黄
                Handles.color = v.SelectedSubElement == 1 ? SelectedColor : HandleEndColor;
                Handles.SphereHandleCap(0, lh, Quaternion.identity, HandleEndSize, EventType.Repaint);
                Handles.color = v.SelectedSubElement == 2 ? SelectedColor : HandleEndColor;
                Handles.SphereHandleCap(0, rh, Quaternion.identity, HandleEndSize, EventType.Repaint);
            }

            // Vertices (orange when locked) / 顶点（锁定时橙色）
            Color vtxCol = curve.IsLocked ? new Color(1f, 0.6f, 0.2f) : VertexColor;
            Handles.color = v.IsSelected && v.SelectedSubElement == 3 ? SelectedColor : vtxCol;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, VertexSize, EventType.Repaint);
        }
    }

    /// <summary>
    /// Draws the advanced-fit reference lines for a 2D curve:
    /// 1. Parallel reference lines per micro-segment (both sides, distance = FitSizeScale), truncated at the
    ///    reflex/perpendicular lines bounding that micro-segment.
    /// 2. Reflex-angle lines (at junctions) and endpoint perpendicular lines, truncated at the adjacent
    ///    parallel lines (else a max-length fallback when no intersection exists).
    /// 绘制 2D 曲线的进阶适应参考线：
    /// 1. 每个小线段两侧的平行参考线（距离 = 适应尺寸缩放），在该小线段首尾两条参考线处截断；
    /// 2. 反射角线（交界处）与端点垂线，在与相邻平行参考线的交点处截断（无交点则用最大长度兜底）。</summary>
    private static void DrawFitReferenceLines(BezierCurve curve)
    {
        if (curve == null || curve.Is3D) return;
        var pts = curve.SamplePoints();
        if (pts.Count < 3) return;
        int n = pts.Count;
        Vector3 normal = curve.PlaneNormal;
        bool isLoop = curve.IsLoop;

        // Which micro-segments are Advanced (FitMode == 1): only these show reference lines, and only their
        // adjacent corner reflex/perpendicular lines are shown. / 哪些小线段为进阶（FitMode==1）：仅这些显示参考线，
        // 且仅显示它们相邻两角的反射角线/端点垂线。
        int segCount = curve.Segments != null ? curve.Segments.Count : 0;
        bool[] isAdv = new bool[Mathf.Max(0, n - 1)];
        for (int m = 0; m < isAdv.Length && m < segCount; m++)
            isAdv[m] = curve.Segments[m].FitMode == 1;

        // Build one reference line per sample point: reflex at an interior junction (and the loop joint),
        // perpendicular at an open-curve endpoint. / 建立每个采样点的参考线：内部交界（及闭环连接点）为反射角线，
        // 开放曲线端点为垂线。
        var origin = new Vector3[n];
        var ldir = new Vector3[n];
        var has = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (isLoop && i == n - 1) continue; // wrap point == pts[0]; skip duplicate / 环绕点即 pts[0]，跳过
            bool endpoint = !isLoop && (i == 0 || i == n - 1);
            Vector3 b = curve.MapToWorld(pts[i]);
            if (endpoint)
            {
                int adj = i == 0 ? 1 : n - 2;
                Vector3 sd = SafeNormalize3(curve.MapToWorld(pts[adj]) - b);
                if (sd.sqrMagnitude < 1e-10f) continue;
                Vector3 perp = SafeNormalize3(Vector3.Cross(normal, sd));
                if (perp.sqrMagnitude < 1e-10f) continue;
                origin[i] = b; ldir[i] = perp; has[i] = true;
            }
            else
            {
                Vector3 a, c;
                if (isLoop && i == 0) { a = curve.MapToWorld(pts[n - 2]); c = curve.MapToWorld(pts[1]); }
                else { a = curve.MapToWorld(pts[i - 1]); c = curve.MapToWorld(pts[i + 1]); }
                Vector3 uIn = SafeNormalize3(b - a);
                Vector3 vOut = SafeNormalize3(c - b);
                if (uIn.sqrMagnitude < 1e-10f || vOut.sqrMagnitude < 1e-10f) continue;
                Vector3 bdir;
                if (Vector3.Dot(uIn, vOut) > 0.9999f)
                {
                    // Straight (both angles 180°): use a perpendicular boundary here (the "both-sides" reference),
                    // so a straight-span micro-segment gets a simple bounded rectangle.
                    // 直线贯通（内外角同为180°）：此处用垂线作为边界（双向参考线），直线段小段得到简单有界矩形。
                    bdir = SafeNormalize3(Vector3.Cross(normal, uIn));
                }
                else
                {
                    // Reflex-angle center line: bisector of the outer/gap angle, pointing into the gap.
                    // 反射角中心参考线：外角（缺口侧）的角平分线方向，指向缺口。
                    bdir = SafeNormalize3(uIn - vOut);
                }
                if (bdir.sqrMagnitude < 1e-10f) continue;
                origin[i] = b; ldir[i] = bdir; has[i] = true;
            }
        }

        // Advanced-fit footprint per micro-segment: the primary/secondary side parallels truncated at the two
        // bounding reflex/perpendicular lines, drawn as a quadrilateral of 4 corners. The primary side is highlighted.
        // 每个进阶小线段的进阶足迹：主/副侧平行线与首尾两条参考线（反射角线/端点垂线）的交点构成 4 角点四角形；
        // 主侧高亮显示（第二步：主/副侧判定 + 4 交点）。
        for (int m = 0; m < n - 1; m++)
        {
            if (!isAdv[m]) continue; // only advanced micro-segments / 仅进阶小线段
            if (!TryGetFootprintCorners(curve, pts, m, n, isLoop, normal, origin, ldir, has, out Vector3[] c))
                continue;

            // Secondary edge + the two cross edges (reflex/endpoint-perpendicular truncated at the parallels).
            // 副侧边 + 两条横截边（反射/端点垂线在平行线处截断）。
            Handles.color = CenterLineColor;
            Handles.DrawLine(c[3], c[2]); // secondary parallel edge / 副侧平行边
            Handles.DrawLine(c[1], c[2]); // end cross edge / 末端横边（终点反射/垂线）
            Handles.DrawLine(c[0], c[3]); // start cross edge / 首端横边（起点反射/垂线）

            // Primary side — highlighted. / 主侧 — 高亮。
            Handles.color = PrimaryLineColor;
            Handles.DrawLine(c[0], c[1]);

            // Corner markers. / 角点标记。
            Handles.color = CenterLineColor;
            for (int k = 0; k < 4; k++)
                Handles.SphereHandleCap(0, c[k], Quaternion.identity, 0.03f, EventType.Repaint);
        }
    }

    /// <summary>
    /// Computes the rectangle footprint of an advanced micro-segment: the bounding parallelogram of the 4 raw
    /// reflex-line intersections is snapped to the segment frame (along = min/max of the intersections projected
    /// onto the segment direction, across = ±FitSizeScale), so the object renders as a rectangle. At a bend this
    /// stretches toward the reflex center (fills the outer gap) while the inner side overlaps (clips).
    /// 计算进阶小线段的矩形足迹：把 4 个原始反射线交点归整到小线段坐标系（沿段方向取交点的 min/max 跨度，
    /// 横跨 ±FitSizeScale），使物体呈矩形。弯折处向反射角中心拉伸（填外角缺口），内角则重叠（穿模）。
    /// </summary>
    private static bool TryGetFootprintCorners(BezierCurve curve, List<Vector2> pts, int m, int n, bool isLoop,
        Vector3 normal, Vector3[] refOrigin, Vector3[] refDir, bool[] hasRef,
        out Vector3[] corners)
    {
        corners = new Vector3[4];
        Vector3 a = curve.MapToWorld(pts[m]);
        Vector3 b = curve.MapToWorld(pts[m + 1]);
        Vector3 dir = SafeNormalize3(b - a);
        if (dir.sqrMagnitude < 1e-10f) return false;
        Vector3 nrm = SafeNormalize3(Vector3.Cross(normal, dir));
        if (nrm.sqrMagnitude < 1e-10f) return false;
        float dist = Mathf.Max(0f, curve.Segments.Count > m ? curve.Segments[m].FitSizeScale : 0.5f);
        Vector3 center = (a + b) * 0.5f;
        int startIdx = m;
        int endIdx = (isLoop && m + 1 == n - 1) ? 0 : m + 1; // loop wrap end → joint at 0 / 闭环环绕末端→连接点0

        // Snapshot the along-segment extent from both sides' intersections with the start/end reference lines.
        // 用两侧平行线与起/终参考线的交点快照沿段方向的跨度。
        float minA = float.MaxValue, maxA = float.MinValue;
        for (int si = 0; si < 2; si++)
        {
            int s = si == 0 ? 1 : -1;
            if (!TryGetParallel(curve, pts, m, s, normal, out Vector3 po, out Vector3 pd)) return false;
            if (hasRef[startIdx] && IntersectLines(po, dir, refOrigin[startIdx], refDir[startIdx], normal, out Vector3 ipS))
                minA = Mathf.Min(minA, Vector3.Dot(ipS - center, dir));
            if (hasRef[endIdx] && IntersectLines(po, dir, refOrigin[endIdx], refDir[endIdx], normal, out Vector3 ipE))
                maxA = Mathf.Max(maxA, Vector3.Dot(ipE - center, dir));
        }
        // Degenerate fallback: bound to the segment itself. / 退化兜底：取段自身跨度。
        float segHalf = Vector3.Distance(a, b) * 0.5f;
        if (minA > maxA) { minA = -segHalf; maxA = segHalf; }

        // Rectangle corners: [start(+d), end(+d), end(-d), start(-d)] in the segment frame.
        // 矩形角点：[起点(+d), 终点(+d), 终点(-d), 起点(-d)]（小线段坐标系）。
        corners[0] = center + dir * minA + nrm * dist;
        corners[1] = center + dir * maxA + nrm * dist;
        corners[2] = center + dir * maxA - nrm * dist;
        corners[3] = center + dir * minA - nrm * dist;
        return true;
    }

    /// <summary>Gets the parallel reference line (origin + in-plane direction) for micro-segment m on side s (±1),
    /// offset by FitSizeScale from the segment center. / 获取小线段 m 在 s（±1）侧的平行参考线（原点+平面内方向），
    /// 距段中心偏移 适应尺寸缩放。</summary>
    private static bool TryGetParallel(BezierCurve curve, List<Vector2> pts, int m, int s, Vector3 normal, out Vector3 origin, out Vector3 dir)
    {
        origin = Vector3.zero; dir = Vector3.zero;
        if (pts == null || m < 0 || m + 1 >= pts.Count) return false;
        Vector3 a = curve.MapToWorld(pts[m]);
        Vector3 b = curve.MapToWorld(pts[m + 1]);
        dir = SafeNormalize3(b - a);
        if (dir.sqrMagnitude < 1e-10f) return false;
        Vector3 nrm = SafeNormalize3(Vector3.Cross(normal, dir));
        if (nrm.sqrMagnitude < 1e-10f) return false;
        float dist = Mathf.Max(0f, curve.Segments.Count > m ? curve.Segments[m].FitSizeScale : 0.5f);
        origin = (a + b) * 0.5f + nrm * (dist * s);
        return true;
    }

    /// <summary>Intersects two coplanar infinite lines (in the curve's plane). Returns false when parallel.
    /// 求平面内两条无限直线的交点（在曲线平面内）；平行时返回 false。</summary>
    private static bool IntersectLines(Vector3 o1, Vector3 d1, Vector3 o2, Vector3 d2, Vector3 normal, out Vector3 point)
    {
        point = Vector3.zero;
        float denom = Vector3.Dot(Vector3.Cross(d1, d2), normal);
        if (Mathf.Abs(denom) < 1e-8f) return false;
        float t = Vector3.Dot(Vector3.Cross(o2 - o1, d2), normal) / denom;
        point = o1 + d1 * t;
        return true;
    }

    /// <summary>Draws preview wireframes (one block per micro-segment, differentiated by PrimitiveType).
    /// 绘制预览线框（每个小线段对应的物块，按 PrimitiveType 差异化）</summary>
    private static void DrawPreviewWireframes(BezierCurve curve)
    {
        var pts = curve.SamplePoints();
        if (pts.Count < 2) return;

        bool is3d = curve.Is3D;
        List<Vector3> pts3d = null;
        if (is3d)
        {
            pts3d = curve.SamplePoints3D();
            if (pts3d.Count < 2) return;
        }

        for (int i = 0; i < pts.Count - 1 && i < curve.Segments.Count; i++)
        {
            if (!CurvePlacementHelper.ComputePlacement(curve, i, pts, pts3d, out Vector3 genPos, out Quaternion rot, out Vector3 scale))
                continue;
            var seg = curve.Segments[i];

            Handles.color = PreviewWireColor;

            // Draw the wireframe by PrimitiveType with dimensions matching the prefab's native size
        // 按 PrimitiveType 绘制线框，尺寸匹配对应 Prefab 原生尺寸
            DrawWireByType(seg.PrimitiveType, genPos, rot, scale);
        }
    }

    /// <summary>Draws the hand-drawn wireframe for a primitive type. / 按类型绘制手绘线框</summary>
    private static void DrawWireByType(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        switch (type)
        {
            case PrimitiveType.Sphere:
                DrawWireSphereNative(pos, rot, scale);
                break;
            case PrimitiveType.Capsule:
                DrawWireCapsuleNative(pos, rot, scale);
                break;
            case PrimitiveType.Cylinder:
                DrawWireCylinderNative(pos, rot, scale);
                break;
            case PrimitiveType.Plane:
                DrawWirePlaneNative(pos, rot, scale);
                break;
            case PrimitiveType.Quad:
                DrawWireQuadNative(pos, rot, scale);
                break;
            default: // Cube
                DrawWireCubeNative(pos, rot, scale);
                break;
        }
    }

    // ===== Triangle preview (real mesh edges) / 三角面预览（真实网格边） =====

    private struct MeshEdge { public Vector3 A, B; }

    /// <summary>Prefab cache (avoid per-frame Resources.Load). / Prefab 缓存（避免每帧 Resources.Load）</summary>
    private static readonly Dictionary<PrimitiveType, GameObject> PrefabCache = new Dictionary<PrimitiveType, GameObject>();

    /// <summary>Deduplicated local-space edge cache per mesh instance. / 网格去重边缓存（本地空间，按网格实例）</summary>
    private static readonly Dictionary<int, MeshEdge[]> EdgeCache = new Dictionary<int, MeshEdge[]>();

    /// <summary>Builtin mesh fallback names (used when the prefab mesh is unreadable).
    /// 内置网格兜底名（Prefab 网格不可读时使用）</summary>
    private static string BuiltinMeshName(PrimitiveType t) => t switch
    {
        PrimitiveType.Sphere => "New-Sphere.fbx",
        PrimitiveType.Capsule => "New-Capsule.fbx",
        PrimitiveType.Cylinder => "New-Cylinder.fbx",
        PrimitiveType.Plane => "New-Plane.fbx",
        PrimitiveType.Quad => "New-Quad.fbx",
        _ => "New-Cube.fbx",
    };

    /// <summary>Gets the mesh for a primitive: the project prefab's mesh first, the builtin mesh as fallback.
    /// 获取物体网格：优先项目 Prefab 网格，兜底内置网格（均要求可读）</summary>
    private static bool TryGetPreviewMesh(PrimitiveType type, out Mesh mesh, out Matrix4x4 localToWorld)
    {
        mesh = null;
        localToWorld = Matrix4x4.identity;
        if (!PrefabCache.TryGetValue(type, out var prefab))
        {
            prefab = Resources.Load<GameObject>($"Blocks/Primitives/{type}");
            PrefabCache[type] = prefab;
        }
        if (prefab != null)
        {
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
            {
                mesh = mf.sharedMesh;
                // Prefab-internal hierarchy transform (mesh may sit on a child object).
                // Prefab 内部层级变换（网格可能挂在子物体上）
                localToWorld = mf.transform.localToWorldMatrix;
                return true;
            }
        }
        mesh = Resources.GetBuiltinResource<Mesh>(BuiltinMeshName(type));
        if (mesh != null && mesh.isReadable)
        {
            localToWorld = Matrix4x4.identity;
            return true;
        }
        return false;
    }

    /// <summary>Extracts deduplicated edges from a mesh (cached per mesh instance).
    /// 提取网格的去重边集合（按网格实例缓存）</summary>
    private static MeshEdge[] GetMeshEdges(Mesh mesh)
    {
        if (EdgeCache.TryGetValue(mesh.GetInstanceID(), out var cached)) return cached;

        var verts = mesh.vertices;
        var tris = mesh.triangles;
        var seen = new HashSet<long>();
        var list = new List<MeshEdge>();
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            AddEdge(tris[i], tris[i + 1]);
            AddEdge(tris[i + 1], tris[i + 2]);
            AddEdge(tris[i + 2], tris[i]);
        }
        EdgeCache[mesh.GetInstanceID()] = list.ToArray();
        return EdgeCache[mesh.GetInstanceID()];

        void AddEdge(int a, int b)
        {
            if (a == b) return;
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (seen.Add(key)) list.Add(new MeshEdge { A = verts[a], B = verts[b] });
        }
    }

    /// <summary>Draws the triangle preview: the real mesh edge set of each segment's primitive.
    /// 绘制三角面预览：每段物体的真实网格边集合</summary>
    private static void DrawPreviewTriangleMeshes(BezierCurve curve)
    {
        var pts = curve.SamplePoints();
        if (pts.Count < 2) return;

        bool is3d = curve.Is3D;
        List<Vector3> pts3d = null;
        if (is3d)
        {
            pts3d = curve.SamplePoints3D();
            if (pts3d.Count < 2) return;
        }

        for (int i = 0; i < pts.Count - 1 && i < curve.Segments.Count; i++)
        {
            if (!CurvePlacementHelper.ComputePlacement(curve, i, pts, pts3d, out Vector3 genPos, out Quaternion rot, out Vector3 scale))
                continue;
            var seg = curve.Segments[i];
            if (!TryGetPreviewMesh(seg.PrimitiveType, out var mesh, out var local))
            {
                // Unreadable mesh: fall back to the hand-drawn wireframe / 网格不可读：退化为手绘线框
                Handles.color = PreviewWireColor;
                DrawWireByType(seg.PrimitiveType, genPos, rot, scale);
                continue;
            }
            var edges = GetMeshEdges(mesh);
            Handles.color = PreviewWireColor;
            // One matrix per segment; lines stay in mesh-local space / 每段一个矩阵；线条保持在网格本地空间
            Handles.matrix = Matrix4x4.TRS(genPos, rot, scale) * local;
            for (int e = 0; e < edges.Length; e++)
                Handles.DrawLine(edges[e].A, edges[e].B);
            Handles.matrix = Matrix4x4.identity;
        }
    }

    // ===== Native primitive size reference / 各 Primitive 原生尺寸参考 =====
    // Cube: 1×1×1, Sphere: 1×1×1 / Cube: 1×1×1    Sphere: 1×1×1
    // Capsule: 1×2×1, Cylinder: 1×2×1 / Capsule: 1×2×1    Cylinder: 1×2×1
    // Plane: 10×1×10, Quad: 1×1 / Plane:  10×1×10   Quad:     1×1

    /// <summary>Cube preview: 1×1×1 wire cube. / Cube 预览：1×1×1 WireCube</summary>
    private static void DrawWireCubeNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, scale);
        Handles.DrawWireCube(Vector3.zero, Vector3.one);
        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Sphere preview: three unit rings deformed uniformly by the TRS matrix.
    /// Sphere 预览：三向单位圆环置于 TRS 矩阵下统一变形</summary>
    private static void DrawWireSphereNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, scale);
        Handles.DrawWireDisc(Vector3.zero, Vector3.right,   0.5f);
        Handles.DrawWireDisc(Vector3.zero, Vector3.up,      0.5f);
        Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 0.5f);
        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Capsule preview: cylinder (diameter 1, height 1) + hemispheres (radius 0.5) at y = ±0.5.
    /// Drawn at native size (total height 2), so the matrix scale equals the segment scale directly.
    /// Capsule 预览：圆柱（直径 1、柱高 1）+ 球心 ±0.5Y、半径 0.5 的上下半球。
    /// 内容已按原生尺寸（总高 2）绘制，矩阵缩放直接取段缩放（不再额外 ×2）</summary>
    private static void DrawWireCapsuleNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, scale);

        // Cylinder part: top/bottom discs (y = ±0.5) + 4 vertical lines / 圆柱部分：上下圆盘（y=±0.5）+ 4 条竖线
        Handles.DrawWireDisc(Vector3.up * 0.5f, Vector3.up, 0.5f);
        Handles.DrawWireDisc(Vector3.down * 0.5f, Vector3.up, 0.5f);
        Vector3 r = Vector3.right * 0.5f, f = Vector3.forward * 0.5f;
        Handles.DrawLine(Vector3.up * 0.5f + r, Vector3.down * 0.5f + r);
        Handles.DrawLine(Vector3.up * 0.5f - r, Vector3.down * 0.5f - r);
        Handles.DrawLine(Vector3.up * 0.5f + f, Vector3.down * 0.5f + f);
        Handles.DrawLine(Vector3.up * 0.5f - f, Vector3.down * 0.5f - f);

        // Upper hemisphere (center +0.5Y) / 上半球（球心 +0.5Y）
        DrawWireHemisphere(Vector3.up * 0.5f, true);
        // Lower hemisphere (center -0.5Y) / 下半球（球心 -0.5Y）
        DrawWireHemisphere(Vector3.down * 0.5f, false);

        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Draws a hemisphere wireframe: two meridian semicircles crossing at the pole (X and Z), no latitude line.
    /// The cylinder's top/bottom discs already serve as the connecting latitude, so the hemisphere needs none.
    /// 绘制半球线框：两条十字经线半圆（X、Z 方向，极点交叉），无纬线。
    /// 圆柱的上下圆盘已充当连接半球的纬线，半球无需再画</summary>
    private static void DrawWireHemisphere(Vector3 center, bool facingUp)
    {
        Vector3 axis = facingUp ? Vector3.up : Vector3.down;
        DrawSemicircle(center, Vector3.right, axis);
        DrawSemicircle(center, Vector3.forward, axis);
    }

    /// <summary>Draws one open meridian semicircle: from equator +h, over the pole (axis direction), to equator -h.
    /// The start point is the +h equator, so the arc ends at -h and never closes back on itself.
    /// 画一条开口经线半圆：从赤道 +h 经极点（axis 方向）到赤道 -h。
    /// 起点为 +h 赤道，终点落在 -h 赤道，弧不会闭合回起点</summary>
    private static void DrawSemicircle(Vector3 center, Vector3 h, Vector3 axis)
    {
        Vector3 prev = center + h * 0.5f;
        for (int i = 1; i <= 12; i++)
        {
            float ph = Mathf.PI * i / 12f;
            Vector3 p = center + (h * Mathf.Cos(ph) + axis * Mathf.Sin(ph)) * 0.5f;
            Handles.DrawLine(prev, p);
            prev = p;
        }
    }

    /// <summary>Cylinder preview: native 1×2×1, deformed by the TRS matrix.
    /// Cylinder 预览：原生 1×2×1，TRS 矩阵统一变形</summary>
    private static void DrawWireCylinderNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, new Vector3(scale.x, scale.y * 2f, scale.z));
        // Top and bottom discs (radius 0.5) / 上下圆盘（半径 0.5）
        Handles.DrawWireDisc(Vector3.up * 0.5f,    Vector3.up, 0.5f);
        Handles.DrawWireDisc(Vector3.down * 0.5f,  Vector3.up, 0.5f);
        // Four vertical lines / 四条竖线
        Vector3 r = Vector3.right * 0.5f, f = Vector3.forward * 0.5f;
        Handles.DrawLine(Vector3.up * 0.5f + r, Vector3.down * 0.5f + r);
        Handles.DrawLine(Vector3.up * 0.5f - r, Vector3.down * 0.5f - r);
        Handles.DrawLine(Vector3.up * 0.5f + f, Vector3.down * 0.5f + f);
        Handles.DrawLine(Vector3.up * 0.5f - f, Vector3.down * 0.5f - f);
        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Plane preview: native 10×10 frame + center cross + blue face-normal line (1 local unit).
    /// Drawn at native size (10×10), so the matrix scale equals the segment scale directly.
    /// Plane 预览：原生 10×10 方框 + 中心十字线 + 蓝色面朝向垂线（1 本地单位，法线 +Y）。
    /// 内容已按原生尺寸（10×10）绘制，矩阵缩放直接取段缩放（不再额外 ×10）</summary>
    private static void DrawWirePlaneNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, new Vector3(scale.x, 1f, scale.z));

        // Frame (half-width 5) / 方框（半宽 5）
        Vector3 r = Vector3.right * 5f, f = Vector3.forward * 5f;
        Handles.DrawLine(r + f, -r + f);
        Handles.DrawLine(-r + f, -r - f);
        Handles.DrawLine(-r - f, r - f);
        Handles.DrawLine(r - f, r + f);

        // Center cross: horizontal + vertical / 中心横线与竖线（十字）
        Handles.DrawLine(-r, r);
        Handles.DrawLine(-f, f);

        // Face-normal line (blue, 1 local unit; Plane normal = +Y) / 面朝向垂线（蓝色，1 本地单位；Plane 法线 = +Y）
        Handles.color = Color.blue;
        Handles.DrawLine(Vector3.zero, Vector3.up);
        Handles.color = PreviewWireColor;

        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Quad preview: native 1×1 frame + one diagonal + blue face-normal line (1 local unit).
    /// Quad 预览：原生 1×1 方框 + 一条对角线 + 蓝色面朝向垂线（1 本地单位，正面朝 -Z）</summary>
    private static void DrawWireQuadNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, scale);

        // Frame (half-size 0.5) / 方框（半宽 0.5）
        Vector3 r = Vector3.right * 0.5f, u = Vector3.up * 0.5f;
        Handles.DrawLine(r + u, -r + u);
        Handles.DrawLine(-r + u, -r - u);
        Handles.DrawLine(-r - u, r - u);
        Handles.DrawLine(r - u, r + u);

        // One diagonal / 一条对角线
        Handles.DrawLine(-r - u, r + u);

        // Face-normal line (blue, 1 local unit; Unity Quad faces -Z) / 面朝向垂线（蓝色，1 本地单位；Unity Quad 正面朝 -Z）
        Handles.color = Color.blue;
        Handles.DrawLine(Vector3.zero, -Vector3.forward);
        Handles.color = PreviewWireColor;

        Handles.matrix = Matrix4x4.identity;
    }

    // ===== Cursor rendering / 游标渲染 =====

    /// <summary>Draws the cursor: blue/orange wireframe sphere + axis-colored arrows (camera-adaptive size).
    /// 绘制游标：蓝色/橙色线框球体 + 轴色箭头（相机自适应大小）</summary>
    private static void DrawCursor(CurveManager m)
    {
        float size = CursorSize * HandleUtility.GetHandleSize(m.CursorPosition);
        bool locked = m.CursorLocked;
        Color cursorCol = locked ? new Color(1f, 0.6f, 0.2f, 0.7f) : new Color(0.2f, 0.5f, 1f, 0.7f);

        Handles.color = cursorCol;
        // Wireframe sphere (three rings) / 球体线框（三向圆环）
        Handles.DrawWireDisc(m.CursorPosition, Vector3.right,   size);
        Handles.DrawWireDisc(m.CursorPosition, Vector3.up,      size);
        Handles.DrawWireDisc(m.CursorPosition, Vector3.forward, size);

        // X axis arrow (red) / X 轴箭头（红）
        Handles.color = Color.red;
        Handles.DrawLine(m.CursorPosition, m.CursorPosition + Vector3.right * size * 2f);
        // Y axis arrow (green) / Y 轴箭头（绿）
        Handles.color = Color.green;
        Handles.DrawLine(m.CursorPosition, m.CursorPosition + Vector3.up * size * 2f);
        // Z axis arrow (blue) / Z 轴箭头（蓝）
        Handles.color = Color.blue;
        Handles.DrawLine(m.CursorPosition, m.CursorPosition + Vector3.forward * size * 2f);
    }
}

