using UnityEngine;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Calculates a banlist badge rectangle against the pixels actually drawn
    /// by a preserve-aspect card image, rather than against its possibly wider
    /// UI container.
    /// </summary>
    public static class BanlistBadgeGeometry
    {
        public const float LeftInsetFraction = 0.015f;
        public const float TopInsetFraction = 0.015f;
        public const float WidthFraction = 0.255f;

        public static bool TryCalculateAnchors(
            Vector2 containerSize,
            Vector2 spriteSize,
            bool preserveAspect,
            out Vector2 anchorMin,
            out Vector2 anchorMax)
        {
            anchorMin = Vector2.zero;
            anchorMax = Vector2.zero;

            float containerWidth = Mathf.Abs(containerSize.x);
            float containerHeight = Mathf.Abs(containerSize.y);
            if (containerWidth <= Mathf.Epsilon ||
                containerHeight <= Mathf.Epsilon)
            {
                return false;
            }

            Vector2 visibleMin = Vector2.zero;
            Vector2 visibleMax = Vector2.one;
            float spriteWidth = Mathf.Abs(spriteSize.x);
            float spriteHeight = Mathf.Abs(spriteSize.y);
            if (preserveAspect &&
                spriteWidth > Mathf.Epsilon &&
                spriteHeight > Mathf.Epsilon)
            {
                float containerAspect = containerWidth / containerHeight;
                float spriteAspect = spriteWidth / spriteHeight;
                if (spriteAspect > containerAspect)
                {
                    float visibleHeight = containerAspect / spriteAspect;
                    float inset = (1f - visibleHeight) * 0.5f;
                    visibleMin.y = inset;
                    visibleMax.y = 1f - inset;
                }
                else if (spriteAspect < containerAspect)
                {
                    float visibleWidth = spriteAspect / containerAspect;
                    float inset = (1f - visibleWidth) * 0.5f;
                    visibleMin.x = inset;
                    visibleMax.x = 1f - inset;
                }
            }

            float visibleWidthFraction = visibleMax.x - visibleMin.x;
            float visibleHeightFraction = visibleMax.y - visibleMin.y;
            float left = visibleMin.x +
                         LeftInsetFraction * visibleWidthFraction;
            float right = left + WidthFraction * visibleWidthFraction;
            float top = visibleMax.y -
                        TopInsetFraction * visibleHeightFraction;

            // Use the same physical size on both axes. This avoids an
            // additional preserve-aspect inset inside the badge itself.
            float badgeWidthPixels =
                WidthFraction * visibleWidthFraction * containerWidth;
            float badgeHeightFraction = badgeWidthPixels / containerHeight;
            float bottom = top - badgeHeightFraction;

            anchorMin = new Vector2(
                Mathf.Clamp(left, visibleMin.x, visibleMax.x),
                Mathf.Clamp(bottom, visibleMin.y, visibleMax.y));
            anchorMax = new Vector2(
                Mathf.Clamp(right, visibleMin.x, visibleMax.x),
                Mathf.Clamp(top, visibleMin.y, visibleMax.y));
            return anchorMax.x > anchorMin.x && anchorMax.y > anchorMin.y;
        }
    }
}
