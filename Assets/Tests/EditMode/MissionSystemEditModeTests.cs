using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class MissionSystemEditModeTests
    {
        [Test]
        public void CatalogOffersTwelveMissionsPerTierWithoutDuplicateIds()
        {
            UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<
                UnityEngine.Object>(
                "Assets/Resources/Missions/MissionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            object[] definitions = ((IEnumerable)catalog.GetType()
                    .GetProperty("Definitions").GetValue(catalog))
                .Cast<object>().ToArray();
            Assert.That(definitions, Has.Length.EqualTo(36));
            Assert.That(definitions.Select(item =>
                    Field(item, "missionId") as string)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(36));
            Assert.That(definitions.Count(item =>
                Field(item, "tier").ToString() == "Tier1"), Is.EqualTo(12));
            Assert.That(definitions.Count(item =>
                Field(item, "tier").ToString() == "Tier2"), Is.EqualTo(12));
            Assert.That(definitions.Count(item =>
                Field(item, "tier").ToString() == "Tier3"), Is.EqualTo(12));
        }

        [Test]
        public void ValidatedCycleSelectsTwoTwoOneAndStaysStable()
        {
            string path = TemporarySave("stable-cycle");
            try
            {
                object repository = CreateRepository(path);
                UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<
                    UnityEngine.Object>(
                    "Assets/Resources/Missions/MissionCatalog.asset");
                Assert.That(catalog, Is.Not.Null);

                const long now = 2_000_000_000L;
                object[] first = { now, catalog, null, null };
                Assert.That((bool)repository.GetType()
                    .GetMethod("TryRefreshMissionCycle")
                    .Invoke(repository, first), Is.True, first[3] as string);
                Assert.That((bool)first[2], Is.True);

                object[] missions = CurrentMissions(repository);
                Assert.That(missions, Has.Length.EqualTo(5));
                Assert.That(missions.Count(item =>
                    Field(item, "tier").ToString() == "Tier1"), Is.EqualTo(2));
                Assert.That(missions.Count(item =>
                    Field(item, "tier").ToString() == "Tier2"), Is.EqualTo(2));
                Assert.That(missions.Count(item =>
                    Field(item, "tier").ToString() == "Tier3"), Is.EqualTo(1));
                Assert.That(missions.Any(item =>
                    Field(item, "scope").ToString().StartsWith("Online",
                        StringComparison.Ordinal)), Is.True);
                string[] identities = missions.Select(item =>
                    Field(item, "missionInstanceId") as string).ToArray();

                object[] second = { now + 30, catalog, null, null };
                Assert.That((bool)repository.GetType()
                    .GetMethod("TryRefreshMissionCycle")
                    .Invoke(repository, second), Is.True, second[3] as string);
                Assert.That((bool)second[2], Is.False);
                Assert.That(CurrentMissions(repository).Select(item =>
                    Field(item, "missionInstanceId") as string),
                    Is.EqualTo(identities));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void RewardClaimIsIdempotentAndDoesNotCountItsOwnCoins()
        {
            string path = TemporarySave("idempotent-claim");
            try
            {
                object repository = CreateRepository(path);
                UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<
                    UnityEngine.Object>(
                    "Assets/Resources/Missions/MissionCatalog.asset");
                const long now = 2_000_000_000L;
                object[] refresh = { now, catalog, null, null };
                Assert.That((bool)repository.GetType()
                    .GetMethod("TryRefreshMissionCycle")
                    .Invoke(repository, refresh), Is.True);

                object mission = CurrentMissions(repository)[0];
                long target = (long)Field(mission, "targetValue");
                SetField(mission, "currentValue", target);
                SetField(mission, "completed", true);
                int reward = (int)Field(mission, "rewardCoins");
                string instanceId = Field(mission, "missionInstanceId") as string;
                int before = (int)repository.GetType()
                    .GetProperty("CoinBalance").GetValue(repository);

                object[] first = { instanceId, now, null, null };
                Assert.That((bool)repository.GetType()
                    .GetMethod("TryClaimMissionReward")
                    .Invoke(repository, first), Is.True, first[3] as string);
                Assert.That((int)repository.GetType()
                    .GetProperty("CoinBalance").GetValue(repository),
                    Is.EqualTo(before + reward));

                object[] second = { instanceId, now, null, null };
                Assert.That((bool)repository.GetType()
                    .GetMethod("TryClaimMissionReward")
                    .Invoke(repository, second), Is.True, second[3] as string);
                Assert.That((int)repository.GetType()
                    .GetProperty("CoinBalance").GetValue(repository),
                    Is.EqualTo(before + reward));

                object missionState = Field(
                    repository.GetType().GetProperty("State")
                        .GetValue(repository),
                    "missions");
                IEnumerable processed = (IEnumerable)Field(
                    missionState,
                    "processedProgressEventIds");
                Assert.That(processed.Cast<object>().Any(value =>
                    value?.ToString().StartsWith("mission-coins:mission:",
                        StringComparison.Ordinal) == true), Is.False);
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void AuthoritativeClockRollbackPreservesCycleAndBlocksClaims()
        {
            string path = TemporarySave("clock-rollback");
            try
            {
                object repository = CreateRepository(path);
                UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<
                    UnityEngine.Object>(
                    "Assets/Resources/Missions/MissionCatalog.asset");
                const long now = 2_000_000_000L;
                object[] first = { now, catalog, null, null };
                Assert.That((bool)repository.GetType()
                    .GetMethod("TryRefreshMissionCycle")
                    .Invoke(repository, first), Is.True);
                object state = repository.GetType().GetProperty("State")
                    .GetValue(repository);
                object missionState = Field(state, "missions");
                string cycle = Field(missionState, "cycleId") as string;

                object[] rollback = { now - 600, catalog, null, null };
                Assert.That((bool)repository.GetType()
                    .GetMethod("TryRefreshMissionCycle")
                    .Invoke(repository, rollback), Is.False);
                Assert.That(rollback[3] as string, Does.Contain("retrocedeu"));
                Assert.That(Field(missionState, "cycleId") as string,
                    Is.EqualTo(cycle));
                Assert.That((bool)Field(missionState, "timeValidated"), Is.False);
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void BronzeBadgeUsesOriginalUntintedSpriteColor()
        {
            Type bootstrap = FindType(
                "ArcaneArena.Frontend.GameFrontendBootstrap");
            Type rankTier = FindType("ArcaneDuel.Game.Competitive.RankTier");
            object bronze = Enum.Parse(rankTier, "Bronze");
            MethodInfo create = bootstrap.GetMethod(
                "CreateRankBadgeImage",
                BindingFlags.NonPublic | BindingFlags.Static);
            var root = new GameObject("Rank Badge Test", typeof(RectTransform));
            try
            {
                Image image = (Image)create.Invoke(null, new object[]
                {
                    root.transform,
                    "Bronze",
                    bronze,
                    Vector2.zero,
                    Vector2.one,
                    1f
                });
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(image.material, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static object CreateRepository(string path)
        {
            Type type = FindType("ArcaneArena.Frontend.DeckRepository");
            object repository = Activator.CreateInstance(type, path);
            type.GetMethod("Load").Invoke(repository, new object[] { null, false });
            return repository;
        }

        private static object[] CurrentMissions(object repository)
        {
            object state = repository.GetType().GetProperty("State")
                .GetValue(repository);
            object missionState = Field(state, "missions");
            return ((IEnumerable)Field(missionState, "missions"))
                .Cast<object>().ToArray();
        }

        private static object Field(object instance, string name) =>
            instance.GetType().GetField(name).GetValue(instance);

        private static void SetField(object instance, string name, object value) =>
            instance.GetType().GetField(name).SetValue(instance, value);

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static string TemporarySave(string name) => Path.Combine(
            Path.GetTempPath(),
            "arcane-missions-" + name + "-" + Guid.NewGuid().ToString("N") +
            ".json");

        private static void DeleteSave(string path)
        {
            foreach (string candidate in new[] { path, path + ".bak", path + ".tmp" })
            {
                if (File.Exists(candidate))
                    File.Delete(candidate);
            }
        }
    }
}
