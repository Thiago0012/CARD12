using System.Collections;
using UnityEngine;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using ArcaneDuel.Game;

namespace ArcaneArena
{
    public sealed class MasterDuelArena3D : MonoBehaviour
    {
        public const int CurrentLayoutVersion = 16;
        public const float StaticFieldWidth = 18.8f;
        public const float StaticFieldDepth = 10.586f;
        private const float VisualCardThickness = 0.012f;
        private const float MinimumVisibleStackThickness = 0.035f;
        private const float MaximumVisibleStackThickness = 0.72f;
        private const float SourceFieldWidth = 1672f;
        private const float SourceFieldHeight = 941f;

        [SerializeField] private int layoutVersion;
        [SerializeField] private Texture2D cardBackTexture;
        [Header("Campo estatico por jogador")]
        [SerializeField] private bool useStaticPngField = true;
        [SerializeField] private Texture2D playerOneFieldTexture;
        [SerializeField] private Texture2D playerTwoFieldTexture;
        private Material _stone;
        private Material _darkStone;
        private Material _monsterGlow;
        private Material _spellGlow;
        private Material _gold;
        private Material _blueWell;
        private Material _violetWell;
        private Material _cardBack;
        private Material _extraBack;
        private Material _paperEdges;
        private Material _pageLines;
        private Material _playerOneField;
        private Material _playerTwoField;
        private Material _invisibleZone;
        private Texture2D _paperEdgeTexture;
        private Transform _playerOneMainDeck;
        private Transform _playerTwoMainDeck;
        private Transform _playerOneExtraDeck;
        private Transform _playerTwoExtraDeck;
        private int _playerOneMainDeckCount = int.MinValue;
        private int _playerTwoMainDeckCount = int.MinValue;
        private int _playerOneExtraDeckCount = int.MinValue;
        private int _playerTwoExtraDeckCount = int.MinValue;

        private void Awake()
        {
            if (transform.childCount == 0 || NeedsEditorRebuild)
                Rebuild();
            else
                RefreshRegistry();
        }

        private IEnumerator Start()
        {
            // DuelAuthoredZoneLayout applies the camera-authored positions two
            // frames after scene load. Repair and validate the four physical
            // special zones immediately afterwards so an older serialized
            // arena or a partial scene merge can never leave only one well.
            yield return null;
            yield return null;
            yield return null;
            EnsureSpecialZonePairs();
        }

        public void Rebuild()
        {
            var mainDeckSnapshot = TransformSnapshot.Capture(
                transform.Find("Players/PLAYER_1/SpecialZones/MainDeck"));
            var extraDeckSnapshot = TransformSnapshot.Capture(
                transform.Find("Players/PLAYER_1/SpecialZones/ExtraDeck"));
            var mainDeckStackSnapshot = TransformSnapshot.Capture(
                transform.Find("Players/PLAYER_1/SpecialZones/MainDeck/Card Stack"));
            var extraDeckStackSnapshot = TransformSnapshot.Capture(
                transform.Find("Players/PLAYER_1/SpecialZones/ExtraDeck/Card Stack"));

            layoutVersion = CurrentLayoutVersion;
            _playerOneMainDeckCount = int.MinValue;
            _playerTwoMainDeckCount = int.MinValue;
            _playerOneExtraDeckCount = int.MinValue;
            _playerTwoExtraDeckCount = int.MinValue;
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    child.SetActive(false);
                    child.transform.SetParent(null);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            CreateMaterials();
            CreateFoundation();
            var players = CreateGroup(transform, "Players");
            CreatePlayerHalf(players, false);
            CreatePlayerHalf(players, true);
            CreateBorder();
            CreateLighting();
            OptimizeStaticGeometry();
            if (!UseStaticField)
            {
                RestorePlayerDeckLayout(
                    mainDeckSnapshot,
                    extraDeckSnapshot,
                    mainDeckStackSnapshot,
                    extraDeckStackSnapshot);
            }
            RefreshRegistry();
        }

        public bool NeedsEditorRebuild => layoutVersion != CurrentLayoutVersion;

        public void SetCardBackTexture(Texture2D texture)
        {
            cardBackTexture = texture;
        }

        public void ConfigureStaticField(
            Texture2D playerOneTexture,
            Texture2D playerTwoTexture = null)
        {
            useStaticPngField = true;
            playerOneFieldTexture = playerOneTexture;
            playerTwoFieldTexture = playerTwoTexture != null
                ? playerTwoTexture
                : playerOneTexture;
        }

        private void CreateFoundation()
        {
            if (UseStaticField)
            {
                CreateStaticFieldFoundation();
                return;
            }

            var environment = CreateGroup(transform, "Environment");
            var foundation = CreateGroup(environment, "Foundation");
            var joints = CreateGroup(environment, "Floor Joints");
            CreateBlock(foundation, "Base de Pedra", new Vector3(0, -0.42f, 0), new Vector3(18.8f, 0.8f, 16.2f), _darkStone);
            CreateBlock(foundation, "Piso da Arena", new Vector3(0, 0f, 0), new Vector3(16.6f, 0.16f, 14.8f), _stone);
            CreateBlock(foundation, "Divisor Central", new Vector3(0, 0.13f, 0), new Vector3(14.2f, 0.13f, 0.16f), _gold);

            for (var z = -6.6f; z <= 6.6f; z += 2.2f)
            {
                CreateBlock(joints, "Junta Horizontal", new Vector3(0, 0.095f, z),
                    new Vector3(14.6f, 0.025f, 0.055f), _darkStone);
            }

            for (var x = -5.75f; x <= 5.75f; x += 2.3f)
            {
                CreateBlock(joints, "Junta Vertical", new Vector3(x, 0.096f, 0),
                    new Vector3(0.05f, 0.026f, 14f), _darkStone);
            }
        }

        private bool UseStaticField =>
            useStaticPngField && playerOneFieldTexture != null;

