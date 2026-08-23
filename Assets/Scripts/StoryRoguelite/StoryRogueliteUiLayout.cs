using System.Collections.Generic;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    /// <summary>
    /// Medidas de referência e regras de posicionamento da interface das
    /// Crônicas do Duelo. Mantém o mapa legível sem permitir que o retrato do
    /// jogador cubra um objetivo interativo.
    /// </summary>
    public static class StoryRogueliteUiLayout
    {
        public static readonly Vector2 MapBaseSize =
            new(1232f, 1657.6f);
        public const float InitialMapZoom = 1f;
        public const float MinimumMapZoom = 0.72f;
        public static readonly Vector2 MarkerHalfSize =
            new(0.031f, 0.031f);
        // Ordem: Left, Top, Right e Bottom como exibidos no Inspector.
        public static readonly Vector4 MarkerRectOffsets =
            new(-30.22862f, 23.55465f, -30.22858f, -21.64555f);

        public static float MarkerLeftExtent => MarkerHalfSize.x -
            MarkerRectOffsets.x /
            (MapBaseSize.x * MinimumMapZoom);
        public static float MarkerRightExtent => MarkerHalfSize.x -
            MarkerRectOffsets.z /
            (MapBaseSize.x * MinimumMapZoom);
        public static float MarkerTopExtent => Mathf.Max(0f,
            MarkerHalfSize.y - MarkerRectOffsets.y /
            (MapBaseSize.y * MinimumMapZoom));
        public static float MarkerBottomExtent => MarkerHalfSize.y -
            MarkerRectOffsets.w /
            (MapBaseSize.y * MinimumMapZoom);
        public static Vector2 MarkerCollisionHalfSize => new(
            Mathf.Max(MarkerLeftExtent, MarkerRightExtent),
            Mathf.Max(MarkerTopExtent, MarkerBottomExtent));

        public static readonly Vector2 MapPanelMin =
            new(0.035f, 0.075f);
        public static readonly Vector2 MapPanelMax =
            new(0.725f, 0.875f);
        public static readonly Vector2 MapViewportMin =
            new(0.018f, 0.025f);
        public static readonly Vector2 MapViewportMax =
            new(0.982f, 0.975f);

        public static readonly Vector2 RewardTileY =
            new(0.20f, 0.75f);
        public static readonly Vector2 RewardCardMin =
            new(0.15f, 0.25f);
        public static readonly Vector2 RewardCardMax =
            new(0.85f, 0.91f);

        public static readonly Vector2 EncounterPortraitMin =
            new(0.325f, 0.23f);
        public static readonly Vector2 EncounterPortraitMax =
            new(0.675f, 0.88f);
        // Ordem: Left, Top, Right e Bottom como exibidos no Inspector.
        public static readonly Vector4 EncounterVeilOffsets =
            new(-109.074f, 0f, -109.074f, 0f);
        public static readonly Vector4 EncounterPortraitOffsets =
            new(-184.6499f, -86.96864f, -28.64995f, -312.9686f);
        public static readonly Vector2 EncounterDialogueMin =
            new(0.12f, 0.055f);
        public static readonly Vector2 EncounterDialogueMax =
            new(0.88f, 0.34f);
        public static readonly Vector2 EncounterDuelButtonMin =
            new(0.72f, 0.10f);
        public static readonly Vector2 EncounterDuelButtonMax =
            new(0.96f, 0.40f);

        private const float MarkerCollisionGap = 0.008f;
        private const float MapEdgeGap = 0.010f;

        public static Vector2 NodeHalfSize(RogueliteNodeType type) =>
            type == RogueliteNodeType.Boss
                ? new Vector2(0.080f, 0.028f)
                : new Vector2(0.060f, 0.024f);

        public static Vector2 ResolveMarkerPosition(
            StoryMapRecord map,
            StoryMapNodeRecord current)
        {
            if (current == null)
                return new Vector2(0.5f, 0.5f);

            Vector2 primary = PrimaryDirection(current.MarkerOffset);
            Vector2 perpendicular = new(-primary.y, primary.x);
            var directions = new List<Vector2>
            {
                primary,
                perpendicular,
                -perpendicular,
                -primary,
                (primary + perpendicular).normalized,
                (primary - perpendicular).normalized,
                (-primary + perpendicular).normalized,
                (-primary - perpendicular).normalized
            };

            Vector2 currentHalf = NodeHalfSize(current.NodeType);
            Vector2 markerCollisionHalf = MarkerCollisionHalfSize;
            foreach (Vector2 direction in directions)
            {
                Vector2 distance = new(
                    currentHalf.x + markerCollisionHalf.x +
                    MarkerCollisionGap,
                    currentHalf.y + markerCollisionHalf.y +
                    MarkerCollisionGap);
                Vector2 candidate = current.NormalizedPosition +
                    DirectionalOffset(direction, distance);
                if (IsInsideMap(candidate) &&
                    !OverlapsAnyObjective(map, candidate))
                {
                    return candidate;
                }
            }

            // Mapas procedurais podem formar corredores especialmente densos.
            // Nesses casos, procura a posição livre mais próxima em uma grade
            // estável. A leve preferência pela direção autoral mantém o
            // marcador visualmente ligado ao nó sem sacrificar os cliques.
            Vector2 best = new(0.5f, 0.5f);
            float bestScore = float.PositiveInfinity;
            const int gridSteps = 48;
            float minimumX = MarkerLeftExtent + MapEdgeGap;
            float maximumX = 1f - MarkerRightExtent - MapEdgeGap;
            float minimumY = MarkerBottomExtent + MapEdgeGap;
            float maximumY = 1f - MarkerTopExtent - MapEdgeGap;
            for (int y = 0; y <= gridSteps; y++)
            {
                for (int x = 0; x <= gridSteps; x++)
                {
                    Vector2 candidate = new(
                        Mathf.Lerp(minimumX, maximumX, x / (float)gridSteps),
                        Mathf.Lerp(minimumY, maximumY, y / (float)gridSteps));
                    if (OverlapsAnyObjective(map, candidate))
                        continue;
                    Vector2 difference = candidate -
                                         current.NormalizedPosition;
                    float preference = difference.sqrMagnitude > 0f
                        ? Vector2.Dot(difference.normalized, primary)
                        : 0f;
                    float score = difference.sqrMagnitude -
                                  preference * 0.0025f;
                    if (score >= bestScore)
                        continue;
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        public static bool OverlapsAnyObjective(
            StoryMapRecord map,
            Vector2 markerPosition)
        {
            if (map?.nodes == null)
                return false;
            float markerMinimumX = markerPosition.x - MarkerLeftExtent;
            float markerMaximumX = markerPosition.x + MarkerRightExtent;
            float markerMinimumY = markerPosition.y - MarkerBottomExtent;
            float markerMaximumY = markerPosition.y + MarkerTopExtent;
            foreach (StoryMapNodeRecord node in map.nodes)
            {
                if (node == null)
                    continue;
                Vector2 half = NodeHalfSize(node.NodeType);
                bool overlapsX = markerMinimumX <
                        node.NormalizedPosition.x + half.x +
                        MarkerCollisionGap &&
                    markerMaximumX > node.NormalizedPosition.x - half.x -
                        MarkerCollisionGap;
                bool overlapsY = markerMinimumY <
                        node.NormalizedPosition.y + half.y +
                        MarkerCollisionGap &&
                    markerMaximumY > node.NormalizedPosition.y - half.y -
                        MarkerCollisionGap;
                if (overlapsX && overlapsY)
                    return true;
            }
            return false;
        }

        private static Vector2 PrimaryDirection(Vector2 authoredOffset)
        {
            if (authoredOffset.sqrMagnitude < 0.000001f)
                return Vector2.up;
            if (Mathf.Abs(authoredOffset.x) > Mathf.Abs(authoredOffset.y))
                return authoredOffset.x >= 0f ? Vector2.right : Vector2.left;
            return authoredOffset.y >= 0f ? Vector2.up : Vector2.down;
        }

        private static Vector2 DirectionalOffset(
            Vector2 direction,
            Vector2 clearance)
        {
            const float epsilon = 0.001f;
            float scale = float.PositiveInfinity;
            if (Mathf.Abs(direction.x) > epsilon)
            {
                scale = Mathf.Min(
                    scale,
                    clearance.x / Mathf.Abs(direction.x));
            }
            if (Mathf.Abs(direction.y) > epsilon)
            {
                scale = Mathf.Min(
                    scale,
                    clearance.y / Mathf.Abs(direction.y));
            }
            if (float.IsPositiveInfinity(scale))
                return Vector2.up * clearance.y;
            return direction * (scale + epsilon);
        }

        private static bool IsInsideMap(Vector2 position) =>
            position.x >= MarkerLeftExtent + MapEdgeGap &&
            position.x <= 1f - MarkerRightExtent - MapEdgeGap &&
            position.y >= MarkerBottomExtent + MapEdgeGap &&
            position.y <= 1f - MarkerTopExtent - MapEdgeGap;
    }
}
