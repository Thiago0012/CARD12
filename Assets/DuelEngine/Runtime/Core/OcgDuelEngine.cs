using System;
using AOT;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ArcaneDuel.DuelEngine.Abstractions;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Content;
using ArcaneDuel.DuelEngine.Diagnostics;
using ArcaneDuel.DuelEngine.Interop;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.Scripts;
using UnityEngine;

namespace ArcaneDuel.DuelEngine.Core
{
    public enum DuelRuleProfile
    {
        TcgMasterRule2021 = 0,
        OcgMasterRule2020 = 1
    }

    public sealed class DuelConfiguration
    {
        private uint playerStartingLifePoints = 8000;
        private uint opponentStartingLifePoints = 8000;

        /// <summary>
        /// Legacy symmetric LP setting. Setting it preserves the standard
        /// duel behavior by updating both logical duelists.
        /// </summary>
        public uint StartingLifePoints
        {
            get => playerStartingLifePoints;
            set
            {
                playerStartingLifePoints = value;
                opponentStartingLifePoints = value;
            }
        }

        public uint PlayerStartingLifePoints
        {
            get => playerStartingLifePoints;
            set => playerStartingLifePoints = value;
        }

        public uint OpponentStartingLifePoints
        {
            get => opponentStartingLifePoints;
            set => opponentStartingLifePoints = value;
        }
        public uint StartingHand { get; set; } = 5;
        public uint DrawPerTurn { get; set; } = 1;
        /// <summary>
        /// Logical player that receives the first turn. The native core
        /// always starts team zero, so OcgDuelEngine maps native seats back
        /// to the stable logical presentation seats when this value is one.
        /// </summary>
        public byte StartingPlayer { get; set; }
        public ulong Seed { get; set; } = 0xA7C4D3E2198B6501UL;
        public bool SimpleOpponentAi { get; set; } = true;
        public bool ShuffleMainDecks { get; set; } = true;
        public DuelRuleProfile RuleProfile { get; set; } =
            DuelRuleProfile.TcgMasterRule2021;
        public uint[] PlayerDeck { get; set; }
        public uint[] OpponentDeck { get; set; }
        public uint[] PlayerExtraDeck { get; set; }
        public uint[] OpponentExtraDeck { get; set; }

        private static long runtimeSeedCounter;

        public static ulong FreshSeed()
        {
            var bytes = new byte[sizeof(ulong)];
            using (RandomNumberGenerator random =
                   RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }
            ulong seed = BitConverter.ToUInt64(bytes, 0);
            if (seed != 0)
                return seed;

            unchecked
            {
                ulong counter = (ulong)System.Threading.Interlocked
                    .Increment(ref runtimeSeedCounter);
                return 0xA7C4D3E2198B6501UL ^ counter ^
                       (ulong)DateTime.UtcNow.Ticks;
            }
        }

        public static DuelConfiguration VerticalSlice(ulong seed = 0xA7C4D3E2198B6501UL)
        {
            uint[] main =
            {
                89631139, 89631139, 89631139,
                46986414, 46986414, 46986414,
                74131780, 74131780, 74131780,
                71413901, 71413901, 71413901,
                7089711, 7089711, 7089711,
                93920745, 93920745, 93920745,
                97268402, 97268402, 97268402,
                77585513, 77585513, 77585513,
                53129443, 53129443, 53129443,
                5318639, 5318639, 5318639,
                44095762, 44095762, 44095762,
                89631139, 46986414, 74131780, 71413901,
                53129443, 5318639, 44095762
            };
            return new DuelConfiguration
            {
                Seed = seed,
                PlayerDeck = (uint[])main.Clone(),
                OpponentDeck = (uint[])main.Clone(),
                PlayerExtraDeck = new uint[] { 11901678, 11901678, 11901678 },
                OpponentExtraDeck = new uint[] { 11901678, 11901678, 11901678 }
            };
        }
    }

    public sealed class OcgFieldCardSnapshot
    {
        public uint Code { get; internal set; }
        public uint Position { get; internal set; }
        public byte Owner { get; internal set; }
        public int Attack { get; internal set; }
        public int Defense { get; internal set; }
        public CardLocation EquipTarget { get; internal set; }
        public CardLocation[] TargetCards { get; internal set; } =
            Array.Empty<CardLocation>();
        public uint[] OverlayCodes { get; internal set; } = Array.Empty<uint>();
        public ushort[] CounterTypes { get; internal set; } =
            Array.Empty<ushort>();
        public uint[] CounterAmounts { get; internal set; } =
            Array.Empty<uint>();
        public uint Status { get; internal set; }
        public bool IsPublic { get; internal set; }
        public uint LinkRating { get; internal set; }
        public uint LinkMarkers { get; internal set; }
    }

