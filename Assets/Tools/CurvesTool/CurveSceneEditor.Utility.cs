using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SceneView curve interaction editor — helpers (coordinate conversion, snapping, math).
/// SceneView 曲线交互编辑器 — 辅助方法（坐标转换、吸附、数学工具）。
/// </summary>
public static partial class CurveSceneEditor
{
    /// <summary>Mouse → world position projected onto the curve's editing plane (falls back to the selected curve's plane).
    /// 鼠标 → 世界坐标，投影到指定曲线的编辑平面（未指定时用选中曲线的平面）</summary>
    private static Vector2 GetMouseWorldPos(Event e, CurveTool w, CurvePlane? planeOverride = null)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        // Prefer the given curve / selected curve's plane / 优先使用指定曲线/选中曲线的平面
        var m = CurveManager.Instance;
        CurvePlane plane = planeOverride ?? m?.SelectedCurve?.Plane ?? CurvePlane.XZ;

        Vector3 normal = plane switch
        {
            CurvePlane.XZ => Vector3.up,
            CurvePlane.XY => Vector3.forward,
            CurvePlane.YZ => Vector3.right,
            _ => Vector3.up,
        };

        Plane p = new Plane(normal, Vector3.zero);
        if (p.Raycast(ray, out float dist))
        {
            Vector3 hit = ray.GetPoint(dist);
            return plane switch
            {
                CurvePlane.XZ => new Vector2(hit.x, hit.z),
                CurvePlane.XY => new Vector2(hit.x, hit.y),
                CurvePlane.YZ => new Vector2(hit.y, hit.z),
                _ => new Vector2(hit.x, hit.z),
            };
        }

