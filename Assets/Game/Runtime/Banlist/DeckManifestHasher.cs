using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ArcaneDuel.Game
{
    public static class DeckManifestHasher
    {
        public static string ComputeSha256(
            string banlistId,
            IEnumerable<string> main,
            IEnumerable<string> extra,
            IEnumerable<string> side)
        {
            string normalized = string.Join("\n", new[]
            {
                "banlist=" + (banlistId ?? string.Empty).Trim(),
                "main=" + NormalizeSection(main),
                "extra=" + NormalizeSection(extra),
                "side=" + NormalizeSection(side)
            });
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return BitConverter.ToString(hash).Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static string NormalizeSection(IEnumerable<string> cards)
        {
            return string.Join(",", (cards ?? Array.Empty<string>())
                .Select(BanlistService.NormalizePasscode)
                .OrderBy(card => card, StringComparer.Ordinal));
        }
    }
}
