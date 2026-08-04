#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Editor.DeveloperTools
{
    [InitializeOnLoad]
    internal static class SelectedCardZeroCoinGrant
    {
        private static readonly ZeroCoinGrantController Controller = new();
        private static readonly UnityEditorZeroCoinGrantBridge Bridge = new();

        static SelectedCardZeroCoinGrant()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Unsubscribe;
            AssemblyReloadEvents.beforeAssemblyReload += Unsubscribe;
        }

        private static void Tick()
        {
            try
            {
                if (Controller.Tick(Bridge))
                    Debug.Log(Bridge.LastSuccessLog);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "ARCANE_DEV_ZERO_ERROR " +
                    exception.GetBaseException().Message);
            }
        }

        private static void Unsubscribe()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Unsubscribe;
        }
    }

    internal sealed class UnityEditorZeroCoinGrantBridge :
        IZeroCoinGrantBridge
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic;

        private Type frontendType;
        private PropertyInfo frontendInstanceProperty;
        private FieldInfo repositoryField;
        private PropertyInfo duelProperty;
        private PropertyInfo textInputProperty;
        private object cachedFrontend;
        private object cachedRepository;

        public bool IsPlaying => EditorApplication.isPlaying &&
            Application.isPlaying;
        public bool IsPaused => EditorApplication.isPaused;
        // A ferramenta e global no Editor durante o Play Mode. Nao exigimos
        // que a Game View retenha o foco depois de abrir a tela permitida.
        public bool IsGameViewFocused => EditorApplication.isPlaying;
        public bool AlphaZeroIsPressed =>
            Keyboard.current?.digit0Key.isPressed == true;
        public bool NumpadZeroIsPressed =>
            Keyboard.current?.numpad0Key.isPressed == true;
        public bool IsAllowedScreen =>
            string.Equals(
                SceneManager.GetActiveScene().name,
                "DeckEditor",
                StringComparison.OrdinalIgnoreCase) ||
            GameObject.Find("Saldo de Moedas") != null;
        public string LastSuccessLog { get; private set; } = string.Empty;

        public bool IsInDuel
        {
            get
            {
                object frontend = ResolveFrontend();
                return ReadBoolean(frontend, duelProperty);
            }
        }

        public bool IsTextInputFocused
        {
            get
            {
                object frontend = ResolveFrontend();
                return ReadBoolean(frontend, textInputProperty);
            }
        }

        public bool IsTransactionBusy
        {
            get
            {
                object frontend = ResolveFrontend();
                if (frontend == null)
                    return false;
                return ReadFieldBoolean(frontend, "_packRevealBusy") ||
                    !string.IsNullOrWhiteSpace(
                        ReadFieldString(frontend, "_activePackOpeningId"));
            }
        }

        public bool IsWalletReady => ResolveRepository() != null;

        public bool TryGrantCoins(
            int amount,
            string reason,
            string idempotencyKey,
            out int balanceAfter,
            out string rejection)
        {
            balanceAfter = 0;
            rejection = string.Empty;
            object repository = ResolveRepository();
            if (repository == null)
            {
                rejection = "A carteira ainda não foi inicializada.";
                return false;
            }

            MethodInfo grant = repository.GetType().GetMethod(
                "TryGrantCoins",
                InstanceFlags);
            if (grant == null)
            {
                rejection = "O serviço de concessão de moedas não foi encontrado.";
                return false;
            }

            object[] arguments =
            {
                amount,
                reason,
                idempotencyKey,
                null,
                null
            };
            bool granted = grant.Invoke(repository, arguments) is true;
            rejection = arguments[4] as string ?? string.Empty;
            balanceAfter = ReadIntegerProperty(repository, "CoinBalance");
            if (granted)
            {
                RefreshVisibleBalance(balanceAfter);
                LastSuccessLog = string.Concat(
                    "ARCANE_DEV_ZERO_GRANTED amount=",
                    amount,
                    " balance=",
                    balanceAfter,
                    " screen=",
                    SceneManager.GetActiveScene().name,
                    " request=",
                    idempotencyKey);
            }
            return granted;
        }

        public void Notify(string message, bool error)
        {
            EditorWindow.focusedWindow?.ShowNotification(
                new GUIContent(message),
                2.5);
            if (error)
                Debug.LogWarning("ARCANE_DEV_ZERO_REJECTED " + message);
        }

        private object ResolveFrontend()
        {
            if (cachedFrontend is UnityEngine.Object unityObject &&
                unityObject != null)
            {
                return cachedFrontend;
            }

            if (frontendType == null)
            {
                frontendType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(
                        "ArcaneArena.Frontend.GameFrontendBootstrap",
                        false))
                    .FirstOrDefault(type => type != null);
                if (frontendType == null)
                    return null;
                frontendInstanceProperty = frontendType.GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public);
                repositoryField = frontendType.GetField(
                    "_repository",
                    InstanceFlags);
                duelProperty = frontendType.GetProperty(
                    "IsInDuel",
                    InstanceFlags);
                textInputProperty = frontendType.GetProperty(
                    "IsTextInputFocused",
                    InstanceFlags);
            }

            cachedFrontend = frontendInstanceProperty?.GetValue(null);
            if (cachedFrontend == null)
            {
                cachedFrontend = UnityEngine.Object.FindAnyObjectByType(
                    frontendType,
                    FindObjectsInactive.Include);
            }
            cachedRepository = null;
            return cachedFrontend;
        }

        private object ResolveRepository()
        {
            object frontend = ResolveFrontend();
            if (frontend == null || repositoryField == null)
                return null;
            cachedRepository = repositoryField.GetValue(frontend);
            if (cachedRepository == null)
                return null;
            PropertyInfo state = cachedRepository.GetType().GetProperty(
                "State",
                InstanceFlags);
            return state?.GetValue(cachedRepository) != null
                ? cachedRepository
                : null;
        }

        private static bool ReadBoolean(
            object source,
            PropertyInfo property)
        {
            return source != null && property?.GetValue(source) is true;
        }

        private static bool ReadFieldBoolean(object source, string name)
        {
            return source?.GetType().GetField(name, InstanceFlags)
                ?.GetValue(source) is true;
        }

        private static string ReadFieldString(object source, string name)
        {
            return source?.GetType().GetField(name, InstanceFlags)
                ?.GetValue(source) as string ?? string.Empty;
        }

        private static int ReadIntegerProperty(object source, string name)
        {
            object value = source?.GetType().GetProperty(name, InstanceFlags)
                ?.GetValue(source);
            return value is int integer ? integer : 0;
        }

        private static void RefreshVisibleBalance(int balance)
        {
            GameObject panel = GameObject.Find("Saldo de Moedas");
            if (panel == null)
                return;
            foreach (Component component in panel.GetComponentsInChildren<
                         Component>(true))
            {
                if (!string.Equals(
                        component.GetType().FullName,
                        "UnityEngine.UI.Text",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                PropertyInfo textProperty = component.GetType().GetProperty(
                    "text",
                    InstanceFlags);
                string current = textProperty?.GetValue(component) as string ??
                    string.Empty;
                if (current.Any(char.IsDigit))
                {
                    textProperty?.SetValue(component, balance.ToString("N0"));
                    return;
                }
            }
        }
    }
}
#endif
