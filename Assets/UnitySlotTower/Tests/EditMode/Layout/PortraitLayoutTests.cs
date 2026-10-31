using NUnit.Framework;
using UnitySlotTower.Core.Layout;

namespace UnitySlotTower.Tests.EditMode.Layout
{
    [Description("FR-001 縦長 3:4、上部 38% が戦闘、余白は黒帯")]
    public sealed class PortraitLayoutTests
    {
        private static (double, double, double, double) Tuple(NormalizedRect r) => (r.X, r.Y, r.Width, r.Height);

        [Test]
        public void Window_Of_960x1280_Uses_Whole_Screen()
        {
            Assert.That(Tuple(PortraitLayout.Letterbox(960, 1280)), Is.EqualTo((0.0, 0.0, 1.0, 1.0)));
        }

        [Test]
        public void Landscape_1080p_Gets_Side_Bars()
        {
            // 高さ 1080 に合わせた幅 810 px を中央に置く。
            var rect = PortraitLayout.Letterbox(1920, 1080);

            Assert.That(Tuple(rect), Is.EqualTo((555.0 / 1920, 0.0, 810.0 / 1920, 1.0)).Using(new Tolerance()));
        }

        [Test]
        public void Landscape_1440p_Gets_Side_Bars()
        {
            var rect = PortraitLayout.Letterbox(2560, 1440);

            Assert.That(Tuple(rect), Is.EqualTo((740.0 / 2560, 0.0, 1080.0 / 2560, 1.0)).Using(new Tolerance()));
        }

        [Test]
        public void Taller_Than_3x4_Gets_Top_And_Bottom_Bars()
        {
            // 幅 1080 に合わせた高さ 1440 px を、高さ 2400 の中央に置く。
            var rect = PortraitLayout.Letterbox(1080, 2400);

            Assert.That(Tuple(rect), Is.EqualTo((0.0, 480.0 / 2400, 1.0, 1440.0 / 2400)).Using(new Tolerance()));
        }

        [Test]
        public void Battle_View_Is_Top_38_Percent_Of_Letterbox()
        {
            var battle = PortraitLayout.Battle(1080, 2400);

            Assert.That(Tuple(battle), Is.EqualTo((0.0, 0.2 + 0.6 * 0.62, 1.0, 0.6 * 0.38)).Using(new Tolerance()));
        }

        [Test]
        public void Ui_Matches_Height_On_Wide_Screens()
        {
            Assert.That(PortraitLayout.MatchHeight(1920, 1080), Is.True);
        }

        [Test]
        public void Ui_Matches_Width_On_Tall_Screens()
        {
            Assert.That(PortraitLayout.MatchHeight(1080, 2400), Is.False);
        }

        private sealed class Tolerance : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                var a = ((double, double, double, double))x;
                var b = ((double, double, double, double))y;
                const double eps = 1e-6;
                var same = System.Math.Abs(a.Item1 - b.Item1) < eps && System.Math.Abs(a.Item2 - b.Item2) < eps
                    && System.Math.Abs(a.Item3 - b.Item3) < eps && System.Math.Abs(a.Item4 - b.Item4) < eps;
                return same ? 0 : 1;
            }
        }
    }
}
