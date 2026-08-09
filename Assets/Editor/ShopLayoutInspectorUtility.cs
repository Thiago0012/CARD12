#if UNITY_EDITOR
using ArcaneArena.Frontend;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Editor
{
    public static class ShopLayoutInspectorUtility
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Card Game/Loja/Selecionar ajustes visuais do pacote")]
        public static void OpenMainMenuAndSelectFrontend()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    "Pare o Play antes de editar o layout permanente da loja.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, MainMenuScenePath,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(
                    MainMenuScenePath,
                    OpenSceneMode.Single);
            }

            GameFrontendBootstrap frontend =
                UnityEngine.Object.FindAnyObjectByType<GameFrontendBootstrap>(
                    FindObjectsInactive.Include);
            if (frontend == null)
            {
                Debug.LogError(
                    "O objeto Interface Principal não foi encontrado na MainMenu.");
                return;
            }

            Selection.activeGameObject = frontend.gameObject;
            EditorGUIUtility.PingObject(frontend.gameObject);
            Debug.Log(
                "SHOP_LAYOUT_INSPECTOR=READY; selecione a seção " +
                "'Loja - pacote editável no Inspector'.");
        }

        [MenuItem("Card Game/Ferramentas/Reabrir aba Game")]
        public static void ReopenGameView()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType(
                "UnityEditor.GameView");
            if (gameViewType == null)
            {
                Debug.LogError("A janela Game da Unity não foi encontrada.");
                return;
            }

            EditorWindow gameView = EditorWindow.GetWindow(
                gameViewType,
                false,
                "Game",
                true);
            gameView.Show();
            gameView.Focus();
            Debug.Log("SHOP_LAYOUT_GAME_VIEW=RESTORED");
        }
    }
}
#endif
