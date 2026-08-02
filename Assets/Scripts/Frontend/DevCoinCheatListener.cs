#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ArcaneArena.Frontend
{
    public sealed class DevCoinCheatListener : MonoBehaviour
    {
        private const string Sequence = "LUCAS GAY";

        [SerializeField] private bool enableDevCoinCheat;
        [SerializeField, Min(0.1f)] private float maxSeconds = 4f;
        [SerializeField, Min(1)] private int grantCoins = 1000;

        private IWalletService _wallet;
        private int _position;
        private float _startedAt;
        private int _activationCounter;
        private string _sessionId;
#if ENABLE_INPUT_SYSTEM
        private Keyboard _subscribedKeyboard;
#endif

        public bool IsEnabledForDevelopment => enableDevCoinCheat;

        private void Awake()
        {
            _sessionId = Guid.NewGuid().ToString("N");
        }

        public void Configure(
            IWalletService wallet,
            bool enabled,
            float sequenceWindowSeconds = 4f,
            int coinsPerActivation = 1000)
        {
            UnsubscribeInput();
            _wallet = wallet;
            enableDevCoinCheat = enabled;
            maxSeconds = Mathf.Max(0.1f, sequenceWindowSeconds);
            grantCoins = Mathf.Max(1, coinsPerActivation);
            ResetSequence();
            if (isActiveAndEnabled)
                SubscribeInput();
        }

        private void OnEnable()
        {
            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            ResetSequence();
        }

        private void SubscribeInput()
        {
            if (!enableDevCoinCheat)
                return;
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange -= OnInputDeviceChange;
            InputSystem.onDeviceChange += OnInputDeviceChange;
            RebindKeyboard();
#endif
        }

        private void UnsubscribeInput()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange -= OnInputDeviceChange;
            if (_subscribedKeyboard != null)
                _subscribedKeyboard.onTextInput -= OnTextInput;
            _subscribedKeyboard = null;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void OnInputDeviceChange(
            InputDevice device,
            InputDeviceChange change)
        {
            if (device is Keyboard || Keyboard.current != _subscribedKeyboard)
                RebindKeyboard();
        }

        private void RebindKeyboard()
        {
            if (_subscribedKeyboard != null)
                _subscribedKeyboard.onTextInput -= OnTextInput;
            _subscribedKeyboard = Keyboard.current;
            if (_subscribedKeyboard != null && enableDevCoinCheat)
                _subscribedKeyboard.onTextInput += OnTextInput;
        }

        private void OnTextInput(char character)
        {
            Accept(character, Time.unscaledTime, Application.isFocused);
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private void Update()
        {
            if (!enableDevCoinCheat)
                return;
#if ENABLE_INPUT_SYSTEM
            if (_subscribedKeyboard != null)
                return;
#endif
            foreach (char character in Input.inputString)
                Accept(character, Time.unscaledTime, Application.isFocused);
        }
#endif

        private void Accept(char raw, float now, bool hasFocus)
        {
            if (!enableDevCoinCheat || _wallet == null || !hasFocus)
                return;
            if (char.IsControl(raw))
            {
                ResetSequence();
                return;
            }

            char character = char.IsWhiteSpace(raw)
                ? ' '
                : char.ToUpperInvariant(raw);
            if (_position > 0 && now - _startedAt > maxSeconds)
                ResetSequence();

            if (character == ' ' && _position > 0 &&
                Sequence[_position - 1] == ' ')
            {
                return;
            }

            if (_position == 0)
            {
                if (character != Sequence[0])
                    return;
                _startedAt = now;
                _position = 1;
                return;
            }

            if (character == Sequence[_position])
            {
                _position++;
                if (_position == Sequence.Length)
                {
                    if (now - _startedAt <= maxSeconds)
                        GrantCoinsOnce();
                    ResetSequence();
                }
                return;
            }

            ResetSequence();
            if (character == Sequence[0])
            {
                _startedAt = now;
                _position = 1;
            }
        }

        private void GrantCoinsOnce()
        {
            int activation = ++_activationCounter;
            string key = $"dev-cheat:{_sessionId}:{activation}";
            _wallet.TryGrantCoins(
                grantCoins,
                "DevCheat",
                key,
                out _,
                out _);
        }

        private void ResetSequence()
        {
            _position = 0;
            _startedAt = 0f;
        }

        public void AcceptCharacterForTests(
            char character,
            float unscaledTime,
            bool hasFocus = true)
        {
            Accept(character, unscaledTime, hasFocus);
        }
    }
}
#endif
