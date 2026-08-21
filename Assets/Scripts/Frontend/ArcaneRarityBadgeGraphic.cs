using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [AddComponentMenu("UI/Arcane Rarity Badge Graphic")]
    public sealed class ArcaneRarityBadgeGraphic : MaskableGraphic
    {
        [SerializeField] private Color topColor = Color.white;
        [SerializeField] private Color bottomColor = Color.gray;
        [SerializeField] private Color edgeColor = Color.white;
        [SerializeField] private Color shineColor = new(1f, 1f, 1f, 0.3f);

        public void SetPalette(
            Color top,
            Color bottom,
            Color edge,
            Color shine)
        {
            topColor = top;
            bottomColor = bottom;
            edgeColor = edge;
            shineColor = shine;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float cut = Mathf.Min(rect.width, rect.height) * 0.16f;
            float border = Mathf.Clamp(
                Mathf.Min(rect.width, rect.height) * 0.075f,
                1f,
                2.5f);

            Vector2[] outer = BuildBeveledRect(rect, cut, 0f);
            Vector2[] inner = BuildBeveledRect(
                rect,
                Mathf.Max(0f, cut - border * 0.35f),
                border);

            AddGradientPolygon(vertexHelper, rect, inner);
            AddBorderRing(vertexHelper, outer, inner);
            AddTopHighlight(vertexHelper, rect, inner, border);
            AddDiagonalSheen(vertexHelper, rect, border);
        }

        private static Vector2[] BuildBeveledRect(
            Rect rect,
            float cut,
            float inset)
        {
            float left = rect.xMin + inset;
            float right = rect.xMax - inset;
            float bottom = rect.yMin + inset;
            float top = rect.yMax - inset;
            float safeCut = Mathf.Min(
                cut,
                Mathf.Min(right - left, top - bottom) * 0.45f);

            return new[]
            {
                new Vector2(left + safeCut, bottom),
                new Vector2(right - safeCut, bottom),
                new Vector2(right, bottom + safeCut),
                new Vector2(right, top - safeCut),
                new Vector2(right - safeCut, top),
                new Vector2(left + safeCut, top),
                new Vector2(left, top - safeCut),
                new Vector2(left, bottom + safeCut)
            };
        }

        private void AddGradientPolygon(
            VertexHelper vertexHelper,
            Rect rect,
            Vector2[] points)
        {
            int centerIndex = vertexHelper.currentVertCount;
            Vector2 center = rect.center;
            vertexHelper.AddVert(
                center,
                EvaluateGradient(rect, center.y),
                new Vector2(0.5f, 0.5f));

            for (int index = 0; index < points.Length; index++)
            {
                Vector2 point = points[index];
                vertexHelper.AddVert(
                    point,
                    EvaluateGradient(rect, point.y),
                    new Vector2(
                        Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                        Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)));
            }

            for (int index = 0; index < points.Length; index++)
            {
                int next = (index + 1) % points.Length;
                vertexHelper.AddTriangle(
                    centerIndex,
                    centerIndex + 1 + index,
                    centerIndex + 1 + next);
            }
        }

        private void AddBorderRing(
            VertexHelper vertexHelper,
            Vector2[] outer,
            Vector2[] inner)
        {
            for (int index = 0; index < outer.Length; index++)
            {
                int next = (index + 1) % outer.Length;
                int start = vertexHelper.currentVertCount;
                Color32 edge = MultiplyAlpha(edgeColor, color.a);
                vertexHelper.AddVert(outer[index], edge, Vector2.zero);
                vertexHelper.AddVert(outer[next], edge, Vector2.right);
                vertexHelper.AddVert(inner[next], edge, Vector2.one);
                vertexHelper.AddVert(inner[index], edge, Vector2.up);
                vertexHelper.AddTriangle(start, start + 1, start + 2);
                vertexHelper.AddTriangle(start, start + 2, start + 3);
            }
        }

        private void AddTopHighlight(
            VertexHelper vertexHelper,
            Rect rect,
            Vector2[] inner,
            float border)
        {
            float yTop = rect.yMax - border * 1.15f;
            float yBottom = Mathf.Max(
                rect.center.y,
                yTop - Mathf.Max(1f, rect.height * 0.08f));
            float left = inner[5].x;
            float right = inner[4].x;
            AddQuad(
                vertexHelper,
                new Vector2(left, yBottom),
                new Vector2(right, yBottom),
                new Vector2(right, yTop),
                new Vector2(left, yTop),
                MultiplyAlpha(shineColor, color.a * 0.68f));
        }

        private void AddDiagonalSheen(
            VertexHelper vertexHelper,
            Rect rect,
            float border)
        {
            float left = rect.xMin + border + rect.width * 0.08f;
            float right = rect.xMin + rect.width * 0.72f;
            float low = rect.yMin + border + rect.height * 0.18f;
            float high = rect.yMax - border - rect.height * 0.10f;
            float width = Mathf.Max(1f, rect.width * 0.10f);
            Color sheen = MultiplyAlpha(shineColor, color.a * 0.34f);
            AddQuad(
                vertexHelper,
                new Vector2(left, low),
                new Vector2(left + width, low),
                new Vector2(right + width, high),
                new Vector2(right, high),
                sheen);
        }

        private Color32 EvaluateGradient(Rect rect, float y)
        {
            float t = Mathf.InverseLerp(rect.yMin, rect.yMax, y);
            Color gradient = Color.Lerp(bottomColor, topColor, t);
            gradient.a *= color.a;
            return gradient;
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 bottomLeft,
            Vector2 bottomRight,
            Vector2 topRight,
            Vector2 topLeft,
            Color32 tint)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(bottomLeft, tint, Vector2.zero);
            vertexHelper.AddVert(bottomRight, tint, Vector2.right);
            vertexHelper.AddVert(topRight, tint, Vector2.one);
            vertexHelper.AddVert(topLeft, tint, Vector2.up);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static Color32 MultiplyAlpha(Color tint, float alpha)
        {
            tint.a *= alpha;
            return tint;
        }
    }
}
