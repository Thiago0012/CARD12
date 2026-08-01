using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Content;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class CuratedDeckBatchAugust2026EditModeTests
    {
        private static readonly uint[][] MainDecks =
        {
            CuratedDeckLists.CrimsonPowerforceMain,
            CuratedDeckLists.DarkMagicalBlastMain,
            CuratedDeckLists.HiddenArtsOfShadowsMain,
            CuratedDeckLists.BlackwingsPrideMain,
            CuratedDeckLists.DragonmaidToOrderX3Main,
            CuratedDeckLists.CyberneticSuccessorMain,
            CuratedDeckLists.RunickMain,
            CuratedDeckLists.ExodiaMain,
            CuratedDeckLists.BlueEyesMaxModifiedMain
        };

        private static readonly uint[][] ExtraDecks =
        {
            CuratedDeckLists.CrimsonPowerforceExtra,
            CuratedDeckLists.DarkMagicalBlastExtra,
            CuratedDeckLists.HiddenArtsOfShadowsExtra,
            CuratedDeckLists.BlackwingsPrideExtra,
            CuratedDeckLists.DragonmaidToOrderX3Extra,
            CuratedDeckLists.CyberneticSuccessorExtra,
            CuratedDeckLists.RunickExtra,
            CuratedDeckLists.ExodiaExtra,
            CuratedDeckLists.BlueEyesMaxModifiedExtra
        };

        [Test]
        public void CapturedDecksPreserveTheirPublishedLists()
        {
            int[] expectedMain = { 40, 40, 41, 40, 40, 40, 42, 40, 41 };
            int[] expectedExtra = { 8, 15, 14, 5, 8, 5, 9, 12, 5 };
            int[] expectedUnique = { 24, 35, 34, 45, 22, 44, 29, 26, 45 };

            for (int index = 0; index < MainDecks.Length; index++)
            {
                Assert.That(MainDecks[index], Has.Length.EqualTo(expectedMain[index]));
                Assert.That(ExtraDecks[index], Has.Length.EqualTo(expectedExtra[index]));
                Assert.That(
                    MainDecks[index].Concat(ExtraDecks[index]).Distinct().Count(),
                    Is.EqualTo(expectedUnique[index]));
            }

            Assert.That(
                MainDecks.SelectMany(cards => cards)
                    .Concat(ExtraDecks.SelectMany(cards => cards))
                    .Distinct()
                    .Count(),
                Is.EqualTo(301));
            Assert.That(
                CuratedDeckLists.BlueEyesMaxModifiedMain.Count(
                    code => code == 89631139),
                Is.EqualTo(2));
        }

        [Test]
        public void EveryCardHasPortuguesePresentationAndCoreRules()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            uint[] codes = MainDecks.SelectMany(cards => cards)
                .Concat(ExtraDecks.SelectMany(cards => cards))
                .Distinct()
                .ToArray();

            foreach (uint code in codes)
            {
                CardRecord card = database.Get(code);
                Assert.That(card.Name, Is.Not.Empty, code.ToString());
                Assert.That(card.Description, Is.Not.Empty, card.Name);
                Assert.That(card.Description, Does.Not.Contain("[ Pendulum Effect ]"), card.Name);
                Assert.That(card.Description, Does.Not.Contain("[ Monster Effect ]"), card.Name);
                Assert.That(card.Description, Does.Not.Contain("You can "), card.Name);
                Assert.That(card.Description, Does.Not.Contain("Special Summon"), card.Name);
                Assert.That(visuals.TryGet(code, out _), Is.True, card.Name);
                Assert.That(File.Exists(visuals.ArtPath(code)), Is.True, card.Name);
            }

            string[] problems = DuelContentValidator.FindProblems(
                database,
                YgoContentLocator.Root,
                MainDecks.Concat(ExtraDecks).ToArray());
            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        [Test]
        public void StoreAndLegacyLibraryUseOnlyTheNewReplacementStructures()
        {
            Type catalog = FindType("ArcaneArena.Frontend.DeckShopCatalog");
            object blueEyes = FindProduct(catalog, "BlueEyesProductId");
            object darkMagician = FindProduct(catalog, "DarkMagicianProductId");

            Assert.That(blueEyes, Is.Not.Null);
            Assert.That(darkMagician, Is.Not.Null);
            Assert.That(
                ProductCards(blueEyes, "MainDeckCardIds"),
                Is.EqualTo(AsIds(CuratedDeckLists.BlueEyesMaxModifiedMain)));
            Assert.That(
                ProductCards(blueEyes, "ExtraDeckCardIds"),
                Is.EqualTo(AsIds(CuratedDeckLists.BlueEyesMaxModifiedExtra)));
            Assert.That(
                ProductCards(darkMagician, "MainDeckCardIds"),
                Is.EqualTo(AsIds(CuratedDeckLists.DarkMagicalBlastMain)));
            Assert.That(
                ProductCards(darkMagician, "ExtraDeckCardIds"),
                Is.EqualTo(AsIds(CuratedDeckLists.DarkMagicalBlastExtra)));
            Assert.That(
                ProductText(darkMagician, "SourceUrl"),
                Does.Contain("dark-magical-blast-703036"));

            string[] addedProducts =
            {
                "CrimsonPowerforceProductId",
                "HiddenArtsOfShadowsProductId",
                "BlackwingsPrideProductId",
                "DragonmaidToOrderX3ProductId",
                "CyberneticSuccessorProductId",
                "RunickProductId",
                "ExodiaProductId"
            };
            Assert.That(
                addedProducts.All(field => FindProduct(catalog, field) != null),
                Is.True);

            DeckLibraryFile legacy = DeckLibraryRepository.CreateDefaults();
            Assert.That(
                legacy.Find("deck-dragao-branco").mainDeck,
                Is.EqualTo(CuratedDeckLists.BlueEyesMaxModifiedMain));
            Assert.That(
                legacy.Find("deck-mago-negro").mainDeck,
                Is.EqualTo(CuratedDeckLists.DarkMagicalBlastMain));
        }

        [Test]
        public void ExistingDarkMagicianListRemainsAuthored()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            Assert.That(CuratedDeckLists.DarkMagicianMain, Is.Not.Empty);
            Assert.That(CuratedDeckLists.DarkMagicianExtra, Is.Not.Empty);
            Assert.That(CuratedDeckLists.DarkMagicianMain, Has.Member(60948488));
            Assert.That(database.TryGet(46986414, out _), Is.True);
            Assert.That(visuals.TryGet(46986414, out _), Is.True);
            Assert.That(File.Exists(visuals.ArtPath(46986414)), Is.True);
        }

        private static string[] AsIds(IEnumerable<uint> codes)
        {
            return codes.Select(code => code.ToString()).ToArray();
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Runtime type {fullName} was not loaded.");
            return type;
        }

        private static object FindProduct(Type catalog, string idField)
        {
            string productId = catalog.GetField(idField).GetValue(null).ToString();
            return catalog.GetMethod("Find").Invoke(null, new object[] { productId });
        }

        private static string[] ProductCards(object product, string propertyName)
        {
            return ((IEnumerable<string>)product.GetType()
                    .GetProperty(propertyName)
                    .GetValue(product))
                .ToArray();
        }

        private static string ProductText(object product, string propertyName)
        {
            return product.GetType()
                .GetProperty(propertyName)
                .GetValue(product)
                .ToString();
        }
    }
}
