using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Binds a visible physical card copy to actions explicitly offered by
    /// ygopro-core. It never creates legality; it only matches the Core
    /// request, definition and exact card address.
    /// </summary>
    public static class CoreCardActionBinding
    {
        public static IReadOnlyList<DuelChoice> ChoicesFor(
            DuelPrompt prompt,
            CardInstanceKey instance)
        {
            if (prompt == null ||
                !instance.IsValid ||
                instance.DefinitionCode == 0)
            {
                return Array.Empty<DuelChoice>();
            }
            return prompt.Choices.Where(choice =>
                choice != null &&
                choice.CardCode == instance.DefinitionCode &&
                (choice.RequestId == 0 ||
                 prompt.RequestId == 0 ||
                 choice.RequestId == prompt.RequestId) &&
                (choice.RuntimeId == 0 ||
                 choice.RuntimeId == instance.RuntimeId) &&
                (!choice.HasLocation ||
                 (choice.Controller == instance.Controller &&
                  (choice.Location & instance.Location) != 0 &&
                  choice.Sequence == instance.Sequence))).ToArray();
        }

        public static DuelChoice FirstChoiceFor(
            DuelPrompt prompt,
            CardInstanceKey instance)
        {
            return ChoicesFor(prompt, instance).FirstOrDefault();
        }

        public static bool BelongsToRequest(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            if (prompt == null || choice == null)
                return false;
            if (choice.RequestId != 0 &&
                prompt.RequestId != 0 &&
                choice.RequestId != prompt.RequestId)
            {
                return false;
            }
            return prompt.Choices.Any(candidate =>
                ReferenceEquals(candidate, choice) ||
                (candidate.Response != null &&
                 choice.Response != null &&
                 candidate.Response.SequenceEqual(choice.Response))) ||
                CoreMessageDecoder.IsValidPlaceSelectionResponse(
                    prompt,
                    choice.Response);
        }
    }
}
