using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class AccountAndUpdateInfrastructureEditModeTests
    {
        [TestCase("1.2.0", "1.2.0", 0)]
        [TestCase("1.2.1", "1.2.0", 1)]
        [TestCase("1.10.0", "1.9.9", 1)]
        [TestCase("2.0.0", "10.0.0", -1)]
        [TestCase("1.2.0+android.4", "1.2.0", 0)]
        public void RemoteUpdateUsesNumericSemanticVersionComparison(
            string installed,
            string published,
            int expectedSign)
        {
            Type comparer = FindType(
                "ArcaneArena.Frontend.RemoteUpdateRuntime+SemanticVersion");
            MethodInfo compare = comparer.GetMethod(
                "Compare",
                BindingFlags.Public | BindingFlags.Static);

            int result = (int)compare.Invoke(
                null,
                new object[] { installed, published });

            Assert.That(Math.Sign(result), Is.EqualTo(expectedSign));
        }

        [Test]
        public void LoginStaysClosedUntilUpdateRuntimeFinishesChecking()
        {
            Type runtime = FindType(
                "ArcaneArena.Frontend.RemoteUpdateRuntime");
            FieldInfo instance = runtime.GetField(
                "_instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            object previous = instance.GetValue(null);
            try
            {
                instance.SetValue(null, null);
                bool ready = (bool)runtime.GetProperty(
                        "EntryReady",
                        BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null);

                Assert.That(ready, Is.False,
                    "O login não pode abrir antes da consulta de versão.");
            }
            finally
            {
                instance.SetValue(null, previous);
            }
        }

        [Test]
        public void RemoteUpdateSettingsFailClosedWhenServerIsUnavailable()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Resources",
                "RemoteUpdates",
                "RemoteUpdateSettings.json");

            string json = File.ReadAllText(path);

            Assert.That(json, Does.Contain("\"failOpenWhenUnavailable\": false"));
        }

        [Test]
        public void CloudProfileRoundTripPreservesNameAndAuthenticatedIdentity()
        {
            string sourcePath = TemporarySave("source");
            string restoredPath = TemporarySave("restored");
            try
            {
                object source = CreateRepository(sourcePath);
                Assert.That(SetName(source, "KimDelas", out string nameError),
                    Is.True, nameError);
                Assert.That(Bind(source, "unity-player-kim", out string bindError),
                    Is.True, bindError);
                string json = (string)source.GetType()
                    .GetMethod("ExportJson")
                    .Invoke(source, new object[] { true });

                object restored = CreateRepository(restoredPath);
                object[] importArguments =
                {
                    json,
                    "unity-player-kim",
                    null
                };
                bool imported = (bool)restored.GetType()
                    .GetMethod("TryImportCloudJson")
                    .Invoke(restored, importArguments);

                Assert.That(imported, Is.True, importArguments[2] as string);
                Assert.That(
                    restored.GetType().GetProperty("PlayerDisplayName")
                        .GetValue(restored),
                    Is.EqualTo("KimDelas"));
                Assert.That(
                    restored.GetType().GetProperty("AuthenticatedPlayerId")
                        .GetValue(restored),
                    Is.EqualTo("unity-player-kim"));
            }
            finally
            {
                DeleteSave(sourcePath);
                DeleteSave(restoredPath);
            }
        }

        [Test]
        public void CloudProfileCannotBeImportedIntoDifferentAuthenticatedIdentity()
        {
            string sourcePath = TemporarySave("owner");
            string targetPath = TemporarySave("intruder");
            try
            {
                object source = CreateRepository(sourcePath);
                Assert.That(Bind(source, "owner-player-id", out _), Is.True);
                string json = (string)source.GetType()
                    .GetMethod("ExportJson")
                    .Invoke(source, new object[] { false });
                object target = CreateRepository(targetPath);
                object[] arguments = { json, "another-player-id", null };

                bool imported = (bool)target.GetType()
                    .GetMethod("TryImportCloudJson")
                    .Invoke(target, arguments);

                Assert.That(imported, Is.False);
                Assert.That(arguments[2] as string, Does.Contain("outra conta"));
            }
            finally
            {
                DeleteSave(sourcePath);
                DeleteSave(targetPath);
            }
        }

        private static object CreateRepository(string path)
        {
            Type type = FindType("ArcaneArena.Frontend.DeckRepository");
            object repository = Activator.CreateInstance(type, path);
            type.GetMethod("Load").Invoke(
                repository,
                new object[] { null, false });
            return repository;
        }

        private static bool SetName(
            object repository,
            string name,
            out string rejection)
        {
            object[] arguments = { name, null };
            bool result = (bool)repository.GetType()
                .GetMethod("TrySetPlayerDisplayName")
                .Invoke(repository, arguments);
            rejection = arguments[1] as string;
            return result;
        }

        private static bool Bind(
            object repository,
            string playerId,
            out string rejection)
        {
            object[] arguments = { playerId, null };
            bool result = (bool)repository.GetType()
                .GetMethod("TryBindAuthenticatedPlayerId")
                .Invoke(repository, arguments);
            rejection = arguments[1] as string;
            return result;
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName + " não foi carregado.");
            return type;
        }

        private static string TemporarySave(string suffix)
        {
            return Path.Combine(
                Path.GetTempPath(),
                "arcane-account-update-tests",
                Guid.NewGuid().ToString("N") + "-" + suffix + ".json");
        }

        private static void DeleteSave(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) &&
                Directory.Exists(directory) &&
                Directory.GetFiles(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }
    }
}
