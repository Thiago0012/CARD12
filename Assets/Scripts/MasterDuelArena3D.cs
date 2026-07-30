using UnityEngine;
using ArcaneArena.Multiplayer;

namespace ArcaneArena
{
    public sealed class MasterDuelArena3D : MonoBehaviour
    {
        public const int CurrentLayoutVersion = 10;
        [SerializeField] private int layoutVersion;
        [SerializeField] private Texture2D cardBackTexture;
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
        private Transform _playerOneMainDeck;
        private Transform _playerTwoMainDeck;
        private Transform _playerOneExtraDeck;
        private Transform _playerTwoExtraDeck;

        private void Awake()
        {
            if (transform.childCount == 0 || NeedsEditorRebuild)
                Rebuild();
            else
                RefreshRegistry();
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
            RestorePlayerDeckLayout(
                mainDeckSnapshot,
                extraDeckSnapshot,
                mainDeckStackSnapshot,
                extraDeckStackSnapshot);
            RefreshRegistry();
        }

        public bool NeedsEditorRebuild => layoutVersion != CurrentLayoutVersion;

        public void SetCardBackTexture(Texture2D texture)
        {
            cardBackTexture = texture;
        }

        private void CreateFoundation()
        {
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

        private void CreatePlayerHalf(Transform players, bool opponent)
        {
            var sideName = opponent ? "PLAYER_2" : "PLAYER_1";
            var owner = opponent ? DuelPlayerSide.PlayerTwo : DuelPlayerSide.PlayerOne;
            var side = new GameObject(sideName);
            side.transform.SetParent(players, false);

            var sign = opponent ? 1f : -1f;
            var rotation = opponent ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            const float spacing = 2.3f;

            var monsterGroup = CreateGroup(side.transform, "MonsterZones");
            var spellGroup = CreateGroup(side.transform, "SpellTrapZones");
            for (var i = 0; i < 5; i++)
            {
                var x = (i - 2) * spacing;
                CreateCardZone(monsterGroup, $"MonsterZone_{i + 1}", new Vector3(x, 0.2f, sign * 1.5f),
                    rotation, _monsterGlow, owner, DuelZoneKind.Monster, i, !opponent);
                CreateCardZone(spellGroup, $"SpellTrapZone_{i + 1}", new Vector3(x, 0.19f, sign * 4.0f),
                    rotation, _spellGlow, owner, DuelZoneKind.SpellTrap, i, false);
            }

            var specials = CreateGroup(side.transform, "SpecialZones");
            var extraDeck = CreateDeckPedestal(specials, "ExtraDeck", new Vector3(-7.05f, 0.18f, sign * 5.75f),
                rotation, _extraBack, false, owner, DuelZoneKind.ExtraDeck);
            var mainDeck = CreateDeckPedestal(specials, "MainDeck", new Vector3(7.05f, 0.18f, sign * 5.75f),
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
            CreateWell(specials, "Graveyard", new Vector3(7.05f, 0.16f, sign * 3.1f),
                _blueWell, owner, DuelZoneKind.Graveyard);
            CreateWell(specials, "Banishment", new Vector3(7.05f, 0.16f, sign * 0.62f),
                _violetWell, owner, DuelZoneKind.Banishment);
            CreateCardZone(
                specials,
                "FieldZone",
                new Vector3(-7.05f, 0.19f, sign * 3.1f),
                rotation,
                _spellGlow,
                owner,
                DuelZoneKind.Field,
                0,
                false);

            if (opponent)
                CreateOpponentHand(side.transform);
        }

        private void CreateCardZone(Transform parent, string name, Vector3 position, Quaternion rotation,
            Material material, DuelPlayerSide owner, DuelZoneKind kind, int zoneIndex, bool interactive)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;
            root.AddComponent<BoxCollider>().size = new Vector3(2f, 0.45f, 2.55f);
            root.AddComponent<DuelZone3D>().Setup(owner, kind, zoneIndex, interactive);

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

        private Transform CreateDeckPedestal(Transform parent, string name, Vector3 position, Quaternion rotation,
            Material backMaterial, bool mainDeck, DuelPlayerSide owner, DuelZoneKind kind)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;
            root.AddComponent<DuelZone3D>().Setup(owner, kind, 0, false);

            CreateBlock(root.transform, "Pedestal", Vector3.zero, new Vector3(2.05f, 0.32f, 2.65f), _darkStone);
            CreateBlock(root.transform, "Gold Trim", new Vector3(0, 0.2f, 0), new Vector3(1.82f, 0.08f, 2.38f), _gold);
            var stack = new GameObject("Card Stack");
            stack.transform.SetParent(root.transform, false);
            stack.transform.localPosition = new Vector3(0, mainDeck ? 0.42f : 0.34f, 0);
            var paperHeight = mainDeck ? 0.34f : 0.18f;
            CreateBlock(stack.transform, "Paper Edges", Vector3.zero,
                new Vector3(1.42f, paperHeight, 1.94f), _paperEdges, false);

            for (var i = 1; i <= 3; i++)
            {
                var layerY = -paperHeight * 0.5f + paperHeight * i / 4f;
                CreateBlock(stack.transform, $"Page Line {i}", new Vector3(0, layerY, 0),
                    new Vector3(1.425f, 0.008f, 1.945f), _pageLines, false);
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
            var deck = side == DuelPlayerSide.PlayerOne ? _playerOneMainDeck : _playerTwoMainDeck;
            if (deck != null)
                return deck.position + deck.up * 0.65f;

            return side == DuelPlayerSide.PlayerOne
                ? new Vector3(7f, 0.8f, -5.7f)
                : new Vector3(7f, 0.8f, 5.7f);
        }

        public void NotifyCardDrawn(DuelPlayerSide side)
        {
            var deck = side == DuelPlayerSide.PlayerOne ? _playerOneMainDeck : _playerTwoMainDeck;
            if (deck == null)
                return;

            var stack = deck.Find("Card Stack");
            if (stack == null)
                return;

            var paper = stack.Find("Paper Edges");
            var topCard = stack.Find("Top Card Back");
            if (paper == null || topCard == null)
                return;

            var scale = paper.localScale;
            scale.y = Mathf.Max(0.08f, scale.y - 0.045f);
            paper.localScale = scale;
            paper.localPosition -= Vector3.up * 0.0225f;
            for (var i = 1; i <= 3; i++)
            {
                var pageLine = stack.Find($"Page Line {i}");
                if (pageLine == null)
                    continue;
                pageLine.localPosition = new Vector3(
                    0f,
                    paper.localPosition.y - scale.y * 0.5f + scale.y * i / 4f,
                    0f);
            }
            topCard.localPosition = new Vector3(
                0f,
                paper.localPosition.y + scale.y * 0.5f + 0.018f,
                0f);
        }

        private void CreateWell(Transform parent, string name, Vector3 position, Material innerMaterial,
            DuelPlayerSide owner, DuelZoneKind kind)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.AddComponent<DuelZone3D>().Setup(owner, kind, 0, false);

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Stone Ring";
            rim.transform.SetParent(root.transform, false);
            rim.transform.localScale = new Vector3(1.08f, 0.16f, 1.08f);
            rim.GetComponent<Renderer>().sharedMaterial = _gold;
            RemoveCollider(rim);

            var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "Energy Well";
            inner.transform.SetParent(root.transform, false);
            inner.transform.localPosition = new Vector3(0, 0.19f, 0);
            inner.transform.localScale = new Vector3(0.78f, 0.08f, 0.78f);
            inner.GetComponent<Renderer>().sharedMaterial = innerMaterial;
            RemoveCollider(inner);
        }

        private void CreateOpponentHand(Transform side)
        {
            var hand = CreateGroup(side, "OpponentHandPreview");
            for (var i = 0; i < 5; i++)
            {
                var card = CreateBlock(hand, $"HiddenCard_{i + 1}",
                    new Vector3((i - 2) * 1.18f, 0.5f, 7.15f),
                    new Vector3(1.04f, 0.055f, 1.48f), _cardBack);
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
            light.shadows = LightShadows.Soft;

            CreatePointLight(lighting, "Blue Graveyard Light", new Vector3(7.05f, 1.4f, -3.1f), new Color(0.12f, 0.5f, 1f));
            CreatePointLight(lighting, "Violet Banish Light", new Vector3(7.05f, 1.2f, -0.62f), new Color(0.6f, 0.18f, 1f));
            CreatePointLight(lighting, "Opponent Blue Graveyard Light", new Vector3(7.05f, 1.4f, 3.1f), new Color(0.12f, 0.5f, 1f));
            CreatePointLight(lighting, "Opponent Violet Banish Light", new Vector3(7.05f, 1.2f, 0.62f), new Color(0.6f, 0.18f, 1f));
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
            _stone = MaterialWithTexture(shader, "Mossy Stone", GenerateStoneTexture(), new Color(0.58f, 0.65f, 0.46f));
            _darkStone = NewMaterial(shader, "Dark Stone", new Color(0.055f, 0.075f, 0.065f, 1f), false);
            _monsterGlow = NewMaterial(shader, "Monster Zone", new Color(0.08f, 0.75f, 0.68f, 0.62f), true);
            _spellGlow = NewMaterial(shader, "Spell Trap Zone", new Color(0.92f, 0.63f, 0.18f, 0.52f), true);
            _gold = NewMaterial(shader, "Ancient Gold", new Color(0.62f, 0.43f, 0.12f, 1f), false);
            _blueWell = NewMaterial(shader, "Graveyard Energy", new Color(0.03f, 0.25f, 0.8f, 1f), false);
            _violetWell = NewMaterial(shader, "Banishment Energy", new Color(0.42f, 0.04f, 0.7f, 1f), false);
            _paperEdges = NewMaterial(shader, "Warm Paper Edges", new Color(0.86f, 0.84f, 0.77f, 1f), false);
            _pageLines = NewMaterial(shader, "Paper Layer Lines", new Color(0.48f, 0.47f, 0.43f, 1f), false);
            var sharedBack = cardBackTexture != null
                ? cardBackTexture
                : GenerateCardBack(new Color(0.48f, 0.09f, 0.02f));
            _cardBack = MaterialWithTexture(shader, "Main Deck Back", sharedBack, Color.white);
            _extraBack = MaterialWithTexture(shader, "Extra Deck Back", sharedBack, Color.white);
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
