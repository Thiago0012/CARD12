using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArcaneDuel.DuelEngine.Data;

namespace ArcaneDuel.DuelEngine.Protocol
{
    public enum CoreMessage : byte
    {
        Retry = 1,
        Hint = 2,
        Waiting = 3,
        Start = 4,
        Win = 5,
        SelectBattleCommand = 10,
        SelectIdleCommand = 11,
        SelectEffectYesNo = 12,
        SelectYesNo = 13,
        SelectOption = 14,
        SelectCard = 15,
        SelectChain = 16,
        SelectPlace = 18,
        SelectPosition = 19,
        SelectTribute = 20,
        SortChain = 21,
        SelectCounter = 22,
        SelectSum = 23,
        SelectDisableField = 24,
        SortCard = 25,
        SelectUnselectCard = 26,
        ConfirmDeckTop = 30,
        ConfirmCards = 31,
        ShuffleDeck = 32,
        ShuffleHand = 33,
        RefreshDeck = 34,
        SwapGraveDeck = 35,
        ShuffleSetCard = 36,
        ReverseDeck = 37,
        DeckTop = 38,
        ShuffleExtra = 39,
        NewTurn = 40,
        NewPhase = 41,
        ConfirmExtraTop = 42,
        Move = 50,
        PositionChange = 53,
        Set = 54,
        Swap = 55,
        FieldDisabled = 56,
        Summoning = 60,
        Summoned = 61,
        SpecialSummoning = 62,
        SpecialSummoned = 63,
        FlipSummoning = 64,
        FlipSummoned = 65,
        Chaining = 70,
        Chained = 71,
        ChainSolving = 72,
        ChainSolved = 73,
        ChainEnd = 74,
        ChainNegated = 75,
        ChainDisabled = 76,
        CardSelected = 80,
        RandomSelected = 81,
        BecomeTarget = 83,
        Draw = 90,
        Damage = 91,
        Recover = 92,
        Equip = 93,
        LifePointsUpdate = 94,
        Unequip = 95,
        CardTarget = 96,
        CancelTarget = 97,
        PayLifePointCost = 100,
        AddCounter = 101,
        RemoveCounter = 102,
        Attack = 110,
        Battle = 111,
        AttackDisabled = 112,
        DamageStepStart = 113,
        DamageStepEnd = 114,
        MissedEffect = 120,
        BeChainTarget = 121,
        CreateRelation = 122,
        ReleaseRelation = 123,
        TossCoin = 130,
        TossDice = 131,
        RockPaperScissors = 132,
        HandResult = 133,
        AnnounceRace = 140,
        AnnounceAttribute = 141,
        AnnounceCard = 142,
        AnnounceNumber = 143,
        CardHint = 160,
        TagSwap = 161,
        ReloadField = 162,
        PlayerHint = 165,
        MatchKill = 170,
        RemoveCards = 190
    }

    public static class DuelLocation
    {
        public const uint Deck = 0x01;
        public const uint Hand = 0x02;
        public const uint MonsterZone = 0x04;
        public const uint SpellTrapZone = 0x08;
        public const uint Graveyard = 0x10;
        public const uint Banished = 0x20;
        public const uint Extra = 0x40;
        public const uint Overlay = 0x80;
    }

    public sealed class CardLocation
    {
        public byte Controller { get; internal set; }
        public byte Location { get; internal set; }
        public uint Sequence { get; internal set; }
        public uint Position { get; internal set; }
    }

    public sealed class DuelChoice
    {
        public ulong RequestId { get; internal set; }
        /// <summary>
        /// Stable identity of the physical presentation instance associated
        /// with this candidate. The Core still validates and consumes the
        /// original response bytes/candidate index; RuntimeId only keeps the
        /// UI and network replica bound to the same physical copy.
        /// </summary>
        public ulong RuntimeId { get; internal set; }
        public string Label { get; internal set; }
        public uint CardCode { get; internal set; }
        public byte[] Response { get; internal set; }
        public bool HasLocation { get; internal set; }
        public byte Controller { get; internal set; }
        public byte Location { get; internal set; }
        public uint Sequence { get; internal set; }
        public uint Position { get; internal set; }
        public int ChoiceIndex { get; internal set; } = -1;
        /// <summary>
        /// Original candidate index emitted by ocgcore. This alias makes the
        /// effect-selection contract explicit without changing the response
        /// bytes used by existing prompt families.
        /// </summary>
        public int CandidateIndex => ChoiceIndex;
        public ulong DescriptionId { get; internal set; }
        public uint SumValue { get; internal set; }
    }

    public sealed class DuelPrompt
    {
        public ulong RequestId { get; internal set; }
        public CoreMessage Message { get; internal set; }
        public byte Player { get; internal set; }
        public string Title { get; internal set; }
        public bool Forced { get; internal set; }
        public bool Cancelable { get; internal set; }
        public uint MinimumSelections { get; internal set; }
        public uint MaximumSelections { get; internal set; }
        public uint RequiredSum { get; internal set; }
        public bool SumAtLeast { get; internal set; }
        public bool RequiresOrderedSelection { get; internal set; }
        public bool RequiresMaskSelection { get; internal set; }
        public ushort CounterType { get; internal set; }
        public ushort RequiredCounterCount { get; internal set; }
        public byte MaskWidth { get; internal set; }
        public List<uint> MandatorySums { get; } = new List<uint>();
        public List<DuelChoice> Choices { get; } = new List<DuelChoice>();
    }

    public sealed class DuelEvent
    {
        public CoreMessage Message { get; internal set; }
        public byte RawMessage { get; internal set; }
        public byte Player { get; internal set; }
        // Filled by a remote presentation mirror when the authoritative
        // snapshot may already have advanced beyond the event being replayed.
        public uint PresentationPhase { get; internal set; }
        public uint Value { get; internal set; }
        public uint Code { get; internal set; }
        public ushort CounterType { get; internal set; }
        public ulong HintValue { get; internal set; }
        // Stable effect description identifier emitted by ocgcore. Keeping
        // it on the event preserves which concrete effect entered the chain.
        public ulong DescriptionId { get; internal set; }
        public uint[] Codes { get; internal set; }
        public CardLocation Previous { get; internal set; }
        public CardLocation Current { get; internal set; }
        public CardLocation[] PreviousLocations { get; internal set; }
        public CardLocation[] CurrentLocations { get; internal set; }
        public int AttackerAttack { get; internal set; }
        public int AttackerDefense { get; internal set; }
        public int TargetAttack { get; internal set; }
        public int TargetDefense { get; internal set; }
        public bool AttackerDestroyed { get; internal set; }
        public bool TargetDestroyed { get; internal set; }
        public bool DirectAttack =>
            Current == null || Current.Location == 0;
        public DuelPrompt Prompt { get; internal set; }
        public string Detail { get; internal set; }
        public bool IsUnknown { get; internal set; }
    }

    public sealed class CoreProtocolException : Exception
    {
        public CoreProtocolException(string message) : base(message)
        {
        }
    }

    internal sealed class PacketReader
    {
        private readonly byte[] data;
        private readonly int end;
        private int cursor;

        internal PacketReader(byte[] data, int offset, int length)
        {
            this.data = data;
            cursor = offset;
            end = checked(offset + length);
            if (offset < 0 || length < 0 || end > data.Length)
            {
                throw new CoreProtocolException("Packet range is outside the native message copy.");
            }
        }

        internal int Remaining => end - cursor;

        internal byte Byte()
        {
            Require(1);
            return data[cursor++];
        }

        internal ushort UInt16()
        {
            Require(2);
            ushort value = (ushort)(data[cursor] | (data[cursor + 1] << 8));
            cursor += 2;
            return value;
        }

