using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// REG の群れ（spec 002 FR-101〜FR-103、AC-101）と、スロットの役をウェーブの進行に渡す規則（FR-111〜FR-114、AC-111）。
    /// 地図は <see cref="BattleFixtures"/>（地上経路 g、空中経路 a）。
    /// </summary>
    public sealed class RegSwarmTests
    {
        private const string Header = "wave,enemy_id,count,interval\n";

        // fodder（HP1・攻撃0・1マス/秒）はおよそ3秒でゴールに着く。
        private const string OneFodderPerWave = "1,0,fodder,S1,g,0\n2,0,fodder,S1,g,0\n";

        private static RegSwarmTable LoadSwarms(string rows) =>
            RegSwarmTable.Load(CsvParser.Parse(RegSwarmTable.FileName, Header + rows), BattleFixtures.LoadEnemies());

        private static WaveRun CreateRun(string swarmRows, int waveCount = 2, BattleBuffs buffs = null)
        {
            var wallet = new CoinWallet(100);
            var config = GameConfig.Load(CsvParser.Parse(GameConfig.FileName,
                "key,value\ninitial_coins,100\nbet,3\nlife,10\nlife_bonus_per_life,3\ndeploy_limit,8\nretreat_refund_rate,0.5\n"));
            var map = BattleFixtures.LoadMap();
            var swarms = LoadSwarms(swarmRows);
            return new WaveRun(config, BattleFixtures.LoadUnits(), BattleFixtures.LoadRanges(),
                new UnitUpgrades(BattleFixtures.LoadUpgrades(), wallet), wallet, waveCount,
                wave => new WaveStage(map, BattleFixtures.LoadWave(OneFodderPerWave, wave)), new SeededRandom(1),
                buffs ?? new BattleBuffs(1.5, 1, 0.5, 1, 0.3), swarms.Create);
        }

        [Test]
        [Description("FR-101: 群れは行ごとに count 体を interval 秒おきに出し、地上と空中はそれぞれ合う経路を使う")]
        public void Swarm_Creates_Count_Enemies_At_Intervals_On_Matching_Paths()
        {
            var spawns = LoadSwarms("1,fodder,3,0.5\n1,air,1,0\n2,melee,9,1\n").Create(1, BattleFixtures.LoadMap());

            Assert.That(spawns.Select(s => (s.Time, s.Enemy.Id, s.Path.Id)), Is.EqualTo(new[]
            {
                (0.0, "fodder", "g"), (0.0, "air", "a"), (0.5, "fodder", "g"), (1.0, "fodder", "g"),
            }));
        }

        [Test]
        public void Unknown_Enemy_Reports_Its_Line()
        {
            var ex = Assert.Throws<CsvFormatException>(() => LoadSwarms("1,dragon,1,0\n"));

            Assert.That((ex.Error.File, ex.Error.Line), Is.EqualTo((RegSwarmTable.FileName, 2)));
        }

        [Test]
        [Description("FR-101: 戦闘中に REG が揃うと群れが出現口から出る")]
        public void Reg_While_Fighting_Spawns_The_Swarm()
        {
            var run = CreateRun("1,fodder,3,0.5\n");
            run.Start();
            run.Advance(1);

            var applied = run.ApplySlotRole(SlotFlag.RegRole);
            run.Advance(61);

            Assert.That((applied, run.Field.Enemies.Count, run.Field.PendingExtraSpawns), Is.EqualTo((true, 4, 0)));
        }

        [Test]
        [Description("FR-102: 準備中に揃った群れはためておき、戦闘開始ですべて出す")]
        public void Reg_While_Preparing_Is_Held_Until_The_Fight_Starts()
        {
            var run = CreateRun("1,fodder,2,0\n");
            run.ApplySlotRole(SlotFlag.RegRole);
            run.ApplySlotRole(SlotFlag.RegRole);
            var held = (run.PendingSwarms, run.Field.PendingExtraSpawns);

            run.Start();
            run.Advance(1);

            Assert.That((held, run.PendingSwarms, run.Field.Enemies.Count), Is.EqualTo(((2, 0), 0, 5)));
        }

        [Test]
        [Description("FR-103: 群れが残っている間はウェーブが終わらない")]
        public void Wave_Does_Not_End_While_Swarm_Enemies_Remain()
        {
            // 群れの最後の1体は 5 秒後に出るので、ウェーブの fodder が着いた後もウェーブ1が続く。
            var run = CreateRun("1,fodder,2,5\n");
            run.Start();
            run.ApplySlotRole(SlotFlag.RegRole);

            run.Advance(300);
            var waveDuring = run.Wave;
            run.Advance(300);

            Assert.That((waveDuring, run.Wave), Is.EqualTo((1, 2)));
        }

        [Test]
        [Description("FR-103: 最後のウェーブを終えた後の REG では出ない")]
        public void Reg_After_The_Last_Wave_Does_Nothing()
        {
            var run = CreateRun("1,fodder,2,0\n", waveCount: 1);
            run.Start();
            run.Advance(600);

            var applied = run.ApplySlotRole(SlotFlag.RegRole);

            Assert.That((run.Phase, applied, run.PendingSwarms), Is.EqualTo((WavePhase.Completed, false, 0)));
        }

        [Test]
        [Description("FR-113: バフの残り時間は戦闘中だけ減り、準備中は減らない")]
        public void Buff_Time_Does_Not_Decrease_While_Preparing()
        {
            var run = CreateRun("1,fodder,1,0\n");
            run.ApplySlotRole(BattleBuffs.AttackRole);
            run.Advance(30);
            var preparing = run.Buffs.AttackStepsLeft;

            run.Start();
            run.Advance(30);

            Assert.That((preparing, run.Buffs.AttackStepsLeft), Is.EqualTo((60, 30)));
        }

        [Test]
        [Description("FR-111・FR-112: バフはウェーブをまたいで残る")]
        public void Buffs_Carry_Over_To_The_Next_Wave()
        {
            var run = CreateRun("1,fodder,1,0\n", buffs: new BattleBuffs(1.5, 100, 0.5, 100, 0.3));
            run.Start();
            run.ApplySlotRole(BattleBuffs.SpeedRole);
            run.Advance(600);

            Assert.That((run.Wave, run.Buffs.IsSpeedActive, run.Field.Units.Count), Is.EqualTo((2, true, 0)));
        }

        [Test]
        [Description("FR-114: HP回復は配置中の味方を回復し、効果のない役（コイン・BIG）は何もしない")]
        public void Heal_Role_Heals_And_Other_Roles_Do_Nothing()
        {
            var run = CreateRun("1,fodder,1,0\n");
            var applied = new System.Collections.Generic.List<string>();
            run.SlotRoleApplied += applied.Add;

            var results = new[] { "coin", SlotFlag.BigRole, BattleBuffs.HealRole }.Select(run.ApplySlotRole).ToArray();

            Assert.That(results, Is.EqualTo(new[] { false, false, true }));
            Assert.That(applied, Is.EqualTo(new[] { BattleBuffs.HealRole }));
        }

        [Test]
        [Description("FR-130: プロジェクトの群れの表は全ウェーブのマップで出られる")]
        public void Project_Table_Creates_A_Swarm_For_Every_Wave()
        {
            var tables = TablePaths.ReadAll();
            var enemies = EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, tables[EnemyTable.FileName]));
            var swarms = RegSwarmTable.Load(CsvParser.Parse(RegSwarmTable.FileName, tables[RegSwarmTable.FileName]), enemies);
            var paths = CsvParser.Parse(BattleMap.PathsFileName, tables[BattleMap.PathsFileName]);

            var counts = Enumerable.Range(1, 5)
                .Select(wave => swarms.Create(wave, BattleMap.Load($"w{wave}", tables[BattleMap.MapFileName($"w{wave}")], paths)).Count)
                .ToArray();

            Assert.That(counts, Has.All.GreaterThan(0));
        }
    }
}
