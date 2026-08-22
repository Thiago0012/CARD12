using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Superficie vetorial da loja: obsidiana, metal quente e recortes
    /// chanfrados. Ela escala com o RectTransform, portanto preserva o
    /// acabamento no PC e no Android sem depender de sprites esticados.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneShopSurfaceGraphic : MaskableGraphic
    {
        private Color _top = new(0.105f, 0.075f, 0.028f, 0.98f);
        private Color _middle = new(0.030f, 0.026f, 0.022f, 0.98f);
        private Color _bottom = new(0.008f, 0.012f, 0.018f, 0.995f);
        private Color _accent = new(0.98f, 0.68f, 0.18f, 0.18f);
        private Color _border = new(0.98f, 0.68f, 0.18f, 0.76f);
        private float _chamfer = 10f;
        private float _borderWidth = 2f;

        public void SetStyle(
            Color accent,
            bool raised,
            float opacity = 1f,
            float chamfer = 10f)
        {
            float alpha = Mathf.Clamp01(opacity);
            _top = raised
                ? new Color(0.120f, 0.084f, 0.030f, 0.98f * alpha)
                : new Color(0.064f, 0.052f, 0.036f, 0.94f * alpha);
            _middle = raised
                ? new Color(0.036f, 0.030f, 0.024f, 0.99f * alpha)
                : new Color(0.020f, 0.023f, 0.027f, 0.97f * alpha);
            _bottom = new Color(0.006f, 0.010f, 0.016f, 0.995f * alpha);
            _accent = new Color(
                accent.r,
                accent.g,
                accent.b,
                (raised ? 0.24f : 0.12f) * alpha);
            _border = new Color(
                accent.r,
                accent.g,
                accent.b,
                (raised ? 0.82f : 0.54f) * alpha);
            _chamfer = Mathf.Max(0f, chamfer);
            _borderWidth = raised ? 2.4f : 1.65f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float cut = Mathf.Min(
                _chamfer,
                Mathf.Min(rect.width, rect.height) * 0.24f);
            Vector2[] outer = ChamferedPoints(rect, cut);

            int center = helper.currentVertCount;
            AddVertex(helper, rect.center, _middle);
            for (int index = 0; index < outer.Length; index++)
            {
                float y = Mathf.InverseLerp(
                    rect.yMin,
                    rect.yMax,
                    outer[index].y);
                Color gradient = y < 0.5f
                    ? Color.Lerp(_bottom, _middle, y * 2f)
                    : Color.Lerp(_middle, _top, (y - 0.5f) * 2f);
                if (index == 0 || index == 1 || index == 7)
                    gradient = Color.Lerp(gradient, _accent, 0.62f);
                AddVertex(helper, outer[index], gradient);
            }
            for (int index = 0; index < outer.Length; index++)
            {
                helper.AddTriangle(
                    center,
                    center + index + 1,
                    center + ((index + 1) % outer.Length) + 1);
            }

            AddBorder(helper, rect, cut);
            AddTopHighlight(helper, rect, cut);
        }

        private void AddBorder(VertexHelper helper, Rect rect, float cut)
        {
            float thickness = Mathf.Clamp(
                _borderWidth,
                1.25f,
                Mathf.Min(rect.width, rect.height) * 0.08f);
            Rect innerRect = new(
                rect.xMin + thickness,
                rect.yMin + thickness,
                Mathf.Max(0f, rect.width - thickness * 2f),
                Mathf.Max(0f, rect.height - thickness * 2f));
            Vector2[] outer = ChamferedPoints(rect, cut);
            Vector2[] inner = ChamferedPoints(
                innerRect,
                Mathf.Max(0f, cut - thickness * 0.55f));
            int start = helper.currentVertCount;
            Color inside = new(
                _border.r,
                _border.g,
                _border.b,
                _border.a * 0.20f);
            for (int index = 0; index < outer.Length; index++)
            {
                Color edge = index <= 2 || index == 7
                    ? Color.Lerp(_border, Color.white, 0.12f)
                    : _border;
                AddVertex(helper, outer[index], edge);
                AddVertex(helper, inner[index], inside);
            }
            for (int index = 0; index < outer.Length; index++)
            {
                int next = (index + 1) % outer.Length;
                int a = start + index * 2;
                int b = start + next * 2;
                helper.AddTriangle(a, b, b + 1);
                helper.AddTriangle(a, b + 1, a + 1);
            }
        }

        private void AddTopHighlight(
            VertexHelper helper,
            Rect rect,
            float cut)
        {
            float height = Mathf.Clamp(rect.height * 0.055f, 1.5f, 5f);
            float left = rect.xMin + cut + 3f;
            float right = rect.xMax - cut - 3f;
            float top = rect.yMax - 1.2f;
            if (right <= left)
                return;
            Color bright = Color.Lerp(_border, Color.white, 0.25f);
            Color fade = new(bright.r, bright.g, bright.b, 0f);
            int start = helper.currentVertCount;
            AddVertex(helper, new Vector2(left, top), bright);
            AddVertex(helper, new Vector2(right, top), bright);
            AddVertex(helper, new Vector2(right, top - height), fade);
            AddVertex(helper, new Vector2(left, top - height), fade);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private static Vector2[] ChamferedPoints(Rect rect, float cut)
        {
            return new[]
            {
                new Vector2(rect.xMin + cut, rect.yMax),
                new Vector2(rect.xMax - cut, rect.yMax),
                new Vector2(rect.xMax, rect.yMax - cut),
                new Vector2(rect.xMax, rect.yMin + cut),
                new Vector2(rect.xMax - cut, rect.yMin),
                new Vector2(rect.xMin + cut, rect.yMin),
                new Vector2(rect.xMin, rect.yMin + cut),
                new Vector2(rect.xMin, rect.yMax - cut)
            };
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
