using System;
using System.Collections;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Keeps the menu theme alive while navigating through frontend scenes.
    /// It does not own any gameplay state and stops before a duel begins.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class MainMenuMusicController : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string DeckEditorSceneName = "DeckEditor";
        private const string ThemeResourcePath =
            "Audio/Music/ThemeSong";
        private const float FadeInDuration = 1.5f;
        private const float FadeOutDuration = 0.4f;

        private static MainMenuMusicController _instance;

        private AudioSource _source;
        private Coroutine _fadeRoutine;
        private bool _frontendActive;
        private bool _playbackRequested;
        private float _volumeEnvelope;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreatePersistentPlayer()
        {
            if (_instance != null)
                return;

            var musicObject = new GameObject("Musica do Menu Principal");
            musicObject.AddComponent<MainMenuMusicController>();
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

            _source = GetComponent<AudioSource>();
            if (_source == null)
                _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.priority = 32;
            _source.ignoreListenerPause = true;
            _source.clip = Resources.Load<AudioClip>(ThemeResourcePath);

            if (_source.clip == null)
            {
                Debug.LogWarning(
                    $"Musica do menu nao encontrada em Resources/{ThemeResourcePath}.");
            }

            ApplyOutputVolume();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void Start()
        {
            SetFrontendActive(
                IsFrontendScene(SceneManager.GetActiveScene().name));
        }

        private void Update()
        {
            ApplyOutputVolume();

            if (!_frontendActive ||
                !_playbackRequested ||
                _fadeRoutine != null ||
                _source == null ||
                _source.clip == null ||
                _source.isPlaying)
            {
                return;
            }

            // AudioSource.loop would restart with a hard cut. Restarting here
            // lets every complete replay receive the same gradual fade-in.
            StartFromBeginning();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            SetFrontendActive(IsFrontendScene(current.name));
        }

        private void SetFrontendActive(bool active)
        {
            if (active == _frontendActive)
                return;

            _frontendActive = active;
            if (_frontendActive)
            {
                StartFromBeginning();
                return;
            }

            StopWithFade();
        }

        private void StartFromBeginning()
        {
            if (_source == null || _source.clip == null)
                return;

            CancelFade();
            _playbackRequested = true;
            _source.Stop();
            _source.time = 0f;
            _volumeEnvelope = 0f;
            ApplyOutputVolume();
            _source.Play();
            _fadeRoutine = StartCoroutine(
                FadeEnvelope(1f, FadeInDuration, false));
        }

        private void StopWithFade()
        {
            _playbackRequested = false;
            CancelFade();

            if (_source == null || !_source.isPlaying)
            {
                StopAndRewind();
                return;
            }

            _fadeRoutine = StartCoroutine(
                FadeEnvelope(0f, FadeOutDuration, true));
        }

        private IEnumerator FadeEnvelope(
            float target,
            float duration,
            bool stopAfterFade)
        {
            float start = _volumeEnvelope;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / safeDuration);
                _volumeEnvelope = Mathf.Lerp(
                    start,
                    target,
                    Mathf.SmoothStep(0f, 1f, progress));
                ApplyOutputVolume();
                yield return null;
            }

            _volumeEnvelope = target;
            ApplyOutputVolume();
            _fadeRoutine = null;

            if (stopAfterFade)
                StopAndRewind();
        }

        private void CancelFade()
        {
            if (_fadeRoutine == null)
                return;

            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        private void StopAndRewind()
        {
            if (_source != null)
            {
                _source.Stop();
                _source.time = 0f;
            }

            _volumeEnvelope = 0f;
            ApplyOutputVolume();
        }

        private void ApplyOutputVolume()
        {
            if (_source == null)
                return;

            ArcaneMusicPreferences.ApplyTo(
                _source,
                _volumeEnvelope);
        }

        private static bool IsFrontendScene(string sceneName)
        {
            return string.Equals(
                       sceneName,
                       MainMenuSceneName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       sceneName,
                       DeckEditorSceneName,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
