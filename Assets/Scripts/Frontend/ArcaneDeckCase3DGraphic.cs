using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Porta-deck procedural com profundidade, tampa chanfrada e núcleo
    /// luminoso. É independente da resolução e evita esticar uma textura
    /// bidimensional quando a miniatura muda de tamanho.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneDeckCase3DGraphic : MaskableGraphic
    {
        private Color _caseColor = new(0.07f, 0.36f, 0.28f, 1f);
        private Color _energyColor = new(0.24f, 0.95f, 0.64f, 1f);

        public void SetStyle(Color caseColor, Color energyColor)
        {
            _caseColor = caseColor;
            _energyColor = energyColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            float width = rect.width;
            float height = rect.height;
            if (width <= 1f || height <= 1f)
                return;

            float spine = Mathf.Clamp(width * 0.16f, 5f, width * 0.24f);
            float bevel = Mathf.Clamp(Mathf.Min(width, height) * 0.065f, 3f, 13f);
            float depth = Mathf.Clamp(width * 0.055f, 2f, 8f);
            float left = rect.xMin;
            float right = rect.xMax - depth;
            float bottom = rect.yMin + depth;
            float top = rect.yMax;

            Color frontBottom = Color.Lerp(_caseColor, Color.black, 0.72f);
            Color frontTop = Color.Lerp(_caseColor, Color.black, 0.40f);
            Color spineColor = Color.Lerp(_caseColor, Color.black, 0.80f);
            Color edge = Color.Lerp(_energyColor, Color.white, 0.22f);
            Color metal = new(0.45f, 0.54f, 0.52f, 0.92f);

            Vector2[] front =
            {
                new(left + spine, bottom),
                new(right - bevel, bottom),
                new(right, bottom + bevel),
                new(right, top - bevel),
                new(right - bevel, top),
                new(left + spine, top - bevel * 0.35f)
            };
            AddPolygon(helper, front, frontBottom);

            Vector2[] upperLight =
            {
                new(left + spine, top - height * 0.40f),
                new(right, top - height * 0.31f),
                new(right, top - bevel),
                new(right - bevel, top),
                new(left + spine, top - bevel * 0.35f)
            };
            AddPolygon(
                helper,
                upperLight,
                new Color(frontTop.r, frontTop.g, frontTop.b, 0.96f));

            Vector2[] side =
            {
                new(left, rect.yMin + bevel),
                new(left + spine, bottom),
                new(left + spine, top - bevel * 0.35f),
                new(left, top - bevel)
            };
            AddPolygon(helper, side, spineColor);

            Vector2[] topFace =
            {
                new(left, top - bevel),
                new(left + spine, top - bevel * 0.35f),
                new(right - bevel, top),
                new(right - bevel - depth, top - depth)
            };
            AddPolygon(
                helper,
                topFace,
                Color.Lerp(_caseColor, Color.white, 0.18f));

            Vector2[] rightDepth =
            {
                new(right, bottom + bevel),
                new(rect.xMax, rect.yMin + bevel),
                new(rect.xMax, top - bevel - depth),
                new(right, top - bevel)
            };
            AddPolygon(helper, rightDepth, Color.Lerp(_caseColor, Color.black, 0.86f));

            AddStrip(
                helper,
                new Vector2(left + spine + bevel * 0.6f, bottom + bevel * 0.65f),
                new Vector2(right - bevel * 0.70f, bottom + bevel * 0.65f),
                Mathf.Clamp(height * 0.012f, 1.2f, 3.2f),
                new Color(edge.r, edge.g, edge.b, 0.74f));
            AddStrip(
                helper,
                new Vector2(left + spine + bevel * 0.45f, top - bevel * 0.80f),
                new Vector2(right - bevel * 0.90f, top - bevel * 0.20f),
                Mathf.Clamp(height * 0.010f, 1.1f, 3f),
                new Color(edge.r, edge.g, edge.b, 0.86f));
            AddStrip(
                helper,
                new Vector2(left + spine * 0.52f, bottom + bevel * 0.55f),
                new Vector2(left + spine * 0.52f, top - bevel * 0.80f),
                Mathf.Clamp(width * 0.018f, 1f, 3f),
                new Color(metal.r, metal.g, metal.b, 0.84f));

            Vector2 center = new(
                left + spine + (right - left - spine) * 0.54f,
                bottom + (top - bottom) * 0.48f);
            float diamondWidth = Mathf.Min(width * 0.20f, height * 0.10f);
            float diamondHeight = diamondWidth * 1.42f;
            AddDiamond(helper, center, diamondWidth * 1.65f, diamondHeight * 1.65f,
                new Color(0.002f, 0.012f, 0.014f, 0.90f));
            AddDiamond(helper, center, diamondWidth * 1.18f, diamondHeight * 1.18f,
                new Color(edge.r, edge.g, edge.b, 0.88f));
            AddDiamond(helper, center, diamondWidth * 0.70f, diamondHeight * 0.70f,
                new Color(0.72f, 1f, 0.90f, 0.96f));

            Vector2[] gloss =
            {
                new(left + spine + bevel, top - height * 0.12f),
                new(right - bevel, top - height * 0.18f),
                new(right - bevel, top - height * 0.31f),
                new(left + spine + bevel, top - height * 0.37f)
            };
            AddPolygon(helper, gloss, new Color(1f, 1f, 1f, 0.055f));
        }

        private static void AddDiamond(
            VertexHelper helper,
            Vector2 center,
            float halfWidth,
            float halfHeight,
            Color color)
        {
            AddPolygon(
                helper,
                new[]
                {
                    new Vector2(center.x, center.y + halfHeight),
                    new Vector2(center.x + halfWidth, center.y),
                    new Vector2(center.x, center.y - halfHeight),
                    new Vector2(center.x - halfWidth, center.y)
                },
                color);
        }

        private static void AddStrip(
            VertexHelper helper,
            Vector2 start,
            Vector2 end,
            float thickness,
            Color color)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new(-direction.y, direction.x);
            Vector2 half = normal * (thickness * 0.5f);
            AddPolygon(
                helper,
                new[] { start - half, end - half, end + half, start + half },
                color);
        }

        private static void AddPolygon(
            VertexHelper helper,
            Vector2[] points,
            Color color)
        {
            if (points == null || points.Length < 3)
                return;
            Vector2 center = Vector2.zero;
            for (int index = 0; index < points.Length; index++)
                center += points[index];
            center /= points.Length;
            int start = helper.currentVertCount;
            AddVertex(helper, center, color);
            for (int index = 0; index < points.Length; index++)
                AddVertex(helper, points[index], color);
            for (int index = 0; index < points.Length; index++)
            {
                helper.AddTriangle(
                    start,
                    start + 1 + index,
                    start + 1 + (index + 1) % points.Length);
            }
        }

        private static void AddVertex(
            VertexHelper helper,
            Vector2 position,
            Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            helper.AddVert(vertex);
        }
    }
}
