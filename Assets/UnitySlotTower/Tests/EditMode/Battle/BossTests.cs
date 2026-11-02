using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// 中ボス・最終ボス（FR-030, AC-010）。中ボスは固有行動なしで強いステータスだけ、最終ボスは範囲攻撃を持つ（2026-10-30 ユーザー決定）。
    /// 範囲攻撃は <see cref="BattleFixtures"/> の quaker（(0,0) から動かず、2 秒ごとに半径 1.5 マスへ攻撃力 500）で見る。
    /// 味方の防御は剣士・弓とも 50 なので、1 回の範囲攻撃で 450 減る。
    /// </summary>
    public sealed class BossTests
    {
        private const string Quaker = "1,0,quaker,S1,g,1\n";

        private static Battlefield FieldWithQuaker() => BattleFixtures.CreateField(Quaker);

        [Test]
        public void Area_Attack_Hits_Every_Unit_Within_Its_Radius()
        {
            var field = FieldWithQuaker();
            var units = BattleFixtures.LoadUnits();
            var sword = field.AddUnit(units.Get("sword"), new GridPoint(1, 0), Facing.Right);
            var near = field.AddUnit(units.Get("archer"), new GridPoint(1, 1), Facing.Right);
            var far = field.AddUnit(units.Get("archer"), new GridPoint(3, 1), Facing.Right);

            field.Advance(120);

            Assert.That((sword.Hp, near.Hp, far.Hp), Is.EqualTo((750, 250, 700)));
        }

        [Test]
        public void First_Area_Attack_Comes_One_Interval_After_Spawning()
        {
            var field = FieldWithQuaker();
            var sword = field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Right);

            field.Advance(119);

            Assert.That(sword.Hp, Is.EqualTo(1200));
        }

        [Test]
        public void Area_Attack_Repeats_Every_Interval()
        {
            var field = FieldWithQuaker();
            var sword = field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Right);

            field.Advance(240);

            Assert.That(sword.Hp, Is.EqualTo(300));
        }

        [Test]
        [Description("味方の攻撃と同じく、対象がないあいだは間隔を消費せず、入った瞬間に撃つ")]
        public void Area_Attack_Waits_For_A_Unit_In_Its_Radius()
        {
            var field = FieldWithQuaker();
            field.Advance(300);
            var sword = field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Right);

            field.Advance(1);

            Assert.That(sword.Hp, Is.EqualTo(750));
        }

        [Test]
        public void Area_Attack_Without_Interval_Is_A_Load_Error()
        {
            var text = BattleFixtures.Enemies.Replace("500,1.5,2\n", "500,1.5,0\n");

            var ex = Assert.Throws<CsvFormatException>(() => EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, text)));

            Assert.That((ex.Error.Line, ex.Error.Column), Is.EqualTo((9, 16)));
        }

        private static EnemyTable ProjectEnemies() =>
            EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, TablePaths.ReadAll()[EnemyTable.FileName]));

        [Test]
        [Description("FR-030 / plan 5章: ボスはブロック消費2以上。中ボスは固有行動なし、最終ボスは範囲攻撃")]
        public void Project_Bosses_Block_Heavily_And_Only_The_Final_Boss_Has_An_Area_Attack()
        {
            var enemies = ProjectEnemies();
            var mid = enemies.Get("mid_boss");
            var final = enemies.Get("final_boss");

            Assert.That((mid.BlockCost >= 2, final.BlockCost >= 2, mid.HasAreaAttack, final.HasAreaAttack),
                Is.EqualTo((true, true, false, true)));
        }

        [Test]
        [Description("plan 5章: 中ボスは 100% で 10 枚、最終ボスは落とさない")]
        public void Project_Boss_Drops_Follow_Plan()
        {
            var enemies = ProjectEnemies();
            var mid = enemies.Get("mid_boss");
            var final = enemies.Get("final_boss");

            Assert.That((mid.DropRate, mid.DropMin, mid.DropMax, final.DropRate),
                Is.EqualTo((1.0, 10, 10, 0.0)));
        }

        [Test]
        [Description("AC-010 / plan 4章: 最終ボスがゴールに着けば全ライフを失いゲームオーバー")]
        public void Project_Final_Boss_Takes_All_Life_At_The_Goal()
        {
            var tables = TablePaths.ReadAll();
            var life = GameConfig.Load(CsvParser.Parse(GameConfig.FileName, tables[GameConfig.FileName])).Life;

            Assert.That(ProjectEnemies().Get("final_boss").LifeDamage, Is.GreaterThanOrEqualTo(life));
        }

        [Test]
        [Description("FR-010: W1〜4 の最後に中ボス、W5 の最後に最終ボス")]
        public void Project_Waves_End_With_Their_Boss()
        {
            var tables = TablePaths.ReadAll();
            var paths = CsvParser.Parse(BattleMap.PathsFileName, tables[BattleMap.PathsFileName]);
            var waves = CsvParser.Parse(WaveSchedule.FileName, tables[WaveSchedule.FileName]);
            var enemies = ProjectEnemies();
            var lastSpawns = new (string, bool)[5];
            for (var wave = 1; wave <= 5; wave++)
            {
                var map = BattleMap.Load($"w{wave}", tables[BattleMap.MapFileName($"w{wave}")], paths);
                var spawns = WaveSchedule.Load(waves, wave, map, enemies).Spawns;
                var last = spawns[spawns.Count - 1];
                lastSpawns[wave - 1] = (last.Enemy.Id, last.IsBoss);
            }

            Assert.That(lastSpawns, Is.EqualTo(new[]
            {
                ("mid_boss", true), ("mid_boss", true), ("mid_boss", true), ("mid_boss", true), ("final_boss", true),
            }));
        }
    }
}
