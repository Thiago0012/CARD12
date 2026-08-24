using System;
using ArcaneArena.Multiplayer;
using ArcaneArena.StoryRoguelite;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Menu não destrutivo exibido sobre a arena. Ele nunca altera
    /// Time.timeScale: a sessão Relay e o Core continuam vivos enquanto a
    /// sobreposição bloqueia apenas os cliques locais no campo.
    /// </summary>
    public sealed partial class GameFrontendBootstrap
    {
        private enum DuelMenuPage
        {
            Overview,
            Settings,
            Responses,
            Help,
            ExitConfirmation
        }

        private GameObject _duelMenuOverlay;
        private RectTransform _duelMenuContent;
        private DuelMenuPage _duelMenuPage;

        private bool IsDuelMenuVisible =>
            _duelMenuOverlay != null && _duelMenuOverlay.activeInHierarchy;

        private void ToggleDuelMenu()
        {
            if (!_duelPresentationVisible)
                return;

            if (IsDuelMenuVisible)
            {
                CloseDuelMenu();
                return;
            }

            OpenDuelMenu();
        }

        private void OpenDuelMenu()
        {
            if (_screenRoot == null || IsDuelMenuVisible)
                return;

            Image overlay = CreatePanel(
                _screenRoot,
                "Menu do Duelo",
                Vector2.zero,
                Vector2.one,
                new Color(0.005f, 0.012f, 0.025f, 0.86f));
            _duelMenuOverlay = overlay.gameObject;
            overlay.raycastTarget = true;
            overlay.transform.SetAsLastSibling();

            GameObject energyObject = new(
                "Energia do Menu",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelModeBackdropGraphic));
            energyObject.transform.SetParent(overlay.transform, false);
            RectTransform energyRect =
                energyObject.GetComponent<RectTransform>();
            Stretch(energyRect);
            DuelModeBackdropGraphic energy =
                energyObject.GetComponent<DuelModeBackdropGraphic>();
            energy.raycastTarget = false;
            energy.SetAccent(ArcaneCyan);

            Image shell = CreateArcaneSurface(
                overlay.transform,
                "Central do Duelo",
                new Vector2(0.15f, 0.085f),
                new Vector2(0.85f, 0.915f),
                ArcaneGold,
                true,
                0.96f);

            CreateText(
                shell.transform,
                "MENU DO DUELO",
                30,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.895f),
                new Vector2(0.52f, 0.975f),
                TextAnchor.MiddleLeft);
            CreateText(
                shell.transform,
                DuelMenuSessionCaption(),
                12,
                FontStyle.Bold,
                new Color(0.57f, 0.84f, 0.92f, 1f),
                new Vector2(0.50f, 0.91f),
                new Vector2(0.965f, 0.965f),
                TextAnchor.MiddleRight);
            CreatePanel(
                shell.transform,
                "Linha do Menu",
                new Vector2(0.03f, 0.887f),
                new Vector2(0.97f, 0.891f),
                new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.78f))
                .raycastTarget = false;

            BuildDuelMenuNavigation(shell.transform);
            Image content = CreateArcaneSurface(
                shell.transform,
                "Conteúdo do Menu",
                new Vector2(0.31f, 0.055f),
                new Vector2(0.97f, 0.865f),
                ArcaneCyan,
                false,
                0.77f);
            _duelMenuContent = content.rectTransform;

            ShowDuelMenuPage(DuelMenuPage.Overview);
        }

        private void CloseDuelMenu()
        {
            if (_duelMenuOverlay == null)
                return;

            _duelMenuOverlay.SetActive(false);
            Destroy(_duelMenuOverlay);
            _duelMenuOverlay = null;
            _duelMenuContent = null;
        }

        private void BuildDuelMenuNavigation(Transform parent)
        {
            Image rail = CreateArcaneSurface(
                parent,
                "Navegação do Menu",
                new Vector2(0.03f, 0.055f),
                new Vector2(0.292f, 0.865f),
                ArcaneGold,
                false,
                0.77f);

            CreateText(
                rail.transform,
                "PARTIDA ATUAL",
                12,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.08f, 0.89f),
                new Vector2(0.92f, 0.96f),
                TextAnchor.MiddleLeft);
            CreateText(
                rail.transform,
                DuelMenuConnectionDescription(),
                14,
                FontStyle.Normal,
                new Color(0.70f, 0.77f, 0.84f, 1f),
                new Vector2(0.08f, 0.75f),
                new Vector2(0.92f, 0.89f),
                TextAnchor.UpperLeft);

            BuildDuelMenuNavigationButton(
                rail.transform,
                "VISÃO GERAL",
                0.64f,
                ArcaneCyan,
                () => ShowDuelMenuPage(DuelMenuPage.Overview));
            BuildDuelMenuNavigationButton(
                rail.transform,
                "CONFIGURAÇÕES",
                0.53f,
                OptionsViolet,
                () => ShowDuelMenuPage(DuelMenuPage.Settings));
            BuildDuelMenuNavigationButton(
                rail.transform,
                "RESPOSTAS DO DUELO",
                0.42f,
                OptionsMint,
                () => ShowDuelMenuPage(DuelMenuPage.Responses));
            BuildDuelMenuNavigationButton(
                rail.transform,
                "AJUDA RÁPIDA",
                0.31f,
                OptionsAmber,
                () => ShowDuelMenuPage(DuelMenuPage.Help));

            CreateArcaneActionButton(
                rail.transform,
                StoryRogueliteRuntime.IsStoryDuel
                    ? "ABANDONAR HISTÓRIA"
                    : "SAIR DO DUELO",
                new Vector2(0.08f, 0.16f),
                new Vector2(0.92f, 0.25f),
                Danger,
                () => ShowDuelMenuPage(DuelMenuPage.ExitConfirmation),
                15);
            CreateArcaneActionButton(
                rail.transform,
                "CONTINUAR DUELO",
                new Vector2(0.08f, 0.045f),
                new Vector2(0.92f, 0.135f),
                Lime,
                CloseDuelMenu,
                16);
        }

        private static void BuildDuelMenuNavigationButton(
            Transform parent,
            string label,
            float yMin,
            Color accent,
            Action action)
        {
            CreateArcaneActionButton(
                parent,
                label,
                new Vector2(0.08f, yMin),
                new Vector2(0.92f, yMin + 0.085f),
                accent,
                action,
                14);
        }

        private void ShowDuelMenuPage(DuelMenuPage page)
        {
            if (_duelMenuContent == null)
                return;

            _duelMenuPage = page;
            for (int index = _duelMenuContent.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = _duelMenuContent.GetChild(index);
                // A superfície graduada pertence ao contêiner e precisa ser
                // preservada; somente a página dinâmica é substituída.
                if (child.GetComponent<ArcanePanelSheenGraphic>() != null)
                    continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            Image pageRoot = CreatePanel(
                _duelMenuContent,
                $"Página {page}",
                Vector2.zero,
                Vector2.one,
                Color.clear);
            pageRoot.raycastTarget = false;

            switch (page)
            {
                case DuelMenuPage.Settings:
                    BuildDuelMenuSettings(pageRoot.transform);
                    break;
                case DuelMenuPage.Responses:
                    BuildDuelMenuResponses(pageRoot.transform);
                    break;
                case DuelMenuPage.Help:
                    BuildDuelMenuHelp(pageRoot.transform);
                    break;
                case DuelMenuPage.ExitConfirmation:
                    BuildDuelMenuExitConfirmation(pageRoot.transform);
                    break;
                default:
                    BuildDuelMenuOverview(pageRoot.transform);
                    break;
            }
        }

        private void BuildDuelMenuOverview(Transform parent)
        {
            BuildDuelMenuPageHeader(
                parent,
                "DUELO EM ANDAMENTO",
                "A interface está protegida; o Core e a conexão continuam ativos.");

            BuildDuelMenuInfoCard(
                parent,
                "CONEXÃO",
                DuelOnlineSession.Instance?.IsOnlineDuelActive == true
                    ? "ONLINE • RELAY ATIVO"
                    : "DUELO LOCAL",
                ArcaneCyan,
                new Vector2(0.045f, 0.63f),
                new Vector2(0.48f, 0.82f));
            BuildDuelMenuInfoCard(
                parent,
                "APRESENTAÇÃO",
                ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsPreferences.Quality),
                OptionsViolet,
                new Vector2(0.52f, 0.63f),
                new Vector2(0.955f, 0.82f));
            BuildDuelMenuInfoCard(
                parent,
                "ORIENTAÇÕES",
                DuelActivationPreferences.GuidanceMessagesEnabled
                    ? "VISÍVEIS" : "OCULTAS",
                OptionsMint,
                new Vector2(0.045f, 0.39f),
                new Vector2(0.48f, 0.58f));
            BuildDuelMenuInfoCard(
                parent,
                "CORRENTES",
                DuelActivationPreferences.ChainPanelEnabled
                    ? "PAINEL VISÍVEL" : "PAINEL OCULTO",
                OptionsAmber,
                new Vector2(0.52f, 0.39f),
                new Vector2(0.955f, 0.58f));

            CreateText(
                parent,
                "Use CONFIGURAÇÕES para áudio e leitura; RESPOSTAS DO DUELO " +
                "controla apenas a apresentação de avisos e correntes. " +
                "Nenhuma dessas opções altera as regras ou os alvos legais.",
                15,
                FontStyle.Normal,
                new Color(0.72f, 0.79f, 0.86f, 1f),
                new Vector2(0.055f, 0.12f),
                new Vector2(0.945f, 0.31f),
                TextAnchor.UpperLeft);
        }

        private void BuildDuelMenuSettings(Transform parent)
        {
            BuildDuelMenuPageHeader(
                parent,
                "CONFIGURAÇÕES RÁPIDAS",
                "Ajustes locais aplicados imediatamente neste dispositivo.");

            BuildDuelMenuSettingRow(
                parent,
                "EFEITOS SONOROS",
                "Ativa ou silencia sons de cartas e interface.",
                ArcaneAudioPreferences.Enabled ? "ATIVOS" : "SILENCIADOS",
                OptionsMint,
                0.68f,
                () =>
                {
                    ArcaneAudioPreferences.Enabled =
                        !ArcaneAudioPreferences.Enabled;
                    RefreshMasterAudioState();
                    ShowDuelMenuPage(_duelMenuPage);
                });
            BuildDuelMenuSettingRow(
                parent,
                "VOLUME DA MÚSICA",
                "Alterna entre silencioso, 50% e 100%.",
                $"{Mathf.RoundToInt(ArcaneMusicPreferences.Volume * 100f)}%",
                ArcaneGold,
                0.51f,
                () =>
                {
                    float current = ArcaneMusicPreferences.Volume;
                    ArcaneMusicPreferences.Volume = current < 0.25f
                        ? 0.5f
                        : current < 0.75f ? 1f : 0f;
                    RefreshMasterAudioState();
                    ShowDuelMenuPage(_duelMenuPage);
                });
            BuildDuelMenuSettingRow(
                parent,
                "QUALIDADE GRÁFICA",
                "Troca o perfil visual preservando o limite do aparelho.",
                ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsPreferences.Quality),
                OptionsViolet,
                0.34f,
                () =>
                {
                    ArcaneGraphicsQuality next =
                        ArcaneGraphicsPreferences.Quality ==
                            ArcaneGraphicsQuality.VeryHigh
                            ? ArcaneGraphicsQuality.VeryLow
                            : (ArcaneGraphicsQuality)
                                ((int)ArcaneGraphicsPreferences.Quality + 1);
                    ArcaneGraphicsPreferences.SetQuality(next);
                    ShowDuelMenuPage(_duelMenuPage);
                });
            BuildDuelMenuSettingRow(
                parent,
                "TAMANHO DO TEXTO",
                "Pequeno, médio ou grande em toda a interface.",
                ArcaneUiTextPreferences.DisplayName(
                    ArcaneUiTextPreferences.Current),
                ArcaneCyan,
                0.17f,
                () =>
                {
                    ArcaneUiTextPreferences.Set(
                        ArcaneUiTextPreferences.Next(
                            ArcaneUiTextPreferences.Current));
                    ShowDuelMenuPage(_duelMenuPage);
                });
        }

        private void BuildDuelMenuResponses(Transform parent)
        {
            BuildDuelMenuPageHeader(
                parent,
                "RESPOSTAS DO DUELO",
                "Preferências visuais; decisões obrigatórias continuam aparecendo.");

            BuildDuelMenuSettingRow(
                parent,
                "JANELAS OPCIONAIS",
                "ON exibe • AUTO aplica o padrão • OFF passa opções.",
                DuelActivationPreferences.DisplayName(
                    DuelActivationPreferences.Mode),
                OptionsViolet,
                0.64f,
                () =>
                {
                    DuelActivationPreferences.Mode =
                        DuelActivationPreferences.Mode switch
                        {
                            ActivationPromptMode.On => ActivationPromptMode.Auto,
                            ActivationPromptMode.Auto => ActivationPromptMode.Off,
                            _ => ActivationPromptMode.On
                        };
                    ShowDuelMenuPage(_duelMenuPage);
                },
                0.13f);
            BuildDuelMenuSettingRow(
                parent,
                "RITMO DAS RESPOSTAS",
                "1×/FASE evita repetição; CLÁSSICO mantém cada janela.",
                DuelActivationPreferences.ResponseWindowRhythmName,
                ArcaneCyan,
                0.49f,
                () =>
                {
                    DuelActivationPreferences.ClassicResponseWindows =
                        !DuelActivationPreferences.ClassicResponseWindows;
                    ShowDuelMenuPage(_duelMenuPage);
                },
                0.13f);
            BuildDuelMenuSettingRow(
                parent,
                "ORIENTAÇÕES EM CAMPO",
                "Mostra ou oculta a faixa informativa superior.",
                DuelActivationPreferences.GuidanceMessagesEnabled
                    ? "VISÍVEIS" : "OCULTAS",
                OptionsMint,
                0.34f,
                () =>
                {
                    DuelActivationPreferences.GuidanceMessagesEnabled =
                        !DuelActivationPreferences.GuidanceMessagesEnabled;
                    ShowDuelMenuPage(_duelMenuPage);
                },
                0.13f);
            BuildDuelMenuSettingRow(
                parent,
                "PAINEL DE CORRENTE",
                "Mostra ou oculta o resumo vermelho da corrente.",
                DuelActivationPreferences.ChainPanelEnabled
                    ? "VISÍVEL" : "OCULTO",
                OptionsAmber,
                0.19f,
                () =>
                {
                    DuelActivationPreferences.ChainPanelEnabled =
                        !DuelActivationPreferences.ChainPanelEnabled;
                    ShowDuelMenuPage(_duelMenuPage);
                },
                0.13f);
            BuildDuelMenuSettingRow(
                parent,
                "SELF CHAIN / ORDEM",
                "Alterna resposta própria e ordem manual em conjunto.",
                DuelActivationPreferences.SelfChainEnabled &&
                DuelActivationPreferences.ManualChainOrder
                    ? "MANUAL" : "SIMPLIFICADO",
                ArcaneCyan,
                0.04f,
                () =>
                {
                    bool enable =
                        !(DuelActivationPreferences.SelfChainEnabled &&
                          DuelActivationPreferences.ManualChainOrder);
                    DuelActivationPreferences.SelfChainEnabled = enable;
                    DuelActivationPreferences.ManualChainOrder = enable;
                    ShowDuelMenuPage(_duelMenuPage);
                },
                0.13f);
        }

        private void BuildDuelMenuHelp(Transform parent)
        {
            BuildDuelMenuPageHeader(
                parent,
                "AJUDA RÁPIDA",
                "Referência curta sem abandonar a partida.");

            BuildDuelMenuHelpCard(
                parent,
                "1  •  ESCOLHA",
                "Selecione uma carta da mão ou do campo. A interface exibirá " +
                "somente ações que o Core considera legais.",
                ArcaneCyan,
                0.67f);
            BuildDuelMenuHelpCard(
                parent,
                "2  •  FASES",
                "Toque no controle de fase para abrir Compra, Apoio, Principal, " +
                "Batalha e Final. Fases indisponíveis permanecem bloqueadas.",
                OptionsMint,
                0.43f);
            BuildDuelMenuHelpCard(
                parent,
                "3  •  PRIORIDADE",
                "Responda às janelas obrigatórias antes de continuar. Em partidas " +
                "online, aguarde a confirmação do host após cada escolha.",
                OptionsAmber,
                0.19f);
        }

        private void BuildDuelMenuExitConfirmation(Transform parent)
        {
            BuildDuelMenuPageHeader(
                parent,
                StoryRogueliteRuntime.IsStoryDuel
                    ? "ABANDONAR DUELO DA HISTÓRIA?"
                    : "SAIR DO DUELO?",
                "Esta é a única ação deste menu que encerra a partida atual.");

            Image warning = CreateArcaneSurface(
                parent,
                "Confirmação de Saída",
                new Vector2(0.08f, 0.38f),
                new Vector2(0.92f, 0.72f),
                Danger,
                true,
                0.84f);
            CreateText(
                warning.transform,
                DuelOnlineSession.Instance?.IsOnlineDuelActive == true
                    ? "A conexão com a sala será encerrada. O adversário poderá " +
                      "receber a vitória conforme as regras da sessão."
                    : "O progresso deste duelo não poderá ser retomado.",
                18,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.22f),
                new Vector2(0.92f, 0.82f),
                TextAnchor.MiddleCenter);

            CreateArcaneActionButton(
                parent,
                "CANCELAR",
                new Vector2(0.08f, 0.16f),
                new Vector2(0.48f, 0.29f),
                ArcaneCyan,
                () => ShowDuelMenuPage(DuelMenuPage.Overview),
                17);
            CreateArcaneActionButton(
                parent,
                "CONFIRMAR SAÍDA",
                new Vector2(0.52f, 0.16f),
                new Vector2(0.92f, 0.29f),
                Danger,
                () =>
                {
                    CloseDuelMenu();
                    ExitDuelPresentationToMenu();
                },
                17);
        }

        private static void BuildDuelMenuPageHeader(
            Transform parent,
            string title,
            string subtitle)
        {
            CreateText(
                parent,
                title,
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.045f, 0.86f),
                new Vector2(0.72f, 0.96f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                subtitle,
                13,
                FontStyle.Normal,
                new Color(0.65f, 0.73f, 0.82f, 1f),
                new Vector2(0.045f, 0.79f),
                new Vector2(0.955f, 0.87f),
                TextAnchor.MiddleLeft);
        }

        private static void BuildDuelMenuInfoCard(
            Transform parent,
            string caption,
            string value,
            Color accent,
            Vector2 min,
            Vector2 max)
        {
            Image card = CreateArcaneSurface(
                parent,
                caption,
                min,
                max,
                accent,
                false,
                0.72f);
            CreateText(
                card.transform,
                caption,
                11,
                FontStyle.Bold,
                new Color(0.66f, 0.74f, 0.82f, 1f),
                new Vector2(0.07f, 0.57f),
                new Vector2(0.93f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                value,
                18,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.12f),
                new Vector2(0.93f, 0.59f),
                TextAnchor.MiddleLeft);
        }

        private static void BuildDuelMenuSettingRow(
            Transform parent,
            string label,
            string description,
            string value,
            Color accent,
            float yMin,
            Action action,
            float height = 0.145f)
        {
            Image row = CreateArcaneSurface(
                parent,
                label,
                new Vector2(0.045f, yMin),
                new Vector2(0.955f, yMin + height),
                accent,
                false,
                0.68f);
            AddButtonBehaviour(row, action);
            Button button = row.GetComponent<Button>();
            ArcanePanelSheenGraphic sheen =
                row.GetComponentInChildren<ArcanePanelSheenGraphic>();
            if (button != null && sheen != null)
                button.targetGraphic = sheen;
            CreateText(
                row.transform,
                label,
                15,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.48f),
                new Vector2(0.55f, 0.88f),
                TextAnchor.MiddleLeft);
            CreateText(
                row.transform,
                description,
                12,
                FontStyle.Normal,
                new Color(0.66f, 0.74f, 0.82f, 1f),
                new Vector2(0.035f, 0.12f),
                new Vector2(0.70f, 0.50f),
                TextAnchor.MiddleLeft);
            CreateText(
                row.transform,
                value,
                16,
                FontStyle.Bold,
                accent,
                new Vector2(0.70f, 0.17f),
                new Vector2(0.955f, 0.83f),
                TextAnchor.MiddleRight);
        }

        private static void BuildDuelMenuHelpCard(
            Transform parent,
            string title,
            string body,
            Color accent,
            float yMin)
        {
            Image card = CreateArcaneSurface(
                parent,
                title,
                new Vector2(0.045f, yMin),
                new Vector2(0.955f, yMin + 0.20f),
                accent,
                false,
                0.68f);
            CreateText(
                card.transform,
                title,
                15,
                FontStyle.Bold,
                accent,
                new Vector2(0.04f, 0.58f),
                new Vector2(0.96f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                body,
                13,
                FontStyle.Normal,
                Color.white,
                new Vector2(0.04f, 0.12f),
                new Vector2(0.96f, 0.62f),
                TextAnchor.UpperLeft);
        }

        private static string DuelMenuSessionCaption()
        {
            DuelOnlineSession session = DuelOnlineSession.Instance;
            if (session?.IsOnlineDuelActive == true)
            {
                string room = string.IsNullOrWhiteSpace(session.RoomCode)
                    ? string.Empty
                    : $"  •  SALA {session.RoomCode}";
                return $"ONLINE  •  {(session.IsHost ? "HOST" : "JOGADOR 2")}{room}";
            }
            return StoryRogueliteRuntime.IsStoryDuel
                ? "HISTÓRIA  •  DUELO ATIVO"
                : "LOCAL  •  DUELO ATIVO";
        }

        private static string DuelMenuConnectionDescription()
        {
            return DuelOnlineSession.Instance?.IsOnlineDuelActive == true
                ? "A conexão permanece ativa enquanto este painel está aberto."
                : "A partida fica preservada até você continuar ou confirmar a saída.";
        }
    }
}