        private void CreateStaticFieldFoundation()
        {
            Transform environment = CreateGroup(transform, "Campo PNG Estatico");
            CreateFieldHalf(
                environment,
                "Campo do Jogador 1",
                _playerOneField,
                false);
            CreateFieldHalf(
                environment,
                "Campo do Jogador 2",
                _playerTwoField != null ? _playerTwoField : _playerOneField,
                true);
        }

        private static void CreateFieldHalf(
            Transform parent,
            string name,
            Material material,
            bool upperHalf)
        {
            var fieldObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fieldObject.name = name;
            fieldObject.isStatic = true;
            fieldObject.transform.SetParent(parent, false);
            float halfDepth = StaticFieldDepth * 0.5f;
            fieldObject.transform.localPosition = new Vector3(
                0f,
                -0.01f,
                upperHalf ? halfDepth * 0.5f : -halfDepth * 0.5f);
            fieldObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            fieldObject.transform.localScale = new Vector3(
                StaticFieldWidth,
                halfDepth,
                1f);
            fieldObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (material != null)
            {
                var scale = new Vector2(1f, 0.5f);
                var offset = new Vector2(0f, upperHalf ? 0.5f : 0f);
                material.mainTextureScale = scale;
                material.mainTextureOffset = offset;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTextureScale("_BaseMap", scale);
                    material.SetTextureOffset("_BaseMap", offset);
                }
            }
            RemoveCollider(fieldObject);
        }

        private static Vector3 FieldPixel(
            float pixelX,
            float pixelY,
            float elevation)
        {
            return new Vector3(
                (pixelX / SourceFieldWidth - 0.5f) * StaticFieldWidth,
                elevation,
                (0.5f - pixelY / SourceFieldHeight) * StaticFieldDepth);
        }

        private void CreatePlayerHalf(Transform players, bool opponent)
        {
            if (UseStaticField)
            {
                CreateStaticPlayerHalf(players, opponent);
                return;
            }

            var sideName = opponent ? "PLAYER_2" : "PLAYER_1";
            var owner = opponent ? DuelPlayerSide.PlayerTwo : DuelPlayerSide.PlayerOne;
            var side = new GameObject(sideName);
            side.transform.SetParent(players, false);

            var sign = opponent ? 1f : -1f;
            var horizontalSign = opponent ? -1f : 1f;
            var rotation = opponent ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            const float spacing = 2.3f;

            var monsterGroup = CreateGroup(side.transform, "MonsterZones");
            var spellGroup = CreateGroup(side.transform, "SpellTrapZones");
            for (var i = 0; i < 5; i++)
            {
                var x = (i - 2) * spacing * horizontalSign;
                CreateCardZone(monsterGroup, $"MonsterZone_{i + 1}", new Vector3(x, 0.2f, sign * 2.1f),
                    rotation, _monsterGlow, owner, DuelZoneKind.Monster, i, !opponent);
                CreateCardZone(spellGroup, $"SpellTrapZone_{i + 1}", new Vector3(x, 0.19f, sign * 4.45f),
                    rotation, _spellGlow, owner, DuelZoneKind.SpellTrap, i, false);
            }

            var extraMonsterGroup =
                CreateGroup(side.transform, "ExtraMonsterZones");
            for (var i = 0; i < 2; i++)
            {
                float direction = opponent ? 1f : -1f;
                float x = direction * (i == 0 ? spacing : -spacing);
                CreateExtraMonsterZone(
                    extraMonsterGroup,
                    $"MonsterZone_{i + 6}",
                    new Vector3(x, 0.21f, 0f),
                    rotation,
                    owner,
                    i + 5,
                    opponent);
            }

            var specials = CreateGroup(side.transform, "SpecialZones");
            float rightSide = 7.05f * horizontalSign;
            float leftSide = -rightSide;
            var extraDeck = CreateDeckPedestal(specials, "ExtraDeck", new Vector3(leftSide, 0.18f, sign * 5.75f),
                rotation, _extraBack, false, owner, DuelZoneKind.ExtraDeck);
            var mainDeck = CreateDeckPedestal(specials, "MainDeck", new Vector3(rightSide, 0.18f, sign * 5.75f),
                rotation, _cardBack, true, owner, DuelZoneKind.MainDeck);
            if (!opponent)
            {
                _playerOneMainDeck = mainDeck;
                _playerOneExtraDeck = extraDeck;
            }
            else
            {
                _playerTwoMainDeck = mainDeck;
                _playerTwoExtraDeck = extraDeck;
            }
            CreateWell(specials, "Graveyard", new Vector3(rightSide, 0.16f, sign * 3.2f),
                _blueWell, owner, DuelZoneKind.Graveyard);
            CreateWell(specials, "Banishment", new Vector3(rightSide, 0.16f, sign * 1.8f),
                _violetWell, owner, DuelZoneKind.Banishment);
            CreateCardZone(
                specials,
                "FieldZone",
                new Vector3(leftSide, 0.19f, sign * 3.1f),
                rotation,
                _spellGlow,
                owner,
                DuelZoneKind.Field,
                0,
                false);

            if (opponent)
                CreateOpponentHand(side.transform);
        }

