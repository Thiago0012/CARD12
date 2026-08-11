using UnityEngine;

namespace ArcaneDuel.Game.Competitive
{
    /// <summary>
    /// Associação explícita entre o domínio e os oito assets aprovados.
    /// Nenhuma busca por nome exibido ou texto localizado participa do mapa.
    /// </summary>
    public static class RankBadgeCatalog
    {
        private static readonly Sprite[] Cache = new Sprite[8];

        public static Sprite Get(RankTier tier)
        {
            int index = Mathf.Clamp((int)tier, 0, Cache.Length - 1);
            if (Cache[index] == null)
                Cache[index] = Resources.Load<Sprite>(ResourcePath((RankTier)index));
            return Cache[index];
        }

        private static string ResourcePath(RankTier tier)
        {
            return tier switch
            {
                RankTier.Wood => "Frontend/Ranked/Badges/Wood",
                RankTier.Stone => "Frontend/Ranked/Badges/Stone",
                RankTier.Iron => "Frontend/Ranked/Badges/Iron",
                RankTier.Silver => "Frontend/Ranked/Badges/Silver",
                RankTier.Gold => "Frontend/Ranked/Badges/Gold",
                RankTier.Platinum => "Frontend/Ranked/Badges/Platinum",
                RankTier.Diamond => "Frontend/Ranked/Badges/Diamond",
                RankTier.GrandMaster => "Frontend/Ranked/Badges/GrandMaster",
                _ => "Frontend/Ranked/Badges/Wood"
            };
        }
    }
}
