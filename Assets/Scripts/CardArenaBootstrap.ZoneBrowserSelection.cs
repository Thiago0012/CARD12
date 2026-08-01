using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private readonly List<Outline> zoneBrowserChoiceOutlines = new();
        private Button zoneBrowserConfirm;
        private DuelPrompt zoneBrowserPrompt;
        private IReadOnlyList<DuelChoice> zoneBrowserStagedChoices;
        private Outline zoneBrowserSelectedOutline;

        private void BuildZoneBrowserConfirmation(Transform tray)
        {
            zoneBrowserConfirm = CreateButton(
                tray,
                "Confirmar Carta da Zona",
                "CONFIRMAR ESCOLHA",
                new Vector2(0.34f, 0.025f),
                new Vector2(0.66f, 0.145f),
                EffectGlow,
                ConfirmZoneBrowserSelection);
            zoneBrowserConfirm.interactable = false;
        }

        private void ConfigureZoneBrowserTrayArtwork()
        {
            if (zoneBrowserTray == null || choiceSelectionTemplate == null)
                return;
            Image image = zoneBrowserTray.GetComponent<Image>();
            image.sprite = choiceSelectionTemplate;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }

        private void ResizeZoneBrowserTray(int cardCount)
        {
            if (zoneBrowserTray == null)
                return;
            int visible = Mathf.Clamp(
                cardCount,
                1,
                MaximumVisibleChoiceCards);
            float width = 0.22f + visible * 0.066f;
            const float center = 0.62f;
            RectTransform rect =
                zoneBrowserTray.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(center - width * 0.5f, 0.25f);
            rect.anchorMax = new Vector2(center + width * 0.5f, 0.71f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void ResetZoneBrowserSelection(
            DuelPrompt prompt = null)
        {
            zoneBrowserPrompt = prompt;
            zoneBrowserStagedChoices = null;
            zoneBrowserSelectedOutline = null;
            zoneBrowserChoiceOutlines.Clear();
            if (zoneBrowserConfirm != null)
                zoneBrowserConfirm.interactable = false;
        }

        private void RegisterZoneBrowserChoice(Outline outline)
        {
            if (outline == null)
                return;
            zoneBrowserChoiceOutlines.Add(outline);
            outline.effectColor = DimmedChoiceAccent();
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private void StageZoneBrowserSelection(
            uint code,
            DuelPrompt prompt,
            IReadOnlyList<DuelChoice> choices,
            Outline selectedOutline)
        {
            ShowInspector(code);
            bool canUse =
                prompt != null &&
                prompt == core?.CurrentPrompt &&
                choices != null &&
                choices.Count > 0;
            if (!canUse)
            {
                SetStatus(
                    "Carta aberta somente para consulta.",
                    Muted);
                return;
            }

            zoneBrowserPrompt = prompt;
            zoneBrowserStagedChoices = choices;
            zoneBrowserSelectedOutline = selectedOutline;
            foreach (Outline outline in zoneBrowserChoiceOutlines)
            {
                if (outline == null)
                    continue;
                bool selected = outline == zoneBrowserSelectedOutline;
                outline.effectColor = selected
                    ? EffectGlow
                    : DimmedChoiceAccent();
                outline.effectDistance = selected
                    ? new Vector2(5f, -5f)
                    : new Vector2(2f, -2f);
            }
            zoneBrowserConfirm.interactable = true;
            SetStatus(
                $"{CardName(code)} selecionada. Confirme para continuar.",
                EffectGlow);
        }

        private void ConfirmZoneBrowserSelection()
        {
            if (zoneBrowserPrompt == null ||
                zoneBrowserStagedChoices == null ||
                zoneBrowserStagedChoices.Count == 0)
            {
                return;
            }
            SubmitZoneBrowserAction(
                zoneBrowserPrompt,
                zoneBrowserStagedChoices);
        }
    }
}
