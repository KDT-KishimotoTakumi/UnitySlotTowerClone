using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンの「Slot Area」に仮のスロット筐体（T110）を組む一回限りの手順。既に筐体があれば何もしない。
    /// 以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class SlotCabinetSetup
    {
        private const string TableFolder = "Assets/UnitySlotTower/Data/Tables/";
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const int CellCount = 5;

        /// <summary>当たり確定ランプの色（spec 002 FR-121）。</summary>
        internal static readonly Color NoticeLampColor = new Color(1f, 0.35f, 0.85f);

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Slot Cabinet")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] スロット筐体は既にあるため作成しない。");
                return;
            }

            var slotArea = GameObject.Find("Slot Area")?.GetComponent<RectTransform>();
            var window = slotArea != null ? slotArea.Find("Reel Window") as RectTransform : null;
            if (window == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Slot Area / Reel Window が見つからない。");
                return;
            }

            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var cabinet = slotArea.gameObject.AddComponent<SlotCabinet>();
            var audio = slotArea.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            var reels = new SlotReelView[SlotReels.ReelCount];
            var stops = new Button[SlotReels.ReelCount];
            for (var i = 0; i < reels.Length; i++)
            {
                var left = 0.03f + i * 0.32f;
                reels[i] = CreateReel(window, i, new Vector2(left, 0.02f), new Vector2(left + 0.3f, 0.98f));

                // ストップボタンは各リールの真下に置く。
                var center = Mathf.Lerp(window.anchorMin.x, window.anchorMax.x, left + 0.15f);
                stops[i] = CreateButton($"Stop Button {i + 1}", slotArea, new Vector2(center - 0.07f, 0.2f),
                    new Vector2(center + 0.07f, 0.3f), new Color(0.85f, 0.15f, 0.15f));
            }

            var lever = CreateButton("Lever Button", slotArea, new Vector2(0.04f, 0.04f), new Vector2(0.2f, 0.16f),
                new Color(0.85f, 0.85f, 0.88f));

            var premonition = CreateImage("Premonition Lamp", slotArea, new Vector2(0.1f, 0.915f), new Vector2(0.42f, 0.975f),
                new Color(1f, 0.55f, 0.05f), _box, false);
            var bonus = CreateImage("Bonus Lamp", slotArea, new Vector2(0.47f, 0.91f), new Vector2(0.6f, 0.98f),
                new Color(1f, 0.2f, 0.2f), _knob, true);
            var kakuhen = CreateImage("Kakuhen Lamp", slotArea, new Vector2(0.62f, 0.91f), new Vector2(0.75f, 0.98f),
                new Color(0.9f, 0.3f, 1f), _knob, true);
            var notice = CreateImage("Notice Lamp", slotArea, new Vector2(0.77f, 0.91f), new Vector2(0.9f, 0.98f),
                NoticeLampColor, _knob, true);

            var coin = CreateImage("Coin Icon", slotArea, new Vector2(0.1f, 0.365f), new Vector2(0.17f, 0.425f),
                new Color(1f, 0.82f, 0.2f), _knob, true);
            var credit = CreateNumber("Credit", slotArea, new Vector2(0.18f, 0.36f), new Vector2(0.42f, 0.43f), Color.white);
            var payout = CreateNumber("Payout", slotArea, new Vector2(0.45f, 0.36f), new Vector2(0.65f, 0.43f),
                new Color(1f, 0.82f, 0.2f));
            var counter = CreateNumber("Bonus Or Kakuhen Counter", slotArea, new Vector2(0.68f, 0.36f),
                new Vector2(0.9f, 0.43f), new Color(0.9f, 0.6f, 1f));

            var serialized = new SerializedObject(cabinet);
            SetTable(serialized, "gameConfigTable", "game_config.csv");
            SetTable(serialized, "reelsTable", SlotReels.FileName);
            SetTable(serialized, "payoutsTable", SlotPayouts.FileName);
            SetTable(serialized, "probabilitiesTable", SlotProbabilities.FileName);
            SetTable(serialized, "premonitionTable", SlotPremonition.FileName);
            SetTable(serialized, "rulesTable", SlotRules.FileName);
            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serialized.FindProperty("leverButton").objectReferenceValue = lever;
            SetArray(serialized.FindProperty("stopButtons"), stops);
            SetArray(serialized.FindProperty("reels"), reels);
            serialized.FindProperty("premonitionLamp").objectReferenceValue = premonition;
            serialized.FindProperty("bonusLamp").objectReferenceValue = bonus;
            serialized.FindProperty("kakuhenLamp").objectReferenceValue = kakuhen;
            serialized.FindProperty("noticeLamp").objectReferenceValue = notice;
            serialized.FindProperty("coinIcon").objectReferenceValue = coin;
            serialized.FindProperty("creditText").objectReferenceValue = credit;
            serialized.FindProperty("payoutText").objectReferenceValue = payout;
            serialized.FindProperty("counterText").objectReferenceValue = counter;
            serialized.FindProperty("audioSource").objectReferenceValue = audio;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // M0 のカメラには AudioListener がないので、先バレ音のために戦闘カメラへ付ける。
            if (Object.FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }

            var scene = slotArea.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] スロット筐体を作成した。");
        }

        private static SlotReelView CreateReel(RectTransform window, int index, Vector2 anchorMin, Vector2 anchorMax)
        {
            var reel = CreateImage($"Reel {index + 1}", window, anchorMin, anchorMax, Color.white, null, false);
            reel.gameObject.AddComponent<RectMask2D>();
            var view = reel.gameObject.AddComponent<SlotReelView>();
            var cells = new RectTransform[CellCount];
            for (var c = 0; c < CellCount; c++)
            {
                var cell = CreateRect($"Cell {c - CellCount / 2}", reel.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
                cells[c] = cell;
                CreateSymbols(cell);
            }

            var serialized = new SerializedObject(view);
            SetArray(serialized.FindProperty("cells"), cells);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 子の順番は SlotSymbol の順（SlotReelView が番号で切り替える）。文字は数字の 7 だけを使う。
        private static void CreateSymbols(RectTransform cell)
        {
            var seven = CreateNumber("Seven", cell, Vector2.zero, Vector2.one, new Color(0.9f, 0.05f, 0.1f));
            seven.text = "7";
            seven.fontStyle = FontStyles.Bold;

            CreateImage("Bar", cell, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.68f), new Color(0.1f, 0.1f, 0.12f), _box, false);
            CreateBuffSymbols(cell, _knob, _box);

            for (var i = 0; i < cell.childCount; i++)
            {
                cell.GetChild(i).gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 7・BAR に続く図柄（spec 002 E-06）を SlotSymbol の順に足す。攻撃力は赤い上向き三角、攻撃速度は青い右向きの二重三角、
        /// HP回復は緑の十字、コインは金の二重丸。
        /// </summary>
        internal static void CreateBuffSymbols(RectTransform cell, Sprite knob, Sprite box)
        {
            var attack = CreateRect("Attack", cell, new Vector2(0.18f, 0.1f), new Vector2(0.82f, 0.9f));
            var arrow = CreateRect("Arrow", attack, Vector2.zero, Vector2.one).gameObject.AddComponent<TriangleGraphic>();
            arrow.color = new Color(0.9f, 0.15f, 0.1f);
            arrow.raycastTarget = false;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var speed = CreateRect("Speed", cell, new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.82f));
            for (var i = 0; i < 2; i++)
            {
                var chevron = CreateRect("Chevron " + (i + 1), speed, new Vector2(i * 0.45f, 0f), new Vector2(0.55f + i * 0.45f, 1f))
                    .gameObject.AddComponent<TriangleGraphic>();
                chevron.color = new Color(0.15f, 0.45f, 0.95f);
                chevron.raycastTarget = false;
            }

            var heal = CreateRect("Heal", cell, Vector2.zero, Vector2.one);
            var green = new Color(0.15f, 0.75f, 0.3f);
            CreateImage("Cross V", heal, new Vector2(0.38f, 0.12f), new Vector2(0.62f, 0.88f), green, box, false);
            CreateImage("Cross H", heal, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.68f), green, box, false);

            var coin = CreateImage("Coin", cell, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.92f), new Color(1f, 0.8f, 0.15f), knob, true);
            CreateImage("Inner", coin.rectTransform, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Color(0.85f, 0.6f, 0.05f), knob, true);
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

        private static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
            Sprite sprite, bool preserveAspect)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, color, _knob, true);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static TextMeshProUGUI CreateNumber(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            Color color)
        {
            var text = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = "0";
            text.color = color;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 200f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void SetTable(SerializedObject serialized, string property, string fileName) =>
            serialized.FindProperty(property).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + fileName);

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
