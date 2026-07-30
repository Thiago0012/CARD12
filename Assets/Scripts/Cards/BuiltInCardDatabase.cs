using System;
using System.Collections.Generic;
using System.IO;

namespace ArcaneArena.Cards
{
    public enum CardAttribute
    {
        None = 0,
        Dark = 1,
        Earth = 2,
        Fire = 3,
        Light = 4,
        Water = 5,
        Wind = 6,
        Divine = 7
    }

    public enum CardEffectId
    {
        None = 0,
        HaneHane = 1,
        FinalFlame = 2,
        Hinotama = 3,
        Fissure = 4,
        Mountain = 5,
        Wasteland = 6,
        GravekeepersAmbusher = 7,
        GravekeepersChief = 8,
        GravekeepersHeadman = 9,
        FalseTrap = 10,
        DarkEnergy = 11,
        ArmedNinja = 12,
        CannonSoldier = 13,
        WhiteMagicalHat = 14,
        DarkElf = 15,
        StopDefense = 16,
        Sangan = 17,
        MukaMuka = 18,
        DragonCaptureJar = 19,
        ParalyzingPotion = 20,
        RemoveTrap = 21,
        ShieldAndSword = 22,
        ManEaterBug = 23,
        PreventDefense = 24,
        StimPack = 25,
        BistroButcher = 26,
        CrassClown = 27,
        JiraiGumo = 28,
        FollowWind = 29,
        SwordOfDeepSeated = 30
    }

    public sealed class CardMetadata
    {
        public string OfficialId { get; }
        public string LocalizedName { get; }
        public CardCategory Category { get; }
        public MonsterFrameKind MonsterFrame { get; }
        public string TypeName { get; }
        public string RaceName { get; }
        public CardAttribute Attribute { get; }
        public int Level { get; }
        public int Attack { get; }
        public int Defense { get; }
        public string Description { get; }
        public CardEffectId EffectId { get; }

        public CardMetadata(
            string officialId,
            string localizedName,
            CardCategory category,
            MonsterFrameKind monsterFrame,
            string typeName,
            string raceName,
            CardAttribute attribute,
            int level,
            int attack,
            int defense,
            string description,
            CardEffectId effectId)
        {
            OfficialId = officialId;
            LocalizedName = localizedName;
            Category = category;
            MonsterFrame = monsterFrame;
            TypeName = typeName;
            RaceName = raceName;
            Attribute = attribute;
            Level = level;
            Attack = attack;
            Defense = defense;
            Description = description;
            EffectId = effectId;
        }
    }

