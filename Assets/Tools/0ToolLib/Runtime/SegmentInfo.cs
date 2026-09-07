using System;
using UnityEngine;

/// <summary>
/// Per-micro-segment (resolution segment) data — controls generation parameters for that segment.
/// 单个小线段（分辨率段）的独立数据 — 控制物体在该段上的生成参数。
/// </summary>
[Serializable]
public class SegmentInfo
{
    /// <summary>Base scale (base size), default 1; may be overridden by automatic segment-length fitting at generation.
    /// 基础缩放（基础尺寸），默认 1，生成时可被自动段长覆盖</summary>
    public Vector3 BaseScale = Vector3.one;

    /// <summary>Primitive type spawned on this segment (Cube/Capsule/Cylinder/Sphere/Plane/Quad).
    /// 生成物体类型（Cube/Capsule/Cylinder/Sphere/Plane/Quad）</summary>
    public PrimitiveType PrimitiveType = PrimitiveType.Cube;

    /// <summary>Whether to overwrite the segment length onto the chosen scale axis.
    /// 是否将段长覆盖到指定缩放轴</summary>
    public bool FitSegmentLength = true;

    /// <summary>Scale axis overwritten when fitting segment length (0=X, 1=Y, 2=Z).
    /// 适应段长时覆盖哪个缩放轴（0=X, 1=Y, 2=Z）</summary>
    public int FitAxis = 2;

    /// <summary>Segment-length fitting mode (0=Simple, 1=Advanced). Simple keeps the current behavior
    /// (assign segment length onto the chosen scale axis); Advanced is the gap-filling variant (algorithm TBD).
    /// 适应段长模式（0=简单，1=进阶）。简单=现有逻辑（把段长赋给指定缩放轴）；进阶=填缺口的新算法（算法待实现）</summary>
    public int FitMode = 0;

    /// <summary>Advanced-fit size scale: distance from the segment to each parallel reference line (world units),
    /// default 0.5. Only relevant when FitMode = Advanced (the control is grayed otherwise).
    /// 进阶适应尺寸缩放：小线段到每条平行参考线的距离（世界单位），默认 0.5；仅 FitMode=进阶时生效（否则灰显）</summary>
    public float FitSizeScale = 0.5f;

    /// <summary>Relative scale multiplier (on top of absolute scale), default 1.
    /// 相对缩放乘数（在绝对缩放基础上），默认 1</summary>
    public Vector3 RelativeScale = Vector3.one;

    /// <summary>Center offset (relative to segment length); 0=segment center, 0.5=half segment length.
    /// 中心点偏移（相对段长），0=段中心，0.5=段长半距</summary>
    public float PositionOffset = 0f;

    /// <summary>Additive XYZ position offset (in segment-local space).
    /// XYZ 加算位置偏移</summary>
    public Vector3 PositionOffset3D = Vector3.zero;

    /// <summary>Relative rotation offset (Euler angles).
    /// 相对旋转偏移（欧拉角）</summary>
    public Vector3 RotationOffset = Vector3.zero;

    /// <summary>Whether this segment is selected in the Scene view.
    /// 在 SceneView 中此段是否被选中</summary>
    [NonSerialized]
    public bool IsSelected;

    /// <summary>Default segment preset.
    /// 默认段预设</summary>
    public static SegmentInfo Default => new SegmentInfo();
}