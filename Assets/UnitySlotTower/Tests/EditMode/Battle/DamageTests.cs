using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("plan 5章: max(攻撃力 − 防御力, 攻撃力 × 0.05)")]
    public sealed class DamageTests
    {
        [Test]
        public void Damage_Is_Attack_Minus_Defense()
        {
            Assert.That(Damage.Calculate(400, 150), Is.EqualTo(250));
        }

        [Test]
        public void Damage_Is_At_Least_5_Percent_Of_Attack()
        {
            Assert.That(Damage.Calculate(200, 300), Is.EqualTo(10));
        }

        [Test]
        public void Fractional_Minimum_Damage_Is_Rounded_Half_Up()
        {
            Assert.That(Damage.Calculate(150, 300), Is.EqualTo(8));
        }
    }
}
