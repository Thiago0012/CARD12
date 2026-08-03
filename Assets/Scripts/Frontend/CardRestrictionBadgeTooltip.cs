using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed class CardRestrictionBadgeTooltip : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private int maximum;
        private GameObject tooltip;

        public void Initialize(int maximumCopies)
        {
            maximum = Mathf.Clamp(maximumCopies, 0, 2);
            gameObject.name = maximum == 0
                ? "Banlist: Proibida"
                : maximum == 1
                    ? "Banlist: Limitada a 1"
                    : "Banlist: Semi-limitada a 2";
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hide();
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            tooltip = new GameObject(
                "Tooltip da Banlist",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            tooltip.transform.SetParent(canvas.transform, false);
            var rect = tooltip.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(270f, 54f);
            rect.position = transform.position + new Vector3(145f, -20f, 0f);
            var panel = tooltip.GetComponent<Image>();
            panel.color = new Color(0.01f, 0.025f, 0.05f, 0.98f);
            panel.raycastTarget = false;

            var labelObject = new GameObject(
                "Texto",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            labelObject.transform.SetParent(tooltip.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = maximum == 0
                ? "PROIBIDA - maximo 0 copias no deck"
                : maximum == 1
                    ? "LIMITADA - maximo 1 copia no deck"
                    : "SEMI-LIMITADA - maximo 2 copias no deck";
            tooltip.transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void Hide()
        {
            if (tooltip != null)
                Destroy(tooltip);
            tooltip = null;
        }
    }
}
