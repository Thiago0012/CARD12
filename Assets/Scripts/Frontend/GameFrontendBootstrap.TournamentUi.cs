using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneArena.Multiplayer.Tournaments;
using ArcaneDuel.Game.Tournaments;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private enum TournamentPage
        {
            None,
            Hub,
            Join,
            CreateBasics,
            CreateRules,
            Lobby,
            DeckSelection,
            Overview,
            Bracket,
            Standings,
            Participants,
            Rules,
            Metrics,
            History,
            Final
        }

        private TournamentPage _tournamentPage;
        private TournamentConfig _tournamentDraft;
        private bool _tournamentEditing;
        private bool _tournamentSubscribed;
        private int _tournamentMetricsTab;
        private Text _tournamentFeedback;
        private InputField _tournamentNameField;
        private InputField _tournamentDescriptionField;
        private InputField _tournamentParticipantsField;
        private InputField _tournamentPasswordField;
        private InputField _tournamentJoinCodeField;
        private InputField _tournamentJoinPasswordField;
        private InputField _tournamentPointsWinField;
        private InputField _tournamentPointsLossField;
        private InputField _tournamentPointsWoField;
        private InputField _tournamentRoundsField;
        private InputField _tournamentTimeoutField;
        private InputField _tournamentCustomBanField;
        private InputField _tournamentPoolField;
        private string _tournamentPendingWoMatchId = string.Empty;
        private string _tournamentPendingWoWinnerId = string.Empty;
        private string _tournamentName = "Torneio Plus Ultra";
        private string _tournamentDescription =
            "Campeonato entre amigos com regras competitivas.";
        private string _tournamentPassword = string.Empty;
        private string _tournamentCustomBan = string.Empty;
        private string _tournamentPool = string.Empty;
        private GameObject _tournamentHelpPanel;

        private const string TournamentRandomDeckPreferenceKey =
            "ArcaneTournament.RandomDeck";
        private const string TournamentManualDeckPreferenceKey =
            "ArcaneTournament.ManualDeckId";

        private TournamentOnlineSession TournamentSession =>
            TournamentOnlineSession.EnsureInstance();

        public bool TournamentUsesRandomDeck =>
            PlayerPrefs.GetInt(TournamentRandomDeckPreferenceKey, 0) != 0;

        public string LocalTournamentPlayerDisplayName
        {
            get
            {
                EnsureTournamentRepository();
                return _repository?.PlayerDisplayName ?? string.Empty;
            }
        }

        public bool SetTournamentDeckPreference(
            bool useRandomDeck,
            string manualDeckId,
            out string rejection)
        {
            rejection = string.Empty;
            EnsureTournamentRepository();
            if (_repository == null)
            {
                rejection = "A coleção de decks ainda não foi carregada.";
                return false;
            }

            if (useRandomDeck)
            {
                bool hasEligibleDeck = _repository.GetDuelEligibleDecks()
                    .Any(deck => TryCreateTournamentLoadout(
                        deck.deckId,
                        out _,
                        out _));
                if (!hasEligibleDeck)
                {
                    rejection =
                        "Você ainda não possui um deck completo, desbloqueado e permitido pelas regras deste torneio.";
                    return false;
                }
            }
            else
            {
                string requested = string.IsNullOrWhiteSpace(manualDeckId)
                    ? PlayerPrefs.GetString(
                        TournamentManualDeckPreferenceKey,
                        _repository.State?.selectedDeckId ?? string.Empty)
                    : manualDeckId.Trim();
                if (!TryCreateTournamentLoadout(
                        requested,
                        out _,
                        out rejection))
                {
                    return false;
                }
                PlayerPrefs.SetString(
                    TournamentManualDeckPreferenceKey,
                    requested);
            }

            PlayerPrefs.SetInt(
                TournamentRandomDeckPreferenceKey,
                useRandomDeck ? 1 : 0);
            PlayerPrefs.Save();
            return true;
        }

        public bool TryGetTournamentDuelLoadout(
            string tournamentId,
            out DuelDeckLoadout loadout,
            out bool usesRandomDeck,
            out string rejection)
        {
            loadout = null;
            rejection = string.Empty;
            usesRandomDeck = TournamentUsesRandomDeck;
            EnsureTournamentRepository();
            if (_repository == null)
            {
                rejection = "A coleção de decks ainda não foi carregada.";
                return false;
            }

            if (usesRandomDeck)
            {
                IReadOnlyList<DeckRecord> eligible = _repository
                    .GetDuelEligibleDecks()
                    .Where(deck => TryCreateTournamentLoadout(
                        deck.deckId,
                        out _,
                        out _))
                    .ToArray();
                if (eligible.Count == 0)
                {
                    rejection =
                        "Nenhum deck desbloqueado atende às regras básicas de duelo.";
                    return false;
                }

                int index = StableTournamentDeckIndex(
                    tournamentId,
                    _repository.State?.localProfileId,
                    eligible.Count);
                return TryCreateTournamentLoadout(
                    eligible[index].deckId,
                    out loadout,
                    out rejection);
            }

            string manualDeckId = PlayerPrefs.GetString(
                TournamentManualDeckPreferenceKey,
                _repository.State?.selectedDeckId ?? string.Empty);
            if (TryCreateTournamentLoadout(
                    manualDeckId,
                    out loadout,
                    out rejection))
            {
                return true;
            }

            string selectedDeckId = _repository.State?.selectedDeckId ??
                string.Empty;
            if (!string.Equals(
                    selectedDeckId,
                    manualDeckId,
                    StringComparison.Ordinal) &&
                TryCreateTournamentLoadout(
                    selectedDeckId,
                    out loadout,
                    out rejection))
            {
                PlayerPrefs.SetString(
                    TournamentManualDeckPreferenceKey,
                    selectedDeckId);
                PlayerPrefs.Save();
                return true;
            }

            return false;
        }

        private bool TryCreateTournamentLoadout(
            string deckId,
            out DuelDeckLoadout loadout,
            out string rejection)
        {
            loadout = null;
            rejection = string.Empty;
            if (_repository == null || !_repository.TryCreateLoadout(
                    deckId,
                    out loadout,
                    out rejection))
            {
                return false;
            }

            TournamentConfig config = TournamentOnlineSession.Instance?
                .State?.config;
            if (config == null)
                return true;

            var manifest = new TournamentDeckManifest
            {
                deckId = loadout.deckId,
                displayName = loadout.displayName,
                banListId = loadout.banlistId,
                sha256 = loadout.normalizedDeckSha256,
                mainDeckCardIds = new List<string>(loadout.mainDeckCardIds),
                extraDeckCardIds = new List<string>(loadout.extraDeckCardIds),
                sideDeckCardIds = new List<string>(loadout.sideDeckCardIds)
            };
            TournamentDeckValidationResult validation =
                TournamentDeckRulesValidator.Validate(manifest, config);
            if (validation.IsValid)
                return true;

            rejection = validation.Summary;
            loadout = null;
            return false;
        }

        private void EnsureTournamentRepository()
        {
            if (_repository != null)
                return;
            ResolveProjectReferences();
            _repository = new DeckRepository();
            _repository.Load(_catalog);
            InitializeCoinRewardAuthorization();
        }

        private static int StableTournamentDeckIndex(
            string tournamentId,
            string profileId,
            int count)
        {
            if (count <= 1)
                return 0;
            unchecked
            {
                uint hash = 2166136261;
                string source = (tournamentId ?? string.Empty) + ":" +
                    (profileId ?? string.Empty);
                foreach (char value in source)
                {
                    hash ^= value;
                    hash *= 16777619;
                }
                return (int)(hash % (uint)count);
            }
        }

        private void ShowTournamentHub()
        {
            EnsureTournamentSubscription();
            RenderTournamentPage(TournamentPage.Hub);
        }

        private void EnsureTournamentSubscription()
        {
            if (_tournamentSubscribed)
                return;
            TournamentSession.StateChanged += OnTournamentStateChanged;
            _tournamentSubscribed = true;
        }

        private void OnTournamentStateChanged()
        {
            if (_tournamentPage == TournamentPage.None ||
                _screenRoot == null)
            {
                return;
            }
            if (_tournamentPage == TournamentPage.CreateBasics ||
                _tournamentPage == TournamentPage.CreateRules ||
                _tournamentPage == TournamentPage.Join ||
                _tournamentPage == TournamentPage.DeckSelection)
            {
                return;
            }
            RenderTournamentPage(_tournamentPage);
        }

        private void RenderTournamentPage(TournamentPage page)
        {
            _tournamentPage = page;
            switch (page)
            {
                case TournamentPage.Hub:
                    BuildTournamentHub();
                    break;
                case TournamentPage.Join:
                    BuildTournamentJoin();
                    break;
                case TournamentPage.CreateBasics:
                    BuildTournamentCreateBasics();
                    break;
                case TournamentPage.CreateRules:
                    BuildTournamentCreateRules();
                    break;
                case TournamentPage.Lobby:
                    BuildTournamentLobby();
                    break;
                case TournamentPage.DeckSelection:
                    BuildTournamentDeckSelection();
                    break;
                case TournamentPage.Overview:
                    BuildTournamentOverview();
                    break;
                case TournamentPage.Bracket:
                    BuildTournamentBracket();
                    break;
                case TournamentPage.Standings:
                    BuildTournamentStandings();
                    break;
                case TournamentPage.Participants:
                    BuildTournamentParticipants();
                    break;
                case TournamentPage.Rules:
                    BuildTournamentRules();
                    break;
                case TournamentPage.Metrics:
                    BuildTournamentMetrics();
                    break;
                case TournamentPage.History:
                    BuildTournamentHistory();
                    break;
                case TournamentPage.Final:
                    BuildTournamentFinal();
                    break;
            }
            AttachTournamentHelpToVisibleControls();
        }

        private void BuildTournamentShell(
            string title,
            string subtitle,
            Action backAction,
            bool showTabs = false)
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("TORNEIOS ONLINE");
            BuildHeader(title, backAction);
            if (_repository != null)
                CreateCoinBalance(_screenRoot);
            CreateText(
                _screenRoot,
                subtitle,
                18,
                FontStyle.Normal,
                Muted,
                new Vector2(0.08f, 0.855f),
                new Vector2(0.72f, 0.90f),
                TextAnchor.MiddleLeft);
            if (showTabs)
                BuildTournamentTabs();
        }

        private void BuildTournamentHub()
        {
            BuildTournamentShell(
                "TORNEIOS",
                "Crie, continue ou consulte campeonatos online.",
                ShowMultiplayerRoom);
            TournamentOnlineSession session = TournamentSession;
            Image create = TournamentCard(
                "CRIAR NOVO TORNEIO",
                "Configure formato, participantes, regras de deck e Best of N.",
                new Vector2(0.07f, 0.48f),
                new Vector2(0.37f, 0.78f),
                Cyan,
                BeginTournamentCreation);
            TournamentCard(
                "CONTINUAR TORNEIO",
                session.HasTournament
                    ? "Retome o lobby ou a rodada atual já sincronizada."
                    : "Restaure o último torneio salvo neste perfil.",
                new Vector2(0.385f, 0.48f),
                new Vector2(0.685f, 0.78f),
                Lime,
                ContinueTournament);
            TournamentCard(
                "TORNEIOS RECENTES",
                "Consulte campeões, pódios e métricas salvas.",
                new Vector2(0.70f, 0.48f),
                new Vector2(0.93f, 0.78f),
                Gold,
                () => RenderTournamentPage(TournamentPage.History));
            create.gameObject.name = "Criar Novo Torneio";

            Image joinPanel = CreatePanel(
                _screenRoot,
                "Entrar em Torneio",
                new Vector2(0.20f, 0.19f),
                new Vector2(0.80f, 0.42f),
                new Color(0.01f, 0.04f, 0.075f, 0.96f));
            AddOutline(joinPanel.gameObject, Cyan, new Vector2(2f, -2f));
            CreateText(joinPanel.transform, "ENTRAR COM CÓDIGO", 25,
                FontStyle.Bold, Color.white, new Vector2(0.06f, 0.58f),
                new Vector2(0.94f, 0.91f), TextAnchor.MiddleCenter);
            CreateText(joinPanel.transform,
                "Use o código compartilhado pelo organizador.", 15,
                FontStyle.Normal, Muted, new Vector2(0.06f, 0.35f),
                new Vector2(0.94f, 0.58f), TextAnchor.MiddleCenter);
            CreateButton(joinPanel.transform, "ABRIR ENTRADA",
                new Vector2(0.22f, 0.08f), new Vector2(0.78f, 0.32f),
                Blue, () => RenderTournamentPage(TournamentPage.Join));
            CreateTournamentFeedback(session.StatusMessage);
        }

        private Image TournamentCard(
            string title,
            string description,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action action)
        {
            Image panel = CreatePanel(_screenRoot, title, min, max,
                new Color(0.008f, 0.035f, 0.065f, 0.97f));
            AddOutline(panel.gameObject, accent, new Vector2(2f, -2f));
            CreatePanel(panel.transform, "Acento", new Vector2(0f, 0f),
                new Vector2(0.025f, 1f), accent).raycastTarget = false;
            CreateText(panel.transform, title, 24, FontStyle.Bold,
                Color.white, new Vector2(0.09f, 0.63f),
                new Vector2(0.91f, 0.90f), TextAnchor.MiddleLeft);
            CreateText(panel.transform, description, 16, FontStyle.Normal,
                Muted, new Vector2(0.09f, 0.28f),
                new Vector2(0.91f, 0.62f), TextAnchor.UpperLeft);
            CreateButton(panel.transform, "ABRIR", new Vector2(0.48f, 0.06f),
                new Vector2(0.91f, 0.25f), accent, action);
            return panel;
        }

        private void BeginTournamentCreation()
        {
            _tournamentEditing = false;
            _tournamentDraft = NewTournamentDraft();
            _tournamentName = _tournamentDraft.name;
            _tournamentDescription = _tournamentDraft.description;
            _tournamentPassword = string.Empty;
            _tournamentCustomBan = string.Empty;
            _tournamentPool = string.Empty;
            RenderTournamentPage(TournamentPage.CreateBasics);
        }

        private void BeginTournamentEdit()
        {
            TournamentState current = TournamentSession.State;
            if (current?.config == null)
                return;
            _tournamentEditing = true;
            _tournamentDraft = JsonUtility.FromJson<TournamentConfig>(
                JsonUtility.ToJson(current.config));
            _tournamentName = _tournamentDraft.name;
            _tournamentDescription = _tournamentDraft.description;
            _tournamentPassword = string.Empty;
            _tournamentCustomBan = string.Join(", ",
                _tournamentDraft.customBanList.Select(rule =>
                    rule.cardId + ":" + rule.maximumCopies));
            _tournamentPool = string.Join(", ",
                _tournamentDraft.allowedCardIds);
            RenderTournamentPage(TournamentPage.CreateBasics);
        }

        private static TournamentConfig NewTournamentDraft()
        {
            return new TournamentConfig
            {
                name = "Torneio Plus Ultra",
                description =
                    "Campeonato entre amigos com regras competitivas.",
                participantLimit = 4,
                bestOf = 3,
                pointsRoundCount = 3,
                pointsPerWin = 3,
                pointsPerLoss = 0,
                pointsPerWalkover = 3,
                formatType = TournamentFormatType.SingleElimination,
                banListMode = TournamentBanListMode.Standard,
                allowedCardPoolMode = TournamentCardPoolMode.AllCards,
                deckLocked = true,
                privateRoom = true,
                allowEarlyStart = true,
                matchTimeoutMinutes = 45
            };
        }

        private void BuildTournamentCreateBasics()
        {
            _tournamentDraft ??= NewTournamentDraft();
            BuildTournamentShell(
                _tournamentEditing ? "EDITAR TORNEIO" : "CRIAR TORNEIO",
                "Passo 1 de 2 — informações e formato.",
                _tournamentEditing
                    ? () => RenderTournamentPage(TournamentPage.Lobby)
                    : () => RenderTournamentPage(TournamentPage.Hub));
            Image panel = CreatePanel(_screenRoot, "Informações e Formato",
                new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.82f),
                new Color(0.008f, 0.032f, 0.06f, 0.97f));
            AddOutline(panel.gameObject, Cyan, new Vector2(2f, -2f));
            FieldLabel(panel.transform, "NOME DO TORNEIO",
                new Vector2(0.05f, 0.82f), new Vector2(0.47f, 0.90f));
            _tournamentNameField = TournamentInput(panel.transform,
                "Nome do torneio", _tournamentName,
                new Vector2(0.05f, 0.70f), new Vector2(0.47f, 0.82f), 48);
            FieldLabel(panel.transform, "DESCRIÇÃO",
                new Vector2(0.53f, 0.82f), new Vector2(0.95f, 0.90f));
            _tournamentDescriptionField = TournamentInput(panel.transform,
                "Descrição", _tournamentDescription,
                new Vector2(0.53f, 0.70f), new Vector2(0.95f, 0.82f), 180);

            SelectionButton(panel.transform,
                "FORMATO\n" + FormatLabel(_tournamentDraft.formatType),
                new Vector2(0.05f, 0.48f), new Vector2(0.31f, 0.65f),
                _tournamentDraft.formatType ==
                    TournamentFormatType.SingleElimination ? Gold : Cyan,
                () =>
                {
                    if (!SaveTournamentBasics())
                        return;
                    _tournamentDraft.formatType = _tournamentDraft.formatType ==
                        TournamentFormatType.SingleElimination
                        ? TournamentFormatType.Points
                        : TournamentFormatType.SingleElimination;
                    RenderTournamentPage(TournamentPage.CreateBasics);
                });
            Image participants = CreatePanel(panel.transform,
                "Participantes", new Vector2(0.37f, 0.48f),
                new Vector2(0.63f, 0.65f),
                new Color(0.045f, 0.075f, 0.025f, 0.98f));
            AddOutline(participants.gameObject, Lime, new Vector2(2f, -2f));
            CreateText(participants.transform, "PARTICIPANTES", 16,
                FontStyle.Bold, Color.white, new Vector2(0.05f, 0.62f),
                new Vector2(0.95f, 0.95f), TextAnchor.MiddleCenter);
            CreateButton(participants.transform, "−", new Vector2(0.04f, 0.08f),
                new Vector2(0.25f, 0.58f), Lime,
                () => AdjustTournamentParticipants(-2));
            _tournamentParticipantsField = TournamentInput(
                participants.transform, "2–32",
                _tournamentDraft.participantLimit.ToString(),
                new Vector2(0.28f, 0.08f), new Vector2(0.72f, 0.58f), 2);
            _tournamentParticipantsField.characterValidation =
                InputField.CharacterValidation.Integer;
            _tournamentParticipantsField.textComponent.alignment =
                TextAnchor.MiddleCenter;
            CreateButton(participants.transform, "+", new Vector2(0.75f, 0.08f),
                new Vector2(0.96f, 0.58f), Lime,
                () => AdjustTournamentParticipants(2));
            SelectionButton(panel.transform,
                "CONFRONTO\nBO" + _tournamentDraft.bestOf,
                new Vector2(0.69f, 0.48f), new Vector2(0.95f, 0.65f),
                Blue, CycleTournamentBestOf);

            CreateText(panel.transform,
                "Mata-mata elimina quem perde a série. Pontos libera rodadas " +
                "e ordena por pontos, vitórias, confronto direto, saldo e dano.",
                18, FontStyle.Normal, Muted, new Vector2(0.06f, 0.28f),
                new Vector2(0.65f, 0.44f), TextAnchor.MiddleLeft);
            SelectionButton(panel.transform,
                "INÍCIO COM MAIORIA\n" +
                YesNo(_tournamentDraft.allowEarlyStart),
                new Vector2(0.69f, 0.28f), new Vector2(0.95f, 0.44f),
                _tournamentDraft.allowEarlyStart ? Lime : Blue,
                () =>
                {
                    if (!SaveTournamentBasics())
                        return;
                    _tournamentDraft.allowEarlyStart =
                        !_tournamentDraft.allowEarlyStart;
                    RenderTournamentPage(TournamentPage.CreateBasics);
                });
            CreateButton(panel.transform, "PRÓXIMO: REGRAS",
                new Vector2(0.58f, 0.07f), new Vector2(0.94f, 0.23f),
                Cyan, () =>
                {
                    if (!SaveTournamentBasics())
                        return;
                    RenderTournamentPage(TournamentPage.CreateRules);
                });
            CreateButton(panel.transform, "CANCELAR",
                new Vector2(0.06f, 0.07f), new Vector2(0.38f, 0.23f),
                Danger, _tournamentEditing
                    ? () => RenderTournamentPage(TournamentPage.Lobby)
                    : () => RenderTournamentPage(TournamentPage.Hub));
            CreateTournamentFeedback(string.Empty);
        }

        private void BuildTournamentCreateRules()
        {
            BuildTournamentShell(
                _tournamentEditing ? "EDITAR REGRAS" : "CRIAR TORNEIO",
                "Passo 2 de 2 — decks, pontuação e segurança.",
                () => RenderTournamentPage(TournamentPage.CreateBasics));
            Image panel = CreatePanel(_screenRoot, "Regras",
                new Vector2(0.045f, 0.10f), new Vector2(0.955f, 0.83f),
                new Color(0.008f, 0.032f, 0.06f, 0.97f));
            AddOutline(panel.gameObject, Cyan, new Vector2(2f, -2f));

            SelectionButton(panel.transform,
                "BAN LIST\n" + BanListLabel(_tournamentDraft.banListMode),
                new Vector2(0.035f, 0.73f), new Vector2(0.24f, 0.91f),
                Gold, CycleTournamentBanList);
            SelectionButton(panel.transform,
                "DECK BLOQUEADO\n" + YesNo(_tournamentDraft.deckLocked),
                new Vector2(0.265f, 0.73f), new Vector2(0.47f, 0.91f),
                Lime, () =>
                {
                    SaveTournamentRuleFields();
                    _tournamentDraft.deckLocked = !_tournamentDraft.deckLocked;
                    RenderTournamentPage(TournamentPage.CreateRules);
                });
            SelectionButton(panel.transform,
                "PRIVACIDADE\n" +
                (_tournamentDraft.privateRoom ? "PRIVADO" : "PÚBLICO"),
                new Vector2(0.50f, 0.73f), new Vector2(0.705f, 0.91f),
                Blue, () =>
                {
                    SaveTournamentRuleFields();
                    _tournamentDraft.privateRoom =
                        !_tournamentDraft.privateRoom;
                    RenderTournamentPage(TournamentPage.CreateRules);
                });
            SelectionButton(panel.transform,
                "WO\n" + YesNo(_tournamentDraft.allowWalkover),
                new Vector2(0.735f, 0.73f), new Vector2(0.94f, 0.91f),
                Cyan, () =>
                {
                    SaveTournamentRuleFields();
                    _tournamentDraft.allowWalkover =
                        !_tournamentDraft.allowWalkover;
                    RenderTournamentPage(TournamentPage.CreateRules);
                });

            FieldLabel(panel.transform, "SENHA (OPCIONAL, 8–64)",
                new Vector2(0.035f, 0.61f), new Vector2(0.32f, 0.69f));
            _tournamentPasswordField = TournamentInput(panel.transform,
                "Senha privada", _tournamentPassword,
                new Vector2(0.035f, 0.49f), new Vector2(0.32f, 0.61f), 64);
            _tournamentPasswordField.contentType =
                InputField.ContentType.Password;
            FieldLabel(panel.transform, "TIMEOUT (MIN)",
                new Vector2(0.34f, 0.61f), new Vector2(0.49f, 0.69f));
            _tournamentTimeoutField = TournamentInput(panel.transform,
                "45", _tournamentDraft.matchTimeoutMinutes.ToString(),
                new Vector2(0.34f, 0.49f), new Vector2(0.49f, 0.61f), 3);
            FieldLabel(panel.transform, "REGRAS PERSONALIZADAS ID:LIMITE",
                new Vector2(0.51f, 0.61f), new Vector2(0.94f, 0.69f));
            _tournamentCustomBanField = TournamentInput(panel.transform,
                "Ex.: 12345678:0, 87654321:1", _tournamentCustomBan,
                new Vector2(0.51f, 0.49f), new Vector2(0.94f, 0.61f), 800);

            FieldLabel(panel.transform, "POOL PERMITIDO (IDs SEPARADOS POR VÍRGULA)",
                new Vector2(0.035f, 0.37f), new Vector2(0.49f, 0.45f));
            _tournamentPoolField = TournamentInput(panel.transform,
                "Vazio = todas as cartas", _tournamentPool,
                new Vector2(0.035f, 0.25f), new Vector2(0.49f, 0.37f), 1200);

            FieldLabel(panel.transform, "PONTOS V / D / WO / RODADAS",
                new Vector2(0.51f, 0.37f), new Vector2(0.94f, 0.45f));
            _tournamentPointsWinField = TournamentInput(panel.transform, "V",
                _tournamentDraft.pointsPerWin.ToString(),
                new Vector2(0.51f, 0.25f), new Vector2(0.60f, 0.37f), 2);
            _tournamentPointsLossField = TournamentInput(panel.transform, "D",
                _tournamentDraft.pointsPerLoss.ToString(),
                new Vector2(0.615f, 0.25f), new Vector2(0.705f, 0.37f), 2);
            _tournamentPointsWoField = TournamentInput(panel.transform, "WO",
                _tournamentDraft.pointsPerWalkover.ToString(),
                new Vector2(0.72f, 0.25f), new Vector2(0.81f, 0.37f), 2);
            _tournamentRoundsField = TournamentInput(panel.transform, "R",
                _tournamentDraft.pointsRoundCount.ToString(),
                new Vector2(0.825f, 0.25f), new Vector2(0.94f, 0.37f), 2);

            CreateButton(panel.transform, "VOLTAR",
                new Vector2(0.035f, 0.04f), new Vector2(0.28f, 0.17f),
                Blue, () =>
                {
                    SaveTournamentRuleFields();
                    RenderTournamentPage(TournamentPage.CreateBasics);
                });
            CreateButton(panel.transform,
                _tournamentEditing ? "SALVAR ALTERAÇÕES" : "CRIAR LOBBY",
                new Vector2(0.58f, 0.04f), new Vector2(0.94f, 0.17f),
                Lime, SubmitTournamentDraft);
            CreateTournamentFeedback(string.Empty);
        }

        private void BuildTournamentJoin()
        {
            BuildTournamentShell("ENTRAR EM TORNEIO",
                "Informe o código do organizador e a senha, se existir.",
                () => RenderTournamentPage(TournamentPage.Hub));
            Image panel = CreatePanel(_screenRoot, "Entrada",
                new Vector2(0.24f, 0.28f), new Vector2(0.76f, 0.76f),
                new Color(0.008f, 0.032f, 0.06f, 0.97f));
            AddOutline(panel.gameObject, Cyan, new Vector2(2f, -2f));
            CreateText(panel.transform, "CÓDIGO DO TORNEIO", 27,
                FontStyle.Bold, Color.white, new Vector2(0.08f, 0.75f),
                new Vector2(0.92f, 0.91f), TextAnchor.MiddleCenter);
            _tournamentJoinCodeField = TournamentInput(panel.transform,
                "ABC123", string.Empty, new Vector2(0.12f, 0.55f),
                new Vector2(0.88f, 0.72f), 12);
            _tournamentJoinCodeField.characterValidation =
                InputField.CharacterValidation.Alphanumeric;
            _tournamentJoinPasswordField = TournamentInput(panel.transform,
                "Senha (opcional)", string.Empty,
                new Vector2(0.12f, 0.34f), new Vector2(0.88f, 0.51f), 64);
            _tournamentJoinPasswordField.contentType =
                InputField.ContentType.Password;
            CreateButton(panel.transform, "ENTRAR",
                new Vector2(0.23f, 0.09f), new Vector2(0.77f, 0.27f),
                Lime, JoinTournamentFromUi);
            CreateTournamentFeedback(string.Empty);
        }

        private void BuildTournamentLobby()
        {
            TournamentState current = TournamentSession.State;
            if (current?.config == null)
            {
                RenderTournamentPage(TournamentPage.Hub);
                return;
            }
            if (current.config.status != TournamentStatus.Lobby)
            {
                RenderTournamentPage(current.config.status ==
                    TournamentStatus.Completed
                    ? TournamentPage.Final
                    : TournamentPage.Overview);
                return;
            }
            BuildTournamentShell("LOBBY DO TORNEIO",
                "Aguardando participantes, confirmação e validação de decks.",
                () => RenderTournamentPage(TournamentPage.Hub));
            CreateText(_screenRoot,
                $"CÓDIGO  •  {TournamentSession.LobbyCode}", 25,
                FontStyle.Bold, Cyan, new Vector2(0.07f, 0.77f),
                new Vector2(0.43f, 0.84f), TextAnchor.MiddleLeft);
            CreateButton(_screenRoot, "COPIAR", new Vector2(0.43f, 0.78f),
                new Vector2(0.55f, 0.835f), Blue,
                () => GUIUtility.systemCopyBuffer = TournamentSession.LobbyCode);
            CreateText(_screenRoot,
                $"PARTICIPANTES  •  {current.players.Count}/" +
                current.config.participantLimit, 19, FontStyle.Bold,
                Color.white, new Vector2(0.62f, 0.77f),
                new Vector2(0.93f, 0.84f), TextAnchor.MiddleRight);

            RectTransform list = CreateScrollGrid(_screenRoot,
                "Participantes do Torneio", new Vector2(0.055f, 0.25f),
                new Vector2(0.64f, 0.75f), new Vector2(410f, 105f),
                new Vector2(12f, 12f), 2);
            foreach (TournamentPlayer player in current.players)
                BuildTournamentPlayerTile(list, player, true);

            Image summary = CreatePanel(_screenRoot, "Resumo do Torneio",
                new Vector2(0.67f, 0.48f), new Vector2(0.94f, 0.75f),
                new Color(0.01f, 0.045f, 0.08f, 0.97f));
            AddOutline(summary.gameObject, Gold, new Vector2(2f, -2f));
            CreateText(summary.transform, "RESUMO", 23, FontStyle.Bold,
                Gold, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.95f),
                TextAnchor.MiddleLeft);
            CreateText(summary.transform, TournamentRulesSummary(current.config),
                17, FontStyle.Normal, Color.white,
                new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.81f),
                TextAnchor.UpperLeft);

            TournamentPlayer local = current.FindPlayer(
                TournamentSession.LocalPlayerId);
            BuildTournamentDeckControls(local);
            CreateButton(_screenRoot,
                local?.isReady == true ? "RETIRAR PRONTO" : "CONFIRMAR PRONTO",
                new Vector2(0.055f, 0.14f), new Vector2(0.30f, 0.22f),
                local?.isReady == true ? Blue : Lime,
                () => SetTournamentReady(local?.isReady != true));
            if (TournamentSession.IsOrganizer)
            {
                TournamentManager view = new TournamentManager(current);
                bool canStart = view.CanStart(out string rejection);
                string startLabel = canStart &&
                    current.players.Count < current.config.participantLimit
                    ? $"INICIAR COM {current.players.Count}"
                    : "INICIAR TORNEIO";
                CreateButton(_screenRoot, startLabel,
                    new Vector2(0.32f, 0.14f), new Vector2(0.57f, 0.22f),
                    Gold, StartTournamentFromUi);
                CreateButton(_screenRoot, "EDITAR", new Vector2(0.59f, 0.14f),
                    new Vector2(0.75f, 0.22f), Cyan, BeginTournamentEdit);
                CreateButton(_screenRoot, "CANCELAR", new Vector2(0.77f, 0.14f),
                    new Vector2(0.94f, 0.22f), Danger,
                    CancelTournamentFromUi);
                CreateTournamentFeedback(canStart
                    ? TournamentStartReadinessSummary(current)
                    : rejection);
            }
            else
            {
                CreateButton(_screenRoot, "SAIR DO TORNEIO",
                    new Vector2(0.70f, 0.14f), new Vector2(0.94f, 0.22f),
                    Danger, LeaveTournamentFromUi);
                CreateTournamentFeedback(TournamentSession.StatusMessage);
            }
        }

        private void BuildTournamentDeckControls(TournamentPlayer local)
        {
            bool random = local?.usesRandomDeck == true ||
                TournamentUsesRandomDeck;
            string deckName = local?.deckValid == true
                ? local.deckName
                : "AGUARDANDO ESCOLHA";
            Image panel = CreatePanel(_screenRoot, "Deck do Torneio",
                new Vector2(0.67f, 0.25f), new Vector2(0.94f, 0.46f),
                new Color(0.01f, 0.045f, 0.08f, 0.98f));
            AddOutline(panel.gameObject, random ? Gold : Cyan,
                new Vector2(2f, -2f));
            CreateText(panel.transform,
                "DECK DO TORNEIO  •  " + (random ? "ALEATÓRIO" : "MANUAL"),
                16, FontStyle.Bold, random ? Gold : Cyan,
                new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateText(panel.transform, deckName, 17, FontStyle.Bold,
                local?.deckValid == true ? Color.white : Muted,
                new Vector2(0.05f, 0.51f), new Vector2(0.95f, 0.73f),
                TextAnchor.MiddleLeft);

            CreateButton(panel.transform,
                "ALEATÓRIO\n" + YesNo(random),
                new Vector2(0.04f, 0.08f), new Vector2(0.47f, 0.47f),
                random ? Gold : Blue,
                () => ChangeTournamentDeckModeFromUi(!random));
            if (random)
            {
                CreateText(panel.transform,
                    "Sorteio entre seus decks válidos e desbloqueados.",
                    14, FontStyle.Bold, Muted,
                    new Vector2(0.51f, 0.08f), new Vector2(0.96f, 0.47f),
                    TextAnchor.MiddleCenter);
            }
            else
            {
                CreateButton(panel.transform, "ESCOLHER DECK",
                    new Vector2(0.51f, 0.08f), new Vector2(0.96f, 0.47f),
                    Lime,
                    () => RenderTournamentPage(
                        TournamentPage.DeckSelection));
            }
        }

        private void BuildTournamentDeckSelection()
        {
            TournamentState current = TournamentSession.State;
            if (current?.config == null)
            {
                RenderTournamentPage(TournamentPage.Hub);
                return;
            }

            EnsureTournamentRepository();
            BuildTournamentShell(
                "ESCOLHER DECK DO TORNEIO",
                "Somente decks deste perfil, desbloqueados, completos e válidos podem ser confirmados.",
                () => RenderTournamentPage(TournamentPage.Lobby));

            List<DeckRecord> decks = _repository?.State?.decks?
                .Where(deck => deck != null)
                .OrderBy(deck => deck.displayName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<DeckRecord>();
            if (decks.Count == 0)
            {
                CreateText(_screenRoot,
                    "Você ainda não possui decks neste perfil. Adquira ou monte um deck antes de confirmar presença.",
                    23, FontStyle.Bold, Gold,
                    new Vector2(0.16f, 0.38f), new Vector2(0.84f, 0.66f),
                    TextAnchor.MiddleCenter);
            }
            else
            {
                RectTransform grid = CreateScrollGrid(_screenRoot,
                    "Decks disponíveis para o torneio",
                    new Vector2(0.055f, 0.16f),
                    new Vector2(0.945f, 0.82f),
                    new Vector2(390f, 235f),
                    new Vector2(14f, 14f),
                    3);
                foreach (DeckRecord deck in decks)
                    BuildTournamentDeckChoiceTile(grid, deck);
            }

            CreateButton(_screenRoot, "VOLTAR AO LOBBY",
                new Vector2(0.055f, 0.07f), new Vector2(0.30f, 0.14f),
                Blue, () => RenderTournamentPage(TournamentPage.Lobby));
            CreateTournamentFeedback(
                "A seleção do torneio não altera o deck ativo dos outros modos.");
        }

        private void BuildTournamentDeckChoiceTile(
            Transform parent,
            DeckRecord deck)
        {
            bool eligible = TryCreateTournamentLoadout(
                deck.deckId,
                out _,
                out string rejection);
            string currentManualId = PlayerPrefs.GetString(
                TournamentManualDeckPreferenceKey,
                _repository.State?.selectedDeckId ?? string.Empty);
            bool selected = !TournamentUsesRandomDeck && string.Equals(
                currentManualId,
                deck.deckId,
                StringComparison.Ordinal);
            Color accent = selected ? Lime : eligible ? Cyan : Danger;
            Image tile = CreatePanel(parent, "Deck " + deck.deckId,
                Vector2.zero, Vector2.one,
                new Color(0.008f, 0.032f, 0.06f, 0.98f));
            AddOutline(tile.gameObject, accent,
                selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f));
            CreateFeaturedCards(tile.transform, deck,
                new Vector2(0.03f, 0.24f), new Vector2(0.49f, 0.91f));
            CreateText(tile.transform, deck.displayName, 20,
                FontStyle.Bold, Color.white,
                new Vector2(0.51f, 0.69f), new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateText(tile.transform,
                $"PRINCIPAL {deck.mainDeckCardIds.Count}  •  EXTRA {deck.extraDeckCardIds.Count}",
                14, FontStyle.Bold, eligible ? Lime : Gold,
                new Vector2(0.51f, 0.52f), new Vector2(0.96f, 0.69f),
                TextAnchor.MiddleCenter);
            if (eligible)
            {
                CreateButton(tile.transform,
                    selected ? "SELECIONADO" : "USAR NO TORNEIO",
                    new Vector2(0.52f, 0.12f), new Vector2(0.95f, 0.43f),
                    selected ? Lime : Cyan,
                    () => SelectTournamentDeckFromUi(deck.deckId));
            }
            else
            {
                CreateText(tile.transform,
                    string.IsNullOrWhiteSpace(rejection)
                        ? "Deck indisponível neste perfil."
                        : rejection,
                    13, FontStyle.Bold, Danger,
                    new Vector2(0.51f, 0.08f), new Vector2(0.96f, 0.49f),
                    TextAnchor.MiddleCenter);
            }
        }

        private void BuildTournamentOverview()
        {
            TournamentState current = RequireActiveTournament();
            if (current == null)
                return;
            if (current.config.status == TournamentStatus.Completed)
            {
                RenderTournamentPage(TournamentPage.Final);
                return;
            }
            BuildTournamentShell(current.config.name.ToUpperInvariant(),
                "Hub principal do campeonato.",
                () => RenderTournamentPage(TournamentPage.Hub), true);
            TournamentManager view = new TournamentManager(current);
            TournamentMatch match = view.ActiveMatchForPlayer(
                TournamentSession.LocalPlayerId);
            Image focus = CreatePanel(_screenRoot, "Próxima Partida",
                new Vector2(0.08f, 0.42f), new Vector2(0.67f, 0.72f),
                new Color(0.008f, 0.04f, 0.075f, 0.98f));
            AddOutline(focus.gameObject, Cyan, new Vector2(2f, -2f));
            CreateText(focus.transform, "SUA PRÓXIMA PARTIDA", 19,
                FontStyle.Bold, Cyan, new Vector2(0.06f, 0.76f),
                new Vector2(0.94f, 0.92f), TextAnchor.MiddleLeft);
            string matchText = match == null
                ? "Aguardando o resultado de outros confrontos"
                : $"{PlayerName(current, match.playerAId)}  VS  " +
                  $"{PlayerName(current, match.playerBId)}\n" +
                  $"{RoundName(current, match.roundId)}  •  BO{match.bestOf}  •  " +
                  MatchStatusLabel(match.status);
            CreateText(focus.transform, matchText, 25, FontStyle.Bold,
                Color.white, new Vector2(0.06f, 0.36f),
                new Vector2(0.94f, 0.73f), TextAnchor.MiddleCenter);
            Image enter = CreateButton(focus.transform, "ENTRAR NO DUELO",
                new Vector2(0.27f, 0.08f), new Vector2(0.73f, 0.30f),
                Lime, EnterTournamentMatchFromUi);
            enter.GetComponent<Button>().interactable = match != null &&
                match.status != TournamentMatchStatus.Waiting;

            Image summary = CreatePanel(_screenRoot, "Resumo",
                new Vector2(0.70f, 0.42f), new Vector2(0.92f, 0.72f),
                new Color(0.01f, 0.045f, 0.08f, 0.98f));
            AddOutline(summary.gameObject, Gold, new Vector2(2f, -2f));
            CreateText(summary.transform,
                $"PARTICIPANTES\n{current.players.Count}\n\n" +
                $"CONFRONTOS CONCLUÍDOS\n" +
                $"{current.matches.Count(item => item.status == TournamentMatchStatus.Finished)}\n\n" +
                $"RODADA ATUAL\n{current.currentRoundNumber}",
                18, FontStyle.Bold, Color.white, new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f), TextAnchor.MiddleCenter);

            CreateTournamentQuickCard("FORMATO",
                FormatLabel(current.config.formatType), 0.08f, Cyan);
            CreateTournamentQuickCard("DUELOS",
                current.stats.globalStats.totalDuels.ToString(), 0.31f, Blue);
            CreateTournamentQuickCard("CARTA MVP",
                TournamentCardName(current.stats.globalStats.mvpCardId),
                0.54f, Gold);
            CreateTournamentQuickCard("SEU STATUS",
                PlayerStatusLabel(current.FindPlayer(
                    TournamentSession.LocalPlayerId)), 0.77f, Lime);
            CreateTournamentFeedback(TournamentSession.StatusMessage);
        }

        private void BuildTournamentTabs()
        {
            (string label, TournamentPage page)[] tabs =
            {
                ("VISÃO GERAL", TournamentPage.Overview),
                ("CHAVE", TournamentPage.Bracket),
                ("CLASSIFICAÇÃO", TournamentPage.Standings),
                ("PARTICIPANTES", TournamentPage.Participants),
                ("REGRAS", TournamentPage.Rules),
                ("MÉTRICAS", TournamentPage.Metrics)
            };
            const float start = 0.055f;
            const float end = 0.945f;
            float width = (end - start) / tabs.Length;
            for (int index = 0; index < tabs.Length; index++)
            {
                float left = start + index * width;
                Color accent = _tournamentPage == tabs[index].page
                    ? Lime
                    : Cyan;
                CreateButton(_screenRoot, tabs[index].label,
                    new Vector2(left, 0.775f),
                    new Vector2(left + width - 0.006f, 0.835f), accent,
                    () => RenderTournamentPage(tabs[index].page));
            }
        }

        private void BuildTournamentBracket()
        {
            TournamentState current = RequireActiveTournament();
            if (current == null)
                return;
            BuildTournamentShell("CHAVE / RODADAS",
                current.config.formatType == TournamentFormatType.SingleElimination
                    ? "Progressão eliminatória do campeonato."
                    : "Agenda de confrontos por rodada.",
                () => RenderTournamentPage(TournamentPage.Overview), true);
            RectTransform content = CreateScrollGrid(_screenRoot, "Chave",
                new Vector2(0.055f, 0.12f), new Vector2(0.945f, 0.75f),
                new Vector2(500f, 142f), new Vector2(15f, 15f), 2);
            foreach (TournamentRound round in current.rounds)
            {
                foreach (string matchId in round.matchIds)
                {
                    TournamentMatch match = current.FindMatch(matchId);
                    if (match == null)
                        continue;
                    Image tile = CreatePanel(content,
                        round.displayName + " " + match.bracketIndex,
                        Vector2.zero, Vector2.one,
                        new Color(0.01f, 0.045f, 0.08f, 0.98f));
                    Color accent = match.Contains(TournamentSession.LocalPlayerId)
                        ? Lime
                        : match.status == TournamentMatchStatus.Finished
                            ? Gold
                            : Cyan;
                    AddOutline(tile.gameObject, accent, new Vector2(2f, -2f));
                    CreateText(tile.transform,
                        round.displayName.ToUpperInvariant() +
                        $"  •  BO{match.bestOf}", 14, FontStyle.Bold,
                        accent, new Vector2(0.05f, 0.72f),
                        new Vector2(0.95f, 0.94f), TextAnchor.MiddleLeft);
                    CreateText(tile.transform,
                        $"{PlayerName(current, match.playerAId)}   " +
                        $"{match.gamesWonByA}  ×  {match.gamesWonByB}   " +
                        $"{PlayerName(current, match.playerBId)}",
                        19, FontStyle.Bold, Color.white,
                        new Vector2(0.05f, 0.30f),
                        new Vector2(0.95f, 0.70f), TextAnchor.MiddleCenter);
                    bool canAwardWalkover = TournamentSession.IsOrganizer &&
                        current.config.allowWalkover && match.HasBothPlayers &&
                        (match.status == TournamentMatchStatus.Ready ||
                         match.status == TournamentMatchStatus.InProgress);
                    bool canReopen = TournamentSession.IsOrganizer &&
                        match.status == TournamentMatchStatus.InProgress;
                    bool hasOrganizerActions = canAwardWalkover || canReopen;
                    CreateText(tile.transform, MatchStatusLabel(match.status),
                        13, FontStyle.Bold, Muted,
                        new Vector2(0.05f,
                            hasOrganizerActions ? 0.21f : 0.07f),
                        new Vector2(0.95f,
                            hasOrganizerActions ? 0.30f : 0.28f),
                        TextAnchor.MiddleCenter);
                    if (!hasOrganizerActions)
                        continue;

                    bool confirming = string.Equals(
                        _tournamentPendingWoMatchId,
                        match.matchId,
                        StringComparison.Ordinal);
                    if (confirming)
                    {
                        CreateButton(tile.transform,
                            "CONFIRMAR WO " + TournamentCompactPlayerName(
                                current,
                                _tournamentPendingWoWinnerId),
                            new Vector2(0.05f, 0.02f),
                            new Vector2(0.48f, 0.19f), Danger,
                            ConfirmTournamentWalkoverFromUi);
                        CreateButton(tile.transform, "VOLTAR",
                            new Vector2(0.52f, 0.02f),
                            new Vector2(0.95f, 0.19f), Blue,
                            CancelTournamentWalkoverFromUi);
                    }
                    else if (canReopen && !canAwardWalkover)
                    {
                        CreateButton(tile.transform, "REABRIR CONFRONTO",
                            new Vector2(0.25f, 0.02f),
                            new Vector2(0.75f, 0.20f), Blue,
                            () => ReopenTournamentMatchFromUi(match.matchId));
                    }
                    else if (canReopen)
                    {
                        CreateButton(tile.transform,
                            "WO " + TournamentCompactPlayerName(
                                current,
                                match.playerAId),
                            new Vector2(0.03f, 0.02f),
                            new Vector2(0.31f, 0.20f), Danger,
                            () => RequestTournamentWalkover(
                                match.matchId,
                                match.playerAId));
                        CreateButton(tile.transform, "REABRIR",
                            new Vector2(0.35f, 0.02f),
                            new Vector2(0.65f, 0.20f), Blue,
                            () => ReopenTournamentMatchFromUi(match.matchId));
                        CreateButton(tile.transform,
                            "WO " + TournamentCompactPlayerName(
                                current,
                                match.playerBId),
                            new Vector2(0.69f, 0.02f),
                            new Vector2(0.97f, 0.20f), Danger,
                            () => RequestTournamentWalkover(
                                match.matchId,
                                match.playerBId));
                    }
                    else
                    {
                        CreateButton(tile.transform,
                            "WO " + TournamentCompactPlayerName(
                                current,
                                match.playerAId),
                            new Vector2(0.05f, 0.02f),
                            new Vector2(0.48f, 0.20f), Danger,
                            () => RequestTournamentWalkover(
                                match.matchId,
                                match.playerAId));
                        CreateButton(tile.transform,
                            "WO " + TournamentCompactPlayerName(
                                current,
                                match.playerBId),
                            new Vector2(0.52f, 0.02f),
                            new Vector2(0.95f, 0.20f), Danger,
                            () => RequestTournamentWalkover(
                                match.matchId,
                                match.playerBId));
                    }
                }
                if (string.IsNullOrWhiteSpace(round.byePlayerId))
                    continue;
                Image byeTile = CreatePanel(content,
                    round.displayName + " folga", Vector2.zero, Vector2.one,
                    new Color(0.01f, 0.045f, 0.08f, 0.98f));
                Color byeAccent = string.Equals(
                    round.byePlayerId,
                    TournamentSession.LocalPlayerId,
                    StringComparison.Ordinal)
                    ? Lime
                    : Gold;
                AddOutline(byeTile.gameObject, byeAccent,
                    new Vector2(2f, -2f));
                CreateText(byeTile.transform,
                    round.displayName.ToUpperInvariant() + "  •  FOLGA",
                    15, FontStyle.Bold, byeAccent,
                    new Vector2(0.05f, 0.63f),
                    new Vector2(0.95f, 0.91f), TextAnchor.MiddleLeft);
                CreateText(byeTile.transform,
                    PlayerName(current, round.byePlayerId) +
                    " descansa nesta rodada.",
                    19, FontStyle.Bold, Color.white,
                    new Vector2(0.05f, 0.18f),
                    new Vector2(0.95f, 0.62f), TextAnchor.MiddleCenter);
            }
            CreateTournamentFeedback(TournamentSession.StatusMessage);
        }

        private void BuildTournamentStandings()
        {
            TournamentState current = RequireActiveTournament();
            if (current == null)
                return;
            BuildTournamentShell("CLASSIFICAÇÃO",
                "Pontos, vitórias, confronto direto, saldo e dano.",
                () => RenderTournamentPage(TournamentPage.Overview), true);
            TournamentManager view = new TournamentManager(current);
            Image header = CreatePanel(_screenRoot, "Cabeçalho",
                new Vector2(0.07f, 0.68f), new Vector2(0.93f, 0.75f),
                new Color(0.02f, 0.10f, 0.15f, 0.98f));
            BuildStandingTexts(header.transform,
                "#", "JOGADOR", "J", "V", "D", "PONTOS", "SALDO", "STATUS",
                Gold);
            RectTransform rows = CreateScrollGrid(_screenRoot, "Tabela",
                new Vector2(0.07f, 0.14f), new Vector2(0.93f, 0.68f),
                new Vector2(1160f, 68f), new Vector2(0f, 7f), 1);
            foreach (TournamentPlayer player in view.OrderedStandings())
            {
                Image row = CreatePanel(rows, player.displayName,
                    Vector2.zero, Vector2.one,
                    new Color(0.006f, 0.035f, 0.065f, 0.97f));
                Color accent = player.playerId == TournamentSession.LocalPlayerId
                    ? Lime
                    : Cyan;
                AddOutline(row.gameObject, new Color(accent.r, accent.g,
                    accent.b, 0.55f), new Vector2(1f, -1f));
                BuildStandingTexts(row.transform,
                    player.rankPosition.ToString(),
                    player.displayName +
                    (player.playerId == TournamentSession.LocalPlayerId
                        ? "  •  VOCÊ" : string.Empty),
                    player.matchesPlayed.ToString(), player.wins.ToString(),
                    player.losses.ToString(), player.points.ToString(),
                    player.GameDifferential.ToString("+0;-0;0"),
                    PlayerStatusLabel(player), accent);
            }
            CreateTournamentFeedback(
                "Desempate: pontos → vitórias → confronto direto → saldo → dano.");
        }

        private void BuildTournamentParticipants()
        {
            TournamentState current = RequireActiveTournament();
            if (current == null)
                return;
            BuildTournamentShell("PARTICIPANTES",
                "Identidade, deck registrado e situação competitiva.",
                () => RenderTournamentPage(TournamentPage.Overview), true);
            RectTransform list = CreateScrollGrid(_screenRoot, "Participantes",
                new Vector2(0.055f, 0.13f), new Vector2(0.945f, 0.75f),
                new Vector2(520f, 130f), new Vector2(14f, 14f), 2);
            foreach (TournamentPlayer player in current.players
                         .OrderBy(item => item.rankPosition))
                BuildTournamentPlayerTile(list, player, false);
            CreateTournamentFeedback(TournamentSession.StatusMessage);
        }

        private void BuildTournamentRules()
        {
            TournamentState current = RequireActiveTournament();
            if (current == null)
                return;
            BuildTournamentShell("REGRAS",
                "Configuração competitiva bloqueada após o início.",
                () => RenderTournamentPage(TournamentPage.Overview), true);
            Image panel = CreatePanel(_screenRoot, "Regras Ativas",
                new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.72f),
                new Color(0.008f, 0.035f, 0.065f, 0.97f));
            AddOutline(panel.gameObject, Gold, new Vector2(2f, -2f));
            CreateText(panel.transform, TournamentRulesSummary(current.config),
                22, FontStyle.Bold, Color.white,
                new Vector2(0.06f, 0.12f), new Vector2(0.46f, 0.90f),
                TextAnchor.UpperLeft);
            string restrictions = current.config.banListMode ==
                TournamentBanListMode.Custom
                ? string.Join("\n", current.config.customBanList.Select(rule =>
                    $"• {TournamentCardName(rule.cardId)}: " +
                    (rule.maximumCopies == 0 ? "PROIBIDA" :
                        "MÁX. " + rule.maximumCopies)))
                : current.config.banListMode == TournamentBanListMode.Standard
                    ? "A lista padrão ativa do jogo será aplicada."
                    : "Somente o limite geral de 3 cópias será aplicado.";
            if (string.IsNullOrWhiteSpace(restrictions))
                restrictions = "Nenhuma restrição personalizada.";
            CreateText(panel.transform,
                "RESTRIÇÕES DE CARTAS\n\n" + restrictions,
                18, FontStyle.Normal, Muted,
                new Vector2(0.52f, 0.12f), new Vector2(0.94f, 0.90f),
                TextAnchor.UpperLeft);
            CreateTournamentFeedback(TournamentSession.StatusMessage);
        }

        private void BuildTournamentMetrics()
        {
            TournamentState current = RequireActiveTournament();
            if (current == null)
                return;
            BuildTournamentShell("MÉTRICAS",
                "Resumo estatístico do campeonato.",
                () => RenderTournamentPage(TournamentPage.Overview), true);
            string[] labels = { "VISÃO GERAL", "JOGADORES", "CARTAS", "PARTIDAS", "FINAL" };
            for (int index = 0; index < labels.Length; index++)
            {
                int selected = index;
                float left = 0.08f + index * 0.17f;
                CreateButton(_screenRoot, labels[index],
                    new Vector2(left, 0.70f), new Vector2(left + 0.16f, 0.755f),
                    _tournamentMetricsTab == index ? Lime : Cyan,
                    () =>
                    {
                        _tournamentMetricsTab = selected;
                        RenderTournamentPage(TournamentPage.Metrics);
                    });
            }
            switch (_tournamentMetricsTab)
            {
                case 1:
                    BuildPlayerMetrics(current);
                    break;
                case 2:
                    BuildCardMetrics(current);
                    break;
                case 3:
                    BuildMatchMetrics(current);
                    break;
                case 4:
                    BuildFinalMetrics(current);
                    break;
                default:
                    BuildOverviewMetrics(current);
                    break;
            }
            CreateTournamentFeedback(
                "As métricas são agregadas durante o duelo para não exceder o transporte.");
        }

        private void BuildOverviewMetrics(TournamentState current)
        {
            TournamentGlobalStats stats = current.stats.globalStats;
            string[] titles = { "PARTICIPANTES", "CONFRONTOS", "DUELOS", "DURAÇÃO" };
            string[] values =
            {
                stats.totalParticipants.ToString(), stats.totalMatches.ToString(),
                stats.totalDuels.ToString(), DurationLabel(stats.totalDurationTicks)
            };
            for (int index = 0; index < 4; index++)
            {
                float left = 0.08f + index * 0.215f;
                Image card = CreatePanel(_screenRoot, titles[index],
                    new Vector2(left, 0.50f), new Vector2(left + 0.19f, 0.66f),
                    new Color(0.008f, 0.04f, 0.075f, 0.98f));
                AddOutline(card.gameObject, index == 3 ? Gold : Cyan,
                    new Vector2(2f, -2f));
                CreateText(card.transform, titles[index], 14, FontStyle.Bold,
                    Muted, new Vector2(0.06f, 0.62f),
                    new Vector2(0.94f, 0.91f), TextAnchor.MiddleCenter);
                CreateText(card.transform, values[index], 28, FontStyle.Bold,
                    Color.white, new Vector2(0.06f, 0.10f),
                    new Vector2(0.94f, 0.61f), TextAnchor.MiddleCenter);
            }
            Image highlight = CreatePanel(_screenRoot, "Destaques",
                new Vector2(0.16f, 0.18f), new Vector2(0.84f, 0.45f),
                new Color(0.01f, 0.045f, 0.08f, 0.98f));
            AddOutline(highlight.gameObject, Gold, new Vector2(2f, -2f));
            CreateText(highlight.transform,
                $"MVP DO TORNEIO\n{PlayerName(current, stats.mvpPlayerId)}\n\n" +
                $"CARTA MAIS USADA\n{TournamentCardName(stats.mostUsedCardId)}",
                19, FontStyle.Bold, Color.white,
                new Vector2(0.06f, 0.12f), new Vector2(0.47f, 0.88f),
                TextAnchor.MiddleCenter);
            CreateText(highlight.transform,
                $"MAIS BANIDA\n{TournamentCardName(stats.mostBanishedCardId)}\n\n" +
                $"MAIOR DANO\n{TournamentCardName(stats.highestDamageCardId)}",
                19, FontStyle.Bold, Color.white,
                new Vector2(0.53f, 0.12f), new Vector2(0.94f, 0.88f),
                TextAnchor.MiddleCenter);
        }

        private void BuildPlayerMetrics(TournamentState current)
        {
            RectTransform content = CreateScrollGrid(_screenRoot,
                "Métricas por Jogador", new Vector2(0.08f, 0.14f),
                new Vector2(0.92f, 0.68f), new Vector2(520f, 150f),
                new Vector2(14f, 14f), 2);
            foreach (TournamentPlayerStats stats in current.stats.perPlayerStats
                         .OrderByDescending(item => item.duelsWon))
            {
                Image tile = CreatePanel(content, stats.playerId,
                    Vector2.zero, Vector2.one,
                    new Color(0.008f, 0.04f, 0.075f, 0.98f));
                AddOutline(tile.gameObject,
                    stats.playerId == TournamentSession.LocalPlayerId ? Lime : Cyan,
                    new Vector2(2f, -2f));
                CreateText(tile.transform, PlayerName(current, stats.playerId),
                    20, FontStyle.Bold, Color.white,
                    new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.92f),
                    TextAnchor.MiddleLeft);
                CreateText(tile.transform,
                    $"V {stats.duelsWon}  •  D {stats.duelsLost}  •  " +
                    $"WIN RATE {stats.WinRate:0.#}%\n" +
                    $"DANO {stats.damageDealt}  •  INVOCAÇÕES {stats.monstersSummoned}  •  " +
                    $"EFEITOS {stats.effectsActivated}",
                    15, FontStyle.Normal, Muted,
                    new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.68f),
                    TextAnchor.MiddleLeft);
            }
        }

        private void BuildCardMetrics(TournamentState current)
        {
            var cards = current.stats.perCardStats
                .Where(item => item != null &&
                    !string.IsNullOrWhiteSpace(item.cardId))
                .GroupBy(item => item.cardId)
                .Select(group => new
                {
                    Id = group.Key,
                    Drawn = group.Sum(item => item.timesDrawn),
                    Summoned = group.Sum(item => item.timesSummoned),
                    Activated = group.Sum(item => item.timesActivated),
                    Banished = group.Sum(item => item.timesBanished),
                    Damage = group.Sum(item => item.battleDamage + item.effectDamage)
                })
                .OrderByDescending(item => item.Summoned + item.Activated + item.Drawn)
                .Take(60)
                .ToList();
            RectTransform content = CreateScrollGrid(_screenRoot,
                "Métricas por Carta", new Vector2(0.08f, 0.14f),
                new Vector2(0.92f, 0.68f), new Vector2(520f, 125f),
                new Vector2(14f, 14f), 2);
            foreach (var card in cards)
            {
                Image tile = CreatePanel(content, card.Id, Vector2.zero,
                    Vector2.one, new Color(0.008f, 0.04f, 0.075f, 0.98f));
                AddOutline(tile.gameObject, Gold, new Vector2(1.5f, -1.5f));
                CreateText(tile.transform, TournamentCardName(card.Id), 19,
                    FontStyle.Bold, Color.white, new Vector2(0.05f, 0.66f),
                    new Vector2(0.95f, 0.92f), TextAnchor.MiddleLeft);
                CreateText(tile.transform,
                    $"COMPRADA {card.Drawn}  •  INVOCADA {card.Summoned}  •  " +
                    $"ATIVADA {card.Activated}\nBANIDA {card.Banished}  •  DANO {card.Damage}",
                    14, FontStyle.Normal, Muted,
                    new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.63f),
                    TextAnchor.MiddleLeft);
            }
            if (cards.Count == 0)
                EmptyTournamentState("As cartas aparecerão após os primeiros duelos.");
        }

        private void BuildMatchMetrics(TournamentState current)
        {
            RectTransform content = CreateScrollGrid(_screenRoot,
                "Métricas por Partida", new Vector2(0.08f, 0.14f),
                new Vector2(0.92f, 0.68f), new Vector2(1100f, 95f),
                new Vector2(0f, 10f), 1);
            foreach (TournamentMatchStats stats in current.stats.perMatchStats
                         .OrderByDescending(item => item.durationTicks))
            {
                Image row = CreatePanel(content, stats.matchId, Vector2.zero,
                    Vector2.one, new Color(0.008f, 0.04f, 0.075f, 0.98f));
                AddOutline(row.gameObject, Cyan, new Vector2(1f, -1f));
                CreateText(row.transform,
                    $"{PlayerName(current, stats.winnerId)} venceu " +
                    $"{PlayerName(current, stats.loserId)}", 18,
                    FontStyle.Bold, Color.white, new Vector2(0.03f, 0.48f),
                    new Vector2(0.50f, 0.90f), TextAnchor.MiddleLeft);
                CreateText(row.transform,
                    $"{stats.turns} turnos  •  {DurationLabel(stats.durationTicks)}  •  " +
                    $"dano {stats.winnerDamage}/{stats.loserDamage}", 15,
                    FontStyle.Normal, Muted, new Vector2(0.52f, 0.20f),
                    new Vector2(0.97f, 0.84f), TextAnchor.MiddleRight);
            }
        }

        private void BuildFinalMetrics(TournamentState current)
        {
            Image panel = CreatePanel(_screenRoot, "Final",
                new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.65f),
                new Color(0.008f, 0.04f, 0.075f, 0.98f));
            AddOutline(panel.gameObject, Gold, new Vector2(3f, -3f));
            CreateText(panel.transform,
                current.config.status == TournamentStatus.Completed
                    ? "CAMPEÃO\n" + PlayerName(current,
                        current.championPlayerId)
                    : "CAMPEONATO EM ANDAMENTO\nO pódio será calculado automaticamente.",
                31, FontStyle.Bold, Gold, new Vector2(0.08f, 0.35f),
                new Vector2(0.92f, 0.84f), TextAnchor.MiddleCenter);
            if (current.config.status == TournamentStatus.Completed)
            {
                CreateButton(panel.transform, "VER PÓDIO",
                    new Vector2(0.30f, 0.10f), new Vector2(0.70f, 0.28f),
                    Lime, () => RenderTournamentPage(TournamentPage.Final));
            }
        }

        private void BuildTournamentHistory()
        {
            BuildTournamentShell("TORNEIOS RECENTES",
                "Campeões, formatos e resultados salvos neste perfil.",
                () => RenderTournamentPage(TournamentPage.Hub));
            IReadOnlyList<TournamentState> history = TournamentSession.History;
            RectTransform content = CreateScrollGrid(_screenRoot, "Histórico",
                new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.78f),
                new Vector2(1100f, 130f), new Vector2(0f, 12f), 1);
            foreach (TournamentState item in history)
            {
                Image row = CreatePanel(content,
                    item.config?.name ?? "Torneio", Vector2.zero, Vector2.one,
                    new Color(0.008f, 0.04f, 0.075f, 0.98f));
                AddOutline(row.gameObject, Gold, new Vector2(2f, -2f));
                CreateText(row.transform,
                    item.config?.name ?? "Torneio", 22, FontStyle.Bold,
                    Color.white, new Vector2(0.04f, 0.58f),
                    new Vector2(0.58f, 0.90f), TextAnchor.MiddleLeft);
                CreateText(row.transform,
                    $"{FormatLabel(item.config.formatType)}  •  BO{item.config.bestOf}  •  " +
                    $"{item.players.Count} participantes", 15,
                    FontStyle.Normal, Muted, new Vector2(0.04f, 0.20f),
                    new Vector2(0.58f, 0.54f), TextAnchor.MiddleLeft);
                CreateText(row.transform,
                    "CAMPEÃO\n" + PlayerName(item, item.championPlayerId), 18,
                    FontStyle.Bold, Gold, new Vector2(0.62f, 0.18f),
                    new Vector2(0.95f, 0.86f), TextAnchor.MiddleCenter);
            }
            if (history.Count == 0)
                EmptyTournamentState("Nenhum campeonato foi encerrado ainda.");
            CreateTournamentFeedback(TournamentSession.StatusMessage);
        }

        private void BuildTournamentFinal()
        {
            TournamentState current = RequireActiveTournament();
            if (current == null)
                return;
            BuildTournamentShell("TORNEIO ENCERRADO",
                "Pódio e resumo final do campeonato.",
                () => RenderTournamentPage(TournamentPage.Hub));
            string first = current.podiumPlayerIds.ElementAtOrDefault(0);
            string second = current.podiumPlayerIds.ElementAtOrDefault(1);
            string third = current.podiumPlayerIds.ElementAtOrDefault(2);
            CreateText(_screenRoot, "CAMPEÃO  •  " + PlayerName(current, first),
                36, FontStyle.Bold, Gold, new Vector2(0.20f, 0.70f),
                new Vector2(0.80f, 0.82f), TextAnchor.MiddleCenter);
            PodiumCard("2º", PlayerName(current, second),
                new Vector2(0.18f, 0.28f), new Vector2(0.40f, 0.58f), Cyan);
            PodiumCard("1º", PlayerName(current, first),
                new Vector2(0.405f, 0.34f), new Vector2(0.625f, 0.65f), Gold);
            PodiumCard("3º", PlayerName(current, third),
                new Vector2(0.63f, 0.24f), new Vector2(0.85f, 0.54f), Lime);
            CreateButton(_screenRoot, "VER MÉTRICAS",
                new Vector2(0.30f, 0.13f), new Vector2(0.49f, 0.21f),
                Cyan, () => RenderTournamentPage(TournamentPage.Metrics));
            CreateButton(_screenRoot, "VER HISTÓRICO",
                new Vector2(0.51f, 0.13f), new Vector2(0.70f, 0.21f),
                Blue, () => RenderTournamentPage(TournamentPage.History));
            CreateTournamentFeedback("Resultado salvo automaticamente.");
        }

        private void PodiumCard(
            string place,
            string player,
            Vector2 min,
            Vector2 max,
            Color accent)
        {
            Image card = CreatePanel(_screenRoot, place + " " + player,
                min, max, new Color(0.008f, 0.04f, 0.075f, 0.98f));
            AddOutline(card.gameObject, accent, new Vector2(3f, -3f));
            CreateText(card.transform, place, 42, FontStyle.Bold, accent,
                new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.91f),
                TextAnchor.MiddleCenter);
            CreateText(card.transform, player, 24, FontStyle.Bold, Color.white,
                new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.52f),
                TextAnchor.MiddleCenter);
        }

        private void BuildTournamentPlayerTile(
            Transform parent,
            TournamentPlayer player,
            bool showValidation)
        {
            if (player == null)
                return;
            Color accent = ParseTournamentColor(player.colorTag);
            Image tile = CreatePanel(parent, player.displayName,
                Vector2.zero, Vector2.one,
                new Color(0.008f, 0.04f, 0.075f, 0.98f));
            AddOutline(tile.gameObject, accent, new Vector2(2f, -2f));
            string tags = player.isOrganizer ? "  •  ORGANIZADOR" : string.Empty;
            if (player.playerId == TournamentSession.LocalPlayerId)
                tags += "  •  VOCÊ";
            CreateText(tile.transform, player.displayName + tags, 18,
                FontStyle.Bold, Color.white, new Vector2(0.05f, 0.67f),
                new Vector2(0.95f, 0.92f), TextAnchor.MiddleLeft);
            CreateText(tile.transform,
                $"{PlayerStatusLabel(player)}  •  " +
                $"DECK{(player.usesRandomDeck ? " ALEATÓRIO" : string.Empty)}: " +
                $"{player.deckName ?? "AGUARDANDO"}" +
                (showValidation && !player.deckValid
                    ? "\n" + player.deckValidationMessage
                    : string.Empty),
                15, FontStyle.Normal,
                player.deckValid ? accent : Danger,
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.64f),
                TextAnchor.MiddleLeft);
        }

        private void BuildStandingTexts(
            Transform parent,
            string rank,
            string name,
            string played,
            string wins,
            string losses,
            string points,
            string differential,
            string status,
            Color color)
        {
            string[] values =
            {
                rank, name, played, wins, losses, points, differential, status
            };
            float[] widths = { .06f, .29f, .08f, .08f, .08f, .12f, .12f, .17f };
            float cursor = 0f;
            for (int index = 0; index < values.Length; index++)
            {
                CreateText(parent, values[index], index == 1 ? 17 : 15,
                    FontStyle.Bold, index == 0 ? color : Color.white,
                    new Vector2(cursor + .006f, .08f),
                    new Vector2(cursor + widths[index] - .006f, .92f),
                    index == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                cursor += widths[index];
            }
        }

        private void CreateTournamentQuickCard(
            string title,
            string value,
            float left,
            Color accent)
        {
            Image card = CreatePanel(_screenRoot, title,
                new Vector2(left, 0.19f), new Vector2(left + 0.18f, 0.35f),
                new Color(0.008f, 0.04f, 0.075f, 0.98f));
            AddOutline(card.gameObject, accent, new Vector2(2f, -2f));
            CreateText(card.transform, title, 13, FontStyle.Bold, Muted,
                new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.91f),
                TextAnchor.MiddleCenter);
            CreateText(card.transform, value, 20, FontStyle.Bold, Color.white,
                new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.61f),
                TextAnchor.MiddleCenter);
        }

        private void EmptyTournamentState(string message)
        {
            CreateText(_screenRoot, message, 21, FontStyle.Bold, Muted,
                new Vector2(0.20f, 0.30f), new Vector2(0.80f, 0.52f),
                TextAnchor.MiddleCenter);
        }

        private TournamentState RequireActiveTournament()
        {
            TournamentState current = TournamentSession.State;
            if (current?.config != null)
                return current;
            RenderTournamentPage(TournamentPage.Hub);
            return null;
        }

        private void ContinueTournament()
        {
            if (TournamentSession.HasTournament)
            {
                TournamentState current = TournamentSession.State;
                RenderTournamentPage(current.config.status switch
                {
                    TournamentStatus.Lobby => TournamentPage.Lobby,
                    TournamentStatus.Completed => TournamentPage.Final,
                    _ => TournamentPage.Overview
                });
                return;
            }
            ResumeTournamentFromUi();
        }

        private async void ResumeTournamentFromUi()
        {
            SetTournamentFeedback("Retomando o último estado íntegro...");
            TournamentOperationResult result =
                await TournamentSession.ResumeTournamentAsync();
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            ContinueTournament();
        }

        private async void JoinTournamentFromUi()
        {
            string code = _tournamentJoinCodeField?.text ?? string.Empty;
            string password = _tournamentJoinPasswordField?.text ?? string.Empty;
            SetTournamentFeedback("Autenticando e entrando no lobby...");
            TournamentOperationResult result = await TournamentSession
                .JoinTournamentAsync(code, password);
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.Lobby);
        }

        private async void SubmitTournamentDraft()
        {
            if (!SaveTournamentRuleFields())
                return;
            TournamentOperationResult validation =
                TournamentManager.ValidateConfig(_tournamentDraft);
            if (!validation.Success)
            {
                SetTournamentFeedback(validation.Message, true);
                return;
            }
            SetTournamentFeedback(_tournamentEditing
                ? "Revalidando regras e decks..."
                : "Criando o lobby na Unity...");
            TournamentOperationResult result = _tournamentEditing
                ? await TournamentSession.UpdateConfigAsync(
                    _tournamentDraft, _tournamentPassword)
                : await TournamentSession.CreateTournamentAsync(
                    _tournamentDraft, _tournamentPassword);
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.Lobby);
        }

        private async void SetTournamentReady(bool ready)
        {
            SetTournamentFeedback(ready
                ? "Validando e confirmando o deck..."
                : "Alterando status...");
            TournamentOperationResult result =
                await TournamentSession.SetReadyAsync(ready);
            if (!result.Success)
                SetTournamentFeedback(result.Message, true);
        }

        private async void ChangeTournamentDeckModeFromUi(
            bool useRandomDeck)
        {
            SetTournamentFeedback(useRandomDeck
                ? "Selecionando um deck aleatório entre os decks disponíveis..."
                : "Restaurando a seleção manual do torneio...");
            TournamentOperationResult result = await TournamentSession
                .UpdateDeckSelectionAsync(useRandomDeck, string.Empty);
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.Lobby);
        }

        private async void SelectTournamentDeckFromUi(string deckId)
        {
            SetTournamentFeedback("Validando propriedade e regras do deck...");
            TournamentOperationResult result = await TournamentSession
                .UpdateDeckSelectionAsync(false, deckId);
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.Lobby);
        }

        private async void StartTournamentFromUi()
        {
            SetTournamentFeedback("Salvando a chave e iniciando...");
            TournamentOperationResult result =
                await TournamentSession.StartTournamentAsync();
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.Overview);
        }

        private void EnterTournamentMatchFromUi()
        {
            TournamentOperationResult result =
                TournamentSession.EnterLocalMatch();
            SetTournamentFeedback(result.Message, !result.Success);
        }

        private void RequestTournamentWalkover(
            string matchId,
            string winnerId)
        {
            _tournamentPendingWoMatchId = matchId ?? string.Empty;
            _tournamentPendingWoWinnerId = winnerId ?? string.Empty;
            RenderTournamentPage(TournamentPage.Bracket);
        }

        private void CancelTournamentWalkoverFromUi()
        {
            _tournamentPendingWoMatchId = string.Empty;
            _tournamentPendingWoWinnerId = string.Empty;
            RenderTournamentPage(TournamentPage.Bracket);
        }

        private async void ConfirmTournamentWalkoverFromUi()
        {
            string matchId = _tournamentPendingWoMatchId;
            string winnerId = _tournamentPendingWoWinnerId;
            if (string.IsNullOrWhiteSpace(matchId) ||
                string.IsNullOrWhiteSpace(winnerId))
            {
                CancelTournamentWalkoverFromUi();
                return;
            }

            SetTournamentFeedback("Registrando WO e salvando a chave...");
            TournamentOperationResult result = await TournamentSession
                .AwardWalkoverAsync(matchId, winnerId);
            _tournamentPendingWoMatchId = string.Empty;
            _tournamentPendingWoWinnerId = string.Empty;
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.Bracket);
        }

        private async void ReopenTournamentMatchFromUi(string matchId)
        {
            SetTournamentFeedback(
                "Descartando a sala anterior e reabrindo o confronto...");
            TournamentOperationResult result =
                await TournamentSession.ReopenMatchAsync(matchId);
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.Bracket);
        }

        private async void CancelTournamentFromUi()
        {
            SetTournamentFeedback("Cancelando e salvando no histórico...");
            TournamentOperationResult result =
                await TournamentSession.CancelTournamentAsync();
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.History);
        }

        private async void LeaveTournamentFromUi()
        {
            TournamentOperationResult result =
                await TournamentSession.LeaveTournamentAsync();
            if (!result.Success)
            {
                SetTournamentFeedback(result.Message, true);
                return;
            }
            RenderTournamentPage(TournamentPage.Hub);
        }

        private void AdjustTournamentParticipants(int delta)
        {
            string name = _tournamentNameField?.text;
            string description = _tournamentDescriptionField?.text;
            int current = int.TryParse(
                    _tournamentParticipantsField?.text,
                    out int parsed)
                ? parsed
                : _tournamentDraft.participantLimit;
            current = Math.Max(2, Math.Min(32, current));
            if (current % 2 != 0)
                current += delta >= 0 ? 1 : -1;
            _tournamentDraft.participantLimit = Math.Max(
                2,
                Math.Min(32, current + delta));
            _tournamentDraft.pointsRoundCount = Math.Min(
                Math.Max(1, _tournamentDraft.pointsRoundCount),
                _tournamentDraft.participantLimit - 1);
            _tournamentName = name?.Trim() ?? _tournamentName;
            _tournamentDescription = description?.Trim() ??
                _tournamentDescription;
            _tournamentDraft.name = _tournamentName;
            _tournamentDraft.description = _tournamentDescription;
            RenderTournamentPage(TournamentPage.CreateBasics);
        }

        private void CycleTournamentBestOf()
        {
            if (!SaveTournamentBasics())
                return;
            int[] values = { 1, 3, 5, 7, 9, 11, 13, 15 };
            int index = Array.IndexOf(values, _tournamentDraft.bestOf);
            _tournamentDraft.bestOf = values[(index + 1) % values.Length];
            RenderTournamentPage(TournamentPage.CreateBasics);
        }

        private void CycleTournamentBanList()
        {
            SaveTournamentRuleFields();
            _tournamentDraft.banListMode = _tournamentDraft.banListMode switch
            {
                TournamentBanListMode.Standard => TournamentBanListMode.Custom,
                TournamentBanListMode.Custom => TournamentBanListMode.None,
                _ => TournamentBanListMode.Standard
            };
            RenderTournamentPage(TournamentPage.CreateRules);
        }

        private bool SaveTournamentBasics()
        {
            _tournamentName = _tournamentNameField?.text?.Trim() ??
                _tournamentName;
            _tournamentDescription =
                _tournamentDescriptionField?.text?.Trim() ??
                _tournamentDescription;
            _tournamentDraft.name = _tournamentName;
            _tournamentDraft.description = _tournamentDescription;
            if (_tournamentParticipantsField != null)
            {
                if (!int.TryParse(
                        _tournamentParticipantsField.text,
                        out int participants) ||
                    participants < 2 || participants > 32 ||
                    participants % 2 != 0)
                {
                    SetTournamentFeedback(
                        "Participantes: informe um número par entre 2 e 32.",
                        true);
                    return false;
                }
                _tournamentDraft.participantLimit = participants;
                _tournamentDraft.pointsRoundCount = Math.Min(
                    Math.Max(1, _tournamentDraft.pointsRoundCount),
                    participants - 1);
            }
            return true;
        }

        private bool SaveTournamentRuleFields()
        {
            _tournamentPassword = _tournamentPasswordField?.text ??
                _tournamentPassword;
            _tournamentCustomBan = _tournamentCustomBanField?.text ??
                _tournamentCustomBan;
            _tournamentPool = _tournamentPoolField?.text ?? _tournamentPool;
            if (!TryReadInt(_tournamentTimeoutField, 5, 240,
                    out _tournamentDraft.matchTimeoutMinutes, "timeout") ||
                !TryReadInt(_tournamentPointsWinField, 0, 99,
                    out _tournamentDraft.pointsPerWin, "pontos por vitória") ||
                !TryReadInt(_tournamentPointsLossField, 0, 99,
                    out _tournamentDraft.pointsPerLoss, "pontos por derrota") ||
                !TryReadInt(_tournamentPointsWoField, 0, 99,
                    out _tournamentDraft.pointsPerWalkover, "pontos por WO") ||
                !TryReadInt(_tournamentRoundsField, 1,
                    Math.Max(1, _tournamentDraft.participantLimit - 1),
                    out _tournamentDraft.pointsRoundCount, "rodadas"))
            {
                return false;
            }
            if (!TryParseCustomBanList(_tournamentCustomBan,
                    out List<TournamentCardRestriction> restrictions,
                    out string banError))
            {
                SetTournamentFeedback(banError, true);
                return false;
            }
            _tournamentDraft.customBanList = restrictions;
            List<string> pool = SplitCardIds(_tournamentPool);
            _tournamentDraft.allowedCardIds = pool;
            _tournamentDraft.allowedCardPoolMode = pool.Count == 0
                ? TournamentCardPoolMode.AllCards
                : TournamentCardPoolMode.SelectedCardsOnly;
            return true;
        }

        private bool TryReadInt(
            InputField field,
            int minimum,
            int maximum,
            out int value,
            string label)
        {
            if (int.TryParse(field?.text, out value) &&
                value >= minimum && value <= maximum)
            {
                return true;
            }
            SetTournamentFeedback(
                $"Valor inválido em {label}: use {minimum}–{maximum}.", true);
            return false;
        }

        private static bool TryParseCustomBanList(
            string source,
            out List<TournamentCardRestriction> restrictions,
            out string error)
        {
            restrictions = new List<TournamentCardRestriction>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(source))
                return true;
            foreach (string raw in source.Split(',', ';', '\n'))
            {
                string item = raw.Trim();
                if (item.Length == 0)
                    continue;
                string[] parts = item.Split(':');
                if (parts.Length != 2 ||
                    !uint.TryParse(parts[0].Trim(), out uint code) || code == 0 ||
                    !int.TryParse(parts[1].Trim(), out int maximum) ||
                    maximum < 0 || maximum > 3)
                {
                    error = "Regra personalizada inválida. Use ID:LIMITE, " +
                        "com limite entre 0 e 3.";
                    return false;
                }
                restrictions.Add(new TournamentCardRestriction
                {
                    cardId = code.ToString("00000000"),
                    maximumCopies = maximum
                });
            }
            return true;
        }

        private static List<string> SplitCardIds(string source)
        {
            return (source ?? string.Empty)
                .Split(',', ';', '\n', ' ')
                .Select(item => uint.TryParse(item.Trim(), out uint code) &&
                    code != 0 ? code.ToString("00000000") : string.Empty)
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private InputField TournamentInput(
            Transform parent,
            string placeholder,
            string value,
            Vector2 min,
            Vector2 max,
            int limit)
        {
            InputField field = CreateProfileNameField(parent, placeholder,
                min, max);
            field.text = value ?? string.Empty;
            field.characterLimit = limit;
            return field;
        }

        private static void FieldLabel(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max)
        {
            CreateText(parent, label, 16, FontStyle.Bold,
                new Color(0.76f, 0.84f, 0.90f, 1f), min, max,
                TextAnchor.MiddleLeft);
        }

        private static void SelectionButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action action)
        {
            CreateButton(parent, label, min, max, accent, action);
        }

        private void CreateTournamentFeedback(string message)
        {
            _tournamentFeedback = CreateText(_screenRoot, message ?? string.Empty,
                16, FontStyle.Bold, Muted, new Vector2(0.08f, 0.035f),
                new Vector2(0.92f, 0.095f), TextAnchor.MiddleCenter);
        }

        private void AttachTournamentHelpToVisibleControls()
        {
            if (_screenRoot == null)
                return;

            foreach (Button button in _screenRoot.GetComponentsInChildren<Button>(
                         true))
            {
                Text label = button.GetComponentInChildren<Text>(true);
                if (label == null || !TryGetTournamentButtonHelp(
                        label.text,
                        out string title,
                        out string body))
                {
                    continue;
                }
                AttachTournamentHelp(button.gameObject, title, body);
            }

            foreach (InputField input in _screenRoot
                         .GetComponentsInChildren<InputField>(true))
            {
                string placeholder = (input.placeholder as Text)?.text ??
                    input.gameObject.name;
                if (!TryGetTournamentInputHelp(
                        placeholder,
                        out string title,
                        out string body))
                {
                    continue;
                }
                AttachTournamentHelp(input.gameObject, title, body);
            }
        }

        private void AttachTournamentHelp(
            GameObject target,
            string title,
            string body)
        {
            if (target == null || string.IsNullOrWhiteSpace(body))
                return;
            TournamentHelpTrigger trigger =
                target.GetComponent<TournamentHelpTrigger>() ??
                target.AddComponent<TournamentHelpTrigger>();
            trigger.Configure(
                title,
                body,
                ShowTournamentHelp,
                HideTournamentHelp);
        }

        private void ShowTournamentHelp(
            RectTransform owner,
            string title,
            string body)
        {
            HideTournamentHelp();
            if (_screenRoot == null)
                return;

            Image panel = CreatePanel(_screenRoot,
                "Ajuda do Torneio",
                new Vector2(0.24f, 0.61f),
                new Vector2(0.76f, 0.82f),
                new Color(0.008f, 0.022f, 0.045f, 0.985f));
            panel.raycastTarget = false;
            AddOutline(panel.gameObject, Gold, new Vector2(3f, -3f));
            CreateText(panel.transform, title, 22, FontStyle.Bold, Gold,
                new Vector2(0.055f, 0.67f), new Vector2(0.945f, 0.93f),
                TextAnchor.MiddleLeft);
            CreateText(panel.transform, body, 17, FontStyle.Normal,
                Color.white,
                new Vector2(0.055f, 0.12f), new Vector2(0.945f, 0.67f),
                TextAnchor.UpperLeft);
            _tournamentHelpPanel = panel.gameObject;
            _tournamentHelpPanel.transform.SetAsLastSibling();
        }

        private void HideTournamentHelp()
        {
            if (_tournamentHelpPanel != null)
                Destroy(_tournamentHelpPanel);
            _tournamentHelpPanel = null;
        }

        private static bool TryGetTournamentButtonHelp(
            string label,
            out string title,
            out string body)
        {
            string key = (label ?? string.Empty)
                .Replace("\n", " ")
                .Trim()
                .ToUpperInvariant();
            title = key.Length == 0 ? "AJUDA" : key;
            body = string.Empty;
            if (key.Length == 0)
                return false;

            if (key == "+" || key == "−" || key == "-")
            {
                title = "PARTICIPANTES";
                body = "Aumenta ou reduz a capacidade máxima do torneio. O início com maioria pode liberar o campeonato antes de todas as vagas serem preenchidas.";
            }
            else if (key.Contains("FORMATO"))
                body = "Alterna entre mata-mata e pontos. Mata-mata elimina quem perde a série; pontos usa rodadas e classificação.";
            else if (key.Contains("CONFRONTO") || key.StartsWith("BO"))
                body = "Define quantos duelos formam cada confronto. Bo3, por exemplo, exige duas vitórias para vencer a série.";
            else if (key.Contains("INÍCIO COM MAIORIA"))
                body = "Permite iniciar quando houver mais da metade da capacidade ocupada, desde que todos os presentes tenham deck válido e estejam prontos.";
            else if (key.Contains("BAN LIST"))
                body = "Escolhe a lista de restrições das cartas: padrão, personalizada ou sem lista adicional.";
            else if (key.Contains("DECK BLOQUEADO"))
                body = "Quando ativado, o hash do deck é fixado no início. O jogador não poderá trocar cartas durante o campeonato.";
            else if (key.Contains("PRIVACIDADE"))
                body = "Torneios privados exigem código e podem usar senha. Torneios públicos podem ser encontrados pelos serviços online habilitados.";
            else if (key == "WO SIM" || key == "WO NÃO" || key.StartsWith("WO "))
                body = "Autoriza vitória por ausência quando um participante não puder concluir o confronto.";
            else if (key.Contains("ALEATÓRIO"))
                body = "Em SIM, o jogo sorteia somente entre seus decks completos, desbloqueados e válidos. Em NÃO, você escolhe manualmente.";
            else if (key.Contains("ESCOLHER DECK") ||
                     key.Contains("USAR NO TORNEIO") ||
                     key.Contains("SELECIONADO"))
                body = "Abre ou confirma o deck exclusivo deste torneio. A escolha não altera o deck ativo dos outros modos.";
            else if (key.Contains("CONFIRMAR PRONTO"))
                body = "Valida novamente o deck e informa ao organizador que você está preparado para iniciar.";
            else if (key.Contains("RETIRAR PRONTO"))
                body = "Retira sua confirmação para permitir uma nova escolha ou correção antes do início.";
            else if (key.Contains("INICIAR TORNEIO") ||
                     key.StartsWith("INICIAR COM"))
                body = "Cria a chave ou as rodadas usando apenas os participantes online, prontos e com decks válidos.";
            else if (key.Contains("COPIAR"))
                body = "Copia o código do lobby para a área de transferência, facilitando o envio aos demais participantes.";
            else if (key.Contains("PRÓXIMO"))
                body = "Salva os dados desta etapa e abre as regras avançadas do campeonato.";
            else if (key.Contains("CRIAR LOBBY"))
                body = "Valida todas as regras e cria o lobby online. Depois disso os participantes poderão entrar pelo código.";
            else if (key == "ENTRAR" || key.Contains("ABRIR ENTRADA"))
                body = "Entra no torneio usando o código e a senha informados, sem exigir que você saia para escolher um deck.";
            else if (key.Contains("ENTRAR NO DUELO"))
                body = "Abre a sala do confronto que foi atribuída a você nesta rodada.";
            else if (key.Contains("EDITAR"))
                body = "Retorna às etapas de criação para ajustar regras enquanto o torneio ainda está no lobby.";
            else if (key.Contains("CANCELAR"))
                body = "Cancela esta operação. Se o torneio já existir, o organizador poderá encerrá-lo para todos.";
            else if (key.Contains("SAIR DO TORNEIO"))
                body = "Remove você do lobby antes do início e preserva seus decks e sua coleção local.";
            else if (key.Contains("VOLTAR"))
                body = "Retorna à tela anterior sem apagar seus decks ou sua coleção.";
            else if (key.Contains("CLASSIFICAÇÃO"))
                body = "Mostra posição, partidas, vitórias, derrotas, pontos e critérios de desempate.";
            else if (key.Contains("CHAVE"))
                body = "Mostra confrontos, rodadas, resultados e progressão do campeonato.";
            else if (key.Contains("MÉTRICAS"))
                body = "Exibe estatísticas consolidadas do torneio, jogadores, partidas e cartas.";
            else if (key.Contains("REGRAS"))
                body = "Mostra o resumo das regras que estão valendo neste campeonato.";
            else
                body = "Executa a ação “" + (label ?? string.Empty).Trim() + "”. Mantenha o cursor ou o toque sobre outras opções para conhecer cada regra antes de confirmar.";
            return true;
        }

        private static bool TryGetTournamentInputHelp(
            string placeholder,
            out string title,
            out string body)
        {
            string key = (placeholder ?? string.Empty).Trim().ToUpperInvariant();
            title = "CAMPO DO TORNEIO";
            body = string.Empty;
            if (key.Contains("NOME"))
            {
                title = "NOME DO TORNEIO";
                body = "Nome público apresentado aos participantes e no histórico do campeonato.";
            }
            else if (key.Contains("DESCRI"))
            {
                title = "DESCRIÇÃO";
                body = "Resumo opcional para explicar o objetivo, horário ou regras especiais do evento.";
            }
            else if (key.Contains("2") && key.Contains("32"))
            {
                title = "CAPACIDADE";
                body = "Quantidade máxima, sempre par, entre 2 e 32 participantes.";
            }
            else if (key.Contains("SENHA"))
            {
                title = "SENHA PRIVADA";
                body = "Proteção opcional de 8 a 64 caracteres. O código sozinho não permitirá entrar quando houver senha.";
            }
            else if (key == "45")
            {
                title = "TIMEOUT";
                body = "Tempo máximo em minutos para concluir cada confronto antes de intervenção do organizador.";
            }
            else if (key.StartsWith("EX.:"))
            {
                title = "BAN LIST PERSONALIZADA";
                body = "Informe ID:limite separados por vírgula. Limite 0 proíbe; 1 limita; 2 permite até duas cópias.";
            }
            else if (key.Contains("TODAS AS CARTAS"))
            {
                title = "POOL PERMITIDO";
                body = "Deixe vazio para usar toda a coleção ou informe IDs separados por vírgula para restringir as cartas permitidas.";
            }
            else if (key == "ABC123")
            {
                title = "CÓDIGO DO TORNEIO";
                body = "Código compartilhado pelo organizador para localizar o lobby online correto.";
            }
            else if (key == "V" || key == "D" || key == "WO" || key == "R")
            {
                title = "PONTUAÇÃO E RODADAS";
                body = "Define, respectivamente, pontos por vitória, derrota, WO e quantidade de rodadas no formato por pontos.";
            }
            else
                return false;
            return true;
        }

        private void SetTournamentFeedback(string message, bool error = false)
        {
            if (_tournamentFeedback == null)
                return;
            _tournamentFeedback.text = message ?? string.Empty;
            _tournamentFeedback.color = error ? Danger : Lime;
        }

        private string TournamentCardName(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return "—";
            CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
            return entry?.DisplayName ?? cardId;
        }

        private static string PlayerName(TournamentState state, string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return "AGUARDANDO";
            return state?.FindPlayer(playerId)?.displayName ?? playerId;
        }

        private static string TournamentCompactPlayerName(
            TournamentState state,
            string playerId)
        {
            string value = PlayerName(state, playerId);
            return value.Length <= 12 ? value : value.Substring(0, 11) + "…";
        }

        private static string RoundName(TournamentState state, string roundId)
        {
            return state?.FindRound(roundId)?.displayName ?? "RODADA";
        }

        private static string TournamentRulesSummary(TournamentConfig config)
        {
            return $"FORMATO: {FormatLabel(config.formatType)}\n" +
                   $"PARTICIPANTES: {config.participantLimit}\n" +
                   $"CONFRONTO: BO{config.bestOf}\n" +
                   $"DECK BLOQUEADO: {YesNo(config.deckLocked)}\n" +
                   $"BAN LIST: {BanListLabel(config.banListMode)}\n" +
                   $"POOL: {(config.allowedCardPoolMode == TournamentCardPoolMode.AllCards ? "TODAS" : config.allowedCardIds.Count + " CARTAS")}\n" +
                   $"INÍCIO COM MAIORIA: {YesNo(config.allowEarlyStart)}\n" +
                   $"WO: {YesNo(config.allowWalkover)}\n" +
                   $"TIMEOUT: {config.matchTimeoutMinutes} MIN";
        }

        private static string TournamentStartReadinessSummary(
            TournamentState state)
        {
            int ready = state?.players?.Count(player =>
                player != null && player.isOnline && player.isReady &&
                player.deckValid && player.deck != null) ?? 0;
            int present = state?.players?.Count ?? 0;
            int capacity = state?.config?.participantLimit ?? 0;
            return present < capacity
                ? $"Todos os {ready} presentes estão prontos. O organizador " +
                  $"pode fechar a sala agora ({present}/{capacity})."
                : $"Todos os {ready} participantes estão prontos para iniciar.";
        }

        private static string FormatLabel(TournamentFormatType format)
        {
            return format == TournamentFormatType.SingleElimination
                ? "MATA-MATA"
                : "PONTOS";
        }

        private static string BanListLabel(TournamentBanListMode mode)
        {
            return mode switch
            {
                TournamentBanListMode.Standard => "PADRÃO",
                TournamentBanListMode.Custom => "PERSONALIZADA",
                _ => "SEM RESTRIÇÃO EXTRA"
            };
        }

        private static string YesNo(bool value) => value ? "SIM" : "NÃO";

        private static string MatchStatusLabel(TournamentMatchStatus status)
        {
            return status switch
            {
                TournamentMatchStatus.Ready => "LIBERADO",
                TournamentMatchStatus.InProgress => "EM DUELO",
                TournamentMatchStatus.Finished => "CONCLUÍDO",
                TournamentMatchStatus.Invalid => "INVÁLIDO",
                TournamentMatchStatus.Bye => "FOLGA",
                _ => "AGUARDANDO"
            };
        }

        private static string PlayerStatusLabel(TournamentPlayer player)
        {
            if (player == null)
                return "AGUARDANDO";
            if (!player.isOnline)
                return "OFFLINE";
            return player.status switch
            {
                TournamentPlayerStatus.Ready => "PRONTO",
                TournamentPlayerStatus.InDuel => "EM DUELO",
                TournamentPlayerStatus.Eliminated => "ELIMINADO",
                TournamentPlayerStatus.Offline => "OFFLINE",
                _ => player.deckValid ? "AGUARDANDO PRONTO" : "DECK INVÁLIDO"
            };
        }

        private static Color ParseTournamentColor(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color)
                ? color
                : Cyan;
        }

        private static string DurationLabel(long ticks)
        {
            if (ticks <= 0)
                return "0m";
            TimeSpan duration = TimeSpan.FromTicks(ticks);
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
                : $"{duration.Minutes}m {duration.Seconds}s";
        }
    }
}
