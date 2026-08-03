using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private static void AddBanlistBadge(Transform cardRoot, string cardId)
        {
            if (cardRoot == null || IsDuelPresentationScene())
                return;

            Sprite sprite = BanlistService.Active.BadgeFor(cardId);
            if (sprite == null)
                return;

            Image badge = CreatePanel(
                cardRoot,
                "Restricao da Banlist",
                new Vector2(0.015f, 0.76f),
                new Vector2(0.27f, 0.985f),
                Color.white);
            badge.sprite = sprite;
            badge.preserveAspect = true;
            badge.raycastTarget = true;
            int maximum = BanlistService.Active.MaximumCopies(cardId);
            badge.gameObject.AddComponent<CardRestrictionBadgeTooltip>()
                .Initialize(maximum);
            badge.transform.SetAsLastSibling();
        }

        private static void RefreshBanlistBadge(
            Transform cardRoot,
            string cardId)
        {
            if (cardRoot == null)
                return;
            CardRestrictionBadgeTooltip[] previous =
                cardRoot.GetComponentsInChildren<CardRestrictionBadgeTooltip>(true);
            foreach (CardRestrictionBadgeTooltip item in previous)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            AddBanlistBadge(cardRoot, cardId);
        }

        private static bool IsDuelPresentationScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return string.Equals(sceneName, DuelArenaSceneName) ||
                   string.Equals(sceneName, "Duel") ||
                   string.Equals(sceneName, "CardLab");
        }
    }
}
