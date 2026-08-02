using System;
using System.IO;

namespace ArcaneArena.Frontend
{
    public enum RewardReceiptStatus
    {
        Granted,
        AlreadyProcessed,
        BlockedNotAuthorized,
        BlockedRevoked,
        BlockedProfileMismatch,
        BlockedInstallationMismatch,
        BlockedInvalidMatch,
        BlockedOfflineMode,
        BlockedIntegrityFailure
    }

    public enum MatchRewardMode
    {
        Offline,
        OnlinePvP
    }

    [Serializable]
    public sealed class CoinRewardEligibilitySnapshot
    {
        public bool wasAuthorizedAtMatchStart;
        public string catalogEntryId;
        public string localProfileId;
        public string installId;
        public int catalogVersion;
        public RewardReceiptStatus blockedStatusAtMatchStart =
            RewardReceiptStatus.BlockedNotAuthorized;

        public static CoinRewardEligibilitySnapshot Blocked(
            string localProfileId,
            string installId,
            int catalogVersion,
            RewardReceiptStatus status)
        {
            return new CoinRewardEligibilitySnapshot
            {
                wasAuthorizedAtMatchStart = false,
                catalogEntryId = string.Empty,
                localProfileId = localProfileId ?? string.Empty,
                installId = installId ?? string.Empty,
                catalogVersion = Math.Max(0, catalogVersion),
                blockedStatusAtMatchStart = status
            };
        }
    }

    [Serializable]
    public sealed class MatchRewardRequest
    {
        public string matchId;
        public string localPlayerId;
        public string localProfileId;
        public MatchRewardMode mode;
        public bool isAuthoritativeFinal;
        public bool isWinner;
        public bool isDraw;
        public int totalOpponentDamage;
        public int completedRounds;
        public CoinRewardEligibilitySnapshot eligibilityAtMatchStart;
    }

    [Serializable]
    public sealed class RewardReceipt
    {
        public string transactionId;
        public string matchId;
        public RewardReceiptStatus status;
        public RewardReceiptStatus originalStatus;
        public int coins;
        public int balanceAfter;

        public bool WasGranted =>
            status == RewardReceiptStatus.Granted ||
            (status == RewardReceiptStatus.AlreadyProcessed &&
             originalStatus == RewardReceiptStatus.Granted);
    }

    public interface IInstallIdentityService
    {
        string GetOrCreateInstallId();
    }

    public interface IWalletService
    {
        bool TryGrantCoins(
            int amount,
            string reason,
            string idempotencyKey,
            out ShopTransactionRecord receipt,
            out string rejection);
    }

    public sealed class LocalInstallIdentityService : IInstallIdentityService
    {
        private readonly string _identityPath;
        private string _cachedInstallId;

        public LocalInstallIdentityService(string identityPath)
        {
            if (string.IsNullOrWhiteSpace(identityPath))
                throw new ArgumentException(
                    "O caminho da identidade local não pode ser vazio.",
                    nameof(identityPath));
            _identityPath = Path.GetFullPath(identityPath);
        }

        public string GetOrCreateInstallId()
        {
            if (IsValidGuid(_cachedInstallId))
                return _cachedInstallId;

            try
            {
                if (File.Exists(_identityPath))
                {
                    string persisted = File.ReadAllText(_identityPath).Trim();
                    if (IsValidGuid(persisted))
                    {
                        _cachedInstallId = persisted;
                        return _cachedInstallId;
                    }
                }
            }
            catch (IOException)
            {
                // A criação atômica abaixo tentará recuperar a identidade.
            }

            _cachedInstallId = Guid.NewGuid().ToString("N");
            string directory = Path.GetDirectoryName(_identityPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            string temporaryPath = _identityPath + ".tmp";
            File.WriteAllText(temporaryPath, _cachedInstallId);
            if (File.Exists(_identityPath))
            {
                File.Copy(temporaryPath, _identityPath, true);
                File.Delete(temporaryPath);
            }
            else
            {
                File.Move(temporaryPath, _identityPath);
            }
            return _cachedInstallId;
        }

        private static bool IsValidGuid(string value)
        {
            return Guid.TryParseExact(value, "N", out _);
        }
    }
}
