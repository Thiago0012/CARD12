using System;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        public string AuthenticatedPlayerId =>
            State?.authenticatedPlayerId?.Trim() ?? string.Empty;

        public string CanonicalPlayerId =>
            !string.IsNullOrWhiteSpace(AuthenticatedPlayerId)
                ? AuthenticatedPlayerId
                : State?.localProfileId?.Trim() ?? string.Empty;

        public bool TryBindAuthenticatedPlayerId(
            string playerId,
            out string rejection)
        {
            rejection = string.Empty;
            if (State == null)
            {
                rejection = "O perfil local ainda não foi carregado.";
                return false;
            }

            string normalized = (playerId ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                rejection = "O ID autenticado recebido é inválido.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(State.authenticatedPlayerId) &&
                !string.Equals(
                    State.authenticatedPlayerId,
                    normalized,
                    StringComparison.Ordinal))
            {
                rejection =
                    "Este save local já está vinculado a outra conta autenticada.";
                return false;
            }

            if (string.Equals(
                    State.authenticatedPlayerId,
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }

            State.authenticatedPlayerId = normalized;
            Save();
            return true;
        }
    }
}
