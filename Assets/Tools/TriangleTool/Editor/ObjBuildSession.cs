using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// Batched editor build session for OBJ models: feeds triangles into the unified
    /// TriangleModelBuilder across multiple EditorApplication.update ticks, then calls
    /// Finish() (mode post-processing) when done. Keeps the editor responsive on large models.
    /// 用于 OBJ 模型的分帧编辑器构建会话：跨多个 EditorApplication.update 帧把三角形喂给统一
    /// TriangleModelBuilder，完成后调用 Finish()（模式后处理）。大模型不卡编辑器。
    /// </summary>
    public class ObjBuildSession
    {
        readonly TriangleModelBuilder _builder;
        readonly Queue<TriangleData> _queue = new Queue<TriangleData>();
        readonly int _batchSize;
        readonly Action _onComplete;

        /// <summary>The underlying builder accumulating geometry / 底层构建器（累积几何）。</summary>
        public TriangleModelBuilder Builder => _builder;

        public bool IsRunning { get; private set; }
        public int TotalTriangles { get; private set; }
        public int TrianglesBuilt { get; private set; }

        public float Progress => TotalTriangles == 0 ? 0f : (float)TrianglesBuilt / TotalTriangles;

        /// <summary>
        /// Create a build session. Call Start() to begin.
        /// 创建构建会话，调用 Start() 开始。
        /// </summary>
        public ObjBuildSession(string rootName, TriangleBuildMode mode,
            float accuracy, int optimizationPasses, bool collidable,
            bool useRectangleOptimization, float rectangleTolerance, bool flipWinding,
            int batchSize = 120, Action onComplete = null)
        {
            _builder = new TriangleModelBuilder
            {
                Mode = mode,
                Accuracy = accuracy,
                OptimizationPasses = optimizationPasses,
                Collidable = collidable,
                UseRectangleOptimization = useRectangleOptimization,
                RectangleTolerance = rectangleTolerance,
                FlipWinding = flipWinding,
            };
            _builder.EnsureRoot(rootName);
            _batchSize = Mathf.Max(1, batchSize);
            _onComplete = onComplete;
        }

        /// <summary>
        /// Start building the given triangles. Any previous run is cancelled first.
        /// 开始构建给定三角形；先取消之前的运行。
        /// </summary>
        public void Start(IList<TriangleData> triangles)
        {
            Cancel();

            _queue.Clear();
            foreach (TriangleData tri in triangles)
                _queue.Enqueue(tri);

            TotalTriangles = _queue.Count;
            TrianglesBuilt = 0;
            IsRunning = true;
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// Stop building without destroying what was already built.
        /// 停止构建（不销毁已构建内容）。
        /// </summary>
        public void Cancel()
        {
            if (IsRunning)
                EditorApplication.update -= Tick;

            IsRunning = false;
        }

        void Tick()
        {
            int built = 0;

            while (_queue.Count > 0 && built < _batchSize)
            {
                TriangleData tri = _queue.Dequeue();
                _builder.BuildOneTriangle(in tri);
                TrianglesBuilt++;
                built++;
            }

            if (_queue.Count == 0)
            {
                EditorApplication.update -= Tick;
                IsRunning = false;

                // Run the mode-specific post-processing phase (consolidation/sweeps/cleanup).
                _builder.Finish();

                Action cb = _onComplete;
                if (cb != null)
                    cb();
            }
        }
    }
}
