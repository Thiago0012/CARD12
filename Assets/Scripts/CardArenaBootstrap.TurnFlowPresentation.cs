using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;

namespace ArcaneArena
{
    /// <summary>
    /// Serializes turn/phase presentation without changing the authoritative
    /// state already produced by ygopro-core.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private sealed class DrawPresentationRequest
        {
            public byte Player;
            public uint[] Codes;
            public CardView[] DrawnCards;
        }

        private const float DrawClickTimeoutSeconds = 5f;
        private readonly Queue<DuelEvent> deferredBattlePresentations = new();

        private bool phasePresentationLocked;
        private bool awaitingDrawDeckClick;
        private bool drawDeckClickRequested;
        private bool replayingDeferredPresentation;
        private uint? presentationPhaseOverride;
        private DuelPlayerSide drawInteractionSide;
        private DuelZone3D activeDrawDeckZone;
        private DrawPresentationRequest activeDrawRequest;
        private Transform activeDrawDeck;
        private Vector3 activeDrawDeckPosition;
        private Quaternion activeDrawDeckRotation;
        private Vector3 activeDrawDeckScale;
        private GameObject activeDrawCard;

        private void PrepareTurnFlowPresentation(DuelEvent duelEvent)
        {
            if (duelEvent == null)
                return;

            bool startsPresentation =
                duelEvent.Message == CoreMessage.NewTurn ||
                duelEvent.Message == CoreMessage.NewPhase ||
                IsTurnDrawEvent(duelEvent);
            if (startsPresentation)
                BeginTurnFlowPresentation();
        }

        private bool IsTurnDrawEvent(DuelEvent duelEvent)
        {
            return duelEvent != null &&
                   duelEvent.Message == CoreMessage.Draw &&
                   duelEvent.Codes != null &&
                   duelEvent.Codes.Length > 0 &&
                   state != null &&
                   state.TurnNumber > 0 &&
                   (state.Phase & 0x001U) != 0;
        }

        private void BeginTurnFlowPresentation()
        {
            if (phasePresentationLocked)
                return;

            phasePresentationLocked = true;
            if (automaticPromptRoutine != null)
            {
                StopCoroutine(automaticPromptRoutine);
                automaticPromptRoutine = null;
                scheduledAutomaticPrompt = null;
            }
            ClearZoneHighlights();
            CloseChoiceModal();
            CloseZoneBrowser();
            ClosePhaseNavigator();
            CloseCardDetails();
            ClearHandSelection();
            SetHandPlacementMode(false);
            RefreshHandLegalGlows();
            if (phaseButton != null)
                phaseButton.interactable = false;
        }

        private void QueueDrawPresentation(DuelEvent duelEvent)
        {
            if (!IsTurnDrawEvent(duelEvent))
                return;

            BeginTurnFlowPresentation();
            int count = duelEvent.Codes.Length;
            CardView[] drawnCards = System.Array.Empty<CardView>();
            if (duelEvent.Player == 0 && handViews.Count > 0)
            {
                drawnCards = handViews
                    .Skip(Mathf.Max(0, handViews.Count - count))
                    .Where(card => card != null)
                    .ToArray();
                foreach (CardView card in drawnCards)
                    card.SetPresentationVisible(false);
            }

            announcementQueue.Enqueue(
                new ArenaAnnouncement
                {
                    DisplayPhase = 0x001U,
                    TurnFlow = true,
                    Draw = new DrawPresentationRequest
                    {
                        Player = duelEvent.Player,
                        Codes = duelEvent.Codes.ToArray(),
                        DrawnCards = drawnCards
                    }
                });
            if (announcementRoutine == null)
                announcementRoutine = StartCoroutine(PlayAnnouncementQueue());
        }