    public sealed class OcgDuelistFieldSnapshot
    {
        public OcgFieldCardSnapshot[] Deck { get; internal set; } =
            Array.Empty<OcgFieldCardSnapshot>();
        public OcgFieldCardSnapshot[] Hand { get; internal set; } =
            Array.Empty<OcgFieldCardSnapshot>();
        public OcgFieldCardSnapshot[] Monsters { get; internal set; } =
            Array.Empty<OcgFieldCardSnapshot>();
        public OcgFieldCardSnapshot[] Spells { get; internal set; } =
            Array.Empty<OcgFieldCardSnapshot>();
        public OcgFieldCardSnapshot[] Graveyard { get; internal set; } =
            Array.Empty<OcgFieldCardSnapshot>();
        public OcgFieldCardSnapshot[] Banished { get; internal set; } =
            Array.Empty<OcgFieldCardSnapshot>();
        public OcgFieldCardSnapshot[] Extra { get; internal set; } =
            Array.Empty<OcgFieldCardSnapshot>();
    }

    public sealed class OcgFieldSnapshot
    {
        public OcgDuelistFieldSnapshot[] Players { get; internal set; } =
            Array.Empty<OcgDuelistFieldSnapshot>();
    }

    public sealed class OcgDuelEngine : IDuelRulesEngine
    {
        private const ulong MasterRule5 = 0x2E800UL;
        private const ulong TcgSegocNonPublic = 0x100000000UL;
        private const ulong TcgSegocFirstTrigger = 0x200000000UL;
        private const ulong SimpleAi = 0x40UL;
        private const uint FaceDownDefense = 0x8;
        private const uint QueryCode = 0x1;
        private const uint QueryPosition = 0x2;
        private const uint QueryAttack = 0x100;
        private const uint QueryDefense = 0x200;
        private const uint QueryEquipCard = 0x4000;
        private const uint QueryTargetCard = 0x8000;
        private const uint QueryOverlayCard = 0x10000;
        private const uint QueryCounters = 0x20000;
        private const uint QueryOwner = 0x40000;
        private const uint QueryStatus = 0x80000;
        private const uint QueryIsPublic = 0x100000;
        private const uint QueryLink = 0x800000;
        private const uint QueryEnd = 0x80000000;
        private const uint FieldQueryFlags = QueryCode | QueryPosition |
            QueryAttack | QueryDefense | QueryEquipCard | QueryTargetCard |
            QueryOverlayCard | QueryCounters | QueryOwner | QueryStatus |
            QueryIsPublic | QueryLink;
        private const int MaximumQueryBytes = 2 * 1024 * 1024;

        private readonly CardDatabase database;
        private readonly ScriptRepository scripts;
        private readonly DuelConfiguration configuration;
        private readonly OcgDuelSafeHandle duel = new OcgDuelSafeHandle();
        private readonly List<DuelEvent> history = new List<DuelEvent>();
        private readonly List<string> nativeLogs = new List<string>();
        private readonly Dictionary<IntPtr, IntPtr> setcodeAllocations = new Dictionary<IntPtr, IntPtr>();
        private readonly object allocationGate = new object();
        private readonly OcgDataReader cardReader;
        private readonly OcgDataReaderDone cardReaderDone;
        private readonly OcgScriptReader scriptReader;
        private readonly OcgLogHandler logHandler;
        private GCHandle selfHandle;
        private Exception callbackFailure;
        private bool disposed;
        private bool hasWinner;
        private ulong nextRequestId;
        private DuelPrompt retryFallbackPrompt;

        public bool IsStarted { get; private set; }
        public bool IsFinished => hasWinner || Status == OcgDuelStatus.End;
        public OcgDuelStatus Status { get; private set; } = OcgDuelStatus.Continue;
        public DuelPrompt CurrentPrompt { get; private set; }
        public IReadOnlyList<DuelEvent> EventHistory => history;
        public IReadOnlyList<string> NativeLogs => nativeLogs;
        public event Action<DuelEvent> EventReceived;

        public static ulong RuleFlagsFor(DuelRuleProfile profile)
        {
            switch (profile)
            {
                case DuelRuleProfile.TcgMasterRule2021:
                    return MasterRule5 |
                           TcgSegocNonPublic |
                           TcgSegocFirstTrigger;
                case DuelRuleProfile.OcgMasterRule2020:
                    return MasterRule5;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(profile),
                        profile,
                        "Unknown duel rule profile.");
            }
        }

        public OcgDuelEngine(CardDatabase database, string ygoContentRoot, DuelConfiguration configuration)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            scripts = new ScriptRepository(ygoContentRoot ?? throw new ArgumentNullException(nameof(ygoContentRoot)));

            cardReader = OnCardRead;
            cardReaderDone = OnCardReadDone;
            scriptReader = OnScriptRead;
            logHandler = OnNativeLog;
            // The native core only retains this GCHandle payload. It must be
            // strong for the complete duel lifetime so callbacks can never
            // observe a collected managed owner between frames.
            selfHandle = GCHandle.Alloc(this);
            IntPtr payload = GCHandle.ToIntPtr(selfHandle);

