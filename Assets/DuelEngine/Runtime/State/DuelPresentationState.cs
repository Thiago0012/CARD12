using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneDuel.DuelEngine.State
{
    public sealed class DuelistState
    {
        public DuelistState()
        {
            for (int index = 0; index < OverlayInstances.Length; index++)
                OverlayInstances[index] = new List<CardInstanceState>();
        }

        public int LifePoints { get; internal set; } = 8000;
        public int DeckCount { get; internal set; } = 40;
        public int ExtraDeckCount { get; internal set; } = 3;
        internal List<uint> DeckContents { get; } = new List<uint>();
        internal List<uint> ExtraDeckContents { get; } = new List<uint>();
        internal List<CardInstanceState> DeckInstances { get; } =
            new List<CardInstanceState>();
        public List<CardInstanceState> ExtraDeckInstances { get; } =
            new List<CardInstanceState>();
        public IReadOnlyList<uint> ExtraDeckCards => ExtraDeckContents;
        public List<uint> Hand { get; } = new List<uint>();
        public List<CardInstanceState> HandInstances { get; } =
            new List<CardInstanceState>();
        public uint[] MonsterZones { get; } = new uint[7];
        public uint[] MonsterPositions { get; } = new uint[7];
        public CardInstanceState[] MonsterInstances { get; } =
            new CardInstanceState[7];
        public List<CardInstanceState>[] OverlayInstances { get; } =
            new List<CardInstanceState>[7];
        public uint[] SpellTrapZones { get; } = new uint[8];
        public uint[] SpellTrapPositions { get; } = new uint[8];
        public CardInstanceState[] SpellTrapInstances { get; } =
            new CardInstanceState[8];
        public List<uint> Graveyard { get; } = new List<uint>();
        public List<CardInstanceState> GraveyardInstances { get; } =
            new List<CardInstanceState>();
        public List<uint> Banished { get; } = new List<uint>();
        public List<CardInstanceState> BanishedInstances { get; } =
            new List<CardInstanceState>();
    }

    public sealed class DuelistPresentationSnapshot
    {
        public int LifePoints { get; internal set; }
        public int DeckCount { get; internal set; }
        public int ExtraDeckCount { get; internal set; }
        public uint[] ExtraDeck { get; internal set; }
        public uint[] ExtraDeckPositions { get; internal set; }
        public uint[] Hand { get; internal set; }
        public uint[] MonsterZones { get; internal set; }
        public uint[] MonsterPositions { get; internal set; }
        public uint[] SpellTrapZones { get; internal set; }
        public uint[] SpellTrapPositions { get; internal set; }
        public uint[] Graveyard { get; internal set; }
        public uint[] Banished { get; internal set; }
        public uint[] BanishedPositions { get; internal set; }
        public uint[][] OverlayMaterials { get; internal set; }
        public ulong[] HandRuntimeIds { get; internal set; }
        public ulong[] ExtraDeckRuntimeIds { get; internal set; }
        public ulong[] MonsterRuntimeIds { get; internal set; }
        public ulong[] SpellTrapRuntimeIds { get; internal set; }
        public ulong[] GraveyardRuntimeIds { get; internal set; }
        public ulong[] BanishedRuntimeIds { get; internal set; }
        public ulong[][] OverlayRuntimeIds { get; internal set; }
        public byte[] HandOwners { get; internal set; }
        public byte[] ExtraDeckOwners { get; internal set; }
        public byte[] MonsterOwners { get; internal set; }
        public byte[] SpellTrapOwners { get; internal set; }
        public byte[] GraveyardOwners { get; internal set; }
        public byte[] BanishedOwners { get; internal set; }
        public byte[][] OverlayOwners { get; internal set; }
    }

    public sealed class DuelPresentationSnapshot
    {
        public DuelistPresentationSnapshot[] Players { get; internal set; }
        public int TurnNumber { get; internal set; }
        public byte TurnPlayer { get; internal set; }
        public uint Phase { get; internal set; }
        public byte? Winner { get; internal set; }
        public uint DisabledFieldMask { get; internal set; }
        public DuelChainLinkSnapshot[] ChainLinks { get; internal set; }
        public CardPresentationMetadataSnapshot[] CardMetadata
        {
            get;
            internal set;
        }
        public PlayerHintSnapshot[] PlayerHints { get; internal set; }
        public DuelSummonSnapshot PendingSummon { get; internal set; }
        public DuelSummonSnapshot LastSummon { get; internal set; }
        public string[] Log { get; internal set; }
    }

    public enum DuelSummonStatus : byte
    {
        Pending = 0,
        Confirmed = 1,
        Negated = 2
    }

    /// <summary>
    /// Presentation-only record of a summon announced by ocgcore. The Core
    /// remains the sole authority: a Summoning message creates a pending
    /// attempt and only the matching Summoned message confirms it.
    /// </summary>
    public sealed class DuelSummonSnapshot
    {
        public CoreMessage Message { get; internal set; }
        public uint CardCode { get; internal set; }
        public ulong RuntimeId { get; internal set; }
        public byte Controller { get; internal set; }
        public byte Location { get; internal set; }
        public uint Sequence { get; internal set; }
        public uint Position { get; internal set; }
        public DuelSummonStatus Status { get; internal set; }
    }

    public enum DuelChainLinkStatus : byte
    {
        Chaining = 0,
        Chained = 1,
        Solving = 2,
        Solved = 3,
        Negated = 4,
        Disabled = 5
    }

    public sealed class DuelChainLinkSnapshot
    {
        public uint ChainIndex { get; internal set; }
        public byte Player { get; internal set; }
        public uint CardCode { get; internal set; }
        public ulong DescriptionId { get; internal set; }
        public ulong RuntimeId { get; internal set; }
        public byte Controller { get; internal set; }
        public byte Location { get; internal set; }
        public uint Sequence { get; internal set; }
        public uint Position { get; internal set; }
        public DuelChainLinkStatus Status { get; internal set; }
    }

    public sealed class CardPresentationMetadataSnapshot
    {
        public ulong RuntimeId { get; internal set; }
        public uint CoreStatus { get; internal set; }
        public bool IsPublic { get; internal set; }
        public uint LinkRating { get; internal set; }
        public uint LinkMarkers { get; internal set; }
        public ushort[] CounterTypes { get; internal set; }
        public uint[] CounterAmounts { get; internal set; }
        public ulong EquippedToRuntimeId { get; internal set; }
        public ulong[] TargetRuntimeIds { get; internal set; }
        public ulong[] RelationRuntimeIds { get; internal set; }
        public byte[] HintTypes { get; internal set; }
        public ulong[] HintValues { get; internal set; }
        public bool IsTemporaryTarget { get; internal set; }
    }

    public sealed class PlayerHintSnapshot
    {
        public byte Player { get; internal set; }
        public byte HintType { get; internal set; }
        public ulong Value { get; internal set; }
    }

    public sealed class DuelPresentationState
    {
        private readonly CardDatabase database;
        private readonly Dictionary<int, ulong> playerHints = new();
        private readonly List<string> transitionConsistencyProblems = new();
        private ulong nextRuntimeId = 1;

        public DuelistState[] Players { get; } = { new DuelistState(), new DuelistState() };
        public int TurnNumber { get; private set; }
        public byte TurnPlayer { get; private set; }
        public uint Phase { get; private set; }
        public byte? Winner { get; private set; }
        public uint DisabledFieldMask { get; private set; }
        public List<DuelChainLinkSnapshot> ChainLinks { get; } = new();
        public bool ChainEndPendingReconciliation { get; private set; }
        public IReadOnlyList<PlayerHintSnapshot> PlayerHints =>
            CapturePlayerHints();
        public DuelSummonSnapshot PendingSummon { get; private set; }
        public DuelSummonSnapshot LastSummon { get; private set; }
        public DuelPrompt Prompt { get; private set; }
        public List<string> Log { get; } = new List<string>();

        public DuelPresentationState(CardDatabase database)
        {
            this.database = database;
        }

        public void ConfigureDeckCounts(
            int playerMain,
            int playerExtra,
            int opponentMain,
            int opponentExtra)
        {
            Players[0].DeckCount = Math.Max(0, playerMain);
            Players[0].ExtraDeckCount = Math.Max(0, playerExtra);
            Players[1].DeckCount = Math.Max(0, opponentMain);
            Players[1].ExtraDeckCount = Math.Max(0, opponentExtra);
            Players[0].DeckContents.Clear();
            Players[0].ExtraDeckContents.Clear();
            Players[0].DeckInstances.Clear();
            Players[0].ExtraDeckInstances.Clear();
            Players[1].DeckContents.Clear();
            Players[1].ExtraDeckContents.Clear();
            Players[1].DeckInstances.Clear();
            Players[1].ExtraDeckInstances.Clear();
        }

        public void ConfigureDeckContents(
            IEnumerable<uint> playerMain,
            IEnumerable<uint> playerExtra,
            IEnumerable<uint> opponentMain,
            IEnumerable<uint> opponentExtra)
        {
            ConfigureDeckContents(Players[0], 0, playerMain, playerExtra);
            ConfigureDeckContents(Players[1], 1, opponentMain, opponentExtra);
        }

        public DuelPresentationSnapshot CaptureSnapshot()
        {
            return new DuelPresentationSnapshot
            {
                Players = new[]
                {
                    Capture(Players[0]),
                    Capture(Players[1])
                },
                TurnNumber = TurnNumber,
                TurnPlayer = TurnPlayer,
                Phase = Phase,
                Winner = Winner,
                DisabledFieldMask = DisabledFieldMask,
                ChainLinks = ChainLinks.Select(CloneChainLink).ToArray(),
                CardMetadata = CaptureCardMetadata(),
                PlayerHints = CapturePlayerHints(),
                PendingSummon = CloneSummon(PendingSummon),
                LastSummon = CloneSummon(LastSummon),
                Log = Log.ToArray()
            };
        }

        public void ReconcileFromCore(OcgFieldSnapshot snapshot)
        {
            if (snapshot?.Players == null || snapshot.Players.Length != 2)
                throw new ArgumentException(
                    "The authoritative Core snapshot must contain two duelists.",
                    nameof(snapshot));

            for (byte controller = 0; controller < Players.Length; controller++)
            {
                OcgDuelistFieldSnapshot source = snapshot.Players[controller];
                if (source == null)
                    throw new ArgumentException(
                        "The authoritative Core snapshot contains a null duelist.",
                        nameof(snapshot));
                DuelistState destination = Players[controller];
                ReconcileList(
                    destination.DeckContents,
                    destination.DeckInstances,
                    source.Deck,
                    controller,
                    (byte)DuelLocation.Deck);
                ReconcileList(
                    destination.ExtraDeckContents,
                    destination.ExtraDeckInstances,
                    source.Extra,
                    controller,
                    (byte)DuelLocation.Extra);
                destination.DeckCount = source.Deck?.Length ?? 0;
                destination.ExtraDeckCount = source.Extra?.Length ?? 0;
                ReconcileList(
                    destination.Hand,
                    destination.HandInstances,
                    source.Hand,
                    controller,
                    (byte)DuelLocation.Hand);
                ReconcileList(
                    destination.Graveyard,
                    destination.GraveyardInstances,
                    source.Graveyard,
                    controller,
                    (byte)DuelLocation.Graveyard);
                ReconcileList(
                    destination.Banished,
                    destination.BanishedInstances,
                    source.Banished,
                    controller,
                    (byte)DuelLocation.Banished);
                ReconcileZones(
                    destination.MonsterZones,
                    destination.MonsterPositions,
                    destination.MonsterInstances,
                    source.Monsters,
                    controller,
                    (byte)DuelLocation.MonsterZone);
                ReconcileZones(
                    destination.SpellTrapZones,
                    destination.SpellTrapPositions,
                    destination.SpellTrapInstances,
                    source.Spells,
                    controller,
                    (byte)DuelLocation.SpellTrapZone);
                ReconcileOverlays(destination, source.Monsters, controller);
            }
            ReconcilePersistentMetadata(snapshot);
            ReconcilePendingSummon();
            BindPromptInstances(Prompt);
        }

        /// <summary>
        /// Clears the transient chain overlay only after the caller has
        /// reconciled the field against an authoritative Core snapshot.
        /// Persistent card metadata is deliberately preserved.
        /// </summary>
        public void CompleteChainEndReconciliation()
        {
            if (!ChainEndPendingReconciliation)
                return;
            ChainLinks.Clear();
            ClearTemporaryTargets();
            ChainEndPendingReconciliation = false;
        }

        public void RestoreSnapshot(DuelPresentationSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Players == null ||
                snapshot.Players.Length != 2)
            {
                throw new ArgumentException(
                    "A presentation snapshot must contain two duelists.",
                    nameof(snapshot));
            }
            Restore(Players[0], snapshot.Players[0], 0);
            Restore(Players[1], snapshot.Players[1], 1);
            TurnNumber = snapshot.TurnNumber;
            TurnPlayer = snapshot.TurnPlayer;
            Phase = snapshot.Phase;
            Winner = snapshot.Winner;
            DisabledFieldMask = snapshot.DisabledFieldMask;
            ChainEndPendingReconciliation = false;
            ChainLinks.Clear();
            ChainLinks.AddRange(
                (snapshot.ChainLinks ?? Array.Empty<DuelChainLinkSnapshot>())
                .Where(link => link != null)
                .Select(CloneChainLink));
            playerHints.Clear();
            foreach (PlayerHintSnapshot hint in
                     snapshot.PlayerHints ?? Array.Empty<PlayerHintSnapshot>())
            {
                if (hint != null)
                    playerHints[PlayerHintKey(hint.Player, hint.HintType)] =
                        hint.Value;
            }
            RestoreCardMetadata(snapshot.CardMetadata);
            PendingSummon = CloneSummon(snapshot.PendingSummon);
            LastSummon = CloneSummon(snapshot.LastSummon);
            Prompt = null;
            Log.Clear();
            if (snapshot.Log != null) Log.AddRange(snapshot.Log);
        }

        public void Apply(DuelEvent duelEvent)
        {
            transitionConsistencyProblems.Clear();
            if (duelEvent.Prompt != null) Prompt = duelEvent.Prompt;
            switch (duelEvent.Message)
            {
                case CoreMessage.Start:
                    Players[0].LifePoints = (int)duelEvent.Value;
                    Players[1].LifePoints = (int)(duelEvent.OpponentValue > 0
                        ? duelEvent.OpponentValue
                        : duelEvent.Value);
                    AddLog("O duelo começou. Regras Mestre 5 ativas.");
                    break;
                case CoreMessage.NewTurn:
                    TurnNumber++;
                    TurnPlayer = duelEvent.Player;
                    AddLog($"Turno {TurnNumber} · Duelista {TurnPlayer + 1}");
                    break;
                case CoreMessage.NewPhase:
                    Phase = duelEvent.Value;
                    AddLog(CoreMessageDecoder.PhaseName(Phase));
                    break;
                case CoreMessage.Draw:
                    ApplyDraw(duelEvent);
                    break;
                case CoreMessage.ShuffleHand:
                    ApplyShuffleHand(duelEvent);
                    break;
                case CoreMessage.Move:
                    ApplyMove(duelEvent);
                    break;
                case CoreMessage.PositionChange:
                    ApplyPositionChange(duelEvent);
                    break;
                case CoreMessage.Swap:
                    ApplySwap(duelEvent);
                    break;
                case CoreMessage.ShuffleSetCard:
                    ApplyShuffleSetCards(duelEvent);
                    break;
                case CoreMessage.SwapGraveDeck:
                    ApplySwapGraveDeck(duelEvent);
                    break;
                case CoreMessage.RemoveCards:
                    ApplyRemoveCards(duelEvent);
                    break;
                case CoreMessage.Damage:
                    Players[duelEvent.Player].LifePoints -= (int)duelEvent.Value;
                    AddLog($"Duelista {duelEvent.Player + 1} sofreu {duelEvent.Value} de dano.");
                    break;
                case CoreMessage.PayLifePointCost:
                    Players[duelEvent.Player].LifePoints = Math.Max(
                        0,
                        Players[duelEvent.Player].LifePoints -
                        (int)duelEvent.Value);
                    AddLog(
                        $"Duelista {duelEvent.Player + 1} pagou " +
                        $"{duelEvent.Value} PV.");
                    break;
                case CoreMessage.Recover:
                    Players[duelEvent.Player].LifePoints += (int)duelEvent.Value;
                    AddLog($"Duelista {duelEvent.Player + 1} recuperou {duelEvent.Value} PV.");
                    break;
                case CoreMessage.LifePointsUpdate:
                    Players[duelEvent.Player].LifePoints = (int)duelEvent.Value;
                    break;
                case CoreMessage.Summoning:
                case CoreMessage.SpecialSummoning:
                case CoreMessage.FlipSummoning:
                    ApplySummonAttempt(duelEvent);
                    break;
                case CoreMessage.Summoned:
                case CoreMessage.SpecialSummoned:
                case CoreMessage.FlipSummoned:
                    ApplySummonConfirmation(duelEvent.Message);
                    break;
                case CoreMessage.Chaining:
                    ApplyChaining(duelEvent);
                    AddLog($"Corrente {duelEvent.Value}: {Name(duelEvent.Code)}.");
                    break;
                case CoreMessage.Chained:
                    SetChainStatus(
                        duelEvent.Value,
                        DuelChainLinkStatus.Chained);
                    break;
                case CoreMessage.ChainSolving:
                    SetChainStatus(
                        duelEvent.Value,
                        DuelChainLinkStatus.Solving);
                    break;
                case CoreMessage.ChainSolved:
                    SetChainStatus(
                        duelEvent.Value,
                        DuelChainLinkStatus.Solved);
                    break;
                case CoreMessage.ChainNegated:
                    SetChainStatus(
                        duelEvent.Value,
                        DuelChainLinkStatus.Negated);
                    break;
                case CoreMessage.ChainDisabled:
                    SetChainStatus(
                        duelEvent.Value,
                        DuelChainLinkStatus.Disabled);
                    break;
                case CoreMessage.ChainEnd:
                    // Keep the resolved/negated/disabled links visible until
                    // the field has been reconciled at the Core safe boundary.
                    ChainEndPendingReconciliation = true;
                    break;
                case CoreMessage.FieldDisabled:
                    DisabledFieldMask = duelEvent.Value;
                    break;
                case CoreMessage.AddCounter:
                    ApplyCounter(duelEvent, true);
                    break;
                case CoreMessage.RemoveCounter:
                    ApplyCounter(duelEvent, false);
                    break;
                case CoreMessage.Equip:
                    ApplyEquip(duelEvent);
                    break;
                case CoreMessage.Unequip:
                    ApplyUnequip(duelEvent);
                    break;
                case CoreMessage.CardTarget:
                    ApplyTargetRelation(duelEvent, true);
                    break;
                case CoreMessage.CancelTarget:
                    ApplyTargetRelation(duelEvent, false);
                    break;
                case CoreMessage.CreateRelation:
                    ApplyGeneralRelation(duelEvent, true);
                    break;
                case CoreMessage.ReleaseRelation:
                    ApplyGeneralRelation(duelEvent, false);
                    break;
                case CoreMessage.BecomeTarget:
                    ApplyTemporaryTargets(duelEvent);
                    break;
                case CoreMessage.CardHint:
                    ApplyCardHint(duelEvent);
                    break;
                case CoreMessage.PlayerHint:
                    playerHints[
                        PlayerHintKey(
                            duelEvent.Player,
                            unchecked((byte)duelEvent.Code))] =
                        duelEvent.HintValue;
                    break;
                case CoreMessage.Attack:
                    AddLog(
                        duelEvent.DirectAttack
                            ? $"Duelista {duelEvent.Player + 1} declarou um ataque direto."
                            : $"Duelista {duelEvent.Player + 1} declarou um ataque.");
                    break;
                case CoreMessage.Battle:
                    AddLog(
                        duelEvent.DirectAttack
                            ? $"Ataque direto com {duelEvent.AttackerAttack} ATK."
                            : $"Batalha: {duelEvent.AttackerAttack} ATK contra " +
                              $"{Math.Max(duelEvent.TargetAttack, duelEvent.TargetDefense)}.");
                    break;
                case CoreMessage.Win:
                    Winner = duelEvent.Player;
                    Prompt = null;
                    AddLog($"Duelista {duelEvent.Player + 1} venceu o duelo.");
                    break;
                case CoreMessage.Retry:
                    AddLog("Resposta inválida; escolha novamente.");
                    break;
                default:
                    // Unknown packets remain explicit in the engine history and
                    // diagnostics, but are not exposed as technical noise in the
                    // player-facing duel timeline.
                    break;
            }
            BindPromptInstances(duelEvent.Prompt);
        }

        public void ClearPrompt()
        {
            Prompt = null;
        }

        private void ApplyDraw(DuelEvent duelEvent)
        {
            DuelistState player = Players[duelEvent.Player];
            foreach (uint code in duelEvent.Codes ?? Array.Empty<uint>())
            {
                bool materialized = HasMaterializedPile(
                    player.DeckContents,
                    player.DeckCount);
                CardInstanceState instance = materialized
                    ? RemoveListCard(
                        player.DeckContents,
                        player.DeckInstances,
                        code,
                        uint.MaxValue,
                        duelEvent.Player,
                        (byte)DuelLocation.Deck)
                    : null;
                uint sequence = (uint)player.Hand.Count;
                player.Hand.Add(code);
                instance ??= CreateInstance(
                        code,
                        duelEvent.Player,
                        duelEvent.Player,
                        (byte)DuelLocation.Hand,
                        sequence,
                        0);
                instance.DefinitionCode = code;
                instance.UpdateAddress(
                    duelEvent.Player,
                    (byte)DuelLocation.Hand,
                    sequence,
                    0);
                player.HandInstances.Add(instance);
                player.DeckCount = materialized
                    ? player.DeckContents.Count
                    : Math.Max(0, player.DeckCount - 1);
            }
            AddLog($"Duelista {duelEvent.Player + 1} comprou {(duelEvent.Codes ?? Array.Empty<uint>()).Length} carta(s).");
        }

        private void ApplyShuffleHand(DuelEvent duelEvent)
        {
            if (duelEvent.Player >= Players.Length)
                return;
            DuelistState player = Players[duelEvent.Player];
            uint[] shuffledCodes = duelEvent.Codes ?? Array.Empty<uint>();
            if (shuffledCodes.Length != player.Hand.Count)
            {
                AddLog(
                    $"A reordenação da mão do Duelista " +
                    $"{duelEvent.Player + 1} foi ignorada por divergência " +
                    "de quantidade.");
                return;
            }

            var instancesByCode =
                new Dictionary<uint, Queue<CardInstanceState>>();
            for (int index = 0; index < player.Hand.Count; index++)
            {
                uint code = player.Hand[index];
                if (!instancesByCode.TryGetValue(
                        code,
                        out Queue<CardInstanceState> instances))
                {
                    instances = new Queue<CardInstanceState>();
                    instancesByCode.Add(code, instances);
                }
                CardInstanceState instance =
                    index < player.HandInstances.Count
                        ? player.HandInstances[index]
                        : null;
                instances.Enqueue(instance);
            }

            var reordered = new List<CardInstanceState>(
                shuffledCodes.Length);
            foreach (uint code in shuffledCodes)
            {
                if (!instancesByCode.TryGetValue(
                        code,
                        out Queue<CardInstanceState> instances) ||
                    instances.Count == 0)
                {
                    AddLog(
                        $"A reordenação da mão do Duelista " +
                        $"{duelEvent.Player + 1} foi ignorada porque o " +
                        $"Core informou uma cópia inesperada de {code:00000000}.");
                    return;
                }
                reordered.Add(
                    instances.Dequeue() ??
                    CreateInstance(
                        code,
                        duelEvent.Player,
                        duelEvent.Player,
                        (byte)DuelLocation.Hand,
                        (uint)reordered.Count,
                        0));
            }

            player.Hand.Clear();
            player.Hand.AddRange(shuffledCodes);
            player.HandInstances.Clear();
            player.HandInstances.AddRange(reordered);
            Reindex(
                player.HandInstances,
                duelEvent.Player,
                (byte)DuelLocation.Hand);
            AddLog(
                $"Mão do Duelista {duelEvent.Player + 1} reordenada pelo Core.");
        }

        private void ApplyMove(DuelEvent duelEvent)
        {
            CardInstanceState moving = null;
            List<CardInstanceState> movingOverlays = null;
            bool sourceIdentityIsMaterialized =
                IsIdentityMaterialized(duelEvent.Previous);
            if (IsMonsterZone(duelEvent.Previous) &&
                IsMonsterZone(duelEvent.Current))
            {
                movingOverlays = TakeOverlayStack(duelEvent.Previous);
            }
            if (duelEvent.Previous != null && duelEvent.Previous.Location != 0)
            {
                moving = Remove(duelEvent.Previous, duelEvent.Code);
            }
            if (sourceIdentityIsMaterialized && moving == null)
            {
                transitionConsistencyProblems.Add(
                    $"Move {duelEvent.Code:00000000} could not recover its " +
                    "physical instance from the authoritative source.");
            }
            if (duelEvent.Current != null && duelEvent.Current.Location != 0)
            {
                byte originalOwner = moving?.Owner ??
                    duelEvent.Previous?.Controller ??
                    duelEvent.Current.Controller;
                Add(
                    duelEvent.Current,
                    duelEvent.Code,
                    moving,
                    originalOwner);
                PlaceOverlayStack(duelEvent.Current, movingOverlays);

                if (IsIdentityMaterialized(duelEvent.Current))
                {
                    CardInstanceState arrived =
                        InstanceFor(duelEvent.Current);
                    if (arrived == null)
                    {
                        transitionConsistencyProblems.Add(
                            $"Move {duelEvent.Code:00000000} did not " +
                            "materialize at its authoritative destination.");
                    }
                    else if (moving != null &&
                             arrived.RuntimeId != moving.RuntimeId)
                    {
                        transitionConsistencyProblems.Add(
                            $"Move {duelEvent.Code:00000000} replaced " +
                            $"physical runtime {moving.RuntimeId} with " +
                            $"runtime {arrived.RuntimeId} at its destination.");
                    }
                }
            }
            if (duelEvent.Previous != null && duelEvent.Current != null &&
                duelEvent.Previous.Location != duelEvent.Current.Location)
            {
                AddLog($"{Name(duelEvent.Code)} → {LocationName(duelEvent.Current.Location)}");
            }
            DetectNegatedSummon(duelEvent);
        }

        private void ApplySummonAttempt(DuelEvent duelEvent)
        {
            CardLocation location = duelEvent.Current;
            CardInstanceState instance = location == null
                ? null
                : InstanceAt(
                    location.Controller,
                    location.Location,
                    location.Sequence);
            PendingSummon = new DuelSummonSnapshot
            {
                Message = duelEvent.Message,
                CardCode = duelEvent.Code,
                RuntimeId = instance?.RuntimeId ?? 0UL,
                Controller = location?.Controller ?? duelEvent.Player,
                Location = location?.Location ?? 0,
                Sequence = location?.Sequence ?? 0U,
                Position = location?.Position ?? 0U,
                Status = DuelSummonStatus.Pending
            };
            AddLog($"Tentativa de invocação: {Name(duelEvent.Code)}.");
        }

        private void ApplySummonConfirmation(CoreMessage confirmation)
        {
            if (PendingSummon == null ||
                !MatchesSummonConfirmation(PendingSummon.Message, confirmation))
            {
                AddLog("Uma invocação foi confirmada pelo Core.");
                return;
            }
            LastSummon = CloneSummon(PendingSummon);
            LastSummon.Status = DuelSummonStatus.Confirmed;
            PendingSummon = null;
            AddLog($"Invocação confirmada: {Name(LastSummon.CardCode)}.");
        }

        private void DetectNegatedSummon(DuelEvent duelEvent)
        {
            if (PendingSummon == null || duelEvent?.Previous == null)
                return;
            CardLocation previous = duelEvent.Previous;
            bool sameCard = duelEvent.Code == 0 ||
                PendingSummon.CardCode == 0 ||
                duelEvent.Code == PendingSummon.CardCode;
            bool sameAddress = previous.Controller == PendingSummon.Controller &&
                (previous.Location & PendingSummon.Location) != 0 &&
                previous.Sequence == PendingSummon.Sequence;
            bool remainsInMonsterZone = duelEvent.Current != null &&
                (duelEvent.Current.Location & DuelLocation.MonsterZone) != 0;
            if (!sameCard || !sameAddress || remainsInMonsterZone)
                return;
            MarkPendingSummonNegated();
        }

        private void ReconcilePendingSummon()
        {
            if (PendingSummon == null)
                return;
            CardInstanceState instance = InstanceAt(
                PendingSummon.Controller,
                PendingSummon.Location,
                PendingSummon.Sequence);
            if (instance != null &&
                (PendingSummon.CardCode == 0 ||
                 instance.DefinitionCode == PendingSummon.CardCode))
            {
                if (PendingSummon.RuntimeId == 0)
                    PendingSummon.RuntimeId = instance.RuntimeId;
                return;
            }
            MarkPendingSummonNegated();
        }

        private void MarkPendingSummonNegated()
        {
            LastSummon = CloneSummon(PendingSummon);
            LastSummon.Status = DuelSummonStatus.Negated;
            PendingSummon = null;
            AddLog($"Invocação negada: {Name(LastSummon.CardCode)}.");
        }

        private static bool MatchesSummonConfirmation(
            CoreMessage attempt,
            CoreMessage confirmation)
        {
            return attempt == CoreMessage.Summoning &&
                       confirmation == CoreMessage.Summoned ||
                   attempt == CoreMessage.SpecialSummoning &&
                       confirmation == CoreMessage.SpecialSummoned ||
                   attempt == CoreMessage.FlipSummoning &&
                       confirmation == CoreMessage.FlipSummoned;
        }

        private void ApplySwap(DuelEvent duelEvent)
        {
            if (duelEvent.Previous == null || duelEvent.Current == null)
                return;
            uint secondCode = duelEvent.Codes != null &&
                duelEvent.Codes.Length > 0
                    ? duelEvent.Codes[0]
                    : 0U;
            List<CardInstanceState> firstOverlays =
                TakeOverlayStack(duelEvent.Previous);
            List<CardInstanceState> secondOverlays =
                TakeOverlayStack(duelEvent.Current);
            CardInstanceState first = Remove(
                duelEvent.Previous,
                duelEvent.Code);
            CardInstanceState second = Remove(
                duelEvent.Current,
                secondCode);
            Add(
                duelEvent.Current,
                duelEvent.Code,
                first,
                first?.Owner ?? duelEvent.Previous.Controller);
            Add(
                duelEvent.Previous,
                secondCode,
                second,
                second?.Owner ?? duelEvent.Current.Controller);
            PlaceOverlayStack(duelEvent.Current, firstOverlays);
            PlaceOverlayStack(duelEvent.Previous, secondOverlays);
            AddLog("Duas cartas trocaram de posição ou controle.");
        }

        private void ApplyShuffleSetCards(DuelEvent duelEvent)
        {
            CardLocation[] previous = duelEvent.PreviousLocations ??
                Array.Empty<CardLocation>();
            CardLocation[] current = duelEvent.CurrentLocations ??
                Array.Empty<CardLocation>();
            int count = Math.Min(previous.Length, current.Length);
            if (count == 0)
                return;

            var instances = new CardInstanceState[count];
            var overlays = new List<CardInstanceState>[count];
            var targets = new CardLocation[count];
            for (int index = 0; index < count; index++)
            {
                uint code = CodeAt(previous[index]);
                overlays[index] = TakeOverlayStack(previous[index]);
                instances[index] = Remove(previous[index], code);
            }

            var reservedTargets = new bool[count];
            for (int index = 0; index < count; index++)
            {
                if (!IsLocated(current[index]))
                    continue;
                targets[index] = current[index];
                for (int candidate = 0; candidate < count; candidate++)
                {
                    if (SameAddress(previous[candidate], current[index]))
                    {
                        reservedTargets[candidate] = true;
                        break;
                    }
                }
            }
            int fallback = 0;
            for (int index = 0; index < count; index++)
            {
                if (targets[index] != null)
                    continue;
                while (fallback < count && reservedTargets[fallback])
                    fallback++;
                targets[index] = fallback < count
                    ? previous[fallback]
                    : previous[index];
                if (fallback < count)
                    reservedTargets[fallback++] = true;
            }

            for (int index = 0; index < count; index++)
            {
                // The shuffle deliberately destroys the mapping for every
                // participant, including the card's owner. Give every target
                // a fresh opaque identity and mask Owner as the controller;
                // otherwise mixed-owner sets leak which original card landed
                // in a slot. Explicit current locations only route overlays.
                CardInstanceState instance = CreateInstance(
                    0,
                    targets[index].Controller,
                    targets[index].Controller,
                    targets[index].Location,
                    targets[index].Sequence,
                    targets[index].Position);
                instance.IdentityOpaque = true;
                Add(
                    targets[index],
                    0,
                    instance,
                    targets[index].Controller);
                PlaceOverlayStack(targets[index], overlays[index]);
            }
            AddLog($"{count} carta(s) baixada(s) foram embaralhadas.");
        }

        private void ApplySwapGraveDeck(DuelEvent duelEvent)
        {
            if (duelEvent.Player >= Players.Length)
                return;
            DuelistState player = Players[duelEvent.Player];
            int oldDeckCount = player.DeckCount;
            uint[] oldDeck = player.DeckContents.Count == oldDeckCount
                ? player.DeckContents.ToArray()
                : new uint[Math.Max(0, oldDeckCount)];
            CardInstanceState[] oldDeckInstances =
                player.DeckInstances.ToArray();
            uint[] oldGrave = player.Graveyard.ToArray();
            CardInstanceState[] oldGraveInstances =
                player.GraveyardInstances.ToArray();

            player.DeckContents.Clear();
            player.DeckInstances.Clear();
            for (int index = 0; index < oldGrave.Length; index++)
            {
                uint code = oldGrave[index];
                CardInstanceState instance = index < oldGraveInstances.Length
                    ? oldGraveInstances[index]
                    : null;
                if (BitIsSet(duelEvent.Codes, index))
                {
                    if (code != 0)
                    {
                        player.ExtraDeckContents.Add(code);
                        instance ??= CreateInstance(
                            code,
                            duelEvent.Player,
                            duelEvent.Player,
                            (byte)DuelLocation.Extra,
                            (uint)player.ExtraDeckInstances.Count,
                            0x1);
                        instance.UpdateAddress(
                            duelEvent.Player,
                            (byte)DuelLocation.Extra,
                            (uint)player.ExtraDeckInstances.Count,
                            0x1);
                        player.ExtraDeckInstances.Add(instance);
                    }
                }
                else if (code != 0)
                {
                    player.DeckContents.Add(code);
                    instance ??= CreateInstance(
                        code,
                        duelEvent.Player,
                        duelEvent.Player,
                        (byte)DuelLocation.Deck,
                        (uint)player.DeckInstances.Count,
                        0);
                    instance.UpdateAddress(
                        duelEvent.Player,
                        (byte)DuelLocation.Deck,
                        (uint)player.DeckInstances.Count,
                        0);
                    player.DeckInstances.Add(instance);
                }
            }

            player.Graveyard.Clear();
            player.GraveyardInstances.Clear();
            for (int index = 0; index < oldDeck.Length; index++)
            {
                uint code = oldDeck[index];
                player.Graveyard.Add(code);
                CardInstanceState instance = index < oldDeckInstances.Length
                    ? oldDeckInstances[index]
                    : null;
                instance ??= CreateInstance(
                        code,
                        duelEvent.Player,
                        duelEvent.Player,
                        (byte)DuelLocation.Graveyard,
                        (uint)index,
                        0x1);
                instance.UpdateAddress(
                    duelEvent.Player,
                    (byte)DuelLocation.Graveyard,
                    (uint)index,
                    0x1);
                player.GraveyardInstances.Add(instance);
            }
            player.DeckCount = player.DeckContents.Count;
            player.ExtraDeckCount = player.ExtraDeckContents.Count;
            AddLog(
                $"Duelista {duelEvent.Player + 1} trocou o Deck pelo Cemitério.");
        }

        private void ApplyRemoveCards(DuelEvent duelEvent)
        {
            int removed = 0;
            IEnumerable<CardLocation> locations =
                (duelEvent.PreviousLocations ?? Array.Empty<CardLocation>())
                .Where(IsLocated)
                .OrderBy(location => location.Controller)
                .ThenBy(location => location.Location)
                .ThenByDescending(location =>
                    (location.Location & DuelLocation.Overlay) != 0
                        ? location.Position
                        : location.Sequence);
            foreach (CardLocation location in locations)
            {
                uint code = CodeAt(location);
                if (IsMonsterZone(location))
                    TakeOverlayStack(location);
                Remove(location, code);
                removed++;
            }
            if (removed > 0)
                AddLog($"{removed} carta(s) foram removida(s) do duelo.");
        }

        private void ApplyChaining(DuelEvent duelEvent)
        {
            ChainEndPendingReconciliation = false;
            CardInstanceState instance = InstanceFor(duelEvent.Current);
            uint chainIndex = duelEvent.Value != 0
                ? duelEvent.Value
                : (uint)ChainLinks.Count + 1;
            ChainLinks.RemoveAll(link =>
                link != null && link.ChainIndex == chainIndex);
            ChainLinks.Add(new DuelChainLinkSnapshot
            {
                ChainIndex = chainIndex,
                Player = duelEvent.Player,
                CardCode = duelEvent.Code,
                DescriptionId = duelEvent.DescriptionId,
                RuntimeId = instance?.RuntimeId ?? 0UL,
                Controller = duelEvent.Current?.Controller ??
                             duelEvent.Player,
                Location = duelEvent.Current?.Location ?? 0,
                Sequence = duelEvent.Current?.Sequence ?? 0,
                Position = duelEvent.Current?.Position ?? 0,
                Status = DuelChainLinkStatus.Chaining
            });
            ChainLinks.Sort((left, right) =>
                left.ChainIndex.CompareTo(right.ChainIndex));
        }

        private void SetChainStatus(
            uint chainIndex,
            DuelChainLinkStatus status)
        {
            DuelChainLinkSnapshot link = ChainLinks.LastOrDefault(candidate =>
                candidate != null &&
                (chainIndex == 0 || candidate.ChainIndex == chainIndex));
            if (link != null)
            {
                link.Status = status;
                string outcome = status switch
                {
                    DuelChainLinkStatus.Chained => "encadeado",
                    DuelChainLinkStatus.Solving => "resolvendo",
                    DuelChainLinkStatus.Solved => "resolvido",
                    DuelChainLinkStatus.Negated => "ativação negada",
                    DuelChainLinkStatus.Disabled => "efeito desabilitado",
                    _ => "ativando"
                };
                AddLog($"CL{link.ChainIndex} · {outcome}: " +
                       $"{Name(link.CardCode)}.");
            }
        }

        private void ApplyCounter(DuelEvent duelEvent, bool add)
        {
            CardInstanceState instance = InstanceFor(duelEvent.Current);
            if (instance == null)
                return;
            if (add)
                instance.AddCounter(duelEvent.CounterType, duelEvent.Value);
            else
                instance.RemoveCounter(
                    duelEvent.CounterType,
                    duelEvent.Value);
        }

        private void ApplyEquip(DuelEvent duelEvent)
        {
            CardInstanceState source = InstanceFor(duelEvent.Previous);
            CardInstanceState target = InstanceFor(duelEvent.Current);
            if (source != null)
                source.EquippedToRuntimeId = target?.RuntimeId ?? 0UL;
        }

        private void ApplyUnequip(DuelEvent duelEvent)
        {
            CardInstanceState source = InstanceFor(duelEvent.Previous);
            if (source != null)
                source.EquippedToRuntimeId = 0;
        }

        private void ApplyTargetRelation(DuelEvent duelEvent, bool add)
        {
            CardInstanceState source = InstanceFor(duelEvent.Previous);
            CardInstanceState target = InstanceFor(duelEvent.Current);
            if (source == null || target == null)
                return;
            if (add)
                source.AddTarget(target.RuntimeId);
            else
                source.RemoveTarget(target.RuntimeId);
        }

        private void ApplyGeneralRelation(DuelEvent duelEvent, bool add)
        {
            CardInstanceState source = InstanceFor(duelEvent.Previous);
            CardInstanceState target = InstanceFor(duelEvent.Current);
            if (source == null || target == null)
                return;
            if (add)
                source.AddRelation(target.RuntimeId);
            else
                source.RemoveRelation(target.RuntimeId);
        }

        private void ApplyTemporaryTargets(DuelEvent duelEvent)
        {
            ClearTemporaryTargets();
            foreach (CardLocation location in
                     duelEvent.CurrentLocations ??
                     Array.Empty<CardLocation>())
            {
                CardInstanceState instance = InstanceFor(location);
                if (instance != null)
                    instance.IsTemporaryTarget = true;
            }
        }

        private void ClearTemporaryTargets()
        {
            foreach (CardInstanceState instance in AllInstances())
                instance.IsTemporaryTarget = false;
        }

        private void ApplyCardHint(DuelEvent duelEvent)
        {
            CardInstanceState instance = InstanceFor(duelEvent.Current);
            instance?.SetHint(
                unchecked((byte)duelEvent.Code),
                duelEvent.HintValue);
        }

        private CardInstanceState InstanceFor(CardLocation location)
        {
            return location == null
                ? null
                : InstanceAt(
                    location.Controller,
                    location.Location,
                    location.Sequence,
                    location.Position);
        }

        private CardInstanceState Remove(CardLocation location, uint code)
        {
            if (location.Controller >= Players.Length)
                return null;
            DuelistState player = Players[location.Controller];
            if ((location.Location & DuelLocation.Overlay) != 0)
            {
                return RemoveOverlay(
                    player,
                    code,
                    location.Sequence,
                    location.Position);
            }
            if ((location.Location & DuelLocation.Deck) != 0)
            {
                bool materialized = HasMaterializedPile(
                    player.DeckContents,
                    player.DeckCount);
                CardInstanceState instance = materialized
                    ? RemoveListCard(
                        player.DeckContents,
                        player.DeckInstances,
                        code,
                        location.Sequence,
                        location.Controller,
                        (byte)DuelLocation.Deck)
                    : null;
                player.DeckCount = materialized
                    ? player.DeckContents.Count
                    : Math.Max(0, player.DeckCount - 1);
                return instance;
            }
            if ((location.Location & DuelLocation.Extra) != 0)
            {
                bool materialized = HasMaterializedPile(
                    player.ExtraDeckContents,
                    player.ExtraDeckCount);
                CardInstanceState instance = materialized
                    ? RemoveListCard(
                        player.ExtraDeckContents,
                        player.ExtraDeckInstances,
                        code,
                        location.Sequence,
                        location.Controller,
                        (byte)DuelLocation.Extra)
                    : null;
                player.ExtraDeckCount = materialized
                    ? player.ExtraDeckContents.Count
                    : Math.Max(0, player.ExtraDeckCount - 1);
                return instance;
            }
            if ((location.Location & DuelLocation.Hand) != 0)
            {
                return RemoveListCard(
                    player.Hand,
                    player.HandInstances,
                    code,
                    location.Sequence,
                    location.Controller,
                    (byte)DuelLocation.Hand);
            }
            else if ((location.Location & DuelLocation.MonsterZone) != 0)
            {
                CardInstanceState instance =
                    TakeZone(player.MonsterInstances, location.Sequence);
                SetZone(player.MonsterZones, location.Sequence, 0);
                SetZone(player.MonsterPositions, location.Sequence, 0);
                return instance;
            }
            else if ((location.Location & DuelLocation.SpellTrapZone) != 0)
            {
                CardInstanceState instance =
                    TakeZone(player.SpellTrapInstances, location.Sequence);
                SetZone(player.SpellTrapZones, location.Sequence, 0);
                SetZone(player.SpellTrapPositions, location.Sequence, 0);
                return instance;
            }
            else if ((location.Location & DuelLocation.Graveyard) != 0)
            {
                return RemoveListCard(
                    player.Graveyard,
                    player.GraveyardInstances,
                    code,
                    location.Sequence,
                    location.Controller,
                    (byte)DuelLocation.Graveyard);
            }
            else if ((location.Location & DuelLocation.Banished) != 0)
            {
                return RemoveListCard(
                    player.Banished,
                    player.BanishedInstances,
                    code,
                    location.Sequence,
                    location.Controller,
                    (byte)DuelLocation.Banished);
            }
            return null;
        }

        private void Add(
            CardLocation location,
            uint code,
            CardInstanceState instance,
            byte originalOwner)
        {
            if (location.Controller >= Players.Length)
                return;
            DuelistState player = Players[location.Controller];
            if ((location.Location & DuelLocation.Deck) != 0)
            {
                bool materialized = HasMaterializedPile(
                    player.DeckContents,
                    player.DeckCount);
                if (materialized)
                {
                    InsertListCard(
                        player.DeckContents,
                        player.DeckInstances,
                        location,
                        code,
                        instance,
                        originalOwner);
                    player.DeckCount = player.DeckContents.Count;
                }
                else
                {
                    // A remote/private pile intentionally has no per-card
                    // list. Preserve that privacy boundary and update only
                    // the authoritative count published by the Core.
                    player.DeckCount++;
                }
                return;
            }
            if ((location.Location & DuelLocation.Extra) != 0)
            {
                bool materialized = HasMaterializedPile(
                    player.ExtraDeckContents,
                    player.ExtraDeckCount);
                if (materialized)
                {
                    InsertListCard(
                        player.ExtraDeckContents,
                        player.ExtraDeckInstances,
                        location,
                        code,
                        instance,
                        originalOwner);
                    player.ExtraDeckCount =
                        player.ExtraDeckContents.Count;
                }
                else
                {
                    player.ExtraDeckCount++;
                }
                return;
            }
            instance ??= CreateInstance(
                code,
                originalOwner,
                location.Controller,
                location.Location,
                location.Sequence,
                location.Position);
            bool preserveOpaqueIdentity =
                instance.IdentityOpaque &&
                IsFacedownFieldAddress(location.Location, location.Position);
            if (preserveOpaqueIdentity)
            {
                code = 0;
                instance.DefinitionCode = 0;
            }
            else
            {
                instance.IdentityOpaque = false;
            }
            if (code != 0)
                instance.DefinitionCode = code;
            instance.UpdateAddress(
                location.Controller,
                location.Location,
                location.Sequence,
                location.Position);
            if ((location.Location & DuelLocation.Overlay) != 0)
            {
                AddOverlay(player, instance, location);
            }
            else if ((location.Location & DuelLocation.Hand) != 0)
            {
                int requestedIndex = location.Sequence > int.MaxValue
                    ? player.Hand.Count
                    : (int)location.Sequence;
                int index = Math.Min(
                    player.Hand.Count,
                    requestedIndex);
                player.Hand.Insert(index, code);
                player.HandInstances.Insert(index, instance);
                Reindex(
                    player.HandInstances,
                    location.Controller,
                    (byte)DuelLocation.Hand);
            }
            else if ((location.Location & DuelLocation.MonsterZone) != 0)
            {
                SetZone(player.MonsterZones, location.Sequence, code);
                SetZone(
                    player.MonsterPositions,
                    location.Sequence,
                    location.Position);
                SetZoneInstance(
                    player.MonsterInstances,
                    location.Sequence,
                    instance);
            }
            else if ((location.Location & DuelLocation.SpellTrapZone) != 0)
            {
                SetZone(player.SpellTrapZones, location.Sequence, code);
                SetZone(
                    player.SpellTrapPositions,
                    location.Sequence,
                    location.Position);
                SetZoneInstance(
                    player.SpellTrapInstances,
                    location.Sequence,
                    instance);
            }
            else if ((location.Location & DuelLocation.Graveyard) != 0)
            {
                player.Graveyard.Add(code);
                player.GraveyardInstances.Add(instance);
                Reindex(
                    player.GraveyardInstances,
                    location.Controller,
                    (byte)DuelLocation.Graveyard);
            }
            else if ((location.Location & DuelLocation.Banished) != 0)
            {
                player.Banished.Add(code);
                player.BanishedInstances.Add(instance);
                Reindex(
                    player.BanishedInstances,
                    location.Controller,
                    (byte)DuelLocation.Banished);
            }
        }

        private static void SetZone(uint[] zones, uint sequence, uint value)
        {
            if (sequence < zones.Length) zones[sequence] = value;
        }

        private static bool HasMaterializedPile(
            IReadOnlyCollection<uint> contents,
            int publishedCount)
        {
            return contents != null &&
                   contents.Count == Math.Max(0, publishedCount);
        }

        private void InsertListCard(
            List<uint> codes,
            List<CardInstanceState> instances,
            CardLocation location,
            uint code,
            CardInstanceState instance,
            byte originalOwner)
        {
            int requested = location.Sequence > int.MaxValue
                ? codes.Count
                : (int)location.Sequence;
            int index = Math.Min(codes.Count, requested);
            instance ??= CreateInstance(
                code,
                originalOwner,
                location.Controller,
                location.Location,
                (uint)index,
                location.Position);
            if (code != 0)
                instance.DefinitionCode = code;
            instance.IdentityOpaque = false;
            instance.UpdateAddress(
                location.Controller,
                location.Location,
                (uint)index,
                location.Position);
            codes.Insert(index, code);
            instances.Insert(index, instance);
            Reindex(instances, location.Controller, location.Location);
        }

        private void ReconcileList(
            List<uint> codes,
            List<CardInstanceState> instances,
            OcgFieldCardSnapshot[] source,
            byte controller,
            byte location)
        {
            source ??= Array.Empty<OcgFieldCardSnapshot>();
            var pool = instances
                .Where(instance => instance != null)
                .ToList();
            var previous = instances.ToArray();
            codes.Clear();
            instances.Clear();
            for (int index = 0; index < source.Length; index++)
            {
                OcgFieldCardSnapshot card = source[index];
                if (card == null)
                    continue;
                CardInstanceState preferred = index < previous.Length
                    ? previous[index]
                    : null;
                CardInstanceState instance = TakeReusableInstance(
                    pool,
                    preferred,
                    card.Code,
                    card.Owner);
                instance ??= CreateInstance(
                    card.Code,
                    card.Owner,
                    controller,
                    location,
                    (uint)instances.Count,
                    card.Position);
                instance.DefinitionCode = card.Code;
                instance.UpdateAddress(
                    controller,
                    location,
                    (uint)instances.Count,
                    card.Position);
                codes.Add(card.Code);
                instances.Add(instance);
            }
        }

        private void ReconcileZones(
            uint[] codes,
            uint[] positions,
            CardInstanceState[] instances,
            OcgFieldCardSnapshot[] source,
            byte controller,
            byte location)
        {
            source ??= Array.Empty<OcgFieldCardSnapshot>();
            var previous = (CardInstanceState[])instances.Clone();
            var pool = previous
                .Where(instance => instance != null)
                .ToList();
            Array.Clear(codes, 0, codes.Length);
            Array.Clear(positions, 0, positions.Length);
            Array.Clear(instances, 0, instances.Length);
            int count = Math.Min(instances.Length, source.Length);
            for (int index = 0; index < count; index++)
            {
                OcgFieldCardSnapshot card = source[index];
                if (card == null)
                    continue;
                CardInstanceState preferred = previous[index];
                if (preferred != null && preferred.IdentityOpaque &&
                    !IsFaceUpPosition(card.Position))
                {
                    pool.Remove(preferred);
                    preferred.DefinitionCode = 0;
                    preferred.UpdateAddress(
                        controller,
                        location,
                        (uint)index,
                        card.Position);
                    codes[index] = 0;
                    positions[index] = card.Position;
                    instances[index] = preferred;
                    continue;
                }
                CardInstanceState instance = TakeReusableInstance(
                    pool,
                    preferred,
                    card.Code,
                    card.Owner);
                instance ??= CreateInstance(
                    card.Code,
                    card.Owner,
                    controller,
                    location,
                    (uint)index,
                    card.Position);
                instance.DefinitionCode = card.Code;
                instance.IdentityOpaque = false;
                instance.UpdateAddress(
                    controller,
                    location,
                    (uint)index,
                    card.Position);
                codes[index] = card.Code;
                positions[index] = card.Position;
                instances[index] = instance;
            }
        }

        private void ReconcileOverlays(
            DuelistState player,
            OcgFieldCardSnapshot[] monsters,
            byte controller)
        {
            monsters ??= Array.Empty<OcgFieldCardSnapshot>();
            CardInstanceState[][] previous = player.OverlayInstances
                .Select(materials => materials.ToArray())
                .ToArray();
            var pool = previous
                .SelectMany(materials => materials)
                .Where(material => material != null)
                .ToList();
            for (int zone = 0; zone < player.OverlayInstances.Length; zone++)
            {
                List<CardInstanceState> destination =
                    player.OverlayInstances[zone];
                destination.Clear();
                uint[] codes = zone < monsters.Length
                    ? monsters[zone]?.OverlayCodes ?? Array.Empty<uint>()
                    : Array.Empty<uint>();
                for (int index = 0; index < codes.Length; index++)
                {
                    CardInstanceState preferred = zone < previous.Length &&
                        index < previous[zone].Length
                            ? previous[zone][index]
                            : null;
                    CardInstanceState material = TakeReusableInstance(
                        pool,
                        preferred,
                        codes[index],
                        preferred?.Owner ?? controller,
                        false);
                    material ??= CreateInstance(
                        codes[index],
                        controller,
                        controller,
                        (byte)DuelLocation.Overlay,
                        (uint)zone,
                        (uint)index);
                    material.DefinitionCode = codes[index];
                    material.UpdateAddress(
                        controller,
                        (byte)DuelLocation.Overlay,
                        (uint)zone,
                        (uint)index);
                    destination.Add(material);
                }
            }
        }

        private void ReconcilePersistentMetadata(OcgFieldSnapshot snapshot)
        {
            for (byte controller = 0;
                 controller < snapshot.Players.Length;
                 controller++)
            {
                OcgDuelistFieldSnapshot player =
                    snapshot.Players[controller];
                ReconcileMetadataArray(
                    controller,
                    (byte)DuelLocation.Deck,
                    player.Deck);
                ReconcileMetadataArray(
                    controller,
                    (byte)DuelLocation.Hand,
                    player.Hand);
                ReconcileMetadataArray(
                    controller,
                    (byte)DuelLocation.MonsterZone,
                    player.Monsters);
                ReconcileMetadataArray(
                    controller,
                    (byte)DuelLocation.SpellTrapZone,
                    player.Spells);
                ReconcileMetadataArray(
                    controller,
                    (byte)DuelLocation.Graveyard,
                    player.Graveyard);
                ReconcileMetadataArray(
                    controller,
                    (byte)DuelLocation.Banished,
                    player.Banished);
                ReconcileMetadataArray(
                    controller,
                    (byte)DuelLocation.Extra,
                    player.Extra);
            }
        }

        private void ReconcileMetadataArray(
            byte controller,
            byte location,
            IReadOnlyList<OcgFieldCardSnapshot> cards)
        {
            if (cards == null)
                return;
            for (int index = 0; index < cards.Count; index++)
            {
                OcgFieldCardSnapshot source = cards[index];
                if (source == null)
                    continue;
                CardInstanceState instance = InstanceAt(
                    controller,
                    location,
                    (uint)index);
                if (instance == null)
                    continue;
                int count = Math.Min(
                    source.CounterTypes?.Length ?? 0,
                    source.CounterAmounts?.Length ?? 0);
                var counters = new List<KeyValuePair<ushort, uint>>(count);
                for (int counter = 0; counter < count; counter++)
                {
                    counters.Add(new KeyValuePair<ushort, uint>(
                        source.CounterTypes[counter],
                        source.CounterAmounts[counter]));
                }
                instance.ReplaceCounters(counters);
                instance.CoreStatus = source.Status;
                instance.IsPublic = source.IsPublic;
                instance.LinkRating = source.LinkRating;
                instance.LinkMarkers = source.LinkMarkers;
                instance.EquippedToRuntimeId =
                    InstanceFor(source.EquipTarget)?.RuntimeId ?? 0UL;
                instance.ReplaceTargets(
                    (source.TargetCards ?? Array.Empty<CardLocation>())
                    .Select(InstanceFor)
                    .Where(target => target != null)
                    .Select(target => target.RuntimeId));
            }
        }

        private static CardInstanceState TakeReusableInstance(
            List<CardInstanceState> pool,
            CardInstanceState preferred,
            uint code,
            byte owner,
            bool requireOwner = true)
        {
            if (preferred != null && preferred.DefinitionCode == code &&
                (!requireOwner || preferred.Owner == owner) &&
                pool.Remove(preferred))
            {
                return preferred;
            }
            int index = pool.FindIndex(instance =>
                instance != null && instance.DefinitionCode == code &&
                (!requireOwner || instance.Owner == owner));
            if (index < 0)
                return null;
            CardInstanceState result = pool[index];
            pool.RemoveAt(index);
            return result;
        }

        private static void ReplaceKnownContents(
            List<uint> destination,
            OcgFieldCardSnapshot[] source)
        {
            destination.Clear();
            foreach (OcgFieldCardSnapshot card in
                     source ?? Array.Empty<OcgFieldCardSnapshot>())
            {
                destination.Add(card?.Code ?? 0U);
            }
        }

        private void ApplyPositionChange(DuelEvent duelEvent)
        {
            CardLocation current = duelEvent.Current;
            if (current == null ||
                current.Controller >= Players.Length)
            {
                return;
            }
            DuelistState player = Players[current.Controller];
            CardInstanceState positioned = InstanceAt(
                current.Controller,
                current.Location,
                current.Sequence);
            if (positioned != null && IsFaceUpPosition(current.Position))
                positioned.IdentityOpaque = false;
            if ((current.Location & DuelLocation.MonsterZone) != 0)
            {
                if (duelEvent.Code != 0 &&
                    (positioned == null || !positioned.IdentityOpaque))
                {
                    SetZone(
                        player.MonsterZones,
                        current.Sequence,
                        duelEvent.Code);
                    SetZoneDefinition(
                        player.MonsterInstances,
                        current.Sequence,
                        duelEvent.Code);
                }
                SetZone(
                    player.MonsterPositions,
                    current.Sequence,
                    current.Position);
                UpdateZonePosition(
                    player.MonsterInstances,
                    current.Sequence,
                    current.Position);
            }
            else if ((current.Location & DuelLocation.SpellTrapZone) != 0)
            {
                if (duelEvent.Code != 0 &&
                    (positioned == null || !positioned.IdentityOpaque))
                {
                    SetZone(
                        player.SpellTrapZones,
                        current.Sequence,
                        duelEvent.Code);
                    SetZoneDefinition(
                        player.SpellTrapInstances,
                        current.Sequence,
                        duelEvent.Code);
                }
                SetZone(
                    player.SpellTrapPositions,
                    current.Sequence,
                    current.Position);
                UpdateZonePosition(
                    player.SpellTrapInstances,
                    current.Sequence,
                    current.Position);
            }
        }

        private static void SetZoneDefinition(
            CardInstanceState[] instances,
            uint sequence,
            uint code)
        {
            if (instances == null || sequence >= instances.Length ||
                instances[sequence] == null)
            {
                return;
            }
            instances[sequence].DefinitionCode = code;
        }

        public CardInstanceState InstanceAt(
            byte controller,
            byte location,
            uint sequence,
            uint overlaySequence = uint.MaxValue)
        {
            if (controller >= Players.Length)
                return null;
            DuelistState player = Players[controller];
            if ((location & DuelLocation.Deck) != 0)
                return sequence < player.DeckInstances.Count
                    ? player.DeckInstances[(int)sequence]
                    : null;
            if ((location & DuelLocation.Extra) != 0)
                return sequence < player.ExtraDeckInstances.Count
                    ? player.ExtraDeckInstances[(int)sequence]
                    : null;
            if ((location & DuelLocation.Overlay) != 0)
            {
                if (sequence >= player.OverlayInstances.Length)
                    return null;
                List<CardInstanceState> materials =
                    player.OverlayInstances[sequence];
                if (overlaySequence < materials.Count)
                    return materials[(int)overlaySequence];
                return materials.Count > 0
                    ? materials[materials.Count - 1]
                    : null;
            }
            if ((location & DuelLocation.Hand) != 0)
                return sequence < player.HandInstances.Count
                    ? player.HandInstances[(int)sequence]
                    : null;
            if ((location & DuelLocation.MonsterZone) != 0)
                return sequence < player.MonsterInstances.Length
                    ? player.MonsterInstances[sequence]
                    : null;
            if ((location & DuelLocation.SpellTrapZone) != 0)
                return sequence < player.SpellTrapInstances.Length
                    ? player.SpellTrapInstances[sequence]
                    : null;
            if ((location & DuelLocation.Graveyard) != 0)
                return sequence < player.GraveyardInstances.Count
                    ? player.GraveyardInstances[(int)sequence]
                    : null;
            if ((location & DuelLocation.Banished) != 0)
                return sequence < player.BanishedInstances.Count
                    ? player.BanishedInstances[(int)sequence]
                    : null;
            return null;
        }

        /// <summary>
        /// Binds every Core candidate to the stable physical instance at the
        /// same authoritative address. CandidateIndex/response bytes remain
        /// the rule authority; RuntimeId prevents equal printed cards from
        /// collapsing into one visual or network choice.
        /// </summary>
        public void BindPromptInstances(DuelPrompt prompt)
        {
            if (prompt == null)
                return;
            foreach (DuelChoice choice in prompt.Choices)
            {
                if (choice == null || !choice.HasLocation ||
                    choice.Controller >= Players.Length)
                {
                    continue;
                }
                CardInstanceState instance = InstanceAt(
                    choice.Controller,
                    choice.Location,
                    choice.Sequence,
                    choice.Position);
                if (instance != null)
                    choice.RuntimeId = instance.RuntimeId;
            }
        }

        public string[] ValidateInstanceConsistency()
        {
            var problems = new List<string>(transitionConsistencyProblems);
            var locationsByRuntimeId = new Dictionary<ulong, string>();
            for (byte controller = 0; controller < Players.Length; controller++)
            {
                DuelistState player = Players[controller];
                ValidateList(
                    problems,
                    locationsByRuntimeId,
                    controller,
                    "deck",
                    (byte)DuelLocation.Deck,
                    player.DeckContents,
                    player.DeckInstances);
                ValidateList(
                    problems,
                    locationsByRuntimeId,
                    controller,
                    "extra",
                    (byte)DuelLocation.Extra,
                    player.ExtraDeckContents,
                    player.ExtraDeckInstances);
                ValidateList(
                    problems,
                    locationsByRuntimeId,
                    controller,
                    "hand",
                    (byte)DuelLocation.Hand,
                    player.Hand,
                    player.HandInstances);
                ValidateList(
                    problems,
                    locationsByRuntimeId,
                    controller,
                    "graveyard",
                    (byte)DuelLocation.Graveyard,
                    player.Graveyard,
                    player.GraveyardInstances);
                ValidateList(
                    problems,
                    locationsByRuntimeId,
                    controller,
                    "banished",
                    (byte)DuelLocation.Banished,
                    player.Banished,
                    player.BanishedInstances);
                ValidateZones(
                    problems,
                    locationsByRuntimeId,
                    controller,
                    "monster",
                    (byte)DuelLocation.MonsterZone,
                    player.MonsterZones,
                    player.MonsterInstances);
                ValidateZones(
                    problems,
                    locationsByRuntimeId,
                    controller,
                    "spell/trap",
                    (byte)DuelLocation.SpellTrapZone,
                    player.SpellTrapZones,
                    player.SpellTrapInstances);
                for (int zone = 0;
                     zone < player.OverlayInstances.Length;
                     zone++)
                {
                    IReadOnlyList<CardInstanceState> materials =
                        player.OverlayInstances[zone];
                    for (int materialIndex = 0;
                         materialIndex < materials.Count;
                         materialIndex++)
                    {
                        CardInstanceState material = materials[materialIndex];
                        if (material == null ||
                            (material.Location & DuelLocation.Overlay) == 0 ||
                            material.Controller != controller ||
                            material.Sequence != zone ||
                            material.Position != (uint)materialIndex)
                        {
                            problems.Add(
                                $"P{controller} overlay[{zone}] contains " +
                                "an invalid material binding.");
                        }
                        RegisterInstance(
                            problems,
                            locationsByRuntimeId,
                            material,
                            $"P{controller} overlay[{zone}][{materialIndex}]");
                    }
                }
            }
            return problems.ToArray();
        }

        private bool IsIdentityMaterialized(CardLocation location)
        {
            if (!IsLocated(location) || location.Controller >= Players.Length)
                return false;
            DuelistState player = Players[location.Controller];
            if ((location.Location & DuelLocation.Deck) != 0)
            {
                return HasMaterializedPile(
                    player.DeckContents,
                    player.DeckCount);
            }
            if ((location.Location & DuelLocation.Extra) != 0)
            {
                return HasMaterializedPile(
                    player.ExtraDeckContents,
                    player.ExtraDeckCount);
            }
            return true;
        }

        private CardInstanceState CreateInstance(
            uint code,
            byte owner,
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            return new CardInstanceState(
                nextRuntimeId++,
                code,
                owner,
                controller,
                location,
                sequence,
                position);
        }

        private CardInstanceState CreateInstanceWithRuntimeId(
            ulong runtimeId,
            uint code,
            byte owner,
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            if (runtimeId == 0)
            {
                return CreateInstance(
                    code,
                    owner,
                    controller,
                    location,
                    sequence,
                    position);
            }
            // Network replicas use high-range opaque tokens for private
            // slots. They must not move the allocator into that reserved
            // range; authoritative Core instance ids remain low and monotonic.
            if (runtimeId < 0x1000000000000000UL &&
                runtimeId >= nextRuntimeId)
                nextRuntimeId = runtimeId + 1;
            return new CardInstanceState(
                runtimeId,
                code,
                owner,
                controller,
                location,
                sequence,
                position);
        }

        private static CardInstanceState RemoveListCard(
            List<uint> cards,
            List<CardInstanceState> instances,
            uint code,
            uint sequence,
            byte controller,
            byte location)
        {
            int index = -1;
            if (sequence < cards.Count &&
                (code == 0 || cards[(int)sequence] == code))
            {
                index = (int)sequence;
            }
            if (index < 0 && code != 0)
                index = cards.IndexOf(code);
            if (index < 0 || index >= cards.Count)
                return null;

            cards.RemoveAt(index);
            CardInstanceState instance = index < instances.Count
                ? instances[index]
                : null;
            if (index < instances.Count)
                instances.RemoveAt(index);
            Reindex(instances, controller, location);
            return instance;
        }

        private static CardInstanceState TakeZone(
            CardInstanceState[] instances,
            uint sequence)
        {
            if (sequence >= instances.Length)
                return null;
            CardInstanceState result = instances[sequence];
            instances[sequence] = null;
            return result;
        }

        private List<CardInstanceState> TakeOverlayStack(
            CardLocation host)
        {
            if (!IsMonsterZone(host) || host.Controller >= Players.Length ||
                host.Sequence >=
                    Players[host.Controller].OverlayInstances.Length)
            {
                return null;
            }
            List<CardInstanceState> source =
                Players[host.Controller].OverlayInstances[host.Sequence];
            var result = new List<CardInstanceState>(source);
            source.Clear();
            return result;
        }

        private void PlaceOverlayStack(
            CardLocation host,
            List<CardInstanceState> materials)
        {
            if (!IsMonsterZone(host) || materials == null ||
                host.Controller >= Players.Length ||
                host.Sequence >=
                    Players[host.Controller].OverlayInstances.Length)
            {
                return;
            }
            List<CardInstanceState> destination =
                Players[host.Controller].OverlayInstances[host.Sequence];
            destination.Clear();
            destination.AddRange(materials);
            for (int index = 0; index < destination.Count; index++)
            {
                destination[index]?.UpdateAddress(
                    host.Controller,
                    (byte)DuelLocation.Overlay,
                    host.Sequence,
                    (uint)index);
            }
        }

        private static bool IsMonsterZone(CardLocation location)
        {
            return IsLocated(location) &&
                   (location.Location & DuelLocation.MonsterZone) != 0;
        }

        private static CardInstanceState RemoveOverlay(
            DuelistState player,
            uint code,
            uint hostSequence,
            uint overlaySequence)
        {
            if (hostSequence >= player.OverlayInstances.Length)
                return null;
            List<CardInstanceState> materials =
                player.OverlayInstances[hostSequence];
            int index = overlaySequence < materials.Count &&
                        (code == 0 ||
                         materials[(int)overlaySequence]
                             .DefinitionCode == code)
                ? (int)overlaySequence
                : materials.FindIndex(material =>
                    material != null &&
                    material.DefinitionCode == code);
            if (index < 0)
                return null;
            CardInstanceState result = materials[index];
            materials.RemoveAt(index);
            for (int materialIndex = 0;
                 materialIndex < materials.Count;
                 materialIndex++)
            {
                CardInstanceState material = materials[materialIndex];
                material?.UpdateAddress(
                    material.Controller,
                    (byte)DuelLocation.Overlay,
                    hostSequence,
                    (uint)materialIndex);
            }
            return result;
        }

        private static void AddOverlay(
            DuelistState player,
            CardInstanceState instance,
            CardLocation location)
        {
            if (location.Sequence >= player.OverlayInstances.Length)
                return;
            List<CardInstanceState> materials =
                player.OverlayInstances[location.Sequence];
            int index = location.Position <= materials.Count
                ? (int)location.Position
                : materials.Count;
            materials.Insert(index, instance);
            for (int materialIndex = 0;
                 materialIndex < materials.Count;
                 materialIndex++)
            {
                CardInstanceState material = materials[materialIndex];
                material?.UpdateAddress(
                    location.Controller,
                    (byte)DuelLocation.Overlay,
                    location.Sequence,
                    (uint)materialIndex);
            }
        }

        private static void SetZoneInstance(
            CardInstanceState[] instances,
            uint sequence,
            CardInstanceState instance)
        {
            if (sequence < instances.Length)
                instances[sequence] = instance;
        }

        private static void UpdateZonePosition(
            CardInstanceState[] instances,
            uint sequence,
            uint position)
        {
            if (sequence >= instances.Length ||
                instances[sequence] == null)
            {
                return;
            }
            CardInstanceState instance = instances[sequence];
            instance.UpdateAddress(
                instance.Controller,
                instance.Location,
                sequence,
                position);
        }

        private static void Reindex(
            List<CardInstanceState> instances,
            byte controller,
            byte location)
        {
            for (int index = 0; index < instances.Count; index++)
            {
                CardInstanceState instance = instances[index];
                instance?.UpdateAddress(
                    controller,
                    location,
                    (uint)index,
                    instance.Position);
            }
        }

        private static void ValidateList(
            ICollection<string> problems,
            IDictionary<ulong, string> locationsByRuntimeId,
            byte controller,
            string label,
            byte expectedLocation,
            IReadOnlyList<uint> codes,
            IReadOnlyList<CardInstanceState> instances)
        {
            if (codes.Count != instances.Count)
                problems.Add(
                    $"P{controller} {label}: {codes.Count} codes != " +
                    $"{instances.Count} instances.");
            int count = Math.Min(codes.Count, instances.Count);
            for (int index = 0; index < count; index++)
            {
                CardInstanceState instance = instances[index];
                string address = $"P{controller} {label}[{index}]";
                if (instance == null ||
                    instance.DefinitionCode != codes[index])
                {
                    problems.Add(
                        $"{address} is not bound " +
                        $"to definition {codes[index]:00000000}.");
                }
                ValidateAddress(
                    problems,
                    instance,
                    controller,
                    expectedLocation,
                    index,
                    address);
                RegisterInstance(
                    problems,
                    locationsByRuntimeId,
                    instance,
                    address);
            }
        }

        private static void ValidateZones(
            ICollection<string> problems,
            IDictionary<ulong, string> locationsByRuntimeId,
            byte controller,
            string label,
            byte expectedLocation,
            IReadOnlyList<uint> codes,
            IReadOnlyList<CardInstanceState> instances)
        {
            int count = Math.Min(codes.Count, instances.Count);
            for (int index = 0; index < count; index++)
            {
                CardInstanceState instance = instances[index];
                if (instance == null && codes[index] != 0 ||
                    instance != null &&
                    instance.DefinitionCode != codes[index])
                {
                    problems.Add(
                        $"P{controller} {label}[{index}] presentation " +
                        "instance does not match the authoritative code.");
                }
                if (instance == null)
                    continue;
                string address = $"P{controller} {label}[{index}]";
                ValidateAddress(
                    problems,
                    instance,
                    controller,
                    expectedLocation,
                    index,
                    address);
                RegisterInstance(
                    problems,
                    locationsByRuntimeId,
                    instance,
                    address);
            }
        }

        private static void ValidateAddress(
            ICollection<string> problems,
            CardInstanceState instance,
            byte controller,
            byte expectedLocation,
            int expectedSequence,
            string address)
        {
            if (instance == null)
                return;
            if (instance.RuntimeId == 0)
                problems.Add($"{address} has no physical runtime identity.");
            if (instance.Controller != controller ||
                (instance.Location & expectedLocation) == 0 ||
                instance.Sequence != (uint)expectedSequence)
            {
                problems.Add(
                    $"{address} stores runtime {instance.RuntimeId}, but its " +
                    $"Core address is P{instance.Controller}/" +
                    $"0x{instance.Location:X2}/{instance.Sequence}.");
            }
        }

        private static void RegisterInstance(
            ICollection<string> problems,
            IDictionary<ulong, string> locationsByRuntimeId,
            CardInstanceState instance,
            string address)
        {
            if (instance == null || instance.RuntimeId == 0)
                return;
            if (locationsByRuntimeId.TryGetValue(
                    instance.RuntimeId,
                    out string previousAddress))
            {
                problems.Add(
                    $"Physical runtime {instance.RuntimeId} is present in " +
                    $"both {previousAddress} and {address}.");
                return;
            }
            locationsByRuntimeId[instance.RuntimeId] = address;
        }

        private uint CodeAt(CardLocation location)
        {
            if (!IsLocated(location) || location.Controller >= Players.Length)
                return 0;
            DuelistState player = Players[location.Controller];
            int sequence = location.Sequence > int.MaxValue
                ? -1
                : (int)location.Sequence;
            if ((location.Location & DuelLocation.Deck) != 0)
            {
                return sequence >= 0 && sequence < player.DeckContents.Count
                    ? player.DeckContents[sequence]
                    : 0;
            }
            if ((location.Location & DuelLocation.Extra) != 0)
            {
                return sequence >= 0 &&
                       sequence < player.ExtraDeckContents.Count
                    ? player.ExtraDeckContents[sequence]
                    : 0;
            }
            if ((location.Location & DuelLocation.Hand) != 0)
                return At(player.Hand, sequence);
            if ((location.Location & DuelLocation.MonsterZone) != 0)
                return At(player.MonsterZones, sequence);
            if ((location.Location & DuelLocation.SpellTrapZone) != 0)
                return At(player.SpellTrapZones, sequence);
            if ((location.Location & DuelLocation.Graveyard) != 0)
                return At(player.Graveyard, sequence);
            if ((location.Location & DuelLocation.Banished) != 0)
                return At(player.Banished, sequence);
            if ((location.Location & DuelLocation.Overlay) != 0 &&
                sequence >= 0 && sequence < player.OverlayInstances.Length)
            {
                int overlay = location.Position > int.MaxValue
                    ? -1
                    : (int)location.Position;
                return overlay >= 0 &&
                       overlay < player.OverlayInstances[sequence].Count
                    ? player.OverlayInstances[sequence][overlay]
                        ?.DefinitionCode ?? 0
                    : 0;
            }
            return 0;
        }

        private static uint At(IReadOnlyList<uint> values, int index)
        {
            return values != null && index >= 0 && index < values.Count
                ? values[index]
                : 0;
        }

        private static bool IsLocated(CardLocation location)
        {
            return location != null && location.Location != 0;
        }

        private static bool SameAddress(
            CardLocation left,
            CardLocation right)
        {
            return left != null && right != null &&
                   left.Controller == right.Controller &&
                   left.Location == right.Location &&
                   left.Sequence == right.Sequence;
        }

        private static bool BitIsSet(uint[] bytes, int bit)
        {
            if (bytes == null || bit < 0)
                return false;
            int index = bit / 8;
            return index < bytes.Length &&
                   ((byte)bytes[index] & (1 << (bit % 8))) != 0;
        }

        private static int CountSetBits(uint[] bytes, int bitCount)
        {
            int result = 0;
            for (int bit = 0; bit < Math.Max(0, bitCount); bit++)
            {
                if (BitIsSet(bytes, bit))
                    result++;
            }
            return result;
        }

        private void ConfigureDeckContents(
            DuelistState player,
            byte controller,
            IEnumerable<uint> main,
            IEnumerable<uint> extra)
        {
            player.DeckContents.Clear();
            player.DeckInstances.Clear();
            foreach (uint code in
                     (main ?? Array.Empty<uint>()).Where(code => code != 0))
            {
                player.DeckContents.Add(code);
                player.DeckInstances.Add(CreateInstance(
                    code,
                    controller,
                    controller,
                    (byte)DuelLocation.Deck,
                    (uint)player.DeckInstances.Count,
                    0));
            }
            player.ExtraDeckContents.Clear();
            player.ExtraDeckInstances.Clear();
            foreach (uint code in
                     (extra ?? Array.Empty<uint>()).Where(code => code != 0))
            {
                player.ExtraDeckContents.Add(code);
                player.ExtraDeckInstances.Add(CreateInstance(
                    code,
                    controller,
                    controller,
                    (byte)DuelLocation.Extra,
                    (uint)player.ExtraDeckInstances.Count,
                    0));
            }
            player.DeckCount = player.DeckContents.Count;
            player.ExtraDeckCount = player.ExtraDeckContents.Count;
        }

        private static void RemoveKnownCard(List<uint> cards, uint code)
        {
            if (cards == null || cards.Count == 0)
                return;
            int index = code == 0 ? cards.Count - 1 : cards.IndexOf(code);
            if (index < 0)
                index = cards.Count - 1;
            cards.RemoveAt(index);
        }

        private string Name(uint code)
        {
            return code != 0 && database != null &&
                   database.TryGet(code, out CardRecord card)
                ? card.Name
                : "Carta oculta";
        }

        private static string LocationName(byte location)
        {
            if ((location & DuelLocation.Hand) != 0) return "Mão";
            if ((location & DuelLocation.MonsterZone) != 0) return "Zona de Monstro";
            if ((location & DuelLocation.SpellTrapZone) != 0) return "Zona de Magia/Armadilha";
            if ((location & DuelLocation.Graveyard) != 0) return "Cemitério";
            if ((location & DuelLocation.Banished) != 0) return "Banimento";
            if ((location & DuelLocation.Deck) != 0) return "Deck";
            if ((location & DuelLocation.Extra) != 0) return "Deck Adicional";
            return "Fora do campo";
        }

        private static bool IsFaceUpPosition(uint position)
        {
            return (position & 0x5U) != 0;
        }

        private static bool IsFacedownFieldAddress(
            byte location,
            uint position)
        {
            return (location &
                    (DuelLocation.MonsterZone |
                     DuelLocation.SpellTrapZone)) != 0 &&
                   !IsFaceUpPosition(position);
        }

        private void AddLog(string entry)
        {
            Log.Add(entry);
            while (Log.Count > 14) Log.RemoveAt(0);
        }

        private IEnumerable<CardInstanceState> AllInstances()
        {
            return Players
                .SelectMany(player =>
                    player.DeckInstances
                        .Concat(player.ExtraDeckInstances)
                        .Concat(player.HandInstances)
                        .Concat(player.MonsterInstances.Where(card =>
                            card != null))
                        .Concat(player.SpellTrapInstances.Where(card =>
                            card != null))
                        .Concat(player.GraveyardInstances)
                        .Concat(player.BanishedInstances)
                        .Concat(player.OverlayInstances.SelectMany(
                            materials => materials)))
                .Where(instance => instance != null &&
                                   instance.RuntimeId != 0)
                .GroupBy(instance => instance.RuntimeId)
                .Select(group => group.First());
        }

        private CardPresentationMetadataSnapshot[] CaptureCardMetadata()
        {
            return AllInstances()
                .Where(instance =>
                    instance.Counters.Count > 0 ||
                    instance.CoreStatus != 0 ||
                    instance.IsPublic ||
                    instance.LinkRating != 0 ||
                    instance.LinkMarkers != 0 ||
                    instance.EquippedToRuntimeId != 0 ||
                    instance.TargetRuntimeIds.Count > 0 ||
                    instance.RelationRuntimeIds.Count > 0 ||
                    instance.Hints.Count > 0 ||
                    instance.IsTemporaryTarget)
                .Select(instance =>
                {
                    KeyValuePair<ushort, uint>[] counters =
                        instance.Counters.OrderBy(item => item.Key).ToArray();
                    KeyValuePair<byte, ulong>[] hints =
                        instance.Hints.OrderBy(item => item.Key).ToArray();
                    return new CardPresentationMetadataSnapshot
                    {
                        RuntimeId = instance.RuntimeId,
                        CoreStatus = instance.CoreStatus,
                        IsPublic = instance.IsPublic,
                        LinkRating = instance.LinkRating,
                        LinkMarkers = instance.LinkMarkers,
                        CounterTypes = counters.Select(item => item.Key)
                            .ToArray(),
                        CounterAmounts = counters.Select(item => item.Value)
                            .ToArray(),
                        EquippedToRuntimeId = instance.EquippedToRuntimeId,
                        TargetRuntimeIds = instance.TargetRuntimeIds
                            .OrderBy(value => value)
                            .ToArray(),
                        RelationRuntimeIds = instance.RelationRuntimeIds
                            .OrderBy(value => value)
                            .ToArray(),
                        HintTypes = hints.Select(item => item.Key).ToArray(),
                        HintValues = hints.Select(item => item.Value).ToArray(),
                        IsTemporaryTarget = instance.IsTemporaryTarget
                    };
                })
                .ToArray();
        }

        private void RestoreCardMetadata(
            IEnumerable<CardPresentationMetadataSnapshot> metadata)
        {
            Dictionary<ulong, CardInstanceState> instances = AllInstances()
                .ToDictionary(instance => instance.RuntimeId);
            foreach (CardPresentationMetadataSnapshot item in
                     metadata ??
                     Array.Empty<CardPresentationMetadataSnapshot>())
            {
                if (item == null || item.RuntimeId == 0 ||
                    !instances.TryGetValue(
                        item.RuntimeId,
                        out CardInstanceState instance))
                {
                    continue;
                }
                int counterCount = Math.Min(
                    item.CounterTypes?.Length ?? 0,
                    item.CounterAmounts?.Length ?? 0);
                var counters = new List<KeyValuePair<ushort, uint>>(
                    counterCount);
                for (int index = 0; index < counterCount; index++)
                {
                    counters.Add(new KeyValuePair<ushort, uint>(
                        item.CounterTypes[index],
                        item.CounterAmounts[index]));
                }
                int hintCount = Math.Min(
                    item.HintTypes?.Length ?? 0,
                    item.HintValues?.Length ?? 0);
                var hints = new List<KeyValuePair<byte, ulong>>(hintCount);
                for (int index = 0; index < hintCount; index++)
                {
                    hints.Add(new KeyValuePair<byte, ulong>(
                        item.HintTypes[index],
                        item.HintValues[index]));
                }
                instance.RestorePresentationMetadata(
                    counters,
                    item.EquippedToRuntimeId,
                    item.TargetRuntimeIds,
                    item.RelationRuntimeIds,
                    hints,
                    item.IsTemporaryTarget);
                instance.CoreStatus = item.CoreStatus;
                instance.IsPublic = item.IsPublic;
                instance.LinkRating = item.LinkRating;
                instance.LinkMarkers = item.LinkMarkers;
            }
        }

        private static DuelChainLinkSnapshot CloneChainLink(
            DuelChainLinkSnapshot source)
        {
            if (source == null)
                return null;
            return new DuelChainLinkSnapshot
            {
                ChainIndex = source.ChainIndex,
                Player = source.Player,
                CardCode = source.CardCode,
                DescriptionId = source.DescriptionId,
                RuntimeId = source.RuntimeId,
                Controller = source.Controller,
                Location = source.Location,
                Sequence = source.Sequence,
                Position = source.Position,
                Status = source.Status
            };
        }

        private static DuelSummonSnapshot CloneSummon(
            DuelSummonSnapshot source)
        {
            if (source == null)
                return null;
            return new DuelSummonSnapshot
            {
                Message = source.Message,
                CardCode = source.CardCode,
                RuntimeId = source.RuntimeId,
                Controller = source.Controller,
                Location = source.Location,
                Sequence = source.Sequence,
                Position = source.Position,
                Status = source.Status
            };
        }

        private PlayerHintSnapshot[] CapturePlayerHints()
        {
            return playerHints
                .OrderBy(item => item.Key)
                .Select(item => new PlayerHintSnapshot
                {
                    Player = (byte)((item.Key >> 8) & 0xff),
                    HintType = (byte)(item.Key & 0xff),
                    Value = item.Value
                })
                .ToArray();
        }

        private static int PlayerHintKey(byte player, byte hintType)
        {
            return (player << 8) | hintType;
        }

        private static DuelistPresentationSnapshot Capture(DuelistState player)
        {
            return new DuelistPresentationSnapshot
            {
                LifePoints = player.LifePoints,
                DeckCount = player.DeckCount,
                ExtraDeckCount = player.ExtraDeckCount,
                ExtraDeck = player.ExtraDeckContents.ToArray(),
                ExtraDeckPositions = player.ExtraDeckInstances
                    .Select(instance => instance?.Position ?? 0U)
                    .ToArray(),
                Hand = player.Hand.ToArray(),
                MonsterZones = (uint[])player.MonsterZones.Clone(),
                MonsterPositions =
                    (uint[])player.MonsterPositions.Clone(),
                SpellTrapZones = (uint[])player.SpellTrapZones.Clone(),
                SpellTrapPositions =
                    (uint[])player.SpellTrapPositions.Clone(),
                Graveyard = player.Graveyard.ToArray(),
                Banished = player.Banished.ToArray(),
                BanishedPositions = player.BanishedInstances
                    .Select(instance => instance?.Position ?? 0U)
                    .ToArray(),
                HandRuntimeIds = RuntimeIds(player.HandInstances),
                ExtraDeckRuntimeIds = RuntimeIds(
                    player.ExtraDeckInstances),
                MonsterRuntimeIds = RuntimeIds(player.MonsterInstances),
                SpellTrapRuntimeIds = RuntimeIds(player.SpellTrapInstances),
                GraveyardRuntimeIds = RuntimeIds(player.GraveyardInstances),
                BanishedRuntimeIds = RuntimeIds(player.BanishedInstances),
                HandOwners = Owners(player.HandInstances),
                ExtraDeckOwners = Owners(player.ExtraDeckInstances),
                MonsterOwners = Owners(player.MonsterInstances),
                SpellTrapOwners = Owners(player.SpellTrapInstances),
                GraveyardOwners = Owners(player.GraveyardInstances),
                BanishedOwners = Owners(player.BanishedInstances),
                OverlayMaterials = player.OverlayInstances
                    .Select(materials => materials
                        .Where(material => material != null)
                        .Select(material => material.DefinitionCode)
                        .ToArray())
                    .ToArray(),
                OverlayRuntimeIds = player.OverlayInstances
                    .Select(materials => RuntimeIds(materials))
                    .ToArray(),
                OverlayOwners = player.OverlayInstances
                    .Select(materials => Owners(materials))
                    .ToArray()
            };
        }

        private void Restore(
            DuelistState destination,
            DuelistPresentationSnapshot source,
            byte controller)
        {
            if (source == null)
            {
                throw new ArgumentException(
                    "A presentation snapshot contains a null duelist.");
            }
            destination.LifePoints = source.LifePoints;
            destination.DeckCount = source.DeckCount;
            destination.ExtraDeckCount = source.ExtraDeckCount;
            destination.DeckContents.Clear();
            destination.ExtraDeckContents.Clear();
            destination.DeckInstances.Clear();
            destination.ExtraDeckInstances.Clear();
            if (source.ExtraDeck != null)
                destination.ExtraDeckContents.AddRange(source.ExtraDeck);
            Replace(destination.Hand, source.Hand);
            Replace(destination.Graveyard, source.Graveyard);
            Replace(destination.Banished, source.Banished);
            Array.Clear(destination.MonsterZones, 0, destination.MonsterZones.Length);
            Array.Clear(
                destination.MonsterPositions,
                0,
                destination.MonsterPositions.Length);
            Array.Clear(destination.SpellTrapZones, 0, destination.SpellTrapZones.Length);
            Array.Clear(
                destination.SpellTrapPositions,
                0,
                destination.SpellTrapPositions.Length);
            if (source.MonsterZones != null)
            {
                Array.Copy(
                    source.MonsterZones,
                    destination.MonsterZones,
                    Math.Min(source.MonsterZones.Length, destination.MonsterZones.Length));
            }
            if (source.SpellTrapZones != null)
            {
                Array.Copy(
                    source.SpellTrapZones,
                    destination.SpellTrapZones,
                    Math.Min(source.SpellTrapZones.Length, destination.SpellTrapZones.Length));
            }
            if (source.MonsterPositions != null)
            {
                Array.Copy(
                    source.MonsterPositions,
                    destination.MonsterPositions,
                    Math.Min(
                        source.MonsterPositions.Length,
                        destination.MonsterPositions.Length));
            }
            if (source.SpellTrapPositions != null)
            {
                Array.Copy(
                    source.SpellTrapPositions,
                    destination.SpellTrapPositions,
                    Math.Min(
                        source.SpellTrapPositions.Length,
                        destination.SpellTrapPositions.Length));
            }
            RebuildInstances(destination, source, controller);
            if (source.OverlayMaterials != null)
            {
                int zoneCount = Math.Min(
                    source.OverlayMaterials.Length,
                    destination.OverlayInstances.Length);
                for (int zone = 0; zone < zoneCount; zone++)
                {
                    uint[] materials =
                        source.OverlayMaterials[zone] ??
                        Array.Empty<uint>();
                    for (int index = 0;
                         index < materials.Length;
                         index++)
                    {
                        ulong runtimeId = source.OverlayRuntimeIds != null &&
                            zone < source.OverlayRuntimeIds.Length &&
                            source.OverlayRuntimeIds[zone] != null &&
                            index < source.OverlayRuntimeIds[zone].Length
                                ? source.OverlayRuntimeIds[zone][index]
                                : 0UL;
                        destination.OverlayInstances[zone].Add(
                            CreateInstanceWithRuntimeId(
                                runtimeId,
                                materials[index],
                                OwnerAt(
                                    source.OverlayOwners,
                                    zone,
                                    index,
                                    controller),
                                controller,
                                (byte)DuelLocation.Overlay,
                                (uint)zone,
                                (uint)index));
                    }
                }
            }
        }

        private void RebuildInstances(
            DuelistState player,
            DuelistPresentationSnapshot source,
            byte controller)
        {
            player.ExtraDeckInstances.Clear();
            for (int index = 0;
                 index < player.ExtraDeckContents.Count;
                 index++)
            {
                player.ExtraDeckInstances.Add(
                    CreateInstanceWithRuntimeId(
                        RuntimeIdAt(source.ExtraDeckRuntimeIds, index),
                        player.ExtraDeckContents[index],
                        OwnerAt(source.ExtraDeckOwners, index, controller),
                        controller,
                        (byte)DuelLocation.Extra,
                        (uint)index,
                        PositionAt(source.ExtraDeckPositions, index)));
            }
            player.HandInstances.Clear();
            for (int index = 0; index < player.Hand.Count; index++)
            {
                player.HandInstances.Add(
                    CreateInstanceWithRuntimeId(
                        RuntimeIdAt(source.HandRuntimeIds, index),
                        player.Hand[index],
                        OwnerAt(source.HandOwners, index, controller),
                        controller,
                        (byte)DuelLocation.Hand,
                        (uint)index,
                        0));
            }
            player.GraveyardInstances.Clear();
            for (int index = 0; index < player.Graveyard.Count; index++)
            {
                player.GraveyardInstances.Add(
                    CreateInstanceWithRuntimeId(
                        RuntimeIdAt(source.GraveyardRuntimeIds, index),
                        player.Graveyard[index],
                        OwnerAt(source.GraveyardOwners, index, controller),
                        controller,
                        (byte)DuelLocation.Graveyard,
                        (uint)index,
                        0));
            }
            player.BanishedInstances.Clear();
            for (int index = 0; index < player.Banished.Count; index++)
            {
                player.BanishedInstances.Add(
                    CreateInstanceWithRuntimeId(
                        RuntimeIdAt(source.BanishedRuntimeIds, index),
                        player.Banished[index],
                        OwnerAt(source.BanishedOwners, index, controller),
                        controller,
                        (byte)DuelLocation.Banished,
                        (uint)index,
                        PositionAt(source.BanishedPositions, index)));
            }
            Array.Clear(
                player.MonsterInstances,
                0,
                player.MonsterInstances.Length);
            foreach (List<CardInstanceState> materials in
                     player.OverlayInstances)
            {
                materials.Clear();
            }
            for (int index = 0; index < player.MonsterZones.Length; index++)
            {
                ulong runtimeId = RuntimeIdAt(
                    source.MonsterRuntimeIds,
                    index);
                if (player.MonsterZones[index] == 0 && runtimeId == 0)
                    continue;
                CardInstanceState instance = CreateInstanceWithRuntimeId(
                        runtimeId,
                        player.MonsterZones[index],
                        OwnerAt(source.MonsterOwners, index, controller),
                        controller,
                        (byte)DuelLocation.MonsterZone,
                        (uint)index,
                        player.MonsterPositions[index]);
                instance.IdentityOpaque =
                    player.MonsterZones[index] == 0 &&
                    runtimeId != 0 &&
                    !IsFaceUpPosition(player.MonsterPositions[index]);
                player.MonsterInstances[index] = instance;
            }
            Array.Clear(
                player.SpellTrapInstances,
                0,
                player.SpellTrapInstances.Length);
            for (int index = 0;
                 index < player.SpellTrapZones.Length;
                 index++)
            {
                ulong runtimeId = RuntimeIdAt(
                    source.SpellTrapRuntimeIds,
                    index);
                if (player.SpellTrapZones[index] == 0 && runtimeId == 0)
                    continue;
                CardInstanceState instance = CreateInstanceWithRuntimeId(
                        runtimeId,
                        player.SpellTrapZones[index],
                        OwnerAt(source.SpellTrapOwners, index, controller),
                        controller,
                        (byte)DuelLocation.SpellTrapZone,
                        (uint)index,
                        player.SpellTrapPositions[index]);
                instance.IdentityOpaque =
                    player.SpellTrapZones[index] == 0 &&
                    runtimeId != 0 &&
                    !IsFaceUpPosition(player.SpellTrapPositions[index]);
                player.SpellTrapInstances[index] = instance;
            }
        }

        private static void Replace(List<uint> destination, uint[] source)
        {
            destination.Clear();
            if (source != null) destination.AddRange(source);
        }

        private static ulong RuntimeIdAt(ulong[] runtimeIds, int index)
        {
            return runtimeIds != null && index >= 0 && index < runtimeIds.Length
                ? runtimeIds[index]
                : 0UL;
        }

        private static uint PositionAt(uint[] positions, int index)
        {
            return positions != null && index >= 0 && index < positions.Length
                ? positions[index]
                : 0U;
        }

        private static ulong[] RuntimeIds(
            IEnumerable<CardInstanceState> instances)
        {
            return instances?
                .Select(instance => instance?.RuntimeId ?? 0UL)
                .ToArray() ?? Array.Empty<ulong>();
        }

        private static byte[] Owners(
            IEnumerable<CardInstanceState> instances)
        {
            return instances?
                .Select(instance => instance?.Owner ?? (byte)0)
                .ToArray() ?? Array.Empty<byte>();
        }

        private static byte OwnerAt(
            byte[] owners,
            int index,
            byte fallback)
        {
            return owners != null && index >= 0 && index < owners.Length
                ? owners[index]
                : fallback;
        }

        private static byte OwnerAt(
            byte[][] owners,
            int zone,
            int index,
            byte fallback)
        {
            return owners != null && zone >= 0 && zone < owners.Length &&
                   owners[zone] != null && index >= 0 &&
                   index < owners[zone].Length
                ? owners[zone][index]
                : fallback;
        }
    }
}
