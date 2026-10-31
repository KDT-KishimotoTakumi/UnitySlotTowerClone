using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("AC-021: 向きによる攻撃範囲")]
    public sealed class UnitRangesTests
    {
        private const string Header = "unit_id,dx,dy\n";

        // 前に1・前に2・前に1かつ右手側に1（右向きでは右手側が下）。
        private const string Archer = "archer,1,0\narcher,2,0\narcher,1,1\n";

        private static readonly UnitTable Units = UnitTable.Load(CsvParser.Parse(UnitTable.FileName,
            "id,max_hp,atk,def,attack_interval,block,tile,cost,cooldown,role,buff_rate\n" +
            "archer,700,350,50,1,0,high,14,15,ranged,1\n"));

        private static readonly GridPoint Origin = new GridPoint(5, 5);

        private static UnitRanges Load(string rows) =>
            UnitRanges.Load(CsvParser.Parse(UnitRanges.FileName, Header + rows), Units);

        private static GridPoint[] Points(params (int x, int y)[] points)
        {
            var result = new GridPoint[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                result[i] = new GridPoint(points[i].x, points[i].y);
            }

            return result;
        }

        [Test]
        public void Facing_Right_Uses_The_Table_Offsets()
        {
            Assert.That(Load(Archer).CellsFrom("archer", Origin, Facing.Right),
                Is.EquivalentTo(Points((6, 5), (7, 5), (6, 6))));
        }

        [Test]
        public void Facing_Down_Turns_Forward_Down_And_Right_Hand_Left()
        {
            Assert.That(Load(Archer).CellsFrom("archer", Origin, Facing.Down),
                Is.EquivalentTo(Points((5, 6), (5, 7), (4, 6))));
        }

        [Test]
        public void Facing_Left_Mirrors_Both_Axes()
        {
            Assert.That(Load(Archer).CellsFrom("archer", Origin, Facing.Left),
                Is.EquivalentTo(Points((4, 5), (3, 5), (4, 4))));
        }

        [Test]
        public void Facing_Up_Turns_Forward_Up_And_Right_Hand_Right()
        {
            Assert.That(Load(Archer).CellsFrom("archer", Origin, Facing.Up),
                Is.EquivalentTo(Points((5, 4), (5, 3), (6, 4))));
        }

        [Test]
        public void Own_Cell_Stays_In_Every_Direction()
        {
            Assert.That(Load("archer,0,0\n").CellsFrom("archer", Origin, Facing.Up), Is.EqualTo(Points((5, 5))));
        }

        [Test]
        public void Unit_Missing_From_Units_Table_Reports_Its_Line()
        {
            var ex = Assert.Throws<CsvFormatException>(() => Load(Archer + "dragon,1,0\n"));
            Assert.That(ex.Error.Line, Is.EqualTo(5));
        }

        [Test]
        public void Duplicate_Offset_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() => Load("archer,1,0\narcher,1,0\n"));
        }

        [Test]
        public void Unit_Without_Range_Is_Reported_By_Name()
        {
            var ex = Assert.Throws<CsvFormatException>(() => Load(""));
            Assert.That(ex.Error.Message, Does.Contain("archer"));
        }

        [Test]
        public void Project_Table_Gives_Every_Unit_A_Range()
        {
            var units = UnitTable.Load(CsvParser.Parse(UnitTable.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, UnitTable.FileName))));

            Assert.DoesNotThrow(() => UnitRanges.Load(CsvParser.Parse(UnitRanges.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, UnitRanges.FileName))), units));
        }
    }
}
