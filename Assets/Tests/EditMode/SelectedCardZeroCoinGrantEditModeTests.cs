using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcaneArena.Editor.DeveloperTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class SelectedCardZeroCoinGrantEditModeTests
    {
        [Test]
        public void AllowedScreenAndAlphaZeroGrantExactlyOneThousand()
        {
            var bridge = ReadyBridge(alpha: true);
            var controller = new ZeroCoinGrantController("test-session");

            Assert.That(controller.Tick(bridge), Is.True);
            Assert.That(bridge.Balance, Is.EqualTo(1000));
            Assert.That(bridge.Amounts, Is.EqualTo(new[] { 1000 }));
            Assert.That(bridge.Reasons,
                Is.EqualTo(new[] { "EditorSelectedCardZero" }));
        }

        [Test]
        public void NumpadZeroUsesTheSameAction()
        {
            var bridge = ReadyBridge(numpad: true);
            var controller = new ZeroCoinGrantController("test-session");

            Assert.That(controller.Tick(bridge), Is.True);
            Assert.That(bridge.Balance, Is.EqualTo(1000));
        }

        [Test]
        public void HeldKeyDoesNotRepeatWithoutAnotherKeyDown()
        {
            var bridge = ReadyBridge(alpha: true);
            var controller = new ZeroCoinGrantController("test-session");

            Assert.That(controller.Tick(bridge), Is.True);
            Assert.That(controller.Tick(bridge), Is.False);
            bridge.AlphaZeroIsPressed = false;
            Assert.That(controller.Tick(bridge), Is.False);
            bridge.AlphaZeroIsPressed = true;
            Assert.That(controller.Tick(bridge), Is.True);
            Assert.That(controller.Tick(bridge), Is.False);
            Assert.That(bridge.Balance, Is.EqualTo(2000));
        }

        [Test]
        public void TwoSeparatePressesCreateTwoUniqueTransactions()
        {
            var bridge = ReadyBridge(alpha: true);
            var controller = new ZeroCoinGrantController("test-session");

            Assert.That(controller.Tick(bridge), Is.True);
            bridge.AlphaZeroIsPressed = false;
            controller.Tick(bridge);
            bridge.AlphaZeroIsPressed = true;
            Assert.That(controller.Tick(bridge), Is.True);

            Assert.That(bridge.Balance, Is.EqualTo(2000));
            Assert.That(bridge.RequestIds, Has.Count.EqualTo(2));
            Assert.That(bridge.RequestIds[0], Is.Not.EqualTo(
                bridge.RequestIds[1]));
        }

        [TestCase(false, true, true, false, false, false, false)]
        [TestCase(true, false, true, false, false, false, false)]
        [TestCase(true, true, false, false, false, false, false)]
        [TestCase(true, true, true, true, false, false, false)]
        [TestCase(true, true, true, false, true, false, false)]
        [TestCase(true, true, true, false, false, true, false)]
        [TestCase(true, true, true, false, false, false, true)]
        public void InvalidContextNeverChangesBalance(
            bool playing,
            bool focused,
            bool allowedScreen,
            bool paused,
            bool duel,
            bool textInput,
            bool transactionBusy)
        {
            FakeBridge bridge = ReadyBridge(alpha: true);
            bridge.IsPlaying = playing;
            bridge.IsGameViewFocused = focused;
            bridge.IsAllowedScreen = allowedScreen;
            bridge.IsPaused = paused;
            bridge.IsInDuel = duel;
            bridge.IsTextInputFocused = textInput;
            bridge.IsTransactionBusy = transactionBusy;

            Assert.That(new ZeroCoinGrantController("test-session")
                .Tick(bridge), Is.False);
            Assert.That(bridge.Balance, Is.Zero);
        }

        [Test]
        public void WalletUnavailableFailsWithoutChangingBalance()
        {
            FakeBridge bridge = ReadyBridge(alpha: true);
            bridge.IsWalletReady = false;

            Assert.That(new ZeroCoinGrantController("test-session")
                .Tick(bridge), Is.False);
            Assert.That(bridge.Balance, Is.Zero);
            Assert.That(bridge.Notifications, Has.Some.Contains(
                "carteira"));
        }

        [Test]
        public void BusyControllerRejectsRecursiveGrant()
        {
            var bridge = ReadyBridge(alpha: true);
            var controller = new ZeroCoinGrantController("test-session");
            bridge.DuringGrant = () =>
                Assert.That(controller.Tick(bridge), Is.False);

            Assert.That(controller.Tick(bridge), Is.True);
            Assert.That(bridge.Amounts, Has.Count.EqualTo(1));
        }

        [Test]
        public void EditorBridgeUsesTheRealWalletAndLedger()
        {
            string directory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Library",
                "CodexTests");
            string savePath = Path.Combine(
                directory,
                "zero-coin-" + Guid.NewGuid().ToString("N") + ".json");
            GameObject root = null;
            try
            {
                UnityEngine.Object catalog = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/Cards/CardCatalog.asset");
                Assert.That(catalog, Is.Not.Null);

                Type frontendType = FindType(
                    "ArcaneArena.Frontend.GameFrontendBootstrap");
                Type repositoryType = FindType(
                    "ArcaneArena.Frontend.DeckRepository");
                root = new GameObject("Zero Coin Integration Test");
                root.SetActive(false);
                Component frontend = root.AddComponent(frontendType);
                object repository = Activator.CreateInstance(
                    repositoryType,
                    new object[] { savePath });
                repositoryType.GetMethod(
                        "Load",
                        new[] { catalog.GetType(), typeof(bool) })
                    .Invoke(repository, new[] { catalog, (object)false });

                SetField(frontend, "_catalog", catalog);
                SetField(frontend, "_repository", repository);
                SetField(frontend, "_deckEditorSelectedCardId", "35224440");

                Type bridgeType = typeof(ZeroCoinGrantController).Assembly
                    .GetType(
                        "ArcaneArena.Editor.DeveloperTools." +
                        "UnityEditorZeroCoinGrantBridge",
                        true);
                object bridge = Activator.CreateInstance(bridgeType, true);
                int before = (int)repositoryType.GetProperty("CoinBalance")
                    .GetValue(repository);
                object[] grant =
                {
                    1000,
                    ZeroCoinGrantController.RewardReason,
                    "editor-zero:integration:1",
                    0,
                    null
                };
                Assert.That(bridgeType.GetMethod("TryGrantCoins")
                    .Invoke(bridge, grant), Is.True);
                Assert.That(grant[3], Is.EqualTo(before + 1000));
                Assert.That(repositoryType.GetProperty("CoinBalance")
                    .GetValue(repository), Is.EqualTo(before + 1000));

                object state = repositoryType.GetProperty("State")
                    .GetValue(repository);
                FieldInfo transactions = state.GetType().GetField(
                    "processedShopTransactions");
                Assert.That(((System.Collections.IEnumerable)transactions
                        .GetValue(state)).Cast<object>()
                    .Any(record =>
                        string.Equals(
                            record.GetType().GetField("kind")?.GetValue(record)
                                as string,
                            "admin-test",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            record.GetType().GetField("productId")
                                ?.GetValue(record) as string,
                            ZeroCoinGrantController.RewardReason,
                            StringComparison.Ordinal) &&
                        (int)record.GetType().GetField("coinDelta")
                            .GetValue(record) == 1000),
                    Is.True);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
                if (File.Exists(savePath))
                    File.Delete(savePath);
                if (Directory.Exists(directory) &&
                    !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
        }

        private static FakeBridge ReadyBridge(
            bool alpha = false,
            bool numpad = false)
        {
            return new FakeBridge
            {
                IsPlaying = true,
                IsGameViewFocused = true,
                IsAllowedScreen = true,
                IsWalletReady = true,
                AlphaZeroIsPressed = alpha,
                NumpadZeroIsPressed = numpad
            };
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Tipo ausente: " + fullName);
            return type;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Campo ausente: " + name);
            field.SetValue(target, value);
        }

        private sealed class FakeBridge : IZeroCoinGrantBridge
        {
            public bool IsPlaying { get; set; }
            public bool IsPaused { get; set; }
            public bool IsGameViewFocused { get; set; }
            public bool AlphaZeroIsPressed { get; set; }
            public bool NumpadZeroIsPressed { get; set; }
            public bool IsAllowedScreen { get; set; }
            public bool IsInDuel { get; set; }
            public bool IsTextInputFocused { get; set; }
            public bool IsTransactionBusy { get; set; }
            public bool IsWalletReady { get; set; }
            public int Balance { get; private set; }
            public List<int> Amounts { get; } = new();
            public List<string> Reasons { get; } = new();
            public List<string> RequestIds { get; } = new();
            public List<string> Notifications { get; } = new();
            public System.Action DuringGrant { get; set; }

            public bool TryGrantCoins(
                int amount,
                string reason,
                string idempotencyKey,
                out int balanceAfter,
                out string rejection)
            {
                DuringGrant?.Invoke();
                Amounts.Add(amount);
                Reasons.Add(reason);
                RequestIds.Add(idempotencyKey);
                Balance += amount;
                balanceAfter = Balance;
                rejection = string.Empty;
                return true;
            }

            public void Notify(string message, bool error)
            {
                Notifications.Add(message);
            }
        }
    }
}
