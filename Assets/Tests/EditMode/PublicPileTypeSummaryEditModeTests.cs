using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class PublicPileTypeSummaryEditModeTests
    {
        [Test]
        public void MixedPileCountsEachCopyByTypeWithoutChangingItsOrder()
        {
            uint[] codes = { 59514116, 38033121, 44095762, 15146890, 59514116 };
            uint[] original = (uint[])codes.Clone();
            var summary = PublicPileTypeSummary.FromVisibleCodes(CardDatabase.LoadDefault(), codes);
            Assert.That(summary.Monsters, Is.EqualTo(2), "Pendulum is a Monster in a public pile.");
            Assert.That(summary.Spells, Is.EqualTo(2), "Count physical copies, not distinct names.");
            Assert.That(summary.Traps, Is.EqualTo(1));
            Assert.That(summary.Unidentified, Is.Zero);
            Assert.That(codes, Is.EqualTo(original), "Core sequence addresses must not change.");
        }

        [Test]
        public void ConcealedAndUnknownIdentitiesDoNotRevealTheirType()
        {
            var summary = PublicPileTypeSummary.FromVisibleCodes(
                CardDatabase.LoadDefault(), new uint[] { 0, uint.MaxValue });
            Assert.That(summary.Monsters + summary.Spells + summary.Traps, Is.Zero);
            Assert.That(summary.Unidentified, Is.EqualTo(2));
            Assert.That(summary.ToDisplayText(), Does.Contain("NÃO REVELADAS 2"));
        }

        [Test]
        public void EmptyPileHasZeroCounts()
        {
            var summary = PublicPileTypeSummary.FromVisibleCodes(null, null);
            Assert.That(summary.ToDisplayText(), Is.EqualTo("MONSTROS 0 · MAGIAS 0 · ARMADILHAS 0"));
        }
    }
}
