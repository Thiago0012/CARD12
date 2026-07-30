using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Preferências exclusivamente locais de apresentação.
    /// Nunca participam do estado autoritativo ou da validação do duelo.
    /// </summary>
    public static class DuelAnimationPreferences
    {
        private const string EnabledKey =
            "arcane_arena.animations.enabled";
        private const string SpeedKey =
            "arcane_arena.animations.speed";
        private const string MonsterEnabledKey =
            "arcane_arena.animations.monster.enabled";
        private const string MonsterSpeedKey =
            "arcane_arena.animations.monster.speed";
        private const string SpellTrapEnabledKey =
            "arcane_arena.animations.spell_trap.enabled";
        private const string SpellTrapSpeedKey =
            "arcane_arena.animations.spell_trap.speed";
        private const string ChainEnabledKey =
            "arcane_arena.animations.chain.enabled";
        private const string ChainSpeedKey =
            "arcane_arena.animations.chain.speed";
        private const float DefaultSpeed = 1f;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float SpeedMultiplier
        {
            get => NormalizeSpeed(
                PlayerPrefs.GetFloat(
                    SpeedKey,
                    DefaultSpeed));
            set
            {
                PlayerPrefs.SetFloat(
                    SpeedKey,
                    NormalizeSpeed(value));
                PlayerPrefs.Save();
            }
        }

        public static bool MonsterEnabled
        {
            get => ReadEnabled(MonsterEnabledKey);
            set => WriteEnabled(MonsterEnabledKey, value);
        }

        public static float MonsterSpeedMultiplier
        {
            get => ReadSpeed(MonsterSpeedKey);
            set => WriteSpeed(MonsterSpeedKey, value);
        }

        public static bool SpellTrapEnabled
        {
            get => ReadEnabled(SpellTrapEnabledKey);
            set => WriteEnabled(SpellTrapEnabledKey, value);
        }

        public static float SpellTrapSpeedMultiplier
        {
            get => ReadSpeed(SpellTrapSpeedKey);
            set => WriteSpeed(SpellTrapSpeedKey, value);
        }

        public static bool ChainEnabled
        {
            get => ReadEnabled(ChainEnabledKey);
            set => WriteEnabled(ChainEnabledKey, value);
        }

        public static float ChainSpeedMultiplier
        {
            get => ReadSpeed(ChainSpeedKey);
            set => WriteSpeed(ChainSpeedKey, value);
        }

        public static float Duration(float baseSeconds)
        {
            if (!Enabled)
                return 0f;
            return Mathf.Max(
                0.01f,
                baseSeconds /
                SpeedMultiplier);
        }

        public static string SpeedLabel =>
            $"{SpeedMultiplier:0.##}x";

        public static string MonsterSpeedLabel =>
            $"{MonsterSpeedMultiplier:0.##}x";

        public static string SpellTrapSpeedLabel =>
            $"{SpellTrapSpeedMultiplier:0.##}x";

        public static string ChainSpeedLabel =>
            $"{ChainSpeedMultiplier:0.##}x";

        public static float MonsterDuration(float baseSeconds)
        {
            return CategoryDuration(
                baseSeconds,
                MonsterEnabled,
                MonsterSpeedMultiplier);
        }

        public static float SpellTrapDuration(float baseSeconds)
        {
            return CategoryDuration(
                baseSeconds,
                SpellTrapEnabled,
                SpellTrapSpeedMultiplier);
        }

        public static float ChainDuration(float baseSeconds)
        {
            return CategoryDuration(
                baseSeconds,
                ChainEnabled,
                ChainSpeedMultiplier);
        }

        public static void ResetToDefaults()
        {
            PlayerPrefs.SetInt(EnabledKey, 1);
            PlayerPrefs.SetFloat(SpeedKey, DefaultSpeed);
            PlayerPrefs.SetInt(MonsterEnabledKey, 1);
            PlayerPrefs.SetFloat(
                MonsterSpeedKey,
                DefaultSpeed);
            PlayerPrefs.SetInt(SpellTrapEnabledKey, 1);
            PlayerPrefs.SetFloat(
                SpellTrapSpeedKey,
                DefaultSpeed);
            PlayerPrefs.SetInt(ChainEnabledKey, 1);
            PlayerPrefs.SetFloat(
                ChainSpeedKey,
                DefaultSpeed);
            PlayerPrefs.Save();
        }

        private static bool ReadEnabled(string key)
        {
            return PlayerPrefs.GetInt(
                       key,
                       Enabled ? 1 : 0) != 0;
        }

        private static void WriteEnabled(
            string key,
            bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static float ReadSpeed(string key)
        {
            return NormalizeSpeed(
                PlayerPrefs.GetFloat(
                    key,
                    SpeedMultiplier));
        }

        private static void WriteSpeed(
            string key,
            float value)
        {
            PlayerPrefs.SetFloat(
                key,
                NormalizeSpeed(value));
            PlayerPrefs.Save();
        }

        private static float CategoryDuration(
            float baseSeconds,
            bool enabled,
            float speed)
        {
            if (!enabled)
                return 0f;
            return Mathf.Max(
                0.01f,
                baseSeconds / speed);
        }

        private static float NormalizeSpeed(float value)
        {
            if (value < 0.875f)
                return 0.75f;
            if (value < 1.25f)
                return 1f;
            if (value < 1.75f)
                return 1.5f;
            return 2f;
        }
    }
}