    public static class BuiltInCardDatabase
    {
        private static readonly Dictionary<string, CardMetadata> Cards =
            new Dictionary<string, CardMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["1784619"] = Monster("01784619", "Uraby", MonsterFrameKind.Normal, "Monstro Normal",
                    "Dinossauro", CardAttribute.Earth, 4, 1500, 800,
                    "Rápido como o vento, este dinossauro dilacera os inimigos com suas garras afiadas."),
                ["2863439"] = Monster("02863439", "Reflexo Demoníaco nº 2", MonsterFrameKind.Normal, "Monstro Normal",
                    "Besta Alada", CardAttribute.Light, 4, 1100, 1400,
                    "Uma pássaro-besta que invoca reforços usando um espelho de mão."),
                ["7089711"] = Monster("07089711", "Hane-Hane", MonsterFrameKind.Effect, "Monstro de Efeito de Virar",
                    "Besta", CardAttribute.Earth, 2, 450, 500,
                    "VIRE: Devolva para a mão do seu dono 1 Card de Monstro no campo à sua escolha.",
                    CardEffectId.HaneHane),
                ["10202894"] = Monster("10202894", "Pássaro da Crista Vermelha", MonsterFrameKind.Normal, "Monstro Normal",
                    "Besta Alada", CardAttribute.Wind, 4, 1550, 1200,
                    "Este monstro mergulha dos céus e ataca com uma chuva de facas guardadas em suas asas."),
                ["13039848"] = Monster("13039848", "Soldado de Pedra Gigante", MonsterFrameKind.Normal, "Monstro Normal",
                    "Rocha", CardAttribute.Earth, 3, 1300, 2000,
                    "Um guerreiro gigante feito de pedra. Um soco desta criatura faz com que a terra estremeça."),
                ["15025844"] = Monster("15025844", "Elfa Mística", MonsterFrameKind.Normal, "Monstro Normal",
                    "Mago", CardAttribute.Light, 4, 800, 2000,
                    "Uma elfa delicada com pouco poder de ataque, mas com uma incrível defesa proveniente de poderes místicos."),
                ["17881964"] = Monster("17881964", "Dragão do Fogo Negro", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Dragão", CardAttribute.Dark, 4, 1500, 1250,
                    "\"Relva-Fogo\" + \"Pequeno Dragão\""),
                ["22910685"] = Monster("22910685", "Rei Fantasma Verde", MonsterFrameKind.Normal, "Monstro Normal",
                    "Planta", CardAttribute.Earth, 3, 500, 1600,
                    "Este jovem rei das florestas vive num mundo verde cheio de árvores e vida selvagem."),
                ["23424603"] = Spell("23424603", "Terra Árida", "Magia de Campo",
                    "Todos os monstros Dinossauro, Zumbi e Rocha no campo ganham 200 de ATK/DEF.",
                    CardEffectId.Wasteland),
                ["28279543"] = Monster("28279543", "Maldição do Dragão", MonsterFrameKind.Normal, "Monstro Normal",
                    "Dragão", CardAttribute.Dark, 5, 2000, 1500,
                    "Um dragão perverso que se apoia nas forças das trevas para executar um poderoso ataque."),
                ["32452818"] = Monster("32452818", "Castor Guerreiro", MonsterFrameKind.Normal, "Monstro Normal",
                    "Besta-Guerreira", CardAttribute.Earth, 4, 1200, 1500,
                    "O que falta a esta criatura em tamanho, ela compensa em defesa quando combate na pradaria."),
                ["36304921"] = Monster("36304921", "Fantasma Sarcástico", MonsterFrameKind.Normal, "Monstro Normal",
                    "Demônio", CardAttribute.Dark, 4, 1400, 1300,
                    "Vestido com um smoking preto como a noite, esta criatura preside à morte."),
                ["37313348"] = Monster("37313348", "Tigre Tartaruga", MonsterFrameKind.Normal, "Monstro Normal",
                    "Aqua", CardAttribute.Water, 4, 1000, 1500,
                    "Um tigre protegido por uma carapaça, que usa suas presas afiadas como lâminas para atacar."),
                ["37421579"] = Monster("37421579", "Charubin, o Cavaleiro do Fogo", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Pyro", CardAttribute.Fire, 3, 1100, 800,
                    "\"Monstro Ovo\" + \"Alma Hinotama\""),
                ["39111158"] = Monster("39111158", "Dragão de Três Chifres", MonsterFrameKind.Normal, "Monstro Normal",
                    "Dragão", CardAttribute.Dark, 8, 2850, 2350,
                    "Um dragão indigno com três chifres afiados brotando da sua cabeça."),
                ["45042329"] = Monster("45042329", "Besta Eletro-Transmissora", MonsterFrameKind.Normal, "Monstro Normal",
                    "Trovão", CardAttribute.Earth, 4, 1200, 1300,
                    "Esta criatura ataca com ondas eletromagnéticas."),
                ["46130346"] = Spell("46130346", "Hinotama", "Magia Normal",
                    "Cause 500 de dano ao seu oponente.", CardEffectId.Hinotama),
                ["50913601"] = Spell("50913601", "Montanha", "Magia de Campo",
                    "Todos os monstros Dragão, Besta Alada e Trovão no campo ganham 200 de ATK/DEF.",
                    CardEffectId.Mountain),
                ["54541900"] = Monster("54541900", "Guerreiro Karbonala", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Guerreiro", CardAttribute.Earth, 4, 1500, 1200,
                    "\"Guerreiro M nº 1\" + \"Guerreiro M nº 2\""),
                ["66788016"] = Spell("66788016", "Fissura", "Magia Normal",
                    "Destrua o 1 monstro com a face para cima que seu oponente controla com o menor ATK (em caso de empate, você escolhe).",
                    CardEffectId.Fissure),
                ["70681994"] = Monster("70681994", "Dragoness, o Cavaleiro Perverso", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Guerreiro", CardAttribute.Wind, 3, 1200, 900,
                    "\"Armaill\" + \"Dragão Escudo de Um Só Olho\""),
                ["71407486"] = Monster("71407486", "Fireyarou", MonsterFrameKind.Normal, "Monstro Normal",
                    "Pyro", CardAttribute.Fire, 4, 1300, 1000,
                    "Uma criatura maléfica envolvida em chamas que ataca os inimigos com fogo intenso."),
                ["73134082"] = Spell("73134082", "Chama Final", "Magia Normal",
                    "Cause 600 de dano aos Pontos de Vida do seu oponente.", CardEffectId.FinalFlame),
                ["77827521"] = Monster("77827521", "Julgamento Infernal", MonsterFrameKind.Normal, "Monstro Normal",
                    "Demônio", CardAttribute.Dark, 4, 1300, 900,
                    "Este demônio julga inimigos que estão trancados em caixões."),
                ["80770678"] = Monster("80770678", "Espírito da Harpa", MonsterFrameKind.Normal, "Monstro Normal",
                    "Fada", CardAttribute.Light, 4, 800, 2000,
                    "Um espírito que acalma a alma com a música da sua harpa celestial."),
                ["89631139"] = Monster("89631139", "Dragão Branco de Olhos Azuis", MonsterFrameKind.Normal, "Monstro Normal",
                    "Dragão", CardAttribute.Light, 8, 3000, 2500,
                    "Este dragão lendário é uma poderosa máquina de destruição. Praticamente invencível, muito poucos enfrentaram esta magnífica criatura e viveram para contar a história."),
                ["95952802"] = Monster("95952802", "Lobo Flor", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Besta", CardAttribute.Earth, 5, 1800, 1400,
                    "\"O Presa de Prata\" + \"Espinhos do Mundo das Trevas\""),
                ["1184620"] = Monster("01184620", "Kojikocy", MonsterFrameKind.Normal, "Monstro Normal",
                    "Guerreiro", CardAttribute.Earth, 4, 1500, 1200,
                    "Um caçador de homens com braços poderosos, capazes de esmagar grandes rochas."),
                ["2504891"] = Monster("02504891", "Cavaleiro Caveira", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Mago", CardAttribute.Dark, 7, 2650, 2250,
                    "\"Sabedoria Corrompida\" + \"Cérebro Ancião\""),
                ["3027001"] = Trap("03027001", "Armadilha Falsa", "Armadilha Normal",
                    "Quando um card ou efeito for ativado que destruiria um ou mais Cards de Armadilha que você controla: em vez disso, destrua este card.",
                    CardEffectId.FalseTrap),
                ["4614116"] = Spell("04614116", "Energia das Trevas", "Magia de Equipamento",
                    "Um monstro do Tipo Demônio equipado com este card ganha 300 de ATK/DEF.",
                    CardEffectId.DarkEnergy),
                ["9076207"] = Monster("09076207", "Ninja Armado", MonsterFrameKind.Effect, "Monstro de Efeito de Virar",
                    "Guerreiro", CardAttribute.Earth, 1, 300, 300,
                    "VIRE: Escolha 1 Card de Magia no campo; destrua-o. Se o alvo estiver com a face para baixo, revele-o e destrua-o apenas se ele for um Card de Magia; caso contrário, coloque-o novamente com a face para baixo. O card revelado não é ativado.",
                    CardEffectId.ArmedNinja),
                ["9293977"] = Monster("09293977", "Dragão de Metal", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Máquina", CardAttribute.Wind, 6, 1850, 1700,
                    "\"Ogre de Aço da Gruta nº 1\" + \"Dragão Inferior\""),
                ["9653271"] = Monster("09653271", "Ataque Kaminari", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Trovão", CardAttribute.Wind, 5, 1900, 1400,
                    "\"Ocubeam\" + \"Mega Esfera de Trovão\""),
                ["11384280"] = Monster("11384280", "Soldado Canhão", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Máquina", CardAttribute.Dark, 4, 1400, 1300,
                    "Ofereça 1 monstro que você controla como Tributo; cause 500 de dano ao seu oponente.",
                    CardEffectId.CannonSoldier),
                ["11901678"] = Monster("11901678", "Dragão Caveira Negro", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Dragão", CardAttribute.Dark, 9, 3200, 2500,
                    "\"Caveira Invocada\" + \"Dragão Negro de Olhos Vermelhos\"\n\n(Este card deve ser sempre considerado como um card \"Arquidemônio\".)"),
                ["15150365"] = Monster("15150365", "Chapéu Mágico Branco", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Mago", CardAttribute.Light, 3, 1000, 700,
                    "Quando este card causar dano de batalha ao seu oponente: ele descarta 1 card aleatório.",
                    CardEffectId.WhiteMagicalHat),
                ["15237615"] = Monster("15237615", "Juíza Imperatriz", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Guerreiro", CardAttribute.Earth, 6, 2100, 1700,
                    "\"Sósia da Rainha\" + \"Hibikime\""),
                ["19066538"] = Monster("19066538", "Cobra do Mar Rugidora", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Aqua", CardAttribute.Water, 6, 2100, 1800,
                    "\"Lâmpada Mística\" + \"Hyosube\""),
                ["21417692"] = Monster("21417692", "Elfa Negra", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Mago", CardAttribute.Dark, 4, 2000, 800,
                    "Você deve pagar 1000 PV para este card declarar um ataque.",
                    CardEffectId.DarkElf),
                ["23771716"] = Monster("23771716", "Peixe de 7 Cores", MonsterFrameKind.Normal, "Monstro Normal",
                    "Peixe", CardAttribute.Water, 4, 1800, 800,
                    "Um raro peixe arco-íris que nunca foi apanhado por homens mortais."),
                ["24611934"] = Monster("24611934", "Ryu-Kishin Potencializado", MonsterFrameKind.Normal, "Monstro Normal",
                    "Demônio", CardAttribute.Dark, 4, 1600, 1200,
                    "Uma gárgula melhorada pelos poderes das trevas. Garras bem afiadas a tornam uma forte adversária."),
                ["25880422"] = Spell("25880422", "Bloquear Ataque", "Magia Normal",
                    "Escolha 1 monstro com a face para cima em Posição de Ataque que seu oponente controla; coloque-o com a face para cima em Posição de Defesa.",
                    CardEffectId.StopDefense),
                ["26202165"] = Monster("26202165", "Sangan", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Demônio", CardAttribute.Dark, 3, 1000, 600,
                    "Se este card for enviado do campo para o Cemitério: adicione 1 monstro com 1500 ou menos de ATK do seu Deck à sua mão, mas você não pode ativar cards ou efeitos de cards com esse nome pelo resto deste turno. Você só pode usar este efeito de \"Sangan\" uma vez por turno.",
                    CardEffectId.Sangan),
                ["28546905"] = Monster("28546905", "Ilusionista Sem Rosto", MonsterFrameKind.Normal, "Monstro Normal",
                    "Mago", CardAttribute.Dark, 5, 1200, 2200,
                    "Manipula os ataques inimigos com o poder da ilusão."),
                ["28593363"] = Monster("28593363", "Tubarão das Profundezas do Mar", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Peixe", CardAttribute.Water, 5, 1900, 1600,
                    "\"Habitante das Profundezas\" + \"Tongyo\""),
                ["41396436"] = Monster("41396436", "Coroa de Asas Azuis", MonsterFrameKind.Normal, "Monstro Normal",
                    "Besta Alada", CardAttribute.Wind, 4, 1600, 1200,
                    "Com cabelo em forma de coroa e corpo envolto por chamas branco-azuladas, este pássaro é uma visão formidável."),
                ["46657337"] = Monster("46657337", "Muka Muka", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Rocha", CardAttribute.Earth, 2, 600, 300,
                    "Ganha 300 de ATK/DEF para cada card na sua mão.",
                    CardEffectId.MukaMuka),
                ["46986414"] = Monster("46986414", "Mago Negro", MonsterFrameKind.Normal, "Monstro Normal",
                    "Mago", CardAttribute.Dark, 7, 2500, 2100,
                    "O mago definitivo em termos de ataque e defesa."),
                ["50045299"] = Trap("50045299", "Jarro de Captura de Dragões", "Armadilha Contínua",
                    "Enquanto este card estiver com a face para cima no campo, coloque todos os monstros Dragão no campo em Posição de Defesa.",
                    CardEffectId.DragonCaptureJar),
                ["50152549"] = Spell("50152549", "Poção Paralisante", "Magia de Equipamento",
                    "Equipe apenas a um monstro que não seja Máquina. Ele não pode atacar.",
                    CardEffectId.ParalyzingPotion),
                ["51482758"] = Spell("51482758", "Remover Armadilha", "Magia Normal",
                    "Escolha 1 Card de Armadilha com a face para cima no campo; destrua-o.",
                    CardEffectId.RemoveTrap),
                ["51828629"] = Monster("51828629", "Giltia, o Cavaleiro D.", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Guerreiro", CardAttribute.Light, 5, 1850, 1500,
                    "\"Guardião do Labirinto\" + \"Protetora do Trono\""),
                ["52097679"] = Spell("52097679", "Escudo & Espada", "Magia Normal",
                    "Até o final deste turno, troque o ATK original pela DEF original de todos os monstros com a face para cima atualmente no campo.",
                    CardEffectId.ShieldAndSword),
                ["54652250"] = Monster("54652250", "Inseto Devorador de Homens", MonsterFrameKind.Effect, "Monstro de Efeito de Virar",
                    "Inseto", CardAttribute.Earth, 2, 450, 600,
                    "VIRE: Escolha 1 monstro no campo; destrua-o.",
                    CardEffectId.ManEaterBug),
                ["56907389"] = Monster("56907389", "Rei Músico", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Mago", CardAttribute.Light, 5, 1750, 1500,
                    "\"Bruxa da Floresta Negra\" + \"Lady da Fé\""),
                ["63102017"] = Spell("63102017", "Impedir a Defesa", "Magia Normal",
                    "Escolha 1 monstro no campo do seu oponente; coloque-o em Posição de Ataque. Se ele estiver com a face para baixo, vire-o com a face para cima e ative imediatamente seus efeitos.",
                    CardEffectId.PreventDefense),
                ["66889139"] = Monster("66889139", "Gaia, o Matador de Dragões", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Dragão", CardAttribute.Wind, 7, 2600, 2100,
                    "\"Gaia, o Cavaleiro Impetuoso\" + \"Maldição do Dragão\""),
                ["71107816"] = Monster("71107816", "O Carniceiro do Bar", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Demônio", CardAttribute.Dark, 4, 1800, 1000,
                    "Quando este card causar dano de batalha ao seu oponente: ele compra 2 cards.",
                    CardEffectId.BistroButcher),
                ["83225447"] = Spell("83225447", "Poção de Estimulação", "Magia de Equipamento",
                    "O monstro equipado ganha 700 de ATK. Durante cada uma de suas Fases de Apoio, ele perde 200 de ATK.",
                    CardEffectId.StimPack),
                ["89112729"] = Monster("89112729", "Ciber Saurus", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Máquina", CardAttribute.Earth, 5, 1800, 1400,
                    "\"Malabarista Explosivo\" + \"Rei Rex de Duas Cabeças\"\n\n(Este card não é considerado como um card \"Ciber\".)"),
                ["93889755"] = Monster("93889755", "Palhaço Grosseiro", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Demônio", CardAttribute.Dark, 4, 1350, 1400,
                    "Quando este card mudar da Posição de Defesa para a Posição de Ataque: devolva 1 monstro no campo do seu oponente para a mão.",
                    CardEffectId.CrassClown),
                ["94773007"] = Monster("94773007", "Jirai Gumo", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Inseto", CardAttribute.Earth, 4, 2200, 100,
                    "Quando este card declarar um ataque: lance uma moeda e escolha cara ou coroa. Se você errar, perca metade dos seus PV.",
                    CardEffectId.JiraiGumo),
                ["94905343"] = Monster("94905343", "Centauro Furioso", MonsterFrameKind.Fusion, "Monstro de Fusão",
                    "Besta-Guerreira", CardAttribute.Earth, 6, 2000, 1700,
                    "\"Touro Guerreiro\" + \"Centauro Místico\""),
                ["98252586"] = Spell("98252586", "Vento Seguidor", "Magia de Equipamento",
                    "Um monstro Besta Alada equipado com este card ganha 300 de ATK/DEF.",
                    CardEffectId.FollowWind),
                ["98495314"] = Spell("98495314", "Espada dos Enraizados", "Magia de Equipamento",
                    "O monstro equipado ganha 500 de ATK/DEF. Se este card for enviado para o Cemitério: coloque-o no topo do seu Deck.",
                    CardEffectId.SwordOfDeepSeated),
                ["gravekeeper_s_ambusher"] = Monster("22134079", "Emboscador do Coveiro", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Mago", CardAttribute.Dark, 4, 1700, 0,
                    "Quando este card for virado com a face para cima: você pode escolher 1 card no Cemitério do seu oponente; coloque o alvo no fundo do Deck dele. Se este card for enviado do campo para o Cemitério depois de ser virado com a face para cima: você pode escolher 1 card \"Necro-Vale\" no seu Cemitério; adicione o alvo à sua mão. Estes efeitos não são afetados por \"Necro-Vale\".",
                    CardEffectId.GravekeepersAmbusher),
                ["gravekeeper_s_chief"] = Monster("62473983", "Chefe do Coveiro", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Mago", CardAttribute.Dark, 5, 1900, 1200,
                    "Você só pode controlar 1 \"Chefe do Coveiro\" com a face para cima. Seu Cemitério não é afetado por \"Necro-Vale\". Quando este card for Invocado por Invocação-Tributo: você pode escolher 1 monstro \"do Coveiro\" no seu Cemitério; Invoque o alvo por Invocação-Especial.",
                    CardEffectId.GravekeepersChief),
                ["gravekeeper_s_headman"] = Monster("21663205", "Mandante do Coveiro", MonsterFrameKind.Effect, "Monstro de Efeito",
                    "Mago", CardAttribute.Dark, 3, 500, 1500,
                    "Se este card for Invocado: você pode escolher 1 monstro \"do Coveiro\" de Nível 4 no seu Cemitério; Invoque-o por Invocação-Especial em Posição de Ataque ou com a face para baixo em Posição de Defesa. Você só pode usar este efeito de \"Mandante do Coveiro\" uma vez por turno. Este efeito não é afetado por \"Necro-Vale\".",
                    CardEffectId.GravekeepersHeadman)
            };

