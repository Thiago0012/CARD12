using System;
using System.Collections;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Trilha exclusiva da oficina de decks. Alterna duas fontes no último
    /// segundo para que a repetição comece antes do término da anterior.
    /// </summary>
    [DefaultExecutionOrder(-895)]
    public sealed class DeckEditorMusicController : MonoBehaviour
    {
        private const string DeckEditorSceneName = "DeckEditor";
        private const string MainMenuSceneName = "MainMenu";
        private const string ResourcePath =
            "Audio/Music/DeckEditor/deckeditorsong";
        private const float TransitionSeconds = 0.30f;
        private const float LoopOverlapSeconds = 1.0f;

        private static DeckEditorMusicController instance;

        private AudioSource source;
        private AudioSource transitionSource;
        private AudioClip track;
        private Coroutine playbackRoutine;
        private float sourceEnvelope;
        private float transitionEnvelope;
        private bool playbackRequested;

        public static void SetPlaybackActive(bool active)
        {
            DeckEditorMusicController player = active
                ? EnsureInstance()
                : instance;
            if (player != null)
                player.SetActiveInternal(active);
        }

        private static DeckEditorMusicController EnsureInstance()
        {
            if (instance != null)
                return instance;
            GameObject root = new("Musica do Editor de Decks");
            return root.AddComponent<DeckEditorMusicController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            source = gameObject.AddComponent<AudioSource>();
            transitionSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(source);
            ConfigureSource(transitionSource);
            track = Resources.Load<AudioClip>(ResourcePath);
            ApplyOutputVolume();
            if (track == null)
            {
                Debug.LogWarning(
                    $"Musica do editor nao encontrada em Resources/{ResourcePath}.");
            }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void Start()
        {
            if (string.Equals(
                    SceneManager.GetActiveScene().name,
                    DeckEditorSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetActiveInternal(true);
            }
        }

        private void Update()
        {
            ApplyOutputVolume();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            if (string.Equals(
                    current.name,
                    DeckEditorSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetActiveInternal(true);
                return;
            }
            if (!string.Equals(
                    current.name,
                    MainMenuSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetActiveInternal(false);
            }
        }

        private void SetActiveInternal(bool active)
        {
            if (active == playbackRequested &&
                (!active || (source != null && source.isPlaying)))
            {
                return;
            }

            playbackRequested = active;
            StopRoutineOnly();
            if (!active)
            {
                playbackRoutine = StartCoroutine(FadeOutAndStop());
                return;
            }
            StopAndRewindSources();
            playbackRoutine = StartCoroutine(PlayOverlappedLoop());
        }

        private IEnumerator PlayOverlappedLoop()
        {
            if (track == null)
            {
                playbackRoutine = null;
                yield break;
            }

            StartTrack(source);
            sourceEnvelope = 0f;
            transitionEnvelope = 0f;
            yield return FadeInSource();

            while (playbackRequested && IsFrontendSceneActive())
            {
                float overlap = Mathf.Min(
                    LoopOverlapSeconds,
                    Mathf.Max(0.08f, track.length * 0.25f));
                float crossfadeAt = Mathf.Max(0f, track.length - overlap);
                while (playbackRequested && source.isPlaying &&
                       source.time < crossfadeAt)
                {
                    yield return null;
                }
                if (!playbackRequested || !IsFrontendSceneActive())
                    break;

                StartTrack(transitionSource);
                transitionEnvelope = 0f;
                float elapsed = 0f;
                while (elapsed < overlap && playbackRequested)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01(elapsed / overlap));
                    sourceEnvelope = 1f - progress;
                    transitionEnvelope = progress;
                    ApplyOutputVolume();
                    yield return null;
                }

                source.Stop();
                source.clip = null;
                AudioSource finished = source;
                source = transitionSource;
                transitionSource = finished;
                sourceEnvelope = 1f;
                transitionEnvelope = 0f;
                ApplyOutputVolume();
            }

            playbackRoutine = null;
        }

        private IEnumerator FadeInSource()
        {
            float elapsed = 0f;
            while (elapsed < TransitionSeconds && playbackRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                sourceEnvelope = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / TransitionSeconds));
                ApplyOutputVolume();
                yield return null;
            }
            sourceEnvelope = playbackRequested ? 1f : 0f;
            ApplyOutputVolume();
        }

        private IEnumerator FadeOutAndStop()
        {
            float sourceStart = sourceEnvelope;
            float transitionStart = transitionEnvelope;
            float elapsed = 0f;
            while (elapsed < TransitionSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / TransitionSeconds));
                sourceEnvelope = Mathf.Lerp(sourceStart, 0f, progress);
                transitionEnvelope = Mathf.Lerp(
                    transitionStart,
                    0f,
                    progress);
                ApplyOutputVolume();
                yield return null;
            }
            StopAndRewindSources();
            playbackRoutine = null;
        }

        private static void ConfigureSource(AudioSource target)
        {
            target.playOnAwake = false;
            target.loop = false;
            target.spatialBlend = 0f;
            target.dopplerLevel = 0f;
            target.priority = 31;
            target.ignoreListenerPause = true;
        }

        private void StartTrack(AudioSource target)
        {
            target.Stop();
            target.clip = track;
            target.time = 0f;
            target.Play();
        }

        private void StopAndRewindSources()
        {
            StopAndRewind(source);
            StopAndRewind(transitionSource);
            sourceEnvelope = 0f;
            transitionEnvelope = 0f;
            ApplyOutputVolume();
        }

        private static void StopAndRewind(AudioSource target)
        {
            if (target == null)
                return;
            target.Stop();
            target.clip = null;
        }

        private void StopRoutineOnly()
        {
            if (playbackRoutine == null)
                return;
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        private void ApplyOutputVolume()
        {
            if (source != null)
                ArcaneMusicPreferences.ApplyTo(source, sourceEnvelope);
            if (transitionSource != null)
            {
                ArcaneMusicPreferences.ApplyTo(
                    transitionSource,
                    transitionEnvelope);
            }
        }

        private static bool IsFrontendSceneActive()
        {
            string sceneName = SceneManager.GetActiveScene().name;
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
