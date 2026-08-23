using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    public enum StoryRelicRarity
    {
        Common,
        Magic,
        Rare,
        Unique
    }

    public enum StoryRelicUseMode
    {
        PassiveRun,
        PassiveDuel,
        ActiveDuel,
        ConsumableDuel,
        ActiveMap,
        ConsumableMap
    }

    public enum StoryRelicTrigger
    {
        None,
        OnAcquire,
        DuelSetup,
        DuelStart,
        TurnStart,
        DrawPhase,
        HandChanged,
        BeforeDamage,
        BeforeBattleDamage,
        LethalDamage,
        DuelVictory,
        DuelDefeat,
        NodeReward,
        MerchantOpened,
        CardRewardOpened,
        MapOpened
    }

    public enum StoryRelicCapacityResult
    {
        CanAcquireDirectly,
        AlreadyOwned,
        MustReplaceSameTier,
        MustReplaceAnyLegalRelic,
        InvalidDefinition
    }

    [CreateAssetMenu(
        menuName = "Arcane Duel/Story Roguelite/Relic Definition")]
    public sealed class StoryRelicDefinition : ScriptableObject
    {
        public string relicId;
        public string displayName;
        public string shortEffect;
        [TextArea(3, 9)] public string description;
        public Sprite icon;
        public StoryRelicRarity rarity;
        public StoryRelicUseMode useMode;
        public StoryRelicTrigger[] triggers = Array.Empty<StoryRelicTrigger>();
        public int initialCharges;
        public bool resetChargesEachDuel;
        public bool destroyedOnUse;
        public bool requiresConfirmation;
        public string effectHandlerId;
        public float[] numericParams = Array.Empty<float>();
        public string[] stringParams = Array.Empty<string>();
        public bool runtimeEnabled = true;
        [TextArea(2, 5)] public string disabledReason;

        public bool IsAvailable => runtimeEnabled &&
            !string.IsNullOrWhiteSpace(relicId) &&
            !string.IsNullOrWhiteSpace(effectHandlerId);
    }

    [CreateAssetMenu(
        menuName = "Arcane Duel/Story Roguelite/Relic Catalog")]
    public sealed class StoryRelicCatalog : ScriptableObject
    {
        public const string ResourcePath =
            "StoryRoguelite/Generated/StoryRelicCatalog";
        public List<StoryRelicDefinition> definitions = new();

        public IReadOnlyList<StoryRelicDefinition> All => definitions?
            .Where(definition => definition != null)
            .ToArray() ?? Array.Empty<StoryRelicDefinition>();
    }

    [CreateAssetMenu(
        menuName = "Arcane Duel/Story Roguelite/Relic Drop Profile")]
    public sealed class StoryRelicDropProfile : ScriptableObject
    {
        public const string ResourcePath =
            "StoryRoguelite/Generated/StoryRelicDropProfile";

        [Header("Normal: none, common, magic, rare, unique")]
        public float[] normal = { 71f, 14f, 10f, 4f, 1f };
        [Header("Elite: none, common, magic, rare, unique")]
        public float[] elite = { 45f, 25f, 16f, 10f, 4f };
        [Header("Final: none, common, magic, rare, unique")]
        public float[] finalArena = { 25f, 25f, 25f, 20f, 5f };
        [Header("Boss: common, magic, rare, unique")]
        public float[] boss = { 15f, 30f, 47.5f, 7.5f };
        [Header("Shrine: common, magic, rare, unique")]
        public float[] shrine = { 45f, 35f, 18f, 2f };
        public bool enableTreasureRelics;
        public bool enableForbiddenAltarRelics;

        public StoryRelicRarity? ResolveEncounterRarity(
            RogueliteNodeType type,
            double roll)
        {
            float[] weights = type switch
            {
                RogueliteNodeType.EliteDuel => elite,
                RogueliteNodeType.FinalDuelArena => finalArena,
                RogueliteNodeType.Boss => boss,
                _ => normal
            };
            bool hasNone = type != RogueliteNodeType.Boss;
            return Resolve(weights, roll, hasNone);
        }

        public StoryRelicRarity ResolveShrineRarity(double roll) =>
            Resolve(shrine, roll, false) ?? StoryRelicRarity.Common;

        public static StoryRelicRarity? Resolve(
            IReadOnlyList<float> weights,
            double roll,
            bool includesNone)
        {
            if (weights == null || weights.Count == 0)
                return includesNone ? null : StoryRelicRarity.Common;
            double target = Math.Max(0d, Math.Min(0.999999999d, roll)) *
                            weights.Sum(value => Math.Max(0f, value));
            double cumulative = 0d;
            for (int index = 0; index < weights.Count; index++)
            {
                cumulative += Math.Max(0f, weights[index]);
                if (target >= cumulative) continue;
                if (includesNone && index == 0) return null;
                int rarityIndex = includesNone ? index - 1 : index;
                return (StoryRelicRarity)Mathf.Clamp(rarityIndex, 0, 3);
            }
            return StoryRelicRarity.Unique;
        }

        public static StoryRelicDropProfile CreateDefaults()
        {
            StoryRelicDropProfile profile = CreateInstance<
                StoryRelicDropProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }
    }

    [Serializable]
    public sealed class StoryRelicRuntimeState
    {
        public string relicId;
        public int acquiredAct;
        public string acquiredNodeId;
        public long acquisitionOrdinal;
        public int chargesRemaining;
        public int usesThisDuel;
        public int usesThisAct;
        public bool consumed;
    }

    [Serializable]
    public sealed class StoryPendingRelicReward
    {
        public string operationId;
        public string sourceNodeId;
        public string sourceType;
        public string title;
        public List<string> relicIds = new();
        public bool shrineChoice;
        public bool advanceActAfterResolution;
        public bool completeNodeAfterResolution;
    }

    [Serializable]
    public sealed class StoryPendingRelicReplacement
    {
        public string operationId;
        public string incomingRelicId;
        public List<string> eligibleRelicIds = new();
    }

    public static class StoryRelicSpecification
    {
        public const int TotalLimit = 10;
        public static readonly string[] LegacyIds =
        {
            "merchant-pouch", "fortune-echo", "reinforced-seal",
            "arcane-archive", "marked-map", "duelist-compass"
        };

        private static readonly StoryRelicDefinition[] Definitions = Build();
        public static IReadOnlyList<StoryRelicDefinition> All => Definitions;

        public static int TierLimit(StoryRelicRarity rarity) => rarity switch
        {
            StoryRelicRarity.Common => 5,
            StoryRelicRarity.Magic => 4,
            StoryRelicRarity.Rare => 3,
            StoryRelicRarity.Unique => 1,
            _ => 0
        };

        private static StoryRelicDefinition[] Build()
        {
            const string duelHook =
                "A ponte autoritativa de compra/dano do duelo ainda não " +
                "expõe este gatilho com segurança. Recurso desabilitado.";
            const string advancedForge =
                "A mecânica avançada da Oficina/Forja ainda está " +
                "simplificada; o ID e o texto ficam preservados.";
            return new[]
            {
                R("merchant-pouch", "Credencial KaibaCorp",
                    "10% de desconto nos mercadores.",
                    "Reduz em 10% os custos em Fragmentos no Card Merchant.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "merchant-discount"),
                R("marked-map", "Mapa do Reino dos Duelistas",
                    "Revela a categoria dos nós Mystery.",
                    "Mostra glifo e rótulo reais dos pontos misteriosos sem mudar a seed.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "marked-map"),
                R("duelist-compass", "Radar do Disco de Duelo",
                    "+2 Fragmentos em vitórias difíceis.",
                    "Concede 2 Fragmentos extras em Elite, Arena Final e Boss.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "hard-win-fragments", numbers: new[] { 2f }),
                R("ancient-wood-charm", "Amuleto de Kuriboh",
                    "+500 LP inicial.",
                    "Adiciona 500 LP ao jogador antes de multiplicadores finais.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveDuel,
                    "starting-lp-add", numbers: new[] { 500f }),
                R("duelist-token", "Ficha da Cidade de Batalha",
                    "+15% de Fragmentos de vitória.",
                    "Aumenta Fragmentos de vitória em 15%, arredondando para baixo.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "victory-fragment-percent", numbers: new[] { 15f }),
                R("messenger-feather", "Pena da Harpia",
                    "Compra futura ao terminar com até 2 cartas.",
                    "Uma vez por duelo, agenda uma compra para a próxima Draw Phase.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveDuel,
                    "low-hand-delayed-draw", false, duelHook),
                R("cracked-shield", "Fragmento da Força Espelho",
                    "Reduz o primeiro dano de batalha em 500.",
                    "O primeiro dano de batalha recebido em cada duelo é reduzido em 500.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveDuel,
                    "first-battle-damage-reduction", false, duelHook),
                R("apprentice-scroll", "Pergaminho do Mago Negro",
                    "Prioriza sinergia em uma opção de carta.",
                    "Uma opção de recompensa recebe afinidade com as tags dominantes do deck.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "reward-tag-affinity", false,
                    "O catálogo atual não possui tags de sinergia normalizadas para todas as cartas."),
                R("return-stone", "Pedra do Monstro Renascido",
                    "Cancela uma transição ainda não resolvida.",
                    "Uma vez por run, volta ao nó anterior antes de produzir efeito.",
                    StoryRelicRarity.Common, StoryRelicUseMode.ConsumableMap,
                    "cancel-pending-transition", false,
                    "A confirmação de retorno precisa de uma etapa de UI dedicada."),
                R("scavenger-ring", "Anel do Caçador de Raros",
                    "+2 Fragmentos em Treasure Vault.",
                    "Acrescenta 2 Fragmentos à recompensa normal do cofre.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "vault-fragments", numbers: new[] { 2f }),
                R("duelist-lens", "Visor do Disco de Duelo",
                    "Mostra o estilo principal do deck do NPC.",
                    "A confirmação de combate informa a família do deck adversário.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "encounter-deck-family"),
                R("whetstone", "Lâmina do Buster Blader",
                    "Efeito reservado para a Forja avançada.",
                    "A primeira Forja do ato receberá uma opção adicional quando a mecânica avançada estiver ativa.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "advanced-forge-extra-option", false, advancedForge),
                R("dimensional-pouch", "Cápsula de Outra Dimensão",
                    "Primeiro Card Pack do ato mostra 6 cartas.",
                    "Aumenta de 5 para 6 as opções do primeiro pacote de cada ato.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveRun,
                    "first-pack-extra-card"),
                R("survivor-totem", "Totem de Kuriboh Alado",
                    "+2 Fragmentos por vitória com LP crítico.",
                    "Vencer com até 25% do LP inicial concede 2 Fragmentos.",
                    StoryRelicRarity.Common, StoryRelicUseMode.PassiveDuel,
                    "low-lp-victory", false,
                    "O resultado atual do core não devolve o LP final ao gerenciador da run."),

                R("fortune-echo", "Eco do Pote da Ganância",
                    "Primeiro reroll do ato é gratuito.",
                    "A primeira atualização de ofertas do Merchant por ato custa zero.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveRun,
                    "merchant-free-reroll"),
                R("second-draw-crystal", "Cristal da Compra do Destino",
                    "Compra adicional na Draw Phase.",
                    "Uma vez por duelo, compra uma carta após a compra normal.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.ActiveDuel,
                    "active-draw-one", false, duelHook, 1, true),
                R("intuition-mask", "Máscara das Trevas",
                    "Exibe o risco das escolhas de evento.",
                    "Mostra risco Baixo, Médio ou Alto em eventos aleatórios.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveRun,
                    "event-risk-label"),
                R("quartz-heart", "Coração do Dragão Branco",
                    "+1.500 LP inicial.",
                    "Adiciona 1.500 LP ao jogador antes de multiplicadores.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveDuel,
                    "starting-lp-add", numbers: new[] { 1500f }),
                R("duelist-clock", "Relógio da Cidade de Batalha",
                    "Preserva uma carga de compra adicional.",
                    "Marca um turno para que a primeira compra de outra relíquia não gaste carga.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.ActiveDuel,
                    "preserve-draw-relic-charge", false, duelHook, 1, true),
                R("fortune-idol", "Ídolo do Pote da Dualidade",
                    "+10 pontos de peso em recompensas especiais.",
                    "Aumenta o peso de tier superior em Vault e recompensas especiais.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveRun,
                    "special-reward-tier-weight", false,
                    "As recompensas ainda não possuem tiers de qualidade independentes."),
                R("archivist-eye", "Olho do Milênio",
                    "Marca uma carta recusada para o futuro.",
                    "Mantém uma carta recusada com peso maior em recompensas futuras.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.ActiveMap,
                    "mark-refused-card", false,
                    "A tela de recompensa ainda não oferece a ação de marcar uma carta recusada."),
                R("first-impact-shield", "Escudo de Kuriboh",
                    "Zera o primeiro dano de batalha.",
                    "O primeiro ataque que causaria dano de batalha causa zero ao LP.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveDuel,
                    "first-battle-damage-zero", false, duelHook),
                R("emerald-hourglass", "Ampulheta do Mago do Tempo",
                    "Recupera cooldown de outra relíquia.",
                    "Reativa no próximo turno uma relíquia de cooldown usada.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.ActiveDuel,
                    "restore-relic-cooldown", false, duelHook, 1, true),
                R("alchemist-vault", "Cofre de KaibaCorp",
                    "Efeito reservado para Workshop avançado.",
                    "Concederá 2 Fragmentos ao remover carta permanentemente.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveRun,
                    "advanced-workshop-fragments", false, advancedForge),
                R("hunter-mark", "Marca do Caçador de Raros",
                    "+1 opção após vencer Elite.",
                    "Recompensas de Elite recebem uma opção adicional, até o teto.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveRun,
                    "elite-reward-extra-option"),
                R("persistence-seal", "Selo do Faraó",
                    "+1.000 LP no duelo após a primeira derrota do ato.",
                    "A primeira derrota não-Boss do ato prepara 1.000 LP extras para o próximo duelo.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveRun,
                    "next-duel-lp-after-loss"),
                R("echo-stone", "Pedra da Corrente de Duelo",
                    "Copia uma relíquia ativável elegível.",
                    "Copia uma vez o efeito de uma ativável não-Única e não-consumível.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.ActiveDuel,
                    "copy-active-relic", false, duelHook, 1, true),
                R("ruins-lantern", "Lanterna do Templo do Faraó",
                    "Spell Ruins mostra 4 opções.",
                    "Adiciona uma opção de Magia/Armadilha às Ruínas.",
                    StoryRelicRarity.Magic, StoryRelicUseMode.PassiveRun,
                    "spell-ruins-extra-card"),

                R("reinforced-seal", "Selo do Reino das Sombras",
                    "+1 Selo máximo e restaura 1.",
                    "Aumenta o máximo de Selos para 4 e restaura um ao adquirir.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveRun,
                    "reinforced-seal", triggers: new[] { StoryRelicTrigger.OnAcquire }),
                R("arcane-archive", "Arquivo de KaibaCorp",
                    "+1 opção de carta pós-duelo.",
                    "Recompensas de duelo não-chefe recebem uma opção adicional.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveRun,
                    "duel-reward-extra-option"),
                R("ruby-heart", "Coração do Dragão Negro de Olhos Vermelhos",
                    "+3.000 LP inicial.",
                    "Adiciona 3.000 LP ao jogador antes de multiplicadores.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveDuel,
                    "starting-lp-add", numbers: new[] { 3000f }),
                R("aegis-mirror", "Égide da Força Espelho",
                    "Reduz pela metade o primeiro dano de 2.000+.",
                    "Uma vez por duelo, reduz em 50% um evento de dano de pelo menos 2.000.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveDuel,
                    "large-damage-half", false, duelHook),
                R("controlled-greed-orb", "Orbe do Pote da Ganância",
                    "Compra 2 cartas e é destruído.",
                    "Consumível de duelo que compra duas cartas pela rotina oficial.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.ConsumableDuel,
                    "consume-draw-two", false, duelHook, 1, false, true, true),
                R("conqueror-crown", "Coroa do Rei dos Jogos",
                    "Recompensa especial por Elite perfeita.",
                    "Após Elite elegível, oferece carta de tier alto ou relíquia adicional.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveRun,
                    "elite-perfect-special-reward", false,
                    "O rastreamento de perda de Selo desde a recuperação ainda não existe."),
                R("phoenix-mark", "Marca da Fênix de Rá",
                    "Evita uma falha não-Boss e é consumida.",
                    "Quando uma derrota não-Boss zeraria os Selos, mantém 1 e destrói a relíquia.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.ConsumableMap,
                    "prevent-run-failure", true, null, 1, false, true),
                R("book-of-paths", "Livro do Reino dos Duelistas",
                    "Revela todos os Mystery visíveis uma vez por ato.",
                    "Ativável de mapa que revela os nós misteriosos já gerados.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.ActiveMap,
                    "reveal-act-mysteries", false,
                    "A sidebar ainda não possui painel de ativáveis de mapa.", 1),
                R("transmutation-chalice", "Cálice da Polimerização",
                    "Efeito reservado para a Forja avançada.",
                    "A Forja avançada mostrará quatro substituições coerentes.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveRun,
                    "advanced-forge-transmutation", false, advancedForge),
                R("eye-of-fate", "Olho de Pegasus",
                    "Revela dados completos de dois combates por ato.",
                    "Antes da confirmação, mostra NPC, IA, família e LP.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.ActiveMap,
                    "inspect-encounter", false,
                    "A confirmação de rota ainda não possui seleção de carga ativável.", 2),
                R("fallen-king-armor", "Armadura do Soldado do Lustro Negro",
                    "+2.000 LP, mas bloqueia cura de Selo na Fonte.",
                    "Adiciona 2.000 LP e impede Healing Spring de restaurar Selos.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveRun,
                    "lp-for-healing-lock", numbers: new[] { 2000f }),
                R("duelist-pact", "Juramento de Orichalcos",
                    "Elite e Arena Final recebem +2.000 LP.",
                    "Aumenta risco de encontros difíceis em troca de recompensas melhores futuras.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveRun,
                    "hard-enemy-lp-add", numbers: new[] { 2000f }),
                R("last-chance-ring", "Anel do Milênio",
                    "Sobrevive a dano letal com 1 LP.",
                    "Uma vez por duelo, dano letal deixa o jogador com 1 LP.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveDuel,
                    "survive-lethal-damage", false, duelHook),
                R("specialization-prism", "Prisma do Herói Elemental",
                    "Especializa recompensas em uma tag escolhida.",
                    "Ao adquirir, fixa uma tag de sinergia para a run.",
                    StoryRelicRarity.Rare, StoryRelicUseMode.PassiveRun,
                    "choose-reward-tag", false,
                    "Faltam catálogo de tags e tela de escolha irreversível."),

                R("titan-heart", "Coração de Exodia",
                    "Dobra o LP inicial final.",
                    "Multiplica por dois o LP após bônus e penalidades aditivos.",
                    StoryRelicRarity.Unique, StoryRelicUseMode.PassiveDuel,
                    "starting-lp-multiply", numbers: new[] { 2f }),
                R("chronos-eye", "Olho do Mago do Tempo",
                    "Desfaz uma decisão reversível de relíquia.",
                    "Reverte apenas a última decisão de relíquia segura no turno.",
                    StoryRelicRarity.Unique, StoryRelicUseMode.ActiveDuel,
                    "undo-relic-command", false, duelHook, 1, true),
                R("forbidden-grimoire", "Grimório do Mago Negro",
                    "Compra 2 e melhora a próxima recompensa.",
                    "Consumível que compra duas cartas e prepara uma opção de tier alto.",
                    StoryRelicRarity.Unique, StoryRelicUseMode.ConsumableDuel,
                    "consume-draw-two-high-tier", false, duelHook, 1, false, true, true),
                R("eternity-shield", "Escudo de Obelisco",
                    "Anula o primeiro evento de dano.",
                    "O primeiro dano de qualquer origem em cada duelo é anulado.",
                    StoryRelicRarity.Unique, StoryRelicUseMode.PassiveDuel,
                    "first-damage-zero", false, duelHook),
                R("archmage-crown", "Coroa do Mago Negro",
                    "+1 opção em recompensas e Merchant.",
                    "Acrescenta uma opção, respeitando teto 6; Forja aguarda modo avançado.",
                    StoryRelicRarity.Unique, StoryRelicUseMode.PassiveRun,
                    "global-extra-option"),
                R("immortal-phoenix", "Fênix do Dragão Alado de Rá",
                    "Repete uma derrota não-Boss sem perder Selo.",
                    "Uma vez por run, preserva encontro, NPC, deck e IA após derrota autoritativa.",
                    StoryRelicRarity.Unique, StoryRelicUseMode.ConsumableMap,
                    "retry-authoritative-loss", true, null, 1, false, true),
                R("singularity-throne", "Trono do Faraó",
                    "Escolhe 2 cartas de Elite; relíquia Rara após Arena Final.",
                    "Amplia a recompensa de Elite e garante rolagem Rara na Arena Final.",
                    StoryRelicRarity.Unique, StoryRelicUseMode.PassiveRun,
                    "elite-double-final-rare", false,
                    "A recompensa dupla exige uma fila autoritativa de duas " +
                    "relíquias, ainda ausente no gerenciador da run."),
                R("pharaoh-fragment", "Enigma do Milênio",
                    "Ascensão com compra e proteção no turno.",
                    "Compra uma carta, anula o primeiro dano e preserva uma carga até a End Phase.",
                    StoryRelicRarity.Unique, StoryRelicUseMode.ActiveDuel,
                    "pharaoh-ascension", false, duelHook, 1, true)
            };
        }

        private static StoryRelicDefinition R(
            string id,
            string name,
            string shortEffect,
            string description,
            StoryRelicRarity rarity,
            StoryRelicUseMode mode,
            string handler,
            bool enabled = true,
            string disabledReason = null,
            int charges = 0,
            bool resetEachDuel = false,
            bool destroyedOnUse = false,
            bool requiresConfirmation = false,
            float[] numbers = null,
            StoryRelicTrigger[] triggers = null)
        {
            StoryRelicDefinition definition = ScriptableObject.CreateInstance<
                StoryRelicDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.relicId = id;
            definition.displayName = name;
            definition.shortEffect = shortEffect;
            definition.description = description;
            definition.rarity = rarity;
            definition.useMode = mode;
            definition.effectHandlerId = handler;
            definition.runtimeEnabled = enabled;
            definition.disabledReason = disabledReason ?? string.Empty;
            definition.initialCharges = charges;
            definition.resetChargesEachDuel = resetEachDuel;
            definition.destroyedOnUse = destroyedOnUse;
            definition.requiresConfirmation = requiresConfirmation;
            definition.numericParams = numbers ?? Array.Empty<float>();
            definition.triggers = triggers ?? Array.Empty<StoryRelicTrigger>();
            return definition;
        }
    }

    public static class StoryRelicLibrary
    {
        private static StoryRelicCatalog catalog;
        private static StoryRelicDropProfile dropProfile;

        public static IReadOnlyList<StoryRelicDefinition> All
        {
            get
            {
                catalog ??= Resources.Load<StoryRelicCatalog>(
                    StoryRelicCatalog.ResourcePath);
                return catalog != null && catalog.All.Count == 50
                    ? catalog.All
                    : StoryRelicSpecification.All;
            }
        }

        public static StoryRelicDropProfile DropProfile
        {
            get
            {
                dropProfile ??= Resources.Load<StoryRelicDropProfile>(
                    StoryRelicDropProfile.ResourcePath);
                return dropProfile ??= StoryRelicDropProfile.CreateDefaults();
            }
        }

        public static StoryRelicDefinition Resolve(string relicId) =>
            All.FirstOrDefault(definition => string.Equals(
                definition.relicId, relicId, StringComparison.Ordinal)) ??
            Legacy(relicId);

        public static void ClearCache()
        {
            catalog = null;
            dropProfile = null;
        }

        private static StoryRelicDefinition Legacy(string relicId)
        {
            StoryRelicDefinition definition = ScriptableObject.CreateInstance<
                StoryRelicDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.relicId = relicId ?? string.Empty;
            definition.displayName = "Relíquia Legada";
            definition.shortEffect = "Efeito preservado de uma versão anterior.";
            definition.description = "O ID foi mantido no save, mas não existe no catálogo atual.";
            definition.effectHandlerId = "legacy-preserved";
            definition.runtimeEnabled = false;
            definition.disabledReason = "Definição desconhecida preservada por compatibilidade.";
            return definition;
        }
    }

    public static class StoryRelicService
    {
        public static IReadOnlyList<StoryRelicRuntimeState> Active(
            StoryRunSave save) => save?.relicStates?
            .Where(state => state != null && !state.consumed)
            .ToArray() ?? Array.Empty<StoryRelicRuntimeState>();

        public static bool Has(StoryRunSave save, string relicId) => Active(save)
            .Any(state => string.Equals(
                state.relicId, relicId, StringComparison.Ordinal));

        public static void MigrateLegacyArtifacts(StoryRunSave save)
        {
            if (save == null) return;
            save.relicStates ??= new List<StoryRelicRuntimeState>();
            save.artifacts ??= new List<string>();
            long ordinal = save.relicStates.Count == 0
                ? 0
                : save.relicStates.Max(state => state?.acquisitionOrdinal ?? 0);
            foreach (string relicId in save.artifacts.Where(
                         id => !string.IsNullOrWhiteSpace(id)).ToArray())
            {
                if (save.relicStates.Any(state => state != null &&
                        string.Equals(state.relicId, relicId,
                            StringComparison.Ordinal)))
                    continue;
                StoryRelicDefinition definition = StoryRelicLibrary.Resolve(
                    relicId);
                save.relicStates.Add(new StoryRelicRuntimeState
                {
                    relicId = relicId,
                    acquiredAct = Math.Max(1, save.actIndex),
                    acquiredNodeId = save.currentNodeId ?? string.Empty,
                    acquisitionOrdinal = ++ordinal,
                    chargesRemaining = Math.Max(0, definition.initialCharges)
                });
            }
            save.relicSchemaVersion = Math.Max(1, save.relicSchemaVersion);
            save.schemaVersion = Math.Max(3, save.schemaVersion);
        }

        public static StoryRelicCapacityResult CheckCapacity(
            StoryRunSave save,
            StoryRelicDefinition incoming)
        {
            if (save == null || incoming == null ||
                string.IsNullOrWhiteSpace(incoming.relicId))
                return StoryRelicCapacityResult.InvalidDefinition;
            if (Has(save, incoming.relicId))
                return StoryRelicCapacityResult.AlreadyOwned;
            IReadOnlyList<StoryRelicRuntimeState> active = Active(save);
            int tierCount = active.Count(state =>
                StoryRelicLibrary.Resolve(state.relicId).rarity ==
                incoming.rarity);
            if (active.Count < StoryRelicSpecification.TotalLimit &&
                tierCount < StoryRelicSpecification.TierLimit(incoming.rarity))
                return StoryRelicCapacityResult.CanAcquireDirectly;
            if (tierCount >= StoryRelicSpecification.TierLimit(incoming.rarity))
                return StoryRelicCapacityResult.MustReplaceSameTier;
            return StoryRelicCapacityResult.MustReplaceAnyLegalRelic;
        }

        public static List<string> ReplacementCandidates(
            StoryRunSave save,
            StoryRelicDefinition incoming)
        {
            StoryRelicCapacityResult capacity = CheckCapacity(save, incoming);
            IEnumerable<StoryRelicRuntimeState> states = Active(save);
            if (capacity == StoryRelicCapacityResult.MustReplaceSameTier)
            {
                states = states.Where(state =>
                    StoryRelicLibrary.Resolve(state.relicId).rarity ==
                    incoming.rarity);
            }
            return states.Select(state => state.relicId).Distinct().ToList();
        }

        public static bool Acquire(
            StoryRunSave save,
            StoryRelicDefinition definition,
            bool suppressAcquireEffects = false)
        {
            if (CheckCapacity(save, definition) !=
                StoryRelicCapacityResult.CanAcquireDirectly)
                return false;
            long ordinal = save.relicStates.Count == 0
                ? 1
                : save.relicStates.Max(state =>
                    state?.acquisitionOrdinal ?? 0) + 1;
            save.relicStates.Add(new StoryRelicRuntimeState
            {
                relicId = definition.relicId,
                acquiredAct = Math.Max(1, save.actIndex),
                acquiredNodeId = save.currentNodeId ?? string.Empty,
                acquisitionOrdinal = ordinal,
                chargesRemaining = Math.Max(0, definition.initialCharges)
            });
            AddUnique(save.artifacts, definition.relicId);
            if (!suppressAcquireEffects && string.Equals(
                    definition.relicId, "reinforced-seal",
                    StringComparison.Ordinal))
            {
                save.seals = Math.Min(MaxSeals(save), save.seals + 1);
            }
            return true;
        }

        public static bool Replace(
            StoryRunSave save,
            string outgoingRelicId,
            StoryRelicDefinition incoming)
        {
            List<string> candidates = ReplacementCandidates(save, incoming);
            if (!candidates.Contains(outgoingRelicId, StringComparer.Ordinal))
                return false;
            Remove(save, outgoingRelicId, false);
            bool acquired = Acquire(save, incoming);
            save.seals = Math.Min(MaxSeals(save), save.seals);
            return acquired;
        }

        public static bool Consume(StoryRunSave save, string relicId)
        {
            StoryRelicRuntimeState state = save?.relicStates?.FirstOrDefault(
                candidate => candidate != null && !candidate.consumed &&
                    string.Equals(candidate.relicId, relicId,
                        StringComparison.Ordinal));
            if (state == null) return false;
            state.consumed = true;
            state.chargesRemaining = 0;
            save.artifacts.RemoveAll(id => string.Equals(
                id, relicId, StringComparison.Ordinal));
            return true;
        }

        public static int MaxSeals(StoryRunSave save) =>
            StoryRunManager.Rules.sealsAtRunStart +
            (Has(save, "reinforced-seal") ? 1 : 0);

        public static int PlayerStartingLifePoints(
            StoryRunSave save,
            int baseLifePoints,
            int temporaryDelta)
        {
            int result = baseLifePoints;
            result += Numeric(save, "ancient-wood-charm", 500);
            result += Numeric(save, "quartz-heart", 1500);
            result += Numeric(save, "ruby-heart", 3000);
            result += Numeric(save, "fallen-king-armor", 2000);
            result += temporaryDelta;
            if (Has(save, "titan-heart"))
                result = checked(result * 2);
            return Math.Max(1, result);
        }

        public static int OpponentStartingLifePoints(
            StoryRunSave save,
            RogueliteNodeType type,
            int baseLifePoints,
            int temporaryDelta)
        {
            int result = baseLifePoints + temporaryDelta;
            if (Has(save, "duelist-pact") &&
                (type == RogueliteNodeType.EliteDuel ||
                 type == RogueliteNodeType.FinalDuelArena))
                result += Numeric(save, "duelist-pact", 2000);
            return Math.Max(1, result);
        }

        public static int VictoryFragments(
            StoryRunSave save,
            RogueliteNodeType type,
            int baseAmount)
        {
            int result = baseAmount;
            if (Has(save, "duelist-compass") &&
                (type == RogueliteNodeType.EliteDuel ||
                 type == RogueliteNodeType.FinalDuelArena ||
                 type == RogueliteNodeType.Boss))
                result += 2;
            if (Has(save, "duelist-token"))
                result += Mathf.FloorToInt(result * 0.15f);
            return Math.Max(0, result);
        }

        public static int DuelRewardChoiceCount(
            StoryRunSave save,
            RogueliteNodeType type)
        {
            int count = 3;
            if (Has(save, "arcane-archive")) count++;
            if (type == RogueliteNodeType.EliteDuel &&
                Has(save, "hunter-mark")) count++;
            if (Has(save, "archmage-crown")) count++;
            return Mathf.Clamp(count, 1, 6);
        }

        public static int MerchantChoiceCount(StoryRunSave save) =>
            Mathf.Clamp(5 + (Has(save, "archmage-crown") ? 1 : 0), 1, 6);

        public static int CardPackChoiceCount(StoryRunSave save)
        {
            if (!Has(save, "dimensional-pouch")) return 5;
            string flag = $"relic-used:dimensional-pouch:act-{save.actIndex}";
            if (save.storyFlags.Contains(flag, StringComparer.Ordinal)) return 5;
            AddUnique(save.storyFlags, flag);
            return 6;
        }

        public static int SpellRuinsChoiceCount(StoryRunSave save) =>
            Has(save, "ruins-lantern") ? 4 : 3;

        public static int VaultFragmentBonus(StoryRunSave save) =>
            Has(save, "scavenger-ring") ? 2 : 0;

        public static bool HealingSpringBlocked(StoryRunSave save) =>
            Has(save, "fallen-king-armor");

        public static bool ProtectRunFromFailure(StoryRunSave save)
        {
            if (!Has(save, "phoenix-mark")) return false;
            Consume(save, "phoenix-mark");
            save.seals = Math.Max(1, save.seals);
            return true;
        }

        private static int Numeric(
            StoryRunSave save,
            string relicId,
            int fallback)
        {
            if (!Has(save, relicId)) return 0;
            StoryRelicDefinition definition = StoryRelicLibrary.Resolve(relicId);
            return definition.numericParams != null &&
                   definition.numericParams.Length > 0
                ? Mathf.RoundToInt(definition.numericParams[0])
                : fallback;
        }

        private static void Remove(
            StoryRunSave save,
            string relicId,
            bool preserveHistory)
        {
            if (save?.relicStates == null) return;
            StoryRelicRuntimeState state = save.relicStates.FirstOrDefault(
                candidate => candidate != null && !candidate.consumed &&
                    string.Equals(candidate.relicId, relicId,
                        StringComparison.Ordinal));
            if (state != null)
            {
                if (preserveHistory) state.consumed = true;
                else save.relicStates.Remove(state);
            }
            save.artifacts.RemoveAll(id => string.Equals(
                id, relicId, StringComparison.Ordinal));
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value) ||
                values.Contains(value, StringComparer.Ordinal)) return;
            values.Add(value);
        }
    }

    public static class StoryRelicRewardResolver
    {
        public static StoryPendingRelicReward ResolvePostDuel(
            StoryRunSave save,
            StoryEncounterDefinition encounter)
        {
            if (save == null || encounter == null) return null;
            string operationId =
                $"RELIC:{save.runId}:{encounter.encounterId}:post-duel-v1";
            if (save.resolvedOperationIds.Contains(
                    operationId, StringComparer.Ordinal))
                return null;
            double roll = UnitDouble(
                save.seed, encounter.nodeId, encounter.encounterId,
                "RELIC_DROP_V1");
            StoryRelicRarity? rarity = StoryRelicLibrary.DropProfile
                .ResolveEncounterRarity(encounter.NodeType, roll);
            if (!rarity.HasValue) return null;
            StoryRelicDefinition picked = Pick(
                save, rarity.Value,
                save.seed, encounter.encounterId, "post-duel");
            if (picked == null) return null;
            return new StoryPendingRelicReward
            {
                operationId = operationId,
                sourceNodeId = encounter.nodeId,
                sourceType = encounter.NodeType.ToString(),
                title = "RELÍQUIA ENCONTRADA",
                relicIds = new List<string> { picked.relicId },
                shrineChoice = false,
                advanceActAfterResolution =
                    encounter.NodeType == RogueliteNodeType.Boss,
                completeNodeAfterResolution =
                    encounter.NodeType == RogueliteNodeType.Boss
            };
        }

        public static StoryPendingRelicReward ResolveShrine(StoryRunSave save)
        {
            if (save == null) return null;
            string operationId =
                $"RELIC:{save.runId}:{save.currentMapId}:{save.currentNodeId}:shrine-v1";
            var options = new List<string>();
            for (int slot = 0; slot < 3; slot++)
            {
                double roll = UnitDouble(
                    save.seed, save.currentMapId, save.currentNodeId,
                    slot, "RELIC_SHRINE_V1");
                StoryRelicRarity rarity = StoryRelicLibrary.DropProfile
                    .ResolveShrineRarity(roll);
                StoryRelicDefinition picked = Pick(
                    save,
                    rarity,
                    new object[]
                    {
                        save.seed,
                        save.currentMapId,
                        save.currentNodeId,
                        slot
                    },
                    options);
                if (picked != null) options.Add(picked.relicId);
            }
            return new StoryPendingRelicReward
            {
                operationId = operationId,
                sourceNodeId = save.currentNodeId,
                sourceType = RogueliteNodeType.RelicShrine.ToString(),
                title = "SANTUÁRIO DE RELÍQUIAS",
                relicIds = options,
                shrineChoice = true,
                completeNodeAfterResolution = true
            };
        }

        public static StoryPendingRelicReward ResolveGuaranteed(
            StoryRunSave save,
            StoryRelicRarity rarity,
            string sourceId,
            string title)
        {
            StoryRelicDefinition picked = Pick(
                save, rarity, save.seed, sourceId, "guaranteed");
            if (picked == null) return null;
            return new StoryPendingRelicReward
            {
                operationId = $"RELIC:{save.runId}:{sourceId}:guaranteed-v1",
                sourceNodeId = save.currentNodeId,
                sourceType = "Guaranteed",
                title = title,
                relicIds = new List<string> { picked.relicId }
            };
        }

        public static double UnitDouble(params object[] values)
        {
            ulong value = StoryDeterminism.Hash(values) >> 11;
            return value * (1.0 / 9007199254740992.0);
        }

        private static StoryRelicDefinition Pick(
            StoryRunSave save,
            StoryRelicRarity rarity,
            params object[] seedParts) => Pick(
                save, rarity, seedParts, Array.Empty<string>());

        private static StoryRelicDefinition Pick(
            StoryRunSave save,
            StoryRelicRarity rarity,
            object[] seedParts,
            IReadOnlyCollection<string> excluded)
        {
            HashSet<string> owned = StoryRelicService.Active(save)
                .Select(state => state.relicId)
                .ToHashSet(StringComparer.Ordinal);
            List<StoryRelicDefinition> pool = StoryRelicLibrary.All
                .Where(definition => definition != null &&
                    definition.IsAvailable && definition.rarity == rarity)
                .Where(definition => !owned.Contains(definition.relicId))
                .Where(definition => excluded == null ||
                    !excluded.Contains(definition.relicId))
                .OrderBy(definition => definition.relicId,
                    StringComparer.Ordinal)
                .ToList();
            if (pool.Count == 0)
            {
                pool = StoryRelicLibrary.All
                    .Where(definition => definition != null &&
                        definition.IsAvailable)
                    .Where(definition => !owned.Contains(definition.relicId))
                    .Where(definition => excluded == null ||
                        !excluded.Contains(definition.relicId))
                    .OrderBy(definition => definition.relicId,
                        StringComparer.Ordinal)
                    .ToList();
            }
            int index = StoryDeterminism.Index(pool.Count, seedParts);
            return index >= 0 ? pool[index] : null;
        }
    }
}
