using System;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Janela única usada pelo matchmaking para oferecer um rival IA quando
    /// nenhum jogador compatível entra na fila em tempo hábil.
    /// </summary>
    public static class RankedBotFallbackPolicy
    {
        public const float MinimumDelaySeconds = 30f;
        public const float MaximumDelaySeconds = 80f;

        public static float DelaySeconds(float normalizedSample)
        {
            if (float.IsNaN(normalizedSample) ||
                float.IsInfinity(normalizedSample))
            {
                normalizedSample = 0f;
            }
            float sample = Math.Max(0f, Math.Min(1f, normalizedSample));
            return MinimumDelaySeconds +
                   (MaximumDelaySeconds - MinimumDelaySeconds) * sample;
        }
    }
}
