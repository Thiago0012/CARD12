using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace ArcaneArena.Editor.AutoPacks
{
    public sealed class AutoPackValidationResult
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool IsValid => Errors.Count == 0;
        public string Summary { get; internal set; } = string.Empty;

        public string ToMessage()
        {
            return string.Join("\n", Errors.Select(error => "- " + error));
        }
    }

    public static class AutoPackValidation
    {
        private static readonly IReadOnlyDictionary<string, string>
            BaselineManualPackHashes = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["pack-01-v1"] = "4463EE27ED1388FBD2827503CB8C6306ADA9DD333930AF4501A0B35E109E40FF",
                ["pack-02-v1"] = "AB8F6CEE11CC83F74F568B42DC5CBC5DDF19CE591593A3DB97C8AF2D4F839020",
                ["pack-03-v1"] = "77F943F4013D9759B27D68AD15732B1FDE544387784794CE9993423A3913EF67",
                ["pack-04-v1"] = "8E533AEDBF8D57ED7339457D89DADEAC24CF61E65742CF7D446C7D0A04860507",
                ["pack-05-v1"] = "9E353807075540B500717AE9D14C69914EF5DA6C73B30FEC61BB75A3AC7FA27D",
                ["pack-06-v1"] = "C3B18752523539869D6F0130DA18CF4C44C746DEFA41347D1604FF93A792FA14",
                ["pack-07-v1"] = "FCDFC8712765DD1C817A0CBF4E4478B6D5F40C95D251B2E67FDD8373B0572B9B",
                ["pack-08-v1"] = "78EF1B0A73618436C56C2D8FEF25FC888A633455977D043A100BC51BA027152A",
                ["pack-09-v1"] = "603D8D979E639920CA32B5143081294EB84E2C5F768BF347653AB71062BC9123",
                ["pack-10-v1"] = "1179A06E1126029A91573A987D2ED7EFE18E10DCADE0449765DCB0DF54923CFE",
                ["pack-11-v1"] = "00BA4D7C4137A18615CF530FD468C1ED96A52C6D5BA233C61F71179C17B413D6",
                ["pack-12-v1"] = "AD423C3A6860548083C163566CAE2FD783ED263CD06AF71E516661AB7D4E3941",
                ["pack-13-v1"] = "BB79D3F6EE84691F506363CE5F0D9C546F3D8AFEBC94BF614D61BA4AF8CA9FB8",
                ["pack-14-v1"] = "91A1020939033672472FD49DA0E63CAD9D76CE6F5482AA04C4EE26A374AF7DA2",
                ["pack-15-v1"] = "CF2D860E411A0DE1266249488AD25889EA1DB4B6F7C4A4B847F2E29CEF36FB59",
                ["pack-16-v1"] = "4E8251392E7395D1AA738A3FB2AC028DF7B6D0305F3D28442FDC83FF27EC77EB",
                ["pack-17-v1"] = "AF146D72AEFF3F5E629F3D214AAB222B52958157C2D4013DA9856BC5541D8021",
                ["pack-18-v1"] = "C158CC0A62AEBE8C93548148095035D5F31F54C9DA49BEC73472052002F8C2AF",
                ["pack-19-v1"] = "E74816775FEE8DE4114A3FC4C1B37244E40F9263929B2B8E3AF52D8A4F515EF7"
            };

        public static AutoPackValidationResult RunStrict()
        {
            var result = new AutoPackValidationResult();
            AutoPackGenerationSettings settings =
                AssetDatabase.LoadAssetAtPath<AutoPackGenerationSettings>(
                    AutoPackPaths.Settings);
            AutoPackGenerationManifest manifest =
                AssetDatabase.LoadAssetAtPath<AutoPackGenerationManifest>(
                    AutoPackPaths.Manifest);
            if (settings == null)
                result.Errors.Add("Settings de pacotes automaticos ausente.");
            if (manifest == null)
                result.Errors.Add("Manifesto de pacotes automaticos ausente.");
            if (settings == null || manifest == null)
                return result;
            if (!settings.HasNormativeValues)
                result.Errors.Add("Settings alterado: valores obrigatorios sao 35/38/35.");
            if (settings.DefaultPackSprite == null)
                result.Errors.Add("defaultPackSprite nao foi configurado.");

            CardCatalogSnapshot snapshot =
                CardCatalogSnapshotBuilder.Build(settings);
            result.Errors.AddRange(snapshot.Errors);
            result.Warnings.AddRange(snapshot.Warnings);
            if (!string.Equals(
                    manifest.LastSourceCatalogHash,
                    snapshot.Hash,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Catalogo obsoleto: execute Tools/Game/Shop/Auto Packs/Rebuild Now.");
            }

            IReadOnlyList<CatalogPackRecord> packs;
            try
            {
                packs = AutoPackCatalogDocument.ReadPacks(
                    AutoPackCatalogDocument.LoadRoot());
            }
            catch (Exception exception)
            {
                result.Errors.Add("PackCatalog invalido: " +
                    exception.GetBaseException().Message);
                return result;
            }

            ValidateBaselineManualPacks(result, packs);
            ValidatePackInvariants(result, packs, snapshot, manifest);
            ValidatePlayerAssemblySafety(result);
            int autoCount = packs.Count(pack => pack.Origin == 1);
            result.Summary = "packs=" + packs.Count +
                " auto=" + autoCount +
                " pending=" + manifest.PendingCardIds.Count +
                " eligible=" + snapshot.EligibleCardIds.Count;
            return result;
        }

        private static void ValidateBaselineManualPacks(
            AutoPackValidationResult result,
            IReadOnlyList<CatalogPackRecord> packs)
        {
            foreach ((string packId, string expectedHash) in
                     BaselineManualPackHashes)
            {
                CatalogPackRecord pack = packs.FirstOrDefault(candidate =>
                    string.Equals(candidate.PackId, packId,
                        StringComparison.Ordinal));
                if (pack == null)
                {
                    result.Errors.Add("Pack publicado removido: " + packId + ".");
                    continue;
                }
                string actual = AutoPackCatalogDocument.PublishedSemanticHash(pack);
                if (!string.Equals(actual, expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add("Pack publicado foi alterado: " +
                        packId + ".");
                }
            }
        }

        private static void ValidatePackInvariants(
            AutoPackValidationResult result,
            IReadOnlyList<CatalogPackRecord> packs,
            CardCatalogSnapshot snapshot,
            AutoPackGenerationManifest manifest)
        {
            string[] duplicatePackIds = packs
                .GroupBy(pack => pack.PackId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            foreach (string duplicate in duplicatePackIds)
                result.Errors.Add("packId duplicado: " + duplicate + ".");

            var coverage = new HashSet<string>(StringComparer.Ordinal);
            var known = new HashSet<string>(
                snapshot.KnownCardIds,
                StringComparer.Ordinal);
            var metadataByPack = AssetDatabase.FindAssets(
                    "t:AutoPackMetadata",
                    new[] { AutoPackPaths.GeneratedFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AutoPackMetadata>)
                .Where(metadata => metadata != null)
                .ToDictionary(metadata => metadata.PackId,
                    StringComparer.Ordinal);

            foreach (CatalogPackRecord pack in packs)
            {
                if (string.IsNullOrWhiteSpace(pack.PackId))
                    result.Errors.Add("Pack sem ID.");
                if (pack.PriceCoins != AutoPackGenerationSettings.RequiredPrice)
                    result.Errors.Add(pack.PackId + " deve custar 35 moedas.");
                if (pack.CardIds.Count == 0 || pack.CardIds.Count > 38)
                    result.Errors.Add(pack.PackId + " possui tamanho invalido.");
                if (pack.CardIds.Distinct(StringComparer.Ordinal).Count() !=
                    pack.CardIds.Count)
                    result.Errors.Add(pack.PackId + " possui carta duplicada.");
                foreach (string cardId in pack.CardIds)
                {
                    if (!known.Contains(cardId))
                        result.Errors.Add(pack.PackId +
                            " referencia carta inexistente " + cardId + ".");
                    if (pack.Published && pack.CountsForAutoCoverage &&
                        !coverage.Add(cardId))
                        result.Errors.Add("Cobertura duplicada para " + cardId + ".");
                }

                if (pack.Origin != 1)
                    continue;
                if (pack.CardIds.Count < 35 || pack.CardIds.Count > 38)
                    result.Errors.Add(pack.PackId +
                        " automatico deve conter 35-38 cartas.");
                if (!pack.Published || !pack.ContentLockedAfterPublish ||
                    !pack.CountsForAutoCoverage)
                    result.Errors.Add(pack.PackId +
                        " automatico nao esta publicado/bloqueado.");
                if (pack.PreviewCardIds.Count != 3 ||
                    pack.PreviewCardIds.Any(id => !pack.CardIds.Contains(id)))
                    result.Errors.Add(pack.PackId + " deve possuir 3 previews validos.");
                string expectedHash = AutoPackCatalogDocument.PackContentHash(
                    pack.PackId,
                    pack.CardIds);
                if (!string.Equals(expectedHash, pack.ContentHash,
                        StringComparison.OrdinalIgnoreCase))
                    result.Errors.Add(pack.PackId + " possui contentHash invalido.");
                if (!metadataByPack.TryGetValue(
                        pack.PackId,
                        out AutoPackMetadata metadata))
                {
                    result.Errors.Add(pack.PackId + " nao possui metadata asset.");
                }
                else if (!string.Equals(metadata.ContentHash, pack.ContentHash,
                             StringComparison.OrdinalIgnoreCase) ||
                         metadata.PriceCoins != 35 ||
                         metadata.CardIds.Count != pack.CardIds.Count ||
                         metadata.PreviewCardIds.Count != 3 ||
                         metadata.PackSprite == null)
                {
                    result.Errors.Add(pack.PackId +
                        " diverge do seu metadata asset.");
                }
            }

            var pending = new HashSet<string>(
                manifest.PendingCardIds,
                StringComparer.Ordinal);
            string[] uncovered = snapshot.EligibleCardIds
                .Where(id => !coverage.Contains(id) && !pending.Contains(id))
                .ToArray();
            if (uncovered.Length > 0)
                result.Errors.Add("IDs elegiveis nao processados: " +
                    string.Join(",", uncovered) + ".");
            int eligiblePending = manifest.PendingCardIds.Count(id =>
                snapshot.EligibleCardIds.Contains(id));
            if (eligiblePending >= AutoPackGenerationSettings.RequiredMinimum)
                result.Errors.Add("Pending contem cartas suficientes para novo pack.");

            foreach (GeneratedPackRecord record in manifest.GeneratedPacks)
            {
                CatalogPackRecord pack = packs.FirstOrDefault(candidate =>
                    string.Equals(candidate.PackId, record.packId,
                        StringComparison.Ordinal));
                if (pack == null || !string.Equals(
                        pack.ContentHash,
                        record.contentHash,
                        StringComparison.OrdinalIgnoreCase))
                    result.Errors.Add("Manifesto diverge do catalogo em " +
                        record.packId + ".");
            }
        }

        private static void ValidatePlayerAssemblySafety(
            AutoPackValidationResult result)
        {
            string[] forbiddenTokens =
            {
                "DevCoinCheatListener",
                "EditorSelectedCardZero",
                "EditorSelectedCardZeroCoinGrant",
                "ZeroCoinGrant",
                "Editor/DeveloperTools"
            };
            foreach (UnityEditor.Compilation.Assembly assembly in
                     CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                foreach (string sourceFile in assembly.sourceFiles ??
                             Array.Empty<string>())
                {
                    string normalized = sourceFile.Replace('\\', '/');
                    if (forbiddenTokens.Any(token => normalized.IndexOf(
                            token,
                            StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        result.Errors.Add("Ferramenta de moedas entrou no Player: " +
                            normalized + ".");
                    }
                }
            }
        }
    }

    public sealed class AutoPackPreBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -500;

        public void OnPreprocessBuild(BuildReport report)
        {
            AutoPackValidationResult validation = AutoPackValidation.RunStrict();
            if (!validation.IsValid)
            {
                throw new BuildFailedException(
                    "Pacotes automaticos invalidos:\n" +
                    validation.ToMessage());
            }
        }
    }
}
