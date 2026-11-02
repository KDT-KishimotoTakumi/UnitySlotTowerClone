using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Core.Ui;

namespace UnitySlotTower.Editor.Ui
{
    /// <summary>
    /// Title 以外のシーンとプレハブの文字を走査し、数字・記号以外を使っている部品を見つける（FR-003、plan 8章）。
    /// </summary>
    public static class TextAudit
    {
        public const string SceneFolder = "Assets/UnitySlotTower/Scenes";
        public const string TitleScene = SceneFolder + "/Title.unity";
        public const string PrefabFolder = "Assets/UnitySlotTower";

        public static List<string> FindViolations(GameObject root, string context)
        {
            var violations = new List<string>();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                Check(text.text, text.transform, context, violations);
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                Check(text.text, text.transform, context, violations);
            }

            return violations;
        }

        /// <summary>プロジェクト全体を走査する。開いていなかったシーンは閉じ、開いていたシーンはそのまま残す。</summary>
        public static List<string> AuditProject()
        {
            var violations = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == TitleScene)
                {
                    continue;
                }

                var scene = SceneManager.GetSceneByPath(path);
                var wasLoaded = scene.isLoaded;
                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        violations.AddRange(FindViolations(root, path));
                    }
                }
                finally
                {
                    if (!wasLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    violations.AddRange(FindViolations(prefab, path));
                }
            }

            return violations;
        }

        private static void Check(string text, Transform transform, string context, List<string> violations)
        {
            var index = NumericText.FindInvalid(text);
            if (index >= 0)
            {
                violations.Add($"{context} {PathOf(transform)}: '{text}' の {index + 1} 文字目 '{text[index]}' は使えない");
            }
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}
