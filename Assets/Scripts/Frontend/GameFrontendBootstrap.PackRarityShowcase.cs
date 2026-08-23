using System.Collections;
using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private bool _premiumRarityShowcaseActive;

        public bool IsPremiumRarityShowcaseActive =>
            _premiumRarityShowcaseActive;

        /// <summary>
        /// Apresentacao cinematografica exclusiva de SR/UR. A carta exibida
        /// aqui e uma copia visual: o resultado verdadeiro ja foi persistido
        /// pelo repositorio antes de a animacao comecar.
        /// </summary>
        private IEnumerator PlayPremiumRarityRevealShowcase(
            Sprite artwork,
            string displayName,
            CardRarity rarity)
        {
            if (_screenRoot == null || artwork == null ||
                rarity < CardRarity.SR)
            {
                yield break;
            }

            _premiumRarityShowcaseActive = true;
            Color accent = ResolvePackOpeningRarityAccent(rarity);
            Color secondary = rarity == CardRarity.UR
                ? new Color(0.18f, 0.92f, 1f, 1f)
                : new Color(1f, 0.94f, 0.55f, 1f);

            Image blocker = CreatePanel(
                _screenRoot,
                rarity == CardRarity.UR
                    ? "Apresentacao Ultra Rara"
                    : "Apresentacao Super Rara",
                Vector2.zero,
                Vector2.one,
                new Color(0.004f, 0.008f, 0.025f, 0.82f));
            blocker.raycastTarget = true;
            blocker.transform.SetAsLastSibling();
            CanvasGroup layerGroup = blocker.gameObject.AddComponent<CanvasGroup>();
            layerGroup.alpha = 0f;

            ArcaneRarityRevealGraphic aura = CreatePackRarityAura(
                blocker.transform,
                $"Campo de Energia {rarity}",
                new Vector2(0.08f, 0.02f),
                new Vector2(0.92f, 0.98f),
                rarity,
                false);

            Image horizonSoft = CreatePanel(
                blocker.transform,
                "Horizonte de Energia Suave",
                new Vector2(0.04f, 0.475f),
                new Vector2(0.96f, 0.535f),
                new Color(accent.r, accent.g, accent.b, 0f));
            horizonSoft.sprite = ResolvePackOpeningGlowSprite();
            horizonSoft.raycastTarget = false;
            horizonSoft.preserveAspect = false;

            Image horizonCore = CreatePanel(
                blocker.transform,
                "Nucleo do Horizonte",
                new Vector2(0.07f, 0.502f),
                new Vector2(0.93f, 0.508f),
                new Color(secondary.r, secondary.g, secondary.b, 0f));
            horizonCore.raycastTarget = false;

            Image card = CreateCardArtwork(
                blocker.transform,
                artwork,
                new Vector2(0.395f, 0.18f),
                new Vector2(0.605f, 0.79f),
                0f,
                false);
            card.gameObject.name = $"Carta em Destaque {rarity}";
            card.raycastTarget = false;
            card.preserveAspect = true;
            AddOutline(
                card.gameObject,
                new Color(accent.r, accent.g, accent.b, 0.96f),
                rarity == CardRarity.UR
                    ? new Vector2(4f, -4f)
                    : new Vector2(3f, -3f));
            AddOutline(
                card.gameObject,
                new Color(secondary.r, secondary.g, secondary.b, 0.72f),
                new Vector2(-2f, 2f));
            ArcaneRarityCardFrameGraphic cardFrame = CreateRarityCardFrame(
                blocker.transform,
                $"Moldura {rarity} da Carta em Destaque",
                new Vector2(0.386f, 0.166f),
                new Vector2(0.614f, 0.804f),
                rarity,
                false);

            Text rarityTitle = CreateText(
                blocker.transform,
                rarity == CardRarity.UR ? "ULTRA RARA" : "SUPER RARA",
                rarity == CardRarity.UR ? 34 : 30,
                FontStyle.Bold,
                secondary,
                new Vector2(0.22f, 0.82f),
                new Vector2(0.78f, 0.90f),
                TextAnchor.MiddleCenter);
            Text cardName = CreateText(
                blocker.transform,
                string.IsNullOrWhiteSpace(displayName)
                    ? "CARTA ADQUIRIDA"
                    : displayName,
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.19f, 0.10f),
                new Vector2(0.81f, 0.17f),
                TextAnchor.MiddleCenter);
            CanvasGroup titleGroup = rarityTitle.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup nameGroup = cardName.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            nameGroup.alpha = 0f;

            RectTransform cardRect = card.rectTransform;
            Vector2 cardBasePosition = cardRect.anchoredPosition;
            float duration = rarity == CardRarity.UR ? 2.15f : 1.55f;
            float elapsed = 0f;
            while (elapsed < duration && blocker != null && card != null)
            {
                elapsed += Mathf.Min(
                    Mathf.Max(0f, Time.unscaledDeltaTime),
                    1f / 30f);
                float progress = Mathf.Clamp01(elapsed / duration);

                float enter = EaseOutQuint(Mathf.Clamp01(progress / 0.22f));
                float charge = EaseInOutSine(Mathf.Clamp01(
                    (progress - 0.14f) / 0.34f));
                float exit = EaseInCubic(Mathf.Clamp01(
                    (progress - 0.86f) / 0.14f));
                float visibility = Mathf.Clamp01(enter * (1f - exit));
                layerGroup.alpha = visibility;

                float impactTime = Mathf.Clamp01(
                    (progress - 0.36f) / 0.32f);
                float impactEnvelope = impactTime > 0f && impactTime < 1f
                    ? Mathf.Sin(impactTime * Mathf.PI) *
                      (1f - impactTime) * (1f - impactTime)
                    : 0f;
                float shake = Mathf.Sin(impactTime * Mathf.PI *
                    (rarity == CardRarity.UR ? 13f : 9f)) * impactEnvelope;

                cardRect.anchoredPosition = cardBasePosition + new Vector2(
                    shake * (rarity == CardRarity.UR ? 7f : 4f),
                    Mathf.Lerp(-88f, 0f, enter) +
                    Mathf.Sin(progress * Mathf.PI) * 8f - exit * 28f);
                cardRect.localScale = Vector3.one * (
                    Mathf.Lerp(0.48f, 1.04f, enter) +
                    impactEnvelope * (rarity == CardRarity.UR ? 0.085f : 0.05f));
                cardRect.localRotation = Quaternion.Euler(
                    0f,
                    Mathf.Lerp(-16f, 0f, enter),
                    Mathf.Lerp(-7f, 0f, enter) - shake * 0.55f);

                float pulse = 0.5f + 0.5f * Mathf.Sin(
                    elapsed * (rarity == CardRarity.UR ? 11f : 8f));
                aura?.SetState(
                    Mathf.Clamp01(charge * (0.74f + impactEnvelope * 0.55f)),
                    Mathf.Clamp01(pulse + impactEnvelope * 0.42f));
                cardFrame?.SetState(
                    Mathf.Clamp01(charge + impactEnvelope * 0.28f),
                    Mathf.Clamp01(pulse + impactEnvelope * 0.46f));

                float horizon = Mathf.Clamp01(charge * (1f - exit));
                horizonSoft.color = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    horizon * (rarity == CardRarity.UR ? 0.62f : 0.48f));
                horizonSoft.rectTransform.localScale = new Vector3(
                    Mathf.Lerp(0.02f, 1f, EaseOutQuint(horizon)),
                    Mathf.Lerp(0.42f, 1.18f, pulse),
                    1f);
                horizonCore.color = new Color(
                    secondary.r,
                    secondary.g,
                    secondary.b,
                    horizon * 0.94f);
                horizonCore.rectTransform.localScale = new Vector3(
                    Mathf.Lerp(0.01f, 1f, EaseOutQuint(horizon)),
                    1f + impactEnvelope * 1.7f,
                    1f);

                titleGroup.alpha = Mathf.Clamp01(
                    (progress - 0.28f) / 0.15f) * (1f - exit);
                nameGroup.alpha = Mathf.Clamp01(
                    (progress - 0.43f) / 0.14f) * (1f - exit);
                yield return null;
            }

            _premiumRarityShowcaseActive = false;
            if (blocker != null)
                Destroy(blocker.gameObject);
        }

        private static ArcaneRarityCardFrameGraphic CreateRarityCardFrame(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            CardRarity rarity,
            bool animateIdle)
        {
            if (parent == null || rarity < CardRarity.SR)
                return null;

            var frameObject = new GameObject(objectName, typeof(RectTransform));
            frameObject.transform.SetParent(parent, false);
            RectTransform rect = frameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            var frame = frameObject.AddComponent<ArcaneRarityCardFrameGraphic>();
            frame.Configure(rarity, animateIdle);
            return frame;
        }
    }
}
