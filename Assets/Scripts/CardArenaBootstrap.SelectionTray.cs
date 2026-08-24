using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Frontend;
using ArcaneDuel.DuelEngine.Diagnostics;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private const float ChoiceCardWidth = 168f;
        private const float DescribedEffectChoiceWidth = 390f;
        private const float ChoiceCardHeight = 244f;
        private const float ChoiceCardSpacing = 14f;
        private const int MaximumVisibleChoiceCards = 5;

        private sealed class ChoiceTrayVisual
        {
            public DuelChoice Choice;
            public Outline Outline;
            public Text Label;
        }

        private readonly Dictionary<int, ChoiceTrayVisual>
            choiceTrayVisuals = new();
        private readonly List<int> orderedPromptIndexes = new();
        private readonly Dictionary<int, ushort> selectedPromptAmounts = new();
        private RectTransform choiceViewport;
        private ScrollRect choiceScroll;
        private Scrollbar choiceScrollbar;
        private Text choiceInstruction;
        private DuelPrompt stagedChoicePrompt;
        private DuelChoice stagedSingleChoice;
        private GameObject compactResponseBar;
        private Text compactResponseText;
        private Text compactResponseHint;
        private Image compactResponseArtwork;
        private Button compactResponseActivateButton;
        private DuelPrompt compactResponsePrompt;
        private DuelChoice compactResponseChoice;
        private readonly DuelResponseWindowLimiter responseWindowLimiter =
            new();

        private bool TryGetRepeatedPhaseResponsePass(
            DuelPrompt prompt,
            out DuelChoice passChoice)
        {
            passChoice = null;
            if (DuelActivationPreferences.ClassicResponseWindows ||
                !IsOptionalLocalResponseWindow(prompt) ||
                state == null ||
                !responseWindowLimiter.IsConsumed(
                    state.TurnNumber,
                    state.Phase))
            {
                return false;
            }

            passChoice = DuelPromptPresentationRules.DeclineChoice(prompt);
            return passChoice?.Response != null &&
                   passChoice.Response.Length > 0;
        }

        private void MarkOptionalResponseDecision(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            if (choice == null ||
                DuelActivationPreferences.ClassicResponseWindows ||
                !IsOptionalLocalResponseWindow(prompt) ||
                state == null)
            {
                return;
            }

            responseWindowLimiter.Consume(state.TurnNumber, state.Phase);
        }

        private static bool IsOptionalLocalResponseWindow(DuelPrompt prompt)
        {
            return prompt != null &&
                   prompt.Player == 0 &&
                   !prompt.Forced &&
                   prompt.Message == CoreMessage.SelectChain &&
                   DuelPromptPresentationRules.DeclineChoice(prompt) != null &&
                   DuelPromptPresentationRules
                       .ActionableResponseChoices(prompt).Count > 0;
        }

        private void BuildChoiceModal()
        {
            choiceModal = CreatePanel(
                frame,
                "Bandeja de Seleção",
                new Vector2(0.225f, 0.27f),
                new Vector2(0.775f, 0.73f),
                Color.white);
            Image background = choiceModal.GetComponent<Image>();
            if (choiceSelectionTemplate != null)
            {
                background.sprite = choiceSelectionTemplate;
                background.type = Image.Type.Simple;
            }
            else
            {
                background.color =
                    new Color(0.006f, 0.025f, 0.045f, 0.985f);
            }
            AddOutline(choiceModal, Cyan);

            choiceTitle = CreateText(
                choiceModal.transform,
                "ESCOLHA UMA CARTA",
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.06f, 0.865f),
                new Vector2(0.94f, 0.965f),
                TextAnchor.MiddleCenter);

            choiceInstruction = CreateText(
                choiceModal.transform,
                "SELECIONE UMA OPÇÃO E CONFIRME",
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.055f, 0.775f),
                new Vector2(0.945f, 0.855f),
                TextAnchor.MiddleCenter);

            GameObject viewportObject = CreatePanel(
                choiceModal.transform,
                "Área de Escolhas",
                new Vector2(0.055f, 0.23f),
                new Vector2(0.945f, 0.755f),
                new Color(0.003f, 0.018f, 0.032f, 0.56f));
            choiceViewport = viewportObject.GetComponent<RectTransform>();
            viewportObject.AddComponent<RectMask2D>();
            choiceContent = CreateRect(
                viewportObject.transform,
                "Cartas para Selecionar",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Vector2.zero);
            choiceContent.pivot = new Vector2(0f, 0.5f);

            choiceScroll = viewportObject.AddComponent<ScrollRect>();
            choiceScroll.viewport = choiceViewport;
            choiceScroll.content = choiceContent;
            choiceScroll.horizontal = true;
            choiceScroll.vertical = false;
            choiceScroll.movementType = ScrollRect.MovementType.Clamped;
            choiceScroll.inertia = true;
            choiceScroll.scrollSensitivity = 42f;
            choiceScroll.decelerationRate = 0.12f;

            GameObject scrollbarTrack = CreatePanel(
                choiceModal.transform,
                "Rolagem Horizontal",
                new Vector2(0.18f, 0.175f),
                new Vector2(0.82f, 0.195f),
                new Color(0.04f, 0.12f, 0.17f, 0.92f));
            Image handle = CreateImage(
                scrollbarTrack.transform,
                "Alça",
                Vector2.zero,
                new Vector2(0.30f, 1f),
                Cyan);
            choiceScrollbar =
                scrollbarTrack.AddComponent<Scrollbar>();
            choiceScrollbar.handleRect = handle.rectTransform;
            choiceScrollbar.targetGraphic = handle;
            choiceScrollbar.direction = Scrollbar.Direction.LeftToRight;
            choiceScroll.horizontalScrollbar = choiceScrollbar;
            choiceScroll.horizontalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;

            choiceConfirm = CreateButton(
                choiceModal.transform,
                "Confirmar Escolha",
                "CONFIRMAR ESCOLHA",
                new Vector2(0.34f, 0.025f),
                new Vector2(0.66f, 0.155f),
                Lime,
                ConfirmMultiSelection);
            choiceConfirm.interactable = false;
            choiceModal.SetActive(false);
            BuildCompactResponseBar();
        }

        private void OpenChoiceModal(
            DuelPrompt prompt,
            IReadOnlyList<DuelChoice> choices)
        {
            if (prompt == null || choices == null || choices.Count == 0)
                return;

            IReadOnlyList<DuelChoice> presentedChoices =
                PresentationChoices(prompt, choices);
            if (presentedChoices.Count == 0)
                presentedChoices = choices;

            int surfaceGeneration = OpenExclusiveDuelUiSurface(
                DuelUiSurfaceKind.PromptPrimary,
                prompt);
            HideCompactResponseBar();
            bool describedEffect = choices.Any(choice =>
                choice != null && choice.DescriptionId != 0);
            bool blockingDecision =
                describedEffect || IsDirectSelectionPrompt(prompt);
            if (blockingDecision)
            {
                // A pergunta do efeito e seus botoes precisam ser a camada
                // principal. Paineis de carta/acao abertos anteriormente nao
                // podem esconder a decisao que mantem o Core aguardando.
                actionPanel?.SetActive(false);
                CloseFieldActionMenu();
                CloseZoneBrowser();
                ClosePhaseNavigator();
                CloseCardDetails();
            }
            ResetChoiceSelectionState();
            stagedChoicePrompt = prompt;
            ApplyChoicePresentationProfile(prompt);
            ResizeChoiceTray(presentedChoices);
            ClearChildren(choiceContent);
            choiceModal.SetActive(true);
            choiceModal.transform.SetAsLastSibling();
            choiceTitle.text =
                ChoicePresentationHeading(prompt).ToUpperInvariant();
            UpdateChoiceInstruction(prompt);
            Canvas.ForceUpdateCanvases();

            float viewportWidth = Mathf.Max(
                1f,
                choiceViewport.rect.width);
            float groupWidth = presentedChoices.Sum(ChoiceWidth) +
                Mathf.Max(0, presentedChoices.Count - 1) * ChoiceCardSpacing;
            float contentWidth = Mathf.Max(viewportWidth, groupWidth + 28f);
            choiceContent.sizeDelta = new Vector2(contentWidth, 0f);
            choiceContent.anchoredPosition = Vector2.zero;
            float start = (contentWidth - groupWidth) * 0.5f;
            float cursor = start;

            foreach ((DuelChoice choice, int index) in
                     presentedChoices.Select(
                         (choice, index) => (choice, index)))
            {
                float width = ChoiceWidth(choice);
                CreateChoiceTrayCard(
                    choice,
                    index,
                    cursor,
                    width,
                    surfaceGeneration);
                cursor += width + ChoiceCardSpacing;
            }

            bool contentOverflows = groupWidth + 28f > viewportWidth + 1f;
            choiceScrollbar.gameObject.SetActive(contentOverflows);
            choiceScroll.horizontalNormalizedPosition = 0f;
            choiceConfirm.gameObject.SetActive(true);
            choiceConfirm.interactable = false;
            UpdateChoiceConfirmLabel(prompt);
            SetDuelExperienceObscured(true);
        }

        private void EnsureRequiredResponseTrayVisible()
        {
            DuelPrompt prompt = core?.CurrentPrompt;
            if (!DuelPromptPresentationRules.RequiresVisibleResponseTray(
                    prompt) ||
                InteractionLocked || choiceModal == null ||
                choiceModal.activeInHierarchy ||
                compactResponseBar?.activeInHierarchy == true ||
                IsAttackTargetingPromptVisible(prompt))
            {
                return;
            }

            // A apresentacao de batalha pode fechar uma janela ja marcada
            // como exibida. Reabra diretamente enquanto o mesmo request ainda
            // aguarda uma resposta, sem depender da identidade em cache.
            selectedPromptIndexes.Clear();
            if (RefreshPrompt(prompt))
                MarkPromptPresented(prompt);
        }

        private void BuildCompactResponseBar()
        {
            compactResponseBar = CreatePanel(
                frame,
                "Ativação de Efeito",
                new Vector2(0.275f, 0.205f),
                new Vector2(0.725f, 0.795f),
                Color.white);
            Image responseBackground = compactResponseBar.GetComponent<Image>();
            if (choiceSelectionTemplate != null)
            {
                responseBackground.sprite = choiceSelectionTemplate;
                responseBackground.type = Image.Type.Simple;
                responseBackground.color = new Color(0.82f, 0.94f, 1f, 1f);
            }
            else
            {
                responseBackground.color =
                    new Color(0.003f, 0.018f, 0.038f, 0.985f);
            }
            AddOutline(compactResponseBar, Cyan);
            CreateImage(
                compactResponseBar.transform,
                "Energia superior da seleção",
                new Vector2(0.075f, 0.952f),
                new Vector2(0.925f, 0.967f),
                Cyan).raycastTarget = false;
            CreateImage(
                compactResponseBar.transform,
                "Energia inferior da seleção",
                new Vector2(0.18f, 0.185f),
                new Vector2(0.82f, 0.191f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.62f))
                .raycastTarget = false;

            GameObject heading = CreatePanel(
                compactResponseBar.transform,
                "Cabeçalho da janela de efeito",
                new Vector2(0.07f, 0.78f),
                new Vector2(0.93f, 0.945f),
                Color.clear);
            AttachDuelSurface(
                heading,
                "Superfície do cabeçalho",
                Cyan,
                true,
                0.94f,
                true,
                9f);
            compactResponseArtwork = CreateImage(
                compactResponseBar.transform,
                "Carta do efeito",
                new Vector2(0.365f, 0.255f),
                new Vector2(0.635f, 0.755f),
                Color.white);
            compactResponseArtwork.preserveAspect = true;
            compactResponseArtwork.raycastTarget = true;
            AddOutline(
                compactResponseArtwork.gameObject,
                new Color(EffectGlow.r, EffectGlow.g, EffectGlow.b, 0.74f));
            Button artworkButton =
                compactResponseArtwork.gameObject.AddComponent<Button>();
            artworkButton.targetGraphic = compactResponseArtwork;
            artworkButton.onClick.AddListener(InspectCompactResponseCard);
            compactResponseText = CreateText(
                heading.transform,
                "VOCÊ PODE RESPONDER",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleCenter);
            compactResponseHint = CreateText(
                compactResponseBar.transform,
                "CLIQUE NA CARTA PARA VER O EFEITO",
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.16f, 0.195f),
                new Vector2(0.84f, 0.245f),
                TextAnchor.MiddleCenter);
            compactResponseActivateButton = CreateButton(
                compactResponseBar.transform,
                "Ativar Efeito",
                "ATIVAR EFEITO",
                new Vector2(0.52f, 0.045f),
                new Vector2(0.92f, 0.155f),
                Cyan,
                OpenCompactResponseChoices);
            PolishPromptButton(compactResponseActivateButton, Cyan, false);
            Button cancelButton = CreateButton(
                compactResponseBar.transform,
                "Cancelar Ativação",
                "CANCELAR",
                new Vector2(0.08f, 0.045f),
                new Vector2(0.48f, 0.155f),
                Gold,
                PassCompactResponse);
            PolishPromptButton(cancelButton, Gold, true);
            compactResponseBar.SetActive(false);
        }

        private static void PolishPromptButton(
            Button button,
            Color accent,
            bool strongOnLeft)
        {
            if (button == null)
                return;
            DuelHudSurfaceGraphic surface = AttachDuelSurface(
                button.gameObject,
                "Superfície moderna",
                accent,
                strongOnLeft,
                0.96f,
                true,
                8f);
            if (surface != null)
            {
                surface.raycastTarget = true;
                button.targetGraphic = surface;
            }
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.88f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            foreach (Text label in button.GetComponentsInChildren<Text>(true))
            {
                label.raycastTarget = false;
                label.color = Color.white;
                label.fontStyle = FontStyle.Bold;
            }
        }

        private void ShowCompactResponseBar(DuelPrompt prompt)
        {
            if (compactResponseBar == null || prompt == null)
                return;
            OpenExclusiveDuelUiSurface(
                DuelUiSurfaceKind.PromptPrimary,
                prompt);
            compactResponsePrompt = prompt;
            int responses = DuelPromptPresentationRules
                .ActionableResponseChoices(prompt)
                .Count;
            DuelChoice response = DuelPromptPresentationRules
                .ActionableResponseChoices(prompt)
                .FirstOrDefault();
            // Mesmo quando ha mais de uma resposta, mantenha uma carta
            // representativa no centro. O botao de ativacao abre a bandeja
            // completa para a escolha da resposta correta.
            compactResponseChoice = response;
            bool showArtwork = CanInspectChoiceIdentity(
                compactResponseChoice);
            if (compactResponseArtwork != null)
            {
                compactResponseArtwork.gameObject.SetActive(showArtwork);
                compactResponseArtwork.sprite = showArtwork
                    ? SpriteFor(compactResponseChoice.CardCode)
                    : null;
            }
            string question = prompt.Message == CoreMessage.SelectChain
                ? "ATIVAR UMA CARTA OU EFEITO?"
                : "ATIVAR O EFEITO DESTA CARTA?";
            compactResponseText.text = responses == 1 && response != null
                ? question + "\n" + CardName(response.CardCode)
                : question + $"\n{responses} OPÇÕES DISPONÍVEIS";
            if (compactResponseHint != null)
            {
                compactResponseHint.text = showArtwork
                    ? "CLIQUE NA CARTA PARA VER O EFEITO"
                    : "ATIVAR EFEITO ABRE AS OPÇÕES DISPONÍVEIS";
            }
            if (compactResponseActivateButton != null)
                compactResponseActivateButton.interactable = responses > 0;
            compactResponseBar.SetActive(true);
            compactResponseBar.transform.SetAsLastSibling();
        }

        private void HideCompactResponseBar()
        {
            if (compactResponseBar != null)
                compactResponseBar.SetActive(false);
            compactResponsePrompt = null;
            compactResponseChoice = null;
        }

        private void InspectCompactResponseCard()
        {
            if (CanInspectChoiceIdentity(compactResponseChoice))
                ShowChoiceInspector(compactResponseChoice);
        }

        private void OpenCompactResponseChoices()
        {
            if (InteractionLocked)
                return;
            DuelPrompt prompt = core?.CurrentPrompt;
            if (!SamePromptIdentity(prompt, compactResponsePrompt))
            {
                HideCompactResponseBar();
                return;
            }
            List<DuelChoice> responses = DuelPromptPresentationRules
                .ActionableResponseChoices(prompt);
            HideCompactResponseBar();
            if (responses.Count == 1)
            {
                CloseCardDetails();
                MarkOptionalResponseDecision(prompt, responses[0]);
                core.SubmitChoice(responses[0]);
                RefreshEverything(true);
                return;
            }
            OpenChoiceModal(prompt, responses);
        }

        private void PassCompactResponse()
        {
            if (InteractionLocked)
                return;
            DuelPrompt prompt = core?.CurrentPrompt;
            if (!SamePromptIdentity(prompt, compactResponsePrompt))
            {
                HideCompactResponseBar();
                return;
            }
            DuelChoice decline =
                DuelPromptPresentationRules.DeclineChoice(prompt);
            if (decline == null)
                return;
            HideCompactResponseBar();
            CloseCardDetails();
            MarkOptionalResponseDecision(prompt, decline);
            core.SubmitChoice(decline);
            RefreshEverything(true);
        }

        private void CreateChoiceTrayCard(
            DuelChoice choice,
            int index,
            float x,
            float width,
            int surfaceGeneration)
        {
            bool describedEffect = choice.DescriptionId != 0;
            GameObject card = CreatePanel(
                choiceContent,
                $"Escolha {index + 1}",
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.10f, 0.16f, 0.98f));
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(
                width,
                ChoiceCardHeight);
            rect.anchoredPosition = new Vector2(x, 0f);
            AddOutline(card, DimmedChoiceAccent());
            Outline outline = card.GetComponent<Outline>();
            var visual = new ChoiceTrayVisual
            {
                Choice = choice,
                Outline = outline
            };
            choiceTrayVisuals[index] = visual;

            var button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            bool visibleIdentity = CanInspectChoiceIdentity(choice);
            if (choice.CardCode != 0 && !describedEffect)
            {
                Image art = CreateImage(
                    card.transform,
                    "Arte",
                    new Vector2(0.08f, 0.20f),
                    new Vector2(0.92f, 0.97f),
                    Color.white);
                art.sprite = visibleIdentity
                    ? SpriteFor(choice.CardCode)
                    : cardBackSprite;
                art.preserveAspect = true;
            }
            visual.Label = CreateText(
                card.transform,
                ChoiceTrayLabel(choice),
                describedEffect ? 14 : 11,
                FontStyle.Bold,
                Color.white,
                describedEffect
                    ? new Vector2(0.07f, 0.08f)
                    : new Vector2(0.03f, 0.01f),
                describedEffect
                    ? new Vector2(0.93f, 0.92f)
                    : new Vector2(
                        0.97f,
                        choice.CardCode == 0 ? 0.92f : 0.21f),
                TextAnchor.MiddleCenter);

            DuelChoice capturedChoice = choice;
            int capturedIndex = index;
            button.onClick.AddListener(
                () =>
                {
                    if (!IsDuelUiGenerationCurrent(
                            surfaceGeneration,
                            DuelUiSurfaceKind.PromptPrimary))
                    {
                        return;
                    }
                    StageChoiceFromTray(capturedChoice, capturedIndex);
                });
        }

        private void StageChoiceFromTray(
            DuelChoice choice,
            int visualIndex)
        {
            DuelPrompt prompt = core?.CurrentPrompt;
            if (choice == null || prompt == null ||
                !SamePromptIdentity(prompt, stagedChoicePrompt))
            {
                CloseChoiceModal();
                return;
            }

            if (prompt.RequiresOrderedSelection && choice.ChoiceIndex >= 0)
            {
                stagedSingleChoice = null;
                if (orderedPromptIndexes.Contains(choice.ChoiceIndex))
                    orderedPromptIndexes.Remove(choice.ChoiceIndex);
                else
                    orderedPromptIndexes.Add(choice.ChoiceIndex);
                selectedPromptIndexes.Clear();
                foreach (int index in orderedPromptIndexes)
                    selectedPromptIndexes.Add(index);
            }
            else if (prompt.Message == CoreMessage.SelectCounter &&
                     choice.ChoiceIndex >= 0)
            {
                stagedSingleChoice = null;
                selectedPromptAmounts.TryGetValue(
                    choice.ChoiceIndex,
                    out ushort current);
                ushort capacity = (ushort)Mathf.Min(
                    ushort.MaxValue,
                    choice.SumValue);
                ushort next = current >= capacity
                    ? (ushort)0
                    : (ushort)(current + 1);
                int totalWithoutCurrent = selectedPromptAmounts.Values.Sum(
                    value => (int)value) - current;
                if (totalWithoutCurrent + next >
                    prompt.RequiredCounterCount)
                {
                    next = 0;
                }
                if (next == 0)
                {
                    selectedPromptAmounts.Remove(choice.ChoiceIndex);
                    selectedPromptIndexes.Remove(choice.ChoiceIndex);
                }
                else
                {
                    selectedPromptAmounts[choice.ChoiceIndex] = next;
                    selectedPromptIndexes.Add(choice.ChoiceIndex);
                }
            }
            else if (IsMultiChoicePrompt(prompt) && choice.ChoiceIndex >= 0)
            {
                stagedSingleChoice = null;
                if (!selectedPromptIndexes.Add(choice.ChoiceIndex))
                    selectedPromptIndexes.Remove(choice.ChoiceIndex);
                while (selectedPromptIndexes.Count > prompt.MaximumSelections)
                    selectedPromptIndexes.Remove(
                        selectedPromptIndexes.First());
            }
            else
            {
                if (IsMultiChoicePrompt(prompt))
                {
                    selectedPromptIndexes.Clear();
                    orderedPromptIndexes.Clear();
                    selectedPromptAmounts.Clear();
                }
                stagedSingleChoice = choice;
            }

            UpdateChoiceTrayVisuals(visualIndex);
            ShowChoiceInspector(choice);
            choiceConfirm.interactable = IsMultiChoicePrompt(prompt)
                ? stagedSingleChoice != null ||
                  IsStructuredSelectionValid(prompt)
                : stagedSingleChoice != null;
            UpdateChoiceConfirmLabel(prompt);
            UpdateChoiceInstruction(prompt);
            SetStatus(
                CanInspectChoiceIdentity(choice)
                    ? $"{CardName(choice.CardCode)} selecionada. Confirme a escolha."
                    : choice.CardCode != 0
                        ? "Carta virada para baixo selecionada. Confirme a escolha."
                    : "Opção selecionada. Confirme para continuar.",
                choiceModalAccent);
        }

        private void ConfirmMultiSelection()
        {
            DuelPrompt prompt = core?.CurrentPrompt;
            if (prompt == null ||
                !SamePromptIdentity(prompt, stagedChoicePrompt))
            {
                CloseChoiceModal();
                return;
            }

            if (IsMultiChoicePrompt(prompt))
            {
                if (stagedSingleChoice != null)
                {
                    MarkOptionalResponseDecision(
                        prompt,
                        stagedSingleChoice);
                    core.SubmitChoice(stagedSingleChoice);
                }
                else if (!IsStructuredSelectionValid(prompt))
                {
                    return;
                }
                else
                {
                    int[] indexes = prompt.RequiresOrderedSelection
                        ? orderedPromptIndexes.ToArray()
                        : selectedPromptIndexes
                            .OrderBy(index => index)
                            .ToArray();
                    byte[] response = BuildStructuredResponse(
                        prompt,
                        indexes);
                    string selectedCards = string.Join(
                        ",",
                        indexes.Select(index =>
                        {
                            DuelChoice selected = prompt.Choices
                                .FirstOrDefault(candidate =>
                                    candidate.ChoiceIndex == index);
                            return selected == null
                                ? $"{index}:missing"
                                : $"{index}:{selected.CardCode}:" +
                                  $"P{selected.Controller}/" +
                                  $"L{selected.Location:X2}/" +
                                  $"S{selected.Sequence}";
                        }));
                    bool submitted = core.SubmitCoreResponse(
                        response,
                        prompt.RequestId);
                    RuntimeDiagnosticRecorder.Record(
                        submitted
                            ? "EFFECT_TARGET_CONFIRMATION"
                            : "EFFECT_TARGET_REJECTED",
                        "EffectFlow",
                        nameof(CardArenaBootstrap),
                        submitted
                            ? "The selected effect targets were delivered to the Core."
                            : "The selected effect targets could not be delivered to the Core.",
                        submitted
                            ? RuntimeDiagnosticSeverity.Info
                            : RuntimeDiagnosticSeverity.Error,
                        indexes.Select(index => prompt.Choices
                                .FirstOrDefault(candidate =>
                                    candidate.ChoiceIndex == index)
                                ?.CardCode ?? 0U)
                            .FirstOrDefault(),
                        prompt.Player,
                        core.IsNetworkReplica
                            ? "online-replica"
                            : "local",
                        $"request={prompt.RequestId}; " +
                        $"message={prompt.Message}; " +
                        $"indexes=[{string.Join(",", indexes)}]; " +
                        $"cards=[{selectedCards}]; " +
                        $"response={System.BitConverter.ToString(response)}");
                    if (!submitted)
                    {
                        SetStatus(
                            "A seleção continua aberta. Confirme os alvos novamente.",
                            Red);
                        choiceModal.transform.SetAsLastSibling();
                        return;
                    }
                }
            }
            else
            {
                if (stagedSingleChoice == null)
                    return;
                MarkOptionalResponseDecision(prompt, stagedSingleChoice);
                core.SubmitChoice(stagedSingleChoice);
            }
            CloseChoiceModal();
            RefreshEverything(true);
        }

        private void UpdateChoiceTrayVisuals(int visualIndex)
        {
            foreach ((int index, ChoiceTrayVisual visual) in
                     choiceTrayVisuals)
            {
                bool selected = IsMultiChoicePrompt(stagedChoicePrompt)
                    ? stagedSingleChoice == visual.Choice ||
                      selectedPromptIndexes.Contains(
                          visual.Choice.ChoiceIndex)
                    : index == visualIndex &&
                      visual.Choice == stagedSingleChoice;
                visual.Outline.effectColor = selected
                    ? choiceModalAccent
                    : DimmedChoiceAccent();
                visual.Outline.effectDistance = selected
                    ? new Vector2(5f, -5f)
                    : new Vector2(2f, -2f);
                if (visual.Label != null)
                {
                    string suffix = string.Empty;
                    if (stagedChoicePrompt?.RequiresOrderedSelection == true)
                    {
                        int order = orderedPromptIndexes.IndexOf(
                            visual.Choice.ChoiceIndex);
                        if (order >= 0)
                            suffix = $"\nORDEM {order + 1}";
                    }
                    else if (stagedChoicePrompt?.Message ==
                             CoreMessage.SelectCounter &&
                             selectedPromptAmounts.TryGetValue(
                                 visual.Choice.ChoiceIndex,
                                 out ushort amount))
                    {
                        suffix = $"\nALOCADO: {amount}";
                    }
                    visual.Label.text = ChoiceTrayLabel(visual.Choice) + suffix;
                }
            }
        }

        private string ChoiceTrayLabel(DuelChoice choice)
        {
            string label = choice != null && choice.CardCode != 0 &&
                           !CanInspectChoiceIdentity(choice)
                ? "CARTA VIRADA PARA BAIXO"
                : ChoiceLabel(choice);
            if (choice == null || !choice.HasLocation ||
                !IsCardSelectionMessage(stagedChoicePrompt?.Message))
            {
                return label;
            }

            string metadata = ChoiceLocationLabel(choice);
            if (stagedChoicePrompt.Message == CoreMessage.SelectSum &&
                choice.SumValue != 0)
            {
                uint first = choice.SumValue & 0xFFFF;
                uint alternative = choice.SumValue >> 16;
                metadata += alternative != 0 && alternative != first
                    ? $" · VALOR {first} OU {alternative}"
                    : $" · VALOR {first}";
            }
            else if (stagedChoicePrompt.Message ==
                     CoreMessage.SelectTribute && choice.SumValue != 0)
            {
                metadata += $" · TRIBUTO {choice.SumValue}";
            }
            return string.IsNullOrWhiteSpace(metadata)
                ? label
                : label + "\n" + metadata;
        }

        private void UpdateChoiceConfirmLabel(DuelPrompt prompt)
        {
            Text label = choiceConfirm?.GetComponentInChildren<Text>();
            if (label == null)
                return;
            if (IsMultiChoicePrompt(prompt) &&
                stagedSingleChoice == null &&
                prompt.Message == CoreMessage.SelectCounter)
            {
                int total = selectedPromptAmounts.Values.Sum(
                    value => (int)value);
                label.text = $"CONFIRMAR - {total}/{prompt.RequiredCounterCount}";
                return;
            }
            if (IsMultiChoicePrompt(prompt) &&
                stagedSingleChoice == null &&
                prompt.Message == CoreMessage.SelectTribute)
            {
                uint tributeValue = (uint)prompt.Choices
                    .Where(choice => choice != null &&
                                     selectedPromptIndexes.Contains(
                                         choice.ChoiceIndex))
                    .Sum(choice => (long)choice.SumValue);
                label.text = $"CONFIRMAR - TRIBUTO " +
                             $"{tributeValue}/{prompt.MinimumSelections}";
                return;
            }
            label.text = IsMultiChoicePrompt(prompt) &&
                         stagedSingleChoice == null
                ? $"CONFIRMAR · {selectedPromptIndexes.Count}/{prompt.MaximumSelections}"
                : "CONFIRMAR ESCOLHA";
        }

        private void ResizeChoiceTray(
            IReadOnlyList<DuelChoice> choices)
        {
            int count = choices?.Count ?? 0;
            int visible = Mathf.Clamp(count, 1, MaximumVisibleChoiceCards);
            float visibleWidth = (choices ?? new List<DuelChoice>())
                .Take(visible)
                .Sum(ChoiceWidth) +
                Mathf.Max(0, visible - 1) * ChoiceCardSpacing + 116f;
            float frameWidth = Mathf.Max(960f, frame.rect.width);
            float frameHeight = Mathf.Max(540f, frame.rect.height);
            float width = Mathf.Clamp(
                visibleWidth / frameWidth,
                0.38f,
                0.76f);
            float height = Mathf.Clamp(
                472f / frameHeight,
                0.44f,
                0.78f);
            const float centerY = 0.515f;
            RectTransform rect = choiceModal.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(
                0.5f - width * 0.5f,
                centerY - height * 0.5f);
            rect.anchorMax = new Vector2(
                0.5f + width * 0.5f,
                centerY + height * 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private Color DimmedChoiceAccent()
        {
            return new Color(
                choiceModalAccent.r * 0.48f,
                choiceModalAccent.g * 0.48f,
                choiceModalAccent.b * 0.48f,
                0.92f);
        }

        private static float ChoiceWidth(DuelChoice choice)
        {
            return choice != null && choice.DescriptionId != 0
                ? DescribedEffectChoiceWidth
                : ChoiceCardWidth;
        }

        private static bool IsMultiChoicePrompt(DuelPrompt prompt)
        {
            return prompt != null &&
                   (prompt.RequiresOrderedSelection ||
                    prompt.RequiresMaskSelection ||
                    prompt.Message == CoreMessage.SelectCounter ||
                    prompt.Message == CoreMessage.SelectCard ||
                    prompt.Message == CoreMessage.SelectTribute ||
                    prompt.Message == CoreMessage.SelectSum) &&
                   (prompt.RequiresOrderedSelection ||
                    prompt.Message == CoreMessage.SelectCounter ||
                    prompt.MaximumSelections > 1);
        }

        private bool IsStructuredSelectionValid(DuelPrompt prompt)
        {
            if (prompt == null)
                return false;
            if (prompt.RequiresOrderedSelection)
                return orderedPromptIndexes.Count == prompt.MaximumSelections &&
                       orderedPromptIndexes.Distinct().Count() ==
                       orderedPromptIndexes.Count;
            if (prompt.Message == CoreMessage.SelectCounter)
            {
                return selectedPromptAmounts.Values.Sum(
                    value => (int)value) == prompt.RequiredCounterCount &&
                    selectedPromptAmounts.All(item =>
                    {
                        DuelChoice choice = prompt.Choices.FirstOrDefault(
                            candidate => candidate.ChoiceIndex == item.Key);
                        return choice != null && item.Value <= choice.SumValue;
                    });
            }
            return CoreMessageDecoder.IsValidSelection(
                prompt,
                selectedPromptIndexes);
        }

        private byte[] BuildStructuredResponse(
            DuelPrompt prompt,
            IReadOnlyList<int> indexes)
        {
            if (prompt.RequiresOrderedSelection)
                return CoreMessageDecoder.OrderedSelectionResponse(indexes);
            if (prompt.Message == CoreMessage.SelectCounter)
            {
                var allocation = new ushort[
                    checked((int)prompt.MaximumSelections)];
                foreach ((int index, ushort amount) in selectedPromptAmounts)
                {
                    if (index >= 0 && index < allocation.Length)
                        allocation[index] = amount;
                }
                return CoreMessageDecoder.CounterResponse(allocation);
            }
            if (prompt.RequiresMaskSelection)
                return CoreMessageDecoder.AnnounceMaskResponse(prompt, indexes);
            return CoreMessageDecoder.CardSelectionResponse(
                indexes.Select(index => (uint)index).ToArray());
        }

        private void ResetChoiceSelectionState()
        {
            selectedPromptIndexes.Clear();
            orderedPromptIndexes.Clear();
            selectedPromptAmounts.Clear();
            choiceTrayVisuals.Clear();
            stagedChoicePrompt = null;
            stagedSingleChoice = null;
        }
    }
}
