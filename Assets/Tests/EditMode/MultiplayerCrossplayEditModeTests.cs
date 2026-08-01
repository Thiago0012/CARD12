using System.IO;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEditor;

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
        }
    }
}
