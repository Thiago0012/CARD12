using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneDuel.Game
{
    [CreateAssetMenu(
        fileName = "StarterDeckCatalog",
        menuName = "Arcane Arena/Starter Deck Catalog")]
    public sealed class StarterDeckCatalog : ScriptableObject
    {
        [SerializeField] private int catalogVersion = 1;
        [SerializeField] private string activeBanlistId;
        [SerializeField] private StarterLegacyPolicy legacyPolicy =
            StarterLegacyPolicy.LegacyPromptOnce;
        [SerializeField] private List<StarterDeckDefinition> decks =
            new List<StarterDeckDefinition>();

        public int CatalogVersion => catalogVersion;
        public string ActiveBanlistId => activeBanlistId ?? string.Empty;
        public StarterLegacyPolicy LegacyPolicy => legacyPolicy;
        public IReadOnlyList<StarterDeckDefinition> Decks => decks;

        public void Initialize(
            int version,
            string banlistId,
            StarterLegacyPolicy policy,
            IReadOnlyList<StarterDeckDefinition> definitions)
        {
            catalogVersion = Math.Max(1, version);
            activeBanlistId = banlistId ?? string.Empty;
            legacyPolicy = policy;
            decks = new List<StarterDeckDefinition>(
                definitions ?? Array.Empty<StarterDeckDefinition>());
        }

        public StarterDeckDefinition Find(string deckId)
        {
            return decks.Find(deck => deck != null &&
                string.Equals(deck.Id, deckId, StringComparison.Ordinal));
        }
    }
}
