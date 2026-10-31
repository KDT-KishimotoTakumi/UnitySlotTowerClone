using System.Collections.Generic;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 種類ごとの無料ストック（FR-015, FR-016）。ウェーブ終了時に配置中だった味方の数だけ増え、コインなしの配置で1つ減る。
    /// ウェーブをまたいで残す。
    /// </summary>
    public sealed class UnitStock
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public int Count(string unitId) => _counts.TryGetValue(unitId, out var count) ? count : 0;

        public void Add(string unitId, int count)
        {
            _counts[unitId] = Count(unitId) + count;
        }

        /// <summary>1つ使う。なければ false で何も変えない。</summary>
        public bool TryTake(string unitId)
        {
            var count = Count(unitId);
            if (count <= 0)
            {
                return false;
            }

            _counts[unitId] = count - 1;
            return true;
        }
    }
}
