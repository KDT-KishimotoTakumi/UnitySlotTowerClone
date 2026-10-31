using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Slot;
using UnitySlotTower.Tutorial;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンに文字なし説明（T403）を付ける一回限りの手順。対象のボタンやランプは、操作バーとスロット筐体が参照しているものを使う。
    /// 既に付いていれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class TutorialSetup
    {
        private const int SlotCount = 5;

        [MenuItem("Tools/UnitySlotTower/Setup/Add Tutorial")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<TutorialDirector>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] 文字なし説明は既にあるため作成しない。");
                return;
            }

            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var input = Object.FindAnyObjectByType<UnitPlacementInput>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var fieldView = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            var camera = GameObject.Find("Battle Camera")?.GetComponent<Camera>();
            var canvas = GameObject.Find("UI Canvas")?.GetComponent<RectTransform>();
            var bar = GameObject.Find("Operation Bar")?.GetComponent<RectTransform>();
            if (battle == null || input == null || cabinet == null || fieldView == null || status == null || camera == null
                || canvas == null || bar == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの戦闘・配置操作・スロット筐体・操作バー・カメラ・キャンバスが見つからない。");
                return;
            }

            var unitIcons = new Object[SlotCount];
            var upgradeIcons = new Object[SlotCount];
            for (var i = 0; i < SlotCount; i++)
            {
                unitIcons[i] = bar.Find($"Unit Icon {i + 1}");
                upgradeIcons[i] = bar.Find($"Upgrade Icon {i + 1}");
                if (unitIcons[i] == null || upgradeIcons[i] == null)
                {
                    Debug.LogError($"[SlotTower Setup] Unit Icon / Upgrade Icon {i + 1} が見つからない。");
                    return;
                }
            }

            var cabinetData = new SerializedObject(cabinet);
            var statusData = new SerializedObject(status);
            var stops = cabinetData.FindProperty("stopButtons");
            var stopRects = new Object[stops.arraySize];
            for (var i = 0; i < stopRects.Length; i++)
            {
                stopRects[i] = RectOf(stops.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            var director = new GameObject("Tutorial").AddComponent<TutorialDirector>();
            var serialized = new SerializedObject(director);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("placementInput").objectReferenceValue = input;
            serialized.FindProperty("slotCabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("fieldView").objectReferenceValue = fieldView;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("inputActions").objectReferenceValue =
                new SerializedObject(input).FindProperty("inputActions").objectReferenceValue;
            serialized.FindProperty("overlayParent").objectReferenceValue = canvas;
            SetArray(serialized.FindProperty("unitIcons"), unitIcons);
            SetArray(serialized.FindProperty("upgradeIcons"), upgradeIcons);
            serialized.FindProperty("startButton").objectReferenceValue =
                RectOf(statusData.FindProperty("startButton").objectReferenceValue);
            serialized.FindProperty("retreatButton").objectReferenceValue =
                RectOf(statusData.FindProperty("retreatButton").objectReferenceValue);
            serialized.FindProperty("leverButton").objectReferenceValue =
                RectOf(cabinetData.FindProperty("leverButton").objectReferenceValue);
            SetArray(serialized.FindProperty("stopButtons"), stopRects);
            serialized.FindProperty("bonusLamp").objectReferenceValue =
                RectOf(cabinetData.FindProperty("bonusLamp").objectReferenceValue);
            serialized.FindProperty("bonusCounter").objectReferenceValue =
                RectOf(cabinetData.FindProperty("counterText").objectReferenceValue);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = director.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 文字なし説明を追加した。");
        }

        /// <summary>見た説明の記録を消し、次のプレイで全部の説明をもう一度出す（確認用）。</summary>
        [MenuItem("Tools/UnitySlotTower/Tutorial/Reset Seen Steps")]
        private static void ResetSeenSteps()
        {
            PlayerPrefs.DeleteKey(TutorialDirector.PrefsKey);
            PlayerPrefs.Save();
            Debug.Log("[SlotTower] 文字なし説明の記録を消した。");
        }

        private static RectTransform RectOf(Object component) =>
            component is Component c ? c.GetComponent<RectTransform>() : null;

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
