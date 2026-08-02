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
        public DuelNetworkSnapshot snapshot;
        public DuelNetworkPrompt prompt;
        public DuelNetworkPresentationEvent[] presentationEvents;

        string IDuelNetworkState.Status => status;
        IReadOnlyList<DuelEvent> IDuelNetworkState.PresentationEvents =>
            DuelNetworkProtocol.ToPresentationEvents(presentationEvents);

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
    public sealed class DuelNetworkPresentationEvent
    {
        public byte message;
        public byte player;
        public uint value;
        public uint presentationPhase;
        public uint[] codes;
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
    }

    [Serializable]
    public sealed class DuelNetworkDuelist
    {
        public int lifePoints;
        public int deckCount;
        public int extraDeckCount;
        public uint[] extraDeck;
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
        public ulong[] monsterRuntimeIds;
        public ulong[] spellTrapRuntimeIds;
        public ulong[] graveyardRuntimeIds;
        public ulong[] banishedRuntimeIds;
        public byte[] handOwners;
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
        public uint[] mandatorySums;
        public DuelNetworkChoice[] choices;
    }

    [Serializable]
    public sealed class DuelNetworkChoice
    {
        public string label;
        public uint cardCode;
        public string responseBase64;
        public bool hasLocation;
        public byte controller;
        public byte location;
        public uint sequence;
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
        public uint value;
        public uint code;
        public uint[] codes;
        public DuelNetworkLocation previous;
        public DuelNetworkLocation current;
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
        public const uint HiddenCardCode = uint.MaxValue;
        private const uint FaceDownAttack = 0x2;
        private const uint FaceDownDefense = 0x8;

        public static bool IsTurnFlowPresentationEvent(DuelEvent duelEvent)
        {
            return duelEvent != null &&
                   (duelEvent.Message == CoreMessage.NewTurn ||
                    duelEvent.Message == CoreMessage.NewPhase ||
                    duelEvent.Message == CoreMessage.Draw);
        }

        public static DuelNetworkPresentationEvent CreatePresentationEvent(
            DuelEvent source,
            uint phase,
            byte recipient)
        {
            if (!IsTurnFlowPresentationEvent(source))
                return null;
            if (recipient > 1)
                throw new ArgumentOutOfRangeException(nameof(recipient));

            uint[] codes = source.Codes?.ToArray() ?? Array.Empty<uint>();
            if (source.Message == CoreMessage.Draw &&
                source.Player != recipient)
            {
                codes = new uint[codes.Length];
            }

            return new DuelNetworkPresentationEvent
            {
                message = (byte)source.Message,
                player = ToPerspective(source.Player, recipient),
                value = source.Value,
                presentationPhase = phase,
                codes = codes
            };
        }

        public static IReadOnlyList<DuelEvent> ToPresentationEvents(
            DuelNetworkPresentationEvent[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<DuelEvent>();

            return source
                .Where(item => item != null)
                .Select(item => new DuelEvent
                {
                    Message = (CoreMessage)item.message,
                    RawMessage = item.message,
                    Player = item.player,
                    Value = item.value,
                    PresentationPhase = item.presentationPhase,
                    Codes = item.codes?.ToArray() ?? Array.Empty<uint>()
                })
                .ToArray();
        }

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
                // Only the player addressed by this prompt receives its
                // legal response bytes. The other peer sees public state.
                prompt = currentPrompt != null && currentPrompt.Player == recipient
                    ? CopyPrompt(currentPrompt, state, recipient)
                    : null,
                presentationEvents = Array.Empty<DuelNetworkPresentationEvent>()
            };
            networkState.publicStateHash =
                ComputePublicProjectionHash(networkState);
            return networkState;
        }

        /// <summary>
        /// Deterministic hash of information that is public to both seats.
        /// Private hand/extra-deck identities and face-down card identities are
        /// intentionally excluded. The logical-seat remap makes the result
        /// identical before and after client perspective rotation.
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

            for (byte logicalSeat = 0; logicalSeat < 2; logicalSeat++)
            {
                int perspectiveIndex = logicalSeat ==
                    networkState.recipientSeat ? 0 : 1;
                HashDuelist(ref hash, snapshot.players[perspectiveIndex]);
            }
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
                value = source.Value,
                code = hideCode ? 0U : eventCode,
                codes = codes,
                previous = CopyLocation(source.Previous, recipient),
                current = CopyLocation(source.Current, recipient),
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
                Value = source.value,
                Code = source.code,
                Codes = Clone(source.codes),
                Previous = ToLocation(source.previous),
                Current = ToLocation(source.current),
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

        private static DuelNetworkDuelist CopyDuelist(
            DuelistPresentationSnapshot source,
            bool ownsCards,
            byte sourceSide,
            byte recipient)
        {
            source ??= new DuelistPresentationSnapshot();
            uint[] hand = Clone(source.Hand);
            uint[] extraDeck = Clone(source.ExtraDeck);
            uint[] monsters = Clone(source.MonsterZones);
            uint[] spells = Clone(source.SpellTrapZones);
            uint[] graveyard = Clone(source.Graveyard);
            uint[] banished = Clone(source.Banished);
            uint[] banishedPositions = Clone(source.BanishedPositions);
            uint[][] overlays = Clone(source.OverlayMaterials);
            uint[] monsterPositions = Clone(source.MonsterPositions);
            uint[] spellPositions = Clone(source.SpellTrapPositions);
            ulong[] handRuntimeIds = Clone(source.HandRuntimeIds);
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
                extraDeck = HiddenCount(extraDeck);
                handRuntimeIds = HiddenRuntimeIds(
                    hand.Length,
                    (byte)DuelLocation.Hand);
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
                monsterRuntimeIds = monsterRuntimeIds,
                spellTrapRuntimeIds = spellRuntimeIds,
                graveyardRuntimeIds = graveyardRuntimeIds,
                banishedRuntimeIds = banishedRuntimeIds,
                handOwners = handOwners,
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
                mandatorySums = source.MandatorySums?.ToArray() ??
                    Array.Empty<uint>(),
                choices = source.Choices.Select(choice =>
                {
                    bool hidden = IsPrivateChoice(choice, state, recipient);
                    return new DuelNetworkChoice
                    {
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
                MonsterRuntimeIds = Clone(source.monsterRuntimeIds),
                SpellTrapRuntimeIds = Clone(source.spellTrapRuntimeIds),
                GraveyardRuntimeIds = Clone(source.graveyardRuntimeIds),
                BanishedRuntimeIds = Clone(source.banishedRuntimeIds),
                OverlayRuntimeIds = OverlayRuntimeIds(source.overlays),
                HandOwners = Clone(source.handOwners),
                MonsterOwners = Clone(source.monsterOwners),
                SpellTrapOwners = Clone(source.spellTrapOwners),
                GraveyardOwners = Clone(source.graveyardOwners),
                BanishedOwners = Clone(source.banishedOwners),
                OverlayOwners = OverlayOwners(source.overlays)
            };
        }

        private static DuelPrompt ToPrompt(DuelNetworkPrompt source)
        {
            if (source == null)
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
                SumAtLeast = source.sumAtLeast
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
                    Label = sourceChoice.label ?? string.Empty,
                    CardCode = sourceChoice.cardCode,
                    Response = response,
                    HasLocation = sourceChoice.hasLocation,
                    Controller = sourceChoice.controller,
                    Location = sourceChoice.location,
                    Sequence = sourceChoice.sequence,
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
                if (cards[index] != 0 &&
                    (position & (FaceDownAttack | FaceDownDefense)) != 0)
                {
                    // Zero means an empty zone to DuelPresentationState.
                    // Keep a nonzero opaque marker so the client renders the
                    // card back without learning the real card identifier.
                    cards[index] = HiddenCardCode;
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
