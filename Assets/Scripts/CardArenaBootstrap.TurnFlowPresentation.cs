using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
            public int HeldDeckCount;
        }

        private const float DrawClickTimeoutSeconds = 5f;
        private const float DrawRevealHoldSeconds = 1.8f;
        private const float DrawRevealFastForwardSeconds = 0.18f;
        private const float TurnFlowWatchdogSeconds = 20f;
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
        private GameObject activeDrawGhost;
        private Vector3 activeDrawStartPosition;
        private bool drawRevealCanFastForward;
        private bool drawRevealFastForwardRequested;
        private float turnFlowPresentationStartedAt;
        private readonly int[] heldDrawDeckCounts = new int[2];

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
            if (IsTurnDrawEvent(duelEvent))
                HoldDrawDeckCount(duelEvent.Player, duelEvent.Codes.Length);
        }

        private int PresentedMainDeckCount(int player)
        {
            if (state?.Players == null || player < 0 ||
                player >= state.Players.Length)
            {
                return 0;
            }
            int held = player < heldDrawDeckCounts.Length
                ? heldDrawDeckCounts[player]
                : 0;
            return Mathf.Max(0, state.Players[player].DeckCount + held);
        }

        private void HoldDrawDeckCount(byte player, int count)
        {
            if (player >= heldDrawDeckCounts.Length || count <= 0)
                return;
            heldDrawDeckCounts[player] += count;
        }

        private void ReleaseDrawDeckCount(
            DrawPresentationRequest request,
            int count)
        {
            if (request == null || request.Player >= heldDrawDeckCounts.Length ||
                request.HeldDeckCount <= 0 || count <= 0)
            {
                return;
            }
            int released = Mathf.Min(count, request.HeldDeckCount);
            request.HeldDeckCount -= released;
            heldDrawDeckCounts[request.Player] = Mathf.Max(
                0,
                heldDrawDeckCounts[request.Player] - released);
            RefreshDeckStackVolumes();
            RefreshPileCounterPresentation(false);
        }

        private void ReleaseAllDrawDeckCounts()
        {
            for (int player = 0; player < heldDrawDeckCounts.Length; player++)
                heldDrawDeckCounts[player] = 0;
            RefreshDeckStackVolumes();
            RefreshPileCounterPresentation(false);
        }

        private bool IsTurnDrawEvent(DuelEvent duelEvent)
        {
            uint eventPhase = duelEvent?.PresentationPhase ?? 0u;
            if (eventPhase == 0u)
                eventPhase = state?.Phase ?? 0u;
            return duelEvent != null &&
                   duelEvent.Message == CoreMessage.Draw &&
                   duelEvent.Codes != null &&
                   duelEvent.Codes.Length > 0 &&
                   state != null &&
                   state.TurnNumber > 0 &&
                   (eventPhase & 0x001U) != 0;
        }

        private void BeginTurnFlowPresentation()
        {
            if (phasePresentationLocked)
                return;

            phasePresentationLocked = true;
            turnFlowPresentationStartedAt = Time.realtimeSinceStartup;
            // A network snapshot can publish the next Core prompt before its
            // presentation event reaches this replica. The phase animation
            // closes that prompt, so it must be considered unpresented until
            // the animation finishes and the player can actually answer it.
            ResetPromptPresentationIdentity();
            observedPrompt = null;
            if (automaticPromptRoutine != null)
            {
                StopCoroutine(automaticPromptRoutine);
                automaticPromptRoutine = null;
                scheduledAutomaticPrompt = null;
            }
            ClearZoneHighlights();
            HideCompactResponseBar();
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
                        DrawnCards = drawnCards,
                        HeldDeckCount = count
                    }
                });
            TryStartAnnouncementQueue();
        }

        private IEnumerator PlayDrawPresentation(
            DrawPresentationRequest request)
        {
            if (request == null)
                yield break;
            activeDrawRequest = request;

            MasterDuelArena3D arena3D =
                FindAnyObjectByType<MasterDuelArena3D>();
            DuelPlayerSide side = PhysicalSideForStatePlayer(request.Player);
            Transform deck = arena3D?.GetMainDeckTransform(side);
            if (deck == null)
            {
                ReleaseDrawDeckCount(request, request.HeldDeckCount);
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
            HideDecisionRibbon();

            bool localDraw = request.Player == 0;
            if (!localDraw)
            {
                uint[] remoteCodes = request.Codes ??
                                     System.Array.Empty<uint>();
                for (int index = 0; index < remoteCodes.Length; index++)
                {
                    Vector3 start = DrawGhostWorldPosition(deck, 0.08f);
                    arena3D.NotifyCardDrawn(side);
                    ReleaseDrawDeckCount(request, 1);
                    yield return AnimateDrawnCard(
                        remoteCodes[index],
                        side,
                        start,
                        false);
                }
                ReleaseDrawDeckCount(request, request.HeldDeckCount);
                RevealDrawnCards(request);
                RestoreActiveDrawDeck();
                activeDrawRequest = null;
                yield break;
            }

            if (localDraw)
            {
                awaitingDrawDeckClick = true;
                drawDeckClickRequested = false;
                drawInteractionSide = side;
                if (activeDrawDeckZone != null)
                    activeDrawDeckZone.SetDropHighlight(true);
            }

            Quaternion drawFocusRotation =
                DrawDeckPresentationRotation(deck);
            yield return MoveDrawDeck(
                deck,
                activeDrawDeckPosition,
                arena3D.GetDrawPresentationWorldPosition(side),
                0.36f,
                activeDrawDeckScale,
                activeDrawDeckScale * (localDraw ? 1.28f : 1.12f),
                activeDrawDeckRotation,
                drawFocusRotation);

            if (localDraw)
            {
                CreateDrawGhost(deck);
                float elapsed = 0f;
                float waitStartedAt = Time.realtimeSinceStartup;
                while (!drawDeckClickRequested &&
                       elapsed < DrawClickTimeoutSeconds)
                {
                    elapsed = Time.realtimeSinceStartup - waitStartedAt;
                    UpdateDrawGhost(deck, elapsed);
                    yield return null;
                }

                awaitingDrawDeckClick = false;
                if (activeDrawDeckZone != null)
                    activeDrawDeckZone.SetDropHighlight(false);
                DestroyActiveDrawGhost();
                yield return new WaitForSecondsRealtime(0.10f);
            }
            else
            {
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
                    ReleaseDrawDeckCount(request, 1);
                    yield return AnimateDrawnCard(
                        codes[index],
                        side,
                        index == 0
                            ? activeDrawStartPosition
                            : DrawGhostWorldPosition(deck, 0.08f),
                        true);
                    RevealDrawnCard(request, index);
                }
            }

            yield return MoveDrawDeck(
                deck,
                deck.position,
                activeDrawDeckPosition,
                0.30f,
                deck.localScale,
                activeDrawDeckScale,
                deck.rotation,
                activeDrawDeckRotation);
            RestoreActiveDrawDeck();
            ReleaseDrawDeckCount(request, request.HeldDeckCount);
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
            return true;
        }

        private void UpdateDrawRevealFastForward()
        {
            if (!drawRevealCanFastForward)
                return;
            bool pointerPressed =
                Mouse.current?.leftButton.wasPressedThisFrame == true ||
                Touchscreen.current?.primaryTouch.press.wasPressedThisFrame ==
                true;
            if (pointerPressed)
                RequestDrawRevealFastForward();
        }

        private void RequestDrawRevealFastForward()
        {
            if (drawRevealCanFastForward)
                drawRevealFastForwardRequested = true;
        }

        private IEnumerator MoveDrawDeck(
            Transform deck,
            Vector3 from,
            Vector3 to,
            float baseDuration,
            Vector3 fromScale,
            Vector3 toScale,
            Quaternion fromRotation,
            Quaternion toRotation)
        {
            float duration = DuelAnimationPreferences.Duration(baseDuration);
            if (duration <= 0f)
            {
                deck.position = to;
                deck.localScale = toScale;
                deck.rotation = toRotation;
                yield break;
            }

            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < duration)
            {
                float elapsed = Time.realtimeSinceStartup - startedAt;
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                deck.position = Vector3.Lerp(from, to, t);
                deck.localScale = Vector3.Lerp(fromScale, toScale, t);
                deck.rotation = Quaternion.Slerp(
                    fromRotation,
                    toRotation,
                    t);
                yield return null;
            }
            deck.position = to;
            deck.localScale = toScale;
            deck.rotation = toRotation;
        }

        private static Quaternion DrawDeckPresentationRotation(
            Transform deck)
        {
            if (deck == null)
                return Quaternion.identity;
            Camera camera = Camera.main;
            if (camera == null)
                return deck.rotation;

            Vector3 surfaceNormal = deck.up;
            Vector3 screenVertical = Vector3.ProjectOnPlane(
                camera.transform.up,
                surfaceNormal);
            if (screenVertical.sqrMagnitude < 0.0001f)
            {
                screenVertical = Vector3.ProjectOnPlane(
                    camera.transform.forward,
                    surfaceNormal);
            }
            if (screenVertical.sqrMagnitude < 0.0001f)
                return deck.rotation;
            screenVertical.Normalize();
            if (Vector3.Dot(screenVertical, deck.forward) < 0f)
                screenVertical = -screenVertical;
            return Quaternion.LookRotation(screenVertical, surfaceNormal);
        }

        private void CreateDrawGhost(Transform deck)
        {
            DestroyActiveDrawGhost();
            if (deck == null || cardBackSprite == null)
                return;

            activeDrawGhost = new GameObject(
                "Carta fantasma · compra aguardando clique");
            SpriteRenderer renderer =
                activeDrawGhost.AddComponent<SpriteRenderer>();
            renderer.sprite = cardBackSprite;
            renderer.sortingOrder = 499;
            renderer.color = new Color(0.72f, 0.90f, 1f, 0.58f);
            activeDrawGhost.transform.rotation =
                deck.rotation * Quaternion.Euler(90f, 0f, 0f);
            Vector3 deckScale = deck.lossyScale;
            Vector3 spriteSize = cardBackSprite.bounds.size;
            activeDrawGhost.transform.localScale = new Vector3(
                1.43f * Mathf.Abs(deckScale.x) /
                Mathf.Max(0.1f, spriteSize.x),
                1.95f * Mathf.Abs(deckScale.z) /
                Mathf.Max(0.1f, spriteSize.y),
                1f);
            UpdateDrawGhost(deck, 0f);
        }

        private void UpdateDrawGhost(Transform deck, float elapsed)
        {
            if (activeDrawGhost == null || deck == null)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 4.8f);
            activeDrawStartPosition = DrawGhostWorldPosition(
                deck,
                Mathf.Lerp(0.055f, 0.13f, pulse));
            activeDrawGhost.transform.position = activeDrawStartPosition;
            SpriteRenderer renderer =
                activeDrawGhost.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                Color color = renderer.color;
                color.a = Mathf.Lerp(0.42f, 0.74f, pulse);
                renderer.color = color;
            }
        }

        private static Vector3 DrawGhostWorldPosition(
            Transform deck,
            float lift)
        {
            if (deck == null)
                return Vector3.zero;
            Transform top = deck.Find("Card Stack/Top Card Back");
            Vector3 surface = top != null
                ? top.position
                : deck.position + deck.up * 0.62f;
            return surface + deck.up * lift;
        }

        private void DestroyActiveDrawGhost()
        {
            if (activeDrawGhost != null)
                Destroy(activeDrawGhost);
            activeDrawGhost = null;
        }

        private IEnumerator AnimateDrawnCard(
            uint code,
            DuelPlayerSide side,
            Vector3 start,
            bool localDraw)
        {
            cardAudioDirector ??= GetComponent<ArcaneAudioDirector>();
            cardAudioDirector?.PlayCardCue(ArcaneCardSound.Draw);
            if (localDraw && arenaCanvas != null)
            {
                yield return AnimateLocalDrawnCard(code, start);
                yield break;
            }

            var cardObject = new GameObject("Carta comprada · apresentação");
            activeDrawCard = cardObject;
            SpriteRenderer renderer = cardObject.AddComponent<SpriteRenderer>();
            Sprite front = localDraw ? SpriteFor(code) : cardBackSprite;
            renderer.sprite = cardBackSprite != null
                ? cardBackSprite
                : front;
            renderer.sortingOrder = 500;
            cardObject.transform.position = start;

            Camera camera = Camera.main;
            float height = renderer.sprite != null
                ? Mathf.Max(0.1f, renderer.sprite.bounds.size.y)
                : 1f;
            Vector3 startScale = Vector3.one * (2.05f / height);
            cardObject.transform.localScale = startScale;
            if (camera != null)
                cardObject.transform.rotation = camera.transform.rotation;

            if (!localDraw)
            {
                Vector3 opponentEnd = DrawCardDestination(
                    camera,
                    side,
                    start);
                yield return MovePresentedCard(
                    cardObject.transform,
                    start,
                    opponentEnd,
                    startScale,
                    startScale * 1.08f,
                    0.44f);
                if (cardObject != null)
                    Destroy(cardObject);
                activeDrawCard = null;
                yield break;
            }

            Vector3 revealPosition = DrawCardRevealPosition(camera, start);
            Vector3 revealScale = Vector3.one * (4.85f / height);
            float duration = DuelAnimationPreferences.Duration(0.58f);
            bool frontVisible = false;
            float travelStartedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - travelStartedAt < duration)
            {
                float elapsed =
                    Time.realtimeSinceStartup - travelStartedAt;
                if (cardObject == null)
                    yield break;
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                if (!frontVisible && t >= 0.25f)
                {
                    renderer.sprite = front;
                    frontVisible = true;
                }
                Vector3 arcDirection = camera != null
                    ? camera.transform.up
                    : Vector3.up;
                Vector3 arc = arcDirection *
                              (Mathf.Sin(t * Mathf.PI) * 0.82f);
                cardObject.transform.position =
                    Vector3.Lerp(start, revealPosition, t) + arc;
                cardObject.transform.localScale = Vector3.Lerp(
                    startScale,
                    revealScale,
                    t);
                if (camera != null)
                {
                    cardObject.transform.rotation =
                        camera.transform.rotation *
                        Quaternion.Euler(0f, 360f * t, 0f);
                }
                yield return null;
            }

            if (cardObject == null)
                yield break;
            renderer.sprite = front;
            cardObject.transform.position = revealPosition;
            cardObject.transform.localScale = revealScale;
            if (camera != null)
                cardObject.transform.rotation = camera.transform.rotation;

            drawRevealFastForwardRequested = false;
            drawRevealCanFastForward = true;
            float holdElapsed = 0f;
            float holdStartedAt = Time.realtimeSinceStartup;
            while (holdElapsed < DrawRevealHoldSeconds)
            {
                if (cardObject == null)
                    yield break;
                if (drawRevealFastForwardRequested && holdElapsed > 0.10f)
                {
                    holdStartedAt = Time.realtimeSinceStartup -
                        (DrawRevealHoldSeconds -
                         DrawRevealFastForwardSeconds);
                    drawRevealFastForwardRequested = false;
                }
                holdElapsed = Time.realtimeSinceStartup - holdStartedAt;
                float pulse = 1f + 0.018f * Mathf.Sin(
                    holdElapsed * 5.6f);
                cardObject.transform.localScale = revealScale * pulse;
                yield return null;
            }
            drawRevealCanFastForward = false;

            Vector3 handDestination = DrawCardDestination(
                camera,
                side,
                start);
            yield return MovePresentedCard(
                cardObject.transform,
                revealPosition,
                handDestination,
                revealScale,
                startScale * 0.72f,
                0.28f);

            if (cardObject != null)
                Destroy(cardObject);
            activeDrawCard = null;
            drawRevealFastForwardRequested = false;
        }

        private IEnumerator AnimateLocalDrawnCard(uint code, Vector3 start)
        {
            var root = new GameObject(
                "Carta comprada · acima da mão",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            activeDrawCard = root;
            root.transform.SetParent(arenaCanvas.transform, false);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Canvas overlayCanvas = root.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 2000;
            root.transform.SetAsLastSibling();

            Image image = CreateImage(
                root.transform,
                "Carta revelada",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Color.white);
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = cardBackSprite;
            RectTransform card = image.rectTransform;
            Camera camera = Camera.main;
            Vector3 viewport3 = camera != null
                ? camera.WorldToViewportPoint(start)
                : new Vector3(0.82f, 0.22f, 1f);
            Vector2 startViewport = new Vector2(
                Mathf.Clamp01(viewport3.x),
                Mathf.Clamp01(viewport3.y));
            Vector2 revealViewport = new Vector2(0.5f, 0.53f);
            float startHeight = Mathf.Clamp(Screen.height * 0.20f, 150f, 230f);
            float revealHeight = Mathf.Clamp(Screen.height * 0.62f, 430f, 680f);
            SetDrawUiPose(card, startViewport, startHeight, 0f);

            Sprite front = SpriteFor(code);
            float travel = DuelAnimationPreferences.Duration(0.58f);
            bool frontVisible = false;
            float travelStartedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - travelStartedAt < travel)
            {
                float elapsed =
                    Time.realtimeSinceStartup - travelStartedAt;
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / travel));
                if (!frontVisible && t >= 0.25f)
                {
                    image.sprite = front;
                    frontVisible = true;
                }
                Vector2 arc = Vector2.up *
                              (Mathf.Sin(t * Mathf.PI) * 0.075f);
                SetDrawUiPose(
                    card,
                    Vector2.Lerp(startViewport, revealViewport, t) + arc,
                    Mathf.Lerp(startHeight, revealHeight, t),
                    360f * t);
                yield return null;
            }

            image.sprite = front;
            SetDrawUiPose(card, revealViewport, revealHeight, 0f);
            drawRevealFastForwardRequested = false;
            drawRevealCanFastForward = true;
            float holdElapsed = 0f;
            float holdStartedAt = Time.realtimeSinceStartup;
            while (holdElapsed < DrawRevealHoldSeconds)
            {
                if (drawRevealFastForwardRequested && holdElapsed > 0.10f)
                {
                    holdStartedAt = Time.realtimeSinceStartup -
                        (DrawRevealHoldSeconds -
                         DrawRevealFastForwardSeconds);
                    drawRevealFastForwardRequested = false;
                }
                holdElapsed = Time.realtimeSinceStartup - holdStartedAt;
                float pulse = 1f + 0.018f *
                              Mathf.Sin(holdElapsed * 5.6f);
                SetDrawUiPose(
                    card,
                    revealViewport,
                    revealHeight * pulse,
                    0f);
                yield return null;
            }
            drawRevealCanFastForward = false;

            Vector2 handViewport = new Vector2(0.5f, 0.10f);
            float handHeight = Mathf.Clamp(Screen.height * 0.16f, 120f, 180f);
            float returnDuration = DuelAnimationPreferences.Duration(0.28f);
            float returnStartedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - returnStartedAt <
                   returnDuration)
            {
                float elapsed =
                    Time.realtimeSinceStartup - returnStartedAt;
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / returnDuration));
                SetDrawUiPose(
                    card,
                    Vector2.Lerp(revealViewport, handViewport, t),
                    Mathf.Lerp(revealHeight, handHeight, t),
                    0f);
                yield return null;
            }
            Destroy(root);
            activeDrawCard = null;
            drawRevealFastForwardRequested = false;
        }

        private static void SetDrawUiPose(
            RectTransform card,
            Vector2 viewport,
            float height,
            float rotationY)
        {
            if (card == null)
                return;
            card.anchorMin = viewport;
            card.anchorMax = viewport;
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(height * 0.70f, height);
            card.localRotation = Quaternion.Euler(0f, rotationY, 0f);
        }

        private static IEnumerator MovePresentedCard(
            Transform card,
            Vector3 from,
            Vector3 to,
            Vector3 fromScale,
            Vector3 toScale,
            float baseDuration)
        {
            float duration = DuelAnimationPreferences.Duration(baseDuration);
            if (duration <= 0f)
            {
                if (card != null)
                {
                    card.position = to;
                    card.localScale = toScale;
                }
                yield break;
            }
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < duration)
            {
                float elapsed = Time.realtimeSinceStartup - startedAt;
                if (card == null)
                    yield break;
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                card.position = Vector3.Lerp(from, to, t);
                card.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }
            if (card != null)
            {
                card.position = to;
                card.localScale = toScale;
            }
        }

        private static Vector3 DrawCardRevealPosition(
            Camera camera,
            Vector3 start)
        {
            if (camera == null)
                return start + Vector3.up * 2.2f;
            float depth = Vector3.Dot(
                start - camera.transform.position,
                camera.transform.forward);
            depth = Mathf.Max(2.1f, depth * 0.43f);
            return camera.ViewportToWorldPoint(
                new Vector3(0.5f, 0.53f, depth));
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
            ResetPromptPresentationIdentity();
            observedPrompt = null;
            RefreshEverything(true);
            ReplayDeferredBattlePresentations();
        }

        private void RecoverStalledTurnFlowPresentation()
        {
            if (!phasePresentationLocked ||
                turnFlowPresentationStartedAt <= 0f ||
                Time.realtimeSinceStartup - turnFlowPresentationStartedAt <
                    TurnFlowWatchdogSeconds)
            {
                return;
            }

            Debug.LogError(
                "[Arcane Duel] A apresentação de fase excedeu 20 segundos; " +
                "liberando a réplica sem alterar o estado do Core.");
            announcementQueue.Clear();
            if (announcementRoutine != null)
            {
                StopCoroutine(announcementRoutine);
                announcementRoutine = null;
            }
            ResetTurnFlowPresentation(true);
            ResetPromptPresentationIdentity();
            observedPrompt = null;
            RefreshEverything(true);
        }

        private void ResetTurnFlowPresentation(bool restoreDeck)
        {
            phasePresentationLocked = false;
            turnFlowPresentationStartedAt = 0f;
            presentationPhaseOverride = null;
            awaitingDrawDeckClick = false;
            drawDeckClickRequested = false;
            drawRevealCanFastForward = false;
            drawRevealFastForwardRequested = false;
            if (activeDrawDeckZone != null)
                activeDrawDeckZone.SetDropHighlight(false);
            DestroyActiveDrawGhost();
            if (restoreDeck)
                RestoreActiveDrawDeck();
            RevealDrawnCards(activeDrawRequest);
            activeDrawRequest = null;
            if (activeDrawCard != null)
                Destroy(activeDrawCard);
            activeDrawCard = null;
            ReleaseAllDrawDeckCounts();
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
