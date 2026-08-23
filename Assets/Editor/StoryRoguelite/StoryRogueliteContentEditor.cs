using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneArena.StoryRoguelite;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.EditorTools
{
    public static class StoryRogueliteContentEditor
    {
        private const string Root = "Assets/Resources/StoryRoguelite";
        private const string Generated = Root + "/Generated";
        private const string NpcFolder = Generated + "/NPCs";

        [MenuItem("Tools/Arcane Duel/Story Roguelite/Sincronizar Conteúdo")]
        public static void Synchronize()
        {
            EnsureFolder("Assets/Resources", "StoryRoguelite");
            EnsureFolder(Root, "Generated");
            EnsureFolder(Generated, "NPCs");
            ConfigureImagesAsSprites(Root + "/NPCs");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            StoryContentCatalog.ClearCache();
            StoryContentCatalogFile content = StoryContentCatalog.Load();

            var npcAssets = new List<StoryNpcDefinition>();
            foreach (StoryNpcRecord record in content.npcs)
            {
                string assetPath = $"{NpcFolder}/{record.npcId}.asset";
                StoryNpcDefinition definition = AssetDatabase.LoadAssetAtPath<
                    StoryNpcDefinition>(assetPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<
                        StoryNpcDefinition>();
                    AssetDatabase.CreateAsset(definition, assetPath);
                }
                Sprite portrait = Resources.Load<Sprite>(
                    record.portraitResourcePath);
                definition.Initialize(record, portrait);
                EditorUtility.SetDirty(definition);
                npcAssets.Add(definition);
            }

            string catalogPath = Generated + "/StoryNpcCatalog.asset";
            StoryNpcCatalog npcCatalog = AssetDatabase.LoadAssetAtPath<
                StoryNpcCatalog>(catalogPath);
            if (npcCatalog == null)
            {
                npcCatalog = ScriptableObject.CreateInstance<StoryNpcCatalog>();
                AssetDatabase.CreateAsset(npcCatalog, catalogPath);
            }
            npcCatalog.Initialize(npcAssets);
            EditorUtility.SetDirty(npcCatalog);

            string lpPath = Generated + "/StoryEncounterLpProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<StoryEncounterLpProfile>(lpPath) ==
                null)
            {
                StoryEncounterLpProfile lp = ScriptableObject.CreateInstance<
                    StoryEncounterLpProfile>();
                AssetDatabase.CreateAsset(lp, lpPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
        }

        [MenuItem("Tools/Arcane Duel/Story Roguelite/Validar Conteúdo")]
        public static void Validate()
        {
            StoryContentCatalog.ClearCache();
            StoryContentCatalogFile content = StoryContentCatalog.Load();
            var errors = new List<string>();
            AddDuplicateErrors(errors, content.npcs.Select(npc => npc.npcId),
                "NPC");
            if (content.npcs.Count < 30)
                errors.Add($"São esperados 30 NPCs; encontrados {content.npcs.Count}.");

            foreach (StoryNpcRecord npc in content.npcs)
            {
                if (string.IsNullOrWhiteSpace(npc.displayName))
                    errors.Add($"{npc.npcId} está sem nome de exibição.");
                if (Resources.Load<Texture2D>(npc.portraitResourcePath) == null &&
                    Resources.Load<Sprite>(npc.portraitResourcePath) == null)
                    errors.Add($"{npc.npcId} está sem retrato em {npc.portraitResourcePath}.");
            }
            List<StoryMapRecord> proceduralMaps =
                StoryProceduralMapGenerator.GenerateRun(20260822L);
            foreach (StoryMapRecord map in proceduralMaps)
            {
                if (!string.IsNullOrWhiteSpace(map.backgroundResourcePath))
                    errors.Add($"{map.mapId} não deve usar imagem de fundo.");
                ValidateMap(map, errors);
            }

            if (errors.Count > 0)
                throw new InvalidDataException(
                    "Conteúdo Story Roguelite inválido:\n- " +
                    string.Join("\n- ", errors));
            Debug.Log($"[Story Roguelite] Conteúdo válido: " +
                      $"{content.npcs.Count} NPCs e " +
                      $"{proceduralMaps.Count} atos procedurais sem imagens.");
        }

        private static void ValidateMap(
            StoryMapRecord map,
            List<string> errors)
        {
            if (map == null)
            {
                errors.Add("O catálogo contém um mapa nulo.");
                return;
            }
            AddDuplicateErrors(errors,
                map.nodes.Select(node => node.nodeId),
                $"nó de {map.mapId}");
            if (map.Node(map.startNodeId) == null)
                errors.Add($"{map.mapId} não possui Start válido.");
            if (map.Node(map.bossNodeId) == null)
                errors.Add($"{map.mapId} não possui Boss válido.");
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(map.startNodeId);
            while (queue.Count > 0)
            {
                string node = queue.Dequeue();
                if (!visited.Add(node)) continue;
                foreach (StoryMapEdgeRecord edge in map.edges.Where(edge =>
                             string.Equals(edge.fromNodeId, node,
                                 StringComparison.Ordinal)))
                    queue.Enqueue(edge.toNodeId);
            }
            if (!visited.Contains(map.bossNodeId))
                errors.Add($"O Boss de {map.mapId} não é alcançável a partir do Start.");
            foreach (StoryMapEdgeRecord edge in map.edges)
            {
                if (map.Node(edge.fromNodeId) == null ||
                    map.Node(edge.toNodeId) == null)
                    errors.Add($"{map.mapId}/{edge.edgeId} referencia um nó inexistente.");
            }
        }

        private static void ConfigureImagesAsSprites(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D",
                         new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                bool changed = importer.textureType !=
                               TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               importer.mipmapEnabled;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                if (changed) importer.SaveAndReimport();
            }
        }

        private static void AddDuplicateErrors(
            List<string> errors,
            IEnumerable<string> values,
            string label)
        {
            foreach (IGrouping<string, string> group in values
                         .GroupBy(value => value ?? string.Empty,
                             StringComparer.Ordinal)
                         .Where(group => string.IsNullOrWhiteSpace(group.Key) ||
                                         group.Count() > 1))
                errors.Add($"ID de {label} vazio ou duplicado: '{group.Key}'.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }

    [CustomEditor(typeof(StoryNpcCatalog))]
    public sealed class StoryNpcCatalogInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Sincronizar NPCs"))
                StoryRogueliteContentEditor.Synchronize();
            if (GUILayout.Button("Validar Conteúdo Roguelite"))
                StoryRogueliteContentEditor.Validate();
        }
    }
}
