using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneDuel.DuelEngine.Protocol
{
    public static class DeterministicDuelPolicy
    {
        public static DuelChoice Choose(DuelPrompt prompt)
        {
            if (prompt == null || prompt.Choices.Count == 0)
            {
                throw new InvalidOperationException("The deterministic policy received an empty prompt.");
            }

            if (prompt.Message == CoreMessage.SelectIdleCommand)
            {
                DuelChoice summon = FindCommand(prompt, 0);
                if (summon != null) return summon;
                DuelChoice battle = FindCommand(prompt, 6);
                if (battle != null) return battle;
                DuelChoice end = FindCommand(prompt, 7);
                if (end != null) return end;
            }
            else if (prompt.Message == CoreMessage.SelectBattleCommand)
            {
                DuelChoice attack = FindCommand(prompt, 1);
                if (attack != null) return attack;
                DuelChoice main2 = FindCommand(prompt, 2);
                if (main2 != null) return main2;
                DuelChoice end = FindCommand(prompt, 3);
                if (end != null) return end;
            }
            else if (prompt.Message == CoreMessage.SelectChain)
            {
                DuelChoice decline = FindValue(prompt, -1);
                if (decline != null) return decline;
            }
            else if (prompt.Message == CoreMessage.SelectEffectYesNo ||
                     prompt.Message == CoreMessage.SelectYesNo)
            {
                DuelChoice decline = FindValue(prompt, 0);
                if (decline != null) return decline;
            }
            else if (prompt.Message == CoreMessage.SelectPosition)
            {
                DuelChoice attack = FindValue(prompt, 1);
                if (attack != null) return attack;
            }
            else if (prompt.Message == CoreMessage.SelectPlace ||
                     prompt.Message == CoreMessage.SelectDisableField)
            {
                DuelChoice placement = ChooseRequiredPlaces(prompt);
                if (placement != null) return placement;
            }
            else if (prompt.Message == CoreMessage.SelectUnselectCard)
            {
                DuelChoice finish = FindValue(prompt, -1);
                bool hasSelectedCard = prompt.Choices.Any(choice =>
                    choice.Label.StartsWith(
                        "Remover",
                        StringComparison.OrdinalIgnoreCase));
                if (!hasSelectedCard)
                {
                    DuelChoice selectable = prompt.Choices.FirstOrDefault(choice =>
                        string.Equals(
                            choice.Label,
                            "Selecionar",
                            StringComparison.OrdinalIgnoreCase));
                    if (selectable != null) return selectable;
                }
                if (finish != null) return finish;
            }
            else if (prompt.Message == CoreMessage.SelectSum ||
                     prompt.Message == CoreMessage.SelectTribute)
            {
                DuelChoice selection = ChooseValidSum(prompt);
                if (selection != null) return selection;
            }
            return prompt.Choices[0];
        }

        private static DuelChoice ChooseRequiredPlaces(DuelPrompt prompt)
        {
            int required = checked((int)prompt.MaximumSelections);
            if (required <= 1)
                return prompt.Choices[0];

            DuelChoice[] selected = prompt.Choices
                .Where(choice => choice.Response?.Length == 3)
                .OrderBy(choice => choice.ChoiceIndex)
                .Take(required)
                .ToArray();
            if (selected.Length != required)
                return null;

            return new DuelChoice
            {
                RequestId = prompt.RequestId,
                Label = $"Selecionar {required} zonas",
                Response = CoreMessageDecoder.PlaceSelectionResponse(selected)
            };
        }

        private static DuelChoice ChooseValidSum(DuelPrompt prompt)
        {
            int[] indexes = prompt.Choices
                .Where(choice => choice.ChoiceIndex >= 0)
                .Select(choice => choice.ChoiceIndex)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            int minimum = prompt.SumAtLeast ||
                          prompt.Message == CoreMessage.SelectTribute
                ? 0
                : checked((int)prompt.MinimumSelections);
            int maximum = prompt.SumAtLeast
                ? indexes.Length
                : Math.Min(
                    checked((int)prompt.MaximumSelections),
                    indexes.Length);
            for (int size = minimum; size <= maximum; size++)
            {
                var selected = new List<int>(size);
                if (TryCombination(
                        prompt,
                        indexes,
                        0,
                        size,
                        selected,
                        out int[] valid))
                {
                    return new DuelChoice
                    {
                        Label = "Seleção determinística válida",
                        Response = CoreMessageDecoder.CardSelectionResponse(
                            valid.Select(value => (uint)value).ToArray())
                    };
                }
            }
            return null;
        }

        private static bool TryCombination(
            DuelPrompt prompt,
            int[] indexes,
            int start,
            int remaining,
            List<int> selected,
            out int[] valid)
        {
            if (remaining == 0)
            {
                if (CoreMessageDecoder.IsValidSelection(prompt, selected))
                {
                    valid = selected.ToArray();
                    return true;
                }
                valid = null;
                return false;
            }
            for (int index = start;
                 index <= indexes.Length - remaining;
                 index++)
            {
                selected.Add(indexes[index]);
                if (TryCombination(
                        prompt,
                        indexes,
                        index + 1,
                        remaining - 1,
                        selected,
                        out valid))
                {
                    return true;
                }
                selected.RemoveAt(selected.Count - 1);
            }
            valid = null;
            return false;
        }

        private static DuelChoice FindCommand(DuelPrompt prompt, int command)
        {
            foreach (DuelChoice choice in prompt.Choices)
            {
                if (choice.Response != null && choice.Response.Length == 4 &&
                    (ReadInt(choice.Response) & 0xFFFF) == command)
                {
                    return choice;
                }
            }
            return null;
        }

        private static DuelChoice FindValue(DuelPrompt prompt, int value)
        {
            foreach (DuelChoice choice in prompt.Choices)
            {
                if (choice.Response != null && choice.Response.Length == 4 && ReadInt(choice.Response) == value)
                {
                    return choice;
                }
            }
            return null;
        }

        private static int ReadInt(byte[] bytes)
        {
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);
        }
    }
}
