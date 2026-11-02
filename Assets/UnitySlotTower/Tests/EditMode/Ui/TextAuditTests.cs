using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Editor.Ui;

namespace UnitySlotTower.Tests.EditMode.Ui
{
    [Description("FR-003 / AC-001 Title 以外で文字を使わない")]
    public sealed class TextAuditTests
    {
        private const string FontAssetPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        private GameObject CreateText(string value)
        {
            _root = new GameObject("Root");
            var child = new GameObject("Label", typeof(RectTransform));
            child.transform.SetParent(_root.transform);
            child.AddComponent<TextMeshProUGUI>().text = value;
            return _root;
        }

        [Test]
        public void Text_With_Letters_Is_Reported()
        {
            Assert.That(TextAudit.FindViolations(CreateText("WAVE 3"), "test").Count, Is.EqualTo(1));
        }

        [Test]
        public void Numeric_Text_Is_Accepted()
        {
            Assert.That(TextAudit.FindViolations(CreateText("×" + "3"), "test"), Is.Empty);
        }

        [Test]
        public void Scenes_And_Prefabs_Use_No_Letters()
        {
            var violations = TextAudit.AuditProject();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void Numeric_Font_Contains_Exactly_The_Allowed_Characters()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null, FontAssetPath);

            var characters = new string(font.characterTable.Select(c => (char)c.unicode).OrderBy(c => c).ToArray());

            Assert.That(characters, Is.EqualTo(new string(NumericText.AllowedCharacters.OrderBy(c => c).ToArray())));
        }

        [Test]
        public void Numeric_Font_Keeps_Every_Glyph_In_Its_Single_Saved_Atlas()
        {
            // 2枚目以降のアトラスはアセットに保存されず、そこに入った字（T308 で見つけた / と %）は描けなかった。
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null, FontAssetPath);

            var outside = new string(font.characterTable.Where(c => c.glyph.atlasIndex != 0).Select(c => (char)c.unicode).ToArray());

            Assert.That((font.atlasTextures.Length, outside, AssetDatabase.IsSubAsset(font.atlasTextures[0])),
                Is.EqualTo((1, "", true)));
        }

        [Test]
        public void Numeric_Font_Cannot_Add_Characters_At_Runtime()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null, FontAssetPath);

            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        }
    }
}
