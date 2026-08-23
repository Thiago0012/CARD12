using System;
using System.Collections;
using System.Reflection;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class CardTransitionPresentationPlayModeTests
    {
        private static readonly BindingFlags PrivateStatic =
            BindingFlags.Static | BindingFlags.NonPublic;

        [TestCase(
            DuelLocation.Hand,
            DuelLocation.MonsterZone,
            0U,
            "Travel")]
        [TestCase(
            DuelLocation.Deck,
            DuelLocation.SpellTrapZone,
            0U,
            "Travel")]
        [TestCase(
            DuelLocation.MonsterZone,
            DuelLocation.Graveyard,
            0U,
            "Travel")]
        [TestCase(
            DuelLocation.MonsterZone,
            DuelLocation.Graveyard,
            0x1U,
            "Destruction")]
        [TestCase(
            DuelLocation.MonsterZone,
            DuelLocation.Banished,
            0U,
            "Travel")]
        [TestCase(
            DuelLocation.Hand,
            DuelLocation.Graveyard,
            0U,
            "Travel")]
        [TestCase(
            DuelLocation.Deck,
            DuelLocation.Banished,
            0U,
            "Travel")]
        [TestCase(
            DuelLocation.Graveyard,
            DuelLocation.Banished,
            0U,
            "Travel")]
        public void MoveReasonSelectsOnlyTheExpectedVisual(
            uint previousLocation,
            uint currentLocation,
            uint reason,
            string expected)
        {
            Type arenaType = Type.GetType(
                "ArcaneArena.CardArenaBootstrap, Assembly-CSharp");
            Assert.That(arenaType, Is.Not.Null);
            MethodInfo classifier = arenaType.GetMethod(
                "CardTransitionKindFor",
                PrivateStatic);
            Assert.That(classifier, Is.Not.Null);

            DuelEvent duelEvent = MoveEvent(
                (byte)previousLocation,
                (byte)currentLocation,
                reason);
            object result = classifier.Invoke(null, new object[] { duelEvent });

            Assert.That(result?.ToString(), Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator TravelAndDestructionRemainPresentationOnly()
        {
            Type animationPreferences = Type.GetType(
                "ArcaneArena.Presentation.DuelAnimationPreferences, " +
                "Assembly-CSharp");
            Assert.That(animationPreferences, Is.Not.Null);
            animationPreferences.GetMethod(
                    "ResetToDefaults",
                    BindingFlags.Static | BindingFlags.Public)
                ?.Invoke(null, null);
            SceneManager.LoadScene("DuelArena");
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = null;
            FieldInfo ready = null;
            for (int frame = 0; frame < 600; frame++)
            {
                arena ??= Array.Find(
                    Resources.FindObjectsOfTypeAll<MonoBehaviour>(),
                    component => component != null &&
                                 component.gameObject.activeInHierarchy &&
                                 component.GetType().Name ==
                                 "CardArenaBootstrap");
                ready ??= arena?.GetType().GetField(
                    "presentationReady",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (arena != null && (bool)(ready?.GetValue(arena) ?? false))
                    break;
                yield return null;
            }
            Assert.That(arena, Is.Not.Null);
            Assert.That(ready?.GetValue(arena), Is.True);

            Type arrivalEffect = Type.GetType(
                "ArcaneArena.Cards.MonsterSummonArrivalEffect, " +
                "Assembly-CSharp");
            MethodInfo playArrivalEffect = arena.GetType().GetMethod(
                "PlayMonsterSummonArrivalEffect",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(arrivalEffect, Is.Not.Null);
            Assert.That(playArrivalEffect, Is.Not.Null);
            playArrivalEffect.Invoke(
                arena,
                new[]
                {
                    Enum.Parse(arrivalEffect, "Purple"),
                    (object)Vector2.zero
                });
            Assert.That(
                GameObject.Find("Efeito de Invocacao Roxo"),
                Is.Not.Null);
            yield return new WaitForSecondsRealtime(0.80f);
            Assert.That(
                GameObject.Find("Efeito de Invocacao Roxo"),
                Is.Null);

            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            controller.ConfigureNetworkReplica(0);
            DuelPresentationState state = controller.PresentationState;
            const uint code = 97268402U;
            state.Players[0].Hand.Clear();
            state.Players[0].HandInstances.Clear();
            var draw = new DuelEvent();
            SetProperty(draw, nameof(DuelEvent.Message), CoreMessage.Draw);
            SetProperty(draw, nameof(DuelEvent.Player), (byte)0);
            SetProperty(draw, nameof(DuelEvent.Codes), new[] { code });
            state.Apply(draw);
            MethodInfo refreshHand = arena.GetType().GetMethod(
                "RefreshEverything",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshHand, Is.Not.Null);
            refreshHand.Invoke(arena, new object[] { true });
            yield return null;
            Assert.That(state.Players[0].Hand, Is.EqualTo(new[] { code }));
            int sequence = Array.FindIndex(
                state.Players[0].MonsterZones,
                value => value == 0);
            Assert.That(sequence, Is.GreaterThanOrEqualTo(0));

            DuelEvent arrival = MoveEvent(
                (byte)DuelLocation.Hand,
                (byte)DuelLocation.MonsterZone,
                0U,
                code,
                0U,
                (uint)sequence,
                0x1U);
            object arrivalVisual = CaptureBeforeStateChange(arena, arrival);
            Assert.That(
                SnapshotField<bool>(arrivalVisual, "FlipToDestination"),
                Is.False,
                "Face-up arrivals should travel directly without a flip.");
            Assert.That(
                SnapshotField<Sprite>(arrivalVisual, "DestinationSprite"),
                Is.SameAs(SnapshotField<Sprite>(arrivalVisual, "Sprite")));
            state.Apply(arrival);
            RefreshAndBegin(arena, arrivalVisual);

            Assert.That(
                state.Players[0].MonsterZones[sequence],
                Is.EqualTo(code),
                "The authoritative state must advance before the overlay ends.");
            yield return new WaitForSecondsRealtime(0.20f);
            Assert.That(
                GameObject.Find("Carta em Movimento"),
                Is.Not.Null);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(GameObject.Find("Carta em Movimento"), Is.Null);

            DuelEvent destruction = MoveEvent(
                (byte)DuelLocation.MonsterZone,
                (byte)DuelLocation.Graveyard,
                0x1U,
                code,
                (uint)sequence,
                0U,
                0x1U);
            object destructionVisual =
                CaptureBeforeStateChange(arena, destruction);
            state.Apply(destruction);
            RefreshAndBegin(arena, destructionVisual);

            GameObject fragments = GameObject.Find("Fragmentos da Carta");
            Assert.That(fragments, Is.Not.Null);
            Assert.That(
                fragments.GetComponentsInChildren<RectMask2D>(true),
                Has.Length.EqualTo(16));
            Assert.That(
                fragments.transform.Find("Impacto da Destruição"),
                Is.Not.Null);
            Assert.That(state.Players[0].Graveyard[^1], Is.EqualTo(code));
            Assert.That(
                state.Players[0].MonsterZones[sequence],
                Is.EqualTo(0));
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(GameObject.Find("Fragmentos da Carta"), Is.Null);
            AssertConcealedSpecialPilesReflectState(arena, state);

            DuelEvent banishment = MoveEvent(
                (byte)DuelLocation.Graveyard,
                (byte)DuelLocation.Banished,
                0U,
                code,
                0U,
                0U,
                0x1U);
            object banishmentVisual =
                CaptureBeforeStateChange(arena, banishment);
            state.Apply(banishment);
            RefreshAndBegin(arena, banishmentVisual);

            Assert.That(state.Players[0].Graveyard, Is.Empty);
            Assert.That(state.Players[0].Banished[^1], Is.EqualTo(code));
            Assert.That(
                GameObject.Find("Carta em Movimento"),
                Is.Not.Null,
                "A carta banida deve viajar visualmente até o poço de banimento.");
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(GameObject.Find("Carta em Movimento"), Is.Null);
            AssertConcealedSpecialPilesReflectState(arena, state);

            state.Apply(draw);
            refreshHand.Invoke(arena, new object[] { true });
            yield return null;
            DuelEvent faceDownArrival = MoveEvent(
                (byte)DuelLocation.Hand,
                (byte)DuelLocation.MonsterZone,
                0U,
                code,
                0U,
                (uint)sequence,
                0x8U);
            object faceDownVisual =
                CaptureBeforeStateChange(arena, faceDownArrival);
            Assert.That(
                SnapshotField<bool>(faceDownVisual, "FlipToDestination"),
                Is.True,
                "A locally visible card should turn to its back while set.");
            Sprite cardBack = PrivateField<Sprite>(arena, "cardBackSprite");
            Assert.That(
                SnapshotField<Sprite>(faceDownVisual, "DestinationSprite"),
                Is.SameAs(cardBack));

            state.Apply(faceDownArrival);
            RefreshAndBegin(arena, faceDownVisual);
            GameObject movingCard = GameObject.Find("Carta em Movimento");
            Assert.That(movingCard, Is.Not.Null);
            Assert.That(
                movingCard.transform.Find("Rastro da Carta"),
                Is.Not.Null);
            Assert.That(
                movingCard.GetComponent<Canvas>().overrideSorting,
                Is.False,
                "The moving card must obey the normal UI hierarchy.");
            GameObject choiceTray =
                PrivateField<GameObject>(arena, "choiceModal");
            Assert.That(choiceTray, Is.Not.Null);
            Assert.That(
                movingCard.transform.GetSiblingIndex(),
                Is.LessThan(choiceTray.transform.GetSiblingIndex()),
                "Selection trays must stay above card travel visuals.");
            Image movingImage = movingCard.GetComponent<Image>();
            Assert.That(movingImage.sprite, Is.Not.SameAs(cardBack));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(movingImage.sprite, Is.SameAs(cardBack));
            Assert.That(
                state.Players[0].MonsterZones[sequence],
                Is.EqualTo(code));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(GameObject.Find("Carta em Movimento"), Is.Null);
        }

        private static void AssertConcealedSpecialPilesReflectState(
            MonoBehaviour arena,
            DuelPresentationState state)
        {
            int presentedGraveyardCards = 0;
            int presentedBanishedCards = 0;
            int graveyardWellCount = 0;
            int banishedWellCount = 0;
            Type wellType = Type.GetType(
                "ArcaneArena.DuelSpecialZoneWellVisual, Assembly-CSharp");
            Assert.That(wellType, Is.Not.Null);
            MethodInfo buildEntries = arena.GetType().GetMethod(
                "BuildZoneBrowserEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildEntries, Is.Not.Null);
            MonoBehaviour[] components =
                Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component == null ||
                    !component.gameObject.activeInHierarchy ||
                    component.GetType().Name != "DuelZone3D")
                {
                    continue;
                }

                PropertyInfo kindProperty = component.GetType().GetProperty(
                    "Kind",
                    BindingFlags.Instance | BindingFlags.Public);
                MethodInfo findPresentedCard = component.GetType().GetMethod(
                    "FindPresentedCard",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(kindProperty, Is.Not.Null);
                Assert.That(findPresentedCard, Is.Not.Null);
                string kind = kindProperty.GetValue(component)?.ToString();
                if (kind != "Graveyard" && kind != "Banishment")
                    continue;

                Assert.That(
                    findPresentedCard.Invoke(component, null),
                    Is.Null,
                    $"{kind} must conceal its cards inside the physical well " +
                    "instead of leaving a card attached to the field.");

                Component well = component.GetComponent(wellType);
                Assert.That(well, Is.Not.Null);
                PropertyInfo countProperty = well.GetType().GetProperty(
                    "CardCount",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(countProperty, Is.Not.Null);
                int count = (int)countProperty.GetValue(well);
                object browserEntries = buildEntries.Invoke(
                    arena,
                    new object[]
                    {
                        component,
                        false,
                        Array.Empty<DuelChoice>()
                    });
                Assert.That(browserEntries, Is.InstanceOf<ICollection>());
                Assert.That(
                    ((ICollection)browserEntries).Count,
                    Is.EqualTo(count),
                    $"Clicking {kind} must list every stored card.");
                if (kind == "Graveyard")
                {
                    graveyardWellCount++;
                    presentedGraveyardCards += count;
                }
                else
                {
                    banishedWellCount++;
                    presentedBanishedCards += count;
                }
            }

            int authoritativeGraveyardCards = 0;
            int authoritativeBanishedCards = 0;
            for (int player = 0; player < state.Players.Length; player++)
            {
                authoritativeGraveyardCards +=
                    state.Players[player].Graveyard.Count;
                authoritativeBanishedCards +=
                    state.Players[player].Banished.Count;
            }

            Assert.That(graveyardWellCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(banishedWellCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(
                presentedGraveyardCards,
                Is.EqualTo(authoritativeGraveyardCards));
            Assert.That(
                presentedBanishedCards,
                Is.EqualTo(authoritativeBanishedCards));
        }

        private static T SnapshotField<T>(object snapshot, string name)
        {
            Assert.That(snapshot, Is.Not.Null);
            FieldInfo field = snapshot.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(snapshot);
        }

        private static T PrivateField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }

        private static object CaptureBeforeStateChange(
            MonoBehaviour arena,
            DuelEvent duelEvent)
        {
            MethodInfo capture = arena.GetType().GetMethod(
                "CaptureCardTransition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(capture, Is.Not.Null);
            object visual = capture.Invoke(arena, new object[] { duelEvent });
            Assert.That(visual, Is.Not.Null);
            return visual;
        }

        private static void RefreshAndBegin(
            MonoBehaviour arena,
            object visual)
        {
            MethodInfo refresh = arena.GetType().GetMethod(
                "RefreshEverything",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo begin = arena.GetType().GetMethod(
                "BeginCardTransition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refresh, Is.Not.Null);
            Assert.That(begin, Is.Not.Null);
            refresh.Invoke(arena, new object[] { true });
            begin.Invoke(arena, new[] { visual });
        }

        private static DuelEvent MoveEvent(
            byte previousLocation,
            byte currentLocation,
            uint reason,
            uint code = 0U,
            uint previousSequence = 0U,
            uint currentSequence = 0U,
            uint currentPosition = 0U)
        {
            var duelEvent = new DuelEvent();
            SetProperty(duelEvent, nameof(DuelEvent.Message), CoreMessage.Move);
            SetProperty(duelEvent, nameof(DuelEvent.Value), reason);
            SetProperty(duelEvent, nameof(DuelEvent.Code), code);
            SetProperty(
                duelEvent,
                nameof(DuelEvent.Previous),
                Location(previousLocation, previousSequence, 0U));
            SetProperty(
                duelEvent,
                nameof(DuelEvent.Current),
                Location(
                    currentLocation,
                    currentSequence,
                    currentPosition));
            return duelEvent;
        }

        private static CardLocation Location(
            byte location,
            uint sequence,
            uint position)
        {
            var cardLocation = new CardLocation();
            SetProperty(
                cardLocation,
                nameof(CardLocation.Controller),
                (byte)0);
            SetProperty(
                cardLocation,
                nameof(CardLocation.Location),
                location);
            SetProperty(
                cardLocation,
                nameof(CardLocation.Sequence),
                sequence);
            SetProperty(
                cardLocation,
                nameof(CardLocation.Position),
                position);
            return cardLocation;
        }

        private static void SetProperty<T>(
            object target,
            string property,
            T value)
        {
            PropertyInfo info = target.GetType().GetProperty(
                property,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null);
            info.SetValue(target, value);
        }
    }
}
