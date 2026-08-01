using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class PlayableArenaPlayModeTests
    {
        [UnityTest]
        public IEnumerator DuelSceneBootsPlayableArena()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            Assert.That(Object.FindAnyObjectByType<DuelArenaController>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MainMenuIsTheOnlyPlayerFacingStartScene()
        {
            SceneManager.LoadScene(ProjectIdentity.BootstrapScene);
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(ProjectIdentity.BootstrapScene));
            Assert.That(
                Object.FindAnyObjectByType<BootstrapFlow>(),
                Is.Null,
                "The retired portal must not be present in the classic main menu.");
        }

        [UnityTest]
        public IEnumerator CardLabSceneBootsCompleteDeckBuilder()
        {
            SceneManager.LoadScene(ProjectIdentity.CardLabScene);
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(ProjectIdentity.CardLabScene));
            Assert.That(Object.FindAnyObjectByType<CardLabController>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator DuelArenaHasIndependentAudioPresentation()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            ArcaneAudioDirector director =
                Object.FindAnyObjectByType<ArcaneAudioDirector>();
            Assert.That(director, Is.Not.Null);
            FieldInfo cardClips = typeof(ArcaneAudioDirector).GetField(
                "cardClips",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(cardClips, Is.Not.Null);
            var loaded = cardClips.GetValue(director) as
                IDictionary<ArcaneCardSound, AudioClip>;
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Count, Is.EqualTo(8));
            Assert.That(loaded[ArcaneCardSound.Fusion], Is.Not.Null);
            Assert.That(loaded[ArcaneCardSound.Synchro], Is.Not.Null);
            Assert.That(loaded[ArcaneCardSound.Xyz], Is.Not.Null);
            Assert.That(loaded[ArcaneCardSound.Magic], Is.Not.Null);
            Assert.That(loaded[ArcaneCardSound.Trap], Is.Not.Null);
            Assert.That(loaded[ArcaneCardSound.PutCard], Is.Not.Null);
            float originalVolume = ArcaneAudioPreferences.Volume;
            bool originalEnabled = ArcaneAudioPreferences.Enabled;
            director.Enabled = true;
            director.Volume = 0.25f;
            Assert.That(ArcaneAudioPreferences.Volume, Is.EqualTo(0.25f));
            Assert.That(
                director.GetComponents<AudioSource>()
                    .All(audio => audio.volume <= 0.25f + 0.001f),
                Is.True);
            float cueLength = director.PlayCardCue(
                ArcaneCardSound.MonsterSummon);
            Assert.That(cueLength, Is.GreaterThan(0f));
            yield return null;
            FieldInfo cardSourceField = typeof(ArcaneAudioDirector).GetField(
                "cardSource",
                BindingFlags.Instance | BindingFlags.NonPublic);
            AudioSource cardSource =
                cardSourceField?.GetValue(director) as AudioSource;
            Assert.That(cardSource, Is.Not.Null);
            Assert.That(cardSource.pitch, Is.EqualTo(1f));
            director.FadeOutCardCue(0.08f);
            yield return new WaitForSecondsRealtime(0.14f);
            Assert.That(cardSource.pitch, Is.EqualTo(1f));
            Assert.That(cardSource.isPlaying, Is.False);
            director.Volume = originalVolume;
            director.Enabled = originalEnabled;
        }

        [UnityTest]
        public IEnumerator DuelSceneBuildsProfessionalBattlePresentation()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;

            MonoBehaviour arena =
                Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .FirstOrDefault(component =>
                        component != null &&
                        component.gameObject.activeInHierarchy &&
                        component.GetType().Name ==
                        "CardArenaBootstrap");
            Assert.That(arena, Is.Not.Null);
            Assert.That(
                FindDescendant(
                    arena.transform,
                    "Navegador Profissional de Fases"),
                Is.Not.Null);
            Assert.That(
                FindDescendant(
                    arena.transform,
                    "Apresentação da Batalha"),
                Is.Not.Null);
            Assert.That(
                FindDescendant(
                    arena.transform,
                    "Anúncio de Turno e Fase"),
                Is.Not.Null);

            Assert.That(
                FindDescendant(arena.transform, "Orientação do Duelo"),
                Is.Not.Null);
            Assert.That(
                FindDescendant(arena.transform, "Mão do Oponente"),
                Is.Not.Null);
            Assert.That(
                FindDescendant(arena.transform, "Ações Recentes"),
                Is.Null,
                "The retired fixed action box must not pollute the arena.");
            Assert.That(
                FindDescendant(arena.transform, "Notificação de Ação"),
                Is.Not.Null);
            Assert.That(
                FindDescendant(arena.transform, "Indicador de Corrente"),
                Is.Not.Null);
            Transform hand =
                FindDescendant(
                    arena.transform,
                    "Mão do Jogador");
            if (hand != null && hand.childCount > 0)
            {
                for (int index = 0;
                     index < hand.childCount;
                     index++)
                {
                    Transform child = hand.GetChild(index);
                    if (!child.name.StartsWith("Hand_"))
                        continue;
                    Assert.That(
                        child.GetComponent<CanvasGroup>(),
                        Is.Not.Null,
                        "Every hand card needs a CanvasGroup for unobstructed drag presentation.");
                }
            }
        }

        [UnityTest]
        public IEnumerator PlayerHandRestsInsideTheLowerResponsiveViewportStrip()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena =
                Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .FirstOrDefault(component =>
                        component != null &&
                        component.gameObject.activeInHierarchy &&
                        component.GetType().Name == "CardArenaBootstrap");
            Assert.That(arena, Is.Not.Null);

            const System.Reflection.BindingFlags privateInstance =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            const System.Reflection.BindingFlags privateStatic =
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic;
            var handField = arena.GetType().GetField("handRoot", privateInstance);
            var frameField = arena.GetType().GetField("frame", privateInstance);
            var calculateMethod = arena.GetType().GetMethod(
                "CalculateHandRestY",
                privateStatic);
            Assert.That(handField, Is.Not.Null);
            Assert.That(frameField, Is.Not.Null);
            Assert.That(calculateMethod, Is.Not.Null);

            var hand = handField.GetValue(arena) as RectTransform;
            var frame = frameField.GetValue(arena) as RectTransform;
            Assert.That(hand, Is.Not.Null);
            Assert.That(frame, Is.Not.Null);
            Assert.That(hand.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(hand.anchorMax, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(hand.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));

            float expected = (float)calculateMethod.Invoke(
                null,
                new object[] { frame.rect.height });
            Assert.That(hand.anchoredPosition.y, Is.EqualTo(expected).Within(0.5f));
            Assert.That(
                hand.anchoredPosition.y,
                Is.LessThanOrEqualTo(-186f),
                "The resting fan must remain down in the lower marked strip, not over the card zones.");

            float smallViewport = (float)calculateMethod.Invoke(
                null,
                new object[] { 720f });
            float largeViewport = (float)calculateMethod.Invoke(
                null,
                new object[] { 1080f });
            Assert.That(smallViewport, Is.EqualTo(-216f).Within(0.01f));
            Assert.That(largeViewport, Is.GreaterThan(smallViewport));
            Assert.That(largeViewport, Is.LessThanOrEqualTo(-186f));
        }
        [UnityTest]
        public IEnumerator ExplicitSelectedDeckSnapshotReachesCore()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena =
                Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .FirstOrDefault(component =>
                    {
                        if (component == null ||
                            !component.gameObject.activeInHierarchy ||
                            component.GetType().Name != "CardArenaBootstrap")
                        {
                            return false;
                        }
                        var databaseField = component.GetType().GetField(
                            "database",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic);
                        return databaseField?.GetValue(component) != null;
                    });
            Assert.That(arena, Is.Not.Null);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);

            System.Type loadoutType = arena.GetType().Assembly.GetType(
                "ArcaneArena.Frontend.DuelDeckLoadout");
            Assert.That(loadoutType, Is.Not.Null);
            object loadout = System.Activator.CreateInstance(loadoutType);
            loadoutType.GetField("profileId")?.SetValue(loadout, "test-profile");
            loadoutType.GetField("deckId")?.SetValue(loadout, "test:selected-red-eyes");
            loadoutType.GetField("displayName")?.SetValue(loadout, "Deck Selecionado de Teste");
            loadoutType.GetField("mainDeckCardIds")?.SetValue(
                loadout,
                Enumerable.Repeat("74677422", 40).ToList());
            loadoutType.GetField("extraDeckCardIds")?.SetValue(
                loadout,
                new List<string>());

            arena.GetType().GetMethod("StartLocalTestDuel")?.Invoke(
                arena,
                new[] { loadout });
            yield return null;

            Assert.That(controller.PresentationState.Players[0].Hand,
                Has.Count.EqualTo(5));
            Assert.That(controller.PresentationState.Players[0].Hand,
                Has.All.EqualTo(74677422u),
                "The duel must use the exact selected loadout snapshot.");
        }

        [UnityTest]
        public IEnumerator EveryCompleteBotDeckStartsABotDuel()
        {
            for (int deckIndex = 0; deckIndex < 23; deckIndex++)
            {
                SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
                yield return null;
                yield return null;

                MonoBehaviour frontend =
                    Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                        .FirstOrDefault(component =>
                            component != null &&
                            component.gameObject.activeInHierarchy &&
                            component.GetType().Name == "GameFrontendBootstrap");
                Assert.That(frontend, Is.Not.Null);
                var showSelection = frontend.GetType().GetMethod(
                    "ShowBotDeckSelection",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(showSelection, Is.Not.Null);
                showSelection.Invoke(frontend, null);
                yield return null;

                Image[] deckTiles = Resources.FindObjectsOfTypeAll<Image>()
                    .Where(candidate =>
                        candidate != null &&
                        candidate.gameObject.activeInHierarchy &&
                        candidate.name.StartsWith(
                            "Deck do bot shop:",
                            System.StringComparison.Ordinal))
                    .OrderBy(candidate => candidate.name)
                    .ToArray();
                string validationDetails = string.Join(
                    " | ",
                    deckTiles.Select(tile =>
                        tile.name + ": " +
                        string.Join(
                            " / ",
                            tile.GetComponentsInChildren<Text>(true)
                                .Select(label => label.text))));
                Assert.That(
                    deckTiles,
                    Has.Length.EqualTo(23),
                    "The bot selection must display all twenty-three curated decks. " +
                    validationDetails);
                Button[] deckButtons = deckTiles
                    .Select(tile => tile.GetComponent<Button>())
                    .ToArray();
                Assert.That(
                    deckButtons,
                    Has.All.Not.Null,
                    "Every curated bot deck must pass validation and be clickable. " +
                    validationDetails);
                Button deckButton = deckButtons[deckIndex];
                Assert.That(deckButton.interactable, Is.True);
                string selectedDeckName = deckButton.name;

                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(
                        null,
                        deckButton.GetComponent<RectTransform>()
                            .TransformPoint(
                                deckButton.GetComponent<RectTransform>().rect.center))
                };
                GameObject receiver = ExecuteEvents.ExecuteHierarchy(
                    deckButton.gameObject,
                    pointer,
                    ExecuteEvents.pointerClickHandler);
                Assert.That(receiver, Is.Not.Null);

                for (int frameIndex = 0; frameIndex < 8; frameIndex++)
                    yield return null;

                Assert.That(
                    SceneManager.GetActiveScene().name,
                    Is.EqualTo("DuelArena"),
                    selectedDeckName +
                    " must transition directly into the authored duel arena.");
                DuelArenaController controller =
                    Object.FindAnyObjectByType<DuelArenaController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.PresentationState, Is.Not.Null);
                Assert.That(controller.CurrentPrompt, Is.Not.Null);
            }
        }
        private static Transform FindDescendant(
            Transform parent,
            string objectName)
        {
            if (parent == null)
                return null;
            if (parent.name == objectName)
                return parent;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform found =
                    FindDescendant(
                        parent.GetChild(index),
                        objectName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
