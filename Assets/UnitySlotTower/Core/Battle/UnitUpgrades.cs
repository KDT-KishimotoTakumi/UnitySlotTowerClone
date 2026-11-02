using System.Collections.Generic;
using UnitySlotTower.Core.Economy;

namespace UnitySlotTower.Core.Battle
{
    public enum UpgradeResult
    {
        Upgraded,
        MaxLevel,
        NotEnoughCoins,
    }

    /// <summary>
    /// 種類ごとの強化段階と支払い（FR-026）。ウェーブをまたいで残る。
    /// 配置中の個体への反映は <see cref="Battlefield.ApplyUpgrade"/> で行う。
    /// </summary>
    public sealed class UnitUpgrades
    {
        private readonly UnitUpgradeTable _table;
        private readonly CoinWallet _wallet;
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        public UnitUpgrades(UnitUpgradeTable table, CoinWallet wallet)
        {
            _table = table;
            _wallet = wallet;
        }

        /// <summary>いまの段階（1 から）。</summary>
        public int Level(string unitId) => _levels.TryGetValue(unitId, out var level) ? level : 1;

        /// <summary>いまの段階の倍率。</summary>
        public UnitUpgradeLevel Current(string unitId) => _table.Get(unitId, Level(unitId));

        /// <summary>次の段階へ上げる費用。最大段階なら null。</summary>
        public int? NextCost(string unitId)
        {
            var level = Level(unitId);
            return level < _table.MaxLevel(unitId) ? _table.Get(unitId, level + 1).Cost : (int?)null;
        }

        /// <summary>いま強化できるかだけを調べる（何も変えない）。</summary>
        public UpgradeResult Check(string unitId)
        {
            var cost = NextCost(unitId);
            if (cost == null)
            {
                return UpgradeResult.MaxLevel;
            }

            return _wallet.Coins < cost.Value ? UpgradeResult.NotEnoughCoins : UpgradeResult.Upgraded;
        }

        /// <summary>拒否したときは何も変えない（コイン・段階）。</summary>
        public UpgradeResult TryUpgrade(string unitId)
        {
            var result = Check(unitId);
            if (result != UpgradeResult.Upgraded)
            {
                return result;
            }

            _wallet.TrySpend(NextCost(unitId).Value);
            _levels[unitId] = Level(unitId) + 1;
            return result;
        }
    }
}
