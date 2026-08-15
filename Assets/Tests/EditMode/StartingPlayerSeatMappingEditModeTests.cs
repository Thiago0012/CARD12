using System;
using System.Linq;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Protocol;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class StartingPlayerSeatMappingEditModeTests
    {
        private const uint BlueEyesWhiteDragon = 89631139;
        private const uint OpponentVanilla = 1784619;

        [Test]
        public void PlayerTwoCanStartWithoutChangingLogicalDeckSeats()
        {
            var configuration = new DuelConfiguration
            {
                StartingPlayer = 1,
                StartingHand = 0,
                ShuffleMainDecks = false,
                SimpleOpponentAi = false,
                PlayerDeck = Enumerable.Repeat(
                    BlueEyesWhiteDragon,
                    40).ToArray(),
                OpponentDeck = Enumerable.Repeat(
                    OpponentVanilla,
                    40).ToArray(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>()
            };
            byte firstTurnPlayer = byte.MaxValue;

            using (OcgDuelEngine engine =
                   OcgDuelEngine.CreateDefault(configuration))
            {
                engine.EventReceived += duelEvent =>
                {
                    if (duelEvent.Message == CoreMessage.NewTurn &&
                        firstTurnPlayer == byte.MaxValue)
                    {
                        firstTurnPlayer = duelEvent.Player;
                    }
                };
                engine.Start();

                Assert.That(firstTurnPlayer, Is.EqualTo(1));
                Assert.That(engine.CurrentPrompt, Is.Not.Null);
                Assert.That(engine.CurrentPrompt.Player, Is.EqualTo(1));
                Assert.That(
                    engine.TryCaptureFieldSnapshot(
                        out OcgFieldSnapshot snapshot),
                    Is.True);
                Assert.That(
                    snapshot.Players[0].Deck
                        .Where(card => card != null)
                        .All(card => card.Code == BlueEyesWhiteDragon),
                    Is.True);
                Assert.That(
                    snapshot.Players[1].Deck
                        .Where(card => card != null)
                        .All(card => card.Code == OpponentVanilla),
                    Is.True);
            }
        }
    }
}
