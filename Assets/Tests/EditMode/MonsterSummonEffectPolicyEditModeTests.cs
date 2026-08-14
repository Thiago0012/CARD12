using System;
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
