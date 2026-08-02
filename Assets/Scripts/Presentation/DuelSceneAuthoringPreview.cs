using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Displays a persistent duel mock-up in the Scene view. The object is
    /// inert in Play Mode and lives under an EditorOnly root, so it does not
    /// add runtime or Android cost.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DuelSceneAuthoringPreview : MonoBehaviour
    {
        private CanvasGroup group;

        private void OnEnable()
        {
            RefreshVisibility();
        }

        private void Start()
        {
            RefreshVisibility();
        }

        private void OnValidate()
        {
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (group == null)
                group = GetComponent<CanvasGroup>();
            if (group == null)
                return;

            group.alpha = Application.isPlaying ? 0f : 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }
}
