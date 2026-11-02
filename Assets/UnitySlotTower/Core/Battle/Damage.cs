using System;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>ダメージ式（plan 5章、アークナイツ準拠）: max(攻撃力 − 防御力, 攻撃力 × 0.05)。</summary>
    public static class Damage
    {
        public const double MinimumRate = 0.05;

        /// <summary>最低ダメージの端数は四捨五入する。</summary>
        public static int Calculate(int atk, int def) =>
            Math.Max(atk - def, (int)Math.Round(atk * MinimumRate, MidpointRounding.AwayFromZero));
    }
}
