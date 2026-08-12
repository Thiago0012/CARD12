using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class AutoPackGenerationEditModeTests
    {
        [TestCase(0, new int[0], 0)]
        [TestCase(1, new int[0], 1)]
        [TestCase(34, new int[0], 34)]
        [TestCase(35, new[] { 35 }, 0)]
        [TestCase(38, new[] { 38 }, 0)]
        [TestCase(39, new[] { 38 }, 1)]
        [TestCase(70, new[] { 35, 35 }, 0)]
        [TestCase(71, new[] { 36, 35 }, 0)]
        [TestCase(76, new[] { 38, 38 }, 0)]
        [TestCase(77, new[] { 38, 38 }, 1)]
        [TestCase(104, new[] { 38, 38 }, 28)]
        [TestCase(105, new[] { 35, 35, 35 }, 0)]
        [TestCase(114, new[] { 38, 38, 38 }, 0)]
        public void PartitionerMatchesEveryNormativeBoundary(
            int count,
            int[] expectedSizes,
            int expectedPending)
        {
            object result = Partition(Ids(count));
            int[] sizes = Values(Property(result, "Sizes"))
                .Select(Convert.ToInt32)
                .ToArray();
            Assert.That(sizes, Is.EqualTo(expectedSizes));
            Assert.That(Values(Property(result, "Pending")),
                Has.Length.EqualTo(expectedPending));
        }

        [Test]
        public void SameInputAndSeedProduceTheSameDistribution()
        {
            Type determinism = FindType(
                "ArcaneArena.Editor.AutoPacks.AutoPackDeterminism");
            MethodInfo shuffle = determinism.GetMethod(
                "Shuffle",
                BindingFlags.Static | BindingFlags.NonPublic);
            string[] source = Ids(114);
            string[] first = Values(shuffle.Invoke(
                    null,
                    new object[] { source, "generator-1|snapshot|1" }))
                .Cast<string>()
                .ToArray();
            string[] second = Values(shuffle.Invoke(
                    null,
                    new object[] { source.Reverse().ToArray(),
                        "generator-1|snapshot|1" }))
                .Cast<string>()
                .ToArray();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(Values(Property(Partition(first), "Packs"))
                    .SelectMany(Values)
                    .Cast<string>(),
                Is.EqualTo(first));
        }

        [Test]
        public void GeneratedCatalogIsAppendOnlyAndNormative()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/Shop/PackCatalog.json");
            Assert.That(json, Is.Not.Null);
            PackCatalogFile file = JsonUtility.FromJson<PackCatalogFile>(json.text);
            Assert.That(file?.packs, Is.Not.Null);
            Assert.That(file.packs.Take(19).Select(pack => pack.packId),
                Is.EqualTo(Enumerable.Range(1, 19)
                    .Select(index => $"pack-{index:00}-v1")));

            PackRecord[] automatic = file.packs
                .Where(pack => pack.origin == 1)
                .ToArray();
            Assert.That(automatic, Has.Length.EqualTo(7));
            Assert.That(automatic.All(pack =>
                    pack.cardIds.Length is >= 35 and <= 38 &&
                    pack.cardIds.Distinct(StringComparer.Ordinal).Count() ==
                    pack.cardIds.Length &&
                    pack.priceCoins == 25 &&
                    pack.previewCardIds.Length == 3 &&
                    pack.contentLockedAfterPublish &&
                    pack.countsForAutoCoverage &&
                    pack.published &&
                    !string.IsNullOrWhiteSpace(pack.contentHash)),
                Is.True);

            UnityEngine.Object manifest = AssetDatabase.LoadAssetAtPath<
                UnityEngine.Object>(
                "Assets/GameData/Shop/AutoPackGenerationManifest.asset");
            Assert.That(manifest, Is.Not.Null);
            Assert.That(Values(Property(manifest, "PendingCardIds")),
                Has.Length.EqualTo(12));
        }

        [Test]
        public void CurrentSnapshotIsIdempotentAndCreatesNoAdditionalPack()
        {
            Type coordinator = FindType(
                "ArcaneArena.Editor.AutoPacks.AutoPackGenerationCoordinator");
            MethodInfo run = coordinator.GetMethod(
                "Run",
                BindingFlags.Static | BindingFlags.NonPublic);
            object preview = run.Invoke(null, new object[] { false, "EditMode" });

            Assert.That(Values(Property(preview, "Errors")), Is.Empty);
            Assert.That(Values(Property(preview, "CreatedPacks")), Is.Empty);
            Assert.That(Values(Property(preview, "NewCardIds")), Is.Empty);
            Assert.That(Values(Property(preview, "PendingCardIds")),
                Has.Length.EqualTo(12));
            Assert.That(Property(preview, "Saved"), Is.False);
        }

        [Test]
        public void GladiatorStarterDeckUsesApprovedThematicReplacement()
        {
            StarterDeckDefinition deck =
                AssetDatabase.LoadAssetAtPath<StarterDeckDefinition>(
                    "Assets/Resources/StarterDecks/Definitions/" +
                    "starter_gladiator_control.asset");
            Assert.That(deck, Is.Not.Null);
            Assert.That(deck.IsPublishable, Is.True);
            Assert.That(deck.MainDeck, Has.Count.EqualTo(40));
            Assert.That(deck.MainDeck.Count(id => id == "35224440"),
                Is.EqualTo(3));
            Assert.That(deck.Replacements.Any(replacement =>
                    replacement.approved &&
                    replacement.removedPasscode == "19613556" &&
                    replacement.replacementPasscode == "35224440"),
                Is.True);
        }

        private static object Partition(string[] ids)
        {
            Type type = FindType(
                "ArcaneArena.Editor.AutoPacks.AutoPackPartitioner");
            MethodInfo method = type.GetMethod(
                "Partition",
                BindingFlags.Public | BindingFlags.Static);
            return method.Invoke(null, new object[] { ids, 35, 38 });
        }

        private static string[] Ids(int count)
        {
            return Enumerable.Range(1, count)
                .Select(index => index.ToString("00000000"))
                .ToArray();
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Tipo ausente: " + fullName);
            return type;
        }

        private static object Property(object source, string name)
        {
            return source.GetType().GetProperty(name).GetValue(source);
        }

        private static object[] Values(object source)
        {
            return ((IEnumerable)source).Cast<object>().ToArray();
        }

        [Serializable]
        private sealed class PackCatalogFile
        {
            public PackRecord[] packs;
        }

        [Serializable]
        private sealed class PackRecord
        {
            public string packId;
            public int priceCoins;
            public int origin;
            public bool contentLockedAfterPublish;
            public string contentHash;
            public bool countsForAutoCoverage;
            public bool published;
            public string[] previewCardIds;
            public string[] cardIds;
        }
    }
}
