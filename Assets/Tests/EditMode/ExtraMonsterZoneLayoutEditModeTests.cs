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

        private static Type TypeByName(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .First(type => type != null);
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