        private void CreateStaticPlayerHalf(Transform players, bool opponent)
        {
            var sideName = opponent ? "PLAYER_2" : "PLAYER_1";
            var owner = opponent ? DuelPlayerSide.PlayerTwo : DuelPlayerSide.PlayerOne;
            var side = new GameObject(sideName);
            side.transform.SetParent(players, false);

            var rotation = opponent ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            float[] zoneColumns = opponent
                ? new[] { 1128f, 986f, 844f, 702f, 560f }
                : new[] { 523f, 665f, 807f, 949f, 1091f };
            float monsterRow = opponent ? 284f : 595f;
            float spellTrapRow = opponent ? 165f : 749f;

            var monsterGroup = CreateGroup(side.transform, "MonsterZones");
            var spellGroup = CreateGroup(side.transform, "SpellTrapZones");
            for (var i = 0; i < 5; i++)
            {
                CreateCardZone(monsterGroup, $"MonsterZone_{i + 1}",
                    FieldPixel(zoneColumns[i], monsterRow, 0.12f),
                    rotation, _monsterGlow, owner, DuelZoneKind.Monster, i, !opponent);
                CreateCardZone(spellGroup, $"SpellTrapZone_{i + 1}",
                    FieldPixel(zoneColumns[i], spellTrapRow, 0.11f),
                    rotation, _spellGlow, owner, DuelZoneKind.SpellTrap, i, false);
            }

            var extraMonsterGroup =
                CreateGroup(side.transform, "ExtraMonsterZones");
            float[] extraMonsterColumns = opponent
                ? new[] { 989f, 687f }
                : new[] { 687f, 989f };
            for (var i = 0; i < 2; i++)
            {
                CreateExtraMonsterZone(
                    extraMonsterGroup,
                    $"MonsterZone_{i + 6}",
                    FieldPixel(extraMonsterColumns[i], 437f, 0.13f),
                    rotation,
                    owner,
                    i + 5,
                    opponent);
            }

            var specials = CreateGroup(side.transform, "SpecialZones");
            Vector3 extraDeckPosition = opponent
                ? FieldPixel(1295f, 63f, 0.12f)
                : FieldPixel(310f, 850f, 0.12f);
            Vector3 mainDeckPosition = opponent
                ? FieldPixel(393f, 70f, 0.12f)
                : FieldPixel(1374f, 845f, 0.12f);
            Vector3 fieldZonePosition = opponent
                ? FieldPixel(1255f, 282f, 0.11f)
                : FieldPixel(385f, 604f, 0.11f);
            // These two fixtures form one compact pair on the local player's
            // right. The opponent pair is derived by an exact 180-degree
            // rotation, so both sides always keep the same spacing and remain
            // visually aligned even when the source field art changes.
            Vector3 localGraveyardPosition =
                FieldPixel(1307f, 646f, 0.11f);
            Vector3 localBanishmentPosition =
                FieldPixel(1307f, 530f, 0.11f);
            Vector3 graveyardPosition = opponent
                ? RotateFieldPosition180(localGraveyardPosition)
                : localGraveyardPosition;
            Vector3 banishmentPosition = opponent
                ? RotateFieldPosition180(localBanishmentPosition)
                : localBanishmentPosition;

            var extraDeck = CreateDeckPedestal(specials, "ExtraDeck", extraDeckPosition,
                rotation, _extraBack, false, owner, DuelZoneKind.ExtraDeck);
            var mainDeck = CreateDeckPedestal(specials, "MainDeck", mainDeckPosition,
                rotation, _cardBack, true, owner, DuelZoneKind.MainDeck);
            if (!opponent)
            {
                _playerOneMainDeck = mainDeck;
                _playerOneExtraDeck = extraDeck;
            }
            else
            {
                _playerTwoMainDeck = mainDeck;
                _playerTwoExtraDeck = extraDeck;
            }
            CreateWell(specials, "Graveyard", graveyardPosition,
                _blueWell, owner, DuelZoneKind.Graveyard);
            CreateWell(specials, "Banishment", banishmentPosition,
                _violetWell, owner, DuelZoneKind.Banishment);
            CreateCardZone(
                specials,
                "FieldZone",
                fieldZonePosition,
                rotation,
                _spellGlow,
                owner,
                DuelZoneKind.Field,
                0,
                false);

            // No campo estatico, a mao do oponente e apresentada pelo HUD autorado
            // da cena (DuelHandLayoutAnchor). Manter esta copia 3D legada criaria
            // uma segunda mao com posicoes fixas e sobrescreveria visualmente o
            // layout ajustado pelo artista. O estado e as regras da mao continuam
            // pertencendo ao Core; somente a representacao duplicada e omitida.
        }

        private void CreateCardZone(Transform parent, string name, Vector3 position, Quaternion rotation,
            Material material, DuelPlayerSide owner, DuelZoneKind kind, int zoneIndex, bool interactive)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;
            root.AddComponent<BoxCollider>().size = UseStaticField
                ? new Vector3(1.58f, 0.45f, 1.86f)
                : new Vector3(2f, 0.45f, 2.55f);
            root.AddComponent<DuelZone3D>().Setup(owner, kind, zoneIndex, interactive);

            if (UseStaticField)
            {
                CreateBlock(
                    root.transform,
                    "Card Inset",
                    new Vector3(0f, 0.025f, 0f),
                    new Vector3(1.46f, 0.025f, 1.78f),
                    _invisibleZone,
                    false);
                return;
            }

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Octagonal Pedestal";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = Vector3.zero;
            pedestal.transform.localScale = new Vector3(0.91f, 0.07f, 1.25f);
            pedestal.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(pedestal);

