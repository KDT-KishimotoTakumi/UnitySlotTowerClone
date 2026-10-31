using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotLotteryTests
    {
        private const string Header = "role,state,setting1,setting2,setting3,setting4,setting5,setting6\n";

        private static readonly SlotState[] States = { SlotState.Normal, SlotState.Kakuhen, SlotState.Bonus };

        private static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        private static SlotPayouts ProjectPayouts() => SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));

        private static SlotProbabilities ProjectProbabilities() =>
            SlotProbabilities.Load(ProjectTable(SlotProbabilities.FileName), ProjectPayouts());

        private static SlotProbabilities Load(string rows) =>
            SlotProbabilities.Load(CsvParser.Parse(SlotProbabilities.FileName, Header + rows), ProjectPayouts());

        [Test]
        [Description("FR-045: 固定シードの大量試行で、全状態×全設定の成立率が表どおり（5σ以内）")]
        public void Project_Draws_Match_The_Table_For_Every_State_And_Setting()
        {
            const int draws = 100000;
            var probabilities = ProjectProbabilities();
            var random = new SeededRandom(20261029);
            var failures = new List<string>();

            foreach (var state in States)
            {
                for (var setting = 1; setting <= SlotProbabilities.SettingCount; setting++)
                {
                    var counts = probabilities.Flags.ToDictionary(flag => flag, _ => 0);
                    for (var i = 0; i < draws; i++)
                    {
                        counts[probabilities.Draw(random, state, setting)]++;
                    }

                    foreach (var pair in counts)
                    {
                        var p = probabilities.Probability(pair.Key, state, setting);
                        var sigma = Math.Sqrt(draws * p * (1 - p));
                        if (Math.Abs(pair.Value - draws * p) > 5 * sigma + 1e-9)
                        {
                            failures.Add($"{state}/setting{setting}/{pair.Key.Id}: {pair.Value} vs {draws * p:0.0}");
                        }
                    }
                }
            }

            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Same_Seed_Gives_The_Same_Flags()
        {
            var probabilities = ProjectProbabilities();
            var a = new SeededRandom(7);
            var b = new SeededRandom(7);

            var first = Enumerable.Range(0, 1000).Select(_ => probabilities.Draw(a, SlotState.Normal, 3).Id).ToList();
            var second = Enumerable.Range(0, 1000).Select(_ => probabilities.Draw(b, SlotState.Normal, 3).Id).ToList();

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        [Description("plan 5章: 表にない残りの確率はハズレ")]
        public void Remaining_Probability_Is_Hazure()
        {
            var probabilities = Load("bell,normal,1/4,1/4,1/4,1/4,1/4,1/4\n");

            Assert.That(probabilities.Probability(probabilities.Hazure, SlotState.Normal, 1), Is.EqualTo(0.75).Within(1e-12));
            Assert.That(probabilities.Probability(probabilities.Hazure, SlotState.Kakuhen, 1), Is.EqualTo(1.0));
            Assert.That(probabilities.Hazure.Roles, Is.Empty);
        }

        [Test]
        [Description("plan 5章: BIG・REG は中段チェリー・スイカとの重複当選を表で定義できる")]
        public void Joined_Roles_Make_One_Overlapping_Flag()
        {
            var probabilities = Load("big+center_cherry,normal,1,1,1,1,1,1\n");

            var flag = probabilities.Draw(new SeededRandom(1), SlotState.Normal, 1);

            Assert.That((flag.Has("big"), flag.Has("center_cherry"), flag.Has("bell")), Is.EqualTo((true, true, false)));
        }

        [Test]
        [Description("FR-045: 設定によって確率が変わる")]
        public void Setting_Selects_Its_Column()
        {
            var probabilities = Load("bell,normal,0,0,0,0,0,1\n");

            Assert.That((probabilities.Draw(new SeededRandom(1), SlotState.Normal, 1).Id,
                    probabilities.Draw(new SeededRandom(1), SlotState.Normal, 6).Id),
                Is.EqualTo((SlotFlag.HazureId, "bell")));
        }

        [TestCase(0)]
        [TestCase(7)]
        public void Setting_Out_Of_Range_Throws(int setting)
        {
            var probabilities = Load("bell,normal,1/4,1/4,1/4,1/4,1/4,1/4\n");

            Assert.Throws<ArgumentOutOfRangeException>(() => probabilities.Draw(new SeededRandom(1), SlotState.Normal, setting));
        }

        [TestCase("dragon,normal,1,1,1,1,1,1\n")]
        [TestCase("big+,normal,1,1,1,1,1,1\n")]
        [TestCase("big+big,normal,1,1,1,1,1,1\n")]
        public void Bad_Role_Is_Reported_With_Its_Line(string row)
        {
            var ex = Assert.Throws<CsvFormatException>(() => Load(row));

            Assert.That((ex.Error.Line, ex.Error.Column), Is.EqualTo((2, 1)));
        }

        [Test]
        [Description("plan 5章: ボーナス持ち越し中は、重複当選からボーナス役を外した小役だけが成立する")]
        public void Without_Bonus_Leaves_Only_The_Small_Role(
            [Values("big+center_cherry:center_cherry", "big+watermelon:watermelon", "reg+watermelon:watermelon", "big:hazure", "bell:bell")] string pair)
        {
            var probabilities = ProjectProbabilities();
            var ids = pair.Split(':');

            var flag = probabilities.WithoutBonus(probabilities.Flags.Single(candidate => candidate.Id == ids[0]));

            Assert.That(flag.Id, Is.EqualTo(ids[1]));
        }

        [Test]
        public void Duplicate_Role_And_State_Is_Reported()
        {
            var ex = Assert.Throws<CsvFormatException>(() =>
                Load("bell,normal,0,0,0,0,0,0\nreplay,normal,0,0,0,0,0,0\nbell,normal,0,0,0,0,0,0\n"));

            Assert.That(ex.Error.Line, Is.EqualTo(4));
        }

        [Test]
        public void Sum_Above_One_Is_Reported()
        {
            var ex = Assert.Throws<CsvFormatException>(() =>
                Load("bell,normal,1/2,0,0,0,0,0\nreplay,normal,0.6,0,0,0,0,0\n"));

            Assert.That(ex.Error.Message, Does.Contain("setting1"));
        }

        [Test]
        [Description("FR-043: 確変中はボーナス確率が上がる")]
        public void Project_Kakuhen_Raises_Bonus_Probability_On_Every_Setting()
        {
            var probabilities = ProjectProbabilities();

            var notRaised = Enumerable.Range(1, SlotProbabilities.SettingCount)
                .Where(setting => BonusProbability(probabilities, SlotState.Kakuhen, setting)
                    <= BonusProbability(probabilities, SlotState.Normal, setting));
            Assert.That(notRaised, Is.Empty);
        }

        [Test]
        [Description("plan 5章: ボーナス中は専用の表で小役が高確率、ボーナスは引かない")]
        public void Project_Bonus_State_Has_High_Small_Role_Rate_And_No_Bonus()
        {
            var probabilities = ProjectProbabilities();

            for (var setting = 1; setting <= SlotProbabilities.SettingCount; setting++)
            {
                Assert.That(BonusProbability(probabilities, SlotState.Bonus, setting), Is.Zero);
                Assert.That(probabilities.Probability(probabilities.Hazure, SlotState.Bonus, setting), Is.LessThan(0.2));
            }
        }

        private static double BonusProbability(SlotProbabilities probabilities, SlotState state, int setting) =>
            probabilities.Flags.Where(flag => flag.Has("big") || flag.Has("reg"))
                .Sum(flag => probabilities.Probability(flag, state, setting));
    }
}
