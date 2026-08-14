using System;
using UnityEngine;

/// <summary>曲线所在平面的向上轴。Y=上(XZ平面), Z=上(XY平面), X=上(YZ平面)。</summary>
public enum UpAxis { Y, Z, X }

/// <summary>控制柄类型（仿 Blender）</summary>
public enum HandleType
{
    Auto,           // 自动：曲柄两端对齐，根据相邻顶点自动调整角度和长度
    Aligned,        // 对齐：方向锁定，长度独立
    AlignedLength,  // 镜像：方向+长度完全镜像
    Vector,         // 矢量：曲柄指向相邻顶点，段间为直线
    Free            // 自由：无任何约束
}

/// 3D 曲线：Position=(worldX, worldZ) 是 XZ 平面投影，Height=worldY。
/// 2D 曲线：Position=(worldX, worldZ) 是 XZ 平面投影，Height=0。
/// 坐标系约定（与 Unity 一致）：X=右, Y=上（高度）, Z=前。
/// </summary>
[Serializable]
public class CurveVertex
{
    /// <summary>顶点在 XZ 平面的投影坐标（x=世界X, y=世界Z）。世界 Y 由 Height 提供。</summary>
    public Vector2 Position;
    /// <summary>顶点在世界空间中的高度（世界 Y）。2D 曲线恒为 0。</summary>
    public float Height;
    /// <summary>左控制柄在 XZ 平面的偏移量（x=世界X偏移, y=世界Z偏移）。高度偏移由 LeftHandleHeight 提供。</summary>
    public Vector2 LeftHandle;
    /// <summary>左控制柄在世界 Y（高度）方向的偏移分量。</summary>
    public float LeftHandleHeight;
    public Vector2 RightHandle;
    /// <summary>右控制柄在世界 Y（高度）方向的偏移分量。</summary>
    public float RightHandleHeight;

    /// <summary>左控制柄类型（Auto/Aligned/AlignedLength/Vector/Free），对应左柄移动时对右柄的约束</summary>
    public HandleType HandleTypeA = HandleType.Auto;
    /// <summary>右控制柄类型（Auto/Aligned/AlignedLength/Vector/Free），对应右柄移动时对左柄的约束</summary>
    public HandleType HandleTypeB = HandleType.Auto;

    /// <summary>顶点分轴锁（UI 和 SceneView 拖拽时保护对应轴不被修改）</summary>
    public bool LockX, LockY, LockZ;
    /// <summary>左控制柄分轴锁</summary>
    public bool LeftHandleLockX, LeftHandleLockY, LeftHandleLockZ;
    /// <summary>右控制柄分轴锁</summary>
    public bool RightHandleLockX, RightHandleLockY, RightHandleLockZ;

    [NonSerialized] public bool IsSelected;
    /// <summary>0=无, 1=左曲柄, 2=右曲柄, 3=顶点自身</summary>
    [NonSerialized] public int SelectedSubElement;

    public CurveVertex(Vector2 position)
    {
        Position = position;
        LeftHandle = new Vector2(-1f, 0f);
        RightHandle = new Vector2(1f, 0f);
    }

    /// <summary>顶点完整 3D 世界坐标。与 Unity 坐标系一致：(X, Y, Z) = (Position.x, Height, Position.y)。</summary>
    public Vector3 PositionV3 => new Vector3(Position.x, Height, Position.y);
    /// <summary>左控制柄在 3D 世界空间中的偏移向量：(X偏移, Y偏移, Z偏移) = (LeftHandle.x, LeftHandleHeight, LeftHandle.y)。</summary>
    public Vector3 LeftHandleOffsetV3 => new Vector3(LeftHandle.x, LeftHandleHeight, LeftHandle.y);
    /// <summary>右控制柄在 3D 世界空间中的偏移向量：(X偏移, Y偏移, Z偏移) = (RightHandle.x, RightHandleHeight, RightHandle.y)。</summary>
    public Vector3 RightHandleOffsetV3 => new Vector3(RightHandle.x, RightHandleHeight, RightHandle.y);

    public Vector2 LeftHandlePosition => Position + LeftHandle;
    public Vector2 RightHandlePosition => Position + RightHandle;

    /// <summary>
    /// 应用控制柄类型约束（移动一侧曲柄后调用）。
    /// movedHandle=1 左柄被移动 → 基于 HandleTypeA 约束右柄；
    /// movedHandle=2 右柄被移动 → 基于 HandleTypeB 约束左柄。
    /// </summary>
    public void ApplyHandleType(int movedHandle, bool is3d = false)
    {
        if (movedHandle == 1)
        {
            // 左柄被移动 → 根据左柄类型约束右柄
            switch (HandleTypeA)
            {
                case HandleType.Auto:
                    RightHandle = -LeftHandle;
                    if (is3d) RightHandleHeight = -LeftHandleHeight;
                    break;
                case HandleType.AlignedLength:
                case HandleType.Aligned:
                    // 仅当右柄也是对齐/镜像族时才同步
                    if (HandleTypeB == HandleType.AlignedLength || HandleTypeB == HandleType.Aligned || HandleTypeB == HandleType.Auto)
                    {
                        if (HandleTypeB == HandleType.AlignedLength)
                        {
                            RightHandle = -LeftHandle;
                            if (is3d) RightHandleHeight = -LeftHandleHeight;
                        }
                        else // Aligned
                        {
                            if (RightHandle.magnitude > 0.0001f)
                                LeftHandle = -RightHandle.normalized * LeftHandle.magnitude;
                            if (is3d) LeftHandleHeight = -RightHandleHeight;
                        }
                    }
                    break;
                case HandleType.Vector:
                case HandleType.Free:
                    break;
            }
        }
        else if (movedHandle == 2)
        {
            // 右柄被移动 → 根据右柄类型约束左柄
            switch (HandleTypeB)
            {
                case HandleType.Auto:
                case HandleType.AlignedLength:
                    LeftHandle = -RightHandle;
                    if (is3d) LeftHandleHeight = -RightHandleHeight;
                    break;
                case HandleType.Aligned:
                case HandleType.Vector:
                case HandleType.Free:
                    break;
            }
            // 右柄移动时，若左柄为对齐/镜像，同步左柄（左柄被动接受右柄的值）
            if (HandleTypeA == HandleType.AlignedLength)
            {
                LeftHandle = -RightHandle;
                if (is3d) LeftHandleHeight = -RightHandleHeight;
            }
            else if (HandleTypeA == HandleType.Aligned)
            {
                if (RightHandle.magnitude > 0.0001f)
                    LeftHandle = -RightHandle.normalized * LeftHandle.magnitude;
                if (is3d) LeftHandleHeight = -RightHandleHeight;
            }
        }
    }
}
