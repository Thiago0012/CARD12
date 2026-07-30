using System;
using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;

namespace ArcaneDuel.Game
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class ArcaneAudioDirector : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips =
            new Dictionary<string, AudioClip>();
        private AudioSource source;

        public bool Enabled
        {
            get => PlayerPrefs.GetInt("ArcaneAudioEnabled", 1) != 0;
            set
            {
                PlayerPrefs.SetInt("ArcaneAudioEnabled", value ? 1 : 0);
                PlayerPrefs.Save();
                if (source != null) source.mute = !value;
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
            source.volume = 0.48f;
            source.mute = !Enabled;
            clips["draw"] = BuildTone("Arcane_Draw", 540f, 760f, 0.09f, 0.18f);
            clips["summon"] = BuildTone("Arcane_Summon", 160f, 460f, 0.24f, 0.28f);
            clips["chain"] = BuildTone("Arcane_Chain", 760f, 1080f, 0.14f, 0.2f);
            clips["attack"] = BuildTone("Arcane_Attack", 310f, 1180f, 0.18f, 0.3f);
            clips["phase"] = BuildTone("Arcane_Phase", 520f, 820f, 0.12f, 0.16f);
            clips["damage"] = BuildTone("Arcane_Damage", 180f, 72f, 0.22f, 0.34f);
            clips["win"] = BuildTone("Arcane_Win", 440f, 880f, 0.55f, 0.3f);
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in clips.Values)
            {
                if (clip != null) Destroy(clip);
            }
            clips.Clear();
        }

        public void Play(DuelEvent duelEvent)
        {
            if (!Enabled || source == null || duelEvent == null) return;
            string key = duelEvent.Message switch
            {
                CoreMessage.Draw => "draw",
                CoreMessage.Summoning => "summon",
                CoreMessage.SpecialSummoning => "summon",
                CoreMessage.FlipSummoning => "summon",
                CoreMessage.Chaining => "chain",
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
