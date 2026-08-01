using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcaneDuel.Editor
{
    [InitializeOnLoad]
    internal static class ArcaneWindowsGraphicsConfigurator
    {
        static ArcaneWindowsGraphicsConfigurator()
        {
            EditorApplication.delayCall += Configure;
        }

        private static void Configure()
        {
            const BuildTarget target = BuildTarget.StandaloneWindows64;
            GraphicsDeviceType[] configured =
                PlayerSettings.GetGraphicsAPIs(target);
            bool alreadySafe =
                !PlayerSettings.GetUseDefaultGraphicsAPIs(target) &&
                configured.SequenceEqual(
                    new[] { GraphicsDeviceType.Direct3D11 });
            if (alreadySafe)
                return;

            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(
                target,
                new[] { GraphicsDeviceType.Direct3D11 });
            AssetDatabase.SaveAssets();
            Debug.Log(
                "ARCANE_WINDOWS_GRAPHICS_API=Direct3D11 " +
                "(D3D12 desativado para evitar estouro de VRAM no Editor).");
        }
    }
}
