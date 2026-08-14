using UnityEngine;

namespace ArcaneArena.Frontend
{
    [CreateAssetMenu(
        fileName = "DuelStatsVisualizationConfig",
        menuName = "Arcane Duel/Perfil/Configuração do Gráfico")]
    public sealed class DuelStatsVisualizationConfig : ScriptableObject
    {
        public const string ResourcePath =
            "Frontend/DuelStatsVisualizationConfig";

        [Min(1f)] public float damagePerDuelCap = 8000f;
        [Min(1f)] public float summonsPerDuelCap = 12f;
        [Min(1f)] public float battleDestroysPerDuelCap = 8f;
        [Min(1f)] public float effectDestroysPerDuelCap = 8f;
        [Min(1f)] public float spellActionsPerDuelCap = 12f;
        [Min(1f)] public float trapActionsPerDuelCap = 12f;

        private static DuelStatsVisualizationConfig _fallback;

        public static DuelStatsVisualizationConfig Resolve()
        {
            DuelStatsVisualizationConfig configured =
                Resources.Load<DuelStatsVisualizationConfig>(ResourcePath);
            if (configured != null)
                return configured;
            if (_fallback == null)
            {
                _fallback = CreateInstance<DuelStatsVisualizationConfig>();
                _fallback.name = "Configuração Padrão do Perfil de Duelo";
                _fallback.hideFlags = HideFlags.HideAndDontSave;
            }
            return _fallback;
        }

        public static float Normalize(float value, float cap)
        {
            return Mathf.Clamp01(Mathf.Max(0f, value) / Mathf.Max(1f, cap));
        }
    }
}
