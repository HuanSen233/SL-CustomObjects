using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SceneView 曲线交互编辑器 — 选择、拖拽、增删顶点/线段。
/// 支持 Shift 多选、Alt+右键插入、Alt+左键选段、网格吸附、轴向感知。
///
/// 拆分文件（partial class）：
///   CurveSceneEditor.MouseEvents.cs  — HandleMouseDown/Drag/Up, HandleKeyDown
///   CurveSceneEditor.HitTest.cs       — 命中检测（顶点/曲线段/游标拾取）
///   CurveSceneEditor.Utility.cs       — 辅助方法（坐标转换、吸附、数学工具）
/// </summary>
public static partial class CurveSceneEditor
{
    private static float VertexHitRadius => (CurveTool.Instance?.VertexSize ?? 0.2f) + 0.1f;
    private static float HandleHitRadius => (CurveTool.Instance?.HandleEndSize ?? 0.12f) + 0.1f;
    private const float CurveHitRadius = 0.25f;
    private const float SegmentHitRadius = 0.3f;
    private static float CursorHitRadius => (CurveTool.Instance?.CursorDisplaySize ?? 0.15f) * 2f;

    private static bool _isDragging;
    private static bool _isDraggingCursor;
    /// <summary>当前拖拽是否已记录 Undo（只在第一次 MouseDrag 时记录一次，避免 Undo 历史爆炸）</summary>
    private static bool _undoRecorded;
    private static Vector3 _dragStartCursorPos;
    private static Vector2 _dragStartMouse;
    private static Vector2 _dragStartValue;
    /// <summary>3D 拖拽起始鼠标位置（3D 曲线用）</summary>
    private static Vector3 _dragStartMouse3D;
    /// <summary>3D 拖拽起始控制柄偏移（3D 曲线用）</summary>
    private static Vector3 _dragStartHandle3DOffset;
    /// <summary>Y 轴拖拽起始屏幕 Y 位置</summary>
    private static float _dragStartScreenY;
    /// <summary>Y 轴拖拽起始高度值（vertex/handle）</summary>
    private static float _dragStartArrowHeight;
    /// <summary>批量拖拽时各顶点的起始值（顶点索引→起始位置）</summary>
    private static Dictionary<int, Vector2> _dragStartValues;
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        SceneView.duringSceneGui += OnSceneGUI;
        _registered = true;
    }

    public static void Unregister()
    {
        if (!_registered) return;
        SceneView.duringSceneGui -= OnSceneGUI;
        _registered = false;
    }

    private static void OnSceneGUI(SceneView sv)
    {
        var w = CurveTool.Instance;
        if (w == null || !w.IsEditMode) { _isDragging = false; _isDraggingCursor = false; _undoRecorded = false; return; }
        var m = CurveManager.Instance;
        if (m == null) return;

        Event e = Event.current;
        int cid = GUIUtility.GetControlID(FocusType.Passive);

        // 编辑模式下始终消耗事件，阻止 Unity 默认场景选择/轨道操作
        HandleUtility.AddDefaultControl(cid);

        switch (e.type)
        {
            case EventType.MouseDown: HandleMouseDown(e, m, w); break;
            case EventType.MouseDrag: HandleMouseDrag(e, m, w); break;
            case EventType.MouseUp: HandleMouseUp(e); break;
            case EventType.KeyDown: HandleKeyDown(e, m); break;
            case EventType.ScrollWheel: break;
        }
    }
}
