using System;
using System.Collections;
using System.IO;
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
    public sealed class StarterDeckOnboardingPlayModeTests
    {
        [UnityTest]
        public IEnumerator NewProfileCannotBypassSixDeckGallery()
        {
            string path = Path.Combine(
                Path.GetFullPath(Path.Combine("Temp", "StarterOnboardingPlayMode")),
                "profile-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
                yield return null;
                yield return null;

                MonoBehaviour frontend = UnityEngine.Object
                    .FindObjectsByType<MonoBehaviour>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(item =>
                        item.GetType().FullName ==
                        "ArcaneArena.Frontend.GameFrontendBootstrap");
                Assert.That(frontend, Is.Not.Null);
                Type repositoryType = TypeByName(
                    "ArcaneArena.Frontend.DeckRepository");
                object repository = Activator.CreateInstance(repositoryType, path);
                UnityEngine.Object cardCatalog = frontend.GetType().GetField(
                        "_catalog",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(frontend) as UnityEngine.Object;
                Assert.That(cardCatalog, Is.Not.Null);
                repositoryType.GetMethod("Load").Invoke(
                    repository, new[] { cardCatalog, (object)false });
                object[] nameArguments = { "Duelista Novo", null };
                Assert.That(repositoryType.GetMethod("TrySetPlayerDisplayName")
                    .Invoke(repository, nameArguments), Is.True);

                frontend.GetType().GetField(
                        "_repository",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(frontend, repository);
                frontend.GetType().GetMethod("ShowMainMenu")
                    .Invoke(frontend, null);
                yield return null;

                StarterDeckCatalog catalog = Resources.Load<StarterDeckCatalog>(
                    "StarterDecks/StarterDeckCatalog");
                Assert.That(catalog, Is.Not.Null);
                Assert.That(catalog.Decks.Count, Is.EqualTo(6));
                foreach (StarterDeckDefinition deck in catalog.Decks)
                {
                    Assert.That(
                        deck.DisplayName,
                        Does.Not.Match(@"^Deck Inicial \d+$"));
                    Assert.That(GameObject.Find(deck.DisplayName), Is.Not.Null);
                }
                Assert.That(GameObject.Find("Moldura HUD da Tela Inicial"), Is.Null,
                    "O menu nao pode reaparecer antes do claim.");

                StarterDeckDefinition choice = catalog.Decks
                    .First(deck => deck != null && deck.IsPublishable);
                frontend.GetType().GetMethod(
                        "ShowStarterDeckDetails",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(frontend, new object[] { choice, "Main" });
                yield return null;
                Assert.That(GameObject.Find("Detalhes da Carta"), Is.Not.Null);
                Assert.That(GameObject.Find("Cartas do Main"), Is.Not.Null);
                Assert.That(
                    GameObject.Find("Visualizador Ampliado do Deck Inicial"),
                    Is.Not.Null);
                Text detailType = frontend.GetType().GetField(
                        "_starterDetailType",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(frontend) as Text;
                Assert.That(detailType, Is.Not.Null);
                Assert.That(detailType.text, Does.Not.Contain("ID"));
                Image detailArtwork = frontend.GetType().GetField(
                        "_starterDetailArtwork",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(frontend) as Image;
                Assert.That(detailArtwork, Is.Not.Null);
                Assert.That(detailArtwork.color, Is.EqualTo(Color.white));
                detailArtwork.GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.That(
                    GameObject.Find("Visualizador Ampliado do Deck Inicial")
                        .activeSelf,
                    Is.True);

                object[] claimArguments = { choice, catalog, null, null };
                Assert.That(repositoryType.GetMethod("TryClaimStarterDeck")
                    .Invoke(repository, claimArguments), Is.True,
                    claimArguments[3] as string);
                frontend.GetType().GetMethod("ShowMainMenu")
                    .Invoke(frontend, null);
                yield return null;
                Assert.That(GameObject.Find("Moldura HUD da Tela Inicial"), Is.Not.Null);
            }
            finally
            {
                string directory = Path.GetDirectoryName(path);
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(
                                 directory,
                                 Path.GetFileName(path) + "*"))
                    {
                        File.Delete(file);
                    }
                }
            }
        }

        private static Type TypeByName(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Tipo ausente: " + fullName);
            return type;
        }
    }
}
