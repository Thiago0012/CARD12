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
    public sealed class CoinRewardAuthorizationEditModeTests
    {
        private const string CatalogPath =
            "Assets/Resources/Shop/AuthorizedCoinRecipientsCatalog.asset";

        [Test]
        public void NicknameNormalizationMatchesSpecification()
        {
            Type normalizer = FindType(
                "ArcaneArena.Frontend.NicknameNormalizer");
            MethodInfo normalize = normalizer.GetMethod(
                "Normalize",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(normalize.Invoke(null, new object[] { null }),
                Is.EqualTo(string.Empty));
            Assert.That(normalize.Invoke(
                    null,
                    new object[] { "  Lucas\t\n Gay  " }),
                Is.EqualTo("LUCAS GAY"));
            Assert.That(normalize.Invoke(null, new object[] { "José" }),
                Is.EqualTo("JOSÉ"));
            Assert.That(normalize.Invoke(null, new object[] { "Jose" }),
                Is.Not.EqualTo("JOSÉ"));
        }

        [Test]
        public void CatalogContainsTheFiveRequestedActiveNicknames()
        {
            UnityEngine.Object catalog = LoadCatalog();
            object[] entries = Values(Property(catalog, "Entries"));
            string[] nicknames = entries
                .Select(entry => Property(entry, "Nickname").ToString())
                .ToArray();
            Assert.That(nicknames, Is.EquivalentTo(new[]
            {
                "Nyarlathotep",
                "KimDelas",
                "xinelonadepobre",
                "Bigcocao",
                "Lukatores"
            }));
            Assert.That(entries.All(entry =>
                    Property(entry, "Status").ToString() == "Active"),
                Is.True);
            Assert.That(entries.Select(entry =>
                    Property(entry, "EntryId").ToString())
                .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(5));
        }

        [Test]
        public void BindingSurvivesNicknameChangeAndReload()
        {
            string path = TemporarySave("rename");
            try
            {
                object repository = CreateRepository(path);
                SetPlayerName(repository, "Nyarlathotep");
                Configure(repository, LoadCatalog(), path + ".install-a");
                object first = Capture(repository);
                AssertAuthorized(first);
                string entryId = Field(first, "catalogEntryId").ToString();

                SetPlayerName(repository, "Nome Alterado");
                object afterRename = Capture(repository);
                AssertAuthorized(afterRename);
                Assert.That(Field(afterRename, "catalogEntryId"),
                    Is.EqualTo(entryId));

                object reloaded = CreateRepository(path);
                Configure(reloaded, LoadCatalog(), path + ".install-a");
                object afterReload = Capture(reloaded);
                AssertAuthorized(afterReload);
                Assert.That(Field(afterReload, "catalogEntryId"),
                    Is.EqualTo(entryId));
            }
            finally
            {
                DeleteTemporaryFiles(path);
            }
        }

        [Test]
        public void BindingCopiedToAnotherInstallIsBlocked()
        {
            string path = TemporarySave("install-mismatch");
            try
            {
                object repository = CreateRepository(path);
                SetPlayerName(repository, "KimDelas");
                Configure(repository, LoadCatalog(), path + ".install-a");
                AssertAuthorized(Capture(repository));

                object copied = CreateRepository(path);
                Configure(copied, LoadCatalog(), path + ".install-b");
                object blocked = Capture(copied);
                Assert.That(Field(blocked, "wasAuthorizedAtMatchStart"),
                    Is.False);
                Assert.That(Field(blocked, "blockedStatusAtMatchStart").ToString(),
                    Is.EqualTo("BlockedInstallationMismatch"));
            }
            finally
            {
                DeleteTemporaryFiles(path);
            }
        }

        [Test]
        public void DisabledPreservesOldBindingAndRevokedBlocksIt()
        {
            string path = TemporarySave("administrative-status");
            UnityEngine.Object catalog = UnityEngine.Object.Instantiate(
                LoadCatalog());
            try
            {
                object repository = CreateRepository(path);
                SetPlayerName(repository, "Lukatores");
                Configure(repository, catalog, path + ".install-a");
                AssertAuthorized(Capture(repository));

                SetEntryStatus(catalog, "Lukatores",
                    "DisabledForNewBindings");
                AssertAuthorized(Capture(repository));

                SetEntryStatus(catalog, "Lukatores", "Revoked");
                object revoked = Capture(repository);
                Assert.That(Field(revoked, "wasAuthorizedAtMatchStart"),
                    Is.False);
                Assert.That(Field(revoked, "blockedStatusAtMatchStart").ToString(),
                    Is.EqualTo("BlockedRevoked"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                DeleteTemporaryFiles(path);
            }
        }

        [Test]
        public void DisabledEntryCannotCreateANewBinding()
        {
            string path = TemporarySave("disabled-new");
            UnityEngine.Object catalog = UnityEngine.Object.Instantiate(
                LoadCatalog());
            try
            {
                SetEntryStatus(catalog, "Bigcocao",
                    "DisabledForNewBindings");
                object repository = CreateRepository(path);
                SetPlayerName(repository, "Bigcocao");
                Configure(repository, catalog, path + ".install-a");
                object blocked = Capture(repository);
                Assert.That(Field(blocked, "wasAuthorizedAtMatchStart"),
                    Is.False);
                Assert.That(Field(blocked, "blockedStatusAtMatchStart").ToString(),
                    Is.EqualTo("BlockedNotAuthorized"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                DeleteTemporaryFiles(path);
            }
        }

        [Test]
        public void RevocationAfterMatchStartBlocksTheFinalClaim()
        {
            string path = TemporarySave("revoked-at-finish");
            UnityEngine.Object catalog = UnityEngine.Object.Instantiate(
                LoadCatalog());
            try
            {
                object repository = CreateRepository(path);
                SetPlayerName(repository, "KimDelas");
                Configure(repository, catalog, path + ".install-a");
                object startSnapshot = Capture(repository);
                AssertAuthorized(startSnapshot);
                SetEntryStatus(catalog, "KimDelas", "Revoked");

                Type requestType = FindType(
                    "ArcaneArena.Frontend.MatchRewardRequest");
                object request = Activator.CreateInstance(requestType);
                SetField(request, "matchId", "match-revoked-finish");
                SetField(request, "localPlayerId", "seat0");
                object state = repository.GetType().GetProperty("State")
                    .GetValue(repository);
                SetField(request, "localProfileId",
                    Field(state, "localProfileId"));
                Type modeType = FindType(
                    "ArcaneArena.Frontend.MatchRewardMode");
                SetField(request, "mode", Enum.Parse(modeType, "OnlinePvP"));
                SetField(request, "isAuthoritativeFinal", true);
                SetField(request, "isWinner", true);
                SetField(request, "totalOpponentDamage", 8000);
                SetField(request, "completedRounds", 6);
                SetField(request, "eligibilityAtMatchStart", startSnapshot);
                object[] arguments = { request, null, null };
                bool claimed = (bool)repository.GetType()
                    .GetMethod("TryClaimOnlineDuelReward")
                    .Invoke(repository, arguments);
                Assert.That(claimed, Is.True, arguments[2] as string);
                Assert.That(Field(arguments[1], "status").ToString(),
                    Is.EqualTo("BlockedRevoked"));
                Assert.That(repository.GetType().GetProperty("CoinBalance")
                    .GetValue(repository), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                DeleteTemporaryFiles(path);
            }
        }

        [Test]
        public void SchemaFourMigrationPreservesEconomyAndStartsUnbound()
        {
            string path = TemporarySave("schema-four");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    "{\"schemaVersion\":4," +
                    "\"localProfileId\":\"legacy-profile\"," +
                    "\"playerDisplayName\":\"Nyarlathotep\"," +
                    "\"coinBalance\":77," +
                    "\"cardQuantities\":[{\"cardId\":\"46986414\",\"quantity\":2}]," +
                    "\"structureDeckPurchases\":[]," +
                    "\"pendingPackOpenings\":[]," +
                    "\"processedShopTransactions\":[]," +
                    "\"decks\":[],\"unlockedDeckProductIds\":[]}");
                object repository = CreateRepository(path);
                object state = repository.GetType().GetProperty("State")
                    .GetValue(repository);
                Assert.That(Field(state, "schemaVersion"), Is.EqualTo(5));
                Assert.That(Field(state, "coinBalance"), Is.EqualTo(77));
                object[] quantities = Values(Field(state, "cardQuantities"));
                Assert.That(quantities, Has.Length.EqualTo(1));
                Assert.That(Field(quantities[0], "quantity"), Is.EqualTo(2));
                object authorization = Field(
                    state,
                    "coinRewardAuthorization");
                Assert.That(authorization, Is.Not.Null);
                Assert.That(Field(authorization, "isAuthorized"), Is.False);
            }
            finally
            {
                DeleteTemporaryFiles(path);
            }
        }

        [Test]
        public void EconomyBuildValidatorAcceptsCatalogAndSceneReferences()
        {
            Type validator = FindType(
                "ArcaneArena.Editor.ShopCatalogValidator");
            string[] problems = (string[])validator.GetMethod(
                    "FindProblems",
                    BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            MethodInfo productionValidation = validator.GetMethod(
                "FindProblems",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(bool) },
                null);
            Assert.That(productionValidation, Is.Not.Null);
            string[] productionProblems = (string[])productionValidation.Invoke(
                null,
                new object[] { true });
            Assert.That(
                productionProblems,
                Is.Empty,
                string.Join("\n", productionProblems));
        }

        private static object CreateRepository(string path)
        {
            Type repositoryType = FindType(
                "ArcaneArena.Frontend.DeckRepository");
            object repository = Activator.CreateInstance(repositoryType, path);
            repositoryType.GetMethod("Load").Invoke(
                repository,
                new object[] { null, false });
            return repository;
        }

        private static void Configure(
            object repository,
            UnityEngine.Object catalog,
            string identityPath)
        {
            Type installType = FindType(
                "ArcaneArena.Frontend.LocalInstallIdentityService");
            object install = Activator.CreateInstance(
                installType,
                identityPath);
            repository.GetType()
                .GetMethod("ConfigureCoinRewardAuthorization")
                .Invoke(repository, new[] { catalog, install });
        }

        private static object Capture(object repository)
        {
            return repository.GetType()
                .GetMethod("CaptureOnlineRewardEligibility")
                .Invoke(repository, null);
        }

        private static void AssertAuthorized(object snapshot)
        {
            Assert.That(Field(snapshot, "wasAuthorizedAtMatchStart"),
                Is.True);
            Assert.That(Field(snapshot, "catalogEntryId").ToString(),
                Is.Not.Empty);
        }

        private static void SetPlayerName(object repository, string nickname)
        {
            object[] arguments = { nickname, null };
            bool result = (bool)repository.GetType()
                .GetMethod("TrySetPlayerDisplayName")
                .Invoke(repository, arguments);
            Assert.That(result, Is.True, arguments[1] as string);
        }

        private static void SetEntryStatus(
            UnityEngine.Object catalog,
            string nickname,
            string status)
        {
            object entry = Values(Property(catalog, "Entries"))
                .Single(candidate => string.Equals(
                    Property(candidate, "Nickname").ToString(),
                    nickname,
                    StringComparison.Ordinal));
            FieldInfo statusField = entry.GetType().GetField(
                "status",
                BindingFlags.Instance | BindingFlags.NonPublic);
            statusField.SetValue(
                entry,
                Enum.Parse(statusField.FieldType, status));
        }

        private static UnityEngine.Object LoadCatalog()
        {
            UnityEngine.Object catalog =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog;
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

        private static void SetField(object source, string name, object value)
        {
            source.GetType().GetField(name).SetValue(source, value);
        }

        private static object[] Values(object source)
        {
            return ((IEnumerable)source).Cast<object>().ToArray();
        }

        private static string TemporarySave(string suffix)
        {
            return Path.Combine(
                Path.GetFullPath(Path.Combine(
                    "Temp",
                    "ArcaneAuthorizationTests")),
                suffix + "-" + Guid.NewGuid().ToString("N") + ".json");
        }

        private static void DeleteTemporaryFiles(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                return;
            Directory.CreateDirectory(directory);
            foreach (string candidate in Directory.GetFiles(
                         directory,
                         Path.GetFileName(path) + "*"))
            {
                File.Delete(candidate);
            }
        }
    }
}
