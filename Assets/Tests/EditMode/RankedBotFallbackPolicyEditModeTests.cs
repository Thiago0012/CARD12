using ArcaneDuel.Game;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class RankedBotFallbackPolicyEditModeTests
    {
        [TestCase(-1f, 30f)]
        [TestCase(0f, 30f)]
        [TestCase(0.5f, 55f)]
        [TestCase(1f, 80f)]
        [TestCase(2f, 80f)]
        public void DelayAlwaysStaysInsideRequestedWindow(
            float sample,
            float expected)
        {
            Assert.That(
                RankedBotFallbackPolicy.DelaySeconds(sample),
                Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void InvalidSampleFallsBackToMinimumDelay()
        {
            Assert.That(
                RankedBotFallbackPolicy.DelaySeconds(float.NaN),
                Is.EqualTo(30f));
            Assert.That(
                RankedBotFallbackPolicy.DelaySeconds(float.PositiveInfinity),
                Is.EqualTo(30f));
        }
    }
}
