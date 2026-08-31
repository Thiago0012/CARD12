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
        [Test]
        public void RankMasterySpinFinishesInTheOfficialUprightOrientation()
        {
            Type rules = TypeByName(
                "ArcaneArena.DuelRankMasteryPresentationRules");
            FieldInfo duration = rules.GetField(
                "SpinDuration",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo resolve = rules.GetMethod(
                "ResolveSpinDegrees",
                BindingFlags.Public | BindingFlags.Static);
            float finalRotation = (float)resolve.Invoke(
                null,
                new object[] { (float)duration.GetRawConstantValue() });
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(0f, finalRotation)),
                Is.LessThan(0.01f),
                "O emblema precisa terminar o giro na orientação original.");
        }

        [Test]
        public void PromotionCinematicNormalizesLargeBadgesInsideTheCamera()
        {
            Type cinematic = TypeByName(
                "ArcaneArena.Multiplayer.RankPromotionCinematic");
            MethodInfo resolve = cinematic.GetMethod(
                "ResolveBadgeScale",
                BindingFlags.Public | BindingFlags.Static);
            FieldInfo targetSize = cinematic.GetField(
                "BadgeTargetWorldSize",
                BindingFlags.Public | BindingFlags.Static);
            Sprite badge = Resources.Load<Sprite>(
                "Frontend/Ranked/Badges/Gold");
            Assert.That(badge, Is.Not.Null);
            float scale = (float)resolve.Invoke(null, new object[] { badge });
            float largestSide = Mathf.Max(
                badge.bounds.size.x,
                badge.bounds.size.y);
            Assert.That(scale, Is.LessThan(1f),
                "As artes grandes não podem manter a escala fixa que cortava suas bordas.");
            Assert.That(
                largestSide * scale,
                Is.EqualTo((float)targetSize.GetRawConstantValue()).Within(0.001f));
        }

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
                Assert.That(FindDescendant(root.transform,
                    "CurrentRankLarge"), Is.Null,
                    "O resultado ranqueado deve mostrar somente elo atual e próximo elo.");
                Text currentRank = FindDescendant(root.transform,
                        "CurrentRankMiniLabel")?.GetComponent<Text>();
                Text nextRank = FindDescendant(root.transform,
                        "NextRankMiniLabel")?.GetComponent<Text>();
                Assert.That(currentRank?.text, Does.StartWith("ELO ATUAL"));
                Assert.That(nextRank?.text, Does.StartWith("PRÓXIMO ELO"));
                RectTransform fullscreen = FindDescendant(root.transform,
                    "RankPromotionFullscreen") as RectTransform;
                Assert.That(fullscreen, Is.Not.Null);
                Assert.That(fullscreen.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(fullscreen.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(fullscreen.parent, Is.EqualTo(
                    root.GetComponentInChildren<Canvas>(true).transform),
                    "A promoção deve cobrir o Canvas 1920x1080 inteiro, não apenas o painel ranqueado.");
                RectTransform cinematicViewport = FindDescendant(
                    root.transform,
                    "RankPromotionCinematicViewport") as RectTransform;
                Assert.That(cinematicViewport, Is.Not.Null);
                Assert.That(cinematicViewport.anchorMin,
                    Is.EqualTo(Vector2.zero));
                Assert.That(cinematicViewport.anchorMax,
                    Is.EqualTo(Vector2.one));
                Assert.That(cinematicViewport.offsetMin,
                    Is.EqualTo(Vector2.zero));
                Assert.That(cinematicViewport.offsetMax,
                    Is.EqualTo(Vector2.zero));
                Assert.That(cinematicViewport.GetComponent<Mask>(), Is.Null);
                Assert.That(cinematicViewport.GetComponent<RectMask2D>(), Is.Null,
                    "A cena do elo não pode ser recortada por uma máscara quadrada.");

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
        public IEnumerator VictorySummaryShowsCoinsPerformanceAndMissionProgress()
        {
            GameObject root = new GameObject("Victory summary test");
            try
            {
                Type presenterType = TypeByName(
                    "ArcaneArena.Multiplayer.OnlineDuelResultPresenter");
                Type resultType = TypeByName(
                    "ArcaneArena.Multiplayer.OnlineDuelResultKind");
                Type summaryType = TypeByName(
                    "ArcaneArena.Multiplayer.DuelResultSummary");
                Type missionType = TypeByName(
                    "ArcaneArena.Multiplayer.DuelResultMissionSummary");
                Component presenter = root.AddComponent(presenterType);
                object summary = Activator.CreateInstance(summaryType);
                summaryType.GetField("showsCoinReward")
                    ?.SetValue(summary, true);
                summaryType.GetField("coinsEarned")?.SetValue(summary, 42);
                summaryType.GetField("balanceAfter")?.SetValue(summary, 318);
                summaryType.GetField("damageDealt")?.SetValue(summary, 5600L);
                summaryType.GetField("damageReceived")
                    ?.SetValue(summary, 1400L);
                summaryType.GetField("roundsOrTurns")
                    ?.SetValue(summary, 7);
                object mission = Activator.CreateInstance(missionType);
                missionType.GetField("name")
                    ?.SetValue(mission, "VITÓRIA IMPECÁVEL");
                missionType.GetField("current")?.SetValue(mission, 3L);
                missionType.GetField("target")?.SetValue(mission, 5L);
                ((IList)summaryType.GetField("missions")
                    ?.GetValue(summary))?.Add(mission);

                object victory = Enum.Parse(resultType, "Victory");
                presenterType.GetMethod("ShowWithSummary")?.Invoke(
                    presenter,
                    new object[]
                    {
                        victory,
                        "Resultado confirmado.",
                        summary,
                        new Action(() => { })
                    });
                yield return null;

                Transform panel = FindDescendant(root.transform,
                    "DuelResultSummary");
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.gameObject.activeSelf, Is.True);
                Text coinText = FindDescendant(panel,
                        "DuelResultStat1")
                    ?.GetComponentInChildren<Text>(true);
                Assert.That(coinText?.text, Does.Contain("+42"));
                Assert.That(coinText?.text, Does.Contain("SALDO 318"));
                Text missionText = FindDescendant(panel,
                        "DuelResultMission1")
                    ?.GetComponentInChildren<Text>(true);
                Assert.That(missionText?.text,
                    Does.Contain("VITÓRIA IMPECÁVEL"));
                Image missionFill = FindDescendant(panel,
                        "MissionProgressFill")
                    ?.GetComponent<Image>();
                Assert.That(missionFill?.fillAmount,
                    Is.EqualTo(0.6f).Within(0.01f));
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
