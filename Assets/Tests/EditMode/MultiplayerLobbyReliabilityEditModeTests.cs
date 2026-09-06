using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class MultiplayerLobbyReliabilityEditModeTests
    {
        private Type coordinatorType;

        [SetUp]
        public void ResolveCoordinator()
        {
            coordinatorType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "ArcaneArena.Multiplayer.MultiplayerSessionCoordinator",
                    false))
                .FirstOrDefault(type => type != null);
            Assert.That(coordinatorType, Is.Not.Null);
        }

        [TestCase("(429) HTTP/1.1 429 Too Many Requests")]
        [TestCase("The lobby rate limit was exceeded")]
        [TestCase("RATE LIMIT")]
        public void LobbyRateLimitDetectionRecognizesServiceResponses(
            string message)
        {
            MethodInfo method = coordinatorType.GetMethod(
                "IsRateLimited",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            bool detected = (bool)method.Invoke(
                null,
                new object[] { new InvalidOperationException(message) });
            Assert.That(detected, Is.True);
        }

        [Test]
        public void LobbyRateLimitDetectionDoesNotMaskOtherFailures()
        {
            MethodInfo method = coordinatorType.GetMethod(
                "IsRateLimited",
                BindingFlags.NonPublic | BindingFlags.Static);
            bool detected = (bool)method.Invoke(
                null,
                new object[]
                {
                    new InvalidOperationException("404 lobby not found")
                });
            Assert.That(detected, Is.False);
        }

        [Test]
        public void LobbyRetryBackoffIsProgressiveAndCapped()
        {
            MethodInfo method = coordinatorType.GetMethod(
                "LobbyRetryDelayMilliseconds",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            int[] actual = Enumerable.Range(1, 6)
                .Select(attempt => (int)method.Invoke(
                    null,
                    new object[] { attempt }))
                .ToArray();
            Assert.That(actual,
                Is.EqualTo(new[] { 2000, 4000, 8000, 12000, 12000, 12000 }));
        }
    }
}
