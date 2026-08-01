using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public enum CardLabMode
    {
        Gallery = 0,
        Editor = 1,
        Shop = 2
    }

    public static class CardLabNavigation
    {
        public const string ModeKey = "ArcaneCardLabMode";
        public const string EditingDeckKey = "ArcaneEditingDeckId";
        public const string OpponentDeckKey = "ArcaneOpponentDeckId";

        public static void Open(CardLabMode mode, string deckId = "")
        {
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            if (!string.IsNullOrWhiteSpace(deckId))
            {
                PlayerPrefs.SetString(EditingDeckKey, deckId);
            }
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    public sealed class DeckLibraryFile
    {
        public int schemaVersion = 1;
        public string activeDeckId = "deck-dragao-branco";
        public List<DeckFile> decks = new List<DeckFile>();

        public DeckFile Find(string deckId)
        {
            return decks?.FirstOrDefault(
                deck => string.Equals(
                    deck.id,
                    deckId,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class DeckLibraryRepository
    {
        private static readonly uint[] MainPool =
        {
            89631139, 46986414, 74677422, 74131780, 71413901,
            7089711, 93920745, 97268402, 77585513, 53129443,
            5318639, 44095762, 1784619, 2863439, 10202894
        };

        public static string LibraryPath => Path.Combine(
            Application.persistentDataPath,
            "Decks",
            "deck-library-v1.json");

        public static DeckLibraryFile LoadOrCreate(out string status)
        {
            try
            {
                if (File.Exists(LibraryPath))
                {
                    DeckLibraryFile saved = JsonUtility.FromJson<DeckLibraryFile>(
                        File.ReadAllText(LibraryPath));
                    Normalize(saved);
                    if (saved.decks.Count > 0)
                    {
                        status = $"{saved.decks.Count} decks carregados.";
                        return saved;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Biblioteca de decks restaurada: {exception.GetBaseException().Message}");
            }

            DeckLibraryFile created = CreateDefaults();
            Save(created);
            status = "Biblioteca inicial criada com três decks gratuitos.";
            return created;
        }

        public static void Save(DeckLibraryFile library)
        {
            Normalize(library);
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            string temporary = LibraryPath + ".tmp";
            File.WriteAllText(
                temporary,
                JsonUtility.ToJson(library, true) + Environment.NewLine);
            if (File.Exists(LibraryPath))
            {
                File.Replace(temporary, LibraryPath, LibraryPath + ".bak", true);
            }
            else
            {
                File.Move(temporary, LibraryPath);
            }
        }

        public static DeckLibraryFile CreateDefaults()
        {
            var library = new DeckLibraryFile
            {
                activeDeckId = "deck-dragao-branco",
                decks = new List<DeckFile>
                {
                    CreateCuratedPreset(
                        "deck-dragao-branco",
                        "Deck Dragão Branco - Blue-Eyes Max",
                        "OLHOS AZUIS",
                        89631139,
                        CuratedDeckLists.BlueEyesMaxModifiedMain,
                        CuratedDeckLists.BlueEyesMaxModifiedExtra),
                    CreateCuratedPreset(
                        "deck-mago-negro",
                        "Deck Mago Negro - Explosão Mágica Negra",
                        "MAGIA NEGRA",
                        46986414,
                        CuratedDeckLists.DarkMagicalBlastMain,
                        CuratedDeckLists.DarkMagicalBlastExtra),
                    CreatePreset(
                        "deck-dragao-negro",
                        "Deck Dragão Negro",
                        "OLHOS VERMELHOS",
                        74677422,
                        2)
                }
            };
            return library;
        }

        public static DeckFile CreateDraft(DeckLibraryFile library)
        {
            int number = 1;
            string id;
            do
            {
                id = $"novo-deck-{number}";
                number++;
            }
            while (library.Find(id) != null);

            var draft = new DeckFile
            {
                schemaVersion = 1,
                id = id,
                name = $"Novo Deck {number - 1}",
                theme = "PERSONALIZADO",
                featuredCode = 0,
                mainDeck = new List<uint>(),
                extraDeck = new List<uint>()
            };
            library.decks.Add(draft);
            Save(library);
            return draft;
        }

        public static bool TryActivate(
            DeckLibraryFile library,
            DeckFile deck,
            CardDatabase database,
            CardVisualCatalog visuals,
            out string status)
        {
            DeckValidationResult validation = DeckRules.Validate(
                deck,
                database,
                visuals);
            if (!validation.IsValid)
            {
                status = validation.Summary;
                return false;
            }

            library.activeDeckId = deck.id;
            Save(library);
            DeckRepository.SaveActive(deck, database, visuals);
            status = $"{deck.name} está ativo no duelo.";
            return true;
        }

        public static DeckFile FindOpponentOrDefault(
            DeckLibraryFile library,
            string deckId)
        {
            DeckFile selected = library?.Find(deckId);
            return selected?.Clone() ??
                   library?.Find(library.activeDeckId)?.Clone() ??
                   DeckRepository.CreateStarterDeck();
        }

        private static DeckFile CreatePreset(
            string id,
            string name,
            string theme,
            uint featured,
            int rotation)
        {
            var ordered = new List<uint> { featured };
            for (int index = 0; index < MainPool.Length; index++)
            {
                uint code = MainPool[(index + rotation) % MainPool.Length];
                if (!ordered.Contains(code)) ordered.Add(code);
            }

            var main = new List<uint>(40);
            for (int index = 0; index < 13; index++)
            {
                main.Add(ordered[index]);
                main.Add(ordered[index]);
                main.Add(ordered[index]);
            }
            main.Add(ordered[13]);
            return new DeckFile
            {
                schemaVersion = 1,
                id = id,
                name = name,
                theme = theme,
                featuredCode = featured,
                mainDeck = main,
                extraDeck = new List<uint> { 11901678, 11901678, 11901678 }
            };
        }

        private static DeckFile CreateCuratedPreset(
            string id,
            string name,
            string theme,
            uint featured,
            IEnumerable<uint> mainDeck,
            IEnumerable<uint> extraDeck)
        {
            return new DeckFile
            {
                schemaVersion = 1,
                id = id,
                name = name,
                theme = theme,
                featuredCode = featured,
                mainDeck = new List<uint>(mainDeck),
                extraDeck = new List<uint>(extraDeck)
            };
        }

        private static void RefreshReplacementDecks(DeckLibraryFile library)
        {
            ReplacePreset(
                library,
                CreateCuratedPreset(
                    "deck-dragao-branco",
                    "Deck Dragão Branco - Blue-Eyes Max",
                    "OLHOS AZUIS",
                    89631139,
                    CuratedDeckLists.BlueEyesMaxModifiedMain,
                    CuratedDeckLists.BlueEyesMaxModifiedExtra));
            ReplacePreset(
                library,
                CreateCuratedPreset(
                    "deck-mago-negro",
                    "Deck Mago Negro - Explosão Mágica Negra",
                    "MAGIA NEGRA",
                    46986414,
                    CuratedDeckLists.DarkMagicalBlastMain,
                    CuratedDeckLists.DarkMagicalBlastExtra));
        }

        private static void ReplacePreset(
            DeckLibraryFile library,
            DeckFile replacement)
        {
            int index = library.decks.FindIndex(
                deck => string.Equals(
                    deck.id,
                    replacement.id,
                    StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                library.decks[index] = replacement;
            }
            else
            {
                library.decks.Add(replacement);
            }
        }

        private static void Normalize(DeckLibraryFile library)
        {
            if (library == null)
            {
                throw new InvalidDataException("A biblioteca de decks está vazia.");
            }
            if (library.schemaVersion != 1)
            {
                throw new InvalidDataException(
                    $"Versão de biblioteca incompatível: {library.schemaVersion}.");
            }
            library.decks ??= new List<DeckFile>();
            RefreshReplacementDecks(library);
            foreach (DeckFile deck in library.decks)
            {
                deck.mainDeck ??= new List<uint>();
                deck.extraDeck ??= new List<uint>();
                if (string.IsNullOrWhiteSpace(deck.id))
                {
                    deck.id = $"deck-{Guid.NewGuid():N}";
                }
                if (string.IsNullOrWhiteSpace(deck.name)) deck.name = "Deck sem nome";
                if (string.IsNullOrWhiteSpace(deck.theme)) deck.theme = "PERSONALIZADO";
                if (deck.featuredCode == 0 && deck.mainDeck.Count > 0)
                {
                    deck.featuredCode = deck.mainDeck[0];
                }
            }
            if (library.Find(library.activeDeckId) == null)
            {
                library.activeDeckId = library.decks.FirstOrDefault()?.id ?? string.Empty;
            }
        }
    }
}
