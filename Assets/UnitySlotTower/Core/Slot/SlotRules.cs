using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>slot_rules.csv の値（plan 5章 スロット）。</summary>
    public sealed class SlotRules
    {
        public const string FileName = "slot_rules.csv";

        public int BigPayoutLimit { get; private set; }
        public int RegPayoutLimit { get; private set; }
        public int KakuhenGames { get; private set; }
        public double KakuhenEntryRate { get; private set; }
        public double WaitSeconds { get; private set; }
        public int MaxSlide { get; private set; }

        /// <summary>攻撃力上昇の倍率と秒数（spec 002 FR-111）。</summary>
        public double AttackBuffRate { get; private set; }
        public double AttackBuffSeconds { get; private set; }

        /// <summary>攻撃速度上昇で攻撃間隔にかける倍率と秒数（spec 002 FR-112）。</summary>
        public double SpeedBuffIntervalRate { get; private set; }
        public double SpeedBuffSeconds { get; private set; }

        /// <summary>HP回復で最大 HP にかける割合（spec 002 FR-114）。</summary>
        public double HealBuffRate { get; private set; }

        public static SlotRules Load(CsvTable table)
        {
            var values = new KeyValueTable(table);
            return new SlotRules
            {
                BigPayoutLimit = values.GetInt("big_payout_limit"),
                RegPayoutLimit = values.GetInt("reg_payout_limit"),
                KakuhenGames = values.GetInt("kakuhen_games"),
                KakuhenEntryRate = values.GetDouble("kakuhen_entry_rate"),
                WaitSeconds = values.GetDouble("wait_seconds"),
                MaxSlide = values.GetInt("max_slide"),
                AttackBuffRate = values.GetDouble("attack_buff_rate"),
                AttackBuffSeconds = values.GetDouble("attack_buff_seconds"),
                SpeedBuffIntervalRate = values.GetDouble("speed_buff_interval_rate"),
                SpeedBuffSeconds = values.GetDouble("speed_buff_seconds"),
                HealBuffRate = values.GetDouble("heal_buff_rate"),
            };
        }
    }
}
