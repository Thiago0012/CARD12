using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.Editor.AutoPacks
{
    [CreateAssetMenu(
        fileName = "AutoPackGenerationManifest",
        menuName = "Game/Shop/Auto Pack Generation Manifest")]
    public sealed class AutoPackGenerationManifest : ScriptableObject
    {
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private int generatorVersion = 1;
        [SerializeField] private string lastSourceCatalogHash = string.Empty;
        [SerializeField] private int nextPackSequence = 1;
        [SerializeField] private List<string> pendingCardIds = new();
        [SerializeField] private List<GeneratedPackRecord> generatedPacks = new();

        public int SchemaVersion => schemaVersion;
        public int GeneratorVersion => generatorVersion;
        public string LastSourceCatalogHash => lastSourceCatalogHash ?? string.Empty;
        public int NextPackSequence => Math.Max(1, nextPackSequence);
        public IReadOnlyList<string> PendingCardIds => pendingCardIds;
        public IReadOnlyList<GeneratedPackRecord> GeneratedPacks => generatedPacks;

        internal void Commit(
            int activeGeneratorVersion,
            string sourceHash,
            int nextSequence,
            IEnumerable<string> pending,
            IEnumerable<GeneratedPackRecord> appended)
        {
            schemaVersion = 1;
            generatorVersion = Math.Max(1, activeGeneratorVersion);
            lastSourceCatalogHash = sourceHash ?? string.Empty;
            nextPackSequence = Math.Max(1, nextSequence);
            pendingCardIds = (pending ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            generatedPacks ??= new List<GeneratedPackRecord>();
            if (appended != null)
                generatedPacks.AddRange(appended.Where(record => record != null));
        }
    }
}