            try
            {
                ulong seedState = configuration.Seed;
                var options = new OcgDuelOptions
                {
                    Seed0 = SplitMix64(ref seedState),
                    Seed1 = SplitMix64(ref seedState),
                    Seed2 = SplitMix64(ref seedState),
                    Seed3 = SplitMix64(ref seedState),
                    Flags = RuleFlagsFor(configuration.RuleProfile) |
                            (configuration.SimpleOpponentAi ? SimpleAi : 0),
                    Team1 = Player(
                        configuration,
                        configuration.StartingPlayer == 1
                            ? configuration.OpponentStartingLifePoints
                            : configuration.PlayerStartingLifePoints),
                    Team2 = Player(
                        configuration,
                        configuration.StartingPlayer == 1
                            ? configuration.PlayerStartingLifePoints
                            : configuration.OpponentStartingLifePoints),
                    CardReader = cardReader,
                    CardReaderPayload = payload,
                    ScriptReader = scriptReader,
                    ScriptReaderPayload = payload,
                    LogHandler = logHandler,
                    LogHandlerPayload = payload,
                    CardReaderDone = cardReaderDone,
                    CardReaderDonePayload = payload,
                    EnableUnsafeLibraries = 1
                };

                int creation = OcgCoreNative.CreateDuel(out IntPtr nativeDuel, ref options);
                if ((OcgDuelCreationStatus)creation != OcgDuelCreationStatus.Success || nativeDuel == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"ocgcore failed to create the duel: {(OcgDuelCreationStatus)creation}.");
                }
                duel.Initialize(nativeDuel);
                LoadRequiredScript("constant.lua");
                LoadRequiredScript("utility.lua");
                ThrowCallbackFailure();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static OcgDuelEngine CreateDefault(DuelConfiguration configuration = null)
        {
            string ygoRoot = YgoContentLocator.Root;
            return new OcgDuelEngine(
                CardDatabase.LoadDefault(),
                ygoRoot,
                configuration ?? DuelConfiguration.VerticalSlice());
        }

        public void AddCard(byte team, uint code, uint location)
        {
            AddCardAt(
                team,
                code,
                location,
                0,
                FaceDownDefense);
        }

        public void AddCardAt(
            byte team,
            uint code,
            uint location,
            uint sequence,
            uint position)
        {
            AddCardAtNative(
                NativePlayerForLogical(team),
                code,
                location,
                sequence,
                position);
        }

        private void AddCardAtNative(
            byte team,
            uint code,
            uint location,
            uint sequence,
            uint position)
        {
            EnsureUsable();
            if (team > 1) throw new ArgumentOutOfRangeException(nameof(team));
            database.Get(code);
            var info = new OcgNewCardInfo
            {
                Team = team,
                Duelist = 0,
                Code = code,
                Controller = team,
                Location = location,
                Sequence = sequence,
                Position = position
            };
            OcgCoreNative.DuelNewCard(duel, ref info);
            ThrowCallbackFailure();
        }

        public bool TryGetCurrentCombatStats(
            byte controller,
            uint location,
            uint sequence,
            out int attack,
            out int defense)
        {
            attack = 0;
            defense = 0;
            if (disposed || !IsStarted || controller > 1)
                return false;

            var info = new OcgQueryInfo
            {
                Flags = QueryAttack | QueryDefense,
                Controller = NativePlayerForLogical(controller),
                Location = location,
                Sequence = sequence,
                OverlaySequence = 0
            };
            IntPtr result = OcgCoreNative.DuelQuery(
                duel,
                out uint length,
                ref info);
            if (result == IntPtr.Zero || length == 0 || length > int.MaxValue)
                return false;

            var buffer = new byte[(int)length];
            Marshal.Copy(result, buffer, 0, buffer.Length);
            return TryReadCombatStats(buffer, out attack, out defense);
        }

        public bool TryCaptureFieldSnapshot(out OcgFieldSnapshot snapshot)
        {
            snapshot = null;
            if (disposed || !IsStarted)
                return false;

            var players = new OcgDuelistFieldSnapshot[2];
            for (byte controller = 0; controller < players.Length; controller++)
            {
                byte nativeController = NativePlayerForLogical(controller);
                if (!TryQueryLocation(nativeController, DuelLocation.Deck, out var deck) ||
                    !TryQueryLocation(nativeController, DuelLocation.Hand, out var hand) ||
                    !TryQueryLocation(nativeController, DuelLocation.MonsterZone,
                        out var monsters) ||
                    !TryQueryLocation(nativeController, DuelLocation.SpellTrapZone,
                        out var spells) ||
                    !TryQueryLocation(nativeController, DuelLocation.Graveyard,
                        out var graveyard) ||
                    !TryQueryLocation(nativeController, DuelLocation.Banished,
                        out var banished) ||
                    !TryQueryLocation(nativeController, DuelLocation.Extra, out var extra))
                {
                    return false;
                }

                RemapSnapshotCards(deck);
                RemapSnapshotCards(hand);
                RemapSnapshotCards(monsters);
                RemapSnapshotCards(spells);
                RemapSnapshotCards(graveyard);
                RemapSnapshotCards(banished);
                RemapSnapshotCards(extra);

                players[controller] = new OcgDuelistFieldSnapshot
                {
                    Deck = deck,
                    Hand = hand,
                    Monsters = monsters,
                    Spells = spells,
                    Graveyard = graveyard,
                    Banished = banished,
                    Extra = extra
                };
            }
            snapshot = new OcgFieldSnapshot { Players = players };
            return true;
        }

