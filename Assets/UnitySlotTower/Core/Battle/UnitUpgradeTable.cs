using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>強化の1段階（plan 5章）。倍率は基本ステータスにかける。</summary>
    public sealed class UnitUpgradeLevel
    {
        /// <summary>表に行がない種類の段階1（倍率1）。</summary>
        public static readonly UnitUpgradeLevel Base = new UnitUpgradeLevel(1, 1, 1, 1, 0);

        internal UnitUpgradeLevel(int level, double hpMult, double atkMult, double defMult, int cost)
        {
            Level = level;
            HpMult = hpMult;
            AtkMult = atkMult;
            DefMult = defMult;
            Cost = cost;
        }

        public int Level { get; }
        public double HpMult { get; }
        public double AtkMult { get; }
        public double DefMult { get; }

        /// <summary>1つ前の段階からこの段階へ上げる費用（コイン）。段階1では使わない。</summary>
        public int Cost { get; }
    }

    /// <summary>unit_upgrades.csv の種類×段階ごとの倍率と費用。</summary>
    public sealed class UnitUpgradeTable
    {
        public const string FileName = "unit_upgrades.csv";

        private readonly Dictionary<string, List<UnitUpgradeLevel>> _levels =
            new Dictionary<string, List<UnitUpgradeLevel>>();

        private UnitUpgradeTable()
        {
        }

        /// <summary>表に行がない種類は 1（強化できない）。</summary>
        public int MaxLevel(string unitId) => _levels.TryGetValue(unitId, out var levels) ? levels.Count : 1;

        /// <param name="level">1 〜 <see cref="MaxLevel"/>。</param>
        public UnitUpgradeLevel Get(string unitId, int level)
        {
            if (!_levels.TryGetValue(unitId, out var levels))
            {
                return UnitUpgradeLevel.Base;
            }

            return levels[level - 1];
        }

        /// <summary>種類ごとに段階1から抜けなく並んでいること（行の順は問わない）。</summary>
        public static UnitUpgradeTable Load(CsvTable table, UnitTable units)
        {
            var upgrades = new UnitUpgradeTable();
            var rows = new Dictionary<string, SortedDictionary<int, CsvRow>>();
            foreach (var row in table.Rows)
            {
                var id = row.Get("unit_id");
                if (!units.Contains(id))
                {
                    throw row.Error("unit_id", $"味方 '{id}' がない");
                }

                if (!rows.TryGetValue(id, out var byLevel))
                {
                    byLevel = new SortedDictionary<int, CsvRow>();
                    rows.Add(id, byLevel);
                }

                var level = row.GetInt("level");
                if (byLevel.ContainsKey(level))
                {
                    throw row.Error("level", $"味方 '{id}' の段階 {level} が重複している");
                }

                byLevel.Add(level, row);
            }

            foreach (var pair in rows)
            {
                var levels = new List<UnitUpgradeLevel>();
                foreach (var entry in pair.Value)
                {
                    if (entry.Key != levels.Count + 1)
                    {
                        throw entry.Value.Error("level", $"味方 '{pair.Key}' の段階 {levels.Count + 1} がない");
                    }

                    var row = entry.Value;
                    levels.Add(new UnitUpgradeLevel(entry.Key, row.GetDouble("hp_mult"), row.GetDouble("atk_mult"),
                        row.GetDouble("def_mult"), row.GetInt("cost")));
                }

                upgrades._levels.Add(pair.Key, levels);
            }

            return upgrades;
        }
    }
}
