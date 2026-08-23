#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ArcaneArena.Cards;
using ArcaneArena.Frontend;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor.CardAudit
{
    public static class CardInventoryAudit
    {
        public const string ReportDirectory =
            "Documentation/CardAudit";
        public const string MatrixJsonPath =
            ReportDirectory + "/CardHealthMatrix.json";
        public const string MatrixCsvPath =
            ReportDirectory + "/CardHealthMatrix.csv";
        public const string ReportPath =
            ReportDirectory + "/CardInventoryAuditReport.md";
        public const string CompatibilityReportPath =
            ReportDirectory + "/CardScriptCompatibilityReport.md";
        public const string FirstBatchPath =
            ReportDirectory + "/FirstBatchPlan.md";

        private const string CatalogPath =
            "Assets/Cards/CardCatalog.asset";
        private const string DocumentationCsvPath =
            "Documentation/CardCatalog.csv";
        private const string CoreDocumentationCsvPath =
            "Documentation/CoreCardCatalog.csv";
        private const string CardsBinPath =
            "Assets/StreamingAssets/Ygo/Data/cards.bin";
        private const string CardTextsPath =
            "Assets/StreamingAssets/Ygo/Data/card-texts.json";
        private const string VisualManifestPath =
            "Assets/StreamingAssets/Ygo/Visual/card-visuals.json";
        private const string OfficialScriptsPath =
            "Assets/StreamingAssets/Ygo/Scripts/official";
        private const string ScriptsPath =
            "Assets/StreamingAssets/Ygo/Scripts";
        private const string CustomScriptsPath =
            "Assets/StreamingAssets/Ygo/CustomScripts";
        private const string WindowsPluginPath =
            "Assets/Plugins/Windows/x86_64/ocgcore.dll";
        private const string AndroidPluginPath =
            "Assets/Plugins/Android/arm64-v8a/libocgcore.so";
        private const string StarterCatalogPath =
            "Assets/Resources/StarterDecks/StarterDeckCatalog.asset";
        private const string PackCatalogPath =
            "Assets/Resources/Shop/PackCatalog.json";

        private static readonly Regex LoadScriptPattern = new(
            @"Duel\s*\.\s*LoadScript\s*\(\s*[""'](?<name>[^""']+)[""']",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem("Tools/Game/Card Audit/Preview Inventory")]
        public static void PreviewInventory()
        {
            CardAuditSnapshot snapshot = BuildSnapshot();
            Debug.Log(BuildConsoleSummary(snapshot, "PREVIEW"));
        }

        [MenuItem("Tools/Game/Card Audit/Generate Phase 0-1 Reports")]
        public static void GenerateReports()
        {
            CardAuditSnapshot snapshot = BuildSnapshot();
            CardAuditReportWriter.WriteAll(snapshot);
            AssetDatabase.Refresh();
            Debug.Log(BuildConsoleSummary(snapshot, "GENERATED"));
        }

        [MenuItem("Tools/Game/Card Audit/Open Inventory Report")]
        public static void OpenInventoryReport()
        {
            string fullPath = FullPath(ReportPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning(
                    "Relatorio ainda nao existe. Execute Generate Phase 0-1 Reports.");
                return;
            }
            EditorUtility.RevealInFinder(fullPath);
        }

        public static void GenerateFromCommandLine()
        {
            GenerateReports();
            CardAuditSnapshot snapshot = LoadGeneratedSnapshot();
            if (snapshot.cards == null || snapshot.cards.Length == 0)
                throw new InvalidDataException("CardHealthMatrix foi gerada vazia.");
            Debug.Log(
                "ARCANE_CARD_AUDIT_OK cards=" + snapshot.cards.Length +
                " blocked=" + snapshot.statuses.bloqueadaDados +
                " catalog=" + snapshot.sources.cardCatalogUniqueOfficialIds +
                " database=" + snapshot.sources.compiledDatabaseCards);
        }

        public static CardAuditSnapshot LoadGeneratedSnapshot()
        {
            string path = FullPath(MatrixJsonPath);
            if (!File.Exists(path))
                throw new FileNotFoundException("CardHealthMatrix ausente.", path);
            CardAuditSnapshot snapshot = JsonUtility.FromJson<
                CardAuditSnapshot>(File.ReadAllText(path, Encoding.UTF8));
            return snapshot ?? throw new InvalidDataException(
                "CardHealthMatrix JSON invalida.");
        }

        public static CardAuditSnapshot BuildSnapshot()
        {
            string root = ProjectRoot();
            DateTime generated = DateTime.UtcNow;
            CardCatalog catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(
                CatalogPath);
            if (catalog == null)
                throw new FileNotFoundException("CardCatalog.asset ausente.", CatalogPath);

            CardCatalogEntry[] catalogEntries = catalog.Entries
                .Where(entry => entry != null)
                .ToArray();
            CsvDocument documentation = CsvDocument.Load(
                FullPath(DocumentationCsvPath), "official_code");
            CsvDocument coreDocumentation = CsvDocument.Load(
                FullPath(CoreDocumentationCsvPath), "official_code");
            CardDatabase database = CardDatabase.Load(
                FullPath(CardsBinPath),
                FullPath(CardTextsPath));
            CardRecord[] databaseCards = database.Cards
                .OrderBy(card => card.Code)
                .ToArray();
            TextCatalog textCatalog = JsonUtility.FromJson<TextCatalog>(
                File.ReadAllText(FullPath(CardTextsPath), Encoding.UTF8));
            if (textCatalog?.cards == null)
                throw new InvalidDataException("card-texts.json invalido.");
            HashSet<uint> textCodes = textCatalog.cards
                .Select(card => card.code)
                .ToHashSet();
            CardVisualCatalog visualCatalog = CardVisualCatalog.Load(
                FullPath(VisualManifestPath));
            Dictionary<uint, CardVisualData> visuals = visualCatalog.Cards
                .ToDictionary(card => card.officialCode);

            Memberships memberships = BuildMemberships();
            PackMemberships packMemberships = BuildPackMemberships();
            string[] invalidEntries = catalogEntries
                .Where(entry => !TryCode(entry.OfficialCardId, out _))
                .Select(entry => entry.StableId + "|" + entry.DisplayName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] duplicateCatalog = catalogEntries
                .Select(entry => Normalize(entry.OfficialCardId))
                .Where(value => !string.IsNullOrEmpty(value))
                .GroupBy(value => value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Dictionary<uint, CardRecord> databaseByCode = databaseCards
                .ToDictionary(card => card.Code);
            string[] generatedEvidence = ExistingGlobalEvidence();
            var health = new List<CardHealthEntry>(catalogEntries.Length);
            foreach (CardCatalogEntry entry in catalogEntries)
            {
                health.Add(BuildHealthEntry(
                    entry,
                    duplicateCatalog,
                    documentation,
                    coreDocumentation,
                    databaseByCode,
                    textCodes,
                    visuals,
                    memberships,
                    packMemberships,
                    generatedEvidence,
                    generated));
            }

            CardHealthEntry[] ordered = health
                .OrderBy(entry => entry.officialCardId, StringComparer.Ordinal)
                .ThenBy(entry => entry.stableId, StringComparer.Ordinal)
                .ToArray();
            HashSet<string> catalogIds = ordered
                .Select(entry => entry.officialCardId)
                .Where(value => !string.IsNullOrEmpty(value))
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> databaseIds = databaseCards
                .Select(card => card.Code.ToString("00000000"))
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> textIds = textCodes
                .Select(code => code.ToString("00000000"))
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> visualIds = visuals.Keys
                .Select(code => code.ToString("00000000"))
                .ToHashSet(StringComparer.Ordinal);

            CardAuditSnapshot snapshot = new()
            {
                generatedUtc = generated.ToString("O", CultureInfo.InvariantCulture),
                projectVersion = ProjectIdentity.ProjectVersion,
                unityVersion = ProjectIdentity.UnityVersion,
                coreApiVersion = ProjectIdentity.CoreApiVersion,
                coreCommit = ProjectIdentity.CoreCommit,
                cardScriptsCommit = ProjectIdentity.CardScriptsCommit,
                babelCdbCommit = ProjectIdentity.BabelCdbCommit,
                gitBranch = ReadGitBranch(root),
                gitHead = ReadGitHead(root),
                cards = ordered
            };
            snapshot.sources = BuildSources(
                catalogEntries,
                catalogIds,
                documentation,
                coreDocumentation,
                databaseCards,
                textCodes,
                visuals,
                memberships,
                packMemberships);
            snapshot.divergences = BuildDivergences(
                invalidEntries,
                duplicateCatalog,
                documentation,
                coreDocumentation,
                catalogIds,
                databaseIds,
                textIds,
                visualIds,
                ordered,
                memberships,
                packMemberships);
            snapshot.statuses = BuildStatusSummary(ordered);
            snapshot.firstBatch = BuildFirstBatch(ordered, memberships);
            return snapshot;
        }

        private static CardHealthEntry BuildHealthEntry(
            CardCatalogEntry entry,
            IReadOnlyCollection<string> duplicateCatalog,
            CsvDocument documentation,
            CsvDocument coreDocumentation,
            IReadOnlyDictionary<uint, CardRecord> database,
            IReadOnlyCollection<uint> textCodes,
            IReadOnlyDictionary<uint, CardVisualData> visuals,
            Memberships memberships,
            PackMemberships packs,
            string[] globalEvidence,
            DateTime generated)
        {
            string normalized = Normalize(entry.OfficialCardId);
            bool validCode = uint.TryParse(
                normalized,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out uint code) && code != 0;
            CardRecord record = null;
            bool inDatabase = validCode && database.TryGetValue(code, out record);
            bool inTexts = validCode && textCodes.Contains(code);
            CardVisualData visual = null;
            bool inVisuals = validCode && visuals.TryGetValue(code, out visual);
            bool scriptRequired = inDatabase &&
                DuelContentValidator.RequiresScript(record);
            ScriptResolution script = ResolveCardScript(code, scriptRequired);
            string assetArtwork = entry.AuthoredArtwork != null
                ? AssetDatabase.GetAssetPath(entry.AuthoredArtwork)
                : string.Empty;
            string streamingArtwork = inVisuals && visual != null
                ? CombineProjectPath("Assets/StreamingAssets/Ygo/Art", visual.artFile)
                : string.Empty;
            string artwork = File.Exists(FullPath(assetArtwork))
                ? assetArtwork
                : File.Exists(FullPath(streamingArtwork))
                    ? streamingArtwork
                    : string.Empty;
            string[] deckLabels = validCode
                ? memberships.For(code)
                : Array.Empty<string>();
            string[] packLabels = validCode
                ? packs.For(code)
                : Array.Empty<string>();
            bool onlineApplicable = inDatabase && IsOnlinePriority(record);
            string[] scenarios = ApplicableScenarios(
                scriptRequired,
                inDatabase ? record : null,
                deckLabels.Length > 0,
                onlineApplicable);
            string[] missingCoverage = MissingCoverage(
                scriptRequired,
                deckLabels.Length > 0,
                onlineApplicable);
            var blockers = new List<string>();
            if (!validCode) blockers.Add("OfficialCardId invalido ou vazio");
            if (!entry.OfficiallyRegistered) blockers.Add("registro oficial ausente");
            if (!entry.IsReadyForGameplay) blockers.Add("catalogo marcado para revisao");
            if (!string.IsNullOrEmpty(normalized) &&
                duplicateCatalog.Contains(normalized))
                blockers.Add("OfficialCardId duplicado no CardCatalog");
            if (!inDatabase) blockers.Add("dados compilados ausentes");
            if (!inTexts) blockers.Add("texto compilado ausente");
            if (!inVisuals) blockers.Add("manifesto visual ausente");
            if (string.IsNullOrEmpty(artwork)) blockers.Add("arte ausente");
            if (scriptRequired && !script.Found) blockers.Add("script obrigatorio ausente");
            if (scriptRequired && script.Empty) blockers.Add("script obrigatorio vazio");
            if (script.MissingDependencies.Length > 0)
                blockers.Add("dependencia Lua ausente");

            string status = blockers.Count > 0
                ? "BLOQUEADA_DADOS"
                : "CARREGA";
            string priority = blockers.Count > 0
                ? "P0"
                : deckLabels.Length > 0
                    ? "P1"
                    : onlineApplicable
                        ? "P3"
                        : scriptRequired || IsSpecialSummon(record)
                            ? "P2"
                            : "P5";
            string setcodes = inDatabase && record.Setcodes != null
                ? string.Join(
                    ";",
                    record.Setcodes.Select(value => "0x" +
                        value.ToString("X4", CultureInfo.InvariantCulture)))
                : string.Empty;

            return new CardHealthEntry
            {
                officialCardId = normalized,
                stableId = entry.StableId ?? string.Empty,
                name = entry.DisplayName ?? string.Empty,
                category = entry.Category.ToString(),
                monsterFrame = entry.MonsterFrame.ToString(),
                typeName = entry.TypeName ?? string.Empty,
                archetypeSetcodes = setcodes,
                aliasOfficialCardId = inDatabase && record.Alias != 0
                    ? record.Alias.ToString("00000000")
                    : string.Empty,
                decks = deckLabels,
                packs = packLabels,
                inCardCatalog = true,
                officiallyRegistered = entry.OfficiallyRegistered,
                readyForGameplay = entry.IsReadyForGameplay,
                inDocumentationCsv = documentation.Contains(normalized),
                inCoreDocumentationCsv = coreDocumentation.Contains(normalized),
                inCompiledDatabase = inDatabase,
                inTextDatabase = inTexts,
                inVisualManifest = inVisuals,
                artworkFound = !string.IsNullOrEmpty(artwork),
                artworkPath = artwork,
                artworkGuid = string.IsNullOrEmpty(assetArtwork)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(assetArtwork),
                scriptRequired = scriptRequired,
                scriptFound = script.Found,
                scriptSource = script.Source,
                scriptPath = script.Path,
                scriptSha256 = script.Hash,
                scriptCompatibility = script.Compatibility,
                missingScriptDependencies = script.MissingDependencies,
                applicableScenarios = scenarios,
                existingEvidence = globalEvidence,
                missingCoverage = missingCoverage,
                aiResult = deckLabels.Length > 0
                    ? "NAO_EXECUTADO_NESTE_LOTE"
                    : "NAO_APLICAVEL_NESTA_PRIORIZACAO",
                multiplayerResult = onlineApplicable
                    ? "NAO_EXECUTADO_NESTE_LOTE"
                    : "NAO_APLICAVEL_NESTA_PRIORIZACAO",
                status = status,
                priority = priority,
                failureCode = blockers.Count > 0 ? "F01" : string.Empty,
                responsibleLayer = blockers.Count > 0
                    ? "dados/importacao/catalogacao"
                    : string.Empty,
                blockingReason = string.Join("; ", blockers),
                risk = blockers.Count > 0
                    ? "ALTO"
                    : inVisuals && visual != null
                        ? visual.riskLevel ?? string.Empty
                        : "NAO_CLASSIFICADO",
                sourceVersion = ProjectIdentity.MultiplayerCompatibility,
                evidenceUpdatedUtc = generated.ToString(
                    "O", CultureInfo.InvariantCulture)
            };
        }

        private static CardAuditSourceSummary BuildSources(
            CardCatalogEntry[] catalogEntries,
            HashSet<string> catalogIds,
            CsvDocument documentation,
            CsvDocument coreDocumentation,
            CardRecord[] database,
            HashSet<uint> textCodes,
            Dictionary<uint, CardVisualData> visuals,
            Memberships memberships,
            PackMemberships packs)
        {
            return new CardAuditSourceSummary
            {
                cardCatalogEntries = catalogEntries.Length,
                cardCatalogUniqueOfficialIds = catalogIds.Count,
                documentationCsvRows = documentation.RowCount,
                documentationCsvUniqueIds = documentation.Ids.Count,
                coreDocumentationRows = coreDocumentation.RowCount,
                coreDocumentationUniqueIds = coreDocumentation.Ids.Count,
                compiledDatabaseCards = database.Length,
                textDatabaseCards = textCodes.Count,
                visualManifestCards = visuals.Count,
                officialScripts = CountFiles(OfficialScriptsPath, "*.lua"),
                customScripts = CountFiles(CustomScriptsPath, "*.lua"),
                streamingArtFiles = CountFiles(
                    "Assets/StreamingAssets/Ygo/Art", "*.jpg"),
                shopDeckProducts = memberships.ShopProductCount,
                starterDecks = memberships.StarterDeckCount,
                curatedDeckArrays = memberships.CuratedArrayCount,
                shopPacks = packs.PackCount,
                cardCatalogSha256 = HashFile(CatalogPath),
                documentationCsvSha256 = HashFile(DocumentationCsvPath),
                coreDocumentationSha256 = HashFile(CoreDocumentationCsvPath),
                cardsBinSha256 = HashFile(CardsBinPath),
                cardTextsSha256 = HashFile(CardTextsPath),
                visualManifestSha256 = HashFile(VisualManifestPath),
                officialScriptsTreeSha256 = HashTree(OfficialScriptsPath, "*.lua"),
                customScriptsTreeSha256 = HashTree(CustomScriptsPath, "*.lua"),
                windowsCorePluginSha256 = HashFile(WindowsPluginPath),
                androidCorePluginSha256 = HashFile(AndroidPluginPath)
            };
        }

        private static CardAuditDivergences BuildDivergences(
            string[] invalidEntries,
            string[] duplicateCatalog,
            CsvDocument documentation,
            CsvDocument coreDocumentation,
            HashSet<string> catalog,
            HashSet<string> database,
            HashSet<string> texts,
            HashSet<string> visuals,
            CardHealthEntry[] cards,
            Memberships memberships,
            PackMemberships packs)
        {
            return new CardAuditDivergences
            {
                duplicateCatalogIds = duplicateCatalog,
                duplicateDocumentationIds = documentation.DuplicateIds,
                invalidCatalogEntries = invalidEntries,
                catalogMissingFromDocumentation = Except(catalog, documentation.Ids),
                documentationMissingFromCatalog = Except(documentation.Ids, catalog),
                catalogMissingFromCoreDocumentation = Except(catalog, coreDocumentation.Ids),
                coreDocumentationMissingFromCatalog = Except(coreDocumentation.Ids, catalog),
                catalogMissingFromCompiledDatabase = Except(catalog, database),
                compiledDatabaseMissingFromCatalog = Except(database, catalog),
                catalogMissingFromTextDatabase = Except(catalog, texts),
                catalogMissingFromVisualManifest = Except(catalog, visuals),
                visualManifestMissingFromCatalog = Except(visuals, catalog),
                missingRequiredScripts = cards
                    .Where(card => card.scriptRequired && !card.scriptFound)
                    .Select(card => card.officialCardId)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToArray(),
                emptyRequiredScripts = cards
                    .Where(card => string.Equals(
                        card.scriptCompatibility,
                        "EMPTY",
                        StringComparison.Ordinal))
                    .Select(card => card.officialCardId)
                    .ToArray(),
                missingScriptDependencies = cards
                    .SelectMany(card => card.missingScriptDependencies.Select(
                        dependency => card.officialCardId + "|" + dependency))
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                missingArtwork = cards
                    .Where(card => !card.artworkFound)
                    .Select(card => card.officialCardId)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToArray(),
                deckCardsMissingFromCatalog = Except(memberships.CardIds, catalog),
                packCardsMissingFromCatalog = Except(packs.CardIds, catalog)
            };
        }

        private static CardAuditStatusSummary BuildStatusSummary(
            CardHealthEntry[] cards)
        {
            return new CardAuditStatusSummary
            {
                inventariada = Count(cards, "status", "INVENTARIADA"),
                bloqueadaDados = Count(cards, "status", "BLOQUEADA_DADOS"),
                carrega = Count(cards, "status", "CARREGA"),
                testeParcial = Count(cards, "status", "TESTE_PARCIAL"),
                passaCore = Count(cards, "status", "PASSA_CORE"),
                passaApresentacao = Count(cards, "status", "PASSA_APRESENTACAO"),
                passaIa = Count(cards, "status", "PASSA_IA"),
                passaOnline = Count(cards, "status", "PASSA_ONLINE"),
                concluida = Count(cards, "status", "CONCLUIDA"),
                priorityP0 = Count(cards, "priority", "P0"),
                priorityP1 = Count(cards, "priority", "P1"),
                priorityP2 = Count(cards, "priority", "P2"),
                priorityP3 = Count(cards, "priority", "P3"),
                priorityP4 = Count(cards, "priority", "P4"),
                priorityP5 = Count(cards, "priority", "P5")
            };
        }

        private static int Count(
            IEnumerable<CardHealthEntry> cards,
            string field,
            string expected)
        {
            return cards.Count(card => string.Equals(
                field == "status" ? card.status : card.priority,
                expected,
                StringComparison.Ordinal));
        }

        private static CardAuditBatchSeed[] BuildFirstBatch(
            CardHealthEntry[] cards,
            Memberships memberships)
        {
            var byId = cards
                .Where(card => !string.IsNullOrEmpty(card.officialCardId))
                .GroupBy(card => card.officialCardId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            var selected = new List<string>(40);
            string[] groups =
            {
                "shop:" + DeckShopCatalog.BlueEyesProductId,
                "shop:" + DeckShopCatalog.DarkMagicianProductId,
                "shop:" + DeckShopCatalog.RedEyesProductId,
                string.IsNullOrEmpty(memberships.FirstStarterId)
                    ? string.Empty
                    : "starter:" + memberships.FirstStarterId
            };
            foreach (string group in groups.Where(value =>
                         !string.IsNullOrEmpty(value)))
            {
                int targetCount = selected.Count + 10;
                AddBatchCards(selected, byId,
                    memberships.Group(group + ":Main"), 8);
                AddBatchCards(selected, byId,
                    memberships.Group(group + ":Extra"), 2);
                AddBatchCards(selected, byId, memberships.Group(group),
                    targetCount - selected.Count);
            }
            foreach (CardHealthEntry card in cards
                         .Where(card => card.priority == "P0" ||
                                        card.priority == "P1")
                         .OrderBy(card => card.priority, StringComparer.Ordinal)
                         .ThenBy(card => card.officialCardId,
                             StringComparer.Ordinal))
            {
                if (selected.Count >= 40)
                    break;
                if (!string.IsNullOrEmpty(card.officialCardId) &&
                    !selected.Contains(card.officialCardId))
                    selected.Add(card.officialCardId);
            }

            return selected.Take(40).Select((id, index) =>
            {
                CardHealthEntry card = byId[id];
                return new CardAuditBatchSeed
                {
                    order = index + 1,
                    officialCardId = id,
                    name = card.name,
                    decks = card.decks,
                    priority = card.priority,
                    status = card.status,
                    proposedScenarios = card.applicableScenarios,
                    rationale = FirstBatchRationale(card)
                };
            }).ToArray();
        }

        private static void AddBatchCards(
            List<string> selected,
            IReadOnlyDictionary<string, CardHealthEntry> byId,
            IEnumerable<uint> source,
            int maximumAdditions)
        {
            if (maximumAdditions <= 0)
                return;
            int added = 0;
            foreach (uint code in source ?? Array.Empty<uint>())
            {
                string id = code.ToString("00000000");
                if (!byId.ContainsKey(id) || selected.Contains(id))
                    continue;
                selected.Add(id);
                added++;
                if (added >= maximumAdditions)
                    return;
            }
        }

        private static string FirstBatchRationale(CardHealthEntry card)
        {
            string joined = string.Join(" ", card.decks ?? Array.Empty<string>());
            if (joined.Contains(DeckShopCatalog.BlueEyesProductId,
                    StringComparison.Ordinal))
                return "Deck publicado Blue-Eyes; prioridade de valor jogavel.";
            if (joined.Contains(DeckShopCatalog.DarkMagicianProductId,
                    StringComparison.Ordinal))
                return "Deck publicado Dark Magician; possui cobertura parcial existente.";
            if (joined.Contains(DeckShopCatalog.RedEyesProductId,
                    StringComparison.Ordinal))
                return "Deck publicado Red-Eyes; linha central recomendada pelo plano.";
            if (joined.IndexOf("starter:", StringComparison.Ordinal) >= 0)
                return "Carta de starter publicado; impacto direto no primeiro acesso.";
            return "Prioridade P0/P1 usada para completar o lote de 40 cartas.";
        }

        private static Memberships BuildMemberships()
        {
            var result = new Memberships();
            foreach (DeckShopProduct product in DeckShopCatalog.Products)
            {
                result.ShopProductCount++;
                string group = "shop:" + product.ProductId;
                result.AddGroup(group, product.MainDeckCardIds);
                result.AddGroup(group, product.ExtraDeckCardIds);
                result.AddGroup(group + ":Main", product.MainDeckCardIds);
                result.AddGroup(group + ":Extra", product.ExtraDeckCardIds);
                result.Add(product.MainDeckCardIds, group + ":Main");
                result.Add(product.ExtraDeckCardIds, group + ":Extra");
            }

            StarterDeckCatalog starter = AssetDatabase.LoadAssetAtPath<
                StarterDeckCatalog>(StarterCatalogPath);
            if (starter != null)
            {
                foreach (StarterDeckDefinition deck in starter.Decks
                             .Where(deck => deck != null))
                {
                    result.StarterDeckCount++;
                    if (string.IsNullOrEmpty(result.FirstStarterId) &&
                        deck.IsPublishable)
                        result.FirstStarterId = deck.Id;
                    string group = "starter:" + deck.Id;
                    result.AddGroup(group, deck.MainDeck);
                    result.AddGroup(group, deck.ExtraDeck);
                    result.AddGroup(group, deck.SideDeck);
                    result.AddGroup(group + ":Main", deck.MainDeck);
                    result.AddGroup(group + ":Extra", deck.ExtraDeck);
                    result.AddGroup(group + ":Side", deck.SideDeck);
                    result.Add(deck.MainDeck, group + ":Main");
                    result.Add(deck.ExtraDeck, group + ":Extra");
                    result.Add(deck.SideDeck, group + ":Side");
                }
            }

            foreach (FieldInfo field in typeof(CuratedDeckLists).GetFields(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(uint[]))
                    continue;
                uint[] values = field.GetValue(null) as uint[] ??
                    Array.Empty<uint>();
                result.CuratedArrayCount++;
                string section = field.Name.EndsWith("Extra",
                    StringComparison.Ordinal)
                    ? "Extra"
                    : field.Name.EndsWith("Side", StringComparison.Ordinal)
                        ? "Side"
                        : "Main";
                string deckName = field.Name.EndsWith(section,
                    StringComparison.Ordinal)
                    ? field.Name.Substring(0, field.Name.Length - section.Length)
                    : field.Name;
                string group = "curated:" + deckName;
                result.AddGroup(group, values);
                result.Add(values, group + ":" + section);
            }
            return result;
        }

        private static PackMemberships BuildPackMemberships()
        {
            string path = FullPath(PackCatalogPath);
            PackCatalogFile file = JsonUtility.FromJson<PackCatalogFile>(
                File.ReadAllText(path, Encoding.UTF8));
            var result = new PackMemberships();
            foreach (PackRecord pack in file?.packs ?? Array.Empty<PackRecord>())
            {
                if (pack == null)
                    continue;
                result.PackCount++;
                foreach (string raw in pack.cardIds ?? Array.Empty<string>())
                {
                    if (!TryCode(raw, out uint code))
                        continue;
                    result.Add(code, pack.packId ?? string.Empty);
                }
            }
            return result;
        }

        private static ScriptResolution ResolveCardScript(
            uint code,
            bool required)
        {
            if (!required)
            {
                return new ScriptResolution
                {
                    Found = true,
                    Compatibility = "NOT_REQUIRED"
                };
            }
            string scriptName = "c" + code.ToString(
                CultureInfo.InvariantCulture) + ".lua";
            string[] candidates =
            {
                CombineProjectPath(CustomScriptsPath, scriptName),
                CombineProjectPath(ScriptsPath, scriptName),
                CombineProjectPath(OfficialScriptsPath, scriptName)
            };
            string selected = candidates.FirstOrDefault(candidate =>
                File.Exists(FullPath(candidate)));
            if (string.IsNullOrEmpty(selected))
            {
                return new ScriptResolution
                {
                    Compatibility = "MISSING"
                };
            }
            var info = new FileInfo(FullPath(selected));
            string[] dependencies = MissingLuaDependencies(selected);
            return new ScriptResolution
            {
                Found = true,
                Empty = info.Length == 0,
                Path = selected,
                Source = selected.StartsWith(CustomScriptsPath,
                    StringComparison.OrdinalIgnoreCase)
                    ? "custom-override"
                    : selected.StartsWith(OfficialScriptsPath,
                        StringComparison.OrdinalIgnoreCase)
                        ? "official"
                        : "global-scripts",
                Hash = info.Length == 0 ? string.Empty : HashFile(selected),
                MissingDependencies = dependencies,
                Compatibility = info.Length == 0
                    ? "EMPTY"
                    : dependencies.Length > 0
                        ? "DEPENDENCY_MISSING"
                        : "RESOLVED_STATIC"
            };
        }

        private static string[] MissingLuaDependencies(string scriptPath)
        {
            string source = File.ReadAllText(
                FullPath(scriptPath), Encoding.UTF8);
            return LoadScriptPattern.Matches(source)
                .Cast<Match>()
                .Select(match => match.Groups["name"].Value.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.Ordinal)
                .Where(name => !ScriptExists(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool ScriptExists(string requestedName)
        {
            string safe = Path.GetFileName(requestedName);
            if (!string.Equals(safe, requestedName, StringComparison.Ordinal))
                return false;
            return new[]
                {
                    CombineProjectPath(CustomScriptsPath, safe),
                    CombineProjectPath(ScriptsPath, safe),
                    CombineProjectPath(OfficialScriptsPath, safe)
                }
                .Any(path => File.Exists(FullPath(path)));
        }

        private static string[] ApplicableScenarios(
            bool scriptRequired,
            CardRecord card,
            bool inDeck,
            bool online)
        {
            var result = new List<string>
            {
                "integridade_fontes"
            };
            if (scriptRequired || IsSpecialSummon(card))
            {
                result.Add("core_positivo_minimo");
                result.Add("core_negativo_relevante");
                result.Add("apresentacao_prompt_zona");
            }
            string description = card?.Description ?? string.Empty;
            if (ContainsAny(description, "uma vez por turno", "até o final",
                    "durante a fase", "próximo turno"))
                result.Add("persistencia_reset");
            if (ContainsAny(description, "efeito rápido", "ativado", "corrente",
                    "negue a ativação"))
                result.Add("corrente_timing");
            if (inDeck)
                result.Add("deck_smoke_ia");
            if (online)
                result.Add("multiplayer_privacidade_resync");
            return result.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] MissingCoverage(
            bool scriptRequired,
            bool inDeck,
            bool online)
        {
            var result = new List<string>();
            if (scriptRequired)
            {
                result.Add("dossie_semantico");
                result.Add("cenario_core_positivo");
                result.Add("cenario_core_negativo");
                result.Add("cenario_apresentacao");
            }
            if (inDeck)
                result.Add("deck_smoke_deterministico_por_linha_central");
            if (online)
                result.Add("host_cliente_privacidade_idempotencia_resync");
            if (result.Count == 0)
                result.Add("validacao_integridade_executada_neste_lote");
            return result.ToArray();
        }

        private static bool IsOnlinePriority(CardRecord card)
        {
            return card != null && ContainsAny(
                card.Description,
                "sua mão",
                "do seu deck",
                "revele",
                "com a face para baixo",
                "escolha 1 card",
                "ordem",
                "matéria xyz",
                "controle desse");
        }

        private static bool IsSpecialSummon(CardRecord card)
        {
            if (card == null)
                return false;
            const uint SpecialFrames = 0x40U | 0x80U | 0x2000U |
                                       0x800000U | 0x1000000U | 0x4000000U;
            return (card.Type & SpecialFrames) != 0;
        }

        private static bool ContainsAny(string source, params string[] values)
        {
            if (string.IsNullOrEmpty(source))
                return false;
            return values.Any(value => source.IndexOf(
                value, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string[] ExistingGlobalEvidence()
        {
            return new[]
            {
                "CardDatabaseEditModeTests (teste existente; execucao do lote pendente)",
                "CardCatalogBatchEditModeTests.EveryCompiledCoreCardRegistersWithNativeCoreLifecycle (teste existente; execucao do lote pendente)"
            };
        }

        private static string BuildConsoleSummary(
            CardAuditSnapshot snapshot,
            string mode)
        {
            return string.Concat(
                "ARCANE_CARD_AUDIT_", mode,
                " catalog=", snapshot.sources.cardCatalogUniqueOfficialIds,
                " docs=", snapshot.sources.documentationCsvUniqueIds,
                " database=", snapshot.sources.compiledDatabaseCards,
                " visuals=", snapshot.sources.visualManifestCards,
                " blocked=", snapshot.statuses.bloqueadaDados,
                " firstBatch=", snapshot.firstBatch.Length);
        }

        private static string[] Except(
            IEnumerable<string> left,
            IEnumerable<string> right)
        {
            return left.Except(right, StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string Normalize(string value)
        {
            return TryCode(value, out uint code)
                ? code.ToString("00000000", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static bool TryCode(string value, out uint code)
        {
            code = 0;
            return !string.IsNullOrWhiteSpace(value) &&
                   uint.TryParse(
                       value.Trim(),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out code) &&
                   code != 0;
        }

        private static int CountFiles(string projectPath, string pattern)
        {
            string path = FullPath(projectPath);
            return Directory.Exists(path)
                ? Directory.GetFiles(path, pattern, SearchOption.AllDirectories).Length
                : 0;
        }

        internal static string HashFile(string projectPath)
        {
            string path = FullPath(projectPath);
            if (!File.Exists(path))
                return string.Empty;
            using SHA256 algorithm = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Hex(algorithm.ComputeHash(stream));
        }

        private static string HashTree(string projectPath, string pattern)
        {
            string root = FullPath(projectPath);
            if (!Directory.Exists(root))
                return string.Empty;
            var manifest = new StringBuilder();
            foreach (string file in Directory.GetFiles(
                         root, pattern, SearchOption.AllDirectories)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                string relative = file.Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                manifest.Append(relative)
                    .Append('|')
                    .Append(HashAbsoluteFile(file))
                    .Append('\n');
            }
            using SHA256 algorithm = SHA256.Create();
            return Hex(algorithm.ComputeHash(
                Encoding.UTF8.GetBytes(manifest.ToString())));
        }

        private static string HashAbsoluteFile(string path)
        {
            using SHA256 algorithm = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Hex(algorithm.ComputeHash(stream));
        }

        private static string Hex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        internal static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ??
                   throw new DirectoryNotFoundException(
                       "Raiz do projeto Unity nao localizada.");
        }

        internal static string FullPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return ProjectRoot();
            return Path.IsPathRooted(projectPath)
                ? projectPath
                : Path.Combine(
                    ProjectRoot(),
                    projectPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string CombineProjectPath(string left, string right)
        {
            return (left.TrimEnd('/', '\\') + "/" +
                    (right ?? string.Empty).TrimStart('/', '\\'))
                .Replace('\\', '/');
        }

        private static string ReadGitBranch(string root)
        {
            string head = ReadGitHeadLine(root);
            const string prefix = "ref: refs/heads/";
            return head.StartsWith(prefix, StringComparison.Ordinal)
                ? head.Substring(prefix.Length).Trim()
                : "DETACHED";
        }

        private static string ReadGitHead(string root)
        {
            string head = ReadGitHeadLine(root);
            const string prefix = "ref: ";
            if (!head.StartsWith(prefix, StringComparison.Ordinal))
                return head.Trim();
            string reference = head.Substring(prefix.Length).Trim();
            string referencePath = Path.Combine(
                root, ".git", reference.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(referencePath))
                return File.ReadAllText(referencePath).Trim();
            string packed = Path.Combine(root, ".git", "packed-refs");
            if (!File.Exists(packed))
                return string.Empty;
            string suffix = " " + reference;
            string line = File.ReadLines(packed).FirstOrDefault(candidate =>
                !candidate.StartsWith("#", StringComparison.Ordinal) &&
                candidate.EndsWith(suffix, StringComparison.Ordinal));
            return string.IsNullOrEmpty(line)
                ? string.Empty
                : line.Substring(0, line.IndexOf(' '));
        }

        private static string ReadGitHeadLine(string root)
        {
            string path = Path.Combine(root, ".git", "HEAD");
            return File.Exists(path)
                ? File.ReadAllText(path).Trim()
                : string.Empty;
        }

        [Serializable]
        private sealed class TextCatalog
        {
            public int schemaVersion;
            public int count;
            public TextCard[] cards;
        }

        [Serializable]
        private sealed class TextCard
        {
            public uint code;
        }

        [Serializable]
        private sealed class PackCatalogFile
        {
            public int version;
            public PackRecord[] packs;
        }

        [Serializable]
        private sealed class PackRecord
        {
            public string packId;
            public string[] cardIds;
        }

        private sealed class ScriptResolution
        {
            public bool Found;
            public bool Empty;
            public string Source = string.Empty;
            public string Path = string.Empty;
            public string Hash = string.Empty;
            public string Compatibility = string.Empty;
            public string[] MissingDependencies = Array.Empty<string>();
        }

        private sealed class Memberships
        {
            private readonly Dictionary<uint, HashSet<string>> byCard = new();
            private readonly Dictionary<string, List<uint>> byGroup =
                new(StringComparer.Ordinal);

            public int ShopProductCount;
            public int StarterDeckCount;
            public int CuratedArrayCount;
            public string FirstStarterId = string.Empty;
            public HashSet<string> CardIds => byCard.Keys
                .Select(code => code.ToString("00000000"))
                .ToHashSet(StringComparer.Ordinal);

            public string[] For(uint code)
            {
                return byCard.TryGetValue(code, out HashSet<string> values)
                    ? values.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                    : Array.Empty<string>();
            }

            public IReadOnlyList<uint> Group(string name)
            {
                return byGroup.TryGetValue(name, out List<uint> values)
                    ? values
                    : Array.Empty<uint>();
            }

            public void Add(IEnumerable<string> cards, string label)
            {
                foreach (string raw in cards ?? Array.Empty<string>())
                {
                    if (TryCode(raw, out uint code))
                        Add(code, label);
                }
            }

            public void Add(IEnumerable<uint> cards, string label)
            {
                foreach (uint code in cards ?? Array.Empty<uint>())
                {
                    if (code != 0)
                        Add(code, label);
                }
            }

            public void AddGroup(string name, IEnumerable<string> cards)
            {
                foreach (string raw in cards ?? Array.Empty<string>())
                {
                    if (TryCode(raw, out uint code))
                        AddGroupCode(name, code);
                }
            }

            public void AddGroup(string name, IEnumerable<uint> cards)
            {
                foreach (uint code in cards ?? Array.Empty<uint>())
                {
                    if (code != 0)
                        AddGroupCode(name, code);
                }
            }

            private void Add(uint code, string label)
            {
                if (!byCard.TryGetValue(code, out HashSet<string> labels))
                    byCard[code] = labels = new HashSet<string>(StringComparer.Ordinal);
                labels.Add(label);
            }

            private void AddGroupCode(string name, uint code)
            {
                if (!byGroup.TryGetValue(name, out List<uint> values))
                    byGroup[name] = values = new List<uint>();
                if (!values.Contains(code))
                    values.Add(code);
            }
        }

        private sealed class PackMemberships
        {
            private readonly Dictionary<uint, HashSet<string>> byCard = new();
            public int PackCount;
            public HashSet<string> CardIds => byCard.Keys
                .Select(code => code.ToString("00000000"))
                .ToHashSet(StringComparer.Ordinal);

            public void Add(uint code, string pack)
            {
                if (!byCard.TryGetValue(code, out HashSet<string> values))
                    byCard[code] = values = new HashSet<string>(StringComparer.Ordinal);
                values.Add(pack);
            }

            public string[] For(uint code)
            {
                return byCard.TryGetValue(code, out HashSet<string> values)
                    ? values.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                    : Array.Empty<string>();
            }
        }

        private sealed class CsvDocument
        {
            private readonly Dictionary<string, List<string[]>> rows;
            public int RowCount { get; }
            public HashSet<string> Ids { get; }
            public string[] DuplicateIds { get; }

            private CsvDocument(
                Dictionary<string, List<string[]>> parsed,
                int rowCount)
            {
                rows = parsed;
                RowCount = rowCount;
                Ids = parsed.Keys.ToHashSet(StringComparer.Ordinal);
                DuplicateIds = parsed
                    .Where(pair => pair.Value.Count > 1)
                    .Select(pair => pair.Key)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
            }

            public bool Contains(string id)
            {
                return !string.IsNullOrEmpty(id) && rows.ContainsKey(id);
            }

            public static CsvDocument Load(string path, string idColumn)
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException("CSV documental ausente.", path);
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length == 0)
                    throw new InvalidDataException("CSV documental vazio: " + path);
                string[] header = ParseCsvLine(lines[0]);
                int idIndex = Array.FindIndex(header, value => string.Equals(
                    value, idColumn, StringComparison.OrdinalIgnoreCase));
                if (idIndex < 0)
                    throw new InvalidDataException(
                        "Coluna " + idColumn + " ausente em " + path);
                var result = new Dictionary<string, List<string[]>>(
                    StringComparer.Ordinal);
                int rowCount = 0;
                foreach (string line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    rowCount++;
                    string[] fields = ParseCsvLine(line);
                    string id = idIndex < fields.Length
                        ? Normalize(fields[idIndex])
                        : string.Empty;
                    if (string.IsNullOrEmpty(id))
                        continue;
                    if (!result.TryGetValue(id, out List<string[]> matches))
                        result[id] = matches = new List<string[]>();
                    matches.Add(fields);
                }
                return new CsvDocument(result, rowCount);
            }

            private static string[] ParseCsvLine(string line)
            {
                var result = new List<string>();
                var field = new StringBuilder();
                bool quoted = false;
                for (int index = 0; index < (line ?? string.Empty).Length; index++)
                {
                    char current = line[index];
                    if (current == '"')
                    {
                        if (quoted && index + 1 < line.Length &&
                            line[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            quoted = !quoted;
                        }
                    }
                    else if (current == ',' && !quoted)
                    {
                        result.Add(field.ToString());
                        field.Clear();
                    }
                    else
                    {
                        field.Append(current);
                    }
                }
                result.Add(field.ToString());
                return result.ToArray();
            }
        }
    }
}
#endif
