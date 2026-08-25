using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcaneDuel.Game.Competitive;
using ArcaneDuel.Game.Social;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private enum FriendsPage
        {
            Connections,
            Requests,
            Duels,
            Add
        }

        private FriendsPage _friendsPage = FriendsPage.Add;
        private string _friendSearchQuery = string.Empty;
        private FriendProfileView _friendSearchResult;
        private string _friendsFeedback = string.Empty;
        private bool _friendsFeedbackIsError;
        private bool _friendsUiOperationInProgress;
        private bool _friendsAutomaticRefreshRunning;
        private GameObject _friendsUiRoot;
        private Button _mainMenuFriendsButton;
        private GameObject _friendDuelModeModal;

        private void OpenPlayerSearchFromBell()
        {
            _friendsPage = FriendDuelChallengeRuntime.IncomingCount > 0
                ? FriendsPage.Duels
                : FriendsPage.Add;
            _friendSearchResult = null;
            _friendsFeedback = string.Empty;
            _friendsFeedbackIsError = false;
            ShowFriendsHubScreen();
            _ = RefreshFriendsHubProfilesAsync();
        }

        private async Task RefreshFriendsHubProfilesAsync()
        {
            if (_friendsAutomaticRefreshRunning)
                return;
            _friendsAutomaticRefreshRunning = true;
            try
            {
                await PlayerFriendsRuntime.RefreshPublicProfilesAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Amigos] O catálogo visual será consultado novamente: " +
                    exception.GetBaseException().Message);
            }
            finally
            {
                _friendsAutomaticRefreshRunning = false;
            }
        }

        private void ShowFriendsHubScreen()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("CENTRAL DE CONEXÕES");
            BuildHeader("AMIGOS", ShowMainMenu);

            Image stage = CreatePanel(
                _screenRoot,
                "Tela Independente de Amigos",
                new Vector2(0.105f, 0.085f),
                new Vector2(0.895f, 0.85f),
                Color.clear);
            BuildFriendsHub(stage.transform);
        }

        private void BuildFriendsHub(Transform parent)
        {
            Image hub = CreatePanel(
                parent,
                "Central de Conexões",
                Vector2.zero,
                Vector2.one,
                Color.clear);
            _friendsUiRoot = hub.gameObject;

            BuildFriendsHeader(hub.transform);

            Image navigation = CreateArcaneSurface(
                hub.transform,
                "Navegação da Central de Jogadores",
                new Vector2(0f, 0f),
                new Vector2(0.245f, 0.805f),
                ArcaneCyan,
                false,
                0.78f);
            BuildFriendsNavigation(navigation.transform);

            Image body = CreateArcaneSurface(
                hub.transform,
                "Conteúdo da Central de Jogadores",
                new Vector2(0.265f, 0f),
                new Vector2(1f, 0.805f),
                ArcaneCyan,
                true,
                0.86f);

            if (_friendsPage == FriendsPage.Connections)
                BuildFriendsList(body.transform);
            else if (_friendsPage == FriendsPage.Requests)
                BuildFriendRequests(body.transform);
            else if (_friendsPage == FriendsPage.Duels)
                BuildFriendDuelChallenges(body.transform);
            else
                BuildFriendSearch(body.transform);

            if (_friendsPage != FriendsPage.Add &&
                !string.IsNullOrWhiteSpace(_friendsFeedback))
            {
                CreateText(
                    body.transform,
                    _friendsFeedback,
                    10,
                    FontStyle.Bold,
                    _friendsFeedbackIsError ? Danger : ArcaneCyan,
                    new Vector2(0.04f, 0.005f),
                    new Vector2(0.96f, 0.05f),
                    TextAnchor.MiddleCenter);
            }
        }

        private void BuildFriendsHeader(Transform parent)
        {
            Image heading = CreateArcaneSurface(
                parent,
                "Central de Jogadores",
                new Vector2(0f, 0.83f),
                new Vector2(1f, 1f),
                ArcaneCyan,
                true,
                0.92f);

            CreatePanel(
                heading.transform,
                "Marcador da Central",
                new Vector2(0.024f, 0.15f),
                new Vector2(0.032f, 0.85f),
                ArcaneCyan).raycastTarget = false;
            CreateText(
                heading.transform,
                "CENTRAL DE JOGADORES",
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.052f, 0.47f),
                new Vector2(0.54f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                heading.transform,
                "ENCONTRE CONTAS PELO NOME OU ID E ORGANIZE SUAS CONEXÕES",
                12,
                FontStyle.Bold,
                Muted,
                new Vector2(0.052f, 0.11f),
                new Vector2(0.65f, 0.48f),
                TextAnchor.MiddleLeft);

            CreateFriendsMetric(
                heading.transform,
                "AMIGOS",
                PlayerFriendsRuntime.FriendCount.ToString(),
                new Vector2(0.665f, 0.15f),
                new Vector2(0.755f, 0.85f),
                ArcaneCyan);
            CreateFriendsMetric(
                heading.transform,
                "PEDIDOS",
                PlayerFriendsRuntime.IncomingCount.ToString(),
                new Vector2(0.77f, 0.15f),
                new Vector2(0.86f, 0.85f),
                PlayerFriendsRuntime.IncomingCount > 0
                    ? ArcaneGold
                    : ArcaneCyan);
            CreateFriendsMetric(
                heading.transform,
                "DUELOS",
                FriendDuelChallengeRuntime.IncomingCount.ToString(),
                new Vector2(0.875f, 0.15f),
                new Vector2(0.965f, 0.85f),
                FriendDuelChallengeRuntime.IncomingCount > 0
                    ? Lime
                    : ArcaneCyan);
        }

        private static void CreateFriendsMetric(
            Transform parent,
            string label,
            string value,
            Vector2 min,
            Vector2 max,
            Color accent)
        {
            Image metric = CreateArcaneSurface(
                parent,
                "Métrica " + label,
                min,
                max,
                accent,
                true,
                0.72f);
            CreateText(
                metric.transform,
                value,
                20,
                FontStyle.Bold,
                accent,
                new Vector2(0.06f, 0.34f),
                new Vector2(0.94f, 0.94f),
                TextAnchor.MiddleCenter);
            CreateText(
                metric.transform,
                label,
                9,
                FontStyle.Bold,
                Muted,
                new Vector2(0.06f, 0.06f),
                new Vector2(0.94f, 0.38f),
                TextAnchor.MiddleCenter);
        }

        private void BuildFriendsNavigation(Transform parent)
        {
            CreateText(
                parent,
                "SOCIAL",
                12,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.08f, 0.875f),
                new Vector2(0.92f, 0.965f),
                TextAnchor.MiddleLeft);
            CreateFriendsNavigationButton(
                parent,
                "+  ADICIONAR JOGADOR",
                FriendsPage.Add,
                0);
            CreateFriendsNavigationButton(
                parent,
                PlayerFriendsRuntime.IncomingCount > 0
                    ? $"PEDIDOS  •  {PlayerFriendsRuntime.IncomingCount}"
                    : "PEDIDOS",
                FriendsPage.Requests,
                1);
            CreateFriendsNavigationButton(
                parent,
                PlayerFriendsRuntime.FriendCount > 0
                    ? $"AMIGOS  •  {PlayerFriendsRuntime.FriendCount}"
                    : "AMIGOS",
                FriendsPage.Connections,
                2);
            CreateFriendsNavigationButton(
                parent,
                FriendDuelChallengeRuntime.IncomingCount > 0
                    ? $"DUELOS  •  {FriendDuelChallengeRuntime.IncomingCount}"
                    : "DUELOS",
                FriendsPage.Duels,
                3);

            CreatePanel(
                parent,
                "Separador da Identidade",
                new Vector2(0.08f, 0.315f),
                new Vector2(0.92f, 0.32f),
                new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.30f))
                .raycastTarget = false;
            CreateText(
                parent,
                "SEU ID DE JOGADOR",
                10,
                FontStyle.Bold,
                Muted,
                new Vector2(0.08f, 0.245f),
                new Vector2(0.92f, 0.305f),
                TextAnchor.MiddleLeft);
            string ownId = PlayerIdAccessRuntime.PublicPlayerId;
            CreateText(
                parent,
                string.IsNullOrWhiteSpace(ownId) ? "PREPARANDO..." : ownId,
                15,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.08f, 0.165f),
                new Vector2(0.92f, 0.245f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                "Compartilhe este número para que outros jogadores encontrem você.",
                10,
                FontStyle.Normal,
                Muted,
                new Vector2(0.08f, 0.035f),
                new Vector2(0.92f, 0.155f),
                TextAnchor.UpperLeft);
        }

        private void CreateFriendsNavigationButton(
            Transform parent,
            string label,
            FriendsPage page,
            int index)
        {
            bool active = _friendsPage == page;
            float top = 0.85f - index * 0.125f;
            float bottom = top - 0.10f;
            Color accent = active ? ArcaneGold : ArcaneCyan;
            Image button = CreateArcaneSurface(
                parent,
                "Aba Social " + label,
                new Vector2(0.065f, bottom),
                new Vector2(0.935f, top),
                accent,
                active,
                active ? 0.88f : 0.48f);
            AddButtonBehaviour(button, () =>
            {
                _friendsPage = page;
                _friendsFeedback = string.Empty;
                ShowFriendsHubScreen();
            });
            CreateText(
                button.transform,
                label,
                13,
                FontStyle.Bold,
                active ? new Color(0.98f, 0.89f, 0.72f, 1f) : Color.white,
                new Vector2(0.09f, 0.08f),
                new Vector2(0.91f, 0.92f),
                TextAnchor.MiddleLeft);
            if (active)
            {
                CreatePanel(
                    button.transform,
                    "Seleção da Navegação",
                    new Vector2(0.015f, 0.16f),
                    new Vector2(0.035f, 0.84f),
                    ArcaneGold).raycastTarget = false;
            }
        }

        private void BuildFriendSearch(Transform parent)
        {
            CreateText(
                parent,
                "PROCURAR JOGADOR",
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.045f, 0.865f),
                new Vector2(0.55f, 0.965f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                "BUSCA EXATA POR NOME OU ID NUMÉRICO",
                10,
                FontStyle.Normal,
                Muted,
                new Vector2(0.52f, 0.865f),
                new Vector2(0.955f, 0.965f),
                TextAnchor.MiddleRight);

            InputField input = CreateFriendSearchField(
                parent,
                "Digite o nome completo ou o ID de 12 números",
                new Vector2(0.045f, 0.69f),
                new Vector2(0.745f, 0.835f));
            input.characterLimit = 50;
            input.text = _friendSearchQuery;
            input.onValueChanged.AddListener(value =>
                _friendSearchQuery = value);
            Image search = CreateArcaneActionButton(
                parent,
                PlayerFriendsRuntime.IsBusy || _friendsUiOperationInProgress
                    ? "BUSCANDO..."
                    : "BUSCAR",
                new Vector2(0.77f, 0.69f),
                new Vector2(0.955f, 0.835f),
                ArcaneCyan,
                SearchFriendFromUi,
                14);
            search.GetComponent<Button>().interactable =
                !PlayerFriendsRuntime.IsBusy && !_friendsUiOperationInProgress;

            if (!string.IsNullOrWhiteSpace(_friendsFeedback))
            {
                CreateText(
                    parent,
                    _friendsFeedback,
                    11,
                    FontStyle.Bold,
                    _friendsFeedbackIsError ? Danger : ArcaneCyan,
                    new Vector2(0.045f, 0.61f),
                    new Vector2(0.955f, 0.685f),
                    TextAnchor.MiddleLeft);
            }

            if (_friendSearchResult != null)
            {
                CreateFriendCard(
                    parent,
                    _friendSearchResult,
                    new Vector2(0.045f, 0.12f),
                    new Vector2(0.955f, 0.59f),
                    true);
            }
            else
            {
                BuildFriendsEmptyState(
                    parent,
                    "BUSQUE UMA NOVA CONEXÃO",
                    "O ID funciona como uma assinatura única. O nome deixa a busca mais natural.",
                    new Vector2(0.045f, 0.11f),
                    new Vector2(0.955f, 0.58f));
            }

            CreateText(
                parent,
                PlayerFriendsRuntime.Status,
                10,
                FontStyle.Normal,
                new Color(Muted.r, Muted.g, Muted.b, 0.82f),
                new Vector2(0.045f, 0.025f),
                new Vector2(0.955f, 0.095f),
                TextAnchor.MiddleLeft);
        }

        private static InputField CreateFriendSearchField(
            Transform parent,
            string placeholder,
            Vector2 min,
            Vector2 max)
        {
            Image background = CreateArcaneSurface(
                parent,
                "Campo de Busca de Jogadores",
                min,
                max,
                ArcaneCyan,
                true,
                0.66f);
            CreatePanel(
                background.transform,
                "Indicador do Campo",
                new Vector2(0.012f, 0.15f),
                new Vector2(0.019f, 0.85f),
                ArcaneCyan).raycastTarget = false;
            CreateText(
                background.transform,
                "⌕",
                27,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.025f, 0.06f),
                new Vector2(0.12f, 0.94f),
                TextAnchor.MiddleCenter);
            Text inputText = CreateText(
                background.transform,
                string.Empty,
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.13f, 0.10f),
                new Vector2(0.96f, 0.90f),
                TextAnchor.MiddleLeft);
            Text placeholderText = CreateText(
                background.transform,
                placeholder,
                14,
                FontStyle.Normal,
                new Color(Muted.r, Muted.g, Muted.b, 0.86f),
                new Vector2(0.13f, 0.10f),
                new Vector2(0.96f, 0.90f),
                TextAnchor.MiddleLeft);

            InputField input = background.gameObject.AddComponent<InputField>();
            ArcanePanelSheenGraphic sheen =
                background.GetComponentInChildren<ArcanePanelSheenGraphic>();
            input.targetGraphic = sheen != null ? sheen : background;
            input.textComponent = inputText;
            input.placeholder = placeholderText;
            input.lineType = InputField.LineType.SingleLine;
            input.selectionColor = new Color(
                ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.42f);
            return input;
        }

        private void BuildFriendsList(Transform parent)
        {
            IReadOnlyList<FriendProfileView> friends =
                PlayerFriendsRuntime.Friends;
            BuildFriendsSectionHeading(
                parent,
                "SUAS CONEXÕES",
                friends.Count == 1 ? "1 DUELISTA" : $"{friends.Count} DUELISTAS");
            if (friends.Count == 0)
            {
                BuildFriendsEmptyState(
                    parent,
                    "SEU NEXO ESTÁ SILENCIOSO",
                    "Procure um jogador pelo nome ou ID e envie a primeira solicitação.",
                    new Vector2(0.04f, 0.14f),
                    new Vector2(0.96f, 0.80f));
                Image add = CreateButton(
                    parent,
                    "ENCONTRAR JOGADORES",
                    new Vector2(0.31f, 0.07f),
                    new Vector2(0.69f, 0.18f),
                    ArcaneCyan,
                    () =>
                    {
                        _friendsPage = FriendsPage.Add;
                        ShowFriendsHubScreen();
                    });
                SetButtonTextSize(add, 13);
                return;
            }

            RectTransform content = CreateFriendsScrollList(parent);
            foreach (FriendProfileView friend in friends)
                CreateFriendListRow(content, friend, false);
        }

        private void BuildFriendRequests(Transform parent)
        {
            IReadOnlyList<FriendProfileView> incoming =
                PlayerFriendsRuntime.IncomingRequests;
            IReadOnlyList<FriendProfileView> outgoing =
                PlayerFriendsRuntime.OutgoingRequests;
            int total = incoming.Count + outgoing.Count;
            BuildFriendsSectionHeading(
                parent,
                "PEDIDOS DE CONEXÃO",
                total == 1 ? "1 PENDENTE" : $"{total} PENDENTES");
            if (total == 0)
            {
                BuildFriendsEmptyState(
                    parent,
                    "NENHUM PEDIDO PENDENTE",
                    "Quando alguém procurar seu nome ou ID, a solicitação aparecerá aqui.",
                    new Vector2(0.04f, 0.12f),
                    new Vector2(0.96f, 0.82f));
                return;
            }

            RectTransform content = CreateFriendsScrollList(parent);
            foreach (FriendProfileView request in incoming)
                CreateFriendListRow(content, request, true);
            foreach (FriendProfileView request in outgoing)
                CreateFriendListRow(content, request, true);
        }

        private void BuildFriendDuelChallenges(Transform parent)
        {
            FriendDuelChallengeView incoming =
                FriendDuelChallengeRuntime.Incoming;
            FriendDuelChallengeView outgoing =
                FriendDuelChallengeRuntime.Outgoing;
            int total = (incoming != null ? 1 : 0) +
                        (outgoing != null ? 1 : 0);
            BuildFriendsSectionHeading(
                parent,
                "DESAFIOS PRIVADOS",
                total == 1 ? "1 ATIVO" : $"{total} ATIVOS");

            if (total == 0)
            {
                BuildFriendsEmptyState(
                    parent,
                    "NENHUM DESAFIO ATIVO",
                    "Abra a lista de amigos, escolha um duelista e toque em DESAFIAR.",
                    new Vector2(0.04f, 0.18f),
                    new Vector2(0.96f, 0.82f));
                CreateText(
                    parent,
                    FriendDuelChallengeRuntime.Status,
                    10,
                    FontStyle.Bold,
                    Muted,
                    new Vector2(0.06f, 0.06f),
                    new Vector2(0.94f, 0.16f),
                    TextAnchor.MiddleCenter);
                return;
            }

            RectTransform content = CreateFriendsScrollList(parent);
            if (incoming != null)
                CreateFriendDuelChallengeRow(content, incoming, true);
            if (outgoing != null)
                CreateFriendDuelChallengeRow(content, outgoing, false);
        }

        private void CreateFriendDuelChallengeRow(
            Transform parent,
            FriendDuelChallengeView challenge,
            bool incoming)
        {
            FriendDuelMode mode = challenge.Mode;
            Color accent = mode == FriendDuelMode.Ranked
                ? ArcaneGold
                : ArcaneCyan;
            string displayName = incoming
                ? challenge.senderDisplayName
                : challenge.recipientDisplayName;
            string publicId = incoming
                ? challenge.senderPublicId
                : challenge.recipientPublicId;
            string iconId = incoming
                ? challenge.senderIconId
                : challenge.recipientIconId;

            Image row = CreateProfileSurface(
                parent,
                "Desafio com " + displayName,
                Vector2.zero,
                Vector2.one,
                accent,
                new Color(0.005f, 0.027f, 0.050f, 0.98f),
                0.64f);
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 132f;
            layout.preferredHeight = 132f;

            CreateBoundedHexIcon(
                row.transform,
                "Emblema do desafiante",
                string.IsNullOrWhiteSpace(iconId)
                    ? ProfileIconCatalog.DefaultIconId
                    : iconId,
                new Vector2(0.025f, 0.13f),
                new Vector2(0.145f, 0.87f));
            CreateText(
                row.transform,
                string.IsNullOrWhiteSpace(displayName)
                    ? "DUELISTA"
                    : displayName,
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.17f, 0.57f),
                new Vector2(0.55f, 0.88f),
                TextAnchor.MiddleLeft);
            CreateText(
                row.transform,
                string.IsNullOrWhiteSpace(publicId)
                    ? "ID PROTEGIDO"
                    : "ID  " + publicId,
                10,
                FontStyle.Bold,
                Muted,
                new Vector2(0.17f, 0.39f),
                new Vector2(0.55f, 0.58f),
                TextAnchor.MiddleLeft);
            CreateText(
                row.transform,
                FriendDuelChallengePolicy.ModeLabel(mode) +
                (mode == FriendDuelMode.Ranked
                    ? "  •  RESULTADO ALTERA PE"
                    : "  •  SEM ALTERAÇÃO DE PE"),
                10,
                FontStyle.Bold,
                accent,
                new Vector2(0.17f, 0.15f),
                new Vector2(0.67f, 0.38f),
                TextAnchor.MiddleLeft);

            string state = ChallengeStatusLabel(challenge, incoming);
            CreateText(
                row.transform,
                state,
                10,
                FontStyle.Bold,
                challenge.Status == FriendDuelChallengeStatus.Pending
                    ? Lime
                    : accent,
                new Vector2(0.57f, 0.62f),
                new Vector2(0.965f, 0.90f),
                TextAnchor.MiddleCenter);

            if (incoming &&
                challenge.Status == FriendDuelChallengeStatus.Pending)
            {
                CreateCompactFriendsButton(
                    row.transform,
                    "ACEITAR",
                    new Vector2(0.70f, 0.31f),
                    new Vector2(0.825f, 0.59f),
                    Lime,
                    () => AcceptFriendDuelFromUi(challenge));
                CreateCompactFriendsButton(
                    row.transform,
                    "RECUSAR",
                    new Vector2(0.84f, 0.31f),
                    new Vector2(0.965f, 0.59f),
                    Danger,
                    () => DeclineFriendDuelFromUi(challenge));
            }
            else
            {
                CreateCompactFriendsButton(
                    row.transform,
                    "CANCELAR",
                    new Vector2(0.72f, 0.25f),
                    new Vector2(0.965f, 0.57f),
                    Danger,
                    () => CancelFriendDuelFromUi(challenge));
            }
        }

        private static string ChallengeStatusLabel(
            FriendDuelChallengeView challenge,
            bool incoming)
        {
            return challenge.Status switch
            {
                FriendDuelChallengeStatus.Pending => incoming
                    ? "DESAFIO RECEBIDO"
                    : "AGUARDANDO RESPOSTA",
                FriendDuelChallengeStatus.Accepted => incoming
                    ? "AGUARDANDO A SALA"
                    : "CRIANDO SALA PRIVADA",
                FriendDuelChallengeStatus.Ready => incoming
                    ? "ENTRANDO NA SALA"
                    : "AGUARDANDO O AMIGO",
                _ => "SINCRONIZANDO"
            };
        }

        private static void BuildFriendsSectionHeading(
            Transform parent,
            string title,
            string count)
        {
            CreateText(parent, title, 16, FontStyle.Bold, Color.white,
                new Vector2(0.04f, 0.87f), new Vector2(0.64f, 0.97f),
                TextAnchor.MiddleLeft);
            CreateText(parent, count, 11, FontStyle.Bold, Muted,
                new Vector2(0.65f, 0.87f), new Vector2(0.96f, 0.97f),
                TextAnchor.MiddleRight);
        }

        private static RectTransform CreateFriendsScrollList(Transform parent)
        {
            Image viewport = CreatePanel(
                parent,
                "Lista de Conexões",
                new Vector2(0.035f, 0.055f),
                new Vector2(0.965f, 0.86f),
                Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();

            GameObject contentObject = new(
                "Conteúdo das Conexões",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(8f, 0f);
            content.offsetMax = new Vector2(-24f, 0f);

            VerticalLayoutGroup layout =
                contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(4, 4, 4, 12);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            // As linhas possuem LayoutElement com uma altura preferencial.
            // Se o grupo não controlar a altura, os RectTransforms esticados
            // nascem com zero pixel dentro do ContentSizeFitter e pedidos
            // válidos ficam invisíveis apesar de o contador estar correto.
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 55f;
            return content;
        }

        private void CreateFriendListRow(
            Transform parent,
            FriendProfileView profile,
            bool request)
        {
            Image row = CreateFriendCard(
                parent,
                profile,
                Vector2.zero,
                Vector2.one,
                false);
            LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = request ? 118f : 104f;
            size.preferredHeight = request ? 118f : 104f;
        }

        private Image CreateFriendCard(
            Transform parent,
            FriendProfileView profile,
            Vector2 min,
            Vector2 max,
            bool expanded)
        {
            Color accent = PresenceColor(profile.presence);
            if (profile.connectionState == FriendConnectionState.IncomingRequest)
                accent = ArcaneGold;
            else if (profile.connectionState == FriendConnectionState.OutgoingRequest)
                accent = ArcaneCyan;

            Image card = CreateProfileSurface(
                parent,
                "Duelista " + profile.displayName,
                min,
                max,
                accent,
                new Color(0.005f, 0.027f, 0.050f, 0.98f),
                0.58f);
            CreateBoundedHexIcon(
                card.transform,
                "Emblema de " + profile.displayName,
                string.IsNullOrWhiteSpace(profile.equippedIconId)
                    ? ProfileIconCatalog.DefaultIconId
                    : profile.equippedIconId,
                new Vector2(0.025f, expanded ? 0.20f : 0.16f),
                new Vector2(expanded ? 0.20f : 0.145f,
                    expanded ? 0.84f : 0.84f));

            float textMin = expanded ? 0.23f : 0.17f;
            CreateText(
                card.transform,
                string.IsNullOrWhiteSpace(profile.displayName)
                    ? "DUELISTA"
                    : profile.displayName,
                expanded ? 21 : 16,
                FontStyle.Bold,
                Color.white,
                new Vector2(textMin, 0.55f),
                new Vector2(0.67f, 0.88f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                string.IsNullOrWhiteSpace(profile.publicId)
                    ? "ID PROTEGIDO"
                    : "ID  " + profile.publicId,
                expanded ? 13 : 11,
                FontStyle.Bold,
                Muted,
                new Vector2(textMin, 0.32f),
                new Vector2(0.67f, 0.57f),
                TextAnchor.MiddleLeft);
            if (expanded)
                BuildExpandedFriendProfile(card.transform, profile, textMin);
            CreateText(
                card.transform,
                ConnectionLabel(profile),
                expanded ? 12 : 10,
                FontStyle.Bold,
                accent,
                expanded
                    ? new Vector2(0.72f, 0.72f)
                    : new Vector2(textMin, 0.10f),
                expanded
                    ? new Vector2(0.965f, 0.90f)
                    : new Vector2(0.67f, 0.34f),
                expanded
                    ? TextAnchor.MiddleCenter
                    : TextAnchor.MiddleLeft);

            BuildFriendCardActions(card.transform, profile);
            return card;
        }

        private static void BuildExpandedFriendProfile(
            Transform parent,
            FriendProfileView profile,
            float textMin)
        {
            string iconTitle = ProfileIconCatalog.Resolve(
                profile.equippedIconId)?.DisplayName ?? "Brasão Arcano";
            string rank = RankRules.DisplayName(profile.rankTier);
            long decided = Math.Max(0, profile.wins) +
                           Math.Max(0, profile.losses);
            double winRate = decided > 0
                ? Math.Max(0, profile.wins) * 100.0 / decided
                : 0.0;

            CreateText(
                parent,
                $"{rank}  •  {Math.Max(0, profile.rankedPoints)} PE",
                12,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(textMin, 0.22f),
                new Vector2(0.48f, 0.36f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                $"{Math.Max(0, profile.duelsPlayed)} DUELOS  •  " +
                $"{Math.Max(0, profile.wins)} V  •  " +
                $"{Math.Max(0, profile.losses)} D  •  {winRate:0.#}%",
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.49f, 0.22f),
                new Vector2(0.70f, 0.36f),
                TextAnchor.MiddleRight);
            CreateText(
                parent,
                iconTitle.ToUpperInvariant(),
                9,
                FontStyle.Bold,
                new Color(Muted.r, Muted.g, Muted.b, 0.86f),
                new Vector2(textMin, 0.08f),
                new Vector2(0.70f, 0.20f),
                TextAnchor.MiddleLeft);
        }

        private void BuildFriendCardActions(
            Transform parent,
            FriendProfileView profile)
        {
            if (profile.connectionState == FriendConnectionState.None)
            {
                CreateCompactFriendsButton(
                    parent, "ADICIONAR", new Vector2(0.72f, 0.30f),
                    new Vector2(0.965f, 0.70f), ArcaneCyan,
                    () => SendFriendRequestFromUi(profile));
            }
            else if (profile.connectionState == FriendConnectionState.IncomingRequest)
            {
                CreateCompactFriendsButton(
                    parent, "ACEITAR", new Vector2(0.70f, 0.53f),
                    new Vector2(0.965f, 0.88f), Lime,
                    () => AcceptFriendFromUi(profile));
                CreateCompactFriendsButton(
                    parent, "IGNORAR", new Vector2(0.70f, 0.12f),
                    new Vector2(0.965f, 0.45f), Muted,
                    () => IgnoreFriendFromUi(profile));
            }
            else if (profile.connectionState == FriendConnectionState.OutgoingRequest)
            {
                CreateCompactFriendsButton(
                    parent, "CANCELAR", new Vector2(0.72f, 0.30f),
                    new Vector2(0.965f, 0.70f), Muted,
                    () => CancelFriendRequestFromUi(profile));
            }
            else if (profile.connectionState == FriendConnectionState.Friend)
            {
                CreateCompactFriendsButton(
                    parent, "DESAFIAR", new Vector2(0.72f, 0.53f),
                    new Vector2(0.965f, 0.88f), Lime,
                    () => ShowFriendDuelModeChooser(profile));
                CreateCompactFriendsButton(
                    parent, "REMOVER", new Vector2(0.72f, 0.12f),
                    new Vector2(0.965f, 0.45f), Danger,
                    () => RemoveFriendFromUi(profile));
            }
        }

        private void ShowFriendDuelModeChooser(FriendProfileView profile)
        {
            if (profile == null || _screenRoot == null)
                return;
            CloseFriendDuelModeChooser();

            Image veil = CreatePanel(
                _screenRoot,
                "Escolha do tipo de desafio",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.88f));
            veil.raycastTarget = true;
            veil.transform.SetAsLastSibling();
            _friendDuelModeModal = veil.gameObject;

            Image shell = CreateArcaneSurface(
                veil.transform,
                "Terminal de Desafio Privado",
                new Vector2(0.20f, 0.15f),
                new Vector2(0.80f, 0.85f),
                ArcaneCyan,
                true,
                0.96f);
            CreatePanel(
                shell.transform,
                "Linha de energia",
                new Vector2(0.04f, 0.89f),
                new Vector2(0.96f, 0.902f),
                new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.72f))
                .raycastTarget = false;
            CreateText(
                shell.transform,
                "DESAFIO DE DUELO",
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.82f),
                new Vector2(0.92f, 0.94f),
                TextAnchor.MiddleCenter);
            CreateText(
                shell.transform,
                $"CONVIDAR  {profile.displayName}  •  ID {profile.publicId}",
                12,
                FontStyle.Bold,
                Muted,
                new Vector2(0.08f, 0.75f),
                new Vector2(0.92f, 0.83f),
                TextAnchor.MiddleCenter);

            CreateFriendDuelModeCard(
                shell.transform,
                profile,
                FriendDuelMode.Casual,
                "CASUAL",
                "Duelo privado livre",
                "O resultado não altera seus Pontos de Elo.",
                new Vector2(0.07f, 0.30f),
                new Vector2(0.48f, 0.70f),
                ArcaneCyan);
            CreateFriendDuelModeCard(
                shell.transform,
                profile,
                FriendDuelMode.Ranked,
                "RANQUEADO",
                "Confronto competitivo",
                "Vitória e derrota alteram o PE das duas contas.",
                new Vector2(0.52f, 0.30f),
                new Vector2(0.93f, 0.70f),
                ArcaneGold);

            CreateText(
                shell.transform,
                "O convite expira automaticamente se não houver resposta.",
                10,
                FontStyle.Bold,
                Muted,
                new Vector2(0.10f, 0.20f),
                new Vector2(0.90f, 0.29f),
                TextAnchor.MiddleCenter);
            Image cancel = CreateButton(
                shell.transform,
                "VOLTAR",
                new Vector2(0.35f, 0.07f),
                new Vector2(0.65f, 0.18f),
                Muted,
                CloseFriendDuelModeChooser);
            SetButtonTextSize(cancel, 12);
        }

        private void CreateFriendDuelModeCard(
            Transform parent,
            FriendProfileView profile,
            FriendDuelMode mode,
            string title,
            string subtitle,
            string detail,
            Vector2 min,
            Vector2 max,
            Color accent)
        {
            Image card = CreateArcaneSurface(
                parent,
                "Modo " + title,
                min,
                max,
                accent,
                true,
                0.78f);
            AddButtonBehaviour(
                card,
                () => BeginFriendDuelChallengeFromUi(profile, mode));
            CreateText(
                card.transform,
                mode == FriendDuelMode.Ranked ? "◆" : "◇",
                32,
                FontStyle.Bold,
                accent,
                new Vector2(0.32f, 0.67f),
                new Vector2(0.68f, 0.94f),
                TextAnchor.MiddleCenter);
            CreateText(
                card.transform,
                title,
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.47f),
                new Vector2(0.92f, 0.70f),
                TextAnchor.MiddleCenter);
            CreateText(
                card.transform,
                subtitle.ToUpperInvariant(),
                10,
                FontStyle.Bold,
                accent,
                new Vector2(0.08f, 0.34f),
                new Vector2(0.92f, 0.49f),
                TextAnchor.MiddleCenter);
            CreateText(
                card.transform,
                detail,
                11,
                FontStyle.Normal,
                Muted,
                new Vector2(0.10f, 0.08f),
                new Vector2(0.90f, 0.34f),
                TextAnchor.MiddleCenter);
        }

        private void CloseFriendDuelModeChooser()
        {
            if (_friendDuelModeModal != null)
                Destroy(_friendDuelModeModal);
            _friendDuelModeModal = null;
        }

        private static void CreateCompactFriendsButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action action)
        {
            Image button = CreateButton(parent, label, min, max, accent, action);
            SetButtonTextSize(button, 11);
        }

        private static void SetButtonTextSize(Image button, int size)
        {
            Text label = button != null
                ? button.GetComponentInChildren<Text>(true)
                : null;
            if (label == null)
                return;
            label.fontSize = size;
            label.resizeTextMaxSize = size;
        }

        private static void BuildFriendsEmptyState(
            Transform parent,
            string title,
            string message,
            Vector2 min,
            Vector2 max)
        {
            Image empty = CreateArcaneSurface(
                parent,
                title,
                min,
                max,
                ArcaneCyan,
                false,
                0.52f);
            CreatePanel(empty.transform, "Órbita Esquerda",
                new Vector2(0.10f, 0.49f), new Vector2(0.35f, 0.505f),
                new Color(0.30f, 0.82f, 1f, 0.40f)).raycastTarget = false;
            CreatePanel(empty.transform, "Órbita Direita",
                new Vector2(0.65f, 0.49f), new Vector2(0.90f, 0.505f),
                new Color(ArcaneGold.r, ArcaneGold.g, ArcaneGold.b, 0.40f))
                .raycastTarget = false;
            CreateText(empty.transform, "◇", 34, FontStyle.Bold, ArcaneGold,
                new Vector2(0.42f, 0.57f), new Vector2(0.58f, 0.86f),
                TextAnchor.MiddleCenter);
            CreateText(empty.transform, title, 17, FontStyle.Bold, Color.white,
                new Vector2(0.10f, 0.36f), new Vector2(0.90f, 0.59f),
                TextAnchor.MiddleCenter);
            CreateText(empty.transform, message, 12, FontStyle.Normal, Muted,
                new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.36f),
                TextAnchor.MiddleCenter);
        }

        private static Color PresenceColor(FriendPresenceState presence)
        {
            return presence switch
            {
                FriendPresenceState.Online => Lime,
                FriendPresenceState.Busy => ArcaneGold,
                FriendPresenceState.Away => Gold,
                FriendPresenceState.Offline => Muted,
                _ => ArcaneCyan
            };
        }

        private static string ConnectionLabel(FriendProfileView profile)
        {
            return profile.connectionState switch
            {
                FriendConnectionState.IncomingRequest => "QUER SE CONECTAR COM VOCÊ",
                FriendConnectionState.OutgoingRequest => "SOLICITAÇÃO ENVIADA",
                FriendConnectionState.Friend => profile.presence switch
                {
                    FriendPresenceState.Online => "● ONLINE",
                    FriendPresenceState.Busy => "● OCUPADO",
                    FriendPresenceState.Away => "● AUSENTE",
                    FriendPresenceState.Offline => "● OFFLINE",
                    _ => "CONEXÃO CONFIRMADA"
                },
                _ => "PERFIL ENCONTRADO"
            };
        }

        private async void SearchFriendFromUi()
        {
            if (_friendsUiOperationInProgress || PlayerFriendsRuntime.IsBusy)
                return;
            _friendsUiOperationInProgress = true;
            _friendSearchResult = null;
            _friendsFeedback = "Procurando duelista no Nexo...";
            _friendsFeedbackIsError = false;
            ShowFriendsHubScreen();
            try
            {
                _friendSearchResult = await PlayerFriendsRuntime.SearchAsync(
                    _friendSearchQuery);
                _friendsFeedback = "Perfil localizado com segurança.";
                _friendsFeedbackIsError = false;
            }
            catch (Exception exception)
            {
                _friendsFeedback = exception.GetBaseException().Message;
                _friendsFeedbackIsError = true;
            }
            finally
            {
                _friendsUiOperationInProgress = false;
                if (this != null)
                    ShowFriendsHubScreen();
            }
        }

        private void SendFriendRequestFromUi(FriendProfileView profile) =>
            RunFriendsUiOperation(
                () => PlayerFriendsRuntime.SendRequestAsync(profile),
                FriendsPage.Requests);

        private void AcceptFriendFromUi(FriendProfileView profile) =>
            RunFriendsUiOperation(
                () => PlayerFriendsRuntime.AcceptAsync(profile.playerId),
                FriendsPage.Connections);

        private void IgnoreFriendFromUi(FriendProfileView profile) =>
            RunFriendsUiOperation(
                () => PlayerFriendsRuntime.IgnoreIncomingAsync(profile.playerId),
                FriendsPage.Requests);

        private void CancelFriendRequestFromUi(FriendProfileView profile) =>
            RunFriendsUiOperation(
                () => PlayerFriendsRuntime.CancelOutgoingAsync(profile.playerId),
                FriendsPage.Requests);

        private void RemoveFriendFromUi(FriendProfileView profile) =>
            RunFriendsUiOperation(
                () => PlayerFriendsRuntime.RemoveFriendAsync(profile.playerId),
                FriendsPage.Connections);

        private void BeginFriendDuelChallengeFromUi(
            FriendProfileView profile,
            FriendDuelMode mode)
        {
            CloseFriendDuelModeChooser();
            RunFriendDuelUiOperation(
                () => FriendDuelChallengeRuntime.ChallengeAsync(profile, mode));
        }

        private void AcceptFriendDuelFromUi(
            FriendDuelChallengeView challenge) =>
            RunFriendDuelUiOperation(
                () => FriendDuelChallengeRuntime.AcceptAsync(
                    challenge.challengeId));

        private void DeclineFriendDuelFromUi(
            FriendDuelChallengeView challenge) =>
            RunFriendDuelUiOperation(
                () => FriendDuelChallengeRuntime.DeclineAsync(
                    challenge.challengeId));

        private void CancelFriendDuelFromUi(
            FriendDuelChallengeView challenge) =>
            RunFriendDuelUiOperation(
                () => FriendDuelChallengeRuntime.CancelAsync(
                    challenge.challengeId));

        private async void RunFriendDuelUiOperation(Func<Task> operation)
        {
            if (_friendsUiOperationInProgress ||
                FriendDuelChallengeRuntime.IsBusy)
            {
                return;
            }
            _friendsUiOperationInProgress = true;
            _friendsFeedback = "Sincronizando desafio privado...";
            _friendsFeedbackIsError = false;
            try
            {
                await operation();
                _friendsPage = FriendsPage.Duels;
                _friendsFeedback = FriendDuelChallengeRuntime.Status;
            }
            catch (Exception exception)
            {
                _friendsFeedback = exception.GetBaseException().Message;
                _friendsFeedbackIsError = true;
            }
            finally
            {
                _friendsUiOperationInProgress = false;
                if (this != null)
                    ShowFriendsHubScreen();
            }
        }

        private async void RunFriendsUiOperation(
            Func<Task> operation,
            FriendsPage successPage)
        {
            if (_friendsUiOperationInProgress || PlayerFriendsRuntime.IsBusy)
                return;
            _friendsUiOperationInProgress = true;
            _friendsFeedback = "Sincronizando conexão...";
            _friendsFeedbackIsError = false;
            try
            {
                await operation();
                _friendsPage = successPage;
                _friendSearchResult = null;
                _friendsFeedback = PlayerFriendsRuntime.Status;
            }
            catch (Exception exception)
            {
                _friendsFeedback = exception.GetBaseException().Message;
                _friendsFeedbackIsError = true;
            }
            finally
            {
                _friendsUiOperationInProgress = false;
                if (this != null)
                    ShowFriendsHubScreen();
            }
        }

        public void DecorateMainMenuFriendsButton(Button friendsButton)
        {
            _mainMenuFriendsButton = friendsButton;
            UpdateMainMenuFriendsBadge();
        }

        private void UpdateMainMenuFriendsBadge()
        {
            if (_mainMenuFriendsButton == null)
                return;
            Transform prior = _mainMenuFriendsButton.transform.Find(
                "Alertas Sociais Pendentes");
            if (prior == null)
            {
                prior = _mainMenuFriendsButton.transform.Find(
                    "Pedidos de Amizade Pendentes");
                if (prior != null)
                    prior.name = "Alertas Sociais Pendentes";
            }
            GameObject badgeObject = prior != null ? prior.gameObject : null;
            int count = PlayerFriendsRuntime.IncomingCount +
                        FriendDuelChallengeRuntime.IncomingCount;
            if (badgeObject == null && count > 0)
            {
                Image badge = CreatePanel(
                    _mainMenuFriendsButton.transform,
                    "Alertas Sociais Pendentes",
                    new Vector2(0.62f, 0.68f),
                    new Vector2(1.22f, 1.20f),
                    new Color(0.08f, 0.80f, 1f, 0.98f));
                // O contador é apenas visual. Se receber raycast, bloqueia a
                // metade direita do sino e faz o clique parecer deslocado.
                badge.raycastTarget = false;
                AddOutline(badge.gameObject, Color.white, new Vector2(1f, -1f));
                CreateText(
                    badge.transform,
                    count > 9 ? "9+" : count.ToString(),
                    12,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.05f, 0.05f),
                    new Vector2(0.95f, 0.95f),
                    TextAnchor.MiddleCenter);
                badgeObject = badge.gameObject;
            }

            if (badgeObject == null)
                return;
            Image badgeImage = badgeObject.GetComponent<Image>();
            if (badgeImage != null)
                badgeImage.raycastTarget = false;
            badgeObject.SetActive(count > 0);
            Text number = badgeObject.GetComponentInChildren<Text>(true);
            if (number != null)
                number.text = count > 9 ? "9+" : count.ToString();
        }

        private void HandleFriendsRuntimeChanged()
        {
            UpdateMainMenuFriendsBadge();
            if (_friendsUiRoot != null &&
                _friendsUiRoot.activeInHierarchy &&
                !_friendsUiOperationInProgress)
            {
                ShowFriendsHubScreen();
            }
        }

        private void HandleFriendDuelChallengeChanged()
        {
            UpdateMainMenuFriendsBadge();
            if (_friendsUiRoot != null &&
                _friendsUiRoot.activeInHierarchy &&
                !_friendsUiOperationInProgress)
            {
                ShowFriendsHubScreen();
            }
        }
    }
}
