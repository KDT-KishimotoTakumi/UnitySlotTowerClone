using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    public enum SummonResult
    {
        Summoned,
        CoolingDown,
        DeployLimitReached,
        NotEnoughCoins,
    }

    /// <summary>
    /// 召喚の可否判定と支払い（FR-020〜FR-022）。待ち時間は種類ごとで、召喚した瞬間から数える。
    /// 時間は <see cref="SimulationClock"/> の固定ステップで進める。
    /// </summary>
    public sealed class UnitSummoner
    {
        private readonly UnitTable _units;
        private readonly CoinWallet _wallet;
        private readonly int _deployLimit;
        private readonly double _refundRate;
        private readonly UnitStock _stock;
        private readonly Dictionary<string, int> _indices = new Dictionary<string, int>();

        // 種類ごとの残り待ちステップ数（UnitTable.All と同じ並び）。
        private readonly int[] _cooldownSteps;

        /// <param name="refundRate">撤退で召喚コストのうち返す割合（FR-025）。</param>
        /// <param name="stock">無料ストック（FR-016）。ある種類はコインを使わずストックから出す。null ならストックなし。</param>
        public UnitSummoner(UnitTable units, CoinWallet wallet, int deployLimit, double refundRate, UnitStock stock = null)
        {
            _units = units;
            _wallet = wallet;
            _deployLimit = deployLimit;
            _refundRate = refundRate;
            _stock = stock ?? new UnitStock();
            _cooldownSteps = new int[units.All.Count];
            for (var i = 0; i < units.All.Count; i++)
            {
                _indices.Add(units.All[i].Id, i);
            }
        }

        /// <summary>フィールド上の味方の総数。</summary>
        public int DeployedCount { get; private set; }

        /// <summary>準備中（plan C-3）。待ち時間で拒否せず、召喚しても待ち時間を始めない。</summary>
        public bool IgnoresCooldown { get; set; }

        /// <summary>いま召喚できるかだけを調べる（何も変えない）。召喚できるなら <see cref="SummonResult.Summoned"/>。</summary>
        public SummonResult Check(string unitId)
        {
            var unit = _units.Get(unitId);
            if (!IgnoresCooldown && _cooldownSteps[_indices[unitId]] > 0)
            {
                return SummonResult.CoolingDown;
            }

            if (DeployedCount >= _deployLimit)
            {
                return SummonResult.DeployLimitReached;
            }

            return _stock.Count(unitId) == 0 && _wallet.Coins < unit.Cost
                ? SummonResult.NotEnoughCoins
                : SummonResult.Summoned;
        }

        /// <summary>拒否したときは何も変えない（コイン・ストック・待ち時間・配置数）。ストックがあれば先に使う（plan 4章）。</summary>
        public SummonResult TrySummon(string unitId)
        {
            var result = Check(unitId);
            if (result != SummonResult.Summoned)
            {
                return result;
            }

            var unit = _units.Get(unitId);
            if (!_stock.TryTake(unitId))
            {
                _wallet.TrySpend(unit.Cost);
            }

            if (!IgnoresCooldown)
            {
                _cooldownSteps[_indices[unitId]] = (int)Math.Round(unit.Cooldown * SimulationClock.StepsPerSecond);
            }

            DeployedCount++;
            return SummonResult.Summoned;
        }

        /// <summary>
        /// 撤退した味方の分の返還（FR-025）。召喚コスト × 返還率を切り捨てて財布に戻し、返した額を返す。
        /// ストックから出した味方も同じく召喚コストを基準にする（plan C-3）。
        /// </summary>
        public int Retreat(string unitId)
        {
            var refund = (int)Math.Floor(_units.Get(unitId).Cost * _refundRate);
            _wallet.Add(refund);
            DeployedCount--;
            return refund;
        }

        /// <summary>倒された味方の分の配置枠を空ける（返還はない）。</summary>
        public void ReleaseFallen()
        {
            DeployedCount--;
        }

        /// <summary>その種類を再び召喚できるまでの残り秒数。召喚できるなら 0。</summary>
        public double CooldownRemaining(string unitId)
        {
            _units.Get(unitId);
            return _cooldownSteps[_indices[unitId]] * SimulationClock.StepSeconds;
        }

        public void Advance(int steps)
        {
            for (var i = 0; i < _cooldownSteps.Length; i++)
            {
                _cooldownSteps[i] = Math.Max(0, _cooldownSteps[i] - steps);
            }
        }
    }
}
