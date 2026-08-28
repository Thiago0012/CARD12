using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Cards;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// World-space Particle System layer for alternative summon methods.
    /// It complements the UI sigil and deliberately avoids VFX Graph so the
    /// same effect remains available on Android and lower-end GPUs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SummonMethodParticleVfx : MonoBehaviour
    {
        private const float Lifetime = 1.34f;
        private static Texture2D softParticleTexture;
        private static Material softParticleMaterial;

        private readonly List<ParticleSystem> particleSystems = new();
        private readonly List<LineRenderer> energyLines = new();
        private MonsterFrameKind frameKind;
        private Color primary;
        private Color secondary;
        private bool reducedDetail;
        private bool allowTrails = true;
        private float particleCountScale = 1f;

        public MonsterFrameKind FrameKind => frameKind;
        public int ParticleSystemCount => particleSystems.Count;
        public int EnergyLineCount => energyLines.Count;
        public bool ReducedDetail => reducedDetail;

        public static SummonMethodParticleVfx Play(
            Transform parent,
            MonsterFrameKind frame,
            bool useReducedDetail)
        {
            return PlayInternal(
                parent,
                frame,
                useReducedDetail,
                useReducedDetail,
                1f);
        }

        public static SummonMethodParticleVfx PlayForCurrentQuality(
            Transform parent,
            MonsterFrameKind frame)
        {
            ArcaneGraphicsQuality quality = ArcaneGraphicsPreferences.Quality;
            bool reduced = quality <= ArcaneGraphicsQuality.Low;
            bool trails = quality > ArcaneGraphicsQuality.VeryLow;
            float countScale = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 0.66f,
                ArcaneGraphicsQuality.Low => 1f,
                ArcaneGraphicsQuality.Medium => 0.76f,
                ArcaneGraphicsQuality.High => 1f,
                _ => 1.24f
            };
            return PlayInternal(
                parent,
                frame,
                reduced,
                trails,
                countScale);
        }

        private static SummonMethodParticleVfx PlayInternal(
            Transform parent,
            MonsterFrameKind frame,
            bool useReducedDetail,
            bool useTrails,
            float countScale)
        {
            if (parent == null || !SummonMethodVfxPalette.Supports(frame))
                return null;

            var root = new GameObject($"VFX de Invocação {frame}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            root.transform.localScale = Vector3.one;
            SummonMethodParticleVfx effect =
                root.AddComponent<SummonMethodParticleVfx>();
            effect.Configure(
                frame,
                useReducedDetail,
                useTrails,
                countScale);
            return effect;
        }

        private void Configure(
            MonsterFrameKind frame,
            bool useReducedDetail,
            bool useTrails,
            float countScale)
        {
            frameKind = frame;
            reducedDetail = useReducedDetail;
            allowTrails = useTrails;
            particleCountScale = Mathf.Max(0.25f, countScale);
            primary = SummonMethodVfxPalette.Primary(frame);
            secondary = SummonMethodVfxPalette.Secondary(frame);

            switch (frameKind)
            {
                case MonsterFrameKind.Fusion:
                    BuildFusion();
                    break;
                case MonsterFrameKind.Synchro:
                    BuildSynchro();
                    break;
                case MonsterFrameKind.Xyz:
                    BuildXyz();
                    break;
                case MonsterFrameKind.Link:
                    BuildLink();
                    break;
                case MonsterFrameKind.Pendulum:
                    BuildPendulum();
                    break;
            }

            foreach (ParticleSystem system in particleSystems)
                system.Play(true);
            StartCoroutine(AnimateAndDispose());
        }

        private void BuildFusion()
        {
            ParticleSystem left = CreateEmitter(
                "Espiral magenta",
                primary,
                new Vector3(-0.24f, 0f, 0f),
                ParticleSystemShapeType.Circle,
                0.46f,
                Vector3.one,
                Count(28, 16),
                0f,
                new Vector2(0.52f, 0.78f),
                new Vector2(0.15f, 0.44f),
                new Vector2(0.055f, 0.12f),
                3.1f,
                true,
                false);
            SetRadialVelocity(left, -0.72f);

            ParticleSystem right = CreateEmitter(
                "Espiral carmesim",
                secondary,
                new Vector3(0.24f, 0f, 0f),
                ParticleSystemShapeType.Circle,
                0.46f,
                Vector3.one,
                Count(28, 16),
                0.025f,
                new Vector2(0.52f, 0.78f),
                new Vector2(0.15f, 0.44f),
                new Vector2(0.055f, 0.12f),
                -3.1f,
                true,
                false);
            SetRadialVelocity(right, -0.72f);

            CreateEmitter(
                "Núcleo da fusão",
                Color.Lerp(primary, Color.white, 0.68f),
                Vector3.zero,
                ParticleSystemShapeType.Sphere,
                0.12f,
                Vector3.one,
                Count(18, 10),
                0.26f,
                new Vector2(0.24f, 0.42f),
                new Vector2(0.55f, 1.15f),
                new Vector2(0.08f, 0.18f),
                0f,
                false,
                true);
        }

        private void BuildSynchro()
        {
            float[] radii = { 0.30f, 0.54f, 0.79f };
            for (int index = 0; index < radii.Length; index++)
            {
                ParticleSystem ring = CreateEmitter(
                    $"Anel de sintonia {index + 1}",
                    index == 1 ? secondary : primary,
                    Vector3.zero,
                    ParticleSystemShapeType.Circle,
                    radii[index],
                    Vector3.one,
                    Count(18 - index * 2, 10 - index),
                    index * 0.075f,
                    new Vector2(0.44f, 0.68f),
                    new Vector2(0.04f, 0.13f),
                    new Vector2(0.035f, 0.075f),
                    index % 2 == 0 ? 1.2f : -1.2f,
                    false,
                    false);
                SetRadialVelocity(ring, -0.18f);
            }

            CreateEmitter(
                "Raios sincro",
                Color.white,
                Vector3.zero,
                ParticleSystemShapeType.Sphere,
                0.08f,
                Vector3.one,
                Count(20, 11),
                0.22f,
                new Vector2(0.30f, 0.54f),
                new Vector2(1.0f, 1.75f),
                new Vector2(0.045f, 0.085f),
                0f,
                true,
                true);
        }

        private void BuildXyz()
        {
            CreateEmitter(
                "Matéria Xyz violeta",
                secondary,
                Vector3.zero,
                ParticleSystemShapeType.Circle,
                0.52f,
                Vector3.one,
                Count(24, 13),
                0f,
                new Vector2(0.72f, 1.02f),
                new Vector2(0.02f, 0.12f),
                new Vector2(0.045f, 0.095f),
                3.4f,
                true,
                false);
            CreateEmitter(
                "Matéria Xyz dourada",
                primary,
                Vector3.zero,
                ParticleSystemShapeType.Circle,
                0.78f,
                Vector3.one,
                Count(22, 12),
                0.05f,
                new Vector2(0.68f, 0.98f),
                new Vector2(0.02f, 0.12f),
                new Vector2(0.045f, 0.11f),
                -2.8f,
                true,
                false);
            CreateEmitter(
                "Estouro de Rank",
                Color.Lerp(primary, Color.white, 0.42f),
                Vector3.zero,
                ParticleSystemShapeType.Sphere,
                0.10f,
                Vector3.one,
                Count(16, 9),
                0.30f,
                new Vector2(0.28f, 0.48f),
                new Vector2(0.70f, 1.40f),
                new Vector2(0.06f, 0.14f),
                0f,
                false,
                true);
        }

        private void BuildLink()
        {
            int nodeCount = reducedDetail ? 6 : 8;
            var points = new Vector3[nodeCount];
            for (int index = 0; index < nodeCount; index++)
            {
                float angle = index * Mathf.PI * 2f / nodeCount;
                points[index] = new Vector3(
                    Mathf.Cos(angle) * 0.72f,
                    Mathf.Sin(angle) * 0.72f,
                    0f);
            }

            CreateEnergyPath("Circuito Link", points, true, primary, 0.035f);
            int spokes = reducedDetail ? 3 : 4;
            for (int index = 0; index < spokes; index++)
            {
                int opposite = (index + nodeCount / 2) % nodeCount;
                CreateEnergyPath(
                    $"Conexão Link {index + 1}",
                    new[] { points[index], Vector3.zero, points[opposite] },
                    false,
                    index % 2 == 0 ? primary : secondary,
                    0.024f);
            }

            ParticleSystem nodes = CreateEmitter(
                "Nós digitais Link",
                primary,
                Vector3.zero,
                ParticleSystemShapeType.Sphere,
                0.01f,
                Vector3.one,
                0,
                0f,
                new Vector2(0.64f, 0.94f),
                Vector2.zero,
                new Vector2(0.08f, 0.13f),
                0f,
                true,
                false);
            foreach (Vector3 point in points)
                EmitAt(nodes, point, primary, 0.10f, 0.86f);
            EmitAt(nodes, Vector3.zero, Color.white, 0.15f, 0.86f);
        }

        private void BuildPendulum()
        {
            ParticleSystem left = CreateEmitter(
                "Pilar Pêndulo esquerdo",
                primary,
                new Vector3(-0.62f, 0f, 0f),
                ParticleSystemShapeType.Box,
                0.01f,
                new Vector3(0.10f, 0.88f, 0.04f),
                Count(25, 13),
                0f,
                new Vector2(0.48f, 0.78f),
                new Vector2(0.18f, 0.52f),
                new Vector2(0.04f, 0.09f),
                0f,
                true,
                true);
            SetLinearVelocity(left, new Vector3(0.32f, 0f, 0f));

            ParticleSystem right = CreateEmitter(
                "Pilar Pêndulo direito",
                secondary,
                new Vector3(0.62f, 0f, 0f),
                ParticleSystemShapeType.Box,
                0.01f,
                new Vector3(0.10f, 0.88f, 0.04f),
                Count(25, 13),
                0.025f,
                new Vector2(0.48f, 0.78f),
                new Vector2(0.18f, 0.52f),
                new Vector2(0.04f, 0.09f),
                0f,
                true,
                true);
            SetLinearVelocity(right, new Vector3(-0.32f, 0f, 0f));

            ParticleSystem arc = CreateEmitter(
                "Arco das escalas",
                Color.Lerp(primary, secondary, 0.50f),
                new Vector3(0f, 0.08f, 0f),
                ParticleSystemShapeType.Circle,
                0.76f,
                Vector3.one,
                Count(28, 15),
                0.16f,
                new Vector2(0.46f, 0.72f),
                new Vector2(0.04f, 0.18f),
                new Vector2(0.045f, 0.095f),
                0.85f,
                true,
                false);
            ParticleSystem.ShapeModule shape = arc.shape;
            shape.arc = 160f;
            arc.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);

            ParticleSystem dust = CreateEmitter(
                "Poeira das escalas Pêndulo",
                Color.Lerp(primary, secondary, 0.50f),
                new Vector3(0f, -0.34f, 0f),
                ParticleSystemShapeType.Box,
                0.01f,
                new Vector3(1.25f, 0.18f, 0.08f),
                Count(32, 15),
                0.04f,
                new Vector2(0.54f, 0.92f),
                new Vector2(0.04f, 0.18f),
                new Vector2(0.022f, 0.070f),
                0.18f,
                false,
                false);
            SetLinearVelocity(dust, new Vector3(0f, 0.34f, 0f));
        }

        private ParticleSystem CreateEmitter(
            string objectName,
            Color tint,
            Vector3 localPosition,
            ParticleSystemShapeType shapeType,
            float radius,
            Vector3 shapeScale,
            int particleCount,
            float burstDelay,
            Vector2 lifetime,
            Vector2 speed,
            Vector2 size,
            float orbitalVelocity,
            bool trails,
            bool stretched)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            particleSystems.Add(system);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.48f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                lifetime.x,
                lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(
                tint,
                Color.Lerp(tint, Color.white, 0.40f));
            main.maxParticles = particleCount > 0
                ? particleCount + 4
                : Count(12, 8);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.useUnscaledTime = true;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = particleCount > 0;
            emission.rateOverTime = 0f;
            if (particleCount > 0)
            {
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(
                        burstDelay,
                        (short)particleCount)
                });
            }

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = particleCount > 0;
            shape.shapeType = shapeType;
            shape.radius = radius;
            shape.scale = shapeScale;
            if (shapeType == ParticleSystemShapeType.Circle)
                shape.radiusThickness = 0.08f;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                system.velocityOverLifetime;
            velocity.enabled = Mathf.Abs(orbitalVelocity) > 0.001f;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalZ = orbitalVelocity;

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = particleCount > 0;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = reducedDetail ? 0.08f : 0.14f;
            noise.frequency = 0.72f;
            noise.scrollSpeed = 0.34f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(tint);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.16f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0.06f)));

            ParticleSystem.TrailModule trail = system.trails;
            bool trailsEnabled = trails && allowTrails;
            trail.enabled = trailsEnabled;
            if (trailsEnabled)
            {
                trail.lifetime = reducedDetail ? 0.16f : 0.26f;
                trail.minVertexDistance = reducedDetail ? 0.10f : 0.055f;
                trail.dieWithParticles = true;
                trail.widthOverTrail = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 1f, 1f, 0f));
                trail.colorOverLifetime = FadeGradient(tint);
            }

            ParticleSystemRenderer renderer =
                child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = stretched
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingOrder = 12;
            renderer.minParticleSize = 0.002f;
            renderer.maxParticleSize = 0.12f;
            if (stretched)
            {
                renderer.velocityScale = 0.18f;
                renderer.lengthScale = 1.8f;
            }
            renderer.sharedMaterial = SharedParticleMaterial();
            if (trailsEnabled)
                renderer.trailMaterial = SharedParticleMaterial();
            return system;
        }

        private void CreateEnergyPath(
            string objectName,
            IReadOnlyList<Vector3> points,
            bool loop,
            Color tint,
            float width)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            energyLines.Add(line);
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Count;
            for (int index = 0; index < points.Count; index++)
                line.SetPosition(index, points[index]);
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.30f),
                new Keyframe(0.18f, 1f),
                new Keyframe(0.82f, 1f),
                new Keyframe(1f, 0.30f));
            line.numCornerVertices = reducedDetail ? 0 : 2;
            line.numCapVertices = reducedDetail ? 0 : 2;
            line.startColor = tint;
            line.endColor = Color.Lerp(tint, Color.white, 0.46f);
            line.sharedMaterial = SharedParticleMaterial();
            line.sortingOrder = 11;
        }

        private IEnumerator AnimateAndDispose()
        {
            float elapsed = 0f;
            while (elapsed < Lifetime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Lifetime);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float flicker = Mathf.Lerp(
                    0.72f,
                    1f,
                    0.5f + 0.5f * Mathf.Sin(elapsed * 24f));
                for (int index = 0; index < energyLines.Count; index++)
                {
                    LineRenderer line = energyLines[index];
                    if (line == null)
                        continue;
                    Color tint = index % 2 == 0 ? primary : secondary;
                    tint.a = envelope * flicker;
                    line.startColor = tint;
                    line.endColor = new Color(
                        1f,
                        1f,
                        1f,
                        envelope * flicker * 0.82f);
                }
                yield return null;
            }
            if (this != null)
                Destroy(gameObject);
        }

        private int Count(int desktop, int mobile)
        {
            int baseline = reducedDetail ? mobile : desktop;
            return Mathf.Max(
                1,
                Mathf.RoundToInt(baseline * particleCountScale));
        }

        private static void SetRadialVelocity(
            ParticleSystem system,
            float velocityValue)
        {
            if (system == null)
                return;
            ParticleSystem.VelocityOverLifetimeModule velocity =
                system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = velocityValue;
        }

        private static void SetLinearVelocity(
            ParticleSystem system,
            Vector3 velocityValue)
        {
            if (system == null)
                return;
            ParticleSystem.VelocityOverLifetimeModule velocity =
                system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = velocityValue.x;
            velocity.y = velocityValue.y;
            velocity.z = velocityValue.z;
        }

        private static void EmitAt(
            ParticleSystem system,
            Vector3 position,
            Color tint,
            float size,
            float lifetime)
        {
            if (system == null)
                return;
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = tint,
                startSize = size,
                startLifetime = lifetime
            };
            system.Emit(parameters, 1);
        }

        private static ParticleSystem.MinMaxGradient FadeGradient(Color tint)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        Color.Lerp(tint, Color.white, 0.36f),
                        0f),
                    new GradientColorKey(tint, 0.54f),
                    new GradientColorKey(
                        Color.Lerp(tint, Color.black, 0.22f),
                        1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.84f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static Material SharedParticleMaterial()
        {
            if (softParticleMaterial != null)
                return softParticleMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            softParticleMaterial = new Material(shader)
            {
                name = "Material VFX de Invocação",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3100
            };
            Texture2D texture = SoftParticleTexture();
            if (softParticleMaterial.HasProperty("_BaseMap"))
                softParticleMaterial.SetTexture("_BaseMap", texture);
            if (softParticleMaterial.HasProperty("_MainTex"))
                softParticleMaterial.SetTexture("_MainTex", texture);
            if (softParticleMaterial.HasProperty("_BaseColor"))
                softParticleMaterial.SetColor("_BaseColor", Color.white);
            if (softParticleMaterial.HasProperty("_Color"))
                softParticleMaterial.SetColor("_Color", Color.white);
            return softParticleMaterial;
        }

        private static Texture2D SoftParticleTexture()
        {
            if (softParticleTexture != null)
                return softParticleTexture;

            const int size = 32;
            softParticleTexture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Textura radial VFX de Invocação",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            float inverseRadius = 1f / (size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        center) * inverseRadius;
                    float alpha = Mathf.Pow(
                        Mathf.Clamp01(1f - distance),
                        1.75f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            softParticleTexture.SetPixels(pixels);
            softParticleTexture.Apply(false, true);
            return softParticleTexture;
        }
    }
}
