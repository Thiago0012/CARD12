using System;
using ArcaneArena.Presentation;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Composição moderna e responsiva da central de configurações. Mantém
    /// as preferências existentes e altera somente sua apresentação visual.
    /// </summary>
    public sealed partial class GameFrontendBootstrap
    {
        private static readonly Color OptionsViolet =
            new(0.48f, 0.37f, 0.96f, 1f);
        private static readonly Color OptionsMint =
            new(0.22f, 0.88f, 0.70f, 1f);
        private static readonly Color OptionsAmber =
            new(0.96f, 0.70f, 0.28f, 1f);
        private static readonly Color OptionsSoftText =
            new(0.66f, 0.73f, 0.82f, 1f);

        private void BuildModernAnimationOptionsScreen()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildModernOptionsBackground();
            BuildModernOptionsHeader();

            Image shell = CreateArcaneSurface(
                _screenRoot,
                "Central de Configurações",
                new Vector2(0.055f, 0.065f),
                new Vector2(0.945f, 0.875f),
                OptionsViolet,
                false,
                0.94f);

            BuildOptionsNavigationRail(shell.transform);
            BuildOptionsContent(shell.transform);
        }

        private void BuildModernOptionsBackground()
        {
            Image holder = CreatePanel(
                _screenRoot,
                "Fundo Tecnológico das Opções",
                Vector2.zero,
                Vector2.one,
                Color.clear);
            holder.transform.SetAsFirstSibling();

            GameObject backdropObject = new(
                "Circuitos de Configuração",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelModeBackdropGraphic));
            backdropObject.transform.SetParent(holder.transform, false);
            RectTransform rect = backdropObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            DuelModeBackdropGraphic backdrop =
                backdropObject.GetComponent<DuelModeBackdropGraphic>();
            backdrop.raycastTarget = false;
            backdrop.SetAccent(OptionsViolet);

            CreatePanel(
                holder.transform,
                "Linha de Energia Superior",
                new Vector2(0.055f, 0.885f),
                new Vector2(0.945f, 0.8885f),
                new Color(
                    OptionsViolet.r,
                    OptionsViolet.g,
                    OptionsViolet.b,
                    0.78f)).raycastTarget = false;
        }

        private void BuildModernOptionsHeader()
        {
            CreateArcaneActionButton(
                _screenRoot,
                "‹",
                new Vector2(0.027f, 0.905f),
                new Vector2(0.071f, 0.972f),
                OptionsViolet,
                ShowMainMenu,
                27);
            CreateText(
                _screenRoot,
                "OPÇÕES",
                32,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.083f, 0.902f),
                new Vector2(0.34f, 0.974f),
                TextAnchor.MiddleLeft);
            CreateText(
                _screenRoot,
                "CONTROLE  •  ÁUDIO  •  APRESENTAÇÃO",
                14,
                FontStyle.Bold,
                new Color(0.65f, 0.58f, 1f, 0.95f),
                new Vector2(0.62f, 0.91f),
                new Vector2(0.94f, 0.965f),
                TextAnchor.MiddleRight);
        }

        private void BuildOptionsNavigationRail(Transform parent)
        {
            Image rail = CreateArcaneSurface(
                parent,
                "Navegação das Opções",
                new Vector2(0.018f, 0.035f),
                new Vector2(0.265f, 0.965f),
                OptionsViolet,
                true,
                0.72f);

            CreateText(
                rail.transform,
                "CENTRAL DO SISTEMA",
                13,
                FontStyle.Bold,
                new Color(0.67f, 0.60f, 1f, 1f),
                new Vector2(0.09f, 0.89f),
                new Vector2(0.91f, 0.95f),
                TextAnchor.MiddleLeft);
            CreateText(
                rail.transform,
                "Configure este dispositivo sem alterar seu perfil em outros aparelhos.",
                16,
                FontStyle.Normal,
                OptionsSoftText,
                new Vector2(0.09f, 0.76f),
                new Vector2(0.91f, 0.88f),
                TextAnchor.UpperLeft);

            BuildOptionsStatusCard(
                rail.transform,
                "QUALIDADE ATUAL",
                ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsPreferences.Quality),
                OptionsMint,
                0.635f);
            BuildOptionsStatusCard(
                rail.transform,
                "ESCALA DE TEXTO",
                ArcaneUiTextPreferences.DisplayName(
                    ArcaneUiTextPreferences.Current),
                OptionsAmber,
                0.505f);

            CreateArcaneActionButton(
                rail.transform,
                "PERFIL DO DUELISTA",
                new Vector2(0.08f, 0.365f),
                new Vector2(0.92f, 0.445f),
                ArcaneCyan,
                () => ShowPlayerProfileSetup(true),
                16);
            CreateArcaneActionButton(
                rail.transform,
                "RESPOSTAS DO DUELO",
                new Vector2(0.08f, 0.265f),
                new Vector2(0.92f, 0.345f),
                OptionsMint,
                ShowDuelResponseOptions,
                16);
            CreateArcaneActionButton(
                rail.transform,
                "TEXTO  •  " + ArcaneUiTextPreferences.DisplayName(
                    ArcaneUiTextPreferences.Current),
                new Vector2(0.08f, 0.165f),
                new Vector2(0.92f, 0.245f),
                OptionsViolet,
                () =>
                {
                    ArcaneUiTextPreferences.Set(
                        ArcaneUiTextPreferences.Next(
                            ArcaneUiTextPreferences.Current));
                    ShowAnimationOptions();
                },
                16);
            CreateArcaneActionButton(
                rail.transform,
                "RESTAURAR PADRÃO",
                new Vector2(0.08f, 0.055f),
                new Vector2(0.92f, 0.135f),
                OptionsAmber,
                ResetAllPresentationPreferences,
                16);
        }

        private static void BuildOptionsStatusCard(
            Transform parent,
            string caption,
            string value,
            Color accent,
            float yMin)
        {
            Image card = CreateArcaneSurface(
                parent,
                caption,
                new Vector2(0.08f, yMin),
                new Vector2(0.92f, yMin + 0.105f),
                accent,
                false,
                0.72f);
            CreateText(
                card.transform,
                caption,
                11,
                FontStyle.Bold,
                OptionsSoftText,
                new Vector2(0.07f, 0.53f),
                new Vector2(0.93f, 0.87f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                value.ToUpperInvariant(),
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.10f),
                new Vector2(0.93f, 0.55f),
                TextAnchor.MiddleLeft);
        }

        private void BuildOptionsContent(Transform parent)
        {
            Image content = CreateArcaneSurface(
                parent,
                "Configurações de Apresentação",
                new Vector2(0.282f, 0.035f),
                new Vector2(0.982f, 0.965f),
                ArcaneCyan,
                false,
                0.78f);

            CreateText(
                content.transform,
                "APRESENTAÇÃO DO JOGO",
                26,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.90f),
                new Vector2(0.60f, 0.965f),
                TextAnchor.MiddleLeft);
            CreateText(
                content.transform,
                "AJUSTES LOCAIS EM TEMPO REAL",
                12,
                FontStyle.Bold,
                OptionsSoftText,
                new Vector2(0.60f, 0.91f),
                new Vector2(0.965f, 0.96f),
                TextAnchor.MiddleRight);

            BuildModernGraphicsCard(content.transform);
            BuildModernAudioCard(content.transform);
            BuildModernAnimationCard(content.transform);
        }

        private void BuildModernGraphicsCard(Transform parent)
        {
            Image card = CreateArcaneSurface(
                parent,
                "Qualidade gráfica",
                new Vector2(0.03f, 0.715f),
                new Vector2(0.97f, 0.885f),
                OptionsViolet,
                true,
                0.72f);
            CreateText(
                card.transform,
                "DESEMPENHO VISUAL",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.57f),
                new Vector2(0.25f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                "Equilíbrio entre nitidez e desempenho",
                12,
                FontStyle.Normal,
                OptionsSoftText,
                new Vector2(0.025f, 0.18f),
                new Vector2(0.25f, 0.56f),
                TextAnchor.MiddleLeft);

            ArcaneGraphicsQuality[] levels =
            {
                ArcaneGraphicsQuality.VeryLow,
                ArcaneGraphicsQuality.Low,
                ArcaneGraphicsQuality.Medium,
                ArcaneGraphicsQuality.High,
                ArcaneGraphicsQuality.VeryHigh
            };
            string[] labels = { "M. BAIXO", "BAIXO", "MÉDIO", "ALTO", "M. ALTO" };
            const float start = 0.285f;
            const float gap = 0.012f;
            const float width = 0.126f;
            for (int index = 0; index < levels.Length; index++)
            {
                ArcaneGraphicsQuality level = levels[index];
                float xMin = start + index * (width + gap);
                bool selected = ArcaneGraphicsPreferences.Quality == level;
                CreateOptionsChoiceButton(
                    card.transform,
                    labels[index],
                    new Vector2(xMin, 0.20f),
                    new Vector2(xMin + width, 0.80f),
                    selected,
                    OptionsViolet,
                    () =>
                    {
                        ArcaneGraphicsPreferences.SetQuality(level);
                        ShowAnimationOptions();
                    });
            }
        }

        private void BuildModernAudioCard(Transform parent)
        {
            Image card = CreateArcaneSurface(
                parent,
                "Mixagem de Áudio",
                new Vector2(0.03f, 0.545f),
                new Vector2(0.97f, 0.70f),
                OptionsMint,
                false,
                0.68f);

            Text effectsValue = BuildModernVolumeControl(
                card.transform,
                "EFEITOS",
                ArcaneAudioPreferences.Volume,
                new Vector2(0.025f, 0.12f),
                new Vector2(0.485f, 0.88f),
                ArcaneCyan,
                value =>
                {
                    ArcaneAudioPreferences.Volume = value;
                    RefreshMasterAudioState();
                });
            Text musicValue = BuildModernVolumeControl(
                card.transform,
                "MÚSICA",
                ArcaneMusicPreferences.Volume,
                new Vector2(0.515f, 0.12f),
                new Vector2(0.975f, 0.88f),
                OptionsMint,
                value =>
                {
                    ArcaneMusicPreferences.Volume = value;
                    RefreshMasterAudioState();
                });
            effectsValue.name = "Valor dos Efeitos";
            musicValue.name = "Valor da Música";
        }

        private static Text BuildModernVolumeControl(
            Transform parent,
            string label,
            float value,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action<float> onChanged)
        {
            Image group = CreatePanel(parent, label, min, max, Color.clear);
            Text valueText = CreateText(
                group.transform,
                $"{Mathf.RoundToInt(value * 100f)}%",
                16,
                FontStyle.Bold,
                accent,
                new Vector2(0.78f, 0.54f),
                new Vector2(0.98f, 0.94f),
                TextAnchor.MiddleRight);
            CreateText(
                group.transform,
                label,
                15,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.02f, 0.54f),
                new Vector2(0.76f, 0.94f),
                TextAnchor.MiddleLeft);

            Slider slider = CreateModernOptionsSlider(
                group.transform,
                value,
                new Vector2(0.02f, 0.13f),
                new Vector2(0.98f, 0.45f),
                accent);
            slider.onValueChanged.AddListener(changedValue =>
            {
                valueText.text = $"{Mathf.RoundToInt(changedValue * 100f)}%";
                onChanged?.Invoke(changedValue);
            });
            return valueText;
        }

        private static Slider CreateModernOptionsSlider(
            Transform parent,
            float value,
            Vector2 min,
            Vector2 max,
            Color accent)
        {
            Image track = CreateArcaneSurface(
                parent,
                "Trilho",
                min,
                max,
                accent,
                false,
                0.54f);

            GameObject fillAreaObject = new("Área de Preenchimento", typeof(RectTransform));
            fillAreaObject.transform.SetParent(track.transform, false);
            RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(9f, 5f);
            fillArea.offsetMax = new Vector2(-9f, -5f);
            Image fill = CreatePanel(
                fillArea,
                "Energia",
                Vector2.zero,
                Vector2.one,
                new Color(accent.r, accent.g, accent.b, 0.90f));

            GameObject handleAreaObject = new("Área do Controle", typeof(RectTransform));
            handleAreaObject.transform.SetParent(track.transform, false);
            RectTransform handleArea = handleAreaObject.GetComponent<RectTransform>();
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(9f, 1f);
            handleArea.offsetMax = new Vector2(-9f, -1f);
            Image handle = CreatePanel(
                handleArea,
                "Controle",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Color.white);
            handle.rectTransform.sizeDelta = new Vector2(18f, 0f);
            AddOutline(handle.gameObject, accent, new Vector2(1.2f, -1.2f));

            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            return slider;
        }

        private void BuildModernAnimationCard(Transform parent)
        {
            Image card = CreateArcaneSurface(
                parent,
                "Ritmo das Animações",
                new Vector2(0.03f, 0.045f),
                new Vector2(0.97f, 0.53f),
                OptionsAmber,
                false,
                0.64f);
            CreateText(
                card.transform,
                "RITMO DAS ANIMAÇÕES",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.86f),
                new Vector2(0.55f, 0.97f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                "Ative cada apresentação e escolha sua velocidade",
                12,
                FontStyle.Normal,
                OptionsSoftText,
                new Vector2(0.52f, 0.86f),
                new Vector2(0.975f, 0.97f),
                TextAnchor.MiddleRight);

            BuildModernAnimationRow(
                card.transform,
                "INVOCAÇÃO DE MONSTROS",
                DuelAnimationPreferences.MonsterEnabled,
                DuelAnimationPreferences.MonsterSpeedMultiplier,
                0.61f,
                () =>
                {
                    DuelAnimationPreferences.MonsterEnabled =
                        !DuelAnimationPreferences.MonsterEnabled;
                    ShowAnimationOptions();
                },
                speed =>
                {
                    DuelAnimationPreferences.MonsterSpeedMultiplier = speed;
                    ShowAnimationOptions();
                });
            BuildModernAnimationRow(
                card.transform,
                "MAGIAS E ARMADILHAS",
                DuelAnimationPreferences.SpellTrapEnabled,
                DuelAnimationPreferences.SpellTrapSpeedMultiplier,
                0.33f,
                () =>
                {
                    DuelAnimationPreferences.SpellTrapEnabled =
                        !DuelAnimationPreferences.SpellTrapEnabled;
                    ShowAnimationOptions();
                },
                speed =>
                {
                    DuelAnimationPreferences.SpellTrapSpeedMultiplier = speed;
                    ShowAnimationOptions();
                });
            BuildModernAnimationRow(
                card.transform,
                "APRESENTAÇÃO DA CORRENTE",
                DuelAnimationPreferences.ChainEnabled,
                DuelAnimationPreferences.ChainSpeedMultiplier,
                0.05f,
                () =>
                {
                    DuelAnimationPreferences.ChainEnabled =
                        !DuelAnimationPreferences.ChainEnabled;
                    ShowAnimationOptions();
                },
                speed =>
                {
                    DuelAnimationPreferences.ChainSpeedMultiplier = speed;
                    ShowAnimationOptions();
                });
        }

        private void BuildModernAnimationRow(
            Transform parent,
            string label,
            bool enabled,
            float currentSpeed,
            float yMin,
            Action toggle,
            Action<float> setSpeed)
        {
            Image row = CreateArcaneSurface(
                parent,
                label,
                new Vector2(0.02f, yMin),
                new Vector2(0.98f, yMin + 0.235f),
                enabled ? OptionsMint : Danger,
                false,
                0.52f);
            CreateText(
                row.transform,
                label,
                15,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.18f),
                new Vector2(0.29f, 0.82f),
                TextAnchor.MiddleLeft);
            CreateOptionsChoiceButton(
                row.transform,
                enabled ? "ATIVA" : "DESATIVADA",
                new Vector2(0.30f, 0.18f),
                new Vector2(0.43f, 0.82f),
                enabled,
                OptionsMint,
                toggle,
                13);

            float[] speeds = { 0.75f, 1f, 1.5f, 2f };
            const float start = 0.46f;
            const float width = 0.115f;
            const float gap = 0.012f;
            for (int index = 0; index < speeds.Length; index++)
            {
                float speed = speeds[index];
                float xMin = start + index * (width + gap);
                CreateOptionsChoiceButton(
                    row.transform,
                    $"{speed:0.##}x",
                    new Vector2(xMin, 0.18f),
                    new Vector2(xMin + width, 0.82f),
                    Mathf.Approximately(currentSpeed, speed),
                    OptionsViolet,
                    () => setSpeed(speed),
                13);
            }
        }

        private void BuildModernDuelResponseOptionsScreen()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildModernOptionsBackground();
            BuildModernDuelResponseHeader();

            Image shell = CreateArcaneSurface(
                _screenRoot,
                "Respostas e Correntes",
                new Vector2(0.055f, 0.065f),
                new Vector2(0.945f, 0.875f),
                OptionsMint,
                false,
                0.94f);

            BuildDuelResponseNavigationRail(shell.transform);
            BuildDuelResponseContent(shell.transform);
        }

        private void BuildModernDuelResponseHeader()
        {
            CreateArcaneActionButton(
                _screenRoot,
                "‹",
                new Vector2(0.027f, 0.905f),
                new Vector2(0.071f, 0.972f),
                OptionsMint,
                ShowAnimationOptions,
                27);
            CreateText(
                _screenRoot,
                "RESPOSTAS DO DUELO",
                31,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.083f, 0.902f),
                new Vector2(0.43f, 0.974f),
                TextAnchor.MiddleLeft);
            CreateText(
                _screenRoot,
                "PRIORIDADE  •  CORRENTES  •  ORIENTAÇÃO",
                14,
                FontStyle.Bold,
                new Color(0.42f, 0.94f, 0.78f, 0.95f),
                new Vector2(0.56f, 0.91f),
                new Vector2(0.94f, 0.965f),
                TextAnchor.MiddleRight);
        }

        private void BuildDuelResponseNavigationRail(Transform parent)
        {
            Image rail = CreateArcaneSurface(
                parent,
                "Resumo das Respostas",
                new Vector2(0.018f, 0.035f),
                new Vector2(0.285f, 0.965f),
                OptionsMint,
                true,
                0.72f);

            CreateText(
                rail.transform,
                "CONTROLE DE PRIORIDADE",
                13,
                FontStyle.Bold,
                OptionsMint,
                new Vector2(0.09f, 0.89f),
                new Vector2(0.91f, 0.95f),
                TextAnchor.MiddleLeft);
            CreateText(
                rail.transform,
                "Ajuste quanto o jogo orienta suas decisões sem alterar " +
                "regras, alvos legais ou a resolução dos efeitos.",
                15,
                FontStyle.Normal,
                OptionsSoftText,
                new Vector2(0.09f, 0.745f),
                new Vector2(0.91f, 0.875f),
                TextAnchor.UpperLeft);

            BuildOptionsStatusCard(
                rail.transform,
                "JANELAS OPCIONAIS",
                DuelActivationPreferences.DisplayName(
                    DuelActivationPreferences.Mode) + " • " +
                DuelActivationPreferences.ResponseWindowRhythmName,
                OptionsViolet,
                0.605f);
            BuildOptionsStatusCard(
                rail.transform,
                "ORIENTAÇÃO EM CAMPO",
                DuelActivationPreferences.GuidanceMessagesEnabled
                    ? "VISÍVEL" : "OCULTA",
                OptionsMint,
                0.475f);
            BuildOptionsStatusCard(
                rail.transform,
                "PAINEL DE CORRENTE",
                DuelActivationPreferences.ChainPanelEnabled
                    ? "VISÍVEL" : "OCULTO",
                OptionsAmber,
                0.345f);

            CreateArcaneActionButton(
                rail.transform,
                "RESTAURAR RESPOSTAS",
                new Vector2(0.08f, 0.155f),
                new Vector2(0.92f, 0.245f),
                OptionsAmber,
                () =>
                {
                    DuelActivationPreferences.RestoreDefaults();
                    ShowDuelResponseOptions();
                },
                15);
            CreateArcaneActionButton(
                rail.transform,
                "VOLTAR ÀS OPÇÕES",
                new Vector2(0.08f, 0.055f),
                new Vector2(0.92f, 0.135f),
                OptionsViolet,
                ShowAnimationOptions,
                15);
        }

        private void BuildDuelResponseContent(Transform parent)
        {
            Image content = CreateArcaneSurface(
                parent,
                "Configuração de Respostas",
                new Vector2(0.30f, 0.035f),
                new Vector2(0.982f, 0.965f),
                OptionsViolet,
                false,
                0.64f);

            CreateText(
                content.transform,
                "CONFIRMAÇÃO DE ATIVAÇÃO",
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.905f),
                new Vector2(0.60f, 0.97f),
                TextAnchor.MiddleLeft);
            CreateText(
                content.transform,
                "Escolhas obrigatórias e alertas de conexão permanecem ativos",
                12,
                FontStyle.Bold,
                OptionsSoftText,
                new Vector2(0.55f, 0.91f),
                new Vector2(0.965f, 0.965f),
                TextAnchor.MiddleRight);

            BuildActivationModeCard(content.transform);
            BuildDuelResponseToggleCard(
                content.transform,
                "SELF CHAIN",
                "Permite responder aos seus próprios elos quando houver opção legal.",
                DuelActivationPreferences.SelfChainEnabled
                    ? "ATIVADO" : "DESATIVADO",
                DuelActivationPreferences.SelfChainEnabled,
                OptionsMint,
                new Vector2(0.035f, 0.355f),
                new Vector2(0.49f, 0.60f),
                () =>
                {
                    DuelActivationPreferences.SelfChainEnabled =
                        !DuelActivationPreferences.SelfChainEnabled;
                    ShowDuelResponseOptions();
                });
            BuildDuelResponseToggleCard(
                content.transform,
                "ORDEM SIMULTÂNEA",
                "MANUAL permite ordenar efeitos; CORE mantém a ordem autoritativa.",
                DuelActivationPreferences.ManualChainOrder
                    ? "MANUAL" : "CORE",
                DuelActivationPreferences.ManualChainOrder,
                OptionsViolet,
                new Vector2(0.51f, 0.355f),
                new Vector2(0.965f, 0.60f),
                () =>
                {
                    DuelActivationPreferences.ManualChainOrder =
                        !DuelActivationPreferences.ManualChainOrder;
                    ShowDuelResponseOptions();
                });
            BuildDuelResponseToggleCard(
                content.transform,
                "AVISOS DE ORIENTAÇÃO",
                "Oculta a faixa azul grande no topo. Alvos e escolhas continuam disponíveis.",
                DuelActivationPreferences.GuidanceMessagesEnabled
                    ? "VISÍVEIS" : "OCULTOS",
                DuelActivationPreferences.GuidanceMessagesEnabled,
                OptionsMint,
                new Vector2(0.035f, 0.075f),
                new Vector2(0.49f, 0.32f),
                () =>
                {
                    DuelActivationPreferences.GuidanceMessagesEnabled =
                        !DuelActivationPreferences.GuidanceMessagesEnabled;
                    ShowDuelResponseOptions();
                });
            BuildDuelResponseToggleCard(
                content.transform,
                "PAINEL DE CORRENTE",
                "Oculta o quadro vermelho de CL. A corrente e seus efeitos seguem funcionando.",
                DuelActivationPreferences.ChainPanelEnabled
                    ? "VISÍVEL" : "OCULTO",
                DuelActivationPreferences.ChainPanelEnabled,
                OptionsAmber,
                new Vector2(0.51f, 0.075f),
                new Vector2(0.965f, 0.32f),
                () =>
                {
                    DuelActivationPreferences.ChainPanelEnabled =
                        !DuelActivationPreferences.ChainPanelEnabled;
                    ShowDuelResponseOptions();
                });
        }

        private void BuildActivationModeCard(Transform parent)
        {
            Image card = CreateArcaneSurface(
                parent,
                "Modo de confirmação",
                new Vector2(0.035f, 0.635f),
                new Vector2(0.965f, 0.885f),
                OptionsViolet,
                true,
                0.72f);
            CreateText(
                card.transform,
                "JANELAS OPCIONAIS",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.53f),
                new Vector2(0.33f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                "ON exibe • AUTO padrão • OFF passa\n1×/FASE evita repetição",
                11,
                FontStyle.Normal,
                OptionsSoftText,
                new Vector2(0.025f, 0.13f),
                new Vector2(0.32f, 0.52f),
                TextAnchor.MiddleLeft);

            CreateOptionsChoiceButton(
                card.transform,
                DuelActivationPreferences.ResponseWindowRhythmName,
                new Vector2(0.34f, 0.20f),
                new Vector2(0.50f, 0.80f),
                true,
                OptionsMint,
                () =>
                {
                    DuelActivationPreferences.ClassicResponseWindows =
                        !DuelActivationPreferences.ClassicResponseWindows;
                    ShowDuelResponseOptions();
                },
                12);

            ActivationPromptMode[] modes =
            {
                ActivationPromptMode.On,
                ActivationPromptMode.Auto,
                ActivationPromptMode.Off
            };
            const float start = 0.53f;
            const float width = 0.13f;
            const float gap = 0.018f;
            for (int index = 0; index < modes.Length; index++)
            {
                ActivationPromptMode mode = modes[index];
                float xMin = start + index * (width + gap);
                CreateOptionsChoiceButton(
                    card.transform,
                    DuelActivationPreferences.DisplayName(mode),
                    new Vector2(xMin, 0.20f),
                    new Vector2(xMin + width, 0.80f),
                    DuelActivationPreferences.Mode == mode,
                    OptionsViolet,
                    () =>
                    {
                        DuelActivationPreferences.Mode = mode;
                        ShowDuelResponseOptions();
                    },
                    14);
            }
        }

        private static void BuildDuelResponseToggleCard(
            Transform parent,
            string title,
            string description,
            string value,
            bool enabled,
            Color accent,
            Vector2 min,
            Vector2 max,
            Action toggle)
        {
            Image card = CreateArcaneSurface(
                parent,
                title,
                min,
                max,
                enabled ? accent : Danger,
                false,
                0.60f);
            CreateText(
                card.transform,
                title,
                16,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.045f, 0.58f),
                new Vector2(0.64f, 0.88f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                description,
                11,
                FontStyle.Normal,
                OptionsSoftText,
                new Vector2(0.045f, 0.12f),
                new Vector2(0.64f, 0.56f),
                TextAnchor.UpperLeft);
            CreateOptionsChoiceButton(
                card.transform,
                value,
                new Vector2(0.68f, 0.22f),
                new Vector2(0.955f, 0.78f),
                enabled,
                accent,
                toggle,
                13);
        }

        private static Image CreateOptionsChoiceButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            bool selected,
            Color accent,
            Action action,
            int fontSize = 14)
        {
            Image button = CreateArcaneSurface(
                parent,
                $"Opção {label}",
                min,
                max,
                selected ? accent : ArcaneCyan,
                selected,
                selected ? 0.92f : 0.45f);
            AddButtonBehaviour(button, action);
            Button behaviour = button.GetComponent<Button>();
            ArcanePanelSheenGraphic sheen =
                button.GetComponentInChildren<ArcanePanelSheenGraphic>();
            if (behaviour != null && sheen != null)
                behaviour.targetGraphic = sheen;
            CreateText(
                button.transform,
                selected ? "◆  " + label : label,
                fontSize,
                FontStyle.Bold,
                selected ? Color.white : OptionsSoftText,
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleCenter);
            return button;
        }

        private void ResetAllPresentationPreferences()
        {
            DuelAnimationPreferences.ResetToDefaults();
            DuelActivationPreferences.RestoreDefaults();
            ArcaneAudioPreferences.ResetToDefaults();
            ArcaneMusicPreferences.ResetToDefaults();
            ArcaneGraphicsPreferences.ResetToAutomatic();
            ArcaneUiTextPreferences.ResetToDefault();
            ShowAnimationOptions();
        }
    }
}
