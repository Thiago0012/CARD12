#if UNITY_EDITOR
using System;

namespace ArcaneArena.Editor.DeveloperTools
{
    public interface IZeroCoinGrantBridge
    {
        bool IsPlaying { get; }
        bool IsPaused { get; }
        bool IsGameViewFocused { get; }
        bool AlphaZeroIsPressed { get; }
        bool NumpadZeroIsPressed { get; }
        bool IsAllowedScreen { get; }
        bool IsInDuel { get; }
        bool IsTextInputFocused { get; }
        bool IsTransactionBusy { get; }
        bool IsWalletReady { get; }
        bool TryGrantCoins(
            int amount,
            string reason,
            string idempotencyKey,
            out int balanceAfter,
            out string rejection);
        void Notify(string message, bool error);
    }

    public sealed class ZeroCoinGrantController
    {
        public const int RewardAmount = 1000;
        public const string RewardReason = "EditorSelectedCardZero";

        private readonly string sessionId;
        private int activation;
        private bool busy;
        private bool zeroWasDown;

        public ZeroCoinGrantController(string stableSessionId = null)
        {
            sessionId = string.IsNullOrWhiteSpace(stableSessionId)
                ? Guid.NewGuid().ToString("N")
                : stableSessionId.Trim();
        }

        public bool Tick(IZeroCoinGrantBridge bridge)
        {
            if (bridge == null)
                return false;

            bool zeroIsDown = bridge.AlphaZeroIsPressed ||
                bridge.NumpadZeroIsPressed;
            bool pressed = zeroIsDown && !zeroWasDown;
            zeroWasDown = zeroIsDown;
            if (!pressed)
                return false;
            if (busy)
                return false;
            if (!bridge.IsPlaying)
            {
                bridge.Notify("DEV: entre no Play Mode para usar as moedas.", true);
                return false;
            }
            if (bridge.IsPaused)
            {
                bridge.Notify("DEV: retire a pausa antes de pressionar 0.", true);
                return false;
            }
            if (!bridge.IsGameViewFocused)
            {
                bridge.Notify("DEV: clique na aba Game e pressione 0 novamente.",
                    true);
                return false;
            }
            if (bridge.IsInDuel)
            {
                bridge.Notify("DEV: a concessão está bloqueada durante o duelo.",
                    true);
                return false;
            }
            if (bridge.IsTextInputFocused)
            {
                bridge.Notify("DEV: saia do campo de texto e pressione 0 novamente.",
                    true);
                return false;
            }
            if (!bridge.IsAllowedScreen)
            {
                bridge.Notify(
                    "DEV: abra a Loja ou o Editor de Deck e pressione 0.", true);
                return false;
            }
            if (bridge.IsTransactionBusy)
            {
                bridge.Notify("DEV: aguarde a operação da loja terminar.", true);
                return false;
            }
            if (!bridge.IsWalletReady)
            {
                bridge.Notify("DEV: a carteira ainda não está disponível.", true);
                return false;
            }

            busy = true;
            try
            {
                string requestId = string.Concat(
                    "editor-zero:",
                    sessionId,
                    ":",
                    (++activation).ToString());
                if (!bridge.TryGrantCoins(
                        RewardAmount,
                        RewardReason,
                        requestId,
                        out int balanceAfter,
                        out string rejection))
                {
                    bridge.Notify(
                        string.IsNullOrWhiteSpace(rejection)
                            ? "DEV: não foi possível conceder as moedas."
                            : "DEV: " + rejection,
                        true);
                    return false;
                }

                bridge.Notify(
                    "+1.000 moedas (DEV)  •  saldo " +
                    balanceAfter.ToString("N0"),
                    false);
                return true;
            }
            finally
            {
                busy = false;
            }
        }
    }
}
#endif
