using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ShiranuiDeckContentEditModeTests
    {
        [Test]
        public void ShiranuiDeckHasTheExactMainAndExtraDeckSizes()
        {
            Assert.That(
                CuratedDeckLists.ShiranuiSupremacyMain,
                Has.Length.EqualTo(40));
            Assert.That(
                CuratedDeckLists.ShiranuiSupremacyExtra,
                Has.Length.EqualTo(11));
            Assert.That(
                CuratedDeckLists.ShiranuiSupremacyMain
                    .Concat(CuratedDeckLists.ShiranuiSupremacyExtra)
                    .Distinct()
                    .Count(),
                Is.EqualTo(34));
        }

        [Test]
        public void EveryShiranuiCardHasPortugueseDataArtAndOfficialScript()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            uint[] codes = CuratedDeckLists.ShiranuiSupremacyMain
                .Concat(CuratedDeckLists.ShiranuiSupremacyExtra)
                .Distinct()
                .ToArray();

            Assert.That(
                database.Get(12612470).Name,
                Is.EqualTo("Procissão do Jarro de Chá"));
            Assert.That(
                database.Get(30888983).Name,
                Is.EqualTo("A Seleção"));
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
