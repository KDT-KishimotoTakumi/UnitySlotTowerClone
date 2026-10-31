using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotPremonitionTests
    {
        private const string ProbabilityHeader = "role,state,setting1,setting2,setting3,setting4,setting5,setting6\n";

        private static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        private static SlotPayouts ProjectPayouts() => SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));

        private static SlotPremonition Load(string rows) =>
            SlotPremonition.Load(CsvParser.Parse(SlotPremonition.FileName, "role,rate\n" + rows), ProjectPayouts());

        /// <summary>重複当選も含め、プロジェクトの役で作った抽選表（確率は使わない）。</summary>
        private static SlotProbabilities Flags() =>
            SlotProbabilities.Load(CsvParser.Parse(SlotProbabilities.FileName, ProbabilityHeader +
                "watermelon,normal,0,0,0,0,0,0\nbig,normal,0,0,0,0,0,0\nbig+watermelon,normal,0,0,0,0,0,0\n"), ProjectPayouts());

        private static SlotFlag Flag(string id)
        {
            var probabilities = Flags();
            if (id == SlotFlag.HazureId)
            {
                return probabilities.Hazure;
            }

            foreach (var flag in probabilities.Flags)
            {
                if (flag.Id == id)
                {
                    return flag;
                }
            }

            throw new AssertionException($"フラグ '{id}' がない");
        }

        [Test]
        [Description("FR-044, AC-042: 成立役の行の発生率で先バレする")]
        public void Rate_Comes_From_The_Role_Row()
        {
            var premonition = Load("watermelon,1/2\n");

            Assert.That(premonition.Rate(Flag("watermelon")), Is.EqualTo(0.5));
        }

        [Test]
        [Description("FR-044, AC-042: 表にない役は先バレしない")]
        public void Unlisted_Role_Never_Premonishes()
        {
            var premonition = Load("watermelon,1\n");

            Assert.That(premonition.Rate(Flag("big")), Is.EqualTo(0));
        }

        [Test]
        [Description("plan 5章: ハズレの行は先バレの「ガセ」の発生率")]
        public void Hazure_Row_Is_The_False_Premonition_Rate()
        {
            var premonition = Load("hazure,1/10\n");

            Assert.That(premonition.Rate(Flag(SlotFlag.HazureId)), Is.EqualTo(0.1).Within(1e-12));
        }

        [Test]
        [Description("plan 5章: 重複当選はその役のうち最も高い発生率で1回だけ抽選する")]
        public void Overlapping_Flag_Uses_The_Highest_Rate_Of_Its_Roles()
        {
            var premonition = Load("watermelon,1/2\nbig,1/8\n");

            Assert.That(premonition.Rate(Flag("big+watermelon")), Is.EqualTo(0.5));
        }

        [Test]
        [Description("FR-044, AC-042: 固定シードの大量試行で、先バレの発生が発生率どおり（5σ以内）")]
        public void Draw_Matches_The_Rate()
        {
            const int draws = 100000;
            const double rate = 0.3;
            var premonition = Load("watermelon,0.3\n");
            var flag = Flag("watermelon");
            var random = new SeededRandom(20261029);

            var count = 0;
            for (var i = 0; i < draws; i++)
            {
                if (premonition.Draw(random, flag))
                {
                    count++;
                }
            }

            Assert.That(count, Is.EqualTo(draws * rate).Within(5 * System.Math.Sqrt(draws * rate * (1 - rate))));
        }

        [Test]
        [Description("plan 5章: プロジェクトの表のガセは初期値 0%")]
        public void Project_Table_Has_No_False_Premonition()
        {
            var premonition = SlotPremonition.Load(ProjectTable(SlotPremonition.FileName), ProjectPayouts());

            Assert.That(premonition.Rate(Flag(SlotFlag.HazureId)), Is.EqualTo(0));
        }

        [Test]
        [Description("FR-044, AC-042: プロジェクトの表は 7揃いのボーナス（BIG・REG）を含むフラグでだけ先バレする")]
        public void Project_Table_Premonishes_Only_Seven_Bonuses()
        {
            var payouts = ProjectPayouts();
            var premonition = SlotPremonition.Load(ProjectTable(SlotPremonition.FileName), payouts);
            var probabilities = SlotProbabilities.Load(ProjectTable(SlotProbabilities.FileName), payouts);
            var wrong = new System.Collections.Generic.List<string>();

            foreach (var flag in probabilities.Flags)
            {
                var isSevenBonus = flag.Has(SlotFlag.BigRole) || flag.Has(SlotFlag.RegRole);
                if (premonition.Rate(flag) > 0 != isSevenBonus)
                {
                    wrong.Add(flag.Id);
                }
            }

            Assert.That(wrong, Is.Empty);
        }

        [Test]
        [Description("FR-050: 払い出し表にない役は読み込みエラー")]
        public void Unknown_Role_Is_A_Load_Error()
        {
            Assert.Throws<CsvFormatException>(() => Load("seven,1/2\n"));
        }

        [Test]
        [Description("FR-050: 発生率が 0〜1 の範囲外なら読み込みエラー")]
        public void Rate_Out_Of_Range_Is_A_Load_Error()
        {
            Assert.Throws<CsvFormatException>(() => Load("watermelon,1.5\n"));
        }
    }
}
