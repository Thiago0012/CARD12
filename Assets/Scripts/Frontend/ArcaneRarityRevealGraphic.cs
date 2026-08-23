using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Aura vetorial leve usada no presságio do pacote e na revelação de
    /// cartas. O desenho é gerado em um único Graphic para evitar dezenas de
    /// Images transparentes e manter a abertura fluida em aparelhos móveis.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneRarityRevealGraphic : MaskableGraphic
    {
        private const int RingSegments = 40;
        private static readonly Color[] RainbowPalette =
        {
            new(0.12f, 0.82f, 1f, 1f),
            new(0.42f, 0.28f, 1f, 1f),
            new(1f, 0.16f, 0.82f, 1f),
            new(1f, 0.74f, 0.18f, 1f),
            new(0.16f, 1f, 0.78f, 1f)
        };

        private CardRarity _rarity = CardRarity.N;
        private float _progress;
        private float _pulse;
        private bool _animateIdle;

        public CardRarity Rarity => _rarity;

        public void Configure(CardRarity rarity, bool animateIdle)
        {
            _rarity = CardRarityCatalog.IsValid(rarity)
                ? rarity
                : CardRarity.N;
            _animateIdle = animateIdle;
            raycastTarget = false;
            color = Color.white;
            if (animateIdle)
                ApplyState(0.44f, 0.72f);
            else
                ApplyState(0f, 0f);
        }

        public void SetState(float progress, float pulse)
        {
            _animateIdle = false;
            ApplyState(progress, pulse);
        }

        private void Update()
        {
            if (!_animateIdle || !Application.isPlaying)
                return;

            float time = Time.unscaledTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(time *
                (_rarity == CardRarity.UR ? 2.35f : 1.75f));
            float glint = 0.5f + 0.5f * Mathf.Sin(time * 3.7f + 0.8f);
            ApplyState(
                Mathf.Lerp(0.38f, 0.64f, wave),
                Mathf.Lerp(0.58f, 1f, glint));
        }

        private void ApplyState(float progress, float pulse)
        {
            _progress = Mathf.Clamp01(Finite(progress));
            _pulse = Mathf.Clamp01(Finite(pulse));
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_progress <= 0.001f)
                return;

            Rect area = rectTransform.rect;
            if (area.width < 2f || area.height < 2f)
                return;

            Vector2 center = area.center;
            float unit = Mathf.Min(area.width, area.height);
            float strength = Mathf.SmoothStep(0f, 1f, _progress);
            Color primary = PrimaryColor(_rarity);
            Color secondary = SecondaryColor(_rarity);

            if (_rarity == CardRarity.UR)
                AddRainbowCurtain(vh, area, strength, _pulse);

            int rayCount = _rarity switch
            {
                CardRarity.UR => 22,
                CardRarity.SR => 16,
                CardRarity.R => 10,
                _ => 6
            };
            float rotation = _pulse * 11f;
            float innerRadius = unit * Mathf.Lerp(0.12f, 0.19f, strength);
            float outerRadius = unit * Mathf.Lerp(0.26f, 0.47f, strength);
            for (int index = 0; index < rayCount; index++)
            {
                float phase = index / (float)rayCount;
                float angle = phase * 360f + rotation;
                float alternating = 0.72f + 0.28f *
                    Mathf.Sin((index + 1) * 2.17f + _pulse * 3.1f);
                Color tint = Color.Lerp(primary, secondary,
                    Mathf.Repeat(index * 0.618034f, 1f));
                tint.a = (0.035f + 0.115f * strength) * alternating;
                AddRay(
                    vh,
                    center,
                    angle,
                    innerRadius,
                    outerRadius * alternating,
                    Mathf.Lerp(1.4f, 4.5f, strength),
                    tint);
            }

            float radius = unit * Mathf.Lerp(0.19f, 0.31f, strength);
            AddRing(vh, center, radius, radius * 0.72f,
                Mathf.Lerp(1.4f, 4.4f, strength),
                WithAlpha(primary, 0.18f + strength * 0.48f));
            if (_rarity >= CardRarity.SR)
            {
                AddRing(vh, center, radius * 1.22f, radius * 0.91f,
                    Mathf.Lerp(1f, 3.1f, strength),
                    WithAlpha(secondary, 0.10f + strength * 0.32f));
            }

            int shardCount = _rarity switch
            {
                CardRarity.UR => 14,
                CardRarity.SR => 9,
                CardRarity.R => 5,
                _ => 2
            };
            for (int index = 0; index < shardCount; index++)
            {
                float orbit = Mathf.Repeat(index * 0.618034f + _pulse * 0.035f, 1f);
                float angle = orbit * Mathf.PI * 2f;
                float distance = unit * Mathf.Lerp(0.24f, 0.46f,
                    Mathf.Repeat(index * 0.381966f, 1f));
                Vector2 position = center + new Vector2(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance * 0.72f);
                float size = unit * Mathf.Lerp(0.006f, 0.018f,
                    Mathf.Repeat(index * 0.754877f, 1f));
                Color tint = Color.Lerp(primary, secondary,
                    Mathf.Repeat(index * 0.414213f, 1f));
                tint.a = (0.15f + 0.58f * strength) *
                    (0.55f + 0.45f * _pulse);
                AddDiamond(vh, position, size, size * 2.4f, tint);
            }
        }

        private static void AddRainbowCurtain(
            VertexHelper vh,
            Rect area,
            float strength,
            float pulse)
        {
            const int bands = 11;
            float bandWidth = area.width / bands;
            for (int index = 0; index < bands; index++)
            {
                float centerDistance = Mathf.Abs(index - (bands - 1) * 0.5f) /
                    ((bands - 1) * 0.5f);
                float taper = 1f - centerDistance * 0.58f;
                float x = area.xMin + bandWidth * index;
                Color tint = RainbowPalette[index % RainbowPalette.Length];
                tint.a = (0.018f + 0.075f * strength) * taper *
                    (0.72f + pulse * 0.28f);
                AddQuad(vh,
                    new Vector2(x, area.yMin + area.height * 0.04f),
                    new Vector2(x + bandWidth * 1.18f, area.yMin),
                    new Vector2(x + bandWidth * 0.80f, area.yMax),
                    new Vector2(x + bandWidth * 0.20f,
                        area.yMax - area.height * 0.02f),
                    tint);
            }
        }

        private static void AddRay(
            VertexHelper vh,
            Vector2 center,
            float angleDegrees,
            float innerRadius,
            float outerRadius,
            float width,
            Color tint)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new(-direction.y, direction.x);
            Vector2 inner = center + direction * innerRadius;
            Vector2 outer = center + direction * outerRadius;
            AddQuad(vh,
                inner - side * width * 0.35f,
                inner + side * width * 0.35f,
                outer + side * width,
                outer - side * width,
                tint);
        }

        private static void AddRing(
            VertexHelper vh,
            Vector2 center,
            float radiusX,
            float radiusY,
            float thickness,
            Color tint)
        {
            int start = vh.currentVertCount;
            float innerX = Mathf.Max(0f, radiusX - thickness);
            float innerY = Mathf.Max(0f, radiusY - thickness);
            for (int segment = 0; segment <= RingSegments; segment++)
            {
                float angle = segment / (float)RingSegments * Mathf.PI * 2f;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(vh, center + new Vector2(
                    direction.x * radiusX,
                    direction.y * radiusY), tint);
                AddVertex(vh, center + new Vector2(
                    direction.x * innerX,
                    direction.y * innerY), tint);
            }
            for (int segment = 0; segment < RingSegments; segment++)
            {
                int index = start + segment * 2;
                vh.AddTriangle(index, index + 2, index + 1);
                vh.AddTriangle(index + 2, index + 3, index + 1);
            }
        }

        private static void AddDiamond(
            VertexHelper vh,
            Vector2 center,
            float halfWidth,
            float halfHeight,
            Color tint)
        {
            int start = vh.currentVertCount;
            AddVertex(vh, center + Vector2.up * halfHeight, tint);
            AddVertex(vh, center + Vector2.right * halfWidth, tint);
            AddVertex(vh, center + Vector2.down * halfHeight, tint);
            AddVertex(vh, center + Vector2.left * halfWidth, tint);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddQuad(
            VertexHelper vh,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color tint)
        {
            int start = vh.currentVertCount;
            AddVertex(vh, a, tint);
            AddVertex(vh, b, tint);
            AddVertex(vh, c, tint);
            AddVertex(vh, d, tint);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vh.AddVert(vertex);
        }

        private static Color PrimaryColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.R => new Color(0.12f, 0.64f, 1f, 1f),
                CardRarity.SR => new Color(1f, 0.66f, 0.08f, 1f),
                CardRarity.UR => new Color(0.98f, 0.14f, 0.78f, 1f),
                _ => new Color(0.72f, 0.78f, 0.86f, 1f)
            };
        }

        private static Color SecondaryColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.R => new Color(0.16f, 0.96f, 1f, 1f),
                CardRarity.SR => new Color(1f, 0.96f, 0.58f, 1f),
                CardRarity.UR => new Color(0.14f, 0.92f, 1f, 1f),
                _ => Color.white
            };
        }

        private static Color WithAlpha(Color tint, float alpha)
        {
            tint.a = Mathf.Clamp01(alpha);
            return tint;
        }

        private static float Finite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
