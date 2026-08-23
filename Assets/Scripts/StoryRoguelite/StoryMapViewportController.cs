using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcaneArena.StoryRoguelite
{
    /// <summary>
    /// Controle leve de zoom para o mapa procedural. O arrasto é delegado ao
    /// ScrollRect e somente um gesto de pinça ou roda altera o tamanho do
    /// conteúdo, sem instanciar câmeras ou texturas adicionais.
    /// </summary>
    public sealed class StoryMapViewportController : MonoBehaviour,
        IScrollHandler
    {
        private ScrollRect scrollRect;
        private RectTransform content;
        private Vector2 baseSize;
        private float zoom = StoryRogueliteUiLayout.InitialMapZoom;
        private float minimumZoom = StoryRogueliteUiLayout.MinimumMapZoom;
        private float maximumZoom = 1.65f;
        private float previousPinchDistance;
#if ENABLE_INPUT_SYSTEM
        private bool enabledEnhancedTouch;
#endif

        public float Zoom => zoom;

        public void Configure(
            ScrollRect targetScrollRect,
            RectTransform targetContent,
            Vector2 unscaledSize,
            float initialZoom = StoryRogueliteUiLayout.InitialMapZoom)
        {
            scrollRect = targetScrollRect;
            content = targetContent;
            baseSize = unscaledSize;
            SetZoom(initialZoom);
        }

        public void ZoomIn() => SetZoom(zoom + 0.12f);
        public void ZoomOut() => SetZoom(zoom - 0.12f);
        public void ResetZoom() => SetZoom(
            StoryRogueliteUiLayout.InitialMapZoom);

        public void Focus(Vector2 normalizedPosition)
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                normalizedPosition.x);
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                normalizedPosition.y);
        }

        public void SetZoom(float value)
        {
            if (content == null) return;
            Vector2 normalized = scrollRect != null
                ? scrollRect.normalizedPosition
                : new Vector2(0.5f, 0f);
            zoom = Mathf.Clamp(value, minimumZoom, maximumZoom);
            content.sizeDelta = baseSize * zoom;
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.normalizedPosition = normalized;
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Mathf.Abs(eventData.scrollDelta.y) < 0.01f) return;
            SetZoom(zoom + Mathf.Sign(eventData.scrollDelta.y) * 0.08f);
            eventData.Use();
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport
                    .enabled)
            {
                UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport
                    .Enable();
                enabledEnhancedTouch = true;
            }
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (enabledEnhancedTouch)
            {
                UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport
                    .Disable();
                enabledEnhancedTouch = false;
            }
#endif
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch
                .activeTouches;
            if (touches.Count != 2)
            {
                previousPinchDistance = 0f;
                return;
            }
            float distance = Vector2.Distance(
                touches[0].screenPosition,
                touches[1].screenPosition);
#else
            if (Input.touchCount != 2)
            {
                previousPinchDistance = 0f;
                return;
            }
            float distance = Vector2.Distance(
                Input.GetTouch(0).position,
                Input.GetTouch(1).position);
#endif
            if (previousPinchDistance > 0f)
            {
                float delta = (distance - previousPinchDistance) /
                              Mathf.Max(240f, Screen.dpi * 1.5f);
                if (Mathf.Abs(delta) > 0.002f)
                    SetZoom(zoom + delta);
            }
            previousPinchDistance = distance;
        }
    }
}
