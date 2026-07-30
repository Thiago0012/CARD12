using System.IO;
using ArcaneDuel.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ArcaneDuel.Editor
{
    public static class ProjectBootstrap
    {
        private const string RenderingDirectory = "Assets/Game/Rendering";
        private const string ScenesDirectory = "Assets/Game/Scenes";
        private const string PipelineAssetPath = RenderingDirectory + "/ArcaneDuelURP.asset";

        [MenuItem("Arcane Duel/Configure Project")]
        public static void Configure()
        {
            EnsureDirectories();
            ConfigurePlayer();
            ConfigureRendering();
            CreateScene(ProjectIdentity.DuelScene, SceneRole.Duel);
            CreateScene(ProjectIdentity.CardLabScene, SceneRole.CardLab);
            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ARCANE_DUEL_BOOTSTRAP_OK");
        }

        private static void EnsureDirectories()
        {
            string[] directories =
            {
                RenderingDirectory,
                ScenesDirectory,
                "Assets/DuelEngine/Runtime/Abstractions",
                "Assets/DuelEngine/Runtime/Interop",
                "Assets/DuelEngine/Runtime/Core",
                "Assets/DuelEngine/Runtime/Protocol",
                "Assets/DuelEngine/Runtime/Data",
                "Assets/DuelEngine/Runtime/Scripts",
                "Assets/DuelEngine/Runtime/State",
                "Assets/DuelEngine/Runtime/Presentation",
                "Assets/DuelEngine/Runtime/Diagnostics",
                "Assets/Plugins/Windows/x86_64",
                "Assets/StreamingAssets/Ygo/CustomScripts",
                "Assets/StreamingAssets/Ygo/Scripts/official",
                "Assets/StreamingAssets/Ygo/Art",
                "Assets/StreamingAssets/Ygo/Visual",
                "Assets/StreamingAssets/Build",
                "Assets/Tests/EditMode",
                "Assets/Tests/PlayMode"
            };

            foreach (string directory in directories)
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Arcane Duel Team";
            PlayerSettings.productName = ProjectIdentity.ProductName;
            PlayerSettings.bundleVersion = ProjectIdentity.ProjectVersion;
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Standalone,
                "com.arcaneduel.client");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;
        }

        private static void ConfigureRendering()
        {
            var pipelineAsset =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    PipelineAssetPath);

            if (pipelineAsset == null)
            {
                pipelineAsset =
                    ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
                pipelineAsset.LoadBuiltinRendererData(RendererType.UniversalRenderer);
            }

            pipelineAsset.supportsHDR = true;
            pipelineAsset.msaaSampleCount = 4;
            pipelineAsset.renderScale = 1.0f;

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
            EditorUtility.SetDirty(pipelineAsset);
        }

        private static void CreateScene(string sceneName, SceneRole role)
        {
            string path = $"{ScenesDirectory}/{sceneName}.unity";
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var context = new GameObject($"{sceneName}Context");
            var marker = context.AddComponent<SceneMarker>();
            marker.Configure(role);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 7.5f, -10f),
                Quaternion.Euler(28f, 0f, 0f));

            var lightObject = new GameObject("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                EnabledSceneAt("Assets/Scenes/MainMenu.unity"),
                EnabledSceneAt("Assets/Scenes/DeckEditor.unity"),
                EnabledSceneAt("Assets/Scenes/DuelArena.unity"),
                EnabledScene(ProjectIdentity.DuelScene),
                EnabledScene(ProjectIdentity.CardLabScene)
            };
        }

        private static EditorBuildSettingsScene EnabledSceneAt(string path)
        {
            return new EditorBuildSettingsScene(path, true);
        }

        private static EditorBuildSettingsScene EnabledScene(string sceneName)
        {
            return new EditorBuildSettingsScene(
                $"{ScenesDirectory}/{sceneName}.unity",
                true);
        }
    }
}
