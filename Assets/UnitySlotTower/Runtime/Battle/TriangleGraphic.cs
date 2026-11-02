using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 矩形いっぱいの右向き三角形（▶）を描く uGUI 部品。画像を用意せずに開始・強化の印を出すための仮（M5 で素材に差し替える）。
    /// 向きは RectTransform の回転で変える。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TriangleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.center.y), color, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
        }
    }
}
