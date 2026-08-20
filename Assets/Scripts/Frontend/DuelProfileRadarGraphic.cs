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
            AddUniformPolygon(
                vertexHelper,
                center,
                radius,
                new Color(0.015f, 0.11f, 0.17f, 0.72f),
                new Color(0.01f, 0.055f, 0.09f, 0.48f));
            AddUniformPolygon(
                vertexHelper,
                center,
                radius * 0.5f,
                new Color(0.03f, 0.18f, 0.24f, 0.34f),
                new Color(0.02f, 0.10f, 0.15f, 0.20f));

            Color grid = new(0.25f, 0.78f, 0.88f, 0.34f);
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

            AddValuePolygon(
                vertexHelper,
                center,
                radius,
                _values,
                new Color(0.05f, 0.72f, 0.96f, 0.22f),
                new Color(0.05f, 0.72f, 0.96f, 0.18f),
                1.08f);
            AddValuePolygon(
                vertexHelper,
                center,
                radius,
                _values,
                new Color(0.05f, 0.82f, 1f, 0.60f),
                new Color(0.10f, 0.66f, 1f, 0.50f),
                1f);

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
                AddPoint(
                    vertexHelper,
                    Point(center, radius * _values[axis], axis),
                    5.5f,
                    outline);
            }
        }

        private static void AddUniformPolygon(
            VertexHelper helper,
            Vector2 center,
            float radius,
            Color centerColor,
            Color edgeColor)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, center, centerColor);
            for (int axis = 0; axis < AxisCount; axis++)
                AddVertex(helper, Point(center, radius, axis), edgeColor);
            for (int axis = 0; axis < AxisCount; axis++)
            {
                helper.AddTriangle(
                    start,
                    start + 1 + axis,
                    start + 1 + (axis + 1) % AxisCount);
            }
        }

        private static void AddValuePolygon(
            VertexHelper helper,
            Vector2 center,
            float radius,
            IReadOnlyList<float> values,
            Color centerColor,
            Color edgeColor,
            float scale)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, center, centerColor);
            for (int axis = 0; axis < AxisCount; axis++)
            {
                float value = values != null && axis < values.Count
                    ? Mathf.Clamp01(values[axis])
                    : 0f;
                AddVertex(
                    helper,
                    Point(center, radius * Mathf.Clamp01(value * scale), axis),
                    edgeColor);
            }
            for (int axis = 0; axis < AxisCount; axis++)
            {
                helper.AddTriangle(
                    start,
                    start + 1 + axis,
                    start + 1 + (axis + 1) % AxisCount);
            }
        }

        private static void AddPoint(
            VertexHelper helper,
            Vector2 center,
            float size,
            Color color)
        {
            float half = size * 0.5f;
            int start = helper.currentVertCount;
            AddVertex(helper, center + new Vector2(0f, half), color);
            AddVertex(helper, center + new Vector2(half, 0f), color);
            AddVertex(helper, center + new Vector2(0f, -half), color);
            AddVertex(helper, center + new Vector2(-half, 0f), color);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
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
