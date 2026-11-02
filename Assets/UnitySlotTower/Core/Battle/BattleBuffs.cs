using System;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// スロットのバフ役による味方全員の強化（spec 002 FR-111〜FR-113）。攻撃力上昇と攻撃速度上昇は別々に数え、
    /// 効果中に揃え直すと残り時間が最初に戻る（倍率は重ならない）。残り時間は <see cref="Advance"/> を呼んだ分だけ減る
    /// （戦闘中だけ呼ぶ）。HP回復は揃った瞬間の1回だけ（<see cref="HealRate"/>、FR-114）。ウェーブをまたいで同じものを使う。
    /// </summary>
    public sealed class BattleBuffs
    {
        /// <summary>slot_payouts.csv のバフ役の ID。</summary>
        public const string AttackRole = "attack";
        public const string SpeedRole = "speed";
        public const string HealRole = "heal";

        private readonly int _attackSteps;
        private readonly int _speedSteps;

        /// <param name="attackRate">攻撃力上昇の倍率（強化の倍率と掛け合わせる）。</param>
        /// <param name="speedIntervalRate">攻撃速度上昇で攻撃間隔にかける倍率（1 未満で短くなる）。</param>
        /// <param name="healRate">HP回復で最大 HP にかける割合。</param>
        public BattleBuffs(double attackRate, double attackSeconds, double speedIntervalRate, double speedSeconds,
            double healRate)
        {
            AttackRate = attackRate;
            SpeedIntervalRate = speedIntervalRate;
            HealRate = healRate;
            _attackSteps = StepsOf(attackSeconds);
            _speedSteps = StepsOf(speedSeconds);
        }

        /// <summary>効果なし（倍率1）。</summary>
        public static BattleBuffs None => new BattleBuffs(1, 0, 1, 0, 0);

        public double AttackRate { get; }
        public double SpeedIntervalRate { get; }
        public double HealRate { get; }

        public int AttackStepsLeft { get; private set; }
        public int SpeedStepsLeft { get; private set; }

        public bool IsAttackActive => AttackStepsLeft > 0;
        public bool IsSpeedActive => SpeedStepsLeft > 0;

        /// <summary>攻撃力上昇の残り時間の割合（0〜1、ゲージ表示用）。</summary>
        public float AttackRemaining => _attackSteps > 0 ? (float)AttackStepsLeft / _attackSteps : 0f;

        /// <summary>攻撃速度上昇の残り時間の割合（0〜1、ゲージ表示用）。</summary>
        public float SpeedRemaining => _speedSteps > 0 ? (float)SpeedStepsLeft / _speedSteps : 0f;

        /// <summary>いまの攻撃力の倍率。</summary>
        public double CurrentAttackRate => IsAttackActive ? AttackRate : 1;

        /// <summary>いまの攻撃間隔の倍率。</summary>
        public double CurrentIntervalRate => IsSpeedActive ? SpeedIntervalRate : 1;

        public void StartAttack() => AttackStepsLeft = _attackSteps;
        public void StartSpeed() => SpeedStepsLeft = _speedSteps;

        public void Advance(int steps)
        {
            AttackStepsLeft = Math.Max(AttackStepsLeft - steps, 0);
            SpeedStepsLeft = Math.Max(SpeedStepsLeft - steps, 0);
        }

        private static int StepsOf(double seconds) => (int)Math.Round(seconds * SimulationClock.StepsPerSecond);
    }
}
