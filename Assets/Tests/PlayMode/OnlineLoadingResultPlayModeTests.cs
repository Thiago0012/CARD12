using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ArcaneDuel.Game.Competitive;
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

        [UnityTest]
        public IEnumerator RankedResultShowsProgressBeforeReturnIsEnabled()
        {
            GameObject root = new GameObject("Ranked result test");
            try
            {
                Type presenterType = TypeByName(
                    "ArcaneArena.Multiplayer.OnlineDuelResultPresenter");
                Type resultType = TypeByName(
                    "ArcaneArena.Multiplayer.OnlineDuelResultKind");
                Component presenter = root.AddComponent(presenterType);
                object victory = Enum.Parse(resultType, "Victory");
                var receipt = new RankChangeReceipt
                {
                    status = RankReceiptStatus.Applied,
                    oldPoints = 24,
                    newPoints = 31,
                    delta = 7,
                    oldTier = RankTier.Wood,
                    newTier = RankTier.Stone,
                    promoted = true
                };
                presenterType.GetMethod("ShowRanked")?.Invoke(
                    presenter,
                    new object[]
                    {
                        victory,
                        "Ranque atualizado.",
                        receipt,
                        new Action(() => { })
                    });
                yield return null;

                Transform ranked = FindDescendant(root.transform,
                    "RankedResult");
                Button returnButton = FindDescendant(root.transform,
                        "ReturnToMenuButton")
                    ?.GetComponent<Button>();
                Button skipButton = FindDescendant(root.transform,
                        "SkipRankAnimation")
                    ?.GetComponent<Button>();
                Assert.That(ranked, Is.Not.Null);
                Assert.That(ranked.gameObject.activeSelf, Is.True);
                Assert.That(returnButton, Is.Not.Null);
                Assert.That(returnButton.interactable, Is.False,
                    "O jogador não pode sair antes de ver a progressão.");
                Assert.That(skipButton, Is.Not.Null);
                Assert.That(skipButton.gameObject.activeSelf, Is.True);

                skipButton.onClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(returnButton.interactable, Is.True);
                Text transition = root.GetComponentsInChildren<Text>(true)
                    .First(text => text.gameObject.name == "Transition");
                Assert.That(transition.text,
                    Is.EqualTo("PROMOÇÃO CONCLUÍDA"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator FirstTurnChoicePanelAppearsOnlyForTheWinner()
        {
            GameObject root = new GameObject("Prelude winner-only test");
            try
            {
                Type presenterType = TypeByName(
                    "ArcaneArena.Multiplayer.OnlineLoadingScreenPresenter");
                Component presenter = root.AddComponent(presenterType);

                presenterType.GetMethod("ShowStartingPlayerWaiting")?.Invoke(
                    presenter,
                    new object[]
                    {
                        "O VENCEDOR ESTÁ DEFININDO QUEM INICIA O DUELO."
                    });
                yield return null;

                Transform panel = FindDescendant(
                    root.transform,
                    "Decisão de Primeiro Turno");
                Assert.That(panel, Is.Not.Null);
                Assert.That(
                    panel.gameObject.activeSelf,
                    Is.False,
                    "O perdedor deve ver apenas o estado de espera, nunca os botões de escolha.");

                presenterType.GetMethod("ShowStartingPlayerChoice")?.Invoke(
                    presenter,
                    new object[] { new Action<bool>(_ => { }) });
                yield return null;
                Assert.That(panel.gameObject.activeSelf, Is.True);
                Button[] choices = panel.GetComponentsInChildren<Button>(true);
                Assert.That(choices, Has.Length.EqualTo(2));
                Assert.That(choices.All(button => button.interactable), Is.True);
                Assert.That(
                    panel.GetComponentsInChildren<Graphic>(true)
                        .Any(graphic => graphic.GetType().Name ==
                                        "ArcaneShopSurfaceGraphic"),
                    Is.True,
                    "A decisão deve usar a mesma superfície moderna da loja.");
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