        public static bool TryGetByAssetName(string assetName, out CardMetadata metadata)
        {
            var key = NormalizeAssetKey(assetName);
            if (Cards.TryGetValue(key, out metadata))
                return true;
            if (CorePrototypeCardDatabase.TryGetByAssetName(
                    key,
                    out metadata))
            {
                return true;
            }
            if (DeckShopCardDatabase.TryGetByAssetName(
                    key,
                    out metadata))
            {
                return true;
            }

            foreach (var pair in Cards)
            {
                if (key.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    metadata = pair.Value;
                    return true;
                }
            }

            metadata = null;
            return false;
        }

        public static string AttributeLabel(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.Dark:
                    return "TREVAS";
                case CardAttribute.Earth:
                    return "TERRA";
                case CardAttribute.Fire:
                    return "FOGO";
                case CardAttribute.Light:
                    return "LUZ";
                case CardAttribute.Water:
                    return "ÁGUA";
                case CardAttribute.Wind:
                    return "VENTO";
                case CardAttribute.Divine:
                    return "DIVINO";
                default:
                    return "—";
            }
        }

        private static CardMetadata Monster(
            string id,
            string name,
            MonsterFrameKind frame,
            string type,
            string race,
            CardAttribute attribute,
            int level,
            int attack,
            int defense,
            string description,
            CardEffectId effectId = CardEffectId.None)
        {
            return new CardMetadata(
                id, name, CardCategory.Monster, frame, type, race,
                attribute, level, attack, defense, description, effectId);
        }

        private static CardMetadata Spell(
            string id,
            string name,
            string type,
            string description,
            CardEffectId effectId = CardEffectId.None)
        {
            return new CardMetadata(
                id, name, CardCategory.Spell, MonsterFrameKind.None, type,
                string.Empty, CardAttribute.None, 0, -1, -1, description, effectId);
        }

        private static CardMetadata Trap(
            string id,
            string name,
            string type,
            string description,
            CardEffectId effectId = CardEffectId.None)
        {
            return new CardMetadata(
                id, name, CardCategory.Trap, MonsterFrameKind.None, type,
                string.Empty, CardAttribute.None, 0, -1, -1, description, effectId);
        }

        private static string NormalizeAssetKey(string assetName)
        {
            var key = Path.GetFileNameWithoutExtension(assetName ?? string.Empty)
                .Replace(" 0", string.Empty)
                .Trim()
                .ToLowerInvariant();
            if (long.TryParse(key, out var numeric))
                return numeric.ToString();
            return key;
        }
    }
}
