using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// スロットのバフ役による味方全員の強化（spec 002 FR-111〜FR-114、AC-111・AC-112）。1 ステップ = 1/60 秒。
    /// 地図・敵・味方は <see cref="BattleFixtures"/>。弓（攻撃350・間隔1秒）は (1,1) から地上経路の (1,0)(2,0) を狙う。
    /// </summary>
    public sealed class BattleBuffTests
    {
        // 攻撃力×1.5・1秒、攻撃間隔×0.5・1秒、HP回復30%。
        private static BattleBuffs Buffs() => new BattleBuffs(1.5, 1, 0.5, 1, 0.3);

        private static BattleUnit Place(Battlefield field, string unitId, int x, int y,
            UnitUpgradeLevel upgrade = null) =>
            field.AddUnit(BattleFixtures.LoadUnits().Get(unitId), new GridPoint(x, y), Facing.Right, upgrade);

        [Test]
        [Description("FR-111: 攻撃力上昇の効果中は配置中の味方の攻撃力に倍率がかかる")]
        public void Attack_Buff_Multiplies_The_Attack()
        {
            var buffs = Buffs();
            var field = BattleFixtures.CreateField("1,0,melee,S1,g,0\n", buffs: buffs);
            Place(field, "archer", 1, 1);
            buffs.StartAttack();

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (525 - 50)));
        }

        [Test]
        [Description("FR-111: 効果中に新しく置いた味方にも効く")]
        public void Attack_Buff_Applies_To_Units_Placed_During_The_Effect()
        {
            var buffs = new BattleBuffs(1.5, 10, 1, 0, 0);
            var field = BattleFixtures.CreateField("1,0,melee,S1,g,0\n", buffs: buffs);
            buffs.StartAttack();
            field.Advance(10);
            Place(field, "archer", 1, 1);

            field.Advance(35);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (525 - 50)));
        }

        [Test]
        [Description("FR-111: 強化の倍率と掛け合わせる（弓の段階2は攻撃×2）")]
        public void Attack_Buff_Is_Multiplied_With_The_Upgrade()
        {
            var buffs = new BattleBuffs(1.2, 10, 1, 0, 0);
            var field = BattleFixtures.CreateField("1,0,melee,S1,g,0\n", buffs: buffs);
            Place(field, "archer", 1, 1, BattleFixtures.LoadUpgrades().Get("archer", 2));
            buffs.StartAttack();

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (840 - 50)));
        }

        [Test]
        [Description("FR-112: 攻撃速度上昇の効果中は次の攻撃までの間隔が倍率どおり短くなる")]
        public void Speed_Buff_Shortens_The_Attack_Interval()
        {
            var buffs = Buffs();
            var field = BattleFixtures.CreateField("1,0,melee,S1,g,0\n", buffs: buffs);
            Place(field, "archer", 1, 1);
            buffs.StartSpeed();

            field.Advance(75);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - 2 * (350 - 50)));
        }

        [Test]
        public void Without_Buffs_The_Interval_Is_Unchanged()
        {
            var field = BattleFixtures.CreateField("1,0,melee,S1,g,0\n", buffs: Buffs());
            Place(field, "archer", 1, 1);

            field.Advance(75);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (350 - 50)));
        }

        [Test]
        [Description("FR-113: 秒数が過ぎると戻る")]
        public void Buff_Ends_After_Its_Seconds()
        {
            var buffs = Buffs();
            buffs.StartAttack();

            buffs.Advance(59);
            var activeBefore = buffs.IsAttackActive;
            buffs.Advance(1);

            Assert.That((activeBefore, buffs.IsAttackActive, buffs.CurrentAttackRate), Is.EqualTo((true, false, 1.0)));
        }

        [Test]
        [Description("FR-113: 効果中に揃え直すと残り時間が最初に戻り、倍率は重ならない")]
        public void Restarting_Resets_The_Remaining_Time_Without_Stacking()
        {
            var buffs = Buffs();
            buffs.StartAttack();
            buffs.Advance(30);

            buffs.StartAttack();

            Assert.That((buffs.AttackStepsLeft, buffs.AttackRemaining, buffs.CurrentAttackRate),
                Is.EqualTo((60, 1f, 1.5)));
        }

        [Test]
        [Description("FR-113: 攻撃力上昇と攻撃速度上昇は別々に数える")]
        public void Attack_And_Speed_Are_Counted_Separately()
        {
            var buffs = new BattleBuffs(1.5, 1, 0.5, 2, 0.3);
            buffs.StartAttack();
            buffs.Advance(30);
            buffs.StartSpeed();
            buffs.Advance(40);

            Assert.That((buffs.IsAttackActive, buffs.IsSpeedActive, buffs.SpeedStepsLeft, buffs.CurrentIntervalRate),
                Is.EqualTo((false, true, 80, 0.5)));
        }

        [Test]
        [Description("FR-114: HP回復は配置中の味方全員を最大 HP の割合だけ回復する")]
        public void Heal_All_Restores_A_Share_Of_Max_Hp()
        {
            // 0 ステップ目に ranged が弓を撃ち 450/700、89 ステップ目に brute がタンクを打って 1100/1800。
            var field = BattleFixtures.CreateField("1,0,ranged,S1,g,0\n1,0,brute,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);
            var tank = Place(field, "tank", 2, 0);
            field.Advance(90);

            field.HealAll(0.3);

            Assert.That((archer.Hp, tank.Hp), Is.EqualTo((450 + 210, 1100 + 540)));
        }

        [Test]
        [Description("FR-114: 最大 HP を超えない")]
        public void Heal_All_Stops_At_Max_Hp()
        {
            var field = BattleFixtures.CreateField("1,0,ranged,S1,g,0\n1,0,brute,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);
            var tank = Place(field, "tank", 2, 0);
            field.Advance(90);

            field.HealAll(0.5);

            Assert.That((archer.Hp, tank.Hp), Is.EqualTo((700, 1800)));
        }
    }
}
