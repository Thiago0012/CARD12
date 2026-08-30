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
        IPointerUpHandler,
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
        private bool selectionEmphasized;
        private Color selectionEmphasisColor =
            new Color(1f, 0.76f, 0.16f, 1f);
        private bool disabledByCore;
        private static readonly Color DisabledByCoreColor =
            new Color(0.34f, 0.055f, 0.07f, 1f);
        private LineRenderer specialZoneOutline;
        private Material specialZoneOutlineMaterial;
        private ParticleSystem placementDust;
        private Material placementDustMaterial;

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
        public bool IsSelectionEmphasized => selectionEmphasized;
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
            if (!enabled)
                selectionEmphasized = false;
            if (enabled)
            {
                // Destinos de invocação/baixar compartilham a leitura azul do
                // tabuleiro. Cores de efeitos continuam livres nas zonas
                // especiais, mas não transformam os slots principais em
                // círculos verdes ou vermelhos.
                dropHighlightColor = UsesPlacementPulse()
                    ? new Color(0.12f, 0.66f, 1f, 1f)
                    : color;
            }
            RefreshSpecialZoneOutline(enabled);
            RefreshExtraMonsterZoneSurface();
            if (enabled)
                EnsureDropSurfaceMaterial();
            if (dropSurfaceMaterial != null)
            {
                dropSurfaceMaterial.color = enabled
                    ? UsesPlacementPulse()
                        ? Color.Lerp(
                            dropSurfaceColor,
                            dropHighlightColor,
                            0.08f)
                        : dropHighlightColor
                    : disabledByCore
                        ? DisabledByCoreColor
                        : dropSurfaceColor;
            }
        }

        /// <summary>
        /// Distinguishes the exact field card selected inside a Core choice
        /// prompt from the other legal candidates. The regular blue outline
        /// remains the legal-action language; the selected card receives a
        /// stronger gold pulse without changing any duel state.
        /// </summary>
        public void SetSelectionEmphasis(bool enabled, Color color)
        {
            if (enabled && !dropHighlighted)
                SetDropHighlight(true);
            selectionEmphasized = enabled && UsesPlacementPulse();
            if (selectionEmphasized)
                selectionEmphasisColor = color;
            RefreshSpecialZoneOutline(dropHighlighted);
            ApplyPlacementDustPalette();
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
            if (!dropHighlighted && !disabledByCore)
                return;
            UpdateSpecialZoneOutline();
            if (dropSurfaceMaterial == null)
                return;
            if (dropHighlighted)
            {
                // Zonas principais usam o anel de energia; a pedra do campo
                // recebe apenas uma leve reflexão azul, sem o antigo bloco
                // opaco que escondia a posição real do slot.
                Color effectiveColor = EffectiveHighlightColor();
                dropSurfaceMaterial.color = UsesPlacementPulse()
                    ? Color.Lerp(
                        dropSurfaceColor,
                        effectiveColor,
                        selectionEmphasized ? 0.22f : 0.08f)
                    : Color.Lerp(
                        effectiveColor,
                        Color.white,
                        0.14f);
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
            if (!SupportsActionOutline())
            {
                return;
            }
            if (enabled)
                EnsureSpecialZoneOutline();
            if (specialZoneOutline != null)
                specialZoneOutline.enabled = enabled;
            if (placementDust != null)
            {
                ApplyPlacementDustPalette();
                if (enabled && UsesPlacementPulse())
                    placementDust.Play(true);
                else
                    placementDust.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private bool SupportsActionOutline()
        {
            return Kind == DuelZoneKind.Monster ||
                   Kind == DuelZoneKind.SpellTrap ||
                   Kind == DuelZoneKind.Field ||
                   Kind == DuelZoneKind.ExtraDeck ||
                   Kind == DuelZoneKind.Graveyard ||
                   Kind == DuelZoneKind.Banishment;
        }

        private bool UsesPlacementPulse()
        {
            return Kind == DuelZoneKind.Monster ||
                   Kind == DuelZoneKind.SpellTrap ||
                   Kind == DuelZoneKind.Field;
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
                float height = UsesPlacementPulse() ? 0.205f : 0.18f;
                if (UsesPlacementPulse())
                {
                    // O marcador segue o quadrado visual das cinco zonas da
                    // arena, como na referência. A linha é fechada pelo loop
                    // do LineRenderer e permanece centralizada no próprio slot.
                    const float halfExtent = 0.73f;
                    specialZoneOutline.positionCount = 4;
                    specialZoneOutline.SetPositions(new[]
                    {
                        new Vector3(-halfExtent, height, -halfExtent),
                        new Vector3(-halfExtent, height, halfExtent),
                        new Vector3(halfExtent, height, halfExtent),
                        new Vector3(halfExtent, height, -halfExtent)
                    });
                    EnsurePlacementDust(outlineParent);
                }
                else
                {
                    const int segments = 48;
                    const float specialWellRadius = 0.52f;
                    specialZoneOutline.positionCount = segments;
                    for (int index = 0; index < segments; index++)
                    {
                        float angle = index * Mathf.PI * 2f / segments;
                        specialZoneOutline.SetPosition(
                            index,
                            new Vector3(
                                Mathf.Cos(angle) * specialWellRadius,
                                height,
                                Mathf.Sin(angle) * specialWellRadius));
                    }
                }
            }
        }

        private void EnsurePlacementDust(Transform parent)
        {
            if (placementDust != null || parent == null)
                return;

            var dustObject = new GameObject("Poeira azul da zona legal");
            dustObject.transform.SetParent(parent, false);
            dustObject.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            dustObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            placementDust = dustObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = placementDust.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.82f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.075f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.68f, 1f, 0.25f),
                new Color(0.42f, 0.92f, 1f, 0.78f));
            main.maxParticles = 22;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = placementDust.emission;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = placementDust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.BoxEdge;
            shape.scale = new Vector3(1.46f, 1.46f, 0.02f);

            ParticleSystemRenderer dustRenderer =
                dustObject.GetComponent<ParticleSystemRenderer>();
            dustRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            dustRenderer.sortingOrder = 4;
            Shader shader = Shader.Find("Sprites/Default") ??
                            Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                placementDustMaterial = new Material(shader)
                {
                    name = "Material da poeira azul de invocação"
                };
                placementDustMaterial.color = Color.white;
                dustRenderer.material = placementDustMaterial;
            }
            placementDust.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyPlacementDustPalette();
        }

        private void UpdateSpecialZoneOutline()
        {
            if (specialZoneOutline == null || !dropHighlighted)
                return;
            Color effectiveColor = EffectiveHighlightColor();
            float pulseSpeed = selectionEmphasized ? 8.4f : 4.8f;
            float pulse =
                (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            Color color = new Color(
                effectiveColor.r,
                effectiveColor.g,
                effectiveColor.b,
                UsesPlacementPulse()
                    ? selectionEmphasized
                        ? Mathf.Lerp(0.72f, 1f, pulse)
                        : Mathf.Lerp(0.38f, 0.94f, pulse)
                    : 0.86f);
            specialZoneOutline.startColor = color;
            specialZoneOutline.endColor = color;
            specialZoneOutline.widthMultiplier = UsesPlacementPulse()
                ? selectionEmphasized
                    ? Mathf.Lerp(0.085f, 0.145f, pulse)
                    : Mathf.Lerp(0.035f, 0.075f, pulse)
                : 0.05f;
        }

        private Color EffectiveHighlightColor()
        {
            return selectionEmphasized
                ? selectionEmphasisColor
                : dropHighlightColor;
        }

        private void ApplyPlacementDustPalette()
        {
            if (placementDust == null)
                return;
            ParticleSystem.MainModule main = placementDust.main;
            ParticleSystem.EmissionModule emission = placementDust.emission;
            if (selectionEmphasized)
            {
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.56f, 0.08f, 0.48f),
                    new Color(1f, 0.95f, 0.50f, 1f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
                emission.rateOverTime = 24f;
            }
            else
            {
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.18f, 0.68f, 1f, 0.25f),
                    new Color(0.42f, 0.92f, 1f, 0.78f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
                emission.rateOverTime = 14f;
            }
        }

        private void OnDestroy()
        {
            CancelPendingAttackDrag();
            if (specialZoneOutlineMaterial != null)
                Destroy(specialZoneOutlineMaterial);
            if (placementDustMaterial != null)
                Destroy(placementDustMaterial);
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
                return;
            }
            FindAnyObjectByType<CardArenaBootstrap>()?.HandleZoneHover(
                this,
                true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
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

        public void OnPointerUp(PointerEventData eventData)
        {
            // Some Android devices can lose OnEndDrag when the finger leaves
            // the original 3D raycast target. PointerUp is an idempotent
            // fallback; EndMonsterAttackDrag ignores it when no drag is active.
            FindAnyObjectByType<CardArenaBootstrap>()?.EndMonsterAttackDrag(
                eventData.position,
                eventData.pointerId);
        }

        private void OnDisable()
        {
            CancelPendingAttackDrag();
            if (specialZoneOutline != null)
                specialZoneOutline.enabled = false;
            if (placementDust != null)
                placementDust.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void CancelPendingAttackDrag()
        {
            if (!Application.isPlaying)
                return;
            FindAnyObjectByType<CardArenaBootstrap>()
                ?.CancelMonsterAttackDrag(this);
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
