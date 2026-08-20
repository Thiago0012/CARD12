using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Vocabulário visual compartilhado das telas Arcane. A arte fornece
    /// material e ornamentação; estes componentes mantêm dados, estados e
    /// interação inteiramente dinâmicos e responsivos.
    /// </summary>
    public sealed partial class GameFrontendBootstrap
    {
        private const string ArcaneProfileFrameResourcePath =
            "Frontend/Profile/ProfileArcaneFrame";

        private static readonly Color ArcaneGold =
            new(0.78f, 0.58f, 0.29f, 1f);
        private static readonly Color ArcaneCyan =
            new(0.12f, 0.75f, 0.88f, 1f);

        private bool BuildArcaneProfileBackground()
        {
            Texture2D texture = Resources.Load<Texture2D>(
                ArcaneProfileFrameResourcePath);
            if (texture == null)
            {
                BuildSharedBackground("PERFIL DO DUELISTA");
                return false;
            }

            RawImage artwork = CreateFullCanvasArtwork(
                "Moldura Arcane do Perfil",
                texture);
            artwork.transform.SetAsFirstSibling();
            artwork.color = Color.white;
            return true;
        }

        private void BuildArcaneProfileHeader(Action backAction)
        {
            CreateArcaneActionButton(
                _screenRoot,
                "‹",
                new Vector2(0.022f, 0.918f),
                new Vector2(0.067f, 0.978f),
                ArcaneGold,
                backAction,
                25);
            CreateText(
                _screenRoot,
                "PERFIL",
                31,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.078f, 0.91f),
                new Vector2(0.30f, 0.982f),
                TextAnchor.MiddleLeft);
        }

        private static Image CreateArcaneSurface(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color accent,
            bool raised = false,
            float opacity = 1f)
        {
            Image panel = CreatePanel(parent, name, min, max, Color.clear);
            GameObject sheenObject = new(
                "Superfície Graduada",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ArcanePanelSheenGraphic));
            sheenObject.transform.SetParent(panel.transform, false);
            RectTransform sheenRect = sheenObject.GetComponent<RectTransform>();
            sheenRect.anchorMin = Vector2.zero;
            sheenRect.anchorMax = Vector2.one;
            sheenRect.offsetMin = Vector2.zero;
            sheenRect.offsetMax = Vector2.zero;
            ArcanePanelSheenGraphic sheen =
                sheenObject.GetComponent<ArcanePanelSheenGraphic>();
            sheen.raycastTarget = false;
            sheen.SetStyle(accent, raised, opacity);
            return panel;
        }

        private static Image CreateArcaneActionButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action action,
            int fontSize = 18)
        {
            Image button = CreateArcaneSurface(
                parent,
                $"Ação Arcane {label}",
                min,
                max,
                accent,
                true,
                0.88f);
            AddButtonBehaviour(button, action);
            Button behaviour = button.GetComponent<Button>();
            ArcanePanelSheenGraphic sheen =
                button.GetComponentInChildren<ArcanePanelSheenGraphic>();
            if (behaviour != null && sheen != null)
                behaviour.targetGraphic = sheen;
            CreateText(
                button.transform,
                label,
                fontSize,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter);
            return button;
        }

        private static Image CreateArcaneTabButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            bool selected,
            Action action)
        {
            Color accent = selected ? ArcaneGold : ArcaneCyan;
            Image button = CreateArcaneSurface(
                parent,
                $"Aba Arcane {label}",
                min,
                max,
                accent,
                selected,
                selected ? 0.84f : 0.54f);
            AddButtonBehaviour(button, action);
            Button behaviour = button.GetComponent<Button>();
            ArcanePanelSheenGraphic sheen =
                button.GetComponentInChildren<ArcanePanelSheenGraphic>();
            if (behaviour != null && sheen != null)
                behaviour.targetGraphic = sheen;
            CreateText(
                button.transform,
                label,
                16,
                FontStyle.Bold,
                selected ? new Color(0.96f, 0.86f, 0.68f, 1f) : Color.white,
                new Vector2(0.06f, 0.08f),
                new Vector2(0.94f, 0.92f),
                TextAnchor.MiddleCenter);
            CreatePanel(
                button.transform,
                selected ? "Seleção Dourada" : "Energia Ciano",
                new Vector2(0.22f, 0.02f),
                new Vector2(0.78f, selected ? 0.055f : 0.035f),
                new Color(accent.r, accent.g, accent.b, selected ? 0.95f : 0.35f))
                .raycastTarget = false;
            return button;
        }

    }
}
