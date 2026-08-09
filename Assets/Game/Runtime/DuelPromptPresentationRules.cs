using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Presentation-only decisions derived from prompts already validated by
    /// ygopro-core.  This class never creates a duel action or changes state.
    /// </summary>
    public static class DuelPromptPresentationRules
    {
        public static bool ShouldAutoPassEmptyChain(DuelPrompt prompt)
        {
            return prompt != null &&
                   prompt.Message == CoreMessage.SelectChain &&
                   !prompt.Forced &&
                   prompt.Choices.Count == 1 &&
                   IsNoResponseChoice(prompt.Choices[0]);
        }

        public static bool RequiresVisibleResponseTray(DuelPrompt prompt)
        {
            if (prompt == null || prompt.Player != 0 ||
                prompt.Choices.Count == 0 ||
                ShouldAutoPassEmptyChain(prompt))
            {
                return false;
            }

            return prompt.Message == CoreMessage.SelectChain ||
                   prompt.Message == CoreMessage.SelectEffectYesNo ||
                   prompt.Message == CoreMessage.SelectYesNo ||
                   prompt.Message == CoreMessage.SelectPosition ||
                   prompt.Message == CoreMessage.SelectOption ||
                   prompt.Message == CoreMessage.AnnounceCard ||
                   prompt.Message == CoreMessage.AnnounceAttribute ||
                   prompt.Message == CoreMessage.AnnounceRace ||
                   prompt.Message == CoreMessage.AnnounceNumber ||
                   prompt.Message == CoreMessage.RockPaperScissors ||
                   prompt.Message == CoreMessage.SelectCounter ||
                   prompt.Message == CoreMessage.SortCard ||
                   prompt.Message == CoreMessage.SortChain;
        }

        public static bool ShouldUseCompactResponseBar(DuelPrompt prompt)
        {
            return RequiresVisibleResponseTray(prompt) &&
                   // A concrete effect question must show its full
                   // description immediately. Hiding it behind a generic
                   // RESPONDER button made legal trigger effects look absent.
                   prompt.Message == CoreMessage.SelectChain &&
                   !prompt.Forced &&
                   DeclineChoice(prompt) != null &&
                   ActionableResponseChoices(prompt).Count == 1;
        }

        public static DuelChoice DeclineChoice(DuelPrompt prompt)
        {
            if (prompt == null)
                return null;
            return prompt.Choices.FirstOrDefault(choice =>
                IsNoResponseChoice(choice) ||
                (prompt.Message == CoreMessage.SelectYesNo &&
                 Contains(choice?.Label, "Não")));
        }

        public static List<DuelChoice> ActionableResponseChoices(
            DuelPrompt prompt)
        {
            DuelChoice decline = DeclineChoice(prompt);
            return prompt?.Choices
                .Where(choice => choice != null && choice != decline)
                .ToList() ?? new List<DuelChoice>();
        }

        /// <summary>
        /// Returns only activation candidates explicitly offered by the Core.
        /// Decline/pass choices and follow-up option/target prompts are not
        /// effect candidates and must never be grouped into this list.
        /// </summary>
        public static List<DuelChoice> EffectCandidates(DuelPrompt prompt)
        {
            return prompt?.Choices
                .Where(choice => IsEffectCandidate(prompt, choice))
                .ToList() ?? new List<DuelChoice>();
        }

        public static bool IsEffectCandidate(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            if (prompt == null || choice == null ||
                IsNoResponseChoice(choice))
            {
                return false;
            }

            return prompt.Message switch
            {
                CoreMessage.SelectChain => true,
                CoreMessage.SelectEffectYesNo =>
                    Contains(choice.Label, "Ativar"),
                CoreMessage.SelectIdleCommand or
                    CoreMessage.SelectBattleCommand =>
                    Contains(choice.Label, "Ativar"),
                _ => false
            };
        }

        public static List<DuelChoice> PhaseChoices(DuelPrompt prompt)
        {
            if (prompt == null ||
                (prompt.Message != CoreMessage.SelectIdleCommand &&
                 prompt.Message != CoreMessage.SelectBattleCommand))
            {
                return new List<DuelChoice>();
            }

            return prompt.Choices
                .Where(choice =>
                    Contains(choice?.Label, "Fase") ||
                    Contains(choice?.Label, "Encerrar turno"))
                .ToList();
        }

        private static bool IsNoResponseChoice(DuelChoice choice)
        {
            if (choice == null)
                return false;
            return Contains(choice.Label, "Não responder") ||
                   Contains(choice.Label, "Nao responder") ||
                   Contains(choice.Label, "Não ativar") ||
                   Contains(choice.Label, "Nao ativar");
        }

        private static bool Contains(string source, string fragment)
        {
            return (source ?? string.Empty).IndexOf(
                fragment,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
