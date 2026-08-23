using System;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneArena.StoryRoguelite;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private void ShowStoryRelicReward(StoryPendingRelicReward reward)
        {
            if (reward == null)
            {
                ShowStoryRoguelite();
                return;
            }
            ClearScreen();
            ClearStoryRuntimeSprites();
            _shopBackAction = ShowStoryMap;
            BuildShopBackground("RELÍQUIAS DA JORNADA");
            BuildProfessionalShopHeader(reward.title, ShowStoryMap);

            StoryPendingRelicReplacement replacement =
                _storyManager.Save.pendingRelicReplacement;
            if (replacement != null)
            {
                ShowStoryRelicReplacement(replacement);
                return;
            }

            CreateText(_screenRoot,
                "Escolha uma melhoria para toda a run. Relíquias não são " +
                "cartas e não ocupam espaço no deck. Limites: 10 no total · " +
                "5 Comuns · 4 Mágicas · 3 Raras · 1 Única.",
                17, FontStyle.Normal, Color.white,
                new Vector2(0.10f, 0.78f), new Vector2(0.90f, 0.87f),
                TextAnchor.MiddleCenter);

            int count = Mathf.Max(1, reward.relicIds.Count);
            float width = count > 2 ? 0.25f : 0.31f;
            float gap = 0.025f;
            float total = count * width + (count - 1) * gap;
            float firstX = 0.5f - total * 0.5f;
            for (int index = 0; index < reward.relicIds.Count; index++)
            {
                string relicId = reward.relicIds[index];
                StoryRelicDefinition definition = StoryRelicLibrary.Resolve(
                    relicId);
                float x = firstX + index * (width + gap);
                Image tile = CreatePanel(_screenRoot,
                    definition.displayName,
                    new Vector2(x, 0.22f),
                    new Vector2(x + width, 0.75f),
                    Color.clear);
                Color rarityColor = StoryRelicRarityColor(
                    definition.rarity);
                DecorateRuntimeShopSurface(tile, rarityColor, true, 13f);
                CreateText(tile.transform,
                    definition.displayName.ToUpperInvariant(),
                    20, FontStyle.Bold, rarityColor,
                    new Vector2(0.07f, 0.77f), new Vector2(0.93f, 0.94f),
                    TextAnchor.MiddleCenter);
                CreateText(tile.transform,
                    StoryRelicRarityLabel(definition.rarity) +
                    " · " + StoryRelicModeLabel(definition.useMode),
                    12, FontStyle.Bold, Cyan,
                    new Vector2(0.07f, 0.67f), new Vector2(0.93f, 0.77f),
                    TextAnchor.MiddleCenter);
                Text description = CreateScrollableText(tile.transform,
                    "Descrição da relíquia",
                    new Vector2(0.08f, 0.24f),
                    new Vector2(0.92f, 0.65f), 14);
                description.text = definition.description;
                Image choose = CreateButton(tile.transform,
                    "ESCOLHER",
                    new Vector2(0.12f, 0.06f),
                    new Vector2(0.88f, 0.19f),
                    Lime,
                    () =>
                    {
                        if (!_storyManager.SelectPendingRelic(
                                relicId, out string rejection))
                            ShowStoryToast(rejection);
                        else ShowStoryRoguelite();
                    });
                DecorateRuntimeShopButton(choose, Lime, true, 7f);
            }

            Image reject = CreateButton(_screenRoot,
                "RECUSAR RELÍQUIA",
                new Vector2(0.38f, 0.10f), new Vector2(0.62f, 0.17f),
                Muted,
                () =>
                {
                    _storyManager.RejectPendingRelic();
                    ShowStoryRoguelite();
                });
            DecorateRuntimeShopButton(reject, Muted, false, 7f);
        }

        private void ShowStoryRelicReplacement(
            StoryPendingRelicReplacement replacement)
        {
            StoryRelicDefinition incoming = StoryRelicLibrary.Resolve(
                replacement.incomingRelicId);
            CreateText(_screenRoot,
                "LIMITE ATINGIDO · ESCOLHA UMA RELÍQUIA PARA SUBSTITUIR\n" +
                "Nova relíquia: " + incoming.displayName,
                21, FontStyle.Bold, Gold,
                new Vector2(0.12f, 0.76f), new Vector2(0.88f, 0.88f),
                TextAnchor.MiddleCenter);

            int count = Mathf.Max(1, replacement.eligibleRelicIds.Count);
            int columns = Math.Min(3, count);
            for (int index = 0; index < replacement.eligibleRelicIds.Count;
                 index++)
            {
                string outgoingId = replacement.eligibleRelicIds[index];
                StoryRelicDefinition outgoing = StoryRelicLibrary.Resolve(
                    outgoingId);
                int row = index / columns;
                int column = index % columns;
                float width = 0.25f;
                float x = 0.105f + column * 0.285f;
                float yMax = 0.70f - row * 0.19f;
                Image button = CreateButton(_screenRoot,
                    outgoing.displayName.ToUpperInvariant() + "\n" +
                    outgoing.shortEffect,
                    new Vector2(x, yMax - 0.16f),
                    new Vector2(x + width, yMax),
                    StoryRelicRarityColor(outgoing.rarity),
                    () =>
                    {
                        if (!_storyManager.ReplacePendingRelic(
                                outgoingId, out string rejection))
                            ShowStoryToast(rejection);
                        else ShowStoryRoguelite();
                    });
                DecorateRuntimeShopButton(button,
                    StoryRelicRarityColor(outgoing.rarity), true, 7f);
            }

            Image cancel = CreateButton(_screenRoot,
                "VOLTAR ÀS OPÇÕES",
                new Vector2(0.25f, 0.08f), new Vector2(0.48f, 0.15f),
                Cyan,
                () =>
                {
                    _storyManager.CancelPendingRelicReplacement();
                    ShowStoryRelicReward(
                        _storyManager.Save.pendingRelicReward);
                });
            DecorateRuntimeShopButton(cancel, Cyan, true, 7f);
            Image reject = CreateButton(_screenRoot,
                "RECUSAR A NOVA RELÍQUIA",
                new Vector2(0.52f, 0.08f), new Vector2(0.75f, 0.15f),
                Muted,
                () =>
                {
                    _storyManager.RejectPendingRelic();
                    ShowStoryRoguelite();
                });
            DecorateRuntimeShopButton(reject, Muted, false, 7f);
        }

        private void ShowStoryRandomEvent(StoryPendingRandomEvent pending)
        {
            if (pending == null)
            {
                ShowStoryRoguelite();
                return;
            }
            ClearScreen();
            ClearStoryRuntimeSprites();
            _shopBackAction = ShowStoryMap;
            BuildShopBackground("EVENTO ALEATÓRIO");
            BuildProfessionalShopHeader(pending.title, ShowStoryMap);

            Image panel = CreatePanel(_screenRoot,
                "Evento aleatório persistido",
                new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.82f),
                Color.clear);
            DecorateRuntimeShopSurface(panel, Gold, true, 15f);
            CreateText(panel.transform, pending.flavorText,
                19, FontStyle.Normal, Color.white,
                new Vector2(0.07f, 0.82f), new Vector2(0.93f, 0.95f),
                TextAnchor.MiddleCenter);

            bool showRisk = _storyManager.HasArtifact("intuition-mask");
            int columns = pending.options.Count > 5 ? 2 : 1;
            int rows = Mathf.CeilToInt(
                pending.options.Count / (float)columns);
            float rowHeight = Mathf.Min(0.125f, 0.69f / Math.Max(1, rows));
            float width = columns == 1 ? 0.76f : 0.37f;
            for (int index = 0; index < pending.options.Count; index++)
            {
                StoryRandomEventOption option = pending.options[index];
                int column = index % columns;
                int row = index / columns;
                float x = columns == 1 ? 0.12f : 0.105f + column * 0.395f;
                float yMax = 0.79f - row * (rowHeight + 0.012f);
                string cardName = string.IsNullOrWhiteSpace(option.cardId)
                    ? string.Empty
                    : " · " + (DeckRepository.ResolveCard(
                        _catalog, option.cardId)?.DisplayName ?? option.cardId);
                string risk = showRisk &&
                    !string.IsNullOrWhiteSpace(option.riskLabel)
                        ? "\nRISCO: " + option.riskLabel.ToUpperInvariant()
                        : string.Empty;
                string cost = option.fragmentCost > 0
                    ? $" · {option.fragmentCost} FRAGMENTOS"
                    : string.Empty;
                Image button = CreateButton(panel.transform,
                    option.label + cardName + cost + "\n" +
                    option.description + risk,
                    new Vector2(x, yMax - rowHeight),
                    new Vector2(x + width, yMax),
                    option.enabled ? Cyan : Muted,
                    () => OnStoryRandomEventOption(pending, option));
                button.GetComponent<Button>().interactable = option.enabled;
                DecorateRuntimeShopButton(button,
                    option.enabled ? Cyan : Muted,
                    option.enabled, 7f);
            }
        }

        private void OnStoryRandomEventOption(
            StoryPendingRandomEvent pending,
            StoryRandomEventOption option)
        {
            if (option.requiresConfirmation)
            {
                ShowStoryRandomEventConfirmation(pending, option);
                return;
            }
            if (!_storyManager.ResolveRandomEventChoice(
                    option.choiceId, out string rejection))
                ShowStoryToast(rejection);
            else ShowStoryRoguelite();
        }

        private void ShowStoryRandomEventConfirmation(
            StoryPendingRandomEvent pending,
            StoryRandomEventOption option)
        {
            Image veil = CreatePanel(_screenRoot,
                "Confirmação do evento",
                Vector2.zero, Vector2.one,
                new Color(0f, 0f, 0f, 0.86f));
            veil.transform.SetAsLastSibling();
            Image modal = CreatePanel(veil.transform,
                "Risco confirmado",
                new Vector2(0.28f, 0.31f), new Vector2(0.72f, 0.69f),
                Color.clear);
            DecorateRuntimeShopSurface(modal, Gold, true, 14f);
            CreateText(modal.transform,
                "CONFIRMAR ESCOLHA",
                24, FontStyle.Bold, Gold,
                new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.91f),
                TextAnchor.MiddleCenter);
            CreateText(modal.transform,
                option.label + "\n\n" + option.description,
                17, FontStyle.Normal, Color.white,
                new Vector2(0.10f, 0.32f), new Vector2(0.90f, 0.72f),
                TextAnchor.MiddleCenter);
            Image cancel = CreateButton(modal.transform,
                "CANCELAR",
                new Vector2(0.08f, 0.09f), new Vector2(0.47f, 0.27f),
                Muted, () => Destroy(veil.gameObject));
            DecorateRuntimeShopButton(cancel, Muted, false, 7f);
            Image confirm = CreateButton(modal.transform,
                "CONFIRMAR",
                new Vector2(0.53f, 0.09f), new Vector2(0.92f, 0.27f),
                Danger,
                () =>
                {
                    Destroy(veil.gameObject);
                    if (!_storyManager.ResolveRandomEventChoice(
                            option.choiceId, out string rejection))
                        ShowStoryToast(rejection);
                    else ShowStoryRoguelite();
                });
            DecorateRuntimeShopButton(confirm, Danger, false, 7f);
        }

        private static Color StoryRelicRarityColor(
            StoryRelicRarity rarity) => rarity switch
        {
            StoryRelicRarity.Common => new Color(.67f, .74f, .80f, 1f),
            StoryRelicRarity.Magic => new Color(.18f, .63f, 1f, 1f),
            StoryRelicRarity.Rare => new Color(1f, .61f, .16f, 1f),
            StoryRelicRarity.Unique => new Color(.72f, .35f, 1f, 1f),
            _ => Color.white
        };

        private static string StoryRelicRarityLabel(
            StoryRelicRarity rarity) => rarity switch
        {
            StoryRelicRarity.Common => "COMUM",
            StoryRelicRarity.Magic => "MÁGICA",
            StoryRelicRarity.Rare => "RARA",
            StoryRelicRarity.Unique => "ÚNICA",
            _ => rarity.ToString().ToUpperInvariant()
        };

        private static string StoryRelicModeLabel(
            StoryRelicUseMode mode) => mode switch
        {
            StoryRelicUseMode.PassiveRun => "PASSIVA DA RUN",
            StoryRelicUseMode.PassiveDuel => "PASSIVA DE DUELO",
            StoryRelicUseMode.ActiveDuel => "ATIVA DE DUELO",
            StoryRelicUseMode.ConsumableDuel => "CONSUMÍVEL DE DUELO",
            StoryRelicUseMode.ActiveMap => "ATIVA DE MAPA",
            StoryRelicUseMode.ConsumableMap => "CONSUMÍVEL DE MAPA",
            _ => mode.ToString().ToUpperInvariant()
        };
    }
}
