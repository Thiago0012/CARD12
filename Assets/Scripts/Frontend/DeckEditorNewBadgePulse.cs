using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed class DeckEditorNewBadgePulse : MonoBehaviour
    {
        private CanvasGroup _group;
        private RectTransform _rect;
        private bool _visible;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>() ??
                     gameObject.AddComponent<CanvasGroup>();
            _rect = transform as RectTransform;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            gameObject.SetActive(visible);
            if (!visible || _group == null)
                return;
            _group.alpha = 0.35f;
            if (_rect != null)
                _rect.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!_visible || _group == null)
                return;
            float phase = Mathf.PingPong(Time.unscaledTime * 2.65f, 1f);
            float faded = Mathf.SmoothStep(0f, 1f, phase);
            _group.alpha = Mathf.Lerp(0.28f, 1f, faded);
            if (_rect != null)
            {
                float scale = Mathf.Lerp(0.97f, 1.035f, faded);
                _rect.localScale = Vector3.one * scale;
            }
        }
    }
}