        internal uint UInt32()
        {
            Require(4);
            uint value = (uint)(data[cursor] |
                                (data[cursor + 1] << 8) |
                                (data[cursor + 2] << 16) |
                                (data[cursor + 3] << 24));
            cursor += 4;
            return value;
        }

        internal ulong UInt64()
        {
            ulong low = UInt32();
            ulong high = UInt32();
            return low | (high << 32);
        }

        internal CardLocation Location()
        {
            return new CardLocation
            {
                Controller = Byte(),
                Location = Byte(),
                Sequence = UInt32(),
                Position = UInt32()
            };
        }

        internal void Skip(int count)
        {
            Require(count);
            cursor += count;
        }

        private void Require(int count)
        {
            if (count < 0 || cursor + count > end)
            {
                throw new CoreProtocolException(
                    $"Message ended unexpectedly at byte {cursor}; needed {count}, remaining {Remaining}.");
            }
        }
    }

    public static class CoreMessageDecoder
    {
        public static List<DuelEvent> Decode(
            byte[] nativeCopy,
            IEnumerable<CardRecord> announceCardCandidates = null)
        {
            var events = new List<DuelEvent>();
            CardRecord[] candidates = announceCardCandidates?.ToArray() ??
                                      Array.Empty<CardRecord>();
            int offset = 0;
            while (offset < nativeCopy.Length)
            {
                if (nativeCopy.Length - offset < 4)
                {
                    throw new CoreProtocolException("Native message stream ended inside a packet length.");
                }
                uint size = ReadUInt32(nativeCopy, offset);
                offset += 4;
                if (size < 1 || size > int.MaxValue || offset + size > nativeCopy.Length)
                {
                    throw new CoreProtocolException($"Invalid ocgcore packet size {size}.");
                }
                byte message = nativeCopy[offset];
                var reader = new PacketReader(nativeCopy, offset + 1, (int)size - 1);
                events.Add(DecodePacket(message, reader, candidates));
                offset += (int)size;
            }
            return events;
        }

