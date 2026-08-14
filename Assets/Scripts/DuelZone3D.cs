using ArcaneArena.Multiplayer;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcaneArena
{
    [DisallowMultipleComponent]
    public sealed class DuelZone3D :
        MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [SerializeField] private DuelZoneAddress address;
        [SerializeField] private bool acceptsLocalInput;
        [SerializeField] private string placedCardId;
        [SerializeField] private Sprite placedCard;
        [SerializeField] private bool faceUp = true;
        [SerializeField] private bool sharedVisualProxy;
        [Header("Apresentacao editavel na Scene")]
        [SerializeField] private Transform cardPresentationAnchor;
        [SerializeField] private Transform combatLabelAnchor;
        [SerializeField] private DuelMonsterPosition monsterPosition =
            DuelMonsterPosition.FaceUpAttack;
        private Renderer dropSurface;
        private Material dropSurfaceMaterial;
        private Color dropSurfaceColor;
        private bool dropHighlighted;
        private Color dropHighlightColor =
            new Color(0.08f, 0.58f, 1f, 1f);
        private bool disabledByCore;
        private static readonly Color DisabledByCoreColor =
            new Color(0.34f, 0.055f, 0.07f, 1f);
        private bool pointerFocused;
        private LineRenderer specialZoneOutline;
        private Material specialZoneOutlineMaterial;

        public Sprite PlacedCard => placedCard;
        public string StableId => address.StableId;
        public DuelPlayerSide Owner => address.Owner;
        public DuelZoneKind Kind => address.Kind;
        public int ZoneIndex => address.Index;
        public string PlacedCardId => placedCardId;
        public bool IsFaceUp => faceUp;
        public DuelMonsterPosition MonsterPosition => monsterPosition;
        public bool AcceptsLocalInput => acceptsLocalInput;
        public bool IsDisabledByCore => disabledByCore;
        public Transform CardPresentationAnchor
        {
            get
            {
                EnsurePresentationAnchors();
                return cardPresentationAnchor != null
                    ? cardPresentationAnchor
                    : transform;
            }
        }
        public Transform CombatLabelAnchor
        {
            get
            {
                EnsurePresentationAnchors();
                return combatLabelAnchor != null
                    ? combatLabelAnchor
                    : transform;
            }
        }
        public bool HasValidIdentity =>
            !string.IsNullOrWhiteSpace(address.StableId) &&
            System.Enum.IsDefined(typeof(DuelPlayerSide), address.Owner) &&
            System.Enum.IsDefined(typeof(DuelZoneKind), address.Kind) &&
            address.Index >= 0;

        public void Setup(
            DuelPlayerSide owner,
            DuelZoneKind kind,
            int index,
            bool interactive,
            bool useSharedVisualProxy = false)
        {
            address = new DuelZoneAddress(owner, kind, index);
            acceptsLocalInput = interactive;
            sharedVisualProxy = useSharedVisualProxy;
            EnsurePresentationAnchors();
            RefreshExtraMonsterZoneSurface();
        }

        private void Awake()
        {
            EnsurePresentationAnchors();
        }

        public void EnsurePresentationAnchors()
        {
            if (cardPresentationAnchor == null)
            {
                cardPresentationAnchor = FindOrCreateAnchor(
                    "POSICAO VISUAL DA CARTA",
                    new Vector3(0f, 0.12f, 0f),
                    DuelCardAnchorRole.Card);
            }
            if (combatLabelAnchor == null)
            {
                combatLabelAnchor = FindOrCreateAnchor(
                    "POSICAO DO ATK DEF",
                    new Vector3(0f, 0.42f, -1.18f),
                    DuelCardAnchorRole.CombatLabel);
            }

            MigratePresentationChild(
                "Carta Invocada",
                cardPresentationAnchor);
            MigratePresentationChild(
                "Indicador de ATK",
                combatLabelAnchor);
        }

        public Transform FindPresentedCard()
        {
            Transform root = CardPresentationAnchor;
            return root != null
                ? root.Find("Carta Invocada")
                : null;
        }

        public Transform FindCombatLabel()
        {
            Transform root = CombatLabelAnchor;
            return root != null
                ? root.Find("Indicador de ATK")
                : null;
        }

        private Transform FindOrCreateAnchor(
            string anchorName,
            Vector3 defaultPosition,
            DuelCardAnchorRole role)
        {
            Transform anchor = transform.Find(anchorName);
            if (anchor == null)
            {
                var item = new GameObject(anchorName);
                anchor = item.transform;
                anchor.SetParent(transform, false);
                anchor.localPosition = defaultPosition;
                anchor.localRotation = Quaternion.identity;
                anchor.localScale = Vector3.one;
            }
            var marker =
                anchor.GetComponent<DuelCardPlacementAnchor>();
            if (marker == null)
                marker = anchor.gameObject.AddComponent<DuelCardPlacementAnchor>();
            marker.Configure(role);
            return anchor;
        }

        private void MigratePresentationChild(
            string childName,
            Transform destination)
        {
            if (destination == null)
                return;
            Transform existing = transform.Find(childName);
            if (existing == null || existing == destination)
                return;
            existing.SetParent(destination, false);
            existing.localPosition = Vector3.zero;
        }

        public void SetLocalControlEnabled(bool enabled)
        {
            acceptsLocalInput = enabled;
        }

        public bool EnsureIdentityFromHierarchy(bool resetEditorInput)
        {
            Transform ownerRoot = transform;
            while (ownerRoot != null &&
                   ownerRoot.name != "PLAYER_1" &&
                   ownerRoot.name != "PLAYER_2")
            {
                ownerRoot = ownerRoot.parent;
            }
            if (ownerRoot == null) return false;

            DuelPlayerSide owner = ownerRoot.name == "PLAYER_1"
                ? DuelPlayerSide.PlayerOne
                : DuelPlayerSide.PlayerTwo;
            DuelZoneKind kind = KindFromName(gameObject.name);
            int index = IndexFromName(gameObject.name);
            address = new DuelZoneAddress(owner, kind, index);
            if (resetEditorInput)
            {
                acceptsLocalInput =
                    owner == DuelPlayerSide.PlayerOne &&
                    (kind == DuelZoneKind.Monster ||
                     kind == DuelZoneKind.SpellTrap ||
                     kind == DuelZoneKind.Field);
            }
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.2f, 0f);
                collider.size = new Vector3(2f, 0.65f, 2.55f);
            }
            RefreshExtraMonsterZoneSurface();
            return true;
        }

        public void SetPlacedCard(
            Sprite sprite,
            string stableCardId,
            bool isFaceUp)
        {
            placedCard = sprite;
            placedCardId = stableCardId ?? string.Empty;
            faceUp = isFaceUp;
            if (Kind == DuelZoneKind.Monster)
            {
                monsterPosition = faceUp
                    ? DuelMonsterPosition.FaceUpAttack
                    : DuelMonsterPosition.FaceDownDefense;
            }
            RefreshExtraMonsterZoneSurface();
        }

        public void SetMonsterPosition(DuelMonsterPosition position)
        {
            monsterPosition = position;
            faceUp = position != DuelMonsterPosition.FaceDownDefense;
        }

        public void ClearPlacedCard()
        {
            placedCard = null;
            placedCardId = string.Empty;
            faceUp = true;
            monsterPosition = DuelMonsterPosition.FaceUpAttack;
            RefreshExtraMonsterZoneSurface();
        }

        public void SetDropHighlight(bool enabled)
        {
            SetDropHighlight(
                enabled,
                new Color(0.08f, 0.58f, 1f, 1f));
        }

        public void SetDropHighlight(bool enabled, Color color)
        {
            dropHighlighted = enabled;
            if (enabled)
                dropHighlightColor = color;
            RefreshSpecialZoneOutline(enabled);
            RefreshExtraMonsterZoneSurface();
            if (enabled)
                EnsureDropSurfaceMaterial();
            if (dropSurfaceMaterial != null)
            {
                dropSurfaceMaterial.color = enabled
                    ? dropHighlightColor
                    : disabledByCore
                        ? DisabledByCoreColor
                        : dropSurfaceColor;
            }
        }

        /// <summary>
        /// Keeps MSG_FIELD_DISABLED visible independently from transient legal
        /// action highlights. The Core remains the authority; this is only its
        /// persistent presentation on the authored board.
        /// </summary>
        public void SetCoreDisabled(bool disabled)
        {
            if (disabledByCore == disabled)
                return;
            disabledByCore = disabled;
            EnsureDropSurfaceMaterial();
            RefreshExtraMonsterZoneSurface();
            if (dropSurfaceMaterial != null && !dropHighlighted)
            {
                dropSurfaceMaterial.color = disabledByCore
                    ? DisabledByCoreColor
                    : dropSurfaceColor;
            }
        }

        private void RefreshExtraMonsterZoneSurface()
        {
            if (Kind != DuelZoneKind.Monster || ZoneIndex < 5)
                return;
            bool visibleOrInteractive =
                dropHighlighted || disabledByCore ||
                !string.IsNullOrWhiteSpace(placedCardId);
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null)
                zoneCollider.enabled = visibleOrInteractive;
            if (!sharedVisualProxy)
                return;
            Transform proxySurface = transform.Find("Card Inset");
            if (proxySurface != null)
                proxySurface.gameObject.SetActive(visibleOrInteractive);
        }

        private void EnsureDropSurfaceMaterial()
        {
            if (dropSurfaceMaterial != null)
                return;
            Transform inset = transform.Find("Card Inset");
            dropSurface = inset != null
                ? inset.GetComponent<Renderer>()
                : GetComponent<Renderer>();
            if (dropSurface == null)
                return;
            dropSurfaceColor = dropSurface.sharedMaterial.color;
            dropSurfaceMaterial = dropSurface.material;
        }

        private void Update()
        {
            if (!dropHighlighted && !pointerFocused && !disabledByCore)
                return;
            UpdateSpecialZoneOutline();
            if (dropSurfaceMaterial == null)
                return;
            if (dropHighlighted)
            {
                float pulse =
                    0.5f + 0.5f *
                    Mathf.Sin(Time.unscaledTime * 5.8f);
                Color low = Color.Lerp(
                    Color.black,
                    dropHighlightColor,
                    0.58f);
                Color high = Color.Lerp(
                    dropHighlightColor,
                    Color.white,
                    0.34f);
                dropSurfaceMaterial.color =
                    Color.Lerp(low, high, pulse);
                return;
            }
            dropSurfaceMaterial.color = disabledByCore
                ? DisabledByCoreColor
                : Color.Lerp(
                    dropSurfaceColor,
                    new Color(0.10f, 0.34f, 0.38f, 1f),
                    0.56f);
        }

        private void RefreshSpecialZoneOutline(bool enabled)
        {
            if (Kind != DuelZoneKind.ExtraDeck &&
                Kind != DuelZoneKind.Graveyard)
            {
                return;
            }
            if (enabled)
                EnsureSpecialZoneOutline();
            if (specialZoneOutline != null)
                specialZoneOutline.enabled = enabled;
        }

        private void EnsureSpecialZoneOutline()
        {
            if (specialZoneOutline != null)
                return;
            Transform outlineParent = Kind == DuelZoneKind.ExtraDeck
                ? transform.Find("Card Stack") ?? transform
                : transform;
            var outlineObject = new GameObject("Contorno de ação legal");
            outlineObject.transform.SetParent(outlineParent, false);
            specialZoneOutline = outlineObject.AddComponent<LineRenderer>();
            specialZoneOutline.useWorldSpace = false;
            specialZoneOutline.loop = true;
            specialZoneOutline.alignment = LineAlignment.View;
            specialZoneOutline.numCapVertices = 4;
            specialZoneOutline.numCornerVertices = 4;
            Shader shader = Shader.Find("Sprites/Default") ??
                            Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                specialZoneOutlineMaterial = new Material(shader)
                {
                    name = "Contorno pulsante de zona especial"
                };
                specialZoneOutline.material = specialZoneOutlineMaterial;
            }

            if (Kind == DuelZoneKind.ExtraDeck)
            {
                Transform topCard = outlineParent.Find("Top Card Back");
                float halfWidth = topCard != null
                    ? Mathf.Abs(topCard.localScale.x) * 0.5f + 0.07f
                    : 0.79f;
                float halfDepth = topCard != null
                    ? Mathf.Abs(topCard.localScale.z) * 0.5f + 0.07f
                    : 1.05f;
                float height = topCard != null
                    ? topCard.localPosition.y +
                      Mathf.Abs(topCard.localScale.y) * 0.5f + 0.035f
                    : 0.16f;
                specialZoneOutline.positionCount = 4;
                specialZoneOutline.SetPositions(new[]
                {
                    new Vector3(-halfWidth, height, -halfDepth),
                    new Vector3(-halfWidth, height, halfDepth),
                    new Vector3(halfWidth, height, halfDepth),
                    new Vector3(halfWidth, height, -halfDepth)
                });
            }
            else
            {
                const int segments = 40;
                specialZoneOutline.positionCount = segments;
                for (int index = 0; index < segments; index++)
                {
                    float angle = index * Mathf.PI * 2f / segments;
                    specialZoneOutline.SetPosition(
                        index,
                        new Vector3(
                            Mathf.Cos(angle) * 1.18f,
                            0.34f,
                            Mathf.Sin(angle) * 1.18f));
                }
            }
        }

        private void UpdateSpecialZoneOutline()
        {
            if (specialZoneOutline == null || !dropHighlighted)
                return;
            float pulse = 0.5f + 0.5f *
                          Mathf.Sin(Time.unscaledTime * 5.8f);
            Color color = Color.Lerp(
                new Color(
                    dropHighlightColor.r,
                    dropHighlightColor.g,
                    dropHighlightColor.b,
                    0.72f),
                Color.white,
                pulse * 0.38f);
            specialZoneOutline.startColor = color;
            specialZoneOutline.endColor = color;
            specialZoneOutline.widthMultiplier =
                Mathf.Lerp(0.075f, 0.15f, pulse);
        }

        private void OnDestroy()
        {
            if (specialZoneOutlineMaterial != null)
                Destroy(specialZoneOutlineMaterial);
            if (dropSurfaceMaterial != null)
                Destroy(dropSurfaceMaterial);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!HasValidIdentity &&
                !EnsureIdentityFromHierarchy(false))
            {
                return;
            }
            FindAnyObjectByType<CardArenaBootstrap>()?.HandleZoneClick(
                this,
                eventData.clickCount,
                eventData.pointerId);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!HasValidIdentity &&
                !EnsureIdentityFromHierarchy(false))
            {
                pointerFocused = false;
                return;
            }
            pointerFocused = true;
            EnsureDropSurfaceMaterial();
            FindAnyObjectByType<CardArenaBootstrap>()?.HandleZoneHover(
                this,
                true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerFocused = false;
            if (!dropHighlighted && dropSurfaceMaterial != null)
                dropSurfaceMaterial.color = dropSurfaceColor;
            if (!HasValidIdentity)
                return;
            FindAnyObjectByType<CardArenaBootstrap>()?.HandleZoneHover(
                this,
                false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            FindAnyObjectByType<CardArenaBootstrap>()?.BeginMonsterAttackDrag(
                this,
                eventData.position,
                eventData.pointerId);
        }

        public void OnDrag(PointerEventData eventData)
        {
            FindAnyObjectByType<CardArenaBootstrap>()?.UpdateMonsterAttackDrag(
                eventData.position,
                eventData.pointerId);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            FindAnyObjectByType<CardArenaBootstrap>()?.EndMonsterAttackDrag(
                eventData.position,
                eventData.pointerId);
        }

        private static DuelZoneKind KindFromName(string objectName)
        {
            if (objectName.StartsWith("SpellTrapZone_"))
                return DuelZoneKind.SpellTrap;
            if (objectName == "FieldZone")
                return DuelZoneKind.Field;
            if (objectName == "MainDeck")
                return DuelZoneKind.MainDeck;
            if (objectName == "ExtraDeck")
                return DuelZoneKind.ExtraDeck;
            if (objectName == "Graveyard")
                return DuelZoneKind.Graveyard;
            if (objectName == "Banishment")
                return DuelZoneKind.Banishment;
            return DuelZoneKind.Monster;
        }

        private static int IndexFromName(string objectName)
        {
            int separator = objectName.LastIndexOf('_');
            return separator >= 0 &&
                   int.TryParse(
                       objectName.Substring(separator + 1),
                       out int oneBased)
                ? Mathf.Max(0, oneBased - 1)
                : 0;
        }
    }
}
