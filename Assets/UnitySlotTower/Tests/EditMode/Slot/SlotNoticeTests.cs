using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    [Description("spec 002 FR-121, AC-114: 当たり確定の告知はボーナス役が成立したゲームだけ")]
    public sealed class SlotNoticeTests
    {
        private static SlotProbabilities ProjectProbabilities()
        {
            var payouts = SlotPayouts.Load(CsvParser.Parse(SlotPayouts.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, SlotPayouts.FileName))));
            return SlotProbabilities.Load(CsvParser.Parse(SlotProbabilities.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, SlotProbabilities.FileName))), payouts);
        }

        [Test]
        public void Every_Project_Flag_With_A_Bonus_Announces_And_Others_Do_Not()
        {
            var flags = ProjectProbabilities().Flags;

            var wrong = flags.Where(flag => SlotNotice.ShouldAnnounce(flag) != (flag.Has("big") || flag.Has("reg")))
                .Select(flag => flag.Id);

            Assert.That(wrong, Is.Empty);
            Assert.That(flags.Count(SlotNotice.ShouldAnnounce), Is.EqualTo(5), "big・reg・big+attack・big+speed・reg+heal");
        }

        [Test]
        [Description("持ち越し中はボーナス役を外したフラグになるので告知しない")]
        public void Flag_Without_Its_Bonus_Does_Not_Announce()
        {
            var probabilities = ProjectProbabilities();
            var bigAttack = probabilities.Flags.Single(flag => flag.Id == "big+attack");

            Assert.That(SlotNotice.ShouldAnnounce(probabilities.WithoutBonus(bigAttack)), Is.False);
        }

        [Test]
        public void No_Flag_Does_Not_Announce()
        {
            Assert.That(SlotNotice.ShouldAnnounce(null), Is.False);
        }
    }
}
