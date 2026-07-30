using System.Collections;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class ProjectBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator SceneMarkerCanBeCreatedAtRuntime()
        {
            var gameObject = new GameObject("TestSceneMarker");
            SceneMarker marker = gameObject.AddComponent<SceneMarker>();
            marker.Configure(SceneRole.CardLab);

            yield return null;

            Assert.That(marker.Role, Is.EqualTo(SceneRole.CardLab));
            Object.Destroy(gameObject);
        }
    }
}

