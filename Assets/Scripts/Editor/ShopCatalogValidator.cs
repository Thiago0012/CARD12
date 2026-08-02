using System;
using System.Collections.Generic;
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
        public int callbackOrder => -200;

        [MenuItem("Arcane Arena/Validar Loja e Economia")]
        public static void ValidateFromMenu()
        {
            string[] problems = FindProblems();
            if (problems.Length == 0)
            {
                Debug.Log("[Loja] Catálogo válido: cobertura, preços e IDs conferidos.");
                return;
            }
            Debug.LogError("[Loja] " + string.Join("\n[Loja] ", problems));
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            string[] problems = FindProblems();
            if (problems.Length > 0)
                throw new BuildFailedException(
                    "A loja não passou na validação:\n" + string.Join("\n", problems));
        }

        public static string[] FindProblems()
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
                if (pack.CardIds.Count < 1 || pack.CardIds.Count > 38)
                    problems.Add(pack.PackId + ": deve conter de 1 a 38 IDs.");
                AddDuplicates(problems, pack.CardIds, pack.PackId + " / carta");
                distributed.AddRange(pack.CardIds);
            }

            AddDuplicates(problems, distributed, "cobertura entre pacotes");
            string[] expected = DeckShopCatalog.CollectibleCardIds.ToArray();
            CardCatalog cardCatalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(
                "Assets/Cards/CardCatalog.asset");
            if (cardCatalog == null)
            {
                problems.Add("Assets/Cards/CardCatalog.asset não foi encontrado.");
            }
            else
            {
                string[] invalidCards = expected
                    .Where(cardId =>
                        DeckRepository.ResolveCard(cardCatalog, cardId) == null)
                    .ToArray();
                if (invalidCards.Length > 0)
                {
                    problems.Add("IDs sem carta real no CardCatalog: " +
                        string.Join(",", invalidCards));
                }
            }
            string[] missing = expected.Except(distributed, StringComparer.Ordinal).ToArray();
            string[] unknown = distributed.Except(expected, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0)
                problems.Add("Cartas ausentes dos pacotes: " + string.Join(",", missing));
            if (unknown.Length > 0)
                problems.Add("Cartas desconhecidas nos pacotes: " + string.Join(",", unknown));
            return problems.ToArray();
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
