using System;
using System.Collections;
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
        private const float MagicSoundGain = 0.75f;
        private readonly Dictionary<string, AudioClip> clips =
            new Dictionary<string, AudioClip>();
        private readonly Dictionary<ArcaneCardSound, AudioClip> cardClips =
            new Dictionary<ArcaneCardSound, AudioClip>();
        private readonly Dictionary<AudioClip, float> balancedGains =
            new Dictionary<AudioClip, float>();
        private AudioSource source;
        private AudioSource cardSource;
        private AudioSource damageHitSource;
        private AudioSource lifePointLossSource;
        private AudioClip damageHitClip;
        private AudioClip lifePointLossClip;
        private Coroutine generalEnvelopeRoutine;
        private Coroutine cardEnvelopeRoutine;
        private Coroutine rapidCardCueRoutine;
        private float generalEnvelope = 1f;
        private float cardEnvelope = 1f;
        private float cardPlaybackGain = 1f;

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

        public float DamageImpactCueDelay => damageHitClip != null
            ? Mathf.Max(0f, damageHitClip.length - 0.5f)
            : 0f;

        public float LifePointLossCueDuration => lifePointLossClip != null
            ? Mathf.Max(0.08f, lifePointLossClip.length)
            : 0.72f;

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
            damageHitSource = gameObject.AddComponent<AudioSource>();
            damageHitSource.playOnAwake = false;
            damageHitSource.spatialBlend = 0f;
            lifePointLossSource = gameObject.AddComponent<AudioSource>();
            lifePointLossSource.playOnAwake = false;
            lifePointLossSource.spatialBlend = 0f;
            ApplyPreferences();
            clips["draw"] = BuildTone("Arcane_Draw", 540f, 760f, 0.09f, 0.18f);
            clips["summon"] = BuildTone("Arcane_Summon", 160f, 460f, 0.24f, 0.28f);
            clips["chain"] = BuildTone("Arcane_Chain", 760f, 1080f, 0.14f, 0.2f);
            clips["attack"] = BuildTone("Arcane_Attack", 310f, 1180f, 0.18f, 0.3f);
            clips["phase"] = BuildTone("Arcane_Phase", 520f, 820f, 0.12f, 0.16f);
            clips["damage"] = BuildTone("Arcane_Damage", 180f, 72f, 0.22f, 0.34f);
            cardClips[ArcaneCardSound.Draw] = clips["draw"];
            LoadCardClip(
                ArcaneCardSound.Draw,
                "Audio/SFX/Duel/carddraw");
            LoadCardClip(ArcaneCardSound.Fusion, "CardsSounds/Fusion.mpeg");
            LoadCardClip(ArcaneCardSound.Magic, "CardsSounds/MagicSound.mpeg");
            LoadCardClip(ArcaneCardSound.MonsterSummon, "CardsSounds/MonsterSummon.mpeg");
            LoadCardClip(ArcaneCardSound.PutCard, "CardsSounds/putCardSound.mpeg");
            LoadCardClip(ArcaneCardSound.Synchro, "CardsSounds/SincronSummonSound.mpeg");
            LoadCardClip(ArcaneCardSound.Trap, "CardsSounds/trapsound.mpeg");
            LoadCardClip(ArcaneCardSound.Xyz, "CardsSounds/XYZ summon.mpeg");
            damageHitClip = LoadDuelClip("Audio/SFX/Duel/HitSound");
            lifePointLossClip = LoadDuelClip("Audio/SFX/Duel/LosingLP");
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
                _ => null
            };
            if (key != null && clips.TryGetValue(key, out AudioClip clip))
            {
                PlayGeneralCue(clip);
            }
        }

        public float PlayCardCue(ArcaneCardSound cue)
        {
            if (rapidCardCueRoutine != null)
            {
                StopCoroutine(rapidCardCueRoutine);
                rapidCardCueRoutine = null;
            }
            return BeginCardCue(cue);
        }

        /// <summary>
        /// Toca o impacto imediato de dano e informa o atraso até o contador
        /// de LP aparecer. O fim do impacto sobrepõe meio segundo à animação
        /// de perda, como na apresentação de um duelo televisionado.
        /// </summary>
        public float PlayDamageImpactCue()
        {
            AudioClip clip = damageHitClip;
            if (clip == null)
                return 0f;

            if (Enabled && damageHitSource != null)
            {
                damageHitSource.PlayOneShot(
                    clip,
                    GainFor(clip));
            }

            return DamageImpactCueDelay;
        }

        /// <summary>
        /// Toca o áudio de perda de LP e devolve exatamente a duração usada
        /// pela interface para levar o número até o perfil atingido.
        /// </summary>
        public float PlayLifePointLossCue()
        {
            AudioClip clip = lifePointLossClip;
            if (clip == null)
                return 0.72f;

            if (Enabled && lifePointLossSource != null)
            {
                lifePointLossSource.PlayOneShot(
                    clip,
                    GainFor(clip));
            }

            return LifePointLossCueDuration;
        }

        public void PlayRapidCardCues(
            ArcaneCardSound cue,
            int count,
            float interval = 0.16f)
        {
            if (rapidCardCueRoutine != null)
                StopCoroutine(rapidCardCueRoutine);
            rapidCardCueRoutine = StartCoroutine(
                PlayRapidCardCueSequence(
                    cue,
                    Mathf.Clamp(count, 1, 12),
                    Mathf.Clamp(interval, 0.08f, 0.35f)));
        }

        private float BeginCardCue(
            ArcaneCardSound cue,
            float maximumDuration = float.PositiveInfinity)
        {
            if (!Enabled || cue == ArcaneCardSound.None || cardSource == null ||
                !cardClips.TryGetValue(cue, out AudioClip clip) || clip == null)
            {
                return 0f;
            }

            if (cardEnvelopeRoutine != null)
                StopCoroutine(cardEnvelopeRoutine);
            cardSource.Stop();
            cardSource.pitch = 1f;
            cardPlaybackGain = balancedGains.TryGetValue(
                    clip,
                    out float value)
                ? value
                : 1f;
            cardEnvelope = 0f;
            ApplySourceVolumes();
            cardSource.clip = clip;
            cardSource.Play();
            float playbackDuration = Mathf.Min(
                clip.length,
                Mathf.Max(0.02f, maximumDuration));
            cardEnvelopeRoutine = StartCoroutine(
                PlayEnvelope(cardSource, playbackDuration, true));
            return playbackDuration;
        }

        private IEnumerator PlayRapidCardCueSequence(
            ArcaneCardSound cue,
            int count,
            float interval)
        {
            for (int index = 0; index < count; index++)
            {
                // End each rapid cue with its own short fade before the next
                // card. This keeps multi-draws crisp without stacking clips
                // or cutting a waveform at full volume.
                BeginCardCue(cue, interval * 0.92f);
                if (index + 1 < count)
                    yield return new WaitForSecondsRealtime(interval);
            }
            rapidCardCueRoutine = null;
        }

        public void FadeOutCardCue(float duration = 0.38f)
        {
            if (cardSource == null || !cardSource.isPlaying)
                return;
            if (cardEnvelopeRoutine != null)
                StopCoroutine(cardEnvelopeRoutine);
            cardEnvelopeRoutine = StartCoroutine(
                FadeOutCardSource(Mathf.Max(0.08f, duration)));
        }

        public void StopCardCue()
        {
            if (cardSource == null)
                return;
            if (rapidCardCueRoutine != null)
            {
                StopCoroutine(rapidCardCueRoutine);
                rapidCardCueRoutine = null;
            }
            if (cardEnvelopeRoutine != null)
            {
                StopCoroutine(cardEnvelopeRoutine);
                cardEnvelopeRoutine = null;
            }
            cardSource.Stop();
            cardSource.pitch = 1f;
            cardEnvelope = 1f;
            cardPlaybackGain = 1f;
            ApplySourceVolumes();
        }

        private void ApplyPreferences()
        {
            bool muted = !ArcaneAudioPreferences.Enabled;
            if (source != null)
            {
                source.mute = muted;
            }
            if (cardSource != null)
            {
                cardSource.mute = muted;
            }
            if (damageHitSource != null)
                damageHitSource.mute = muted;
            if (lifePointLossSource != null)
                lifePointLossSource.mute = muted;
            ApplySourceVolumes();
        }

        private void PlayGeneralCue(AudioClip clip)
        {
            if (clip == null || source == null)
                return;
            if (generalEnvelopeRoutine != null)
                StopCoroutine(generalEnvelopeRoutine);
            source.Stop();
            source.pitch = 1f;
            source.clip = clip;
            generalEnvelope = 0f;
            ApplySourceVolumes();
            source.Play();
            generalEnvelopeRoutine = StartCoroutine(
                PlayEnvelope(source, clip.length, false));
        }

        private IEnumerator PlayEnvelope(
            AudioSource target,
            float duration,
            bool card)
        {
            float fadeIn = Mathf.Min(0.055f, duration * 0.22f);
            float fadeOut = Mathf.Min(0.20f, duration * 0.34f);
            float elapsed = 0f;
            while (target != null && target.isPlaying && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float envelope = elapsed < fadeIn
                    ? Mathf.Clamp01(elapsed / Mathf.Max(0.001f, fadeIn))
                    : elapsed > duration - fadeOut
                        ? Mathf.Clamp01(
                            (duration - elapsed) /
                            Mathf.Max(0.001f, fadeOut))
                        : 1f;
                if (card)
                    cardEnvelope = envelope;
                else
                    generalEnvelope = envelope;
                ApplySourceVolumes();
                yield return null;
            }
            if (target != null)
            {
                if (target.isPlaying)
                    target.Stop();
                target.pitch = 1f;
            }
            if (card)
            {
                cardEnvelope = 1f;
                cardPlaybackGain = 1f;
                cardEnvelopeRoutine = null;
            }
            else
            {
                generalEnvelope = 1f;
                generalEnvelopeRoutine = null;
            }
            ApplySourceVolumes();
        }

        private IEnumerator FadeOutCardSource(float duration)
        {
            float from = cardEnvelope;
            for (float elapsed = 0f;
                 cardSource != null && cardSource.isPlaying &&
                 elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                cardEnvelope = Mathf.Lerp(
                    from,
                    0f,
                    Mathf.SmoothStep(0f, 1f, elapsed / duration));
                ApplySourceVolumes();
                yield return null;
            }
            if (cardSource != null)
            {
                cardSource.Stop();
                cardSource.pitch = 1f;
            }
            cardEnvelope = 1f;
            cardPlaybackGain = 1f;
            cardEnvelopeRoutine = null;
            ApplySourceVolumes();
        }

        private void ApplySourceVolumes()
        {
            float volume = ArcaneAudioPreferences.Volume;
            if (source != null)
                source.volume = volume * generalEnvelope;
            if (cardSource != null)
            {
                cardSource.volume =
                    volume * cardEnvelope * cardPlaybackGain;
            }
            if (damageHitSource != null)
                damageHitSource.volume = volume;
            if (lifePointLossSource != null)
                lifePointLossSource.volume = volume;
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
            float categoryGain = cue == ArcaneCardSound.Magic
                ? MagicSoundGain
                : 1f;
            balancedGains[clip] =
                CalculateBalancedGain(clip) * categoryGain;
        }

        private AudioClip LoadDuelClip(string resourcePath)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"Som de duelo ausente: {resourcePath}.");
                return null;
            }

            clip.LoadAudioData();
            balancedGains[clip] = CalculateBalancedGain(clip);
            return clip;
        }

        private float GainFor(AudioClip clip)
        {
            return clip != null && balancedGains.TryGetValue(
                clip,
                out float gain)
                ? gain
                : 1f;
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
