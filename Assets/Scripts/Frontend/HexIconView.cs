using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    public sealed class HexIconView : MonoBehaviour
    {
        public const float RegularHexAspect = 0.8660254f;
        // 5,5% em cada lado preserva a espessura visual já aprovada da moldura.
        public const float PortraitFrameInset = 0.055f;
        private static Sprite _hexMaskSprite;
        private static Sprite _hexGradientSprite;
        private RawImage _portrait;
        private AspectRatioFitter _portraitFitter;
        private AspectRatioFitter _rootFitter;
        private Image _containerImage;
        private Image _clipImage;
        private Mask _clipMask;
        private HexIconAuraView _aura;
        private Color _accent = new(0.24f, 0.91f, 0.96f, 1f);

        public string IconId { get; private set; } =
            ProfileIconCatalog.DefaultIconId;

        public void SetIcon(string iconId)
        {
            EnsureHierarchy();
            IconId = ProfileIconCatalog.ResolveId(iconId);
            ProfileIconDefinition definition =
                ProfileIconCatalog.Resolve(IconId);
            ApplyUniformHexMode(definition.AuraTheme);
            Texture2D texture = ProfileIconCatalog.LoadTexture(IconId);
            _portrait.texture = texture;
            _portrait.uvRect = definition.PortraitUv;
            _aura.SetTheme(definition.AuraTheme);
            // O viewport final sempre representa um hexágono regular. Não
            // usamos a proporção do arquivo-fonte porque as artes vieram de
            // telas com dimensões diferentes (paisagem, quadrada e retrato).
            _portraitFitter.aspectRatio = RegularHexAspect;
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
            _containerImage.sprite = GetHexGradientSprite();
            _containerImage.type = Image.Type.Simple;
            ApplyFrameTint();
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
            clipRect.anchorMin = new Vector2(
                PortraitFrameInset, PortraitFrameInset);
            clipRect.anchorMax = new Vector2(
                1f - PortraitFrameInset, 1f - PortraitFrameInset);
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

            EnsureAuraLayer();
        }

        private void EnsureAuraLayer()
        {
            Transform auraTransform =
                transform.Find(HexIconAuraView.LayerObjectName);
            if (auraTransform == null)
            {
                GameObject auraObject = new(
                    HexIconAuraView.LayerObjectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer));
                auraObject.transform.SetParent(transform, false);
                auraTransform = auraObject.transform;
            }

            RectTransform auraRect = (RectTransform)auraTransform;
            // A geometria do efeito mantém um centro totalmente vazio. O
            // retângulo maior dá espaço para a energia sair da moldura sem
            // alterar o tamanho oficial do ícone nem o recorte do retrato.
            auraRect.anchorMin = new Vector2(-0.16f, -0.16f);
            auraRect.anchorMax = new Vector2(1.16f, 1.16f);
            auraRect.offsetMin = Vector2.zero;
            auraRect.offsetMax = Vector2.zero;
            auraRect.localScale = Vector3.one;
            auraRect.localRotation = Quaternion.identity;
            _aura = auraTransform.GetComponent<HexIconAuraView>() ??
                auraTransform.gameObject.AddComponent<HexIconAuraView>();
            _aura.raycastTarget = false;
            auraTransform.SetAsLastSibling();
        }

        private void ApplyUniformHexMode(ProfileIconAuraTheme auraTheme)
        {
            _containerImage.sprite = GetHexGradientSprite();
            ApplyFrameTint();
            // Molduras de aura são visuais completos e exclusivos. A imagem
            // azul padrão fica realmente fora da renderização (não apenas
            // transparente), impedindo qualquer resquício nas bordas.
            _containerImage.enabled =
                auraTheme == ProfileIconAuraTheme.None;
            _clipImage.sprite = GetHexMaskSprite();
            _clipMask.enabled = true;
            _portraitFitter.aspectMode =
                AspectRatioFitter.AspectMode.EnvelopeParent;
            _portrait.rectTransform.localScale = Vector3.one;
        }

        private void ApplyFrameTint()
        {
            if (_containerImage == null)
                return;
            // Mantém todos os emblemas na mesma família ciano/azul. A cor de
            // estado (equipado, adquirido ou premium) influencia apenas de
            // forma sutil, sem criar nove molduras visualmente diferentes.
            Color softTint = Color.Lerp(Color.white, _accent, 0.16f);
            softTint.a = Mathf.Clamp01(_accent.a);
            _containerImage.color = softTint;
        }

        private static Sprite GetHexGradientSprite()
        {
            if (_hexGradientSprite != null)
                return _hexGradientSprite;
            const int size = 256;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Arcane Hexagonal Gradient Frame",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color highlight = new(0.64f, 1f, 1f, 1f);
            Color cyan = new(0.02f, 0.84f, 1f, 1f);
            Color blue = new(0.10f, 0.33f, 1f, 1f);
            Color violet = new(0.45f, 0.16f, 0.93f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int insideSamples = 0;
                const int samples = 4;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float sampleU = (x + (sx + 0.5f) / samples) / size;
                    float sampleV = (y + (sy + 0.5f) / samples) / size;
                    if (IsInsideRegularHex(sampleU, sampleV))
                        insideSamples++;
                }
                float alpha = insideSamples / (float)(samples * samples);
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float diagonal = Mathf.Clamp01(u * 0.46f + (1f - v) * 0.54f);
                Color upper = Color.Lerp(highlight, cyan,
                    Mathf.Clamp01(v * 1.4f));
                Color lower = Color.Lerp(blue, violet,
                    Mathf.Clamp01((1f - v) * 0.72f + u * 0.28f));
                Color color = Color.Lerp(upper, lower, diagonal * 0.72f);
                color.a = alpha;
                texture.SetPixel(x, y, color);
            }
            texture.Apply(false, true);
            _hexGradientSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _hexGradientSprite.name = "Arcane Hexagonal Gradient Frame";
            return _hexGradientSprite;
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
