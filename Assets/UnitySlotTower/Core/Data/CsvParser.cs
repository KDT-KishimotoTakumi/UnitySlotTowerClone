using System.Collections.Generic;
using System.Text;

namespace UnitySlotTower.Core.Data
{
    /// <summary>1 レコード分のセルと、そのレコードが始まる物理行（1 始まり）。</summary>
    public readonly struct CsvRecord
    {
        public CsvRecord(int line, IReadOnlyList<string> cells)
        {
            Line = line;
            Cells = cells;
        }

        public int Line { get; }
        public IReadOnlyList<string> Cells { get; }
    }

    /// <summary>
    /// Excel の「CSV UTF-8」形式を読む。先頭 BOM、引用符（"" のエスケープ、セル内の改行・コンマ）、
    /// 空行の読み飛ばしに対応する。引用符なしのセルは前後の空白を取り除く。
    /// </summary>
    public static class CsvParser
    {
        public static CsvTable Parse(string fileName, string text)
        {
            var records = ParseRecords(fileName, text);
            if (records.Count == 0)
            {
                throw new CsvFormatException(new CsvError(fileName, 1, 0, "見出し行がない"));
            }

            var header = records[0];
            var rows = new List<CsvRow>(records.Count - 1);
            var table = new CsvTable(fileName, header.Cells, rows);
            for (var i = 1; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Cells.Count != header.Cells.Count)
                {
                    throw new CsvFormatException(new CsvError(fileName, record.Line, 0,
                        $"セル数 {record.Cells.Count} が見出しの列数 {header.Cells.Count} と一致しない"));
                }

                rows.Add(new CsvRow(table, record.Line, record.Cells));
            }

            return table;
        }

        /// <summary>見出しを解釈せずにレコードだけを返す（マップのグリッドなど）。</summary>
        public static List<CsvRecord> ParseRecords(string fileName, string text)
        {
            var records = new List<CsvRecord>();
            if (string.IsNullOrEmpty(text))
            {
                return records;
            }

            var index = text[0] == '﻿' ? 1 : 0;
            var line = 1;
            var cells = new List<string>();
            var cell = new StringBuilder();
            var recordLine = line;
            var cellQuoted = false;

            while (index <= text.Length)
            {
                var atEnd = index == text.Length;
                var c = atEnd ? '\n' : text[index];

                if (c == '"' && cell.Length == 0 && !cellQuoted)
                {
                    var quoteLine = line;
                    index++;
                    while (true)
                    {
                        if (index >= text.Length)
                        {
                            throw new CsvFormatException(new CsvError(fileName, quoteLine, cells.Count + 1,
                                "引用符が閉じていない"));
                        }

                        var q = text[index];
                        if (q == '"')
                        {
                            if (index + 1 < text.Length && text[index + 1] == '"')
                            {
                                cell.Append('"');
                                index += 2;
                                continue;
                            }

                            index++;
                            break;
                        }

                        if (q == '\n')
                        {
                            line++;
                        }

                        if (q != '\r')
                        {
                            cell.Append(q);
                        }

                        index++;
                    }

                    cellQuoted = true;
                    continue;
                }

                if (c == ',')
                {
                    cells.Add(Finish(cell, cellQuoted));
                    cellQuoted = false;
                    index++;
                    continue;
                }

                if (c == '\r')
                {
                    index++;
                    continue;
                }

                if (c == '\n')
                {
                    cells.Add(Finish(cell, cellQuoted));
                    if (!IsEmptyRecord(cells, cellQuoted))
                    {
                        records.Add(new CsvRecord(recordLine, cells.ToArray()));
                    }

                    cells.Clear();
                    cellQuoted = false;
                    line++;
                    recordLine = line;
                    index++;
                    continue;
                }

                if (!cellQuoted)
                {
                    cell.Append(c);
                }

                index++;
            }

            return records;
        }

        private static string Finish(StringBuilder cell, bool quoted)
        {
            var value = quoted ? cell.ToString() : cell.ToString().Trim();
            cell.Clear();
            return value;
        }

        private static bool IsEmptyRecord(List<string> cells, bool lastQuoted) =>
            cells.Count == 1 && cells[0].Length == 0 && !lastQuoted;
    }
}
