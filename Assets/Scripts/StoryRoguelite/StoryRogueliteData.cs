using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    public enum GameplayMode
    {
        Standard = 0,
        StoryRoguelite = 1
    }

    public enum StoryRunStatus
    {
        Active = 0,
        Completed = 1,
        Failed = 2,
        Abandoned = 3,
        Legacy = 4
    }

    public enum RogueliteNodeType
    {
        Start,
        NormalDuel,
        EliteDuel,
        FinalDuelArena,
        Boss,
        CardPack,
        SpellRuins,
        CardMerchant,
        TreasureVault,
        RelicShrine,
        DeckWorkshop,
        DeckForge,
        HealingSpring,
        RestCamp,
        Mystery,
        MysteryEvent,
        ForbiddenAltar
    }

    public enum RogueliteNodeState
    {
        Locked,
        Available,
        Selected,
        Current,
        Completed,
        BlockedByChoice
    }

    public enum NpcPresentation
    {
        Masculine,
        Feminine,
        Neutral,
        Unspecified
    }

    [Flags]
    public enum EncounterRole
    {
        None = 0,
        Normal = 1 << 0,
        Elite = 1 << 1,
        Boss = 1 << 2,
        Rival = 1 << 3,
        Merchant = 1 << 4,
        Event = 1 << 5
    }

    public enum NpcDeckSelectionMode
    {
        Fixed,
        Seasonal,
        WeightedPool
    }

    public enum StoryLpRisk
    {
        Safe,
        HighImpact,
        UnusableAt6000,
        ManualReview
    }

    [Serializable]
    public sealed class StoryRuleProfile
    {
        public int startingMainDeckSize = 20;
        public int minimumMainDeckSize = 20;
        public int maximumMainDeckSize = 30;
        public int maximumExtraDeckSize = 15;
        public int playerStartingLifePoints = 6000;
        public int startingHandSize = 5;
        public int sealsAtRunStart = 3;

        public StoryDeckValidationResult Validate(
            IReadOnlyList<string> main,
            IReadOnlyList<string> extra,
            bool isRunStart,
            BanlistService banlist = null)
        {
            var result = new StoryDeckValidationResult();
            main ??= Array.Empty<string>();
            extra ??= Array.Empty<string>();
            int requiredMinimum = isRunStart
                ? startingMainDeckSize
                : minimumMainDeckSize;
            int requiredMaximum = isRunStart
                ? startingMainDeckSize
                : maximumMainDeckSize;
            if (main.Count < requiredMinimum || main.Count > requiredMaximum)
            {
                result.errors.Add(isRunStart
                    ? $"A jornada deve começar com exatamente {startingMainDeckSize} cartas no Deck Principal."
                    : $"O Deck Principal da jornada deve ter {minimumMainDeckSize}–{maximumMainDeckSize} cartas.");
            }
            if (extra.Count > maximumExtraDeckSize)
                result.errors.Add($"O Deck Adicional deve ter no máximo {maximumExtraDeckSize} cartas.");

            var copies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string raw in main.Concat(extra))
            {
                string cardId = BanlistService.NormalizePasscode(raw);
                if (string.IsNullOrEmpty(cardId))
                {
                    result.errors.Add("O deck contém uma identificação de carta inválida.");
                    continue;
                }
                copies.TryGetValue(cardId, out int count);
                copies[cardId] = count + 1;
            }
            banlist ??= BanlistService.Active;
            foreach (KeyValuePair<string, int> copy in copies)
            {
                int limit = banlist.MaximumCopies(copy.Key);
                if (copy.Value > limit)
                    result.errors.Add($"A carta {copy.Key} possui {copy.Value} cópias; o limite atual é {limit}.");
            }
            return result;
        }
    }

    [Serializable]
    public sealed class StoryDeckValidationResult
    {
        public List<string> errors = new();
        public bool IsValid => errors.Count == 0;
        public string Summary => IsValid
            ? "Deck válido para Crônicas do Duelo."
            : string.Join(" ", errors.Distinct());
    }

    [Serializable]
    public sealed class StoryMapNodeRecord
    {
        public string nodeId;
        public string type;
        public string publicLabel;
        public float x;
        public float y;
        public float markerOffsetX;
        public float markerOffsetY;
        public string encounterPoolId;
        public string rewardTableId;
        public string configId;

        public RogueliteNodeType NodeType =>
            StoryContentCatalog.ParseNodeType(type);
        public Vector2 NormalizedPosition => new(x, y);
        public Vector2 MarkerOffset => new(markerOffsetX, markerOffsetY);
    }

    [Serializable]
    public sealed class StoryMapEdgeRecord
    {
        public string edgeId;
        public string fromNodeId;
        public string toNodeId;
        public List<StoryPointRecord> controlPoints = new();
    }

    [Serializable]
    public sealed class StoryPointRecord
    {
        public float x;
        public float y;
        public Vector2 Point => new(x, y);
    }

    [Serializable]
    public sealed class StoryMapRecord
    {
        public string mapId;
        public string displayName;
        public string backgroundResourcePath;
        public string startNodeId;
        public string bossNodeId;
        public List<StoryMapNodeRecord> nodes = new();
        public List<StoryMapEdgeRecord> edges = new();

        public StoryMapNodeRecord Node(string nodeId) => nodes.FirstOrDefault(
            node => string.Equals(node.nodeId, nodeId, StringComparison.Ordinal));

        public StoryMapEdgeRecord Edge(string from, string to) => edges.FirstOrDefault(
            edge => string.Equals(edge.fromNodeId, from, StringComparison.Ordinal) &&
                    string.Equals(edge.toNodeId, to, StringComparison.Ordinal));
    }

    [Serializable]
    public sealed class StoryNpcRecord
    {
        public string npcId;
        public string displayName;
        public string portraitResourcePath;
        public string presentation;
        public string role;
        public int firstAct = 1;
        public int lastAct = 3;
        public int aiTierMin = 1;
        public int aiTierMax = 2;
        public string fixedDeckId;
        public string personalityProfileId;
        public string dialogueSetId;
        public bool recurring;
        public bool enabled = true;
    }

    [Serializable]
    public sealed class StoryContentCatalogFile
    {
        public int schemaVersion = 1;
        public int generatorVersion = 1;
        public string seasonId;
        public List<StoryNpcRecord> npcs = new();
        public List<StoryMapRecord> maps = new();
    }

    public static class StoryContentCatalog
    {
        public const string ResourcePath = "StoryRoguelite/StoryContentCatalog";
        public const string DefaultSeasonId = "story-roguelite-v1";
        private static StoryContentCatalogFile cached;

        public static StoryContentCatalogFile Load()
        {
            if (cached != null) return cached;
            TextAsset json = Resources.Load<TextAsset>(ResourcePath);
            if (json == null || string.IsNullOrWhiteSpace(json.text))
                throw new InvalidOperationException(
                    $"Catálogo roguelite ausente em Resources/{ResourcePath}.json.");
            cached = JsonUtility.FromJson<StoryContentCatalogFile>(json.text);
            if (cached == null || cached.maps == null || cached.maps.Count == 0)
                throw new InvalidOperationException("O catálogo roguelite não possui mapas válidos.");
            cached.npcs ??= new List<StoryNpcRecord>();
            cached.maps ??= new List<StoryMapRecord>();
            return cached;
        }

        public static void ClearCache() => cached = null;

        public static NpcPresentation ParsePresentation(string value) =>
            Enum.TryParse(value, true, out NpcPresentation parsed)
                ? parsed
                : NpcPresentation.Unspecified;

        public static EncounterRole ParseRole(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return EncounterRole.Normal;
            EncounterRole result = EncounterRole.None;
            foreach (string part in value.Split('|'))
            {
                if (Enum.TryParse(part.Trim(), true, out EncounterRole parsed))
                    result |= parsed;
            }
            return result == EncounterRole.None ? EncounterRole.Normal : result;
        }

        public static RogueliteNodeType ParseNodeType(string value) =>
            Enum.TryParse(value, true, out RogueliteNodeType parsed)
                ? parsed
                : RogueliteNodeType.Mystery;

        public static StoryMapRecord ResolveMap(string mapId)
        {
            return Load().maps.FirstOrDefault(map =>
                string.Equals(map.mapId, mapId, StringComparison.Ordinal));
        }

        public static IReadOnlyList<StoryNpcRecord> RuntimeNpcs()
        {
            StoryNpcCatalog configured = Resources.Load<StoryNpcCatalog>(
                "StoryRoguelite/Generated/StoryNpcCatalog");
            if (configured != null && configured.All.Count > 0)
                return configured.All
                    .Where(npc => npc != null)
                    .Select(npc => npc.ToRuntimeRecord())
                    .ToArray();
            return Load().npcs;
        }

        public static StoryNpcRecord ResolveNpc(string npcId) => RuntimeNpcs()
            .FirstOrDefault(npc => string.Equals(
                npc.npcId, npcId, StringComparison.Ordinal));

        public static string PublicNodeLabel(RogueliteNodeType type)
        {
            return type switch
            {
                RogueliteNodeType.Start => "PORTÃO DO DUELISTA",
                RogueliteNodeType.NormalDuel => "ARENA DE DUELO",
                RogueliteNodeType.EliteDuel => "DUELO DE ELITE",
                RogueliteNodeType.FinalDuelArena => "ARENA FINAL",
                RogueliteNodeType.Boss => "MESTRE DO DUELO SOMBRIO",
                RogueliteNodeType.CardPack => "PACOTE DE CARTAS",
                RogueliteNodeType.SpellRuins => "RUÍNAS DE MAGIAS",
                RogueliteNodeType.CardMerchant => "MERCADOR DE CARTAS",
                RogueliteNodeType.TreasureVault => "COFRE DO TESOURO",
                RogueliteNodeType.RelicShrine => "SANTUÁRIO DE RELÍQUIAS",
                RogueliteNodeType.DeckWorkshop => "OFICINA DO DECK",
                RogueliteNodeType.DeckForge => "FORJA DO DECK",
                RogueliteNodeType.HealingSpring => "FONTE DE CURA",
                RogueliteNodeType.RestCamp => "ACAMPAMENTO",
                RogueliteNodeType.Mystery => "MISTÉRIO",
                RogueliteNodeType.MysteryEvent => "EVENTO MISTERIOSO",
                RogueliteNodeType.ForbiddenAltar => "ALTAR PROIBIDO",
                _ => type.ToString()
            };
        }
    }

    public static class StoryDeterminism
    {
        public static ulong Hash(params object[] values)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (object value in values ?? Array.Empty<object>())
                {
                    string text = value?.ToString() ?? string.Empty;
                    foreach (char character in text)
                    {
                        hash ^= character;
                        hash *= 1099511628211UL;
                    }
                    hash ^= 0xff;
                    hash *= 1099511628211UL;
                }
                return hash;
            }
        }

        public static int Index(int count, params object[] values) => count <= 0
            ? -1
            : (int)(Hash(values) % (ulong)count);

        public static List<T> Shuffle<T>(IEnumerable<T> source, params object[] values)
        {
            var result = source?.ToList() ?? new List<T>();
            ulong state = Hash(values);
            for (int index = result.Count - 1; index > 0; index--)
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                int selected = (int)(z % (ulong)(index + 1));
                (result[index], result[selected]) = (result[selected], result[index]);
            }
            return result;
        }
    }
}
