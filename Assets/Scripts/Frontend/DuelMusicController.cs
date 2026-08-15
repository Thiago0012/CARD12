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
        private const float StartDelaySeconds = 3.5f;
        private const float FadeInSeconds = 1.8f;

        private static DuelMusicController instance;

        private AudioSource source;
        private AudioClip[] tracks;
        private Coroutine playbackRoutine;
        private float volumeEnvelope;
        private int previousTrack = -1;

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
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 33;
            source.ignoreListenerPause = true;
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

            StopRoutineOnly();
            source.Stop();
            if (source.clip != null)
                source.time = 0f;
            volumeEnvelope = 0f;
            ApplyOutputVolume();
            playbackRoutine = StartCoroutine(BeginAfterDelay());
        }

        private IEnumerator BeginAfterDelay()
        {
            yield return new WaitForSecondsRealtime(StartDelaySeconds);
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    DuelArenaSceneName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                playbackRoutine = null;
                yield break;
            }

            int selected = SelectTrackIndex();
            source.clip = tracks[selected];
            source.time = 0f;
            source.loop = true;
            source.Play();

            float elapsed = 0f;
            while (elapsed < FadeInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / FadeInSeconds);
                volumeEnvelope = Mathf.SmoothStep(0f, 1f, progress);
                ApplyOutputVolume();
                yield return null;
            }

            volumeEnvelope = 1f;
            ApplyOutputVolume();
            playbackRoutine = null;
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
            StopRoutineOnly();
            if (source != null)
            {
                source.Stop();
                if (source.clip != null)
                    source.time = 0f;
            }
            volumeEnvelope = 0f;
            ApplyOutputVolume();
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
        }
    }
}
