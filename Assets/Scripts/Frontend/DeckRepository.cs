using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneArena.Cards;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Persistência local do construtor de decks.
    /// O estado usa IDs oficiais, nunca nomes de arquivo ou referências de cena,
    /// para que futuramente possa ser enviado a um servidor para validação.
    /// </summary>
    public sealed partial class DeckRepository
    {
        private const int CurrentSchemaVersion = 5;
        private const int MainDeckMinimum = 40;
        private const int MainDeckMaximum = 60;
        private const int ExtraDeckMaximum = 15;
        private const int CopyLimit = 3;
        public const int MinimumPlayerNameLength = 3;
        public const int MaximumPlayerNameLength = 18;
        private readonly string _savePath;
        private CardCatalog _catalog;

        public DeckCollectionState State { get; private set; }
        public string PlayerDisplayName =>
            State?.playerDisplayName?.Trim() ?? string.Empty;
        public bool HasPlayerProfile =>
            !string.IsNullOrWhiteSpace(PlayerDisplayName);
        public DeckRecord SelectedDeck =>
            State?.decks?.Find(deck =>
                deck != null &&
                string.Equals(
                    deck.deckId,
                    State.selectedDeckId,
                    StringComparison.Ordinal));

        public DeckRepository()
            : this(
                Path.Combine(
                    Application.persistentDataPath,
                    "ArcaneArena",
                    "decks.json"))
        {
        }

        public DeckRepository(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException(
                    "O caminho do save não pode ser vazio.",
                    nameof(savePath));
            }

            _savePath = Path.GetFullPath(savePath);
        }

        public void Load(
            CardCatalog catalog,
            bool persistNormalizedState = true)
        {
            _catalog = catalog;
            State = null;
            try
            {
                if (File.Exists(_savePath))
                    State = JsonUtility.FromJson<DeckCollectionState>(
                        File.ReadAllText(_savePath));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Não foi possível carregar os decks locais: {exception.Message}");
            }

            State ??= new DeckCollectionState();
            int loadedSchemaVersion = State.schemaVersion;
            State.schemaVersion = CurrentSchemaVersion;
            State.decks ??= new List<DeckRecord>();
            State.unlockedDeckProductIds ??= new List<string>();
            NormalizeEconomyState(loadedSchemaVersion);
            if (string.IsNullOrWhiteSpace(State.localProfileId))
                State.localProfileId = Guid.NewGuid().ToString("N");
            NormalizeCoinRewardAuthorizationState(loadedSchemaVersion);

            State.decks.RemoveAll(deck => deck == null);
            foreach (var deck in State.decks)
                deck.Normalize();

            if (State.decks.Count == 0)
                State.decks.Add(CreateStarterDeck(catalog));
            if (string.IsNullOrWhiteSpace(State.selectedDeckId) ||
                State.decks.All(deck =>
                    !string.Equals(
                        deck.deckId,
                        State.selectedDeckId,
                        StringComparison.Ordinal)))
            {
                State.selectedDeckId = State.decks[0].deckId;
            }

            if (persistNormalizedState)
                Save();
        }

        public bool TrySetPlayerDisplayName(
            string proposedName,
            out string rejection)
        {
            rejection = string.Empty;
            if (State == null)
            {
                rejection =
                    "O perfil local ainda não foi carregado.";
                return false;
            }

            if (!TryValidatePlayerDisplayName(
                    proposedName,
                    out var normalizedName,
                    out rejection))
            {
                return false;
            }

            State.playerDisplayName = normalizedName;
            TryBindConfiguredNickname();
            Save();
            return true;
        }

        public static bool TryValidatePlayerDisplayName(
            string proposedName,
            out string normalizedName,
            out string rejection)
        {
            normalizedName = string.Empty;
            rejection = string.Empty;

            if (string.IsNullOrWhiteSpace(proposedName))
            {
                rejection = "Escolha um nome de duelista.";
                return false;
            }

            var parts = proposedName.Trim().Split(
                (char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            normalizedName = string.Join(" ", parts);
            if (normalizedName.Length < MinimumPlayerNameLength ||
                normalizedName.Length > MaximumPlayerNameLength)
            {
                rejection =
                    $"Use de {MinimumPlayerNameLength} a {MaximumPlayerNameLength} caracteres.";
                return false;
            }

            foreach (var character in normalizedName)
            {
                if (char.IsLetterOrDigit(character) ||
                    character == ' ' ||
                    character == '-' ||
                    character == '_' ||
                    character == '.')
                {
                    continue;
                }

                rejection =
                    "Use apenas letras, números, espaços, hífen, sublinhado ou ponto.";
                return false;
            }

            return true;
        }

        public bool IsDeckProductUnlocked(string productId)
        {
            return State?.unlockedDeckProductIds != null &&
                   State.unlockedDeckProductIds.Contains(productId);
        }

        public bool TryUseFreeDeckProduct(
            string productId,
            out DeckRecord deck,
            out string rejection)
        {
            deck = null;
            rejection = string.Empty;
            if (State == null)
            {
                rejection =
                    "A coleção de decks ainda não foi carregada.";
                return false;
            }

            var product = DeckShopCatalog.Find(productId);
            if (product == null)
            {
                rejection =
                    "Esse produto não existe no catálogo da loja.";
                return false;
            }

            deck = State.decks.Find(candidate =>
                candidate != null &&
                string.Equals(
                    candidate.deckId,
                    product.DeckId,
                    StringComparison.Ordinal));
            if (deck == null)
            {
                deck = product.CreateDeckRecord();
            }

            if (!TryValidateForDuel(
                    deck,
                    _catalog,
                    out rejection))
            {
                deck = null;
                return false;
            }

            if (!State.decks.Contains(deck))
                State.decks.Add(deck);

            if (!State.unlockedDeckProductIds.Contains(
                    product.ProductId))
            {
                State.unlockedDeckProductIds.Add(
                    product.ProductId);
            }

            State.selectedDeckId = deck.deckId;
            Save();
            return true;
        }

        public DeckRecord CreateDeck(string displayName, int caseTheme)
        {
            var deck = new DeckRecord
            {
                deckId = Guid.NewGuid().ToString("N"),
                displayName = string.IsNullOrWhiteSpace(displayName)
                    ? $"Novo Deck {State.decks.Count + 1}"
                    : displayName.Trim(),
                caseTheme = Mathf.Max(0, caseTheme)
            };
            deck.Normalize();
            State.decks.Add(deck);
            if (string.IsNullOrWhiteSpace(State.selectedDeckId))
                State.selectedDeckId = deck.deckId;
            Save();
            return deck;
        }

        public bool IsSelected(DeckRecord deck)
        {
            return deck != null &&
                   State != null &&
                   string.Equals(
                       State.selectedDeckId,
                       deck.deckId,
                       StringComparison.Ordinal);
        }

        public bool TrySelectDeck(
            string deckId,
            out string rejection)
        {
            rejection = string.Empty;
            if (State == null)
            {
                rejection = "A coleção de decks ainda não foi carregada.";
                return false;
            }

            var deck = State.decks.Find(candidate =>
                candidate != null &&
                string.Equals(
                    candidate.deckId,
                    deckId,
                    StringComparison.Ordinal));
            if (deck == null)
            {
                rejection = "O deck solicitado não existe neste perfil.";
                return false;
            }

            if (!TryValidateForDuel(deck, _catalog, out rejection))
                return false;
            if (!TryValidateOwnership(deck, out rejection))
                return false;

            State.selectedDeckId = deck.deckId;
            Save();
            return true;
        }

        public bool TryCreateSelectedLoadout(
            out DuelDeckLoadout loadout,
            out string rejection)
        {
            loadout = null;
            var selected = SelectedDeck;
            if (selected == null)
            {
                rejection =
                    "Nenhum deck foi selecionado para este perfil.";
                return false;
            }

            if (!TryValidateForDuel(
                    selected,
                    _catalog,
                    out rejection))
            {
                return false;
            }
            if (!TryValidateOwnership(selected, out rejection))
                return false;

            loadout = DuelDeckLoadout.Create(
                State.localProfileId,
                selected,
                PlayerDisplayName);
            return loadout != null;
        }

        public static bool TryValidateForDuel(
            DeckRecord deck,
            CardCatalog catalog,
            out string rejection)
        {
            rejection = string.Empty;
            if (deck == null)
            {
                rejection = "Deck inexistente.";
                return false;
            }

            deck.Normalize();
            if (deck.mainDeckCardIds.Count < MainDeckMinimum ||
                deck.mainDeckCardIds.Count > MainDeckMaximum)
            {
                rejection =
                    $"O Deck Principal deve conter de {MainDeckMinimum} a {MainDeckMaximum} cards.";
                return false;
            }

            if (deck.extraDeckCardIds.Count > ExtraDeckMaximum)
            {
                rejection =
                    $"O Deck Adicional pode conter no máximo {ExtraDeckMaximum} cards.";
                return false;
            }

            var copies = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (var cardId in deck.mainDeckCardIds)
            {
                if (!TryValidateCardPlacement(
                        catalog,
                        cardId,
                        false,
                        out rejection))
                {
                    return false;
                }
                if (!TryCountCopy(cardId, copies, out rejection))
                    return false;
            }

            foreach (var cardId in deck.extraDeckCardIds)
            {
                if (!TryValidateCardPlacement(
                        catalog,
                        cardId,
                        true,
                        out rejection))
                {
                    return false;
                }
                if (!TryCountCopy(cardId, copies, out rejection))
                    return false;
            }

            return true;
        }

        private bool TryValidateOwnership(
            DeckRecord deck,
            out string rejection)
        {
            rejection = string.Empty;
            if (deck == null)
                return false;

            var copies = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (var cardId in deck.mainDeckCardIds.Concat(
                         deck.extraDeckCardIds))
            {
                var normalized =
                    NormalizeNumericCardId(cardId);
                copies.TryGetValue(normalized, out var count);
                copies[normalized] = count + 1;
            }

            foreach (var pair in copies)
            {
                var owned = DeckShopCatalog.OwnedCopies(
                    State,
                    pair.Key);
                if (pair.Value <= owned)
                    continue;

                var entry = ResolveCard(_catalog, pair.Key);
                var name = entry != null
                    ? entry.DisplayName
                    : pair.Key;
                rejection =
                    owned == 0
                        ? $"{name} ainda não foi adquirido na Loja de Decks."
                        : $"O perfil possui {owned} cópia(s) de {name}, mas o deck usa {pair.Value}.";
                return false;
            }

            return true;
        }

        public void Save()
        {
            if (State == null)
                return;

            foreach (var deck in State.decks)
                deck?.Normalize();

            var directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            SaveAtomically(JsonUtility.ToJson(State, true));
        }

        private void SaveAtomically(string json)
        {
            string directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = _savePath + ".tmp";
            string backupPath = _savePath + ".bak";
            File.WriteAllText(temporaryPath, json ?? string.Empty);
            if (File.Exists(_savePath))
            {
                try
                {
                    File.Replace(temporaryPath, _savePath, backupPath, true);
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // Alguns sistemas de arquivos Android não expõem Replace.
                }
                catch (IOException)
                {
                    // Usa a substituição portátil abaixo.
                }

                File.Copy(temporaryPath, _savePath, true);
                File.Delete(temporaryPath);
                return;
            }

            File.Move(temporaryPath, _savePath);
        }

        public static string StableCardId(CardCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;
            return !string.IsNullOrWhiteSpace(entry.OfficialCardId)
                ? entry.OfficialCardId
                : $"asset:{entry.StableId}";
        }

        public static CardCatalogEntry ResolveCard(
            CardCatalog catalog,
            string stableCardId)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(stableCardId))
                return null;
            if (stableCardId.StartsWith(
                    "asset:",
                    StringComparison.Ordinal))
            {
                return catalog.FindByStableId(
                    stableCardId.Substring("asset:".Length));
            }

            var officialEntry =
                catalog.FindByOfficialId(stableCardId);
            if (officialEntry != null)
                return officialEntry;

            // Migração para decks salvos antes da padronização em oito
            // dígitos. Ex.: 5318639.jpg representa o ID oficial 05318639.
            var normalizedRequestedId =
                NormalizeNumericCardId(stableCardId);
            if (string.IsNullOrWhiteSpace(normalizedRequestedId))
                return null;

            // Official card codes are commonly exported without their
            // leading zero (for example 4335645 instead of 04335645).
            // CardCatalog stores the canonical eight-digit representation,
            // so normalize before falling back to legacy artwork names.
            if (uint.TryParse(
                    normalizedRequestedId,
                    out uint numericOfficialId))
            {
                CardCatalogEntry paddedOfficialEntry =
                    catalog.FindByOfficialId(
                        numericOfficialId.ToString("00000000"));
                if (paddedOfficialEntry != null)
                    return paddedOfficialEntry;
            }

            foreach (var entry in catalog.Entries)
            {
                if (entry?.Artwork == null)
                    continue;
                if (string.Equals(
                        NormalizeNumericCardId(
                            entry.Artwork.name),
                        normalizedRequestedId,
                        StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        public static bool BelongsToExtraDeck(CardCatalogEntry entry)
        {
            if (entry == null || entry.Category != CardCategory.Monster)
                return false;

            return
                entry.MonsterFrame == MonsterFrameKind.Fusion ||
                entry.MonsterFrame == MonsterFrameKind.Synchro ||
                entry.MonsterFrame == MonsterFrameKind.Xyz ||
                entry.MonsterFrame == MonsterFrameKind.Link;
        }

        private static bool TryValidateCardPlacement(
            CardCatalog catalog,
            string cardId,
            bool expectedInExtraDeck,
            out string rejection)
        {
            rejection = string.Empty;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                rejection = "O deck contém um ID de card vazio.";
                return false;
            }

            if (catalog == null)
                return true;

            var entry = ResolveCard(catalog, cardId);
            if (entry == null)
            {
                rejection =
                    $"O card {cardId} não foi encontrado no CardCatalog. Verifique se a imagem ainda existe em Assets/Cards.";
                return false;
            }

            if (entry.Artwork == null)
            {
                rejection =
                    $"A entrada do card {cardId} existe, mas está sem imagem vinculada.";
                return false;
            }

            if (!entry.IsReadyForGameplay)
            {
                rejection =
                    $"O card {cardId} ainda exige identificação manual.";
                return false;
            }

            var runtime =
                FrontendCardRuntimeCompatibility.ProfileFor(
                    entry);
            if (!runtime.CanEnterDuel)
            {
                rejection =
                    $"{entry.DisplayName} não possui um handler de runtime registrado no Core. {runtime.Note}";
                return false;
            }

            if (BelongsToExtraDeck(entry) != expectedInExtraDeck)
            {
                rejection = expectedInExtraDeck
                    ? $"{entry.DisplayName} não pertence ao Deck Adicional."
                    : $"{entry.DisplayName} deve ficar no Deck Adicional.";
                return false;
            }

            return true;
        }

        private static bool TryCountCopy(
            string cardId,
            IDictionary<string, int> copies,
            out string rejection)
        {
            rejection = string.Empty;
            copies.TryGetValue(cardId, out var count);
            count++;
            copies[cardId] = count;
            if (count <= CopyLimit)
                return true;

            rejection =
                $"O card {cardId} excede o limite geral de {CopyLimit} cópias.";
            return false;
        }

        private static string NormalizeNumericCardId(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var trimmed = value.Trim();
            for (var index = 0;
                 index < trimmed.Length;
                 index++)
            {
                if (!char.IsDigit(trimmed[index]))
                    return string.Empty;
            }

            var normalized = trimmed.TrimStart('0');
            return normalized.Length == 0 ? "0" : normalized;
        }

        private static DeckRecord CreateStarterDeck(CardCatalog catalog)
        {
            var deck = new DeckRecord
            {
                deckId = Guid.NewGuid().ToString("N"),
                displayName = "Deck Inicial",
                caseTheme = 0
            };

            if (catalog != null)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (entry == null ||
                        !entry.IsReadyForGameplay ||
                        entry.Artwork == null ||
                        !FrontendCardRuntimeCompatibility
                            .CanEnterDuel(entry))
                    {
                        continue;
                    }

                    var id = StableCardId(entry);
                    if (BelongsToExtraDeck(entry))
                    {
                        if (deck.extraDeckCardIds.Count < 15)
                            deck.extraDeckCardIds.Add(id);
                    }
                    else if (deck.mainDeckCardIds.Count < 40)
                    {
                        deck.mainDeckCardIds.Add(id);
                    }
                }
            }

            deck.Normalize();
            return deck;
        }
    }
}
