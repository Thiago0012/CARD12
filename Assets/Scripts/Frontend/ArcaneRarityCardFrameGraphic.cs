using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Moldura luminosa desenhada sobre a carta, sem textura adicional.
    /// SR usa ouro quente; UR percorre um espectro magenta/ciano. O mesh
    /// permanece leve o bastante para as cinco cartas da abertura no mobile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneRarityCardFrameGraphic : MaskableGraphic
    {
        private const int SegmentsPerEdge = 9;

        private CardRarity _rarity = CardRarity.SR;
        private float _progress = 1f;
        private float _pulse = 0.72f;
        private float _phase;
        private bool _animateIdle;

        public void Configure(CardRarity rarity, bool animateIdle)
        {
            _rarity = rarity == CardRarity.UR
                ? CardRarity.UR
                : CardRarity.SR;
            _animateIdle = animateIdle;
            raycastTarget = false;
            color = Color.white;
            SetVerticesDirty();
        }

        public void SetState(float progress, float pulse)
        {
            _progress = Mathf.Clamp01(progress);
            _pulse = Mathf.Clamp01(pulse);
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!_animateIdle || !Application.isPlaying)
                return;

            _phase = Mathf.Repeat(Time.unscaledTime *
                (_rarity == CardRarity.UR ? 0.18f : 0.08f), 1f);
            float wave = 0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime * (_rarity == CardRarity.UR ? 4.2f : 2.8f));
            _progress = Mathf.Lerp(0.82f, 1f, wave);
            _pulse = wave;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect area = GetPixelAdjustedRect();
            if (area.width <= 0f || area.height <= 0f || _progress <= 0f)
                return;

            float unit = Mathf.Min(area.width, area.height);
            float line = Mathf.Clamp(unit * 0.025f, 2.2f, 9f);
            float glow = Mathf.Clamp(unit * 0.052f, 5f, 18f);
            float strength = Mathf.SmoothStep(0f, 1f, _progress);

            Color primary = _rarity == CardRarity.UR
                ? Color.HSVToRGB(Mathf.Repeat(0.88f + _phase, 1f), 0.88f, 1f)
                : new Color(1f, 0.60f, 0.04f, 1f);
            Color secondary = _rarity == CardRarity.UR
                ? Color.HSVToRGB(Mathf.Repeat(0.51f + _phase, 1f), 0.80f, 1f)
                : new Color(1f, 0.95f, 0.48f, 1f);

            // Halo externo: fica atrás da linha principal e torna a cor
            // perceptível mesmo sobre ilustrações muito claras.
            AddBorder(vh, area, glow,
                WithAlpha(primary, strength * (0.10f + _pulse * 0.10f)),
                WithAlpha(secondary, strength * (0.05f + _pulse * 0.08f)));
            AddBorder(vh, Inset(area, glow * 0.46f), glow * 0.48f,
                WithAlpha(primary, strength * (0.18f + _pulse * 0.12f)),
                WithAlpha(secondary, strength * (0.12f + _pulse * 0.10f)));

            Rect sharpArea = Inset(area, glow * 0.78f);
            if (_rarity == CardRarity.UR)
                AddPrismaticBorder(vh, sharpArea, line, strength);
            else
                AddBorder(vh, sharpArea, line,
                    WithAlpha(primary, 0.90f * strength),
                    WithAlpha(secondary, (0.82f + _pulse * 0.18f) * strength));

            // Filete branco interno e cantos reforçados dão leitura de metal,
            // semelhante a uma carta apoiada dentro de uma moldura física.
            Rect inner = Inset(sharpArea, line * 1.08f);
            AddBorder(vh, inner, Mathf.Max(1f, line * 0.24f),
                WithAlpha(Color.white, (0.38f + _pulse * 0.34f) * strength),
                WithAlpha(secondary, 0.46f * strength));
            AddCornerAccents(vh, sharpArea, line, primary, secondary, strength);
        }

        private void AddPrismaticBorder(
            VertexHelper vh,
            Rect area,
            float thickness,
            float strength)
        {
            for (int edge = 0; edge < 4; edge++)
            {
                for (int segment = 0; segment < SegmentsPerEdge; segment++)
                {
                    float start = segment / (float)SegmentsPerEdge;
                    float end = (segment + 1f) / SegmentsPerEdge;
                    float hueA = Mathf.Repeat(
                        _phase + (edge * SegmentsPerEdge + segment) /
                        (SegmentsPerEdge * 4f), 1f);
                    float hueB = Mathf.Repeat(hueA + 0.10f, 1f);
                    Color a = Color.HSVToRGB(hueA, 0.82f, 1f);
                    Color b = Color.HSVToRGB(hueB, 0.82f, 1f);
                    a.a = (0.86f + _pulse * 0.14f) * strength;
                    b.a = (0.86f + (1f - _pulse) * 0.14f) * strength;

                    Rect segmentRect = edge switch
                    {
                        0 => Rect.MinMaxRect(
                            Mathf.Lerp(area.xMin, area.xMax, start),
                            area.yMax - thickness,
                            Mathf.Lerp(area.xMin, area.xMax, end),
                            area.yMax),
                        1 => Rect.MinMaxRect(
                            area.xMax - thickness,
                            Mathf.Lerp(area.yMax, area.yMin, end),
                            area.xMax,
                            Mathf.Lerp(area.yMax, area.yMin, start)),
                        2 => Rect.MinMaxRect(
                            Mathf.Lerp(area.xMax, area.xMin, end),
                            area.yMin,
                            Mathf.Lerp(area.xMax, area.xMin, start),
                            area.yMin + thickness),
                        _ => Rect.MinMaxRect(
                            area.xMin,
                            Mathf.Lerp(area.yMin, area.yMax, start),
                            area.xMin + thickness,
                            Mathf.Lerp(area.yMin, area.yMax, end))
                    };
                    AddQuad(vh, segmentRect, a, b);
                }
            }
        }

        private static void AddCornerAccents(
            VertexHelper vh,
            Rect area,
            float line,
            Color primary,
            Color secondary,
            float strength)
        {
            float length = Mathf.Min(area.width, area.height) * 0.16f;
            float width = line * 1.65f;
            Color a = WithAlpha(primary, 0.90f * strength);
            Color b = WithAlpha(secondary, 0.96f * strength);
            AddQuad(vh, Rect.MinMaxRect(area.xMin, area.yMax - width,
                area.xMin + length, area.yMax), a, b);
            AddQuad(vh, Rect.MinMaxRect(area.xMax - length, area.yMax - width,
                area.xMax, area.yMax), b, a);
            AddQuad(vh, Rect.MinMaxRect(area.xMin, area.yMin,
                area.xMin + length, area.yMin + width), b, a);
            AddQuad(vh, Rect.MinMaxRect(area.xMax - length, area.yMin,
                area.xMax, area.yMin + width), a, b);
        }

        private static void AddBorder(
            VertexHelper vh,
            Rect area,
            float thickness,
            Color first,
            Color second)
        {
            if (thickness <= 0f || area.width <= 0f || area.height <= 0f)
                return;
            float t = Mathf.Min(thickness,
                Mathf.Min(area.width, area.height) * 0.48f);
            AddQuad(vh, Rect.MinMaxRect(
                area.xMin, area.yMax - t, area.xMax, area.yMax),
                first, second);
            AddQuad(vh, Rect.MinMaxRect(
                area.xMin, area.yMin, area.xMax, area.yMin + t),
                second, first);
            AddQuad(vh, Rect.MinMaxRect(
                area.xMin, area.yMin + t, area.xMin + t, area.yMax - t),
                first, second);
            AddQuad(vh, Rect.MinMaxRect(
                area.xMax - t, area.yMin + t, area.xMax, area.yMax - t),
                second, first);
        }

        private static void AddQuad(
            VertexHelper vh,
            Rect area,
            Color start,
            Color end)
        {
            int index = vh.currentVertCount;
            AddVertex(vh, new Vector2(area.xMin, area.yMin), start);
            AddVertex(vh, new Vector2(area.xMin, area.yMax), start);
            AddVertex(vh, new Vector2(area.xMax, area.yMax), end);
            AddVertex(vh, new Vector2(area.xMax, area.yMin), end);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private static void AddVertex(
            VertexHelper vh,
            Vector2 position,
            Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vh.AddVert(vertex);
        }

        private static Rect Inset(Rect area, float amount)
        {
            float safe = Mathf.Max(0f, Mathf.Min(amount,
                Mathf.Min(area.width, area.height) * 0.46f));
            return Rect.MinMaxRect(
                area.xMin + safe,
                area.yMin + safe,
                area.xMax - safe,
                area.yMax - safe);
        }

        private static Color WithAlpha(Color tint, float alpha)
        {
            tint.a = Mathf.Clamp01(alpha);
            return tint;
        }
    }
}