        private void RemapSnapshotCards(OcgFieldCardSnapshot[] cards)
        {
            if (!SeatsAreSwapped || cards == null)
                return;
            foreach (OcgFieldCardSnapshot card in cards)
            {
                if (card == null)
                    continue;
                if (card.Owner <= 1)
                    card.Owner = LogicalPlayerForNative(card.Owner);
                RemapLocation(card.EquipTarget);
                RemapLocations(card.TargetCards);
            }
        }

        private bool TryQueryLocation(
            byte controller,
            uint location,
            out OcgFieldCardSnapshot[] cards)
        {
            cards = Array.Empty<OcgFieldCardSnapshot>();
            var info = new OcgQueryInfo
            {
                Flags = FieldQueryFlags,
                Controller = controller,
                Location = location,
                Sequence = 0,
                OverlaySequence = 0
            };
            IntPtr result = OcgCoreNative.DuelQueryLocation(
                duel,
                out uint length,
                ref info);
            if (result == IntPtr.Zero || length < sizeof(uint) ||
                length > MaximumQueryBytes)
            {
                return false;
            }

            var buffer = new byte[(int)length];
            Marshal.Copy(result, buffer, 0, buffer.Length);
            return TryReadLocationQuery(buffer, out cards);
        }

        private static bool TryReadLocationQuery(
            byte[] buffer,
            out OcgFieldCardSnapshot[] cards)
        {
            cards = Array.Empty<OcgFieldCardSnapshot>();
            if (buffer == null || buffer.Length < sizeof(uint) ||
                BitConverter.ToUInt32(buffer, 0) != buffer.Length - sizeof(uint))
            {
                return false;
            }

            var result = new List<OcgFieldCardSnapshot>();
            OcgFieldCardSnapshot current = null;
            int offset = sizeof(uint);
            while (offset < buffer.Length)
            {
                if (offset + sizeof(ushort) > buffer.Length)
                    return false;
                ushort blockLength = BitConverter.ToUInt16(buffer, offset);
                offset += sizeof(ushort);
                if (blockLength == 0)
                {
                    if (current != null)
                        return false;
                    result.Add(null);
                    continue;
                }
                if (blockLength < sizeof(uint) ||
                    offset + blockLength > buffer.Length)
                {
                    return false;
                }

                uint flag = BitConverter.ToUInt32(buffer, offset);
                int dataOffset = offset + sizeof(uint);
                int dataLength = blockLength - sizeof(uint);
                if (flag == QueryEnd)
                {
                    if (current == null)
                        return false;
                    result.Add(current);
                    current = null;
                }
                else
                {
                    current ??= new OcgFieldCardSnapshot();
                    switch (flag)
                    {
                        case QueryCode when dataLength >= sizeof(uint):
                            current.Code = BitConverter.ToUInt32(
                                buffer,
                                dataOffset);
                            break;
                        case QueryPosition when dataLength >= sizeof(uint):
                            current.Position = BitConverter.ToUInt32(
                                buffer,
                                dataOffset);
                            break;
                        case QueryAttack when dataLength >= sizeof(int):
                            current.Attack = BitConverter.ToInt32(
                                buffer,
                                dataOffset);
                            break;
                        case QueryDefense when dataLength >= sizeof(int):
                            current.Defense = BitConverter.ToInt32(
                                buffer,
                                dataOffset);
                            break;
                        case QueryEquipCard when dataLength == 10:
                            current.EquipTarget = ReadQueryLocation(
                                buffer,
                                dataOffset);
                            if (current.EquipTarget.Location == 0)
                                current.EquipTarget = null;
                            break;
                        case QueryTargetCard
                            when dataLength >= sizeof(uint):
                            uint targetCount = BitConverter.ToUInt32(
                                buffer,
                                dataOffset);
                            if (targetCount > 256 ||
                                dataLength != sizeof(uint) +
                                    checked((int)targetCount * 10))
                            {
                                return false;
                            }
                            current.TargetCards =
                                new CardLocation[targetCount];
                            for (int index = 0;
                                 index < targetCount;
                                 index++)
                            {
                                current.TargetCards[index] =
                                    ReadQueryLocation(
                                        buffer,
                                        dataOffset + sizeof(uint) +
                                        index * 10);
                            }
                            break;
                        case QueryOwner when dataLength >= sizeof(byte):
                            current.Owner = buffer[dataOffset];
                            break;
                        case QueryOverlayCard
                            when dataLength >= sizeof(uint):
                            uint count = BitConverter.ToUInt32(
                                buffer,
                                dataOffset);
                            if (count > 256 ||
                                dataLength != sizeof(uint) +
                                    checked((int)count * sizeof(uint)))
                            {
                                return false;
                            }
                            current.OverlayCodes = new uint[count];
                            for (int index = 0; index < count; index++)
                            {
                                current.OverlayCodes[index] =
                                    BitConverter.ToUInt32(
                                        buffer,
                                        dataOffset + sizeof(uint) +
                                        index * sizeof(uint));
                            }
                            break;
                        case QueryCounters
                            when dataLength >= sizeof(uint):
                            uint counterCount = BitConverter.ToUInt32(
                                buffer,
                                dataOffset);
                            if (counterCount > 256 ||
                                dataLength != sizeof(uint) +
                                    checked((int)counterCount *
                                            sizeof(uint)))
                            {
                                return false;
                            }
                            current.CounterTypes =
                                new ushort[counterCount];
                            current.CounterAmounts =
                                new uint[counterCount];
                            for (int index = 0;
                                 index < counterCount;
                                 index++)
                            {
                                uint packed = BitConverter.ToUInt32(
                                    buffer,
                                    dataOffset + sizeof(uint) +
                                    index * sizeof(uint));
                                current.CounterTypes[index] =
                                    unchecked((ushort)packed);
                                current.CounterAmounts[index] = packed >> 16;
                            }
                            break;
                        case QueryStatus when dataLength >= sizeof(uint):
                            current.Status = BitConverter.ToUInt32(
                                buffer,
                                dataOffset);
                            break;
                        case QueryIsPublic when dataLength >= sizeof(byte):
                            current.IsPublic = buffer[dataOffset] != 0;
                            break;
                        case QueryLink when dataLength >= sizeof(uint) * 2:
                            current.LinkRating = BitConverter.ToUInt32(
                                buffer,
                                dataOffset);
                            current.LinkMarkers = BitConverter.ToUInt32(
                                buffer,
                                dataOffset + sizeof(uint));
                            break;
                    }
                }
                offset += blockLength;
            }
            if (current != null)
                return false;
            cards = result.ToArray();
            return true;
        }

