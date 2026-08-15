using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    /// <summary>
    /// Visual-only opening played after the Core has produced the initial
    /// field. It temporarily hides that field while decks and opening hands
    /// are presented, and never submits a Core response.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private sealed class OpeningDeckPose
        {
            public Transform Transform;
            public Vector3 Scale;
        }

        private Coroutine openingDuelRoutine;
        private int activeOpeningCards;

        public void StartOpeningDuelPresentation()
        {
            if (!isActiveAndEnabled || frame == null || state == null)
                return;
            if (openingDuelRoutine != null)
                return;
            openingDuelRoutine = StartCoroutine(PlayOpeningDuelPresentation());
        }

        private IEnumerator PlayOpeningDuelPresentation()
        {
            criticalInteractionLocked = true;
            core?.SetPresentationDecisionLocked(true);
            ClearZoneHighlights();
            CloseChoiceModal();
            HideCompactResponseBar();
            CloseCardDetails();

            foreach (CardView card in handViews.Where(card => card != null))
                card.SetPresentationVisible(false);
            CanvasGroup opponentHandGroup = null;
            if (opponentHandFan != null)
            {
                opponentHandGroup = opponentHandFan.GetComponent<CanvasGroup>() ??
                                    opponentHandFan.AddComponent<CanvasGroup>();
                opponentHandGroup.alpha = 0f;
                opponentHandGroup.blocksRaycasts = false;
            }

            MasterDuelArena3D arena3D =
                FindAnyObjectByType<MasterDuelArena3D>();
            var decks = new List<OpeningDeckPose>();
            AddOpeningDeck(
                decks,
                arena3D?.GetMainDeckTransform(DuelPlayerSide.PlayerOne));
            AddOpeningDeck(
                decks,
                arena3D?.GetExtraDeckTransform(DuelPlayerSide.PlayerOne));
            AddOpeningDeck(
                decks,
                arena3D?.GetMainDeckTransform(DuelPlayerSide.PlayerTwo));
            AddOpeningDeck(
                decks,
                arena3D?.GetExtraDeckTransform(DuelPlayerSide.PlayerTwo));
            yield return AnimateOpeningDecks(decks);

            SetStatus("MÃOS INICIAIS · DISTRIBUINDO CARTAS", Cyan);
            int count = Mathf.Min(5, handViews.Count);
            for (int index = 0; index < count; index++)
            {
                StartOpeningDealCard(
                    arena3D,
                    DuelPlayerSide.PlayerTwo,
                    cardBackSprite,
                    OpeningOpponentHandDestination(index, count));
                yield return new WaitForSecondsRealtime(0.055f);
                CardView localCard = handViews[index];
                StartOpeningDealCard(
                    arena3D,
                    DuelPlayerSide.PlayerOne,
                    localCard?.Artwork ?? cardBackSprite,
                    localCard?.Rect != null
                        ? RectCenterScreen(localCard.Rect)
                        : handRoot != null
                            ? RectCenterScreen(handRoot)
                            : new Vector2(
                                Screen.width * 0.5f,
                                Screen.height * 0.08f));
                yield return new WaitForSecondsRealtime(0.075f);
            }
            float dealDeadline = Time.realtimeSinceStartup + 1.2f;
            while (activeOpeningCards > 0 &&
                   Time.realtimeSinceStartup < dealDeadline)
            {
                yield return null;
            }

            foreach (CardView card in handViews.Where(card => card != null))
                card.SetPresentationVisible(true);
            if (opponentHandGroup != null)
                opponentHandGroup.alpha = 1f;

            yield return PlayDuelTitleSweep();
            RestoreOpeningDecks(decks);
            criticalInteractionLocked = false;
            bool keepNetworkLock = DuelOnlineSession.Instance
                ?.RequiresPresentationLock == true;
            core?.SetPresentationDecisionLocked(keepNetworkLock);
            openingDuelRoutine = null;
            RefreshEverything(true);
            SetStatus("DUELO INICIADO", Lime);
        }

        private static void AddOpeningDeck(
            ICollection<OpeningDeckPose> decks,
            Transform deck)
        {
            if (deck == null)
                return;
            decks.Add(new OpeningDeckPose
            {
                Transform = deck,
                Scale = deck.localScale
            });
            deck.localScale = Vector3.zero;
        }

        private IEnumerator AnimateOpeningDecks(
            IReadOnlyList<OpeningDeckPose> decks)
        {
            var pulses = new List<GameObject>();
            if (cardBackSprite != null)
            {
                foreach (OpeningDeckPose pose in decks)
                {
                    if (pose?.Transform == null ||
                        !TryScreenToFrameLocal(
                            WorldScreenPoint(pose.Transform.position),
                            out Vector2 pulsePosition))
                    {
                        continue;
                    }
                    pulses.Add(CreateTransitionPulse(
                        cardBackSprite,
                        pulsePosition,
                        Cyan));
                }
            }
            float startedAt = Time.realtimeSinceStartup;
            const float duration = 0.44f;
            while (Time.realtimeSinceStartup - startedAt < duration)
            {
                float t = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) / duration);
                float overshoot = t < 0.72f
                    ? Mathf.Lerp(0f, 1.10f, TransitionEaseOutCubic(t / 0.72f))
                    : Mathf.Lerp(1.10f, 1f, Mathf.SmoothStep(
                        0f,
                        1f,
                        (t - 0.72f) / 0.28f));
                foreach (OpeningDeckPose pose in decks)
                {
                    if (pose?.Transform != null)
                        pose.Transform.localScale = pose.Scale * overshoot;
                }
                foreach (GameObject pulse in pulses)
                    UpdateTransitionPulse(pulse, t, 0f);
                yield return null;
            }
            RestoreOpeningDecks(decks);
            foreach (GameObject pulse in pulses)
            {
                if (pulse != null)
                    Destroy(pulse);
            }
            yield return new WaitForSecondsRealtime(0.08f);
        }

        private static void RestoreOpeningDecks(
            IReadOnlyList<OpeningDeckPose> decks)
        {
            if (decks == null)
                return;
            foreach (OpeningDeckPose pose in decks)
            {
                if (pose?.Transform != null)
                    pose.Transform.localScale = pose.Scale;
            }
        }

        private void StartOpeningDealCard(
            MasterDuelArena3D arena3D,
            DuelPlayerSide side,
            Sprite sprite,
            Vector2 destinationScreen)
        {
            Transform deck = arena3D?.GetMainDeckTransform(side);
            if (deck == null || sprite == null ||
                !TryScreenToFrameLocal(
                    WorldScreenPoint(deck.position + deck.up * 0.62f),
                    out Vector2 start) ||
                !TryScreenToFrameLocal(destinationScreen, out Vector2 end))
            {
                return;
            }
            activeOpeningCards++;
            StartCoroutine(AnimateOpeningDealCard(sprite, start, end));
        }

        private IEnumerator AnimateOpeningDealCard(
            Sprite sprite,
            Vector2 start,
            Vector2 end)
        {
            GameObject card = CreateTransitionCard(sprite, start);
            card.name = "Carta da Mão Inicial";
            RectTransform rect = card.GetComponent<RectTransform>();
            CanvasGroup cardGroup = card.GetComponent<CanvasGroup>();
            float startedAt = Time.realtimeSinceStartup;
            const float duration = 0.34f;
            while (card != null &&
                   Time.realtimeSinceStartup - startedAt < duration)
            {
                float t = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) / duration);
                float eased = TransitionEaseOutCubic(t);
                rect.anchoredPosition = Vector2.Lerp(start, end, eased) +
                                        Vector2.up * Mathf.Sin(t * Mathf.PI) * 42f;
                rect.localScale = Vector3.one * Mathf.Lerp(0.76f, 1f, eased);
                rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(-7f, 0f, eased));
                cardGroup.alpha = t < 0.88f
                    ? 1f
                    : 1f - Mathf.SmoothStep(0.88f, 1f, t);
                yield return null;
            }
            if (card != null)
                Destroy(card);
            activeOpeningCards = Mathf.Max(0, activeOpeningCards - 1);
        }

        private Vector2 OpeningOpponentHandDestination(int index, int count)
        {
            RectTransform root = opponentHandContent != null
                ? opponentHandContent
                : opponentHandFan?.GetComponent<RectTransform>();
            if (root == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.92f);
            Vector2 center = RectCenterScreen(root);
            float spacing = Mathf.Min(48f, Screen.width * 0.026f);
            return center + new Vector2(
                (index - (count - 1) * 0.5f) * spacing,
                -Mathf.Abs(index - (count - 1) * 0.5f) * 2f);
        }

        private IEnumerator PlayDuelTitleSweep()
        {
            GameObject root = CreateTransitionContainer("Abertura · DUEL");
            root.transform.SetAsLastSibling();
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = frame != null
                ? frame.rect.size
                : new Vector2(1920f, 1080f);
            CanvasGroup titleGroup = root.GetComponent<CanvasGroup>();
            Text title = CreateText(
                root.transform,
                "D U E L",
                190,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.05f, 0.30f),
                new Vector2(0.95f, 0.70f),
                TextAnchor.MiddleCenter);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform titleRect = title.rectTransform;
            float startedAt = Time.realtimeSinceStartup;
            const float duration = 0.82f;
            while (root != null &&
                   Time.realtimeSinceStartup - startedAt < duration)
            {
                float t = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) / duration);
                titleRect.localScale = Vector3.one * Mathf.Lerp(
                    1.45f,
                    1f,
                    TransitionEaseOutCubic(t));
                titleGroup.alpha = t < 0.20f
                    ? Mathf.SmoothStep(0f, 1f, t / 0.20f)
                    : t > 0.70f
                        ? 1f - Mathf.SmoothStep(0f, 1f, (t - 0.70f) / 0.30f)
                        : 1f;
                yield return null;
            }
            if (root != null)
                Destroy(root);
        }
    }
}
