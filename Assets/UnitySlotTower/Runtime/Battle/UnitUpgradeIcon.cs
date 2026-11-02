using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 操作バーの種類ごとの強化アイコン（T308）。クリックで1段階強化する。強化段階を点の数、次の強化費用を数字で表示し、
    /// 強化できない（コイン不足・最大段階）ときは暗くする。最大段階では費用を隠す。
    /// </summary>
    public sealed class UnitUpgradeIcon : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private UnitPlacementInput placementInput;
        [SerializeField] private BattleController battle;
        [Tooltip("units.csv の並びで 0 始まり。")]
        [SerializeField] private int slot;

        [SerializeField] private Graphic arrow;
        [SerializeField] private TMP_Text costText;
        [Tooltip("強化段階の点（左から段階 1, 2, ...）。")]
        [SerializeField] private Image[] levelPips;
        [SerializeField] private Color pipOnColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color pipOffColor = new Color(0.2f, 0.2f, 0.22f);
        [SerializeField] private Color availableColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color unavailableColor = new Color(0.4f, 0.4f, 0.4f);

        private int _shownLevel = -1;
        private int _shownCost = -1;
        private int _shownAvailable = -1;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                placementInput.UpgradeSlot(slot);
            }
        }

        private void LateUpdate()
        {
            var upgrades = battle.Upgrades;
            if (upgrades == null || slot >= battle.Units.All.Count)
            {
                return;
            }

            var unitId = battle.Units.All[slot].Id;
            var level = upgrades.Level(unitId);
            if (level != _shownLevel)
            {
                _shownLevel = level;
                for (var i = 0; i < levelPips.Length; i++)
                {
                    levelPips[i].color = i < level ? pipOnColor : pipOffColor;
                }
            }

            var cost = upgrades.NextCost(unitId) ?? 0;
            if (cost != _shownCost)
            {
                _shownCost = cost;
                costText.gameObject.SetActive(cost > 0);
                costText.SetText("{0}", cost);
            }

            var available = upgrades.Check(unitId) == UpgradeResult.Upgraded ? 1 : 0;
            if (available != _shownAvailable)
            {
                _shownAvailable = available;
                arrow.color = available == 1 ? availableColor : unavailableColor;
            }
        }
    }
}
