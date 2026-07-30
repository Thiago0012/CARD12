using System;
using System.Collections.Generic;
using System.IO;

namespace ArcaneArena.Cards
{
    /// <summary>
    /// Pacote pequeno e auditável de cartas escolhidas para exercitar o Core.
    /// As chaves são os nomes numéricos dos arquivos; OfficialId preserva o
    /// passcode oficial com oito dígitos.
    /// </summary>
    public static class CorePrototypeCardDatabase
    {
        private static readonly Dictionary<string, CardMetadata> Cards =
            new Dictionary<string, CardMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                // Magias
                ["55144522"] = Spell(
                    "55144522", "Pote da Ganância", "Magia Normal",
                    "Compre 2 cards."),
                ["83764719"] = Spell(
                    "83764719", "Reviver Monstro", "Magia Normal",
                    "Escolha 1 monstro em qualquer Cemitério; Invoque-o por Invocação-Especial."),
                ["53129443"] = Spell(
                    "53129443", "Buraco Negro", "Magia Normal",
                    "Destrua todos os monstros no campo."),
                ["5318639"] = Spell(
                    "05318639", "Tufão Espacial Místico", "Magia Rápida",
                    "Escolha 1 Magia/Armadilha no campo; destrua o alvo."),
                ["14087893"] = Spell(
                    "14087893", "Livro da Lua", "Magia Rápida",
                    "Escolha 1 monstro com a face para cima no campo; coloque o alvo com a face para baixo em Posição de Defesa."),
                ["56747793"] = Spell(
                    "56747793", "Juntos Resistiremos", "Magia de Equipamento",
                    "O monstro equipado ganha 800 de ATK/DEF para cada monstro com a face para cima que você controla."),
                ["73628505"] = Spell(
                    "73628505", "Transformação Campal", "Magia Normal",
                    "Adicione 1 Magia de Campo do seu Deck à sua mão."),
                ["24094653"] = Spell(
                    "24094653", "Polimerização", "Magia Normal",
                    "Invoque por Invocação-Fusão 1 Monstro de Fusão do seu Deck Adicional, usando monstros da sua mão ou do campo como Matéria de Fusão."),
                ["46052429"] = Spell(
                    "46052429", "Arte Ritual Avançada", "Magia de Ritual",
                    "Este card é usado para a Invocação-Ritual de qualquer 1 Monstro de Ritual. Você também deve enviar Monstros Normais do seu Deck para o Cemitério cuja soma dos Níveis seja igual ao Nível desse Monstro de Ritual."),
                ["295517"] = Spell(
                    "00295517", "Um Oceano Lendário", "Magia de Campo",
                    "(O nome deste card deve ser sempre considerado como \"Umi\".)\nTodos os monstros de ÁGUA no campo ganham 200 de ATK/DEF. Reduza em 1 o Nível de todos os monstros de ÁGUA na mão e no campo dos dois duelistas."),

                // Armadilhas
                ["44095762"] = Trap(
                    "44095762", "Força do Espelho", "Armadilha Normal",
                    "Quando um monstro do oponente declarar um ataque: destrua todos os monstros em Posição de Ataque do seu oponente."),
                ["53582587"] = Trap(
                    "53582587", "Tributo Torrencial", "Armadilha Normal",
                    "Quando um ou mais monstros forem Invocados: destrua todos os monstros no campo."),
                ["62279055"] = Trap(
                    "62279055", "Cilindro Mágico", "Armadilha Normal",
                    "Quando um monstro do oponente declarar um ataque: escolha o monstro atacante; negue o ataque e, se isso acontecer, cause dano ao seu oponente igual ao ATK do monstro."),
                ["41420027"] = Trap(
                    "41420027", "Julgamento Solene", "Armadilha de Resposta",
                    "Quando um ou mais monstros seriam Invocados OU um Card de Magia/Armadilha for ativado: pague metade dos seus PV; negue a Invocação ou ativação e, se isso acontecer, destrua esse card."),
                ["97077563"] = Trap(
                    "97077563", "Chamado dos Assombrados", "Armadilha Contínua",
                    "Ative este card ao escolher 1 monstro no seu Cemitério; Invoque o alvo por Invocação-Especial em Posição de Ataque. Quando este card deixar o campo, destrua esse monstro. Quando esse monstro for destruído, destrua este card."),
                ["29401950"] = Trap(
                    "29401950", "Buraco Armadilha Sem Fundo", "Armadilha Normal",
                    "Quando seu oponente Invocar um ou mais monstros com 1500 ou mais de ATK: destrua esse(s) monstro(s) com 1500 ou mais de ATK e, se isso acontecer, bana-o(s)."),
                ["94192409"] = Trap(
                    "94192409", "Aparelho de Evacuação Obrigatória", "Armadilha Normal",
                    "Escolha 1 monstro no campo; devolva o alvo para a mão."),
                ["82732705"] = Trap(
                    "82732705", "Drenar Habilidades", "Armadilha Contínua",
                    "Ative este card ao pagar 1000 PV. Negue os efeitos de todos os monstros com a face para cima enquanto eles estiverem com a face para cima no campo (mas seus efeitos ainda podem ser ativados)."),
                ["4206964"] = Trap(
                    "04206964", "Buraco Armadilha", "Armadilha Normal",
                    "Quando seu oponente Invocar por Invocação-Normal ou Virar 1 monstro com 1000 ou mais de ATK: escolha o monstro; destrua o alvo."),
                ["49010598"] = Trap(
                    "49010598", "Cólera Divina", "Armadilha de Resposta",
                    "Quando um efeito de monstro for ativado: descarte 1 card; negue a ativação e, se isso acontecer, destrua esse monstro."),

