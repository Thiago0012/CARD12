using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Fundo vetorial responsivo das ramificações da Central de Duelos.
    /// O mesmo desenho recebe a cor de cada modo sem esticar uma arte raster.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelModeBackdropGraphic : MaskableGraphic
    {
        private Color _accent = new(0.10f, 0.82f, 0.94f, 1f);

        public void SetAccent(Color accent)
        {
            _accent = accent;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            AddBackground(helper, rect);

            Vector2 focus = new(
                Mathf.Lerp(rect.xMin, rect.xMax, 0.70f),
                Mathf.Lerp(rect.yMin, rect.yMax, 0.48f));
            float radius = Mathf.Min(rect.width, rect.height) * 0.27f;
            AddRing(helper, focus, radius, 2.4f, 0.17f);
            AddRing(helper, focus, radius * 0.72f, 1.3f, 0.10f);
            AddRing(helper, focus, radius * 1.34f, 1.1f, 0.07f);

            AddLine(helper,
                new Vector2(rect.xMin + rect.width * 0.08f,
                    rect.yMin + rect.height * 0.20f),
                new Vector2(rect.xMax - rect.width * 0.06f,
                    rect.yMin + rect.height * 0.20f),
                1.2f,
                WithAlpha(_accent, 0.10f));
            AddLine(helper,
                new Vector2(rect.xMin + rect.width * 0.18f,
                    rect.yMax - rect.height * 0.14f),
                new Vector2(rect.xMax - rect.width * 0.04f,
                    rect.yMax - rect.height * 0.14f),
                1.2f,
                WithAlpha(_accent, 0.12f));

            AddCornerFrame(helper, rect);
            AddDiamond(helper, focus + Vector2.up * radius, 6f, 0.48f);
            AddDiamond(helper, focus + Vector2.down * radius, 6f, 0.48f);
            AddDiamond(helper, focus + Vector2.left * radius, 5f, 0.34f);
            AddDiamond(helper, focus + Vector2.right * radius, 5f, 0.34f);
        }

        private void AddBackground(VertexHelper helper, Rect rect)
        {
            int start = helper.currentVertCount;
            Color bottom = new(0.002f, 0.009f, 0.016f, 1f);
            Color top = new(
                Mathf.Lerp(0.008f, _accent.r, 0.035f),
                Mathf.Lerp(0.020f, _accent.g, 0.035f),
                Mathf.Lerp(0.032f, _accent.b, 0.045f),
                1f);
            AddVertex(helper, new Vector2(rect.xMin, rect.yMin), bottom);
            AddVertex(helper, new Vector2(rect.xMin, rect.yMax), top);
            AddVertex(helper, new Vector2(rect.xMax, rect.yMax), top);
            AddVertex(helper, new Vector2(rect.xMax, rect.yMin), bottom);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);

            // Faixa diagonal muito discreta: dá profundidade sem competir
            // com textos, cartas ou botões.
            Vector2 a = new(rect.xMin - rect.width * 0.08f,
                rect.yMin + rect.height * 0.34f);
            Vector2 b = new(rect.xMax + rect.width * 0.08f,
                rect.yMax - rect.height * 0.24f);
            AddLine(helper, a, b, rect.height * 0.19f,
                WithAlpha(_accent, 0.025f));
        }

        private void AddCornerFrame(VertexHelper helper, Rect rect)
        {
            float insetX = rect.width * 0.018f;
            float insetY = rect.height * 0.026f;
            float horizontal = Mathf.Min(rect.width * 0.11f, 150f);
            float vertical = Mathf.Min(rect.height * 0.12f, 90f);
            float width = 1.7f;
            Color edge = WithAlpha(_accent, 0.38f);
            Vector2 bl = new(rect.xMin + insetX, rect.yMin + insetY);
            Vector2 br = new(rect.xMax - insetX, rect.yMin + insetY);
            Vector2 tl = new(rect.xMin + insetX, rect.yMax - insetY);
            Vector2 tr = new(rect.xMax - insetX, rect.yMax - insetY);

            AddLine(helper, bl, bl + Vector2.right * horizontal, width, edge);
            AddLine(helper, bl, bl + Vector2.up * vertical, width, edge);
            AddLine(helper, br, br + Vector2.left * horizontal, width, edge);
            AddLine(helper, br, br + Vector2.up * vertical, width, edge);
            AddLine(helper, tl, tl + Vector2.right * horizontal, width, edge);
            AddLine(helper, tl, tl + Vector2.down * vertical, width, edge);
            AddLine(helper, tr, tr + Vector2.left * horizontal, width, edge);
            AddLine(helper, tr, tr + Vector2.down * vertical, width, edge);
        }

        private void AddRing(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float thickness,
            float alpha)
        {
            const int segments = 64;
            float inner = Mathf.Max(0f, radius - thickness * 0.5f);
            float outer = radius + thickness * 0.5f;
            int start = helper.currentVertCount;
            Color bright = WithAlpha(_accent, alpha);
            Color dim = WithAlpha(_accent, alpha * 0.38f);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Color color = Color.Lerp(
                    dim,
                    bright,
                    0.5f + 0.5f * Mathf.Sin(angle * 4f));
                AddVertex(helper, center + direction * outer, color);
                AddVertex(helper, center + direction * inner,
                    WithAlpha(color, color.a * 0.34f));
            }
            for (int index = 0; index < segments; index++)
            {
                int next = (index + 1) % segments;
                int outerCurrent = start + index * 2;
                int innerCurrent = outerCurrent + 1;
                int outerNext = start + next * 2;
                int innerNext = outerNext + 1;
                helper.AddTriangle(outerCurrent, outerNext, innerNext);
                helper.AddTriangle(outerCurrent, innerNext, innerCurrent);
            }
        }

        private void AddDiamond(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float alpha)
        {
            int start = helper.currentVertCount;
            Color color = WithAlpha(_accent, alpha);
            AddVertex(helper, center + Vector2.up * radius, color);
            AddVertex(helper, center + Vector2.right * radius, color);
            AddVertex(helper, center + Vector2.down * radius, color);
            AddVertex(helper, center + Vector2.left * radius, color);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddLine(
            VertexHelper helper,
            Vector2 from,
            Vector2 to,
            float width,
            Color color)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 0.001f)
                return;
            Vector2 normal = new(-direction.y, direction.x);
            normal.Normalize();
            normal *= width * 0.5f;
            int start = helper.currentVertCount;
            AddVertex(helper, from - normal, color);
            AddVertex(helper, from + normal, color);
            AddVertex(helper, to + normal, color);
            AddVertex(helper, to - normal, color);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
