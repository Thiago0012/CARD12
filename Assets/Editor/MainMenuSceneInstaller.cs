#if UNITY_EDITOR
using System;
using System.Linq;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Editor
{
    public static class MainMenuSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Card Game/Frontend/Preparar Main Menu editavel na Scene")]
        public static void Install()
        {
            MainMenuUiAssetSynchronizer.Sync();
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            GameFrontendBootstrap bootstrap =
                UnityEngine.Object.FindAnyObjectByType<GameFrontendBootstrap>(
                    FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                bootstrap = new GameObject("Interface Principal")
                    .AddComponent<GameFrontendBootstrap>();
            }

            foreach (MainMenuSceneView previous in
                     UnityEngine.Object.FindObjectsByType<MainMenuSceneView>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(previous);
            }

            bootstrap.BuildEditorPreview();
            Canvas canvas = bootstrap.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
                throw new InvalidOperationException(
                    "O Canvas do Main Menu nao foi gerado.");

            RectTransform authoredRoot =
                FindDescendant(canvas.transform, "Tela Atual")
                    as RectTransform;
            if (authoredRoot == null)
                throw new InvalidOperationException(
                    "A raiz editavel do Main Menu nao foi encontrada.");
            authoredRoot.name = "MAIN MENU EDITAVEL";

            RawImage hudOverlay = FindDescendant(
                    authoredRoot,
                    "Moldura HUD da Tela Inicial")
                ?.GetComponent<RawImage>();
            Material hudOverlayMaterial =
                MainMenuUiAssetSynchronizer.EnsureHudOverlayMaterial();
            if (hudOverlay == null || hudOverlayMaterial == null)
            {
                throw new InvalidOperationException(
                    "A moldura recortada do Main Menu nao foi configurada.");
            }
            hudOverlay.material = hudOverlayMaterial;
            hudOverlay.raycastTarget = false;
            EditorUtility.SetDirty(hudOverlay);

            var dynamicObject = new GameObject(
                "CONTEUDO DINAMICO (NAO EDITAR)",
                typeof(RectTransform));
            RectTransform dynamicRoot =
                dynamicObject.GetComponent<RectTransform>();
            dynamicRoot.SetParent(authoredRoot.parent, false);
            dynamicRoot.anchorMin = Vector2.zero;
            dynamicRoot.anchorMax = Vector2.one;
            dynamicRoot.offsetMin = Vector2.zero;
            dynamicRoot.offsetMax = Vector2.zero;
            dynamicObject.SetActive(false);

            Button duel = FindButton(authoredRoot, "DUELAR");
            Button decks = FindButton(authoredRoot, "DECKS");
            Button shop = FindButton(authoredRoot, "LOJA");
            Button multiplayer = FindButton(authoredRoot, "MULTIPLAYER");
            Button settings = FindButton(authoredRoot, "CONFIG");
            Button profile = FindButton(authoredRoot, "PERFIL", false);
            if (duel == null || decks == null || shop == null ||
                multiplayer == null || settings == null)
            {
                throw new InvalidOperationException(
                    "Um ou mais botoes obrigatorios nao foram encontrados.");
            }

            MainMenuSceneView view =
                canvas.gameObject.AddComponent<MainMenuSceneView>();
            view.Configure(
                canvas,
                authoredRoot,
                dynamicRoot,
                duel,
                decks,
                shop,
                multiplayer,
                settings,
                profile);
            authoredRoot.gameObject.SetActive(true);
            ShopSceneInstaller.EnsureForScene(scene, view);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = authoredRoot.gameObject;
            Debug.Log(
                "ARCANE_MAIN_MENU_SCENE_EDITABLE=READY; " +
                "edite MAIN MENU EDITAVEL diretamente na Hierarchy.");
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
                return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = FindDescendant(
                    root.GetChild(index),
                    objectName);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static Button FindButton(
            Transform root,
            string token,
            bool required = true)
        {
            Button result = root
                .GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button =>
                    button != null &&
                    button.name.IndexOf(
                        token,
                        StringComparison.OrdinalIgnoreCase) >= 0);
            if (result == null && required)
            {
                Debug.LogError($"Botao {token} nao encontrado.");
            }
            return result;
        }
    }
}
#endif
