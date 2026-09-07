using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// OBJ model loader: resolves a model path (bare file name or absolute path) and
    /// produces the flat triangle list for the V1 (Exact) renderer.
    /// OBJ 模型加载器：解析模型路径（文件名或绝对路径）并产出 V1 (Exact) 渲染用的平面三角形列表。
    ///
    /// Ported from TriangleScpSl / ModelFactory (Foibos, CC-BY-SA 3.0).
    /// 移植自 TriangleScpSl / ModelFactory（Foibos，CC-BY-SA 3.0）。
    /// </summary>
    public static class ObjModelLoader
    {
        /// <summary>
        /// Directory used to resolve bare file names (e.g. "Suzanne" or "Suzanne.obj").
        /// Defaults to Assets/Tools/TriangleTool/TestModels.
        /// 用于解析裸文件名的目录（如 "Suzanne" 或 "Suzanne.obj"），默认 Assets/Tools/TriangleTool/TestModels。
        /// </summary>
        public static string ModelsDirectory { get; set; }

        static ObjModelLoader()
        {
            ModelsDirectory = Path.Combine(Application.dataPath, "Tools", "TriangleTool", "TestModels");
        }

        /// <summary>
        /// Load an OBJ into triangles.
        /// 加载 OBJ 为三角形列表。
        /// </summary>
        /// <param name="requestedFile">Bare file name (resolved against ModelsDirectory) or absolute path.
        /// 裸文件名（相对 ModelsDirectory 解析）或绝对路径。</param>
        /// <param name="fallbackColor">Color used when the OBJ has no material/vertex colors / 无颜色时的回退色。</param>
        /// <param name="forceObjColor">Overwrite parsed colors with fallbackColor / 用回退色覆盖解析出的颜色。</param>
        public static bool TryLoadTriangles(
            string requestedFile, Color fallbackColor, bool forceObjColor,
            out List<TriangleData> triangles, out string normalizedFileName, out string error)
        {
            triangles = new List<TriangleData>();
            normalizedFileName = string.Empty;
            error = string.Empty;

            if (!TryResolveModelPath(requestedFile, out string modelPath, out normalizedFileName, out error))
                return false;

            if (!ObjTriangleParser.TryParseFile(modelPath, fallbackColor, out List<TriangleData> parsedTriangles, out string parseError))
            {
                error = "Failed to parse OBJ: " + parseError;
                return false;
            }

            triangles = parsedTriangles;

            if (forceObjColor)
            {
                for (int i = 0; i < triangles.Count; i++)
                {
                    TriangleData tri = triangles[i];
                    triangles[i] = new TriangleData(tri.P1, tri.P2, tri.P3, fallbackColor);
                }
            }

            if (triangles.Count == 0)
            {
                error = "No valid non-degenerate triangles found in model file.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolve the request into an existing .obj path.
        /// 把请求解析为存在的 .obj 路径。
        /// </summary>
        static bool TryResolveModelPath(string requestedFile, out string modelPath, out string normalizedFileName, out string error)
        {
            modelPath = string.Empty;
            normalizedFileName = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(requestedFile))
            {
                error = "Model file name cannot be empty.";
                return false;
            }

            string fileName = Path.GetFileName(requestedFile);

            if (!string.Equals(requestedFile, fileName, StringComparison.Ordinal))
            {
                // Absolute or relative path: use it directly when it exists.
                // 绝对或相对路径：存在则直接使用。
                if (File.Exists(requestedFile))
                {
                    modelPath = requestedFile;
                    normalizedFileName = fileName;
                    return true;
                }

                error = "Model file not found: " + requestedFile;
                return false;
            }

            string extension = Path.GetExtension(fileName);

            if (string.IsNullOrEmpty(extension))
            {
                string objName = fileName + ".obj";
                string objPath = Path.Combine(ModelsDirectory, objName);

                if (File.Exists(objPath))
                    fileName = objName;
                else
                {
                    error = "Model file not found: " + objPath;
                    return false;
                }
            }
            else if (!fileName.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only .obj files are supported.";
                return false;
            }

            modelPath = Path.Combine(ModelsDirectory, fileName);

            if (!File.Exists(modelPath))
            {
                error = "Model file not found: " + modelPath;
                return false;
            }

            normalizedFileName = fileName;
            return true;
        }
    }
}
