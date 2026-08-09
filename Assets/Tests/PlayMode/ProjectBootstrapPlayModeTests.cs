using System.Collections;
using System.Reflection;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
    }
}
