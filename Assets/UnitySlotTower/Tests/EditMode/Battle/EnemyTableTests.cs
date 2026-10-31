using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    public sealed class EnemyTableTests
    {
        private static EnemyTable Project() =>
            EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, EnemyTable.FileName))));

        [Test]
        public void Reads_Movement_And_Attack_Columns()
        {
            var ranged = BattleFixtures.LoadEnemies().Get("ranged");

            Assert.That((ranged.MaxHp, ranged.Atk, ranged.AttackInterval, ranged.MoveSpeed, ranged.Range, ranged.IsAir),
                Is.EqualTo((700, 300, 2.0, 0.5, 1.5, false)));
        }

        [Test]
        public void Reads_Drop_Columns()
        {
            var air = BattleFixtures.LoadEnemies().Get("air");

            Assert.That((air.IsAir, air.BlockCost, air.LifeDamage, air.DropRate, air.DropMin, air.DropMax),
                Is.EqualTo((true, 1, 1, 0.5, 1, 2)));
        }

        [Test]
        public void Duplicate_Id_Is_A_Load_Error_At_Its_Line()
        {
            // 3 行目の ranged を melee に書き換えて、2 行目と重複させる。
            var text = BattleFixtures.Enemies.Replace("ranged,", "melee,");

            var ex = Assert.Throws<CsvFormatException>(() => EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, text)));

            Assert.That(ex.Error.Line, Is.EqualTo(3));
        }

        [Test]
        [Description("FR-030 / M2: 近距離・遠距離・空中の雑魚3種（ボス2種は M3）")]
        public void Project_Has_Melee_Ranged_And_Air_Grunts()
        {
            var enemies = Project();

            Assert.That((enemies.Get("melee").Range, enemies.Get("ranged").Range > 0, enemies.Get("air").IsAir),
                Is.EqualTo((0.0, true, true)));
        }

        [Test]
        [Description("plan 5章: ドロップは 近 40%で1 / 遠 50%で1〜2 / 空 50%で1〜2")]
        public void Project_Grunt_Drops_Follow_Plan()
        {
            var enemies = Project();
            var drops = new[] { "melee", "ranged", "air" };
            var actual = new (double, int, int)[drops.Length];
            for (var i = 0; i < drops.Length; i++)
            {
                var enemy = enemies.Get(drops[i]);
                actual[i] = (enemy.DropRate, enemy.DropMin, enemy.DropMax);
            }

            Assert.That(actual, Is.EqualTo(new[] { (0.4, 1, 1), (0.5, 1, 2), (0.5, 1, 2) }));
        }
    }
}
