using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ArcaneDuel.Editor.Validation
{
    /// <summary>
    /// Runs the complete EditMode and PlayMode project suites through the
    /// already-open Unity Editor. Requests, state and results are stored only
    /// under the project root so the gate is reproducible without batch mode.
    /// A completed/cancelled state never restarts a previous request.
    /// </summary>
    [InitializeOnLoad]
    public static class FieldComplianceGateRunner
    {
        private static readonly string[] EditModeGateTests =
        {
            "ArcaneDuel.Tests.EditMode.CoreProtocolEditModeTests",
            "ArcaneDuel.Tests.EditMode.DuelWireProtocolEditModeTests",
            "ArcaneDuel.Tests.EditMode.DuelStateCountEditModeTests",
            "ArcaneDuel.Tests.EditMode.StabilizationRegressionEditModeTests",
            "ArcaneDuel.Tests.EditMode.ExtraDeckSummonEditModeTests",
            "ArcaneDuel.Tests.EditMode.MultiplayerStateRepairEditModeTests",
            "ArcaneDuel.Tests.EditMode.MultiplayerCrossplayEditModeTests",
            "ArcaneDuel.Tests.EditMode.DuelEffectDescriptionResolverEditModeTests",
            "ArcaneDuel.Tests.EditMode.NinjaCardEffectSemanticsEditModeTests",
            "ArcaneDuel.Tests.EditMode.CardAudit.CardScenarioRunnerEditModeTests",
            "ArcaneDuel.Tests.EditMode.OcgHeadlessDuelEditModeTests",
            "ArcaneDuel.Tests.EditMode.RankPointServiceEditModeTests",
            "ArcaneDuel.Tests.EditMode.TournamentManagerEditModeTests"
        };

        private static readonly string[] PlayModeGateTests =
        {
            "ArcaneDuel.Tests.PlayMode.ArenaStabilizationPlayModeTests",
            "ArcaneDuel.Tests.PlayMode.MultiplayerCrossplayPlayModeTests",
            "ArcaneDuel.Tests.PlayMode.PlayableArenaPlayModeTests"
        };

        private const string RequestName = "codex-phase3-6.request";
        private const string StateName = "codex-phase3-6.state";
        private const string EditStage = "edit";
        private const string PlayStage = "play";
        private static bool callbacksRegistered;
        private static double nextPollTime;

        static FieldComplianceGateRunner()
        {
            RestoreCallbacksForActiveRun();
            EditorApplication.delayCall += ContinueRequestedRun;
            EditorApplication.update += PollForRequest;
        }

        private static void RestoreCallbacksForActiveRun()
        {
            if (!File.Exists(RequestPath) || !File.Exists(StatePath))
                return;

            string state = File.ReadAllText(StatePath).Trim();
            if (state == "edit-running")
                EnsureCallbacks(EditStage);
            else if (state == "play-running")
                EnsureCallbacks(PlayStage);
        }

        private static void PollForRequest()
        {
            if (EditorApplication.timeSinceStartup < nextPollTime)
                return;
            nextPollTime = EditorApplication.timeSinceStartup + 1.0;
            if (File.Exists(RequestPath))
                ContinueRequestedRun();
        }

        [MenuItem("Arcane Duel/Validation/Run Field Compliance Phases 3-6")]
        public static void RunFromMenu()
        {
            Directory.CreateDirectory(ResultDirectory);
            File.WriteAllText(
                RequestPath,
                DateTime.UtcNow.ToString("O"));
            File.WriteAllText(StatePath, "pending-edit");
            EditorApplication.delayCall += ContinueRequestedRun;
        }

        private static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            Application.dataPath;

        private static string ResultDirectory =>
            Path.Combine(ProjectRoot, "TestResults");

        private static string RequestPath =>
            Path.Combine(ResultDirectory, RequestName);

        private static string StatePath =>
            Path.Combine(ResultDirectory, StateName);

        private static void ContinueRequestedRun()
        {
            if (!File.Exists(RequestPath) ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            Directory.CreateDirectory(ResultDirectory);
            string state = File.Exists(StatePath)
                ? File.ReadAllText(StatePath).Trim()
                : "pending-edit";
            if (state == "done")
                return;

            if (state == "restore-main-menu")
            {
                RestoreMainMenuAndDeleteGeneratedTestScenes();
                File.WriteAllText(StatePath, "restored-main-menu");
                File.Delete(RequestPath);
                return;
            }

            if (state == "cancel-active")
            {
                int cancelled = CancelAllActiveTestRuns();
                File.AppendAllText(
                    SummaryPath,
                    $"CANCEL active-runs={cancelled} " +
                    $"{DateTime.UtcNow:O}{Environment.NewLine}");
                File.WriteAllText(StatePath, "cancelled");
                File.Delete(RequestPath);
                return;
            }

            if (state == "edit-running")
            {
                EnsureCallbacks(EditStage);
                return;
            }
            if (state == "play-running")
            {
                EnsureCallbacks(PlayStage);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += ContinueRequestedRun;
                return;
            }

            if (state == "pending-play")
                StartStage(PlayStage, TestMode.PlayMode);
            else
                StartStage(EditStage, TestMode.EditMode);
        }

        private static void StartStage(string stage, TestMode mode)
        {
            callbacksRegistered = false;
            File.WriteAllText(StatePath, stage + "-running");
            File.AppendAllText(
                SummaryPath,
                $"START {stage} {DateTime.UtcNow:O}{Environment.NewLine}");
            EnsureCallbacks(stage);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = mode,
                assemblyNames = new[]
                {
                    mode == TestMode.EditMode
                        ? "ArcaneDuel.EditModeTests"
                        : "ArcaneDuel.PlayModeTests"
                },
                testNames = mode == TestMode.EditMode
                    ? EditModeGateTests
                    : PlayModeGateTests
            };
            try
            {
                string runGuid = api.Execute(new ExecutionSettings(filter));
                File.WriteAllText(
                    Path.Combine(ResultDirectory, "codex-phase3-6.run-guid"),
                    runGuid ?? string.Empty);
            }
            catch (Exception exception)
            {
                File.AppendAllText(
                    SummaryPath,
                    $"RUN-ERROR {stage} {exception}{Environment.NewLine}");
                File.WriteAllText(StatePath, "failed-" + stage + "-runner");
                File.Delete(RequestPath);
                throw;
            }
        }

        private static int CancelAllActiveTestRuns()
        {
            Type holderType = typeof(TestRunnerApi).Assembly.GetType(
                "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder");
            PropertyInfo instanceProperty = holderType?.GetProperty(
                "instance",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            object holder = instanceProperty?.GetValue(null);
            MethodInfo getAllRunners = holderType?.GetMethod(
                "GetAllRunners",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (!(getAllRunners?.Invoke(holder, null) is IEnumerable runners))
                return 0;

            int cancelled = 0;
            foreach (object runner in runners)
            {
                MethodInfo cancel = runner?.GetType().GetMethod(
                    "CancelRun",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (cancel?.Invoke(runner, null) is bool didCancel && didCancel)
                    cancelled++;
            }
            return cancelled;
        }

        private static void RestoreMainMenuAndDeleteGeneratedTestScenes()
        {
            const string mainMenuPath = "Assets/Scenes/MainMenu.unity";
            if (File.Exists(Path.Combine(ProjectRoot, mainMenuPath)))
                EditorSceneManager.OpenScene(mainMenuPath);

            foreach (string guid in AssetDatabase.FindAssets(
                         "InitTestScene t:Scene",
                         new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path)
                    .StartsWith("InitTestScene", StringComparison.Ordinal))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
            AssetDatabase.Refresh();
        }

        private static void EnsureCallbacks(string stage)
        {
            if (callbacksRegistered)
                return;
            callbacksRegistered = true;
            ScriptableObject.CreateInstance<TestRunnerApi>()
                .RegisterCallbacks(new GateCallbacks(stage), 1000);
        }

        private static string SummaryPath =>
            Path.Combine(ResultDirectory, "codex-phase3-6-summary.log");

        private static string XmlPath(string stage) =>
            Path.Combine(
                ResultDirectory,
                $"codex-phase3-6-{stage}.xml");

        private static void CompleteStage(
            string stage,
            ITestResultAdaptor result)
        {
            string expected = stage + "-running";
            string current = File.Exists(StatePath)
                ? File.ReadAllText(StatePath).Trim()
                : string.Empty;
            if (!string.Equals(current, expected, StringComparison.Ordinal))
                return;

            File.WriteAllText(XmlPath(stage), result.ToXml().OuterXml);
            File.AppendAllText(
                SummaryPath,
                $"FINISH {stage} status={result.ResultState} " +
                $"pass={result.PassCount} fail={result.FailCount} " +
                $"skip={result.SkipCount} inconclusive={result.InconclusiveCount} " +
                $"duration={result.Duration:0.000}s{Environment.NewLine}");

            callbacksRegistered = false;
            if (result.FailCount > 0)
            {
                File.WriteAllText(StatePath, "failed-" + stage);
                File.Delete(RequestPath);
                return;
            }
            if (stage == EditStage)
            {
                File.WriteAllText(StatePath, "pending-play");
                EditorApplication.delayCall += ContinueRequestedRun;
            }
            else
            {
                File.WriteAllText(StatePath, "done");
                File.Delete(RequestPath);
            }
        }

        [Serializable]
        private sealed class GateCallbacks : ICallbacks
        {
            [SerializeField] private string stage;

            public GateCallbacks(string stage)
            {
                this.stage = stage;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                CompleteStage(stage, result);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus !=
                    UnityEditor.TestTools.TestRunner.Api.TestStatus.Failed)
                    return;
                File.AppendAllText(
                    SummaryPath,
                    $"FAIL {stage} {result.FullName}: {result.Message}" +
                    Environment.NewLine + result.StackTrace +
                    Environment.NewLine);
            }
        }
    }
}
