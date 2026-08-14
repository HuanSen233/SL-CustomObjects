using System;
using UnityEngine;

/// <summary>
/// 单个小线段（分辨率段）的独立数据 — 控制物体在该段上的生成参数。
/// </summary>
[Serializable]
public class SegmentInfo
{
    /// <summary>基础缩放（基础尺寸），默认 1，生成时可被自动段长覆盖</summary>
    public Vector3 BaseScale = Vector3.one;

    /// <summary>生成物体类型（Cube/Capsule/Cylinder/Sphere/Plane/Quad）</summary>
    public PrimitiveType PrimitiveType = PrimitiveType.Cube;

    /// <summary>是否将段长覆盖到指定缩放轴</summary>
    public bool FitSegmentLength = true;

    /// <summary>适应段长时覆盖哪个缩放轴（0=X, 1=Y, 2=Z）</summary>
    public int FitAxis = 2;

    /// <summary>相对缩放乘数（在绝对缩放基础上），默认 1</summary>
    public Vector3 RelativeScale = Vector3.one;
    /// <summary>中心点偏移（相对段长），0=段中心，0.5=段长半距</summary>
    public float PositionOffset = 0f;
    /// <summary>XYZ 加算位置偏移</summary>
    public Vector3 PositionOffset3D = Vector3.zero;

    /// <summary>相对旋转偏移（欧拉角）</summary>
    public Vector3 RotationOffset = Vector3.zero;

    /// <summary>在 SceneView 中此段是否被选中</summary>
    [NonSerialized]
    public bool IsSelected;

    public static SegmentInfo Default => new SegmentInfo();
}
