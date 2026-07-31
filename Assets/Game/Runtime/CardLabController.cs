using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneDuel.Game
{
    public sealed class CardLabController : MonoBehaviour
    {
        private enum CatalogFilter
        {
            Todos,
            Monstros,
            Magias,
            Armadilhas,
            ExtraDeck
        }

        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const int PageSize = 10;

        private CardDatabase database;
        private CardVisualCatalog visuals;
        private CardViewRegistry views;
        private List<CardRecord> allCards;
        private List<CardRecord> filteredCards;
        private DeckFile deck;
        private DeckLibraryFile library;
        private CardLabMode mode;
        private DeckValidationResult validation;
        private uint selectedCode;
        private string search = string.Empty;
        private CatalogFilter filter;
        private int page;
        private Vector2 deckScroll;
        private string status = "Carregando catálogo...";
        private Texture2D white;
        private Texture2D buttonNormal;
        private Texture2D buttonHover;
        private Texture2D buttonActive;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle tinyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle centeredStyle;

        private void Awake()
        {
            Application.runInBackground = true;
            white = Solid(Color.white);
            buttonNormal = Solid(new Color(0.025f, 0.12f, 0.18f, 0.98f));
            buttonHover = Solid(new Color(0.05f, 0.34f, 0.42f, 1f));
            buttonActive = Solid(new Color(0.38f, 0.20f, 0.50f, 1f));
            try
            {
                database = CardDatabase.LoadDefault();
                visuals = CardVisualCatalog.LoadDefault();
                views = new CardViewRegistry(visuals);
                allCards = database.Cards
                    .OrderBy(card => card.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(card => card.Code)
                    .ToList();
                library = DeckLibraryRepository.LoadOrCreate(out status);
                mode = (CardLabMode)Mathf.Clamp(
                    PlayerPrefs.GetInt(
                        CardLabNavigation.ModeKey,
                        (int)CardLabMode.Gallery),
                    (int)CardLabMode.Gallery,
                    (int)CardLabMode.Shop);
                string editingId = PlayerPrefs.GetString(
                    CardLabNavigation.EditingDeckKey,
                    library.activeDeckId);
                deck = library.Find(editingId) ??
                       library.Find(library.activeDeckId) ??
                       library.decks.FirstOrDefault();
                RefreshCatalog();
                ValidateDeck();
                selectedCode = allCards.Count > 0 ? allCards[0].Code : 0;
            }
            catch (Exception exception)
            {
                status = $"Falha ao abrir o laboratório: {exception.GetBaseException().Message}";
                Debug.LogException(exception);
            }
        }

        private void Update()
        {
            if (ArcaneInput.EscapePressedThisFrame)
            {
                if (mode != CardLabMode.Gallery)
                {
                    mode = CardLabMode.Gallery;
                    CardLabNavigation.Open(mode);
                }
                else
                {
                    SceneManager.LoadScene(ProjectIdentity.BootstrapScene);
                }
            }
        }

        private void OnDestroy()
        {
            views?.Dispose();
            if (white != null) Destroy(white);
            if (buttonNormal != null) Destroy(buttonNormal);
            if (buttonHover != null) Destroy(buttonHover);
            if (buttonActive != null) Destroy(buttonActive);
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.color = new Color(0.005f, 0.01f, 0.025f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), white);
            GUI.color = Color.white;

            float scale = Mathf.Min(
                Screen.width / DesignWidth,
                Screen.height / DesignHeight);
            float offsetX = (Screen.width - DesignWidth * scale) * 0.5f;
            float offsetY = (Screen.height - DesignHeight * scale) * 0.5f;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(offsetX, offsetY, 0f),
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            DrawBackdrop();
            DrawHeader();
            if (database == null || visuals == null || library == null)
            {
                GUI.Label(new Rect(180, 430, 1560, 130), status, titleStyle);
                GUI.matrix = previous;
                return;
            }
            switch (mode)
            {
                case CardLabMode.Gallery:
                    DrawGallery();
                    break;
                case CardLabMode.Shop:
                    DrawShop();
                    break;
                default:
                    DrawFilters();
                    DrawCatalog();
                    DrawDeckPanel();
                    DrawInspector();
                    break;
            }
            GUI.matrix = previous;
        }

        private void DrawBackdrop()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0.012f, 0.025f, 0.052f));
            Fill(new Rect(0, 0, DesignWidth, 7), new Color(0.12f, 0.92f, 1f));
            Fill(new Rect(0, 7, DesignWidth, 2), new Color(0.78f, 0.42f, 1f));
            Fill(new Rect(0, 92, DesignWidth, 1), new Color(0.13f, 0.48f, 0.58f, 0.7f));
        }

        private void DrawHeader()
        {
            string heading = mode switch
            {
                CardLabMode.Gallery => $"MEUS DECKS  {library?.decks?.Count ?? 0}",
                CardLabMode.Shop => "LOJA DE DECKS",
                _ => "EDITOR DE DECK"
            };
            GUI.Label(new Rect(32, 20, 620, 52), heading, titleStyle);
            GUI.Label(
                new Rect(565, 27, 620, 36),
                mode == CardLabMode.Editor
                    ? "200 CARTAS · CLIQUE PARA INSPECIONAR · + DECK PARA ADICIONAR"
                    : "COLEÇÃO LOCAL · DECKS SALVOS COM IDs ESTÁVEIS",
                subtitleStyle);
            if (GUI.Button(new Rect(1490, 20, 185, 52), "VOLTAR", buttonStyle))
            {
                if (mode == CardLabMode.Gallery)
                {
                    SceneManager.LoadScene(ProjectIdentity.BootstrapScene);
                }
                else
                {
                    mode = CardLabMode.Gallery;
                    CardLabNavigation.Open(mode);
                }
            }
            string action = mode switch
            {
                CardLabMode.Gallery => "LOJA",
                CardLabMode.Shop => "MEUS DECKS",
                _ => "SALVAR"
            };
            if (GUI.Button(new Rect(1690, 20, 198, 52), action, buttonStyle))
            {
                if (mode == CardLabMode.Gallery)
                {
                    mode = CardLabMode.Shop;
                    CardLabNavigation.Open(mode);
                }
                else if (mode == CardLabMode.Shop)
                {
                    mode = CardLabMode.Gallery;
                    CardLabNavigation.Open(mode);
                }
                else
                {
                    SaveDeck();
                }
            }
        }

        private void DrawGallery()
        {
            GUI.Label(
                new Rect(45, 120, 1200, 40),
                "Escolha um deck para editar ou ativar no próximo duelo.",
                bodyStyle);
            DrawCreateDeckTile(new Rect(70, 205, 385, 340));
            for (int index = 0; index < library.decks.Count; index++)
            {
                int slot = index + 1;
                int column = slot % 4;
                int row = slot / 4;
                DrawDeckGalleryTile(
                    library.decks[index],
                    new Rect(70 + column * 455, 205 + row * 390, 385, 340));
            }
            GUI.Label(
                new Rect(70, 994, 1300, 35),
                status,
                tinyStyle);
        }

        private void DrawCreateDeckTile(Rect rect)
        {
            Fill(rect, new Color(0.08f, 0.16f, 0.025f, 0.98f));
            Stroke(rect, new Color(0.68f, 1f, 0.04f), 2);
            GUIStyle plus = new GUIStyle(titleStyle)
            {
                fontSize = 82,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.68f, 1f, 0.04f) }
            };
            GUI.Label(new Rect(rect.x, rect.y + 55, rect.width, 130), "+", plus);
            GUI.Label(
                new Rect(rect.x + 25, rect.y + 220, rect.width - 50, 42),
                "CRIAR NOVO DECK",
                centeredStyle);
            if (GUI.Button(
                new Rect(rect.x + 45, rect.y + 278, rect.width - 90, 44),
                "COMEÇAR VAZIO",
                buttonStyle))
            {
                DeckFile created = DeckLibraryRepository.CreateDraft(library);
                OpenEditor(created);
            }
        }

        private void DrawDeckGalleryTile(DeckFile candidate, Rect rect)
        {
            bool active = string.Equals(
                library.activeDeckId,
                candidate.id,
                StringComparison.OrdinalIgnoreCase);
            DeckValidationResult result = DeckRules.Validate(
                candidate,
                database,
                visuals);
            Color accent = active
                ? new Color(0.68f, 1f, 0.04f)
                : result.IsValid
                    ? new Color(0.15f, 0.84f, 0.96f)
                    : new Color(1f, 0.24f, 0.42f);
            Fill(rect, new Color(0.02f, 0.075f, 0.105f, 0.99f));
            Stroke(rect, accent, active ? 4 : 2);
            if (active)
            {
                Fill(new Rect(rect.x, rect.y, rect.width, 34), accent);
                GUI.Label(
                    new Rect(rect.x + 12, rect.y + 2, rect.width - 24, 30),
                    "✓ ATIVO NO DUELO",
                    centeredStyle);
            }

            Rect caseRect = new Rect(rect.x + 125, rect.y + 52, 135, 160);
            Fill(caseRect, new Color(
                active ? 0.12f : 0.17f,
                active ? 0.24f : 0.12f,
                active ? 0.62f : 0.42f,
                1f));
            Stroke(caseRect, accent, 3);
            GUI.Label(
                new Rect(caseRect.x + 14, caseRect.y + 50, caseRect.width - 28, 54),
                "ARCANE\nDECK",
                centeredStyle);
            GUI.Label(
                new Rect(rect.x + 20, rect.y + 224, rect.width - 40, 36),
                candidate.name,
                centeredStyle);
            GUI.Label(
                new Rect(rect.x + 20, rect.y + 257, rect.width - 40, 28),
                $"{candidate.mainDeck.Count} PRINCIPAL · {candidate.extraDeck.Count} EXTRA",
                tinyStyle);
            if (GUI.Button(
                new Rect(rect.x + 24, rect.y + 294, 158, 32),
                "EDITAR",
                buttonStyle))
            {
                OpenEditor(candidate);
            }
            GUI.enabled = result.IsValid && !active;
            if (GUI.Button(
                new Rect(rect.x + 202, rect.y + 294, 158, 32),
                active ? "ATIVO" : "USAR",
                buttonStyle))
            {
                DeckLibraryRepository.TryActivate(
                    library,
                    candidate,
                    database,
                    visuals,
                    out status);
            }
            GUI.enabled = true;
        }

        private void DrawShop()
        {
            GUI.Label(
                new Rect(90, 124, 1300, 38),
                "Escolha um deck · todos são gratuitos nesta versão.",
                bodyStyle);
            DeckLibraryFile presets = DeckLibraryRepository.CreateDefaults();
            for (int index = 0; index < presets.decks.Count; index++)
            {
                DrawShopCard(
                    presets.decks[index],
                    new Rect(120 + index * 585, 210, 520, 690),
                    index);
            }
            GUI.Label(new Rect(120, 945, 1500, 50), status, bodyStyle);
        }

        private void DrawShopCard(DeckFile preset, Rect rect, int colorIndex)
        {
            Color[] accents =
            {
                new Color(0.12f, 0.88f, 1f),
                new Color(1f, 0.72f, 0.20f),
                new Color(1f, 0.18f, 0.42f)
            };
            Color accent = accents[Mathf.Clamp(colorIndex, 0, accents.Length - 1)];
            Fill(rect, new Color(
                accent.r * 0.12f,
                accent.g * 0.12f,
                accent.b * 0.12f,
                0.99f));
            Stroke(rect, accent, 2);
            GUI.Label(
                new Rect(rect.x + 30, rect.y + 25, 290, 30),
                preset.theme,
                tinyStyle);
            Fill(
                new Rect(rect.x + 365, rect.y + 18, 125, 42),
                new Color(0.68f, 1f, 0.04f));
            GUI.Label(
                new Rect(rect.x + 365, rect.y + 23, 125, 32),
                "GRÁTIS",
                centeredStyle);
            GUI.Label(
                new Rect(rect.x + 30, rect.y + 78, rect.width - 60, 45),
                preset.name,
                titleStyle);

            Rect cardRect = new Rect(rect.x + 145, rect.y + 145, 230, 320);
            if (views.TryGetTexture(preset.featuredCode, out Texture2D texture))
            {
                GUI.DrawTexture(cardRect, texture, ScaleMode.ScaleAndCrop);
            }
            Stroke(cardRect, accent, 2);
            GUI.Label(
                new Rect(rect.x + 45, rect.y + 492, rect.width - 90, 62),
                "Deck temático completo, pronto para edição e duelos locais.",
                bodyStyle);
            GUI.Label(
                new Rect(rect.x + 45, rect.y + 565, rect.width - 90, 30),
                $"{preset.mainDeck.Count} PRINCIPAL · {preset.extraDeck.Count} EXTRA",
                tinyStyle);
            if (GUI.Button(
                new Rect(rect.x + 45, rect.y + 615, rect.width - 90, 52),
                "USAR ESTE DECK",
                buttonStyle))
            {
                DeckFile existing = library.Find(preset.id);
                if (existing == null)
                {
                    existing = preset.Clone();
                    library.decks.Add(existing);
                }
                DeckLibraryRepository.TryActivate(
                    library,
                    existing,
                    database,
                    visuals,
                    out status);
            }
        }

        private void OpenEditor(DeckFile selected)
        {
            deck = selected;
            mode = CardLabMode.Editor;
            CardLabNavigation.Open(mode, selected.id);
            ValidateDeck();
            status = $"Editando {selected.name}.";
        }

        private void DrawFilters()
        {
            Panel(new Rect(26, 112, 1210, 98));
            GUI.Label(new Rect(45, 128, 100, 24), "BUSCAR", tinyStyle);
            string nextSearch = GUI.TextField(
                new Rect(44, 155, 390, 39),
                search,
                64);
            if (!string.Equals(nextSearch, search, StringComparison.Ordinal))
            {
                search = nextSearch;
                page = 0;
                RefreshCatalog();
            }

            CatalogFilter[] filters = (CatalogFilter[])Enum.GetValues(
                typeof(CatalogFilter));
            for (int index = 0; index < filters.Length; index++)
            {
                CatalogFilter candidate = filters[index];
                Color previous = GUI.backgroundColor;
                if (candidate == filter)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.84f, 0.92f);
                }
                if (GUI.Button(
                    new Rect(460 + index * 145, 145, 132, 48),
                    FilterName(candidate),
                    buttonStyle))
                {
                    filter = candidate;
                    page = 0;
                    RefreshCatalog();
                }
                GUI.backgroundColor = previous;
            }
        }

        private void DrawCatalog()
        {
            Panel(new Rect(26, 224, 1210, 660));
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(filteredCards.Count / (float)PageSize));
            page = Mathf.Clamp(page, 0, pageCount - 1);
            GUI.Label(
                new Rect(48, 239, 610, 31),
                $"CATÁLOGO · {filteredCards.Count} RESULTADOS · PÁGINA {page + 1}/{pageCount}",
                subtitleStyle);
            if (GUI.Button(new Rect(1008, 237, 82, 38), "◀", buttonStyle))
            {
                page = Mathf.Max(0, page - 1);
            }
            if (GUI.Button(new Rect(1100, 237, 82, 38), "▶", buttonStyle))
            {
                page = Mathf.Min(pageCount - 1, page + 1);
            }

            int first = page * PageSize;
            for (int local = 0; local < PageSize; local++)
            {
                int cardIndex = first + local;
                if (cardIndex >= filteredCards.Count) break;
                int column = local % 5;
                int row = local / 5;
                DrawCatalogCard(
                    filteredCards[cardIndex],
                    new Rect(48 + column * 234, 286 + row * 287, 214, 270));
            }
        }

        private void DrawCatalogCard(CardRecord card, Rect rect)
        {
            bool selected = selectedCode == card.Code;
            Fill(
                rect,
                selected
                    ? new Color(0.075f, 0.20f, 0.26f, 1f)
                    : new Color(0.025f, 0.075f, 0.11f, 0.98f));
            Stroke(
                rect,
                selected
                    ? new Color(1f, 0.74f, 0.22f)
                    : new Color(0.16f, 0.52f, 0.62f),
                selected ? 3 : 1);

            Rect artRect = new Rect(rect.x + 34, rect.y + 12, 146, 178);
            if (views.TryGetTexture(card.Code, out Texture2D texture))
            {
                GUI.DrawTexture(artRect, texture, ScaleMode.ScaleAndCrop);
            }
            else
            {
                Fill(artRect, new Color(0.18f, 0.20f, 0.25f));
            }
            if (GUI.Button(artRect, GUIContent.none, GUIStyle.none))
            {
                selectedCode = card.Code;
            }
            GUI.Label(
                new Rect(rect.x + 10, rect.y + 195, rect.width - 20, 38),
                card.Name,
                centeredStyle);
            GUI.Label(
                new Rect(rect.x + 10, rect.y + 231, 90, 24),
                $"ID {card.Code:00000000}",
                tinyStyle);
            if (GUI.Button(
                new Rect(rect.x + 118, rect.y + 229, 84, 30),
                "+ DECK",
                buttonStyle))
            {
                AddCard(card);
            }
        }

        private void DrawDeckPanel()
        {
            Rect panel = new Rect(1254, 112, 640, 943);
            Panel(panel);
            GUI.Label(new Rect(1280, 132, 590, 39), deck.name.ToUpperInvariant(), titleStyle);
            GUI.Label(
                new Rect(1280, 178, 590, 31),
                $"MAIN {deck.mainDeck.Count}/{DeckRules.MaximumMainDeck} · " +
                $"EXTRA {deck.extraDeck.Count}/{DeckRules.MaximumExtraDeck}",
                subtitleStyle);

            Color validationColor = validation != null && validation.IsValid
                ? new Color(0.18f, 0.95f, 0.60f)
                : new Color(1f, 0.45f, 0.30f);
            GUIStyle validationStyle = new GUIStyle(bodyStyle);
            validationStyle.normal.textColor = validationColor;
            GUI.Label(
                new Rect(1280, 214, 585, 58),
                validation?.Summary ?? status,
                validationStyle);

            Rect viewport = new Rect(1274, 284, 598, 566);
            List<KeyValuePair<uint, int>> main = Group(deck.mainDeck);
            List<KeyValuePair<uint, int>> extra = Group(deck.extraDeck);
            float contentHeight = 74 + (main.Count + extra.Count) * 37f;
            deckScroll = GUI.BeginScrollView(
                viewport,
                deckScroll,
                new Rect(0, 0, 568, Mathf.Max(viewport.height - 8, contentHeight)));
            float y = 2;
            GUI.Label(new Rect(4, y, 530, 28), "MAIN DECK", subtitleStyle);
            y += 34;
            DrawDeckRows(main, deck.mainDeck, ref y);
            y += 10;
            GUI.Label(new Rect(4, y, 530, 28), "EXTRA DECK", subtitleStyle);
            y += 34;
            DrawDeckRows(extra, deck.extraDeck, ref y);
            GUI.EndScrollView();

            if (GUI.Button(new Rect(1280, 873, 180, 53), "SALVAR DECK", buttonStyle))
            {
                SaveDeck();
            }
            if (GUI.Button(new Rect(1470, 873, 185, 53), "RESTAURAR", buttonStyle))
            {
                deck = DeckRepository.CreateStarterDeck();
                ValidateDeck();
                status = "Deck inicial restaurado.";
            }
            if (GUI.Button(new Rect(1665, 873, 198, 53), "INICIAR DUELO", buttonStyle))
            {
                StartDuel();
            }
            GUI.Label(new Rect(1280, 945, 580, 80), status, bodyStyle);
        }

        private void DrawDeckRows(
            List<KeyValuePair<uint, int>> grouped,
            List<uint> source,
            ref float y)
        {
            foreach (KeyValuePair<uint, int> item in grouped)
            {
                string name = database.TryGet(item.Key, out CardRecord card)
                    ? card.Name
                    : item.Key.ToString("00000000");
                GUI.Label(
                    new Rect(8, y + 5, 430, 27),
                    $"{item.Value}×  {name}",
                    bodyStyle);
                if (GUI.Button(new Rect(470, y, 54, 30), "−", buttonStyle))
                {
                    source.Remove(item.Key);
                    ValidateDeck();
                    status = $"{name}: uma cópia removida.";
                    return;
                }
                y += 37;
            }
            if (grouped.Count == 0)
            {
                GUI.Label(new Rect(8, y, 500, 28), "Nenhuma carta.", tinyStyle);
                y += 32;
            }
        }

        private void DrawInspector()
        {
            Panel(new Rect(26, 900, 1210, 155));
            if (selectedCode == 0 ||
                !database.TryGet(selectedCode, out CardRecord card) ||
                !visuals.TryGet(selectedCode, out CardVisualData visual))
            {
                GUI.Label(
                    new Rect(50, 935, 1140, 60),
                    "Selecione uma carta para inspecionar seus dados.",
                    bodyStyle);
                return;
            }
            GUI.Label(
                new Rect(48, 916, 650, 34),
                card.Name.ToUpperInvariant(),
                subtitleStyle);
            GUI.Label(
                new Rect(48, 953, 340, 28),
                $"ATK {card.Attack} · DEF {card.Defense} · NV {card.Level}",
                tinyStyle);
            GUI.Label(
                new Rect(405, 953, 300, 28),
                $"RISCO {visual.riskLevel} · {visual.frameStyle.ToUpperInvariant()}",
                tinyStyle);
            GUI.Label(
                new Rect(48, 985, 1140, 55),
                card.Description,
                bodyStyle);
        }

        private void AddCard(CardRecord card)
        {
            List<uint> destination = DeckRules.IsExtraDeck(card)
                ? deck.extraDeck
                : deck.mainDeck;
            int copies = deck.mainDeck.Count(code => CopyKey(code) == CopyKey(card.Code)) +
                         deck.extraDeck.Count(code => CopyKey(code) == CopyKey(card.Code));
            if (copies >= DeckRules.MaximumCopies)
            {
                status = $"{card.Name}: limite de três cópias atingido.";
                return;
            }
            int maximum = DeckRules.IsExtraDeck(card)
                ? DeckRules.MaximumExtraDeck
                : DeckRules.MaximumMainDeck;
            if (destination.Count >= maximum)
            {
                status = $"{(DeckRules.IsExtraDeck(card) ? "Extra" : "Main")} Deck está cheio.";
                return;
            }
            destination.Add(card.Code);
            selectedCode = card.Code;
            ValidateDeck();
            status = $"{card.Name} adicionada.";
        }

        private uint CopyKey(uint code)
        {
            CardRecord card = database.Get(code);
            return card.Alias != 0 ? card.Alias : card.Code;
        }

        private void SaveDeck()
        {
            ValidateDeck();
            try
            {
                DeckLibraryRepository.Save(library);
                if (validation.IsValid &&
                    string.Equals(
                        library.activeDeckId,
                        deck.id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    DeckRepository.SaveActive(deck, database, visuals);
                }
                status = validation.IsValid
                    ? "Deck salvo em JSON versionado."
                    : $"Rascunho salvo. {validation.Summary}";
            }
            catch (Exception exception)
            {
                status = $"Falha ao salvar: {exception.GetBaseException().Message}";
            }
        }

        private void StartDuel()
        {
            ValidateDeck();
            if (!validation.IsValid)
            {
                status = validation.Summary;
                return;
            }
            DeckLibraryRepository.TryActivate(
                library,
                deck,
                database,
                visuals,
                out status);
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
        }

        private void ValidateDeck()
        {
            validation = DeckRules.Validate(deck, database, visuals);
        }

        private void RefreshCatalog()
        {
            if (allCards == null) return;
            string query = search.Trim();
            filteredCards = allCards
                .Where(card =>
                    MatchesFilter(card) &&
                    (query.Length == 0 ||
                     card.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                     card.Code.ToString("00000000").Contains(query)))
                .ToList();
        }

        private bool MatchesFilter(CardRecord card)
        {
            const uint monster = 0x1;
            const uint spell = 0x2;
            const uint trap = 0x4;
            return filter switch
            {
                CatalogFilter.Monstros =>
                    (card.Type & monster) != 0 && !DeckRules.IsExtraDeck(card),
                CatalogFilter.Magias => (card.Type & spell) != 0,
                CatalogFilter.Armadilhas => (card.Type & trap) != 0,
                CatalogFilter.ExtraDeck => DeckRules.IsExtraDeck(card),
                _ => true
            };
        }

        private static List<KeyValuePair<uint, int>> Group(List<uint> cards)
        {
            return cards
                .GroupBy(code => code)
                .Select(group => new KeyValuePair<uint, int>(
                    group.Key,
                    group.Count()))
                .ToList();
        }

        private static string FilterName(CatalogFilter value)
        {
            return value switch
            {
                CatalogFilter.ExtraDeck => "EXTRA",
                CatalogFilter.Armadilhas => "ARMADILHAS",
                CatalogFilter.Monstros => "MONSTROS",
                CatalogFilter.Magias => "MAGIAS",
                _ => "TODAS"
            };
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = Style(28, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            subtitleStyle = Style(16, FontStyle.Bold, new Color(0.53f, 0.92f, 1f), TextAnchor.MiddleLeft);
            bodyStyle = Style(14, FontStyle.Normal, new Color(0.84f, 0.92f, 0.97f), TextAnchor.UpperLeft);
            bodyStyle.wordWrap = true;
            tinyStyle = Style(11, FontStyle.Bold, new Color(0.58f, 0.75f, 0.83f), TextAnchor.MiddleLeft);
            tinyStyle.wordWrap = true;
            centeredStyle = Style(12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            centeredStyle.wordWrap = true;
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
            buttonStyle.normal.background = buttonNormal;
            buttonStyle.hover.background = buttonHover;
            buttonStyle.active.background = buttonActive;
        }

        private static GUIStyle Style(
            int size,
            FontStyle fontStyle,
            Color color,
            TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                normal = { textColor = color }
            };
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void Panel(Rect rect)
        {
            Fill(rect, new Color(0.022f, 0.052f, 0.09f, 0.98f));
            Stroke(rect, new Color(0.13f, 0.48f, 0.58f, 0.68f), 1);
        }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }

        private void Stroke(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
