using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArcaneDuel.Editor
{
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        private const string MainMenuPath =
            "Assets/Scenes/MainMenu.unity";

        static PlayModeStartScene()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("Arcane Duel/Use Classic Main Menu as Play Start Scene")]
        public static void Ensure()
        {
            if (Array.Exists(
                    Environment.GetCommandLineArgs(),
                    argument => string.Equals(
                        argument,
                        "-runTests",
                        StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            SceneAsset mainMenu =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
            if (mainMenu != null &&
                EditorSceneManager.playModeStartScene != mainMenu)
            {
                EditorSceneManager.playModeStartScene = mainMenu;
                Debug.Log(
                    "Arcane Duel: Play Mode começará pela Central de Duelos clássica.");
            }
        }
    }
}