        private static CardLocation ReadQueryLocation(
            byte[] buffer,
            int offset)
        {
            return new CardLocation
            {
                Controller = buffer[offset],
                Location = buffer[offset + 1],
                Sequence = BitConverter.ToUInt32(buffer, offset + 2),
                Position = BitConverter.ToUInt32(buffer, offset + 6)
            };
        }

        private static bool TryReadCombatStats(
            byte[] buffer,
            out int attack,
            out int defense)
        {
            attack = 0;
            defense = 0;
            bool hasAttack = false;
            bool hasDefense = false;
            int offset = 0;
            while (buffer != null && offset + 6 <= buffer.Length)
            {
                ushort blockLength = BitConverter.ToUInt16(buffer, offset);
                offset += sizeof(ushort);
                if (blockLength < sizeof(uint) ||
                    offset + blockLength > buffer.Length)
                {
                    return false;
                }

                uint flag = BitConverter.ToUInt32(buffer, offset);
                if (flag == QueryEnd)
                    break;
                if (blockLength >= sizeof(uint) * 2)
                {
                    int value = BitConverter.ToInt32(
                        buffer,
                        offset + sizeof(uint));
                    if (flag == QueryAttack)
                    {
                        attack = value;
                        hasAttack = true;
                    }
                    else if (flag == QueryDefense)
                    {
                        defense = value;
                        hasDefense = true;
                    }
                }
                offset += blockLength;
            }
            return hasAttack && hasDefense;
        }

        public void Start()
        {
            EnsureUsable();
            if (IsStarted) throw new InvalidOperationException("The duel has already started.");
            string[] unsupported = DuelContentValidator.FindProblems(
                database,
                scripts,
                configuration.PlayerDeck,
                configuration.PlayerExtraDeck,
                configuration.OpponentDeck,
                configuration.OpponentExtraDeck);
            if (unsupported.Length > 0)
            {
                RuntimeDiagnosticRecorder.Record(
                    "F01",
                    "DataOrScript",
                    nameof(OcgDuelEngine),
                    "Selected duel contains unsupported cards or dependencies.",
                    details: string.Join(" | ", unsupported));
                throw new InvalidDataException(
                    "The selected duel contains unsupported cards: " +
                    string.Join(" | ", unsupported));
            }
            AddDeck(
                NativePlayerForLogical(0),
                configuration.ShuffleMainDecks
                    ? Shuffled(configuration.PlayerDeck, configuration.Seed ^ 0xA11CE001UL)
                    : configuration.PlayerDeck,
                DuelLocation.Deck);
            AddDeck(
                NativePlayerForLogical(0),
                configuration.PlayerExtraDeck,
                DuelLocation.Extra);
            AddDeck(
                NativePlayerForLogical(1),
                configuration.ShuffleMainDecks
                    ? Shuffled(configuration.OpponentDeck, configuration.Seed ^ 0xA11CE002UL)
                    : configuration.OpponentDeck,
                DuelLocation.Deck);
            AddDeck(
                NativePlayerForLogical(1),
                configuration.OpponentExtraDeck,
                DuelLocation.Extra);
            OcgCoreNative.StartDuel(duel);
            IsStarted = true;
            Emit(new DuelEvent
            {
                Message = CoreMessage.Start,
                RawMessage = (byte)CoreMessage.Start,
                Player = configuration.StartingPlayer,
                Value = configuration.PlayerStartingLifePoints,
                OpponentValue = configuration.OpponentStartingLifePoints,
                Detail = "Duelo iniciado pelo ocgcore"
            });
            Process();
        }

