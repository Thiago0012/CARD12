using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor
{
    public static class StarterDeckAssetSynchronizer
    {
        private const string SourcePath =
            "Assets/Resources/StarterDecks/starter-deck-sources.json";
        private const string CatalogPath =
            "Assets/Resources/StarterDecks/StarterDeckCatalog.asset";
        private const string DefinitionFolder =
            "Assets/Resources/StarterDecks/Definitions";
        private const string ReportPath =
            "Assets/GeneratedReports/StarterDecks/StarterDeckImportReport.md";

        [MenuItem("Arcane Arena/Content/Sync Starter Deck Catalog")]
        public static void SyncStarterDeckCatalog()
        {
            CardCatalogSynchronizer.SyncStarterDeckCards();
            TextAsset sourceAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath);
            if (sourceAsset == null)
                throw new FileNotFoundException(SourcePath);

            StarterDeckSourceCatalogFile source =
                JsonUtility.FromJson<StarterDeckSourceCatalogFile>(
                    sourceAsset.text);
            if (source?.decks == null || source.decks.Count != 6)
                throw new InvalidDataException(
                    "A fonte dos decks iniciais deve conter exatamente seis entradas.");

            BanlistDefinition banlistDefinition =
                Resources.Load<BanlistDefinition>(
                    "Banlist/tcg_eu_2026_05_18");
            if (banlistDefinition == null)
                throw new FileNotFoundException(
                    "Resources/Banlist/tcg_eu_2026_05_18.asset");

            var banlist = new BanlistService(banlistDefinition);
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            Directory.CreateDirectory(DefinitionFolder);

            var definitions = new List<StarterDeckDefinition>();
            var report = BeginReport(source, banlistDefinition);
            foreach (StarterDeckSourceRecord record in source.decks)
            {
                StarterDeckSanitizationResult sanitized =
                    StarterDeckSanitizer.Sanitize(
                        record.raw,
                        banlist,
                        record.approvedReplacements);
                var issues = ValidateContent(
                    record,
                    sanitized,
                    database,
                    visuals);
                if (!sanitized.IsLegal)
                    issues.Add(sanitized.LegalitySummary);

                List<string> previews = sanitized.MainDeck
                    .Concat(sanitized.ExtraDeck)
                    .Distinct(StringComparer.Ordinal)
                    .Take(3)
                    .ToList();
                string assetPath =
                    $"{DefinitionFolder}/{record.id}.asset";
                StarterDeckDefinition definition =
                    AssetDatabase.LoadAssetAtPath<StarterDeckDefinition>(
                        assetPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<
                        StarterDeckDefinition>();
                    AssetDatabase.CreateAsset(definition, assetPath);
                }
                definition.Initialize(
                    record,
                    sanitized,
                    banlist.Id,
                    previews,
                    issues);
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
                AppendReport(report, definition);
            }

            StarterDeckCatalog catalog =
                AssetDatabase.LoadAssetAtPath<StarterDeckCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StarterDeckCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Initialize(
                source.catalogVersion,
                banlist.Id,
                StarterLegacyPolicy.LegacyPromptOnce,
                definitions);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(ReportPath);
            int blocked = definitions.Count(deck => !deck.IsPublishable);
            string message =
                $"ARCANE_STARTER_CATALOG_SYNC_OK decks={definitions.Count} " +
                $"publishable={definitions.Count - blocked} blocked={blocked}";
            if (blocked == 0)
                Debug.Log(message);
            else
                Debug.LogWarning(message);
        }

        private static List<string> ValidateContent(
            StarterDeckSourceRecord source,
            StarterDeckSanitizationResult deck,
            CardDatabase database,
            CardVisualCatalog visuals)
        {
            var issues = new List<string>();
            ValidateSection(
                "Main", deck.MainDeck, false, database, visuals, issues);
            ValidateSection(
                "Extra", deck.ExtraDeck, true, database, visuals, issues);
            ValidateSection(
                "Side", deck.SideDeck, null, database, visuals, issues);
            if (source.raw == null ||
                string.IsNullOrWhiteSpace(source.raw.sourceUrl))
            {
                issues.Add("URL de origem ausente.");
            }
            return issues.Distinct().ToList();
        }

        private static void ValidateSection(
            string section,
            IEnumerable<string> cards,
            bool? expectExtra,
            CardDatabase database,
            CardVisualCatalog visuals,
            ICollection<string> issues)
        {
            foreach (string passcode in cards)
            {
                if (!uint.TryParse(passcode, out uint code) ||
                    !database.TryGet(code, out CardRecord card))
                {
                    issues.Add($"{section}: carta {passcode} ausente no Core.");
                    continue;
                }
                if (expectExtra.HasValue &&
                    DeckRules.IsExtraDeck(card) != expectExtra.Value)
                {
                    issues.Add($"{section}: {passcode} está na seção incorreta.");
                }
                if (!visuals.TryGet(code, out CardVisualData visual) ||
                    !File.Exists(visuals.ArtPath(code)))
                {
                    issues.Add($"{section}: apresentação ausente para {passcode}.");
                    continue;
                }
                if (visual.scriptStatus != "not_required_no_effect" &&
                    string.IsNullOrWhiteSpace(visual.scriptFile))
                {
                    issues.Add($"{section}: script de efeito ausente para {passcode}.");
                }
            }
        }

        private static StringBuilder BeginReport(
            StarterDeckSourceCatalogFile source,
            BanlistDefinition banlist)
        {
            var report = new StringBuilder();
            report.AppendLine("# Starter Deck Import Report");
            report.AppendLine();
            report.AppendLine($"- Catalog version: `{source.catalogVersion}`");
            report.AppendLine($"- Active banlist: `{banlist.Id}`");
            report.AppendLine($"- Banlist hash: `{banlist.SourceSha256}`");
            report.AppendLine("- Runtime HTTP: `disabled` (assets locais)");
            report.AppendLine();
            return report;
        }

        private static void AppendReport(
            StringBuilder report,
            StarterDeckDefinition deck)
        {
            report.AppendLine($"## {deck.DisplayName} (`{deck.Id}`)");
            report.AppendLine();
            report.AppendLine($"- Source: {deck.Raw.sourceUrl}");
            if (!string.IsNullOrWhiteSpace(deck.Raw.sourceCorrectionNote))
                report.AppendLine($"- Source correction: {deck.Raw.sourceCorrectionNote}");
            report.AppendLine(
                $"- Raw: Main {deck.Raw.mainDeck.Count}, Extra " +
                $"{deck.Raw.extraDeck.Count}, Side {deck.Raw.sideDeck.Count}");
            report.AppendLine(
                $"- Sanitized: Main {deck.MainDeck.Count}, Extra " +
                $"{deck.ExtraDeck.Count}, Side {deck.SideDeck.Count}");
            report.AppendLine($"- Raw SHA-256: `{deck.RawSha256}`");
            report.AppendLine($"- Sanitized SHA-256: `{deck.SanitizedSha256}`");
            report.AppendLine($"- Publishable: `{deck.IsPublishable}`");
            foreach (ReplacementAuditEntry removal in deck.Replacements)
            {
                report.AppendLine(
                    $"- Removed `{removal.removedPasscode}` from " +
                    $"{removal.section}: {removal.reason}");
            }
            foreach (string issue in deck.ValidationIssues)
                report.AppendLine($"- BLOCKER: {issue}");
            report.AppendLine();
        }
    }
}
