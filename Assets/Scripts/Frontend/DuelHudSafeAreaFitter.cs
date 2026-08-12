using UnityEngine;

namespace ArcaneArena.Frontend
{
    [DisallowMultipleComponent]
    public sealed class DuelHudSafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Vector2 _authoredPosition;
        private Vector3 _authoredScale = Vector3.one;
        private bool _captured;
        private int _width;
        private int _height;
        private Rect _safeArea;
        private Rect _cameraRect;

        private void Awake()
        {
            _rect = transform as RectTransform;
            CaptureAuthoredPosition();
            ApplyIfChanged(true);
        }

        private void OnEnable() => ApplyIfChanged(true);

        private void LateUpdate() => ApplyIfChanged(false);

        public void CaptureAuthoredPosition()
        {
            _rect ??= transform as RectTransform;
            if (_rect == null)
                return;
            _authoredPosition = _rect.anchoredPosition;
            _authoredScale = _rect.localScale;
            _captured = true;
        }

        private void ApplyIfChanged(bool force)
        {
            if (_rect == null || _rect.parent is not RectTransform parent)
                return;
            Rect cameraRect = Camera.main != null
                ? Camera.main.pixelRect
                : new Rect(0f, 0f, Screen.width, Screen.height);
            Rect safeArea = Screen.safeArea;
            if (!force && _width == Screen.width && _height == Screen.height &&
                Approximately(_safeArea, safeArea) &&
                Approximately(_cameraRect, cameraRect))
                return;

            _width = Screen.width;
            _height = Screen.height;
            _safeArea = safeArea;
            _cameraRect = cameraRect;
            if (!_captured)
                CaptureAuthoredPosition();
            _rect.anchoredPosition = _authoredPosition;
            _rect.localScale = _authoredScale;
            Canvas.ForceUpdateCanvases();

            Rect usable = Intersect(safeArea, cameraRect);
            if (usable.width <= 1f || usable.height <= 1f)
                usable = cameraRect;
            Vector3[] corners = new Vector3[4];
            _rect.GetWorldCorners(corners);
            Camera uiCamera = ResolveUiCamera();
            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            float visibleWidth = Mathf.Max(1f, max.x - min.x);
            float visibleHeight = Mathf.Max(1f, max.y - min.y);
            float fit = Mathf.Min(
                1f,
                usable.width / visibleWidth,
                usable.height / visibleHeight);
            if (fit < 0.999f)
            {
                _rect.localScale = _authoredScale * fit;
                Canvas.ForceUpdateCanvases();
                _rect.GetWorldCorners(corners);
                min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
                max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            }
            float dx = min.x < usable.xMin ? usable.xMin - min.x :
                max.x > usable.xMax ? usable.xMax - max.x : 0f;
            float dy = min.y < usable.yMin ? usable.yMin - min.y :
                max.y > usable.yMax ? usable.yMax - max.y : 0f;
            if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
                return;
            Vector2 localScale = new(
                parent.rect.width / Mathf.Max(1f, cameraRect.width),
                parent.rect.height / Mathf.Max(1f, cameraRect.height));
            _rect.anchoredPosition += new Vector2(
                dx * localScale.x,
                dy * localScale.y);
        }

        private Camera ResolveUiCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }

        public static Rect Intersect(Rect first, Rect second)
        {
            float xMin = Mathf.Max(first.xMin, second.xMin);
            float yMin = Mathf.Max(first.yMin, second.yMin);
            float xMax = Mathf.Min(first.xMax, second.xMax);
            float yMax = Mathf.Min(first.yMax, second.yMax);
            return xMax <= xMin || yMax <= yMin
                ? Rect.zero
                : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool Approximately(Rect a, Rect b) =>
            Mathf.Abs(a.x - b.x) < 0.5f &&
            Mathf.Abs(a.y - b.y) < 0.5f &&
            Mathf.Abs(a.width - b.width) < 0.5f &&
            Mathf.Abs(a.height - b.height) < 0.5f;
    }
}
