using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Scene-authored presentation settings for a duel hand. This component
    /// never identifies cards or validates gameplay; it only controls how the
    /// confirmed hand state is rendered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelHandLayoutAnchor : MonoBehaviour
    {
        public enum HandOwner
        {
            LocalPlayer,
            Opponent
        }

        [SerializeField] private HandOwner owner = HandOwner.LocalPlayer;
        [SerializeField] private Vector2 cardSize = new(178f, 258f);
        [SerializeField, Min(0f)] private float maximumSpacing = 96f;
        [SerializeField, Min(0f)] private float maximumSpan = 720f;
        [SerializeField, Range(0f, 30f)] private float maximumFanAngle = 6f;
        [SerializeField, Min(0f)] private float focusGapMinimum = 70f;
        [SerializeField, Min(0f)] private float focusGapMaximum = 132f;
        [SerializeField, Min(0f)] private float focusGapBase = 430f;
        [SerializeField] private Vector2 placementModeOffset =
            new(0f, -136f);
        [SerializeField, Range(0.1f, 1f)] private float placementModeScale =
            0.70f;
        [SerializeField, Min(0f)] private float selectedLift = 44f;
        [SerializeField, Min(0f)] private float hoverLift = 56f;
        [SerializeField, Range(1f, 1.5f)] private float selectedScale = 1.04f;
        [SerializeField, Range(1f, 1.5f)] private float hoverScale = 1.07f;
        [SerializeField, Range(0.01f, 0.5f)] private float animationDuration =
            0.14f;

        public HandOwner Owner => owner;
        public Vector2 CardSize => cardSize;
        public Vector2 PlacementModeOffset => placementModeOffset;
        public float PlacementModeScale => placementModeScale;
        public float SelectedLift => selectedLift;
        public float HoverLift => hoverLift;
        public float SelectedScale => selectedScale;
        public float HoverScale => hoverScale;
        public float AnimationDuration => animationDuration;

        public void ConfigureOwner(HandOwner value)
        {
            owner = value;
            if (owner == HandOwner.Opponent && cardSize.y > 100f)
            {
                cardSize = new Vector2(42f, 61f);
                maximumSpacing = 29f;
                maximumSpan = 261f;
                maximumFanAngle = 14.4f;
                focusGapMinimum = 0f;
                focusGapMaximum = 0f;
                focusGapBase = 0f;
            }
        }

        public Vector2 PositionFor(int index, int count)
        {
            if (count <= 1)
                return Vector2.zero;
            float center = (count - 1) * 0.5f;
            float spacing = Mathf.Min(
                maximumSpacing,
                maximumSpan / Mathf.Max(1f, count - 1f));
            float vertical = owner == HandOwner.Opponent
                ? 4f - Mathf.Abs(index - center) * 2f
                : 0f;
            return new Vector2((index - center) * spacing, vertical);
        }

        public float AngleFor(int index, int count)
        {
            if (count <= 1)
                return 0f;
            float center = (count - 1) * 0.5f;
            float normalized =
                (index - center) / Mathf.Max(1f, center);
            return normalized * -maximumFanAngle;
        }

        public float FocusSeparationFor(int count)
        {
            if (count <= 1 || focusGapMaximum <= 0f)
                return 0f;
            float span = Mathf.Min(
                maximumSpacing * (count - 1f),
                maximumSpan);
            return Mathf.Clamp(
                focusGapBase - span * 0.5f,
                focusGapMinimum,
                focusGapMaximum);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null)
                return;
            Gizmos.color = owner == HandOwner.LocalPlayer
                ? new Color(0.10f, 0.95f, 1f, 0.85f)
                : new Color(1f, 0.72f, 0.18f, 0.85f);
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            for (int index = 0; index < corners.Length; index++)
                Gizmos.DrawLine(corners[index], corners[(index + 1) % 4]);
        }
#endif
    }
}
