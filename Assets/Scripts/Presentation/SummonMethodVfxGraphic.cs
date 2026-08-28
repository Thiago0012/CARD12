using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Shared visual identity for the five alternative summon methods. This is
    /// presentation-only and never participates in duel legality.
    /// </summary>
    public static class SummonMethodVfxPalette
    {
        public static bool Supports(MonsterFrameKind frame)
        {
            return frame == MonsterFrameKind.Fusion ||
                   frame == MonsterFrameKind.Synchro ||
                   frame == MonsterFrameKind.Xyz ||
                   frame == MonsterFrameKind.Link ||
                   frame == MonsterFrameKind.Pendulum;
        }

        public static Color Primary(MonsterFrameKind frame)
        {
            return frame switch
            {
                MonsterFrameKind.Fusion => Hex("#D96BFF"),
                MonsterFrameKind.Synchro => Hex("#F4FCFF"),
                MonsterFrameKind.Xyz => Hex("#FFD568"),
                MonsterFrameKind.Link => Hex("#42C8FF"),
                MonsterFrameKind.Pendulum => Hex("#4AF2C3"),
                _ => Hex("#52C3FF")
            };
        }

        public static Color Secondary(MonsterFrameKind frame)
        {
            return frame switch
            {
                MonsterFrameKind.Fusion => Hex("#FF477F"),
                MonsterFrameKind.Synchro => Hex("#52D9FF"),
                MonsterFrameKind.Xyz => Hex("#7255C7"),
                MonsterFrameKind.Link => Hex("#3474FF"),
                MonsterFrameKind.Pendulum => Hex("#F061FF"),
                _ => Hex("#E8F7FF")
            };
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color parsed)
                ? parsed
                : Color.white;
        }
    }

    /// <summary>
    /// Texture-free summon sigil. One batched UI mesh keeps the effect light
    /// enough for Android while giving every summon method a distinct motion
    /// language.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SummonMethodVfxGraphic : MaskableGraphic
    {
        private MonsterFrameKind frameKind = MonsterFrameKind.Unknown;
        private Color primary = Color.cyan;
        private Color secondary = Color.white;
        private float progress;
        private float animationTime;
        private bool reducedDetail;
        private readonly Vector2[] linkNodes = new Vector2[8];

        public MonsterFrameKind FrameKind => frameKind;
        public bool ReducedDetail => reducedDetail;

        public void Configure(
            MonsterFrameKind frame,
            Color primaryColor,
            Color secondaryColor,
            bool useReducedDetail)
        {
            frameKind = frame;
            primary = primaryColor;
            secondary = secondaryColor;
            reducedDetail = useReducedDetail;
            raycastTarget = false;
            color = Color.white;
            SetVerticesDirty();
        }

        public void SetAnimation(float normalizedProgress, float time)
        {
            progress = Mathf.Clamp01(normalizedProgress);
            animationTime = time;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!SummonMethodVfxPalette.Supports(frameKind))
                return;

            Rect rect = rectTransform.rect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Vector2 center = rect.center;
            float extent = Mathf.Min(rect.width, rect.height);
            float reveal = EaseOutCubic(Mathf.InverseLerp(0f, 0.58f, progress));
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float radius = extent * Mathf.Lerp(0.16f, 0.43f, reveal);

            switch (frameKind)
            {
                case MonsterFrameKind.Fusion:
                    BuildFusion(helper, center, radius, envelope);
                    break;
                case MonsterFrameKind.Synchro:
                    BuildSynchro(helper, center, radius, envelope);
                    break;
                case MonsterFrameKind.Xyz:
                    BuildXyz(helper, center, radius, envelope);
                    break;
                case MonsterFrameKind.Link:
                    BuildLink(helper, center, radius, envelope);
                    break;
                case MonsterFrameKind.Pendulum:
                    BuildPendulum(helper, center, radius, envelope);
                    break;
            }
        }

        private void BuildFusion(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float envelope)
        {
            float rotation = animationTime * 118f;
            int segments = reducedDetail ? 18 : 28;
            AddSpiral(
                helper,
                center + Vector2.left * radius * 0.12f,
                radius * 0.92f,
                radius * 0.10f,
                rotation,
                1.55f,
                segments,
                Mathf.Max(2.2f, radius * 0.018f),
                WithAlpha(primary, envelope * 0.84f));
            AddSpiral(
                helper,
                center + Vector2.right * radius * 0.12f,
                radius * 0.92f,
                radius * 0.10f,
                180f - rotation,
                -1.55f,
                segments,
                Mathf.Max(2.2f, radius * 0.018f),
                WithAlpha(secondary, envelope * 0.82f));
            AddRing(
                helper,
                center,
                radius * (0.40f + 0.08f * Mathf.Sin(animationTime * 7f)),
                radius * 0.035f,
                20,
                rotation * -0.42f,
                WithAlpha(Color.white, envelope * 0.72f));
            AddDiamondOutline(
                helper,
                center,
                radius * 0.31f,
                radius * 0.020f,
                rotation * 0.18f,
                WithAlpha(primary, envelope * 0.62f));
        }

        private void BuildSynchro(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float envelope)
        {
            float rotation = animationTime * 74f;
            int ringSegments = reducedDetail ? 20 : 32;
            for (int index = 0; index < 3; index++)
            {
                float ringRadius = radius * (0.34f + index * 0.25f);
                float direction = index % 2 == 0 ? 1f : -1f;
                AddArc(
                    helper,
                    center,
                    ringRadius,
                    Mathf.Max(1.8f, radius * (0.018f - index * 0.002f)),
                    ringSegments,
                    rotation * direction + index * 33f,
                    286f,
                    WithAlpha(
                        index == 1 ? secondary : primary,
                        envelope * (0.88f - index * 0.14f)));
            }

            int ticks = reducedDetail ? 8 : 12;
            for (int index = 0; index < ticks; index++)
            {
                float angle = rotation * -0.24f + index * 360f / ticks;
                Vector2 direction = Direction(angle);
                float length = index % 3 == 0 ? 0.16f : 0.09f;
                AddLine(
                    helper,
                    center + direction * radius * (0.78f - length),
                    center + direction * radius * 0.78f,
                    Mathf.Max(1.5f, radius * 0.012f),
                    WithAlpha(primary, envelope * 0.90f));
            }
            AddStar(
                helper,
                center,
                radius * 0.31f,
                rotation * -0.16f,
                WithAlpha(Color.white, envelope * 0.82f));
        }

        private void BuildXyz(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float envelope)
        {
            float rotation = animationTime * 92f;
            AddDisc(
                helper,
                center,
                radius * 0.72f,
                reducedDetail ? 16 : 24,
                WithAlpha(new Color(0.005f, 0.004f, 0.02f), envelope * 0.52f));
            int orbitSegments = reducedDetail ? 18 : 28;
            AddEllipseArc(
                helper,
                center,
                new Vector2(radius * 0.90f, radius * 0.42f),
                radius * 0.020f,
                orbitSegments,
                rotation,
                WithAlpha(primary, envelope * 0.82f));
            AddEllipseArc(
                helper,
                center,
                new Vector2(radius * 0.82f, radius * 0.34f),
                radius * 0.017f,
                orbitSegments,
                -rotation - 58f,
                WithAlpha(secondary, envelope * 0.72f));
            AddRing(
                helper,
                center,
                radius * 0.37f,
                radius * 0.026f,
                reducedDetail ? 18 : 26,
                -rotation * 0.4f,
                WithAlpha(primary, envelope * 0.64f));

            int sparks = reducedDetail ? 5 : 8;
            for (int index = 0; index < sparks; index++)
            {
                float angle = rotation + index * 360f / sparks;
                Vector2 point = center + new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius * 0.90f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius * 0.42f);
                AddDiamondOutline(
                    helper,
                    point,
                    radius * (index % 2 == 0 ? 0.055f : 0.038f),
                    Mathf.Max(1.2f, radius * 0.011f),
                    -angle,
                    WithAlpha(primary, envelope * 0.92f));
            }

            // A falling star field makes the Xyz portal read as depth rather
            // than another generic ring. Positions are deterministic, so the
            // mesh remains cheap and stable on mobile.
            int fallingStars = reducedDetail ? 6 : 12;
            for (int index = 0; index < fallingStars; index++)
            {
                float fall = Mathf.Repeat(
                    progress * (1.25f + index % 3 * 0.12f) +
                    index * 0.137f,
                    1f);
                float x = center.x +
                          Mathf.Sin(index * 19.73f) * radius * 0.92f;
                float y = Mathf.Lerp(
                    center.y + radius * 1.08f,
                    center.y - radius * 0.96f,
                    fall);
                float length = radius * (0.07f + index % 3 * 0.025f);
                AddLine(
                    helper,
                    new Vector2(x, y + length),
                    new Vector2(x, y - length),
                    Mathf.Max(1f, radius * 0.008f),
                    WithAlpha(
                        index % 2 == 0 ? primary : Color.white,
                        envelope * (1f - fall) * 0.82f));
            }
        }

        private void BuildLink(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float envelope)
        {
            float rotation = animationTime * 26f;
            int nodeCount = reducedDetail ? 6 : 8;
            for (int index = 0; index < nodeCount; index++)
            {
                float angle = rotation + index * 360f / nodeCount;
                linkNodes[index] =
                    center + Direction(angle) * radius * 0.77f;
                AddLine(
                    helper,
                    center,
                    linkNodes[index],
                    Mathf.Max(1.6f, radius * 0.013f),
                    WithAlpha(secondary, envelope * 0.42f));
                AddDiamondOutline(
                    helper,
                    linkNodes[index],
                    radius * 0.070f,
                    Mathf.Max(1.5f, radius * 0.013f),
                    angle,
                    WithAlpha(primary, envelope * 0.90f));
            }
            for (int index = 0; index < nodeCount; index++)
            {
                AddLine(
                    helper,
                    linkNodes[index],
                    linkNodes[(index + 1) % nodeCount],
                    Mathf.Max(1.4f, radius * 0.011f),
                    WithAlpha(primary, envelope * 0.65f));
            }
            if (!reducedDetail)
            {
                for (int index = 0; index < nodeCount; index += 2)
                {
                    AddLine(
                        helper,
                        linkNodes[index],
                        linkNodes[(index + 3) % nodeCount],
                        Mathf.Max(1.1f, radius * 0.008f),
                        WithAlpha(secondary, envelope * 0.34f));
                }
            }
            AddDiamondOutline(
                helper,
                center,
                radius * 0.20f,
                radius * 0.025f,
                -rotation,
                WithAlpha(Color.white, envelope * 0.82f));

            // Long converging Link arrows construct the silhouette of the
            // arriving card. Their heads point inward instead of orbiting.
            int arrowCount = reducedDetail ? 4 : 8;
            for (int index = 0; index < arrowCount; index++)
            {
                float angle = 22.5f + index * 360f / arrowCount;
                Vector2 direction = Direction(angle);
                AddArrow(
                    helper,
                    center + direction * radius * 1.08f,
                    center + direction * radius * 0.29f,
                    Mathf.Max(1.8f, radius * 0.016f),
                    radius * 0.075f,
                    WithAlpha(
                        index % 2 == 0 ? primary : secondary,
                        envelope * 0.88f));
            }
            Vector2 cardHalf = new(radius * 0.18f, radius * 0.265f);
            Vector2 topLeft = center + new Vector2(-cardHalf.x, cardHalf.y);
            Vector2 topRight = center + cardHalf;
            Vector2 bottomRight = center +
                                  new Vector2(cardHalf.x, -cardHalf.y);
            Vector2 bottomLeft = center - cardHalf;
            Color cardEdge = WithAlpha(Color.white, envelope * 0.72f);
            AddLine(helper, topLeft, topRight, radius * 0.016f, cardEdge);
            AddLine(helper, topRight, bottomRight, radius * 0.016f, cardEdge);
            AddLine(helper, bottomRight, bottomLeft, radius * 0.016f, cardEdge);
            AddLine(helper, bottomLeft, topLeft, radius * 0.016f, cardEdge);
        }

        private void BuildPendulum(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float envelope)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(animationTime * 7f);
            float beamOffset = radius * 0.66f;
            float beamHeight = radius * (1.35f + pulse * 0.16f);
            float beamWidth = Mathf.Max(3f, radius * 0.028f);
            AddLine(
                helper,
                center + new Vector2(-beamOffset, -beamHeight * 0.5f),
                center + new Vector2(-beamOffset, beamHeight * 0.5f),
                beamWidth,
                WithAlpha(primary, envelope * 0.90f));
            AddLine(
                helper,
                center + new Vector2(beamOffset, -beamHeight * 0.5f),
                center + new Vector2(beamOffset, beamHeight * 0.5f),
                beamWidth,
                WithAlpha(secondary, envelope * 0.90f));
            AddArc(
                helper,
                center + Vector2.down * radius * 0.18f,
                radius * 0.87f,
                Mathf.Max(2f, radius * 0.017f),
                reducedDetail ? 20 : 32,
                18f,
                144f,
                WithAlpha(primary, envelope * 0.72f));
            AddArc(
                helper,
                center + Vector2.down * radius * 0.18f,
                radius * 0.75f,
                Mathf.Max(1.8f, radius * 0.015f),
                reducedDetail ? 20 : 32,
                18f,
                144f,
                WithAlpha(secondary, envelope * 0.68f));
            AddLine(
                helper,
                center + new Vector2(-beamOffset, radius * 0.50f),
                center,
                Mathf.Max(1.4f, radius * 0.012f),
                WithAlpha(primary, envelope * 0.58f));
            AddLine(
                helper,
                center,
                center + new Vector2(beamOffset, radius * 0.50f),
                Mathf.Max(1.4f, radius * 0.012f),
                WithAlpha(secondary, envelope * 0.58f));
            AddDiamondOutline(
                helper,
                center,
                radius * (0.19f + pulse * 0.035f),
                radius * 0.024f,
                45f,
                WithAlpha(Color.white, envelope * 0.84f));

            // Central balance: the two scale cards hang from a beam that
            // settles as the summon reaches its reveal point.
            float swing = Mathf.Sin(animationTime * 4.2f) *
                          Mathf.Lerp(13f, 2f, progress);
            Vector2 pivot = center + Vector2.up * radius * 0.47f;
            Vector2 bar = Direction(swing) * radius * 0.46f;
            Vector2 leftEnd = pivot - bar;
            Vector2 rightEnd = pivot + bar;
            AddLine(
                helper,
                center + Vector2.up * radius * 0.84f,
                pivot,
                Mathf.Max(1.8f, radius * 0.015f),
                WithAlpha(Color.white, envelope * 0.72f));
            AddLine(
                helper,
                leftEnd,
                rightEnd,
                Mathf.Max(2.2f, radius * 0.020f),
                WithAlpha(Color.white, envelope * 0.86f));
            float hanger = radius * 0.25f;
            AddLine(
                helper,
                leftEnd,
                leftEnd + Vector2.down * hanger,
                Mathf.Max(1.4f, radius * 0.012f),
                WithAlpha(primary, envelope * 0.78f));
            AddLine(
                helper,
                rightEnd,
                rightEnd + Vector2.down * hanger,
                Mathf.Max(1.4f, radius * 0.012f),
                WithAlpha(secondary, envelope * 0.78f));
            AddArc(
                helper,
                leftEnd + Vector2.down * hanger,
                radius * 0.15f,
                radius * 0.014f,
                reducedDetail ? 10 : 16,
                200f,
                140f,
                WithAlpha(primary, envelope * 0.82f));
            AddArc(
                helper,
                rightEnd + Vector2.down * hanger,
                radius * 0.15f,
                radius * 0.014f,
                reducedDetail ? 10 : 16,
                200f,
                140f,
                WithAlpha(secondary, envelope * 0.82f));
        }

        private static void AddSpiral(
            VertexHelper helper,
            Vector2 center,
            float startRadius,
            float endRadius,
            float startAngle,
            float turns,
            int segments,
            float width,
            Color value)
        {
            Vector2 previous = center + Direction(startAngle) * startRadius;
            for (int index = 1; index <= segments; index++)
            {
                float t = index / (float)segments;
                float angle = startAngle + turns * 360f * t;
                float radius = Mathf.Lerp(startRadius, endRadius, t);
                Vector2 current = center + Direction(angle) * radius;
                Color faded = WithAlpha(value, value.a * Mathf.Lerp(0.45f, 1f, t));
                AddLine(helper, previous, current, width, faded);
                previous = current;
            }
        }

        private static void AddRing(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float width,
            int segments,
            float rotation,
            Color value)
        {
            AddArc(helper, center, radius, width, segments, rotation, 360f, value);
        }

        private static void AddArc(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float width,
            int segments,
            float rotation,
            float sweep,
            Color value)
        {
            Vector2 previous = center + Direction(rotation) * radius;
            for (int index = 1; index <= segments; index++)
            {
                float angle = rotation + sweep * index / segments;
                Vector2 current = center + Direction(angle) * radius;
                AddLine(helper, previous, current, width, value);
                previous = current;
            }
        }

        private static void AddEllipseArc(
            VertexHelper helper,
            Vector2 center,
            Vector2 radii,
            float width,
            int segments,
            float rotation,
            Color value)
        {
            Vector2 Point(float angle)
            {
                float radians = angle * Mathf.Deg2Rad;
                return center + new Vector2(
                    Mathf.Cos(radians) * radii.x,
                    Mathf.Sin(radians) * radii.y);
            }

            Vector2 previous = Point(rotation);
            for (int index = 1; index <= segments; index++)
            {
                Vector2 current = Point(rotation + 360f * index / segments);
                AddLine(helper, previous, current, width, value);
                previous = current;
            }
        }

        private static void AddStar(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float rotation,
            Color value)
        {
            for (int index = 0; index < 8; index++)
            {
                float angle = rotation + index * 45f;
                float length = index % 2 == 0 ? radius : radius * 0.66f;
                AddLine(
                    helper,
                    center - Direction(angle) * length,
                    center + Direction(angle) * length,
                    Mathf.Max(1.4f, radius * 0.035f),
                    value);
            }
        }

        private static void AddDiamondOutline(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float width,
            float rotation,
            Color value)
        {
            Vector2 top = center + Direction(rotation) * radius;
            Vector2 right = center + Direction(rotation + 90f) * radius;
            Vector2 bottom = center + Direction(rotation + 180f) * radius;
            Vector2 left = center + Direction(rotation + 270f) * radius;
            AddLine(helper, top, right, width, value);
            AddLine(helper, right, bottom, width, value);
            AddLine(helper, bottom, left, width, value);
            AddLine(helper, left, top, width, value);
        }

        private static void AddDisc(
            VertexHelper helper,
            Vector2 center,
            float radius,
            int segments,
            Color value)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, center, value);
            for (int index = 0; index <= segments; index++)
            {
                AddVertex(
                    helper,
                    center + Direction(index * 360f / segments) * radius,
                    WithAlpha(value, value.a * 0.35f));
            }
            for (int index = 0; index < segments; index++)
                helper.AddTriangle(start, start + index + 1, start + index + 2);
        }

        private static void AddLine(
            VertexHelper helper,
            Vector2 start,
            Vector2 end,
            float width,
            Color value)
        {
            Vector2 tangent = end - start;
            if (tangent.sqrMagnitude <= 0.0001f)
                return;
            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized *
                             (width * 0.5f);
            int first = helper.currentVertCount;
            AddVertex(helper, start - normal, value);
            AddVertex(helper, start + normal, value);
            AddVertex(helper, end + normal, value);
            AddVertex(helper, end - normal, value);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }

        private static void AddArrow(
            VertexHelper helper,
            Vector2 start,
            Vector2 end,
            float width,
            float headSize,
            Color value)
        {
            Vector2 direction = (end - start).normalized;
            if (direction.sqrMagnitude <= 0.0001f)
                return;
            Vector2 side = new(-direction.y, direction.x);
            Vector2 neck = end - direction * headSize;
            AddLine(helper, start, neck, width, value);
            AddLine(
                helper,
                end,
                neck + side * headSize * 0.62f,
                width,
                value);
            AddLine(
                helper,
                end,
                neck - side * headSize * 0.62f,
                width,
                value);
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

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static Color WithAlpha(Color value, float alpha)
        {
            value.a = Mathf.Clamp01(alpha);
            return value;
        }

        private static float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - value, 3f);
        }
    }
}
