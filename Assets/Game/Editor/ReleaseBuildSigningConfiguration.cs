using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Editor
{
    [Serializable]
    internal sealed class ReleaseBuildSecrets
    {
        public string androidKeystorePath;
        public string androidKeystorePassword;
        public string androidAlias;
        public string androidAliasPassword;
    }

    /// <summary>
    /// Applies the ignored production keystore to builds made by the game's
    /// editor assembly. This assembly cannot reference Assembly-CSharp-Editor.
    /// </summary>
    internal static class ReleaseBuildSigningConfiguration
    {
        internal static void ApplyAndroidSigning(bool required)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName ??
                Application.dataPath;
            string configPath = Path.Combine(
                projectRoot,
                ".release-secrets",
                "release-secrets.json");
            if (!File.Exists(configPath))
            {
                if (required)
                    throw new FileNotFoundException(
                        "As chaves de release Android não foram inicializadas.",
                        configPath);
                return;
            }

            ReleaseBuildSecrets secrets = JsonUtility.FromJson<
                ReleaseBuildSecrets>(File.ReadAllText(configPath));
            if (secrets == null)
                throw new InvalidDataException(
                    "A configuração local de assinatura é inválida.");
            string keystore = Path.IsPathRooted(secrets.androidKeystorePath)
                ? secrets.androidKeystorePath
                : Path.Combine(projectRoot, secrets.androidKeystorePath);
            keystore = Path.GetFullPath(keystore);
            if (!File.Exists(keystore))
                throw new FileNotFoundException(
                    "O cofre Android definitivo não foi encontrado.",
                    keystore);
            if (string.IsNullOrWhiteSpace(secrets.androidKeystorePassword) ||
                string.IsNullOrWhiteSpace(secrets.androidAlias) ||
                string.IsNullOrWhiteSpace(secrets.androidAliasPassword))
            {
                throw new InvalidDataException(
                    "As credenciais Android locais estão incompletas.");
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass =
                secrets.androidKeystorePassword;
            PlayerSettings.Android.keyaliasName = secrets.androidAlias;
            PlayerSettings.Android.keyaliasPass = secrets.androidAliasPassword;
        }
    }
}