        public OcgDuelStatus Process()
        {
            EnsureUsable();
            if (!IsStarted) throw new InvalidOperationException("Start the duel before processing it.");
            retryFallbackPrompt = CurrentPrompt;
            CurrentPrompt = null;
            int iterations = 0;
            do
            {
                if (++iterations > 4096)
                {
                    RuntimeDiagnosticRecorder.Record(
                        "F00",
                        "Core",
                        nameof(OcgDuelEngine),
                        "Core processing safety limit exceeded.",
                        RuntimeDiagnosticSeverity.Critical,
                        details: $"status={Status}; events={history.Count}");
                    throw new InvalidOperationException("ocgcore exceeded the processing safety limit.");
                }
                Status = (OcgDuelStatus)OcgCoreNative.DuelProcess(duel);
                CopyAndDecodeMessages();
                ThrowCallbackFailure();
            }
            while (Status == OcgDuelStatus.Continue);
            retryFallbackPrompt = null;
            return Status;
        }

        public void SubmitResponse(byte[] response)
        {
            EnsureUsable();
            if (IsFinished)
            {
                throw new InvalidOperationException("The duel already emitted its terminal winner event.");
            }
            if (Status != OcgDuelStatus.Awaiting)
            {
                throw new InvalidOperationException("ocgcore is not awaiting a response.");
            }
            if (response == null || response.Length == 0)
            {
                throw new ArgumentException("A protocol response cannot be empty.", nameof(response));
            }
            OcgCoreNative.DuelSetResponse(duel, response, (uint)response.Length);
            Process();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            duel.Dispose();
            lock (allocationGate)
            {
                foreach (IntPtr allocation in setcodeAllocations.Values)
                {
                    if (allocation != IntPtr.Zero) Marshal.FreeHGlobal(allocation);
                }
                setcodeAllocations.Clear();
            }
            if (selfHandle.IsAllocated) selfHandle.Free();
            GC.SuppressFinalize(this);
        }

        private void AddDeck(byte team, uint[] cards, uint location)
        {
            if (cards == null) return;
            for (int i = cards.Length - 1; i >= 0; i--)
            {
                AddCardAtNative(
                    team,
                    cards[i],
                    location,
                    0,
                    FaceDownDefense);
            }
        }

        private void LoadRequiredScript(string name)
        {
            if (!scripts.TryRead(name, out byte[] bytes))
            {
                throw new FileNotFoundException($"Required ocgcore global script '{name}' is missing.");
            }
            if (OcgCoreNative.LoadScript(duel.DangerousGetHandle(), bytes, (uint)bytes.Length, name) == 0)
            {
                throw new InvalidDataException($"ocgcore rejected required global script '{name}'.");
            }
        }

        private void CopyAndDecodeMessages()
        {
            IntPtr source = OcgCoreNative.DuelGetMessage(duel, out uint length);
            if (length == 0) return;
            if (source == IntPtr.Zero || length > 16 * 1024 * 1024)
            {
                RuntimeDiagnosticRecorder.Record(
                    "F03",
                    "Protocol",
                    nameof(OcgDuelEngine),
                    "Core returned an invalid message buffer.",
                    details: $"length={length}; sourceIsNull={source == IntPtr.Zero}");
                throw new CoreProtocolException($"ocgcore returned an invalid message buffer ({length} bytes).");
            }
            byte[] copy = new byte[length];
            Marshal.Copy(source, copy, 0, (int)length);
            foreach (DuelEvent duelEvent in CoreMessageDecoder.Decode(
                         copy,
                         database.Cards))
            {
                RemapEventToLogicalSeats(duelEvent);
                if (duelEvent.Message == CoreMessage.Retry &&
                    retryFallbackPrompt != null)
                {
                    // Clone the prompt with a fresh RequestId so the UI
                    // layer recognises it as a new prompt and redraws.
                    DuelPrompt clone = new DuelPrompt
                    {
                        RequestId = ++nextRequestId,
                        Message = retryFallbackPrompt.Message,
                        Player = retryFallbackPrompt.Player,
                        Title = retryFallbackPrompt.Title,
                        Forced = retryFallbackPrompt.Forced,
                        Cancelable = retryFallbackPrompt.Cancelable,
                        MinimumSelections = retryFallbackPrompt.MinimumSelections,
                        MaximumSelections = retryFallbackPrompt.MaximumSelections,
                        RequiredSum = retryFallbackPrompt.RequiredSum,
                        SumAtLeast = retryFallbackPrompt.SumAtLeast,
                        RequiresOrderedSelection =
                            retryFallbackPrompt.RequiresOrderedSelection,
                        RequiresMaskSelection =
                            retryFallbackPrompt.RequiresMaskSelection,
                        CounterType = retryFallbackPrompt.CounterType,
                        RequiredCounterCount =
                            retryFallbackPrompt.RequiredCounterCount,
                        MaskWidth = retryFallbackPrompt.MaskWidth
                    };
                    foreach (uint sum in retryFallbackPrompt.MandatorySums)
                        clone.MandatorySums.Add(sum);
                    foreach (DuelChoice choice in retryFallbackPrompt.Choices)
                        clone.Choices.Add(CloneChoice(choice, clone.RequestId));
                    duelEvent.Prompt = clone;
                    duelEvent.Player = clone.Player;
                }
                Emit(duelEvent);
            }
        }

