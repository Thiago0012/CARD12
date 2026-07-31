using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class MausoleumLockdownEdisonContentEditModeTests
    {
        [Test]
        public void MausoleumDeckHasTheExactMainAndExtraDeckSizes()
        {
            Assert.That(
                CuratedDeckLists.MausoleumLockdownEdisonMain,
                Has.Length.EqualTo(40));
            Assert.That(
                CuratedDeckLists.MausoleumLockdownEdisonExtra,
                Has.Length.EqualTo(15));
            Assert.That(
                CuratedDeckLists.MausoleumLockdownEdisonMain
                    .Concat(CuratedDeckLists.MausoleumLockdownEdisonExtra)
                    .Distinct()
                    .Count(),
                Is.EqualTo(35));
        }

        [Test]
        public void EveryMausoleumCardHasPortugueseDataArtAndOfficialScript()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            uint[] codes = CuratedDeckLists.MausoleumLockdownEdisonMain
                .Concat(CuratedDeckLists.MausoleumLockdownEdisonExtra)
                .Distinct()
                .ToArray();

            Assert.That(
                database.Get(80921533).Name,
                Is.EqualTo("Mausoléu do Imperador"));
            Assert.That(
                database.Get(37694547).Name,
                Is.EqualTo("Vila Mecânica"));
            foreach (uint code in codes)
            {
                CardRecord card = database.Get(code);
                Assert.That(card.Name, Is.Not.Empty, code.ToString());
                Assert.That(card.Description, Is.Not.Empty, card.Name);
                Assert.That(
                    visuals.TryGet(code, out CardVisualData visual),
                    Is.True,
                    card.Name);
                Assert.That(
                    File.Exists(visuals.ArtPath(code)),
                    Is.True,
                    card.Name);
                Assert.That(
                    visual.scriptStatus,
                    Is.EqualTo("true"),
                    card.Name);
                Assert.That(
                    File.Exists(Path.Combine(
                        Application.streamingAssetsPath,
                        "Ygo",
                        "Scripts",
                        "official",
                        visual.scriptFile)),
                    Is.True,
                    card.Name);
            }
        }
    }
}
