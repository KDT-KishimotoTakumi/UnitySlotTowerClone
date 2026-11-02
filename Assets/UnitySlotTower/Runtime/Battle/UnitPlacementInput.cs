using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 召喚・配置・向き選びの入力（FR-002、plan 3章）。マウスとキーボードの操作を <see cref="UnitDeployment"/> の操作に変える。
    /// マウス: 操作バーのアイコンを押してフィールドのマスで離す（クリックで選んでからマスをクリックしてもよい）。
    /// マスを決めたあとは、ポインターの方向（上下左右）を向き、フィールドをクリックすると決まる。マスの中心付近のクリックはマス選びに戻る。
    /// キーボード: 1〜5 で選び、矢印でカーソル移動と向き、Enter で決定、Esc で1段階戻る。
    /// 一時停止（T307）: P で切り替え。Esc は配置操作中なら1段階戻り、それ以外（一時停止中を含む）は一時停止を切り替える。
    /// 一時停止中は選択・配置・撤退・強化・▶ の操作を受け付けない（FR-012）。
    /// 撤退（T308）: 配置中の味方をクリックか Tab で選び、X か操作バーの撤退アイコンで撤退させる。
    /// 強化（T308）: Shift + 1〜5 か操作バーの強化アイコン。▶: 配置操作中でないときの Enter か操作バーの ▶。
    /// </summary>
    public sealed class UnitPlacementInput : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";
        private const int SlotCount = 5;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [Tooltip("向きを決めるのに必要な、マスの中心からポインターまでの距離（マス）。")]
        [SerializeField, FormerlySerializedAs("facingDragCells")] private float facingMinCells = 0.5f;

        private readonly InputAction[] _selects = new InputAction[SlotCount];
        private readonly InputAction[] _upgrades = new InputAction[SlotCount];
        private InputAction _cycleUnit;
        private InputAction _retreat;
        private InputAction _cursor;
        private InputAction _confirm;
        private InputAction _cancel;
        private InputAction _point;
        private InputAction _press;
        private InputAction _pause;
        private Vector2 _lastCursorValue;
        private Vector2 _lastPointer;
        private bool _ignoreRelease;

        private void Awake()
        {
            var map = inputActions.FindActionMap(ActionMap, true);
            for (var i = 0; i < SlotCount; i++)
            {
                _selects[i] = map.FindAction($"SelectUnit{i + 1}", true);
                _upgrades[i] = map.FindAction($"Upgrade{i + 1}", true);
            }

            _cycleUnit = map.FindAction("CycleUnit", true);
            _retreat = map.FindAction("Retreat", true);

            _cursor = map.FindAction("Cursor", true);
            _confirm = map.FindAction("Confirm", true);
            _cancel = map.FindAction("Cancel", true);
            _point = map.FindAction("Point", true);
            _press = map.FindAction("Press", true);
            _pause = map.FindAction("Pause", true);
        }

        private void OnEnable()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                _selects[i].performed += OnSelectAction;
                _upgrades[i].performed += OnUpgradeAction;
            }

            _cycleUnit.performed += OnCycleUnitAction;
            _retreat.performed += OnRetreatAction;
            _cursor.performed += OnCursorAction;
            _cursor.canceled += OnCursorAction;
            _confirm.performed += OnConfirmAction;
            _cancel.performed += OnCancelAction;
            _pause.performed += OnPauseAction;
            _cursor.actionMap.Enable();
        }

        private void OnDisable()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                _selects[i].performed -= OnSelectAction;
                _upgrades[i].performed -= OnUpgradeAction;
            }

            _cycleUnit.performed -= OnCycleUnitAction;
            _retreat.performed -= OnRetreatAction;
            _cursor.performed -= OnCursorAction;
            _cursor.canceled -= OnCursorAction;
            _confirm.performed -= OnConfirmAction;
            _cancel.performed -= OnCancelAction;
            _pause.performed -= OnPauseAction;
        }

        /// <summary>
        /// 操作バーの slot 番目（0 始まり、units.csv の並び）の味方を選ぶ。選択中の味方をもう一度選ぶと選択をやめる。
        /// 召喚できない（コイン不足・配置上限・待ち時間）ときは何も選ばない。
        /// </summary>
        public void SelectSlot(int slot)
        {
            var deployment = battle.Deployment;
            if (deployment == null || battle.IsPaused || slot < 0 || slot >= battle.Units.All.Count)
            {
                return;
            }

            var unitId = battle.Units.All[slot].Id;
            if (deployment.Selected != null && deployment.Selected.Id == unitId)
            {
                deployment.Cancel();
                if (deployment.Phase != DeployPhase.Idle)
                {
                    deployment.Cancel();
                }

                return;
            }

            if (deployment.Select(unitId) == SummonResult.Summoned)
            {
                SelectedUnit = null;
            }
        }

        /// <summary>撤退の対象として選んでいる配置中の味方。いなければ null。</summary>
        public BattleUnit SelectedUnit { get; private set; }

        /// <summary>操作バーの slot 番目の味方の種類を1段階強化する（FR-026）。</summary>
        public UpgradeResult? UpgradeSlot(int slot)
        {
            var deployment = battle.Deployment;
            if (deployment == null || battle.IsPaused || slot < 0 || slot >= battle.Units.All.Count)
            {
                return null;
            }

            return deployment.Upgrade(battle.Units.All[slot].Id);
        }

        /// <summary>選んでいる味方を撤退させる（FR-025）。</summary>
        public void RetreatSelected()
        {
            var deployment = battle.Deployment;
            if (deployment == null || battle.IsPaused || !IsOnField(SelectedUnit))
            {
                return;
            }

            deployment.Retreat(SelectedUnit);
            SelectedUnit = null;
        }

        /// <summary>配置中の味方を並び順に1体ずつ選ぶ（Tab）。最後の次は先頭に戻る。</summary>
        public void CycleUnit()
        {
            var field = battle.Field;
            if (field == null || battle.IsPaused || battle.Deployment.Phase != DeployPhase.Idle)
            {
                return;
            }

            var units = field.Units;
            var start = IndexOf(units, SelectedUnit);
            for (var step = 1; step <= units.Count; step++)
            {
                var unit = units[(start + step + units.Count) % units.Count];
                if (unit.IsAlive)
                {
                    SelectedUnit = unit;
                    return;
                }
            }

            SelectedUnit = null;
        }

        private void OnUpgradeAction(InputAction.CallbackContext context) =>
            UpgradeSlot(System.Array.IndexOf(_upgrades, context.action));

        private void OnCycleUnitAction(InputAction.CallbackContext context) => CycleUnit();

        private void OnRetreatAction(InputAction.CallbackContext context) => RetreatSelected();

        private bool IsOnField(BattleUnit unit) => unit != null && unit.IsAlive && IndexOf(battle.Field.Units, unit) >= 0;

        private static int IndexOf(System.Collections.Generic.IReadOnlyList<BattleUnit> units, BattleUnit unit)
        {
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i] == unit)
                {
                    return i;
                }
            }

            return -1;
        }

        private void OnSelectAction(InputAction.CallbackContext context)
        {
            // Shift + 数字は強化（plan 3章）なので選択には使わない。
            if (context.control.device is Keyboard keyboard && keyboard.shiftKey.isPressed)
            {
                return;
            }

            SelectSlot(System.Array.IndexOf(_selects, context.action));
        }

        private void OnCursorAction(InputAction.CallbackContext context)
        {
            // 押し続けで繰り返さず、新しく押された方向だけ1マス（1回）動かす。画面の上は y が小さい方。
            var value = context.ReadValue<Vector2>();
            var dx = value.x != 0f && _lastCursorValue.x == 0f ? (int)Mathf.Sign(value.x) : 0;
            var dy = dx == 0 && value.y != 0f && _lastCursorValue.y == 0f ? -(int)Mathf.Sign(value.y) : 0;
            _lastCursorValue = value;
            if ((dx != 0 || dy != 0) && !battle.IsPaused)
            {
                battle.Deployment?.MoveCursor(dx, dy);
            }
        }

        private void OnConfirmAction(InputAction.CallbackContext context)
        {
            var deployment = battle.Deployment;
            if (deployment == null || battle.IsPaused)
            {
                return;
            }

            if (deployment.Phase == DeployPhase.ChoosingCell)
            {
                deployment.ChooseCell();
            }
            else if (deployment.Phase == DeployPhase.ChoosingFacing)
            {
                deployment.Confirm();
            }
            else
            {
                battle.StartWave();
            }
        }

        private void OnCancelAction(InputAction.CallbackContext context)
        {
            // Esc は Pause にも割り当ててあるが、配置操作中かどうかで振り分けるためここで扱う（plan 3章）。
            var deployment = battle.Deployment;
            if (deployment == null)
            {
                return;
            }

            if (!battle.IsPaused && deployment.Phase != DeployPhase.Idle)
            {
                deployment.Cancel();
            }
            else
            {
                battle.TogglePause();
            }
        }

        private void OnPauseAction(InputAction.CallbackContext context)
        {
            // Esc は OnCancelAction で振り分ける。
            if (context.control is KeyControl key && key.keyCode == Key.Escape)
            {
                return;
            }

            if (battle.Deployment != null)
            {
                battle.TogglePause();
            }
        }

        private void Update()
        {
            var deployment = battle.Deployment;
            if (deployment == null || battle.IsPaused)
            {
                return;
            }

            var pointer = _point.ReadValue<Vector2>();
            var pointerMoved = pointer != _lastPointer;
            _lastPointer = pointer;

            // 倒された・ウェーブが替わったなどでフィールドから居なくなった味方は選びをやめる。
            if (SelectedUnit != null && !IsOnField(SelectedUnit))
            {
                SelectedUnit = null;
            }

            // 配置操作中でないときにフィールドをクリックしたら、そのマスの味方を選ぶ（居なければ選びをやめる）。
            if (_press.WasPressedThisFrame() && deployment.Phase == DeployPhase.Idle
                && battleCamera.pixelRect.Contains(pointer))
            {
                SelectedUnit = TryGetCell(pointer, out var clickedCell) ? UnitAt(clickedCell) : null;
            }

            // キーボードで動かしたカーソル・向きを、止まっているマウスが上書きしないよう、動いたときだけ追う。
            if (pointerMoved && deployment.Phase == DeployPhase.ChoosingCell && TryGetCell(pointer, out var hovered))
            {
                deployment.SetCursor(hovered);
            }
            else if (pointerMoved && deployment.Phase == DeployPhase.ChoosingFacing && TryGetFacing(pointer, out var facing))
            {
                deployment.SetFacing(facing);
            }

            // 向き選びでフィールドをクリックしたら、向きを決めて置く。マスの中心付近ならマス選びに戻る。
            // 操作バーなどフィールドの外のクリックは扱わない（ほかの味方の選び直しに使う）。
            if (_press.WasPressedThisFrame() && deployment.Phase == DeployPhase.ChoosingFacing
                && battleCamera.pixelRect.Contains(pointer))
            {
                _ignoreRelease = true;
                if (TryGetFacing(pointer, out var clicked))
                {
                    deployment.SetFacing(clicked);
                    deployment.Confirm();
                }
                else
                {
                    deployment.Cancel();
                }
            }

            if (!_press.WasReleasedThisFrame())
            {
                return;
            }

            // 向き選びのクリックを離したときに、戻ったマス選びでそのままマスが決まらないようにする。
            if (_ignoreRelease)
            {
                _ignoreRelease = false;
            }
            else if (deployment.Phase == DeployPhase.ChoosingCell && TryGetCell(pointer, out var dropped))
            {
                deployment.SetCursor(dropped);
                deployment.ChooseCell();
            }
        }

        private BattleUnit UnitAt(GridPoint cell)
        {
            var units = battle.Field.Units;
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i].IsAlive && units[i].Cell.Equals(cell))
                {
                    return units[i];
                }
            }

            return null;
        }

        private bool TryGetCell(Vector2 screen, out GridPoint cell)
        {
            cell = default;
            return battleCamera.pixelRect.Contains(screen)
                && Physics.Raycast(battleCamera.ScreenPointToRay(screen), out var hit, 100f)
                && fieldView.TryGetCell(hit.collider.transform.position, out cell);
        }

        /// <summary>選んだマスからポインターへの向き（フィールドの水平面上）。近すぎれば false。</summary>
        private bool TryGetFacing(Vector2 screen, out Facing facing)
        {
            facing = default;
            var origin = fieldView.CellTopToWorld(battle.Deployment.Cursor);
            var plane = new Plane(fieldView.transform.up, origin);
            var ray = battleCamera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out var distance))
            {
                return false;
            }

            var local = fieldView.transform.InverseTransformVector(ray.GetPoint(distance) - origin);
            if (new Vector2(local.x, local.z).magnitude < facingMinCells)
            {
                return false;
            }

            // フィールドの +z は画面の奥＝マップの上。
            facing = Mathf.Abs(local.x) >= Mathf.Abs(local.z)
                ? local.x > 0f ? Facing.Right : Facing.Left
                : local.z > 0f ? Facing.Up : Facing.Down;
            return true;
        }
    }
}
