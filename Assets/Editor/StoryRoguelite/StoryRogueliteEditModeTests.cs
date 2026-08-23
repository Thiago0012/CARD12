using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneArena.Frontend;
using ArcaneArena.StoryRoguelite;
using ArcaneDuel.DuelEngine.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class StoryRogueliteEditModeTests
    {
        [Test]
        public void OfficialLifePointMatrix_IsKeptSeparateFromStandardDuel()
        {
            Assert.That(
                StoryEncounterLpProfile.ResolveOfficialEnemyLifePoints(
                    RogueliteNodeType.NormalDuel, 1),
                Is.EqualTo(6000));
            Assert.That(
                StoryEncounterLpProfile.ResolveOfficialEnemyLifePoints(
                    RogueliteNodeType.EliteDuel, 1),
                Is.EqualTo(12000));
            Assert.That(
                StoryEncounterLpProfile.ResolveOfficialEnemyLifePoints(
                    RogueliteNodeType.FinalDuelArena, 1),
                Is.EqualTo(14000));
            Assert.That(
                StoryEncounterLpProfile.ResolveOfficialEnemyLifePoints(
                    RogueliteNodeType.Boss, 1),
                Is.EqualTo(20000));
            Assert.That(
                StoryEncounterLpProfile.ResolveOfficialEnemyLifePoints(
                    RogueliteNodeType.Boss, 2),
                Is.EqualTo(35000));
            Assert.That(
                StoryEncounterLpProfile.ResolveOfficialEnemyLifePoints(
                    RogueliteNodeType.Boss, 3),
                Is.EqualTo(50000));

            var standard = new DuelConfiguration();
            Assert.That(standard.PlayerStartingLifePoints, Is.EqualTo(8000));
            Assert.That(standard.OpponentStartingLifePoints, Is.EqualTo(8000));
            standard.PlayerStartingLifePoints = 6000;
            standard.OpponentStartingLifePoints = 20000;
            Assert.That(standard.PlayerStartingLifePoints, Is.EqualTo(6000));
            Assert.That(standard.OpponentStartingLifePoints, Is.EqualTo(20000));
        }

        [Test]
        public void StoryDeckRules_RequireTwentyAtStart_AndTwentyToThirtyLater()
        {
            IReadOnlyList<string> legal = StoryStarterDeckService
                .BuildStarters().First().Main;
            Assert.That(legal, Has.Count.EqualTo(20));
            Assert.That(StoryRunManager.Rules.Validate(
                legal, Array.Empty<string>(), true).IsValid, Is.True);
            Assert.That(StoryRunManager.Rules.Validate(
                legal.Take(19).ToArray(), Array.Empty<string>(), true).IsValid,
                Is.False);
            IReadOnlyList<string> legalThirty = StoryStarterDeckService
                .TakeLegalCards(
                    DeckShopCatalog.CreateOpponentRoster().First()
                        .mainDeckCardIds,
                    30);
            Assert.That(legalThirty, Has.Count.EqualTo(30));
            Assert.That(StoryRunManager.Rules.Validate(
                legalThirty,
                Array.Empty<string>(), false).IsValid,
                Is.True);
            Assert.That(StoryRunManager.Rules.Validate(
                legalThirty.Concat(new[] { legalThirty[0] }).ToArray(),
                Array.Empty<string>(), false).IsValid,
                Is.False);
        }

        [Test]
        public void ContentCatalog_HasOfficialNpcNamesFromSpecification()
        {
            StoryContentCatalog.ClearCache();
            StoryContentCatalogFile content = StoryContentCatalog.Load();
            Assert.That(content.npcs, Has.Count.EqualTo(30));
            Assert.That(content.maps, Has.Count.EqualTo(7));
            string[] expectedNames =
            {
                "Kael", "Rina", "Helena", "Selene", "Mika", "Amara",
                "Ren", "Yumi", "Darius", "Axel", "Zahir", "Nyra",
                "Dante", "Lucien", "Valeria", "Skye", "Akane",
                "Aureon", "Vespera", "Amon"
            };
            Assert.That(content.npcs.Take(20).Select(npc => npc.displayName),
                Is.EqualTo(expectedNames));
            Assert.That(content.npcs.Select(npc => npc.npcId).Distinct().Count(),
                Is.EqualTo(30));
        }

        [Test]
        public void SameSeed_ProducesSameProceduralMapsAndDialogue()
        {
            long seed = 2026082201L;
            List<StoryMapRecord> mapsA =
                StoryProceduralMapGenerator.GenerateRun(seed);
            List<StoryMapRecord> mapsB =
                StoryProceduralMapGenerator.GenerateRun(seed);
            Assert.That(JsonUtility.ToJson(new StoryMapList(mapsA)),
                Is.EqualTo(JsonUtility.ToJson(new StoryMapList(mapsB))));
            StoryNpcRecord npc = StoryContentCatalog.Load().npcs[0];
            string first = StoryDialogueService.Create(
                seed, "encounter-1", npc, RogueliteNodeType.NormalDuel);
            string second = StoryDialogueService.Create(
                seed, "encounter-1", npc, RogueliteNodeType.NormalDuel);
            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void ProceduralMaps_AreImageFreeReachableAndGuaranteeTwoDuels()
        {
            List<StoryMapRecord> maps = StoryProceduralMapGenerator.GenerateRun(
                77123944L);
            Assert.That(maps, Has.Count.EqualTo(3));
            foreach (StoryMapRecord map in maps)
            {
                Assert.That(map.backgroundResourcePath, Is.Empty);
                Assert.That(map.Node(map.startNodeId), Is.Not.Null);
                Assert.That(map.Node(map.bossNodeId), Is.Not.Null);
                Assert.That(map.nodes.All(node =>
                        node.x >= 0f && node.x <= 1f &&
                        node.y >= 0f && node.y <= 1f),
                    Is.True);
                Assert.That(MinimumDuelsOnAnyPath(
                        map, map.startNodeId, new HashSet<string>()),
                    Is.GreaterThanOrEqualTo(
                        StoryProceduralMapGenerator.MinimumDuelsBeforeBoss),
                    map.mapId);
            }
        }

        [Test]
        public void AccountCoinRewards_UseRequestedRanges()
        {
            for (int index = 0; index < 64; index++)
            {
                int simple = StoryRunManager.CalculateAccountCoinReward(
                    9900L + index,
                    "normal-" + index,
                    RogueliteNodeType.NormalDuel);
                int hard = StoryRunManager.CalculateAccountCoinReward(
                    4400L + index,
                    "elite-" + index,
                    RogueliteNodeType.EliteDuel);
                Assert.That(simple, Is.InRange(1, 5));
                Assert.That(hard, Is.InRange(10, 25));
            }
        }

        [Test]
        public void AccountCoinReward_IsPersistedOnlyOncePerEncounter()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "arcane-story-coin-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "decks.json");
            try
            {
                var repository = new DeckRepository(path);
                repository.Load(null);
                int before = repository.CoinBalance;
                Assert.That(repository.TryGrantStoryRogueliteCoins(
                    "run:test:encounter:coin",
                    17,
                    out _,
                    out string firstRejection), Is.True, firstRejection);
                Assert.That(repository.TryGrantStoryRogueliteCoins(
                    "run:test:encounter:coin",
                    17,
                    out _,
                    out string secondRejection), Is.True, secondRejection);
                Assert.That(repository.CoinBalance, Is.EqualTo(before + 17));
                Assert.That(repository.State.processedShopTransactions.Count(
                        transaction => transaction.transactionId ==
                            "run:test:encounter:coin"),
                    Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PendingTransition_IsResumedAtomically()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "arcane-story-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "run.json");
            try
            {
                var persistence = new StoryRunPersistence(path);
                var manager = new StoryRunManager(null, persistence);
                StoryStarterDeck starter = StoryStarterDeckService
                    .BuildStarters().First();
                manager.StartNew(721983L, starter.Main, starter.Extra,
                    "test-profile", "Tester", "");
                string destination = manager.CurrentMap.edges
                    .First(edge => edge.fromNodeId ==
                                   manager.Save.currentNodeId)
                    .toNodeId;
                Assert.That(manager.SelectNode(destination, out _), Is.True);
                Assert.That(manager.CommitSelectedTransition(out _), Is.True);
                Assert.That(persistence.Load().pendingTransition, Is.Not.Null);

                var resumed = new StoryRunManager(null, persistence);
                Assert.That(resumed.Save.pendingTransition, Is.Null);
                Assert.That(resumed.Save.currentNodeId, Is.EqualTo(destination));
                Assert.That(resumed.RuntimeNode(destination).state,
                    Is.EqualTo(RogueliteNodeState.Current));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void DuelWithoutAuthoritativeResult_IsLossOnNextLaunch()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "arcane-story-duel-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "run.json");
            try
            {
                var persistence = new StoryRunPersistence(path);
                var manager = new StoryRunManager(null, persistence);
                StoryStarterDeck starter = StoryStarterDeckService
                    .BuildStarters().First();
                manager.StartNew(972431L, starter.Main, starter.Extra,
                    "test-profile", "Tester", "");
                manager.Save.pendingEncounter = new StoryEncounterDefinition
                {
                    encounterId = "encounter-interrompido",
                    mapId = manager.Save.currentMapId,
                    nodeId = manager.Save.currentNodeId,
                    npcName = "NPC de Teste",
                    nodeType = RogueliteNodeType.NormalDuel.ToString(),
                    npcId = "npc-test",
                    botProfileId = "bot-test",
                    enemyDeckId = "deck-test",
                    enemyMainDeck = starter.Main.ToList(),
                    act = manager.Save.actIndex,
                    playerLifePoints = 8000,
                    opponentLifePoints = 6000
                };

                Assert.That(manager.MarkDuelStarted(
                    "encounter-interrompido", out _), Is.True);
                Assert.That(persistence.Load().activeDuelEncounterId,
                    Is.EqualTo("encounter-interrompido"));

                var resumed = new StoryRunManager(null, persistence);
                Assert.That(resumed.Save.seals,
                    Is.EqualTo(StoryRunManager.Rules.sealsAtRunStart - 1));
                Assert.That(resumed.Save.pendingEncounter, Is.Null);
                Assert.That(string.IsNullOrEmpty(
                    resumed.Save.activeDuelEncounterId), Is.True);
                Assert.That(resumed.Save.status,
                    Is.EqualTo(StoryRunStatus.Active));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void EmptyPendingObjects_AreRecoveredFromExistingSave()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "arcane-story-empty-state-tests-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "run.json");
            try
            {
                var persistence = new StoryRunPersistence(path);
                var manager = new StoryRunManager(null, persistence);
                StoryStarterDeck starter = StoryStarterDeckService
                    .BuildStarters().First();
                manager.StartNew(618427L, starter.Main, starter.Extra,
                    "test-profile", "Tester", "");
                manager.Save.pendingEncounter = new StoryEncounterDefinition();
                manager.Save.pendingReward = new StoryPendingReward();
                manager.Save.pendingChoice = new StoryPendingChoice();
                manager.Save.pendingTransition = new StoryPendingTransition();
                manager.Save.activeDuelStartedUtcTicks = DateTime.UtcNow.Ticks;

                File.WriteAllText(path,
                    JsonUtility.ToJson(manager.Save, true));

                var resumed = new StoryRunManager(null, persistence);
                Assert.That(resumed.Save.pendingEncounter, Is.Null);
                Assert.That(resumed.Save.pendingReward, Is.Null);
                Assert.That(resumed.Save.pendingChoice, Is.Null);
                Assert.That(resumed.Save.pendingTransition, Is.Null);
                Assert.That(resumed.Save.activeDuelStartedUtcTicks, Is.Zero);
                Assert.That(resumed.Save.currentNodeId, Is.EqualTo("start"));
                Assert.That(resumed.Save.status,
                    Is.EqualTo(StoryRunStatus.Active));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void LiveEmptyEncounter_IsClearedBeforeItCanOpenOrLaunch()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "arcane-story-live-empty-tests-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "run.json");
            try
            {
                var persistence = new StoryRunPersistence(path);
                var manager = new StoryRunManager(null, persistence);
                StoryStarterDeck starter = StoryStarterDeckService
                    .BuildStarters().First();
                manager.StartNew(884621L, starter.Main, starter.Extra,
                    "test-profile", "Tester", "");
                manager.Save.pendingEncounter = new StoryEncounterDefinition();
                manager.Save.activeDuelStartedUtcTicks = DateTime.UtcNow.Ticks;

                Assert.That(manager.MarkDuelStarted(string.Empty, out _),
                    Is.False);
                Assert.That(manager.RepairInvalidPendingEncounter(), Is.True);
                Assert.That(manager.Save.pendingEncounter, Is.Null);
                Assert.That(manager.Save.activeDuelStartedUtcTicks, Is.Zero);
                Assert.That(manager.Save.seals,
                    Is.EqualTo(StoryRunManager.Rules.sealsAtRunStart));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void LeavingBossDuel_FailsRunImmediately()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "arcane-story-boss-exit-tests-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "run.json");
            try
            {
                var manager = new StoryRunManager(
                    null, new StoryRunPersistence(path));
                StoryStarterDeck starter = StoryStarterDeckService
                    .BuildStarters().First();
                manager.StartNew(991723L, starter.Main, starter.Extra,
                    "test-profile", "Tester", "");
                manager.Save.currentNodeId = manager.CurrentMap.bossNodeId;
                StoryRuntimeNode boss = manager.RuntimeNode(
                    manager.CurrentMap.bossNodeId);
                boss.resolvedType = RogueliteNodeType.Boss.ToString();
                boss.state = RogueliteNodeState.Current;
                manager.Save.pendingEncounter = new StoryEncounterDefinition
                {
                    encounterId = "boss-exit",
                    mapId = manager.Save.currentMapId,
                    nodeId = manager.Save.currentNodeId,
                    npcName = "Amon",
                    nodeType = RogueliteNodeType.Boss.ToString(),
                    npcId = "NPC_020",
                    botProfileId = "bot-test",
                    enemyDeckId = "deck-test",
                    enemyMainDeck = starter.Main.ToList(),
                    act = manager.Save.actIndex,
                    playerLifePoints = 6000,
                    opponentLifePoints = 20000
                };
                Assert.That(manager.MarkDuelStarted("boss-exit", out _),
                    Is.True);
                Assert.That(manager.ForfeitActiveDuel("boss-exit"), Is.True);
                Assert.That(manager.Save.status,
                    Is.EqualTo(StoryRunStatus.Failed));
                Assert.That(manager.Save.seals, Is.Zero);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Serializable]
        private sealed class StoryMapList
        {
            public List<StoryMapRecord> maps;

            public StoryMapList(List<StoryMapRecord> value)
            {
                maps = value;
            }
        }

        private static int MinimumDuelsOnAnyPath(
            StoryMapRecord map,
            string nodeId,
            HashSet<string> visiting)
        {
            if (!visiting.Add(nodeId)) return int.MaxValue / 4;
            StoryMapNodeRecord node = map.Node(nodeId);
            int here = node != null &&
                       (node.NodeType == RogueliteNodeType.NormalDuel ||
                        node.NodeType == RogueliteNodeType.EliteDuel ||
                        node.NodeType == RogueliteNodeType.FinalDuelArena)
                ? 1
                : 0;
            if (string.Equals(nodeId, map.bossNodeId,
                    StringComparison.Ordinal))
            {
                visiting.Remove(nodeId);
                return here;
            }
            List<StoryMapEdgeRecord> outgoing = map.edges.Where(edge =>
                    string.Equals(edge.fromNodeId, nodeId,
                        StringComparison.Ordinal))
                .ToList();
            if (outgoing.Count == 0)
            {
                visiting.Remove(nodeId);
                return int.MinValue / 4;
            }
            int minimum = outgoing.Min(edge => MinimumDuelsOnAnyPath(
                map, edge.toNodeId, visiting));
            visiting.Remove(nodeId);
            return here + minimum;
        }
    }
}
