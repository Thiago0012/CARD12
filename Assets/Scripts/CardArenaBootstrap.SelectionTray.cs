using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private const float ChoiceCardWidth = 168f;
        private const float ChoiceCardHeight = 244f;
        private const float ChoiceCardSpacing = 14f;
        private const int MaximumVisibleChoiceCards = 5;

        private sealed class ChoiceTrayVisual
        {
            public DuelChoice Choice;
            public Outline Outline;
        }

        private readonly Dictionary<int, ChoiceTrayVisual>
            choiceTrayVisuals = new();
        private RectTransform choiceViewport;
        private ScrollRect choiceScroll;
        private Scrollbar choiceScrollbar;
        private DuelPrompt stagedChoicePrompt;
        private DuelChoice stagedSingleChoice;
        private GameObject compactResponseBar;
        private Text compactResponseText;
        private DuelPrompt compactResponsePrompt;

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
                new Vector2(0.06f, 0.82f),
                new Vector2(0.94f, 0.95f),
                TextAnchor.MiddleCenter);

            GameObject viewportObject = CreatePanel(
                choiceModal.transform,
                "Área de Escolhas",
                new Vector2(0.055f, 0.23f),
                new Vector2(0.945f, 0.80f),
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
            choiceScroll.movementType = ScrollRect.MovementType.Elastic;
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

            HideCompactResponseBar();
            ResetChoiceSelectionState();
            stagedChoicePrompt = prompt;
            ApplyChoicePresentationProfile(prompt);
            ResizeChoiceTray(choices.Count);
            ClearChildren(choiceContent);
            choiceModal.SetActive(true);
            choiceModal.transform.SetAsLastSibling();
            choiceTitle.text =
                ChoicePresentationHeading(prompt).ToUpperInvariant();
            Canvas.ForceUpdateCanvases();

            float viewportWidth = Mathf.Max(
                1f,
                choiceViewport.rect.width);
            float groupWidth =
                choices.Count * ChoiceCardWidth +
                Mathf.Max(0, choices.Count - 1) * ChoiceCardSpacing;
            float contentWidth = Mathf.Max(viewportWidth, groupWidth + 28f);
            choiceContent.sizeDelta = new Vector2(contentWidth, 0f);
            choiceContent.anchoredPosition = Vector2.zero;
            float start = (contentWidth - groupWidth) * 0.5f;

            foreach ((DuelChoice choice, int index) in
                     choices.Select((choice, index) => (choice, index)))
            {
                CreateChoiceTrayCard(choice, index, start);
            }

            choiceScrollbar.gameObject.SetActive(
                choices.Count > MaximumVisibleChoiceCards);
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
                IsPromptPresentationCurrent(prompt))
            {
                return;
            }
            RefreshEverything(false);
        }

        private void BuildCompactResponseBar()
        {
            compactResponseBar = CreatePanel(
                frame,
                "Resposta Rápida",
                new Vector2(0.34f, 0.72f),
                new Vector2(0.66f, 0.81f),
                new Color(0.025f, 0.11f, 0.13f, 0.97f));
            AddOutline(compactResponseBar, EffectGlow);
            compactResponseText = CreateText(
                compactResponseBar.transform,
                "VOCÊ PODE RESPONDER",
                14,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.10f),
                new Vector2(0.47f, 0.90f),
                TextAnchor.MiddleCenter);
            CreateButton(
                compactResponseBar.transform,
                "Responder",
                "RESPONDER",
                new Vector2(0.50f, 0.16f),
                new Vector2(0.73f, 0.84f),
                EffectGlow,
                OpenCompactResponseChoices);
            CreateButton(
                compactResponseBar.transform,
                "Passar",
                "PASSAR",
                new Vector2(0.75f, 0.16f),
                new Vector2(0.97f, 0.84f),
                Muted,
                PassCompactResponse);
            compactResponseBar.SetActive(false);
        }

        private void ShowCompactResponseBar(DuelPrompt prompt)
        {
            if (compactResponseBar == null || prompt == null)
                return;
            compactResponsePrompt = prompt;
            int responses = DuelPromptPresentationRules
                .ActionableResponseChoices(prompt)
                .Count;
            compactResponseText.text = responses == 1
                ? "VOCÊ PODE RESPONDER"
                : $"{responses} RESPOSTAS DISPONÍVEIS";
            compactResponseBar.SetActive(true);
            compactResponseBar.transform.SetAsLastSibling();
        }

        private void HideCompactResponseBar()
        {
            if (compactResponseBar != null)
                compactResponseBar.SetActive(false);
            compactResponsePrompt = null;
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
            core.SubmitChoice(decline);
            RefreshEverything(true);
        }

        private void CreateChoiceTrayCard(
            DuelChoice choice,
            int index,
            float start)
        {
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
                ChoiceCardWidth,
                ChoiceCardHeight);
            rect.anchoredPosition = new Vector2(
                start + index * (ChoiceCardWidth + ChoiceCardSpacing),
                0f);
            AddOutline(card, DimmedChoiceAccent());
            Outline outline = card.GetComponent<Outline>();
            choiceTrayVisuals[index] = new ChoiceTrayVisual
            {
                Choice = choice,
                Outline = outline
            };

            var button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            if (choice.CardCode != 0)
            {
                Image art = CreateImage(
                    card.transform,
                    "Arte",
                    new Vector2(0.08f, 0.20f),
                    new Vector2(0.92f, 0.97f),
                    Color.white);
                art.sprite = SpriteFor(choice.CardCode);
                art.preserveAspect = true;
            }
            CreateText(
                card.transform,
                ChoiceLabel(choice),
                11,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.03f, 0.01f),
                new Vector2(0.97f, choice.CardCode == 0 ? 0.92f : 0.21f),
                TextAnchor.MiddleCenter);

            DuelChoice capturedChoice = choice;
            int capturedIndex = index;
            button.onClick.AddListener(
                () => StageChoiceFromTray(capturedChoice, capturedIndex));
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

            if (choice.CardCode != 0)
                ShowInspector(choice.CardCode);

            if (IsMultiChoicePrompt(prompt) && choice.ChoiceIndex >= 0)
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
                    selectedPromptIndexes.Clear();
                stagedSingleChoice = choice;
            }

            UpdateChoiceTrayVisuals(visualIndex);
            choiceConfirm.interactable = IsMultiChoicePrompt(prompt)
                ? stagedSingleChoice != null ||
                  CoreMessageDecoder.IsValidSelection(
                      prompt,
                      selectedPromptIndexes)
                : stagedSingleChoice != null;
            UpdateChoiceConfirmLabel(prompt);
            SetStatus(
                choice.CardCode != 0
                    ? $"{CardName(choice.CardCode)} selecionada. Confirme a escolha."
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
                    core.SubmitChoice(stagedSingleChoice);
                }
                else if (!CoreMessageDecoder.IsValidSelection(
                        prompt,
                        selectedPromptIndexes))
                {
                    return;
                }
                else
                {
                    core.SubmitCoreResponse(
                        CoreMessageDecoder.CardSelectionResponse(
                            selectedPromptIndexes
                                .OrderBy(index => index)
                                .Select(index => (uint)index)
                                .ToArray()),
                        prompt.RequestId);
                }
            }
            else
            {
                if (stagedSingleChoice == null)
                    return;
                core.SubmitChoice(stagedSingleChoice);
            }
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
            }
        }

        private void UpdateChoiceConfirmLabel(DuelPrompt prompt)
        {
            Text label = choiceConfirm?.GetComponentInChildren<Text>();
            if (label == null)
                return;
            label.text = IsMultiChoicePrompt(prompt) &&
                         stagedSingleChoice == null
                ? $"CONFIRMAR · {selectedPromptIndexes.Count}/{prompt.MaximumSelections}"
                : "CONFIRMAR ESCOLHA";
        }

        private void ResizeChoiceTray(int count)
        {
            int visible = Mathf.Clamp(
                count,
                1,
                MaximumVisibleChoiceCards);
            float width = 0.22f + visible * 0.066f;
            RectTransform rect = choiceModal.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f - width * 0.5f, 0.27f);
            rect.anchorMax = new Vector2(0.5f + width * 0.5f, 0.73f);
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

        private static bool IsMultiChoicePrompt(DuelPrompt prompt)
        {
            return prompt != null &&
                   (prompt.Message == CoreMessage.SelectCard ||
                    prompt.Message == CoreMessage.SelectTribute ||
                    prompt.Message == CoreMessage.SelectSum) &&
                   prompt.MaximumSelections > 1;
        }

        private void ResetChoiceSelectionState()
        {
            selectedPromptIndexes.Clear();
            choiceTrayVisuals.Clear();
            stagedChoicePrompt = null;
            stagedSingleChoice = null;
        }
    }
}
