using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private void ShowAccountCenter()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("IDENTIDADE DO DUELISTA");
            BuildHeader("CONTA", () => ShowPlayerProfileSetup(true));

            Image stage = CreatePanel(
                _screenRoot,
                "Central Moderna da Conta",
                new Vector2(0.105f, 0.085f),
                new Vector2(0.895f, 0.85f),
                Color.clear);
            BuildAccountCenter(stage.transform);
            _ = RefreshAccountStateAsync();
        }

        private void BuildAccountCenter(Transform parent)
        {
            bool protectedAccount = PlayerAccountRuntime.IsProtected;
            bool synchronized = PlayerCloudSaveRuntime.State ==
                                PlayerCloudSaveState.Synchronized;

            Image hub = CreatePanel(
                parent,
                "Núcleo da Identidade",
                Vector2.zero,
                Vector2.one,
                Color.clear);

            Image heading = CreateArcaneSurface(
                hub.transform,
                "Cabeçalho da Identidade",
                new Vector2(0f, 0.83f),
                new Vector2(1f, 1f),
                ArcaneCyan,
                true,
                0.92f);
            CreatePanel(
                heading.transform,
                "Marcador da Identidade",
                new Vector2(0.024f, 0.15f),
                new Vector2(0.032f, 0.85f),
                ArcaneGold).raycastTarget = false;
            CreateText(
                heading.transform,
                "IDENTIDADE DO DUELISTA",
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.052f, 0.47f),
                new Vector2(0.55f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                heading.transform,
                "MASTER DUEL 2 PLUS ULTRA  •  CONTA, NUVEM E RECUPERAÇÃO",
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.052f, 0.11f),
                new Vector2(0.69f, 0.48f),
                TextAnchor.MiddleLeft);
            CreateAccountMetric(
                heading.transform,
                "CONTA",
                protectedAccount ? "VINCULADA" : "CONVIDADO",
                new Vector2(0.705f, 0.15f),
                new Vector2(0.835f, 0.85f),
                protectedAccount ? Lime : ArcaneGold);
            CreateAccountMetric(
                heading.transform,
                "NUVEM",
                synchronized ? "ATIVA" : "AGUARDANDO",
                new Vector2(0.85f, 0.15f),
                new Vector2(0.975f, 0.85f),
                synchronized ? ArcaneCyan : ArcaneGold);

            Image identity = CreateArcaneSurface(
                hub.transform,
                "Cartão da Identidade",
                new Vector2(0f, 0f),
                new Vector2(0.245f, 0.805f),
                ArcaneCyan,
                false,
                0.78f);
            BuildAccountIdentityCard(identity.transform, protectedAccount);

            Image body = CreateArcaneSurface(
                hub.transform,
                "Ações da Identidade",
                new Vector2(0.265f, 0f),
                new Vector2(1f, 0.805f),
                protectedAccount ? ArcaneCyan : ArcaneGold,
                true,
                0.86f);
            BuildAccountActions(body.transform, protectedAccount, synchronized);
        }

        private static void CreateAccountMetric(
            Transform parent,
            string label,
            string value,
            Vector2 min,
            Vector2 max,
            Color accent)
        {
            Image metric = CreateArcaneSurface(
                parent,
                "Métrica da Conta " + label,
                min,
                max,
                accent,
                true,
                0.72f);
            CreateText(
                metric.transform,
                value,
                13,
                FontStyle.Bold,
                accent,
                new Vector2(0.05f, 0.36f),
                new Vector2(0.95f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateText(
                metric.transform,
                label,
                9,
                FontStyle.Bold,
                Muted,
                new Vector2(0.05f, 0.07f),
                new Vector2(0.95f, 0.39f),
                TextAnchor.MiddleCenter);
        }

        private void BuildAccountIdentityCard(
            Transform parent,
            bool protectedAccount)
        {
            CreateText(
                parent,
                "REGISTRO DO JOGADOR",
                12,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.08f, 0.875f),
                new Vector2(0.92f, 0.965f),
                TextAnchor.MiddleLeft);

            Image seal = CreateArcaneSurface(
                parent,
                "Selo da Conta",
                new Vector2(0.16f, 0.62f),
                new Vector2(0.84f, 0.85f),
                protectedAccount ? Lime : ArcaneGold,
                true,
                0.78f);
            CreateText(
                seal.transform,
                protectedAccount ? "✓" : "◇",
                38,
                FontStyle.Bold,
                protectedAccount ? Lime : ArcaneGold,
                new Vector2(0.05f, 0.30f),
                new Vector2(0.95f, 0.95f),
                TextAnchor.MiddleCenter);
            CreateText(
                seal.transform,
                protectedAccount ? "IDENTIDADE VINCULADA" : "IDENTIDADE LOCAL",
                10,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.95f, 0.35f),
                TextAnchor.MiddleCenter);

            CreateText(
                parent,
                "NOME PÚBLICO",
                9,
                FontStyle.Bold,
                Muted,
                new Vector2(0.08f, 0.53f),
                new Vector2(0.92f, 0.60f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                _repository?.PlayerDisplayName ?? "DUELISTA",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.45f),
                new Vector2(0.92f, 0.54f),
                TextAnchor.MiddleLeft);
            CreatePanel(
                parent,
                "Separador do Registro",
                new Vector2(0.08f, 0.41f),
                new Vector2(0.92f, 0.415f),
                new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.28f))
                .raycastTarget = false;

            CreateText(
                parent,
                "ID NUMÉRICO DO JOGADOR",
                9,
                FontStyle.Bold,
                Muted,
                new Vector2(0.08f, 0.33f),
                new Vector2(0.92f, 0.40f),
                TextAnchor.MiddleLeft);
            string publicId = PlayerIdAccessRuntime.PublicPlayerId;
            CreateText(
                parent,
                string.IsNullOrWhiteSpace(publicId) ? "PREPARANDO..." : publicId,
                16,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.08f, 0.25f),
                new Vector2(0.92f, 0.34f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                protectedAccount
                    ? "ACESSO  " + PlayerAccountRuntime.AccountUsername
                    : "ACESSO AINDA NÃO VINCULADO",
                9,
                FontStyle.Bold,
                protectedAccount ? Lime : ArcaneGold,
                new Vector2(0.08f, 0.18f),
                new Vector2(0.92f, 0.25f),
                TextAnchor.MiddleLeft);

            CreateArcaneActionButton(
                parent,
                "EDITAR NOME",
                new Vector2(0.08f, 0.045f),
                new Vector2(0.92f, 0.145f),
                ArcaneGold,
                () => ShowPlayerNameEditor(true, ShowAccountCenter),
                12);
        }

        private void BuildAccountActions(
            Transform parent,
            bool protectedAccount,
            bool synchronized)
        {
            Color statusAccent = protectedAccount ? Lime : ArcaneGold;
            CreateText(
                parent,
                protectedAccount ? "PROGRESSO PROTEGIDO" : "PROTEJA SEU PROGRESSO",
                22,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.05f, 0.85f),
                new Vector2(0.72f, 0.96f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                protectedAccount
                    ? "Sua identidade pode ser restaurada após reinstalar o jogo ou trocar de aparelho."
                    : "Vincule credenciais à identidade atual para conservar o mesmo ID, perfil e progresso.",
                12,
                FontStyle.Normal,
                Muted,
                new Vector2(0.05f, 0.76f),
                new Vector2(0.93f, 0.86f),
                TextAnchor.MiddleLeft);

            Image state = CreateArcaneSurface(
                parent,
                "Estado da Sincronização",
                new Vector2(0.05f, 0.51f),
                new Vector2(0.95f, 0.72f),
                statusAccent,
                false,
                0.66f);
            CreatePanel(
                state.transform,
                "Pulso do Estado",
                new Vector2(0.018f, 0.18f),
                new Vector2(0.028f, 0.82f),
                synchronized ? ArcaneCyan : statusAccent).raycastTarget = false;
            CreateText(
                state.transform,
                synchronized ? "SINCRONIZAÇÃO CONCLUÍDA" : "ESTADO DA CONTA",
                13,
                FontStyle.Bold,
                synchronized ? ArcaneCyan : statusAccent,
                new Vector2(0.055f, 0.54f),
                new Vector2(0.94f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                state.transform,
                PlayerCloudSaveRuntime.Status,
                11,
                FontStyle.Normal,
                Color.white,
                new Vector2(0.055f, 0.12f),
                new Vector2(0.94f, 0.55f),
                TextAnchor.MiddleLeft);

            if (!protectedAccount)
            {
                CreateArcaneActionButton(
                    parent,
                    "VINCULAR E PROTEGER",
                    new Vector2(0.05f, 0.31f),
                    new Vector2(0.48f, 0.46f),
                    Lime,
                    () => ShowAccountCredentials(false),
                    14);
            }
            else
            {
                CreateArcaneActionButton(
                    parent,
                    "SINCRONIZAR AGORA",
                    new Vector2(0.05f, 0.31f),
                    new Vector2(0.48f, 0.46f),
                    ArcaneCyan,
                    SynchronizeAccountFromUi,
                    14);
            }
            CreateArcaneActionButton(
                parent,
                "RESTAURAR OUTRA CONTA",
                new Vector2(0.52f, 0.31f),
                new Vector2(0.95f, 0.46f),
                ArcaneCyan,
                () => ShowAccountCredentials(true),
                14);

            CreateText(
                parent,
                "SEGURANÇA DA IDENTIDADE",
                10,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.05f, 0.20f),
                new Vector2(0.95f, 0.27f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                "A senha nunca aparece no perfil. O nome público e o ID numérico continuam sendo sua assinatura dentro do jogo.",
                10,
                FontStyle.Normal,
                Muted,
                new Vector2(0.05f, 0.085f),
                new Vector2(0.95f, 0.20f),
                TextAnchor.UpperLeft);
        }

        private async System.Threading.Tasks.Task RefreshAccountStateAsync()
        {
            try
            {
                await PlayerAccountRuntime.RefreshProtectionStateAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Conta] Não foi possível atualizar o estado: " +
                    exception.GetBaseException().Message);
            }
        }

        private async void SynchronizeAccountFromUi()
        {
            try
            {
                await PlayerCloudSaveRuntime.ReloadForCurrentAccountAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Conta] " + exception.Message);
            }
            if (this != null)
                ShowAccountCenter();
        }

        private void ShowAccountCredentials(bool signInExisting)
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground(
                signInExisting ? "RESTAURAÇÃO DA IDENTIDADE" : "VÍNCULO DA IDENTIDADE");
            BuildHeader(
                signInExisting ? "RESTAURAR CONTA" : "PROTEGER CONTA",
                signInExisting && _repository != null &&
                !_repository.HasPlayerProfile
                    ? ShowPlayerProfileSetup
                    : ShowAccountCenter);

            Image stage = CreatePanel(
                _screenRoot,
                "Terminal de Credenciais",
                new Vector2(0.13f, 0.09f),
                new Vector2(0.87f, 0.85f),
                Color.clear);
            BuildAccountCredentials(stage.transform, signInExisting);
        }

        private void BuildAccountCredentials(
            Transform parent,
            bool signInExisting)
        {
            Image heading = CreateArcaneSurface(
                parent,
                "Cabeçalho das Credenciais",
                new Vector2(0f, 0.81f),
                new Vector2(1f, 1f),
                signInExisting ? ArcaneCyan : ArcaneGold,
                true,
                0.92f);
            CreatePanel(
                heading.transform,
                "Marcador das Credenciais",
                new Vector2(0.024f, 0.16f),
                new Vector2(0.033f, 0.84f),
                signInExisting ? ArcaneCyan : ArcaneGold).raycastTarget = false;
            CreateText(
                heading.transform,
                signInExisting ? "RESTAURAR IDENTIDADE" : "VINCULAR IDENTIDADE ATUAL",
                24,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.055f, 0.43f),
                new Vector2(0.70f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                heading.transform,
                "ACESSO SEGURO  •  MASTER DUEL 2 PLUS ULTRA",
                10,
                FontStyle.Bold,
                Muted,
                new Vector2(0.055f, 0.10f),
                new Vector2(0.70f, 0.44f),
                TextAnchor.MiddleLeft);

            Image briefing = CreateArcaneSurface(
                parent,
                "Informações da Operação",
                new Vector2(0f, 0f),
                new Vector2(0.32f, 0.78f),
                signInExisting ? ArcaneCyan : ArcaneGold,
                false,
                0.76f);
            CreateText(
                briefing.transform,
                signInExisting ? "RECUPERAÇÃO" : "PROTEÇÃO",
                12,
                FontStyle.Bold,
                signInExisting ? ArcaneCyan : ArcaneGold,
                new Vector2(0.09f, 0.84f),
                new Vector2(0.91f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateText(
                briefing.transform,
                signInExisting
                    ? "Entre com as credenciais já vinculadas. A sessão será trocada e o perfil salvo na nuvem será restaurado."
                    : "Crie um acesso para a identidade que está neste aparelho. O ID numérico e o nome público não serão trocados.",
                12,
                FontStyle.Normal,
                Color.white,
                new Vector2(0.09f, 0.54f),
                new Vector2(0.91f, 0.82f),
                TextAnchor.UpperLeft);
            CreatePanel(
                briefing.transform,
                "Separador da Segurança",
                new Vector2(0.09f, 0.47f),
                new Vector2(0.91f, 0.475f),
                new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.30f))
                .raycastTarget = false;
            CreateText(
                briefing.transform,
                "O usuário de acesso é privado e separado do nome mostrado aos outros duelistas.",
                10,
                FontStyle.Normal,
                Muted,
                new Vector2(0.09f, 0.25f),
                new Vector2(0.91f, 0.44f),
                TextAnchor.UpperLeft);
            CreateText(
                briefing.transform,
                "ID ATUAL\n" + PlayerIdAccessRuntime.PublicPlayerId,
                13,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.09f, 0.07f),
                new Vector2(0.91f, 0.22f),
                TextAnchor.MiddleLeft);

            Image form = CreateArcaneSurface(
                parent,
                "Formulário de Credenciais",
                new Vector2(0.345f, 0f),
                new Vector2(1f, 0.78f),
                ArcaneCyan,
                true,
                0.86f);
            CreateText(
                form.transform,
                "CREDENCIAIS DE ACESSO",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.84f),
                new Vector2(0.93f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateText(
                form.transform,
                "USUÁRIO",
                10,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.07f, 0.72f),
                new Vector2(0.93f, 0.79f),
                TextAnchor.MiddleLeft);
            InputField username = CreateAccountInputField(
                form.transform,
                "3 a 20 caracteres",
                new Vector2(0.07f, 0.58f),
                new Vector2(0.93f, 0.72f));
            username.characterLimit = 20;
            username.text = signInExisting ? string.Empty : SuggestedAccountUsername();

            CreateText(
                form.transform,
                "SENHA",
                10,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.07f, 0.47f),
                new Vector2(0.93f, 0.54f),
                TextAnchor.MiddleLeft);
            InputField password = CreateAccountInputField(
                form.transform,
                "8 a 30 caracteres",
                new Vector2(0.07f, 0.33f),
                new Vector2(0.93f, 0.47f));
            password.characterLimit = 30;
            password.contentType = InputField.ContentType.Password;
            password.ForceLabelUpdate();

            Text feedback = CreateText(
                form.transform,
                signInExisting
                    ? "A identidade armazenada na nuvem será recuperada."
                    : "Use maiúscula, minúscula, número e símbolo. Guarde essas credenciais.",
                10,
                FontStyle.Normal,
                ArcaneGold,
                new Vector2(0.07f, 0.23f),
                new Vector2(0.93f, 0.31f),
                TextAnchor.MiddleLeft);

            bool busy = false;
            Action submit = async () =>
            {
                if (busy)
                    return;
                busy = true;
                feedback.text = signInExisting
                    ? "AUTENTICANDO E RESTAURANDO..."
                    : "VINCULANDO E SINCRONIZANDO...";
                feedback.color = ArcaneCyan;
                try
                {
                    if (signInExisting)
                    {
                        await PlayerAccountRuntime.SignInExistingAccountAsync(
                            username.text,
                            password.text);
                    }
                    else
                    {
                        await PlayerAccountRuntime.ProtectCurrentAccountAsync(
                            username.text,
                            password.text);
                    }
                    if (this == null)
                        return;
                    PlayerFriendsRuntime.SetLocalDisplayName(
                        _repository?.PlayerDisplayName);
                    ShowAccountCenter();
                }
                catch (Exception exception)
                {
                    busy = false;
                    feedback.text = DescribeAccountFailure(exception);
                    feedback.color = Danger;
                }
            };
            CreateArcaneActionButton(
                form.transform,
                signInExisting ? "ENTRAR E RESTAURAR" : "VINCULAR CONTA",
                new Vector2(0.20f, 0.055f),
                new Vector2(0.80f, 0.19f),
                signInExisting ? ArcaneCyan : Lime,
                submit,
                14);
        }

        private static InputField CreateAccountInputField(
            Transform parent,
            string placeholder,
            Vector2 min,
            Vector2 max)
        {
            Image background = CreateArcaneSurface(
                parent,
                "Campo de Credencial",
                min,
                max,
                ArcaneCyan,
                true,
                0.64f);
            CreatePanel(
                background.transform,
                "Indicador da Credencial",
                new Vector2(0.014f, 0.16f),
                new Vector2(0.022f, 0.84f),
                ArcaneCyan).raycastTarget = false;
            Text inputText = CreateText(
                background.transform,
                string.Empty,
                16,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.055f, 0.10f),
                new Vector2(0.95f, 0.90f),
                TextAnchor.MiddleLeft);
            Text placeholderText = CreateText(
                background.transform,
                placeholder,
                13,
                FontStyle.Normal,
                new Color(Muted.r, Muted.g, Muted.b, 0.86f),
                new Vector2(0.055f, 0.10f),
                new Vector2(0.95f, 0.90f),
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

        private string SuggestedAccountUsername()
        {
            string source = _repository?.PlayerDisplayName ?? string.Empty;
            var result = new System.Text.StringBuilder(20);
            foreach (char character in source)
            {
                if (char.IsLetterOrDigit(character) ||
                    character == '.' || character == '-' ||
                    character == '@' || character == '_')
                {
                    result.Append(character);
                }
                if (result.Length >= 20)
                    break;
            }
            return result.Length >= 3 ? result.ToString() : string.Empty;
        }

        private static string DescribeAccountFailure(Exception exception)
        {
            string message = exception?.GetBaseException().Message ??
                             "Falha desconhecida.";
            if (message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("exists", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Esse usuário já está em uso. Escolha outro ou use ENTRAR.";
            }
            if (message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("credentials", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Usuário ou senha inválidos.";
            }
            return message;
        }
    }
}
