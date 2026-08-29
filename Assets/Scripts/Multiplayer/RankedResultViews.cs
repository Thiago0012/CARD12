using System;
using System.Collections;
using System.Collections.Generic;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Multiplayer
{
    public sealed class RankPointsBarView : MonoBehaviour
    {
        private Image fill;
        private Image energyFlow;
        private Text valueLabel;
        private Text remainingLabel;
        private Color restingColor;

        public void Initialize(
            Image fillImage,
            Image energyFlowImage,
            Text value,
            Text remaining)
        {
            fill = fillImage;
            energyFlow = energyFlowImage;
            valueLabel = value;
            remainingLabel = remaining;
            restingColor = fill != null ? fill.color : Color.white;
        }

        public void SetState(int points)
        {
            RankTier tier = RankRules.ResolveTier(points);
            SetVisual(tier, RankRules.TierProgress01(points), points);
        }

        public void SetVisual(RankTier tier, float progress, int absolutePoints)
        {
            progress = Mathf.Clamp01(progress);
            if (fill != null)
                fill.fillAmount = progress;
            RankDefinition definition = RankRules.Definition(tier);
            int inside = Mathf.Clamp(
                absolutePoints - definition.Minimum,
                0,
                tier == RankTier.GrandMaster ? 25 : 24);
            if (valueLabel != null)
            {
                valueLabel.text = tier == RankTier.GrandMaster &&
                                  absolutePoints >= RankRules.MaximumPoints
                    ? "200 PE · MAX"
                    : $"{absolutePoints} PE";
            }
            if (remainingLabel == null)
                return;
            if (tier == RankTier.GrandMaster)
            {
                remainingLabel.text = absolutePoints >= RankRules.MaximumPoints
                    ? "RANQUE MÁXIMO"
                    : $"{RankRules.MaximumPoints - absolutePoints} PE PARA MAX";
            }
            else
            {
                RankTier next = (RankTier)((int)tier + 1);
                int remaining = definition.Maximum + 1 - absolutePoints;
                remainingLabel.text =
                    $"{inside}/25 · {remaining} PE PARA " +
                    RankRules.DisplayName(next);
            }
        }

        public void SetMotionEnergy(float intensity, bool positive)
        {
            float pulse = Mathf.Clamp01(intensity);
            Color accent = positive
                ? new Color(0.54f, 1f, 0.34f, 1f)
                : new Color(1f, 0.34f, 0.32f, 1f);
            if (fill != null)
                fill.color = Color.Lerp(restingColor, accent, pulse * 0.46f);
            if (energyFlow != null)
            {
                energyFlow.fillAmount = fill != null ? fill.fillAmount : 0f;
                energyFlow.color = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    pulse * 0.38f);
            }
            if (valueLabel != null)
            {
                float scale = 1f + pulse * 0.11f;
                valueLabel.rectTransform.localScale = new Vector3(
                    scale,
                    scale,
                    1f);
            }
        }

        public void ClearMotionEnergy()
        {
            if (fill != null)
                fill.color = restingColor;
            if (energyFlow != null)
                energyFlow.color = Color.clear;
            if (valueLabel != null)
                valueLabel.rectTransform.localScale = Vector3.one;
        }
    }

    public sealed class RankEmblemView : MonoBehaviour
    {
        private Image emblem;
        private Text tierLabel;
        private CanvasGroup group;
        private CanvasGroup labelGroup;

        public void Initialize(Image image, Text label)
        {
            emblem = image;
            tierLabel = label;
            group = image != null
                ? image.GetComponent<CanvasGroup>() ??
                  image.gameObject.AddComponent<CanvasGroup>()
                : null;
            labelGroup = label != null
                ? label.GetComponent<CanvasGroup>() ??
                  label.gameObject.AddComponent<CanvasGroup>()
                : null;
        }

        public void SetTier(RankTier tier)
        {
            if (emblem != null)
            {
                emblem.sprite = RankBadgeCatalog.Get(tier);
                emblem.preserveAspect = true;
                emblem.color = Color.white;
            }
            if (tierLabel != null)
                tierLabel.text = RankRules.DisplayName(tier);
        }

        public IEnumerator SetCinematicVisibility(bool visible, float duration)
        {
            if (group == null)
                yield break;
            float from = group.alpha;
            float to = visible ? 1f : 0f;
            duration = Mathf.Max(0.01f, duration);
            for (float elapsed = 0f; elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = EaseOutCubic(elapsed / duration);
                group.alpha = Mathf.Lerp(from, to, t);
                if (labelGroup != null)
                    labelGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            group.alpha = to;
            if (labelGroup != null)
                labelGroup.alpha = to;
        }

        public IEnumerator SwapTo(RankTier tier, float duration)
        {
            if (emblem == null || group == null)
            {
                SetTier(tier);
                yield break;
            }
            RectTransform rect = emblem.rectTransform;
            Vector3 original = Vector3.one;
            float half = Mathf.Max(0.08f, duration * 0.5f);
            for (float elapsed = 0f; elapsed < half;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / half);
                group.alpha = 1f - t;
                if (labelGroup != null)
                    labelGroup.alpha = 1f - t;
                rect.localScale = Vector3.Lerp(original, original * 0.82f, t);
                yield return null;
            }
            SetTier(tier);
            for (float elapsed = 0f; elapsed < half;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / half);
                group.alpha = t;
                if (labelGroup != null)
                    labelGroup.alpha = t;
                rect.localScale = Vector3.Lerp(original * 1.18f, original, t);
                yield return null;
            }
            group.alpha = 1f;
            if (labelGroup != null)
                labelGroup.alpha = 1f;
            rect.localScale = original;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }
    }

    public sealed class RankSideSlotView : MonoBehaviour
    {
        private Image current;
        private Image next;
        private Text currentLabel;
        private Text nextLabel;

        public void Initialize(
            Image currentImage,
            Text currentText,
            Image nextImage,
            Text nextText)
        {
            current = currentImage;
            currentLabel = currentText;
            next = nextImage;
            nextLabel = nextText;
        }

        public void SetTier(RankTier tier)
        {
            SetImage(current, tier);
            if (currentLabel != null)
                currentLabel.text = RankRules.DisplayName(tier);
            bool hasNext = tier < RankTier.GrandMaster;
            if (next != null)
                next.gameObject.SetActive(hasNext);
            if (nextLabel != null)
            {
                nextLabel.text = hasNext
                    ? RankRules.DisplayName((RankTier)((int)tier + 1))
                    : "MAX";
            }
            if (hasNext)
                SetImage(next, (RankTier)((int)tier + 1));
        }

        private static void SetImage(Image image, RankTier tier)
        {
            if (image == null)
                return;
            image.sprite = RankBadgeCatalog.Get(tier);
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.82f);
        }
    }

    public sealed class RankResultBanner : MonoBehaviour
    {
        private Text result;
        private Text delta;
        private Text transition;
        private CanvasGroup resultGroup;
        private CanvasGroup deltaGroup;

        public void Initialize(Text resultText, Text deltaText, Text transitionText)
        {
            result = resultText;
            delta = deltaText;
            transition = transitionText;
            resultGroup = EnsureGroup(result);
            deltaGroup = EnsureGroup(delta);
        }

        public void Prepare(OnlineDuelResultKind kind, RankChangeReceipt receipt)
        {
            if (result != null)
            {
                result.text = kind == OnlineDuelResultKind.Victory
                    ? "VITÓRIA"
                    : kind == OnlineDuelResultKind.Defeat
                        ? "DERROTA"
                        : kind == OnlineDuelResultKind.Draw
                            ? "EMPATE"
                            : "PARTIDA ENCERRADA";
                result.color = kind == OnlineDuelResultKind.Victory
                    ? new Color(0.65f, 1f, 0.15f, 1f)
                    : kind == OnlineDuelResultKind.Defeat
                        ? new Color(1f, 0.30f, 0.38f, 1f)
                        : new Color(1f, 0.78f, 0.20f, 1f);
            }
            if (delta != null)
            {
                delta.text = receipt.delta > 0
                    ? $"+{receipt.delta} PE"
                    : $"{receipt.delta} PE";
                delta.color = receipt.delta > 0
                    ? new Color(0.65f, 1f, 0.15f, 1f)
                    : receipt.delta < 0
                        ? new Color(1f, 0.38f, 0.38f, 1f)
                        : new Color(0.70f, 0.82f, 0.92f, 1f);
            }
            SetTransition(string.Empty, Color.white);
            SetResultAlpha(0f);
            SetDeltaAlpha(0f);
        }

        public void SetResultAlpha(float alpha)
        {
            SetAlpha(resultGroup, result, alpha, 0.95f);
        }

        public void SetDeltaAlpha(float alpha)
        {
            SetAlpha(deltaGroup, delta, alpha, 0.88f);
        }

        public void SetDeltaMotion(float intensity, bool positive)
        {
            if (delta == null)
                return;
            float pulse = Mathf.Clamp01(intensity);
            float scale = 1f + pulse * 0.14f;
            delta.rectTransform.localScale = new Vector3(scale, scale, 1f);
            Color baseColor = positive
                ? new Color(0.65f, 1f, 0.15f, 1f)
                : new Color(1f, 0.38f, 0.38f, 1f);
            delta.color = Color.Lerp(baseColor, Color.white, pulse * 0.45f);
        }

        public void SetTransition(string text, Color color)
        {
            if (transition == null)
                return;
            transition.text = text ?? string.Empty;
            transition.color = color;
        }

        private static CanvasGroup EnsureGroup(Graphic graphic)
        {
            if (graphic == null)
                return null;
            return graphic.GetComponent<CanvasGroup>() ??
                   graphic.gameObject.AddComponent<CanvasGroup>();
        }

        private static void SetAlpha(
            CanvasGroup group,
            Graphic graphic,
            float alpha,
            float initialScale)
        {
            float clamped = Mathf.Clamp01(alpha);
            if (group != null)
                group.alpha = clamped;
            if (graphic != null)
            {
                float eased = Mathf.SmoothStep(0f, 1f, clamped);
                graphic.rectTransform.localScale = Vector3.one *
                    Mathf.Lerp(initialScale, 1f, eased);
            }
        }
    }

    /// <summary>
    /// Pequena cena em tempo real para a troca de patente. Ela usa os emblemas
    /// aprovados pelo catálogo, mas os apresenta sobre uma medalha, aro e
    /// fragmentos tridimensionais renderizados em uma textura própria. Dessa
    /// forma o resultado mantém o acabamento de uma cena 3D sem depender de
    /// uma câmera da arena, de resolução ou de orientação da tela.
    /// </summary>
    public sealed class RankPromotionCinematic : MonoBehaviour
    {
        private const int CinematicLayer = 5; // UI: isolado da câmera da arena.

        private sealed class BadgeModel
        {
            public Transform root;
            public SpriteRenderer badge;
            public Renderer[] body;
            public Material metal;
            public Material glow;
        }

        private readonly List<Material> runtimeMaterials = new List<Material>();
        private readonly List<Transform> energyFragments = new List<Transform>();
        private readonly List<Vector3> fragmentOrigins = new List<Vector3>();
        private RawImage viewport;
        private CanvasGroup viewportGroup;
        private RenderTexture renderTexture;
        private GameObject sceneRoot;
        private Transform stage;
        private Camera renderCamera;
        private Light keyLight;
        private BadgeModel outgoing;
        private BadgeModel incoming;
        private Material fragmentMaterial;
        private bool skipRequested;

        public void Initialize(RawImage output)
        {
            viewport = output;
            if (viewport != null)
            {
                viewport.raycastTarget = false;
                viewport.color = Color.white;
            }
            if (EnsureViewportGroup())
                viewportGroup.alpha = 0f;
        }

        public IEnumerator Play(
            RankTier oldTier,
            RankTier newTier,
            bool promotion)
        {
            if (viewport == null || oldTier == newTier ||
                !EnsureViewportGroup())
                yield break;

            EnsureScene();
            if (renderCamera == null || stage == null)
                yield break;

            Color outgoingColor = TierColor(oldTier);
            Color incomingColor = TierColor(newTier);
            ConfigureModel(outgoing, oldTier, outgoingColor);
            ConfigureModel(incoming, newTier, incomingColor);
            PrepareTransition(promotion, incomingColor);

            float duration = promotion ? 1.72f : 1.34f;
            for (float elapsed = 0f; elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                if (skipRequested)
                {
                    Hide();
                    yield break;
                }
                float time = Mathf.Clamp01(elapsed / duration);
                UpdateTransition(time, promotion, incomingColor);
                yield return null;
            }

            UpdateTransition(1f, promotion, incomingColor);
            yield return new WaitForSecondsRealtime(promotion ? 0.16f : 0.08f);
            Hide();
        }

        public void Hide()
        {
            if (EnsureViewportGroup())
                viewportGroup.alpha = 0f;
            if (stage != null)
                stage.gameObject.SetActive(false);
        }

        public void Skip()
        {
            skipRequested = true;
            Hide();
        }

        private bool EnsureViewportGroup()
        {
            if (viewport == null)
                return false;
            if (viewportGroup == null)
            {
                viewportGroup = viewport.GetComponent<CanvasGroup>();
                if (viewportGroup == null)
                    viewportGroup = viewport.gameObject.AddComponent<CanvasGroup>();
            }
            if (viewportGroup == null)
                return false;
            viewportGroup.blocksRaycasts = false;
            viewportGroup.interactable = false;
            return true;
        }

        public void ResetSequence()
        {
            skipRequested = false;
            Hide();
        }

        private void EnsureScene()
        {
            if (sceneRoot != null)
                return;

            sceneRoot = new GameObject("RankPromotionCinematic3D");
            sceneRoot.transform.SetParent(transform, false);
            sceneRoot.layer = CinematicLayer;

            GameObject cameraObject = new GameObject(
                "RankCinematicCamera",
                typeof(Camera));
            cameraObject.transform.SetParent(sceneRoot.transform, false);
            cameraObject.layer = CinematicLayer;
            renderCamera = cameraObject.GetComponent<Camera>();
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = Color.clear;
            renderCamera.cullingMask = 1 << CinematicLayer;
            renderCamera.fieldOfView = 31f;
            renderCamera.nearClipPlane = 0.05f;
            renderCamera.farClipPlane = 32f;
            renderCamera.allowHDR = false;
            renderCamera.allowMSAA = false;
            renderCamera.transform.localPosition = new Vector3(0f, 0.04f, -7.35f);
            renderCamera.transform.localRotation = Quaternion.identity;

            renderTexture = new RenderTexture(
                512,
                512,
                16,
                RenderTextureFormat.ARGB32)
            {
                name = "Rank Promotion Cinematic Target",
                filterMode = FilterMode.Bilinear
            };
            renderTexture.Create();
            renderCamera.targetTexture = renderTexture;
            viewport.texture = renderTexture;

            GameObject stageObject = new GameObject("RankPromotionStage");
            stageObject.transform.SetParent(sceneRoot.transform, false);
            stageObject.layer = CinematicLayer;
            stage = stageObject.transform;

            CreateLight(
                "Key Light",
                new Color(0.50f, 0.84f, 1f, 1f),
                1.35f,
                new Vector3(-2.4f, 2.8f, -3.1f));
            CreateLight(
                "Rim Light",
                new Color(1f, 0.63f, 0.20f, 1f),
                0.90f,
                new Vector3(2.1f, 0.8f, -2.1f));

            outgoing = CreateBadgeModel("Elo Atual 3D");
            incoming = CreateBadgeModel("Novo Elo 3D");
            CreateEnergyFragments();
            stage.gameObject.SetActive(false);
        }

        private void CreateLight(
            string name,
            Color color,
            float intensity,
            Vector3 position)
        {
            GameObject lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(sceneRoot.transform, false);
            lightObject.transform.localPosition = position;
            lightObject.layer = CinematicLayer;
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 8.5f;
            light.intensity = intensity;
            light.color = color;
            if (keyLight == null)
                keyLight = light;
        }

        private BadgeModel CreateBadgeModel(string name)
        {
            GameObject rootObject = new GameObject(name);
            rootObject.transform.SetParent(stage, false);
            rootObject.layer = CinematicLayer;
            Transform root = rootObject.transform;

            Material metal = CreateMaterial(
                name + " Metal",
                new Color(0.20f, 0.72f, 1f, 1f),
                false);
            Material glow = CreateMaterial(
                name + " Aura",
                new Color(0.36f, 0.88f, 1f, 1f),
                true);

            var renderers = new List<Renderer>();
            CreateMedallionPart(
                root,
                "Base octogonal",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0f, 0.30f),
                new Vector3(1.32f, 0.13f, 1.32f),
                Quaternion.Euler(90f, 0f, 0f),
                metal,
                renderers);
            CreateMedallionPart(
                root,
                "Aro luminoso",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0f, 0.14f),
                new Vector3(1.42f, 0.06f, 1.42f),
                Quaternion.Euler(90f, 0f, 0f),
                glow,
                renderers);
            CreateMedallionPart(
                root,
                "Núcleo escuro",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0f, -0.01f),
                new Vector3(1.16f, 0.075f, 1.16f),
                Quaternion.Euler(90f, 0f, 0f),
                CreateMaterial(
                    name + " Núcleo",
                    new Color(0.014f, 0.040f, 0.085f, 1f),
                    false),
                renderers);

            for (int index = 0; index < 8; index++)
            {
                float angle = index * 45f * Mathf.Deg2Rad;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * 1.48f,
                    Mathf.Sin(angle) * 1.48f,
                    0.11f);
                CreateMedallionPart(
                    root,
                    "Fragmento do aro",
                    PrimitiveType.Cube,
                    position,
                    new Vector3(0.075f, 0.20f, 0.08f),
                    Quaternion.Euler(0f, 0f, index * 45f - 18f),
                    glow,
                    renderers);
            }

            GameObject badgeObject = new GameObject(
                "Emblema de elo",
                typeof(SpriteRenderer));
            badgeObject.transform.SetParent(root, false);
            badgeObject.transform.localPosition = new Vector3(0f, 0f, -0.16f);
            badgeObject.transform.localScale = Vector3.one * 1.55f;
            badgeObject.layer = CinematicLayer;
            SpriteRenderer badge = badgeObject.GetComponent<SpriteRenderer>();
            badge.sortingOrder = 3;
            badge.color = Color.white;

            return new BadgeModel
            {
                root = root,
                badge = badge,
                body = renderers.ToArray(),
                metal = metal,
                glow = glow
            };
        }

        private void CreateMedallionPart(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Material material,
            List<Renderer> renderers)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.layer = CinematicLayer;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
        }

        private void CreateEnergyFragments()
        {
            fragmentMaterial = CreateMaterial(
                "Partículas da promoção de elo",
                new Color(0.55f, 0.92f, 1f, 1f),
                true);
            for (int index = 0; index < 18; index++)
            {
                GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fragment.name = "Fragmento de energia";
                fragment.transform.SetParent(stage, false);
                fragment.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
                fragment.layer = CinematicLayer;
                Collider collider = fragment.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
                fragment.GetComponent<Renderer>().sharedMaterial = fragmentMaterial;
                energyFragments.Add(fragment.transform);
                float angle = index / 18f * Mathf.PI * 2f;
                fragmentOrigins.Add(new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f));
            }
        }

        private void ConfigureModel(
            BadgeModel model,
            RankTier tier,
            Color accent)
        {
            if (model == null)
                return;
            model.root.gameObject.SetActive(true);
            if (model.badge != null)
                model.badge.sprite = RankBadgeCatalog.Get(tier);
            SetMaterialColor(model.metal, Color.Lerp(
                new Color(0.06f, 0.10f, 0.16f, 1f), accent, 0.58f), false);
            SetMaterialColor(model.glow, Color.Lerp(Color.white, accent, 0.72f), true);
        }

        private void PrepareTransition(bool promotion, Color accent)
        {
            stage.gameObject.SetActive(true);
            if (EnsureViewportGroup())
                viewportGroup.alpha = 0f;
            stage.localRotation = Quaternion.Euler(0f, 0f, 0f);
            renderCamera.transform.localPosition = new Vector3(0f, 0.04f, -7.85f);
            outgoing.root.localPosition = Vector3.zero;
            outgoing.root.localRotation = Quaternion.identity;
            outgoing.root.localScale = Vector3.one;
            incoming.root.localPosition = new Vector3(
                0f,
                promotion ? -1.65f : 1.65f,
                0.18f);
            incoming.root.localRotation = Quaternion.Euler(
                promotion ? 24f : -24f,
                promotion ? -145f : 145f,
                0f);
            incoming.root.localScale = Vector3.one * 0.24f;
            SetModelOpacity(outgoing, 1f);
            SetModelOpacity(incoming, 0f);
            SetMaterialColor(fragmentMaterial, accent, true);
            foreach (Transform fragment in energyFragments)
                fragment.gameObject.SetActive(true);
        }

        private void UpdateTransition(float time, bool promotion, Color accent)
        {
            float entry = SmoothRange(time, 0f, 0.18f);
            float handoff = SmoothRange(time, 0.28f, 0.62f);
            float settle = SmoothRange(time, 0.62f, 1f);
            float direction = promotion ? 1f : -1f;
            float cinematicArc = Mathf.Sin(
                Mathf.SmoothStep(0f, 1f, time) * Mathf.PI);

            if (EnsureViewportGroup())
            {
                viewportGroup.alpha = Mathf.Clamp01(
                    Mathf.Min(entry * 1.35f, (1f - settle) * 1.85f));
            }
            stage.localRotation = Quaternion.Euler(
                cinematicArc * 7f,
                Mathf.Lerp(-15f * direction, 12f * direction, handoff),
                Mathf.Sin(time * Mathf.PI * 2f) * 4f);
            renderCamera.transform.localPosition = new Vector3(
                0f,
                0.04f,
                Mathf.Lerp(-7.85f, -5.72f, cinematicArc));

            outgoing.root.localPosition = new Vector3(
                0f,
                Mathf.Lerp(0f, 1.86f * direction, handoff),
                Mathf.Lerp(0f, 0.68f, handoff));
            outgoing.root.localScale = Vector3.one * Mathf.Lerp(1f, 1.58f, handoff);
            outgoing.root.localRotation = Quaternion.Euler(
                7f * Mathf.Sin(time * Mathf.PI),
                Mathf.Lerp(0f, -92f * direction, handoff),
                0f);
            SetModelOpacity(outgoing, 1f - handoff);

            incoming.root.localPosition = Vector3.Lerp(
                new Vector3(0f, -1.65f * direction, 0.18f),
                Vector3.zero,
                handoff);
            incoming.root.localScale = Vector3.one * Mathf.Lerp(0.24f, 1f, handoff);
            incoming.root.localRotation = Quaternion.Euler(
                Mathf.Lerp(24f * direction, 0f, handoff),
                Mathf.Lerp(-145f * direction, 0f, handoff),
                0f);
            SetModelOpacity(incoming, handoff);

            if (keyLight != null)
                keyLight.intensity = Mathf.Lerp(0.95f, 2.15f, cinematicArc);
            for (int index = 0; index < energyFragments.Count; index++)
            {
                Transform fragment = energyFragments[index];
                Vector3 origin = fragmentOrigins[index];
                float angle = time * Mathf.PI * 2f * 1.7f + index * 0.42f;
                float radius = Mathf.Lerp(1.85f, 0.48f, handoff) +
                    Mathf.Sin(time * Mathf.PI * 3f + index) * 0.11f;
                fragment.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0.16f + origin.y * 0.09f);
                fragment.localRotation = Quaternion.Euler(
                    time * 480f + index * 19f,
                    time * 260f + index * 31f,
                    time * 610f + index * 13f);
                float scale = Mathf.Lerp(0.45f, 1.25f, cinematicArc);
                fragment.localScale = Vector3.one * 0.075f * scale;
            }
            SetMaterialColor(fragmentMaterial, Color.Lerp(
                Color.white,
                accent,
                0.65f + 0.25f * cinematicArc), true);
        }

        private static float SmoothRange(float value, float start, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));
        }

        private static void SetModelOpacity(BadgeModel model, float opacity)
        {
            if (model == null)
                return;
            float alpha = Mathf.Clamp01(opacity);
            if (model.badge != null)
                model.badge.color = new Color(1f, 1f, 1f, alpha);
            if (model.body == null)
                return;
            bool visible = alpha > 0.025f;
            foreach (Renderer renderer in model.body)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        private Material CreateMaterial(string name, Color color, bool emissive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Sprites/Default");
            if (shader == null)
                return null;
            Material material = new Material(shader) { name = name };
            runtimeMaterials.Add(material);
            SetMaterialColor(material, color, emissive);
            return material;
        }

        private static void SetMaterialColor(
            Material material,
            Color color,
            bool emissive)
        {
            if (material == null)
                return;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.76f);
            }
        }

        private static Color TierColor(RankTier tier)
        {
            return tier switch
            {
                RankTier.Wood => new Color(0.66f, 0.39f, 0.18f, 1f),
                RankTier.Stone => new Color(0.58f, 0.67f, 0.72f, 1f),
                RankTier.Iron => new Color(0.42f, 0.52f, 0.62f, 1f),
                RankTier.Bronze => new Color(0.88f, 0.46f, 0.20f, 1f),
                RankTier.Silver => new Color(0.72f, 0.86f, 0.95f, 1f),
                RankTier.Gold => new Color(1f, 0.74f, 0.22f, 1f),
                RankTier.Platinum => new Color(0.30f, 0.94f, 0.83f, 1f),
                RankTier.Diamond => new Color(0.34f, 0.68f, 1f, 1f),
                RankTier.GrandMaster => new Color(0.92f, 0.30f, 0.76f, 1f),
                _ => new Color(0.36f, 0.84f, 1f, 1f)
            };
        }

        private void OnDestroy()
        {
            if (viewport != null && viewport.texture == renderTexture)
                viewport.texture = null;
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            foreach (Material material in runtimeMaterials)
            {
                if (material != null)
                    Destroy(material);
            }
            runtimeMaterials.Clear();
            if (sceneRoot != null)
                Destroy(sceneRoot);
        }
    }

    public sealed class RankTransitionAnimator : MonoBehaviour
    {
        private RankPointsBarView bar;
        private RankEmblemView emblem;
        private RankSideSlotView sides;
        private RankResultBanner banner;
        private RankPromotionCinematic cinematic;
        private Button leaveButton;
        private Button skipButton;
        private RankChangeReceipt receipt;
        private Action completed;
        private Coroutine routine;
        private bool skipRequested;

        public void Initialize(
            RankPointsBarView pointsBar,
            RankEmblemView emblemView,
            RankSideSlotView sideSlots,
            RankResultBanner resultBanner,
            RankPromotionCinematic rankCinematic,
            Button returnButton,
            Button controlledSkipButton)
        {
            bar = pointsBar;
            emblem = emblemView;
            sides = sideSlots;
            banner = resultBanner;
            cinematic = rankCinematic;
            leaveButton = returnButton;
            skipButton = controlledSkipButton;
        }

        public void Play(
            OnlineDuelResultKind kind,
            RankChangeReceipt committedReceipt,
            Action onCompleted)
        {
            if (routine != null)
                StopCoroutine(routine);
            receipt = committedReceipt;
            completed = onCompleted;
            skipRequested = false;
            if (leaveButton != null)
                leaveButton.interactable = false;
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.interactable = true;
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(SkipToFinal);
            }
            cinematic?.ResetSequence();
            emblem.SetTier(receipt.oldTier);
            sides.SetTier(receipt.oldTier);
            bar.SetState(receipt.oldPoints);
            banner.Prepare(kind, receipt);
            routine = StartCoroutine(Sequence());
        }

        public void SkipToFinal()
        {
            skipRequested = true;
            cinematic?.Skip();
        }

        private IEnumerator Sequence()
        {
            yield return FadeResult(0.32f);
            if (FinishIfSkipped())
                yield break;
            yield return Wait(0.12f);
            if (FinishIfSkipped())
                yield break;
            yield return FadeDelta(0.42f);
            if (FinishIfSkipped())
                yield break;
            yield return Wait(0.25f);
            if (FinishIfSkipped())
                yield break;

            if (receipt.delta > 0)
                yield return AnimateUp();
            else if (receipt.delta < 0)
                yield return AnimateDown();

            if (FinishIfSkipped())
                yield break;
            if (receipt.shieldPreventedDemotion)
            {
                banner.SetTransition(
                    "PROTEGIDO NESTA DERROTA",
                    new Color(0.35f, 0.90f, 1f, 1f));
                yield return Wait(0.75f);
            }
            ApplyFinalState();
        }

        private IEnumerator AnimateUp()
        {
            int cursor = receipt.oldPoints;
            RankTier visualTier = receipt.oldTier;
            while (cursor < receipt.newPoints)
            {
                RankDefinition definition = RankRules.Definition(visualTier);
                int nextFloor = definition.Maximum + 1;
                int segmentEnd = Mathf.Min(receipt.newPoints, nextFloor);
                float start = (cursor - definition.Minimum) / 25f;
                float end = segmentEnd == nextFloor
                    ? 1f
                    : (segmentEnd - definition.Minimum) / 25f;
                yield return AnimateBar(
                    visualTier,
                    cursor,
                    segmentEnd,
                    start,
                    end);
                cursor = segmentEnd;
                if (cursor != nextFloor || visualTier >= receipt.newTier)
                    continue;

                RankTier promoted = (RankTier)((int)visualTier + 1);
                banner.SetTransition(
                    "PROMOÇÃO · ELO EM ASCENSÃO",
                    new Color(0.65f, 1f, 0.22f, 1f));
                yield return Wait(0.18f);
                yield return PresentTierChange(
                    visualTier,
                    promoted,
                    true);
                sides.SetTier(promoted);
                bar.SetVisual(promoted, 0f, cursor);
                visualTier = promoted;
                banner.SetTransition(string.Empty, Color.white);
            }
        }

        private IEnumerator AnimateDown()
        {
            int cursor = receipt.oldPoints;
            RankTier visualTier = receipt.oldTier;
            while (cursor > receipt.newPoints)
            {
                RankDefinition definition = RankRules.Definition(visualTier);
                int floor = definition.Minimum;
                int segmentEnd = Mathf.Max(receipt.newPoints, floor);
                float start = (cursor - floor) / 25f;
                float end = (segmentEnd - floor) / 25f;
                yield return AnimateBar(
                    visualTier,
                    cursor,
                    segmentEnd,
                    start,
                    end);
                cursor = segmentEnd;
                if (receipt.newPoints >= floor || visualTier <= receipt.newTier)
                    break;

                RankTier demoted = (RankTier)((int)visualTier - 1);
                banner.SetTransition(
                    "REBAIXAMENTO · ELO REAJUSTADO",
                    new Color(1f, 0.38f, 0.38f, 1f));
                yield return Wait(0.18f);
                yield return PresentTierChange(
                    visualTier,
                    demoted,
                    false);
                sides.SetTier(demoted);
                cursor = floor - 1;
                bar.SetVisual(demoted, 1f, cursor);
                visualTier = demoted;
                banner.SetTransition(string.Empty, Color.white);
            }
        }

        private IEnumerator AnimateBar(
            RankTier tier,
            int fromPoints,
            int toPoints,
            float from,
            float to)
        {
            int direction = Math.Sign(toPoints - fromPoints);
            int pointCount = Math.Abs(toPoints - fromPoints);
            if (direction == 0 || pointCount == 0)
            {
                bar.SetVisual(tier, to, toPoints);
                yield break;
            }

            const float secondsPerPoint = 0.095f;
            bool positive = direction > 0;
            for (int index = 0; index < pointCount; index++)
            {
                int currentPoints = fromPoints + direction * index;
                int nextPoints = currentPoints + direction;
                float startProgress = Mathf.Lerp(
                    from,
                    to,
                    index / (float)pointCount);
                float endProgress = Mathf.Lerp(
                    from,
                    to,
                    (index + 1) / (float)pointCount);
                bar.SetVisual(tier, startProgress, currentPoints);
                for (float elapsed = 0f; elapsed < secondsPerPoint;
                     elapsed += Time.unscaledDeltaTime)
                {
                    if (skipRequested)
                        yield break;
                    float t = EaseInOutCubic(elapsed / secondsPerPoint);
                    bar.SetVisual(
                        tier,
                        Mathf.Lerp(startProgress, endProgress, t),
                        currentPoints);
                    float pulse = Mathf.Sin(t * Mathf.PI);
                    bar.SetMotionEnergy(pulse, positive);
                    banner.SetDeltaMotion(pulse, positive);
                    yield return null;
                }
                bar.SetVisual(tier, endProgress, nextPoints);
            }
            bar.SetVisual(tier, to, toPoints);
            bar.ClearMotionEnergy();
            banner.SetDeltaMotion(0f, positive);
        }

        private IEnumerator PresentTierChange(
            RankTier from,
            RankTier to,
            bool promotion)
        {
            if (cinematic == null)
            {
                yield return emblem.SwapTo(to, promotion ? 0.58f : 0.52f);
                yield break;
            }

            yield return emblem.SetCinematicVisibility(false, 0.13f);
            yield return cinematic.Play(from, to, promotion);
            emblem.SetTier(to);
            yield return emblem.SetCinematicVisibility(true, 0.20f);
        }

        private IEnumerator FadeResult(float duration)
        {
            for (float elapsed = 0f; elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                if (skipRequested)
                    yield break;
                banner.SetResultAlpha(elapsed / duration);
                yield return null;
            }
            banner.SetResultAlpha(1f);
        }

        private IEnumerator FadeDelta(float duration)
        {
            for (float elapsed = 0f; elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                if (skipRequested)
                    yield break;
                banner.SetDeltaAlpha(elapsed / duration);
                yield return null;
            }
            banner.SetDeltaAlpha(1f);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        private static float EaseInOutCubic(float value)
        {
            float t = Mathf.Clamp01(value);
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private bool FinishIfSkipped()
        {
            if (!skipRequested)
                return false;
            ApplyFinalState();
            return true;
        }

        private void ApplyFinalState()
        {
            banner.SetResultAlpha(1f);
            banner.SetDeltaAlpha(1f);
            banner.SetDeltaMotion(0f, receipt.delta >= 0);
            bar.ClearMotionEnergy();
            cinematic?.Hide();
            emblem.SetTier(receipt.newTier);
            sides.SetTier(receipt.newTier);
            bar.SetState(receipt.newPoints);
            if (receipt.shieldPreventedDemotion)
            {
                banner.SetTransition(
                    "PROTEGIDO · ESCUDO CONSUMIDO",
                    new Color(0.35f, 0.90f, 1f, 1f));
            }
            else if (receipt.promoted)
            {
                banner.SetTransition(
                    "PROMOÇÃO CONCLUÍDA",
                    new Color(0.65f, 1f, 0.22f, 1f));
            }
            else if (receipt.demoted)
            {
                banner.SetTransition(
                    "NOVO ELO",
                    new Color(1f, 0.55f, 0.28f, 1f));
            }
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
            if (leaveButton != null)
                leaveButton.interactable = true;
            routine = null;
            Action callback = completed;
            completed = null;
            callback?.Invoke();
        }
    }
}
