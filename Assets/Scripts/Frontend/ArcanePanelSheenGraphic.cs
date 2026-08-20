using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Superfície chanfrada e graduada usada pelo Kit Visual Arcane. Evita
    /// que cartões dinâmicos pareçam retângulos planos sem depender de uma
    /// imagem raster exclusiva para cada tamanho.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcanePanelSheenGraphic : MaskableGraphic
    {
        private Color _top = new(0.025f, 0.075f, 0.105f, 0.92f);
        private Color _middle = new(0.008f, 0.033f, 0.055f, 0.94f);
        private Color _bottom = new(0.002f, 0.012f, 0.024f, 0.98f);
        private Color _accent = new(0.12f, 0.75f, 0.88f, 0.16f);
        private Color _border = new(0.12f, 0.75f, 0.88f, 0.62f);
        private float _chamfer = 9f;

        public void SetStyle(Color accent, bool raised, float opacity)
        {
            float alpha = Mathf.Clamp01(opacity);
            _top = raised
                ? new Color(0.030f, 0.082f, 0.108f, 0.93f * alpha)
                : new Color(0.018f, 0.052f, 0.078f, 0.88f * alpha);
            _middle = raised
                ? new Color(0.010f, 0.038f, 0.060f, 0.95f * alpha)
                : new Color(0.006f, 0.026f, 0.045f, 0.92f * alpha);
            _bottom = new Color(0.002f, 0.010f, 0.020f, 0.98f * alpha);
            _accent = new Color(
                accent.r,
                accent.g,
                accent.b,
                (raised ? 0.18f : 0.10f) * alpha);
            _border = new Color(
                accent.r,
                accent.g,
                accent.b,
                (raised ? 0.78f : 0.52f) * alpha);
            _chamfer = raised ? 11f : 8f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            float cut = Mathf.Min(
                _chamfer,
                Mathf.Min(rect.width, rect.height) * 0.18f);
            Vector2[] points =
            {
                new(rect.xMin + cut, rect.yMax),
                new(rect.xMax - cut, rect.yMax),
                new(rect.xMax, rect.yMax - cut),
                new(rect.xMax, rect.yMin + cut),
                new(rect.xMax - cut, rect.yMin),
                new(rect.xMin + cut, rect.yMin),
                new(rect.xMin, rect.yMin + cut),
                new(rect.xMin, rect.yMax - cut)
            };

            Vector2 center = rect.center;
            int centerIndex = helper.currentVertCount;
            AddVertex(helper, center, _middle);
            for (int index = 0; index < points.Length; index++)
            {
                float normalizedY = Mathf.InverseLerp(
                    rect.yMin,
                    rect.yMax,
                    points[index].y);
                Color gradient = normalizedY < 0.5f
                    ? Color.Lerp(_bottom, _middle, normalizedY * 2f)
                    : Color.Lerp(_middle, _top, (normalizedY - 0.5f) * 2f);
                if (index == 0 || index == 1 || index == 7)
                    gradient = Color.Lerp(gradient, _accent, 0.45f);
                AddVertex(helper, points[index], gradient);
            }
            for (int index = 0; index < points.Length; index++)
            {
                helper.AddTriangle(
                    centerIndex,
                    centerIndex + 1 + index,
                    centerIndex + 1 + (index + 1) % points.Length);
            }

            AddContinuousBorder(helper, rect, cut);
        }

        private void AddContinuousBorder(
            VertexHelper helper,
            Rect rect,
            float outerCut)
        {
            float thickness = Mathf.Clamp(
                Mathf.Min(rect.width, rect.height) * 0.018f,
                1.5f,
                3.5f);
            Rect inner = new(
                rect.xMin + thickness,
                rect.yMin + thickness,
                Mathf.Max(0f, rect.width - thickness * 2f),
                Mathf.Max(0f, rect.height - thickness * 2f));
            float innerCut = Mathf.Max(0f, outerCut - thickness * 0.42f);
            Vector2[] outer = ChamferedPoints(rect, outerCut);
            Vector2[] innerPoints = ChamferedPoints(inner, innerCut);
            int start = helper.currentVertCount;
            Color inside = new(
                _border.r,
                _border.g,
                _border.b,
                _border.a * 0.28f);
            for (int index = 0; index < outer.Length; index++)
            {
                Color outerColor = index <= 2 || index == 7
                    ? Color.Lerp(_border, Color.white, 0.14f)
                    : _border;
                AddVertex(helper, outer[index], outerColor);
                AddVertex(helper, innerPoints[index], inside);
            }
            for (int index = 0; index < outer.Length; index++)
            {
                int next = (index + 1) % outer.Length;
                int outerCurrent = start + index * 2;
                int innerCurrent = outerCurrent + 1;
                int outerNext = start + next * 2;
                int innerNext = outerNext + 1;
                helper.AddTriangle(outerCurrent, outerNext, innerNext);
                helper.AddTriangle(outerCurrent, innerNext, innerCurrent);
            }
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
