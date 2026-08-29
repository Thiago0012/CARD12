using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Movimento ambiente sutil para a artwork equipada. Usa tempo não
    /// escalado para continuar natural fora de uma partida e sempre retorna
    /// à pose-base, evitando acumular deslocamento.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuArtworkFloat : MonoBehaviour
    {
        [SerializeField] private Vector2 travel = new(7f, 11f);
        [SerializeField] private float speed = 0.58f;
        [SerializeField] private float rotationDegrees = 0.55f;
        [SerializeField] private float scaleAmount = 0.006f;

        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private Vector2 _basePosition;
        private Vector3 _baseScale = Vector3.one;
        private float _phase;
        private bool _configured;

        public void Configure(string stableId)
        {
            _rect ??= transform as RectTransform;
            _canvasGroup ??= GetComponent<CanvasGroup>();
            if (_rect == null)
                return;

            _basePosition = Vector2.zero;
            _baseScale = Vector3.one;
            _rect.anchoredPosition = _basePosition;
            _rect.localScale = _baseScale;
            _rect.localRotation = Quaternion.identity;
            _phase = StablePhase(stableId);
            _configured = true;
        }

        private void Update()
        {
            if (!_configured || _rect == null)
                return;

            float time = Time.unscaledTime * speed + _phase;
            _rect.anchoredPosition = _basePosition + new Vector2(
                Mathf.Sin(time * 0.83f) * travel.x,
                Mathf.Sin(time) * travel.y);
            _rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Sin(time * 0.61f) * rotationDegrees);
            float scale = 1f + Mathf.Sin(time * 0.74f) * scaleAmount;
            _rect.localScale = _baseScale * scale;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0.975f + Mathf.Sin(time * 0.67f) * 0.025f;
        }

        private static float StablePhase(string stableId)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in stableId ?? string.Empty)
                    hash = hash * 31 + character;
                return Mathf.Abs(hash % 1000) / 1000f * Mathf.PI * 2f;
            }
        }
    }
}
