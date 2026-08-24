using System;
using System.Collections;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneDuel.Game.Accounts;
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
            if (!PlayerIdAccessRuntime.Allows(
                    PlayerIdCapability.Economy,
                    out rejection))
            {
                return false;
            }
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

#if UNITY_EDITOR
            GrantEditorPackAnimationTestCoins();
#endif

        }

#if UNITY_EDITOR
        private void GrantEditorPackAnimationTestCoins()
        {
            // Crédito único e idempotente para testar compras/animações dentro
            // do Editor. Este bloco não é compilado em nenhuma build PC ou
            // Android, portanto não cria uma fonte de moedas no jogo público.
            const int testCoins = 10000;
            const string transactionId =
                "editor-pack-opening-animation-wallet-v2";
            if (_repository.TryGrantCoins(
                    testCoins,
                    "Teste da animação de abertura de pacotes",
                    transactionId,
                    out _,
                    out string rejection))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(rejection))
            {
                Debug.LogWarning(
                    "[Loja/Editor] Não foi possível conceder as moedas de " +
                    "teste: " + rejection);
            }
        }
#endif

        private void ShowEconomyShop()
        {
            if (!PlayerIdAccessRuntime.Allows(
                    PlayerIdCapability.Economy,
                    out string accessRejection))
            {
                ShowPlayerIdCapabilityBlocked(accessRejection);
                return;
            }
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
            _shopBackAction = ReturnFromShopToMainMenu;
            BuildShopBackground("LOJA");
            BuildProfessionalShopHeader(
                "LOJA",
                ReturnFromShopToMainMenu);
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
            _shopBackAction = ReturnFromShopToMainMenu;
            _shopSceneView.Bind(
                () =>
                {
                    FrontendClickAudio.Play();
                    ReturnFromShopToMainMenu();
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
            _shopSceneView.ApplyProfessionalTheme();
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
            DecorateRuntimeShopSurface(panel, Gold, true, 8f);
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
            DecorateRuntimeShopButton(button, accent, true, 7f);
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
            DecorateRuntimeShopSurface(badge, Gold, false, 7f);
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
            DecorateRuntimeShopButton(
                button,
                selected ? Gold : new Color(0.78f, 0.48f, 0.12f, 1f),
                selected,
                8f);
            RectTransform rect = button.rectTransform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-12f, -63.347f);
            rect.offsetMax = new Vector2(-12f, -63.347f);
        }

        private void CreatePackProductTile(
            Transform parent,
            ShopPackDefinition pack)
        {
            Color accent = ResolveShopProductAccent(pack.PackId, false);
            Image tile = CreateShopTile(parent, pack.PackId, accent);
            CreateText(tile.transform, "PACOTE PREMIUM", 11, FontStyle.Bold,
                accent,
                new Vector2(0.05f, 0.84f), new Vector2(0.42f, 0.96f),
                TextAnchor.MiddleLeft);
            CreateShopMicroBadge(tile.transform, "5 CARTAS", accent,
                new Vector2(0.73f, 0.855f), new Vector2(0.94f, 0.955f));
            CreateText(tile.transform, pack.DisplayName, 21, FontStyle.Bold,
                Color.white, new Vector2(shopPackTitleAnchorMinX, 0.66f),
                new Vector2(0.95f, 0.86f), TextAnchor.MiddleLeft);

            Image previewStage = CreatePanel(tile.transform,
                "Palco do Pacote", new Vector2(0.035f, 0.17f),
                new Vector2(0.315f, 0.70f), Color.clear);
            DecorateRuntimeShopSurface(previewStage, accent, true, 8f);
            previewStage.raycastTarget = false;
            CreatePackCardFanPreview(tile.transform, pack);
            CreateText(tile.transform,
                $"SORTEIOS INDEPENDENTES\n{pack.CardIds.Count} CARTAS POSSÍVEIS",
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
            Color accent = ResolveShopProductAccent(
                product.ProductId + ":" + index, true);
            Image tile = CreateShopTile(parent, product.ProductId, accent);
            int purchased = _repository.StructureDeckPurchaseCount(product.ProductId);
            CreateText(tile.transform, "DECK ESTRUTURAL • " + product.ArchetypeLabel, 11,
                FontStyle.Bold, accent, new Vector2(0.05f, 0.84f),
                new Vector2(0.70f, 0.96f), TextAnchor.MiddleLeft);
            CreateShopMicroBadge(tile.transform,
                $"{purchased}/{product.MaxPurchases}",
                purchased >= product.MaxPurchases ? Danger : accent,
                new Vector2(0.77f, 0.855f), new Vector2(0.94f, 0.955f));
            CreateText(tile.transform, product.DisplayName, 20, FontStyle.Bold,
                Color.white, new Vector2(0.05f, 0.67f),
                new Vector2(0.95f, 0.86f), TextAnchor.MiddleLeft);

            Image deckCase = CreatePanel(tile.transform,
                "Estojo do Deck", new Vector2(0.04f, 0.16f),
                new Vector2(0.48f, 0.65f), Color.clear);
            DecorateRuntimeShopSurface(deckCase, accent, true, 9f);
            deckCase.raycastTarget = false;
            int previewCount = Mathf.Min(3, product.PreviewCardIds.Count);
            float[] previewRotations = { 8f, 0f, -8f };
            for (int previewIndex = 0; previewIndex < previewCount; previewIndex++)
            {
                string cardId = product.PreviewCardIds[previewIndex];
                CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
                float left = 0.065f + previewIndex * 0.125f;
                CreateCardArtwork(tile.transform, entry?.Artwork,
                    new Vector2(left, 0.235f), new Vector2(left + 0.145f, 0.635f),
                    previewRotations[previewIndex], true);
            }
            CreateText(tile.transform,
                $"{product.MainDeckCardIds.Count} PRINCIPAL\n" +
                $"{product.ExtraDeckCardIds.Count} ADICIONAL",
                13, FontStyle.Bold, Muted,
                new Vector2(0.51f, 0.35f), new Vector2(0.94f, 0.62f),
                TextAnchor.MiddleLeft);
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

        private static Color ResolveShopProductAccent(
            string stableId,
            bool structureDeck)
        {
            Color[] packAccents =
            {
                new(0.98f, 0.68f, 0.18f, 1f),
                new(0.42f, 0.80f, 0.92f, 1f),
                new(0.72f, 0.48f, 0.96f, 1f),
                new(0.96f, 0.42f, 0.34f, 1f)
            };
            Color[] deckAccents =
            {
                new(0.95f, 0.76f, 0.30f, 1f),
                new(0.40f, 0.84f, 0.64f, 1f),
                new(0.42f, 0.70f, 0.96f, 1f),
                new(0.88f, 0.52f, 0.24f, 1f)
            };
            Color[] accents = structureDeck ? deckAccents : packAccents;
            int hash = 17;
            string value = stableId ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
                hash = unchecked(hash * 31 + value[index]);
            return accents[Mathf.Abs(hash % accents.Length)];
        }

        private static Image CreateShopMicroBadge(
            Transform parent,
            string label,
            Color accent,
            Vector2 min,
            Vector2 max)
        {
            Image badge = CreatePanel(parent, "Selo " + label, min, max,
                Color.clear);
            DecorateRuntimeShopSurface(badge, accent, false, 5f);
            badge.raycastTarget = false;
            CreateText(badge.transform, label, 10, FontStyle.Bold,
                Color.white, new Vector2(0.06f, 0.04f),
                new Vector2(0.94f, 0.96f), TextAnchor.MiddleCenter);
            return badge;
        }

        private static Image CreateShopTile(
            Transform parent,
            string name,
            Color accent)
        {
            Image tile = CreatePanel(parent, name, Vector2.zero, Vector2.one,
                new Color(0.004f, 0.008f, 0.014f, 0.99f));
            DecorateRuntimeShopSurface(tile, accent, true, 11f);
            Image rail = CreatePanel(
                tile.transform,
                "Filete superior da vitrine",
                new Vector2(0.035f, 0.965f),
                new Vector2(0.965f, 0.985f),
                new Color(accent.r, accent.g, accent.b, 0.76f));
            rail.raycastTarget = false;
            return tile;
        }

        private static ArcaneShopSurfaceGraphic DecorateRuntimeShopSurface(
            Image target,
            Color accent,
            bool raised,
            float chamfer)
        {
            if (target == null)
                return null;
            target.color = new Color(0f, 0f, 0f, 0.015f);
            Transform existing = target.transform.Find("Superfície Dourada");
            ArcaneShopSurfaceGraphic surface;
            if (existing != null)
            {
                surface = existing.GetComponent<ArcaneShopSurfaceGraphic>();
            }
            else
            {
                var item = new GameObject(
                    "Superfície Dourada",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(ArcaneShopSurfaceGraphic));
                RectTransform rect = item.GetComponent<RectTransform>();
                rect.SetParent(target.transform, false);
                rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                surface = item.GetComponent<ArcaneShopSurfaceGraphic>();
            }
            if (surface != null)
            {
                surface.raycastTarget = false;
                surface.SetStyle(accent, raised, 1f, chamfer);
            }
            return surface;
        }

        private static void DecorateRuntimeShopButton(
            Image buttonImage,
            Color accent,
            bool raised,
            float chamfer)
        {
            ArcaneShopSurfaceGraphic surface = DecorateRuntimeShopSurface(
                buttonImage,
                accent,
                raised,
                chamfer);
            Button button = buttonImage != null
                ? buttonImage.GetComponent<Button>()
                : null;
            if (button == null || surface == null)
                return;
            surface.raycastTarget = true;
            button.targetGraphic = surface;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.16f);
            colors.pressedColor = Color.Lerp(Color.white, accent, 0.42f);
            colors.selectedColor = Color.Lerp(Color.white, accent, 0.22f);
            colors.fadeDuration = 0.10f;
            button.colors = colors;
        }

        private void ShowPackDetails(ShopPackDefinition pack)
        {
            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = ShowEconomyShop;
            BuildShopBackground("CONTEÚDO DO PACOTE");
            BuildProfessionalShopHeader(pack.DisplayName, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);

            Color accent = ResolveShopProductAccent(pack.PackId, false);
            Image summary = CreatePanel(_screenRoot, "Resumo do Pacote",
                new Vector2(0.055f, 0.735f), new Vector2(0.945f, 0.865f),
                Color.clear);
            DecorateRuntimeShopSurface(summary, accent, true, 11f);
            CreateText(summary.transform, "COLEÇÃO DO PACOTE", 11,
                FontStyle.Bold, accent, new Vector2(0.025f, 0.67f),
                new Vector2(0.30f, 0.94f), TextAnchor.MiddleLeft);
            CreateText(summary.transform, pack.Description, 15,
                FontStyle.Bold, Color.white, new Vector2(0.025f, 0.16f),
                new Vector2(0.54f, 0.69f), TextAnchor.MiddleLeft);
            CreateShopFeatureChip(summary.transform, "5", "CARTAS",
                accent, new Vector2(0.56f, 0.15f), new Vector2(0.67f, 0.85f));
            CreateShopFeatureChip(summary.transform,
                pack.CardIds.Count.ToString(), "POSSÍVEIS", accent,
                new Vector2(0.685f, 0.15f), new Vector2(0.81f, 0.85f));
            CreateShopPriceButton(summary.transform, "COMPRAR",
                pack.PriceCoins, new Vector2(0.825f, 0.15f),
                new Vector2(0.975f, 0.85f), Gold,
                () => ShowPackPurchaseConfirmation(pack));

            RectTransform content = CreateShopScrollGrid(_screenRoot,
                "Cartas do Pacote", new Vector2(0.055f, 0.055f),
                new Vector2(0.945f, 0.715f), new Vector2(205f, 295f),
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
            BuildProfessionalShopHeader(product.DisplayName, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);

            Color accent = ResolveShopProductAccent(product.ProductId, true);
            int purchased = _repository.StructureDeckPurchaseCount(
                product.ProductId);
            Image summary = CreatePanel(_screenRoot, "Resumo do Deck Estrutural",
                new Vector2(0.055f, 0.735f), new Vector2(0.945f, 0.865f),
                Color.clear);
            DecorateRuntimeShopSurface(summary, accent, true, 11f);
            CreateText(summary.transform,
                "DECK ESTRUTURAL • " + product.ArchetypeLabel, 11,
                FontStyle.Bold, accent, new Vector2(0.025f, 0.67f),
                new Vector2(0.42f, 0.94f), TextAnchor.MiddleLeft);
            CreateText(summary.transform, product.Description, 14,
                FontStyle.Bold, Color.white, new Vector2(0.025f, 0.14f),
                new Vector2(0.50f, 0.69f), TextAnchor.MiddleLeft);
            CreateShopFeatureChip(summary.transform,
                product.MainDeckCardIds.Count.ToString(), "PRINCIPAL", accent,
                new Vector2(0.515f, 0.15f), new Vector2(0.635f, 0.85f));
            CreateShopFeatureChip(summary.transform,
                product.ExtraDeckCardIds.Count.ToString(), "ADICIONAL", accent,
                new Vector2(0.65f, 0.15f), new Vector2(0.77f, 0.85f));
            CreateShopFeatureChip(summary.transform,
                $"{purchased}/{product.MaxPurchases}", "ADQUIRIDOS", accent,
                new Vector2(0.785f, 0.15f), new Vector2(0.885f, 0.85f));
            CreateShopPriceButton(summary.transform, "COMPRAR",
                product.PriceCoins, new Vector2(0.895f, 0.15f),
                new Vector2(0.985f, 0.85f), Gold,
                () => ShowStructureDeckPurchaseConfirmation(product));

            RectTransform content = CreateShopScrollGrid(_screenRoot,
                "Lista Completa", new Vector2(0.055f, 0.055f),
                new Vector2(0.945f, 0.715f), new Vector2(205f, 295f),
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
            Color rarityAccent = entry != null &&
                CardRarityCatalog.IsValid(entry.Rarity)
                    ? RarityColor(entry.Rarity)
                    : Gold;
            Image tile = CreateShopTile(parent, "Carta " + cardId, rarityAccent);
            Image artwork = CreateCardArtwork(tile.transform, entry?.Artwork,
                new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.91f), 0f, true);
            AddBanlistBadge(artwork.transform, cardId);
            CreateShopMicroBadge(tile.transform,
                entry != null && CardRarityCatalog.IsValid(entry.Rarity)
                    ? CardRarityCatalog.Label(entry.Rarity)
                    : "CARD",
                rarityAccent, new Vector2(0.69f, 0.86f),
                new Vector2(0.94f, 0.965f));
            CreateText(tile.transform, section, 9, FontStyle.Bold,
                rarityAccent, new Vector2(0.05f, 0.12f),
                new Vector2(0.95f, 0.21f), TextAnchor.MiddleCenter);
            Text cardName = CreateText(tile.transform,
                entry?.DisplayName ?? cardId, 11, FontStyle.Bold,
                Color.white, new Vector2(0.05f, 0.025f),
                new Vector2(0.95f, 0.125f), TextAnchor.MiddleCenter);
            cardName.resizeTextMinSize = 8;
            AddButtonBehaviour(artwork,
                () => ShowShopCardDetails(cardId, _shopBackAction));
        }

        private static Image CreateShopFeatureChip(
            Transform parent,
            string value,
            string label,
            Color accent,
            Vector2 min,
            Vector2 max)
        {
            Image chip = CreatePanel(parent, label, min, max, Color.clear);
            DecorateRuntimeShopSurface(chip, accent, false, 6f);
            chip.raycastTarget = false;
            CreateText(chip.transform, value, 20, FontStyle.Bold,
                Color.white, new Vector2(0.04f, 0.30f),
                new Vector2(0.96f, 0.94f), TextAnchor.MiddleCenter);
            CreateText(chip.transform, label, 8, FontStyle.Bold,
                accent, new Vector2(0.04f, 0.05f),
                new Vector2(0.96f, 0.35f), TextAnchor.MiddleCenter);
            return chip;
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
            BuildProfessionalShopHeader(entry.DisplayName, safeReturn);
            CreateCoinBalance(_screenRoot);

            Image panel = CreatePanel(_screenRoot, "Detalhes da Carta",
                new Vector2(0.07f, 0.09f), new Vector2(0.93f, 0.82f),
                new Color(0.008f, 0.025f, 0.05f, 0.98f));
            DecorateRuntimeShopSurface(panel, Gold, false, 14f);
            AddOutline(panel.gameObject,
                new Color(Gold.r, Gold.g, Gold.b, 0.78f),
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
            DecorateRuntimeShopSurface(
                metadataPanel,
                new Color(0.92f, 0.62f, 0.18f, 1f),
                false,
                7f);
            AddOutline(metadataPanel.gameObject,
                new Color(Gold.r, Gold.g, Gold.b, 0.42f),
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
                new Color(Gold.r, Gold.g, Gold.b, 0.92f));
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
                    new Color(Gold.r, Gold.g, Gold.b, 0.32f),
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
                $"independentemente e pode conter duplicatas. " +
                $"Chances: N 55% • R 25% • SR 12% • UR 8%.",
                pack.PriceCoins,
                "PACOTE PREMIUM",
                $"5 CARTAS  •  {pack.CardIds.Count} POSSÍVEIS",
                ResolveShopBoosterPackSprite(),
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
                "DECK ESTRUTURAL",
                $"{product.MainDeckCardIds.Count} PRINCIPAL  •  " +
                $"{product.ExtraDeckCardIds.Count} ADICIONAL",
                DeckRepository.ResolveCard(_catalog, product.CoverCardId)?.Artwork,
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
            string productType,
            string productMetric,
            Sprite productArtwork,
            Action confirm)
        {
            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = ShowEconomyShop;
            BuildShopBackground(section);
            BuildProfessionalShopHeader(section, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);
            Image panel = CreatePanel(_screenRoot, "Confirmação de Compra",
                new Vector2(0.16f, 0.18f), new Vector2(0.84f, 0.80f),
                new Color(0.008f, 0.025f, 0.05f, 0.99f));
            DecorateRuntimeShopSurface(panel, Gold, true, 15f);
            AddOutline(panel.gameObject,
                new Color(Gold.r, Gold.g, Gold.b, 0.84f),
                new Vector2(3f, -3f));
            CreateText(panel.transform, "REVISÃO DO PEDIDO", 11,
                FontStyle.Bold, Gold, new Vector2(0.055f, 0.89f),
                new Vector2(0.45f, 0.965f), TextAnchor.MiddleLeft);
            Image previewStage = CreatePanel(panel.transform,
                "Apresentação do Produto", new Vector2(0.055f, 0.28f),
                new Vector2(0.31f, 0.87f), Color.clear);
            DecorateRuntimeShopSurface(previewStage, Gold, false, 10f);
            if (productArtwork != null)
            {
                Image preview = CreatePanel(previewStage.transform,
                    "Arte do Produto", new Vector2(0.12f, 0.08f),
                    new Vector2(0.88f, 0.92f), Color.white);
                preview.sprite = productArtwork;
                preview.preserveAspect = true;
                preview.raycastTarget = false;
            }
            else
            {
                CreateText(previewStage.transform, "MD2\nPLUS ULTRA", 23,
                    FontStyle.Bold, Gold, new Vector2(0.08f, 0.15f),
                    new Vector2(0.92f, 0.85f), TextAnchor.MiddleCenter);
            }
            CreateText(panel.transform, productType, 12, FontStyle.Bold,
                Gold, new Vector2(0.35f, 0.75f),
                new Vector2(0.92f, 0.85f), TextAnchor.MiddleLeft);
            Text productTitle = CreateText(panel.transform, productName, 30,
                FontStyle.Bold, Color.white, new Vector2(0.35f, 0.61f),
                new Vector2(0.92f, 0.76f), TextAnchor.MiddleLeft);
            productTitle.resizeTextMinSize = 20;
            CreateText(panel.transform, productMetric, 12, FontStyle.Bold,
                new Color(0.93f, 0.78f, 0.45f, 1f),
                new Vector2(0.35f, 0.54f), new Vector2(0.92f, 0.63f),
                TextAnchor.MiddleLeft);
            CreateText(panel.transform, description, 16, FontStyle.Normal,
                Muted, new Vector2(0.35f, 0.30f),
                new Vector2(0.92f, 0.54f), TextAnchor.MiddleLeft);
            Color amountColor = _repository.CoinBalance >= price
                ? Lime
                : Danger;
            CreateShopAmountBadge(panel.transform, "PREÇO", price,
                new Vector2(0.35f, 0.16f), new Vector2(0.62f, 0.30f),
                amountColor);
            CreateShopAmountBadge(panel.transform, "SALDO",
                _repository.CoinBalance, new Vector2(0.65f, 0.16f),
                new Vector2(0.92f, 0.30f), amountColor);
            Image cancelButton = CreateButton(panel.transform, "CANCELAR",
                new Vector2(0.055f, 0.055f), new Vector2(0.31f, 0.20f),
                Danger, ShowEconomyShop);
            DecorateRuntimeShopButton(cancelButton, Danger, false, 8f);
            Image confirmButton = CreateButton(panel.transform, "CONFIRMAR COMPRA",
                new Vector2(0.35f, 0.035f), new Vector2(0.92f, 0.14f),
                Gold, confirm);
            DecorateRuntimeShopButton(confirmButton, Gold, true, 8f);
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
            _shopBackAction = ReturnFromShopToMainMenu;
            BuildShopBackground("ABERTURA DE PACOTE");
            BuildProfessionalShopHeader(
                pack?.DisplayName ?? "Pacote",
                ReturnFromShopToMainMenu);
            CreateCoinBalance(_screenRoot);
            Color accent = ResolveShopProductAccent(
                pack?.PackId ?? opening.packId,
                false);
            CardRarity peakRarity = ResolvePackOpeningPeakRarity(opening);

            if (!_packOpeningStarted)
            {
                if (peakRarity >= CardRarity.SR)
                {
                    CreatePackRarityAura(
                            _screenRoot,
                            $"Presságio do Pacote {peakRarity}",
                            new Vector2(0.275f, 0.15f),
                            new Vector2(0.725f, 0.82f),
                            peakRarity,
                            true);
                }
                Image ambientGlow = CreatePanel(_screenRoot,
                    "Luz Ambiente do Pacote", new Vector2(0.265f, 0.18f),
                    new Vector2(0.735f, 0.81f),
                    new Color(accent.r, accent.g, accent.b, 0.22f));
                ambientGlow.sprite = ResolvePackOpeningGlowSprite();
                ambientGlow.preserveAspect = true;
                ambientGlow.raycastTarget = false;
                CreateText(_screenRoot, "PACOTE SELADO  •  5 CARTAS", 12,
                    FontStyle.Bold, accent, new Vector2(0.34f, 0.76f),
                    new Vector2(0.66f, 0.81f), TextAnchor.MiddleCenter);
                Image closedPack = CreatePanel(_screenRoot, "Pacote Fechado",
                    new Vector2(0.38f, 0.29f), new Vector2(0.62f, 0.75f),
                    new Color(0.025f, 0.10f, 0.18f, 1f));
                Sprite boosterSprite = ResolveShopBoosterPackSprite();
                if (boosterSprite != null)
                {
                    closedPack.sprite = boosterSprite;
                    closedPack.color = Color.white;
                    closedPack.preserveAspect = true;
                }
                AddOutline(closedPack.gameObject,
                    new Color(accent.r, accent.g, accent.b, 0.38f),
                    new Vector2(1f, -1f));
                if (boosterSprite == null)
                {
                    CreateText(closedPack.transform, "MASTER DUEL 2\nPLUS ULTRA", 35,
                        FontStyle.Bold, Color.white, new Vector2(0.08f, 0.25f),
                        new Vector2(0.92f, 0.78f), TextAnchor.MiddleCenter);
                }
                Image openButton = CreateButton(_screenRoot, "ABRIR PACOTE",
                    new Vector2(0.38f, 0.13f), new Vector2(0.62f, 0.22f),
                    Gold, () => StartPackOpeningPresentation(opening));
                DecorateRuntimeShopButton(openButton, accent, true, 10f);
                CreateText(_screenRoot,
                    "O conteúdo da compra já está protegido no seu perfil.",
                    12, FontStyle.Bold, Muted, new Vector2(0.30f, 0.075f),
                    new Vector2(0.70f, 0.12f), TextAnchor.MiddleCenter);
                return;
            }

            bool animateEntry = playEntrySequence &&
                packOpeningAnimationEnabled &&
                opening.revealed != null &&
                opening.revealed.All(value => !value);
            PackOpeningAnimationView animationView = animateEntry
                ? CreatePackOpeningAnimationView(pack, opening)
                : null;
            Text revealInstruction = CreateText(_screenRoot,
                "REVELE AS CINCO CARTAS • O RESULTADO JÁ ESTÁ SALVO",
                14, FontStyle.Bold, Color.white, new Vector2(0.18f, 0.78f),
                new Vector2(0.82f, 0.86f), TextAnchor.MiddleCenter);
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
                // Sem sombra fixa: durante o voo ela permanecia no destino e
                // parecia uma caixa preta de encaixe vazia.
                Image card = CreateCardArtwork(cardParent,
                     artwork,
                     finalMin,
                     finalMax, 0f, false);
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
                    DecorateRevealedPackCard(
                        opening,
                        index,
                        card,
                        entry,
                        false);
                    AddButtonBehaviour(card,
                        () => ShowShopCardDetails(
                            opening.cardIds[capturedIndex],
                            () => ShowPackOpening(opening)));
                }
            }

            if (allRevealed)
                CreatePackOpeningFinishButton(opening);

            if (animationView != null)
                BeginPackOpeningPresentation(animationView);
        }

        private IEnumerator RevealPackCard(
            PendingPackOpeningRecord opening,
            int index,
            Image card)
        {
            _packRevealBusy = true;
            const float halfDuration = 0.22f;
            float elapsed = 0f;
            while (elapsed < halfDuration && card != null)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                float progress = Mathf.Clamp01(elapsed / halfDuration);
                float angle = Mathf.Lerp(0f, 90f,
                    EaseInCubic(progress));
                card.rectTransform.localRotation = Quaternion.Euler(0f, angle, 0f);
                card.rectTransform.localScale = Vector3.one *
                    (1f + Mathf.Sin(progress * Mathf.PI) * 0.035f);
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
            CardRarity revealedRarity =
                PackRarityDistribution.ResolveCardRarity(entry);
            Image revealGlow = CreatePackCardRevealGlow(card, entry, index);
            ArcaneRarityRevealGraphic rarityAura =
                CreatePackCardRarityAura(card, revealedRarity, index);
            if (card != null)
            {
                card.sprite = entry?.Artwork;
                card.color = Color.white;
                card.preserveAspect = true;
                // O selo e a moldura nascem no exato quadro em que a carta
                // está de perfil. Como são filhos do RectTransform da carta,
                // concluem a segunda metade do giro junto com a arte.
                EnsurePackCardRarityDecoration(
                    card,
                    revealedRarity,
                    index);
            }
            elapsed = 0f;
            while (elapsed < halfDuration && card != null)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                float progress = Mathf.Clamp01(elapsed / halfDuration);
                float angle = Mathf.Lerp(90f, 0f,
                    EaseOutQuint(progress));
                card.rectTransform.localRotation = Quaternion.Euler(0f, angle, 0f);
                card.rectTransform.localScale = Vector3.one *
                    Mathf.LerpUnclamped(
                        1.055f,
                        1f,
                        EaseOutBack(progress, 0.025f));
                if (revealGlow != null)
                {
                    float pulse = Mathf.Sin(progress * Mathf.PI);
                    SetPackOpeningImageAlpha(revealGlow, pulse * 0.42f);
                    revealGlow.rectTransform.localScale = Vector3.one *
                        Mathf.Lerp(0.76f, 1.26f, EaseOutQuint(progress));
                }
                rarityAura?.SetState(
                    Mathf.Lerp(0.08f, 0.46f, EaseOutQuint(progress)),
                    Mathf.Sin(progress * Mathf.PI));
                yield return null;
            }
            if (card != null)
            {
                if (revealedRarity >= CardRarity.SR)
                {
                    yield return PlayPremiumRarityRevealShowcase(
                        entry?.Artwork,
                        entry?.DisplayName ?? opening.cardIds[index],
                        revealedRarity);
                }
                yield return PlayPackCardRarityCelebration(
                    card,
                    rarityAura,
                    revealedRarity);
            }
            if (card != null)
            {
                card.rectTransform.localRotation = Quaternion.identity;
                card.rectTransform.localScale = Vector3.one;
                DecorateRevealedPackCard(
                    opening,
                    index,
                    card,
                    entry,
                    true);
            }
            if (revealGlow != null)
                Destroy(revealGlow.gameObject);
            if (rarityAura != null)
                Destroy(rarityAura.gameObject);
            _packRevealBusy = false;
            if (opening.revealed != null &&
                opening.revealed.All(value => value))
            {
                CreatePackOpeningFinishButton(opening);
            }
        }

        private ArcaneRarityRevealGraphic CreatePackCardRarityAura(
            Image card,
            CardRarity rarity,
            int index)
        {
            if (card == null || rarity == CardRarity.N)
                return null;

            Vector2 padding = rarity == CardRarity.UR
                ? new Vector2(0.34f, 0.22f)
                : rarity == CardRarity.SR
                    ? new Vector2(0.27f, 0.18f)
                    : new Vector2(0.20f, 0.14f);
            ArcaneRarityRevealGraphic aura = CreatePackRarityAura(
                card.transform,
                $"Energia {rarity} da Carta {index + 1}",
                -padding,
                Vector2.one + padding,
                rarity,
                false);
            aura?.transform.SetAsFirstSibling();
            return aura;
        }

        private IEnumerator PlayPackCardRarityCelebration(
            Image card,
            ArcaneRarityRevealGraphic rarityAura,
            CardRarity rarity)
        {
            if (card == null)
                yield break;

            float duration = rarity switch
            {
                CardRarity.UR => 0.86f,
                CardRarity.SR => 0.58f,
                CardRarity.R => 0.28f,
                _ => 0.12f
            };
            float amplitude = rarity switch
            {
                CardRarity.UR => 4.2f,
                CardRarity.SR => 3.1f,
                CardRarity.R => 1.8f,
                _ => 0.8f
            };
            RectTransform rect = card.rectTransform;
            Vector2 basePosition = rect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration && card != null)
            {
                elapsed += Mathf.Min(
                    Mathf.Max(0f, Time.unscaledDeltaTime),
                    1f / 20f);
                float progress = Mathf.Clamp01(elapsed / duration);
                float decay = (1f - progress) * (1f - progress);
                float impact = Mathf.Sin(progress * Mathf.PI * 7f) * decay;
                float vertical = Mathf.Abs(
                    Mathf.Sin(progress * Mathf.PI * 3.5f)) * decay;
                rect.anchoredPosition = basePosition + new Vector2(
                    impact * amplitude,
                    vertical * amplitude * 0.62f);
                rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    -impact * amplitude * 0.32f);
                rect.localScale = Vector3.one *
                    (1f + vertical * (rarity >= CardRarity.SR ? 0.045f : 0.018f));

                float auraEnvelope = Mathf.Sin(progress * Mathf.PI);
                rarityAura?.SetState(
                    Mathf.Lerp(0.46f, 1f, EaseOutQuint(
                        Mathf.Clamp01(progress * 1.6f))),
                    auraEnvelope);
                yield return null;
            }

            if (card != null)
            {
                rect.anchoredPosition = basePosition;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }
            rarityAura?.SetState(0f, 0f);
        }

        private void DecorateRevealedPackCard(
            PendingPackOpeningRecord opening,
            int index,
            Image card,
            CardCatalogEntry entry,
            bool replaceRevealInteraction)
        {
            if (opening == null || card == null || index < 0 ||
                index >= opening.cardIds.Count)
            {
                return;
            }

            float left = 0.075f + index * 0.185f;
            Text name = CreateText(
                _screenRoot,
                entry?.DisplayName ?? opening.cardIds[index],
                13,
                FontStyle.Bold,
                Color.white,
                new Vector2(left - 0.01f, 0.20f),
                new Vector2(left + 0.16f, 0.27f),
                TextAnchor.MiddleCenter);
            name.gameObject.name = $"Nome da Carta Revelada {index + 1}";

            CardRarity rarity =
                PackRarityDistribution.ResolveCardRarity(entry);
            EnsurePackCardRarityDecoration(card, rarity, index);

            if (!replaceRevealInteraction)
                return;

            Button button = card.GetComponent<Button>();
            if (button == null)
                return;
            button.onClick.RemoveAllListeners();
            string cardId = opening.cardIds[index];
            button.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                ShowShopCardDetails(cardId, () => ShowPackOpening(opening));
            });
        }

        private void EnsurePackCardRarityDecoration(
            Image card,
            CardRarity rarity,
            int index)
        {
            if (card == null || !CardRarityCatalog.IsValid(rarity))
                return;

            string badgeName = $"Raridade {rarity}";
            if (card.transform.Find(badgeName) == null)
            {
                CreateRarityBadge(
                    card.transform,
                    rarity,
                    new Vector2(0.66f, 0.895f),
                    new Vector2(0.985f, 0.995f),
                    12);
            }
            EnsureRevealedPackCardRarityFrame(card, rarity, index);
        }

        private void EnsureRevealedPackCardRarityFrame(
            Image card,
            CardRarity rarity,
            int index)
        {
            if (card == null || rarity < CardRarity.SR)
                return;

            string objectName = $"Moldura Persistente {rarity} da Carta {index + 1}";
            if (card.transform.Find(objectName) != null)
                return;

            Vector2 padding = rarity == CardRarity.UR
                ? new Vector2(0.045f, 0.026f)
                : new Vector2(0.034f, 0.020f);
            ArcaneRarityCardFrameGraphic frame = CreateRarityCardFrame(
                card.transform,
                objectName,
                -padding,
                Vector2.one + padding,
                rarity,
                true);
            frame?.transform.SetAsFirstSibling();
        }

        private Image CreatePackCardRevealGlow(
            Image card,
            CardCatalogEntry entry,
            int index)
        {
            if (card == null || _screenRoot == null)
                return null;
            RectTransform cardRect = card.rectTransform;
            Vector2 padding = new(0.025f, 0.045f);
            CardRarity rarity =
                PackRarityDistribution.ResolveCardRarity(entry);
            Color tint = RarityColor(rarity);
            Image glow = CreatePanel(
                _screenRoot,
                $"Clarão da Carta Revelada {index + 1}",
                cardRect.anchorMin - padding,
                cardRect.anchorMax + padding,
                new Color(tint.r, tint.g, tint.b, 0f));
            glow.sprite = ResolvePackOpeningGlowSprite();
            glow.preserveAspect = true;
            glow.raycastTarget = false;
            glow.transform.SetSiblingIndex(card.transform.GetSiblingIndex());
            return glow;
        }

        private void CreatePackOpeningFinishButton(
            PendingPackOpeningRecord opening)
        {
            if (opening == null || _screenRoot == null ||
                _screenRoot.Find("Botão CONCLUIR ABERTURA") != null)
            {
                return;
            }

            Image finishButton = CreateButton(
                _screenRoot,
                "CONCLUIR ABERTURA",
                new Vector2(0.36f, 0.10f),
                new Vector2(0.64f, 0.18f),
                Gold,
                () =>
                {
                    if (_repository.TryCompletePackOpening(
                            opening.transactionId,
                            out string rejection))
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
            DecorateRuntimeShopButton(finishButton, Gold, true, 9f);
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

            // Remove a linguagem azul da casca compartilhada sem alterar as
            // outras telas do frontend. Nas páginas da loja, o título real já
            // é desenhado pelo cabeçalho e a legenda antiga só disputava
            // espaço com o saldo.
            Image legacyHeader = background.transform.Find("Faixa Superior")
                ?.GetComponent<Image>();
            if (legacyHeader != null)
            {
                legacyHeader.color =
                    new Color(0.012f, 0.010f, 0.009f, 0.92f);
                Text sectionLabel = legacyHeader.GetComponentInChildren<Text>(true);
                if (sectionLabel != null)
                    sectionLabel.gameObject.SetActive(false);
            }
            Image legacyAccent = background.transform.Find("Linha Ciano")
                ?.GetComponent<Image>();
            if (legacyAccent != null)
                legacyAccent.color =
                    new Color(Gold.r, Gold.g, Gold.b, 0.78f);
            for (int lineIndex = 1; lineIndex <= 9; lineIndex++)
            {
                Image line = background.transform.Find($"Linha {lineIndex}")
                    ?.GetComponent<Image>();
                if (line != null)
                    line.color = new Color(
                        Gold.r, Gold.g, Gold.b, 0.055f);
            }

            Image image = CreatePanel(background.transform,
                "Arte de Fundo da Loja", Vector2.zero, Vector2.one,
                Color.white);
            image.sprite = artwork;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.transform.SetAsFirstSibling();
            var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = artwork.rect.width / artwork.rect.height;

            Image veil = CreatePanel(background.transform,
                "Contraste da Arte da Loja", Vector2.zero, Vector2.one,
                new Color(0.010f, 0.008f, 0.012f, 0.46f));
            veil.raycastTarget = false;
            veil.transform.SetSiblingIndex(1);

            Image upperRail = CreatePanel(
                background.transform,
                "Filete superior dourado",
                new Vector2(0.035f, 0.902f),
                new Vector2(0.965f, 0.906f),
                new Color(Gold.r, Gold.g, Gold.b, 0.76f));
            upperRail.raycastTarget = false;
            Image lowerRail = CreatePanel(
                background.transform,
                "Filete inferior dourado",
                new Vector2(0.055f, 0.043f),
                new Vector2(0.945f, 0.046f),
                new Color(Gold.r, Gold.g, Gold.b, 0.42f));
            lowerRail.raycastTarget = false;
        }

        private void BuildProfessionalShopHeader(
            string title,
            Action backAction)
        {
            BuildHeader(title, backAction);
            Image back = _screenRoot.Find("Botão ‹")?.GetComponent<Image>();
            if (back != null)
                DecorateRuntimeShopButton(
                    back,
                    new Color(0.98f, 0.68f, 0.18f, 1f),
                    true,
                    8f);

            Text titleText = _screenRoot.Find(title)?.GetComponent<Text>();
            if (titleText == null)
                return;
            MasterDuelTypography.Apply(
                titleText,
                FontStyle.Bold,
                titleText.fontSize);
            Shadow shadow = titleText.GetComponent<Shadow>() ??
                            titleText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;
        }

        private Sprite ResolveShopBackgroundSprite()
        {
            return ResolveShopVisualSprite(shopBackgroundSprite,
                "Shop/ShopBackgroundGold-v2",
                "Fundo Dourado da Loja",
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
                return imported;
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
                trackImage.color = new Color(0.055f, 0.038f, 0.018f, 0.96f);
            Image handle = track.Find("Área Deslizante/Alça")
                ?.GetComponent<Image>();
            if (handle != null)
                handle.color = new Color(Gold.r, Gold.g, Gold.b, 0.92f);
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
            // Sprite.Create marks the generated fallback as DontSave. A
            // Resources sprite is an imported asset owned by Unity and must
            // never be destroyed by a runtime view.
            if ((sprite.hideFlags & HideFlags.DontSave) != 0)
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

        private void ReturnFromShopToMainMenu()
        {
            LeaveShop(() => RunMainMenuTransition(ShowMainMenu));
        }
    }
}
