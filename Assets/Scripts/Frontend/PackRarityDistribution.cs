using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Regra única de obtenção de raridade dos boosters. Os quatro primeiros
    /// slots usam 70/25/4/1 e o quinto garante R ou superior em 85/13/2.
    /// A distribuição efetiva de uma abertura é 56/37/5,8/1,2.
    /// </summary>
    public static class PackRarityDistribution
    {
        public const int NormalPercent = 70;
        public const int RarePercent = 25;
        public const int SuperRarePercent = 4;
        public const int UltraRarePercent = 1;
        public const int GuaranteedRarePercent = 85;
        public const int GuaranteedSuperRarePercent = 13;
        public const int GuaranteedUltraRarePercent = 2;
        public const int GuaranteedSlotIndex = 4;
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

        public static int WeightForSlot(CardRarity rarity, int slotIndex)
        {
            if (slotIndex != GuaranteedSlotIndex)
                return Weight(rarity);
            return rarity switch
            {
                CardRarity.N => 0,
                CardRarity.R => GuaranteedRarePercent,
                CardRarity.SR => GuaranteedSuperRarePercent,
                CardRarity.UR => GuaranteedUltraRarePercent,
                _ => 0
            };
        }

        public static CardRarity ResolveRollForSlot(int slotIndex, int roll)
        {
            if (slotIndex != GuaranteedSlotIndex)
                return ResolveRoll(roll);
            int normalized = ((roll % TotalPercent) + TotalPercent) %
                TotalPercent;
            if (normalized < GuaranteedRarePercent)
                return CardRarity.R;
            normalized -= GuaranteedRarePercent;
            return normalized < GuaranteedSuperRarePercent
                ? CardRarity.SR
                : CardRarity.UR;
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

        public static CardRarity ResolveAvailableRollForSlot(
            int roll,
            IReadOnlyCollection<CardRarity> available,
            int slotIndex)
        {
            if (available == null || available.Count == 0)
                return CardRarity.Unknown;

            int total = OrderedRarities
                .Where(available.Contains)
                .Sum(rarity => WeightForSlot(rarity, slotIndex));
            // Um pool sem R/SR/UR não pode travar a compra: nesse caso o
            // quinto slot recua de forma explícita para a tabela normal.
            if (total <= 0)
                return ResolveAvailableRoll(roll, available);

            int normalized = ((roll % total) + total) % total;
            foreach (CardRarity rarity in OrderedRarities)
            {
                if (!available.Contains(rarity))
                    continue;
                int weight = WeightForSlot(rarity, slotIndex);
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
