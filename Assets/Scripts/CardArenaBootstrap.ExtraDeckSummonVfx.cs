using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private sealed class ExtraDeckSummonFocus
        {
            public Color Accent;
            public Image ScreenGlow;
            public CanvasGroup CrossGlow;
            public RectTransform HorizontalBeam;
            public RectTransform VerticalBeam;
            public RectTransform[] Rings;
            public CanvasGroup[] RingGroups;
        }

        private ExtraDeckSummonFocus CreateExtraDeckSummonFocus(
            Transform parent,
            Color accent)
        {
            var focus = new ExtraDeckSummonFocus
            {
                Accent = accent,
                Rings = new RectTransform[3],
                RingGroups = new CanvasGroup[3]
            };
            focus.ScreenGlow = CreateImage(
                parent,
                "Brilho da Invocação do Deck Adicional",
                Vector2.zero,
                Vector2.one,
                new Color(accent.r, accent.g, accent.b, 0f));
            focus.ScreenGlow.raycastTarget = false;

            var cross = new GameObject(
                "Foco de Luz da Invocação",
                typeof(RectTransform),
                typeof(CanvasGroup));
            cross.transform.SetParent(parent, false);
            RectTransform crossRect = cross.GetComponent<RectTransform>();
            crossRect.anchorMin = Vector2.zero;
            crossRect.anchorMax = Vector2.one;
            crossRect.offsetMin = crossRect.offsetMax = Vector2.zero;
            focus.CrossGlow = cross.GetComponent<CanvasGroup>();
            focus.CrossGlow.alpha = 0f;
            focus.CrossGlow.blocksRaycasts = false;
            focus.CrossGlow.interactable = false;
            focus.HorizontalBeam = CreateImage(
                cross.transform,
                "Feixe Horizontal",
                new Vector2(0f, 0.493f),
                new Vector2(1f, 0.507f),
                new Color(accent.r, accent.g, accent.b, 0.72f))
                .rectTransform;
            focus.VerticalBeam = CreateImage(
                cross.transform,
                "Feixe Vertical",
                new Vector2(0.496f, 0f),
                new Vector2(0.504f, 1f),
                new Color(accent.r, accent.g, accent.b, 0.58f))
                .rectTransform;
            focus.HorizontalBeam.GetComponent<Image>().raycastTarget = false;
            focus.VerticalBeam.GetComponent<Image>().raycastTarget = false;

            for (int index = 0; index < focus.Rings.Length; index++)
            {
                var ring = new GameObject(
                    $"Anel de Invocação {index + 1}",
                    typeof(RectTransform),
                    typeof(CanvasGroup));
                ring.transform.SetParent(parent, false);
                RectTransform rect = ring.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(500f, 710f);
                CanvasGroup ringGroup = ring.GetComponent<CanvasGroup>();
                ringGroup.alpha = 0f;
                ringGroup.blocksRaycasts = false;
                ringGroup.interactable = false;
                CreateFocusFrame(rect, accent, index);
                focus.Rings[index] = rect;
                focus.RingGroups[index] = ringGroup;
            }
            return focus;
        }

        private void CreateFocusFrame(
            RectTransform parent,
            Color accent,
            int index)
        {
            float thickness = 0.008f + index * 0.003f;
            Color color = new Color(
                accent.r,
                accent.g,
                accent.b,
                0.82f - index * 0.13f);
            Image top = CreateImage(
                parent,
                "Luz Superior",
                new Vector2(0f, 1f - thickness),
                Vector2.one,
                color);
            Image bottom = CreateImage(
                parent,
                "Luz Inferior",
                Vector2.zero,
                new Vector2(1f, thickness),
                color);
            Image left = CreateImage(
                parent,
                "Luz Esquerda",
                Vector2.zero,
                new Vector2(thickness, 1f),
                color);
            Image right = CreateImage(
                parent,
                "Luz Direita",
                new Vector2(1f - thickness, 0f),
                Vector2.one,
                color);
            top.raycastTarget = false;
            bottom.raycastTarget = false;
            left.raycastTarget = false;
            right.raycastTarget = false;
        }

        private void UpdateExtraDeckSummonFocus(
            ExtraDeckSummonFocus focus,
            float elapsed,
            float duration,
            float speed)
        {
            if (focus == null)
                return;
            float progress = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, duration));
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float pulse =
                0.5f + 0.5f * Mathf.Sin(elapsed * 7f * speed);
            focus.ScreenGlow.color = new Color(
                focus.Accent.r,
                focus.Accent.g,
                focus.Accent.b,
                envelope * Mathf.Lerp(0.055f, 0.15f, pulse));
            focus.CrossGlow.alpha =
                envelope * Mathf.Lerp(0.28f, 0.66f, pulse);
            focus.HorizontalBeam.localScale = new Vector3(
                Mathf.Lerp(0.35f, 1f, envelope),
                1f,
                1f);
            focus.VerticalBeam.localScale = new Vector3(
                1f,
                Mathf.Lerp(0.35f, 1f, envelope),
                1f);

            for (int index = 0; index < focus.Rings.Length; index++)
            {
                float wave = Mathf.Repeat(
                    progress * 2.2f + index / 3f,
                    1f);
                focus.Rings[index].localScale =
                    Vector3.one * Mathf.Lerp(0.72f, 1.52f, wave);
                focus.Rings[index].localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    elapsed * (index % 2 == 0 ? 24f : -24f));
                focus.RingGroups[index].alpha =
                    envelope * (1f - wave) * 0.78f;
            }
        }
    }
}
