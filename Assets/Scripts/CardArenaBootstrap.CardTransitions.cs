using System.Linq;
using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Cards;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    /// <summary>
    /// Non-authoritative card travel and destruction presentation. The Core
    /// state is reconciled immediately; these overlays never delay commands.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private const uint MoveReasonDestroy = 0x1U;
        private static readonly Vector2 TransitionCardSize =
            new(136f, 198f);

        private enum CardTransitionKind
        {
            None,
            Travel,
            Destruction
        }

        private sealed class CardTransitionSnapshot
        {
            public CardTransitionKind Kind;
            public uint Code;
            public CardLocation Previous;
            public CardLocation Current;
            public Vector2 SourceScreenPoint;
            public Sprite Sprite;
            public Sprite DestinationSprite;
            public bool FlipToDestination;
            public bool EntersField;
            public MonsterSummonArrivalEffect ArrivalEffect;
            public CanvasGroup HiddenTarget;
            public bool Released;
        }

        private readonly List<CardTransitionSnapshot>
            deferredMonsterArrivals = new();
        private int monsterArrivalSequenceGeneration;

        private CardTransitionSnapshot CaptureCardTransition(
            DuelEvent duelEvent)
        {
            CardTransitionKind kind = CardTransitionKindFor(duelEvent);
            if (kind == CardTransitionKind.None || frame == null)
                return null;

            TryGetLocationScreenPoint(
                duelEvent.Previous,
                duelEvent.Code,
                true,
                out Vector2 sourceScreenPoint,
                out Sprite visibleSprite);
            visibleSprite ??= SpriteForTransition(
                duelEvent.Code,
                duelEvent.Previous);

            bool entersField = kind == CardTransitionKind.Travel &&
                               HasLocation(duelEvent.Current) &&
                               IsFieldLocation(duelEvent.Current.Location);
            bool destinationFaceUp = entersField &&
                                     IsFaceUp(duelEvent.Current.Position);
            Sprite destinationSprite = visibleSprite;
            if (entersField)
            {
                destinationSprite = destinationFaceUp && duelEvent.Code != 0
                    ? SpriteFor(duelEvent.Code)
                    : cardBackSprite;
                if (destinationFaceUp)
                    visibleSprite = destinationSprite;
            }

            return new CardTransitionSnapshot
            {
                Kind = kind,
                Code = duelEvent.Code,
                Previous = duelEvent.Previous,
                Current = duelEvent.Current,
                SourceScreenPoint = sourceScreenPoint,
                Sprite = visibleSprite,
                DestinationSprite = destinationSprite,
                FlipToDestination = entersField &&
                                    !destinationFaceUp &&
                                    visibleSprite != cardBackSprite,
                EntersField = entersField,
                ArrivalEffect = entersField && destinationFaceUp &&
                                (duelEvent.Current.Location &
                                 DuelLocation.MonsterZone) != 0
                    ? ArrivalEffectFor(duelEvent.Code)
                    : MonsterSummonArrivalEffect.None
            };
        }

        private void BeginCardTransition(CardTransitionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Sprite == null || frame == null)
                return;

            if (IsFaceUpMonsterArrival(snapshot))
            {
                snapshot.HiddenTarget = HideTransitionTarget(snapshot.Current);
                if (ShouldDeferMonsterArrival(snapshot))
                    DeferMonsterArrival(snapshot);
                else
                    StartCoroutine(ResolveMonsterArrivalSequence(
                        snapshot,
                        monsterArrivalSequenceGeneration));
                return;
            }

            StartCardTransitionNow(snapshot);
        }

        private void StartCardTransitionNow(CardTransitionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Released)
                return;
            snapshot.Released = true;

            if (!TransitionDestinationStillCurrent(snapshot))
            {
                RevealTransitionTarget(snapshot.HiddenTarget);
                return;
            }

            float duration = CardTransitionDuration(
                snapshot.Code,
                snapshot.Kind == CardTransitionKind.Destruction
                    ? 0.40f
                    : snapshot.EntersField ? 0.32f : 0.42f);
            if (duration <= 0f)
            {
                RevealTransitionTarget(snapshot.HiddenTarget);
                return;
            }

            bool foundDestination = TryGetLocationScreenPoint(
                snapshot.Current,
                snapshot.Code,
                false,
                out Vector2 destinationScreenPoint,
                out _);
            if (!foundDestination &&
                snapshot.Kind == CardTransitionKind.Destruction)
            {
                foundDestination = TryGetGraveyardScreenPoint(
                    snapshot.Previous?.Controller ?? 0,
                    out destinationScreenPoint);
            }
            if (!foundDestination)
            {
                RevealTransitionTarget(snapshot.HiddenTarget);
                return;
            }

            if (!TryScreenToFrameLocal(
                    snapshot.SourceScreenPoint,
                    out Vector2 start) ||
                !TryScreenToFrameLocal(
                    destinationScreenPoint,
                    out Vector2 destination))
            {
                RevealTransitionTarget(snapshot.HiddenTarget);
                return;
            }

            CanvasGroup target = snapshot.HiddenTarget ??
                                 HideTransitionTarget(snapshot.Current);
            if (snapshot.Kind == CardTransitionKind.Destruction)
            {
                StartCoroutine(AnimateCardDestruction(
                    snapshot.Sprite,
                    start,
                    destination,
                    duration,
                    target));
                return;
            }

            StartCoroutine(AnimateCardTravel(
                snapshot.Sprite,
                snapshot.DestinationSprite,
                snapshot.FlipToDestination,
                start,
                destination,
                duration,
                target,
                snapshot.ArrivalEffect));
        }

        private bool ShouldDeferMonsterArrival(
            CardTransitionSnapshot snapshot)
        {
            if (!IsFaceUpMonsterArrival(snapshot))
                return false;
            return SummonPresentationMatches(
                       pendingSummonPresentation,
                       snapshot) ||
                   SummonPresentationMatches(
                       activeCardSoundPresentation,
                       snapshot) ||
                   cardSoundPresentationQueue.Any(request =>
                       SummonPresentationMatches(request, snapshot));
        }

        private static bool IsFaceUpMonsterArrival(
            CardTransitionSnapshot snapshot)
        {
            return snapshot != null && snapshot.EntersField &&
                   snapshot.Code != 0 && snapshot.Current != null &&
                   (snapshot.Current.Location & DuelLocation.MonsterZone) != 0 &&
                   IsFaceUp(snapshot.Current.Position);
        }

        private IEnumerator ResolveMonsterArrivalSequence(
            CardTransitionSnapshot snapshot,
            int generation)
        {
            // MSG_MOVE can precede MSG_*_SUMMONING. Hold the visual briefly
            // so a presentation announced by the Core in the same event
            // burst can run first. No prompt or Core progression is blocked.
            float deadline = Time.unscaledTime + 0.16f;
            while (snapshot != null && !snapshot.Released &&
                   Time.unscaledTime < deadline)
            {
                if (generation != monsterArrivalSequenceGeneration)
                {
                    snapshot.Released = true;
                    RevealTransitionTarget(snapshot.HiddenTarget);
                    yield break;
                }
                if (!TransitionDestinationStillCurrent(snapshot))
                {
                    snapshot.Released = true;
                    RevealTransitionTarget(snapshot.HiddenTarget);
                    yield break;
                }
                if (ShouldDeferMonsterArrival(snapshot))
                {
                    DeferMonsterArrival(snapshot);
                    yield break;
                }
                yield return null;
            }
            if (generation != monsterArrivalSequenceGeneration)
            {
                if (snapshot != null && !snapshot.Released)
                {
                    snapshot.Released = true;
                    RevealTransitionTarget(snapshot.HiddenTarget);
                }
                yield break;
            }
            StartCardTransitionNow(snapshot);
        }

        private void DeferMonsterArrival(CardTransitionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Released ||
                deferredMonsterArrivals.Contains(snapshot))
            {
                return;
            }
            deferredMonsterArrivals.Add(snapshot);
            StartCoroutine(ReleaseDeferredMonsterArrivalAfterTimeout(snapshot));
        }

        private bool TransitionDestinationStillCurrent(
            CardTransitionSnapshot snapshot)
        {
            if (!IsFaceUpMonsterArrival(snapshot))
                return true;
            DuelZone3D zone = ZoneFor(snapshot.Current);
            return zone != null && CodeAt(zone) == snapshot.Code;
        }

        private static bool SummonPresentationMatches(
            CardSoundPresentationRequest request,
            CardTransitionSnapshot snapshot)
        {
            if (request == null || snapshot == null ||
                request.Code != snapshot.Code)
            {
                return false;
            }
            if (request.Location == 0 || snapshot.Current == null)
                return true;
            return request.Controller == snapshot.Current.Controller &&
                   (request.Location & snapshot.Current.Location) != 0 &&
                   request.Sequence == snapshot.Current.Sequence;
        }

        private void ReleaseDeferredMonsterArrival(
            CardSoundPresentationRequest request)
        {
            CardTransitionSnapshot snapshot = deferredMonsterArrivals
                .FirstOrDefault(candidate =>
                    SummonPresentationMatches(request, candidate));
            if (snapshot == null)
                return;
            deferredMonsterArrivals.Remove(snapshot);
            StartCardTransitionNow(snapshot);
        }

        private IEnumerator ReleaseDeferredMonsterArrivalAfterTimeout(
            CardTransitionSnapshot snapshot)
        {
            yield return new WaitForSecondsRealtime(4f);
            if (snapshot == null || snapshot.Released)
                yield break;
            deferredMonsterArrivals.Remove(snapshot);
            StartCardTransitionNow(snapshot);
        }

        private void CancelDeferredMonsterArrivals()
        {
            monsterArrivalSequenceGeneration++;
            foreach (CardTransitionSnapshot snapshot in deferredMonsterArrivals)
            {
                if (snapshot == null || snapshot.Released)
                    continue;
                snapshot.Released = true;
                RevealTransitionTarget(snapshot.HiddenTarget);
            }
            deferredMonsterArrivals.Clear();
        }

        private static CardTransitionKind CardTransitionKindFor(
            DuelEvent duelEvent)
        {
            if (duelEvent?.Message != CoreMessage.Move ||
                !HasLocation(duelEvent.Previous))
            {
                return CardTransitionKind.None;
            }

            if ((duelEvent.Value & MoveReasonDestroy) != 0U)
                return CardTransitionKind.Destruction;
            if (!HasLocation(duelEvent.Current))
                return CardTransitionKind.None;

            uint previous = duelEvent.Previous.Location;
            uint current = duelEvent.Current.Location;
            bool entersField = IsFieldLocation(current) &&
                               (previous & (DuelLocation.Hand |
                                            DuelLocation.Deck |
                                            DuelLocation.Extra)) != 0U;
            bool entersGraveyard = IsFieldLocation(previous) &&
                                   (current & DuelLocation.Graveyard) != 0U;
            return entersField || entersGraveyard
                ? CardTransitionKind.Travel
                : CardTransitionKind.None;
        }

        private static bool HasLocation(CardLocation location)
        {
            return location != null && location.Location != 0;
        }

        private static bool IsFieldLocation(uint location)
        {
            return (location & (DuelLocation.MonsterZone |
                                DuelLocation.SpellTrapZone)) != 0U;
        }

        private float CardTransitionDuration(uint code, float baseDuration)
        {
            if (code == 0)
                return DuelAnimationPreferences.Duration(baseDuration);
            return IsMonster(code)
                ? DuelAnimationPreferences.MonsterDuration(baseDuration)
                : DuelAnimationPreferences.SpellTrapDuration(baseDuration);
        }

        private Sprite SpriteForTransition(
            uint code,
            CardLocation previous)
        {
            if (previous == null)
                return cardBackSprite;
            uint location = previous.Location;
            bool privateOrigin =
                (location & (DuelLocation.Deck | DuelLocation.Extra)) != 0U ||
                ((location & DuelLocation.Hand) != 0U &&
                 previous.Controller != 0);
            bool faceDown = !IsFaceUp(previous.Position);
            return privateOrigin || faceDown || code == 0
                ? cardBackSprite
                : SpriteFor(code);
        }

        private bool TryGetLocationScreenPoint(
            CardLocation location,
            uint code,
            bool preferExistingCard,
            out Vector2 screenPoint,
            out Sprite visibleSprite)
        {
            screenPoint = frame != null
                ? RectTransformUtility.WorldToScreenPoint(
                    UiCamera(),
                    frame.TransformPoint(frame.rect.center))
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            visibleSprite = null;
            if (!HasLocation(location))
                return false;

            if ((location.Location & DuelLocation.Hand) != 0U)
            {
                if (location.Controller == 0)
                {
                    CardView handCard = handViews.FirstOrDefault(card =>
                        card != null &&
                        card.HandIndex == (int)location.Sequence &&
                        (code == 0 || card.Code == code));
                    handCard ??= handViews.FirstOrDefault(card =>
                        card != null && (code == 0 || card.Code == code));
                    if (handCard?.Rect != null)
                    {
                        screenPoint = RectCenterScreen(handCard.Rect);
                        visibleSprite = handCard.Artwork;
                        return true;
                    }
                    if (handRoot != null)
                    {
                        screenPoint = RectCenterScreen(handRoot);
                        return true;
                    }
                }
                else
                {
                    RectTransform opponent = opponentHandContent != null
                        ? opponentHandContent
                        : opponentHandFan?.GetComponent<RectTransform>();
                    if (opponent != null)
                    {
                        screenPoint = RectCenterScreen(opponent);
                        visibleSprite = cardBackSprite;
                        return true;
                    }
                }
                return false;
            }

            DuelZone3D zone = ZoneFor(location);
            if (zone == null)
                return false;
            Transform presented = zone.FindPresentedCard();
            if (preferExistingCard && presented != null)
            {
                WorldCardInstanceView view =
                    presented.GetComponent<WorldCardInstanceView>();
                bool matchingIdentity = code == 0 || view == null ||
                    view.InstanceKey.DefinitionCode == 0 ||
                    view.InstanceKey.DefinitionCode == code;
                if (matchingIdentity)
                {
                    screenPoint = WorldScreenPoint(presented.position);
                    visibleSprite = VisibleWorldCardSprite(presented);
                    return true;
                }
            }

            Transform target = presented != null && !preferExistingCard
                ? presented
                : zone.CardPresentationAnchor;
            screenPoint = WorldScreenPoint(target.position);
            if (presented != null)
                visibleSprite = VisibleWorldCardSprite(presented);
            return true;
        }

        private bool TryGetGraveyardScreenPoint(
            byte controller,
            out Vector2 screenPoint)
        {
            DuelZone3D graveyard = AllZones().FirstOrDefault(zone =>
                zone != null &&
                StatePlayerForZone(zone) == controller &&
                zone.Kind == DuelZoneKind.Graveyard);
            if (graveyard == null)
            {
                screenPoint = Vector2.zero;
                return false;
            }
            screenPoint = WorldScreenPoint(
                graveyard.CardPresentationAnchor.position);
            return true;
        }

        private CanvasGroup HideTransitionTarget(CardLocation location)
        {
            if (!HasLocation(location))
                return null;
            DuelZone3D zone = ZoneFor(location);
            Transform presented = zone?.FindPresentedCard();
            if (presented == null)
                return null;
            CanvasGroup group = presented.GetComponent<CanvasGroup>();
            if (group == null)
                return null;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return group;
        }

        private Sprite VisibleWorldCardSprite(Transform presented)
        {
            if (presented == null)
                return null;
            Image image = presented
                .GetComponentsInChildren<Image>(true)
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.activeInHierarchy &&
                    candidate.sprite != null);
            return image != null ? image.sprite : null;
        }

        private Vector2 RectCenterScreen(RectTransform rect)
        {
            return RectTransformUtility.WorldToScreenPoint(
                UiCamera(),
                rect.TransformPoint(rect.rect.center));
        }

        private static Vector2 WorldScreenPoint(Vector3 worldPosition)
        {
            Camera camera = Camera.main;
            return camera != null
                ? (Vector2)camera.WorldToScreenPoint(worldPosition)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private bool TryScreenToFrameLocal(
            Vector2 screenPoint,
            out Vector2 localPoint)
        {
            if (frame == null)
            {
                localPoint = Vector2.zero;
                return false;
            }
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                frame,
                screenPoint,
                UiCamera(),
                out localPoint);
        }

        private Camera UiCamera()
        {
            return arenaCanvas != null &&
                   arenaCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? arenaCanvas.worldCamera
                : null;
        }

        private static void RevealTransitionTarget(CanvasGroup target)
        {
            if (target == null)
                return;
            target.alpha = 1f;
        }
    }
}
