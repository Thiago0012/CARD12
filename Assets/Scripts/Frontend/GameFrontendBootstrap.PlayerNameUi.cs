using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private void BuildModernPlayerNameEditor(
            bool canReturn,
            Action backAction)
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("IDENTIDADE DO DUELISTA");
            if (canReturn)
                BuildHeader("IDENTIDADE", backAction ?? ShowMainMenu);

            Image stage = CreatePanel(
                _screenRoot,
                "Registro Moderno do Duelista",
                new Vector2(0.105f, 0.085f),
                new Vector2(0.895f, 0.85f),
                Color.clear);

            Image heading = CreateArcaneSurface(
                stage.transform,
                "Cabeçalho do Registro",
                new Vector2(0f, 0.82f),
                new Vector2(1f, 1f),
                ArcaneCyan,
                true,
                0.92f);
            CreatePanel(
                heading.transform,
                "Marcador do Registro",
                new Vector2(0.024f, 0.15f),
                new Vector2(0.032f, 0.85f),
                ArcaneGold).raycastTarget = false;
            CreateText(
                heading.transform,
                canReturn ? "EDITAR IDENTIDADE" : "CRIAR IDENTIDADE",
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.052f, 0.46f),
                new Vector2(0.62f, 0.91f),
                TextAnchor.MiddleLeft);
            CreateText(
                heading.transform,
                "MASTER DUEL 2 PLUS ULTRA  •  REGISTRO DO JOGADOR",
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.052f, 0.10f),
                new Vector2(0.70f, 0.47f),
                TextAnchor.MiddleLeft);
            Image status = CreateArcaneSurface(
                heading.transform,
                "Estado do Registro",
                new Vector2(0.79f, 0.16f),
                new Vector2(0.965f, 0.84f),
                canReturn ? ArcaneCyan : ArcaneGold,
                true,
                0.72f);
            CreateText(
                status.transform,
                canReturn ? "PERFIL ATIVO" : "NOVO DUELISTA",
                12,
                FontStyle.Bold,
                canReturn ? ArcaneCyan : ArcaneGold,
                new Vector2(0.05f, 0.30f),
                new Vector2(0.95f, 0.83f),
                TextAnchor.MiddleCenter);
            CreateText(
                status.transform,
                "IDENTIDADE",
                9,
                FontStyle.Bold,
                Muted,
                new Vector2(0.05f, 0.06f),
                new Vector2(0.95f, 0.34f),
                TextAnchor.MiddleCenter);

            Image briefing = CreateArcaneSurface(
                stage.transform,
                "Orientação do Duelista",
                new Vector2(0f, 0f),
                new Vector2(0.32f, 0.79f),
                ArcaneGold,
                false,
                0.76f);
            BuildPlayerNameBriefing(briefing.transform, canReturn);

            Image form = CreateArcaneSurface(
                stage.transform,
                "Formulário da Identidade",
                new Vector2(0.345f, 0f),
                new Vector2(1f, 0.79f),
                ArcaneCyan,
                true,
                0.86f);
            BuildPlayerNameForm(form.transform, canReturn);
        }

        private void BuildPlayerNameBriefing(
            Transform parent,
            bool canReturn)
        {
            CreateText(
                parent,
                canReturn ? "ASSINATURA PÚBLICA" : "PRIMEIRO REGISTRO",
                12,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.09f, 0.85f),
                new Vector2(0.91f, 0.95f),
                TextAnchor.MiddleLeft);

            Image emblem = CreateArcaneSurface(
                parent,
                "Emblema da Identidade",
                new Vector2(0.20f, 0.59f),
                new Vector2(0.80f, 0.82f),
                ArcaneCyan,
                true,
                0.68f);
            CreateText(
                emblem.transform,
                "◇",
                38,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.05f, 0.27f),
                new Vector2(0.95f, 0.95f),
                TextAnchor.MiddleCenter);
            CreateText(
                emblem.transform,
                "REGISTRO DE DUELISTA",
                9,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.95f, 0.32f),
                TextAnchor.MiddleCenter);

            CreateText(
                parent,
                canReturn
                    ? "Altere o nome que os outros jogadores verão no perfil, na busca e nas conexões."
                    : "Escolha o nome que representará você no perfil, na busca de jogadores e durante as conexões.",
                11,
                FontStyle.Normal,
                Color.white,
                new Vector2(0.09f, 0.38f),
                new Vector2(0.91f, 0.56f),
                TextAnchor.UpperLeft);
            CreatePanel(
                parent,
                "Separador da Identidade Pública",
                new Vector2(0.09f, 0.33f),
                new Vector2(0.91f, 0.335f),
                new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.30f))
                .raycastTarget = false;
            CreateText(
                parent,
                "O nome pode mudar. O ID numérico da conta continua sendo sua referência permanente.",
                10,
                FontStyle.Normal,
                Muted,
                new Vector2(0.09f, 0.17f),
                new Vector2(0.91f, 0.31f),
                TextAnchor.UpperLeft);
            string publicId = PlayerIdAccessRuntime.PublicPlayerId;
            CreateText(
                parent,
                "ID DO JOGADOR\n" +
                (string.IsNullOrWhiteSpace(publicId)
                    ? "PREPARANDO..."
                    : publicId),
                12,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.09f, 0.035f),
                new Vector2(0.91f, 0.15f),
                TextAnchor.MiddleLeft);
        }

        private void BuildPlayerNameForm(
            Transform parent,
            bool canReturn)
        {
            CreateText(
                parent,
                canReturn ? "ATUALIZAR NOME PÚBLICO" : "DEFINIR NOME PÚBLICO",
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.82f),
                new Vector2(0.93f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                canReturn
                    ? "A alteração será sincronizada com a identidade atual."
                    : "Este será o nome usado para apresentar seu perfil aos outros duelistas.",
                11,
                FontStyle.Normal,
                Muted,
                new Vector2(0.07f, 0.73f),
                new Vector2(0.93f, 0.82f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                "NOME DE DUELISTA",
                10,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.07f, 0.61f),
                new Vector2(0.93f, 0.69f),
                TextAnchor.MiddleLeft);
            InputField input = CreateAccountInputField(
                parent,
                "Ex.: Duelista Plus Ultra",
                new Vector2(0.07f, 0.45f),
                new Vector2(0.93f, 0.61f));
            input.characterLimit = DeckRepository.MaximumPlayerNameLength;
            input.text = _repository.PlayerDisplayName;

            Text feedback = CreateText(
                parent,
                "USE DE 3 A 18 CARACTERES  •  ESTE NOME SERÁ PÚBLICO",
                10,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.07f, 0.35f),
                new Vector2(0.93f, 0.43f),
                TextAnchor.MiddleLeft);

            Action confirm = () =>
            {
                if (_repository.TrySetPlayerDisplayName(
                        input.text,
                        out string rejection))
                {
                    PlayerIdAccessRuntime.SetPlayerDisplayName(
                        _repository.PlayerDisplayName);
                    PlayerFriendsRuntime.SetLocalDisplayName(
                        _repository.PlayerDisplayName);
                    if (_repository.NeedsStarterDeckSelection)
                        ShowStarterDeckSelection();
                    else if (canReturn)
                        ShowAccountCenter();
                    else
                        ShowMainMenu();
                    return;
                }

                feedback.text = rejection.ToUpperInvariant();
                feedback.color = Danger;
                input.ActivateInputField();
            };
            input.onEndEdit.AddListener(_ => confirm());
            CreateArcaneActionButton(
                parent,
                canReturn ? "SALVAR IDENTIDADE" : "CONFIRMAR IDENTIDADE",
                canReturn
                    ? new Vector2(0.18f, 0.15f)
                    : new Vector2(0.18f, 0.21f),
                canReturn
                    ? new Vector2(0.82f, 0.30f)
                    : new Vector2(0.82f, 0.34f),
                canReturn ? ArcaneCyan : Lime,
                confirm,
                15);
            if (!canReturn)
            {
                CreateArcaneActionButton(
                    parent,
                    "ENTRAR EM CONTA EXISTENTE",
                    new Vector2(0.18f, 0.105f),
                    new Vector2(0.82f, 0.19f),
                    ArcaneCyan,
                    () => ShowAccountCredentials(true),
                    12);
            }
            CreateText(
                parent,
                canReturn
                    ? "O NOME E O ID SERÃO ASSOCIADOS AO SEU REGISTRO DE JOGADOR"
                    : "SE VOCÊ JÁ TEM CONTA, ENTRE ANTES DE CRIAR OUTRO PERFIL",
                9,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.07f, 0.045f),
                new Vector2(0.93f, 0.12f),
                TextAnchor.MiddleCenter);
        }
    }
}
