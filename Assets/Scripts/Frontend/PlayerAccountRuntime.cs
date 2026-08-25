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
            string previousPlayerId = AuthenticationService.Instance.PlayerId;
            if (AuthenticationService.Instance.IsSignedIn)
            {
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
                        username.Trim(),
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
            await PlayerCloudSaveRuntime.ReloadForCurrentAccountAsync();
            if (PlayerCloudSaveRuntime.State !=
                PlayerCloudSaveState.Synchronized)
            {
                throw new InvalidOperationException(
                    "A conta foi autenticada, mas os dados ainda não puderam " +
                    "ser restaurados. " + PlayerCloudSaveRuntime.Status);
            }
            await RebindSessionServicesAsync();
            Changed?.Invoke();
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
                bool restored = string.Equals(
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
