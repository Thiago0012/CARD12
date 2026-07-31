using UnityEngine.InputSystem;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Centralizes frame-based keyboard shortcuts on the Input System used by
    /// every scene EventSystem. Pointer and touch input remain owned by Unity UI.
    /// </summary>
    internal static class ArcaneInput
    {
        public static bool EnterPressedThisFrame
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null &&
                       (keyboard.enterKey.wasPressedThisFrame ||
                        keyboard.numpadEnterKey.wasPressedThisFrame);
            }
        }

        public static bool EscapePressedThisFrame =>
            Keyboard.current?.escapeKey.wasPressedThisFrame == true;

        public static bool RefreshPressedThisFrame =>
            Keyboard.current?.f5Key.wasPressedThisFrame == true;
    }
}
