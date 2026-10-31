using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Slot;
using UnitySlotTower.Tutorial;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンをスロットと戦闘の連動（spec 002）に合わせる一回限りの手順。バッファーのアイコンと強化欄を外し、リールの図柄を
    /// 新しい6種類に差し替え、リプレイランプを当たり確定ランプにし、操作バーにバフのゲージ、フィールドにバフの表示、
    /// 筐体に当たりの演出を付け、REG の群れの表を戦闘に渡す。既に演出が付いていれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class SlotBattleLinkSetup
    {
        private const string TableFolder = "Assets/UnitySlotTower/Data/Tables/";

        // 7・BAR の後ろの子（旧: ベル・スイカ・チェリー・リプレイ）を差し替える。
        private const int KeptSymbolCount = 2;

        [MenuItem("Tools/UnitySlotTower/Setup/Apply Slot Battle Link")]
        private static void Apply()
        {
            if (Object.FindAnyObjectByType<SlotCelebration>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] スロットと戦闘の連動は既に反映済みのため何もしない。");
                return;
            }

            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var actors = Object.FindAnyObjectByType<BattleActorsView>(FindObjectsInactive.Include);
            var fieldView = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var director = Object.FindAnyObjectByType<TutorialDirector>(FindObjectsInactive.Include);
            var canvas = GameObject.Find("UI Canvas")?.GetComponent<RectTransform>();
            var bar = GameObject.Find("Operation Bar")?.GetComponent<RectTransform>();
            if (battle == null || cabinet == null || actors == null || fieldView == null || canvas == null || bar == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの戦闘・スロット筐体・味方表示・フィールド・キャンバス・操作バーが見つからない。");
                return;
            }

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            ReplaceSymbols(cabinet, knob, box);
            RenameNoticeLamp(cabinet);
            RemoveBufferIcons(bar, director);
            RemoveBufferLook(actors);

            var battleData = new SerializedObject(battle);
            battleData.FindProperty("regSwarmsTable").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + RegSwarmTable.FileName);
            battleData.ApplyModifiedPropertiesWithoutUndo();

            CreateBuffGauges(bar, battle, box);

            var aura = actors.gameObject.AddComponent<BuffAuraView>();
            var auraData = new SerializedObject(aura);
            auraData.FindProperty("battle").objectReferenceValue = battle;
            auraData.FindProperty("fieldView").objectReferenceValue = fieldView;
            auraData.ApplyModifiedPropertiesWithoutUndo();

            var celebration = cabinet.gameObject.AddComponent<SlotCelebration>();
            var celebrationData = new SerializedObject(celebration);
            celebrationData.FindProperty("cabinet").objectReferenceValue = cabinet;
            celebrationData.FindProperty("shakeTarget").objectReferenceValue = cabinet.GetComponent<RectTransform>();
            celebrationData.FindProperty("overlayParent").objectReferenceValue = canvas;
            celebrationData.FindProperty("audioSource").objectReferenceValue = cabinet.GetComponent<AudioSource>();
            celebrationData.FindProperty("sparkSprite").objectReferenceValue = knob;
            celebrationData.ApplyModifiedPropertiesWithoutUndo();

            var scene = cabinet.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] スロットと戦闘の連動をシーンに反映した。");
        }

        private static void ReplaceSymbols(SlotCabinet cabinet, Sprite knob, Sprite box)
        {
            var reels = new SerializedObject(cabinet).FindProperty("reels");
            for (var r = 0; r < reels.arraySize; r++)
            {
                var reel = (SlotReelView)reels.GetArrayElementAtIndex(r).objectReferenceValue;
                var cells = new SerializedObject(reel).FindProperty("cells");
                for (var c = 0; c < cells.arraySize; c++)
                {
                    var cell = (RectTransform)cells.GetArrayElementAtIndex(c).objectReferenceValue;
                    for (var i = cell.childCount - 1; i >= KeptSymbolCount; i--)
                    {
                        Object.DestroyImmediate(cell.GetChild(i).gameObject);
                    }

                    SlotCabinetSetup.CreateBuffSymbols(cell, knob, box);
                    for (var i = KeptSymbolCount; i < cell.childCount; i++)
                    {
                        cell.GetChild(i).gameObject.SetActive(false);
                    }
                }
            }
        }

        private static void RenameNoticeLamp(SlotCabinet cabinet)
        {
            var lamp = (Graphic)new SerializedObject(cabinet).FindProperty("noticeLamp").objectReferenceValue;
            if (lamp != null)
            {
                lamp.name = "Notice Lamp";
                lamp.color = SlotCabinetSetup.NoticeLampColor;
            }
        }

        // バッファーは5番目のアイコンと強化欄だった。説明の対象からも外す。
        private static void RemoveBufferIcons(RectTransform bar, TutorialDirector director)
        {
            foreach (var name in new[] { "Unit Icon 5", "Upgrade Icon 5" })
            {
                var icon = bar.Find(name);
                if (icon != null)
                {
                    Object.DestroyImmediate(icon.gameObject);
                }
            }

            if (director == null)
            {
                return;
            }

            var data = new SerializedObject(director);
            RemoveMissing(data.FindProperty("unitIcons"));
            RemoveMissing(data.FindProperty("upgradeIcons"));
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveMissing(SerializedProperty array)
        {
            for (var i = array.arraySize - 1; i >= 0; i--)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    array.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private static void RemoveBufferLook(BattleActorsView actors)
        {
            var data = new SerializedObject(actors);
            var looks = data.FindProperty("unitLooks");
            for (var i = looks.arraySize - 1; i >= 0; i--)
            {
                if (looks.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == "buffer")
                {
                    looks.DeleteArrayElementAtIndex(i);
                }
            }

            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // 外したバッファーのアイコンの場所（操作バーの左から5番目）に、攻撃力と攻撃速度の縦ゲージを並べる。
        private static void CreateBuffGauges(RectTransform bar, BattleController battle, Sprite box)
        {
            var root = CreateRect("Buff Gauges", bar, new Vector2(0.51f, 0.06f), new Vector2(0.61f, 0.94f));
            var attack = CreateGauge(root, "Attack", 0f, new Color(0.95f, 0.25f, 0.15f), box, out var attackFill);
            var attackArrow = CreateTriangle("Arrow", attack, new Vector2(0.15f, 0.78f), new Vector2(0.85f, 0.98f),
                new Color(0.95f, 0.25f, 0.15f));
            attackArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var speed = CreateGauge(root, "Speed", 0.52f, new Color(0.2f, 0.55f, 1f), box, out var speedFill);
            var speedMarks = new List<Object>();
            for (var i = 0; i < 2; i++)
            {
                speedMarks.Add(CreateTriangle("Chevron " + (i + 1), speed, new Vector2(0.05f + i * 0.4f, 0.8f),
                    new Vector2(0.55f + i * 0.4f, 0.96f), new Color(0.2f, 0.55f, 1f)));
            }

            var gauges = root.gameObject.AddComponent<BuffGauges>();
            var data = new SerializedObject(gauges);
            data.FindProperty("battle").objectReferenceValue = battle;
            data.FindProperty("attackFill").objectReferenceValue = attackFill;
            SetArray(data.FindProperty("attackMarks"), new Object[] { attackArrow });
            data.FindProperty("speedFill").objectReferenceValue = speedFill;
            SetArray(data.FindProperty("speedMarks"), speedMarks.ToArray());
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform CreateGauge(RectTransform root, string name, float left, Color color, Sprite box,
            out Image fill)
        {
            var gauge = CreateRect(name, root, new Vector2(left, 0f), new Vector2(left + 0.48f, 1f));
            var back = CreateImage("Back", gauge, new Vector2(0.2f, 0.02f), new Vector2(0.8f, 0.74f),
                new Color(0.08f, 0.08f, 0.1f), box);
            fill = CreateImage("Fill", back.rectTransform, Vector2.zero, Vector2.one, color, null);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;
            return gauge;
        }

        private static TriangleGraphic CreateTriangle(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            Color color)
        {
            var triangle = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TriangleGraphic>();
            triangle.color = color;
            triangle.raycastTarget = false;
            return triangle;
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
            Sprite sprite)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

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
