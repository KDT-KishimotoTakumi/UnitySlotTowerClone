using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンの戦闘フィールドに、戦闘の進行・配置操作・その仮表示と、操作バーの仮の味方アイコン5つ（T208）を付ける一回限りの手順。
    /// 既に付いていれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class BattlePlacementSetup
    {
        private const string TableFolder = "Assets/UnitySlotTower/Data/Tables/";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        // units.csv の並び（タンク・剣士・弓・ヒーラー・バッファー）の仮の色。
        private static readonly Color[] IconColors =
        {
            new Color(0.45f, 0.55f, 0.75f),
            new Color(0.85f, 0.3f, 0.25f),
            new Color(0.35f, 0.75f, 0.35f),
            new Color(0.95f, 0.75f, 0.85f),
            new Color(0.95f, 0.8f, 0.25f),
        };

        [MenuItem("Tools/UnitySlotTower/Setup/Build Battle Placement")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] 配置操作は既にあるため作成しない。");
                return;
            }

            var field = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var camera = GameObject.Find("Battle Camera")?.GetComponent<Camera>();
            var bar = GameObject.Find("Operation Bar")?.GetComponent<RectTransform>();
            if (field == null || camera == null || bar == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Battle Field / Battle Camera / Operation Bar が見つからない。");
                return;
            }

            var battle = field.gameObject.AddComponent<BattleController>();
            Assign(battle, "gameConfigTable", Table(GameConfig.FileName));
            Assign(battle, "unitsTable", Table(UnitTable.FileName));
            Assign(battle, "unitRangesTable", Table(UnitRanges.FileName));
            Assign(battle, "unitUpgradesTable", Table(UnitUpgradeTable.FileName));
            Assign(battle, "enemiesTable", Table(EnemyTable.FileName));
            Assign(battle, "wavesTable", Table(WaveSchedule.FileName));
            Assign(battle, "fieldView", field);
            Assign(battle, "slotCabinet", Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include));

            var view = new GameObject("Placement View").AddComponent<UnitPlacementView>();
            view.transform.SetParent(field.transform, false);
            Assign(view, "battle", battle);
            Assign(view, "fieldView", field);

            var input = field.gameObject.AddComponent<UnitPlacementInput>();
            Assign(input, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            Assign(input, "battle", battle);
            Assign(input, "fieldView", field);
            Assign(input, "battleCamera", camera);

            for (var i = 0; i < IconColors.Length; i++)
            {
                var icon = new GameObject($"Unit Icon {i + 1}", typeof(RectTransform), typeof(Image), typeof(UnitBarIcon));
                icon.layer = bar.gameObject.layer;
                var rect = icon.GetComponent<RectTransform>();
                rect.SetParent(bar, false);
                rect.anchorMin = new Vector2(0.03f + i * 0.12f, 0.12f);
                rect.anchorMax = new Vector2(0.13f + i * 0.12f, 0.88f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                icon.GetComponent<Image>().color = IconColors[i];
                var barIcon = icon.GetComponent<UnitBarIcon>();
                Assign(barIcon, "placementInput", input);
                var serialized = new SerializedObject(barIcon);
                serialized.FindProperty("slot").intValue = i;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            EditorSceneManager.SaveScene(field.gameObject.scene);
            Debug.Log("[SlotTower Setup] 配置操作と操作バーの仮アイコンを作成した。");
        }

        private static TextAsset Table(string fileName) => AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + fileName);

        private static void Assign(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