        private IEnumerator PlayDrawPresentation(
            DrawPresentationRequest request)
        {
            if (request == null)
                yield break;
            activeDrawRequest = request;

            MasterDuelArena3D arena3D =
                FindAnyObjectByType<MasterDuelArena3D>();
            DuelPlayerSide side = request.Player == 0
                ? DuelPlayerSide.PlayerOne
                : DuelPlayerSide.PlayerTwo;
            Transform deck = arena3D?.GetMainDeckTransform(side);
            if (deck == null)
            {
                RevealDrawnCards(request);
                activeDrawRequest = null;
                yield return new WaitForSecondsRealtime(0.15f);
                yield break;
            }

            activeDrawDeck = deck;
            activeDrawDeckZone = deck.GetComponent<DuelZone3D>();
            activeDrawDeckPosition = deck.position;
            activeDrawDeckRotation = deck.rotation;
            activeDrawDeckScale = deck.localScale;

            bool localDraw = request.Player == 0;
            if (localDraw)
            {
                awaitingDrawDeckClick = true;
                drawDeckClickRequested = false;
                drawInteractionSide = side;
                if (activeDrawDeckZone != null)
                    activeDrawDeckZone.SetDropHighlight(true);
                SetStatus(
                    "FASE DE COMPRA · o Deck está se aproximando.",
                    Cyan);
            }

            yield return MoveDrawDeck(
                deck,
                activeDrawDeckPosition,
                arena3D.GetDrawPresentationWorldPosition(side),
                0.36f);

            if (localDraw)
            {
                float elapsed = 0f;
                while (!drawDeckClickRequested &&
                       elapsed < DrawClickTimeoutSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    int remaining = Mathf.Max(
                        1,
                        Mathf.CeilToInt(
                            DrawClickTimeoutSeconds - elapsed));
                    SetStatus(
                        $"FASE DE COMPRA · clique no Deck ({remaining}s)",
                        Cyan);
                    yield return null;
                }

                bool automatic = !drawDeckClickRequested;
                awaitingDrawDeckClick = false;
                if (activeDrawDeckZone != null)
                    activeDrawDeckZone.SetDropHighlight(false);
                SetStatus(
                    automatic
                        ? "COMPRA AUTOMÁTICA · limite de 5 segundos atingido."
                        : "CARTA COMPRADA",
                    automatic ? Gold : Cyan);
                yield return new WaitForSecondsRealtime(0.10f);
            }
            else
            {
                SetStatus("O OPONENTE ESTÁ COMPRANDO UMA CARTA", Gold);
                yield return new WaitForSecondsRealtime(
                    DuelAnimationPreferences.Duration(0.55f));
            }

            uint[] codes = request.Codes ?? System.Array.Empty<uint>();
            if (codes.Length == 0)
            {
                RevealDrawnCards(request);
            }
            else
            {
                for (int index = 0; index < codes.Length; index++)
                {
                    arena3D.NotifyCardDrawn(side);
                    yield return AnimateDrawnCard(
                        codes[index],
                        side,
                        deck.position + deck.up * 0.72f);
                    RevealDrawnCard(request, index);
                }
            }

            yield return MoveDrawDeck(
                deck,
                deck.position,
                activeDrawDeckPosition,
                0.30f);
            RestoreActiveDrawDeck();
            RevealDrawnCards(request);
            activeDrawRequest = null;
        }

        private bool TryHandleDrawDeckClick(DuelZone3D zone)
        {
            if (!awaitingDrawDeckClick || zone == null ||
                zone.Kind != DuelZoneKind.MainDeck ||
                zone.Owner != drawInteractionSide)
            {
                return false;
            }

            drawDeckClickRequested = true;
            SetStatus("COMPRA CONFIRMADA", Cyan);
            return true;
        }

        private IEnumerator MoveDrawDeck(
            Transform deck,
            Vector3 from,
            Vector3 to,
            float baseDuration)
        {
            float duration = DuelAnimationPreferences.Duration(baseDuration);
            if (duration <= 0f)
            {
                deck.position = to;
                yield break;
            }

            for (float elapsed = 0f;
                 elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                deck.position = Vector3.Lerp(from, to, t);
                yield return null;
            }
            deck.position = to;
        }

        private IEnumerator AnimateDrawnCard(
            uint code,
            DuelPlayerSide side,
            Vector3 start)
        {
            var cardObject = new GameObject("Carta comprada · apresentação");
            activeDrawCard = cardObject;
            SpriteRenderer renderer = cardObject.AddComponent<SpriteRenderer>();
            renderer.sprite = side == DuelPlayerSide.PlayerOne
                ? SpriteFor(code)
                : cardBackSprite;
            renderer.sortingOrder = 500;
            cardObject.transform.position = start;

            Camera camera = Camera.main;
            if (camera != null)
            {
                cardObject.transform.rotation = camera.transform.rotation;
                float height = renderer.sprite != null
                    ? Mathf.Max(0.1f, renderer.sprite.bounds.size.y)
                    : 1f;
                cardObject.transform.localScale =
                    Vector3.one * (2.15f / height);
            }

            Vector3 end = DrawCardDestination(camera, side, start);
            float duration = DuelAnimationPreferences.Duration(0.44f);
            for (float elapsed = 0f;
                 elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                Vector3 arc = Vector3.up *
                              (Mathf.Sin(t * Mathf.PI) * 0.72f);
                cardObject.transform.position =
                    Vector3.Lerp(start, end, t) + arc;
                cardObject.transform.localScale *=
                    1f + Time.unscaledDeltaTime * 0.18f;
                yield return null;
            }

            if (cardObject != null)
                Destroy(cardObject);
            activeDrawCard = null;
        }

