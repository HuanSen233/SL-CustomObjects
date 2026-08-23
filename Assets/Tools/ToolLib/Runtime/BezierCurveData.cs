using System;
using UnityEngine;

/// <summary>Up axis of the curve's editing plane. Y=up (XZ plane), Z=up (XY plane), X=up (YZ plane).
/// 曲线所在平面的向上轴。Y=上(XZ平面), Z=上(XY平面), X=上(YZ平面)。</summary>
public enum UpAxis { Y, Z, X }

/// <summary>Handle type (Blender-style). / 控制柄类型（仿 Blender）</summary>
public enum HandleType
{
    Auto,           // Auto: both ends aligned; angle/length follow neighbors. / 自动：曲柄两端对齐，根据相邻顶点自动调整角度和长度
    Aligned,        // Aligned: direction locked, length independent. / 对齐：方向锁定，长度独立
    AlignedLength,  // AlignedLength: direction + length fully mirrored. / 镜像：方向+长度完全镜像
    Vector,         // Vector: handle points at the neighbor; spans become straight. / 矢量：曲柄指向相邻顶点，段间为直线
    Free            // Free: unconstrained. / 自由：无任何约束
}

/// <summary>
/// 3D curve: Position=(worldX, worldZ) is the XZ projection, Height=worldY.
/// 2D curve: Position=(worldX, worldZ) is the XZ projection, Height=0.
/// Coordinate convention (matches Unity): X=right, Y=up (height), Z=forward.
/// 3D 曲线：Position=(worldX, worldZ) 是 XZ 平面投影，Height=worldY。
/// 2D 曲线：Position=(worldX, worldZ) 是 XZ 平面投影，Height=0。
/// 坐标系约定（与 Unity 一致）：X=右, Y=上（高度）, Z=前。
/// </summary>
[Serializable]
public class CurveVertex
{
    /// <summary>Vertex projection on the XZ plane (x=worldX, y=worldZ). World Y comes from Height.
    /// 顶点在 XZ 平面的投影坐标（x=世界X, y=世界Z）。世界 Y 由 Height 提供。</summary>
    public Vector2 Position;
    /// <summary>Vertex height in world space (world Y). Always 0 for 2D curves.
    /// 顶点在世界空间中的高度（世界 Y）。2D 曲线恒为 0。</summary>
    public float Height;
    /// <summary>Left-handle offset on the XZ plane (x=X offset, y=Z offset). Height offset comes from LeftHandleHeight.
    /// 左控制柄在 XZ 平面的偏移量（x=世界X偏移, y=世界Z偏移）。高度偏移由 LeftHandleHeight 提供。</summary>
    public Vector2 LeftHandle;
    /// <summary>Left-handle offset component along world Y (height).
    /// 左控制柄在世界 Y（高度）方向的偏移分量。</summary>
    public float LeftHandleHeight;
    public Vector2 RightHandle;
    /// <summary>Right-handle offset component along world Y (height).
    /// 右控制柄在世界 Y（高度）方向的偏移分量。</summary>
    public float RightHandleHeight;

    /// <summary>Left-handle type (Auto/Aligned/AlignedLength/Vector/Free) — constrains the right handle when the left moves.
    /// 左控制柄类型（Auto/Aligned/AlignedLength/Vector/Free），对应左柄移动时对右柄的约束</summary>
    public HandleType HandleTypeA = HandleType.Auto;
    /// <summary>Right-handle type (Auto/Aligned/AlignedLength/Vector/Free) — constrains the left handle when the right moves.
    /// 右控制柄类型（Auto/Aligned/AlignedLength/Vector/Free），对应右柄移动时对左柄的约束</summary>
    public HandleType HandleTypeB = HandleType.Auto;

    /// <summary>Per-axis locks for the vertex (protect axes during UI and SceneView drags).
    /// Axis mapping (matches the editing-plane convention): LockX = world X, LockY = plane-Y (Position.y, i.e. world Z),
    /// LockZ = world Y (Height). Field names are kept for serialization compatibility.
    /// 顶点分轴锁（UI 和 SceneView 拖拽时保护对应轴不被修改）。
    /// 轴映射（与编辑平面约定一致）：LockX=世界X，LockY=平面Y（Position.y，即世界 Z），LockZ=世界Y（Height）。
    /// 字段名保留原名以保证序列化兼容。</summary>
    public bool LockX, LockY, LockZ;
    /// <summary>Per-axis locks for the left handle. / 左控制柄分轴锁</summary>
    public bool LeftHandleLockX, LeftHandleLockY, LeftHandleLockZ;
    /// <summary>Per-axis locks for the right handle. / 右控制柄分轴锁</summary>
    public bool RightHandleLockX, RightHandleLockY, RightHandleLockZ;

