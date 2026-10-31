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
            };
        }
    }
}
