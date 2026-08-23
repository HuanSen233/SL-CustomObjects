using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// Simple OBJ parser producing a flat triangle list.
    /// Handles: v (with optional vertex colors), mtllib / usemtl / Kd material colors,
    /// f faces (fan triangulation of n-gons, v/vt/vn references).
    /// X axis is mirrored (-x, y, z) to convert OBJ's right-handed system to Unity's
    /// left-handed one, then face winding is reversed to keep normals outward.
    /// 简单 OBJ 解析器，输出平面三角形列表。支持 v（含可选顶点色）、mtllib/usemtl/Kd 材质色、
    /// f 面（n-gon 扇形三角化，v/vt/vn 引用）。X 轴镜像 (-x,y,z) 以从 OBJ 右手系转到
    /// Unity 左手系，随后反转面绕序保持法线朝外。
    ///
    /// Ported from TriangleScpSl / ObjParser (Foibos, CC-BY-SA 3.0).
    /// 移植自 TriangleScpSl / ObjParser（Foibos，CC-BY-SA 3.0）。
    /// </summary>
    public static class ObjTriangleParser
    {
        /// <summary>
        /// Parse an OBJ file into triangles.
        /// 解析 OBJ 文件为三角形列表。
        /// </summary>
        public static bool TryParseFile(string filePath, Color fallbackColor, out List<TriangleData> triangles, out string error)
        {
            triangles = new List<TriangleData>();
            error = string.Empty;

            if (!File.Exists(filePath))
            {
                error = "File not found: " + filePath;
                return false;
            }

            try
            {
                var vertices = new List<Vector3>();
                var vertexColors = new List<Color?>();
                var materials = new Dictionary<string, Color>();
                Color? activeMaterialColor = null;
                string baseDir = Path.GetDirectoryName(filePath);

                // Try to load MTL file with the same name as the OBJ file
                // 尝试加载与 OBJ 同名的 MTL 文件
                string mtlPath = Path.ChangeExtension(filePath, ".mtl");
                if (!string.IsNullOrEmpty(baseDir))
                    mtlPath = Path.Combine(baseDir, Path.GetFileName(mtlPath));

                if (File.Exists(mtlPath))
                    ParseMtlFile(mtlPath, materials);

                string[] lines = File.ReadAllLines(filePath);

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex].Trim();

                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    if (line.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
                    {
                        ParseMaterialLibraries(line, baseDir, materials);
                        continue;
                    }

                    if (line.StartsWith("usemtl ", StringComparison.OrdinalIgnoreCase))
                    {
                        string materialName = line.Substring(7).Trim();
                        activeMaterialColor = materials.TryGetValue(materialName, out Color materialColor)
                            ? materialColor
                            : (Color?)null;
                        continue;
                    }

                    if (line.StartsWith("v ", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length < 4 ||
                            !TryParseFloat(parts[1], out float x) ||
                            !TryParseFloat(parts[2], out float y) ||
                            !TryParseFloat(parts[3], out float z))
                        {
                            error = "Invalid vertex at line " + (lineIndex + 1) + ".";
                            return false;
                        }

                        // Mirror X to convert right-handed OBJ into left-handed Unity space.
                        vertices.Add(new Vector3(-x, y, z));

                        if (parts.Length >= 7 &&
                            TryParseFloat(parts[4], out float r) &&
                            TryParseFloat(parts[5], out float g) &&
                            TryParseFloat(parts[6], out float b))
                        {
                            vertexColors.Add(NormalizeColor(r, g, b));
                        }
                        else
                        {
                            vertexColors.Add(null);
                        }

                        continue;
                    }

                    if (!line.StartsWith("f ", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string[] partsFace = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    if (partsFace.Length < 4)
                    {
                        error = "Face with less than 3 vertices at line " + (lineIndex + 1) + ".";
                        return false;
                    }

                    var faceIndices = new List<int>();

                    for (int i = 1; i < partsFace.Length; i++)
                    {
                        string vertexRef = partsFace[i];
                        string indexToken = vertexRef.Split('/')[0];

                        if (!int.TryParse(indexToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rawIndex))
                        {
                            error = "Invalid face index at line " + (lineIndex + 1) + ".";
                            return false;
                        }

                        if (!TryResolveIndex(rawIndex, vertices.Count, out int resolvedIndex))
                        {
                            error = "Face index out of range at line " + (lineIndex + 1) + ".";
                            return false;
                        }

                        faceIndices.Add(resolvedIndex);
                    }

                    // Mirroring X above reverses winding, so reverse the loop back to keep normals outward.
                    // 上面的 X 镜像反转了绕序，这里反转回去让法线朝外。
                    faceIndices.Reverse();

                    for (int i = 1; i < faceIndices.Count - 1; i++)
                    {
                        int i1 = faceIndices[0];
                        int i2 = faceIndices[i];
                        int i3 = faceIndices[i + 1];

                        Color triangleColor = ResolveTriangleColor(i1, i2, i3, activeMaterialColor, vertexColors, fallbackColor);
                        triangles.Add(new TriangleData(vertices[i1], vertices[i2], vertices[i3], triangleColor));
                    }
                }

                if (triangles.Count == 0)
                {
                    error = "No triangles parsed from OBJ.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "OBJ parse failed: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Parse mtllib line(s) referencing material files.
        /// 解析引用材质文件的 mtllib 行。
        /// </summary>
        static void ParseMaterialLibraries(string line, string baseDir, Dictionary<string, Color> materials)
        {
            string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < tokens.Length; i++)
            {
                string mtlName = tokens[i];
                string mtlPath = string.IsNullOrEmpty(baseDir) ? mtlName : Path.Combine(baseDir, mtlName);

                if (!File.Exists(mtlPath))
                    continue;

                ParseMtlFile(mtlPath, materials);
            }
        }

        /// <summary>
        /// Parse an MTL file: newmtl blocks with Kd (diffuse) color.
        /// 解析 MTL 文件：newmtl 块中的 Kd（漫反射）颜色。
        /// </summary>
        static void ParseMtlFile(string mtlPath, Dictionary<string, Color> materials)
        {
            string currentMaterial = null;

            foreach (string rawLine in File.ReadAllLines(mtlPath))
            {
                string line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("newmtl ", StringComparison.OrdinalIgnoreCase))
                {
                    currentMaterial = line.Substring(7).Trim();
                    continue;
                }

                if (currentMaterial == null || !line.StartsWith("Kd ", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] kdParts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (kdParts.Length < 4 ||
                    !TryParseFloat(kdParts[1], out float r) ||
                    !TryParseFloat(kdParts[2], out float g) ||
                    !TryParseFloat(kdParts[3], out float b))
                {
                    continue;
                }

                materials[currentMaterial] = NormalizeColor(r, g, b);
            }
        }

        /// <summary>
        /// Resolve triangle color: active material color first, else averaged vertex colors, else fallback.
        /// 解析三角形颜色：优先当前材质色，其次顶点色平均，最后回退色。
        /// </summary>
        static Color ResolveTriangleColor(int i1, int i2, int i3, Color? materialColor, List<Color?> vertexColors, Color fallbackColor)
        {
            if (materialColor.HasValue)
                return materialColor.Value;

            Color accumulated = Color.black;
            int count = 0;

            Color? c1 = vertexColors[i1];
            Color? c2 = vertexColors[i2];
            Color? c3 = vertexColors[i3];

            if (c1.HasValue) { accumulated += c1.Value; count++; }
            if (c2.HasValue) { accumulated += c2.Value; count++; }
            if (c3.HasValue) { accumulated += c3.Value; count++; }

            return count > 0 ? accumulated / count : fallbackColor;
        }

        /// <summary>
        /// Normalize 0..1 or 0..255 color values.
        /// 归一化 0..1 或 0..255 的颜色值。
        /// </summary>
        static Color NormalizeColor(float r, float g, float b)
        {
            if (r > 1f || g > 1f || b > 1f)
                return new Color(Mathf.Clamp01(r / 255f), Mathf.Clamp01(g / 255f), Mathf.Clamp01(b / 255f), 1f);

            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
        }

        /// <summary>
        /// Resolve an OBJ index (1-based, negative = relative to end).
        /// 解析 OBJ 索引（1 基，负值表示相对末尾）。
        /// </summary>
        static bool TryResolveIndex(int rawIndex, int vertexCount, out int resolvedIndex)
        {
            resolvedIndex = rawIndex > 0 ? rawIndex - 1 : vertexCount + rawIndex;
            return resolvedIndex >= 0 && resolvedIndex < vertexCount;
        }

        static bool TryParseFloat(string token, out float value)
        {
            return float.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }
    }
}
