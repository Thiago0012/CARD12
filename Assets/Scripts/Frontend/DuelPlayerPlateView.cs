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
        private DuelHudSurfaceGraphic _surface;

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
            Color accent = local
                ? new Color(0.14f, 0.63f, 1f, 1f)
                : new Color(1f, 0.25f, 0.32f, 1f);
            ConfigurePlate(root, local, accent);
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
                ratio.aspectRatio = HexIconView.RegularHexAspect;
            }
            RectTransform iconRect = (RectTransform)iconTransform;
            iconRect.anchorMin = local ? new Vector2(0.018f, 0.025f) : new Vector2(0.982f, 0.025f);
            iconRect.anchorMax = local ? new Vector2(0.018f, 0.975f) : new Vector2(0.982f, 0.975f);
            iconRect.pivot = local ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            _icon = iconTransform.GetComponent<HexIconView>();
            _icon.SetAccent(accent);

            _name = FindAuthoredName(side) ?? EnsureText(
                "Identidade do Duelista",
                Vector2.zero,
                Vector2.one,
                13,
                new Color(0.98f, 0.78f, 0.30f),
                local ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            ConfigureText(
                _name,
                local ? new Vector2(0.285f, 0.735f) : new Vector2(0.03f, 0.735f),
                local ? new Vector2(0.97f, 0.97f) : new Vector2(0.715f, 0.97f),
                17,
                Color.white,
                local ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _rank = EnsureText(
                "Patente do Duelista",
                local ? new Vector2(0.285f, 0.625f) : new Vector2(0.03f, 0.625f),
                local ? new Vector2(0.97f, 0.755f) : new Vector2(0.715f, 0.755f),
                9,
                accent,
                local ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            LayoutAuthoredLifeValues(local);
            _name.raycastTarget = false;
            _rank.raycastTarget = false;
            EnsureTextShadow(_name, 1.5f);
            _name.transform.SetAsLastSibling();
            _rank.transform.SetAsLastSibling();
            iconTransform.SetAsLastSibling();
            if (root != null &&
                root.gameObject.GetComponent<DuelHudSafeAreaFitter>() == null)
            {
                root.gameObject.AddComponent<DuelHudSafeAreaFitter>();
            }
        }

        private void ConfigurePlate(
            RectTransform root,
            bool local,
            Color accent)
        {
            if (root != null && root.anchorMin == root.anchorMax)
                root.sizeDelta = new Vector2(356f, 108f);

            Image plate = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            plate.color = Color.clear;
            plate.raycastTarget = false;
            Outline outline = GetComponent<Outline>() ??
                gameObject.AddComponent<Outline>();
            outline.effectColor = Color.clear;
            outline.effectDistance = Vector2.zero;
            outline.useGraphicAlpha = true;

            EnsureModernSurface(local, accent);

            Vector2 contentMin = local
                ? new Vector2(0.255f, 0.02f)
                : new Vector2(0.015f, 0.02f);
            Vector2 contentMax = local
                ? new Vector2(0.985f, 0.985f)
                : new Vector2(0.745f, 0.985f);
            Image lifeBackground = EnsureImage(
                "Fundo da Vida",
                new Vector2(contentMin.x, 0.02f),
                new Vector2(contentMax.x, 0.615f),
                Color.clear);
            Image nameBackground = EnsureImage(
                "Faixa do Nome",
                new Vector2(contentMin.x, 0.615f),
                new Vector2(contentMax.x, 0.985f),
                Color.clear);
            lifeBackground.enabled = false;
            nameBackground.enabled = false;
            EnsureImage(
                "Linha de Destaque",
                new Vector2(contentMin.x, 0.595f),
                new Vector2(contentMax.x, 0.62f),
                new Color(accent.r, accent.g, accent.b, 0.72f));
        }

        private void EnsureModernSurface(bool local, Color accent)
        {
            Transform existing = transform.Find("Placa Translúcida do Duelista");
            GameObject surfaceObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    "Placa Translúcida do Duelista",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(DuelHudSurfaceGraphic));
            if (existing == null)
                surfaceObject.transform.SetParent(transform, false);
            SetRect(
                surfaceObject.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.one);
            _surface = surfaceObject.GetComponent<DuelHudSurfaceGraphic>();
            _surface.raycastTarget = false;
            _surface.SetStyle(accent, local, 0.96f, true, 12f);
            surfaceObject.transform.SetAsFirstSibling();
        }

        private Text FindAuthoredName(PlateSide side)
        {
            string authoredName = side == PlateSide.Local
                ? "PLAYER"
                : "OPONENTE";
            Transform existing = transform.Find(authoredName);
            return existing != null ? existing.GetComponent<Text>() : null;
        }

        private void LayoutAuthoredLifeValues(bool local)
        {
            Transform labelTransform = transform.Find("LP");
            Transform valueTransform = transform.Find("8000");
            if (labelTransform == null || valueTransform == null)
                return;

            if (local)
            {
                ConfigureText(
                    labelTransform.GetComponent<Text>(),
                    new Vector2(0.285f, 0.08f),
                    new Vector2(0.40f, 0.55f),
                    16,
                    new Color(0.83f, 0.90f, 0.94f),
                    TextAnchor.MiddleLeft);
                ConfigureText(
                    valueTransform.GetComponent<Text>(),
                    new Vector2(0.40f, 0.04f),
                    new Vector2(0.97f, 0.59f),
                    31,
                    Color.white,
                    TextAnchor.MiddleLeft);
            }
            else
            {
                ConfigureText(
                    labelTransform.GetComponent<Text>(),
                    new Vector2(0.03f, 0.08f),
                    new Vector2(0.145f, 0.55f),
                    16,
                    new Color(0.83f, 0.90f, 0.94f),
                    TextAnchor.MiddleLeft);
                ConfigureText(
                    valueTransform.GetComponent<Text>(),
                    new Vector2(0.145f, 0.04f),
                    new Vector2(0.715f, 0.59f),
                    31,
                    Color.white,
                    TextAnchor.MiddleLeft);
            }
            EnsureTextShadow(valueTransform.GetComponent<Text>(), 2f);
            labelTransform.SetAsLastSibling();
            valueTransform.SetAsLastSibling();
        }

        private Image EnsureImage(
            string objectName,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            Transform existing = transform.Find(objectName);
            GameObject gameObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
            if (existing == null)
                gameObject.transform.SetParent(transform, false);
            SetRect(gameObject.GetComponent<RectTransform>(), min, max);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            gameObject.transform.SetAsFirstSibling();
            return image;
        }

        private static void EnsureTextShadow(Text text, float distance)
        {
            if (text == null)
                return;
            Shadow shadow = text.GetComponent<Shadow>() ??
                text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
            shadow.effectDistance = new Vector2(distance, -distance);
            shadow.useGraphicAlpha = true;
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
            ConfigureText(text, min, max, size, color, alignment);
            return text;
        }

        private static void ConfigureText(
            Text text,
            Vector2 min,
            Vector2 max,
            int size,
            Color color,
            TextAnchor alignment)
        {
            if (text == null)
                return;
            SetRect(text.rectTransform, min, max);
            text.font = MasterDuelTypography.Resolve(FontStyle.Bold, size);
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = size;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 min,
            Vector2 max)
        {
            if (rect == null)
                return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
