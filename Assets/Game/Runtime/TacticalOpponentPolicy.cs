using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Stateful wrapper used by the arena. It remembers choices made during
    /// the current turn so optional effects cannot be selected in a useless
    /// loop and target selection can consider the effect that opened it.
    /// </summary>
    public sealed class TacticalOpponentAgent
    {
        private readonly BotDecisionService decisionService =
            new BotDecisionService();
        private readonly Dictionary<string, int> repetitions =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> stateVisits =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private int observedTurn = -1;
        private uint sourceCardCode;
        private string sourceLabel = string.Empty;
        private string previousState = string.Empty;
        private int noProgressDecisions;
        private BotProfile profile;
        private int decisionSeed;

        public void Configure(BotProfile botProfile, int seed)
        {
            profile = botProfile ?? DynamicBotCatalog.Find("BOT_017");
            decisionSeed = seed;
            decisionService.Configure(profile, seed);
            ResetMemory();
        }

        public float DecisionDelay(DuelPrompt prompt)
        {
            BotDifficultySettings settings = DynamicBotCatalog.Settings(
                (profile ?? DynamicBotCatalog.Find("BOT_017")).skill);
            return TacticalOpponentPolicy.DecisionDelay(prompt) *
                   settings.DelayMultiplier;
        }

        public DuelChoice Choose(
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database)
        {
            int turn = state?.TurnNumber ?? 0;
            if (turn != observedTurn)
            {
                repetitions.Clear();
                stateVisits.Clear();
                sourceCardCode = 0;
                sourceLabel = string.Empty;
                previousState = string.Empty;
                noProgressDecisions = 0;
                observedTurn = turn;
            }

            string stateSignature =
                TacticalOpponentPolicy.StateSignature(prompt, state);
            stateVisits.TryGetValue(stateSignature, out int visits);
            stateVisits[stateSignature] = visits + 1;
            if (string.Equals(
                    stateSignature,
                    previousState,
                    StringComparison.Ordinal))
            {
                noProgressDecisions++;
            }
            else
            {
                noProgressDecisions = 0;
                previousState = stateSignature;
            }
            var context = new TacticalDecisionContext(
                repetitions,
                sourceCardCode,
                sourceLabel,
                stateSignature,
                visits,
                noProgressDecisions);
            if (profile == null)
                Configure(BotRuntimeSelection.CurrentProfile,
                    BotRuntimeSelection.CurrentSeed);
            DuelChoice choice = decisionService.Choose(
                prompt,
                state,
                database,
                context);
            if (choice == null)
                return null;

            // A legal optional decision that produces no authoritative state
            // change must not be selected forever. Prefer a legal choice that
            // has not yet been tried in this exact Core state; this never
            // invents a response because every candidate still comes from the
            // current prompt emitted by ygopro-core.
            if (!prompt.Forced &&
                noProgressDecisions > 0 &&
                context.RepetitionCount(prompt, choice) > 0)
            {
                DuelChoice untried = prompt.Choices
                    .Where(candidate =>
                        context.RepetitionCount(prompt, candidate) == 0)
                    .OrderByDescending(candidate =>
                        TacticalOpponentPolicy.ScoreChoice(
                            candidate,
                            prompt,
                            state,
                            database,
                            context))
                    .ThenBy(candidate => candidate.ChoiceIndex)
                    .FirstOrDefault();
                if (untried != null)
                    choice = untried;
            }

            string key = TacticalOpponentPolicy.DecisionKey(
                prompt,
                choice,
                stateSignature);
            repetitions.TryGetValue(key, out int count);
            repetitions[key] = count + 1;

            string legalChoices = string.Join(
                ", ",
                prompt.Choices.Select(candidate =>
                {
                    int score = TacticalOpponentPolicy.ScoreChoice(
                        candidate,
                        prompt,
                        state,
                        database,
                        context);
                    return candidate.ChoiceIndex + ":" +
                           candidate.Label + "=" + score;
                }));
            DuelDevelopmentLog.Write(
                DuelLogCategory.BotDecision,
                $"request={prompt.RequestId}; state={stateSignature}; " +
                $"visits={visits}; noProgress={noProgressDecisions}; " +
                $"legal=[{legalChoices}]; " +
                $"chosen={choice.ChoiceIndex}:{choice.Label}; " +
                $"card={choice.CardCode:00000000}; " +
                $"materialsPrompt={prompt.Message}");

            if (choice.CardCode != 0 &&
                (prompt.Message == CoreMessage.SelectIdleCommand ||
                 prompt.Message == CoreMessage.SelectBattleCommand ||
                 prompt.Message == CoreMessage.SelectChain ||
                 prompt.Message == CoreMessage.SelectEffectYesNo))
            {
                sourceCardCode = choice.CardCode;
                sourceLabel = choice.Label ?? string.Empty;
            }
            return choice;
        }

        public void Reset()
        {
            ResetMemory();
            decisionService.Configure(
                profile ?? DynamicBotCatalog.Find("BOT_017"),
                decisionSeed);
        }

        private void ResetMemory()
        {
            repetitions.Clear();
            stateVisits.Clear();
            observedTurn = -1;
            sourceCardCode = 0;
            sourceLabel = string.Empty;
            previousState = string.Empty;
            noProgressDecisions = 0;
        }
    }

    internal sealed class TacticalDecisionContext
    {
        private readonly IReadOnlyDictionary<string, int> repetitions;

        internal TacticalDecisionContext(
            IReadOnlyDictionary<string, int> repetitions,
            uint sourceCardCode,
            string sourceLabel,
            string stateSignature,
            int stateVisits,
            int noProgressDecisions)
        {
            this.repetitions = repetitions;
            SourceCardCode = sourceCardCode;
            SourceLabel = sourceLabel ?? string.Empty;
            StateSignature = stateSignature ?? string.Empty;
            StateVisits = stateVisits;
            NoProgressDecisions = noProgressDecisions;
        }

        internal uint SourceCardCode { get; }
        internal string SourceLabel { get; }
        internal string StateSignature { get; }
        internal int StateVisits { get; }
        internal int NoProgressDecisions { get; }

        internal int RepetitionCount(DuelPrompt prompt, DuelChoice choice)
        {
            if (repetitions == null)
                return 0;
            return repetitions.TryGetValue(
                TacticalOpponentPolicy.DecisionKey(
                    prompt,
                    choice,
                    StateSignature),
                out int count)
                ? count
                : 0;
        }
    }

    /// <summary>
    /// Evaluates only choices already validated and emitted by ygopro-core.
    /// Rules, costs, timing and legality remain exclusively in the Core.
    /// </summary>
    public static class TacticalOpponentPolicy
    {
        private const uint Monster = 0x1;
        private const uint Spell = 0x2;
        private const uint Trap = 0x4;
        private const uint Effect = 0x20;
        private const uint Fusion = 0x40;
        private const uint Synchro = 0x2000;
        private const uint Xyz = 0x800000;
        private const uint Link = 0x4000000;
        private const uint ExtraTypes = Fusion | Synchro | Xyz | Link;
        private const uint FaceUpAttack = 0x1;
        private const uint DefensePositions = 0x2 | 0x8;

        public static DuelChoice Choose(
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database)
        {
            return Choose(prompt, state, database, null);
        }

        internal static DuelChoice Choose(
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database,
            TacticalDecisionContext context)
        {
            if (prompt == null || prompt.Choices.Count == 0)
                return null;

            switch (prompt.Message)
            {
                case CoreMessage.SelectIdleCommand:
                case CoreMessage.SelectBattleCommand:
                case CoreMessage.SelectChain:
                case CoreMessage.SelectEffectYesNo:
                case CoreMessage.SelectYesNo:
                case CoreMessage.SelectPosition:
                case CoreMessage.SelectOption:
                    return prompt.Choices
                        .OrderByDescending(choice =>
                            Score(choice, prompt, state, database, context))
                        .ThenBy(choice => choice.ChoiceIndex)
                        .First();

                case CoreMessage.SelectPlace:
                case CoreMessage.SelectDisableField:
                    return ChooseCentralLegalZone(prompt);

                case CoreMessage.SelectCard:
                    return ChooseSingleCard(
                               prompt,
                               state,
                               database,
                               context) ??
                           DeterministicDuelPolicy.Choose(prompt);

                default:
                    // Tribute, sum and multi-material selections have exact
                    // protocol constraints. Keep the proven legal solver.
                    return DeterministicDuelPolicy.Choose(prompt);
            }
        }

        public static float DecisionDelay(DuelPrompt prompt)
        {
            if (prompt == null)
                return 0.32f;
            switch (prompt.Message)
            {
                case CoreMessage.SelectChain:
                    return prompt.Forced ? 0.28f : 0.36f;
                case CoreMessage.SelectYesNo:
                case CoreMessage.SelectEffectYesNo:
                    return 0.40f;
                case CoreMessage.SelectPosition:
                    return 0.46f;
                case CoreMessage.SelectBattleCommand:
                    return 0.54f;
                case CoreMessage.SelectIdleCommand:
                    return Math.Min(
                        1.02f,
                        0.64f + prompt.Choices.Count * 0.018f);
                default:
                    return Math.Min(
                        0.92f,
                        0.56f + prompt.Choices.Count * 0.012f);
            }
        }

        internal static string DecisionKey(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            return DecisionKey(prompt, choice, string.Empty);
        }

        internal static string DecisionKey(
            DuelPrompt prompt,
            DuelChoice choice,
            string stateSignature)
        {
            return string.Concat(
                stateSignature ?? string.Empty, ":",
                ((int)(prompt?.Message ?? 0)).ToString(), ":",
                choice?.CardCode.ToString() ?? "0", ":",
                choice?.Controller.ToString() ?? "0", ":",
                choice?.Location.ToString() ?? "0", ":",
                choice?.Sequence.ToString() ?? "0", ":",
                choice?.DescriptionId.ToString() ?? "0", ":",
                Fold(choice?.Label));
        }

        internal static string StateSignature(
            DuelPrompt prompt,
            DuelPresentationState state)
        {
            if (state == null)
                return $"prompt:{prompt?.RequestId ?? 0}";
            var builder = new StringBuilder(256);
            builder.Append(state.TurnNumber).Append(':')
                .Append(state.TurnPlayer).Append(':')
                .Append(state.Phase).Append(':')
                .Append(prompt?.Player ?? 0).Append(':')
                .Append((int)(prompt?.Message ?? 0));
            for (int player = 0; player < state.Players.Length; player++)
            {
                DuelistState duelist = state.Players[player];
                builder.Append("|P").Append(player)
                    .Append(':').Append(duelist.LifePoints)
                    .Append(':').Append(duelist.Hand.Count)
                    .Append(':').Append(duelist.DeckCount)
                    .Append(':').Append(duelist.Graveyard.Count);
                foreach (uint code in duelist.MonsterZones)
                    builder.Append(',').Append(code);
                builder.Append('/');
                foreach (uint position in duelist.MonsterPositions)
                    builder.Append(',').Append(position);
                builder.Append('/');
                foreach (uint code in duelist.SpellTrapZones)
                    builder.Append(',').Append(code);
            }
            return builder.ToString();
        }

        internal static int ScoreChoice(
            DuelChoice choice,
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database,
            TacticalDecisionContext context)
        {
            return Score(choice, prompt, state, database, context);
        }

        private static DuelChoice ChooseCentralLegalZone(DuelPrompt prompt)
        {
            int[] preference = { 2, 1, 3, 0, 4, 5, 6 };
            int required = Math.Max(
                1,
                checked((int)prompt.MaximumSelections));
            if (required > 1)
                return DeterministicDuelPolicy.Choose(prompt);

            DuelChoice[] selected = prompt.Choices
                .OrderBy(choice =>
                {
                    int rank = Array.IndexOf(
                        preference,
                        (int)choice.Sequence);
                    return rank < 0 ? int.MaxValue : rank;
                })
                .ThenBy(choice => choice.ChoiceIndex)
                .Take(required)
                .ToArray();
            if (selected.Length == 0)
                return null;
            return selected[0];
        }

        private static DuelChoice ChooseSingleCard(
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database,
            TacticalDecisionContext context)
        {
            if (prompt.MinimumSelections != 1 ||
                prompt.MaximumSelections != 1)
            {
                return null;
            }

            List<DuelChoice> candidates = prompt.Choices
                .Where(choice => choice.CardCode != 0)
                .ToList();
            if (candidates.Count == 0)
                return null;

            List<DuelChoice> hostile = candidates
                .Where(choice =>
                    choice.HasLocation &&
                    choice.Controller != prompt.Player &&
                    (choice.Location &
                     (DuelLocation.MonsterZone |
                      DuelLocation.SpellTrapZone)) != 0)
                .ToList();
            if (hostile.Count > 0)
            {
                bool battle = state != null &&
                              (state.Phase & 0x0F8U) != 0;
                return battle
                    ? hostile
                        .OrderBy(choice =>
                            BattleStat(
                                choice.Controller,
                                choice.Sequence,
                                state,
                                database))
                        .ThenBy(choice => choice.ChoiceIndex)
                        .First()
                    : hostile
                        .OrderByDescending(choice =>
                            ThreatValue(choice.CardCode, database))
                        .ThenBy(choice => choice.ChoiceIndex)
                        .First();
            }

            string sourceText = CardText(
                context?.SourceCardCode ?? 0,
                database);
            bool paysResource =
                Contains(sourceText, "DISCARD") ||
                Contains(sourceText, "TRIBUTE 1") ||
                Contains(sourceText, "SEND 1") ||
                Contains(sourceText, "BANISH 1");
            bool retrievesCard =
                Contains(sourceText, "ADD 1") ||
                Contains(sourceText, "SPECIAL SUMMON") ||
                Contains(sourceText, "TARGET 1") ||
                Contains(sourceText, "FROM YOUR DECK") ||
                Contains(sourceText, "FROM YOUR GY");

            IOrderedEnumerable<DuelChoice> ordered;
            if (candidates.Any(choice =>
                    choice.HasLocation &&
                    (choice.Location & DuelLocation.Extra) != 0))
            {
                ordered = candidates.OrderByDescending(choice =>
                    CardStrategicValue(
                        choice.CardCode,
                        prompt.Player,
                        state,
                        database));
            }
            else if (retrievesCard)
            {
                ordered = candidates.OrderByDescending(choice =>
                    CardStrategicValue(
                        choice.CardCode,
                        prompt.Player,
                        state,
                        database));
            }
            else if (paysResource)
            {
                ordered = candidates.OrderBy(choice =>
                    CardStrategicValue(
                        choice.CardCode,
                        prompt.Player,
                        state,
                        database));
            }
            else
            {
                ordered = candidates.OrderByDescending(choice =>
                    CardStrategicValue(
                        choice.CardCode,
                        prompt.Player,
                        state,
                        database));
            }
            return ordered.ThenBy(choice => choice.ChoiceIndex).First();
        }

        private static int Score(
            DuelChoice choice,
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database,
            TacticalDecisionContext context)
        {
            string label = Fold(choice.Label);
            int strategic = CardStrategicValue(
                choice.CardCode,
                prompt.Player,
                state,
                database);
            int score = strategic;
            bool behind = IsBehind(state, prompt.Player, database);

            if (Contains(label, "INVOCACAO ESPECIAL"))
            {
                score += 112000;
                if (choice.HasLocation &&
                    (choice.Location & DuelLocation.Extra) != 0)
                {
                    score += ScoreExtraDeckCommitment(
                        choice,
                        prompt.Player,
                        state,
                        database);
                }
            }
            else if (Contains(label, "ATIVAR"))
            {
                score += 82000 + ActivationUtility(
                    choice.CardCode,
                    prompt,
                    state,
                    database);
            }
            else if (Contains(label, "INVOCAR"))
            {
                score += 88000;
                if (database != null &&
                    database.TryGet(choice.CardCode, out CardRecord summon))
                {
                    score += summon.Attack >= summon.Defense
                        ? 6000
                        : behind ? 4500 : 0;
                }
            }
            else if (Contains(label, "ATACAR"))
            {
                score += ScoreAttack(
                    choice.CardCode,
                    prompt.Player,
                    state,
                    database);
            }
            else if (Contains(label, "BAIXAR MAGIA") ||
                     Contains(label, "BAIXAR MONSTRO"))
            {
                score += 51000;
                if (behind) score += 7000;
                if (database != null &&
                    database.TryGet(choice.CardCode, out CardRecord setCard) &&
                    (setCard.Type & Trap) != 0)
                {
                    score += 9000;
                }
            }
            else if (Contains(label, "MUDAR POSICAO"))
            {
                score += ScorePositionChange(
                    choice,
                    prompt.Player,
                    behind,
                    state,
                    database);
            }
            else if (Contains(label, "FASE DE BATALHA"))
            {
                score += CanAttackProfitably(
                        state,
                        prompt.Player,
                        database)
                    ? 78000
                    : 8000;
            }
            else if (Contains(label, "FASE PRINCIPAL 2"))
            {
                score += CanAttackProfitably(
                        state,
                        prompt.Player,
                        database)
                    ? 4000
                    : 28000;
            }
            else if (Contains(label, "SIM"))
            {
                score += 34000 + ActivationUtility(
                    context?.SourceCardCode ?? choice.CardCode,
                    prompt,
                    state,
                    database);
            }
            else if (Contains(label, "NAO ATIVAR") ||
                     Contains(label, "NAO RESPONDER") ||
                     Contains(label, "CANCELAR"))
            {
                score += prompt.Forced ? -120000 : 12000;
            }
            else if (Contains(label, "ENCERRAR"))
            {
                score += HasConstructiveAlternative(prompt)
                    ? -70000
                    : 18000;
            }
            else if (prompt.Message == CoreMessage.SelectOption)
            {
                score += OptionUtility(choice, database);
            }

            if (prompt.Message == CoreMessage.SelectPosition &&
                database != null &&
                database.TryGet(choice.CardCode, out CardRecord positionCard))
            {
                bool attackPosition = Contains(label, "ATAQUE");
                bool prefersAttack =
                    positionCard.Attack > 0 &&
                    (!behind || positionCard.Attack >= positionCard.Defense);
                score += attackPosition == prefersAttack ? 38000 : -26000;
            }

            int repetitions = context?.RepetitionCount(prompt, choice) ?? 0;
            score -= repetitions * 42000;
            if (repetitions >= 2 && !prompt.Forced)
                score -= 180000;
            if (context != null && !prompt.Forced)
            {
                score -= context.StateVisits * 18000;
                score -= context.NoProgressDecisions * 32000;
                if (IsXyzChoice(choice, database) &&
                    (repetitions > 0 || context.NoProgressDecisions > 0))
                {
                    score -= 92000;
                }
            }
            return score;
        }

        private static int ScoreExtraDeckCommitment(
            DuelChoice choice,
            byte player,
            DuelPresentationState state,
            CardDatabase database)
        {
            if (choice == null || database == null ||
                !database.TryGet(choice.CardCode, out CardRecord result))
            {
                return 0;
            }
            int resultValue = CardStrategicValue(
                choice.CardCode,
                player,
                state,
                database);
            if (state == null || player >= state.Players.Length)
                return resultValue * 8;

            DuelistState me = state.Players[player];
            var materialValues = new List<int>();
            for (int index = 0; index < me.MonsterZones.Length; index++)
            {
                uint materialCode = me.MonsterZones[index];
                if (materialCode == 0 ||
                    !database.TryGet(materialCode, out CardRecord material))
                {
                    continue;
                }
                if ((result.Type & Xyz) != 0 &&
                    material.Level != result.Level)
                {
                    continue;
                }
                materialValues.Add(
                    CardStrategicValue(
                        materialCode,
                        player,
                        state,
                        database));
            }
            materialValues.Sort();
            int required = (result.Type & Link) != 0 ? 1 : 2;
            int materialCost = materialValues
                .Take(required)
                .Sum();
            int net = resultValue * 2 - materialCost;
            int utility = Math.Max(-70000, Math.Min(70000, net * 9));
            if ((result.Type & Xyz) != 0 &&
                materialValues.Count >= 2 &&
                resultValue * 2 < materialCost + 900)
            {
                utility -= 76000;
            }
            return utility;
        }

        private static bool IsXyzChoice(
            DuelChoice choice,
            CardDatabase database)
        {
            return choice != null &&
                   choice.CardCode != 0 &&
                   choice.HasLocation &&
                   (choice.Location & DuelLocation.Extra) != 0 &&
                   database != null &&
                   database.TryGet(
                       choice.CardCode,
                       out CardRecord card) &&
                   (card.Type & Xyz) != 0;
        }

        private static int ScoreAttack(
            uint attackerCode,
            byte player,
            DuelPresentationState state,
            CardDatabase database)
        {
            int attack = AttackValue(attackerCode, database);
            if (attack <= 0)
                return -90000;
            if (state == null)
                return 72000 + attack;

            DuelistState enemy = state.Players[1 - player];
            List<int> targets = Enumerable.Range(
                    0,
                    enemy.MonsterZones.Length)
                .Where(index => enemy.MonsterZones[index] != 0)
                .Select(index =>
                    BattleStat(
                        (byte)(1 - player),
                        (uint)index,
                        state,
                        database))
                .ToList();
            if (targets.Count == 0)
                return 104000 + attack * 3;

            int weakest = targets.Min();
            int margin = attack - weakest;
            return margin > 0
                ? 96000 + margin * 6
                : 9000 + margin * 12;
        }

        private static int ScorePositionChange(
            DuelChoice choice,
            byte player,
            bool behind,
            DuelPresentationState state,
            CardDatabase database)
        {
            if (database == null ||
                !database.TryGet(choice.CardCode, out CardRecord card))
            {
                return 24000;
            }
            bool currentlyAttack = false;
            if (state != null &&
                choice.Sequence <
                state.Players[player].MonsterPositions.Length)
            {
                currentlyAttack =
                    (state.Players[player]
                         .MonsterPositions[choice.Sequence] &
                     FaceUpAttack) != 0;
            }
            bool shouldAttack =
                card.Attack > 0 &&
                card.Attack >= card.Defense &&
                (!behind || card.Attack >= EnemyBestStat(
                    state,
                    player,
                    database));
            return currentlyAttack == shouldAttack ? 6000 : 36000;
        }

        private static int ActivationUtility(
            uint code,
            DuelPrompt prompt,
            DuelPresentationState state,
            CardDatabase database)
        {
            string text = CardText(code, database);
            if (text.Length == 0)
                return 0;

            byte player = prompt?.Player ?? 1;
            DuelistState me = state != null ? state.Players[player] : null;
            DuelistState enemy = state != null ? state.Players[1 - player] : null;
            int utility = 0;

            if (Contains(text, "ADD 1") ||
                Contains(text, "FROM YOUR DECK TO YOUR HAND"))
            {
                utility += 31000;
            }
            if (Contains(text, "DRAW"))
                utility += 26000;
            if (Contains(text, "FUSION SUMMON") ||
                Contains(text, "SYNCHRO SUMMON") ||
                Contains(text, "XYZ SUMMON") ||
                Contains(text, "LINK SUMMON"))
            {
                utility += 30000;
            }
            if (Contains(text, "SPECIAL SUMMON"))
                utility += 23000;
            if (Contains(text, "NEGATE"))
            {
                bool reacting = state != null &&
                                state.TurnPlayer != player;
                utility += reacting ? 33000 : 16000;
            }
            if (Contains(text, "DESTROY") ||
                Contains(text, "BANISH") ||
                Contains(text, "RETURN") ||
                Contains(text, "SHUFFLE") &&
                Contains(text, "OPPONENT"))
            {
                utility += CountFieldCards(enemy) > 0 ? 29000 : -22000;
            }
            if (Contains(text, "FROM YOUR GY") ||
                Contains(text, "IN YOUR GY"))
            {
                utility += me != null && me.Graveyard.Count > 0
                    ? 17000
                    : -8000;
            }
            if (Contains(text, "PAY HALF YOUR LP") ||
                Contains(text, "PAY 2000 LP") ||
                Contains(text, "PAY 2500 LP"))
            {
                if (me != null && me.LifePoints < 3200)
                    utility -= 30000;
            }
            return utility;
        }

        private static int OptionUtility(
            DuelChoice choice,
            CardDatabase database)
        {
            string option = DescriptionText(choice.DescriptionId, database);
            int result = 0;
            if (Contains(option, "ADD") || Contains(option, "DRAW"))
                result += 18000;
            if (Contains(option, "SPECIAL SUMMON") ||
                Contains(option, "FUSION SUMMON"))
            {
                result += 22000;
            }
            if (Contains(option, "DESTROY") || Contains(option, "BANISH"))
                result += 15000;
            return result;
        }

        private static bool CanAttackProfitably(
            DuelPresentationState state,
            byte player,
            CardDatabase database)
        {
            if (state == null || player >= state.Players.Length)
                return false;
            DuelistState me = state.Players[player];
            var attackers = new List<int>();
            for (int index = 0; index < me.MonsterZones.Length; index++)
            {
                if (me.MonsterZones[index] == 0 ||
                    (me.MonsterPositions[index] & FaceUpAttack) == 0)
                {
                    continue;
                }
                int attack = AttackValue(me.MonsterZones[index], database);
                if (attack > 0) attackers.Add(attack);
            }
            if (attackers.Count == 0)
                return false;

            DuelistState enemy = state.Players[1 - player];
            var defenders = new List<int>();
            for (int index = 0; index < enemy.MonsterZones.Length; index++)
            {
                if (enemy.MonsterZones[index] == 0) continue;
                defenders.Add(BattleStat(
                    (byte)(1 - player),
                    (uint)index,
                    state,
                    database));
            }
            return defenders.Count == 0 || attackers.Max() > defenders.Min();
        }

        private static bool IsBehind(
            DuelPresentationState state,
            byte player,
            CardDatabase database)
        {
            if (state == null || player >= state.Players.Length)
                return false;
            DuelistState me = state.Players[player];
            DuelistState enemy = state.Players[1 - player];
            int myScore = me.LifePoints + BoardValue(me, database);
            int enemyScore = enemy.LifePoints + BoardValue(enemy, database);
            return myScore + 700 < enemyScore;
        }

        private static int BoardValue(
            DuelistState player,
            CardDatabase database)
        {
            if (player == null) return 0;
            int result = 0;
            foreach (uint code in player.MonsterZones)
                result += ThreatValue(code, database);
            foreach (uint code in player.SpellTrapZones)
                result += code == 0 ? 0 : 700;
            return result;
        }

        private static int EnemyBestStat(
            DuelPresentationState state,
            byte player,
            CardDatabase database)
        {
            if (state == null) return 0;
            DuelistState enemy = state.Players[1 - player];
            int best = 0;
            for (int index = 0; index < enemy.MonsterZones.Length; index++)
            {
                if (enemy.MonsterZones[index] == 0) continue;
                best = Math.Max(
                    best,
                    BattleStat(
                        (byte)(1 - player),
                        (uint)index,
                        state,
                        database));
            }
            return best;
        }

        private static int BattleStat(
            byte controller,
            uint sequence,
            DuelPresentationState state,
            CardDatabase database)
        {
            if (state == null ||
                controller >= state.Players.Length ||
                sequence >= state.Players[controller].MonsterZones.Length)
            {
                return 1800;
            }
            DuelistState player = state.Players[controller];
            uint code = player.MonsterZones[sequence];
            if (code == 0 || database == null ||
                !database.TryGet(code, out CardRecord card))
            {
                return 1800;
            }
            uint position = player.MonsterPositions[sequence];
            return (position & DefensePositions) != 0
                ? Math.Max(0, card.Defense)
                : Math.Max(0, card.Attack);
        }

        private static int CardStrategicValue(
            uint code,
            byte player,
            DuelPresentationState state,
            CardDatabase database)
        {
            if (code == 0 || database == null ||
                !database.TryGet(code, out CardRecord card))
            {
                return 0;
            }

            int combat = Math.Max(
                Math.Max(0, card.Attack),
                Math.Max(0, card.Defense));
            int value = combat + Math.Max(0, card.Level) * 42;
            if ((card.Type & Effect) != 0) value += 650;
            if ((card.Type & (Spell | Trap)) != 0) value += 820;
            if ((card.Type & ExtraTypes) != 0) value += 1000;

            string text = Fold(card.Description);
            if (Contains(text, "FROM YOUR DECK TO YOUR HAND") ||
                Contains(text, "ADD 1")) value += 1050;
            if (Contains(text, "DRAW")) value += 800;
            if (Contains(text, "SPECIAL SUMMON")) value += 950;
            if (Contains(text, "NEGATE")) value += 850;
            if (Contains(text, "DESTROY") || Contains(text, "BANISH"))
                value += 750;
            value += ArchetypeAffinity(card, player, state, database);
            return value;
        }

        private static int ArchetypeAffinity(
            CardRecord card,
            byte player,
            DuelPresentationState state,
            CardDatabase database)
        {
            if (card?.Setcodes == null ||
                card.Setcodes.Length == 0 ||
                state == null ||
                player >= state.Players.Length ||
                database == null)
            {
                return 0;
            }

            int matches = 0;
            foreach (uint code in VisibleCards(state.Players[player]))
            {
                if (code == 0 || code == card.Code ||
                    !database.TryGet(code, out CardRecord other) ||
                    other.Setcodes == null)
                {
                    continue;
                }
                if (card.Setcodes.Intersect(other.Setcodes).Any())
                    matches++;
            }
            return Math.Min(4, matches) * 240;
        }

        private static IEnumerable<uint> VisibleCards(DuelistState player)
        {
            return player.Hand
                .Concat(player.MonsterZones)
                .Concat(player.SpellTrapZones)
                .Concat(player.Graveyard)
                .Where(code => code != 0);
        }

        private static int ThreatValue(uint code, CardDatabase database)
        {
            if (code == 0 || database == null ||
                !database.TryGet(code, out CardRecord card))
            {
                return 0;
            }
            int value = Math.Max(
                Math.Max(0, card.Attack),
                Math.Max(0, card.Defense));
            if ((card.Type & Effect) != 0) value += 850;
            if ((card.Type & ExtraTypes) != 0) value += 900;
            return value;
        }

        private static int AttackValue(uint code, CardDatabase database)
        {
            return code != 0 && database != null &&
                   database.TryGet(code, out CardRecord card)
                ? Math.Max(0, card.Attack)
                : 0;
        }

        private static int CountFieldCards(DuelistState player)
        {
            if (player == null) return 0;
            return player.MonsterZones.Count(code => code != 0) +
                   player.SpellTrapZones.Count(code => code != 0);
        }

        private static bool HasConstructiveAlternative(DuelPrompt prompt)
        {
            return prompt.Choices.Any(choice =>
            {
                string label = Fold(choice.Label);
                return Contains(label, "INVOCAR") ||
                       Contains(label, "ATIVAR") ||
                       Contains(label, "BAIXAR") ||
                       Contains(label, "ATACAR") ||
                       Contains(label, "FASE DE BATALHA");
            });
        }

        private static string CardText(uint code, CardDatabase database)
        {
            return code != 0 && database != null &&
                   database.TryGet(code, out CardRecord card)
                ? Fold(card.Description)
                : string.Empty;
        }

        private static string DescriptionText(
            ulong descriptionId,
            CardDatabase database)
        {
            if (descriptionId == 0 || database == null)
                return string.Empty;
            uint code = (uint)(descriptionId >> 20);
            int index = checked((int)(descriptionId & 0xFFFFF));
            if (!database.TryGet(code, out CardRecord card) ||
                card.Strings == null ||
                index < 0 || index >= card.Strings.Length)
            {
                return string.Empty;
            }
            return Fold(card.Strings[index]);
        }

        private static bool Contains(string value, string fragment)
        {
            return value?.IndexOf(
                fragment,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Fold(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }
            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
