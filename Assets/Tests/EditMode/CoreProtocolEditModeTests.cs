using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class CoreProtocolEditModeTests
    {
        [Test]
        public void DecoderReadsFramedTurnPhaseAndDrawMessages()
        {
            var bytes = new List<byte>();
            Packet(bytes, (byte)CoreMessage.NewTurn, new byte[] { 0 });
            Packet(bytes, (byte)CoreMessage.NewPhase, new byte[] { 4, 0 });
            Packet(bytes, (byte)CoreMessage.Draw, new byte[]
            {
                0, 1, 0, 0, 0,
                0xA3, 0xA9, 0x57, 0x05,
                1, 0, 0, 0
            });

            List<DuelEvent> events = CoreMessageDecoder.Decode(bytes.ToArray());
            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events[0].Message, Is.EqualTo(CoreMessage.NewTurn));
            Assert.That(events[1].Value, Is.EqualTo(4));
            Assert.That(events[2].Codes, Is.EqualTo(new uint[] { 89631139 }));
        }

        [Test]
        public void DecoderPreservesAttackSourceAndTargetForPresentation()
        {
            var payload = new List<byte>();
            Location(
                payload,
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                1);
            Location(
                payload,
                1,
                (byte)DuelLocation.MonsterZone,
                3,
                2);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.Attack,
                payload.ToArray());

            DuelEvent duelEvent =
                CoreMessageDecoder.Decode(framed.ToArray())[0];

            Assert.That(duelEvent.Player, Is.Zero);
            Assert.That(duelEvent.Previous.Controller, Is.Zero);
            Assert.That(duelEvent.Previous.Sequence, Is.EqualTo(2));
            Assert.That(duelEvent.Current.Controller, Is.EqualTo(1));
            Assert.That(duelEvent.Current.Sequence, Is.EqualTo(3));
            Assert.That(duelEvent.DirectAttack, Is.False);
        }

        [Test]
        public void DecoderPreservesBattleValuesAndDestructionFlags()
        {
            var payload = new List<byte>();
            Location(
                payload,
                1,
                (byte)DuelLocation.MonsterZone,
                1,
                1);
            UInt32(payload, 2500);
            UInt32(payload, 1600);
            payload.Add(0);
            Location(
                payload,
                0,
                (byte)DuelLocation.MonsterZone,
                4,
                2);
            UInt32(payload, 1200);
            UInt32(payload, 2000);
            payload.Add(1);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.Battle,
                payload.ToArray());

            DuelEvent duelEvent =
                CoreMessageDecoder.Decode(framed.ToArray())[0];

            Assert.That(duelEvent.Player, Is.EqualTo(1));
            Assert.That(duelEvent.AttackerAttack, Is.EqualTo(2500));
            Assert.That(duelEvent.AttackerDefense, Is.EqualTo(1600));
            Assert.That(duelEvent.TargetAttack, Is.EqualTo(1200));
            Assert.That(duelEvent.TargetDefense, Is.EqualTo(2000));
            Assert.That(duelEvent.AttackerDestroyed, Is.False);
            Assert.That(duelEvent.TargetDestroyed, Is.True);
        }

        [Test]
        public void DecoderRejectsTruncatedPackets()
        {
            Assert.Throws<CoreProtocolException>(() =>
                CoreMessageDecoder.Decode(new byte[] { 12, 0, 0, 0, (byte)CoreMessage.Draw, 0 }));
        }

        [Test]
        public void IdleChoicesKeepExactCardLocationForDirectInteraction()
        {
            var payload = new List<byte> { 0 };
            UInt32(payload, 1);
            UInt32(payload, 89631139);
            payload.Add(0);
            payload.Add((byte)DuelLocation.Hand);
            UInt32(payload, 3);
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 0);
            payload.Add(0);
            payload.Add(1);
            payload.Add(0);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectIdleCommand,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt, Is.Not.Null);
            Assert.That(prompt.Choices[0].CardCode, Is.EqualTo(89631139));
            Assert.That(prompt.Choices[0].HasLocation, Is.True);
            Assert.That(
                prompt.Choices[0].Location,
                Is.EqualTo((byte)DuelLocation.Hand));
            Assert.That(prompt.Choices[0].Sequence, Is.EqualTo(3));
            Assert.That(
                prompt.Choices.Exists(choice => choice.Label == "Encerrar turno"),
                Is.True);
        }

        [Test]
        public void PlaceChoicesExposeTheExactClickableZone()
        {
            var payload = new List<byte> { 0, 1 };
            uint unavailable = uint.MaxValue & ~(1u << 3);
            UInt32(payload, unavailable);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectPlace,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.Choices, Has.Count.EqualTo(1));
            DuelChoice zone = prompt.Choices[0];
            Assert.That(zone.HasLocation, Is.True);
            Assert.That(zone.Controller, Is.Zero);
            Assert.That(
                zone.Location,
                Is.EqualTo((byte)DuelLocation.MonsterZone));
            Assert.That(zone.Sequence, Is.EqualTo(3));
            Assert.That(
                zone.Response,
                Is.EqualTo(
                    new byte[]
                    {
                        0,
                        (byte)DuelLocation.MonsterZone,
                        3
                    }));
        }

        [Test]
        public void MultiplePlacesAreReturnedInOneCompleteCoreResponse()
        {
            var payload = new List<byte> { 0, 2 };
            uint unavailable = uint.MaxValue &
                ~(1u << 2) &
                ~(1u << 9);
            UInt32(payload, unavailable);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectPlace,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.Forced, Is.True);
            Assert.That(prompt.MinimumSelections, Is.EqualTo(2));
            Assert.That(prompt.MaximumSelections, Is.EqualTo(2));
            Assert.That(prompt.Choices, Has.Count.EqualTo(2));
            Assert.That(
                prompt.Choices.Select(choice => choice.ChoiceIndex),
                Is.EqualTo(new[] { 0, 1 }));

            byte[] expected =
            {
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                0,
                (byte)DuelLocation.SpellTrapZone,
                1
            };
            byte[] response = CoreMessageDecoder.PlaceSelectionResponse(
                prompt.Choices);
            Assert.That(response, Is.EqualTo(expected));
            Assert.That(
                CoreMessageDecoder.IsValidPlaceSelectionResponse(
                    prompt,
                    response),
                Is.True);

            DuelChoice deterministic =
                DeterministicDuelPolicy.Choose(prompt);
            Assert.That(deterministic.Response, Is.EqualTo(expected));
            Assert.That(
                CoreCardActionBinding.BelongsToRequest(
                    prompt,
                    deterministic),
                Is.True);

            DuelChoice tactical =
                TacticalOpponentPolicy.Choose(prompt, null, null);
            Assert.That(tactical.Response.Length, Is.EqualTo(6));
            Assert.That(
                CoreMessageDecoder.IsValidPlaceSelectionResponse(
                    prompt,
                    tactical.Response),
                Is.True);
        }

        [Test]
        public void MultiplePlaceResponseRejectsDuplicateOrPartialZones()
        {
            var payload = new List<byte> { 0, 2 };
            uint unavailable = uint.MaxValue &
                ~(1u << 1) &
                ~(1u << 3);
            UInt32(payload, unavailable);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectDisableField,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;
            byte[] oneZone = prompt.Choices[0].Response;
            byte[] duplicate = CoreMessageDecoder.PlaceSelectionResponse(
                new[] { prompt.Choices[0], prompt.Choices[0] });

            Assert.That(
                CoreMessageDecoder.IsValidPlaceSelectionResponse(
                    prompt,
                    oneZone),
                Is.False);
            Assert.That(
                CoreMessageDecoder.IsValidPlaceSelectionResponse(
                    prompt,
                    duplicate),
                Is.False);
        }

        [Test]
        public void OpponentPlaceMaskMapsToTheAbsoluteOpponentField()
        {
            var payload = new List<byte> { 1, 1 };
            uint unavailable = uint.MaxValue & ~(1u << 2);
            UInt32(payload, unavailable);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectPlace,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.Player, Is.EqualTo(1));
            Assert.That(prompt.Choices, Has.Count.EqualTo(1));
            Assert.That(prompt.Choices[0].Controller, Is.EqualTo(1));
            Assert.That(
                prompt.Choices[0].Response,
                Is.EqualTo(new byte[]
                {
                    1,
                    (byte)DuelLocation.MonsterZone,
                    2
                }));
        }

        [Test]
        public void EmptyOptionalChainIsSafeToPassWithoutOpeningAWindow()
        {
            var payload = new List<byte> { 0, 0, 0 };
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 0);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectChain,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.Choices, Has.Count.EqualTo(1));
            Assert.That(
                DuelPromptPresentationRules.ShouldAutoPassEmptyChain(prompt),
                Is.True);
            Assert.That(
                DuelPromptPresentationRules.RequiresVisibleResponseTray(prompt),
                Is.False);
        }

        [Test]
        public void ChainWithARealResponseStillRequiresThePlayer()
        {
            var payload = new List<byte> { 0, 0, 0 };
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 1);
            UInt32(payload, 89631139);
            Location(
                payload,
                0,
                (byte)DuelLocation.SpellTrapZone,
                0,
                8);
            UInt64(payload, 0);
            payload.Add(0);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectChain,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.Choices, Has.Count.EqualTo(2));
            Assert.That(
                DuelPromptPresentationRules.ShouldAutoPassEmptyChain(prompt),
                Is.False);
            Assert.That(
                DuelPromptPresentationRules.RequiresVisibleResponseTray(prompt),
                Is.True);
            Assert.That(
                DuelPromptPresentationRules.ShouldUseCompactResponseBar(prompt),
                Is.True);
            Assert.That(
                DuelPromptPresentationRules.ActionableResponseChoices(prompt),
                Has.Count.EqualTo(1));
            Assert.That(
                DuelPromptPresentationRules.DeclineChoice(prompt)?.Label,
                Does.Contain("responder"));
        }

        [Test]
        public void ForcedChainStillOpensTheCompleteResponseTray()
        {
            var payload = new List<byte> { 0, 0, 1 };
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 1);
            UInt32(payload, 89631139);
            Location(
                payload,
                0,
                (byte)DuelLocation.SpellTrapZone,
                0,
                8);
            UInt64(payload, 0);
            payload.Add(0);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectChain,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.Forced, Is.True);
            Assert.That(
                DuelPromptPresentationRules.ShouldUseCompactResponseBar(prompt),
                Is.False);
            Assert.That(
                DuelPromptPresentationRules.DeclineChoice(prompt),
                Is.Null);
        }

        [Test]
        public void EffectQuestionUsesCompactRespondOrPassControls()
        {
            var payload = new List<byte> { 0 };
            UInt32(payload, 97268402);
            Location(
                payload,
                0,
                (byte)DuelLocation.Hand,
                2,
                1);
            UInt64(payload, 0);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectEffectYesNo,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(
                DuelPromptPresentationRules.ShouldUseCompactResponseBar(prompt),
                Is.True);
            Assert.That(
                DuelPromptPresentationRules.ActionableResponseChoices(prompt)
                    .Single().Label,
                Does.Contain("Ativar"));
            Assert.That(
                DuelPromptPresentationRules.DeclineChoice(prompt)?.Label,
                Does.Contain("ativar"));
        }

        [Test]
        public void UnselectPromptReturnsTheIterativePairExpectedByCore()
        {
            var payload = new List<byte> { 0, 1, 0 };
            UInt32(payload, 1);
            UInt32(payload, 2);
            UInt32(payload, 1);
            UInt32(payload, 89631139);
            Location(
                payload,
                0,
                (byte)DuelLocation.Hand,
                3,
                1);
            UInt32(payload, 0);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectUnselectCard,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.Choices, Has.Count.EqualTo(2));
            Assert.That(
                prompt.Choices[0].Response,
                Is.EqualTo(new byte[]
                {
                    1, 0, 0, 0,
                    0, 0, 0, 0
                }));
            Assert.That(
                prompt.Choices[1].Response,
                Is.EqualTo(CoreMessageDecoder.IntResponse(-1)));
            Assert.That(
                DeterministicDuelPolicy.Choose(prompt),
                Is.SameAs(prompt.Choices[0]),
                "A política deve selecionar ao menos uma carta antes de concluir.");
        }

        [Test]
        public void UnselectPolicyFinishesAfterASelectedCardExists()
        {
            var payload = new List<byte> { 0, 1, 0 };
            UInt32(payload, 1);
            UInt32(payload, 2);
            UInt32(payload, 0);
            UInt32(payload, 1);
            UInt32(payload, 89631139);
            Location(
                payload,
                0,
                (byte)DuelLocation.Hand,
                3,
                1);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectUnselectCard,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(
                DeterministicDuelPolicy.Choose(prompt),
                Is.SameAs(prompt.Choices[1]),
                "A política deve concluir depois que já existe uma carta selecionada.");
        }

        [Test]
        public void SumPromptValidatesAlternativeMaterialTotals()
        {
            var payload = new List<byte> { 0, 0 };
            UInt32(payload, 3);
            UInt32(payload, 1);
            UInt32(payload, 2);
            UInt32(payload, 0);
            UInt32(payload, 2);
            UInt32(payload, 11111111);
            Location(
                payload,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                1);
            UInt32(payload, 1);
            UInt32(payload, 22222222);
            Location(
                payload,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                1);
            UInt32(payload, 2);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectSum,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.RequiredSum, Is.EqualTo(3));
            Assert.That(
                CoreMessageDecoder.IsValidSelection(prompt, new[] { 0 }),
                Is.False);
            Assert.That(
                CoreMessageDecoder.IsValidSelection(prompt, new[] { 0, 1 }),
                Is.True);
            DuelChoice automatic = DeterministicDuelPolicy.Choose(prompt);
            Assert.That(
                automatic.Response,
                Is.EqualTo(
                    CoreMessageDecoder.CardSelectionResponse(
                        new uint[] { 0, 1 })));
        }

        [Test]
        public void SumAtLeastPromptCanCombineAllSelectableMaterials()
        {
            var payload = new List<byte> { 0, 1 };
            UInt32(payload, 6);
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 2);
            UInt32(payload, 11111111);
            Location(
                payload,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                1);
            UInt32(payload, 3);
            UInt32(payload, 22222222);
            Location(
                payload,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                1);
            UInt32(payload, 3);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectSum,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.SumAtLeast, Is.True);
            Assert.That(prompt.MaximumSelections, Is.EqualTo(2));
            Assert.That(
                CoreMessageDecoder.IsValidSelection(prompt, new[] { 0 }),
                Is.False);
            Assert.That(
                CoreMessageDecoder.IsValidSelection(prompt, new[] { 0, 1 }),
                Is.True);
            Assert.That(
                DeterministicDuelPolicy.Choose(prompt).Response,
                Is.EqualTo(
                    CoreMessageDecoder.CardSelectionResponse(
                        new uint[] { 0, 1 })));
        }

        [Test]
        public void TributePromptUsesReleaseValueInsteadOfCardCount()
        {
            var payload = new List<byte> { 0, 0 };
            UInt32(payload, 2);
            UInt32(payload, 2);
            UInt32(payload, 2);

            UInt32(payload, 50354944);
            payload.Add(0);
            payload.Add((byte)DuelLocation.MonsterZone);
            UInt32(payload, 0);
            payload.Add(2);

            UInt32(payload, 10000001);
            payload.Add(0);
            payload.Add((byte)DuelLocation.MonsterZone);
            UInt32(payload, 1);
            payload.Add(1);

            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectTribute,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(
                prompt.Choices.Single(choice => choice.ChoiceIndex == 0)
                    .SumValue,
                Is.EqualTo(2));
            Assert.That(
                CoreMessageDecoder.IsValidSelection(prompt, new[] { 0 }),
                Is.True);
            Assert.That(
                CoreMessageDecoder.IsValidSelection(prompt, new[] { 1 }),
                Is.False);
            Assert.That(
                DeterministicDuelPolicy.Choose(prompt).Response,
                Is.EqualTo(
                    CoreMessageDecoder.CardSelectionResponse(
                        new uint[] { 0 })));
        }

        [Test]
        public void AnnounceAndSortPromptsProduceNativeWidthResponses()
        {
            var racePayload = new List<byte> { 0, 1 };
            UInt64(racePayload, (1UL << 0) | (1UL << 13));
            var racePacket = new List<byte>();
            Packet(
                racePacket,
                (byte)CoreMessage.AnnounceRace,
                racePayload.ToArray());
            DuelPrompt racePrompt =
                CoreMessageDecoder.Decode(racePacket.ToArray())[0].Prompt;
            Assert.That(racePrompt.Choices, Has.Count.EqualTo(2));
            Assert.That(
                racePrompt.Choices.All(choice => choice.Response.Length == 8),
                Is.True);

            var attributePayload = new List<byte> { 0, 1 };
            UInt32(attributePayload, (1U << 2) | (1U << 5));
            var attributePacket = new List<byte>();
            Packet(
                attributePacket,
                (byte)CoreMessage.AnnounceAttribute,
                attributePayload.ToArray());
            DuelPrompt attributePrompt =
                CoreMessageDecoder.Decode(attributePacket.ToArray())[0].Prompt;
            Assert.That(attributePrompt.Choices, Has.Count.EqualTo(2));
            Assert.That(
                attributePrompt.Choices.All(
                    choice => choice.Response.Length == 4),
                Is.True);
            Assert.That(
                attributePrompt.Choices.Select(choice => choice.Response),
                Does.Contain(CoreMessageDecoder.IntResponse(1 << 5)));

            var numberPayload = new List<byte> { 0, 2 };
            UInt64(numberPayload, 3);
            UInt64(numberPayload, 6);
            var numberPacket = new List<byte>();
            Packet(
                numberPacket,
                (byte)CoreMessage.AnnounceNumber,
                numberPayload.ToArray());
            DuelPrompt numberPrompt =
                CoreMessageDecoder.Decode(numberPacket.ToArray())[0].Prompt;
            Assert.That(numberPrompt.Choices, Has.Count.EqualTo(2));
            Assert.That(
                numberPrompt.Choices[1].Response,
                Is.EqualTo(CoreMessageDecoder.IntResponse(1)));

            var sortPayload = new List<byte> { 0 };
            UInt32(sortPayload, 3);
            for (uint index = 0; index < 3; index++)
            {
                UInt32(sortPayload, 10000000 + index);
                sortPayload.Add(0);
                UInt32(sortPayload, DuelLocation.Deck);
                UInt32(sortPayload, index);
            }
            var sortPacket = new List<byte>();
            Packet(
                sortPacket,
                (byte)CoreMessage.SortCard,
                sortPayload.ToArray());
            DuelPrompt sortPrompt =
                CoreMessageDecoder.Decode(sortPacket.ToArray())[0].Prompt;
            Assert.That(sortPrompt.Choices, Has.Count.EqualTo(7));
            Assert.That(
                sortPrompt.Choices.Take(6)
                    .All(choice => choice.Response.Length == 3),
                Is.True);
        }

        [Test]
        public void AnnounceCardProducesSelectableNativeCardCodes()
        {
            const ulong isCode = 0x4000010000000000;
            const ulong or = 0x4000000500000000;
            var payload = new List<byte> { 0, 5 };
            UInt64(payload, 24508238);
            UInt64(payload, isCode);
            UInt64(payload, 53094821);
            UInt64(payload, isCode);
            UInt64(payload, or);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.AnnounceCard,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt, Is.Not.Null);
            Assert.That(prompt.Message, Is.EqualTo(CoreMessage.AnnounceCard));
            Assert.That(prompt.Choices.Select(choice => choice.CardCode),
                Is.EquivalentTo(new uint[] { 24508238, 53094821 }));
            Assert.That(prompt.Choices.All(choice =>
                choice.Response.Length == 4), Is.True);
        }

        [Test]
        public void SortPromptAcceptsMoreThanEightCardsWithoutExploding()
        {
            const uint count = 12;
            var payload = new List<byte> { 0 };
            UInt32(payload, count);
            for (uint index = 0; index < count; index++)
            {
                UInt32(payload, 20000000 + index);
                payload.Add(0);
                UInt32(payload, DuelLocation.Deck);
                UInt32(payload, index);
            }
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SortCard,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt.Choices, Has.Count.EqualTo(3));
            Assert.That(prompt.Choices.Take(2).All(
                choice => choice.Response.Length == count), Is.True);
            Assert.That(prompt.Choices[2].Response, Is.EqualTo(new byte[] { 0xFF }));
        }

        [Test]
        public void SelectCounterProducesNativePerCardAllocationResponses()
        {
            var payload = new List<byte> { 0 };
            UInt16(payload, 0x1234);
            UInt16(payload, 3);
            UInt32(payload, 2);
            UInt32(payload, 10000001);
            payload.Add(0);
            payload.Add((byte)DuelLocation.MonsterZone);
            payload.Add(0);
            UInt16(payload, 2);
            UInt32(payload, 10000002);
            payload.Add(0);
            payload.Add((byte)DuelLocation.MonsterZone);
            payload.Add(1);
            UInt16(payload, 3);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SelectCounter,
                payload.ToArray());

            DuelPrompt prompt =
                CoreMessageDecoder.Decode(framed.ToArray())[0].Prompt;

            Assert.That(prompt, Is.Not.Null);
            Assert.That(prompt.Message, Is.EqualTo(CoreMessage.SelectCounter));
            Assert.That(prompt.Player, Is.Zero);
            Assert.That(prompt.Choices, Is.Not.Empty);
            Assert.That(prompt.Choices.All(choice =>
                choice.Response.Length == 4), Is.True);
            Assert.That(prompt.Choices.Select(choice => choice.Response),
                Does.Contain(new byte[] { 2, 0, 1, 0 }));
        }

        [Test]
        public void PayLifePointCostUpdatesPresentationLifePoints()
        {
            var payload = new List<byte> { 1 };
            UInt32(payload, 1200);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.PayLifePointCost,
                payload.ToArray());

            DuelEvent duelEvent =
                CoreMessageDecoder.Decode(framed.ToArray())[0];
            var state = new ArcaneDuel.DuelEngine.State.DuelPresentationState(null);
            state.Apply(duelEvent);

            Assert.That(duelEvent.Player, Is.EqualTo(1));
            Assert.That(duelEvent.Value, Is.EqualTo(1200));
            Assert.That(state.Players[1].LifePoints, Is.EqualTo(6800));
        }

        [Test]
        public void SwapGraveDeckSkipsNativeHeaderBeforeItsBitfield()
        {
            var payload = new List<byte> { 1 };
            UInt32(payload, 4);
            UInt32(payload, 2);
            payload.Add(0x05);
            payload.Add(0x80);
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.SwapGraveDeck,
                payload.ToArray());

            DuelEvent duelEvent =
                CoreMessageDecoder.Decode(framed.ToArray())[0];

            Assert.That(duelEvent.Player, Is.EqualTo(1));
            Assert.That(duelEvent.Value, Is.EqualTo(4));
            Assert.That(duelEvent.Codes, Is.EqualTo(new uint[] { 0x05, 0x80 }));
        }

        [Test]
        public void KnownPresentationEventsAreNotReportedAsUnknown()
        {
            var framed = new List<byte>();
            Packet(
                framed,
                (byte)CoreMessage.PositionChange,
                new byte[] { 1, 2, 3, 4 });

            DuelEvent duelEvent =
                CoreMessageDecoder.Decode(framed.ToArray())[0];

            Assert.That(duelEvent.IsUnknown, Is.False);
            Assert.That(
                duelEvent.Message,
                Is.EqualTo(CoreMessage.PositionChange));
        }

        private static void Packet(List<byte> output, byte message, byte[] payload)
        {
            uint size = (uint)payload.Length + 1;
            output.Add((byte)size);
            output.Add((byte)(size >> 8));
            output.Add((byte)(size >> 16));
            output.Add((byte)(size >> 24));
            output.Add(message);
            output.AddRange(payload);
        }

        private static void UInt32(List<byte> output, uint value)
        {
            output.Add((byte)value);
            output.Add((byte)(value >> 8));
            output.Add((byte)(value >> 16));
            output.Add((byte)(value >> 24));
        }

        private static void UInt16(List<byte> output, ushort value)
        {
            output.Add((byte)value);
            output.Add((byte)(value >> 8));
        }

        private static void UInt64(List<byte> output, ulong value)
        {
            UInt32(output, (uint)value);
            UInt32(output, (uint)(value >> 32));
        }

        private static void Location(
            List<byte> output,
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            output.Add(controller);
            output.Add(location);
            UInt32(output, sequence);
            UInt32(output, position);
        }
    }
}
