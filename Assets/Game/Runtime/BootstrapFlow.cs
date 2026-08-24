using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Content;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneDuel.Game
{
    public sealed class BootstrapFlow : MonoBehaviour
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private Texture2D background;
        private Texture2D white;
        private Texture2D buttonNormal;
        private Texture2D buttonHover;
        private Texture2D buttonActive;
        private GUIStyle logoStyle;
        private GUIStyle duelStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle tinyStyle;
        private GUIStyle menuButtonStyle;
        private GUIStyle modalTitle54;
        private GUIStyle modalTitle50;
        private bool showRules;
        private bool showDiagnostics;
        private bool showDuelSelection;
        private bool showBotDeckSelection;
        private bool showOptions;
        private float pulse;
        private string supportStatus = string.Empty;
        private string duelModeStatus = string.Empty;
        private CardDatabase database;
        private CardVisualCatalog visuals;
        private CardViewRegistry cardViews;
        private DeckLibraryFile deckLibrary;

        private bool AudioEnabled
        {
            get => PlayerPrefs.GetInt("ArcaneAudioEnabled", 1) != 0;
            set
            {
                PlayerPrefs.SetInt("ArcaneAudioEnabled", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            Application.runInBackground = true;
            white = Solid(Color.white);
            buttonNormal = Solid(new Color(0.025f, 0.11f, 0.18f, 0.96f));
            buttonHover = Solid(new Color(0.05f, 0.32f, 0.42f, 0.98f));
            buttonActive = Solid(new Color(0.35f, 0.20f, 0.48f, 0.98f));
            try
            {
                database = CardDatabase.LoadDefault();
                visuals = CardVisualCatalog.LoadDefault();
                cardViews = new CardViewRegistry(visuals);
                deckLibrary = DeckLibraryRepository.LoadOrCreate(
                    out string libraryStatus);
                duelModeStatus = libraryStatus;
            }
            catch (Exception exception)
            {
                duelModeStatus =
                    $"Decks indisponíveis: {exception.GetBaseException().Message}";
                Debug.LogException(exception);
            }

            string path = YgoContentLocator.Resolve(
                "UI",
                "title_arena.png");
            if (File.Exists(path))
            {
                background = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!background.LoadImage(File.ReadAllBytes(path)))
                {
                    Destroy(background);
                    background = null;
                }
            }

            if (HasArgument("-arcaneSkipTitle"))
            {
                SceneManager.LoadScene(ProjectIdentity.DuelScene);
                return;
            }
            if (HasArgument("-arcaneDuelSelection"))
            {
                showDuelSelection = true;
            }
            if (HasArgument("-arcaneOptions"))
            {
                showOptions = true;
            }
            string capture = ArgumentValue("-arcaneCapture");
            if (!string.IsNullOrEmpty(capture))
            {
                StartCoroutine(CaptureAndExit(capture));
            }
        }

        private void Update()
        {
            pulse = (Mathf.Sin(Time.unscaledTime * 1.7f) + 1f) * 0.5f;
            if (ArcaneInput.EnterPressedThisFrame &&
                !showRules &&
                !showDiagnostics &&
                !showDuelSelection &&
                !showOptions)
            {
                showDuelSelection = true;
            }
            if (ArcaneInput.EscapePressedThisFrame)
            {
                if (showRules) showRules = false;
                else if (showDiagnostics) showDiagnostics = false;
                else if (showBotDeckSelection) showBotDeckSelection = false;
                else if (showOptions) showOptions = false;
                else if (showDuelSelection) showDuelSelection = false;
                else Application.Quit(0);
            }
        }

        private void OnDestroy()
        {
            if (background != null) Destroy(background);
            if (white != null) Destroy(white);
            if (buttonNormal != null) Destroy(buttonNormal);
            if (buttonHover != null) Destroy(buttonHover);
            if (buttonActive != null) Destroy(buttonActive);
            cardViews?.Dispose();
        }

        private void OnGUI()
        {
            EnsureStyles();
            Color screenColor = GUI.color;
            GUI.color = new Color(0.005f, 0.01f, 0.025f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = screenColor;

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

            if (background != null)
            {
                GUI.DrawTexture(
                    new Rect(0, 0, DesignWidth, DesignHeight),
                    background,
                    ScaleMode.ScaleAndCrop);
            }
            else
            {
                Fill(
                    new Rect(0, 0, DesignWidth, DesignHeight),
                    new Color(0.01f, 0.02f, 0.06f));
            }
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0.005f, 0.01f, 0.03f, 0.28f));
            Fill(
                new Rect(0, 0, 1030, DesignHeight),
                new Color(0.005f, 0.012f, 0.035f, 0.38f));
            Fill(new Rect(0, 0, DesignWidth, 7), new Color(0.16f, 0.92f, 1f));
            Fill(new Rect(0, 7, DesignWidth, 2), new Color(0.82f, 0.55f, 0.2f));

            DrawBrand();
            DrawMenu();
            if (showRules) DrawRules();
            if (showDiagnostics) DrawDiagnostics();
            if (showDuelSelection) DrawDuelSelection();
            if (showBotDeckSelection) DrawBotDeckSelection();
            if (showOptions) DrawOptions();
            GUI.matrix = previous;
        }

        private void DrawBrand()
        {
            GUI.Label(
                new Rect(104, 92, 700, 34),
                "ARCANE // DUEL SYSTEM",
                tinyStyle);
            GUI.Label(new Rect(92, 142, 790, 116), "ARCANE", logoStyle);
            GUI.Label(new Rect(96, 246, 790, 134), "DUEL", duelStyle);
            float glow = Mathf.Lerp(0.45f, 0.95f, pulse);
            Fill(
                new Rect(108, 391, 620, 3),
                new Color(0.18f, 0.9f, 1f, glow));
            Fill(
                new Rect(108, 398, 380, 1),
                new Color(0.92f, 0.67f, 0.25f, 0.82f));
            GUI.Label(
                new Rect(106, 426, 660, 85),
                "DOMINE O CAMPO. CONSTRUA A CORRENTE.\nDECIDA O DUELO.",
                subtitleStyle);
            GUI.Label(
                new Rect(108, 526, 590, 84),
                "Uma arena original de estratégia construída sobre o núcleo oficial de regras OCG.",
                bodyStyle);

            Fill(
                new Rect(108, 858, 720, 1),
                new Color(0.3f, 0.58f, 0.7f, 0.45f));
            GUI.Label(
                new Rect(108, 878, 760, 32),
                "CORE API 11.0 · REGRAS MESTRE 5 · CATÁLOGO COMPLETO 200",
                tinyStyle);
            GUI.Label(
                new Rect(108, 914, 760, 32),
                $"UNITY {ProjectIdentity.UnityVersion} · RELEASE {ProjectIdentity.ProjectVersion}",
                tinyStyle);
        }

        private void DrawMenu()
        {
            Rect panel = new Rect(1305, 122, 480, 840);
            Fill(panel, new Color(0.01f, 0.025f, 0.06f, 0.93f));
            Stroke(panel, new Color(0.23f, 0.78f, 0.91f, 0.76f), 2);
            Fill(
                new Rect(panel.x, panel.y, 7, panel.height),
                new Color(0.76f, 0.45f, 0.96f, 0.92f));
            GUI.Label(
                new Rect(1352, 154, 385, 44),
                "PORTAL DO DUELISTA",
                subtitleStyle);
            GUI.Label(
                new Rect(1352, 199, 385, 42),
                "Monte. Teste. Duele.",
                bodyStyle);

            if (GUI.Button(
                new Rect(1350, 252, 390, 72),
                "DUELAR",
                menuButtonStyle))
            {
                showDuelSelection = true;
            }
            if (GUI.Button(
                new Rect(1350, 337, 390, 72),
                "DECKS",
                menuButtonStyle))
            {
                CardLabNavigation.Open(CardLabMode.Gallery);
                SceneManager.LoadScene(ProjectIdentity.CardLabScene);
            }
            if (GUI.Button(
                new Rect(1350, 422, 390, 72),
                "LOJA DE DECKS",
                menuButtonStyle))
            {
                CardLabNavigation.Open(CardLabMode.Shop);
                SceneManager.LoadScene(ProjectIdentity.CardLabScene);
            }
            if (GUI.Button(
                new Rect(1350, 507, 390, 72),
                "OPÇÕES",
                menuButtonStyle))
            {
                showOptions = true;
            }

            if (GUI.Button(
                new Rect(1350, 604, 185, 52),
                "REGRAS",
                menuButtonStyle))
            {
                showRules = true;
            }
            if (GUI.Button(
                new Rect(1555, 604, 185, 52),
                "SUPORTE",
                menuButtonStyle))
            {
                showDiagnostics = true;
            }
            if (GUI.Button(
                new Rect(1350, 670, 390, 52),
                AudioEnabled ? "ÁUDIO · ATIVO" : "ÁUDIO · DESATIVADO",
                menuButtonStyle))
            {
                AudioEnabled = !AudioEnabled;
            }

            Fill(
                new Rect(1350, 744, 390, 74),
                new Color(0.025f, 0.07f, 0.12f, 0.92f));
            Stroke(
                new Rect(1350, 744, 390, 74),
                new Color(0.18f, 0.47f, 0.58f, 0.65f),
                1);
            GUI.Label(
                new Rect(1378, 755, 340, 24),
                "CONTEÚDO LOCAL VALIDADO",
                tinyStyle);
            GUI.Label(
                new Rect(1378, 781, 340, 26),
                "200 CARTAS · 200 ARTES · 8 LOTES",
                bodyStyle);

            if (GUI.Button(
                new Rect(1350, 840, 390, 52),
                "SAIR DO JOGO",
                menuButtonStyle))
            {
                Application.Quit(0);
            }
            GUI.Label(
                new Rect(1352, 875, 385, 28),
                "ENTER · iniciar duelo     ESC · voltar/sair",
                tinyStyle);
            GUI.Label(
                new Rect(1352, 911, 385, 25),
                "Arcane Duel Team · projeto independente",
                tinyStyle);
        }

        private void DrawRules()
        {
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.84f));
            Rect modal = new Rect(360, 135, 1200, 810);
            Fill(modal, new Color(0.015f, 0.035f, 0.075f, 0.99f));
            Stroke(modal, new Color(0.22f, 0.86f, 0.95f), 2);
            GUI.Label(
                new Rect(420, 178, 1080, 60),
                "REGRAS DA ARENA",
                duelStyle);
            GUI.Label(
                new Rect(450, 270, 1020, 470),
                "• Cada duelista começa com 8.000 Pontos de Vida e cinco cartas.\n\n" +
                "• Fases, Invocações, tributos, Correntes, Magias, Armadilhas e batalha são decididos pelo ocgcore; a interface apenas apresenta escolhas válidas.\n\n" +
                "• Use o painel SUA DECISÃO para Invocar, baixar cartas, ativar efeitos, atacar ou encerrar a fase.\n\n" +
                "• Cartas indicadas pelo Core recebem um contorno violeta. Clique em qualquer carta visível para consultar seus dados.\n\n" +
                "• MODO AUTO executa a política determinística de teste. Desative-o para comandar o duelo manualmente.\n\n" +
                "• Monte e salve seu deck no Laboratório. O jogo valida 40–60 cartas no Main Deck, até 15 no Extra Deck e três cópias por código/alias.",
                bodyStyle);
            if (GUI.Button(
                new Rect(760, 828, 400, 72),
                "VOLTAR AO PORTAL",
                menuButtonStyle))
            {
                showRules = false;
            }
        }

        private void DrawDiagnostics()
        {
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.84f));
            Rect modal = new Rect(415, 150, 1090, 780);
            Fill(modal, new Color(0.015f, 0.035f, 0.075f, 0.99f));
            Stroke(modal, new Color(0.76f, 0.46f, 0.96f), 2);
            GUI.Label(
                new Rect(470, 192, 980, 58),
                "DIAGNÓSTICO DA BUILD",
                duelStyle);
            string coreVersion;
            try
            {
                coreVersion = OcgCoreVersionProbe.Read().ToString();
            }
            catch (Exception exception)
            {
                coreVersion = $"indisponível ({exception.GetBaseException().Message})";
            }
            GUI.Label(
                new Rect(500, 296, 920, 330),
                $"Arcane Duel: {ProjectIdentity.ProjectVersion}\n" +
                $"Unity: {Application.unityVersion} (alvo {ProjectIdentity.UnityVersion})\n" +
                $"OCG API carregada: {coreVersion}\n" +
                $"ygopro-core: {ProjectIdentity.CoreCommit}\n" +
                $"CardScripts: {ProjectIdentity.CardScriptsCommit}\n" +
                $"BabelCDB: {ProjectIdentity.BabelCdbCommit}\n" +
                $"Plataforma: {Application.platform}\n" +
                $"Sistema: {SystemInfo.operatingSystem}\n" +
                $"Log: {Application.consoleLogPath}",
                bodyStyle);
            if (GUI.Button(
                new Rect(530, 674, 395, 70),
                "EXPORTAR PACOTE DE SUPORTE",
                menuButtonStyle))
            {
                try
                {
                    supportStatus = $"Pacote criado em:\n{SupportBundle.Export()}";
                }
                catch (Exception exception)
                {
                    supportStatus =
                        $"Falha ao exportar: {exception.GetBaseException().Message}";
                }
            }
            if (GUI.Button(
                new Rect(995, 674, 395, 70),
                "VOLTAR AO PORTAL",
                menuButtonStyle))
            {
                showDiagnostics = false;
            }
            GUI.Label(
                new Rect(500, 775, 920, 90),
                supportStatus,
                tinyStyle);
        }

        private void DrawDuelSelection()
        {
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.86f));
            Rect modal = new Rect(360, 95, 1200, 900);
            Fill(modal, new Color(0.012f, 0.03f, 0.07f, 0.99f));
            Stroke(modal, new Color(0.18f, 0.88f, 0.98f), 2);
            Fill(
                new Rect(modal.x, modal.y, modal.width, 7),
                new Color(0.74f, 0.42f, 1f));
            GUI.Label(
                new Rect(modal.x + 70, 138, modal.width - 140, 78),
                "MODOS DE DUELO",
                modalTitle54);
            GUI.Label(
                new Rect(465, 225, 990, 64),
                ActiveDeckSummary(),
                bodyStyle);

            if (GUI.Button(
                new Rect(570, 330, 780, 76),
                "ENFRENTAR BOT",
                menuButtonStyle))
            {
                showBotDeckSelection = true;
            }
            if (GUI.Button(
                new Rect(570, 424, 780, 76),
                "CRIAR SALA PRIVADA",
                menuButtonStyle))
            {
                duelModeStatus =
                    "A sala privada está preservada no fluxo visual. A conexão online será ativada somente após a aceitação final da build local, conforme o plano do projeto.";
            }
            if (GUI.Button(
                new Rect(570, 518, 780, 76),
                "ENTRAR COM CÓDIGO",
                menuButtonStyle))
            {
                duelModeStatus =
                    "Entrada por código preparada para a etapa multiplayer; nenhuma sala fictícia é criada nesta build local.";
            }
            if (GUI.Button(
                new Rect(570, 612, 780, 76),
                "TREINO GUIADO LOCAL",
                menuButtonStyle))
            {
                StartDuel(true, false);
            }
            if (GUI.Button(
                new Rect(570, 706, 780, 62),
                "ASSISTIR DUELO DEMONSTRAÇÃO",
                menuButtonStyle))
            {
                StartDuel(false, true);
            }

            GUI.Label(
                new Rect(535, 786, 850, 64),
                duelModeStatus,
                tinyStyle);
            if (GUI.Button(
                new Rect(1010, 872, 340, 58),
                "EDITAR DECK ANTES DO DUELO",
                menuButtonStyle))
            {
                CardLabNavigation.Open(
                    CardLabMode.Editor,
                    deckLibrary?.activeDeckId);
                SceneManager.LoadScene(ProjectIdentity.CardLabScene);
            }
            if (GUI.Button(
                new Rect(570, 872, 340, 58),
                "VOLTAR AO PORTAL",
                menuButtonStyle))
            {
                showDuelSelection = false;
            }
        }

        private void DrawBotDeckSelection()
        {
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.92f));
            Rect modal = new Rect(165, 78, 1590, 930);
            Fill(modal, new Color(0.012f, 0.03f, 0.07f, 0.995f));
            Stroke(modal, new Color(0.18f, 0.88f, 0.98f), 2);
            Fill(new Rect(modal.x, modal.y, modal.width, 7), new Color(0.7f, 1f, 0.06f));
            GUI.Label(
                new Rect(235, 122, 1100, 62),
                "ESCOLHA O DECK DO BOT",
                duelStyle);
            GUI.Label(
                new Rect(240, 188, 1200, 36),
                "O bot recebe uma cópia independente. Seu deck ativo não é alterado.",
                bodyStyle);

            List<DeckFile> decks = deckLibrary?.decks ?? new List<DeckFile>();
            for (int index = 0; index < decks.Count; index++)
            {
                int column = index % 3;
                int row = index / 3;
                DeckFile previewDeck = decks[index];
                DrawDeckPreview(
                    previewDeck,
                    new Rect(245 + column * 500, 270 + row * 330, 450, 280),
                    () =>
                    {
                        PlayerPrefs.SetString(
                            CardLabNavigation.OpponentDeckKey,
                            previewDeck.id);
                        PlayerPrefs.Save();
                        StartDuel(false, false);
                    },
                    "DUELAR CONTRA ESTE DECK");
            }
            if (GUI.Button(
                new Rect(760, 914, 400, 56),
                "VOLTAR AOS MODOS",
                menuButtonStyle))
            {
                showBotDeckSelection = false;
            }
        }

        private void DrawDeckPreview(
            DeckFile deck,
            Rect rect,
            Action onClick,
            string actionLabel)
        {
            bool valid = database != null &&
                         visuals != null &&
                         DeckRules.Validate(deck, database, visuals).IsValid;
            Color accent = valid
                ? new Color(0.64f, 1f, 0.04f)
                : new Color(1f, 0.18f, 0.38f);
            Fill(rect, new Color(0.025f, 0.065f, 0.09f, 0.98f));
            Stroke(rect, accent, 2);
            GUI.Label(
                new Rect(rect.x + 24, rect.y + 18, rect.width - 48, 30),
                deck.theme,
                tinyStyle);
            uint[] preview = deck.mainDeck
                .Distinct()
                .Take(3)
                .ToArray();
            for (int index = 0; index < preview.Length; index++)
            {
                Rect cardRect = new Rect(
                    rect.x + 126 + index * 62,
                    rect.y + 57 + Mathf.Abs(1 - index) * 10,
                    92,
                    126);
                if (cardViews != null &&
                    cardViews.TryGetTexture(preview[index], out Texture2D texture))
                {
                    GUI.DrawTexture(cardRect, texture, ScaleMode.ScaleAndCrop);
                }
                Stroke(cardRect, new Color(0.68f, 0.88f, 1f), 1);
            }
            GUI.Label(
                new Rect(rect.x + 20, rect.y + 188, rect.width - 40, 34),
                deck.name,
                subtitleStyle);
            GUI.Label(
                new Rect(rect.x + 20, rect.y + 220, 180, 28),
                $"{deck.mainDeck.Count} MAIN · {deck.extraDeck.Count} EXTRA",
                tinyStyle);
            GUI.enabled = valid;
            if (GUI.Button(
                new Rect(rect.x + 218, rect.y + 216, rect.width - 238, 45),
                valid ? actionLabel : "DECK INCOMPLETO",
                menuButtonStyle))
            {
                onClick();
            }
            GUI.enabled = true;
        }

        private void DrawOptions()
        {
            Fill(
                new Rect(0, 0, DesignWidth, DesignHeight),
                new Color(0f, 0f, 0.02f, 0.9f));
            Rect modal = new Rect(300, 100, 1320, 880);
            Fill(modal, new Color(0.018f, 0.11f, 0.14f, 0.995f));
            Stroke(modal, new Color(0.16f, 0.92f, 1f), 2);
            GUI.Label(
                new Rect(modal.x + 70, 122, modal.width - 140, 76),
                "ANIMAÇÕES DO DUELO",
                modalTitle50);
            GUI.Label(
                new Rect(520, 215, 900, 42),
                "Configuração local: altera apenas a apresentação deste dispositivo.",
                bodyStyle);

            DrawActivationResponseRow(new Rect(380, 260, 1160, 72));
            DrawOptionRow(
                new Rect(380, 350, 1160, 140),
                "INVOCAÇÃO DE MONSTROS",
                DuelAnimationFamily.Summon);
            DrawOptionRow(
                new Rect(380, 510, 1160, 140),
                "ATIVAÇÃO DE MAGIAS / ARMADILHAS",
                DuelAnimationFamily.Activation);
            DrawOptionRow(
                new Rect(380, 670, 1160, 140),
                "APRESENTAÇÃO DA CORRENTE",
                DuelAnimationFamily.Chain);

            if (GUI.Button(
                new Rect(510, 840, 380, 62),
                "RESTAURAR PADRÃO",
                menuButtonStyle))
            {
                DuelPresentationPreferences.RestoreDefaults();
                DuelActivationPreferences.RestoreDefaults();
            }
            if (GUI.Button(
                new Rect(1030, 840, 380, 62),
                "SALVAR E VOLTAR",
                menuButtonStyle))
            {
                showOptions = false;
            }
        }

        private void DrawActivationResponseRow(Rect rect)
        {
            Fill(rect, new Color(0.03f, 0.22f, 0.25f, 0.95f));
            Stroke(rect, new Color(0.12f, 0.72f, 0.82f), 1);
            GUI.Label(
                new Rect(rect.x + 18, rect.y + 10, 160, 52),
                "RESPOSTAS",
                subtitleStyle);

            ActivationPromptMode[] modes =
            {
                ActivationPromptMode.On,
                ActivationPromptMode.Auto,
                ActivationPromptMode.Off
            };
            for (int index = 0; index < modes.Length; index++)
            {
                ActivationPromptMode mode = modes[index];
                Color original = GUI.backgroundColor;
                if (DuelActivationPreferences.Mode == mode)
                    GUI.backgroundColor = new Color(0.68f, 1f, 0.04f);
                if (GUI.Button(
                    new Rect(rect.x + 170 + index * 90, rect.y + 12, 78, 48),
                    DuelActivationPreferences.DisplayName(mode),
                    menuButtonStyle))
                {
                    DuelActivationPreferences.Mode = mode;
                }
                GUI.backgroundColor = original;
            }

            if (GUI.Button(
                new Rect(rect.x + 450, rect.y + 12, 210, 48),
                DuelActivationPreferences.ResponseWindowRhythmName,
                menuButtonStyle))
            {
                DuelActivationPreferences.ClassicResponseWindows =
                    !DuelActivationPreferences.ClassicResponseWindows;
            }
            if (GUI.Button(
                new Rect(rect.x + 670, rect.y + 12, 205, 48),
                DuelActivationPreferences.SelfChainEnabled
                    ? "SELF CHAIN: ON" : "SELF CHAIN: OFF",
                menuButtonStyle))
            {
                DuelActivationPreferences.SelfChainEnabled =
                    !DuelActivationPreferences.SelfChainEnabled;
            }
            if (GUI.Button(
                new Rect(rect.x + 885, rect.y + 12, 250, 48),
                DuelActivationPreferences.ManualChainOrder
                    ? "ORDEM: MANUAL" : "ORDEM: CORE",
                menuButtonStyle))
            {
                DuelActivationPreferences.ManualChainOrder =
                    !DuelActivationPreferences.ManualChainOrder;
            }
        }

        private void DrawOptionRow(
            Rect rect,
            string label,
            DuelAnimationFamily family)
        {
            Fill(rect, new Color(0.03f, 0.22f, 0.25f, 0.95f));
            Stroke(rect, new Color(0.12f, 0.72f, 0.82f), 1);
            GUI.Label(
                new Rect(rect.x + 30, rect.y + 16, 560, 30),
                label,
                subtitleStyle);
            bool enabled = DuelPresentationPreferences.IsEnabled(family);
            if (GUI.Button(
                new Rect(rect.x + 30, rect.y + 68, 300, 52),
                enabled ? "LIGADA" : "DESLIGADA",
                menuButtonStyle))
            {
                DuelPresentationPreferences.SetEnabled(family, !enabled);
            }

            float current = DuelPresentationPreferences.Speed(family);
            float[] speeds = { 0.75f, 1f, 1.5f, 2f };
            for (int index = 0; index < speeds.Length; index++)
            {
                float speed = speeds[index];
                Color original = GUI.backgroundColor;
                if (Mathf.Approximately(current, speed))
                {
                    GUI.backgroundColor = new Color(0.68f, 1f, 0.04f);
                }
                if (GUI.Button(
                    new Rect(rect.x + 480 + index * 155, rect.y + 68, 132, 52),
                    $"{speed:0.##}x",
                    menuButtonStyle))
                {
                    DuelPresentationPreferences.SetSpeed(family, speed);
                }
                GUI.backgroundColor = original;
            }
        }

        private string ActiveDeckSummary()
        {
            DeckFile active = deckLibrary?.Find(deckLibrary.activeDeckId);
            return active == null
                ? "DECK ATIVO · carregando biblioteca..."
                : $"DECK ATIVO · {active.name} · {active.mainDeck.Count} MAIN · " +
                  $"{active.extraDeck.Count} EXTRA\n" +
                  "Enfrente um bot local ou use o treinamento guiado.";
        }

        private void DrawModeCard(
            Rect rect,
            string title,
            string subtitle,
            string description,
            Color accent,
            Action onClick)
        {
            Fill(rect, new Color(0.022f, 0.06f, 0.105f, 0.98f));
            Stroke(rect, new Color(accent.r, accent.g, accent.b, 0.72f), 2);
            Fill(new Rect(rect.x, rect.y, rect.width, 8), accent);
            Fill(
                new Rect(rect.x + 36, rect.y + 40, 88, 88),
                new Color(accent.r * 0.32f, accent.g * 0.32f, accent.b * 0.32f));
            Stroke(
                new Rect(rect.x + 36, rect.y + 40, 88, 88),
                accent,
                2);
            GUI.Label(
                new Rect(rect.x + 40, rect.y + 58, 80, 50),
                "◆",
                duelStyle);
            GUI.Label(
                new Rect(rect.x + 36, rect.y + 152, rect.width - 72, 50),
                title,
                subtitleStyle);
            GUI.Label(
                new Rect(rect.x + 36, rect.y + 198, rect.width - 72, 30),
                subtitle,
                tinyStyle);
            GUI.Label(
                new Rect(rect.x + 36, rect.y + 244, rect.width - 72, 92),
                description,
                bodyStyle);
            if (GUI.Button(
                new Rect(rect.x + 36, rect.yMax - 64, rect.width - 72, 44),
                "SELECIONAR E DUELAR",
                menuButtonStyle))
            {
                onClick();
            }
        }

        private void StartDuel(bool tutorial, bool automatic)
        {
            PlayerPrefs.SetInt("ArcaneTutorialMode", tutorial ? 1 : 0);
            PlayerPrefs.SetInt("ArcaneAutoStart", automatic ? 1 : 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
        }

        private void EnsureStyles()
        {
            if (logoStyle != null) return;
            logoStyle = Label(
                86,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleLeft);
            duelStyle = Label(
                66,
                FontStyle.Bold,
                new Color(0.67f, 0.89f, 1f),
                TextAnchor.MiddleLeft);
            subtitleStyle = Label(
                24,
                FontStyle.Bold,
                new Color(0.5f, 0.92f, 1f),
                TextAnchor.MiddleLeft);
            subtitleStyle.wordWrap = true;
            bodyStyle = Label(
                20,
                FontStyle.Normal,
                new Color(0.82f, 0.9f, 0.96f),
                TextAnchor.UpperLeft);
            bodyStyle.wordWrap = true;
            tinyStyle = Label(
                13,
                FontStyle.Bold,
                new Color(0.55f, 0.75f, 0.85f),
                TextAnchor.MiddleLeft);
            tinyStyle.wordWrap = true;
            menuButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(28, 18, 8, 8),
                wordWrap = true
            };
            menuButtonStyle.normal.background = buttonNormal;
            menuButtonStyle.hover.background = buttonHover;
            menuButtonStyle.active.background = buttonActive;
            menuButtonStyle.normal.textColor = Color.white;
            menuButtonStyle.hover.textColor = Color.white;
            menuButtonStyle.active.textColor = Color.white;
            modalTitle54 = new GUIStyle(duelStyle)
            {
                fontSize = 54,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            modalTitle50 = new GUIStyle(duelStyle)
            {
                fontSize = 50,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
        }

        private static GUIStyle Label(
            int size,
            FontStyle style,
            Color color,
            TextAnchor anchor)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = style,
                alignment = anchor,
                normal = { textColor = color }
            };
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
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
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static bool HasArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            foreach (string argument in arguments)
            {
                if (string.Equals(
                    argument,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ArgumentValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(
                    arguments[index],
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }
            return null;
        }

        private static IEnumerator CaptureAndExit(string path)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForSecondsRealtime(1.2f);
            Application.Quit(0);
        }
    }
}
