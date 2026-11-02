using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// slot_reels.csv の図柄配列（3リール×21コマ）。コマ i はコマ i+1 の上に並ぶので、
    /// 停止位置 s のとき上段 s-1・中段 s・下段 s+1 が見える（端は循環）。
    /// </summary>
    public sealed class SlotReels
    {
        public const string FileName = "slot_reels.csv";
        public const int ReelCount = 3;
        public const int StopCount = 21;

        private readonly SlotSymbol[,] _symbols = new SlotSymbol[ReelCount, StopCount];

        private SlotReels()
        {
        }

        /// <summary>リール（0 始まり、左から）のコマ（0〜20、範囲外は循環）の図柄。</summary>
        public SlotSymbol At(int reel, int index) => _symbols[reel, Wrap(index)];

        public static int Wrap(int index) => ((index % StopCount) + StopCount) % StopCount;

        public static SlotReels Load(CsvTable table)
        {
            var reels = new SlotReels();
            var seen = new bool[StopCount];
            foreach (var row in table.Rows)
            {
                var index = row.GetInt("index");
                if (index < 0 || index >= StopCount)
                {
                    throw row.Error("index", $"0〜{StopCount - 1} の範囲外: {index}");
                }

                if (seen[index])
                {
                    throw row.Error("index", $"コマ {index} が重複している");
                }

                seen[index] = true;
                for (var reel = 0; reel < ReelCount; reel++)
                {
                    reels._symbols[reel, index] = SlotSymbols.Parse(row, $"reel{reel + 1}");
                }
            }

            for (var index = 0; index < StopCount; index++)
            {
                if (!seen[index])
                {
                    throw new CsvFormatException(new CsvError(table.FileName, 0, 0, $"コマ {index} がない"));
                }
            }

            return reels;
        }
    }
}
