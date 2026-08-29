using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public enum MissionTier
    {
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3
    }

    public enum MissionCategory
    {
        Duel,
        Online,
        Login,
        Economy
    }

    public enum MissionScope
    {
        Global,
        OnlineAny,
        OnlineRanked,
        OnlineTournament,
        StoryRoguelite,
        Collection
    }

    public enum MissionMetric
    {
        CardsDestroyed,
        MonstersSummoned,
        OnlineMatchesPlayed,
        OnlineMatchesWon,
        DamageDealt,
        TrapsActivated,
        SpellsActivated,
        SpellsOrTrapsActivated,
        AccountCoinsEarnedEligible,
        DailyLogin
    }

    [Serializable]
    public sealed class MissionDefinitionData
    {
        public string missionId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public MissionTier tier = MissionTier.Tier1;
        public MissionCategory category = MissionCategory.Duel;
        public MissionScope scope = MissionScope.Global;
        public MissionMetric metric;
        public int targetValue = 1;
        public int rewardCoins = 1;
        [Min(1)] public int weight = 1;
        public bool active = true;
        public string prerequisiteMissionId;

        public void Normalize()
        {
            missionId = (missionId ?? string.Empty).Trim();
            displayName = (displayName ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();
            prerequisiteMissionId =
                (prerequisiteMissionId ?? string.Empty).Trim();
            targetValue = Math.Max(1, targetValue);
            rewardCoins = Math.Max(1, rewardCoins);
            weight = Math.Max(1, weight);
        }
    }

    [CreateAssetMenu(
        fileName = "MissionCatalog",
        menuName = "Arcane Arena/Missões/Catálogo")]
    public sealed class MissionCatalog : ScriptableObject
    {
        public const int CurrentCatalogVersion = 1;
        private const string ResourcePath = "Missions/MissionCatalog";
        private static MissionCatalog _runtime;

        [SerializeField] private int catalogVersion = CurrentCatalogVersion;
        [SerializeField] private List<MissionDefinitionData> definitions =
            new();

        public int CatalogVersion => Math.Max(1, catalogVersion);
        public IReadOnlyList<MissionDefinitionData> Definitions => definitions;

        public MissionDefinitionData Find(string missionId)
        {
            return definitions?.FirstOrDefault(item => item != null &&
                string.Equals(item.missionId, missionId,
                    StringComparison.Ordinal));
        }

        public static MissionCatalog LoadRuntime()
        {
            if (_runtime != null)
                return _runtime;
            _runtime = Resources.Load<MissionCatalog>(ResourcePath);
            if (_runtime != null)
            {
                _runtime.Normalize();
                return _runtime;
            }

            // Compatibilidade segura para uma build que ainda não tenha
            // importado o asset. O catálogo continua sendo o único ponto de
            // leitura do sistema e pode ser substituído pelo asset no Editor.
            _runtime = CreateInstance<MissionCatalog>();
            _runtime.name = "Catálogo de Missões (Runtime)";
            _runtime.definitions = MissionCatalogDefaults.Create();
            _runtime.Normalize();
            return _runtime;
        }

        public void ReplaceDefinitionsForTests(
            IEnumerable<MissionDefinitionData> values)
        {
            definitions = values?.Where(item => item != null).ToList() ??
                          new List<MissionDefinitionData>();
            Normalize();
        }

        private void OnValidate() => Normalize();

        private void Normalize()
        {
            catalogVersion = Math.Max(1, catalogVersion);
            definitions ??= new List<MissionDefinitionData>();
            definitions.RemoveAll(item => item == null);
            foreach (MissionDefinitionData item in definitions)
                item.Normalize();
            definitions = definitions
                .Where(item => !string.IsNullOrWhiteSpace(item.missionId))
                .GroupBy(item => item.missionId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }
    }

    public static class MissionCatalogDefaults
    {
        public static List<MissionDefinitionData> Create()
        {
            return new List<MissionDefinitionData>
            {
                D("t1-destroy-5", "Quebre a formação", "Destrua 5 cartas.",
                    MissionTier.Tier1, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.CardsDestroyed, 5, 8),
                D("t1-summon-10", "Reúna suas forças", "Invoque 10 monstros.",
                    MissionTier.Tier1, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.MonstersSummoned, 10, 8),
                D("t1-online-2", "Entre na arena", "Conclua 2 duelos online.",
                    MissionTier.Tier1, MissionCategory.Online,
                    MissionScope.OnlineAny, MissionMetric.OnlineMatchesPlayed,
                    2, 10),
                D("t1-login", "Presença confirmada", "Entre no jogo neste ciclo.",
                    MissionTier.Tier1, MissionCategory.Login,
                    MissionScope.Global, MissionMetric.DailyLogin, 1, 10),
                D("t1-damage-3000", "Primeiro impacto",
                    "Cause 3.000 de dano em duelos online.", MissionTier.Tier1,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.DamageDealt, 3000, 10),
                D("t1-trap-5", "Armadilha preparada", "Ative 5 Armadilhas.",
                    MissionTier.Tier1, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.TrapsActivated, 5, 8),
                D("t1-spell-5", "Primeira fórmula", "Ative 5 Magias.",
                    MissionTier.Tier1, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.SpellsActivated, 5, 8),
                D("t1-win-1", "Primeira vitória online",
                    "Vença 1 duelo online.", MissionTier.Tier1,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.OnlineMatchesWon, 1, 12),
                D("t1-summon-6-online", "Presença na rede",
                    "Invoque 6 monstros em duelos online.", MissionTier.Tier1,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.MonstersSummoned, 6, 10),
                D("t1-destroy-3-online", "Ruptura conectada",
                    "Destrua 3 cartas em duelos online.", MissionTier.Tier1,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.CardsDestroyed, 3, 10),
                D("t1-damage-5000-global", "Golpe de abertura",
                    "Cause 5.000 de dano em qualquer duelo.", MissionTier.Tier1,
                    MissionCategory.Duel, MissionScope.Global,
                    MissionMetric.DamageDealt, 5000, 9),
                D("t1-ranked-play-1", "Entrada competitiva",
                    "Conclua 1 duelo ranqueado.", MissionTier.Tier1,
                    MissionCategory.Online, MissionScope.OnlineRanked,
                    MissionMetric.OnlineMatchesPlayed, 1, 12),

                D("t2-destroy-20", "Campo desfeito", "Destrua 20 cartas.",
                    MissionTier.Tier2, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.CardsDestroyed, 20, 20),
                D("t2-summon-40", "Exército arcano", "Invoque 40 monstros.",
                    MissionTier.Tier2, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.MonstersSummoned, 40, 20),
                D("t2-online-5", "Duelista conectado", "Conclua 5 duelos online.",
                    MissionTier.Tier2, MissionCategory.Online,
                    MissionScope.OnlineAny, MissionMetric.OnlineMatchesPlayed,
                    5, 20),
                D("t2-win-3", "Sequência vitoriosa", "Vença 3 duelos online.",
                    MissionTier.Tier2, MissionCategory.Online,
                    MissionScope.OnlineAny, MissionMetric.OnlineMatchesWon,
                    3, 25),
                D("t2-damage-10000", "Pressão constante",
                    "Cause 10.000 de dano.", MissionTier.Tier2,
                    MissionCategory.Duel, MissionScope.Global,
                    MissionMetric.DamageDealt, 10000, 22),
                D("t2-activate-15", "Domínio das fórmulas",
                    "Ative 15 Magias ou Armadilhas.", MissionTier.Tier2,
                    MissionCategory.Duel, MissionScope.Global,
                    MissionMetric.SpellsOrTrapsActivated, 15, 20),
                D("t2-spell-12", "Biblioteca arcana", "Ative 12 Magias.",
                    MissionTier.Tier2, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.SpellsActivated, 12, 20),
                D("t2-trap-10", "Rede de contenção", "Ative 10 Armadilhas.",
                    MissionTier.Tier2, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.TrapsActivated, 10, 20),
                D("t2-ranked-play-3", "Circuito competitivo",
                    "Conclua 3 duelos ranqueados.", MissionTier.Tier2,
                    MissionCategory.Online, MissionScope.OnlineRanked,
                    MissionMetric.OnlineMatchesPlayed, 3, 25),
                D("t2-ranked-win-2", "Ascensão ranqueada",
                    "Vença 2 duelos ranqueados.", MissionTier.Tier2,
                    MissionCategory.Online, MissionScope.OnlineRanked,
                    MissionMetric.OnlineMatchesWon, 2, 30),
                D("t2-online-summon-25", "Exército conectado",
                    "Invoque 25 monstros em duelos online.", MissionTier.Tier2,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.MonstersSummoned, 25, 24),
                D("t2-online-destroy-12", "Demolição online",
                    "Destrua 12 cartas em duelos online.", MissionTier.Tier2,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.CardsDestroyed, 12, 24),

                D("t3-destroy-50-online", "Ruptura total",
                    "Destrua 50 cartas em duelos online.", MissionTier.Tier3,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.CardsDestroyed, 50, 40),
                D("t3-win-6", "Conquistador da rede", "Vença 6 duelos online.",
                    MissionTier.Tier3, MissionCategory.Online,
                    MissionScope.OnlineAny, MissionMetric.OnlineMatchesWon,
                    6, 45),
                D("t3-online-10", "Maratona de duelos",
                    "Conclua 10 duelos online.", MissionTier.Tier3,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.OnlineMatchesPlayed, 10, 35),
                D("t3-damage-25000", "Poder esmagador",
                    "Cause 25.000 de dano em duelos online.", MissionTier.Tier3,
                    MissionCategory.Online, MissionScope.OnlineAny,
                    MissionMetric.DamageDealt, 25000, 40),
                D("t3-summon-100", "Legião invocada", "Invoque 100 monstros.",
                    MissionTier.Tier3, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.MonstersSummoned,
                    100, 40),
                D("t3-coins-75", "Tesouro conquistado",
                    "Ganhe 75 moedas em fontes elegíveis.", MissionTier.Tier3,
                    MissionCategory.Economy, MissionScope.Collection,
                    MissionMetric.AccountCoinsEarnedEligible, 75, 50),
                D("t3-ranked-play-8", "Veterano competitivo",
                    "Conclua 8 duelos ranqueados.", MissionTier.Tier3,
                    MissionCategory.Online, MissionScope.OnlineRanked,
                    MissionMetric.OnlineMatchesPlayed, 8, 45),
                D("t3-ranked-win-4", "Dominador ranqueado",
                    "Vença 4 duelos ranqueados.", MissionTier.Tier3,
                    MissionCategory.Online, MissionScope.OnlineRanked,
                    MissionMetric.OnlineMatchesWon, 4, 50),
                D("t3-ranked-damage-20000", "Pressão competitiva",
                    "Cause 20.000 de dano em duelos ranqueados.",
                    MissionTier.Tier3, MissionCategory.Online,
                    MissionScope.OnlineRanked, MissionMetric.DamageDealt,
                    20000, 48),
                D("t3-activate-30", "Mestre das respostas",
                    "Ative 30 Magias ou Armadilhas.", MissionTier.Tier3,
                    MissionCategory.Duel, MissionScope.Global,
                    MissionMetric.SpellsOrTrapsActivated, 30, 40),
                D("t3-trap-25", "Arquiteto de armadilhas",
                    "Ative 25 Armadilhas.", MissionTier.Tier3,
                    MissionCategory.Duel, MissionScope.Global,
                    MissionMetric.TrapsActivated, 25, 40),
                D("t3-spell-25", "Arquimago do duelo", "Ative 25 Magias.",
                    MissionTier.Tier3, MissionCategory.Duel,
                    MissionScope.Global, MissionMetric.SpellsActivated, 25, 40)
            };
        }

        private static MissionDefinitionData D(
            string id,
            string name,
            string description,
            MissionTier tier,
            MissionCategory category,
            MissionScope scope,
            MissionMetric metric,
            int target,
            int reward)
        {
            return new MissionDefinitionData
            {
                missionId = id,
                displayName = name,
                description = description,
                tier = tier,
                category = category,
                scope = scope,
                metric = metric,
                targetValue = target,
                rewardCoins = reward,
                weight = 1,
                active = true
            };
        }
    }

    [Serializable]
    public sealed class MissionProgressState
    {
        public string missionInstanceId;
        public string definitionId;
        public string displayName;
        public string description;
        public MissionTier tier;
        public MissionScope scope;
        public MissionMetric metric;
        public long currentValue;
        public long targetValue;
        public int rewardCoins;
        public bool completed;
        public bool rewardClaimed;

        public void Normalize()
        {
            missionInstanceId = (missionInstanceId ?? string.Empty).Trim();
            definitionId = (definitionId ?? string.Empty).Trim();
            displayName = (displayName ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();
            targetValue = Math.Max(1, targetValue);
            currentValue = Math.Max(0, Math.Min(currentValue, targetValue));
            rewardCoins = Math.Max(1, rewardCoins);
            completed = completed || currentValue >= targetValue;
        }
    }

    [Serializable]
    public sealed class MissionState
    {
        public int schemaVersion = 1;
        public string cycleId;
        public long cycleStartUtcTicks;
        public long cycleEndUtcTicks;
        public bool timeValidated;
        public long lastAuthoritativeUtcTicks;
        public string deviceSessionId;
        public List<MissionProgressState> missions = new();
        public List<string> claimedMissionInstanceIds = new();
        public List<string> resolvedRewardOperationIds = new();
        public List<string> processedProgressEventIds = new();
    }

    public static class MissionCycleRules
    {
        public const long CycleSeconds = 48L * 60L * 60L;

        public static string CycleId(long utcUnixSeconds) =>
            "utc48h:" + Math.Max(0, utcUnixSeconds) / CycleSeconds;

        public static long CycleStart(long utcUnixSeconds) =>
            Math.Max(0, utcUnixSeconds) / CycleSeconds * CycleSeconds;

        public static IReadOnlyList<MissionDefinitionData> Select(
            MissionCatalog catalog,
            string cycleId,
            string profileId)
        {
            if (catalog == null)
                return Array.Empty<MissionDefinitionData>();
            var selected = new List<MissionDefinitionData>(5);
            var families = new HashSet<string>(StringComparer.Ordinal);
            SelectTier(catalog, MissionTier.Tier1, 2, cycleId, profileId,
                selected, families);
            SelectTier(catalog, MissionTier.Tier2, 2, cycleId, profileId,
                selected, families);
            SelectTier(catalog, MissionTier.Tier3, 1, cycleId, profileId,
                selected, families);

            if (selected.Any(IsOnline))
                return selected;

            for (int index = selected.Count - 1; index >= 0; index--)
            {
                MissionTier tier = selected[index].tier;
                MissionDefinitionData replacement = Ordered(
                        catalog.Definitions.Where(item => item != null &&
                            item.active && item.tier == tier && IsOnline(item)),
                        cycleId, profileId)
                    .FirstOrDefault();
                if (replacement == null)
                    continue;
                selected[index] = replacement;
                break;
            }
            return selected;
        }

        private static void SelectTier(
            MissionCatalog catalog,
            MissionTier tier,
            int count,
            string cycleId,
            string profileId,
            ICollection<MissionDefinitionData> selected,
            ISet<string> families)
        {
            List<MissionDefinitionData> ordered = Ordered(
                    catalog.Definitions.Where(item => item != null &&
                        item.active && item.tier == tier &&
                        string.IsNullOrWhiteSpace(item.prerequisiteMissionId)),
                    cycleId, profileId)
                .ToList();
            foreach (MissionDefinitionData item in ordered)
            {
                if (selected.Count(candidate => candidate.tier == tier) >= count)
                    return;
                string family = Family(item.metric);
                if (families.Contains(family))
                    continue;
                selected.Add(item);
                families.Add(family);
            }
            foreach (MissionDefinitionData item in ordered)
            {
                if (selected.Count(candidate => candidate.tier == tier) >= count)
                    return;
                if (selected.Contains(item))
                    continue;
                selected.Add(item);
                families.Add(Family(item.metric));
            }
        }

        private static IEnumerable<MissionDefinitionData> Ordered(
            IEnumerable<MissionDefinitionData> source,
            string cycleId,
            string profileId)
        {
            return source.OrderBy(item =>
                StableHash($"{cycleId}|{profileId}|{item.missionId}") /
                (double)Math.Max(1, item.weight));
        }

        private static bool IsOnline(MissionDefinitionData item) =>
            item.scope == MissionScope.OnlineAny ||
            item.scope == MissionScope.OnlineRanked ||
            item.scope == MissionScope.OnlineTournament;

        private static string Family(MissionMetric metric)
        {
            return metric switch
            {
                MissionMetric.OnlineMatchesPlayed => "online-match",
                MissionMetric.OnlineMatchesWon => "online-match",
                MissionMetric.SpellsActivated => "activation",
                MissionMetric.TrapsActivated => "activation",
                MissionMetric.SpellsOrTrapsActivated => "activation",
                _ => metric.ToString()
            };
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
