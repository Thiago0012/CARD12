using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private readonly Dictionary<CardRarity, Text> _craftWalletTexts = new();
        private Text _deckEditorRarityText;
        private Text _deckEditorOwnershipText;
        private Button _generateCardButton;
        private Button _dismantleCardButton;
        private GameObject _deckEditorArtworkRarityBadge;
        private GameObject _craftConfirmationModal;

        private void BuildCraftWalletBar()
        {
            _craftWalletTexts.Clear();
            Image bar = CreatePanel(
                _screenRoot,
                "Saldos de Craft Points",
                new Vector2(0.015f, 0.834f),
                new Vector2(0.985f, 0.889f),
                new Color(0.012f, 0.035f, 0.06f, 0.99f));
            AddOutline(
                bar.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.58f),
                new Vector2(1.5f, -1.5f));
            CreateText(
                bar.transform,
                "CRAFT POINTS",
                14,
                FontStyle.Bold,
                Muted,
                new Vector2(0.015f, 0.08f),
                new Vector2(0.14f, 0.92f),
                TextAnchor.MiddleLeft);

            CardRarity[] rarities =
                { CardRarity.N, CardRarity.R, CardRarity.SR, CardRarity.UR };
            for (int index = 0; index < rarities.Length; index++)
            {
                CardRarity rarity = rarities[index];
                float minX = 0.15f + index * 0.2075f;
                float maxX = minX + 0.19f;
                Image balance = CreatePanel(
                    bar.transform,
                    $"Saldo CP {rarity}",
                    new Vector2(minX, 0.10f),
                    new Vector2(maxX, 0.90f),
                    new Color(0.02f, 0.065f, 0.10f, 0.98f));
                AddOutline(
                    balance.gameObject,
                    RarityColor(rarity),
                    new Vector2(1.5f, -1.5f));
                CreateRarityBadge(
                    balance.transform,
                    rarity,
                    new Vector2(0.03f, 0.12f),
                    new Vector2(0.31f, 0.88f),
                    14);
                Text value = CreateText(
                    balance.transform,
                    _repository.CraftPointBalance(rarity).ToString(),
                    18,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.34f, 0.04f),
                    new Vector2(0.96f, 0.96f),
                    TextAnchor.MiddleRight);
                _craftWalletTexts[rarity] = value;
            }
        }

        private void BuildDeckEditorCraftActions(Transform panel)
        {
            _deckEditorRarityText = CreateText(
                panel,
                "RARIDADE ?  •  POSSUI 0  •  DESMONTÁVEIS 0",
                13,
                FontStyle.Bold,
                Muted,
                new Vector2(0.04f, 0.305f),
                new Vector2(0.96f, 0.342f),
                TextAnchor.MiddleCenter);
            _deckEditorOwnershipText = _deckEditorRarityText;

            Image dismantle = CreateButton(
                panel,
                "DESMANTELAR  +10",
                new Vector2(0.035f, 0.025f),
                new Vector2(0.49f, 0.105f),
                Danger,
                () => ShowCraftConfirmation(false));
            _dismantleCardButton = dismantle.GetComponent<Button>();
            Image generate = CreateButton(
                panel,
                "GERAR  -30",
                new Vector2(0.51f, 0.025f),
                new Vector2(0.965f, 0.105f),
                Blue,
                () => ShowCraftConfirmation(true));
            _generateCardButton = generate.GetComponent<Button>();
        }

        private void RefreshDeckEditorCraftDetails(
            CardCatalogEntry entry,
            string cardId)
        {
            if (entry == null || _repository == null)
                return;
            int owned = _repository.OwnedCardQuantity(cardId);
            int eligible = _repository.DismantlableCardQuantity(cardId);
            int protectedQuantity = _repository.ProtectedCardQuantity(cardId);
            string protectedLabel = protectedQuantity > 0
                ? $"  •  PROTEGIDAS {protectedQuantity}"
                : string.Empty;
            if (_deckEditorOwnershipText != null)
            {
                _deckEditorOwnershipText.text =
                    $"RARIDADE {CardRarityCatalog.Label(entry.Rarity)}  •  " +
                    $"POSSUI {owned}  •  DESMONTÁVEIS {eligible}" + protectedLabel;
                _deckEditorOwnershipText.color =
                    CardRarityCatalog.IsValid(entry.Rarity)
                        ? RarityColor(entry.Rarity)
                        : Muted;
            }
            if (_deckEditorArtworkRarityBadge != null)
                Destroy(_deckEditorArtworkRarityBadge);
            _deckEditorArtworkRarityBadge = null;
            if (_deckEditorDetailArtwork != null &&
                CardRarityCatalog.IsValid(entry.Rarity))
            {
                _deckEditorArtworkRarityBadge = CreateRarityBadge(
                    _deckEditorDetailArtwork.transform,
                    entry.Rarity,
                    new Vector2(0.70f, 0.82f),
                    new Vector2(0.985f, 0.985f),
                    13).gameObject;
            }
            bool canGenerate =
                entry.IsCraftable &&
                _repository.CraftPointBalance(entry.Rarity) >=
                    CardRarityCatalog.GenerateCost(entry.Rarity);
            bool canDismantle = entry.IsDismantlable && eligible > 0;
            SetCraftButtonInteractable(_generateCardButton, canGenerate);
            SetCraftButtonInteractable(_dismantleCardButton, canDismantle);
        }

        private void ShowCraftConfirmation(bool generate)
        {
            string cardId = _deckEditorSelectedCardId;
            CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
            if (entry == null || !CardRarityCatalog.IsValid(entry.Rarity))
                return;
            if (_craftConfirmationModal != null)
                Destroy(_craftConfirmationModal);

            int before = _repository.CraftPointBalance(entry.Rarity);
            int delta = generate
                ? -CardRarityCatalog.GenerateCost(entry.Rarity)
                : CardRarityCatalog.DismantleReturn(entry.Rarity);
            IReadOnlyList<string> affected = generate
                ? Array.Empty<string>()
                : _repository.DecksAffectedByDismantle(cardId, 1);
            Image veil = CreatePanel(
                _screenRoot,
                "Confirmação de craft",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0.01f, 0.025f, 0.94f));
            _craftConfirmationModal = veil.gameObject;
            Image modal = CreatePanel(
                veil.transform,
                generate ? "Confirmar geração" : "Confirmar desmontagem",
                new Vector2(0.27f, 0.24f),
                new Vector2(0.73f, 0.76f),
                new Color(0.015f, 0.045f, 0.075f, 1f));
            Color accent = generate ? Blue : Danger;
            AddOutline(modal.gameObject, accent, new Vector2(3f, -3f));
            CreateText(
                modal.transform,
                generate ? "CONFIRMAR GERAÇÃO" : "CONFIRMAR DESMONTAGEM",
                28,
                FontStyle.Bold,
                accent,
                new Vector2(0.06f, 0.80f),
                new Vector2(0.94f, 0.94f),
                TextAnchor.MiddleCenter);
            CreateText(
                modal.transform,
                $"{entry.DisplayName}\n" +
                $"RARIDADE {entry.Rarity}  •  QUANTIDADE 1\n" +
                $"CP {entry.Rarity}: {before}  →  {before + delta}\n" +
                (generate
                    ? $"A coleção passará a ter {_repository.OwnedCardQuantity(cardId) + 1} cópia(s)."
                    : $"A coleção passará a ter {Math.Max(0, _repository.OwnedCardQuantity(cardId) - 1)} cópia(s)."),
                19,
                FontStyle.Normal,
                Color.white,
                new Vector2(0.08f, 0.42f),
                new Vector2(0.92f, 0.78f),
                TextAnchor.MiddleCenter);
            Text feedback = CreateText(
                modal.transform,
                affected.Count > 0
                    ? "ATENÇÃO: afetará " + string.Join(", ", affected) + "."
                    : generate
                        ? "A geração usa CP da mesma raridade."
                        : "Cópias de Deck Estrutural nunca são removidas.",
                15,
                FontStyle.Bold,
                affected.Count > 0 ? Gold : Muted,
                new Vector2(0.08f, 0.29f),
                new Vector2(0.92f, 0.42f),
                TextAnchor.MiddleCenter);
            CreateButton(
                modal.transform,
                "CANCELAR",
                new Vector2(0.08f, 0.08f),
                new Vector2(0.46f, 0.22f),
                Muted,
                CloseCraftConfirmation);
            CreateButton(
                modal.transform,
                "CONFIRMAR",
                new Vector2(0.54f, 0.08f),
                new Vector2(0.92f, 0.22f),
                accent,
                () => ExecuteConfirmedCraft(
                    generate,
                    entry,
                    cardId,
                    affected.Count > 0,
                    feedback));
            veil.transform.SetAsLastSibling();
        }

        private void ExecuteConfirmedCraft(
            bool generate,
            CardCatalogEntry entry,
            string cardId,
            bool deckImpactConfirmed,
            Text feedback)
        {
            string transactionId = $"craft:{Guid.NewGuid():N}";
            bool success = generate
                ? _repository.TryGenerateCard(
                    cardId,
                    1,
                    transactionId,
                    out CraftOperationResult _,
                    out string rejection)
                : _repository.TryDismantleCard(
                    cardId,
                    1,
                    CardFinish.Normal,
                    transactionId,
                    deckImpactConfirmed,
                    out CraftOperationResult _,
                    out rejection);
            if (!success)
            {
                feedback.text = rejection;
                feedback.color = Danger;
                return;
            }

            CloseCraftConfirmation();
            ShowDeckEditor(_editingDeck);
            ShowDeckEditorCardDetails(cardId);
            SetEditorStatus(
                generate
                    ? $"{entry.DisplayName} foi gerada por 30 CP {entry.Rarity}."
                    : $"{entry.DisplayName} foi desmantelada por 10 CP {entry.Rarity}.",
                Lime);
        }

        private void CloseCraftConfirmation()
        {
            if (_craftConfirmationModal != null)
                Destroy(_craftConfirmationModal);
            _craftConfirmationModal = null;
        }

        private static void SetCraftButtonInteractable(
            Button button,
            bool interactable)
        {
            if (button == null)
                return;
            button.interactable = interactable;
            if (button.targetGraphic != null)
            {
                Color color = button.targetGraphic.color;
                color.a = interactable ? 1f : 0.42f;
                button.targetGraphic.color = color;
            }
        }

        private static void AddCardRarityBadge(
            Transform card,
            CardCatalogEntry entry)
        {
            if (card == null || entry == null ||
                !CardRarityCatalog.IsValid(entry.Rarity))
            {
                return;
            }
            CreateRarityBadge(
                card,
                entry.Rarity,
                new Vector2(0.66f, 0.78f),
                new Vector2(0.98f, 0.985f),
                11);
        }

        private static Image CreateRarityBadge(
            Transform parent,
            CardRarity rarity,
            Vector2 min,
            Vector2 max,
            int fontSize)
        {
            Image badge = CreatePanel(
                parent,
                $"Raridade {rarity}",
                min,
                max,
                RarityColor(rarity));
            badge.raycastTarget = false;
            if (rarity == CardRarity.UR)
            {
                Image blueHalf = CreatePanel(
                    badge.transform,
                    "Gradiente azul UR",
                    new Vector2(0.52f, 0f),
                    Vector2.one,
                    new Color(0.12f, 0.55f, 1f, 0.85f));
                blueHalf.raycastTarget = false;
            }
            AddOutline(
                badge.gameObject,
                new Color(1f, 1f, 1f, 0.82f),
                new Vector2(1f, -1f));
            Text label = CreateText(
                badge.transform,
                CardRarityCatalog.Label(rarity),
                fontSize,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            label.transform.SetAsLastSibling();
            return badge;
        }

        private static Color RarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.N => Hex("#84919C"),
                CardRarity.R => Hex("#2588E4"),
                CardRarity.SR => Hex("#D5A900"),
                CardRarity.UR => Hex("#8E35EA"),
                _ => Muted
            };
        }
    }
}
