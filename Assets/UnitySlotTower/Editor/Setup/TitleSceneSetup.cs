using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Layout;
using UnitySlotTower.Flow;
using UnitySlotTower.Layout;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Title シーン（T401、plan 4章）を作る一回限りの手順。ロゴ（文字はここだけ可: FR-003）と ▶、設定1〜6の筐体プレートと ◀ を置き、
    /// ビルドの先頭に登録する。既存のシーンは上書きしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class TitleSceneSetup
    {
        public const string ScenePath = "Assets/UnitySlotTower/Scenes/Title.unity";
        private const string NumericFontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string LogoFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        // 設定1（機械割が低く難しい）から6へ、青→赤に寄せる仮の色。
        private static readonly Color[] PlateColors =
        {
            new Color(0.2f, 0.35f, 0.75f), new Color(0.2f, 0.55f, 0.7f), new Color(0.25f, 0.6f, 0.35f),
            new Color(0.75f, 0.65f, 0.2f), new Color(0.8f, 0.45f, 0.2f), new Color(0.8f, 0.2f, 0.25f),
        };

        private static Sprite _box;

        [MenuItem("Tools/UnitySlotTower/Setup/Create Title Scene")]
        private static void CreateTitleScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.LogWarning($"[SlotTower Setup] {ScenePath} は既にあるため作成しない。");
                return;
            }

            // 自動化から呼ぶため保存ダイアログは出さず、未保存の変更があれば中止する。
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[SlotTower Setup] 未保存のシーンがあるため中止した。保存してから再実行する。");
                    return;
                }
            }

            var numericFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NumericFontPath);
            var logoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LogoFontPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (numericFont == null || logoFont == null || actions == null)
            {
                Debug.LogError("[SlotTower Setup] フォントか InputActionAsset が見つからない。");
                return;
            }

            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Background Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.gameObject.tag = "MainCamera";
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

            var scaler = CreateCanvas(out var root);
            var backdrop = CreateImage("Backdrop", root, Vector2.zero, Vector2.one, new Color(0.1f, 0.08f, 0.14f));
            backdrop.raycastTarget = true;

            // タイトル: ロゴと ▶。
            var title = CreateRect("Title Panel", root, Vector2.zero, Vector2.one);
            var logo = CreateRect("Logo", title, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.82f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            logo.font = logoFont;
            logo.text = "SLOT TOWER";
            logo.color = new Color(1f, 0.85f, 0.3f);
            logo.fontStyle = FontStyles.Bold;
            logo.enableAutoSizing = true;
            logo.fontSizeMin = 10f;
            logo.fontSizeMax = 300f;
            logo.alignment = TextAlignmentOptions.Center;
            logo.raycastTarget = false;

            var start = CreateButton("Start Button", title, new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.42f),
                new Color(0.25f, 0.6f, 0.3f));
            CreateRect("Play", start.GetComponent<RectTransform>(), new Vector2(0.4f, 0.2f), new Vector2(0.64f, 0.8f))
                .gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;

            // 難易度選択: 設定1〜6の筐体プレート（3列×2段）と ◀。
            var difficulty = CreateRect("Difficulty Panel", root, Vector2.zero, Vector2.one);
            var plates = new Button[GameSession.MaxSetting];
            for (var i = 0; i < plates.Length; i++)
            {
                var column = i % 3;
                var row = i / 3;
                var min = new Vector2(0.08f + column * 0.3f, 0.6f - row * 0.26f);
                var plate = CreateButton($"Setting {i + 1}", difficulty, min, min + new Vector2(0.24f, 0.22f), PlateColors[i]);
                plate.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                var plateRect = plate.GetComponent<RectTransform>();
                CreateImage("Reel Window", plateRect, new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.85f),
                    new Color(0.92f, 0.92f, 0.9f));
                var number = CreateRect("Number", plateRect, new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.5f))
                    .gameObject.AddComponent<TextMeshProUGUI>();
                number.font = numericFont;
                number.text = (i + 1).ToString();
                number.color = Color.white;
                number.enableAutoSizing = true;
                number.fontSizeMin = 10f;
                number.fontSizeMax = 300f;
                number.alignment = TextAlignmentOptions.Center;
                number.raycastTarget = false;
                plates[i] = plate;
            }

            var back = CreateButton("Back Button", difficulty, new Vector2(0.06f, 0.86f), new Vector2(0.2f, 0.94f),
                new Color(0.3f, 0.3f, 0.34f));
            var arrow = CreateRect("Arrow", back.GetComponent<RectTransform>(), new Vector2(0.34f, 0.2f), new Vector2(0.62f, 0.8f));
            arrow.localEulerAngles = new Vector3(0f, 0f, 180f);
            arrow.gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;
            difficulty.gameObject.SetActive(false);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().firstSelectedGameObject = start.gameObject;

            var layout = new GameObject("Portrait Screen Layout").AddComponent<PortraitScreenLayout>();
            Assign(layout, "canvasScaler", scaler);

            var screen = new GameObject("Title Screen").AddComponent<TitleScreen>();
            Assign(screen, "inputActions", actions);
            Assign(screen, "titlePanel", title.gameObject);
            Assign(screen, "difficultyPanel", difficulty.gameObject);
            Assign(screen, "startButton", start);
            Assign(screen, "backButton", back);
            var serialized = new SerializedObject(screen);
            var buttons = serialized.FindProperty("settingButtons");
            buttons.arraySize = plates.Length;
            for (var i = 0; i < plates.Length; i++)
            {
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = plates[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildFirst();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SlotTower Setup] {ScenePath} を作成した。");
        }

        private static CanvasScaler CreateCanvas(out RectTransform root)
        {
            var canvasObject = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(PortraitLayout.ReferenceWidth, PortraitLayout.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            root = new GameObject("Portrait Root", typeof(RectTransform)).GetComponent<RectTransform>();
            root.gameObject.layer = canvasObject.layer;
            root.SetParent(canvasObject.transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(PortraitLayout.ReferenceWidth, PortraitLayout.ReferenceHeight);
            return scaler;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, color);
            image.sprite = _box;
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            // キーボードで選んでいるボタンが分かるように、選んでいないボタンを暗くする。
            var colors = button.colors;
            colors.normalColor = new Color(0.65f, 0.65f, 0.65f);
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            return button;
        }

        private static void Assign(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildFirst()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath)
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
