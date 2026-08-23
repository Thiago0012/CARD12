using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.StoryRoguelite
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StoryMapEdgeGraphic : MaskableGraphic
    {
        private StoryMapRecord map;
        private HashSet<string> accessible = new();

        public void Configure(
            StoryMapRecord source,
            IEnumerable<StoryRuntimeNode> runtimeNodes)
        {
            map = source;
            accessible = new HashSet<string>(
                (runtimeNodes ?? Enumerable.Empty<StoryRuntimeNode>())
                    .Where(node => node.state != RogueliteNodeState.Locked)
                    .Select(node => node.nodeId));
            color = Color.white;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (map?.edges == null || map.nodes == null) return;
            foreach (StoryMapEdgeRecord edge in map.edges)
            {
                StoryMapNodeRecord from = map.Node(edge.fromNodeId);
                StoryMapNodeRecord to = map.Node(edge.toNodeId);
                if (from == null || to == null) continue;
                Color tint = accessible.Contains(edge.fromNodeId) &&
                             accessible.Contains(edge.toNodeId)
                    ? new Color(0.18f, 0.95f, 0.82f, 0.78f)
                    : new Color(0.32f, 0.42f, 0.48f, 0.24f);
                AddSegment(helper,
                    ToLocal(from.NormalizedPosition),
                    ToLocal(to.NormalizedPosition),
                    3.5f,
                    tint);
            }
        }

        private Vector2 ToLocal(Vector2 normalized) => new(
            (normalized.x - 0.5f) * rectTransform.rect.width,
            (normalized.y - 0.5f) * rectTransform.rect.height);

        private static void AddSegment(
            VertexHelper helper,
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude < 0.001f) return;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x)
                .normalized * width * 0.5f;
            int index = helper.currentVertCount;
            helper.AddVert(start - perpendicular, color, Vector2.zero);
            helper.AddVert(start + perpendicular, color, Vector2.up);
            helper.AddVert(end + perpendicular, color, Vector2.one);
            helper.AddVert(end - perpendicular, color, Vector2.right);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
