using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public enum DuelAnimationFamily
    {
        Summon,
        Activation,
        Chain
    }

    public static class DuelPresentationPreferences
    {
        private const string Prefix = "ArcanePresentation.";

        public static bool IsEnabled(DuelAnimationFamily family)
        {
            return PlayerPrefs.GetInt(Prefix + family + ".Enabled", 1) != 0;
        }

        public static void SetEnabled(DuelAnimationFamily family, bool value)
        {
            PlayerPrefs.SetInt(Prefix + family + ".Enabled", value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static float Speed(DuelAnimationFamily family)
        {
            return Mathf.Clamp(
                PlayerPrefs.GetFloat(Prefix + family + ".Speed", 1f),
                0.75f,
                2f);
        }

        public static void SetSpeed(DuelAnimationFamily family, float value)
        {
            PlayerPrefs.SetFloat(
                Prefix + family + ".Speed",
                Mathf.Clamp(value, 0.75f, 2f));
            PlayerPrefs.Save();
        }

        public static bool TryResolve(
            DuelEvent duelEvent,
            out DuelAnimationFamily family,
            out float speed)
        {
            switch (duelEvent.Message)
            {
                case CoreMessage.Summoning:
                case CoreMessage.Summoned:
                case CoreMessage.SpecialSummoning:
                case CoreMessage.SpecialSummoned:
                case CoreMessage.FlipSummoning:
                case CoreMessage.FlipSummoned:
                    family = DuelAnimationFamily.Summon;
                    break;
                case CoreMessage.Chaining:
                    family = DuelAnimationFamily.Activation;
                    break;
                case CoreMessage.Chained:
                case CoreMessage.ChainSolving:
                case CoreMessage.ChainSolved:
                case CoreMessage.ChainEnd:
                case CoreMessage.ChainNegated:
                case CoreMessage.ChainDisabled:
                    family = DuelAnimationFamily.Chain;
                    break;
                default:
                    family = DuelAnimationFamily.Chain;
                    speed = 1f;
                    return true;
            }

            speed = Speed(family);
            return IsEnabled(family);
        }

        public static void RestoreDefaults()
        {
            foreach (DuelAnimationFamily family in
                     (DuelAnimationFamily[])System.Enum.GetValues(
                         typeof(DuelAnimationFamily)))
            {
                SetEnabled(family, true);
                SetSpeed(family, 1f);
            }
        }
    }
}
