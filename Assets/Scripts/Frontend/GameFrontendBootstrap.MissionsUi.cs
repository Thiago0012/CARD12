using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private bool _missionsUiVisible;
        private bool _missionSyncInProgress;
        private bool _missionSyncRequestedForView;
        private bool _missionClaimInProgress;
        private Text _missionCountdownText;
        private Text _missionStatusText;
        private float _nextMissionCountdownRefresh;
        private string _missionFeedback = string.Empty;
        private bool _missionFeedbackIsError;

        private void EnsureMissionsReadyInBackground()
        {
            if (_repository == null || _missionSyncInProgress)
                return;
            _ = SynchronizeMissionsAsync(false);
        }

        private void ShowMissionsScreen()
        {
            if (_screenRoot == null)
                return;
            SetDuelPresentation(false);
            ClearScreen();
            _missionsUiVisible = true;
            BuildSharedBackground("CRÔNICAS DO DUELO");
            BuildHeader("MISSÕES", ShowMainMenu);

            CreateText(
                _screenRoot,
                "CICLO DE 48 HORAS",
                17,
                FontStyle.Bold,
                Gold,
                new Vector2(0.69f, 0.91f),
                new Vector2(0.94f, 0.975f),
                TextAnchor.MiddleRight);

            Image cyclePanel = CreateArcaneSurface(
                _screenRoot,
                "Estado do Ciclo",
                new Vector2(0.055f, 0.782f),
                new Vector2(0.945f, 0.862f),
                Cyan,
                true,
                0.76f);
            _missionStatusText = CreateText(
                cyclePanel.transform,
                MissionStatusLabel(),
                16,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.12f),
                new Vector2(0.61f, 0.88f),
                TextAnchor.MiddleLeft);
            _missionCountdownText = CreateText(
                cyclePanel.transform,
                "SINCRONIZANDO...",
                19,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.62f, 0.12f),
                new Vector2(0.79f, 0.88f),
                TextAnchor.MiddleRight);
            CreateText(
                cyclePanel.transform,
                $"MOEDAS  {_repository?.CoinBalance ?? 0:N0}",
                18,
                FontStyle.Bold,
                Gold,
                new Vector2(0.805f, 0.12f),
                new Vector2(0.975f, 0.88f),
                TextAnchor.MiddleRight);

            MissionState state = _repository?.Missions;
            IReadOnlyList<MissionProgressState> missions =
                state?.missions != null
                    ? state.missions
                    : Array.Empty<MissionProgressState>();
            if (missions.Count == 0)
            {
                BuildMissionEmptyState();
            }
            else
            {
                int count = Math.Min(5, missions.Count);
                for (int index = 0; index < count; index++)
                    BuildMissionCard(missions[index], index, state);
            }

            Color feedbackColor = _missionFeedbackIsError ? Danger : Muted;
            CreateText(
                _screenRoot,
                string.IsNullOrWhiteSpace(_missionFeedback)
                    ? "O progresso usa somente eventos confirmados do duelo."
                    : _missionFeedback,
                14,
                FontStyle.Normal,
                feedbackColor,
                new Vector2(0.075f, 0.035f),
                new Vector2(0.925f, 0.092f),
                TextAnchor.MiddleCenter);

            UpdateMissionCountdown(true);
            if (!_missionSyncRequestedForView)
            {
                _missionSyncRequestedForView = true;
                _ = SynchronizeMissionsAsync(true);
            }
        }

        private void BuildMissionEmptyState()
        {
            Image panel = CreateArcaneSurface(
                _screenRoot,
                "Missões aguardando validação",
                new Vector2(0.16f, 0.25f),
                new Vector2(0.84f, 0.70f),
                Gold,
                true,
                0.78f);
            CreateText(
                panel.transform,
                "MISSÕES AGUARDANDO SINCRONIZAÇÃO",
                27,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.60f),
                new Vector2(0.92f, 0.82f),
                TextAnchor.MiddleCenter);
            CreateText(
                panel.transform,
                "Conecte-se ao servidor para validar o horário e receber " +
                "as cinco missões deste ciclo. O relógio local não troca " +
                "missões nem libera recompensas.",
                18,
                FontStyle.Normal,
                Muted,
                new Vector2(0.12f, 0.26f),
                new Vector2(0.88f, 0.58f),
                TextAnchor.MiddleCenter);
            if (!_missionSyncInProgress)
            {
                CreateArcaneActionButton(
                    panel.transform,
                    "TENTAR NOVAMENTE",
                    new Vector2(0.31f, 0.07f),
                    new Vector2(0.69f, 0.22f),
                    Cyan,
                    () =>
                    {
                        _missionSyncRequestedForView = true;
                        _ = SynchronizeMissionsAsync(true);
                    },
                    16);
            }
        }

        private void BuildMissionCard(
            MissionProgressState mission,
            int index,
            MissionState state)
        {
            if (mission == null)
                return;
            float top = 0.752f - index * 0.13f;
            float bottom = top - 0.113f;
            Color tierColor = MissionTierColor(mission.tier);
            Image card = CreateArcaneSurface(
                _screenRoot,
                "Missão " + mission.displayName,
                new Vector2(0.055f, bottom),
                new Vector2(0.945f, top),
                tierColor,
                mission.completed && !mission.rewardClaimed,
                mission.rewardClaimed ? 0.46f : 0.76f);

            CreateText(
                card.transform,
                TierLabel(mission.tier),
                13,
                FontStyle.Bold,
                tierColor,
                new Vector2(0.018f, 0.55f),
                new Vector2(0.12f, 0.88f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                ScopeLabel(mission.scope),
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.018f, 0.15f),
                new Vector2(0.12f, 0.50f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                mission.displayName.ToUpperInvariant(),
                18,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.135f, 0.53f),
                new Vector2(0.50f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                card.transform,
                mission.description,
                14,
                FontStyle.Normal,
                Muted,
                new Vector2(0.135f, 0.14f),
                new Vector2(0.50f, 0.53f),
                TextAnchor.MiddleLeft);

            Image progressTrack = CreatePanel(
                card.transform,
                "Trilho do Progresso",
                new Vector2(0.515f, 0.30f),
                new Vector2(0.735f, 0.47f),
                new Color(0.01f, 0.025f, 0.045f, 0.96f));
            progressTrack.raycastTarget = false;
            Image progress = CreatePanel(
                progressTrack.transform,
                "Progresso",
                Vector2.zero,
                Vector2.one,
                tierColor);
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillOrigin = 0;
            progress.fillAmount = mission.targetValue <= 0
                ? 0f
                : Mathf.Clamp01((float)mission.currentValue /
                                mission.targetValue);
            progress.raycastTarget = false;
            CreateText(
                card.transform,
                $"{mission.currentValue:N0} / {mission.targetValue:N0}",
                14,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.515f, 0.49f),
                new Vector2(0.735f, 0.82f),
                TextAnchor.MiddleCenter);
            CreateText(
                card.transform,
                $"+{mission.rewardCoins} MOEDAS",
                15,
                FontStyle.Bold,
                Gold,
                new Vector2(0.745f, 0.18f),
                new Vector2(0.825f, 0.82f),
                TextAnchor.MiddleCenter);

            string actionLabel = mission.rewardClaimed
                ? "RESGATADA"
                : mission.completed
                    ? "RESGATAR"
                    : "EM PROGRESSO";
            Image action = CreateArcaneActionButton(
                card.transform,
                actionLabel,
                new Vector2(0.83f, 0.20f),
                new Vector2(0.98f, 0.80f),
                mission.completed ? Gold : Cyan,
                () => ClaimMission(mission.missionInstanceId),
                14);
            Button button = action.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = mission.completed &&
                                      !mission.rewardClaimed &&
                                      state.timeValidated &&
                                      !_missionClaimInProgress;
            }
        }

        private async Task SynchronizeMissionsAsync(bool redraw)
        {
            if (_repository == null || _missionSyncInProgress)
                return;
            _missionSyncInProgress = true;
            if (redraw)
            {
                _missionFeedback = "Validando o ciclo com o servidor...";
                _missionFeedbackIsError = false;
            }
            try
            {
                string rejection = string.Empty;
                bool validated = await PlayerIdAccessRuntime
                    .ValidateAuthoritativeTimeAsync();
                if (validated && PlayerIdAccessRuntime.TryGetAuthoritativeUtc(
                        out long now,
                        out string source) &&
                    _repository.TryRefreshMissionCycle(
                        now,
                        MissionCatalog.LoadRuntime(),
                        out _,
                        out rejection))
                {
                    _missionFeedback = "Ciclo validado por " + source + ".";
                    _missionFeedbackIsError = false;
                }
                else
                {
                    _repository.MarkMissionTimeUnvalidated();
                    _missionFeedback = string.IsNullOrWhiteSpace(rejection)
                        ? "Sem horário autoritativo: o ciclo atual foi preservado."
                        : rejection;
                    _missionFeedbackIsError = true;
                }
            }
            catch (Exception exception)
            {
                _repository.MarkMissionTimeUnvalidated();
                _missionFeedback = "Não foi possível validar o ciclo: " +
                    exception.GetBaseException().Message;
                _missionFeedbackIsError = true;
            }
            finally
            {
                _missionSyncInProgress = false;
                if (this != null && _missionsUiVisible)
                    ShowMissionsScreen();
            }
        }

        private void ClaimMission(string missionInstanceId)
        {
            if (_missionClaimInProgress || _repository == null)
                return;
            if (!PlayerIdAccessRuntime.TryGetAuthoritativeUtc(
                    out long now,
                    out _))
            {
                _missionFeedback = "Valide o horário antes de resgatar.";
                _missionFeedbackIsError = true;
                ShowMissionsScreen();
                return;
            }
            _missionClaimInProgress = true;
            try
            {
                if (_repository.TryClaimMissionReward(
                        missionInstanceId,
                        now,
                        out ShopTransactionRecord receipt,
                        out string rejection))
                {
                    _missionFeedback = receipt == null
                        ? "Recompensa já processada."
                        : $"Recompensa recebida: +{receipt.coinDelta} moedas.";
                    _missionFeedbackIsError = false;
                }
                else
                {
                    _missionFeedback = rejection;
                    _missionFeedbackIsError = true;
                }
            }
            finally
            {
                _missionClaimInProgress = false;
                ShowMissionsScreen();
            }
        }

        private void UpdateMissionCountdown(bool force = false)
        {
            if (!_missionsUiVisible || _missionCountdownText == null ||
                (!force && Time.unscaledTime < _nextMissionCountdownRefresh))
            {
                return;
            }
            _nextMissionCountdownRefresh = Time.unscaledTime + 0.25f;
            MissionState state = _repository?.Missions;
            if (state == null || !state.timeValidated ||
                !PlayerIdAccessRuntime.TryGetAuthoritativeUtc(
                    out long now,
                    out _))
            {
                _missionCountdownText.text = "TEMPO NÃO VALIDADO";
                _missionCountdownText.color = Danger;
                if (_missionStatusText != null)
                    _missionStatusText.text = MissionStatusLabel();
                return;
            }
            long end = new DateTimeOffset(new DateTime(
                    state.cycleEndUtcTicks,
                    DateTimeKind.Utc))
                .ToUnixTimeSeconds();
            long remaining = Math.Max(0, end - now);
            long hours = remaining / 3600;
            long minutes = remaining % 3600 / 60;
            long seconds = remaining % 60;
            _missionCountdownText.text =
                $"{hours:00}:{minutes:00}:{seconds:00}";
            _missionCountdownText.color = remaining <= 3600 ? Gold : Cyan;
            if (_missionStatusText != null)
                _missionStatusText.text = MissionStatusLabel();
            if (remaining == 0 && !_missionSyncInProgress)
                _ = SynchronizeMissionsAsync(true);
        }

        private string MissionStatusLabel()
        {
            MissionState state = _repository?.Missions;
            if (_missionSyncInProgress)
                return "VALIDANDO HORÁRIO DO SERVIDOR";
            if (state?.missions == null || state.missions.Count == 0)
                return "CICLO AINDA NÃO CARREGADO";
            if (!state.timeValidated)
                return "CICLO PRESERVADO · TEMPO NÃO VALIDADO";
            int claimed = 0;
            int completed = 0;
            foreach (MissionProgressState mission in state.missions)
            {
                if (mission.rewardClaimed)
                    claimed++;
                else if (mission.completed)
                    completed++;
            }
            return $"5 MISSÕES · {completed} PARA RESGATAR · " +
                   $"{claimed} RESGATADAS";
        }

        private static Color MissionTierColor(MissionTier tier)
        {
            return tier switch
            {
                MissionTier.Tier1 => new Color(0.18f, 0.84f, 0.95f, 1f),
                MissionTier.Tier2 => new Color(0.40f, 0.55f, 1f, 1f),
                MissionTier.Tier3 => new Color(0.96f, 0.72f, 0.24f, 1f),
                _ => Cyan
            };
        }

        private static string TierLabel(MissionTier tier) => tier switch
        {
            MissionTier.Tier1 => "MISSÃO I",
            MissionTier.Tier2 => "MISSÃO II",
            MissionTier.Tier3 => "MISSÃO III",
            _ => "MISSÃO"
        };

        private static string ScopeLabel(MissionScope scope) => scope switch
        {
            MissionScope.Global => "QUALQUER DUELO",
            MissionScope.OnlineAny => "ONLINE",
            MissionScope.OnlineRanked => "RANQUEADO",
            MissionScope.OnlineTournament => "TORNEIO",
            MissionScope.StoryRoguelite => "CRÔNICAS",
            MissionScope.Collection => "CONTA",
            _ => string.Empty
        };

        /// <summary>
        /// Returns detached mission values for result presentation. The
        /// result screen can never mutate mission state or claim rewards.
        /// </summary>
        public IReadOnlyList<MissionProgressState> CaptureMissionProgress(
            int maximum = 3)
        {
            if (_repository?.Missions?.missions == null || maximum <= 0)
                return Array.Empty<MissionProgressState>();

            return _repository.Missions.missions
                .Where(mission => mission != null && !mission.rewardClaimed)
                .OrderByDescending(mission => mission.completed)
                .ThenByDescending(mission =>
                    mission.targetValue <= 0
                        ? 0d
                        : mission.currentValue / (double)mission.targetValue)
                .ThenBy(mission => mission.tier)
                .Take(maximum)
                .Select(mission => new MissionProgressState
                {
                    missionInstanceId = mission.missionInstanceId,
                    definitionId = mission.definitionId,
                    displayName = mission.displayName,
                    description = mission.description,
                    tier = mission.tier,
                    scope = mission.scope,
                    metric = mission.metric,
                    currentValue = mission.currentValue,
                    targetValue = mission.targetValue,
                    rewardCoins = mission.rewardCoins,
                    completed = mission.completed,
                    rewardClaimed = mission.rewardClaimed
                })
                .ToArray();
        }
    }
}
