using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Aura vetorial animada da moldura hexagonal. O centro permanece vazio:
    /// toda a geometria é construída na faixa exterior do hexágono, portanto
    /// a arte e o rosto do avatar nunca recebem uma sobreposição.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HexIconAuraView : MaskableGraphic
    {
        public const string LayerObjectName = "Aura Viva da Moldura";
        public const float InnerPortraitSafeScale = 0.70f;

        private const int EdgeCount = 6;
        private const int RunnersPerEdge = 5;
        private const int ParticleCount = 14;
        private const float BaseHexScale = 0.765f;
        private ProfileIconAuraTheme _theme;
        private float _instancePhase;

        public ProfileIconAuraTheme Theme => _theme;

        public override Texture mainTexture => Texture2D.whiteTexture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            // Use hierarchy position as a stable phase offset without relying on the
            // deprecated Unity object instance identifier.
            _instancePhase = Mathf.Repeat((transform.GetSiblingIndex() + 1) * 0.173f, 1f);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void SetTheme(ProfileIconAuraTheme theme)
        {
            _theme = theme;
            bool visible = theme != ProfileIconAuraTheme.None;
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
            if (visible)
                SetVerticesDirty();
        }

        private void Update()
        {
            if (_theme != ProfileIconAuraTheme.None && isActiveAndEnabled)
                SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (_theme == ProfileIconAuraTheme.None)
                return;

            Rect rect = GetPixelAdjustedRect();
            float time = Application.isPlaying
                ? Time.unscaledTime
                : Time.realtimeSinceStartup;
            time += _instancePhase * 6.2831853f;
            float pulse = 0.72f + 0.28f *
                (0.5f + 0.5f * Mathf.Sin(time * 2.15f));
            AuraPalette palette = PaletteFor(_theme);

            // Moldura exclusiva e opaca: ocupa exatamente a faixa entre o
            // recorte do retrato (89% do ícone) e o contorno externo. Assim,
            // não depende da antiga moldura azul e não deixa lacunas.
            AddLivingFrame(vertexHelper, rect, time, pulse, palette);

            // Três anéis suaves produzem o halo difuso. Eles começam depois
            // da área segura do retrato e se estendem para fora da moldura.
            AddHexRing(vertexHelper, rect, BaseHexScale, 0.805f,
                WithAlpha(palette.glow, 0.12f * pulse));
            AddHexRing(vertexHelper, rect, 0.800f, 0.850f,
                WithAlpha(palette.secondary, 0.085f * pulse));
            AddHexRing(vertexHelper, rect, 0.844f, 0.915f,
                WithAlpha(palette.glow, 0.035f * pulse));

            // Linha de energia principal, mais fina e mais luminosa.
            AddAnimatedEnergyTrack(
                vertexHelper, rect, time, pulse, palette);
            AddWisps(vertexHelper, rect, time, pulse, palette);
            AddParticles(vertexHelper, rect, time, pulse, palette);
        }

        private static void AddLivingFrame(
            VertexHelper vh,
            Rect rect,
            float time,
            float pulse,
            AuraPalette palette)
        {
            const float inner = 0.674f;
            const float outer = 0.765f;
            for (int edge = 0; edge < EdgeCount; edge++)
            {
                int next = (edge + 1) % EdgeCount;
                const int subdivisions = 6;
                for (int segment = 0; segment < subdivisions; segment++)
                {
                    float a = segment / (float)subdivisions;
                    float b = (segment + 1f) / subdivisions;
                    float perimeter = (edge + (a + b) * 0.5f) / EdgeCount;
                    float travellingLight = 0.5f + 0.5f * Mathf.Sin(
                        perimeter * 12.566371f - time * 2.75f);
                    Color frame = Color.Lerp(
                        palette.primary,
                        palette.secondary,
                        travellingLight);
                    frame = Color.Lerp(
                        frame,
                        palette.highlight,
                        Mathf.Pow(travellingLight, 7f) * 0.72f);
                    frame.a = 0.90f + pulse * 0.10f;
                    AddQuad(
                        vh,
                        Vector2.Lerp(
                            HexPoint(rect, edge, inner),
                            HexPoint(rect, next, inner), a),
                        Vector2.Lerp(
                            HexPoint(rect, edge, inner),
                            HexPoint(rect, next, inner), b),
                        Vector2.Lerp(
                            HexPoint(rect, edge, outer),
                            HexPoint(rect, next, outer), b),
                        Vector2.Lerp(
                            HexPoint(rect, edge, outer),
                            HexPoint(rect, next, outer), a),
                        frame);
                }
            }

            // Fio interno luminoso separa a arte da energia sem cobrir o
            // retrato; o centro do componente permanece totalmente vazio.
            AddHexRing(vh, rect, inner, inner + 0.014f,
                WithAlpha(palette.highlight, 0.72f + pulse * 0.22f));
        }

        private static void AddAnimatedEnergyTrack(
            VertexHelper vh,
            Rect rect,
            float time,
            float pulse,
            AuraPalette palette)
        {
            for (int edge = 0; edge < EdgeCount; edge++)
            {
                int next = (edge + 1) % EdgeCount;
                Vector2 innerStart = HexPoint(rect, edge, 0.800f);
                Vector2 innerEnd = HexPoint(rect, next, 0.800f);
                Vector2 outerStart = HexPoint(rect, edge, 0.828f);
                Vector2 outerEnd = HexPoint(rect, next, 0.828f);
                for (int segment = 0; segment < RunnersPerEdge; segment++)
                {
                    float a = segment / (float)RunnersPerEdge;
                    float b = (segment + 1f) / RunnersPerEdge;
                    float progress = (edge + (a + b) * 0.5f) / EdgeCount;
                    float wave = 0.5f + 0.5f * Mathf.Sin(
                        progress * 18.849556f - time * 3.9f);
                    float spark = Mathf.Pow(wave, 4.5f);
                    Color energy = Color.Lerp(
                        palette.primary, palette.highlight, spark);
                    energy.a = (0.20f + spark * 0.68f) * pulse;
                    AddQuad(
                        vh,
                        Vector2.Lerp(innerStart, innerEnd, a),
                        Vector2.Lerp(innerStart, innerEnd, b),
                        Vector2.Lerp(outerStart, outerEnd, b),
                        Vector2.Lerp(outerStart, outerEnd, a),
                        energy);
                }
            }
        }

        private static void AddWisps(
            VertexHelper vh,
            Rect rect,
            float time,
            float pulse,
            AuraPalette palette)
        {
            Vector2 center = rect.center;
            float minimum = Mathf.Min(rect.width, rect.height);
            for (int i = 0; i < 12; i++)
            {
                float phase = i / 12f;
                float moving = Mathf.Repeat(phase + time *
                    (0.018f + (i % 3) * 0.004f), 1f);
                Vector2 basePoint = PointOnHex(rect, moving, 0.835f);
                Vector2 outward = (basePoint - center).normalized;
                Vector2 tangent = new(-outward.y, outward.x);
                float sway = Mathf.Sin(time * 1.7f + i * 2.31f);
                float length = minimum * (0.055f +
                    0.032f * (0.5f + 0.5f * sway));
                float width = minimum * (0.009f + (i % 2) * 0.003f);
                Vector2 tip = basePoint + outward * length +
                    tangent * sway * minimum * 0.014f;
                Color baseColor = WithAlpha(
                    i % 3 == 0 ? palette.secondary : palette.primary,
                    0.32f * pulse);
                Color tipColor = WithAlpha(palette.highlight, 0f);
                AddTaperedTriangle(
                    vh,
                    basePoint - tangent * width,
                    basePoint + tangent * width,
                    tip,
                    baseColor,
                    tipColor);
            }
        }

        private static void AddParticles(
            VertexHelper vh,
            Rect rect,
            float time,
            float pulse,
            AuraPalette palette)
        {
            Vector2 center = rect.center;
            float minimum = Mathf.Min(rect.width, rect.height);
            for (int i = 0; i < ParticleCount; i++)
            {
                float seed = Hash01(i * 17.173f + 4.91f);
                float lifetime = Mathf.Repeat(
                    time * (0.13f + seed * 0.055f) + seed * 7.31f,
                    1f);
                float perimeter = Mathf.Repeat(
                    seed + time * (0.006f + (i % 4) * 0.002f),
                    1f);
                Vector2 edge = PointOnHex(rect, perimeter, 0.850f);
                Vector2 outward = (edge - center).normalized;
                Vector2 tangent = new(-outward.y, outward.x);
                Vector2 position = edge +
                    outward * minimum * (0.018f + lifetime * 0.095f) +
                    Vector2.up * minimum * lifetime * 0.035f +
                    tangent * Mathf.Sin(time + i * 1.83f) * minimum * 0.012f;
                float fade = 4f * lifetime * (1f - lifetime);
                float size = minimum * Mathf.Lerp(0.010f, 0.022f, seed) *
                    Mathf.Lerp(1f, 0.38f, lifetime);
                Color particle = Color.Lerp(
                    palette.secondary, palette.highlight, seed);
                particle.a = fade * pulse * 0.68f;
                AddDiamond(vh, position, size, particle);
            }
        }

        private static void AddHexRing(
            VertexHelper vh,
            Rect rect,
            float innerScale,
            float outerScale,
            Color color)
        {
            for (int i = 0; i < EdgeCount; i++)
            {
                int next = (i + 1) % EdgeCount;
                AddQuad(
                    vh,
                    HexPoint(rect, i, innerScale),
                    HexPoint(rect, next, innerScale),
                    HexPoint(rect, next, outerScale),
                    HexPoint(rect, i, outerScale),
                    color);
            }
        }

        private static Vector2 HexPoint(Rect rect, int index, float scale)
        {
            Vector2 center = rect.center;
            float halfWidth = rect.width * 0.5f * scale;
            float halfHeight = rect.height * 0.5f * scale;
            return ((index % EdgeCount + EdgeCount) % EdgeCount) switch
            {
                0 => center + new Vector2(0f, halfHeight),
                1 => center + new Vector2(halfWidth, halfHeight * 0.5f),
                2 => center + new Vector2(halfWidth, -halfHeight * 0.5f),
                3 => center + new Vector2(0f, -halfHeight),
                4 => center + new Vector2(-halfWidth, -halfHeight * 0.5f),
                _ => center + new Vector2(-halfWidth, halfHeight * 0.5f)
            };
        }

        private static Vector2 PointOnHex(
            Rect rect,
            float progress,
            float scale)
        {
            float edgeProgress = Mathf.Repeat(progress, 1f) * EdgeCount;
            int edge = Mathf.FloorToInt(edgeProgress) % EdgeCount;
            float local = edgeProgress - Mathf.Floor(edgeProgress);
            return Vector2.Lerp(
                HexPoint(rect, edge, scale),
                HexPoint(rect, edge + 1, scale),
                local);
        }

        private static void AddQuad(
            VertexHelper vh,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(Vertex(a, color));
            vh.AddVert(Vertex(b, color));
            vh.AddVert(Vertex(c, color));
            vh.AddVert(Vertex(d, color));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        private static void AddTaperedTriangle(
            VertexHelper vh,
            Vector2 left,
            Vector2 right,
            Vector2 tip,
            Color baseColor,
            Color tipColor)
        {
            int start = vh.currentVertCount;
            vh.AddVert(Vertex(left, baseColor));
            vh.AddVert(Vertex(right, baseColor));
            vh.AddVert(Vertex(tip, tipColor));
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddDiamond(
            VertexHelper vh,
            Vector2 center,
            float size,
            Color color)
        {
            AddQuad(
                vh,
                center + new Vector2(0f, size),
                center + new Vector2(size * 0.62f, 0f),
                center + new Vector2(0f, -size),
                center + new Vector2(-size * 0.62f, 0f),
                color);
        }

        private static UIVertex Vertex(Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = new Vector2(0.5f, 0.5f);
            return vertex;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static float Hash01(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
        }

        private static AuraPalette PaletteFor(ProfileIconAuraTheme theme)
        {
            return theme switch
            {
                ProfileIconAuraTheme.AzureArcane => new AuraPalette(
                    new Color(0.02f, 0.72f, 1f),
                    new Color(0.18f, 0.22f, 1f),
                    new Color(0.70f, 0.96f, 1f),
                    new Color(0.06f, 0.48f, 1f)),
                ProfileIconAuraTheme.SolarLegendary => new AuraPalette(
                    new Color(1f, 0.48f, 0.02f),
                    new Color(1f, 0.88f, 0.12f),
                    new Color(1f, 1f, 0.72f),
                    new Color(1f, 0.58f, 0.04f)),
                _ => new AuraPalette(
                    new Color(1f, 0.025f, 0.12f),
                    new Color(0.94f, 0.02f, 0.72f),
                    new Color(1f, 0.68f, 0.93f),
                    new Color(0.70f, 0.04f, 0.80f))
            };
        }

        private readonly struct AuraPalette
        {
            public readonly Color primary;
            public readonly Color secondary;
            public readonly Color highlight;
            public readonly Color glow;

            public AuraPalette(
                Color primary,
                Color secondary,
                Color highlight,
                Color glow)
            {
                this.primary = primary;
                this.secondary = secondary;
                this.highlight = highlight;
                this.glow = glow;
            }
        }
    }
}
