using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 操作バーのバフの残り時間ゲージ（spec 002 FR-115）。攻撃力上昇・攻撃速度上昇それぞれの残り時間を縦の塗りで示し、
    /// 効果がないあいだは印を暗くする。文字は使わない。
    /// </summary>
    public sealed class BuffGauges : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private Image attackFill;
        [SerializeField] private Graphic[] attackMarks = new Graphic[0];
        [SerializeField] private Image speedFill;
        [SerializeField] private Graphic[] speedMarks = new Graphic[0];
        [SerializeField] private Color inactiveTint = new Color(0.35f, 0.35f, 0.38f);

        private Color[] _attackColors;
        private Color[] _speedColors;
        private bool? _shownAttack;
        private bool? _shownSpeed;

        private void Awake()
        {
            _attackColors = ColorsOf(attackMarks);
            _speedColors = ColorsOf(speedMarks);
        }

        private void LateUpdate()
        {
            var run = battle.Run;
            if (run == null)
            {
                return;
            }

            var buffs = run.Buffs;
            attackFill.fillAmount = buffs.AttackRemaining;
            speedFill.fillAmount = buffs.SpeedRemaining;
            Tint(attackMarks, _attackColors, buffs.IsAttackActive, ref _shownAttack);
            Tint(speedMarks, _speedColors, buffs.IsSpeedActive, ref _shownSpeed);
        }

        private void Tint(Graphic[] marks, Color[] colors, bool active, ref bool? shown)
        {
            if (shown == active)
            {
                return;
            }

            shown = active;
            for (var i = 0; i < marks.Length; i++)
            {
                marks[i].color = active ? colors[i] : colors[i] * inactiveTint;
            }
        }

        private static Color[] ColorsOf(Graphic[] marks)
        {
            var colors = new Color[marks.Length];
            for (var i = 0; i < marks.Length; i++)
            {
                colors[i] = marks[i].color;
            }

            return colors;
        }
    }
}
