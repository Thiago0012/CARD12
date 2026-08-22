using System.Collections;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Persistent duel-only music player. The arena starts it after the
    /// visual opening, independently from every Core command or rule.
    /// </summary>
    [DefaultExecutionOrder(-890)]
    public sealed class DuelMusicController : MonoBehaviour
    {
        private const string DuelArenaSceneName = "DuelArena";
        private const string MusicResourceFolder = "Audio/Music/Duel";
        private const float StartDelaySeconds = 1f;
        private const float FadeInSeconds = 1.35f;
        private const float TrackCrossfadeSeconds = 0.50f;
        private const float ExitFadeSeconds = 0.90f;

        private static DuelMusicController instance;

        private AudioSource source;
        private AudioSource transitionSource;
        private AudioClip[] tracks;
        private Coroutine playbackRoutine;
        private float volumeEnvelope;
        private float transitionEnvelope;
        private int previousTrack = -1;
        private bool playbackRequested;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreatePersistentPlayer()
        {
            EnsureInstance();
        }

        public static void BeginDuelPlayback()
        {
            EnsureInstance()?.SchedulePlayback();
        }

        private static DuelMusicController EnsureInstance()
        {
            if (instance != null)
                return instance;
            var musicObject = new GameObject("Musica dos Duelos");
            return musicObject.AddComponent<DuelMusicController>();
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
            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();
            transitionSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(source);
            ConfigureSource(transitionSource);
            tracks = Resources.LoadAll<AudioClip>(MusicResourceFolder);
            ApplyOutputVolume();

            if (tracks == null || tracks.Length == 0)
            {
                Debug.LogWarning(
                    "Nenhuma musica de duelo foi encontrada em Resources/" +
                    MusicResourceFolder + ".");
            }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
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
            if (!string.Equals(
                    current.name,
                    DuelArenaSceneName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                StopPlayback();
            }
        }

        private void SchedulePlayback()
        {
            if (tracks == null || tracks.Length == 0 ||
                !string.Equals(
                    SceneManager.GetActiveScene().name,
                    DuelArenaSceneName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            playbackRequested = true;
            StopRoutineOnly();
            StopAndRewindSources();
            playbackRoutine = StartCoroutine(PlayPlaylistAfterDelay());
        }

        private IEnumerator PlayPlaylistAfterDelay()
        {
            yield return new WaitForSecondsRealtime(StartDelaySeconds);
            if (!playbackRequested || !IsDuelSceneActive())
            {
                playbackRoutine = null;
                yield break;
            }

            int duelTrackIndex = SelectTrackIndex();
            StartTrack(source, duelTrackIndex);

            float elapsed = 0f;
            while (elapsed < FadeInSeconds && playbackRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / FadeInSeconds);
                volumeEnvelope = Mathf.SmoothStep(0f, 1f, progress);
                ApplyOutputVolume();
                yield return null;
            }

            volumeEnvelope = 1f;
            ApplyOutputVolume();

            while (playbackRequested && IsDuelSceneActive())
            {
                float crossfadeAt = Mathf.Max(
                    0f,
                    source.clip.length - TrackCrossfadeSeconds);
                while (playbackRequested && source.isPlaying &&
                       source.time < crossfadeAt)
                {
                    yield return null;
                }
                if (!playbackRequested || !IsDuelSceneActive())
                    yield break;

                // A faixa e sorteada uma unica vez por duelo. O segundo
                // AudioSource existe somente para repetir essa mesma musica
                // sem produzir um corte seco no ponto de loop.
                StartTrack(transitionSource, duelTrackIndex);
                transitionEnvelope = 0f;
                float crossfadeElapsed = 0f;
                while (crossfadeElapsed < TrackCrossfadeSeconds &&
                       playbackRequested)
                {
                    crossfadeElapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(
                        crossfadeElapsed / TrackCrossfadeSeconds);
                    float eased = Mathf.SmoothStep(0f, 1f, progress);
                    volumeEnvelope = 1f - eased;
                    transitionEnvelope = eased;
                    ApplyOutputVolume();
                    yield return null;
                }

                source.Stop();
                source.clip = null;
                AudioSource finishedSource = source;
                source = transitionSource;
                transitionSource = finishedSource;
                volumeEnvelope = 1f;
                transitionEnvelope = 0f;
                ApplyOutputVolume();
            }

            playbackRoutine = null;
        }

        private static void ConfigureSource(AudioSource target)
        {
            target.playOnAwake = false;
            target.loop = false;
            target.spatialBlend = 0f;
            target.dopplerLevel = 0f;
            target.priority = 33;
            target.ignoreListenerPause = true;
        }

        private void StartTrack(AudioSource target, int trackIndex)
        {
            target.Stop();
            target.clip = tracks[trackIndex];
            target.time = 0f;
            target.Play();
        }

        private int SelectTrackIndex()
        {
            if (tracks.Length <= 1)
                return previousTrack = 0;

            int selected;
            do
            {
                selected = Random.Range(0, tracks.Length);
            }
            while (selected == previousTrack);
            previousTrack = selected;
            return selected;
        }

        private void StopPlayback()
        {
            playbackRequested = false;
            StopRoutineOnly();
            if ((source != null && source.isPlaying) ||
                (transitionSource != null && transitionSource.isPlaying))
            {
                playbackRoutine = StartCoroutine(FadeOutAndStop());
                return;
            }

            StopAndRewindSources();
        }

        private IEnumerator FadeOutAndStop()
        {
            float sourceStart = volumeEnvelope;
            float transitionStart = transitionEnvelope;
            float elapsed = 0f;
            while (elapsed < ExitFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / ExitFadeSeconds));
                volumeEnvelope = Mathf.Lerp(sourceStart, 0f, progress);
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

        private void StopAndRewindSources()
        {
            StopAndRewind(source);
            StopAndRewind(transitionSource);
            volumeEnvelope = 0f;
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
                ArcaneMusicPreferences.ApplyTo(source, volumeEnvelope);
            if (transitionSource != null)
            {
                ArcaneMusicPreferences.ApplyTo(
                    transitionSource,
                    transitionEnvelope);
            }
        }

        private static bool IsDuelSceneActive()
        {
            return string.Equals(
                SceneManager.GetActiveScene().name,
                DuelArenaSceneName,
                System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
