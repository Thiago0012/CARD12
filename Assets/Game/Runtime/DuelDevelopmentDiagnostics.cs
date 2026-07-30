using System;
using System.Diagnostics;
using UnityEngine;

namespace ArcaneDuel.Game
{
    [Flags]
    public enum DuelLogCategory
    {
        None = 0,
        Core = 1 << 0,
        CoreMessage = 1 << 1,
        Selection = 1 << 2,
        CardInstance = 1 << 3,
        Zone = 1 << 4,
        UiBinding = 1 << 5,
        BotDecision = 1 << 6,
        ExtraDeck = 1 << 7,
        StateSync = 1 << 8,
        Animation = 1 << 9,
        Error = 1 << 10,
        All = ~0
    }

    public static class DuelDevelopmentLog
    {
        private static DuelLogCategory enabled =
            DuelLogCategory.Error |
            DuelLogCategory.StateSync |
            DuelLogCategory.CardInstance;

        public static void Configure(DuelLogCategory categories)
        {
            enabled = categories;
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Write(
            DuelLogCategory category,
            string message,
            UnityEngine.Object context = null)
        {
            if ((enabled & category) == 0)
                return;
            string line = $"[Arcane Duel][{category}] {message}";
            if (category == DuelLogCategory.Error)
                UnityEngine.Debug.LogError(line, context);
            else if (category == DuelLogCategory.StateSync)
                UnityEngine.Debug.LogWarning(line, context);
            else
                UnityEngine.Debug.Log(line, context);
        }
    }

    [DisallowMultipleComponent]
    public sealed class DuelDiagnosticsSettings : MonoBehaviour
    {
        [SerializeField] private DuelLogCategory enabledCategories =
            DuelLogCategory.Error |
            DuelLogCategory.StateSync |
            DuelLogCategory.CardInstance |
            DuelLogCategory.BotDecision;

        private void Awake()
        {
            DuelDevelopmentLog.Configure(enabledCategories);
        }

        private void OnValidate()
        {
            DuelDevelopmentLog.Configure(enabledCategories);
        }
    }
}
