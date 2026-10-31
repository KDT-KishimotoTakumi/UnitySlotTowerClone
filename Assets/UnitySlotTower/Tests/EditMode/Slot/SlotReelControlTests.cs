using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotReelControlTests
    {
        private static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        private static SlotReels ProjectReels() => SlotReels.Load(ProjectTable(SlotReels.FileName));

        private static SlotPayouts ProjectPayouts() => SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));

        // 期待値は slot_reels.csv から手で読んだ出目（停止位置 s で上段 s-1・中段 s・下段 s+1）。

        private static readonly int[][] Orders =
        {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
        };

        private static SlotProbabilities ProjectProbabilities() =>
            SlotProbabilities.Load(ProjectTable(SlotProbabilities.FileName), ProjectPayouts());

        private static SlotReelControl ProjectControl() =>
            new SlotReelControl(ProjectReels(), ProjectPayouts(), SlotRules.Load(ProjectTable(SlotRules.FileName)).MaxSlide);

        private static SlotFlag Flag(string id) => ProjectProbabilities().Flags.Single(flag => flag.Id == id);

        /// <summary>全押し順×全押し位置（21^3）で1ゲームずつ止め、押し位置・停止位置・揃った役を渡す。</summary>
        private static void ForEachGame(SlotReelControl control, SlotFlag flag, string carriedBonus,
            Action<string, int[], int[], List<string>> visit)
        {
            var reels = ProjectReels();
            var payouts = ProjectPayouts();
            var presses = new int[SlotReels.ReelCount];
            var stops = new int[SlotReels.ReelCount];
            foreach (var order in Orders)
            {
                for (var a = 0; a < SlotReels.StopCount; a++)
                for (var b = 0; b < SlotReels.StopCount; b++)
                for (var c = 0; c < SlotReels.StopCount; c++)
                {
                    presses[order[0]] = a;
                    presses[order[1]] = b;
                    presses[order[2]] = c;
                    var spin = control.Start(flag, carriedBonus);
                    foreach (var reel in order)
                    {
                        stops[reel] = spin.Stop(reel, presses[reel]);
                    }

                    var game = $"{flag.Id}+{carriedBonus ?? "-"} order={string.Join("", order)} press={string.Join(",", presses)} stop={string.Join(",", stops)}";
                    visit(game, presses, stops, SlotLines.Aligned(reels, payouts, stops));
                }
            }
        }

        private static void AddFirst(List<string> failures, string message)
        {
            if (failures.Count < 10)
            {
                failures.Add(message);
            }
        }

        [Test]
        [Description("C-5: 停止位置は押した位置から下向きに最大4コマ（p, p-1, …, p-4）")]
        public void Every_Stop_Slides_At_Most_Four_Frames()
        {
            var control = ProjectControl();
            var failures = new List<string>();
            foreach (var flag in ProjectProbabilities().Flags)
            {
                ForEachGame(control, flag, null, (game, presses, stops, aligned) =>
                {
                    for (var reel = 0; reel < SlotReels.ReelCount; reel++)
                    {
                        var slide = SlotReels.Wrap(presses[reel] - stops[reel]);
                        if (slide > 4)
                        {
                            AddFirst(failures, $"{game} reel{reel + 1} slide={slide}");
                        }
                    }
                });
            }

            Assert.That(failures, Is.Empty);
        }

        [Test]
        [Description("AC-041: 全フラグ（＋ボーナス持ち越し）×全押し順×全押し位置で、成立していない役は揃わない")]
        public void Unflagged_Roles_Never_Align()
        {
            var control = ProjectControl();
            var probabilities = ProjectProbabilities();
            var cases = probabilities.Flags.Select(flag => (Flag: flag, Carried: (string)null))
                .Concat(new[] { (probabilities.Hazure, "big"), (probabilities.Hazure, "reg") });
            var failures = new List<string>();
            foreach (var (flag, carried) in cases)
            {
                ForEachGame(control, flag, carried, (game, presses, stops, aligned) =>
                {
                    if (aligned.Any(role => !flag.Has(role) && role != carried))
                    {
                        AddFirst(failures, $"{game} aligned={string.Join(",", aligned)}");
                    }
                });
            }

            Assert.That(failures, Is.Empty);
        }

        [Test]
        [Description("AC-041 / plan 5章: リプレイ・ベル・ハズレはどの押し順・押し位置でも成立役どおりの出目になる")]
        public void Replay_Bell_And_Hazure_Always_Show_Exactly_The_Flag(
            [Values("hazure", "replay", "bell")] string flagId)
        {
            var flag = Flag(flagId);
            var failures = new List<string>();
            ForEachGame(ProjectControl(), flag, null, (game, presses, stops, aligned) =>
            {
                if (!aligned.SequenceEqual(flag.Roles))
                {
                    AddFirst(failures, $"{game} aligned={string.Join(",", aligned)}");
                }
            });

            Assert.That(failures, Is.Empty);
        }

        [Test]
        [Description("FR-040: レア役・ボーナスは、押した位置のままでは揃わない目押しでも滑りで引き込まれる")]
        public void Flagged_Roles_Are_Pulled_In_By_Sliding(
            [Values("watermelon", "cherry", "center_cherry", "big", "reg")] string flagId)
        {
            var reels = ProjectReels();
            var payouts = ProjectPayouts();
            var pulledIn = 0;
            ForEachGame(ProjectControl(), Flag(flagId), null, (game, presses, stops, aligned) =>
            {
                if (aligned.Contains(flagId) && !SlotLines.Aligned(reels, payouts, presses).Contains(flagId))
                {
                    pulledIn++;
                }
            });

            Assert.That(pulledIn, Is.GreaterThan(0));
        }

        [Test]
        [Description("FR-041: 持ち越し中の BIG は、ハズレのゲームでも滑りで引き込まれる")]
        public void Carried_Bonus_Is_Pulled_In_On_A_Hazure_Game()
        {
            var reels = ProjectReels();
            var payouts = ProjectPayouts();
            var pulledIn = 0;
            ForEachGame(ProjectControl(), ProjectProbabilities().Hazure, "big", (game, presses, stops, aligned) =>
            {
                if (aligned.Contains("big") && !SlotLines.Aligned(reels, payouts, presses).Contains("big"))
                {
                    pulledIn++;
                }
            });

            Assert.That(pulledIn, Is.GreaterThan(0));
        }

        private static int BonusAlignedGames(SlotFlag flag, string carried, string bonus)
        {
            var count = 0;
            ForEachGame(ProjectControl(), flag, carried, (game, presses, stops, aligned) =>
            {
                if (aligned.Contains(bonus))
                {
                    count++;
                }
            });

            return count;
        }

        [Test]
        [Description("plan 5章: 持ち越し中のボーナスは小役より優先して引き込む（レア役が成立しても揃う回数はハズレのとき以上）")]
        public void Carried_Bonus_Has_Priority_Over_Rare_Roles(
            [Values("watermelon", "cherry", "center_cherry")] string flagId,
            [Values("big", "reg")] string bonus)
        {
            var withRareRole = BonusAlignedGames(Flag(flagId), bonus, bonus);

            Assert.That(withRareRole, Is.GreaterThanOrEqualTo(BonusAlignedGames(ProjectProbabilities().Hazure, bonus, bonus)));
        }

        [Test]
        [Description("plan 5章: ボーナスとレア役の重複当選でも、ボーナスはボーナス単独のとき以上に揃う")]
        public void Bonus_Has_Priority_Over_A_Rare_Role_Drawn_With_It(
            [Values("big+center_cherry", "big+watermelon", "reg+watermelon")] string flagId)
        {
            var bonus = flagId.Split(SlotFlag.RoleSeparator)[0];
            var together = BonusAlignedGames(Flag(flagId), null, bonus);

            Assert.That(together, Is.GreaterThanOrEqualTo(BonusAlignedGames(Flag(bonus), null, bonus)));
        }

        [Test]
        [Description("plan 5章: 左リール中段のチェリーは中段チェリー（左 4-5-6 = replay/cherry/bell）")]
        public void Cherry_On_Left_Middle_Is_Center_Cherry()
        {
            var aligned = SlotLines.Aligned(ProjectReels(), ProjectPayouts(), new[] { 5, 0, 0 });

            Assert.That(aligned, Is.EqualTo(new[] { "center_cherry" }));
        }

        [Test]
        [Description("plan 5章: 左リール上段のチェリーは角チェリー（左 5-6-7 = cherry/bell/watermelon）")]
        public void Cherry_On_Left_Top_Is_Corner_Cherry()
        {
            var aligned = SlotLines.Aligned(ProjectReels(), ProjectPayouts(), new[] { 6, 0, 1 });

            Assert.That(aligned, Is.EqualTo(new[] { "cherry" }));
        }

        [Test]
        [Description("plan 5章: 斜めライン（左上・中中・右下）のベル")]
        public void Bell_On_Diagonal_Line_Is_Aligned()
        {
            var aligned = SlotLines.Aligned(ProjectReels(), ProjectPayouts(), new[] { 3, 2, 5 });

            Assert.That(aligned, Is.EqualTo(new[] { "bell" }));
        }

        [Test]
        [Description("plan 5章: 左上がりの斜めでスイカと角チェリーが同時に揃う（左 5-6-7、中 20-0-1、右 20-0-1）")]
        public void Two_Roles_Can_Align_At_Once()
        {
            var aligned = SlotLines.Aligned(ProjectReels(), ProjectPayouts(), new[] { 6, 0, 0 });

            Assert.That(aligned, Is.EquivalentTo(new[] { "cherry", "watermelon" }));
        }
    }
}
