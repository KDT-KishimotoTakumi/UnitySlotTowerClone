using System;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// フィールド上の敵1体。経路のマスの並びを、マス中心を結ぶ折れ線として一定速度で進む（plan 1章）。
    /// 位置はマス単位の連続値で、マス (x, y) の中心が (x, y)。
    /// </summary>
    public sealed class BattleEnemy
    {
        internal BattleEnemy(EnemyDefinition definition, MapPath path)
        {
            Definition = definition;
            Path = path;
            Hp = definition.MaxHp;
            X = path.Cells[0].X;
            Y = path.Cells[0].Y;
        }

        public EnemyDefinition Definition { get; }
        public MapPath Path { get; }
        public int Hp { get; private set; }

        /// <summary>出現口から進んだ距離（マス）。</summary>
        public double Distance { get; private set; }

        public double X { get; private set; }
        public double Y { get; private set; }

        /// <summary>経路の終点（ゴールのマス中心）に着いた。</summary>
        public bool ReachedGoal => Distance >= Path.Cells.Count - 1 - Epsilon;

        public bool IsAlive => Hp > 0;

        /// <summary>ゴールまでの残りの道のり（マス）。味方はこれが最も短い敵を狙う。</summary>
        public double RemainingDistance => Path.Cells.Count - 1 - Distance;

        /// <summary>中心が入っているマス。境界ちょうどなら座標の大きい側。</summary>
        public GridPoint Cell =>
            new GridPoint((int)Math.Floor(X + 0.5 + Epsilon), (int)Math.Floor(Y + 0.5 + Epsilon));

        /// <summary>この敵をブロックしている味方。ブロックされていなければ null。</summary>
        public BattleUnit BlockedBy { get; internal set; }

        /// <summary>次に攻撃できるまでの残りステップ数。</summary>
        internal int AttackCooldownSteps { get; set; }

        /// <summary>次に範囲攻撃できるまでの残りステップ数。出現から1間隔待ってから撃つ。</summary>
        internal int AreaCooldownSteps { get; set; }

        // 1/60 秒ぶんの移動を足し続けたときの丸め誤差の許容幅。
        private const double Epsilon = 1e-9;

        /// <summary>経路に沿って進め、位置を更新する。ゴールを越えては進まない。</summary>
        internal void Move(double distance)
        {
            var length = Path.Cells.Count - 1;
            Distance = Math.Min(Distance + distance, length);
            var index = Math.Min((int)(Distance + Epsilon), length - 1);
            var t = Math.Min(Math.Max(Distance - index, 0), 1);
            var from = Path.Cells[index];
            var to = Path.Cells[index + 1];
            X = from.X + (to.X - from.X) * t;
            Y = from.Y + (to.Y - from.Y) * t;
        }

        internal void TakeDamage(int damage)
        {
            Hp = Hp > damage ? Hp - damage : 0;
        }

        internal double DistanceTo(GridPoint cell)
        {
            var dx = cell.X - X;
            var dy = cell.Y - Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
