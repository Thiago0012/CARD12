using System;
using System.Collections.Generic;
using ArcaneArena.Frontend;
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
        private sealed class PileCounterVisual
        {
            public DuelZone3D Zone;
            public RectTransform Rect;
            public CanvasGroup Group;
            public DuelHudSurfaceGraphic Surface;
            public Image AccentBar;
            public Text Caption;
            public Text Count;
            public Color Accent;
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
                byte owner = StatePlayerForZone(zone);
                Color accent = extra
                    ? Gold
                    : owner == 0 ? Cyan : Red;
                var root = new GameObject(
                    extra ? "Contador do Deck Adicional" : "Contador do Deck",
                    typeof(RectTransform),
                    typeof(CanvasGroup));
                root.transform.SetParent(frame, false);
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = extra
                    ? new Vector2(88f, 48f)
                    : new Vector2(94f, 48f);

                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                group.alpha = 0f;

                GameObject surfaceObject = new(
                    "Superfície",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(DuelHudSurfaceGraphic));
                surfaceObject.transform.SetParent(root.transform, false);
                RectTransform surfaceRect =
                    surfaceObject.GetComponent<RectTransform>();
                surfaceRect.anchorMin = Vector2.zero;
                surfaceRect.anchorMax = Vector2.one;
                surfaceRect.offsetMin = Vector2.zero;
                surfaceRect.offsetMax = Vector2.zero;
                DuelHudSurfaceGraphic surface =
                    surfaceObject.GetComponent<DuelHudSurfaceGraphic>();
                surface.raycastTarget = false;
                surface.SetStyle(
                    accent,
                    owner == 0,
                    0.82f,
                    true,
                    7f);

                Image accentBar = CreateImage(
                    root.transform,
                    "Linha de Identidade",
                    new Vector2(0.08f, 0.18f),
                    new Vector2(0.105f, 0.82f),
                    new Color(accent.r, accent.g, accent.b, 0.92f));
                accentBar.raycastTarget = false;

                Text caption = CreateText(
                    root.transform,
                    extra ? "EXTRA" : "DECK",
                    8,
                    FontStyle.Bold,
                    new Color(0.78f, 0.88f, 0.94f, 0.94f),
                    new Vector2(0.16f, 0.49f),
                    new Vector2(0.95f, 0.88f),
                    TextAnchor.MiddleLeft);
                caption.raycastTarget = false;
                caption.horizontalOverflow = HorizontalWrapMode.Overflow;
                caption.verticalOverflow = VerticalWrapMode.Overflow;

                Text count = CreateText(
                    root.transform,
                    "0",
                    22,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.16f, 0.06f),
                    new Vector2(0.95f, 0.59f),
                    TextAnchor.MiddleLeft);
                count.raycastTarget = false;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Overflow;

                pileCounters[zone.StableId] = new PileCounterVisual
                {
                    Zone = zone,
                    Rect = rect,
                    Group = group,
                    Surface = surface,
                    AccentBar = accentBar,
                    Caption = caption,
                    Count = count,
                    Accent = accent,
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
                UpdatePileCounterStyle(visual, count);
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
                Vector3 screenPoint = camera.WorldToScreenPoint(
                    anchor.position + Vector3.up * 0.34f);
                if (screenPoint.z <= 0.01f)
                {
                    SetPileCounterVisible(visual, false);
                    continue;
                }

                Vector2 screen = screenPoint;
                Vector2 center = new(
                    Screen.width * 0.5f,
                    Screen.height * 0.5f);
                Vector2 direction = (screen - center).normalized;
                if (direction.sqrMagnitude < 0.001f)
                    direction = StatePlayerForZone(visual.Zone) == 0
                        ? new Vector2(0.8f, -0.6f)
                        : new Vector2(-0.8f, 0.6f);
                screen += direction * (visual.IsExtraDeck ? 34f : 40f);

                if (!TryScreenToFrameLocal(screen, out Vector2 local))
                {
                    SetPileCounterVisible(visual, false);
                    continue;
                }

                Rect bounds = frame.rect;
                const float horizontalPadding = 52f;
                const float verticalPadding = 32f;
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

        private void UpdatePileCounterStyle(
            PileCounterVisual visual,
            int count)
        {
            bool emptyMainDeck = !visual.IsExtraDeck && count == 0;
            Color accent = emptyMainDeck
                ? Red
                : visual.Accent;
            Color numberColor = emptyMainDeck
                ? new Color(1f, 0.43f, 0.49f, 1f)
                : visual.IsExtraDeck && count == 0
                    ? Muted
                    : Color.white;
            visual.Surface?.SetStyle(
                accent,
                StatePlayerForZone(visual.Zone) == 0,
                emptyMainDeck ? 0.96f : 0.82f,
                true,
                7f);
            if (visual.AccentBar != null)
            {
                visual.AccentBar.color = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    0.92f);
            }
            if (visual.Count != null)
                visual.Count.color = numberColor;
            if (visual.Caption != null)
            {
                visual.Caption.color = emptyMainDeck
                    ? new Color(1f, 0.68f, 0.72f, 0.98f)
                    : new Color(0.78f, 0.88f, 0.94f, 0.94f);
            }
        }
    }
}
