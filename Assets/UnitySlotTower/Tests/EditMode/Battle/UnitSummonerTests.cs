using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("AC-020: コイン不足・配置上限・種類ごとの待ち時間で召喚が拒否され、撤退で返還率どおりのコインが戻る")]
    public sealed class UnitSummonerTests
    {
        // tank: コスト15・待ち20秒、sword: コスト12・待ち12秒、archer: コスト14・待ち15秒
        private const string Units =
            "id,max_hp,atk,def,attack_interval,block,tile,cost,cooldown,role,buff_rate\n" +
            "tank,1800,220,300,1.2,3,ground,15,20,melee,1\n" +
            "sword,1200,400,150,1,2,ground,12,12,melee,1\n" +
            "archer,700,350,50,1,0,high,14,15,ranged,1\n";

        private CoinWallet _wallet;

        private UnitSummoner Create(int coins, int deployLimit = 8)
        {
            _wallet = new CoinWallet(coins);
            return new UnitSummoner(UnitTable.Load(CsvParser.Parse(UnitTable.FileName, Units)), _wallet, deployLimit, 0.5);
        }

        [Test]
        public void Summon_Spends_The_Unit_Cost()
        {
            var summoner = Create(30);

            Assert.That(summoner.TrySummon("tank"), Is.EqualTo(SummonResult.Summoned));
            Assert.That((_wallet.Coins, summoner.DeployedCount), Is.EqualTo((15, 1)));
        }

        [Test]
        public void Summon_Is_Rejected_When_Coins_Are_Short()
        {
            var summoner = Create(11);

            Assert.That(summoner.TrySummon("sword"), Is.EqualTo(SummonResult.NotEnoughCoins));
        }

        [Test]
        public void Rejection_For_Coins_Changes_Nothing()
        {
            var summoner = Create(11);

            summoner.TrySummon("sword");

            Assert.That((_wallet.Coins, summoner.DeployedCount, summoner.CooldownRemaining("sword")),
                Is.EqualTo((11, 0, 0.0)));
        }

        [Test]
        public void Summon_Is_Rejected_At_The_Deploy_Limit()
        {
            var summoner = Create(100, deployLimit: 2);
            summoner.TrySummon("tank");
            summoner.TrySummon("sword");

            Assert.That(summoner.TrySummon("archer"), Is.EqualTo(SummonResult.DeployLimitReached));
        }

        [Test]
        public void Rejection_For_Limit_Keeps_Coins()
        {
            var summoner = Create(100, deployLimit: 2);
            summoner.TrySummon("tank");
            summoner.TrySummon("sword");

            summoner.TrySummon("archer");

            Assert.That(_wallet.Coins, Is.EqualTo(100 - 15 - 12));
        }

        [Test]
        public void Same_Type_Is_Rejected_During_Its_Cooldown()
        {
            var summoner = Create(100);
            summoner.TrySummon("sword");

            Assert.That(summoner.TrySummon("sword"), Is.EqualTo(SummonResult.CoolingDown));
        }

        [Test]
        public void Other_Type_Can_Be_Summoned_During_A_Cooldown()
        {
            var summoner = Create(100);
            summoner.TrySummon("sword");

            Assert.That(summoner.TrySummon("tank"), Is.EqualTo(SummonResult.Summoned));
        }

        [Test]
        public void Cooldown_Starts_At_The_Unit_Value()
        {
            var summoner = Create(100);

            summoner.TrySummon("sword");

            Assert.That(summoner.CooldownRemaining("sword"), Is.EqualTo(12.0).Within(1e-9));
        }

        [Test]
        public void Same_Type_Is_Still_Rejected_One_Step_Before_The_Cooldown_Ends()
        {
            var summoner = Create(100);
            summoner.TrySummon("sword");

            summoner.Advance(12 * SimulationClock.StepsPerSecond - 1);

            Assert.That(summoner.TrySummon("sword"), Is.EqualTo(SummonResult.CoolingDown));
        }

        [Test]
        public void Same_Type_Can_Be_Summoned_When_The_Cooldown_Ends()
        {
            var summoner = Create(100);
            summoner.TrySummon("sword");

            summoner.Advance(12 * SimulationClock.StepsPerSecond);

            Assert.That(summoner.TrySummon("sword"), Is.EqualTo(SummonResult.Summoned));
        }

        [Test]
        public void Retreat_Refunds_The_Cost_Times_The_Rate_Rounded_Down()
        {
            var summoner = Create(30);
            summoner.TrySummon("tank");

            // 15 × 0.5 = 7.5 → 7
            Assert.That((summoner.Retreat("tank"), _wallet.Coins), Is.EqualTo((7, 22)));
        }

        [Test]
        public void Retreat_Frees_A_Deploy_Slot()
        {
            var summoner = Create(100, deployLimit: 2);
            summoner.TrySummon("tank");
            summoner.TrySummon("sword");

            summoner.Retreat("tank");

            Assert.That(summoner.TrySummon("archer"), Is.EqualTo(SummonResult.Summoned));
        }
    }
}
