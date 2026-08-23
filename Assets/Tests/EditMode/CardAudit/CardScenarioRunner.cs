using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;

namespace ArcaneDuel.Tests.EditMode.CardAudit
{
    /// <summary>
    /// Deterministic, headless infrastructure runner used before promoting a
    /// card from CARREGA to any semantic audit status. It intentionally drives
    /// both seats through the authoritative Core and never fabricates Unity
    /// state. Card-specific rule assertions remain in dedicated scenarios.
    /// </summary>
    public sealed class CardScenarioRunner
    {
        private const uint StableMainDeckFiller = 1784619; // Uraby.
        private const int DefaultDecisionLimit = 640;

        public CardScenarioRunResult Run(
            uint cardCode,
            ulong seed,
            int targetTurns = 4,
            int decisionLimit = DefaultDecisionLimit)
        {
            if (targetTurns < 2)
                throw new ArgumentOutOfRangeException(nameof(targetTurns));
            if (decisionLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(decisionLimit));

            CardDatabase database = CardDatabase.LoadDefault();
            CardRecord subject = database.Get(cardCode);
            bool isExtraDeck = DeckRules.IsExtraDeck(subject);
            BuildDecks(cardCode, isExtraDeck, out uint[] main, out uint[] extra);

            var result = new CardScenarioRunResult
            {
                CardCode = cardCode,
                CardName = subject.Name,
                IsExtraDeck = isExtraDeck,
                Seed = seed,
                TargetTurns = targetTurns
            };
            var signature = new StringBuilder(16 * 1024);
            var configuration = new DuelConfiguration
            {
                Seed = seed,
                StartingLifePoints = 20000,
                PlayerDeck = (uint[])main.Clone(),
                OpponentDeck = (uint[])main.Clone(),
                PlayerExtraDeck = (uint[])extra.Clone(),
                OpponentExtraDeck = (uint[])extra.Clone(),
                SimpleOpponentAi = false,
                ShuffleMainDecks = false
            };

            try
            {
                using (OcgDuelEngine engine =
                       OcgDuelEngine.CreateDefault(configuration))
                {
                    engine.EventReceived += duelEvent =>
                    {
                        AppendEvent(signature, duelEvent);
                        if (duelEvent.Message == CoreMessage.NewTurn)
                            result.Turns++;
                        if (duelEvent.Message == CoreMessage.Retry)
                            result.Retries++;
                        if (duelEvent.IsUnknown)
                            result.UnknownMessages++;
                        if (ReferencesCard(duelEvent, cardCode))
                            result.SubjectObserved = true;
                    };

                    engine.Start();
                    CaptureSubject(engine, cardCode, result);
                    while (!engine.IsFinished &&
                           result.Turns < targetTurns &&
                           result.Decisions < decisionLimit)
                    {
                        DuelPrompt prompt = engine.CurrentPrompt;
                        if (prompt == null)
                        {
                            result.UntypedPrompt = true;
                            break;
                        }

                        if (prompt.Choices.Any(choice =>
                                choice.CardCode == cardCode))
                        {
                            result.SubjectOfferedAsLegalChoice = true;
                            result.SubjectObserved = true;
                        }

                        DuelChoice choice = Choose(prompt, cardCode);
                        if (choice?.Response == null ||
                            choice.Response.Length == 0)
                        {
                            result.Failure =
                                $"Prompt {prompt.Message} returned no legal response.";
                            break;
                        }

                        if (prompt.Player == 0)
                            result.PlayerOneDecisions++;
                        else if (prompt.Player == 1)
                            result.PlayerTwoDecisions++;

                        AppendChoice(signature, prompt, choice);
                        result.Decisions++;
                        engine.SubmitResponse(choice.Response);
                    }

                    CaptureSubject(engine, cardCode, result);
                    result.FinishedWithWinner = engine.IsFinished;
                    result.NativeFailures = engine.NativeLogs
                        .Where(log => IsNativeFailure(log, database))
                        .ToArray();
                }
            }
            catch (Exception exception)
            {
                result.Failure = exception.GetType().Name + ": " +
                                 exception.Message;
            }

            result.Fingerprint = signature.ToString();
            result.FingerprintSha256 = Sha256(result.Fingerprint);
            result.Completed =
                string.IsNullOrEmpty(result.Failure) &&
                !result.UntypedPrompt &&
                result.Retries == 0 &&
                result.UnknownMessages == 0 &&
                result.NativeFailures.Length == 0 &&
                result.SubjectObserved &&
                (result.FinishedWithWinner || result.Turns >= targetTurns);
            return result;
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static void BuildDecks(
            uint subject,
            bool isExtraDeck,
            out uint[] main,
            out uint[] extra)
        {
            main = Enumerable.Repeat(StableMainDeckFiller, 40).ToArray();
            if (!isExtraDeck)
                main[0] = subject;
            extra = isExtraDeck ? new[] { subject } : Array.Empty<uint>();
        }

        private static DuelChoice Choose(DuelPrompt prompt, uint subject)
        {
            DuelChoice subjectChoice = prompt.Choices.FirstOrDefault(choice =>
                choice.CardCode == subject &&
                choice.Response != null &&
                choice.Response.Length > 0);
            if (subjectChoice != null &&
                (prompt.Message == CoreMessage.SelectIdleCommand ||
                 prompt.Message == CoreMessage.SelectBattleCommand ||
                 prompt.Message == CoreMessage.SelectChain ||
                 (prompt.Message == CoreMessage.SelectCard &&
                  prompt.MinimumSelections <= 1)))
            {
                return subjectChoice;
            }

            if (prompt.Message == CoreMessage.SelectEffectYesNo ||
                prompt.Message == CoreMessage.SelectYesNo)
            {
                DuelChoice affirmative = prompt.Choices.FirstOrDefault(choice =>
                    choice.Response != null &&
                    choice.Response.Length == sizeof(int) &&
                    ReadInt32(choice.Response) != 0);
                if (affirmative != null)
                    return affirmative;
            }

            return DeterministicDuelPolicy.Choose(prompt);
        }

        private static int ReadInt32(byte[] bytes)
        {
            return bytes[0] |
                   (bytes[1] << 8) |
                   (bytes[2] << 16) |
                   (bytes[3] << 24);
        }

        private static void CaptureSubject(
            OcgDuelEngine engine,
            uint subject,
            CardScenarioRunResult result)
        {
            if (!engine.TryCaptureFieldSnapshot(out OcgFieldSnapshot snapshot))
                return;
            result.SnapshotsCaptured++;
            if (snapshot.Players.Any(player => PlayerContains(player, subject)))
                result.SubjectObserved = true;
        }

        private static bool PlayerContains(
            OcgDuelistFieldSnapshot player,
            uint subject)
        {
            return Contains(player.Deck, subject) ||
                   Contains(player.Hand, subject) ||
                   Contains(player.Monsters, subject) ||
                   Contains(player.Spells, subject) ||
                   Contains(player.Graveyard, subject) ||
                   Contains(player.Banished, subject) ||
                   Contains(player.Extra, subject);
        }

        private static bool Contains(
            IEnumerable<OcgFieldCardSnapshot> cards,
            uint subject)
        {
            return cards != null && cards.Any(card =>
                card != null && card.Code == subject);
        }

        private static bool ReferencesCard(DuelEvent duelEvent, uint subject)
        {
            return duelEvent.Code == subject ||
                   (duelEvent.Codes?.Contains(subject) ?? false) ||
                   (duelEvent.Prompt?.Choices.Any(choice =>
                       choice.CardCode == subject) ?? false);
        }

        private static bool IsNativeFailure(string log, CardDatabase database)
        {
            if (string.IsNullOrEmpty(log))
                return false;
            if (log.StartsWith("[0]", StringComparison.Ordinal))
                return true;
            if (!log.StartsWith("SCRIPT_MISSING", StringComparison.Ordinal))
                return false;

            // The native loader reports missing c0.lua and scriptless Normal
            // Monsters while constructing a duel. Neither represents a broken
            // effect. Keep failing for every published card whose type really
            // requires an effect script, so this filter cannot hide missing Lua.
            int codeStart = log.IndexOf('c');
            if (codeStart < 0)
                return true;
            codeStart++;
            int codeEnd = codeStart;
            while (codeEnd < log.Length && char.IsDigit(log[codeEnd]))
                codeEnd++;
            if (codeEnd == codeStart ||
                !uint.TryParse(log.Substring(codeStart, codeEnd - codeStart),
                    out uint code))
            {
                return true;
            }
            if (code == 0)
                return false;
            return !database.TryGet(code, out CardRecord record) ||
                   DuelContentValidator.RequiresScript(record);
        }

        private static void AppendEvent(
            StringBuilder signature,
            DuelEvent duelEvent)
        {
            signature.Append("E:")
                .Append(duelEvent.RawMessage).Append(':')
                .Append(duelEvent.Player).Append(':')
                .Append(duelEvent.Value).Append(':')
                .Append(duelEvent.Code).Append(':')
                .Append(duelEvent.Previous?.Controller ?? 255).Append('/')
                .Append(duelEvent.Previous?.Location ?? 0).Append('/')
                .Append(duelEvent.Previous?.Sequence ?? 0).Append(':')
                .Append(duelEvent.Current?.Controller ?? 255).Append('/')
                .Append(duelEvent.Current?.Location ?? 0).Append('/')
                .Append(duelEvent.Current?.Sequence ?? 0).Append(':')
                .Append(duelEvent.Prompt?.Message.ToString() ?? "-")
                .Append(':')
                .Append(duelEvent.Prompt?.Choices.Count ?? 0)
                .AppendLine();
        }

        private static void AppendChoice(
            StringBuilder signature,
            DuelPrompt prompt,
            DuelChoice choice)
        {
            signature.Append("R:")
                .Append(prompt.Message).Append(':')
                .Append(prompt.Player).Append(':')
                .Append(choice.CardCode).Append(':')
                .Append(Convert.ToBase64String(choice.Response))
                .AppendLine();
        }
    }

