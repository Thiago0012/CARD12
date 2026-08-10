using System;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public enum ArcaneUiTextSize
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    /// <summary>
    /// Preferência local e independente de plataforma para a escala dos textos.
    /// A camada visual observa Changed e reaplica a escala sem recarregar a cena.
    /// </summary>
    public static class ArcaneUiTextPreferences
    {
        private const string TextSizeKey = "ArcaneUi.TextSize";

        public static event Action Changed;

        public static ArcaneUiTextSize Current
        {
            get
            {
                int stored = PlayerPrefs.GetInt(
                    TextSizeKey,
                    (int)ArcaneUiTextSize.Medium);
                return stored switch
                {
                    (int)ArcaneUiTextSize.Small => ArcaneUiTextSize.Small,
                    (int)ArcaneUiTextSize.Large => ArcaneUiTextSize.Large,
                    _ => ArcaneUiTextSize.Medium
                };
            }
        }

        public static float Multiplier => MultiplierFor(Current);

        public static float MultiplierFor(ArcaneUiTextSize size)
        {
            return size switch
            {
                ArcaneUiTextSize.Small => 0.90f,
                ArcaneUiTextSize.Large => 1.20f,
                _ => 1f
            };
        }

        public static int Scale(int baseSize)
        {
            return Mathf.Max(
                1,
                Mathf.RoundToInt(Mathf.Max(1, baseSize) * Multiplier));
        }

        public static string DisplayName(ArcaneUiTextSize size)
        {
            return size switch
            {
                ArcaneUiTextSize.Small => "PEQUENO",
                ArcaneUiTextSize.Large => "GRANDE",
                _ => "MÉDIO"
            };
        }

        public static ArcaneUiTextSize Next(ArcaneUiTextSize current)
        {
            return current switch
            {
                ArcaneUiTextSize.Small => ArcaneUiTextSize.Medium,
                ArcaneUiTextSize.Medium => ArcaneUiTextSize.Large,
                _ => ArcaneUiTextSize.Small
            };
        }

        public static void Set(ArcaneUiTextSize size)
        {
            ArcaneUiTextSize sanitized = size switch
            {
                ArcaneUiTextSize.Small => ArcaneUiTextSize.Small,
                ArcaneUiTextSize.Large => ArcaneUiTextSize.Large,
                _ => ArcaneUiTextSize.Medium
            };
            if (Current == sanitized && PlayerPrefs.HasKey(TextSizeKey))
                return;

            PlayerPrefs.SetInt(TextSizeKey, (int)sanitized);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ResetToDefault()
        {
            Set(ArcaneUiTextSize.Medium);
        }
    }
}
