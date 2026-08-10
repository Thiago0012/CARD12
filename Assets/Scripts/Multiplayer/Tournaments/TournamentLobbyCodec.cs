using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ArcaneArena.Multiplayer.Tournaments
{
    internal sealed class TournamentEncodedPayload
    {
        internal TournamentEncodedPayload(
            IReadOnlyList<string> chunks,
            string sha256)
        {
            Chunks = chunks ?? Array.Empty<string>();
            Sha256 = sha256 ?? string.Empty;
        }

        internal IReadOnlyList<string> Chunks { get; }
        internal string Sha256 { get; }
    }

    /// <summary>
    /// Codec compartilhado pelos snapshots do Lobby e pelos envelopes dos
    /// jogadores. O JSON nunca é enviado diretamente: gzip reduz os nomes
    /// repetidos dos modelos e o hash impede aplicar estado parcial/corrompido.
    /// </summary>
    internal static class TournamentLobbyCodec
    {
        internal const int ChunkCharacterLimit = 1800;
        internal const int MaximumLobbyChunks = 14;
        internal const int MaximumPlayerChunks = 5;

        internal static TournamentEncodedPayload Encode<T>(
            T value,
            int maximumChunks)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (maximumChunks < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumChunks));

            string json = JsonUtility.ToJson(value, false);
            byte[] compressed = Compress(Encoding.UTF8.GetBytes(json));
            string base64 = Convert.ToBase64String(compressed);
            var chunks = new List<string>();
            for (int offset = 0; offset < base64.Length;
                 offset += ChunkCharacterLimit)
            {
                int length = Math.Min(
                    ChunkCharacterLimit,
                    base64.Length - offset);
                chunks.Add(base64.Substring(offset, length));
            }
            if (chunks.Count > maximumChunks)
            {
                throw new InvalidOperationException(
                    $"Payload compactado excedeu {maximumChunks} blocos " +
                    $"({compressed.Length} bytes compactados)." );
            }
            return new TournamentEncodedPayload(
                chunks,
                ComputeSha256(compressed));
        }

        internal static bool TryDecode<T>(
            IReadOnlyList<string> chunks,
            string expectedSha256,
            out T value,
            out string error)
        {
            value = default;
            error = string.Empty;
            if (chunks == null || chunks.Count == 0)
            {
                error = "Snapshot sem blocos.";
                return false;
            }
            try
            {
                var builder = new StringBuilder(chunks.Count *
                    ChunkCharacterLimit);
                for (int index = 0; index < chunks.Count; index++)
                {
                    if (string.IsNullOrEmpty(chunks[index]))
                    {
                        error = $"Bloco {index + 1} ausente.";
                        return false;
                    }
                    builder.Append(chunks[index]);
                }
                byte[] compressed = Convert.FromBase64String(
                    builder.ToString());
                string actualSha256 = ComputeSha256(compressed);
                if (!string.Equals(
                        actualSha256,
                        expectedSha256 ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase))
                {
                    error = "Hash do snapshot não confere.";
                    return false;
                }
                string json = Encoding.UTF8.GetString(
                    Decompress(compressed));
                value = JsonUtility.FromJson<T>(json);
                if (value == null)
                {
                    error = "Snapshot vazio após desserialização.";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                value = default;
                return false;
            }
        }

        private static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(
                output,
                System.IO.Compression.CompressionLevel.Optimal,
                true))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private static byte[] Decompress(byte[] compressed)
        {
            using var input = new MemoryStream(compressed, false);
            using var gzip = new GZipStream(
                input,
                CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private static string ComputeSha256(byte[] data)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data ?? Array.Empty<byte>());
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
                builder.Append(value.ToString("x2"));
            return builder.ToString();
        }
    }
}
