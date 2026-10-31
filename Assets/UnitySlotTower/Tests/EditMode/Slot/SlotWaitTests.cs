using NUnit.Framework;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotWaitTests
    {
        [Test]
        public void First_Lever_Starts_Immediately()
        {
            var wait = new SlotWait(2.0);
            wait.Request();

            Assert.That(wait.Consume(0.0), Is.True);
        }

        [Test]
        public void Lever_Within_Wait_Is_Held_Until_Wait_Elapses()
        {
            var wait = new SlotWait(2.0);
            wait.Started(10.0);
            wait.Request();

            Assert.That(wait.Consume(11.9), Is.False);
            Assert.That(wait.IsPending, Is.True);
            Assert.That(wait.Consume(12.0), Is.True);
        }

        [Test]
        public void Without_Request_Nothing_Starts()
        {
            var wait = new SlotWait(2.0);

            Assert.That(wait.Consume(100.0), Is.False);
        }

        [Test]
        public void Consume_Clears_The_Request()
        {
            var wait = new SlotWait(2.0);
            wait.Request();
            wait.Consume(0.0);

            Assert.That(wait.IsPending, Is.False);
            Assert.That(wait.Consume(0.0), Is.False);
        }

        [Test]
        public void Remaining_Counts_Down_From_Last_Start()
        {
            var wait = new SlotWait(2.0);
            Assert.That(wait.Remaining(0.0), Is.EqualTo(0.0));

            wait.Started(5.0);

            Assert.That(wait.Remaining(5.5), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(wait.Remaining(8.0), Is.EqualTo(0.0));
        }
    }
}
