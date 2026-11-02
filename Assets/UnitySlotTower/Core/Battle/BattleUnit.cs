using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>フィールドに配置された味方1体。ステータスは種類の強化段階に合わせる（FR-026）。</summary>
    public sealed class BattleUnit
    {
        internal BattleUnit(UnitDefinition definition, GridPoint cell, Facing facing, IReadOnlyList<GridPoint> rangeCells,
            UnitUpgradeLevel upgrade)
        {
            Definition = definition;
            Cell = cell;
            Facing = facing;
            RangeCells = rangeCells;
            ApplyUpgrade(upgrade);
            Hp = MaxHp;
        }

        public UnitDefinition Definition { get; }

        /// <summary>強化後の最大 HP・攻撃力・防御力（FR-026）。</summary>
        public int MaxHp { get; private set; }

        public int Atk { get; private set; }
        public int Def { get; private set; }

        public GridPoint Cell { get; }
        public Facing Facing { get; }

        /// <summary>向きに合わせて回した攻撃範囲のマス（マップ外も含む）。</summary>
        public IReadOnlyList<GridPoint> RangeCells { get; }

        public int Hp { get; private set; }
        public bool IsAlive => Hp > 0;

        /// <summary>ブロック中の敵のブロック消費の合計。<see cref="UnitDefinition.Block"/> を超えない。</summary>
        public int BlockUsed { get; internal set; }

        /// <summary>次に攻撃（回復）できるまでの残りステップ数。</summary>
        internal int AttackCooldownSteps { get; set; }

        internal bool InRange(GridPoint cell)
        {
            for (var i = 0; i < RangeCells.Count; i++)
            {
                if (RangeCells[i].Equals(cell))
                {
                    return true;
                }
            }

            return false;
        }

        internal void TakeDamage(int damage)
        {
            Hp = Hp > damage ? Hp - damage : 0;
        }

        /// <summary>最大 HP を超えては回復しない。</summary>
        internal void Heal(int amount)
        {
            Hp = Math.Min(Hp + amount, MaxHp);
        }

        /// <summary>基本ステータスに段階の倍率をかけ直す（端数は四捨五入）。最大 HP が増えた分は現在 HP にも足す（plan 5章）。</summary>
        internal void ApplyUpgrade(UnitUpgradeLevel upgrade)
        {
            var maxHp = Scale(Definition.MaxHp, upgrade.HpMult);
            Hp += maxHp - MaxHp;
            MaxHp = maxHp;
            Atk = Scale(Definition.Atk, upgrade.AtkMult);
            Def = Scale(Definition.Def, upgrade.DefMult);
        }

        private static int Scale(int value, double rate) =>
            (int)Math.Round(value * rate, MidpointRounding.AwayFromZero);
    }
}
