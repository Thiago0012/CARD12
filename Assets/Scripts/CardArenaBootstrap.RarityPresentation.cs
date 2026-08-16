using ArcaneArena.Cards;
using ArcaneDuel.DuelEngine.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private GameObject detailRarityBadge;
        private Image detailRarityBadgeBackground;
        private Image detailRarityUrBlueHalf;
        private Text detailRarityLabel;

        private void BuildDetailRarityBadge()
        {
            if (detailArtwork == null || detailRarityBadge != null)
                return;
            detailRarityBadge = CreatePanel(
                detailArtwork.transform,
                "Raridade da Carta",
                new Vector2(0.70f, 0.82f),
                new Vector2(0.985f, 0.985f),
                Color.clear);
            detailRarityBadgeBackground =
                detailRarityBadge.GetComponent<Image>();
            detailRarityBadgeBackground.raycastTarget = false;
            AddOutline(
                detailRarityBadge,
                new Color(1f, 1f, 1f, 0.82f));
            detailRarityUrBlueHalf = CreateImage(
                detailRarityBadge.transform,
                "Gradiente azul UR",
                new Vector2(0.52f, 0f),
                Vector2.one,
                new Color(0.12f, 0.55f, 1f, 0.85f));
            detailRarityUrBlueHalf.raycastTarget = false;
            detailRarityLabel = CreateText(
                detailRarityBadge.transform,
                "?",
                14,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            detailRarityLabel.raycastTarget = false;
            detailRarityLabel.transform.SetAsLastSibling();
            detailRarityBadge.SetActive(false);
        }

        private void RefreshDetailRarity(
            CardCatalogEntry entry,
            CardRecord card)
        {
            BuildDetailRarityBadge();
            CardRarity rarity = entry?.Rarity ?? CardRarity.Unknown;
            if (!CardRarityCatalog.IsValid(rarity) && card != null)
                CardRarityCatalog.TryResolve(card.EnglishName, out rarity);
            bool visible = CardRarityCatalog.IsValid(rarity);
            if (detailRarityBadge == null)
                return;
            detailRarityBadge.SetActive(visible);
            if (!visible)
                return;
            detailRarityBadgeBackground.color = rarity switch
            {
                CardRarity.N => Hex("#84919C"),
                CardRarity.R => Hex("#2588E4"),
                CardRarity.SR => Hex("#D5A900"),
                CardRarity.UR => Hex("#8E35EA"),
                _ => Muted
            };
            detailRarityUrBlueHalf.gameObject.SetActive(
                rarity == CardRarity.UR);
            detailRarityLabel.text = CardRarityCatalog.Label(rarity);
            detailRarityBadge.transform.SetAsLastSibling();
        }
    }
}
