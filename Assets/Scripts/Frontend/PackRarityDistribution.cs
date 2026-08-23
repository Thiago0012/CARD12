using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Regra única de obtenção de raridade dos boosters.
    /// O intervalo inteiro [0, 99] evita arredondamento e permite validar
    /// exatamente os 55/25/12/8 pontos percentuais solicitados.
    /// </summary>
    public static class PackRarityDistribution
    {
        public const int NormalPercent = 55;
        public const int RarePercent = 25;
        public const int SuperRarePercent = 12;
        public const int UltraRarePercent = 8;
        public const int TotalPercent = 100;

        public static CardRarity ResolveRoll(int roll)
        {
            int normalized = ((roll % TotalPercent) + TotalPercent) %
                TotalPercent;
            if (normalized < NormalPercent)
                return CardRarity.N;
            normalized -= NormalPercent;
            if (normalized < RarePercent)
                return CardRarity.R;
            normalized -= RarePercent;
            return normalized < SuperRarePercent
                ? CardRarity.SR
                : CardRarity.UR;
        }

        public static int Weight(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.N => NormalPercent,
                CardRarity.R => RarePercent,
                CardRarity.SR => SuperRarePercent,
                CardRarity.UR => UltraRarePercent,
                _ => 0
            };
        }

        /// <summary>
        /// Raridade efetiva usada por todo o fluxo de boosters. O catálogo
        /// legado contém algumas cartas de anime/custom sem metadado de
        /// raridade; elas já participavam do pool Normal na compra, portanto
        /// a mesma regra precisa ser usada pela animação e pelos selos.
        /// </summary>
        public static CardRarity ResolveCardRarity(CardCatalogEntry entry)
        {
            return entry != null && CardRarityCatalog.IsValid(entry.Rarity)
                ? entry.Rarity
                : CardRarity.N;
        }

        /// <summary>
        /// Alguns catálogos temáticos pequenos não possuem as quatro
        /// raridades. Neles, somente os pesos disponíveis são normalizados;
        /// nenhuma carta externa ao pacote é introduzida silenciosamente.
        /// </summary>
        public static CardRarity ResolveAvailableRoll(
            int roll,
            IReadOnlyCollection<CardRarity> available)
        {
            if (available == null || available.Count == 0)
                return CardRarity.Unknown;

            int total = 0;
            foreach (CardRarity rarity in OrderedRarities)
            {
                if (available.Contains(rarity))
                    total += Weight(rarity);
            }
            if (total <= 0)
                return CardRarity.Unknown;

            int normalized = ((roll % total) + total) % total;
            foreach (CardRarity rarity in OrderedRarities)
            {
                if (!available.Contains(rarity))
                    continue;
                int weight = Weight(rarity);
                if (normalized < weight)
                    return rarity;
                normalized -= weight;
            }
            return CardRarity.Unknown;
        }

        public static readonly CardRarity[] OrderedRarities =
        {
            CardRarity.N,
            CardRarity.R,
            CardRarity.SR,
            CardRarity.UR
        };
    }
}
