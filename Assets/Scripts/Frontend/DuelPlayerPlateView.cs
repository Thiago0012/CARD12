using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    public sealed class DuelPlayerPlateView : MonoBehaviour
    {
        public enum PlateSide
        {
            Local,
            Opponent
        }

        private HexIconView _icon;
        private Text _name;
        private Text _rank;

        public void Bind(DuelIdentitySnapshot identity, PlateSide side)
        {
            identity ??= new DuelIdentitySnapshot
            {
                stablePlayerId = side == PlateSide.Local ? "local" : "opponent",
                nickname = side == PlateSide.Local ? "DUELISTA" : "OPONENTE",
                equippedIconId = ProfileIconCatalog.DefaultIconId,
                rankTier = RankTier.Wood,
                cosmeticsCatalogVersion = ProfileIconCatalog.CatalogVersion
            };
            EnsureHierarchy(side);
            _icon.SetIcon(identity.equippedIconId);
            _name.text = string.IsNullOrWhiteSpace(identity.nickname)
                ? (side == PlateSide.Local ? "DUELISTA" : "OPONENTE")
                : identity.nickname.ToUpperInvariant();
            _rank.text = RankRules.DisplayName(identity.rankTier);
        }

        private void EnsureHierarchy(PlateSide side)
        {
            bool local = side == PlateSide.Local;
            RectTransform root = transform as RectTransform;
            Transform iconTransform = transform.Find("Ícone do Perfil");
            if (iconTransform == null)
            {
                GameObject iconObject = new(
                    "Ícone do Perfil",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Mask),
                    typeof(HexIconView),
                    typeof(AspectRatioFitter));
                iconObject.transform.SetParent(transform, false);
                iconTransform = iconObject.transform;
                AspectRatioFitter ratio = iconObject.GetComponent<AspectRatioFitter>();
                ratio.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                ratio.aspectRatio = 1f;
            }
            RectTransform iconRect = (RectTransform)iconTransform;
            iconRect.anchorMin = local ? new Vector2(0.015f, 0.08f) : new Vector2(0.985f, 0.08f);
            iconRect.anchorMax = local ? new Vector2(0.015f, 0.92f) : new Vector2(0.985f, 0.92f);
            iconRect.pivot = local ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            _icon = iconTransform.GetComponent<HexIconView>();

            _name = EnsureText(
                "Identidade do Duelista",
                local ? new Vector2(0.25f, 0.63f) : new Vector2(0.03f, 0.63f),
                local ? new Vector2(0.96f, 0.96f) : new Vector2(0.75f, 0.96f),
                13,
                new Color(0.98f, 0.78f, 0.30f),
                local ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _rank = EnsureText(
                "Patente do Duelista",
                local ? new Vector2(0.25f, 0.42f) : new Vector2(0.03f, 0.42f),
                local ? new Vector2(0.96f, 0.65f) : new Vector2(0.75f, 0.65f),
                10,
                new Color(0.28f, 0.90f, 0.96f),
                local ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _name.raycastTarget = false;
            _rank.raycastTarget = false;
            if (root != null &&
                root.gameObject.GetComponent<DuelHudSafeAreaFitter>() == null)
            {
                root.gameObject.AddComponent<DuelHudSafeAreaFitter>();
            }
        }

        private Text EnsureText(
            string objectName,
            Vector2 min,
            Vector2 max,
            int size,
            Color color,
            TextAnchor alignment)
        {
            Transform existing = transform.Find(objectName);
            GameObject gameObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
            if (existing == null)
                gameObject.transform.SetParent(transform, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = size;
            return text;
        }
    }
}
