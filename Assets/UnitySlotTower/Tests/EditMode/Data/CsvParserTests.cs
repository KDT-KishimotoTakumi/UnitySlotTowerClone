using NUnit.Framework;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Tests.EditMode.Data
{
    public sealed class CsvParserTests
    {
        [Test]
        public void Header_And_Rows_Are_Read_By_Column_Name()
        {
            var table = CsvParser.Parse("units.csv", "id,cost\ntank,15\nsword,12\n");

            Assert.That(table.Rows[1].Get("cost"), Is.EqualTo("12"));
        }

        [Test]
        public void Utf8_Bom_Is_Ignored_In_Header()
        {
            var table = CsvParser.Parse("units.csv", "﻿id,cost\ntank,15\n");

            Assert.That(table.Rows[0].Get("id"), Is.EqualTo("tank"));
        }

        [Test]
        public void Quoted_Field_Keeps_Commas_Quotes_And_Newlines()
        {
            var table = CsvParser.Parse("a.csv", "id,note\nx,\"a,\"\"b\"\"\nc\"\n");

            Assert.That(table.Rows[0].Get("note"), Is.EqualTo("a,\"b\"\nc"));
        }

        [Test]
        public void Empty_Lines_Are_Skipped()
        {
            var table = CsvParser.Parse("a.csv", "id\r\n\r\nx\r\n  \r\ny\r\n\r\n");

            Assert.That(table.Rows.Count, Is.EqualTo(2));
        }

        [Test]
        public void Row_Keeps_Its_Physical_Line_Number()
        {
            var table = CsvParser.Parse("a.csv", "id\n\nx\n\"multi\nline\"\nz\n");

            Assert.That(table.Rows[2].Line, Is.EqualTo(6));
        }

        [Test]
        public void Probability_Accepts_Denominator_Notation()
        {
            var table = CsvParser.Parse("p.csv", "role,p\nbell,1/8\n");

            Assert.That(table.Rows[0].GetProbability("p"), Is.EqualTo(0.125).Within(1e-12));
        }

        [Test]
        public void Probability_Accepts_Decimal_Denominator()
        {
            var table = CsvParser.Parse("p.csv", "role,p\nbell,1/7.3\n");

            Assert.That(table.Rows[0].GetProbability("p"), Is.EqualTo(1.0 / 7.3).Within(1e-12));
        }

        [Test]
        public void Probability_Accepts_Plain_Decimal()
        {
            var table = CsvParser.Parse("p.csv", "role,p\nbell,0.25\n");

            Assert.That(table.Rows[0].GetProbability("p"), Is.EqualTo(0.25).Within(1e-12));
        }

        [Test]
        public void Invalid_Number_Reports_File_Line_And_Column()
        {
            var table = CsvParser.Parse("units.csv", "id,cost\ntank,15\nsword,abc\n");

            var ex = Assert.Throws<CsvFormatException>(() => table.Rows[1].GetInt("cost"));
            Assert.That((ex.Error.File, ex.Error.Line, ex.Error.Column), Is.EqualTo(("units.csv", 3, 2)));
        }

        [Test]
        public void Zero_Denominator_Is_Rejected()
        {
            var table = CsvParser.Parse("p.csv", "role,p\nbell,1/0\n");

            Assert.Throws<CsvFormatException>(() => table.Rows[0].GetProbability("p"));
        }

        [Test]
        public void Unterminated_Quote_Reports_Its_Start_Line()
        {
            var ex = Assert.Throws<CsvFormatException>(() => CsvParser.Parse("a.csv", "id\nx\n\"open\n"));

            Assert.That(ex.Error.Line, Is.EqualTo(3));
        }

        [Test]
        public void Cell_Count_Mismatch_Reports_The_Row()
        {
            var ex = Assert.Throws<CsvFormatException>(() => CsvParser.Parse("a.csv", "id,cost\ntank,15,9\n"));

            Assert.That((ex.Error.File, ex.Error.Line), Is.EqualTo(("a.csv", 2)));
        }

        [Test]
        public void Missing_Column_Is_Reported_With_File_Name()
        {
            var table = CsvParser.Parse("units.csv", "id\ntank\n");

            var ex = Assert.Throws<CsvFormatException>(() => table.Rows[0].Get("cost"));
            Assert.That(ex.Error.File, Is.EqualTo("units.csv"));
        }

        [Test]
        public void Error_Text_Contains_File_Line_And_Column()
        {
            var error = new CsvError("units.csv", 3, 2, "bad");

            Assert.That(error.ToString(), Does.StartWith("units.csv:3:2"));
        }
    }
}
