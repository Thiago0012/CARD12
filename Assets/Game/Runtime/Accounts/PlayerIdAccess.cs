using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ArcaneDuel.Game.Accounts
{
    /// <summary>
    /// Chaves estáveis que podem ser bloqueadas individualmente para um ID.
    /// Elas também formam o contrato compartilhado entre o jogo e o backend.
    /// </summary>
    public static class PlayerIdCapability
    {
        public const string Game = "game";
        public const string Online = "online";
        public const string Ranked = "ranked";
        public const string Economy = "economy";
    }

    public static class PlayerIdFeature
    {
        public const string ExclusiveAccountContent =
            "exclusive-account-content";

        // Liberação individual para os três emblemas cuja moldura possui
        // animação própria. Não representa cargo: o catálogo concede a chave
        // somente ao ID numérico autorizado.
        public const string ExclusiveAnimatedProfileIcons =
            "exclusive-animated-profile-icons";
    }

    /// <summary>
    /// Registro devolvido pelo catálogo autoritativo para o ID autenticado.
    /// Não há cargos: liberações e bloqueios pertencem diretamente ao ID.
    /// </summary>
    [Serializable]
    public sealed class PlayerIdAccessSnapshot
    {
        public int schemaVersion = 1;
        public string playerId;
        public string publicId;
        public bool blockGameAccess;
        public List<string> blockedCapabilities = new List<string>();
        public List<string> grantedFeatures = new List<string>();
        public string message;
        public long firstSeenUtcUnixSeconds;
        public long lastSeenUtcUnixSeconds;
        public long validUntilUtcUnixSeconds;

        [NonSerialized]
        public bool serverVerified;

        public void Normalize()
        {
            schemaVersion = Math.Max(1, schemaVersion);
            playerId = (playerId ?? string.Empty).Trim();
            publicId = PlayerIdAccessPolicy.NormalizePublicId(
                publicId,
                playerId);
            blockedCapabilities ??= new List<string>();
            grantedFeatures ??= new List<string>();
            NormalizeKeys(blockedCapabilities);
            NormalizeKeys(grantedFeatures);
            message = (message ?? string.Empty).Trim();
            firstSeenUtcUnixSeconds = Math.Max(0, firstSeenUtcUnixSeconds);
            lastSeenUtcUnixSeconds = Math.Max(0, lastSeenUtcUnixSeconds);
            validUntilUtcUnixSeconds = Math.Max(0, validUntilUtcUnixSeconds);
        }

        public PlayerIdAccessSnapshot Copy()
        {
            Normalize();
            return new PlayerIdAccessSnapshot
            {
                schemaVersion = schemaVersion,
                playerId = playerId,
                publicId = publicId,
                blockGameAccess = blockGameAccess,
                blockedCapabilities = new List<string>(blockedCapabilities),
                grantedFeatures = new List<string>(grantedFeatures),
                message = message,
                firstSeenUtcUnixSeconds = firstSeenUtcUnixSeconds,
                lastSeenUtcUnixSeconds = lastSeenUtcUnixSeconds,
                validUntilUtcUnixSeconds = validUntilUtcUnixSeconds,
                serverVerified = serverVerified
            };
        }

        private static void NormalizeKeys(List<string> keys)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = keys.Count - 1; index >= 0; index--)
            {
                string normalized = (keys[index] ?? string.Empty)
                    .Trim()
                    .ToLowerInvariant();
                if (normalized.Length == 0 || !unique.Add(normalized))
                    keys.RemoveAt(index);
                else
                    keys[index] = normalized;
            }
            keys.Sort(StringComparer.Ordinal);
        }
    }

    public static class PlayerIdAccessPolicy
    {
        public const int PublicIdLength = 12;
        public const int PublicProfileSchemaVersion = 1;

        /// <summary>
        /// A tela de login ainda não carregou o save autenticado. Nessa fase o
        /// catálogo recebe versão zero e preserva o perfil público já salvo,
        /// em vez de substituir o ícone real pelo brasão padrão de bootstrap.
        /// </summary>
        public static int PublicProfileUploadSchemaVersion(
            bool authenticatedProfileLoaded) =>
            authenticatedProfileLoaded ? PublicProfileSchemaVersion : 0;

        /// <summary>
        /// Converte a revisão persistente do save para milissegundos Unix,
        /// mantendo o número dentro da faixa inteira segura do JavaScript.
        /// </summary>
        public static long PublicProfileRevisionUtcMilliseconds(
            long lastModifiedUtcTicks)
        {
            long unixTicks = lastModifiedUtcTicks - DateTime.UnixEpoch.Ticks;
            return unixTicks <= 0
                ? 0
                : unixTicks / TimeSpan.TicksPerMillisecond;
        }

        public static PlayerIdAccessSnapshot CreateUnverifiedFallback(
            string playerId)
        {
            var snapshot = new PlayerIdAccessSnapshot
            {
                playerId = playerId ?? string.Empty,
                serverVerified = false
            };
            snapshot.Normalize();
            return snapshot;
        }

        public static bool AllowsStandardCapability(
            PlayerIdAccessSnapshot snapshot,
            string capability,
            bool allowWhenUnverified = true)
        {
            string key = NormalizeKey(capability);
            if (key.Length == 0)
                return false;
            if (snapshot == null)
                return allowWhenUnverified;

            snapshot.Normalize();
            if (!snapshot.serverVerified)
                return allowWhenUnverified;
            if (snapshot.blockGameAccess)
                return false;
            return !Contains(snapshot.blockedCapabilities, "*") &&
                   !Contains(snapshot.blockedCapabilities, key);
        }

        public static bool HasGrantedFeature(
            PlayerIdAccessSnapshot snapshot,
            string feature)
        {
            string key = NormalizeKey(feature);
            if (snapshot == null || key.Length == 0)
                return false;

            snapshot.Normalize();
            return snapshot.serverVerified &&
                   !snapshot.blockGameAccess &&
                   Contains(snapshot.grantedFeatures, key);
        }

        public static string FormatPublicId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return string.Empty;

            byte[] source = Encoding.UTF8.GetBytes(playerId.Trim());
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
                digest = sha256.ComputeHash(source);

            ulong seed = 0;
            for (int index = 0; index < sizeof(ulong); index++)
                seed = (seed << 8) | digest[index];

            const ulong minimum = 100000000000UL;
            const ulong range = 900000000000UL;
            ulong numericId = minimum + seed % range;
            return numericId.ToString(
                "D" + PublicIdLength,
                CultureInfo.InvariantCulture);
        }

        public static string NormalizePublicId(
            string proposedPublicId,
            string canonicalPlayerId)
        {
            string normalized = (proposedPublicId ?? string.Empty).Trim();
            if (IsValidPublicId(normalized))
                return normalized;
            return FormatPublicId(canonicalPlayerId);
        }

        public static bool IsValidPublicId(string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId) ||
                publicId.Length != PublicIdLength)
            {
                return false;
            }

            foreach (char character in publicId)
            {
                if (character < '0' || character > '9')
                    return false;
            }
            return true;
        }

        private static bool Contains(List<string> values, string key)
        {
            return values != null && values.Exists(value =>
                string.Equals(value, key, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
