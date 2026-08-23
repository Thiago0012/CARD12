using System.Collections;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Reproduz o retorno sonoro de geração e a mesma amostra invertida ao
    /// desmantelar. A versão reversa é criada uma única vez e reutilizada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeckEditorCraftAudio : MonoBehaviour
    {
        private const string ResourcePath = "Audio/SFX/UI/crafted";
        private static DeckEditorCraftAudio instance;

        private AudioSource source;
        private AudioClip craftedClip;
        private AudioClip reversedClip;
        private Coroutine prepareRoutine;

        public static void Preload()
        {
            DeckEditorCraftAudio player = EnsureInstance();
            player?.BeginPrepareReversedClip();
        }

        public static void Play(bool dismantling)
        {
            DeckEditorCraftAudio player = EnsureInstance();
            if (player != null)
                player.PlayInternal(dismantling);
        }

        private static DeckEditorCraftAudio EnsureInstance()
        {
            if (instance != null)
                return instance;
            GameObject root = new("Audio de Craft do Editor");
            return root.AddComponent<DeckEditorCraftAudio>();
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
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 24;
            source.ignoreListenerPause = true;
            craftedClip = Resources.Load<AudioClip>(ResourcePath);
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
            if (prepareRoutine != null)
                StopCoroutine(prepareRoutine);
            if (reversedClip != null)
                Destroy(reversedClip);
        }

        private void PlayInternal(bool dismantling)
        {
            if (source == null || craftedClip == null)
                return;

            source.Stop();
            source.pitch = 1f;
            AudioClip clip = dismantling
                ? reversedClip
                : craftedClip;
            if (clip != null)
            {
                source.clip = clip;
                source.timeSamples = 0;
            }
            else
            {
                // Fallback para plataformas que não expõem PCM de MP3.
                source.clip = craftedClip;
                source.pitch = -1f;
                source.timeSamples = Mathf.Max(0, craftedClip.samples - 1);
            }
            ApplyPreferences();
            source.Play();
        }

        private void BeginPrepareReversedClip()
        {
            if (craftedClip == null || reversedClip != null ||
                prepareRoutine != null)
            {
                return;
            }
            prepareRoutine = StartCoroutine(PrepareReversedClip());
        }

        private IEnumerator PrepareReversedClip()
        {
            // Deixa a Oficina terminar seu primeiro frame antes de decodificar
            // o MP3. A inversão também é dividida entre frames para não travar
            // o clique de desmanche.
            yield return null;
            AudioClip original = craftedClip;
            if (original == null || original.samples <= 0 ||
                original.channels <= 0)
            {
                prepareRoutine = null;
                yield break;
            }

            int channels = original.channels;
            int frames = original.samples;
            float[] samples = new float[frames * channels];
            if (!original.GetData(samples, 0))
            {
                prepareRoutine = null;
                yield break;
            }

            const int SwapsPerFrame = 12000;
            int left = 0;
            int right = frames - 1;
            while (left < right)
            {
                int limit = Mathf.Min(left + SwapsPerFrame, right);
                for (; left < limit && left < right; left++, right--)
                {
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int leftIndex = left * channels + channel;
                        int rightIndex = right * channels + channel;
                        (samples[leftIndex], samples[rightIndex]) =
                            (samples[rightIndex], samples[leftIndex]);
                    }
                }
                yield return null;
            }

            AudioClip reversed = AudioClip.Create(
                original.name + " (Reverso)",
                frames,
                channels,
                original.frequency,
                false);
            if (reversed.SetData(samples, 0))
                reversedClip = reversed;
            else
                Destroy(reversed);
            prepareRoutine = null;
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
