using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class MonsterSummonEffectPolicyEditModeTests
    {
        private const string ComplexEffect =
            "Uma vez por turno: voce pode escolher uma carta no campo; " +
            "negue seus efeitos e, depois, se esta carta ainda estiver no " +
            "campo, destrua o alvo escolhido.";

        [Test]
        public void FusionUsesOnlyPurpleAndSynchroUsesOnlyBlue()
        {
            Assert.That(Resolve("Fusion", 1, string.Empty), Is.EqualTo("Purple"));
            Assert.That(Resolve("Synchro", 12, ComplexEffect), Is.EqualTo("Blue"));
        }

        [Test]
        public void LinkNeverUsesBlueOrYellow()
        {
            Assert.That(Resolve("Link", 8, ComplexEffect), Is.EqualTo("None"));
        }

        [Test]
        public void YellowRequiresEffectFrameLevelSixAndComplexText()
        {
            Assert.That(Resolve("Effect", 6, ComplexEffect), Is.EqualTo("Yellow"));
            Assert.That(Resolve("Effect", 5, ComplexEffect), Is.EqualTo("None"));
            Assert.That(Resolve("Effect", 8, "Efeito curto."), Is.EqualTo("None"));
            Assert.That(Resolve("Normal", 8, ComplexEffect), Is.EqualTo("None"));
            Assert.That(Resolve("Ritual", 8, ComplexEffect), Is.EqualTo("Yellow"));
            Assert.That(Resolve("Xyz", 8, ComplexEffect), Is.EqualTo("None"));
        }

        [TestCase("Duel/SummonEffects/EffectYellow")]
        [TestCase("Duel/SummonEffects/EffectBlue")]
        [TestCase("Duel/SummonEffects/EffectPurple")]
        public void SummonEffectSpriteIsImportable(string resourcePath)
        {
            Assert.That(Resources.Load<Sprite>(resourcePath), Is.Not.Null);
        }

        [Test]
        public void AlternativeSummonMethodsHaveDistinctVisualPalettes()
        {
            Type palette = Type.GetType(
                "ArcaneArena.Presentation.SummonMethodVfxPalette, " +
                "Assembly-CSharp");
            Type frame = Type.GetType(
                "ArcaneArena.Cards.MonsterFrameKind, Assembly-CSharp");
            Assert.That(palette, Is.Not.Null);
            Assert.That(frame, Is.Not.Null);

            MethodInfo supports = palette.GetMethod(
                "Supports",
                BindingFlags.Static | BindingFlags.Public);
            MethodInfo primary = palette.GetMethod(
                "Primary",
                BindingFlags.Static | BindingFlags.Public);
            MethodInfo secondary = palette.GetMethod(
                "Secondary",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(supports, Is.Not.Null);
            Assert.That(primary, Is.Not.Null);
            Assert.That(secondary, Is.Not.Null);

            var primaryColors = new HashSet<Color>();
            foreach (string frameName in new[]
                     {
                         "Fusion",
                         "Synchro",
                         "Xyz",
                         "Link",
                         "Pendulum"
                     })
            {
                object frameValue = Enum.Parse(frame, frameName);
                Assert.That(
                    supports.Invoke(null, new[] { frameValue }),
                    Is.EqualTo(true),
                    $"{frameName} must own a summon visual identity.");
                Color first = (Color)primary.Invoke(
                    null,
                    new[] { frameValue });
                Color second = (Color)secondary.Invoke(
                    null,
                    new[] { frameValue });
                Assert.That(
                    first,
                    Is.Not.EqualTo(second),
                    $"{frameName} needs a two-tone animated palette.");
                primaryColors.Add(first);
            }

            Assert.That(
                primaryColors.Count,
                Is.EqualTo(5),
                "Each summon method must remain visually distinguishable.");
        }

        [Test]
        public void OrdinaryMonsterFramesDoNotTriggerMethodSigils()
        {
            Type palette = Type.GetType(
                "ArcaneArena.Presentation.SummonMethodVfxPalette, " +
                "Assembly-CSharp");
            Type frame = Type.GetType(
                "ArcaneArena.Cards.MonsterFrameKind, Assembly-CSharp");
            MethodInfo supports = palette?.GetMethod(
                "Supports",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(supports, Is.Not.Null);

            foreach (string frameName in new[]
                     {
                         "Normal",
                         "Effect",
                         "Ritual",
                         "Token"
                     })
            {
                object frameValue = Enum.Parse(frame, frameName);
                Assert.That(
                    supports.Invoke(null, new[] { frameValue }),
                    Is.EqualTo(false));
            }
        }

        [TestCase(0x00040008U, "Fusion")]
        [TestCase(0x00080008U, "Synchro")]
        [TestCase(0x00200008U, "Xyz")]
        [TestCase(0x10000008U, "Link")]
        public void CoreMaterialReasonSelectsTheMatchingCinematic(
            uint reason,
            string expectedFrame)
        {
            Type arena = Type.GetType(
                "ArcaneArena.CardArenaBootstrap, Assembly-CSharp");
            MethodInfo classifier = arena?.GetMethod(
                "MaterialFrameForReason",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(classifier, Is.Not.Null);
            Assert.That(
                classifier.Invoke(null, new object[] { reason })?.ToString(),
                Is.EqualTo(expectedFrame));
        }

        [Test]
        public void FusionMaterialFormationUsesRequestedTwoThreeAndFiveLayouts()
        {
            Type arena = Type.GetType(
                "ArcaneArena.CardArenaBootstrap, Assembly-CSharp");
            Type frame = Type.GetType(
                "ArcaneArena.Cards.MonsterFrameKind, Assembly-CSharp");
            MethodInfo layout = arena?.GetMethod(
                "SummonMaterialOrigins",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(layout, Is.Not.Null);
            object fusion = Enum.Parse(frame, "Fusion");

            Vector2[] two = (Vector2[])layout.Invoke(
                null,
                new[] { (object)2, fusion });
            Vector2[] three = (Vector2[])layout.Invoke(
                null,
                new[] { (object)3, fusion });
            Vector2[] five = (Vector2[])layout.Invoke(
                null,
                new[] { (object)5, fusion });

            Assert.That(two.Length, Is.EqualTo(2));
            Assert.That(two[0].x, Is.LessThan(0f));
            Assert.That(two[1].x, Is.GreaterThan(0f));
            Assert.That(two[0].y, Is.EqualTo(two[1].y).Within(0.01f));
            Assert.That(three.Length, Is.EqualTo(3));
            Assert.That(three[0].y, Is.GreaterThan(three[1].y));
            Assert.That(three[1].x, Is.LessThan(0f));
            Assert.That(three[2].x, Is.GreaterThan(0f));
            Assert.That(five.Length, Is.EqualTo(5));
            for (int index = 0; index < five.Length; index++)
            {
                Assert.That(
                    five[index].magnitude,
                    Is.GreaterThan(220f),
                    "Five materials must surround the merge point.");
            }
        }

        [Test]
        public void AttackArrowOwnsCurveHeadGlowAndQualityScaledPulses()
        {
            Type arrowType = Type.GetType(
                "ArcaneArena.Presentation.DuelAttackArrowVfx, " +
                "Assembly-CSharp");
            Assert.That(arrowType, Is.Not.Null);
            var root = new GameObject("Teste da seta de ataque");
            try
            {
                LineRenderer body = root.AddComponent<LineRenderer>();
                Component arrow = root.AddComponent(arrowType);
                MethodInfo configure = arrowType.GetMethod(
                    "Configure",
                    BindingFlags.Instance | BindingFlags.Public);
                MethodInfo endpoints = arrowType.GetMethod(
                    "SetEndpoints",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(configure, Is.Not.Null);
                Assert.That(endpoints, Is.Not.Null);
                configure.Invoke(
                    arrow,
                    new object[]
                    {
                        body,
                        Color.red,
                        Color.yellow,
                        Color.gray
                    });
                endpoints.Invoke(
                    arrow,
                    new object[]
                    {
                        Vector3.zero,
                        new Vector3(4f, 0f, 6f),
                        true,
                        false
                    });

                Assert.That(body.positionCount, Is.GreaterThan(2));
                Assert.That(
                    root.GetComponentsInChildren<LineRenderer>().Length,
                    Is.GreaterThanOrEqualTo(6),
                    "The trajectory needs body, halo, arrow head and pulses.");
                Assert.That(
                    arrowType.GetProperty("ActivePulseCount")
                        ?.GetValue(arrow),
                    Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string Resolve(
            string frameName,
            int level,
            string effectText)
        {
            Type policy = Type.GetType(
                "ArcaneArena.Cards.MonsterSummonEffectPolicy, Assembly-CSharp");
            Type frame = Type.GetType(
                "ArcaneArena.Cards.MonsterFrameKind, Assembly-CSharp");
            Assert.That(policy, Is.Not.Null);
            Assert.That(frame, Is.Not.Null);
            MethodInfo resolve = policy.GetMethod(
                "Resolve",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { frame, typeof(int), typeof(string) },
                null);
            Assert.That(resolve, Is.Not.Null);
            object frameValue = Enum.Parse(frame, frameName);
            return resolve.Invoke(
                    null,
                    new[] { frameValue, (object)level, effectText })
                ?.ToString();
        }
    }
}
