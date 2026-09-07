using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TriangleTool
{
    /// <summary>
    /// 2D spatial hash over (theta, phi) angle space for fast stretch lookup.
    /// A "stretch" is an invisible transform that deforms child quads; parallelograms
    /// with similar angles can share one stretch.
    /// (theta, phi) 角度空间的 2D 空间哈希，用于快速查找可复用的 stretch。
    /// "stretch" 是变形子 Quad 的不可见变换；角度相近的平行四边形可共享同一个 stretch。
    ///
    /// Ported from TriangleScpSl / StretchSpatialIndex (Foibos, CC-BY-SA 3.0).
    /// 移植自 TriangleScpSl / StretchSpatialIndex（Foibos，CC-BY-SA 3.0）。
    /// </summary>
    public class StretchSpatialIndex
    {
        readonly Dictionary<(int, int), List<Entry>> _cells = new Dictionary<(int, int), List<Entry>>();
        readonly float _cellSize;   // radians / 弧度
        readonly int _searchRadius; // cells to scan in each direction / 每个方向扫描的格数

        /// <param name="cellSize">Size of a cell in radians. 0.05 is a reasonable default.
        /// 单元格大小（弧度），0.05 为合理默认值。</param>
        /// <param name="maxAngularTolerance">
        /// Worst-case angular distance between an acceptable candidate and the query point.
        /// Determines how many neighboring cells to scan.
        /// 可接受候选与查询点之间最坏角度距离，决定扫描多少邻格。</param>
        public StretchSpatialIndex(float cellSize = 0.05f, float maxAngularTolerance = 0.1f)
        {
            _cellSize = cellSize;
            _searchRadius = Mathf.Max(1, Mathf.CeilToInt(maxAngularTolerance / cellSize));
        }

        public int Count { get; private set; }

        (int, int) CellOf(float theta, float phi)
            => (Mathf.FloorToInt(theta / _cellSize), Mathf.FloorToInt(phi / _cellSize));

        public void Add(float theta, float phi, GameObject stretch)
        {
            (int, int) key = CellOf(theta, phi);

            if (!_cells.TryGetValue(key, out List<Entry> list))
            {
                list = new List<Entry>(4);
                _cells[key] = list;
            }

            list.Add(new Entry(theta, phi, stretch));
            Count++;
        }

        /// <summary>
        /// Enumerate all entries in the cell containing (theta, phi) and its neighbors.
        /// Caller filters by actual geometric error.
        /// 枚举包含 (theta, phi) 的单元格及其邻格内的所有条目；调用方按实际几何误差过滤。
        /// </summary>
        public IEnumerable<Entry> QueryNearby(float theta, float phi)
        {
            (int ct, int cp) = CellOf(theta, phi);

            for (int dt = -_searchRadius; dt <= _searchRadius; dt++)
            {
                for (int dp = -_searchRadius; dp <= _searchRadius; dp++)
                {
                    (int, int) key = (ct + dt, cp + dp);

                    if (_cells.TryGetValue(key, out List<Entry> list))
                    {
                        foreach (Entry e in list)
                            yield return e;
                    }
                }
            }
        }

        public IEnumerable<Entry> All()
        {
            return _cells.Values.SelectMany(list => list);
        }

        /// <summary>Removes a stretch entry by its GameObject. Returns true when found.
        /// 按 GameObject 移除 stretch 条目，找到返回 true。</summary>
        public bool Remove(GameObject stretch)
        {
            foreach (List<Entry> list in _cells.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!ReferenceEquals(list[i].Stretch, stretch))
                        continue;

                    list.RemoveAt(i);
                    Count--;
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            _cells.Clear();
            Count = 0;
        }

        public readonly struct Entry
        {
            public readonly float Theta;
            public readonly float Phi;
            public readonly GameObject Stretch;

            public Entry(float theta, float phi, GameObject stretch)
            {
                Theta = theta;
                Phi = phi;
                Stretch = stretch;
            }
        }
    }
}
