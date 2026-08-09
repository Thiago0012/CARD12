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
#if UNITY_EDITOR
        private bool validationRefreshScheduled;
#endif

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
#if UNITY_EDITOR
            if (validationRefreshScheduled)
                return;
            validationRefreshScheduled = true;
            UnityEditor.EditorApplication.delayCall +=
                RefreshAfterValidation;
#else
            RefreshVisibility();
#endif
        }

#if UNITY_EDITOR
        private void RefreshAfterValidation()
        {
            validationRefreshScheduled = false;
            if (this != null)
                RefreshVisibility();
        }

        private void OnDisable()
        {
            UnityEditor.EditorApplication.delayCall -=
                RefreshAfterValidation;
            validationRefreshScheduled = false;
        }
#endif

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
