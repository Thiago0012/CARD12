using UnityEngine;

namespace ArcaneArena
{
    /// <summary>
    /// Garante que builds mobile nunca entrem em orientação de retrato.
    /// O PlayerSettings define a orientação inicial do processo; esta trava
    /// mantém as duas posições horizontais válidas durante toda a execução.
    /// </summary>
    public static class MobileLandscapeOrientation
    {
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void EnforceBeforeSplashScreen()
        {
#if UNITY_ANDROID || UNITY_IOS
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
        }
    }
}