    public sealed class CardScenarioRunResult
    {
        public uint CardCode { get; internal set; }
        public string CardName { get; internal set; }
        public bool IsExtraDeck { get; internal set; }
        public ulong Seed { get; internal set; }
        public int TargetTurns { get; internal set; }
        public int Turns { get; internal set; }
        public int Decisions { get; internal set; }
        public int PlayerOneDecisions { get; internal set; }
        public int PlayerTwoDecisions { get; internal set; }
        public int Retries { get; internal set; }
        public int UnknownMessages { get; internal set; }
        public int SnapshotsCaptured { get; internal set; }
        public bool SubjectObserved { get; internal set; }
        public bool SubjectOfferedAsLegalChoice { get; internal set; }
        public bool UntypedPrompt { get; internal set; }
        public bool FinishedWithWinner { get; internal set; }
        public bool Completed { get; internal set; }
        public string Failure { get; internal set; }
        public string Fingerprint { get; internal set; }
        public string FingerprintSha256 { get; internal set; }
        public string[] NativeFailures { get; internal set; } =
            Array.Empty<string>();

        public string Diagnostic =>
            $"{CardName} ({CardCode:00000000}); seed={Seed:X16}; " +
            $"turns={Turns}/{TargetTurns}; decisions={Decisions}; " +
            $"p1={PlayerOneDecisions}; p2={PlayerTwoDecisions}; " +
            $"retry={Retries}; unknown={UnknownMessages}; " +
            $"snapshots={SnapshotsCaptured}; observed={SubjectObserved}; " +
            $"offered={SubjectOfferedAsLegalChoice}; " +
            $"untyped={UntypedPrompt}; winner={FinishedWithWinner}; " +
            $"trace={FingerprintSha256}; " +
            $"native={string.Join(" | ", NativeFailures)}; " +
            $"failure={Failure ?? "none"}";
    }
}
