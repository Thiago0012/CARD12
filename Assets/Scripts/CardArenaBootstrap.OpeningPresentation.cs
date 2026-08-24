using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Multiplayer;
using ArcaneArena.Frontend;
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
            // Nothing from the persistent transition canvas may remain behind
            // the letterboxed arena once the authored opening takes control.
            FindAnyObjectByType<OnlineLoadingScreenPresenter>(
                    FindObjectsInactive.Include)
                ?.HideImmediately();
            // Start the duel soundtrack with the opening itself. The music
            // controller owns the one-second fade and prevents duplicate play.
            DuelMusicController.BeginDuelPlayback();
            SuspendAnnouncementsForOpening();
            HideDecisionRibbon();
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
                opponentHandGroup =
                    opponentHandFan.GetComponent<CanvasGroup>();
                if (opponentHandGroup == null)
                    opponentHandGroup =
                        opponentHandFan.AddComponent<CanvasGroup>();
                if (opponentHandGroup != null)
                {
                    opponentHandGroup.alpha = 0f;
                    opponentHandGroup.blocksRaycasts = false;
                }
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
            if (count > 0)
            {
                cardAudioDirector ??=
                    GetComponent<ArcaneDuel.Game.ArcaneAudioDirector>();
                cardAudioDirector?.PlayRapidCardCues(
                    ArcaneDuel.Game.ArcaneCardSound.Draw,
                    count * 2,
                    0.125f);
            }
            for (int index = 0; index < count; index++)
            {
                StartOpeningDealCard(
                    arena3D,
                    DuelPlayerSide.PlayerTwo,
                    cardBackSprite,
                    OpeningOpponentHandDestination(index, count),
                    null);
                yield return new WaitForSecondsRealtime(0.11f);
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
                                Screen.height * 0.08f),
                    localCard);
                yield return new WaitForSecondsRealtime(0.14f);
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
            ResumeAnnouncementsAfterOpening();
            RefreshEverything(true);
            HideDecisionRibbon();
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
            Vector2 destinationScreen,
            CardView revealOnArrival)
        {
            Transform deck = arena3D?.GetMainDeckTransform(side);
            if (deck == null || sprite == null ||
                !TryScreenToFrameLocal(
                    WorldScreenPoint(deck.position + deck.up * 0.62f),
                    out Vector2 start) ||
                !TryScreenToFrameLocal(destinationScreen, out Vector2 end))
            {
                revealOnArrival?.SetPresentationVisible(true);
                return;
            }
            activeOpeningCards++;
            StartCoroutine(AnimateOpeningDealCard(
                sprite,
                start,
                end,
                revealOnArrival));
        }

        private IEnumerator AnimateOpeningDealCard(
            Sprite sprite,
            Vector2 start,
            Vector2 end,
            CardView revealOnArrival)
        {
            GameObject card = CreateTransitionCard(sprite, start);
            card.name = "Carta da Mão Inicial";
            RectTransform rect = card.GetComponent<RectTransform>();
            CanvasGroup cardGroup = card.GetComponent<CanvasGroup>();
            float startedAt = Time.realtimeSinceStartup;
            const float duration = 0.34f;
            bool arrived = false;
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
                // At t=0.68 the eased card is already 96.7% of the way to
                // its slot. Reveal the real hand view there so low frame
                // rates cannot produce an empty frame between both visuals.
                if (!arrived && t >= 0.68f)
                {
                    arrived = true;
                    revealOnArrival?.SetPresentationVisible(true);
                }
                cardGroup.alpha = t < 0.68f
                    ? 1f
                    : 1f - Mathf.SmoothStep(0.68f, 1f, t);
                yield return null;
            }
            revealOnArrival?.SetPresentationVisible(true);
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
                260,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.02f, 0.22f),
                new Vector2(0.98f, 0.78f),
                TextAnchor.MiddleCenter);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform titleRect = title.rectTransform;
            float startedAt = Time.realtimeSinceStartup;
            const float duration = 0.92f;
            while (root != null &&
                   Time.realtimeSinceStartup - startedAt < duration)
            {
                float t = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) / duration);
                titleRect.localScale = Vector3.one * Mathf.Lerp(
                    1.58f,
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
