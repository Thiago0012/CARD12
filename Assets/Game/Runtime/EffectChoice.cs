using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Presentation projection of one concrete effect candidate emitted by
    /// ocgcore. It never creates legality and keeps the original DuelChoice so
    /// submission sends exactly the response bytes supplied by the Core.
    /// </summary>
    public sealed class EffectChoice
    {
        private EffectChoice()
        {
        }

        public ulong RequestId { get; private set; }
        public int CandidateIndex { get; private set; }
        public ulong RuntimeId { get; private set; }
        public uint OfficialCardId { get; private set; }
        public ulong DescriptionId { get; private set; }
        public byte SourceController { get; private set; }
        public byte SourceLocation { get; private set; }
        public uint SourceSequence { get; private set; }
        public uint SourcePosition { get; private set; }
        public bool IsMandatory { get; private set; }
        public string UiSummary { get; private set; }
        public DuelChoice ProtocolChoice { get; private set; }

        public byte[] Response => ProtocolChoice?.Response == null
            ? Array.Empty<byte>()
            : (byte[])ProtocolChoice.Response.Clone();

        public static IReadOnlyList<EffectChoice> FromPrompt(
            DuelPrompt prompt,
            CardDatabase database)
        {
            if (prompt == null)
                return Array.Empty<EffectChoice>();

            return DuelPromptPresentationRules.EffectCandidates(prompt)
                .Select(choice => Create(prompt, choice, database))
                .Where(choice => choice != null)
                .ToArray();
        }

        public static EffectChoice Create(
            DuelPrompt prompt,
            DuelChoice choice,
            CardDatabase database)
        {
            if (!DuelPromptPresentationRules.IsEffectCandidate(
                    prompt,
                    choice))
            {
                return null;
            }

            int candidateIndex = choice.CandidateIndex;
            if (candidateIndex < 0)
            {
                // SELECT_EFFECTYN has one implicit activation candidate. Its
                // response is still the authoritative Core payload (1).
                candidateIndex = prompt.Choices.IndexOf(choice);
            }

            return new EffectChoice
            {
                RequestId = choice.RequestId != 0
                    ? choice.RequestId
                    : prompt.RequestId,
                CandidateIndex = candidateIndex,
                RuntimeId = choice.RuntimeId,
                OfficialCardId = choice.CardCode,
                DescriptionId = choice.DescriptionId,
                SourceController = choice.Controller,
                SourceLocation = choice.Location,
                SourceSequence = choice.Sequence,
                SourcePosition = choice.Position,
                IsMandatory = prompt.Forced,
                UiSummary = DuelEffectDescriptionResolver.ChoiceLabel(
                    choice,
                    database),
                ProtocolChoice = choice
            };
        }
    }
}
