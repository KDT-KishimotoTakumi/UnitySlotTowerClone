using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 操作バーの状態表示とボタン（T308、plan 2章）: ライフ・所持コイン・ウェーブ番号（n/最終）、
    /// ▶（準備中だけ出す）・撤退（味方を選んでいるときだけ出す）・一時停止（一時停止中は色を変える）。
    /// </summary>
    public sealed class OperationBarStatus : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private UnitPlacementInput placementInput;
        [SerializeField] private TMP_Text lifeText;
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button retreatButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Image pauseBackground;
        [SerializeField] private Color pausedColor = new Color(0.95f, 0.75f, 0.2f);

        private Color _pauseNormalColor;
        private int _shownLife = -1;
        private int _shownCoins = -1;
        private int _shownWave = -1;
        private bool _shownPaused;

        private void Awake()
        {
            _pauseNormalColor = pauseBackground.color;
        }

        private void OnEnable()
        {
            startButton.onClick.AddListener(battle.StartWave);
            retreatButton.onClick.AddListener(placementInput.RetreatSelected);
            pauseButton.onClick.AddListener(battle.TogglePause);
        }

        private void OnDisable()
        {
            startButton.onClick.RemoveListener(battle.StartWave);
            retreatButton.onClick.RemoveListener(placementInput.RetreatSelected);
            pauseButton.onClick.RemoveListener(battle.TogglePause);
        }

        private void LateUpdate()
        {
            var run = battle.Run;
            if (run == null)
            {
                return;
            }

            SetNumber(lifeText, run.Field.Life, ref _shownLife);
            SetNumber(coinText, run.Wallet.Coins, ref _shownCoins);
            if (run.Wave != _shownWave)
            {
                _shownWave = run.Wave;
                waveText.SetText("{0}/{1}", (float)run.Wave, battle.WaveCount);
            }

            SetActive(startButton, run.Phase == WavePhase.Preparing);
            SetActive(retreatButton, placementInput.SelectedUnit != null);
            if (battle.IsUserPaused != _shownPaused)
            {
                _shownPaused = battle.IsUserPaused;
                pauseBackground.color = _shownPaused ? pausedColor : _pauseNormalColor;
            }
        }

        private static void SetActive(Button button, bool active)
        {
            if (button.gameObject.activeSelf != active)
            {
                button.gameObject.SetActive(active);
            }
        }

        private static void SetNumber(TMP_Text text, int value, ref int shown)
        {
            if (value != shown)
            {
                shown = value;
                text.SetText("{0}", value);
            }
        }
    }
}
