using System.Linq;
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
        }

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
                EntersField = entersField
            };
        }

        private void BeginCardTransition(CardTransitionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Sprite == null || frame == null)
                return;

            float duration = CardTransitionDuration(
                snapshot.Code,
                snapshot.Kind == CardTransitionKind.Destruction
                    ? 0.66f
                    : snapshot.EntersField ? 0.32f : 0.42f);
            if (duration <= 0f)
                return;

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
                return;

            if (!TryScreenToFrameLocal(
                    snapshot.SourceScreenPoint,
                    out Vector2 start) ||
                !TryScreenToFrameLocal(
                    destinationScreenPoint,
                    out Vector2 destination))
            {
                return;
            }

            CanvasGroup target = HideTransitionTarget(snapshot.Current);
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
                target));
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
