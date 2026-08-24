using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcaneDuel.Game.Accounts;
using ArcaneDuel.Game.Social;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Options;
using UnityEngine;
using UnityEngine.Networking;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    public sealed class PlayerFriendsRuntime : MonoBehaviour
    {
        private static PlayerFriendsRuntime _instance;
        private static Task _readyTask;

        private readonly List<FriendProfileView> _friends = new();
        private readonly List<FriendProfileView> _incoming = new();
        private readonly List<FriendProfileView> _outgoing = new();
        private string _localDisplayName = string.Empty;
        private bool _friendsSdkInitialized;
        private bool _eventsSubscribed;
        private bool _initialized;
        private bool _busy;
        private string _status = "Preparando conexões sociais...";

        public static event Action Changed;

        public static bool IsReady => _instance?._initialized == true;
        public static bool IsBusy => _instance?._busy == true;
        public static string Status =>
            _instance?._status ?? "Serviço social ainda não iniciado.";
        public static int FriendCount => _instance?._friends.Count ?? 0;
        public static int IncomingCount => _instance?._incoming.Count ?? 0;
        public static int OutgoingCount => _instance?._outgoing.Count ?? 0;
        public static IReadOnlyList<FriendProfileView> Friends =>
            CopyList(_instance?._friends);
        public static IReadOnlyList<FriendProfileView> IncomingRequests =>
            CopyList(_instance?._incoming);
        public static IReadOnlyList<FriendProfileView> OutgoingRequests =>
            CopyList(_instance?._outgoing);

        // Aguarda os inicializadores dos pacotes UGS concluírem o registro no
        // CoreRegistry antes de tentar obter FriendsService.Instance.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (_instance != null)
                return;
            var root = new GameObject("Central de Amigos");
            root.AddComponent<PlayerFriendsRuntime>();
        }

        public static Task EnsureReadyAsync()
        {
            EnsureExists();
            if (!_instance._initialized &&
                !_instance._busy &&
                (_readyTask == null || _readyTask.IsCompleted))
            {
                _readyTask = _instance.InitializeAsync();
            }
            return _readyTask ?? Task.CompletedTask;
        }

        public static void SetLocalDisplayName(string displayName)
        {
            EnsureExists();
            _instance._localDisplayName = (displayName ?? string.Empty).Trim();
            if (_instance._initialized)
                _ = _instance.SyncUnityPlayerNameAsync();
        }

        public static async Task<FriendProfileView> SearchAsync(string query)
        {
            await EnsureReadyAsync();
            if (!PlayerFriendSearchPolicy.TryNormalize(
                    query,
                    out string normalized,
                    out bool numeric,
                    out string rejection))
            {
                throw new InvalidOperationException(rejection);
            }

            FriendProfileView known = FindKnownProfile(normalized, numeric);
            if (known != null)
                return known;

            if (!IsReady)
            {
                throw new InvalidOperationException(
                    "O serviço de amigos ainda não está disponível. " + Status);
            }

            if (!PlayerIdAccessRuntime.IsCatalogConfigured)
            {
                if (!numeric && normalized.Contains("#"))
                {
                    return new FriendProfileView
                    {
                        displayName = DisplayNameFromUnityName(normalized),
                        unityPlayerName = normalized,
                        publicId = string.Empty,
                        connectionState = FriendConnectionState.None,
                        presence = FriendPresenceState.Unknown
                    };
                }

                throw new InvalidOperationException(
                    "A busca de novos jogadores será liberada quando o " +
                    "catálogo online estiver ativado. Amigos e pedidos já " +
                    "existentes continuam disponíveis.");
            }

            return await _instance.SearchCatalogAsync(normalized);
        }

        public static async Task SendRequestAsync(FriendProfileView profile)
        {
            await EnsureReadyAsync();
            RequireReady();
            if (profile == null)
                throw new InvalidOperationException("O perfil encontrado é inválido.");
            if (string.Equals(
                    profile.playerId,
                    PlayerIdAccessRuntime.CanonicalPlayerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Você não pode adicionar a própria conta.");
            }

            await _instance.RunOperationAsync(
                async () =>
                {
                    if (!string.IsNullOrWhiteSpace(profile.playerId))
                    {
                        await FriendsService.Instance.AddFriendAsync(
                            profile.playerId);
                    }
                    else if (!string.IsNullOrWhiteSpace(
                                 profile.unityPlayerName))
                    {
                        await FriendsService.Instance.AddFriendByNameAsync(
                            profile.unityPlayerName);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "O perfil não possui uma identidade online válida.");
                    }
                },
                "Solicitação enviada. O jogador aparecerá em Pedidos até aceitar.");
        }

        public static async Task AcceptAsync(string playerId)
        {
            await EnsureReadyAsync();
            RequireReady();
            await _instance.RunOperationAsync(
                () => FriendsService.Instance.AddFriendAsync(playerId),
                "Solicitação aceita. A conexão foi adicionada.");
        }

        public static async Task IgnoreIncomingAsync(string playerId)
        {
            await EnsureReadyAsync();
            RequireReady();
            await _instance.RunOperationAsync(
                () => FriendsService.Instance.DeleteIncomingFriendRequestAsync(
                    playerId),
                "Solicitação ignorada.");
        }

        public static async Task CancelOutgoingAsync(string playerId)
        {
            await EnsureReadyAsync();
            RequireReady();
            await _instance.RunOperationAsync(
                () => FriendsService.Instance.DeleteOutgoingFriendRequestAsync(
                    playerId),
                "Solicitação cancelada.");
        }

        public static async Task RemoveFriendAsync(string playerId)
        {
            await EnsureReadyAsync();
            RequireReady();
            await _instance.RunOperationAsync(
                () => FriendsService.Instance.DeleteFriendAsync(playerId),
                "Jogador removido da sua lista.");
        }

        public static async Task RefreshAsync()
        {
            await EnsureReadyAsync();
            RequireReady();
            await _instance.RunOperationAsync(
                () => FriendsService.Instance.ForceRelationshipsRefreshAsync(),
                "Lista de conexões atualizada.");
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _readyTask = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _busy = true;
            NotifyChanged();
            try
            {
                await PlayerIdAccessRuntime.EnsureReadyAsync();
                if (UnityServices.State !=
                    ServicesInitializationState.Initialized)
                {
                    throw new InvalidOperationException(
                        "Os serviços da Unity ainda não concluíram a inicialização.");
                }
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    throw new InvalidOperationException(
                        "A conta não foi autenticada pela Unity.");
                }

                await SyncUnityPlayerNameAsync();
                if (!_friendsSdkInitialized)
                {
                    var options = new InitializeOptions()
                        .WithMemberProfile(true)
                        .WithMemberPresence(true)
                        .WithEvents(true);
                    await FriendsService.Instance.InitializeAsync(options);
                    _friendsSdkInitialized = true;
                }
                SubscribeToEvents();
                await FriendsService.Instance.SetPresenceAvailabilityAsync(
                    Availability.Online);
                _initialized = true;
                _status = "Conexões online sincronizadas.";
                RebuildLists();
            }
            catch (Exception exception)
            {
                _initialized = false;
                _status = DescribeFailure(exception);
                Debug.LogWarning("[Amigos] " + _status);
            }
            finally
            {
                _busy = false;
                NotifyChanged();
            }
        }

        private async Task SyncUnityPlayerNameAsync()
        {
            if (!AuthenticationService.Instance.IsSignedIn ||
                string.IsNullOrWhiteSpace(_localDisplayName))
            {
                return;
            }

            string desired = SanitizeUnityPlayerName(_localDisplayName);
            if (desired.Length < 3)
                return;
            string current = AuthenticationService.Instance.PlayerName ??
                             string.Empty;
            int suffixIndex = current.LastIndexOf('#');
            string currentBase = suffixIndex > 0
                ? current.Substring(0, suffixIndex)
                : current;
            if (string.Equals(currentBase, desired, StringComparison.Ordinal))
                return;
            await AuthenticationService.Instance.UpdatePlayerNameAsync(desired);
        }

        private async Task<FriendProfileView> SearchCatalogAsync(string query)
        {
            _busy = true;
            _status = "Procurando duelista no Nexo...";
            NotifyChanged();
            try
            {
                string url = PlayerIdAccessRuntime.CatalogBaseUrl +
                             "/v1/player/search?query=" +
                             UnityWebRequest.EscapeURL(query);
                using UnityWebRequest request = UnityWebRequest.Get(url);
                request.timeout =
                    PlayerIdAccessRuntime.CatalogRequestTimeoutSeconds;
                request.SetRequestHeader(
                    "Authorization",
                    "Bearer " + AuthenticationService.Instance.AccessToken);
                UnityWebRequestAsyncOperation send = request.SendWebRequest();
                while (!send.isDone)
                    await Task.Yield();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException(
                        $"A busca respondeu HTTP {request.responseCode}: " +
                        request.error);
                }

                FriendSearchResponse response = JsonUtility.FromJson<
                    FriendSearchResponse>(request.downloadHandler.text);
                if (response == null || !response.found)
                {
                    throw new InvalidOperationException(
                        !string.IsNullOrWhiteSpace(response?.message)
                            ? response.message
                            : "Nenhum jogador foi encontrado com esse nome ou ID.");
                }
                if (string.IsNullOrWhiteSpace(response.playerId) ||
                    !PlayerIdAccessPolicy.IsValidPublicId(response.publicId))
                {
                    throw new InvalidOperationException(
                        "O catálogo devolveu um perfil incompleto.");
                }

                FriendProfileView profile = response.ToProfile();
                ApplyKnownRelationship(profile);
                _status = "Perfil encontrado.";
                return profile;
            }
            finally
            {
                _busy = false;
                NotifyChanged();
            }
        }

        private async Task RunOperationAsync(
            Func<Task> operation,
            string successMessage)
        {
            if (_busy)
                throw new InvalidOperationException("Aguarde a operação atual.");
            _busy = true;
            _status = "Sincronizando conexões...";
            NotifyChanged();
            try
            {
                await operation();
                await FriendsService.Instance.ForceRelationshipsRefreshAsync();
                RebuildLists();
                _status = successMessage;
            }
            catch (Exception exception)
            {
                _status = DescribeFailure(exception);
                throw new InvalidOperationException(_status, exception);
            }
            finally
            {
                _busy = false;
                NotifyChanged();
            }
        }

        private void SubscribeToEvents()
        {
            if (_eventsSubscribed)
                return;
            FriendsService.Instance.RelationshipAdded +=
                _ => HandleRelationshipChanged();
            FriendsService.Instance.RelationshipDeleted +=
                _ => HandleRelationshipChanged();
            FriendsService.Instance.PresenceUpdated +=
                _ => HandleRelationshipChanged();
            _eventsSubscribed = true;
        }

        private void HandleRelationshipChanged()
        {
            RebuildLists();
            _status = "A lista de conexões recebeu uma atualização.";
            NotifyChanged();
        }

        private void RebuildLists()
        {
            _friends.Clear();
            _incoming.Clear();
            _outgoing.Clear();
            if (!_initialized)
                return;

            AppendRelationships(
                FriendsService.Instance.Friends,
                FriendConnectionState.Friend,
                _friends);
            AppendRelationships(
                FriendsService.Instance.IncomingFriendRequests,
                FriendConnectionState.IncomingRequest,
                _incoming);
            AppendRelationships(
                FriendsService.Instance.OutgoingFriendRequests,
                FriendConnectionState.OutgoingRequest,
                _outgoing);
        }

        private static void AppendRelationships(
            IReadOnlyList<Relationship> relationships,
            FriendConnectionState state,
            List<FriendProfileView> target)
        {
            if (relationships == null)
                return;
            foreach (Relationship relationship in relationships)
            {
                if (relationship?.Member == null)
                    continue;
                target.Add(ToProfile(relationship.Member, state));
            }
            target.Sort((left, right) => string.Compare(
                left.displayName,
                right.displayName,
                StringComparison.CurrentCultureIgnoreCase));
        }

        private static FriendProfileView ToProfile(
            Member member,
            FriendConnectionState state)
        {
            string unityName = member.Profile?.Name ?? string.Empty;
            return new FriendProfileView
            {
                playerId = member.Id ?? string.Empty,
                publicId = PlayerIdAccessPolicy.FormatPublicId(member.Id),
                displayName = DisplayNameFromUnityName(unityName),
                unityPlayerName = unityName,
                equippedIconId = ProfileIconCatalog.DefaultIconId,
                connectionState = state,
                presence = ResolvePresence(member.Presence?.Availability),
                lastSeenUtcUnixSeconds = member.Presence == null
                    ? 0
                    : new DateTimeOffset(member.Presence.LastSeen)
                        .ToUnixTimeSeconds()
            };
        }

        private static FriendPresenceState ResolvePresence(
            Availability? availability)
        {
            return availability switch
            {
                Availability.Online => FriendPresenceState.Online,
                Availability.Busy => FriendPresenceState.Busy,
                Availability.Away => FriendPresenceState.Away,
                Availability.Offline => FriendPresenceState.Offline,
                Availability.Invisible => FriendPresenceState.Offline,
                _ => FriendPresenceState.Unknown
            };
        }

        private static FriendProfileView FindKnownProfile(
            string query,
            bool numeric)
        {
            IEnumerable<FriendProfileView> all = Friends
                .Concat(IncomingRequests)
                .Concat(OutgoingRequests);
            return all.FirstOrDefault(profile => numeric
                ? string.Equals(profile.publicId, query,
                    StringComparison.Ordinal)
                : string.Equals(profile.displayName, query,
                      StringComparison.CurrentCultureIgnoreCase) ||
                  string.Equals(profile.unityPlayerName, query,
                      StringComparison.OrdinalIgnoreCase));
        }

        private static void ApplyKnownRelationship(FriendProfileView profile)
        {
            if (profile == null)
                return;
            FriendProfileView known = Friends.Concat(IncomingRequests)
                .Concat(OutgoingRequests)
                .FirstOrDefault(candidate => string.Equals(
                    candidate.playerId,
                    profile.playerId,
                    StringComparison.Ordinal));
            if (known == null)
                return;
            profile.connectionState = known.connectionState;
            profile.presence = known.presence;
        }

        private static string SanitizeUnityPlayerName(string displayName)
        {
            var characters = new List<char>(45);
            foreach (char character in displayName ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character) ||
                    character == '_' || character == '-')
                {
                    characters.Add(character);
                }
                if (characters.Count == 45)
                    break;
            }
            return new string(characters.ToArray());
        }

        private static string DisplayNameFromUnityName(string unityName)
        {
            if (string.IsNullOrWhiteSpace(unityName))
                return "DUELISTA";
            int suffix = unityName.LastIndexOf('#');
            return suffix > 0
                ? unityName.Substring(0, suffix)
                : unityName;
        }

        private static string DescribeFailure(Exception exception)
        {
            string detail = exception?.GetBaseException().Message ??
                            "falha desconhecida";
            return "Não foi possível sincronizar os amigos. Confirme que o " +
                   "serviço Friends está habilitado no Unity Dashboard. " +
                   detail;
        }

        private static void RequireReady()
        {
            if (!IsReady)
                throw new InvalidOperationException(Status);
        }

        private static IReadOnlyList<FriendProfileView> CopyList(
            List<FriendProfileView> source)
        {
            return source == null
                ? Array.Empty<FriendProfileView>()
                : source.Select(item => item.Copy()).ToArray();
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private async void OnApplicationQuit()
        {
            if (!_initialized)
                return;
            try
            {
                await FriendsService.Instance.SetPresenceAvailabilityAsync(
                    Availability.Offline);
            }
            catch
            {
                // O encerramento do processo não deve ser bloqueado pela rede.
            }
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;
            _instance = null;
            _readyTask = null;
        }
    }
}
