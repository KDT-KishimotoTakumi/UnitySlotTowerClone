using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Tutorial;
using UnitySlotTower.Slot;
using UnitySlotTower.Tutorial;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("AC-011 の一部: 一時停止中は敵が止まり、閲覧以外の操作が拒否される（FR-012, T307）")]
    public sealed class PauseTests
    {
        private Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private SlotCabinet _cabinet;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Editor が非フォーカスでもフレームと入力が進むようにする（後で戻す）。
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (var device in InputSystem.devices)
            {
                if ((device is Keyboard || device is Mouse) && device.enabled)
                {
                    _disabled.Add(device);
                }
            }

            foreach (var device in _disabled)
            {
                InputSystem.DisableDevice(device);
            }

            _keyboard = InputSystem.AddDevice<Keyboard>("Test Keyboard");
            // 説明（T403）が出て時間が止まらないよう、全部見たことにする。
            TutorialDirector.ProgressOverride = TutorialProgress.AllSeen();

            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            for (var i = 0; i < 300 && (_battle.Field == null || _battle.Field.Enemies.Count == 0); i++)
            {
                _battle.StartWave();
                yield return null;
            }

            Assert.That(_battle?.Field?.Enemies.Count, Is.GreaterThan(0), "Game シーンの敵が出てこない");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            InputSystem.RemoveDevice(_keyboard);
            foreach (var device in _disabled)
            {
                InputSystem.EnableDevice(device);
            }

            _disabled.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            Application.runInBackground = _runInBackground;
            TutorialDirector.ProgressOverride = null;
        }

        [UnityTest]
        public IEnumerator P_Stops_Time_And_Refuses_Slot_And_Placement_Until_Resumed()
        {
            var reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            yield return Tap(Key.P);
            Assert.That(_battle.IsPaused, Is.True, "P で一時停止しない");

            var enemy = _battle.Field.Enemies[0];
            var tick = _battle.Field.Tick;
            var distance = enemy.Distance;
            var coins = _battle.Wallet.Coins;
            yield return Tap(Key.Space);
            yield return Tap(Key.Digit1);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That((_battle.Field.Tick, enemy.Distance, reels[0].IsSpinning, _battle.Wallet.Coins,
                    _battle.Deployment.Phase),
                Is.EqualTo((tick, distance, false, coins, DeployPhase.Idle)));

            yield return Tap(Key.P);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That((_battle.IsPaused, Time.timeScale, _battle.Field.Tick > tick),
                Is.EqualTo((false, 1f, true)), "P で再開しない");
        }

        [UnityTest]
        public IEnumerator Escape_Steps_Back_While_Placing_And_Otherwise_Toggles_Pause()
        {
            yield return Tap(Key.Digit1);
            Assert.That(_battle.Deployment.Phase, Is.EqualTo(DeployPhase.ChoosingCell));

            yield return Tap(Key.Escape);
            var afterCancel = (_battle.Deployment.Phase, _battle.IsPaused);
            yield return Tap(Key.Escape);
            var afterPause = _battle.IsPaused;
            yield return Tap(Key.Escape);

            Assert.That((afterCancel, afterPause, _battle.IsPaused),
                Is.EqualTo(((DeployPhase.Idle, false), true, false)));
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }
    }
}
