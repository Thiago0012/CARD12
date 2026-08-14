using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    public sealed class HexIconView : MonoBehaviour
    {
        private static Sprite _hexMaskSprite;
        private RawImage _portrait;
        private AspectRatioFitter _portraitFitter;
        private Image _containerImage;
        private Mask _mask;

        public string IconId { get; private set; } =
            ProfileIconCatalog.DefaultIconId;

        public void SetIcon(string iconId)
        {
            EnsureHierarchy();
            IconId = ProfileIconCatalog.ResolveId(iconId);
            ProfileIconDefinition definition =
                ProfileIconCatalog.Resolve(IconId);
            ApplyAssetMode(definition.AssetMode);
            Texture2D texture = ProfileIconCatalog.LoadTexture(IconId);
            _portrait.texture = texture;
            _portrait.uvRect = new Rect(0f, 0f, 1f, 1f);
            _portraitFitter.aspectRatio = texture != null && texture.height > 0
                ? (float)texture.width / texture.height
                : 1f;
        }

        private void EnsureHierarchy()
        {
            _containerImage = GetComponent<Image>() ??
                gameObject.AddComponent<Image>();
            _containerImage.raycastTarget = false;
            _mask = GetComponent<Mask>();

            if (_portrait == null)
            {
                Transform existing = transform.Find("Retrato");
                GameObject portraitObject = existing != null
                    ? existing.gameObject
                    : new GameObject(
                        "Retrato",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(RawImage),
                        typeof(AspectRatioFitter));
                if (existing == null)
                    portraitObject.transform.SetParent(transform, false);
                RectTransform rect = portraitObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                _portrait = portraitObject.GetComponent<RawImage>();
                _portrait.raycastTarget = false;
                _portraitFitter = portraitObject.GetComponent<AspectRatioFitter>();
            }
        }

        private void ApplyAssetMode(ProfileIconAssetMode assetMode)
        {
            bool preframed = assetMode == ProfileIconAssetMode.PreframedHex;
            if (preframed)
            {
                if (_mask != null)
                    _mask.enabled = false;
                _containerImage.sprite = null;
                _containerImage.color = Color.clear;
                _portraitFitter.aspectMode =
                    AspectRatioFitter.AspectMode.FitInParent;
                return;
            }

            _containerImage.sprite = GetHexMaskSprite();
            _containerImage.color = Color.white;
            _mask ??= gameObject.AddComponent<Mask>();
            _mask.enabled = true;
            _mask.showMaskGraphic = false;
            _portraitFitter.aspectMode =
                AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        private static Sprite GetHexMaskSprite()
        {
            if (_hexMaskSprite != null)
                return _hexMaskSprite;
            const int size = 128;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Hexagonal Profile Mask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color clear = new(1f, 1f, 1f, 0f);
            Color solid = Color.white;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float ny = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                bool inside = nx <= 0.91f && ny <= 0.98f &&
                              nx * 0.58f + ny <= 1.02f;
                texture.SetPixel(x, y, inside ? solid : clear);
            }
            texture.Apply(false, true);
            _hexMaskSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _hexMaskSprite.name = "Hexagonal Profile Mask";
            return _hexMaskSprite;
        }
    }
}