        private static DuelEvent DecodePacket(
            byte raw,
            PacketReader reader,
            IReadOnlyList<CardRecord> announceCardCandidates)
        {
            CoreMessage message = (CoreMessage)raw;
            var result = new DuelEvent { Message = message, RawMessage = raw };
            switch (message)
            {
                case CoreMessage.Retry:
                    result.Detail = "O core recusou a resposta anterior.";
                    break;
                case CoreMessage.Hint:
                    result.Value = reader.Byte();
                    result.Player = reader.Byte();
                    if (reader.Remaining >= 8) reader.UInt64();
                    result.Detail = "Indicação de regra recebida.";
                    break;
                case CoreMessage.Waiting:
                    result.Detail = "Aguardando o outro duelista.";
                    break;
                case CoreMessage.Start:
                    result.Detail = "Estado inicial do duelo recebido.";
                    reader.Skip(reader.Remaining);
                    break;
                case CoreMessage.NewTurn:
                    result.Player = reader.Byte();
                    result.Detail = $"Turno do duelista {result.Player + 1}";
                    break;
                case CoreMessage.NewPhase:
                    result.Value = reader.UInt16();
                    result.Detail = PhaseName(result.Value);
                    break;
                case CoreMessage.Win:
                    result.Player = reader.Byte();
                    result.Value = reader.Byte();
                    result.Detail = $"Duelista {result.Player + 1} venceu";
                    break;
                case CoreMessage.Draw:
                    DecodeDraw(reader, result);
                    break;
                case CoreMessage.Move:
                    result.Code = reader.UInt32();
                    result.Previous = reader.Location();
                    result.Current = reader.Location();
                    result.Value = reader.UInt32();
                    break;
                case CoreMessage.Damage:
                case CoreMessage.Recover:
                case CoreMessage.LifePointsUpdate:
                    result.Player = reader.Byte();
                    result.Value = reader.UInt32();
                    break;
                case CoreMessage.PayLifePointCost:
                    result.Player = reader.Byte();
                    result.Value = reader.UInt32();
                    result.Detail =
                        $"Duelista {result.Player + 1} pagou {result.Value} PV.";
                    break;
                case CoreMessage.Summoning:
                case CoreMessage.SpecialSummoning:
                case CoreMessage.FlipSummoning:
                    result.Code = reader.UInt32();
                    result.Current = reader.Location();
                    break;
                case CoreMessage.Summoned:
                case CoreMessage.SpecialSummoned:
                case CoreMessage.FlipSummoned:
                case CoreMessage.ChainEnd:
                case CoreMessage.AttackDisabled:
                    break;
                case CoreMessage.Chaining:
                    result.Code = reader.UInt32();
                    result.Current = reader.Location();
                    result.Player = result.Current.Controller;
                    reader.Skip(2 + 4);
                    result.DescriptionId = reader.UInt64();
                    result.Value = reader.UInt32();
                    break;
                case CoreMessage.Chained:
                case CoreMessage.ChainSolving:
                case CoreMessage.ChainSolved:
                case CoreMessage.ChainNegated:
                case CoreMessage.ChainDisabled:
                    result.Value = reader.Byte();
                    break;
                case CoreMessage.SelectIdleCommand:
                    result.Prompt = DecodeIdle(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectBattleCommand:
                    result.Prompt = DecodeBattle(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectEffectYesNo:
                    result.Prompt = DecodeEffectYesNo(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectYesNo:
                    result.Prompt = DecodeYesNo(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectOption:
                    result.Prompt = DecodeOptions(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectCard:
                    result.Prompt = DecodeCardSelection(reader, false);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectTribute:
                    result.Prompt = DecodeCardSelection(reader, true);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectUnselectCard:
                    result.Prompt = DecodeUnselectCard(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectSum:
                    result.Prompt = DecodeSum(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectCounter:
                    result.Prompt = DecodeCounter(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SortCard:
                case CoreMessage.SortChain:
                    result.Prompt = DecodeSort(reader, message);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.AnnounceRace:
                case CoreMessage.AnnounceAttribute:
                    result.Prompt = DecodeAnnounceMask(reader, message);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.AnnounceCard:
                    result.Prompt = DecodeAnnounceCard(
                        reader,
                        announceCardCandidates);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.AnnounceNumber:
                    result.Prompt = DecodeAnnounceNumber(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectChain:
                    result.Prompt = DecodeChain(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectPlace:
                case CoreMessage.SelectDisableField:
                    result.Prompt = DecodePlace(reader, message);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.SelectPosition:
                    result.Prompt = DecodePosition(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.PositionChange:
                    DecodePositionChange(reader, result);
                    break;
                case CoreMessage.Swap:
                    result.Code = reader.UInt32();
                    result.Previous = reader.Location();
                    result.Codes = new[] { reader.UInt32() };
                    result.Current = reader.Location();
                    result.Detail = "Duas cartas trocaram de posição ou controle.";
                    break;
                case CoreMessage.ShuffleSetCard:
                    DecodeShuffleSetCards(reader, result);
                    break;
                case CoreMessage.SwapGraveDeck:
                    DecodeSwapGraveDeck(reader, result);
                    break;
                case CoreMessage.RemoveCards:
                    DecodeRemoveCards(reader, result);
                    break;
                case CoreMessage.Attack:
                    DecodeAttack(reader, result);
                    break;
                case CoreMessage.Battle:
                    DecodeBattleEvent(reader, result);
                    break;
                case CoreMessage.ConfirmDeckTop:
                case CoreMessage.ConfirmCards:
                case CoreMessage.ShuffleDeck:
                case CoreMessage.RefreshDeck:
                case CoreMessage.ReverseDeck:
                case CoreMessage.DeckTop:
                case CoreMessage.ShuffleExtra:
                case CoreMessage.ConfirmExtraTop:
                case CoreMessage.Set:
                case CoreMessage.CardSelected:
                case CoreMessage.RandomSelected:
                case CoreMessage.DamageStepStart:
                case CoreMessage.DamageStepEnd:
                case CoreMessage.BeChainTarget:
                    result.Detail = $"Evento de duelo {message}.";
                    reader.Skip(reader.Remaining);
                    break;
                case CoreMessage.FieldDisabled:
                    DecodeFieldDisabled(reader, result);
                    break;
                case CoreMessage.BecomeTarget:
                    DecodeBecomeTarget(reader, result);
                    break;
                case CoreMessage.Equip:
                    DecodeEquip(reader, result);
                    break;
                case CoreMessage.Unequip:
                    DecodeUnequip(reader, result);
                    break;
                case CoreMessage.CardTarget:
                    DecodeCardTarget(reader, result, "selecionou como");
                    break;
                case CoreMessage.CancelTarget:
                    DecodeCardTarget(reader, result, "cancelou");
                    break;
                case CoreMessage.AddCounter:
                    DecodeCounter(reader, result, "adicionado(s)");
                    break;
                case CoreMessage.RemoveCounter:
                    DecodeCounter(reader, result, "removido(s)");
                    break;
                case CoreMessage.MissedEffect:
                    DecodeMissedEffect(reader, result);
                    break;
                case CoreMessage.CreateRelation:
                    DecodeRelation(reader, result, "criada");
                    break;
                case CoreMessage.ReleaseRelation:
                    DecodeRelation(reader, result, "removida");
                    break;
                case CoreMessage.TossCoin:
                    DecodeRandom(reader, result, "moeda(s)");
                    break;
                case CoreMessage.TossDice:
                    DecodeRandom(reader, result, "dado(s)");
                    break;
                case CoreMessage.HandResult:
                    DecodeHandResult(reader, result);
                    break;
                case CoreMessage.RockPaperScissors:
                    result.Prompt = DecodeRockPaperScissors(reader);
                    result.Player = result.Prompt.Player;
                    break;
                case CoreMessage.CardHint:
                    DecodeCardHint(reader, result);
                    break;
                case CoreMessage.PlayerHint:
                    DecodePlayerHint(reader, result);
                    break;
                case CoreMessage.TagSwap:
                case CoreMessage.ReloadField:
                    // These synchronization packets belong to tag/reload
                    // duel modes. Card12 is a two-seat Master Rule duel, but
                    // decoding them as known packets keeps the protocol
                    // complete if the pinned Core emits one during recovery.
                    result.Detail = $"Evento de sincronização {message}.";
                    reader.Skip(reader.Remaining);
                    break;
                case CoreMessage.MatchKill:
                    result.Code = reader.UInt32();
                    result.Detail =
                        $"Vitória de Match causada por {result.Code:00000000}.";
                    break;
                case CoreMessage.ShuffleHand:
                    DecodeShuffleHand(reader, result);
                    break;
                default:
                    result.IsUnknown = true;
                    result.Detail = $"Mensagem ocgcore {raw} ainda não projetada ({reader.Remaining} bytes).";
                    break;
            }
            return result;
        }

        private static void DecodeShuffleSetCards(
            PacketReader reader,
            DuelEvent result)
        {
            result.Value = reader.Byte();
            int count = (int)GuardCount(
                reader.Byte(),
                32,
                "set-card shuffle count");
            result.PreviousLocations = new CardLocation[count];
            result.CurrentLocations = new CardLocation[count];
            for (int index = 0; index < count; index++)
                result.PreviousLocations[index] = reader.Location();
            for (int index = 0; index < count; index++)
                result.CurrentLocations[index] = reader.Location();
            result.Detail = $"{count} carta(s) baixada(s) foram embaralhadas.";
        }

        private static void DecodeSwapGraveDeck(
            PacketReader reader,
            DuelEvent result)
        {
            result.Player = reader.Byte();
            result.Value = reader.UInt32();
            uint bitfieldSize = GuardCount(
                reader.UInt32(),
                256,
                "grave/deck swap bitfield");
            if (reader.Remaining != bitfieldSize)
            {
                throw new CoreProtocolException(
                    "MSG_SWAP_GRAVE_DECK bitfield length does not match its header.");
            }
            result.Codes = new uint[bitfieldSize];
            for (int index = 0; index < result.Codes.Length; index++)
                result.Codes[index] = reader.Byte();
            result.Detail =
                $"Duelista {result.Player + 1} trocou o Deck pelo Cemitério.";
        }

        private static void DecodeRemoveCards(
            PacketReader reader,
            DuelEvent result)
        {
            int count = (int)GuardCount(
                reader.UInt32(),
                255,
                "removed cards");
            result.PreviousLocations = new CardLocation[count];
            for (int index = 0; index < count; index++)
                result.PreviousLocations[index] = reader.Location();
            result.Detail = $"{count} carta(s) foram removida(s) do duelo.";
        }

        private static void DecodeAttack(
            PacketReader reader,
            DuelEvent result)
        {
            result.Previous = reader.Location();
            result.Current = reader.Location();
            result.Player = result.Previous.Controller;
            result.Detail = result.DirectAttack
                ? $"Duelista {result.Player + 1} declarou um ataque direto."
                : $"Duelista {result.Player + 1} declarou um ataque " +
                  $"contra a zona {result.Current.Sequence + 1}.";
        }

        private static void DecodeBattleEvent(
            PacketReader reader,
            DuelEvent result)
        {
            result.Previous = reader.Location();
            result.AttackerAttack = unchecked((int)reader.UInt32());
            result.AttackerDefense = unchecked((int)reader.UInt32());
            result.AttackerDestroyed = reader.Byte() != 0;
            result.Current = reader.Location();
            result.TargetAttack = unchecked((int)reader.UInt32());
            result.TargetDefense = unchecked((int)reader.UInt32());
            result.TargetDestroyed = reader.Byte() != 0;
            result.Player = result.Previous.Controller;
            result.Detail = result.DirectAttack
                ? $"Ataque direto com {result.AttackerAttack} ATK."
                : $"Batalha: {result.AttackerAttack} ATK contra " +
                  $"{Math.Max(result.TargetAttack, result.TargetDefense)}.";
        }

        private static void DecodePositionChange(
            PacketReader reader,
            DuelEvent result)
        {
            const int fullPayloadSize = 9;
            if (reader.Remaining < fullPayloadSize)
            {
                result.Detail =
                    $"Mudança de posição recebida com payload parcial " +
                    $"({reader.Remaining}/{fullPayloadSize} bytes).";
                reader.Skip(reader.Remaining);
                return;
            }

            result.Code = reader.UInt32();
            byte controller = reader.Byte();
            byte location = reader.Byte();
            byte sequence = reader.Byte();
            byte previousPosition = reader.Byte();
            byte currentPosition = reader.Byte();
            result.Player = controller;
            result.Previous = new CardLocation
            {
                Controller = controller,
                Location = location,
                Sequence = sequence,
                Position = previousPosition
            };
            result.Current = new CardLocation
            {
                Controller = controller,
                Location = location,
                Sequence = sequence,
                Position = currentPosition
            };
            result.Detail =
                $"Posição de {result.Code:00000000}: " +
                $"{previousPosition:X2} → {currentPosition:X2}.";
        }

        private static void DecodeDraw(PacketReader reader, DuelEvent result)
        {
            result.Player = reader.Byte();
            uint count = reader.UInt32();
            if (count > 80)
            {
                throw new CoreProtocolException($"Impossible draw count {count}.");
            }
            result.Codes = new uint[count];
            for (int i = 0; i < count; i++)
            {
                result.Codes[i] = reader.UInt32();
                reader.UInt32();
            }
        }

        private static void DecodeShuffleHand(
            PacketReader reader,
            DuelEvent result)
        {
            result.Player = reader.Byte();
            uint count = GuardCount(
                reader.UInt32(),
                80,
                "shuffled hand cards");
            result.Codes = new uint[count];
            for (int index = 0; index < count; index++)
                result.Codes[index] = reader.UInt32();
            result.Detail =
                $"A mão do Duelista {result.Player + 1} foi reordenada.";
        }

        private static void DecodeEquip(PacketReader reader, DuelEvent result)
        {
            result.Previous = reader.Location();
            result.Current = reader.Location();
            result.Detail = $"Uma carta foi equipada.";
        }

        private static void DecodeUnequip(PacketReader reader, DuelEvent result)
        {
            result.Previous = reader.Location();
            result.Detail = $"Uma carta foi desequipada.";
        }

        private static void DecodeCardTarget(PacketReader reader, DuelEvent result, string action)
        {
            result.Previous = reader.Location();
            result.Current = reader.Location();
            result.Detail = $"Uma carta {action} um alvo.";
        }

        private static void DecodeBecomeTarget(PacketReader reader, DuelEvent result)
        {
            uint count = GuardCount(
                reader.UInt32(),
                255,
                "target cards");
            result.CurrentLocations = new CardLocation[count];
            for (int i = 0; i < count; i++)
            {
                result.CurrentLocations[i] = reader.Location();
            }
            result.Detail = $"{count} carta(s) se tornou/tornaram alvo.";
        }

        private static void DecodeCounter(PacketReader reader, DuelEvent result, string action)
        {
            result.CounterType = reader.UInt16();
            result.Code = result.CounterType;
            byte controller = reader.Byte();
            byte location = reader.Byte();
            byte sequence = reader.Byte();
            result.Current = new CardLocation
            {
                Controller = controller,
                Location = location,
                Sequence = sequence,
                Position = 0
            };
            result.Value = reader.UInt16();
            result.Detail = $"{result.Value} marcador(es) ({result.Code:X4}) {action}.";
        }

        private static void DecodeFieldDisabled(PacketReader reader, DuelEvent result)
        {
            result.Value = reader.UInt32();
            result.Detail = $"Zonas desativadas mask: {result.Value:X8}.";
        }

        private static void DecodeRelation(PacketReader reader, DuelEvent result, string action)
        {
            result.Previous = reader.Location();
            result.Current = reader.Location();
            result.Detail = $"Relação {action}.";
        }

        private static void DecodeRandom(PacketReader reader, DuelEvent result, string item)
        {
            result.Player = reader.Byte();
            uint count = reader.Byte();
            result.Codes = new uint[count];
            for (int i = 0; i < count; i++)
                result.Codes[i] = reader.Byte();
            result.Detail = $"O Duelista {result.Player + 1} rolou {count} {item}.";
        }

        private static void DecodeHandResult(PacketReader reader, DuelEvent result)
        {
            result.Value = reader.Byte();
            result.Detail = $"Resultado de Jankenpon: {result.Value}.";
        }

        private static DuelPrompt DecodeRockPaperScissors(
            PacketReader reader)
        {
            byte player = reader.Byte();
            var prompt = NewPrompt(
                CoreMessage.RockPaperScissors,
                player,
                "Escolha pedra, papel ou tesoura");
            prompt.Forced = true;
            prompt.MinimumSelections = 1;
            prompt.MaximumSelections = 1;
            prompt.Choices.Add(Choice("Pedra", 0, IntResponse(1)));
            prompt.Choices.Add(Choice("Tesoura", 0, IntResponse(2)));
            prompt.Choices.Add(Choice("Papel", 0, IntResponse(3)));
            return prompt;
        }

        private static void DecodeCardHint(PacketReader reader, DuelEvent result)
        {
            result.Current = reader.Location();
            result.Code = reader.Byte();
            result.HintValue = reader.UInt64();
            result.Value = unchecked((uint)result.HintValue);
            result.Detail = $"Hint ({result.Code}) aplicado na carta.";
        }

        private static void DecodePlayerHint(PacketReader reader, DuelEvent result)
        {
            result.Player = reader.Byte();
            result.Code = reader.Byte();
            result.HintValue = reader.UInt64();
            result.Value = unchecked((uint)result.HintValue);
            result.Detail = $"Hint ({result.Code}) aplicado ao jogador {result.Player + 1}.";
        }

        private static void DecodeMissedEffect(PacketReader reader, DuelEvent result)
        {
            result.Current = reader.Location();
            result.Code = reader.UInt32();
            result.Detail = $"Efeito {result.Code:00000000} perdido.";
        }

        private static DuelPrompt DecodeIdle(PacketReader reader)
        {
            var prompt = NewPrompt(CoreMessage.SelectIdleCommand, reader.Byte(), "Escolha uma ação");
            ReadCommandCards(reader, prompt, "Invocar", 0, false);
            ReadCommandCards(reader, prompt, "Invocação especial", 1, false);
            ReadCommandCards(reader, prompt, "Mudar posição", 2, true);
            ReadCommandCards(reader, prompt, "Baixar monstro", 3, false);
            ReadCommandCards(reader, prompt, "Baixar magia/armadilha", 4, false);
            ReadActivations(reader, prompt, "Ativar", 5);
            bool battle = reader.Byte() != 0;
            bool end = reader.Byte() != 0;
            bool shuffle = reader.Byte() != 0;
            if (battle) prompt.Choices.Add(Choice("Entrar na Fase de Batalha", 0, IntResponse(6)));
            if (end) prompt.Choices.Add(Choice("Encerrar turno", 0, IntResponse(7)));
            if (shuffle) prompt.Choices.Add(Choice("Embaralhar a mão", 0, IntResponse(8)));
            return prompt;
        }

        private static DuelPrompt DecodeBattle(PacketReader reader)
        {
            var prompt = NewPrompt(CoreMessage.SelectBattleCommand, reader.Byte(), "Fase de Batalha");
            ReadActivations(reader, prompt, "Ativar", 0);
            uint count = GuardCount(reader.UInt32(), 80, "attack choices");
            for (int i = 0; i < count; i++)
            {
                uint code = reader.UInt32();
                byte controller = reader.Byte();
                byte location = reader.Byte();
                uint sequence = reader.Byte();
                reader.Byte();
                prompt.Choices.Add(Choice(
                    "Atacar",
                    code,
                    IntResponse((i << 16) + 1),
                    controller,
                    location,
                    sequence,
                    i));
            }
            bool main2 = reader.Byte() != 0;
            bool end = reader.Byte() != 0;
            if (main2) prompt.Choices.Add(Choice("Ir para a Fase Principal 2", 0, IntResponse(2)));
            if (end) prompt.Choices.Add(Choice("Encerrar turno", 0, IntResponse(3)));
            return prompt;
        }

        private static DuelPrompt DecodeEffectYesNo(PacketReader reader)
        {
            byte player = reader.Byte();
            uint code = reader.UInt32();
            CardLocation location = reader.Location();
            ulong description = reader.UInt64();
            var prompt = NewPrompt(CoreMessage.SelectEffectYesNo, player, "Ativar efeito?");
            DuelChoice activate = Choice(
                "Ativar efeito",
                code,
                IntResponse(1),
                location.Controller,
                location.Location,
                location.Sequence,
                0,
                position: location.Position);
            activate.DescriptionId = description;
            prompt.Choices.Add(activate);
            DuelChoice decline = Choice(
                "Não ativar",
                code,
                IntResponse(0),
                location.Controller,
                location.Location,
                location.Sequence,
                position: location.Position);
            prompt.Choices.Add(decline);
            return prompt;
        }

        private static DuelPrompt DecodeYesNo(PacketReader reader)
        {
            byte player = reader.Byte();
            ulong description = reader.UInt64();
            var prompt = NewPrompt(CoreMessage.SelectYesNo, player, "Confirmar ação?");
            DuelChoice confirm = Choice(
                "Sim",
                0,
                IntResponse(1),
                choiceIndex: 0);
            confirm.DescriptionId = description;
            prompt.Choices.Add(confirm);
            prompt.Choices.Add(Choice("Não", 0, IntResponse(0)));
            return prompt;
        }

        private static DuelPrompt DecodeOptions(PacketReader reader)
        {
            byte player = reader.Byte();
            var prompt = NewPrompt(CoreMessage.SelectOption, player, "Escolha uma opção");
            uint count = GuardCount(reader.Byte(), 64, "options");
            for (int i = 0; i < count; i++)
            {
                ulong description = reader.UInt64();
                DuelChoice choice = Choice(
                    $"Opção {i + 1} · {description}",
                    0,
                    IntResponse(i));
                choice.DescriptionId = description;
                prompt.Choices.Add(choice);
            }
            return prompt;
        }

        private static DuelPrompt DecodeCardSelection(PacketReader reader, bool tribute)
        {
            byte player = reader.Byte();
            bool cancelable = reader.Byte() != 0;
            uint minimum = GuardCount(reader.UInt32(), 80, "selection minimum");
            uint maximum = GuardCount(reader.UInt32(), 80, "selection maximum");
            uint count = GuardCount(reader.UInt32(), 200, "selectable cards");
            var prompt = NewPrompt(
                tribute ? CoreMessage.SelectTribute : CoreMessage.SelectCard,
                player,
                tribute ? "Escolha os tributos" : "Escolha uma carta");
            prompt.Forced = !cancelable;
            prompt.Cancelable = cancelable;
            prompt.MinimumSelections = minimum;
            prompt.MaximumSelections = maximum;
            for (int i = 0; i < count; i++)
            {
                uint code = reader.UInt32();
                byte controller = reader.Byte();
                byte location = reader.Byte();
                uint sequence = reader.UInt32();
                uint selectionValue = tribute
                    ? reader.Byte()
                    : reader.UInt32();
                DuelChoice choice = Choice(
                    $"Selecionar carta {i + 1}",
                    code,
                    CardSelectionResponse(new[] { (uint)i }),
                    controller,
                    location,
                    sequence,
                    i);
                if (tribute)
                {
                    // MSG_SELECT_TRIBUTE uses min as the required tribute
                    // value. A single card can contribute two or three.
                    choice.SumValue = selectionValue;
                }
                else
                {
                    choice.Position = selectionValue;
                }
                prompt.Choices.Add(choice);
            }
            if (minimum == 0 || cancelable)
            {
                prompt.Choices.Add(Choice(
                    "Cancelar",
                    0,
                    IntResponse(-1)));
            }
            if (minimum > 1 && count >= minimum)
            {
                var indexes = new uint[minimum];
                for (uint i = 0; i < minimum; i++) indexes[i] = i;
                prompt.Choices.Insert(0, Choice($"Selecionar as primeiras {minimum}", 0, CardSelectionResponse(indexes)));
            }
            if (maximum == 0) prompt.Choices.Clear();
            return prompt;
        }

        private static DuelPrompt DecodeUnselectCard(PacketReader reader)
        {
            byte player = reader.Byte();
            bool finishable = reader.Byte() != 0;
            bool cancelable = reader.Byte() != 0;
            uint minimum = GuardCount(reader.UInt32(), 80, "iterative selection minimum");
            uint maximum = GuardCount(reader.UInt32(), 80, "iterative selection maximum");
            var prompt = NewPrompt(
                CoreMessage.SelectUnselectCard,
                player,
                "Escolha ou remova uma carta");
            prompt.Forced = !cancelable;
            prompt.Cancelable = cancelable;
            prompt.MinimumSelections = minimum;
            prompt.MaximumSelections = maximum;

            uint selectable = GuardCount(
                reader.UInt32(),
                200,
                "iterative selectable cards");
            for (int index = 0; index < selectable; index++)
            {
                uint code = reader.UInt32();
                CardLocation location = reader.Location();
                prompt.Choices.Add(Choice(
                    "Selecionar",
                    code,
                    PairResponse(1, index),
                    location.Controller,
                    location.Location,
                    location.Sequence,
                    index,
                    location.Position));
            }

            uint selected = GuardCount(
                reader.UInt32(),
                200,
                "iterative selected cards");
            for (int index = 0; index < selected; index++)
            {
                uint code = reader.UInt32();
                CardLocation location = reader.Location();
                prompt.Choices.Add(Choice(
                    "Remover da seleção",
                    code,
                    PairResponse(1, checked((int)selectable + index)),
                    location.Controller,
                    location.Location,
                    location.Sequence,
                    checked((int)selectable + index),
                    location.Position));
            }

            if (finishable)
            {
                prompt.Choices.Add(Choice(
                    "Concluir seleção",
                    0,
                    IntResponse(-1)));
            }
            else if (cancelable)
            {
                prompt.Choices.Add(Choice(
                    "Cancelar",
                    0,
                    IntResponse(-1)));
            }
            return prompt;
        }

        private static DuelPrompt DecodeSum(PacketReader reader)
        {
            byte player = reader.Byte();
            bool atLeast = reader.Byte() != 0;
            uint required = reader.UInt32();
            uint minimum = GuardCount(reader.UInt32(), 80, "sum selection minimum");
            uint maximum = GuardCount(reader.UInt32(), 80, "sum selection maximum");
            var prompt = NewPrompt(
                CoreMessage.SelectSum,
                player,
                atLeast
                    ? $"Escolha materiais com soma mínima {required}"
                    : $"Escolha materiais com soma {required}");
            prompt.MinimumSelections = minimum;
            prompt.MaximumSelections = maximum;
            prompt.RequiredSum = required;
            prompt.SumAtLeast = atLeast;
            prompt.Forced = true;

            uint mandatory = GuardCount(
                reader.UInt32(),
                80,
                "mandatory sum cards");
            for (int index = 0; index < mandatory; index++)
            {
                reader.UInt32();
                reader.Location();
                prompt.MandatorySums.Add(reader.UInt32());
            }

            uint selectable = GuardCount(
                reader.UInt32(),
                200,
                "sum selectable cards");
            for (int index = 0; index < selectable; index++)
            {
                uint code = reader.UInt32();
                CardLocation location = reader.Location();
                uint sumValue = reader.UInt32();
                DuelChoice choice = Choice(
                    "Selecionar material",
                    code,
                    CardSelectionResponse(new[] { (uint)index }),
                    location.Controller,
                    location.Location,
                    location.Sequence,
                    index,
                    location.Position);
                choice.SumValue = sumValue;
                prompt.Choices.Add(choice);
            }
            if (atLeast && maximum == 0)
            {
                // SelectWithSumGreater encodes max=0 to mean "no explicit
                // card-count limit". Expose the selectable count as the UI
                // ceiling while retaining SumAtLeast for Core validation.
                prompt.MaximumSelections = selectable;
                if (IsValidSelection(prompt, Array.Empty<int>()))
                {
                    prompt.Choices.Add(Choice(
                        "Confirmar sem material adicional",
                        0,
                        CardSelectionResponse(Array.Empty<uint>())));
                }
            }
            return prompt;
        }

        private static DuelPrompt DecodeCounter(PacketReader reader)
        {
            byte player = reader.Byte();
            ushort counterType = reader.UInt16();
            ushort required = reader.UInt16();
            uint count = GuardCount(
                reader.UInt32(),
                200,
                "counter selection cards");
            var capacities = new ushort[count];
            var locations = new CardLocation[count];
            for (int index = 0; index < count; index++)
            {
                reader.UInt32(); // Identity is not needed to encode the response.
                locations[index] = new CardLocation
                {
                    Controller = reader.Byte(),
                    Location = reader.Byte(),
                    Sequence = reader.Byte(),
                    Position = 0
                };
                capacities[index] = reader.UInt16();
            }

            var prompt = NewPrompt(
                CoreMessage.SelectCounter,
                player,
                $"Distribua {required} contador(es) do tipo {counterType}");
            prompt.Forced = true;
            prompt.CounterType = counterType;
            prompt.RequiredCounterCount = required;
            prompt.MinimumSelections = 0;
            prompt.MaximumSelections = count;

            ushort[] automatic = new ushort[capacities.Length];
            int remaining = required;
            for (int index = 0;
                 index < capacities.Length && remaining > 0;
                 index++)
            {
                automatic[index] = (ushort)Math.Min(
                    capacities[index],
                    remaining);
                remaining -= automatic[index];
            }
            prompt.Choices.Add(Choice(
                "Distribuir automaticamente",
                0,
                CounterResponse(automatic)));
            for (int index = 0; index < capacities.Length; index++)
            {
                CardLocation location = locations[index];
                DuelChoice choice = Choice(
                    $"{(location.Controller == player ? "Sua zona" : "Zona do rival")} " +
                    $"{location.Sequence + 1} - capacidade {capacities[index]}",
                    0,
                    Array.Empty<byte>(),
                    location.Controller,
                    location.Location,
                    location.Sequence,
                    index);
                choice.SumValue = capacities[index];
                prompt.Choices.Add(choice);
            }
            return prompt;
        }

        public static byte[] CounterResponse(
            IReadOnlyList<ushort> allocation)
        {
            var response = new byte[(allocation?.Count ?? 0) * 2];
            for (int index = 0; index < (allocation?.Count ?? 0); index++)
            {
                response[index * 2] = (byte)allocation[index];
                response[index * 2 + 1] = (byte)(allocation[index] >> 8);
            }
            return response;
        }

        public static byte[] OrderedSelectionResponse(
            IEnumerable<int> orderedIndexes)
        {
            int[] order = orderedIndexes?.ToArray() ?? Array.Empty<int>();
            if (order.Length > byte.MaxValue ||
                order.Any(index => index < 0 || index >= order.Length) ||
                order.Distinct().Count() != order.Length)
            {
                throw new ArgumentException(
                    "A sort response must contain each original candidate " +
                    "index exactly once.",
                    nameof(orderedIndexes));
            }

            // ocgcore's SortCard/SortChain response is indexed by the
            // original candidate and stores that candidate's final rank.
            // The presenter collects the inverse representation (candidate
            // indexes in the desired visual order), so convert it here.
            var rankByOriginalCandidate = new byte[order.Length];
            for (int rank = 0; rank < order.Length; rank++)
                rankByOriginalCandidate[order[rank]] = (byte)rank;
            return rankByOriginalCandidate;
        }

        public static byte[] AnnounceMaskResponse(
            DuelPrompt prompt,
            IEnumerable<int> selectedBits)
        {
            if (prompt == null || !prompt.RequiresMaskSelection)
                throw new ArgumentException(
                    "The prompt does not require a combined announce mask.",
                    nameof(prompt));
            int[] bits = selectedBits?.Distinct().ToArray() ??
                         Array.Empty<int>();
            if (bits.Length != prompt.MaximumSelections ||
                bits.Any(bit => bit < 0 || bit >= prompt.MaskWidth))
            {
                throw new ArgumentException(
                    "The selected announce bits do not satisfy the prompt.",
                    nameof(selectedBits));
            }
            ulong mask = 0;
            foreach (int bit in bits)
                mask |= 1UL << bit;
            return prompt.Message == CoreMessage.AnnounceRace
                ? UInt64Response(mask)
                : IntResponse(unchecked((int)mask));
        }

        private static DuelPrompt DecodeSort(
            PacketReader reader,
            CoreMessage message)
        {
            byte player = reader.Byte();
            // The Core may legally ask to order more than eight cards (for
            // example, a large Deck-bottom operation). The typed presenter
            // collects the requested order directly instead of enumerating
            // every permutation.
            uint count = GuardCount(reader.UInt32(), 200, "sort cards");
            var names = new List<string>();
            var cards = new List<uint>();
            var controllers = new List<byte>();
            var locations = new List<byte>();
            var sequences = new List<uint>();
            for (int index = 0; index < count; index++)
            {
                uint code = reader.UInt32();
                byte controller = reader.Byte();
                byte location = checked((byte)reader.UInt32());
                uint sequence = reader.UInt32();
                names.Add(code.ToString("00000000"));
                cards.Add(code);
                controllers.Add(controller);
                locations.Add(location);
                sequences.Add(sequence);
            }

            var prompt = NewPrompt(
                message,
                player,
                message == CoreMessage.SortChain
                    ? "Ordene os efeitos da Corrente"
                    : "Ordene as cartas do topo do Deck");
            if (count == 0)
            {
                prompt.Choices.Add(Choice(
                    "Manter ordem",
                    0,
                    new byte[] { 0xFF }));
                return prompt;
            }

            prompt.RequiresOrderedSelection = true;
            prompt.MinimumSelections = count;
            prompt.MaximumSelections = count;
            prompt.Choices.Add(Choice(
                "Manter ordem atual",
                0,
                new byte[] { 0xFF }));
            for (int index = 0; index < count; index++)
            {
                prompt.Choices.Add(Choice(
                    $"{index + 1}. {names[index]}",
                    cards[index],
                    Array.Empty<byte>(),
                    controllers[index],
                    locations[index],
                    sequences[index],
                    index));
            }
            return prompt;
        }

        private static DuelPrompt DecodeAnnounceMask(
            PacketReader reader,
            CoreMessage message)
        {
            byte player = reader.Byte();
            uint count = GuardCount(reader.Byte(), 8, "announced mask count");
            bool race = message == CoreMessage.AnnounceRace;
            ulong available = race
                ? reader.UInt64()
                : reader.UInt32();
            var prompt = NewPrompt(
                message,
                player,
                race
                    ? "Declare um Tipo de monstro"
                    : "Declare um Atributo");
            prompt.MinimumSelections = count;
            prompt.MaximumSelections = count;
            prompt.RequiresMaskSelection = count > 1;
            prompt.MaskWidth = (byte)(race ? 64 : 32);

            string[] labels = race
                ? new[]
                {
                    "Guerreiro", "Mago", "Fada", "Demônio", "Zumbi",
                    "Máquina", "Aqua", "Piro", "Rocha", "Besta Alada",
                    "Planta", "Inseto", "Trovão", "Dragão", "Besta",
                    "Besta-Guerreira", "Dinossauro", "Peixe",
                    "Serpente Marinha", "Réptil", "Psíquico",
                    "Besta Divina", "Deus Criador", "Wyrm",
                    "Ciberso", "Ilusão"
                }
                : new[]
                {
                    "TERRA", "ÁGUA", "FOGO", "VENTO",
                    "LUZ", "TREVAS", "DIVINO"
                };
            var bits = new List<int>();
            int width = race ? 64 : 32;
            for (int bit = 0; bit < width; bit++)
            {
                if ((available & (1UL << bit)) != 0) bits.Add(bit);
            }
            if (count > 1)
            {
                ulong automaticMask = 0;
                foreach (int bit in bits.Take(checked((int)count)))
                    automaticMask |= 1UL << bit;
                prompt.Choices.Add(Choice(
                    "Declarar primeira combinação válida",
                    0,
                    race
                        ? UInt64Response(automaticMask)
                        : IntResponse(unchecked((int)automaticMask))));
            }
            foreach (int bit in bits)
            {
                ulong mask = 1UL << bit;
                prompt.Choices.Add(Choice(
                    bit < labels.Length
                        ? labels[bit]
                        : $"TIPO {bit + 1}",
                    0,
                    race
                        ? UInt64Response(mask)
                        : IntResponse(unchecked((int)mask)),
                    choiceIndex: bit));
            }
            return prompt;
        }

        private static DuelPrompt DecodeAnnounceNumber(PacketReader reader)
        {
            byte player = reader.Byte();
            uint count = GuardCount(reader.Byte(), 64, "announced numbers");
            var prompt = NewPrompt(
                CoreMessage.AnnounceNumber,
                player,
                "Declare um número");
            for (int index = 0; index < count; index++)
            {
                ulong number = reader.UInt64();
                prompt.Choices.Add(Choice(
                    number.ToString(),
                    0,
                    IntResponse(index)));
            }
            return prompt;
        }

        private static DuelPrompt DecodeAnnounceCard(
            PacketReader reader,
            IReadOnlyList<CardRecord> candidates)
        {
            byte player = reader.Byte();
            uint count = GuardCount(reader.Byte(), 255, "announce-card opcodes");
            var opcodes = new ulong[count];
            for (int index = 0; index < count; index++)
                opcodes[index] = reader.UInt64();

            var prompt = NewPrompt(
                CoreMessage.AnnounceCard,
                player,
                "Declare o nome de uma carta");
            prompt.MinimumSelections = 1;
            prompt.MaximumSelections = 1;
            CardRecord[] matchingCards = candidates
                .Where(card => AnnounceCardFilter.IsDeclarable(card, opcodes))
                .OrderBy(card => card.Name, StringComparer.CurrentCulture)
                .ThenBy(card => card.Code)
                .ToArray();
            if (matchingCards.Length > 0)
            {
                foreach (CardRecord card in matchingCards)
                {
                    prompt.Choices.Add(Choice(
                        card.Name,
                        card.Code,
                        IntResponse(unchecked((int)card.Code))));
                }
                return prompt;
            }

            foreach (uint code in AnnounceCardFilter.LiteralCardCodes(opcodes))
            {
                prompt.Choices.Add(Choice(
                    code.ToString("00000000"),
                    code,
                    IntResponse(unchecked((int)code))));
            }
            return prompt;
        }

        private static DuelPrompt DecodeChain(PacketReader reader)
        {
            byte player = reader.Byte();
            reader.Byte();
            bool forced = reader.Byte() != 0;
            reader.UInt32();
            reader.UInt32();
            uint count = GuardCount(reader.UInt32(), 100, "chain choices");
            var prompt = NewPrompt(CoreMessage.SelectChain, player, "Responder à corrente");
            prompt.Forced = forced;
            for (int i = 0; i < count; i++)
            {
                uint code = reader.UInt32();
                CardLocation location = reader.Location();
                ulong description = reader.UInt64();
                reader.Byte();
                DuelChoice choice = Choice(
                    "Encadear efeito",
                    code,
                    IntResponse(i),
                    location.Controller,
                    location.Location,
                    location.Sequence,
                    i,
                    location.Position);
                choice.DescriptionId = description;
                prompt.Choices.Add(choice);
            }
            if (!forced) prompt.Choices.Add(Choice("Não responder", 0, IntResponse(-1)));
            return prompt;
        }

        private static DuelPrompt DecodePlace(
            PacketReader reader,
            CoreMessage message)
        {
            byte player = reader.Byte();
            uint count = GuardCount(reader.Byte(), 16, "place selection count");
            uint unavailable = reader.UInt32();
            var prompt = NewPrompt(
                message,
                player,
                message == CoreMessage.SelectDisableField
                    ? "Escolha uma zona para desabilitar"
                    : "Escolha uma zona");
            prompt.Forced = true;
            prompt.MinimumSelections = count;
            prompt.MaximumSelections = count;
            int choiceIndex = 0;
            for (byte relativeController = 0;
                 relativeController < 2;
                 relativeController++)
            {
                // ocgcore encodes the requesting player's field in the low
                // 16 bits even when that player is team 1.  Presentation and
                // the response use absolute controller ids, so remap here.
                byte controller = relativeController == 0
                    ? player
                    : (byte)(1 - player);
                for (byte locationIndex = 0; locationIndex < 2; locationIndex++)
                {
                    byte location = locationIndex == 0 ? (byte)DuelLocation.MonsterZone : (byte)DuelLocation.SpellTrapZone;
                    int width = locationIndex == 0 ? 7 : 8;
                    for (byte sequence = 0; sequence < width; sequence++)
                    {
                        int bit =
                            sequence +
                            (locationIndex * 8) +
                            (relativeController * 16);
                        if ((unavailable & (1u << bit)) == 0)
                        {
                            prompt.Choices.Add(Choice(
                                $"Zona {(locationIndex == 0 ? "de Monstro" : "de Magia/Armadilha")} {sequence + 1}",
                                0,
                                new[] { controller, location, sequence },
                                controller,
                                location,
                                sequence,
                                choiceIndex++));
                        }
                    }
                }
            }
            if (count > 1)
            {
                prompt.DetailTitle($"Escolha {count} zonas");
            }
            return prompt;
        }

        private static DuelPrompt DecodePosition(PacketReader reader)
        {
            byte player = reader.Byte();
            uint code = reader.UInt32();
            byte positions = reader.Byte();
            var prompt = NewPrompt(CoreMessage.SelectPosition, player, "Escolha a posição");
            AddPosition(prompt, positions, 0x1, "Ataque com a face para cima", code);
            AddPosition(prompt, positions, 0x2, "Ataque com a face para baixo", code);
            AddPosition(prompt, positions, 0x4, "Defesa com a face para cima", code);
            AddPosition(prompt, positions, 0x8, "Defesa com a face para baixo", code);
            return prompt;
        }

        private static void ReadCommandCards(PacketReader reader, DuelPrompt prompt, string label, int command, bool shortLocation)
        {
            uint count = GuardCount(reader.UInt32(), 200, "command cards");
            for (int i = 0; i < count; i++)
            {
                uint code = reader.UInt32();
                byte controller = reader.Byte();
                byte location = reader.Byte();
                uint sequence = shortLocation ? reader.Byte() : reader.UInt32();
                prompt.Choices.Add(Choice(
                    label,
                    code,
                    IntResponse((i << 16) + command),
                    controller,
                    location,
                    sequence,
                    i));
            }
        }

        private static void ReadActivations(PacketReader reader, DuelPrompt prompt, string label, int command)
        {
            uint count = GuardCount(reader.UInt32(), 200, "activations");
            for (int i = 0; i < count; i++)
            {
                uint code = reader.UInt32();
                byte controller = reader.Byte();
                byte location = reader.Byte();
                uint sequence = reader.UInt32();
                ulong description = reader.UInt64();
                reader.Byte();
                DuelChoice choice = Choice(
                    label,
                    code,
                    IntResponse((i << 16) + command),
                    controller,
                    location,
                    sequence,
                    i);
                choice.DescriptionId = description;
                prompt.Choices.Add(choice);
            }
        }

        private static void AddPosition(DuelPrompt prompt, byte allowed, byte position, string label, uint code)
        {
            if ((allowed & position) != 0) prompt.Choices.Add(Choice(label, code, IntResponse(position)));
        }

        private static DuelPrompt NewPrompt(CoreMessage message, byte player, string title)
        {
            return new DuelPrompt { Message = message, Player = player, Title = title };
        }

        private static void DetailTitle(this DuelPrompt prompt, string title)
        {
            prompt.Title = title;
        }

        private static DuelChoice Choice(
            string label,
            uint code,
            byte[] response,
            byte controller = 0,
            byte location = 0,
            uint sequence = 0,
            int choiceIndex = -1,
            uint position = 0)
        {
            return new DuelChoice
            {
                Label = label,
                CardCode = code,
                Response = response,
                HasLocation = location != 0,
                Controller = controller,
                Location = location,
                Sequence = sequence,
                Position = position,
                ChoiceIndex = choiceIndex
            };
        }

        public static byte[] IntResponse(int value)
        {
            return new[]
            {
                (byte)value,
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)(value >> 24)
            };
        }

        public static byte[] PairResponse(int first, int second)
        {
            byte[] response = new byte[8];
            WriteUInt32(response, 0, unchecked((uint)first));
            WriteUInt32(response, 4, unchecked((uint)second));
            return response;
        }

        public static byte[] UInt64Response(ulong value)
        {
            byte[] response = new byte[8];
            WriteUInt32(response, 0, (uint)value);
            WriteUInt32(response, 4, (uint)(value >> 32));
            return response;
        }

        public static byte[] CardSelectionResponse(uint[] indexes)
        {
            byte[] response = new byte[8 + (indexes.Length * 4)];
            WriteUInt32(response, 0, 0);
            WriteUInt32(response, 4, (uint)indexes.Length);
            for (int i = 0; i < indexes.Length; i++)
            {
                WriteUInt32(response, 8 + (i * 4), indexes[i]);
            }
            return response;
        }

        public static byte[] PlaceSelectionResponse(
            IEnumerable<DuelChoice> choices)
        {
            DuelChoice[] selected = choices?.ToArray() ??
                Array.Empty<DuelChoice>();
            if (selected.Length == 0)
            {
                throw new ArgumentException(
                    "At least one field zone must be selected.",
                    nameof(choices));
            }

            byte[] response = new byte[selected.Length * 3];
            for (int index = 0; index < selected.Length; index++)
            {
                byte[] zone = selected[index]?.Response;
                if (zone == null || zone.Length != 3)
                {
                    throw new ArgumentException(
                        "Every selected field zone must contain a three-byte response.",
                        nameof(choices));
                }
                Buffer.BlockCopy(zone, 0, response, index * 3, 3);
            }
            return response;
        }

        public static bool IsValidPlaceSelectionResponse(
            DuelPrompt prompt,
            byte[] response)
        {
            if (prompt == null || response == null ||
                (prompt.Message != CoreMessage.SelectPlace &&
                 prompt.Message != CoreMessage.SelectDisableField))
            {
                return false;
            }

            int required = checked((int)prompt.MaximumSelections);
            if (required <= 0 || response.Length != required * 3)
                return false;

            var selectedZones = new HashSet<string>(
                StringComparer.Ordinal);
            for (int offset = 0; offset < response.Length; offset += 3)
            {
                string zoneKey = string.Concat(
                    response[offset], ":",
                    response[offset + 1], ":",
                    response[offset + 2]);
                if (!selectedZones.Add(zoneKey) ||
                    !prompt.Choices.Any(choice =>
                        choice.Response != null &&
                        choice.Response.Length == 3 &&
                        choice.Response[0] == response[offset] &&
                        choice.Response[1] == response[offset + 1] &&
                        choice.Response[2] == response[offset + 2]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsValidSelection(
            DuelPrompt prompt,
            IEnumerable<int> selectedIndexes)
        {
            if (prompt == null) return false;
            int[] indexes = selectedIndexes?
                .Distinct()
                .OrderBy(value => value)
                .ToArray() ?? Array.Empty<int>();
            bool tributeSelection =
                prompt.Message == CoreMessage.SelectTribute;
            if (!prompt.SumAtLeast &&
                !tributeSelection &&
                (indexes.Length < prompt.MinimumSelections ||
                 indexes.Length > prompt.MaximumSelections))
            {
                return false;
            }
            if (tributeSelection)
            {
                if (indexes.Length > prompt.MaximumSelections)
                {
                    return false;
                }
                uint tributeValue = 0;
                foreach (int index in indexes)
                {
                    DuelChoice choice = prompt.Choices.FirstOrDefault(
                        candidate => candidate.ChoiceIndex == index);
                    if (choice == null) return false;
                    tributeValue += choice.SumValue;
                }
                return tributeValue >= prompt.MinimumSelections;
            }
            if (prompt.Message != CoreMessage.SelectSum)
            {
                return true;
            }

            var values = new List<uint>(prompt.MandatorySums);
            foreach (int index in indexes)
            {
                DuelChoice choice = prompt.Choices.FirstOrDefault(
                    candidate => candidate.ChoiceIndex == index);
                if (choice == null) return false;
                values.Add(choice.SumValue);
            }
            if (values.Count == 0) return prompt.RequiredSum == 0;

            if (prompt.SumAtLeast)
            {
                uint minimumTotal = 0;
                uint maximumTotal = 0;
                uint smallest = uint.MaxValue;
                foreach (uint encoded in values)
                {
                    uint first = encoded & 0xFFFF;
                    uint second = encoded >> 16;
                    if (second == 0) second = first;
                    uint minimum = Math.Min(first, second);
                    uint maximum = Math.Max(first, second);
                    minimumTotal += minimum;
                    maximumTotal += maximum;
                    smallest = Math.Min(smallest, minimum);
                }
                return maximumTotal >= prompt.RequiredSum &&
                       minimumTotal - smallest < prompt.RequiredSum;
            }

            var totals = new HashSet<uint> { 0 };
            foreach (uint encoded in values)
            {
                uint first = encoded & 0xFFFF;
                uint second = encoded >> 16;
                if (second == 0) second = first;
                var next = new HashSet<uint>();
                foreach (uint total in totals)
                {
                    next.Add(total + first);
                    next.Add(total + second);
                }
                totals = next;
            }
            return totals.Contains(prompt.RequiredSum);
        }

        private static void BuildPermutations(
            List<byte> current,
            bool[] used,
            int count,
            List<byte[]> output,
            int maximum)
        {
            if (output.Count >= maximum) return;
            if (current.Count == count)
            {
                output.Add(current.ToArray());
                return;
            }
            for (byte index = 0;
                 index < count && output.Count < maximum;
                 index++)
            {
                if (used[index]) continue;
                used[index] = true;
                current.Add(index);
                BuildPermutations(
                    current,
                    used,
                    count,
                    output,
                    maximum);
                current.RemoveAt(current.Count - 1);
                used[index] = false;
            }
        }

        private static void BuildMaskCombinations(
            List<int> bits,
            int start,
            int remaining,
            ulong current,
            List<ulong> output,
            int maximum)
        {
            if (output.Count >= maximum) return;
            if (remaining == 0)
            {
                output.Add(current);
                return;
            }
            for (int index = start;
                 index <= bits.Count - remaining &&
                 output.Count < maximum;
                 index++)
            {
                BuildMaskCombinations(
                    bits,
                    index + 1,
                    remaining - 1,
                    current | (1UL << bits[index]),
                    output,
                    maximum);
            }
        }

        private static uint GuardCount(uint count, uint maximum, string context)
        {
            if (count > maximum)
            {
                throw new CoreProtocolException($"Invalid {context} count {count} (maximum {maximum}).");
            }
            return count;
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] |
                          (data[offset + 1] << 8) |
                          (data[offset + 2] << 16) |
                          (data[offset + 3] << 24));
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        public static string PhaseName(uint phase)
        {
            switch (phase)
            {
                case 0x01: return "Fase de Compra";
                case 0x02: return "Fase de Apoio";
                case 0x04: return "Fase Principal 1";
                case 0x08: return "Início da Fase de Batalha";
                case 0x10: return "Etapa de Batalha";
                case 0x20: return "Etapa de Dano";
                case 0x40: return "Cálculo de Dano";
                case 0x80: return "Fase de Batalha";
                case 0x100: return "Fase Principal 2";
                case 0x200: return "Fase Final";
                default: return $"Fase 0x{phase:X}";
            }
        }
    }
}
