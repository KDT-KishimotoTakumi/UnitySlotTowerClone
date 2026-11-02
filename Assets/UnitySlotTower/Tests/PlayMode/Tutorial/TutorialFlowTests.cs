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
using UnitySlotTower.Tutorial;

namespace UnitySlotTower.Tests.PlayMode.Tutorial
{
    [Description("FR-004（T403）: 文字なし説明は初めての場面で時間を止めて出し、押して離すと閉じる")]
    public sealed class TutorialFlowTests
    {
        private Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private TutorialDirector _director;
        private TutorialProgress _progress;

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

            // PlayerPrefs の記録には触れず、まだ何も見ていない状態から始める。
            _progress = new TutorialProgress();
            TutorialDirector.ProgressOverride = _progress;
            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _director = Object.FindAnyObjectByType<TutorialDirector>();
            Assert.That(_director, Is.Not.Null, "Game シーンに文字なし説明がない");
            for (var i = 0; i < 60 && !_director.Current.HasValue; i++)
            {
                yield return null;
            }
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
        public IEnumerator Summon_Is_Shown_At_Start_And_Holds_Time_And_Input()
        {
            Assert.That((_director.Current, _battle.IsHeld, Time.timeScale),
                Is.EqualTo(((TutorialStep?)TutorialStep.Summon, true, 0f)));

            // 表示中は選択も一時停止の切り替えも受け付けない。
            yield return Tap(Key.Digit1);
            yield return Tap(Key.P);

            Assert.That((_battle.Deployment.Phase, _battle.IsUserPaused, _director.Current),
                Is.EqualTo((DeployPhase.Idle, false, (TutorialStep?)TutorialStep.Summon)));
        }

        [UnityTest]
        public IEnumerator Release_After_A_Moment_Closes_And_Records_The_Step()
        {
            var prefsBefore = PlayerPrefs.GetString(TutorialDirector.PrefsKey, "");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Tap(Key.Enter);

            Assert.That((_director.Current, _battle.IsHeld, Time.timeScale, _progress.HasSeen(TutorialStep.Summon),
                    _battle.Deployment.Phase, _battle.Run.Phase),
                Is.EqualTo(((TutorialStep?)null, false, 1f, true, DeployPhase.Idle, WavePhase.Preparing)),
                "閉じる Enter が ▶ に使われた、または閉じない");
            Assert.That(PlayerPrefs.GetString(TutorialDirector.PrefsKey, ""), Is.EqualTo(prefsBefore),
                "テスト用の記録なのに PlayerPrefs に保存した");
        }

        [UnityTest]
        public IEnumerator Selecting_A_Unit_Then_Shows_Placement()
        {
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Tap(Key.Enter);
            yield return Tap(Key.Digit1);
            yield return null;

            Assert.That((_battle.Deployment.Phase, _director.Current, _battle.IsHeld),
                Is.EqualTo((DeployPhase.ChoosingCell, (TutorialStep?)TutorialStep.Place, true)));
        }

        [UnityTest]
        public IEnumerator Placement_Marks_The_Cells_Where_The_Unit_Can_Be_Placed()
        {
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Tap(Key.Enter);
            yield return Tap(Key.Digit2);
            yield return null;

            var markers = 0;
            foreach (var image in GameObject.Find("Tutorial Overlay").GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                markers += image.enabled && image.name.StartsWith("Placeable") ? 1 : 0;
            }

            var placeable = 0;
            var map = _battle.Run.Map;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    placeable += _battle.Deployment.CanPlace(new GridPoint(x, y)) ? 1 : 0;
                }
            }

            Assert.That((_director.Current, markers),
                Is.EqualTo(((TutorialStep?)TutorialStep.Place, placeable)), "置けるマスと印の数が合わない");
            Assert.That(placeable, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Facing_Turns_The_Unit_To_Show_The_Range_Rotating()
        {
            _progress.MarkSeen(TutorialStep.Summon);
            _progress.MarkSeen(TutorialStep.Place);
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Tap(Key.Enter);

            // 置けるマスを Core の操作で選び、向き選びに入れる。
            var deployment = _battle.Deployment;
            deployment.Select("sword");
            var map = _battle.Run.Map;
            for (var y = 0; y < map.Height && deployment.Phase == DeployPhase.ChoosingCell; y++)
            {
                for (var x = 0; x < map.Width && deployment.Phase == DeployPhase.ChoosingCell; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (deployment.CanPlace(cell))
                    {
                        deployment.SetCursor(cell);
                        deployment.ChooseCell();
                    }
                }
            }

            yield return null;
            yield return null;
            var first = deployment.Facing;
            yield return new WaitForSecondsRealtime(1.7f);

            Assert.That((_director.Current, first, deployment.Facing, deployment.Phase),
                Is.EqualTo(((TutorialStep?)TutorialStep.Facing, Facing.Right, Facing.Down, DeployPhase.ChoosingFacing)));
        }

        [UnityTest]
        public IEnumerator Quick_Press_Right_After_Showing_Does_Not_Close()
        {
            yield return Tap(Key.Enter);

            Assert.That(_director.Current, Is.EqualTo(TutorialStep.Summon));
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
