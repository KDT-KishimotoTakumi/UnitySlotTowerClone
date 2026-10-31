using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンの仮の地面（M0）を、マップ CSV から作る戦闘フィールド（T201）に置き換える。
    /// ウェーブごとのマップ（T305）は既存のフィールドにも Assign Wave Maps で設定できる。
    /// </summary>
    internal static class BattleFieldSetup
    {
        private const string TableFolder = "Assets/UnitySlotTower/Data/Tables/";
        private const string PlaceholderName = "Battle Field (Graybox)";
        private const int WaveCount = 5;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Battle Field")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] 戦闘フィールドは既にあるため作成しない。");
                return;
            }

            var placeholder = GameObject.Find(PlaceholderName);
            if (placeholder != null)
            {
                Object.DestroyImmediate(placeholder);
            }

            var field = new GameObject("Battle Field").AddComponent<BattleFieldView>();
            AssignTables(field);
            Debug.Log("[SlotTower Setup] 戦闘フィールドを作成した。");
        }

        [MenuItem("Tools/UnitySlotTower/Setup/Assign Wave Maps")]
        private static void AssignWaveMaps()
        {
            var field = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            if (field == null)
            {
                Debug.LogError("[SlotTower Setup] 戦闘フィールドが見つからない。");
                return;
            }

            AssignTables(field);
            Debug.Log($"[SlotTower Setup] 戦闘フィールドに W1〜W{WaveCount} のマップとウェーブ数を設定した。");
        }

        private static void AssignTables(BattleFieldView field)
        {
            var serialized = new SerializedObject(field);
            var maps = serialized.FindProperty("mapTables");
            maps.arraySize = WaveCount;
            for (var i = 0; i < WaveCount; i++)
            {
                maps.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + BattleMap.MapFileName($"w{i + 1}"));
            }

            serialized.FindProperty("pathsTable").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + BattleMap.PathsFileName);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var battle = field.GetComponent<BattleController>();
            if (battle != null)
            {
                var battleSerialized = new SerializedObject(battle);
                battleSerialized.FindProperty("waveCount").intValue = WaveCount;
                battleSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            EditorSceneManager.SaveScene(field.gameObject.scene);
        }
    }
}
