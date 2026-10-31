using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnitySlotTower.Core.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 数字と記号だけを含む静的な TextMeshPro フォントアセットを作り、TMP の既定フォントにする（T007、plan 8章）。
    /// それ以外の文字は字形がないため表示できない。
    /// </summary>
    internal static class NumericFontSetup
    {
        public const string FontAssetPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Tools/UnitySlotTower/Setup/Import TMP Essentials")]
        private static void ImportEssentials()
        {
            if (File.Exists(TmpSettingsPath))
            {
                Debug.LogWarning("[SlotTower Setup] TMP Essential Resources は導入済み。");
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        [MenuItem("Tools/UnitySlotTower/Setup/Create Numeric Font")]
        private static void CreateNumericFont()
        {
            if (File.Exists(FontAssetPath))
            {
                Debug.LogWarning($"[SlotTower Setup] {FontAssetPath} は既にあるため作成しない。");
                return;
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[SlotTower Setup] {SourceFontPath} がない。先に Import TMP Essentials を実行する。");
                return;
            }

            // 2枚目以降のアトラスは保存されないので、全字が1枚に入る大きさにし、複数アトラスは使わない（T308）。
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 512, 512,
                AtlasPopulationMode.Dynamic, false);
            font.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            if (!font.TryAddCharacters(NumericText.AllowedCharacters, out var missing))
            {
                Debug.LogError($"[SlotTower Setup] 元フォントに字形がない文字: {missing}");
                return;
            }

            font.atlasPopulationMode = AtlasPopulationMode.Static;

            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
            AssetDatabase.CreateAsset(font, FontAssetPath);
            font.atlasTexture.name = font.name + " Atlas";
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SlotTower Setup] {FontAssetPath} を作成し、TMP の既定フォントにした。");
        }
    }
}
