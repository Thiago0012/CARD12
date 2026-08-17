using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneArena.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class DuelTestPerspectiveController : MonoBehaviour
    {
        public static DuelTestPerspectiveController Instance { get; private set; }

        [Header("Cliente local simulado")]
        [SerializeField] private DuelPlayerSide localClientSide = DuelPlayerSide.PlayerOne;
        [SerializeField] private bool allowSwitchingClients = true;

        [Header("Câmera")]
        [SerializeField] private Camera duelCamera;
        [SerializeField] private Vector3 playerOneCameraPosition = new Vector3(0f, 16.2f, -15.2f);
        [SerializeField] private Vector3 playerOneCameraEuler = new Vector3(46.5f, 0f, 0f);
        [SerializeField] private bool useStaticFieldCamera;
        [SerializeField, Min(1f)] private float staticFieldOrthographicSize =
            MasterDuelArena3D.StaticFieldDepth * 0.5f;
        [SerializeField, Min(0.05f)] private float transitionDuration = 0.65f;

        [Header("Configuração do editor")]
        [SerializeField, HideInInspector] private int editorSetupVersion;

        private Coroutine _cameraTransition;
        private Transform _playerOne;
        private Transform _playerTwo;
        private DuelFieldRegistry _fieldRegistry;
        private float _lastStaticFieldAspect = -1f;

        public event Action<DuelPlayerSide> PerspectiveChanged;
        public DuelPlayerSide LocalClientSide => localClientSide;
        public bool AllowSwitchingClients => allowSwitchingClients;
        public bool IsTransitioning => _cameraTransition != null;
        public int EditorSetupVersion => editorSetupVersion;

        private void Awake()
        {
            Instance = this;
            ResolveReferences();
            ApplyPerspective(localClientSide, true);
        }

        private void OnEnable()
        {
            Instance = this;
            ResolveReferences();
            EnsureBothHiddenHandPreviews();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (useStaticFieldCamera && duelCamera != null &&
                !Mathf.Approximately(_lastStaticFieldAspect, duelCamera.aspect))
            {
                ApplyProjectionMode();
            }

            if (!Application.isPlaying || !allowSwitchingClients || Keyboard.current == null)
                return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame ||
                Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                SwitchTo(DuelPlayerSide.PlayerOne);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame ||
                     Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                SwitchTo(DuelPlayerSide.PlayerTwo);
            }
            else if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleClient();
            }
        }

        public bool CanControl(DuelPlayerSide side)
        {
            return !IsTransitioning && side == localClientSide;
        }

        public void ToggleClient()
        {
            SwitchTo(localClientSide == DuelPlayerSide.PlayerOne
                ? DuelPlayerSide.PlayerTwo
                : DuelPlayerSide.PlayerOne);
        }

        public void SwitchTo(DuelPlayerSide side)
        {
            if (!allowSwitchingClients && side != localClientSide)
                return;

            ApplyPerspective(side, false);
        }

        public void ConfigureClientSwitching(
            bool enabled,
            DuelPlayerSide initialSide)
        {
            // No modo bot, P2 pertence ao simulador. Isto impede que a mão
            // escondida do oponente seja aberta pelos controles de teste.
            allowSwitchingClients = true;
            ApplyPerspective(initialSide, true);
            allowSwitchingClients = enabled;
            RefreshZoneAuthority();
        }

        public void ApplyPerspective(DuelPlayerSide side, bool immediate)
        {
            localClientSide = side;
            ResolveReferences();
            UpdateHiddenHandPreviews();
            SetAllZoneInput(false);

            var notifyImmediately = true;
            if (duelCamera != null)
            {
                ApplyProjectionMode();
                GetCameraPose(side, out var targetPosition, out var targetRotation);
                if (_cameraTransition != null)
                    StopCoroutine(_cameraTransition);

                if (immediate || !Application.isPlaying)
                {
                    duelCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
                }
                else
                {
                    notifyImmediately = false;
                    _cameraTransition = StartCoroutine(
                        AnimateCamera(side, targetPosition, targetRotation));
                }
            }

            if (notifyImmediately)
            {
                RefreshZoneAuthority();
                PerspectiveChanged?.Invoke(side);
            }
        }

        public void CapturePlayerOneCameraPose()
        {
            ResolveReferences();
            if (duelCamera == null)
                return;

            playerOneCameraPosition = duelCamera.transform.position;
            playerOneCameraEuler = duelCamera.transform.eulerAngles;
        }

        public void ConfigureStaticFieldCamera(bool enabled)
        {
            useStaticFieldCamera = enabled;
            if (enabled)
            {
                playerOneCameraPosition = new Vector3(0f, 12f, 0f);
                playerOneCameraEuler = new Vector3(90f, 0f, 0f);
                staticFieldOrthographicSize =
                    MasterDuelArena3D.StaticFieldDepth * 0.5f;
            }
            ResolveReferences();
            ApplyProjectionMode();
            ApplyPerspective(localClientSide, true);
        }

        private void ApplyProjectionMode()
        {
            if (duelCamera == null)
                return;
            duelCamera.orthographic = useStaticFieldCamera;
            if (!useStaticFieldCamera)
                return;
            float aspect = Mathf.Max(0.1f, duelCamera.aspect);
            float sizeRequiredByWidth =
                MasterDuelArena3D.StaticFieldWidth * 0.5f / aspect;
            duelCamera.orthographicSize = Mathf.Max(
                staticFieldOrthographicSize,
                sizeRequiredByWidth);
            duelCamera.nearClipPlane = 0.1f;
            duelCamera.farClipPlane = 100f;
            _lastStaticFieldAspect = aspect;
        }

        public void MarkEditorSetupComplete(int version)
        {
            editorSetupVersion = Mathf.Max(editorSetupVersion, version);
        }

        public void SynchronizeOpponentDecksFromPlayerOne()
        {
            ResolveReferences();
            if (_playerOne == null || _playerTwo == null)
                return;

            MirrorDeck(
                _playerOne.Find("SpecialZones/MainDeck"),
                _playerTwo.Find("SpecialZones/MainDeck"));
            MirrorDeck(
                _playerOne.Find("SpecialZones/ExtraDeck"),
                _playerTwo.Find("SpecialZones/ExtraDeck"));
        }

        public void EnsureBothHiddenHandPreviews()
        {
            ResolveReferences();
            if (_playerOne == null || _playerTwo == null)
                return;

            var playerOnePreview = _playerOne.Find("OpponentHandPreview");
            var playerTwoPreview = _playerTwo.Find("OpponentHandPreview");
            if (playerOnePreview == null && playerTwoPreview != null)
            {
                var clone = Instantiate(playerTwoPreview.gameObject, _playerOne, false);
                clone.name = "OpponentHandPreview";
                foreach (Transform card in clone.transform)
                {
                    var position = card.localPosition;
                    card.localPosition = new Vector3(position.x, position.y, -Mathf.Abs(position.z));
                    card.localRotation = Quaternion.identity;
                }
            }

            UpdateHiddenHandPreviews();
        }

        public void SetHiddenHandCardCount(
            DuelPlayerSide owner,
            int cardCount)
        {
            ResolveReferences();
            var player =
                owner == DuelPlayerSide.PlayerOne
                    ? _playerOne
                    : _playerTwo;
            var preview =
                player != null
                    ? player.Find("OpponentHandPreview")
                    : null;
            if (preview == null)
                return;

            // The authored preview may have been disabled while the arena UI
            // was binding. Its visibility is a perspective concern; the
            // children below are the authoritative hand-count concern.
            preview.gameObject.SetActive(owner != localClientSide);

            var count = Mathf.Max(0, cardCount);
            Transform template =
                preview.childCount > 0
                    ? preview.GetChild(0)
                    : null;
            if (template == null)
                return;

            while (preview.childCount < count)
            {
                var clone =
                    Instantiate(
                        template.gameObject,
                        preview,
                        false);
                clone.name =
                    $"HiddenCard_{preview.childCount}";
            }

            var maximumSpan = 5.8f;
            var spacing = count <= 1
                ? 0f
                : Mathf.Min(
                    1.18f,
                    maximumSpan / (count - 1));
            var center = (count - 1) * 0.5f;
            var localZ =
                owner == DuelPlayerSide.PlayerOne
                    ? (useStaticFieldCamera ? -4.62f : -7.15f)
                    : (useStaticFieldCamera ? 4.62f : 7.15f);
            var rotation =
                owner == DuelPlayerSide.PlayerOne
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 180f, 0f);

            for (var index = 0;
                 index < preview.childCount;
                 index++)
            {
                var card = preview.GetChild(index);
                var visible = index < count;
                card.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                card.localPosition =
                    new Vector3(
                        (index - center) * spacing,
                        0.5f,
                        localZ);
                card.localRotation = rotation;
            }
        }

        private IEnumerator AnimateCamera(
            DuelPlayerSide side,
            Vector3 targetPosition,
            Quaternion targetRotation)
        {
            var startPosition = duelCamera.transform.position;
            var startRotation = duelCamera.transform.rotation;
            var duration = Mathf.Max(0.05f, transitionDuration);

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var normalized = Mathf.Clamp01(elapsed / duration);
                var eased = normalized * normalized * (3f - 2f * normalized);
                duelCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, targetPosition, eased),
                    Quaternion.Slerp(startRotation, targetRotation, eased));
                yield return null;
            }

            duelCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
            _cameraTransition = null;
            RefreshZoneAuthority();
            PerspectiveChanged?.Invoke(side);
        }

        private void GetCameraPose(DuelPlayerSide side, out Vector3 position, out Quaternion rotation)
        {
            if (side == DuelPlayerSide.PlayerOne)
            {
                position = playerOneCameraPosition;
                rotation = Quaternion.Euler(playerOneCameraEuler);
                return;
            }

            position = new Vector3(
                playerOneCameraPosition.x,
                playerOneCameraPosition.y,
                -playerOneCameraPosition.z);
            rotation = Quaternion.Euler(
                playerOneCameraEuler.x,
                playerOneCameraEuler.y + 180f,
                -playerOneCameraEuler.z);
        }

        private void ResolveReferences()
        {
            if (duelCamera == null)
                duelCamera = Camera.main;
            if (_fieldRegistry == null)
                _fieldRegistry = GetComponent<DuelFieldRegistry>();

            var players = transform.Find("Players");
            if (players == null)
                return;

            _playerOne = players.Find("PLAYER_1");
            _playerTwo = players.Find("PLAYER_2");
        }

        private void RefreshZoneAuthority()
        {
            if (_fieldRegistry == null)
                _fieldRegistry = GetComponent<DuelFieldRegistry>();
            var zones = Array.Empty<DuelZone3D>();
            if (_fieldRegistry != null)
            {
                _fieldRegistry.RebuildIndex();
                zones = new DuelZone3D[_fieldRegistry.Zones.Count];
                for (var i = 0; i < zones.Length; i++)
                    zones[i] = _fieldRegistry.Zones[i];
            }
            else
            {
                zones = FindObjectsByType<DuelZone3D>(
                    FindObjectsInactive.Include);
            }

            foreach (var zone in zones)
            {
                if (zone == null)
                    continue;

                zone.EnsureIdentityFromHierarchy(false);
                var isPlayableFieldZone =
                    zone.Kind == DuelZoneKind.Monster ||
                    zone.Kind == DuelZoneKind.SpellTrap ||
                    zone.Kind == DuelZoneKind.Field;
                zone.SetLocalControlEnabled(
                    isPlayableFieldZone && zone.Owner == localClientSide);
            }
        }

        private void SetAllZoneInput(bool enabled)
        {
            if (_fieldRegistry == null)
                _fieldRegistry = GetComponent<DuelFieldRegistry>();
            if (_fieldRegistry == null)
                return;

            foreach (var zone in _fieldRegistry.Zones)
            {
                if (zone != null)
                    zone.SetLocalControlEnabled(enabled);
            }
        }

        private void UpdateHiddenHandPreviews()
        {
            if (_playerOne == null || _playerTwo == null)
                return;

            var playerOnePreview = _playerOne.Find("OpponentHandPreview");
            var playerTwoPreview = _playerTwo.Find("OpponentHandPreview");
            if (playerOnePreview != null)
                playerOnePreview.gameObject.SetActive(localClientSide == DuelPlayerSide.PlayerTwo);
            if (playerTwoPreview != null)
                playerTwoPreview.gameObject.SetActive(localClientSide == DuelPlayerSide.PlayerOne);
        }

        private static void MirrorDeck(Transform source, Transform target)
        {
            if (source == null || target == null)
                return;

            var position = source.localPosition;
            target.localPosition = new Vector3(position.x, position.y, -position.z);
            target.localScale = source.localScale;
            target.localRotation = Quaternion.Euler(0f, 180f, 0f) * source.localRotation;
            CopyChildTransforms(source, target);
        }

        private static void CopyChildTransforms(Transform source, Transform target)
        {
            foreach (Transform sourceChild in source)
            {
                var targetChild = target.Find(sourceChild.name);
                if (targetChild == null)
                    continue;

                targetChild.localPosition = sourceChild.localPosition;
                targetChild.localRotation = sourceChild.localRotation;
                targetChild.localScale = sourceChild.localScale;
                CopyChildTransforms(sourceChild, targetChild);
            }
        }
    }

    public abstract class ArenaBorderFollowerBehaviour : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float borderMargin = 0.55f;
        [SerializeField] private float minimumHalfWidth = 9.05f;
        [SerializeField] private float minimumHalfDepth = 7.75f;
        [SerializeField, HideInInspector] private float currentHalfWidth;
        [SerializeField, HideInInspector] private float currentHalfDepth;

        protected virtual void OnEnable()
        {
            RefreshBorder(true);
        }

        protected virtual void Update()
        {
            if (!Application.isPlaying)
                RefreshBorder(false);
        }

        public void RefreshBorder(bool force)
        {
            var border = transform.Find("Arena Border");
            if (border == null)
                return;

            var hasBounds = TryGetDeckBounds(out var deckBounds);
            var halfWidth = hasBounds
                ? Mathf.Max(minimumHalfWidth, Mathf.Max(Mathf.Abs(deckBounds.min.x), Mathf.Abs(deckBounds.max.x)) + borderMargin)
                : minimumHalfWidth;
            var halfDepth = hasBounds
                ? Mathf.Max(minimumHalfDepth, Mathf.Max(Mathf.Abs(deckBounds.min.z), Mathf.Abs(deckBounds.max.z)) + borderMargin)
                : minimumHalfDepth;

            if (!force &&
                Mathf.Abs(currentHalfWidth - halfWidth) < 0.002f &&
                Mathf.Abs(currentHalfDepth - halfDepth) < 0.002f)
            {
                return;
            }

            currentHalfWidth = halfWidth;
            currentHalfDepth = halfDepth;
            PositionHorizontalBorder(border.Find("Horizontal Stones"), halfWidth, halfDepth);
            PositionVerticalBorder(border.Find("Vertical Stones"), halfWidth, halfDepth);
        }

        private bool TryGetDeckBounds(out Bounds localBounds)
        {
            localBounds = default;
            var hasBounds = false;
            var zones = GetComponentsInChildren<DuelZone3D>(true);
            foreach (var zone in zones)
            {
                if (zone.Kind != DuelZoneKind.MainDeck && zone.Kind != DuelZoneKind.ExtraDeck)
                    continue;

                foreach (var renderer in zone.GetComponentsInChildren<Renderer>(true))
                {
                    var worldBounds = renderer.bounds;
                    foreach (var corner in BoundsCorners(worldBounds))
                    {
                        var localPoint = transform.InverseTransformPoint(corner);
                        if (!hasBounds)
                        {
                            localBounds = new Bounds(localPoint, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localPoint);
                        }
                    }
                }
            }

            return hasBounds;
        }

        private static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            yield return new Vector3(min.x, min.y, min.z);
            yield return new Vector3(min.x, min.y, max.z);
            yield return new Vector3(min.x, max.y, min.z);
            yield return new Vector3(min.x, max.y, max.z);
            yield return new Vector3(max.x, min.y, min.z);
            yield return new Vector3(max.x, min.y, max.z);
            yield return new Vector3(max.x, max.y, min.z);
            yield return new Vector3(max.x, max.y, max.z);
        }

        private static void PositionHorizontalBorder(Transform group, float halfWidth, float halfDepth)
        {
            if (group == null)
                return;

            var bottom = new List<Transform>();
            var top = new List<Transform>();
            foreach (Transform stone in group)
            {
                (stone.localPosition.z < 0f ? bottom : top).Add(stone);
            }

            PositionHorizontalRow(bottom, -halfDepth, halfWidth);
            PositionHorizontalRow(top, halfDepth, halfWidth);
        }

        private static void PositionHorizontalRow(List<Transform> row, float z, float halfWidth)
        {
            row.Sort((left, right) => left.localPosition.x.CompareTo(right.localPosition.x));
            for (var i = 0; i < row.Count; i++)
            {
                var normalized = row.Count <= 1 ? 0.5f : (float)i / (row.Count - 1);
                var position = row[i].localPosition;
                position.x = Mathf.Lerp(-halfWidth + 0.65f, halfWidth - 0.65f, normalized);
                position.z = z;
                row[i].localPosition = position;
            }
        }

        private static void PositionVerticalBorder(Transform group, float halfWidth, float halfDepth)
        {
            if (group == null)
                return;

            var left = new List<Transform>();
            var right = new List<Transform>();
            foreach (Transform stone in group)
            {
                (stone.localPosition.x < 0f ? left : right).Add(stone);
            }

            PositionVerticalRow(left, -halfWidth, halfDepth);
            PositionVerticalRow(right, halfWidth, halfDepth);
        }

        private static void PositionVerticalRow(List<Transform> row, float x, float halfDepth)
        {
            row.Sort((left, right) => left.localPosition.z.CompareTo(right.localPosition.z));
            for (var i = 0; i < row.Count; i++)
            {
                var normalized = row.Count <= 1 ? 0.5f : (float)i / (row.Count - 1);
                var position = row[i].localPosition;
                position.x = x;
                position.z = Mathf.Lerp(-halfDepth + 0.95f, halfDepth - 0.95f, normalized);
                row[i].localPosition = position;
            }
        }
    }
}
