using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private const float FieldActionWidth = 112f;
        private const float FieldActionHeight = 48f;
        private const float FieldActionGap = 8f;

        private GameObject fieldActionPanel;
        private DuelZone3D fieldActionZone;
        private DuelPrompt fieldActionPrompt;

        private void BuildFieldActionMenu()
        {
            fieldActionPanel = CreatePanel(
                frame,
                "Acoes da Carta no Campo",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Color(0.006f, 0.025f, 0.04f, 0.96f));
            AddOutline(fieldActionPanel, Cyan);
            fieldActionPanel.SetActive(false);
        }

        private void OpenFieldActionMenu(
            DuelZone3D zone,
            DuelPrompt prompt,
            IReadOnlyList<DuelChoice> choices)
        {
            if (zone == null || prompt == null ||
                choices == null || choices.Count == 0)
            {
                CloseFieldActionMenu();
                return;
            }

            if (fieldActionPanel == null)
                BuildFieldActionMenu();
            ClearChildren(fieldActionPanel.transform);
            fieldActionZone = zone;
            fieldActionPrompt = prompt;

            RectTransform panel =
                fieldActionPanel.GetComponent<RectTransform>();
            float width = choices.Count * FieldActionWidth +
                          Mathf.Max(0, choices.Count - 1) * FieldActionGap;
            panel.sizeDelta = new Vector2(width, FieldActionHeight);

            for (int index = 0; index < choices.Count; index++)
            {
                DuelChoice choice = choices[index];
                Color accent = FieldActionAccent(choice);
                Button button = CreateButton(
                    fieldActionPanel.transform,
                    $"Acao {index + 1}",
                    FieldActionLabel(zone, choice),
                    Vector2.zero,
                    Vector2.zero,
                    accent,
                    () => SubmitFieldAction(choice));
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax =
                    new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta =
                    new Vector2(FieldActionWidth, FieldActionHeight);
                rect.anchoredPosition = new Vector2(
                    index * (FieldActionWidth + FieldActionGap),
                    0f);
            }

            fieldActionPanel.SetActive(true);
            fieldActionPanel.transform.SetAsLastSibling();
            UpdateFieldActionMenuPosition();
        }

        private void SubmitFieldAction(DuelChoice choice)
        {
            DuelPrompt prompt = core?.CurrentPrompt;
            if (choice == null ||
                !SamePromptIdentity(prompt, fieldActionPrompt) ||
                !CoreCardActionBinding.BelongsToRequest(prompt, choice))
            {
                CloseFieldActionMenu();
                return;
            }

            CloseFieldActionMenu();
            core.SubmitChoice(choice);
            RefreshEverything(true);
        }

        private void UpdateFieldActionMenuPosition()
        {
            if (fieldActionPanel == null ||
                !fieldActionPanel.activeSelf)
            {
                return;
            }
            if (fieldActionZone == null ||
                !SamePromptIdentity(core?.CurrentPrompt, fieldActionPrompt) ||
                InteractionLocked || Camera.main == null)
            {
                CloseFieldActionMenu();
                return;
            }

            Vector3 screen = Camera.main.WorldToScreenPoint(
                fieldActionZone.transform.position + Vector3.up * 0.55f);
            Camera uiCamera = arenaCanvas != null &&
                              arenaCanvas.renderMode !=
                                  RenderMode.ScreenSpaceOverlay
                ? arenaCanvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    frame,
                    screen,
                    uiCamera,
                    out Vector2 local))
            {
                return;
            }

            RectTransform panel =
                fieldActionPanel.GetComponent<RectTransform>();
            local.y += 54f;
            float halfWidth = panel.sizeDelta.x * 0.5f;
            float halfHeight = panel.sizeDelta.y * 0.5f;
            local.x = Mathf.Clamp(
                local.x,
                frame.rect.xMin + halfWidth + 8f,
                frame.rect.xMax - halfWidth - 8f);
            local.y = Mathf.Clamp(
                local.y,
                frame.rect.yMin + halfHeight + 8f,
                frame.rect.yMax - halfHeight - 8f);
            panel.anchoredPosition = local;
            fieldActionPanel.transform.SetAsLastSibling();
        }

        private string FieldActionLabel(
            DuelZone3D zone,
            DuelChoice choice)
        {
            if (Contains(choice?.Label, "Ativar"))
                return "ATIVAR EFEITO";
            if (Contains(choice?.Label, "posi"))
            {
                uint current = PositionAt(zone);
                bool defense =
                    (current & (FaceUpDefense | FaceDownDefense)) != 0;
                return defense ? "MODO ATAQUE" : "MODO DEFESA";
            }
            if (Contains(choice?.Label, "Atacar"))
                return "ATACAR";
            return (choice?.Label ?? "ESCOLHER").ToUpperInvariant();
        }

        private Color FieldActionAccent(DuelChoice choice)
        {
            if (Contains(choice?.Label, "Ativar"))
                return EffectGlow;
            if (Contains(choice?.Label, "posi"))
                return SummonBlue;
            return Cyan;
        }

        private void CloseFieldActionMenu()
        {
            if (fieldActionPanel != null)
                fieldActionPanel.SetActive(false);
            fieldActionZone = null;
            fieldActionPrompt = null;
        }
    }
}
