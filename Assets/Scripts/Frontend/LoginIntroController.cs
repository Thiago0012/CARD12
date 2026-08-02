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
                loginButton.interactable = false;
                loginButton.onClick.AddListener(HandleLoginButton);
            }
        }

        private void Start()
        {
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
        }

        public void HandleLoginButton()
        {
            if (_isLeaving)
                return;

            _isLeaving = true;
            if (loginButton != null)
                loginButton.interactable = false;
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
                yield return null;
            }

            if (_isLeaving)
                yield break;

            SetCanvasGroup(loginButtonGroup, 1f, true);
            if (loginButton != null)
                loginButton.interactable = true;
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
                if (loginButton != null)
                    loginButton.interactable = true;
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
