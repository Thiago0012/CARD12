using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository : IWalletService
    {
        public const int OnlineRewardRuleVersion = 2;
        private const string AuthorizationIntegrityPepper =
            "ArcaneArena.LocalCoinAuthorization.v1.2026-08-02";

        private AuthorizedCoinRecipientsCatalog _authorizedRecipientsCatalog;
        private IInstallIdentityService _installIdentityService;

        public CoinRewardAuthorizationState CoinRewardAuthorization =>
            State?.coinRewardAuthorization;

        public void ConfigureCoinRewardAuthorization(
            AuthorizedCoinRecipientsCatalog catalog,
            IInstallIdentityService installIdentityService = null)
        {
            _authorizedRecipientsCatalog = catalog;
            _installIdentityService = installIdentityService ??
                new LocalInstallIdentityService(Path.Combine(
                    Path.GetDirectoryName(_savePath) ?? string.Empty,
                    "install.identity"));
            if (State == null)
                return;

            NormalizeCoinRewardAuthorizationState(State.schemaVersion);
            TryBindConfiguredNickname();
        }

        private void NormalizeCoinRewardAuthorizationState(
            int loadedSchemaVersion)
        {
            State.coinRewardAuthorization ??=
                new CoinRewardAuthorizationState();
            if (loadedSchemaVersion < 5)
            {
                // Migração não destrutiva: somente a autorização começa vazia.
                State.coinRewardAuthorization =
                    new CoinRewardAuthorizationState();
            }
        }

        private void TryBindConfiguredNickname()
        {
            if (State == null || _authorizedRecipientsCatalog == null ||
                _installIdentityService == null ||
                string.IsNullOrWhiteSpace(State.playerDisplayName))
            {
                return;
            }

            TryBindCurrentNickname(out _);
        }

        public bool TryBindCurrentNickname(
            out RewardReceiptStatus status)
        {
            status = RewardReceiptStatus.BlockedNotAuthorized;
            if (State == null || _authorizedRecipientsCatalog == null ||
                _installIdentityService == null)
            {
                status = RewardReceiptStatus.BlockedInvalidMatch;
                return false;
            }

            CoinRewardAuthorizationState state =
                State.coinRewardAuthorization ??=
                    new CoinRewardAuthorizationState();
            string installId;
            try
            {
                installId = _installIdentityService.GetOrCreateInstallId();
            }
            catch (Exception)
            {
                status = RewardReceiptStatus.BlockedInstallationMismatch;
                return false;
            }

            if (state.isAuthorized)
            {
                status = ValidateExistingBinding(
                    state,
                    State.localProfileId,
                    installId,
                    persistRevocation: true);
                return status == RewardReceiptStatus.Granted;
            }

            if (!_authorizedRecipientsCatalog.TryFindActive(
                    State.playerDisplayName,
                    out AuthorizedRecipientEntry entry))
            {
                return false;
            }

            state.isAuthorized = true;
            state.isRevoked = false;
            state.catalogEntryId = entry.EntryId;
            state.originallyAuthorizedNickname = State.playerDisplayName;
            state.normalizedAuthorizedNickname = entry.NormalizedNickname;
            state.boundLocalProfileId = State.localProfileId;
            state.boundInstallId = installId;
            state.authorizedAtUtcUnixSeconds =
                DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            state.catalogVersionAtAuthorization =
                _authorizedRecipientsCatalog.CatalogVersion;
            state.integrityTag = ComputeAuthorizationIntegrityTag(state);
            Save();
            status = RewardReceiptStatus.Granted;
            return true;
        }

        public CoinRewardEligibilitySnapshot CaptureOnlineRewardEligibility()
        {
            string profileId = State?.localProfileId ?? string.Empty;
            int catalogVersion =
                _authorizedRecipientsCatalog?.CatalogVersion ?? 0;
            string installId = string.Empty;
            try
            {
                installId = _installIdentityService?.GetOrCreateInstallId() ??
                    string.Empty;
            }
            catch (Exception)
            {
                return CoinRewardEligibilitySnapshot.Blocked(
                    profileId,
                    string.Empty,
                    catalogVersion,
                    RewardReceiptStatus.BlockedInstallationMismatch);
            }

            if (!TryBindCurrentNickname(out RewardReceiptStatus status))
            {
                return CoinRewardEligibilitySnapshot.Blocked(
                    profileId,
                    installId,
                    catalogVersion,
                    status);
            }

            CoinRewardAuthorizationState state =
                State.coinRewardAuthorization;
            return new CoinRewardEligibilitySnapshot
            {
                wasAuthorizedAtMatchStart = true,
                catalogEntryId = state.catalogEntryId ?? string.Empty,
                localProfileId = profileId,
                installId = installId,
                catalogVersion = catalogVersion,
                blockedStatusAtMatchStart = RewardReceiptStatus.Granted
            };
        }

        public bool TryClaimOnlineDuelReward(
            MatchRewardRequest request,
            out RewardReceipt receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            if (State == null)
            {
                rejection = "O perfil local ainda não foi carregado.";
                return false;
            }
            if (request == null ||
                string.IsNullOrWhiteSpace(request.matchId) ||
                string.IsNullOrWhiteSpace(request.localPlayerId))
            {
                rejection = "A recompensa exige uma partida e um jogador local válidos.";
                return false;
            }

            string transactionId = BuildOnlineRewardTransactionId(
                request.matchId,
                request.localPlayerId);
            ShopTransactionRecord existing = FindTransaction(transactionId);
            if (existing != null)
            {
                if (!string.Equals(existing.kind, "online-pvp-reward",
                        StringComparison.Ordinal))
                {
                    rejection = "O ID da recompensa já pertence a outra transação.";
                    return false;
                }
                receipt = ToAlreadyProcessedReceipt(existing);
                return true;
            }

            RewardReceiptStatus status = ValidateRewardEnvelope(request);
            if (status == RewardReceiptStatus.Granted)
                status = ValidateEligibilitySnapshot(
                    request.eligibilityAtMatchStart,
                    request.localProfileId);

            int coins = status == RewardReceiptStatus.Granted
                ? OnlineDuelCoinReward.Calculate(
                    request.totalOpponentDamage,
                    request.completedRounds,
                    request.isWinner,
                    request.isDraw)
                : 0;

            string stateSnapshot = JsonUtility.ToJson(State);
            try
            {
                State.coinBalance = checked(State.coinBalance + coins);
                ShopTransactionRecord record = CreateTransaction(
                    transactionId,
                    "online-pvp-reward",
                    string.Empty,
                    coins,
                    Array.Empty<string>());
                record.matchId = request.matchId;
                record.localPlayerId = request.localPlayerId;
                record.localProfileId = request.localProfileId;
                record.catalogEntryId =
                    request.eligibilityAtMatchStart?.catalogEntryId ??
                    string.Empty;
                record.rewardRuleVersion = OnlineRewardRuleVersion;
                record.rewardStatus = status;
                record.damageDealt = Math.Max(0, request.totalOpponentDamage);
                record.completedRounds = Math.Max(0, request.completedRounds);
                record.winner = request.isWinner;
                record.draw = request.isDraw;
                State.processedShopTransactions.Add(record);
                RecordEligibleMissionCoins(
                    transactionId,
                    coins,
                    true,
                    false,
                    false);
                Save();
                receipt = ToRewardReceipt(record, status);
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(stateSnapshot);
                rejection = "A recompensa não foi gravada: " +
                    exception.GetBaseException().Message;
                return false;
            }
        }

        public bool TryGrantCoins(
            int amount,
            string reason,
            string idempotencyKey,
            out ShopTransactionRecord receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            string normalizedReason = string.IsNullOrWhiteSpace(reason)
                ? "Admin/Test"
                : reason.Trim();
            if (amount <= 0)
            {
                rejection = "A concessão de teste deve ser positiva.";
                return false;
            }
            if (!TryPrepareTransaction(
                    idempotencyKey,
                    "admin-test",
                    normalizedReason,
                    out receipt,
                    out rejection))
            {
                return receipt != null;
            }

            string stateSnapshot = JsonUtility.ToJson(State);
            try
            {
                State.coinBalance = checked(State.coinBalance + amount);
                receipt = CreateTransaction(
                    idempotencyKey,
                    "admin-test",
                    normalizedReason,
                    amount,
                    Array.Empty<string>());
                receipt.rewardStatus = RewardReceiptStatus.Granted;
                State.processedShopTransactions.Add(receipt);
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(stateSnapshot);
                receipt = null;
                rejection = "A concessão de teste não foi gravada: " +
                    exception.GetBaseException().Message;
                return false;
            }
        }

        public static string BuildOnlineRewardTransactionId(
            string matchId,
            string localPlayerId)
        {
            return $"{matchId}:online-pvp-reward:v{OnlineRewardRuleVersion}:" +
                   localPlayerId;
        }

        public static string RewardStatusMessage(RewardReceiptStatus status)
        {
            return status switch
            {
                RewardReceiptStatus.Granted => "Recompensa online concedida.",
                RewardReceiptStatus.AlreadyProcessed =>
                    "A recompensa desta partida já foi processada.",
                RewardReceiptStatus.BlockedNotAuthorized =>
                    "Perfil não autorizado para recompensas online.",
                RewardReceiptStatus.BlockedRevoked =>
                    "A autorização de recompensa foi revogada.",
                RewardReceiptStatus.BlockedProfileMismatch =>
                    "A autorização pertence a outro perfil local.",
                RewardReceiptStatus.BlockedInstallationMismatch =>
                    "A autorização pertence a outra instalação.",
                RewardReceiptStatus.BlockedOfflineMode =>
                    "Partidas offline não concedem moedas.",
                RewardReceiptStatus.BlockedIntegrityFailure =>
                    "A autorização local não passou na verificação de integridade.",
                _ => "A partida não é válida para recompensa online."
            };
        }

        private RewardReceiptStatus ValidateRewardEnvelope(
            MatchRewardRequest request)
        {
            if (request.mode != MatchRewardMode.OnlinePvP)
                return RewardReceiptStatus.BlockedOfflineMode;
            if (!request.isAuthoritativeFinal ||
                request.matchId.Length > 128 ||
                request.localPlayerId.Length > 64)
            {
                return RewardReceiptStatus.BlockedInvalidMatch;
            }
            if (string.IsNullOrWhiteSpace(request.localProfileId) ||
                !string.Equals(
                    request.localProfileId,
                    State.localProfileId,
                    StringComparison.Ordinal))
            {
                return RewardReceiptStatus.BlockedProfileMismatch;
            }
            return RewardReceiptStatus.Granted;
        }

        private RewardReceiptStatus ValidateEligibilitySnapshot(
            CoinRewardEligibilitySnapshot snapshot,
            string localProfileId)
        {
            if (snapshot == null)
                return RewardReceiptStatus.BlockedInvalidMatch;
            if (!snapshot.wasAuthorizedAtMatchStart)
                return NormalizeBlockedStatus(snapshot.blockedStatusAtMatchStart);
            if (!string.Equals(snapshot.localProfileId, localProfileId,
                    StringComparison.Ordinal))
            {
                return RewardReceiptStatus.BlockedProfileMismatch;
            }

            string currentInstallId;
            try
            {
                currentInstallId =
                    _installIdentityService?.GetOrCreateInstallId() ??
                    string.Empty;
            }
            catch (Exception)
            {
                return RewardReceiptStatus.BlockedInstallationMismatch;
            }
            if (string.IsNullOrWhiteSpace(currentInstallId) ||
                !string.Equals(snapshot.installId, currentInstallId,
                    StringComparison.Ordinal))
            {
                return RewardReceiptStatus.BlockedInstallationMismatch;
            }

            CoinRewardAuthorizationState state =
                State.coinRewardAuthorization;
            if (state == null || !state.isAuthorized)
                return RewardReceiptStatus.BlockedNotAuthorized;
            if (!string.Equals(
                    state.catalogEntryId,
                    snapshot.catalogEntryId,
                    StringComparison.Ordinal))
            {
                return RewardReceiptStatus.BlockedInvalidMatch;
            }
            return ValidateExistingBinding(
                state,
                localProfileId,
                currentInstallId,
                persistRevocation: true);
        }

        private RewardReceiptStatus ValidateExistingBinding(
            CoinRewardAuthorizationState state,
            string localProfileId,
            string installId,
            bool persistRevocation)
        {
            if (state == null || !state.isAuthorized)
                return RewardReceiptStatus.BlockedNotAuthorized;
            if (!HasValidAuthorizationIntegrityTag(state))
                return RewardReceiptStatus.BlockedIntegrityFailure;
            if (!string.Equals(state.boundLocalProfileId, localProfileId,
                    StringComparison.Ordinal))
            {
                return RewardReceiptStatus.BlockedProfileMismatch;
            }
            if (!string.Equals(state.boundInstallId, installId,
                    StringComparison.Ordinal))
            {
                return RewardReceiptStatus.BlockedInstallationMismatch;
            }

            AuthorizedRecipientEntry entry =
                _authorizedRecipientsCatalog?.FindByEntryId(
                    state.catalogEntryId);
            if (state.isRevoked ||
                entry?.Status == AuthorizedRecipientStatus.Revoked)
            {
                if (!state.isRevoked && persistRevocation)
                {
                    state.isRevoked = true;
                    state.integrityTag =
                        ComputeAuthorizationIntegrityTag(state);
                    Save();
                }
                return RewardReceiptStatus.BlockedRevoked;
            }

            // Entrada ausente ou Disabled preserva um binding antigo.
            return RewardReceiptStatus.Granted;
        }

        private static RewardReceiptStatus NormalizeBlockedStatus(
            RewardReceiptStatus status)
        {
            return status == RewardReceiptStatus.Granted ||
                   status == RewardReceiptStatus.AlreadyProcessed
                ? RewardReceiptStatus.BlockedNotAuthorized
                : status;
        }

        private static RewardReceipt ToRewardReceipt(
            ShopTransactionRecord record,
            RewardReceiptStatus status)
        {
            return new RewardReceipt
            {
                transactionId = record.transactionId,
                matchId = record.matchId,
                status = status,
                originalStatus = status,
                coins = Math.Max(0, record.coinDelta),
                balanceAfter = Math.Max(0, record.balanceAfter)
            };
        }

        private static RewardReceipt ToAlreadyProcessedReceipt(
            ShopTransactionRecord record)
        {
            return new RewardReceipt
            {
                transactionId = record.transactionId,
                matchId = record.matchId,
                status = RewardReceiptStatus.AlreadyProcessed,
                originalStatus = record.rewardStatus,
                coins = Math.Max(0, record.coinDelta),
                balanceAfter = Math.Max(0, record.balanceAfter)
            };
        }

        private static string ComputeAuthorizationIntegrityTag(
            CoinRewardAuthorizationState state)
        {
            string payload = string.Join("\u001f", new[]
            {
                AuthorizationIntegrityPepper,
                state.isAuthorized ? "1" : "0",
                state.isRevoked ? "1" : "0",
                state.catalogEntryId ?? string.Empty,
                state.originallyAuthorizedNickname ?? string.Empty,
                state.normalizedAuthorizedNickname ?? string.Empty,
                state.boundLocalProfileId ?? string.Empty,
                state.boundInstallId ?? string.Empty,
                state.authorizedAtUtcUnixSeconds.ToString(),
                state.catalogVersionAtAuthorization.ToString()
            });
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static bool HasValidAuthorizationIntegrityTag(
            CoinRewardAuthorizationState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.integrityTag))
                return false;
            string expected = ComputeAuthorizationIntegrityTag(state);
            return string.Equals(
                expected,
                state.integrityTag,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
