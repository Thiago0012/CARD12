using System.Collections;
using ArcaneArena.Frontend;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArcaneArena
{
    public static class DuelRankMasteryPresentationRules
    {
        public const float SpinDuration = 1f;
        public const float TotalDuration = 3.6f;

        public static float ResolveBadgeSize(float profileIconHeight)
        {
            return Mathf.Clamp(profileIconHeight * 1.12f, 104f, 148f);
        }

        public static float ResolveSpinDegrees(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / SpinDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            // Três voltas completas: a animação continua rápida, mas termina
            // exatamente na orientação oficial do emblema. -900° encerrava
            // meia volta adiante e deixava o elo de cabeça para baixo.
            return -1080f * eased;
        }

        public static float ResolveOpacity(float elapsed)
        {
            if (elapsed < 0f || elapsed >= TotalDuration) return 0f;
            if (elapsed < 0.16f)
                return Mathf.SmoothStep(0f, 1f, elapsed / 0.16f);
            if (elapsed > 3f)
                return 1f - Mathf.SmoothStep(0f, 1f,
                    (elapsed - 3f) / (TotalDuration - 3f));
            return 1f;
        }
    }

    public sealed partial class CardArenaBootstrap
    {
        private GameObject rankMasteryRoot;
        private Coroutine rankMasteryRoutine;
        private int rankAnimationPreviewKeyPresses;
        private float lastRankAnimationPreviewKeyTime = -100f;
        private OnlineDuelResultPresenter rankAnimationPreviewPresenter;

        private void UpdateRankMasteryShortcut()
        {
            UpdateRankAnimationPreviewShortcut();
            if (Mouse.current?.middleButton.wasPressedThisFrame != true ||
                frame == null || localLifePanel == null ||
                localDuelIdentity == null || core == null || core.IsFinished ||
                state == null)
            {
                return;
            }

            StartRankMasteryPresentation();
        }

        private void UpdateRankAnimationPreviewShortcut()
        {
            Keyboard keyboard = Keyboard.current;
            bool online = DuelOnlineSession.Instance
                ?.IsOnlineDuelActive == true;
            if (keyboard == null || core == null || core.IsFinished || online)
            {
                rankAnimationPreviewKeyPresses = 0;
                return;
            }
            if (keyboard.lKey.wasPressedThisFrame != true)
                return;

            float now = Time.unscaledTime;
            if (now - lastRankAnimationPreviewKeyTime > 2f)
                rankAnimationPreviewKeyPresses = 0;
            lastRankAnimationPreviewKeyTime = now;
            rankAnimationPreviewKeyPresses++;
            if (rankAnimationPreviewKeyPresses < 5)
                return;
            rankAnimationPreviewKeyPresses = 0;

            if (InteractionLocked)
            {
                SetStatus(
                    "Aguarde a decisão atual terminar para visualizar os elos.",
                    Muted);
                return;
            }
            rankAnimationPreviewPresenter ??=
                GetComponent<OnlineDuelResultPresenter>() ??
                gameObject.AddComponent<OnlineDuelResultPresenter>();
            if (rankAnimationPreviewPresenter
                .IsDevelopmentPreviewPlaying)
                return;

            criticalInteractionLocked = true;
            core.SetPresentationDecisionLocked(true);
            rankAnimationPreviewPresenter.PlayDevelopmentRankShowcase(
                CompleteRankAnimationPreview);
        }

        private void CompleteRankAnimationPreview()
        {
            criticalInteractionLocked = false;
            core?.SetPresentationDecisionLocked(false);
            ResetPromptPresentationIdentity();
            observedPrompt = null;
            if (presentationReady)
                RefreshEverything(true);
        }

        private void StartRankMasteryPresentation()
        {
            StopRankMasteryPresentation();

            RectTransform profileIcon = localLifePanel.transform
                .Find("Ícone do Perfil") as RectTransform;
            RectTransform plate = localLifePanel.transform as RectTransform;
            RectTransform reference = profileIcon != null ? profileIcon : plate;
            if (reference == null || frame == null)
                return;

            Vector3[] corners = new Vector3[4];
            reference.GetWorldCorners(corners);
            Vector2 bottomLeft = frame.InverseTransformPoint(corners[0]);
            Vector2 topRight = frame.InverseTransformPoint(corners[2]);
            float iconHeight = Mathf.Max(1f, topRight.y - bottomLeft.y);
            float badgeSize = DuelRankMasteryPresentationRules
                .ResolveBadgeSize(iconHeight);
            float burstSize = badgeSize * 2.05f;
            Vector2 iconCenter = (bottomLeft + topRight) * 0.5f;
            Vector2 targetCenter = new(
                iconCenter.x,
                topRight.y + badgeSize * 0.5f + Mathf.Max(8f, iconHeight * 0.08f));
            Vector2 anchorReference = new(
                frame.rect.xMin + frame.rect.width * 0.5f,
                frame.rect.yMin + frame.rect.height * 0.5f);

            rankMasteryRoot = new GameObject(
                "Maestria de Elo Local",
                typeof(RectTransform),
                typeof(CanvasGroup));
            rankMasteryRoot.transform.SetParent(frame, false);
            RectTransform rootRect = rankMasteryRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(burstSize, burstSize);
            rootRect.anchoredPosition = targetCenter - anchorReference;

            Color accent = RankMasteryAccent(localDuelIdentity.rankTier);
            GameObject burstObject = new(
                "Fogos e Pulsações do Elo",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelRankMasteryBurstGraphic));
            burstObject.transform.SetParent(rankMasteryRoot.transform, false);
            RectTransform burstRect = burstObject.GetComponent<RectTransform>();
            StretchMasteryRect(burstRect);
            DuelRankMasteryBurstGraphic burst =
                burstObject.GetComponent<DuelRankMasteryBurstGraphic>();
            burst.raycastTarget = false;

            Image halo = CreateMasteryImage(
                rankMasteryRoot.transform,
                "Halo do Elo",
                RankBadgeCatalog.Get(localDuelIdentity.rankTier),
                badgeSize * 1.28f,
                new Color(accent.r, accent.g, accent.b, 0.22f));
            Image badge = CreateMasteryImage(
                rankMasteryRoot.transform,
                "Elo Atual",
                RankBadgeCatalog.Get(localDuelIdentity.rankTier),
                badgeSize,
                Color.white);
            halo.transform.SetAsLastSibling();
            badge.transform.SetAsLastSibling();

            Text label = CreateMasteryLabel(
                rankMasteryRoot.transform,
                RankRules.DisplayName(localDuelIdentity.rankTier),
                accent,
                badgeSize);
            label.transform.SetAsLastSibling();
            rankMasteryRoot.transform.SetAsLastSibling();

            rankMasteryRoutine = StartCoroutine(PlayRankMasteryPresentation(
                rootRect,
                rankMasteryRoot.GetComponent<CanvasGroup>(),
                burst,
                halo.rectTransform,
                badge.rectTransform,
                label,
                accent));
        }

        private IEnumerator PlayRankMasteryPresentation(
            RectTransform root,
            CanvasGroup group,
            DuelRankMasteryBurstGraphic burst,
            RectTransform halo,
            RectTransform badge,
            Text label,
            Color accent)
        {
            Vector2 origin = root.anchoredPosition;
            for (float elapsed = 0f;
                 elapsed < DuelRankMasteryPresentationRules.TotalDuration &&
                 root != null;
                 elapsed += Time.unscaledDeltaTime)
            {
                float intro = Mathf.Clamp01(
                    elapsed / DuelRankMasteryPresentationRules.SpinDuration);
                float easedIntro = 1f - Mathf.Pow(1f - intro, 3f);
                float reveal = Mathf.Clamp01((elapsed - 0.66f) / 0.48f);
                float pulse = reveal <= 0f
                    ? 0f
                    : Mathf.Repeat((elapsed - 0.76f) / 0.78f, 1f);
                float settle = intro < 1f
                    ? 1f + Mathf.Sin(intro * Mathf.PI) * 0.12f
                    : 1f + Mathf.Sin((elapsed - 1f) * 3.8f) * 0.025f;
                float scale = Mathf.LerpUnclamped(0.28f, settle, easedIntro);

                group.alpha = DuelRankMasteryPresentationRules.ResolveOpacity(
                    elapsed);
                group.interactable = false;
                group.blocksRaycasts = false;
                root.anchoredPosition = origin + Vector2.up *
                    (Mathf.SmoothStep(0f, 11f, intro) +
                     Mathf.Sin(Mathf.Max(0f, elapsed - 1f) * 2.5f) * 3.5f);
                badge.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    DuelRankMasteryPresentationRules.ResolveSpinDegrees(elapsed));
                badge.localScale = Vector3.one * scale;
                halo.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    -DuelRankMasteryPresentationRules.ResolveSpinDegrees(elapsed) *
                    0.34f);
                halo.localScale = Vector3.one *
                    (scale * (1.04f + Mathf.Sin(elapsed * 7f) * 0.04f));
                Color labelColor = accent;
                labelColor.a = Mathf.Clamp01(reveal * group.alpha);
                label.color = labelColor;
                burst.SetAnimation(accent, reveal, pulse, elapsed);
                yield return null;
            }

            if (rankMasteryRoot != null)
                Destroy(rankMasteryRoot);
            rankMasteryRoot = null;
            rankMasteryRoutine = null;
        }

        private void StopRankMasteryPresentation()
        {
            if (rankMasteryRoutine != null)
            {
                StopCoroutine(rankMasteryRoutine);
                rankMasteryRoutine = null;
            }
            if (rankMasteryRoot != null)
                Destroy(rankMasteryRoot);
            rankMasteryRoot = null;
        }

        private static Image CreateMasteryImage(
            Transform parent,
            string objectName,
            Sprite sprite,
            float size,
            Color tint)
        {
            GameObject imageObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = tint;
            return image;
        }

        private static Text CreateMasteryLabel(
            Transform parent,
            string rankName,
            Color accent,
            float badgeSize)
        {
            GameObject textObject = new(
                "Nome do Elo Atual",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(badgeSize * 1.8f, 30f);
            rect.anchoredPosition = new Vector2(0f, -badgeSize * 0.54f);
            Text text = textObject.GetComponent<Text>();
            text.font = MasterDuelTypography.Resolve(FontStyle.Bold, 14);
            text.fontSize = 14;
            text.fontStyle = FontStyle.Bold;
            text.text = rankName?.ToUpperInvariant() ?? "ELO ATUAL";
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = new Color(accent.r, accent.g, accent.b, 0f);
            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0.02f, 0.05f, 0.96f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
            return text;
        }

        private static void StretchMasteryRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color RankMasteryAccent(RankTier tier)
        {
            return tier switch
            {
                RankTier.Wood => new Color(0.72f, 0.43f, 0.20f, 1f),
                RankTier.Stone => new Color(0.62f, 0.72f, 0.80f, 1f),
                RankTier.Iron => new Color(0.50f, 0.64f, 0.77f, 1f),
                RankTier.Bronze => new Color(0.95f, 0.50f, 0.20f, 1f),
                RankTier.Silver => new Color(0.78f, 0.91f, 1f, 1f),
                RankTier.Gold => new Color(1f, 0.77f, 0.25f, 1f),
                RankTier.Platinum => new Color(0.31f, 1f, 0.87f, 1f),
                RankTier.Diamond => new Color(0.38f, 0.76f, 1f, 1f),
                RankTier.GrandMaster => new Color(1f, 0.34f, 0.82f, 1f),
                _ => Cyan
            };
        }
    }
}
