using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using UnityEngine;

namespace ArcaneDuel.Game
{
    [Serializable]
    public sealed class DeckFile
    {
        public int schemaVersion = 1;
        public string id = "deck-arcano";
        public string name = "Deck Arcano";
        public string theme = "ARCANO";
        public uint featuredCode;
        public List<uint> mainDeck = new List<uint>();
        public List<uint> extraDeck = new List<uint>();
        public List<uint> sideDeck = new List<uint>();

        public DeckFile Clone()
        {
            return new DeckFile
            {
                schemaVersion = schemaVersion,
                id = id,
                name = name,
                theme = theme,
                featuredCode = featuredCode,
                mainDeck = new List<uint>(mainDeck ?? new List<uint>()),
                extraDeck = new List<uint>(extraDeck ?? new List<uint>()),
                sideDeck = new List<uint>(sideDeck ?? new List<uint>())
            };
        }
    }

    public sealed class DeckValidationResult
    {
        private readonly List<string> errors = new List<string>();

        public bool IsValid => errors.Count == 0;
        public IReadOnlyList<string> Errors => errors;
        public string Summary => IsValid
            ? "Deck válido e pronto para duelo."
            : string.Join(" ", errors);

        internal void Add(string error)
        {
            errors.Add(error);
        }
    }

    public static class DeckRules
    {
        public const int MinimumMainDeck = 40;
        public const int MaximumMainDeck = 60;
        public const int MaximumExtraDeck = 15;
        public const int MaximumCopies = 3;

        private const uint Fusion = 0x40;
        private const uint Synchro = 0x2000;
        private const uint Xyz = 0x800000;
        private const uint Link = 0x4000000;

        public static bool IsExtraDeck(CardRecord card)
        {
            uint extraTypes = Fusion | Synchro | Xyz | Link;
            return card != null && (card.Type & extraTypes) != 0;
        }

        public static DeckValidationResult Validate(
            DeckFile deck,
            CardDatabase database,
            CardVisualCatalog visuals,
            BanlistService banlist = null)
        {
            banlist ??= BanlistService.Active;
            var result = new DeckValidationResult();
            if (deck == null)
            {
                result.Add("O arquivo de deck está vazio.");
                return result;
            }
            if (deck.schemaVersion != 1)
            {
                result.Add($"Versão de deck incompatível: {deck.schemaVersion}.");
            }

            deck.mainDeck ??= new List<uint>();
            deck.extraDeck ??= new List<uint>();
            deck.sideDeck ??= new List<uint>();
            if (deck.mainDeck.Count < MinimumMainDeck ||
                deck.mainDeck.Count > MaximumMainDeck)
            {
                result.Add(
                    $"O Main Deck precisa ter {MinimumMainDeck}–{MaximumMainDeck} cartas.");
            }
            if (deck.extraDeck.Count > MaximumExtraDeck)
            {
                result.Add(
                    $"O Extra Deck aceita no máximo {MaximumExtraDeck} cartas.");
            }
            if (deck.sideDeck.Count > DeckLegalityValidator.MaximumSide)
            {
                result.Add(
                    $"O Side Deck aceita no máximo {DeckLegalityValidator.MaximumSide} cartas.");
            }

            var copies = new Dictionary<uint, int>();
            ValidateSection(
                "Main Deck",
                deck.mainDeck,
                false,
                database,
                visuals,
                copies,
                result);
            ValidateSection(
                "Side Deck",
                deck.sideDeck,
                null,
                database,
                visuals,
                copies,
                result);
            ValidateSection(
                "Extra Deck",
                deck.extraDeck,
                true,
                database,
                visuals,
                copies,
                result);

            foreach (KeyValuePair<uint, int> copy in copies)
            {
                if (copy.Value > MaximumCopies)
                {
                    string name = database.TryGet(copy.Key, out CardRecord card)
                        ? card.Name
                        : copy.Key.ToString("00000000");
                    result.Add(
                        $"{name} excede o limite de {MaximumCopies} cópias.");
                }
            }

            DeckLegalityResult banlistResult = DeckLegalityValidator.Validate(
                deck.mainDeck.Select(code => code.ToString("00000000")),
                deck.extraDeck.Select(code => code.ToString("00000000")),
                deck.sideDeck.Select(code => code.ToString("00000000")),
                banlist);
            foreach (string error in banlistResult.Errors)
            {
                if (!result.Errors.Contains(error))
                    result.Add(error);
            }
            return result;
        }

