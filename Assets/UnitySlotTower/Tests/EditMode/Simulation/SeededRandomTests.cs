using NUnit.Framework;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Simulation
{
    public sealed class SeededRandomTests
    {
        [Test]
        public void Matches_SplitMix64_Reference_Output()
        {
            var random = new SeededRandom(0);

            Assert.That(random.NextUInt64(), Is.EqualTo(0xE220A8397B1DCDAFUL));
        }

        [Test]
        public void Same_Seed_Gives_Same_Sequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            var same = true;
            for (var i = 0; i < 1000; i++)
            {
                same &= a.NextInt(0, 100) == b.NextInt(0, 100);
            }

            Assert.That(same, Is.True);
        }

        [Test]
        public void Different_Seeds_Give_Different_Sequences()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            var same = true;
            for (var i = 0; i < 10; i++)
            {
                same &= a.NextUInt64() == b.NextUInt64();
            }

            Assert.That(same, Is.False);
        }

        [Test]
        public void NextInt_Stays_Within_Range_And_Covers_It()
        {
            var random = new SeededRandom(7);
            var seen = new bool[6];
            var inRange = true;
            for (var i = 0; i < 10000; i++)
            {
                var value = random.NextInt(3, 9);
                inRange &= value >= 3 && value < 9;
                if (inRange)
                {
                    seen[value - 3] = true;
                }
            }

            Assert.That(inRange && System.Array.TrueForAll(seen, s => s), Is.True);
        }

        [Test]
        public void NextDouble_Stays_In_Unit_Interval()
        {
            var random = new SeededRandom(9);
            var inRange = true;
            for (var i = 0; i < 10000; i++)
            {
                var value = random.NextDouble();
                inRange &= value >= 0.0 && value < 1.0;
            }

            Assert.That(inRange, Is.True);
        }
    }
}
