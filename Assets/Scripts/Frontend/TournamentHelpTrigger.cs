using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Exibe ajuda após três segundos de hover, foco ou toque prolongado.
    /// Um toque prolongado consome o clique para não executar a ação enquanto
    /// o jogador ainda está lendo a explicação.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class TournamentHelpTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        internal const float DefaultDelaySeconds = 3f;

        private string helpTitle;
        private string helpBody;
        private Action<RectTransform, string, string> show;
        private Action hide;
        private RectTransform owner;
        private Button button;
        private bool waiting;
        private bool visible;
        private bool pointerDown;
        private float startedAt;

        internal void Configure(
            string title,
            string body,
            Action<RectTransform, string, string> showAction,
            Action hideAction)
        {
            helpTitle = title?.Trim() ?? string.Empty;
            helpBody = body?.Trim() ?? string.Empty;
            show = showAction;
            hide = hideAction;
            owner = transform as RectTransform;
            button = GetComponent<Button>();
        }

        private void Update()
        {
            if (!waiting || visible ||
                Time.unscaledTime - startedAt < DefaultDelaySeconds)
            {
                return;
            }

            visible = true;
            waiting = false;
            show?.Invoke(owner, helpTitle, helpBody);
            if (pointerDown && button != null && button.interactable)
                button.interactable = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            BeginWaiting();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelAndHide();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDown = true;
            BeginWaiting();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerDown = false;
            bool restoreButton = button != null && !button.interactable;
            CancelAndHide();
            if (restoreButton && isActiveAndEnabled)
                StartCoroutine(RestoreButtonNextFrame());
        }

        public void OnSelect(BaseEventData eventData)
        {
            BeginWaiting();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            CancelAndHide();
        }

        private void OnDisable()
        {
            pointerDown = false;
            if (button != null && !button.interactable)
                button.interactable = true;
            CancelAndHide();
        }

        private void BeginWaiting()
        {
            if (visible || string.IsNullOrWhiteSpace(helpBody))
                return;
            waiting = true;
            startedAt = Time.unscaledTime;
        }

        private void CancelAndHide()
        {
            waiting = false;
            if (!visible)
                return;
            visible = false;
            hide?.Invoke();
        }

        private IEnumerator RestoreButtonNextFrame()
        {
            yield return null;
            if (button != null)
                button.interactable = true;
        }
    }
}
