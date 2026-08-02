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
                   prompt.Message == CoreMessage.SelectYesNo;
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
            if (choice == null || choice.CardCode != 0)
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
