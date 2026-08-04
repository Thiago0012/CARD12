#if UNITY_EDITOR
using System;

namespace ArcaneArena.Editor.CardAudit
{
    [Serializable]
    public sealed class CardAuditSnapshot
    {
        public int schemaVersion = 1;
        public string generatedUtc = string.Empty;
        public string projectVersion = string.Empty;
        public string unityVersion = string.Empty;
        public string coreApiVersion = string.Empty;
        public string coreCommit = string.Empty;
        public string cardScriptsCommit = string.Empty;
        public string babelCdbCommit = string.Empty;
        public string gitBranch = string.Empty;
        public string gitHead = string.Empty;
        public CardAuditSourceSummary sources = new();
        public CardAuditStatusSummary statuses = new();
        public CardAuditDivergences divergences = new();
        public CardHealthEntry[] cards = Array.Empty<CardHealthEntry>();
        public CardAuditBatchSeed[] firstBatch =
            Array.Empty<CardAuditBatchSeed>();
    }

    [Serializable]
    public sealed class CardAuditSourceSummary
    {
        public int cardCatalogEntries;
        public int cardCatalogUniqueOfficialIds;
        public int documentationCsvRows;
        public int documentationCsvUniqueIds;
        public int coreDocumentationRows;
        public int coreDocumentationUniqueIds;
        public int compiledDatabaseCards;
        public int textDatabaseCards;
        public int visualManifestCards;
        public int officialScripts;
        public int customScripts;
        public int streamingArtFiles;
        public int shopDeckProducts;
        public int starterDecks;
        public int curatedDeckArrays;
        public int shopPacks;
        public string cardCatalogSha256 = string.Empty;
        public string documentationCsvSha256 = string.Empty;
        public string coreDocumentationSha256 = string.Empty;
        public string cardsBinSha256 = string.Empty;
        public string cardTextsSha256 = string.Empty;
        public string visualManifestSha256 = string.Empty;
        public string officialScriptsTreeSha256 = string.Empty;
        public string customScriptsTreeSha256 = string.Empty;
        public string windowsCorePluginSha256 = string.Empty;
        public string androidCorePluginSha256 = string.Empty;
    }

    [Serializable]
    public sealed class CardAuditStatusSummary
    {
        public int inventariada;
        public int bloqueadaDados;
        public int carrega;
        public int testeParcial;
        public int passaCore;
        public int passaApresentacao;
        public int passaIa;
        public int passaOnline;
        public int concluida;
        public int priorityP0;
        public int priorityP1;
        public int priorityP2;
        public int priorityP3;
        public int priorityP4;
        public int priorityP5;
    }

    [Serializable]
    public sealed class CardAuditDivergences
    {
        public string[] duplicateCatalogIds = Array.Empty<string>();
        public string[] duplicateDocumentationIds = Array.Empty<string>();
        public string[] invalidCatalogEntries = Array.Empty<string>();
        public string[] catalogMissingFromDocumentation = Array.Empty<string>();
        public string[] documentationMissingFromCatalog = Array.Empty<string>();
        public string[] catalogMissingFromCoreDocumentation = Array.Empty<string>();
        public string[] coreDocumentationMissingFromCatalog = Array.Empty<string>();
        public string[] catalogMissingFromCompiledDatabase = Array.Empty<string>();
        public string[] compiledDatabaseMissingFromCatalog = Array.Empty<string>();
        public string[] catalogMissingFromTextDatabase = Array.Empty<string>();
        public string[] catalogMissingFromVisualManifest = Array.Empty<string>();
        public string[] visualManifestMissingFromCatalog = Array.Empty<string>();
        public string[] missingRequiredScripts = Array.Empty<string>();
        public string[] emptyRequiredScripts = Array.Empty<string>();
        public string[] missingScriptDependencies = Array.Empty<string>();
        public string[] missingArtwork = Array.Empty<string>();
        public string[] deckCardsMissingFromCatalog = Array.Empty<string>();
        public string[] packCardsMissingFromCatalog = Array.Empty<string>();
    }

    [Serializable]
    public sealed class CardHealthEntry
    {
        public string officialCardId = string.Empty;
        public string stableId = string.Empty;
        public string name = string.Empty;
        public string category = string.Empty;
        public string monsterFrame = string.Empty;
        public string typeName = string.Empty;
        public string archetypeSetcodes = string.Empty;
        public string aliasOfficialCardId = string.Empty;
        public string[] decks = Array.Empty<string>();
        public string[] packs = Array.Empty<string>();
        public bool inCardCatalog;
        public bool officiallyRegistered;
        public bool readyForGameplay;
        public bool inDocumentationCsv;
        public bool inCoreDocumentationCsv;
        public bool inCompiledDatabase;
        public bool inTextDatabase;
        public bool inVisualManifest;
        public bool artworkFound;
        public string artworkPath = string.Empty;
        public string artworkGuid = string.Empty;
        public bool scriptRequired;
        public bool scriptFound;
        public string scriptSource = string.Empty;
        public string scriptPath = string.Empty;
        public string scriptSha256 = string.Empty;
        public string scriptCompatibility = string.Empty;
        public string[] missingScriptDependencies = Array.Empty<string>();
        public string[] applicableScenarios = Array.Empty<string>();
        public string[] existingEvidence = Array.Empty<string>();
        public string[] missingCoverage = Array.Empty<string>();
        public string coreResult = "NAO_EXECUTADO_NESTE_LOTE";
        public string presentationResult = "NAO_EXECUTADO_NESTE_LOTE";
        public string aiResult = "NAO_APLICAVEL_OU_NAO_EXECUTADO";
        public string multiplayerResult = "NAO_APLICAVEL_OU_NAO_EXECUTADO";
        public string regressionResult = "NAO_EXECUTADO_NESTE_LOTE";
        public string status = "INVENTARIADA";
        public string priority = "P5";
        public string failureCode = string.Empty;
        public string responsibleLayer = string.Empty;
        public string blockingReason = string.Empty;
        public string risk = string.Empty;
        public string sourceVersion = string.Empty;
        public string evidenceUpdatedUtc = string.Empty;
    }

    [Serializable]
    public sealed class CardAuditBatchSeed
    {
        public int order;
        public string officialCardId = string.Empty;
        public string name = string.Empty;
        public string[] decks = Array.Empty<string>();
        public string priority = string.Empty;
        public string status = string.Empty;
        public string normalizedCondition = "PREENCHER_NA_FASE_3";
        public string normalizedCost = "PREENCHER_NA_FASE_3";
        public string normalizedTarget = "PREENCHER_NA_FASE_3";
        public string normalizedOperation = "PREENCHER_NA_FASE_3";
        public string normalizedDuration = "PREENCHER_NA_FASE_3";
        public string normalizedLimit = "PREENCHER_NA_FASE_3";
        public string[] proposedScenarios = Array.Empty<string>();
        public string rationale = string.Empty;
    }
}
#endif
