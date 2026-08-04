using System;
using System.Collections;
using System.Linq;
using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private enum ShopTab
        {
            Packages,
            StructureDecks
        }

        [Header("Loja e economia")]
        [SerializeField] private Sprite shopCoinSprite;
        [SerializeField] private Sprite shopClosedPackSprite;
        [SerializeField]
        private AuthorizedCoinRecipientsCatalog authorizedCoinRecipientsCatalog;

        private ShopTab _shopTab = ShopTab.Packages;
        private Action _shopBackAction;
        private string _activePackOpeningId = string.Empty;
        private bool _packOpeningStarted;
        private bool _packRevealBusy;

        public CoinRewardEligibilitySnapshot CaptureOnlineDuelRewardEligibility()
        {
            if (_repository == null)
            {
                return CoinRewardEligibilitySnapshot.Blocked(
                    string.Empty,
                    string.Empty,
                    authorizedCoinRecipientsCatalog?.CatalogVersion ?? 0,
                    RewardReceiptStatus.BlockedInvalidMatch);
            }
            return _repository.CaptureOnlineRewardEligibility();
        }

        public bool TryApplyOnlineDuelReward(
            string matchId,
            string localPlayerId,
            int damageDealt,
            int completedRounds,
            bool winner,
            bool draw,
            CoinRewardEligibilitySnapshot eligibilityAtMatchStart,
            out RewardReceipt receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            if (_repository == null)
            {
                rejection = "O perfil local não está disponível para salvar a recompensa.";
                return false;
            }
            return _repository.TryClaimOnlineDuelReward(
                new MatchRewardRequest
                {
                    matchId = matchId,
                    localPlayerId = localPlayerId,
                    localProfileId = _repository.State.localProfileId,
                    mode = MatchRewardMode.OnlinePvP,
                    isAuthoritativeFinal = true,
                    isWinner = winner,
                    isDraw = draw,
                    totalOpponentDamage = damageDealt,
                    completedRounds = completedRounds,
                    eligibilityAtMatchStart = eligibilityAtMatchStart
                },
                out receipt,
                out rejection);
        }

        private void InitializeCoinRewardAuthorization()
        {
            if (_repository == null)
                return;
            if (authorizedCoinRecipientsCatalog == null)
            {
                // Fallback para cenas de diagnóstico criadas dinamicamente.
                // As cenas de produção mantêm a referência serializada.
                authorizedCoinRecipientsCatalog =
                    Resources.Load<AuthorizedCoinRecipientsCatalog>(
                        "Shop/AuthorizedCoinRecipientsCatalog");
            }
            _repository.ConfigureCoinRewardAuthorization(
                authorizedCoinRecipientsCatalog);

        }

        private void ShowEconomyShop()
        {
            PendingPackOpeningRecord pending = _repository?.PendingPackOpening;
            if (pending != null)
            {
                ShowPackOpening(pending);
                return;
            }

            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = () => LeaveShop(ShowMainMenu);
            BuildSharedBackground("LOJA");
            BuildHeader("LOJA ARCANE", () => LeaveShop(ShowMainMenu));
            CreateCoinBalance(_screenRoot);

            CreateText(
                _screenRoot,
                string.IsNullOrWhiteSpace(_shopFeedback)
                    ? "Moedas são obtidas exclusivamente em duelos online PvP concluídos."
                    : _shopFeedback,
                15,
                FontStyle.Bold,
                _shopFeedbackIsError ? Danger : Muted,
                new Vector2(0.08f, 0.805f),
                new Vector2(0.72f, 0.852f),
                TextAnchor.MiddleLeft);

            CreateShopTabButton(
                "PACOTES",
                ShopTab.Packages,
                new Vector2(0.08f, 0.855f),
                new Vector2(0.29f, 0.905f));
            CreateShopTabButton(
                "DECKS ESTRUTURAIS",
                ShopTab.StructureDecks,
                new Vector2(0.30f, 0.855f),
                new Vector2(0.55f, 0.905f));

            RectTransform content = CreateScrollGrid(
                _screenRoot,
                "Vitrine da Loja",
                new Vector2(0.055f, 0.055f),
                new Vector2(0.945f, 0.79f),
                new Vector2(510f, 210f),
                new Vector2(24f, 20f),
                3);

            if (_shopTab == ShopTab.Packages)
            {
                foreach (ShopPackDefinition pack in ShopPackCatalog.Packs)
                    CreatePackProductTile(content, pack);
            }
            else
            {
                for (int index = 0; index < DeckShopCatalog.Products.Count; index++)
                {
                    CreateStructureDeckProductTile(
                        content,
                        DeckShopCatalog.Products[index],
                        index);
                }
            }
        }

        private void CreateCoinBalance(Transform parent)
        {
            Image panel = CreatePanel(
                parent,
                "Saldo de Moedas",
                new Vector2(0.76f, 0.895f),
                new Vector2(0.955f, 0.975f),
                new Color(0.015f, 0.045f, 0.075f, 0.98f));
            AddOutline(panel.gameObject, new Color(Gold.r, Gold.g, Gold.b, 0.9f),
                new Vector2(2f, -2f));
            Image icon = CreatePanel(
                panel.transform,
                "Ícone de Moeda",
                new Vector2(0.055f, 0.18f),
                new Vector2(0.22f, 0.82f),
                Gold);
            if (shopCoinSprite != null)
            {
                icon.sprite = shopCoinSprite;
                icon.preserveAspect = true;
                icon.color = Color.white;
            }
            else
            {
                CreateText(icon.transform, "A", 21, FontStyle.Bold, Ink,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }
            CreateText(
                panel.transform,
                (_repository?.CoinBalance ?? 0).ToString("N0"),
                28,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.26f, 0.08f),
                new Vector2(0.94f, 0.92f),
                TextAnchor.MiddleRight);
        }

        private void CreateShopTabButton(
            string label,
            ShopTab tab,
            Vector2 min,
            Vector2 max)
        {
            bool selected = _shopTab == tab;
            CreateButton(
                _screenRoot,
                label,
                min,
                max,
                selected ? Lime : Cyan,
                () =>
                {
                    _shopTab = tab;
                    ShowEconomyShop();
                });
        }

        private void CreatePackProductTile(
            Transform parent,
            ShopPackDefinition pack)
        {
            Image tile = CreateShopTile(parent, pack.PackId, Cyan);
            CreateText(tile.transform, "PACOTE", 12, FontStyle.Bold, Cyan,
                new Vector2(0.05f, 0.84f), new Vector2(0.42f, 0.96f),
                TextAnchor.MiddleLeft);
            CreateText(tile.transform, pack.DisplayName, 21, FontStyle.Bold,
                Color.white, new Vector2(0.05f, 0.66f),
                new Vector2(0.95f, 0.86f), TextAnchor.MiddleLeft);

            for (int previewIndex = 0;
                 previewIndex < pack.PreviewCardIds.Count;
                 previewIndex++)
            {
                string cardId = pack.PreviewCardIds[previewIndex];
                CardCatalogEntry preview = DeckRepository.ResolveCard(
                    _catalog, cardId);
                float left = 0.05f + previewIndex * 0.09f;
                CreateCardArtwork(tile.transform, preview?.Artwork,
                    new Vector2(left, 0.25f),
                    new Vector2(left + 0.08f, 0.65f), 0f, true);
            }
            CreateText(tile.transform,
                $"5 cartas • duplicatas permitidas\n{pack.CardIds.Count} cartas possíveis",
                13, FontStyle.Bold, Muted,
                new Vector2(0.34f, 0.32f), new Vector2(0.94f, 0.62f),
                TextAnchor.MiddleLeft);
            AddButtonBehaviour(tile, () => ShowPackDetails(pack));
            CreateButton(tile.transform,
                $"COMPRAR  •  {pack.PriceCoins}",
                new Vector2(0.34f, 0.07f), new Vector2(0.94f, 0.28f),
                Gold, () => ShowPackPurchaseConfirmation(pack));
        }

        private void CreateStructureDeckProductTile(
            Transform parent,
            DeckShopProduct product,
            int index)
        {
            Color[] accents = { Cyan, Gold, Danger };
            Color accent = accents[index % accents.Length];
            Image tile = CreateShopTile(parent, product.ProductId, accent);
            int purchased = _repository.StructureDeckPurchaseCount(product.ProductId);
            CreateText(tile.transform, product.ArchetypeLabel, 12,
                FontStyle.Bold, accent, new Vector2(0.05f, 0.84f),
                new Vector2(0.66f, 0.96f), TextAnchor.MiddleLeft);
            CreateText(tile.transform,
                $"{purchased}/{product.MaxPurchases}", 12, FontStyle.Bold,
                purchased >= product.MaxPurchases ? Danger : Muted,
                new Vector2(0.72f, 0.84f), new Vector2(0.94f, 0.96f),
                TextAnchor.MiddleRight);
            CreateText(tile.transform, product.DisplayName, 20, FontStyle.Bold,
                Color.white, new Vector2(0.05f, 0.67f),
                new Vector2(0.95f, 0.86f), TextAnchor.MiddleLeft);

            for (int previewIndex = 0; previewIndex < 3; previewIndex++)
            {
                string cardId = product.PreviewCardIds[previewIndex];
                CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
                float left = 0.06f + previewIndex * 0.15f;
                CreateCardArtwork(tile.transform, entry?.Artwork,
                    new Vector2(left, 0.25f), new Vector2(left + 0.13f, 0.65f),
                    0f, true);
            }
            AddButtonBehaviour(tile, () => ShowStructureDeckDetails(product));
            CreateButton(tile.transform,
                purchased >= product.MaxPurchases
                    ? "LIMITE ATINGIDO"
                    : $"COMPRAR  •  {product.PriceCoins}",
                new Vector2(0.55f, 0.07f), new Vector2(0.94f, 0.31f),
                purchased >= product.MaxPurchases ? Danger : Gold,
                () => ShowStructureDeckPurchaseConfirmation(product));
        }

        private static Image CreateShopTile(
            Transform parent,
            string name,
            Color accent)
        {
            Image tile = CreatePanel(parent, name, Vector2.zero, Vector2.one,
                new Color(0.008f, 0.025f, 0.05f, 0.99f));
            AddOutline(tile.gameObject,
                new Color(accent.r, accent.g, accent.b, 0.72f),
                new Vector2(2f, -2f));
            return tile;
        }

        private void ShowPackDetails(ShopPackDefinition pack)
        {
            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = ShowEconomyShop;
            BuildSharedBackground("CONTEÚDO DO PACOTE");
            BuildHeader(pack.DisplayName, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);
            CreateText(_screenRoot,
                $"{pack.Description}  •  Cada abertura contém 5 sorteios independentes.",
                16, FontStyle.Bold, Muted, new Vector2(0.08f, 0.81f),
                new Vector2(0.72f, 0.87f), TextAnchor.MiddleLeft);
            CreateButton(_screenRoot,
                $"COMPRAR POR {pack.PriceCoins}",
                new Vector2(0.76f, 0.81f), new Vector2(0.95f, 0.87f),
                Gold, () => ShowPackPurchaseConfirmation(pack));

            RectTransform content = CreateScrollGrid(_screenRoot,
                "Cartas do Pacote", new Vector2(0.055f, 0.055f),
                new Vector2(0.945f, 0.79f), new Vector2(205f, 295f),
                new Vector2(18f, 18f), 7);
            foreach (string cardId in pack.CardIds)
                CreateShopCardTile(content, cardId, "NO PACOTE");
        }

        private void ShowStructureDeckDetails(DeckShopProduct product)
        {
            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = ShowEconomyShop;
            BuildSharedBackground("DECK ESTRUTURAL");
            BuildHeader(product.DisplayName, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);

            CreateText(_screenRoot,
                $"{product.Description}  •  {product.MainDeckCardIds.Count} Principal  •  " +
                $"{product.ExtraDeckCardIds.Count} Adicional",
                15, FontStyle.Bold, Muted, new Vector2(0.08f, 0.81f),
                new Vector2(0.70f, 0.87f), TextAnchor.MiddleLeft);
            CreateButton(_screenRoot,
                $"COMPRAR POR {product.PriceCoins}",
                new Vector2(0.75f, 0.81f), new Vector2(0.95f, 0.87f),
                Gold, () => ShowStructureDeckPurchaseConfirmation(product));

            RectTransform content = CreateScrollGrid(_screenRoot,
                "Lista Completa", new Vector2(0.055f, 0.055f),
                new Vector2(0.945f, 0.79f), new Vector2(205f, 295f),
                new Vector2(18f, 18f), 7);
            foreach (string cardId in product.MainDeckCardIds)
                CreateShopCardTile(content, cardId, "PRINCIPAL");
            foreach (string cardId in product.ExtraDeckCardIds)
                CreateShopCardTile(content, cardId, "ADICIONAL");
        }

        private void CreateShopCardTile(
            Transform parent,
            string cardId,
            string section)
        {
            CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
            Image tile = CreateShopTile(parent, "Carta " + cardId, Cyan);
            Image artwork = CreateCardArtwork(tile.transform, entry?.Artwork,
                new Vector2(0.09f, 0.20f), new Vector2(0.91f, 0.94f), 0f, true);
            AddBanlistBadge(artwork.transform, cardId);
            CreateText(tile.transform, section, 11, FontStyle.Bold, Cyan,
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.18f),
                TextAnchor.MiddleCenter);
            AddButtonBehaviour(artwork,
                () => ShowShopCardDetails(cardId, _shopBackAction));
        }

        private void ShowShopCardDetails(string cardId, Action returnAction)
        {
            CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
            if (entry == null)
                return;
            Action safeReturn = returnAction ?? ShowEconomyShop;
            SetDuelPresentation(false);
            ClearScreen();
            _deckEditorSelectedCardId = cardId;
            _shopBackAction = safeReturn;
            BuildSharedBackground("DETALHES DA CARTA");
            BuildHeader(entry.DisplayName, safeReturn);
            CreateCoinBalance(_screenRoot);

            Image panel = CreatePanel(_screenRoot, "Detalhes da Carta",
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.82f),
                new Color(0.008f, 0.025f, 0.05f, 0.98f));
            AddOutline(panel.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f),
                new Vector2(3f, -3f));
            Image detailArtwork = CreateCardArtwork(panel.transform, entry.Artwork,
                new Vector2(0.07f, 0.10f), new Vector2(0.34f, 0.90f), 0f, true);
            AddBanlistBadge(detailArtwork.transform, cardId);
            CreateText(panel.transform, entry.DisplayName, 30, FontStyle.Bold,
                Color.white, new Vector2(0.39f, 0.75f),
                new Vector2(0.94f, 0.91f), TextAnchor.MiddleLeft);
            CreateText(panel.transform,
                $"{entry.TypeName}  •  ID {entry.OfficialCardId}\n" +
                $"Na coleção: {_repository.OwnedCardQuantity(cardId)}",
                17, FontStyle.Bold, Cyan, new Vector2(0.39f, 0.61f),
                new Vector2(0.94f, 0.75f), TextAnchor.UpperLeft);
            CreateText(panel.transform, entry.EffectText, 16, FontStyle.Normal,
                Muted, new Vector2(0.39f, 0.12f),
                new Vector2(0.94f, 0.59f), TextAnchor.UpperLeft);
        }

        private void ShowPackPurchaseConfirmation(ShopPackDefinition pack)
        {
            ShowPurchaseConfirmation(
                "CONFIRMAR PACOTE",
                pack.DisplayName,
                $"Você receberá exatamente 5 cartas. Cada posição é sorteada " +
                $"independentemente e pode conter duplicatas.",
                pack.PriceCoins,
                () =>
                {
                    string transactionId = Guid.NewGuid().ToString("N");
                    if (_repository.TryPurchasePack(pack.PackId, transactionId,
                            out PendingPackOpeningRecord opening,
                            out _, out string rejection))
                    {
                        _shopFeedback = $"{pack.DisplayName} comprado. Abra suas cinco cartas.";
                        _shopFeedbackIsError = false;
                        _activePackOpeningId = opening.transactionId;
                        _packOpeningStarted = false;
                        ShowPackOpening(opening);
                    }
                    else
                    {
                        SetShopFailure(rejection);
                    }
                });
        }

        private void ShowStructureDeckPurchaseConfirmation(
            DeckShopProduct product)
        {
            int purchased = _repository.StructureDeckPurchaseCount(product.ProductId);
            ShowPurchaseConfirmation(
                "CONFIRMAR DECK ESTRUTURAL",
                product.DisplayName,
                $"A compra concede as quantidades exatas do Deck Principal e " +
                $"Adicional. Limite: {purchased}/{product.MaxPurchases}.",
                product.PriceCoins,
                () =>
                {
                    string transactionId = Guid.NewGuid().ToString("N");
                    if (_repository.TryPurchaseStructureDeck(
                            product.ProductId, transactionId,
                            out _, out string rejection))
                    {
                        _shopFeedback = $"{product.DisplayName} foi adicionado à coleção.";
                        _shopFeedbackIsError = false;
                        ShowEconomyShop();
                    }
                    else
                    {
                        SetShopFailure(rejection);
                    }
                });
        }

        private void ShowPurchaseConfirmation(
            string section,
            string productName,
            string description,
            int price,
            Action confirm)
        {
            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = ShowEconomyShop;
            BuildSharedBackground(section);
            BuildHeader(section, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);
            Image panel = CreatePanel(_screenRoot, "Confirmação de Compra",
                new Vector2(0.23f, 0.22f), new Vector2(0.77f, 0.78f),
                new Color(0.008f, 0.025f, 0.05f, 0.99f));
            AddOutline(panel.gameObject,
                new Color(Gold.r, Gold.g, Gold.b, 0.84f),
                new Vector2(3f, -3f));
            CreateText(panel.transform, productName, 34, FontStyle.Bold,
                Color.white, new Vector2(0.08f, 0.69f),
                new Vector2(0.92f, 0.90f), TextAnchor.MiddleCenter);
            CreateText(panel.transform, description, 18, FontStyle.Normal,
                Muted, new Vector2(0.10f, 0.42f),
                new Vector2(0.90f, 0.68f), TextAnchor.MiddleCenter);
            CreateText(panel.transform,
                $"PREÇO  {price}   •   SALDO  {_repository.CoinBalance}",
                23, FontStyle.Bold,
                _repository.CoinBalance >= price ? Lime : Danger,
                new Vector2(0.10f, 0.28f), new Vector2(0.90f, 0.43f),
                TextAnchor.MiddleCenter);
            CreateButton(panel.transform, "CANCELAR",
                new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.25f),
                Danger, ShowEconomyShop);
            CreateButton(panel.transform, "CONFIRMAR COMPRA",
                new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.25f),
                Gold, confirm);
        }

        private void ShowPackOpening(PendingPackOpeningRecord opening)
        {
            if (opening == null)
            {
                ShowEconomyShop();
                return;
            }
            if (!string.Equals(_activePackOpeningId, opening.transactionId,
                    StringComparison.Ordinal))
            {
                _activePackOpeningId = opening.transactionId;
                _packOpeningStarted = opening.revealed != null &&
                    opening.revealed.Any(value => value);
            }

            ShopPackDefinition pack = ShopPackCatalog.Find(opening.packId);
            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = () => LeaveShop(ShowMainMenu);
            BuildSharedBackground("ABERTURA DE PACOTE");
            BuildHeader(pack?.DisplayName ?? "Pacote",
                () => LeaveShop(ShowMainMenu));
            CreateCoinBalance(_screenRoot);

            if (!_packOpeningStarted)
            {
                Image closedPack = CreatePanel(_screenRoot, "Pacote Fechado",
                    new Vector2(0.37f, 0.25f), new Vector2(0.63f, 0.76f),
                    new Color(0.025f, 0.10f, 0.18f, 1f));
                if (shopClosedPackSprite != null)
                {
                    closedPack.sprite = shopClosedPackSprite;
                    closedPack.color = Color.white;
                    closedPack.preserveAspect = true;
                }
                AddOutline(closedPack.gameObject,
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f),
                    new Vector2(5f, -5f));
                CreateText(closedPack.transform, "ARCANE\nPACK", 45,
                    FontStyle.Bold, Color.white, new Vector2(0.08f, 0.25f),
                    new Vector2(0.92f, 0.78f), TextAnchor.MiddleCenter);
                CreateButton(_screenRoot, "ABRIR PACOTE",
                    new Vector2(0.38f, 0.13f), new Vector2(0.62f, 0.22f),
                    Lime, () =>
                    {
                        _packOpeningStarted = true;
                        ShowPackOpening(opening);
                    });
                return;
            }

            CreateText(_screenRoot,
                "Toque ou clique em cada carta para revelar. O resultado já está salvo.",
                17, FontStyle.Bold, Muted, new Vector2(0.16f, 0.79f),
                new Vector2(0.84f, 0.86f), TextAnchor.MiddleCenter);
            bool allRevealed = true;
            for (int index = 0; index < opening.cardIds.Count; index++)
            {
                int capturedIndex = index;
                bool revealed = opening.revealed[index];
                allRevealed &= revealed;
                CardCatalogEntry entry = DeckRepository.ResolveCard(
                    _catalog, opening.cardIds[index]);
                float left = 0.075f + index * 0.185f;
                Image card = CreateCardArtwork(_screenRoot,
                    revealed ? entry?.Artwork : null,
                    new Vector2(left, 0.27f),
                    new Vector2(left + 0.15f, 0.72f), 0f, true);
                if (!revealed)
                {
                    card.color = new Color(0.025f, 0.10f, 0.18f, 1f);
                    CreateText(card.transform, "ARCANE\n?", 25,
                        FontStyle.Bold, Cyan, Vector2.zero, Vector2.one,
                        TextAnchor.MiddleCenter);
                    AddButtonBehaviour(card, () =>
                    {
                        if (!_packRevealBusy)
                            StartCoroutine(RevealPackCard(opening, capturedIndex, card));
                    });
                }
                else
                {
                    CreateText(_screenRoot, entry?.DisplayName ?? opening.cardIds[index],
                        13, FontStyle.Bold, Color.white,
                        new Vector2(left - 0.01f, 0.20f),
                        new Vector2(left + 0.16f, 0.27f),
                        TextAnchor.MiddleCenter);
                    AddButtonBehaviour(card,
                        () => ShowShopCardDetails(
                            opening.cardIds[capturedIndex],
                            () => ShowPackOpening(opening)));
                }
            }

            if (allRevealed)
            {
                CreateButton(_screenRoot, "CONCLUIR ABERTURA",
                    new Vector2(0.36f, 0.10f), new Vector2(0.64f, 0.18f),
                    Lime, () =>
                    {
                        if (_repository.TryCompletePackOpening(
                                opening.transactionId, out string rejection))
                        {
                            _activePackOpeningId = string.Empty;
                            _packOpeningStarted = false;
                            _shopFeedback = "Pacote concluído. As cartas já estão na coleção.";
                            _shopFeedbackIsError = false;
                            ShowEconomyShop();
                        }
                        else
                        {
                            SetShopFailure(rejection);
                        }
                    });
            }
        }

        private IEnumerator RevealPackCard(
            PendingPackOpeningRecord opening,
            int index,
            Image card)
        {
            _packRevealBusy = true;
            const float halfDuration = 0.18f;
            float elapsed = 0f;
            while (elapsed < halfDuration && card != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float angle = Mathf.Lerp(0f, 90f,
                    Mathf.Clamp01(elapsed / halfDuration));
                card.rectTransform.localRotation = Quaternion.Euler(0f, angle, 0f);
                yield return null;
            }

            if (!_repository.TryRevealPackCard(
                    opening.transactionId, index, out string rejection))
            {
                _packRevealBusy = false;
                SetShopFailure(rejection);
                yield break;
            }

            CardCatalogEntry entry = DeckRepository.ResolveCard(
                _catalog, opening.cardIds[index]);
            if (card != null)
            {
                card.sprite = entry?.Artwork;
                card.color = Color.white;
                card.preserveAspect = true;
            }
            elapsed = 0f;
            while (elapsed < halfDuration && card != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float angle = Mathf.Lerp(90f, 0f,
                    Mathf.Clamp01(elapsed / halfDuration));
                card.rectTransform.localRotation = Quaternion.Euler(0f, angle, 0f);
                yield return null;
            }
            _packRevealBusy = false;
            ShowPackOpening(opening);
        }

        private void SetShopFailure(string rejection)
        {
            _shopFeedback = string.IsNullOrWhiteSpace(rejection)
                ? "A operação da loja não pôde ser concluída."
                : rejection;
            _shopFeedbackIsError = true;
            ShowEconomyShop();
        }

        private void LeaveShop(Action destination)
        {
            _shopBackAction = null;
            destination?.Invoke();
        }
    }
}
