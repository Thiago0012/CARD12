using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Presentation-only arena rebuilt from the useful visual structure of the
    /// former project. It owns no cards, turns, choices, or rules.
    /// </summary>
    public sealed class ArcaneField3DPresenter : MonoBehaviour
    {
        private const string RootName = "Arcane 3D Field Presentation";
        private readonly List<Material> materials = new List<Material>();
        private Texture2D stoneTexture;
        private Transform presentationRoot;

        public bool IsReady => presentationRoot != null;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            foreach (Material material in materials)
            {
                if (material != null) Destroy(material);
            }
            if (stoneTexture != null) Destroy(stoneTexture);
        }

        public void EnsureBuilt()
        {
            Transform existing = transform.Find(RootName);
            if (existing != null)
            {
                presentationRoot = existing;
                return;
            }

            presentationRoot = new GameObject(RootName).transform;
            presentationRoot.SetParent(transform, false);
            BuildArena();
        }

        public Rect ZoneRect(
            byte controller,
            byte location,
            int sequence,
            float designWidth,
            float designHeight)
        {
            Vector3 world = ZoneWorld(controller, location, sequence);
            Vector2 size = controller == 0
                ? new Vector2(126f, 162f)
                : new Vector2(108f, 142f);
            return ProjectedRect(world + Vector3.up * 0.32f, size, designWidth, designHeight);
        }

        public Rect SpecialRect(
            byte controller,
            byte location,
            float designWidth,
            float designHeight)
        {
            Vector3 world = ZoneWorld(controller, location, 0);
            Vector2 size =
                location == DuelLocation.Graveyard ||
                location == DuelLocation.Banished
                    ? new Vector2(100f, 126f)
                    : new Vector2(104f, 142f);
            return ProjectedRect(world + Vector3.up * 0.38f, size, designWidth, designHeight);
        }

        private Rect ProjectedRect(
            Vector3 world,
            Vector2 size,
            float designWidth,
            float designHeight)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return new Rect(
                    designWidth * 0.5f - size.x * 0.5f,
                    designHeight * 0.5f - size.y * 0.5f,
                    size.x,
                    size.y);
            }
            Vector3 viewport = camera.WorldToViewportPoint(world);
            float perspective = Mathf.Lerp(
                0.82f,
                1.08f,
                1f - Mathf.Clamp01(viewport.y));
            perspective *= 43f / Mathf.Max(30f, camera.fieldOfView);
            size *= perspective;
            return new Rect(
                viewport.x * designWidth - size.x * 0.5f,
                (1f - viewport.y) * designHeight - size.y * 0.5f,
                size.x,
                size.y);
        }

        private static Vector3 ZoneWorld(
            byte controller,
            byte location,
            int sequence)
        {
            bool opponent = controller != 0;
            float sign = opponent ? 1f : -1f;
            switch (location)
            {
                case (byte)DuelLocation.MonsterZone:
                    return new Vector3((sequence - 2) * 2.3f, 0.2f, sign * 1.5f);
                case (byte)DuelLocation.SpellTrapZone:
                    return new Vector3((sequence - 2) * 2.3f, 0.2f, sign * 4f);
                case (byte)DuelLocation.Extra:
                    return new Vector3(-7.05f, 0.24f, sign * 5.75f);
                case (byte)DuelLocation.Deck:
                    return new Vector3(7.05f, 0.24f, sign * 5.75f);
                case (byte)DuelLocation.Graveyard:
                    return new Vector3(7.05f, 0.22f, sign * 3.1f);
                case (byte)DuelLocation.Banished:
                    return new Vector3(7.05f, 0.22f, sign * 0.62f);
                default:
                    return Vector3.zero;
            }
        }

        private void BuildArena()
        {
            Shader shader =
                Shader.Find("ArcaneDuel/ArenaSurface") ??
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            Material stone = StoneMaterial(shader);
            Material dark = NewMaterial(
                shader,
                "Dark Stone",
                new Color(0.055f, 0.075f, 0.065f, 1f),
                0.22f);
            Material gold = NewMaterial(
                shader,
                "Ancient Gold",
                new Color(0.62f, 0.43f, 0.12f, 1f),
                0.22f);
            Material monster = GlowMaterial(
                shader,
                "Monster Zone",
                new Color(0.08f, 0.75f, 0.68f, 0.62f));
            Material spell = GlowMaterial(
                shader,
                "Spell Trap Zone",
                new Color(0.92f, 0.63f, 0.18f, 0.52f));
            Material blue = EnergyMaterial(
                shader,
                "Graveyard Energy",
                new Color(0.03f, 0.25f, 0.8f, 1f));
            Material violet = EnergyMaterial(
                shader,
                "Banished Energy",
                new Color(0.42f, 0.04f, 0.7f, 1f));
            Material paperEdges = NewMaterial(
                shader,
                "Warm Paper Edges",
                new Color(0.86f, 0.84f, 0.77f, 1f),
                0.12f);
            Material pageLines = NewMaterial(
                shader,
                "Paper Layer Lines",
                new Color(0.48f, 0.47f, 0.43f, 1f),
                0.08f);

            Transform foundation = Group(presentationRoot, "Foundation");
            Block(
                foundation,
                "Base de Pedra",
                new Vector3(0, -0.42f, 0),
                new Vector3(18.8f, 0.8f, 16.2f),
                dark);
            Block(
                foundation,
                "Piso da Arena",
                Vector3.zero,
                new Vector3(16.6f, 0.16f, 14.8f),
                stone);
            Block(
                foundation,
                "Divisor Central",
                new Vector3(0, 0.13f, 0),
                new Vector3(14.2f, 0.13f, 0.16f),
                gold);

            for (float z = -6.6f; z <= 6.6f; z += 2.2f)
            {
                Block(
                    foundation,
                    "Junta Horizontal",
                    new Vector3(0, 0.095f, z),
                    new Vector3(14.6f, 0.025f, 0.055f),
                    dark);
            }
            for (float x = -5.75f; x <= 5.75f; x += 2.3f)
            {
                Block(
                    foundation,
                    "Junta Vertical",
                    new Vector3(x, 0.096f, 0),
                    new Vector3(0.05f, 0.026f, 14f),
                    dark);
            }

            BuildHalf(
                false,
                stone,
                dark,
                gold,
                monster,
                spell,
                blue,
                violet,
                paperEdges,
                pageLines);
            BuildHalf(
                true,
                stone,
                dark,
                gold,
                monster,
                spell,
                blue,
                violet,
                paperEdges,
                pageLines);
            BuildBorder(stone, dark, gold);
            BuildLighting();
        }

        private void BuildHalf(
            bool opponent,
            Material stone,
            Material dark,
            Material gold,
            Material monster,
            Material spell,
            Material blue,
            Material violet,
            Material paperEdges,
            Material pageLines)
        {
            Transform half = Group(
                presentationRoot,
                opponent ? "Opponent Side" : "Player Side");
            float sign = opponent ? 1f : -1f;
            for (int index = 0; index < 5; index++)
            {
                float x = (index - 2) * 2.3f;
                Pedestal(
                    half,
                    $"Monster Zone {index + 1}",
                    new Vector3(x, 0.2f, sign * 1.5f),
                    monster,
                    dark,
                    gold);
                Pedestal(
                    half,
                    $"Spell Trap Zone {index + 1}",
                    new Vector3(x, 0.19f, sign * 4f),
                    spell,
                    dark,
                    gold);
            }

            DeckPedestal(
                half,
                "Extra Deck",
                new Vector3(-7.05f, 0.18f, sign * 5.75f),
                dark,
                gold,
                violet,
                paperEdges,
                pageLines);
            DeckPedestal(
                half,
                "Main Deck",
                new Vector3(7.05f, 0.18f, sign * 5.75f),
                dark,
                gold,
                stone,
                paperEdges,
                pageLines);
            EnergyWell(
                half,
                "Graveyard",
                new Vector3(7.05f, 0.16f, sign * 3.1f),
                gold,
                blue);
            EnergyWell(
                half,
                "Banished",
                new Vector3(7.05f, 0.16f, sign * 0.62f),
                gold,
                violet);
            Pedestal(
                half,
                "Field Zone",
                new Vector3(-7.05f, 0.19f, sign * 3.1f),
                spell,
                dark,
                gold);
        }

        private void Pedestal(
            Transform parent,
            string name,
            Vector3 position,
            Material glow,
            Material dark,
            Material gold)
        {
            Transform root = Group(parent, name);
            root.localPosition = position;
            Primitive(
                PrimitiveType.Cylinder,
                root,
                "Octagonal Pedestal",
                Vector3.zero,
                new Vector3(0.91f, 0.07f, 1.25f),
                glow);
            Block(
                root,
                "Card Inset",
                new Vector3(0, 0.085f, 0),
                new Vector3(1.43f, 0.035f, 1.96f),
                dark);
        }

        private void DeckPedestal(
            Transform parent,
            string name,
            Vector3 position,
            Material dark,
            Material gold,
            Material top,
            Material paperEdges,
            Material pageLines)
        {
            Transform root = Group(parent, name);
            root.localPosition = position;
            Block(
                root,
                "Deck Base",
                Vector3.zero,
                new Vector3(2.05f, 0.32f, 2.65f),
                dark);
            Block(
                root,
                "Gold Trim",
                new Vector3(0, 0.2f, 0),
                new Vector3(1.82f, 0.08f, 2.38f),
                gold);
            float paperHeight = name == "Main Deck" ? 0.34f : 0.18f;
            float stackY = name == "Main Deck" ? 0.42f : 0.34f;
            Block(
                root,
                "Paper Edges",
                new Vector3(0, stackY, 0),
                new Vector3(1.42f, paperHeight, 1.94f),
                paperEdges);
            for (int index = 1; index <= 3; index++)
            {
                float y = stackY - paperHeight * 0.5f +
                          paperHeight * index / 4f;
                Block(
                    root,
                    $"Page Line {index}",
                    new Vector3(0, y, 0),
                    new Vector3(1.425f, 0.008f, 1.945f),
                    pageLines);
            }
            Block(
                root,
                "Top Card Back",
                new Vector3(0, stackY + paperHeight * 0.5f + 0.018f, 0),
                new Vector3(1.43f, 0.026f, 1.95f),
                top);
        }

        private void EnergyWell(
            Transform parent,
            string name,
            Vector3 position,
            Material gold,
            Material energy)
        {
            Transform root = Group(parent, name);
            root.localPosition = position;
            Primitive(
                PrimitiveType.Cylinder,
                root,
                "Gold Ring",
                Vector3.zero,
                new Vector3(1.08f, 0.16f, 1.08f),
                gold);
            Primitive(
                PrimitiveType.Cylinder,
                root,
                "Energy Core",
                new Vector3(0, 0.18f, 0),
                new Vector3(0.78f, 0.08f, 0.78f),
                energy);
        }

        private void BuildCentralSigil(
            Material dark,
            Material gold,
            Material energy)
        {
            Transform sigil = Group(presentationRoot, "Central Duel Sigil");
            Primitive(
                PrimitiveType.Cylinder,
                sigil,
                "Outer Sigil",
                new Vector3(0, 0.105f, 0),
                new Vector3(1.45f, 0.022f, 1.45f),
                gold);
            Primitive(
                PrimitiveType.Cylinder,
                sigil,
                "Inner Sigil",
                new Vector3(0, 0.135f, 0),
                new Vector3(1.08f, 0.018f, 1.08f),
                dark);
            Primitive(
                PrimitiveType.Cylinder,
                sigil,
                "Energy Seal",
                new Vector3(0, 0.158f, 0),
                new Vector3(0.68f, 0.012f, 0.68f),
                energy);
            for (int index = 0; index < 4; index++)
            {
                GameObject diamond = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                diamond.name = $"Sigil Diamond {index + 1}";
                diamond.transform.SetParent(sigil, false);
                float angle = index * Mathf.PI * 0.5f;
                diamond.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 1.75f,
                    0.16f,
                    Mathf.Sin(angle) * 1.75f);
                diamond.transform.localRotation =
                    Quaternion.Euler(0, 45f - index * 90f, 0);
                diamond.transform.localScale =
                    new Vector3(0.34f, 0.025f, 0.34f);
                diamond.GetComponent<Renderer>().sharedMaterial = gold;
                Collider collider = diamond.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }
        }

        private void BuildBorder(
            Material stone,
            Material dark,
            Material gold)
        {
            Transform border = Group(presentationRoot, "Arena Border");
            for (float x = -8.4f; x <= 8.4f; x += 1.2f)
            {
                Block(
                    border,
                    "Border Stone",
                    new Vector3(x, 0.08f, -7.75f),
                    new Vector3(1.08f, 0.45f, 0.65f),
                    stone);
                Block(
                    border,
                    "Border Stone",
                    new Vector3(x, 0.08f, 7.75f),
                    new Vector3(1.08f, 0.45f, 0.65f),
                    stone);
            }
            for (float z = -6.8f; z <= 6.8f; z += 1.2f)
            {
                Block(
                    border,
                    "Side Stone",
                    new Vector3(-9.05f, 0.05f, z),
                    new Vector3(0.65f, 0.5f, 1.05f),
                    stone);
                Block(
                    border,
                    "Side Stone",
                    new Vector3(9.05f, 0.05f, z),
                    new Vector3(0.65f, 0.5f, 1.05f),
                    stone);
            }
        }

        private void BuildSideEnvironment(
            Material dark,
            Material gold,
            Material blue,
            Material violet)
        {
            Transform environment =
                Group(presentationRoot, "Arcane Side Environment");
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 10.55f;
                Block(
                    environment,
                    side < 0 ? "Left Obsidian Wing" : "Right Obsidian Wing",
                    new Vector3(x, -0.31f, 0),
                    new Vector3(2.15f, 0.52f, 15.4f),
                    dark);
                Block(
                    environment,
                    "Luminous Wing Rail",
                    new Vector3(side * 9.72f, 0.16f, 0),
                    new Vector3(0.09f, 0.10f, 14.6f),
                    gold);
                for (int index = 0; index < 4; index++)
                {
                    float z = -5.25f + index * 3.5f;
                    Block(
                        environment,
                        "Relic Plinth",
                        new Vector3(x, 0.03f, z),
                        new Vector3(1.25f, 0.30f, 1.25f),
                        gold);
                    Primitive(
                        PrimitiveType.Sphere,
                        environment,
                        "Arcane Relic",
                        new Vector3(x, 0.42f, z),
                        new Vector3(0.62f, 0.36f, 0.62f),
                        (index + side) % 2 == 0 ? blue : violet);
                }
                for (int corner = -1; corner <= 1; corner += 2)
                {
                    Vector3 position =
                        new Vector3(x, 0.20f, corner * 7.05f);
                    Block(
                        environment,
                        "Corner Monolith",
                        position,
                        new Vector3(1.35f, 1.25f, 1.20f),
                        dark);
                    Block(
                        environment,
                        "Monolith Crown",
                        position + Vector3.up * 0.72f,
                        new Vector3(1.05f, 0.18f, 0.95f),
                        gold);
                }
            }
        }

        private void BuildLighting()
        {
            RenderSettings.ambientLight = new Color(0.30f, 0.35f, 0.34f);
            GameObject sun = new GameObject("Arena Sun", typeof(Light));
            sun.transform.SetParent(presentationRoot, false);
            sun.transform.rotation = Quaternion.Euler(48f, -28f, 0);
            Light light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.92f, 0.75f);
            light.shadows = LightShadows.Soft;

            PointLight("Player Blue Well", new Vector3(7.05f, 1.5f, -3.1f), new Color(0.05f, 0.42f, 1f));
            PointLight("Player Violet Well", new Vector3(7.05f, 1.3f, -0.62f), new Color(0.55f, 0.08f, 1f));
            PointLight("Opponent Blue Well", new Vector3(7.05f, 1.5f, 3.1f), new Color(0.05f, 0.42f, 1f));
            PointLight("Opponent Violet Well", new Vector3(7.05f, 1.3f, 0.62f), new Color(0.55f, 0.08f, 1f));
        }

        private void PointLight(string name, Vector3 position, Color color)
        {
            GameObject item = new GameObject(name, typeof(Light));
            item.transform.SetParent(presentationRoot, false);
            item.transform.localPosition = position;
            Light light = item.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 2.2f;
            light.color = color;
        }

        private Material StoneMaterial(Shader shader)
        {
            stoneTexture = CreateStoneTexture();
            Material material = NewMaterial(
                shader,
                "Mossy Flagstone",
                new Color(0.82f, 0.88f, 0.76f),
                0.24f);
            material.mainTexture = stoneTexture;
            return material;
        }

        private Material GlowMaterial(Shader shader, string name, Color color)
        {
            Material material = NewMaterial(shader, name, color, 0.34f);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.22f);
            }
            return material;
        }

        private Material EnergyMaterial(
            Shader shader,
            string name,
            Color color)
        {
            Material material = NewMaterial(shader, name, color, 0.72f);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.52f);
            }
            return material;
        }

        private Material NewMaterial(
            Shader shader,
            string name,
            Color color,
            float smoothness)
        {
            Material material = new Material(shader)
            {
                name = name,
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            materials.Add(material);
            return material;
        }

        private static Texture2D CreateStoneTexture()
        {
            const int size = 512;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                true)
            {
                name = "Detailed Mossy Flagstones",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new Vector2((float)x / size, (float)y / size);
                    float stone = Mathf.PerlinNoise(uv.x * 25f + 3f, uv.y * 25f + 8f);
                    float moss = Mathf.PerlinNoise(uv.x * 8f + 18f, uv.y * 8f + 5f);
                    Color rock = Color.Lerp(
                        new Color(0.22f, 0.25f, 0.20f),
                        new Color(0.58f, 0.60f, 0.47f),
                        stone);
                    Color green = Color.Lerp(
                        new Color(0.05f, 0.12f, 0.035f),
                        new Color(0.25f, 0.42f, 0.12f),
                        moss);
                    pixels[y * size + x] = Color.Lerp(
                        rock,
                        green,
                        Mathf.SmoothStep(0.58f, 0.82f, moss) * 0.72f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static Transform Group(Transform parent, string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void Block(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            Primitive(
                PrimitiveType.Cube,
                parent,
                name,
                position,
                scale,
                material);
        }

        private static void ChamferedPlate(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 size,
            float chamfer,
            Material material)
        {
            float halfX = size.x * 0.5f;
            float halfY = size.y * 0.5f;
            float halfZ = size.z * 0.5f;
            chamfer = Mathf.Min(
                chamfer,
                Mathf.Min(halfX, halfZ) * 0.8f);
            var outline = new[]
            {
                new Vector2(-halfX + chamfer, -halfZ),
                new Vector2(halfX - chamfer, -halfZ),
                new Vector2(halfX, -halfZ + chamfer),
                new Vector2(halfX, halfZ - chamfer),
                new Vector2(halfX - chamfer, halfZ),
                new Vector2(-halfX + chamfer, halfZ),
                new Vector2(-halfX, halfZ - chamfer),
                new Vector2(-halfX, -halfZ + chamfer)
            };
            var vertices = new Vector3[18];
            for (int index = 0; index < outline.Length; index++)
            {
                vertices[index] =
                    new Vector3(outline[index].x, halfY, outline[index].y);
                vertices[index + 8] =
                    new Vector3(outline[index].x, -halfY, outline[index].y);
            }
            vertices[16] = new Vector3(0, halfY, 0);
            vertices[17] = new Vector3(0, -halfY, 0);
            var triangles = new List<int>(96);
            for (int index = 0; index < outline.Length; index++)
            {
                int next = (index + 1) % outline.Length;
                triangles.Add(16);
                triangles.Add(next);
                triangles.Add(index);
                triangles.Add(17);
                triangles.Add(index + 8);
                triangles.Add(next + 8);
                triangles.Add(index);
                triangles.Add(next);
                triangles.Add(next + 8);
                triangles.Add(index);
                triangles.Add(next + 8);
                triangles.Add(index + 8);
            }
            var mesh = new Mesh
            {
                name = $"{name} Mesh",
                vertices = vertices,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject item = new GameObject(
                name,
                typeof(MeshFilter),
                typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Primitive(
            PrimitiveType type,
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = item.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }
}