                // Monstros de Efeito
                ["71413901"] = Monster(
                    "71413901", "Breaker, o Guerreiro Mágico",
                    "Monstro de Efeito", "Mago", CardAttribute.Dark,
                    4, 1600, 1000,
                    "Se este card for Invocado por Invocação-Normal: coloque 1 Marcador de Magia nele (máx. 1). Ganha 300 de ATK para cada Marcador de Magia nele. Você pode remover 1 Marcador de Magia deste card e, depois, escolha 1 Magia/Armadilha no campo; destrua o alvo."),
                ["7572887"] = Monster(
                    "07572887", "D.D. Lady Guerreira",
                    "Monstro de Efeito", "Guerreiro", CardAttribute.Light,
                    4, 1500, 1600,
                    "Depois do cálculo de dano, quando este card batalhar um monstro do oponente: você pode banir esse monstro e, além disso, bana este card."),
                ["70095154"] = Monster(
                    "70095154", "Dragão Cibernético",
                    "Monstro de Efeito", "Máquina", CardAttribute.Light,
                    5, 2100, 1600,
                    "Se somente seu oponente controlar um monstro, você pode Invocar este card por Invocação-Especial (da sua mão)."),
                ["78658564"] = Monster(
                    "78658564", "Força de Ataque dos Goblins",
                    "Monstro de Efeito", "Guerreiro", CardAttribute.Earth,
                    4, 2300, 0,
                    "Se este card atacar, ele é colocado em Posição de Defesa no final da Fase de Batalha e sua posição de batalha não pode ser mudada até a Fase Final do seu próximo turno."),
                ["31305911"] = Monster(
                    "31305911", "Marshmallon",
                    "Monstro de Efeito", "Fada", CardAttribute.Light,
                    3, 300, 500,
                    "Não pode ser destruído em batalha. Depois do cálculo de dano, se este card foi atacado e estava com a face para baixo no começo da Etapa de Dano: o duelista atacante sofre 1000 de dano."),
                ["77585513"] = Monster(
                    "77585513", "Jinzo",
                    "Monstro de Efeito", "Máquina", CardAttribute.Dark,
                    6, 2400, 1500,
                    "Os Cards de Armadilha, bem como seus efeitos no campo, não podem ser ativados. Negue todos os efeitos de Armadilha no campo."),
                ["40640057"] = Monster(
                    "40640057", "Kuriboh",
                    "Monstro de Efeito", "Demônio", CardAttribute.Dark,
                    1, 300, 200,
                    "Durante o cálculo de dano, se um monstro do seu oponente atacar (Efeito Rápido): você pode descartar este card; você não sofre dano de batalha dessa batalha."),
                ["97268402"] = Monster(
                    "97268402", "Ocultador de Efeitos",
                    "Monstro Regulador de Efeito", "Mago", CardAttribute.Light,
                    1, 0, 0,
                    "Durante a Fase Principal do seu oponente (Efeito Rápido): você pode enviar este card da sua mão para o Cemitério e, depois, escolha 1 Monstro de Efeito que seu oponente controla; até o final deste turno, negue os efeitos desse monstro com a face para cima que seu oponente controla."),
                ["74131780"] = Monster(
                    "74131780", "Força Exilada",
                    "Monstro de Efeito", "Guerreiro", CardAttribute.Earth,
                    4, 1000, 1000,
                    "Você pode oferecer este card como Tributo para escolher 1 monstro no campo; destrua o alvo."),
                ["93920745"] = Monster(
                    "93920745", "Soldado Pinguim",
                    "Monstro de Efeito de Virar", "Aqua", CardAttribute.Water,
                    2, 750, 500,
                    "VIRE: Você pode escolher até 2 monstros no campo; devolva os alvos para a mão.")
            };

        public static bool TryGetByAssetName(
            string assetName,
            out CardMetadata metadata)
        {
            return Cards.TryGetValue(
                NormalizeAssetKey(assetName),
                out metadata);
        }

        private static CardMetadata Monster(
            string id,
            string name,
            string type,
            string race,
            CardAttribute attribute,
            int level,
            int attack,
            int defense,
            string description)
        {
            return new CardMetadata(
                id,
                name,
                CardCategory.Monster,
                MonsterFrameKind.Effect,
                type,
                race,
                attribute,
                level,
                attack,
                defense,
                description,
                CardEffectId.None);
        }

        private static CardMetadata Spell(
            string id,
            string name,
            string type,
            string description)
        {
            return new CardMetadata(
                id,
                name,
                CardCategory.Spell,
                MonsterFrameKind.None,
                type,
                string.Empty,
                CardAttribute.None,
                0,
                -1,
                -1,
                description,
                CardEffectId.None);
        }

        private static CardMetadata Trap(
            string id,
            string name,
            string type,
            string description)
        {
            return new CardMetadata(
                id,
                name,
                CardCategory.Trap,
                MonsterFrameKind.None,
                type,
                string.Empty,
                CardAttribute.None,
                0,
                -1,
                -1,
                description,
                CardEffectId.None);
        }

        private static string NormalizeAssetKey(string assetName)
        {
            var key = Path
                .GetFileNameWithoutExtension(assetName ?? string.Empty)
                .Trim();
            return long.TryParse(key, out var numeric)
                ? numeric.ToString()
                : key.ToLowerInvariant();
        }
    }
}
