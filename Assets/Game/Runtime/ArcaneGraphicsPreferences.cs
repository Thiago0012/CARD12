using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArcaneDuel.Game
{
    public enum ArcaneGraphicsQuality
    {
        VeryLow,
        Low,
        Medium,
        High,
        VeryHigh
    }

    public static class ArcaneGraphicsPreferences
    {
        private const string QualityKey = "ArcaneGraphicsQuality";
        private const int EditorSafeGraphicsMemoryMb = 4096;

        public static event Action QualityChanged;

        public static ArcaneGraphicsQuality Quality { get; private set; } =
            ArcaneGraphicsQuality.VeryHigh;
        public static bool IsMobileRuntime =>
            Application.isMobilePlatform && !Application.isEditor;

        public static int CardTextureWidth => Quality switch
        {
            ArcaneGraphicsQuality.VeryLow => 256,
            ArcaneGraphicsQuality.Low => 384,
            ArcaneGraphicsQuality.Medium => 512,
            ArcaneGraphicsQuality.High => 768,
            _ => 0
        };

        public static bool ReduceArenaLighting =>
            Quality <= ArcaneGraphicsQuality.Low;

        public static bool UseStaticArenaBatching =>
            IsMobileRuntime || Quality <= ArcaneGraphicsQuality.Medium;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
            ArcaneGraphicsQuality requested = PlayerPrefs.HasKey(QualityKey)
                ? Sanitize(PlayerPrefs.GetInt(QualityKey))
                : ResolveAutomaticQuality(
                    IsMobileRuntime,
                    SystemInfo.systemMemorySize,
                    SystemInfo.graphicsMemorySize,
                    SystemInfo.graphicsShaderLevel,
                    SystemInfo.processorCount);
            Quality = ApplyHardwareSafetyLimit(
                requested,
                Application.isEditor,
                SystemInfo.graphicsMemorySize);
            Apply(Quality, false);
            Application.lowMemory -= ReleaseUnusedMemory;
            Application.lowMemory += ReleaseUnusedMemory;
        }

        public static void SetQuality(ArcaneGraphicsQuality quality)
        {
            Apply(quality, true);
        }

        public static void ResetToAutomatic()
        {
            PlayerPrefs.DeleteKey(QualityKey);
            ArcaneGraphicsQuality automatic = ResolveAutomaticQuality(
                IsMobileRuntime,
                SystemInfo.systemMemorySize,
                SystemInfo.graphicsMemorySize,
                SystemInfo.graphicsShaderLevel,
                SystemInfo.processorCount);
            Apply(automatic, false);
        }

        public static ArcaneGraphicsQuality ResolveAutomaticQuality(
            bool mobile,
            int systemMemoryMb,
            int graphicsMemoryMb,
            int shaderLevel,
            int processorCount)
        {
            if (!mobile)
                return ArcaneGraphicsQuality.High;

            bool veryLimited =
                systemMemoryMb > 0 && systemMemoryMb <= 3072 ||
                graphicsMemoryMb > 0 && graphicsMemoryMb <= 768 ||
                shaderLevel < 40 ||
                processorCount > 0 && processorCount <= 4;
            if (veryLimited)
                return ArcaneGraphicsQuality.VeryLow;

            bool limited =
                systemMemoryMb > 0 && systemMemoryMb <= 4096 ||
                graphicsMemoryMb > 0 && graphicsMemoryMb <= 1536 ||
                shaderLevel < 45 ||
                processorCount > 0 && processorCount <= 6;
            return limited
                ? ArcaneGraphicsQuality.Low
                : ArcaneGraphicsQuality.Medium;
        }

        public static string DisplayName(ArcaneGraphicsQuality quality)
        {
            return quality switch
            {
                ArcaneGraphicsQuality.VeryLow => "MUITO BAIXO",
                ArcaneGraphicsQuality.Low => "BAIXO",
                ArcaneGraphicsQuality.Medium => "MÉDIO",
                ArcaneGraphicsQuality.High => "ALTO",
                _ => "MUITO ALTO"
            };
        }

        public static ArcaneGraphicsQuality ApplyHardwareSafetyLimit(
            ArcaneGraphicsQuality requested,
            bool editor,
            int graphicsMemoryMb)
        {
            if (editor &&
                graphicsMemoryMb > 0 &&
                graphicsMemoryMb <= EditorSafeGraphicsMemoryMb &&
                requested > ArcaneGraphicsQuality.Medium)
            {
                return ArcaneGraphicsQuality.Medium;
            }

            return requested;
        }

        private static void Apply(
            ArcaneGraphicsQuality quality,
            bool persist)
        {
            quality = ApplyHardwareSafetyLimit(
                quality,
                Application.isEditor,
                SystemInfo.graphicsMemorySize);
            Quality = quality;
            int qualityIndex = quality == ArcaneGraphicsQuality.VeryHigh
                ? Mathf.Max(0, QualitySettings.names.Length - 1)
                : Mathf.Clamp((int)quality, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(qualityIndex, true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 30,
                ArcaneGraphicsQuality.Low => 45,
                _ => 60
            };
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            ApplyUnityQuality(quality);
            ApplyUniversalPipeline(quality);

            if (persist)
            {
                PlayerPrefs.SetInt(QualityKey, (int)quality);
                PlayerPrefs.Save();
            }
            QualityChanged?.Invoke();
            Debug.Log(
                $"ARCANE_GRAPHICS_QUALITY level={DisplayName(quality)}; " +
                $"renderScale={RenderScale(quality):0.00}; " +
                $"cardWidth={(CardTextureWidth == 0 ? "original" : CardTextureWidth.ToString())}; " +
                $"targetFps={Application.targetFrameRate}");
        }

        private static void ApplyUnityQuality(ArcaneGraphicsQuality quality)
        {
            QualitySettings.pixelLightCount = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 0,
                ArcaneGraphicsQuality.Low => 1,
                ArcaneGraphicsQuality.Medium => 2,
                _ => 4
            };
            QualitySettings.shadows = quality switch
            {
                ArcaneGraphicsQuality.Medium =>
                    UnityEngine.ShadowQuality.HardOnly,
                ArcaneGraphicsQuality.High =>
                    UnityEngine.ShadowQuality.All,
                ArcaneGraphicsQuality.VeryHigh =>
                    UnityEngine.ShadowQuality.All,
                _ => UnityEngine.ShadowQuality.Disable
            };
            QualitySettings.shadowDistance = ShadowDistance(quality);
            QualitySettings.anisotropicFiltering = quality switch
            {
                ArcaneGraphicsQuality.VeryHigh => AnisotropicFiltering.ForceEnable,
                ArcaneGraphicsQuality.High => AnisotropicFiltering.Enable,
                _ => AnisotropicFiltering.Disable
            };
            QualitySettings.realtimeReflectionProbes =
                quality >= ArcaneGraphicsQuality.High;
            QualitySettings.lodBias = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 0.40f,
                ArcaneGraphicsQuality.Low => 0.55f,
                ArcaneGraphicsQuality.Medium => 0.75f,
                ArcaneGraphicsQuality.High => 1f,
                _ => 1.5f
            };
            QualitySettings.globalTextureMipmapLimit = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 2,
                ArcaneGraphicsQuality.Low => 1,
                _ => 0
            };
            Shader.globalMaximumLOD = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 150,
                ArcaneGraphicsQuality.Low => 200,
                ArcaneGraphicsQuality.Medium => 300,
                ArcaneGraphicsQuality.High => 450,
                _ => 1000
            };
        }

        private static void ApplyUniversalPipeline(
            ArcaneGraphicsQuality quality)
        {
            if (GraphicsSettings.currentRenderPipeline is not
                UniversalRenderPipelineAsset pipeline)
            {
                return;
            }

            pipeline.renderScale = RenderScale(quality);
            pipeline.supportsHDR = quality >= ArcaneGraphicsQuality.High;
            pipeline.msaaSampleCount = quality switch
            {
                ArcaneGraphicsQuality.High => 2,
                ArcaneGraphicsQuality.VeryHigh => 4,
                _ => 1
            };
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = ShadowDistance(quality);
            pipeline.maxAdditionalLightsCount = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 0,
                ArcaneGraphicsQuality.Low => 1,
                ArcaneGraphicsQuality.Medium => 2,
                _ => 4
            };
            pipeline.useSRPBatcher = true;
            pipeline.colorGradingLutSize =
                quality >= ArcaneGraphicsQuality.High ? 32 : 16;
            pipeline.useAdaptivePerformance = true;
        }

        private static float RenderScale(ArcaneGraphicsQuality quality)
        {
            return quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 0.55f,
                ArcaneGraphicsQuality.Low => 0.68f,
                ArcaneGraphicsQuality.Medium => 0.82f,
                _ => 1f
            };
        }

        private static float ShadowDistance(ArcaneGraphicsQuality quality)
        {
            return quality switch
            {
                ArcaneGraphicsQuality.Medium => 16f,
                ArcaneGraphicsQuality.High => 34f,
                ArcaneGraphicsQuality.VeryHigh => 50f,
                _ => 0f
            };
        }

        private static ArcaneGraphicsQuality Sanitize(int value)
        {
            return (ArcaneGraphicsQuality)Mathf.Clamp(
                value,
                (int)ArcaneGraphicsQuality.VeryLow,
                (int)ArcaneGraphicsQuality.VeryHigh);
        }

        private static void ReleaseUnusedMemory()
        {
            Resources.UnloadUnusedAssets();
        }
    }
}
