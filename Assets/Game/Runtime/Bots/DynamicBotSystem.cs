using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.Game.Competitive;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public enum BotSkillLevel
    {
        Beginner = 1,
        Intermediate = 2,
        Advanced = 3,
        Expert = 4,
        Master = 5
    }

    public enum BotPersonality
    {
        Opportunist,
        Aggressive,
        Casual,
        Defensive,
        Curious,
        Balanced,
        Calculating,
        Creative
    }

    public enum BotArchetype
    {
        Aggro,
        Tempo,
        Midrange,
        Control,
        Combo,
        Toolbox,
        Otk
    }

    [Serializable]
    public sealed class BotProfile
    {
        public string botId;
        public string displayName;
        public BotSkillLevel skill;
        public BotPersonality personality;
        public BotArchetype primaryArchetype;
        public BotArchetype secondaryArchetype;
        public int initialRankPoints;
        public int minimumDeckPower;
        public int maximumDeckPower;
    }

    public readonly struct BotDifficultySettings
    {
        public BotDifficultySettings(
            float epsilon,
            float temperature,
            float maximumSuboptimalRate,
            int topK,
            float delayMultiplier)
        {
            Epsilon = epsilon;
            Temperature = temperature;
            MaximumSuboptimalRate = maximumSuboptimalRate;
            TopK = topK;
            DelayMultiplier = delayMultiplier;
        }

        public float Epsilon { get; }
        public float Temperature { get; }
        public float MaximumSuboptimalRate { get; }
        public int TopK { get; }
        public float DelayMultiplier { get; }
    }

    /// <summary>
    /// Catálogo versionado descrito pela especificação de bots. IDs são
    /// estáveis e nunca dependem do nome exibido, da cena ou de GameObjects.
    /// </summary>
    public static class DynamicBotCatalog
    {
        public const string SeasonId = "season-2026-01";
        public const int CatalogVersion = 1;
        public const string CatalogHash = "dynamic-bots-v1-33-2026-08-11";

        private static readonly BotProfile[] Profiles =
        {
            P("BOT_001", "Fraudinha", 1, BotPersonality.Opportunist, BotArchetype.Aggro, BotArchetype.Tempo),
            P("BOT_002", "Baroquinha", 1, BotPersonality.Aggressive, BotArchetype.Aggro),
            P("BOT_003", "Funciondicero", 1, BotPersonality.Casual, BotArchetype.Midrange),
            P("BOT_004", "Ordelei", 1, BotPersonality.Defensive, BotArchetype.Control),
            P("BOT_005", "Strada", 1, BotPersonality.Aggressive, BotArchetype.Combo),
            P("BOT_006", "Biólogo", 1, BotPersonality.Curious, BotArchetype.Toolbox),
            P("BOT_007", "Ana", 1, BotPersonality.Balanced, BotArchetype.Midrange),
            P("BOT_008", "Viniado", 2, BotPersonality.Aggressive, BotArchetype.Aggro, BotArchetype.Combo),
            P("BOT_009", "Coutinho", 2, BotPersonality.Balanced, BotArchetype.Midrange),
            P("BOT_010", "Taloson", 2, BotPersonality.Calculating, BotArchetype.Control),
            P("BOT_011", "Volpi", 2, BotPersonality.Opportunist, BotArchetype.Tempo),
            P("BOT_012", "Márcia", 2, BotPersonality.Defensive, BotArchetype.Control),
            P("BOT_013", "Jão", 2, BotPersonality.Aggressive, BotArchetype.Aggro),
            P("BOT_014", "Fisheballcat", 2, BotPersonality.Creative, BotArchetype.Toolbox),
            P("BOT_015", "Lucas Gay", 3, BotPersonality.Opportunist, BotArchetype.Combo, BotArchetype.Tempo),
            P("BOT_016", "Joseph Rosewel", 3, BotPersonality.Calculating, BotArchetype.Control),
            P("BOT_017", "But-2-eno", 3, BotPersonality.Balanced, BotArchetype.Midrange, BotArchetype.Combo),
            P("BOT_018", "Aura", 3, BotPersonality.Defensive, BotArchetype.Control, BotArchetype.Tempo),
            P("BOT_019", "Juan", 3, BotPersonality.Aggressive, BotArchetype.Aggro, BotArchetype.Combo),
            P("BOT_020", "Mario", 3, BotPersonality.Balanced, BotArchetype.Midrange),
            P("BOT_021", "Electrolux", 3, BotPersonality.Opportunist, BotArchetype.Toolbox, BotArchetype.Tempo),
            P("BOT_022", "Pablo Escobar", 4, BotPersonality.Aggressive, BotArchetype.Combo),
            P("BOT_023", "2,2,4-Trimetilpentano", 4, BotPersonality.Calculating, BotArchetype.Toolbox, BotArchetype.Combo),
            P("BOT_024", "Dr. Júlio", 4, BotPersonality.Calculating, BotArchetype.Control),
            P("BOT_025", "Shalon", 4, BotPersonality.Defensive, BotArchetype.Control, BotArchetype.Tempo),
            P("BOT_026", "Mustang", 4, BotPersonality.Aggressive, BotArchetype.Aggro, BotArchetype.Combo),
            P("BOT_027", "Seixas", 4, BotPersonality.Balanced, BotArchetype.Midrange, BotArchetype.Toolbox),
            P("BOT_028", "Exterminator", 5, BotPersonality.Aggressive, BotArchetype.Combo, BotArchetype.Otk),
            P("BOT_029", "Kindred", 5, BotPersonality.Calculating, BotArchetype.Control, BotArchetype.Tempo),
            P("BOT_030", "Boy Pinto", 5, BotPersonality.Opportunist, BotArchetype.Combo, BotArchetype.Toolbox),
            P("BOT_031", "Florêncio", 5, BotPersonality.Defensive, BotArchetype.Control),
            P("BOT_032", "Luffitoro", 5, BotPersonality.Balanced, BotArchetype.Midrange, BotArchetype.Combo),
            P("BOT_033", "Super Choque", 5, BotPersonality.Aggressive, BotArchetype.Tempo, BotArchetype.Combo)
        };

        public static IReadOnlyList<BotProfile> All => Profiles;

        public static BotProfile Find(string botId) => Profiles.FirstOrDefault(
            profile => string.Equals(profile.botId, botId, StringComparison.Ordinal));

        public static BotDifficultySettings Settings(BotSkillLevel level) => level switch
        {
            BotSkillLevel.Beginner => new BotDifficultySettings(.22f, 1.35f, .18f, 2, 1.20f),
            BotSkillLevel.Intermediate => new BotDifficultySettings(.12f, .95f, .10f, 3, 1.08f),
            BotSkillLevel.Advanced => new BotDifficultySettings(.06f, .65f, .05f, 4, 1.00f),
            BotSkillLevel.Expert => new BotDifficultySettings(.025f, .40f, .02f, 5, .90f),
            _ => new BotDifficultySettings(.005f, .18f, 0f, int.MaxValue, .82f)
        };

        public static string SkillName(BotSkillLevel level) => level switch
        {
            BotSkillLevel.Beginner => "INICIANTE",
            BotSkillLevel.Intermediate => "INTERMEDIÁRIO",
            BotSkillLevel.Advanced => "AVANÇADO",
            BotSkillLevel.Expert => "ESPECIALISTA",
            _ => "MESTRE"
        };

        private static BotProfile P(
            string id,
            string name,
            int skill,
            BotPersonality personality,
            BotArchetype primary,
            BotArchetype secondary = BotArchetype.Midrange)
        {
            BotSkillLevel level = (BotSkillLevel)skill;
            int[] seedPe = { 0, 25, 65, 105, 145, 175 };
            int[] minPower = { 0, 20, 35, 50, 65, 78 };
            int[] maxPower = { 0, 45, 60, 75, 88, 100 };
            return new BotProfile
            {
                botId = id,
                displayName = name,
                skill = level,
                personality = personality,
                primaryArchetype = primary,
                secondaryArchetype = secondary,
                initialRankPoints = seedPe[skill],
                minimumDeckPower = minPower[skill],
                maximumDeckPower = maxPower[skill]
            };
        }
    }

    [Serializable]
    public sealed class BotPersistentRecord
    {
        public string botId;
        public int rankedPoints;
        public int stateVersion = 1;
        public string seasonId;
        public string deckVariantId;
        public int wins;
        public int losses;
        public List<string> processedRankTransactions = new List<string>();
    }

    [Serializable]
    internal sealed class BotPersistentCollection
    {
        public int schemaVersion = 1;
        public string catalogHash = DynamicBotCatalog.CatalogHash;
        public List<BotPersistentRecord> bots = new List<BotPersistentRecord>();
    }

    public sealed class BotStateRepository
    {
        private static readonly int[] RankedSearchWindows = { 15, 30, 50, 200 };
        private readonly string path;
        private BotPersistentCollection state;

        public BotStateRepository(string path = null)
        {
            this.path = path ?? Path.Combine(
                Application.persistentDataPath, "ArcaneArena", "bots.json");
        }

        public BotPersistentRecord GetOrCreate(BotProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Load();
            BotPersistentRecord record = state.bots.FirstOrDefault(item =>
                string.Equals(item.botId, profile.botId, StringComparison.Ordinal));
            if (record != null)
            {
                record.processedRankTransactions ??= new List<string>();
                return record;
            }
            record = new BotPersistentRecord
            {
                botId = profile.botId,
                rankedPoints = profile.initialRankPoints,
                seasonId = DynamicBotCatalog.SeasonId,
                deckVariantId = "default"
            };
            state.bots.Add(record);
            Save();
            return record;
        }

        public RankPlayerSnapshot CaptureRankSnapshot(BotProfile profile)
        {
            BotPersistentRecord record = GetOrCreate(profile);
            return new RankPlayerSnapshot
            {
                stablePlayerId = record.botId,
                rankedPoints = RankRules.ClampPoints(record.rankedPoints),
                tier = RankRules.ResolveTier(record.rankedPoints),
                stateVersion = Math.Max(1, record.stateVersion),
                promotionShieldActive = false,
                promotionShieldTier = RankTier.Wood,
                rulesVersion = RankRules.RulesVersion,
                rulesHash = RankRules.RulesHash
            };
        }

        /// <summary>
        /// Escolhe o adversário ranqueado pelo PE persistente, sem permitir
        /// que o jogador selecione deliberadamente uma dificuldade. A busca
        /// amplia a janela somente quando não há candidato próximo e evita,
        /// quando possível, os últimos oponentes enfrentados.
        /// </summary>
        public BotProfile SelectRankedOpponent(
            int playerPoints,
            int matchmakingSeed,
            IReadOnlyCollection<string> recentBotIds = null)
        {
            Load();
            bool changed = false;
            foreach (BotProfile profile in DynamicBotCatalog.All)
            {
                if (state.bots.Any(item => string.Equals(
                        item.botId, profile.botId,
                        StringComparison.Ordinal)))
                {
                    continue;
                }

                state.bots.Add(new BotPersistentRecord
                {
                    botId = profile.botId,
                    rankedPoints = profile.initialRankPoints,
                    seasonId = DynamicBotCatalog.SeasonId,
                    deckVariantId = "default"
                });
                changed = true;
            }
            if (changed) Save();

            int clampedPlayerPoints = RankRules.ClampPoints(playerPoints);
            var recent = recentBotIds == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(recentBotIds, StringComparer.Ordinal);
            List<(BotProfile Profile, int Points)> all =
                (from profile in DynamicBotCatalog.All
                 join record in state.bots on profile.botId equals record.botId
                 select (profile, RankRules.ClampPoints(record.rankedPoints)))
                .ToList();

            foreach (int window in RankedSearchWindows)
            {
                List<(BotProfile Profile, int Points)> candidates = all
                    .Where(item => Math.Abs(
                        item.Points - clampedPlayerPoints) <= window)
                    .ToList();
                if (candidates.Count == 0) continue;

                List<(BotProfile Profile, int Points)> fresh = candidates
                    .Where(item => !recent.Contains(item.Profile.botId))
                    .ToList();
                if (fresh.Count > 0) candidates = fresh;

                return candidates
                    .OrderBy(item => Math.Abs(
                        item.Points - clampedPlayerPoints))
                    .ThenBy(item => StableMatchOrder(
                        item.Profile.botId, matchmakingSeed))
                    .ThenBy(item => item.Profile.botId,
                        StringComparer.Ordinal)
                    .First().Profile;
            }

            return DynamicBotCatalog.Find("BOT_017");
        }

        private static uint StableMatchOrder(string botId, int seed)
        {
            unchecked
            {
                uint value = (uint)seed ^ 2166136261u;
                string text = botId ?? string.Empty;
                for (int index = 0; index < text.Length; index++)
                {
                    value ^= text[index];
                    value *= 16777619u;
                }
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                return value;
            }
        }

        public bool TryCommitRankReceipt(
            BotProfile profile,
            RankChangeReceipt receipt,
            out string rejection)
        {
            rejection = string.Empty;
            if (profile == null || receipt == null ||
                string.IsNullOrWhiteSpace(receipt.transactionId))
            {
                rejection = "Recibo ranqueado do bot inválido.";
                return false;
            }

            BotPersistentRecord record = GetOrCreate(profile);
            record.processedRankTransactions ??= new List<string>();
            if (record.processedRankTransactions.Contains(receipt.transactionId))
                return true;
            if (!string.Equals(receipt.stablePlayerId, record.botId,
                    StringComparison.Ordinal) ||
                receipt.rulesVersion != RankRules.RulesVersion ||
                !string.Equals(receipt.rulesHash, RankRules.RulesHash,
                    StringComparison.Ordinal) ||
                receipt.oldPoints != record.rankedPoints ||
                receipt.stateVersionBefore != record.stateVersion ||
                !string.Equals(receipt.transactionId,
                    RankPointService.BuildTransactionId(
                        receipt.matchId, record.botId),
                    StringComparison.Ordinal))
            {
                rejection = "O estado ranqueado do bot não corresponde ao snapshot selado.";
                return false;
            }

            record.rankedPoints = receipt.newPoints;
            record.stateVersion = receipt.stateVersionAfter;
            if (receipt.outcome == RankedOutcome.Win) record.wins++;
            if (receipt.outcome == RankedOutcome.Loss ||
                receipt.outcome == RankedOutcome.ConfirmedAbandonment)
            {
                record.losses++;
            }
            record.processedRankTransactions.Add(receipt.transactionId);
            if (record.processedRankTransactions.Count > 256)
            {
                record.processedRankTransactions.RemoveRange(
                    0, record.processedRankTransactions.Count - 256);
            }
            Save();
            return true;
        }

        private void Load()
        {
            if (state != null) return;
            try
            {
                state = File.Exists(path)
                    ? JsonUtility.FromJson<BotPersistentCollection>(File.ReadAllText(path))
                    : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Bot state reset after invalid save: {exception.Message}");
            }
            state ??= new BotPersistentCollection();
            state.bots ??= new List<BotPersistentRecord>();
        }

        public void Save()
        {
            Load();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(state, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }
    }

    public static class BotRuntimeSelection
    {
        private const string BotIdKey = "ArcaneArena.Bot.SelectedId";
        private const string BotSeedKey = "ArcaneArena.Bot.DecisionSeed";
        private const string RecentBotsKey = "ArcaneArena.Bot.RecentRanked";
        private const int RecentBotLimit = 3;

        public static void Select(string botId, int seed)
        {
            PlayerPrefs.SetString(BotIdKey, botId ?? string.Empty);
            PlayerPrefs.SetString(BotSeedKey, seed.ToString());
            PlayerPrefs.Save();
        }

        public static BotProfile CurrentProfile =>
            DynamicBotCatalog.Find(PlayerPrefs.GetString(BotIdKey, "BOT_017")) ??
            DynamicBotCatalog.Find("BOT_017");

        public static int CurrentSeed => int.TryParse(
            PlayerPrefs.GetString(BotSeedKey, "173205"), out int seed)
                ? seed
                : 173205;

        public static IReadOnlyList<string> RecentRankedBotIds =>
            PlayerPrefs.GetString(RecentBotsKey, string.Empty)
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .Take(RecentBotLimit)
                .ToArray();

        public static void RememberRankedOpponent(string botId)
        {
            if (string.IsNullOrWhiteSpace(botId)) return;
            var recent = new List<string> { botId.Trim() };
            recent.AddRange(RecentRankedBotIds.Where(item =>
                !string.Equals(item, botId, StringComparison.Ordinal)));
            PlayerPrefs.SetString(
                RecentBotsKey,
                string.Join("|", recent.Take(RecentBotLimit)));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Seleciona somente respostas exatas já emitidas como legais pelo Core.
    /// Nunca interpreta texto de carta, cria alvos, paga custos ou altera estado.
    /// </summary>
    internal sealed class BotDecisionService
    {
        private BotProfile profile;
        private int seed;
        private int sequence;

        internal void Configure(BotProfile nextProfile, int decisionSeed)
        {
            profile = nextProfile ?? DynamicBotCatalog.Find("BOT_017");
            seed = decisionSeed;
            sequence = 0;
        }

        internal DuelChoice Choose(
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database,
            TacticalDecisionContext context)
        {
            DuelChoice safe = TacticalOpponentPolicy.Choose(prompt, state, database, context);
            if (safe == null || prompt == null || prompt.Choices.Count < 2)
                return safe;
            if (!CanSelectOneExactChoice(prompt)) return safe;

            BotDifficultySettings settings = DynamicBotCatalog.Settings(profile.skill);
            List<(DuelChoice choice, int score)> ranked = prompt.Choices
                .Select(choice => (choice, Score(choice, prompt, state, database, context)))
                .OrderByDescending(item => item.Item2)
                .ThenBy(item => item.choice.ChoiceIndex)
                .Take(Math.Min(prompt.Choices.Count, settings.TopK))
                .ToList();
            if (ranked.Count < 2 || settings.MaximumSuboptimalRate <= 0f)
                return ranked[0].choice;

            double roll = Next01(prompt.RequestId);
            double chance = Math.Min(settings.Epsilon, settings.MaximumSuboptimalRate);
            if (roll >= chance) return ranked[0].choice;

            double temperature = Math.Max(.05, settings.Temperature);
            int best = ranked[0].score;
            var weights = new double[ranked.Count - 1];
            double total = 0;
            for (int index = 1; index < ranked.Count; index++)
            {
                double normalized = Math.Max(-20d,
                    (ranked[index].score - best) / (25000d * temperature));
                weights[index - 1] = Math.Exp(normalized);
                total += weights[index - 1];
            }
            if (total <= 0) return ranked[0].choice;
            double pick = Next01(prompt.RequestId ^ 0x9E3779B97F4A7C15UL) * total;
            for (int index = 1; index < ranked.Count; index++)
            {
                pick -= weights[index - 1];
                if (pick <= 0) return ranked[index].choice;
            }
            return ranked[ranked.Count - 1].choice;
        }

        private int Score(
            DuelChoice choice,
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database,
            TacticalDecisionContext context)
        {
            int score = TacticalOpponentPolicy.ScoreChoice(
                choice, prompt, state, database, context);
            string label = (choice.Label ?? string.Empty).ToUpperInvariant();
            if (profile.personality == BotPersonality.Aggressive &&
                (label.Contains("ATAC") || label.Contains("INVOC"))) score += 9000;
            if (profile.personality == BotPersonality.Defensive &&
                (label.Contains("DEFESA") || label.Contains("BAIXAR"))) score += 9000;
            if (profile.personality == BotPersonality.Calculating && choice.CardCode != 0)
                score += 5000;
            if (profile.personality == BotPersonality.Casual) score -= 2500;
            return score;
        }

        private static bool CanSelectOneExactChoice(DuelPrompt prompt)
        {
            switch (prompt.Message)
            {
                case CoreMessage.SelectIdleCommand:
                case CoreMessage.SelectBattleCommand:
                case CoreMessage.SelectChain:
                case CoreMessage.SelectEffectYesNo:
                case CoreMessage.SelectYesNo:
                case CoreMessage.SelectPosition:
                case CoreMessage.SelectOption:
                case CoreMessage.SelectPlace:
                    return prompt.MinimumSelections <= 1 && prompt.MaximumSelections <= 1;
                case CoreMessage.SelectCard:
                    return prompt.MinimumSelections == 1 && prompt.MaximumSelections == 1;
                default:
                    return false;
            }
        }

        private double Next01(ulong requestId)
        {
            unchecked
            {
                ulong value = (ulong)(uint)seed ^ requestId ^
                              ((ulong)(uint)sequence++ * 0x9E3779B97F4A7C15UL);
                value ^= value >> 30;
                value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27;
                value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (value >> 11) * (1.0 / (1UL << 53));
            }
        }
    }
}
