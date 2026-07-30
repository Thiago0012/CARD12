using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.State;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class PresentationRebuildEditModeTests
    {
        [Test]
        public void PresentationCanBeRebuiltFromSnapshotWithoutGameObjects()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            var source = new DuelPresentationState(database);
            source.ConfigureDeckCounts(45, 7, 40, 3);
            source.Players[0].Hand.Add(89631139);
            source.Players[0].Hand.Add(46986414);
            source.Players[0].MonsterZones[2] = 89631139;

            DuelPresentationSnapshot snapshot = source.CaptureSnapshot();
            var rebuilt = new DuelPresentationState(database);
            rebuilt.RestoreSnapshot(snapshot);

            Assert.That(rebuilt.Players[0].LifePoints, Is.EqualTo(8000));
            Assert.That(rebuilt.Players[0].DeckCount, Is.EqualTo(45));
            Assert.That(rebuilt.Players[0].ExtraDeckCount, Is.EqualTo(7));
            Assert.That(
                rebuilt.Players[0].Hand,
                Is.EqualTo(source.Players[0].Hand));
            Assert.That(
                rebuilt.Players[0].MonsterZones,
                Is.EqualTo(source.Players[0].MonsterZones));
            Assert.That(rebuilt.Log, Is.EqualTo(source.Log));
        }
    }
}
