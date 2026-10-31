using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Tutorial;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tutorial
{
    /// <summary>
    /// 文字なし説明（FR-004、T403、plan 8章）。手順ごとに初めての場面で画面を薄暗くし、対象を光る枠で囲んで、手のアニメで操作を見せる。
    /// 表示中は時間を止め（<see cref="BattleController.SetHeld"/>）、クリックか Enter / Space を離すと閉じて再開する。
    /// 見た手順は PlayerPrefs に残し、二度と出さない（PC 全体で1回）。手順を出す場面は <see cref="TutorialProgress"/>。
    /// 配置の説明は置けるマスすべてに印を付け、向きの説明は手の動きに合わせて実際の向きを回して攻撃範囲の見本を回して見せる。
    /// 重ねる部品は実行時に作る。配置操作の入力より後に動かし、閉じるクリックが同じフレームで配置に使われないようにする。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class TutorialDirector : MonoBehaviour
    {
        public const string PrefsKey = "UnitySlotTower.TutorialSeen";
        private const string ActionMap = "SlotTower";
        private const float TapSeconds = 0.9f;
        private const float DragSeconds = 1.6f;
        private const int MaxMarkers = 64;

        // 向きの説明で順に見せる向きと、それぞれの矢印の番号（矢印は 右・上・左・下 の順に作る）。
        private static readonly Facing[] FacingCycle = { Facing.Right, Facing.Down, Facing.Left, Facing.Up };
        private static readonly int[] FacingArrow = { 0, 3, 2, 1 };

        [SerializeField] private BattleController battle;
        [SerializeField] private UnitPlacementInput placementInput;
        [SerializeField] private SlotCabinet slotCabinet;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("説明を重ねるキャンバス（Screen Space Overlay）。黒帯も含めて覆う。")]
        [SerializeField] private RectTransform overlayParent;

        [Header("Targets")]
        [SerializeField] private RectTransform[] unitIcons = new RectTransform[0];
        [SerializeField] private RectTransform[] upgradeIcons = new RectTransform[0];
        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform retreatButton;
        [SerializeField] private RectTransform leverButton;
        [SerializeField] private RectTransform[] stopButtons = new RectTransform[0];
        [SerializeField] private RectTransform bonusLamp;
        [SerializeField] private RectTransform bonusCounter;

        [Header("Look")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color frameColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color handColor = Color.white;
        [Tooltip("キャンバス単位（基準 1440×1920）。")]
        [SerializeField] private Vector2 handSize = new Vector2(150f, 190f);
        [SerializeField] private float holePadding = 18f;
        [SerializeField] private float frameThickness = 8f;
        [Tooltip("表示してからこの秒数（実時間）は閉じない。直前の操作の続きで閉じてしまわないように。")]
        [SerializeField] private float minShowSeconds = 0.6f;

        private readonly Vector3[] _corners = new Vector3[4];
        private readonly Vector2[] _taps = new Vector2[4];
        private readonly Image[] _dims = new Image[4];
        private readonly Image[] _frames = new Image[4];
        private readonly Graphic[] _arrows = new Graphic[4];
        private readonly Image[] _markers = new Image[MaxMarkers];
        private readonly System.Collections.Generic.List<GridPoint> _facingCells = new System.Collections.Generic.List<GridPoint>();
        private RectTransform _root;
        private HandGraphic _hand;
        private InputAction _press;
        private InputAction _confirm;
        private InputAction _lever;
        private TutorialProgress _progress;
        private float _shownAt;
        private bool _armed;

        // 表示中の手順の見せ方（毎フレーム、対象の位置から作り直す）。
        private Rect _hole;
        private bool _hasHole;
        private int _tapCount;
        private bool _drag;
        private Vector2 _dragFrom;
        private Vector2 _dragTo;
        private bool _showArrows;
        private int _activeArrow;
        private int _markerCount;

        /// <summary>テスト用。設定すると PlayerPrefs の代わりにこの記録を使い、保存もしない。シーンを読み込む前に設定する。</summary>
        public static TutorialProgress ProgressOverride { get; set; }

        /// <summary>表示中の手順。表示していなければ null。</summary>
        public TutorialStep? Current { get; private set; }

        public TutorialProgress Progress => _progress;

        private void Awake()
        {
            if (battle == null || placementInput == null || slotCabinet == null || fieldView == null || battleCamera == null
                || inputActions == null || overlayParent == null)
            {
                Debug.LogError("文字なし説明の参照が足りない", this);
                enabled = false;
                return;
            }

            _progress = ProgressOverride ?? TutorialProgress.Parse(PlayerPrefs.GetString(PrefsKey, ""));
            var map = inputActions.FindActionMap(ActionMap, true);
            _press = map.FindAction("Press", true);
            _confirm = map.FindAction("Confirm", true);
            _lever = map.FindAction("Lever", true);
            BuildOverlay();
        }

        private void Update()
        {
            if (Current.HasValue)
            {
                if (Current == TutorialStep.Facing)
                {
                    // 手の動きに合わせて実際の向きを回し、攻撃範囲の見本が回るのを見せる（表示より前に変える）。
                    battle.Deployment.SetFacing(FacingCycle[FacingIndex(Time.unscaledTime - _shownAt)]);
                }

                UpdateDismiss();
                return;
            }

            var run = battle.Run;
            if (_progress.IsComplete || run == null || battle.IsUserPaused)
            {
                return;
            }

            var situation = new TutorialSituation(run.Wave, run.Phase, run.Deployment.Phase, CountAlive(run.Field.Units),
                placementInput.SelectedUnit != null, slotCabinet.State == SlotState.Bonus);
            var next = _progress.Next(situation);
            if (next.HasValue)
            {
                Show(next.Value);
            }
        }

        private void LateUpdate()
        {
            if (Current.HasValue)
            {
                var time = Time.unscaledTime - _shownAt;
                Plan(Current.Value, time);
                Draw(time);
            }
        }

        private void OnDestroy()
        {
            // 時間の戻しは BattleController がシーンを離れるときに行う。
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }
        }

        private void Show(TutorialStep step)
        {
            Current = step;
            _shownAt = Time.unscaledTime;
            _armed = false;
            battle.SetHeld(true);
            if (step == TutorialStep.Facing)
            {
                CollectFacingCells();
            }

            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
        }

        private void Close()
        {
            _progress.MarkSeen(Current.Value);
            if (ProgressOverride == null)
            {
                PlayerPrefs.SetString(PrefsKey, _progress.Serialize());
                PlayerPrefs.Save();
            }

            Current = null;
            _root.gameObject.SetActive(false);
            battle.SetHeld(false);
        }

        // 表示してから押して離したら閉じる。押すのは時間が止まっている間なので、ほかの操作には使われない。
        private void UpdateDismiss()
        {
            if (Time.unscaledTime - _shownAt < minShowSeconds)
            {
                return;
            }

            _armed |= _press.WasPressedThisFrame() || _confirm.WasPressedThisFrame() || _lever.WasPressedThisFrame();
            if (_armed && (_press.WasReleasedThisFrame() || _confirm.WasReleasedThisFrame() || _lever.WasReleasedThisFrame()))
            {
                Close();
            }
        }

        private void Plan(TutorialStep step, float time)
        {
            _hasHole = false;
            _tapCount = 0;
            _drag = false;
            _showArrows = false;
            _markerCount = 0;
            var deployment = battle.Deployment;
            switch (step)
            {
                case TutorialStep.Summon:
                    AddTargets(unitIcons);
                    AddTap(First(unitIcons));
                    break;
                case TutorialStep.Place:
                    PlanPlace(deployment);
                    break;
                case TutorialStep.Facing:
                    PlanFacing(deployment.Cursor, FacingIndex(time));
                    break;
                case TutorialStep.Start:
                    AddTarget(startButton);
                    AddTap(startButton);
                    break;
                case TutorialStep.Slot:
                    AddTarget(leverButton);
                    AddTargets(stopButtons);
                    AddTap(leverButton);
                    foreach (var stop in stopButtons)
                    {
                        AddTap(stop);
                    }

                    break;
                case TutorialStep.Bonus:
                    AddTarget(bonusLamp);
                    AddTarget(bonusCounter);
                    AddTarget(leverButton);
                    AddTap(leverButton);
                    break;
                case TutorialStep.Upgrade:
                    AddTargets(upgradeIcons);
                    AddTap(First(upgradeIcons));
                    break;
                case TutorialStep.Retreat:
                    AddTarget(retreatButton);
                    AddTap(retreatButton);
                    break;
            }
        }

        // 置けるマスすべてに光る印を付け、手は選んだアイコンから、カーソルに最も近い置けるマスへドラッグする。
        private void PlanPlace(UnitDeployment deployment)
        {
            var icon = SelectedIcon(deployment);
            AddScreenRect(battleCamera.pixelRect);
            AddTarget(icon);

            var map = battle.Run.Map;
            var cursor = deployment.Cursor;
            var target = cursor;
            var best = int.MaxValue;
            var markerSize = CellSpacing(cursor) * 0.45f;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (!deployment.CanPlace(cell))
                    {
                        continue;
                    }

                    var distance = Mathf.Abs(x - cursor.X) + Mathf.Abs(y - cursor.Y);
                    if (distance < best)
                    {
                        best = distance;
                        target = cell;
                    }

                    if (_markerCount < _markers.Length)
                    {
                        var marker = _markers[_markerCount++].rectTransform;
                        marker.anchoredPosition = ToLocal(CellScreen(cell, true));
                        marker.sizeDelta = new Vector2(markerSize, markerSize);
                    }
                }
            }

            if (TryCenter(icon, out var from))
            {
                _drag = true;
                _dragFrom = from;
                _dragTo = ToLocal(CellScreen(target, true));
            }
        }

        // 選んだマスの周りの4方向に矢印を出し、手はいまの向きの矢印へ動かす（向きはポインターの方向）。
        // 光らせる範囲は、4方向すべての攻撃範囲が入る広さにする。
        private void PlanFacing(GridPoint cell, int cycleIndex)
        {
            var center = ToLocal(CellScreen(cell, true));
            _showArrows = true;
            _activeArrow = FacingArrow[cycleIndex];
            AddLocalPoint(center);
            foreach (var rangeCell in _facingCells)
            {
                AddLocalPoint(ToLocal(CellScreen(rangeCell, false)));
            }

            for (var i = 0; i < 4; i++)
            {
                var direction = i == 0 ? new GridPoint(cell.X + 1, cell.Y)
                    : i == 1 ? new GridPoint(cell.X, cell.Y - 1)
                    : i == 2 ? new GridPoint(cell.X - 1, cell.Y)
                    : new GridPoint(cell.X, cell.Y + 1);
                var point = ToLocal(CellScreen(direction, false));
                var arrow = _arrows[i].rectTransform;
                var offset = point - center;
                var size = Mathf.Clamp(offset.magnitude * 0.85f, 70f, 220f);
                arrow.anchoredPosition = point;
                arrow.sizeDelta = new Vector2(size, size);
                arrow.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
                AddLocalPoint(point + offset.normalized * size * 0.6f);
            }

            _drag = true;
            _dragFrom = center;
            _dragTo = _arrows[_activeArrow].rectTransform.anchoredPosition;
        }

        // 4方向それぞれの攻撃範囲のマスを集めておく。向きは最後に右へ戻す（説明中は手の動きに合わせて回す）。
        private void CollectFacingCells()
        {
            _facingCells.Clear();
            var deployment = battle.Deployment;
            foreach (var facing in FacingCycle)
            {
                deployment.SetFacing(facing);
                foreach (var cell in deployment.PreviewRange)
                {
                    _facingCells.Add(cell);
                }
            }

            deployment.SetFacing(FacingCycle[0]);
        }

        private static int FacingIndex(float time) => (int)(time / DragSeconds) % FacingCycle.Length;

        // 画面上の1マスの大きさ（キャンバス単位）。
        private float CellSpacing(GridPoint cell) =>
            Vector2.Distance(ToLocal(CellScreen(cell, false)), ToLocal(CellScreen(new GridPoint(cell.X + 1, cell.Y), false)));

        private void Draw(float time)
        {
            var size = _root.rect.size;
            var hole = _hasHole ? Clamp(Expand(_hole, holePadding), size) : new Rect(size * 0.5f, Vector2.zero);
            SetRect(_dims[0], new Rect(0f, 0f, size.x, hole.yMin));
            SetRect(_dims[1], new Rect(0f, hole.yMax, size.x, size.y - hole.yMax));
            SetRect(_dims[2], new Rect(0f, hole.yMin, hole.xMin, hole.height));
            SetRect(_dims[3], new Rect(hole.xMax, hole.yMin, size.x - hole.xMax, hole.height));

            var pulse = frameColor;
            pulse.a = 0.6f + 0.4f * Mathf.Sin(time * 6f);
            var t = frameThickness;
            SetRect(_frames[0], new Rect(hole.xMin - t, hole.yMin - t, hole.width + t * 2f, t));
            SetRect(_frames[1], new Rect(hole.xMin - t, hole.yMax, hole.width + t * 2f, t));
            SetRect(_frames[2], new Rect(hole.xMin - t, hole.yMin, t, hole.height));
            SetRect(_frames[3], new Rect(hole.xMax, hole.yMin, t, hole.height));
            foreach (var frame in _frames)
            {
                frame.enabled = _hasHole;
                frame.color = pulse;
            }

            for (var i = 0; i < _arrows.Length; i++)
            {
                _arrows[i].enabled = _showArrows;
                var color = frameColor;
                color.a = i == _activeArrow ? 1f : 0.4f;
                _arrows[i].color = color;
            }

            for (var i = 0; i < _markers.Length; i++)
            {
                _markers[i].enabled = i < _markerCount;
                _markers[i].color = pulse;
            }

            DrawHand(time);
        }

        private void DrawHand(float time)
        {
            var rect = _hand.rectTransform;
            _hand.enabled = _drag || _tapCount > 0;
            if (_drag)
            {
                // 押す → 動かす → 離して少し止まる、をくり返す。
                var p = time % DragSeconds / DragSeconds;
                var move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.75f, p));
                rect.anchoredPosition = Vector2.Lerp(_dragFrom, _dragTo, move);
                rect.localScale = Vector3.one * (p < 0.75f ? 0.88f : 1f);
            }
            else if (_tapCount > 0)
            {
                // 対象の上から降りてきて押し、また上がる。対象が複数なら順に押す。
                var index = (int)(time / TapSeconds) % _tapCount;
                var p = time % TapSeconds / TapSeconds;
                var lift = 40f * (1f - Mathf.Sin(p * Mathf.PI));
                rect.anchoredPosition = _taps[index] + new Vector2(0f, -lift);
                rect.localScale = Vector3.one * (p > 0.4f && p < 0.65f ? 0.88f : 1f);
            }
        }

        private void AddTargets(RectTransform[] targets)
        {
            foreach (var target in targets)
            {
                AddTarget(target);
            }
        }

        private void AddTarget(RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            target.GetWorldCorners(_corners);
            for (var i = 0; i < _corners.Length; i++)
            {
                // Screen Space Overlay のキャンバスでは、ワールド座標がそのまま画面のピクセル。
                AddLocalPoint(ToLocal(RectTransformUtility.WorldToScreenPoint(null, _corners[i])));
            }
        }

        private void AddScreenRect(Rect screen)
        {
            AddLocalPoint(ToLocal(screen.min));
            AddLocalPoint(ToLocal(screen.max));
        }

        private void AddLocalPoint(Vector2 point)
        {
            if (!_hasHole)
            {
                _hole = new Rect(point, Vector2.zero);
                _hasHole = true;
                return;
            }

            _hole.xMin = Mathf.Min(_hole.xMin, point.x);
            _hole.yMin = Mathf.Min(_hole.yMin, point.y);
            _hole.xMax = Mathf.Max(_hole.xMax, point.x);
            _hole.yMax = Mathf.Max(_hole.yMax, point.y);
        }

        private void AddTap(RectTransform target)
        {
            if (_tapCount < _taps.Length && TryCenter(target, out var center))
            {
                _taps[_tapCount++] = center;
            }
        }

        private bool TryCenter(RectTransform target, out Vector2 center)
        {
            center = default;
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            target.GetWorldCorners(_corners);
            center = ToLocal(RectTransformUtility.WorldToScreenPoint(null, (_corners[0] + _corners[2]) * 0.5f));
            return true;
        }

        private RectTransform SelectedIcon(UnitDeployment deployment)
        {
            var units = battle.Units.All;
            for (var i = 0; i < units.Count && i < unitIcons.Length; i++)
            {
                if (units[i] == deployment.Selected)
                {
                    return unitIcons[i];
                }
            }

            return First(unitIcons);
        }

        private static RectTransform First(RectTransform[] targets) => targets.Length > 0 ? targets[0] : null;

        // マップ外のマス（矢印の先）は高さを引けないので地面の高さで出す。
        private Vector2 CellScreen(GridPoint cell, bool top) =>
            battleCamera.WorldToScreenPoint(top ? fieldView.CellTopToWorld(cell) : fieldView.CellToWorld(cell));

        private Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            return local;
        }

        private static int CountAlive(System.Collections.Generic.IReadOnlyList<BattleUnit> units)
        {
            var count = 0;
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i].IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private static Rect Expand(Rect rect, float amount) =>
            new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);

        private static Rect Clamp(Rect rect, Vector2 size)
        {
            var xMin = Mathf.Clamp(rect.xMin, 0f, size.x);
            var yMin = Mathf.Clamp(rect.yMin, 0f, size.y);
            return Rect.MinMaxRect(xMin, yMin, Mathf.Clamp(rect.xMax, xMin, size.x), Mathf.Clamp(rect.yMax, yMin, size.y));
        }

        private static void SetRect(Graphic graphic, Rect rect)
        {
            var transform = graphic.rectTransform;
            transform.anchoredPosition = rect.position;
            transform.sizeDelta = rect.size;
        }

        // 黒帯も覆うようキャンバスいっぱいに広げ、左下を原点にする。全面の透明な板でクリックを下に通さない。
        private void BuildOverlay()
        {
            _root = new GameObject("Tutorial Overlay", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.gameObject.layer = overlayParent.gameObject.layer;
            _root.SetParent(overlayParent, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.pivot = Vector2.zero;
            _root.offsetMin = _root.offsetMax = Vector2.zero;

            var blocker = CreateGraphic<Image>("Blocker", Vector2.zero, new Color(0f, 0f, 0f, 0f));
            blocker.raycastTarget = true;
            blocker.rectTransform.anchorMax = Vector2.one;
            blocker.rectTransform.offsetMin = blocker.rectTransform.offsetMax = Vector2.zero;

            for (var i = 0; i < _dims.Length; i++)
            {
                _dims[i] = CreateGraphic<Image>($"Dim {i + 1}", Vector2.zero, dimColor);
            }

            for (var i = 0; i < _frames.Length; i++)
            {
                _frames[i] = CreateGraphic<Image>($"Frame {i + 1}", Vector2.zero, frameColor);
            }

            for (var i = 0; i < _markers.Length; i++)
            {
                _markers[i] = CreateGraphic<Image>($"Placeable {i + 1}", new Vector2(0.5f, 0.5f), frameColor);
                _markers[i].enabled = false;
            }

            for (var i = 0; i < _arrows.Length; i++)
            {
                _arrows[i] = CreateGraphic<TriangleGraphic>($"Arrow {i + 1}", new Vector2(0.5f, 0.5f), frameColor);
            }

            _hand = CreateGraphic<HandGraphic>("Hand", HandGraphic.FingertipPivot, handColor);
            _hand.rectTransform.sizeDelta = handSize;
            _root.gameObject.SetActive(false);
        }

        private T CreateGraphic<T>(string name, Vector2 pivot, Color color) where T : Graphic
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = _root.gameObject.layer;
            rect.SetParent(_root, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            var graphic = rect.gameObject.AddComponent<T>();
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }
    }
}
