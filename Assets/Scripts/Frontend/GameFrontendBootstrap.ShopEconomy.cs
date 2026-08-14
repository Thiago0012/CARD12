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
            StructureDecks,
            ProfileIcons
        }

        [Header("Loja e economia")]
        [SerializeField] private Sprite shopBackgroundSprite;
        [SerializeField] private Sprite shopCoinSprite;
        [SerializeField] private Sprite shopClosedPackSprite;
        [SerializeField]
        private AuthorizedCoinRecipientsCatalog authorizedCoinRecipientsCatalog;

        [Header("Loja - cartas dos pacotes editáveis no Inspector")]
        [Tooltip("Início horizontal do nome do pacote. Aumente para afastar o texto da arte.")]
        [InspectorName("Título - início horizontal (X)")]
        [SerializeField, Range(0f, 0.8f)]
        private float shopPackTitleAnchorMinX = 0.34f;
        [Tooltip("Largura e altura de cada carta do leque.")]
        [InspectorName("Cartas do leque - tamanho")]
        [SerializeField]
        private Vector2 shopPackCardSize = new(0.105f, 0.30f);
        [Tooltip("Posição inicial da carta esquerda.")]
        [InspectorName("Carta esquerda - posição")]
        [SerializeField]
        private Vector2 shopPackLeftCardAnchorMin = new(0.045f, 0.34f);
        [Tooltip("Posição inicial da carta central.")]
        [InspectorName("Carta central - posição")]
        [SerializeField]
        private Vector2 shopPackCenterCardAnchorMin = new(0.115f, 0.36f);
        [Tooltip("Posição inicial da carta direita.")]
        [InspectorName("Carta direita - posição")]
        [SerializeField]
        private Vector2 shopPackRightCardAnchorMin = new(0.185f, 0.34f);
        [Tooltip("Rotação Z das cartas esquerda, central e direita, nessa ordem.")]
        [InspectorName("Cartas do leque - rotações")]
        [SerializeField]
        private Vector3 shopPackCardRotations = new(11f, 0f, -11f);

        private ShopTab _shopTab = ShopTab.Packages;
        private Action _shopBackAction;
        private string _activePackOpeningId = string.Empty;
        private bool _packOpeningStarted;
        private bool _packRevealBusy;
        private bool _shopPurchaseBusy;
        private Sprite _shopMysteryCardSprite;
        private Sprite _runtimeShopBackgroundSprite;
        private Sprite _runtimeShopBoosterPackSprite;
        private Sprite _runtimeShopCurrencySprite;
        private ShopSceneView _shopSceneView;

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
            int rewardDamageDealt,
            long statisticsDamageDealt,
            long statisticsDamageReceived,
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
            bool applied = _repository.TryClaimOnlineDuelReward(
                new MatchRewardRequest
                {
                    matchId = matchId,
                    localPlayerId = localPlayerId,
                    localProfileId = _repository.State.localProfileId,
                    mode = MatchRewardMode.OnlinePvP,
                    isAuthoritativeFinal = true,
                    isWinner = winner,
                    isDraw = draw,
                    totalOpponentDamage = rewardDamageDealt,
                    completedRounds = completedRounds,
                    eligibilityAtMatchStart = eligibilityAtMatchStart
                },
                out receipt,
                out rejection);
            if (!applied)
                return false;

            bool ranked = Multiplayer.DuelOnlineSession.Instance?.
                CompetitivePolicy ==
                ArcaneDuel.Game.Competitive.CompetitivePolicy.Ranked;
            _repository.TryRecordAuthoritativeDuelResult(
                "result:online:" + matchId + ":" + localPlayerId,
                winner,
                draw,
                true,
                ranked,
                statisticsDamageDealt,
                statisticsDamageReceived,
                out string statisticRejection);
            if (!string.IsNullOrWhiteSpace(statisticRejection))
                Debug.LogWarning("[Profile statistics] " + statisticRejection);
            return true;
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
            _shopPurchaseBusy = false;

            SetDuelPresentation(false);
            if (TryShowAuthoredEconomyShop())
                return;

            ClearScreen();
            _shopBackAction = () => LeaveShop(ShowMainMenu);
            BuildShopBackground("LOJA");
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
            CreateShopTabButton(
                "ÍCONES",
                ShopTab.ProfileIcons,
                new Vector2(0.56f, 0.855f),
                new Vector2(0.72f, 0.905f));

            bool iconCatalog = _shopTab == ShopTab.ProfileIcons;
            RectTransform content = CreateShopScrollGrid(
                _screenRoot,
                "Vitrine da Loja",
                new Vector2(0.055f, 0.055f),
                new Vector2(0.945f, 0.79f),
                iconCatalog ? new Vector2(385f, 210f) : new Vector2(510f, 210f),
                iconCatalog ? new Vector2(18f, 20f) : new Vector2(24f, 20f),
                iconCatalog ? 4 : 3);

            PopulateShopCatalog(content);
        }

        private bool TryShowAuthoredEconomyShop()
        {
            if (_shopSceneView == null || !_shopSceneView.IsConfigured)
                return false;

            ClearScreen();
            _shopBackAction = () => LeaveShop(ShowMainMenu);
            _shopSceneView.Bind(
                () =>
                {
                    FrontendClickAudio.Play();
                    LeaveShop(ShowMainMenu);
                },
                () =>
                {
                    FrontendClickAudio.Play();
                    SelectShopTab(ShopTab.Packages);
                },
                () =>
                {
                    FrontendClickAudio.Play();
                    SelectShopTab(ShopTab.StructureDecks);
                },
                () =>
                {
                    FrontendClickAudio.Play();
                    SelectShopTab(ShopTab.ProfileIcons);
                });
            _shopSceneView.SetBalance(_repository?.CoinBalance ?? 0);
            _shopSceneView.SetFeedback(
                string.IsNullOrWhiteSpace(_shopFeedback)
                    ? "Moedas são obtidas exclusivamente em duelos online PvP concluídos."
                    : _shopFeedback,
                _shopFeedbackIsError ? Danger : Muted);
            _shopSceneView.SetSelectedTab(
                (int)_shopTab,
                Lime,
                Cyan);
            _shopSceneView.ConfigureCatalogLayout(
                _shopTab == ShopTab.ProfileIcons);
            _shopSceneView.SetVisible(true);
            PopulateShopCatalog(_shopSceneView.CatalogContent);
            return true;
        }

        private void SelectShopTab(ShopTab tab)
        {
            _shopTab = tab;
            ShowEconomyShop();
        }

        private void PopulateShopCatalog(Transform content)
        {
            if (content == null)
                return;

            if (_shopTab == ShopTab.Packages)
            {
                foreach (ShopPackDefinition pack in ShopPackCatalog.Packs)
                    CreatePackProductTile(content, pack);
                return;
            }

            if (_shopTab == ShopTab.StructureDecks)
            {
                for (int index = 0; index < DeckShopCatalog.Products.Count;
                     index++)
                {
                    CreateStructureDeckProductTile(
                        content,
                        DeckShopCatalog.Products[index],
                        index);
                }
                return;
            }

            foreach (ProfileIconDefinition icon in
                     ProfileIconCatalog.Purchasable)
            {
                CreateProfileIconShopTile(content, icon);
            }
        }

        private void CreateCoinBalance(Transform parent)
        {
            Sprite currencySprite = ResolveShopCurrencySprite();
            Image panel = CreatePanel(
                parent,
                "Saldo de Moedas",
                new Vector2(0.76f, 0.895f),
                new Vector2(0.955f, 0.975f),
                new Color(0.015f, 0.045f, 0.075f, 0.98f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = new Vector2(143.7935f, 0f);
            panelRect.offsetMax = new Vector2(14.20645f, 0f);
            AddOutline(panel.gameObject, new Color(Gold.r, Gold.g, Gold.b, 0.9f),
                new Vector2(2f, -2f));
            CreateShopCurrencyIcon(panel.transform, "Ícone de Moeda",
                new Vector2(0.065f, 0.14f), new Vector2(0.26f, 0.86f),
                currencySprite);
            CreateText(
                panel.transform,
                (_repository?.CoinBalance ?? 0).ToString("N0"),
                28,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.275f, 0.08f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleLeft);
        }

        private Image CreateShopCurrencyIcon(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Sprite currencySprite = null)
        {
            Sprite sprite = currencySprite != null
                ? currencySprite
                : ResolveShopCurrencySprite();
            Image icon = CreatePanel(parent, name, min, max,
                sprite != null ? Color.white : Gold);
            icon.raycastTarget = false;
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.preserveAspect = true;
            }
            else
            {
                CreateText(icon.transform, "A", 18, FontStyle.Bold, Ink,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }
            return icon;
        }

        private Image CreateShopPriceButton(
            Transform parent,
            string label,
            int price,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action action)
        {
            Image button = CreateButton(parent, $"{label} {price}", min, max,
                accent, action);
            Text labelText = button.GetComponentInChildren<Text>();
            if (labelText != null)
            {
                labelText.text = label;
                labelText.gameObject.name = "Ação da Compra";
                RectTransform labelRect = labelText.rectTransform;
                labelRect.anchorMin = new Vector2(0.045f, 0.05f);
                labelRect.anchorMax = new Vector2(0.57f, 0.95f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                labelText.alignment = TextAnchor.MiddleRight;
            }

            CreateShopCurrencyIcon(button.transform, "Gema do Preço",
                new Vector2(0.60f, 0.19f), new Vector2(0.72f, 0.81f));
            CreateText(button.transform, price.ToString(), 22, FontStyle.Bold,
                Color.white, new Vector2(0.74f, 0.05f),
                new Vector2(0.95f, 0.95f), TextAnchor.MiddleLeft);
            return button;
        }

        private void CreateShopAmountBadge(
            Transform parent,
            string label,
            int amount,
            Vector2 min,
            Vector2 max,
            Color amountColor)
        {
            Image badge = CreatePanel(parent, label, min, max,
                new Color(0.015f, 0.045f, 0.075f, 0.96f));
            AddOutline(badge.gameObject,
                new Color(Gold.r, Gold.g, Gold.b, 0.48f),
                new Vector2(1f, -1f));
            CreateText(badge.transform, label, 15, FontStyle.Bold, Muted,
                new Vector2(0.055f, 0.08f), new Vector2(0.43f, 0.92f),
                TextAnchor.MiddleLeft);
            CreateText(badge.transform, amount.ToString(), 23, FontStyle.Bold,
                amountColor, new Vector2(0.43f, 0.08f),
                new Vector2(0.73f, 0.92f), TextAnchor.MiddleRight);
            CreateShopCurrencyIcon(badge.transform, "Gema de " + label,
                new Vector2(0.75f, 0.17f), new Vector2(0.93f, 0.83f));
        }

        private void CreateShopTabButton(
            string label,
            ShopTab tab,
            Vector2 min,
            Vector2 max)
        {
            bool selected = _shopTab == tab;
            Image button = CreateButton(
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
            RectTransform rect = button.rectTransform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-12f, -63.347f);
            rect.offsetMax = new Vector2(-12f, -63.347f);
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
                Color.white, new Vector2(shopPackTitleAnchorMinX, 0.66f),
                new Vector2(0.95f, 0.86f), TextAnchor.MiddleLeft);

            CreatePackCardFanPreview(tile.transform, pack);
            CreateText(tile.transform,
                $"5 cartas • duplicatas permitidas\n{pack.CardIds.Count} cartas possíveis",
                13, FontStyle.Bold, Muted,
                new Vector2(0.34f, 0.32f), new Vector2(0.94f, 0.62f),
                TextAnchor.MiddleLeft);
            AddButtonBehaviour(tile, () => ShowPackDetails(pack));
            CreateShopPriceButton(tile.transform, "COMPRAR", pack.PriceCoins,
                new Vector2(0.34f, 0.07f), new Vector2(0.94f, 0.28f),
                Gold, () => ShowPackPurchaseConfirmation(pack));
        }

        private void CreatePackCardFanPreview(
            Transform parent,
            ShopPackDefinition pack)
        {
            Vector2[] cardMins =
            {
                shopPackLeftCardAnchorMin,
                shopPackCenterCardAnchorMin,
                shopPackRightCardAnchorMin
            };
            float[] rotations =
            {
                shopPackCardRotations.x,
                shopPackCardRotations.y,
                shopPackCardRotations.z
            };
            int previewCount = Mathf.Min(3, pack.PreviewCardIds.Count);
            for (int previewIndex = 0;
                 previewIndex < previewCount;
                 previewIndex++)
            {
                string cardId = pack.PreviewCardIds[previewIndex];
                CardCatalogEntry preview = DeckRepository.ResolveCard(
                    _catalog, cardId);
                Vector2 min = cardMins[previewIndex];
                Image card = CreateCardArtwork(parent, preview?.Artwork, min,
                    min + shopPackCardSize,
                    rotations[previewIndex], true);
                card.gameObject.name =
                    $"Carta Destaque {previewIndex + 1} de {pack.PackId}";
                card.raycastTarget = false;
            }
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
            if (purchased >= product.MaxPurchases)
            {
                CreateButton(tile.transform, "LIMITE ATINGIDO",
                    new Vector2(0.55f, 0.07f), new Vector2(0.94f, 0.31f),
                    Danger, () => ShowStructureDeckPurchaseConfirmation(product));
            }
            else
            {
                CreateShopPriceButton(tile.transform, "COMPRAR",
                    product.PriceCoins, new Vector2(0.55f, 0.07f),
                    new Vector2(0.94f, 0.31f), Gold,
                    () => ShowStructureDeckPurchaseConfirmation(product));
            }
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
            BuildShopBackground("CONTEÚDO DO PACOTE");
            BuildHeader(pack.DisplayName, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);
            CreateText(_screenRoot,
                $"{pack.Description}  •  Cada abertura contém 5 sorteios independentes.",
                16, FontStyle.Bold, Muted, new Vector2(0.08f, 0.81f),
                new Vector2(0.72f, 0.87f), TextAnchor.MiddleLeft);
            CreateShopPriceButton(_screenRoot, "COMPRAR POR", pack.PriceCoins,
                new Vector2(0.76f, 0.81f), new Vector2(0.95f, 0.87f),
                Gold, () => ShowPackPurchaseConfirmation(pack));

            RectTransform content = CreateShopScrollGrid(_screenRoot,
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
            BuildShopBackground("DECK ESTRUTURAL");
            BuildHeader(product.DisplayName, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);

            CreateText(_screenRoot,
                $"{product.Description}  •  {product.MainDeckCardIds.Count} Principal  •  " +
                $"{product.ExtraDeckCardIds.Count} Adicional",
                15, FontStyle.Bold, Muted, new Vector2(0.08f, 0.81f),
                new Vector2(0.70f, 0.87f), TextAnchor.MiddleLeft);
            CreateShopPriceButton(_screenRoot, "COMPRAR POR",
                product.PriceCoins,
                new Vector2(0.75f, 0.81f), new Vector2(0.95f, 0.87f),
                Gold, () => ShowStructureDeckPurchaseConfirmation(product));

            RectTransform content = CreateShopScrollGrid(_screenRoot,
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
            BuildShopBackground("DETALHES DA CARTA");
            BuildHeader(entry.DisplayName, safeReturn);
            CreateCoinBalance(_screenRoot);

            Image panel = CreatePanel(_screenRoot, "Detalhes da Carta",
                new Vector2(0.07f, 0.09f), new Vector2(0.93f, 0.82f),
                new Color(0.008f, 0.025f, 0.05f, 0.98f));
            AddOutline(panel.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f),
                new Vector2(3f, -3f));
            Image detailArtwork = CreateCardArtwork(panel.transform, entry.Artwork,
                new Vector2(0.04f, 0.08f), new Vector2(0.34f, 0.92f), 0f, true);
            AddBanlistBadge(detailArtwork.transform, cardId);

            Text title = CreateText(panel.transform, entry.DisplayName, 30,
                FontStyle.Bold, Color.white, new Vector2(0.39f, 0.78f),
                new Vector2(0.96f, 0.92f), TextAnchor.MiddleLeft);
            title.gameObject.name = "Título da Carta da Loja";
            title.resizeTextMinSize = 22;

            Image metadataPanel = CreatePanel(panel.transform,
                "Metadados da Carta da Loja", new Vector2(0.39f, 0.65f),
                new Vector2(0.96f, 0.77f),
                new Color(0.025f, 0.11f, 0.16f, 0.96f));
            AddOutline(metadataPanel.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f),
                new Vector2(1f, -1f));
            Text metadata = CreateText(metadataPanel.transform,
                $"{entry.TypeName}  •  ID {entry.OfficialCardId}\n" +
                $"NA COLEÇÃO  •  {_repository.OwnedCardQuantity(cardId)}",
                17, FontStyle.Bold, Cyan, new Vector2(0.035f, 0.08f),
                new Vector2(0.965f, 0.92f), TextAnchor.MiddleLeft);
            metadata.gameObject.name = "Texto dos Metadados da Carta da Loja";
            metadata.resizeTextMinSize = 15;

            Image effectHeader = CreatePanel(panel.transform,
                "Cabeçalho do Efeito da Carta", new Vector2(0.39f, 0.58f),
                new Vector2(0.96f, 0.64f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.92f));
            CreateText(effectHeader.transform, "EFEITO DA CARTA", 16,
                FontStyle.Bold, Ink, new Vector2(0.035f, 0f),
                new Vector2(0.965f, 1f), TextAnchor.MiddleLeft);

            Text effectText = CreateScrollableText(panel.transform,
                "Painel do Efeito da Carta", new Vector2(0.39f, 0.08f),
                new Vector2(0.96f, 0.57f));
            effectText.gameObject.name = "Texto do Efeito da Carta";
            effectText.text = string.IsNullOrWhiteSpace(entry.EffectText)
                ? "Esta carta não possui texto de efeito."
                : entry.EffectText;
            effectText.fontSize = 19;
            effectText.color = new Color(0.92f, 0.96f, 1f, 1f);
            effectText.lineSpacing = 1.14f;
            ScrollRect effectScroll = effectText.GetComponentInParent<ScrollRect>();
            if (effectScroll != null)
            {
                AddOutline(effectScroll.gameObject,
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0.32f),
                    new Vector2(1f, -1f));
                effectScroll.scrollSensitivity = 56f;
                effectScroll.verticalNormalizedPosition = 1f;
            }
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
                    if (_shopPurchaseBusy)
                        return;
                    _shopPurchaseBusy = true;
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
                        _shopPurchaseBusy = false;
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
            BuildShopBackground(section);
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
            Color amountColor = _repository.CoinBalance >= price
                ? Lime
                : Danger;
            CreateShopAmountBadge(panel.transform, "PREÇO", price,
                new Vector2(0.10f, 0.28f), new Vector2(0.48f, 0.43f),
                amountColor);
            CreateShopAmountBadge(panel.transform, "SALDO",
                _repository.CoinBalance, new Vector2(0.52f, 0.28f),
                new Vector2(0.90f, 0.43f), amountColor);
            CreateButton(panel.transform, "CANCELAR",
                new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.25f),
                Danger, ShowEconomyShop);
            CreateButton(panel.transform, "CONFIRMAR COMPRA",
                new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.25f),
                Gold, confirm);
        }

        private void ShowPackOpening(
            PendingPackOpeningRecord opening,
            bool playEntrySequence = false)
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
            BuildShopBackground("ABERTURA DE PACOTE");
            BuildHeader(pack?.DisplayName ?? "Pacote",
                () => LeaveShop(ShowMainMenu));
            CreateCoinBalance(_screenRoot);

            if (!_packOpeningStarted)
            {
                Image closedPack = CreatePanel(_screenRoot, "Pacote Fechado",
                    new Vector2(0.37f, 0.25f), new Vector2(0.63f, 0.76f),
                    new Color(0.025f, 0.10f, 0.18f, 1f));
                Sprite boosterSprite = ResolveShopBoosterPackSprite();
                if (boosterSprite != null)
                {
                    closedPack.sprite = boosterSprite;
                    closedPack.color = Color.white;
                    closedPack.preserveAspect = true;
                }
                AddOutline(closedPack.gameObject,
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f),
                    new Vector2(5f, -5f));
                if (boosterSprite == null)
                {
                    CreateText(closedPack.transform, "ARCANE\nPACK", 45,
                        FontStyle.Bold, Color.white, new Vector2(0.08f, 0.25f),
                        new Vector2(0.92f, 0.78f), TextAnchor.MiddleCenter);
                }
                CreateButton(_screenRoot, "ABRIR PACOTE",
                    new Vector2(0.38f, 0.13f), new Vector2(0.62f, 0.22f),
                    Lime, () => StartPackOpeningPresentation(opening));
                return;
            }

            bool animateEntry = playEntrySequence &&
                packOpeningAnimationEnabled &&
                opening.revealed != null &&
                opening.revealed.All(value => !value);
            PackOpeningAnimationView animationView = animateEntry
                ? CreatePackOpeningAnimationView(pack)
                : null;
            Text revealInstruction = CreateText(_screenRoot,
                "Toque ou clique em cada carta para revelar. O resultado já está salvo.",
                17, FontStyle.Bold, Muted, new Vector2(0.16f, 0.79f),
                new Vector2(0.84f, 0.86f), TextAnchor.MiddleCenter);
            revealInstruction.gameObject.SetActive(!animateEntry);
            if (animationView != null)
                animationView.RevealInstruction = revealInstruction;

            bool allRevealed = true;
            for (int index = 0; index < opening.cardIds.Count; index++)
            {
                int capturedIndex = index;
                bool revealed = opening.revealed[index];
                allRevealed &= revealed;
                CardCatalogEntry entry = DeckRepository.ResolveCard(
                    _catalog, opening.cardIds[index]);
                float left = 0.075f + index * 0.185f;
                Sprite artwork = revealed
                    ? entry?.Artwork
                    : ResolveShopMysteryCardSprite();
                Transform cardParent = animationView != null
                    ? animationView.Layer
                    : _screenRoot;
                Vector2 finalMin = new Vector2(left, 0.27f);
                Vector2 finalMax = new Vector2(left + 0.15f, 0.72f);
                Image card = CreateCardArtwork(cardParent,
                    artwork,
                    finalMin,
                    finalMax, 0f, true);
                card.gameObject.name = revealed
                    ? $"Carta Revelada {index + 1}"
                    : $"Carta Oculta {index + 1}";
                if (!revealed)
                {
                    bool hasMysteryArtwork = artwork != null;
                    card.color = hasMysteryArtwork
                        ? Color.white
                        : new Color(0.025f, 0.10f, 0.18f, 1f);
                    card.preserveAspect = true;
                    if (!hasMysteryArtwork)
                    {
                        CreateText(card.transform, "ARCANE\n?", 25,
                            FontStyle.Bold, Cyan, Vector2.zero, Vector2.one,
                            TextAnchor.MiddleCenter);
                    }
                    AddButtonBehaviour(card, () =>
                    {
                        if (!_packRevealBusy && !_packOpeningSequenceActive)
                            StartCoroutine(RevealPackCard(opening, capturedIndex, card));
                    });
                    if (animationView != null)
                    {
                        Button cardButton = card.GetComponent<Button>();
                        if (cardButton != null)
                            cardButton.interactable = false;
                        RegisterPackOpeningCard(
                            animationView,
                            card,
                            cardButton,
                            finalMin,
                            finalMax,
                            index);
                    }
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

            if (animationView != null)
                BeginPackOpeningPresentation(animationView);
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

        private Sprite ResolveShopMysteryCardSprite()
        {
            if (_shopMysteryCardSprite != null)
                return _shopMysteryCardSprite;

            Texture2D texture = Resources.Load<Texture2D>("CardArtFallback");
            if (texture == null || texture.width <= 0 || texture.height <= 0)
                return null;

            // The source is square, but the luminous card occupies this
            // centered portrait region. Cropping at runtime keeps the artwork
            // proportional in the five tall reveal slots without stretching.
            var crop = new Rect(
                texture.width * 0.18f,
                texture.height * 0.055f,
                texture.width * 0.64f,
                texture.height * 0.89f);
            _shopMysteryCardSprite = Sprite.Create(
                texture,
                crop,
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _shopMysteryCardSprite.name = "Arcane Pack Mystery Card";
            _shopMysteryCardSprite.hideFlags = HideFlags.DontSave;
            return _shopMysteryCardSprite;
        }

        private void BuildShopBackground(string section)
        {
            Image background = BuildSharedBackground(section);
            Sprite artwork = ResolveShopBackgroundSprite();
            if (artwork == null || background == null)
                return;

            Image image = CreatePanel(background.transform,
                "Arte de Fundo da Loja", Vector2.zero, Vector2.one,
                new Color(0.62f, 0.66f, 0.76f, 1f));
            image.sprite = artwork;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.transform.SetAsFirstSibling();
            var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = artwork.rect.width / artwork.rect.height;

            Image veil = CreatePanel(background.transform,
                "Contraste da Arte da Loja", Vector2.zero, Vector2.one,
                new Color(0.005f, 0.015f, 0.035f, 0.34f));
            veil.raycastTarget = false;
            veil.transform.SetSiblingIndex(1);
        }

        private Sprite ResolveShopBackgroundSprite()
        {
            return ResolveShopVisualSprite(shopBackgroundSprite,
                "Shop/ShopBackground", "Fundo da Loja Arcane",
                ref _runtimeShopBackgroundSprite);
        }

        private Sprite ResolveShopBoosterPackSprite()
        {
            return ResolveShopVisualSprite(shopClosedPackSprite,
                "Shop/BoosterPack", "Booster Arcane",
                ref _runtimeShopBoosterPackSprite);
        }

        private Sprite ResolveShopCurrencySprite()
        {
            if (_runtimeShopCurrencySprite != null)
                return _runtimeShopCurrencySprite;
            Sprite imported = Resources.Load<Sprite>("Shop/CurrencyCrystal");
            if (imported != null)
            {
                _runtimeShopCurrencySprite = imported;
                return imported;
            }
            return ResolveShopVisualSprite(shopCoinSprite,
                "Shop/CurrencyCrystal", "Moeda Arcane",
                ref _runtimeShopCurrencySprite);
        }

        private static RectTransform CreateShopScrollGrid(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Vector2 cellSize,
            Vector2 spacing,
            int columns)
        {
            RectTransform content = CreateScrollGrid(parent, name, min, max,
                cellSize, spacing, columns, out RectTransform viewport);
            Transform trackTransform = viewport.Find("Barra de Rolagem");
            if (trackTransform is not RectTransform track)
                return content;

            track.anchorMin = new Vector2(0.965f, 0.015f);
            track.anchorMax = new Vector2(0.992f, 0.985f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(29.06448f, 0f);
            track.offsetMax = new Vector2(-0.0005178425f, 0f);

            Image trackImage = track.GetComponent<Image>();
            if (trackImage != null)
                trackImage.color = new Color(0.04f, 0.09f, 0.13f, 0.96f);
            Image handle = track.Find("Área Deslizante/Alça")
                ?.GetComponent<Image>();
            if (handle != null)
                handle.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.92f);
            return content;
        }

        private static Sprite ResolveShopVisualSprite(
            Sprite configured,
            string resourcePath,
            string runtimeName,
            ref Sprite runtimeSprite)
        {
            if (configured != null)
                return configured;

            Sprite importedSprite = Resources.Load<Sprite>(resourcePath);
            if (importedSprite != null)
                return importedSprite;
            if (runtimeSprite != null)
                return runtimeSprite;

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null || texture.width <= 0 || texture.height <= 0)
                return null;
            runtimeSprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            runtimeSprite.name = runtimeName;
            runtimeSprite.hideFlags = HideFlags.DontSave;
            return runtimeSprite;
        }

        private void ReleaseShopMysteryCardSprite()
        {
            if (_shopMysteryCardSprite == null)
                return;
            Destroy(_shopMysteryCardSprite);
            _shopMysteryCardSprite = null;
        }

        private void ReleaseShopVisualSprites()
        {
            ReleaseRuntimeShopSprite(ref _runtimeShopBackgroundSprite);
            ReleaseRuntimeShopSprite(ref _runtimeShopBoosterPackSprite);
            ReleaseRuntimeShopSprite(ref _runtimeShopCurrencySprite);
        }

        private static void ReleaseRuntimeShopSprite(ref Sprite sprite)
        {
            if (sprite == null)
                return;
            Destroy(sprite);
            sprite = null;
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
