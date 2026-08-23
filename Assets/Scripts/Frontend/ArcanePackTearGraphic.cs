using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Rasgo luminoso procedural para a abertura de pacotes. A geometria e
    /// desenhada no Canvas, sem textura externa, e permanece leve no Android.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArcanePackTearGraphic : MaskableGraphic
    {
        // Mais segmentos deixam a cabeça do corte avançar continuamente em
        // telas de 30/60 FPS, sem o efeito de "degraus" dos 18 segmentos
        // anteriores. O buffer é reutilizado para não gerar lixo por frame.
        private const int SegmentCount = 36;

        [SerializeField, Range(0f, 1f)] private float progress;
        [SerializeField, Range(0f, 1.5f)] private float energy = 1f;
        [SerializeField]
        private Color outerColor = new(0.05f, 0.82f, 1f, 0.28f);
        [SerializeField]
        private Color middleColor = new(1f, 0.72f, 0.24f, 0.62f);
        [SerializeField]
        private Color coreColor = new(0.92f, 1f, 1f, 1f);

        private readonly Vector2[] _points =
            new Vector2[SegmentCount + 1];

        public void SetPalette(Color outer, Color middle, Color core)
        {
            outerColor = outer;
            middleColor = middle;
            coreColor = core;
            SetVerticesDirty();
        }

        public void SetState(float visibleProgress, float intensity)
        {
            float nextProgress = Mathf.Clamp01(Sanitize(visibleProgress, 0f));
            float nextEnergy = Mathf.Clamp(Sanitize(intensity, 0f), 0f, 1.5f);
            if (Mathf.Approximately(progress, nextProgress) &&
                Mathf.Approximately(energy, nextEnergy))
            {
                return;
            }

            progress = nextProgress;
            energy = nextEnergy;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (progress <= 0.001f || energy <= 0.001f)
                return;

            Rect rect = GetPixelAdjustedRect();
            int visibleSegments = Mathf.Clamp(
                Mathf.CeilToInt(SegmentCount * progress), 1, SegmentCount);
            float centerY = rect.center.y;
            float amplitude = Mathf.Max(2.5f, rect.height * 0.18f);
            for (int index = 0; index <= visibleSegments; index++)
            {
                float normalized = index / (float)SegmentCount;
                float clipped = Mathf.Min(normalized, progress);
                _points[index] = new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, clipped),
                    centerY + JaggedOffset(clipped) * amplitude);
            }

            int pointCount = visibleSegments + 1;
            AddStrip(helper, _points, pointCount,
                Mathf.Max(13f, rect.height * 0.72f),
                WithEnergy(outerColor, 0.78f));
            AddStrip(helper, _points, pointCount,
                Mathf.Max(5.5f, rect.height * 0.31f),
                WithEnergy(middleColor, 0.94f));
            AddStrip(helper, _points, pointCount,
                Mathf.Max(1.8f, rect.height * 0.10f),
                WithEnergy(coreColor, 1f));

            Vector2 head = _points[visibleSegments];
            float headSize = Mathf.Max(8f, rect.height * 0.46f) *
                Mathf.Lerp(0.72f, 1.18f,
                    Mathf.Max(0f, Mathf.Sin(progress * Mathf.PI * 5f)));
            AddDiamond(helper, head, headSize * 1.85f,
                WithEnergy(outerColor, 0.42f));
            AddDiamond(helper, head, headSize,
                WithEnergy(coreColor, 0.95f));

            // Fragmentos de energia acompanham a cabeca do corte e evitam a
            // leitura de uma simples linha que aumenta de tamanho.
            for (int sparkIndex = 0; sparkIndex < 4; sparkIndex++)
            {
                float phase = Mathf.Max(0f, Mathf.Sin(
                    progress * Mathf.PI * 7f + sparkIndex * 1.37f));
                float direction = sparkIndex % 2 == 0 ? 1f : -1f;
                Vector2 sparkCenter = head + new Vector2(
                    -headSize * (1.25f + sparkIndex * 0.82f),
                    direction * headSize * (0.58f + sparkIndex * 0.13f));
                AddDiamond(
                    helper,
                    sparkCenter,
                    headSize * Mathf.Lerp(0.16f, 0.34f, phase),
                    WithEnergy(
                        sparkIndex % 2 == 0 ? middleColor : coreColor,
                        0.42f + phase * 0.34f));
            }
        }

        private Color WithEnergy(Color source, float multiplier)
        {
            source.a = Mathf.Clamp01(source.a * energy * multiplier);
            return source;
        }

        private static void AddStrip(
            VertexHelper helper,
            Vector2[] points,
            int pointCount,
            float thickness,
            Color color)
        {
            if (points == null || pointCount < 2)
                return;

            float half = thickness * 0.5f;
            for (int index = 0; index < pointCount - 1; index++)
            {
                Vector2 start = points[index];
                Vector2 end = points[index + 1];
                Vector2 direction = end - start;
                if (direction.sqrMagnitude < 0.0001f)
                    continue;
                direction.Normalize();
                Vector2 normal = new(-direction.y, direction.x);
                float taper = Mathf.Lerp(0.72f, 1f,
                    index / Mathf.Max(1f, pointCount - 2f));
                Vector2 offset = normal * half * taper;
                int vertex = helper.currentVertCount;
                helper.AddVert(start - offset, color, Vector2.zero);
                helper.AddVert(start + offset, color, Vector2.up);
                helper.AddVert(end + offset, color, Vector2.one);
                helper.AddVert(end - offset, color, Vector2.right);
                helper.AddTriangle(vertex, vertex + 1, vertex + 2);
                helper.AddTriangle(vertex, vertex + 2, vertex + 3);
            }
        }

        private static float JaggedOffset(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            float position = normalized * SegmentCount;
            float detail = Mathf.Sin(position * 2.21f) * 0.17f +
                Mathf.Sin(position * 5.17f + 0.43f) * 0.085f;
            return detail * envelope;
        }

        private static void AddDiamond(
            VertexHelper helper,
            Vector2 center,
            float size,
            Color color)
        {
            int vertex = helper.currentVertCount;
            helper.AddVert(center + Vector2.up * size, color, Vector2.up);
            helper.AddVert(center + Vector2.right * size * 0.62f,
                color, Vector2.right);
            helper.AddVert(center + Vector2.down * size, color, Vector2.down);
            helper.AddVert(center + Vector2.left * size * 0.62f,
                color, Vector2.left);
            helper.AddTriangle(vertex, vertex + 1, vertex + 2);
            helper.AddTriangle(vertex, vertex + 2, vertex + 3);
        }

        private static float Sanitize(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : value;
        }
    }
}
