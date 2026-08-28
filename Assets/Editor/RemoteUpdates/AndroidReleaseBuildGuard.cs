using System;
using System.IO;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArcaneDuel.Editor.RemoteUpdates
{
    /// <summary>
    /// The ordinary Unity Build button must never silently produce a debug
    /// APK that is later mistaken for an update.  All non-development Android
    /// builds receive the local production signing material or fail before
    /// Unity starts writing the package.
    /// </summary>
    public sealed class AndroidReleaseBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android ||
                (report.summary.options & BuildOptions.Development) != 0)
            {
                return;
            }

            try
            {
                ReleaseSigningConfiguration.ApplyAndroidSigning(true);

                if (!PlayerSettings.Android.useCustomKeystore ||
                    string.IsNullOrWhiteSpace(
                        PlayerSettings.Android.keystoreName) ||
                    !File.Exists(PlayerSettings.Android.keystoreName) ||
                    string.IsNullOrWhiteSpace(
                        PlayerSettings.Android.keyaliasName))
                {
                    throw new BuildFailedException(
                        "A build Android de release não recebeu um cofre " +
                        "de assinatura válido.");
                }

                string certificate =
                    ReleaseSigningConfiguration.AndroidCertificateSha256();
                if (certificate.Length != 64)
                {
                    throw new BuildFailedException(
                        "O certificado SHA-256 da build Android de release " +
                        "é inválido.");
                }

                if (PlayerSettings.Android.bundleVersionCode <= 0 ||
                    string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
                {
                    throw new BuildFailedException(
                        "Defina uma versão e um código Android positivos " +
                        "antes de criar a build de release.");
                }

                int publishedCode = ReadPublishedAndroidVersionCode();
                if (publishedCode > 0 &&
                    PlayerSettings.Android.bundleVersionCode <= publishedCode)
                {
                    throw new BuildFailedException(
                        "Esta build Android mantém o versionCode " +
                        PlayerSettings.Android.bundleVersionCode +
                        ", mas a versão publicada já usa " + publishedCode +
                        ". Use a Central de Publicação ou " +
                        "Build-And-Publish-Release.ps1 para incrementar " +
                        "a versão antes da build.");
                }
            }
            catch (BuildFailedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(
                    "A build Android de release foi bloqueada porque a " +
                    "assinatura não pôde ser verificada: " +
                    exception.GetBaseException().Message);
            }
        }

        private static int ReadPublishedAndroidVersionCode()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName ?? Application.dataPath;
            string path = Path.Combine(
                projectRoot,
                "ContentStaging",
                "production",
                "v2",
                "release-envelope.json");
            if (!File.Exists(path))
                return 0;

            RemoteReleaseEnvelope envelope = JsonUtility.FromJson<
                RemoteReleaseEnvelope>(File.ReadAllText(path));
            return Math.Max(0, envelope?.payload?.android?.versionCode ?? 0);
        }
    }
}
