using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private static bool IsMultiPlacePrompt(DuelPrompt prompt)
        {
            return prompt != null &&
                   (prompt.Message == CoreMessage.SelectPlace ||
                    prompt.Message == CoreMessage.SelectDisableField) &&
                   prompt.MaximumSelections > 1;
        }

        private void StagePlaceChoice(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            if (prompt == null || choice == null ||
                prompt != core?.CurrentPrompt || choice.ChoiceIndex < 0)
            {
                return;
            }

            if (!selectedPromptIndexes.Add(choice.ChoiceIndex))
                selectedPromptIndexes.Remove(choice.ChoiceIndex);

            int required = (int)prompt.MaximumSelections;
            if (selectedPromptIndexes.Count == required)
            {
                DuelChoice[] selected = prompt.Choices
                    .Where(candidate => selectedPromptIndexes.Contains(
                        candidate.ChoiceIndex))
                    .OrderBy(candidate => candidate.ChoiceIndex)
                    .ToArray();
                core.SubmitCoreResponse(
                    CoreMessageDecoder.PlaceSelectionResponse(selected),
                    prompt.RequestId);
                RefreshEverything(true);
                return;
            }

            ClearZoneHighlights();
            HighlightPromptZones(prompt);
            SetStatus(
                $"Escolha {required} zonas iluminadas · " +
                $"{selectedPromptIndexes.Count}/{required}.",
                Cyan);
        }
    }
}
