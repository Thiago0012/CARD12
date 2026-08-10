using System.Collections;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    internal sealed class ArcaneUiTextScaleTarget : MonoBehaviour
    {
        private Text target;
        private int baseFontSize;
        private int baseBestFitMinimum;
        private int baseBestFitMaximum;
        private bool configured;

        internal void Configure(
            Text text,
            int fontSize,
            int bestFitMinimum,
            int bestFitMaximum)
        {
            target = text;
            baseFontSize = Mathf.Max(1, fontSize);
            baseBestFitMinimum = Mathf.Max(1, bestFitMinimum);
            baseBestFitMaximum = Mathf.Max(
                baseBestFitMinimum,
                bestFitMaximum);
            configured = true;
            Apply();
        }

        internal void Apply()
        {
            if (!configured || target == null)
                return;

            target.fontSize = ArcaneUiTextPreferences.Scale(baseFontSize);
            target.resizeTextMinSize = ArcaneUiTextPreferences.Scale(
                baseBestFitMinimum);
            target.resizeTextMaxSize = Mathf.Max(
                target.resizeTextMinSize,
                ArcaneUiTextPreferences.Scale(baseBestFitMaximum));
        }
    }

    /// <summary>
    /// Registra textos gerados e também alcança textos já existentes nas cenas.
    /// O tamanho-base fica em um componente para impedir escala acumulativa.
    /// </summary>
    internal sealed class ArcaneUiTextScaleRuntime : MonoBehaviour
    {
        private static ArcaneUiTextScaleRuntime instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            EnsureInstance();
        }

        internal static void Register(Text text, int baseFontSize)
        {
            if (text == null)
                return;

            ArcaneUiTextScaleTarget marker =
                text.GetComponent<ArcaneUiTextScaleTarget>() ??
                text.gameObject.AddComponent<ArcaneUiTextScaleTarget>();
            marker.Configure(
                text,
                baseFontSize,
                text.resizeTextMinSize,
                Mathf.Max(baseFontSize, text.resizeTextMaxSize));
        }

        internal static void ApplyToLoadedTexts()
        {
            Text[] texts = FindObjectsByType<Text>(
                FindObjectsInactive.Include);
            foreach (Text text in texts)
            {
                if (text == null)
                    continue;
                ArcaneUiTextScaleTarget marker =
                    text.GetComponent<ArcaneUiTextScaleTarget>();
                if (marker == null)
                {
                    marker = text.gameObject
                        .AddComponent<ArcaneUiTextScaleTarget>();
                    marker.Configure(
                        text,
                        text.fontSize,
                        text.resizeTextMinSize,
                        Mathf.Max(text.fontSize, text.resizeTextMaxSize));
                }
                else
                {
                    marker.Apply();
                }
            }
        }

        private static void EnsureInstance()
        {
            if (instance != null)
                return;
            var root = new GameObject("Arcane UI Text Scale Runtime");
            instance = root.AddComponent<ArcaneUiTextScaleRuntime>();
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            ArcaneUiTextPreferences.Changed += ApplyToLoadedTexts;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            StartCoroutine(ApplyAfterLayout());
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;
            ArcaneUiTextPreferences.Changed -= ApplyToLoadedTexts;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(ApplyAfterLayout());
        }

        private static IEnumerator ApplyAfterLayout()
        {
            yield return null;
            ApplyToLoadedTexts();
        }
    }
}
