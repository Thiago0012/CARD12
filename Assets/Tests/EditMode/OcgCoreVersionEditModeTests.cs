using ArcaneDuel.DuelEngine.Diagnostics;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class OcgCoreVersionEditModeTests
    {
        [Test]
        public void PinnedNativeCoreReportsExpectedApiVersion()
        {
            OcgCoreVersion version = OcgCoreVersionProbe.Read();

            Assert.That(version.Major, Is.EqualTo(11));
            Assert.That(version.Minor, Is.EqualTo(0));
        }
    }
}
