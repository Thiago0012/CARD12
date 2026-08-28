using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Presentation-only controller for the title/login scene. It owns no
    /// gameplay state and is destroyed when the main menu is loaded.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class LoginIntroController : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        [Header("Interface")]
        [SerializeField] private RectTransform logo;
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private Button loginButton;
        [SerializeField] private CanvasGroup loginButtonGroup;

        [Header("Audio")]
        [SerializeField] private AudioSource themeSource;
        [SerializeField] private AudioSource titleCallSource;
        [SerializeField] private AudioSource shineSource;
        [SerializeField] private AudioClip loginTheme;
        [SerializeField] private AudioClip titleCall;
        [SerializeField] private AudioClip shineSound;

        [Header("Sincronizacao")]
        [SerializeField, Range(0.05f, 5f)]
        private float logoDuration = 3f;
        [SerializeField, Min(0f)] private float titleCallDelay = 1.7f;
        [SerializeField, Min(0f)] private float shineLeadSeconds = 1.5f;
        [SerializeField, Range(0.05f, 2f)]
        private float themeFadeInDuration = 0.75f;
        [SerializeField, Range(0.05f, 1f)]
        private float shineFadeInDuration = 0.3f;
        [SerializeField, Range(0.1f, 2f)]
        private float exitFadeDuration = 1f;

        private bool _isLeaving;
        private bool _updateBlocksEntry;
        private GameObject _updateOffer;
        private Button _updateButton;
        private Text _updateLabel;
        private Action _updateAction;
        private GameObject _accountRestoreOffer;
        private Button _accountRestoreButton;
        private CanvasGroup _accountRestoreGroup;

        public void Configure(
            RectTransform logoRect,
            CanvasGroup logoCanvasGroup,
            Button button,
            CanvasGroup buttonCanvasGroup,
            AudioSource theme,
            AudioSource title,
            AudioSource shine,
            AudioClip themeClip,
            AudioClip titleClip,
            AudioClip shineClip)
        {
            logo = logoRect;
            logoGroup = logoCanvasGroup;
            loginButton = button;
            loginButtonGroup = buttonCanvasGroup;
            themeSource = theme;
            titleCallSource = title;
            shineSource = shine;
            loginTheme = themeClip;
            titleCall = titleClip;
            shineSound = shineClip;
            logoDuration = 3f;
        }

        private void Awake()
        {
            PrepareSource(themeSource, loginTheme, true);
            PrepareSource(titleCallSource, titleCall, false);
            PrepareSource(shineSource, shineSound, false);

            if (logo != null)
                logo.localScale = Vector3.one * 0.06f;
            SetCanvasGroup(logoGroup, 0f, false);
            SetCanvasGroup(loginButtonGroup, 0f, false);

            if (loginButton != null)
            {
                ApplyModernArcaneButton(
                    loginButton,
                    new Color(0.12f, 0.75f, 0.88f, 1f),
                    11f);
                loginButton.interactable = false;
                loginButton.onClick.AddListener(HandleLoginButton);
            }
        }

        private void Start()
        {
            BuildAccountRestoreOffer();
            SetCanvasGroup(_accountRestoreGroup, 0f, false);
            if (themeSource != null && loginTheme != null)
            {
                themeSource.volume = 0f;
                themeSource.Play();
                StartCoroutine(
                    FadeSource(
                        themeSource,
                        0.72f,
                        themeFadeInDuration));
            }

            StartCoroutine(RevealInterface());
            StartCoroutine(PlayTitleAccents());
        }

        private void OnDestroy()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(HandleLoginButton);
            if (_updateButton != null)
                _updateButton.onClick.RemoveListener(HandleUpdateButton);
            if (_accountRestoreButton != null)
            {
                _accountRestoreButton.onClick.RemoveListener(
                    HandleAccountRestoreButton);
            }
        }

        /// <summary>
        /// Exibe somente o atalho de atualização sobre a abertura. Nenhum
        /// painel separado cobre o logo, a animação ou o áudio da cena.
        /// </summary>
        public void ShowUpdateOffer(
            Action action,
            bool blocksEntry,
            string label = "ATUALIZAR")
        {
            _updateAction = action;
            _updateBlocksEntry = blocksEntry;
            if (loginButton != null && blocksEntry)
                loginButton.interactable = false;
            if (_accountRestoreButton != null && blocksEntry)
                _accountRestoreButton.interactable = false;
            if (loginButtonGroup != null && blocksEntry)
            {
                loginButtonGroup.interactable = false;
                loginButtonGroup.blocksRaycasts = false;
            }
            if (_accountRestoreGroup != null && blocksEntry)
            {
                _accountRestoreGroup.interactable = false;
                _accountRestoreGroup.blocksRaycasts = false;
            }

            if (_updateOffer == null)
                BuildUpdateOffer();
            if (_updateLabel != null)
                _updateLabel.text = string.IsNullOrWhiteSpace(label)
                    ? "ATUALIZAR"
                    : label.Trim().ToUpperInvariant();
            if (_updateButton != null)
                _updateButton.interactable = true;
            _updateOffer.SetActive(true);
            StopCoroutineIfRunning();
            StartCoroutine(RevealUpdateOffer());
        }

        public void SetUpdateProgress(float progress)
        {
            if (_updateOffer == null)
                return;
            int percentage = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
            if (_updateLabel != null)
                _updateLabel.text = $"ATUALIZANDO  {percentage}%";
            if (_updateButton != null)
                _updateButton.interactable = false;
        }

        public void SetUpdateStatus(string label, bool interactable)
        {
            if (_updateOffer == null)
                BuildUpdateOffer();
            if (_updateLabel != null)
                _updateLabel.text = string.IsNullOrWhiteSpace(label)
                    ? "AGUARDE"
                    : label.Trim().ToUpperInvariant();
            if (_updateButton != null)
                _updateButton.interactable = interactable;
            _updateOffer.SetActive(true);
        }

        public void HideUpdateOffer()
        {
            _updateBlocksEntry = false;
            _updateAction = null;
            if (_updateOffer != null)
                _updateOffer.SetActive(false);
            if (!_isLeaving && loginButton != null &&
                (loginButtonGroup == null || loginButtonGroup.alpha > 0.99f))
            {
                loginButton.interactable = true;
            }
            if (!_isLeaving && _accountRestoreButton != null &&
                (_accountRestoreGroup == null ||
                 _accountRestoreGroup.alpha > 0.99f))
            {
                _accountRestoreButton.interactable = true;
            }
        }

        private void BuildAccountRestoreOffer()
        {
            if (_accountRestoreOffer != null)
                return;
            Transform parent = loginButton != null
                ? loginButton.transform.parent
                : transform;
            _accountRestoreOffer = new GameObject(
                "Acesso à Conta Existente",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Button),
                typeof(CanvasGroup));
            _accountRestoreOffer.transform.SetParent(parent, false);
            RectTransform rect =
                _accountRestoreOffer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -367.2549f);
            rect.sizeDelta = new Vector2(300f, 76.5097f);

            Image background = _accountRestoreOffer.GetComponent<Image>();
            background.color = new Color(0.012f, 0.035f, 0.060f, 0.92f);
            Outline outline = _accountRestoreOffer.GetComponent<Outline>();
            outline.effectColor = new Color(0.54f, 0.72f, 0.82f, 0.82f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            _accountRestoreButton =
                _accountRestoreOffer.GetComponent<Button>();
            _accountRestoreButton.targetGraphic = background;
            ColorBlock colors = _accountRestoreButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.38f, 0.78f, 0.90f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.38f, 0.48f, 0.54f, 0.72f);
            colors.fadeDuration = 0.10f;
            _accountRestoreButton.colors = colors;
            _accountRestoreButton.onClick.AddListener(
                HandleAccountRestoreButton);
            _accountRestoreGroup =
                _accountRestoreOffer.GetComponent<CanvasGroup>();

            CreateUpdateText(
                _accountRestoreOffer.transform,
                "ENTRAR EM CONTA",
                20,
                new Color(0.82f, 0.94f, 1f, 1f),
                new Vector2(0.05f, 0.34f),
                new Vector2(0.95f, 0.92f));
            CreateUpdateText(
                _accountRestoreOffer.transform,
                "RECUPERAR NO PC OU CELULAR",
                11,
                new Color(0.48f, 0.70f, 0.80f, 1f),
                new Vector2(0.05f, 0.05f),
                new Vector2(0.95f, 0.40f));
            ApplyModernArcaneButton(
                _accountRestoreButton,
                new Color(0.40f, 0.68f, 0.80f, 1f),
                10f);
        }

        private static void ApplyModernArcaneButton(
            Button button,
            Color accent,
            float chamfer)
        {
            if (button == null)
                return;

            Image legacy = button.GetComponent<Image>();
            if (legacy != null)
                legacy.color = new Color(0f, 0f, 0f, 0.015f);
            Outline legacyOutline = button.GetComponent<Outline>();
            if (legacyOutline != null)
                legacyOutline.enabled = false;

            Transform existing = button.transform.Find(
                "Superfície Arcane do Botão");
            ArcaneShopSurfaceGraphic surface = existing != null
                ? existing.GetComponent<ArcaneShopSurfaceGraphic>()
                : null;
            if (surface == null)
            {
                var surfaceObject = new GameObject(
                    "Superfície Arcane do Botão",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(ArcaneShopSurfaceGraphic));
                surfaceObject.transform.SetParent(button.transform, false);
                surfaceObject.transform.SetAsFirstSibling();
                RectTransform rect =
                    surfaceObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                surface = surfaceObject.GetComponent<ArcaneShopSurfaceGraphic>();
            }

            surface.SetStyle(accent, true, 1f, chamfer);
            surface.raycastTarget = true;
            button.targetGraphic = surface;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.16f);
            colors.pressedColor = Color.Lerp(Color.white, accent, 0.42f);
            colors.selectedColor = Color.Lerp(Color.white, accent, 0.22f);
            colors.disabledColor = new Color(0.48f, 0.54f, 0.58f, 0.72f);
            colors.fadeDuration = 0.10f;
            button.colors = colors;

            foreach (Text label in button.GetComponentsInChildren<Text>(true))
            {
                label.font = MasterDuelTypography.Resolve(
                    FontStyle.Bold,
                    Mathf.Max(25, label.fontSize));
                label.fontStyle = FontStyle.Normal;
                label.resizeTextForBestFit = false;
                Shadow shadow = label.GetComponent<Shadow>() ??
                                label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
                shadow.effectDistance = new Vector2(1f, -1f);
                shadow.useGraphicAlpha = true;
            }
        }

        private void HandleAccountRestoreButton()
        {
            if (_isLeaving || _updateBlocksEntry ||
                !RemoteUpdateRuntime.EntryReady)
            {
                return;
            }
            PlayerAccountRuntime.RequestRestoreOnNextMenu();
            HandleLoginButton();
        }

        private void BuildUpdateOffer()
        {
            Transform parent = loginButton != null
                ? loginButton.transform.parent
                : transform;
            _updateOffer = new GameObject(
                "Atalho de Atualização",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Button),
                typeof(CanvasGroup));
            _updateOffer.transform.SetParent(parent, false);
            RectTransform rect = _updateOffer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-72f, 64f);
            rect.sizeDelta = new Vector2(330f, 86f);

            Image background = _updateOffer.GetComponent<Image>();
            background.color = new Color(0.012f, 0.035f, 0.060f, 0.96f);
            Outline outline = _updateOffer.GetComponent<Outline>();
            outline.effectColor = new Color(0.10f, 0.86f, 1f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            _updateButton = _updateOffer.GetComponent<Button>();
            _updateButton.targetGraphic = background;
            ColorBlock colors = _updateButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.38f, 0.78f, 0.90f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.48f, 0.60f, 0.66f, 0.82f);
            colors.fadeDuration = 0.10f;
            _updateButton.colors = colors;
            _updateButton.onClick.AddListener(HandleUpdateButton);

            Image energy = CreateUpdateImage(
                _updateOffer.transform,
                "Marcador de Energia",
                new Vector2(0f, 0.08f),
                new Vector2(0.018f, 0.92f),
                new Color(0.10f, 0.86f, 1f, 1f));
            energy.raycastTarget = false;
            Image iconPlate = CreateUpdateImage(
                _updateOffer.transform,
                "Ícone de Atualização",
                new Vector2(0.055f, 0.15f),
                new Vector2(0.245f, 0.85f),
                new Color(0.02f, 0.16f, 0.23f, 0.98f));
            iconPlate.raycastTarget = false;
            Outline iconOutline = iconPlate.gameObject.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0.12f, 0.78f, 0.95f, 0.92f);
            iconOutline.effectDistance = new Vector2(1.5f, -1.5f);
            CreateUpdateText(
                iconPlate.transform,
                "↻",
                34,
                new Color(0.18f, 0.90f, 1f, 1f),
                Vector2.zero,
                Vector2.one);
            _updateLabel = CreateUpdateText(
                _updateOffer.transform,
                "ATUALIZAR",
                20,
                Color.white,
                new Vector2(0.29f, 0.12f),
                new Vector2(0.93f, 0.88f));
            _updateLabel.alignment = TextAnchor.MiddleLeft;

            Image notification = CreateUpdateImage(
                _updateOffer.transform,
                "Notificação de Nova Versão",
                new Vector2(0.925f, 0.68f),
                new Vector2(0.975f, 0.88f),
                new Color(0.95f, 0.38f, 0.25f, 1f));
            notification.raycastTarget = false;
        }

        private void HandleUpdateButton()
        {
            if (_updateButton != null)
                _updateButton.interactable = false;
            _updateAction?.Invoke();
        }

        private IEnumerator RevealUpdateOffer()
        {
            CanvasGroup group = _updateOffer.GetComponent<CanvasGroup>();
            RectTransform rect = _updateOffer.GetComponent<RectTransform>();
            group.alpha = 0f;
            rect.localScale = Vector3.one * 0.92f;
            float elapsed = 0f;
            const float duration = 0.24f;
            while (elapsed < duration && _updateOffer.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                float value = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                group.alpha = value;
                rect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, value);
                yield return null;
            }
            group.alpha = 1f;
            rect.localScale = Vector3.one;
        }

        private void StopCoroutineIfRunning()
        {
            // As corrotinas da abertura precisam continuar; o efeito do atalho
            // é curto e pode simplesmente coexistir com elas.
        }

        private static Image CreateUpdateImage(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            GameObject value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            value.transform.SetParent(parent, false);
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = value.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateUpdateText(
            Transform parent,
            string value,
            int size,
            Color color,
            Vector2 min,
            Vector2 max)
        {
            GameObject label = new GameObject(
                "Texto",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            label.transform.SetParent(parent, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        public void HandleLoginButton()
        {
            if (_isLeaving || _updateBlocksEntry ||
                !RemoteUpdateRuntime.EntryReady)
                return;

            _isLeaving = true;
            if (loginButton != null)
                loginButton.interactable = false;
            if (_accountRestoreButton != null)
                _accountRestoreButton.interactable = false;
            StopAllCoroutines();
            StartCoroutine(LeaveForMainMenu());
        }

        private IEnumerator RevealInterface()
        {
            float duration = Mathf.Clamp(logoDuration, 0.05f, 5f);
            float elapsed = 0f;
            while (elapsed < duration && !_isLeaving)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                if (logo != null)
                    logo.localScale = Vector3.one *
                                      Mathf.LerpUnclamped(
                                          0.06f,
                                          1f,
                                          eased);
                if (logoGroup != null)
                    logoGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
                yield return null;
            }

            if (_isLeaving)
                yield break;

            if (logo != null)
                logo.localScale = Vector3.one;
            SetCanvasGroup(logoGroup, 1f, false);

            elapsed = 0f;
            const float buttonFadeDuration = 0.18f;
            while (elapsed < buttonFadeDuration && !_isLeaving)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(
                    elapsed / buttonFadeDuration);
                if (loginButtonGroup != null)
                    loginButtonGroup.alpha = progress;
                if (_accountRestoreGroup != null)
                    _accountRestoreGroup.alpha = progress;
                yield return null;
            }

            if (_isLeaving)
                yield break;

            bool entryAvailable = !_updateBlocksEntry &&
                                  RemoteUpdateRuntime.EntryReady;
            SetCanvasGroup(loginButtonGroup, 1f, entryAvailable);
            SetCanvasGroup(_accountRestoreGroup, 1f, entryAvailable);
            if (loginButton != null)
                loginButton.interactable = !_updateBlocksEntry &&
                                           RemoteUpdateRuntime.EntryReady;
            if (_accountRestoreButton != null)
                _accountRestoreButton.interactable = !_updateBlocksEntry &&
                                                     RemoteUpdateRuntime.EntryReady;
        }

        private IEnumerator PlayTitleAccents()
        {
            yield return WaitUnscaled(titleCallDelay);
            if (_isLeaving)
                yield break;

            if (titleCallSource != null && titleCall != null)
            {
                titleCallSource.volume = 1f;
                titleCallSource.Play();
            }

            float titleLength = titleCall != null
                ? titleCall.length
                : shineLeadSeconds;
            float shineDelay = Mathf.Max(
                0f,
                titleLength - shineLeadSeconds);
            yield return WaitUnscaled(shineDelay);
            if (_isLeaving || shineSource == null || shineSound == null)
                yield break;

            shineSource.volume = 0f;
            shineSource.Play();
            yield return FadeSource(
                shineSource,
                0.9f,
                shineFadeInDuration);
        }

        private IEnumerator LeaveForMainMenu()
        {
            float duration = Mathf.Max(0.1f, exitFadeDuration);
            float elapsed = 0f;
            float themeStart = VolumeOf(themeSource);
            float titleStart = VolumeOf(titleCallSource);
            float shineStart = VolumeOf(shineSource);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = progress * progress * (3f - 2f * progress);
                SetVolume(themeSource, Mathf.Lerp(themeStart, 0f, eased));
                SetVolume(titleCallSource, Mathf.Lerp(titleStart, 0f, eased));
                SetVolume(shineSource, Mathf.Lerp(shineStart, 0f, eased));
                yield return null;
            }

            StopSource(themeSource);
            StopSource(titleCallSource);
            StopSource(shineSource);

            if (!Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            {
                Debug.LogError(
                    "A cena MainMenu nao esta disponivel no Build Settings.");
                _isLeaving = false;
                SetCanvasGroup(loginButtonGroup, 1f, true);
                SetCanvasGroup(_accountRestoreGroup, 1f, true);
                if (loginButton != null)
                    loginButton.interactable = true;
                if (_accountRestoreButton != null)
                    _accountRestoreButton.interactable = true;
                PlayerAccountRuntime.ConsumeRestoreRequest();
                yield break;
            }

            SceneManager.LoadSceneAsync(
                MainMenuSceneName,
                LoadSceneMode.Single);
        }

        private static IEnumerator FadeSource(
            AudioSource source,
            float target,
            float duration)
        {
            if (source == null)
                yield break;

            float start = source.volume;
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / safeDuration);
                source.volume = Mathf.Lerp(
                    start,
                    target,
                    Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }
            source.volume = target;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static void PrepareSource(
            AudioSource source,
            AudioClip clip,
            bool loop)
        {
            if (source == null)
                return;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.clip = clip;
        }

        private static void SetCanvasGroup(
            CanvasGroup group,
            float alpha,
            bool interactive)
        {
            if (group == null)
                return;
            group.alpha = alpha;
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }

        private static float VolumeOf(AudioSource source) =>
            source != null && source.isPlaying ? source.volume : 0f;

        private static void SetVolume(AudioSource source, float volume)
        {
            if (source != null)
                source.volume = volume;
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null)
                return;
            source.volume = 0f;
            source.Stop();
        }
    }
}
