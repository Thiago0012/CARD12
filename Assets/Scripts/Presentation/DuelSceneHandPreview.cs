using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Keeps the representative Scene-view card size synchronized with the
    /// runtime hand renderer. This is presentation-only authoring data.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DuelSceneHandPreview : MonoBehaviour
    {
        [SerializeField] private DuelHandLayoutAnchor targetLayout;
        [SerializeField] private RectTransform representativeCard;
        private Vector2 lastSize;

        public void Configure(
            DuelHandLayoutAnchor layout,
            RectTransform card)
        {
            targetLayout = layout;
            representativeCard = card;
            lastSize = card != null ? card.rect.size : Vector2.zero;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || targetLayout == null ||
                representativeCard == null)
            {
                return;
            }

            Vector2 size = representativeCard.rect.size;
            if ((size - lastSize).sqrMagnitude < 0.01f &&
                (size - targetLayout.CardSize).sqrMagnitude < 0.01f)
            {
                return;
            }

            lastSize = size;
            targetLayout.ConfigureCardSize(size);
            UnityEditor.EditorUtility.SetDirty(targetLayout);
            UnityEditor.SceneManagement.EditorSceneManager
                .MarkSceneDirty(gameObject.scene);
#endif
        }
    }
}
