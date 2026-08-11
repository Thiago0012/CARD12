using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ArcaneArena.Frontend;
using ArcaneDuel.Game;
using ArcaneDuel.Game.Tournaments;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using LobbyModel = Unity.Services.Lobbies.Models.Lobby;
using LobbyPlayer = Unity.Services.Lobbies.Models.Player;

namespace ArcaneArena.Multiplayer.Tournaments
{
    /// <summary>
    /// Sala persistente do campeonato. O Lobby carrega apenas snapshots
    /// compactos; cada confronto abre uma Session/Relay 1v1 separada.
    /// Somente o host atual do Lobby consolida participantes e resultados.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TournamentOnlineSession : MonoBehaviour
    {
        internal const string ProtocolVersion = "arcane-tournament-v2";

        private const string TypeKey = "tt";
        private const string ProtocolKey = "tp";
        private const string TournamentIdKey = "ti";
        private const string StateCountKey = "tc";
        private const string StateHashKey = "th";
        private const string StateRevisionKey = "tr";
        private const string StateChunkPrefix = "ts";
        private const string ProfileKey = "pf";
        private const string ProfileHashKey = "ph";
        private const string DuelRoomKey = "du";
        private const string ResultCountKey = "rc";
        private const string ResultHashKey = "rh";
        private const string ResultChunkPrefix = "rs";
        private const float PollIntervalSeconds = 2f;
        private const float HeartbeatIntervalSeconds = 15f;

        [Serializable]
        private sealed class PlayerProfileEnvelope
        {
            public string displayName;
            public string avatarId;
            public bool ready;
            public string appVersion;
            public string protocolVersion;
            public string platform;
            public bool usesRandomDeck;
            public TournamentDeckManifest deck;
        }

        [Serializable]
        private sealed class DuelRoomEnvelope
        {
            public string tournamentId;
            public string matchId;
            public string hostPlayerId;
            public string roomCode;
            public int gameNumber;
            public int revision;
        }

        public static TournamentOnlineSession Instance { get; private set; }

        private TournamentPersistenceStore persistence;
        private TournamentPersistenceEnvelope persisted;
        private TournamentManager manager;
        private LobbyModel lobby;
        private PlayerProfileEnvelope localProfile;
        private TournamentState state;
        private bool operationInProgress;
        private float nextPollAt;
        private float nextHeartbeatAt;
        private int lastDecodedRevision = -1;
        private int lastPublishedRevision = -1;
        private string statusMessage = string.Empty;

        public event Action StateChanged;

        public TournamentState State => state;
        public IReadOnlyList<TournamentState> History =>
            persisted?.history ?? (IReadOnlyList<TournamentState>)
                Array.Empty<TournamentState>();
        public bool HasTournament => state?.config != null && lobby != null;
        public bool IsOrganizer => lobby != null && string.Equals(
            lobby.HostId,
            LocalPlayerId,
            StringComparison.Ordinal);
        public bool IsBusy => operationInProgress;
        public string StatusMessage => statusMessage;
        public string LobbyCode => lobby?.LobbyCode ?? state?.lobbyCode ??
            string.Empty;
        public string LocalPlayerId =>
            AuthenticationService.Instance?.PlayerId ?? string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            EnsureInstance();
        }

        public static TournamentOnlineSession EnsureInstance()
        {
            if (Instance != null)
                return Instance;
            var root = new GameObject("Arcane Tournament Online Session");
            Instance = root.AddComponent<TournamentOnlineSession>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            persistence = new TournamentPersistenceStore();
            persisted = persistence.Load();
        }

