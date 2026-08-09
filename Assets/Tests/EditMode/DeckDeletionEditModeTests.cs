using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class DeckDeletionEditModeTests
    {
        [Test]
        public void DeleteSelectedDeckSelectsNextAndPersists()
        {
            string path = TemporarySave();
            try
            {
                object repository = CreateRepository(path);
                object first = CreateDeck(repository, "Primeiro");
                object second = CreateDeck(repository, "Segundo");
                string firstId = Field<string>(first, "deckId");
                string secondId = Field<string>(second, "deckId");

                Assert.That(DeleteDeck(repository, firstId, out string rejection),
                    Is.True, rejection);
                Assert.That(DeckIds(repository), Is.EqualTo(new[] { secondId }));
                Assert.That(SelectedDeckId(repository), Is.EqualTo(secondId));

                object reloaded = CreateRepository(path);
                Assert.That(DeckIds(reloaded), Is.EqualTo(new[] { secondId }));
                Assert.That(SelectedDeckId(reloaded), Is.EqualTo(secondId));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void DeleteLastDeckLeavesGalleryEmptyAndRejectsUnknownId()
        {
            string path = TemporarySave();
            try
            {
                object repository = CreateRepository(path);
                object deck = CreateDeck(repository, "Descartável");
                string deckId = Field<string>(deck, "deckId");

                Assert.That(DeleteDeck(repository, deckId, out string rejection),
                    Is.True, rejection);
                Assert.That(DeckIds(repository), Is.Empty);
                Assert.That(SelectedDeckId(repository), Is.Empty);
                Assert.That(DeleteDeck(repository, "inexistente", out rejection),
                    Is.False);
                Assert.That(rejection, Is.Not.Empty);
                Assert.That(DeckIds(repository), Is.Empty);
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
            type.GetMethod("Load")?.Invoke(
                repository,
                new object[] { null, false });
            return repository;
        }

        private static object CreateDeck(object repository, string name)
        {
            return repository.GetType().GetMethod("CreateDeck")?.Invoke(
                repository,
                new object[] { name, 0 });
        }

        private static bool DeleteDeck(
            object repository,
            string deckId,
            out string rejection)
        {
            object[] arguments = { deckId, null };
            bool deleted = (bool)repository.GetType()
                .GetMethod("TryDeleteDeck")
                .Invoke(repository, arguments);
            rejection = arguments[1] as string;
            return deleted;
        }

        private static string[] DeckIds(object repository)
        {
            object state = repository.GetType().GetProperty("State")
                .GetValue(repository);
            return ((IEnumerable)state.GetType().GetField("decks")
                    .GetValue(state))
                .Cast<object>()
                .Select(deck => Field<string>(deck, "deckId"))
                .ToArray();
        }

        private static string SelectedDeckId(object repository)
        {
            object state = repository.GetType().GetProperty("State")
                .GetValue(repository);
            return Field<string>(state, "selectedDeckId");
        }

        private static T Field<T>(object source, string name)
        {
            return (T)source.GetType().GetField(name).GetValue(source);
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Tipo runtime ausente: " + fullName);
            return type;
        }

        private static string TemporarySave()
        {
            return Path.Combine(
                Path.GetFullPath(Path.Combine("Temp", "DeckDeletionTests")),
                "decks-" + Guid.NewGuid().ToString("N") + ".json");
        }

        private static void DeleteSave(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory))
            {
                return;
            }

            foreach (string candidate in Directory.GetFiles(
                         directory,
                         Path.GetFileName(path) + "*"))
            {
                File.Delete(candidate);
            }
        }
    }
}
