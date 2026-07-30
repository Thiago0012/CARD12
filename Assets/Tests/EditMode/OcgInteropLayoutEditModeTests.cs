using System;
using System.Runtime.InteropServices;
using ArcaneDuel.DuelEngine.Core;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class OcgInteropLayoutEditModeTests
    {
        [Test]
        public void NativeLayoutsMatchPinnedOcgcoreApi11()
        {
            var assembly = typeof(OcgDuelEngine).Assembly;
            AssertSize(assembly.GetType("ArcaneDuel.DuelEngine.Interop.OcgCardData", true), 64);
            AssertSize(assembly.GetType("ArcaneDuel.DuelEngine.Interop.OcgPlayer", true), 12);
            AssertSize(assembly.GetType("ArcaneDuel.DuelEngine.Interop.OcgDuelOptions", true), 136);
            AssertSize(assembly.GetType("ArcaneDuel.DuelEngine.Interop.OcgNewCardInfo", true), 24);
            AssertSize(assembly.GetType("ArcaneDuel.DuelEngine.Interop.OcgQueryInfo", true), 20);

            Type cardData = assembly.GetType("ArcaneDuel.DuelEngine.Interop.OcgCardData", true);
            Assert.That(Marshal.OffsetOf(cardData, "Setcodes").ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf(cardData, "Race").ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf(cardData, "LinkMarker").ToInt32(), Is.EqualTo(56));
        }

        private static void AssertSize(Type type, int expected)
        {
            Assert.That(Marshal.SizeOf(type), Is.EqualTo(expected), type.FullName);
        }
    }
}
