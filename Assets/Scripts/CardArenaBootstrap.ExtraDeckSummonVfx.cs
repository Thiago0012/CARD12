using ArcaneArena.Cards;
using ArcaneArena.Presentation;
using ArcaneDuel.Game;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private sealed class ExtraDeckSummonFocus
        {
            public Color Accent;
            public Color Secondary;
            public MonsterFrameKind Frame;
            public Image ScreenGlow;
            public CanvasGroup CrossGlow;
            public RectTransform HorizontalBeam;
            public RectTransform VerticalBeam;
            public RectTransform[] Rings;
            public CanvasGroup[] RingGroups;
            public SummonMethodVfxGraphic Motif;
            public RectTransform[] Materials;
            public CanvasGroup[] MaterialGroups;
            public Vector2[] MaterialOrigins;
            public float[] MaterialRotations;
            public Image MergeFlash;
            public float CardReveal;
            public int DetailLevel;
        }

        private ExtraDeckSummonFocus CreateExtraDeckSummonFocus(
            Transform parent,
            Color accent,
            MonsterFrameKind summonFrame,
            IReadOnlyList<uint> materialCodes)
        {
            Color secondary = SummonMethodVfxPalette.Secondary(summonFrame);
            ArcaneGraphicsQuality quality = ArcaneGraphicsPreferences.Quality;
            var focus = new ExtraDeckSummonFocus
            {
                Accent = accent,
                Secondary = secondary,
                Frame = summonFrame,
                Rings = new RectTransform[3],
                RingGroups = new CanvasGroup[3],
                DetailLevel = (int)quality
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

            var motif = new GameObject(
                $"Assinatura da Invocação {summonFrame}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(SummonMethodVfxGraphic));
            motif.transform.SetParent(parent, false);
            RectTransform motifRect = motif.GetComponent<RectTransform>();
            motifRect.anchorMin = new Vector2(0.08f, 0.04f);
            motifRect.anchorMax = new Vector2(0.92f, 0.96f);
            motifRect.offsetMin = motifRect.offsetMax = Vector2.zero;
            focus.Motif = motif.GetComponent<SummonMethodVfxGraphic>();
            focus.Motif.Configure(
                summonFrame,
                accent,
                secondary,
                quality <= ArcaneGraphicsQuality.Low);
            motif.transform.SetSiblingIndex(
                Mathf.Max(0, motif.transform.GetSiblingIndex() - 1));

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
                CreateFocusFrame(
                    rect,
                    index % 2 == 0 ? accent : secondary,
                    index);
                focus.Rings[index] = rect;
                focus.RingGroups[index] = ringGroup;
            }
            CreateSummonMaterialCards(
                focus,
                parent,
                materialCodes);
            return focus;
        }

        private void CreateSummonMaterialCards(
            ExtraDeckSummonFocus focus,
            Transform parent,
            IReadOnlyList<uint> materialCodes)
        {
            int count = Mathf.Min(materialCodes?.Count ?? 0, 8);
            focus.Materials = new RectTransform[count];
            focus.MaterialGroups = new CanvasGroup[count];
            focus.MaterialOrigins = SummonMaterialOrigins(
                count,
                focus.Frame);
            focus.MaterialRotations = new float[count];

            float viewportScale = frame == null
                ? 1f
                : Mathf.Clamp(frame.rect.height / 1080f, 0.72f, 1.14f);
            float cardHeight = 182f * viewportScale;
            float cardWidth = cardHeight * 0.695f;
            for (int index = 0; index < count; index++)
            {
                uint code = materialCodes[index];
                Image material = CreateImage(
                    parent,
                    $"Material {index + 1} · {CardName(code)}",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Color.white);
                material.sprite = SpriteFor(code);
                material.preserveAspect = true;
                material.raycastTarget = false;
                material.rectTransform.sizeDelta =
                    new Vector2(cardWidth, cardHeight);
                material.rectTransform.anchoredPosition =
                    focus.MaterialOrigins[index] * viewportScale;
                focus.MaterialOrigins[index] *= viewportScale;
                float rotation = count <= 2
                    ? (index == 0 ? -5f : 5f)
                    : Mathf.Lerp(-12f, 12f, index / Mathf.Max(1f, count - 1f));
                focus.MaterialRotations[index] = rotation;
                material.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, rotation);
                CanvasGroup materialGroup =
                    material.gameObject.AddComponent<CanvasGroup>();
                materialGroup.alpha = 0f;
                materialGroup.blocksRaycasts = false;
                materialGroup.interactable = false;
                Outline outline = material.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(
                    focus.Accent.r,
                    focus.Accent.g,
                    focus.Accent.b,
                    0.82f);
                outline.effectDistance = new Vector2(3f, -3f);
                focus.Materials[index] = material.rectTransform;
                focus.MaterialGroups[index] = materialGroup;
            }

            focus.MergeFlash = CreateImage(
                parent,
                "Clarão de união dos materiais",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Color(1f, 1f, 1f, 0f));
            focus.MergeFlash.raycastTarget = false;
            focus.MergeFlash.rectTransform.sizeDelta =
                Vector2.one * 260f * viewportScale;
            focus.MergeFlash.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
        }

        private static Vector2[] SummonMaterialOrigins(
            int count,
            MonsterFrameKind frameKind)
        {
            var result = new Vector2[Mathf.Max(0, count)];
            if (count == 0)
                return result;
            if (count == 1)
            {
                result[0] = frameKind == MonsterFrameKind.Link
                    ? new Vector2(-310f, 50f)
                    : new Vector2(0f, 230f);
                return result;
            }
            if (count == 2)
            {
                result[0] = new Vector2(-235f, 12f);
                result[1] = new Vector2(235f, 12f);
                return result;
            }
            if (count == 3)
            {
                result[0] = new Vector2(0f, 245f);
                result[1] = new Vector2(-285f, -128f);
                result[2] = new Vector2(285f, -128f);
                return result;
            }

            float angleOffset = frameKind == MonsterFrameKind.Xyz
                ? 18f
                : 90f;
            for (int index = 0; index < count; index++)
            {
                float angle = angleOffset + index * 360f / count;
                float radians = angle * Mathf.Deg2Rad;
                result[index] = new Vector2(
                    Mathf.Cos(radians) * 345f,
                    Mathf.Sin(radians) * 235f);
            }
            return result;
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
            float qualityIntensity = Mathf.Lerp(
                0.58f,
                1.12f,
                focus.DetailLevel / 4f);
            focus.ScreenGlow.color = new Color(
                focus.Accent.r,
                focus.Accent.g,
                focus.Accent.b,
                envelope * Mathf.Lerp(0.045f, 0.14f, pulse) *
                qualityIntensity);

            // Long orthogonal beams belong to Link's construction language;
            // the other methods now rely on their own spiral, tuner, galaxy
            // or pendulum signature instead of sharing the old generic cross.
            float crossWeight = focus.Frame == MonsterFrameKind.Link
                ? 1f
                : 0f;
            focus.CrossGlow.alpha = envelope * crossWeight *
                                    Mathf.Lerp(0.14f, 0.42f, pulse) *
                                    qualityIntensity;
            focus.HorizontalBeam.localScale = new Vector3(
                Mathf.Lerp(0.35f, 1f, envelope),
                1f,
                1f);
            focus.VerticalBeam.localScale = new Vector3(
                1f,
                Mathf.Lerp(0.35f, 1f, envelope),
                1f);

            focus.Motif?.SetAnimation(progress, elapsed);

            int visibleFrames = focus.DetailLevel switch
            {
                <= 0 => 1,
                1 => 1,
                2 => 2,
                _ => 3
            };
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
                    focus.Frame == MonsterFrameKind.Link &&
                    index < visibleFrames
                        ? envelope * (1f - wave) * 0.42f *
                          qualityIntensity
                        : 0f;
            }

            UpdateSummonMaterials(focus, progress, elapsed);
        }

        private static void UpdateSummonMaterials(
            ExtraDeckSummonFocus focus,
            float progress,
            float elapsed)
        {
            int count = focus.Materials?.Length ?? 0;
            float revealStart = count > 0 ? 0.43f : 0.20f;
            float revealEnd = count > 0 ? 0.61f : 0.43f;
            focus.CardReveal = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(revealStart, revealEnd, progress));

            float mergePeak = 1f - Mathf.Clamp01(
                Mathf.Abs(progress - revealStart) / 0.105f);
            if (focus.MergeFlash != null)
            {
                focus.MergeFlash.color = new Color(
                    1f,
                    1f,
                    1f,
                    mergePeak * mergePeak *
                    Mathf.Lerp(0.36f, 0.78f, focus.DetailLevel / 4f));
                focus.MergeFlash.rectTransform.localScale = Vector3.one *
                    Mathf.Lerp(0.28f, 1.55f, 1f - mergePeak);
                focus.MergeFlash.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, 45f + elapsed * 96f);
            }

            for (int index = 0; index < count; index++)
            {
                RectTransform material = focus.Materials[index];
                CanvasGroup materialGroup = focus.MaterialGroups[index];
                if (material == null || materialGroup == null)
                    continue;

                float stagger = count <= 2 ? 0f : index * 0.012f;
                float merge = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        focus.Frame == MonsterFrameKind.Pendulum
                            ? 0.25f + stagger
                            : 0.13f + stagger,
                        0.47f + stagger,
                        progress));
                float appear = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        0.015f + stagger,
                        0.12f + stagger,
                        progress));
                float disappear = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        0.39f + stagger,
                        0.53f + stagger,
                        progress));

                Vector2 origin = focus.MaterialOrigins[index];
                if (focus.Frame == MonsterFrameKind.Xyz)
                {
                    origin = RotateSummonPoint(
                        origin,
                        progress * 112f * (1f - merge));
                }
                else if (focus.Frame == MonsterFrameKind.Synchro)
                {
                    origin = RotateSummonPoint(
                        origin,
                        progress * 34f * (index % 2 == 0 ? 1f : -1f) *
                        (1f - merge));
                }
                else if (focus.Frame == MonsterFrameKind.Pendulum)
                {
                    float direction = index % 2 == 0 ? -1f : 1f;
                    origin.y += Mathf.Sin(elapsed * 5.4f + index * Mathf.PI) *
                                28f * (1f - merge);
                    origin.x += direction *
                                Mathf.Sin(elapsed * 2.7f) * 18f *
                                (1f - merge);
                }

                material.anchoredPosition = Vector2.Lerp(
                    origin,
                    Vector2.zero,
                    merge);
                float methodRotation = focus.Frame switch
                {
                    MonsterFrameKind.Fusion =>
                        (index % 2 == 0 ? 1f : -1f) * merge * 156f,
                    MonsterFrameKind.Synchro => merge * 72f,
                    MonsterFrameKind.Xyz => merge * 210f,
                    MonsterFrameKind.Link =>
                        (index % 2 == 0 ? -1f : 1f) * merge * 90f,
                    MonsterFrameKind.Pendulum =>
                        Mathf.Sin(elapsed * 4.8f + index * Mathf.PI) *
                        10f * (1f - merge),
                    _ => 0f
                };
                material.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(
                        focus.MaterialRotations[index] + methodRotation,
                        0f,
                        merge));
                material.localScale = Vector3.one * Mathf.Lerp(
                    0.92f,
                    0.20f,
                    merge);
                materialGroup.alpha = appear * disappear;
            }
        }

        private static Vector2 RotateSummonPoint(
            Vector2 point,
            float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                point.x * cosine - point.y * sine,
                point.x * sine + point.y * cosine);
        }
    }
}
