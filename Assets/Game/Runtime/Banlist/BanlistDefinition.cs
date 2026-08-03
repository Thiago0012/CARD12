using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneDuel.Game
{
    [Serializable]
    public sealed class BanlistEntry
    {
        public string officialName;
        public string passcode;
        [Range(0, 2)] public int maxCopies;
    }

    [Serializable]
    public sealed class BanlistSeedFile
    {
        public int schemaVersion = 1;
        public string id;
        public string effectiveDate;
        public string sourceSha256;
        public List<BanlistEntry> entries = new List<BanlistEntry>();
    }

    [CreateAssetMenu(
        fileName = "BanlistDefinition",
        menuName = "Arcane Arena/Banlist Definition")]
    public sealed class BanlistDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string effectiveDate;
        [SerializeField] private string sourceSha256;
        [SerializeField] private List<BanlistEntry> entries =
            new List<BanlistEntry>();
        [SerializeField] private Sprite forbiddenBadge;
        [SerializeField] private Sprite limitedBadge;
        [SerializeField] private Sprite semiLimitedBadge;

        public string Id => id ?? string.Empty;
        public string EffectiveDate => effectiveDate ?? string.Empty;
        public string SourceSha256 => sourceSha256 ?? string.Empty;
        public IReadOnlyList<BanlistEntry> Entries => entries;
        public Sprite ForbiddenBadge => forbiddenBadge;
        public Sprite LimitedBadge => limitedBadge;
        public Sprite SemiLimitedBadge => semiLimitedBadge;

        public void Initialize(
            BanlistSeedFile seed,
            Sprite forbidden,
            Sprite limited,
            Sprite semiLimited)
        {
            if (seed == null)
                throw new ArgumentNullException(nameof(seed));

            id = seed.id ?? string.Empty;
            effectiveDate = seed.effectiveDate ?? string.Empty;
            sourceSha256 = seed.sourceSha256 ?? string.Empty;
            entries = new List<BanlistEntry>(
                seed.entries ?? new List<BanlistEntry>());
            forbiddenBadge = forbidden;
            limitedBadge = limited;
            semiLimitedBadge = semiLimited;
        }
    }
}
