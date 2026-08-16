using System.Collections;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Plays the two-part victory/defeat cue used by both bot and online
    /// result screens. It only observes the presentation result and shares
    /// the global effects-volume preference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelResultAudioPlayer : MonoBehaviour
    {
        private const string Root = "Audio/SFX/Duel/";
        private const float VoiceDelaySeconds = 0.50f;
        private static DuelResultAudioPlayer instance;

        private AudioSource source;
        private AudioClip victorySound;
        private AudioClip defeatSound;
        private AudioClip victoryVoice;
        private AudioClip defeatVoice;
        private Coroutine playbackRoutine;
        private bool resultPlaybackLatched;
        private OnlineDuelResultKind latchedResult;

        public static void Play(OnlineDuelResultKind result)
        {
            DuelResultAudioPlayer player = EnsureInstance();
            if (player == null)
                return;
            player.Begin(result);
        }

        public static void StopPlayback()
        {
            if (instance != null)
                instance.StopCurrent(true);
        }

        private static DuelResultAudioPlayer EnsureInstance()
        {
            if (instance != null)
                return instance;
            GameObject root = new("Arcane Duel Result Audio");
            instance = root.AddComponent<DuelResultAudioPlayer>();
            DontDestroyOnLoad(root);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            victorySound = Resources.Load<AudioClip>(Root + "victorysound");
            defeatSound = Resources.Load<AudioClip>(Root + "losesound");
            victoryVoice = Resources.Load<AudioClip>(Root + "victoryvoice");
            defeatVoice = Resources.Load<AudioClip>(Root + "losevoice");
        }

        private void Update()
        {
            if (source != null && source.isPlaying)
                ApplyPreferences();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Begin(OnlineDuelResultKind result)
        {
            AudioClip sound;
            AudioClip voice;
            if (result == OnlineDuelResultKind.Victory)
            {
                sound = victorySound;
                voice = victoryVoice;
            }
            else if (result == OnlineDuelResultKind.Defeat)
            {
                sound = defeatSound;
                voice = defeatVoice;
            }
            else
            {
                return;
            }

            // Show/ShowRanked can be refreshed more than once for the same
            // authoritative result. Keep the cue idempotent until the result
            // screen is closed so the voice can never be played twice.
            if (resultPlaybackLatched && latchedResult == result)
                return;
            StopCurrent(false);
            resultPlaybackLatched = true;
            latchedResult = result;
            playbackRoutine = StartCoroutine(PlaySequence(sound, voice));
        }

        private IEnumerator PlaySequence(AudioClip sound, AudioClip voice)
        {
            yield return PlayClip(sound);
            if (sound != null && voice != null)
                yield return new WaitForSecondsRealtime(VoiceDelaySeconds);
            yield return PlayClip(voice);
            playbackRoutine = null;
        }

        private IEnumerator PlayClip(AudioClip clip)
        {
            if (clip == null || source == null)
                yield break;

            source.clip = clip;
            source.time = 0f;
            ApplyPreferences();
            source.Play();
            while (source != null && source.isPlaying)
            {
                ApplyPreferences();
                yield return null;
            }
        }

        private void StopCurrent(bool releaseResultLatch)
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
            if (releaseResultLatch)
                resultPlaybackLatched = false;
        }

        private void ApplyPreferences()
        {
            if (source == null)
                return;
            source.mute = !ArcaneAudioPreferences.Enabled ||
                          ArcaneAudioPreferences.Volume <= 0.0001f;
            source.volume = ArcaneAudioPreferences.Volume;
        }
    }
}
