using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private enum DuelUiSurfaceKind
        {
            None,
            PromptPrimary,
            ZoneBrowser,
            CardInspector,
            FieldAction,
            PhaseNavigator,
            DuelHistory,
            DeveloperMenu
        }

        private DuelUiSurfaceKind activeDuelUiSurface;
        private int duelUiSurfaceGeneration;
        private int duelUiInputBlockedFrame = -1;
        private DuelPrompt suspendedDuelPrompt;

        private int OpenExclusiveDuelUiSurface(
            DuelUiSurfaceKind next,
            DuelPrompt prompt = null)
        {
            bool promptWasVisible =
                choiceModal?.activeInHierarchy == true ||
                compactResponseBar?.activeInHierarchy == true ||
                attackTargetingActive;
            if (next != DuelUiSurfaceKind.PromptPrimary && promptWasVisible)
                suspendedDuelPrompt = core?.CurrentPrompt;

            duelUiSurfaceGeneration++;
            duelUiInputBlockedFrame = Time.frameCount;
            HideExclusiveDuelUiSurfacesExcept(next);
            activeDuelUiSurface = next;

            if (next == DuelUiSurfaceKind.PromptPrimary && prompt != null &&
                !SamePromptIdentity(suspendedDuelPrompt, prompt))
            {
                suspendedDuelPrompt = null;
            }

            SetDuelExperienceObscured(next != DuelUiSurfaceKind.None);
            return duelUiSurfaceGeneration;
        }

        private void HideExclusiveDuelUiSurfacesExcept(
            DuelUiSurfaceKind retained)
        {
            actionPanel?.SetActive(false);

            if (retained != DuelUiSurfaceKind.PromptPrimary)
            {
                choiceModal?.SetActive(false);
                compactResponseBar?.SetActive(false);
                ResetChoiceSelectionState();
                compactResponsePrompt = null;
                compactResponseChoice = null;
            }

            if (retained != DuelUiSurfaceKind.ZoneBrowser)
            {
                zoneBrowser?.SetActive(false);
                ResetZoneBrowserSelection();
            }

            if (retained != DuelUiSurfaceKind.CardInspector)
                HideCardInspectorVisuals();

            if (retained != DuelUiSurfaceKind.FieldAction)
                HideFieldActionVisuals();

            if (retained != DuelUiSurfaceKind.PhaseNavigator)
                phaseNavigator?.SetActive(false);

            if (retained != DuelUiSurfaceKind.DuelHistory)
                duelHistoryOverlay?.SetActive(false);

            if (retained != DuelUiSurfaceKind.DeveloperMenu)
                developerMenuOverlay?.SetActive(false);

            if (retained != DuelUiSurfaceKind.PromptPrimary)
                SuspendAttackTargetingVisuals();
        }

        private void HideCardInspectorVisuals()
        {
            detailPanel?.SetActive(false);
            detailZoomOverlay?.SetActive(false);
            inspectedCode = 0;
            inspectedZone = null;
        }

        private void HideFieldActionVisuals()
        {
            fieldActionPanel?.SetActive(false);
            fieldActionZone = null;
            fieldActionPrompt = null;
            fieldActionEffectChoices.Clear();
        }

        private bool IsDuelUiGenerationCurrent(
            int generation,
            DuelUiSurfaceKind expected)
        {
            return generation == duelUiSurfaceGeneration &&
                   activeDuelUiSurface == expected;
        }

        private bool IsDuelUiInputBlockedThisFrame =>
            duelUiInputBlockedFrame == Time.frameCount;

        private void MarkDuelUiSurfaceClosed(DuelUiSurfaceKind surface)
        {
            if (activeDuelUiSurface != surface)
                return;
            activeDuelUiSurface = DuelUiSurfaceKind.None;
            duelUiSurfaceGeneration++;
            duelUiInputBlockedFrame = Time.frameCount;
            SetDuelExperienceObscured(false);
        }

        private void CloseCardDetailsFromUser()
        {
            CloseCardDetails();
            if (activeDuelUiSurface == DuelUiSurfaceKind.CardInspector)
            {
                MarkDuelUiSurfaceClosed(DuelUiSurfaceKind.CardInspector);
                RestoreSuspendedPromptIfCurrent();
            }
        }

        private void CloseZoneBrowserFromUser()
        {
            CloseZoneBrowser();
            MarkDuelUiSurfaceClosed(DuelUiSurfaceKind.ZoneBrowser);
            RestoreSuspendedPromptIfCurrent();
        }

        private void ClosePhaseNavigatorFromUser()
        {
            ClosePhaseNavigator();
            MarkDuelUiSurfaceClosed(DuelUiSurfaceKind.PhaseNavigator);
            RestoreSuspendedPromptIfCurrent();
        }

        private void RestoreSuspendedPromptIfCurrent()
        {
            DuelPrompt suspended = suspendedDuelPrompt;
            suspendedDuelPrompt = null;
            if (suspended == null || InteractionLocked ||
                !SamePromptIdentity(core?.CurrentPrompt, suspended))
            {
                return;
            }

            ResetPromptPresentationIdentity();
            RefreshEverything(true);
        }

        private void HandleDuelUiBackInput()
        {
            bool backPressed =
                Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            bool rightClick =
                Mouse.current?.rightButton.wasPressedThisFrame == true;
            if (!backPressed && !rightClick)
                return;

            if (zoneBrowser?.activeInHierarchy == true)
            {
                CloseZoneBrowserFromUser();
                return;
            }
            if (duelHistoryOverlay?.activeInHierarchy == true)
            {
                CloseDuelHistory();
                return;
            }
            if (developerMenuOverlay?.activeInHierarchy == true)
            {
                CloseDeveloperCardMenu();
                return;
            }
            if (phaseNavigator?.activeInHierarchy == true)
            {
                ClosePhaseNavigatorFromUser();
                return;
            }
            if (detailZoomOverlay?.activeInHierarchy == true)
            {
                CloseDetailZoom();
                duelUiInputBlockedFrame = Time.frameCount;
                return;
            }
            if (detailPanel?.activeInHierarchy == true &&
                activeDuelUiSurface == DuelUiSurfaceKind.CardInspector)
            {
                CloseCardDetailsFromUser();
                return;
            }
            if (fieldActionPanel?.activeInHierarchy == true)
            {
                CloseFieldActionMenu();
                MarkDuelUiSurfaceClosed(DuelUiSurfaceKind.FieldAction);
                RestoreSuspendedPromptIfCurrent();
                return;
            }
            if (choiceModal?.activeInHierarchy == true ||
                compactResponseBar?.activeInHierarchy == true)
            {
                TryCancelCurrentPromptFromBack();
            }
        }

        private void TryCancelCurrentPromptFromBack()
        {
            DuelPrompt prompt = core?.CurrentPrompt;
            if (prompt == null || !prompt.Cancelable ||
                (attackTargetingActive && directAttackTargetChoice != null))
                return;
            DuelChoice cancel = DuelPromptPresentationRules.DeclineChoice(prompt);
            if (cancel == null ||
                !CoreCardActionBinding.BelongsToRequest(prompt, cancel))
            {
                return;
            }

            duelUiInputBlockedFrame = Time.frameCount;
            if (attackTargetingActive)
                CancelAttackTargeting();
            CloseChoiceModal();
            HideCompactResponseBar();
            MarkOptionalResponseDecision(prompt, cancel);
            core.SubmitChoice(cancel);
            RefreshEverything(true);
        }
    }
}
