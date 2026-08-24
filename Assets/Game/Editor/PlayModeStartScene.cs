using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArcaneDuel.Editor
{
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        private const string LoginPath =
            "Assets/Scenes/Login.unity";

        static PlayModeStartScene()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("Master Duel 2 Plus Ultra/Usar Abertura como Cena Inicial")]
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
            SceneAsset login =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(LoginPath);
            if (login != null &&
                EditorSceneManager.playModeStartScene != login)
            {
                EditorSceneManager.playModeStartScene = login;
                Debug.Log(
                    "Master Duel 2 Plus Ultra: Play Mode começará pela abertura.");
            }
        }
    }
}