        private void Emit(DuelEvent duelEvent)
        {
            history.Add(duelEvent);
            if (duelEvent.Message == CoreMessage.Win)
            {
                hasWinner = true;
                CurrentPrompt = null;
            }
            else if (duelEvent.Prompt != null && !hasWinner)
            {
                if (duelEvent.Prompt.RequestId == 0)
                {
                    duelEvent.Prompt.RequestId = ++nextRequestId;
                    foreach (DuelChoice choice in duelEvent.Prompt.Choices)
                        choice.RequestId = duelEvent.Prompt.RequestId;
                }
                CurrentPrompt = duelEvent.Prompt;
            }
            EventReceived?.Invoke(duelEvent);
        }

        private static OcgPlayer Player(
            DuelConfiguration configuration,
            uint startingLifePoints)
        {
            return new OcgPlayer
            {
                StartingLifePoints = startingLifePoints,
                StartingDrawCount = configuration.StartingHand,
                DrawCountPerTurn = configuration.DrawPerTurn
            };
        }

        private static ulong SplitMix64(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static uint[] Shuffled(uint[] source, ulong seed)
        {
            if (source == null) return null;
            uint[] result = (uint[])source.Clone();
            ulong state = seed;
            for (int i = result.Length - 1; i > 0; i--)
            {
                int selected = (int)(SplitMix64(ref state) % (ulong)(i + 1));
                uint temporary = result[i];
                result[i] = result[selected];
                result[selected] = temporary;
            }
            return result;
        }

        private static OcgDuelEngine Owner(IntPtr payload)
        {
            return (OcgDuelEngine)GCHandle.FromIntPtr(payload).Target;
        }

        private static DuelChoice CloneChoice(
            DuelChoice source,
            ulong requestId)
        {
            return new DuelChoice
            {
                RequestId = requestId,
                RuntimeId = source.RuntimeId,
                Label = source.Label,
                CardCode = source.CardCode,
                Response = source.Response == null
                    ? null
                    : (byte[])source.Response.Clone(),
                HasLocation = source.HasLocation,
                Controller = source.Controller,
                Location = source.Location,
                Sequence = source.Sequence,
                Position = source.Position,
                DirectAttackAvailable = source.DirectAttackAvailable,
                ChoiceIndex = source.ChoiceIndex,
                DescriptionId = source.DescriptionId,
                SumValue = source.SumValue
            };
        }

        private bool SeatsAreSwapped => configuration.StartingPlayer == 1;

        private byte NativePlayerForLogical(byte player)
        {
            if (player > 1 || !SeatsAreSwapped)
                return player;
            return (byte)(1 - player);
        }

        private byte LogicalPlayerForNative(byte player)
        {
            return NativePlayerForLogical(player);
        }

        private void RemapEventToLogicalSeats(DuelEvent duelEvent)
        {
            if (!SeatsAreSwapped || duelEvent == null)
                return;

            duelEvent.Player = LogicalPlayerForNative(duelEvent.Player);
            RemapLocation(duelEvent.Previous);
            RemapLocation(duelEvent.Current);
            RemapLocations(duelEvent.PreviousLocations);
            RemapLocations(duelEvent.CurrentLocations);
            if (duelEvent.Prompt == null)
                return;

            duelEvent.Prompt.Player =
                LogicalPlayerForNative(duelEvent.Prompt.Player);
            foreach (DuelChoice choice in duelEvent.Prompt.Choices)
            {
                if (choice != null && choice.Controller <= 1)
                {
                    // Response is deliberately kept byte-for-byte native.
                    // Only presentation metadata uses logical seats.
                    choice.Controller =
                        LogicalPlayerForNative(choice.Controller);
                }
            }
        }

        private void RemapLocations(CardLocation[] locations)
        {
            if (locations == null)
                return;
            foreach (CardLocation location in locations)
                RemapLocation(location);
        }

        private void RemapLocation(CardLocation location)
        {
            if (location != null && location.Controller <= 1)
            {
                location.Controller =
                    LogicalPlayerForNative(location.Controller);
            }
        }

        [MonoPInvokeCallback(typeof(OcgDataReader))]
        private static void OnCardRead(IntPtr payload, uint code, IntPtr data)
        {
            OcgDuelEngine owner = Owner(payload);
            try
            {
                if (!owner.database.TryGet(code, out CardRecord record))
                {
                    // The official Eye of Timaeus script assigns this
                    // name-only code. It is not a physical card and therefore
                    // has no BabelCDB row, but ocgcore still asks the callback
                    // to resolve it while applying EFFECT_ADD_CODE.
                    if (code == 10000050)
                    {
                        Marshal.StructureToPtr(
                            new OcgCardData { Code = code },
                            data,
                            false);
                        return;
                    }
                    throw new KeyNotFoundException($"ocgcore requested card {code:00000000}, outside the pinned catalog.");
                }
                IntPtr setcodes = IntPtr.Zero;
                if (record.Setcodes != null && record.Setcodes.Length > 0)
                {
                    setcodes = Marshal.AllocHGlobal((record.Setcodes.Length + 1) * sizeof(ushort));
                    try
                    {
                        for (int i = 0; i < record.Setcodes.Length; i++)
                        {
                            Marshal.WriteInt16(setcodes, i * sizeof(ushort), unchecked((short)record.Setcodes[i]));
                        }
                        Marshal.WriteInt16(setcodes, record.Setcodes.Length * sizeof(ushort), 0);
                        Marshal.StructureToPtr(record.ToNative(setcodes), data, false);
                        lock (owner.allocationGate) owner.setcodeAllocations[data] = setcodes;
                        setcodes = IntPtr.Zero;
                    }
                    finally
                    {
                        if (setcodes != IntPtr.Zero) Marshal.FreeHGlobal(setcodes);
                    }
                }
                else
                {
                    Marshal.StructureToPtr(record.ToNative(IntPtr.Zero), data, false);
                }
            }
            catch (Exception exception)
            {
                owner.callbackFailure = exception;
                Marshal.StructureToPtr(new OcgCardData { Code = code }, data, false);
            }
        }

        [MonoPInvokeCallback(typeof(OcgDataReaderDone))]
        private static void OnCardReadDone(IntPtr payload, IntPtr data)
        {
            OcgDuelEngine owner = Owner(payload);
            try
            {
                lock (owner.allocationGate)
                {
                    if (owner.setcodeAllocations.TryGetValue(data, out IntPtr allocation))
                    {
                        if (allocation != IntPtr.Zero) Marshal.FreeHGlobal(allocation);
                        owner.setcodeAllocations.Remove(data);
                    }
                }
            }
            catch (Exception exception)
            {
                owner.callbackFailure = exception;
            }
        }

        [MonoPInvokeCallback(typeof(OcgScriptReader))]
        private static int OnScriptRead(IntPtr payload, IntPtr nativeDuel, IntPtr nativeName)
        {
            OcgDuelEngine owner = Owner(payload);
            try
            {
                string name = Marshal.PtrToStringAnsi(nativeName);
                if (string.IsNullOrEmpty(name) || !owner.scripts.TryRead(name, out byte[] script))
                {
                    owner.nativeLogs.Add($"SCRIPT_MISSING {name}");
                    RuntimeDiagnosticRecorder.Record(
                        "F01",
                        "CardScripts",
                        nameof(OcgDuelEngine),
                        "Core requested a missing card script.",
                        details: name ?? string.Empty);
                    return 0;
                }
                return OcgCoreNative.LoadScript(nativeDuel, script, (uint)script.Length, name);
            }
            catch (Exception exception)
            {
                owner.callbackFailure = exception;
                return 0;
            }
        }

        [MonoPInvokeCallback(typeof(OcgLogHandler))]
        private static void OnNativeLog(IntPtr payload, IntPtr nativeMessage, int type)
        {
            OcgDuelEngine owner = Owner(payload);
            string message = Marshal.PtrToStringAnsi(nativeMessage) ?? string.Empty;
            owner.nativeLogs.Add($"[{type}] {message}");
            if (type == 0)
            {
                RuntimeDiagnosticRecorder.Record(
                    "F02",
                    "Core",
                    nameof(OcgDuelEngine),
                    "Core emitted an error log.",
                    details: message);
                TryWriteUnityLog(message, true);
            }
            else TryWriteUnityLog(message, false);
        }

        private static void TryWriteUnityLog(string message, bool error)
        {
            try
            {
                if (error) Debug.LogError($"[ocgcore] {message}");
                else Debug.Log($"[ocgcore] {message}");
            }
            catch
            {
                // Logging is observational only. A platform logging backend,
                // headless validation host or shutdown race must never escape
                // through the native callback and interrupt a card effect.
            }
        }

        private void ThrowCallbackFailure()
        {
            if (callbackFailure == null) return;
            Exception failure = callbackFailure;
            callbackFailure = null;
            RuntimeDiagnosticRecorder.Record(
                "F03",
                "Interop",
                nameof(OcgDuelEngine),
                "Core callback failed.",
                RuntimeDiagnosticSeverity.Critical,
                exception: failure);
            throw new InvalidOperationException("An ocgcore callback failed.", failure);
        }

        private void EnsureUsable()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OcgDuelEngine));
        }
    }
}
