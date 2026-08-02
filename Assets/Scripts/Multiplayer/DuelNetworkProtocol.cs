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
        public int sequence;
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
        public uint[] hand;
        public uint[] monsterZones;
        public uint[] monsterPositions;
        public uint[] spellTrapZones;
        public uint[] spellTrapPositions;
        public uint[] graveyard;
        public uint[] banished;
        public uint[][] overlayMaterials;
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
            return new DuelNetworkState
            {
                sequence = sequence,
                status = status ?? string.Empty,
                snapshot = new DuelNetworkSnapshot
                {
                    players = new[]
                    {
                        CopyDuelist(snapshot.Players[recipient], true),
                        CopyDuelist(snapshot.Players[1 - recipient], false)
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

        private static DuelNetworkDuelist CopyDuelist(
            DuelistPresentationSnapshot source,
            bool ownsCards)
        {
            source ??= new DuelistPresentationSnapshot();
            uint[] hand = Clone(source.Hand);
            uint[] monsters = Clone(source.MonsterZones);
            uint[] spells = Clone(source.SpellTrapZones);
            uint[] graveyard = Clone(source.Graveyard);
            uint[] banished = Clone(source.Banished);
            uint[][] overlays = Clone(source.OverlayMaterials);
            uint[] monsterPositions = Clone(source.MonsterPositions);
            uint[] spellPositions = Clone(source.SpellTrapPositions);

            if (!ownsCards)
            {
                // Hand cards never become public through the network.
                hand = HiddenCount(hand);
                HideFaceDown(monsters, monsterPositions);
                HideFaceDown(spells, spellPositions);
                // A face-down banished card cannot be distinguished in the
                // current presentation snapshot, so keep this whole private
                // list opaque instead of risking a data leak.
                banished = HiddenCount(banished);
                overlays = HideOverlayMaterials(overlays);
            }

            return new DuelNetworkDuelist
            {
                lifePoints = source.LifePoints,
                deckCount = source.DeckCount,
                extraDeckCount = source.ExtraDeckCount,
                hand = hand,
                monsterZones = monsters,
                monsterPositions = monsterPositions,
                spellTrapZones = spells,
                spellTrapPositions = spellPositions,
                graveyard = graveyard,
                banished = banished,
                overlayMaterials = overlays
            };
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
                        descriptionId = choice.DescriptionId,
                        sumValue = choice.SumValue
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
                             DuelLocation.Extra | DuelLocation.Banished)) != 0)
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
                Hand = Clone(source.hand),
                MonsterZones = Clone(source.monsterZones),
                MonsterPositions = Clone(source.monsterPositions),
                SpellTrapZones = Clone(source.spellTrapZones),
                SpellTrapPositions = Clone(source.spellTrapPositions),
                Graveyard = Clone(source.graveyard),
                Banished = Clone(source.banished),
                OverlayMaterials = Clone(source.overlayMaterials)
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

        private static void HideFaceDown(uint[] cards, uint[] positions)
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
    }
}