            var inset = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inset.name = "Card Inset";
            inset.transform.SetParent(root.transform, false);
            inset.transform.localPosition = new Vector3(0, 0.085f, 0);
            inset.transform.localScale = new Vector3(1.43f, 0.035f, 1.96f);
            inset.GetComponent<Renderer>().sharedMaterial = _darkStone;
            RemoveCollider(inset);
        }

        private void CreateExtraMonsterZone(
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            DuelPlayerSide owner,
            int zoneIndex,
            bool sharedVisualProxy)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;
            root.AddComponent<BoxCollider>().size = UseStaticField
                ? new Vector3(1.58f, 0.45f, 1.86f)
                : new Vector3(2f, 0.45f, 2.55f);
            DuelZone3D zone = root.AddComponent<DuelZone3D>();
            zone.Setup(
                owner,
                DuelZoneKind.Monster,
                zoneIndex,
                owner == DuelPlayerSide.PlayerOne,
                sharedVisualProxy);

            if (!sharedVisualProxy && !UseStaticField)
            {
                var pedestal =
                    GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pedestal.name = "Octagonal Pedestal";
                pedestal.transform.SetParent(root.transform, false);
                pedestal.transform.localScale =
                    new Vector3(0.91f, 0.07f, 1.25f);
                pedestal.GetComponent<Renderer>().sharedMaterial =
                    _monsterGlow;
                RemoveCollider(pedestal);
            }

            var inset = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inset.name = "Card Inset";
            inset.transform.SetParent(root.transform, false);
            inset.transform.localPosition = new Vector3(
                0f,
                UseStaticField ? 0.025f : sharedVisualProxy ? 0.089f : 0.085f,
                0f);
            inset.transform.localScale = UseStaticField
                ? new Vector3(1.46f, 0.025f, 1.78f)
                : new Vector3(1.43f, 0.035f, 1.96f);
            inset.GetComponent<Renderer>().sharedMaterial = UseStaticField
                ? _invisibleZone
                : _darkStone;
            RemoveCollider(inset);
            zone.ClearPlacedCard();
        }

        private Transform CreateDeckPedestal(Transform parent, string name, Vector3 position, Quaternion rotation,
            Material backMaterial, bool mainDeck, DuelPlayerSide owner, DuelZoneKind kind)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;
            root.AddComponent<DuelZone3D>().Setup(owner, kind, 0, false);
            if (UseStaticField)
            {
                root.AddComponent<BoxCollider>().size =
                    new Vector3(1.58f, 0.55f, 1.86f);
            }

            if (!UseStaticField)
            {
                CreateBlock(root.transform, "Pedestal", Vector3.zero, new Vector3(2.05f, 0.32f, 2.65f), _darkStone);
                CreateBlock(root.transform, "Gold Trim", new Vector3(0, 0.2f, 0), new Vector3(1.82f, 0.08f, 2.38f), _gold);
            }
            var stack = new GameObject("Card Stack");
            stack.transform.SetParent(root.transform, false);
            // The static field markings are intentionally more compact than the
            // old 3D pedestals. Keep the visual pile inside its printed zone;
            // the root collider and stable zone identity remain unchanged.
            stack.transform.localScale = UseStaticField
                ? new Vector3(0.84f, 1f, 0.84f)
                : Vector3.one;
            var initialCount = mainDeck ? 40 : 15;
            var paperHeight = DeckThicknessForCardCount(initialCount);
            var stackBaseY = UseStaticField ? 0.03f : 0.25f;
            stack.transform.localPosition = new Vector3(
                0f,
                stackBaseY + paperHeight * 0.5f,
                0f);
            CreateBlock(stack.transform, "Paper Edges", Vector3.zero,
                new Vector3(1.60f, paperHeight, 2.12f), _paperEdges, false);

            for (var i = 1; i <= 3; i++)
            {
                var layerY = -paperHeight * 0.5f + paperHeight * i / 4f;
                CreateBlock(stack.transform, $"Page Line {i}", new Vector3(0, layerY, 0),
                    new Vector3(1.605f, 0.008f, 2.125f), _pageLines, false);
            }

            CreateBlock(stack.transform, "Top Card Back",
                new Vector3(0, paperHeight * 0.5f + 0.018f, 0),
                new Vector3(1.43f, 0.026f, 1.95f), backMaterial, false);
            return root.transform;
        }

        public Vector3 PlayerMainDeckWorldPosition =>
            GetMainDeckWorldPosition(DuelPlayerSide.PlayerOne);

        public void NotifyCardDrawn()
        {
            NotifyCardDrawn(DuelPlayerSide.PlayerOne);
        }

        public Vector3 GetMainDeckWorldPosition(DuelPlayerSide side)
        {
            var deck = GetMainDeckTransform(side);
            if (deck != null)
            {
                Transform topCard = deck.Find("Card Stack/Top Card Back");
                if (topCard != null && topCard.gameObject.activeInHierarchy)
                    return topCard.position + topCard.up * 0.03f;
                return deck.position + deck.up * 0.08f;
            }

            return side == DuelPlayerSide.PlayerOne
                ? FieldPixel(1374f, 845f, 0.8f)
                : FieldPixel(393f, 70f, 0.8f);
        }

        public Transform GetMainDeckTransform(DuelPlayerSide side)
        {
            return side == DuelPlayerSide.PlayerOne
                ? _playerOneMainDeck
                : _playerTwoMainDeck;
        }

        public Transform GetExtraDeckTransform(DuelPlayerSide side)
        {
            return side == DuelPlayerSide.PlayerOne
                ? _playerOneExtraDeck
                : _playerTwoExtraDeck;
        }

        public static float DeckThicknessForCardCount(int cardCount)
        {
            if (cardCount <= 0)
                return 0f;
            return Mathf.Clamp(
                cardCount * VisualCardThickness,
                MinimumVisibleStackThickness,
                MaximumVisibleStackThickness);
        }

        public void SetDeckCardCounts(
            DuelPlayerSide side,
            int mainDeckCount,
            int extraDeckCount)
        {
            if (side == DuelPlayerSide.PlayerOne)
            {
                ApplyDeckStackCountIfChanged(
                    GetMainDeckTransform(side),
                    mainDeckCount,
                    ref _playerOneMainDeckCount);
                ApplyDeckStackCountIfChanged(
                    GetExtraDeckTransform(side),
                    extraDeckCount,
                    ref _playerOneExtraDeckCount);
                return;
            }

            ApplyDeckStackCountIfChanged(
                GetMainDeckTransform(side),
                mainDeckCount,
                ref _playerTwoMainDeckCount);
            ApplyDeckStackCountIfChanged(
                GetExtraDeckTransform(side),
                extraDeckCount,
                ref _playerTwoExtraDeckCount);
        }

        private void ApplyDeckStackCountIfChanged(
            Transform deck,
            int cardCount,
            ref int displayedCount)
        {
            cardCount = Mathf.Max(0, cardCount);
            Transform stack = deck != null ? deck.Find("Card Stack") : null;
            if (stack == null)
            {
                // A replica can receive its first state before the authored
                // arena has finished rebuilding its zone registry. Do not
                // cache that state as presented until there is a real stack
                // to which it can be applied.
                displayedCount = int.MinValue;
                return;
            }

            bool expectedVisible = cardCount > 0;
            bool visibilityMatches =
                stack.gameObject.activeSelf == expectedVisible;
            if (displayedCount == cardCount && visibilityMatches)
                return;
            displayedCount = cardCount;
            ApplyDeckStackCount(deck, cardCount);
        }

        private void ApplyDeckStackCount(Transform deck, int cardCount)
        {
            Transform stack = deck != null ? deck.Find("Card Stack") : null;
            if (stack == null)
                return;

            bool hasCards = cardCount > 0;
            stack.gameObject.SetActive(hasCards);
            if (!hasCards)
                return;

            float thickness = DeckThicknessForCardCount(cardCount);
            float stackBaseY = UseStaticField ? 0.03f : 0.25f;
            stack.localPosition = new Vector3(
                stack.localPosition.x,
                stackBaseY + thickness * 0.5f,
                stack.localPosition.z);

            Transform paper = stack.Find("Paper Edges");
            Transform topCard = stack.Find("Top Card Back");
            if (paper != null)
            {
                Vector3 scale = paper.localScale;
                // A narrow exposed lip keeps the layered paper readable even
                // from the arena's mostly top-down camera.
                scale.x = 1.60f;
                scale.y = thickness;
                scale.z = 2.12f;
                paper.localScale = scale;
                paper.localPosition = Vector3.zero;
                ApplyPaperEdgeTexture(paper);
            }

            for (int index = 1; index <= 3; index++)
            {
                Transform pageLine = stack.Find($"Page Line {index}");
                if (pageLine == null)
                    continue;
                pageLine.gameObject.SetActive(cardCount >= index * 3);
                pageLine.localPosition = new Vector3(
                    pageLine.localPosition.x,
                    -thickness * 0.5f + thickness * index / 4f,
                    pageLine.localPosition.z);
                pageLine.localScale = new Vector3(
                    1.605f,
                    0.008f,
                    2.125f);
            }

            if (topCard != null)
            {
                topCard.localPosition = new Vector3(
                    topCard.localPosition.x,
                    thickness * 0.5f + 0.018f,
                    topCard.localPosition.z);
            }
        }

        private void ApplyPaperEdgeTexture(Transform paper)
        {
            Renderer renderer = paper != null
                ? paper.GetComponent<Renderer>()
                : null;
            Material material = renderer?.sharedMaterial;
            if (material == null)
                return;
            _paperEdgeTexture ??= GeneratePaperEdgeTexture();
            material.mainTexture = _paperEdgeTexture;
            material.mainTextureScale = new Vector2(2f, 7f);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", _paperEdgeTexture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
        }

        public Vector3 GetDrawPresentationWorldPosition(
            DuelPlayerSide side)
        {
            Vector3 fallback = transform.TransformPoint(
                new Vector3(0f, 1.28f, 0f));
            Camera camera = Camera.main;
            if (camera == null)
                return fallback;
            float depth = Vector3.Dot(
                fallback - camera.transform.position,
                camera.transform.forward);
            return camera.ViewportToWorldPoint(
                new Vector3(0.5f, 0.5f, Mathf.Max(2f, depth)));
        }

        public void NotifyCardDrawn(DuelPlayerSide side)
        {
            var deck = GetMainDeckTransform(side);
            if (deck == null)
                return;

            // The Core snapshot already contains the post-draw count before
            // this presentation callback. Reapply it without subtracting a
            // second visual card from the pile.
            int count = side == DuelPlayerSide.PlayerOne
                ? _playerOneMainDeckCount
                : _playerTwoMainDeckCount;
            if (count != int.MinValue)
                ApplyDeckStackCount(deck, count);
        }

        private DuelZone3D CreateWell(Transform parent, string name, Vector3 position, Material innerMaterial,
            DuelPlayerSide owner, DuelZoneKind kind)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = owner == DuelPlayerSide.PlayerTwo
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
            DuelZone3D zone = root.AddComponent<DuelZone3D>();
            zone.Setup(owner, kind, 0, false);

            BuildWellPresentation(zone, innerMaterial, kind);
            return zone;
        }

        private void BuildWellPresentation(
            DuelZone3D zone,
            Material innerMaterial,
            DuelZoneKind kind)
        {
            if (zone == null)
                return;

            Transform root = zone.transform;
            var interaction = root.GetComponent<BoxCollider>();
            if (interaction == null)
                interaction = root.gameObject.AddComponent<BoxCollider>();
            interaction.enabled = true;
            interaction.center = new Vector3(0f, 0.12f, 0f);
            interaction.size = new Vector3(1.48f, 0.55f, 1.48f);

            // Remove the former static-field placeholder. It was intentionally
            // invisible and is the reason an upgraded scene could retain a
            // clickable zone without showing its physical fixture.
            Transform legacyInset = root.Find("Card Inset");
            if (legacyInset != null)
            {
                legacyInset.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(legacyInset.gameObject);
                else
                    DestroyImmediate(legacyInset.gameObject);
            }

            // The card is intentionally recessed inside the fixture instead
            // of being laid directly on the field texture. This keeps the top
            // public card visible while preserving the impression that the
            // remaining pile is stored below the battlefield.
            zone.CardPresentationAnchor.localPosition =
                new Vector3(0f, 0.145f, 0f);

            CreateWellCylinder(
                root,
                "Base de Pedra",
                new Vector3(0f, 0.018f, 0f),
                new Vector3(0.72f, 0.075f, 0.72f),
                _darkStone);
            CreateWellCylinder(
                root,
                "Aro Esculpido",
                new Vector3(0f, 0.076f, 0f),
                new Vector3(0.63f, 0.035f, 0.63f),
                UseStaticField ? _stone : _gold);
            CreateWellCylinder(
                root,
                "Canal de Energia",
                new Vector3(0f, 0.104f, 0f),
                new Vector3(0.54f, 0.018f, 0.54f),
                innerMaterial);

            Material coreMaterial = kind == DuelZoneKind.Banishment
                ? _darkStone
                : innerMaterial;
            Renderer core = CreateWellCylinder(
                root,
                kind == DuelZoneKind.Banishment
                    ? "Abismo do Banimento"
                    : "Nucleo do Cemiterio",
                new Vector3(0f, 0.098f, 0f),
                new Vector3(0.46f, 0.012f, 0.46f),
                coreMaterial);

            var accents = new Renderer[8];
            for (int index = 0; index < accents.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / accents.Length;
                Vector3 runePosition = new Vector3(
                    Mathf.Cos(angle) * 0.305f,
                    0.126f,
                    Mathf.Sin(angle) * 0.305f);
                GameObject rune = CreateBlock(
                    root,
                    $"Runa {index + 1}",
                    runePosition,
                    new Vector3(0.105f, 0.025f, 0.038f),
                    innerMaterial,
                    false);
                rune.transform.localRotation = Quaternion.Euler(
                    0f,
                    -index * 45f,
                    0f);
                accents[index] = rune.GetComponent<Renderer>();
            }

            if (kind == DuelZoneKind.Banishment)
            {
                Renderer vortex = CreateWellCylinder(
                    root,
                    "Vortice Banido",
                    new Vector3(0f, 0.108f, 0f),
                    new Vector3(0.25f, 0.009f, 0.25f),
                    innerMaterial);
                core = vortex;
            }

            var visual = root.GetComponent<DuelSpecialZoneWellVisual>();
            if (visual == null)
                visual = root.gameObject.AddComponent<DuelSpecialZoneWellVisual>();
            visual.Configure(kind, core, accents, innerMaterial.color);
        }

        /// <summary>
        /// Guarantees one visible and interactive Graveyard/Banishment pair
        /// for each player. Safe to call repeatedly after scene upgrades.
        /// </summary>
        public void EnsureSpecialZonePairs()
        {
            EnsureWellMaterials();

            var layout = FindFirstObjectByType<DuelAuthoredZoneLayout>(
                FindObjectsInactive.Include);
            EnsureSpecialZonePair(
                DuelPlayerSide.PlayerOne,
                FieldPixel(1307f, 646f, 0.11f),
                FieldPixel(1307f, 530f, 0.11f),
                layout);
            EnsureSpecialZonePair(
                DuelPlayerSide.PlayerTwo,
                RotateFieldPosition180(FieldPixel(1307f, 646f, 0.11f)),
                RotateFieldPosition180(FieldPixel(1307f, 530f, 0.11f)),
                layout);
            RefreshRegistry();
        }

        private void EnsureSpecialZonePair(
            DuelPlayerSide owner,
            Vector3 graveyardPosition,
            Vector3 banishmentPosition,
            DuelAuthoredZoneLayout layout)
        {
            string playerName = owner == DuelPlayerSide.PlayerOne
                ? "PLAYER_1"
                : "PLAYER_2";
            Transform player = transform.Find("Players/" + playerName);
            if (player == null)
                return;
            Transform specials = player.Find("SpecialZones");
            if (specials == null)
                specials = CreateGroup(player, "SpecialZones");

            DuelZone3D graveyard = EnsureSpecialZone(
                specials,
                "Graveyard",
                owner,
                DuelZoneKind.Graveyard,
                graveyardPosition,
                _blueWell);
            DuelZone3D banishment = EnsureSpecialZone(
                specials,
                "Banishment",
                owner,
                DuelZoneKind.Banishment,
                banishmentPosition,
                _violetWell);

            ApplyAndShowSpecialZone(graveyard, layout);
            ApplyAndShowSpecialZone(banishment, layout);
        }

        private DuelZone3D EnsureSpecialZone(
            Transform parent,
            string name,
            DuelPlayerSide owner,
            DuelZoneKind kind,
            Vector3 fallbackPosition,
            Material material)
        {
            DuelZone3D result = null;
            DuelZone3D[] candidates =
                parent.GetComponentsInChildren<DuelZone3D>(true);
            for (int index = 0; index < candidates.Length; index++)
            {
                DuelZone3D candidate = candidates[index];
                if (candidate.Owner == owner && candidate.Kind == kind)
                {
                    result = candidate;
                    break;
                }
            }

            if (result == null)
            {
                result = CreateWell(
                    parent,
                    name,
                    fallbackPosition,
                    material,
                    owner,
                    kind);
            }
            else
            {
                result.gameObject.name = name;
                result.Setup(owner, kind, 0, false);
                if (result.GetComponent<DuelSpecialZoneWellVisual>() == null ||
                    result.transform.Find("Base de Pedra") == null)
                {
                    BuildWellPresentation(result, material, kind);
                }
            }

            return result;
        }

        private static void ApplyAndShowSpecialZone(
            DuelZone3D zone,
            DuelAuthoredZoneLayout layout)
        {
            if (zone == null)
                return;

            zone.gameObject.SetActive(true);
            BoxCollider collider = zone.GetComponent<BoxCollider>();
            if (collider != null)
                collider.enabled = true;
            Renderer[] renderers =
                zone.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                    continue;
                renderer.gameObject.SetActive(true);
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
            }

            // ApplyOne receives the exact active instance under this arena;
            // it cannot accidentally select a detached legacy duplicate.
            layout?.ApplyOne(zone);
        }

        private void EnsureWellMaterials()
        {
            if (_darkStone != null && _stone != null &&
                _blueWell != null && _violetWell != null)
            {
                return;
            }
            CreateMaterials();
        }

        private static Renderer CreateWellCylinder(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            RemoveCollider(item);
            return renderer;
        }

        private static Vector3 RotateFieldPosition180(Vector3 position)
        {
            return new Vector3(-position.x, position.y, -position.z);
        }

        private void CreateOpponentHand(Transform side)
        {
            var hand = CreateGroup(side, "OpponentHandPreview");
            for (var i = 0; i < 5; i++)
            {
                var card = CreateBlock(hand, $"HiddenCard_{i + 1}",
                    new Vector3((i - 2) * 1.02f, 0.42f,
                        UseStaticField ? 4.62f : 7.15f),
                    new Vector3(0.92f, 0.055f, 1.32f), _cardBack);
                card.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }
        }

        private void RefreshRegistry()
        {
            var registry = GetComponent<DuelFieldRegistry>();
            if (registry == null)
                registry = gameObject.AddComponent<DuelFieldRegistry>();
            registry.RebuildIndex();

            _playerOneMainDeck = ResolveZoneTransform(registry, "p1.main_deck.0");
            _playerTwoMainDeck = ResolveZoneTransform(registry, "p2.main_deck.0");
            _playerOneExtraDeck = ResolveZoneTransform(registry, "p1.extra_deck.0");
            _playerTwoExtraDeck = ResolveZoneTransform(registry, "p2.extra_deck.0");
        }

        private void RestorePlayerDeckLayout(
            TransformSnapshot mainDeckSnapshot,
            TransformSnapshot extraDeckSnapshot,
            TransformSnapshot mainDeckStackSnapshot,
            TransformSnapshot extraDeckStackSnapshot)
        {
            mainDeckSnapshot.ApplyTo(_playerOneMainDeck);
            extraDeckSnapshot.ApplyTo(_playerOneExtraDeck);
            mainDeckStackSnapshot.ApplyTo(
                _playerOneMainDeck != null ? _playerOneMainDeck.Find("Card Stack") : null);
            extraDeckStackSnapshot.ApplyTo(
                _playerOneExtraDeck != null ? _playerOneExtraDeck.Find("Card Stack") : null);
            MirrorPlayerDeck(_playerOneMainDeck, _playerTwoMainDeck);
            MirrorPlayerDeck(_playerOneExtraDeck, _playerTwoExtraDeck);
        }

        private static Transform ResolveZoneTransform(DuelFieldRegistry registry, string stableId)
        {
            return registry.TryGetZone(stableId, out var zone) && zone != null
                ? zone.transform
                : null;
        }

        private static void MirrorPlayerDeck(Transform source, Transform target)
        {
            if (source == null || target == null)
                return;

            var position = source.localPosition;
            target.localPosition = new Vector3(-position.x, position.y, -position.z);
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

        private readonly struct TransformSnapshot
        {
            private readonly bool _isValid;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _scale;

            private TransformSnapshot(Transform source)
            {
                _isValid = source != null;
                _position = source != null ? source.localPosition : Vector3.zero;
                _rotation = source != null ? source.localRotation : Quaternion.identity;
                _scale = source != null ? source.localScale : Vector3.one;
            }

            public static TransformSnapshot Capture(Transform source)
            {
                return new TransformSnapshot(source);
            }

            public void ApplyTo(Transform target)
            {
                if (!_isValid || target == null)
                    return;

                target.localPosition = _position;
                target.localRotation = _rotation;
                target.localScale = _scale;
            }
        }

        private void CreateBorder()
        {
            if (UseStaticField)
                return;

            var border = CreateGroup(transform, "Arena Border");
            var horizontal = CreateGroup(border, "Horizontal Stones");
            var vertical = CreateGroup(border, "Vertical Stones");
            for (var x = -8.4f; x <= 8.4f; x += 1.2f)
            {
                CreateBlock(horizontal, "Pedra da Borda", new Vector3(x, 0.08f, -7.75f),
                    new Vector3(1.08f, 0.45f, 0.65f), _stone);
                CreateBlock(horizontal, "Pedra da Borda", new Vector3(x, 0.08f, 7.75f),
                    new Vector3(1.08f, 0.45f, 0.65f), _stone);
            }

            for (var z = -6.8f; z <= 6.8f; z += 1.2f)
            {
                CreateBlock(vertical, "Pedra Lateral", new Vector3(-9.05f, 0.05f, z),
                    new Vector3(0.65f, 0.5f, 1.05f), _stone);
                CreateBlock(vertical, "Pedra Lateral", new Vector3(9.05f, 0.05f, z),
                    new Vector3(0.65f, 0.5f, 1.05f), _stone);
            }
        }

        private void CreateLighting()
        {
            var lighting = CreateGroup(transform, "Lighting");
            var sun = new GameObject("Arena Sun", typeof(Light));
            sun.transform.SetParent(lighting, false);
            sun.transform.localRotation = Quaternion.Euler(48f, -28f, 0);
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.92f, 0.75f);
            light.shadows = ArcaneGraphicsPreferences.ReduceArenaLighting
                ? LightShadows.None
                : LightShadows.Soft;

            if (UseStaticField)
                return;

            CreatePointLight(lighting, "Blue Graveyard Light", new Vector3(7.05f, 1.4f, -3.1f), new Color(0.12f, 0.5f, 1f));
            CreatePointLight(lighting, "Violet Banish Light", new Vector3(7.05f, 1.2f, -0.62f), new Color(0.6f, 0.18f, 1f));
            CreatePointLight(lighting, "Opponent Blue Graveyard Light", new Vector3(7.05f, 1.4f, 3.1f), new Color(0.12f, 0.5f, 1f));
            CreatePointLight(lighting, "Opponent Violet Banish Light", new Vector3(7.05f, 1.2f, 0.62f), new Color(0.6f, 0.18f, 1f));
        }

        private void OptimizeStaticGeometry()
        {
            if (!Application.isPlaying ||
                !ArcaneGraphicsPreferences.UseStaticArenaBatching)
            {
                return;
            }
            Transform environment = transform.Find("Environment");
            if (environment == null)
                environment = transform.Find("Campo PNG Estatico");
            Transform border = transform.Find("Arena Border");
            if (environment != null)
                StaticBatchingUtility.Combine(environment.gameObject);
            if (border != null)
                StaticBatchingUtility.Combine(border.gameObject);
        }

        private void CreatePointLight(Transform parent, string name, Vector3 position, Color color)
        {
            var item = new GameObject(name, typeof(Light));
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            var light = item.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 2.2f;
            light.color = color;
        }

        private void CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _stone = UseStaticField
                ? NewMaterial(
                    shader,
                    "Pedra de apoio",
                    new Color(0.42f, 0.46f, 0.39f, 1f),
                    false)
                : MaterialWithTexture(
                    shader,
                    "Mossy Stone",
                    GenerateStoneTexture(),
                    new Color(0.58f, 0.65f, 0.46f));
            _darkStone = NewMaterial(shader, "Dark Stone", new Color(0.055f, 0.075f, 0.065f, 1f), false);
            _monsterGlow = NewMaterial(shader, "Monster Zone", new Color(0.08f, 0.75f, 0.68f, 0.62f), true);
            _spellGlow = NewMaterial(shader, "Spell Trap Zone", new Color(0.92f, 0.63f, 0.18f, 0.52f), true);
            _gold = NewMaterial(shader, "Ancient Gold", new Color(0.62f, 0.43f, 0.12f, 1f), false);
            _blueWell = NewMaterial(shader, "Graveyard Energy", new Color(0.03f, 0.25f, 0.8f, 1f), false);
            _violetWell = NewMaterial(shader, "Banishment Energy", new Color(0.42f, 0.04f, 0.7f, 1f), false);
            _paperEdgeTexture = GeneratePaperEdgeTexture();
            _paperEdges = MaterialWithTexture(
                shader,
                "Warm Paper Edges",
                _paperEdgeTexture,
                Color.white);
            _paperEdges.mainTextureScale = new Vector2(2f, 7f);
            _pageLines = NewMaterial(shader, "Paper Layer Lines", new Color(0.48f, 0.47f, 0.43f, 1f), false);
            var sharedBack = cardBackTexture != null
                ? cardBackTexture
                : GenerateCardBack(new Color(0.48f, 0.09f, 0.02f));
            _cardBack = MaterialWithTexture(shader, "Main Deck Back", sharedBack, Color.white);
            _extraBack = MaterialWithTexture(shader, "Extra Deck Back", sharedBack, Color.white);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ??
                           Shader.Find("Unlit/Texture") ??
                           shader;
            if (playerOneFieldTexture != null)
            {
                _playerOneField = MaterialWithTexture(
                    unlit,
                    "Campo PNG - Jogador 1",
                    playerOneFieldTexture,
                    Color.white);
                Texture2D secondTexture = playerTwoFieldTexture != null
                    ? playerTwoFieldTexture
                    : playerOneFieldTexture;
                _playerTwoField = MaterialWithTexture(
                    unlit,
                    "Campo PNG - Jogador 2",
                    secondTexture,
                    Color.white);
            }
            _invisibleZone = NewMaterial(
                unlit,
                "Zona invisivel interativa",
                new Color(0f, 0f, 0f, 0f),
                true);
        }

        private static Material NewMaterial(Shader shader, string name, Color color, bool transparent)
        {
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt(
                    "_SrcBlend",
                    (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt(
                    "_DstBlend",
                    (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.22f);
            return material;
        }

        private static Material MaterialWithTexture(Shader shader, string name, Texture texture, Color tint)
        {
            var material = NewMaterial(shader, name, tint, false);
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", tint);
            return material;
        }

        private static Texture2D GenerateStoneTexture()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Detailed Mossy Flagstones",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var uv = new Vector2((float)x / size, (float)y / size);
                var stoneNoise = Mathf.PerlinNoise(uv.x * 25f + 3f, uv.y * 25f + 8f);
                var mossNoise = Mathf.PerlinNoise(uv.x * 8f + 18f, uv.y * 8f + 5f);
                var stone = Color.Lerp(new Color(0.22f, 0.25f, 0.2f), new Color(0.58f, 0.6f, 0.47f), stoneNoise);
                var moss = Color.Lerp(new Color(0.05f, 0.12f, 0.035f), new Color(0.25f, 0.42f, 0.12f), mossNoise);
                pixels[y * size + x] = Color.Lerp(stone, moss, Mathf.SmoothStep(0.58f, 0.82f, mossNoise) * 0.72f);
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static Texture2D GenerateCardBack(Color accent)
        {
            const int width = 192;
            const int height = 272;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Spiral Card Back",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var uv = new Vector2((x + 0.5f) / width, (y + 0.5f) / height);
                var p = new Vector2((uv.x - 0.5f) * 1.45f, uv.y - 0.5f);
                var radius = p.magnitude;
                var spiral = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y, p.x) * 2.5f + radius * 29f))), 7f);
                var border = uv.x < 0.045f || uv.x > 0.955f || uv.y < 0.032f || uv.y > 0.968f;
                pixels[y * width + x] = border
                    ? new Color(0.94f, 0.64f, 0.19f)
                    : Color.Lerp(new Color(0.01f, 0.006f, 0.004f), accent, spiral);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D GeneratePaperEdgeTexture()
        {
            const int width = 128;
            const int height = 128;
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                true)
            {
                name = "Layered Card Paper Edges",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float fiber = Mathf.PerlinNoise(
                    x * 0.105f + 7.3f,
                    y * 0.037f + 19.1f);
                float fineFiber = Mathf.PerlinNoise(
                    x * 0.31f + 2.7f,
                    y * 0.16f + 5.9f);
                float layer = Mathf.Abs(Mathf.Sin(y * Mathf.PI / 4f));
                float layerShadow = Mathf.SmoothStep(0.86f, 1f, layer);
                Color warmPaper = Color.Lerp(
                    new Color(0.67f, 0.64f, 0.55f, 1f),
                    new Color(0.93f, 0.90f, 0.79f, 1f),
                    fiber * 0.72f + fineFiber * 0.28f);
                pixels[y * width + x] = Color.Lerp(
                    warmPaper,
                    new Color(0.35f, 0.33f, 0.29f, 1f),
                    layerShadow * 0.34f);
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, bool markStatic = true)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.isStatic = markStatic;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(block);
            return block;
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null)
                return;
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }
    }
}
