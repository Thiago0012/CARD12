using ArcaneDuel.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneDuel.Editor
{
    public static class DuelArenaInstaller
    {
        [MenuItem("Arcane Duel/Install Playable Arena")]
        public static void Install()
        {
            InstallBootstrap();
            InstallDuel();
            InstallCardLab();
            AssetDatabase.SaveAssets();
            Debug.Log("ARCANE_DUEL_COMPLETE_UI_OK");
        }

        private static void InstallBootstrap()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            GameObject root = GameObject.Find("BootstrapContext") ?? new GameObject("BootstrapContext");
            if (root.GetComponent<BootstrapFlow>() == null) root.AddComponent<BootstrapFlow>();
            EditorSceneManager.SaveScene(scene);
        }

        private static void InstallDuel()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Duel.unity", OpenSceneMode.Single);
            GameObject root = GameObject.Find("DuelContext") ?? new GameObject("DuelContext");
            if (root.GetComponent<ArcaneField3DPresenter>() == null) root.AddComponent<ArcaneField3DPresenter>();
            if (root.GetComponent<DuelArenaController>() == null) root.AddComponent<DuelArenaController>();
            if (root.GetComponent<AudioSource>() == null) root.AddComponent<AudioSource>();
            if (root.GetComponent<ArcaneAudioDirector>() == null) root.AddComponent<ArcaneAudioDirector>();
            EditorSceneManager.SaveScene(scene);
        }

        private static void InstallCardLab()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/CardLab.unity", OpenSceneMode.Single);
            GameObject root = GameObject.Find("CardLabContext") ?? new GameObject("CardLabContext");
            if (root.GetComponent<CardLabController>() == null) root.AddComponent<CardLabController>();
            EditorSceneManager.SaveScene(scene);
        }
    }
}
