using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ShopEconomyEditModeTests
    {
        [TestCase(8000, 6, true, false, 53)]
        [TestCase(8000, 7, true, false, 49)]
        [TestCase(8000, 8, true, false, 45)]
        [TestCase(8000, 9, true, false, 41)]
        [TestCase(300, 9, true, false, 0)]
        [TestCase(8000, 4, false, false, 26)]
        [TestCase(4000, 12, false, false, 13)]
        [TestCase(149, 2, true, false, 0)]
        [TestCase(8000, 2, true, true, 0)]
        public void OnlineRewardMatchesSpecification(
            int damage,
            int rounds,
            bool winner,
            bool draw,
            int expected)
        {
            Type reward = FindType("ArcaneArena.Frontend.OnlineDuelCoinReward");
            MethodInfo calculate = reward.GetMethod(
                "Calculate", BindingFlags.Public | BindingFlags.Static);
            int actual = (int)calculate.Invoke(
                null, new object[] { damage, rounds, winner, draw });
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void VersionedPackCatalogCoversEveryCollectibleExactlyOnce()
        {
            Type packCatalog = FindType("ArcaneArena.Frontend.ShopPackCatalog");
            Type deckCatalog = FindType("ArcaneArena.Frontend.DeckShopCatalog");
            object[] packs = Values(packCatalog.GetProperty("Packs").GetValue(null));
            string[] collectible = Values(
                    deckCatalog.GetProperty("CollectibleCardIds").GetValue(null))
                .Select(value => value.ToString()).ToArray();
            string[] distributed = packs
                .SelectMany(pack => Values(Property(pack, "CardIds")))
                .Select(value => value.ToString())
                .ToArray();

            Assert.That(packs, Has.Length.EqualTo(19));
            Assert.That(packs.All(pack =>
                Values(Property(pack, "CardIds")).Length is >= 1 and <= 38),
                Is.True);
            Assert.That(distributed, Has.Length.EqualTo(collectible.Length));
            Assert.That(distributed.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(distributed.Length));
            Assert.That(distributed, Is.EquivalentTo(collectible));
        }

        [Test]
        public void PackPurchaseIsFiveCardsAtomicAndIdempotent()
        {
            string path = TemporarySave("pack");
            try
            {
                UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/Cards/CardCatalog.asset");
                Assert.That(catalog, Is.Not.Null);
                object repository = CreateRepository(path, catalog);
                SetCoinBalance(repository, 70);
                string packId = FirstPackId();
                object[] first = { packId, "tx-pack-1", null, null, null };
                bool purchased = (bool)repository.GetType()
                    .GetMethod("TryPurchasePack")
                    .Invoke(repository, first);
                Assert.That(purchased, Is.True, first[4] as string);
                Assert.That(CoinBalance(repository), Is.EqualTo(35));
                Assert.That(Values(Field(first[2], "cardIds")), Has.Length.EqualTo(5));

                object[] repeated = { packId, "tx-pack-1", null, null, null };
                bool replayed = (bool)repository.GetType()
                    .GetMethod("TryPurchasePack")
                    .Invoke(repository, repeated);
                Assert.That(replayed, Is.True, repeated[4] as string);
                Assert.That(CoinBalance(repository), Is.EqualTo(35),
                    "A repetição do mesmo request não pode cobrar novamente.");
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void OnlineRewardRequestCannotCreditTwice()
        {
            string path = TemporarySave("reward");
            try
            {
                object repository = CreateRepository(path, null);
                MethodInfo grant = repository.GetType()
                    .GetMethod("TryGrantOnlineDuelReward");
                object[] first =
                    { "match-1:seat0", 8000, 6, true, false, 0, null };
                object[] repeated =
                    { "match-1:seat0", 8000, 6, true, false, 0, null };
                Assert.That((bool)grant.Invoke(repository, first), Is.True);
                Assert.That((bool)grant.Invoke(repository, repeated), Is.True);
                Assert.That((int)first[5], Is.EqualTo(53));
                Assert.That((int)repeated[5], Is.EqualTo(53));
                Assert.That(CoinBalance(repository), Is.EqualTo(53));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        private static object CreateRepository(string path, object catalog)
        {
            Type type = FindType("ArcaneArena.Frontend.DeckRepository");
            object repository = Activator.CreateInstance(type, path);
            MethodInfo load = type.GetMethod("Load");
            load.Invoke(repository, new[] { catalog, (object)false });
            return repository;
        }

        private static void SetCoinBalance(object repository, int value)
        {
            object state = repository.GetType().GetProperty("State").GetValue(repository);
            state.GetType().GetField("coinBalance").SetValue(state, value);
        }

        private static int CoinBalance(object repository)
        {
            return (int)repository.GetType().GetProperty("CoinBalance")
                .GetValue(repository);
        }

        private static string FirstPackId()
        {
            Type type = FindType("ArcaneArena.Frontend.ShopPackCatalog");
            object pack = Values(type.GetProperty("Packs").GetValue(null))[0];
            return Property(pack, "PackId") as string;
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
                Application.temporaryCachePath,
                "arcane-shop-" + suffix + "-" + Guid.NewGuid().ToString("N") + ".json");
        }

        private static void DeleteSave(string path)
        {
            foreach (string candidate in new[]
                     { path, path + ".tmp", path + ".bak" })
            {
                if (File.Exists(candidate))
                    File.Delete(candidate);
            }
        }
    }
}
