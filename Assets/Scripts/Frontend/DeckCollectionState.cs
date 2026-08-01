using System;
using System.Collections.Generic;

namespace ArcaneArena.Frontend
{
    [Serializable]
    public sealed class DeckCollectionState
    {
        public int schemaVersion = 3;
        public string localProfileId;
        public string playerDisplayName;
        public string selectedDeckId;
        public List<DeckRecord> decks = new List<DeckRecord>();
        public List<string> unlockedDeckProductIds =
            new List<string>();
    }

    /// <summary>
    /// Cópia imutável por convenção do deck escolhido para uma nova partida.
    /// Contém somente IDs estáveis e dados serializáveis: nenhuma referência de
    /// cena, Sprite ou estado visual participa da identidade do loadout.
    /// </summary>
    [Serializable]
    public sealed class DuelDeckLoadout
    {
        public string profileId;
        public string playerDisplayName;
        public string deckId;
        public string displayName;
        public List<string> mainDeckCardIds = new List<string>();
        public List<string> extraDeckCardIds = new List<string>();

        public static DuelDeckLoadout Create(
            string profileId,
            DeckRecord deck,
            string playerDisplayName = null)
        {
            if (deck == null)
                return null;

            deck.Normalize();
            return new DuelDeckLoadout
            {
                profileId = profileId ?? string.Empty,
                playerDisplayName = playerDisplayName ?? string.Empty,
                deckId = deck.deckId ?? string.Empty,
                displayName = deck.displayName ?? "Deck sem nome",
                mainDeckCardIds = new List<string>(
                    deck.mainDeckCardIds),
                extraDeckCardIds = new List<string>(
                    deck.extraDeckCardIds)
            };
        }
    }

    [Serializable]
    public sealed class DeckRecord
    {
        public string deckId;
        public string displayName;
        public int caseTheme;
        public List<string> mainDeckCardIds = new List<string>();
        public List<string> extraDeckCardIds = new List<string>();
        public List<string> featuredCardIds = new List<string>();

        public int TotalCards =>
            (mainDeckCardIds?.Count ?? 0) +
            (extraDeckCardIds?.Count ?? 0);

        public void Normalize()
        {
            mainDeckCardIds ??= new List<string>();
            extraDeckCardIds ??= new List<string>();
            featuredCardIds ??= new List<string>();
            if (string.IsNullOrWhiteSpace(deckId))
                deckId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Novo Deck";
            caseTheme = Math.Max(0, caseTheme);
            RefreshFeaturedCards();
        }

        public void RefreshFeaturedCards()
        {
            featuredCardIds ??= new List<string>();
            featuredCardIds.Clear();

            AppendFeatured(mainDeckCardIds);
            if (featuredCardIds.Count < 3)
                AppendFeatured(extraDeckCardIds);
        }

        private void AppendFeatured(List<string> source)
        {
            if (source == null)
                return;

            foreach (var cardId in source)
            {
                if (featuredCardIds.Count >= 3)
                    return;
                if (string.IsNullOrWhiteSpace(cardId) ||
                    featuredCardIds.Contains(cardId))
                {
                    continue;
                }

                featuredCardIds.Add(cardId);
            }
        }
    }
}
