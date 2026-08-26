using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public static class PlayerAccountRuntime
    {
        private static bool _restoreRequestedForNextMenu;

        public static event Action Changed;

        public static bool IsProtected =>
            !string.IsNullOrWhiteSpace(
                AuthenticationService.Instance.PlayerInfo?.Username);

        public static string AccountUsername =>
            AuthenticationService.Instance.PlayerInfo?.Username ??
            string.Empty;

        public static void RequestRestoreOnNextMenu()
        {
            _restoreRequestedForNextMenu = true;
        }

        public static bool ConsumeRestoreRequest()
        {
            bool requested = _restoreRequestedForNextMenu;
            _restoreRequestedForNextMenu = false;
            return requested;
        }

        public static async Task RefreshProtectionStateAsync()
        {
            await PlayerIdAccessRuntime.EnsureReadyAsync();
            if (AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.GetPlayerInfoAsync();
            Changed?.Invoke();
        }

        public static async Task ProtectCurrentAccountAsync(
            string username,
            string password)
        {
            ValidateCredentials(username, password);
            await PlayerIdAccessRuntime.EnsureReadyAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                throw new InvalidOperationException(
                    "A conta atual ainda não foi autenticada.");

            string originalPlayerId = AuthenticationService.Instance.PlayerId;
            try
            {
                await PlayerCloudSaveRuntime.UploadNowAsync();
            }
            catch
            {
                // A identidade pode ser protegida mesmo que o Cloud Save
                // esteja temporariamente fora do ar. O envio será repetido.
            }
            await AuthenticationService.Instance.AddUsernamePasswordAsync(
                username.Trim(),
                password);
            if (!string.Equals(
                    originalPlayerId,
                    AuthenticationService.Instance.PlayerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A proteção tentou trocar a identidade da conta atual.");
            }
            await AuthenticationService.Instance.GetPlayerInfoAsync();
            try
            {
                await PlayerCloudSaveRuntime.UploadNowAsync();
            }
            catch
            {
                // Mantém a conta protegida e preserva o cache local.
            }
            Changed?.Invoke();
        }

        public static async Task SignInExistingAccountAsync(
            string username,
            string password)
        {
            ValidateCredentials(username, password);
            await PlayerIdAccessRuntime.EnsureReadyAsync();
            string normalizedUsername = username.Trim();
            string previousPlayerId = AuthenticationService.Instance.PlayerId;
            string previousUsername = string.Empty;
            if (AuthenticationService.Instance.IsSignedIn)
            {
                try
                {
                    await AuthenticationService.Instance.GetPlayerInfoAsync();
                    previousUsername = AuthenticationService.Instance
                        .PlayerInfo?.Username ?? string.Empty;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[Conta] Os vínculos da sessão atual não puderam ser " +
                        "consultados antes da restauração: " +
                        exception.GetBaseException().Message);
                }

                if (string.Equals(
                        previousUsername,
                        normalizedUsername,
                        StringComparison.OrdinalIgnoreCase))
                {
                    await RestoreSignedInAccountAsync();
                    return;
                }

                try
                {
                    await PlayerCloudSaveRuntime.UploadNowAsync();
                }
                catch (Exception exception)
                {
                    if (PlayerCloudSaveRuntime.HasLocalProfile)
                    {
                        throw new InvalidOperationException(
                            "O perfil atual ainda não foi salvo na nuvem. " +
                            "Sincronize-o antes de trocar de conta.",
                            exception);
                    }
                }
                // Mantém o token da sessão anterior até o novo login ser
                // confirmado. Uma senha errada não pode deixar o save órfão.
                AuthenticationService.Instance.SignOut(false);
            }

            try
            {
                await AuthenticationService.Instance
                    .SignInWithUsernamePasswordAsync(
                        normalizedUsername,
                        password);
            }
            catch (Exception exception)
            {
                bool restored = await RestorePreviousSessionAsync(
                    previousPlayerId);
                throw new InvalidOperationException(
                    restored
                        ? "Não foi possível entrar nessa conta. A conta que " +
                          "já estava neste aparelho foi preservada."
                        : "Não foi possível entrar nessa conta. Confira as " +
                          "credenciais e tente novamente.",
                    exception);
            }
            await AuthenticationService.Instance.GetPlayerInfoAsync();
            await RebindSessionServicesAsync();
            await PlayerCloudSaveRuntime.RestoreForCurrentAccountAsync();
            if (PlayerCloudSaveRuntime.State ==
                PlayerCloudSaveState.Synchronized)
            {
                await RefreshRestoredProfileAsync();
            }
            Changed?.Invoke();
        }

        public static async Task<bool> AccessOrCreateAccountAsync(
            string username,
            string password)
        {
            ValidateCredentials(username, password);
            await PlayerIdAccessRuntime.EnsureReadyAsync();
            string normalizedUsername = username.Trim();
            string previousPlayerId = AuthenticationService.Instance.PlayerId;
            string previousUsername = await TryGetCurrentUsernameAsync();

            if (AuthenticationService.Instance.IsSignedIn &&
                string.Equals(
                    previousUsername,
                    normalizedUsername,
                    StringComparison.OrdinalIgnoreCase))
            {
                await RestoreSignedInAccountAsync();
                return false;
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                try
                {
                    await PlayerCloudSaveRuntime.UploadNowAsync();
                }
                catch (Exception exception)
                {
                    if (!string.IsNullOrWhiteSpace(previousUsername) &&
                        PlayerCloudSaveRuntime.HasLocalProfile)
                    {
                        throw new InvalidOperationException(
                            "A conta atual ainda não foi sincronizada. " +
                            "Use SINCRONIZAR AGORA antes de trocar de conta.",
                            exception);
                    }
                    Debug.LogWarning(
                        "[Conta] O save convidado permaneceu local antes da " +
                        "tentativa de acesso: " +
                        exception.GetBaseException().Message);
                }
                AuthenticationService.Instance.SignOut(false);
            }

            Exception signInFailure;
            try
            {
                await AuthenticationService.Instance
                    .SignInWithUsernamePasswordAsync(
                        normalizedUsername,
                        password);
                await AuthenticationService.Instance.GetPlayerInfoAsync();
                await RebindSessionServicesAsync();
                await PlayerCloudSaveRuntime.RestoreForCurrentAccountAsync();
                if (PlayerCloudSaveRuntime.State ==
                    PlayerCloudSaveState.Synchronized)
                {
                    await RefreshRestoredProfileAsync();
                }
                Changed?.Invoke();
                return false;
            }
            catch (Exception exception)
            {
                signInFailure = exception;
            }

            bool restored = await RestorePreviousSessionAsync(previousPlayerId);
            if (!restored)
            {
                throw new InvalidOperationException(
                    "Não foi possível entrar nem recuperar com segurança a " +
                    "identidade que já estava neste aparelho.",
                    signInFailure);
            }

            string restoredUsername = await TryGetCurrentUsernameAsync();
            if (!string.IsNullOrWhiteSpace(restoredUsername))
            {
                throw new InvalidOperationException(
                    "Usuário ou senha inválidos. A conta que já estava neste " +
                    "aparelho foi preservada.",
                    signInFailure);
            }

            try
            {
                await ProtectCurrentAccountAsync(
                    normalizedUsername,
                    password);
                return true;
            }
            catch (AuthenticationException exception) when (
                exception.ErrorCode ==
                    AuthenticationErrorCodes.AccountAlreadyLinked ||
                exception.ErrorCode ==
                    AuthenticationErrorCodes.AccountLinkLimitExceeded)
            {
                throw new InvalidOperationException(
                    "Esse usuário já existe, mas a senha informada não " +
                    "corresponde.",
                    exception);
            }
        }

        private static async Task<string> TryGetCurrentUsernameAsync()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
                return string.Empty;
            try
            {
                await AuthenticationService.Instance.GetPlayerInfoAsync();
                return AuthenticationService.Instance.PlayerInfo?.Username ??
                       string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Conta] O nome de acesso atual não pôde ser consultado: " +
                    exception.GetBaseException().Message);
                return string.Empty;
            }
        }

        private static async Task RestoreSignedInAccountAsync()
        {
            await PlayerCloudSaveRuntime.RestoreForCurrentAccountAsync();
            if (PlayerCloudSaveRuntime.State ==
                PlayerCloudSaveState.Synchronized)
            {
                await RefreshRestoredProfileAsync();
            }
            else
            {
                await RebindSessionServicesAsync();
            }
            Changed?.Invoke();
        }

        private static async Task RefreshRestoredProfileAsync()
        {
            PlayerFriendsRuntime.SetLocalDisplayName(
                PlayerCloudSaveRuntime.LocalPlayerDisplayName);
            try
            {
                await PlayerIdAccessRuntime.RefreshNowAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Conta] O perfil restaurado será publicado no próximo " +
                    "heartbeat: " + exception.GetBaseException().Message);
            }
            await PlayerFriendsRuntime.RebindCurrentAuthenticationAsync();
            await FriendDuelChallengeRuntime
                .RebindCurrentAuthenticationAsync();
        }

        private static async Task<bool> RestorePreviousSessionAsync(
            string previousPlayerId)
        {
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance
                        .SignInAnonymouslyAsync();
                }
                bool restored = string.IsNullOrWhiteSpace(previousPlayerId)
                    ? AuthenticationService.Instance.IsSignedIn
                    : string.Equals(
                        AuthenticationService.Instance.PlayerId,
                        previousPlayerId,
                        StringComparison.Ordinal);
                if (restored)
                    await RebindSessionServicesAsync();
                return restored;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Conta] A sessão anterior não pôde ser retomada: " +
                    exception.GetBaseException().Message);
                return false;
            }
        }

        private static async Task RebindSessionServicesAsync()
        {
            try
            {
                await PlayerIdAccessRuntime.RebindCurrentAuthenticationAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Conta] O catálogo de IDs reconectará depois: " +
                    exception.GetBaseException().Message);
            }

            await PlayerFriendsRuntime.RebindCurrentAuthenticationAsync();
            await FriendDuelChallengeRuntime
                .RebindCurrentAuthenticationAsync();
        }

        private static void ValidateCredentials(
            string username,
            string password)
        {
            string normalized = (username ?? string.Empty).Trim();
            if (normalized.Length < 3 || normalized.Length > 20)
                throw new ArgumentException(
                    "O usuário deve ter de 3 a 20 caracteres.");
            foreach (char character in normalized)
            {
                if (char.IsLetterOrDigit(character) ||
                    character == '.' || character == '-' ||
                    character == '@' || character == '_')
                {
                    continue;
                }
                throw new ArgumentException(
                    "No usuário, use apenas letras, números, ponto, hífen, @ ou sublinhado.");
            }

            if (string.IsNullOrEmpty(password) ||
                password.Length < 8 || password.Length > 30)
            {
                throw new ArgumentException(
                    "A senha deve ter de 8 a 30 caracteres.");
            }
            bool upper = false;
            bool lower = false;
            bool number = false;
            bool symbol = false;
            foreach (char character in password)
            {
                upper |= char.IsUpper(character);
                lower |= char.IsLower(character);
                number |= char.IsDigit(character);
                symbol |= !char.IsLetterOrDigit(character);
            }
            if (!upper || !lower || !number || !symbol)
            {
                throw new ArgumentException(
                    "A senha precisa de maiúscula, minúscula, número e símbolo.");
            }
        }
    }
}
