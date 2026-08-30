using ArcaneArena;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class DuelRankMasteryPresentationEditModeTests
    {
        [Test]
        public void BadgeIsSlightlyLargerThanProfileIconAndStaysBounded()
        {
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveBadgeSize(100f),
                Is.EqualTo(112f).Within(0.001f));
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveBadgeSize(40f),
                Is.EqualTo(104f).Within(0.001f));
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveBadgeSize(300f),
                Is.EqualTo(148f).Within(0.001f));
        }

        [Test]
        public void IntroCompletesItsFastSpinInOneSecond()
        {
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveSpinDegrees(0f),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveSpinDegrees(0.5f),
                Is.LessThan(-700f));
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveSpinDegrees(1f),
                Is.EqualTo(-900f).Within(0.001f));
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveSpinDegrees(2f),
                Is.EqualTo(-900f).Within(0.001f));
        }

        [Test]
        public void PresentationFadesInAndCleansUpAtItsDuration()
        {
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveOpacity(-0.01f),
                Is.Zero);
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveOpacity(0.16f),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveOpacity(3.3f),
                Is.InRange(0f, 1f));
            Assert.That(
                DuelRankMasteryPresentationRules.ResolveOpacity(
                    DuelRankMasteryPresentationRules.TotalDuration),
                Is.Zero);
        }
    }
}
