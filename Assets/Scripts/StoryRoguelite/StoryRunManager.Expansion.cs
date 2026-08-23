using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneArena.Frontend;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    public sealed partial class StoryRunManager
    {
        public bool SelectPendingRelic(
            string relicId,
            out string rejection)
        {
            rejection = string.Empty;
            StoryPendingRelicReward reward = Save?.pendingRelicReward;
            if (reward == null || !reward.relicIds.Contains(
                    relicId, StringComparer.Ordinal))
            {
                rejection = "A relíquia não pertence a esta recompensa.";
                return false;
            }

            StoryRelicDefinition definition = StoryRelicLibrary.Resolve(
                relicId);
            StoryRelicCapacityResult capacity = StoryRelicService
                .CheckCapacity(Save, definition);
            if (capacity == StoryRelicCapacityResult.CanAcquireDirectly)
            {
                StoryRelicService.Acquire(Save, definition);
                FinishPendingRelicReward();
                return true;
            }
            if (capacity == StoryRelicCapacityResult.AlreadyOwned)
            {
                rejection = "Essa relíquia já está ativa nesta jornada.";
                return false;
            }
            if (capacity == StoryRelicCapacityResult.InvalidDefinition)
            {
                rejection = "A definição dessa relíquia está indisponível.";
                return false;
            }

            List<string> candidates = StoryRelicService
                .ReplacementCandidates(Save, definition);
            if (candidates.Count == 0)
            {
                rejection = "Nenhuma substituição válida foi encontrada.";
                return false;
            }
            Save.pendingRelicReplacement = new StoryPendingRelicReplacement
            {
                operationId = reward.operationId + ":replace:" + relicId,
                incomingRelicId = relicId,
                eligibleRelicIds = candidates
            };
            Persist();
            return true;
        }

        public bool ReplacePendingRelic(
            string outgoingRelicId,
            out string rejection)
        {
            rejection = string.Empty;
            StoryPendingRelicReplacement replacement =
                Save?.pendingRelicReplacement;
            if (replacement == null || !replacement.eligibleRelicIds.Contains(
                    outgoingRelicId, StringComparer.Ordinal))
            {
                rejection = "A relíquia escolhida não pode ser substituída.";
                return false;
            }
            StoryRelicDefinition incoming = StoryRelicLibrary.Resolve(
                replacement.incomingRelicId);
            if (!StoryRelicService.Replace(
                    Save, outgoingRelicId, incoming))
            {
                rejection = "A substituição não pôde ser concluída.";
                return false;
            }
            Save.pendingRelicReplacement = null;
            FinishPendingRelicReward();
            return true;
        }

        public void RejectPendingRelic()
        {
            if (Save?.pendingRelicReward == null) return;
            Save.pendingRelicReplacement = null;
            FinishPendingRelicReward();
        }

        public void CancelPendingRelicReplacement()
        {
            if (Save?.pendingRelicReplacement == null) return;
            Save.pendingRelicReplacement = null;
            Persist();
        }

        private void FinishPendingRelicReward()
        {
            StoryPendingRelicReward reward = Save?.pendingRelicReward;
            if (reward == null) return;
            MarkResolved(reward.operationId);
            Save.pendingRelicReward = null;
            Save.pendingRelicReplacement = null;
            if (reward.completeNodeAfterResolution)
                CompleteCurrentNode(false);
            if (reward.advanceActAfterResolution)
                AdvanceActOrComplete();
            else
                Persist();
        }

        private void HandleExpandedEncounterVictory(
            StoryEncounterDefinition encounter)
        {
            AddUnique(Save.defeatedNpcIds, encounter.npcId);
            Save.pendingEncounter = null;

            if (encounter.nestedRandomEventDuel)
            {
                Save.fragments += Math.Max(0,
                    encounter.eventVictoryFragments);
                if (Save.pendingRandomEvent != null)
                    Save.pendingRandomEvent.waitingForNestedDuel = false;
                if (string.Equals(
                        Save.pendingRandomEvent?.eventId,
                        "lightning-arena",
                        StringComparison.Ordinal))
                {
                    CreateReward("RECOMPENSA DO DESAFIO", 3);
                    Save.pendingReward.completeRandomEventOnClaim = true;
                }
                else
                {
                    CompletePendingRandomEvent(
                        $"Vitória: +{encounter.eventVictoryFragments} Fragmentos.");
                }
                Persist();
                return;
            }

            int baseFragments = encounter.NodeType switch
            {
                RogueliteNodeType.Boss => 10,
                RogueliteNodeType.FinalDuelArena => 8,
                RogueliteNodeType.EliteDuel => 5,
                _ => 2
            };
            int fragmentsAwarded = StoryRelicService.VictoryFragments(
                Save, encounter.NodeType, baseFragments);
            Save.fragments += fragmentsAwarded;
            int accountCoins = encounter.suppressAccountCoins
                ? 0
                : AccountCoinReward(encounter);
            if (!encounter.suppressAccountCoins)
                QueueAccountCoinReward(encounter, accountCoins);

            if (encounter.NodeType != RogueliteNodeType.Boss)
            {
                CreateReward(
                    "RECOMPENSA DE DUELO",
                    StoryRelicService.DuelRewardChoiceCount(
                        Save, encounter.NodeType),
                    false,
                    false,
                    fragmentsAwarded,
                    accountCoins);
            }

            if (!encounter.suppressRelicDrop)
            {
                Save.pendingRelicReward = StoryRelicRewardResolver
                    .ResolvePostDuel(Save, encounter);
            }
            if (encounter.NodeType == RogueliteNodeType.Boss &&
                Save.pendingRelicReward == null)
            {
                CompleteCurrentNode(false);
                AdvanceActOrComplete();
                return;
            }
            Persist();
        }

        private void HandleExpandedEncounterDefeat(
            StoryEncounterDefinition encounter)
        {
            if (encounter.nestedRandomEventDuel)
            {
                Save.pendingEncounter = null;
                Save.seals = Math.Max(0, Save.seals - 1);
                if (Save.seals <= 0)
                    Save.status = StoryRunStatus.Failed;
                else
                    CompletePendingRandomEvent(
                        "Derrota no desafio: 1 Selo perdido.");
                Persist();
                return;
            }

            if (encounter.NodeType != RogueliteNodeType.Boss &&
                StoryRelicService.Has(Save, "immortal-phoenix"))
            {
                StoryRelicService.Consume(Save, "immortal-phoenix");
                encounter.encounterId += ":phoenix-retry";
                encounter.resultCommitted = false;
                encounter.winner = byte.MaxValue;
                Save.pendingEncounter = encounter;
                Persist();
                return;
            }

            Save.seals = Math.Max(0, Save.seals - 1);
            if (Save.seals <= 0 &&
                StoryRelicService.ProtectRunFromFailure(Save))
                Save.seals = 1;
            Save.pendingEncounter = null;

            if (encounter.NodeType != RogueliteNodeType.Boss &&
                HasArtifact("persistence-seal"))
            {
                string flag = $"relic-used:persistence-seal:act-{Save.actIndex}";
                if (!Save.storyFlags.Contains(flag, StringComparer.Ordinal))
                {
                    AddUnique(Save.storyFlags, flag);
                    AddNextDuelModifier(flag, 1000, 0, 0);
                }
            }

            if (Save.seals <= 0)
                Save.status = StoryRunStatus.Failed;
            else if (encounter.NodeType == RogueliteNodeType.Boss)
            {
                StoryRuntimeNode boss = RuntimeNode(Save.currentNodeId);
                if (boss != null)
                {
                    boss.resolved = false;
                    boss.state = RogueliteNodeState.Current;
                }
            }
            else
                CompleteCurrentNode(false);
            Persist();
        }

        public bool ResolveRandomEventChoice(
            string choiceId,
            out string rejection)
        {
            rejection = string.Empty;
            StoryPendingRandomEvent pending = Save?.pendingRandomEvent;
            StoryRandomEventOption option = pending?.options.FirstOrDefault(
                candidate => string.Equals(candidate.choiceId, choiceId,
                    StringComparison.Ordinal));
            if (pending == null || option == null || !option.enabled)
            {
                rejection = "Essa opção não está disponível.";
                return false;
            }
            if (pending.waitingForNestedDuel)
            {
                rejection = "O desafio deste evento ainda está em andamento.";
                return false;
            }
            if (Save.fragments < option.fragmentCost)
            {
                rejection = "Fragmentos insuficientes.";
                return false;
            }

            Save.fragments -= option.fragmentCost;
            double firstRoll = pending.preRolledValues.Count > 0
                ? pending.preRolledValues[0]
                : 0d;
            string summary = "Evento concluído.";
            bool startsDuel = false;
            switch (pending.eventId)
            {
                case "unstable-vault":
                    if (choiceId == "careful") Save.fragments += 3;
                    else if (choiceId == "force")
                    {
                        int delta = firstRoll < .60d ? 8 :
                            firstRoll < .90d ? 5 :
                            -Math.Min(2, Save.fragments);
                        Save.fragments = Math.Max(0, Save.fragments + delta);
                        summary = $"Resultado do cofre: {Signed(delta)} Fragmentos.";
                    }
                    break;
                case "forbidden-library":
                    if (!string.IsNullOrWhiteSpace(option.cardId))
                    {
                        Save.reserveCards.Add(option.cardId);
                        AddNextDuelModifier(pending.operationId,
                            -1000, 0, 0);
                    }
                    break;
                case "wounded-duelist":
                    if (choiceId == "help")
                        Save.seals = Math.Min(MaxSeals, Save.seals + 1);
                    else if (choiceId == "advice") Save.fragments += 2;
                    break;
                case "blood-pact":
                    if (choiceId.StartsWith("pact-", StringComparison.Ordinal))
                    {
                        Save.seals = Math.Max(1, Save.seals - 1);
                        Save.fragments += 9;
                        AddCardToReserve(option.cardId);
                    }
                    break;
                case "path-oracle":
                    RevealUpcomingMysteries(choiceId == "full" ? int.MaxValue : 2);
                    break;
                case "wandering-merchant":
                    if (choiceId.StartsWith("buy-", StringComparison.Ordinal))
                        AddCardToReserve(option.cardId);
                    break;
                case "cracked-relic":
                    if (choiceId.StartsWith("restore-", StringComparison.Ordinal))
                        RestoreRelicCharge(option.relicId);
                    else if (choiceId == "dismantle") Save.fragments += 3;
                    break;
                case "lightning-arena":
                    if (choiceId == "challenge")
                    {
                        StartNestedEventDuel(pending, null, true);
                        startsDuel = true;
                    }
                    break;
                case "fate-table":
                    if (choiceId.StartsWith("bet-", StringComparison.Ordinal))
                    {
                        int bet = option.fragmentCost;
                        if (firstRoll < .55d) Save.fragments += bet * 2;
                        summary = firstRoll < .55d
                            ? $"Aposta vencida: +{bet} Fragmentos líquidos."
                            : $"Aposta perdida: -{bet} Fragmentos.";
                    }
                    break;
                case "cursed-seal":
                    if (choiceId == "accept")
                    {
                        Save.fragments += 7;
                        AddNextDuelModifier(pending.operationId,
                            0, 3000, 0);
                    }
                    break;
                case "arcane-capsule":
                    AddCardToReserve(option.cardId);
                    break;
                case "memory-rift":
                    if (choiceId.StartsWith("echo-", StringComparison.Ordinal))
                    {
                        StartNestedEventDuel(pending, option.npcId, false);
                        startsDuel = true;
                    }
                    break;
                case "arcane-spring":
                    if (choiceId == "seal")
                        Save.seals = Math.Min(MaxSeals, Save.seals + 1);
                    else Save.fragments += 4;
                    break;
                case "thousand-eyes-trial":
                    int trialDelta = choiceId switch
                    {
                        "caution" => 2,
                        "balance" => firstRoll < .70d ? 5 : -1,
                        "audacity" => firstRoll < .45d ? 10 :
                            firstRoll < .80d ? 5 : 0,
                        _ => 0
                    };
                    Save.fragments = Math.Max(0,
                        Save.fragments + trialDelta);
                    if (choiceId == "audacity" && firstRoll >= .80d)
                        AddNextDuelModifier(pending.operationId,
                            -1500, 0, 0);
                    summary = $"Julgamento: {Signed(trialDelta)} Fragmentos.";
                    break;
                case "duelist-sanctuary":
                    if (choiceId == "hand")
                        AddNextDuelModifier(pending.operationId,
                            0, 0, 1);
                    else Save.fragments += 2;
                    break;
            }

            if (!startsDuel) CompletePendingRandomEvent(summary);
            else Persist();
            return true;
        }

        private void StartNestedEventDuel(
            StoryPendingRandomEvent pending,
            string npcId,
            bool lightningArena)
        {
            StoryEncounterDefinition encounter = EnsureEncounter(
                RogueliteNodeType.NormalDuel);
            encounter.nestedRandomEventDuel = true;
            encounter.sourceEventOperationId = pending.operationId;
            encounter.suppressAccountCoins = true;
            encounter.suppressRelicDrop = true;
            encounter.eventVictoryFragments = lightningArena ? 7 : 6;
            if (lightningArena)
            {
                int[] life = { 9000, 13000, 17000 };
                encounter.opponentLifePoints = life[Mathf.Clamp(
                    Save.actIndex - 1, 0, life.Length - 1)];
                encounter.aiTier = Math.Max(encounter.aiTier,
                    Mathf.Clamp(Save.actIndex + 1, 2, 4));
            }
            else
            {
                StoryNpcRecord npc = StoryContentCatalog.ResolveNpc(npcId);
                if (npc != null)
                {
                    encounter.npcId = npc.npcId;
                    encounter.npcName = npc.displayName;
                    encounter.portraitResourcePath = npc.portraitResourcePath;
                    encounter.aiTier = Math.Min(5,
                        Math.Max(encounter.aiTier, npc.aiTierMin) + 1);
                }
                encounter.opponentLifePoints += 3000;
            }
            pending.waitingForNestedDuel = true;
            pending.nestedDuelId = encounter.encounterId;
            Persist();
        }

        private void CompletePendingRandomEvent(string summary)
        {
            StoryPendingRandomEvent pending = Save?.pendingRandomEvent;
            if (pending == null) return;
            pending.resultSummary = summary ?? string.Empty;
            MarkResolved(pending.operationId);
            Save.randomEventHistory.Add(new StoryRandomEventHistoryEntry
            {
                actIndex = pending.actIndex,
                nodeId = pending.nodeId,
                eventId = pending.eventId
            });
            Save.pendingRandomEvent = null;
            CompleteCurrentNode(false);
            Persist();
        }

        private void AddNextDuelModifier(
            string sourceOperationId,
            int playerLpDelta,
            int opponentLpDelta,
            int handDelta)
        {
            Save.nextDuelModifiers ??= new StoryNextDuelModifiers();
            if (Save.nextDuelModifiers.sourceOperationIds.Contains(
                    sourceOperationId, StringComparer.Ordinal)) return;
            Save.nextDuelModifiers.playerStartingLpDelta += playerLpDelta;
            Save.nextDuelModifiers.opponentStartingLpDelta += opponentLpDelta;
            Save.nextDuelModifiers.openingHandDelta += handDelta;
            AddUnique(Save.nextDuelModifiers.sourceOperationIds,
                sourceOperationId);
        }

        private void RevealUpcomingMysteries(int maximum)
        {
            StoryMapRecord map = CurrentMap;
            if (map == null) return;
            StoryMapNodeRecord current = map.Node(Save.currentNodeId);
            float currentY = current?.y ?? 0f;
            foreach (StoryMapNodeRecord node in map.nodes
                         .Where(node => node.NodeType ==
                             RogueliteNodeType.Mystery)
                         .Where(node => node.y > currentY &&
                             node.y <= currentY + 0.26f)
                         .OrderBy(node => node.y)
                         .ThenBy(node => node.nodeId,
                             StringComparer.Ordinal)
                         .Take(maximum))
                AddUnique(Save.revealedNodeIds, node.nodeId);
        }

        private void RestoreRelicCharge(string relicId)
        {
            StoryRelicRuntimeState state = StoryRelicService.Active(Save)
                .FirstOrDefault(candidate => string.Equals(
                    candidate.relicId, relicId, StringComparison.Ordinal));
            StoryRelicDefinition definition = StoryRelicLibrary.Resolve(
                relicId);
            if (state != null && definition.initialCharges > 0)
                state.chargesRemaining = Math.Min(
                    definition.initialCharges, state.chargesRemaining + 1);
        }

        private void AddCardToReserve(string cardId)
        {
            if (!string.IsNullOrWhiteSpace(cardId))
                Save.reserveCards.Add(cardId);
        }

        private static string Signed(int value) => value >= 0
            ? "+" + value
            : value.ToString();
    }
}
