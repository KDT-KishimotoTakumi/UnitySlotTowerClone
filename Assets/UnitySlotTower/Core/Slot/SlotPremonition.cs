using System;
using System.Collections.Generic;
using System.Globalization;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// slot_premonition.csv の役ごとの先バレ発生率と、レバーオン時の先バレ抽選（FR-044）。
    /// 表にない役は発生しない。ハズレの行は「ガセ」の発生率（plan 5章）。
    /// </summary>
    public sealed class SlotPremonition
    {
        public const string FileName = "slot_premonition.csv";

        private readonly Dictionary<string, double> _rates = new Dictionary<string, double>();

        private SlotPremonition()
        {
        }

        /// <summary>フラグの先バレ発生率。重複当選はその役のうち最も高い率（plan 5章）。</summary>
        public double Rate(SlotFlag flag)
        {
            if (flag.Roles.Count == 0)
            {
                return RoleRate(SlotFlag.HazureId);
            }

            var rate = 0.0;
            foreach (var role in flag.Roles)
            {
                rate = Math.Max(rate, RoleRate(role));
            }

            return rate;
        }

        /// <summary>先バレの抽選。発生率が 0 のフラグでは乱数を使わない。</summary>
        public bool Draw(IRandom random, SlotFlag flag)
        {
            var rate = Rate(flag);
            return rate > 0 && random.NextDouble() < rate;
        }

        private double RoleRate(string role) => _rates.TryGetValue(role, out var rate) ? rate : 0;

        public static SlotPremonition Load(CsvTable table, SlotPayouts payouts)
        {
            var result = new SlotPremonition();
            foreach (var row in table.Rows)
            {
                var role = row.Get("role");
                if (role != SlotFlag.HazureId && !payouts.Contains(role))
                {
                    throw row.Error("role", $"役 '{role}' が {SlotPayouts.FileName} にない");
                }

                if (result._rates.ContainsKey(role))
                {
                    throw row.Error("role", $"役 '{role}' が重複している");
                }

                var rate = row.GetProbability("rate");
                if (rate < 0 || rate > 1)
                {
                    throw row.Error("rate", $"発生率が 0〜1 の範囲外: {rate.ToString(CultureInfo.InvariantCulture)}");
                }

                result._rates.Add(role, rate);
            }

            return result;
        }
    }
}
