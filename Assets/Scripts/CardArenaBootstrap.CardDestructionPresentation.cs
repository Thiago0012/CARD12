using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private sealed class CardFragmentVisual
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 Start;
            public Vector2 Burst;
            public Vector2 DestinationOffset;
            public float Rotation;
            public float Delay;
        }

        private IEnumerator AnimateCardDestruction(
            Sprite sprite,
            Vector2 start,
            Vector2 destination,
            float duration,
            CanvasGroup target,
            DuelSpecialZoneWellVisual destinationWell)
        {
            GameObject container = CreateTransitionContainer(
                "Fragmentos da Carta");
            Image impact = CreateDestructionImpact(
                container.transform,
                sprite,
                start);
            CanvasGroup impactGroup = impact.GetComponent<CanvasGroup>();
            List<CardFragmentVisual> fragments = CreateCardFragments(
                container.transform,
                sprite,
                start);
            Color transitionAccent = destinationWell != null
                ? destinationWell.AccentColor
                : Gold;
            GameObject destinationPulse = destinationWell == null
                ? CreateTransitionPulse(
                    sprite,
                    destination,
                    transitionAccent)
                : null;
            destinationPulse?.transform.SetSiblingIndex(
                container.transform.GetSiblingIndex());
            destinationWell?.BeginIngress();
            float distance = Vector2.Distance(start, destination);
            float arc = Mathf.Clamp(distance * 0.06f, 16f, 48f);
            float elapsed = 0f;
            while (elapsed < duration && container != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float globalT = Mathf.Clamp01(elapsed / duration);
                float impactT = Mathf.Clamp01(globalT / 0.20f);
                impactGroup.alpha = 1f - Mathf.SmoothStep(
                    0.32f,
                    1f,
                    impactT);
                impact.rectTransform.localScale = Vector3.one * Mathf.Lerp(
                    1f,
                    1.14f,
                    TransitionEaseOutCubic(impactT));
                impact.color = Color.Lerp(
                    Color.white,
                    new Color(
                        transitionAccent.r,
                        transitionAccent.g,
                        transitionAccent.b,
                        1f),
                    Mathf.Sin(impactT * Mathf.PI));
                foreach (CardFragmentVisual fragment in fragments)
                {
                    if (fragment.Rect == null)
                        continue;
                    float t = Mathf.Clamp01(
                        (globalT - 0.14f - fragment.Delay) /
                        Mathf.Max(0.01f, 0.86f - fragment.Delay));
                    if (t <= 0f)
                    {
                        fragment.Group.alpha = 0f;
                        continue;
                    }

                    fragment.Group.alpha = 1f;
                    if (t < 0.28f)
                    {
                        float burstT = TransitionEaseOutCubic(t / 0.28f);
                        fragment.Rect.anchoredPosition = fragment.Start +
                            fragment.Burst * burstT;
                        fragment.Rect.localRotation = Quaternion.Euler(
                            0f,
                            0f,
                            fragment.Rotation * 0.22f * burstT);
                        fragment.Rect.localScale = Vector3.one * Mathf.Lerp(
                            1f,
                            0.88f,
                            burstT);
                        continue;
                    }

                    float travelT = Mathf.Clamp01((t - 0.28f) / 0.72f);
                    float attraction = TransitionEaseInCubic(travelT);
                    Vector2 burstPosition = fragment.Start + fragment.Burst;
                    Vector2 targetPosition =
                        destination + fragment.DestinationOffset +
                        (destinationWell != null
                            ? Vector2.down * 18f
                            : Vector2.zero);
                    fragment.Rect.anchoredPosition = Vector2.Lerp(
                        burstPosition,
                        targetPosition,
                        attraction) +
                        Vector2.up * Mathf.Sin(travelT * Mathf.PI) * arc;
                    fragment.Rect.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        fragment.Rotation * Mathf.Lerp(
                            0.22f,
                            1f,
                            travelT));
                    fragment.Rect.localScale = Vector3.one * Mathf.Lerp(
                        0.88f,
                        destinationWell != null ? 0.025f : 0.08f,
                        attraction);
                    fragment.Group.alpha = 1f - Mathf.SmoothStep(
                        destinationWell != null ? 0.82f : 0.64f,
                        1f,
                        travelT);
                }
                if (destinationWell != null)
                {
                    destinationWell.SetIngressProgress(
                        Mathf.Clamp01(Mathf.InverseLerp(
                            0.52f,
                            1f,
                            globalT)));
                }
                UpdateTransitionPulse(destinationPulse, globalT, 0.70f);
                yield return null;
            }
            // O destino de destruição é um poço público. O estado mantém a
            // carta e o navegador a exibe sob demanda; nada fica estacionado
            // sobre o campo depois que os fragmentos entram no poço.
            if (destinationWell == null)
                RevealTransitionTarget(target);
            destinationWell?.PlayArrivalPulse();
            if (destinationPulse != null)
                Destroy(destinationPulse);
            if (container != null)
                Destroy(container);
        }

        private Image CreateDestructionImpact(
            Transform parent,
            Sprite sprite,
            Vector2 center)
        {
            Image impact = CreateImage(
                parent,
                "Impacto da Destruição",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Color.white);
            impact.sprite = sprite;
            impact.preserveAspect = true;
            impact.raycastTarget = false;
            impact.rectTransform.sizeDelta = ResponsiveTransitionCardSize();
            impact.rectTransform.anchoredPosition = center;
            var group = impact.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            Outline outline = impact.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.62f, 0.18f, 0.94f);
            outline.effectDistance = new Vector2(6f, -6f);
            return impact;
        }

        private List<CardFragmentVisual> CreateCardFragments(
            Transform parent,
            Sprite sprite,
            Vector2 center)
        {
            const int columns = 4;
            const int rows = 4;
            Vector2 cardSize = ResponsiveTransitionCardSize();
            Vector2 fragmentSize = new(
                cardSize.x / columns + 1f,
                cardSize.y / rows + 1f);
            var fragments = new List<CardFragmentVisual>(columns * rows);
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Vector2 offset = new(
                    (column - (columns - 1) * 0.5f) *
                    (cardSize.x / columns),
                    (row - (rows - 1) * 0.5f) *
                    (cardSize.y / rows));
                var fragmentObject = new GameObject(
                    $"Fragmento {row + 1}-{column + 1}",
                    typeof(RectTransform),
                    typeof(RectMask2D),
                    typeof(CanvasGroup));
                fragmentObject.transform.SetParent(parent, false);
                RectTransform fragmentRect =
                    fragmentObject.GetComponent<RectTransform>();
                fragmentRect.anchorMin = fragmentRect.anchorMax =
                    new Vector2(0.5f, 0.5f);
                fragmentRect.pivot = new Vector2(0.5f, 0.5f);
                fragmentRect.sizeDelta = fragmentSize;
                fragmentRect.anchoredPosition = center + offset;
                CanvasGroup fragmentGroup =
                    fragmentObject.GetComponent<CanvasGroup>();
                fragmentGroup.alpha = 0f;

                Image image = CreateImage(
                    fragmentObject.transform,
                    "Recorte da Arte",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Color.white);
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = cardSize;
                image.rectTransform.anchoredPosition = -offset;

                Vector2 direction = offset.sqrMagnitude > 0.01f
                    ? offset.normalized
                    : Vector2.up;
                float variation = ((row * columns + column) % 4 - 1.5f) * 7f;
                fragments.Add(new CardFragmentVisual
                {
                    Rect = fragmentRect,
                    Group = fragmentGroup,
                    Start = center + offset,
                    Burst = direction * (30f + 5f * (row + column)) +
                            new Vector2(variation, -variation * 0.35f),
                    DestinationOffset = direction * (4f + row * 1.5f),
                    Rotation = (column - 1.5f) * 88f +
                               (row - 1.5f) * 42f,
                    Delay = (row * columns + column) * 0.0025f
                });
            }
            return fragments;
        }
    }
}
