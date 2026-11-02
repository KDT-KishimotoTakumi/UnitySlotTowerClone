using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UnitySlotTower.Core.Data
{
    /// <summary>検証で見つかった問題の一覧。検証は最初の問題で止めず、すべて集める。</summary>
    public sealed class ValidationReport
    {
        public List<CsvError> Errors { get; } = new List<CsvError>();
        public bool IsValid => Errors.Count == 0;

        public void Add(CsvError error) => Errors.Add(error);

        public override string ToString()
        {
            var text = new StringBuilder();
            foreach (var error in Errors)
            {
                text.AppendLine(error.ToString());
            }

            return text.ToString();
        }
    }

    public enum ColumnType
    {
        Text,
        Id,
        Int,
        Float,
        Probability,
        Bool,
        Enum,
    }

    /// <summary>1 列の型と範囲。範囲は両端を含む。</summary>
    public sealed class ColumnRule
    {
        private ColumnRule(string name, ColumnType type, double min = double.MinValue, double max = double.MaxValue,
            string[] values = null)
        {
            Name = name;
            Type = type;
            Min = min;
            Max = max;
            Values = values ?? Array.Empty<string>();
        }

        public string Name { get; }
        public ColumnType Type { get; }
        public double Min { get; }
        public double Max { get; }
        public IReadOnlyList<string> Values { get; }

        public static ColumnRule Text(string name) => new ColumnRule(name, ColumnType.Text);
        public static ColumnRule Id(string name) => new ColumnRule(name, ColumnType.Id);
        public static ColumnRule Int(string name, int min, int max) => new ColumnRule(name, ColumnType.Int, min, max);
        public static ColumnRule Float(string name, double min, double max) => new ColumnRule(name, ColumnType.Float, min, max);
        public static ColumnRule Probability(string name) => new ColumnRule(name, ColumnType.Probability, 0, 1);
        public static ColumnRule Bool(string name) => new ColumnRule(name, ColumnType.Bool);
        public static ColumnRule Enum(string name, params string[] values) => new ColumnRule(name, ColumnType.Enum, values: values);
    }

    /// <summary>表の列の規則と、重複してはいけない列の組（空なら重複検査なし）。</summary>
    public sealed class TableSchema
    {
        public TableSchema(string fileName, string[] uniqueKey, params ColumnRule[] columns)
        {
            FileName = fileName;
            UniqueKey = uniqueKey ?? Array.Empty<string>();
            Columns = columns;
        }

        public string FileName { get; }
        public IReadOnlyList<string> UniqueKey { get; }
        public IReadOnlyList<ColumnRule> Columns { get; }
    }

    public static class TableValidator
    {
        public static void ValidateColumns(CsvTable table, TableSchema schema, ValidationReport report)
        {
            var missing = false;
            foreach (var rule in schema.Columns)
            {
                if (!table.HasColumn(rule.Name))
                {
                    report.Add(new CsvError(table.FileName, 1, 0, $"列 '{rule.Name}' がない"));
                    missing = true;
                }
            }

            if (missing)
            {
                return;
            }

            var keys = new Dictionary<string, int>();
            foreach (var row in table.Rows)
            {
                foreach (var rule in schema.Columns)
                {
                    ValidateCell(row, rule, report);
                }

                if (schema.UniqueKey.Count > 0)
                {
                    var key = KeyOf(row, schema.UniqueKey);
                    if (keys.TryGetValue(key, out var firstLine))
                    {
                        report.Add(row.Error(schema.UniqueKey[0],
                            $"({string.Join(",", schema.UniqueKey)}) = ({key}) が {firstLine} 行目と重複している").Error);
                    }
                    else
                    {
                        keys.Add(key, row.Line);
                    }
                }
            }
        }

        /// <summary>key,value 表の各キーを、そのキーの規則で検証する。不足と未知のキーも報告する。</summary>
        public static void ValidateKeyValues(CsvTable table, IReadOnlyList<ColumnRule> keys, ValidationReport report)
        {
            KeyValueTable values;
            try
            {
                values = new KeyValueTable(table);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return;
            }

            var known = new HashSet<string>();
            foreach (var rule in keys)
            {
                known.Add(rule.Name);
                if (!values.Contains(rule.Name))
                {
                    report.Add(new CsvError(table.FileName, 0, 0, $"キー '{rule.Name}' がない"));
                    continue;
                }

                var row = values.Row(rule.Name);
                ValidateValue(row, KeyValueTable.ValueColumn, rule, report);
            }

            foreach (var row in table.Rows)
            {
                if (!known.Contains(row.Get(KeyValueTable.KeyColumn)))
                {
                    report.Add(row.Error(KeyValueTable.KeyColumn, $"未知のキー '{row.Get(KeyValueTable.KeyColumn)}'").Error);
                }
            }
        }

        public static void ValidateReference(CsvTable from, string column, CsvTable to, string toColumn,
            ValidationReport report, params string[] extraAllowed) =>
            ValidateListReference(from, column, null, to, toColumn, report, extraAllowed);

        /// <summary>column の値を separator で区切った各 ID が to の toColumn にあることを確かめる（separator が null なら区切らない）。</summary>
        public static void ValidateListReference(CsvTable from, string column, char? separator, CsvTable to,
            string toColumn, ValidationReport report, params string[] extraAllowed)
        {
            if (!from.HasColumn(column) || !to.HasColumn(toColumn))
            {
                return;
            }

            var ids = new HashSet<string>(extraAllowed);
            foreach (var row in to.Rows)
            {
                ids.Add(row.Get(toColumn));
            }

            foreach (var row in from.Rows)
            {
                var value = row.Get(column);
                foreach (var id in separator.HasValue ? value.Split(separator.Value) : new[] { value })
                {
                    if (!ids.Contains(id))
                    {
                        report.Add(row.Error(column, $"'{id}' が {to.FileName} の {toColumn} にない").Error);
                    }
                }
            }
        }

        /// <summary>groupColumn の値ごと・valueColumns の列ごとに、確率の合計が 1 を超えないことを確かめる。</summary>
        public static void ValidateProbabilitySum(CsvTable table, string groupColumn, string[] valueColumns,
            ValidationReport report)
        {
            if (!table.HasColumn(groupColumn))
            {
                return;
            }

            var sums = new Dictionary<(string Group, string Column), double>();
            foreach (var row in table.Rows)
            {
                var group = row.Get(groupColumn);
                foreach (var column in valueColumns)
                {
                    if (!table.HasColumn(column) || !TryRead(() => row.GetProbability(column), out var p))
                    {
                        continue;
                    }

                    sums.TryGetValue((group, column), out var sum);
                    sums[(group, column)] = sum + p;
                }
            }

            foreach (var pair in sums)
            {
                // 分母表記の丸め誤差は許す。
                if (pair.Value > 1 + 1e-9)
                {
                    report.Add(new CsvError(table.FileName, 0, table.ColumnIndex(pair.Key.Column) + 1,
                        $"{groupColumn}={pair.Key.Group} の {pair.Key.Column} の確率の合計 {pair.Value.ToString("0.####", CultureInfo.InvariantCulture)} が 1 を超えている"));
                }
            }
        }

        /// <summary>map_id と path_id ごとに step を 1 から順に並べ、隣り合うマスが上下左右につながっていることを確かめる。</summary>
        public static void ValidatePathContinuity(CsvTable table, ValidationReport report)
        {
            var paths = new Dictionary<(string Map, string Path), List<(int Step, int X, int Y, CsvRow Row)>>();
            foreach (var row in table.Rows)
            {
                if (!TryRead(() => row.GetInt("step"), out var step)
                    || !TryRead(() => row.GetInt("x"), out var x)
                    || !TryRead(() => row.GetInt("y"), out var y))
                {
                    continue;
                }

                var key = (row.Get("map_id"), row.Get("path_id"));
                if (!paths.TryGetValue(key, out var cells))
                {
                    cells = new List<(int, int, int, CsvRow)>();
                    paths.Add(key, cells);
                }

                cells.Add((step, x, y, row));
            }

            foreach (var cells in paths.Values)
            {
                cells.Sort((a, b) => a.Step.CompareTo(b.Step));
                for (var i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    if (cell.Step != i + 1)
                    {
                        report.Add(cell.Row.Error("step", $"step は 1 から欠番なく並べる（{i + 1} のはずが {cell.Step}）").Error);
                        break;
                    }

                    if (i > 0)
                    {
                        var previous = cells[i - 1];
                        if (Math.Abs(cell.X - previous.X) + Math.Abs(cell.Y - previous.Y) != 1)
                        {
                            report.Add(cell.Row.Error("x",
                                $"({previous.X},{previous.Y}) から ({cell.X},{cell.Y}) へ上下左右につながっていない").Error);
                        }
                    }
                }
            }
        }

        private static void ValidateCell(CsvRow row, ColumnRule rule, ValidationReport report) =>
            ValidateValue(row, rule.Name, rule, report);

        private static void ValidateValue(CsvRow row, string column, ColumnRule rule, ValidationReport report)
        {
            try
            {
                double value;
                switch (rule.Type)
                {
                    case ColumnType.Text:
                        return;
                    case ColumnType.Id:
                        if (row.Get(column).Length == 0)
                        {
                            throw row.Error(column, "ID が空");
                        }

                        return;
                    case ColumnType.Bool:
                        row.GetBool(column);
                        return;
                    case ColumnType.Enum:
                        var text = row.Get(column);
                        foreach (var allowed in rule.Values)
                        {
                            if (allowed == text)
                            {
                                return;
                            }
                        }

                        throw row.Error(column, $"'{text}' は {string.Join("/", rule.Values)} のどれでもない");
                    case ColumnType.Int:
                        value = row.GetInt(column);
                        break;
                    case ColumnType.Probability:
                        value = row.GetProbability(column);
                        break;
                    default:
                        value = row.GetDouble(column);
                        break;
                }

                if (value < rule.Min || value > rule.Max)
                {
                    throw row.Error(column, $"{value.ToString(CultureInfo.InvariantCulture)} が範囲 " +
                        $"{rule.Min.ToString(CultureInfo.InvariantCulture)}〜{rule.Max.ToString(CultureInfo.InvariantCulture)} の外");
                }
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
            }
        }

        private static string KeyOf(CsvRow row, IReadOnlyList<string> columns)
        {
            var parts = new string[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                parts[i] = row.Get(columns[i]);
            }

            return string.Join(",", parts);
        }

        private static bool TryRead<T>(Func<T> read, out T value)
        {
            try
            {
                value = read();
                return true;
            }
            catch (CsvFormatException)
            {
                value = default;
                return false;
            }
        }
    }
}
