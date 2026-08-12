using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Stores only the authored screen presentation of duel zones. Gameplay
    /// identity and duel rules remain in DuelZone3D/Core; this component only
    /// places the corresponding visual transforms after the arena is ready.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelAuthoredZoneLayout : MonoBehaviour
    {
        public const float RuntimeCardScale = 0.00745f;
        private static readonly Vector2 RuntimeCardPixels =
            new Vector2(178f, 258f);

        [Serializable]
        public struct Entry
        {
            [SerializeField] private string stableZoneId;
            [SerializeField] private Vector2 viewportCenter;
            [SerializeField] private Vector2 viewportRightHalfAxis;
            [SerializeField] private Vector2 viewportUpHalfAxis;
            [SerializeField] private float screenAngle;
            [SerializeField] private Vector3 pileReferenceScale;
            [SerializeField] private Vector3 surfaceReferenceScale;
            [SerializeField] private Vector3 colliderReferenceSize;

            public string StableZoneId => stableZoneId;
            public Vector2 ViewportCenter => viewportCenter;
            public Vector2 ViewportRightHalfAxis => viewportRightHalfAxis;
            public Vector2 ViewportUpHalfAxis => viewportUpHalfAxis;
            public float ScreenAngle => screenAngle;
            public Vector3 PileReferenceScale => pileReferenceScale;
            public Vector3 SurfaceReferenceScale => surfaceReferenceScale;
            public Vector3 ColliderReferenceSize => colliderReferenceSize;

            public Entry(
                string id,
                Vector2 center,
                Vector2 rightHalfAxis,
                Vector2 upHalfAxis,
                float angle,
                Vector3 pileScale,
                Vector3 surfaceScale,
                Vector3 colliderSize)
            {
                stableZoneId = id;
                viewportCenter = center;
                viewportRightHalfAxis = rightHalfAxis;
                viewportUpHalfAxis = upHalfAxis;
                screenAngle = angle;
                pileReferenceScale = pileScale;
                surfaceReferenceScale = surfaceScale;
                colliderReferenceSize = colliderSize;
            }
        }

        [Header("Layout visual salvo pela Scene")]
        [SerializeField] private List<Entry> zones = new List<Entry>();
        [SerializeField] private Camera duelCamera;

        public IReadOnlyList<Entry> Zones => zones;

        private IEnumerator Start()
        {
            // MasterDuelArena3D and the camera configure their runtime view in
            // Awake/Start. Applying after those steps prevents legacy setup
            // code from replacing the visual authoring data.
            yield return null;
            yield return null;
            ApplyAll();
        }

        public void Upsert(
            DuelZone3D zone,
            Vector2 center,
            Vector2 rightHalfAxis,
            Vector2 upHalfAxis,
            float angle,
            Vector3 pileScale,
            Vector3 surfaceScale,
            Vector3 colliderSize)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.StableId))
                return;

            int index = zones.FindIndex(value =>
                string.Equals(
                    value.StableZoneId,
                    zone.StableId,
                    StringComparison.Ordinal));
            if (index >= 0)
            {
                Entry old = zones[index];
                if (pileScale.sqrMagnitude < 0.0001f)
                    pileScale = old.PileReferenceScale;
                if (surfaceScale.sqrMagnitude < 0.0001f)
                    surfaceScale = old.SurfaceReferenceScale;
                if (colliderSize.sqrMagnitude < 0.0001f)
                    colliderSize = old.ColliderReferenceSize;
            }

            Entry entry = new Entry(
                zone.StableId,
                center,
                rightHalfAxis,
                upHalfAxis,
                angle,
                pileScale,
                surfaceScale,
                colliderSize);
            if (index >= 0)
                zones[index] = entry;
            else
                zones.Add(entry);
            zones = zones
                .OrderBy(value => value.StableZoneId, StringComparer.Ordinal)
                .ToList();
        }

        public bool TryGet(string stableZoneId, out Entry entry)
        {
            int index = zones.FindIndex(value =>
                string.Equals(
                    value.StableZoneId,
                    stableZoneId,
                    StringComparison.Ordinal));
            if (index >= 0)
            {
                entry = zones[index];
                return true;
            }

            entry = default;
            return false;
        }

        public int ApplyAll()
        {
            Camera camera = ResolveCamera();
            if (camera == null || zones.Count == 0)
                return 0;

            Dictionary<string, DuelZone3D> byId =
                FindObjectsByType<DuelZone3D>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Where(zone =>
                        zone != null &&
                        zone.gameObject.scene == gameObject.scene &&
                        !string.IsNullOrWhiteSpace(zone.StableId))
                    .GroupBy(zone => zone.StableId)
                    .ToDictionary(group => group.Key, group => group.First());

            int applied = 0;
            foreach (Entry entry in zones)
            {
                if (!byId.TryGetValue(
                        entry.StableZoneId,
                        out DuelZone3D zone))
                {
                    continue;
                }

                if (ApplyToZone(zone, entry, camera))
                    applied++;
            }

            return applied;
        }

        public bool ApplyOne(DuelZone3D zone)
        {
            return zone != null &&
                   TryGet(zone.StableId, out Entry entry) &&
                   ApplyToZone(zone, entry, ResolveCamera());
        }

        public static bool ApplyToZone(
            DuelZone3D zone,
            Entry entry,
            Camera camera)
        {
            if (zone == null || camera == null)
                return false;

            zone.EnsurePresentationAnchors();
            Transform zoneRoot = zone.transform;
            MasterDuelArena3D arena =
                zone.GetComponentInParent<MasterDuelArena3D>(true);
            Transform arenaRoot = arena != null ? arena.transform : null;
            Vector3 planeNormal = arenaRoot != null
                ? arenaRoot.up
                : Vector3.up;
            Plane fieldPlane = new Plane(planeNormal, zoneRoot.position);

            if (!TryHitViewport(
                    camera,
                    entry.ViewportCenter,
                    fieldPlane,
                    out Vector3 center) ||
                !TryHitViewport(
                    camera,
                    entry.ViewportCenter -
                    entry.ViewportRightHalfAxis,
                    fieldPlane,
                    out Vector3 left) ||
                !TryHitViewport(
                    camera,
                    entry.ViewportCenter +
                    entry.ViewportRightHalfAxis,
                    fieldPlane,
                    out Vector3 right) ||
                !TryHitViewport(
                    camera,
                    entry.ViewportCenter - entry.ViewportUpHalfAxis,
                    fieldPlane,
                    out Vector3 bottom) ||
                !TryHitViewport(
                    camera,
                    entry.ViewportCenter + entry.ViewportUpHalfAxis,
                    fieldPlane,
                    out Vector3 top))
            {
                return false;
            }

            float worldWidth = Mathf.Max(0.01f, Vector3.Distance(left, right));
            float worldHeight = Mathf.Max(0.01f, Vector3.Distance(bottom, top));
            float widthScale = worldWidth /
                (RuntimeCardPixels.x * RuntimeCardScale);
            float heightScale = worldHeight /
                (RuntimeCardPixels.y * RuntimeCardScale);
            float uniformScale = Mathf.Clamp(
                (widthScale + heightScale) * 0.5f,
                0.05f,
                8f);

            Quaternion rotation =
                (arenaRoot != null
                    ? arenaRoot.rotation
                    : Quaternion.identity) *
                Quaternion.Euler(0f, -NormalizeAngle(entry.ScreenAngle), 0f);
            bool changed =
                (zoneRoot.position - center).sqrMagnitude > 0.000001f ||
                Quaternion.Angle(zoneRoot.rotation, rotation) > 0.001f;
            zoneRoot.SetPositionAndRotation(center, rotation);

            Transform anchor = zone.CardPresentationAnchor;
            Vector3 anchorPosition = new Vector3(
                0f,
                anchor.localPosition.y,
                0f);
            Vector3 anchorScale = Vector3.one * uniformScale;
            changed |=
                (anchor.localPosition - anchorPosition).sqrMagnitude >
                    0.000001f ||
                Quaternion.Angle(anchor.localRotation, Quaternion.identity) >
                    0.001f ||
                (anchor.localScale - anchorScale).sqrMagnitude > 0.000001f;
            anchor.localPosition = anchorPosition;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = anchorScale;

            Transform combatLabel = zone.CombatLabelAnchor;
            float displayedCardHeight =
                RuntimeCardPixels.y * RuntimeCardScale * uniformScale;
            float labelDistance = displayedCardHeight * 0.5f +
                                  Mathf.Max(0.08f,
                                      displayedCardHeight * 0.055f);
            Vector3 labelPosition = new Vector3(
                0f,
                combatLabel.localPosition.y,
                -labelDistance);
            changed |=
                (combatLabel.localPosition - labelPosition).sqrMagnitude >
                    0.000001f ||
                Quaternion.Angle(
                    combatLabel.localRotation,
                    Quaternion.identity) > 0.001f;
            combatLabel.localPosition = labelPosition;
            combatLabel.localRotation = Quaternion.identity;
            combatLabel.localScale = Vector3.one;

            Transform pile = zoneRoot.Find("Card Stack");
            if (pile != null &&
                entry.PileReferenceScale.sqrMagnitude > 0.0001f)
            {
                Vector3 desired = entry.PileReferenceScale * uniformScale;
                changed |= (pile.localScale - desired).sqrMagnitude >
                           0.000001f;
                pile.localScale = desired;
            }

            Transform surface = zoneRoot.Find("Card Inset");
            if (surface != null)
            {
                Vector3 baseScale =
                    entry.SurfaceReferenceScale.sqrMagnitude > 0.0001f
                        ? entry.SurfaceReferenceScale
                        : surface.localScale;
                Vector3 desired = new Vector3(
                    baseScale.x * uniformScale,
                    baseScale.y,
                    baseScale.z * uniformScale);
                changed |= (surface.localScale - desired).sqrMagnitude >
                           0.000001f;
                surface.localScale = desired;
            }

            BoxCollider collider = zoneRoot.GetComponent<BoxCollider>();
            if (collider != null)
            {
                Vector3 baseSize =
                    entry.ColliderReferenceSize.sqrMagnitude > 0.0001f
                        ? entry.ColliderReferenceSize
                        : collider.size;
                Vector3 desired = new Vector3(
                    baseSize.x * uniformScale,
                    baseSize.y,
                    baseSize.z * uniformScale);
                changed |= (collider.size - desired).sqrMagnitude >
                           0.000001f;
                collider.size = desired;
            }

            return changed;
        }

        private Camera ResolveCamera()
        {
            if (duelCamera == null)
                duelCamera = Camera.main;
            return duelCamera;
        }

        private static bool TryHitViewport(
            Camera camera,
            Vector2 viewport,
            Plane plane,
            out Vector3 point)
        {
            Ray ray = camera.ViewportPointToRay(
                new Vector3(viewport.x, viewport.y, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
