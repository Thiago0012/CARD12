using ArcaneArena.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Editor
{
    public static class StaticDuelFieldInstaller
    {
        private const string DuelScenePath = "Assets/Scenes/DuelArena.unity";
        private const string FieldTexturePath =
            "Assets/Templates/Field/field1.png";

        [MenuItem("Card Game/Arena/Aplicar campo PNG field1")]
        public static void ApplyField1ToDuelArena()
        {
            Texture2D fieldTexture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(FieldTexturePath);
            if (fieldTexture == null)
            {
                throw new System.IO.FileNotFoundException(
                    $"Campo nao encontrado em {FieldTexturePath}.");
            }

            Scene scene = EditorSceneManager.OpenScene(
                DuelScenePath,
                OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MasterDuelArena3D[] arenas =
                    root.GetComponentsInChildren<MasterDuelArena3D>(true);
                foreach (MasterDuelArena3D arena in arenas)
                {
                    arena.ConfigureStaticField(fieldTexture, fieldTexture);
                    EditorUtility.SetDirty(arena);
                    if (arena.gameObject.activeInHierarchy)
                        arena.Rebuild();
                }

                DuelTestPerspectiveController[] perspectives =
                    root.GetComponentsInChildren<DuelTestPerspectiveController>(true);
                foreach (DuelTestPerspectiveController perspective in perspectives)
                {
                    perspective.ConfigureStaticFieldCamera(true);
                    EditorUtility.SetDirty(perspective);
                }
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize =
                    MasterDuelArena3D.StaticFieldDepth * 0.5f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.transform.SetPositionAndRotation(
                    new Vector3(0f, 12f, 0f),
                    Quaternion.Euler(90f, 0f, 0f));
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(camera.transform);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "ARCANE_STATIC_FIELD1_OK: campo PNG e zonas atualizados em DuelArena.");
        }
    }
}
