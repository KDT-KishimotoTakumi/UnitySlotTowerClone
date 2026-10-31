using System;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    public enum WavePhase
    {
        /// <summary>敵が出る前の配置時間（plan C-2）。<see cref="WaveRun.Start"/> まで続く。</summary>
        Preparing,
        Fighting,
        /// <summary>最後のウェーブを終えた。</summary>
        Completed,
        GameOver,
    }

    /// <summary>1ウェーブ分のマップと出現内容。</summary>
    public sealed class WaveStage
    {
        public WaveStage(BattleMap map, WaveSchedule schedule)
        {
            Map = map;
            Schedule = schedule;
        }

        public BattleMap Map { get; }
        public WaveSchedule Schedule { get; }
    }

    /// <summary>
    /// ウェーブの進行（FR-010, FR-013〜016、plan 4章）。準備 → 戦闘 → 終了（ライフボーナス・ストック化）→ 次のウェーブの準備 と進める。
    /// 財布・強化・ストックはウェーブをまたいで同じものを使い、戦闘・召喚・配置操作はウェーブごとに作り直す。
    /// </summary>
    public sealed class WaveRun
    {
        private readonly GameConfig _config;
        private readonly UnitTable _units;
        private readonly UnitRanges _ranges;
        private readonly int _waveCount;
        private readonly Func<int, WaveStage> _loadStage;
        private readonly IRandom _random;

        private int _collectedDrops;

        /// <param name="waveCount">最後のウェーブの番号（1から）。</param>
        /// <param name="loadStage">ウェーブ番号からそのウェーブのマップと出現内容を返す。</param>
        public WaveRun(GameConfig config, UnitTable units, UnitRanges ranges, UnitUpgrades upgrades, CoinWallet wallet,
            int waveCount, Func<int, WaveStage> loadStage, IRandom random)
        {
            _config = config;
            _units = units;
            _ranges = ranges;
            Upgrades = upgrades;
            Wallet = wallet;
            _waveCount = waveCount;
            _loadStage = loadStage;
            _random = random;
            Begin(1);
        }

        public int Wave { get; private set; }
        public WavePhase Phase { get; private set; }
        public CoinWallet Wallet { get; }
        public UnitUpgrades Upgrades { get; }
        public UnitStock Stock { get; } = new UnitStock();

        /// <summary>最後に終えたウェーブのライフボーナス（まだなければ 0）。</summary>
        public int LastLifeBonus { get; private set; }

        /// <summary>いまのウェーブのマップ。</summary>
        public BattleMap Map { get; private set; }

        public Battlefield Field { get; private set; }
        public UnitSummoner Summoner { get; private set; }
        public UnitDeployment Deployment { get; private set; }

        /// <summary>新しいウェーブの準備に入った（戦闘・召喚・配置操作が作り直された）。引数はウェーブ番号。</summary>
        public event Action<int> WaveBegan;

        /// <summary>準備中なら戦闘を始める（▶）。</summary>
        public void Start()
        {
            if (Phase != WavePhase.Preparing)
            {
                return;
            }

            Phase = WavePhase.Fighting;
            Summoner.IgnoresCooldown = false;
        }

        /// <summary>
        /// 戦闘中だけ時間を進める。撃破ドロップは落ちた時点で財布に入れる（FR-033）。ウェーブが終わったら残りのステップは捨てる
        /// （次のウェーブは準備から）。
        /// </summary>
        public void Advance(int steps)
        {
            for (var i = 0; i < steps && Phase == WavePhase.Fighting; i++)
            {
                Field.Advance(1);
                Summoner.Advance(1);
                CollectDrops();

                if (Field.IsGameOver)
                {
                    Phase = WavePhase.GameOver;
                }
                else if (Field.IsCleared)
                {
                    EndWave();
                }
            }
        }

        private void CollectDrops()
        {
            var dropped = Field.DroppedCoins;
            if (dropped > _collectedDrops)
            {
                Wallet.Add(dropped - _collectedDrops);
                _collectedDrops = dropped;
            }
        }

        /// <summary>
        /// 残りライフ × 係数のコインを加え（FR-014）、配置中の味方を種類ごとに無料ストックへ足す（FR-015）。
        /// フィールドは次のウェーブで作り直すので、味方はそこで全員外れる（返還なし）。
        /// </summary>
        private void EndWave()
        {
            LastLifeBonus = Field.Life * _config.LifeBonusPerLife;
            Wallet.Add(LastLifeBonus);
            foreach (var unit in Field.Units)
            {
                Stock.Add(unit.Definition.Id, 1);
            }

            if (Wave >= _waveCount)
            {
                Phase = WavePhase.Completed;
                return;
            }

            Begin(Wave + 1);
        }

        private void Begin(int wave)
        {
            var stage = _loadStage(wave);
            Wave = wave;
            Phase = WavePhase.Preparing;
            Map = stage.Map;
            Field = new Battlefield(stage.Schedule, _ranges, _config.Life, _random);
            _collectedDrops = 0;
            Summoner = new UnitSummoner(_units, Wallet, _config.DeployLimit, _config.RetreatRefundRate, Stock)
            {
                IgnoresCooldown = true,
            };
            Deployment = new UnitDeployment(Map, _units, _ranges, Summoner, Field, Upgrades);
            WaveBegan?.Invoke(wave);
        }
    }
}
