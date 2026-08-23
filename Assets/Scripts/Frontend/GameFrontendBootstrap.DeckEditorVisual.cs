using System;
using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Acabamento visual da mesa de edição. A estrutura funcional continua
    /// pertencendo ao editor existente; esta camada uniformiza superfícies,
    /// contraste e hierarquia com a identidade esmeralda da Oficina de Decks.
    /// </summary>
    public sealed partial class GameFrontendBootstrap
    {
        private void ApplyDeckWorkshopEditorVisuals(
            DeckRecord deck,
            Image detailsPanel,
            Image deckPanel,
            Image collectionPanel,
            bool selected,
            bool legal)
        {
            RestyleDeckEditorBackground();

            Color deckAccent = selected ? DeckAmber : DeckEmerald;
            SkinDeckEditorSurface(
                detailsPanel,
                DeckEmerald,
                true,
                0.92f);
            SkinDeckEditorSurface(
                deckPanel,
                deckAccent,
                true,
                0.94f);
            SkinDeckEditorSurface(
                collectionPanel,
                DeckMint,
                true,
                0.92f);

            Transform craftBar = FindDescendantByName(
                _screenRoot,
                "Saldos de Craft Points");
            SkinDeckEditorSurface(
                craftBar != null ? craftBar.GetComponent<Image>() : null,
                DeckEmerald,
                false,
                0.88f);
            if (craftBar != null)
            {
                foreach (Image balance in
                         craftBar.GetComponentsInChildren<Image>(true))
                {
                    if (balance != null &&
                        balance.name.StartsWith(
                            "Saldo CP ",
                            StringComparison.Ordinal))
                    {
                        SkinDeckEditorSurface(
                            balance,
                            DeckMint,
                            false,
                            0.68f);
                    }
                }
            }

            Transform listTab = FindDescendantByName(
                collectionPanel != null
                    ? collectionPanel.transform
                    : null,
                "Aba Lista de Cartas");
            SkinDeckEditorSurface(
                listTab != null ? listTab.GetComponent<Image>() : null,
                DeckEmerald,
                true,
                0.86f);
            if (listTab != null)
            {
                Text tabLabel = listTab.GetComponentInChildren<Text>(true);
                if (tabLabel != null)
                    tabLabel.color = Color.white;
            }

            SkinNamedDeckEditorSurface(
                deckPanel != null ? deckPanel.transform : null,
                "Cabeçalho do Deck Principal",
                DeckEmerald,
                0.72f);
            SkinNamedDeckEditorSurface(
                deckPanel != null ? deckPanel.transform : null,
                "Cartas do Deck Principal",
                DeckMint,
                0.46f);
            SkinNamedDeckEditorSurface(
                deckPanel != null ? deckPanel.transform : null,
                "Cabeçalho do Deck Adicional",
                DeckMint,
                0.66f);
            SkinNamedDeckEditorSurface(
                deckPanel != null ? deckPanel.transform : null,
                "Cartas do Deck Adicional",
                DeckMint,
                0.42f);
            SkinNamedDeckEditorSurface(
                detailsPanel != null ? detailsPanel.transform : null,
                "Informações de combate do editor",
                DeckMint,
                0.52f);
            RestyleDeckEditorHeaderButtons(selected, legal);
            AddDeckEditorSectionIdentity(deck, selected);
        }

        private static void SkinNamedDeckEditorSurface(
            Transform root,
            string objectName,
            Color accent,
            float opacity)
        {
            Transform target = FindDescendantByName(root, objectName);
            SkinDeckEditorSurface(
                target != null ? target.GetComponent<Image>() : null,
                accent,
                false,
                opacity);
        }

        private void RestyleDeckEditorBackground()
        {
            Transform background = FindDescendantByName(
                _screenRoot,
                "Fundo");
            if (background == null)
                return;

            Image backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null)
                backgroundImage.color = DeckGraphite;

            Transform upper = FindDescendantByName(
                background,
                "Faixa Superior");
            if (upper != null)
            {
                Image upperImage = upper.GetComponent<Image>();
                if (upperImage != null)
                {
                    upperImage.color =
                        new Color(0.008f, 0.07f, 0.055f, 0.97f);
                }

                Text section = upper.GetComponentInChildren<Text>(true);
                if (section != null)
                {
                    section.text = string.Empty;
                    section.enabled = false;
                }
            }

            Transform mainLine = FindDescendantByName(
                background,
                "Linha Ciano");
            if (mainLine != null)
            {
                Image lineImage = mainLine.GetComponent<Image>();
                if (lineImage != null)
                    lineImage.color = DeckEmerald;
            }

            for (int index = 1; index <= 9; index++)
            {
                Transform line = FindDescendantByName(
                    background,
                    $"Linha {index}");
                Image lineImage = line != null
                    ? line.GetComponent<Image>()
                    : null;
                if (lineImage != null)
                {
                    lineImage.color = new Color(
                        DeckEmerald.r,
                        DeckEmerald.g,
                        DeckEmerald.b,
                        0.10f);
                }
            }
        }

        private static void SkinDeckEditorSurface(
            Image panel,
            Color accent,
            bool raised,
            float opacity)
        {
            if (panel == null)
                return;

            panel.color = Color.clear;
            Transform existing = panel.transform.Find(
                "Acabamento da Oficina de Decks");
            if (existing != null)
                UnityEngine.Object.Destroy(existing.gameObject);

            GameObject visualObject = new(
                "Acabamento da Oficina de Decks",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ArcanePanelSheenGraphic));
            visualObject.transform.SetParent(panel.transform, false);
            visualObject.transform.SetAsFirstSibling();
            RectTransform rect = visualObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            ArcanePanelSheenGraphic sheen =
                visualObject.GetComponent<ArcanePanelSheenGraphic>();
            sheen.raycastTarget = false;
            sheen.SetStyle(accent, raised, opacity);
        }

        private void RestyleDeckEditorHeaderButtons(
            bool selected,
            bool legal)
        {
            if (_screenRoot == null)
                return;

            for (int index = 0; index < _screenRoot.childCount; index++)
            {
                Transform child = _screenRoot.GetChild(index);
                Button button = child.GetComponent<Button>();
                Image image = child.GetComponent<Image>();
                if (button == null || image == null)
                    continue;

                Color accent;
                if (child.name.IndexOf(
                        "SALVAR",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    accent = DeckEmerald;
                }
                else if (child.name.IndexOf(
                             "DECK ATIVO",
                             StringComparison.OrdinalIgnoreCase) >= 0 ||
                         child.name.IndexOf(
                             "USAR NO DUELO",
                             StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    accent = selected
                        ? legal ? DeckAmber : Gold
                        : DeckMint;
                }
                else
                {
                    accent = DeckEmerald;
                }

                SkinDeckEditorSurface(image, accent, true, 0.88f);
                ArcanePanelSheenGraphic target =
                    image.GetComponentInChildren<ArcanePanelSheenGraphic>();
                if (target != null)
                    button.targetGraphic = target;
            }
        }

        private void AddDeckEditorSectionIdentity(
            DeckRecord deck,
            bool selected)
        {
            if (_screenRoot == null)
                return;

            Image marker = CreatePanel(
                _screenRoot,
                "Marcador esmeralda do editor",
                new Vector2(0.071f, 0.914f),
                new Vector2(0.075f, 0.968f),
                selected ? DeckAmber : DeckEmerald);
            marker.raycastTarget = false;

            if (deck == null)
                return;
            Text deckName = FindDescendantTextContaining(
                _screenRoot,
                deck.displayName);
            if (deckName != null &&
                deckName.transform.parent == _screenRoot)
            {
                deckName.fontSize = 30;
                deckName.color = Color.white;
            }
        }
    }
}
