using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Tutorial
{
    /// <summary>
    /// 人差し指を上に伸ばした手（説明の指さし、T403）を矩形の組み合わせで描く uGUI 部品。画像を用意せずに出すための仮（M5 で素材に差し替える）。
    /// 指先は矩形の上辺、左から 30% の位置。RectTransform の pivot をそこに置くと、位置がそのまま指先になる。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HandGraphic : MaskableGraphic
    {
        public static readonly Vector2 FingertipPivot = new Vector2(0.3f, 1f);

        // 矩形に対する割合（xMin, yMin, xMax, yMax）。下が 0、上が 1。
        private static readonly Vector4[] Parts =
        {
            new Vector4(0.19f, 0.42f, 0.41f, 1f),     // 人差し指
            new Vector4(0.12f, 0f, 0.92f, 0.5f),      // 手のひら
            new Vector4(0.41f, 0.36f, 0.6f, 0.58f),   // 曲げた中指
            new Vector4(0.6f, 0.34f, 0.77f, 0.54f),   // 曲げた薬指
            new Vector4(0.77f, 0.3f, 0.92f, 0.48f),   // 曲げた小指
            new Vector4(0f, 0.12f, 0.16f, 0.36f),     // 親指
        };

        [SerializeField] private Color outlineColor = new Color(0.1f, 0.1f, 0.12f);
        [SerializeField] private float outline = 6f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            foreach (var part in Parts)
            {
                AddQuad(vh, rect, part, outline, outlineColor);
            }

            foreach (var part in Parts)
            {
                AddQuad(vh, rect, part, 0f, color);
            }
        }

        private static void AddQuad(VertexHelper vh, Rect rect, Vector4 part, float grow, Color32 quadColor)
        {
            var xMin = rect.xMin + rect.width * part.x - grow;
            var yMin = rect.yMin + rect.height * part.y - grow;
            var xMax = rect.xMin + rect.width * part.z + grow;
            var yMax = rect.yMin + rect.height * part.w + grow;
            var start = vh.currentVertCount;
            vh.AddVert(new Vector3(xMin, yMin), quadColor, Vector2.zero);
            vh.AddVert(new Vector3(xMin, yMax), quadColor, Vector2.zero);
            vh.AddVert(new Vector3(xMax, yMax), quadColor, Vector2.zero);
            vh.AddVert(new Vector3(xMax, yMin), quadColor, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
