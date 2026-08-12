using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class ProjectBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator SceneMarkerCanBeCreatedAtRuntime()
        {
            var gameObject = new GameObject("TestSceneMarker");
            SceneMarker marker = gameObject.AddComponent<SceneMarker>();
            marker.Configure(SceneRole.CardLab);

            yield return null;

            Assert.That(marker.Role, Is.EqualTo(SceneRole.CardLab));
            Object.Destroy(gameObject);
        }

        [UnityTest]
        public IEnumerator MainMenuUsesTheNewHudAndDedicatedButtons()
        {
            SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            yield return null;
            yield return null;

            MonoBehaviour frontend = null;
            foreach (MonoBehaviour candidate in
                     Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.GetType().Name != "GameFrontendBootstrap")
                    continue;
                frontend = candidate;
                break;
            }
            Assert.That(frontend, Is.Not.Null);
            object repository = frontend.GetType().GetField(
                    "_repository",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(frontend);
            object state = repository?.GetType().GetProperty("State")
                ?.GetValue(repository);
            state?.GetType().GetField("starterDeckClaimed")
                ?.SetValue(state, true);
            frontend.GetType().GetMethod("ShowMainMenu")?.Invoke(
                frontend,
                null);
            yield return null;

            Assert.That(
                GameObject.Find("Moldura HUD da Tela Inicial"),
                Is.Not.Null);
            Assert.That(GameObject.Find("Ação DUELAR"), Is.Not.Null);
            Assert.That(GameObject.Find("Ação DECKS"), Is.Not.Null);
            Assert.That(GameObject.Find("Ação LOJA"), Is.Not.Null);
            Assert.That(GameObject.Find("Ação MULTIPLAYER"), Is.Not.Null);
            Assert.That(
                GameObject.Find("Arte Botão MULTIPLAYER")
                    .GetComponent<RectTransform>().anchorMin.y,
                Is.EqualTo(0.2065f).Within(0.0001f));
            Assert.That(
                GameObject.Find("Arte Botão DECKS")
                    .GetComponent<RectTransform>().anchorMin.y,
                Is.EqualTo(-0.1044f).Within(0.0001f));
            Assert.That(
                GameObject.Find("Arte Botão LOJA")
                    .GetComponent<RectTransform>().anchorMin.y,
                Is.EqualTo(-0.1028f).Within(0.0001f));

            Object assets =
                Resources.Load("Frontend/MainMenuUiAssets");
            Assert.That(assets, Is.Not.Null);
            Assert.That(
                assets.GetType().GetField("interfaceClick")?.GetValue(assets),
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ShopMysteryArtworkUsesTheAuthoredQuestionCardCrop()
        {
            SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            yield return null;
            yield return null;

            MonoBehaviour frontend = null;
            foreach (MonoBehaviour candidate in
                     Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.GetType().Name == "GameFrontendBootstrap")
                {
                    frontend = candidate;
                    break;
                }
            }
            Assert.That(frontend, Is.Not.Null);

            MethodInfo resolver = frontend.GetType().GetMethod(
                "ResolveShopMysteryCardSprite",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolver, Is.Not.Null);
            var sprite = resolver.Invoke(frontend, null) as Sprite;

            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.texture, Is.Not.Null);
            Assert.That(sprite.texture.name, Is.EqualTo("CardArtFallback"));
            Assert.That(sprite.rect.width / sprite.rect.height,
                Is.InRange(0.70f, 0.74f));
            Assert.That(sprite.rect.width, Is.LessThan(sprite.texture.width));
            Assert.That(sprite.rect.height, Is.LessThan(sprite.texture.height));
        }

        [UnityTest]
        public IEnumerator ShopCardDetailsUseReadableScrollableEffectText()
        {
            SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            yield return null;
            yield return null;

            MonoBehaviour frontend = null;
            foreach (MonoBehaviour candidate in
                     Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.GetType().Name == "GameFrontendBootstrap")
                {
                    frontend = candidate;
                    break;
                }
            }
            Assert.That(frontend, Is.Not.Null);

            MethodInfo showDetails = frontend.GetType().GetMethod(
                "ShowShopCardDetails",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showDetails, Is.Not.Null);
            showDetails.Invoke(frontend, new object[] { "32138660", null });
            yield return null;

            GameObject titleObject = GameObject.Find("Título da Carta da Loja");
            GameObject metadataObject =
                GameObject.Find("Texto dos Metadados da Carta da Loja");
            GameObject effectPanel = GameObject.Find("Painel do Efeito da Carta");
            GameObject effectObject = GameObject.Find("Texto do Efeito da Carta");

            Assert.That(titleObject, Is.Not.Null);
            Assert.That(titleObject.GetComponent<Text>().fontSize,
                Is.GreaterThanOrEqualTo(30));
            Assert.That(metadataObject, Is.Not.Null);
            Assert.That(metadataObject.GetComponent<Text>().fontSize,
                Is.GreaterThanOrEqualTo(17));
            Assert.That(effectPanel, Is.Not.Null);
            Assert.That(effectPanel.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(effectObject, Is.Not.Null);
            Text effectText = effectObject.GetComponent<Text>();
            Assert.That(effectText.resizeTextForBestFit, Is.False);
            Assert.That(effectText.fontSize, Is.GreaterThanOrEqualTo(19));
            Assert.That(effectText.color.grayscale, Is.GreaterThan(0.9f));
            Assert.That(effectText.text.Length, Is.GreaterThan(100));
        }

        [UnityTest]
        public IEnumerator ShopUsesPersistentAuthoredHierarchyAtRuntime()
        {
            SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            yield return null;
            yield return null;

            MonoBehaviour frontend = Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include)
                .FirstOrDefault(candidate =>
                    candidate.GetType().Name == "GameFrontendBootstrap");
            Assert.That(frontend, Is.Not.Null);

            MethodInfo showShop = frontend.GetType().GetMethod(
                "ShowEconomyShop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showShop, Is.Not.Null);
            showShop.Invoke(frontend, null);
            yield return null;

            MonoBehaviour shop = Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include)
                .FirstOrDefault(candidate =>
                    candidate.GetType().FullName ==
                    "ArcaneArena.Frontend.ShopSceneView");
            Assert.That(shop, Is.Not.Null);
            RectTransform root = shop.GetType().GetProperty("Root")
                ?.GetValue(shop) as RectTransform;
            RectTransform content = shop.GetType()
                .GetProperty("CatalogContent")
                ?.GetValue(shop) as RectTransform;
            Assert.That(root, Is.Not.Null);
            Assert.That(content, Is.Not.Null);
            Assert.That(root.gameObject.activeInHierarchy, Is.True);
            Assert.That(content.childCount, Is.GreaterThan(0));
            Assert.That(GameObject.Find("Catalog Scroll View"), Is.Not.Null);

            frontend.GetType().GetMethod("ShowMainMenu")?.Invoke(
                frontend,
                null);
            yield return null;
            Assert.That(root.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ShopUsesTheNewBackgroundCardsAndCurrencyArtwork()
        {
            SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            yield return null;
            yield return null;

            MonoBehaviour frontend = null;
            foreach (MonoBehaviour candidate in
                     Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.GetType().Name == "GameFrontendBootstrap")
                {
                    frontend = candidate;
                    break;
                }
            }
            Assert.That(frontend, Is.Not.Null);

            string[] resolverNames =
            {
                "ResolveShopBackgroundSprite",
                "ResolveShopBoosterPackSprite",
                "ResolveShopCurrencySprite"
            };
            string[] textureNames =
            {
                "ShopBackground",
                "BoosterPack",
                "CurrencyCrystal"
            };
            for (int index = 0; index < resolverNames.Length; index++)
            {
                MethodInfo resolver = frontend.GetType().GetMethod(
                    resolverNames[index],
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(resolver, Is.Not.Null);
                var sprite = resolver.Invoke(frontend, null) as Sprite;
                Assert.That(sprite, Is.Not.Null, resolverNames[index]);
                Assert.That(sprite.texture, Is.Not.Null, resolverNames[index]);
                Assert.That(sprite.texture.name, Is.EqualTo(textureNames[index]));
            }

            MethodInfo buildBackground = frontend.GetType().GetMethod(
                "BuildShopBackground",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildBackground, Is.Not.Null);
            buildBackground.Invoke(frontend, new object[] { "LOJA" });

            Image backgroundArt = null;
            foreach (Image candidate in Object.FindObjectsByType<Image>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.gameObject.name != "Arte de Fundo da Loja")
                    continue;
                backgroundArt = candidate;
                break;
            }
            Assert.That(backgroundArt, Is.Not.Null);
            Assert.That(backgroundArt.sprite, Is.Not.Null);
            Assert.That(backgroundArt.GetComponent<AspectRatioFitter>(), Is.Not.Null);

            System.Type packCatalogType = frontend.GetType().Assembly.GetType(
                "ArcaneArena.Frontend.ShopPackCatalog");
            Assert.That(packCatalogType, Is.Not.Null);
            object packs = packCatalogType.GetProperty(
                    "Packs",
                    BindingFlags.Static | BindingFlags.Public)
                ?.GetValue(null);
            object firstPack = null;
            if (packs is IEnumerable enumerable)
            {
                foreach (object pack in enumerable)
                {
                    firstPack = pack;
                    break;
                }
            }
            Assert.That(firstPack, Is.Not.Null);

            var tileHost = new GameObject(
                "Teste da Vitrine do Pacote",
                typeof(RectTransform));
            MethodInfo createPackTile = frontend.GetType().GetMethod(
                "CreatePackProductTile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(createPackTile, Is.Not.Null);
            createPackTile.Invoke(frontend,
                new[] { tileHost.transform, firstPack });

            int highlightedCards = 0;
            int openPacks = 0;
            int priceGems = 0;
            Image highlightedCardImage = null;
            foreach (Image image in tileHost.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject.name.StartsWith("Carta Destaque "))
                {
                    highlightedCards++;
                    highlightedCardImage ??= image;
                }
                if (image.gameObject.name.StartsWith("Pacote Aberto "))
                {
                    openPacks++;
                }
                if (image.gameObject.name == "Gema do Preço")
                    priceGems++;
            }
            Assert.That(highlightedCards, Is.EqualTo(3));
            Assert.That(openPacks, Is.Zero,
                "A vitrine deve mostrar somente as três cartas, sem o pacote aberto.");
            Assert.That(priceGems, Is.EqualTo(1));
            Assert.That(highlightedCardImage, Is.Not.Null);
            float cardWidth = highlightedCardImage.rectTransform.anchorMax.x -
                highlightedCardImage.rectTransform.anchorMin.x;
            float cardHeight = highlightedCardImage.rectTransform.anchorMax.y -
                highlightedCardImage.rectTransform.anchorMin.y;
            Assert.That(cardWidth, Is.EqualTo(0.105f).Within(0.0001f));
            Assert.That(cardHeight, Is.EqualTo(0.30f).Within(0.0001f));
            Assert.That(highlightedCardImage.rectTransform.anchorMax.y,
                Is.LessThanOrEqualTo(0.66f));
            Object.Destroy(tileHost);
        }

        [UnityTest]
        public IEnumerator PackOpeningAnimationEndsAtExistingFiveRevealAnchors()
        {
            SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            yield return null;
            yield return null;

            MonoBehaviour frontend = null;
            foreach (MonoBehaviour candidate in
                     Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.GetType().Name == "GameFrontendBootstrap")
                {
                    frontend = candidate;
                    break;
                }
            }
            Assert.That(frontend, Is.Not.Null);

            string[] shortDurationFields =
            {
                "packOpeningFadeDuration",
                "packOpeningEnterDuration",
                "packOpeningAnticipationDuration",
                "packOpeningTearDuration",
                "packOpeningFlapDuration",
                "packOpeningBurstDuration",
                "packOpeningStackRiseDuration",
                "packOpeningFanDuration",
                "packOpeningSettleDuration"
            };
            foreach (string fieldName in shortDurationFields)
            {
                FieldInfo field = frontend.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, fieldName);
                field.SetValue(frontend, 0.01f);
            }
            frontend.GetType().GetField(
                    "packOpeningCardStagger",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(frontend, 0.002f);
            frontend.GetType().GetField(
                    "packOpeningAnimationEnabled",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(frontend, true);

            System.Type packCatalogType = frontend.GetType().Assembly.GetType(
                "ArcaneArena.Frontend.ShopPackCatalog");
            Assert.That(packCatalogType, Is.Not.Null);
            object packCollection = packCatalogType.GetProperty(
                    "Packs",
                    BindingFlags.Static | BindingFlags.Public)
                ?.GetValue(null);
            object pack = ((IEnumerable)packCollection).Cast<object>().First();
            List<string> cards = ((IEnumerable)pack.GetType()
                    .GetProperty("CardIds")?.GetValue(pack))
                .Cast<object>()
                .Take(5)
                .Select(value => value.ToString())
                .ToList();
            Assert.That(cards, Has.Count.EqualTo(5));
            System.Type openingType = frontend.GetType().Assembly.GetType(
                "ArcaneArena.Frontend.PendingPackOpeningRecord");
            Assert.That(openingType, Is.Not.Null);
            object opening = System.Activator.CreateInstance(openingType);
            openingType.GetField("transactionId")?.SetValue(
                opening,
                "playmode-pack-animation");
            openingType.GetField("packId")?.SetValue(
                opening,
                pack.GetType().GetProperty("PackId")?.GetValue(pack));
            openingType.GetField("cardIds")?.SetValue(opening, cards);
            var revealedState = Enumerable.Repeat(false, 5).ToList();
            openingType.GetField("revealed")?.SetValue(
                opening,
                revealedState);

            MethodInfo showOpening = frontend.GetType().GetMethod(
                "ShowPackOpening",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showOpening, Is.Not.Null);
            showOpening.Invoke(frontend, new object[] { opening, false });
            yield return null;

            GameObject openButtonObject = GameObject.Find("Botão ABRIR PACOTE");
            Assert.That(openButtonObject, Is.Not.Null);
            Button openButton = openButtonObject.GetComponent<Button>();
            Assert.That(openButton, Is.Not.Null);
            openButton.onClick.Invoke();

            Assert.That(frontend.GetType().GetProperty(
                    "IsPackOpeningPresentationActive")?.GetValue(frontend),
                Is.True);
            Assert.That(frontend.GetType().GetProperty(
                    "PackOpeningPresentationStateName")?.GetValue(frontend),
                Is.Not.EqualTo("RevealReady"));
            Assert.That(revealedState.Any(value => value), Is.False,
                "A apresentação não pode revelar nem alterar o resultado salvo.");

            Image[] movingCards = Object.FindObjectsByType<Image>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(image => image.gameObject.name.StartsWith("Carta Oculta "))
                .ToArray();
            Assert.That(movingCards, Has.Length.EqualTo(5));
            Assert.That(movingCards.All(card =>
                    card.GetComponent<Button>() != null &&
                    !card.GetComponent<Button>().interactable),
                Is.True,
                "As cartas devem ignorar cliques durante a sequência.");

            yield return new WaitForSecondsRealtime(0.35f);
            yield return null;
            yield return null;

            Assert.That(frontend.GetType().GetProperty(
                    "IsPackOpeningPresentationActive")?.GetValue(frontend),
                Is.False);
            Assert.That(frontend.GetType().GetProperty(
                    "PackOpeningPresentationStateName")?.GetValue(frontend),
                Is.EqualTo("RevealReady"));
            Assert.That(GameObject.Find("Animação de Abertura do Pacote"),
                Is.Null);
            Assert.That(revealedState.Any(value => value), Is.False);

            Image[] settledCards = Object.FindObjectsByType<Image>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(image => image.gameObject.name.StartsWith("Carta Oculta "))
                .OrderBy(image => image.gameObject.name)
                .ToArray();
            Assert.That(settledCards, Has.Length.EqualTo(5));
            for (int index = 0; index < settledCards.Length; index++)
            {
                RectTransform rect = settledCards[index].rectTransform;
                float expectedLeft = 0.075f + index * 0.185f;
                Assert.That(rect.anchorMin.x,
                    Is.EqualTo(expectedLeft).Within(0.0001f));
                Assert.That(rect.anchorMax.x,
                    Is.EqualTo(expectedLeft + 0.15f).Within(0.0001f));
                Assert.That(rect.anchorMin.y,
                    Is.EqualTo(0.27f).Within(0.0001f));
                Assert.That(rect.anchorMax.y,
                    Is.EqualTo(0.72f).Within(0.0001f));
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
                Assert.That(rect.localRotation,
                    Is.EqualTo(Quaternion.identity));
                Assert.That(rect.GetComponent<Button>().interactable, Is.True);
                Assert.That(rect.GetComponent<CanvasGroup>(), Is.Null,
                    "O bloqueador temporário da animação não pode permanecer " +
                    "sobre a carta revelável.");
                Assert.That(rect.GetComponent<Image>().raycastTarget, Is.True);
            }
        }
    }
}
