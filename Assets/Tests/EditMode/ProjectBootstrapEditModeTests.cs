using ArcaneDuel.DuelEngine;
using ArcaneDuel.Game;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ProjectBootstrapEditModeTests
    {
        [Test]
        public void ProjectUsesRequiredUnityAndRulesAuthority()
        {
            Assert.That(ProjectIdentity.UnityVersion, Is.EqualTo("6000.5.0f1"));
            Assert.That(DuelEngineModule.Authority, Is.EqualTo("ygopro-core"));
        }
    }
}

