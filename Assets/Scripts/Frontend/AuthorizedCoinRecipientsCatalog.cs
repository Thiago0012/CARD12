using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public enum AuthorizedRecipientStatus
    {
        Active,
        DisabledForNewBindings,
        Revoked
    }

    [Serializable]
    public sealed class AuthorizedRecipientEntry
    {
        [SerializeField, HideInInspector] private string entryId;
        [SerializeField] private string nickname;
        [SerializeField, HideInInspector] private string normalizedNickname;
        [SerializeField] private AuthorizedRecipientStatus status =
            AuthorizedRecipientStatus.Active;
        [SerializeField, TextArea] private string note;

        public string EntryId => entryId ?? string.Empty;
        public string Nickname => nickname ?? string.Empty;
        public string NormalizedNickname => normalizedNickname ?? string.Empty;
        public AuthorizedRecipientStatus Status => status;
        public string Note => note ?? string.Empty;

#if UNITY_EDITOR
        internal void ValidateForEditor(ISet<string> usedEntryIds)
        {
            if (string.IsNullOrWhiteSpace(entryId) ||
                !usedEntryIds.Add(entryId))
            {
                do
                {
                    entryId = Guid.NewGuid().ToString("N");
                } while (!usedEntryIds.Add(entryId));
            }

            normalizedNickname = NicknameNormalizer.Normalize(nickname);
        }
#endif
    }

    [CreateAssetMenu(
        fileName = "AuthorizedCoinRecipientsCatalog",
        menuName = "Master Duel 2 Plus Ultra/Economia/Destinatários de Moedas")]
    public sealed class AuthorizedCoinRecipientsCatalog : ScriptableObject
    {
        [SerializeField, Min(1)] private int catalogVersion = 1;
        [SerializeField] private List<AuthorizedRecipientEntry> entries = new();

        public int CatalogVersion => Math.Max(1, catalogVersion);
        public IReadOnlyList<AuthorizedRecipientEntry> Entries =>
            entries ?? (IReadOnlyList<AuthorizedRecipientEntry>)
                Array.Empty<AuthorizedRecipientEntry>();

        public bool TryFindActive(
            string nickname,
            out AuthorizedRecipientEntry match)
        {
            string key = NicknameNormalizer.Normalize(nickname);
            match = string.IsNullOrEmpty(key)
                ? null
                : Entries.FirstOrDefault(entry =>
                    entry != null &&
                    entry.Status == AuthorizedRecipientStatus.Active &&
                    string.Equals(
                        entry.NormalizedNickname,
                        key,
                        StringComparison.Ordinal));
            return match != null;
        }

        public AuthorizedRecipientEntry FindByEntryId(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return null;
            return Entries.FirstOrDefault(entry =>
                entry != null && string.Equals(
                    entry.EntryId,
                    entryId,
                    StringComparison.Ordinal));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            catalogVersion = Math.Max(1, catalogVersion);
            entries ??= new List<AuthorizedRecipientEntry>();
            var usedEntryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AuthorizedRecipientEntry entry in entries)
                entry?.ValidateForEditor(usedEntryIds);
        }
#endif
    }

    public static class NicknameNormalizer
    {
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string value = raw.Normalize(NormalizationForm.FormKC).Trim();
            var normalized = new StringBuilder(value.Length);
            bool pendingSpace = false;
            foreach (char character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = normalized.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    normalized.Append(' ');
                    pendingSpace = false;
                }
                normalized.Append(char.ToUpperInvariant(character));
            }
            return normalized.ToString();
        }
    }
}
