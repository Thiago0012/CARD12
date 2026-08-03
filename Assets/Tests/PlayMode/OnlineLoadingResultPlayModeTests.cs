using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class OnlineLoadingResultPlayModeTests
    {
        [UnityTest]
        public IEnumerator LoadingCanvasBlocksTheWholeViewportWithUnscaledTime()
        {
            GameObject root = new GameObject("Online presentation test");
            float previousScale = Time.timeScale;
            try
            {
                Type type = TypeByName(
                    "ArcaneArena.Multiplayer.OnlineLoadingScreenPresenter");
                Component presenter = root.AddComponent(type);
                type.GetMethod("Show")?.Invoke(
                    presenter,
                    new object[] { "Carregando duelo...", "Teste" });
                Time.timeScale = 0f;
                for (int frame = 0; frame < 30; frame++)
                    yield return null;

                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.gameObject.activeSelf, Is.True);
                Assert.That(canvas.isRootCanvas, Is.True);
                Assert.That(canvas.sortingOrder, Is.GreaterThanOrEqualTo(32000));
                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                Assert.That(group, Is.Not.Null);
                Assert.That(group.blocksRaycasts, Is.True);
                Assert.That(group.alpha, Is.EqualTo(1f).Within(0.01f));

                Transform blocker = FindDescendant(canvas.transform, "BlackBlocker");
                Assert.That(blocker, Is.Not.Null);
                RectTransform rect = blocker as RectTransform;
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(blocker.GetComponent<Image>().raycastTarget, Is.True);
            }
            finally
            {
                Time.timeScale = previousScale;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator ResultCanvasMapsTitleAndBackButton()
        {
            GameObject root = new GameObject("Online result test");
            try
            {
                Type presenterType = TypeByName(
                    "ArcaneArena.Multiplayer.OnlineDuelResultPresenter");
                Type resultType = TypeByName(
                    "ArcaneArena.Multiplayer.OnlineDuelResultKind");
                Component presenter = root.AddComponent(presenterType);
                int returns = 0;
                object victory = Enum.Parse(resultType, "Victory");
                presenterType.GetMethod("Show")?.Invoke(
                    presenter,
                    new object[]
                    {
                        victory,
                        "Recompensa salva.",
                        new Action(() => returns++)
                    });
                yield return null;

                Text title = root.GetComponentsInChildren<Text>(true)
                    .First(text => text.gameObject.name == "ResultTitle");
                Button button = root.GetComponentInChildren<Button>(true);
                Assert.That(title.text, Is.EqualTo("VITÓRIA"));
                Assert.That(button, Is.Not.Null);
                Assert.That(button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(returns, Is.EqualTo(1));

                presenterType.GetMethod("SetReturnButtonInteractable")
                    ?.Invoke(presenter, new object[] { false });
                Assert.That(button.interactable, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == name);
        }

        private static Type TypeByName(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
