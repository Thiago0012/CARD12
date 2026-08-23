using ArcaneArena.Cards;
using ArcaneDuel.DuelEngine.Data;
using UnityEngine;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private const string LegacyDuelRarityBadgeName = "Raridade da Carta";

        private void BuildDetailRarityBadge()
        {
            RemoveLegacyDuelRarityBadge();
        }

        private void RefreshDetailRarity(
            CardCatalogEntry entry,
            CardRecord card)
        {
            RemoveLegacyDuelRarityBadge();
        }

        private void RemoveLegacyDuelRarityBadge()
        {
            if (detailArtwork == null)
                return;

            Transform artwork = detailArtwork.transform;
            for (int index = artwork.childCount - 1; index >= 0; index--)
            {
                Transform child = artwork.GetChild(index);
                if (child.name != LegacyDuelRarityBadgeName)
                    continue;

                // O selo de raridade pertence apenas às telas de coleção,
                // loja e editor. Na arena ele também encobria a arte da carta.
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
    }
}
