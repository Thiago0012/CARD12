using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Cards;
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
            CanvasGroup target,
            MonsterSummonArrivalEffect arrivalEffect,
            MonsterFrameKind summonFrame,
            DuelZone3D destinationZone,
            bool sinksIntoSpecialPile,
            DuelSpecialZoneWellVisual destinationWell)
        {
            Color transitionAccent = destinationWell != null
                ? destinationWell.AccentColor
                : Cyan;
            GameObject overlay = CreateTransitionCard(
                sprite,
                start,
                transitionAccent);
            RectTransform rect = overlay.GetComponent<RectTransform>();
            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            Image image = overlay.GetComponent<Image>();
            RectTransform trail = overlay.transform.Find("Rastro da Carta")
                ?.GetComponent<RectTransform>();
            GameObject destinationPulse = sinksIntoSpecialPile
                ? null
                : CreateTransitionPulse(
                    destinationSprite ?? sprite,
                    destination,
                    transitionAccent);
            destinationPulse?.transform.SetSiblingIndex(
                overlay.transform.GetSiblingIndex());
            destinationWell?.BeginIngress();
            float distance = Vector2.Distance(start, destination);
            float arc = Mathf.Clamp(distance * 0.075f, 18f, 56f);
            float startTilt = Mathf.Clamp(
                (start.x - destination.x) * 0.025f,
                -8f,
                8f);
            float elapsed = 0f;
            bool destinationApplied = false;
            while (elapsed < duration && overlay != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = TransitionTravelEase(t);
                rect.anchoredPosition = Vector2.Lerp(
                    start,
                    destination,
                    eased) + Vector2.up * Mathf.Sin(t * Mathf.PI) * arc;
                rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(startTilt, 0f, TransitionEaseOutCubic(t)));
                float scale;
                if (t < 0.24f)
                {
                    scale = Mathf.Lerp(
                        0.92f,
                        1.06f,
                        TransitionEaseOutCubic(t / 0.24f));
                }
                else
                {
                    scale = Mathf.Lerp(
                        1.06f,
                        1f,
                        Mathf.SmoothStep(0f, 1f, (t - 0.24f) / 0.76f));
                }
                float horizontalScale = 1f;
                if (flipToDestination)
                {
                    const float flipStart = 0.16f;
                    const float flipMiddle = 0.38f;
                    const float flipEnd = 0.60f;
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
                if (sinksIntoSpecialPile)
                {
                    const float ingressStart = 0.70f;
                    Vector2 approach = destination + Vector2.up *
                        Mathf.Clamp(
                            ResponsiveTransitionCardSize().y * 0.18f,
                            28f,
                            48f);
                    if (t < ingressStart)
                    {
                        float approachT = Mathf.Clamp01(t / ingressStart);
                        Vector2 tangent = destination - start;
                        Vector2 normal = tangent.sqrMagnitude > 0.01f
                            ? new Vector2(-tangent.y, tangent.x).normalized
                            : Vector2.right;
                        float handedness = start.x <= destination.x ? 1f : -1f;
                        Vector2 control = Vector2.Lerp(start, approach, 0.52f) +
                            Vector2.up * arc +
                            normal * handedness * Mathf.Min(24f, arc * 0.45f);
                        rect.anchoredPosition = QuadraticBezier(
                            start,
                            control,
                            approach,
                            TransitionEaseOutCubic(approachT));
                        rect.localRotation = Quaternion.Euler(
                            Mathf.Sin(approachT * Mathf.PI) * -7f,
                            0f,
                            Mathf.Lerp(startTilt, 0f, approachT));
                        scale = Mathf.Lerp(
                            0.94f,
                            0.88f,
                            Mathf.SmoothStep(0f, 1f, approachT));
                    }
                    else
                    {
                        float ingress = Mathf.Clamp01(
                            Mathf.InverseLerp(ingressStart, 1f, t));
                        float suction = TransitionEaseInCubic(ingress);
                        float swirl = Mathf.Sin(ingress * Mathf.PI * 2f) *
                                      (1f - ingress) * 10f;
                        rect.anchoredPosition = Vector2.Lerp(
                            approach,
                            destination + Vector2.down * 24f,
                            suction) + Vector2.right * swirl;
                        rect.localRotation = Quaternion.Euler(
                            Mathf.Lerp(0f, 78f, suction),
                            Mathf.Sin(ingress * Mathf.PI) * 7f,
                            Mathf.Sin(ingress * Mathf.PI * 1.5f) * 4f);
                        scale = Mathf.Lerp(0.88f, 0.028f, suction);
                        destinationWell?.SetIngressProgress(ingress);
                    }
                }
                rect.localScale = new Vector3(
                    scale * horizontalScale,
                    scale,
                    1f);
                float fadeStart = sinksIntoSpecialPile ? 0.88f : 0.92f;
                group.alpha = t < fadeStart
                    ? 1f
                    : 1f - Mathf.SmoothStep(fadeStart, 1f, t);
                if (trail != null)
                {
                    float trailStrength = sinksIntoSpecialPile
                        ? 1f - Mathf.SmoothStep(0.70f, 1f, t)
                        : Mathf.Sin(t * Mathf.PI);
                    trail.anchoredPosition = new Vector2(
                        Mathf.Lerp(-13f, -5f, t),
                        Mathf.Lerp(8f, 3f, t));
                    Image trailImage = trail.GetComponent<Image>();
                    if (trailImage != null)
                    {
                        trailImage.color = new Color(
                            transitionAccent.r,
                            transitionAccent.g,
                            transitionAccent.b,
                            0.17f * trailStrength);
                    }
                }
                UpdateTransitionPulse(destinationPulse, t, 0.62f);
                yield return null;
            }
            if (overlay != null && destinationSprite != null)
                image.sprite = destinationSprite;
            if (!sinksIntoSpecialPile)
                RevealTransitionTarget(target);
            destinationWell?.PlayArrivalPulse();
            if (PlaySummonMethodArrivalEffect(summonFrame, destination))
                PlaySummonMethodParticleVfx(summonFrame, destinationZone);
            else
                PlayMonsterSummonArrivalEffect(arrivalEffect, destination);
            if (destinationPulse != null)
                Destroy(destinationPulse);
            if (overlay != null)
                Destroy(overlay);
        }

        private GameObject CreateTransitionCard(
            Sprite sprite,
            Vector2 position)
        {
            return CreateTransitionCard(sprite, position, Cyan);
        }

        private GameObject CreateTransitionCard(
            Sprite sprite,
            Vector2 position,
            Color accent)
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
            outline.effectColor = new Color(
                accent.r,
                accent.g,
                accent.b,
                0.72f);
            outline.effectDistance = new Vector2(3f, -3f);

            Image trail = CreateImage(
                root.transform,
                "Rastro da Carta",
                Vector2.zero,
                Vector2.one,
                new Color(accent.r, accent.g, accent.b, 0.13f));
            trail.sprite = sprite;
            trail.preserveAspect = true;
            trail.raycastTarget = false;
            trail.rectTransform.anchoredPosition = new Vector2(-9f, 5f);
            return root;
        }

        private GameObject CreateTransitionPulse(
            Sprite sprite,
            Vector2 position,
            Color accent)
        {
            GameObject root = CreateTransitionContainer(
                "Pulso de Destino",
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = ResponsiveTransitionCardSize() * 1.08f;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * 0.78f;

            Image image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(accent.r, accent.g, accent.b, 0.16f);

            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = new Color(
                accent.r,
                accent.g,
                accent.b,
                0.82f);
            outline.effectDistance = new Vector2(5f, -5f);
            root.GetComponent<CanvasGroup>().alpha = 0f;
            return root;
        }

        private static void UpdateTransitionPulse(
            GameObject pulse,
            float normalizedTime,
            float startAt)
        {
            if (pulse == null)
                return;
            float t = Mathf.Clamp01(Mathf.InverseLerp(
                startAt,
                1f,
                normalizedTime));
            CanvasGroup group = pulse.GetComponent<CanvasGroup>();
            RectTransform rect = pulse.GetComponent<RectTransform>();
            group.alpha = Mathf.Sin(t * Mathf.PI) * 0.82f;
            rect.localScale = Vector3.one * Mathf.Lerp(
                0.78f,
                1.16f,
                TransitionEaseOutCubic(t));
        }

        private static float TransitionTravelEase(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.86f)
            {
                return Mathf.LerpUnclamped(
                    0f,
                    1.012f,
                    TransitionEaseOutCubic(t / 0.86f));
            }
            return Mathf.Lerp(
                1.012f,
                1f,
                Mathf.SmoothStep(0f, 1f, (t - 0.86f) / 0.14f));
        }

        private static float TransitionEaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float TransitionEaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        private static Vector2 QuadraticBezier(
            Vector2 start,
            Vector2 control,
            Vector2 end,
            float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return inverse * inverse * start +
                   2f * inverse * t * control +
                   t * t * end;
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
            Consider(duelHistoryButton?.gameObject);
            Consider(duelHistoryOverlay);
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
