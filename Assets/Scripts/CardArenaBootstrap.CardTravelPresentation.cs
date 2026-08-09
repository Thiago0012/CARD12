using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private IEnumerator AnimateCardTravel(
            Sprite sprite,
            Sprite destinationSprite,
            bool flipToDestination,
            Vector2 start,
            Vector2 destination,
            float duration,
            CanvasGroup target)
        {
            GameObject overlay = CreateTransitionCard(sprite, start);
            RectTransform rect = overlay.GetComponent<RectTransform>();
            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            Image image = overlay.GetComponent<Image>();
            float distance = Vector2.Distance(start, destination);
            float arc = Mathf.Clamp(distance * 0.12f, 28f, 88f);
            float elapsed = 0f;
            bool destinationApplied = false;
            while (elapsed < duration && overlay != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                rect.anchoredPosition = Vector2.Lerp(
                    start,
                    destination,
                    eased) + Vector2.up * Mathf.Sin(t * Mathf.PI) * arc;
                rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(-7f, 0f, eased));
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
                float horizontalScale = 1f;
                if (flipToDestination)
                {
                    const float flipStart = 0.20f;
                    const float flipMiddle = 0.43f;
                    const float flipEnd = 0.66f;
                    if (t < flipMiddle)
                    {
                        horizontalScale = 1f - Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(flipStart, flipMiddle, t));
                    }
                    else
                    {
                        if (!destinationApplied)
                        {
                            image.sprite = destinationSprite ?? sprite;
                            destinationApplied = true;
                        }
                        horizontalScale = Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(flipMiddle, flipEnd, t));
                    }
                }
                rect.localScale = new Vector3(
                    scale * horizontalScale,
                    scale,
                    1f);
                group.alpha = t < 0.88f
                    ? 1f
                    : 1f - Mathf.SmoothStep(0.88f, 1f, t);
                yield return null;
            }
            if (overlay != null && destinationSprite != null)
                image.sprite = destinationSprite;
            RevealTransitionTarget(target);
            if (overlay != null)
                Destroy(overlay);
        }

        private GameObject CreateTransitionCard(
            Sprite sprite,
            Vector2 position)
        {
            GameObject root = CreateTransitionContainer(
                "Carta em Movimento",
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = ResponsiveTransitionCardSize();
            rect.anchoredPosition = position;
            Image image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f);
            outline.effectDistance = new Vector2(3f, -3f);
            return root;
        }

        private GameObject CreateTransitionContainer(
            string objectName,
            params System.Type[] extraComponents)
        {
            var components = new List<System.Type>
            {
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup)
            };
            components.AddRange(extraComponents);
            var root = new GameObject(objectName, components.ToArray());
            root.transform.SetParent(frame, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = false;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            PlaceTransitionBelowInterface(root.transform);
            return root;
        }

        private void PlaceTransitionBelowInterface(Transform transition)
        {
            if (transition == null || frame == null)
                return;

            int interfaceIndex = frame.childCount - 1;
            bool foundInterface = false;
            void Consider(GameObject panel)
            {
                if (panel == null || panel.transform.parent != frame)
                    return;
                interfaceIndex = Mathf.Min(
                    interfaceIndex,
                    panel.transform.GetSiblingIndex());
                foundInterface = true;
            }

            Consider(FindObject(frame, "LP do Player"));
            Consider(FindObject(frame, "LP do Oponente"));
            Consider(FindObject(frame, "Controle de Fases"));
            Consider(detailPanel);
            Consider(actionPanel);
            Consider(fieldActionPanel);
            Consider(choiceModal);
            Consider(compactResponseBar);
            Consider(zoneBrowser);
            Consider(decisionRibbon);
            Consider(recentActionsPanel);
            Consider(chainIndicator);
            Consider(phaseNavigator);
            Consider(battleHud);
            Consider(announcementRoot);
            Consider(opponentHandFan);

            if (foundInterface)
                transition.SetSiblingIndex(interfaceIndex);
            else
                transition.SetAsLastSibling();
        }

        private Vector2 ResponsiveTransitionCardSize()
        {
            float scale = frame == null
                ? 1f
                : Mathf.Clamp(frame.rect.height / 1080f, 0.76f, 1.08f);
            return TransitionCardSize * scale;
        }
    }
}
