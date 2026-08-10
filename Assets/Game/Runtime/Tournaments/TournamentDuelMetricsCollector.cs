using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneDuel.Game.Tournaments
{
    /// <summary>
    /// Converte eventos observáveis do Core em métricas neutras do torneio.
    /// A agregação acontece durante o duelo; o lobby recebe apenas o resumo,
    /// evitando pacotes grandes e perda de eventos por limite de transporte.
    /// </summary>
    public sealed class TournamentDuelMetricsCollector
    {
        private readonly string duelId;
        private readonly string playerAId;
        private readonly string playerBId;
        private readonly Dictionary<string, TournamentCardStats> cards =
            new Dictionary<string, TournamentCardStats>(StringComparer.Ordinal);
        private readonly Dictionary<uint, CardActivation> activeChains =
            new Dictionary<uint, CardActivation>();
        private readonly long startedAtUtcTicks;
        private int currentTurn;
        private int eventCount;
        private int pendingBattleDamageDealer = -1;
        private string pendingBattleCardId = string.Empty;

        private readonly struct CardActivation
        {
            internal CardActivation(byte player, string cardId)
            {
                Player = player;
                CardId = cardId;
            }

            internal byte Player { get; }
            internal string CardId { get; }
        }

        public TournamentDuelMetricsCollector(
            string duelId,
            string playerAId,
            string playerBId)
        {
            this.duelId = duelId ?? string.Empty;
            this.playerAId = playerAId ?? string.Empty;
            this.playerBId = playerBId ?? string.Empty;
            startedAtUtcTicks = DateTime.UtcNow.Ticks;
        }

        public void Capture(DuelEvent duelEvent)
        {
            if (duelEvent == null)
                return;
            eventCount++;
            switch (duelEvent.Message)
            {
                case CoreMessage.NewTurn:
                    currentTurn++;
                    break;
                case CoreMessage.Draw:
                    Player(duelEvent.Player).cardsDrawn +=
                        Math.Max(1, (int)duelEvent.Value);
                    break;
                case CoreMessage.Summoning:
                case CoreMessage.FlipSummoning:
                    RegisterSummon(duelEvent, false);
                    break;
                case CoreMessage.SpecialSummoning:
                    RegisterSummon(duelEvent, true);
                    break;
                case CoreMessage.Chaining:
                    RegisterActivation(duelEvent);
                    break;
                case CoreMessage.ChainSolved:
                    RegisterResolution(duelEvent.Value);
                    break;
                case CoreMessage.Move:
                    RegisterMove(duelEvent);
                    break;
                case CoreMessage.Battle:
                    pendingBattleDamageDealer = duelEvent.Player <= 1
                        ? duelEvent.Player
                        : -1;
                    pendingBattleCardId = Normalize(duelEvent.Code);
                    RegisterBattleDestruction(duelEvent);
                    break;
                case CoreMessage.Damage:
                    RegisterDamage(duelEvent);
                    break;
                case CoreMessage.NewPhase:
                case CoreMessage.ChainEnd:
                    pendingBattleDamageDealer = -1;
                    pendingBattleCardId = string.Empty;
                    break;
            }
        }

        public TournamentDuelStatsSnapshot Finish()
        {
            var snapshot = new TournamentDuelStatsSnapshot
            {
                statsSnapshotId = Guid.NewGuid().ToString("N"),
                duelId = duelId,
                startedAtUtcTicks = startedAtUtcTicks,
                finishedAtUtcTicks = DateTime.UtcNow.Ticks,
                turnCount = currentTurn,
                capturedEventCount = eventCount,
                playerA = playerA,
                playerB = playerB,
                perCardStats = cards.Values
                    .OrderBy(card => card.cardId, StringComparer.Ordinal)
                    .ThenBy(card => card.playerId, StringComparer.Ordinal)
                    .ToList()
            };
            return snapshot;
        }

        private readonly TournamentDuelPlayerStats playerA =
            new TournamentDuelPlayerStats { startedFirst = true };
        private readonly TournamentDuelPlayerStats playerB =
            new TournamentDuelPlayerStats();

        private TournamentDuelPlayerStats Player(int seat)
        {
            playerA.playerId = playerAId;
            playerB.playerId = playerBId;
            return seat == 1 ? playerB : playerA;
        }

        private void RegisterSummon(DuelEvent duelEvent, bool special)
        {
            int seat = ResolveController(duelEvent);
            TournamentDuelPlayerStats player = Player(seat);
            player.monstersSummoned++;
            if (special)
                player.specialSummons++;
            TournamentCardStats card = Card(seat, duelEvent.Code);
            if (card != null)
                card.timesSummoned++;
        }

        private void RegisterActivation(DuelEvent duelEvent)
        {
            int seat = ResolveController(duelEvent);
            Player(seat).effectsActivated++;
            TournamentCardStats card = Card(seat, duelEvent.Code);
            if (card != null)
            {
                card.timesActivated++;
                activeChains[duelEvent.Value] = new CardActivation(
                    (byte)seat,
                    card.cardId);
            }
        }

        private void RegisterResolution(uint chainIndex)
        {
            if (!activeChains.TryGetValue(chainIndex, out CardActivation activation))
                return;
            Player(activation.Player).effectsResolved++;
            activeChains.Remove(chainIndex);
        }

        private void RegisterMove(DuelEvent duelEvent)
        {
            string cardId = Normalize(duelEvent.Code);
            if (string.IsNullOrEmpty(cardId) || duelEvent.Current == null)
                return;
            int seat = duelEvent.Current.Controller <= 1
                ? duelEvent.Current.Controller
                : ResolveController(duelEvent);
            TournamentCardStats card = Card(seat, duelEvent.Code);
            TournamentDuelPlayerStats player = Player(seat);
            uint previous = duelEvent.Previous?.Location ?? 0;
            uint current = duelEvent.Current.Location;
            if (current == DuelLocation.Graveyard &&
                previous != DuelLocation.Graveyard)
            {
                player.cardsSentToGraveyard++;
                card.timesSentToGraveyard++;
            }
            else if (current == DuelLocation.Banished &&
                     previous != DuelLocation.Banished)
            {
                player.cardsBanished++;
                card.timesBanished++;
            }
            else if (current == DuelLocation.Hand &&
                     previous != DuelLocation.Hand &&
                     previous != DuelLocation.Deck)
            {
                player.cardsReturnedToHand++;
                card.timesReturnedToHand++;
            }
            else if (current == DuelLocation.Deck &&
                     previous != DuelLocation.Deck)
            {
                player.cardsReturnedToDeck++;
                card.timesReturnedToDeck++;
            }
        }

        private void RegisterBattleDestruction(DuelEvent duelEvent)
        {
            if (duelEvent.AttackerDestroyed)
                Player(duelEvent.Player).cardsDestroyed++;
            if (duelEvent.TargetDestroyed && duelEvent.Player <= 1)
                Player(1 - duelEvent.Player).cardsDestroyed++;
        }

        private void RegisterDamage(DuelEvent duelEvent)
        {
            if (duelEvent.Player > 1)
                return;
            int receiver = duelEvent.Player;
            int dealer = 1 - receiver;
            int damage = (int)Math.Min(100000u, duelEvent.Value);
            Player(receiver).damageReceived += damage;
            Player(dealer).damageDealt += damage;
            bool battle = pendingBattleDamageDealer == dealer;
            if (battle && !string.IsNullOrEmpty(pendingBattleCardId))
            {
                TournamentCardStats card = Card(dealer, pendingBattleCardId);
                if (card != null)
                    card.battleDamage += damage;
            }
            pendingBattleDamageDealer = -1;
            pendingBattleCardId = string.Empty;
        }

        private TournamentCardStats Card(int seat, uint code)
        {
            return Card(seat, Normalize(code));
        }

        private TournamentCardStats Card(int seat, string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return null;
            string playerId = seat == 1 ? playerBId : playerAId;
            string key = playerId + ":" + cardId;
            if (!cards.TryGetValue(key, out TournamentCardStats stats))
            {
                stats = new TournamentCardStats
                {
                    playerId = playerId,
                    cardId = cardId,
                    duelsAppeared = 1
                };
                cards.Add(key, stats);
            }
            return stats;
        }

        private static int ResolveController(DuelEvent duelEvent)
        {
            if (duelEvent?.Current != null && duelEvent.Current.Controller <= 1)
                return duelEvent.Current.Controller;
            if (duelEvent?.Previous != null && duelEvent.Previous.Controller <= 1)
                return duelEvent.Previous.Controller;
            return duelEvent != null && duelEvent.Player == 1 ? 1 : 0;
        }

        private static string Normalize(uint code)
        {
            return code == 0 ? string.Empty : code.ToString("00000000");
        }

        private static string Normalize(string code)
        {
            return uint.TryParse(code, out uint parsed) && parsed != 0
                ? parsed.ToString("00000000")
                : string.Empty;
        }
    }
}
