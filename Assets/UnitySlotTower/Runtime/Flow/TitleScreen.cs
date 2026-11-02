using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// Title シーン（T401、plan 4章）。ロゴと ▶ の画面から、設定1〜6の筐体プレートを選ぶ画面に進み、選んだ設定で Game を始める。
    /// マウスはクリック、キーボードは矢印で選んで Enter（UI のナビゲーション）、Esc で難易度選択からタイトルへ戻る。
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [Tooltip("設定1〜6の順。")]
        [SerializeField] private Button[] settingButtons = new Button[GameSession.MaxSetting];

        private InputAction _cancel;

        /// <summary>難易度選択の画面を表示中か。</summary>
        public bool IsChoosingDifficulty => difficultyPanel.activeSelf;

        private void Awake()
        {
            _cancel = inputActions.FindActionMap("SlotTower", true).FindAction("Cancel", true);
            startButton.onClick.AddListener(ShowDifficulty);
            backButton.onClick.AddListener(ShowTitle);
            for (var i = 0; i < settingButtons.Length; i++)
            {
                var setting = GameSession.MinSetting + i;
                settingButtons[i].onClick.AddListener(() => GameSession.StartGame(setting));
            }

            ShowTitle();
        }

        private void OnEnable()
        {
            _cancel.performed += OnCancel;
            _cancel.actionMap.Enable();
        }

        private void OnDisable() => _cancel.performed -= OnCancel;

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (IsChoosingDifficulty)
            {
                ShowTitle();
            }
        }

        // 背景をクリックすると選択が外れてキーボードで操作できなくなるので、画面の既定のボタンを選び直す。
        private void Update()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy)
            {
                eventSystem.SetSelectedGameObject(DefaultButton().gameObject);
            }
        }

        private void ShowTitle() => Show(false);

        private void ShowDifficulty() => Show(true);

        private void Show(bool difficulty)
        {
            titlePanel.SetActive(!difficulty);
            difficultyPanel.SetActive(difficulty);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(DefaultButton().gameObject);
            }
        }

        private Button DefaultButton() => IsChoosingDifficulty ? settingButtons[0] : startButton;
    }
}
