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
        public const int GeneratorVersion = 4;
        public const int ActCount = 3;
        public const int MinimumDuelsBeforeBoss = 2;
        public const int MinimumMerchantLayer = 6;

        private static readonly string[] ActNames =
        {
            "Caminhos de Obsidiana",
            "Circuito do Eclipse",
            "Trono do Arcano"
        };

        // A abertura oferece preparação ou recursos sem exigir moeda. Assim,
        // toda primeira escolha é útil mesmo com zero Fragmentos Arcanos.
        private static readonly RogueliteNodeType[] OpeningPool =
        {
            RogueliteNodeType.CardPack,
            RogueliteNodeType.SpellRuins,
            RogueliteNodeType.TreasureVault,
            RogueliteNodeType.DeckWorkshop,
            RogueliteNodeType.RestCamp,
            RogueliteNodeType.MysteryEvent
        };

        // O mercador é planejado separadamente para aparecer somente depois
        // dos dois duelos que garantem moeda. Os demais objetivos continuam
        // procedurais e não se repetem dentro da mesma camada.
        private static readonly RogueliteNodeType[] ProgressionPool =
        {
            RogueliteNodeType.CardPack,
            RogueliteNodeType.SpellRuins,
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

        private static readonly RogueliteNodeType[] PreparationPool =
        {
            RogueliteNodeType.HealingSpring,
            RogueliteNodeType.DeckForge,
            RogueliteNodeType.RelicShrine,
            RogueliteNodeType.RestCamp
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

            EnforceRandomEventCount(map, seed, safeAct);

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

        private static void EnforceRandomEventCount(
            StoryMapRecord map,
            long seed,
            int act)
        {
            StoryRandomEventProfile profile = StoryRandomEventLibrary.Profile;
            if (profile == null || !profile.enabled) return;
            int minimum = Mathf.Max(0, profile.MinimumForAct(act));
            int maximum = Mathf.Max(minimum, profile.MaximumForAct(act));
            int target = minimum + StoryDeterminism.Index(
                maximum - minimum + 1, seed, act, "event-node-count-v1");

            List<StoryMapNodeRecord> eventNodes = map.nodes
                .Where(node => node.NodeType ==
                    RogueliteNodeType.MysteryEvent)
                .OrderBy(node => StoryDeterminism.Hash(
                    seed, act, node.nodeId, "event-keep-v1"))
                .ToList();
            foreach (StoryMapNodeRecord excess in eventNodes.Skip(target))
                SetNodeType(excess, ResolveFallbackType(map, excess));

            int missing = Math.Max(0, target - eventNodes.Count);
            if (missing == 0) return;
            HashSet<RogueliteNodeType> replaceable = new()
            {
                RogueliteNodeType.CardPack,
                RogueliteNodeType.SpellRuins,
                RogueliteNodeType.TreasureVault,
                RogueliteNodeType.DeckWorkshop,
                RogueliteNodeType.DeckForge,
                RogueliteNodeType.HealingSpring,
                RogueliteNodeType.RestCamp,
                RogueliteNodeType.Mystery,
                RogueliteNodeType.ForbiddenAltar
            };
            HashSet<float> occupiedLayers = map.nodes
                .Where(node => node.NodeType ==
                    RogueliteNodeType.MysteryEvent)
                .Select(node => node.y)
                .ToHashSet();
            foreach (StoryMapNodeRecord candidate in map.nodes
                         .Where(node => replaceable.Contains(node.NodeType))
                         .Where(node => !occupiedLayers.Contains(node.y))
                         .GroupBy(node => node.y)
                         .Select(group => group.OrderBy(node =>
                             StoryDeterminism.Hash(seed, act, node.nodeId,
                                 "event-add-lane-v1")).First())
                         .OrderBy(node => StoryDeterminism.Hash(
                             seed, act, node.nodeId, "event-add-v1"))
                         .Take(missing))
                SetNodeType(candidate, RogueliteNodeType.MysteryEvent);
        }

        private static RogueliteNodeType ResolveFallbackType(
            StoryMapRecord map,
            StoryMapNodeRecord node)
        {
            RogueliteNodeType[] candidates =
            {
                RogueliteNodeType.TreasureVault,
                RogueliteNodeType.CardPack,
                RogueliteNodeType.SpellRuins,
                RogueliteNodeType.DeckWorkshop,
                RogueliteNodeType.RestCamp
            };
            HashSet<RogueliteNodeType> used = map.nodes
                .Where(other => other != node &&
                    Mathf.Approximately(other.y, node.y))
                .Select(other => other.NodeType)
                .ToHashSet();
            return candidates.FirstOrDefault(type => !used.Contains(type));
        }

        private static void SetNodeType(
            StoryMapNodeRecord node,
            RogueliteNodeType type)
        {
            node.type = type.ToString();
            node.publicLabel = StoryContentCatalog.PublicNodeLabel(type);
            node.configId = "procedural-v4";
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
                    seed, act, layer, layerCount, lane, laneCount);
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
            int lane,
            int laneCount)
        {
            // Toda rota atravessa as camadas 2 e 5. Como todos os nós dessas
            // camadas são combates, qualquer caminho válido contém ao menos
            // dois duelistas antes do chefe.
            if (layer == 2) return RogueliteNodeType.NormalDuel;
            if (layer == 5)
                return act == 1 && lane % 2 == 0
                    ? RogueliteNodeType.NormalDuel
                    : RogueliteNodeType.EliteDuel;
            if (act >= 2 && layer == layerCount - 3 && lane == 0)
                return RogueliteNodeType.FinalDuelArena;

            int merchantLayer = ResolveMerchantLayer(
                seed, act, layerCount);
            int merchantLane = StoryDeterminism.Index(
                laneCount, seed, act, merchantLayer, "merchant-lane");
            if (layer == merchantLayer && lane == merchantLane)
                return RogueliteNodeType.CardMerchant;

            if (layer == 1)
                return ResolvePoolType(
                    OpeningPool, seed, act, layer, lane, "opening");
            if (layer == layerCount - 2)
                return ResolvePoolType(
                    PreparationPool, seed, act, layer, lane, "preparation");
            return ResolvePoolType(
                ProgressionPool, seed, act, layer, lane, "progression");
        }

        private static int ResolveMerchantLayer(
            long seed,
            int act,
            int layerCount)
        {
            var candidates = new List<int>();
            for (int candidate = MinimumMerchantLayer;
                 candidate <= layerCount - 2;
                 candidate++)
            {
                // Nos atos avançados, a Arena Final ocupa a primeira rota da
                // camada narrativa. Reservá-la evita escolhas contraditórias.
                if (act >= 2 && candidate == layerCount - 3)
                    continue;
                candidates.Add(candidate);
            }
            return candidates[StoryDeterminism.Index(
                candidates.Count, seed, act, "merchant-layer")];
        }

        private static RogueliteNodeType ResolvePoolType(
            IEnumerable<RogueliteNodeType> pool,
            long seed,
            int act,
            int layer,
            int lane,
            string phase)
        {
            List<RogueliteNodeType> shuffled = StoryDeterminism.Shuffle(
                pool, seed, act, layer, phase);
            return shuffled[lane % shuffled.Count];
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
                configId = "procedural-v4"
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
