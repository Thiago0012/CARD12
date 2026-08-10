using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.Game.Tournaments;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class TournamentManagerEditModeTests
    {
        [Test]
        public void ConfigAcceptsEveryEvenParticipantCountAndRejectsOddCapacity()
        {
            TournamentConfig config = Config(4, TournamentFormatType.Points);
            config.bestOf = 2;
            Assert.That(
                TournamentManager.ValidateConfig(config).Success,
                Is.False);

            config.bestOf = 3;
            config.participantLimit = 6;
            Assert.That(
                TournamentManager.ValidateConfig(config).Success,
                Is.True);

            foreach (int supported in Enumerable.Range(1, 16)
                         .Select(value => value * 2))
            {
                config.participantLimit = supported;
                config.pointsRoundCount = Math.Min(
                    config.pointsRoundCount,
                    supported - 1);
                Assert.That(
                    TournamentManager.ValidateConfig(config).Success,
                    Is.True,
                    supported.ToString());
            }

            config.participantLimit = 5;
            Assert.That(
                TournamentManager.ValidateConfig(config).Success,
                Is.False);
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(6)]
        [TestCase(8)]
        [TestCase(10)]
        [TestCase(12)]
        [TestCase(16)]
        [TestCase(32)]
        public void EverySupportedEliminationSizeBuildsACompleteSafeBracket(
            int participants)
        {
            TournamentManager manager = ReadyManager(
                Config(participants, TournamentFormatType.SingleElimination));
            Assert.That(manager.StartTournament().Success, Is.True);
            Assert.That(
                manager.State.matches.Count(match =>
                    match.status != TournamentMatchStatus.Bye),
                Is.EqualTo(participants - 1));

            int resultIndex = 0;
            while (manager.State.config.status == TournamentStatus.InProgress)
            {
                List<TournamentMatch> ready = manager.State.matches
                    .Where(match => match.status == TournamentMatchStatus.Ready)
                    .ToList();
                Assert.That(ready, Is.Not.Empty);
                Assert.That(
                    ready.SelectMany(match => new[]
                        {
                            match.playerAId,
                            match.playerBId
                        })
                        .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
                        .GroupBy(playerId => playerId)
                        .All(group => group.Count() == 1),
                    Is.True,
                    "Um jogador não pode ter dois confrontos liberados ao mesmo tempo.");

                foreach (TournamentMatch match in ready)
                {
                    Assert.That(manager.SubmitGameResult(Result(
                        manager,
                        match,
                        match.playerAId,
                        "size-" + participants + "-" + resultIndex++)).Success,
                        Is.True);
                }
            }

            Assert.That(manager.State.config.status,
                Is.EqualTo(TournamentStatus.Completed));
            Assert.That(manager.State.stats.globalStats.totalMatches,
                Is.EqualTo(participants - 1));
        }

        [TestCase(4, 3)]
        [TestCase(8, 5)]
        [TestCase(8, 6)]
        [TestCase(16, 9)]
        public void EarlyStartUsesStrictMajorityAndLocksPresentPlayers(
            int capacity,
            int present)
        {
            TournamentConfig config = Config(
                capacity,
                TournamentFormatType.SingleElimination);
            config.allowEarlyStart = true;
            TournamentManager manager = ReadyManager(config, present);

            Assert.That(manager.CanStart(out string rejection),
                Is.True, rejection);
            Assert.That(manager.StartTournament().Success, Is.True);
            Assert.That(manager.State.players, Has.Count.EqualTo(present));
            Assert.That(manager.State.config.participantLimit,
                Is.EqualTo(capacity));

            CompleteAllReadyMatches(manager, "early");
            Assert.That(manager.State.config.status,
                Is.EqualTo(TournamentStatus.Completed));
            Assert.That(manager.State.stats.globalStats.totalMatches,
                Is.EqualTo(present - 1));
        }

        [Test]
        public void EarlyStartRejectsHalfFilledLobby()
        {
            TournamentConfig config = Config(
                8,
                TournamentFormatType.SingleElimination);
            config.allowEarlyStart = true;
            TournamentManager manager = ReadyManager(config, 4);

            Assert.That(manager.CanStart(out string rejection), Is.False);
            Assert.That(rejection, Does.Contain("maioria"));
        }

        [Test]
        public void SingleEliminationBo3ProgressesAndIgnoresDuplicateResult()
        {
            TournamentManager manager = ReadyManager(
                Config(4, TournamentFormatType.SingleElimination, 3));
            Assert.That(manager.StartTournament().Success, Is.True);

            List<TournamentMatch> semifinals = manager.State.matches
                .Where(match => match.roundNumber == 1)
                .ToList();
            Assert.That(semifinals, Has.Count.EqualTo(2));
            foreach (TournamentMatch match in semifinals)
            {
                TournamentMatchResult first = Result(
                    manager,
                    match,
                    match.playerAId,
                    "game-1");
                Assert.That(manager.SubmitGameResult(first).Success, Is.True);
                int revisionAfterFirst = manager.State.revision;
                Assert.That(manager.SubmitGameResult(first).Success, Is.True);
                Assert.That(manager.State.revision, Is.EqualTo(revisionAfterFirst));
                Assert.That(manager.SubmitGameResult(Result(
                    manager,
                    match,
                    match.playerAId,
                    "game-2")).Success, Is.True);
                Assert.That(match.status, Is.EqualTo(
                    TournamentMatchStatus.Finished));
            }

            TournamentMatch final = manager.State.matches.Single(match =>
                match.roundNumber == 2);
            Assert.That(final.status, Is.EqualTo(TournamentMatchStatus.Ready));
            Assert.That(manager.SubmitGameResult(Result(
                manager, final, final.playerAId, "final-1")).Success, Is.True);
            Assert.That(manager.SubmitGameResult(Result(
                manager, final, final.playerAId, "final-2")).Success, Is.True);

            Assert.That(manager.State.config.status,
                Is.EqualTo(TournamentStatus.Completed));
            Assert.That(manager.State.championPlayerId,
                Is.EqualTo(final.playerAId));
            Assert.That(manager.State.podiumPlayerIds, Has.Count.EqualTo(3));
            Assert.That(manager.State.stats.globalStats.totalMatches,
                Is.EqualTo(3));
            Assert.That(manager.State.stats.globalStats.totalDuels,
                Is.EqualTo(6));
        }

        [Test]
        public void PointsRoundRobinReleasesOneRoundAtATimeAndFindsChampion()
        {
            TournamentConfig config = Config(
                4,
                TournamentFormatType.Points,
                1);
            config.pointsRoundCount = 3;
            TournamentManager manager = ReadyManager(config);
            Assert.That(manager.StartTournament().Success, Is.True);
            Assert.That(manager.State.matches.Count, Is.EqualTo(6));

            int resultIndex = 0;
            while (manager.State.config.status == TournamentStatus.InProgress)
            {
                List<TournamentMatch> ready = manager.State.matches
                    .Where(match => match.status == TournamentMatchStatus.Ready)
                    .ToList();
                Assert.That(ready, Has.Count.EqualTo(2));
                foreach (TournamentMatch match in ready)
                {
                    string winner = match.Contains("p1")
                        ? "p1"
                        : match.playerAId;
                    Assert.That(manager.SubmitGameResult(Result(
                        manager,
                        match,
                        winner,
                        "points-" + resultIndex++)).Success, Is.True);
                }
            }

            Assert.That(manager.State.championPlayerId, Is.EqualTo("p1"));
            Assert.That(manager.State.FindPlayer("p1").seriesWins,
                Is.EqualTo(3));
            Assert.That(manager.OrderedStandings()[0].playerId,
                Is.EqualTo("p1"));
        }

        [TestCase(6, 6, 15)]
        [TestCase(8, 5, 10)]
        public void PointsModeCompletesWithEvenOrOddPresentPlayers(
            int capacity,
            int present,
            int expectedMatches)
        {
            TournamentConfig config = Config(
                capacity,
                TournamentFormatType.Points);
            config.allowEarlyStart = true;
            config.pointsRoundCount = present % 2 == 0
                ? present - 1
                : present;
            TournamentManager manager = ReadyManager(config, present);
            Assert.That(manager.StartTournament().Success, Is.True);
            Assert.That(manager.State.matches, Has.Count.EqualTo(expectedMatches));
            if (present % 2 != 0)
            {
                Assert.That(manager.State.rounds.All(round =>
                    !string.IsNullOrWhiteSpace(round.byePlayerId)), Is.True);
                Assert.That(manager.State.rounds
                    .Select(round => round.byePlayerId)
                    .Distinct(StringComparer.Ordinal)
                    .Count(), Is.EqualTo(present));
            }

            CompleteAllReadyMatches(manager, "points-flex");

            Assert.That(manager.State.config.status,
                Is.EqualTo(TournamentStatus.Completed));
            Assert.That(manager.State.stats.globalStats.totalMatches,
                Is.EqualTo(expectedMatches));
            Assert.That(manager.OrderedStandings(), Has.Count.EqualTo(present));
        }

        [Test]
        public void FutureByeFedMatchRejectsResultBeforeItsRoundIsReleased()
        {
            TournamentManager manager = ReadyManager(
                Config(10, TournamentFormatType.SingleElimination));
            Assert.That(manager.StartTournament().Success, Is.True);
            TournamentMatch future = manager.State.matches.First(match =>
                match.roundNumber == 2 && match.HasBothPlayers &&
                match.status == TournamentMatchStatus.Waiting);

            TournamentOperationResult result = manager.SubmitGameResult(Result(
                manager,
                future,
                future.playerAId,
                "too-early"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("não está liberado"));
        }

        [Test]
        public void CustomBanListAndSelectedPoolExplainInvalidDeck()
        {
            TournamentConfig config = Config(
                2,
                TournamentFormatType.SingleElimination);
            config.banListMode = TournamentBanListMode.Custom;
            config.customBanList.Add(new TournamentCardRestriction
            {
                cardId = "10000000",
                maximumCopies = 1
            });
            TournamentDeckManifest restricted = ValidDeck(10000000);
            restricted.mainDeckCardIds[1] = "10000000";
            restricted.sha256 = string.Empty;
            TournamentDeckValidationResult banResult =
                TournamentDeckRulesValidator.Validate(restricted, config);
            Assert.That(banResult.IsValid, Is.False);
            Assert.That(banResult.Summary, Does.Contain("limitada a 1"));

            config.banListMode = TournamentBanListMode.None;
            config.allowedCardPoolMode =
                TournamentCardPoolMode.SelectedCardsOnly;
            config.allowedCardIds = restricted.mainDeckCardIds
                .Take(39)
                .Distinct()
                .ToList();
            TournamentDeckManifest outsidePool = ValidDeck(20000000);
            TournamentDeckValidationResult poolResult =
                TournamentDeckRulesValidator.Validate(outsidePool, config);
            Assert.That(poolResult.IsValid, Is.False);
            Assert.That(poolResult.Summary, Does.Contain("pool permitido"));
        }

        [Test]
        public void LockedDeckCannotChangeAfterTournamentStarts()
        {
            TournamentManager manager = ReadyManager(
                Config(2, TournamentFormatType.SingleElimination));
            Assert.That(manager.StartTournament().Success, Is.True);
            TournamentDeckManifest changed = ValidDeck(30000000);
            TournamentOperationResult result = manager.AddOrUpdateParticipant(
                "p1",
                "Player 1",
                changed);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("bloqueado"));
        }

        [Test]
        public void OrganizerWalkoverClosesTheSeriesAndAdvancesTheBracket()
        {
            TournamentConfig config = Config(
                4,
                TournamentFormatType.SingleElimination,
                5);
            config.allowWalkover = true;
            config.pointsPerWalkover = 3;
            TournamentManager manager = ReadyManager(config);
            Assert.That(manager.StartTournament().Success, Is.True);

            TournamentMatch semifinal = manager.State.matches.First(match =>
                match.roundNumber == 1);
            Assert.That(manager.AwardWalkover(
                semifinal.matchId,
                semifinal.playerBId).Success, Is.True);
            Assert.That(semifinal.status,
                Is.EqualTo(TournamentMatchStatus.Finished));
            Assert.That(semifinal.winnerId, Is.EqualTo(semifinal.playerBId));
            Assert.That(semifinal.gamesWonByB, Is.EqualTo(1));
            Assert.That(manager.State.FindPlayer(semifinal.playerBId).points,
                Is.EqualTo(3));
            Assert.That(manager.State.results.Last().walkover, Is.True);
        }

        [Test]
        public void FailedRelayMatchCanBeReopenedWithoutChangingTheScore()
        {
            TournamentManager manager = ReadyManager(
                Config(2, TournamentFormatType.SingleElimination, 3));
            Assert.That(manager.StartTournament().Success, Is.True);
            TournamentMatch match = manager.State.matches.Single();
            Assert.That(manager.SetMatchRelayRoom(
                match.matchId,
                match.playerAId,
                "ABC123").Success, Is.True);
            Assert.That(manager.MarkMatchInProgress(
                match.matchId,
                match.playerAId).Success, Is.True);

            TournamentOperationResult reopened = manager.ReopenMatch(
                match.matchId,
                "falha de conexão");

            Assert.That(reopened.Success, Is.True);
            Assert.That(match.status, Is.EqualTo(TournamentMatchStatus.Ready));
            Assert.That(match.relayRoomCode, Is.Empty);
            Assert.That(match.gamesWonByA, Is.Zero);
            Assert.That(match.gamesWonByB, Is.Zero);
        }

        [Test]
        public void PersistenceRoundTripFallsBackToLastGoodBackup()
        {
            string directory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Temp",
                "TournamentTests",
                Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "tournaments.json");
            try
            {
                var store = new TournamentPersistenceStore(path);
                TournamentManager manager = ReadyManager(
                    Config(2, TournamentFormatType.SingleElimination));
                var envelope = new TournamentPersistenceEnvelope();
                var ticket = new TournamentConnectionTicket
                {
                    tournamentId = manager.State.config.tournamentId,
                    lobbyId = "lobby-id",
                    lobbyCode = "ABC123",
                    localPlayerId = "p1"
                };
                store.SaveActive(envelope, manager.State, ticket);
                manager.State.config.description = "segunda versão";
                store.SaveActive(envelope, manager.State, ticket);
                File.WriteAllText(path, "{corrompido");

                TournamentPersistenceEnvelope loaded = store.Load();
                Assert.That(loaded.activeTournament, Is.Not.Null);
                Assert.That(loaded.connectionTicket.lobbyCode,
                    Is.EqualTo("ABC123"));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        private static TournamentManager ReadyManager(
            TournamentConfig config,
            int? presentCount = null)
        {
            TournamentManager manager = TournamentManager.Create(config);
            int count = presentCount ?? config.participantLimit;
            for (int index = 1; index <= count; index++)
            {
                TournamentOperationResult added =
                    manager.AddOrUpdateParticipant(
                        "p" + index,
                        "Player " + index,
                        ValidDeck(10000000 + index * 100),
                        index == 1);
                Assert.That(added.Success, Is.True, added.Message);
            }
            return manager;
        }

        private static void CompleteAllReadyMatches(
            TournamentManager manager,
            string prefix)
        {
            int resultIndex = 0;
            while (manager.State.config.status == TournamentStatus.InProgress)
            {
                List<TournamentMatch> ready = manager.State.matches
                    .Where(match => match.status == TournamentMatchStatus.Ready)
                    .ToList();
                Assert.That(ready, Is.Not.Empty);
                Assert.That(ready.SelectMany(match => new[]
                    {
                        match.playerAId,
                        match.playerBId
                    })
                    .GroupBy(playerId => playerId)
                    .All(group => group.Count() == 1), Is.True);
                foreach (TournamentMatch match in ready)
                {
                    Assert.That(manager.SubmitGameResult(Result(
                        manager,
                        match,
                        match.playerAId,
                        prefix + "-" + resultIndex++)).Success, Is.True);
                }
            }
        }

        private static TournamentConfig Config(
            int participants,
            TournamentFormatType format,
            int bestOf = 1)
        {
            return new TournamentConfig
            {
                tournamentId = Guid.NewGuid().ToString("N"),
                name = "Torneio de Teste",
                formatType = format,
                participantLimit = participants,
                bestOf = bestOf,
                pointsRoundCount = Math.Max(1, participants - 1),
                banListMode = TournamentBanListMode.None,
                allowedCardPoolMode = TournamentCardPoolMode.AllCards,
                bracketSeed = 12345
            };
        }

        private static TournamentDeckManifest ValidDeck(int firstCode)
        {
            var cards = new List<string>();
            for (int index = 0; index < 40; index++)
                cards.Add((firstCode + index).ToString("00000000"));
            var deck = new TournamentDeckManifest
            {
                deckId = "deck-" + firstCode,
                displayName = "Deck " + firstCode,
                banListId = string.Empty,
                mainDeckCardIds = cards
            };
            TournamentDeckRulesValidator.Validate(
                deck,
                new TournamentConfig
                {
                    name = "Hash",
                    participantLimit = 2,
                    bestOf = 1,
                    banListMode = TournamentBanListMode.None
                });
            return deck;
        }

        private static TournamentMatchResult Result(
            TournamentManager manager,
            TournamentMatch match,
            string winner,
            string suffix)
        {
            string loser = string.Equals(winner, match.playerAId,
                StringComparison.Ordinal)
                ? match.playerBId
                : match.playerAId;
            return new TournamentMatchResult
            {
                resultId = match.matchId + ":" + suffix,
                tournamentId = manager.State.config.tournamentId,
                roundId = match.roundId,
                matchId = match.matchId,
                playerAId = match.playerAId,
                playerBId = match.playerBId,
                winnerId = winner,
                loserId = loser,
                finishedAtUtcTicks = DateTime.UtcNow.Ticks
            };
        }
    }
}
