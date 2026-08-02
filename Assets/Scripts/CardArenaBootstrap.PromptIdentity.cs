using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private bool hasPresentedPromptIdentity;
        private bool presentedPromptWasNull;
        private CoreMessage presentedPromptMessage;
        private byte presentedPromptPlayer;
        private DuelPrompt presentedPromptReference;

        private bool IsPromptPresentationCurrent(DuelPrompt prompt)
        {
            if (!hasPresentedPromptIdentity)
                return false;
            if (prompt == null)
                return presentedPromptWasNull;
            if (presentedPromptWasNull)
                return false;
            return SamePromptIdentity(prompt, presentedPromptReference) &&
                   prompt.Message == presentedPromptMessage &&
                   prompt.Player == presentedPromptPlayer;
        }

        private void MarkPromptPresented(DuelPrompt prompt)
        {
            hasPresentedPromptIdentity = true;
            presentedPromptWasNull = prompt == null;
            presentedPromptReference = prompt;
            presentedPromptMessage = prompt?.Message ?? 0;
            presentedPromptPlayer = prompt?.Player ?? 0;
        }

        private void ResetPromptPresentationIdentity()
        {
            hasPresentedPromptIdentity = false;
            presentedPromptWasNull = false;
            presentedPromptMessage = 0;
            presentedPromptPlayer = 0;
            presentedPromptReference = null;
        }

        private static bool SamePromptIdentity(
            DuelPrompt first,
            DuelPrompt second)
        {
            if (ReferenceEquals(first, second))
                return true;
            if (first == null || second == null)
                return false;
            if (first.RequestId == 0 || second.RequestId == 0)
                return false;
            return first.RequestId == second.RequestId &&
                   first.Message == second.Message &&
                   first.Player == second.Player;
        }
    }
}