    /// <summary>Whether this vertex is selected (session state). / 是否选中（会话状态）</summary>
    [NonSerialized] public bool IsSelected;
    /// <summary>0=none, 1=left handle, 2=right handle, 3=vertex itself.
    /// 0=无, 1=左曲柄, 2=右曲柄, 3=顶点自身</summary>
    [NonSerialized] public int SelectedSubElement;

    /// <summary>Creates a vertex with default horizontal handles. / 创建带默认水平控制柄的顶点</summary>
    public CurveVertex(Vector2 position)
    {
        Position = position;
        LeftHandle = new Vector2(-1f, 0f);
        RightHandle = new Vector2(1f, 0f);
    }

    /// <summary>Full 3D world position, matching Unity axes: (X, Y, Z) = (Position.x, Height, Position.y).
    /// 顶点完整 3D 世界坐标。与 Unity 坐标系一致：(X, Y, Z) = (Position.x, Height, Position.y)。</summary>
    public Vector3 PositionV3 => new Vector3(Position.x, Height, Position.y);
    /// <summary>Left-handle offset vector in 3D world space: (X, Y, Z) = (LeftHandle.x, LeftHandleHeight, LeftHandle.y).
    /// 左控制柄在 3D 世界空间中的偏移向量：(X偏移, Y偏移, Z偏移) = (LeftHandle.x, LeftHandleHeight, LeftHandle.y)。</summary>
    public Vector3 LeftHandleOffsetV3 => new Vector3(LeftHandle.x, LeftHandleHeight, LeftHandle.y);
    /// <summary>Right-handle offset vector in 3D world space: (X, Y, Z) = (RightHandle.x, RightHandleHeight, RightHandle.y).
    /// 右控制柄在 3D 世界空间中的偏移向量：(X偏移, Y偏移, Z偏移) = (RightHandle.x, RightHandleHeight, RightHandle.y)。</summary>
    public Vector3 RightHandleOffsetV3 => new Vector3(RightHandle.x, RightHandleHeight, RightHandle.y);

    /// <summary>World-space position of the left handle (vertex + offset). / 左控制柄世界位置</summary>
    public Vector2 LeftHandlePosition => Position + LeftHandle;
    /// <summary>World-space position of the right handle (vertex + offset). / 右控制柄世界位置</summary>
    public Vector2 RightHandlePosition => Position + RightHandle;

    /// <summary>
    /// Applies handle-type constraints after moving one handle.
    /// movedHandle=1 (left moved) → constrain right by HandleTypeA;
    /// movedHandle=2 (right moved) → constrain left by HandleTypeB.
    /// 应用控制柄类型约束（移动一侧曲柄后调用）。
    /// movedHandle=1 左柄被移动 → 基于 HandleTypeA 约束右柄；
    /// movedHandle=2 右柄被移动 → 基于 HandleTypeB 约束左柄。
    /// </summary>
    public void ApplyHandleType(int movedHandle, bool is3d = false)
    {
        if (movedHandle == 1)
        {
            // Left handle moved → constrain the right handle by the left handle's type.
            // 左柄被移动 → 根据左柄类型约束右柄
            switch (HandleTypeA)
            {
                case HandleType.Auto:
                    RightHandle = -LeftHandle;
                    if (is3d) RightHandleHeight = -LeftHandleHeight;
                    break;
                case HandleType.AlignedLength:
                case HandleType.Aligned:
                    // Sync only when the right handle is also an aligned/mirror family type.
                    // 仅当右柄也是对齐/镜像族时才同步
                    if (HandleTypeB == HandleType.AlignedLength || HandleTypeB == HandleType.Aligned || HandleTypeB == HandleType.Auto)
                    {
                        if (HandleTypeB == HandleType.AlignedLength)
                        {
                            RightHandle = -LeftHandle;
                            if (is3d) RightHandleHeight = -LeftHandleHeight;
                        }
                        else // Aligned: direction only.
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
            // Right handle moved → constrain the left handle by the right handle's type.
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
            // When the right moves and the left is aligned/mirror, sync the left passively.
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