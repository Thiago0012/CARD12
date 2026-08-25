using System.IO;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class MultiplayerCrossplayEditModeTests
    {
        [Test]
        public void NativeCorePluginsCoverWindowsAndAndroidArm64()
        {
            const string androidPath =
                "Assets/Plugins/Android/arm64-v8a/libocgcore.so";
            const string windowsPath =
                "Assets/Plugins/Windows/x86_64/ocgcore.dll";
            Assert.That(File.Exists(androidPath), Is.True);
            Assert.That(File.Exists(windowsPath), Is.True);

            var android = AssetImporter.GetAtPath(androidPath) as
                PluginImporter;
            var windows = AssetImporter.GetAtPath(windowsPath) as
                PluginImporter;
            Assert.That(android, Is.Not.Null);
            Assert.That(windows, Is.Not.Null);
            Assert.That(
                android.GetCompatibleWithPlatform(BuildTarget.Android),
                Is.True);
            Assert.That(
                android.GetCompatibleWithPlatform(
                    BuildTarget.StandaloneWindows64),
                Is.False);
            Assert.That(
                windows.GetCompatibleWithPlatform(
                    BuildTarget.StandaloneWindows64),
                Is.True);
            Assert.That(
                windows.GetCompatibleWithPlatform(BuildTarget.Android),
                Is.False);
        }

        [Test]
        public void MultiplayerCompatibilityPinsCoreAndCardContent()
        {
            string compatibility =
                ProjectIdentity.MultiplayerCompatibility;
            Assert.That(
                compatibility,
                Does.Contain(ProjectIdentity.ProjectVersion));
            Assert.That(
                compatibility,
                Does.Contain(ProjectIdentity.CoreCommit));
            Assert.That(
                compatibility,
                Does.Contain(ProjectIdentity.CardScriptsCommit));
            Assert.That(
                compatibility,
                Does.Contain(ProjectIdentity.BabelCdbCommit));
            Assert.That(
                compatibility,
                Does.Contain(BanlistService.ActiveBanlistId));
        }

        [TestCase(RuntimePlatform.Android)]
        [TestCase(RuntimePlatform.WindowsPlayer)]
        [TestCase(RuntimePlatform.WindowsEditor)]
        [TestCase(RuntimePlatform.IPhonePlayer)]
        public void RelayPolicyUsesFirewallSafeEncryptedWebSockets(
            RuntimePlatform platform)
        {
            Assert.That(
                SelectRelayProtocol(platform),
                Is.EqualTo("WSS"));
            Assert.That(
                RelayProtocolRequiresWebSockets(),
                Is.True);
        }

        [Test]
        public void InstalledProductIdentityUsesCurrentGameName()
        {
            Assert.That(
                ProjectIdentity.ProductName,
                Is.EqualTo("Master Duel 2 Plus Ultra"));
            Assert.That(
                PlayerSettings.productName,
                Is.EqualTo(ProjectIdentity.ProductName));
        }

        private static string SelectRelayProtocol(RuntimePlatform platform)
        {
            foreach (System.Reflection.Assembly assembly in
                     System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type policy = assembly.GetType(
                    "ArcaneArena.Multiplayer.RelayTransportPolicy");
                if (policy == null)
                    continue;
                object selected = policy.GetMethod("Select")?.Invoke(
                    null,
                    new object[] { platform });
                return selected?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        private static bool RelayProtocolRequiresWebSockets()
        {
            foreach (System.Reflection.Assembly assembly in
                     System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type policy = assembly.GetType(
                    "ArcaneArena.Multiplayer.RelayTransportPolicy");
                if (policy == null)
                    continue;
                object selected = policy.GetMethod("Select")?.Invoke(
                    null,
                    new object[] { RuntimePlatform.Android });
                object requiresWebSockets = policy
                    .GetMethod("RequiresWebSockets")
                    ?.Invoke(null, new[] { selected });
                return requiresWebSockets is true;
            }
            return false;
        }

    }
}
