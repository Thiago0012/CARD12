using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Superfície procedural do HUD de duelo. Mantém as placas legíveis sem
    /// transformar a arena em uma coleção de retângulos opacos e sem depender
    /// de uma textura específica para cada proporção de tela.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelHudSurfaceGraphic : MaskableGraphic
    {
        private Color accent = new(0.16f, 0.68f, 1f, 1f);
        private bool strongOnLeft = true;
        private bool directional = true;
        private float opacity = 1f;
        private float chamfer = 10f;

        public void SetStyle(
            Color newAccent,
            bool solidAtLeft,
            float surfaceOpacity = 1f,
            bool useDirectionalFade = true,
            float cornerCut = 10f)
        {
            accent = newAccent;
            strongOnLeft = solidAtLeft;
            directional = useDirectionalFade;
            opacity = Mathf.Clamp01(surfaceOpacity);
            chamfer = Mathf.Max(2f, cornerCut);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float cut = Mathf.Min(
                chamfer,
                Mathf.Min(rect.width, rect.height) * 0.24f);
            float middle = Mathf.Lerp(rect.xMin, rect.xMax, 0.52f);
            float[] x =
            {
                rect.xMin,
                rect.xMin + cut,
                middle,
                rect.xMax - cut,
                rect.xMax
            };
            float[] bottom =
            {
                rect.yMin + cut,
                rect.yMin,
                rect.yMin,
                rect.yMin,
                rect.yMin + cut
            };
            float[] top =
            {
                rect.yMax - cut,
                rect.yMax,
                rect.yMax,
                rect.yMax,
                rect.yMax - cut
            };

            int fillStart = helper.currentVertCount;
            for (int index = 0; index < x.Length; index++)
            {
                float normalized = Mathf.InverseLerp(rect.xMin, rect.xMax, x[index]);
                Color lower = SurfaceColor(normalized, false);
                Color upper = SurfaceColor(normalized, true);
                AddVertex(helper, new Vector2(x[index], bottom[index]), lower);
                AddVertex(helper, new Vector2(x[index], top[index]), upper);
            }
            for (int index = 0; index < x.Length - 1; index++)
            {
                int a = fillStart + index * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                helper.AddTriangle(a, b, d);
                helper.AddTriangle(a, d, c);
            }

            AddBorder(helper, rect, cut);
            AddEnergyLine(helper, rect, cut);
        }

        private Color SurfaceColor(float normalizedX, bool upper)
        {
            float strength = directional
                ? (strongOnLeft ? 1f - normalizedX : normalizedX)
                : 0.68f;
            strength = Mathf.SmoothStep(0f, 1f, strength);
            Color deep = new(0.003f, 0.012f, 0.027f, 1f);
            Color tinted = Color.Lerp(
                deep,
                new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.22f, 1f),
                0.35f + strength * 0.65f);
            if (upper)
                tinted = Color.Lerp(tinted, new Color(accent.r, accent.g, accent.b, 1f), 0.055f);
            float alpha = directional
                ? Mathf.Lerp(0.27f, 0.91f, strength)
                : 0.88f;
            tinted.a = alpha * opacity;
            return tinted;
        }

        private void AddBorder(VertexHelper helper, Rect rect, float cut)
        {
            float thickness = Mathf.Clamp(
                Mathf.Min(rect.width, rect.height) * 0.022f,
                1.25f,
                3f);
            Rect inner = new(
                rect.xMin + thickness,
                rect.yMin + thickness,
                Mathf.Max(0f, rect.width - thickness * 2f),
                Mathf.Max(0f, rect.height - thickness * 2f));
            Vector2[] outer = Points(rect, cut);
            Vector2[] inside = Points(inner, Mathf.Max(0f, cut - thickness * 0.45f));
            int start = helper.currentVertCount;
            for (int index = 0; index < outer.Length; index++)
            {
                float normalized = Mathf.InverseLerp(rect.xMin, rect.xMax, outer[index].x);
                float strength = directional
                    ? (strongOnLeft ? 1f - normalized : normalized)
                    : 0.8f;
                Color outside = new(
                    accent.r,
                    accent.g,
                    accent.b,
                    Mathf.Lerp(0.24f, 0.92f, strength) * opacity);
                Color innerColor = new(outside.r, outside.g, outside.b, outside.a * 0.18f);
                AddVertex(helper, outer[index], outside);
                AddVertex(helper, inside[index], innerColor);
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

        private void AddEnergyLine(VertexHelper helper, Rect rect, float cut)
        {
            float startX = strongOnLeft || !directional
                ? rect.xMin + cut
                : rect.xMax - cut;
            float endX = strongOnLeft || !directional
                ? Mathf.Lerp(rect.xMin, rect.xMax, directional ? 0.64f : 0.82f)
                : Mathf.Lerp(rect.xMin, rect.xMax, 0.36f);
            float y = rect.yMax - 2.2f;
            float half = 1.1f;
            Color bright = new(accent.r, accent.g, accent.b, 0.92f * opacity);
            Color faint = new(accent.r, accent.g, accent.b, 0.04f);
            int start = helper.currentVertCount;
            AddVertex(helper, new Vector2(startX, y - half), bright);
            AddVertex(helper, new Vector2(startX, y + half), bright);
            AddVertex(helper, new Vector2(endX, y - half), faint);
            AddVertex(helper, new Vector2(endX, y + half), faint);
            helper.AddTriangle(start, start + 1, start + 3);
            helper.AddTriangle(start, start + 3, start + 2);
        }

        private static Vector2[] Points(Rect rect, float cut)
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

        private static void AddVertex(VertexHelper helper, Vector2 position, Color value)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = value;
            helper.AddVert(vertex);
        }
    }

    /// <summary>
    /// Camada de posse do turno recortada pela geometria interna da arena.
    /// A arte do campo e a moldura universal usam a mesma proporção 16:9,
    /// portanto os pontos são medidos no espaço normalizado do tabuleiro,
    /// e não no retângulo inteiro da tela.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelTurnFieldGlowGraphic : MaskableGraphic
    {
        private bool localTurn = true;
        private Color activeColor = new(0.12f, 0.62f, 1f, 1f);

        public void SetTurn(bool isLocalTurn, Color color)
        {
            localTurn = isLocalTurn;
            activeColor = color;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Vector2[] fieldHalf = FieldHalfPoints(rect, localTurn);
            AddFilledPolygon(helper, fieldHalf, point => FillColor(rect, point));

            // O último segmento fecha junto ao divisor central. Ele fica sem
            // contorno para que a faixa central e seus avisos permaneçam
            // neutros e legíveis.
            AddOuterRim(
                helper,
                fieldHalf,
                Mathf.Clamp(rect.height * 0.0070f, 3.0f, 8.5f),
                new Color(activeColor.r, activeColor.g, activeColor.b, 0.075f));
            AddOuterRim(
                helper,
                fieldHalf,
                Mathf.Clamp(rect.height * 0.0022f, 1.2f, 3.0f),
                new Color(activeColor.r, activeColor.g, activeColor.b, 0.34f));
        }

        private Color FillColor(Rect rect, Vector2 position)
        {
            float normalizedY = Mathf.InverseLerp(
                rect.yMin,
                rect.yMax,
                position.y);
            float fromCenter = localTurn
                ? 1f - Mathf.InverseLerp(0.09f, 0.52f, normalizedY)
                : Mathf.InverseLerp(0.48f, 0.91f, normalizedY);
            float strength = Mathf.Pow(Mathf.Clamp01(fromCenter), 1.28f);
            return new Color(
                activeColor.r,
                activeColor.g,
                activeColor.b,
                Mathf.Lerp(0.006f, 0.175f, strength));
        }

        private static Vector2[] FieldHalfPoints(Rect rect, bool lowerHalf)
        {
            Vector2[] source = lowerHalf
                ? MirroredLowerSourcePoints()
                : UpperSourcePoints();
            Vector2[] result = new Vector2[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                // A arte usa origem no topo; RectTransform usa origem embaixo.
                result[index] = new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, source[index].x),
                    Mathf.Lerp(rect.yMax, rect.yMin, source[index].y));
            }
            return result;
        }

        private static Vector2[] UpperSourcePoints()
        {
            // Delineamento interno da metade superior de field1.png. Os
            // pontos ficam ligeiramente dentro do filete ciano da arena.
            return new[]
            {
                new Vector2(0.184f, 0.462f),
                new Vector2(0.181f, 0.380f),
                new Vector2(0.182f, 0.297f),
                new Vector2(0.185f, 0.235f),
                new Vector2(0.209f, 0.174f),
                new Vector2(0.219f, 0.162f),
                new Vector2(0.286f, 0.128f),
                new Vector2(0.292f, 0.116f),
                new Vector2(0.708f, 0.116f),
                new Vector2(0.714f, 0.128f),
                new Vector2(0.781f, 0.162f),
                new Vector2(0.791f, 0.174f),
                new Vector2(0.815f, 0.235f),
                new Vector2(0.818f, 0.297f),
                new Vector2(0.819f, 0.380f),
                new Vector2(0.816f, 0.462f)
            };
        }

        private static Vector2[] MirroredLowerSourcePoints()
        {
            Vector2[] upper = UpperSourcePoints();
            Vector2[] lower = new Vector2[upper.Length];
            for (int index = 0; index < upper.Length; index++)
            {
                Vector2 source = upper[upper.Length - 1 - index];
                lower[index] = new Vector2(1f - source.x, 1f - source.y);
            }
            return lower;
        }

        private static void AddFilledPolygon(
            VertexHelper helper,
            Vector2[] polygon,
            System.Func<Vector2, Color> colorAt)
        {
            if (polygon == null || polygon.Length < 3)
                return;

            int vertexStart = helper.currentVertCount;
            for (int index = 0; index < polygon.Length; index++)
                AddVertex(helper, polygon[index], colorAt(polygon[index]));

            var remaining = new System.Collections.Generic.List<int>(
                polygon.Length);
            for (int index = 0; index < polygon.Length; index++)
                remaining.Add(index);

            bool counterClockwise = SignedArea(polygon) > 0f;
            int safety = polygon.Length * polygon.Length;
            while (remaining.Count > 3 && safety-- > 0)
            {
                bool clipped = false;
                for (int index = 0; index < remaining.Count; index++)
                {
                    int previous = remaining[
                        (index - 1 + remaining.Count) % remaining.Count];
                    int current = remaining[index];
                    int next = remaining[(index + 1) % remaining.Count];
                    if (!IsEar(
                            polygon,
                            remaining,
                            previous,
                            current,
                            next,
                            counterClockwise))
                    {
                        continue;
                    }

                    AddTriangle(
                        helper,
                        vertexStart,
                        previous,
                        current,
                        next,
                        counterClockwise);
                    remaining.RemoveAt(index);
                    clipped = true;
                    break;
                }
                if (!clipped)
                    return;
            }

            if (remaining.Count == 3)
            {
                AddTriangle(
                    helper,
                    vertexStart,
                    remaining[0],
                    remaining[1],
                    remaining[2],
                    counterClockwise);
            }
        }

        private static bool IsEar(
            Vector2[] polygon,
            System.Collections.Generic.List<int> remaining,
            int previous,
            int current,
            int next,
            bool counterClockwise)
        {
            float cross = Cross(
                polygon[current] - polygon[previous],
                polygon[next] - polygon[current]);
            if (counterClockwise ? cross <= 0.0001f : cross >= -0.0001f)
                return false;

            for (int index = 0; index < remaining.Count; index++)
            {
                int point = remaining[index];
                if (point == previous || point == current || point == next)
                    continue;
                if (InsideTriangle(
                        polygon[point],
                        polygon[previous],
                        polygon[current],
                        polygon[next]))
                {
                    return false;
                }
            }
            return true;
        }

        private static void AddTriangle(
            VertexHelper helper,
            int vertexStart,
            int first,
            int second,
            int third,
            bool counterClockwise)
        {
            if (counterClockwise)
                helper.AddTriangle(
                    vertexStart + first,
                    vertexStart + second,
                    vertexStart + third);
            else
                helper.AddTriangle(
                    vertexStart + first,
                    vertexStart + third,
                    vertexStart + second);
        }

        private static float SignedArea(Vector2[] polygon)
        {
            float area = 0f;
            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 current = polygon[index];
                Vector2 next = polygon[(index + 1) % polygon.Length];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        private static float Cross(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        private static bool InsideTriangle(
            Vector2 point,
            Vector2 first,
            Vector2 second,
            Vector2 third)
        {
            float firstSide = Cross(second - first, point - first);
            float secondSide = Cross(third - second, point - second);
            float thirdSide = Cross(first - third, point - third);
            bool hasNegative = firstSide < -0.0001f ||
                secondSide < -0.0001f || thirdSide < -0.0001f;
            bool hasPositive = firstSide > 0.0001f ||
                secondSide > 0.0001f || thirdSide > 0.0001f;
            return !(hasNegative && hasPositive);
        }

        private static void AddOuterRim(
            VertexHelper helper,
            Vector2[] polygon,
            float width,
            Color rim)
        {
            if (polygon == null || polygon.Length < 3 || width <= 0f)
                return;

            for (int index = 0; index < polygon.Length - 1; index++)
                AddLineSegment(helper, polygon[index], polygon[index + 1], width, rim);
        }

        private static void AddLineSegment(
            VertexHelper helper,
            Vector2 startPoint,
            Vector2 endPoint,
            float width,
            Color value)
        {
            Vector2 direction = endPoint - startPoint;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x)
                .normalized * (width * 0.5f);
            int start = helper.currentVertCount;
            AddVertex(helper, startPoint - perpendicular, value);
            AddVertex(helper, startPoint + perpendicular, value);
            AddVertex(helper, endPoint + perpendicular, value);
            AddVertex(helper, endPoint - perpendicular, value);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddVertex(VertexHelper helper, Vector2 position, Color value)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = value;
            helper.AddVert(vertex);
        }
    }

    /// <summary>
    /// Nó octogonal de fase. O desenho procedural mantém a mesma proporção em
    /// PC e Android e fornece uma superfície real de raycast ao Button pai.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelPhaseNodeGraphic : MaskableGraphic
    {
        private Color accent = new(0.15f, 0.72f, 1f, 1f);
        private bool legal;
        private bool active;

        public void SetState(Color newAccent, bool isLegal, bool isActive)
        {
            accent = newAccent;
            legal = isLegal;
            active = isActive;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float cut = Mathf.Min(rect.width, rect.height) * 0.22f;
            Vector2[] outer = Octagon(rect, cut);
            Rect innerRect = new(
                rect.xMin + 2.2f,
                rect.yMin + 2.2f,
                Mathf.Max(0f, rect.width - 4.4f),
                Mathf.Max(0f, rect.height - 4.4f));
            Vector2[] inner = Octagon(innerRect, Mathf.Max(1f, cut - 1.2f));

            Color deep = new(0.003f, 0.012f, 0.026f, legal ? 0.96f : 0.78f);
            Color center = Color.Lerp(
                deep,
                new Color(accent.r * 0.28f, accent.g * 0.28f, accent.b * 0.32f, 1f),
                active ? 0.52f : legal ? 0.34f : 0.12f);
            int centerIndex = helper.currentVertCount;
            AddVertex(helper, rect.center, center);
            for (int index = 0; index < inner.Length; index++)
            {
                float upper = Mathf.InverseLerp(rect.yMin, rect.yMax, inner[index].y);
                Color edgeFill = Color.Lerp(deep, center, 0.58f + upper * 0.18f);
                AddVertex(helper, inner[index], edgeFill);
            }
            for (int index = 0; index < inner.Length; index++)
            {
                int next = (index + 1) % inner.Length;
                helper.AddTriangle(centerIndex, centerIndex + 1 + index, centerIndex + 1 + next);
            }

            int ringStart = helper.currentVertCount;
            for (int index = 0; index < outer.Length; index++)
            {
                Color outerColor = new(
                    accent.r,
                    accent.g,
                    accent.b,
                    legal ? 0.98f : active ? 0.78f : 0.34f);
                Color innerColor = new(
                    accent.r,
                    accent.g,
                    accent.b,
                    legal ? 0.30f : 0.10f);
                AddVertex(helper, outer[index], outerColor);
                AddVertex(helper, inner[index], innerColor);
            }
            for (int index = 0; index < outer.Length; index++)
            {
                int next = (index + 1) % outer.Length;
                int a = ringStart + index * 2;
                int b = ringStart + next * 2;
                helper.AddTriangle(a, b, b + 1);
                helper.AddTriangle(a, b + 1, a + 1);
            }

            float railHalf = Mathf.Max(1.2f, rect.height * 0.018f);
            float railWidth = rect.width * (legal ? 0.58f : 0.34f);
            Color railColor = new(accent.r, accent.g, accent.b, legal ? 0.92f : 0.28f);
            AddQuad(
                helper,
                rect.center.x - railWidth * 0.5f,
                rect.center.x + railWidth * 0.5f,
                rect.yMin + rect.height * 0.10f - railHalf,
                rect.yMin + rect.height * 0.10f + railHalf,
                railColor);
        }

        private static Vector2[] Octagon(Rect rect, float cut)
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

        private static void AddQuad(
            VertexHelper helper,
            float xMin,
            float xMax,
            float yMin,
            float yMax,
            Color value)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, new Vector2(xMin, yMin), value);
            AddVertex(helper, new Vector2(xMax, yMin), value);
            AddVertex(helper, new Vector2(xMax, yMax), value);
            AddVertex(helper, new Vector2(xMin, yMax), value);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddVertex(VertexHelper helper, Vector2 position, Color value)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = value;
            helper.AddVert(vertex);
        }
    }

    /// <summary>
    /// Placa lateral do controle de turno. Evita o botão retangular legado e
    /// conserva uma área de toque ampla e previsível em qualquer resolução.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelPhaseControlGraphic : MaskableGraphic
    {
        private Color accent = new(0.12f, 0.62f, 1f, 1f);
        private bool enabledState = true;

        public void SetStyle(Color newAccent, bool isEnabled)
        {
            accent = newAccent;
            enabledState = isEnabled;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float cut = Mathf.Min(rect.width, rect.height) * 0.14f;
            Vector2[] points = new[]
            {
                new Vector2(rect.xMin + cut, rect.yMax),
                new Vector2(rect.xMax - cut * 0.55f, rect.yMax),
                new Vector2(rect.xMax, rect.yMax - cut * 0.55f),
                new Vector2(rect.xMax, rect.yMin + cut),
                new Vector2(rect.xMax - cut, rect.yMin),
                new Vector2(rect.xMin + cut * 0.55f, rect.yMin),
                new Vector2(rect.xMin, rect.yMin + cut * 0.55f),
                new Vector2(rect.xMin, rect.yMax - cut)
            };
            Color deep = new(0.002f, 0.010f, 0.024f, enabledState ? 0.92f : 0.64f);
            Color tinted = new(
                accent.r * 0.16f,
                accent.g * 0.16f,
                accent.b * 0.20f,
                enabledState ? 0.94f : 0.62f);
            int center = helper.currentVertCount;
            AddVertex(helper, rect.center, tinted);
            for (int index = 0; index < points.Length; index++)
            {
                float side = Mathf.InverseLerp(rect.xMin, rect.xMax, points[index].x);
                AddVertex(helper, points[index], Color.Lerp(tinted, deep, 0.38f + side * 0.34f));
            }
            for (int index = 0; index < points.Length; index++)
            {
                int next = (index + 1) % points.Length;
                helper.AddTriangle(center, center + 1 + index, center + 1 + next);
            }

            float thickness = Mathf.Clamp(rect.height * 0.025f, 1.2f, 3f);
            Rect innerRect = new(
                rect.xMin + thickness,
                rect.yMin + thickness,
                rect.width - thickness * 2f,
                rect.height - thickness * 2f);
            Vector2[] inner = new[]
            {
                new Vector2(innerRect.xMin + cut, innerRect.yMax),
                new Vector2(innerRect.xMax - cut * 0.55f, innerRect.yMax),
                new Vector2(innerRect.xMax, innerRect.yMax - cut * 0.55f),
                new Vector2(innerRect.xMax, innerRect.yMin + cut),
                new Vector2(innerRect.xMax - cut, innerRect.yMin),
                new Vector2(innerRect.xMin + cut * 0.55f, innerRect.yMin),
                new Vector2(innerRect.xMin, innerRect.yMin + cut * 0.55f),
                new Vector2(innerRect.xMin, innerRect.yMax - cut)
            };
            int ringStart = helper.currentVertCount;
            for (int index = 0; index < points.Length; index++)
            {
                Color outside = new(
                    accent.r,
                    accent.g,
                    accent.b,
                    enabledState ? 0.90f : 0.26f);
                AddVertex(helper, points[index], outside);
                AddVertex(helper, inner[index], new Color(outside.r, outside.g, outside.b, 0.08f));
            }
            for (int index = 0; index < points.Length; index++)
            {
                int next = (index + 1) % points.Length;
                int a = ringStart + index * 2;
                int b = ringStart + next * 2;
                helper.AddTriangle(a, b, b + 1);
                helper.AddTriangle(a, b + 1, a + 1);
            }
        }

        private static void AddVertex(VertexHelper helper, Vector2 position, Color value)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = value;
            helper.AddVert(vertex);
        }
    }
}
