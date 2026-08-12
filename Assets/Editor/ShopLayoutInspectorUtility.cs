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

        [MenuItem("Card Game/Loja/Selecionar Shop View na Hierarchy")]
        public static void OpenMainMenuAndSelectShopView()
        {
            if (!OpenMainMenuForEditing())
                return;

            ShopSceneView view =
                UnityEngine.Object.FindAnyObjectByType<ShopSceneView>(
                    FindObjectsInactive.Include);
            if (view == null || !view.IsConfigured)
            {
                Debug.LogError(
                    "A Shop View permanente nao foi encontrada. Use " +
                    "Card Game/Loja/Instalar Shop View editavel na Scene.");
                return;
            }

            Selection.activeGameObject = view.Root.gameObject;
            EditorGUIUtility.PingObject(view.Root.gameObject);
            Debug.Log(
                "SHOP_SCENE_VIEW=READY; edite LOJA EDITAVEL e seus filhos " +
                "diretamente na Hierarchy e no Inspector.");
        }

        [MenuItem("Card Game/Loja/Selecionar ajustes das cartas do pacote")]
        public static void OpenMainMenuAndSelectFrontend()
        {
            if (!OpenMainMenuForEditing())
                return;

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
                "'Loja - cartas dos pacotes editáveis no Inspector'.");
        }

        private static bool OpenMainMenuForEditing()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    "Pare o Play antes de editar o layout permanente da loja.");
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, MainMenuScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return false;
                EditorSceneManager.OpenScene(
                    MainMenuScenePath,
                    OpenSceneMode.Single);
            }
            return true;
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
