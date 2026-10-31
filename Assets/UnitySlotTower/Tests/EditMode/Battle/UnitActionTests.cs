using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// 味方の攻撃・HP0 での消滅（FR-024, FR-031）。1 ステップ = 1/60 秒。
    /// 地図・敵・味方・範囲は <see cref="BattleFixtures"/>（地上経路 g: (0,0)→(3,0)、空中経路 a: (0,1)→(2,1)→(2,0)→(3,0)）。
    /// 敵は中心が入っているマスで範囲に入る。
    /// </summary>
    public sealed class UnitActionTests
    {
        private static Battlefield Create(string waveRows) => BattleFixtures.CreateField(waveRows);

        private static BattleUnit Place(Battlefield field, string unitId, int x, int y,
            Facing facing = Facing.Right) =>
            field.AddUnit(BattleFixtures.LoadUnits().Get(unitId), new GridPoint(x, y), facing);

        [Test]
        [Description("plan 5章: ダメージ = max(攻撃力 − 防御力, 攻撃力 × 0.05)")]
        public void Unit_Attacks_An_Enemy_In_Its_Range()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (350 - 50)));
        }

        [Test]
        public void Enemy_Outside_The_Range_Is_Not_Attacked()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);

            field.Advance(25);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000));
        }

        [Test]
        [Description("plan 5章: 攻撃対象は範囲内でゴールに最も近い敵")]
        public void Unit_Targets_The_Enemy_Closest_To_The_Goal()
        {
            var field = Create("1,0,melee,S1,g,0\n1,0.5,melee,S1,g,0\n");
            field.Advance(75);
            Place(field, "archer", 1, 1);

            field.Advance(1);

            Assert.That((field.Enemies[0].Hp, field.Enemies[1].Hp), Is.EqualTo((700, 1000)));
        }

        [Test]
        [Description("FR-031: 弓は空中の敵を攻撃できる")]
        public void Archer_Attacks_An_Air_Enemy()
        {
            var field = Create("1,0,air,S2,a,0\n");
            Place(field, "archer", 1, 1);

            field.Advance(20);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(600 - (350 - 20)));
        }

        [Test]
        [Description("FR-031: 弓以外は空中の敵を攻撃しない")]
        public void Tank_Does_Not_Attack_An_Air_Enemy()
        {
            var field = Create("1,0,air,S2,a,0\n");
            Place(field, "tank", 2, 0);

            field.Advance(85);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(600));
        }

        [Test]
        [Description("plan 5章: 剣士とタンクはブロック中の敵を優先する")]
        public void Melee_Unit_Prefers_The_Enemy_It_Blocks()
        {
            // heavy（消費2）は剣士（ブロック1）を通り過ぎ、後の melee がブロックされる。
            var field = Create("1,0,heavy,S1,g,0\n1,0.5,melee,S1,g,0\n");
            Place(field, "sword", 1, 0);

            field.Advance(100);

            Assert.That((field.Enemies[0].Hp, field.Enemies[1].Hp), Is.EqualTo((2000 - 350, 1000 - 350)));
        }

        [Test]
        public void Unit_Attacks_Again_After_Its_Interval()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "sword", 1, 0);

            field.Advance(90);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - 2 * 350));
        }

        [Test]
        [Description("FR-024: HP0 の敵は消える（ゴール到着には数えない）")]
        public void Enemy_At_Zero_Hp_Disappears()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "sword", 1, 0);

            field.Advance(150);

            Assert.That((field.Enemies.Count, field.ArrivedCount), Is.EqualTo((0, 0)));
        }

        [Test]
        [Description("倒した敵のぶんブロックの空きが戻る")]
        public void Defeated_Enemy_Frees_The_Block()
        {
            var field = Create("1,0,melee,S1,g,0\n1,3,melee,S1,g,0\n");
            Place(field, "sword", 1, 0);
            field.Advance(181);
            var second = field.Enemies[field.Enemies.Count - 1];

            field.Advance(59);

            Assert.That(second.X, Is.EqualTo(0.5).Within(1e-6));
        }

        [Test]
        [Description("FR-024: HP0 の味方は消滅する")]
        public void Unit_At_Zero_Hp_Disappears()
        {
            var field = Create("1,0,brute,S1,g,0\n");
            Place(field, "sword", 1, 0);

            field.Advance(100);

            Assert.That(field.Units.Count, Is.EqualTo(0));
        }

        [Test]
        [Description("FR-024: 消滅した味方がブロックしていた敵は再び進む")]
        public void Enemy_Blocked_By_A_Fallen_Unit_Moves_On()
        {
            var field = Create("1,0,brute,S1,g,0\n");
            Place(field, "sword", 1, 0);

            field.Advance(150);

            Assert.That(field.Enemies[0].X, Is.EqualTo(1.5).Within(1e-6));
        }

        [Test]
        [Description("AC-022: ヒーラーは範囲内で HP 割合が最も低い味方を回復する（HP の値ではなく割合）")]
        public void Healer_Heals_The_Ally_With_The_Lowest_Hp_Ratio()
        {
            // 0 ステップ目に ranged が弓を撃ち 450/700、89 ステップ目に brute がタンクを打って 1100/1800。
            var field = Create("1,0,ranged,S1,g,0\n1,0,brute,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);
            var tank = Place(field, "tank", 2, 0);
            field.Advance(90);
            Place(field, "healer", 2, 1, Facing.Left);

            field.Advance(1);

            Assert.That((tank.Hp, archer.Hp), Is.EqualTo((1100 + 200, 450)));
        }

        [Test]
        public void Heal_Does_Not_Exceed_Max_Hp()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            var sword = Place(field, "sword", 1, 0);
            field.Advance(30);
            Place(field, "healer", 1, 1);

            field.Advance(1);

            Assert.That(sword.Hp, Is.EqualTo(1200));
        }

        [Test]
        [Description("FR-028: ヒーラーは敵を攻撃しない")]
        public void Healer_Does_Not_Attack_Enemies()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "healer", 1, 1);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000));
        }

        [Test]
        [Description("AC-022: バッファーは範囲内の味方の攻撃力に倍率をかける")]
        public void Buffer_Raises_The_Attack_Of_Allies_In_Range()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);
            Place(field, "buffer", 0, 1);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (525 - 50)));
        }

        [Test]
        [Description("plan 5章: 強化は重ねがけせず、最も高い倍率だけが効く")]
        public void Only_The_Highest_Buff_Applies()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);
            Place(field, "weak_buffer", 0, 1);
            Place(field, "buffer", 0, 1);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (525 - 50)));
        }
    }
}
