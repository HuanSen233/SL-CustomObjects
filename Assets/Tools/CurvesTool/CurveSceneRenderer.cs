using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SceneView 曲线渲染器 — 绘制虚拟曲线及预览线框。
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
    private static Color PreviewWireColor => CurveTool.Instance?.GenerationColor ?? new Color(1f, 1f, 1f, 0.25f);
    private static float CursorSize => CurveTool.Instance?.CursorDisplaySize ?? 0.15f;

    /// <summary>根据控制柄类型返回线颜色（高对比度色标）</summary>
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
        if (!w.IsEditMode && !w.PreviewMode) return;
        var m = CurveManager.Instance;
        if (m == null) return;

        if (w.IsEditMode)
        {
            // 编辑模式：完整渲染
            if (m.Curves.Count > 0)
            {
                foreach (var curve in m.Curves)
                {
                    if (!curve.IsVisible) continue;
                    DrawCurveLine(curve);
                    DrawVerticesAndHandles(curve);
                    if (w.PreviewMode) DrawPreviewWireframes(curve);
                }
            }
            DrawCursor(m);
        }
        else if (w.PreviewMode)
        {
            // 仅预览模式：只渲染预览线框
            foreach (var curve in m.Curves)
            {
                if (!curve.IsVisible) continue;
                DrawPreviewWireframes(curve);
            }
        }
    }

    /// <summary>绘制曲线折线，按段选中/锁定状态着色</summary>
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

    /// <summary>绘制顶点和曲柄</summary>
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

                // 曲柄线 — 按 HandleType 着色（选中时线不变色，仅端点变色）
                Handles.color = GetHandleLineColor(v.HandleTypeA);
                Handles.DrawLine(pos, lh, 2f);
                Handles.color = GetHandleLineColor(v.HandleTypeB);
                Handles.DrawLine(pos, rh, 2f);

                // 曲柄端点（球形）— 始终用默认端点色，仅选中时变黄
                Handles.color = v.SelectedSubElement == 1 ? SelectedColor : HandleEndColor;
                Handles.SphereHandleCap(0, lh, Quaternion.identity, HandleEndSize, EventType.Repaint);
                Handles.color = v.SelectedSubElement == 2 ? SelectedColor : HandleEndColor;
                Handles.SphereHandleCap(0, rh, Quaternion.identity, HandleEndSize, EventType.Repaint);
            }

            // 顶点（锁定时橙色）
            Color vtxCol = curve.IsLocked ? new Color(1f, 0.6f, 0.2f) : VertexColor;
            Handles.color = v.IsSelected && v.SelectedSubElement == 3 ? SelectedColor : vtxCol;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, VertexSize, EventType.Repaint);
        }
    }

    /// <summary>绘制预览线框（每个小线段对应的物块，按 PrimitiveType 差异化）</summary>
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

            // 按 PrimitiveType 绘制线框，尺寸匹配对应 Prefab 原生尺寸
            switch (seg.PrimitiveType)
            {
                case PrimitiveType.Sphere:
                    DrawWireSphereNative(genPos, rot, scale);
                    break;
                case PrimitiveType.Capsule:
                    DrawWireCapsuleNative(genPos, rot, scale);
                    break;
                case PrimitiveType.Cylinder:
                    DrawWireCylinderNative(genPos, rot, scale);
                    break;
                case PrimitiveType.Plane:
                    DrawWirePlaneNative(genPos, rot, scale);
                    break;
                case PrimitiveType.Quad:
                    DrawWireQuadNative(genPos, rot, scale);
                    break;
                default: // Cube
                    DrawWireCubeNative(genPos, rot, scale);
                    break;
            }
        }
    }

    // ===== 各 Primitive 原生尺寸参考 =====
    // Cube:    1×1×1    Sphere:   1×1×1
    // Capsule: 1×2×1    Cylinder: 1×2×1
    // Plane:  10×1×10   Quad:     1×1

    /// <summary>Cube 预览：1×1×1 WireCube</summary>
    private static void DrawWireCubeNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, scale);
        Handles.DrawWireCube(Vector3.zero, Vector3.one);
        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Sphere 预览：三向单位圆环置于 TRS 矩阵下统一变形</summary>
    private static void DrawWireSphereNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, scale);
        Handles.DrawWireDisc(Vector3.zero, Vector3.right,   0.5f);
        Handles.DrawWireDisc(Vector3.zero, Vector3.up,      0.5f);
        Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 0.5f);
        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Capsule 预览：原生 1×2×1，高=scale.y*2</summary>
    private static void DrawWireCapsuleNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Vector3 capSize = new Vector3(scale.x, scale.y * 2f, scale.z);
        Handles.matrix = Matrix4x4.TRS(pos, rot, capSize);
        Handles.DrawWireCube(Vector3.zero, Vector3.one);
        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Cylinder 预览：原生 1×2×1，TRS 矩阵统一变形</summary>
    private static void DrawWireCylinderNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, new Vector3(scale.x, scale.y * 2f, scale.z));
        // 上下圆盘（半径 0.5）
        Handles.DrawWireDisc(Vector3.up * 0.5f,    Vector3.up, 0.5f);
        Handles.DrawWireDisc(Vector3.down * 0.5f,  Vector3.up, 0.5f);
        // 四条竖线
        Vector3 r = Vector3.right * 0.5f, f = Vector3.forward * 0.5f;
        Handles.DrawLine(Vector3.up * 0.5f + r, Vector3.down * 0.5f + r);
        Handles.DrawLine(Vector3.up * 0.5f - r, Vector3.down * 0.5f - r);
        Handles.DrawLine(Vector3.up * 0.5f + f, Vector3.down * 0.5f + f);
        Handles.DrawLine(Vector3.up * 0.5f - f, Vector3.down * 0.5f - f);
        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Plane 预览：原生 10×1×10，TRS 矩阵统一变形</summary>
    private static void DrawWirePlaneNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Handles.matrix = Matrix4x4.TRS(pos, rot, new Vector3(scale.x * 10f, 1f, scale.z * 10f));
        // 单位方块（半宽 0.5）的十字 + 对角线
        Vector3 r = Vector3.right * 0.5f, f = Vector3.forward * 0.5f;
        Handles.DrawLine(-r, r);
        Handles.DrawLine(-f, f);
        Handles.DrawLine(-r - f, r + f);
        Handles.DrawLine(r - f, -r + f);
        Handles.matrix = Matrix4x4.identity;
    }

    /// <summary>Quad 预览：原生 1×1，十字线跨距=scale</summary>
    private static void DrawWireQuadNative(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Vector3 right = rot * Vector3.right * scale.x * 0.5f;
        Vector3 up = rot * Vector3.up * scale.y * 0.5f;
        Handles.DrawLine(pos - right, pos + right);
        Handles.DrawLine(pos - up, pos + up);
    }

    // ===== 游标渲染 =====

    /// <summary>绘制游标：蓝色/橙色线框球体 + 轴色箭头（相机自适应大小）</summary>
    private static void DrawCursor(CurveManager m)
    {
        float size = CursorSize * HandleUtility.GetHandleSize(m.CursorPosition);
        bool locked = m.CursorLocked;
        Color cursorCol = locked ? new Color(1f, 0.6f, 0.2f, 0.7f) : new Color(0.2f, 0.5f, 1f, 0.7f);

        Handles.color = cursorCol;
        // 球体线框（三向圆环）
        Handles.DrawWireDisc(m.CursorPosition, Vector3.right,   size);
        Handles.DrawWireDisc(m.CursorPosition, Vector3.up,      size);
        Handles.DrawWireDisc(m.CursorPosition, Vector3.forward, size);

        // X 轴箭头（红）
        Handles.color = Color.red;
        Handles.DrawLine(m.CursorPosition, m.CursorPosition + Vector3.right * size * 2f);
        // Y 轴箭头（绿）
        Handles.color = Color.green;
        Handles.DrawLine(m.CursorPosition, m.CursorPosition + Vector3.up * size * 2f);
        // Z 轴箭头（蓝）
        Handles.color = Color.blue;
        Handles.DrawLine(m.CursorPosition, m.CursorPosition + Vector3.forward * size * 2f);
    }
}

