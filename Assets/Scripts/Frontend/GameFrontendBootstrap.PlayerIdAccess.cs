using System;
using System.Threading.Tasks;
using ArcaneDuel.Game.Accounts;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private bool _playerIdAccessScreenVisible;
        private bool _publicProfileRefreshRunning;
        private bool _publicProfileRefreshQueued;
        private bool _publicProfileReadyForUpload;

        private void InitializePlayerIdAccess()
        {
            PlayerIdAccessRuntime.AccessChanged += ApplyPlayerIdAccess;
            PlayerFriendsRuntime.Changed += HandleFriendsRuntimeChanged;
            FriendDuelChallengeRuntime.Changed +=
                HandleFriendDuelChallengeChanged;
            if (_repository != null)
            {
                _repository.LocalSaveCommitted -= HandlePublicProfileChanged;
                _repository.LocalSaveCommitted += HandlePublicProfileChanged;
            }
            SyncPublicProfileSnapshot();
            PlayerFriendsRuntime.SetLocalDisplayName(
                _repository?.PlayerDisplayName);
            _ = BindPlayerIdWhenReadyAsync();
            _ = PlayerFriendsRuntime.EnsureReadyAsync();
            _ = FriendDuelChallengeRuntime.EnsureReadyAsync();
        }

        private async Task BindPlayerIdWhenReadyAsync()
        {
            try
            {
                PlayerIdAccessSnapshot snapshot =
                    await PlayerIdAccessRuntime.EnsureReadyAsync();
                if (this != null && snapshot != null)
                {
                    ApplyPlayerIdAccess(snapshot);
                    try
                    {
                        await PlayerCloudSaveRuntime.EnsureSynchronizedAsync();
                        if (this == null)
                            return;
                        PlayerIdAccessRuntime.SetPlayerDisplayName(
                            _repository?.PlayerDisplayName);
                        SyncPublicProfileSnapshot(true);
                        PlayerFriendsRuntime.SetLocalDisplayName(
                            _repository?.PlayerDisplayName);
                        await PlayerIdAccessRuntime.RefreshNowAsync();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "O nome público será enviado ao catálogo no próximo " +
                            "heartbeat: " +
                            exception.GetBaseException().Message);
                    }
                }
            }
            finally
            {
                if (this != null)
                {
                    bool revealAfterBootstrap = _accountBootstrapPending;
                    _accountBootstrapPending = false;
                    if (revealAfterBootstrap)
                        _accountSessionBootstrapCompleted = true;
                    if (revealAfterBootstrap &&
                        !IsDuelSceneName(
                            UnityEngine.SceneManagement.SceneManager
                                .GetActiveScene().name) &&
                        !_playerIdAccessScreenVisible)
                    {
                        InitializeScenePresentation();
                    }
                }
            }
        }

        private void ApplyPlayerIdAccess(PlayerIdAccessSnapshot snapshot)
        {
            if (snapshot == null || this == null)
                return;

            _repository?.SynchronizeExclusiveProfileIconEntitlements();
            PlayerIdAccessRuntime.SetPlayerDisplayName(
                _repository?.PlayerDisplayName);
            SyncPublicProfileSnapshot();
            PlayerFriendsRuntime.SetLocalDisplayName(
                _repository?.PlayerDisplayName);

            string bindingRejection = string.Empty;
            bool accountBound = _repository != null &&
                !string.IsNullOrWhiteSpace(snapshot.playerId) &&
                _repository.TryBindAuthenticatedPlayerId(
                    snapshot.playerId,
                    out bindingRejection);
            if (_repository != null &&
                !string.IsNullOrWhiteSpace(snapshot.playerId) &&
                !accountBound)
            {
                Debug.LogWarning("[ID da conta] " + bindingRejection);
            }
            else if (accountBound)
            {
                string publicId = PlayerIdAccessPolicy.NormalizePublicId(
                    snapshot.publicId,
                    snapshot.playerId);
                if (DeveloperAccountRegistry.IsDeveloperPublicId(publicId) &&
                    !_repository.TryGrantCoins(
                        5000,
                        "Crédito único da conta de desenvolvimento",
                        "dev-entitlement:coins:v1:" + publicId,
                        out _,
                        out string grantRejection))
                {
                    Debug.LogWarning(
                        "[Conta de desenvolvimento] " + grantRejection);
                }
            }

            if (snapshot.serverVerified &&
                !PlayerIdAccessPolicy.AllowsStandardCapability(
                    snapshot,
                    PlayerIdCapability.Game,
                    false))
            {
                ShowPlayerIdAccessBlocked(snapshot.message);
                return;
            }

            if (_playerIdAccessScreenVisible)
            {
                _playerIdAccessScreenVisible = false;
                InitializeScenePresentation();
            }
        }

        private void ShowPlayerIdAccessBlocked(string message)
        {
            _playerIdAccessScreenVisible = true;
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("CONTROLE DE ACESSO");
            CreateText(
                _screenRoot,
                "ACESSO INDISPONÍVEL",
                36,
                FontStyle.Bold,
                Danger,
                new Vector2(0.16f, 0.58f),
                new Vector2(0.84f, 0.72f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                string.IsNullOrWhiteSpace(message)
                    ? "Este ID não possui acesso ao jogo neste momento."
                    : message,
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.18f, 0.41f),
                new Vector2(0.82f, 0.58f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                "ID DA CONTA  " + PlayerIdAccessRuntime.PublicPlayerId,
                15,
                FontStyle.Bold,
                Muted,
                new Vector2(0.20f, 0.34f),
                new Vector2(0.80f, 0.41f),
                TextAnchor.MiddleCenter);
            CreateButton(
                _screenRoot,
                "VERIFICAR NOVAMENTE",
                new Vector2(0.34f, 0.21f),
                new Vector2(0.66f, 0.30f),
                Cyan,
                RetryPlayerIdAccess);
        }

        private async void RetryPlayerIdAccess()
        {
            try
            {
                await PlayerIdAccessRuntime.RefreshNowAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Não foi possível atualizar o controle deste ID: " +
                    exception.GetBaseException().Message);
            }
        }

        private void ShowPlayerIdCapabilityBlocked(string rejection)
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("CONTROLE POR ID");
            CreateText(
                _screenRoot,
                "RECURSO LIMITADO",
                34,
                FontStyle.Bold,
                Danger,
                new Vector2(0.15f, 0.56f),
                new Vector2(0.85f, 0.70f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                rejection,
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.18f, 0.40f),
                new Vector2(0.82f, 0.56f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                "ID DA CONTA  " + PlayerIdAccessRuntime.PublicPlayerId,
                15,
                FontStyle.Bold,
                Muted,
                new Vector2(0.20f, 0.33f),
                new Vector2(0.80f, 0.40f),
                TextAnchor.MiddleCenter);
            CreateButton(
                _screenRoot,
                "VOLTAR",
                new Vector2(0.38f, 0.21f),
                new Vector2(0.62f, 0.30f),
                Cyan,
                ShowMainMenu);
        }

        private void ReleasePlayerIdAccess()
        {
            if (_repository != null)
                _repository.LocalSaveCommitted -= HandlePublicProfileChanged;
            PlayerIdAccessRuntime.AccessChanged -= ApplyPlayerIdAccess;
            PlayerFriendsRuntime.Changed -= HandleFriendsRuntimeChanged;
            FriendDuelChallengeRuntime.Changed -=
                HandleFriendDuelChallengeChanged;
        }

        private void SyncPublicProfileSnapshot(bool readyForUpload = false)
        {
            if (_repository?.State == null)
                return;
            if (readyForUpload)
                _publicProfileReadyForUpload = true;
            DuelStatisticsScope statistics = _repository?.Statistics?.overall ??
                                              new DuelStatisticsScope();
            int ownedCardCopies = 0;
            int uniqueCardCount = 0;
            foreach (CardQuantityRecord card in _repository.State.cardQuantities)
            {
                if (card == null || card.quantity <= 0)
                    continue;
                uniqueCardCount++;
                ownedCardCopies = (int)Math.Min(
                    int.MaxValue,
                    (long)ownedCardCopies + card.quantity);
            }
            PlayerCraftWallet craftPoints = _repository.State.craftPoints ??
                                            new PlayerCraftWallet();
            PlayerIdAccessRuntime.SetPlayerPublicProfile(
                _repository?.EquippedIconId,
                _repository?.CaptureRankSnapshot()?.rankedPoints ?? 0,
                statistics.duelsPlayed,
                statistics.wins,
                statistics.losses,
                statistics.draws,
                _repository.CoinBalance,
                _repository.OwnedIconIds.Count,
                _repository.OwnedArtworkIds.Count,
                ownedCardCopies,
                uniqueCardCount,
                _repository.State.decks?.Count ?? 0,
                _repository.State.unlockedDeckProductIds?.Count ?? 0,
                craftPoints.cpN,
                craftPoints.cpR,
                craftPoints.cpSR,
                craftPoints.cpUR,
                _repository.EquippedArtworkId,
                PlayerIdAccessPolicy.PublicProfileRevisionUtcMilliseconds(
                    _repository.State.lastModifiedUtcTicks),
                _publicProfileReadyForUpload);
        }

        private void HandlePublicProfileChanged()
        {
            SyncPublicProfileSnapshot();
            if (!_publicProfileReadyForUpload)
                return;
            _publicProfileRefreshQueued = true;
            if (!_publicProfileRefreshRunning)
                _ = RefreshPublicProfileAsync();
        }

        private async Task RefreshPublicProfileAsync()
        {
            _publicProfileRefreshRunning = true;
            try
            {
                while (_publicProfileRefreshQueued && this != null)
                {
                    _publicProfileRefreshQueued = false;
                    try
                    {
                        await PlayerIdAccessRuntime.RefreshNowAsync();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "O perfil público será sincronizado no próximo " +
                            "heartbeat: " +
                            exception.GetBaseException().Message);
                    }
                }
            }
            finally
            {
                _publicProfileRefreshRunning = false;
            }
        }
    }
}
