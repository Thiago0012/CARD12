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

    /// <summary>
    /// Barra de elo vetorial feita para completar a moldura da arte da
    /// Central de Duelos. Fundo, filetes metálicos, energia, divisões e
    /// marcador final são uma única malha; o progresso deixa de parecer um
    /// retângulo ciano simplesmente colocado sobre a imagem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneRankProgressGraphic : MaskableGraphic
    {
        private float progress;
        private Color energy = new(0.10f, 0.82f, 0.95f, 1f);
        private Color metal = new(0.92f, 0.65f, 0.24f, 1f);

        public float Progress => progress;

        public void SetProgress(
            float normalizedProgress,
            Color energyColor,
            Color metalColor)
        {
            progress = Mathf.Clamp01(normalizedProgress);
            energy = energyColor;
            metal = metalColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float cut = Mathf.Min(13f, rect.height * 0.32f);
            AddChamferedGradient(
                helper,
                rect,
                cut,
                new Color(0.002f, 0.010f, 0.022f, 0.88f),
                new Color(0.014f, 0.052f, 0.075f, 0.82f),
                false);
            AddBorder(helper, rect, cut);

            float horizontalInset = Mathf.Clamp(
                rect.width * 0.012f,
                5f,
                9f);
            float verticalInset = Mathf.Clamp(
                rect.height * 0.18f,
                4f,
                8f);
            Rect channel = new(
                rect.xMin + horizontalInset,
                rect.yMin + verticalInset,
                Mathf.Max(0f, rect.width - horizontalInset * 2f),
                Mathf.Max(0f, rect.height - verticalInset * 2f));
            AddChamferedGradient(
                helper,
                channel,
                Mathf.Min(7f, channel.height * 0.30f),
                new Color(0.002f, 0.016f, 0.030f, 0.96f),
                new Color(0.010f, 0.055f, 0.076f, 0.94f),
                false);

            float fillWidth = channel.width * progress;
            if (fillWidth > 0.75f)
            {
                Rect fill = new(
                    channel.xMin,
                    channel.yMin,
                    fillWidth,
                    channel.height);
                AddChamferedGradient(
                    helper,
                    fill,
                    Mathf.Min(6f, Mathf.Min(
                        fill.width * 0.24f,
                        fill.height * 0.28f)),
                    Color.Lerp(
                        new Color(0.015f, 0.18f, 0.31f, 0.98f),
                        energy,
                        0.42f),
                    Color.Lerp(energy, Color.white, 0.20f),
                    true);
                AddProgressHighlight(helper, fill);
                AddEndMarker(helper, channel, fill.xMax);
            }

            AddSegments(helper, channel);
        }

        private void AddBorder(
            VertexHelper helper,
            Rect rect,
            float cut)
        {
            float thickness = Mathf.Clamp(rect.height * 0.055f, 1.4f, 3f);
            Rect inside = new(
                rect.xMin + thickness,
                rect.yMin + thickness,
                Mathf.Max(0f, rect.width - thickness * 2f),
                Mathf.Max(0f, rect.height - thickness * 2f));
            Vector2[] outer = ChamferedPoints(rect, cut);
            Vector2[] inner = ChamferedPoints(
                inside,
                Mathf.Max(0f, cut - thickness * 0.65f));
            int start = helper.currentVertCount;
            for (int index = 0; index < outer.Length; index++)
            {
                float x = Mathf.InverseLerp(
                    rect.xMin,
                    rect.xMax,
                    outer[index].x);
                Color edge = Color.Lerp(metal, energy, x * 0.72f);
                edge.a = index <= 2 || index == 7 ? 0.82f : 0.54f;
                AddVertex(helper, outer[index], edge);
                AddVertex(
                    helper,
                    inner[index],
                    new Color(edge.r, edge.g, edge.b, edge.a * 0.12f));
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

        private void AddProgressHighlight(VertexHelper helper, Rect fill)
        {
            float height = Mathf.Clamp(fill.height * 0.16f, 1f, 3f);
            Color bright = Color.Lerp(energy, Color.white, 0.48f);
            bright.a = 0.88f;
            Color fade = new(bright.r, bright.g, bright.b, 0f);
            AddQuad(
                helper,
                new Rect(
                    fill.xMin + 3f,
                    fill.yMax - height - 1f,
                    Mathf.Max(0f, fill.width - 6f),
                    height),
                fade,
                bright);
        }

        private void AddEndMarker(
            VertexHelper helper,
            Rect channel,
            float x)
        {
            float radius = Mathf.Clamp(channel.height * 0.24f, 2.5f, 6f);
            x = Mathf.Clamp(x, channel.xMin + radius, channel.xMax - radius);
            Color outer = Color.Lerp(energy, Color.white, 0.34f);
            outer.a = 0.96f;
            Color inner = Color.Lerp(metal, energy, 0.55f);
            inner.a = 0.92f;
            AddDiamond(helper, new Vector2(x, channel.center.y), radius, outer);
            AddDiamond(
                helper,
                new Vector2(x, channel.center.y),
                radius * 0.48f,
                inner);
        }

        private static void AddSegments(VertexHelper helper, Rect channel)
        {
            for (int index = 1; index < 10; index++)
            {
                float x = Mathf.Lerp(
                    channel.xMin,
                    channel.xMax,
                    index / 10f);
                AddQuad(
                    helper,
                    new Rect(x - 0.45f, channel.yMin + 2f,
                        0.9f, Mathf.Max(0f, channel.height - 4f)),
                    new Color(0.50f, 0.82f, 0.90f, 0.10f),
                    new Color(0.82f, 0.96f, 1f, 0.18f));
            }
        }

        private void AddChamferedGradient(
            VertexHelper helper,
            Rect rect,
            float cut,
            Color bottom,
            Color top,
            bool horizontalEnergy)
        {
            Vector2[] points = ChamferedPoints(rect, cut);
            int center = helper.currentVertCount;
            AddVertex(helper, rect.center, Color.Lerp(bottom, top, 0.48f));
            for (int index = 0; index < points.Length; index++)
            {
                float y = Mathf.InverseLerp(
                    rect.yMin,
                    rect.yMax,
                    points[index].y);
                Color value = Color.Lerp(bottom, top, y);
                if (horizontalEnergy)
                {
                    float x = Mathf.InverseLerp(
                        rect.xMin,
                        rect.xMax,
                        points[index].x);
                    value = Color.Lerp(value, energy, 0.18f + x * 0.36f);
                }
                AddVertex(helper, points[index], value);
            }
            for (int index = 0; index < points.Length; index++)
            {
                helper.AddTriangle(
                    center,
                    center + index + 1,
                    center + ((index + 1) % points.Length) + 1);
            }
        }

        private static void AddQuad(
            VertexHelper helper,
            Rect rect,
            Color bottom,
            Color top)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;
            int start = helper.currentVertCount;
            AddVertex(helper, new Vector2(rect.xMin, rect.yMin), bottom);
            AddVertex(helper, new Vector2(rect.xMin, rect.yMax), top);
            AddVertex(helper, new Vector2(rect.xMax, rect.yMax), top);
            AddVertex(helper, new Vector2(rect.xMax, rect.yMin), bottom);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddDiamond(
            VertexHelper helper,
            Vector2 center,
            float radius,
            Color value)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, center, value);
            AddVertex(helper, center + Vector2.up * radius, value);
            AddVertex(helper, center + Vector2.right * radius, value);
            AddVertex(helper, center + Vector2.down * radius, value);
            AddVertex(helper, center + Vector2.left * radius, value);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
            helper.AddTriangle(start, start + 3, start + 4);
            helper.AddTriangle(start, start + 4, start + 1);
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
            Color value)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = value;
            helper.AddVert(vertex);
        }
    }
}
