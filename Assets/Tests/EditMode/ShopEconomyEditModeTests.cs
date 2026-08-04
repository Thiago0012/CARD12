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

            Assert.That(packs, Has.Length.GreaterThanOrEqualTo(19));
            Assert.That(packs.All(pack =>
                Values(Property(pack, "CardIds")).Length is >= 1 and <= 38),
                Is.True);
            Assert.That(distributed.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(distributed.Length));
            Assert.That(collectible.Except(distributed, StringComparer.Ordinal),
                Is.Empty,
                "Todo card colecionavel dos Decks Estruturais deve continuar coberto.");
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
                ConfigureAuthorization(
                    repository,
                    path,
                    "Nyarlathotep",
                    "primary");
                object eligibility = repository.GetType()
                    .GetMethod("CaptureOnlineRewardEligibility")
                    .Invoke(repository, null);
                object request = CreateRewardRequest(
                    repository,
                    "match-1",
                    "seat0",
                    eligibility,
                    8000,
                    6,
                    true,
                    false);
                object first = ClaimReward(repository, request);
                object repeated = ClaimReward(repository, request);
                Assert.That((int)Field(first, "coins"), Is.EqualTo(53));
                Assert.That((int)Field(repeated, "coins"), Is.EqualTo(53));
                Assert.That(Field(repeated, "status").ToString(),
                    Is.EqualTo("AlreadyProcessed"));
                Assert.That(CoinBalance(repository), Is.EqualTo(53));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void BlockedMatchIsConsumedBeforeFutureAuthorization()
        {
            string path = TemporarySave("blocked-reward");
            try
            {
                object repository = CreateRepository(path, null);
                ConfigureAuthorization(
                    repository,
                    path,
                    "Visitante",
                    "blocked");
                object blockedSnapshot = repository.GetType()
                    .GetMethod("CaptureOnlineRewardEligibility")
                    .Invoke(repository, null);
                object blockedRequest = CreateRewardRequest(
                    repository,
                    "match-blocked",
                    "seat0",
                    blockedSnapshot,
                    8000,
                    6,
                    true,
                    false);
                object blocked = ClaimReward(repository, blockedRequest);
                Assert.That(Field(blocked, "status").ToString(),
                    Is.EqualTo("BlockedNotAuthorized"));
                Assert.That(CoinBalance(repository), Is.Zero);

                SetPlayerName(repository, "Nyarlathotep");
                object authorizedSnapshot = repository.GetType()
                    .GetMethod("CaptureOnlineRewardEligibility")
                    .Invoke(repository, null);
                object repeatedRequest = CreateRewardRequest(
                    repository,
                    "match-blocked",
                    "seat0",
                    authorizedSnapshot,
                    8000,
                    6,
                    true,
                    false);
                object repeated = ClaimReward(repository, repeatedRequest);
                Assert.That(Field(repeated, "status").ToString(),
                    Is.EqualTo("AlreadyProcessed"));
                Assert.That(Field(repeated, "originalStatus").ToString(),
                    Is.EqualTo("BlockedNotAuthorized"));
                Assert.That(CoinBalance(repository), Is.Zero);
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void NicknameChangedDuringMatchUsesTheStartSnapshot()
        {
            string path = TemporarySave("start-snapshot");
            try
            {
                object repository = CreateRepository(path, null);
                ConfigureAuthorization(
                    repository,
                    path,
                    "Visitante",
                    "snapshot");
                object startSnapshot = repository.GetType()
                    .GetMethod("CaptureOnlineRewardEligibility")
                    .Invoke(repository, null);
                SetPlayerName(repository, "Nyarlathotep");
                object request = CreateRewardRequest(
                    repository,
                    "match-start-snapshot",
                    "seat0",
                    startSnapshot,
                    8000,
                    6,
                    true,
                    false);
                object receipt = ClaimReward(repository, request);
                Assert.That(Field(receipt, "status").ToString(),
                    Is.EqualTo("BlockedNotAuthorized"));
                Assert.That(CoinBalance(repository), Is.Zero);
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void OfflineMatchNeverCreditsAnAuthorizedProfile()
        {
            string path = TemporarySave("offline-reward");
            try
            {
                object repository = CreateRepository(path, null);
                ConfigureAuthorization(
                    repository,
                    path,
                    "KimDelas",
                    "offline");
                object snapshot = repository.GetType()
                    .GetMethod("CaptureOnlineRewardEligibility")
                    .Invoke(repository, null);
                object request = CreateRewardRequest(
                    repository,
                    "match-offline",
                    "seat0",
                    snapshot,
                    8000,
                    6,
                    true,
                    false);
                Type modeType = FindType("ArcaneArena.Frontend.MatchRewardMode");
                SetField(request, "mode", Enum.Parse(modeType, "Offline"));
                object receipt = ClaimReward(repository, request);
                Assert.That(Field(receipt, "status").ToString(),
                    Is.EqualTo("BlockedOfflineMode"));
                Assert.That(CoinBalance(repository), Is.Zero);
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

        private static void ConfigureAuthorization(
            object repository,
            string savePath,
            string nickname,
            string identitySuffix)
        {
            SetPlayerName(repository, nickname);
            object authorizationCatalog =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/Resources/Shop/AuthorizedCoinRecipientsCatalog.asset");
            Assert.That(authorizationCatalog, Is.Not.Null);
            Type installType = FindType(
                "ArcaneArena.Frontend.LocalInstallIdentityService");
            object installIdentity = Activator.CreateInstance(
                installType,
                savePath + "." + identitySuffix + ".identity");
            repository.GetType()
                .GetMethod("ConfigureCoinRewardAuthorization")
                .Invoke(repository, new[]
                {
                    authorizationCatalog,
                    installIdentity
                });
        }

        private static void SetPlayerName(object repository, string nickname)
        {
            object[] arguments = { nickname, null };
            bool saved = (bool)repository.GetType()
                .GetMethod("TrySetPlayerDisplayName")
                .Invoke(repository, arguments);
            Assert.That(saved, Is.True, arguments[1] as string);
        }

        private static object CreateRewardRequest(
            object repository,
            string matchId,
            string localPlayerId,
            object eligibility,
            int damage,
            int rounds,
            bool winner,
            bool draw)
        {
            Type requestType = FindType(
                "ArcaneArena.Frontend.MatchRewardRequest");
            object request = Activator.CreateInstance(requestType);
            SetField(request, "matchId", matchId);
            SetField(request, "localPlayerId", localPlayerId);
            object state = repository.GetType().GetProperty("State")
                .GetValue(repository);
            SetField(request, "localProfileId",
                Field(state, "localProfileId"));
            Type modeType = FindType("ArcaneArena.Frontend.MatchRewardMode");
            SetField(request, "mode", Enum.Parse(modeType, "OnlinePvP"));
            SetField(request, "isAuthoritativeFinal", true);
            SetField(request, "isWinner", winner);
            SetField(request, "isDraw", draw);
            SetField(request, "totalOpponentDamage", damage);
            SetField(request, "completedRounds", rounds);
            SetField(request, "eligibilityAtMatchStart", eligibility);
            return request;
        }

        private static object ClaimReward(object repository, object request)
        {
            object[] arguments = { request, null, null };
            bool claimed = (bool)repository.GetType()
                .GetMethod("TryClaimOnlineDuelReward")
                .Invoke(repository, arguments);
            Assert.That(claimed, Is.True, arguments[2] as string);
            Assert.That(arguments[1], Is.Not.Null);
            return arguments[1];
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
                Path.GetFullPath(Path.Combine("Temp", "ArcaneEconomyTests")),
                "arcane-shop-" + suffix + "-" + Guid.NewGuid().ToString("N") + ".json");
        }

        private static void DeleteSave(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            foreach (string candidate in Directory.GetFiles(
                         directory,
                         Path.GetFileName(path) + "*"))
            {
                if (File.Exists(candidate))
                    File.Delete(candidate);
            }
        }
    }
}
