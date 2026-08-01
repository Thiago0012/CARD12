using System.Collections;
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
            frontend.GetType().GetMethod("ShowMainMenu")?.Invoke(
                frontend,
                null);
            yield return null;

            Assert.That(GameObject.Find("HUD da Tela Inicial"), Is.Not.Null);
            Assert.That(GameObject.Find("Ação DUELAR"), Is.Not.Null);
            Assert.That(GameObject.Find("Ação DECKS"), Is.Not.Null);
            Assert.That(GameObject.Find("Ação LOJA"), Is.Not.Null);
            Assert.That(GameObject.Find("Ação MULTIPLAYER"), Is.Not.Null);
            Assert.That(GameObject.Find("Qualidade da Conexão"), Is.Not.Null);

            Object assets =
                Resources.Load("Frontend/MainMenuUiAssets");
            Assert.That(assets, Is.Not.Null);
            Assert.That(
                assets.GetType().GetField("interfaceClick")?.GetValue(assets),
                Is.Not.Null);
        }
    }
}
