using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>役1つの払い出しと揃う図柄。払い出しは1ゲーム1回（ラインの数で加算しない）。</summary>
    public sealed class SlotPayout
    {
        internal SlotPayout(string role, int payout, SlotSymbol?[] symbols)
        {
            Role = role;
            Payout = payout;
            Symbols = symbols;
        }

        public string Role { get; }
        public int Payout { get; }

        /// <summary>左・中・右の図柄。null はどの図柄でもよい。</summary>
        public IReadOnlyList<SlotSymbol?> Symbols { get; }
    }

    /// <summary>slot_payouts.csv の役一覧。</summary>
    public sealed class SlotPayouts
    {
        public const string FileName = "slot_payouts.csv";

        private readonly Dictionary<string, SlotPayout> _roles = new Dictionary<string, SlotPayout>();
        private readonly List<SlotPayout> _all = new List<SlotPayout>();
        private readonly string _fileName;

        private SlotPayouts(string fileName)
        {
            _fileName = fileName;
        }

        public IReadOnlyList<SlotPayout> All => _all;

        public bool Contains(string role) => _roles.ContainsKey(role);

        public SlotPayout Get(string role)
        {
            if (_roles.TryGetValue(role, out var payout))
            {
                return payout;
            }

            throw new CsvFormatException(new CsvError(_fileName, 0, 0, $"役 '{role}' がない"));
        }

        public static SlotPayouts Load(CsvTable table)
        {
            var payouts = new SlotPayouts(table.FileName);
            foreach (var row in table.Rows)
            {
                var role = row.Get("role");
                if (payouts._roles.ContainsKey(role))
                {
                    throw row.Error("role", $"役 '{role}' が重複している");
                }

                var symbols = new SlotSymbol?[SlotReels.ReelCount];
                for (var reel = 0; reel < symbols.Length; reel++)
                {
                    symbols[reel] = SlotSymbols.ParseOrAny(row, $"symbol{reel + 1}");
                }

                var payout = new SlotPayout(role, row.GetInt("payout"), symbols);
                payouts._roles.Add(role, payout);
                payouts._all.Add(payout);
            }

            return payouts;
        }
    }
}
