using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    /// <summary>
    /// Mantem toda a interface dentro de uma moldura logica 16:9.
    /// O monitor pode mudar de resolucao ou proporcao sem alterar a geometria
    /// interna criada pelo designer na Hierarchy.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class UniversalAspectFrame : MonoBehaviour
    {
        public const float DefaultAspect = 16f / 9f;

        [SerializeField] private float targetAspect = DefaultAspect;
        [SerializeField] private bool controlMainCameraViewport;

        private RectTransform _rect;
        private RectTransform _parentRect;
        private Vector2 _lastParentSize = new Vector2(-1f, -1f);
        private Rect _lastCameraRect = new Rect(-1f, -1f, -1f, -1f);
        private bool _applyingLayout;

        public void Configure(bool controlCamera)
        {
            controlMainCameraViewport = controlCamera;
            ApplyLayout(true);
        }

        private void OnEnable()
        {
            CacheReferences();
            ApplyLayout(true);
        }

        private void LateUpdate()
        {
            ApplyLayout(false);
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout(true);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || !controlMainCameraViewport)
                return;

            var camera = Camera.main;
            if (camera != null)
                camera.rect = new Rect(0f, 0f, 1f, 1f);
        }

        private void CacheReferences()
        {
            _rect = transform as RectTransform;
            _parentRect = transform.parent as RectTransform;
        }

        private void ApplyLayout(bool force)
        {
            if (_applyingLayout)
                return;
            if (_rect == null || _parentRect == null)
                CacheReferences();
            if (_rect == null || _parentRect == null)
                return;

            var parentSize = _parentRect.rect.size;
            if (parentSize.x <= 0.01f || parentSize.y <= 0.01f)
                return;

            if (force ||
                !Mathf.Approximately(parentSize.x, _lastParentSize.x) ||
                !Mathf.Approximately(parentSize.y, _lastParentSize.y))
            {
                _applyingLayout = true;
                var parentAspect = parentSize.x / parentSize.y;
                var frameSize = parentAspect > targetAspect
                    ? new Vector2(parentSize.y * targetAspect, parentSize.y)
                    : new Vector2(parentSize.x, parentSize.x / targetAspect);

                _rect.anchorMin = new Vector2(0.5f, 0.5f);
                _rect.anchorMax = new Vector2(0.5f, 0.5f);
                _rect.pivot = new Vector2(0.5f, 0.5f);
                _rect.anchoredPosition = Vector2.zero;
                _rect.sizeDelta = frameSize;
                _rect.localScale = Vector3.one;
                _rect.localRotation = Quaternion.identity;
                _lastParentSize = parentSize;
                _applyingLayout = false;
            }

            if (Application.isPlaying && controlMainCameraViewport)
                ApplyCameraViewport();
        }

        private void ApplyCameraViewport()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            var screenAspect = (float)Screen.width / Screen.height;
            Rect viewport;
            if (screenAspect > targetAspect)
            {
                var width = targetAspect / screenAspect;
                viewport = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                var height = screenAspect / targetAspect;
                viewport = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }

            if (viewport == _lastCameraRect)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;
            camera.rect = viewport;
            _lastCameraRect = viewport;
        }
    }

    public static class UniversalUiLayout
    {
        public const string FrameName = "Area Segura Universal 16x9";
        public static readonly Vector2 ReferenceResolution =
            new Vector2(1920f, 1080f);

        public static void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
                return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
        }

        public static RectTransform CreateFrame(
            Transform parent,
            bool controlCameraViewport)
        {
            if (parent == null)
                return null;

            var existing = parent.Find(FrameName) as RectTransform;
            RectTransform frame;
            if (existing != null)
            {
                frame = existing;
            }
            else
            {
                var frameObject = new GameObject(
                    FrameName,
                    typeof(RectTransform),
                    typeof(UniversalAspectFrame));
                frameObject.transform.SetParent(parent, false);
                frame = frameObject.GetComponent<RectTransform>();
            }

            var aspect = frame.GetComponent<UniversalAspectFrame>();
            if (aspect == null)
                aspect = frame.gameObject.AddComponent<UniversalAspectFrame>();
            aspect.Configure(controlCameraViewport);
            return frame;
        }
    }
}
