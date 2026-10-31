using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotTheoryTests
    {
        private const int Bet = 3;
        private const string ProbabilityHeader = "role,state,setting1,setting2,setting3,setting4,setting5,setting6\n";

        private static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        /// <summary>ボーナス上限 20枚（ベル8枚×3ゲーム）、確変 <paramref name="kakuhenGames"/> ゲーム・突入率 <paramref name="entryRate"/>。</summary>
        private static CsvTable ShortRules(int kakuhenGames, double entryRate) =>
            CsvParser.Parse(SlotRules.FileName,
                $"key,value\nbig_payout_limit,20\nreg_payout_limit,20\nkakuhen_games,{kakuhenGames}\nkakuhen_entry_rate,{entryRate}\nwait_seconds,2\nmax_slide,4\n");

        /// <summary>プロジェクトのリール・払い出し表と、行 <paramref name="probabilityRows"/> だけの抽選表で設定1の理論値を出す。</summary>
        private static SlotTheoryResult Theory(string probabilityRows, CsvTable rules, SlotPlayStyle style)
        {
            var reels = SlotReels.Load(ProjectTable(SlotReels.FileName));
            var payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));
            var probabilities = SlotProbabilities.Load(CsvParser.Parse(SlotProbabilities.FileName, ProbabilityHeader + probabilityRows), payouts);
            return SlotTheory.Calculate(reels, payouts, probabilities, SlotRules.Load(rules), Bet, 1, style);
        }

        [Test]
        [Description("AC-040: 毎ゲームベル（8枚、必ず揃う）なら機械割は 8/3")]
        public void Payout_Rate_Of_Bell_Every_Game_Is_Its_Payout_Over_The_Bet()
        {
            var result = Theory("bell,normal,1,1,1,1,1,1\n", ShortRules(2, 1), SlotPlayStyle.Random);

            Assert.That(result.PayoutRate, Is.EqualTo(8.0 / 3).Within(1e-9));
        }

        [Test]
        [Description("AC-040: リプレイ 1/4・ベル 1/4 なら 払い出し 2枚 / 投入 3×3/4枚")]
        public void Payout_Rate_Does_Not_Count_The_Bet_After_A_Replay()
        {
            var result = Theory("replay,normal,1/4,1/4,1/4,1/4,1/4,1/4\nbell,normal,1/4,1/4,1/4,1/4,1/4,1/4\n", ShortRules(2, 1),
                SlotPlayStyle.Random);

            Assert.That(result.PayoutRate, Is.EqualTo(2 / 2.25).Within(1e-9));
        }

        [Test]
        [Description("AC-040: 完全攻略で毎ゲーム BIG・ボーナス中ベル・突入率0なら、BIG 1G＋ボーナス3G で 24枚 / 12枚")]
        public void Perfect_Play_Payout_Rate_Includes_The_Bonus()
        {
            var result = Theory("big,normal,1,1,1,1,1,1\nbell,bonus,1,1,1,1,1,1\n", ShortRules(2, 0), SlotPlayStyle.Perfect);

            Assert.That(result.PayoutRate, Is.EqualTo(24.0 / 12).Within(1e-9));
        }

        [Test]
        [Description("AC-040: 突入率1・確変2Gでハズレのみなら、BIG 1G＋ボーナス3G＋確変2G で 24枚 / 18枚")]
        public void Perfect_Play_Payout_Rate_Includes_The_Kakuhen_Games()
        {
            var result = Theory("big,normal,1,1,1,1,1,1\nbell,bonus,1,1,1,1,1,1\n", ShortRules(2, 1), SlotPlayStyle.Perfect);

            Assert.That(result.PayoutRate, Is.EqualTo(24.0 / 18).Within(1e-9));
        }

        [Test]
        [Description("AC-040: 上と同じ台で BIG は 6ゲームに1回")]
        public void Perfect_Play_Big_Rate_Counts_Every_Game()
        {
            var result = Theory("big,normal,1,1,1,1,1,1\nbell,bonus,1,1,1,1,1,1\n", ShortRules(2, 1), SlotPlayStyle.Perfect);

            Assert.That(result.BigRate, Is.EqualTo(1.0 / 6).Within(1e-9));
        }
    }
}
