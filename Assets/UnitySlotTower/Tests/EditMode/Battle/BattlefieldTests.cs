using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// 敵の出現・経路移動・空中移動・射程攻撃（FR-030〜032）。1 ステップ = 1/60 秒。
    /// 地図と敵は <see cref="BattleFixtures"/>（地上経路 g: (0,0)→(3,0)、空中経路 a: (0,1)→(2,1)→(2,0)→(3,0)）。
    /// </summary>
    public sealed class BattlefieldTests
    {
        private static Battlefield Create(string waveRows) => BattleFixtures.CreateField(waveRows);

        private static BattleUnit Place(Battlefield field, string unitId, int x, int y) =>
            field.AddUnit(BattleFixtures.LoadUnits().Get(unitId), new GridPoint(x, y), Facing.Right);

        private static double[] Position(BattleEnemy enemy) => new[] { enemy.X, enemy.Y };

        [Test]
        public void Enemy_Appears_At_Its_Spawn_Time()
        {
            var field = Create("1,0,melee,S1,g,0\n1,2,ranged,S1,g,0\n");

            field.Advance(120);
            var before = field.Enemies.Count;
            field.Advance(1);

            Assert.That((before, field.Enemies.Count), Is.EqualTo((1, 2)));
        }

        [Test]
        public void Ground_Enemy_Moves_Along_Its_Path_At_Its_Speed()
        {
            var field = Create("1,0,melee,S1,g,0\n");

            field.Advance(90);

            Assert.That(Position(field.Enemies[0]), Is.EqualTo(new[] { 1.5, 0.0 }).Within(1e-6));
        }

        [Test]
        [Description("FR-031: 空中の敵は高台・通行不可マスの上を飛ぶ")]
        public void Air_Enemy_Flies_Over_Blocked_Tiles()
        {
            var field = Create("1,0,air,S2,a,0\n");

            field.Advance(60);

            Assert.That(Position(field.Enemies[0]), Is.EqualTo(new[] { 2.0, 1.0 }).Within(1e-6));
        }

        [Test]
        public void Enemy_Turns_At_A_Path_Corner()
        {
            var field = Create("1,0,air,S2,a,0\n");

            field.Advance(75);

            Assert.That(Position(field.Enemies[0]), Is.EqualTo(new[] { 2.0, 0.5 }).Within(1e-6));
        }

        [Test]
        public void Enemy_Short_Of_The_Goal_Stays_On_The_Field()
        {
            var field = Create("1,0,melee,S1,g,0\n");

            field.Advance(170);

            Assert.That((field.Enemies.Count, field.ArrivedCount), Is.EqualTo((1, 0)));
        }

        [Test]
        public void Enemy_At_The_Goal_Leaves_And_Is_Counted()
        {
            var field = Create("1,0,melee,S1,g,0\n");

            field.Advance(181);

            Assert.That((field.Enemies.Count, field.ArrivedCount), Is.EqualTo((0, 1)));
        }

        [Test]
        [Description("FR-032: 遠距離雑魚は射程内の高台の味方を攻撃する")]
        public void Ranged_Enemy_Attacks_A_High_Ground_Unit_In_Range()
        {
            var field = Create("1,0,ranged,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);

            field.Advance(1);

            Assert.That(archer.Hp, Is.EqualTo(700 - (300 - 50)));
        }

        [Test]
        public void Ranged_Enemy_Attacks_Again_After_Its_Interval()
        {
            var field = Create("1,0,ranged,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);

            field.Advance(120);
            var before = archer.Hp;
            field.Advance(1);

            Assert.That((before, archer.Hp), Is.EqualTo((450, 200)));
        }

        [Test]
        public void Unit_Out_Of_Range_Is_Not_Attacked()
        {
            var field = Create("1,0,ranged,S1,g,0\n");
            var archer = Place(field, "archer", 3, 1);

            field.Advance(60);

            Assert.That(archer.Hp, Is.EqualTo(700));
        }

        [Test]
        public void Ranged_Enemy_Targets_The_Nearest_Unit()
        {
            var field = Create("1,0,ranged,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);
            var tank = Place(field, "tank", 1, 0);

            field.Advance(1);

            Assert.That((archer.Hp, tank.Hp), Is.EqualTo((700, 1800 - 15)));
        }

        [Test]
        [Description("plan 5章: 遠距離雑魚は移動しながら攻撃する")]
        public void Ranged_Enemy_Keeps_Moving_While_Attacking()
        {
            var field = Create("1,0,ranged,S1,g,0\n");
            Place(field, "archer", 1, 1);

            field.Advance(60);

            Assert.That(Position(field.Enemies[0]), Is.EqualTo(new[] { 0.5, 0.0 }).Within(1e-6));
        }

        [Test]
        [Description("近距離雑魚はブロックした味方だけを攻撃する")]
        public void Melee_Enemy_Does_Not_Attack_Units_It_Passes()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            var tank = Place(field, "tank", 1, 1);

            field.Advance(150);

            Assert.That(tank.Hp, Is.EqualTo(1800));
        }

        [Test]
        public void Air_Enemy_Attacks_Units_In_Range()
        {
            var field = Create("1,0,air,S2,a,0\n");
            var archer = Place(field, "archer", 1, 1);

            field.Advance(1);

            Assert.That(archer.Hp, Is.EqualTo(700 - (150 - 50)));
        }

        [Test]
        [Description("FR-027: ブロックされた地上の敵は味方のマスの手前の境界で止まる")]
        public void Blocked_Ground_Enemy_Stops_At_The_Edge_Of_The_Unit_Cell()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "tank", 2, 0);

            field.Advance(240);

            Assert.That(Position(field.Enemies[0]), Is.EqualTo(new[] { 1.5, 0.0 }).Within(1e-6));
        }

        [Test]
        [Description("plan 5章: 遠距離雑魚はブロックされると停止する")]
        public void Blocked_Ranged_Enemy_Stops()
        {
            var field = Create("1,0,ranged,S1,g,0\n");
            Place(field, "tank", 1, 0);

            field.Advance(180);

            Assert.That(Position(field.Enemies[0]), Is.EqualTo(new[] { 0.5, 0.0 }).Within(1e-6));
        }

        [Test]
        [Description("AC-021: タンクはブロック数3まで止め、4体目は通す")]
        public void Unit_Blocks_No_More_Enemies_Than_Its_Block_Count()
        {
            var field = Create("1,0,melee,S1,g,0\n1,0.5,melee,S1,g,0\n1,1,melee,S1,g,0\n1,1.5,melee,S1,g,0\n");
            Place(field, "tank", 2, 0);

            field.Advance(300);

            Assert.That((field.Enemies.Count, field.ArrivedCount), Is.EqualTo((3, 1)));
        }

        [Test]
        [Description("FR-027: ブロック消費2の敵はブロック数を2使う")]
        public void Heavy_Enemy_Uses_Two_Of_The_Block_Count()
        {
            var field = Create("1,0,heavy,S1,g,0\n1,0.5,melee,S1,g,0\n1,1,melee,S1,g,0\n");
            Place(field, "tank", 2, 0);

            field.Advance(300);

            Assert.That((field.Enemies.Count, field.ArrivedCount), Is.EqualTo((2, 1)));
        }

        [Test]
        [Description("FR-027: 空きが消費に足りない敵は通し、後から来た足りる敵は止める")]
        public void Enemy_Costing_More_Than_The_Free_Block_Passes()
        {
            var field = Create("1,0,heavy,S1,g,0\n1,0.5,melee,S1,g,0\n");
            Place(field, "sword", 2, 0);

            field.Advance(240);

            Assert.That((field.Enemies.Count, field.ArrivedCount), Is.EqualTo((1, 1)));
        }

        [Test]
        [Description("FR-031: 空中の敵はブロックされない")]
        public void Air_Enemy_Is_Not_Blocked()
        {
            var field = Create("1,0,air,S2,a,0\n");
            Place(field, "tank", 2, 0);

            field.Advance(150);

            Assert.That(field.ArrivedCount, Is.EqualTo(1));
        }

        [Test]
        [Description("plan 5章: 近距離雑魚はブロックされると、ブロックした味方を攻撃する")]
        public void Blocked_Melee_Enemy_Attacks_The_Blocking_Unit()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            var sword = Place(field, "sword", 1, 0);

            field.Advance(31);

            Assert.That(sword.Hp, Is.EqualTo(1200 - (200 - 50)));
        }

        [Test]
        public void Blocked_Melee_Enemy_Attacks_Again_After_Its_Interval()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            var sword = Place(field, "sword", 1, 0);

            field.Advance(125);

            Assert.That(sword.Hp, Is.EqualTo(1200 - 2 * (200 - 50)));
        }

        [Test]
        public void Enemy_Already_Inside_A_Newly_Placed_Unit_Cell_Is_Blocked()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            field.Advance(60);
            Place(field, "tank", 1, 0);

            field.Advance(60);

            Assert.That(Position(field.Enemies[0]), Is.EqualTo(new[] { 1.0, 0.0 }).Within(1e-6));
        }

        [Test]
        [Description("FR-025: 撤退した味方がブロックしていた敵は再び進む")]
        public void Enemy_Blocked_By_A_Retreated_Unit_Moves_On()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            var tank = Place(field, "tank", 2, 0);
            field.Advance(240);

            field.RemoveUnit(tank);
            field.Advance(60);

            Assert.That(field.Enemies[0].X, Is.GreaterThan(1.5));
        }
    }
}
