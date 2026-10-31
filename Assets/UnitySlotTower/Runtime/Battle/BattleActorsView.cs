using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 味方と敵のグレーボックス表示（T209、plan 11章）。味方は種類ごとの色と形・向きの印・攻撃範囲、
    /// 敵は種類ごとの形（近距離は箱、遠距離は円柱、空中は浮いた球と地面の影。ブロック消費が大きいほど大きい）、
    /// どちらにもカメラを向く HP ゲージを付ける。M5 で素材に差し替える。
    /// </summary>
    public sealed class BattleActorsView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Serializable]
        private struct UnitLook
        {
            public string id;
            public PrimitiveType shape;
            public Vector3 size;
            public Color color;
        }

        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [Tooltip("HP ゲージを向けるカメラ。")]
        [SerializeField] private Camera battleCamera;

        [Header("Units")]
        [Tooltip("units.csv の id ごとの形・大きさ・色（操作バーのアイコンと同じ色）。")]
        [SerializeField] private UnitLook[] unitLooks =
        {
            new UnitLook { id = "tank", shape = PrimitiveType.Cube, size = new Vector3(0.75f, 0.5f, 0.75f), color = new Color(0.45f, 0.55f, 0.75f) },
            new UnitLook { id = "sword", shape = PrimitiveType.Cube, size = new Vector3(0.4f, 0.75f, 0.4f), color = new Color(0.85f, 0.3f, 0.25f) },
            new UnitLook { id = "archer", shape = PrimitiveType.Cylinder, size = new Vector3(0.45f, 0.4f, 0.45f), color = new Color(0.35f, 0.75f, 0.35f) },
            new UnitLook { id = "healer", shape = PrimitiveType.Sphere, size = new Vector3(0.6f, 0.6f, 0.6f), color = new Color(0.95f, 0.75f, 0.85f) },
            new UnitLook { id = "buffer", shape = PrimitiveType.Capsule, size = new Vector3(0.45f, 0.4f, 0.45f), color = new Color(0.95f, 0.8f, 0.25f) },
        };
        [SerializeField] private Color unknownUnitColor = new Color(0.8f, 0.8f, 0.8f);
        [SerializeField] private Color noseColor = new Color(0.15f, 0.15f, 0.18f);
        [Tooltip("攻撃範囲の印の明るさ（味方の色にかける）。")]
        [SerializeField, Range(0f, 1f)] private float rangeBrightness = 0.8f;

        [Header("Enemies")]
        [SerializeField] private Color meleeEnemyColor = new Color(0.55f, 0.12f, 0.15f);
        [SerializeField] private Color rangedEnemyColor = new Color(0.6f, 0.2f, 0.55f);
        [SerializeField] private Color airEnemyColor = new Color(0.35f, 0.2f, 0.6f);
        [SerializeField] private Color shadowColor = new Color(0.1f, 0.1f, 0.12f);
        [SerializeField] private float enemySize = 0.5f;

        [Header("HP Gauge")]
        [SerializeField] private Vector2 gaugeSize = new Vector2(0.8f, 0.1f);
        [SerializeField] private Color gaugeBackColor = new Color(0.08f, 0.08f, 0.08f);
        [SerializeField] private Color unitGaugeColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color enemyGaugeColor = new Color(0.95f, 0.3f, 0.25f);

        private sealed class Actor
        {
            public Transform Root;
            public Transform Gauge;
            public Transform Fill;
            public int Hp = -1;
            public int Seen;
        }

        private readonly Dictionary<BattleUnit, Actor> _units = new Dictionary<BattleUnit, Actor>();
        private readonly Dictionary<BattleEnemy, Actor> _enemies = new Dictionary<BattleEnemy, Actor>();
        private readonly List<BattleUnit> _goneUnits = new List<BattleUnit>();
        private readonly List<BattleEnemy> _goneEnemies = new List<BattleEnemy>();
        private readonly Dictionary<GridPoint, Color> _rangeCells = new Dictionary<GridPoint, Color>();
        private readonly List<Transform> _rangeMarkers = new List<Transform>();
        private MaterialPropertyBlock _block;
        private Transform _unitRoot;
        private Transform _enemyRoot;
        private Transform _rangeRoot;
        private bool _rangesDirty;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _rangeRoot = CreateGroup("Unit Ranges");
            _unitRoot = CreateGroup("Units");
            _enemyRoot = CreateGroup("Enemies");
        }

        private void LateUpdate()
        {
            var field = battle.Field;
            if (field == null)
            {
                return;
            }

            var frame = Time.frameCount;
            SyncUnits(field.Units, frame);
            SyncEnemies(field.Enemies, frame);
            if (_rangesDirty)
            {
                _rangesDirty = false;
                RebuildRanges();
            }
        }

        private void SyncUnits(IReadOnlyList<BattleUnit> units, int frame)
        {
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.IsAlive)
                {
                    continue;
                }

                if (!_units.TryGetValue(unit, out var actor))
                {
                    actor = CreateUnit(unit);
                    _units.Add(unit, actor);
                    _rangesDirty = true;
                }

                actor.Seen = frame;
                UpdateGauge(actor, unit.Hp, unit.MaxHp);
            }

            _goneUnits.Clear();
            foreach (var pair in _units)
            {
                if (pair.Value.Seen != frame)
                {
                    _goneUnits.Add(pair.Key);
                }
            }

            foreach (var unit in _goneUnits)
            {
                Destroy(_units[unit].Root.gameObject);
                _units.Remove(unit);
                _rangesDirty = true;
            }
        }

        private void SyncEnemies(IReadOnlyList<BattleEnemy> enemies, int frame)
        {
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!enemy.IsAlive || enemy.ReachedGoal)
                {
                    continue;
                }

                if (!_enemies.TryGetValue(enemy, out var actor))
                {
                    actor = CreateEnemy(enemy);
                    _enemies.Add(enemy, actor);
                }

                actor.Seen = frame;
                actor.Root.position = fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir);
                UpdateGauge(actor, enemy.Hp, enemy.Definition.MaxHp);
            }

            _goneEnemies.Clear();
            foreach (var pair in _enemies)
            {
                if (pair.Value.Seen != frame)
                {
                    _goneEnemies.Add(pair.Key);
                }
            }

            foreach (var enemy in _goneEnemies)
            {
                Destroy(_enemies[enemy].Root.gameObject);
                _enemies.Remove(enemy);
            }
        }

        private Actor CreateUnit(BattleUnit unit)
        {
            var look = LookOf(unit.Definition.Id);
            var root = new GameObject($"Unit {unit.Definition.Id} {unit.Cell.X},{unit.Cell.Y}").transform;
            root.SetParent(_unitRoot, false);
            root.SetPositionAndRotation(fieldView.CellTopToWorld(unit.Cell), Quaternion.LookRotation(Direction(unit.Facing)));

            var height = BodyHeight(look.shape, look.size.y);
            var body = CreatePrimitive(look.shape, "Body", root, look.color);
            body.localScale = look.size;
            body.localPosition = Vector3.up * (height * 0.5f);

            // 向きの印: 体の前面から突き出した小さな箱。
            var nose = CreatePrimitive(PrimitiveType.Cube, "Nose", root, noseColor);
            nose.localScale = new Vector3(0.15f, 0.15f, 0.3f);
            nose.localPosition = new Vector3(0f, height * 0.5f, look.size.z * 0.5f + 0.1f);

            return CreateActor(root, height, unitGaugeColor);
        }

        private Actor CreateEnemy(BattleEnemy enemy)
        {
            var definition = enemy.Definition;
            var root = new GameObject($"Enemy {definition.Id}").transform;
            root.SetParent(_enemyRoot, false);

            // ボス（ブロック消費 2 以上）ほど大きくする。
            var size = enemySize * (1f + 0.35f * (Mathf.Max(1, definition.BlockCost) - 1));
            PrimitiveType shape;
            Color color;
            if (definition.IsAir)
            {
                shape = PrimitiveType.Sphere;
                color = airEnemyColor;
            }
            else if (definition.Range > 0)
            {
                shape = PrimitiveType.Cylinder;
                color = rangedEnemyColor;
            }
            else
            {
                shape = PrimitiveType.Cube;
                color = meleeEnemyColor;
            }

            var scale = shape == PrimitiveType.Cylinder ? new Vector3(size, size * 0.5f, size) : Vector3.one * size;
            var body = CreatePrimitive(shape, "Body", root, color);
            body.localScale = scale;
            if (definition.IsAir)
            {
                // 空中の敵は空中経路の高さに浮かべ、真下の地面に影を置く。
                var lift = fieldView.PositionToWorld(0, 0, true) - fieldView.PositionToWorld(0, 0, false);
                var shadow = CreatePrimitive(PrimitiveType.Cylinder, "Shadow", root, shadowColor);
                shadow.localScale = new Vector3(size * 0.9f, 0.005f, size * 0.9f);
                shadow.localPosition = Vector3.down * (lift.magnitude - 0.01f);
                return CreateActor(root, size * 0.5f, enemyGaugeColor);
            }

            var height = BodyHeight(shape, scale.y);
            body.localPosition = Vector3.up * (height * 0.5f);
            return CreateActor(root, height, enemyGaugeColor);
        }

        /// <summary>体の上に HP ゲージ（暗い背景と、HP 割合だけ左から伸びる中身）を付ける。</summary>
        private Actor CreateActor(Transform root, float top, Color fillColor)
        {
            var gauge = new GameObject("HP Gauge").transform;
            gauge.SetParent(root, false);
            gauge.localPosition = Vector3.up * (top + 0.25f);


            var back = CreatePrimitive(PrimitiveType.Quad, "Back", gauge, gaugeBackColor);
            back.localScale = new Vector3(gaugeSize.x + 0.04f, gaugeSize.y + 0.04f, 1f);
            var fill = CreatePrimitive(PrimitiveType.Quad, "Fill", gauge, fillColor);
            fill.localScale = new Vector3(gaugeSize.x, gaugeSize.y, 1f);
            // Quad の表は -z 側。カメラの向きに揃えたとき背景より手前に出す。
            fill.localPosition = new Vector3(0f, 0f, -0.01f);
            return new Actor { Root = root, Gauge = gauge, Fill = fill };
        }

        private void UpdateGauge(Actor actor, int hp, int maxHp)
        {
            // カメラと同じ向きにして、常に正面から見えるようにする。
            if (battleCamera != null)
            {
                actor.Gauge.rotation = battleCamera.transform.rotation;
            }

            if (actor.Hp == hp)
            {
                return;
            }

            actor.Hp = hp;
            var ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            var width = gaugeSize.x * ratio;
            actor.Fill.localScale = new Vector3(width, gaugeSize.y, 1f);
            actor.Fill.localPosition = new Vector3((width - gaugeSize.x) * 0.5f, 0f, -0.01f);
        }

        /// <summary>配置中の味方の攻撃範囲のマスに印を置く。重なるマスは後から置いた味方の色にする。</summary>
        private void RebuildRanges()
        {
            _rangeCells.Clear();
            var units = battle.Field.Units;
            for (var i = 0; i < units.Count; i++)
            {
                if (!_units.ContainsKey(units[i]))
                {
                    continue;
                }

                var color = LookOf(units[i].Definition.Id).color * rangeBrightness;
                color.a = 1f;
                foreach (var cell in units[i].RangeCells)
                {
                    if (fieldView.Map.Contains(cell))
                    {
                        _rangeCells[cell] = color;
                    }
                }
            }

            var count = 0;
            foreach (var pair in _rangeCells)
            {
                if (count == _rangeMarkers.Count)
                {
                    var created = CreatePrimitive(PrimitiveType.Cube, "Range", _rangeRoot, Color.white);
                    // 経路の線（上面 +0.03）より低く、マスの色が周りに見える大きさにする。
                    created.localScale = new Vector3(0.5f, 0.01f, 0.5f);
                    _rangeMarkers.Add(created);
                }

                var marker = _rangeMarkers[count++];
                marker.position = fieldView.CellTopToWorld(pair.Key) + Vector3.up * 0.01f;
                SetColor(marker.GetComponent<Renderer>(), pair.Value);
                marker.gameObject.SetActive(true);
            }

            for (var i = count; i < _rangeMarkers.Count; i++)
            {
                _rangeMarkers[i].gameObject.SetActive(false);
            }
        }

        private UnitLook LookOf(string id)
        {
            foreach (var look in unitLooks)
            {
                if (look.id == id)
                {
                    return look;
                }
            }

            return new UnitLook { id = id, shape = PrimitiveType.Cube, size = Vector3.one * 0.5f, color = unknownUnitColor };
        }

        /// <summary>プリミティブの見た目の高さ（円柱とカプセルは大きさ 1 で高さ 2）。</summary>
        private static float BodyHeight(PrimitiveType shape, float scaleY) =>
            shape == PrimitiveType.Cylinder || shape == PrimitiveType.Capsule ? scaleY * 2f : scaleY;

        /// <summary>向きのワールド方向（フィールドの +z が画面の奥＝マップの上）。</summary>
        private Vector3 Direction(Facing facing)
        {
            switch (facing)
            {
                case Facing.Down:
                    return -fieldView.transform.forward;
                case Facing.Left:
                    return -fieldView.transform.right;
                case Facing.Up:
                    return fieldView.transform.forward;
                default:
                    return fieldView.transform.right;
            }
        }

        private Transform CreateGroup(string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(transform, false);
            return group;
        }

        private Transform CreatePrimitive(PrimitiveType shape, string name, Transform parent, Color color)
        {
            var primitive = GameObject.CreatePrimitive(shape);
            primitive.name = name;
            // 配置のレイキャストがマスの箱だけに当たるよう、当たり判定を持たせない。
            Destroy(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            SetColor(primitive.GetComponent<Renderer>(), color);
            return primitive.transform;
        }

        private void SetColor(Renderer target, Color color)
        {
            _block.SetColor(BaseColor, color);
            target.SetPropertyBlock(_block);
        }
    }
}
