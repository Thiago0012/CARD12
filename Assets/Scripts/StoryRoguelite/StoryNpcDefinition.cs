using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    [CreateAssetMenu(
        fileName = "StoryNpcDefinition",
        menuName = "Arcane Duel/Story Roguelite/NPC Definition")]
    public sealed class StoryNpcDefinition : ScriptableObject
    {
        [SerializeField] private string npcId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite portraitSprite;
        [SerializeField] private string portraitResourcePath;
        [SerializeField] private NpcPresentation presentation;
        [SerializeField] private EncounterRole role = EncounterRole.Normal;
        [SerializeField, Range(1, 3)] private int firstAct = 1;
        [SerializeField, Range(1, 3)] private int lastAct = 3;
        [SerializeField, Range(1, 5)] private int aiTierMin = 1;
        [SerializeField, Range(1, 5)] private int aiTierMax = 2;
        [SerializeField] private NpcDeckSelectionMode deckSelectionMode =
            NpcDeckSelectionMode.Seasonal;
        [SerializeField] private string fixedDeckId;
        [SerializeField] private List<string> preferredDeckFamilies = new();
        [SerializeField] private List<string> explicitDeckPool = new();
        [SerializeField] private string personalityProfileId;
        [SerializeField] private string dialogueSetId;
        [SerializeField] private bool recurring;
        [SerializeField] private bool enabled = true;

        public string NpcId => npcId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? $"Duelista {NpcId}"
            : displayName.Trim();
        public Sprite PortraitSprite => portraitSprite;
        public string PortraitResourcePath => portraitResourcePath ?? string.Empty;
        public NpcPresentation Presentation => presentation;
        public EncounterRole Role => role;
        public int FirstAct => firstAct;
        public int LastAct => lastAct;
        public int AiTierMin => aiTierMin;
        public int AiTierMax => aiTierMax;
        public NpcDeckSelectionMode DeckSelectionMode => deckSelectionMode;
        public string FixedDeckId => fixedDeckId ?? string.Empty;
        public IReadOnlyList<string> PreferredDeckFamilies => preferredDeckFamilies;
        public IReadOnlyList<string> ExplicitDeckPool => explicitDeckPool;
        public string PersonalityProfileId => personalityProfileId ?? string.Empty;
        public string DialogueSetId => dialogueSetId ?? string.Empty;
        public bool IsRecurring => recurring;
        public bool Enabled => enabled;

        public void Initialize(StoryNpcRecord source, Sprite portrait)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            npcId = source.npcId;
            displayName = source.displayName;
            portraitSprite = portrait;
            portraitResourcePath = source.portraitResourcePath;
            presentation = StoryContentCatalog.ParsePresentation(source.presentation);
            role = StoryContentCatalog.ParseRole(source.role);
            firstAct = Mathf.Clamp(source.firstAct, 1, 3);
            lastAct = Mathf.Clamp(source.lastAct, firstAct, 3);
            aiTierMin = Mathf.Clamp(source.aiTierMin, 1, 5);
            aiTierMax = Mathf.Clamp(source.aiTierMax, aiTierMin, 5);
            deckSelectionMode = NpcDeckSelectionMode.Seasonal;
            fixedDeckId = source.fixedDeckId ?? string.Empty;
            personalityProfileId = source.personalityProfileId ?? string.Empty;
            dialogueSetId = source.dialogueSetId ?? "duel-default";
            recurring = source.recurring;
            enabled = source.enabled;
        }

        public StoryNpcRecord ToRuntimeRecord()
        {
            return new StoryNpcRecord
            {
                npcId = NpcId,
                displayName = DisplayName,
                portraitResourcePath = PortraitResourcePath,
                presentation = Presentation.ToString(),
                role = Role.ToString().Replace(", ", "|"),
                firstAct = FirstAct,
                lastAct = LastAct,
                aiTierMin = AiTierMin,
                aiTierMax = AiTierMax,
                fixedDeckId = FixedDeckId,
                personalityProfileId = PersonalityProfileId,
                dialogueSetId = DialogueSetId,
                recurring = IsRecurring,
                enabled = Enabled
            };
        }
    }
}
