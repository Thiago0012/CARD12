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
            CanvasGroup target)
        {
            GameObject container = CreateTransitionContainer(
                "Fragmentos da Carta");
            List<CardFragmentVisual> fragments = CreateCardFragments(
                container.transform,
                sprite,
                start);
            float distance = Vector2.Distance(start, destination);
            float arc = Mathf.Clamp(distance * 0.12f, 28f, 92f);
            float elapsed = 0f;
            while (elapsed < duration && container != null)
            {
                elapsed += Time.unscaledDeltaTime;
                foreach (CardFragmentVisual fragment in fragments)
                {
                    if (fragment.Rect == null)
                        continue;
                    float t = Mathf.Clamp01(
                        (elapsed - fragment.Delay) /
                        Mathf.Max(0.01f, duration - fragment.Delay));
                    float burstT = Mathf.Clamp01(t / 0.24f);
                    float travelT = Mathf.Clamp01((t - 0.16f) / 0.84f);
                    Vector2 burstPosition = fragment.Start +
                                            fragment.Burst *
                                            (1f - Mathf.Pow(1f - burstT, 3f));
                    Vector2 targetPosition =
                        destination + fragment.DestinationOffset;
                    fragment.Rect.anchoredPosition = Vector2.Lerp(
                        burstPosition,
                        targetPosition,
                        Mathf.SmoothStep(0f, 1f, travelT)) +
                        Vector2.up * Mathf.Sin(travelT * Mathf.PI) * arc;
                    fragment.Rect.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        fragment.Rotation * travelT);
                    fragment.Rect.localScale = Vector3.one *
                        Mathf.Lerp(1f, 0.22f, travelT);
                    fragment.Group.alpha =
                        1f - Mathf.SmoothStep(0.56f, 1f, travelT);
                }
                yield return null;
            }
            RevealTransitionTarget(target);
            if (container != null)
                Destroy(container);
        }

        private List<CardFragmentVisual> CreateCardFragments(
            Transform parent,
            Sprite sprite,
            Vector2 center)
        {
            const int columns = 3;
            const int rows = 3;
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
                float variation = ((row * columns + column) % 3 - 1) * 9f;
                fragments.Add(new CardFragmentVisual
                {
                    Rect = fragmentRect,
                    Group = fragmentObject.GetComponent<CanvasGroup>(),
                    Start = center + offset,
                    Burst = direction * (32f + 7f * (row + column)) +
                            new Vector2(variation, -variation * 0.35f),
                    DestinationOffset = direction * (8f + row * 2f),
                    Rotation = (column - 1) * 95f +
                               (row - 1) * 36f,
                    Delay = (row * columns + column) * 0.008f
                });
            }
            return fragments;
        }
    }
}
