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
            FieldInfo magicGain = typeof(ArcaneAudioDirector).GetField(
                "MagicSoundGain",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(magicGain, Is.Not.Null);
            Assert.That(
                (float)magicGain.GetRawConstantValue(),
                Is.EqualTo(0.75f),
                "MagicSound must be 25% quieter than its balanced base gain.");
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

            Transform guidanceRibbon = FindDescendant(
                arena.transform,
                "Orientação do Duelo");
            Assert.That(guidanceRibbon, Is.Not.Null);
            Assert.That(
                guidanceRibbon.gameObject.activeSelf,
                Is.False,
                "Passive priority/core messages must stay hidden over the duel field.");
            MethodInfo updateGuidance = arena.GetType().GetMethod(
                "UpdateDecisionRibbon",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updateGuidance, Is.Not.Null);
            updateGuidance.Invoke(
                arena,
                new object[]
                {
                    "Nenhuma resposta legal disponível.",
                    Color.cyan
                });
            Assert.That(
                guidanceRibbon.gameObject.activeSelf,
                Is.False,
                "A new core prompt must not reactivate the retired ribbon.");
            Assert.That(
                FindDescendant(
                    arena.transform,
                    "POSICAO DA MAO DO OPONENTE"),
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
                    "POSICAO DA MAO DO JOGADOR");
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
        public IEnumerator DuelSceneBuildsFiveCardConfirmationTrays()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;

            MonoBehaviour arena =
                Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .FirstOrDefault(component =>
                        component != null &&
                        component.gameObject.activeInHierarchy &&
                        component.GetType().Name == "CardArenaBootstrap");
            Assert.That(arena, Is.Not.Null);
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.NonPublic;
            System.Type type = arena.GetType();
            Sprite template = type.GetField(
                    "choiceSelectionTemplate",
                    flags)
                ?.GetValue(arena) as Sprite;
            ScrollRect promptScroll = type.GetField(
                    "choiceScroll",
                    flags)
                ?.GetValue(arena) as ScrollRect;
            ScrollRect zoneScroll = type.GetField(
                    "zoneBrowserScroll",
                    flags)
                ?.GetValue(arena) as ScrollRect;
            Button zoneConfirm = type.GetField(
                    "zoneBrowserConfirm",
                    flags)
                ?.GetValue(arena) as Button;
            RectTransform zoneTray = type.GetField(
                    "zoneBrowserTray",
                    flags)
                ?.GetValue(arena) is GameObject zoneTrayObject
                    ? zoneTrayObject.GetComponent<RectTransform>()
                    : null;
            Text choiceInstruction = type.GetField(
                    "choiceInstruction",
                    flags)
                ?.GetValue(arena) as Text;
            FieldInfo visibleLimit = type.GetField(
                "MaximumVisibleChoiceCards",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(template, Is.Not.Null);
            Assert.That(promptScroll, Is.Not.Null);
            Assert.That(promptScroll.horizontal, Is.True);
            Assert.That(promptScroll.vertical, Is.False);
            Assert.That(zoneScroll, Is.Not.Null);
            Assert.That(zoneScroll.horizontal, Is.True);
            Assert.That(zoneScroll.vertical, Is.False);
            Assert.That(zoneConfirm, Is.Not.Null);
            Assert.That(zoneConfirm.interactable, Is.False);
            Assert.That(zoneTray, Is.Not.Null);
            Assert.That(
                (zoneTray.anchorMin.x + zoneTray.anchorMax.x) * 0.5f,
                Is.EqualTo(0.5f).Within(0.001f),
                "The Extra Deck tray must use the real screen centre.");
            Assert.That(choiceInstruction, Is.Not.Null,
                "Blocking choices need persistent visual instructions.");
            Assert.That(visibleLimit, Is.Not.Null);
            Assert.That(visibleLimit.GetRawConstantValue(), Is.EqualTo(5));
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
        public IEnumerator EveryLegalBotDeckStartsAndIllegalDecksAreDisabled()
        {
            SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            yield return null;
            yield return null;

            MonoBehaviour initialFrontend =
                Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .FirstOrDefault(component =>
                        component != null &&
                        component.gameObject.activeInHierarchy &&
                        component.GetType().Name == "GameFrontendBootstrap");
            Assert.That(initialFrontend, Is.Not.Null);
            ConfigureFrontendWithStarterDeck(initialFrontend);
            var initialSelection = initialFrontend.GetType().GetMethod(
                "ShowBotDeckSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            initialSelection.Invoke(initialFrontend, null);
            yield return null;

            Image[] initialTiles = Resources.FindObjectsOfTypeAll<Image>()
                .Where(candidate =>
                    candidate != null &&
                    candidate.gameObject.activeInHierarchy &&
                    candidate.name.StartsWith(
                        "Deck do bot shop:",
                        System.StringComparison.Ordinal))
                .OrderBy(candidate => candidate.name)
                .ToArray();
            Assert.That(initialTiles, Has.Length.EqualTo(23));
            string[] legalDeckNames = initialTiles
                .Where(tile => tile.GetComponent<Button>() != null)
                .Select(tile => tile.name)
                .ToArray();
            Assert.That(legalDeckNames, Has.Length.EqualTo(16));
            Assert.That(initialTiles.Count(tile =>
                tile.GetComponent<Button>() == null), Is.EqualTo(7));

            foreach (string legalDeckName in legalDeckNames)
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
                ConfigureFrontendWithStarterDeck(frontend);
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
                Assert.That(
                    deckTiles,
                    Has.Length.EqualTo(23));
                Button deckButton = deckTiles
                    .First(tile => tile.name == legalDeckName)
                    .GetComponent<Button>();
                Assert.That(deckButton, Is.Not.Null);
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

        private static void ConfigureFrontendWithStarterDeck(
            MonoBehaviour frontend)
        {
            StarterDeckCatalog starterCatalog =
                Resources.Load<StarterDeckCatalog>(
                    "StarterDecks/StarterDeckCatalog");
            StarterDeckDefinition starter = starterCatalog.Decks
                .First(deck => deck != null && deck.IsPublishable);
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.NonPublic;
            object repository = frontend.GetType().GetField(
                    "_repository", flags)
                .GetValue(frontend);
            object state = repository.GetType().GetProperty("State")
                .GetValue(repository);
            System.Type deckType = frontend.GetType().Assembly.GetType(
                "ArcaneArena.Frontend.DeckRecord");
            object deck = System.Activator.CreateInstance(deckType);
            string deckId = "playmode-starter";
            deckType.GetField("deckId").SetValue(deck, deckId);
            deckType.GetField("displayName").SetValue(
                deck, starter.DisplayName);
            deckType.GetField("mainDeckCardIds").SetValue(
                deck, new List<string>(starter.MainDeck));
            deckType.GetField("extraDeckCardIds").SetValue(
                deck, new List<string>(starter.ExtraDeck));
            deckType.GetField("sideDeckCardIds").SetValue(
                deck, new List<string>());
            deckType.GetMethod("Normalize").Invoke(deck, null);

            var decks = (System.Collections.IList)state.GetType()
                .GetField("decks").GetValue(state);
            decks.Clear();
            decks.Add(deck);
            state.GetType().GetField("selectedDeckId")
                .SetValue(state, deckId);
            state.GetType().GetField("playerDisplayName")
                .SetValue(state, "Duelista PlayMode");
            state.GetType().GetField("starterDeckClaimed")
                .SetValue(state, true);

            var quantities = (System.Collections.IList)state.GetType()
                .GetField("cardQuantities").GetValue(state);
            quantities.Clear();
            System.Type quantityType = frontend.GetType().Assembly.GetType(
                "ArcaneArena.Frontend.CardQuantityRecord");
            foreach (var group in starter.MainDeck
                         .Concat(starter.ExtraDeck)
                         .GroupBy(cardId => cardId))
            {
                object quantity = System.Activator.CreateInstance(quantityType);
                quantityType.GetField("cardId").SetValue(quantity, group.Key);
                quantityType.GetField("quantity").SetValue(
                    quantity, group.Count());
                quantities.Add(quantity);
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
