using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public enum StarterLegacyPolicy
    {
        LegacyPromptOnce = 0,
        MarkCompletedByMigration = 1
    }

    [Serializable]
    public sealed class RawStarterDeckDefinition
    {
        public string sourceTitle;
        public string sourceUrl;
        public string sourceCorrectionNote;
        public List<string> mainDeck = new List<string>();
        public List<string> extraDeck = new List<string>();
        public List<string> sideDeck = new List<string>();
    }

    [Serializable]
    public sealed class ReplacementAuditEntry
    {
        public string removedPasscode;
        public string replacementPasscode;
        public string section;
        public string reason;
        public bool approved;
    }

    [Serializable]
    public sealed class StarterDeckSourceRecord
    {
        public string id;
        public string displayName;
        public RawStarterDeckDefinition raw = new RawStarterDeckDefinition();
        public List<ReplacementAuditEntry> approvedReplacements =
            new List<ReplacementAuditEntry>();
    }

    [Serializable]
    public sealed class StarterDeckSourceCatalogFile
    {
        public int schemaVersion = 1;
        public int catalogVersion = 1;
        public List<StarterDeckSourceRecord> decks =
            new List<StarterDeckSourceRecord>();
    }

    [CreateAssetMenu(
        fileName = "StarterDeckDefinition",
        menuName = "Arcane Arena/Starter Deck Definition")]
    public sealed class StarterDeckDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string banlistVersion;
        [SerializeField] private RawStarterDeckDefinition raw;
        [SerializeField] private List<string> mainDeck = new List<string>();
        [SerializeField] private List<string> extraDeck = new List<string>();
        [SerializeField] private List<string> sideDeck = new List<string>();
        [SerializeField] private List<string> previewCardIds = new List<string>();
        [SerializeField] private string rawSha256;
        [SerializeField] private string sanitizedSha256;
        [SerializeField] private List<ReplacementAuditEntry> replacements =
            new List<ReplacementAuditEntry>();
        [SerializeField] private List<string> validationIssues =
            new List<string>();
        [SerializeField] private bool publishable;

        public string Id => id ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string BanlistVersion => banlistVersion ?? string.Empty;
        public RawStarterDeckDefinition Raw => raw;
        public IReadOnlyList<string> MainDeck => mainDeck;
        public IReadOnlyList<string> ExtraDeck => extraDeck;
        public IReadOnlyList<string> SideDeck => sideDeck;
        public IReadOnlyList<string> PreviewCardIds => previewCardIds;
        public string RawSha256 => rawSha256 ?? string.Empty;
        public string SanitizedSha256 => sanitizedSha256 ?? string.Empty;
        public IReadOnlyList<ReplacementAuditEntry> Replacements => replacements;
        public IReadOnlyList<string> ValidationIssues => validationIssues;
        public bool IsPublishable => publishable;

        public void Initialize(
            StarterDeckSourceRecord source,
            StarterDeckSanitizationResult sanitized,
            string activeBanlist,
            IReadOnlyList<string> previews,
            IReadOnlyList<string> issues)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (sanitized == null)
                throw new ArgumentNullException(nameof(sanitized));

            id = source.id ?? string.Empty;
            displayName = source.displayName ?? source.id ?? string.Empty;
            banlistVersion = activeBanlist ?? string.Empty;
            raw = source.raw ?? new RawStarterDeckDefinition();
            mainDeck = new List<string>(sanitized.MainDeck);
            extraDeck = new List<string>(sanitized.ExtraDeck);
            sideDeck = new List<string>(sanitized.SideDeck);
            previewCardIds = new List<string>(previews ?? Array.Empty<string>());
            rawSha256 = DeckManifestHasher.ComputeSha256(
                activeBanlist,
                raw.mainDeck,
                raw.extraDeck,
                raw.sideDeck);
            sanitizedSha256 = DeckManifestHasher.ComputeSha256(
                activeBanlist,
                mainDeck,
                extraDeck,
                sideDeck);
            replacements = new List<ReplacementAuditEntry>(sanitized.Audit);
            validationIssues = new List<string>(issues ?? Array.Empty<string>());
            publishable = sanitized.IsLegal && validationIssues.Count == 0 &&
                previewCardIds.Count == 3;
        }
    }

}
