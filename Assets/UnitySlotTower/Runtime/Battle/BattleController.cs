using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// ウェーブの進行（<see cref="WaveRun"/>）を組み立てて実時間で進める。財布はスロット筐体と共有する（T301）。
    /// スロット操作中も止まらない（FR-011）。戦闘は ▶（<see cref="StartWave"/>）で始める。autoStart なら準備に入るとすぐ始める。
    /// ウェーブ n はマップ w{n} で戦い、ウェーブが始まるとフィールドの表示を差し替える（T305）。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class BattleController : MonoBehaviour
    {
        [Header("Tables")]
        [SerializeField] private TextAsset gameConfigTable;
        [SerializeField] private TextAsset unitsTable;
        [SerializeField] private TextAsset unitRangesTable;
        [SerializeField] private TextAsset unitUpgradesTable;
        [SerializeField] private TextAsset enemiesTable;
        [SerializeField] private TextAsset wavesTable;

        [SerializeField] private BattleFieldView fieldView;
        [Tooltip("所持コインを共有するスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;
        [Tooltip("最後のウェーブの番号。waves.csv とフィールドのマップの表にそのウェーブまでが要る。")]
        [SerializeField] private int waveCount = 5;
        [Tooltip("準備に入ったらすぐ戦闘を始める（確認用。通常は ▶ で始める）。")]
        [SerializeField] private bool autoStart;

        private SimulationClock _clock;

        public int WaveCount => waveCount;
        public UnitTable Units { get; private set; }
        public WaveRun Run { get; private set; }
        public CoinWallet Wallet => Run?.Wallet;
        public UnitSummoner Summoner => Run?.Summoner;
        public UnitUpgrades Upgrades => Run?.Upgrades;
        public Battlefield Field => Run?.Field;
        public UnitDeployment Deployment => Run?.Deployment;

        private void Start()
        {
            // マップは BattleFieldView の Awake、財布は SlotCabinet の Awake で作られている。
            if (fieldView == null || fieldView.Map == null)
            {
                Debug.LogError("戦闘フィールドのマップがない", this);
                enabled = false;
                return;
            }

            if (slotCabinet == null || slotCabinet.Wallet == null)
            {
                Debug.LogError("財布を共有するスロット筐体がない", this);
                enabled = false;
                return;
            }

            try
            {
                var config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
                Units = UnitTable.Load(Parse(UnitTable.FileName, unitsTable));
                var ranges = UnitRanges.Load(Parse(UnitRanges.FileName, unitRangesTable), Units);
                var upgradeTable = UnitUpgradeTable.Load(Parse(UnitUpgradeTable.FileName, unitUpgradesTable), Units);
                var enemies = EnemyTable.Load(Parse(EnemyTable.FileName, enemiesTable));
                var waves = Parse(WaveSchedule.FileName, wavesTable);
                var wallet = slotCabinet.Wallet;
                Run = new WaveRun(config, Units, ranges, new UnitUpgrades(upgradeTable, wallet), wallet, waveCount,
                    wave =>
                    {
                        var map = wave == 1 ? fieldView.Map : fieldView.LoadWaveMap(wave);
                        return new WaveStage(map, WaveSchedule.Load(waves, wave, map, enemies));
                    },
                    new SeededRandom((ulong)DateTime.UtcNow.Ticks));
                Run.WaveBegan += OnWaveBegan;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
                return;
            }

            _clock = new SimulationClock();
        }

        private bool _userPaused;

        /// <summary>
        /// 時間が止まっているか（一時停止中か説明の表示中）。止まっているあいだは配置・撤退・強化・スロットの操作を受け付けない（FR-012）。
        /// </summary>
        public bool IsPaused => _userPaused || IsHeld;

        /// <summary>プレイヤーの一時停止（FR-012）で止まっているか。説明の表示中は含まない。</summary>
        public bool IsUserPaused => _userPaused;

        /// <summary>文字なし説明（T403）の表示中で止まっているか。このあいだは一時停止を切り替えられない。</summary>
        public bool IsHeld { get; private set; }

        /// <summary>一時停止を切り替える。スロットのリールやウェイトも止めるため Time.timeScale を 0 にする。</summary>
        public void SetPaused(bool paused)
        {
            _userPaused = paused;
            ApplyTimeStop();
        }

        public void TogglePause()
        {
            if (!IsHeld)
            {
                SetPaused(!_userPaused);
            }
        }

        /// <summary>説明の表示のあいだ時間を止める（T403）。一時停止と同じく操作を受け付けない。</summary>
        public void SetHeld(bool held)
        {
            IsHeld = held;
            ApplyTimeStop();
        }

        private void ApplyTimeStop()
        {
            var stopped = IsPaused;
            Time.timeScale = stopped ? 0f : 1f;
            if (_clock != null)
            {
                _clock.IsPaused = stopped;
            }

            if (slotCabinet != null)
            {
                slotCabinet.InputLocked = stopped;
            }
        }

        /// <summary>準備中なら戦闘を始める（▶、plan 4章）。一時停止中は受け付けない。</summary>
        public void StartWave()
        {
            if (!IsPaused)
            {
                Run?.Start();
            }
        }

        private void OnDestroy()
        {
            // シーンを離れても時間が止まったままにならないようにする。
            if (IsPaused)
            {
                Time.timeScale = 1f;
            }

            if (Run != null)
            {
                Run.WaveBegan -= OnWaveBegan;
            }
        }

        private void OnWaveBegan(int wave) => fieldView.Show(Run.Map);

        private void Update()
        {
            if (autoStart)
            {
                Run.Start();
            }

            Run.Advance(_clock.Advance(Time.deltaTime));
        }

        private static CsvTable Parse(string fileName, TextAsset asset)
        {
            if (asset == null)
            {
                throw new InvalidOperationException($"{fileName} が設定されていない");
            }

            return CsvParser.Parse(fileName, asset.text);
        }
    }
}
