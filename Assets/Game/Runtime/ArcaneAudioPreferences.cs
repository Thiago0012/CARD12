using UnityEngine;

namespace ArcaneDuel.Game
{
    public static class ArcaneAudioPreferences
    {
        private const string EnabledKey = "ArcaneAudioEnabled";
        private const string VolumeKey = "arcane_arena.audio.volume";
        public const float DefaultVolume = 1f;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

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

        public static void ResetToDefaults()
        {
            Enabled = true;
            Volume = DefaultVolume;
        }
    }
}
