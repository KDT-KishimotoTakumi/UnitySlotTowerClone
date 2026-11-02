using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>配置時に選ぶ向き。画面上の上下左右（上は CSV の上の行＝y が小さい方）。</summary>
    public enum Facing
    {
        Right,
        Down,
        Left,
        Up,
    }

    /// <summary>
    /// unit_ranges.csv の攻撃範囲（plan 6章）。右向き基準のマス相対座標で持ち、向きに応じて回転する（FR-023）。
    /// dx は前方、dy は下方向（GridPoint の y と同じ向き）。
    /// </summary>
    public sealed class UnitRanges
    {
        public const string FileName = "unit_ranges.csv";

        private readonly Dictionary<string, List<GridPoint>> _offsets = new Dictionary<string, List<GridPoint>>();

        private UnitRanges()
        {
        }

        /// <summary>すべての味方に1マス以上の範囲があり、同じマスの重複と units.csv にない味方がないこと。</summary>
        public static UnitRanges Load(CsvTable table, UnitTable units)
        {
            var ranges = new UnitRanges();
            foreach (var row in table.Rows)
            {
                var unitId = row.Get("unit_id");
                if (!units.Contains(unitId))
                {
                    throw row.Error("unit_id", $"味方 '{unitId}' が units.csv にない");
                }

                if (!ranges._offsets.TryGetValue(unitId, out var offsets))
                {
                    offsets = new List<GridPoint>();
                    ranges._offsets.Add(unitId, offsets);
                }

                var offset = new GridPoint(row.GetInt("dx"), row.GetInt("dy"));
                if (offsets.Contains(offset))
                {
                    throw row.Error("dx", $"味方 '{unitId}' の範囲 {offset} が重複している");
                }

                offsets.Add(offset);
            }

            foreach (var unit in units.All)
            {
                if (!ranges._offsets.ContainsKey(unit.Id))
                {
                    throw new CsvFormatException(new CsvError(table.FileName, 0, 0, $"味方 '{unit.Id}' の攻撃範囲がない"));
                }
            }

            return ranges;
        }

        /// <summary>origin に facing 向きで置いた味方の攻撃範囲（マップ外のマスも含む。CSV の行順）。</summary>
        public IReadOnlyList<GridPoint> CellsFrom(string unitId, GridPoint origin, Facing facing)
        {
            if (!_offsets.TryGetValue(unitId, out var offsets))
            {
                throw new CsvFormatException(new CsvError(FileName, 0, 0, $"味方 '{unitId}' の攻撃範囲がない"));
            }

            var cells = new List<GridPoint>(offsets.Count);
            foreach (var offset in offsets)
            {
                var rotated = Rotate(offset, facing);
                cells.Add(new GridPoint(origin.X + rotated.X, origin.Y + rotated.Y));
            }

            return cells;
        }

        // y が下向きの座標で、右向き基準の (前方, 下) を各向きへ回す。
        private static GridPoint Rotate(GridPoint offset, Facing facing)
        {
            switch (facing)
            {
                case Facing.Down:
                    return new GridPoint(-offset.Y, offset.X);
                case Facing.Left:
                    return new GridPoint(-offset.X, -offset.Y);
                case Facing.Up:
                    return new GridPoint(offset.Y, -offset.X);
                default:
                    return offset;
            }
        }
    }
}
