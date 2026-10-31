using NUnit.Framework;
using UnitySlotTower.Core.Ui;

namespace UnitySlotTower.Tests.EditMode.Ui
{
    [Description("FR-003 数字と記号（+ − × / %）以外の文字を使わない")]
    public sealed class NumericTextTests
    {
        [TestCase("")]
        [TestCase("0123456789")]
        [TestCase("+30")]
        [TestCase("-3")]
        [TestCase("−" + "15")]
        [TestCase("×" + "2")]
        [TestCase("1/7")]
        [TestCase("50%")]
        public void Digits_And_Symbols_Are_Allowed(string text)
        {
            Assert.That(NumericText.FindInvalid(text), Is.EqualTo(-1));
        }

        [TestCase("x2", 0)]
        [TestCase("12a", 2)]
        [TestCase("WAVE", 0)]
        [TestCase("1.5", 1)]
        [TestCase("コイン", 0)]
        [TestCase("10 20", 2)]
        public void Other_Characters_Are_Reported_At_Their_Position(string text, int index)
        {
            Assert.That(NumericText.FindInvalid(text), Is.EqualTo(index));
        }
    }
}
