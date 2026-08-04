using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ArcaneArena.Editor.AutoPacks
{
    public sealed class AutoPackPartitionResult
    {
        internal AutoPackPartitionResult(
            IEnumerable<IReadOnlyList<string>> packs,
            IEnumerable<string> pending)
        {
            Packs = (packs ?? Array.Empty<IReadOnlyList<string>>()).ToArray();
            Pending = (pending ?? Array.Empty<string>()).ToArray();
        }

        public IReadOnlyList<IReadOnlyList<string>> Packs { get; }
        public IReadOnlyList<string> Pending { get; }
        public IReadOnlyList<int> Sizes => Packs.Select(pack => pack.Count).ToArray();
    }

    public static class AutoPackPartitioner
    {
        public static AutoPackPartitionResult Partition(
            IReadOnlyList<string> orderedUniqueIds,
            int min = AutoPackGenerationSettings.RequiredMinimum,
            int max = AutoPackGenerationSettings.RequiredMaximum)
        {
            if (orderedUniqueIds == null)
                throw new ArgumentNullException(nameof(orderedUniqueIds));
            if (min <= 0 || max < min)
                throw new ArgumentOutOfRangeException(nameof(min));
            if (orderedUniqueIds.Any(string.IsNullOrWhiteSpace) ||
                orderedUniqueIds.Distinct(StringComparer.Ordinal).Count() !=
                orderedUniqueIds.Count)
            {
                throw new ArgumentException(
                    "A entrada deve conter somente IDs unicos e validos.",
                    nameof(orderedUniqueIds));
            }

            int count = orderedUniqueIds.Count;
            if (count < min)
                return new AutoPackPartitionResult(
                    Array.Empty<IReadOnlyList<string>>(),
                    orderedUniqueIds);

            int packCount = (count + max - 1) / max;
            var sizes = new List<int>();
            int assigned;
            if (count >= min * packCount)
            {
                int baseSize = count / packCount;
                int remainder = count % packCount;
                for (int index = 0; index < packCount; index++)
                    sizes.Add(baseSize + (index < remainder ? 1 : 0));
                assigned = count;
            }
            else
            {
                int fullPacks = count / max;
                for (int index = 0; index < fullPacks; index++)
                    sizes.Add(max);
                assigned = fullPacks * max;
            }

            var packs = new List<IReadOnlyList<string>>(sizes.Count);
            int offset = 0;
            foreach (int size in sizes)
            {
                packs.Add(orderedUniqueIds.Skip(offset).Take(size).ToArray());
                offset += size;
            }
            return new AutoPackPartitionResult(
                packs,
                orderedUniqueIds.Skip(assigned));
        }
    }

    internal static class AutoPackDeterminism
    {
        internal static string Sha256(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(
                Encoding.UTF8.GetBytes(value ?? string.Empty));
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        internal static IReadOnlyList<string> Shuffle(
            IEnumerable<string> ids,
            string seedMaterial)
        {
            var result = (ids ?? Array.Empty<string>())
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            byte[] seed = HexBytes(Sha256(seedMaterial));
            using var generator = new DeterministicByteGenerator(seed);
            for (int index = result.Length - 1; index > 0; index--)
            {
                int selected = generator.Next(index + 1);
                (result[index], result[selected]) =
                    (result[selected], result[index]);
            }
            return result;
        }

        private static byte[] HexBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
                bytes[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            return bytes;
        }

        private sealed class DeterministicByteGenerator : IDisposable
        {
            private readonly HMACSHA256 hmac;
            private ulong counter;
            private byte[] buffer = Array.Empty<byte>();
            private int offset;

            internal DeterministicByteGenerator(byte[] seed)
            {
                hmac = new HMACSHA256(seed ?? Array.Empty<byte>());
            }

            internal int Next(int exclusiveMaximum)
            {
                if (exclusiveMaximum <= 0)
                    throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
                uint limit = uint.MaxValue - (uint.MaxValue % (uint)exclusiveMaximum);
                uint value;
                do
                {
                    value = NextUInt32();
                }
                while (value >= limit);
                return (int)(value % (uint)exclusiveMaximum);
            }

            private uint NextUInt32()
            {
                if (offset + 4 > buffer.Length)
                    Refill();
                uint value = BitConverter.ToUInt32(buffer, offset);
                offset += 4;
                return value;
            }

            private void Refill()
            {
                counter++;
                buffer = hmac.ComputeHash(BitConverter.GetBytes(counter));
                offset = 0;
            }

            public void Dispose()
            {
                hmac.Dispose();
            }
        }
    }
}
