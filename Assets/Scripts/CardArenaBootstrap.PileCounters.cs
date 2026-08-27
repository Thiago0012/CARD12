using System;
using System.Collections.Generic;
using ArcaneArena.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    /// <summary>
    /// Mantém os totais públicos das pilhas privadas presos à arena 3D.
    /// Os valores vêm do estado autoritativo do duelo; esta camada nunca
    /// inspeciona a identidade das cartas ocultas nem calcula o total por conta
    /// própria.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private static readonly Color PileCounterColor =
            new(0.96f, 0.93f, 0.84f, 1f);

        private sealed class PileCounterVisual
        {
            public DuelZone3D Zone;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Text Count;
            public bool IsExtraDeck;
            public bool Hovered;
            public bool Visible;
            public int DisplayedCount = int.MinValue;
            public float PulseUntil;
        }

        private readonly Dictionary<string, PileCounterVisual> pileCounters =
            new(StringComparer.Ordinal);

        private void BuildPileCounterPresentation()
        {
            DisposePileCounterPresentation();
            if (frame == null)
                return;

            foreach (DuelZone3D zone in AllZones())
            {
                if (zone == null ||
                    (zone.Kind != DuelZoneKind.MainDeck &&
                     zone.Kind != DuelZoneKind.ExtraDeck))
                {
                    continue;
                }
                if (!zone.HasValidIdentity &&
                    !zone.EnsureIdentityFromHierarchy(false))
                {
                    continue;
                }

                bool extra = zone.Kind == DuelZoneKind.ExtraDeck;
                var root = new GameObject(
                    extra ? "Contador do Deck Adicional" : "Contador do Deck",
                    typeof(RectTransform),
                    typeof(CanvasGroup));
                root.transform.SetParent(frame, false);
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(48f, 32f);

                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                group.alpha = 0f;

                Text count = CreateText(
                    root.transform,
                    "0",
                    27,
                    FontStyle.Bold,
                    PileCounterColor,
                    Vector2.zero,
                    Vector2.one,
                    TextAnchor.MiddleCenter);
                count.raycastTarget = false;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Overflow;
                Shadow countShadow = count.gameObject.AddComponent<Shadow>();
                countShadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
                countShadow.effectDistance = new Vector2(1.2f, -1.2f);
                countShadow.useGraphicAlpha = true;

                pileCounters[zone.StableId] = new PileCounterVisual
                {
                    Zone = zone,
                    Rect = rect,
                    Group = group,
                    Count = count,
                    IsExtraDeck = extra
                };
            }
        }

        private void EnsurePileCounterPresentation()
        {
            if (frame == null)
                return;

            bool hasPile = false;
            bool rebuild = false;
            int expectedPileCount = 0;
            foreach (DuelZone3D zone in AllZones())
            {
                if (zone == null ||
                    (zone.Kind != DuelZoneKind.MainDeck &&
                     zone.Kind != DuelZoneKind.ExtraDeck))
                {
                    continue;
                }
                hasPile = true;
                expectedPileCount++;
                if (!pileCounters.TryGetValue(
                        zone.StableId,
                        out PileCounterVisual visual) ||
                    visual == null || visual.Zone != zone ||
                    visual.Rect == null)
                {
                    rebuild = true;
                    break;
                }
            }

            if ((hasPile && pileCounters.Count != expectedPileCount) ||
                rebuild)
            {
                BuildPileCounterPresentation();
                RefreshPileCounterPresentation(true);
            }
        }

        private void DisposePileCounterPresentation()
        {
            foreach (PileCounterVisual visual in pileCounters.Values)
            {
                if (visual?.Rect != null)
                    Destroy(visual.Rect.gameObject);
            }
            pileCounters.Clear();
        }

        private void RefreshPileCounterPresentation(bool force)
        {
            if (state == null)
                return;

            foreach (PileCounterVisual visual in pileCounters.Values)
            {
                if (visual?.Zone == null || visual.Count == null)
                    continue;
                int owner = StatePlayerForZone(visual.Zone);
                int count = visual.IsExtraDeck
                    ? state.Players[owner].ExtraDeckCount
                    : state.Players[owner].DeckCount;
                count = Mathf.Max(0, count);
                if (!force && visual.DisplayedCount == count)
                    continue;

                bool changed = visual.DisplayedCount != int.MinValue &&
                               visual.DisplayedCount != count;
                visual.DisplayedCount = count;
                visual.Count.text = count.ToString();
                UpdatePileCounterStyle(visual);
                if (changed)
                    visual.PulseUntil = Time.unscaledTime + 0.28f;
            }
        }

        private void UpdatePileCounterPresentation()
        {
            if (pileCounters.Count == 0 || frame == null)
                return;

            Camera camera = Camera.main;
            foreach (PileCounterVisual visual in pileCounters.Values)
            {
                if (visual?.Rect == null || visual.Group == null ||
                    visual.Zone == null || camera == null)
                {
                    SetPileCounterVisible(visual, false);
                    continue;
                }

                Transform anchor = visual.Zone.CardPresentationAnchor;
                // O ponto de leitura usa o eixo central do verso da carta.
                // A rotação do monte já espelha o avanço superior para o
                // adversário; não aplicamos desvio lateral, pois ele levava
                // o número à quina da carta.
                Vector3 numberWorldPosition = anchor.position +
                    anchor.forward * 0.55f +
                    Vector3.up * 0.22f;
                Vector3 screenPoint = camera.WorldToScreenPoint(
                    numberWorldPosition);
                if (screenPoint.z <= 0.01f)
                {
                    SetPileCounterVisible(visual, false);
                    continue;
                }

                Vector2 screen = screenPoint;
                // Correção fina de -10 px solicitada para a leitura permanecer
                // presa à perspectiva do verso da carta, não à tela inteira.
                screen.y -= 10f;
                // No lado inferior, o ponto correto é a borda superior
                // central do verso. A elevação é menor que a do ajuste
                // anterior para não jogar o número para fora da carta.
                if (StatePlayerForZone(visual.Zone) == 0)
                    screen += Vector2.up * 30f;

                if (!TryScreenToFrameLocal(screen, out Vector2 local))
                {
                    SetPileCounterVisible(visual, false);
                    continue;
                }

                Rect bounds = frame.rect;
                const float horizontalPadding = 28f;
                const float verticalPadding = 18f;
                if (local.x < bounds.xMin - horizontalPadding ||
                    local.x > bounds.xMax + horizontalPadding ||
                    local.y < bounds.yMin - verticalPadding ||
                    local.y > bounds.yMax + verticalPadding)
                {
                    SetPileCounterVisible(visual, false);
                    continue;
                }

                local.x = Mathf.Clamp(
                    local.x,
                    bounds.xMin + horizontalPadding,
                    bounds.xMax - horizontalPadding);
                local.y = Mathf.Clamp(
                    local.y,
                    bounds.yMin + verticalPadding,
                    bounds.yMax - verticalPadding);
                visual.Rect.anchoredPosition = local;
                SetPileCounterVisible(visual, true);

                float pulse = visual.PulseUntil > Time.unscaledTime
                    ? Mathf.Sin((visual.PulseUntil - Time.unscaledTime) *
                                 Mathf.PI / 0.28f) * 0.12f
                    : 0f;
                float hover = visual.Hovered ? 0.06f : 0f;
                visual.Rect.localScale = Vector3.one * (1f + pulse + hover);
                visual.Group.alpha = visual.Hovered ? 1f : 0.86f;
            }
        }

        private void SetPileCounterHovered(DuelZone3D zone, bool hovered)
        {
            if (zone == null ||
                (zone.Kind != DuelZoneKind.MainDeck &&
                 zone.Kind != DuelZoneKind.ExtraDeck) ||
                !pileCounters.TryGetValue(zone.StableId, out
                    PileCounterVisual visual) || visual == null)
            {
                return;
            }
            visual.Hovered = hovered;
        }

        private static void SetPileCounterVisible(
            PileCounterVisual visual,
            bool visible)
        {
            if (visual == null || visual.Group == null)
                return;
            if (visual.Visible == visible)
                return;
            visual.Visible = visible;
            visual.Group.alpha = visible ? 0.86f : 0f;
        }

        private static void UpdatePileCounterStyle(PileCounterVisual visual)
        {
            if (visual.Count != null)
                visual.Count.color = PileCounterColor;
        }
    }
}
