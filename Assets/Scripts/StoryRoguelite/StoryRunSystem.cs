using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneArena.Frontend;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    [Serializable]
    public sealed class StoryRuntimeNode
    {
        public string mapId;
        public string nodeId;
        public string resolvedType;
        public RogueliteNodeState state;
        public bool resolved;

        public RogueliteNodeType NodeType =>
            StoryContentCatalog.ParseNodeType(resolvedType);
    }

    [Serializable]
    public sealed class StoryPendingTransition
    {
        public string transitionId;
        public string mapId;
        public string fromNodeId;
        public string toNodeId;
        public string edgeId;
    }

    [Serializable]
    public sealed class StoryEncounterDefinition
    {
        public string encounterId;
        public string mapId;
        public string nodeId;
        public string npcId;
        public string npcName;
        public string portraitResourcePath;
        public int act;
        public string nodeType;
        public int aiTier;
        public string botProfileId;
        public string enemyDeckId;
        public List<string> enemyMainDeck = new();
        public List<string> enemyExtraDeck = new();
        public int playerLifePoints;
        public int opponentLifePoints;
        public int playerOpeningHandSize = 5;
        public string dialogueLine;
        public bool nestedRandomEventDuel;
        public string sourceEventOperationId;
        public int eventVictoryFragments;
        public bool suppressAccountCoins;
        public bool suppressRelicDrop;
        public bool resultCommitted;
        public byte winner = byte.MaxValue;

        public RogueliteNodeType NodeType =>
            StoryContentCatalog.ParseNodeType(nodeType);
    }

    [Serializable]
    public sealed class StoryPendingReward
    {
        public string rewardId;
        public string sourceNodeId;
        public string title;
        public List<string> cardIds = new();
        public List<int> costs = new();
        public List<string> claimedCardIds = new();
        public bool allowMultiple;
        public int rerollCount;
        public int fragmentsAwarded;
        public int accountCoinsAwarded;
        public int maximumClaims = 1;
        public bool completeRandomEventOnClaim;
        public bool claimed;
    }

    [Serializable]
    public sealed class StoryAccountCoinReward
    {
        public string operationId;
        public int amount;
    }

    /// <summary>
    /// Applies the economy bonus requested for Chronicle runs without using
    /// floating-point arithmetic. The percentage is stable for each reward,
    /// varies from 40% to 45%, and integer division always floors fractions.
    /// </summary>
    public static class StoryRunRewardEconomy
    {
        public const int MinimumBoostPercent = 40;
        public const int MaximumBoostPercent = 45;

        public static int ResolveBoostPercent(
            long seed,
            string rewardId,
            string currencyKind)
        {
            int span = MaximumBoostPercent - MinimumBoostPercent + 1;
            return MinimumBoostPercent + StoryDeterminism.Index(
                span,
                seed,
                rewardId ?? string.Empty,
                currencyKind ?? string.Empty,
                "story-run-reward-boost-v1");
        }

        public static int Increase(
            int baseAmount,
            long seed,
            string rewardId,
            string currencyKind)
        {
            if (baseAmount <= 0) return baseAmount;

            int percent = ResolveBoostPercent(seed, rewardId, currencyKind);
            long increased = (long)baseAmount * (100 + percent) / 100;
            return increased > int.MaxValue ? int.MaxValue : (int)increased;
        }
    }

    [Serializable]
    public sealed class StoryChoiceOption
    {
        public string optionId;
        public string label;
        public string description;
        public int fragmentDelta;
        public int sealDelta;
        public string cardId;
        public string storyFlag;
    }

    [Serializable]
    public sealed class StoryPendingChoice
    {
        public string choiceId;
        public string sourceNodeId;
        public string title;
        public string body;
        public List<StoryChoiceOption> options = new();
        public bool resolved;
    }

    [Serializable]
    public sealed class StoryRunSave
    {
        public int schemaVersion = 3;
        public int generatorVersion = StoryProceduralMapGenerator.GeneratorVersion;
        public string runId;
        public long seed;
        public string seasonId;
        public StoryRunStatus status = StoryRunStatus.Active;
        public int actIndex;
        public int mapSequenceIndex;
        public List<string> mapSequence = new();
        public List<StoryMapRecord> generatedMaps = new();
        public string currentMapId;
        public string currentNodeId;
        public List<StoryRuntimeNode> runtimeNodes = new();
        public List<string> mainDeck = new();
        public List<string> extraDeck = new();
        public List<string> reserveCards = new();
        public List<string> artifacts = new();
        public List<StoryRelicRuntimeState> relicStates = new();
        public int relicSchemaVersion = 1;
        public List<string> defeatedNpcIds = new();
        public List<string> storyFlags = new();
        public List<string> resolvedOperationIds = new();
        public List<StoryAccountCoinReward> pendingAccountCoinRewards = new();
        public int accountCoinsEarned;
        public int fragments;
        public int seals = 3;
        public StoryPendingTransition pendingTransition;
        public StoryEncounterDefinition pendingEncounter;
        public StoryPendingReward pendingReward;
        public StoryPendingChoice pendingChoice;
        public StoryPendingRelicReward pendingRelicReward;
        public StoryPendingRelicReplacement pendingRelicReplacement;
        public StoryPendingRandomEvent pendingRandomEvent;
        public List<StoryRandomEventHistoryEntry> randomEventHistory = new();
        public StoryNextDuelModifiers nextDuelModifiers = new();
        public List<string> revealedNodeIds = new();
        public string activeDuelEncounterId;
        public long activeDuelStartedUtcTicks;
        public string profileId;
        public string playerName;
        public string equippedIconId;
        public long updatedUtcTicks;
    }

    public sealed class StoryRunPersistence
    {
        public const string FileName = "story-roguelite-run.json";
        private readonly string path;

        public StoryRunPersistence(string customPath = null)
        {
            path = string.IsNullOrWhiteSpace(customPath)
                ? Path.Combine(
                    Application.persistentDataPath,
                    "ArcaneArena",
                    FileName)
                : customPath;
        }

        public string PathOnDisk => path;
        public bool Exists => File.Exists(path);

        public StoryRunSave Load()
        {
            string source = File.Exists(path)
                ? path
                : File.Exists(path + ".bak")
                    ? path + ".bak"
                    : null;
            if (source == null) return null;
            try
            {
                StoryRunSave save = JsonUtility.FromJson<StoryRunSave>(
                    File.ReadAllText(source));
                Normalize(save);
                return save;
            }
            catch (Exception exception)
            {
                if (string.Equals(source, path, StringComparison.Ordinal) &&
                    File.Exists(path + ".bak"))
                {
                    try
                    {
                        StoryRunSave backup = JsonUtility.FromJson<StoryRunSave>(
                            File.ReadAllText(path + ".bak"));
                        Normalize(backup);
                        Debug.LogWarning(
                            "[Story Roguelite] O snapshot de segurança foi " +
                            "usado para retomar a jornada.");
                        return backup;
                    }
                    catch (Exception backupException)
                    {
                        Debug.LogWarning(
                            "[Story Roguelite] O snapshot de segurança também " +
                            "está ilegível: " + backupException.Message);
                    }
                }
                Debug.LogWarning(
                    "[Story Roguelite] O save foi preservado, mas não pôde " +
                    "ser lido: " + exception.Message);
                return null;
            }
        }

        public void Save(StoryRunSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            Normalize(save);
            save.updatedUtcTicks = DateTime.UtcNow.Ticks;
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }

            try
            {
                File.Replace(temporary, path, null);
            }
            catch (PlatformNotSupportedException)
            {
                ReplacePortable(temporary, path);
            }
            catch (IOException)
            {
                ReplacePortable(temporary, path);
            }
        }

        private static void ReplacePortable(string temporary, string path)
        {
            // Some Android file systems do not implement File.Replace. Keep a
            // recoverable copy while committing the already-written snapshot.
            string backup = path + ".bak";
            File.Copy(path, backup, true);
            File.Delete(path);
            File.Move(temporary, path);
        }

        public static void Normalize(StoryRunSave save)
        {
            if (save == null) return;
            save.mapSequence ??= new List<string>();
            save.generatedMaps ??= new List<StoryMapRecord>();
            save.runtimeNodes ??= new List<StoryRuntimeNode>();
            save.mainDeck ??= new List<string>();
            save.extraDeck ??= new List<string>();
            save.reserveCards ??= new List<string>();
            save.artifacts ??= new List<string>();
            save.relicStates ??= new List<StoryRelicRuntimeState>();
            save.defeatedNpcIds ??= new List<string>();
            save.storyFlags ??= new List<string>();
            save.resolvedOperationIds ??= new List<string>();
            save.pendingAccountCoinRewards ??=
                new List<StoryAccountCoinReward>();
            save.randomEventHistory ??=
                new List<StoryRandomEventHistoryEntry>();
            save.nextDuelModifiers ??= new StoryNextDuelModifiers();
            save.nextDuelModifiers.sourceOperationIds ??= new List<string>();
            save.revealedNodeIds ??= new List<string>();
            save.pendingAccountCoinRewards.RemoveAll(reward =>
                reward == null || string.IsNullOrWhiteSpace(
                    reward.operationId) || reward.amount <= 0);
            if (save.pendingTransition != null &&
                string.IsNullOrWhiteSpace(
                    save.pendingTransition.transitionId))
                save.pendingTransition = null;
            if (save.pendingEncounter != null)
            {
                save.pendingEncounter.enemyMainDeck ??= new List<string>();
                save.pendingEncounter.enemyExtraDeck ??= new List<string>();
                if (string.IsNullOrWhiteSpace(
                        save.pendingEncounter.encounterId))
                    save.pendingEncounter = null;
            }
            if (save.pendingReward != null)
            {
                save.pendingReward.cardIds ??= new List<string>();
                save.pendingReward.costs ??= new List<int>();
                save.pendingReward.claimedCardIds ??= new List<string>();
                if (string.IsNullOrWhiteSpace(save.pendingReward.rewardId))
                    save.pendingReward = null;
            }
            if (save.pendingChoice != null)
            {
                save.pendingChoice.options ??= new List<StoryChoiceOption>();
                if (string.IsNullOrWhiteSpace(save.pendingChoice.choiceId))
                    save.pendingChoice = null;
            }
            if (save.pendingRelicReward != null)
            {
                save.pendingRelicReward.relicIds ??= new List<string>();
                if (string.IsNullOrWhiteSpace(
                        save.pendingRelicReward.operationId))
                    save.pendingRelicReward = null;
            }
            if (save.pendingRelicReplacement != null)
            {
                save.pendingRelicReplacement.eligibleRelicIds ??=
                    new List<string>();
                if (string.IsNullOrWhiteSpace(
                        save.pendingRelicReplacement.operationId))
                    save.pendingRelicReplacement = null;
            }
            if (save.pendingRandomEvent != null)
            {
                save.pendingRandomEvent.generatedCardIds ??=
                    new List<string>();
                save.pendingRandomEvent.generatedNpcIds ??=
                    new List<string>();
                save.pendingRandomEvent.preRolledValues ??=
                    new List<double>();
                save.pendingRandomEvent.options ??=
                    new List<StoryRandomEventOption>();
                if (string.IsNullOrWhiteSpace(
                        save.pendingRandomEvent.operationId))
                    save.pendingRandomEvent = null;
            }
            StoryRelicService.MigrateLegacyArtifacts(save);
            if (string.IsNullOrWhiteSpace(save.activeDuelEncounterId))
            {
                save.activeDuelEncounterId = string.Empty;
                save.activeDuelStartedUtcTicks = 0;
            }
        }
    }

    public sealed class StoryStarterDeck
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public IReadOnlyList<string> Main { get; set; }
        public IReadOnlyList<string> Extra { get; set; }
        public string CoverCardId { get; set; }
    }

    public static class StoryStarterDeckService
    {
        public const int RequiredStarterCount = 10;

        public static IReadOnlyList<StoryStarterDeck> BuildStarters()
        {
            List<DeckRecord> roster = DeckShopCatalog.CreateOpponentRoster()
                .Where(deck => deck != null && deck.mainDeckCardIds.Count >= 20)
                .Take(RequiredStarterCount)
                .ToList();
            if (roster.Count == 0)
                return Array.Empty<StoryStarterDeck>();

            while (roster.Count < RequiredStarterCount)
                roster.Add(roster[roster.Count % Math.Max(1, roster.Count)]);

            return roster.Select((deck, index) =>
            {
                List<string> main = TakeLegalCards(deck.mainDeckCardIds, 20);
                return new StoryStarterDeck
                {
                    Id = $"story-starter-{index + 1:00}",
                    DisplayName = deck.displayName,
                    Main = main,
                    Extra = TakeLegalCards(deck.extraDeckCardIds, 15),
                    CoverCardId = main.FirstOrDefault() ?? string.Empty
                };
            }).ToArray();
        }

        public static List<string> TakeLegalCards(
            IEnumerable<string> source,
            int maximum)
        {
            var result = new List<string>();
            var copies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string raw in source ?? Array.Empty<string>())
            {
                string cardId = BanlistService.NormalizePasscode(raw);
                if (string.IsNullOrWhiteSpace(cardId)) continue;
                copies.TryGetValue(cardId, out int count);
                int limit = BanlistService.Active.MaximumCopies(cardId);
                if (count >= limit) continue;
                copies[cardId] = count + 1;
                result.Add(cardId);
                if (result.Count >= maximum) break;
            }
            return result;
        }
    }

    public static class StoryDialogueService
    {
        private static readonly string[] Openings =
        {
            "Então foi você quem atravessou o véu.",
            "Eu ouvi seus passos antes mesmo de você chegar.",
            "As cartas trouxeram você até mim.",
            "Coragem não falta em quem escolhe este caminho.",
            "O mapa muda, mas todo caminho cobra seu preço."
        };

        private static readonly string[] Challenges =
        {
            "Se veio me desafiar, mostre que seu deck merece seguir adiante.",
            "Erga seu disco de duelo. Só a vitória abrirá esta rota.",
            "Não espere piedade; aqui, cada decisão deixa uma cicatriz.",
            "Uma única jogada pode separar sua glória do esquecimento.",
            "Vamos descobrir se seus selos resistem ao meu jogo."
        };

        private static readonly string[] BossLines =
        {
            "Você chegou longe, mas este ato termina diante de mim.",
            "Nenhum caminho existe depois deste duelo — a menos que você o conquiste.",
            "Traga tudo o que aprendeu. Eu serei a prova final deste ato."
        };

        public static string Create(
            long seed,
            string encounterId,
            StoryNpcRecord npc,
            RogueliteNodeType nodeType)
        {
            string opening = Openings[StoryDeterminism.Index(
                Openings.Length, seed, encounterId, npc?.npcId, "opening")];
            string challenge = nodeType == RogueliteNodeType.Boss
                ? BossLines[StoryDeterminism.Index(
                    BossLines.Length, seed, encounterId, npc?.npcId, "boss")]
                : Challenges[StoryDeterminism.Index(
                    Challenges.Length, seed, encounterId, npc?.npcId, "challenge")];
            return $"{opening} {challenge}";
        }
    }

    public static class StoryRewardService
    {
        public static IReadOnlyList<CardCatalogEntry> EligibleEntries(
            CardCatalog catalog,
            bool spellTrapOnly = false,
            CardCategory? requiredCategory = null)
        {
            IEnumerable<CardCatalogEntry> entries = catalog?.Entries ??
                Array.Empty<CardCatalogEntry>();
            return entries
                .Where(entry => entry != null &&
                    entry.IsReadyForGameplay && entry.IsCollectible)
                .Where(entry => !spellTrapOnly ||
                    entry.Category == CardCategory.Spell ||
                    entry.Category == CardCategory.Trap)
                .Where(entry => !requiredCategory.HasValue ||
                    entry.Category == requiredCategory.Value)
                .Where(entry => !string.IsNullOrWhiteSpace(
                    entry.OfficialCardId))
                .Where(entry => BanlistService.Active.MaximumCopies(
                    entry.OfficialCardId) > 0)
                .GroupBy(entry => entry.OfficialCardId,
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(entry => entry.OfficialCardId,
                    StringComparer.Ordinal)
                .ToArray();
        }

        public static bool HasEligibleCards(
            CardCatalog catalog,
            params CardCategory[] categories)
        {
            IReadOnlyList<CardCatalogEntry> entries = EligibleEntries(catalog);
            return categories == null || categories.Length == 0
                ? entries.Count > 0
                : entries.Any(entry => categories.Contains(entry.Category));
        }

        public static List<string> PickCardChoices(
            long seed,
            string rewardId,
            CardCatalog catalog,
            int count = 3,
            bool spellTrapOnly = false,
            CardCategory? requiredCategory = null)
        {
            count = Math.Max(0, count);
            List<CardCatalogEntry> candidates = EligibleEntries(
                    catalog, spellTrapOnly, requiredCategory)
                .ToList();
            if (candidates.Count == 0)
            {
                return StoryDeterminism.Shuffle(
                        DeckShopCatalog.CollectibleCardIds,
                        seed,
                        rewardId)
                    .Take(count)
                    .ToList();
            }

            var result = new List<string>();
            for (int slot = 0; slot < count; slot++)
            {
                int roll = StoryDeterminism.Index(
                    100, seed, rewardId, slot, "rarity");
                CardRarity desired = roll < 60
                    ? CardRarity.N
                    : roll < 85
                        ? CardRarity.R
                        : roll < 96
                            ? CardRarity.SR
                            : CardRarity.UR;
                List<CardCatalogEntry> pool = candidates
                    .Where(entry => entry.Rarity == desired)
                    .Where(entry => !result.Contains(
                        entry.OfficialCardId, StringComparer.Ordinal))
                    .ToList();
                if (pool.Count == 0)
                {
                    pool = candidates.Where(entry => !result.Contains(
                            entry.OfficialCardId, StringComparer.Ordinal))
                        .ToList();
                }
                int index = StoryDeterminism.Index(
                    pool.Count, seed, rewardId, slot, "card");
                if (index >= 0) result.Add(pool[index].OfficialCardId);
            }
            return result;
        }
    }

    public sealed partial class StoryRunManager
    {
        public static readonly StoryRuleProfile Rules = new();
        private readonly StoryRunPersistence persistence;
        private readonly CardCatalog catalog;
        private readonly StoryEncounterLpProfile lifePoints;

        public StoryRunManager(
            CardCatalog cardCatalog,
            StoryRunPersistence runPersistence = null)
        {
            catalog = cardCatalog;
            persistence = runPersistence ?? new StoryRunPersistence();
            lifePoints = Resources.Load<StoryEncounterLpProfile>(
                StoryEncounterLpProfile.ResourcePath);
            Save = persistence.Load();
            if (Save != null && Save.status == StoryRunStatus.Active)
            {
                ResolveInterruptedDuel();
                if (Save.status == StoryRunStatus.Active)
                {
                    EnsureProceduralMaps();
                    EnsureCurrentMapLayout();
                    ResumePendingTransition();
                }
            }
        }

        public StoryRunSave Save { get; private set; }
        public StoryMapRecord CurrentMap => Save == null
            ? null
            : Save.generatedMaps?.FirstOrDefault(map => map != null &&
                  string.Equals(map.mapId, Save.currentMapId,
                      StringComparison.Ordinal)) ??
              StoryContentCatalog.ResolveMap(Save.currentMapId);
        public StoryMapNodeRecord CurrentNode => CurrentMap?.Node(
            Save?.currentNodeId);
        public bool HasActiveRun => Save?.status == StoryRunStatus.Active;
        public int MaxSeals => StoryRelicService.MaxSeals(Save);
        public IReadOnlyList<StoryAccountCoinReward> PendingAccountCoinRewards =>
            Save?.pendingAccountCoinRewards?.ToArray() ??
            Array.Empty<StoryAccountCoinReward>();
        public string SelectedNodeId { get; private set; }
        public int PlayerStartingLifePoints => StoryRelicService
            .PlayerStartingLifePoints(
                Save,
                lifePoints != null
                    ? lifePoints.playerLifePoints
                    : Rules.playerStartingLifePoints,
                0);

        public int ResolveEnemyLifePoints(RogueliteNodeType type, int act)
        {
            int baseLifePoints = lifePoints != null
                ? lifePoints.ResolveEnemyLifePoints(type, act)
                : StoryEncounterLpProfile.ResolveOfficialEnemyLifePoints(
                    type, act);
            return StoryRelicService.OpponentStartingLifePoints(
                Save, type, baseLifePoints, 0);
        }

        public StoryRunSave StartNew(
            long seed,
            IReadOnlyList<string> main,
            IReadOnlyList<string> extra,
            string profileId,
            string playerName,
            string equippedIconId)
        {
            StoryDeckValidationResult validation = Rules.Validate(
                main, extra, true);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Summary);

            List<StoryMapRecord> generatedMaps =
                StoryProceduralMapGenerator.GenerateRun(seed);
            List<string> mapOrder = generatedMaps.Select(map => map.mapId)
                .ToList();

            Save = new StoryRunSave
            {
                runId = "run-" + Guid.NewGuid().ToString("N"),
                seed = seed,
                seasonId = StoryContentCatalog.DefaultSeasonId,
                status = StoryRunStatus.Active,
                actIndex = 1,
                mapSequenceIndex = 0,
                mapSequence = mapOrder,
                generatedMaps = generatedMaps,
                mainDeck = main.ToList(),
                extraDeck = extra?.ToList() ?? new List<string>(),
                fragments = 0,
                seals = Rules.sealsAtRunStart,
                profileId = profileId ?? string.Empty,
                playerName = string.IsNullOrWhiteSpace(playerName)
                    ? "DUELISTA"
                    : playerName.Trim(),
                equippedIconId = ProfileIconCatalog.ResolveId(equippedIconId)
            };
            InitializeMap(0);
            Persist();
            return Save;
        }

        public bool SelectNode(string nodeId, out string rejection)
        {
            rejection = string.Empty;
            SelectedNodeId = null;
            if (!HasActiveRun || CurrentMap == null)
            {
                rejection = "Nenhuma jornada ativa foi encontrada.";
                return false;
            }
            StoryRuntimeNode runtime = RuntimeNode(nodeId);
            if (runtime == null || runtime.state != RogueliteNodeState.Available)
            {
                rejection = "Este destino ainda não está disponível.";
                return false;
            }
            if (CurrentMap.Edge(Save.currentNodeId, nodeId) == null)
            {
                rejection = "Não existe uma rota direta até esse destino.";
                return false;
            }
            SelectedNodeId = nodeId;
            return true;
        }

        public bool CommitSelectedTransition(out string rejection)
        {
            rejection = string.Empty;
            if (string.IsNullOrWhiteSpace(SelectedNodeId))
            {
                rejection = "Selecione um destino antes de confirmar.";
                return false;
            }
            StoryMapEdgeRecord edge = CurrentMap.Edge(
                Save.currentNodeId, SelectedNodeId);
            if (edge == null)
            {
                rejection = "A rota selecionada não é mais válida.";
                return false;
            }
            Save.pendingTransition = new StoryPendingTransition
            {
                transitionId = $"{Save.runId}:{Save.currentMapId}:{edge.edgeId}",
                mapId = Save.currentMapId,
                fromNodeId = Save.currentNodeId,
                toNodeId = SelectedNodeId,
                edgeId = edge.edgeId
            };
            Persist();
            return true;
        }

        public void FinalizeTransition()
        {
            StoryPendingTransition transition = Save?.pendingTransition;
            if (transition == null) return;
            if (!string.Equals(transition.mapId, Save.currentMapId,
                    StringComparison.Ordinal))
            {
                Save.pendingTransition = null;
                Persist();
                return;
            }

            StoryRuntimeNode previous = RuntimeNode(transition.fromNodeId);
            if (previous != null && previous.resolved)
                previous.state = RogueliteNodeState.Completed;
            StoryRuntimeNode destination = RuntimeNode(transition.toNodeId);
            if (destination == null)
                throw new InvalidOperationException(
                    "O destino persistido não existe no mapa atual.");
            Save.currentNodeId = destination.nodeId;
            destination.state = RogueliteNodeState.Current;
            foreach (StoryMapEdgeRecord route in CurrentMap.edges.Where(route =>
                         string.Equals(route.fromNodeId,
                             transition.fromNodeId,
                             StringComparison.Ordinal)))
            {
                StoryRuntimeNode abandoned = RuntimeNode(route.toNodeId);
                if (abandoned != null && abandoned != destination &&
                    abandoned.state == RogueliteNodeState.Available)
                    abandoned.state = RogueliteNodeState.BlockedByChoice;
            }
            foreach (StoryRuntimeNode node in NodesForCurrentMap())
            {
                if (node != destination && node.state == RogueliteNodeState.Selected)
                    node.state = RogueliteNodeState.Available;
            }
            Save.pendingTransition = null;
            SelectedNodeId = null;
            Persist();
            PrepareCurrentNode();
        }

        public void ResumePendingTransition()
        {
            if (Save?.pendingTransition == null) return;
            FinalizeTransition();
        }

        public StoryEncounterDefinition PrepareCurrentNode()
        {
            if (!HasActiveRun || CurrentNode == null) return null;
            StoryRuntimeNode runtime = RuntimeNode(CurrentNode.nodeId);
            if (runtime == null || runtime.resolved) return null;

            // A resolução pendente pertence ao nó atual. Em especial, após
            // uma vitória, recriar o encontro antes de entregar a recompensa
            // prendia o jogador em uma sequência infinita de adversários.
            if (Save.pendingReward != null || Save.pendingChoice != null ||
                Save.pendingRelicReward != null ||
                Save.pendingRelicReplacement != null ||
                Save.pendingRandomEvent != null)
                return null;

            RogueliteNodeType type = runtime.NodeType;
            if (IsCombat(type)) return EnsureEncounter(type);

            switch (type)
            {
                case RogueliteNodeType.CardPack:
                    CreateReward("PACOTE DE CARTAS",
                        StoryRelicService.CardPackChoiceCount(Save));
                    break;
                case RogueliteNodeType.SpellRuins:
                    CreateReward(
                        "RUÍNAS DE MAGIAS E ARMADILHAS",
                        StoryRelicService.SpellRuinsChoiceCount(Save),
                        false,
                        true);
                    break;
                case RogueliteNodeType.TreasureVault:
                    CreateReward("COFRE DO TESOURO", 3);
                    int vaultFragments = StoryRelicService.VaultFragmentBonus(
                        Save);
                    GrantRunFragments(
                        vaultFragments,
                        $"{Save.currentMapId}:{Save.currentNodeId}:vault");
                    break;
                case RogueliteNodeType.CardMerchant:
                    CreateReward("MERCADOR DE CARTAS",
                        StoryRelicService.MerchantChoiceCount(Save), true);
                    break;
                case RogueliteNodeType.RelicShrine:
                    Save.pendingRelicReward =
                        StoryRelicRewardResolver.ResolveShrine(Save);
                    break;
                case RogueliteNodeType.DeckWorkshop:
                case RogueliteNodeType.DeckForge:
                    CreateChoice(
                        "OFICINA DO DECK",
                        "Organize a reserva antes da próxima batalha.",
                        new StoryChoiceOption
                        {
                            optionId = "forge-fragments",
                            label = "RECICLAR SOBRAS",
                            description = "Receba 2 fragmentos.",
                            fragmentDelta = 2
                        },
                        new StoryChoiceOption
                        {
                            optionId = "forge-pass",
                            label = "MANTER O DECK",
                            description = "Continue sem alterações."
                        });
                    break;
                case RogueliteNodeType.HealingSpring:
                case RogueliteNodeType.RestCamp:
                    if (type == RogueliteNodeType.HealingSpring &&
                        StoryRelicService.HealingSpringBlocked(Save))
                    {
                        CreateChoice(
                            "PAUSA SEGURA",
                            "A Armadura do Soldado do Lustro Negro impede " +
                            "a Fonte de restaurar Selos.",
                            new StoryChoiceOption
                            {
                                optionId = "rest-fragments",
                                label = "PROCURAR FRAGMENTOS",
                                description = "Receba 2 fragmentos.",
                                fragmentDelta = 2
                            });
                    }
                    else
                    {
                        CreateChoice(
                            "PAUSA SEGURA",
                            "A rota oferece um breve momento de recuperação.",
                            new StoryChoiceOption
                            {
                                optionId = "rest-seal",
                                label = "RESTAURAR SELO",
                                description = "Recupere 1 selo.",
                                sealDelta = 1
                            },
                            new StoryChoiceOption
                            {
                                optionId = "rest-fragments",
                                label = "PROCURAR FRAGMENTOS",
                                description = "Receba 2 fragmentos.",
                                fragmentDelta = 2
                            });
                    }
                    break;
                case RogueliteNodeType.ForbiddenAltar:
                    CreateChoice(
                        "ALTAR PROIBIDO",
                        "O altar oferece poder em troca de segurança.",
                        new StoryChoiceOption
                        {
                            optionId = "altar-risk",
                            label = "OFERECER UM SELO",
                            description = "Perca 1 selo e receba 6 fragmentos.",
                            sealDelta = -1,
                            fragmentDelta = 6,
                            storyFlag = "altar:accepted"
                        },
                        new StoryChoiceOption
                        {
                            optionId = "altar-refuse",
                            label = "RECUSAR",
                            description = "Preserve seus recursos."
                        });
                    break;
                case RogueliteNodeType.MysteryEvent:
                    Save.pendingRandomEvent = StoryRandomEventService.Resolve(
                        Save, CurrentMap, catalog);
                    break;
                default:
                    CreateChoice("EVENTO MISTERIOSO",
                        "A passagem se fecha sem produzir efeito.",
                        new StoryChoiceOption
                        {
                            optionId = "mystery-leave",
                            label = "CONTINUAR",
                            description = "Retorne ao mapa."
                        });
                    break;
            }
            Persist();
            return null;
        }

        public bool IsEncounterReady(StoryEncounterDefinition encounter)
        {
            return HasActiveRun && encounter != null &&
                !encounter.resultCommitted &&
                !string.IsNullOrWhiteSpace(encounter.encounterId) &&
                !string.IsNullOrWhiteSpace(encounter.mapId) &&
                !string.IsNullOrWhiteSpace(encounter.nodeId) &&
                !string.IsNullOrWhiteSpace(encounter.npcId) &&
                !string.IsNullOrWhiteSpace(encounter.npcName) &&
                !string.IsNullOrWhiteSpace(encounter.nodeType) &&
                !string.IsNullOrWhiteSpace(encounter.botProfileId) &&
                !string.IsNullOrWhiteSpace(encounter.enemyDeckId) &&
                string.Equals(encounter.mapId, Save.currentMapId,
                    StringComparison.Ordinal) &&
                string.Equals(encounter.nodeId, Save.currentNodeId,
                    StringComparison.Ordinal) &&
                encounter.act == Save.actIndex &&
                encounter.playerLifePoints > 0 &&
                encounter.opponentLifePoints > 0 &&
                encounter.enemyMainDeck != null &&
                encounter.enemyMainDeck.Count >= Rules.minimumMainDeckSize;
        }

        public bool RepairInvalidPendingEncounter()
        {
            if (Save == null) return false;
            StoryEncounterDefinition encounter = Save.pendingEncounter;
            if (encounter != null && IsEncounterReady(encounter))
                return false;

            bool hasInvalidEncounter = encounter != null;
            bool hasOrphanedDuelTimestamp =
                string.IsNullOrWhiteSpace(Save.activeDuelEncounterId) &&
                Save.activeDuelStartedUtcTicks != 0;
            if (!hasInvalidEncounter && !hasOrphanedDuelTimestamp)
                return false;

            if (!string.IsNullOrWhiteSpace(Save.activeDuelEncounterId))
            {
                ResolveInterruptedDuel();
                return true;
            }

            Save.pendingEncounter = null;
            ClearActiveDuelMarker();
            Persist();
            return true;
        }

        public void ResolveChoice(string optionId)
        {
            StoryPendingChoice choice = Save?.pendingChoice;
            if (choice == null || choice.resolved) return;
            string operationId = choice.choiceId + ":" + optionId;
            if (AlreadyResolved(operationId)) return;
            StoryChoiceOption selected = choice.options.FirstOrDefault(
                option => string.Equals(option.optionId, optionId,
                    StringComparison.Ordinal));
            if (selected == null) return;
            if (Save.seals + selected.sealDelta < 1)
                return;
            Save.fragments = Math.Max(0, Save.fragments + selected.fragmentDelta);
            Save.seals = Math.Min(MaxSeals,
                Math.Max(0, Save.seals + selected.sealDelta));
            if (!string.IsNullOrWhiteSpace(selected.cardId))
                Save.reserveCards.Add(selected.cardId);
            if (!string.IsNullOrWhiteSpace(selected.storyFlag))
            {
                if (selected.storyFlag.StartsWith("artifact:",
                        StringComparison.Ordinal))
                {
                    string artifactId = selected.storyFlag.Substring(9);
                    bool added = !HasArtifact(artifactId);
                    AddUnique(Save.artifacts, artifactId);
                    if (added && string.Equals(artifactId,
                            "reinforced-seal", StringComparison.Ordinal))
                        Save.seals = Math.Min(MaxSeals, Save.seals + 1);
                }
                else AddUnique(Save.storyFlags, selected.storyFlag);
            }
            MarkResolved(operationId);
            choice.resolved = true;
            Save.pendingChoice = null;
            CompleteCurrentNode();
        }

        public void ClaimReward(string cardId)
        {
            StoryPendingReward reward = Save?.pendingReward;
            if (reward == null || reward.claimed ||
                !reward.cardIds.Contains(cardId, StringComparer.Ordinal))
                return;
            if (reward.claimedCardIds.Contains(cardId, StringComparer.Ordinal))
                return;
            string operationId = reward.rewardId + ":" + cardId;
            if (AlreadyResolved(operationId)) return;
            int cardIndex = reward.cardIds.FindIndex(id => string.Equals(
                id, cardId, StringComparison.Ordinal));
            int cost = cardIndex >= 0 && cardIndex < reward.costs.Count
                ? Math.Max(0, reward.costs[cardIndex])
                : 0;
            if (Save.fragments < cost) return;
            Save.fragments -= cost;
            Save.reserveCards.Add(cardId);
            MarkResolved(operationId);
            reward.claimedCardIds.Add(cardId);
            int maximumClaims = Math.Max(1, reward.maximumClaims);
            if (reward.allowMultiple ||
                reward.claimedCardIds.Count < maximumClaims)
            {
                Persist();
                return;
            }
            reward.claimed = true;
            Save.pendingReward = null;
            if (reward.completeRandomEventOnClaim)
                CompletePendingRandomEvent("Carta recebida após o duelo.");
            else
                CompleteCurrentNode();
        }

        public void FinishPendingReward()
        {
            StoryPendingReward reward = Save?.pendingReward;
            if (reward == null || !reward.allowMultiple) return;
            reward.claimed = true;
            Save.pendingReward = null;
            CompleteCurrentNode();
        }

        public bool RerollMerchant(out string rejection)
        {
            rejection = string.Empty;
            StoryPendingReward reward = Save?.pendingReward;
            if (reward == null || !reward.allowMultiple)
            {
                rejection = "Nenhum mercador está aberto.";
                return false;
            }
            int cost = MerchantRerollCost(reward);
            if (Save.fragments < cost)
            {
                rejection = $"São necessários {cost} Fragmentos Arcanos.";
                return false;
            }
            string operationId = reward.rewardId + ":reroll:" +
                                 reward.rerollCount;
            if (AlreadyResolved(operationId)) return true;
            Save.fragments -= cost;
            if (cost == 0 && HasArtifact("fortune-echo"))
                AddUnique(Save.storyFlags,
                    $"relic-used:fortune-echo:act-{Save.actIndex}");
            reward.rerollCount++;
            reward.cardIds = StoryRewardService.PickCardChoices(
                Save.seed,
                reward.rewardId + ":reroll:" + reward.rerollCount,
                catalog,
                5);
            reward.costs = reward.cardIds.Select((_, index) =>
                    MerchantCardCost(4 + index * 2))
                .ToList();
            reward.claimedCardIds.Clear();
            MarkResolved(operationId);
            Persist();
            return true;
        }

        public void CommitEncounterResult(string encounterId, byte winner)
        {
            StoryEncounterDefinition encounter = Save?.pendingEncounter;
            if (encounter == null || encounter.resultCommitted ||
                !string.Equals(encounter.encounterId, encounterId,
                    StringComparison.Ordinal))
                return;
            string operationId = "duel-result:" + encounterId;
            ClearActiveDuelMarker();
            if (AlreadyResolved(operationId))
            {
                Persist();
                return;
            }
            encounter.resultCommitted = true;
            encounter.winner = winner;
            MarkResolved(operationId);
            if (winner == 0) HandleExpandedEncounterVictory(encounter);
            else HandleExpandedEncounterDefeat(encounter);
        }

        public bool MarkDuelStarted(
            string encounterId,
            out string rejection)
        {
            rejection = string.Empty;
            StoryEncounterDefinition encounter = Save?.pendingEncounter;
            if (string.IsNullOrWhiteSpace(encounterId) ||
                !IsEncounterReady(encounter) ||
                !string.Equals(encounter.encounterId, encounterId,
                    StringComparison.Ordinal))
            {
                rejection = "O encontro selecionado não está mais disponível.";
                return false;
            }

            Save.activeDuelEncounterId = encounterId;
            Save.activeDuelStartedUtcTicks = DateTime.UtcNow.Ticks;
            Persist();
            return true;
        }

        public bool ForfeitActiveDuel(string encounterId)
        {
            if (!HasActiveRun || string.IsNullOrWhiteSpace(encounterId) ||
                !string.Equals(Save.activeDuelEncounterId, encounterId,
                    StringComparison.Ordinal))
                return false;

            if (Save.pendingEncounter?.NodeType == RogueliteNodeType.Boss)
                CommitBossExitFailure(encounterId);
            else
                CommitEncounterResult(encounterId, 1);
            return true;
        }

        public void AcknowledgeAccountCoinReward(string operationId)
        {
            if (Save?.pendingAccountCoinRewards == null ||
                string.IsNullOrWhiteSpace(operationId)) return;
            int removed = Save.pendingAccountCoinRewards.RemoveAll(reward =>
                reward != null && string.Equals(reward.operationId,
                    operationId, StringComparison.Ordinal));
            if (removed > 0) Persist();
        }

        public bool HasArtifact(string artifactId) =>
            StoryRelicService.Has(Save, artifactId);

        public int MerchantRerollCost(StoryPendingReward reward)
        {
            if (reward == null) return 0;
            bool free = HasArtifact("fortune-echo") &&
                !Save.storyFlags.Contains(
                    $"relic-used:fortune-echo:act-{Save.actIndex}",
                    StringComparer.Ordinal);
            return free ? 0 : 2 + reward.rerollCount * 2;
        }

        public StoryRuntimeNode RuntimeNode(string nodeId) => Save?.runtimeNodes
            .FirstOrDefault(node => string.Equals(node.mapId, Save.currentMapId,
                    StringComparison.Ordinal) &&
                string.Equals(node.nodeId, nodeId, StringComparison.Ordinal));

        public IReadOnlyList<StoryRuntimeNode> NodesForCurrentMap() =>
            Save?.runtimeNodes.Where(node => string.Equals(
                    node.mapId, Save.currentMapId, StringComparison.Ordinal))
                .ToArray() ?? Array.Empty<StoryRuntimeNode>();

        public void Abandon()
        {
            if (Save == null) return;
            Save.status = StoryRunStatus.Abandoned;
            Persist();
        }

        public bool MoveReserveCardToMain(string cardId, out string rejection)
        {
            rejection = string.Empty;
            if (!HasActiveRun || !Save.reserveCards.Contains(
                    cardId, StringComparer.Ordinal))
            {
                rejection = "A carta não está na reserva da jornada.";
                return false;
            }
            if (Save.mainDeck.Count >= Rules.maximumMainDeckSize)
            {
                rejection = $"O Deck Principal já possui {Rules.maximumMainDeckSize} cartas.";
                return false;
            }
            int copies = Save.mainDeck.Concat(Save.extraDeck).Count(id =>
                string.Equals(id, cardId, StringComparison.Ordinal));
            int limit = BanlistService.Active.MaximumCopies(cardId);
            if (copies >= limit)
            {
                rejection = $"O limite de {limit} cópias desta carta foi atingido.";
                return false;
            }
            Save.reserveCards.Remove(cardId);
            Save.mainDeck.Add(cardId);
            Persist();
            return true;
        }

        public bool MoveMainCardToReserve(string cardId, out string rejection)
        {
            rejection = string.Empty;
            if (!HasActiveRun || Save.mainDeck.Count <= Rules.minimumMainDeckSize)
            {
                rejection = $"O Deck Principal deve manter ao menos {Rules.minimumMainDeckSize} cartas.";
                return false;
            }
            int index = Save.mainDeck.FindIndex(id => string.Equals(
                id, cardId, StringComparison.Ordinal));
            if (index < 0)
            {
                rejection = "A carta não está no Deck Principal.";
                return false;
            }
            Save.mainDeck.RemoveAt(index);
            Save.reserveCards.Add(cardId);
            Persist();
            return true;
        }

        private void InitializeMap(int index)
        {
            Save.mapSequenceIndex = index;
            Save.actIndex = index + 1;
            Save.currentMapId = Save.mapSequence[index];
            StoryMapRecord map = CurrentMap ?? throw new InvalidOperationException(
                "O mapa selecionado não existe no catálogo.");
            Save.currentNodeId = map.startNodeId;
            foreach (StoryMapNodeRecord node in map.nodes)
            {
                if (RuntimeNodeFor(map.mapId, node.nodeId) != null) continue;
                Save.runtimeNodes.Add(new StoryRuntimeNode
                {
                    mapId = map.mapId,
                    nodeId = node.nodeId,
                    resolvedType = ResolveNodeType(map, node),
                    state = string.Equals(node.nodeId, map.startNodeId,
                        StringComparison.Ordinal)
                        ? RogueliteNodeState.Completed
                        : RogueliteNodeState.Locked,
                    resolved = string.Equals(node.nodeId, map.startNodeId,
                        StringComparison.Ordinal)
                });
            }
            UnlockOutgoing(map.startNodeId);
        }

        private string ResolveNodeType(
            StoryMapRecord map,
            StoryMapNodeRecord node)
        {
            if (node.NodeType != RogueliteNodeType.Mystery)
                return node.NodeType.ToString();
            RogueliteNodeType[] choices =
            {
                RogueliteNodeType.CardPack,
                RogueliteNodeType.CardMerchant,
                RogueliteNodeType.TreasureVault,
                RogueliteNodeType.RelicShrine,
                RogueliteNodeType.ForbiddenAltar
            };
            return choices[StoryDeterminism.Index(
                choices.Length, Save.seed, map.mapId, node.nodeId, "mystery")]
                .ToString();
        }

        private StoryEncounterDefinition EnsureEncounter(
            RogueliteNodeType type)
        {
            if (Save.pendingEncounter != null)
            {
                if (IsEncounterReady(Save.pendingEncounter))
                    return Save.pendingEncounter;
                Save.pendingEncounter = null;
                ClearActiveDuelMarker();
            }
            string encounterPrefix =
                $"{Save.runId}:{Save.currentMapId}:{Save.currentNodeId}:duel";
            int attempt = Save.resolvedOperationIds.Count(id =>
                id.StartsWith("duel-result:" + encounterPrefix,
                    StringComparison.Ordinal)) + 1;
            string encounterId = $"{encounterPrefix}:attempt-{attempt}";
            EncounterRole requiredRole = type == RogueliteNodeType.Boss
                ? EncounterRole.Boss
                : type == RogueliteNodeType.EliteDuel ||
                  type == RogueliteNodeType.FinalDuelArena
                    ? EncounterRole.Elite
                    : EncounterRole.Normal;
            IReadOnlyList<StoryNpcRecord> configuredNpcs =
                StoryContentCatalog.RuntimeNpcs();
            List<StoryNpcRecord> candidates = configuredNpcs
                .Where(npc => npc.enabled)
                .Where(HasPdfNpcName)
                .Where(npc => Save.actIndex >= npc.firstAct &&
                              Save.actIndex <= npc.lastAct)
                .Where(npc => (StoryContentCatalog.ParseRole(npc.role) &
                               requiredRole) != 0)
                .Where(npc => npc.recurring || !Save.defeatedNpcIds.Contains(
                    npc.npcId, StringComparer.Ordinal))
                .ToList();
            if (candidates.Count == 0)
                candidates = configuredNpcs
                    .Where(npc => npc.enabled)
                    .Where(HasPdfNpcName)
                    .Where(npc => (StoryContentCatalog.ParseRole(npc.role) &
                                   requiredRole) != 0)
                    .ToList();
            if (candidates.Count == 0)
                candidates = configuredNpcs
                    .Where(npc => npc.enabled)
                    .Where(HasPdfNpcName)
                    .ToList();
            StoryNpcRecord npc = candidates[StoryDeterminism.Index(
                candidates.Count, Save.seed, encounterId, "npc")];
            int tierSpan = Math.Max(1, npc.aiTierMax - npc.aiTierMin + 1);
            int tier = npc.aiTierMin + StoryDeterminism.Index(
                tierSpan, Save.seed, encounterId, "tier");
            BotProfile bot = DynamicBotCatalog.All
                .Where(profile => (int)profile.skill == tier)
                .OrderBy(profile => profile.botId, StringComparer.Ordinal)
                .FirstOrDefault() ?? DynamicBotCatalog.All.First();
            List<DeckRecord> decks = DeckShopCatalog.CreateOpponentRoster()
                .Where(deck => deck != null && deck.mainDeckCardIds.Count >= 20)
                .ToList();
            DeckRecord deck = string.IsNullOrWhiteSpace(npc.fixedDeckId)
                ? null
                : decks.FirstOrDefault(candidate => string.Equals(
                    candidate.deckId, npc.fixedDeckId,
                    StringComparison.Ordinal));
            deck ??= decks[StoryDeterminism.Index(
                decks.Count, Save.seed, encounterId, "deck")];
            bool consumeNextDuelModifiers = type != RogueliteNodeType.Boss &&
                Save.nextDuelModifiers != null &&
                Save.nextDuelModifiers.HasAny;
            int playerDelta = consumeNextDuelModifiers
                ? Save.nextDuelModifiers.playerStartingLpDelta
                : 0;
            int opponentDelta = consumeNextDuelModifiers
                ? Save.nextDuelModifiers.opponentStartingLpDelta
                : 0;
            int openingHandDelta = consumeNextDuelModifiers
                ? Save.nextDuelModifiers.openingHandDelta
                : 0;
            int playerBaseLifePoints = lifePoints != null
                ? lifePoints.playerLifePoints
                : Rules.playerStartingLifePoints;
            int opponentBaseLifePoints = lifePoints != null
                ? lifePoints.ResolveEnemyLifePoints(type, Save.actIndex)
                : StoryEncounterLpProfile.ResolveOfficialEnemyLifePoints(
                    type, Save.actIndex);
            Save.pendingEncounter = new StoryEncounterDefinition
            {
                encounterId = encounterId,
                mapId = Save.currentMapId,
                nodeId = Save.currentNodeId,
                npcId = npc.npcId,
                npcName = npc.displayName,
                portraitResourcePath = npc.portraitResourcePath,
                act = Save.actIndex,
                nodeType = type.ToString(),
                aiTier = tier,
                botProfileId = bot.botId,
                enemyDeckId = deck.deckId,
                enemyMainDeck = StoryStarterDeckService.TakeLegalCards(
                    deck.mainDeckCardIds, 30),
                enemyExtraDeck = StoryStarterDeckService.TakeLegalCards(
                    deck.extraDeckCardIds, 15),
                playerLifePoints = StoryRelicService.PlayerStartingLifePoints(
                    Save, playerBaseLifePoints, playerDelta),
                opponentLifePoints = StoryRelicService
                    .OpponentStartingLifePoints(
                        Save, type, opponentBaseLifePoints, opponentDelta),
                playerOpeningHandSize = Mathf.Clamp(
                    5 + openingHandDelta, 1, 10),
                dialogueLine = StoryDialogueService.Create(
                    Save.seed, encounterId, npc, type)
            };
            if (consumeNextDuelModifiers)
                Save.nextDuelModifiers.Clear();
            Persist();
            return Save.pendingEncounter;
        }

        private void CreateReward(
            string title,
            int count,
            bool merchant = false,
            bool spellTrapOnly = false,
            int fragmentsAwarded = 0,
            int accountCoinsAwarded = 0)
        {
            string rewardId =
                $"{Save.runId}:{Save.currentMapId}:{Save.currentNodeId}:reward";
            List<string> cards = StoryRewardService.PickCardChoices(
                Save.seed, rewardId, catalog, count, spellTrapOnly);
            Save.pendingReward = new StoryPendingReward
            {
                rewardId = rewardId,
                sourceNodeId = Save.currentNodeId,
                title = title,
                cardIds = cards,
                costs = cards.Select((_, index) => merchant
                        ? MerchantCardCost(4 + index * 2)
                        : 0)
                    .ToList(),
                allowMultiple = merchant,
                fragmentsAwarded = fragmentsAwarded,
                accountCoinsAwarded = accountCoinsAwarded
            };
        }

        private void CreateChoice(
            string title,
            string body,
            params StoryChoiceOption[] options)
        {
            string choiceId =
                $"{Save.runId}:{Save.currentMapId}:{Save.currentNodeId}:choice";
            List<StoryChoiceOption> resolvedOptions = options?.ToList() ??
                new List<StoryChoiceOption>();
            foreach (StoryChoiceOption option in resolvedOptions)
            {
                if (option == null || option.fragmentDelta <= 0) continue;
                int baseAmount = option.fragmentDelta;
                option.fragmentDelta = IncreaseRunFragmentReward(
                    baseAmount,
                    $"{choiceId}:{option.optionId}");
                option.description = ReplaceFragmentRewardAmount(
                    option.description,
                    baseAmount,
                    option.fragmentDelta);
            }

            Save.pendingChoice = new StoryPendingChoice
            {
                choiceId = choiceId,
                sourceNodeId = Save.currentNodeId,
                title = title,
                body = body,
                options = resolvedOptions
            };
        }

        private static string ReplaceFragmentRewardAmount(
            string description,
            int baseAmount,
            int increasedAmount)
        {
            if (string.IsNullOrWhiteSpace(description) ||
                baseAmount == increasedAmount)
                return description;

            return description
                .Replace($"{baseAmount} fragmentos",
                    $"{increasedAmount} fragmentos")
                .Replace($"{baseAmount} Fragmentos",
                    $"{increasedAmount} Fragmentos");
        }

        private void CreateRelicChoice()
        {
            List<StoryArtifactDefinition> available = StoryDeterminism.Shuffle(
                    StoryArtifactCatalog.All.Where(definition =>
                        !HasArtifact(definition.artifactId)),
                    Save.seed,
                    Save.currentMapId,
                    Save.currentNodeId,
                    "relic-choice")
                .Take(3)
                .ToList();
            if (available.Count == 0)
            {
                CreateChoice(
                    "SANTUÁRIO DE RELÍQUIAS",
                    "Todas as relíquias desta temporada já acompanham a run.",
                    new StoryChoiceOption
                    {
                        optionId = "relic-complete",
                        label = "RECOLHER FRAGMENTOS",
                        description = "Receba 4 Fragmentos Arcanos.",
                        fragmentDelta = 4
                    });
                return;
            }

            CreateChoice(
                "SANTUÁRIO DE RELÍQUIAS",
                "Relíquias são melhorias passivas que duram até o fim desta " +
                "run. Escolha uma; ela não é uma carta e não ocupa espaço no deck.",
                available.Select(definition => new StoryChoiceOption
                {
                    optionId = "relic-" + definition.artifactId,
                    label = definition.displayName.ToUpperInvariant(),
                    description = definition.description,
                    storyFlag = "artifact:" + definition.artifactId
                }).ToArray());
        }

        private int MerchantCardCost(int baseCost)
        {
            if (!HasArtifact("merchant-pouch")) return Math.Max(0, baseCost);
            return Math.Max(1, Mathf.FloorToInt(baseCost * 0.90f));
        }

        private int AccountCoinReward(StoryEncounterDefinition encounter)
        {
            return CalculateAccountCoinReward(
                Save.seed, encounter.encounterId, encounter.NodeType);
        }

        private int IncreaseRunFragmentReward(
            int baseAmount,
            string rewardId)
        {
            return StoryRunRewardEconomy.Increase(
                baseAmount,
                Save.seed,
                rewardId,
                "run-fragments");
        }

        private int GrantRunFragments(int baseAmount, string rewardId)
        {
            int amount = IncreaseRunFragmentReward(baseAmount, rewardId);
            if (amount > 0)
                Save.fragments = checked(Save.fragments + amount);
            return amount;
        }

        public static int CalculateAccountCoinReward(
            long seed,
            string encounterId,
            RogueliteNodeType nodeType)
        {
            bool hard = IsHardEncounter(nodeType);
            int minimum = hard ? 10 : 1;
            int span = hard ? 16 : 5;
            int baseReward = minimum + StoryDeterminism.Index(
                span, seed, encounterId, "account-coins");
            return StoryRunRewardEconomy.Increase(
                baseReward,
                seed,
                encounterId,
                "account-coins");
        }

        private void QueueAccountCoinReward(
            StoryEncounterDefinition encounter,
            int amount)
        {
            if (amount <= 0) return;
            string operationId =
                $"{encounter.encounterId}:account-coins:v1";
            if (Save.pendingAccountCoinRewards.Any(reward => reward != null &&
                    string.Equals(reward.operationId, operationId,
                        StringComparison.Ordinal)))
                return;
            Save.pendingAccountCoinRewards.Add(new StoryAccountCoinReward
            {
                operationId = operationId,
                amount = amount
            });
            Save.accountCoinsEarned = checked(
                Save.accountCoinsEarned + amount);
        }

        private static bool HasPdfNpcName(StoryNpcRecord npc)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.displayName) ||
                npc.displayName.StartsWith("Duelista ",
                    StringComparison.OrdinalIgnoreCase))
                return false;
            string digits = new string((npc.npcId ?? string.Empty)
                .Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int number) &&
                   number >= 1 && number <= 20;
        }

        private static bool IsHardEncounter(RogueliteNodeType type) =>
            type == RogueliteNodeType.EliteDuel ||
            type == RogueliteNodeType.FinalDuelArena ||
            type == RogueliteNodeType.Boss;

        private void CompleteCurrentNode(bool saveNow = true)
        {
            StoryRuntimeNode runtime = RuntimeNode(Save.currentNodeId);
            if (runtime == null) return;
            runtime.resolved = true;
            runtime.state = RogueliteNodeState.Completed;
            UnlockOutgoing(runtime.nodeId);
            if (saveNow) Persist();
        }

        private void AdvanceActOrComplete()
        {
            if (Save.mapSequenceIndex + 1 >= Save.mapSequence.Count)
            {
                Save.status = StoryRunStatus.Completed;
                Persist();
                return;
            }
            InitializeMap(Save.mapSequenceIndex + 1);
            Persist();
        }

        private void UnlockOutgoing(string nodeId)
        {
            StoryMapRecord map = CurrentMap;
            if (map == null) return;
            foreach (StoryMapEdgeRecord edge in map.edges.Where(edge =>
                         string.Equals(edge.fromNodeId, nodeId,
                             StringComparison.Ordinal)))
            {
                StoryRuntimeNode destination = RuntimeNodeFor(
                    map.mapId, edge.toNodeId);
                if (destination != null && !destination.resolved)
                    destination.state = RogueliteNodeState.Available;
            }
        }

        private StoryRuntimeNode RuntimeNodeFor(string mapId, string nodeId) =>
            Save?.runtimeNodes.FirstOrDefault(node =>
                string.Equals(node.mapId, mapId, StringComparison.Ordinal) &&
                string.Equals(node.nodeId, nodeId, StringComparison.Ordinal));

        private void ResolveInterruptedDuel()
        {
            string encounterId = Save?.activeDuelEncounterId;
            if (string.IsNullOrWhiteSpace(encounterId)) return;

            StoryEncounterDefinition encounter = Save.pendingEncounter;
            if (encounter != null && !encounter.resultCommitted &&
                string.Equals(encounter.encounterId, encounterId,
                    StringComparison.Ordinal))
            {
                // O marcador é gravado antes da troca de cena. Se ainda existir
                // ao reconstruir o gerenciador, o processo terminou sem um
                // resultado autoritativo e o duelo conta como derrota.
                if (encounter.NodeType == RogueliteNodeType.Boss)
                    CommitBossExitFailure(encounterId);
                else
                    CommitEncounterResult(encounterId, 1);
                return;
            }

            CommitInterruptedDuelFallback(encounterId);
        }

        private void CommitInterruptedDuelFallback(string encounterId)
        {
            string operationId = "duel-result:" + encounterId;
            ClearActiveDuelMarker();
            Save.pendingEncounter = null;
            if (AlreadyResolved(operationId))
            {
                Persist();
                return;
            }

            MarkResolved(operationId);
            StoryRuntimeNode interrupted = RuntimeNode(Save.currentNodeId);
            if (interrupted?.NodeType == RogueliteNodeType.Boss)
            {
                Save.seals = 0;
                Save.status = StoryRunStatus.Failed;
                Persist();
                return;
            }
            Save.seals = Math.Max(0, Save.seals - 1);
            if (Save.seals <= 0)
                Save.status = StoryRunStatus.Failed;
            else
            {
                StoryRuntimeNode current = RuntimeNode(Save.currentNodeId);
                if (current != null && IsCombat(current.NodeType))
                {
                    if (current.NodeType == RogueliteNodeType.Boss)
                    {
                        current.resolved = false;
                        current.state = RogueliteNodeState.Current;
                    }
                    else
                        CompleteCurrentNode(false);
                }
            }
            Persist();
        }

        private void CommitBossExitFailure(string encounterId)
        {
            string operationId = "duel-result:" + encounterId;
            ClearActiveDuelMarker();
            if (!AlreadyResolved(operationId)) MarkResolved(operationId);
            if (Save.pendingEncounter != null)
            {
                Save.pendingEncounter.resultCommitted = true;
                Save.pendingEncounter.winner = 1;
            }
            Save.pendingEncounter = null;
            Save.pendingReward = null;
            Save.pendingChoice = null;
            Save.pendingRelicReward = null;
            Save.pendingRelicReplacement = null;
            Save.pendingRandomEvent = null;
            Save.seals = 0;
            Save.status = StoryRunStatus.Failed;
            Persist();
        }

        private void EnsureProceduralMaps()
        {
            bool current = Save.generatorVersion ==
                    StoryProceduralMapGenerator.GeneratorVersion &&
                Save.generatedMaps != null &&
                Save.generatedMaps.Count ==
                    StoryProceduralMapGenerator.ActCount &&
                Save.mapSequence != null &&
                Save.mapSequence.SequenceEqual(
                    Save.generatedMaps.Select(map => map?.mapId),
                    StringComparer.Ordinal) &&
                Save.generatedMaps.All(map => map != null &&
                    string.IsNullOrWhiteSpace(map.backgroundResourcePath));
            if (current) return;

            int index = Mathf.Clamp(
                Math.Max(0, Save.actIndex - 1),
                0,
                StoryProceduralMapGenerator.ActCount - 1);
            Save.generatorVersion = StoryProceduralMapGenerator.GeneratorVersion;
            Save.schemaVersion = Math.Max(3, Save.schemaVersion);
            Save.generatedMaps = StoryProceduralMapGenerator.GenerateRun(
                Save.seed);
            Save.mapSequence = Save.generatedMaps.Select(map => map.mapId)
                .ToList();
            Save.runtimeNodes.Clear();
            Save.pendingTransition = null;
            Save.pendingEncounter = null;
            Save.pendingReward = null;
            Save.pendingChoice = null;
            Save.pendingRelicReward = null;
            Save.pendingRelicReplacement = null;
            Save.pendingRandomEvent = null;
            ClearActiveDuelMarker();
            InitializeMap(index);
            Persist();
        }

        private void EnsureCurrentMapLayout()
        {
            StoryMapRecord map = CurrentMap;
            if (map == null || map.nodes == null || map.nodes.Count == 0)
                return;

            bool requiresMigration = map.Node(Save.currentNodeId) == null ||
                map.nodes.Any(node =>
                    RuntimeNodeFor(map.mapId, node.nodeId) == null);
            if (!requiresMigration) return;

            Save.runtimeNodes.RemoveAll(node => string.Equals(
                node.mapId, map.mapId, StringComparison.Ordinal));
            Save.pendingTransition = null;
            Save.pendingEncounter = null;
            Save.pendingReward = null;
            Save.pendingChoice = null;
            Save.pendingRelicReward = null;
            Save.pendingRelicReplacement = null;
            Save.pendingRandomEvent = null;
            ClearActiveDuelMarker();
            InitializeMap(Save.mapSequenceIndex);
            Persist();
        }

        private void ClearActiveDuelMarker()
        {
            if (Save == null) return;
            Save.activeDuelEncounterId = string.Empty;
            Save.activeDuelStartedUtcTicks = 0;
        }

        private bool AlreadyResolved(string operationId) =>
            Save.resolvedOperationIds.Contains(operationId,
                StringComparer.Ordinal);

        private void MarkResolved(string operationId) =>
            AddUnique(Save.resolvedOperationIds, operationId);

        private static void AddUnique(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value) ||
                values.Contains(value, StringComparer.Ordinal)) return;
            values.Add(value);
        }

        private void Persist() => persistence.Save(Save);

        public static bool IsCombat(RogueliteNodeType type) =>
            type == RogueliteNodeType.NormalDuel ||
            type == RogueliteNodeType.EliteDuel ||
            type == RogueliteNodeType.FinalDuelArena ||
            type == RogueliteNodeType.Boss;
    }

    public sealed class StoryDuelLaunchContext
    {
        public string runId;
        public string encounterId;
        public int playerLifePoints;
        public int opponentLifePoints;
        public int minimumMainDeckSize;
    }

    public static class StoryRogueliteRuntime
    {
        public static StoryRunManager Manager { get; private set; }
        public static StoryDuelLaunchContext DuelContext { get; private set; }
        public static bool ReturnToStoryRequested { get; private set; }
        public static bool IsStoryDuel => DuelContext != null;

        public static StoryRunManager GetManager(CardCatalog catalog)
        {
            Manager ??= new StoryRunManager(catalog);
            return Manager;
        }

        public static void PrepareDuel(StoryDuelLaunchContext context)
        {
            DuelContext = context ?? throw new ArgumentNullException(
                nameof(context));
            ReturnToStoryRequested = false;
        }

        public static bool CommitAuthoritativeResult(byte winner)
        {
            if (DuelContext == null || Manager?.Save == null) return false;
            Manager.CommitEncounterResult(DuelContext.encounterId, winner);
            ReturnToStoryRequested = true;
            return true;
        }

        public static bool ForfeitActiveDuel()
        {
            if (DuelContext == null || Manager?.Save == null) return false;
            bool forfeited = Manager.ForfeitActiveDuel(
                DuelContext.encounterId);
            ReturnToStoryRequested = true;
            return forfeited;
        }

        public static void RequestReturnToStory()
        {
            ReturnToStoryRequested = true;
        }

        public static bool ConsumeReturnRequest()
        {
            bool value = ReturnToStoryRequested;
            ReturnToStoryRequested = false;
            DuelContext = null;
            return value;
        }

        public static void ClearDuelContext()
        {
            DuelContext = null;
        }
    }
}
