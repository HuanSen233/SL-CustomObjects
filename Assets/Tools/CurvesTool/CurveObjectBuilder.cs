using UnityEditor;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Curve object generator — spawns a PrimitiveComponent along every micro-segment of a Bezier curve.
/// Position = segment center (offsettable); scale = BaseScale → FitSegmentLength × segment length → × RelativeScale.
/// Each segment picks its own primitive type via PrimitiveType.
/// 曲线物体生成器 — 沿贝塞尔曲线每个小线段生成 PrimitiveComponent。
/// 生成位置 = 段中心（可偏移），缩放 = BaseScale → FitSegmentLength 乘段长 → × RelativeScale。
/// 每段按 PrimitiveType 独立选择生成物体类型。
/// </summary>
public static class CurveObjectBuilder
{

    /// <summary>Builds objects from a curve. / 从曲线生成物体</summary>
    public static void Build(BezierCurve curve)
    {
        if (curve == null || curve.Vertices.Count < 2)
        {
            EditorUtility.DisplayDialog(L10n.T("error"), L10n.T("err_no_curve"), L10n.T("ok"));
            return;
        }

        var pts = curve.SamplePoints();
        if (pts.Count < 2)
        {
            EditorUtility.DisplayDialog(L10n.T("error"), L10n.T("err_no_samples"), L10n.T("ok"));
            return;
        }

        // Keep segment data in sync before iterating.
        // 确保段数据同步
        curve.RebuildSegments();

        GameObject parent = new GameObject(L10n.T("gen_parent_prefix") + curve.Name);
        Undo.RegisterCreatedObjectUndo(parent, "生成曲线物体");

        int totalSegs = Mathf.Min(pts.Count - 1, curve.Segments.Count);
        bool is3d = curve.Is3D;
        List<Vector3> pts3d = null;
        if (is3d) pts3d = curve.SamplePoints3D();
        // Precompute the advanced-fit boundary lines once (unused for non-advanced / 3D). / 预计算进阶适应边界线一次
        // （非进阶/3D 时不用）。
        CurveFitRefs refs = is3d ? CurveFitRefs.Empty : CurveFitGeometry.ComputeBoundaryLines(curve, pts);

        for (int i = 0; i < totalSegs; i++)
        {
            if (!CurvePlacementHelper.ComputePlacement(curve, i, pts, pts3d, refs, out Vector3 pos, out Quaternion rot, out Vector3 scale))
                continue;
            var seg = curve.Segments[i];
            Spawn(pos, rot, scale, CurveTool.Instance?.GenerationColor ?? Color.white, parent, i, seg.PrimitiveType);
        }

        Selection.activeGameObject = parent;
        SceneView.FrameLastActiveSceneView();
        Debug.Log($"<color=#00FF00>{L10n.T("gen_success", totalSegs, curve.Name)}</color>");
    }

    /// <summary>Spawns a single primitive block and parents it under the generated-object container. / 生成单个物块并挂到生成容器下</summary>
    private static void Spawn(Vector3 pos, Quaternion rot, Vector3 scale, Color color, GameObject parent, int idx, PrimitiveType primitiveType)
    {
        string path = $"Assets/Resources/Blocks/Primitives/{primitiveType}.prefab";
        var comp = PrimitiveComponent.Create<PrimitiveComponent>(path);
        if (comp == null) { Debug.LogError($"[CurveTool] Prefab not found: {path}"); return; }

        comp.gameObject.name = $"seg_{idx}";
        comp.transform.position = pos;
        comp.transform.rotation = rot;
        comp.transform.localScale = scale;
        comp.transform.SetParent(parent.transform);
        comp.Color = color;
        comp.Collidable = true;
        comp.Visible = true;
        Undo.RegisterCreatedObjectUndo(comp.gameObject, "生成曲线物体");
    }
}