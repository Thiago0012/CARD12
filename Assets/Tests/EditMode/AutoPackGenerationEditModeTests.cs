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
        [TestCase(39, new int[0], 39)]
        [TestCase(40, new[] { 40 }, 0)]
        [TestCase(85, new[] { 85 }, 0)]
        [TestCase(86, new[] { 43, 43 }, 0)]
        [TestCase(169, new[] { 85, 84 }, 0)]
        [TestCase(170, new[] { 85, 85 }, 0)]
        [TestCase(171, new[] { 57, 57, 57 }, 0)]
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
        public void GeneratedCatalogV2IsCompleteAndNormative()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/Shop/PackCatalog.json");
            Assert.That(json, Is.Not.Null);
            PackCatalogFile file = JsonUtility.FromJson<PackCatalogFile>(json.text);
            Assert.That(file?.packs, Is.Not.Null);
            Assert.That(file.version, Is.EqualTo(2));
            Assert.That(file.seed, Is.EqualTo(23082026));
            Assert.That(file.packs, Has.Length.EqualTo(40));
            Assert.That(file.packs.Select(pack => pack.packId),
                Is.EqualTo(Enumerable.Range(1, 40)
                    .Select(index => $"thematic-pack-{index:000}-v2")));

            PackRecord[] automatic = file.packs
                .Where(pack => pack.origin == 1)
                .ToArray();
            Assert.That(automatic, Has.Length.EqualTo(40));
            Assert.That(automatic.All(pack =>
                    pack.cardIds.Length >= 40 &&
                    pack.cardIds.Length <= 85 &&
                    pack.cardIds.Distinct(StringComparer.Ordinal).Count() ==
                    pack.cardIds.Length &&
                    pack.priceCoins == 25 &&
                    pack.previewCardIds.Length == 3 &&
                    pack.contentLockedAfterPublish &&
                    pack.countsForAutoCoverage &&
                    pack.published &&
                    pack.generatorVersion == 2 &&
                    !string.IsNullOrWhiteSpace(pack.contentHash)),
                Is.True);

            string[] actual = automatic
                .SelectMany(pack => pack.cardIds)
                .ToArray();
            Assert.That(actual, Has.Length.EqualTo(3252));
            Assert.That(actual.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(actual.Length));

            UnityEngine.Object catalog =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/Cards/CardCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            string[] expected = Values(Property(catalog, "Entries"))
                .Where(entry => entry != null &&
                    (bool)Property(entry, "IsCollectible") &&
                    (bool)Property(entry, "IsReadyForGameplay") &&
                    (bool)Property(entry, "OfficiallyRegistered"))
                .Select(entry => Property(entry, "OfficialCardId") as string)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            Assert.That(actual.OrderBy(id => id, StringComparer.Ordinal),
                Is.EqualTo(expected));
        }

        [Test]
        public void StrictPreBuildValidationAcceptsThematicCatalog()
        {
            Type validationType = FindType(
                "ArcaneArena.Editor.AutoPacks.AutoPackValidation");
            MethodInfo runStrict = validationType.GetMethod(
                "RunStrict",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(runStrict, Is.Not.Null);

            object result = runStrict.Invoke(null, null);
            string message = result.GetType().GetMethod("ToMessage")
                ?.Invoke(result, null) as string;
            Assert.That((bool)Property(result, "IsValid"), Is.True,
                message);
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
            return method.Invoke(null, new object[] { ids, 40, 85 });
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
            public int version;
            public int seed;
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
            public int generatorVersion;
            public bool countsForAutoCoverage;
            public bool published;
            public string[] previewCardIds;
            public string[] cardIds;
        }
    }
}
