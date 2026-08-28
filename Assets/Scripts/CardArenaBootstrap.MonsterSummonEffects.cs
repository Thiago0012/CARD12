using System.Collections;
using ArcaneArena.Cards;
using ArcaneArena.Presentation;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    /// <summary>
    /// Short, non-blocking monster arrival rings shown after card travel.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private Sprite summonEffectYellow;
        private Sprite summonEffectBlue;
        private Sprite summonEffectPurple;

        private MonsterSummonArrivalEffect ArrivalEffectFor(uint code)
        {
            return MonsterSummonEffectPolicy.Resolve(LegacyEntryFor(code));
        }

        private void PlayMonsterSummonArrivalEffect(
            MonsterSummonArrivalEffect effect,
            Vector2 destination)
        {
            Sprite sprite = SummonEffectSprite(effect);
            if (effect == MonsterSummonArrivalEffect.None || sprite == null ||
                frame == null)
            {
                return;
            }
            StartCoroutine(AnimateMonsterSummonArrivalEffect(
                sprite,
                effect,
                destination));
        }

        private bool PlaySummonMethodArrivalEffect(
            MonsterFrameKind summonFrame,
            Vector2 destination)
        {
            if (!SummonMethodVfxPalette.Supports(summonFrame) || frame == null)
                return false;
            StartCoroutine(AnimateSummonMethodArrivalEffect(
                summonFrame,
                destination));
            return true;
        }

        private bool PlaySummonMethodParticleVfx(
            MonsterFrameKind summonFrame,
            DuelZone3D destinationZone)
        {
            if (!SummonMethodVfxPalette.Supports(summonFrame) ||
                destinationZone == null)
            {
                return false;
            }
            Transform anchor = destinationZone.CardPresentationAnchor;
            return SummonMethodParticleVfx.PlayForCurrentQuality(
                       anchor,
                       summonFrame) != null;
        }

        private IEnumerator AnimateSummonMethodArrivalEffect(
            MonsterFrameKind summonFrame,
            Vector2 destination)
        {
            GameObject root = CreateTransitionContainer(
                $"Impacto de Invocação {summonFrame}",
                typeof(CanvasRenderer),
                typeof(SummonMethodVfxGraphic));
            RectTransform rect = root.GetComponent<RectTransform>();
            float viewportScale = Mathf.Clamp(
                frame.rect.height / 1080f,
                0.72f,
                1.10f);
            float diameter = (Application.isMobilePlatform ? 430f : 520f) *
                             viewportScale;
            rect.sizeDelta = Vector2.one * diameter;
            rect.anchoredPosition = destination;
            rect.localScale = Vector3.one * 0.44f;

            Color primary = SummonMethodVfxPalette.Primary(summonFrame);
            Color secondary = SummonMethodVfxPalette.Secondary(summonFrame);
            SummonMethodVfxGraphic graphic =
                root.GetComponent<SummonMethodVfxGraphic>();
            graphic.Configure(
                summonFrame,
                primary,
                secondary,
                ArcaneGraphicsPreferences.Quality <=
                ArcaneGraphicsQuality.Low);

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            const float duration = 0.86f;
            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float appear = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0f, 0.12f, t));
                float disappear = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.66f, 1f, t));
                group.alpha = appear * disappear;
                rect.localScale = Vector3.one * Mathf.Lerp(
                    0.44f,
                    1.18f,
                    TransitionEaseOutCubic(t));
                graphic.SetAnimation(t, elapsed);
                yield return null;
            }
            if (root != null)
                Destroy(root);
        }

        private Sprite SummonEffectSprite(MonsterSummonArrivalEffect effect)
        {
            switch (effect)
            {
                case MonsterSummonArrivalEffect.Yellow:
                    summonEffectYellow ??= Resources.Load<Sprite>(
                        "Duel/SummonEffects/EffectYellow");
                    return summonEffectYellow;
                case MonsterSummonArrivalEffect.Blue:
                    summonEffectBlue ??= Resources.Load<Sprite>(
                        "Duel/SummonEffects/EffectBlue");
                    return summonEffectBlue;
                case MonsterSummonArrivalEffect.Purple:
                    summonEffectPurple ??= Resources.Load<Sprite>(
                        "Duel/SummonEffects/EffectPurple");
                    return summonEffectPurple;
                default:
                    return null;
            }
        }

        private IEnumerator AnimateMonsterSummonArrivalEffect(
            Sprite sprite,
            MonsterSummonArrivalEffect effect,
            Vector2 destination)
        {
            string colorName = effect switch
            {
                MonsterSummonArrivalEffect.Yellow => "Amarelo",
                MonsterSummonArrivalEffect.Blue => "Azul",
                MonsterSummonArrivalEffect.Purple => "Roxo",
                _ => ""
            };
            GameObject root = CreateTransitionContainer(
                $"Efeito de Invocacao {colorName}",
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            float viewportScale = frame == null
                ? 1f
                : Mathf.Clamp(frame.rect.height / 1080f, 0.72f, 1.10f);
            rect.sizeDelta = Vector2.one * 390f * viewportScale;
            rect.anchoredPosition = destination;
            rect.localScale = Vector3.one * 0.48f;
            rect.localRotation = Quaternion.Euler(0f, 0f, -7f);

            Image image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            const float duration = 0.68f;
            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float appear = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0f, 0.16f, t));
                float disappear = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.34f, 1f, t));
                group.alpha = appear * disappear * 0.94f;
                float scale = Mathf.Lerp(
                    0.48f,
                    1.10f,
                    TransitionEaseOutCubic(t));
                rect.localScale = Vector3.one * scale;
                rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(-7f, 3f, t));
                yield return null;
            }
            if (root != null)
                Destroy(root);
        }
    }
}
