using System;
using System.Collections;
using System.IO;
using ArcaneArena.Frontend;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Opt-in two-process crossplay diagnostic. It is inert during normal
    /// gameplay and only starts when -arcaneMultiplayerSmokeRole is present.
    /// </summary>
    internal sealed class DuelMultiplayerSmokeAutomation : MonoBehaviour
    {
        private const string RoleArgument =
            "-arcaneMultiplayerSmokeRole";
        private const string RoomFileArgument =
            "-arcaneMultiplayerSmokeRoomFile";
        private const string ResultFileArgument =
            "-arcaneMultiplayerSmokeResult";
        private const float DefaultTimeoutSeconds = 180f;

        [Serializable]
        private sealed class SmokeResult
        {
            public string role;
            public bool success;
            public string stage;
            public string roomCode;
            public string scene;
            public string status;
            public ulong stateVersion;
            public uint acceptedRemoteCommands;
            public int submittedLocalActions;
            public int localHand;
            public int opponentHand;
            public int localDeck;
            public int opponentDeck;
            public bool decisionLocked;
            public bool hasPrompt;
            public ulong promptRequestId;
            public int promptPlayer;
            public int promptMessage;
            public int promptChoices;
        }

        private string role;
        private string roomFile;
        private string resultFile;
        private int submittedLocalActions;
        private ulong submittedRequestId;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenRequested()
        {
            string requestedRole = ArgumentValue(RoleArgument);
            if (!IsRole(requestedRole) ||
                FindFirstObjectByType<DuelMultiplayerSmokeAutomation>() != null)
            {
                return;
            }

            var root = new GameObject("Arcane Multiplayer Smoke Automation");
            DontDestroyOnLoad(root);
            root.AddComponent<DuelMultiplayerSmokeAutomation>();
        }
#endif

        private IEnumerator Start()
        {
            role = ArgumentValue(RoleArgument).Trim().ToLowerInvariant();
            roomFile = ArgumentValue(RoomFileArgument);
            resultFile = ArgumentValue(ResultFileArgument);
            if (!IsRole(role) || string.IsNullOrWhiteSpace(roomFile) ||
                string.IsNullOrWhiteSpace(resultFile))
            {
                Finish(false, "invalid-arguments", null, 2);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup +
                             DefaultTimeoutSeconds;
            WriteProgress("starting", null);

            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    ProjectIdentity.MainMenuScene,
                    StringComparison.OrdinalIgnoreCase) &&
                Application.CanStreamedLevelBeLoaded(
                    ProjectIdentity.MainMenuScene))
            {
                SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            }

            while (GameFrontendBootstrap.Instance == null &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (GameFrontendBootstrap.Instance == null)
            {
                Finish(false, "frontend-timeout", null, 3);
                yield break;
            }

            while (Time.realtimeSinceStartup < deadline &&
                   !GameFrontendBootstrap.Instance.TryGetSelectedDuelLoadout(
                       out _,
                       out _))
            {
                yield return new WaitForSecondsRealtime(0.25f);
            }

            DuelOnlineSession session = DuelOnlineSession.EnsureInstance();
            if (role == "host")
            {
                session.BeginHostingForDiagnostics();
                while (string.IsNullOrWhiteSpace(session.RoomCode) &&
                       Time.realtimeSinceStartup < deadline)
                {
                    WriteProgress("host-creating-room", session);
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                if (string.IsNullOrWhiteSpace(session.RoomCode))
                {
                    Finish(false, "host-room-timeout", session, 4);
                    yield break;
                }
                WriteRoomCode(session.RoomCode);
            }
            else
            {
                string code = string.Empty;
                while (Time.realtimeSinceStartup < deadline &&
                       !TryReadRoomCode(out code))
                {
                    WriteProgress("client-waiting-room-code", session);
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                if (string.IsNullOrWhiteSpace(code))
                {
                    Finish(false, "client-room-code-timeout", session, 5);
                    yield break;
                }
                session.BeginJoiningForDiagnostics(code);
            }

            float nextActionTime = 0f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (role == "host" && session.DiagnosticCanBeginMatch)
                    session.BeginMatchForDiagnostics();

                DuelArenaController controller =
                    session.DiagnosticController;
                if (session.DiagnosticArenaSynchronized &&
                    controller != null &&
                    !controller.PresentationDecisionLocked &&
                    Time.realtimeSinceStartup >= nextActionTime)
                {
                    DuelPrompt prompt = controller.CurrentPrompt;
                    bool ownsPrompt = prompt != null && prompt.Player == 0 &&
                                      prompt.RequestId != 0 &&
                                      prompt.RequestId != submittedRequestId;
                    if (ownsPrompt)
                    {
                        DuelChoice choice =
                            DeterministicDuelPolicy.Choose(prompt);
                        if (choice != null)
                        {
                            submittedRequestId = prompt.RequestId;
                            submittedLocalActions++;
                            controller.SubmitChoice(choice);
                            nextActionTime =
                                Time.realtimeSinceStartup + 0.4f;
                        }
                    }
                }

                if (HasSucceeded(session, controller))
                {
                    WriteProgress("passed", session);
                    // Allow the final command acknowledgement and result
                    // file to reach the other live process before exiting.
                    yield return new WaitForSecondsRealtime(3f);
                    Finish(true, "passed", session, 0);
                    yield break;
                }

                WriteProgress(
                    session.DiagnosticMatchStarted
                        ? "playing-two-live-peers"
                        : "lobby-handshake",
                    session);
                yield return new WaitForSecondsRealtime(0.2f);
            }

            Finish(false, "smoke-timeout", session, 6);
        }

        private bool HasSucceeded(
            DuelOnlineSession session,
            DuelArenaController controller)
        {
            DuelPresentationState state = controller?.PresentationState;
            bool fieldLoaded = state?.Players != null &&
                               state.Players.Length == 2 &&
                               state.Players[0].DeckCount > 0 &&
                               state.Players[1].DeckCount > 0 &&
                               state.Players[0].Hand.Count > 0 &&
                               state.Players[1].Hand.Count > 0;
            bool bothActionsConfirmed = role == "host"
                ? submittedLocalActions > 0 &&
                  session.DiagnosticAcceptedRemoteCommands > 0
                : submittedLocalActions > 0 &&
                  session.DiagnosticLocalCommandAcknowledged;
            return session.DiagnosticArenaSynchronized && fieldLoaded &&
                   bothActionsConfirmed;
        }

        private void WriteRoomCode(string code)
        {
            string directory = Path.GetDirectoryName(roomFile);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(roomFile, code.Trim().ToUpperInvariant());
        }

        private bool TryReadRoomCode(out string code)
        {
            code = string.Empty;
            try
            {
                if (!File.Exists(roomFile))
                    return false;
                code = File.ReadAllText(roomFile).Trim().ToUpperInvariant();
                return code.Length >= 6 && code.Length <= 12;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void WriteProgress(
            string stage,
            DuelOnlineSession session)
        {
            try
            {
                DuelArenaController controller =
                    session?.DiagnosticController;
                DuelPresentationState state = controller?.PresentationState;
                DuelPrompt prompt = controller?.CurrentPrompt;
                var result = new SmokeResult
                {
                    role = role,
                    success = stage == "passed",
                    stage = stage,
                    roomCode = session?.RoomCode ?? string.Empty,
                    scene = SceneManager.GetActiveScene().name,
                    status = session?.Status ?? string.Empty,
                    stateVersion = session?.DiagnosticStateVersion ?? 0,
                    acceptedRemoteCommands =
                        session?.DiagnosticAcceptedRemoteCommands ?? 0,
                    submittedLocalActions = submittedLocalActions,
                    localHand = state?.Players?[0].Hand.Count ?? 0,
                    opponentHand = state?.Players?[1].Hand.Count ?? 0,
                    localDeck = state?.Players?[0].DeckCount ?? 0,
                    opponentDeck = state?.Players?[1].DeckCount ?? 0,
                    decisionLocked =
                        controller?.PresentationDecisionLocked ?? false,
                    hasPrompt = prompt != null,
                    promptRequestId = prompt?.RequestId ?? 0,
                    promptPlayer = prompt?.Player ?? -1,
                    promptMessage = (int)(prompt?.Message ?? 0),
                    promptChoices = prompt?.Choices.Count ?? 0
                };
                string directory = Path.GetDirectoryName(resultFile);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                string temporary = resultFile + ".writing-" + role;
                File.WriteAllText(
                    temporary,
                    JsonUtility.ToJson(result, true));
                File.Copy(temporary, resultFile, true);
                File.Delete(temporary);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void Finish(
            bool success,
            string stage,
            DuelOnlineSession session,
            int exitCode)
        {
            WriteProgress(stage, session);
            Debug.Log(
                $"[MP-SMOKE] role={role} success={success} stage={stage} " +
                $"state={session?.DiagnosticStateVersion ?? 0} " +
                $"actions={submittedLocalActions}");
            Application.Quit(exitCode);
        }

        private static bool IsRole(string value)
        {
            return string.Equals(value, "host", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "client", StringComparison.OrdinalIgnoreCase);
        }

        private static string ArgumentValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(
                        arguments[index],
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1] ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
