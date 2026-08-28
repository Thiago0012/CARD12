using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using ArcaneDuel.Game.Accounts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class ArenaStabilizationPlayModeTests
    {
        private const uint DarkMagician = 46986414;
        private const uint BlueEyesWhiteDragon = 89631139;
        private const uint DarkMagicalCircle = 47222536;
        private const uint EffectVeiler = 97268402;
        private const uint Polymerization = 24094653;
        private const uint RelinquishedAnima = 94259633;
        private const uint FaceDown = 0xA;

        [Test]
        public void CombatPresentationDistinguishesBuffsTunersAndHiddenArrivals()
        {
            System.Type arenaType = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "ArcaneArena.CardArenaBootstrap",
                    false))
                .First(type => type != null);
            const BindingFlags staticPrivate =
                BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo statColor = arenaType.GetMethod(
                "CombatStatPresentationColor",
                staticPrivate);
            MethodInfo tunerType = arenaType.GetMethod(
                "IsTunerType",
                staticPrivate);
            MethodInfo worldCardVisible = arenaType.GetMethod(
                "IsPresentedWorldCardVisible",
                staticPrivate);
            Assert.That(statColor, Is.Not.Null);
            Assert.That(tunerType, Is.Not.Null);
            Assert.That(worldCardVisible, Is.Not.Null);

            Color enhanced = (Color)statColor.Invoke(
                null,
                new object[] { 2800, 800 });
            Color printed = (Color)statColor.Invoke(
                null,
                new object[] { 800, 800 });
            Assert.That(
                ColorUtility.ToHtmlStringRGB(enhanced),
                Is.EqualTo("52C3FF"));
            Assert.That(printed, Is.EqualTo(Color.white));
            Assert.That(
                (bool)tunerType.Invoke(null, new object[] { 0x1000U }),
                Is.True);
            Assert.That(
                (bool)tunerType.Invoke(null, new object[] { 0x20U }),
                Is.False);

            var zoneObject = new GameObject("Combat visibility test zone");
            System.Type zoneType = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "ArcaneArena.DuelZone3D",
                    false))
                .First(type => type != null);
            Component zone = zoneObject.AddComponent(zoneType);
            Transform presentationAnchor = zoneType
                .GetProperty("CardPresentationAnchor")
                ?.GetValue(zone) as Transform;
            Assert.That(presentationAnchor, Is.Not.Null);
            var card = new GameObject(
                "Carta Invocada",
                typeof(CanvasGroup));
            card.transform.SetParent(presentationAnchor, false);
            CanvasGroup group = card.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            Assert.That(
                (bool)worldCardVisible.Invoke(null, new object[] { zone }),
                Is.False,
                "ATK/DEF must remain hidden while the arriving card is hidden.");
            group.alpha = 1f;
            Assert.That(
                (bool)worldCardVisible.Invoke(null, new object[] { zone }),
                Is.True);
            Object.DestroyImmediate(zoneObject);
        }

        [UnityTest]
        public IEnumerator AuthoredArenaNeverConsumesThePlayersPromptsAutomatically()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            FieldInfo autoPlay = typeof(DuelArenaController).GetField(
                "autoPlay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(autoPlay, Is.Not.Null);
            Assert.That(
                autoPlay.GetValue(controller),
                Is.False,
                "The persisted debug AUTO setting must not consume player " +
                "decisions in the authored arena.");

            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator OptionalResponseUsesOneCompactBarPerRequest()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.NonPublic;
            arena.GetType()
                .GetMethod("SuppressAnnouncementBanner", flags)
                ?.Invoke(arena, null);
            yield return null;
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            controller.ConfigureNetworkReplica(0);
            FieldInfo replicaPrompt = typeof(DuelArenaController).GetField(
                "replicaPrompt",
                flags);
            DuelPrompt first = OptionalChainPrompt(7001);
            replicaPrompt?.SetValue(controller, first);
            arena.GetType()
                .GetMethod("ResetPromptPresentationIdentity", flags)
                ?.Invoke(arena, null);
            arena.GetType()
                .GetMethod("RefreshEverything", flags)
                ?.Invoke(arena, new object[] { true });

            GameObject compact = arena.GetType()
                .GetField("compactResponseBar", flags)
                ?.GetValue(arena) as GameObject;
            GameObject modal = arena.GetType()
                .GetField("choiceModal", flags)
                ?.GetValue(arena) as GameObject;
            Assert.That(compact, Is.Not.Null);
            Assert.That(compact.activeSelf, Is.True);
            Assert.That(modal?.activeSelf, Is.False);
            Image responseArtwork = arena.GetType()
                .GetField("compactResponseArtwork", flags)
                ?.GetValue(arena) as Image;
            Assert.That(responseArtwork, Is.Not.Null);
            Assert.That(responseArtwork.gameObject.activeSelf, Is.True,
                "A single public response must show a clickable card preview.");
            responseArtwork.GetComponent<Button>()?.onClick.Invoke();
            GameObject responseDetails = arena.GetType()
                .GetField("detailPanel", flags)
                ?.GetValue(arena) as GameObject;
            Assert.That(responseDetails?.activeSelf, Is.True,
                "Clicking the response preview must open its card text.");

            DuelPrompt repeatedSnapshot = OptionalChainPrompt(7001);
            replicaPrompt?.SetValue(controller, repeatedSnapshot);
            arena.GetType()
                .GetMethod("RefreshEverything", flags)
                ?.Invoke(arena, new object[] { true });
            DuelPrompt stillPresented = arena.GetType()
                .GetField("compactResponsePrompt", flags)
                ?.GetValue(arena) as DuelPrompt;
            Assert.That(
                stillPresented,
                Is.SameAs(first),
                "Um snapshot online do mesmo RequestId não pode reabrir a bandeja.");

            DuelChoice submitted = null;
            System.Action<DuelChoice> previousSubmit =
                DuelOnlineBridge.SubmitReplicaChoice;
            DuelOnlineBridge.SubmitReplicaChoice =
                choice => submitted = choice;
            try
            {
                Button pass = compact
                    .GetComponentsInChildren<Button>(true)
                    .First(button => button.name == "Passar");
                pass.onClick.Invoke();
                Assert.That(submitted, Is.Not.Null);
                Assert.That(
                    submitted.Label,
                    Is.EqualTo("Não responder"),
                    "Passing SELECT_CHAIN must use the explicit no-response choice.");
                Assert.That(compact.activeSelf, Is.False);
            }
            finally
            {
                DuelOnlineBridge.SubmitReplicaChoice = previousSubmit;
            }
        }

        [UnityTest]
        public IEnumerator FieldMonsterWaitsForExplicitEffectOrPositionChoice()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic;
            arena.GetType().GetMethod("SuppressAnnouncementBanner", flags)
                ?.Invoke(arena, null);
            yield return null;
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            controller.ConfigureNetworkReplica(0);
            arena.GetType().GetField("state", flags)
                ?.SetValue(arena, controller.PresentationState);
            controller.PresentationState.Apply(
                MoveIntoMonsterZone(EffectVeiler, 0));

            DuelPrompt prompt = FieldActionPrompt(7201);
            typeof(DuelArenaController).GetField("replicaPrompt", flags)
                ?.SetValue(controller, prompt);
            arena.GetType().GetMethod("ResetPromptPresentationIdentity", flags)
                ?.Invoke(arena, null);
            arena.GetType().GetMethod("RefreshEverything", flags)
                ?.Invoke(arena, new object[] { true });

            Component zone = FindZone("PlayerOne", "Monster", 0);
            Assert.That(zone, Is.Not.Null);
            DuelChoice submitted = null;
            System.Action<DuelChoice> previous =
                DuelOnlineBridge.SubmitReplicaChoice;
            DuelOnlineBridge.SubmitReplicaChoice = choice => submitted = choice;
            try
            {
                arena.GetType().GetMethod("HandleZoneClick", flags)
                    ?.Invoke(arena, new object[] { zone, 1 });
                GameObject menu = arena.GetType()
                    .GetField("fieldActionPanel", flags)
                    ?.GetValue(arena) as GameObject;
                Assert.That(menu, Is.Not.Null);
                Assert.That(menu.activeSelf, Is.True);
                Assert.That(submitted, Is.Null,
                    "Clicking a field card must not pick its first action.");

                Button[] buttons = menu.GetComponentsInChildren<Button>(true);
                Assert.That(buttons, Has.Length.EqualTo(2));
                string[] labels = buttons.Select(button =>
                        button.GetComponentInChildren<Text>().text)
                    .ToArray();
                Assert.That(labels, Does.Contain("MODO DEFESA"));
                Assert.That(labels, Does.Contain("ATIVAR EFEITO"));

                buttons.Single(button =>
                        button.GetComponentInChildren<Text>().text ==
                        "ATIVAR EFEITO")
                    .onClick.Invoke();
                Assert.That(submitted, Is.Not.Null);
                Assert.That(submitted.Label, Does.Contain("Ativar"));
                Assert.That(menu.activeSelf, Is.False);
            }
            finally
            {
                DuelOnlineBridge.SubmitReplicaChoice = previous;
            }
        }

        [UnityTest]
        public IEnumerator SingleHandEffectSelectionRequiresTrayConfirmation()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic;
            arena.GetType().GetMethod("SuppressAnnouncementBanner", flags)
                ?.Invoke(arena, null);
            yield return null;
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            controller.ConfigureNetworkReplica(0);
            arena.GetType().GetField("state", flags)
                ?.SetValue(arena, controller.PresentationState);
            controller.PresentationState.Players[0].Hand.Clear();
            controller.PresentationState.Players[0].HandInstances.Clear();
            controller.PresentationState.Apply(DrawEvent(0, EffectVeiler));

            DuelPrompt prompt = SingleHandSelectionPrompt(7301);
            typeof(DuelArenaController).GetField("replicaPrompt", flags)
                ?.SetValue(controller, prompt);
            arena.GetType().GetMethod("ResetPromptPresentationIdentity", flags)
                ?.Invoke(arena, null);
            arena.GetType().GetMethod("RefreshEverything", flags)
                ?.Invoke(arena, new object[] { true });

            GameObject tray = arena.GetType().GetField("choiceModal", flags)
                ?.GetValue(arena) as GameObject;
            Assert.That(tray, Is.Not.Null);
            Assert.That(tray.activeSelf, Is.True,
                "A escolha de um Dragao da mao deve abrir a bandeja.");

            DuelChoice submitted = null;
            System.Action<DuelChoice> previous =
                DuelOnlineBridge.SubmitReplicaChoice;
            DuelOnlineBridge.SubmitReplicaChoice = choice => submitted = choice;
            try
            {
                Button card = arena.GetType().GetField("choiceContent", flags)
                    ?.GetValue(arena) is RectTransform content
                    ? content.GetChild(0).GetComponent<Button>()
                    : null;
                Assert.That(card, Is.Not.Null);
                card.onClick.Invoke();
                Assert.That(submitted, Is.Null,
                    "Inspecting the candidate must not resolve the effect.");
                GameObject details = arena.GetType()
                    .GetField("detailPanel", flags)
                    ?.GetValue(arena) as GameObject;
                Assert.That(details?.activeSelf, Is.True,
                    "Clicking the candidate must open its card text and artwork.");
                GameObject zoom = arena.GetType()
                    .GetField("detailZoomOverlay", flags)
                    ?.GetValue(arena) as GameObject;
                Assert.That(zoom, Is.Not.Null,
                    "The duel inspector must expose the reusable zoom viewer.");
                Image detailArt = arena.GetType()
                    .GetField("detailArtwork", flags)
                    ?.GetValue(arena) as Image;
                Assert.That(detailArt, Is.Not.Null);
                GameObject cardActions = arena.GetType()
                    .GetField("actionPanel", flags)
                    ?.GetValue(arena) as GameObject;
                cardActions?.SetActive(true);
                detailArt.GetComponent<Button>()?.onClick.Invoke();
                Assert.That(zoom.activeSelf, Is.True,
                    "Clicking the inspected artwork must open zoom.");
                Assert.That(cardActions?.activeSelf, Is.False,
                    "Card actions must not overlap the enlarged inspector.");
                Image zoomArtwork = arena.GetType()
                    .GetField("detailZoomArtwork", flags)
                    ?.GetValue(arena) as Image;
                object zoomViewer = arena.GetType()
                    .GetField("detailZoomViewer", flags)
                    ?.GetValue(arena);
                Assert.That(zoomArtwork, Is.Not.Null);
                Assert.That(zoomViewer, Is.Not.Null);
                var scrollEvent = new PointerEventData(EventSystem.current)
                {
                    scrollDelta = Vector2.up
                };
                for (int step = 0; step < 20; step++)
                {
                    zoomViewer.GetType().GetMethod("OnScroll")
                        ?.Invoke(zoomViewer, new object[] { scrollEvent });
                }
                Assert.That(zoomArtwork.rectTransform.localScale.x,
                    Is.EqualTo(1.75f).Within(0.001f));
                arena.GetType().GetMethod("CloseDetailZoom", flags)
                    ?.Invoke(arena, null);

                Button confirm = arena.GetType().GetField("choiceConfirm", flags)
                    ?.GetValue(arena) as Button;
                Assert.That(confirm, Is.Not.Null);
                Assert.That(confirm.interactable, Is.True);
                confirm.onClick.Invoke();
                Assert.That(submitted, Is.Not.Null);
                Assert.That(submitted.CardCode, Is.EqualTo(EffectVeiler));
                Assert.That(tray.activeSelf, Is.False);
            }
            finally
            {
                DuelOnlineBridge.SubmitReplicaChoice = previous;
            }
        }

        [UnityTest]
        public IEnumerator TwoEffectTargetsReachTheCoreInOneConfirmedResponse()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic;
            arena.GetType().GetMethod("SuppressAnnouncementBanner", flags)
                ?.Invoke(arena, null);
            yield return null;

            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            controller.ConfigureNetworkReplica(0);
            DuelPrompt prompt = TwoTargetSelectionPrompt(7351);
            typeof(DuelArenaController).GetField("replicaPrompt", flags)
                ?.SetValue(controller, prompt);
            arena.GetType().GetMethod(
                    "ResetPromptPresentationIdentity",
                    flags)
                ?.Invoke(arena, null);
            arena.GetType().GetMethod("RefreshEverything", flags)
                ?.Invoke(arena, new object[] { true });

            GameObject tray = arena.GetType()
                .GetField("choiceModal", flags)
                ?.GetValue(arena) as GameObject;
            RectTransform content = arena.GetType()
                .GetField("choiceContent", flags)
                ?.GetValue(arena) as RectTransform;
            Button confirm = arena.GetType()
                .GetField("choiceConfirm", flags)
                ?.GetValue(arena) as Button;
            GameObject details = arena.GetType()
                .GetField("detailPanel", flags)
                ?.GetValue(arena) as GameObject;
            Assert.That(tray?.activeSelf, Is.True);
            Assert.That(content, Is.Not.Null);
            Assert.That(content.childCount, Is.EqualTo(2));
            Assert.That(confirm, Is.Not.Null);

            byte[] submitted = null;
            ulong submittedRequest = 0;
            System.Action<byte[], ulong> previous =
                DuelOnlineBridge.SubmitReplicaResponse;
            DuelOnlineBridge.SubmitReplicaResponse = (response, request) =>
            {
                submitted = response;
                submittedRequest = request;
            };
            try
            {
                content.GetChild(0).GetComponent<Button>().onClick.Invoke();
                content.GetChild(1).GetComponent<Button>().onClick.Invoke();
                Assert.That(details?.activeSelf, Is.True,
                    "Clicking a public candidate must open its persistent card details.");
                Assert.That(
                    tray.transform.GetSiblingIndex(),
                    Is.GreaterThan(details.transform.GetSiblingIndex()),
                    "The mandatory tray must remain above the detail panel.");
                Assert.That(confirm.interactable, Is.True);

                confirm.onClick.Invoke();

                Assert.That(submittedRequest, Is.EqualTo(7351));
                Assert.That(
                    submitted,
                    Is.EqualTo(CoreMessageDecoder.CardSelectionResponse(
                        new uint[] { 0, 1 })));
                Assert.That(tray.activeSelf, Is.False);
            }
            finally
            {
                DuelOnlineBridge.SubmitReplicaResponse = previous;
            }
        }

        [UnityTest]
        public IEnumerator AuthoredArenaBuildsSevenMonsterZonesPerPlayer()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            Component[] zones = Resources
                .FindObjectsOfTypeAll<Component>()
                .Where(component =>
                    component != null &&
                    component.gameObject.scene.IsValid() &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "DuelZone3D" &&
                    component.GetType().GetProperty("Kind")
                        ?.GetValue(component)?.ToString() == "Monster")
                .ToArray();
            for (int player = 0; player < 2; player++)
            {
                int[] indexes = zones
                    .Where(zone => System.Convert.ToInt32(
                        zone.GetType().GetProperty("Owner")
                            ?.GetValue(zone)) == player)
                    .Select(zone => System.Convert.ToInt32(
                        zone.GetType().GetProperty("ZoneIndex")
                            ?.GetValue(zone)))
                    .OrderBy(index => index)
                    .ToArray();
                Assert.That(
                    indexes,
                    Is.EqualTo(Enumerable.Range(0, 7).ToArray()));
            }
        }

        [UnityTest]
        public IEnumerator LinkMonsterHasAVisibleWorldCardInExtraMonsterZone()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            controller.PresentationState.Apply(
                MoveIntoMonsterZone(RelinquishedAnima, 5));
            arena.GetType()
                .GetMethod(
                    "ReconcileField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, null);
            yield return null;

            Component zone = FindZone("PlayerOne", "Monster", 5);
            Assert.That(zone, Is.Not.Null);
            Transform worldCard = FindPresentedCard(zone);
            Assert.That(worldCard, Is.Not.Null);
            Transform front = worldCard.Find("Frente");
            Assert.That(front, Is.Not.Null);
            Assert.That(front.gameObject.activeSelf, Is.True);
            Assert.That(
                front.GetComponent<UnityEngine.UI.Image>().sprite,
                Is.Not.Null);
            MethodInfo formatMarkers = arena.GetType().GetMethod(
                "FormatLinkMarkers",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(formatMarkers, Is.Not.Null);
            Assert.That(
                formatMarkers.Invoke(null, new object[] { 0x1EFU }),
                Is.EqualTo("↙↓↘←→↖↑↗"),
                "All Core Link Marker bits must have a clear visual arrow; this mapping must not infer legal zones.");
        }

        [UnityTest]
        public IEnumerator PhaseNavigatorClosesCardInspectorAndActions()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            for (int frame = 0;
                 frame < 600 &&
                 controller.CurrentPrompt == null;
                 frame++)
            {
                yield return null;
            }
            Assert.That(controller.CurrentPrompt, Is.Not.Null);
            arena.GetType()
                .GetMethod("PrepareCaptureState", flags)
                ?.Invoke(arena, new object[] { "inspector" });
            GameObject actions = arena.GetType()
                .GetField("actionPanel", flags)
                ?.GetValue(arena) as GameObject;
            GameObject details = arena.GetType()
                .GetField("detailPanel", flags)
                ?.GetValue(arena) as GameObject;
            GameObject phases = arena.GetType()
                .GetField("phaseNavigator", flags)
                ?.GetValue(arena) as GameObject;
            Assert.That(actions, Is.Not.Null);
            Assert.That(details, Is.Not.Null);
            Assert.That(phases, Is.Not.Null);
            arena.GetType()
                .GetMethod("SuppressAnnouncementBanner", flags)
                ?.Invoke(arena, null);
            foreach (string fieldName in new[]
                     {
                         "criticalInteractionLocked",
                         "phasePresentationLocked",
                         "cardPresentationDecisionLocked"
                     })
            {
                arena.GetType().GetField(fieldName, flags)?.SetValue(arena, false);
            }
            controller.SetPresentationDecisionLocked(false);
            actions.SetActive(true);
            Assert.That(details.activeSelf, Is.True);
            bool interactionLocked = (bool)arena.GetType()
                .GetProperty("InteractionLocked", flags)
                .GetValue(arena);
            Assert.That(interactionLocked, Is.False,
                "The phase navigator fixture must not be animation-locked.");

            arena.GetType()
                .GetMethod("OpenPhaseNavigator", flags)
                ?.Invoke(
                    arena,
                    new object[]
                    {
                        controller.CurrentPrompt,
                        new List<DuelChoice> { new DuelChoice() }
                    });
            Assert.That(phases.activeSelf, Is.True,
                "The phase navigator must open synchronously.");
            yield return null;

            Assert.That(phases.activeSelf, Is.True);
            Assert.That(actions.activeSelf, Is.False);
            Assert.That(details.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ModernPhaseControlKeepsTheAuthoredClickTarget()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            GameObject phasePanel = arena.GetType()
                .GetField("phaseControlPanel", flags)
                ?.GetValue(arena) as GameObject;
            Button phaseButton = arena.GetType()
                .GetField("phaseButton", flags)
                ?.GetValue(arena) as Button;

            Assert.That(phasePanel, Is.Not.Null);
            Assert.That(phaseButton, Is.Not.Null);
            Assert.That(phaseButton.name, Does.Contain("Avan"));
            Assert.That(
                phasePanel.GetComponentsInChildren<Button>(true).Length,
                Is.EqualTo(1),
                "The visual skin must not add a nested competing Button.");
            Assert.That(phaseButton.targetGraphic, Is.Not.Null);
            Assert.That(
                phaseButton.targetGraphic.raycastTarget,
                Is.True,
                "The authored phase Button must remain the raycast owner.");

            Component modernSurface = phasePanel
                .GetComponentsInChildren<Component>(true)
                .FirstOrDefault(component =>
                    component != null &&
                    component.GetType().Name == "DuelPhaseControlGraphic");
            Assert.That(modernSurface, Is.Not.Null);
            Assert.That(
                ((Graphic)modernSurface).raycastTarget,
                Is.False,
                "The decorative phase skin must never consume input.");
        }

        [UnityTest]
        public IEnumerator SetSpellTrapUsesTheCardBackAndHidesItsFace()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            controller.PresentationState.Apply(
                MoveEvent(
                    DarkMagicalCircle,
                    0,
                    0,
                    0,
                    0,
                    (byte)DuelLocation.SpellTrapZone,
                    0,
                    FaceDown));

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic;
            arena.GetType()
                .GetMethod("ReconcileField", flags)
                ?.Invoke(arena, null);
            yield return null;

            Component zone = FindZone(
                "PlayerOne",
                "SpellTrap",
                0);
            Assert.That(zone, Is.Not.Null);
            Transform card = FindPresentedCard(zone);
            Assert.That(card, Is.Not.Null);
            Transform front = card.Find("Frente");
            Transform back = card.Find("Verso");
            Assert.That(front, Is.Not.Null);
            Assert.That(back, Is.Not.Null);
            Assert.That(front.gameObject.activeSelf, Is.False);
            Assert.That(back.gameObject.activeSelf, Is.True);
            Assert.That(
                zone.GetType().GetProperty("IsFaceUp")?.GetValue(zone),
                Is.False);
            Component view = card.GetComponents<Component>()
                .First(component =>
                    component.GetType().Name ==
                    "WorldCardInstanceView");
            Assert.That(
                view.GetType().GetProperty("IsFaceUp")?.GetValue(view),
                Is.False);
        }

        [UnityTest]
        public IEnumerator HiddenOpponentWorldViewIsNotTreatedAsAnEmptyZone()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Component zone = FindZone("PlayerTwo", "Monster", 0);
            Assert.That(arena, Is.Not.Null);
            Assert.That(zone, Is.Not.Null);
            Transform anchor = zone.GetType()
                .GetProperty("CardPresentationAnchor")
                ?.GetValue(zone) as Transform;
            Assert.That(anchor, Is.Not.Null);

            var card = new GameObject("Carta Invocada");
            card.transform.SetParent(anchor, false);
            new GameObject("Frente").transform.SetParent(card.transform, false);
            new GameObject("Verso").transform.SetParent(card.transform, false);
            System.Type viewType = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "ArcaneArena.WorldCardInstanceView",
                    false))
                .First(type => type != null);
            Component hiddenView = card.AddComponent(viewType);
            var key = new CardInstanceKey(
                0xE000000000000101UL,
                0,
                1,
                1,
                (byte)DuelLocation.MonsterZone,
                0,
                FaceDown);
            viewType.GetMethod("Bind")?.Invoke(
                hiddenView,
                new object[] { key, false });

            MethodInfo validateView = arena.GetType().GetMethod(
                "HasWorldCardRepresentation",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(validateView, Is.Not.Null);
            bool represented = (bool)validateView.Invoke(
                null,
                new object[] { zone, key, 0U, true });

            Assert.That(
                represented,
                Is.True,
                "Uma carta oculta continua ocupando a zona e deve manter o verso 3D.");
            Object.Destroy(card);
        }

        [UnityTest]
        public IEnumerator CardDetailsRequireClickAndNeverRevealOpponentFaceDowns()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            DuelPresentationState state = controller.PresentationState;
            state.Apply(MoveEvent(
                DarkMagician,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                4,
                0x1U));
            state.Apply(MoveEvent(
                BlueEyesWhiteDragon,
                1,
                0,
                0,
                1,
                (byte)DuelLocation.MonsterZone,
                4,
                FaceDown));

            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic;
            arena.GetType().GetMethod("ReconcileField", flags)
                ?.Invoke(arena, null);
            yield return null;

            Component localZone = FindZone("PlayerOne", "Monster", 4);
            Component opponentZone = FindZone("PlayerTwo", "Monster", 4);
            GameObject details = arena.GetType()
                .GetField("detailPanel", flags)
                ?.GetValue(arena) as GameObject;
            Assert.That(localZone, Is.Not.Null);
            Assert.That(opponentZone, Is.Not.Null);
            Assert.That(details, Is.Not.Null);

            MethodInfo close = arena.GetType().GetMethod(
                "CloseCardDetails",
                flags);
            MethodInfo hover = arena.GetType().GetMethod(
                "HandleZoneHover",
                flags);
            MethodInfo inspect = arena.GetType().GetMethod(
                "InspectZone",
                flags);
            close?.Invoke(arena, null);
            hover?.Invoke(arena, new object[] { localZone, true });
            Assert.That(
                details.activeSelf,
                Is.False,
                "Hover must not open persistent card details.");

            inspect?.Invoke(arena, new object[] { localZone });
            Assert.That(
                details.activeSelf,
                Is.True,
                "Click inspection of a public card must open details.");

            close?.Invoke(arena, null);
            inspect?.Invoke(arena, new object[] { opponentZone });
            Assert.That(
                details.activeSelf,
                Is.False,
                "An opponent face-down card must remain opaque to the host and guest UI.");
        }

        [UnityTest]
        public IEnumerator EveryArenaZoneCanBeHoveredWithoutNullReference()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            MethodInfo pileLabel =
                arena.GetType().GetMethod("PileLabel", flags);
            MethodInfo hover =
                arena.GetType().GetMethod("HandleZoneHover", flags);
            Assert.That(pileLabel, Is.Not.Null);
            Assert.That(hover, Is.Not.Null);
            Text status = arena.GetType().GetField("status", flags)
                ?.GetValue(arena) as Text;
            Assert.That(status, Is.Not.Null);
            const string untouchedStatus = "STATUS NÃO ALTERADO POR HOVER";
            status.text = untouchedStatus;

            Component[] zones = Resources
                .FindObjectsOfTypeAll<Component>()
                .Where(component =>
                    component != null &&
                    component.gameObject.scene.IsValid() &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "DuelZone3D")
                .ToArray();
            Assert.That(zones.Length, Is.GreaterThan(20));
            foreach (Component zone in zones)
            {
                string label =
                    pileLabel.Invoke(arena, new object[] { zone }) as string;
                Assert.That(
                    label,
                    Is.Not.Null.And.Not.Empty,
                    zone.gameObject.name);
                Assert.DoesNotThrow(() =>
                    hover.Invoke(arena, new object[] { zone, true }));
                Assert.DoesNotThrow(() =>
                    hover.Invoke(arena, new object[] { zone, false }));
            }
            Assert.That(
                status.text,
                Is.EqualTo(untouchedStatus),
                "Passar o mouse pelos slots e montes não deve escrever mensagens no ribbon.");
            Assert.That(
                pileLabel.Invoke(arena, new object[] { null }),
                Is.EqualTo("Zona indisponivel"));

            System.Type zoneType = zones[0].GetType();
            var orphan = new GameObject("Decorative Unbound Zone");
            Component orphanZone = orphan.AddComponent(zoneType);
            MethodInfo enter = zoneType.GetMethod("OnPointerEnter", flags);
            MethodInfo exit = zoneType.GetMethod("OnPointerExit", flags);
            Assert.DoesNotThrow(() =>
                enter.Invoke(orphanZone, new object[] { null }));
            Assert.DoesNotThrow(() =>
                exit.Invoke(orphanZone, new object[] { null }));
            Object.Destroy(orphan);
        }

        [UnityTest]
        public IEnumerator MissingAuthoritativeWorldCardIsRepairedBeforeUse()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            controller.PresentationState.Apply(
                MoveIntoMonsterZone(DarkMagician, 0));

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic;
            MethodInfo reconcile =
                arena.GetType().GetMethod("ReconcileField", flags);
            MethodInfo validate =
                arena.GetType().GetMethod(
                    "ValidatePresentationConsistency",
                    flags);
            Assert.That(reconcile, Is.Not.Null);
            Assert.That(validate, Is.Not.Null);
            reconcile.Invoke(arena, null);
            yield return null;

            Component zone = FindZone(
                "PlayerOne",
                "Monster",
                0);
            Assert.That(zone, Is.Not.Null);
            Transform card = FindPresentedCard(zone);
            Assert.That(card, Is.Not.Null);
            AssertWorldViewMatches(
                card,
                controller.PresentationState
                    .Players[0]
                    .MonsterInstances[0]
                    .RuntimeId);

            Object.Destroy(card.gameObject);
            yield return null;
            Assert.That(
                FindPresentedCard(zone),
                Is.Null);

            string[] problems = validate.Invoke(
                arena,
                new object[] { null, true }) as string[];
            Assert.That(problems, Is.Not.Null);
            Assert.That(
                problems.Any(problem =>
                    problem.Contains("world view")),
                Is.True);
            yield return null;

            Transform repaired =
                FindPresentedCard(zone);
            Assert.That(
                repaired,
                Is.Not.Null,
                "The view must be recreated from authoritative state.");
            AssertWorldViewMatches(
                repaired,
                controller.PresentationState
                    .Players[0]
                    .MonsterInstances[0]
                    .RuntimeId);
        }

        [UnityTest]
        public IEnumerator DestroyedLocalMonsterDoesNotSurviveAsAWorldView()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.NonPublic;
            MethodInfo reconcile = arena.GetType().GetMethod(
                "ReconcileField", flags);
            Assert.That(reconcile, Is.Not.Null);

            controller.PresentationState.Apply(
                MoveIntoMonsterZone(DarkMagician, 0));
            reconcile.Invoke(arena, null);
            yield return null;

            Component zone = FindZone("PlayerOne", "Monster", 0);
            Assert.That(zone, Is.Not.Null);
            Assert.That(FindPresentedCard(zone), Is.Not.Null);

            controller.PresentationState.Apply(MoveEvent(
                DarkMagician,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                0,
                (byte)DuelLocation.Graveyard,
                0,
                1,
                0x1U));
            reconcile.Invoke(arena, null);
            yield return null;

            Assert.That(
                controller.PresentationState.Players[0].MonsterZones[0],
                Is.EqualTo(0U));
            Assert.That(
                FindPresentedCard(zone),
                Is.Null,
                "A carta destruída não pode continuar visível somente para " +
                "o jogador local depois de o Core confirmar o cemitério.");
        }

        [UnityTest]
        public IEnumerator PassiveRefreshRepairsAStaleWorldCardWithoutANewEvent()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            Component zone = FindZone("PlayerOne", "Monster", 4);
            Assert.That(zone, Is.Not.Null);
            Assert.That(
                controller.PresentationState.Players[0].MonsterZones[4],
                Is.EqualTo(0U),
                "O cenário de recuperação exige uma zona autoritativamente vazia.");

            Transform anchor = zone.GetType()
                .GetProperty("CardPresentationAnchor")
                ?.GetValue(zone) as Transform;
            Assert.That(anchor, Is.Not.Null);
            var stale = new GameObject("Carta Invocada");
            stale.transform.SetParent(anchor, false);
            Assert.That(FindPresentedCard(zone), Is.Not.Null);

            arena.GetType().GetMethod(
                    "RefreshEverything",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, new object[] { false });
            yield return null;

            Assert.That(
                FindPresentedCard(zone),
                Is.Null,
                "A verificação passiva deve limpar uma View órfã mesmo sem " +
                "receber um novo evento do Core.");
        }

        [UnityTest]
        public IEnumerator EquipRelationshipUsesATacticalLineWithoutDebugText()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            DuelPresentationState state = controller.PresentationState;
            state.Apply(MoveEvent(
                DarkMagicalCircle,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.SpellTrapZone,
                0,
                1));
            state.Apply(MoveIntoMonsterZone(DarkMagician, 0));
            state.Apply(EquipEvent(
                0,
                (byte)DuelLocation.SpellTrapZone,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                0));

            CardInstanceState equipped = state.Players[0]
                .SpellTrapInstances[0];
            Assert.That(equipped, Is.Not.Null);
            Assert.That(equipped.EquippedToRuntimeId, Is.Not.EqualTo(0UL),
                "A limpeza visual não pode remover o vínculo mecânico.");

            arena.GetType().GetMethod(
                    "ReconcileField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, null);
            MethodInfo refreshRelations = arena.GetType().GetMethod(
                "RefreshFieldRelationPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshRelations, Is.Not.Null);
            refreshRelations.Invoke(arena, new object[] { true });
            yield return null;

            Component zone = FindZone("PlayerOne", "SpellTrap", 0);
            Transform card = FindPresentedCard(zone);
            Assert.That(card, Is.Not.Null);
            Text indicator = card.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.gameObject.name ==
                    "Indicadores de Campo" ||
                    text.gameObject.name == "Estado do Core");
            Assert.That(
                indicator == null || !indicator.gameObject.activeSelf ||
                !indicator.text.Contains("EQUIP"),
                Is.True,
                "O vínculo de equipamento não deve virar texto técnico sobre a arte.");

            Transform relationRoot = arena.transform.Find(
                "Conexões táticas do campo");
            Assert.That(relationRoot, Is.Not.Null);
            Assert.That(
                relationRoot.GetComponentsInChildren<LineRenderer>(true)
                    .Count(line => line.gameObject.activeInHierarchy),
                Is.EqualTo(1),
                "Um equipamento público deve ser apresentado como uma conexão " +
                "tática entre as duas cartas do campo.");

            state.Apply(MoveEvent(
                DarkMagicalCircle,
                0,
                (byte)DuelLocation.SpellTrapZone,
                0,
                0,
                (byte)DuelLocation.Graveyard,
                0,
                1));
            arena.GetType().GetMethod(
                    "ReconcileField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, null);
            refreshRelations.Invoke(arena, new object[] { true });
            yield return null;
            Assert.That(
                relationRoot.GetComponentsInChildren<LineRenderer>(true)
                    .Count(line => line.gameObject.activeInHierarchy),
                Is.EqualTo(0),
                "A conexão precisa desaparecer quando uma das cartas deixa o campo.");
        }

        [UnityTest]
        public IEnumerator ConsecutivePileUpdatesKeepTheLatestAuthoritativeView()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic;
            MethodInfo reconcile =
                arena.GetType().GetMethod("ReconcileField", flags);
            Assert.That(reconcile, Is.Not.Null);

            controller.PresentationState.Apply(
                MoveEvent(
                    DarkMagician,
                    0,
                    0,
                    0,
                    0,
                    (byte)DuelLocation.Graveyard,
                    0,
                    1));
            reconcile.Invoke(arena, null);

            controller.PresentationState.Apply(
                MoveEvent(
                    EffectVeiler,
                    0,
                    0,
                    0,
                    0,
                    (byte)DuelLocation.Graveyard,
                    1,
                    1));
            reconcile.Invoke(arena, null);

            Component zone = FindZone(
                "PlayerOne",
                "Graveyard",
                0);
            Assert.That(zone, Is.Not.Null);
            Transform card = FindPresentedCard(zone);
            Assert.That(
                card,
                Is.Not.Null,
                "A deferred Destroy cannot hide the replacement pile view.");
            AssertWorldViewMatches(
                card,
                controller.PresentationState
                    .Players[0]
                    .GraveyardInstances[1]
                    .RuntimeId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrivateDeckPilesDoNotRequireIndividualWorldCardViews()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            MethodInfo validate = arena.GetType().GetMethod(
                "ValidatePresentationConsistency",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(validate, Is.Not.Null);

            string[] problems = validate.Invoke(
                arena,
                new object[] { null, false }) as string[];
            Assert.That(problems, Is.Not.Null);
            Assert.That(
                problems.Any(problem =>
                    problem.Contains("main_deck") ||
                    problem.Contains("extra_deck")),
                Is.False,
                "Private Deck/Extra Deck piles are count-backed proxies, " +
                "not one public WorldCardInstanceView per hidden card.");
        }

        [UnityTest]
        public IEnumerator DuplicateSelectionCancelAndSecondSummonStayIndependent()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            DuelPresentationState state =
                controller.PresentationState;
            state.Players[0].Hand.Clear();
            state.Players[0].HandInstances.Clear();
            state.Apply(DrawEvent(0, EffectVeiler, EffectVeiler));
            CardInstanceState first =
                state.Players[0].HandInstances[0];
            CardInstanceState second =
                state.Players[0].HandInstances[1];
            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            arena.GetType()
                .GetMethod("RebuildHand", flags)
                ?.Invoke(arena, null);
            yield return null;
            Component secondView = Resources
                .FindObjectsOfTypeAll<Component>()
                .First(component =>
                    component != null &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "CardView" &&
                    RuntimeIdOf(component) == second.RuntimeId);
            object keyBefore = secondView.GetType()
                .GetProperty("InstanceKey")
                ?.GetValue(secondView);
            secondView.GetType()
                .GetMethod("SetHandOrder", flags)
                ?.Invoke(secondView, new object[] { 7 });
            object keyAfter = secondView.GetType()
                .GetProperty("InstanceKey")
                ?.GetValue(secondView);
            Assert.That(RuntimeIdOf(secondView), Is.EqualTo(second.RuntimeId));
            Assert.That(
                keyAfter?.GetType()
                    .GetProperty("Sequence")
                    ?.GetValue(keyAfter),
                Is.EqualTo(
                    keyBefore?.GetType()
                        .GetProperty("Sequence")
                        ?.GetValue(keyBefore)),
                "Visual hand reordering cannot rewrite the Core address.");
            DuelPrompt prompt = DuplicateIdlePrompt();

            DuelChoice firstAction =
                CoreCardActionBinding.FirstChoiceFor(
                    prompt,
                    first.Key);
            Assert.That(firstAction, Is.Not.Null);
            // Cancelling locally sends no response and changes no binding.
            Assert.That(
                state.Players[0].HandInstances[0].RuntimeId,
                Is.EqualTo(first.RuntimeId));
            DuelChoice secondAction =
                CoreCardActionBinding.FirstChoiceFor(
                    prompt,
                    second.Key);
            Assert.That(secondAction, Is.Not.Null);
            Assert.That(secondAction.Sequence, Is.EqualTo(1));

            state.Apply(
                MoveEvent(
                    EffectVeiler,
                    0,
                    (byte)DuelLocation.Hand,
                    secondAction.Sequence,
                    0,
                    (byte)DuelLocation.MonsterZone,
                    0,
                    1));

            arena.GetType()
                .GetMethod("RebuildHand", flags)
                ?.Invoke(arena, null);
            arena.GetType()
                .GetMethod("ReconcileField", flags)
                ?.Invoke(arena, null);
            yield return null;

            Assert.That(state.Players[0].HandInstances, Has.Count.EqualTo(1));
            Assert.That(
                state.Players[0].HandInstances[0].RuntimeId,
                Is.EqualTo(first.RuntimeId));
            Assert.That(
                state.Players[0].MonsterInstances[0].RuntimeId,
                Is.EqualTo(second.RuntimeId));
            Component zone = FindZone(
                "PlayerOne",
                "Monster",
                0);
            Transform worldCard =
                FindPresentedCard(zone);
            Assert.That(worldCard, Is.Not.Null);
            AssertWorldViewMatches(worldCard, second.RuntimeId);

            Component[] handCards = Resources
                .FindObjectsOfTypeAll<Component>()
                .Where(component =>
                    component != null &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "CardView")
                .ToArray();
            Assert.That(handCards, Has.Length.EqualTo(1));
            object remainingKey = handCards[0].GetType()
                .GetProperty("InstanceKey")
                ?.GetValue(handCards[0]);
            Assert.That(
                remainingKey?.GetType()
                    .GetProperty("RuntimeId")
                    ?.GetValue(remainingKey),
                Is.EqualTo(first.RuntimeId));
        }

        [UnityTest]
        public IEnumerator LegalCardGlowPulsesWithSummonAndEffectColors()
        {
            var cardObject = new GameObject(
                "Legal glow test",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            System.Type cardViewType = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("ArcaneArena.CardView"))
                .First(type => type != null);
            System.Type arenaType = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly =>
                    assembly.GetType("ArcaneArena.CardArenaBootstrap"))
                .First(type => type != null);
            var normalSummon = new DuelChoice();
            typeof(DuelChoice).GetProperty("Label")
                ?.SetValue(normalSummon, "Invocar");
            MethodInfo normalSummonClassifier = arenaType.GetMethod(
                "IsNormalSummonChoice",
                BindingFlags.Static |
                BindingFlags.NonPublic);
            Assert.That(
                normalSummonClassifier?.Invoke(
                    null,
                    new object[] { normalSummon }),
                Is.True,
                "A Invocação-Normal deve receber o contorno azul.");
            var specialSummon = new DuelChoice();
            typeof(DuelChoice).GetProperty("Label")
                ?.SetValue(specialSummon, "Invocação especial");
            MethodInfo specialSummonClassifier = arenaType.GetMethod(
                "IsSpecialSummonChoice",
                BindingFlags.Static |
                BindingFlags.NonPublic);
            Assert.That(
                specialSummonClassifier?.Invoke(
                    null,
                    new object[] { specialSummon }),
                Is.True,
                "A Invocação-Especial deve usar o contorno de efeito.");
            var activation = new DuelChoice();
            typeof(DuelChoice).GetProperty("Label")
                ?.SetValue(activation, "Ativar efeito");
            MethodInfo effectClassifier = arenaType.GetMethod(
                "IsEffectActivationChoice",
                BindingFlags.Static |
                BindingFlags.NonPublic);
            Assert.That(
                effectClassifier?.Invoke(
                    null,
                    new object[] { null, activation }),
                Is.True,
                "Ativações devem receber o contorno de efeito.");
            Component card = cardObject.AddComponent(cardViewType);
            MethodInfo setup = cardViewType.GetMethods()
                .First(method =>
                    method.Name == "Setup" &&
                    method.GetParameters().Length == 4 &&
                    method.GetParameters()[1].ParameterType == typeof(uint));
            setup.Invoke(card, new object[] { null, 12345678u, null, 0 });
            cardViewType.GetMethod("SetPresentationVisible")
                ?.Invoke(card, new object[] { true });
            Outline outline = cardObject.GetComponent<Outline>();

            MethodInfo singleGlow = cardViewType.GetMethods()
                .First(method =>
                    method.Name == "SetLegalActionGlow" &&
                    method.GetParameters().Length == 2);
            Color summonAccent = (Color)arenaType.GetField(
                "SummonBlue",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Color effectAccent = (Color)arenaType.GetField(
                "EffectGlow",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(ColorUtility.ToHtmlStringRGB(summonAccent), Is.EqualTo("52C3FF"));
            Assert.That(ColorUtility.ToHtmlStringRGB(effectAccent), Is.EqualTo("A0FF25"));
            singleGlow.Invoke(
                card,
                new object[]
                {
                    summonAccent,
                    true
                });
            yield return null;
            Color summonColor = outline.effectColor;
            Vector2 firstDistance = outline.effectDistance;
            Assert.That(summonColor.b, Is.GreaterThan(summonColor.r));
            yield return new WaitForSecondsRealtime(0.16f);
            Assert.That(
                outline.effectDistance,
                Is.Not.EqualTo(firstDistance),
                "O contorno legal deve pulsar, não ficar estático.");

            singleGlow.Invoke(
                card,
                new object[]
                {
                    effectAccent,
                    true
                });
            yield return null;
            Color effectColor = outline.effectColor;
            Assert.That(effectColor.g, Is.GreaterThan(effectColor.r));
            Assert.That(effectColor.g, Is.GreaterThan(effectColor.b));

            Object.Destroy(cardObject);
        }

        [UnityTest]
        public IEnumerator CardSoundsFollowCardCategoryAndSummonFrame()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            object catalog = arena.GetType()
                .GetProperty("CardCatalog")
                ?.GetValue(arena);
            Assert.That(catalog, Is.Not.Null);

            MethodInfo summonSound = arena.GetType().GetMethod(
                "SummonSoundFor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo activationSound = arena.GetType().GetMethod(
                "ActivationSoundFor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo faceDown = arena.GetType().GetMethod(
                "IsFaceDownPlacement",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(summonSound, Is.Not.Null);
            Assert.That(activationSound, Is.Not.Null);
            Assert.That(faceDown, Is.Not.Null);

            AssertSoundForFrame(
                arena,
                catalog,
                summonSound,
                5,
                ArcaneCardSound.Fusion);
            AssertSoundForFrame(
                arena,
                catalog,
                summonSound,
                6,
                ArcaneCardSound.Synchro);
            AssertSoundForFrame(
                arena,
                catalog,
                summonSound,
                7,
                ArcaneCardSound.Xyz);
            AssertSoundForFrame(
                arena,
                catalog,
                summonSound,
                8,
                ArcaneCardSound.MonsterSummon);
            AssertSoundForFrame(
                arena,
                catalog,
                summonSound,
                9,
                ArcaneCardSound.MonsterSummon);
            AssertSoundForCategory(
                arena,
                catalog,
                activationSound,
                2,
                ArcaneCardSound.Magic);
            AssertSoundForCategory(
                arena,
                catalog,
                activationSound,
                3,
                ArcaneCardSound.Trap);
            AssertSoundForCategory(
                arena,
                catalog,
                activationSound,
                1,
                ArcaneCardSound.Magic);

            DuelEvent setCard = MoveEvent(
                DarkMagician,
                0,
                (byte)DuelLocation.Hand,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                0x8);
            Assert.That(
                faceDown.Invoke(null, new object[] { setCard }),
                Is.True);
            arena.GetType().GetMethod(
                    "QueueCardSoundPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, new object[] { setCard });
            yield return null;
            Assert.That(
                arena.GetType().GetField(
                        "cardSoundPresentationRoutine",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(arena),
                Is.Null,
                "Carta baixada deve tocar o som sem abrir destaque na tela.");
            arena.GetType().GetMethod(
                    "ResetCardSoundPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, null);
        }

        [UnityTest]
        public IEnumerator SummonCutInWaitsForCoreConfirmationAndSkipsNegation()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            var presentationState = new DuelPresentationState(null);
            FieldInfo arenaState = arena.GetType().GetField(
                "state",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(arenaState, Is.Not.Null);
            arenaState.SetValue(arena, presentationState);
            MethodInfo create = arena.GetType().GetMethod(
                "CreateCardSoundPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo pending = arena.GetType().GetField(
                "pendingSummonPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(create, Is.Not.Null);
            Assert.That(pending, Is.Not.Null);

            DuelEvent placed = MoveIntoMonsterZone(DarkMagician, 0);
            presentationState.Apply(placed);
            DuelEvent attempt = SummonEvent(
                CoreMessage.Summoning,
                DarkMagician,
                0,
                0,
                0x1);
            presentationState.Apply(attempt);
            Assert.That(create.Invoke(arena, new object[] { attempt }), Is.Null);
            Assert.That(pending.GetValue(arena), Is.Not.Null,
                "The cut-in may be staged during the negation window, but it must not play yet.");

            DuelEvent confirmed = Decode(
                CoreMessage.Summoned,
                new List<byte>());
            presentationState.Apply(confirmed);
            Assert.That(create.Invoke(arena, new object[] { confirmed }),
                Is.Not.Null,
                "Only the Core confirmation releases the summon cut-in.");
            Assert.That(pending.GetValue(arena), Is.Null);

            presentationState.Apply(MoveIntoMonsterZone(EffectVeiler, 1));
            DuelEvent negatedAttempt = SummonEvent(
                CoreMessage.Summoning,
                EffectVeiler,
                0,
                1,
                0x1);
            presentationState.Apply(negatedAttempt);
            Assert.That(create.Invoke(arena, new object[] { negatedAttempt }), Is.Null);
            DuelEvent removed = MoveEvent(
                EffectVeiler,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                0,
                (byte)DuelLocation.Graveyard,
                0,
                0x1);
            presentationState.Apply(removed);
            Assert.That(create.Invoke(arena, new object[] { removed }), Is.Null);
            Assert.That(pending.GetValue(arena), Is.Null,
                "A negated summon must discard the staged cut-in.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtraDeckRequiresAuthoritativeLegalSummonForOutline()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            Component extra = FindZone("PlayerOne", "ExtraDeck", 0);
            Component grave = FindZone("PlayerOne", "Graveyard", 0);
            Assert.That(extra, Is.Not.Null);
            Assert.That(grave, Is.Not.Null);
            arena.enabled = false;

            var prompt = new DuelPrompt();
            typeof(DuelPrompt).GetProperty("Message")
                ?.SetValue(prompt, CoreMessage.SelectIdleCommand);
            typeof(DuelPrompt).GetProperty("Player")
                ?.SetValue(prompt, (byte)0);
            prompt.Choices.Add(LocatedChoice(
                "Invocação especial",
                (byte)DuelLocation.Extra));
            prompt.Choices.Add(LocatedChoice(
                "Ativar",
                (byte)DuelLocation.Graveyard));
            arena.GetType().GetMethod(
                    "HighlightPromptZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, new object[] { prompt });
            extra.GetType().GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(extra, null);
            grave.GetType().GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(grave, null);

            Transform extraStack = extra.transform.Find("Card Stack");
            Assert.That(extraStack, Is.Not.Null);
            LineRenderer extraOutline = extraStack
                .Find("Contorno de ação legal")
                ?.GetComponent<LineRenderer>();
            LineRenderer graveOutline = grave.transform
                .Find("Contorno de ação legal")
                ?.GetComponent<LineRenderer>();
            Assert.That(graveOutline, Is.Not.Null);
            if (extraOutline != null)
            {
                Assert.That(
                    extraOutline.enabled,
                    Is.False,
                    "Uma escolha sem resposta/comando autoritativo não pode iluminar o Deck Adicional.");
            }
            Assert.That(graveOutline.enabled, Is.True);
            Assert.That(graveOutline.loop, Is.True);
            Assert.That(graveOutline.startColor.g, Is.GreaterThan(0.75f));

            DuelPresentationState presentationState =
                (DuelPresentationState)arena.GetType().GetField(
                    "state",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(arena);
            Assert.That(presentationState, Is.Not.Null);
            List<uint> extraContents = typeof(DuelistState).GetProperty(
                    "ExtraDeckContents",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(presentationState.Players[0]) as List<uint>;
            Assert.That(extraContents, Is.Not.Null);
            extraContents.Clear();
            extraContents.Add(RelinquishedAnima);
            presentationState.Players[0].ExtraDeckInstances.Clear();

            prompt.Choices.Clear();
            prompt.Choices.Add(LocatedChoice(
                "Invocação especial",
                (byte)DuelLocation.Extra,
                RelinquishedAnima,
                new byte[] { 1, 0, 0, 0 }));
            arena.GetType().GetMethod(
                    "HighlightPromptZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, new object[] { prompt });
            extra.GetType().GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(extra, null);
            extraOutline = extraStack
                .Find("Contorno de ação legal")
                ?.GetComponent<LineRenderer>();
            Assert.That(extraOutline, Is.Not.Null);
            Assert.That(
                extraOutline.enabled,
                Is.True,
                "Uma Invocação-Especial autoritativa deve iluminar o Deck Adicional.");

            extraContents[0] = DeveloperAccountRegistry.MixaelCardCode;
            prompt.Choices.Clear();
            prompt.Choices.Add(LocatedChoice(
                "Invocação especial",
                (byte)DuelLocation.Extra,
                DeveloperAccountRegistry.MixaelCardCode,
                new byte[] { 1, 0, 0, 0 }));
            arena.GetType().GetMethod(
                    "HighlightPromptZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, new object[] { prompt });
            extra.GetType().GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(extra, null);
            Assert.That(
                extraOutline.enabled,
                Is.False,
                "O card exclusivo do atalho dev não pode acender o Deck Adicional normal.");
        }

        [UnityTest]
        public IEnumerator ExtraDeckBrowserOpensForInspectionWithoutLegalSummon()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            FieldInfo readyField = arena.GetType().GetField(
                "presentationReady",
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int frame = 0;
                 frame < 600 &&
                 !(bool)(readyField?.GetValue(arena) ?? false);
                 frame++)
            {
                yield return null;
            }
            Assert.That(readyField, Is.Not.Null);
            Assert.That(readyField.GetValue(arena), Is.True);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.PlayerExtraDeckCards, Is.Not.Empty);
            Component extra = FindZone("PlayerOne", "ExtraDeck", 0);
            Assert.That(extra, Is.Not.Null);

            MethodInfo openBrowser = arena.GetType().GetMethod(
                "OpenZoneChoices",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(openBrowser, Is.Not.Null);
            openBrowser.Invoke(arena, new object[] { extra, null });

            GameObject browser = arena.GetType().GetField(
                    "zoneBrowser",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(arena) as GameObject;
            RectTransform content = arena.GetType().GetField(
                    "zoneBrowserContent",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(arena) as RectTransform;
            Assert.That(browser, Is.Not.Null);
            Assert.That(browser.activeSelf, Is.True);
            Assert.That(content, Is.Not.Null);
            Assert.That(
                content.childCount,
                Is.EqualTo(controller.PlayerExtraDeckCards.Count));
            ScrollRect scroll = browser.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.horizontal, Is.True);
            Assert.That(scroll.vertical, Is.False);

            Button inspect = content.GetChild(0).GetComponent<Button>();
            Assert.That(inspect, Is.Not.Null);
            inspect.onClick.Invoke();
            uint inspectedCode = (uint)arena.GetType().GetField(
                    "inspectedCode",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(arena);
            Assert.That(
                inspectedCode,
                Is.EqualTo(controller.PlayerExtraDeckCards[0]));
            Assert.That(
                content.GetChild(0).transform.Find("Invocar"),
                Is.Null,
                "Sem escolha legal, a bandeja deve ser somente consulta.");

            Button dismiss = browser.GetComponent<Button>();
            Assert.That(dismiss, Is.Not.Null);
            dismiss.onClick.Invoke();
            Assert.That(browser.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator CardPresentationLocksPlayerAndBotDecisions()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            for (int frame = 0;
                 frame < 600 && controller.CurrentPrompt == null;
                 frame++)
            {
                yield return null;
            }
            DuelPrompt prompt = controller.CurrentPrompt;
            Assert.That(prompt, Is.Not.Null);
            Assert.That(prompt.Choices, Is.Not.Empty);
            arena.GetType().GetMethod(
                    "SetCardPresentationDecisionLock",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, new object[] { true });
            Assert.That(controller.PresentationDecisionLocked, Is.True);
            controller.SubmitChoice(prompt.Choices[0]);
            Assert.That(
                controller.CurrentPrompt,
                Is.SameAs(prompt),
                "O Core não deve aceitar outra jogada durante o destaque.");
            arena.GetType().GetMethod(
                    "SetCardPresentationDecisionLock",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(arena, new object[] { false });
            Assert.That(controller.PresentationDecisionLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator OnlineResponseWaitsForPresentationAndIsNotLost()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            for (int frame = 0;
                 frame < 600 && controller.CurrentPrompt == null;
                 frame++)
            {
                yield return null;
            }
            DuelPrompt prompt = controller.CurrentPrompt;
            DuelChoice choice = prompt?.Choices.FirstOrDefault(candidate =>
                candidate.Response != null && candidate.Response.Length > 0);
            Assert.That(choice, Is.Not.Null);

            MethodInfo setPresentationLock = arena.GetType().GetMethod(
                "SetCardPresentationDecisionLock",
                BindingFlags.Instance | BindingFlags.NonPublic);
            setPresentationLock?.Invoke(arena, new object[] { true });
            controller.SubmitCoreResponse(choice.Response, prompt.RequestId);
            Assert.That(controller.CurrentPrompt, Is.SameAs(prompt));
            Assert.That(
                typeof(DuelArenaController).GetField(
                        "deferredCoreResponse",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(controller),
                Is.Not.Null,
                "A resposta online não pode ser descartada durante a animação.");

            setPresentationLock?.Invoke(arena, new object[] { false });
            for (int frame = 0;
                 frame < 120 && ReferenceEquals(controller.CurrentPrompt, prompt);
                 frame++)
            {
                yield return null;
            }
            Assert.That(controller.CurrentPrompt, Is.Not.SameAs(prompt));
            Assert.That(
                typeof(DuelArenaController).GetField(
                        "deferredCoreResponse",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(controller),
                Is.Null);
        }

        [UnityTest]
        public IEnumerator AuthoritativeRemoteResponseBypassesHostPresentationLock()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            for (int frame = 0;
                 frame < 600 && controller.CurrentPrompt == null;
                 frame++)
            {
                yield return null;
            }

            DuelPrompt prompt = controller.CurrentPrompt;
            DuelChoice choice = prompt?.Choices.FirstOrDefault(candidate =>
                candidate.Response != null && candidate.Response.Length > 0);
            Assert.That(choice, Is.Not.Null);

            MethodInfo setPresentationLock = arena.GetType().GetMethod(
                "SetCardPresentationDecisionLock",
                BindingFlags.Instance | BindingFlags.NonPublic);
            setPresentationLock?.Invoke(arena, new object[] { true });
            Assert.That(controller.PresentationDecisionLocked, Is.True);
            Assert.That(
                controller.SubmitAuthoritativeNetworkResponse(
                    choice.Response,
                    prompt.RequestId),
                Is.True);

            for (int frame = 0;
                 frame < 120 && ReferenceEquals(controller.CurrentPrompt, prompt);
                 frame++)
            {
                yield return null;
            }
            Assert.That(
                controller.CurrentPrompt,
                Is.Not.SameAs(prompt),
                "Host-only presentation must never hold the authoritative remote response.");
            setPresentationLock?.Invoke(arena, new object[] { false });
        }

        [UnityTest]
        public IEnumerator DrawPhaseWaitsForTheCorrectDeckClickAndRestoresIt()
        {
            PlayerPrefs.SetInt("ArcaneAutoStart", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
            yield return WaitForPresentationReady(arena);
            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            for (int frame = 0;
                 frame < 600 && controller.CurrentPrompt == null;
                 frame++)
            {
                yield return null;
            }
            Assert.That(
                controller.CurrentPrompt,
                Is.Not.Null,
                "The native Core must reach a stable player decision first.");
            arena.GetType()
                .GetMethod("SuppressAnnouncementBanner", flags)
                ?.Invoke(arena, null);
            yield return null;
            controller.ConfigureNetworkReplica(0);

            DuelPrompt responsePrompt = EffectQuestionPrompt(8123);
            FieldInfo replicaPrompt = typeof(DuelArenaController).GetField(
                "replicaPrompt",
                flags);
            Assert.That(replicaPrompt, Is.Not.Null);
            replicaPrompt.SetValue(controller, responsePrompt);
            arena.GetType()
                .GetMethod("ResetPromptPresentationIdentity", flags)
                ?.Invoke(arena, null);
            arena.GetType()
                .GetMethod("RefreshEverything", flags)
                ?.Invoke(arena, new object[] { true });
            GameObject compactResponse = arena.GetType()
                .GetField("compactResponseBar", flags)
                ?.GetValue(arena) as GameObject;
            GameObject responseModal = arena.GetType()
                .GetField("choiceModal", flags)
                ?.GetValue(arena) as GameObject;
            Assert.That(compactResponse, Is.Not.Null);
            Assert.That(
                compactResponse.activeSelf,
                Is.False,
                "A concrete effect question must not be reduced to the generic compact bar.");
            Assert.That(
                responseModal?.activeSelf,
                Is.True,
                "The full effect question is presented before the delayed Draw event.");

            DuelPresentationState state = controller.PresentationState;
            state.Players[0].Hand.Clear();
            state.Players[0].HandInstances.Clear();
            state.Apply(TurnEvent(0));
            state.Apply(PhaseEvent(0x001));
            DuelEvent draw = DrawEvent(0, Polymerization);
            state.Apply(draw);
            arena.GetType()
                .GetMethod("RebuildHand", flags)
                ?.Invoke(arena, null);

            CardInstanceState drawn =
                state.Players[0].HandInstances.Last();
            Component drawnView = null;
            for (int frame = 0; frame < 120 && drawnView == null; frame++)
            {
                drawnView = Resources
                    .FindObjectsOfTypeAll<Component>()
                    .FirstOrDefault(component =>
                        component != null &&
                        component.gameObject.activeInHierarchy &&
                        component.GetType().Name == "CardView" &&
                        RuntimeIdOf(component) == drawn.RuntimeId);
                if (drawnView == null)
                    yield return null;
            }
            Assert.That(
                drawnView,
                Is.Not.Null,
                "The drawn RuntimeId must materialize as a hand CardView.");
            Component mainDeck = FindZone("PlayerOne", "MainDeck", 0);
            Component extraDeck = FindZone("PlayerOne", "ExtraDeck", 0);
            Assert.That(mainDeck, Is.Not.Null);
            Assert.That(extraDeck, Is.Not.Null);
            Vector3 originalDeckPosition = mainDeck.transform.position;
            Vector3 originalDeckScale = mainDeck.transform.localScale;

            controller.PresentNetworkEvent(draw);

            FieldInfo awaiting = arena.GetType().GetField(
                "awaitingDrawDeckClick",
                flags);
            FieldInfo locked = arena.GetType().GetField(
                "phasePresentationLocked",
                flags);
            Assert.That(awaiting, Is.Not.Null);
            Assert.That(locked, Is.Not.Null);
            for (int frame = 0;
                 frame < 10 && !(bool)awaiting.GetValue(arena);
                 frame++)
            {
                yield return null;
            }

            Assert.That(awaiting.GetValue(arena), Is.True);
            Assert.That(locked.GetValue(arena), Is.True);
            Assert.That(
                compactResponse.activeSelf,
                Is.False,
                "The Draw presentation must temporarily close the response tray.");
            CanvasGroup handInteraction = arena.GetType()
                .GetField("handInteractionGroup", flags)
                ?.GetValue(arena) as CanvasGroup;
            Assert.That(handInteraction, Is.Not.Null);
            Assert.That(
                handInteraction.alpha,
                Is.LessThan(1f),
                "The hand is dimmed only while the Draw presentation owns input.");
            FieldInfo drawGhost = arena.GetType().GetField(
                "activeDrawGhost",
                flags);
            Assert.That(drawGhost, Is.Not.Null);
            float ghostDeadline = Time.realtimeSinceStartup + 2f;
            while (drawGhost.GetValue(arena) == null &&
                   Time.realtimeSinceStartup < ghostDeadline)
            {
                yield return null;
            }
            Assert.That(
                drawGhost.GetValue(arena),
                Is.Not.Null,
                "A carta fantasma deve sair parcialmente do Deck enquanto aguarda o clique.");
            GameObject ghost = (GameObject)drawGhost.GetValue(arena);
            Quaternion expectedGhostRotation =
                mainDeck.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
            Assert.That(
                Quaternion.Angle(
                    ghost.transform.rotation,
                    expectedGhostRotation),
                Is.LessThan(0.5f),
                "A carta fantasma deve preservar a inclinação do Deck.");
            Transform topCard =
                mainDeck.transform.Find("Card Stack/Top Card Back");
            Assert.That(topCard, Is.Not.Null);
            Vector3 ghostOffset = ghost.transform.position - topCard.position;
            Assert.That(
                Vector3.Cross(mainDeck.transform.up, ghostOffset).magnitude,
                Is.LessThan(0.01f),
                "A carta fantasma deve ficar diretamente acima do Deck.");
            Assert.That(
                mainDeck.transform.localScale.magnitude,
                Is.GreaterThan(originalDeckScale.magnitude * 1.20f),
                "O Deck deve ficar maior e mais próximo da câmera durante a compra.");
            Camera drawCamera = Camera.main;
            Assert.That(drawCamera, Is.Not.Null);
            Vector3 projectedVertical = Vector3.ProjectOnPlane(
                drawCamera.transform.up,
                mainDeck.transform.up).normalized;
            float verticalAngle = Vector3.Angle(
                projectedVertical,
                mainDeck.transform.forward);
            Assert.That(
                Mathf.Min(verticalAngle, 180f - verticalAngle),
                Is.LessThan(1f),
                "O eixo longo do Deck deve ficar vertical na tela.");
            Component arena3D = Resources
                .FindObjectsOfTypeAll<Component>()
                .First(component =>
                    component != null &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "MasterDuelArena3D");
            object localSide = mainDeck.GetType()
                .GetProperty("Owner")
                ?.GetValue(mainDeck);
            Vector3 drawFocus = (Vector3)arena3D.GetType()
                .GetMethod("GetDrawPresentationWorldPosition")
                ?.Invoke(arena3D, new[] { localSide });
            Assert.That(
                Vector3.Distance(
                    drawFocus,
                    originalDeckPosition),
                Is.GreaterThan(1f),
                "The physical Deck needs a distinct position in front of the player.");
            Vector3 focusViewport =
                drawCamera.WorldToViewportPoint(drawFocus);
            Assert.That(focusViewport.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(focusViewport.y, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(
                drawnView.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(0f).Within(0.001f),
                "The Core draw must stay hidden until presentation confirms it.");

            MethodInfo click = arena.GetType().GetMethod(
                "TryHandleDrawDeckClick",
                flags);
            Assert.That(click, Is.Not.Null);
            Assert.That(
                click.Invoke(arena, new object[] { extraDeck }),
                Is.False,
                "Only the highlighted Main Deck may confirm the draw.");
            Assert.That(
                click.Invoke(arena, new object[] { mainDeck }),
                Is.True);

            FieldInfo revealCanFastForward = arena.GetType().GetField(
                "drawRevealCanFastForward",
                flags);
            FieldInfo activeDrawCard = arena.GetType().GetField(
                "activeDrawCard",
                flags);
            Assert.That(revealCanFastForward, Is.Not.Null);
            Assert.That(activeDrawCard, Is.Not.Null);
            float revealDeadline = Time.realtimeSinceStartup + 3f;
            while (!(bool)revealCanFastForward.GetValue(arena) &&
                   Time.realtimeSinceStartup < revealDeadline)
            {
                yield return null;
            }
            Assert.That(
                revealCanFastForward.GetValue(arena),
                Is.True,
                "A carta comprada deve girar e permanecer revelada antes de ir para a mão.");
            Assert.That(activeDrawCard.GetValue(arena), Is.Not.Null);
            Canvas drawOverlay = ((GameObject)activeDrawCard.GetValue(arena))
                .GetComponent<Canvas>();
            Assert.That(drawOverlay, Is.Not.Null);
            Assert.That(drawOverlay.overrideSorting, Is.True);
            Assert.That(
                drawOverlay.sortingOrder,
                Is.GreaterThan(1000),
                "A carta comprada deve sobrepor visualmente as cartas da mão.");
            arena.GetType()
                .GetMethod("RequestDrawRevealFastForward", flags)
                ?.Invoke(arena, null);

            FieldInfo activeRequest = arena.GetType().GetField(
                "activeDrawRequest",
                flags);
            Assert.That(activeRequest, Is.Not.Null);
            float drawDeadline = Time.realtimeSinceStartup + 5f;
            while (activeRequest.GetValue(arena) != null &&
                   Time.realtimeSinceStartup < drawDeadline)
            {
                yield return null;
            }
            while (((bool)locked.GetValue(arena) ||
                    responseModal == null ||
                    !responseModal.activeSelf) &&
                   Time.realtimeSinceStartup < drawDeadline)
            {
                yield return null;
            }

            bool drawCompleted = activeRequest.GetValue(arena) == null;
            bool deckRestored = Vector3.Distance(
                mainDeck.transform.position,
                originalDeckPosition) < 0.01f;
            float finalCardAlpha =
                drawnView.GetComponent<CanvasGroup>().alpha;
            arena.GetType()
                .GetMethod("SuppressAnnouncementBanner", flags)
                ?.Invoke(arena, null);

            Assert.That(drawCompleted, Is.True);
            Assert.That(
                deckRestored,
                Is.True,
                "The Deck must return exactly to its authored position.");
            Assert.That(
                Vector3.Distance(
                    mainDeck.transform.localScale,
                    originalDeckScale),
                Is.LessThan(0.001f),
                "The Deck scale must be restored exactly after the draw.");
            Assert.That(drawGhost.GetValue(arena), Is.Null);
            Assert.That(finalCardAlpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(locked.GetValue(arena), Is.False);
            Assert.That(
                compactResponse.activeSelf,
                Is.False,
                "A concrete effect choice must not regress to the generic compact bar.");
            Assert.That(
                responseModal?.activeSelf,
                Is.True,
                "The same concrete Core prompt must reopen after the Draw animation.");
            Assert.That(
                handInteraction.alpha,
                Is.EqualTo(1f).Within(0.001f),
                "The local hand must become fully opaque and interactive again.");
            FieldInfo timeout = arena.GetType().GetField(
                "DrawClickTimeoutSeconds",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(timeout, Is.Not.Null);
            Assert.That(timeout.GetRawConstantValue(), Is.EqualTo(5f));
            FieldInfo revealHold = arena.GetType().GetField(
                "DrawRevealHoldSeconds",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(revealHold, Is.Not.Null);
            Assert.That(revealHold.GetRawConstantValue(), Is.EqualTo(1.8f));
        }

        private static MonoBehaviour FindArena()
        {
            return Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .FirstOrDefault(component =>
                    component != null &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "CardArenaBootstrap");
        }

        private static IEnumerator WaitForPresentationReady(
            MonoBehaviour arena)
        {
            FieldInfo ready = arena?.GetType().GetField(
                "presentationReady",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(ready, Is.Not.Null);
            for (int frame = 0;
                 frame < 600 && !(bool)ready.GetValue(arena);
                 frame++)
            {
                yield return null;
            }
            Assert.That(
                ready.GetValue(arena),
                Is.True,
                "The arena presentation must finish loading before the test manipulates authoritative state.");
        }

        private static Component FindZone(
            string owner,
            string kind,
            int index)
        {
            return Resources.FindObjectsOfTypeAll<Component>()
                .FirstOrDefault(component =>
                {
                    if (component == null ||
                        !component.gameObject.scene.IsValid() ||
                        !component.gameObject.activeInHierarchy ||
                        component.GetType().Name != "DuelZone3D")
                    {
                        return false;
                    }
                    System.Type type = component.GetType();
                    object zoneOwner =
                        type.GetProperty("Owner")?.GetValue(component);
                    object zoneKind =
                        type.GetProperty("Kind")?.GetValue(component);
                    object zoneIndex =
                        type.GetProperty("ZoneIndex")?.GetValue(component);
                    return zoneOwner?.ToString() == owner &&
                           zoneKind?.ToString() == kind &&
                           zoneIndex is int current &&
                           current == index;
                });
        }

        private static Transform FindPresentedCard(Component zone)
        {
            if (zone == null)
                return null;
            return zone.GetType()
                .GetMethod("FindPresentedCard")
                ?.Invoke(zone, null) as Transform;
        }

        private static DuelChoice LocatedChoice(
            string label,
            byte location,
            uint code = 0,
            byte[] response = null)
        {
            var choice = new DuelChoice();
            System.Type type = typeof(DuelChoice);
            type.GetProperty("Label")?.SetValue(choice, label);
            type.GetProperty("CardCode")?.SetValue(choice, code);
            type.GetProperty("Response")?.SetValue(choice, response);
            type.GetProperty("HasLocation")?.SetValue(choice, true);
            type.GetProperty("Controller")?.SetValue(choice, (byte)0);
            type.GetProperty("Location")?.SetValue(choice, location);
            type.GetProperty("Sequence")?.SetValue(choice, 0u);
            return choice;
        }

        private static void AssertWorldViewMatches(
            Transform card,
            ulong runtimeId)
        {
            Component view = card.GetComponents<Component>()
                .FirstOrDefault(component =>
                    component.GetType().Name ==
                    "WorldCardInstanceView");
            Assert.That(view, Is.Not.Null);
            object key = view.GetType()
                .GetProperty("InstanceKey")
                ?.GetValue(view);
            Assert.That(key, Is.Not.Null);
            object actualRuntimeId = key.GetType()
                .GetProperty("RuntimeId")
                ?.GetValue(key);
            Assert.That(actualRuntimeId, Is.EqualTo(runtimeId));
        }

        private static void AssertSoundForFrame(
            MonoBehaviour arena,
            object catalog,
            MethodInfo resolver,
            int frame,
            ArcaneCardSound expected)
        {
            var entries = catalog.GetType().GetProperty("Entries")
                ?.GetValue(catalog) as System.Collections.IEnumerable;
            object entry = entries?.Cast<object>().FirstOrDefault(card =>
                card != null && System.Convert.ToInt32(
                    card.GetType().GetProperty("MonsterFrame")
                        ?.GetValue(card)) == frame);
            Assert.That(entry, Is.Not.Null, $"Catálogo sem exemplo {frame}.");
            string officialId = entry.GetType()
                .GetProperty("OfficialCardId")?.GetValue(entry) as string;
            uint code = uint.Parse(officialId);
            Assert.That(
                resolver.Invoke(arena, new object[] { code }),
                Is.EqualTo(expected));
        }

        private static void AssertSoundForCategory(
            MonoBehaviour arena,
            object catalog,
            MethodInfo resolver,
            int category,
            ArcaneCardSound expected)
        {
            var entries = catalog.GetType().GetProperty("Entries")
                ?.GetValue(catalog) as System.Collections.IEnumerable;
            object entry = entries?.Cast<object>().FirstOrDefault(card =>
                card != null && System.Convert.ToInt32(
                    card.GetType().GetProperty("Category")
                        ?.GetValue(card)) == category);
            Assert.That(
                entry,
                Is.Not.Null,
                $"Catálogo sem exemplo {category}.");
            string officialId = entry.GetType()
                .GetProperty("OfficialCardId")?.GetValue(entry) as string;
            uint code = uint.Parse(officialId);
            Assert.That(
                resolver.Invoke(arena, new object[] { code }),
                Is.EqualTo(expected));
        }

        private static ulong RuntimeIdOf(Component cardView)
        {
            object key = cardView?.GetType()
                .GetProperty("InstanceKey")
                ?.GetValue(cardView);
            object value = key?.GetType()
                .GetProperty("RuntimeId")
                ?.GetValue(key);
            return value is ulong runtimeId ? runtimeId : 0;
        }

        private static DuelEvent MoveIntoMonsterZone(
            uint code,
            uint sequence)
        {
            return MoveEvent(
                code,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                sequence,
                1);
        }

        private static DuelEvent DrawEvent(
            byte player,
            params uint[] codes)
        {
            var payload = new List<byte> { player };
            UInt32(payload, (uint)codes.Length);
            foreach (uint code in codes)
            {
                UInt32(payload, code);
                UInt32(payload, 0);
            }
            return Decode(CoreMessage.Draw, payload);
        }

        private static DuelEvent TurnEvent(byte player)
        {
            return Decode(
                CoreMessage.NewTurn,
                new List<byte> { player });
        }

        private static DuelEvent PhaseEvent(ushort phase)
        {
            return Decode(
                CoreMessage.NewPhase,
                new List<byte>
                {
                    (byte)(phase & 0xFF),
                    (byte)((phase >> 8) & 0xFF)
                });
        }

        private static DuelEvent MoveEvent(
            uint code,
            byte previousController,
            byte previousLocation,
            uint previousSequence,
            byte currentController,
            byte currentLocation,
            uint currentSequence,
            uint currentPosition,
            uint reason = 0)
        {
            var payload = new List<byte>();
            UInt32(payload, code);
            Location(
                payload,
                previousController,
                previousLocation,
                previousSequence,
                0);
            Location(
                payload,
                currentController,
                currentLocation,
                currentSequence,
                currentPosition);
            UInt32(payload, reason);
            return Decode(CoreMessage.Move, payload);
        }

        private static DuelEvent EquipEvent(
            byte sourceController,
            byte sourceLocation,
            uint sourceSequence,
            byte targetController,
            byte targetLocation,
            uint targetSequence)
        {
            var payload = new List<byte>();
            Location(payload, sourceController, sourceLocation, sourceSequence, 1);
            Location(payload, targetController, targetLocation, targetSequence, 1);
            return Decode(CoreMessage.Equip, payload);
        }

        private static DuelEvent SummonEvent(
            CoreMessage message,
            uint code,
            byte controller,
            uint sequence,
            uint position)
        {
            var payload = new List<byte>();
            UInt32(payload, code);
            Location(payload, controller,
                (byte)DuelLocation.MonsterZone, sequence, position);
            return Decode(message, payload);
        }

        private static DuelPrompt DuplicateIdlePrompt()
        {
            var payload = new List<byte> { 0 };
            UInt32(payload, 2);
            CommandCard(payload, EffectVeiler, 0);
            CommandCard(payload, EffectVeiler, 1);
            for (int category = 0; category < 5; category++)
                UInt32(payload, 0);
            payload.Add(0);
            payload.Add(1);
            payload.Add(0);
            return Decode(
                    CoreMessage.SelectIdleCommand,
                    payload)
                .Prompt;
        }

        private static DuelPrompt OptionalChainPrompt(ulong requestId)
        {
            var payload = new List<byte> { 0, 0, 0 };
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 1);
            UInt32(payload, EffectVeiler);
            Location(
                payload,
                0,
                (byte)DuelLocation.Hand,
                0,
                1);
            UInt32(payload, 0);
            UInt32(payload, 0);
            payload.Add(0);
            DuelPrompt prompt = Decode(
                    CoreMessage.SelectChain,
                    payload)
                .Prompt;
            SetProperty(prompt, nameof(DuelPrompt.RequestId), requestId);
            return prompt;
        }

        private static DuelPrompt EffectQuestionPrompt(ulong requestId)
        {
            var payload = new List<byte> { 0 };
            UInt32(payload, EffectVeiler);
            Location(
                payload,
                0,
                (byte)DuelLocation.Hand,
                0,
                1);
            UInt32(payload, 0);
            UInt32(payload, 0);
            DuelPrompt prompt = Decode(
                    CoreMessage.SelectEffectYesNo,
                    payload)
                .Prompt;
            typeof(DuelPrompt)
                .GetProperty(
                    "RequestId",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                ?.SetValue(prompt, requestId);
            return prompt;
        }

        private static DuelPrompt FieldActionPrompt(ulong requestId)
        {
            var prompt = new DuelPrompt();
            SetProperty(prompt, nameof(DuelPrompt.RequestId), requestId);
            SetProperty(prompt, nameof(DuelPrompt.Message),
                CoreMessage.SelectIdleCommand);
            SetProperty(prompt, nameof(DuelPrompt.Player), (byte)0);
            SetProperty(prompt, nameof(DuelPrompt.Title), "Escolha uma acao");
            prompt.Choices.Add(LocatedActionChoice(
                requestId,
                "Mudar posicao",
                EffectVeiler,
                (byte)DuelLocation.MonsterZone,
                0,
                2));
            prompt.Choices.Add(LocatedActionChoice(
                requestId,
                "Ativar",
                EffectVeiler,
                (byte)DuelLocation.MonsterZone,
                0,
                5));
            return prompt;
        }

        private static DuelPrompt SingleHandSelectionPrompt(ulong requestId)
        {
            var prompt = new DuelPrompt();
            SetProperty(prompt, nameof(DuelPrompt.RequestId), requestId);
            SetProperty(prompt, nameof(DuelPrompt.Message),
                CoreMessage.SelectCard);
            SetProperty(prompt, nameof(DuelPrompt.Player), (byte)0);
            SetProperty(prompt, nameof(DuelPrompt.Title),
                "Escolha um Dragao da sua mao");
            SetProperty(prompt, nameof(DuelPrompt.MinimumSelections), 1U);
            SetProperty(prompt, nameof(DuelPrompt.MaximumSelections), 1U);
            prompt.Choices.Add(LocatedActionChoice(
                requestId,
                "Invocar por Invocacao-Especial",
                EffectVeiler,
                (byte)DuelLocation.Hand,
                0,
                0));
            return prompt;
        }

        private static DuelPrompt TwoTargetSelectionPrompt(ulong requestId)
        {
            var prompt = new DuelPrompt();
            SetProperty(prompt, nameof(DuelPrompt.RequestId), requestId);
            SetProperty(prompt, nameof(DuelPrompt.Message),
                CoreMessage.SelectCard);
            SetProperty(prompt, nameof(DuelPrompt.Player), (byte)0);
            SetProperty(prompt, nameof(DuelPrompt.Title),
                "Escolha ate 2 monstros com a face para cima");
            SetProperty(prompt, nameof(DuelPrompt.MinimumSelections), 1U);
            SetProperty(prompt, nameof(DuelPrompt.MaximumSelections), 2U);

            var automaticShortcut = new DuelChoice();
            SetProperty(automaticShortcut, nameof(DuelChoice.RequestId),
                requestId);
            SetProperty(automaticShortcut, nameof(DuelChoice.Label),
                "Selecionar as primeiras 2");
            SetProperty(automaticShortcut, nameof(DuelChoice.Response),
                CoreMessageDecoder.CardSelectionResponse(
                    new uint[] { 0, 1 }));
            prompt.Choices.Add(automaticShortcut);

            DuelChoice first = LocatedActionChoice(
                requestId,
                "Selecionar carta 1",
                DarkMagician,
                (byte)DuelLocation.MonsterZone,
                0,
                0);
            SetProperty(first, nameof(DuelChoice.ChoiceIndex), 0);
            prompt.Choices.Add(first);

            DuelChoice second = LocatedActionChoice(
                requestId,
                "Selecionar carta 2",
                BlueEyesWhiteDragon,
                (byte)DuelLocation.MonsterZone,
                1,
                1);
            SetProperty(second, nameof(DuelChoice.Controller), (byte)1);
            SetProperty(second, nameof(DuelChoice.Position), 0x1U);
            SetProperty(second, nameof(DuelChoice.ChoiceIndex), 1);
            prompt.Choices.Add(second);
            return prompt;
        }

        private static DuelChoice LocatedActionChoice(
            ulong requestId,
            string label,
            uint code,
            byte location,
            uint sequence,
            int response)
        {
            var choice = new DuelChoice();
            SetProperty(choice, nameof(DuelChoice.RequestId), requestId);
            SetProperty(choice, nameof(DuelChoice.Label), label);
            SetProperty(choice, nameof(DuelChoice.CardCode), code);
            SetProperty(choice, nameof(DuelChoice.Response),
                System.BitConverter.GetBytes(response));
            SetProperty(choice, nameof(DuelChoice.HasLocation), true);
            SetProperty(choice, nameof(DuelChoice.Controller), (byte)0);
            SetProperty(choice, nameof(DuelChoice.Location), location);
            SetProperty(choice, nameof(DuelChoice.Sequence), sequence);
            SetProperty(choice, nameof(DuelChoice.ChoiceIndex), 0);
            return choice;
        }

        private static void SetProperty(
            object target,
            string name,
            object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance);
            Assert.That(property, Is.Not.Null);
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(target, new[] { value });
        }

        private static void CommandCard(
            List<byte> payload,
            uint code,
            uint sequence)
        {
            UInt32(payload, code);
            payload.Add(0);
            payload.Add((byte)DuelLocation.Hand);
            UInt32(payload, sequence);
        }

        private static DuelEvent Decode(
            CoreMessage message,
            List<byte> payload)
        {
            var framed = new List<byte>();
            UInt32(framed, (uint)payload.Count + 1);
            framed.Add((byte)message);
            framed.AddRange(payload);
            return CoreMessageDecoder.Decode(framed.ToArray())[0];
        }

        private static void Location(
            List<byte> bytes,
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            bytes.Add(controller);
            bytes.Add(location);
            UInt32(bytes, sequence);
            UInt32(bytes, position);
        }

        private static void UInt32(
            List<byte> bytes,
            uint value)
        {
            bytes.Add((byte)(value & 0xFF));
            bytes.Add((byte)((value >> 8) & 0xFF));
            bytes.Add((byte)((value >> 16) & 0xFF));
            bytes.Add((byte)((value >> 24) & 0xFF));
        }
    }
}
