using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class StarterDeckClaimEditModeTests
    {
        [Test]
        public void C01_ClaimIsAtomicAndIdempotent()
        {
            string path = TemporarySave("idempotent");
            try
            {
                object repository = CreateRepository(path);
                SetPlayerName(repository, "Duelista Teste");
                StarterDeckCatalog catalog = StarterCatalog();
                StarterDeckDefinition definition = catalog.Decks
                    .First(deck => deck != null && deck.IsPublishable);

                object firstReceipt = Claim(
                    repository, definition, catalog, expected: true);
                object state = State(repository);
                int quantityAfterFirst = TotalOwnedQuantity(state);
                int deckCountAfterFirst = Values(Field(state, "decks")).Length;
                Assert.That(quantityAfterFirst, Is.EqualTo(
                    definition.MainDeck.Count + definition.ExtraDeck.Count));

                object repeatedReceipt = Claim(
                    repository, definition, catalog, expected: true);
                state = State(repository);

                Assert.That(Field(firstReceipt, "transactionId"),
                    Is.EqualTo(Field(repeatedReceipt, "transactionId")));
                Assert.That(TotalOwnedQuantity(state),
                    Is.EqualTo(quantityAfterFirst));
                Assert.That(Values(Field(state, "decks")).Length,
                    Is.EqualTo(deckCountAfterFirst));
                Assert.That(Values(Field(state, "processedShopTransactions"))
                    .Count(item => string.Equals(
                        Field(item, "kind") as string,
                        "starter-deck",
                        StringComparison.Ordinal)), Is.EqualTo(1));
                Assert.That(Field(state, "starterDeckClaimed"), Is.True);
                Assert.That(Field(state, "starterDeckId"),
                    Is.EqualTo(definition.Id));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void C02_FailureBeforeCommitRollsBackInventoryAndDeck()
        {
            string path = TemporarySave("rollback");
            Type repositoryType = FindType("ArcaneArena.Frontend.DeckRepository");
            FieldInfo hook = repositoryType.GetField(
                "StarterClaimBeforeCommitTestHook",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(hook, Is.Not.Null);
            try
            {
                object repository = CreateRepository(path);
                SetPlayerName(repository, "Duelista Teste");
                StarterDeckCatalog catalog = StarterCatalog();
                StarterDeckDefinition definition = catalog.Decks
                    .First(deck => deck != null && deck.IsPublishable);
                hook.SetValue(null, (Action)(() =>
                    throw new InvalidOperationException("falha simulada")));
                object[] arguments = { definition, catalog, null, null };

                bool claimed = (bool)repositoryType
                    .GetMethod("TryClaimStarterDeck")
                    .Invoke(repository, arguments);

                Assert.That(claimed, Is.False);
                object state = State(repository);
                Assert.That(Field(state, "starterDeckClaimed"), Is.False);
                Assert.That(Values(Field(state, "decks")), Is.Empty);
                Assert.That(TotalOwnedQuantity(state), Is.Zero);
                Assert.That(Values(Field(state, "processedShopTransactions"))
                    .Count(item => string.Equals(
                        Field(item, "kind") as string,
                        "starter-deck",
                        StringComparison.Ordinal)), Is.Zero);
            }
            finally
            {
                hook?.SetValue(null, null);
                DeleteSave(path);
            }
        }

        [Test]
        public void C03_ProfileCannotClaimAnotherStarterDeck()
        {
            string path = TemporarySave("single-choice");
            try
            {
                object repository = CreateRepository(path);
                SetPlayerName(repository, "Duelista Teste");
                StarterDeckCatalog catalog = StarterCatalog();
                StarterDeckDefinition[] choices = catalog.Decks
                    .Where(deck => deck != null && deck.IsPublishable)
                    .Take(2)
                    .ToArray();
                Assert.That(choices, Has.Length.EqualTo(2));

                Claim(repository, choices[0], catalog, expected: true);
                object[] arguments = { choices[1], catalog, null, null };
                bool claimed = (bool)repository.GetType()
                    .GetMethod("TryClaimStarterDeck")
                    .Invoke(repository, arguments);

                Assert.That(claimed, Is.False);
                Assert.That(arguments[3] as string, Does.Contain("ja foi escolhido"));
                Assert.That(Field(State(repository), "starterDeckId"),
                    Is.EqualTo(choices[0].Id));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void C04_ClaimPersistsAcrossRepositoryReload()
        {
            string path = TemporarySave("reload");
            try
            {
                object repository = CreateRepository(path);
                SetPlayerName(repository, "Duelista Teste");
                StarterDeckCatalog catalog = StarterCatalog();
                StarterDeckDefinition definition = catalog.Decks
                    .First(deck => deck != null && deck.IsPublishable);
                Claim(repository, definition, catalog, expected: true);

                object reloaded = CreateRepository(path);
                object state = State(reloaded);
                Assert.That(Field(state, "starterDeckClaimed"), Is.True);
                Assert.That(Field(state, "starterDeckId"),
                    Is.EqualTo(definition.Id));
                Assert.That(reloaded.GetType()
                    .GetProperty("NeedsStarterDeckSelection")
                    .GetValue(reloaded), Is.False);
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void O01_EveryPublishableStarterDeckPassesHostGate()
        {
            StarterDeckCatalog catalog = StarterCatalog();
            StarterDeckDefinition[] publishable = catalog.Decks
                .Where(deck => deck != null && deck.IsPublishable)
                .ToArray();
            Assert.That(catalog.Decks.Count, Is.EqualTo(6));
            Assert.That(publishable, Has.Length.EqualTo(5));

            foreach (StarterDeckDefinition definition in publishable)
            {
                object loadout = CreateLoadout(definition);
                object[] arguments = { loadout, null };
                bool valid = (bool)FindType(
                        "ArcaneArena.Multiplayer.OnlineDeckLegalityGate")
                    .GetMethod("TryValidate",
                        BindingFlags.Static | BindingFlags.NonPublic |
                        BindingFlags.Public)
                    .Invoke(null, arguments);
                Assert.That(valid, Is.True,
                    definition.DisplayName + ": " + arguments[1]);
            }
        }

        [Test]
        public void O02_HostGateRejectsTamperedManifest()
        {
            StarterDeckDefinition definition = StarterCatalog().Decks
                .First(deck => deck != null && deck.IsPublishable);
            object loadout = CreateLoadout(definition);
            SetField(loadout, "normalizedDeckSha256",
                new string('0', 64));
            object[] arguments = { loadout, null };

            bool valid = (bool)FindType(
                    "ArcaneArena.Multiplayer.OnlineDeckLegalityGate")
                .GetMethod("TryValidate",
                    BindingFlags.Static | BindingFlags.NonPublic |
                    BindingFlags.Public)
                .Invoke(null, arguments);

            Assert.That(valid, Is.False);
            Assert.That(arguments[1] as string, Does.Contain("SHA-256"));
        }

        private static object CreateRepository(string path)
        {
            Type type = FindType("ArcaneArena.Frontend.DeckRepository");
            object repository = Activator.CreateInstance(type, path);
            UnityEngine.Object cardCatalog = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/Cards/CardCatalog.asset");
            Assert.That(cardCatalog, Is.Not.Null);
            type.GetMethod("Load").Invoke(
                repository, new[] { cardCatalog, (object)false });
            return repository;
        }

        private static object CreateLoadout(StarterDeckDefinition definition)
        {
            Type type = FindType("ArcaneArena.Frontend.DuelDeckLoadout");
            object loadout = Activator.CreateInstance(type);
            var main = new List<string>(definition.MainDeck);
            var extra = new List<string>(definition.ExtraDeck);
            var side = new List<string>(definition.SideDeck);
            SetField(loadout, "profileId", "test-profile");
            SetField(loadout, "playerDisplayName", "Duelista Teste");
            SetField(loadout, "deckId", definition.Id);
            SetField(loadout, "displayName", definition.DisplayName);
            SetField(loadout, "mainDeckCardIds", main);
            SetField(loadout, "extraDeckCardIds", extra);
            SetField(loadout, "sideDeckCardIds", side);
            SetField(loadout, "banlistId", BanlistService.ActiveBanlistId);
            SetField(loadout, "normalizedDeckSha256",
                DeckManifestHasher.ComputeSha256(
                    BanlistService.ActiveBanlistId, main, extra, side));
            return loadout;
        }

        private static StarterDeckCatalog StarterCatalog()
        {
            StarterDeckCatalog catalog = Resources.Load<StarterDeckCatalog>(
                "StarterDecks/StarterDeckCatalog");
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }

        private static void SetPlayerName(object repository, string nickname)
        {
            object[] arguments = { nickname, null };
            bool saved = (bool)repository.GetType()
                .GetMethod("TrySetPlayerDisplayName")
                .Invoke(repository, arguments);
            Assert.That(saved, Is.True, arguments[1] as string);
        }

        private static object Claim(
            object repository,
            StarterDeckDefinition definition,
            StarterDeckCatalog catalog,
            bool expected)
        {
            object[] arguments = { definition, catalog, null, null };
            bool claimed = (bool)repository.GetType()
                .GetMethod("TryClaimStarterDeck")
                .Invoke(repository, arguments);
            Assert.That(claimed, Is.EqualTo(expected), arguments[3] as string);
            if (expected)
                Assert.That(arguments[2], Is.Not.Null);
            return arguments[2];
        }

        private static int TotalOwnedQuantity(object state)
        {
            return Values(Field(state, "cardQuantities"))
                .Sum(item => (int)Field(item, "quantity"));
        }

        private static object State(object repository)
        {
            return repository.GetType().GetProperty("State").GetValue(repository);
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

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Tipo runtime ausente: " + fullName);
            return type;
        }

        private static string TemporarySave(string suffix)
        {
            return Path.Combine(
                Path.GetFullPath(Path.Combine("Temp", "StarterDeckClaimTests")),
                "starter-" + suffix + "-" + Guid.NewGuid().ToString("N") + ".json");
        }

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
