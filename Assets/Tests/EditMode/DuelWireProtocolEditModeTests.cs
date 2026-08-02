using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    /// <summary>
    /// The codec lives in the predefined Assembly-CSharp because the online
    /// session does. Named test assemblies cannot reference predefined Unity
    /// assemblies, so these tests exercise its public API through reflection.
    /// </summary>
    public sealed class DuelWireProtocolEditModeTests
    {
        private const string Namespace = "ArcaneArena.Multiplayer.";

        private Type protocolType;
        private Type kindType;
        private Type packetType;
        private Type transferType;
        private Type reassemblerType;
        private Type ackTrackerType;

        [SetUp]
        public void ResolveCodecTypes()
        {
            protocolType = FindType(Namespace + "DuelWireProtocol");
            kindType = FindType(Namespace + "DuelWireKind");
            packetType = FindType(Namespace + "DuelWirePacket");
            transferType = FindType(Namespace + "DuelWireTransfer");
            reassemblerType = FindType(Namespace + "DuelWireReassembler");
            ackTrackerType = FindType(Namespace + "DuelWireAckTracker");

            Assert.That(protocolType, Is.Not.Null);
            Assert.That(kindType, Is.Not.Null);
            Assert.That(packetType, Is.Not.Null);
            Assert.That(transferType, Is.Not.Null);
            Assert.That(reassemblerType, Is.Not.Null);
            Assert.That(ackTrackerType, Is.Not.Null);
        }

        [Test]
        public void LargeUtf8PayloadAbove256KiBReassemblesExactly()
        {
            string original = string.Concat(
                Enumerable.Repeat("Mausoléu・Runick・ação\n", 15000));
            byte[] expected = new System.Text.UTF8Encoding(false, true)
                .GetBytes(original);
            Assert.That(expected.Length, Is.GreaterThan(256 * 1024));

            object transfer = InvokeStatic(
                "CreateUtf8Transfer",
                DeckKind(),
                original);
            var packets = GetPackets(transfer);
            object reassembler = Activator.CreateInstance(reassemblerType);
            byte[] completed = null;
            foreach (object packet in packets)
            {
                object decoded = Decode(Encode(packet));
                var result = Accept(reassembler, decoded, out byte[] payload,
                    out string error);
                Assert.That(result, Is.Not.EqualTo("Rejected"), error);
                if (payload != null)
                    completed = payload;
            }

            Assert.That(completed, Is.EqualTo(expected));
            Assert.That(GetBool(reassembler, "IsComplete"), Is.True);
            object[] decodeArgs = { completed, null, null };
            bool utf8Ok = (bool)GetMethod(
                protocolType,
                "TryDecodeUtf8",
                3).Invoke(null, decodeArgs);
            Assert.That(utf8Ok, Is.True, decodeArgs[2] as string);
            Assert.That(decodeArgs[1] as string, Is.EqualTo(original));
        }

        [Test]
        public void ReassemblyAcceptsPacketsOutOfOrder()
        {
            byte[] expected = CreatePayload(9917);
            object transfer = CreateTransfer(expected);
            var packets = GetPackets(transfer);
            packets.Reverse();
            object reassembler = Activator.CreateInstance(reassemblerType);
            byte[] completed = null;

            foreach (object packet in packets)
            {
                string result = Accept(
                    reassembler,
                    Decode(Encode(packet)),
                    out byte[] payload,
                    out string error);
                Assert.That(result, Is.Not.EqualTo("Rejected"), error);
                completed ??= payload;
            }

            Assert.That(completed, Is.EqualTo(expected));
            Assert.That(GetBool(reassembler, "IsComplete"), Is.True);
        }

        [Test]
        public void DuplicateChunkIsIdempotent()
        {
            byte[] expected = CreatePayload(2401);
            var packets = GetPackets(CreateTransfer(expected));
            object reassembler = Activator.CreateInstance(reassemblerType);

            object first = Decode(Encode(packets[0]));
            Assert.That(Accept(reassembler, first, out _, out _),
                Is.EqualTo("Accepted"));
            Assert.That(Accept(reassembler, first, out _, out string error),
                Is.EqualTo("Duplicate"), error);

            byte[] completed = null;
            for (int index = 1; index < packets.Count; index++)
            {
                Accept(
                    reassembler,
                    Decode(Encode(packets[index])),
                    out byte[] payload,
                    out error);
                completed ??= payload;
            }
            Assert.That(completed, Is.EqualTo(expected));
        }

        [Test]
        public void MissingChunkIsReportedAndDoesNotComplete()
        {
            var packets = GetPackets(CreateTransfer(CreatePayload(4200)));
            const int omittedIndex = 2;
            object reassembler = Activator.CreateInstance(reassemblerType);

            for (int index = 0; index < packets.Count; index++)
            {
                if (index == omittedIndex)
                    continue;
                string result = Accept(
                    reassembler,
                    Decode(Encode(packets[index])),
                    out byte[] completed,
                    out string error);
                Assert.That(result, Is.Not.EqualTo("Rejected"), error);
                Assert.That(completed, Is.Null);
            }

            Assert.That(GetBool(reassembler, "IsComplete"), Is.False);
            int[] missing = (int[])reassemblerType
                .GetMethod("GetMissingChunkIndices")
                .Invoke(reassembler, null);
            Assert.That(missing, Is.EqualTo(new[] { omittedIndex }));
        }

        [Test]
        public void CorruptDataOrMetadataFailsPacketChecksum()
        {
            object packet = GetPackets(CreateTransfer(CreatePayload(1600)))[0];
            byte[] corruptData = Encode(packet);
            corruptData[corruptData.Length - 1] ^= 0x5a;
            AssertDecodeRejected(corruptData, "checksum");

            byte[] corruptMetadata = Encode(packet);
            corruptMetadata[28] ^= 0x01; // Chunk index in the binary header.
            AssertDecodeRejected(corruptMetadata, "checksum");
        }

        [Test]
        public void ConflictingTransferMetadataIsRejected()
        {
            var first = GetPackets(CreateTransfer(CreatePayload(1800)));
            var second = GetPackets(CreateTransfer(CreatePayload(1800)));
            object reassembler = Activator.CreateInstance(reassemblerType);

            Assert.That(Accept(
                reassembler,
                Decode(Encode(first[0])),
                out _,
                out _), Is.EqualTo("Accepted"));
            string result = Accept(
                reassembler,
                Decode(Encode(second[1])),
                out _,
                out string error);
            Assert.That(result, Is.EqualTo("Rejected"));
            Assert.That(error, Does.Contain("different transfer"));
        }

        [Test]
        public void EveryMaximumChunkPacketStaysBelowOneThousandBytes()
        {
            int maximumPayload = GetConstant<int>("MaximumPayloadBytes");
            int maximumPacket = GetConstant<int>(
                "MaximumEncodedPacketBytes");
            Assert.That(maximumPayload, Is.GreaterThanOrEqualTo(256 * 1024));
            Assert.That(maximumPacket, Is.LessThan(1000));
            Assert.That(maximumPacket, Is.LessThan(1264));

            var packets = GetPackets(CreateTransfer(
                CreatePayload(maximumPayload)));
            int observedMaximum = packets.Max(packet => Encode(packet).Length);
            Assert.That(observedMaximum, Is.EqualTo(maximumPacket));
            Assert.That(observedMaximum + sizeof(ushort), Is.LessThan(1000));
        }

        [Test]
        public void ChunkAndTransferAcksRoundTripAndCloseTracker()
        {
            object transfer = CreateTransfer(CreatePayload(1700));
            var packets = GetPackets(transfer);
            object tracker = Activator.CreateInstance(
                ackTrackerType,
                transfer);

            object chunkAck = InvokeStatic(
                "CreateChunkAck",
                packets[0]);
            object decodedChunkAck = Decode(Encode(chunkAck));
            Assert.That(AcceptAck(
                tracker,
                decodedChunkAck,
                out string error), Is.EqualTo("Accepted"), error);
            Assert.That(GetBool(tracker, "IsComplete"), Is.False);

            object transferAck = InvokeStatic(
                "CreateTransferAck",
                transfer);
            object decodedTransferAck = Decode(Encode(transferAck));
            Assert.That(AcceptAck(
                tracker,
                decodedTransferAck,
                out error), Is.EqualTo("Completed"), error);
            Assert.That(GetBool(tracker, "IsComplete"), Is.True);
            int[] missing = (int[])ackTrackerType
                .GetMethod("GetMissingChunkIndices")
                .Invoke(tracker, null);
            Assert.That(missing, Is.Empty);
        }

        private object CreateTransfer(byte[] payload)
        {
            return InvokeStatic("CreateTransfer", DeckKind(), payload);
        }

        private object DeckKind()
        {
            return Enum.Parse(kindType, "Deck");
        }

        private List<object> GetPackets(object transfer)
        {
            Assert.That(transferType.IsInstanceOfType(transfer), Is.True);
            var source = (IEnumerable)transferType
                .GetProperty("Packets")
                .GetValue(transfer);
            return source.Cast<object>().ToList();
        }

        private byte[] Encode(object packet)
        {
            Assert.That(packetType.IsInstanceOfType(packet), Is.True);
            return (byte[])GetMethod(
                protocolType,
                "EncodePacket",
                1).Invoke(null, new[] { packet });
        }

        private object Decode(byte[] encoded)
        {
            object[] args = { encoded, null, null };
            bool success = (bool)GetMethod(
                protocolType,
                "TryDecodePacket",
                3).Invoke(null, args);
            Assert.That(success, Is.True, args[2] as string);
            Assert.That(packetType.IsInstanceOfType(args[1]), Is.True);
            return args[1];
        }

        private void AssertDecodeRejected(byte[] encoded, string message)
        {
            object[] args = { encoded, null, null };
            bool success = (bool)GetMethod(
                protocolType,
                "TryDecodePacket",
                3).Invoke(null, args);
            Assert.That(success, Is.False);
            Assert.That(args[1], Is.Null);
            Assert.That(args[2] as string, Does.Contain(message).IgnoreCase);
        }

        private string Accept(
            object reassembler,
            object packet,
            out byte[] payload,
            out string error)
        {
            object[] args = { packet, null, null };
            object result = GetMethod(
                reassemblerType,
                "Accept",
                3).Invoke(reassembler, args);
            payload = args[1] as byte[];
            error = args[2] as string;
            return result.ToString();
        }

        private string AcceptAck(
            object tracker,
            object packet,
            out string error)
        {
            object[] args = { packet, null };
            object result = GetMethod(
                ackTrackerType,
                "Accept",
                2).Invoke(tracker, args);
            error = args[1] as string;
            return result.ToString();
        }

        private object InvokeStatic(string name, params object[] arguments)
        {
            MethodInfo method = protocolType.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Single(candidate =>
                    candidate.Name == name &&
                    ParametersMatch(candidate, arguments));
            return method.Invoke(null, arguments);
        }

        private static bool ParametersMatch(
            MethodInfo method,
            object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != arguments.Length)
                return false;
            for (int index = 0; index < parameters.Length; index++)
            {
                if (arguments[index] != null &&
                    !parameters[index].ParameterType.IsInstanceOfType(
                        arguments[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static MethodInfo GetMethod(
            Type type,
            string name,
            int parameterCount)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                    BindingFlags.Static)
                .Single(method => method.Name == name &&
                    method.GetParameters().Length == parameterCount);
        }

        private T GetConstant<T>(string name)
        {
            return (T)protocolType.GetField(
                name,
                BindingFlags.Public | BindingFlags.Static).GetRawConstantValue();
        }

        private static bool GetBool(object instance, string property)
        {
            return (bool)instance.GetType().GetProperty(property)
                .GetValue(instance);
        }

        private static byte[] CreatePayload(int length)
        {
            var result = new byte[length];
            uint value = 0x9e3779b9u;
            for (int index = 0; index < result.Length; index++)
            {
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                result[index] = (byte)value;
            }
            return result;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type result = assembly.GetType(fullName, false);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