        // Fallback when the ray is parallel to the editing plane: project onto a camera-facing plane through the cursor/origin
        // 射线与编辑平面平行时退路：投到过游标/原点且垂直于相机的平面上
        Vector3 fallbackCenter = m != null ? m.CursorPosition : Vector3.zero;
        Plane fallback = new Plane(-Camera.current.transform.forward, fallbackCenter);
        if (fallback.Raycast(ray, out float dist2))
        {
            Vector3 hit = ray.GetPoint(dist2);
            return plane switch
            {
                CurvePlane.XZ => new Vector2(hit.x, hit.z),
                CurvePlane.XY => new Vector2(hit.x, hit.y),
                CurvePlane.YZ => new Vector2(hit.y, hit.z),
                _ => new Vector2(hit.x, hit.z),
            };
        }
        return Vector2.zero;
    }

    /// <summary>3D mouse position: projected onto a horizontal plane at the given height.
    /// 3D 鼠标位置：投影到指定高度的水平面</summary>
    private static Vector3 GetMouseWorldPos3D(Event e, float height)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane hPlane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
        if (hPlane.Raycast(ray, out float dist))
            return ray.GetPoint(dist);
        return new Vector3(0f, height, 0f);
    }

    /// <summary>Snaps a Vector3 to the nearest grid point by the given grid size (per-component, reusing SnapFloat's zero-guard).
    /// 将 Vector3 按指定网格大小吸附到最近的网格点（逐分量复用 SnapFloat 的除零保护）</summary>
    private static Vector3 SnapVector3(Vector3 value, Vector3 gridSize)
    {
        return new Vector3(
            SnapFloat(value.x, gridSize.x),
            SnapFloat(value.y, gridSize.y),
            SnapFloat(value.z, gridSize.z)
        );
    }

    /// <summary>Snaps a float to the nearest grid point (returns the raw value when the grid is too small, preventing division by zero).
    /// 将浮点数按指定网格大小吸附到最近的网格点（网格尺寸过小时直接返回原值，防止除零）</summary>
    private static float SnapFloat(float value, float gridSize)
    {
        if (gridSize < 0.0001f) return value;
        return Mathf.Round(value / gridSize) * gridSize;
    }

    /// <summary>Current snap step: EditorSnapSettings.move in editor mode (follows Edit > Snap Settings), tool-local values otherwise.
    /// 当前吸附步长：编辑器模式使用 EditorSnapSettings.move（跟随 Edit > Snap Settings），工具模式使用工具自身设置</summary>
    private static Vector3 GetSnapStep(CurveTool w, bool ctrlSnap)
    {
        if (w != null && w.UseEditorSnapSettings)
            return EditorSnapSettings.move;
        return ctrlSnap ? w.SnapIncrementMove : w.SnapGridSize;
    }

    private static Vector2 GetElementValue(CurveManager m)
    {
        var v = m.SelectedVertex;
        if (v == null) return Vector2.zero;
        return v.SelectedSubElement switch
        {
            3 => v.Position,
            1 => v.LeftHandlePosition,
            2 => v.RightHandlePosition,
            _ => Vector2.zero,
        };
    }

    /// <summary>Saves the initial positions of all multi-selected vertices at drag start.
    /// 拖拽开始时保存所有多选顶点的初始位置</summary>
    private static void SaveDragStartValues(CurveManager m)
    {
        _dragStartValues = new Dictionary<int, Vector2>();
        var curve = m.SelectedCurve;
        if (curve == null) return;
        int sub = m.SelectedVertex?.SelectedSubElement ?? 3;
        foreach (int vi in m.SelectedVertexIndices)
        {
            var v = curve.Vertices[vi];
            _dragStartValues[vi] = sub switch
            {
                3 => v.Position,
                1 => v.LeftHandlePosition,
                2 => v.RightHandlePosition,
                _ => v.Position,
            };
        }
    }

    /// <summary>Snaps the drag-start values to avoid a first-frame jump. / 拖拽开始时吸附起始值，避免首帧跳位</summary>
    private static void SnapDragStartIfNeeded(Event e, CurveManager m)
    {
        bool snapActive = EditorSnapSettings.gridSnapEnabled || e.control || e.command;
        if (!snapActive) return;
        var curve = m.SelectedCurve;
        if (curve == null) return;

        Vector3 gs = GetSnapStep(CurveTool.Instance, e.control || e.command);

        // Snap the 2D/3D vertex start value / 2D/3D 顶点起始值吸附
        Vector3 ws = curve.MapToWorld(_dragStartValue);
        ws = SnapVector3(ws, gs);
        _dragStartValue = curve.MapFromWorld(ws);

        // Snap the mouse start position as well (2D) / 同步吸附鼠标起始位置（2D）
        Vector3 wm = curve.MapToWorld(_dragStartMouse);
        wm = SnapVector3(wm, gs);
        _dragStartMouse = curve.MapFromWorld(wm);

        // 3D curves: snap _dragStartMouse3D as well (XZ plane) / 3D 曲线：同步吸附 _dragStartMouse3D（XZ 平面）
        if (m.SelectedCurve != null && m.SelectedCurve.Is3D)
        {
            Vector3 m3 = _dragStartMouse3D;
            m3.x = SnapFloat(m3.x, gs.x);
            m3.z = SnapFloat(m3.z, gs.z);
            _dragStartMouse3D = m3;

            // Snap the start arrow height (for cases 4/5/6) / 吸附起始箭头高度（case 4/5/6 用）
            if (m.SelectedVertex != null)
            {
                int sub = m.SelectedVertex.SelectedSubElement;
                if (sub >= 4 && sub <= 6)
                {
                    float ah = SnapFloat(_dragStartArrowHeight, gs.y);
                    _dragStartArrowHeight = ah;
                }
            }
        }
    }

    /// <summary>Closest distance between a ray and a 3D point. / 射线到 3D 点的最近距离</summary>
    private static float RayPointDist(Ray ray, Vector3 point)
    {
        Vector3 toPoint = point - ray.origin;
        float t = Vector3.Dot(toPoint, ray.direction);
        if (t < 0f) return float.MaxValue;
        return Vector3.Distance(ray.GetPoint(t), point);
    }

    private static float PointToSegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float ls = ab.sqrMagnitude;
        if (ls < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ls);
        return Vector2.Distance(p, a + t * ab);
    }

    private static Vector2 CubicBez(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t, tt = t * t, uu = u * u;
        return uu * u * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + tt * t * p3;
    }
}
