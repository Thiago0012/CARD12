using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ExtraMonsterZoneLayoutEditModeTests
    {
        [Test]
        public void AuthoredArenaProvidesBothSharedExtraMonsterZones()
        {
            var root = new GameObject("Arena Layout Test");
            root.SetActive(false);
            try
            {
                Type arenaType = TypeByName("ArcaneArena.MasterDuelArena3D");
                Component arena = root.AddComponent(arenaType);
                arenaType.GetMethod("Rebuild")?.Invoke(arena, null);
                Component[] zones = root
                    .GetComponentsInChildren<Component>(true)
                    .Where(component =>
                        component.GetType().FullName ==
                        "ArcaneArena.DuelZone3D")
                    .ToArray();

                for (int player = 0; player < 2; player++)
                {
                    int[] monsterIndexes = zones
                        .Where(zone =>
                            PropertyInt(zone, "Owner") == player &&
                            PropertyText(zone, "Kind") == "Monster")
                        .Select(zone => PropertyInt(zone, "ZoneIndex"))
                        .OrderBy(index => index)
                        .ToArray();
                    Assert.That(
                        monsterIndexes,
                        Is.EqualTo(Enumerable.Range(0, 7).ToArray()));
                }

                Component playerLeft = Find(zones, 0, 5);
                Component opponentRight = Find(zones, 1, 6);
                Component playerRight = Find(zones, 0, 6);
                Component opponentLeft = Find(zones, 1, 5);
                Assert.That(
                    playerLeft.transform.position,
                    Is.EqualTo(opponentRight.transform.position));
                Assert.That(
                    playerRight.transform.position,
                    Is.EqualTo(opponentLeft.transform.position));

                Component playerCenter = Find(zones, 0, 2);
                Component opponentCenter = Find(zones, 1, 2);
                Assert.That(
                    Mathf.Abs(playerCenter.transform.position.z),
                    Is.GreaterThan(2f),
                    "The main Monster Zones must remain visibly separated from the shared Extra Monster Zones.");
                Assert.That(
                    PlanarRotationDistance(
                        playerCenter.transform.position,
                        opponentCenter.transform.position),
                    Is.LessThan(0.001f),
                    "The opponent half must be a true 180-degree rotation of the local half.");

                Component playerDeck = Find(zones, 0, "MainDeck");
                Component playerGrave = Find(zones, 0, "Graveyard");
                Component playerBanish = Find(zones, 0, "Banishment");
                Component playerExtra = Find(zones, 0, "ExtraDeck");
                Component opponentDeck = Find(zones, 1, "MainDeck");
                Component opponentGrave = Find(zones, 1, "Graveyard");
                Component opponentBanish = Find(zones, 1, "Banishment");
                Component opponentExtra = Find(zones, 1, "ExtraDeck");
                Assert.That(playerDeck.transform.position.x, Is.GreaterThan(0f));
                Assert.That(playerGrave.transform.position.x, Is.GreaterThan(0f));
                Assert.That(playerExtra.transform.position.x, Is.LessThan(0f));
                Assert.That(opponentDeck.transform.position.x, Is.LessThan(0f));
                Assert.That(opponentGrave.transform.position.x, Is.LessThan(0f));
                Assert.That(opponentExtra.transform.position.x, Is.GreaterThan(0f));
                AssertPhysicalSpecialZone(playerGrave);
                AssertPhysicalSpecialZone(playerBanish);
                AssertPhysicalSpecialZone(opponentGrave);
                AssertPhysicalSpecialZone(opponentBanish);
                AssertCompactSpecialZonePair(playerGrave, playerBanish);
                AssertCompactSpecialZonePair(opponentGrave, opponentBanish);
                Assert.That(
                    zones.Count(zone =>
                        PropertyText(zone, "Kind") == "Graveyard" ||
                        PropertyText(zone, "Kind") == "Banishment"),
                    Is.EqualTo(4),
                    "The arena must expose exactly two physical special-zone pairs.");
                Assert.That(
                    PlanarRotationDistance(
                        playerDeck.transform.position,
                        opponentDeck.transform.position),
                    Is.LessThan(0.001f));
                Assert.That(
                    PlanarRotationDistance(
                        playerExtra.transform.position,
                        opponentExtra.transform.position),
                    Is.LessThan(0.001f));
                Assert.That(
                    PlanarRotationDistance(
                        playerGrave.transform.position,
                        opponentGrave.transform.position),
                    Is.LessThan(0.001f));
                Assert.That(
                    PlanarRotationDistance(
                        playerBanish.transform.position,
                        opponentBanish.transform.position),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Component Find(
            Component[] zones,
            int owner,
            int index)
        {
            return zones.Single(zone =>
                PropertyInt(zone, "Owner") == owner &&
                PropertyText(zone, "Kind") == "Monster" &&
                PropertyInt(zone, "ZoneIndex") == index);
        }

        private static Component Find(
            Component[] zones,
            int owner,
            string kind)
        {
            return zones.Single(zone =>
                PropertyInt(zone, "Owner") == owner &&
                PropertyText(zone, "Kind") == kind);
        }

        private static Type TypeByName(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .First(type => type != null);
        }

        private static void AssertPhysicalSpecialZone(Component zone)
        {
            Assert.That(zone.gameObject.activeSelf, Is.True);
            Assert.That(zone.GetComponent<BoxCollider>(), Is.Not.Null);
            Transform baseTransform = zone.transform.Find("Base de Pedra");
            Assert.That(baseTransform, Is.Not.Null);
            Assert.That(
                baseTransform.localScale.x,
                Is.LessThan(0.9f),
                "Unity cylinders are two units wide; a scale near 1.6 would make the two fixtures overlap into one oversized circle.");
            Assert.That(zone.transform.Find("Aro Esculpido"), Is.Not.Null);
            Assert.That(zone.transform.Find("Canal de Energia"), Is.Not.Null);
            Assert.That(
                zone.GetComponentsInChildren<Renderer>(true),
                Is.Not.Empty);
            Assert.That(
                zone.GetComponentsInChildren<Renderer>(true)
                    .All(renderer =>
                        renderer.enabled &&
                        !renderer.forceRenderingOff),
                Is.True,
                "Every physical well renderer must remain visible at runtime.");
            Assert.That(
                zone.GetComponents<Component>().Any(component =>
                    component.GetType().FullName ==
                    "ArcaneArena.DuelSpecialZoneWellVisual"),
                Is.True);
            var anchor = zone.GetType()
                .GetProperty("CardPresentationAnchor")
                ?.GetValue(zone) as Transform;
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor.localPosition.y, Is.GreaterThan(0.1f));
        }

        private static void AssertCompactSpecialZonePair(
            Component graveyard,
            Component banishment)
        {
            Assert.That(
                Mathf.Abs(graveyard.transform.position.x -
                          banishment.transform.position.x),
                Is.LessThan(0.001f));
            float spacing = Mathf.Abs(
                graveyard.transform.position.z -
                banishment.transform.position.z);
            Assert.That(spacing, Is.InRange(1.1f, 1.6f));
        }

        private static float PlanarRotationDistance(
            Vector3 local,
            Vector3 opponent)
        {
            return Vector2.Distance(
                new Vector2(-local.x, -local.z),
                new Vector2(opponent.x, opponent.z));
        }

        private static int PropertyInt(Component component, string name)
        {
            object value = component.GetType().GetProperty(name)?.GetValue(component);
            return Convert.ToInt32(value);
        }

        private static string PropertyText(Component component, string name)
        {
            object value = component.GetType().GetProperty(name)?.GetValue(component);
            return value?.ToString() ?? string.Empty;
        }
    }
}
