using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 操作バーの味方アイコン（T308）。押すとその味方を選び、そのままフィールドへドラッグして離すとマスが決まる。
    /// コスト（ストックがあれば ×個数）、待ち時間ゲージ、召喚できないときの暗転、選択中の枠を表示する。
    /// </summary>
    public sealed class UnitBarIcon : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private UnitPlacementInput placementInput;
        [SerializeField] private BattleController battle;
        [Tooltip("units.csv の並びで 0 始まり。")]
        [SerializeField] private int slot;

        [SerializeField] private Image body;
        [Tooltip("待ち時間の残りの割合だけ下から覆う（Filled・Vertical）。")]
        [SerializeField] private Image cooldownGauge;
        [SerializeField] private Image selectedFrame;
        [SerializeField] private TMP_Text costText;
        [Tooltip("無料ストックの個数（×n）。ストックがないときは隠す。")]
        [SerializeField] private TMP_Text stockText;
        [Tooltip("召喚できないときに本体の色にかける。")]
        [SerializeField] private Color unavailableTint = new Color(0.45f, 0.45f, 0.45f);

        private Color _bodyColor;
        private int _shownCost = -1;
        private int _shownStock = -1;
        private bool _shownAvailable = true;

        private void Awake()
        {
            _bodyColor = body.color;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                placementInput.SelectSlot(slot);
            }
        }

        private void LateUpdate()
        {
            var summoner = battle.Summoner;
            if (summoner == null || slot >= battle.Units.All.Count)
            {
                return;
            }

            var unit = battle.Units.All[slot];
            var stock = battle.Run.Stock.Count(unit.Id);
            if (stock != _shownStock)
            {
                _shownStock = stock;
                stockText.gameObject.SetActive(stock > 0);
                stockText.SetText("×{0}", stock);
            }

            if (unit.Cost != _shownCost)
            {
                _shownCost = unit.Cost;
                costText.SetText("{0}", unit.Cost);
            }

            var remaining = summoner.IgnoresCooldown ? 0d : summoner.CooldownRemaining(unit.Id);
            cooldownGauge.fillAmount = unit.Cooldown > 0d ? (float)(remaining / unit.Cooldown) : 0f;

            var available = summoner.Check(unit.Id) == SummonResult.Summoned;
            if (available != _shownAvailable)
            {
                _shownAvailable = available;
                body.color = available ? _bodyColor : _bodyColor * unavailableTint;
            }

            var selected = battle.Deployment.Selected;
            selectedFrame.enabled = selected != null && selected.Id == unit.Id;
        }
    }
}
