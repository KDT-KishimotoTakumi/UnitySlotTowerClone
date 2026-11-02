using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 配置操作の仮表示（T208）: カーソル（置ければ緑・置けなければ赤）、向きの矢印と攻撃範囲の見本。
    /// 撤退の対象に選んだ配置中の味方には枠を出す（T308）。配置済みの味方と敵は BattleActorsView（T209）が表示する。
    /// </summary>
    public sealed class UnitPlacementView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private UnitPlacementInput placementInput;
        [SerializeField] private Color placeableColor = new Color(0.3f, 0.95f, 0.4f);
        [SerializeField] private Color blockedColor = new Color(0.95f, 0.25f, 0.2f);
        [SerializeField] private Color rangeColor = new Color(1f, 0.6f, 0.15f);
        [SerializeField] private Color selectedUnitColor = new Color(1f, 0.95f, 0.4f);

        private readonly List<Transform> _rangeMarkers = new List<Transform>();
        private MaterialPropertyBlock _block;
        private Transform _cursor;
        private Renderer _cursorRenderer;
        private Color _cursorColor;
        private Transform _arrow;
        private Transform _selectedMarker;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _cursor = CreateCube("Placement Cursor", transform, placeableColor);
            _cursor.localScale = new Vector3(0.9f, 0.04f, 0.9f);
            _cursorRenderer = _cursor.GetComponent<Renderer>();
            _cursorColor = placeableColor;
            _arrow = CreateCube("Facing Arrow", transform, placeableColor);
            _arrow.localScale = new Vector3(0.15f, 0.06f, 0.7f);
            _selectedMarker = CreateCube("Selected Unit Marker", transform, selectedUnitColor);
            _selectedMarker.localScale = new Vector3(1f, 0.03f, 1f);
        }

        private void LateUpdate()
        {
            var deployment = battle.Deployment;
            if (deployment == null)
            {
                return;
            }

            ShowPlacement(deployment);
            var selected = placementInput.SelectedUnit;
            _selectedMarker.gameObject.SetActive(selected != null);
            if (selected != null)
            {
                _selectedMarker.position = fieldView.CellTopToWorld(selected.Cell) + Vector3.up * 0.015f;
            }
        }

        private void ShowPlacement(UnitDeployment deployment)
        {
            var active = deployment.Phase != DeployPhase.Idle;
            _cursor.gameObject.SetActive(active);
            _arrow.gameObject.SetActive(deployment.Phase == DeployPhase.ChoosingFacing);
            var rangeCount = 0;
            if (active)
            {
                var top = fieldView.CellTopToWorld(deployment.Cursor);
                _cursor.position = top + Vector3.up * 0.03f;
                var color = deployment.CanPlace(deployment.Cursor) ? placeableColor : blockedColor;
                if (color != _cursorColor)
                {
                    _cursorColor = color;
                    SetColor(_cursorRenderer, color);
                }

                var direction = Direction(deployment.Facing);
                _arrow.position = top + Vector3.up * 0.1f + direction * 0.45f;
                _arrow.rotation = Quaternion.LookRotation(direction);

                if (deployment.Phase == DeployPhase.ChoosingFacing)
                {
                    foreach (var cell in deployment.PreviewRange)
                    {
                        if (fieldView.Map.Contains(cell))
                        {
                            RangeMarker(rangeCount++).position = fieldView.CellTopToWorld(cell) + Vector3.up * 0.02f;
                        }
                    }
                }
            }

            for (var i = 0; i < _rangeMarkers.Count; i++)
            {
                _rangeMarkers[i].gameObject.SetActive(i < rangeCount);
            }
        }

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

        private Transform RangeMarker(int index)
        {
            if (index == _rangeMarkers.Count)
            {
                var marker = CreateCube("Range Preview", transform, rangeColor);
                marker.localScale = new Vector3(0.7f, 0.02f, 0.7f);
                _rangeMarkers.Add(marker);
            }

            return _rangeMarkers[index];
        }

        private Transform CreateCube(string name, Transform parent, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            // 配置のレイキャストがマスの箱だけに当たるよう、目印には当たり判定を持たせない。
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            SetColor(cube.GetComponent<Renderer>(), color);
            return cube.transform;
        }

        private void SetColor(Renderer target, Color color)
        {
            _block.SetColor(BaseColor, color);
            target.SetPropertyBlock(_block);
        }
    }
}
