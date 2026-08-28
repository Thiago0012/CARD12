using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneArena
{
    /// <summary>
    /// Text and filtering used only by the visual selection tray. The Core
    /// prompt and its legal responses remain untouched.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private static IReadOnlyList<DuelChoice> PresentationChoices(
            DuelPrompt prompt,
            IReadOnlyList<DuelChoice> choices)
        {
            if (prompt == null || choices == null ||
                (prompt.Message != CoreMessage.SelectCard &&
                 prompt.Message != CoreMessage.SelectTribute))
            {
                return choices ?? new List<DuelChoice>();
            }

            bool hasIndividualCandidates = choices.Any(choice =>
                choice != null && choice.ChoiceIndex >= 0 &&
                choice.CardCode != 0);
            if (!hasIndividualCandidates)
                return choices;

            // Keep every physical candidate and cancellation response. Hide
            // only the decoder's deterministic "first N" shortcut, which
            // would select materials the player did not explicitly mark.
            return choices.Where(choice =>
                    choice == null || choice.CardCode != 0 ||
                    choice.ChoiceIndex >= 0 ||
                    string.IsNullOrWhiteSpace(choice.Label) ||
                    choice.Label.IndexOf(
                        "Selecionar as primeiras",
                        StringComparison.OrdinalIgnoreCase) < 0)
                .ToList();
        }

        private void UpdateChoiceInstruction(DuelPrompt prompt)
        {
            if (choiceInstruction == null)
                return;

            int selected = selectedPromptIndexes.Count;
            string sources = SelectionSourceSummary(prompt);
            string sourceSuffix = string.IsNullOrWhiteSpace(sources)
                ? string.Empty
                : $" · ORIGEM: {sources}";
            choiceInstruction.color = choiceModalAccent;
            choiceInstruction.text = prompt?.Message switch
            {
                CoreMessage.SelectEffectYesNo or CoreMessage.SelectYesNo =>
                    "A RESOLUÇÃO ESTÁ PAUSADA · LEIA A OPÇÃO E CONFIRME",
                CoreMessage.SelectChain =>
                    "ESCOLHA UMA RESPOSTA LEGAL OU PASSE A PRIORIDADE",
                CoreMessage.SelectSum =>
                    $"MATÉRIAS DISPONÍVEIS · SELECIONADAS {selected} · " +
                    $"SOMA {(prompt.SumAtLeast ? "MÍNIMA" : "EXATA")} " +
                    $"{prompt.RequiredSum}{sourceSuffix}",
                CoreMessage.SelectTribute =>
                    $"TRIBUTOS DISPONÍVEIS · SELECIONADOS {selected} · " +
                    $"VALOR NECESSÁRIO {prompt.MinimumSelections}" +
                    sourceSuffix,
                CoreMessage.SelectUnselectCard =>
                    $"ESCOLHA OU REMOVA UMA MATÉRIA · " +
                    $"JÁ SELECIONADAS {IterativeSelectionCount(prompt)}" +
                    sourceSuffix,
                CoreMessage.SelectCard =>
                    $"SOMENTE CANDIDATAS LEGAIS · SELECIONADAS {selected} · " +
                    SelectionRange(prompt) + sourceSuffix,
                _ => "SELECIONE UMA OPÇÃO E CONFIRME"
            };
        }

        private static bool IsCardSelectionMessage(CoreMessage? message)
        {
            return message == CoreMessage.SelectCard ||
                   message == CoreMessage.SelectTribute ||
                   message == CoreMessage.SelectSum ||
                   message == CoreMessage.SelectUnselectCard;
        }

        private static string SelectionRange(DuelPrompt prompt)
        {
            if (prompt == null)
                return string.Empty;
            if (prompt.MinimumSelections == prompt.MaximumSelections)
                return $"ESCOLHA {prompt.MinimumSelections}";
            return $"ESCOLHA DE {prompt.MinimumSelections} A " +
                   $"{prompt.MaximumSelections}";
        }

        private static int IterativeSelectionCount(DuelPrompt prompt)
        {
            return prompt?.Choices.Count(choice =>
                choice?.Label?.IndexOf(
                    "Remover",
                    StringComparison.OrdinalIgnoreCase) >= 0) ?? 0;
        }

        private static string SelectionSourceSummary(DuelPrompt prompt)
        {
            if (prompt == null)
                return string.Empty;
            return string.Join(
                " / ",
                prompt.Choices
                    .Where(choice => choice != null && choice.CardCode != 0 &&
                                     choice.HasLocation)
                    .Select(ChoiceLocationLabel)
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Distinct());
        }

        private static string ChoiceLocationLabel(DuelChoice choice)
        {
            if (choice == null || !choice.HasLocation)
                return string.Empty;
            bool local = choice.Controller == 0;
            uint location = choice.Location;
            if ((location & DuelLocation.Deck) != 0)
                return local ? "SEU DECK" : "DECK DO OPONENTE";
            if ((location & DuelLocation.Hand) != 0)
                return local ? "SUA MÃO" : "MÃO DO OPONENTE";
            if ((location & DuelLocation.Extra) != 0)
                return local
                    ? "SEU DECK ADICIONAL"
                    : "DECK ADICIONAL DO OPONENTE";
            if ((location & DuelLocation.Graveyard) != 0)
                return local ? "SEU CEMITÉRIO" : "CEMITÉRIO DO OPONENTE";
            if ((location & DuelLocation.Banished) != 0)
                return local ? "SUAS CARTAS BANIDAS" : "BANIDAS DO OPONENTE";
            if ((location & DuelLocation.MonsterZone) != 0)
                return local ? "SEU CAMPO" : "CAMPO DO OPONENTE";
            if ((location & DuelLocation.SpellTrapZone) != 0)
                return local
                    ? "SUAS MAGIAS/ARMADILHAS"
                    : "MAGIAS/ARMADILHAS DO OPONENTE";
            return string.Empty;
        }
    }
}
