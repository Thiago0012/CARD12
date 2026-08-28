using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using ArcaneArena.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private sealed class AttackTargetCandidate
        {
            public DuelChoice Choice;
            public DuelZone3D Zone;
            public ulong RuntimeId;
        }

        private readonly List<AttackTargetCandidate> attackTargetCandidates =
            new();
        private DuelZone3D pendingAttackSource;
        private DuelChoice pendingAttackCommand;
        private ulong pendingAttackCommandRequestId;
        private DuelZone3D intendedAttackTarget;
        private bool intendedDirectAttack;
        private int capturedAttackPointerId = int.MinValue;
        private int attackTargetSelectionPointerId = int.MinValue;
        private bool awaitingAttackTargetPrompt;
        private bool attackTargetingActive;
        private bool attackArrowLockedToIntent;
        private DuelPrompt attackTargetPrompt;
        private DuelChoice directAttackTargetChoice;
        private DuelZone3D highlightedAttackTarget;
        private GameObject directAttackTargetButton;
        private Coroutine attackTargetAutoSubmitRoutine;
        private Vector2 lastAttackPointerPosition;
        private float attackPointerReleaseFallbackAt = -1f;
        private const float AttackPointerReleaseFallbackSeconds = 0.10f;

        public void BeginMonsterAttackDrag(
            DuelZone3D zone,
            Vector2 screenPosition,
            int pointerId)
        {
            DuelPrompt prompt = core?.CurrentPrompt;
            if (InteractionLocked || zone == null ||
                prompt?.Message != CoreMessage.SelectBattleCommand)
            {
                return;
            }

            DuelChoice attack = ChoicesForCard(
                    prompt,
                    CodeAt(zone),
                    StatePlayerForZone(zone),
                    (byte)DuelLocation.MonsterZone,
                    zone.ZoneIndex)
                .FirstOrDefault(choice => Contains(choice.Label, "Atacar"));
            if (attack == null)
                return;

            PrepareAttackTargeting(zone, attack);
            draggingAttacker = zone;
            capturedAttackPointerId = pointerId;
            lastAttackPointerPosition = screenPosition;
            attackPointerReleaseFallbackAt = -1f;
            EnsureAttackLine();
            UpdateMonsterAttackDrag(screenPosition, pointerId);
        }

        public void UpdateMonsterAttackDrag(
            Vector2 screenPosition,
            int pointerId)
        {
            if (!OwnsAttackPointer(pointerId) || attackLine == null ||
                Camera.main == null)
            {
                return;
            }

            lastAttackPointerPosition = screenPosition;
            attackPointerReleaseFallbackAt = -1f;

            DuelZone3D source = draggingAttacker ?? pendingAttackSource;
            if (source == null)
                return;

            DuelZone3D hovered = null;
            if (TryRaycastZone(screenPosition, out DuelZone3D raycastZone))
                hovered = raycastZone;
            if (attackTargetingActive)
                UpdateAttackTargetHover(hovered);

            Vector3 start = AttackAnchor(source);
            Vector3 end;
            bool validTarget = false;
            bool directTarget = false;
            if (attackTargetingActive &&
                TryCandidateForZone(hovered, out AttackTargetCandidate target))
            {
                end = AttackAnchor(target.Zone);
                validTarget = true;
            }
            else if (!attackTargetingActive && hovered != null &&
                     hovered.Kind == DuelZoneKind.Monster &&
                     hovered.Owner != source.Owner)
            {
                // Before the Core opens SelectCard there is no legal-target
                // list yet, but snapping to the opposing monster keeps the
                // drag stable on both mouse and touch. Final legality still
                // comes exclusively from the following Core prompt.
                end = AttackAnchor(hovered);
                validTarget = true;
            }
            else if (CanAimAtDirectAttack(screenPosition))
            {
                end = DirectAttackPoint(source.Owner);
                validTarget = true;
                directTarget = true;
            }
            else
            {
                Ray ray = Camera.main.ScreenPointToRay(screenPosition);
                end = ray.origin + ray.direction * 14f;
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    end = hit.point + Vector3.up * 0.25f;
            }

            RenderAttackArrow(
                start,
                end,
                validTarget,
                directTarget);
        }

        public void EndMonsterAttackDrag(
            Vector2 screenPosition,
            int pointerId)
        {
            if (draggingAttacker == null)
            {
                return;
            }
            if (!OwnsAttackPointer(pointerId) || InteractionLocked)
            {
                // A lost/cancelled Android touch or a presentation lock that
                // arrives between press and release must never leave the
                // targeting arrow and its pending command alive forever.
                CancelAttackTargeting();
                return;
            }

            DuelZone3D attacker = draggingAttacker;
            draggingAttacker = null;
            capturedAttackPointerId = int.MinValue;
            attackPointerReleaseFallbackAt = -1f;
            if (TryRaycastZone(screenPosition, out DuelZone3D target) &&
                target != null && target.Kind == DuelZoneKind.Monster &&
                target.Owner != attacker.Owner)
            {
                intendedAttackTarget = target;
            }
            intendedDirectAttack =
                pendingAttackCommand?.DirectAttackAvailable == true &&
                CanAimAtDirectAttack(screenPosition);
            attackArrowLockedToIntent =
                intendedAttackTarget != null || intendedDirectAttack;

            DuelChoice attack = pendingAttackCommand;
            if (attack == null ||
                !CoreCardActionBinding.BelongsToRequest(
                    core?.CurrentPrompt,
                    attack))
            {
                CancelAttackTargeting();
                return;
            }

            core.SubmitChoice(attack);
            RefreshEverything(true);
        }

        private bool OwnsAttackPointer(int pointerId)
        {
            return capturedAttackPointerId == int.MinValue ||
                   capturedAttackPointerId == -1 ||
                   pointerId == -1 ||
                   pointerId == capturedAttackPointerId;
        }

        private void UpdateAttackTargetingPointer()
        {
            RecoverReleasedAttackTouch();
            if (!attackTargetingActive || pendingAttackSource == null ||
                InteractionLocked)
            {
                return;
            }

            // The release submits the attack command first and the Core emits
            // the target prompt on a later frame. Mouse/touch hover must not
            // overwrite the target captured at release during that gap.
            if (attackArrowLockedToIntent)
            {
                if (intendedAttackTarget != null)
                    PointAttackLineAt(intendedAttackTarget);
                else if (intendedDirectAttack)
                    PointAttackLineAtDirectTarget();
                return;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null &&
                touchscreen.primaryTouch.press.isPressed)
            {
                int touchId = touchscreen.primaryTouch.touchId.ReadValue();
                // InputSystemUIInputModule's PointerEventData.pointerId is not
                // guaranteed to equal TouchControl.touchId on every Android
                // device. Hovering is pointer-neutral; only EventSystem click
                // events are allowed to capture a selection pointer.
                UpdateMonsterAttackDrag(
                    touchscreen.primaryTouch.position.ReadValue(),
                    -1);
                return;
            }

            if (Mouse.current != null)
            {
                UpdateMonsterAttackDrag(
                    Mouse.current.position.ReadValue(),
                    -1);
            }
        }

        private void RecoverReleasedAttackTouch()
        {
            if (draggingAttacker == null || capturedAttackPointerId < 0)
            {
                attackPointerReleaseFallbackAt = -1f;
                return;
            }

            Touchscreen touchscreen = Touchscreen.current;
            bool anyTouchPressed = false;
            if (touchscreen != null)
            {
                for (int index = 0;
                     index < touchscreen.touches.Count;
                     index++)
                {
                    if (!touchscreen.touches[index].press.isPressed)
                        continue;
                    anyTouchPressed = true;
                    break;
                }
            }
            if (anyTouchPressed)
            {
                attackPointerReleaseFallbackAt = -1f;
                return;
            }

            if (attackPointerReleaseFallbackAt < 0f)
            {
                attackPointerReleaseFallbackAt =
                    Time.unscaledTime + AttackPointerReleaseFallbackSeconds;
                return;
            }
            if (Time.unscaledTime < attackPointerReleaseFallbackAt)
                return;

            int releasedPointerId = capturedAttackPointerId;
            EndMonsterAttackDrag(
                lastAttackPointerPosition,
                releasedPointerId);
        }

        public void CancelMonsterAttackDrag(DuelZone3D source)
        {
            if (source == null ||
                (draggingAttacker != source && pendingAttackSource != source))
            {
                return;
            }
            CancelAttackTargeting();
        }

        private void PrepareAttackTargeting(
            DuelZone3D attacker,
            DuelChoice attackCommand)
        {
            CancelAttackTargeting();
            pendingAttackSource = attacker;
            pendingAttackCommand = attackCommand;
            pendingAttackCommandRequestId =
                attackCommand?.RequestId ?? core?.CurrentPrompt?.RequestId ?? 0;
            awaitingAttackTargetPrompt = true;
            attackArrowLockedToIntent = false;
        }

        private bool TryPresentAttackTargeting(DuelPrompt prompt)
        {
            if (!awaitingAttackTargetPrompt || pendingAttackSource == null ||
                !IsAttackTargetSelectionPrompt(prompt))
            {
                return false;
            }

            attackTargetCandidates.Clear();
            foreach (DuelChoice choice in prompt.Choices.Where(choice =>
                         choice != null && choice.HasLocation &&
                         (choice.Location & DuelLocation.MonsterZone) != 0))
            {
                DuelZone3D zone = FindZone(
                    choice.Controller,
                    choice.Location,
                    (int)choice.Sequence);
                if (zone == null)
                    continue;
                CardInstanceState instance = InstanceAt(zone);
                attackTargetCandidates.Add(new AttackTargetCandidate
                {
                    Choice = choice,
                    Zone = zone,
                    RuntimeId = choice.RuntimeId != 0
                        ? choice.RuntimeId
                        : instance?.RuntimeId ?? 0
                });
            }

            directAttackTargetChoice =
                pendingAttackCommand?.DirectAttackAvailable == true
                    ? prompt.Choices.FirstOrDefault(choice =>
                        choice != null && !choice.HasLocation &&
                        choice.Response != null && choice.Response.Length > 0)
                    : null;
            if (attackTargetCandidates.Count == 0 &&
                directAttackTargetChoice == null)
            {
                return false;
            }

            int generation = OpenExclusiveDuelUiSurface(
                DuelUiSurfaceKind.PromptPrimary,
                prompt);
            attackTargetPrompt = prompt;
            attackTargetingActive = true;
            awaitingAttackTargetPrompt = false;
            EnsureAttackLine();
            ShowDirectAttackTarget(generation);

            DuelChoice intended = null;
            if (intendedAttackTarget != null &&
                TryCandidateForZone(
                    intendedAttackTarget,
                    out AttackTargetCandidate candidate))
            {
                intended = candidate.Choice;
                UpdateAttackTargetHover(candidate.Zone);
                PointAttackLineAt(candidate.Zone);
            }
            else if (intendedDirectAttack &&
                     directAttackTargetChoice != null)
            {
                intended = directAttackTargetChoice;
                PointAttackLineAtDirectTarget();
            }
            else
            {
                attackArrowLockedToIntent = false;
                intendedAttackTarget = null;
                intendedDirectAttack = false;
                if (directAttackTargetChoice != null)
                    PointAttackLineAtDirectTarget();
                else
                    HideAttackArrow();
            }

            SetStatus(
                directAttackTargetChoice != null
                    ? "APONTE A SETA PARA UM ALVO LEGAL OU PARA ATAQUE DIRETO."
                    : "APONTE A SETA E CLIQUE EM UM MONSTRO DESTACADO.",
                Cyan);
            if (intended != null)
            {
                if (attackTargetAutoSubmitRoutine != null)
                    StopCoroutine(attackTargetAutoSubmitRoutine);
                attackTargetAutoSubmitRoutine = StartCoroutine(
                    SubmitAttackTargetNextFrame(prompt, intended));
            }
            return true;
        }

        private bool IsAttackTargetSelectionPrompt(DuelPrompt prompt)
        {
            if (prompt == null || prompt.Player != 0 ||
                prompt.Message != CoreMessage.SelectCard ||
                prompt.MaximumSelections != 1 ||
                prompt.MinimumSelections > 1)
            {
                return false;
            }

            DuelChoice[] located = prompt.Choices
                .Where(choice => choice != null && choice.HasLocation)
                .ToArray();
            return located.Length > 0 && located.All(choice =>
                (choice.Location & DuelLocation.MonsterZone) != 0 &&
                choice.Controller != StatePlayerForZone(pendingAttackSource));
        }

        private IEnumerator SubmitAttackTargetNextFrame(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            yield return null;
            attackTargetAutoSubmitRoutine = null;
            if (attackTargetingActive &&
                SamePromptIdentity(core?.CurrentPrompt, prompt))
            {
                SubmitAttackTargetChoice(choice);
            }
        }

        private bool TrySubmitAttackTargetFromZone(
            DuelZone3D zone,
            int pointerId)
        {
            if (!attackTargetingActive)
                return false;
            if (pointerId >= 0 &&
                attackTargetSelectionPointerId != int.MinValue &&
                attackTargetSelectionPointerId != pointerId)
            {
                return true;
            }
            if (pointerId >= 0)
                attackTargetSelectionPointerId = pointerId;
            if (!SamePromptIdentity(core?.CurrentPrompt, attackTargetPrompt))
            {
                CancelAttackTargeting();
                return true;
            }
            if (!TryCandidateForZone(zone, out AttackTargetCandidate candidate))
            {
                SetStatus(
                    "Este monstro não é um alvo legal para o ataque atual.",
                    Muted);
                return true;
            }

            UpdateAttackTargetHover(zone);
            PointAttackLineAt(zone);
            SubmitAttackTargetChoice(candidate.Choice);
            return true;
        }

        private bool TryCandidateForZone(
            DuelZone3D zone,
            out AttackTargetCandidate candidate)
        {
            candidate = null;
            if (zone == null)
                return false;
            CardInstanceState instance = InstanceAt(zone);
            ulong runtimeId = instance?.RuntimeId ?? 0;
            candidate = attackTargetCandidates.FirstOrDefault(item =>
                item != null && item.Zone == zone &&
                (runtimeId == 0 || item.RuntimeId == 0 ||
                 item.RuntimeId == runtimeId));
            if (candidate != null)
                return true;

            byte controller = StatePlayerForZone(zone);
            byte location = LocationFor(zone.Kind);
            int sequence = SequenceFor(zone);
            candidate = attackTargetCandidates.FirstOrDefault(item =>
                item?.Choice != null && item.Choice.Controller == controller &&
                (item.Choice.Location & location) != 0 &&
                item.Choice.Sequence == (uint)sequence &&
                (item.RuntimeId == 0 || runtimeId == 0 ||
                 item.RuntimeId == runtimeId));
            return candidate != null;
        }

        private bool UpdateAttackTargetHover(
            DuelZone3D zone,
            bool pointerInside = true)
        {
            if (!attackTargetingActive)
                return false;
            if (highlightedAttackTarget != null &&
                (highlightedAttackTarget != zone || !pointerInside))
            {
                highlightedAttackTarget.SetDropHighlight(false);
                highlightedAttackTarget = null;
            }
            if (pointerInside &&
                TryCandidateForZone(zone, out AttackTargetCandidate candidate))
            {
                highlightedAttackTarget = candidate.Zone;
                highlightedAttackTarget.SetDropHighlight(true, Cyan);
                PointAttackLineAt(candidate.Zone);
            }
            return true;
        }

        private void SubmitAttackTargetChoice(DuelChoice choice)
        {
            DuelPrompt prompt = core?.CurrentPrompt;
            if (choice == null ||
                !SamePromptIdentity(prompt, attackTargetPrompt) ||
                !CoreCardActionBinding.BelongsToRequest(prompt, choice))
            {
                CancelAttackTargeting();
                return;
            }

            CancelAttackTargeting();
            core.SubmitChoice(choice);
            RefreshEverything(true);
        }

        private void ShowDirectAttackTarget(int generation)
        {
            if (directAttackTargetButton == null)
            {
                directAttackTargetButton = CreatePanel(
                    frame,
                    "Alvo de Ataque Direto",
                    new Vector2(0.405f, 0.80f),
                    new Vector2(0.595f, 0.885f),
                    new Color(0.10f, 0.025f, 0.035f, 0.94f));
                AddOutline(directAttackTargetButton, Red);
                CreateText(
                    directAttackTargetButton.transform,
                    "ATAQUE DIRETO",
                    16,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.04f, 0.08f),
                    new Vector2(0.96f, 0.92f),
                    TextAnchor.MiddleCenter);
                Button button = directAttackTargetButton.AddComponent<Button>();
                button.targetGraphic =
                    directAttackTargetButton.GetComponent<Image>();
                button.onClick.AddListener(SubmitDirectAttackTarget);
            }
            directAttackTargetButton.SetActive(
                directAttackTargetChoice != null &&
                IsDuelUiGenerationCurrent(
                    generation,
                    DuelUiSurfaceKind.PromptPrimary));
            if (directAttackTargetButton.activeSelf)
                directAttackTargetButton.transform.SetAsLastSibling();
        }

        private void SubmitDirectAttackTarget()
        {
            if (!attackTargetingActive || directAttackTargetChoice == null ||
                activeDuelUiSurface != DuelUiSurfaceKind.PromptPrimary)
            {
                return;
            }
            PointAttackLineAtDirectTarget();
            SubmitAttackTargetChoice(directAttackTargetChoice);
        }

        private void PointAttackLineAt(DuelZone3D target)
        {
            if (pendingAttackSource == null ||
                target == null)
            {
                return;
            }
            RenderAttackArrow(
                AttackAnchor(pendingAttackSource),
                AttackAnchor(target),
                true,
                false);
        }

        private void PointAttackLineAtDirectTarget()
        {
            if (pendingAttackSource == null)
                return;
            RenderAttackArrow(
                AttackAnchor(pendingAttackSource),
                DirectAttackPoint(pendingAttackSource.Owner),
                true,
                true);
        }

        private void RenderAttackArrow(
            Vector3 start,
            Vector3 end,
            bool validTarget,
            bool directTarget)
        {
            EnsureAttackLine();
            if (attackArrowVfx != null)
            {
                attackArrowVfx.SetEndpoints(
                    start,
                    end,
                    validTarget,
                    directTarget);
                return;
            }

            if (attackLine == null)
                return;
            attackLine.positionCount = 2;
            attackLine.SetPosition(0, start);
            attackLine.SetPosition(1, end);
            attackLine.enabled = true;
        }

        private void HideAttackArrow()
        {
            if (attackArrowVfx != null)
                attackArrowVfx.SetVisible(false);
            else if (attackLine != null)
                attackLine.enabled = false;
        }

        private static Vector3 AttackAnchor(DuelZone3D zone)
        {
            Transform anchor = zone?.CardPresentationAnchor;
            return anchor != null
                ? anchor.position + Vector3.up * 0.32f
                : zone != null
                    ? zone.transform.position + Vector3.up * 0.70f
                    : Vector3.zero;
        }

        private bool CanAimAtDirectAttack(Vector2 screenPosition)
        {
            if (pendingAttackCommand?.DirectAttackAvailable != true ||
                pendingAttackSource == null || Camera.main == null)
            {
                return false;
            }
            Vector2 directScreen = Camera.main.WorldToScreenPoint(
                DirectAttackPoint(pendingAttackSource.Owner));
            float radius = Mathf.Max(96f, Screen.height * 0.085f);
            return Vector2.Distance(screenPosition, directScreen) <= radius;
        }

        private bool IsAttackTargetingPromptVisible(DuelPrompt prompt)
        {
            return attackTargetingActive &&
                   SamePromptIdentity(prompt, attackTargetPrompt);
        }

        private void SuspendAttackTargetingVisuals()
        {
            if (highlightedAttackTarget != null)
                highlightedAttackTarget.SetDropHighlight(false);
            highlightedAttackTarget = null;
            HideAttackArrow();
            if (directAttackTargetButton != null)
                directAttackTargetButton.SetActive(false);
            if (attackTargetingActive)
            {
                attackTargetingActive = false;
                awaitingAttackTargetPrompt = true;
            }
            attackTargetPrompt = null;
            attackTargetCandidates.Clear();
        }

        private void RestartAttackTargetingAfterRetry()
        {
            if (pendingAttackSource == null)
                return;
            SuspendAttackTargetingVisuals();
            awaitingAttackTargetPrompt = true;
            intendedAttackTarget = null;
            intendedDirectAttack = false;
            attackArrowLockedToIntent = false;
        }

        private void CancelAttackTargeting()
        {
            if (attackTargetAutoSubmitRoutine != null)
            {
                StopCoroutine(attackTargetAutoSubmitRoutine);
                attackTargetAutoSubmitRoutine = null;
            }
            if (highlightedAttackTarget != null)
                highlightedAttackTarget.SetDropHighlight(false);
            highlightedAttackTarget = null;
            HideAttackArrow();
            if (directAttackTargetButton != null)
                directAttackTargetButton.SetActive(false);
            draggingAttacker = null;
            capturedAttackPointerId = int.MinValue;
            attackTargetSelectionPointerId = int.MinValue;
            lastAttackPointerPosition = Vector2.zero;
            attackPointerReleaseFallbackAt = -1f;
            pendingAttackSource = null;
            pendingAttackCommand = null;
            pendingAttackCommandRequestId = 0;
            intendedAttackTarget = null;
            intendedDirectAttack = false;
            attackArrowLockedToIntent = false;
            awaitingAttackTargetPrompt = false;
            attackTargetingActive = false;
            attackTargetPrompt = null;
            directAttackTargetChoice = null;
            attackTargetCandidates.Clear();
        }

        private void AbandonAttackTargetingIfSuperseded(DuelPrompt prompt)
        {
            if ((!awaitingAttackTargetPrompt && !attackTargetingActive) ||
                prompt == null || IsAttackTargetSelectionPrompt(prompt) ||
                prompt.RequestId == pendingAttackCommandRequestId ||
                prompt.Message == CoreMessage.SelectBattleCommand)
            {
                return;
            }
            CancelAttackTargeting();
        }
    }
}
