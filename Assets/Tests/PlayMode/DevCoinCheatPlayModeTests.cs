using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class DevCoinCheatPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExactAndLowercaseSequencesGrantOnceEach()
        {
            string path = TemporarySave();
            GameObject root = new GameObject("Dev coin listener test");
            try
            {
                object repository = CreateRepository(path);
                Type listenerType = FindType(
                    "ArcaneArena.Frontend.DevCoinCheatListener");
                Component listener = root.AddComponent(listenerType);
                listenerType.GetMethod("Configure").Invoke(
                    listener,
                    new[] { repository, (object)true, 4f, 1000 });

                Enter(listener, "LUCAS GAY", 1f, 0.35f, true);
                Assert.That(CoinBalance(repository), Is.EqualTo(1000));
                Enter(listener, "lucas gay", 10f, 0.35f, true);
                Assert.That(CoinBalance(repository), Is.EqualTo(2000));
                yield return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                DeleteTemporaryFiles(path);
            }
        }

        [UnityTest]
        public IEnumerator TimeoutWrongInputAndMissingFocusDoNotGrant()
        {
            string path = TemporarySave();
            GameObject root = new GameObject("Dev coin listener guard test");
            try
            {
                object repository = CreateRepository(path);
                Type listenerType = FindType(
                    "ArcaneArena.Frontend.DevCoinCheatListener");
                Component listener = root.AddComponent(listenerType);
                listenerType.GetMethod("Configure").Invoke(
                    listener,
                    new[] { repository, (object)true, 4f, 1000 });

                Enter(listener, "LUCAS GAY", 1f, 0.51f, true);
                Enter(listener, "LUCAX GAY", 20f, 0.2f, true);
                Enter(listener, "LUCAS GAY", 30f, 0.2f, false);
                Assert.That(CoinBalance(repository), Is.Zero);
                yield return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                DeleteTemporaryFiles(path);
            }
        }

        private static void Enter(
            object listener,
            string sequence,
            float startedAt,
            float step,
            bool focused)
        {
            MethodInfo accept = listener.GetType().GetMethod(
                "AcceptCharacterForTests");
            for (int index = 0; index < sequence.Length; index++)
            {
                accept.Invoke(listener, new object[]
                {
                    sequence[index],
                    startedAt + index * step,
                    focused
                });
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

        private static int CoinBalance(object repository)
        {
            return (int)repository.GetType().GetProperty("CoinBalance")
                .GetValue(repository);
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
                Path.GetFullPath(Path.Combine(
                    "Temp",
                    "ArcaneDevCheatTests")),
                "dev-cheat-" + Guid.NewGuid().ToString("N") + ".json");
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
