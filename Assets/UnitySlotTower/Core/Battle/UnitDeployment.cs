using System.Collections.Generic;

namespace UnitySlotTower.Core.Battle
{
    public enum DeployPhase
    {
        Idle,
        ChoosingCell,
        ChoosingFacing,
    }

    /// <summary>
    /// 召喚する味方を選び、マスと向きを決めて置くまでの操作の状態（FR-002, FR-023、plan 3章）。
    /// マウスとキーボードの入力はどちらもこの操作に変換する。支払いは向きを決めた時点で行う。
    /// </summary>
    public sealed class UnitDeployment
    {
        private readonly BattleMap _map;
        private readonly UnitTable _units;
        private readonly UnitRanges _ranges;
        private readonly UnitSummoner _summoner;
        private readonly Battlefield _field;
        private readonly UnitUpgrades _upgrades;

        private bool _cursorPlaced;

        public UnitDeployment(BattleMap map, UnitTable units, UnitRanges ranges, UnitSummoner summoner, Battlefield field,
            UnitUpgrades upgrades)
        {
            _map = map;
            _units = units;
            _ranges = ranges;
            _summoner = summoner;
            _field = field;
            _upgrades = upgrades;
            // 戦闘と同じ寿命なので解除しない。
            _field.UnitFell += _ => _summoner.ReleaseFallen();
        }

        public DeployPhase Phase { get; private set; }

        /// <summary>選択中の味方。<see cref="DeployPhase.Idle"/> では null。</summary>
        public UnitDefinition Selected { get; private set; }

        public GridPoint Cursor { get; private set; }

        public Facing Facing { get; private set; }

        /// <summary>
        /// 召喚できる味方ならマス選びを始める（どの段階からでも選び直せる）。召喚できなければ何も変えず理由を返す。
        /// カーソルは最初だけマップ中央に置き、以後は前回の位置を引き継ぐ。
        /// </summary>
        public SummonResult Select(string unitId)
        {
            var result = _summoner.Check(unitId);
            if (result != SummonResult.Summoned)
            {
                return result;
            }

            if (!_cursorPlaced)
            {
                Cursor = new GridPoint(_map.Width / 2, _map.Height / 2);
                _cursorPlaced = true;
            }

            Selected = _units.Get(unitId);
            Phase = DeployPhase.ChoosingCell;
            Facing = Facing.Right;
            return result;
        }

        /// <summary>
        /// 矢印キー1回分（dy は負が画面の上）。マス選びではカーソルを動かし（マップの端で止まる）、向き選びではその方向を向く。
        /// </summary>
        public void MoveCursor(int dx, int dy)
        {
            if (Phase == DeployPhase.ChoosingCell)
            {
                SetCursor(new GridPoint(Clamp(Cursor.X + dx, _map.Width), Clamp(Cursor.Y + dy, _map.Height)));
            }
            else if (Phase == DeployPhase.ChoosingFacing && (dx != 0 || dy != 0))
            {
                SetFacing(dx > 0 ? Facing.Right : dx < 0 ? Facing.Left : dy > 0 ? Facing.Down : Facing.Up);
            }
        }

        /// <summary>マス選びでだけ効く。マップ外は無視する。</summary>
        public void SetCursor(GridPoint cell)
        {
            if (Phase == DeployPhase.ChoosingCell && _map.Contains(cell))
            {
                Cursor = cell;
            }
        }

        /// <summary>選択中の味方を置けるマスか（マスの種類と、ほかの味方が居ないこと）。</summary>
        public bool CanPlace(GridPoint cell)
        {
            if (Selected == null || !UnitPlacement.CanPlace(_map, cell, Selected))
            {
                return false;
            }

            foreach (var unit in _field.Units)
            {
                if (unit.Cell.Equals(cell))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>カーソルのマスに決めて向き選びへ進む。置けないマスなら false でそのまま。</summary>
        public bool ChooseCell()
        {
            if (Phase != DeployPhase.ChoosingCell || !CanPlace(Cursor))
            {
                return false;
            }

            Phase = DeployPhase.ChoosingFacing;
            return true;
        }

        /// <summary>向き選びでだけ効く。</summary>
        public void SetFacing(Facing facing)
        {
            if (Phase == DeployPhase.ChoosingFacing)
            {
                Facing = facing;
            }
        }

        /// <summary>選択中の味方をカーソルのマスに今の向きで置いたときの攻撃範囲（マップ外を含む）。選択なしなら空。</summary>
        public IReadOnlyList<GridPoint> PreviewRange =>
            Selected == null ? (IReadOnlyList<GridPoint>)new GridPoint[0] : _ranges.CellsFrom(Selected.Id, Cursor, Facing);

        /// <summary>
        /// 向き選びで、支払って置く。召喚できなくなっていたら（向き選びの間にコインが減ったなど）置かずに理由を返す。
        /// どちらでも操作は終わる。向き選び以外では何もしない。
        /// </summary>
        public SummonResult Confirm()
        {
            if (Phase != DeployPhase.ChoosingFacing)
            {
                return SummonResult.Summoned;
            }

            var result = _summoner.TrySummon(Selected.Id);
            if (result == SummonResult.Summoned)
            {
                _field.AddUnit(Selected, Cursor, Facing, _upgrades.Current(Selected.Id));
            }

            Clear();
            return result;
        }

        /// <summary>配置中の味方を撤退させ、返還する（FR-025）。フィールドに居なければ何もしない。</summary>
        public void Retreat(BattleUnit unit)
        {
            if (_field.RemoveUnit(unit))
            {
                _summoner.Retreat(unit.Definition.Id);
            }
        }

        /// <summary>
        /// その種類を1段階強化する（FR-026。戦闘中いつでも、配置操作の途中でもよい）。配置中の同種の全個体にすぐ反映し、
        /// 以後に置く個体もこの段階で置く。拒否したときは何も変えない。
        /// </summary>
        public UpgradeResult Upgrade(string unitId)
        {
            var result = _upgrades.TryUpgrade(unitId);
            if (result == UpgradeResult.Upgraded)
            {
                _field.ApplyUpgrade(unitId, _upgrades.Current(unitId));
            }

            return result;
        }

        /// <summary>1段階戻す（向き選び→マス選び→選択なし）。</summary>
        public void Cancel()
        {
            if (Phase == DeployPhase.ChoosingFacing)
            {
                Phase = DeployPhase.ChoosingCell;
                return;
            }

            Clear();
        }

        private void Clear()
        {
            Selected = null;
            Phase = DeployPhase.Idle;
            Facing = Facing.Right;
        }

        private static int Clamp(int value, int size) => value < 0 ? 0 : value >= size ? size - 1 : value;
    }
}
