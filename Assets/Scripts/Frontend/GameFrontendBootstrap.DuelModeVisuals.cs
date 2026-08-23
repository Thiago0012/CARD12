using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private static readonly Color DuelOfflineAccent =
            new(0.08f, 0.82f, 0.94f, 1f);
        private static readonly Color DuelRankedAccent =
            new(0.20f, 0.48f, 1f, 1f);
        private static readonly Color DuelCasualAccent =
            new(1f, 0.30f, 0.16f, 1f);
        private static readonly Color DuelRankedRoomAccent =
            new(0.62f, 0.32f, 1f, 1f);

        private Image BuildDuelModeBackground(
            string modeLabel,
            Color accent)
        {
            Image root = CreatePanel(
                _screenRoot,
                $"Fundo moderno {modeLabel}",
                Vector2.zero,
                Vector2.one,
                Color.clear);
            root.transform.SetAsFirstSibling();

            GameObject backdropObject = new(
                "Geometria da Central de Duelos",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelModeBackdropGraphic));
            backdropObject.transform.SetParent(root.transform, false);
            Stretch(backdropObject.GetComponent<RectTransform>());
            DuelModeBackdropGraphic backdrop =
                backdropObject.GetComponent<DuelModeBackdropGraphic>();
            backdrop.raycastTarget = false;
            backdrop.SetAccent(accent);

            Image topRail = CreateArcaneSurface(
                root.transform,
                "Barra superior do modo",
                new Vector2(0.012f, 0.875f),
                new Vector2(0.988f, 0.992f),
                accent,
                true,
                0.72f);
            topRail.raycastTarget = false;
            CreateText(
                topRail.transform,
                "MASTER DUEL 2 PLUS ULTRA  •  CENTRAL DE DUELOS",
                12,
                FontStyle.Bold,
                new Color(accent.r, accent.g, accent.b, 0.78f),
                new Vector2(0.57f, 0.12f),
                new Vector2(0.96f, 0.88f),
                TextAnchor.MiddleRight);
            return root;
        }

        private void BuildDuelModeHeader(
            string title,
            string eyebrow,
            Color accent,
            Action backAction)
        {
            CreateArcaneActionButton(
                _screenRoot,
                "‹",
                new Vector2(0.026f, 0.902f),
                new Vector2(0.073f, 0.972f),
                accent,
                backAction,
                26);
            CreateText(
                _screenRoot,
                title,
                31,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.088f, 0.917f),
                new Vector2(0.58f, 0.973f),
                TextAnchor.MiddleLeft);
            CreateText(
                _screenRoot,
                eyebrow,
                12,
                FontStyle.Bold,
                new Color(accent.r, accent.g, accent.b, 0.88f),
                new Vector2(0.088f, 0.886f),
                new Vector2(0.58f, 0.923f),
                TextAnchor.MiddleLeft);
        }

        private static Image CreateDuelModeSurface(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color accent,
            bool raised = false,
            float opacity = 1f)
        {
            return CreateArcaneSurface(
                parent,
                name,
                min,
                max,
                accent,
                raised,
                opacity);
        }

        private static void TintDuelModeScrollGrid(
            RectTransform content,
            Color accent)
        {
            if (content == null || content.parent == null)
                return;

            Image viewport = content.parent.GetComponent<Image>();
            if (viewport != null)
            {
                viewport.color = new Color(0.002f, 0.012f, 0.022f, 0.86f);
                AddOutline(
                    viewport.gameObject,
                    new Color(accent.r, accent.g, accent.b, 0.34f),
                    new Vector2(1.5f, -1.5f));
            }

            Scrollbar scrollbar =
                content.parent.GetComponent<ScrollRect>()?.verticalScrollbar;
            if (scrollbar != null && scrollbar.targetGraphic != null)
            {
                scrollbar.targetGraphic.color = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    0.92f);
            }
        }
    }
}
