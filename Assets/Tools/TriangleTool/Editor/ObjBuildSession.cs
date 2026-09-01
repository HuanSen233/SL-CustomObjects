using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TriangleTool.EditorTools
{
    /// <summary>
    /// Batched editor build session for either the Edit tab's face-group generate or the Model Import tab's
    /// OBJ build: feeds triangles into the unified TriangleModelBuilder across multiple EditorApplication.update
    /// ticks, capping the number of spawned blocks per frame, then calls Finish() (mode post-processing) when done.
    /// Keeps the editor responsive on large models / large selections.
    /// 分帧编辑器构建会话（编辑页三角面组构建或模型导入 OBJ 构建共用）：跨多个 EditorApplication.update 帧
    /// 把三角形喂给统一 TriangleModelBuilder，并限制每帧生成的块数，完成后调用 Finish()（模式后处理）。
    /// 大模型/大选区不卡编辑器。
    /// </summary>
    public class ObjBuildSession
    {
        readonly TriangleModelBuilder _builder;
        readonly Queue<TriangleData> _queue = new Queue<TriangleData>();
        readonly int _maxBlocksPerFrame;
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
            int maxBlocksPerFrame = 120, Action onComplete = null)
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
            _maxBlocksPerFrame = Mathf.Max(1, maxBlocksPerFrame);
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
            // Feed triangles until this frame's spawned-block budget is reached (measure the block delta
            // each triangle). A single triangle may exceed the budget; we stop after it (can't split it).
            // 本帧喂入三角形直到达到本轮生成的块数预算（每三角形测一次块数增量）。
            // 单个三角形可能超出预算；构建完该三角形后即停止（三角形不可拆分）。
            int blocks = 0;

            while (_queue.Count > 0)
            {
                int before = _builder.BlocksBuilt;
                TriangleData tri = _queue.Dequeue();
                _builder.BuildOneTriangle(in tri);
                TrianglesBuilt++;
                blocks += _builder.BlocksBuilt - before;
                if (blocks >= _maxBlocksPerFrame)
                    break;
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
