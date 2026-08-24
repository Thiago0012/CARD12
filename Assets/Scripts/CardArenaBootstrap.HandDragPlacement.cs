using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Multiplayer;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    /// <summary>
    /// Presentation-only hand dragging. The drop submits an action that was
    /// offered by the current Core prompt; a requested destination is used
    /// only when the following SelectPlace prompt contains that exact zone.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private sealed class PendingHandDrop
        {
            public string StableZoneId;
            public DuelPlayerSide PhysicalOwner;
            public DuelZoneKind Kind;
            public int Sequence;
            public uint Code;
            public float ExpiresAt;
        }

        private readonly HashSet<DuelZone3D> handDragCandidateZones = new();
        private readonly List<Image> handDragTrajectorySegments = new();
        private CardView handDragCard;
        private DuelZone3D handDragHoveredZone;
        private DuelZone3D handDragHoverCandidate;
        private float handDragHoverCandidateSince;
        private RectTransform handDragTrajectory;
        private Vector2 handDragOriginScreen;
        private PendingHandDrop pendingHandDrop;

        public void BeginCardDrag(CardView card)
        {
            ResetHandCardDragPresentation();
            if (card == null || InteractionLocked || core?.CurrentPrompt == null)
                return;

            if (selectedCard != card)
                SelectCard(card);
            DuelPrompt prompt = core.CurrentPrompt;
            DuelChoice monsterAction = HandDropMonsterAction(prompt, card);
            DuelChoice spellTrapAction = HandDropSpellTrapAction(prompt, card);
            if (monsterAction == null && spellTrapAction == null)
                return;

            handDragCard = card;
            handDragOriginScreen = card.Rect != null
                ? RectCenterScreen(card.Rect)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.84f);

            DuelZoneKind targetKind = monsterAction != null
                ? DuelZoneKind.Monster
                : DuelZoneKind.SpellTrap;
            foreach (DuelZone3D zone in AllZones())
            {
                if (!IsCandidateHandDropZone(zone, targetKind))
                    continue;
                handDragCandidateZones.Add(zone);
                zone.SetDropHighlight(true, SummonBlue);
            }

            EnsureHandDragTrajectory();
            UpdateCardDrag(card, handDragOriginScreen);
            SetStatus(
                handDragCandidateZones.Count > 0
                    ? "Arraste ate uma zona azul. O Core confirmara o destino legal."
                    : "Nao ha uma zona livre para esta acao.",
                handDragCandidateZones.Count > 0 ? Cyan : Muted);
        }

        public void UpdateCardDrag(CardView card, Vector2 screenPosition)
        {
            if (card == null || card != handDragCard)
                return;

            DuelZone3D nextHovered = null;
            if (TryRaycastZone(screenPosition, out DuelZone3D raycastZone) &&
                handDragCandidateZones.Contains(raycastZone))
            {
                nextHovered = raycastZone;
            }
            if (nextHovered != handDragHoverCandidate)
            {
                handDragHoverCandidate = nextHovered;
                handDragHoverCandidateSince = Time.unscaledTime;
            }
            float hoverDelay = nextHovered == null ? 0.075f : 0.035f;
            if (nextHovered != handDragHoveredZone &&
                Time.unscaledTime - handDragHoverCandidateSince >= hoverDelay)
            {
                if (handDragHoveredZone != null)
                    handDragHoveredZone.SetDropHighlight(true, SummonBlue);
                handDragHoveredZone = nextHovered;
                if (handDragHoveredZone != null)
                    handDragHoveredZone.SetDropHighlight(
                        true,
                        new Color(0.28f, 0.88f, 1f, 1f));
            }

            Vector2 destinationScreen = handDragHoveredZone != null
                ? WorldScreenPoint(
                    handDragHoveredZone.CardPresentationAnchor.position)
                : screenPosition;
            UpdateHandDragTrajectory(
                handDragOriginScreen,
                destinationScreen,
                handDragHoveredZone != null ? Lime : Cyan);
        }

        public void EndCardDrag(Vector2 screenPosition)
        {
            CardView draggedCard = handDragCard;
            DuelZone3D destination = null;
            if (TryRaycastZone(screenPosition, out DuelZone3D raycastZone) &&
                handDragCandidateZones.Contains(raycastZone))
            {
                destination = raycastZone;
            }

            ClearHandDragVisuals();
            handDragCard = null;
            handDragHoveredZone = null;
            handDragHoverCandidate = null;

            if (draggedCard == null || destination == null ||
                InteractionLocked || core?.CurrentPrompt == null)
            {
                return;
            }

            DuelPrompt prompt = core.CurrentPrompt;
            DuelChoice action = destination.Kind == DuelZoneKind.Monster
                ? HandDropMonsterAction(prompt, draggedCard)
                : HandDropSpellTrapAction(prompt, draggedCard);
            if (action == null)
            {
                SetStatus(
                    "Essa acao deixou de estar disponivel. Escolha novamente.",
                    Muted);
                return;
            }

            pendingHandDrop = new PendingHandDrop
            {
                StableZoneId = destination.StableId,
                PhysicalOwner = destination.Owner,
                Kind = destination.Kind,
                Sequence = SequenceFor(destination),
                Code = draggedCard.Code,
                ExpiresAt = Time.unscaledTime + 20f
            };
            actionPanel?.SetActive(false);
            ClearHandSelection();
            core.SubmitChoice(action);
            observedPrompt = null;
            RefreshEverything(true);
        }

        private DuelChoice HandDropMonsterAction(
            DuelPrompt prompt,
            CardView card)
        {
            if (prompt == null || card == null || !IsMonster(card.Code))
                return null;
            List<DuelChoice> choices = ChoicesForCard(prompt, card.InstanceKey);
            return choices.FirstOrDefault(IsSummonChoice) ??
                   choices.FirstOrDefault(choice =>
                       Contains(choice.Label, "Baixar"));
        }

        private DuelChoice HandDropSpellTrapAction(
            DuelPrompt prompt,
            CardView card)
        {
            if (prompt == null || card == null || IsMonster(card.Code))
                return null;
            return ChoicesForCard(prompt, card.InstanceKey)
                .FirstOrDefault(choice => Contains(choice.Label, "Baixar"));
        }

        private bool IsCandidateHandDropZone(
            DuelZone3D zone,
            DuelZoneKind targetKind)
        {
            if (zone == null || !zone.gameObject.activeInHierarchy ||
                !IsLocalZone(zone) || zone.Kind != targetKind ||
                zone.IsDisabledByCore || CodeAt(zone) != 0)
            {
                return false;
            }

            // Dragging starts before MSG_SELECT_PLACE. Main zones are the
            // universally safe candidates; shared Extra Monster Zones remain
            // under the exact mask emitted by the Core after the action.
            return zone.ZoneIndex >= 0 && zone.ZoneIndex < 5;
        }

        private bool TryCompletePendingHandDrop(DuelPrompt prompt)
        {
            if (pendingHandDrop == null)
                return false;
            if (Time.unscaledTime > pendingHandDrop.ExpiresAt)
            {
                pendingHandDrop = null;
                return false;
            }
            if (prompt == null || prompt.Player != 0 ||
                prompt.Message != CoreMessage.SelectPlace ||
                prompt.MaximumSelections != 1)
            {
                return false;
            }

            PendingHandDrop requested = pendingHandDrop;
            DuelZone3D zone = AllZones().FirstOrDefault(candidate =>
                candidate != null &&
                candidate.Owner == requested.PhysicalOwner &&
                candidate.Kind == requested.Kind &&
                SequenceFor(candidate) == requested.Sequence &&
                (string.IsNullOrWhiteSpace(requested.StableZoneId) ||
                 candidate.StableId == requested.StableZoneId));
            pendingHandDrop = null;
            if (zone == null)
                return false;

            byte controller = StatePlayerForZone(zone);
            byte location = LocationFor(zone.Kind);
            int sequence = SequenceFor(zone);
            DuelChoice exact = prompt.Choices.FirstOrDefault(choice =>
                choice.HasLocation &&
                choice.Controller == controller &&
                (choice.Location & location) != 0 &&
                choice.Sequence == sequence);
            if (exact == null)
            {
                SetStatus(
                    "O Core restringiu os destinos. Escolha uma zona iluminada.",
                    Gold);
                return false;
            }

            ScheduleAutomaticPromptChoice(
                prompt,
                exact,
                $"Destino confirmado para {CardName(requested.Code)}.");
            return true;
        }

        private void EnsureHandDragTrajectory()
        {
            if (handDragTrajectory != null || frame == null)
                return;
            var root = new GameObject(
                "Trajetoria da Carta",
                typeof(RectTransform),
                typeof(CanvasGroup));
            root.transform.SetParent(frame, false);
            handDragTrajectory = root.GetComponent<RectTransform>();
            handDragTrajectory.anchorMin = Vector2.zero;
            handDragTrajectory.anchorMax = Vector2.one;
            handDragTrajectory.offsetMin = Vector2.zero;
            handDragTrajectory.offsetMax = Vector2.zero;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            PlaceTransitionBelowInterface(root.transform);

            for (int index = 0; index < 18; index++)
            {
                var segmentObject = new GameObject(
                    $"Segmento {index + 1:00}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                segmentObject.transform.SetParent(root.transform, false);
                Image image = segmentObject.GetComponent<Image>();
                image.raycastTarget = false;
                handDragTrajectorySegments.Add(image);
            }
        }

        private void UpdateHandDragTrajectory(
            Vector2 startScreen,
            Vector2 endScreen,
            Color color)
        {
            EnsureHandDragTrajectory();
            if (handDragTrajectory == null ||
                !TryScreenToFrameLocal(startScreen, out Vector2 start) ||
                !TryScreenToFrameLocal(endScreen, out Vector2 end))
            {
                return;
            }

            handDragTrajectory.gameObject.SetActive(true);
            float distance = Vector2.Distance(start, end);
            Vector2 control = (start + end) * 0.5f +
                              Vector2.up * Mathf.Clamp(
                                  distance * 0.22f,
                                  52f,
                                  170f);
            Vector2 previous = start;
            int count = handDragTrajectorySegments.Count;
            for (int index = 0; index < count; index++)
            {
                float t = (index + 1f) / count;
                float inverse = 1f - t;
                Vector2 point = inverse * inverse * start +
                                2f * inverse * t * control +
                                t * t * end;
                Vector2 delta = point - previous;
                Image segment = handDragTrajectorySegments[index];
                RectTransform rect = segment.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = (previous + point) * 0.5f;
                rect.sizeDelta = new Vector2(
                    delta.magnitude + 2f,
                    Mathf.Lerp(3f, 7f, t));
                rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                segment.color = new Color(
                    color.r,
                    color.g,
                    color.b,
                    Mathf.Lerp(0.24f, 0.92f, t));
                previous = point;
            }
        }

        private void ClearHandDragVisuals()
        {
            foreach (DuelZone3D zone in handDragCandidateZones)
                zone?.SetDropHighlight(false);
            handDragCandidateZones.Clear();
            if (handDragTrajectory != null)
                handDragTrajectory.gameObject.SetActive(false);
        }

        private void ResetHandCardDragPresentation()
        {
            ClearHandDragVisuals();
            handDragCard = null;
            handDragHoveredZone = null;
            handDragHoverCandidate = null;
            handDragHoverCandidateSince = 0f;
            pendingHandDrop = null;
            if (handDragTrajectory != null)
                Destroy(handDragTrajectory.gameObject);
            handDragTrajectory = null;
            handDragTrajectorySegments.Clear();
        }
    }
}
