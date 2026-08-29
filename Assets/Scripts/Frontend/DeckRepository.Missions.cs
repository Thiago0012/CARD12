using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        private const int MissionStateSchemaVersion = 1;
        private const int MaximumMissionProgressEvents = 2048;
        private const int MissionClockRollbackToleranceSeconds = 120;
        private const int MissionRewardRuleVersion = 1;

        public MissionState Missions => State?.missions;

        private void NormalizeMissionState(int loadedSchemaVersion)
        {
            if (State == null)
                return;
            State.missions ??= new MissionState();
            MissionState state = State.missions;
            state.schemaVersion = MissionStateSchemaVersion;
            state.cycleId = (state.cycleId ?? string.Empty).Trim();
            state.deviceSessionId = string.IsNullOrWhiteSpace(
                state.deviceSessionId)
                ? Guid.NewGuid().ToString("N")
                : state.deviceSessionId.Trim();
            state.missions ??= new List<MissionProgressState>();
            state.claimedMissionInstanceIds ??= new List<string>();
            state.resolvedRewardOperationIds ??= new List<string>();
            state.processedProgressEventIds ??= new List<string>();
            state.missions.RemoveAll(item => item == null);
            foreach (MissionProgressState mission in state.missions)
                mission.Normalize();
            state.claimedMissionInstanceIds = Unique(
                state.claimedMissionInstanceIds);
            state.resolvedRewardOperationIds = Unique(
                state.resolvedRewardOperationIds);
            state.processedProgressEventIds = Unique(
                state.processedProgressEventIds);
            TrimMissionEvents(state.processedProgressEventIds);

            // A validação não sobrevive ao processo: um relógio local não
            // pode transformar sozinho um save carregado em tempo confiável.
            state.timeValidated = false;
        }

        public bool TryRefreshMissionCycle(
            long authoritativeUtcUnixSeconds,
            MissionCatalog catalog,
            out bool changed,
            out string rejection)
        {
            changed = false;
            rejection = string.Empty;
            if (State?.missions == null || catalog == null ||
                authoritativeUtcUnixSeconds <= 0)
            {
                rejection = "Não foi possível validar o ciclo de missões.";
                return false;
            }

            MissionState state = State.missions;
            long last = state.lastAuthoritativeUtcTicks > 0
                ? new DateTime(state.lastAuthoritativeUtcTicks,
                    DateTimeKind.Utc).ToUnixSeconds()
                : 0;
            if (last > 0 && authoritativeUtcUnixSeconds +
                MissionClockRollbackToleranceSeconds < last)
            {
                state.timeValidated = false;
                Save();
                rejection = "O horário do servidor retrocedeu. " +
                            "O ciclo foi preservado até uma nova validação.";
                return false;
            }

            long effectiveNow = Math.Max(last, authoritativeUtcUnixSeconds);
            string cycleId = MissionCycleRules.CycleId(effectiveNow);
            bool needsCycle = !string.Equals(
                                  state.cycleId,
                                  cycleId,
                                  StringComparison.Ordinal) ||
                              state.missions.Count == 0;
            if (needsCycle)
            {
                IReadOnlyList<MissionDefinitionData> selected =
                    MissionCycleRules.Select(
                        catalog,
                        cycleId,
                        State.localProfileId ?? string.Empty);
                if (selected.Count == 0)
                {
                    rejection = "O catálogo não possui missões ativas.";
                    return false;
                }
                long start = MissionCycleRules.CycleStart(effectiveNow);
                state.cycleId = cycleId;
                state.cycleStartUtcTicks = UnixTicks(start);
                state.cycleEndUtcTicks = UnixTicks(
                    start + MissionCycleRules.CycleSeconds);
                state.missions = selected.Select(definition =>
                    CreateProgress(cycleId, definition)).ToList();
                state.claimedMissionInstanceIds.Clear();
                state.resolvedRewardOperationIds.Clear();
                state.processedProgressEventIds.Clear();
                changed = true;
            }

            state.timeValidated = true;
            state.lastAuthoritativeUtcTicks = UnixTicks(effectiveNow);
            string loginEvent = $"login:{state.cycleId}:" +
                                (State.localProfileId ?? string.Empty);
            if (!state.processedProgressEventIds.Contains(loginEvent))
            {
                ApplyMissionMetric(
                    MissionMetric.DailyLogin,
                    1,
                    false,
                    false,
                    false,
                    false,
                    false);
                RememberMissionEvent(loginEvent);
                changed = true;
            }
            Save();
            return true;
        }

        public void MarkMissionTimeUnvalidated()
        {
            if (State?.missions == null || !State.missions.timeValidated)
                return;
            State.missions.timeValidated = false;
            Save();
        }

        public bool TryClaimMissionReward(
            string missionInstanceId,
            long authoritativeUtcUnixSeconds,
            out ShopTransactionRecord receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            MissionState state = State?.missions;
            if (state == null || !state.timeValidated ||
                authoritativeUtcUnixSeconds <= 0)
            {
                rejection = "Valide o horário do servidor antes de resgatar.";
                return false;
            }
            if (!string.Equals(
                    state.cycleId,
                    MissionCycleRules.CycleId(authoritativeUtcUnixSeconds),
                    StringComparison.Ordinal))
            {
                rejection = "Este ciclo expirou. Atualize as missões.";
                return false;
            }

            MissionProgressState mission = state.missions.FirstOrDefault(item =>
                item != null && string.Equals(
                    item.missionInstanceId,
                    missionInstanceId,
                    StringComparison.Ordinal));
            if (mission == null)
            {
                rejection = "A missão não pertence ao ciclo atual.";
                return false;
            }
            if (!mission.completed)
            {
                rejection = "Conclua a missão antes de resgatar.";
                return false;
            }

            string operationId = MissionRewardOperationId(
                state.cycleId,
                mission.missionInstanceId);
            ShopTransactionRecord existing = FindTransaction(operationId);
            if (existing != null)
            {
                if (!string.Equals(existing.kind, "mission-reward",
                        StringComparison.Ordinal))
                {
                    rejection = "O identificador do resgate já está em uso.";
                    return false;
                }
                mission.rewardClaimed = true;
                AddUnique(state.claimedMissionInstanceIds,
                    mission.missionInstanceId);
                AddUnique(state.resolvedRewardOperationIds, operationId);
                Save();
                receipt = existing;
                return true;
            }
            if (mission.rewardClaimed ||
                state.resolvedRewardOperationIds.Contains(operationId))
            {
                rejection = "Esta recompensa já foi resgatada.";
                return false;
            }

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                State.coinBalance = checked(
                    State.coinBalance + mission.rewardCoins);
                receipt = CreateTransaction(
                    operationId,
                    "mission-reward",
                    mission.definitionId,
                    mission.rewardCoins,
                    Array.Empty<string>());
                receipt.rewardRuleVersion = MissionRewardRuleVersion;
                receipt.rewardStatus = RewardReceiptStatus.Granted;
                State.processedShopTransactions.Add(receipt);
                mission.rewardClaimed = true;
                AddUnique(state.claimedMissionInstanceIds,
                    mission.missionInstanceId);
                AddUnique(state.resolvedRewardOperationIds, operationId);
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                receipt = null;
                rejection = "A recompensa não foi gravada: " +
                            exception.GetBaseException().Message;
                return false;
            }
        }

        internal void RecordMissionDuelResult(
            string resultId,
            bool winner,
            bool online,
            bool ranked,
            long damageDealt)
        {
            string eventId = "mission-result:" + (resultId ?? string.Empty);
            if (!CanApplyMissionEvent(eventId))
                return;
            ApplyMissionMetric(MissionMetric.DamageDealt,
                Math.Max(0, damageDealt), online, ranked, false, false, false);
            if (online)
            {
                ApplyMissionMetric(MissionMetric.OnlineMatchesPlayed,
                    1, true, ranked, false, false, false);
                if (winner)
                {
                    ApplyMissionMetric(MissionMetric.OnlineMatchesWon,
                        1, true, ranked, false, false, false);
                }
            }
            RememberMissionEvent(eventId);
        }

        internal void RecordMissionStatisticEvent(
            string eventId,
            DuelStatisticEventType eventType,
            long amount,
            bool online,
            bool ranked)
        {
            string missionEventId = "mission-stat:" +
                                    (eventId ?? string.Empty);
            if (!CanApplyMissionEvent(missionEventId))
                return;
            switch (eventType)
            {
                case DuelStatisticEventType.MonsterDestroyedByBattle:
                case DuelStatisticEventType.MonsterDestroyedByEffect:
                case DuelStatisticEventType.SpellDestroyed:
                case DuelStatisticEventType.TrapDestroyed:
                    ApplyMissionMetric(MissionMetric.CardsDestroyed, amount,
                        online, ranked, false, false, false);
                    break;
                case DuelStatisticEventType.SpellActivated:
                    ApplyMissionMetric(MissionMetric.SpellsActivated, amount,
                        online, ranked, false, false, false);
                    ApplyMissionMetric(MissionMetric.SpellsOrTrapsActivated,
                        amount, online, ranked, false, false, false);
                    break;
                case DuelStatisticEventType.TrapActivated:
                    ApplyMissionMetric(MissionMetric.TrapsActivated, amount,
                        online, ranked, false, false, false);
                    ApplyMissionMetric(MissionMetric.SpellsOrTrapsActivated,
                        amount, online, ranked, false, false, false);
                    break;
                case DuelStatisticEventType.MonsterSummoned:
                case DuelStatisticEventType.SpecialSummon:
                    ApplyMissionMetric(MissionMetric.MonstersSummoned, amount,
                        online, ranked, false, false, false);
                    break;
            }
            RememberMissionEvent(missionEventId);
        }

        internal void RecordEligibleMissionCoins(
            string sourceOperationId,
            int amount,
            bool online,
            bool ranked,
            bool story)
        {
            string eventId = "mission-coins:" +
                             (sourceOperationId ?? string.Empty);
            if (amount <= 0 || !CanApplyMissionEvent(eventId))
                return;
            ApplyMissionMetric(MissionMetric.AccountCoinsEarnedEligible,
                amount, online, ranked, false, story, true);
            RememberMissionEvent(eventId);
        }

        public static string MissionRewardOperationId(
            string cycleId,
            string missionInstanceId)
        {
            return $"mission:{cycleId}:{missionInstanceId}:reward:" +
                   $"v{MissionRewardRuleVersion}";
        }

        private bool CanApplyMissionEvent(string eventId)
        {
            return State?.missions?.missions != null &&
                   State.missions.missions.Count > 0 &&
                   !string.IsNullOrWhiteSpace(eventId) &&
                   !State.missions.processedProgressEventIds.Contains(eventId);
        }

        private void ApplyMissionMetric(
            MissionMetric metric,
            long amount,
            bool online,
            bool ranked,
            bool tournament,
            bool story,
            bool collection)
        {
            if (amount <= 0 || State?.missions?.missions == null)
                return;
            foreach (MissionProgressState mission in State.missions.missions)
            {
                if (mission == null || mission.rewardClaimed ||
                    mission.metric != metric ||
                    !ScopeMatches(mission.scope, online, ranked, tournament,
                        story, collection))
                {
                    continue;
                }
                mission.currentValue = Math.Min(
                    mission.targetValue,
                    checked(mission.currentValue + amount));
                mission.completed = mission.currentValue >= mission.targetValue;
            }
        }

        private static bool ScopeMatches(
            MissionScope scope,
            bool online,
            bool ranked,
            bool tournament,
            bool story,
            bool collection)
        {
            return scope switch
            {
                MissionScope.Global => true,
                MissionScope.OnlineAny => online,
                MissionScope.OnlineRanked => online && ranked,
                MissionScope.OnlineTournament => online && tournament,
                MissionScope.StoryRoguelite => story,
                MissionScope.Collection => collection,
                _ => false
            };
        }

        private void RememberMissionEvent(string eventId)
        {
            AddUnique(State.missions.processedProgressEventIds, eventId);
            TrimMissionEvents(State.missions.processedProgressEventIds);
        }

        private static MissionProgressState CreateProgress(
            string cycleId,
            MissionDefinitionData definition)
        {
            return new MissionProgressState
            {
                missionInstanceId = cycleId + ":" + definition.missionId,
                definitionId = definition.missionId,
                displayName = definition.displayName,
                description = definition.description,
                tier = definition.tier,
                scope = definition.scope,
                metric = definition.metric,
                currentValue = 0,
                targetValue = definition.targetValue,
                rewardCoins = definition.rewardCoins
            };
        }

        private static List<string> Unique(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static void AddUnique(ICollection<string> values, string value)
        {
            if (values != null && !string.IsNullOrWhiteSpace(value) &&
                !values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static void TrimMissionEvents(List<string> values)
        {
            if (values == null || values.Count <= MaximumMissionProgressEvents)
                return;
            values.RemoveRange(0, values.Count - MaximumMissionProgressEvents);
        }

        private static long UnixTicks(long unixSeconds) =>
            DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.Ticks;
    }

    internal static class MissionDateTimeExtensions
    {
        public static long ToUnixSeconds(this DateTime value) =>
            new DateTimeOffset(value).ToUnixTimeSeconds();
    }
}
