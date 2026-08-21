using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Tipografia oficial do Master Duel 2 Plus Ultra. Centraliza a família
    /// usada por interfaces autoradas e por elementos criados em runtime,
    /// garantindo o mesmo resultado no Editor, Windows e Android.
    /// </summary>
    public static class MasterDuelTypography
    {
        private const string RegularResource =
            "Fonts/Oxanium-Regular";
        private const string SemiBoldResource =
            "Fonts/Oxanium-SemiBold";
        private const string BoldResource =
            "Fonts/Oxanium-Bold";

        private static Font _regular;
        private static Font _semiBold;
        private static Font _bold;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _regular = null;
            _semiBold = null;
            _bold = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
            {
                ApplyToHierarchy(canvas.transform);
            }
        }

        public static Font Resolve(
            FontStyle requestedStyle,
            int fontSize = 0)
        {
            bool bold = requestedStyle == FontStyle.Bold ||
                        requestedStyle == FontStyle.BoldAndItalic;
            if (bold && fontSize >= 25)
            {
                _bold ??= Resources.Load<Font>(BoldResource);
                if (_bold != null)
                    return _bold;
            }
            if (bold)
            {
                _semiBold ??= Resources.Load<Font>(SemiBoldResource);
                if (_semiBold != null)
                    return _semiBold;
            }

            _regular ??= Resources.Load<Font>(RegularResource);
            if (_regular != null)
                return _regular;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static void Apply(
            Text text,
            FontStyle requestedStyle,
            int fontSize = 0)
        {
            if (text == null)
                return;
            int resolvedSize = fontSize > 0
                ? fontSize
                : text.fontSize;
            text.font = Resolve(requestedStyle, resolvedSize);
            text.fontStyle = requestedStyle == FontStyle.Italic ||
                             requestedStyle == FontStyle.BoldAndItalic
                ? FontStyle.Italic
                : FontStyle.Normal;
        }

        public static void ApplyToHierarchy(Transform root)
        {
            if (root == null)
                return;
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                FontStyle requestedStyle = text.fontStyle;
                Apply(text, requestedStyle, text.fontSize);
            }
        }
    }
}
