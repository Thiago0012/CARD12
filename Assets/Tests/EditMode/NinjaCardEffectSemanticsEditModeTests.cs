using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Protocol;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class NinjaCardEffectSemanticsEditModeTests
    {
        private const uint Jioh = 92962242;
        private const uint Vanilla = 1784619;
        private const uint FaceUpAttack = 0x1;
        private const uint FaceDownDefense = 0x8;

        [Test]
        public void JiohResolvesBothEffectsAndLocksTurnedMonsters()
        {
            uint[] deck = Enumerable.Repeat(Vanilla, 40).ToArray();
            var configuration = new DuelConfiguration
            {
                Seed = 0x9296224200C0FFEEUL,
                StartingLifePoints = 20000,
                StartingHand = 0,
                PlayerDeck = (uint[])deck.Clone(),
                OpponentDeck = (uint[])deck.Clone(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>(),
                SimpleOpponentAi = false,
                ShuffleMainDecks = false
            };

            int turns = 0;
            int retries = 0;
            int unknown = 0;
            int jiohChains = 0;
            int jiohDestroys = 0;
            bool resolvingJioh = false;
            bool firstEffectResolved = false;
            bool opponentFlipChosen = false;
            bool lockedPositionChoiceOffered = false;
            var positionChanges = new List<CardLocation>();
            var activatedEffectIds = new List<ulong>();

            using (OcgDuelEngine engine =
                   OcgDuelEngine.CreateDefault(configuration))
            {
                // Jioh can be Tribute Summoned immediately. The opposing
                // field has two face-up targets and one facedown monster that
                // will trigger Jioh's destruction effect on the next turn.
                engine.AddCardAt(
                    0, Vanilla, DuelLocation.MonsterZone, 0, FaceUpAttack);
                engine.AddCardAt(
                    0, Vanilla, DuelLocation.MonsterZone, 1, FaceUpAttack);
                engine.AddCardAt(0, Jioh, DuelLocation.Hand, 0, 0);
                engine.AddCardAt(
                    1, Vanilla, DuelLocation.MonsterZone, 0, FaceUpAttack);
                engine.AddCardAt(
                    1, Vanilla, DuelLocation.MonsterZone, 1, FaceUpAttack);
                engine.AddCardAt(
                    1, Vanilla, DuelLocation.MonsterZone, 2, FaceDownDefense);

                engine.EventReceived += duelEvent =>
                {
                    if (duelEvent.Message == CoreMessage.NewTurn) turns++;
                    if (duelEvent.Message == CoreMessage.Retry) retries++;
                    if (duelEvent.IsUnknown) unknown++;
                    if (duelEvent.Message == CoreMessage.Chaining &&
                        duelEvent.Code == Jioh)
                    {
                        resolvingJioh = true;
                        jiohChains++;
                        activatedEffectIds.Add(duelEvent.DescriptionId);
                    }
                    if (resolvingJioh &&
                        duelEvent.Message == CoreMessage.PositionChange)
                    {
                        positionChanges.Add(duelEvent.Current);
                    }
                    if (resolvingJioh &&
                        duelEvent.Message == CoreMessage.Move &&
                        duelEvent.Previous?.Controller == 1 &&
                        duelEvent.Current?.Location == DuelLocation.Graveyard)
                    {
                        jiohDestroys++;
                    }
                    if (duelEvent.Message == CoreMessage.ChainEnd)
                    {
                        if (resolvingJioh && positionChanges.Count >= 2)
                            firstEffectResolved = true;
                        resolvingJioh = false;
                    }
                };

                engine.Start();
                int decisions = 0;
                while (!engine.IsFinished && decisions++ < 500 &&
                       jiohDestroys == 0)
                {
                    DuelPrompt prompt = engine.CurrentPrompt;
                    Assert.That(
                        prompt,
                        Is.Not.Null,
                        "The Core must never await an untyped Ninja choice.");
                    byte[] response = Choose(
                        prompt,
                        resolvingJioh,
                        firstEffectResolved,
                        ref opponentFlipChosen,
                        ref lockedPositionChoiceOffered);
                    Assert.That(response, Is.Not.Null.And.Not.Empty);
                    engine.SubmitResponse(response);
                }

                Assert.That(
                    engine.NativeLogs.Where(log =>
                        log.StartsWith("[0]", StringComparison.Ordinal)),
                    Is.Empty,
                    "The official Ninja scripts emitted a Core error.");
                Assert.That(
                    engine.TryCaptureFieldSnapshot(
                        out OcgFieldSnapshot snapshot),
                    Is.True);
                Assert.That(snapshot.Players[0].Monsters[0].Code,
                    Is.EqualTo(Jioh));
                Assert.That(snapshot.Players[1].Monsters[0], Is.Null);
                Assert.That(snapshot.Players[1].Monsters[1].Position,
                    Is.EqualTo(FaceDownDefense));
                Assert.That(snapshot.Players[1].Monsters[2].Position,
                    Is.EqualTo(FaceUpAttack));
                Assert.That(
                    snapshot.Players[1].Graveyard.Count(card =>
                        card?.Code == Vanilla),
                    Is.EqualTo(1));
            }

            Assert.That(turns, Is.GreaterThanOrEqualTo(2));
            Assert.That(retries, Is.Zero);
            Assert.That(unknown, Is.Zero);
            Assert.That(jiohChains, Is.EqualTo(2));
            Assert.That(
                activatedEffectIds,
                Is.EqualTo(new[]
                {
                    (ulong)Jioh << 20,
                    ((ulong)Jioh << 20) | 4
                }),
                "Each Jioh chain must keep the exact selected effect ID.");
            Assert.That(jiohDestroys, Is.EqualTo(1));
            Assert.That(opponentFlipChosen, Is.True);
            Assert.That(lockedPositionChoiceOffered, Is.False,
                "A monster locked by Jioh was still offered as a manual " +
                "position-change action.");
            Assert.That(positionChanges, Has.Count.EqualTo(2));
            Assert.That(positionChanges.Select(location => location.Controller),
                Is.All.EqualTo(1));
            Assert.That(positionChanges.Select(location => location.Sequence),
                Is.EquivalentTo(new uint[] { 0, 1 }));
            Assert.That(positionChanges.Select(location => location.Position),
                Is.All.EqualTo(FaceDownDefense));
        }

        private static byte[] Choose(
            DuelPrompt prompt,
            bool resolvingJioh,
            bool firstEffectResolved,
            ref bool opponentFlipChosen,
            ref bool lockedPositionChoiceOffered)
        {
            if (prompt.Message == CoreMessage.SelectIdleCommand)
            {
                if (prompt.Player == 0 && !firstEffectResolved)
                {
                    DuelChoice summon = prompt.Choices.FirstOrDefault(choice =>
                        choice.CardCode == Jioh &&
                        Contains(choice.Label, "Invocar") &&
                        !Contains(choice.Label, "especial"));
                    if (summon != null) return summon.Response;
                }

                if (prompt.Player == 1 && firstEffectResolved &&
                    !opponentFlipChosen)
                {
                    lockedPositionChoiceOffered = prompt.Choices.Any(choice =>
                        Contains(choice.Label, "Mudar") &&
                        choice.Controller == 1 &&
                        choice.Location == DuelLocation.MonsterZone &&
                        (choice.Sequence == 0 || choice.Sequence == 1));
                    DuelChoice flip = prompt.Choices.FirstOrDefault(choice =>
                        Contains(choice.Label, "Mudar") &&
                        choice.Controller == 1 &&
                        choice.Location == DuelLocation.MonsterZone &&
                        choice.Sequence == 2);
                    if (flip != null)
                    {
                        opponentFlipChosen = true;
                        return flip.Response;
                    }
                }

                return (prompt.Choices.FirstOrDefault(choice =>
                            Contains(choice.Label, "Encerrar turno")) ??
                        DeterministicDuelPolicy.Choose(prompt)).Response;
            }

            if (prompt.Message == CoreMessage.SelectEffectYesNo ||
                prompt.Message == CoreMessage.SelectYesNo)
            {
                return prompt.Choices.First(choice =>
                    choice.Response?.Length == sizeof(int) &&
                    ReadInt32(choice.Response) != 0).Response;
            }

            if (prompt.Message == CoreMessage.SelectCard && resolvingJioh)
            {
                DuelChoice[] opponent = prompt.Choices
                    .Where(choice =>
                        choice.ChoiceIndex >= 0 &&
                        choice.Controller == 1 &&
                        choice.Location == DuelLocation.MonsterZone)
                    .OrderBy(choice => choice.Sequence)
                    .ToArray();
                int wanted = firstEffectResolved
                    ? 1
                    : Math.Min(2, opponent.Length);
                Assert.That(opponent, Has.Length.GreaterThanOrEqualTo(wanted));
                return CoreMessageDecoder.CardSelectionResponse(
                    opponent.Take(wanted)
                        .Select(choice => (uint)choice.ChoiceIndex)
                        .ToArray());
            }

            if (prompt.Message == CoreMessage.SelectChain)
            {
                DuelChoice jioh = prompt.Choices.FirstOrDefault(choice =>
                    choice.CardCode == Jioh);
                if (jioh != null) return jioh.Response;
                DuelChoice pass = prompt.Choices.FirstOrDefault(choice =>
                    choice.Response?.Length == sizeof(int) &&
                    ReadInt32(choice.Response) == -1);
                if (pass != null) return pass.Response;
            }

            return DeterministicDuelPolicy.Choose(prompt).Response;
        }

        private static int ReadInt32(byte[] bytes)
        {
            return bytes[0] |
                   (bytes[1] << 8) |
                   (bytes[2] << 16) |
                   (bytes[3] << 24);
        }

        private static bool Contains(string source, string fragment)
        {
            return (source ?? string.Empty).IndexOf(
                fragment,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
