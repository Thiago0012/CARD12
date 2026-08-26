using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Editor.RemoteUpdates
{
    [Serializable]
    internal sealed class LocalReleaseSecrets
    {
        public int schemaVersion = 1;
        public string manifestKeyId;
        public string manifestPrivateKeyPath;
        public string androidKeystorePath;
        public string androidKeystorePassword;
        public string androidAlias;
        public string androidAliasPassword;
        public string androidCertificateSha256;
    }

    [Serializable]
    internal sealed class PortableRsaPrivateKey
    {
        public string modulus;
        public string exponent;
        public string d;
        public string p;
        public string q;
        public string dp;
        public string dq;
        public string inverseQ;
    }

    /// <summary>
    /// Loads release secrets from an ignored local file. Private material and
    /// passwords are never serialized into Unity assets or publication files.
    /// </summary>
    internal static class ReleaseSigningConfiguration
    {
        private const string RelativeSecretsPath =
            ".release-secrets/release-secrets.json";

        internal static string SecretsPath => Path.Combine(
            ProjectRoot,
            RelativeSecretsPath.Replace('/', Path.DirectorySeparatorChar));

        internal static LocalReleaseSecrets LoadRequired()
        {
            if (!File.Exists(SecretsPath))
            {
                throw new FileNotFoundException(
                    "As chaves de publicação ainda não foram inicializadas. " +
                    "Execute Tools/RemoteUpdates/Initialize-ReleaseSecurity.ps1.",
                    SecretsPath);
            }
            LocalReleaseSecrets secrets = JsonUtility.FromJson<
                LocalReleaseSecrets>(File.ReadAllText(SecretsPath));
            if (secrets == null ||
                string.IsNullOrWhiteSpace(secrets.manifestPrivateKeyPath) ||
                string.IsNullOrWhiteSpace(secrets.androidKeystorePath))
            {
                throw new InvalidDataException(
                    "O arquivo local de segurança está incompleto.");
            }
            secrets.manifestPrivateKeyPath = ResolveProjectPath(
                secrets.manifestPrivateKeyPath);
            secrets.androidKeystorePath = ResolveProjectPath(
                secrets.androidKeystorePath);
            return secrets;
        }

        internal static void ApplyAndroidSigning(bool required)
        {
            LocalReleaseSecrets secrets;
            try
            {
                secrets = LoadRequired();
            }
            catch when (!required)
            {
                return;
            }

            if (!File.Exists(secrets.androidKeystorePath))
                throw new FileNotFoundException(
                    "O cofre Android definitivo não foi encontrado.",
                    secrets.androidKeystorePath);
            if (string.IsNullOrWhiteSpace(secrets.androidKeystorePassword) ||
                string.IsNullOrWhiteSpace(secrets.androidAlias) ||
                string.IsNullOrWhiteSpace(secrets.androidAliasPassword))
            {
                throw new InvalidDataException(
                    "As credenciais locais do cofre Android estão incompletas.");
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = secrets.androidKeystorePath;
            PlayerSettings.Android.keystorePass =
                secrets.androidKeystorePassword;
            PlayerSettings.Android.keyaliasName = secrets.androidAlias;
            PlayerSettings.Android.keyaliasPass = secrets.androidAliasPassword;
        }

        internal static void SignEnvelope(RemoteReleaseEnvelope envelope)
        {
            if (envelope?.payload == null)
                throw new ArgumentNullException(nameof(envelope));
            LocalReleaseSecrets secrets = LoadRequired();
            if (!File.Exists(secrets.manifestPrivateKeyPath))
                throw new FileNotFoundException(
                    "A chave privada do manifesto não foi encontrada.",
                    secrets.manifestPrivateKeyPath);

            PortableRsaPrivateKey key = JsonUtility.FromJson<
                PortableRsaPrivateKey>(File.ReadAllText(
                secrets.manifestPrivateKeyPath));
            if (key == null || string.IsNullOrWhiteSpace(key.d))
                throw new InvalidDataException(
                    "A chave privada portátil do manifesto é inválida.");
            using RSA rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Convert.FromBase64String(key.modulus),
                Exponent = Convert.FromBase64String(key.exponent),
                D = Convert.FromBase64String(key.d),
                P = Convert.FromBase64String(key.p),
                Q = Convert.FromBase64String(key.q),
                DP = Convert.FromBase64String(key.dp),
                DQ = Convert.FromBase64String(key.dq),
                InverseQ = Convert.FromBase64String(key.inverseQ)
            });
            byte[] payload = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(envelope.payload, false));
            envelope.keyId = string.IsNullOrWhiteSpace(secrets.manifestKeyId)
                ? "production-2026"
                : secrets.manifestKeyId.Trim();
            envelope.signatureBase64 = Convert.ToBase64String(rsa.SignData(
                payload,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1));
        }

        internal static string AndroidCertificateSha256()
        {
            return NormalizeHash(LoadRequired().androidCertificateSha256);
        }

        internal static string NormalizeHash(string value)
        {
            return (value ?? string.Empty)
                .Replace(":", string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private static string ResolveProjectPath(string path)
        {
            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }

        private static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            Application.dataPath;
    }
}
