using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;

namespace ArcaneArena.Frontend
{
    public static class PlayerAccountRuntime
    {
        public static event Action Changed;

        public static bool IsProtected =>
            !string.IsNullOrWhiteSpace(
                AuthenticationService.Instance.PlayerInfo?.Username);

        public static string AccountUsername =>
            AuthenticationService.Instance.PlayerInfo?.Username ??
            string.Empty;

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
            if (AuthenticationService.Instance.IsSignedIn)
            {
                try
                {
                    await PlayerCloudSaveRuntime.UploadNowAsync();
                }
                catch
                {
                    // O login não deve ficar preso se a conta anônima vazia
                    // ainda não tiver Cloud Save habilitado no Dashboard.
                }
                AuthenticationService.Instance.SignOut(true);
            }

            await AuthenticationService.Instance
                .SignInWithUsernamePasswordAsync(
                    username.Trim(),
                    password);
            await AuthenticationService.Instance.GetPlayerInfoAsync();
            await PlayerCloudSaveRuntime.ReloadForCurrentAccountAsync();
            await PlayerIdAccessRuntime.RebindCurrentAuthenticationAsync();
            Changed?.Invoke();
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
