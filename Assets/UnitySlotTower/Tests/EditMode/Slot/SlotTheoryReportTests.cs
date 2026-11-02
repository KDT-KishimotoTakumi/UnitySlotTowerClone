using NUnit.Framework;
using UnitySlotTower.Editor.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotTheoryReportTests
    {
        [Test]
        [Description("AC-040: Editor ツールが設定1〜6の理論機械割を1行ずつ出す")]
        public void Report_Lists_Every_Setting()
        {
            var lines = SlotTheoryReport.Build().Trim().Split('\n');

            Assert.That((lines.Length, lines[1].StartsWith("1: "), lines[6].StartsWith("6: ")), Is.EqualTo((7, true, true)), string.Join("\n", lines));
        }
    }
}
