using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneDuel.Game.Tournaments
{
    /// <summary>
    /// Autoridade determinística do campeonato. Ela não conhece cenas, UI,
    /// Lobby ou Relay: recebe participantes/resultados validados e produz a
    /// próxima chave/classificação.
    /// </summary>
    public sealed class TournamentManager
    {
        private static readonly string[] PlayerColors =
        {
            "#34DDF4", "#F2C766", "#C8FF19", "#FF6B8A",
            "#7E8CFF", "#D978FF", "#3EE2A7", "#FF9C4A",
            "#61B8FF", "#E7E7E7", "#B1FF74", "#FFCF4F",
            "#75F1E7", "#D18DFF", "#FF829B", "#7AA5FF"
        };

        private bool processingResult;

        public TournamentManager(TournamentState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            NormalizeState(State);
        }

        public TournamentState State { get; private set; }

        public static TournamentOperationResult ValidateConfig(
            TournamentConfig config)
        {
            if (config == null)
                return TournamentOperationResult.Fail(
                    "As configurações do torneio estão vazias.");
            config.Normalize();
            if (config.name.Length < 3 || config.name.Length > 48)
            {
                return TournamentOperationResult.Fail(
                    "O nome do torneio deve ter de 3 a 48 caracteres.");
            }
            if (!IsSupportedParticipantLimit(config.participantLimit))
            {
                return TournamentOperationResult.Fail(
                    "Escolha um número par de participantes entre 2 e 32.");
            }
            if (config.bestOf < 1 || config.bestOf > 15 ||
                config.bestOf % 2 == 0)
            {
                return TournamentOperationResult.Fail(
                    "Best of N deve ser ímpar, entre Bo1 e Bo15.");
            }
            if (config.formatType == TournamentFormatType.Points &&
                (config.pointsRoundCount < 1 ||
                 config.pointsRoundCount >= config.participantLimit))
            {
                return TournamentOperationResult.Fail(
                    $"No modo por pontos, use de 1 a " +
                    $"{config.participantLimit - 1} rodadas.");
            }
            if (config.allowedCardPoolMode ==
                    TournamentCardPoolMode.SelectedCardsOnly &&
                config.allowedCardIds.Count == 0)
            {
                return TournamentOperationResult.Fail(
                    "O pool selecionado precisa conter pelo menos uma carta.");
            }
            if (config.banListMode == TournamentBanListMode.Custom &&
                config.customBanList.Any(entry =>
                    entry == null ||
                    string.IsNullOrWhiteSpace(entry.cardId) ||
                    entry.maximumCopies < 0 ||
                    entry.maximumCopies > 3))
            {
                return TournamentOperationResult.Fail(
                    "A ban list personalizada contém uma regra inválida.");
            }
            if (config.matchTimeoutMinutes < 5 ||
                config.matchTimeoutMinutes > 240)
            {
                return TournamentOperationResult.Fail(
                    "O timeout deve ficar entre 5 e 240 minutos.");
            }
            return TournamentOperationResult.Ok("Configuração válida.");
        }

        public static TournamentManager Create(TournamentConfig config)
        {
            TournamentOperationResult validation = ValidateConfig(config);
            if (!validation.Success)
                throw new ArgumentException(validation.Message, nameof(config));

            config.status = TournamentStatus.Lobby;
            if (config.bracketSeed == 0)
                config.bracketSeed = StableSeed(config.tournamentId);
            var state = new TournamentState
            {
                config = config,
                createdAtUtcTicks = DateTime.UtcNow.Ticks,
                stats = new TournamentStats
                {
                    tournamentId = config.tournamentId
                }
            };
            return new TournamentManager(state);
        }

        public TournamentOperationResult AddOrUpdateParticipant(
            string playerId,
            string displayName,
            TournamentDeckManifest deck,
            bool organizer = false,
            string avatarId = "default",
            bool ready = true)
        {
            playerId = playerId?.Trim() ?? string.Empty;
            displayName = CollapseWhitespace(displayName);
            if (string.IsNullOrWhiteSpace(playerId))
                return TournamentOperationResult.Fail("PlayerId ausente.");
            if (displayName.Length < 1 || displayName.Length > 32)
                return TournamentOperationResult.Fail("Nome de jogador inválido.");
            if (State.config.status != TournamentStatus.Lobby)
            {
                TournamentPlayer locked = State.FindPlayer(playerId);
                if (locked == null)
                {
                    return TournamentOperationResult.Fail(
                        "O torneio já começou e não aceita novos jogadores.");
                }
                string receivedHash = deck?.sha256 ?? string.Empty;
                if (State.config.deckLocked &&
                    !string.Equals(
                        locked.lockedDeckHash,
                        receivedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return TournamentOperationResult.Fail(
                        "O deck está bloqueado desde o início do torneio.");
                }
                return TournamentOperationResult.Ok(
                    "O participante já está registrado.");
            }

            TournamentDeckValidationResult validation =
                TournamentDeckRulesValidator.Validate(deck, State.config);
            TournamentPlayer existing = State.FindPlayer(playerId);
            if (existing == null)
            {
                if (State.players.Count >= State.config.participantLimit)
                    return TournamentOperationResult.Fail("O torneio está lotado.");
                existing = new TournamentPlayer
                {
                    playerId = playerId,
                    colorTag = PlayerColors[
                        State.players.Count % PlayerColors.Length],
                    statsId = $"{State.config.tournamentId}:{playerId}",
                    isOrganizer = organizer
                };
                State.players.Add(existing);
                State.stats.perPlayerStats.Add(
                    new TournamentPlayerStats { playerId = playerId });
            }

            existing.displayName = displayName;
            existing.avatarId = avatarId ?? "default";
            existing.deck = deck ?? new TournamentDeckManifest();
            existing.deckId = existing.deck.deckId;
            existing.deckName = existing.deck.displayName;
            existing.deckHash = existing.deck.sha256;
            existing.deckValid = validation.IsValid;
            existing.deckValidationMessage = validation.Summary;
            existing.isReady = validation.IsValid && ready;
            existing.isOrganizer |= organizer;
            existing.isOnline = true;
            existing.status = existing.isReady
                ? TournamentPlayerStatus.Ready
                : TournamentPlayerStatus.Waiting;
            if (organizer)
            {
                State.organizerPlayerId = playerId;
                foreach (TournamentPlayer player in State.players)
                    player.isOrganizer = player == existing;
            }
            RecalculateDeckPresence();
            Touch();
            return validation.IsValid
                ? TournamentOperationResult.Ok("Deck validado e jogador pronto.")
                : TournamentOperationResult.Fail(validation.Summary);
        }

        public TournamentOperationResult SetParticipantReady(
            string playerId,
            bool ready)
        {
            if (State.config.status != TournamentStatus.Lobby)
            {
                return TournamentOperationResult.Fail(
                    "O status de pronto só pode mudar antes do início.");
            }
            TournamentPlayer player = State.FindPlayer(playerId);
            if (player == null)
                return TournamentOperationResult.Fail("Participante não encontrado.");
            if (ready && (!player.deckValid || player.deck == null))
            {
                return TournamentOperationResult.Fail(
                    "Corrija o deck antes de confirmar presença.");
            }
            player.isReady = ready;
            player.status = ready
                ? TournamentPlayerStatus.Ready
                : TournamentPlayerStatus.Waiting;
            Touch();
            return TournamentOperationResult.Ok(
                ready ? "Participante pronto." : "Participante aguardando.");
        }

        public TournamentOperationResult RemoveParticipant(string playerId)
        {
            if (State.config.status != TournamentStatus.Lobby)
            {
                return TournamentOperationResult.Fail(
                    "Participantes não podem ser removidos após o início.");
            }
            TournamentPlayer player = State.FindPlayer(playerId);
            if (player == null)
                return TournamentOperationResult.Ok("Participante já estava ausente.");
            if (player.isOrganizer)
            {
                return TournamentOperationResult.Fail(
                    "O organizador deve cancelar o torneio em vez de sair.");
            }
            State.players.Remove(player);
            State.stats.perPlayerStats.RemoveAll(item =>
                item != null && string.Equals(
                    item.playerId,
                    playerId,
                    StringComparison.Ordinal));
            RecalculateDeckPresence();
            Touch();
            return TournamentOperationResult.Ok("Participante removido.");
        }

        public TournamentOperationResult UpdateLobbyConfig(
            TournamentConfig updated)
        {
            if (State.config.status != TournamentStatus.Lobby)
            {
                return TournamentOperationResult.Fail(
                    "Regras competitivas ficam bloqueadas após o início.");
            }
            if (updated == null)
                return TournamentOperationResult.Fail("Configuração ausente.");
            updated.tournamentId = State.config.tournamentId;
            updated.status = TournamentStatus.Lobby;
            TournamentOperationResult validation = ValidateConfig(updated);
            if (!validation.Success)
                return validation;
            if (updated.participantLimit < State.players.Count)
            {
                return TournamentOperationResult.Fail(
                    "A nova quantidade de vagas é menor que o lobby atual.");
            }

            State.config = updated;
            foreach (TournamentPlayer player in State.players)
            {
                TournamentDeckValidationResult deckValidation =
                    TournamentDeckRulesValidator.Validate(
                        player.deck,
                        State.config);
                player.deckValid = deckValidation.IsValid;
                player.deckValidationMessage = deckValidation.Summary;
                player.isReady &= deckValidation.IsValid;
                player.status = player.isReady
                    ? TournamentPlayerStatus.Ready
                    : TournamentPlayerStatus.Waiting;
            }
            RecalculateDeckPresence();
            Touch();
            return TournamentOperationResult.Ok(
                "Configuração do lobby atualizada e decks revalidados.");
        }

        public TournamentOperationResult SetPlayerOnline(
            string playerId,
            bool online)
        {
            TournamentPlayer player = State.FindPlayer(playerId);
            if (player == null)
                return TournamentOperationResult.Fail("Participante não encontrado.");
            player.isOnline = online;
            if (!online && !player.isEliminated)
                player.status = TournamentPlayerStatus.Offline;
            else if (online && !player.isEliminated)
                player.status = player.isReady
                    ? TournamentPlayerStatus.Ready
                    : TournamentPlayerStatus.Waiting;
            Touch();
            return TournamentOperationResult.Ok();
        }

        public bool CanStart(out string rejection)
        {
            rejection = string.Empty;
            if (State.config.status != TournamentStatus.Lobby)
            {
                rejection = "O torneio não está no lobby.";
                return false;
            }
            TournamentOperationResult configValidation =
                ValidateConfig(State.config);
            if (!configValidation.Success)
            {
                rejection = configValidation.Message;
                return false;
            }
            if (State.players.Count < 2)
            {
                rejection = "Aguardando pelo menos 2 participantes.";
                return false;
            }
            if (State.players.Count > State.config.participantLimit)
            {
                rejection =
                    "A quantidade de jogadores ultrapassou as vagas da sala.";
                return false;
            }
            if (State.players.Count != State.config.participantLimit &&
                !State.config.allowEarlyStart)
            {
                rejection =
                    $"Aguardando {State.config.participantLimit - State.players.Count} " +
                    "participante(s). Ative o início antecipado para fechar " +
                    "a sala com quem já está pronto.";
                return false;
            }
            int earlyStartMinimum = State.config.participantLimit / 2 + 1;
            if (State.players.Count != State.config.participantLimit &&
                State.config.allowEarlyStart &&
                State.players.Count < earlyStartMinimum)
            {
                rejection =
                    $"O início antecipado exige maioria: {earlyStartMinimum} de " +
                    $"{State.config.participantLimit} participantes.";
                return false;
            }
            if (State.players.Any(player => player == null))
            {
                rejection = "Há um participante inválido.";
                return false;
            }
            TournamentPlayer offline = State.players.Find(player =>
                !player.isOnline);
            if (offline != null)
            {
                rejection =
                    $"{offline.displayName} está offline.";
                return false;
            }
            TournamentPlayer invalidDeck = State.players.Find(player =>
                !player.deckValid || player.deck == null);
            if (invalidDeck != null)
            {
                rejection = string.IsNullOrWhiteSpace(
                        invalidDeck.deckValidationMessage)
                    ? $"{invalidDeck.displayName} está com deck inválido."
                    : $"{invalidDeck.displayName}: " +
                      invalidDeck.deckValidationMessage;
                return false;
            }
            TournamentPlayer waiting = State.players.Find(player =>
                !player.isReady);
            if (waiting != null)
            {
                rejection =
                    $"{waiting.displayName} precisa confirmar PRONTO.";
                return false;
            }
            if (State.players.Select(player => player.playerId)
                .Distinct(StringComparer.Ordinal).Count() != State.players.Count)
            {
                rejection = "Existem participantes duplicados.";
                return false;
            }
            return true;
        }

        public TournamentOperationResult StartTournament()
        {
            if (!CanStart(out string rejection))
                return TournamentOperationResult.Fail(rejection);

            // Ao iniciar antecipadamente, os participantes presentes passam a
            // ser a lista oficial. A capacidade original permanece registrada,
            // mas a publicação seguinte bloqueia o Lobby e entradas tardias.
            if (State.config.allowEarlyStart &&
                State.players.Count < State.config.participantLimit)
            {
                State.config.pointsRoundCount = Math.Min(
                    State.config.pointsRoundCount,
                    Math.Max(1, State.players.Count % 2 == 0
                        ? State.players.Count - 1
                        : State.players.Count));
            }

            foreach (TournamentPlayer player in State.players)
            {
                player.lockedDeckHash = State.config.deckLocked
                    ? player.deckHash
                    : string.Empty;
                player.isEliminated = false;
                player.status = TournamentPlayerStatus.Ready;
            }
            State.rounds.Clear();
            State.matches.Clear();
            State.results.Clear();
            State.currentRoundNumber = 1;
            State.startedAtUtcTicks = DateTime.UtcNow.Ticks;
            State.config.status = TournamentStatus.InProgress;

            List<TournamentPlayer> seeded = SeedPlayers(State.players);
            if (State.config.formatType == TournamentFormatType.SingleElimination)
                BuildSingleElimination(seeded);
            else
                BuildPointsSchedule(seeded);
            SetRoundReady(1);
            UpdateRankings();
            Touch();
            return TournamentOperationResult.Ok("Torneio iniciado.");
        }

        public TournamentMatch ActiveMatchForPlayer(string playerId)
        {
            return State.matches
                .Where(match => match != null && match.Contains(playerId) &&
                    match.status != TournamentMatchStatus.Finished &&
                    match.status != TournamentMatchStatus.Invalid &&
                    match.status != TournamentMatchStatus.Bye)
                .OrderBy(match => match.roundNumber)
                .ThenBy(match => match.bracketIndex)
                .FirstOrDefault();
        }

        public TournamentOperationResult SetMatchRelayRoom(
            string matchId,
            string hostPlayerId,
            string roomCode)
        {
            TournamentMatch match = State.FindMatch(matchId);
            if (match == null)
                return TournamentOperationResult.Fail("Confronto não encontrado.");
            if (match.status != TournamentMatchStatus.Ready &&
                match.status != TournamentMatchStatus.InProgress)
            {
                return TournamentOperationResult.Fail(
                    "O confronto ainda não está liberado.");
            }
            if (!string.Equals(
                    match.playerAId,
                    hostPlayerId,
                    StringComparison.Ordinal))
            {
                return TournamentOperationResult.Fail(
                    "Apenas o Jogador A deste confronto pode criar a sala Relay.");
            }
            match.relayHostPlayerId = hostPlayerId;
            match.relayRoomCode = (roomCode ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
            match.updateRevision++;
            Touch();
            return TournamentOperationResult.Ok("Sala do confronto publicada.");
        }

        public TournamentOperationResult MarkMatchInProgress(
            string matchId,
            string playerId)
        {
            TournamentMatch match = State.FindMatch(matchId);
            if (match == null || !match.Contains(playerId))
                return TournamentOperationResult.Fail("Confronto inválido.");
            if (match.status != TournamentMatchStatus.Ready &&
                match.status != TournamentMatchStatus.InProgress)
            {
                return TournamentOperationResult.Fail(
                    "O confronto ainda não está liberado.");
            }
            if (HasOtherActiveMatch(match.playerAId, match.matchId) ||
                HasOtherActiveMatch(match.playerBId, match.matchId))
            {
                return TournamentOperationResult.Fail(
                    "Um dos participantes já está em outro confronto.");
            }
            match.status = TournamentMatchStatus.InProgress;
            if (match.startedAtUtcTicks == 0)
                match.startedAtUtcTicks = DateTime.UtcNow.Ticks;
            SetPlayerDuelStatus(match.playerAId, true);
            SetPlayerDuelStatus(match.playerBId, true);
            match.updateRevision++;
            Touch();
            return TournamentOperationResult.Ok("Confronto em andamento.");
        }

        public TournamentOperationResult ReopenMatch(
            string matchId,
            string reason)
        {
            TournamentMatch match = State.FindMatch(matchId);
            if (match == null)
                return TournamentOperationResult.Fail("Confronto não encontrado.");
            if (match.status == TournamentMatchStatus.Finished)
                return TournamentOperationResult.Fail(
                    "Um confronto finalizado não pode ser reaberto automaticamente.");
            match.status = TournamentMatchStatus.Ready;
            match.relayRoomCode = string.Empty;
            match.relayHostPlayerId = string.Empty;
            SetPlayerDuelStatus(match.playerAId, false);
            SetPlayerDuelStatus(match.playerBId, false);
            match.updateRevision++;
            Touch();
            return TournamentOperationResult.Ok(
                string.IsNullOrWhiteSpace(reason)
                    ? "Confronto reaberto."
                    : "Confronto reaberto: " + reason);
        }

        public TournamentOperationResult SubmitGameResult(
            TournamentMatchResult result)
        {
            if (processingResult)
            {
                return TournamentOperationResult.Fail(
                    "Outro resultado ainda está sendo consolidado.");
            }
            processingResult = true;
            try
            {
                return SubmitGameResultInternal(result);
            }
            finally
            {
                processingResult = false;
            }
        }

        public TournamentOperationResult AwardWalkover(
            string matchId,
            string winnerId)
        {
            if (!State.config.allowWalkover)
                return TournamentOperationResult.Fail("WO está desativado.");
            TournamentMatch match = State.FindMatch(matchId);
            if (match == null || !match.Contains(winnerId))
                return TournamentOperationResult.Fail("Confronto/ganhador inválido.");
            string loserId = string.Equals(
                match.playerAId,
                winnerId,
                StringComparison.Ordinal)
                ? match.playerBId
                : match.playerAId;
            return SubmitGameResult(new TournamentMatchResult
            {
                resultId = $"wo:{matchId}:{State.revision + 1}",
                tournamentId = State.config.tournamentId,
                roundId = match.roundId,
                matchId = matchId,
                playerAId = match.playerAId,
                playerBId = match.playerBId,
                winnerId = winnerId,
                loserId = loserId,
                finishedAtUtcTicks = DateTime.UtcNow.Ticks,
                walkover = true
            });
        }

        public TournamentOperationResult Cancel(string reason)
        {
            if (State.config.status == TournamentStatus.Completed ||
                State.config.status == TournamentStatus.Cancelled)
            {
                return TournamentOperationResult.Ok("Torneio já encerrado.");
            }
            State.config.status = TournamentStatus.Cancelled;
            State.config.presentationMessage = string.IsNullOrWhiteSpace(reason)
                ? "Torneio cancelado pelo organizador."
                : reason.Trim();
            State.finishedAtUtcTicks = DateTime.UtcNow.Ticks;
            Touch();
            return TournamentOperationResult.Ok("Torneio cancelado.");
        }

        public IReadOnlyList<TournamentPlayer> OrderedStandings()
        {
            return State.players
                .Where(player => player != null)
                .OrderBy(player => player.rankPosition <= 0
                    ? int.MaxValue
                    : player.rankPosition)
                .ThenBy(player => player.displayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private TournamentOperationResult SubmitGameResultInternal(
            TournamentMatchResult result)
        {
            if (State.config.status != TournamentStatus.InProgress)
                return TournamentOperationResult.Fail("O torneio não está em andamento.");
            if (result == null || string.IsNullOrWhiteSpace(result.resultId))
                return TournamentOperationResult.Fail("Resultado sem identificação única.");
            if (!string.Equals(
                    result.tournamentId,
                    State.config.tournamentId,
                    StringComparison.Ordinal))
                return TournamentOperationResult.Fail("Resultado de outro torneio.");
            if (State.results.Any(existing => existing != null &&
                string.Equals(
                    existing.resultId,
                    result.resultId,
                    StringComparison.Ordinal)))
            {
                return TournamentOperationResult.Ok(
                    "Resultado duplicado ignorado com segurança.");
            }

            TournamentMatch match = State.FindMatch(result.matchId);
            if (match == null || match.status == TournamentMatchStatus.Finished)
                return TournamentOperationResult.Fail("Confronto ausente ou já encerrado.");
            if (match.status != TournamentMatchStatus.Ready &&
                match.status != TournamentMatchStatus.InProgress)
            {
                return TournamentOperationResult.Fail(
                    "O confronto ainda não está liberado para receber resultado.");
            }
            if (!match.HasBothPlayers ||
                !match.Contains(result.winnerId) ||
                !match.Contains(result.loserId) ||
                string.Equals(
                    result.winnerId,
                    result.loserId,
                    StringComparison.Ordinal))
            {
                return TournamentOperationResult.Fail(
                    "Vencedor/derrotado não pertencem ao confronto.");
            }
            if (!PlayersMatchResult(match, result))
                return TournamentOperationResult.Fail("Participantes do resultado divergentes.");

            result.roundId = match.roundId;
            result.playerAId = match.playerAId;
            result.playerBId = match.playerBId;
            if (result.finishedAtUtcTicks <= 0)
                result.finishedAtUtcTicks = DateTime.UtcNow.Ticks;
            if (string.Equals(
                    result.winnerId,
                    match.playerAId,
                    StringComparison.Ordinal))
                match.gamesWonByA++;
            else
                match.gamesWonByB++;
            result.gamesWonByA = match.gamesWonByA;
            result.gamesWonByB = match.gamesWonByB;
            match.acceptedResultIds ??= new List<string>();
            match.acceptedResultIds.Add(result.resultId);
            State.results.Add(result);
            State.stats.globalStats.totalDuels++;
            ApplyGameOutcomeToPlayers(match, result);
            AggregateStats(result);

            int needed = match.bestOf / 2 + 1;
            if (result.walkover ||
                match.gamesWonByA >= needed || match.gamesWonByB >= needed)
            {
                FinishSeries(match, result.winnerId, result.loserId, result.walkover);
            }
            else
            {
                match.status = TournamentMatchStatus.Ready;
                match.relayRoomCode = string.Empty;
                match.relayHostPlayerId = string.Empty;
                SetPlayerDuelStatus(match.playerAId, false);
                SetPlayerDuelStatus(match.playerBId, false);
            }
            match.updateRevision++;
            UpdateRankings();
            RefreshGlobalHighlights();
            Touch();
            return TournamentOperationResult.Ok(
                match.status == TournamentMatchStatus.Finished
                    ? "Série concluída e torneio atualizado."
                    : $"Jogo registrado. Placar: {match.gamesWonByA}–" +
                      $"{match.gamesWonByB}.");
        }

        private void BuildSingleElimination(IReadOnlyList<TournamentPlayer> players)
        {
            int bracketSize = NextPowerOfTwo(players.Count);
            int roundCount = 0;
            for (int size = bracketSize; size > 1; size /= 2)
                roundCount++;
            for (int roundNumber = 1; roundNumber <= roundCount; roundNumber++)
            {
                int matchCount = bracketSize / (1 << roundNumber);
                TournamentRound round = CreateRound(
                    roundNumber,
                    EliminationRoundName(bracketSize, roundNumber, roundCount));
                for (int index = 0; index < matchCount; index++)
                    CreateMatch(round, index);
            }

            TournamentRound firstRound = State.rounds.Find(round =>
                round != null && round.roundNumber == 1);
            int firstRoundMatches = bracketSize / 2;
            int byeCount = bracketSize - players.Count;
            HashSet<int> byeSlots = DistributedByeSlots(
                firstRoundMatches,
                byeCount);
            int playerIndex = 0;
            for (int index = 0; index < firstRoundMatches; index++)
            {
                TournamentMatch match = State.FindMatch(
                    firstRound.matchIds[index]);
                match.playerAId = players[playerIndex++].playerId;
                if (byeSlots.Contains(index))
                {
                    match.status = TournamentMatchStatus.Bye;
                    match.winnerId = match.playerAId;
                    match.finishedAtUtcTicks = DateTime.UtcNow.Ticks;
                    ForwardEliminationWinner(match, match.playerAId);
                }
                else
                {
                    match.playerBId = players[playerIndex++].playerId;
                }
            }
            firstRound.completed = firstRound.matchIds.All(matchId =>
                IsResolvedMatch(State.FindMatch(matchId)));
        }

        private void BuildPointsSchedule(IReadOnlyList<TournamentPlayer> players)
        {
            var rotating = players.Select(player => player.playerId).ToList();
            if (rotating.Count % 2 != 0)
                rotating.Add(string.Empty);
            int totalRounds = Math.Min(
                State.config.pointsRoundCount,
                rotating.Count - 1);
            for (int roundNumber = 1; roundNumber <= totalRounds; roundNumber++)
            {
                TournamentRound round = CreateRound(
                    roundNumber,
                    $"Rodada {roundNumber}");
                int pairCount = rotating.Count / 2;
                for (int index = 0; index < pairCount; index++)
                {
                    string left = rotating[index];
                    string right = rotating[rotating.Count - 1 - index];
                    if (string.IsNullOrWhiteSpace(left) ||
                        string.IsNullOrWhiteSpace(right))
                    {
                        round.byePlayerId = string.IsNullOrWhiteSpace(left)
                            ? right
                            : left;
                        continue;
                    }
                    TournamentMatch match = CreateMatch(round, index);
                    bool invert = (roundNumber + index) % 2 == 0;
                    match.playerAId = invert ? right : left;
                    match.playerBId = invert ? left : right;
                }
                string last = rotating[rotating.Count - 1];
                rotating.RemoveAt(rotating.Count - 1);
                rotating.Insert(1, last);
            }
        }

        private TournamentRound CreateRound(int roundNumber, string name)
        {
            var round = new TournamentRound
            {
                roundId = $"{State.config.tournamentId}:r{roundNumber}",
                roundNumber = roundNumber,
                displayName = name
            };
            State.rounds.Add(round);
            return round;
        }

        private TournamentMatch CreateMatch(
            TournamentRound round,
            int bracketIndex)
        {
            var match = new TournamentMatch
            {
                matchId = $"{round.roundId}:m{bracketIndex}",
                roundId = round.roundId,
                roundNumber = round.roundNumber,
                bracketIndex = bracketIndex,
                bestOf = State.config.bestOf,
                status = TournamentMatchStatus.Waiting,
                scheduledAtUtcTicks = DateTime.UtcNow.Ticks
            };
            State.matches.Add(match);
            round.matchIds.Add(match.matchId);
            return match;
        }

        private void FinishSeries(
            TournamentMatch match,
            string winnerId,
            string loserId,
            bool walkover)
        {
            match.status = TournamentMatchStatus.Finished;
            match.winnerId = winnerId;
            match.loserId = loserId;
            match.finishedAtUtcTicks = DateTime.UtcNow.Ticks;
            match.relayRoomCode = string.Empty;
            match.relayHostPlayerId = string.Empty;
            State.stats.globalStats.totalMatches++;
            TournamentPlayer winner = State.FindPlayer(winnerId);
            TournamentPlayer loser = State.FindPlayer(loserId);
            if (winner != null)
            {
                winner.matchesPlayed++;
                winner.wins++;
                winner.seriesWins++;
                winner.points += walkover
                    ? State.config.pointsPerWalkover
                    : State.config.pointsPerWin;
                winner.status = TournamentPlayerStatus.Ready;
                winner.currentWinStreak++;
                winner.longestWinStreak = Math.Max(
                    winner.longestWinStreak,
                    winner.currentWinStreak);
                winner.currentLossStreak = 0;
            }
            if (loser != null)
            {
                loser.matchesPlayed++;
                loser.losses++;
                loser.seriesLosses++;
                loser.points += State.config.pointsPerLoss;
                loser.currentLossStreak++;
                loser.longestLossStreak = Math.Max(
                    loser.longestLossStreak,
                    loser.currentLossStreak);
                loser.currentWinStreak = 0;
                loser.status = TournamentPlayerStatus.Ready;
            }

            TournamentRound round = State.FindRound(match.roundId);
            if (round != null)
            {
                round.completed = round.matchIds.All(matchId =>
                    IsResolvedMatch(State.FindMatch(matchId)));
            }

            if (State.config.formatType == TournamentFormatType.SingleElimination)
                AdvanceSingleElimination(match, winner, loser);
            else
                AdvancePointsRound(round);
        }

        private void AdvanceSingleElimination(
            TournamentMatch completed,
            TournamentPlayer winner,
            TournamentPlayer loser)
        {
            if (completed.roundNumber >= State.rounds.Count)
            {
                CompleteTournament(winner?.playerId);
                return;
            }
            if (loser != null)
            {
                loser.isEliminated = true;
                loser.status = TournamentPlayerStatus.Eliminated;
            }
            ForwardEliminationWinner(completed, winner?.playerId);

            TournamentRound current = State.FindRound(completed.roundId);
            if (current != null && current.completed)
            {
                State.currentRoundNumber = completed.roundNumber + 1;
                SetRoundReady(State.currentRoundNumber);
            }
        }

        private void AdvancePointsRound(TournamentRound round)
        {
            if (round == null || !round.completed)
                return;
            if (round.roundNumber >= State.rounds.Count)
            {
                UpdateRankings();
                CompleteTournament(OrderedStandings().FirstOrDefault()?.playerId);
                return;
            }
            State.currentRoundNumber = round.roundNumber + 1;
            SetRoundReady(State.currentRoundNumber);
        }

        private void CompleteTournament(string championId)
        {
            State.config.status = TournamentStatus.Completed;
            State.finishedAtUtcTicks = DateTime.UtcNow.Ticks;
            State.championPlayerId = championId ?? string.Empty;
            UpdateRankings();
            State.podiumPlayerIds = OrderedStandings()
                .Take(3)
                .Select(player => player.playerId)
                .ToList();
            State.stats.globalStats.championId =
                State.podiumPlayerIds.ElementAtOrDefault(0) ?? string.Empty;
            State.stats.globalStats.runnerUpId =
                State.podiumPlayerIds.ElementAtOrDefault(1) ?? string.Empty;
            State.stats.globalStats.thirdPlaceId =
                State.podiumPlayerIds.ElementAtOrDefault(2) ?? string.Empty;
            foreach (TournamentPlayer player in State.players)
            {
                if (!string.Equals(
                        player.playerId,
                        championId,
                        StringComparison.Ordinal))
                {
                    player.isEliminated = true;
                    player.status = TournamentPlayerStatus.Eliminated;
                }
                else
                {
                    player.status = TournamentPlayerStatus.Ready;
                }
            }
        }

        private void SetRoundReady(int roundNumber)
        {
            foreach (TournamentMatch match in State.matches)
            {
                if (match.roundNumber == roundNumber && match.HasBothPlayers &&
                    match.status == TournamentMatchStatus.Waiting)
                {
                    match.status = TournamentMatchStatus.Ready;
                }
            }
        }

        private void ForwardEliminationWinner(
            TournamentMatch completed,
            string winnerId)
        {
            if (completed == null || string.IsNullOrWhiteSpace(winnerId) ||
                completed.roundNumber >= State.rounds.Count)
            {
                return;
            }
            TournamentMatch next = State.matches.Find(match =>
                match != null &&
                match.roundNumber == completed.roundNumber + 1 &&
                match.bracketIndex == completed.bracketIndex / 2);
            if (next == null)
                return;
            if (completed.bracketIndex % 2 == 0)
                next.playerAId = winnerId;
            else
                next.playerBId = winnerId;
        }

        private static HashSet<int> DistributedByeSlots(
            int matchCount,
            int byeCount)
        {
            var result = new HashSet<int>();
            if (matchCount <= 0 || byeCount <= 0)
                return result;
            for (int index = 0; index < byeCount; index++)
            {
                int slot = (int)Math.Floor(
                    (index + 0.5d) * matchCount / byeCount);
                slot = Math.Max(0, Math.Min(matchCount - 1, slot));
                while (result.Contains(slot))
                    slot = (slot + 1) % matchCount;
                result.Add(slot);
            }
            return result;
        }

        private static bool IsResolvedMatch(TournamentMatch match)
        {
            return match != null &&
                   (match.status == TournamentMatchStatus.Finished ||
                    match.status == TournamentMatchStatus.Bye);
        }

        private void ApplyGameOutcomeToPlayers(
            TournamentMatch match,
            TournamentMatchResult result)
        {
            TournamentPlayer winner = State.FindPlayer(result.winnerId);
            TournamentPlayer loser = State.FindPlayer(result.loserId);
            if (winner != null)
                winner.gamesWon++;
            if (loser != null)
                loser.gamesLost++;
        }

        private void AggregateStats(TournamentMatchResult result)
        {
            TournamentDuelStatsSnapshot snapshot = result.stats;
            TournamentPlayerStats winner = EnsurePlayerStats(result.winnerId);
            TournamentPlayerStats loser = EnsurePlayerStats(result.loserId);
            if (winner != null)
            {
                winner.duelsPlayed++;
                winner.duelsWon++;
                if (result.surrender)
                    winner.opponentSurrenders++;
            }
            if (loser != null)
            {
                loser.duelsPlayed++;
                loser.duelsLost++;
                if (result.surrender)
                    loser.surrenders++;
            }
            if (snapshot == null)
                return;

            long duration = Math.Max(
                0,
                snapshot.finishedAtUtcTicks - snapshot.startedAtUtcTicks);
            ApplyPlayerSnapshot(snapshot.playerA, result, duration);
            ApplyPlayerSnapshot(snapshot.playerB, result, duration);
            foreach (TournamentCardStats card in
                     snapshot.perCardStats ?? new List<TournamentCardStats>())
            {
                if (card == null || string.IsNullOrWhiteSpace(card.cardId))
                    continue;
                TournamentCardStats aggregate = State.stats.perCardStats.Find(
                    item => item != null && string.Equals(
                        item.cardId,
                        card.cardId,
                        StringComparison.Ordinal));
                if (aggregate == null)
                {
                    aggregate = new TournamentCardStats
                    {
                        cardId = card.cardId
                    };
                    State.stats.perCardStats.Add(aggregate);
                }
                MergeCardStats(aggregate, card);
                if (string.Equals(
                        card.playerId,
                        result.winnerId,
                        StringComparison.Ordinal) &&
                    CardAppeared(card))
                {
                    aggregate.duelsWonWhenUsed++;
                }
            }
            State.stats.perMatchStats.Add(new TournamentMatchStats
            {
                resultId = result.resultId,
                matchId = result.matchId,
                winnerId = result.winnerId,
                loserId = result.loserId,
                durationTicks = duration,
                turns = snapshot.turnCount,
                winnerDamage = DamageFor(snapshot, result.winnerId),
                loserDamage = DamageFor(snapshot, result.loserId),
                highestDamageCardId = snapshot.perCardStats
                    .OrderByDescending(card =>
                        (card?.battleDamage ?? 0) +
                        (card?.effectDamage ?? 0))
                    .FirstOrDefault()?.cardId ?? string.Empty
            });
            State.stats.globalStats.totalDurationTicks += duration;
        }

        private void ApplyPlayerSnapshot(
            TournamentDuelPlayerStats source,
            TournamentMatchResult result,
            long duration)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.playerId))
                return;
            TournamentPlayerStats target = EnsurePlayerStats(source.playerId);
            if (target == null)
                return;
            target.totalDuelDurationTicks += duration;
            target.damageDealt += source.damageDealt;
            target.damageReceived += source.damageReceived;
            target.monstersSummoned += source.monstersSummoned;
            target.specialSummons += source.specialSummons;
            target.effectsActivated += source.effectsActivated;
            target.effectsResolved += source.effectsResolved;
            target.cardsDestroyed += source.cardsDestroyed;
            target.cardsSentToGraveyard += source.cardsSentToGraveyard;
            target.cardsBanished += source.cardsBanished;
            target.cardsDrawn += source.cardsDrawn;
            target.cardsReturnedToHand += source.cardsReturnedToHand;
            target.cardsReturnedToDeck += source.cardsReturnedToDeck;
            target.cardsTributed += source.cardsTributed;
            if (source.startedFirst)
            {
                target.gamesStartedFirst++;
                if (string.Equals(
                        source.playerId,
                        result.winnerId,
                        StringComparison.Ordinal))
                    target.winsStartedFirst++;
            }
            else
            {
                target.gamesStartedSecond++;
                if (string.Equals(
                        source.playerId,
                        result.winnerId,
                        StringComparison.Ordinal))
                    target.winsStartedSecond++;
            }
        }

        private TournamentPlayerStats EnsurePlayerStats(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return null;
            TournamentPlayerStats stats = State.FindPlayerStats(playerId);
            if (stats != null)
                return stats;
            stats = new TournamentPlayerStats { playerId = playerId };
            State.stats.perPlayerStats.Add(stats);
            return stats;
        }

        private void UpdateRankings()
        {
            List<TournamentPlayer> ordered = State.players
                .Where(player => player != null)
                .ToList();
            ordered.Sort(ComparePointsPlayers);
            if (State.config.formatType == TournamentFormatType.SingleElimination)
            {
                ordered = ordered
                    .OrderBy(player => player.isEliminated)
                    .ThenByDescending(player => player.seriesWins)
                    .ThenByDescending(player => player.GameDifferential)
                    .ThenBy(player => player.displayName,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                if (!string.IsNullOrWhiteSpace(State.championPlayerId))
                {
                    TournamentPlayer champion = ordered.Find(player =>
                        string.Equals(
                            player.playerId,
                            State.championPlayerId,
                            StringComparison.Ordinal));
                    if (champion != null)
                    {
                        ordered.Remove(champion);
                        ordered.Insert(0, champion);
                    }
                }
            }
            for (int index = 0; index < ordered.Count; index++)
                ordered[index].rankPosition = index + 1;
        }

        private int ComparePointsPlayers(
            TournamentPlayer left,
            TournamentPlayer right)
        {
            int comparison = right.points.CompareTo(left.points);
            if (comparison != 0)
                return comparison;
            comparison = right.seriesWins.CompareTo(left.seriesWins);
            if (comparison != 0)
                return comparison;

            int leftHeadToHead = 0;
            int rightHeadToHead = 0;
            foreach (TournamentMatch match in State.matches)
            {
                if (match == null ||
                    match.status != TournamentMatchStatus.Finished ||
                    !match.Contains(left.playerId) ||
                    !match.Contains(right.playerId))
                {
                    continue;
                }
                if (string.Equals(match.winnerId, left.playerId,
                        StringComparison.Ordinal))
                    leftHeadToHead++;
                else if (string.Equals(match.winnerId, right.playerId,
                             StringComparison.Ordinal))
                    rightHeadToHead++;
            }
            comparison = rightHeadToHead.CompareTo(leftHeadToHead);
            if (comparison != 0)
                return comparison;
            comparison = right.GameDifferential.CompareTo(
                left.GameDifferential);
            if (comparison != 0)
                return comparison;
            comparison = right.gamesWon.CompareTo(left.gamesWon);
            if (comparison != 0)
                return comparison;
            int leftDamage = State.FindPlayerStats(left.playerId)?.damageDealt ?? 0;
            int rightDamage = State.FindPlayerStats(right.playerId)?.damageDealt ?? 0;
            comparison = rightDamage.CompareTo(leftDamage);
            if (comparison != 0)
                return comparison;
            return StringComparer.CurrentCultureIgnoreCase.Compare(
                left.displayName,
                right.displayName);
        }

        private void RefreshGlobalHighlights()
        {
            State.stats.globalStats.totalParticipants = State.players.Count;
            TournamentPlayer mvp = State.players
                .OrderByDescending(player => player.seriesWins)
                .ThenByDescending(player => player.gamesWon)
                .ThenByDescending(player => player.GameDifferential)
                .FirstOrDefault();
            State.stats.globalStats.mvpPlayerId = mvp?.playerId ?? string.Empty;
            TournamentCardStats used = State.stats.perCardStats
                .OrderByDescending(card =>
                    card.timesSummoned + card.timesActivated + card.timesDrawn)
                .FirstOrDefault();
            TournamentCardStats banished = State.stats.perCardStats
                .OrderByDescending(card => card.timesBanished)
                .FirstOrDefault();
            TournamentCardStats damage = State.stats.perCardStats
                .OrderByDescending(card => card.battleDamage + card.effectDamage)
                .FirstOrDefault();
            State.stats.globalStats.mostUsedCardId = used?.cardId ?? string.Empty;
            State.stats.globalStats.mostBanishedCardId =
                banished?.cardId ?? string.Empty;
            State.stats.globalStats.highestDamageCardId =
                damage?.cardId ?? string.Empty;
            State.stats.globalStats.mvpCardId = used?.cardId ?? string.Empty;
        }

        private void RecalculateDeckPresence()
        {
            Dictionary<string, int> presence = State.players
                .Where(player => player?.deck != null)
                .SelectMany(player => player.deck.mainDeckCardIds
                    .Concat(player.deck.extraDeckCardIds)
                    .Concat(player.deck.sideDeckCardIds)
                    .Where(cardId => !string.IsNullOrWhiteSpace(cardId))
                    .Distinct(StringComparer.Ordinal))
                .GroupBy(cardId => cardId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);
            foreach (TournamentCardStats card in State.stats.perCardStats)
                card.timesIncludedInDeck = 0;
            foreach (KeyValuePair<string, int> pair in presence)
            {
                TournamentCardStats card = State.stats.perCardStats.Find(item =>
                    item != null && string.Equals(
                        item.cardId,
                        pair.Key,
                        StringComparison.Ordinal));
                if (card == null)
                {
                    card = new TournamentCardStats { cardId = pair.Key };
                    State.stats.perCardStats.Add(card);
                }
                card.timesIncludedInDeck = pair.Value;
            }
        }

        private static void MergeCardStats(
            TournamentCardStats target,
            TournamentCardStats source)
        {
            target.timesDrawn += source.timesDrawn;
            target.timesSummoned += source.timesSummoned;
            target.timesActivated += source.timesActivated;
            target.timesDestroyed += source.timesDestroyed;
            target.timesSentToGraveyard += source.timesSentToGraveyard;
            target.timesBanished += source.timesBanished;
            target.timesReturnedToHand += source.timesReturnedToHand;
            target.timesReturnedToDeck += source.timesReturnedToDeck;
            target.battleDamage += source.battleDamage;
            target.effectDamage += source.effectDamage;
            target.duelsAppeared += source.duelsAppeared;
        }

        private static bool CardAppeared(TournamentCardStats card)
        {
            return card != null &&
                (card.timesDrawn > 0 || card.timesSummoned > 0 ||
                 card.timesActivated > 0 || card.timesDestroyed > 0 ||
                 card.timesSentToGraveyard > 0 || card.timesBanished > 0);
        }

        private static int DamageFor(
            TournamentDuelStatsSnapshot snapshot,
            string playerId)
        {
            if (snapshot?.playerA != null && string.Equals(
                    snapshot.playerA.playerId,
                    playerId,
                    StringComparison.Ordinal))
                return snapshot.playerA.damageDealt;
            if (snapshot?.playerB != null && string.Equals(
                    snapshot.playerB.playerId,
                    playerId,
                    StringComparison.Ordinal))
                return snapshot.playerB.damageDealt;
            return 0;
        }

        private bool HasOtherActiveMatch(string playerId, string exceptMatchId)
        {
            return State.matches.Any(match => match != null &&
                !string.Equals(
                    match.matchId,
                    exceptMatchId,
                    StringComparison.Ordinal) &&
                match.Contains(playerId) &&
                match.status == TournamentMatchStatus.InProgress);
        }

        private void SetPlayerDuelStatus(string playerId, bool inDuel)
        {
            TournamentPlayer player = State.FindPlayer(playerId);
            if (player == null || player.isEliminated)
                return;
            player.status = inDuel
                ? TournamentPlayerStatus.InDuel
                : player.isOnline
                    ? TournamentPlayerStatus.Ready
                    : TournamentPlayerStatus.Offline;
        }

        private static bool PlayersMatchResult(
            TournamentMatch match,
            TournamentMatchResult result)
        {
            bool direct = string.Equals(
                              match.playerAId,
                              result.playerAId,
                              StringComparison.Ordinal) &&
                          string.Equals(
                              match.playerBId,
                              result.playerBId,
                              StringComparison.Ordinal);
            bool omitted = string.IsNullOrWhiteSpace(result.playerAId) &&
                           string.IsNullOrWhiteSpace(result.playerBId);
            return direct || omitted;
        }

        private List<TournamentPlayer> SeedPlayers(
            IEnumerable<TournamentPlayer> source)
        {
            var result = source
                .OrderBy(player => player.playerId, StringComparer.Ordinal)
                .ToList();
            var random = new Random(State.config.bracketSeed);
            for (int index = result.Count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                (result[index], result[swap]) = (result[swap], result[index]);
            }
            return result;
        }

        private static string EliminationRoundName(
            int participantCount,
            int roundNumber,
            int roundCount)
        {
            if (roundNumber == roundCount)
                return "Final";
            if (roundNumber == roundCount - 1)
                return "Semifinais";
            if (roundNumber == roundCount - 2)
                return "Quartas de final";
            int remaining = participantCount / (1 << (roundNumber - 1));
            return $"Rodada de {remaining}";
        }

        private static bool IsSupportedParticipantLimit(int value)
        {
            return value >= 2 && value <= 32 && value % 2 == 0;
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 1;
            while (result < value)
                result <<= 1;
            return result;
        }

        private static int StableSeed(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value ?? string.Empty)
                    hash = hash * 31 + character;
                return hash == 0 ? 1 : hash;
            }
        }

        private static string CollapseWhitespace(string value)
        {
            return string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[])null,
                    StringSplitOptions.RemoveEmptyEntries));
        }

        private void Touch()
        {
            State.revision++;
            State.lastSavedAtUtcTicks = DateTime.UtcNow.Ticks;
            State.stats.globalStats.totalParticipants = State.players.Count;
        }

        private static void NormalizeState(TournamentState state)
        {
            state.config ??= new TournamentConfig();
            state.config.Normalize();
            state.players ??= new List<TournamentPlayer>();
            state.players.RemoveAll(player => player == null);
            state.rounds ??= new List<TournamentRound>();
            state.matches ??= new List<TournamentMatch>();
            state.results ??= new List<TournamentMatchResult>();
            state.podiumPlayerIds ??= new List<string>();
            state.stats ??= new TournamentStats();
            state.stats.tournamentId = state.config.tournamentId;
            state.stats.perPlayerStats ??= new List<TournamentPlayerStats>();
            state.stats.perCardStats ??= new List<TournamentCardStats>();
            state.stats.perMatchStats ??= new List<TournamentMatchStats>();
            state.stats.globalStats ??= new TournamentGlobalStats();
            foreach (TournamentPlayer player in state.players)
            {
                player.deck ??= new TournamentDeckManifest();
                player.deck.Normalize();
            }
            foreach (TournamentMatch match in state.matches)
                match.acceptedResultIds ??= new List<string>();
        }
    }
}
