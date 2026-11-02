using NUnit.Framework;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Simulation
{
    public sealed class SimulationClockTests
    {
        [Test]
        public void One_Second_Advances_Sixty_Steps()
        {
            var clock = new SimulationClock();

            Assert.That(clock.Advance(1.0), Is.EqualTo(60));
        }

        [Test]
        public void Sixty_Frames_Of_One_Sixtieth_Advance_Sixty_Ticks()
        {
            var clock = new SimulationClock();
            for (var i = 0; i < 60; i++)
            {
                clock.Advance(1.0 / 60.0);
            }

            Assert.That(clock.Tick, Is.EqualTo(60));
        }

        [Test]
        public void Fractional_Time_Carries_To_Next_Advance()
        {
            var clock = new SimulationClock();
            clock.Advance(0.01);

            Assert.That(clock.Advance(0.01), Is.EqualTo(1));
        }

        [Test]
        public void Paused_Clock_Does_Not_Advance()
        {
            var clock = new SimulationClock { IsPaused = true };
            clock.Advance(5.0);

            Assert.That(clock.Tick, Is.EqualTo(0));
        }

        [Test]
        public void Time_Spent_Paused_Is_Not_Carried_After_Resume()
        {
            var clock = new SimulationClock { IsPaused = true };
            clock.Advance(5.0);
            clock.IsPaused = false;

            Assert.That(clock.Advance(0.0), Is.EqualTo(0));
        }

        [Test]
        public void Elapsed_Seconds_Follows_Ticks()
        {
            var clock = new SimulationClock();
            clock.Advance(0.5);

            Assert.That(clock.ElapsedSeconds, Is.EqualTo(0.5).Within(1e-9));
        }
    }
}
