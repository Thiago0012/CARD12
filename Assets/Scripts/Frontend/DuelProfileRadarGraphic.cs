using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    public sealed class DuelProfileRadarGraphic : MaskableGraphic
    {
        private const int AxisCount = 6;
        private readonly float[] _values = new float[AxisCount];

        public IReadOnlyList<float> Values => _values;

        public void SetValues(IReadOnlyList<float> values)
        {
            for (int index = 0; index < AxisCount; index++)
            {
                _values[index] = values != null && index < values.Count
                    ? Mathf.Clamp01(values[index])
                    : 0f;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect bounds = rectTransform.rect;
            float radius = Mathf.Max(
                0f,
                Mathf.Min(bounds.width, bounds.height) * 0.5f - 7f);
            if (radius <= 0f)
                return;

            Vector2 center = bounds.center;
            Color grid = new(0.25f, 0.72f, 0.82f, 0.30f);
            for (int ring = 1; ring <= 4; ring++)
            {
                float ringRadius = radius * ring / 4f;
                for (int axis = 0; axis < AxisCount; axis++)
                {
                    AddLine(
                        vertexHelper,
                        Point(center, ringRadius, axis),
                        Point(center, ringRadius, axis + 1),
                        1.4f,
                        grid);
                }
            }

            for (int axis = 0; axis < AxisCount; axis++)
            {
                AddLine(
                    vertexHelper,
                    center,
                    Point(center, radius, axis),
                    1.2f,
                    grid);
            }

            int centerIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center, new Color(0.05f, 0.72f, 0.96f, 0.34f));
            for (int axis = 0; axis < AxisCount; axis++)
            {
                AddVertex(
                    vertexHelper,
                    Point(center, radius * _values[axis], axis),
                    new Color(0.05f, 0.72f, 0.96f, 0.48f));
            }
            for (int axis = 0; axis < AxisCount; axis++)
            {
                vertexHelper.AddTriangle(
                    centerIndex,
                    centerIndex + 1 + axis,
                    centerIndex + 1 + (axis + 1) % AxisCount);
            }

            Color outline = new(0.20f, 0.92f, 1f, 0.96f);
            for (int axis = 0; axis < AxisCount; axis++)
            {
                AddLine(
                    vertexHelper,
                    Point(center, radius * _values[axis], axis),
                    Point(center, radius * _values[(axis + 1) % AxisCount],
                        axis + 1),
                    2.6f,
                    outline);
            }
        }

        private static Vector2 Point(
            Vector2 center,
            float radius,
            int axis)
        {
            float angle = Mathf.PI * 0.5f -
                          axis * Mathf.PI * 2f / AxisCount;
            return center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)) * radius;
        }

        private static void AddLine(
            VertexHelper helper,
            Vector2 from,
            Vector2 to,
            float width,
            Color color)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude <= 0.0001f)
                return;
            Vector2 normal = new Vector2(-direction.y, direction.x)
                .normalized * width * 0.5f;
            int start = helper.currentVertCount;
            AddVertex(helper, from - normal, color);
            AddVertex(helper, from + normal, color);
            AddVertex(helper, to + normal, color);
            AddVertex(helper, to - normal, color);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
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
