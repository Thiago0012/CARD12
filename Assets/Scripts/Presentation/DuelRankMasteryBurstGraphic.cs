using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Procedural UI burst used behind the local rank mastery emblem. Keeping
    /// the rings and sparks in one mesh avoids runtime textures and particle
    /// systems while still producing a crisp result at every resolution.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelRankMasteryBurstGraphic : MaskableGraphic
    {
        private const int RingSegments = 56;
        private Color accent = new(0.35f, 0.78f, 1f, 1f);
        private float reveal;
        private float pulse;
        private float animationTime;

        public void SetAnimation(
            Color newAccent,
            float newReveal,
            float newPulse,
            float newAnimationTime)
        {
            accent = newAccent;
            reveal = Mathf.Clamp01(newReveal);
            pulse = Mathf.Clamp01(newPulse);
            animationTime = Mathf.Max(0f, newAnimationTime);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (reveal <= 0.001f)
                return;

            Rect bounds = rectTransform.rect;
            Vector2 center = bounds.center;
            float radius = Mathf.Min(bounds.width, bounds.height) * 0.5f;
            float bloom = Mathf.SmoothStep(0f, 1f, reveal);
            float wave = Mathf.SmoothStep(0f, 1f, pulse);

            AddRing(vertexHelper, center,
                radius * (0.31f + wave * 0.055f),
                Mathf.Max(1.6f, radius * 0.018f), 360f,
                animationTime * -115f,
                WithAlpha(accent, bloom * (1f - wave) * 0.72f));
            AddRing(vertexHelper, center,
                radius * (0.39f + wave * 0.10f),
                Mathf.Max(1.2f, radius * 0.012f), 290f,
                animationTime * 92f,
                WithAlpha(Color.white, bloom * (1f - wave) * 0.54f));
            AddDashedRing(vertexHelper, center,
                radius * (0.49f + wave * 0.06f),
                Mathf.Max(1.5f, radius * 0.014f),
                animationTime * -48f,
                WithAlpha(accent, bloom * (0.28f + (1f - wave) * 0.46f)));

            // Raios longos usam a diagonal do Canvas e atravessam a tela.
            // Assim a composição continua claramente fullscreen mesmo em
            // 16:9, sem formar uma caixa quadrada visual ao redor do elo.
            float screenRadius = Mathf.Sqrt(
                bounds.width * bounds.width + bounds.height * bounds.height) *
                0.54f;
            const int screenRayCount = 16;
            for (int index = 0; index < screenRayCount; index++)
            {
                float angle = (index / (float)screenRayCount * 360f +
                               animationTime * (index % 2 == 0 ? 5f : -4f)) *
                              Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new(-direction.y, direction.x);
                float shimmer = 0.5f + 0.5f * Mathf.Sin(
                    animationTime * 8f + index * 1.71f);
                AddRay(
                    vertexHelper,
                    center + direction * radius * 0.18f,
                    center + direction * screenRadius,
                    tangent,
                    Mathf.Max(0.75f, radius * (0.0025f + shimmer * 0.002f)),
                    WithAlpha(
                        index % 4 == 0 ? Color.white : accent,
                        bloom * (1f - wave * 0.55f) *
                        Mathf.Lerp(0.055f, 0.16f, shimmer)));
            }

            const int sparkCount = 18;
            for (int index = 0; index < sparkCount; index++)
            {
                float seed = index * 0.61803398875f;
                float angle = (seed * 360f + animationTime * 24f) *
                    Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new(-direction.y, direction.x);
                float stagger = Mathf.Repeat(index * 0.173f, 1f);
                float travel = Mathf.Clamp01(reveal * 1.32f - stagger * 0.32f);
                float sparkle = 0.62f +
                    Mathf.Sin(animationTime * 10f + index * 2.13f) * 0.38f;
                float sparkRadius = radius *
                    Mathf.Lerp(0.27f, 0.49f + stagger * 0.08f, travel);
                Vector2 position = center + direction * sparkRadius;
                float size = radius * Mathf.Lerp(0.018f, 0.038f, sparkle) *
                    bloom;
                Color sparkColor = index % 3 == 0
                    ? Color.white
                    : Color.Lerp(accent, Color.white, 0.28f + sparkle * 0.25f);
                sparkColor.a = bloom * Mathf.Lerp(0.36f, 0.92f, sparkle) *
                    (0.48f + (1f - wave) * 0.52f);
                AddDiamond(vertexHelper, position, direction, tangent,
                    size * 1.8f, size * 0.72f, sparkColor);

                if (index % 3 == 0)
                {
                    Vector2 rayStart = center + direction * radius * 0.30f;
                    Vector2 rayEnd = center + direction *
                        (sparkRadius - size * 1.4f);
                    AddRay(vertexHelper, rayStart, rayEnd, tangent,
                        Mathf.Max(0.8f, radius * 0.006f),
                        WithAlpha(accent, sparkColor.a * 0.42f));
                }
            }
        }

        private static void AddRing(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float thickness,
            float sweepDegrees,
            float rotationDegrees,
            Color ringColor)
        {
            float inner = Mathf.Max(0f, radius - thickness * 0.5f);
            float outer = radius + thickness * 0.5f;
            int segments = Mathf.Max(3,
                Mathf.CeilToInt(RingSegments * sweepDegrees / 360f));
            for (int index = 0; index < segments; index++)
            {
                float a0 = (rotationDegrees + sweepDegrees * index / segments) *
                    Mathf.Deg2Rad;
                float a1 = (rotationDegrees +
                    sweepDegrees * (index + 1) / segments) * Mathf.Deg2Rad;
                Vector2 d0 = new(Mathf.Cos(a0), Mathf.Sin(a0));
                Vector2 d1 = new(Mathf.Cos(a1), Mathf.Sin(a1));
                AddQuad(vertexHelper,
                    center + d0 * inner,
                    center + d0 * outer,
                    center + d1 * outer,
                    center + d1 * inner,
                    ringColor);
            }
        }

        private static void AddDashedRing(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float thickness,
            float rotationDegrees,
            Color ringColor)
        {
            const int dashCount = 14;
            const float dashSweep = 12f;
            for (int index = 0; index < dashCount; index++)
            {
                AddRing(vertexHelper, center, radius, thickness, dashSweep,
                    rotationDegrees + index * (360f / dashCount), ringColor);
            }
        }

        private static void AddDiamond(
            VertexHelper vertexHelper,
            Vector2 center,
            Vector2 up,
            Vector2 right,
            float vertical,
            float horizontal,
            Color diamondColor)
        {
            AddQuad(vertexHelper,
                center + up * vertical,
                center + right * horizontal,
                center - up * vertical,
                center - right * horizontal,
                diamondColor);
        }

        private static void AddRay(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            Vector2 tangent,
            float halfWidth,
            Color rayColor)
        {
            Vector2 width = tangent * halfWidth;
            AddQuad(vertexHelper,
                start - width,
                start + width,
                end + width,
                end - width,
                rayColor);
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color quadColor)
        {
            int start = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, a, quadColor);
            AddVertex(vertexHelper, b, quadColor);
            AddVertex(vertexHelper, c, quadColor);
            AddVertex(vertexHelper, d, quadColor);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddVertex(
            VertexHelper vertexHelper,
            Vector2 position,
            Color vertexColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vertex.uv0 = new Vector2(0.5f, 0.5f);
            vertexHelper.AddVert(vertex);
        }

        private static Color WithAlpha(Color source, float alpha)
        {
            source.a = Mathf.Clamp01(alpha);
            return source;
        }
    }
}
