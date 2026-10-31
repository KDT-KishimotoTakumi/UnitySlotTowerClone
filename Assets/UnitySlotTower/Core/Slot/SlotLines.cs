using System.Collections.Generic;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>有効ライン5本（上・中・下段と斜め2本）で揃った役の判定（plan 5章）。</summary>
    public static class SlotLines
    {
        public const int LineCount = 5;

        public const string CherryRole = "cherry";
        public const string CenterCherryRole = "center_cherry";

        // [ライン, リール] の段（-1 上段・0 中段・1 下段）。上・中・下・左上から右下・左下から右上。
        private static readonly int[,] Rows =
        {
            { -1, -1, -1 },
            { 0, 0, 0 },
            { 1, 1, 1 },
            { -1, 0, 1 },
            { 1, 0, -1 },
        };

        /// <summary>停止位置（左・中・右）で揃った役を、払い出し表の順に重複なく返す。</summary>
        public static List<string> Aligned(SlotReels reels, SlotPayouts payouts, IReadOnlyList<int> stops)
        {
            var aligned = new List<string>();
            foreach (var payout in payouts.All)
            {
                for (var line = 0; line < LineCount; line++)
                {
                    if (CanUseLine(payout.Role, line) && Matches(reels, payout, stops, line))
                    {
                        aligned.Add(payout.Role);
                        break;
                    }
                }
            }

            return aligned;
        }

        // チェリーは左リールの段で区別する: 中段なら中段チェリー、上段・下段なら角チェリー。
        private static bool CanUseLine(string role, int line)
        {
            switch (role)
            {
                case CherryRole: return Rows[line, 0] != 0;
                case CenterCherryRole: return Rows[line, 0] == 0;
                default: return true;
            }
        }

        private static bool Matches(SlotReels reels, SlotPayout payout, IReadOnlyList<int> stops, int line)
        {
            for (var reel = 0; reel < SlotReels.ReelCount; reel++)
            {
                var symbol = payout.Symbols[reel];
                if (symbol.HasValue && reels.At(reel, stops[reel] + Rows[line, reel]) != symbol.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
