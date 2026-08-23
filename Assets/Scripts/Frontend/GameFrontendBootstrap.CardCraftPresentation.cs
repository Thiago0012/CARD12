using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private sealed class CraftFragment
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 Origin;
            public Vector2 Direction;
            public float Rotation;
        }

        private void PlayDeckEditorCraftVisual(bool generate, Sprite sprite)
        {
            if (sprite == null || _screenRoot == null ||
                _deckEditorDetailArtwork == null)
            {
                return;
            }
            StartCoroutine(AnimateDeckEditorCraftVisual(generate, sprite));
        }

        private IEnumerator AnimateDeckEditorCraftVisual(
            bool generate,
            Sprite sprite)
        {
            RectTransform screen = _screenRoot as RectTransform;
            if (screen == null)
                yield break;

            Vector3[] worldCorners = new Vector3[4];
            _deckEditorDetailArtwork.rectTransform.GetWorldCorners(worldCorners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                screen,
                RectTransformUtility.WorldToScreenPoint(null,
                    (worldCorners[0] + worldCorners[2]) * 0.5f),
                null,
                out Vector2 center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                screen,
                RectTransformUtility.WorldToScreenPoint(null, worldCorners[0]),
                null,
                out Vector2 bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                screen,
                RectTransformUtility.WorldToScreenPoint(null, worldCorners[2]),
                null,
                out Vector2 topRight);
            Vector2 cardSize = new(
                Mathf.Max(72f, Mathf.Abs(topRight.x - bottomLeft.x)),
                Mathf.Max(108f, Mathf.Abs(topRight.y - bottomLeft.y)));

            Image container = CreatePanel(
                _screenRoot,
                generate ? "Efeito de geração" : "Efeito de desmantelo",
                Vector2.zero,
                Vector2.one,
                Color.clear);
            container.raycastTarget = false;
            container.transform.SetAsLastSibling();

            Image flash = CreateCardArtwork(
                container.transform,
                sprite,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                0f,
                true);
            flash.raycastTarget = false;
            flash.rectTransform.sizeDelta = cardSize;
            flash.rectTransform.anchoredPosition = center;
            CanvasGroup flashGroup =
                flash.gameObject.AddComponent<CanvasGroup>();
            AddOutline(
                flash.gameObject,
                generate
                    ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.96f)
                    : new Color(Danger.r, Gold.g, 0.18f, 0.98f),
                new Vector2(5f, -5f));

            const int columns = 3;
            const int rows = 4;
            var fragments = new List<CraftFragment>(columns * rows);
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Vector2 cell = new(cardSize.x / columns, cardSize.y / rows);
                Vector2 offset = new(
                    (column - (columns - 1) * 0.5f) * cell.x,
                    (row - (rows - 1) * 0.5f) * cell.y);
                var fragmentObject = new GameObject(
                    $"Fragmento {row + 1}-{column + 1}",
                    typeof(RectTransform),
                    typeof(RectMask2D),
                    typeof(CanvasGroup));
                fragmentObject.transform.SetParent(container.transform, false);
                RectTransform rect =
                    fragmentObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = cell + Vector2.one * 1.5f;
                rect.anchoredPosition = center + offset;
                Image crop = CreateCardArtwork(
                    fragmentObject.transform,
                    sprite,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    0f,
                    true);
                crop.raycastTarget = false;
                crop.rectTransform.sizeDelta = cardSize;
                crop.rectTransform.anchoredPosition = -offset;
                CanvasGroup group =
                    fragmentObject.GetComponent<CanvasGroup>();
                group.alpha = generate ? 0f : 1f;
                Vector2 direction = offset.sqrMagnitude > 0.01f
                    ? offset.normalized
                    : Vector2.up;
                fragments.Add(new CraftFragment
                {
                    Rect = rect,
                    Group = group,
                    Origin = center + offset,
                    Direction = direction * (42f + 8f * (row + column)),
                    Rotation = (column - 1f) * 52f + (row - 1.5f) * 22f
                });
            }

            float duration = generate ? 0.30f : 0.36f;
            float elapsed = 0f;
            while (elapsed < duration && container != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                flashGroup.alpha = generate
                    ? 1f - Mathf.SmoothStep(0.55f, 1f, t)
                    : 1f - Mathf.SmoothStep(0.12f, 0.62f, t);
                flash.rectTransform.localScale = Vector3.one *
                    (generate
                        ? Mathf.Lerp(0.86f, 1.12f, eased)
                        : Mathf.Lerp(1f, 1.08f, eased));

                foreach (CraftFragment fragment in fragments)
                {
                    if (fragment.Rect == null)
                        continue;
                    float travel = generate ? 1f - eased : eased;
                    fragment.Rect.anchoredPosition = fragment.Origin +
                        fragment.Direction * travel;
                    fragment.Rect.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        fragment.Rotation * travel);
                    fragment.Rect.localScale = Vector3.one *
                        Mathf.Lerp(generate ? 0.34f : 1f,
                            generate ? 1f : 0.20f,
                            eased);
                    fragment.Group.alpha = generate
                        ? Mathf.SmoothStep(0f, 0.48f, t)
                        : 1f - Mathf.SmoothStep(0.44f, 1f, t);
                }
                yield return null;
            }
            if (container != null)
                Destroy(container.gameObject);
        }
    }
}
