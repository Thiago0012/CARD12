using System.IO;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class MobileLandscapeOrientationEditModeTests
    {
        [Test]
        public void MobileBuildAllowsOnlyTheTwoLandscapeOrientations()
        {
            string settings = File.ReadAllText(
                "ProjectSettings/ProjectSettings.asset");
            Assert.That(settings,
                Does.Contain("defaultScreenOrientation: 4"));
            Assert.That(settings,
                Does.Contain("allowedAutorotateToPortrait: 0"));
            Assert.That(settings,
                Does.Contain("allowedAutorotateToPortraitUpsideDown: 0"));
            Assert.That(settings,
                Does.Contain("allowedAutorotateToLandscapeRight: 1"));
            Assert.That(settings,
                Does.Contain("allowedAutorotateToLandscapeLeft: 1"));
            Assert.That(settings, Does.Contain("useOSAutorotation: 0"));
        }
    }
}
