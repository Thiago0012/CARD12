using System.Collections;
using ArcaneArena.Frontend;
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
        private const float VoicePositionRatio = 0.50f;
        private static DuelResultAudioPlayer instance;

        private AudioSource songSource;
        private AudioSource voiceSource;
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
            songSource = gameObject.AddComponent<AudioSource>();
            voiceSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(songSource, 20);
            ConfigureSource(voiceSource, 18);
            victorySound = Resources.Load<AudioClip>(Root + "winsong") ??
                           Resources.Load<AudioClip>(Root + "victorysound");
            defeatSound = Resources.Load<AudioClip>(Root + "losesong") ??
                          Resources.Load<AudioClip>(Root + "losesound");
            victoryVoice = Resources.Load<AudioClip>(Root + "victoryvoice");
            defeatVoice = Resources.Load<AudioClip>(Root + "losevoice");
        }

        private void Update()
        {
            if ((songSource != null && songSource.isPlaying) ||
                (voiceSource != null && voiceSource.isPlaying))
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
            DuelMusicController.StopForResult();
            resultPlaybackLatched = true;
            latchedResult = result;
            playbackRoutine = StartCoroutine(PlaySequence(sound, voice));
        }

        private IEnumerator PlaySequence(AudioClip sound, AudioClip voice)
        {
            ApplyPreferences();
            if (sound != null && songSource != null)
            {
                songSource.clip = sound;
                songSource.time = 0f;
                songSource.Play();
            }

            float voiceAt = sound != null
                ? Mathf.Max(0f, sound.length * VoicePositionRatio)
                : 0f;
            float elapsed = 0f;
            while (sound != null && songSource != null &&
                   songSource.isPlaying && elapsed < voiceAt)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyPreferences();
                yield return null;
            }

            if (voice != null && voiceSource != null)
            {
                voiceSource.clip = voice;
                voiceSource.time = 0f;
                voiceSource.Play();
            }

            while ((songSource != null && songSource.isPlaying) ||
                   (voiceSource != null && voiceSource.isPlaying))
            {
                ApplyPreferences();
                yield return null;
            }
            playbackRoutine = null;
        }

        private static void ConfigureSource(AudioSource target, int priority)
        {
            target.playOnAwake = false;
            target.loop = false;
            target.spatialBlend = 0f;
            target.dopplerLevel = 0f;
            target.priority = priority;
            target.ignoreListenerPause = true;
        }

        private void StopCurrent(bool releaseResultLatch)
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }
            if (songSource != null)
            {
                songSource.Stop();
                songSource.clip = null;
            }
            if (voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.clip = null;
            }
            if (releaseResultLatch)
                resultPlaybackLatched = false;
        }

        private void ApplyPreferences()
        {
            if (songSource != null)
                ArcaneMusicPreferences.ApplyTo(songSource, 1f);
            if (voiceSource != null)
            {
                voiceSource.mute = !ArcaneAudioPreferences.Enabled ||
                                   ArcaneAudioPreferences.Volume <= 0.0001f;
                voiceSource.volume = ArcaneAudioPreferences.Volume;
            }
        }
    }
}
