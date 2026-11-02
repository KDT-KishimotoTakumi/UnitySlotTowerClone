using System.Collections.Generic;
using System.Globalization;

namespace UnitySlotTower.Core.Data
{
    public sealed class CsvTable
    {
        private readonly Dictionary<string, int> _columns = new Dictionary<string, int>();

        public string FileName { get; }
        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<CsvRow> Rows { get; }

        internal CsvTable(string fileName, IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
        {
            FileName = fileName;
            Headers = headers;
            Rows = rows;
            for (var i = 0; i < headers.Count; i++)
            {
                _columns[headers[i]] = i;
            }
        }

        /// <summary>列の位置（0 始まり）。ない場合は -1。</summary>
        public int ColumnIndex(string column) => _columns.TryGetValue(column, out var index) ? index : -1;

        public bool HasColumn(string column) => _columns.ContainsKey(column);
    }

    /// <summary>1 行分のデータ。型変換に失敗するとファイル名・行・列つきの <see cref="CsvFormatException"/> を投げる。</summary>
    public sealed class CsvRow
    {
        private readonly IReadOnlyList<string> _cells;

        internal CsvRow(CsvTable table, int line, IReadOnlyList<string> cells)
        {
            Table = table;
            Line = line;
            _cells = cells;
        }

        public CsvTable Table { get; }
        public int Line { get; }

        public string Get(string column) => _cells[IndexOf(column)];

        public int GetInt(string column)
        {
            var text = Get(column);
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            throw Error(column, $"整数ではない: '{text}'");
        }

        public double GetDouble(string column)
        {
            var text = Get(column);
            if (TryParseDouble(text, out var value))
            {
                return value;
            }

            throw Error(column, $"数値ではない: '{text}'");
        }

        public bool GetBool(string column)
        {
            switch (Get(column).ToLowerInvariant())
            {
                case "1":
                case "true":
                    return true;
                case "0":
                case "false":
                    return false;
                default:
                    throw Error(column, $"真偽値（0/1）ではない: '{Get(column)}'");
            }
        }

        /// <summary>確率を読む。<c>1/7.3</c> のような分母表記と小数（<c>0.25</c>）を受け付ける。</summary>
        public double GetProbability(string column)
        {
            var text = Get(column);
            var slash = text.IndexOf('/');
            if (slash < 0)
            {
                return GetDouble(column);
            }

            if (TryParseDouble(text.Substring(0, slash), out var numerator)
                && TryParseDouble(text.Substring(slash + 1), out var denominator))
            {
                if (denominator > 0)
                {
                    return numerator / denominator;
                }

                throw Error(column, $"分母が0以下: '{text}'");
            }

            throw Error(column, $"確率の表記ではない: '{text}'");
        }

        public CsvFormatException Error(string column, string message)
        {
            var index = Table.ColumnIndex(column);
            return new CsvFormatException(new CsvError(Table.FileName, Line, index + 1, $"[{column}] {message}"));
        }

        private int IndexOf(string column)
        {
            var index = Table.ColumnIndex(column);
            if (index < 0)
            {
                throw new CsvFormatException(new CsvError(Table.FileName, 1, 0, $"列 '{column}' がない"));
            }

            return index;
        }

        private static bool TryParseDouble(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
