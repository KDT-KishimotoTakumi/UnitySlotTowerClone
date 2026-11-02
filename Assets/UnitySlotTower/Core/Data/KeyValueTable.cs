using System.Collections.Generic;

namespace UnitySlotTower.Core.Data
{
    /// <summary><c>key,value</c> 形式の表（game_config.csv、slot_rules.csv）。</summary>
    public sealed class KeyValueTable
    {
        public const string KeyColumn = "key";
        public const string ValueColumn = "value";

        private readonly Dictionary<string, CsvRow> _rows = new Dictionary<string, CsvRow>();
        private readonly CsvTable _table;

        public KeyValueTable(CsvTable table)
        {
            _table = table;
            foreach (var row in table.Rows)
            {
                var key = row.Get(KeyColumn);
                if (_rows.ContainsKey(key))
                {
                    throw row.Error(KeyColumn, $"キー '{key}' が重複している");
                }

                _rows.Add(key, row);
            }

            if (!table.HasColumn(ValueColumn))
            {
                throw new CsvFormatException(new CsvError(table.FileName, 1, 0, $"列 '{ValueColumn}' がない"));
            }
        }

        public CsvRow Row(string key)
        {
            if (_rows.TryGetValue(key, out var row))
            {
                return row;
            }

            throw new CsvFormatException(new CsvError(_table.FileName, 0, 0, $"キー '{key}' がない"));
        }

        public bool Contains(string key) => _rows.ContainsKey(key);

        public int GetInt(string key) => Row(key).GetInt(ValueColumn);
        public double GetDouble(string key) => Row(key).GetDouble(ValueColumn);
    }
}
