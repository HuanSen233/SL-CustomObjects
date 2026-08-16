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
    /// Capsule 预览：圆柱（直径 1、柱高 1）+ 球心 ±0.5Y、半径 0.5 的上下半球</summary>
    private static void DrawWireCapsuleNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, new Vector3(scale.x, scale.y * 2f, scale.z));

        // Cylinder part: top/bottom discs (y = ±0.5) + 4 vertical lines / 圆柱部分：上下圆盘（y=±0.5）+ 4 条竖线
        Handles.DrawWireDisc(Vector3.up * 0.5f, Vector3.up, 0.5f);
        Handles.DrawWireDisc(Vector3.down * 0.5f, Vector3.up, 0.5f);
        Vector3 r = Vector3.right * 0.5f, f = Vector3.forward * 0.5f;
        Handles.DrawLine(Vector3.up * 0.5f + r, Vector3.down * 0.5f + r);
        Handles.DrawLine(Vector3.up * 0.5f - r, Vector3.down * 0.5f - r);
        Handles.DrawLine(Vector3.up * 0.5f + f, Vector3.down * 0.5f + f);
        Handles.DrawLine(Vector3.up * 0.5f - f, Vector3.down * 0.5f - f);

        // Upper hemisphere (center +0.5Y) / 上半球（球心 +0.5Y）
        DrawWireHemisphere(Vector3.up * 0.5f);
        // Lower hemisphere (center -0.5Y) / 下半球（球心 -0.5Y）
        DrawWireHemisphere(Vector3.down * 0.5f);

        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Draws a hemisphere wireframe (three meridian semicircles, radius 0.5).
    /// 绘制半球线框（三条经线半圆，半径 0.5）</summary>
    private static void DrawWireHemisphere(Vector3 center)
    {
        Handles.DrawWireArc(center, Vector3.right, Vector3.forward, 180f, 0.5f);
        Handles.DrawWireArc(center, Vector3.forward, Vector3.right, 180f, 0.5f);
        Handles.DrawWireArc(center, (Vector3.right + Vector3.forward).normalized, Vector3.forward, 180f, 0.5f);
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

    /// <summary>Plane preview: native 10×1×10 frame + center cross + blue face-normal line (1 local unit).
    /// Plane 预览：原生 10×1×10 方框 + 中心十字线 + 蓝色面朝向垂线（1 本地单位，法线 +Y）</summary>
    private static void DrawWirePlaneNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, new Vector3(scale.x * 10f, 1f, scale.z * 10f));

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