        private static void ValidateSection(
            string section,
            IEnumerable<uint> cards,
            bool? expectExtra,
            CardDatabase database,
            CardVisualCatalog visuals,
            Dictionary<uint, int> copies,
            DeckValidationResult result)
        {
            int index = 0;
            foreach (uint code in cards)
            {
                index++;
                if (!database.TryGet(code, out CardRecord card))
                {
                    result.Add($"{section} #{index}: código {code:00000000} não existe.");
                    continue;
                }
                if (!visuals.TryGet(code, out CardVisualData visual))
                {
                    result.Add($"{card.Name} não possui apresentação completa.");
                    continue;
                }else if (visual.scriptStatus != "not_required_no_effect" &&
                         string.IsNullOrWhiteSpace(visual.scriptFile))
                {
                    result.Add($"{card.Name} não possui script de efeito resolvido.");
                }
                if (expectExtra.HasValue &&
                    IsExtraDeck(card) != expectExtra.Value)
                {
                    result.Add($"{card.Name} está na seção incorreta do deck.");
                }

                uint copyKey = card.Alias != 0 ? card.Alias : card.Code;
                copies.TryGetValue(copyKey, out int count);
                copies[copyKey] = count + 1;
            }
        }
    }

    public static class DeckRepository
    {
        public static string ActiveDeckPath => Path.Combine(
            Application.persistentDataPath,
            "Decks",
            "active-deck.json");

        public static DeckFile LoadActiveOrDefault(
            CardDatabase database,
            CardVisualCatalog visuals,
            out string status)
        {
            try
            {
                if (File.Exists(ActiveDeckPath))
                {
                    DeckFile saved = Load(ActiveDeckPath);
                    DeckValidationResult validation =
                        DeckRules.Validate(saved, database, visuals);
                    if (validation.IsValid)
                    {
                        status = $"Deck carregado: {saved.name}";
                        return saved;
                    }
                    status = "Deck salvo inválido; o deck inicial foi restaurado.";
                }
            }
            catch (Exception exception)
            {
                status =
                    $"Deck salvo não pôde ser lido ({exception.GetBaseException().Message}); " +
                    "o deck inicial foi restaurado.";
                return CreateStarterDeck();
            }

            status = "Deck inicial carregado.";
            return CreateStarterDeck();
        }

        public static DeckFile Load(string path)
        {
            DeckFile result = JsonUtility.FromJson<DeckFile>(
                File.ReadAllText(path));
            if (result == null)
            {
                throw new InvalidDataException("O arquivo de deck está vazio.");
            }
            result.mainDeck ??= new List<uint>();
            result.extraDeck ??= new List<uint>();
            result.sideDeck ??= new List<uint>();
            return result;
        }

        public static void Save(
            string path,
            DeckFile deck,
            CardDatabase database,
            CardVisualCatalog visuals)
        {
            DeckValidationResult validation =
                DeckRules.Validate(deck, database, visuals);
            if (!validation.IsValid)
            {
                throw new InvalidDataException(validation.Summary);
            }
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(
                path,
                JsonUtility.ToJson(deck, true) + Environment.NewLine);
        }

        public static void SaveActive(
            DeckFile deck,
            CardDatabase database,
            CardVisualCatalog visuals)
        {
            Save(ActiveDeckPath, deck, database, visuals);
        }

        public static DeckFile CreateStarterDeck()
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
                1784619, 1784619, 1784619,
                2863439, 2863439, 2863439,
                10202894
            };
            return new DeckFile
            {
                schemaVersion = 1,
                id = "deck-inicial",
                name = "Deck Arcano Inicial",
                theme = "ARENA",
                featuredCode = 89631139,
                mainDeck = main.ToList(),
                extraDeck = new List<uint> { 11901678, 11901678, 11901678 }
            };
        }
    }
}
