using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArcaneArena.Editor
{
    public sealed class ShopCatalogValidator : IPreprocessBuildWithReport
    {
        private const string AuthorizationCatalogPath =
            "Assets/Resources/Shop/AuthorizedCoinRecipientsCatalog.asset";
        private const string FrontendBootstrapScriptPath =
            "Assets/Scripts/Frontend/GameFrontendBootstrap.cs";

        public int callbackOrder => -200;

        [MenuItem("Arcane Arena/Validar Loja e Economia")]
        public static void ValidateFromMenu()
        {
            string[] problems = FindProblems(
                productionBuild: !EditorUserBuildSettings.development);
            if (problems.Length == 0)
            {
                Debug.Log("[Loja] Catálogo válido: cobertura, preços e IDs conferidos.");
                return;
            }
            Debug.LogError("[Loja] " + string.Join("\n[Loja] ", problems));
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            bool productionBuild =
                (report.summary.options & BuildOptions.Development) == 0;
            string[] problems = FindProblems(productionBuild);
            if (problems.Length > 0)
                throw new BuildFailedException(
                    "A loja não passou na validação:\n" + string.Join("\n", problems));
        }

        public static string[] FindProblems()
        {
            return FindProblems(productionBuild: false);
        }

        private static string[] FindProblems(bool productionBuild)
        {
            var problems = new List<string>();
            string[] productIds = DeckShopCatalog.Products
                .Select(product => product.ProductId).ToArray();
            AddDuplicates(problems, productIds, "ID de Deck Estrutural");
            foreach (DeckShopProduct product in DeckShopCatalog.Products)
            {
                if (product.PriceCoins <= 0)
                    problems.Add(product.ProductId + ": preço deve ser positivo.");
                if (product.MaxPurchases <= 0)
                    problems.Add(product.ProductId + ": limite de compras inválido.");
                if (product.PreviewCardIds.Count != 3)
                    problems.Add(product.ProductId + ": deve possuir 3 artes de destaque.");
                if (product.Previews.Any(preview => !preview.HasValidCrop))
                    problems.Add(product.ProductId + ": recorte de arte fora de 0..1.");
                if (product.MainDeckCardIds.Count == 0)
                    problems.Add(product.ProductId + ": Deck Principal vazio.");
            }

            var distributed = new List<string>();
            string[] packIds = ShopPackCatalog.Packs
                .Select(pack => pack.PackId).ToArray();
            AddDuplicates(problems, packIds, "ID de pacote");
            foreach (ShopPackDefinition pack in ShopPackCatalog.Packs)
            {
                if (pack.CardIds.Count < ShopPackCatalog.MinimumCardsPerPack ||
                    pack.CardIds.Count > ShopPackCatalog.MaximumCardsPerPack)
                {
                    problems.Add(pack.PackId + $": deve conter de " +
                        $"{ShopPackCatalog.MinimumCardsPerPack} a " +
                        $"{ShopPackCatalog.MaximumCardsPerPack} IDs.");
                }
                if (pack.PriceCoins != ShopPackCatalog.PackPriceCoins)
                    problems.Add(pack.PackId + ": deve custar 25 moedas.");
                if (pack.PreviewCardIds.Count != 3)
                    problems.Add(pack.PackId + ": deve possuir 3 previews.");
                AddDuplicates(problems, pack.CardIds, pack.PackId + " / carta");
                distributed.AddRange(pack.CardIds);
            }

            AddDuplicates(problems, distributed, "cobertura entre pacotes");
            CardCatalog cardCatalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(
                "Assets/Cards/CardCatalog.asset");
            if (cardCatalog == null)
            {
                problems.Add("Assets/Cards/CardCatalog.asset não foi encontrado.");
            }
            else
            {
                string[] expected = cardCatalog.Entries
                    .Where(entry => entry != null && entry.IsCollectible &&
                        entry.IsReadyForGameplay && entry.OfficiallyRegistered)
                    .Select(entry => FrontendCardIdentity.NormalizeOfficialId(
                        entry.OfficialCardId))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                string[] invalidCards = distributed
                    .Where(cardId =>
                        DeckRepository.ResolveCard(cardCatalog, cardId) == null)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (invalidCards.Length > 0)
                {
                    problems.Add("IDs sem carta real no CardCatalog: " +
                        string.Join(",", invalidCards));
                }
                string[] missing = expected.Except(
                    distributed, StringComparer.Ordinal).ToArray();
                if (missing.Length > 0)
                    problems.Add("Cartas ausentes dos pacotes: " +
                        string.Join(",", missing));
            }
            ValidateAuthorizationCatalog(problems);
            ValidateBootstrapReferences(problems);
            return problems.ToArray();
        }

        private static void ValidateAuthorizationCatalog(
            ICollection<string> problems)
        {
            AuthorizedCoinRecipientsCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AuthorizedCoinRecipientsCatalog>(
                    AuthorizationCatalogPath);
            if (catalog == null)
            {
                problems.Add(
                    "Catálogo de destinatários autorizados não encontrado em " +
                    AuthorizationCatalogPath + ".");
                return;
            }
            if (catalog.CatalogVersion < 1)
                problems.Add("catalogVersion de autorização deve ser no mínimo 1.");
            if (catalog.Entries.Count == 0)
                problems.Add("O catálogo de destinatários autorizados está vazio.");

            foreach (AuthorizedRecipientEntry entry in catalog.Entries)
            {
                if (entry == null)
                {
                    problems.Add("O catálogo de autorização contém uma entrada nula.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(entry.EntryId))
                    problems.Add("Uma entrada autorizada está sem entryId.");
                if (string.IsNullOrWhiteSpace(entry.Nickname))
                    problems.Add(entry.EntryId + ": nickname vazio.");
                string expected = NicknameNormalizer.Normalize(entry.Nickname);
                if (!string.Equals(expected, entry.NormalizedNickname,
                        StringComparison.Ordinal))
                {
                    problems.Add(entry.EntryId +
                        ": normalizedNickname está desatualizado.");
                }
            }

            AddDuplicates(
                problems,
                catalog.Entries.Where(entry => entry != null)
                    .Select(entry => entry.EntryId),
                "entryId de autorização");
            string[] duplicateNames = catalog.Entries
                .Where(entry => entry != null &&
                    entry.Status != AuthorizedRecipientStatus.Revoked &&
                    !string.IsNullOrWhiteSpace(entry.NormalizedNickname))
                .GroupBy(
                    entry => entry.NormalizedNickname,
                    StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateNames.Length > 0)
            {
                problems.Add("Nickname normalizado duplicado: " +
                    string.Join(",", duplicateNames));
            }
        }

        private static void ValidateBootstrapReferences(
            ICollection<string> problems)
        {
            string bootstrapGuid = AssetDatabase.AssetPathToGUID(
                FrontendBootstrapScriptPath);
            string catalogGuid = AssetDatabase.AssetPathToGUID(
                AuthorizationCatalogPath);
            if (string.IsNullOrWhiteSpace(bootstrapGuid) ||
                string.IsNullOrWhiteSpace(catalogGuid))
            {
                return;
            }

            IEnumerable<string> paths = AssetDatabase.FindAssets("t:Scene")
                .Concat(AssetDatabase.FindAssets("t:Prefab"))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path));
            foreach (string path in paths)
            {
                string yaml;
                try
                {
                    yaml = File.ReadAllText(path);
                }
                catch (IOException)
                {
                    continue;
                }
                if (!yaml.Contains(bootstrapGuid, StringComparison.Ordinal))
                    continue;
                if (!yaml.Contains(
                        "authorizedCoinRecipientsCatalog: {fileID: 11400000, guid: " +
                        catalogGuid,
                        StringComparison.Ordinal))
                {
                    problems.Add(path +
                        ": GameFrontendBootstrap sem referência serializada ao catálogo de autorização.");
                }
            }
        }

        private static void AddDuplicates(
            ICollection<string> problems,
            IEnumerable<string> values,
            string label)
        {
            string[] duplicates = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicates.Length > 0)
                problems.Add(label + " duplicado: " + string.Join(",", duplicates));
        }
    }
}
