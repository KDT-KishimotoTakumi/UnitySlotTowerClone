using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>リールの図柄（plan 5章）。</summary>
    public enum SlotSymbol
    {
        Seven,
        Bar,
        Bell,
        Watermelon,
        Cherry,
        Replay,
    }

    public static class SlotSymbols
    {
        /// <summary>払い出し表で「どの図柄でもよい」を表す CSV の値。</summary>
        public const string AnyId = "any";

        /// <summary>CSV の図柄 ID（例 <c>seven</c>）を読む。</summary>
        public static SlotSymbol Parse(CsvRow row, string column)
        {
            switch (row.Get(column))
            {
                case "seven": return SlotSymbol.Seven;
                case "bar": return SlotSymbol.Bar;
                case "bell": return SlotSymbol.Bell;
                case "watermelon": return SlotSymbol.Watermelon;
                case "cherry": return SlotSymbol.Cherry;
                case "replay": return SlotSymbol.Replay;
                default: throw row.Error(column, $"未知の図柄 '{row.Get(column)}'");
            }
        }

        /// <summary>図柄 ID か <c>any</c>（null を返す）を読む。</summary>
        public static SlotSymbol? ParseOrAny(CsvRow row, string column) =>
            row.Get(column) == AnyId ? (SlotSymbol?)null : Parse(row, column);
    }
}
