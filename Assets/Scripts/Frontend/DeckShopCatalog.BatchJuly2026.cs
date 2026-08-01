using System.Collections.Generic;
using ArcaneDuel.Game;

namespace ArcaneArena.Frontend
{
    public static partial class DeckShopCatalog
    {
        public const string AzaminaIllusionsProductId =
            "n-r-azamina-illusions-723781";
        public const string PlantLinkProductId =
            "plant-link-722230";
        public const string NoobsGaiaProductId =
            "noobs-gaia-263232";
        public const string SummonBansProductId =
            "summon-bans-723769";
        public const string StarWarriorLevel5XyzProductId =
            "star-warrior-level-5-xyz-deck-master-duel-2-722498";
        public const string AssaultModeGoodStuffProductId =
            "n-r-assault-mode-good-stuff-724215";
        public const string Dragones2ProductId =
            "dragones-2-724339";
        public const string FemaleReptileProductId =
            "female-reptile-deck-724288";
        public const string ReturnToSenderProductId =
            "return-to-sender-723161";

        private static IReadOnlyList<DeckShopProduct>
            CreateBatchJuly2026Products()
        {
            return new[]
            {
                Product(
                    AzaminaIllusionsProductId,
                    "Ilusões Azamina N/R",
                    "AZAMINA / ILUSÃO",
                    "Ilusões N/R, Magias Azamina e opções variadas no Deck Adicional.",
                    "65033975",
                    "https://ygoprodeck.com/deck/n-r-azamina-illusions-723781",
                    0,
                    CuratedDeckLists.AzaminaIllusionsMain,
                    CuratedDeckLists.AzaminaIllusionsExtra),
                Product(
                    PlantLinkProductId,
                    "Plantas Link",
                    "PLANTA / LINK",
                    "Solarsementes, Aromas e escalada de Invocações-Link com monstros Planta.",
                    "27520594",
                    "https://ygoprodeck.com/deck/plant-link-722230",
                    1,
                    CuratedDeckLists.PlantLinkMain,
                    CuratedDeckLists.PlantLinkExtra),
                Product(
                    NoobsGaiaProductId,
                    "Gaia para Iniciantes",
                    "GAIA",
                    "Uma rota direta para Gaia e suas Fusões com suporte de Dragão.",
                    "34130561",
                    "https://ygoprodeck.com/deck/noobs-gaia-263232",
                    2,
                    CuratedDeckLists.NoobsGaiaMain,
                    CuratedDeckLists.NoobsGaiaExtra),
                Product(
                    SummonBansProductId,
                    "Bloqueio de Invocações",
                    "MONARCA / CONTROLE",
                    "Monarcas, Tributos e efeitos que restringem as Invocações do adversário.",
                    "87288189",
                    "https://ygoprodeck.com/deck/summon-bans-723769",
                    0,
                    CuratedDeckLists.SummonBansMain,
                    CuratedDeckLists.SummonBansExtra),
                Product(
                    StarWarriorLevel5XyzProductId,
                    "Guerreiros Estelares Nível 5 Xyz",
                    "GUERREIRO / XYZ",
                    "Guerreiros de Nível 5 que acessam Fusões e monstros Xyz de Classe 5.",
                    "96220350",
                    "https://ygoprodeck.com/deck/star-warrior-level-5-xyz-deck-master-duel-2-722498",
                    1,
                    CuratedDeckLists.StarWarriorLevel5XyzMain,
                    CuratedDeckLists.StarWarriorLevel5XyzExtra),
                Product(
                    AssaultModeGoodStuffProductId,
                    "Modo de Assalto N/R",
                    "MODO DE ASSALTO",
                    "Reguladores Psíquicos e suporte N/R para acessar monstros Sincro e Modo de Assalto.",
                    "74644400",
                    "https://ygoprodeck.com/deck/n-r-assault-mode-good-stuff-724215",
                    2,
                    CuratedDeckLists.AssaultModeGoodStuffMain,
                    CuratedDeckLists.AssaultModeGoodStuffExtra),
                Product(
                    Dragones2ProductId,
                    "Dragões 2",
                    "DRAGÃO",
                    "Dragões de diferentes atributos com linhas Rokket, Olhos Vermelhos e Olhos Azuis.",
                    "88264978",
                    "https://ygoprodeck.com/deck/dragones-2-724339",
                    0,
                    CuratedDeckLists.Dragones2Main,
                    CuratedDeckLists.Dragones2Extra),
                Product(
                    FemaleReptileProductId,
                    "Deck Reptiliano Feminino",
                    "REPTILIANA",
                    "Monstros Reptiliana reduzem o ATK adversário e preparam Vaskii para dominar o campo.",
                    "16886617",
                    "https://ygoprodeck.com/deck/female-reptile-deck-724288",
                    1,
                    CuratedDeckLists.FemaleReptileMain,
                    CuratedDeckLists.FemaleReptileExtra),
                Product(
                    ReturnToSenderProductId,
                    "Devolução ao Remetente",
                    "CONTROLE / RETORNO",
                    "Controle de campo que devolve cartas e transforma a posição do duelo.",
                    "94160895",
                    "https://ygoprodeck.com/deck/return-to-sender-723161",
                    2,
                    CuratedDeckLists.ReturnToSenderMain,
                    CuratedDeckLists.ReturnToSenderExtra)
            };
        }

        private static DeckShopProduct Product(
            string productId,
            string displayName,
            string archetypeLabel,
            string description,
            string coverCardId,
            string sourceUrl,
            int caseTheme,
            uint[] mainDeck,
            uint[] extraDeck)
        {
            return new DeckShopProduct(
                productId,
                displayName,
                archetypeLabel,
                description,
                coverCardId,
                sourceUrl,
                caseTheme,
                CuratedDeckLists.AsCardIds(mainDeck),
                CuratedDeckLists.AsCardIds(extraDeck));
        }
    }
}
