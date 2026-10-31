using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotTableTests
    {
        private static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        [Test]
        [Description("plan 5章: 3リール×21コマ、図柄は 7・BAR・ベル・スイカ・チェリー・リプレイ")]
        public void Project_Reels_Use_Every_Symbol_On_Every_Reel()
        {
            var reels = SlotReels.Load(ProjectTable(SlotReels.FileName));

            var missing = Enumerable.Range(0, SlotReels.ReelCount)
                .SelectMany(reel => System.Enum.GetValues(typeof(SlotSymbol)).Cast<SlotSymbol>()
                    .Where(symbol => Enumerable.Range(0, SlotReels.StopCount).All(i => reels.At(reel, i) != symbol))
                    .Select(symbol => $"reel{reel + 1}:{symbol}"));
            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void Reel_Index_Wraps_Around()
        {
            var reels = SlotReels.Load(ProjectTable(SlotReels.FileName));

            Assert.That((reels.At(0, -1), reels.At(0, 21)), Is.EqualTo((reels.At(0, 20), reels.At(0, 0))));
        }

        [Test]
        public void Missing_Reel_Index_Is_Reported()
        {
            var text = "index,reel1,reel2,reel3\n" + string.Concat(Enumerable.Range(0, 20).Select(i => $"{i},bell,bell,bell\n"));

            var ex = Assert.Throws<CsvFormatException>(() => SlotReels.Load(CsvParser.Parse("slot_reels.csv", text)));
            Assert.That(ex.Error.Message, Does.Contain("20"));
        }

        [Test]
        [Description("FR-041: ベル・リプレイ・チェリー・スイカ・中段チェリー・BIG・REG")]
        public void Project_Payouts_Define_All_Roles()
        {
            var payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));

            Assert.That(payouts.All.Select(p => p.Role),
                Is.EquivalentTo(new[] { "replay", "bell", "watermelon", "cherry", "center_cherry", "big", "reg" }));
        }

        [Test]
        [Description("FR-041: BIG は 7・7・7")]
        public void Big_Is_Three_Sevens()
        {
            var payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));

            Assert.That(payouts.Get("big").Symbols,
                Is.EqualTo(new SlotSymbol?[] { SlotSymbol.Seven, SlotSymbol.Seven, SlotSymbol.Seven }));
        }

        [Test]
        [Description("FR-041: REG は 7・7・BAR")]
        public void Reg_Is_Seven_Seven_Bar()
        {
            var payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));

            Assert.That(payouts.Get("reg").Symbols,
                Is.EqualTo(new SlotSymbol?[] { SlotSymbol.Seven, SlotSymbol.Seven, SlotSymbol.Bar }));
        }

        [Test]
        [Description("plan 5章: チェリーは左リールだけで成立する")]
        public void Cherry_Needs_Only_The_Left_Reel()
        {
            var payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));

            Assert.That(payouts.Get("cherry").Symbols, Is.EqualTo(new SlotSymbol?[] { SlotSymbol.Cherry, null, null }));
        }

        [Test]
        public void Duplicate_Role_Is_Reported_With_Its_Line()
        {
            var text = "role,payout,symbol1,symbol2,symbol3\nbell,8,bell,bell,bell\nbell,6,bell,bell,bell\n";

            var ex = Assert.Throws<CsvFormatException>(() => SlotPayouts.Load(CsvParser.Parse("slot_payouts.csv", text)));
            Assert.That(ex.Error.Line, Is.EqualTo(3));
        }

        [Test]
        [Description("plan 5章: BIG 240枚、REG 96枚、確変 100G・突入率 100%、ウェイト 2.0 秒、最大4コマ滑り")]
        public void Project_Rules_Hold_Planned_Initial_Values()
        {
            var rules = SlotRules.Load(ProjectTable(SlotRules.FileName));

            Assert.That((rules.BigPayoutLimit, rules.RegPayoutLimit, rules.KakuhenGames, rules.KakuhenEntryRate,
                    rules.WaitSeconds, rules.MaxSlide),
                Is.EqualTo((240, 96, 100, 1.0, 2.0, 4)));
        }
    }
}
