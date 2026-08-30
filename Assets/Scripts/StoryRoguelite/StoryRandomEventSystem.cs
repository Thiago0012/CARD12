using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;
using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    [Serializable]
    public sealed class StoryRandomEventChoiceDefinition
    {
        public string choiceId;
        public string label;
        [TextArea(2, 6)] public string description;
        public string riskLabel;
        public bool requiresConfirmation;
    }

    [CreateAssetMenu(
        menuName = "Arcane Duel/Story Roguelite/Random Event Definition")]
    public sealed class StoryRandomEventDefinition : ScriptableObject
    {
        public string eventId;
        public string displayName;
        [TextArea(3, 8)] public string flavorText;
        public Sprite icon;
        public int weightAct1;
        public int weightAct2;
        public int weightAct3;
        public string handlerId;
        public StoryRandomEventChoiceDefinition[] choices =
            Array.Empty<StoryRandomEventChoiceDefinition>();

        public int WeightForAct(int act) => act switch
        {
            <= 1 => Mathf.Max(0, weightAct1),
            2 => Mathf.Max(0, weightAct2),
            _ => Mathf.Max(0, weightAct3)
        };
    }

    [CreateAssetMenu(
        menuName = "Arcane Duel/Story Roguelite/Random Event Catalog")]
    public sealed class StoryRandomEventCatalog : ScriptableObject
    {
        public const string ResourcePath =
            "StoryRoguelite/Generated/StoryRandomEventCatalog";
        public List<StoryRandomEventDefinition> definitions = new();
        public IReadOnlyList<StoryRandomEventDefinition> All => definitions?
            .Where(definition => definition != null)
            .ToArray() ?? Array.Empty<StoryRandomEventDefinition>();
    }

    [CreateAssetMenu(
        menuName = "Arcane Duel/Story Roguelite/Random Event Profile")]
    public sealed class StoryRandomEventProfile : ScriptableObject
    {
        public const string ResourcePath =
            "StoryRoguelite/Generated/StoryRandomEventProfile";
        public bool enabled = true;
        public int minimumAct1 = 1;
        public int maximumAct1 = 2;
        public int minimumAct2 = 1;
        public int maximumAct2 = 3;
        public int minimumAct3 = 2;
        public int maximumAct3 = 3;

        public int MinimumForAct(int act) => act switch
        {
            <= 1 => minimumAct1,
            2 => minimumAct2,
            _ => minimumAct3
        };

        public int MaximumForAct(int act) => act switch
        {
            <= 1 => maximumAct1,
            2 => maximumAct2,
            _ => maximumAct3
        };
    }

    [Serializable]
    public sealed class StoryRandomEventOption
    {
        public string choiceId;
        public string label;
        public string description;
        public string riskLabel;
        public bool enabled = true;
        public bool requiresConfirmation;
        public int fragmentCost;
        public string cardId;
        public string relicId;
        public string npcId;
    }

    [Serializable]
    public sealed class StoryPendingRandomEvent
    {
        public string operationId;
        public string nodeId;
        public string eventId;
        public string title;
        public string flavorText;
        public int actIndex;
        public List<string> generatedCardIds = new();
        public List<string> generatedNpcIds = new();
        public List<double> preRolledValues = new();
        public List<StoryRandomEventOption> options = new();
        public bool waitingForNestedDuel;
        public string nestedDuelId;
        public string resultSummary;
    }

    [Serializable]
    public sealed class StoryRandomEventHistoryEntry
    {
        public int actIndex;
        public string nodeId;
        public string eventId;
    }

    [Serializable]
    public sealed class StoryNextDuelModifiers
    {
        public int playerStartingLpDelta;
        public int opponentStartingLpDelta;
        public int openingHandDelta;
        public List<string> sourceOperationIds = new();

        public bool HasAny => playerStartingLpDelta != 0 ||
            opponentStartingLpDelta != 0 || openingHandDelta != 0;

        public void Clear()
        {
            playerStartingLpDelta = 0;
            opponentStartingLpDelta = 0;
            openingHandDelta = 0;
            sourceOperationIds?.Clear();
        }
    }

    public static class StoryRandomEventSpecification
    {
        private static readonly StoryRandomEventDefinition[] Definitions =
            Build();
        public static IReadOnlyList<StoryRandomEventDefinition> All =>
            Definitions;

        private static StoryRandomEventDefinition[] Build() => new[]
        {
            E("unstable-vault", "Cofre de KaibaCorp Danificado",
                "Um cofre avariado pulsa com energia instável.", 10, 10, 10),
            E("forbidden-library", "Biblioteca do Mago Negro",
                "Um tomo proibido oferece conhecimento em troca de força vital.", 6, 8, 8),
            E("wounded-duelist", "Duelista das Sombras Ferido",
                "Um duelista caído ainda guarda recursos e conselhos.", 8, 8, 8),
            E("blood-pact", "Pacto de Orichalcos",
                "O selo promete poder imediato por um preço irreversível.", 0, 5, 7),
            E("path-oracle", "Oráculo do Colar do Milênio",
                "O colar revela ecos dos caminhos que ainda não foram escolhidos.", 8, 8, 8),
            E("wandering-merchant", "Mercador da Cidade de Batalha",
                "Um vendedor itinerante oferece três cartas sem direito a reroll.", 7, 7, 7),
            E("cracked-relic", "Item do Milênio Fraturado",
                "O núcleo rachado pode restaurar uma carga ou virar Fragmentos.", 5, 6, 6),
            E("lightning-arena", "Desafio de Raigeki",
                "Uma arena elétrica convoca um duelista opcional.", 4, 5, 6),
            E("fate-table", "Mesa de Dungeon Dice Monsters",
                "Os dados do destino aceitam uma única aposta de Fragmentos.", 8, 8, 8),
            E("cursed-seal", "Marca do Reino das Sombras",
                "A maldição concede poder agora e fortalece o próximo inimigo.", 0, 5, 7),
            E("arcane-capsule", "Cápsula de Monstros",
                "Três monstros aguardam dentro da cápsula arcana.", 10, 10, 10),
            E("memory-rift", "Portal do Reino das Sombras",
                "Um eco de um duelista derrotado pede revanche.", 0, 4, 6),
            E("arcane-spring", "Fonte do Santuário do Faraó",
                "A fonte oferece recuperação ou energia para a jornada.", 8, 8, 8),
            E("thousand-eyes-trial", "Julgamento de Mil Olhos",
                "Três graus de risco observam a sua escolha.", 0, 4, 6),
            E("duelist-sanctuary", "Santuário do Coração das Cartas",
                "O santuário prepara sua mão para o próximo duelo.", 5, 6, 7)
        };

        private static StoryRandomEventDefinition E(
            string id,
            string name,
            string flavor,
            int act1,
            int act2,
            int act3)
        {
            StoryRandomEventDefinition definition =
                ScriptableObject.CreateInstance<StoryRandomEventDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.eventId = id;
            definition.displayName = name;
            definition.flavorText = flavor;
            definition.weightAct1 = act1;
            definition.weightAct2 = act2;
            definition.weightAct3 = act3;
            definition.handlerId = id;
            return definition;
        }
    }

    public static class StoryRandomEventLibrary
    {
        private static StoryRandomEventCatalog catalog;
        private static StoryRandomEventProfile profile;

        public static IReadOnlyList<StoryRandomEventDefinition> All
        {
            get
            {
                catalog ??= Resources.Load<StoryRandomEventCatalog>(
                    StoryRandomEventCatalog.ResourcePath);
                return catalog != null && catalog.All.Count == 15
                    ? catalog.All
                    : StoryRandomEventSpecification.All;
            }
        }

        public static StoryRandomEventProfile Profile
        {
            get
            {
                profile ??= Resources.Load<StoryRandomEventProfile>(
                    StoryRandomEventProfile.ResourcePath);
                if (profile != null) return profile;
                profile = ScriptableObject.CreateInstance<
                    StoryRandomEventProfile>();
                profile.hideFlags = HideFlags.HideAndDontSave;
                return profile;
            }
        }

        public static StoryRandomEventDefinition Resolve(string eventId) =>
            All.FirstOrDefault(definition => string.Equals(
                definition.eventId, eventId, StringComparison.Ordinal));

        public static void ClearCache()
        {
            catalog = null;
            profile = null;
        }
    }

    public static class StoryRandomEventService
    {
        public static StoryPendingRandomEvent Resolve(
            StoryRunSave save,
            StoryMapRecord map,
            CardCatalog cardCatalog)
        {
            if (save == null || map == null) return null;
            if (save.pendingRandomEvent != null && string.Equals(
                    save.pendingRandomEvent.nodeId,
                    save.currentNodeId,
                    StringComparison.Ordinal))
                return save.pendingRandomEvent;

            List<StoryRandomEventDefinition> eligible =
                StoryRandomEventLibrary.All
                    .Where(definition => definition != null &&
                        definition.WeightForAct(save.actIndex) > 0)
                    .Where(definition => IsEligible(
                        definition, save, map, cardCatalog))
                    .ToList();
            HashSet<string> usedThisAct = save.randomEventHistory
                .Where(entry => entry != null &&
                    entry.actIndex == save.actIndex)
                .Select(entry => entry.eventId)
                .ToHashSet(StringComparer.Ordinal);
            List<StoryRandomEventDefinition> withoutRepeats = eligible
                .Where(definition => !usedThisAct.Contains(definition.eventId))
                .ToList();
            if (withoutRepeats.Count > 0) eligible = withoutRepeats;
            if (eligible.Count == 0)
            {
                StoryRandomEventDefinition fallback =
                    StoryRandomEventLibrary.Resolve("unstable-vault");
                if (fallback != null) eligible.Add(fallback);
            }
            StoryRandomEventDefinition selected = WeightedPick(
                eligible,
                save.actIndex,
                save.seed,
                save.currentMapId,
                save.currentNodeId,
                "MAP_EVENT_V1");
            if (selected == null) return null;

            var pending = new StoryPendingRandomEvent
            {
                operationId =
                    $"EVT:{save.runId}:{save.actIndex}:{save.currentNodeId}:{selected.eventId}",
                nodeId = save.currentNodeId,
                eventId = selected.eventId,
                title = selected.displayName,
                flavorText = selected.flavorText,
                actIndex = save.actIndex
            };
            for (int index = 0; index < 5; index++)
            {
                pending.preRolledValues.Add(StoryRelicRewardResolver.UnitDouble(
                    save.seed, save.actIndex, save.currentNodeId,
                    selected.eventId, index, "EVENT_ROLL_V1"));
            }
            PreparePayload(pending, save, map, cardCatalog);
            return pending;
        }

        private static bool IsEligible(
            StoryRandomEventDefinition definition,
            StoryRunSave save,
            StoryMapRecord map,
            CardCatalog cardCatalog)
        {
            return definition.eventId switch
            {
                "blood-pact" => save.seals >= 2,
                "memory-rift" => save.defeatedNpcIds.Count > 0,
                "forbidden-library" => StoryRewardService.HasEligibleCards(
                    cardCatalog, CardCategory.Spell, CardCategory.Trap),
                _ => true
            };
        }

        private static StoryRandomEventDefinition WeightedPick(
            IReadOnlyList<StoryRandomEventDefinition> candidates,
            int act,
            params object[] seedParts)
        {
            int total = candidates?.Sum(candidate =>
                candidate.WeightForAct(act)) ?? 0;
            if (total <= 0) return candidates?.FirstOrDefault();
            int roll = StoryDeterminism.Index(total, seedParts);
            int cumulative = 0;
            foreach (StoryRandomEventDefinition candidate in candidates)
            {
                cumulative += candidate.WeightForAct(act);
                if (roll < cumulative) return candidate;
            }
            return candidates[candidates.Count - 1];
        }

        private static void PreparePayload(
            StoryPendingRandomEvent pending,
            StoryRunSave save,
            StoryMapRecord map,
            CardCatalog catalog)
        {
            switch (pending.eventId)
            {
                case "unstable-vault":
                    Add(pending, "careful", "ABRIR COM CUIDADO",
                        "Receba 3 Fragmentos.", "Baixo");
                    Add(pending, "force", "FORÇAR A FECHADURA",
                        "60%: +8, 30%: +5, 10%: perde até 2 Fragmentos.",
                        "Alto", true);
                    break;
                case "forbidden-library":
                    pending.generatedCardIds = StoryRewardService
                        .PickCardChoices(save.seed, pending.operationId,
                            catalog, 3, true);
                    foreach (string cardId in pending.generatedCardIds)
                        AddCard(pending, "tome-" + cardId,
                            "CONSULTAR O TOMO", cardId,
                            "Escolha esta carta e aplique -1.000 LP no próximo duelo não-Boss.",
                            "Médio", true);
                    Add(pending, "ignore", "IGNORAR O TOMO",
                        "Encerre sem efeito.", "Baixo");
                    break;
                case "wounded-duelist":
                    Add(pending, "help", "AJUDAR",
                        "Custa 2 Fragmentos e restaura 1 Selo.", "Baixo",
                        true, save.fragments >= 2 &&
                              save.seals < StoryRelicService.MaxSeals(save), 2);
                    Add(pending, "advice", "OUVIR O CONSELHO",
                        "Receba 2 Fragmentos.", "Baixo");
                    break;
                case "blood-pact":
                    pending.generatedCardIds = StoryRewardService
                        .PickCardChoices(save.seed, pending.operationId,
                            catalog, 3, false, CardCategory.Monster);
                    foreach (string cardId in pending.generatedCardIds)
                        AddCard(pending, "pact-" + cardId,
                            "ACEITAR O PACTO", cardId,
                            "Perca 1 Selo, receba 9 Fragmentos e esta carta.",
                            "Alto", true);
                    Add(pending, "refuse", "RECUSAR",
                        "Encerre sem penalidade.", "Baixo");
                    break;
                case "path-oracle":
                    Add(pending, "observe", "OBSERVAR",
                        "Revele até dois Mystery nas próximas camadas.", "Baixo");
                    Add(pending, "full", "COMPRAR A VISÃO COMPLETA",
                        "Custa 2 Fragmentos e revela todos os Mystery próximos.",
                        "Baixo", true, save.fragments >= 2, 2);
                    break;
                case "wandering-merchant":
                    pending.generatedCardIds = StoryRewardService
                        .PickCardChoices(save.seed, pending.operationId,
                            catalog, 3);
                    for (int index = 0; index < pending.generatedCardIds.Count;
                         index++)
                    {
                        int cost = 3 + index;
                        string cardId = pending.generatedCardIds[index];
                        AddCard(pending, "buy-" + cardId, "COMPRAR",
                            cardId, $"Compre por {cost} Fragmentos.", "Baixo",
                            false, save.fragments >= cost, cost);
                    }
                    Add(pending, "leave", "SAIR",
                        "Encerre sem comprar.", "Baixo");
                    break;
                case "cracked-relic":
                    foreach (StoryRelicRuntimeState state in
                             StoryRelicService.Active(save))
                    {
                        StoryRelicDefinition definition =
                            StoryRelicLibrary.Resolve(state.relicId);
                        if (definition.rarity == StoryRelicRarity.Unique ||
                            definition.initialCharges <= 0 ||
                            state.chargesRemaining >= definition.initialCharges)
                            continue;
                        Add(pending, "restore-" + state.relicId,
                            "RESTAURAR " + definition.displayName.ToUpperInvariant(),
                            "Custa 2 Fragmentos e recupera 1 carga.", "Baixo",
                            true, save.fragments >= 2, 2, null, state.relicId);
                    }
                    Add(pending, "dismantle", "DESMONTAR O NÚCLEO",
                        "Receba 3 Fragmentos.", "Baixo");
                    break;
                case "lightning-arena":
                    Add(pending, "challenge", "DESAFIAR",
                        "Inicie um duelo opcional sem moedas permanentes.",
                        "Alto", true);
                    Add(pending, "refuse", "RECUSAR",
                        "Encerre sem efeito.", "Baixo");
                    break;
                case "fate-table":
                    foreach (int amount in new[] { 2, 4, 6 })
                        Add(pending, "bet-" + amount, $"APOSTAR {amount}",
                            "55%: ganho líquido igual à aposta; 45%: perde a aposta.",
                            "Alto", true, save.fragments >= amount, amount);
                    break;
                case "cursed-seal":
                    Add(pending, "accept", "ACEITAR A MALDIÇÃO",
                        "Receba 7 Fragmentos; próximo inimigo não-Boss recebe +3.000 LP.",
                        "Alto", true);
                    Add(pending, "purify", "PURIFICAR",
                        "Custa 3 Fragmentos e encerra sem modificador.", "Baixo",
                        true, save.fragments >= 3, 3);
                    Add(pending, "ignore", "IGNORAR",
                        "Encerre sem efeito.", "Baixo");
                    break;
                case "arcane-capsule":
                    pending.generatedCardIds = StoryRewardService
                        .PickCardChoices(save.seed, pending.operationId,
                            catalog, 3, false, CardCategory.Monster);
                    foreach (string cardId in pending.generatedCardIds)
                        AddCard(pending, "capsule-" + cardId,
                            "ESCOLHER MONSTRO", cardId,
                            "Envie esta carta para a reserva.", "Baixo");
                    break;
                case "memory-rift":
                    pending.generatedNpcIds = save.defeatedNpcIds
                        .AsEnumerable().Reverse().Distinct().Take(3).ToList();
                    foreach (string npcId in pending.generatedNpcIds)
                    {
                        StoryNpcRecord npc = StoryContentCatalog.ResolveNpc(npcId);
                        Add(pending, "echo-" + npcId,
                            "ENFRENTAR " + (npc?.displayName ?? npcId).ToUpperInvariant(),
                            "Eco com dificuldade +1 e +3.000 LP. Vitória: +6 Fragmentos.",
                            "Alto", true, true, 0, null, null, npcId);
                    }
                    Add(pending, "refuse", "RECUSAR",
                        "Encerre sem efeito.", "Baixo");
                    break;
                case "arcane-spring":
                    Add(pending, "seal", "RESTAURAR SELO",
                        "Recupere 1 Selo até o máximo.", "Baixo", true,
                        save.seals < StoryRelicService.MaxSeals(save));
                    Add(pending, "energy", "ABSORVER ENERGIA",
                        "Receba 4 Fragmentos.", "Baixo");
                    break;
                case "thousand-eyes-trial":
                    Add(pending, "caution", "PRUDÊNCIA",
                        "Receba 2 Fragmentos garantidos.", "Baixo");
                    Add(pending, "balance", "EQUILÍBRIO",
                        "70%: +5 Fragmentos; 30%: perde 1.", "Médio", true);
                    Add(pending, "audacity", "AUDÁCIA",
                        "45%: +10; 35%: +5; 20%: -1.500 LP no próximo duelo.",
                        "Alto", true);
                    break;
                case "duelist-sanctuary":
                    Add(pending, "hand", "PREPARAR A MÃO",
                        "Indisponível nesta versão: o core atual expõe o " +
                        "tamanho inicial da mão de forma simétrica para os " +
                        "dois duelistas.",
                        "Baixo", true, false, 4);
                    Add(pending, "meditate", "MEDITAR",
                        "Receba 2 Fragmentos.", "Baixo");
                    break;
            }
        }

        private static void AddCard(
            StoryPendingRandomEvent pending,
            string choiceId,
            string label,
            string cardId,
            string description,
            string risk,
            bool confirm = false,
            bool enabled = true,
            int cost = 0)
        {
            Add(pending, choiceId, label, description, risk, confirm,
                enabled, cost, cardId);
        }

        private static void Add(
            StoryPendingRandomEvent pending,
            string choiceId,
            string label,
            string description,
            string risk,
            bool confirm = false,
            bool enabled = true,
            int cost = 0,
            string cardId = null,
            string relicId = null,
            string npcId = null)
        {
            pending.options.Add(new StoryRandomEventOption
            {
                choiceId = choiceId,
                label = label,
                description = description,
                riskLabel = risk,
                requiresConfirmation = confirm,
                enabled = enabled,
                fragmentCost = cost,
                cardId = cardId ?? string.Empty,
                relicId = relicId ?? string.Empty,
                npcId = npcId ?? string.Empty
            });
        }
    }
}
