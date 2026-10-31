using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Tutorial;
using UnitySlotTower.Slot;
using UnitySlotTower.Tutorial;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("spec 002: スロットと戦闘の連動（AC-101・AC-110・AC-113）を Game シーンで確かめる")]
    public sealed class SlotBattleLinkTests
    {
        private Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private SlotCabinet _cabinet;
        private SlotCelebration _celebration;

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
                if (device is Keyboard && device.enabled)
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
            _celebration = Object.FindAnyObjectByType<SlotCelebration>();
            for (var i = 0; i < 300 && _battle.Field == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle?.Field, Is.Not.Null, "Game シーンの戦闘が始まらない");
            Assert.That(_celebration, Is.Not.Null, "Game シーンに当たりの演出がない");
        }

        [TearDown]
        public void TearDown()
        {
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
        [Description("AC-101: 戦闘中に REG が揃うと、シーンの群れの表どおりに敵が出現口から出て、フィールドに表示される")]
        public IEnumerator Reg_During_The_Fight_Spawns_The_Swarm_From_The_Table()
        {
            _battle.StartWave();
            yield return null;
            var before = _battle.Field.Enemies.Count + _battle.Field.PendingExtraSpawns;

            Assert.That(_battle.Run.ApplySlotRole(SlotFlag.RegRole), Is.True);
            var pending = _battle.Field.PendingExtraSpawns;
            yield return new WaitForSeconds(0.2f);

            Assert.That(pending, Is.GreaterThan(0), "reg_swarms.csv の群れが戦闘に渡っていない");
            Assert.That(_battle.Field.Enemies.Count + _battle.Field.PendingExtraSpawns, Is.GreaterThanOrEqualTo(before + pending));
        }

        [UnityTest]
        [Description("AC-101: 準備中に揃った群れは戦闘開始で出る")]
        public IEnumerator Reg_While_Preparing_Comes_Out_When_The_Fight_Starts()
        {
            _battle.Run.ApplySlotRole(SlotFlag.RegRole);
            var held = _battle.Run.PendingSwarms;

            _battle.StartWave();
            yield return null;

            Assert.That((held, _battle.Run.PendingSwarms, _battle.Field.PendingExtraSpawns + _battle.Field.Enemies.Count > 0),
                Is.EqualTo((1, 0, true)));
        }

        [UnityTest]
        [Description("AC-113 / FR-124: 演出中も敵が進み、レバー（Space）でリールが回り、演出はクリックを受けない")]
        public IEnumerator Battle_And_Slot_Keep_Going_During_The_Celebration()
        {
            _battle.StartWave();
            for (var i = 0; i < 60 && _battle.Field.Enemies.Count == 0; i++)
            {
                yield return null;
            }

            _celebration.Play(SlotCelebrationKind.Big);
            var enemy = _battle.Field.Enemies[0];
            var distance = enemy.Distance;
            yield return Tap(Key.Space);
            var reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            for (var i = 0; i < 180 && !reels[0].IsSpinning; i++)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.3f);

            Assert.That((_celebration.IsPlaying, reels[0].IsSpinning, enemy.Distance > distance), Is.EqualTo((true, true, true)),
                "演出中に戦闘かスロットが止まった");
            var flash = GameObject.Find("Celebration Flash").GetComponent<Image>();
            Assert.That((flash.enabled, flash.raycastTarget), Is.EqualTo((true, false)));
        }

        [UnityTest]
        [Description("AC-113 / FR-122: BIG と REG は色が違う")]
        public IEnumerator Big_And_Reg_Flash_In_Different_Colors()
        {
            var flash = GameObject.Find("Celebration Flash").GetComponent<Image>();
            _celebration.Play(SlotCelebrationKind.Big);
            yield return null;
            var big = flash.color;
            _celebration.Play(SlotCelebrationKind.Reg);
            yield return null;
            var reg = flash.color;

            Assert.That(big.g - reg.g, Is.GreaterThan(0.4f), $"BIG {big} と REG {reg} の色が近い");
        }

        [UnityTest]
        [Description("AC-110 / FR-115: 操作バーは味方4種のアイコンとバフのゲージ。攻撃力上昇でゲージが満ちる")]
        public IEnumerator Operation_Bar_Has_Four_Units_And_Buff_Gauges()
        {
            var icons = Object.FindObjectsByType<UnitBarIcon>(FindObjectsSortMode.None);
            var gauges = Object.FindAnyObjectByType<BuffGauges>();
            _battle.Run.ApplySlotRole(BattleBuffs.AttackRole);
            yield return null;

            var fill = gauges.transform.Find("Attack/Back/Fill").GetComponent<Image>();
            Assert.That((icons.Length, _battle.Units.All.Count, fill.fillAmount), Is.EqualTo((4, 4, 1f)));
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
