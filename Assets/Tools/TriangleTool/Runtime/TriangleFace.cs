using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// A single triangle face — pure editable data (three world-space points + color + flags),
    /// creates no GameObjects. Non-serialized selection state (selected face / selected vertex)
    /// is session-only. The generated model is produced separately from this data (TriangleModelBuilder).
    /// 单个三角面 — 纯可编辑数据（三个世界坐标点 + 颜色 + 开关），不产生 GameObject。
    /// 非序列化选中态（选中面/选中顶点）仅为会话状态。生成模型由本数据另行产出（TriangleModelBuilder）。
    /// </summary>
    [System.Serializable]
    public class TriangleFace
    {
        public string Name;

        /// <summary>Three world-space vertices. / 三个世界坐标顶点</summary>
        public Vector3 P1;
        public Vector3 P2;
        public Vector3 P3;

        /// <summary>Face color used for preview outline and generated quads. / 面颜色（预览轮廓与生成的 Quad 使用）</summary>
        public Color Color = new Color(1f, 0.3f, 0.7f, 1f);

        /// <summary>Enabled faces take part in generation. / 启用面参与生成</summary>
        public bool IsEnabled = true;
        /// <summary>Visible faces are drawn in the Scene view. / 可见面在场景视图绘制</summary>
        public bool IsVisible = true;
        /// <summary>Locked faces cannot be edited/dragged. / 锁定面不可编辑/拖拽</summary>
        public bool IsLocked;

        /// <summary>Session-only selection flag. / 仅为会话的选中标志</summary>
        [System.NonSerialized] public bool IsSelected;

        public TriangleFace() { }

        public TriangleFace(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public TriangleFace(Vector3 p1, Vector3 p2, Vector3 p3, Color color)
        {
            P1 = p1;
            P2 = p2;
            P3 = p3;
            Color = color;
        }

        /// <summary>Creates a non-degenerate default triangle face (in the XZ plane, normal = ±Y).
        /// 创建一个非退化的默认三角面（位于 XZ 平面，法线 = ±Y）。</summary>
        public static TriangleFace CreateDefault(string name = "NewTriangle")
        {
            return new TriangleFace(new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 2f))
            {
                Name = name,
            };
        }
    }
}
