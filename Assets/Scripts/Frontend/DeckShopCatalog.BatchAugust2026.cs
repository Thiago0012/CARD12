using System.Collections.Generic;
using ArcaneDuel.Game;

namespace ArcaneArena.Frontend
{
    public static partial class DeckShopCatalog
    {
        public const string CrimsonPowerforceProductId =
            "crimson-powerforce-637629";
        public const string HiddenArtsOfShadowsProductId =
            "hidden-arts-of-shadows-47456";
        public const string BlackwingsPrideProductId =
            "blackwing-s-pride-681357";
        public const string DragonmaidToOrderX3ProductId =
            "dragonmaid-to-order-x3-705747";
        public const string CyberneticSuccessorProductId =
            "cybernetic-successor-722408";
        public const string RunickProductId =
            "runick-724086";
        public const string ExodiaProductId =
            "exodia-723881";

        private static DeckShopProduct CreateBlueEyesMaxReplacementProduct()
        {
            return Product(
                BlueEyesProductId,
                "Dragão Branco - Blue-Eyes Max",
                "OLHOS AZUIS / RITUAL",
                "Dragões de Olhos Azuis, suporte de Cemitério e a chegada do Dragão MÁX do Caos.",
                "89631139",
                "https://yugipedia.com/wiki/Blue-Eyes_Max",
                0,
                CuratedDeckLists.BlueEyesMaxModifiedMain,
                CuratedDeckLists.BlueEyesMaxModifiedExtra);
        }

        private static DeckShopProduct CreateDarkMagicalBlastReplacementProduct()
        {
            return Product(
                DarkMagicianProductId,
                "Mago Negro - Explosão Mágica",
                "MAGIA NEGRA / CAOS",
                "Mago Negro, Cavaleiros Lendários e Fusões dracônicas para dominar o campo.",
                "46986414",
                "https://ygoprodeck.com/deck/dark-magical-blast-703036",
                2,
                CuratedDeckLists.DarkMagicalBlastMain,
                CuratedDeckLists.DarkMagicalBlastExtra);
        }

        private static IReadOnlyList<DeckShopProduct>
            CreateBatchAugust2026Products()
        {
            return new[]
            {
                Product(
                    CrimsonPowerforceProductId,
                    "Força do Poder Carmesim",
                    "RESSONADOR / DEMÔNIO",
                    "Ressonadores aceleram uma sequência de Dragões Vermelhos Arquidemônios.",
                    "70902743",
                    "https://ygoprodeck.com/deck/crimson-powerforce-637629",
                    1,
                    CuratedDeckLists.CrimsonPowerforceMain,
                    CuratedDeckLists.CrimsonPowerforceExtra),
                Product(
                    HiddenArtsOfShadowsProductId,
                    "Artes Ocultas das Sombras",
                    "NINJA / ARTE NINJITSU",
                    "Ninjas alternam posições de batalha e invocam suas Fusões pelas Artes Ninjitsu.",
                    "11825276",
                    "https://ygoprodeck.com/deck/hidden-arts-of-shadows-549728",
                    2,
                    CuratedDeckLists.HiddenArtsOfShadowsMain,
                    CuratedDeckLists.HiddenArtsOfShadowsExtra),
                Product(
                    BlackwingsPrideProductId,
                    "Orgulho dos Asas Negras",
                    "ASA NEGRA / SINCRO",
                    "Asas Negras formam rapidamente monstros Sincro e pressionam com dano de efeito.",
                    "73218989",
                    "https://ygoprodeck.com/deck/blackwing-s-pride-681357",
                    0,
                    CuratedDeckLists.BlackwingsPrideMain,
                    CuratedDeckLists.BlackwingsPrideExtra),
                Product(
                    DragonmaidToOrderX3ProductId,
                    "Dragonmaid Sob Encomenda x3",
                    "DRAGONMAID",
                    "Dragonmaids alternam suas formas, reciclam recursos e protegem o campo com Sheou.",
                    "24799107",
                    "https://ygoprodeck.com/deck/dragonmaid-to-order-x3-705747",
                    1,
                    CuratedDeckLists.DragonmaidToOrderX3Main,
                    CuratedDeckLists.DragonmaidToOrderX3Extra),
                Product(
                    CyberneticSuccessorProductId,
                    "Sucessor Cibernético",
                    "CIBERNÉTICO / CIBERSOMBRIO",
                    "Dragões Cibernéticos e Cibersombrios se unem para chegar ao Dragão Final Cibersombrio.",
                    "37542782",
                    "https://ygoprodeck.com/deck/cybernetic-successor-722408",
                    2,
                    CuratedDeckLists.CyberneticSuccessorMain,
                    CuratedDeckLists.CyberneticSuccessorExtra),
                Product(
                    RunickProductId,
                    "Runick",
                    "RUNICK / CONTROLE",
                    "Magias Rápidas Runick controlam o duelo e invocam monstros diretamente do Deck Adicional.",
                    "92107604",
                    "https://ygoprodeck.com/deck/runick-724086",
                    0,
                    CuratedDeckLists.RunickMain,
                    CuratedDeckLists.RunickExtra),
                Product(
                    ExodiaProductId,
                    "Exodia",
                    "EXODIA / MILÊNIO",
                    "As cinco partes do Proibido e o suporte do Milênio conduzem à vitória de Exodia.",
                    "33396948",
                    "https://ygoprodeck.com/deck/exodia-723881",
                    1,
                    CuratedDeckLists.ExodiaMain,
                    CuratedDeckLists.ExodiaExtra)
            };
        }
    }
}
