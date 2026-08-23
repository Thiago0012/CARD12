using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    [Serializable]
    public sealed class StoryArtifactDefinition
    {
        public string artifactId;
        public string displayName;
        public string shortEffect;
        public string description;
    }

    public static class StoryArtifactCatalog
    {
        private static readonly StoryArtifactDefinition[] Definitions =
        {
            new()
            {
                artifactId = "merchant-pouch",
                displayName = "Bolsa do Mercador",
                shortEffect = "10% de desconto nos mercadores.",
                description = "Reduz em 10% o custo, em Fragmentos Arcanos, " +
                    "das cartas compradas nos mercadores da run."
            },
            new()
            {
                artifactId = "fortune-echo",
                displayName = "Eco da Fortuna",
                shortEffect = "1 atualização gratuita por ato.",
                description = "A primeira atualização de ofertas de um " +
                    "mercador em cada ato não consome Fragmentos Arcanos."
            },
            new()
            {
                artifactId = "reinforced-seal",
                displayName = "Selo Reforçado",
                shortEffect = "+1 vida máxima e restaura 1 vida.",
                description = "Aumenta o limite de Selos de Duelo em 1 e " +
                    "restaura imediatamente um selo. Selos são as vidas da run."
            },
            new()
            {
                artifactId = "arcane-archive",
                displayName = "Arquivo Arcano",
                shortEffect = "+1 opção após vencer duelos.",
                description = "Apresenta uma opção de carta adicional nas " +
                    "recompensas obtidas depois de um duelo vencido."
            },
            new()
            {
                artifactId = "marked-map",
                displayName = "Mapa Marcado",
                shortEffect = "Revela previamente os pontos misteriosos.",
                description = "Mostra o verdadeiro tipo dos pontos de " +
                    "interrogação antes de o duelista escolher a rota."
            },
            new()
            {
                artifactId = "duelist-compass",
                displayName = "Bússola do Duelista",
                shortEffect = "+2 Fragmentos em vitórias difíceis.",
                description = "Concede 2 Fragmentos Arcanos adicionais ao " +
                    "vencer duelos de Elite, Arenas Finais ou Chefes."
            }
        };

        public static IReadOnlyList<StoryArtifactDefinition> All => Definitions;

        public static StoryArtifactDefinition Resolve(string artifactId) =>
            Definitions.FirstOrDefault(definition => string.Equals(
                definition.artifactId, artifactId, StringComparison.Ordinal)) ??
            new StoryArtifactDefinition
            {
                artifactId = artifactId ?? string.Empty,
                displayName = string.IsNullOrWhiteSpace(artifactId)
                    ? "Relíquia desconhecida"
                    : artifactId.Replace('-', ' '),
                shortEffect = "Efeito legado desta jornada.",
                description = "Esta relíquia pertence a uma versão anterior " +
                    "da run e foi preservada no save."
            };
    }

    /// <summary>
    /// Gera somente dados leves (nós, posições e arestas). Nenhuma textura de
    /// mapa participa da geração ou da persistência da jornada.
    /// </summary>
    public static class StoryProceduralMapGenerator
    {
        public const int GeneratorVersion = 2;
        public const int ActCount = 3;
        public const int MinimumDuelsBeforeBoss = 2;

        private static readonly string[] ActNames =
        {
            "Caminhos de Obsidiana",
            "Circuito do Eclipse",
            "Trono do Arcano"
        };

        private static readonly RogueliteNodeType[] UtilityPool =
        {
            RogueliteNodeType.CardPack,
            RogueliteNodeType.SpellRuins,
            RogueliteNodeType.CardMerchant,
            RogueliteNodeType.TreasureVault,
            RogueliteNodeType.RelicShrine,
            RogueliteNodeType.DeckWorkshop,
            RogueliteNodeType.DeckForge,
            RogueliteNodeType.HealingSpring,
            RogueliteNodeType.RestCamp,
            RogueliteNodeType.Mystery,
            RogueliteNodeType.MysteryEvent,
            RogueliteNodeType.ForbiddenAltar
        };

        public static List<StoryMapRecord> GenerateRun(long seed)
        {
            var result = new List<StoryMapRecord>(ActCount);
            for (int act = 1; act <= ActCount; act++)
                result.Add(GenerateAct(seed, act));
            return result;
        }

        public static StoryMapRecord GenerateAct(long seed, int act)
        {
            int safeAct = Mathf.Clamp(act, 1, ActCount);
            int layerCount = 9 + safeAct - 1;
            string mapId = $"procedural-act-{safeAct}";
            var map = new StoryMapRecord
            {
                mapId = mapId,
                displayName = ActNames[safeAct - 1],
                backgroundResourcePath = string.Empty,
                startNodeId = "start",
                bossNodeId = "boss"
            };

            var layers = new List<List<StoryMapNodeRecord>>(layerCount);
            for (int layer = 0; layer < layerCount; layer++)
            {
                List<StoryMapNodeRecord> row = BuildLayer(
                    seed, safeAct, layer, layerCount, mapId);
                layers.Add(row);
                map.nodes.AddRange(row);
            }

            var incoming = new HashSet<string>(StringComparer.Ordinal);
            for (int layer = 0; layer + 1 < layers.Count; layer++)
            {
                List<StoryMapNodeRecord> current = layers[layer];
                List<StoryMapNodeRecord> next = layers[layer + 1];
                foreach (StoryMapNodeRecord from in current)
                {
                    int connections = next.Count > 1 ? 2 : 1;
                    foreach (StoryMapNodeRecord to in next
                                 .OrderBy(candidate => Mathf.Abs(
                                     candidate.x - from.x))
                                 .ThenBy(candidate => StoryDeterminism.Hash(
                                     seed, mapId, from.nodeId,
                                     candidate.nodeId, "edge"))
                                 .Take(connections))
                    {
                        AddEdge(map, incoming, from, to);
                    }
                }

                foreach (StoryMapNodeRecord to in next.Where(candidate =>
                             !incoming.Contains(candidate.nodeId)))
                {
                    StoryMapNodeRecord closest = current.OrderBy(candidate =>
                        Mathf.Abs(candidate.x - to.x)).First();
                    AddEdge(map, incoming, closest, to);
                }
            }
            return map;
        }

        private static List<StoryMapNodeRecord> BuildLayer(
            long seed,
            int act,
            int layer,
            int layerCount,
            string mapId)
        {
            if (layer == 0)
                return new List<StoryMapNodeRecord>
                {
                    CreateNode("start", RogueliteNodeType.Start,
                        0.5f, 0.055f)
                };
            if (layer == layerCount - 1)
                return new List<StoryMapNodeRecord>
                {
                    CreateNode("boss", RogueliteNodeType.Boss,
                        0.5f, 0.945f)
                };

            int laneCount = 2 + StoryDeterminism.Index(
                2, seed, mapId, layer, "lanes");
            float y = Mathf.Lerp(0.15f, 0.85f,
                (layer - 1f) / Math.Max(1f, layerCount - 3f));
            var result = new List<StoryMapNodeRecord>(laneCount);
            for (int lane = 0; lane < laneCount; lane++)
            {
                float x = laneCount == 2
                    ? (lane == 0 ? 0.30f : 0.70f)
                    : 0.20f + lane * 0.30f;
                float jitter = (StoryDeterminism.Index(
                    101, seed, mapId, layer, lane, "jitter") - 50) / 2500f;
                RogueliteNodeType type = ResolveLayerType(
                    seed, act, layer, layerCount, lane);
                result.Add(CreateNode(
                    $"l{layer:00}-n{lane + 1:00}",
                    type,
                    Mathf.Clamp(x + jitter, 0.14f, 0.86f),
                    y));
            }
            return result;
        }

        private static RogueliteNodeType ResolveLayerType(
            long seed,
            int act,
            int layer,
            int layerCount,
            int lane)
        {
            // Toda rota atravessa as camadas 2 e 5. Como todos os nós dessas
            // camadas são combates, qualquer caminho válido contém ao menos
            // dois duelistas antes do chefe.
            if (layer == 2) return RogueliteNodeType.NormalDuel;
            if (layer == 5)
                return act == 1 && lane % 2 == 0
                    ? RogueliteNodeType.NormalDuel
                    : RogueliteNodeType.EliteDuel;
            if (layer == layerCount - 2)
            {
                RogueliteNodeType[] preparation =
                {
                    RogueliteNodeType.HealingSpring,
                    RogueliteNodeType.DeckForge,
                    RogueliteNodeType.RelicShrine
                };
                return preparation[lane % preparation.Length];
            }
            if (layer == 1 && lane == 0)
                return RogueliteNodeType.CardMerchant;
            if (layer == 3 && lane == 0)
                return RogueliteNodeType.RelicShrine;
            if (act >= 2 && layer == layerCount - 3 && lane == 0)
                return RogueliteNodeType.FinalDuelArena;

            int index = StoryDeterminism.Index(
                UtilityPool.Length, seed, act, layer, lane, "node-type");
            return UtilityPool[index];
        }

        private static StoryMapNodeRecord CreateNode(
            string nodeId,
            RogueliteNodeType type,
            float x,
            float y)
        {
            return new StoryMapNodeRecord
            {
                nodeId = nodeId,
                type = type.ToString(),
                publicLabel = StoryContentCatalog.PublicNodeLabel(type),
                x = x,
                y = y,
                markerOffsetY = type == RogueliteNodeType.Boss
                    ? -0.055f
                    : 0.050f,
                encounterPoolId = "procedural",
                rewardTableId = "story-default",
                configId = "procedural-v2"
            };
        }

        private static void AddEdge(
            StoryMapRecord map,
            HashSet<string> incoming,
            StoryMapNodeRecord from,
            StoryMapNodeRecord to)
        {
            if (map.edges.Any(edge => string.Equals(
                    edge.fromNodeId, from.nodeId, StringComparison.Ordinal) &&
                string.Equals(edge.toNodeId, to.nodeId,
                    StringComparison.Ordinal)))
                return;
            map.edges.Add(new StoryMapEdgeRecord
            {
                edgeId = $"{from.nodeId}-{to.nodeId}",
                fromNodeId = from.nodeId,
                toNodeId = to.nodeId
            });
            incoming.Add(to.nodeId);
        }
    }
}
