using System;
using System.Linq;
using System.Reflection;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class MobileOptimizationEditModeTests
    {
        [Test]
        public void DeckCatalogGestureSeparatesVerticalScrollFromCardDrag()
        {
            Type dragType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "ArcaneArena.Frontend.DeckEditorCardDrag",
                    false))
                .FirstOrDefault(type => type != null);
            Assert.That(dragType, Is.Not.Null);
            MethodInfo prefersScroll = dragType.GetMethod(
                "PrefersCatalogScroll",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(prefersScroll, Is.Not.Null);
            Assert.That(
                prefersScroll.Invoke(null, new object[]
                    { new Vector2(5f, 70f) }),
                Is.True);
            Assert.That(
                prefersScroll.Invoke(null, new object[]
                    { new Vector2(70f, 5f) }),
                Is.False);
            Assert.That(
                prefersScroll.Invoke(null, new object[] { Vector2.zero }),
                Is.True,
                "Um toque ainda sem deslocamento deve favorecer a rolagem e não iniciar um arraste acidental.");
        }

        [Test]
        public void AutomaticQualityProtectsLimitedPhonesAndKeepsPcDetailed()
        {
            Assert.That(
                ArcaneGraphicsPreferences.ResolveAutomaticQuality(
                    false,
                    16384,
                    8192,
                    50,
                    12),
                Is.EqualTo(ArcaneGraphicsQuality.High));
            Assert.That(
                ArcaneGraphicsPreferences.ResolveAutomaticQuality(
                    true,
                    3072,
                    768,
                    35,
                    4),
                Is.EqualTo(ArcaneGraphicsQuality.VeryLow));
            Assert.That(
                ArcaneGraphicsPreferences.ResolveAutomaticQuality(
                    true,
                    4096,
                    1536,
                    45,
                    6),
                Is.EqualTo(ArcaneGraphicsQuality.Low));
            Assert.That(
                ArcaneGraphicsPreferences.ResolveAutomaticQuality(
                    true,
                    8192,
                    4096,
                    50,
                    8),
                Is.EqualTo(ArcaneGraphicsQuality.Medium));
        }

        [Test]
        public void AllFiveGraphicsLevelsHaveReadablePortugueseNames()
        {
            Assert.That(
                ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsQuality.VeryLow),
                Is.EqualTo("MUITO BAIXO"));
            Assert.That(
                ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsQuality.Low),
                Is.EqualTo("BAIXO"));
            Assert.That(
                ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsQuality.Medium),
                Is.EqualTo("MÉDIO"));
            Assert.That(
                ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsQuality.High),
                Is.EqualTo("ALTO"));
            Assert.That(
                ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsQuality.VeryHigh),
                Is.EqualTo("MUITO ALTO"));
        }

        [Test]
        public void EditorWithFourGigabytesOfGraphicsMemory_IsCappedAtMedium()
        {
            ArcaneGraphicsQuality result =
                ArcaneGraphicsPreferences.ApplyHardwareSafetyLimit(
                    ArcaneGraphicsQuality.VeryHigh,
                    true,
                    4096);

            Assert.That(result, Is.EqualTo(ArcaneGraphicsQuality.Medium));
        }

        [Test]
        public void PlayerBuild_KeepsRequestedGraphicsQuality()
        {
            ArcaneGraphicsQuality result =
                ArcaneGraphicsPreferences.ApplyHardwareSafetyLimit(
                    ArcaneGraphicsQuality.VeryHigh,
                    false,
                    2048);

            Assert.That(result, Is.EqualTo(ArcaneGraphicsQuality.VeryHigh));
        }
    }
}
