using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-010, FR-013〜016: ウェーブの準備・戦闘・終了とストック（AC-012）")]
    public sealed class WaveRunTests
    {
        // フィクスチャの地図（4×2）。地上マスは (1,0)(2,0)、高台マスは (1,1)(3,1)。
        //   S1 G G E
        //   S2 H X H
        // fodder（HP1・攻撃0・1マス/秒）は 0 秒に出て約3秒でゴールに着く。
        private const string OneFodderPerWave =
            "1,0,fodder,S1,g,0\n2,0,fodder,S1,g,0\n3,0,fodder,S1,g,0\n";

        // 10 秒あればどのウェーブも終わる。
        private const int WaveSteps = 600;

        private CoinWallet _wallet;

        private WaveRun Create(int coins = 100, int waveCount = 3, int life = 10, int lifeBonus = 3, int deployLimit = 8,
            string waveRows = OneFodderPerWave)
        {
            _wallet = new CoinWallet(coins);
            var config = GameConfig.Load(CsvParser.Parse(GameConfig.FileName,
                $"key,value\ninitial_coins,{coins}\nbet,3\nlife,{life}\nlife_bonus_per_life,{lifeBonus}\n" +
                $"deploy_limit,{deployLimit}\nretreat_refund_rate,0.5\n"));
            var units = BattleFixtures.LoadUnits();
            var map = BattleFixtures.LoadMap();
            return new WaveRun(config, units, BattleFixtures.LoadRanges(),
                new UnitUpgrades(BattleFixtures.LoadUpgrades(), _wallet), _wallet, waveCount,
                wave => new WaveStage(map, BattleFixtures.LoadWave(waveRows, wave)), new SeededRandom(1));
        }

        private static SummonResult Place(WaveRun run, string unitId, int x, int y)
        {
            var result = run.Deployment.Select(unitId);
            if (result != SummonResult.Summoned)
            {
                return result;
            }

            run.Deployment.SetCursor(new GridPoint(x, y));
            Assert.That(run.Deployment.ChooseCell(), Is.True, $"{unitId} を ({x},{y}) に置けない");
            return run.Deployment.Confirm();
        }

        /// <summary>W1 で剣士を1体置いて終え、W2 の準備に入った状態（剣士のストック1）。</summary>
        private WaveRun ClearWave1WithSword(int coins = 100, int lifeBonus = 3, int deployLimit = 8)
        {
            var run = Create(coins: coins, lifeBonus: lifeBonus, deployLimit: deployLimit);
            Place(run, "sword", 1, 0);
            run.Start();
            run.Advance(WaveSteps);
            Assert.That((run.Wave, run.Stock.Count("sword")), Is.EqualTo((2, 1)), "W1 を剣士1体で終えられない");
            return run;
        }

        [Test]
        public void Run_Starts_In_Preparation_Of_Wave_1()
        {
            var run = Create();

            Assert.That((run.Wave, run.Phase), Is.EqualTo((1, WavePhase.Preparing)));
        }

        [Test]
        public void Enemies_Do_Not_Appear_During_Preparation()
        {
            var run = Create();

            run.Advance(WaveSteps);

            Assert.That((run.Field.Enemies.Count, run.Field.Tick), Is.EqualTo((0, 0L)));
        }

        [Test]
        public void Starting_Begins_The_Fight()
        {
            var run = Create();

            run.Start();
            run.Advance(1);

            Assert.That((run.Phase, run.Field.Enemies.Count), Is.EqualTo((WavePhase.Fighting, 1)));
        }

        [Test]
        [Description("plan C-3: 準備中は召喚の待ち時間を無視する")]
        public void Same_Type_Can_Be_Summoned_Again_During_Preparation()
        {
            var run = Create();
            Place(run, "sword", 1, 0);

            Assert.That(Place(run, "sword", 2, 0), Is.EqualTo(SummonResult.Summoned));
        }

        [Test]
        public void Preparation_Summons_Leave_No_Cooldown_For_The_Fight()
        {
            var run = Create();
            Place(run, "sword", 1, 0);

            run.Start();

            Assert.That(run.Summoner.CooldownRemaining("sword"), Is.EqualTo(0));
        }

        [Test]
        public void Cooldown_Applies_During_The_Fight()
        {
            var run = Create();
            run.Start();
            Place(run, "sword", 1, 0);

            Assert.That(run.Deployment.Select("sword"), Is.EqualTo(SummonResult.CoolingDown));
        }

        [Test]
        [Description("AC-012: 残りライフ分のコインが加算され、コインが残る")]
        public void Clearing_A_Wave_Adds_Life_Bonus_To_The_Coins()
        {
            // fodder がゴールに着いてライフ 9。ボーナスは 9 × 3 = 27。ゴールに着いた敵は落とさない。
            var run = Create(coins: 100);
            run.Start();

            run.Advance(WaveSteps);

            Assert.That(_wallet.Coins, Is.EqualTo(127));
        }

        [Test]
        public void Clearing_A_Wave_Moves_To_Preparation_Of_The_Next()
        {
            var run = Create();
            run.Start();

            run.Advance(WaveSteps);

            Assert.That((run.Wave, run.Phase), Is.EqualTo((2, WavePhase.Preparing)));
        }

        [Test]
        [Description("FR-013: ライフは各ウェーブ開始時に満タンに戻る")]
        public void Next_Wave_Starts_With_Full_Life()
        {
            var run = Create(life: 10);
            run.Start();

            run.Advance(WaveSteps);

            Assert.That(run.Field.Life, Is.EqualTo(10));
        }

        [Test]
        public void Wave_Began_Is_Raised_For_The_Next_Wave()
        {
            var run = Create();
            var began = 0;
            run.WaveBegan += wave => began = wave;
            run.Start();

            run.Advance(WaveSteps);

            Assert.That(began, Is.EqualTo(2));
        }

        [Test]
        public void Losing_All_Life_Ends_The_Run()
        {
            var run = Create(life: 1);
            run.Start();

            run.Advance(WaveSteps);

            Assert.That((run.Wave, run.Phase), Is.EqualTo((1, WavePhase.GameOver)));
        }

        [Test]
        [Description("AC-012: 配置中だった数と同数の無料ストックができる")]
        public void Deployed_Units_Become_Free_Stock_By_Type()
        {
            // 剣士が fodder を止めて倒し、弓2体は高台に居るだけ。
            var run = Create();
            Place(run, "sword", 1, 0);
            Place(run, "archer", 1, 1);
            Place(run, "archer", 3, 1);
            run.Start();

            run.Advance(WaveSteps);

            Assert.That((run.Stock.Count("sword"), run.Stock.Count("archer"), run.Stock.Count("tank")),
                Is.EqualTo((1, 2, 0)));
        }

        [Test]
        public void Next_Wave_Starts_With_An_Empty_Field()
        {
            var run = Create();
            Place(run, "sword", 1, 0);
            run.Start();

            run.Advance(WaveSteps);

            Assert.That((run.Field.Units.Count, run.Summoner.DeployedCount), Is.EqualTo((0, 0)));
        }

        [Test]
        [Description("FR-016: 無料ストックはコインなしで配置できる")]
        public void Placing_From_Stock_Costs_No_Coins()
        {
            var run = ClearWave1WithSword();
            var coins = _wallet.Coins;

            Place(run, "sword", 1, 0);

            Assert.That((_wallet.Coins, run.Stock.Count("sword")), Is.EqualTo((coins, 0)));
        }

        [Test]
        public void Stock_Can_Be_Placed_Without_Enough_Coins()
        {
            // 剣士 12 枚をちょうど払い、ライフボーナスなし。fodder のドロップ（40%で1枚）があっても 12 枚には届かない。
            var run = ClearWave1WithSword(coins: 12, lifeBonus: 0);

            Assert.That(Place(run, "sword", 1, 0), Is.EqualTo(SummonResult.Summoned));
        }

        [Test]
        [Description("plan C-3: ストックからの配置も配置上限に数える")]
        public void Stock_Placement_Counts_Toward_The_Deploy_Limit()
        {
            var run = ClearWave1WithSword(deployLimit: 1);
            Place(run, "sword", 1, 0);

            Assert.That(run.Deployment.Select("archer"), Is.EqualTo(SummonResult.DeployLimitReached));
        }

        [Test]
        [Description("AC-012: 強化が残り、ストックから置いた個体は HP 全快")]
        public void Stock_Unit_Has_Full_Hp_At_The_Carried_Upgrade()
        {
            // 剣士は段階2で HP×1.5 → 1800。
            var run = Create();
            Place(run, "sword", 1, 0);
            run.Deployment.Upgrade("sword");
            run.Start();
            run.Advance(WaveSteps);

            Place(run, "sword", 1, 0);

            var unit = run.Field.Units[0];
            Assert.That((unit.MaxHp, unit.Hp), Is.EqualTo((1800, 1800)));
        }

        [Test]
        public void Unused_Stock_Carries_Over_To_Later_Waves()
        {
            var run = ClearWave1WithSword();
            run.Start();

            run.Advance(WaveSteps);

            Assert.That((run.Wave, run.Stock.Count("sword")), Is.EqualTo((3, 1)));
        }

        [Test]
        [Description("plan C-3: ストック由来の味方の撤退も召喚コストを基準に返す")]
        public void Retreating_A_Stock_Unit_Refunds_Half_Its_Cost()
        {
            var run = ClearWave1WithSword();
            Place(run, "sword", 1, 0);
            var coins = _wallet.Coins;

            run.Deployment.Retreat(run.Field.Units[0]);

            Assert.That(_wallet.Coins - coins, Is.EqualTo(6));
        }

        [Test]
        [Description("FR-033: 撃破ドロップは戦闘中に財布へ入る")]
        public void Drops_Go_Into_The_Wallet_During_The_Fight()
        {
            // rich は倒されると必ず 2〜3 枚落とす。ウェーブも終わるのでライフボーナスは 0 にする。
            var run = Create(coins: 100, lifeBonus: 0, waveRows: "1,0,rich,S1,g,0\n");
            Place(run, "sword", 1, 0);
            var coins = _wallet.Coins;
            run.Start();

            run.Advance(120);

            Assert.That(_wallet.Coins - coins, Is.InRange(2, 3));
        }

        [Test]
        public void Clearing_The_Last_Wave_Completes_The_Run()
        {
            var run = Create(waveCount: 1);
            run.Start();

            run.Advance(WaveSteps);

            Assert.That((run.Wave, run.Phase), Is.EqualTo((1, WavePhase.Completed)));
        }

        [Test]
        [Description("AC-010: 最終ボスを倒しても、残りの敵を全滅させるまでクリアにならない（2026-10-30 ユーザー決定）")]
        public void Defeating_The_Final_Boss_Does_Not_Complete_The_Run_While_Enemies_Remain()
        {
            // ボス役の fodder は約 0.5 秒で剣士に倒される。雑魚は 5 秒に出る。
            var run = Create(waveCount: 1, waveRows: "1,0,fodder,S1,g,1\n1,5,fodder,S1,g,0\n");
            Place(run, "sword", 1, 0);
            run.Start();

            run.Advance(120);

            Assert.That((run.Field.Enemies.Count, run.Phase), Is.EqualTo((0, WavePhase.Fighting)));
        }
    }
}
