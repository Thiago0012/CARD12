using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class DuelStateCountEditModeTests
    {
        [Test]
        public void DeckAndExtraCountsFollowMovesInBothDirections()
        {
            var state = new DuelPresentationState(null);
            state.ConfigureDeckCounts(40, 15, 40, 15);

            state.Apply(Move(
                10000001,
                0,
                (byte)DuelLocation.Deck,
                0,
                0,
                0,
                (byte)DuelLocation.Hand,
                0,
                0));
            Assert.That(state.Players[0].DeckCount, Is.EqualTo(39));
            Assert.That(state.Players[0].Hand, Has.Count.EqualTo(1));

            state.Apply(Move(
                10000001,
                0,
                (byte)DuelLocation.Hand,
                0,
                0,
                0,
                (byte)DuelLocation.Deck,
                0,
                0));
            Assert.That(state.Players[0].DeckCount, Is.EqualTo(40));
            Assert.That(state.Players[0].Hand, Is.Empty);

            state.Apply(Move(
                10000002,
                0,
                (byte)DuelLocation.Extra,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                1));
            Assert.That(state.Players[0].ExtraDeckCount, Is.EqualTo(14));
            Assert.That(state.Players[0].MonsterZones[2], Is.EqualTo(10000002));

            state.Apply(Move(
                10000002,
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                1,
                0,
                (byte)DuelLocation.Extra,
                0,
                0));
            Assert.That(state.Players[0].ExtraDeckCount, Is.EqualTo(15));
            Assert.That(state.Players[0].MonsterZones[2], Is.Zero);
        }

        [Test]
        public void CountOnlyPrivateDeckSurvivesDrawAndReturnWithoutLeakingContents()
        {
            var state = new DuelPresentationState(null);
            state.ConfigureDeckCounts(40, 15, 40, 15);

            state.Apply(Draw(0, 10000003));

            Assert.That(state.Players[0].DeckCount, Is.EqualTo(39));
            Assert.That(state.Players[0].Hand, Is.EqualTo(new[] { 10000003U }));

            state.Apply(Move(
                10000003,
                0,
                (byte)DuelLocation.Hand,
                0,
                0,
                0,
                (byte)DuelLocation.Deck,
                0,
                0));

            Assert.That(state.Players[0].DeckCount, Is.EqualTo(40));
            Assert.That(state.Players[0].Hand, Is.Empty);
            Assert.That(
                state.CaptureSnapshot().Players[0].Hand,
                Is.Empty,
                "Returning a card to a count-only pile must not expose its identity.");
        }

        private static DuelEvent Draw(byte player, params uint[] codes)
        {
            var payload = new List<byte> { player };
            UInt32(payload, (uint)(codes?.Length ?? 0));
            foreach (uint code in codes ?? System.Array.Empty<uint>())
            {
                UInt32(payload, code);
                UInt32(payload, 0);
            }

            var framed = new List<byte>();
            UInt32(framed, (uint)payload.Count + 1);
            framed.Add((byte)CoreMessage.Draw);
            framed.AddRange(payload);
            return CoreMessageDecoder.Decode(framed.ToArray())[0];
        }

        private static DuelEvent Move(
            uint code,
            byte previousController,
            byte previousLocation,
            uint previousSequence,
            uint previousPosition,
            byte currentController,
            byte currentLocation,
            uint currentSequence,
            uint currentPosition)
        {
            var payload = new List<byte>();
            UInt32(payload, code);
            Location(
                payload,
                previousController,
                previousLocation,
                previousSequence,
                previousPosition);
            Location(
                payload,
                currentController,
                currentLocation,
                currentSequence,
                currentPosition);
            UInt32(payload, 0);

            var framed = new List<byte>();
            uint size = (uint)payload.Count + 1;
            UInt32(framed, size);
            framed.Add((byte)CoreMessage.Move);
            framed.AddRange(payload);
            return CoreMessageDecoder.Decode(framed.ToArray())[0];
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

        private static void UInt32(List<byte> output, uint value)
        {
            output.Add((byte)value);
            output.Add((byte)(value >> 8));
            output.Add((byte)(value >> 16));
            output.Add((byte)(value >> 24));
        }
    }
}
