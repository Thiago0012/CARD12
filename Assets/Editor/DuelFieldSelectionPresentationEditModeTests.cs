using ArcaneArena;
using ArcaneArena.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class DuelFieldSelectionPresentationEditModeTests
    {
        [Test]
        public void ExactSelectedMonsterCanBeEmphasizedWithoutChangingIdentity()
        {
            var firstObject = new GameObject("Primeiro monstro");
            var secondObject = new GameObject("Segundo monstro");
            try
            {
                DuelZone3D first = firstObject.AddComponent<DuelZone3D>();
                DuelZone3D second = secondObject.AddComponent<DuelZone3D>();
                first.Setup(
                    DuelPlayerSide.PlayerTwo,
                    DuelZoneKind.Monster,
                    0,
                    false);
                second.Setup(
                    DuelPlayerSide.PlayerTwo,
                    DuelZoneKind.Monster,
                    1,
                    false);
                first.SetDropHighlight(true);
                second.SetDropHighlight(true);

                second.SetSelectionEmphasis(
                    true,
                    new Color(1f, 0.76f, 0.16f, 1f));

                Assert.That(first.IsSelectionEmphasized, Is.False);
                Assert.That(second.IsSelectionEmphasized, Is.True);
                Assert.That(first.ZoneIndex, Is.Zero);
                Assert.That(second.ZoneIndex, Is.EqualTo(1));

                second.SetSelectionEmphasis(false, Color.white);
                Assert.That(second.IsSelectionEmphasized, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }
    }
}
