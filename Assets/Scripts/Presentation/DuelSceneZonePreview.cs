using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Links one editable Scene-view card to the presentation of one stable
    /// duel zone. Card identity, rules and authoritative state are untouched.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DuelSceneZonePreview : MonoBehaviour
    {
        [SerializeField] private DuelZone3D targetZone;
        [SerializeField] private Vector2 referenceCardSize =
            new Vector2(76f, 110f);
        [SerializeField, HideInInspector] private Vector3 referencePileScale;
        [SerializeField, HideInInspector]
        private Vector3 referenceDropSurfaceScale;
        [SerializeField, HideInInspector] private Vector3 referenceColliderSize;

        private bool synchronizing;
        private Vector3 lastLocalPosition;
        private Vector3 lastLocalScale;
        private Vector3 lastLocalEulerAngles;
        private Vector2 lastSize;
        private bool snapshotReady;

        public DuelZone3D TargetZone => targetZone;
        public Vector2 ReferenceCardSize => referenceCardSize;

        public void Configure(DuelZone3D zone, Vector2 cardSize)
        {
            targetZone = zone;
            referenceCardSize = cardSize;
            referencePileScale = CapturePileScale(zone);
            referenceDropSurfaceScale = CaptureDropSurfaceScale(zone);
            referenceColliderSize = CaptureColliderSize(zone);
            CaptureSnapshot();
        }

        /// <summary>
        /// Saves exactly what is visible in the editable frame and applies it
        /// to the same presentation anchors used by Play Mode.
        /// </summary>
        public bool SyncNow()
        {
            if (synchronizing || Application.isPlaying || targetZone == null ||
                !(transform is RectTransform rect))
            {
                return false;
            }

            RectTransform frame = FindUniversalFrame();
            if (frame == null || frame.rect.width <= 1f ||
                frame.rect.height <= 1f ||
                !TryCaptureLayout(
                    frame,
                    rect,
                    out Vector2 center,
                    out Vector2 rightHalfAxis,
                    out Vector2 upHalfAxis,
                    out float angle))
            {
                return false;
            }

            synchronizing = true;
            try
            {
                if (referencePileScale.sqrMagnitude < 0.0001f)
                    referencePileScale = CapturePileScale(targetZone);
                if (referenceDropSurfaceScale.sqrMagnitude < 0.0001f)
                {
                    referenceDropSurfaceScale =
                        CaptureDropSurfaceScale(targetZone);
                }
                if (referenceColliderSize.sqrMagnitude < 0.0001f)
                    referenceColliderSize = CaptureColliderSize(targetZone);

                DuelAuthoredZoneLayout layout =
                    frame.GetComponent<DuelAuthoredZoneLayout>();
                if (layout == null)
                    layout = frame.gameObject.AddComponent<DuelAuthoredZoneLayout>();
                layout.Upsert(
                    targetZone,
                    center,
                    rightHalfAxis,
                    upHalfAxis,
                    angle,
                    referencePileScale,
                    referenceDropSurfaceScale,
                    referenceColliderSize);
                bool changed = layout.ApplyOne(targetZone);
                CaptureSnapshot();

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(layout);
                if (changed)
                {
                    UnityEditor.EditorUtility.SetDirty(targetZone);
                    UnityEditor.EditorUtility.SetDirty(targetZone.transform);
                    UnityEditor.EditorUtility.SetDirty(
                        targetZone.CardPresentationAnchor);
                    UnityEditor.SceneManagement.EditorSceneManager
                        .MarkSceneDirty(gameObject.scene);
                }
#endif
                return changed;
            }
            finally
            {
                synchronizing = false;
            }
        }

        public static bool TryCaptureLayout(
            RectTransform frame,
            RectTransform rect,
            out Vector2 center,
            out Vector2 rightHalfAxis,
            out Vector2 upHalfAxis,
            out float angle)
        {
            center = default;
            rightHalfAxis = default;
            upHalfAxis = default;
            angle = 0f;
            if (frame == null || rect == null ||
                frame.rect.width <= 1f || frame.rect.height <= 1f)
            {
                return false;
            }

            var worldCorners = new Vector3[4];
            rect.GetWorldCorners(worldCorners);
            var corners = new Vector2[4];
            for (int index = 0; index < corners.Length; index++)
            {
                Vector3 local = frame.InverseTransformPoint(
                    worldCorners[index]);
                corners[index] = new Vector2(local.x, local.y);
            }

            Vector2 localCenter =
                (corners[0] + corners[1] + corners[2] + corners[3]) *
                0.25f;
            Vector2 localRight =
                (corners[2] + corners[3]) * 0.5f - localCenter;
            Vector2 localUp =
                (corners[1] + corners[2]) * 0.5f - localCenter;
            center = ToViewportPoint(frame, localCenter);
            rightHalfAxis = new Vector2(
                localRight.x / frame.rect.width,
                localRight.y / frame.rect.height);
            upHalfAxis = new Vector2(
                localUp.x / frame.rect.width,
                localUp.y / frame.rect.height);
            angle = Mathf.Atan2(localRight.y, localRight.x) *
                    Mathf.Rad2Deg;
            return rightHalfAxis.sqrMagnitude > 0.0000001f &&
                   upHalfAxis.sqrMagnitude > 0.0000001f;
        }

        private void OnEnable()
        {
            snapshotReady = false;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || targetZone == null)
                return;
            if (!snapshotReady || HasPreviewChanged())
                SyncNow();
#endif
        }

        private bool HasPreviewChanged()
        {
            if (!(transform is RectTransform rect))
                return false;
            return (rect.localPosition - lastLocalPosition).sqrMagnitude >
                       0.0001f ||
                   (rect.localScale - lastLocalScale).sqrMagnitude >
                       0.0001f ||
                   (rect.localEulerAngles - lastLocalEulerAngles).sqrMagnitude >
                       0.0001f ||
                   (rect.rect.size - lastSize).sqrMagnitude > 0.01f;
        }

        private void CaptureSnapshot()
        {
            if (!(transform is RectTransform rect))
                return;
            lastLocalPosition = rect.localPosition;
            lastLocalScale = rect.localScale;
            lastLocalEulerAngles = rect.localEulerAngles;
            lastSize = rect.rect.size;
            rect.hasChanged = false;
            snapshotReady = true;
        }

        private RectTransform FindUniversalFrame()
        {
            Transform cursor = transform;
            while (cursor != null)
            {
                if (cursor.GetComponent<DuelSceneAuthoringPreview>() != null)
                    return cursor.parent as RectTransform;
                cursor = cursor.parent;
            }
            return null;
        }

        private static Vector2 ToViewportPoint(
            RectTransform frame,
            Vector2 local)
        {
            return new Vector2(
                Mathf.InverseLerp(frame.rect.xMin, frame.rect.xMax, local.x),
                Mathf.InverseLerp(frame.rect.yMin, frame.rect.yMax, local.y));
        }

        private static Vector3 CapturePileScale(DuelZone3D zone)
        {
            Transform pile = zone != null
                ? zone.transform.Find("Card Stack")
                : null;
            return pile != null ? pile.localScale : Vector3.zero;
        }

        private static Vector3 CaptureDropSurfaceScale(DuelZone3D zone)
        {
            Transform surface = zone != null
                ? zone.transform.Find("Card Inset")
                : null;
            return surface != null ? surface.localScale : Vector3.zero;
        }

        private static Vector3 CaptureColliderSize(DuelZone3D zone)
        {
            BoxCollider collider = zone != null
                ? zone.GetComponent<BoxCollider>()
                : null;
            return collider != null ? collider.size : Vector3.zero;
        }
    }
}
