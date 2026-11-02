using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンの戦闘フィールドに、味方と敵のグレーボックス表示（T209）を付ける一回限りの手順。
    /// 既に付いていれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class BattleActorsSetup
    {
        [MenuItem("Tools/UnitySlotTower/Setup/Build Battle Actors View")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<BattleActorsView>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] 味方と敵の表示は既にあるため作成しない。");
                return;
            }

            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var camera = GameObject.Find("Battle Camera")?.GetComponent<Camera>();
            if (battle == null || camera == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの BattleController / Battle Camera が見つからない。先に Build Battle Placement を実行する。");
                return;
            }

            var field = battle.GetComponent<BattleFieldView>();
            var view = new GameObject("Actors View").AddComponent<BattleActorsView>();
            view.transform.SetParent(field.transform, false);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = field;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            EditorSceneManager.SaveScene(field.gameObject.scene);
            Debug.Log("[SlotTower Setup] 味方と敵の表示を作成した。");
        }
    }
}
