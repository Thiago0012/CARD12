using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ArcaneArena.Frontend;
using ArcaneDuel.Game;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using UnityEngine;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Single facade for the Unity Multiplayer Services session lifecycle.
    /// Relay allocation, Lobby membership and NGO start/stop are deliberately
    /// owned by Sessions so they cannot drift into different states.
    /// </summary>
    internal sealed class MultiplayerSessionCoordinator
    {
        internal const string SessionType = "arcane-duel-private-v5";
        internal const string PrivateDuelMode = "private-duel-1v1";
        internal const string RankedMatchmakingMode =
            "ranked-matchmaking-1v1";

        private const string AppVersionKey = "appVersion";
        private const string ProtocolVersionKey = "protocolVersion";
        private const string EngineVersionKey = "engineVersion";
        private const string RulesetHashKey = "rulesetHash";
        private const string CardDatabaseHashKey = "cardDbHash";
        private const string BanlistVersionKey = "banlistVersion";
        private const string ModeKey = "mode";
        private const string StatusKey = "status";
        private const string JoinableKey = "joinable";
        private const string MatchIdKey = "matchId";
        private const string HostEpochKey = "hostEpoch";
        private const string CompatibilityKey = "compatibility";

        private ISession currentSession;
        private Task leaveTask;

        public ISession CurrentSession => currentSession;
        public bool HasSession => currentSession != null && currentSession.IsMember;
        public bool IsHost => currentSession?.IsHost == true;
        public string Code => currentSession?.Code ?? string.Empty;
        public string SessionId => currentSession?.Id ?? string.Empty;

        public async Task<IHostSession> CreateAsync(
            DuelDeckLoadout loadout,
            string protocolVersion)
        {
            await LeaveAsync();

            var options = new SessionOptions
            {
                Type = SessionType,
                Name = "Arcane Duel Private 1v1",
                MaxPlayers = 2,
                IsPrivate = true,
                IsLocked = false,
                SessionProperties = CreateSessionProperties(
                    protocolVersion,
                    PrivateDuelMode,
                    false),
                PlayerProperties = CreatePlayerProperties(loadout, 0)
            };
            options.WithRelayNetwork();
            options.WithNetworkOptions(new NetworkOptions
            {
                RelayProtocol = RelayProtocol.DTLS
            });

            Debug.Log("[MP] stage=session-create transport=relay protocol=dtls capacity=2");
            IHostSession created = await ConnectWithLobbyEventRetryAsync(
                () => MultiplayerService.Instance.CreateSessionAsync(options),
                true);
            Bind(created);
            Debug.Log("[MP] stage=session-ready role=host members=1");
            return created;
        }

        public async Task<ISession> JoinByCodeAsync(
            string code,
            DuelDeckLoadout loadout,
            string protocolVersion)
        {
            await LeaveAsync();

            var options = new JoinSessionOptions
            {
                Type = SessionType,
                PlayerProperties = CreatePlayerProperties(loadout, 1)
            };
            options.WithNetworkOptions(new NetworkOptions
            {
                RelayProtocol = RelayProtocol.DTLS
            });

            Debug.Log("[MP] stage=session-join transport=relay protocol=dtls");
            ISession joined = await ConnectWithLobbyEventRetryAsync(
                () => MultiplayerService.Instance.JoinSessionByCodeAsync(
                    code,
                    options),
                false);
            Bind(joined);
            ValidateCompatibility(protocolVersion, PrivateDuelMode);
            Debug.Log("[MP] stage=session-ready role=client members=" +
                joined.PlayerCount);
            return joined;
        }

        public async Task<ISession> MatchmakeRankedAsync(
            DuelDeckLoadout loadout,
            string protocolVersion)
        {
            await LeaveAsync();

            var options = new SessionOptions
            {
                Type = SessionType,
                Name = "Arcane Duel Ranked 1v1",
                MaxPlayers = 2,
                IsPrivate = false,
                IsLocked = false,
                SessionProperties = CreateSessionProperties(
                    protocolVersion,
                    RankedMatchmakingMode,
                    true),
                // Quick Join can either create or join. The definitive seat
                // is written immediately after the service returns.
                PlayerProperties = CreatePlayerProperties(loadout, -1)
            };
            options.WithRelayNetwork();
            options.WithNetworkOptions(new NetworkOptions
            {
                RelayProtocol = RelayProtocol.DTLS
            });

            var quickJoin = new QuickJoinOptions
            {
                CreateSession = true,
                Timeout = TimeSpan.FromSeconds(
                    ComputeQuickJoinTimeoutSeconds()),
                Filters = new List<FilterOption>
                {
                    new FilterOption(
                        FilterField.StringIndex1,
                        RankedMatchmakingMode,
                        FilterOperation.Equal),
                    new FilterOption(
                        FilterField.StringIndex2,
                        protocolVersion,
                        FilterOperation.Equal),
                    new FilterOption(
                        FilterField.StringIndex3,
                        ComputeCompatibilityHash(),
                        FilterOperation.Equal),
                    new FilterOption(
                        FilterField.AvailableSlots,
                        "1",
                        FilterOperation.GreaterOrEqual),
                    new FilterOption(
                        FilterField.IsLocked,
                        "false",
                        FilterOperation.Equal)
                }
            };

            Debug.Log(
                "[MP] stage=ranked-matchmaking transport=relay " +
                "protocol=dtls capacity=2");
            ISession matched = await ConnectWithLobbyEventRetryAsync(
                () => MultiplayerService.Instance.MatchmakeSessionAsync(
                    quickJoin,
                    options),
                null);
            Bind(matched);
            ValidateCompatibility(
                protocolVersion,
                RankedMatchmakingMode);
            if (matched.CurrentPlayer != null)
            {
                matched.CurrentPlayer.SetProperty(
                    "seat",
                    MemberPlayerProperty(matched.IsHost ? "0" : "1"));
                await matched.SaveCurrentPlayerDataAsync();
            }
            Debug.Log(
                "[MP] stage=ranked-match-ready role=" +
                (matched.IsHost ? "host" : "client") +
                " members=" + matched.PlayerCount);
            return matched;
        }

        public async Task<ISession> ReconnectAsync(
            string knownSessionId = null)
        {
            string sessionId = string.IsNullOrWhiteSpace(knownSessionId)
                ? SessionId
                : knownSessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new InvalidOperationException(
                    "Nao existe uma sessao conhecida para reconectar.");

            Debug.Log("[MP] stage=reconnect-attempt");
            Unbind();
            currentSession = await MultiplayerService.Instance
                .ReconnectToSessionAsync(
                    sessionId,
                    new ReconnectSessionOptions { Type = SessionType });
            Bind(currentSession);
            Debug.Log("[MP] stage=reconnect-complete role=" +
                (currentSession.IsHost ? "host" : "client"));
            return currentSession;
        }

        public bool HasMember(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) &&
                currentSession?.HasPlayer(playerId) == true;
        }

        public async Task SetPlayerStateAsync(
            string connectionState,
            bool ready)
        {
            if (currentSession?.CurrentPlayer == null)
                return;

            currentSession.CurrentPlayer.SetProperty(
                "connectionState",
                MemberPlayerProperty(connectionState));
            currentSession.CurrentPlayer.SetProperty(
                "ready",
                MemberPlayerProperty(ready ? "true" : "false"));
            await currentSession.SaveCurrentPlayerDataAsync();
        }

        public async Task SetHostMatchStateAsync(
            string status,
            string matchId,
            bool joinable)
        {
            if (currentSession?.IsHost != true)
                return;

            IHostSession host = currentSession.AsHost();
            host.SetProperty(StatusKey, PublicProperty(status));
            host.SetProperty(MatchIdKey,
                MemberSessionProperty(matchId ?? string.Empty));
            host.SetProperty(JoinableKey,
                PublicProperty(joinable ? "true" : "false"));
            host.IsLocked = !joinable;
            await host.SavePropertiesAsync();
        }

        public Task LeaveAsync()
        {
            if (leaveTask != null)
                return leaveTask;
            if (currentSession == null)
                return Task.CompletedTask;

            ISession leavingSession = currentSession;
            Unbind();
            currentSession = null;
            leaveTask = LeaveCurrentSessionAsync(leavingSession);
            return leaveTask;
        }

        private async Task LeaveCurrentSessionAsync(ISession leavingSession)
        {
            // Ensure LeaveAsync has published the shared task before any
            // caller can attempt to create or join the next room.
            await Task.Yield();
            try
            {
                if (leavingSession.IsMember)
                    await leavingSession.LeaveAsync();
            }
            catch (SessionException exception)
            {
                // Leave is idempotent from the game's point of view. A lobby
                // already deleted by its host is an expected terminal state.
                Debug.LogWarning("[MP] stage=session-leave result=" +
                    exception.Error);
            }
            finally
            {
                leaveTask = null;
            }
        }

        private static Dictionary<string, SessionProperty>
            CreateSessionProperties(
                string protocolVersion,
                string mode,
                bool indexedForMatchmaking)
        {
            PropertyIndex modeIndex = indexedForMatchmaking
                ? PropertyIndex.String1
                : PropertyIndex.None;
            PropertyIndex protocolIndex = indexedForMatchmaking
                ? PropertyIndex.String2
                : PropertyIndex.None;
            PropertyIndex compatibilityIndex = indexedForMatchmaking
                ? PropertyIndex.String3
                : PropertyIndex.None;
            return new Dictionary<string, SessionProperty>
            {
                [AppVersionKey] = PublicProperty(ProjectIdentity.ProjectVersion),
                [ProtocolVersionKey] = PublicProperty(
                    protocolVersion,
                    protocolIndex),
                [EngineVersionKey] = PublicProperty(
                    ProjectIdentity.CoreApiVersion + "|" +
                    ProjectIdentity.CoreCommit),
                [RulesetHashKey] = PublicProperty(
                    ProjectIdentity.CardScriptsCommit),
                [CardDatabaseHashKey] = PublicProperty(
                    ProjectIdentity.BabelCdbCommit),
                [BanlistVersionKey] = PublicProperty(
                    BanlistService.ActiveBanlistId),
                [ModeKey] = PublicProperty(mode, modeIndex),
                [CompatibilityKey] = PublicProperty(
                    ComputeCompatibilityHash(),
                    compatibilityIndex),
                [StatusKey] = PublicProperty("waiting"),
                [JoinableKey] = PublicProperty("true"),
                [MatchIdKey] = MemberSessionProperty(string.Empty),
                [HostEpochKey] = MemberSessionProperty("1")
            };
        }

        private static async Task<TSession> ConnectWithLobbyEventRetryAsync<TSession>(
            Func<Task<TSession>> connect,
            bool? createdByLocalPlayer)
            where TSession : class, ISession
        {
            HashSet<string> membershipsBefore =
                await JoinedSessionIdsOrEmptyAsync();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return await connect();
                }
                catch (Exception exception)
                    when (IsLobbyEventConnectionFailure(exception))
                {
                    await CleanupNewLobbyMembershipsAsync(
                        membershipsBefore,
                        createdByLocalPlayer);
                    if (attempt > 0)
                        throw;
                    Debug.LogWarning(
                        "[MP] stage=lobby-events-retry reason=23000");
                    await Task.Delay(700);
                    membershipsBefore = await JoinedSessionIdsOrEmptyAsync();
                }
            }
            throw new InvalidOperationException(
                "A conexão com os eventos do Lobby não foi concluída.");
        }

        private static async Task<HashSet<string>> JoinedSessionIdsOrEmptyAsync()
        {
            try
            {
                List<string> ids = await MultiplayerService.Instance
                    .GetJoinedSessionIdsAsync();
                return new HashSet<string>(
                    ids ?? new List<string>(),
                    StringComparer.Ordinal);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MP] stage=lobby-memberships-read result=" +
                    exception.GetBaseException().Message);
                return new HashSet<string>(StringComparer.Ordinal);
            }
        }

        private static async Task CleanupNewLobbyMembershipsAsync(
            HashSet<string> membershipsBefore,
            bool? createdByLocalPlayer)
        {
            HashSet<string> current = await JoinedSessionIdsOrEmptyAsync();
            foreach (string lobbyId in current)
            {
                if (string.IsNullOrWhiteSpace(lobbyId) ||
                    membershipsBefore.Contains(lobbyId))
                {
                    continue;
                }
                try
                {
                    bool deleteLobby = createdByLocalPlayer == true;
                    if (!createdByLocalPlayer.HasValue)
                    {
                        try
                        {
                            var lobby = await LobbyService.Instance
                                .GetLobbyAsync(lobbyId);
                            deleteLobby = string.Equals(
                                lobby?.HostId,
                                AuthenticationService.Instance.PlayerId,
                                StringComparison.Ordinal);
                        }
                        catch (Exception lookupException)
                        {
                            Debug.LogWarning(
                                "[MP] stage=lobby-ownership-read result=" +
                                lookupException.GetBaseException().Message);
                        }
                    }
                    if (deleteLobby)
                    {
                        await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                    }
                    else
                    {
                        await LobbyService.Instance.RemovePlayerAsync(
                            lobbyId,
                            AuthenticationService.Instance.PlayerId);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[MP] stage=lobby-membership-cleanup result=" +
                        exception.GetBaseException().Message);
                }
            }
        }

        private static bool IsLobbyEventConnectionFailure(
            Exception exception)
        {
            for (Exception current = exception;
                 current != null;
                 current = current.InnerException)
            {
                string message = current.Message ?? string.Empty;
                if (message.IndexOf(
                        "lobby events",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf(
                        "23000",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static Dictionary<string, PlayerProperty>
            CreatePlayerProperties(DuelDeckLoadout loadout, int seat)
        {
            return new Dictionary<string, PlayerProperty>
            {
                ["displayName"] = MemberPlayerProperty(
                    Limit(loadout?.playerDisplayName, 32)),
                ["deckHash"] = MemberPlayerProperty(ComputeDeckHash(loadout)),
                ["banlistId"] = MemberPlayerProperty(
                    loadout?.banlistId ?? string.Empty),
                ["ready"] = MemberPlayerProperty("false"),
                ["platform"] = MemberPlayerProperty(Application.platform.ToString()),
                ["seat"] = MemberPlayerProperty(seat.ToString()),
                ["connectionState"] = MemberPlayerProperty("connecting")
            };
        }

        private void ValidateCompatibility(
            string protocolVersion,
            string expectedMode)
        {
            if (currentSession == null)
                throw new InvalidOperationException("Sessao ausente apos entrada.");

            RequireProperty(AppVersionKey, ProjectIdentity.ProjectVersion);
            RequireProperty(ProtocolVersionKey, protocolVersion);
            RequireProperty(EngineVersionKey,
                ProjectIdentity.CoreApiVersion + "|" +
                ProjectIdentity.CoreCommit);
            RequireProperty(RulesetHashKey,
                ProjectIdentity.CardScriptsCommit);
            RequireProperty(CardDatabaseHashKey,
                ProjectIdentity.BabelCdbCommit);
            RequireProperty(BanlistVersionKey,
                BanlistService.ActiveBanlistId);
            RequireProperty(ModeKey, expectedMode);
        }

        private void RequireProperty(string key, string expected)
        {
            if (!currentSession.Properties.TryGetValue(
                    key,
                    out SessionProperty property) ||
                !string.Equals(
                    property?.Value,
                    expected,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Sala criada por uma versao incompativel do Card12.");
            }
        }

        private void Bind(ISession session)
        {
            Unbind();
            currentSession = session ?? throw new ArgumentNullException(
                nameof(session));
            currentSession.Deleted += OnSessionTerminated;
            currentSession.RemovedFromSession += OnSessionTerminated;
            currentSession.SessionHostChanged += OnHostChanged;
        }

        private void Unbind()
        {
            if (currentSession == null)
                return;
            currentSession.Deleted -= OnSessionTerminated;
            currentSession.RemovedFromSession -= OnSessionTerminated;
            currentSession.SessionHostChanged -= OnHostChanged;
        }

        private void OnSessionTerminated()
        {
            Debug.LogWarning("[MP] stage=session-terminated");
        }

        private static void OnHostChanged(string _)
        {
            // Host migration is intentionally disabled for this authoritative
            // OCG Core. A replacement client cannot safely reconstruct its
            // private native engine state.
            Debug.LogWarning("[MP] stage=host-changed action=end-match");
        }

        private static SessionProperty PublicProperty(
            string value,
            PropertyIndex index = PropertyIndex.None)
        {
            return new SessionProperty(
                value ?? string.Empty,
                VisibilityPropertyOptions.Public,
                index);
        }

        private static SessionProperty MemberSessionProperty(string value)
        {
            return new SessionProperty(
                value ?? string.Empty,
                VisibilityPropertyOptions.Member);
        }

        private static PlayerProperty MemberPlayerProperty(string value)
        {
            return new PlayerProperty(
                value ?? string.Empty,
                VisibilityPropertyOptions.Member);
        }

        private static string Limit(string value, int maximumLength)
        {
            value ??= string.Empty;
            return value.Length <= maximumLength
                ? value
                : value.Substring(0, maximumLength);
        }

        internal static string ComputeDeckHash(DuelDeckLoadout loadout)
        {
            if (loadout == null)
                return string.Empty;
            return DeckManifestHasher.ComputeSha256(
                loadout.banlistId,
                loadout.mainDeckCardIds,
                loadout.extraDeckCardIds,
                loadout.sideDeckCardIds);
        }

        internal static string ComputeCompatibilityHash()
        {
            return ComputeStableHash(ProjectIdentity.MultiplayerCompatibility);
        }

        private static double ComputeQuickJoinTimeoutSeconds()
        {
            // Different deterministic timeouts prevent two players who press
            // the button together from both creating an empty room on the
            // same retry tick. One becomes host while the other is still
            // querying and joins it.
            string playerId = AuthenticationService.Instance.PlayerId ??
                string.Empty;
            uint hash = 2166136261u;
            for (int i = 0; i < playerId.Length; i++)
            {
                hash ^= playerId[i];
                hash *= 16777619u;
            }
            return 2.0d + hash % 2500u / 1000d;
        }

        private static string ComputeStableHash(string value)
        {
            value ??= string.Empty;

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }
            return hash.ToString("x16");
        }

    }
}