        private void Update()
        {
            if (lobby == null || operationInProgress ||
                Time.realtimeSinceStartup < nextPollAt)
            {
                return;
            }
            nextPollAt = Time.realtimeSinceStartup + PollIntervalSeconds;
            _ = PollLobbyAsync();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && lobby != null)
                nextPollAt = 0f;
        }

        public async Task<TournamentOperationResult> CreateTournamentAsync(
            TournamentConfig config,
            string password)
        {
            if (operationInProgress)
                return TournamentOperationResult.Fail("Outra operação está em andamento.");
            TournamentOperationResult validation =
                TournamentManager.ValidateConfig(config);
            if (!validation.Success)
                return validation;
            password = password?.Trim() ?? string.Empty;
            if (password.Length > 0 &&
                (password.Length < 8 || password.Length > 64))
            {
                return TournamentOperationResult.Fail(
                    "A senha privada deve ter de 8 a 64 caracteres.");
            }

            operationInProgress = true;
            try
            {
                await InitializeServicesAsync();
                if (!TryBuildLocalProfile(false, out localProfile,
                        out string profileError))
                {
                    return TournamentOperationResult.Fail(profileError);
                }
                config.standardBanListId = string.IsNullOrWhiteSpace(
                    config.standardBanListId)
                    ? BanlistService.ActiveBanlistId
                    : config.standardBanListId;
                config.passwordHash = HashPassword(password);
                manager = TournamentManager.Create(config);
                state = manager.State;
                TournamentOperationResult participant =
                    manager.AddOrUpdateParticipant(
                        LocalPlayerId,
                        localProfile.displayName,
                        localProfile.deck,
                        true,
                        localProfile.avatarId,
                        false,
                        localProfile.usesRandomDeck);
                if (!participant.Success &&
                    state.FindPlayer(LocalPlayerId) == null)
                {
                    return participant;
                }

                Dictionary<string, DataObject> data = BuildStateData(state);
                lobby = await LobbyService.Instance.CreateLobbyAsync(
                    config.name,
                    config.participantLimit,
                    new CreateLobbyOptions
                    {
                        IsPrivate = config.privateRoom,
                        Password = password.Length == 0 ? null : password,
                        IsLocked = false,
                        Player = BuildLobbyPlayer(localProfile),
                        Data = data
                    });
                state.lobbyId = lobby.Id;
                state.lobbyCode = lobby.LobbyCode;
                manager = new TournamentManager(state);
                lastPublishedRevision = -1;
                await PublishStateAsync(true);
                statusMessage =
                    "Torneio criado. Compartilhe o código e confirme PRONTO.";
                PersistCurrent();
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                ResetRuntimeLobby();
                statusMessage = "Não foi possível criar o torneio: " +
                    FriendlyError(exception);
                return TournamentOperationResult.Fail(statusMessage);
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<TournamentOperationResult> JoinTournamentAsync(
            string code,
            string password)
        {
            if (operationInProgress)
                return TournamentOperationResult.Fail("Outra operação está em andamento.");
            code = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length < 6 || code.Length > 12)
                return TournamentOperationResult.Fail("Informe um código de 6 a 12 caracteres.");

            operationInProgress = true;
            try
            {
                await InitializeServicesAsync();
                if (!TryBuildLocalProfile(false, out localProfile,
                        out string profileError))
                {
                    return TournamentOperationResult.Fail(profileError);
                }
                lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(
                    code,
                    new JoinLobbyByCodeOptions
                    {
                        Password = string.IsNullOrWhiteSpace(password)
                            ? null
                            : password.Trim(),
                        Player = BuildLobbyPlayer(localProfile)
                    });
                if (!TryReadState(lobby, out TournamentState remote,
                        out string stateError))
                {
                    await SafeRemoveLocalPlayerAsync();
                    ResetRuntimeLobby();
                    return TournamentOperationResult.Fail(stateError);
                }
                state = remote;
                manager = IsOrganizer ? new TournamentManager(state) : null;
                lastDecodedRevision = state.revision;
                statusMessage =
                    "Entrada concluída. Revise o deck e confirme PRONTO.";
                PersistCurrent();
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                ResetRuntimeLobby();
                statusMessage = "Não foi possível entrar no torneio: " +
                    FriendlyError(exception);
                return TournamentOperationResult.Fail(statusMessage);
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<TournamentOperationResult> ResumeTournamentAsync()
        {
            TournamentConnectionTicket ticket = persisted?.connectionTicket;
            if (ticket == null || !ticket.IsValid)
            {
                return TournamentOperationResult.Fail(
                    "Não há torneio online salvo para continuar.");
            }
            if (operationInProgress)
                return TournamentOperationResult.Fail("Outra operação está em andamento.");
            operationInProgress = true;
            try
            {
                await InitializeServicesAsync();
                lobby = await LobbyService.Instance.GetLobbyAsync(ticket.lobbyId);
                if (!TryReadState(lobby, out TournamentState remote,
                        out string error))
                {
                    ResetRuntimeLobby();
                    return TournamentOperationResult.Fail(error);
                }
                state = remote;
                manager = IsOrganizer ? new TournamentManager(state) : null;
                lastDecodedRevision = state.revision;
                TryBuildLocalProfile(
                    state.FindPlayer(LocalPlayerId)?.isReady == true,
                    out localProfile,
                    out _);
                statusMessage = "Torneio retomado no último estado íntegro.";
                nextPollAt = 0f;
                PersistCurrent();
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                ResetRuntimeLobby();
                return TournamentOperationResult.Fail(
                    "Não foi possível retomar o torneio: " +
                    FriendlyError(exception));
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<TournamentOperationResult> UpdateConfigAsync(
            TournamentConfig config,
            string newPassword)
        {
            if (!IsOrganizer || manager == null)
                return TournamentOperationResult.Fail("Apenas o organizador pode editar.");
            if (operationInProgress)
                return TournamentOperationResult.Fail("Outra operação está em andamento.");
            newPassword = newPassword?.Trim() ?? string.Empty;
            if (newPassword.Length > 0 &&
                (newPassword.Length < 8 || newPassword.Length > 64))
            {
                return TournamentOperationResult.Fail(
                    "A nova senha deve ter de 8 a 64 caracteres.");
            }
            config.passwordHash = newPassword.Length > 0
                ? HashPassword(newPassword)
                : state.config.passwordHash;
            TournamentState rollbackState = CloneState(state);
            TournamentOperationResult result =
                manager.UpdateLobbyConfig(config);
            if (!result.Success)
                return result;
            state = manager.State;
            operationInProgress = true;
            try
            {
                if (newPassword.Length > 0)
                {
                    lobby = await LobbyService.Instance.UpdateLobbyAsync(
                        lobby.Id,
                        new UpdateLobbyOptions { Password = newPassword });
                }
                await PublishStateAsync(true);
                statusMessage = result.Message;
                RaiseStateChanged();
                return result;
            }
            catch (Exception exception)
            {
                state = rollbackState;
                manager = new TournamentManager(state);
                return TournamentOperationResult.Fail(
                    "A alteração não foi publicada e o estado anterior foi restaurado: " +
                    FriendlyError(exception));
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<TournamentOperationResult> SetReadyAsync(bool ready)
        {
            if (lobby == null)
                return TournamentOperationResult.Fail("Entre em um torneio primeiro.");
            if (!TryBuildLocalProfile(ready, out PlayerProfileEnvelope profile,
                    out string error))
            {
                return TournamentOperationResult.Fail(error);
            }
            operationInProgress = true;
            try
            {
                localProfile = profile;
                lobby = await LobbyService.Instance.UpdatePlayerAsync(
                    lobby.Id,
                    LocalPlayerId,
                    new UpdatePlayerOptions
                    {
                        Data = BuildProfileData(localProfile)
                    });
                if (IsOrganizer && manager != null)
                {
                    manager.AddOrUpdateParticipant(
                        LocalPlayerId,
                        localProfile.displayName,
                        localProfile.deck,
                        true,
                        localProfile.avatarId,
                        ready,
                        localProfile.usesRandomDeck);
                    state = manager.State;
                    await PublishStateAsync(true);
                }
                statusMessage = ready
                    ? "Presença confirmada. Aguardando os demais jogadores."
                    : "Status alterado para aguardando.";
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                statusMessage = "Não foi possível atualizar o status: " +
                    FriendlyError(exception);
                return TournamentOperationResult.Fail(statusMessage);
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<TournamentOperationResult> UpdateDeckSelectionAsync(
            bool useRandomDeck,
            string manualDeckId)
        {
            if (lobby == null)
            {
                return TournamentOperationResult.Fail(
                    "Entre em um torneio antes de escolher o deck.");
            }
            if (operationInProgress)
            {
                return TournamentOperationResult.Fail(
                    "Outra operação do torneio está em andamento.");
            }

            GameFrontendBootstrap frontend = GameFrontendBootstrap.Instance;
            string rejection = string.Empty;
            if (frontend == null || !frontend.SetTournamentDeckPreference(
                    useRandomDeck,
                    manualDeckId,
                    out rejection))
            {
                return TournamentOperationResult.Fail(
                    string.IsNullOrWhiteSpace(rejection)
                        ? "Não foi possível aplicar a escolha de deck."
                        : rejection);
            }

            TournamentOperationResult result = await SetReadyAsync(false);
            if (!result.Success)
                return result;

            statusMessage = useRandomDeck
                ? "Deck aleatório confirmado entre os decks desbloqueados. Confirme PRONTO quando estiver preparado."
                : "Deck do torneio atualizado. Confirme PRONTO quando estiver preparado.";
            RaiseStateChanged();
            return TournamentOperationResult.Ok(statusMessage);
        }

        public async Task<TournamentOperationResult> StartTournamentAsync()
        {
            if (!IsOrganizer || manager == null)
                return TournamentOperationResult.Fail("Apenas o organizador pode iniciar.");
            if (operationInProgress)
                return TournamentOperationResult.Fail("Outra operação está em andamento.");
            TournamentState rollbackState = CloneState(state);
            TournamentOperationResult result = manager.StartTournament();
            if (!result.Success)
                return result;
            state = manager.State;
            operationInProgress = true;
            try
            {
                await PublishStateAsync(true);
                statusMessage = "Torneio iniciado. Os primeiros confrontos foram liberados.";
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                state = rollbackState;
                manager = new TournamentManager(state);
                statusMessage = "Falha ao publicar o início: " +
                    FriendlyError(exception);
                return TournamentOperationResult.Fail(statusMessage);
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<TournamentOperationResult> AwardWalkoverAsync(
            string matchId,
            string winnerId)
        {
            if (!IsOrganizer || manager == null)
            {
                return TournamentOperationResult.Fail(
                    "Apenas o organizador pode registrar WO.");
            }
            if (operationInProgress)
                return TournamentOperationResult.Fail("Outra operação está em andamento.");

            TournamentState rollbackState = CloneState(state);
            TournamentOperationResult result = manager.AwardWalkover(
                matchId,
                winnerId);
            if (!result.Success)
                return result;

            state = manager.State;
            operationInProgress = true;
            try
            {
                await PublishStateAsync(true);
                statusMessage = "WO registrado e chave atualizada.";
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                state = rollbackState;
                manager = new TournamentManager(state);
                statusMessage = "O WO não foi publicado e foi revertido: " +
                    FriendlyError(exception);
                RaiseStateChanged();
                return TournamentOperationResult.Fail(statusMessage);
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<TournamentOperationResult> ReopenMatchAsync(
            string matchId)
        {
            if (!IsOrganizer || manager == null)
            {
                return TournamentOperationResult.Fail(
                    "Apenas o organizador pode reabrir um confronto.");
            }
            if (operationInProgress)
                return TournamentOperationResult.Fail("Outra operação está em andamento.");

            TournamentState rollbackState = CloneState(state);
            TournamentOperationResult result = manager.ReopenMatch(
                matchId,
                "nova tentativa autorizada pelo organizador");
            if (!result.Success)
                return result;

            state = manager.State;
            operationInProgress = true;
            try
            {
                await PublishStateAsync(true);
                statusMessage =
                    "Confronto reaberto. A sala Relay anterior foi descartada.";
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                state = rollbackState;
                manager = new TournamentManager(state);
                statusMessage = "A reabertura não foi publicada e foi revertida: " +
                    FriendlyError(exception);
                RaiseStateChanged();
                return TournamentOperationResult.Fail(statusMessage);
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public TournamentOperationResult EnterLocalMatch()
        {
            if (state?.config?.status != TournamentStatus.InProgress)
                return TournamentOperationResult.Fail("O torneio não está em andamento.");
            TournamentManager view = manager ?? new TournamentManager(state);
            TournamentMatch match = view.ActiveMatchForPlayer(LocalPlayerId);
            if (match == null || match.status == TournamentMatchStatus.Waiting)
            {
                return TournamentOperationResult.Fail(
                    "Sua próxima partida ainda não foi liberada.");
            }
            var context = new TournamentDuelContext
            {
                tournamentId = state.config.tournamentId,
                lobbyId = state.lobbyId,
                matchId = match.matchId,
                roundId = match.roundId,
                roundNumber = match.roundNumber,
                bestOf = match.bestOf,
                playerAId = match.playerAId,
                playerBId = match.playerBId,
                localPlayerId = LocalPlayerId,
                competitivePolicy = state.config.competitivePolicy
            };
            DuelOnlineSession duel = DuelOnlineSession.EnsureInstance();
            if (context.LocalPlayerHosts)
            {
                duel.BeginTournamentHosting(context);
                statusMessage = "Criando a sala segura do confronto...";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(match.relayRoomCode))
                {
                    return TournamentOperationResult.Fail(
                        "Aguardando o Jogador A criar a sala do confronto.");
                }
                duel.BeginTournamentJoining(context, match.relayRoomCode);
                statusMessage = "Entrando na sala segura do confronto...";
            }
            RaiseStateChanged();
            return TournamentOperationResult.Ok(statusMessage);
        }

        internal async Task NotifyDuelRoomCreatedAsync(
            TournamentDuelContext context,
            string roomCode)
        {
            if (lobby == null || context == null || !context.IsValid ||
                !context.LocalPlayerHosts)
            {
                return;
            }
            TournamentMatch currentMatch = state?.FindMatch(context.matchId);
            var envelope = new DuelRoomEnvelope
            {
                tournamentId = context.tournamentId,
                matchId = context.matchId,
                hostPlayerId = context.localPlayerId,
                roomCode = (roomCode ?? string.Empty).Trim().ToUpperInvariant(),
                gameNumber = (currentMatch?.gamesWonByA ?? 0) +
                    (currentMatch?.gamesWonByB ?? 0),
                revision = state?.revision ?? 0
            };
            try
            {
                lobby = await LobbyService.Instance.UpdatePlayerAsync(
                    lobby.Id,
                    LocalPlayerId,
                    new UpdatePlayerOptions
                    {
                        Data = new Dictionary<string, PlayerDataObject>
                        {
                            [DuelRoomKey] = MemberPlayerData(
                                JsonUtility.ToJson(envelope, false))
                        }
                    });
                statusMessage = "Sala do confronto publicada. Aguardando o rival.";
                nextPollAt = 0f;
                RaiseStateChanged();
            }
            catch (Exception exception)
            {
                statusMessage = "Falha ao publicar a sala do confronto: " +
                    FriendlyError(exception);
                RaiseStateChanged();
            }
        }

        internal async Task ReportDuelResultAsync(
            TournamentDuelContext context,
            int winnerSeat,
            bool surrender,
            bool timeout,
            TournamentDuelStatsSnapshot stats)
        {
            if (lobby == null || context == null || !context.IsValid ||
                !context.LocalPlayerHosts)
            {
                return;
            }
            string winnerId = winnerSeat == 1
                ? context.playerBId
                : context.playerAId;
            string loserId = winnerSeat == 1
                ? context.playerAId
                : context.playerBId;
            var result = new TournamentMatchResult
            {
                resultId = $"{context.matchId}:{Guid.NewGuid():N}",
                tournamentId = context.tournamentId,
                roundId = context.roundId,
                matchId = context.matchId,
                playerAId = context.playerAId,
                playerBId = context.playerBId,
                winnerId = winnerId,
                loserId = loserId,
                finishedAtUtcTicks = DateTime.UtcNow.Ticks,
                surrender = surrender,
                timeout = timeout,
                statsSnapshotId = stats?.statsSnapshotId ?? string.Empty,
                stats = stats
            };
            try
            {
                TournamentEncodedPayload payload = TournamentLobbyCodec.Encode(
                    result,
                    TournamentLobbyCodec.MaximumPlayerChunks);
                var data = new Dictionary<string, PlayerDataObject>
                {
                    [ResultCountKey] = MemberPlayerData(
                        payload.Chunks.Count.ToString()),
                    [ResultHashKey] = MemberPlayerData(payload.Sha256)
                };
                for (int index = 0; index < payload.Chunks.Count; index++)
                {
                    data[ResultChunkPrefix + index] =
                        MemberPlayerData(payload.Chunks[index]);
                }
                lobby = await LobbyService.Instance.UpdatePlayerAsync(
                    lobby.Id,
                    LocalPlayerId,
                    new UpdatePlayerOptions { Data = data });

                if (IsOrganizer && manager != null)
                {
                    int before = state.revision;
                    TournamentOperationResult accepted =
                        manager.SubmitGameResult(result);
                    state = manager.State;
                    if (accepted.Success && state.revision != before)
                        await PublishStateAsync(true);
                }
                statusMessage = "Resultado enviado e salvo no campeonato.";
                nextPollAt = 0f;
                RaiseStateChanged();
            }
            catch (Exception exception)
            {
                statusMessage = "Falha ao registrar o resultado: " +
                    FriendlyError(exception);
                RaiseStateChanged();
            }
        }

        public async Task<TournamentOperationResult> CancelTournamentAsync()
        {
            if (!IsOrganizer || manager == null)
                return TournamentOperationResult.Fail("Apenas o organizador pode cancelar.");
            TournamentState rollbackState = CloneState(state);
            manager.Cancel("Torneio cancelado pelo organizador.");
            state = manager.State;
            operationInProgress = true;
            try
            {
                await PublishStateAsync(true);
                PersistCurrent();
                // The locked cancelled lobby is intentionally left alive for
                // its short inactivity window. That gives every participant
                // time to poll the final state before Unity Lobby expires it.
                ResetRuntimeLobby(false);
                statusMessage = "Torneio cancelado e salvo no histórico.";
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                state = rollbackState;
                manager = new TournamentManager(state);
                statusMessage = "Falha ao cancelar o torneio: " +
                    FriendlyError(exception);
                return TournamentOperationResult.Fail(statusMessage);
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<TournamentOperationResult> LeaveTournamentAsync()
        {
            if (lobby == null)
                return TournamentOperationResult.Ok("Você já saiu do torneio.");
            if (IsOrganizer)
            {
                return TournamentOperationResult.Fail(
                    "O organizador deve cancelar o torneio para encerrar a sala.");
            }
            operationInProgress = true;
            try
            {
                await SafeRemoveLocalPlayerAsync();
                persisted.activeTournament = null;
                persisted.connectionTicket = null;
                persistence.Save(persisted);
                ResetRuntimeLobby();
                statusMessage = "Você saiu do torneio.";
                RaiseStateChanged();
                return TournamentOperationResult.Ok(statusMessage);
            }
            catch (Exception exception)
            {
                return TournamentOperationResult.Fail(
                    "Não foi possível sair: " + FriendlyError(exception));
            }
            finally
            {
                operationInProgress = false;
            }
        }

        private async Task PollLobbyAsync()
        {
            operationInProgress = true;
            try
            {
                LobbyModel refreshed = await LobbyService.Instance
                    .GetLobbyAsync(lobby.Id);
                lobby = refreshed;
                bool hostNow = IsOrganizer;
                if (hostNow && manager == null && state != null)
                {
                    manager = new TournamentManager(state);
                    state.organizerPlayerId = LocalPlayerId;
                    foreach (TournamentPlayer player in state.players)
                        player.isOrganizer = player.playerId == LocalPlayerId;
                }

                if (hostNow)
                {
                    if (Time.realtimeSinceStartup >= nextHeartbeatAt)
                    {
                        await LobbyService.Instance.SendHeartbeatPingAsync(
                            lobby.Id);
                        nextHeartbeatAt = Time.realtimeSinceStartup +
                            HeartbeatIntervalSeconds;
                    }
                    int revisionBefore = state?.revision ?? -1;
                    ReconcileParticipants();
                    ReconcileDuelRoomsAndResults();
                    state = manager.State;
                    if (state.revision != revisionBefore ||
                        state.revision != lastPublishedRevision)
                    {
                        await PublishStateAsync(false);
                    }
                }
                else if (TryReadState(lobby, out TournamentState remote,
                             out string error))
                {
                    if (remote.revision >= lastDecodedRevision)
                    {
                        state = remote;
                        lastDecodedRevision = remote.revision;
                        statusMessage = StatusForState(remote);
                        PersistCurrent();
                        RaiseStateChanged();
                    }
                }
                else
                {
                    statusMessage = "Sincronização aguardando snapshot íntegro: " +
                        error;
                    RaiseStateChanged();
                }
            }
            catch (Exception exception)
            {
                statusMessage = "Conexão com o lobby temporariamente indisponível: " +
                    FriendlyError(exception);
                RaiseStateChanged();
            }
            finally
            {
                operationInProgress = false;
            }
        }

        private void ReconcileParticipants()
        {
            if (manager == null || lobby?.Players == null)
                return;
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (LobbyPlayer member in lobby.Players)
            {
                if (member == null || string.IsNullOrWhiteSpace(member.Id) ||
                    !TryReadProfile(member, out PlayerProfileEnvelope profile))
                {
                    continue;
                }
                present.Add(member.Id);
                TournamentPlayer existing = state.FindPlayer(member.Id);
                bool organizer = string.Equals(
                    member.Id,
                    lobby.HostId,
                    StringComparison.Ordinal);
                if (ParticipantChanged(existing, profile, organizer))
                {
                    manager.AddOrUpdateParticipant(
                        member.Id,
                        profile.displayName,
                        profile.deck,
                        organizer,
                        profile.avatarId,
                        profile.ready,
                        profile.usesRandomDeck);
                }
                else if (existing != null && !existing.isOnline)
                {
                    manager.SetPlayerOnline(member.Id, true);
                }
            }

            foreach (TournamentPlayer player in state.players.ToArray())
            {
                if (player == null || present.Contains(player.playerId))
                    continue;
                if (state.config.status == TournamentStatus.Lobby &&
                    !player.isOrganizer)
                {
                    manager.RemoveParticipant(player.playerId);
                }
                else if (player.isOnline)
                {
                    manager.SetPlayerOnline(player.playerId, false);
                }
            }
        }

        private void ReconcileDuelRoomsAndResults()
        {
            if (manager == null || lobby?.Players == null ||
                state.config.status != TournamentStatus.InProgress)
            {
                return;
            }
            foreach (LobbyPlayer member in lobby.Players)
            {
                if (member == null)
                    continue;
                string duelJson = GetPlayerData(member, DuelRoomKey);
                if (!string.IsNullOrWhiteSpace(duelJson))
                {
                    try
                    {
                        DuelRoomEnvelope room =
                            JsonUtility.FromJson<DuelRoomEnvelope>(duelJson);
                        TournamentMatch match = state.FindMatch(room?.matchId);
                        if (match != null &&
                            string.Equals(member.Id, match.playerAId,
                                StringComparison.Ordinal) &&
                            string.Equals(room.tournamentId,
                                state.config.tournamentId,
                                StringComparison.Ordinal) &&
                            room.gameNumber ==
                                match.gamesWonByA + match.gamesWonByB &&
                            !string.IsNullOrWhiteSpace(room.roomCode) &&
                            !string.Equals(match.relayRoomCode, room.roomCode,
                                StringComparison.Ordinal))
                        {
                            manager.SetMatchRelayRoom(
                                match.matchId,
                                member.Id,
                                room.roomCode);
                            manager.MarkMatchInProgress(match.matchId, member.Id);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("[Tournament] Sala de confronto inválida: " +
                            exception.GetBaseException().Message);
                    }
                }

                if (TryReadResult(member, out TournamentMatchResult result))
                {
                    TournamentMatch match = state.FindMatch(result.matchId);
                    if (match != null && string.Equals(
                            member.Id,
                            match.playerAId,
                            StringComparison.Ordinal))
                    {
                        manager.SubmitGameResult(result);
                    }
                }
            }
        }

        private async Task PublishStateAsync(bool force)
        {
            if (!IsOrganizer || manager == null || lobby == null)
                return;
            state = manager.State;
            if (!force && state.revision == lastPublishedRevision)
                return;
            Dictionary<string, DataObject> data = BuildStateData(state);
            lobby = await LobbyService.Instance.UpdateLobbyAsync(
                lobby.Id,
                new UpdateLobbyOptions
                {
                    Name = state.config.name,
                    MaxPlayers = state.config.participantLimit,
                    IsPrivate = state.config.privateRoom,
                    IsLocked = state.config.status != TournamentStatus.Lobby,
                    Data = data
                });
            lastPublishedRevision = state.revision;
            lastDecodedRevision = state.revision;
            PersistCurrent();
            RaiseStateChanged();
        }

        private static Dictionary<string, DataObject> BuildStateData(
            TournamentState tournamentState)
        {
            TournamentEncodedPayload payload = TournamentLobbyCodec.Encode(
                tournamentState,
                TournamentLobbyCodec.MaximumLobbyChunks);
            var data = new Dictionary<string, DataObject>
            {
                [TypeKey] = MemberLobbyData("arcane-tournament"),
                [ProtocolKey] = MemberLobbyData(ProtocolVersion),
                [TournamentIdKey] = MemberLobbyData(
                    tournamentState.config.tournamentId),
                [StateCountKey] = MemberLobbyData(
                    payload.Chunks.Count.ToString()),
                [StateHashKey] = MemberLobbyData(payload.Sha256),
                [StateRevisionKey] = MemberLobbyData(
                    tournamentState.revision.ToString())
            };
            for (int index = 0; index < payload.Chunks.Count; index++)
                data[StateChunkPrefix + index] =
                    MemberLobbyData(payload.Chunks[index]);
            return data;
        }

        private static bool TryReadState(
            LobbyModel source,
            out TournamentState tournamentState,
            out string error)
        {
            tournamentState = null;
            error = string.Empty;
            if (source?.Data == null ||
                !string.Equals(GetLobbyData(source, TypeKey),
                    "arcane-tournament", StringComparison.Ordinal) ||
                !string.Equals(GetLobbyData(source, ProtocolKey),
                    ProtocolVersion, StringComparison.Ordinal))
            {
                error = "A sala usa outro tipo ou versão de protocolo.";
                return false;
            }
            if (!int.TryParse(GetLobbyData(source, StateCountKey),
                    out int count) || count < 1 ||
                count > TournamentLobbyCodec.MaximumLobbyChunks)
            {
                error = "Contagem de blocos do torneio inválida.";
                return false;
            }
            var chunks = new List<string>(count);
            for (int index = 0; index < count; index++)
                chunks.Add(GetLobbyData(source, StateChunkPrefix + index));
            return TournamentLobbyCodec.TryDecode(
                chunks,
                GetLobbyData(source, StateHashKey),
                out tournamentState,
                out error);
        }

        private bool TryBuildLocalProfile(
            bool ready,
            out PlayerProfileEnvelope profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            GameFrontendBootstrap frontend = GameFrontendBootstrap.Instance;
            if (frontend == null)
            {
                error = "A interface do perfil ainda não foi carregada.";
                return false;
            }

            bool hasLoadout = frontend.TryGetTournamentDuelLoadout(
                state?.config?.tournamentId ?? string.Empty,
                out DuelDeckLoadout loadout,
                out bool usesRandomDeck,
                out error);
            if (!hasLoadout && ready)
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Escolha um deck válido antes de confirmar presença."
                    : error;
                return false;
            }

            var manifest = hasLoadout
                ? new TournamentDeckManifest
                {
                    deckId = loadout.deckId,
                    displayName = loadout.displayName,
                    banListId = loadout.banlistId,
                    sha256 = loadout.normalizedDeckSha256,
                    mainDeckCardIds = new List<string>(loadout.mainDeckCardIds),
                    extraDeckCardIds = new List<string>(loadout.extraDeckCardIds),
                    sideDeckCardIds = new List<string>(loadout.sideDeckCardIds)
                }
                : new TournamentDeckManifest
                {
                    displayName = "Aguardando escolha de deck"
                };
            manifest.Normalize();
            string displayName = hasLoadout
                ? loadout.playerDisplayName
                : frontend.LocalTournamentPlayerDisplayName;
            profile = new PlayerProfileEnvelope
            {
                displayName = string.IsNullOrWhiteSpace(displayName)
                    ? "DUELISTA " + ShortPlayerId(LocalPlayerId)
                    : displayName.Trim(),
                avatarId = "default",
                ready = ready,
                appVersion = ProjectIdentity.ProjectVersion,
                protocolVersion = ProtocolVersion,
                platform = Application.platform.ToString(),
                usesRandomDeck = usesRandomDeck,
                deck = manifest
            };
            if (!hasLoadout)
                error = string.Empty;
            return true;
        }

        private static LobbyPlayer BuildLobbyPlayer(
            PlayerProfileEnvelope profile)
        {
            return new LobbyPlayer(
                AuthenticationService.Instance.PlayerId,
                data: BuildProfileData(profile));
        }

        private static Dictionary<string, PlayerDataObject> BuildProfileData(
            PlayerProfileEnvelope profile)
        {
            TournamentEncodedPayload payload = TournamentLobbyCodec.Encode(
                profile,
                1);
            return new Dictionary<string, PlayerDataObject>
            {
                [ProfileKey] = MemberPlayerData(payload.Chunks[0]),
                [ProfileHashKey] = MemberPlayerData(payload.Sha256)
            };
        }

        private static bool TryReadProfile(
            LobbyPlayer player,
            out PlayerProfileEnvelope profile)
        {
            profile = null;
            string chunk = GetPlayerData(player, ProfileKey);
            string hash = GetPlayerData(player, ProfileHashKey);
            if (string.IsNullOrWhiteSpace(chunk) ||
                string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }
            if (!TournamentLobbyCodec.TryDecode(
                    new[] { chunk },
                    hash,
                    out profile,
                    out _))
            {
                return false;
            }
            return profile != null && string.Equals(
                profile.protocolVersion,
                ProtocolVersion,
                StringComparison.Ordinal);
        }

        private static bool TryReadResult(
            LobbyPlayer player,
            out TournamentMatchResult result)
        {
            result = null;
            if (!int.TryParse(GetPlayerData(player, ResultCountKey),
                    out int count) || count < 1 ||
                count > TournamentLobbyCodec.MaximumPlayerChunks)
            {
                return false;
            }
            var chunks = new List<string>(count);
            for (int index = 0; index < count; index++)
                chunks.Add(GetPlayerData(player, ResultChunkPrefix + index));
            return TournamentLobbyCodec.TryDecode(
                chunks,
                GetPlayerData(player, ResultHashKey),
                out result,
                out _);
        }

        private static bool ParticipantChanged(
            TournamentPlayer existing,
            PlayerProfileEnvelope profile,
            bool organizer)
        {
            if (existing == null || profile?.deck == null)
                return true;
            return !string.Equals(existing.displayName, profile.displayName,
                       StringComparison.Ordinal) ||
                   !string.Equals(existing.avatarId, profile.avatarId,
                       StringComparison.Ordinal) ||
                   !string.Equals(existing.deckHash, profile.deck.sha256,
                       StringComparison.OrdinalIgnoreCase) ||
                   existing.usesRandomDeck != profile.usesRandomDeck ||
                   existing.isReady != profile.ready ||
                   existing.isOrganizer != organizer;
        }

        private void PersistCurrent()
        {
            if (state == null)
                return;
            var ticket = new TournamentConnectionTicket
            {
                tournamentId = state.config.tournamentId,
                lobbyId = state.lobbyId,
                lobbyCode = state.lobbyCode,
                localPlayerId = LocalPlayerId,
                localPlayerIsOrganizer = IsOrganizer,
                updatedAtUtcTicks = DateTime.UtcNow.Ticks
            };
            persistence.SaveActive(persisted, state, ticket);
            if (state.config.status == TournamentStatus.Completed ||
                state.config.status == TournamentStatus.Cancelled)
            {
                persistence.ArchiveActive(persisted);
            }
        }

        private async Task SafeRemoveLocalPlayerAsync()
        {
            if (lobby == null || string.IsNullOrWhiteSpace(LocalPlayerId))
                return;
            await LobbyService.Instance.RemovePlayerAsync(
                lobby.Id,
                LocalPlayerId);
        }

        private static async Task InitializeServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                string authenticationProfile = CommandLineValue(
                    "-arcaneAuthProfile");
                if (string.IsNullOrWhiteSpace(authenticationProfile))
                {
                    await UnityServices.InitializeAsync();
                }
                else
                {
                    var options = new InitializationOptions();
                    options.SetProfile(authenticationProfile.Trim());
                    await UnityServices.InitializeAsync(options);
                }
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static string CommandLineValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(
                        arguments[index],
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }
            return string.Empty;
        }

        private static TournamentState CloneState(TournamentState source)
        {
            return source == null
                ? null
                : JsonUtility.FromJson<TournamentState>(
                    JsonUtility.ToJson(source, false));
        }

        private void ResetRuntimeLobby(bool clearState = true)
        {
            lobby = null;
            manager = null;
            localProfile = null;
            lastDecodedRevision = -1;
            lastPublishedRevision = -1;
            if (clearState)
                state = null;
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static string StatusForState(TournamentState current)
        {
            if (current?.config == null)
                return "Aguardando dados do torneio.";
            return current.config.status switch
            {
                TournamentStatus.Lobby =>
                    "Lobby sincronizado. Confirme PRONTO quando seu deck estiver válido.",
                TournamentStatus.InProgress =>
                    "Torneio em andamento. Consulte sua próxima partida.",
                TournamentStatus.Completed =>
                    "Torneio encerrado. O resultado foi salvo no histórico.",
                TournamentStatus.Cancelled => "Torneio cancelado.",
                _ => "Torneio sincronizado."
            };
        }

        private static DataObject MemberLobbyData(string value)
        {
            return new DataObject(DataObject.VisibilityOptions.Member,
                value ?? string.Empty);
        }

        private static PlayerDataObject MemberPlayerData(string value)
        {
            return new PlayerDataObject(
                PlayerDataObject.VisibilityOptions.Member,
                value ?? string.Empty);
        }

        private static string GetLobbyData(LobbyModel source, string key)
        {
            return source?.Data != null &&
                   source.Data.TryGetValue(key, out DataObject value)
                ? value?.Value ?? string.Empty
                : string.Empty;
        }

        private static string GetPlayerData(LobbyPlayer source, string key)
        {
            return source?.Data != null &&
                   source.Data.TryGetValue(key, out PlayerDataObject value)
                ? value?.Value ?? string.Empty
                : string.Empty;
        }

        private static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
                builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        private static string ShortPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return "LOCAL";
            return playerId.Length <= 6
                ? playerId.ToUpperInvariant()
                : playerId.Substring(0, 6).ToUpperInvariant();
        }

        private static string FriendlyError(Exception exception)
        {
            string message = exception?.GetBaseException().Message ??
                "erro desconhecido";
            if (message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
                return "senha incorreta ou fora do padrão exigido";
            if (message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0)
                return "código expirado ou sala encerrada";
            if (message.IndexOf("rate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "serviço ocupado; tente novamente em instantes";
            return message;
        }
    }
}
