using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// スロットのバフの表示（spec 002 FR-115）。攻撃力上昇・攻撃速度上昇の効果中は、配置中の味方の足元に色つきの輪を脈打たせ
    /// （両方なら2重）、HP回復が揃ったときは味方ごとに緑の光の柱を立ち上らせる。文字は使わない。
    /// </summary>
    public sealed class BuffAuraView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Color attackColor = new Color(1f, 0.3f, 0.2f);
        [SerializeField] private Color speedColor = new Color(0.25f, 0.65f, 1f);
        [SerializeField] private Color healColor = new Color(0.35f, 1f, 0.45f);
        [SerializeField] private float ringSize = 1.25f;
        [SerializeField] private float pulsePerSecond = 2f;
        [SerializeField] private float healSeconds = 0.9f;

        private sealed class Aura
        {
            public Transform Attack;
            public Transform Speed;
        }

        private readonly Dictionary<BattleUnit, Aura> _auras = new Dictionary<BattleUnit, Aura>();
        private readonly List<BattleUnit> _gone = new List<BattleUnit>();
        private readonly List<(Transform Glow, BattleUnit Unit, float Until)> _heals =
            new List<(Transform, BattleUnit, float)>();
        private readonly HashSet<BattleUnit> _seen = new HashSet<BattleUnit>();
        private MaterialPropertyBlock _block;
        private Transform _root;
        private WaveRun _run;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _root = new GameObject("Buff Auras").transform;
            _root.SetParent(transform, false);
        }

        private void OnDestroy()
        {
            if (_run != null)
            {
                _run.SlotRoleApplied -= OnSlotRoleApplied;
            }
        }

        private void LateUpdate()
        {
            var run = battle.Run;
            if (run == null)
            {
                return;
            }

            if (_run != run)
            {
                _run = run;
                _run.SlotRoleApplied += OnSlotRoleApplied;
            }

            var buffs = run.Buffs;
            var pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * pulsePerSecond * 2f * Mathf.PI);
            _seen.Clear();
            foreach (var unit in run.Field.Units)
            {
                if (!unit.IsAlive)
                {
                    continue;
                }

                _seen.Add(unit);
                if (!_auras.TryGetValue(unit, out var aura))
                {
                    aura = new Aura
                    {
                        Attack = CreateRing("Attack Aura", attackColor),
                        Speed = CreateRing("Speed Aura", speedColor),
                    };
                    _auras.Add(unit, aura);
                }

                var position = fieldView.CellTopToWorld(unit.Cell);
                Place(aura.Attack, buffs.IsAttackActive, position + Vector3.up * 0.02f, ringSize * pulse);
                Place(aura.Speed, buffs.IsSpeedActive, position + Vector3.up * 0.03f,
                    ringSize * (buffs.IsAttackActive ? 0.82f : 1f) * pulse);
            }

            _gone.Clear();
            foreach (var pair in _auras)
            {
                if (!_seen.Contains(pair.Key))
                {
                    _gone.Add(pair.Key);
                }
            }

            foreach (var unit in _gone)
            {
                Destroy(_auras[unit].Attack.gameObject);
                Destroy(_auras[unit].Speed.gameObject);
                _auras.Remove(unit);
            }

            UpdateHeals();
        }

        private void OnSlotRoleApplied(string role)
        {
            if (role != BattleBuffs.HealRole)
            {
                return;
            }

            foreach (var unit in _run.Field.Units)
            {
                if (unit.IsAlive)
                {
                    var glow = CreatePrimitive(PrimitiveType.Cylinder, "Heal Glow", healColor);
                    _heals.Add((glow, unit, Time.time + healSeconds));
                }
            }
        }

        // 光の柱を伸ばしながら細くし、時間が来たら消す。
        private void UpdateHeals()
        {
            for (var i = _heals.Count - 1; i >= 0; i--)
            {
                var (glow, unit, until) = _heals[i];
                var left = until - Time.time;
                if (left <= 0f)
                {
                    Destroy(glow.gameObject);
                    _heals.RemoveAt(i);
                    continue;
                }

                var t = 1f - left / healSeconds;
                var height = Mathf.Lerp(0.2f, 1.6f, t);
                var width = Mathf.Lerp(0.9f, 0.2f, t);
                glow.position = fieldView.CellTopToWorld(unit.Cell) + Vector3.up * height * 0.5f;
                glow.localScale = new Vector3(width, height * 0.5f, width);
            }
        }

        private static void Place(Transform ring, bool active, Vector3 position, float size)
        {
            if (ring.gameObject.activeSelf != active)
            {
                ring.gameObject.SetActive(active);
            }

            if (active)
            {
                ring.position = position;
                ring.localScale = new Vector3(size, 0.01f, size);
            }
        }

        private Transform CreateRing(string name, Color color)
        {
            var ring = CreatePrimitive(PrimitiveType.Cylinder, name, color);
            ring.gameObject.SetActive(false);
            return ring;
        }

        private Transform CreatePrimitive(PrimitiveType shape, string name, Color color)
        {
            var primitive = GameObject.CreatePrimitive(shape);
            primitive.name = name;
            // 配置のレイキャストがマスの箱だけに当たるよう、当たり判定を持たせない。
            Destroy(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(_root, false);
            _block.SetColor(BaseColor, color);
            primitive.GetComponent<Renderer>().SetPropertyBlock(_block);
            return primitive.transform;
        }
    }
}
