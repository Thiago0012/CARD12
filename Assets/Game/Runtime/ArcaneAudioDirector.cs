using System;
using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public enum ArcaneCardSound
    {
        None,
        Draw,
        Fusion,
        Magic,
        MonsterSummon,
        PutCard,
        Synchro,
        Trap,
        Xyz
    }

    [RequireComponent(typeof(AudioSource))]
    public sealed class ArcaneAudioDirector : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips =
            new Dictionary<string, AudioClip>();
        private readonly Dictionary<ArcaneCardSound, AudioClip> cardClips =
            new Dictionary<ArcaneCardSound, AudioClip>();
        private readonly Dictionary<AudioClip, float> balancedGains =
            new Dictionary<AudioClip, float>();
        private AudioSource source;
        private AudioSource cardSource;

        public bool Enabled
        {
            get => ArcaneAudioPreferences.Enabled;
            set
            {
                ArcaneAudioPreferences.Enabled = value;
                ApplyPreferences();
            }
        }

        public float Volume
        {
            get => ArcaneAudioPreferences.Volume;
            set
            {
                ArcaneAudioPreferences.Volume = value;
                ApplyPreferences();
            }
        }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            if (source == null)
            {
                enabled = false;
                Debug.LogError(
                    "ArcaneAudioDirector requires an AudioSource.");
                return;
            }
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            cardSource = gameObject.AddComponent<AudioSource>();
            cardSource.playOnAwake = false;
            cardSource.spatialBlend = 0f;
            ApplyPreferences();
            clips["draw"] = BuildTone("Arcane_Draw", 540f, 760f, 0.09f, 0.18f);
            clips["summon"] = BuildTone("Arcane_Summon", 160f, 460f, 0.24f, 0.28f);
            clips["chain"] = BuildTone("Arcane_Chain", 760f, 1080f, 0.14f, 0.2f);
            clips["attack"] = BuildTone("Arcane_Attack", 310f, 1180f, 0.18f, 0.3f);
            clips["phase"] = BuildTone("Arcane_Phase", 520f, 820f, 0.12f, 0.16f);
            clips["damage"] = BuildTone("Arcane_Damage", 180f, 72f, 0.22f, 0.34f);
            clips["win"] = BuildTone("Arcane_Win", 440f, 880f, 0.55f, 0.3f);
            cardClips[ArcaneCardSound.Draw] = clips["draw"];
            LoadCardClip(ArcaneCardSound.Fusion, "CardsSounds/Fusion.mpeg");
            LoadCardClip(ArcaneCardSound.Magic, "CardsSounds/MagicSound.mpeg");
            LoadCardClip(ArcaneCardSound.MonsterSummon, "CardsSounds/MonsterSummon.mpeg");
            LoadCardClip(ArcaneCardSound.PutCard, "CardsSounds/putCardSound.mpeg");
            LoadCardClip(ArcaneCardSound.Synchro, "CardsSounds/SincronSummonSound.mpeg");
            LoadCardClip(ArcaneCardSound.Trap, "CardsSounds/trapsound.mpeg");
            LoadCardClip(ArcaneCardSound.Xyz, "CardsSounds/XYZ summon.mpeg");
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in clips.Values)
            {
                if (clip != null) Destroy(clip);
            }
            clips.Clear();
            cardClips.Clear();
            balancedGains.Clear();
        }

        public void Play(DuelEvent duelEvent)
        {
            if (!Enabled || source == null || duelEvent == null) return;
            string key = duelEvent.Message switch
            {
                CoreMessage.Attack => "attack",
                CoreMessage.NewPhase => "phase",
                CoreMessage.Damage => "damage",
                CoreMessage.Win => "win",
                _ => null
            };
            if (key != null && clips.TryGetValue(key, out AudioClip clip))
            {
                source.PlayOneShot(clip);
            }
        }

        public float PlayCardCue(ArcaneCardSound cue)
        {
            if (!Enabled || cue == ArcaneCardSound.None || cardSource == null ||
                !cardClips.TryGetValue(cue, out AudioClip clip) || clip == null)
            {
                return 0f;
            }

            cardSource.Stop();
            cardSource.pitch = 1f;
            float gain = balancedGains.TryGetValue(clip, out float value)
                ? value
                : 1f;
            cardSource.PlayOneShot(clip, gain);
            return clip.length;
        }

        public void AccelerateCardCue()
        {
            if (cardSource != null && cardSource.isPlaying)
                cardSource.pitch = 2f;
        }

        public void StopCardCue()
        {
            if (cardSource == null)
                return;
            cardSource.Stop();
            cardSource.pitch = 1f;
        }

        private void ApplyPreferences()
        {
            bool muted = !ArcaneAudioPreferences.Enabled;
            float volume = ArcaneAudioPreferences.Volume;
            if (source != null)
            {
                source.mute = muted;
                source.volume = volume;
            }
            if (cardSource != null)
            {
                cardSource.mute = muted;
                cardSource.volume = volume;
            }
        }

        private void LoadCardClip(ArcaneCardSound cue, string resourcePath)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"Som de card ausente: {resourcePath}.");
                return;
            }
            clip.LoadAudioData();
            cardClips[cue] = clip;
            balancedGains[clip] = CalculateBalancedGain(clip);
        }

        private static float CalculateBalancedGain(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0)
                return 1f;
            float[] samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
                return 1f;

            double squares = 0d;
            float peak = 0f;
            int active = 0;
            foreach (float sample in samples)
            {
                float amplitude = Mathf.Abs(sample);
                peak = Mathf.Max(peak, amplitude);
                if (amplitude < 0.01f)
                    continue;
                squares += sample * sample;
                active++;
            }
            if (active == 0 || peak <= 0.0001f)
                return 1f;
            float rms = Mathf.Sqrt((float)(squares / active));
            float rmsGain = 0.16f / Mathf.Max(0.001f, rms);
            float peakGain = 0.92f / peak;
            return Mathf.Clamp(Mathf.Min(rmsGain, peakGain), 0.35f, 1.5f);
        }

        private static AudioClip BuildTone(
            string name,
            float startFrequency,
            float endFrequency,
            float duration,
            float amplitude)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(256, Mathf.RoundToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float progress = index / (float)(sampleCount - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                phase += frequency / sampleRate;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                samples[index] =
                    Mathf.Sin(phase * Mathf.PI * 2f) *
                    envelope *
                    amplitude;
            }
            AudioClip clip = AudioClip.Create(
                name,
                sampleCount,
                1,
                sampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
