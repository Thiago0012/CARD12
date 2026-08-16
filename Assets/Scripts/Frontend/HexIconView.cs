using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    public sealed class HexIconView : MonoBehaviour
    {
        public const float RegularHexAspect = 0.8660254f;
        private static Sprite _hexMaskSprite;
        private RawImage _portrait;
        private AspectRatioFitter _portraitFitter;
        private AspectRatioFitter _rootFitter;
        private Image _containerImage;
        private Image _clipImage;
        private Mask _clipMask;
        private Color _accent = new(0.24f, 0.91f, 0.96f, 1f);

        public string IconId { get; private set; } =
            ProfileIconCatalog.DefaultIconId;

        public void SetIcon(string iconId)
        {
            EnsureHierarchy();
            IconId = ProfileIconCatalog.ResolveId(iconId);
            ProfileIconDefinition definition =
                ProfileIconCatalog.Resolve(IconId);
            ApplyUniformHexMode();
            Texture2D texture = ProfileIconCatalog.LoadTexture(IconId);
            _portrait.texture = texture;
            _portrait.uvRect = definition.PortraitUv;
            _portraitFitter.aspectRatio = texture != null && texture.height > 0
                ? (texture.width * definition.PortraitUv.width) /
                  (texture.height * definition.PortraitUv.height)
                : 1f;
        }

        public void SetAccent(Color accent)
        {
            _accent = accent;
            EnsureHierarchy();
            _containerImage.color = _accent;
        }

        private void EnsureHierarchy()
        {
            _containerImage = GetComponent<Image>() ??
                gameObject.AddComponent<Image>();
            _containerImage.raycastTarget = false;
            _containerImage.sprite = GetHexMaskSprite();
            _containerImage.type = Image.Type.Simple;
            _containerImage.color = _accent;
            Mask legacyMask = GetComponent<Mask>();
            if (legacyMask != null)
                legacyMask.enabled = false;

            _rootFitter = GetComponent<AspectRatioFitter>() ??
                gameObject.AddComponent<AspectRatioFitter>();
            if (_rootFitter.aspectMode == AspectRatioFitter.AspectMode.None)
            {
                _rootFitter.aspectMode =
                    AspectRatioFitter.AspectMode.FitInParent;
            }
            _rootFitter.aspectRatio = RegularHexAspect;

            Transform clipTransform = transform.Find("Recorte Hexagonal");
            if (clipTransform == null)
            {
                GameObject clipObject = new(
                    "Recorte Hexagonal",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Mask));
                clipObject.transform.SetParent(transform, false);
                clipTransform = clipObject.transform;
            }
            RectTransform clipRect = (RectTransform)clipTransform;
            clipRect.anchorMin = new Vector2(0.055f, 0.055f);
            clipRect.anchorMax = new Vector2(0.945f, 0.945f);
            clipRect.offsetMin = Vector2.zero;
            clipRect.offsetMax = Vector2.zero;
            _clipImage = clipTransform.GetComponent<Image>();
            _clipImage.sprite = GetHexMaskSprite();
            _clipImage.type = Image.Type.Simple;
            _clipImage.color = Color.white;
            _clipImage.raycastTarget = false;
            _clipMask = clipTransform.GetComponent<Mask>();
            _clipMask.enabled = true;
            _clipMask.showMaskGraphic = false;

            if (_portrait == null)
            {
                Transform existing = clipTransform.Find("Retrato") ??
                    transform.Find("Retrato");
                GameObject portraitObject = existing != null
                    ? existing.gameObject
                    : new GameObject(
                        "Retrato",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(RawImage),
                        typeof(AspectRatioFitter));
                portraitObject.transform.SetParent(clipTransform, false);
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

        private void ApplyUniformHexMode()
        {
            _containerImage.sprite = GetHexMaskSprite();
            _containerImage.color = _accent;
            _clipImage.sprite = GetHexMaskSprite();
            _clipMask.enabled = true;
            _portraitFitter.aspectMode =
                AspectRatioFitter.AspectMode.EnvelopeParent;
            _portrait.rectTransform.localScale = Vector3.one;
        }

        private static Sprite GetHexMaskSprite()
        {
            if (_hexMaskSprite != null)
                return _hexMaskSprite;
            const int size = 256;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Hexagonal Profile Mask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int insideSamples = 0;
                const int samples = 4;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float u = (x + (sx + 0.5f) / samples) / size;
                    float v = (y + (sy + 0.5f) / samples) / size;
                    if (IsInsideRegularHex(u, v))
                        insideSamples++;
                }
                float alpha = insideSamples / (float)(samples * samples);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
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

        private static bool IsInsideRegularHex(float u, float v)
        {
            float halfWidth = v < 0.25f
                ? v * 2f
                : v > 0.75f
                    ? (1f - v) * 2f
                    : 0.5f;
            return Mathf.Abs(u - 0.5f) <= halfWidth;
        }
    }
}
