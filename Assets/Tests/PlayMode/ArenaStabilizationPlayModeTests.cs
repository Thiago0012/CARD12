using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class ArenaStabilizationPlayModeTests
    {
        private const uint DarkMagician = 46986414;
        private const uint DarkMagicalCircle = 47222536;
        private const uint EffectVeiler = 97268402;
        private const uint FaceDown = 0xA;

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
        public IEnumerator SetSpellTrapUsesTheCardBackAndHidesItsFace()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
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
            Transform card = zone.transform.Find("Carta Invocada");
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
            Transform card = zone.transform.Find("Carta Invocada");
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
                zone.transform.Find("Carta Invocada"),
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
                zone.transform.Find("Carta Invocada");
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
        public IEnumerator ConsecutivePileUpdatesKeepTheLatestAuthoritativeView()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
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
            Transform card = zone.transform.Find("Carta Invocada");
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
        public IEnumerator DuplicateSelectionCancelAndSecondSummonStayIndependent()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = FindArena();
            Assert.That(arena, Is.Not.Null);
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
                zone.transform.Find("Carta Invocada");
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

        private static MonoBehaviour FindArena()
        {
            return Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .FirstOrDefault(component =>
                    component != null &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "CardArenaBootstrap");
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

        private static DuelEvent MoveEvent(
            uint code,
            byte previousController,
            byte previousLocation,
            uint previousSequence,
            byte currentController,
            byte currentLocation,
            uint currentSequence,
            uint currentPosition)
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
            UInt32(payload, 0);
            return Decode(CoreMessage.Move, payload);
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
