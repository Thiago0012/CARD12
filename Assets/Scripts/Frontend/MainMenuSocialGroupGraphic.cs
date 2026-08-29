using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    public sealed class MainMenuSocialGroupGraphic : MaskableGraphic
    {
        private static readonly Color Interior =
            new(0.012f, 0.047f, 0.078f, 0.98f);
        private static readonly Color Cyan =
            new(0.30f, 0.91f, 1f, 1f);
        private static readonly Color SoftCyan =
            new(0.63f, 0.96f, 1f, 0.92f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            AddPolygon(vh, rect, Interior, new[]
            {
                new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.08f),
                new Vector2(0.94f, 0.23f), new Vector2(0.94f, 0.77f),
                new Vector2(0.80f, 0.92f), new Vector2(0.20f, 0.92f),
                new Vector2(0.06f, 0.77f), new Vector2(0.06f, 0.23f)
            });

            AddPerson(vh, rect, new Vector2(0.31f, 0.51f), 0.105f,
                0.22f, 0.24f, SoftCyan);
            AddPerson(vh, rect, new Vector2(0.69f, 0.51f), 0.105f,
                0.22f, 0.24f, SoftCyan);
            AddPerson(vh, rect, new Vector2(0.50f, 0.61f), 0.13f,
                0.29f, 0.31f, Cyan);
        }

        private static void AddPerson(
            VertexHelper vh,
            Rect rect,
            Vector2 headCenter,
            float headRadius,
            float bodyWidth,
            float bodyHeight,
            Color color)
        {
            AddCircle(vh, rect, headCenter, headRadius, color, 18);
            float top = headCenter.y - headRadius * 1.38f;
            AddPolygon(vh, rect, color, new[]
            {
                new Vector2(headCenter.x - bodyWidth * 0.34f, top),
                new Vector2(headCenter.x + bodyWidth * 0.34f, top),
                new Vector2(headCenter.x + bodyWidth * 0.56f,
                    top - bodyHeight),
                new Vector2(headCenter.x - bodyWidth * 0.56f,
                    top - bodyHeight)
            });
        }

        private static void AddCircle(
            VertexHelper vh,
            Rect rect,
            Vector2 center,
            float radius,
            Color color,
            int segments)
        {
            var points = new List<Vector2>(segments);
            float aspect = rect.height <= 0f ? 1f : rect.height / rect.width;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                points.Add(new Vector2(
                    center.x + Mathf.Cos(angle) * radius * aspect,
                    center.y + Mathf.Sin(angle) * radius));
            }
            AddPolygon(vh, rect, color, points);
        }

        private static void AddPolygon(
            VertexHelper vh,
            Rect rect,
            Color color,
            IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3)
                return;
            int start = vh.currentVertCount;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 point = points[index];
                vh.AddVert(
                    new Vector3(
                        Mathf.Lerp(rect.xMin, rect.xMax, point.x),
                        Mathf.Lerp(rect.yMin, rect.yMax, point.y)),
                    color,
                    Vector2.zero);
            }
            for (int index = 1; index < points.Count - 1; index++)
                vh.AddTriangle(start, start + index, start + index + 1);
        }
    }
}
