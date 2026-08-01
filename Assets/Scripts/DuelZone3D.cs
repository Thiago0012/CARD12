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
        [SerializeField] private DuelMonsterPosition monsterPosition =
            DuelMonsterPosition.FaceUpAttack;
        private Renderer dropSurface;
        private Color dropSurfaceColor;
        private bool dropHighlighted;
        private Color dropHighlightColor =
            new Color(0.08f, 0.58f, 1f, 1f);
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
            RefreshExtraMonsterZoneSurface();
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
            if (dropSurface == null)
            {
                Transform inset = transform.Find("Card Inset");
                dropSurface = inset != null
                    ? inset.GetComponent<Renderer>()
                    : GetComponent<Renderer>();
                if (dropSurface != null)
                    dropSurfaceColor = dropSurface.sharedMaterial.color;
            }
            if (dropSurface != null)
            {
                dropSurface.material.color = enabled
                    ? dropHighlightColor
                    : dropSurfaceColor;
            }
        }

        private void RefreshExtraMonsterZoneSurface()
        {
            if (Kind != DuelZoneKind.Monster || ZoneIndex < 5)
                return;
            bool visibleOrInteractive =
                dropHighlighted || !string.IsNullOrWhiteSpace(placedCardId);
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null)
                zoneCollider.enabled = visibleOrInteractive;
            if (!sharedVisualProxy)
                return;
            Transform proxySurface = transform.Find("Card Inset");
            if (proxySurface != null)
                proxySurface.gameObject.SetActive(visibleOrInteractive);
        }

        private void Update()
        {
            UpdateSpecialZoneOutline();
            if (dropSurface == null)
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
                dropSurface.material.color =
                    Color.Lerp(low, high, pulse);
                return;
            }
            dropSurface.material.color = pointerFocused
                ? Color.Lerp(
                    dropSurfaceColor,
                    new Color(0.10f, 0.34f, 0.38f, 1f),
                    0.56f)
                : dropSurfaceColor;
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
            var outlineObject = new GameObject("Contorno de ação legal");
            outlineObject.transform.SetParent(transform, false);
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
                specialZoneOutline.positionCount = 4;
                specialZoneOutline.SetPositions(new[]
                {
                    new Vector3(-1.12f, 0.77f, -1.43f),
                    new Vector3(-1.12f, 0.77f, 1.43f),
                    new Vector3(1.12f, 0.77f, 1.43f),
                    new Vector3(1.12f, 0.77f, -1.43f)
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
                eventData.clickCount);
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
            FindAnyObjectByType<CardArenaBootstrap>()?.HandleZoneHover(
                this,
                true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerFocused = false;
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
                eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            FindAnyObjectByType<CardArenaBootstrap>()?.UpdateMonsterAttackDrag(
                eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            FindAnyObjectByType<CardArenaBootstrap>()?.EndMonsterAttackDrag(
                eventData.position);
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
