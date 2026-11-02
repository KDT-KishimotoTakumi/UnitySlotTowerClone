using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// ゴール到達でのライフ減少・ゲームオーバー（FR-013）と撃破ドロップ（FR-033）。1 ステップ = 1/60 秒。
    /// 地図と敵は <see cref="BattleFixtures"/>（地上経路 g: (0,0)→(3,0)。速度1の敵は出現から 180 ステップでゴールに着く）。
    /// </summary>
    public sealed class LifeAndDropTests
    {
        [Test]
        [Description("FR-013: ゴールに着いた敵のライフ減少量だけライフが減る")]
        public void Life_Drops_By_The_Arrived_Enemys_Life_Damage()
        {
            var field = BattleFixtures.CreateField("1,0,heavy,S1,g,0\n");

            field.Advance(181);

            Assert.That(field.Life, Is.EqualTo(8));
        }

        [Test]
        [Description("AC-010: ライフ0でゲームオーバー。減少量が残りより多くても0で止まる")]
        public void Game_Is_Over_When_Life_Reaches_Zero()
        {
            var field = BattleFixtures.CreateField("1,0,heavy,S1,g,0\n", life: 1);

            field.Advance(181);

            Assert.That((field.Life, field.IsGameOver), Is.EqualTo((0, true)));
        }

        [Test]
        public void Battle_Stops_After_Game_Over()
        {
            var field = BattleFixtures.CreateField("1,0,melee,S1,g,0\n1,0.5,melee,S1,g,0\n", life: 1);

            field.Advance(180 + 60);

            Assert.That(field.Tick, Is.EqualTo(180));
        }

        /// <summary>
        /// 1 体だけ出る敵を剣士（(1,0) 右向き）に倒させる戦闘を、1 つの固定シード乱数で何度も行い、各戦闘のドロップ合計を返す。
        /// 敵は HP1 で、出現から 30 ステップで剣士の範囲に入り倒される。
        /// </summary>
        private static int[] DropsOfDefeats(string enemyId, int trials)
        {
            var wave = BattleFixtures.LoadWave($"1,0,{enemyId},S1,g,0\n");
            var ranges = BattleFixtures.LoadRanges();
            var sword = BattleFixtures.LoadUnits().Get("sword");
            var random = new SeededRandom(42);
            var drops = new int[trials];
            for (var i = 0; i < trials; i++)
            {
                var field = new Battlefield(wave, ranges, 10, random);
                field.AddUnit(sword, new GridPoint(1, 0), Facing.Right);
                field.Advance(60);
                drops[i] = field.DroppedCoins;
            }

            return drops;
        }

        [Test]
        [Description("AC-030: 40% で 1 枚落とす敵を 2000 回倒すと、落とす割合は 40% ± 4%（標準偏差の約 3.6 倍）")]
        public void Defeated_Enemy_Drops_Coins_At_Its_Drop_Rate()
        {
            var drops = DropsOfDefeats("fodder", 2000);

            Assert.That(drops.Sum() / 2000.0, Is.EqualTo(0.4).Within(0.04));
        }

        [Test]
        [Description("AC-030: 必ず 2〜3 枚落とす敵を 200 回倒すと、量は 2 と 3 だけが出る")]
        public void Drop_Amount_Ranges_From_Min_To_Max()
        {
            var drops = DropsOfDefeats("rich", 200);

            Assert.That(drops.Distinct().OrderBy(d => d), Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        [Description("FR-033: ドロップは撃破時だけ。ゴールに着いた敵は落とさない")]
        public void Enemy_Reaching_The_Goal_Drops_Nothing()
        {
            var field = BattleFixtures.CreateField("1,0,rich,S1,g,0\n");

            field.Advance(180);

            Assert.That((field.ArrivedCount, field.DroppedCoins), Is.EqualTo((1, 0)));
        }
    }
}
