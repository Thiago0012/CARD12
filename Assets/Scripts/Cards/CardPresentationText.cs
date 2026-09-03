using System;
using ArcaneDuel.DuelEngine.Data;
using UnityEngine;

namespace ArcaneArena.Cards
{
    /// <summary>
    /// Centraliza a política de idioma dos textos exibidos ao jogador.
    /// Os scripts de duelo continuam usando os dados originais; esta classe
    /// protege apenas a apresentação para que uma tradução incompleta não
    /// misture inglês e português na mesma interface.
    /// </summary>
    public static class CardPresentationText
    {
        private const string TranslationReviewMessage =
            "A tradução em português do efeito desta carta está em revisão. " +
            "O texto em inglês foi ocultado para manter a interface em português.";

        private static readonly string[] EnglishMarkers =
        {
            " once per ", " you can ", " your opponent ",
            " this card ", " this turn ", " target 1 ",
            " special summon", " normal summon", " from your ",
            " to your hand", " on the field", " in your graveyard",
            " destroy that", " banish that", " when this ",
            " if this ", " during your "
        };

        private static readonly string[] EnglishVocabulary =
        {
            " the ", " this ", " that ", " card ", " you ",
            " your ", " opponent ", " monster ", " spell ",
            " trap ", " deck ", " hand ", " field ", " summon ",
            " turn ", " target ", " destroy ", " banish ",
            " once ", " when ", " during ", " cannot "
        };

        private static readonly object DatabaseSync = new object();
        private static CardDatabase portugueseDatabase;
        private static bool databaseLoadAttempted;

        public static string NamePtBr(
            string officialCardId,
            string fallback)
        {
            return TryResolvePortugueseCard(
                    officialCardId,
                    out CardRecord localized) &&
                   !string.IsNullOrWhiteSpace(localized.Name)
                ? localized.Name.Trim()
                : fallback ?? string.Empty;
        }

        public static string EffectPtBr(
            CardCatalogEntry entry,
            string emptyMessage = "Esta carta não possui texto de efeito.")
        {
            if (entry != null && TryResolvePortugueseCard(
                    entry.OfficialCardId,
                    out CardRecord localized) &&
                !string.IsNullOrWhiteSpace(localized.Description))
            {
                return ExplainPendulumSections(
                    LocalizeKnownHeadings(localized.Description.Trim()));
            }
            return EffectPtBr(entry?.EffectText, emptyMessage);
        }

        public static void InvalidateDatabaseCache()
        {
            lock (DatabaseSync)
            {
                portugueseDatabase = null;
                databaseLoadAttempted = false;
            }
        }

        public static string EffectPtBr(
            string value,
            string emptyMessage = "Esta carta não possui texto de efeito.")
        {
            if (string.IsNullOrWhiteSpace(value))
                return emptyMessage;

            string localized = LocalizeKnownHeadings(value.Trim());
            return LooksEnglish(localized)
                ? TranslationReviewMessage
                : ExplainPendulumSections(localized);
        }

        private static string LocalizeKnownHeadings(string value)
        {
            return value
                .Replace("[ Pendulum Effect ]", "[ Efeito de Pêndulo ]")
                .Replace("[Pendulum Effect]", "[Efeito de Pêndulo]")
                .Replace("[Efeito de Pêndulo]", "[ Efeito de Pêndulo ]")
                .Replace("[ Monster Effect ]", "[ Efeito de Monstro ]")
                .Replace("[Monster Effect]", "[Efeito de Monstro]")
                .Replace("[Efeito de Monstro]", "[ Efeito de Monstro ]")
                .Replace("[ Spell Effect ]", "[ Efeito de Magia ]")
                .Replace("[Spell Effect]", "[Efeito de Magia]")
                .Replace("[Efeito de Magia]", "[ Efeito de Magia ]")
                .Replace("[ Trap Effect ]", "[ Efeito de Armadilha ]")
                .Replace("[Trap Effect]", "[Efeito de Armadilha]")
                .Replace("[Efeito de Armadilha]", "[ Efeito de Armadilha ]");
        }

        private static string ExplainPendulumSections(string value)
        {
            const string heading = "[ Efeito de Pêndulo ]";
            const string compactHeading = "[Efeito de Pêndulo]";
            const string explanation =
                "(Como Card de Magia na Zona de Pêndulo)";
            if (value.Contains(explanation, StringComparison.Ordinal))
                return value;
            if (value.Contains(heading, StringComparison.Ordinal))
                return value.Replace(
                    heading,
                    heading + "\n" + explanation);
            if (value.Contains(compactHeading, StringComparison.Ordinal))
                return value.Replace(
                    compactHeading,
                    compactHeading + "\n" + explanation);
            return value;
        }

        private static bool LooksEnglish(string value)
        {
            string normalized = " " + value.ToLowerInvariant()
                .Replace('\r', ' ')
                .Replace('\n', ' ') + " ";

            foreach (string marker in EnglishMarkers)
            {
                if (normalized.Contains(marker, StringComparison.Ordinal))
                    return true;
            }

            int vocabularyMatches = 0;
            foreach (string word in EnglishVocabulary)
            {
                if (!normalized.Contains(word, StringComparison.Ordinal))
                    continue;
                vocabularyMatches++;
                if (vocabularyMatches >= 4)
                    return true;
            }
            return false;
        }

        private static bool TryResolvePortugueseCard(
            string officialCardId,
            out CardRecord localized)
        {
            localized = null;
            if (!uint.TryParse(officialCardId, out uint code) || code == 0)
                return false;

            lock (DatabaseSync)
            {
                if (!databaseLoadAttempted)
                {
                    databaseLoadAttempted = true;
                    try
                    {
                        portugueseDatabase = CardDatabase.LoadDefault();
                    }
                    catch (Exception exception)
                    {
                        portugueseDatabase = null;
                        Debug.LogWarning(
                            "Não foi possível carregar a localização " +
                            "portuguesa das cartas: " +
                            exception.GetBaseException().Message);
                    }
                }
                return portugueseDatabase != null &&
                    portugueseDatabase.TryGet(code, out localized) &&
                    localized != null;
            }
        }
    }
}
