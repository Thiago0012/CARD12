using System;
using ArcaneDuel.Game.Accounts;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private void RefreshAuthoredMainMenuArtwork()
        {
            if (_mainMenuSceneView == null)
                return;
            string artworkId = _repository?.EquippedArtworkId ??
                               ProfileArtworkCatalog.DefaultArtworkId;
            _mainMenuSceneView.SetEquippedArtwork(
                ProfileArtworkCatalog.LoadSprite(artworkId),
                artworkId);
        }

        private void CreateRuntimeMainMenuArtwork()
        {
            string artworkId = _repository?.EquippedArtworkId ??
                               ProfileArtworkCatalog.DefaultArtworkId;
            Sprite sprite = ProfileArtworkCatalog.LoadSprite(artworkId);
            if (sprite == null || _screenRoot == null)
                return;

            var viewportObject = new GameObject(
                "Artwork Equipada - Recorte da Moldura",
                typeof(RectTransform),
                typeof(RectMask2D));
            viewportObject.transform.SetParent(_screenRoot, false);
            RectTransform viewport =
                viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = new Vector2(0.365f, 0.165f);
            viewport.anchorMax = new Vector2(0.955f, 0.845f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            Image image = CreateArtworkImage(
                viewport,
                "Arte Flutuante",
                sprite,
                new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.975f));
            CanvasGroup group = image.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            MainMenuArtworkFloat motion =
                image.gameObject.AddComponent<MainMenuArtworkFloat>();
            motion.Configure(artworkId);
        }

        private void CreateArtworkShopTile(
            Transform parent,
            ProfileArtworkDefinition artwork)
        {
            bool owned = _repository?.OwnsArtwork(artwork.ArtworkId) == true;
            bool equipped = owned && string.Equals(
                _repository?.EquippedArtworkId,
                artwork.ArtworkId,
                StringComparison.Ordinal);
            Color accent = equipped
                ? new Color(0.78f, 1f, 0.20f, 1f)
                : owned
                    ? new Color(0.34f, 0.88f, 0.96f, 1f)
                    : Gold;
            Image tile = CreateShopTile(
                parent,
                artwork.DisplayName,
                accent);

            CreateText(tile.transform,
                equipped ? "ARTWORK EQUIPADA" : owned
                    ? "ARTWORK ADQUIRIDA"
                    : "ARTWORK PREMIUM",
                10,
                FontStyle.Bold,
                new Color(accent.r, accent.g, accent.b, 0.94f),
                new Vector2(0.055f, 0.90f),
                new Vector2(0.945f, 0.97f),
                TextAnchor.MiddleCenter);
            Text title = CreateText(tile.transform,
                artwork.DisplayName,
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.055f, 0.79f),
                new Vector2(0.945f, 0.90f),
                TextAnchor.MiddleCenter);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 11;

            Image stage = CreatePanel(
                tile.transform,
                "Prévia da Artwork",
                new Vector2(0.055f, 0.205f),
                new Vector2(0.945f, 0.79f),
                new Color(0.002f, 0.006f, 0.012f, 0.74f));
            stage.gameObject.AddComponent<RectMask2D>();
            DecorateRuntimeShopSurface(stage, accent, false, 12f);
            Sprite sprite = ProfileArtworkCatalog.LoadSprite(
                artwork.ArtworkId);
            if (sprite != null)
            {
                CreateArtworkImage(stage.transform,
                    artwork.DisplayName,
                    sprite,
                    new Vector2(0.025f, 0.025f),
                    new Vector2(0.975f, 0.975f));
            }
            else
            {
                CreateText(stage.transform,
                    "ARTE INDISPONÍVEL",
                    14,
                    FontStyle.Bold,
                    Muted,
                    new Vector2(0.08f, 0.08f),
                    new Vector2(0.92f, 0.92f),
                    TextAnchor.MiddleCenter);
            }

            if (owned)
            {
                string action = equipped ? "EQUIPADA" : "EQUIPAR";
                Image button = CreateButton(
                    tile.transform,
                    action,
                    new Vector2(0.08f, 0.04f),
                    new Vector2(0.92f, 0.18f),
                    accent,
                    () => HandleArtworkShopAction(artwork));
                DecorateRuntimeShopButton(button, accent, !equipped, 8f);
            }
            else
            {
                CreateShopPriceButton(tile.transform,
                    "COMPRAR",
                    artwork.PriceCoins,
                    new Vector2(0.08f, 0.04f),
                    new Vector2(0.92f, 0.18f),
                    Gold,
                    () => HandleArtworkShopAction(artwork));
            }
        }

        private void HandleArtworkShopAction(
            ProfileArtworkDefinition artwork)
        {
            if (_repository?.OwnsArtwork(artwork.ArtworkId) == true)
            {
                if (!_repository.TryEquipArtwork(
                        artwork.ArtworkId,
                        out string rejection))
                {
                    _shopFeedback = rejection;
                    _shopFeedbackIsError = true;
                }
                else
                {
                    _shopFeedback = $"{artwork.DisplayName} equipada na tela inicial.";
                    _shopFeedbackIsError = false;
                }
                ShowEconomyShop();
                return;
            }

            ShowArtworkPurchaseConfirmation(artwork);
        }

        private void ShowArtworkPurchaseConfirmation(
            ProfileArtworkDefinition artwork)
        {
            if (!PlayerIdAccessRuntime.Allows(
                    PlayerIdCapability.Economy,
                    out string accessRejection))
            {
                ShowPlayerIdCapabilityBlocked(accessRejection);
                return;
            }

            SetDuelPresentation(false);
            ClearScreen();
            BuildShopBackground("CONFIRMAR ARTWORK");
            BuildProfessionalShopHeader(
                artwork.DisplayName,
                ShowEconomyShop);
            CreateCoinBalance(_screenRoot);

            Image panel = CreatePanel(
                _screenRoot,
                "Confirmação da Artwork",
                new Vector2(0.23f, 0.12f),
                new Vector2(0.77f, 0.82f),
                new Color(0.008f, 0.025f, 0.05f, 0.99f));
            DecorateRuntimeShopSurface(panel, Gold, true, 15f);
            CreateText(panel.transform,
                "ARTWORK DA TELA INICIAL",
                14,
                FontStyle.Bold,
                Gold,
                new Vector2(0.08f, 0.91f),
                new Vector2(0.92f, 0.97f),
                TextAnchor.MiddleCenter);

            Image preview = CreatePanel(
                panel.transform,
                "Prévia Ampliada",
                new Vector2(0.08f, 0.30f),
                new Vector2(0.92f, 0.89f),
                new Color(0.002f, 0.006f, 0.012f, 0.74f));
            preview.gameObject.AddComponent<RectMask2D>();
            DecorateRuntimeShopSurface(preview, Gold, false, 16f);
            Sprite sprite = ProfileArtworkCatalog.LoadSprite(
                artwork.ArtworkId);
            if (sprite != null)
            {
                CreateArtworkImage(preview.transform,
                    artwork.DisplayName,
                    sprite,
                    new Vector2(0.02f, 0.02f),
                    new Vector2(0.98f, 0.98f));
            }

            CreateText(panel.transform,
                artwork.DisplayName,
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.22f),
                new Vector2(0.93f, 0.30f),
                TextAnchor.MiddleCenter);
            CreateText(panel.transform,
                "Após a compra, equipe a arte na categoria Artwork.",
                13,
                FontStyle.Bold,
                Muted,
                new Vector2(0.08f, 0.17f),
                new Vector2(0.92f, 0.23f),
                TextAnchor.MiddleCenter);
            CreateShopPriceButton(panel.transform,
                "COMPRAR POR",
                artwork.PriceCoins,
                new Vector2(0.18f, 0.045f),
                new Vector2(0.82f, 0.16f),
                Gold,
                () =>
                {
                    bool ok = _repository.TryPurchaseArtwork(
                        artwork.ArtworkId,
                        Guid.NewGuid().ToString("N"),
                        out _,
                        out string rejection);
                    _shopFeedback = ok
                        ? $"{artwork.DisplayName} adquirida. Agora você pode equipá-la."
                        : rejection;
                    _shopFeedbackIsError = !ok;
                    ShowEconomyShop();
                });
        }

        private static Image CreateArtworkImage(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 min,
            Vector2 max)
        {
            var item = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = item.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }
}
