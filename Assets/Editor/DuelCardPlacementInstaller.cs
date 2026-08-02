#if UNITY_EDITOR
using System;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Editor
{
    public static class DuelCardPlacementInstaller
    {
        private const string ScenePath = "Assets/Scenes/DuelArena.unity";

        [MenuItem("Card Game/Duelo/Preparar posicoes editaveis das cartas")]
        public static void Install()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            DuelZone3D[] zones =
                UnityEngine.Object.FindObjectsByType<DuelZone3D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (zones.Length == 0)
                throw new InvalidOperationException(
                    "Nenhuma zona de duelo foi encontrada.");

            foreach (DuelZone3D zone in zones)
            {
                if (zone == null)
                    continue;
                zone.EnsurePresentationAnchors();
                EditorUtility.SetDirty(zone);
            }

            PrepareHandAnchors();

            DuelFieldRegistry registry =
                UnityEngine.Object.FindAnyObjectByType<DuelFieldRegistry>(
                    FindObjectsInactive.Include);
            registry?.RebuildIndex();
            if (registry != null)
                EditorUtility.SetDirty(registry);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"ARCANE_DUEL_CARD_PLACEMENT=READY; zones={zones.Length}; " +
                "edite POSICAO VISUAL DA CARTA, POSICAO DO ATK DEF e " +
                "as duas POSICOES DA MAO.");
        }

        private static void PrepareHandAnchors()
        {
            CardArenaBootstrap arena =
                UnityEngine.Object.FindAnyObjectByType<CardArenaBootstrap>(
                    FindObjectsInactive.Include);
            if (arena == null)
                throw new InvalidOperationException(
                    "CardArenaBootstrap nao foi encontrado na cena.");

            Transform canvas = arena.transform.Find("Arena Canvas");
            if (canvas == null)
                throw new InvalidOperationException(
                    "Arena Canvas nao foi encontrado.");
            Transform frame = canvas.Find("Area Segura Universal 16x9");
            if (frame == null)
                throw new InvalidOperationException(
                    "Area Segura Universal 16x9 nao foi encontrada.");

            RectTransform local = EnsureRect(
                frame,
                "POSICAO DA MAO DO JOGADOR");
            local.anchorMin = new Vector2(0.5f, 0f);
            local.anchorMax = new Vector2(0.5f, 0f);
            local.pivot = new Vector2(0.5f, 0f);
            if (local.sizeDelta.sqrMagnitude < 1f ||
                (Vector2.Distance(
                     local.sizeDelta,
                     new Vector2(100f, 100f)) < 0.01f &&
                 local.anchoredPosition.sqrMagnitude < 0.01f))
            {
                local.sizeDelta = new Vector2(1000f, 330f);
                local.anchoredPosition = new Vector2(0f, -189f);
            }
            DuelHandLayoutAnchor localLayout =
                local.GetComponent<DuelHandLayoutAnchor>() ??
                local.gameObject.AddComponent<DuelHandLayoutAnchor>();
            localLayout.ConfigureOwner(
                DuelHandLayoutAnchor.HandOwner.LocalPlayer);
            EditorUtility.SetDirty(localLayout);

            RectTransform opponent = EnsureRect(
                frame,
                "POSICAO DA MAO DO OPONENTE");
            opponent.anchorMin = new Vector2(0.365f, 0.865f);
            opponent.anchorMax = new Vector2(0.635f, 0.995f);
            opponent.offsetMin = Vector2.zero;
            opponent.offsetMax = Vector2.zero;
            DuelHandLayoutAnchor opponentLayout =
                opponent.GetComponent<DuelHandLayoutAnchor>() ??
                opponent.gameObject.AddComponent<DuelHandLayoutAnchor>();
            opponentLayout.ConfigureOwner(
                DuelHandLayoutAnchor.HandOwner.Opponent);
            EditorUtility.SetDirty(opponentLayout);
        }

        private static RectTransform EnsureRect(
            Transform parent,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing is RectTransform rect)
                return rect;
            var root = new GameObject(objectName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return root.GetComponent<RectTransform>();
        }
    }
}
#endif
