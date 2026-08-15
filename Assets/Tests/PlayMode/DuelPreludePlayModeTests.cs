using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class DuelPreludePlayModeTests
    {
        [TestCase("Rock", "Rock", "Tie")]
        [TestCase("Paper", "Paper", "Tie")]
        [TestCase("Scissors", "Scissors", "Tie")]
        [TestCase("Rock", "Scissors", "PlayerOne")]
        [TestCase("Paper", "Rock", "PlayerOne")]
        [TestCase("Scissors", "Paper", "PlayerOne")]
        [TestCase("Scissors", "Rock", "PlayerTwo")]
        [TestCase("Rock", "Paper", "PlayerTwo")]
        [TestCase("Paper", "Scissors", "PlayerTwo")]
        public void RockPaperScissorsResolvesEveryPair(
            string first,
            string second,
            string expected)
        {
            Type choiceType = TypeByName(
                "ArcaneArena.Presentation.DuelPreludeChoice");
            Type rulesType = TypeByName(
                "ArcaneArena.Presentation.DuelPreludeRules");
            MethodInfo resolve = rulesType.GetMethod(
                "Resolve",
                BindingFlags.Public | BindingFlags.Static);

            object result = resolve.Invoke(
                null,
                new[]
                {
                    Enum.Parse(choiceType, first),
                    Enum.Parse(choiceType, second)
                });

            Assert.That(result.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void OnlinePreludeHasVersionedRoundChoiceAndResultMessages()
        {
            Type session = TypeByName(
                "ArcaneArena.Multiplayer.DuelOnlineSession");
            BindingFlags hidden = BindingFlags.NonPublic |
                                  BindingFlags.Static;
            Assert.That(
                session.GetField("PreludeMessage", hidden)
                    ?.GetRawConstantValue(),
                Is.EqualTo("arcane.duel.prelude.v1"));
            Assert.That(
                session.GetField("PreludeChoiceMessage", hidden)
                    ?.GetRawConstantValue(),
                Is.EqualTo("arcane.duel.prelude-choice.v1"));
            Assert.That(
                session.GetField("PreludeResultMessage", hidden)
                    ?.GetRawConstantValue(),
                Is.EqualTo("arcane.duel.prelude-result.v1"));

            Type result = session.GetNestedType(
                "PreludeResultPayload",
                BindingFlags.NonPublic);
            Assert.That(result?.GetField("round"), Is.Not.Null);
            Assert.That(result?.GetField("hostChoice"), Is.Not.Null);
            Assert.That(result?.GetField("clientChoice"), Is.Not.Null);
            Assert.That(result?.GetField("winnerSeat"), Is.Not.Null);
            Assert.That(result?.GetField("tie"), Is.Not.Null);
        }

        private static Type TypeByName(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
