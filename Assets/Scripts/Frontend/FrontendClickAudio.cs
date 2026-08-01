using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Frontend
{
    internal static class FrontendClickAudio
    {
        private const string DuelArenaSceneName = "DuelArena";
        private const string ResourcePath = "Frontend/MainMenuUiAssets";

        private static AudioSource _source;
        private static MainMenuUiAssets _assets;

        public static void Play()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (!ArcaneAudioPreferences.Enabled ||
                sceneName == DuelArenaSceneName ||
                sceneName == ProjectIdentity.DuelScene)
            {
                return;
            }

            _assets ??= Resources.Load<MainMenuUiAssets>(ResourcePath);
            if (_assets == null || _assets.interfaceClick == null)
                return;

            EnsureSource();
            _source.PlayOneShot(
                _assets.interfaceClick,
                ArcaneAudioPreferences.Volume);
        }

        private static void EnsureSource()
        {
            if (_source != null)
                return;

            var audioObject = new GameObject("Áudio da Interface");
            Object.DontDestroyOnLoad(audioObject);
            _source = audioObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
        }
    }
}
