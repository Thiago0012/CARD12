using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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
        public void ReleaseManifestIsSignedByTheEmbeddedProductionKey()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string settingsJson = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets",
                "Resources",
                "RemoteUpdates",
                "RemoteUpdateSettings.json"));
            Assert.That(settingsJson, Does.Contain("\"requireSignature\": true"));
            Assert.That(settingsJson, Does.Contain("\"expectedKeyId\": \"production-2026\""));

            Type runtimeType = FindType(
                "ArcaneArena.Frontend.RemoteUpdateRuntime");
            Type envelopeType = FindType(
                "ArcaneArena.Frontend.RemoteReleaseEnvelope");
            string envelopeJson = File.ReadAllText(Path.Combine(
                projectRoot,
                "ContentStaging",
                "production",
                "v2",
                "release-envelope.json"));
            object envelope = JsonUtility.FromJson(
                envelopeJson,
                envelopeType);
            var gameObject = new GameObject("Manifest signature test");
            try
            {
                object runtime = gameObject.AddComponent(runtimeType);
                MethodInfo validate = runtimeType.GetMethod(
                    "ValidateEnvelope",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.DoesNotThrow(() => validate.Invoke(
                    runtime,
                    new[] { envelope, (object)false }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ReleaseManifestRejectsPayloadChangedAfterSigning()
        {
            Type runtimeType = FindType(
                "ArcaneArena.Frontend.RemoteUpdateRuntime");
            Type envelopeType = FindType(
                "ArcaneArena.Frontend.RemoteReleaseEnvelope");
            string envelopeJson = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "ContentStaging",
                "production",
                "v2",
                "release-envelope.json"));
            object envelope = JsonUtility.FromJson(envelopeJson, envelopeType);
            object payload = envelopeType.GetField("payload").GetValue(envelope);
            payload.GetType().GetField("summary").SetValue(
                payload,
                "manifesto adulterado");
            var gameObject = new GameObject("Tampered manifest test");
            try
            {
                object runtime = gameObject.AddComponent(runtimeType);
                MethodInfo validate = runtimeType.GetMethod(
                    "ValidateEnvelope",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                TargetInvocationException exception = Assert.Throws<
                    TargetInvocationException>(() => validate.Invoke(
                    runtime,
                    new[] { envelope, (object)false }));
                Assert.That(
                    exception.InnerException,
                    Is.TypeOf<System.Security.Cryptography.CryptographicException>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ProductionManifestHasFreshnessAndRollbackMetadata()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string path = Path.Combine(
                projectRoot,
                "ContentStaging",
                "production",
                "v2",
                "release-envelope.json");
            string json = File.ReadAllText(path);

            Assert.That(json, Does.Contain("\"sequenceNumber\": 2"));
            Assert.That(json, Does.Contain("\"channel\": \"production\""));
            Assert.That(json, Does.Contain("\"expiresUtc\":"));

            string runtime = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets",
                "Scripts",
                "Frontend",
                "RemoteUpdateRuntime.cs"));
            Assert.That(runtime, Does.Contain(
                "manifest.sequenceNumber < trusted.highestSequenceNumber"));
            Assert.That(runtime, Does.Contain("expiresUtc <= now"));
        }

        [Test]
        public void LegacyManifestRemainsAvailableForInstalledVersionMigration()
        {
            string json = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "ContentStaging",
                "production",
                "release-envelope.json"));

            Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(json, Does.Not.Contain("\"sequenceNumber\""));
        }

        [Test]
        public void AndroidUpdaterDeclaresInstallerPermissionAndReceiver()
        {
            string manifest = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "Android",
                "MasterDuelUpdater.androidlib",
                "AndroidManifest.xml"));

            Assert.That(manifest, Does.Contain("REQUEST_INSTALL_PACKAGES"));
            Assert.That(manifest, Does.Contain("UpdateInstallReceiver"));
            Assert.That(manifest, Does.Contain("android:exported=\"false\""));
        }

        [Test]
        public void WindowsUpdaterContainsRollbackAndPathContainmentGuards()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Scripts",
                "Frontend",
                "PlatformApplicationUpdater.cs"));

            Assert.That(source, Does.Contain("backupDirectory"));
            Assert.That(source, Does.Contain("O pacote tentou gravar fora da instalação"));
            Assert.That(source, Does.Contain("Copy-Item -LiteralPath $backupFile"));
        }

        [Test]
        public void WindowsUpdateExtractorRejectsZipTraversal()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "master-duel-update-test-" + Guid.NewGuid().ToString("N"));
            string archivePath = Path.Combine(root, "malicious.zip");
            string destination = Path.Combine(root, "staging");
            Directory.CreateDirectory(root);
            try
            {
                using (var archive = ZipFile.Open(
                           archivePath,
                           ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = archive.CreateEntry(
                        "../outside.txt");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write("blocked");
                }

                Type updater = FindType(
                    "ArcaneArena.Frontend.PlatformApplicationUpdater");
                MethodInfo extract = updater.GetMethod(
                    "ExtractZipSafely",
                    BindingFlags.NonPublic | BindingFlags.Static);
                TargetInvocationException exception = Assert.Throws<
                    TargetInvocationException>(() => extract.Invoke(
                    null,
                    new object[] { archivePath, destination }));

                Assert.That(exception.InnerException, Is.TypeOf<IOException>());
                Assert.That(
                    File.Exists(Path.Combine(root, "outside.txt")),
                    Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
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
                object sourceState = source.GetType()
                    .GetProperty("State")
                    .GetValue(source);
                SetField(sourceState, "coinBalance", 4321);
                object sourceRank = GetField(sourceState, "rankData");
                SetField(sourceRank, "rankedPoints", 180);
                object sourceStatistics = GetField(sourceState, "statistics");
                object sourceOverall = GetField(sourceStatistics, "overall");
                SetField(sourceOverall, "duelsPlayed", 17L);
                SetField(sourceOverall, "wins", 11L);
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
                object restoredState = restored.GetType()
                    .GetProperty("State")
                    .GetValue(restored);
                Assert.That(GetField(restoredState, "coinBalance"),
                    Is.GreaterThanOrEqualTo(4321),
                    "A restauração não pode remover moedas; recompensas de " +
                    "ranque ainda não processadas podem aumentar o saldo.");
                Assert.That(
                    GetField(GetField(restoredState, "rankData"),
                        "rankedPoints"),
                    Is.EqualTo(180));
                object restoredOverall = GetField(
                    GetField(restoredState, "statistics"),
                    "overall");
                Assert.That(GetField(restoredOverall, "duelsPlayed"),
                    Is.EqualTo(17L));
                Assert.That(GetField(restoredOverall, "wins"),
                    Is.EqualTo(11L));
            }
            finally
            {
                DeleteSave(sourcePath);
                DeleteSave(restoredPath);
            }
        }

        [Test]
        public void TitleAccountRestoreRequestIsConsumedOnlyOnce()
        {
            Type account = FindType(
                "ArcaneArena.Frontend.PlayerAccountRuntime");
            MethodInfo request = account.GetMethod(
                "RequestRestoreOnNextMenu",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo consume = account.GetMethod(
                "ConsumeRestoreRequest",
                BindingFlags.Public | BindingFlags.Static);

            consume.Invoke(null, null);
            request.Invoke(null, null);

            Assert.That((bool)consume.Invoke(null, null), Is.True);
            Assert.That((bool)consume.Invoke(null, null), Is.False);
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

        [Test]
        public void CloudRestoreRequiresARealPlayerProfile()
        {
            Type stateType = FindType(
                "ArcaneArena.Frontend.DeckCollectionState");
            object emptyState = Activator.CreateInstance(stateType);
            object namedState = Activator.CreateInstance(stateType);
            SetField(namedState, "playerDisplayName", "KimDelas");

            Type runtime = FindType(
                "ArcaneArena.Frontend.PlayerCloudSaveRuntime");
            MethodInfo hasProfile = runtime.GetMethod(
                "HasPlayerProfile",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(
                (bool)hasProfile.Invoke(null, new[] { emptyState }),
                Is.False);
            Assert.That(
                (bool)hasProfile.Invoke(null, new[] { namedState }),
                Is.True);
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

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name).GetValue(target);
        }

        private static void SetField(
            object target,
            string name,
            object value)
        {
            target.GetType().GetField(name).SetValue(target, value);
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
