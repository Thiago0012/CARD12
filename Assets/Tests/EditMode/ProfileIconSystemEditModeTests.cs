using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ProfileIconSystemEditModeTests
    {
        [Test]
        public void CatalogHasDefaultAndNineIconsAtExactPrice()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            object[] purchasable = icons.Where(icon =>
                (bool)Property(icon, "IsPurchasable")).ToArray();

            Assert.That(icons, Has.Length.EqualTo(10));
            Assert.That(purchasable, Has.Length.EqualTo(9));
            Assert.That(purchasable.All(icon =>
                (int)Property(icon, "PriceCoins") == 35), Is.True);
            Assert.That(icons.Count(icon =>
                !(bool)Property(icon, "IsPurchasable")), Is.EqualTo(1));
            Assert.That(icons.All(icon =>
                Property(icon, "AssetMode").ToString() == "PreframedHex"),
                Is.True,
                "Os 10 ícones atuais já incluem sua própria borda hexagonal.");
        }

        [Test]
        public void EveryPreframedIconUsesTheSameBoundedVerticalScale()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            Type viewType = FindType("ArcaneArena.Frontend.HexIconView");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            var root = new GameObject("Profile Icon Scale Test",
                typeof(RectTransform));
            try
            {
                Component view = root.AddComponent(viewType);
                MethodInfo setIcon = viewType.GetMethod("SetIcon");
                foreach (object icon in icons)
                {
                    setIcon.Invoke(
                        view,
                        new[] { Property(icon, "IconId") });
                    Transform portrait = root.transform.Find("Retrato");
                    Assert.That(portrait, Is.Not.Null);
                    Assert.That(portrait.localScale.x,
                        Is.EqualTo(1f).Within(0.001f));
                    Assert.That(portrait.localScale.y,
                        Is.EqualTo(0.86f).Within(0.001f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EveryPurchasableIconLoadsAsAProjectResource()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            foreach (object icon in icons.Where(candidate =>
                         (bool)Property(candidate, "IsPurchasable")))
            {
                string path = Property(icon, "ResourcePath") as string;
                Assert.That(Resources.Load<Texture2D>(path), Is.Not.Null,
                    "Recurso ausente: " + path);
            }
        }

        [Test]
        public void PurchaseIsAtomicIdempotentAndEquipPersists()
        {
            string path = TemporarySave("purchase");
            try
            {
                object repository = CreateRepository(path);
                SetCoinBalance(repository, 75);
                string iconId = PurchasableIds()[0];

                object[] first = { iconId, "icon-tx-1", null, null };
                bool purchased = (bool)repository.GetType()
                    .GetMethod("TryPurchaseIcon").Invoke(repository, first);
                Assert.That(purchased, Is.True, first[3] as string);
                Assert.That(CoinBalance(repository), Is.EqualTo(40));

                object[] repeated = { iconId, "icon-tx-1", null, null };
                bool replayed = (bool)repository.GetType()
                    .GetMethod("TryPurchaseIcon").Invoke(repository, repeated);
                Assert.That(replayed, Is.True, repeated[3] as string);
                Assert.That(CoinBalance(repository), Is.EqualTo(40));

                object[] equip = { iconId, null };
                bool equipped = (bool)repository.GetType()
                    .GetMethod("TryEquipIcon").Invoke(repository, equip);
                Assert.That(equipped, Is.True, equip[1] as string);

                object reloaded = CreateRepository(path);
                Assert.That(Property(reloaded, "EquippedIconId"),
                    Is.EqualTo(iconId));
                Assert.That(CoinBalance(reloaded), Is.EqualTo(40));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void DuelIdentityIsFrozenAfterMatchStarts()
        {
            string path = TemporarySave("identity");
            try
            {
                object repository = CreateRepository(path);
                SetCoinBalance(repository, 100);
                string[] ids = PurchasableIds().Take(2).ToArray();
                PurchaseAndEquip(repository, ids[0], "identity-tx-1");
                object snapshot = repository.GetType()
                    .GetMethod("CaptureDuelIdentitySnapshot")
                    .Invoke(repository, null);

                PurchaseAndEquip(repository, ids[1], "identity-tx-2");
                Assert.That(Field(snapshot, "equippedIconId"),
                    Is.EqualTo(ids[0]),
                    "A identidade apresentada no duelo deve ser imutável.");
                Assert.That(Property(repository, "EquippedIconId"),
                    Is.EqualTo(ids[1]));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void ConfirmedStatisticsAreScopedAndIdempotent()
        {
            string path = TemporarySave("statistics");
            try
            {
                object repository = CreateRepository(path);
                Type eventType = FindType(
                    "ArcaneArena.Frontend.DuelStatisticEventType");
                object specialSummon = Enum.Parse(eventType, "SpecialSummon");
                MethodInfo recordEvent = repository.GetType().GetMethod(
                    "TryRecordAuthoritativeStatisticEvent");
                object[] first =
                    { "match-1:event-1", specialSummon, 1L, true, true, null };
                Assert.That((bool)recordEvent.Invoke(repository, first), Is.True,
                    first[5] as string);
                object[] replay =
                    { "match-1:event-1", specialSummon, 1L, true, true, null };
                Assert.That((bool)recordEvent.Invoke(repository, replay), Is.True,
                    replay[5] as string);

                MethodInfo recordResult = repository.GetType().GetMethod(
                    "TryRecordAuthoritativeDuelResult");
                object[] result =
                    { "match-1", true, false, true, true, 2500L, 900L, null };
                Assert.That((bool)recordResult.Invoke(repository, result), Is.True,
                    result[7] as string);
                object[] resultReplay =
                    { "match-1", true, false, true, true, 2500L, 900L, null };
                Assert.That((bool)recordResult.Invoke(repository, resultReplay),
                    Is.True, resultReplay[7] as string);

                object statistics = Property(repository, "Statistics");
                foreach (string scopeName in new[] { "overall", "online", "ranked" })
                {
                    object scope = Field(statistics, scopeName);
                    Assert.That(Field(scope, "duelsPlayed"), Is.EqualTo(1L));
                    Assert.That(Field(scope, "wins"), Is.EqualTo(1L));
                    Assert.That(Field(scope, "damageDealt"), Is.EqualTo(2500L));
                    Assert.That(Field(scope, "damageReceived"), Is.EqualTo(900L));
                    Assert.That(
                        Field(scope, "maxDamageDealtInSingleDuel"),
                        Is.EqualTo(2500L));
                    Assert.That(
                        Field(scope, "maxDamageReceivedInSingleDuel"),
                        Is.EqualTo(900L));
                    Assert.That(Field(scope, "specialSummons"), Is.EqualTo(1L));
                    Assert.That(Field(scope, "monstersSummoned"), Is.EqualTo(1L));
                }
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void LegacyStatisticsMigrateWithoutResettingExistingCounters()
        {
            string path = TemporarySave("statistics-migration");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    "{\"schemaVersion\":9,\"statistics\":{" +
                    "\"overall\":{\"duelsPlayed\":7," +
                    "\"wins\":4,\"damageDealt\":12345}," +
                    "\"online\":{},\"ranked\":{}}}");

                object repository = CreateRepository(path);
                object state = Property(repository, "State");
                object statistics = Property(repository, "Statistics");
                object overall = Field(statistics, "overall");

                Assert.That(Field(state, "schemaVersion"), Is.EqualTo(10));
                Assert.That(Field(overall, "duelsPlayed"), Is.EqualTo(7L));
                Assert.That(Field(overall, "wins"), Is.EqualTo(4L));
                Assert.That(Field(overall, "damageDealt"), Is.EqualTo(12345L));
                Assert.That(Field(overall, "damageReceived"), Is.EqualTo(0L));
                Assert.That(
                    Field(overall, "maxDamageDealtInSingleDuel"),
                    Is.EqualTo(0L));
                Assert.That(
                    Field(overall, "maxDamageReceivedInSingleDuel"),
                    Is.EqualTo(0L));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void DuelProfileNormalizationIsSafeForEmptyProfiles()
        {
            Type config = FindType(
                "ArcaneArena.Frontend.DuelStatsVisualizationConfig");
            MethodInfo normalize = config.GetMethod(
                "Normalize",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo resolve = config.GetMethod(
                "Resolve",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(normalize, Is.Not.Null);
            Assert.That(resolve, Is.Not.Null);
            object configuredCaps = resolve.Invoke(null, null);
            Assert.That(Field(configuredCaps, "damagePerDuelCap"),
                Is.EqualTo(8000f));
            Assert.That(
                (float)normalize.Invoke(null, new object[] { 100f, 0f }),
                Is.EqualTo(1f));
            Assert.That(
                (float)normalize.Invoke(null, new object[] { 0f, 0f }),
                Is.Zero);
            Assert.That(
                (float)normalize.Invoke(null, new object[] { -10f, 100f }),
                Is.Zero);
        }

        [Test]
        public void HudSafeAreaUsesIntersectionWithArenaViewport()
        {
            Type fitter = FindType("ArcaneArena.Frontend.DuelHudSafeAreaFitter");
            MethodInfo intersect = fitter.GetMethod(
                "Intersect", BindingFlags.Public | BindingFlags.Static);
            Rect actual = (Rect)intersect.Invoke(null, new object[]
            {
                new Rect(30f, 20f, 1900f, 1040f),
                new Rect(100f, 0f, 1720f, 1080f)
            });
            Assert.That(actual, Is.EqualTo(new Rect(100f, 20f, 1720f, 1040f)));
        }

        private static void PurchaseAndEquip(
            object repository,
            string iconId,
            string transactionId)
        {
            object[] purchase = { iconId, transactionId, null, null };
            Assert.That((bool)repository.GetType().GetMethod("TryPurchaseIcon")
                .Invoke(repository, purchase), Is.True, purchase[3] as string);
            object[] equip = { iconId, null };
            Assert.That((bool)repository.GetType().GetMethod("TryEquipIcon")
                .Invoke(repository, equip), Is.True, equip[1] as string);
        }

        private static object CreateRepository(string path)
        {
            Type type = FindType("ArcaneArena.Frontend.DeckRepository");
            object repository = Activator.CreateInstance(type, path);
            type.GetMethod("Load").Invoke(repository, new object[] { null, false });
            return repository;
        }

        private static string[] PurchasableIds()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            return Values(catalog.GetProperty("All").GetValue(null))
                .Where(icon => (bool)Property(icon, "IsPurchasable"))
                .Select(icon => Property(icon, "IconId") as string)
                .ToArray();
        }

        private static void SetCoinBalance(object repository, int value)
        {
            object state = Property(repository, "State");
            state.GetType().GetField("coinBalance").SetValue(state, value);
        }

        private static int CoinBalance(object repository) =>
            (int)Property(repository, "CoinBalance");

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Tipo runtime ausente: " + fullName);
            return type;
        }

        private static object Property(object source, string name) =>
            source.GetType().GetProperty(name).GetValue(source);

        private static object Field(object source, string name) =>
            source.GetType().GetField(name).GetValue(source);

        private static object[] Values(object source) =>
            ((IEnumerable)source).Cast<object>().ToArray();

        private static string TemporarySave(string suffix) => Path.Combine(
            Path.GetFullPath(Path.Combine("Temp", "ArcaneProfileIconTests")),
            "profile-" + suffix + "-" + Guid.NewGuid().ToString("N") + ".json");

        private static void DeleteSave(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;
            foreach (string candidate in Directory.GetFiles(
                         directory, Path.GetFileName(path) + "*"))
            {
                File.Delete(candidate);
            }
        }
    }
}
