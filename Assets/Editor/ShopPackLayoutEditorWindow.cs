#if UNITY_EDITOR
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Editor
{
    public sealed class ShopPackLayoutEditorWindow : EditorWindow
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        private GameFrontendBootstrap _frontend;
        private Vector2 _scroll;

        [MenuItem("Card Game/Loja/Editar tamanho dos pacotes e cartas")]
        public static void OpenWindow()
        {
            ShopPackLayoutEditorWindow window = GetWindow<
                ShopPackLayoutEditorWindow>(
                "Pacotes da Loja");
            window.minSize = new Vector2(470f, 560f);
            window.Show();
            window.LocateFrontend();
        }

        private void OnEnable()
        {
            LocateFrontend(false);
        }

        private void OnFocus()
        {
            if (_frontend == null)
                LocateFrontend(false);
        }

        private void LocateFrontend(bool openMainMenu = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (openMainMenu && activeScene.path != MainMenuScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(
                    MainMenuScenePath,
                    OpenSceneMode.Single);
            }

            _frontend = Object.FindAnyObjectByType<GameFrontendBootstrap>(
                FindObjectsInactive.Include);
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Tamanho das cartas dos pacotes",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Edite aqui fora do Play. A vitrine usa somente as três " +
                "cartas em leque, sem a imagem do pacote aberto. Os números " +
                "são proporcionais ao quadro de cada produto: 0 é o início e 1 é o final. " +
                "Depois clique em Salvar ajustes na MainMenu.",
                MessageType.Info);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox(
                    "Pare o Play para alterar e salvar o layout.",
                    MessageType.Warning);
                return;
            }

            if (_frontend == null)
            {
                EditorGUILayout.HelpBox(
                    "A MainMenu ainda não está aberta ou o componente da " +
                    "loja não foi localizado.",
                    MessageType.Warning);
                if (GUILayout.Button("Abrir MainMenu e localizar a loja"))
                    LocateFrontend();
                return;
            }

            SerializedObject serialized = new SerializedObject(_frontend);
            SerializedProperty titleX = serialized.FindProperty(
                "shopPackTitleAnchorMinX");
            SerializedProperty cardSize = serialized.FindProperty(
                "shopPackCardSize");
            SerializedProperty leftCard = serialized.FindProperty(
                "shopPackLeftCardAnchorMin");
            SerializedProperty centerCard = serialized.FindProperty(
                "shopPackCenterCardAnchorMin");
            SerializedProperty rightCard = serialized.FindProperty(
                "shopPackRightCardAnchorMin");
            SerializedProperty rotations = serialized.FindProperty(
                "shopPackCardRotations");

            if (titleX == null || cardSize == null ||
                leftCard == null || centerCard == null ||
                rightCard == null || rotations == null)
            {
                EditorGUILayout.HelpBox(
                    "Os campos editáveis da loja não foram encontrados. " +
                    "Aguarde a Unity terminar de compilar.",
                    MessageType.Error);
                return;
            }

            serialized.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6f);
            EditorGUI.BeginChangeCheck();
            float newTitleX = EditorGUILayout.Slider(
                "Início horizontal do título",
                titleX.floatValue,
                0f,
                0.8f);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(
                "Três cartas do leque",
                EditorStyles.boldLabel);
            Vector2 newCardSize = EditorGUILayout.Vector2Field(
                "Tamanho das cartas",
                cardSize.vector2Value);
            Vector2 newLeftCard = EditorGUILayout.Vector2Field(
                "Posição da carta esquerda",
                leftCard.vector2Value);
            Vector2 newCenterCard = EditorGUILayout.Vector2Field(
                "Posição da carta central",
                centerCard.vector2Value);
            Vector2 newRightCard = EditorGUILayout.Vector2Field(
                "Posição da carta direita",
                rightCard.vector2Value);
            Vector3 newRotations = EditorGUILayout.Vector3Field(
                "Rotações (esq./centro/dir.)",
                rotations.vector3Value);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_frontend, "Ajustar pacotes da loja");
                titleX.floatValue = newTitleX;
                cardSize.vector2Value = new Vector2(
                    Mathf.Max(0.01f, newCardSize.x),
                    Mathf.Max(0.01f, newCardSize.y));
                leftCard.vector2Value = newLeftCard;
                centerCard.vector2Value = newCenterCard;
                rightCard.vector2Value = newRightCard;
                rotations.vector3Value = newRotations;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_frontend);
                EditorSceneManager.MarkSceneDirty(_frontend.gameObject.scene);
            }

            EditorGUILayout.Space(14f);
            EditorGUILayout.HelpBox(
                "Sugestão: altere largura e altura em passos de 0,01. " +
                "Aumentar Tamanho das cartas deixa as três cartas maiores.",
                MessageType.None);

            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.9f, 0.65f);
            if (GUILayout.Button("Salvar ajustes na MainMenu", GUILayout.Height(36f)))
            {
                EditorSceneManager.SaveScene(
                    _frontend.gameObject.scene,
                    MainMenuScenePath);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("Layout salvo na MainMenu"));
            }
            GUI.backgroundColor = previousColor;

            if (GUILayout.Button("Selecionar Interface Principal no Inspector"))
            {
                Selection.activeGameObject = _frontend.gameObject;
                EditorGUIUtility.PingObject(_frontend.gameObject);
            }
            if (GUILayout.Button("Reabrir aba Game"))
                ShopLayoutInspectorUtility.ReopenGameView();

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
