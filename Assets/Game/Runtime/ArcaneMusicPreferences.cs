using UnityEngine;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Shared volume channel for every music track in the project.
    /// Music presenters can keep their own fade envelope and mix gain while
    /// this class applies the player's global music preference.
    /// </summary>
    public static class ArcaneMusicPreferences
    {
        private const string VolumeKey =
            "arcane_arena.music.volume";
        public const float DefaultVolume = 0.50f;
        public const float VolumeStep = 0.10f;

        public static float Volume
        {
            get => Mathf.Clamp01(
                PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static float AdjustVolume(int steps)
        {
            float currentStep = Mathf.Round(Volume / VolumeStep);
            Volume = (currentStep + steps) * VolumeStep;
            return Volume;
        }

        public static void ApplyTo(
            AudioSource source,
            float envelope = 1f,
            float mixGain = 1f)
        {
            if (source == null)
                return;

            float musicVolume = Volume;
            source.mute =
                !ArcaneAudioPreferences.Enabled ||
                musicVolume <= 0.0001f;
            source.volume = Mathf.Clamp01(
                musicVolume *
                Mathf.Clamp01(envelope) *
                Mathf.Max(0f, mixGain));
        }

        public static void ResetToDefaults()
        {
            Volume = DefaultVolume;
        }
    }
}