        private static Vector3 DrawCardDestination(
            Camera camera,
            DuelPlayerSide side,
            Vector3 start)
        {
            if (camera == null)
            {
                return start + new Vector3(
                    0f,
                    1.2f,
                    side == DuelPlayerSide.PlayerOne ? -3f : 3f);
            }

            float depth = Vector3.Dot(
                start - camera.transform.position,
                camera.transform.forward);
            depth = Mathf.Max(2.25f, depth * 0.62f);
            return camera.ViewportToWorldPoint(
                new Vector3(
                    0.5f,
                    side == DuelPlayerSide.PlayerOne ? 0.10f : 0.90f,
                    depth));
        }

        private static void RevealDrawnCard(
            DrawPresentationRequest request,
            int index)
        {
            if (request?.DrawnCards == null ||
                index < 0 || index >= request.DrawnCards.Length)
            {
                return;
            }
            request.DrawnCards[index]?.SetPresentationVisible(true);
        }

        private static void RevealDrawnCards(
            DrawPresentationRequest request)
        {
            if (request?.DrawnCards == null)
                return;
            foreach (CardView card in request.DrawnCards)
                card?.SetPresentationVisible(true);
        }

        private void CompleteTurnFlowPresentation()
        {
            if (!phasePresentationLocked || announcementQueue.Count > 0)
                return;

            ResetTurnFlowPresentation(true);
            observedPrompt = null;
            RefreshEverything(true);
            ReplayDeferredBattlePresentations();
        }

        private void ResetTurnFlowPresentation(bool restoreDeck)
        {
            phasePresentationLocked = false;
            presentationPhaseOverride = null;
            awaitingDrawDeckClick = false;
            drawDeckClickRequested = false;
            if (activeDrawDeckZone != null)
                activeDrawDeckZone.SetDropHighlight(false);
            if (restoreDeck)
                RestoreActiveDrawDeck();
            RevealDrawnCards(activeDrawRequest);
            activeDrawRequest = null;
            if (activeDrawCard != null)
                Destroy(activeDrawCard);
            activeDrawCard = null;
        }

        private void RestoreActiveDrawDeck()
        {
            if (activeDrawDeck != null)
            {
                activeDrawDeck.position = activeDrawDeckPosition;
                activeDrawDeck.rotation = activeDrawDeckRotation;
                activeDrawDeck.localScale = activeDrawDeckScale;
            }
            activeDrawDeck = null;
            activeDrawDeckZone = null;
        }

        private bool DeferBattlePresentationIfNeeded(DuelEvent duelEvent)
        {
            if (!phasePresentationLocked || replayingDeferredPresentation ||
                !IsBattlePresentationEvent(duelEvent.Message))
            {
                return false;
            }

            deferredBattlePresentations.Enqueue(duelEvent);
            return true;
        }

        private static bool IsBattlePresentationEvent(CoreMessage message)
        {
            return message == CoreMessage.Attack ||
                   message == CoreMessage.Battle ||
                   message == CoreMessage.AttackDisabled ||
                   message == CoreMessage.DamageStepStart ||
                   message == CoreMessage.DamageStepEnd ||
                   message == CoreMessage.Damage;
        }

        private void ReplayDeferredBattlePresentations()
        {
            if (deferredBattlePresentations.Count == 0)
                return;

            replayingDeferredPresentation = true;
            try
            {
                while (deferredBattlePresentations.Count > 0)
                {
                    HandleArenaPresentationEvent(
                        deferredBattlePresentations.Dequeue());
                }
            }
            finally
            {
                replayingDeferredPresentation = false;
            }
        }
    }
}
