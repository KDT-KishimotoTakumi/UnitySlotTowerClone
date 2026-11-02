using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>リールの図柄（spec 002 E-06）。攻撃力・攻撃速度・HP回復はバフ役、コインは小役の図柄。</summary>
    public enum SlotSymbol
    {
        Seven,
        Bar,
        Attack,
        Speed,
        Heal,
        Coin,
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
                case "attack": return SlotSymbol.Attack;
                case "speed": return SlotSymbol.Speed;
                case "heal": return SlotSymbol.Heal;
                case "coin": return SlotSymbol.Coin;
                default: throw row.Error(column, $"未知の図柄 '{row.Get(column)}'");
            }
        }

        /// <summary>図柄 ID か <c>any</c>（null を返す）を読む。</summary>
        public static SlotSymbol? ParseOrAny(CsvRow row, string column) =>
            row.Get(column) == AnyId ? (SlotSymbol?)null : Parse(row, column);
    }
}
