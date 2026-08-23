using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public enum ActivationPromptMode
    {
        On = 0,
        Auto = 1,
        Off = 2
    }

    /// <summary>
    /// Local response-presentation preferences. These values never add,
    /// remove or validate Core candidates; they may only choose the pass
    /// response already present in an optional prompt.
    /// </summary>
    public static class DuelActivationPreferences
    {
        private const int CurrentPreferencesSchema = 1;
        private const string PreferencesSchemaKey =
            "ArcaneDuel.ActivationPreferencesSchema";
        private const string ModeKey = "ArcaneDuel.ActivationPromptMode";
        private const string SelfChainKey = "ArcaneDuel.SelfChain";
        private const string ManualOrderKey = "ArcaneDuel.ManualChainOrder";
        private const string GuidanceMessagesKey =
            "ArcaneDuel.GuidanceMessages";
        private const string ChainPanelKey = "ArcaneDuel.ChainPanel";

        public static ActivationPromptMode Mode
        {
            get
            {
                EnsureEffectPromptSafetyMigration();
                int stored = PlayerPrefs.GetInt(
                    ModeKey,
                    (int)ActivationPromptMode.On);
                return stored >= (int)ActivationPromptMode.On &&
                       stored <= (int)ActivationPromptMode.Off
                    ? (ActivationPromptMode)stored
                    : ActivationPromptMode.On;
            }
            set
            {
                EnsureEffectPromptSafetyMigration();
                PlayerPrefs.SetInt(ModeKey, (int)value);
                PlayerPrefs.SetInt(
                    PreferencesSchemaKey,
                    CurrentPreferencesSchema);
                PlayerPrefs.Save();
            }
        }

        public static bool SelfChainEnabled
        {
            get
            {
                EnsureEffectPromptSafetyMigration();
                return PlayerPrefs.GetInt(SelfChainKey, 1) != 0;
            }
            set
            {
                EnsureEffectPromptSafetyMigration();
                PlayerPrefs.SetInt(SelfChainKey, value ? 1 : 0);
                PlayerPrefs.SetInt(
                    PreferencesSchemaKey,
                    CurrentPreferencesSchema);
                PlayerPrefs.Save();
            }
        }

        public static bool ManualChainOrder
        {
            get => PlayerPrefs.GetInt(ManualOrderKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(ManualOrderKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Controls only the large, non-interactive guidance ribbon shown at
        /// the top of the field. Required choices remain available.
        /// </summary>
        public static bool GuidanceMessagesEnabled
        {
            get => PlayerPrefs.GetInt(GuidanceMessagesKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(GuidanceMessagesKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Controls the non-interactive red chain summary panel. The chain
        /// itself and every legal response continue to be processed by Core.
        /// </summary>
        public static bool ChainPanelEnabled
        {
            get => PlayerPrefs.GetInt(ChainPanelKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(ChainPanelKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static string DisplayName(ActivationPromptMode mode)
        {
            return mode switch
            {
                ActivationPromptMode.On => "ON",
                ActivationPromptMode.Off => "OFF",
                _ => "AUTO"
            };
        }

        public static void RestoreDefaults()
        {
            PlayerPrefs.SetInt(
                ModeKey,
                (int)ActivationPromptMode.On);
            PlayerPrefs.SetInt(SelfChainKey, 1);
            PlayerPrefs.SetInt(ManualOrderKey, 1);
            PlayerPrefs.SetInt(GuidanceMessagesKey, 1);
            PlayerPrefs.SetInt(ChainPanelKey, 1);
            PlayerPrefs.SetInt(
                PreferencesSchemaKey,
                CurrentPreferencesSchema);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Earlier builds presented OFF next to the visual-message controls,
        /// which made it easy to disable legal Trap, Quick Effect and monster
        /// response windows while intending to hide only the large guidance
        /// panels. Migrate that ambiguous persisted state once. A player can
        /// still explicitly select OFF afterwards; the schema marker prevents
        /// a subsequent startup from overriding that deliberate choice.
        /// </summary>
        private static void EnsureEffectPromptSafetyMigration()
        {
            if (PlayerPrefs.GetInt(PreferencesSchemaKey, 0) >=
                CurrentPreferencesSchema)
            {
                return;
            }

            PlayerPrefs.SetInt(
                ModeKey,
                (int)ActivationPromptMode.On);
            PlayerPrefs.SetInt(SelfChainKey, 1);
            PlayerPrefs.SetInt(
                PreferencesSchemaKey,
                CurrentPreferencesSchema);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Applies user notification preferences to an already legal prompt.
    /// It can only return a decline/pass choice emitted by ocgcore.
    /// </summary>
    public static class DuelActivationPromptPolicy
    {
        public static bool TryGetAutomaticPass(
            DuelPrompt prompt,
            byte? lastChainPlayer,
            out DuelChoice passChoice,
            out string reason)
        {
            return TryGetAutomaticPass(
                prompt,
                DuelActivationPreferences.Mode,
                DuelActivationPreferences.SelfChainEnabled,
                lastChainPlayer,
                out passChoice,
                out reason);
        }

        public static bool TryGetAutomaticPass(
            DuelPrompt prompt,
            ActivationPromptMode mode,
            bool selfChainEnabled,
            byte? lastChainPlayer,
            out DuelChoice passChoice,
            out string reason)
        {
            passChoice = null;
            reason = string.Empty;
            if (prompt == null || prompt.Player != 0 || prompt.Forced ||
                !IsOptionalActivationPrompt(prompt))
            {
                return false;
            }

            DuelChoice decline =
                DuelPromptPresentationRules.DeclineChoice(prompt);
            if (decline == null || decline.Response == null ||
                decline.Response.Length == 0)
            {
                return false;
            }

            bool hasActionableCandidate =
                DuelPromptPresentationRules
                    .ActionableResponseChoices(prompt).Count > 0;
            if (!hasActionableCandidate)
            {
                passChoice = decline;
                reason = "Nenhuma resposta legal disponível";
                return true;
            }

            bool selfChainOpportunity =
                lastChainPlayer.HasValue &&
                lastChainPlayer.Value == prompt.Player;
            if (selfChainOpportunity && selfChainEnabled)
                return false;

            if (mode == ActivationPromptMode.On)
                return false;
            if (mode == ActivationPromptMode.Auto &&
                !selfChainOpportunity)
            {
                // AUTO keeps ordinary legal response windows visible. Its
                // only automatic candidate-bearing pass is the user's own
                // follow-up window when Self Chain was explicitly disabled.
                return false;
            }

            passChoice = decline;
            reason = mode == ActivationPromptMode.Off
                ? "Confirmação de efeitos em OFF"
                : "Self Chain desativado";
            return true;
        }

        public static bool TryGetAutomaticSort(
            DuelPrompt prompt,
            bool manualChainOrder,
            out DuelChoice keepCoreOrder)
        {
            keepCoreOrder = null;
            if (manualChainOrder || prompt == null || prompt.Player != 0 ||
                prompt.Message != CoreMessage.SortChain ||
                !prompt.RequiresOrderedSelection)
            {
                return false;
            }

            foreach (DuelChoice choice in prompt.Choices)
            {
                if (choice?.Response?.Length == 1 &&
                    choice.Response[0] == 0xFF)
                {
                    keepCoreOrder = choice;
                    return true;
                }
            }
            return false;
        }

        private static bool IsOptionalActivationPrompt(DuelPrompt prompt)
        {
            // ON/AUTO/OFF controls response windows in a Chain. A direct
            // question such as SELECT_EFFECT_YESNO is the Core asking about
            // the card currently resolving (for example, a Synchro summon
            // trigger). Silently declining that question makes a legal
            // effect look broken, so it must always reach the player.
            return prompt.Message == CoreMessage.SelectChain;
        }
    }
}
