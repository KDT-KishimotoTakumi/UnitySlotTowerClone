using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Tutorial;

namespace UnitySlotTower.Tests.EditMode.Tutorial
{
    [Description("FR-004（T403）: 文字なし説明を出す場面と、初回だけにするための記録")]
    public sealed class TutorialProgressTests
    {
        private static TutorialSituation Situation(int wave = 1, WavePhase wavePhase = WavePhase.Preparing,
            DeployPhase deployPhase = DeployPhase.Idle, int unitsOnField = 0, bool unitSelected = false,
            bool slotInBonus = false) =>
            new TutorialSituation(wave, wavePhase, deployPhase, unitsOnField, unitSelected, slotInBonus);

        private static TutorialProgress SeenExcept(TutorialStep step)
        {
            var progress = new TutorialProgress();
            foreach (TutorialStep other in System.Enum.GetValues(typeof(TutorialStep)))
            {
                if (other != step)
                {
                    progress.MarkSeen(other);
                }
            }

            return progress;
        }

        [Test]
        public void Summon_Is_Shown_First_At_The_Start_Of_Wave_1()
        {
            Assert.That(new TutorialProgress().Next(Situation()), Is.EqualTo(TutorialStep.Summon));
        }

        [Test]
        public void Placement_And_Facing_Follow_The_Deploy_Phase()
        {
            var progress = new TutorialProgress();
            progress.MarkSeen(TutorialStep.Summon);

            Assert.That((progress.Next(Situation(deployPhase: DeployPhase.ChoosingCell)),
                    progress.Next(Situation(deployPhase: DeployPhase.ChoosingFacing))),
                Is.EqualTo(((TutorialStep?)TutorialStep.Place, (TutorialStep?)TutorialStep.Facing)));
        }

        [Test]
        public void Placement_Wins_Over_Summon_When_Already_Placing()
        {
            Assert.That(new TutorialProgress().Next(Situation(deployPhase: DeployPhase.ChoosingCell)),
                Is.EqualTo(TutorialStep.Place));
        }

        [Test]
        public void Start_Is_Shown_After_The_First_Unit_Is_Placed_While_Preparing()
        {
            var progress = SeenExcept(TutorialStep.Start);

            Assert.That((progress.Next(Situation()), progress.Next(Situation(unitsOnField: 1))),
                Is.EqualTo(((TutorialStep?)null, (TutorialStep?)TutorialStep.Start)));
        }

        [Test]
        public void Slot_Is_Shown_When_Fighting_Begins()
        {
            var progress = SeenExcept(TutorialStep.Slot);

            Assert.That((progress.Next(Situation(unitsOnField: 1)),
                    progress.Next(Situation(wavePhase: WavePhase.Fighting, unitsOnField: 1))),
                Is.EqualTo(((TutorialStep?)null, (TutorialStep?)TutorialStep.Slot)));
        }

        [Test]
        public void Bonus_Is_Shown_When_The_Slot_Enters_A_Bonus()
        {
            var progress = SeenExcept(TutorialStep.Bonus);

            Assert.That(progress.Next(Situation(wavePhase: WavePhase.Fighting, slotInBonus: true)),
                Is.EqualTo(TutorialStep.Bonus));
        }

        [Test]
        public void Upgrade_Is_Shown_From_The_Preparation_Of_Wave_2()
        {
            var progress = SeenExcept(TutorialStep.Upgrade);

            Assert.That((progress.Next(Situation(wave: 1)), progress.Next(Situation(wave: 2))),
                Is.EqualTo(((TutorialStep?)null, (TutorialStep?)TutorialStep.Upgrade)));
        }

        [Test]
        public void Retreat_Is_Shown_When_A_Placed_Unit_Is_Selected()
        {
            var progress = SeenExcept(TutorialStep.Retreat);

            Assert.That(progress.Next(Situation(wavePhase: WavePhase.Fighting, unitsOnField: 1, unitSelected: true)),
                Is.EqualTo(TutorialStep.Retreat));
        }

        [Test]
        public void Idle_Only_Steps_Wait_While_Placing()
        {
            var progress = SeenExcept(TutorialStep.Retreat);

            Assert.That(progress.Next(Situation(deployPhase: DeployPhase.ChoosingCell, unitSelected: true)),
                Is.Null);
        }

        [TestCase(WavePhase.Completed)]
        [TestCase(WavePhase.GameOver)]
        public void Nothing_Is_Shown_After_The_Game_Ends(WavePhase phase)
        {
            Assert.That(new TutorialProgress().Next(Situation(wavePhase: phase, deployPhase: DeployPhase.ChoosingCell)),
                Is.Null);
        }

        [Test]
        public void Seen_Steps_Are_Not_Shown_Again()
        {
            var progress = new TutorialProgress();
            progress.MarkSeen(TutorialStep.Summon);

            Assert.That(progress.Next(Situation()), Is.Null);
        }

        [Test]
        public void Serialize_And_Parse_Round_Trip()
        {
            var progress = new TutorialProgress();
            progress.MarkSeen(TutorialStep.Slot);
            progress.MarkSeen(TutorialStep.Summon);

            var restored = TutorialProgress.Parse(progress.Serialize());

            Assert.That((progress.Serialize(), restored.HasSeen(TutorialStep.Summon), restored.HasSeen(TutorialStep.Slot),
                    restored.HasSeen(TutorialStep.Place)),
                Is.EqualTo(("Summon,Slot", true, true, false)));
        }

        [Test]
        public void Parse_Ignores_Unknown_Numeric_And_Empty_Names()
        {
            var progress = TutorialProgress.Parse("Summon,,Dance, Retreat ,3");

            Assert.That((progress.Serialize(), TutorialProgress.Parse(null).Serialize()),
                Is.EqualTo(("Summon,Retreat", "")));
        }

        [Test]
        public void AllSeen_Is_Complete_And_Shows_Nothing()
        {
            var progress = TutorialProgress.AllSeen();

            Assert.That((progress.IsComplete, progress.Next(Situation(deployPhase: DeployPhase.ChoosingCell))),
                Is.EqualTo((true, (TutorialStep?)null)));
        }
    }
}
