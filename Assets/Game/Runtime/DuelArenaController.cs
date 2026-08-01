using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Content;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneDuel.Game
{
    public sealed class DuelArenaController : MonoBehaviour
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private OcgDuelEngine engine;
        private ArcaneField3DPresenter fieldPresenter;
        private CardDatabase database;
        private CardVisualCatalog visuals;
        private CardViewRegistry cardViews;
        private readonly PlayerChoicePresenter choicePresenter =
            new PlayerChoicePresenter();
        private readonly DuelAnimationQueue animationQueue =
            new DuelAnimationQueue();
        private readonly TacticalOpponentAgent tacticalOpponent =
            new TacticalOpponentAgent();
        private ArcaneAudioDirector audioDirector;
        private DuelPresentationState state;
        private DeckFile playerDeck;
        private DeckFile opponentDeck;
        private Texture2D arenaBackground;
        private Texture2D white;
        private Texture2D cardBack;
        private Texture2D buttonNormal;
        private Texture2D buttonHover;
        private Texture2D buttonActive;
        private Texture2D phaseDisc;
        private Texture2D actionDisc;
        private GUIStyle titleStyle;
        private GUIStyle phaseStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle tinyStyle;
        private GUIStyle centeredStyle;
        private GUIStyle lifeStyle;
        private GUIStyle buttonStyle;
        private GUIStyle cardNameStyle;
        private string status = "Preparando arena...";
        private string deckStatus = string.Empty;
        private uint selectedCode;
        private byte selectedController = byte.MaxValue;
        private byte selectedLocation;
        private int selectedSequence = -1;
        private uint zoomCode;
        private readonly List<DuelChoice> contextualChoices =
            new List<DuelChoice>();
        private readonly HashSet<int> selectedPromptIndexes =
            new HashSet<int>();
        private readonly List<uint> playerExtraCards =
            new List<uint>();
        private readonly List<uint> zoneBrowserCards =
            new List<uint>();
        private string zoneBrowserTitle = string.Empty;
        private byte zoneBrowserController;
        private byte zoneBrowserLocation;
        private Vector2 zoneBrowserScroll;
        private Vector2 selectionTrayScroll;
        private Vector2 inspectorScroll;
        private bool showHistory;
        private bool showPhaseChoices;
        private bool autoPlay;
        private bool tutorialMode;
        private float nextAutoDecision;
        private Vector2 actionScroll;
        private bool externalPresentation;
        private bool presentationDecisionLocked;
        private bool networkReplica;
        private bool remotePlayerOneAuthority;
        private byte networkLocalPlayer;
        private DuelPrompt replicaPrompt;

        public bool ExternalPresentation => externalPresentation;
        public bool IsNetworkReplica => networkReplica;
        public byte NetworkLocalPlayer => networkLocalPlayer;
        public DuelPrompt CurrentPrompt => networkReplica
            ? replicaPrompt
            : engine?.CurrentPrompt;
        public DuelPresentationState PresentationState => state;
        public CardDatabase Database => database;
        public IReadOnlyList<uint> PlayerExtraDeckCards => playerExtraCards;
        public bool IsFinished => networkReplica
            ? state != null && state.Winner.HasValue
            : engine == null || engine.IsFinished;
        public bool PresentationDecisionLocked =>
            presentationDecisionLocked;
        public event Action<DuelEvent> CoreEventPresented;
        public event Action<string> CoreFailure;
        public event Action PresentationStateChanged;

        public bool TryGetCurrentCombatStats(
            byte controller,
            byte location,
            uint sequence,
            out int attack,
            out int defense)
        {
            if (engine != null)
            {
                return engine.TryGetCurrentCombatStats(
                    controller,
                    location,
                    sequence,
                    out attack,
                    out defense);
            }
            attack = 0;
            defense = 0;
            return false;
        }

        private void Awake()
        {
            Application.runInBackground = true;
            if (GetComponent<DuelDiagnosticsSettings>() == null)
                gameObject.AddComponent<DuelDiagnosticsSettings>();
            externalPresentation =
                GetComponent("CardArenaBootstrap") != null ||
                string.Equals(
                    SceneManager.GetActiveScene().name,
                    "DuelArena",
                    StringComparison.OrdinalIgnoreCase);
            if (!externalPresentation)
            {
                fieldPresenter = GetComponent<ArcaneField3DPresenter>() ??
                                 gameObject.AddComponent<ArcaneField3DPresenter>();
                fieldPresenter.EnsureBuilt();
                ConfigureCamera();
                InitializeTextures();
                LoadArenaBackground();
            }
            tutorialMode = PlayerPrefs.GetInt("ArcaneTutorialMode", 0) != 0;
            // The authored arena is always a manual player surface. A
            // persisted AUTO preference from the fallback/debug arena must
            // never consume player 0 prompts behind this UI.
            autoPlay =
                !externalPresentation &&
                PlayerPrefs.GetInt("ArcaneAutoStart", 0) != 0;
            try
            {
                database = CardDatabase.LoadDefault();
                visuals = CardVisualCatalog.LoadDefault();
                cardViews = new CardViewRegistry(visuals);
                state = new DuelPresentationState(database);
                playerDeck = DeckRepository.LoadActiveOrDefault(
                    database,
                    visuals,
                    out deckStatus);
                DeckLibraryFile library =
                    DeckLibraryRepository.LoadOrCreate(out string libraryStatus);
                opponentDeck = DeckLibraryRepository.FindOpponentOrDefault(
                    library,
                    PlayerPrefs.GetString(
                        CardLabNavigation.OpponentDeckKey,
                        "deck-mago-negro"));
                playerExtraCards.Clear();
                playerExtraCards.AddRange(playerDeck.extraDeck);
                deckStatus = $"{deckStatus} {libraryStatus}";
                state.ConfigureDeckCounts(
                    playerDeck.mainDeck.Count,
                    playerDeck.extraDeck.Count,
                    opponentDeck.mainDeck.Count,
                    opponentDeck.extraDeck.Count);

                DuelConfiguration configuration =
                    DuelConfiguration.VerticalSlice(DuelConfiguration.FreshSeed());
                configuration.PlayerDeck = playerDeck.mainDeck.ToArray();
                configuration.PlayerExtraDeck = playerDeck.extraDeck.ToArray();
                configuration.OpponentDeck = opponentDeck.mainDeck.ToArray();
                configuration.OpponentExtraDeck = opponentDeck.extraDeck.ToArray();
                configuration.SimpleOpponentAi = !externalPresentation;
                engine = OcgDuelEngine.CreateDefault(configuration);
                audioDirector = GetComponent<ArcaneAudioDirector>() ??
                                gameObject.AddComponent<ArcaneAudioDirector>();
                engine.EventReceived += OnCoreEvent;
                engine.Start();
                status = tutorialMode
                    ? "Treino guiado ativo · siga o painel à esquerda"
                    : "Duelo ativo · escolha uma ação válida";
                TryScheduleCommandLineCapture();
            }
            catch (Exception exception)
            {
                status =
                    $"Falha ao iniciar: {exception.GetBaseException().Message}";
                Debug.LogException(exception);
            }
        }

        private void Update()
        {
            animationQueue.Tick(Time.unscaledDeltaTime);
            if (ArcaneInput.EscapePressedThisFrame)
            {
                if (zoomCode != 0)
                {
                    zoomCode = 0;
                    return;
                }
                SceneManager.LoadScene(ProjectIdentity.BootstrapScene);
                return;
            }
            if (ArcaneInput.RefreshPressedThisFrame && state != null)
            {
                DuelPresentationSnapshot snapshot = state.CaptureSnapshot();
                state.RestoreSnapshot(snapshot);
                status = "Arena reconstruída integralmente do snapshot";
            }
            if (engine == null ||
                engine.IsFinished ||
                engine.CurrentPrompt == null ||
                presentationDecisionLocked ||
                Time.unscaledTime < nextAutoDecision)
            {
                return;
            }
            bool opponentPrompt = engine.CurrentPrompt.Player == 1;
            if (opponentPrompt && remotePlayerOneAuthority)
            {
                // A segunda cadeira pertence ao cliente remoto. Nunca deixe
                // a IA local consumir uma escolha que ainda precisa viajar
                // pelo Relay.
                return;
            }
            if (!opponentPrompt && !autoPlay)
            {
                return;
            }
            nextAutoDecision =
                Time.unscaledTime +
                (opponentPrompt
                    ? TacticalOpponentPolicy.DecisionDelay(
                        engine.CurrentPrompt)
                    : 0.32f);
            Submit(
                opponentPrompt
                    ? tacticalOpponent.Choose(
                        engine.CurrentPrompt,
                        state,
                        database)
                    : DeterministicDuelPolicy.Choose(engine.CurrentPrompt));
        }

        private void OnDestroy()
        {
            if (engine != null)
            {
                engine.EventReceived -= OnCoreEvent;
                engine.Dispose();
            }
            cardViews?.Dispose();
            if (arenaBackground != null) Destroy(arenaBackground);
            if (white != null) Destroy(white);
            if (cardBack != null) Destroy(cardBack);
            if (buttonNormal != null) Destroy(buttonNormal);
            if (buttonHover != null) Destroy(buttonHover);
            if (buttonActive != null) Destroy(buttonActive);
            if (phaseDisc != null) Destroy(phaseDisc);
            if (actionDisc != null) Destroy(actionDisc);
        }

        private void OnCoreEvent(DuelEvent duelEvent)
        {
            state.Apply(duelEvent);
            CardInstanceState affectedInstance =
                duelEvent.Current == null
                    ? null
                    : state.InstanceAt(
                        duelEvent.Current.Controller,
                        duelEvent.Current.Location,
                        duelEvent.Current.Sequence);
            DuelDevelopmentLog.Write(
                DuelLogCategory.CoreMessage,
                $"turn={state.TurnNumber}; phase={state.Phase:X}; " +
                $"message={duelEvent.Message}; player={duelEvent.Player}; " +
                $"priority={engine?.CurrentPrompt?.Player.ToString() ?? "-"}; " +
                $"request={duelEvent.Prompt?.RequestId ?? 0}; " +
                $"code={duelEvent.Code:00000000}; " +
                $"instance={affectedInstance?.Key.ToString() ?? "-"}; " +
                $"from={FormatLocation(duelEvent.Previous)}; " +
                $"to={FormatLocation(duelEvent.Current)}; " +
                $"legal={duelEvent.Prompt?.Choices.Count ?? 0}; " +
                $"result=LP({state.Players[0].LifePoints}," +
                $"{state.Players[1].LifePoints})");
            string[] instanceProblems =
                state.ValidateInstanceConsistency();
            if (instanceProblems.Length > 0)
            {
                DuelDevelopmentLog.Write(
                    DuelLogCategory.StateSync,
                    string.Join(" | ", instanceProblems),
                    this);
            }
            TrackPlayerExtraDeck(duelEvent);
            choicePresenter.Rebuild(duelEvent.Prompt ?? engine?.CurrentPrompt);
            contextualChoices.Clear();
            selectedPromptIndexes.Clear();
            if (DuelPresentationPreferences.TryResolve(
                    duelEvent,
                    out _,
                    out float animationSpeed))
            {
                animationQueue.Enqueue(duelEvent, animationSpeed);
            }
            audioDirector?.Play(duelEvent);
            if (duelEvent.IsUnknown)
            {
                Debug.LogWarning($"[Arcane Duel protocol] {duelEvent.Detail}");
            }
            CoreEventPresented?.Invoke(duelEvent);
        }

        private void OnGUI()
        {
            if (externalPresentation)
            {
                return;
            }
            EnsureStyles();
            if (fieldPresenter == null || !fieldPresenter.IsReady)
            {
                Color originalColor = GUI.color;
                GUI.color = new Color(0.004f, 0.008f, 0.018f);
                GUI.DrawTexture(
                    new Rect(0, 0, Screen.width, Screen.height),
                    white);
                GUI.color = originalColor;
            }

            float scale = Mathf.Min(
                Screen.width / DesignWidth,
                Screen.height / DesignHeight);
            float offsetX = (Screen.width - DesignWidth * scale) * 0.5f;
            float offsetY = (Screen.height - DesignHeight * scale) * 0.5f;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(offsetX, offsetY, 0f),
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            DrawBackdrop();
            if (state == null)
            {
                Panel(new Rect(420, 410, 1080, 220), 0.98f);
                GUI.Label(
                    new Rect(470, 455, 980, 120),
                    status,
                    titleStyle);
                GUI.matrix = previous;
                return;
            }

            DrawHeader();
            DrawPhaseOrb();
            DrawOpponentHand();
            DrawField();
            DrawHand();
            DrawActionPanel();
            DrawSelectionTray();
            DrawGlobalChoices();
            DrawContextActions();
            if (showHistory) DrawTimeline();
            DrawInspector();
            DrawZoneBrowser();
            DrawCardZoom();
            DrawVisualCue();
            DrawWinner();
            GUI.matrix = previous;
        }

        private void DrawBackdrop()
        {
            if (fieldPresenter != null && fieldPresenter.IsReady)
            {
                Fill(
                    new Rect(0, 0, DesignWidth, DesignHeight),
                    new Color(0.005f, 0.01f, 0.018f, 0.035f));
                Fill(
                    new Rect(0, 0, DesignWidth, 105),
                    new Color(0.002f, 0.008f, 0.018f, 0.80f));
            }
            else if (arenaBackground != null)
            {
                GUI.DrawTexture(
                    new Rect(0, 0, DesignWidth, DesignHeight),
                    arenaBackground,
                    ScaleMode.ScaleAndCrop);
            }
            else
            {
                Fill(
                    new Rect(0, 0, DesignWidth, DesignHeight),
                    new Color(0.012f, 0.025f, 0.055f));
            }
            if (fieldPresenter == null || !fieldPresenter.IsReady)
            {
                Fill(
                    new Rect(0, 0, DesignWidth, DesignHeight),
                    new Color(0.005f, 0.01f, 0.025f, 0.12f));
                Fill(
                    new Rect(0, 0, DesignWidth, 118),
                    new Color(0.004f, 0.012f, 0.03f, 0.88f));
            }
            Fill(new Rect(0, 0, DesignWidth, 6), new Color(0.15f, 0.92f, 1f));
            Fill(new Rect(0, 6, DesignWidth, 2), new Color(0.82f, 0.57f, 0.24f));
        }

        private void DrawHeader()
        {
            DrawLifeBadge(
                new Rect(1510, 18, 382, 92),
                "OPONENTE · IA DO CORE",
                state.Players[1].LifePoints,
                new Color(0.28f, 0.46f, 1f),
                true);
            DrawLifeBadge(
                new Rect(24, 934, 350, 122),
                "PLAYER · VOCÊ",
                state.Players[0].LifePoints,
                new Color(0.92f, 0.18f, 0.08f),
                false);

            string phase = state.Phase == 0
                ? "PREPARAÇÃO"
                : CoreMessageDecoder.PhaseName(state.Phase).ToUpperInvariant();
            Panel(new Rect(710, 16, 500, 76), 0.90f);
            GUI.Label(
                new Rect(730, 23, 460, 25),
                $"TURNO {Mathf.Max(1, state.TurnNumber)} · " +
                (state.TurnPlayer == 0 ? "SUA PRIORIDADE" : "PRIORIDADE DO RIVAL"),
                centeredStyle);
            GUI.Label(new Rect(730, 48, 460, 34), phase, phaseStyle);

            if (GUI.Button(
                new Rect(24, 20, 92, 46),
                "MENU",
                buttonStyle))
            {
                SceneManager.LoadScene(ProjectIdentity.BootstrapScene);
            }
            if (GUI.Button(
                new Rect(124, 20, 92, 46),
                "DECK",
                buttonStyle))
            {
                CardLabNavigation.Open(CardLabMode.Gallery);
                SceneManager.LoadScene(ProjectIdentity.CardLabScene);
            }
            if (GUI.Button(
                new Rect(224, 20, 54, 46),
                "?",
                buttonStyle))
            {
                tutorialMode = !tutorialMode;
            }
            if (audioDirector != null &&
                GUI.Button(
                    new Rect(286, 20, 78, 46),
                    audioDirector.Enabled ? "SOM" : "MUDO",
                    buttonStyle))
            {
                audioDirector.Enabled = !audioDirector.Enabled;
            }
            if (GUI.Button(
                new Rect(372, 20, 92, 46),
                autoPlay ? "AUTO" : "MANUAL",
                buttonStyle))
            {
                autoPlay = !autoPlay;
                nextAutoDecision = Time.unscaledTime + 0.25f;
            }
            if (GUI.Button(
                new Rect(472, 20, 68, 46),
                "LOG",
                buttonStyle))
            {
                showHistory = !showHistory;
            }
        }

        private void DrawLifeBadge(
            Rect rect,
            string player,
            int life,
            Color accent,
            bool alignRight)
        {
            Panel(rect, 0.92f);
            Fill(
                new Rect(
                    alignRight ? rect.xMax - 7 : rect.x,
                    rect.y,
                    7,
                    rect.height),
                accent);
            TextAnchor oldAlignment = tinyStyle.alignment;
            tinyStyle.alignment =
                alignRight ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            GUI.Label(
                new Rect(rect.x + 20, rect.y + 11, rect.width - 40, 22),
                player,
                tinyStyle);
            tinyStyle.alignment = oldAlignment;
            TextAnchor oldLifeAlignment = lifeStyle.alignment;
            lifeStyle.alignment =
                alignRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            GUI.Label(
                new Rect(rect.x + 18, rect.y + 31, rect.width - 36, 43),
                $"{life:N0} PV",
                lifeStyle);
            lifeStyle.alignment = oldLifeAlignment;
        }

        private void DrawPhaseRibbon()
        {
            (uint Value, string Label)[] phases =
            {
                (0x01, "COMPRA"),
                (0x02, "APOIO"),
                (0x04, "PRINCIPAL 1"),
                (0x80, "BATALHA"),
                (0x100, "PRINCIPAL 2"),
                (0x200, "FINAL")
            };
            float width = 112f;
            float start = 624f;
            for (int index = 0; index < phases.Length; index++)
            {
                bool active =
                    state.Phase == phases[index].Value ||
                    (phases[index].Value == 0x80 &&
                     state.Phase >= 0x08 &&
                     state.Phase <= 0x80);
                Rect rect = new Rect(start + index * width, 105, width - 4, 25);
                Fill(
                    rect,
                    active
                        ? new Color(0.10f, 0.78f, 0.92f, 0.96f)
                        : new Color(0.015f, 0.04f, 0.08f, 0.88f));
                GUIStyle style = new GUIStyle(tinyStyle)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor =
                    active ? new Color(0.01f, 0.04f, 0.07f) : new Color(0.58f, 0.72f, 0.80f);
                GUI.Label(rect, phases[index].Label, style);
            }
        }

        private void DrawPhaseOrb()
        {
            if (phaseDisc == null ||
                zoneBrowserCards.Count > 0 ||
                zoomCode != 0)
            {
                return;
            }
            Rect orb = new Rect(1572, 385, 138, 138);
            GUI.DrawTexture(orb, phaseDisc, ScaleMode.StretchToFill);
            string phase = state.Phase switch
            {
                0x01 => "DRAW",
                0x02 => "STANDBY",
                0x04 => "MAIN 1",
                >= 0x08 and <= 0x80 => "BATTLE",
                0x100 => "MAIN 2",
                0x200 => "END",
                _ => "DUEL"
            };
            GUI.Label(
                new Rect(orb.x + 12, orb.y + 37, orb.width - 24, 62),
                $"TURNO {Mathf.Max(1, state.TurnNumber)}\n{phase}",
                centeredStyle);
            if (GUI.Button(orb, GUIContent.none, GUIStyle.none))
            {
                DuelPrompt prompt = engine?.CurrentPrompt;
                bool canAdvance =
                    prompt != null &&
                    prompt.Choices.Any(IsPhaseChoice);
                if (canAdvance)
                {
                    showPhaseChoices = !showPhaseChoices;
                }
                else
                {
                    status =
                        "O Core ainda não liberou uma mudança de fase nesta janela.";
                }
            }
        }

        private void DrawOpponentHand()
        {
            int count = state.Players[1].Hand.Count;
            if (count <= 0) count = 5;
            float start = 960f - (Mathf.Min(count, 8) * 25f);
            for (int index = 0; index < Mathf.Min(count, 8); index++)
            {
                Rect card = new Rect(start + index * 50f, 132, 50, 72);
                GUI.DrawTexture(card, cardBack, ScaleMode.StretchToFill);
                Stroke(card, new Color(0.82f, 0.34f, 0.68f, 0.72f), 1);
            }
            GUI.Label(
                new Rect(810, 135, 190, 24),
                $"MÃO DO RIVAL · {count}",
                tinyStyle);
        }

        private void DrawField()
        {
            DrawZoneRow(
                state.Players[1].SpellTrapZones,
                507,
                216,
                true,
                false);
            DrawZoneRow(
                state.Players[1].MonsterZones,
                507,
                364,
                true,
                true);
            DrawZoneRow(
                state.Players[0].MonsterZones,
                507,
                520,
                false,
                true);
            DrawZoneRow(
                state.Players[0].SpellTrapZones,
                507,
                668,
                false,
                false);

            DrawPile(362, 210, "EXTRA", state.Players[1].ExtraDeckCount, true);
            DrawPile(1464, 210, "DECK", state.Players[1].DeckCount, true);
            DrawDiscard(1464, 365, "CEMITÉRIO", state.Players[1].Graveyard, true);
            DrawDiscard(362, 365, "BANIDAS", state.Players[1].Banished, true);

            DrawDiscard(362, 520, "BANIDAS", state.Players[0].Banished, false);
            DrawDiscard(1464, 520, "CEMITÉRIO", state.Players[0].Graveyard, false);
            DrawPile(362, 676, "EXTRA", state.Players[0].ExtraDeckCount, false);
            DrawPile(1464, 676, "DECK", state.Players[0].DeckCount, false);
        }

        private void DrawZoneRow(
            uint[] zones,
            float startX,
            float y,
            bool opponent,
            bool monster)
        {
            Color accent = monster
                ? new Color(0.12f, 0.88f, 0.98f)
                : new Color(0.94f, 0.68f, 0.24f);
            byte controller = opponent ? (byte)1 : (byte)0;
            byte location = monster
                ? (byte)DuelLocation.MonsterZone
                : (byte)DuelLocation.SpellTrapZone;
            for (int index = 0; index < 5; index++)
            {
                Rect zone = fieldPresenter != null && fieldPresenter.IsReady
                    ? fieldPresenter.ZoneRect(
                        controller,
                        location,
                        index,
                        DesignWidth,
                        DesignHeight)
                    : new Rect(startX + index * 187f, y, 112, 139);
                DuelChoice legalPlacement = FindLocationChoice(
                    controller,
                    location,
                    (uint)index);
                bool legalZone = legalPlacement != null;
                uint code = zones[index];
                bool immersive =
                    fieldPresenter != null &&
                    fieldPresenter.IsReady;
                if (!immersive || legalZone)
                {
                    Fill(
                        zone,
                        new Color(
                            legalZone ? 0.20f : accent.r * 0.08f,
                            legalZone ? 0.42f : accent.g * 0.08f,
                            legalZone ? 0.04f : accent.b * 0.08f,
                            legalZone ? 0.62f : 0.48f));
                    Stroke(
                        zone,
                        legalZone
                            ? new Color(0.68f, 1f, 0.04f, 1f)
                            : new Color(accent.r, accent.g, accent.b, 0.58f),
                        legalZone ? 5 : 2);
                }
                if (code == 0)
                {
                    if (!immersive || legalZone)
                    {
                        GUIStyle zoneStyle = new GUIStyle(tinyStyle)
                        {
                            alignment = TextAnchor.MiddleCenter
                        };
                        zoneStyle.normal.textColor = legalZone
                            ? new Color(0.80f, 1f, 0.36f)
                            : new Color(accent.r, accent.g, accent.b, 0.56f);
                        GUI.Label(
                            zone,
                            legalZone
                                ? "CLIQUE AQUI"
                                : monster
                                    ? $"MONSTRO {index + 1}"
                                    : $"MAGIA / ARM. {index + 1}",
                            zoneStyle);
                    }
                    if (legalZone &&
                        GUI.Button(zone, GUIContent.none, GUIStyle.none))
                    {
                        Submit(legalPlacement);
                    }
                }
                else
                {
                    DrawCard(
                        new Rect(zone.x + 7, zone.y + 6, 98, 128),
                        code,
                        opponent,
                        true,
                        controller,
                        location,
                        index);
                }
            }
        }

        private void DrawPile(
            float x,
            float y,
            string name,
            int count,
            bool opponent)
        {
            byte controller = opponent ? (byte)1 : (byte)0;
            byte location = name == "EXTRA"
                ? (byte)DuelLocation.Extra
                : (byte)DuelLocation.Deck;
            Rect pile = fieldPresenter != null && fieldPresenter.IsReady
                ? fieldPresenter.SpecialRect(
                    controller,
                    location,
                    DesignWidth,
                    DesignHeight)
                : new Rect(x, y, 86, 122);
            x = pile.x;
            y = pile.y;
            Rect shadow = new Rect(x + 6, y + 7, pile.width, pile.height);
            Fill(shadow, new Color(0f, 0f, 0f, 0.48f));
            GUI.DrawTexture(pile, cardBack, ScaleMode.StretchToFill);
            Color accent = opponent
                ? new Color(0.92f, 0.26f, 0.52f)
                : new Color(0.10f, 0.88f, 1f);
            Stroke(pile, accent, 2);
            Rect badge = new Rect(x + 50, y + 93, 48, 36);
            Fill(badge, new Color(0.005f, 0.02f, 0.045f, 0.95f));
            Stroke(badge, accent, 1);
            GUIStyle countStyle = new GUIStyle(centeredStyle)
            {
                fontSize = 16
            };
            GUI.Label(badge, count.ToString(), countStyle);
            GUI.Label(
                new Rect(x - 10, y + 132, 112, 24),
                name,
                tinyStyle);
            if (GUI.Button(pile, GUIContent.none, GUIStyle.none))
            {
                if (name == "EXTRA" && !opponent)
                {
                    OpenZoneBrowser(
                        "SEU EXTRA DECK",
                        playerExtraCards,
                        0,
                        (byte)DuelLocation.Extra);
                }
                else
                {
                    status = opponent
                        ? $"{name} do rival: {count} carta(s); conteúdo oculto."
                        : $"{name}: {count} carta(s). O conteúdo do Deck Principal permanece oculto durante o duelo.";
                }
            }
        }

        private void DrawDiscard(
            float x,
            float y,
            string name,
            List<uint> cards,
            bool opponent)
        {
            byte controller = opponent ? (byte)1 : (byte)0;
            byte location = name == "CEMITÉRIO"
                ? (byte)DuelLocation.Graveyard
                : (byte)DuelLocation.Banished;
            Rect zone = fieldPresenter != null && fieldPresenter.IsReady
                ? fieldPresenter.SpecialRect(
                    controller,
                    location,
                    DesignWidth,
                    DesignHeight)
                : new Rect(x, y, 86, 122);
            x = zone.x;
            y = zone.y;
            if (cards.Count == 0)
            {
                if (fieldPresenter == null || !fieldPresenter.IsReady)
                {
                    Fill(zone, new Color(0.04f, 0.055f, 0.09f, 0.66f));
                    Stroke(
                        zone,
                        opponent
                            ? new Color(0.66f, 0.26f, 0.55f, 0.62f)
                            : new Color(0.12f, 0.68f, 0.84f, 0.62f),
                        2);
                    GUIStyle icon = new GUIStyle(centeredStyle)
                    {
                        fontSize = 30
                    };
                    GUI.Label(zone, "◇", icon);
                }
            }
            else
            {
                DrawCard(
                    zone,
                    cards[cards.Count - 1],
                    opponent,
                    true,
                    opponent ? (byte)1 : (byte)0,
                    name == "CEMITÉRIO"
                        ? (byte)DuelLocation.Graveyard
                        : (byte)DuelLocation.Banished,
                    cards.Count - 1);
            }
            GUI.Label(
                new Rect(x - 12, y + 132, 126, 24),
                $"{name} · {cards.Count}",
                tinyStyle);
            Rect readableLocationLabel =
                new Rect(x - 12, y + 130, 126, 27);
            Fill(
                readableLocationLabel,
                new Color(0.004f, 0.018f, 0.038f, 0.90f));
            GUIStyle readableLocationStyle = new GUIStyle(tinyStyle)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(
                readableLocationLabel,
                $"{name} · {cards.Count}",
                readableLocationStyle);
            if (cards.Count > 0 &&
                GUI.Button(
                    new Rect(x - 12, y + 128, 126, 30),
                    "ABRIR",
                    buttonStyle))
            {
                OpenZoneBrowser(
                    $"{name} · {(opponent ? "RIVAL" : "VOCÊ")}",
                    cards,
                    opponent ? (byte)1 : (byte)0,
                    name == "CEMITÉRIO"
                        ? (byte)DuelLocation.Graveyard
                        : (byte)DuelLocation.Banished);
            }
        }

        private void DrawHand()
        {
            List<uint> hand = state.Players[0].Hand;
            const float cardWidth = 148f;
            const float cardHeight = 210f;
            float spacing = hand.Count <= 1
                ? 0f
                : Mathf.Min(96f, 720f / (hand.Count - 1));
            float span = hand.Count <= 1
                ? cardWidth
                : cardWidth + (hand.Count - 1) * spacing;
            float start = 960f - span * 0.5f;
            Fill(
                new Rect(410, 822, 1100, 3),
                new Color(0.12f, 0.88f, 1f, 0.62f));
            GUI.Label(
                new Rect(430, 827, 610, 28),
                $"SUA MÃO · {hand.Count} CARTAS  ·  CLIQUE PARA JOGAR",
                subtitleStyle);
            int selectedIndex =
                selectedController == 0 &&
                selectedLocation == (byte)DuelLocation.Hand &&
                selectedSequence >= 0 &&
                selectedSequence < hand.Count
                    ? selectedSequence
                    : -1;
            int hoveredIndex = -1;
            for (int index = 0; index < hand.Count; index++)
            {
                Rect baseRect = new Rect(
                    start + index * spacing,
                    866,
                    cardWidth,
                    cardHeight);
                if (baseRect.Contains(Event.current.mousePosition))
                    hoveredIndex = index;
            }

            int focusIndex = selectedIndex >= 0
                ? selectedIndex
                : hoveredIndex;
            for (int index = 0; index < hand.Count; index++)
            {
                if (index == focusIndex) continue;
                DrawHandCard(
                    hand,
                    index,
                    start,
                    spacing,
                    cardWidth,
                    cardHeight,
                    focusIndex,
                    false);
            }
            if (focusIndex >= 0 && focusIndex < hand.Count)
            {
                DrawHandCard(
                    hand,
                    focusIndex,
                    start,
                    spacing,
                    cardWidth,
                    cardHeight,
                    focusIndex,
                    true);
            }
        }

        private void DrawHandCard(
            IReadOnlyList<uint> hand,
            int index,
            float start,
            float spacing,
            float cardWidth,
            float cardHeight,
            int focusIndex,
            bool focused)
        {
            float separation = 0f;
            if (focusIndex >= 0 && !focused)
                separation = index < focusIndex ? -18f : 18f;

            bool candidate = choicePresenter.IsCandidate(hand[index]);
            Rect card = focused
                ? new Rect(
                    start + index * spacing - 15f,
                    candidate ? 760f : 782f,
                    178f,
                    252f)
                : new Rect(
                    start + index * spacing + separation,
                    candidate ? 840f : 866f,
                    cardWidth,
                    cardHeight);
            float centerIndex = (hand.Count - 1) * 0.5f;
            float angle = focused
                ? 0f
                : Mathf.Clamp(
                    (index - centerIndex) * 2.35f,
                    -6f,
                    6f);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, card.center);
            DrawCard(
                card,
                hand[index],
                false,
                false,
                0,
                (byte)DuelLocation.Hand,
                index);
            GUI.matrix = matrix;
        }

        private void DrawActionPanel()
        {
            DuelPrompt prompt = engine?.CurrentPrompt;
            if (prompt == null && !tutorialMode) return;

            Rect panel = new Rect(585, 110, 750, 82);
            Panel(panel, 0.92f);
            Fill(
                new Rect(panel.x, panel.yMax - 4, panel.width, 4),
                new Color(0.68f, 1f, 0.04f));
            GUIStyle promptTitle = new GUIStyle(subtitleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18
            };
            GUI.Label(
                new Rect(610, 117, 700, 28),
                prompt == null ? "FLUXO DO DUELO" : prompt.Title.ToUpperInvariant(),
                promptTitle);
            GUI.Label(
                new Rect(615, 146, 690, 34),
                prompt == null
                    ? "O Core está resolvendo a jogada."
                    : InteractionInstruction(prompt),
                centeredStyle);

            if (tutorialMode && prompt == null)
            {
                GUI.Label(
                    new Rect(540, 205, 840, 20),
                    TutorialText(prompt),
                    tinyStyle);
            }
        }

        private void DrawTimeline()
        {
            Rect panel = new Rect(1590, 145, 310, 494);
            Panel(panel, 0.92f);
            GUI.Label(
                new Rect(1615, 165, 260, 30),
                "HISTÓRICO DO DUELO",
                subtitleStyle);
            Fill(
                new Rect(1615, 202, 260, 1),
                new Color(0.20f, 0.75f, 0.84f, 0.48f));
            int first = Mathf.Max(0, state.Log.Count - 8);
            float y = 218;
            for (int index = first; index < state.Log.Count; index++)
            {
                string log = state.Log[index];
                if (log.StartsWith("Protocolo:", StringComparison.Ordinal))
                {
                    continue;
                }
                Fill(
                    new Rect(1617, y + 7, 5, 5),
                    new Color(0.30f, 0.86f, 0.95f));
                GUI.Label(
                    new Rect(1630, y, 244, 47),
                    log,
                    tinyStyle);
                y += 51;
            }
        }

        private void DrawInspector()
        {
            if (selectedCode == 0 ||
                !database.TryGet(selectedCode, out CardRecord card))
            {
                return;
            }

            Rect panel = new Rect(18, 145, 405, 725);
            Panel(panel, 0.97f);
            Color accent = CardColor(card.Code);
            Stroke(panel, accent, 3);
            Fill(new Rect(18, 145, 405, 62), accent);
            GUI.Label(
                new Rect(38, 153, 337, 48),
                card.Name.ToUpperInvariant(),
                cardNameStyle);
            if (GUI.Button(
                new Rect(378, 155, 34, 34),
                "X",
                buttonStyle))
            {
                selectedCode = 0;
                selectedController = byte.MaxValue;
                selectedLocation = 0;
                selectedSequence = -1;
                contextualChoices.Clear();
                return;
            }

            Texture2D texture = null;
            if (cardViews.TryGetTexture(card.Code, out texture))
            {
                GUI.DrawTexture(
                    new Rect(42, 225, 190, 266),
                    texture,
                    ScaleMode.ScaleAndCrop);
                Stroke(
                    new Rect(42, 225, 190, 266),
                    new Color(0.88f, 0.72f, 0.28f),
                    2);
            }
            GUI.Label(
                new Rect(248, 226, 148, 52),
                $"ID {card.Code:00000000}",
                tinyStyle);
            GUI.Label(
                new Rect(248, 292, 148, 92),
                $"ATK {card.Attack}\nDEF {card.Defense}\nNÍVEL {card.Level}",
                tinyStyle);
            if (GUI.Button(
                new Rect(248, 405, 148, 50),
                "AMPLIAR",
                buttonStyle))
            {
                zoomCode = card.Code;
            }
            Fill(
                new Rect(42, 512, 354, 42),
                accent);
            GUI.Label(
                new Rect(54, 519, 330, 28),
                "DESCRICAO / EFEITO",
                tinyStyle);
            GUIStyle readable = new GUIStyle(bodyStyle)
            {
                fontSize = 17,
                wordWrap = true
            };
            float textHeight = Mathf.Max(
                280f,
                readable.CalcHeight(
                    new GUIContent(card.Description),
                    326f));
            inspectorScroll = GUI.BeginScrollView(
                new Rect(42, 566, 354, 278),
                inspectorScroll,
                new Rect(0, 0, 326, textHeight));
            GUI.Label(
                new Rect(0, 0, 326, textHeight),
                card.Description,
                readable);
            GUI.EndScrollView();
        }

        private void DrawCardZoom()
        {
            if (zoomCode == 0 ||
                !database.TryGet(zoomCode, out CardRecord card))
            {
                return;
            }
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.018f, 0.86f));
            Rect modal = new Rect(260, 125, 1400, 830);
            Panel(modal, 0.99f);
            Stroke(modal, new Color(0.76f, 0.45f, 1f), 3);
            Texture2D texture = null;
            if (cardViews.TryGetTexture(card.Code, out texture))
            {
                Rect artRect = new Rect(350, 196, 410, 574);
                GUI.DrawTexture(artRect, texture, ScaleMode.ScaleAndCrop);
                Stroke(artRect, new Color(0.95f, 0.72f, 0.24f), 3);
            }
            GUI.Label(
                new Rect(825, 205, 720, 86),
                card.Name.ToUpperInvariant(),
                titleStyle);
            GUI.Label(
                new Rect(850, 322, 650, 48),
                $"CÓDIGO {card.Code:00000000}  ·  ATK {card.Attack}  ·  DEF {card.Defense}  ·  NÍVEL {card.Level}",
                subtitleStyle);
            Fill(
                new Rect(850, 390, 650, 2),
                new Color(0.18f, 0.78f, 0.90f, 0.72f));
            GUIStyle zoomText = new GUIStyle(bodyStyle)
            {
                fontSize = 18
            };
            GUI.Label(
                new Rect(850, 420, 650, 330),
                card.Description,
                zoomText);
            if (GUI.Button(
                new Rect(970, 814, 380, 64),
                "FECHAR DETALHES",
                buttonStyle))
            {
                zoomCode = 0;
            }
        }

        private void DrawVisualCue()
        {
            DuelVisualCue cue = animationQueue.Current;
            if (cue == null) return;
            float alpha = Mathf.Sin(animationQueue.Progress * Mathf.PI);
            if (cue.CardCode != 0 &&
                cardViews.TryGetTexture(
                    cue.CardCode,
                    out Texture2D cueTexture))
            {
                Fill(
                    new Rect(0, 0, DesignWidth, DesignHeight),
                    new Color(0.01f, 0.03f, 0.04f, 0.42f * alpha));
                Rect highlightedCard = new Rect(770, 238, 380, 532);
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.94f * alpha);
                GUI.DrawTexture(
                    highlightedCard,
                    cueTexture,
                    ScaleMode.ScaleAndCrop);
                GUI.color = oldColor;
                Stroke(
                    highlightedCard,
                    new Color(
                        cue.Color.r,
                        cue.Color.g,
                        cue.Color.b,
                        alpha),
                    6);
            }
            Rect banner = new Rect(600, 790, 720, 82);
            Fill(
                banner,
                new Color(
                    cue.Color.r * 0.10f,
                    cue.Color.g * 0.10f,
                    cue.Color.b * 0.10f,
                    0.92f * alpha));
            Stroke(
                banner,
                new Color(cue.Color.r, cue.Color.g, cue.Color.b, alpha),
                3);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(banner, cue.Text, titleStyle);
            GUI.color = previous;
        }

        private void DrawWinner()
        {
            if (!state.Winner.HasValue) return;
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.62f));
            Rect modal = new Rect(525, 330, 870, 390);
            Panel(modal, 0.99f);
            Color accent = state.Winner.Value == 0
                ? new Color(1f, 0.76f, 0.22f)
                : new Color(0.92f, 0.24f, 0.48f);
            Stroke(modal, accent, 3);
            GUI.Label(
                new Rect(575, 372, 770, 92),
                state.Winner.Value == 0 ? "VITÓRIA" : "DERROTA",
                titleStyle);
            GUI.Label(
                new Rect(595, 486, 730, 58),
                "O resultado foi determinado integralmente pelo ocgcore.",
                centeredStyle);
            if (GUI.Button(
                new Rect(610, 607, 310, 58),
                "NOVO DUELO",
                buttonStyle))
            {
                SceneManager.LoadScene(ProjectIdentity.DuelScene);
            }
            if (GUI.Button(
                new Rect(1000, 607, 310, 58),
                "VOLTAR AO PORTAL",
                buttonStyle))
            {
                SceneManager.LoadScene(ProjectIdentity.BootstrapScene);
            }
        }

        private string TutorialText(DuelPrompt prompt)
        {
            if (prompt == null)
            {
                return state.Phase == 0
                    ? "A partida está sendo preparada e as mãos iniciais serão compradas."
                    : $"Fase atual: {CoreMessageDecoder.PhaseName(state.Phase)}. Aguarde a próxima janela de decisão.";
            }
            return prompt.Message switch
            {
                CoreMessage.SelectIdleCommand =>
                    "Escolha o que fazer na Fase Principal. Somente ações legais aparecem abaixo.",
                CoreMessage.SelectBattleCommand =>
                    "Escolha um atacante ou avance. O Core valida alvos, dano e restrições.",
                CoreMessage.SelectChain =>
                    "Uma Corrente pode ser criada. Encadeie uma resposta ou passe a prioridade.",
                CoreMessage.SelectCard =>
                    "O efeito exige uma carta. As candidatas legais estão destacadas em violeta.",
                CoreMessage.SelectUnselectCard =>
                    "Marque ou desmarque as cartas legais e confirme a selecao.",
                CoreMessage.SelectSum =>
                    $"Selecione materiais cuja soma cumpra {prompt.RequiredSum}.",
                CoreMessage.SelectTribute =>
                    "Selecione os Tributos exigidos para concluir a Invocação.",
                CoreMessage.SelectPlace =>
                    "Escolha uma zona livre indicada pelo Core.",
                CoreMessage.SortCard or CoreMessage.SortChain =>
                    "Defina a ordem solicitada para a resolucao.",
                CoreMessage.AnnounceRace or
                    CoreMessage.AnnounceAttribute or
                    CoreMessage.AnnounceCard or
                    CoreMessage.AnnounceNumber =>
                    "Escolha o valor anunciado solicitado pelo efeito.",
                CoreMessage.SelectPosition =>
                    "Defina a posição de batalha permitida para esta carta.",
                _ =>
                    "Leia a solicitação e escolha uma das respostas validadas pelo Core."
            };
        }

        private string ChoiceLabel(DuelChoice choice)
        {
            if (choice.CardCode == 0 ||
                !database.TryGet(choice.CardCode, out CardRecord card))
            {
                return choice.Label;
            }
            string name = card.Name.Length > 25
                ? card.Name.Substring(0, 24) + "…"
                : card.Name;
            return $"{choice.Label}\n{name}";
        }

        private void Submit(DuelChoice choice)
        {
            if (engine == null || choice == null) return;
            DuelPrompt prompt = engine.CurrentPrompt;
            if (!ChoiceBelongsToCurrentPrompt(prompt, choice))
            {
                status =
                    "A decisao mudou; escolha novamente na interface atual.";
                DuelDevelopmentLog.Write(
                    DuelLogCategory.Error,
                    $"Rejected stale choice request={choice.RequestId}; " +
                    $"current={prompt?.RequestId ?? 0}; " +
                    $"choice={choice.Label}; code={choice.CardCode:00000000}",
                    this);
                return;
            }
            if (choice.CardCode != 0) selectedCode = choice.CardCode;
            string responseHex = BitConverter.ToString(
                choice.Response ?? Array.Empty<byte>());
            string legalLabels = string.Join(
                ", ",
                prompt.Choices.Select(candidate =>
                    candidate.ChoiceIndex + ":" + candidate.Label));
            DuelDevelopmentLog.Write(
                DuelLogCategory.Selection,
                $"request={prompt?.RequestId ?? 0}; " +
                $"choice={choice.ChoiceIndex}; label={choice.Label}; " +
                $"card={choice.CardCode:00000000}; " +
                $"controller={choice.Controller}; " +
                $"location={choice.Location:X2}; sequence={choice.Sequence}; " +
                $"response={responseHex}; legal=[{legalLabels}]",
                this);
            SubmitRaw(choice.Response);
        }

        public void SubmitChoice(DuelChoice choice)
        {
            if (networkReplica)
            {
                DuelOnlineBridge.SubmitReplicaChoice?.Invoke(choice);
                return;
            }
            Submit(choice);
        }

        /// <summary>
        /// Coloca esta arena no modo de apresentação remota. Neste modo
        /// nenhuma instância local do Core decide regras ou recebe dados
        /// secretos do adversário.
        /// </summary>
        public void ConfigureNetworkReplica(byte localPlayer)
        {
            networkReplica = true;
            networkLocalPlayer = localPlayer;
            remotePlayerOneAuthority = false;
            replicaPrompt = null;
            presentationDecisionLocked = false;
            if (engine != null)
            {
                engine.EventReceived -= OnCoreEvent;
                engine.Dispose();
                engine = null;
            }
            if (database != null)
            {
                state = new DuelPresentationState(database);
            }
            choicePresenter.Rebuild(null);
            contextualChoices.Clear();
            selectedPromptIndexes.Clear();
            status = "Conectado à autoridade da sala. Aguardando o duelo.";
        }

        /// <summary>
        /// O host conserva o Core, mas entrega todas as decisões do jogador
        /// 2 ao cliente remoto em vez de acionar a IA de demonstração.
        /// </summary>
        public void ConfigureRemotePlayerOneAuthority(bool enabled)
        {
            remotePlayerOneAuthority = enabled;
        }

        public void ApplyNetworkState(IDuelNetworkState networkState)
        {
            if (!networkReplica || networkState == null || database == null)
                return;

            try
            {
                networkState.ApplyTo(state, database, out replicaPrompt);
                choicePresenter.Rebuild(replicaPrompt);
                contextualChoices.Clear();
                selectedPromptIndexes.Clear();
                showPhaseChoices = false;
                status = string.IsNullOrWhiteSpace(networkState.Status)
                    ? "Duelo online sincronizado."
                    : networkState.Status;
                // The authored arena caches the presentation-state reference.
                // Notify it after replacing a replica snapshot so it rebinds
                // without fabricating a Core event on this assembly boundary.
                PresentationStateChanged?.Invoke();
            }
            catch (Exception exception)
            {
                string failure = exception.GetBaseException().Message;
                status = $"Falha de sincronização online: {failure}";
                CoreFailure?.Invoke(failure);
                Debug.LogException(exception);
            }
        }

        public void SetPresentationDecisionLocked(bool locked)
        {
            presentationDecisionLocked = locked;
            if (!locked)
                nextAutoDecision = Time.unscaledTime + 0.12f;
        }

        public void SubmitCoreResponse(
            byte[] response,
            ulong requestId = 0)
        {
            if (networkReplica)
            {
                DuelOnlineBridge.SubmitReplicaResponse?.Invoke(
                    response,
                    requestId == 0 ? replicaPrompt?.RequestId ?? 0 : requestId);
                return;
            }
            DuelPrompt prompt = engine?.CurrentPrompt;
            if (requestId != 0 &&
                prompt != null &&
                prompt.RequestId != 0 &&
                requestId != prompt.RequestId)
            {
                DuelDevelopmentLog.Write(
                    DuelLogCategory.Error,
                    $"Rejected stale raw response request={requestId}; " +
                    $"current={prompt.RequestId}.",
                    this);
                return;
            }
            SubmitRaw(response);
        }

        private static bool ChoiceBelongsToCurrentPrompt(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            return CoreCardActionBinding.BelongsToRequest(
                prompt,
                choice);
        }

        private static string FormatLocation(CardLocation location)
        {
            return location == null
                ? "-"
                : $"P{location.Controller}/L{location.Location:X2}/" +
                  $"S{location.Sequence}/P{location.Position:X2}";
        }

        public void RestartExternalDuel(
            uint[] playerMain,
            uint[] playerExtra,
            uint[] opponentMain,
            uint[] opponentExtra)
        {
            presentationDecisionLocked = false;
            if (!externalPresentation || database == null)
                return;
            if (playerMain == null || playerMain.Length < 40)
                throw new ArgumentException(
                    "O Deck Principal do jogador precisa ter ao menos 40 cartas.",
                    nameof(playerMain));
            if (opponentMain == null || opponentMain.Length < 40)
                throw new ArgumentException(
                    "O Deck Principal do oponente precisa ter ao menos 40 cartas.",
                    nameof(opponentMain));

            if (engine != null)
            {
                engine.EventReceived -= OnCoreEvent;
                engine.Dispose();
            }

            state = new DuelPresentationState(database);
            playerExtraCards.Clear();
            if (playerExtra != null)
                playerExtraCards.AddRange(playerExtra);
            state.ConfigureDeckCounts(
                playerMain.Length,
                playerExtra?.Length ?? 0,
                opponentMain.Length,
                opponentExtra?.Length ?? 0);

            DuelConfiguration configuration =
                DuelConfiguration.VerticalSlice(DuelConfiguration.FreshSeed());
            configuration.PlayerDeck = playerMain.ToArray();
            configuration.PlayerExtraDeck =
                playerExtra?.ToArray() ?? Array.Empty<uint>();
            configuration.OpponentDeck = opponentMain.ToArray();
            configuration.OpponentExtraDeck =
                opponentExtra?.ToArray() ?? Array.Empty<uint>();
            configuration.SimpleOpponentAi = false;
            tacticalOpponent.Reset();
            engine = OcgDuelEngine.CreateDefault(configuration);
            engine.EventReceived += OnCoreEvent;
            choicePresenter.Rebuild(null);
            contextualChoices.Clear();
            selectedPromptIndexes.Clear();
            nextAutoDecision = Time.unscaledTime + 0.22f;
            engine.Start();
            status =
                "Duelo reiniciado com os decks selecionados na interface antiga.";
            Debug.Log(
                $"[Arcane legacy bridge] Core restarted: " +
                $"playerMain={playerMain.Length}, playerExtra={playerExtra?.Length ?? 0}, " +
                $"opponentMain={opponentMain.Length}, opponentExtra={opponentExtra?.Length ?? 0}, "+
                $"seed={configuration.Seed:X16}.");
        }

        private void SubmitRaw(byte[] response)
        {
            if (engine == null || response == null) return;
            if (presentationDecisionLocked)
            {
                status = "Aguarde a apresentação da carta terminar.";
                return;
            }
            try
            {
                state.ClearPrompt();
                choicePresenter.Rebuild(null);
                contextualChoices.Clear();
                selectedPromptIndexes.Clear();
                showPhaseChoices = false;
                engine.SubmitResponse(response);
                status = engine.IsFinished
                    ? "Duelo concluído pelo Core"
                    : "Ação confirmada · aguardando a próxima decisão";
            }
            catch (Exception exception)
            {
                string failure = exception.GetBaseException().Message;
                status =
                    $"O duelo foi interrompido pelo Core: {failure}";
                CoreFailure?.Invoke(failure);
                Debug.LogException(exception);
            }
        }

        private void DrawCard(
            Rect rect,
            uint code,
            bool opponent,
            bool compact,
            byte controller = byte.MaxValue,
            byte location = 0,
            int sequence = -1)
        {
            Texture2D texture = null;
            bool hasArt = cardViews != null &&
                          cardViews.TryGetTexture(code, out texture);
            if (hasArt)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop);
            }
            else
            {
                Fill(rect, CardColor(code));
                if (database.TryGet(code, out CardRecord fallback))
                {
                    GUI.Label(
                        new Rect(
                            rect.x + 5,
                            rect.y + 6,
                            rect.width - 10,
                            compact ? 50 : 65),
                        fallback.Name,
                        compact ? tinyStyle : bodyStyle);
                }
            }

            bool candidate = IsCardCandidate(
                code,
                controller,
                location,
                sequence);
            bool hovered = rect.Contains(Event.current.mousePosition);
            bool selected =
                selectedCode == code &&
                (selectedController == byte.MaxValue ||
                 (selectedController == controller &&
                  selectedLocation == location &&
                  selectedSequence == sequence));
            Color border = selected
                ? new Color(1f, 0.78f, 0.18f)
                : candidate
                    ? new Color(0.68f, 1f, 0.04f)
                    : hovered
                        ? new Color(1f, 0.78f, 0.18f)
                    : opponent
                        ? new Color(0.90f, 0.32f, 0.58f, 0.82f)
                        : new Color(0.26f, 0.90f, 1f, 0.82f);
            Stroke(
                rect,
                border,
                selected || candidate || hovered ? 3 : 1);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                HandleCardClick(
                    code,
                    controller,
                    location,
                    sequence);
            }
        }

        private void HandleCardClick(
            uint code,
            byte controller,
            byte location,
            int sequence)
        {
            selectedCode = code;
            selectedController = controller;
            selectedLocation = location;
            selectedSequence = sequence;
            contextualChoices.Clear();
            DuelPrompt prompt = engine?.CurrentPrompt;
            if (prompt == null)
            {
                status = database.TryGet(code, out CardRecord inspected)
                    ? $"{inspected.Name} selecionada · clique em AMPLIAR para ler."
                    : $"Carta {code:00000000} selecionada.";
                return;
            }

            IEnumerable<DuelChoice> candidates = prompt.Choices.Where(
                choice => choice.CardCode == code);
            if (controller != byte.MaxValue && location != 0 && sequence >= 0)
            {
                DuelChoice[] exact = candidates
                    .Where(choice =>
                        !choice.HasLocation ||
                        (choice.Controller == controller &&
                         choice.Location == location &&
                         choice.Sequence == (uint)sequence))
                    .ToArray();
                if (exact.Length > 0) candidates = exact;
            }
            contextualChoices.AddRange(candidates);

            if (prompt.Message == CoreMessage.SelectUnselectCard &&
                contextualChoices.Count == 1)
            {
                Submit(contextualChoices[0]);
                return;
            }

            if ((prompt.Message == CoreMessage.SelectCard ||
                 prompt.Message == CoreMessage.SelectTribute ||
                 prompt.Message == CoreMessage.SelectSum) &&
                contextualChoices.Count == 1)
            {
                DuelChoice selected = contextualChoices[0];
                if (prompt.MaximumSelections > 1 ||
                    prompt.MinimumSelections > 1)
                {
                    TogglePromptSelection(selected);
                }
                else
                {
                    Submit(selected);
                }
                return;
            }

            if (contextualChoices.Count == 0)
            {
                status =
                    "Carta selecionada para inspeção. Nenhuma ação legal está disponível para ela nesta janela.";
            }
            else
            {
                status =
                    $"{contextualChoices.Count} ação(ões) legal(is) para a carta selecionada.";
            }
        }

        private void TrackPlayerExtraDeck(DuelEvent duelEvent)
        {
            if (duelEvent.Message != CoreMessage.Move || duelEvent.Code == 0)
            {
                return;
            }
            if (duelEvent.Previous != null &&
                duelEvent.Previous.Controller == 0 &&
                duelEvent.Previous.Location == DuelLocation.Extra)
            {
                int sequence = (int)duelEvent.Previous.Sequence;
                if (sequence >= 0 && sequence < playerExtraCards.Count)
                {
                    playerExtraCards.RemoveAt(sequence);
                }
                else
                {
                    playerExtraCards.Remove(duelEvent.Code);
                }
            }
            if (duelEvent.Current != null &&
                duelEvent.Current.Controller == 0 &&
                duelEvent.Current.Location == DuelLocation.Extra)
            {
                int sequence = Mathf.Clamp(
                    (int)duelEvent.Current.Sequence,
                    0,
                    playerExtraCards.Count);
                playerExtraCards.Insert(sequence, duelEvent.Code);
            }
        }

        private bool IsCardCandidate(
            uint code,
            byte controller,
            byte location,
            int sequence)
        {
            DuelPrompt prompt = engine?.CurrentPrompt;
            if (prompt == null) return false;
            foreach (DuelChoice choice in prompt.Choices)
            {
                if (choice.CardCode != code) continue;
                if (!choice.HasLocation ||
                    controller == byte.MaxValue ||
                    (choice.Controller == controller &&
                     choice.Location == location &&
                     choice.Sequence == (uint)Mathf.Max(0, sequence)))
                {
                    return true;
                }
            }
            return false;
        }

        private DuelChoice FindLocationChoice(
            byte controller,
            byte location,
            uint sequence)
        {
            DuelPrompt prompt = engine?.CurrentPrompt;
            return prompt?.Choices.FirstOrDefault(
                choice =>
                    choice.HasLocation &&
                    choice.Controller == controller &&
                    choice.Location == location &&
                    choice.Sequence == sequence &&
                    (prompt.Message == CoreMessage.SelectPlace ||
                     choice.CardCode == 0));
        }

        private void DrawContextActions()
        {
            if (contextualChoices.Count == 0 ||
                zoneBrowserCards.Count > 0 ||
                zoomCode != 0 ||
                IsCardSelectionPrompt(engine?.CurrentPrompt))
            {
                return;
            }

            int visible = Mathf.Min(contextualChoices.Count, 5);
            const float width = 164f;
            const float spacing = 176f;
            float start = 960f -
                          ((visible - 1) * spacing + width) * 0.5f;
            for (int index = 0; index < visible; index++)
            {
                DuelChoice choice = contextualChoices[index];
                Rect action = new Rect(
                    start + index * spacing,
                    724,
                    width,
                    62f);
                if (GUI.Button(
                    action,
                    ShortActionLabel(choice.Label).Replace("\n", " "),
                    buttonStyle))
                {
                    Submit(choice);
                    return;
                }
            }
        }

        private void DrawSelectionTray()
        {
            DuelPrompt prompt = engine?.CurrentPrompt;
            if (!IsCardSelectionPrompt(prompt) ||
                zoneBrowserCards.Count > 0 ||
                zoomCode != 0)
            {
                return;
            }
            List<DuelChoice> candidates = prompt.Choices
                .Where(choice =>
                    choice.CardCode != 0 &&
                    choice.ChoiceIndex >= 0)
                .ToList();
            if (candidates.Count == 0) return;

            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.36f));
            Rect tray = new Rect(400, 590, 1120, 445);
            Panel(tray, 0.985f);
            Stroke(tray, new Color(0.16f, 0.88f, 1f), 2);
            GUI.Label(
                new Rect(440, 612, 1040, 45),
                prompt.Message == CoreMessage.SelectTribute
                    ? "SELECIONE OS TRIBUTOS DESTACADOS"
                    : prompt.Message == CoreMessage.SelectSum
                        ? $"SELECIONE MATERIAIS · SOMA {prompt.RequiredSum}"
                    : "SELECIONE A CARTA SOLICITADA PELO EFEITO",
                subtitleStyle);
            GUI.Label(
                new Rect(440, 652, 760, 30),
                $"ESCOLHAS {prompt.MinimumSelections}–{prompt.MaximumSelections} · " +
                $"{selectedPromptIndexes.Count} SELECIONADA(S)",
                tinyStyle);

            Rect viewport = new Rect(430, 692, 1060, 250);
            float contentWidth = Mathf.Max(
                viewport.width - 8,
                candidates.Count * 164f);
            selectionTrayScroll = GUI.BeginScrollView(
                viewport,
                selectionTrayScroll,
                new Rect(0, 0, contentWidth, 225));
            for (int index = 0; index < candidates.Count; index++)
            {
                DuelChoice choice = candidates[index];
                Rect cardRect = new Rect(index * 164f + 10, 8, 132, 186);
                DrawCard(
                    cardRect,
                    choice.CardCode,
                    choice.Controller != 0,
                    false,
                    choice.Controller,
                    choice.Location,
                    (int)choice.Sequence);
                if (selectedPromptIndexes.Contains(choice.ChoiceIndex))
                {
                    Fill(
                        new Rect(cardRect.x + 90, cardRect.y + 8, 34, 34),
                        new Color(0.68f, 1f, 0.04f));
                    GUI.Label(
                        new Rect(cardRect.x + 90, cardRect.y + 8, 34, 34),
                        "✓",
                        centeredStyle);
                    Stroke(cardRect, new Color(0.68f, 1f, 0.04f), 5);
                }
            }
            GUI.EndScrollView();

            bool canConfirm = CoreMessageDecoder.IsValidSelection(
                prompt,
                selectedPromptIndexes);
            GUI.enabled = canConfirm;
            if (GUI.Button(
                new Rect(690, 962, 300, 54),
                "CONFIRMAR SELEÇÃO",
                buttonStyle))
            {
                SubmitRaw(CoreMessageDecoder.CardSelectionResponse(
                    selectedPromptIndexes
                        .OrderBy(index => index)
                        .Select(index => (uint)index)
                        .ToArray()));
            }
            GUI.enabled = true;
            DuelChoice cancel = prompt.Choices.FirstOrDefault(
                choice =>
                    choice.CardCode == 0 &&
                    choice.Label.IndexOf(
                        "Cancelar",
                        StringComparison.OrdinalIgnoreCase) >= 0);
            GUI.enabled = cancel != null;
            if (GUI.Button(
                new Rect(1030, 962, 250, 54),
                "CANCELAR",
                buttonStyle))
            {
                Submit(cancel);
            }
            GUI.enabled = true;
        }

        private static bool IsCardSelectionPrompt(DuelPrompt prompt)
        {
            return prompt != null &&
                   (prompt.Message == CoreMessage.SelectCard ||
                    prompt.Message == CoreMessage.SelectTribute ||
                    prompt.Message == CoreMessage.SelectSum);
        }

        private void TogglePromptSelection(DuelChoice choice)
        {
            DuelPrompt prompt = engine?.CurrentPrompt;
            if (prompt == null || choice == null || choice.ChoiceIndex < 0)
            {
                return;
            }
            if (!selectedPromptIndexes.Add(choice.ChoiceIndex))
            {
                selectedPromptIndexes.Remove(choice.ChoiceIndex);
            }
            while (selectedPromptIndexes.Count > prompt.MaximumSelections)
            {
                int first = selectedPromptIndexes.First();
                selectedPromptIndexes.Remove(first);
            }
            status =
                $"{selectedPromptIndexes.Count}/{prompt.MaximumSelections} carta(s) selecionada(s).";
        }

        private void DrawGlobalChoices()
        {
            DuelPrompt prompt = engine?.CurrentPrompt;
            if (prompt == null ||
                zoneBrowserCards.Count > 0 ||
                zoomCode != 0)
            {
                return;
            }

            bool modal =
                prompt.Message == CoreMessage.SelectEffectYesNo ||
                prompt.Message == CoreMessage.SelectYesNo ||
                prompt.Message == CoreMessage.SelectOption ||
                prompt.Message == CoreMessage.SelectPosition ||
                prompt.Message == CoreMessage.SortCard ||
                prompt.Message == CoreMessage.SortChain ||
                prompt.Message == CoreMessage.AnnounceRace ||
                prompt.Message == CoreMessage.AnnounceAttribute ||
                prompt.Message == CoreMessage.AnnounceCard ||
                prompt.Message == CoreMessage.AnnounceNumber;
            List<DuelChoice> choices = modal
                ? prompt.Choices.ToList()
                : prompt.Choices
                    .Where(choice =>
                        choice.CardCode == 0 &&
                        !choice.HasLocation)
                    .ToList();
            if (choices.Count == 0) return;

            List<DuelChoice> phaseChoices = choices
                .Where(IsPhaseChoice)
                .ToList();
            if (phaseChoices.Count > 0)
            {
                if (!showPhaseChoices) return;
                DrawPhaseSelection(phaseChoices);
                return;
            }

            if (modal)
            {
                bool grid =
                    prompt.Message == CoreMessage.SortCard ||
                    prompt.Message == CoreMessage.SortChain ||
                    prompt.Message == CoreMessage.AnnounceRace ||
                    prompt.Message == CoreMessage.AnnounceAttribute ||
                    prompt.Message == CoreMessage.AnnounceCard ||
                    prompt.Message == CoreMessage.AnnounceNumber;
                if (grid)
                {
                    DrawChoiceGrid(prompt, choices);
                    return;
                }
                Fill(
                    new Rect(0, 0, DesignWidth, DesignHeight),
                    new Color(0f, 0f, 0.02f, 0.52f));
                Rect modalRect = new Rect(610, 355, 700, 300);
                Panel(modalRect, 0.99f);
                Stroke(modalRect, new Color(0.68f, 1f, 0.04f), 3);
                GUI.Label(
                    new Rect(660, 395, 600, 58),
                    prompt.Title.ToUpperInvariant(),
                    titleStyle);
                float width = Mathf.Min(240f, 600f / choices.Count - 12f);
                float total = choices.Count * (width + 12f) - 12f;
                for (int index = 0; index < choices.Count; index++)
                {
                    DuelChoice choice = choices[index];
                    if (GUI.Button(
                        new Rect(
                            960f - total * 0.5f + index * (width + 12f),
                            510,
                            width,
                            72),
                        ChoiceLabel(choice),
                        buttonStyle))
                    {
                        Submit(choice);
                        return;
                    }
                }
                return;
            }

            int visible = Mathf.Min(choices.Count, 4);
            float widthEach = 190f;
            float totalWidth = visible * (widthEach + 10f);
            float startX = 960f - totalWidth * 0.5f;
            for (int index = 0; index < visible; index++)
            {
                DuelChoice choice = choices[index];
                if (GUI.Button(
                    new Rect(
                        startX + index * (widthEach + 10f),
                        contextualChoices.Count > 0 ? 685 : 752,
                        widthEach,
                        54),
                    choice.Label.ToUpperInvariant(),
                    buttonStyle))
                {
                    Submit(choice);
                    return;
                }
            }
        }

        private void DrawChoiceGrid(
            DuelPrompt prompt,
            List<DuelChoice> choices)
        {
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.68f));
            Rect panel = new Rect(350, 170, 1220, 740);
            Panel(panel, 0.995f);
            Stroke(panel, new Color(0.68f, 1f, 0.04f), 3);
            GUI.Label(
                new Rect(410, 210, 1100, 58),
                prompt.Title.ToUpperInvariant(),
                titleStyle);

            const int columns = 4;
            const float width = 250f;
            const float height = 62f;
            const float horizontalGap = 22f;
            const float verticalGap = 18f;
            float startX =
                960f - (columns * width + (columns - 1) * horizontalGap) * 0.5f;
            for (int index = 0; index < choices.Count; index++)
            {
                int column = index % columns;
                int row = index / columns;
                Rect button = new Rect(
                    startX + column * (width + horizontalGap),
                    310 + row * (height + verticalGap),
                    width,
                    height);
                if (button.yMax > panel.yMax - 30) break;
                if (GUI.Button(
                    button,
                    ChoiceLabel(choices[index]).ToUpperInvariant(),
                    buttonStyle))
                {
                    Submit(choices[index]);
                    return;
                }
            }
        }

        private void DrawPhaseSelection(List<DuelChoice> choices)
        {
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.62f));
            Rect panel = new Rect(360, 305, 1200, 470);
            Panel(panel, 0.995f);
            Stroke(panel, new Color(0.16f, 0.88f, 1f), 3);
            GUI.Label(
                new Rect(470, 345, 980, 55),
                "SELECIONE UMA FASE PARA AVANÇAR",
                titleStyle);
            (string Label, uint Value)[] phases =
            {
                ("DRAW", 0x01),
                ("STANDBY", 0x02),
                ("MAIN 1", 0x04),
                ("BATTLE", 0x80),
                ("MAIN 2", 0x100),
                ("END", 0x200)
            };
            const float diameter = 132f;
            const float spacing = 160f;
            float start =
                960f - ((phases.Length - 1) * spacing + diameter) * 0.5f;
            for (int index = 0; index < phases.Length - 1; index++)
            {
                Fill(
                    new Rect(
                        start + diameter + index * spacing,
                        518,
                        spacing - diameter,
                        3),
                    new Color(0.18f, 0.52f, 0.62f, 0.72f));
            }
            for (int index = 0; index < phases.Length; index++)
            {
                bool current = IsCurrentPhase(phases[index].Value);
                DuelChoice choice = choices.FirstOrDefault(
                    candidate =>
                        string.Equals(
                            PhaseChoiceLabel(candidate.Label),
                            phases[index].Label,
                            StringComparison.OrdinalIgnoreCase));
                bool available = choice != null;
                Rect disc = new Rect(
                    start + index * spacing,
                    454,
                    diameter,
                    diameter);
                Color oldColor = GUI.color;
                GUI.color = available
                    ? Color.white
                    : current
                        ? new Color(0.50f, 0.96f, 1f, 0.94f)
                        : new Color(0.28f, 0.38f, 0.44f, 0.54f);
                GUI.DrawTexture(disc, phaseDisc, ScaleMode.StretchToFill);
                GUI.color = oldColor;
                GUI.Label(
                    new Rect(
                        disc.x + 10,
                        disc.y + 37,
                        disc.width - 20,
                        70),
                    current
                        ? $"{phases[index].Label}\nATUAL"
                        : phases[index].Label,
                    centeredStyle);
                if (available &&
                    GUI.Button(disc, GUIContent.none, GUIStyle.none))
                {
                    Submit(choice);
                    return;
                }
            }
            if (GUI.Button(
                new Rect(785, 670, 350, 58),
                "CANCELAR",
                buttonStyle))
            {
                showPhaseChoices = false;
            }
        }

        private bool IsCurrentPhase(uint phase)
        {
            if (phase == 0x80)
            {
                return state.Phase >= 0x08 && state.Phase <= 0x80;
            }
            return state.Phase == phase;
        }

        private static bool IsPhaseChoice(DuelChoice choice)
        {
            if (choice == null || string.IsNullOrWhiteSpace(choice.Label))
            {
                return false;
            }
            return choice.Label.IndexOf(
                       "Fase",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   choice.Label.IndexOf(
                       "Encerrar turno",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string PhaseChoiceLabel(string label)
        {
            if (label.IndexOf("Batalha", StringComparison.OrdinalIgnoreCase) >= 0)
                return "BATTLE";
            if (label.IndexOf("Principal 2", StringComparison.OrdinalIgnoreCase) >= 0)
                return "MAIN 2";
            if (label.IndexOf("Encerrar", StringComparison.OrdinalIgnoreCase) >= 0)
                return "END";
            return label.ToUpperInvariant();
        }

        private static string ShortActionLabel(string label)
        {
            if (label.IndexOf("especial", StringComparison.OrdinalIgnoreCase) >= 0)
                return "INVOCAR\nESPECIAL";
            if (label.IndexOf("Invocar", StringComparison.OrdinalIgnoreCase) >= 0)
                return "INVOCAR";
            if (label.IndexOf("Baixar", StringComparison.OrdinalIgnoreCase) >= 0)
                return "BAIXAR";
            if (label.IndexOf("Ativar", StringComparison.OrdinalIgnoreCase) >= 0)
                return "ATIVAR";
            if (label.IndexOf("Atacar", StringComparison.OrdinalIgnoreCase) >= 0)
                return "ATACAR";
            if (label.IndexOf("posição", StringComparison.OrdinalIgnoreCase) >= 0)
                return "POSIÇÃO";
            return label.ToUpperInvariant();
        }

        private string InteractionInstruction(DuelPrompt prompt)
        {
            return prompt.Message switch
            {
                CoreMessage.SelectIdleCommand =>
                    "Clique em uma carta iluminada da mão, do campo ou do Extra Deck.",
                CoreMessage.SelectBattleCommand =>
                    "Clique no seu monstro iluminado e escolha ATACAR.",
                CoreMessage.SelectPlace =>
                    "Clique diretamente em uma zona verde do campo.",
                CoreMessage.SelectCard =>
                    "Clique diretamente em uma carta com contorno verde.",
                CoreMessage.SelectUnselectCard =>
                    "Clique para marcar ou desmarcar e confirme a selecao.",
                CoreMessage.SelectSum =>
                    $"Selecione materiais que cumpram a soma {prompt.RequiredSum}.",
                CoreMessage.SelectTribute =>
                    "Clique no monstro destacado que será usado como Tributo.",
                CoreMessage.SelectChain =>
                    "Clique na carta de resposta ou escolha NÃO RESPONDER.",
                CoreMessage.SelectPosition =>
                    "Escolha a posição no painel central.",
                CoreMessage.SortCard or CoreMessage.SortChain =>
                    "Escolha a ordem das cartas no painel central.",
                CoreMessage.AnnounceRace or
                    CoreMessage.AnnounceAttribute or
                    CoreMessage.AnnounceCard or
                    CoreMessage.AnnounceNumber =>
                    "Escolha o valor solicitado pelo efeito.",
                _ =>
                    "Escolha uma resposta exibida no centro da arena."
            };
        }

        private void OpenZoneBrowser(
            string title,
            IEnumerable<uint> cards,
            byte controller,
            byte location)
        {
            zoneBrowserCards.Clear();
            if (cards != null) zoneBrowserCards.AddRange(cards);
            zoneBrowserTitle = title;
            zoneBrowserController = controller;
            zoneBrowserLocation = location;
            zoneBrowserScroll = Vector2.zero;
            if (zoneBrowserCards.Count == 0)
            {
                status = $"{title}: nenhuma carta.";
            }
        }

        private void DrawZoneBrowser()
        {
            if (zoneBrowserCards.Count == 0) return;
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.78f));
            Rect modal = new Rect(170, 120, 1580, 820);
            Panel(modal, 0.995f);
            Stroke(modal, new Color(0.16f, 0.88f, 1f), 3);
            GUI.Label(
                new Rect(225, 155, 1150, 58),
                $"{zoneBrowserTitle} · {zoneBrowserCards.Count} CARTA(S)",
                titleStyle);
            if (GUI.Button(
                new Rect(1470, 158, 220, 52),
                "FECHAR",
                buttonStyle))
            {
                zoneBrowserCards.Clear();
                return;
            }

            Rect viewport = new Rect(220, 235, 1480, 640);
            int columns = 8;
            int rows = Mathf.CeilToInt(zoneBrowserCards.Count / (float)columns);
            zoneBrowserScroll = GUI.BeginScrollView(
                viewport,
                zoneBrowserScroll,
                new Rect(0, 0, 1445, Mathf.Max(620, rows * 270)));
            for (int index = 0; index < zoneBrowserCards.Count; index++)
            {
                int column = index % columns;
                int row = index / columns;
                uint code = zoneBrowserCards[index];
                DrawCard(
                    new Rect(column * 180 + 18, row * 270 + 12, 148, 208),
                    code,
                    zoneBrowserController != 0,
                    false,
                    zoneBrowserController,
                    zoneBrowserLocation,
                    index);
                if (database.TryGet(code, out CardRecord card))
                {
                    GUI.Label(
                        new Rect(column * 180 + 10, row * 270 + 224, 164, 40),
                        card.Name,
                        centeredStyle);
                }
            }
            GUI.EndScrollView();
        }

        private Color CardColor(uint code)
        {
            if (!database.TryGet(code, out CardRecord card))
                return new Color(0.18f, 0.20f, 0.25f);
            if ((card.Type & 0x2) != 0)
                return new Color(0.05f, 0.48f, 0.43f);
            if ((card.Type & 0x4) != 0)
                return new Color(0.62f, 0.18f, 0.45f);
            if ((card.Type & 0x40) != 0)
                return new Color(0.43f, 0.25f, 0.68f);
            if ((card.Type & 0x2000) != 0)
                return new Color(0.76f, 0.78f, 0.80f);
            if ((card.Type & 0x800000) != 0)
                return new Color(0.08f, 0.09f, 0.12f);
            return new Color(0.78f, 0.64f, 0.34f);
        }

        private void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.006f, 0.014f, 0.018f);
            camera.orthographic = false;
            camera.fieldOfView = 43f;
            camera.transform.position = new Vector3(0f, 16.2f, -15.2f);
            camera.transform.rotation = Quaternion.Euler(46.5f, 0f, 0f);
        }

        private void LoadArenaBackground()
        {
            string path = YgoContentLocator.Resolve(
                "UI",
                "duel_arena_v2.png");
            if (!File.Exists(path)) return;
            arenaBackground =
                new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "Arcane Duel Arena V2",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            if (!arenaBackground.LoadImage(File.ReadAllBytes(path)))
            {
                Destroy(arenaBackground);
                arenaBackground = null;
            }
        }

        private void InitializeTextures()
        {
            white = SolidTexture(Color.white);
            cardBack = BuildCardBack();
            buttonNormal =
                SolidTexture(new Color(0.025f, 0.10f, 0.17f, 0.98f));
            buttonHover =
                SolidTexture(new Color(0.05f, 0.35f, 0.44f, 1f));
            buttonActive =
                SolidTexture(new Color(0.42f, 0.20f, 0.52f, 1f));
            phaseDisc = BuildDisc(
                new Color(0.025f, 0.42f, 0.76f),
                new Color(1f, 0.83f, 0.12f));
            actionDisc = BuildDisc(
                new Color(0.015f, 0.48f, 0.66f),
                new Color(0.62f, 1f, 0.08f));
        }

        private static Texture2D BuildDisc(Color inner, Color ring)
        {
            const int size = 192;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "Arcane Action Disc",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new Vector2(
                        (x + 0.5f) / size - 0.5f,
                        (y + 0.5f) / size - 0.5f);
                    float radius = uv.magnitude * 2f;
                    if (radius > 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }
                    float innerRim =
                        1f -
                        Mathf.Clamp01(
                            Mathf.Abs(radius - 0.70f) / 0.12f);
                    float outerRim =
                        Mathf.SmoothStep(0.79f, 0.96f, radius);
                    float ringAmount = Mathf.Max(
                        innerRim * 0.62f,
                        outerRim);
                    Color brightInner = new Color(
                        Mathf.Clamp01(inner.r * 1.65f + 0.04f),
                        Mathf.Clamp01(inner.g * 1.65f + 0.04f),
                        Mathf.Clamp01(inner.b * 1.65f + 0.04f),
                        1f);
                    Color color = Color.Lerp(
                        brightInner,
                        ring,
                        ringAmount);
                    color.a = radius < 0.92f
                        ? 0.98f
                        : 1f - Mathf.SmoothStep(0.92f, 1f, radius);
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D BuildCardBack()
        {
            const int width = 128;
            const int height = 180;
            var texture =
                new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Arcane Card Back",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 uv =
                    new Vector2(
                        (x + 0.5f) / width,
                        (y + 0.5f) / height);
                Vector2 point =
                    new Vector2((uv.x - 0.5f) * 1.4f, uv.y - 0.5f);
                float radius = point.magnitude;
                float angle = Mathf.Atan2(point.y, point.x);
                float spiral = Mathf.Pow(
                    Mathf.Clamp01(
                        1f -
                        Mathf.Abs(
                            Mathf.Sin(angle * 2.4f + radius * 27f))),
                    6f);
                bool border =
                    uv.x < 0.045f ||
                    uv.x > 0.955f ||
                    uv.y < 0.032f ||
                    uv.y > 0.968f;
                pixels[y * width + x] = border
                    ? new Color(0.80f, 0.55f, 0.17f)
                    : Color.Lerp(
                        new Color(0.008f, 0.006f, 0.016f),
                        new Color(0.30f, 0.06f, 0.44f),
                        spiral);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = Style(
                42,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            phaseStyle = Style(
                22,
                FontStyle.Bold,
                new Color(0.58f, 0.94f, 1f),
                TextAnchor.MiddleCenter);
            subtitleStyle = Style(
                16,
                FontStyle.Bold,
                new Color(0.56f, 0.92f, 1f),
                TextAnchor.MiddleLeft);
            bodyStyle = Style(
                14,
                FontStyle.Normal,
                new Color(0.86f, 0.93f, 0.97f),
                TextAnchor.UpperLeft);
            bodyStyle.wordWrap = true;
            tinyStyle = Style(
                11,
                FontStyle.Bold,
                new Color(0.64f, 0.78f, 0.85f),
                TextAnchor.UpperLeft);
            tinyStyle.wordWrap = true;
            centeredStyle = Style(
                18,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            lifeStyle = Style(
                27,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleLeft);
            cardNameStyle = Style(
                13,
                FontStyle.Bold,
                Color.white,
                TextAnchor.UpperLeft);
            cardNameStyle.wordWrap = true;
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(8, 8, 5, 5),
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
            buttonStyle.normal.background = buttonNormal;
            buttonStyle.hover.background = buttonHover;
            buttonStyle.active.background = buttonActive;
        }

        private static GUIStyle Style(
            int size,
            FontStyle fontStyle,
            Color color,
            TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                normal = { textColor = color }
            };
        }

        private static Texture2D SolidTexture(Color color)
        {
            var texture =
                new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void Panel(Rect rect, float alpha)
        {
            Fill(
                rect,
                new Color(0.008f, 0.025f, 0.055f, alpha));
            Stroke(
                rect,
                new Color(0.16f, 0.60f, 0.70f, 0.66f),
                1);
        }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }

        private void Stroke(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(
                new Rect(
                    rect.x,
                    rect.yMax - thickness,
                    rect.width,
                    thickness),
                color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(
                new Rect(
                    rect.xMax - thickness,
                    rect.y,
                    thickness,
                    rect.height),
                color);
        }

        private void TryScheduleCommandLineCapture()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string captureState = string.Empty;
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(
                    arguments[index],
                    "-arcaneCaptureState",
                    StringComparison.OrdinalIgnoreCase))
                {
                    captureState = arguments[index + 1];
                    break;
                }
            }
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(
                    arguments[index],
                    "-arcaneCapture",
                    StringComparison.OrdinalIgnoreCase))
                {
                    StartCoroutine(
                        CaptureAndExit(
                            arguments[index + 1],
                            captureState));
                    return;
                }
            }
        }

        private IEnumerator CaptureAndExit(string path, string captureState)
        {
            yield return new WaitForSecondsRealtime(1.1f);
            if (externalPresentation)
            {
                Component presentation =
                    GetComponent("CardArenaBootstrap");
                presentation?.GetType()
                    .GetMethod(
                        "PrepareCaptureState",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public)
                    ?.Invoke(
                        presentation,
                        new object[] { captureState });
                yield return new WaitForSecondsRealtime(0.4f);
                ScreenCapture.CaptureScreenshot(path, 1);
                yield return new WaitForSecondsRealtime(1.2f);
                Application.Quit(0);
                yield break;
            }

            DuelPrompt prompt = engine?.CurrentPrompt;
            bool captureAction =
                string.Equals(
                    captureState,
                    "action",
                    StringComparison.OrdinalIgnoreCase);
            bool capturePlacement =
                string.Equals(
                    captureState,
                    "placement",
                    StringComparison.OrdinalIgnoreCase);
            if ((captureAction || capturePlacement) &&
                prompt != null)
            {
                DuelChoice cardChoice = prompt.Choices.FirstOrDefault(
                    choice =>
                        choice.CardCode != 0 &&
                        (!choice.HasLocation ||
                         choice.Location == DuelLocation.Hand));
                if (cardChoice != null)
                {
                    HandleCardClick(
                        cardChoice.CardCode,
                        cardChoice.HasLocation
                            ? cardChoice.Controller
                            : byte.MaxValue,
                        cardChoice.HasLocation
                            ? cardChoice.Location
                            : (byte)0,
                        cardChoice.HasLocation
                            ? (int)cardChoice.Sequence
                            : -1);
                    if (capturePlacement)
                    {
                        DuelChoice summon =
                            contextualChoices.FirstOrDefault(
                                choice =>
                                    choice.Label.IndexOf(
                                        "Invocar",
                                        StringComparison.OrdinalIgnoreCase) >= 0);
                        if (summon != null)
                        {
                            Submit(summon);
                            yield return new WaitForSecondsRealtime(0.25f);
                        }
                    }
                }
            }
            else if (string.Equals(
                         captureState,
                         "phase",
                         StringComparison.OrdinalIgnoreCase) &&
                     prompt != null &&
                     prompt.Choices.Any(IsPhaseChoice))
            {
                showPhaseChoices = true;
            }
            yield return new WaitForSecondsRealtime(0.4f);
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForSecondsRealtime(1.2f);
            Application.Quit(0);
        }
    }
}
