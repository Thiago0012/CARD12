using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class CardRarityCraftEditModeTests
    {
        [Test]
        public void R01_ImportedCatalogMatchesEveryPdfInvariant()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/CardData/MasterDuelRarities.json");
            Assert.That(asset, Is.Not.Null);
            RarityHeader header = JsonUtility.FromJson<RarityHeader>(asset.text);
            Assert.That(header.schemaVersion, Is.EqualTo(1));
            Assert.That(header.entryCount, Is.EqualTo(13_856));
            Assert.That(header.normalCount, Is.EqualTo(5_303));
            Assert.That(header.rareCount, Is.EqualTo(4_013));
            Assert.That(header.superRareCount, Is.EqualTo(2_895));
            Assert.That(header.ultraRareCount, Is.EqualTo(1_645));
        }

        [Test]
        public void R02_LocalizedNamesStayLocalizedAndAlternateArtKeepsItsRarity()
        {
            UnityEngine.Object catalog = Catalog();
            object blueEyes = FindCard(catalog, "89631139");
            Assert.That(Property(blueEyes, "DisplayName"),
                Is.EqualTo("Dragão Branco de Olhos Azuis"));
            Assert.That(Property(blueEyes, "EnglishName"),
                Is.EqualTo("Blue-Eyes White Dragon"));
            Assert.That(Property(blueEyes, "Rarity").ToString(), Is.EqualTo("UR"));

            object baseArt = FindCard(catalog, "24094653");
            object alternateArt = FindCard(catalog, "27847700");
            Assert.That(Property(baseArt, "Rarity").ToString(), Is.EqualTo("N"));
            Assert.That(Property(alternateArt, "Rarity").ToString(), Is.EqualTo("UR"));
            Assert.That(Property(baseArt, "DisplayName"),
                Is.EqualTo(Property(alternateArt, "DisplayName")));
        }

        [Test]
        public void R03_LeadingTheIsOptionalWhenTheLookupIsUnambiguous()
        {
            Type catalogType = FindType("ArcaneArena.Cards.CardRarityCatalog");
            Type rarityType = FindType("ArcaneArena.Cards.CardRarity");
            MethodInfo resolve = catalogType.GetMethod(
                "TryResolve",
                new[] { typeof(string), rarityType.MakeByRefType() });
            Assert.That(resolve, Is.Not.Null);
            object[] arguments = { "Winged Dragon of Ra", null };
            bool resolved = (bool)resolve.Invoke(null, arguments);
            Assert.That(resolved, Is.True);
            Assert.That(arguments[1].ToString(), Is.EqualTo("UR"));
        }

        [Test]
        public void C01_GenerateAndDismantleUseOnlyMatchingRarityWallet()
        {
            string path = TemporarySave("atomic");
            try
            {
                object repository = CreateRepository(path);
                SetCraftBalance(repository, "UR", 60);
                object[] generate =
                    { "89631139", 1, "craft-generate-1", null, null };
                bool generated = (bool)repository.GetType()
                    .GetMethod("TryGenerateCard")
                    .Invoke(repository, generate);
                Assert.That(generated, Is.True, generate[4] as string);
                Assert.That(CraftBalance(repository, "UR"), Is.EqualTo(30));
                Assert.That(CraftBalance(repository, "N"), Is.Zero);

                object[] repeated =
                    { "89631139", 1, "craft-generate-1", null, null };
                bool idempotent = (bool)repository.GetType()
                    .GetMethod("TryGenerateCard")
                    .Invoke(repository, repeated);
                Assert.That(idempotent, Is.True, repeated[4] as string);
                Assert.That(CraftBalance(repository, "UR"), Is.EqualTo(30));

                Type finishType = FindType("ArcaneArena.Cards.CardFinish");
                object[] dismantle =
                {
                    "89631139", 1, Enum.Parse(finishType, "Normal"),
                    "craft-dismantle-1", true, null, null
                };
                bool dismantled = (bool)repository.GetType()
                    .GetMethod("TryDismantleCard")
                    .Invoke(repository, dismantle);
                Assert.That(dismantled, Is.True, dismantle[6] as string);
                Assert.That(CraftBalance(repository, "UR"), Is.EqualTo(40));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void C02_StructureDeckCopiesAreProtectedFromDismantling()
        {
            string path = TemporarySave("structure-protection");
            try
            {
                object repository = CreateRepository(path);
                Type shopType = FindType("ArcaneArena.Frontend.DeckShopCatalog");
                object product = Values(shopType.GetProperty("Products").GetValue(null))[0];
                int price = (int)Property(product, "PriceCoins");
                SetCoinBalance(repository, price);
                object[] purchase =
                {
                    Property(product, "ProductId"),
                    "structure-protection-1",
                    null,
                    null
                };
                bool purchased = (bool)repository.GetType()
                    .GetMethod("TryPurchaseStructureDeck")
                    .Invoke(repository, purchase);
                Assert.That(purchased, Is.True, purchase[3] as string);

                string protectedCard = Values(Property(product, "MainDeckCardIds"))
                    .Select(item => item.ToString())
                    .First(cardId => Property(FindCard(Catalog(), cardId), "Rarity")
                        .ToString() != "Unknown");
                int protectedQuantity = (int)repository.GetType()
                    .GetMethod("ProtectedCardQuantity")
                    .Invoke(repository, new object[] { protectedCard });
                int eligible = (int)repository.GetType()
                    .GetMethod("DismantlableCardQuantity")
                    .Invoke(repository, new object[] { protectedCard });
                Assert.That(protectedQuantity, Is.GreaterThan(0));
                Assert.That(eligible, Is.Zero);

                Type finishType = FindType("ArcaneArena.Cards.CardFinish");
                object[] dismantle =
                {
                    protectedCard, 1, Enum.Parse(finishType, "Normal"),
                    "structure-dismantle-blocked", true, null, null
                };
                bool dismantled = (bool)repository.GetType()
                    .GetMethod("TryDismantleCard")
                    .Invoke(repository, dismantle);
                Assert.That(dismantled, Is.False);
                Assert.That(dismantle[6] as string, Does.Contain("Estrutural"));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        private static object CreateRepository(string path)
        {
            Type type = FindType("ArcaneArena.Frontend.DeckRepository");
            object repository = Activator.CreateInstance(type, path);
            type.GetMethod("Load").Invoke(
                repository,
                new[] { Catalog(), (object)false });
            return repository;
        }

        private static UnityEngine.Object Catalog()
        {
            UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/Cards/CardCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }

        private static object FindCard(UnityEngine.Object catalog, string cardId)
        {
            object entry = catalog.GetType()
                .GetMethod("FindByOfficialId")
                .Invoke(catalog, new object[] { cardId });
            if (entry == null && long.TryParse(cardId, out long numeric))
            {
                entry = catalog.GetType()
                    .GetMethod("FindByOfficialId")
                    .Invoke(catalog, new object[] { numeric.ToString("D8") });
            }
            Assert.That(entry, Is.Not.Null, "Carta ausente: " + cardId);
            return entry;
        }

        private static void SetCraftBalance(
            object repository,
            string rarity,
            int value)
        {
            object state = repository.GetType().GetProperty("State")
                .GetValue(repository);
            object wallet = Field(state, "craftPoints");
            wallet.GetType().GetField("cp" + rarity).SetValue(wallet, value);
        }

        private static int CraftBalance(object repository, string rarity)
        {
            Type rarityType = FindType("ArcaneArena.Cards.CardRarity");
            return (int)repository.GetType().GetMethod("CraftPointBalance")
                .Invoke(repository, new[] { Enum.Parse(rarityType, rarity) });
        }

        private static void SetCoinBalance(object repository, int value)
        {
            object state = repository.GetType().GetProperty("State")
                .GetValue(repository);
            state.GetType().GetField("coinBalance").SetValue(state, value);
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Tipo runtime ausente: " + fullName);
            return type;
        }

        private static object Property(object source, string name)
        {
            return source.GetType().GetProperty(name).GetValue(source);
        }

        private static object Field(object source, string name)
        {
            return source.GetType().GetField(name).GetValue(source);
        }

        private static object[] Values(object source)
        {
            return ((IEnumerable)source).Cast<object>().ToArray();
        }

        private static string TemporarySave(string suffix)
        {
            return Path.Combine(
                Path.GetFullPath(Path.Combine("Temp", "ArcaneRarityTests")),
                "rarity-" + suffix + "-" + Guid.NewGuid().ToString("N") + ".json");
        }

        private static void DeleteSave(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;
            foreach (string candidate in Directory.GetFiles(
                         directory,
                         Path.GetFileName(path) + "*"))
            {
                File.Delete(candidate);
            }
        }

        [Serializable]
        private sealed class RarityHeader
        {
            public int schemaVersion;
            public int entryCount;
            public int normalCount;
            public int rareCount;
            public int superRareCount;
            public int ultraRareCount;
        }
    }
}
