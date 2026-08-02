using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Logical payload carried by the v3 duel wire envelope. New values can
    /// be added without changing the binary envelope itself.
    /// </summary>
    public enum DuelWireKind : ushort
    {
        Unknown = 0,
        Deck = 1,
        State = 2,
        Start = 3,
        Response = 4,
        Control = 5
    }

    public enum DuelWirePacketType : byte
    {
        Data = 1,
        ChunkAck = 2,
        TransferAck = 3
    }

    public enum DuelWireAcceptResult
    {
        Rejected = 0,
        Accepted = 1,
        Duplicate = 2,
        Completed = 3
    }

    public enum DuelWireAckResult
    {
        Rejected = 0,
        Accepted = 1,
        Duplicate = 2,
        Completed = 3
    }

    /// <summary>
    /// One immutable v3 packet. Payload bytes are never exposed by reference;
    /// callers can request a defensive copy when diagnostics require it.
    /// </summary>
    public sealed class DuelWirePacket
    {
        private readonly byte[] data;

        public DuelWirePacketType PacketType { get; }
        public DuelWireKind Kind { get; }
        public Guid TransferId { get; }
        public int TotalLength { get; }
        public ushort ChunkIndex { get; }
        public ushort ChunkCount { get; }
        public ulong PayloadChecksum { get; }
        public int DataLength => data.Length;
        public bool IsData => PacketType == DuelWirePacketType.Data;
        public bool IsChunkAck => PacketType == DuelWirePacketType.ChunkAck;
        public bool IsTransferAck => PacketType == DuelWirePacketType.TransferAck;

        internal byte[] DataUnsafe => data;
        internal byte[] EncodedCache { get; set; }

        internal DuelWirePacket(
            DuelWirePacketType packetType,
            DuelWireKind kind,
            Guid transferId,
            int totalLength,
            ushort chunkIndex,
            ushort chunkCount,
            ulong payloadChecksum,
            byte[] packetData,
            bool takeOwnership)
        {
            PacketType = packetType;
            Kind = kind;
            TransferId = transferId;
            TotalLength = totalLength;
            ChunkIndex = chunkIndex;
            ChunkCount = chunkCount;
            PayloadChecksum = payloadChecksum;
            byte[] source = packetData ?? Array.Empty<byte>();
            data = takeOwnership ? source : (byte[])source.Clone();
        }

        public byte[] GetDataCopy()
        {
            return (byte[])data.Clone();
        }
    }

    /// <summary>
    /// Immutable set of packets representing a complete logical payload.
    /// Keep this object until its transfer ACK arrives so missing chunks can
    /// be resent without serializing the payload again.
    /// </summary>
    public sealed class DuelWireTransfer
    {
        private readonly DuelWirePacket[] packets;

        public DuelWireKind Kind { get; }
        public Guid TransferId { get; }
        public int TotalLength { get; }
        public ushort ChunkCount => (ushort)packets.Length;
        public ulong PayloadChecksum { get; }
        public IReadOnlyList<DuelWirePacket> Packets => packets;

        internal DuelWireTransfer(
            DuelWireKind kind,
            Guid transferId,
            int totalLength,
            ulong payloadChecksum,
            DuelWirePacket[] packets)
        {
            Kind = kind;
            TransferId = transferId;
            TotalLength = totalLength;
            PayloadChecksum = payloadChecksum;
            this.packets = packets ?? throw new ArgumentNullException(
                nameof(packets));
        }

        public DuelWirePacket GetPacket(int index)
        {
            if (index < 0 || index >= packets.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return packets[index];
        }
    }

    /// <summary>
    /// Binary, platform-neutral v3 codec. Every packet is small enough for a
    /// normal unfragmented NGO datagram. Reliability comes from the explicit
    /// chunk/final ACK protocol above, never from a transport-wide ordered
    /// stream. All integers use little-endian encoding.
    /// </summary>
    public static class DuelWireProtocol
    {
        public const byte Version = 3;
        public const int ChunkDataBytes = 800;
        public const int MaximumPayloadBytes = 512 * 1024;
        public const int HeaderBytes = 48;
        public const int MaximumEncodedPacketBytes =
            HeaderBytes + ChunkDataBytes;
        public const int MaximumWriterPacketBytes =
            sizeof(ushort) + MaximumEncodedPacketBytes;

        private const uint Magic = 0x33575544u; // "DUW3" little-endian.
        private const int MagicOffset = 0;
        private const int VersionOffset = 4;
        private const int PacketTypeOffset = 5;
        private const int KindOffset = 6;
        private const int TransferIdOffset = 8;
        private const int TotalLengthOffset = 24;
        private const int ChunkIndexOffset = 28;
        private const int ChunkCountOffset = 30;
        private const int ChunkLengthOffset = 32;
        private const int ReservedOffset = 34;
        private const int PayloadChecksumOffset = 36;
        private const int PacketChecksumOffset = 44;
        private const uint Fnv32Offset = 2166136261u;
        private const uint Fnv32Prime = 16777619u;
        private const ulong Fnv64Offset = 14695981039346656037ul;
        private const ulong Fnv64Prime = 1099511628211ul;

        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        public static DuelWireTransfer CreateUtf8Transfer(
            DuelWireKind kind,
            string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            return CreateTransfer(kind, StrictUtf8.GetBytes(text));
        }

        public static DuelWireTransfer CreateTransfer(
            DuelWireKind kind,
            byte[] payload)
        {
            return CreateTransfer(kind, payload, Guid.NewGuid());
        }

        public static DuelWireTransfer CreateTransfer(
            DuelWireKind kind,
            byte[] payload,
            Guid transferId)
        {
            ValidateTransferArguments(kind, payload, transferId);

            int chunkCount = ExpectedChunkCount(payload.Length);
            ulong checksum = ComputePayloadChecksum(payload);
            var packets = new DuelWirePacket[chunkCount];
            for (int index = 0; index < chunkCount; index++)
            {
                int offset = index * ChunkDataBytes;
                int length = Math.Min(
                    ChunkDataBytes,
                    payload.Length - offset);
                if (payload.Length == 0)
                    length = 0;

                var data = new byte[length];
                if (length > 0)
                    Buffer.BlockCopy(payload, offset, data, 0, length);
                packets[index] = new DuelWirePacket(
                    DuelWirePacketType.Data,
                    kind,
                    transferId,
                    payload.Length,
                    (ushort)index,
                    (ushort)chunkCount,
                    checksum,
                    data,
                    true);
            }

            return new DuelWireTransfer(
                kind,
                transferId,
                payload.Length,
                checksum,
                packets);
        }

        public static DuelWirePacket CreateChunkAck(
            DuelWirePacket dataPacket)
        {
            if (dataPacket == null)
                throw new ArgumentNullException(nameof(dataPacket));
            if (!dataPacket.IsData)
                throw new ArgumentException(
                    "Only a data packet can be acknowledged as a chunk.",
                    nameof(dataPacket));

            return CreateAck(
                DuelWirePacketType.ChunkAck,
                dataPacket.Kind,
                dataPacket.TransferId,
                dataPacket.TotalLength,
                dataPacket.ChunkIndex,
                dataPacket.ChunkCount,
                dataPacket.PayloadChecksum);
        }

        public static DuelWirePacket CreateTransferAck(
            DuelWireTransfer transfer)
        {
            if (transfer == null)
                throw new ArgumentNullException(nameof(transfer));
            return CreateAck(
                DuelWirePacketType.TransferAck,
                transfer.Kind,
                transfer.TransferId,
                transfer.TotalLength,
                ushort.MaxValue,
                transfer.ChunkCount,
                transfer.PayloadChecksum);
        }

        public static DuelWirePacket CreateTransferAck(
            DuelWireReassembler reassembler)
        {
            if (reassembler == null)
                throw new ArgumentNullException(nameof(reassembler));
            if (!reassembler.IsComplete)
                throw new InvalidOperationException(
                    "A transfer ACK can only be created after reassembly.");
            return CreateAck(
                DuelWirePacketType.TransferAck,
                reassembler.Kind,
                reassembler.TransferId,
                reassembler.TotalLength,
                ushort.MaxValue,
                reassembler.ChunkCount,
                reassembler.PayloadChecksum);
        }

        public static byte[] EncodePacket(DuelWirePacket packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));
            if (!ValidatePacketMetadata(packet, out string error))
                throw new ArgumentException(error, nameof(packet));

            int dataLength = packet.DataLength;
            var result = new byte[HeaderBytes + dataLength];
            WriteUInt32(result, MagicOffset, Magic);
            result[VersionOffset] = Version;
            result[PacketTypeOffset] = (byte)packet.PacketType;
            WriteUInt16(result, KindOffset, (ushort)packet.Kind);
            byte[] guid = packet.TransferId.ToByteArray();
            Buffer.BlockCopy(guid, 0, result, TransferIdOffset, guid.Length);
            WriteInt32(result, TotalLengthOffset, packet.TotalLength);
            WriteUInt16(result, ChunkIndexOffset, packet.ChunkIndex);
            WriteUInt16(result, ChunkCountOffset, packet.ChunkCount);
            WriteUInt16(result, ChunkLengthOffset, (ushort)dataLength);
            WriteUInt16(result, ReservedOffset, 0);
            WriteUInt64(
                result,
                PayloadChecksumOffset,
                packet.PayloadChecksum);
            if (dataLength > 0)
            {
                Buffer.BlockCopy(
                    packet.DataUnsafe,
                    0,
                    result,
                    HeaderBytes,
                    dataLength);
            }
            WriteUInt32(
                result,
                PacketChecksumOffset,
                ComputePacketChecksum(result));
            return result;
        }

        public static bool TryDecodePacket(
            byte[] encoded,
            out DuelWirePacket packet,
            out string error)
        {
            packet = null;
            error = string.Empty;
            if (encoded == null)
            {
                error = "Packet buffer is null.";
                return false;
            }
            if (encoded.Length < HeaderBytes ||
                encoded.Length > MaximumEncodedPacketBytes)
            {
                error = $"Packet length {encoded.Length} is outside the " +
                    $"safe range {HeaderBytes}-{MaximumEncodedPacketBytes}.";
                return false;
            }
            if (ReadUInt32(encoded, MagicOffset) != Magic)
            {
                error = "Packet magic is invalid.";
                return false;
            }
            if (encoded[VersionOffset] != Version)
            {
                error = $"Unsupported duel wire version " +
                    $"{encoded[VersionOffset]}.";
                return false;
            }

            uint storedPacketChecksum = ReadUInt32(
                encoded,
                PacketChecksumOffset);
            if (storedPacketChecksum != ComputePacketChecksum(encoded))
            {
                error = "Packet checksum mismatch; data or metadata is corrupt.";
                return false;
            }

            var packetType = (DuelWirePacketType)encoded[PacketTypeOffset];
            var kind = (DuelWireKind)ReadUInt16(encoded, KindOffset);
            var guidBytes = new byte[16];
            Buffer.BlockCopy(
                encoded,
                TransferIdOffset,
                guidBytes,
                0,
                guidBytes.Length);
            var transferId = new Guid(guidBytes);
            int totalLength = ReadInt32(encoded, TotalLengthOffset);
            ushort chunkIndex = ReadUInt16(encoded, ChunkIndexOffset);
            ushort chunkCount = ReadUInt16(encoded, ChunkCountOffset);
            ushort chunkLength = ReadUInt16(encoded, ChunkLengthOffset);
            ushort reserved = ReadUInt16(encoded, ReservedOffset);
            ulong payloadChecksum = ReadUInt64(
                encoded,
                PayloadChecksumOffset);

            if (reserved != 0)
            {
                error = "Reserved packet metadata must be zero.";
                return false;
            }
            if (encoded.Length != HeaderBytes + chunkLength)
            {
                error = "Encoded packet length does not match chunk metadata.";
                return false;
            }

            var data = new byte[chunkLength];
            if (chunkLength > 0)
            {
                Buffer.BlockCopy(
                    encoded,
                    HeaderBytes,
                    data,
                    0,
                    chunkLength);
            }
            var candidate = new DuelWirePacket(
                packetType,
                kind,
                transferId,
                totalLength,
                chunkIndex,
                chunkCount,
                payloadChecksum,
                data,
                true);
            if (!ValidatePacketMetadata(candidate, out error))
                return false;

            packet = candidate;
            return true;
        }

        /// <summary>
        /// Writes one length-prefixed binary packet to an NGO writer. The
        /// resulting writer growth is at most MaximumWriterPacketBytes.
        /// </summary>
        public static bool TryWritePacket(
            ref FastBufferWriter writer,
            DuelWirePacket packet,
            out string error)
        {
            error = string.Empty;
            try
            {
                byte[] encoded = packet?.EncodedCache;
                if (encoded == null)
                {
                    encoded = EncodePacket(packet);
                    packet.EncodedCache = encoded;
                }
                writer.WriteValueSafe((ushort)encoded.Length);
                writer.WriteBytesSafe(encoded);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }

        /// <summary>
        /// Reads one packet written by TryWritePacket from an NGO reader.
        /// </summary>
        public static bool TryReadPacket(
            ref FastBufferReader reader,
            out DuelWirePacket packet,
            out string error)
        {
            packet = null;
            error = string.Empty;
            try
            {
                reader.ReadValueSafe(out ushort length);
                if (length < HeaderBytes ||
                    length > MaximumEncodedPacketBytes)
                {
                    error = $"Length prefix {length} is outside the safe " +
                        "packet range.";
                    return false;
                }
                var encoded = new byte[length];
                reader.ReadBytesSafe(ref encoded, length);
                return TryDecodePacket(encoded, out packet, out error);
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }

        public static bool TryDecodeUtf8(
            byte[] payload,
            out string text,
            out string error)
        {
            text = string.Empty;
            error = string.Empty;
            if (payload == null)
            {
                error = "UTF-8 payload is null.";
                return false;
            }
            if (payload.Length > MaximumPayloadBytes)
            {
                error = "UTF-8 payload exceeds the safe transfer limit.";
                return false;
            }
            try
            {
                text = StrictUtf8.GetString(payload);
                return true;
            }
            catch (DecoderFallbackException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static ulong ComputePayloadChecksum(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            ulong hash = Fnv64Offset;
            for (int index = 0; index < payload.Length; index++)
            {
                hash ^= payload[index];
                hash *= Fnv64Prime;
            }
            return hash;
        }

        internal static int ExpectedChunkCount(int totalLength)
        {
            if (totalLength < 0 || totalLength > MaximumPayloadBytes)
                return -1;
            return Math.Max(
                1,
                (totalLength + ChunkDataBytes - 1) / ChunkDataBytes);
        }

        private static DuelWirePacket CreateAck(
            DuelWirePacketType packetType,
            DuelWireKind kind,
            Guid transferId,
            int totalLength,
            ushort chunkIndex,
            ushort chunkCount,
            ulong payloadChecksum)
        {
            var result = new DuelWirePacket(
                packetType,
                kind,
                transferId,
                totalLength,
                chunkIndex,
                chunkCount,
                payloadChecksum,
                Array.Empty<byte>(),
                true);
            if (!ValidatePacketMetadata(result, out string error))
                throw new ArgumentException(error);
            return result;
        }

        private static void ValidateTransferArguments(
            DuelWireKind kind,
            byte[] payload,
            Guid transferId)
        {
            if (kind == DuelWireKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (payload.Length > MaximumPayloadBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(payload),
                    $"Payload exceeds {MaximumPayloadBytes} bytes.");
            }
            if (transferId == Guid.Empty)
                throw new ArgumentException(
                    "Transfer id cannot be empty.",
                    nameof(transferId));
        }

        private static bool ValidatePacketMetadata(
            DuelWirePacket packet,
            out string error)
        {
            error = string.Empty;
            if (packet.Kind == DuelWireKind.Unknown)
            {
                error = "Payload kind cannot be Unknown.";
                return false;
            }
            if (packet.TransferId == Guid.Empty)
            {
                error = "Transfer id cannot be empty.";
                return false;
            }
            int expectedCount = ExpectedChunkCount(packet.TotalLength);
            if (expectedCount < 1 || expectedCount > ushort.MaxValue ||
                packet.ChunkCount != expectedCount)
            {
                error = "Chunk count does not match total payload length.";
                return false;
            }

            switch (packet.PacketType)
            {
                case DuelWirePacketType.Data:
                    if (packet.ChunkIndex >= packet.ChunkCount)
                    {
                        error = "Data chunk index is out of range.";
                        return false;
                    }
                    int expectedLength = ExpectedChunkLength(
                        packet.TotalLength,
                        packet.ChunkIndex,
                        packet.ChunkCount);
                    if (packet.DataLength != expectedLength)
                    {
                        error = "Data chunk length does not match metadata.";
                        return false;
                    }
                    return true;

                case DuelWirePacketType.ChunkAck:
                    if (packet.DataLength != 0 ||
                        packet.ChunkIndex >= packet.ChunkCount)
                    {
                        error = "Chunk ACK metadata is invalid.";
                        return false;
                    }
                    return true;

                case DuelWirePacketType.TransferAck:
                    if (packet.DataLength != 0 ||
                        packet.ChunkIndex != ushort.MaxValue)
                    {
                        error = "Transfer ACK metadata is invalid.";
                        return false;
                    }
                    return true;

                default:
                    error = "Packet type is invalid.";
                    return false;
            }
        }

        private static int ExpectedChunkLength(
            int totalLength,
            ushort chunkIndex,
            ushort chunkCount)
        {
            if (totalLength == 0)
                return 0;
            if (chunkIndex + 1 < chunkCount)
                return ChunkDataBytes;
            return totalLength - (chunkCount - 1) * ChunkDataBytes;
        }

        private static uint ComputePacketChecksum(byte[] encoded)
        {
            uint hash = Fnv32Offset;
            for (int index = 0; index < encoded.Length; index++)
            {
                if (index >= PacketChecksumOffset &&
                    index < PacketChecksumOffset + sizeof(uint))
                {
                    continue;
                }
                hash ^= encoded[index];
                hash *= Fnv32Prime;
            }
            return hash;
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] |
                buffer[offset + 1] << 8);
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] |
                buffer[offset + 1] << 8 |
                buffer[offset + 2] << 16 |
                buffer[offset + 3] << 24);
        }

        private static int ReadInt32(byte[] buffer, int offset)
        {
            return unchecked((int)ReadUInt32(buffer, offset));
        }

        private static ulong ReadUInt64(byte[] buffer, int offset)
        {
            uint low = ReadUInt32(buffer, offset);
            uint high = ReadUInt32(buffer, offset + sizeof(uint));
            return low | (ulong)high << 32;
        }

        private static void WriteUInt16(
            byte[] buffer,
            int offset,
            ushort value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(
            byte[] buffer,
            int offset,
            uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt32(
            byte[] buffer,
            int offset,
            int value)
        {
            WriteUInt32(buffer, offset, unchecked((uint)value));
        }

        private static void WriteUInt64(
            byte[] buffer,
            int offset,
            ulong value)
        {
            WriteUInt32(buffer, offset, (uint)value);
            WriteUInt32(buffer, offset + sizeof(uint), (uint)(value >> 32));
        }
    }

    /// <summary>
    /// Bounded, order-independent and duplicate-safe transfer assembler.
    /// One instance handles one transfer id; call Reset before reusing it.
    /// </summary>
    public sealed class DuelWireReassembler
    {
        private byte[][] chunks;
        private bool[] received;
        private int receivedCount;
        private bool initialized;
        private bool faulted;
        private bool complete;

        public DuelWireKind Kind { get; private set; }
        public Guid TransferId { get; private set; }
        public int TotalLength { get; private set; }
        public ushort ChunkCount { get; private set; }
        public ulong PayloadChecksum { get; private set; }
        public int ReceivedChunkCount => receivedCount;
        public bool IsInitialized => initialized;
        public bool IsFaulted => faulted;
        public bool IsComplete => complete;

        public DuelWireAcceptResult Accept(
            DuelWirePacket packet,
            out byte[] completedPayload,
            out string error)
        {
            completedPayload = null;
            error = string.Empty;
            if (packet == null)
            {
                error = "Packet is null.";
                return DuelWireAcceptResult.Rejected;
            }
            if (!packet.IsData)
            {
                error = "Only data packets can be reassembled.";
                return DuelWireAcceptResult.Rejected;
            }
            if (faulted)
            {
                error = "Reassembler is faulted and must be reset.";
                return DuelWireAcceptResult.Rejected;
            }

            if (!initialized)
                Initialize(packet);
            else if (!MetadataMatches(packet))
            {
                error = "Packet belongs to a different transfer or has " +
                    "conflicting metadata.";
                return DuelWireAcceptResult.Rejected;
            }

            int index = packet.ChunkIndex;
            byte[] incoming = packet.DataUnsafe;
            if (received[index])
            {
                if (!BytesEqual(chunks[index], incoming))
                {
                    faulted = true;
                    error = "A duplicate chunk contains conflicting data.";
                    return DuelWireAcceptResult.Rejected;
                }
                return DuelWireAcceptResult.Duplicate;
            }

            chunks[index] = (byte[])incoming.Clone();
            received[index] = true;
            receivedCount++;
            if (receivedCount != ChunkCount)
                return DuelWireAcceptResult.Accepted;

            var payload = new byte[TotalLength];
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                byte[] chunk = chunks[chunkIndex];
                if (chunk == null)
                {
                    faulted = true;
                    error = "Transfer completed with an absent chunk.";
                    return DuelWireAcceptResult.Rejected;
                }
                if (chunk.Length > 0)
                {
                    Buffer.BlockCopy(
                        chunk,
                        0,
                        payload,
                        chunkIndex * DuelWireProtocol.ChunkDataBytes,
                        chunk.Length);
                }
            }

            if (DuelWireProtocol.ComputePayloadChecksum(payload) !=
                PayloadChecksum)
            {
                faulted = true;
                error = "Completed transfer checksum mismatch.";
                return DuelWireAcceptResult.Rejected;
            }

            complete = true;
            completedPayload = payload;
            return DuelWireAcceptResult.Completed;
        }

        public int[] GetMissingChunkIndices()
        {
            if (!initialized)
                return Array.Empty<int>();
            var missing = new List<int>(ChunkCount - receivedCount);
            for (int index = 0; index < received.Length; index++)
            {
                if (!received[index])
                    missing.Add(index);
            }
            return missing.ToArray();
        }

        public void Reset()
        {
            chunks = null;
            received = null;
            receivedCount = 0;
            initialized = false;
            faulted = false;
            complete = false;
            Kind = DuelWireKind.Unknown;
            TransferId = Guid.Empty;
            TotalLength = 0;
            ChunkCount = 0;
            PayloadChecksum = 0;
        }

        private void Initialize(DuelWirePacket packet)
        {
            Kind = packet.Kind;
            TransferId = packet.TransferId;
            TotalLength = packet.TotalLength;
            ChunkCount = packet.ChunkCount;
            PayloadChecksum = packet.PayloadChecksum;
            chunks = new byte[ChunkCount][];
            received = new bool[ChunkCount];
            initialized = true;
        }

        private bool MetadataMatches(DuelWirePacket packet)
        {
            return packet.Kind == Kind &&
                   packet.TransferId == TransferId &&
                   packet.TotalLength == TotalLength &&
                   packet.ChunkCount == ChunkCount &&
                   packet.PayloadChecksum == PayloadChecksum;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Sender-side ACK state. Chunk ACKs permit selective retries; a transfer
    /// ACK closes the transfer after the receiver verifies its full checksum.
    /// </summary>
    public sealed class DuelWireAckTracker
    {
        private readonly bool[] acknowledged;
        private int acknowledgedCount;

        public DuelWireKind Kind { get; }
        public Guid TransferId { get; }
        public int TotalLength { get; }
        public ushort ChunkCount { get; }
        public ulong PayloadChecksum { get; }
        public bool TransferAcknowledged { get; private set; }
        public bool AllChunksAcknowledged =>
            acknowledgedCount == acknowledged.Length;
        // Only the transfer ACK proves that the receiver reassembled the
        // bytes and verified the full-payload checksum. Individual ACKs are
        // intentionally insufficient to close the sender-side transfer.
        public bool IsComplete => TransferAcknowledged;

        public DuelWireAckTracker(DuelWireTransfer transfer)
        {
            if (transfer == null)
                throw new ArgumentNullException(nameof(transfer));
            Kind = transfer.Kind;
            TransferId = transfer.TransferId;
            TotalLength = transfer.TotalLength;
            ChunkCount = transfer.ChunkCount;
            PayloadChecksum = transfer.PayloadChecksum;
            acknowledged = new bool[ChunkCount];
        }

        public DuelWireAckResult Accept(
            DuelWirePacket ack,
            out string error)
        {
            error = string.Empty;
            if (ack == null ||
                ack.PacketType != DuelWirePacketType.ChunkAck &&
                ack.PacketType != DuelWirePacketType.TransferAck)
            {
                error = "Packet is not an ACK.";
                return DuelWireAckResult.Rejected;
            }
            if (!MetadataMatches(ack))
            {
                error = "ACK belongs to a different transfer or has " +
                    "conflicting metadata.";
                return DuelWireAckResult.Rejected;
            }
            if (ack.IsTransferAck)
            {
                if (TransferAcknowledged)
                    return DuelWireAckResult.Duplicate;
                TransferAcknowledged = true;
                return DuelWireAckResult.Completed;
            }

            int index = ack.ChunkIndex;
            if (acknowledged[index])
                return DuelWireAckResult.Duplicate;
            acknowledged[index] = true;
            acknowledgedCount++;
            return DuelWireAckResult.Accepted;
        }

        public int[] GetMissingChunkIndices()
        {
            if (TransferAcknowledged)
                return Array.Empty<int>();
            var missing = new List<int>(
                acknowledged.Length - acknowledgedCount);
            for (int index = 0; index < acknowledged.Length; index++)
            {
                if (!acknowledged[index])
                    missing.Add(index);
            }
            return missing.ToArray();
        }

        public bool IsChunkAcknowledged(int index)
        {
            if (index < 0 || index >= acknowledged.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return acknowledged[index];
        }

        private bool MetadataMatches(DuelWirePacket packet)
        {
            return packet.Kind == Kind &&
                   packet.TransferId == TransferId &&
                   packet.TotalLength == TotalLength &&
                   packet.ChunkCount == ChunkCount &&
                   packet.PayloadChecksum == PayloadChecksum;
        }
    }
}
