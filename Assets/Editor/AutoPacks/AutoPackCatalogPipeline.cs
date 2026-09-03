using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ArcaneArena.Cards;
using ArcaneDuel.DuelEngine.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor.AutoPacks
{
    internal static class CardCatalogSnapshotBuilder
    {
        private const uint TokenType = 0x4000U;

        internal static CardCatalogSnapshot Build(
            AutoPackGenerationSettings settings)
        {
            var snapshot = new CardCatalogSnapshot();
            if (settings == null)
            {
                snapshot.Errors.Add("AutoPackGenerationSettings ausente.");
                return snapshot;
            }

            CardCatalog catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(
                AutoPackPaths.CardCatalog);
            if (catalog == null)
            {
                snapshot.Errors.Add("CardCatalog oficial ausente em " +
                    AutoPackPaths.CardCatalog + ".");
                return snapshot;
            }

            CardDatabase database;
            try
            {
                database = CardDatabase.LoadDefault();
            }
            catch (Exception exception)
            {
                snapshot.Errors.Add("Banco compilado de cartas invalido: " +
                    exception.GetBaseException().Message);
                return snapshot;
            }

            var hashLines = new List<string>
            {
                "generator=" + settings.GeneratorVersion.ToString(
                    CultureInfo.InvariantCulture)
            };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (CardCatalogEntry entry in catalog.Entries
                         .Where(candidate => candidate != null)
                         .OrderBy(candidate => Normalize(candidate.OfficialCardId),
                             StringComparer.Ordinal))
            {
                string cardId = Normalize(entry.OfficialCardId);
                if (string.IsNullOrWhiteSpace(cardId))
                {
                    if (entry.OfficiallyRegistered)
                        snapshot.Errors.Add("Entrada oficial sem cardId: " +
                            entry.DisplayName + ".");
                    continue;
                }
                if (!seen.Add(cardId))
                {
                    snapshot.Errors.Add("cardId duplicado no CardCatalog: " +
                        cardId + ".");
                    continue;
                }

                snapshot.KnownCardIds.Add(cardId);
                string artPath = entry.AuthoredArtwork != null
                    ? AssetDatabase.GetAssetPath(entry.AuthoredArtwork)
                    : string.Empty;
                bool excludedPath = settings.ExcludedPathTokens.Any(token =>
                    !string.IsNullOrWhiteSpace(token) &&
                    artPath.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                CardRecord card = null;
                bool coreExists = uint.TryParse(cardId, out uint code) &&
                    database.TryGet(code, out card);
                bool token = coreExists && (card.Type & TokenType) != 0;
                bool collectible = entry.OfficiallyRegistered &&
                    entry.IsReadyForGameplay && entry.IsCollectible;
                bool eligible = entry.OfficiallyRegistered &&
                    entry.IsReadyForGameplay && coreExists && !token &&
                    !excludedPath && entry.HasArtwork;

                string state = eligible ? "eligible" : "excluded";
                if (entry.OfficiallyRegistered && entry.IsReadyForGameplay &&
                    coreExists && !token && !excludedPath && !entry.HasArtwork)
                {
                    snapshot.DeferredCardIds.Add(cardId);
                    state = "deferred-art";
                    string message = "Carta " + cardId +
                        " nao possui arte para a loja e ficou pendente.";
                    if (settings.BlockOnMissingArtwork)
                        snapshot.Errors.Add(message);
                    else
                        snapshot.Warnings.Add(message);
                }
                else if (entry.OfficiallyRegistered && !coreExists)
                {
                    snapshot.Errors.Add("Carta oficial " + cardId +
                        " nao existe no banco compilado do Core.");
                    state = "missing-core";
                }

                if (collectible)
                    snapshot.CollectibleCardIds.Add(cardId);
                if (eligible)
                    snapshot.EligibleCardIds.Add(cardId);
                hashLines.Add(string.Join("|", new[]
                {
                    cardId,
                    state,
                    ((int)entry.Category).ToString(CultureInfo.InvariantCulture),
                    ((int)entry.MonsterFrame).ToString(CultureInfo.InvariantCulture),
                    coreExists ? card.Type.ToString(CultureInfo.InvariantCulture) : "0",
                    string.IsNullOrWhiteSpace(artPath)
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(artPath)
                }));
            }

            snapshot.KnownCardIds.Sort(StringComparer.Ordinal);
            snapshot.CollectibleCardIds.Sort(StringComparer.Ordinal);
            snapshot.EligibleCardIds.Sort(StringComparer.Ordinal);
            snapshot.DeferredCardIds.Sort(StringComparer.Ordinal);
            snapshot.Hash = AutoPackDeterminism.Sha256(
                string.Join("\n", hashLines));
            return snapshot;
        }

        internal static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string trimmed = value.Trim();
            if (trimmed.Any(character => !char.IsDigit(character)))
                return string.Empty;
            string normalized = trimmed.TrimStart('0');
            return normalized.Length == 0 ? "0" : normalized;
        }
    }

    internal sealed class CatalogPackRecord
    {
        internal string PackId;
        internal string DisplayName;
        internal string Description;
        internal int PriceCoins;
        internal int Origin;
        internal string GenerationBatchId;
        internal int GeneratorVersion;
        internal string ContentHash;
        internal bool ContentLockedAfterPublish;
        internal bool CountsForAutoCoverage;
        internal bool ManualVisualOverride;
        internal bool NeedsPreviewReview;
        internal bool Published;
        internal List<string> CardIds = new();
        internal List<string> PreviewCardIds = new();
        internal JObject Source;
    }

    internal static class AutoPackCatalogDocument
    {
        internal static JObject LoadRoot()
        {
            if (!File.Exists(AutoPackPaths.Catalog))
                throw new FileNotFoundException(AutoPackPaths.Catalog);
            JObject root = JObject.Parse(
                File.ReadAllText(AutoPackPaths.Catalog, Encoding.UTF8));
            if (root["packs"] is not JArray)
                throw new InvalidDataException("PackCatalog.json nao possui packs.");
            return root;
        }

        internal static IReadOnlyList<CatalogPackRecord> ReadPacks(JObject root)
        {
            var result = new List<CatalogPackRecord>();
            foreach (JObject node in ((JArray)root["packs"]).OfType<JObject>())
            {
                int origin = node.Value<int?>("origin") ?? 0;
                result.Add(new CatalogPackRecord
                {
                    PackId = node.Value<string>("packId") ?? string.Empty,
                    DisplayName = node.Value<string>("displayName") ?? string.Empty,
                    Description = node.Value<string>("description") ?? string.Empty,
                    PriceCoins = node.Value<int?>("priceCoins") ??
                        AutoPackGenerationSettings.RequiredPrice,
                    Origin = origin,
                    GenerationBatchId = node.Value<string>(
                        "generationBatchId") ?? string.Empty,
                    GeneratorVersion = node.Value<int?>("generatorVersion") ?? 0,
                    ContentHash = node.Value<string>("contentHash") ?? string.Empty,
                    ContentLockedAfterPublish = node.Value<bool?>(
                        "contentLockedAfterPublish") ?? true,
                    CountsForAutoCoverage = node.Value<bool?>(
                        "countsForAutoCoverage") ?? true,
                    ManualVisualOverride = node.Value<bool?>(
                        "manualVisualOverride") ?? false,
                    NeedsPreviewReview = node.Value<bool?>(
                        "needsPreviewReview") ?? false,
                    Published = node.Value<bool?>("published") ?? true,
                    CardIds = NormalizeArray(node["cardIds"]),
                    PreviewCardIds = NormalizeArray(node["previewCardIds"]),
                    Source = node
                });
            }
            return result;
        }

        internal static JObject CreateAutoPackNode(
            GeneratedPackRecord record,
            string displayName,
            IReadOnlyList<string> previews,
            int generatorVersion)
        {
            return new JObject
            {
                ["packId"] = record.packId,
                ["displayName"] = displayName,
                ["description"] =
                    "Cinco sorteios independentes com reposicao. " +
                    "Duplicatas sao permitidas.",
                ["priceCoins"] = AutoPackGenerationSettings.RequiredPrice,
                ["origin"] = 1,
                ["generationBatchId"] = record.generationBatchId,
                ["generatorVersion"] = generatorVersion,
                ["contentLockedAfterPublish"] = true,
                ["contentHash"] = record.contentHash,
                ["countsForAutoCoverage"] = true,
                ["manualVisualOverride"] = false,
                ["needsPreviewReview"] = false,
                ["published"] = true,
                ["previewCardIds"] = new JArray(previews),
                ["cardIds"] = new JArray(record.cardIds)
            };
        }

        internal static void SaveAtomically(JObject root)
        {
            string fullPath = Path.GetFullPath(AutoPackPaths.Catalog);
            string temporary = fullPath + ".autopack.tmp";
            string backup = fullPath + ".autopack.bak";
            string json = root.ToString(Formatting.Indented) + "\n";
            JObject.Parse(json);
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            try
            {
                File.Replace(temporary, fullPath, backup, true);
                if (File.Exists(backup))
                    File.Delete(backup);
            }
            catch
            {
                if (File.Exists(backup))
                    File.Copy(backup, fullPath, true);
                throw;
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            AssetDatabase.ImportAsset(
                AutoPackPaths.Catalog,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        internal static string PackContentHash(
            string packId,
            IEnumerable<string> cardIds)
        {
            return AutoPackDeterminism.Sha256(
                string.Join("|", new[]
                {
                    packId ?? string.Empty,
                    AutoPackGenerationSettings.RequiredPrice.ToString(
                        CultureInfo.InvariantCulture),
                    string.Join(",", cardIds ?? Array.Empty<string>())
                }));
        }

        internal static string PublishedSemanticHash(CatalogPackRecord pack)
        {
            return AutoPackDeterminism.Sha256(string.Join("|", new[]
            {
                pack.PackId ?? string.Empty,
                pack.DisplayName ?? string.Empty,
                pack.Description ?? string.Empty,
                string.Join(",", pack.CardIds)
            }));
        }

        private static List<string> NormalizeArray(JToken token)
        {
            return token is JArray array
                ? array.Values<string>()
                    .Select(CardCatalogSnapshotBuilder.Normalize)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList()
                : new List<string>();
        }
    }

    internal static class AutoPackAssetRepository
    {
        internal static (AutoPackGenerationSettings settings,
            AutoPackGenerationManifest manifest) GetOrCreate()
        {
            EnsureFolder("Assets/GameData");
            EnsureFolder("Assets/GameData/Shop");
            EnsureFolder("Assets/GameData/Shop/Packs");
            EnsureFolder(AutoPackPaths.GeneratedFolder);

            AutoPackGenerationSettings settings =
                AssetDatabase.LoadAssetAtPath<AutoPackGenerationSettings>(
                    AutoPackPaths.Settings);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<
                    AutoPackGenerationSettings>();
                Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(
                        AutoPackPaths.DefaultPackSprite)
                    .OfType<Sprite>()
                    .FirstOrDefault();
                settings.InitializeDefaultSprite(sprite);
                AssetDatabase.CreateAsset(settings, AutoPackPaths.Settings);
                EditorUtility.SetDirty(settings);
            }

            AutoPackGenerationManifest manifest =
                AssetDatabase.LoadAssetAtPath<AutoPackGenerationManifest>(
                    AutoPackPaths.Manifest);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<
                    AutoPackGenerationManifest>();
                AssetDatabase.CreateAsset(manifest, AutoPackPaths.Manifest);
                EditorUtility.SetDirty(manifest);
            }
            AssetDatabase.SaveAssets();
            return (settings, manifest);
        }

        internal static AutoPackMetadata CreateMetadata(
            GeneratedPackRecord record,
            IReadOnlyList<string> previews,
            AutoPackGenerationSettings settings)
        {
            string sequence = record.packId.Substring(
                record.packId.LastIndexOf('-') + 1);
            string path = AutoPackPaths.GeneratedFolder +
                "/AutoPack_" + sequence + ".asset";
            AutoPackMetadata existing =
                AssetDatabase.LoadAssetAtPath<AutoPackMetadata>(path);
            if (existing != null)
            {
                if (existing.Published &&
                    string.Equals(existing.ContentHash, record.contentHash,
                        StringComparison.Ordinal))
                {
                    record.assetGuid = AssetDatabase.AssetPathToGUID(path);
                    return existing;
                }
                throw new InvalidOperationException(
                    "Asset automatico existente nao pode ser sobrescrito: " + path);
            }

            var metadata = ScriptableObject.CreateInstance<AutoPackMetadata>();
            metadata.Initialize(
                record.packId,
                record.generationBatchId,
                settings.GeneratorVersion,
                record.cardIds,
                previews,
                settings.DefaultPackSprite,
                record.contentHash);
            AssetDatabase.CreateAsset(metadata, path);
            AssetDatabase.SaveAssets();
            record.assetGuid = AssetDatabase.AssetPathToGUID(path);
            return metadata;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) ||
                !AssetDatabase.IsValidFolder(parent))
            {
                throw new DirectoryNotFoundException(
                    "Pasta pai ausente para " + path + ".");
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    public static class AutoPackGenerationCoordinator
    {
        private static bool running;

        public static bool IsRunning => running;

        [MenuItem("Tools/Game/Shop/Auto Packs/Preview Changes")]
        public static void PreviewChanges()
        {
            AutoPackGenerationReport report = Run(save: false, "ManualPreview");
            LogReport(report, "PREVIEW");
        }

        [MenuItem("Tools/Game/Shop/Auto Packs/Rebuild Now")]
        public static void RebuildNow()
        {
            AutoPackGenerationReport report = Run(save: true, "ManualRebuild");
            LogReport(report, "REBUILD");
            if (report.Errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", report.Errors));
        }

        public static void RebuildFromCommandLine()
        {
            RebuildNow();
            Debug.Log("ARCANE_AUTO_PACK_REBUILD_OK");
        }

        [MenuItem("Tools/Game/Shop/Auto Packs/Validate")]
        public static void ValidateFromMenu()
        {
            AutoPackValidationResult result = AutoPackValidation.RunStrict();
            if (!result.IsValid)
                throw new InvalidOperationException(result.ToMessage());
            Debug.Log("ARCANE_AUTO_PACK_VALIDATE_OK " + result.Summary);
        }

        public static void ValidateFromCommandLine()
        {
            ValidateFromMenu();
        }

        [MenuItem("Tools/Game/Shop/Auto Packs/Open Manifest")]
        public static void OpenManifest()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<
                AutoPackGenerationManifest>(AutoPackPaths.Manifest);
            if (Selection.activeObject != null)
                EditorGUIUtility.PingObject(Selection.activeObject);
        }

        internal static void RequestRebuild(string reason)
        {
            if (running || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
            {
                CardCatalogChangePostprocessor.ScheduleAgain();
                return;
            }
            AutoPackGenerationReport report = Run(save: true, reason);
            LogReport(report, "AUTO");
        }

        internal static AutoPackGenerationReport Run(bool save, string reason)
        {
            if (running)
                throw new InvalidOperationException(
                    "Uma geracao de pacotes ja esta em andamento.");
            running = true;
            try
            {
                (AutoPackGenerationSettings settings,
                    AutoPackGenerationManifest manifest) =
                    AutoPackAssetRepository.GetOrCreate();
                var report = new AutoPackGenerationReport
                {
                    PreviousHash = manifest.LastSourceCatalogHash
                };
                if (!settings.Enabled)
                {
                    report.Warnings.Add("Gerador desabilitado por settings.");
                    return report;
                }
                if (!settings.HasNormativeValues)
                {
                    report.Errors.Add(
                        "Settings deve manter min=40, max=85 e price=25.");
                    return report;
                }

                CardCatalogSnapshot snapshot =
                    CardCatalogSnapshotBuilder.Build(settings);
                report.SourceHash = snapshot.Hash;
                report.Warnings.AddRange(snapshot.Warnings);
                report.Errors.AddRange(snapshot.Errors);
                if (report.Errors.Count > 0)
                    return report;

                JObject root = AutoPackCatalogDocument.LoadRoot();
                IReadOnlyList<CatalogPackRecord> existing =
                    AutoPackCatalogDocument.ReadPacks(root);
                var covered = new HashSet<string>(
                    existing.Where(pack => pack.Published &&
                            pack.CountsForAutoCoverage)
                        .SelectMany(pack => pack.CardIds),
                    StringComparer.Ordinal);
                var eligible = new HashSet<string>(
                    snapshot.EligibleCardIds,
                    StringComparer.Ordinal);
                var deferred = new HashSet<string>(
                    snapshot.DeferredCardIds,
                    StringComparer.Ordinal);
                var previousPending = new HashSet<string>(
                    manifest.PendingCardIds,
                    StringComparer.Ordinal);
                foreach (string removed in previousPending.Where(id =>
                             !eligible.Contains(id) && !deferred.Contains(id)))
                {
                    report.RemovedPendingCardIds.Add(removed);
                }

                var eligiblePending = previousPending
                    .Where(eligible.Contains)
                    .Where(id => !covered.Contains(id))
                    .ToHashSet(StringComparer.Ordinal);
                string[] newIds = snapshot.EligibleCardIds
                    .Where(id => !covered.Contains(id) &&
                        !eligiblePending.Contains(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                report.NewCardIds.AddRange(newIds);
                eligiblePending.UnionWith(newIds);

                string seedMaterial = string.Join("|", new[]
                {
                    settings.GeneratorVersion.ToString(CultureInfo.InvariantCulture),
                    snapshot.Hash,
                    manifest.NextPackSequence.ToString(CultureInfo.InvariantCulture)
                });
                IReadOnlyList<string> shuffled = AutoPackDeterminism.Shuffle(
                    eligiblePending,
                    seedMaterial);
                AutoPackPartitionResult partition = AutoPackPartitioner.Partition(
                    shuffled,
                    settings.MinCardsPerPack,
                    settings.MaxCardsPerPack);
                string batchId = AutoPackDeterminism.Sha256(
                        "batch|" + seedMaterial)
                    .Substring(0, 32)
                    .ToLowerInvariant();

                int sequence = manifest.NextPackSequence;
                var previewsByPack = new Dictionary<string, string[]>(
                    StringComparer.Ordinal);
                foreach (IReadOnlyList<string> cards in partition.Packs)
                {
                    string packId = "auto-pack-" + sequence.ToString(
                        "0000", CultureInfo.InvariantCulture);
                    var record = new GeneratedPackRecord
                    {
                        packId = packId,
                        generationBatchId = batchId,
                        cardIds = cards.ToList(),
                        published = true,
                        manualVisualOverride = false,
                        contentHash = AutoPackCatalogDocument.PackContentHash(
                            packId, cards)
                    };
                    report.CreatedPacks.Add(record);
                    previewsByPack[packId] = ChoosePreviews(cards);
                    sequence++;
                }
                report.PendingCardIds.AddRange(
                    partition.Pending.Concat(deferred)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal));

                ValidateDraft(report, existing);
                if (!save || report.Errors.Count > 0)
                    return report;

                foreach (GeneratedPackRecord record in report.CreatedPacks)
                {
                    AutoPackAssetRepository.CreateMetadata(
                        record,
                        previewsByPack[record.packId],
                        settings);
                    string displayName = string.Format(
                        CultureInfo.InvariantCulture,
                        settings.DisplayNamePattern,
                        int.Parse(record.packId.Substring(
                            record.packId.LastIndexOf('-') + 1),
                            CultureInfo.InvariantCulture));
                    ((JArray)root["packs"]).Add(
                        AutoPackCatalogDocument.CreateAutoPackNode(
                            record,
                            displayName,
                            previewsByPack[record.packId],
                            settings.GeneratorVersion));
                }
                if (report.CreatedPacks.Count > 0)
                    AutoPackCatalogDocument.SaveAtomically(root);

                manifest.Commit(
                    settings.GeneratorVersion,
                    snapshot.Hash,
                    sequence,
                    report.PendingCardIds,
                    report.CreatedPacks);
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssets();
                report.Saved = true;
                WriteMarkdownReport(report, reason, existing);
                return report;
            }
            catch (Exception exception)
            {
                var failed = new AutoPackGenerationReport();
                failed.Errors.Add(exception.GetBaseException().Message);
                return failed;
            }
            finally
            {
                running = false;
            }
        }

        private static string[] ChoosePreviews(IReadOnlyList<string> cards)
        {
            return new[]
                {
                    cards[0],
                    cards[cards.Count / 2],
                    cards[cards.Count - 1]
                }
                .Distinct(StringComparer.Ordinal)
                .Concat(cards)
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToArray();
        }

        private static void ValidateDraft(
            AutoPackGenerationReport report,
            IReadOnlyList<CatalogPackRecord> existing)
        {
            var ids = new HashSet<string>(
                existing.Select(pack => pack.PackId),
                StringComparer.Ordinal);
            var covered = new HashSet<string>(
                existing.SelectMany(pack => pack.CardIds),
                StringComparer.Ordinal);
            foreach (GeneratedPackRecord pack in report.CreatedPacks)
            {
                if (!ids.Add(pack.packId))
                    report.Errors.Add("packId duplicado: " + pack.packId);
                if (pack.cardIds.Count < AutoPackGenerationSettings.RequiredMinimum ||
                    pack.cardIds.Count > AutoPackGenerationSettings.RequiredMaximum)
                {
                    report.Errors.Add(pack.packId +
                        " possui quantidade fora de 40-85.");
                }
                if (pack.cardIds.Distinct(StringComparer.Ordinal).Count() !=
                    pack.cardIds.Count)
                    report.Errors.Add(pack.packId + " possui cardId duplicado.");
                foreach (string cardId in pack.cardIds)
                {
                    if (!covered.Add(cardId))
                        report.Errors.Add("cardId coberto duas vezes: " + cardId);
                }
            }
        }

        private static void WriteMarkdownReport(
            AutoPackGenerationReport report,
            string reason,
            IReadOnlyList<CatalogPackRecord> previousPacks)
        {
            string directory = Path.GetDirectoryName(AutoPackPaths.Report);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            var text = new StringBuilder();
            text.AppendLine("# Auto Pack Generation Report");
            text.AppendLine();
            text.AppendLine("- UTC: `" + DateTime.UtcNow.ToString("O") + "`");
            text.AppendLine("- Trigger: `" + reason + "`");
            text.AppendLine("- Previous source hash: `" +
                report.PreviousHash + "`");
            text.AppendLine("- Current source hash: `" +
                report.SourceHash + "`");
            text.AppendLine("- Published packs before generation: `" +
                previousPacks.Count + "`");
            text.AppendLine("- New cardIds detected: `" +
                report.NewCardIds.Count + "`");
            text.AppendLine("- Packs created: `" +
                report.CreatedPacks.Count + "`");
            text.AppendLine("- Pending cardIds: `" +
                report.PendingCardIds.Count + "`");
            text.AppendLine();
            text.AppendLine("## Created packs");
            text.AppendLine();
            foreach (GeneratedPackRecord pack in report.CreatedPacks)
            {
                text.AppendLine("- `" + pack.packId + "`: " +
                    pack.cardIds.Count + " cards, hash `" +
                    pack.contentHash + "`, asset GUID `" +
                    pack.assetGuid + "`");
            }
            if (report.CreatedPacks.Count == 0)
                text.AppendLine("- None.");
            text.AppendLine();
            text.AppendLine("## Pending pool");
            text.AppendLine();
            text.AppendLine(report.PendingCardIds.Count == 0
                ? "- Empty."
                : "- `" + string.Join("`, `", report.PendingCardIds) + "`");
            text.AppendLine();
            text.AppendLine("## Warnings and errors");
            text.AppendLine();
            foreach (string warning in report.Warnings)
                text.AppendLine("- WARNING: " + warning);
            foreach (string error in report.Errors)
                text.AppendLine("- ERROR: " + error);
            if (report.Warnings.Count == 0 && report.Errors.Count == 0)
                text.AppendLine("- None.");
            File.WriteAllText(
                AutoPackPaths.Report,
                text.ToString(),
                new UTF8Encoding(false));
        }

        private static void LogReport(
            AutoPackGenerationReport report,
            string mode)
        {
            string message = "ARCANE_AUTO_PACK_" + mode +
                " created=" + report.CreatedPacks.Count +
                " pending=" + report.PendingCardIds.Count +
                " new=" + report.NewCardIds.Count +
                " saved=" + report.Saved;
            if (report.Errors.Count > 0)
                Debug.LogError(message + " errors=" +
                    string.Join(" | ", report.Errors));
            else
                Debug.Log(message);
        }
    }
}
