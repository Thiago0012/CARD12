using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ArcaneDuel.DuelEngine.Abstractions;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Content;
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
        public uint StartingLifePoints { get; set; } = 8000;
        public uint StartingHand { get; set; } = 5;
        public uint DrawPerTurn { get; set; } = 1;
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
            unchecked
            {
                ulong ticks = (ulong)DateTime.UtcNow.Ticks;
                ulong monotonic = unchecked((uint)Environment.TickCount);
                ulong counter = (ulong)System.Threading.Interlocked.Increment(
                    ref runtimeSeedCounter);
                ulong seed = ticks ^ (monotonic << 21) ^
                             (counter * 0x9E3779B97F4A7C15UL);
                return seed != 0
                    ? seed
                    : 0xA7C4D3E2198B6501UL ^ counter;
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

    public sealed class OcgDuelEngine : IDuelRulesEngine
    {
        private const ulong MasterRule5 = 0x2E800UL;
        private const ulong TcgSegocNonPublic = 0x100000000UL;
        private const ulong TcgSegocFirstTrigger = 0x200000000UL;
        private const ulong SimpleAi = 0x40UL;
        private const uint FaceDownDefense = 0x8;
        private const uint QueryAttack = 0x100;
        private const uint QueryDefense = 0x200;
        private const uint QueryEnd = 0x80000000;

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
            selfHandle = GCHandle.Alloc(this);
            IntPtr payload = GCHandle.ToIntPtr(selfHandle);

            ulong seedState = configuration.Seed;
            var options = new OcgDuelOptions
            {
                Seed0 = SplitMix64(ref seedState),
                Seed1 = SplitMix64(ref seedState),
                Seed2 = SplitMix64(ref seedState),
                Seed3 = SplitMix64(ref seedState),
                Flags = RuleFlagsFor(configuration.RuleProfile) |
                        (configuration.SimpleOpponentAi ? SimpleAi : 0),
                Team1 = Player(configuration),
                Team2 = Player(configuration),
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
                selfHandle.Free();
                throw new InvalidOperationException($"ocgcore failed to create the duel: {(OcgDuelCreationStatus)creation}.");
            }
            duel.Initialize(nativeDuel);
            LoadRequiredScript("constant.lua");
            LoadRequiredScript("utility.lua");
            ThrowCallbackFailure();
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
                Controller = controller,
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
                throw new InvalidDataException(
                    "The selected duel contains unsupported cards: " +
                    string.Join(" | ", unsupported));
            }
            AddDeck(
                0,
                configuration.ShuffleMainDecks
                    ? Shuffled(configuration.PlayerDeck, configuration.Seed ^ 0xA11CE001UL)
                    : configuration.PlayerDeck,
                DuelLocation.Deck);
            AddDeck(0, configuration.PlayerExtraDeck, DuelLocation.Extra);
            AddDeck(
                1,
                configuration.ShuffleMainDecks
                    ? Shuffled(configuration.OpponentDeck, configuration.Seed ^ 0xA11CE002UL)
                    : configuration.OpponentDeck,
                DuelLocation.Deck);
            AddDeck(1, configuration.OpponentExtraDeck, DuelLocation.Extra);
            OcgCoreNative.StartDuel(duel);
            IsStarted = true;
            Emit(new DuelEvent
            {
                Message = CoreMessage.Start,
                RawMessage = (byte)CoreMessage.Start,
                Value = configuration.StartingLifePoints,
                Detail = "Duelo iniciado pelo ocgcore"
            });
            Process();
        }

        public OcgDuelStatus Process()
        {
            EnsureUsable();
            if (!IsStarted) throw new InvalidOperationException("Start the duel before processing it.");
            CurrentPrompt = null;
            int iterations = 0;
            do
            {
                if (++iterations > 4096)
                {
                    throw new InvalidOperationException("ocgcore exceeded the processing safety limit.");
                }
                Status = (OcgDuelStatus)OcgCoreNative.DuelProcess(duel);
                CopyAndDecodeMessages();
                ThrowCallbackFailure();
            }
            while (Status == OcgDuelStatus.Continue);
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
                AddCard(team, cards[i], location);
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
                throw new CoreProtocolException($"ocgcore returned an invalid message buffer ({length} bytes).");
            }
            byte[] copy = new byte[length];
            Marshal.Copy(source, copy, 0, (int)length);
            foreach (DuelEvent duelEvent in CoreMessageDecoder.Decode(
                         copy,
                         database.Cards))
            {
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

        private static OcgPlayer Player(DuelConfiguration configuration)
        {
            return new OcgPlayer
            {
                StartingLifePoints = configuration.StartingLifePoints,
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
                    for (int i = 0; i < record.Setcodes.Length; i++)
                    {
                        Marshal.WriteInt16(setcodes, i * sizeof(ushort), unchecked((short)record.Setcodes[i]));
                    }
                    Marshal.WriteInt16(setcodes, record.Setcodes.Length * sizeof(ushort), 0);
                }
                Marshal.StructureToPtr(record.ToNative(setcodes), data, false);
                lock (owner.allocationGate) owner.setcodeAllocations[data] = setcodes;
            }
            catch (Exception exception)
            {
                owner.callbackFailure = exception;
                Marshal.StructureToPtr(new OcgCardData { Code = code }, data, false);
            }
        }

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

        private static int OnScriptRead(IntPtr payload, IntPtr nativeDuel, IntPtr nativeName)
        {
            OcgDuelEngine owner = Owner(payload);
            try
            {
                string name = Marshal.PtrToStringAnsi(nativeName);
                if (string.IsNullOrEmpty(name) || !owner.scripts.TryRead(name, out byte[] script))
                {
                    owner.nativeLogs.Add($"SCRIPT_MISSING {name}");
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

        private static void OnNativeLog(IntPtr payload, IntPtr nativeMessage, int type)
        {
            OcgDuelEngine owner = Owner(payload);
            string message = Marshal.PtrToStringAnsi(nativeMessage) ?? string.Empty;
            owner.nativeLogs.Add($"[{type}] {message}");
            if (type == 0) Debug.LogError($"[ocgcore] {message}");
            else Debug.Log($"[ocgcore] {message}");
        }

        private void ThrowCallbackFailure()
        {
            if (callbackFailure == null) return;
            Exception failure = callbackFailure;
            callbackFailure = null;
            throw new InvalidOperationException("An ocgcore callback failed.", failure);
        }

        private void EnsureUsable()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OcgDuelEngine));
        }
    }
}
