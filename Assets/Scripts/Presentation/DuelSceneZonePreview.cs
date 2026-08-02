using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Links a Scene-view handle to the presentation anchor of one stable duel
    /// zone. It does not identify cards or participate in duel rules.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DuelSceneZonePreview : MonoBehaviour
    {
        private const float FieldWidth = 18.8f;
        private const float FieldDepth = 10.586f;

        [SerializeField] private DuelZone3D targetZone;
        [SerializeField] private Vector2 referenceCardSize = new(76f, 110f);
        [SerializeField, HideInInspector] private Vector3 referencePileScale;
        [SerializeField, HideInInspector] private Vector3 referenceDropSurfaceScale;
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
        /// Copies this editor handle to the real presentation transform used by
        /// Play Mode. Stable zone identity and duel state are never changed.
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
                frame.rect.height <= 1f)
            {
                return false;
            }

            synchronizing = true;
            try
            {
                if (referencePileScale.sqrMagnitude < 0.0001f)
                    referencePileScale = CapturePileScale(targetZone);
                if (referenceDropSurfaceScale.sqrMagnitude < 0.0001f)
                    referenceDropSurfaceScale =
                        CaptureDropSurfaceScale(targetZone);
                if (referenceColliderSize.sqrMagnitude < 0.0001f)
                    referenceColliderSize = CaptureColliderSize(targetZone);

                Vector3 world = rect.TransformPoint(rect.rect.center);
                Vector3 local = frame.InverseTransformPoint(world);
                Vector2 normalized = new(
                    Mathf.InverseLerp(
                        frame.rect.xMin,
                        frame.rect.xMax,
                        local.x),
                    Mathf.InverseLerp(
                        frame.rect.yMin,
                        frame.rect.yMax,
                        local.y));
                float scale = rect.rect.height /
                    Mathf.Max(1f, referenceCardSize.y);
                bool changed = ApplyPresentationToZone(
                    targetZone,
                    normalized,
                    rect.localEulerAngles.z,
                    scale,
                    referencePileScale,
                    referenceDropSurfaceScale,
                    referenceColliderSize);
                CaptureSnapshot();
#if UNITY_EDITOR
                if (changed)
                {
                    UnityEditor.EditorUtility.SetDirty(targetZone);
                    UnityEditor.EditorUtility.SetDirty(targetZone.transform);
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

        public static bool ApplyPresentationToZone(
            DuelZone3D zone,
            Vector2 normalized,
            float angle,
            float scale,
            Vector3 pileReferenceScale,
            Vector3 dropSurfaceReferenceScale,
            Vector3 colliderReferenceSize)
        {
            if (zone == null)
                return false;

            zone.EnsurePresentationAnchors();
            Transform zoneRoot = zone.transform;
            MasterDuelArena3D arena =
                zone.GetComponentInParent<MasterDuelArena3D>(true);
            Transform fieldRoot = arena != null ? arena.transform : null;
            Vector3 local = fieldRoot != null
                ? fieldRoot.InverseTransformPoint(zoneRoot.position)
                : zoneRoot.position;
            local.x = (normalized.x - 0.5f) * FieldWidth;
            local.z = (normalized.y - 0.5f) * FieldDepth;
            Vector3 desiredPosition = fieldRoot != null
                ? fieldRoot.TransformPoint(local)
                : local;
            Quaternion desiredRotation =
                (fieldRoot != null ? fieldRoot.rotation : Quaternion.identity) *
                Quaternion.Euler(0f, -NormalizeAngle(angle), 0f);
            bool changed =
                (zoneRoot.position - desiredPosition).sqrMagnitude > 0.000001f ||
                Quaternion.Angle(zoneRoot.rotation, desiredRotation) > 0.001f;
            zoneRoot.SetPositionAndRotation(desiredPosition, desiredRotation);

            Transform anchor = zone.CardPresentationAnchor;
            Vector3 anchorPosition = anchor.localPosition;
            Vector3 desiredAnchorPosition =
                new(0f, anchorPosition.y, 0f);
            Vector3 desiredAnchorScale =
                Vector3.one * Mathf.Max(0.1f, scale);
            changed |=
                (anchor.localPosition - desiredAnchorPosition).sqrMagnitude >
                    0.000001f ||
                Quaternion.Angle(anchor.localRotation, Quaternion.identity) >
                    0.001f ||
                (anchor.localScale - desiredAnchorScale).sqrMagnitude >
                    0.000001f;
            anchor.localPosition = desiredAnchorPosition;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = desiredAnchorScale;

            Transform pile = zoneRoot.Find("Card Stack");
            if (pile != null)
            {
                Vector3 baseScale = pileReferenceScale.sqrMagnitude > 0.0001f
                    ? pileReferenceScale
                    : pile.localScale;
                Vector3 desiredPileScale = baseScale * Mathf.Max(0.1f, scale);
                changed |=
                    (pile.localScale - desiredPileScale).sqrMagnitude >
                    0.000001f;
                pile.localScale = desiredPileScale;
            }

            // O brilho de selecao e a area de clique fazem parte somente da
            // apresentacao da zona. Eles acompanham o mesmo tamanho editado para
            // a carta sem alterar StableId, tipo, dono ou qualquer regra do Core.
            Transform dropSurface = zoneRoot.Find("Card Inset");
            if (dropSurface != null)
            {
                Vector3 baseScale =
                    dropSurfaceReferenceScale.sqrMagnitude > 0.0001f
                        ? dropSurfaceReferenceScale
                        : dropSurface.localScale;
                Vector3 desiredSurfaceScale = new(
                    baseScale.x * Mathf.Max(0.1f, scale),
                    baseScale.y,
                    baseScale.z * Mathf.Max(0.1f, scale));
                changed |=
                    (dropSurface.localScale - desiredSurfaceScale).sqrMagnitude >
                    0.000001f;
                dropSurface.localScale = desiredSurfaceScale;
            }

            BoxCollider collider = zoneRoot.GetComponent<BoxCollider>();
            if (collider != null)
            {
                Vector3 baseSize = colliderReferenceSize.sqrMagnitude > 0.0001f
                    ? colliderReferenceSize
                    : collider.size;
                Vector3 desiredColliderSize = new(
                    baseSize.x * Mathf.Max(0.1f, scale),
                    baseSize.y,
                    baseSize.z * Mathf.Max(0.1f, scale));
                changed |=
                    (collider.size - desiredColliderSize).sqrMagnitude >
                    0.000001f;
                collider.size = desiredColliderSize;
            }

            return changed;
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
                   (rect.localScale - lastLocalScale).sqrMagnitude > 0.0001f ||
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

        private static Vector3 CapturePileScale(DuelZone3D zone)
        {
            Transform pile = zone != null ? zone.transform.Find("Card Stack") : null;
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

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
