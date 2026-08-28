using System;
using System.Collections;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private static byte _pendingStartingPlayer;
        private bool _offlinePreludeInProgress;

        private void BeginOfflineDuelPrelude()
        {
            if (_offlinePreludeInProgress)
                return;
            StartCoroutine(PlayOfflineDuelPrelude());
        }

        private IEnumerator PlayOfflineDuelPrelude()
        {
            _offlinePreludeInProgress = true;
            OnlineLoadingScreenPresenter presenter = DuelOnlineSession
                .EnsureInstance()
                .TransitionPresenter;

            if (Application.isBatchMode || HasCommandLineArgument(
                    "-arcaneSkipDuelPrelude"))
            {
                _pendingStartingPlayer = 0;
            }
            else
            {
                string opponentName = _pendingDuelMode == PendingDuelMode.Bot
                    ? _pendingBotLoadout?.displayName ?? "OPONENTE IA"
                    : "OPONENTE";
                int round = 1;
                while (true)
                {
                    DuelPreludeChoice localChoice = DuelPreludeChoice.None;
                    presenter?.ShowRockPaperScissors(
                        opponentName,
                        round,
                        choice => localChoice = choice);
                    while (localChoice == DuelPreludeChoice.None)
                        yield return null;

                    presenter?.ShowRockPaperScissorsWaiting(
                        "Escolha confirmada · revelando simultaneamente...");
                    yield return new WaitForSecondsRealtime(0.32f);
                    DuelPreludeChoice opponentChoice =
                        (DuelPreludeChoice)UnityEngine.Random.Range(1, 4);
                    DuelPreludeOutcome outcome = DuelPreludeRules.Resolve(
                        localChoice,
                        opponentChoice);
                    bool tie = outcome == DuelPreludeOutcome.Tie;
                    bool localWon = outcome == DuelPreludeOutcome.PlayerOne;
                    presenter?.ShowRockPaperScissorsResult(
                        localChoice,
                        opponentChoice,
                        localWon,
                        tie);
                    yield return new WaitForSecondsRealtime(tie ? 0.98f : 1.42f);
                    if (!tie)
                    {
                        _pendingStartingPlayer = localWon
                            ? (byte)0
                            : (byte)1;
                        break;
                    }
                    round++;
                }
            }

            presenter?.ShowDuelLoading(
                "PREPARANDO O DUELO",
                "Sincronizando decks e campo de batalha.",
                0.04f);
            if (IsActiveScene(DuelArenaSceneName) ||
                !Application.CanStreamedLevelBeLoaded(DuelArenaSceneName))
            {
                yield return SimulateLoadingProgress(presenter, 0.58f);
                _offlinePreludeInProgress = false;
                StartCoroutine(StartRequestedDuelAfterArenaReset());
                yield break;
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(
                DuelArenaSceneName,
                LoadSceneMode.Single);
            if (load == null)
            {
                _offlinePreludeInProgress = false;
                presenter?.ShowError(
                    "A arena não pôde ser carregada.",
                    () => presenter.Hide());
                yield break;
            }

            load.allowSceneActivation = false;
            float startedAt = Time.realtimeSinceStartup;
            const float minimumLoadingSeconds = 0.90f;
            while (load.progress < 0.9f ||
                   Time.realtimeSinceStartup - startedAt <
                       minimumLoadingSeconds)
            {
                float simulated = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) /
                    minimumLoadingSeconds);
                float actual = Mathf.Clamp01(load.progress / 0.9f);
                presenter?.SetProgress(Mathf.Min(
                    0.96f,
                    Mathf.Max(actual * 0.96f, simulated * 0.92f)));
                yield return null;
            }
            presenter?.SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.16f);
            load.allowSceneActivation = true;
            while (!load.isDone)
                yield return null;
            _offlinePreludeInProgress = false;
        }

        private static IEnumerator SimulateLoadingProgress(
            OnlineLoadingScreenPresenter presenter,
            float duration)
        {
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < duration)
            {
                float value = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) /
                    Mathf.Max(0.01f, duration));
                presenter?.SetProgress(Mathf.Lerp(0.04f, 1f, value));
                yield return null;
            }
            presenter?.SetProgress(1f);
        }

        private static bool HasCommandLineArgument(string expected)
        {
            return Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => string.Equals(
                    argument,
                    expected,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
