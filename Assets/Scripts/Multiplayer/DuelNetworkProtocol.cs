using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Wire-only representation of the duel. These classes deliberately use
    /// fields so JsonUtility can serialize them without reflection settings.
    /// They are not saved to disk and never contain an opponent's hand.
    /// </summary>
    [Serializable]
    public sealed class DuelNetworkState : IDuelNetworkState
    {
        public string matchId;
        public int sequence;
        public byte recipientSeat;
        public ulong stateVersion;
        public ulong publicStateHash;
        public uint lastAcceptedClientSequence;
        public ulong acknowledgedCommandId;
        public ulong acknowledgedResponseRequestId;
        public string status;
        public bool hasDuelClock;
        public float localDuelTimeRemaining;
        public float opponentDuelTimeRemaining;
        public byte activeDuelClockPlayer;
        public bool isDuelClockRunning;
        public DuelNetworkSnapshot snapshot;
        public DuelNetworkPrompt prompt;

        string IDuelNetworkState.Status => status;
        bool IDuelNetworkState.HasDuelClock => hasDuelClock;
        float IDuelNetworkState.LocalDuelTimeRemaining =>
            localDuelTimeRemaining;
        float IDuelNetworkState.OpponentDuelTimeRemaining =>
            opponentDuelTimeRemaining;
        byte IDuelNetworkState.ActiveDuelClockPlayer =>
            activeDuelClockPlayer;
        bool IDuelNetworkState.IsDuelClockRunning =>
            isDuelClockRunning;

        void IDuelNetworkState.ApplyTo(
            DuelPresentationState state,
            CardDatabase database,
            out DuelPrompt currentPrompt)
        {
            DuelNetworkProtocol.Apply(this, state, database, out currentPrompt);
        }

        bool IDuelNetworkState.TryGetCombatStats(
            byte controller,
            byte location,
            uint sequence,
            out int attack,
            out int defense)
        {
            attack = 0;
            defense = 0;
            if ((location & DuelLocation.MonsterZone) == 0 ||
                snapshot?.players == null ||
                controller >= snapshot.players.Length ||
                sequence > int.MaxValue)
            {
                return false;
            }

            DuelNetworkDuelist player = snapshot.players[controller];
            int index = (int)sequence;
            if (player?.monsterAttack == null ||
                player.monsterDefense == null ||
                index >= player.monsterAttack.Length ||
                index >= player.monsterDefense.Length ||
                player.monsterAttack[index] == int.MinValue ||
                player.monsterDefense[index] == int.MinValue)
            {
                return false;
            }

            attack = player.monsterAttack[index];
            defense = player.monsterDefense[index];
            return true;
        }
    }

    [Serializable]
    public sealed class DuelNetworkSnapshot
    {
        public DuelNetworkDuelist[] players;
        public int turnNumber;
        public byte turnPlayer;
        public uint phase;
        public bool hasWinner;
        public byte winner;
        public uint disabledFieldMask;
        public DuelNetworkChainLink[] chainLinks;
        public DuelNetworkCardMetadata[] cardMetadata;
        public DuelNetworkPlayerHint[] playerHints;
        public DuelNetworkSummon pendingSummon;
        public DuelNetworkSummon lastSummon;
    }

    [Serializable]
    public sealed class DuelNetworkSummon
    {
        public byte message;
        public uint cardCode;
        public ulong runtimeId;
        public byte controller;
        public byte location;
        public uint sequence;
        public uint position;
        public byte status;
    }

    [Serializable]
    public sealed class DuelNetworkChainLink
    {
        public uint chainIndex;
        public byte player;
        public uint cardCode;
        public ulong descriptionId;
        public ulong runtimeId;
        public byte controller;
        public byte location;
        public uint sequence;
        public uint position;
        public byte status;
    }

    [Serializable]
    public sealed class DuelNetworkCardMetadata
    {
        public ulong runtimeId;
        public uint coreStatus;
        public bool isPublic;
        public uint linkRating;
        public uint linkMarkers;
        public ushort[] counterTypes;
        public uint[] counterAmounts;
        public ulong equippedToRuntimeId;
        public ulong[] targetRuntimeIds;
        public ulong[] relationRuntimeIds;
        public byte[] hintTypes;
        public ulong[] hintValues;
        public bool isTemporaryTarget;
    }

    [Serializable]
    public sealed class DuelNetworkPlayerHint
    {
        public byte player;
        public byte hintType;
        public ulong value;
    }

    [Serializable]
    public sealed class DuelNetworkDuelist
    {
        public int lifePoints;
        public int deckCount;
        public int extraDeckCount;
        public uint[] extraDeck;
        public uint[] extraDeckPositions;
        public uint[] hand;
        public uint[] monsterZones;
        public uint[] monsterPositions;
        public int[] monsterAttack;
        public int[] monsterDefense;
        public uint[] spellTrapZones;
        public uint[] spellTrapPositions;
        public uint[] graveyard;
        public uint[] banished;
        public uint[] banishedPositions;
        public ulong[] handRuntimeIds;
        public ulong[] extraDeckRuntimeIds;
        public ulong[] monsterRuntimeIds;
        public ulong[] spellTrapRuntimeIds;
        public ulong[] graveyardRuntimeIds;
        public ulong[] banishedRuntimeIds;
        public byte[] handOwners;
        public byte[] extraDeckOwners;
        public byte[] monsterOwners;
        public byte[] spellTrapOwners;
        public byte[] graveyardOwners;
        public byte[] banishedOwners;
        public DuelNetworkOverlayStack[] overlays;
    }

    [Serializable]
    public sealed class DuelNetworkOverlayStack
    {
        public uint[] cards;
        public ulong[] runtimeIds;
        public byte[] owners;
    }

    [Serializable]
    public sealed class DuelNetworkPrompt
    {
        public ulong requestId;
        public byte message;
        public byte player;
        public string title;
        public bool forced;
        public bool cancelable;
        public uint minimumSelections;
        public uint maximumSelections;
        public uint requiredSum;
        public bool sumAtLeast;
        public bool requiresOrderedSelection;
        public bool requiresMaskSelection;
        public ushort counterType;
        public ushort requiredCounterCount;
        public byte maskWidth;
        public uint[] mandatorySums;
        public DuelNetworkChoice[] choices;
    }

    [Serializable]
    public sealed class DuelNetworkChoice
    {
        public ulong runtimeId;
        public string label;
        public uint cardCode;
        public string responseBase64;
        public bool hasLocation;
        public byte controller;
        public byte location;
        public uint sequence;
        public uint position;
        public bool directAttackAvailable;
        public int choiceIndex;
        public ulong descriptionId;
        public uint sumValue;
    }

    [Serializable]
    public sealed class DuelNetworkLocation
    {
        public byte controller;
        public byte location;
        public uint sequence;
        public uint position;
    }

    [Serializable]
    public sealed class DuelNetworkPresentationEvent
    {
        public string matchId;
        public int eventSequence;
        public int requiredStateSequence;
        public byte message;
        public byte player;
        public uint presentationPhase;
        public uint value;
        public uint code;
        public ushort counterType;
        public ulong hintValue;
        public ulong descriptionId;
        public uint[] codes;
        public DuelNetworkLocation previous;
        public DuelNetworkLocation current;
        public DuelNetworkLocation[] previousLocations;
        public DuelNetworkLocation[] currentLocations;
        public int attackerAttack;
        public int attackerDefense;
        public int targetAttack;
        public int targetDefense;
        public bool attackerDestroyed;
        public bool targetDestroyed;
    }

    /// <summary>
    /// Applies perspective mapping before a state leaves the host. For a
    /// client, its own side becomes P0, so the existing arena can keep a
    /// single UI layout. Hidden opponent cards are always zeroed out.
    /// </summary>
    public static class DuelNetworkProtocol
    {
        private const uint FaceDownAttack = 0x2;
        private const uint FaceDownDefense = 0x8;

        public static DuelNetworkState CreateState(
            DuelPresentationState state,
            DuelPrompt currentPrompt,
            byte recipient,
            int sequence,
            string status)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (recipient > 1)
                throw new ArgumentOutOfRangeException(nameof(recipient));

            DuelPresentationSnapshot snapshot = state.CaptureSnapshot();
            var networkState = new DuelNetworkState
            {
                sequence = sequence,
                recipientSeat = recipient,
                status = status ?? string.Empty,
                snapshot = new DuelNetworkSnapshot
                {
                    players = new[]
                    {
                        CopyDuelist(
                            snapshot.Players[recipient],
                            true,
                            recipient,
                            recipient),
                        CopyDuelist(
                            snapshot.Players[1 - recipient],
                            false,
                            (byte)(1 - recipient),
                            recipient)
                    },
                    turnNumber = snapshot.TurnNumber,
                    turnPlayer = ToPerspective(snapshot.TurnPlayer, recipient),
                    phase = snapshot.Phase,
                    hasWinner = snapshot.Winner.HasValue,
                    winner = snapshot.Winner.HasValue
                        ? ToPerspective(snapshot.Winner.Value, recipient)
                        : (byte)0
                },
                // Only the player addressed by the Core receives the prompt.
                // This also covers responses and optional effects outside
                // that player's turn without leaking private choices.
                prompt = currentPrompt != null && currentPrompt.Player == recipient
                    ? CopyPrompt(currentPrompt, state, recipient)
                    : null
            };
            HashSet<ulong> visibleRuntimeIds = VisibleRuntimeIds(
                networkState.snapshot.players);
            networkState.snapshot.disabledFieldMask =
                ToPerspectiveFieldMask(
                    snapshot.DisabledFieldMask,
                    recipient);
            networkState.snapshot.chainLinks = CopyChainLinks(
                snapshot.ChainLinks,
                recipient);
            networkState.snapshot.cardMetadata = CopyCardMetadata(
                snapshot.CardMetadata,
                visibleRuntimeIds);
            networkState.snapshot.playerHints = CopyPlayerHints(
                snapshot.PlayerHints,
                recipient);
            networkState.snapshot.pendingSummon = CopySummon(
                snapshot.PendingSummon,
                recipient);
            networkState.snapshot.lastSummon = CopySummon(
                snapshot.LastSummon,
                recipient);
            networkState.publicStateHash =
                ComputePublicProjectionHash(networkState);
            return networkState;
        }

        /// <summary>
        /// Deterministic hash of the exact privacy-filtered projection sent to
        /// one recipient. The legacy property name is retained for wire
        /// compatibility, but the hash also covers that recipient's private
        /// choices and card identities. The client must acknowledge precisely
        /// the snapshot it received, not a weaker public-state subset.
        /// </summary>
        public static ulong ComputePublicProjectionHash(
            DuelNetworkState networkState)
        {
            if (networkState?.snapshot?.players == null ||
                networkState.snapshot.players.Length != 2 ||
                networkState.recipientSeat > 1)
            {
                return 0;
            }

            ulong hash = 14695981039346656037UL;
            HashString(ref hash, networkState.matchId);
            DuelNetworkSnapshot snapshot = networkState.snapshot;
            HashInt(ref hash, snapshot.turnNumber);
            HashByte(ref hash,
                ToLogical(snapshot.turnPlayer, networkState.recipientSeat));
            HashUInt(ref hash, snapshot.phase);
            HashByte(ref hash, snapshot.hasWinner ? (byte)1 : (byte)0);
            if (snapshot.hasWinner)
            {
                HashByte(ref hash,
                    ToLogical(snapshot.winner, networkState.recipientSeat));
            }
            HashUInt(
                ref hash,
                ToPerspectiveFieldMask(
                    snapshot.disabledFieldMask,
                    networkState.recipientSeat));

            for (byte logicalSeat = 0; logicalSeat < 2; logicalSeat++)
            {
                int perspectiveIndex = logicalSeat ==
                    networkState.recipientSeat ? 0 : 1;
                HashDuelist(ref hash, snapshot.players[perspectiveIndex]);
            }
            HashChainLinks(
                ref hash,
                snapshot.chainLinks,
                networkState.recipientSeat);
            HashPublicCardMetadata(ref hash, snapshot.cardMetadata);
            HashPlayerHints(ref hash, snapshot.playerHints);
            HashSummon(ref hash, snapshot.pendingSummon, networkState.recipientSeat);
            HashSummon(ref hash, snapshot.lastSummon, networkState.recipientSeat);
            HashPrompt(
                ref hash,
                networkState.prompt,
                networkState.recipientSeat);
            return hash;
        }

        public static void Apply(
            DuelNetworkState networkState,
            DuelPresentationState state,
            CardDatabase database,
            out DuelPrompt prompt)
        {
            if (networkState?.snapshot?.players == null ||
                networkState.snapshot.players.Length != 2)
            {
                throw new ArgumentException(
                    "O estado online não contém os dois duelistas.",
                    nameof(networkState));
            }
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            DuelNetworkSnapshot source = networkState.snapshot;
            state.RestoreSnapshot(new DuelPresentationSnapshot
            {
                Players = new[]
                {
                    ToSnapshot(source.players[0]),
                    ToSnapshot(source.players[1])
                },
                TurnNumber = source.turnNumber,
                TurnPlayer = source.turnPlayer,
                Phase = source.phase,
                Winner = source.hasWinner ? source.winner : (byte?)null,
                DisabledFieldMask = source.disabledFieldMask,
                ChainLinks = ToChainLinks(source.chainLinks),
                CardMetadata = ToCardMetadata(source.cardMetadata),
                PlayerHints = ToPlayerHints(source.playerHints),
                PendingSummon = ToSummon(source.pendingSummon),
                LastSummon = ToSummon(source.lastSummon),
                // The host's full event log can mention a card before it is
                // public. A remote replica keeps an empty local history.
                Log = Array.Empty<string>()
            });

            prompt = ToPrompt(networkState.prompt);
            if (prompt != null)
            {
                state.Apply(new DuelEvent { Prompt = prompt });
            }
        }

        public static DuelNetworkPresentationEvent CreatePresentationEvent(
            DuelEvent source,
            byte recipient,
            int eventSequence,
            int requiredStateSequence,
            string matchId)
        {
            return CreatePresentationEvent(
                source,
                null,
                recipient,
                eventSequence,
                requiredStateSequence,
                matchId);
        }

        public static DuelNetworkPresentationEvent CreatePresentationEvent(
            DuelEvent source,
            DuelPresentationState presentationState,
            byte recipient,
            int eventSequence,
            int requiredStateSequence,
            string matchId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (recipient > 1)
                throw new ArgumentOutOfRangeException(nameof(recipient));

            bool hideCode = EventCodeIsPrivate(source, recipient);
            uint eventCode = source.Code != 0
                ? source.Code
                : CardCodeAt(presentationState, source.Previous);
            uint[] codes = source.Codes == null
                ? Array.Empty<uint>()
                : (uint[])source.Codes.Clone();
            if ((source.Message == CoreMessage.Draw ||
                 source.Message == CoreMessage.ShuffleHand) &&
                source.Player != recipient)
            {
                // MSG_SHUFFLE_HAND carries the complete reordered hand just
                // like MSG_DRAW carries the drawn identities. Only the owner
                // may receive those codes; the opponent receives the count
                // and animation event with opaque zero values.
                Array.Clear(codes, 0, codes.Length);
            }
            else if (source.Message == CoreMessage.Swap &&
                     LocationCodeIsPrivate(source.Current, recipient) &&
                     codes.Length > 0)
            {
                codes[0] = 0;
            }
            return new DuelNetworkPresentationEvent
            {
                matchId = matchId ?? string.Empty,
                eventSequence = eventSequence,
                requiredStateSequence = requiredStateSequence,
                message = (byte)source.Message,
                player = source.Player <= 1
                    ? ToPerspective(source.Player, recipient)
                    : source.Player,
                presentationPhase = source.PresentationPhase != 0
                    ? source.PresentationPhase
                    : presentationState?.Phase ?? 0U,
                value = source.Value,
                code = hideCode ? 0U : eventCode,
                counterType = source.CounterType,
                hintValue = hideCode ? 0UL : source.HintValue,
                descriptionId = hideCode ? 0UL : source.DescriptionId,
                codes = codes,
                previous = CopyLocation(source.Previous, recipient),
                current = CopyLocation(source.Current, recipient),
                previousLocations = CopyLocations(
                    source.PreviousLocations,
                    recipient),
                currentLocations = CopyLocations(
                    source.CurrentLocations,
                    recipient),
                attackerAttack = source.AttackerAttack,
                attackerDefense = source.AttackerDefense,
                targetAttack = source.TargetAttack,
                targetDefense = source.TargetDefense,
                attackerDestroyed = source.AttackerDestroyed,
                targetDestroyed = source.TargetDestroyed
            };
        }

        private static uint CardCodeAt(
            DuelPresentationState state,
            CardLocation location)
        {
            if (state == null || location == null ||
                location.Controller >= state.Players.Length ||
                location.Sequence > int.MaxValue)
            {
                return 0;
            }
            DuelistState player = state.Players[location.Controller];
            int sequence = (int)location.Sequence;
            if ((location.Location & DuelLocation.Hand) != 0)
                return sequence < player.Hand.Count ? player.Hand[sequence] : 0;
            if ((location.Location & DuelLocation.MonsterZone) != 0)
                return sequence < player.MonsterZones.Length
                    ? player.MonsterZones[sequence]
                    : 0;
            if ((location.Location & DuelLocation.SpellTrapZone) != 0)
                return sequence < player.SpellTrapZones.Length
                    ? player.SpellTrapZones[sequence]
                    : 0;
            if ((location.Location & DuelLocation.Graveyard) != 0)
                return sequence < player.Graveyard.Count
                    ? player.Graveyard[sequence]
                    : 0;
            if ((location.Location & DuelLocation.Banished) != 0)
                return sequence < player.Banished.Count
                    ? player.Banished[sequence]
                    : 0;
            return 0;
        }

        public static DuelEvent ToPresentationEvent(
            DuelNetworkPresentationEvent source)
        {
            if (source == null)
                return null;
            return new DuelEvent
            {
                Message = (CoreMessage)source.message,
                RawMessage = source.message,
                Player = source.player,
                PresentationPhase = source.presentationPhase,
                Value = source.value,
                Code = source.code,
                CounterType = source.counterType,
                HintValue = source.hintValue,
                DescriptionId = source.descriptionId,
                Codes = Clone(source.codes),
                Previous = ToLocation(source.previous),
                Current = ToLocation(source.current),
                PreviousLocations = ToLocations(source.previousLocations),
                CurrentLocations = ToLocations(source.currentLocations),
                AttackerAttack = source.attackerAttack,
                AttackerDefense = source.attackerDefense,
                TargetAttack = source.targetAttack,
                TargetDefense = source.targetDefense,
                AttackerDestroyed = source.attackerDestroyed,
                TargetDestroyed = source.targetDestroyed,
                Detail = string.Empty
            };
        }

        private static bool EventCodeIsPrivate(
            DuelEvent source,
            byte recipient)
        {
            // A card that activates an effect is public for the duration of
            // the chain even when the triggering location was the hand. The
            // Core broadcasts MSG_CHAINING to both duelists; redacting it here
            // made the remote player lose both card and effect identity.
            if (source.Message == CoreMessage.Chaining)
                return false;
            if (source.Message == CoreMessage.Draw)
                return source.Player != recipient;
            CardLocation location = source.Message == CoreMessage.Swap
                ? source.Previous
                : source.Current ?? source.Previous;
            return LocationCodeIsPrivate(location, recipient);
        }

        private static bool LocationCodeIsPrivate(
            CardLocation location,
            byte recipient)
        {
            if (location == null || location.Controller == recipient)
                return false;
            uint privateLocations = DuelLocation.Hand | DuelLocation.Deck |
                                    DuelLocation.Extra;
            if ((location.Location & privateLocations) != 0)
                return true;
            if ((location.Location & DuelLocation.Banished) != 0 &&
                (location.Position & (FaceDownAttack | FaceDownDefense)) != 0)
            {
                return true;
            }
            return (location.Location &
                    (DuelLocation.MonsterZone | DuelLocation.SpellTrapZone)) != 0 &&
                   (location.Position &
                    (FaceDownAttack | FaceDownDefense)) != 0;
        }

        private static DuelNetworkLocation CopyLocation(
            CardLocation source,
            byte recipient)
        {
            if (source == null)
                return null;
            return new DuelNetworkLocation
            {
                controller = source.Controller <= 1
                    ? ToPerspective(source.Controller, recipient)
                    : source.Controller,
                location = source.Location,
                sequence = source.Sequence,
                position = source.Position
            };
        }

        private static CardLocation ToLocation(DuelNetworkLocation source)
        {
            if (source == null)
                return null;
            return new CardLocation
            {
                Controller = source.controller,
                Location = source.location,
                Sequence = source.sequence,
                Position = source.position
            };
        }

        private static DuelNetworkLocation[] CopyLocations(
            IEnumerable<CardLocation> source,
            byte recipient)
        {
            return source?
                .Where(location => location != null)
                .Select(location => CopyLocation(location, recipient))
                .ToArray() ?? Array.Empty<DuelNetworkLocation>();
        }

        private static CardLocation[] ToLocations(
            IEnumerable<DuelNetworkLocation> source)
        {
            return source?
                .Where(location => location != null)
                .Select(ToLocation)
                .ToArray() ?? Array.Empty<CardLocation>();
        }

        private static uint ToPerspectiveFieldMask(
            uint mask,
            byte recipient)
        {
            if (recipient == 0)
                return mask;
            return (mask << 16) | (mask >> 16);
        }

        private static DuelNetworkChainLink[] CopyChainLinks(
            IEnumerable<DuelChainLinkSnapshot> source,
            byte recipient)
        {
            return source?
                .Where(link => link != null)
                .Select(link =>
                {
                    var location = new CardLocation
                    {
                        Controller = link.Controller,
                        Location = link.Location,
                        Sequence = link.Sequence,
                        Position = link.Position
                    };
                    bool privateAddress = LocationCodeIsPrivate(
                        location,
                        recipient);
                    return new DuelNetworkChainLink
                    {
                        chainIndex = link.ChainIndex,
                        player = ToPerspective(link.Player, recipient),
                        cardCode = link.CardCode,
                        descriptionId = link.DescriptionId,
                        runtimeId = privateAddress
                            ? HiddenRuntimeId(
                                link.Location,
                                link.Sequence > int.MaxValue
                                    ? int.MaxValue
                                    : (int)link.Sequence,
                                0)
                            : link.RuntimeId,
                        controller = ToPerspective(
                            link.Controller,
                            recipient),
                        location = link.Location,
                        sequence = link.Sequence,
                        position = link.Position,
                        status = (byte)link.Status
                    };
                })
                .ToArray() ?? Array.Empty<DuelNetworkChainLink>();
        }

        private static DuelChainLinkSnapshot[] ToChainLinks(
            IEnumerable<DuelNetworkChainLink> source)
        {
            return source?
                .Where(link => link != null)
                .Select(link => new DuelChainLinkSnapshot
                {
                    ChainIndex = link.chainIndex,
                    Player = link.player,
                    CardCode = link.cardCode,
                    DescriptionId = link.descriptionId,
                    RuntimeId = link.runtimeId,
                    Controller = link.controller,
                    Location = link.location,
                    Sequence = link.sequence,
                    Position = link.position,
                    Status = Enum.IsDefined(
                        typeof(DuelChainLinkStatus),
                        link.status)
                            ? (DuelChainLinkStatus)link.status
                            : DuelChainLinkStatus.Chaining
                })
                .ToArray() ?? Array.Empty<DuelChainLinkSnapshot>();
        }

        private static DuelNetworkSummon CopySummon(
            DuelSummonSnapshot source,
            byte recipient)
        {
            if (source == null)
                return null;
            return new DuelNetworkSummon
            {
                message = (byte)source.Message,
                cardCode = source.CardCode,
                runtimeId = source.RuntimeId,
                controller = ToPerspective(source.Controller, recipient),
                location = source.Location,
                sequence = source.Sequence,
                position = source.Position,
                status = (byte)source.Status
            };
        }

        private static DuelSummonSnapshot ToSummon(DuelNetworkSummon source)
        {
            if (IsEmptySummon(source))
                return null;
            return new DuelSummonSnapshot
            {
                Message = Enum.IsDefined(typeof(CoreMessage), source.message)
                    ? (CoreMessage)source.message
                    : CoreMessage.Summoning,
                CardCode = source.cardCode,
                RuntimeId = source.runtimeId,
                Controller = source.controller,
                Location = source.location,
                Sequence = source.sequence,
                Position = source.position,
                Status = Enum.IsDefined(typeof(DuelSummonStatus), source.status)
                    ? (DuelSummonStatus)source.status
                    : DuelSummonStatus.Pending
            };
        }

        private static HashSet<ulong> VisibleRuntimeIds(
            IEnumerable<DuelNetworkDuelist> players)
        {
            var result = new HashSet<ulong>();
            foreach (DuelNetworkDuelist player in
                     players ?? Array.Empty<DuelNetworkDuelist>())
            {
                if (player == null)
                    continue;
                AddRuntimeIds(result, player.extraDeckRuntimeIds);
                AddRuntimeIds(result, player.handRuntimeIds);
                AddRuntimeIds(result, player.monsterRuntimeIds);
                AddRuntimeIds(result, player.spellTrapRuntimeIds);
                AddRuntimeIds(result, player.graveyardRuntimeIds);
                AddRuntimeIds(result, player.banishedRuntimeIds);
                foreach (DuelNetworkOverlayStack stack in
                         player.overlays ??
                         Array.Empty<DuelNetworkOverlayStack>())
                {
                    AddRuntimeIds(result, stack?.runtimeIds);
                }
            }
            return result;
        }

        private static void AddRuntimeIds(
            ISet<ulong> destination,
            IEnumerable<ulong> values)
        {
            if (values == null)
                return;
            foreach (ulong value in values)
            {
                if (value != 0)
                    destination.Add(value);
            }
        }

        private static DuelNetworkCardMetadata[] CopyCardMetadata(
            IEnumerable<CardPresentationMetadataSnapshot> source,
            ISet<ulong> visibleRuntimeIds)
        {
            return source?
                .Where(item => item != null && item.RuntimeId != 0 &&
                               visibleRuntimeIds.Contains(item.RuntimeId))
                .Select(item => new DuelNetworkCardMetadata
                {
                    runtimeId = item.RuntimeId,
                    coreStatus = item.CoreStatus,
                    isPublic = item.IsPublic,
                    linkRating = item.LinkRating,
                    linkMarkers = item.LinkMarkers,
                    counterTypes = Clone(item.CounterTypes),
                    counterAmounts = Clone(item.CounterAmounts),
                    equippedToRuntimeId = visibleRuntimeIds.Contains(
                        item.EquippedToRuntimeId)
                            ? item.EquippedToRuntimeId
                            : 0UL,
                    targetRuntimeIds = (item.TargetRuntimeIds ??
                        Array.Empty<ulong>())
                        .Where(visibleRuntimeIds.Contains)
                        .ToArray(),
                    relationRuntimeIds = (item.RelationRuntimeIds ??
                        Array.Empty<ulong>())
                        .Where(visibleRuntimeIds.Contains)
                        .ToArray(),
                    hintTypes = Clone(item.HintTypes),
                    hintValues = Clone(item.HintValues),
                    isTemporaryTarget = item.IsTemporaryTarget
                })
                .ToArray() ?? Array.Empty<DuelNetworkCardMetadata>();
        }

        private static CardPresentationMetadataSnapshot[] ToCardMetadata(
            IEnumerable<DuelNetworkCardMetadata> source)
        {
            return source?
                .Where(item => item != null && item.runtimeId != 0)
                .Select(item => new CardPresentationMetadataSnapshot
                {
                    RuntimeId = item.runtimeId,
                    CoreStatus = item.coreStatus,
                    IsPublic = item.isPublic,
                    LinkRating = item.linkRating,
                    LinkMarkers = item.linkMarkers,
                    CounterTypes = Clone(item.counterTypes),
                    CounterAmounts = Clone(item.counterAmounts),
                    EquippedToRuntimeId = item.equippedToRuntimeId,
                    TargetRuntimeIds = Clone(item.targetRuntimeIds),
                    RelationRuntimeIds = Clone(item.relationRuntimeIds),
                    HintTypes = Clone(item.hintTypes),
                    HintValues = Clone(item.hintValues),
                    IsTemporaryTarget = item.isTemporaryTarget
                })
                .ToArray() ??
                Array.Empty<CardPresentationMetadataSnapshot>();
        }

        private static DuelNetworkPlayerHint[] CopyPlayerHints(
            IEnumerable<PlayerHintSnapshot> source,
            byte recipient)
        {
            // The pinned protocol does not describe which hint types are
            // public. Preserve privacy by routing player hints only to their
            // owner until each type has an explicit visibility policy.
            return source?
                .Where(hint => hint != null && hint.Player == recipient)
                .Select(hint => new DuelNetworkPlayerHint
                {
                    player = 0,
                    hintType = hint.HintType,
                    value = hint.Value
                })
                .ToArray() ?? Array.Empty<DuelNetworkPlayerHint>();
        }

        private static PlayerHintSnapshot[] ToPlayerHints(
            IEnumerable<DuelNetworkPlayerHint> source)
        {
            return source?
                .Where(hint => hint != null)
                .Select(hint => new PlayerHintSnapshot
                {
                    Player = hint.player,
                    HintType = hint.hintType,
                    Value = hint.value
                })
                .ToArray() ?? Array.Empty<PlayerHintSnapshot>();
        }

        private static DuelNetworkDuelist CopyDuelist(
            DuelistPresentationSnapshot source,
            bool ownsCards,
            byte sourceSide,
            byte recipient)
        {
            source ??= new DuelistPresentationSnapshot();
            uint[] hand = Clone(source.Hand);
            uint[] extraDeck = Clone(source.ExtraDeck);
            uint[] extraDeckPositions = Clone(source.ExtraDeckPositions);
            uint[] monsters = Clone(source.MonsterZones);
            uint[] spells = Clone(source.SpellTrapZones);
            uint[] graveyard = Clone(source.Graveyard);
            uint[] banished = Clone(source.Banished);
            uint[] banishedPositions = Clone(source.BanishedPositions);
            uint[][] overlays = Clone(source.OverlayMaterials);
            uint[] monsterPositions = Clone(source.MonsterPositions);
            uint[] spellPositions = Clone(source.SpellTrapPositions);
            ulong[] handRuntimeIds = Clone(source.HandRuntimeIds);
            ulong[] extraDeckRuntimeIds = Clone(
                source.ExtraDeckRuntimeIds);
            ulong[] monsterRuntimeIds = Clone(source.MonsterRuntimeIds);
            ulong[] spellRuntimeIds = Clone(source.SpellTrapRuntimeIds);
            ulong[] graveyardRuntimeIds = Clone(source.GraveyardRuntimeIds);
            ulong[] banishedRuntimeIds = Clone(source.BanishedRuntimeIds);
            ulong[][] overlayRuntimeIds = Clone(source.OverlayRuntimeIds);
            byte[] handOwners = PerspectiveOwners(
                source.HandOwners,
                hand.Length,
                sourceSide,
                recipient);
            byte[] extraDeckOwners = PerspectiveOwners(
                source.ExtraDeckOwners,
                extraDeck.Length,
                sourceSide,
                recipient);
            byte[] monsterOwners = PerspectiveOwners(
                source.MonsterOwners,
                monsters.Length,
                sourceSide,
                recipient);
            byte[] spellOwners = PerspectiveOwners(
                source.SpellTrapOwners,
                spells.Length,
                sourceSide,
                recipient);
            byte[] graveyardOwners = PerspectiveOwners(
                source.GraveyardOwners,
                graveyard.Length,
                sourceSide,
                recipient);
            byte[] banishedOwners = PerspectiveOwners(
                source.BanishedOwners,
                banished.Length,
                sourceSide,
                recipient);
            byte[][] overlayOwners = PerspectiveOwners(
                source.OverlayOwners,
                overlays,
                sourceSide,
                recipient);

            if (!ownsCards)
            {
                // Hand cards never become public through the network.
                hand = HiddenCount(hand);
                handRuntimeIds = HiddenRuntimeIds(
                    hand.Length,
                    (byte)DuelLocation.Hand);
                HideFaceDownExtra(
                    extraDeck,
                    extraDeckPositions,
                    extraDeckRuntimeIds);
                HideFaceDown(
                    monsters,
                    monsterPositions,
                    monsterRuntimeIds,
                    (byte)DuelLocation.MonsterZone);
                HideFaceDown(
                    spells,
                    spellPositions,
                    spellRuntimeIds,
                    (byte)DuelLocation.SpellTrapZone);
                HideFaceDown(
                    banished,
                    banishedPositions,
                    banishedRuntimeIds,
                    (byte)DuelLocation.Banished);
            }

            return new DuelNetworkDuelist
            {
                lifePoints = source.LifePoints,
                deckCount = source.DeckCount,
                extraDeckCount = source.ExtraDeckCount,
                extraDeck = extraDeck,
                extraDeckPositions = extraDeckPositions,
                hand = hand,
                monsterZones = monsters,
                monsterPositions = monsterPositions,
                monsterAttack = UnknownCombatStats(monsters.Length),
                monsterDefense = UnknownCombatStats(monsters.Length),
                spellTrapZones = spells,
                spellTrapPositions = spellPositions,
                graveyard = graveyard,
                banished = banished,
                banishedPositions = banishedPositions,
                handRuntimeIds = handRuntimeIds,
                extraDeckRuntimeIds = extraDeckRuntimeIds,
                monsterRuntimeIds = monsterRuntimeIds,
                spellTrapRuntimeIds = spellRuntimeIds,
                graveyardRuntimeIds = graveyardRuntimeIds,
                banishedRuntimeIds = banishedRuntimeIds,
                handOwners = handOwners,
                extraDeckOwners = extraDeckOwners,
                monsterOwners = monsterOwners,
                spellTrapOwners = spellOwners,
                graveyardOwners = graveyardOwners,
                banishedOwners = banishedOwners,
                overlays = ToOverlayStacks(
                    overlays,
                    overlayRuntimeIds,
                    overlayOwners)
            };
        }

        public static void PopulateCombatStats(
            DuelNetworkState networkState,
            DuelArenaController authoritativeController,
            byte recipient)
        {
            if (networkState?.snapshot?.players == null ||
                authoritativeController == null || recipient > 1)
            {
                return;
            }

            for (byte perspectiveSide = 0;
                 perspectiveSide < networkState.snapshot.players.Length;
                 perspectiveSide++)
            {
                DuelNetworkDuelist target =
                    networkState.snapshot.players[perspectiveSide];
                if (target == null)
                    continue;
                int count = target.monsterZones?.Length ?? 0;
                target.monsterAttack = UnknownCombatStats(count);
                target.monsterDefense = UnknownCombatStats(count);
                byte authoritativeSide = perspectiveSide == 0
                    ? recipient
                    : (byte)(1 - recipient);
                for (int sequence = 0; sequence < count; sequence++)
                {
                    bool faceDown = target.monsterPositions != null &&
                        sequence < target.monsterPositions.Length &&
                        (target.monsterPositions[sequence] &
                         (FaceDownAttack | FaceDownDefense)) != 0;
                    if (perspectiveSide != 0 && faceDown)
                        continue;
                    if (!authoritativeController.TryGetCurrentCombatStats(
                            authoritativeSide,
                            (byte)DuelLocation.MonsterZone,
                            (uint)sequence,
                            out int attack,
                            out int defense))
                    {
                        continue;
                    }
                    target.monsterAttack[sequence] = attack;
                    target.monsterDefense[sequence] = defense;
                }
            }
        }

        private static DuelNetworkPrompt CopyPrompt(
            DuelPrompt source,
            DuelPresentationState state,
            byte recipient)
        {
            return new DuelNetworkPrompt
            {
                requestId = source.RequestId,
                message = (byte)source.Message,
                player = ToPerspective(source.Player, recipient),
                title = source.Title ?? string.Empty,
                forced = source.Forced,
                cancelable = source.Cancelable,
                minimumSelections = source.MinimumSelections,
                maximumSelections = source.MaximumSelections,
                requiredSum = source.RequiredSum,
                sumAtLeast = source.SumAtLeast,
                requiresOrderedSelection = source.RequiresOrderedSelection,
                requiresMaskSelection = source.RequiresMaskSelection,
                counterType = source.CounterType,
                requiredCounterCount = source.RequiredCounterCount,
                maskWidth = source.MaskWidth,
                mandatorySums = source.MandatorySums?.ToArray() ??
                    Array.Empty<uint>(),
                choices = source.Choices.Select(choice =>
                {
                    bool hidden = IsPrivateChoice(choice, state, recipient);
                    return new DuelNetworkChoice
                    {
                        runtimeId = hidden && choice.HasLocation
                            ? HiddenRuntimeId(
                                choice.Location,
                                choice.Sequence > int.MaxValue
                                    ? int.MaxValue
                                    : (int)choice.Sequence,
                                0)
                            : choice.RuntimeId,
                        label = hidden ? "Carta oculta" : (choice.Label ?? string.Empty),
                        cardCode = hidden ? 0u : choice.CardCode,
                        responseBase64 = choice.Response == null
                            ? string.Empty
                            : Convert.ToBase64String(choice.Response),
                        hasLocation = choice.HasLocation,
                        controller = choice.HasLocation
                            ? ToPerspective(choice.Controller, recipient)
                            : choice.Controller,
                        location = choice.Location,
                        sequence = choice.Sequence,
                        position = choice.Position,
                        directAttackAvailable =
                            choice.DirectAttackAvailable,
                        choiceIndex = choice.ChoiceIndex,
                        descriptionId = hidden ? 0UL : choice.DescriptionId,
                        sumValue = hidden ? 0U : choice.SumValue
                    };
                }).ToArray()
            };
        }

        private static bool IsPrivateChoice(
            DuelChoice choice,
            DuelPresentationState state,
            byte recipient)
        {
            if (choice == null || !choice.HasLocation ||
                choice.Controller == recipient)
            {
                return false;
            }
            uint location = choice.Location;
            if ((location & (DuelLocation.Hand | DuelLocation.Deck |
                             DuelLocation.Extra)) != 0)
            {
                return true;
            }
            if (choice.Controller >= state.Players.Length)
                return true;

            uint position = 0;
            DuelistState opponent = state.Players[choice.Controller];
            if ((location & DuelLocation.MonsterZone) != 0 &&
                choice.Sequence < opponent.MonsterPositions.Length)
            {
                position = opponent.MonsterPositions[choice.Sequence];
            }
            else if ((location & DuelLocation.SpellTrapZone) != 0 &&
                     choice.Sequence < opponent.SpellTrapPositions.Length)
            {
                position = opponent.SpellTrapPositions[choice.Sequence];
            }
            else if ((location & DuelLocation.Banished) != 0)
            {
                position = choice.Sequence < opponent.BanishedInstances.Count
                    ? opponent.BanishedInstances[(int)choice.Sequence]
                        ?.Position ?? 0U
                    : FaceDownDefense;
            }
            return (position & (FaceDownAttack | FaceDownDefense)) != 0;
        }

        private static DuelistPresentationSnapshot ToSnapshot(
            DuelNetworkDuelist source)
        {
            source ??= new DuelNetworkDuelist();
            return new DuelistPresentationSnapshot
            {
                LifePoints = source.lifePoints,
                DeckCount = source.deckCount,
                ExtraDeckCount = source.extraDeckCount,
                ExtraDeck = Clone(source.extraDeck),
                ExtraDeckPositions = Clone(source.extraDeckPositions),
                Hand = Clone(source.hand),
                MonsterZones = Clone(source.monsterZones),
                MonsterPositions = Clone(source.monsterPositions),
                SpellTrapZones = Clone(source.spellTrapZones),
                SpellTrapPositions = Clone(source.spellTrapPositions),
                Graveyard = Clone(source.graveyard),
                Banished = Clone(source.banished),
                BanishedPositions = Clone(source.banishedPositions),
                OverlayMaterials = OverlayCards(source.overlays),
                HandRuntimeIds = Clone(source.handRuntimeIds),
                ExtraDeckRuntimeIds = Clone(source.extraDeckRuntimeIds),
                MonsterRuntimeIds = Clone(source.monsterRuntimeIds),
                SpellTrapRuntimeIds = Clone(source.spellTrapRuntimeIds),
                GraveyardRuntimeIds = Clone(source.graveyardRuntimeIds),
                BanishedRuntimeIds = Clone(source.banishedRuntimeIds),
                OverlayRuntimeIds = OverlayRuntimeIds(source.overlays),
                HandOwners = Clone(source.handOwners),
                ExtraDeckOwners = Clone(source.extraDeckOwners),
                MonsterOwners = Clone(source.monsterOwners),
                SpellTrapOwners = Clone(source.spellTrapOwners),
                GraveyardOwners = Clone(source.graveyardOwners),
                BanishedOwners = Clone(source.banishedOwners),
                OverlayOwners = OverlayOwners(source.overlays)
            };
        }

        private static DuelPrompt ToPrompt(DuelNetworkPrompt source)
        {
            if (IsEmptyPrompt(source))
                return null;

            var prompt = new DuelPrompt
            {
                RequestId = source.requestId,
                Message = (CoreMessage)source.message,
                Player = source.player,
                Title = source.title ?? string.Empty,
                Forced = source.forced,
                Cancelable = source.cancelable,
                MinimumSelections = source.minimumSelections,
                MaximumSelections = source.maximumSelections,
                RequiredSum = source.requiredSum,
                SumAtLeast = source.sumAtLeast,
                RequiresOrderedSelection = source.requiresOrderedSelection,
                RequiresMaskSelection = source.requiresMaskSelection,
                CounterType = source.counterType,
                RequiredCounterCount = source.requiredCounterCount,
                MaskWidth = source.maskWidth
            };
            if (source.mandatorySums != null)
                prompt.MandatorySums.AddRange(source.mandatorySums);
            foreach (DuelNetworkChoice sourceChoice in
                     source.choices ?? Array.Empty<DuelNetworkChoice>())
            {
                if (sourceChoice == null)
                    continue;
                byte[] response = Array.Empty<byte>();
                if (!string.IsNullOrWhiteSpace(sourceChoice.responseBase64))
                {
                    try { response = Convert.FromBase64String(sourceChoice.responseBase64); }
                    catch (FormatException)
                    {
                        throw new ArgumentException(
                            "Uma resposta online recebeu Base64 inválido.");
                    }
                }
                prompt.Choices.Add(new DuelChoice
                {
                    RequestId = prompt.RequestId,
                    RuntimeId = sourceChoice.runtimeId,
                    Label = sourceChoice.label ?? string.Empty,
                    CardCode = sourceChoice.cardCode,
                    Response = response,
                    HasLocation = sourceChoice.hasLocation,
                    Controller = sourceChoice.controller,
                    Location = sourceChoice.location,
                    Sequence = sourceChoice.sequence,
                    Position = sourceChoice.position,
                    DirectAttackAvailable =
                        sourceChoice.directAttackAvailable,
                    ChoiceIndex = sourceChoice.choiceIndex,
                    DescriptionId = sourceChoice.descriptionId,
                    SumValue = sourceChoice.sumValue
                });
            }
            return prompt;
        }

        private static void HideFaceDown(
            uint[] cards,
            uint[] positions,
            ulong[] runtimeIds,
            byte location)
        {
            if (cards == null)
                return;
            for (int index = 0; index < cards.Length; index++)
            {
                uint position = positions != null && index < positions.Length
                    ? positions[index]
                    : 0;
                if ((position & (FaceDownAttack | FaceDownDefense)) != 0)
                {
                    cards[index] = 0;
                    if (runtimeIds != null && index < runtimeIds.Length)
                    {
                        runtimeIds[index] = HiddenRuntimeId(
                            location,
                            index,
                            0);
                    }
                }
            }
        }

        private static void HideFaceDownExtra(
            uint[] cards,
            uint[] positions,
            ulong[] runtimeIds)
        {
            if (cards == null)
                return;
            for (int index = 0; index < cards.Length; index++)
            {
                uint position = positions != null && index < positions.Length
                    ? positions[index]
                    : 0U;
                bool faceUp = (position & 0x5U) != 0;
                if (faceUp)
                    continue;
                cards[index] = 0;
                if (runtimeIds != null && index < runtimeIds.Length)
                {
                    runtimeIds[index] = HiddenRuntimeId(
                        (byte)DuelLocation.Extra,
                        index,
                        0);
                }
            }
        }

        private static uint[][] HideOverlayMaterials(uint[][] overlays)
        {
            if (overlays == null)
                return Array.Empty<uint[]>();
            var result = new uint[overlays.Length][];
            for (int index = 0; index < overlays.Length; index++)
                result[index] = HiddenCount(overlays[index]);
            return result;
        }

        private static uint[] HiddenCount(uint[] source)
        {
            return new uint[source?.Length ?? 0];
        }

        private static ulong[] HiddenRuntimeIds(int count, byte location)
        {
            var result = new ulong[Math.Max(0, count)];
            for (int index = 0; index < result.Length; index++)
                result[index] = HiddenRuntimeId(location, index, 0);
            return result;
        }

        private static ulong[][] HiddenOverlayRuntimeIds(uint[][] overlays)
        {
            var result = new ulong[overlays?.Length ?? 0][];
            for (int zone = 0; zone < result.Length; zone++)
            {
                int count = overlays[zone]?.Length ?? 0;
                result[zone] = new ulong[count];
                for (int index = 0; index < count; index++)
                {
                    result[zone][index] = HiddenRuntimeId(
                        (byte)DuelLocation.Overlay,
                        zone,
                        index);
                }
            }
            return result;
        }

        private static ulong HiddenRuntimeId(
            byte location,
            int sequence,
            int subSequence)
        {
            // Per-recipient, address-derived tokens remain stable across
            // repair snapshots but cannot correlate a private card before
            // and after it changes hidden zones or is shuffled.
            return 0x6000000000000000UL |
                   ((ulong)location << 48) |
                   ((ulong)(uint)(sequence + 1) << 20) |
                   (uint)(subSequence + 1);
        }

        private static void HashDuelist(
            ref ulong hash,
            DuelNetworkDuelist player)
        {
            if (player == null)
            {
                HashByte(ref hash, 0);
                return;
            }

            HashByte(ref hash, 1);
            HashInt(ref hash, player.lifePoints);
            HashInt(ref hash, player.deckCount);
            HashInt(ref hash, player.extraDeckCount);
            HashInt(ref hash, player.hand?.Length ?? 0);

            HashPublicField(
                ref hash,
                player.monsterZones,
                player.monsterPositions,
                player.monsterAttack,
                player.monsterDefense);
            HashPublicField(
                ref hash,
                player.spellTrapZones,
                player.spellTrapPositions,
                null,
                null);
            HashUIntArray(ref hash, player.graveyard);
            HashPublicField(
                ref hash,
                player.banished,
                player.banishedPositions,
                null,
                null);

            int overlayZones = player.overlays?.Length ?? 0;
            HashInt(ref hash, overlayZones);
            for (int zone = 0; zone < overlayZones; zone++)
                HashUIntArray(ref hash, player.overlays[zone]?.cards);

            // Hash the complete privacy-filtered projection actually sent to
            // this recipient. This detects a lost/reordered private hand,
            // runtime identity or combat-stat field without exposing any
            // information that CopyDuelist already redacted.
            HashUIntArray(ref hash, player.extraDeck);
            HashUIntArray(ref hash, player.extraDeckPositions);
            HashUIntArray(ref hash, player.hand);
            HashUIntArray(ref hash, player.monsterZones);
            HashUIntArray(ref hash, player.monsterPositions);
            HashIntArray(ref hash, player.monsterAttack);
            HashIntArray(ref hash, player.monsterDefense);
            HashUIntArray(ref hash, player.spellTrapZones);
            HashUIntArray(ref hash, player.spellTrapPositions);
            HashUIntArray(ref hash, player.banishedPositions);
            HashOrderedULongArray(ref hash, player.handRuntimeIds);
            HashOrderedULongArray(ref hash, player.extraDeckRuntimeIds);
            HashOrderedULongArray(ref hash, player.monsterRuntimeIds);
            HashOrderedULongArray(ref hash, player.spellTrapRuntimeIds);
            HashOrderedULongArray(ref hash, player.graveyardRuntimeIds);
            HashOrderedULongArray(ref hash, player.banishedRuntimeIds);
            HashByteArray(ref hash, player.handOwners);
            HashByteArray(ref hash, player.extraDeckOwners);
            HashByteArray(ref hash, player.monsterOwners);
            HashByteArray(ref hash, player.spellTrapOwners);
            HashByteArray(ref hash, player.graveyardOwners);
            HashByteArray(ref hash, player.banishedOwners);
            for (int zone = 0; zone < overlayZones; zone++)
            {
                DuelNetworkOverlayStack stack = player.overlays[zone];
                HashOrderedULongArray(ref hash, stack?.runtimeIds);
                HashByteArray(ref hash, stack?.owners);
            }
        }

        private static void HashChainLinks(
            ref ulong hash,
            IEnumerable<DuelNetworkChainLink> links,
            byte recipient)
        {
            DuelNetworkChainLink[] ordered = links?
                .Where(link => link != null)
                .OrderBy(link => link.chainIndex)
                .ToArray() ?? Array.Empty<DuelNetworkChainLink>();
            HashInt(ref hash, ordered.Length);
            foreach (DuelNetworkChainLink link in ordered)
            {
                HashUInt(ref hash, link.chainIndex);
                HashByte(ref hash, ToLogical(link.player, recipient));
                HashUInt(ref hash, link.cardCode);
                HashULong(ref hash, link.descriptionId);
                HashULong(ref hash, link.runtimeId);
                HashByte(ref hash, ToLogical(link.controller, recipient));
                HashByte(ref hash, link.location);
                HashUInt(ref hash, link.sequence);
                HashUInt(ref hash, link.position);
                HashByte(ref hash, link.status);
            }
        }

        private static void HashSummon(
            ref ulong hash,
            DuelNetworkSummon summon,
            byte recipient)
        {
            if (IsEmptySummon(summon))
            {
                HashByte(ref hash, 0);
                return;
            }
            HashByte(ref hash, 1);
            HashByte(ref hash, summon.message);
            HashUInt(ref hash, summon.cardCode);
            HashULong(ref hash, summon.runtimeId);
            HashByte(ref hash, ToLogical(summon.controller, recipient));
            HashByte(ref hash, summon.location);
            HashUInt(ref hash, summon.sequence);
            HashUInt(ref hash, summon.position);
            HashByte(ref hash, summon.status);
        }

        private static void HashPublicCardMetadata(
            ref ulong hash,
            IEnumerable<DuelNetworkCardMetadata> metadata)
        {
            DuelNetworkCardMetadata[] visible = metadata?
                .Where(item => item != null)
                .OrderBy(item => item.runtimeId)
                .ToArray() ?? Array.Empty<DuelNetworkCardMetadata>();
            HashInt(ref hash, visible.Length);
            foreach (DuelNetworkCardMetadata item in visible)
            {
                HashULong(ref hash, item.runtimeId);
                HashByte(ref hash, item.isPublic ? (byte)1 : (byte)0);
                HashUInt(ref hash, item.coreStatus);
                HashUInt(ref hash, item.linkRating);
                HashUInt(ref hash, item.linkMarkers);
                int counters = Math.Min(
                    item.counterTypes?.Length ?? 0,
                    item.counterAmounts?.Length ?? 0);
                HashInt(ref hash, counters);
                for (int index = 0; index < counters; index++)
                {
                    HashUInt(ref hash, item.counterTypes[index]);
                    HashUInt(ref hash, item.counterAmounts[index]);
                }
                HashULong(ref hash, item.equippedToRuntimeId);
                HashULongArray(ref hash, item.targetRuntimeIds);
                HashULongArray(ref hash, item.relationRuntimeIds);
                HashByteArray(ref hash, item.hintTypes);
                HashOrderedULongArray(ref hash, item.hintValues);
                HashByte(ref hash, item.isTemporaryTarget ? (byte)1 : (byte)0);
            }
        }

        private static void HashPlayerHints(
            ref ulong hash,
            IEnumerable<DuelNetworkPlayerHint> hints)
        {
            DuelNetworkPlayerHint[] values = hints?
                .Where(hint => hint != null)
                .OrderBy(hint => hint.player)
                .ThenBy(hint => hint.hintType)
                .ToArray() ?? Array.Empty<DuelNetworkPlayerHint>();
            HashInt(ref hash, values.Length);
            foreach (DuelNetworkPlayerHint hint in values)
            {
                HashByte(ref hash, hint.player);
                HashByte(ref hash, hint.hintType);
                HashULong(ref hash, hint.value);
            }
        }

        private static void HashPrompt(
            ref ulong hash,
            DuelNetworkPrompt prompt,
            byte recipient)
        {
            if (IsEmptyPrompt(prompt))
            {
                HashByte(ref hash, 0);
                return;
            }
            HashByte(ref hash, 1);
            HashULong(ref hash, prompt.requestId);
            HashByte(ref hash, prompt.message);
            HashByte(ref hash, ToLogical(prompt.player, recipient));
            HashString(ref hash, prompt.title);
            HashByte(ref hash, prompt.forced ? (byte)1 : (byte)0);
            HashByte(ref hash, prompt.cancelable ? (byte)1 : (byte)0);
            HashUInt(ref hash, prompt.minimumSelections);
            HashUInt(ref hash, prompt.maximumSelections);
            HashUInt(ref hash, prompt.requiredSum);
            HashByte(ref hash, prompt.sumAtLeast ? (byte)1 : (byte)0);
            HashByte(
                ref hash,
                prompt.requiresOrderedSelection ? (byte)1 : (byte)0);
            HashByte(
                ref hash,
                prompt.requiresMaskSelection ? (byte)1 : (byte)0);
            HashUInt(ref hash, prompt.counterType);
            HashUInt(ref hash, prompt.requiredCounterCount);
            HashByte(ref hash, prompt.maskWidth);
            HashUIntArray(ref hash, prompt.mandatorySums);

            DuelNetworkChoice[] choices = prompt.choices ??
                                          Array.Empty<DuelNetworkChoice>();
            HashInt(ref hash, choices.Length);
            foreach (DuelNetworkChoice choice in choices)
            {
                if (choice == null)
                {
                    HashByte(ref hash, 0);
                    continue;
                }
                HashByte(ref hash, 1);
                HashULong(ref hash, choice.runtimeId);
                HashString(ref hash, choice.label);
                HashUInt(ref hash, choice.cardCode);
                HashString(ref hash, choice.responseBase64);
                HashByte(ref hash, choice.hasLocation ? (byte)1 : (byte)0);
                HashByte(
                    ref hash,
                    choice.hasLocation
                        ? ToLogical(choice.controller, recipient)
                        : choice.controller);
                HashByte(ref hash, choice.location);
                HashUInt(ref hash, choice.sequence);
                HashUInt(ref hash, choice.position);
                HashByte(
                    ref hash,
                    choice.directAttackAvailable ? (byte)1 : (byte)0);
                HashInt(ref hash, choice.choiceIndex);
                HashULong(ref hash, choice.descriptionId);
                HashUInt(ref hash, choice.sumValue);
            }
        }

        private static bool IsEmptySummon(DuelNetworkSummon summon)
        {
            return summon == null ||
                   summon.message == 0 &&
                   summon.cardCode == 0 &&
                   summon.runtimeId == 0 &&
                   summon.controller == 0 &&
                   summon.location == 0 &&
                   summon.sequence == 0 &&
                   summon.position == 0 &&
                   summon.status == 0;
        }

        private static bool IsEmptyPrompt(DuelNetworkPrompt prompt)
        {
            return prompt == null ||
                   prompt.requestId == 0 &&
                   prompt.message == 0 &&
                   prompt.player == 0 &&
                   string.IsNullOrEmpty(prompt.title) &&
                   !prompt.forced &&
                   !prompt.cancelable &&
                   prompt.minimumSelections == 0 &&
                   prompt.maximumSelections == 0 &&
                   prompt.requiredSum == 0 &&
                   !prompt.sumAtLeast &&
                   !prompt.requiresOrderedSelection &&
                   !prompt.requiresMaskSelection &&
                   prompt.counterType == 0 &&
                   prompt.requiredCounterCount == 0 &&
                   prompt.maskWidth == 0 &&
                   (prompt.mandatorySums == null ||
                    prompt.mandatorySums.Length == 0) &&
                   (prompt.choices == null || prompt.choices.Length == 0);
        }

        private static void HashPublicField(
            ref ulong hash,
            uint[] cards,
            uint[] positions,
            int[] attack,
            int[] defense)
        {
            int count = Math.Max(cards?.Length ?? 0, positions?.Length ?? 0);
            HashInt(ref hash, count);
            for (int index = 0; index < count; index++)
            {
                uint position = positions != null && index < positions.Length
                    ? positions[index]
                    : 0;
                bool faceDown = (position & (FaceDownAttack | FaceDownDefense)) != 0;
                uint code = cards != null && index < cards.Length && !faceDown
                    ? cards[index]
                    : 0;
                HashUInt(ref hash, code);
                HashUInt(ref hash, position);
                if (attack != null || defense != null)
                {
                    HashInt(ref hash,
                        !faceDown && attack != null && index < attack.Length
                            ? attack[index]
                            : int.MinValue);
                    HashInt(ref hash,
                        !faceDown && defense != null && index < defense.Length
                            ? defense[index]
                            : int.MinValue);
                }
            }
        }

        private static void HashUIntArray(ref ulong hash, uint[] values)
        {
            HashInt(ref hash, values?.Length ?? 0);
            if (values == null)
                return;
            for (int index = 0; index < values.Length; index++)
                HashUInt(ref hash, values[index]);
        }

        private static void HashULongArray(ref ulong hash, ulong[] values)
        {
            HashInt(ref hash, values?.Length ?? 0);
            if (values == null)
                return;
            foreach (ulong value in values.OrderBy(value => value))
                HashULong(ref hash, value);
        }

        private static void HashOrderedULongArray(
            ref ulong hash,
            ulong[] values)
        {
            HashInt(ref hash, values?.Length ?? 0);
            if (values == null)
                return;
            foreach (ulong value in values)
                HashULong(ref hash, value);
        }

        private static void HashIntArray(ref ulong hash, int[] values)
        {
            HashInt(ref hash, values?.Length ?? 0);
            if (values == null)
                return;
            foreach (int value in values)
                HashInt(ref hash, value);
        }

        private static void HashByteArray(ref ulong hash, byte[] values)
        {
            HashInt(ref hash, values?.Length ?? 0);
            if (values == null)
                return;
            foreach (byte value in values)
                HashByte(ref hash, value);
        }

        private static byte ToLogical(byte perspectiveSide, byte recipient)
        {
            return perspectiveSide == 0 ? recipient : (byte)(1 - recipient);
        }

        private static void HashString(ref ulong hash, string value)
        {
            value ??= string.Empty;
            for (int index = 0; index < value.Length; index++)
                HashUInt(ref hash, value[index]);
            HashUInt(ref hash, 0xffffffffU);
        }

        private static void HashInt(ref ulong hash, int value)
        {
            HashUInt(ref hash, unchecked((uint)value));
        }

        private static void HashUInt(ref ulong hash, uint value)
        {
            HashByte(ref hash, (byte)value);
            HashByte(ref hash, (byte)(value >> 8));
            HashByte(ref hash, (byte)(value >> 16));
            HashByte(ref hash, (byte)(value >> 24));
        }

        private static void HashULong(ref ulong hash, ulong value)
        {
            HashUInt(ref hash, unchecked((uint)value));
            HashUInt(ref hash, unchecked((uint)(value >> 32)));
        }

        private static void HashByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }

        private static byte ToPerspective(byte globalSide, byte recipient)
        {
            return globalSide == recipient ? (byte)0 : (byte)1;
        }

        private static uint[] Clone(uint[] source)
        {
            return source == null ? Array.Empty<uint>() : (uint[])source.Clone();
        }

        private static uint[][] Clone(uint[][] source)
        {
            if (source == null)
                return Array.Empty<uint[]>();
            var result = new uint[source.Length][];
            for (int index = 0; index < source.Length; index++)
                result[index] = Clone(source[index]);
            return result;
        }

        private static ulong[] Clone(ulong[] source)
        {
            return source == null ? Array.Empty<ulong>() : (ulong[])source.Clone();
        }

        private static ushort[] Clone(ushort[] source)
        {
            return source == null
                ? Array.Empty<ushort>()
                : (ushort[])source.Clone();
        }

        private static ulong[][] Clone(ulong[][] source)
        {
            if (source == null)
                return Array.Empty<ulong[]>();
            var result = new ulong[source.Length][];
            for (int index = 0; index < source.Length; index++)
                result[index] = Clone(source[index]);
            return result;
        }

        private static DuelNetworkOverlayStack[] ToOverlayStacks(
            uint[][] cards,
            ulong[][] runtimeIds,
            byte[][] owners)
        {
            int count = Math.Max(
                Math.Max(cards?.Length ?? 0, runtimeIds?.Length ?? 0),
                owners?.Length ?? 0);
            var result = new DuelNetworkOverlayStack[count];
            for (int index = 0; index < count; index++)
            {
                result[index] = new DuelNetworkOverlayStack
                {
                    cards = cards != null && index < cards.Length
                        ? Clone(cards[index])
                        : Array.Empty<uint>(),
                    runtimeIds = runtimeIds != null && index < runtimeIds.Length
                        ? Clone(runtimeIds[index])
                        : Array.Empty<ulong>(),
                    owners = owners != null && index < owners.Length
                        ? Clone(owners[index])
                        : Array.Empty<byte>()
                };
            }
            return result;
        }

        private static uint[][] OverlayCards(DuelNetworkOverlayStack[] source)
        {
            if (source == null)
                return Array.Empty<uint[]>();
            var result = new uint[source.Length][];
            for (int index = 0; index < source.Length; index++)
                result[index] = Clone(source[index]?.cards);
            return result;
        }

        private static ulong[][] OverlayRuntimeIds(
            DuelNetworkOverlayStack[] source)
        {
            if (source == null)
                return Array.Empty<ulong[]>();
            var result = new ulong[source.Length][];
            for (int index = 0; index < source.Length; index++)
                result[index] = Clone(source[index]?.runtimeIds);
            return result;
        }

        private static byte[][] OverlayOwners(
            DuelNetworkOverlayStack[] source)
        {
            if (source == null)
                return Array.Empty<byte[]>();
            var result = new byte[source.Length][];
            for (int index = 0; index < source.Length; index++)
                result[index] = Clone(source[index]?.owners);
            return result;
        }

        private static byte[] PerspectiveOwners(
            byte[] source,
            int count,
            byte fallback,
            byte recipient)
        {
            var result = new byte[Math.Max(0, count)];
            for (int index = 0; index < result.Length; index++)
            {
                byte owner = source != null && index < source.Length
                    ? source[index]
                    : fallback;
                result[index] = ToPerspective(owner, recipient);
            }
            return result;
        }

        private static byte[][] PerspectiveOwners(
            byte[][] source,
            uint[][] cards,
            byte fallback,
            byte recipient)
        {
            int zones = Math.Max(source?.Length ?? 0, cards?.Length ?? 0);
            var result = new byte[zones][];
            for (int zone = 0; zone < zones; zone++)
            {
                int count = cards != null && zone < cards.Length
                    ? cards[zone]?.Length ?? 0
                    : source != null && zone < source.Length
                        ? source[zone]?.Length ?? 0
                        : 0;
                result[zone] = PerspectiveOwners(
                    source != null && zone < source.Length
                        ? source[zone]
                        : null,
                    count,
                    fallback,
                    recipient);
            }
            return result;
        }

        private static byte[] Clone(byte[] source)
        {
            return source == null ? Array.Empty<byte>() : (byte[])source.Clone();
        }

        private static int[] UnknownCombatStats(int count)
        {
            var result = new int[Math.Max(0, count)];
            for (int index = 0; index < result.Length; index++)
                result[index] = int.MinValue;
            return result;
        }
    }
}
