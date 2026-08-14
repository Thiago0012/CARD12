using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private sealed class ZoneBrowserEntry
        {
            public uint Code { get; }
            public uint CoreSequence { get; }
            public IReadOnlyList<DuelChoice> LegalChoices { get; }

            public ZoneBrowserEntry(
                uint code,
                uint coreSequence,
                IReadOnlyList<DuelChoice> legalChoices)
            {
                Code = code;
                CoreSequence = coreSequence;
                LegalChoices = legalChoices ?? new List<DuelChoice>();
            }
        }

        private readonly List<Outline> zoneBrowserChoiceOutlines = new();
        private Button zoneBrowserConfirm;
        private Button zoneBrowserCancel;
        private DuelPrompt zoneBrowserPrompt;
        private IReadOnlyList<DuelChoice> zoneBrowserStagedChoices;
        private Outline zoneBrowserSelectedOutline;
        private bool zoneBrowserSummonMode;

        private void BuildZoneBrowserConfirmation(Transform tray)
        {
            zoneBrowserConfirm = CreateButton(
                tray,
                "Confirmar Carta da Zona",
                "SELECIONAR",
                new Vector2(0.245f, 0.025f),
                new Vector2(0.495f, 0.145f),
                EffectGlow,
                ConfirmZoneBrowserSelection);
            zoneBrowserConfirm.interactable = false;

            zoneBrowserCancel = CreateButton(
                tray,
                "Cancelar Carta da Zona",
                "NÃO FAZER NADA",
                new Vector2(0.505f, 0.025f),
                new Vector2(0.755f, 0.145f),
                Muted,
                CancelZoneBrowserSelection);
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
            float requiredWidth = visible * ChoiceCardWidth +
                                  Mathf.Max(0, visible - 1) * 12f +
                                  104f;
            float frameWidth = Mathf.Max(960f, frame.rect.width);
            float width = Mathf.Clamp(
                requiredWidth / frameWidth,
                0.40f,
                0.68f);
            // Keep the authored card inspector column on the left free.
            // Inspecting an Extra Deck candidate must not cover its tray.
            const float safeAreaMin = 0.29f;
            const float safeAreaMax = 0.97f;
            const float center = (safeAreaMin + safeAreaMax) * 0.5f;
            RectTransform rect =
                zoneBrowserTray.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(center - width * 0.5f, 0.235f);
            rect.anchorMax = new Vector2(center + width * 0.5f, 0.725f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void RefreshZoneBrowserScrolling()
        {
            if (zoneBrowserViewport == null || zoneBrowserContent == null ||
                zoneBrowserScroll == null)
            {
                return;
            }

            float viewportWidth = zoneBrowserViewport.rect.width;
            float contentWidth = zoneBrowserContent.rect.width;
            bool contentOverflows = contentWidth > viewportWidth + 1f;
            if (zoneBrowserScrollbar != null)
                zoneBrowserScrollbar.gameObject.SetActive(contentOverflows);

            zoneBrowserScroll.horizontalNormalizedPosition = 0f;
            if (!contentOverflows)
            {
                Vector2 position = zoneBrowserContent.anchoredPosition;
                position.x = Mathf.Max(12f, (viewportWidth - contentWidth) * 0.5f);
                zoneBrowserContent.anchoredPosition = position;
            }
        }

        private void ConfigureZoneBrowserActionMode(bool summonMode)
        {
            zoneBrowserSummonMode = summonMode;
            if (zoneBrowserConfirm == null)
                return;
            Text label = zoneBrowserConfirm.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = summonMode ? "INVOCAR" : "SELECIONAR";
        }

        private List<ZoneBrowserEntry> BuildZoneBrowserEntries(
            bool browsingExtraDeck,
            IReadOnlyList<DuelChoice> legalChoices)
        {
            var entries = new List<ZoneBrowserEntry>();
            if (!browsingExtraDeck)
            {
                if (legalChoices == null)
                    return entries;
                foreach (DuelChoice choice in legalChoices)
                {
                    if (choice == null || choice.CardCode == 0)
                        continue;
                    entries.Add(new ZoneBrowserEntry(
                        choice.CardCode,
                        choice.Sequence,
                        new[] { choice }));
                }
                return entries;
            }

            uint[] extraDeck = core?.PlayerExtraDeckCards?.ToArray() ??
                               System.Array.Empty<uint>();
            bool coreOfferedActions = legalChoices != null &&
                                      legalChoices.Count > 0;
            if (!coreOfferedActions)
            {
                for (uint sequence = 0; sequence < extraDeck.Length; sequence++)
                {
                    uint code = extraDeck[sequence];
                    if (code != 0)
                    {
                        entries.Add(new ZoneBrowserEntry(
                            code,
                            sequence,
                            System.Array.Empty<DuelChoice>()));
                    }
                }
                return entries;
            }

            // The Core already validated summon method, materials, timing and
            // available zones. Presentation must expose only those exact
            // choices, preserving the Core sequence instead of recomputing
            // legality from card artwork or metadata.
            foreach (IGrouping<uint, DuelChoice> group in legalChoices
                         .Where(choice => choice != null)
                         .GroupBy(choice => choice.Sequence)
                         .OrderBy(group => group.Key))
            {
                DuelChoice[] groupedChoices = group.ToArray();
                uint code = groupedChoices
                    .Select(choice => choice.CardCode)
                    .FirstOrDefault(value => value != 0);
                if (code == 0 && group.Key < extraDeck.Length)
                    code = extraDeck[group.Key];
                if (code == 0)
                    continue;
                entries.Add(new ZoneBrowserEntry(
                    code,
                    group.Key,
                    groupedChoices));
            }
            return entries;
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
                zoneBrowserSummonMode
                    ? $"{CardName(code)} selecionada. Confirme a Invocação."
                    : $"{CardName(code)} selecionada. Confirme para continuar.",
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

        private void CancelZoneBrowserSelection()
        {
            CloseZoneBrowserFromUser();
            SetStatus(
                zoneBrowserSummonMode
                    ? "Invocação do Deck Adicional cancelada."
                    : "Nenhuma ação foi escolhida.",
                Muted);
        }
    }
}
