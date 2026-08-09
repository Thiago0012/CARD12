using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class DuelEffectDescriptionResolverEditModeTests
    {
        private const uint Jioh = 92962242;

        [Test]
        public void JiohEffectsArePresentedAsTwoDistinctChoices()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            string first = DuelEffectDescriptionResolver.ChoiceLabel(
                DecodeEffectChoice(Jioh, ((ulong)Jioh << 20) | 0),
                database);
            string second = DuelEffectDescriptionResolver.ChoiceLabel(
                DecodeEffectChoice(Jioh, ((ulong)Jioh << 20) | 4),
                database);

            Assert.That(first, Does.Contain("EFEITO 1"));
            Assert.That(first, Does.Contain(
                "Virar até 2 monstros com a face para baixo"));
            Assert.That(second, Does.Contain("EFEITO 2"));
            Assert.That(second, Does.Contain(
                "Destruir 1 card que seu oponente controla"));
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void ChainingMessagePreservesSelectedEffectIdentity()
        {
            ulong descriptionId = ((ulong)Jioh << 20) | 4;
            var payload = new List<byte>();
            UInt32(payload, Jioh);
            payload.Add(0);
            payload.Add((byte)DuelLocation.MonsterZone);
            UInt32(payload, 2);
            UInt32(payload, 1);
            payload.Add(0);
            payload.Add((byte)DuelLocation.MonsterZone);
            UInt32(payload, 2);
            UInt64(payload, descriptionId);
            UInt32(payload, 1);

            var framed = new List<byte>();
            UInt32(framed, (uint)payload.Count + 1);
            framed.Add((byte)CoreMessage.Chaining);
            framed.AddRange(payload);
            DuelEvent chaining = CoreMessageDecoder.Decode(
                framed.ToArray())[0];

            Assert.That(chaining.Code, Is.EqualTo(Jioh));
            Assert.That(chaining.DescriptionId, Is.EqualTo(descriptionId));
            Assert.That(chaining.Value, Is.EqualTo(1));
        }

        [Test]
        public void EffectChoiceModelPreservesTwoCandidatesFromSameCopy()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            ulong firstId = (ulong)Jioh << 20;
            ulong secondId = firstId | 4;
            DuelPrompt prompt = DecodeIdleEffectPrompt(firstId, secondId);
            SetProperty(prompt, nameof(DuelPrompt.RequestId), 77UL);
            foreach (DuelChoice choice in prompt.Choices)
            {
                SetProperty(choice, nameof(DuelChoice.RequestId), 77UL);
                SetProperty(choice, nameof(DuelChoice.RuntimeId), 9001UL);
            }

            IReadOnlyList<EffectChoice> effects =
                EffectChoice.FromPrompt(prompt, database);

            Assert.That(effects.Count, Is.EqualTo(2));
            Assert.That(effects.Select(effect => effect.RequestId),
                Is.EqualTo(new[] { 77UL, 77UL }));
            Assert.That(effects.Select(effect => effect.CandidateIndex),
                Is.EqualTo(new[] { 0, 1 }));
            Assert.That(effects.Select(effect => effect.RuntimeId),
                Is.EqualTo(new[] { 9001UL, 9001UL }));
            Assert.That(effects.Select(effect => effect.DescriptionId),
                Is.EqualTo(new[] { firstId, secondId }));
            Assert.That(effects.Select(effect => effect.SourceLocation),
                Is.EqualTo(new[]
                {
                    (byte)DuelLocation.MonsterZone,
                    (byte)DuelLocation.MonsterZone
                }));
            Assert.That(effects.Select(effect => effect.SourceSequence),
                Is.EqualTo(new[] { 2U, 2U }));
            Assert.That(effects[0].Response,
                Is.EqualTo(CoreMessageDecoder.IntResponse(5)));
            Assert.That(effects[1].Response,
                Is.EqualTo(CoreMessageDecoder.IntResponse((1 << 16) + 5)));
            Assert.That(effects[0].UiSummary,
                Is.Not.EqualTo(effects[1].UiSummary));
            Assert.That(effects.All(effect => !effect.IsMandatory), Is.True);
        }

        [Test]
        public void EffectYesNoDeclineIsNotAnEffectCandidate()
        {
            ulong descriptionId = ((ulong)Jioh << 20) | 4;
            DuelPrompt prompt = DecodeEffectPrompt(Jioh, descriptionId);

            Assert.That(prompt.Choices[0].CandidateIndex, Is.Zero);
            Assert.That(prompt.Choices[0].DescriptionId,
                Is.EqualTo(descriptionId));
            Assert.That(prompt.Choices[1].DescriptionId, Is.Zero);
            Assert.That(
                DuelPromptPresentationRules.EffectCandidates(prompt),
                Is.EqualTo(new[] { prompt.Choices[0] }));
        }

        private static DuelChoice DecodeEffectChoice(
            uint code,
            ulong descriptionId)
        {
            return DecodeEffectPrompt(code, descriptionId)
                .Choices.First(choice =>
                    choice.Label.StartsWith("Ativar"));
        }

        private static DuelPrompt DecodeEffectPrompt(
            uint code,
            ulong descriptionId)
        {
            var payload = new List<byte> { 0 };
            UInt32(payload, code);
            payload.Add(0);
            payload.Add((byte)DuelLocation.MonsterZone);
            UInt32(payload, 0);
            UInt32(payload, 1);
            UInt64(payload, descriptionId);
            var framed = new List<byte>();
            UInt32(framed, (uint)payload.Count + 1);
            framed.Add((byte)CoreMessage.SelectEffectYesNo);
            framed.AddRange(payload);
            return CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;
        }

        private static DuelPrompt DecodeIdleEffectPrompt(
            ulong firstDescriptionId,
            ulong secondDescriptionId)
        {
            var payload = new List<byte> { 0 };
            for (int category = 0; category < 5; category++)
                UInt32(payload, 0);
            UInt32(payload, 2);
            Activation(payload, firstDescriptionId);
            Activation(payload, secondDescriptionId);
            payload.Add(0);
            payload.Add(0);
            payload.Add(0);

            var framed = new List<byte>();
            UInt32(framed, (uint)payload.Count + 1);
            framed.Add((byte)CoreMessage.SelectIdleCommand);
            framed.AddRange(payload);
            return CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;
        }

        private static void Activation(
            List<byte> payload,
            ulong descriptionId)
        {
            UInt32(payload, Jioh);
            payload.Add(0);
            payload.Add((byte)DuelLocation.MonsterZone);
            UInt32(payload, 2);
            UInt64(payload, descriptionId);
            payload.Add(0);
        }

        private static void SetProperty<T>(
            object target,
            string property,
            T value)
        {
            target.GetType().GetProperty(
                    property,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static void UInt32(List<byte> output, uint value)
        {
            output.Add((byte)value);
            output.Add((byte)(value >> 8));
            output.Add((byte)(value >> 16));
            output.Add((byte)(value >> 24));
        }

        private static void UInt64(List<byte> output, ulong value)
        {
            UInt32(output, (uint)value);
            UInt32(output, (uint)(value >> 32));
        }
    }
}
