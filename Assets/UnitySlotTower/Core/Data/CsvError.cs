using System;

namespace UnitySlotTower.Core.Data
{
    /// <summary>CSV の問題箇所。Line と Column は 1 始まりで、0 は「特定なし」。</summary>
    public sealed class CsvError
    {
        public CsvError(string file, int line, int column, string message)
        {
            File = file;
            Line = line;
            Column = column;
            Message = message;
        }

        public string File { get; }
        public int Line { get; }
        public int Column { get; }
        public string Message { get; }

        public override string ToString() => $"{File}:{Line}:{Column} {Message}";
    }

    public sealed class CsvFormatException : Exception
    {
        public CsvFormatException(CsvError error) : base(error.ToString())
        {
            Error = error;
        }

        public CsvError Error { get; }
    }
}
