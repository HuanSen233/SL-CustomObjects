using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// A single triangle with its color (parsed from OBJ materials or vertex colors).
    /// 单个三角形及其颜色（来自 OBJ 材质或顶点色）。
    /// </summary>
    public struct TriangleData
    {
        public Vector3 P1;
        public Vector3 P2;
        public Vector3 P3;
        public Color Color;

        public TriangleData(Vector3 p1, Vector3 p2, Vector3 p3, Color color)
        {
            P1 = p1;
            P2 = p2;
            P3 = p3;
            Color = color;
        }
    }
}
