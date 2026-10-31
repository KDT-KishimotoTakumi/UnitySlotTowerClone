using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// 下部のスロット筐体の仮表示（T110、plan 2・3・5章）。ベット＋レバー（Space）で回し、J/K/L かボタンで目押しする。
    /// ランプ: 先バレ（ボーナスを揃えるまで点灯）・ボーナス中・確変中・当たり確定（ボーナスを揃えるまで点滅、spec 002 FR-121）。
    /// 数字: クレジット・払い出し・ボーナス中の払い出し累計か確変の残りゲーム数。
    /// 揃った役・先バレ・当たり確定は、リールが見た目の上でも止まってからイベントで知らせる（戦闘のバフ・群れと演出、spec 002）。
    /// </summary>
    public sealed class SlotCabinet : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";
        private static readonly string[] StopActions = { "StopLeft", "StopCenter", "StopRight" };

        [Header("Tables")]
        [SerializeField] private TextAsset gameConfigTable;
        [SerializeField] private TextAsset reelsTable;
        [SerializeField] private TextAsset payoutsTable;
        [SerializeField] private TextAsset probabilitiesTable;
        [SerializeField] private TextAsset premonitionTable;
        [SerializeField] private TextAsset rulesTable;

        [Tooltip("難易度（設定1〜6）。Game シーンを直接開いたときだけ使う。難易度選択を通ったときはその設定（T401）。")]
        [SerializeField, Range(1, 6)] private int setting = 1;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Button leverButton;
        [SerializeField] private Button[] stopButtons = new Button[SlotReels.ReelCount];

        [Header("View")]
        [SerializeField] private SlotReelView[] reels = new SlotReelView[SlotReels.ReelCount];
        [SerializeField] private Graphic premonitionLamp;
        [SerializeField] private Graphic bonusLamp;
        [SerializeField] private Graphic kakuhenLamp;
        [Tooltip("当たり確定ランプ。ボーナスに当たってから揃えるまで点滅する。")]
        [SerializeField, FormerlySerializedAs("replayLamp")] private Graphic noticeLamp;
        [SerializeField] private float noticeBlinkPerSecond = 4f;
        [SerializeField] private Graphic coinIcon;
        [SerializeField] private TMP_Text creditText;
        [SerializeField] private TMP_Text payoutText;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private Color lampOffColor = new Color(0.18f, 0.18f, 0.2f);

        [Header("Sound")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("未設定なら仮のチャイムを作って鳴らす。")]
        [SerializeField] private AudioClip premonitionSound;

        private const double CoinBlinkSeconds = 1.0;

        private SlotMachine _machine;
        private CoinWallet _wallet;
        private SlotWait _wait;
        private InputAction _lever;
        private readonly InputAction[] _stops = new InputAction[SlotReels.ReelCount];
        private Color[] _lampOnColors;
        private Graphic[] _lamps;
        private bool _premonitionLit;
        private double _coinBlinkUntil;
        private int _coinsBeforeLastStop;
        private int _lastPayout;
        private int _shownCredit = -1;
        private int _shownPayout = -1;
        private int _shownCounter = -1;
        private readonly List<string> _pendingAligned = new List<string>();
        private bool _pendingAnnounce;
        private bool _pendingResult;

        /// <summary>所持コイン。戦闘の召喚・撃破ドロップと共有する（FR-040、T301）。Awake で作る。</summary>
        public CoinWallet Wallet => _wallet;

        /// <summary>true のあいだはレバーとストップを受け付けない（一時停止中: FR-012）。</summary>
        public bool InputLocked { get; set; }

        /// <summary>このプレイの難易度（設定1〜6、FR-045）。</summary>
        public int Setting => setting;

        /// <summary>スロットの状態（通常・ボーナス・確変）。読み込みに失敗していれば通常。</summary>
        public SlotState State => _machine?.State ?? SlotState.Normal;

        /// <summary>slot_rules.csv の値（バフの倍率・秒数を含む）。読み込みに失敗していれば null。</summary>
        public SlotRules Rules { get; private set; }

        /// <summary>ボーナスに当たっていて、まだ揃えていない（spec 002 FR-121）。</summary>
        public bool IsBonusPending => _machine != null && _machine.IsBonusPending;

        /// <summary>レバーオンで先バレが起きた（spec 002 FR-120）。</summary>
        public event Action PremonitionStarted;

        /// <summary>ボーナスに当たったゲームの第3停止後（リールが止まって見えてから）。BIG・REG は区別しない（spec 002 FR-121）。</summary>
        public event Action BonusAnnounced;

        /// <summary>揃った役（払い出し表の順）。リールが止まって見えてから知らせる（spec 002）。</summary>
        public event Action<IReadOnlyList<string>> RolesAligned;

        private void Awake()
        {
            setting = GameSession.SelectedSetting ?? setting;
            SlotReels slotReels;
            try
            {
                var config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
                slotReels = SlotReels.Load(Parse(SlotReels.FileName, reelsTable));
                var payouts = SlotPayouts.Load(Parse(SlotPayouts.FileName, payoutsTable));
                var probabilities = SlotProbabilities.Load(Parse(SlotProbabilities.FileName, probabilitiesTable), payouts);
                var premonition = SlotPremonition.Load(Parse(SlotPremonition.FileName, premonitionTable), payouts);
                var rules = SlotRules.Load(Parse(SlotRules.FileName, rulesTable));
                Rules = rules;
                _wallet = new CoinWallet(config.InitialCoins);
                _wait = new SlotWait(rules.WaitSeconds);
                var control = new SlotReelControl(slotReels, payouts, rules.MaxSlide);
                _machine = new SlotMachine(slotReels, payouts, probabilities, premonition, control, rules, _wallet,
                    config.Bet, setting, new SeededRandom((ulong)DateTime.UtcNow.Ticks));
                _machine.RolesAligned += OnRolesAligned;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
                return;
            }

            for (var i = 0; i < reels.Length; i++)
            {
                reels[i].Initialize(slotReels, i, 0);
            }

            _lamps = new[] { premonitionLamp, bonusLamp, kakuhenLamp, noticeLamp };
            _lampOnColors = new Color[_lamps.Length];
            for (var i = 0; i < _lamps.Length; i++)
            {
                _lampOnColors[i] = _lamps[i].color;
            }

            if (premonitionSound == null)
            {
                premonitionSound = CreateChime();
            }

            var map = inputActions.FindActionMap(ActionMap, true);
            _lever = map.FindAction("Lever", true);
            for (var i = 0; i < _stops.Length; i++)
            {
                _stops[i] = map.FindAction(StopActions[i], true);
            }

            // クリックしたボタンが選択されたままだと Space（Submit）で押されてしまうので、押したら選択を外す。
            leverButton.onClick.AddListener(() =>
            {
                Deselect();
                OnLever();
            });
            for (var i = 0; i < stopButtons.Length; i++)
            {
                var reel = i;
                stopButtons[i].onClick.AddListener(() =>
                {
                    Deselect();
                    OnStop(reel);
                });
            }
        }

        private void OnEnable()
        {
            if (_lever == null)
            {
                return;
            }

            _lever.performed += OnLeverAction;
            _stops[0].performed += OnStopLeftAction;
            _stops[1].performed += OnStopCenterAction;
            _stops[2].performed += OnStopRightAction;
            _lever.actionMap.Enable();
        }

        private void OnDisable()
        {
            if (_lever == null)
            {
                return;
            }

            _lever.performed -= OnLeverAction;
            _stops[0].performed -= OnStopLeftAction;
            _stops[1].performed -= OnStopCenterAction;
            _stops[2].performed -= OnStopRightAction;
        }

        private void OnLeverAction(InputAction.CallbackContext context) => OnLever();
        private void OnStopLeftAction(InputAction.CallbackContext context) => OnStop(0);
        private void OnStopCenterAction(InputAction.CallbackContext context) => OnStop(1);
        private void OnStopRightAction(InputAction.CallbackContext context) => OnStop(2);

        private static void Deselect()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        // 回転中のレバーは無視する。ウェイト中は受け付けておき、時間が来たら回す（plan 5章）。
        private void OnLever()
        {
            if (!InputLocked && _machine.Spin == null)
            {
                _wait.Request();
            }
        }

        private void OnStop(int reel)
        {
            var spin = _machine.Spin;
            if (InputLocked || spin == null || spin.IsStopped(reel))
            {
                return;
            }

            _coinsBeforeLastStop = _wallet.Coins;
            var stop = _machine.Stop(reel, reels[reel].PressPosition);
            reels[reel].StopAt(stop);
            if (_machine.Spin == null)
            {
                _lastPayout = _wallet.Coins - _coinsBeforeLastStop;
                _pendingResult = true;
                _pendingAnnounce = _machine.AnnouncesBonus;
            }
        }

        private void OnRolesAligned(IReadOnlyList<string> roles)
        {
            _pendingAligned.Clear();
            _pendingAligned.AddRange(roles);
        }

        private void OnDestroy()
        {
            if (_machine != null)
            {
                _machine.RolesAligned -= OnRolesAligned;
            }
        }

        // 3本目を押したゲームの結果を、リールが見た目の上でも止まってから知らせる。
        private void PublishResult()
        {
            _pendingResult = false;
            if (_pendingAnnounce)
            {
                _pendingAnnounce = false;
                BonusAnnounced?.Invoke();
            }

            if (_pendingAligned.Count > 0)
            {
                RolesAligned?.Invoke(_pendingAligned);
                _pendingAligned.Clear();
            }
        }

        private void Update()
        {
            var now = Time.timeAsDouble;
            if (_wait.Consume(now))
            {
                if (_pendingResult)
                {
                    PublishResult();
                }

                if (_machine.TryLever())
                {
                    _wait.Started(now);
                    _lastPayout = 0;
                    foreach (var reel in reels)
                    {
                        reel.StartSpin();
                    }

                    if (_machine.Premonition)
                    {
                        _premonitionLit = true;
                        audioSource.PlayOneShot(premonitionSound);
                        PremonitionStarted?.Invoke();
                    }
                }
                else
                {
                    // 所持コインが足りない（FR-040、plan 3章）。
                    _coinBlinkUntil = now + CoinBlinkSeconds;
                }
            }

            if (_machine.State == SlotState.Bonus)
            {
                _premonitionLit = false;
            }

            SetLamp(0, _premonitionLit);
            SetLamp(1, _machine.State == SlotState.Bonus);
            SetLamp(2, _machine.State == SlotState.Kakuhen);
            SetLamp(3, _machine.IsBonusPending && !_pendingResult && (int)(now * noticeBlinkPerSecond * 2) % 2 == 0);

            var blink = now < _coinBlinkUntil && (int)(now * 10) % 2 == 0;
            coinIcon.enabled = !blink;

            // 3本目を押したあとの払い出しは、リールが見た目の上でも止まってから数字に出す。
            if (_machine.Spin != null || !AnyReelSpinning())
            {
                UpdateNumbers();
                if (_pendingResult && _machine.Spin == null)
                {
                    PublishResult();
                }
            }
        }

        private void UpdateNumbers()
        {
            SetNumber(creditText, _wallet.Coins, ref _shownCredit);
            SetNumber(payoutText, _lastPayout, ref _shownPayout);
            var counter = _machine.State == SlotState.Bonus ? _machine.BonusPayout
                : _machine.State == SlotState.Kakuhen ? _machine.KakuhenGamesLeft
                : 0;
            SetNumber(counterText, counter, ref _shownCounter);
            counterText.enabled = _machine.State != SlotState.Normal;
        }

        private static void SetNumber(TMP_Text text, int value, ref int shown)
        {
            if (value != shown)
            {
                shown = value;
                text.SetText("{0}", value);
            }
        }

        private void SetLamp(int index, bool on) => _lamps[index].color = on ? _lampOnColors[index] : lampOffColor;

        private bool AnyReelSpinning()
        {
            foreach (var reel in reels)
            {
                if (reel.IsSpinning)
                {
                    return true;
                }
            }

            return false;
        }

        private static CsvTable Parse(string fileName, TextAsset asset)
        {
            if (asset == null)
            {
                throw new InvalidOperationException($"{fileName} が設定されていない");
            }

            return CsvParser.Parse(fileName, asset.text);
        }

        // 素材を選ぶまで（T501）の仮の先バレ音。2音の上昇チャイム。
        private static AudioClip CreateChime()
        {
            const int rate = 44100;
            const float noteSeconds = 0.18f;
            var noteSamples = (int)(rate * noteSeconds);
            var data = new float[noteSamples * 2];
            var notes = new[] { 880f, 1318.5f };
            for (var n = 0; n < notes.Length; n++)
            {
                for (var i = 0; i < noteSamples; i++)
                {
                    var t = (float)i / rate;
                    var envelope = 1f - (float)i / noteSamples;
                    data[n * noteSamples + i] = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * envelope * 0.4f;
                }
            }

            var clip = AudioClip.Create("Premonition Chime", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
