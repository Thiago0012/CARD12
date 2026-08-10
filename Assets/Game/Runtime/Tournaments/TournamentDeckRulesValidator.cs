using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneDuel.Game.Tournaments
{
    public sealed class TournamentDeckValidationResult
    {
        private readonly List<string> errors = new List<string>();

        public bool IsValid => errors.Count == 0;
        public IReadOnlyList<string> Errors => errors;
        public string Summary => IsValid
            ? "Deck validado para este torneio."
            : string.Join(" ", errors);

        internal void Add(string error)
        {
            if (!string.IsNullOrWhiteSpace(error) && !errors.Contains(error))
                errors.Add(error);
        }
    }

    public static class TournamentDeckRulesValidator
    {
        public static TournamentDeckValidationResult Validate(
            TournamentDeckManifest manifest,
            TournamentConfig config)
        {
            var result = new TournamentDeckValidationResult();
            if (manifest == null)
            {
                result.Add("Nenhum deck foi registrado.");
                return result;
            }
            if (config == null)
            {
                result.Add("As regras do torneio estão indisponíveis.");
                return result;
            }

            manifest.Normalize();
            config.Normalize();
            ValidateSectionSizes(manifest, result);

            var allCards = manifest.mainDeckCardIds
                .Concat(manifest.extraDeckCardIds)
                .Concat(manifest.sideDeckCardIds)
                .ToList();
            for (int index = 0; index < allCards.Count; index++)
            {
                if (string.IsNullOrEmpty(allCards[index]))
                    result.Add("O deck contém um identificador de carta inválido.");
            }

            Dictionary<string, int> copies = allCards
                .Where(cardId => !string.IsNullOrEmpty(cardId))
                .GroupBy(cardId => cardId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);

            if (config.allowedCardPoolMode ==
                TournamentCardPoolMode.SelectedCardsOnly)
            {
                var allowed = new HashSet<string>(
                    config.allowedCardIds,
                    StringComparer.Ordinal);
                foreach (string cardId in copies.Keys.OrderBy(
                             value => value,
                             StringComparer.Ordinal))
                {
                    if (!allowed.Contains(cardId))
                    {
                        result.Add(
                            $"A carta {cardId} não pertence ao pool permitido.");
                    }
                }
            }

            switch (config.banListMode)
            {
                case TournamentBanListMode.Standard:
                    ValidateStandardBanList(manifest, config, copies, result);
                    break;
                case TournamentBanListMode.Custom:
                    ValidateCustomBanList(config, copies, result);
                    break;
                case TournamentBanListMode.None:
                    ValidateGeneralCopyLimit(copies, result);
                    break;
                default:
                    result.Add("O modo de ban list não é reconhecido.");
                    break;
            }

            string expectedHash = DeckManifestHasher.ComputeSha256(
                manifest.banListId,
                manifest.mainDeckCardIds,
                manifest.extraDeckCardIds,
                manifest.sideDeckCardIds);
            if (string.IsNullOrWhiteSpace(manifest.sha256))
                manifest.sha256 = expectedHash;
            else if (!string.Equals(
                         manifest.sha256,
                         expectedHash,
                         StringComparison.OrdinalIgnoreCase))
            {
                result.Add(
                    "A assinatura do deck não corresponde às cartas recebidas.");
            }
            return result;
        }

        private static void ValidateSectionSizes(
            TournamentDeckManifest manifest,
            TournamentDeckValidationResult result)
        {
            int main = manifest.mainDeckCardIds.Count;
            if (main < DeckLegalityValidator.MinimumMain ||
                main > DeckLegalityValidator.MaximumMain)
            {
                result.Add(
                    $"O Main Deck deve ter {DeckLegalityValidator.MinimumMain}–" +
                    $"{DeckLegalityValidator.MaximumMain} cartas.");
            }
            if (manifest.extraDeckCardIds.Count >
                DeckLegalityValidator.MaximumExtra)
            {
                result.Add(
                    $"O Extra Deck deve ter no máximo " +
                    $"{DeckLegalityValidator.MaximumExtra} cartas.");
            }
            if (manifest.sideDeckCardIds.Count >
                DeckLegalityValidator.MaximumSide)
            {
                result.Add(
                    $"O Side Deck deve ter no máximo " +
                    $"{DeckLegalityValidator.MaximumSide} cartas.");
            }
        }

        private static void ValidateStandardBanList(
            TournamentDeckManifest manifest,
            TournamentConfig config,
            IReadOnlyDictionary<string, int> copies,
            TournamentDeckValidationResult result)
        {
            BanlistService service;
            try
            {
                service = BanlistService.Active;
            }
            catch (Exception exception)
            {
                result.Add(
                    "A ban list padrão não pôde ser carregada: " +
                    exception.GetBaseException().Message);
                return;
            }

            string requiredId = string.IsNullOrWhiteSpace(
                config.standardBanListId)
                ? service.Id
                : config.standardBanListId;
            if (!string.Equals(
                    requiredId,
                    service.Id,
                    StringComparison.Ordinal))
            {
                result.Add(
                    $"A ban list exigida ({requiredId}) não está disponível " +
                    $"nesta versão do jogo.");
                return;
            }
            if (!string.Equals(
                    manifest.banListId,
                    requiredId,
                    StringComparison.Ordinal))
            {
                result.Add(
                    $"O deck foi registrado na ban list " +
                    $"{manifest.banListId}, mas o torneio exige {requiredId}.");
            }

            foreach (KeyValuePair<string, int> copy in copies)
            {
                int maximum = service.MaximumCopies(copy.Key);
                if (copy.Value > maximum)
                {
                    result.Add(
                        $"A carta {copy.Key} usa {copy.Value} cópia(s); " +
                        $"o limite é {maximum}.");
                }
            }
        }

        private static void ValidateCustomBanList(
            TournamentConfig config,
            IReadOnlyDictionary<string, int> copies,
            TournamentDeckValidationResult result)
        {
            var limits = config.customBanList.ToDictionary(
                entry => entry.cardId,
                entry => entry.maximumCopies,
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> copy in copies)
            {
                int maximum = limits.TryGetValue(copy.Key, out int configured)
                    ? configured
                    : 3;
                if (copy.Value > maximum)
                {
                    string status = maximum switch
                    {
                        0 => "proibida",
                        1 => "limitada a 1",
                        2 => "limitada a 2",
                        _ => "limitada a 3"
                    };
                    result.Add(
                        $"A carta {copy.Key} está {status} e aparece " +
                        $"{copy.Value} vez(es).");
                }
            }
        }

        private static void ValidateGeneralCopyLimit(
            IReadOnlyDictionary<string, int> copies,
            TournamentDeckValidationResult result)
        {
            foreach (KeyValuePair<string, int> copy in copies)
            {
                if (copy.Value > DeckRules.MaximumCopies)
                {
                    result.Add(
                        $"A carta {copy.Key} excede o limite geral de " +
                        $"{DeckRules.MaximumCopies} cópias.");
                }
            }
        }
    }
}
