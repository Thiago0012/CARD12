#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ArcaneArena.Editor.CardAudit
{
    internal static class CardAuditReportWriter
    {
        public static void WriteAll(CardAuditSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            string directory = CardInventoryAudit.FullPath(
                CardInventoryAudit.ReportDirectory);
            Directory.CreateDirectory(directory);
            WriteAtomic(
                CardInventoryAudit.FullPath(CardInventoryAudit.MatrixJsonPath),
                JsonUtility.ToJson(snapshot, true) + Environment.NewLine);
            WriteAtomic(
                CardInventoryAudit.FullPath(CardInventoryAudit.MatrixCsvPath),
                BuildCsv(snapshot));
            WriteAtomic(
                CardInventoryAudit.FullPath(CardInventoryAudit.ReportPath),
                BuildInventoryReport(snapshot));
            WriteAtomic(
                CardInventoryAudit.FullPath(
                    CardInventoryAudit.CompatibilityReportPath),
                BuildCompatibilityReport(snapshot));
            WriteAtomic(
                CardInventoryAudit.FullPath(CardInventoryAudit.FirstBatchPath),
                BuildFirstBatchReport(snapshot));
        }

        private static string BuildCsv(CardAuditSnapshot snapshot)
        {
            var output = new StringBuilder();
            WriteRow(output, new[]
            {
                "official_card_id", "stable_id", "name", "category",
                "monster_frame", "type_name", "archetype_setcodes", "decks",
                "packs", "in_catalog", "registered", "ready_for_gameplay",
                "in_documentation_csv", "in_core_documentation_csv",
                "in_compiled_database", "in_text_database", "in_visual_manifest",
                "artwork_found", "artwork_path", "script_required", "script_found",
                "script_source", "script_path", "script_sha256",
                "script_compatibility", "missing_script_dependencies",
                "applicable_scenarios", "existing_evidence", "missing_coverage",
                "core_result", "presentation_result", "ai_result",
                "multiplayer_result", "regression_result", "status", "priority",
                "failure_code", "responsible_layer", "blocking_reason", "risk",
                "source_version", "evidence_updated_utc"
            });
            foreach (CardHealthEntry card in snapshot.cards ??
                     Array.Empty<CardHealthEntry>())
            {
                WriteRow(output, new[]
                {
                    card.officialCardId, card.stableId, card.name, card.category,
                    card.monsterFrame, card.typeName, card.archetypeSetcodes,
                    Join(card.decks), Join(card.packs), Bool(card.inCardCatalog),
                    Bool(card.officiallyRegistered), Bool(card.readyForGameplay),
                    Bool(card.inDocumentationCsv), Bool(card.inCoreDocumentationCsv),
                    Bool(card.inCompiledDatabase), Bool(card.inTextDatabase),
                    Bool(card.inVisualManifest), Bool(card.artworkFound),
                    card.artworkPath, Bool(card.scriptRequired),
                    Bool(card.scriptFound), card.scriptSource, card.scriptPath,
                    card.scriptSha256, card.scriptCompatibility,
                    Join(card.missingScriptDependencies),
                    Join(card.applicableScenarios), Join(card.existingEvidence),
                    Join(card.missingCoverage), card.coreResult,
                    card.presentationResult, card.aiResult, card.multiplayerResult,
                    card.regressionResult, card.status, card.priority,
                    card.failureCode, card.responsibleLayer, card.blockingReason,
                    card.risk, card.sourceVersion, card.evidenceUpdatedUtc
                });
            }
            return output.ToString();
        }

        private static string BuildInventoryReport(CardAuditSnapshot snapshot)
        {
            CardAuditSourceSummary source = snapshot.sources;
            CardAuditStatusSummary status = snapshot.statuses;
            CardAuditDivergences divergence = snapshot.divergences;
            var report = new StringBuilder();
            report.AppendLine("# Auditoria de cartas - Fases 0 e 1")
                .AppendLine()
                .AppendLine("> Escopo deliberado: baseline, inventario e priorizacao. " +
                            "Nenhum efeito, regra do core ou comportamento funcional foi alterado.")
                .AppendLine()
                .AppendLine("## Baseline reproduzivel")
                .AppendLine()
                .AppendLine("| Item | Valor |")
                .AppendLine("|---|---|")
                .AppendLine(Row("Gerado em UTC", snapshot.generatedUtc))
                .AppendLine(Row("Projeto", snapshot.projectVersion))
                .AppendLine(Row("Unity", snapshot.unityVersion))
                .AppendLine(Row("Branch", snapshot.gitBranch))
                .AppendLine(Row("HEAD", snapshot.gitHead))
                .AppendLine(Row("API do core", snapshot.coreApiVersion))
                .AppendLine(Row("ygopro-core", snapshot.coreCommit))
                .AppendLine(Row("CardScripts", snapshot.cardScriptsCommit))
                .AppendLine(Row("BabelCDB", snapshot.babelCdbCommit))
                .AppendLine()
                .AppendLine("## Fontes encontradas")
                .AppendLine()
                .AppendLine("| Fonte | Contagem | SHA-256 |")
                .AppendLine("|---|---:|---|")
                .AppendLine(SourceRow("CardCatalog.asset", source.cardCatalogEntries,
                    source.cardCatalogSha256))
                .AppendLine(SourceRow("Documentation/CardCatalog.csv",
                    source.documentationCsvRows, source.documentationCsvSha256))
                .AppendLine(SourceRow("Documentation/CoreCardCatalog.csv",
                    source.coreDocumentationRows, source.coreDocumentationSha256))
                .AppendLine(SourceRow("cards.bin", source.compiledDatabaseCards,
                    source.cardsBinSha256))
                .AppendLine(SourceRow("card-texts.json", source.textDatabaseCards,
                    source.cardTextsSha256))
                .AppendLine(SourceRow("card-visuals.json", source.visualManifestCards,
                    source.visualManifestSha256))
                .AppendLine(SourceRow("scripts oficiais", source.officialScripts,
                    source.officialScriptsTreeSha256))
                .AppendLine(SourceRow("scripts customizados", source.customScripts,
                    source.customScriptsTreeSha256))
                .AppendLine(SourceRow("plugin Windows", 1,
                    source.windowsCorePluginSha256))
                .AppendLine(SourceRow("plugin Android arm64", 1,
                    source.androidCorePluginSha256))
                .AppendLine()
                .AppendLine("Conteudo publicado: " + source.shopDeckProducts +
                            " produtos/decks de loja, " + source.starterDecks +
                            " starters, " + source.curatedDeckArrays +
                            " listas curadas e " + source.shopPacks + " pacotes.")
                .AppendLine()
                .AppendLine("## Estado da matriz")
                .AppendLine()
                .AppendLine("| Status | Cartas |")
                .AppendLine("|---|---:|")
                .AppendLine(CountRow("INVENTARIADA", status.inventariada))
                .AppendLine(CountRow("BLOQUEADA_DADOS", status.bloqueadaDados))
                .AppendLine(CountRow("CARREGA", status.carrega))
                .AppendLine(CountRow("TESTE_PARCIAL", status.testeParcial))
                .AppendLine(CountRow("PASSA_CORE", status.passaCore))
                .AppendLine(CountRow("PASSA_APRESENTACAO", status.passaApresentacao))
                .AppendLine(CountRow("PASSA_IA", status.passaIa))
                .AppendLine(CountRow("PASSA_ONLINE", status.passaOnline))
                .AppendLine(CountRow("CONCLUIDA", status.concluida))
                .AppendLine()
                .AppendLine("Prioridades: P0=" + status.priorityP0 + ", P1=" +
                            status.priorityP1 + ", P2=" + status.priorityP2 +
                            ", P3=" + status.priorityP3 + ", P4=" +
                            status.priorityP4 + ", P5=" + status.priorityP5 + ".")
                .AppendLine()
                .AppendLine("## Divergencias de integridade")
                .AppendLine()
                .AppendLine("| Codigo | Divergencia | Total | Amostra |")
                .AppendLine("|---|---|---:|---|");

            AddDivergence(report, "F01", "ID duplicado no catalogo",
                divergence.duplicateCatalogIds);
            AddDivergence(report, "F01", "ID duplicado no CSV documental",
                divergence.duplicateDocumentationIds);
            AddDivergence(report, "F01", "Entrada invalida no catalogo",
                divergence.invalidCatalogEntries);
            AddDivergence(report, "F01", "Catalogo sem CSV documental",
                divergence.catalogMissingFromDocumentation);
            AddDivergence(report, "F01", "CSV documental sem catalogo",
                divergence.documentationMissingFromCatalog);
            AddDivergence(report, "F01", "Catalogo sem documento do core",
                divergence.catalogMissingFromCoreDocumentation);
            AddDivergence(report, "F01", "Documento do core sem catalogo",
                divergence.coreDocumentationMissingFromCatalog);
            AddDivergence(report, "F01", "Catalogo sem dados compilados",
                divergence.catalogMissingFromCompiledDatabase);
            AddDivergence(report, "F01", "Dados compilados sem catalogo",
                divergence.compiledDatabaseMissingFromCatalog);
            AddDivergence(report, "F01", "Catalogo sem texto compilado",
                divergence.catalogMissingFromTextDatabase);
            AddDivergence(report, "F08", "Catalogo sem manifesto visual",
                divergence.catalogMissingFromVisualManifest);
            AddDivergence(report, "F08", "Manifesto visual sem catalogo",
                divergence.visualManifestMissingFromCatalog);
            AddDivergence(report, "F02", "Script obrigatorio ausente",
                divergence.missingRequiredScripts);
            AddDivergence(report, "F02", "Script obrigatorio vazio",
                divergence.emptyRequiredScripts);
            AddDivergence(report, "F02", "Dependencia Lua ausente",
                divergence.missingScriptDependencies);
            AddDivergence(report, "F08", "Arte ausente",
                divergence.missingArtwork);
            AddDivergence(report, "F01", "Carta de deck fora do catalogo",
                divergence.deckCardsMissingFromCatalog);
            AddDivergence(report, "F01", "Carta de pack fora do catalogo",
                divergence.packCardsMissingFromCatalog);

            report.AppendLine()
                .AppendLine("## Arquitetura preservada")
                .AppendLine()
                .AppendLine("- BabelCDB e os artefatos compilados continuam sendo a fonte de dados.")
                .AppendLine("- CardScripts/Lua continuam sendo a fonte de efeitos.")
                .AppendLine("- ygopro-core continua sendo o arbitro das regras.")
                .AppendLine("- C# permanece responsavel por catalogo, apresentacao, protocolo, IA e multiplayer.")
                .AppendLine("- A ferramenta nova fica em `Assets/Editor/CardAudit` e executa em modo de leitura/relatorio.")
                .AppendLine()
                .AppendLine("## Limites desta evidencia")
                .AppendLine()
                .AppendLine("A Fase 1 comprova presenca e coerencia estrutural; ela nao comprova semantica. " +
                            "`CARREGA` significa que dados, texto, visual, arte e script obrigatorio estao localizaveis. " +
                            "Os status PASSA_* e CONCLUIDA permanecem zerados ate os cenarios das fases seguintes.")
                .AppendLine()
                .AppendLine("A suite existente `CardCatalogBatchEditModeTests` declara 23 lotes de 25 imagens " +
                            "(cobertura maxima de 575 posicoes), enquanto o manifesto atual possui " +
                            source.visualManifestCards + " entradas. O teste de ciclo nativo percorre a base compilada, " +
                            "mas a lacuna visual deve ser removida numa fase posterior.")
                .AppendLine()
                .AppendLine("Arquivos gerados: `CardHealthMatrix.csv`, `CardHealthMatrix.json`, " +
                            "`CardScriptCompatibilityReport.md` e `FirstBatchPlan.md`.");
            return report.ToString();
        }

        private static string BuildCompatibilityReport(CardAuditSnapshot snapshot)
        {
            CardHealthEntry[] cards = snapshot.cards ?? Array.Empty<CardHealthEntry>();
            CardHealthEntry[] required = cards.Where(card => card.scriptRequired).ToArray();
            var report = new StringBuilder();
            report.AppendLine("# Compatibilidade estatica de scripts de cartas")
                .AppendLine()
                .AppendLine("> Este relatorio comprova resolucao de arquivo e dependencias `Duel.LoadScript`; " +
                            "nao comprova semantica do efeito nem compatibilidade total com o core.")
                .AppendLine()
                .AppendLine("- Scripts obrigatorios: " + required.Length)
                .AppendLine("- Resolvidos e nao vazios: " + required.Count(card =>
                    card.scriptFound && card.scriptCompatibility == "RESOLVED_STATIC"))
                .AppendLine("- Ausentes: " + required.Count(card => !card.scriptFound))
                .AppendLine("- Vazios: " + required.Count(card =>
                    card.scriptCompatibility == "EMPTY"))
                .AppendLine("- Com dependencia ausente: " + required.Count(card =>
                    card.missingScriptDependencies != null &&
                    card.missingScriptDependencies.Length > 0))
                .AppendLine()
                .AppendLine("| Card ID | Nome | Origem | Resultado | Dependencias ausentes |")
                .AppendLine("|---|---|---|---|---|");
            foreach (CardHealthEntry card in required.Where(card =>
                         !card.scriptFound ||
                         card.scriptCompatibility != "RESOLVED_STATIC" ||
                         (card.missingScriptDependencies?.Length ?? 0) > 0))
            {
                report.Append("| ").Append(EscapeMd(card.officialCardId))
                    .Append(" | ").Append(EscapeMd(card.name))
                    .Append(" | ").Append(EscapeMd(card.scriptSource))
                    .Append(" | ").Append(EscapeMd(card.scriptCompatibility))
                    .Append(" | ").Append(EscapeMd(Join(card.missingScriptDependencies)))
                    .AppendLine(" |");
            }
            return report.ToString();
        }

        private static string BuildFirstBatchReport(CardAuditSnapshot snapshot)
        {
            var report = new StringBuilder();
            report.AppendLine("# Primeiro lote proposto")
                .AppendLine()
                .AppendLine("Lote de " + (snapshot.firstBatch?.Length ?? 0) +
                            " cartas para as fases semanticas, priorizando linhas centrais de " +
                            "Blue-Eyes, Dark Magician, Red-Eyes e o primeiro starter publicado.")
                .AppendLine()
                .AppendLine("> Aprovacao funcional ainda nao foi concedida. Os campos de efeito " +
                            "permanecem marcados para preenchimento na Fase 3.")
                .AppendLine()
                .AppendLine("| # | Card ID | Nome | Decks | Prioridade | Estado | Motivo | Cenarios |")
                .AppendLine("|---:|---|---|---|---|---|---|---|");
            foreach (CardAuditBatchSeed card in snapshot.firstBatch ??
                     Array.Empty<CardAuditBatchSeed>())
            {
                report.Append("| ").Append(card.order.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(EscapeMd(card.officialCardId))
                    .Append(" | ").Append(EscapeMd(card.name))
                    .Append(" | ").Append(EscapeMd(Join(card.decks)))
                    .Append(" | ").Append(EscapeMd(card.priority))
                    .Append(" | ").Append(EscapeMd(card.status))
                    .Append(" | ").Append(EscapeMd(card.rationale))
                    .Append(" | ").Append(EscapeMd(Join(card.proposedScenarios)))
                    .AppendLine(" |");
            }
            return report.ToString();
        }

        private static void AddDivergence(
            StringBuilder report,
            string code,
            string description,
            string[] values)
        {
            values ??= Array.Empty<string>();
            string sample = string.Join(", ", values.Take(8));
            if (values.Length > 8)
                sample += ", ...";
            report.Append("| ").Append(code).Append(" | ")
                .Append(EscapeMd(description)).Append(" | ")
                .Append(values.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(EscapeMd(sample)).AppendLine(" |");
        }

        private static string SourceRow(string name, int count, string hash)
        {
            return "| " + EscapeMd(name) + " | " +
                   count.ToString(CultureInfo.InvariantCulture) + " | `" +
                   (string.IsNullOrEmpty(hash) ? "AUSENTE" : hash) + "` |";
        }

        private static string Row(string name, string value)
        {
            return "| " + EscapeMd(name) + " | " + EscapeMd(value) + " |";
        }

        private static string CountRow(string name, int value)
        {
            return "| " + name + " | " +
                   value.ToString(CultureInfo.InvariantCulture) + " |";
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Join(IEnumerable<string> values)
        {
            return string.Join(";", values ?? Array.Empty<string>());
        }

        private static string EscapeMd(string value)
        {
            return (value ?? string.Empty)
                .Replace("|", "\\|")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static void WriteRow(StringBuilder output, IEnumerable<string> values)
        {
            output.AppendLine(string.Join(",", values.Select(Csv)));
        }

        private static string Csv(string value)
        {
            string safe = value ?? string.Empty;
            return safe.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                ? safe
                : "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private static void WriteAtomic(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content ?? string.Empty,
                new UTF8Encoding(false));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }
    }
}
#endif
