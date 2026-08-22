using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private enum PackOpeningPresentationState
        {
            Idle,
            Fade,
            PackEnter,
            Anticipation,
            Tear,
            FlapOpen,
            Burst,
            StackRise,
            FanOut,
            Settle,
            RevealReady
        }

        [Header("Loja - animação de abertura do pacote")]
        [SerializeField]
        private bool packOpeningAnimationEnabled = true;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningFadeDuration = 0.22f;
        [SerializeField, Range(0.05f, 1.5f)]
        private float packOpeningEnterDuration = 0.62f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningAnticipationDuration = 0.30f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningTearDuration = 0.52f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningFlapDuration = 0.48f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningBurstDuration = 0.34f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningStackRiseDuration = 0.50f;
        [SerializeField, Range(0.10f, 1.5f)]
        private float packOpeningFanDuration = 0.72f;
        [SerializeField, Range(0f, 0.20f)]
        private float packOpeningCardStagger = 0.18f;
        [SerializeField, Range(0.05f, 0.75f)]
        private float packOpeningSettleDuration = 0.18f;

        private sealed class PackOpeningAnimationCard
        {
            public RectTransform Rect;
            public Button Button;
            public CanvasGroup CanvasGroup;
            public RectTransform MotionTrail;
            public CanvasGroup MotionTrailGroup;
            public RectTransform LandingGlow;
            public CanvasGroup LandingGlowGroup;
            public Vector2 FinalMin;
            public Vector2 FinalMax;
            public Vector2 StackOffset;
            public Vector2 ApexOffset;
            public Vector2 ApproachOffset;
            public float LaunchRotation;
            public int LaunchOrder;
            public bool ReleasedToFront;
        }

        private sealed class PackOpeningParticle
        {
            public RectTransform Rect;
            public CanvasGroup CanvasGroup;
            public Vector2 Start;
            public Vector2 End;
            public float StartRotation;
            public float RotationSpeed;
        }

        private sealed class PackOpeningAnimationView
        {
            public RectTransform Layer;
            public RectTransform RearGlow;
            public CanvasGroup RearGlowGroup;
            public RectTransform OuterGlow;
            public CanvasGroup OuterGlowGroup;
            public RectTransform ReleaseBeam;
            public CanvasGroup ReleaseBeamGroup;
            public RectTransform PackRoot;
            public CanvasGroup PackGroup;
            public Image InnerDark;
            public RectTransform LeftFlap;
            public RectTransform RightFlap;
            public RectTransform FrontLip;
            public CanvasGroup FrontLipGroup;
            public RectTransform TearGlow;
            public CanvasGroup TearGlowGroup;
            public ArcanePackTearGraphic TearGraphic;
            public RectTransform SkipButton;
            public Text RevealInstruction;
            public Vector2 PackBasePosition;
            public Vector2 PackEnterOffset;
            public Vector2 FrontLipBasePosition;
            public Vector2 TearBasePosition;
            public float PackWidth;
            public float PackHeight;
            public readonly List<PackOpeningAnimationCard> Cards = new();
            public readonly List<PackOpeningParticle> Particles = new();
        }

        private Coroutine _packOpeningSequenceRoutine;
        private PackOpeningAnimationView _activePackOpeningView;
        private PackOpeningPresentationState _packOpeningPresentationState;
        private bool _packOpeningSequenceActive;
        private bool _packOpeningSkipRequested;
        private Texture2D _packOpeningGlowTexture;
        private Sprite _packOpeningGlowSprite;

        public bool IsPackOpeningPresentationActive =>
            _packOpeningSequenceActive;
        public string PackOpeningPresentationStateName =>
            _packOpeningPresentationState.ToString();

        private void StartPackOpeningPresentation(
            PendingPackOpeningRecord opening)
        {
            if (opening == null || _packOpeningSequenceActive)
                return;

            _packOpeningStarted = true;
            ShowPackOpening(opening, packOpeningAnimationEnabled);
        }

        private PackOpeningAnimationView CreatePackOpeningAnimationView(
            ShopPackDefinition pack)
        {
            Image blocker = CreatePanel(
                _screenRoot,
                "Animação de Abertura do Pacote",
                Vector2.zero,
                Vector2.one,
                Color.clear);
            blocker.raycastTarget = true;

            // A abertura se move continuamente. Um Canvas aninhado impede
            // que cada transformação das cartas invalide toda a tela da loja.
            Canvas motionCanvas = blocker.gameObject.AddComponent<Canvas>();
            motionCanvas.overrideSorting = false;
            blocker.gameObject.AddComponent<GraphicRaycaster>();

            var view = new PackOpeningAnimationView
            {
                Layer = blocker.rectTransform
            };

            // O cenário da loja já funciona como fundo cinematográfico da
            // abertura. Não adicionamos películas de tela inteira: além de
            // esconder seus detalhes, a combinação de escurecimento e clarão
            // dourado produzia um fundo amarelado translúcido.

            Image outerGlow = CreatePanel(
                view.Layer,
                "Aura Exterior do Pacote",
                new Vector2(0.19f, 0.02f),
                new Vector2(0.81f, 0.96f),
                new Color(Gold.r, Gold.g, Gold.b, 0f));
            outerGlow.sprite = ResolvePackOpeningGlowSprite();
            outerGlow.preserveAspect = true;
            outerGlow.raycastTarget = false;
            view.OuterGlow = outerGlow.rectTransform;
            view.OuterGlowGroup = AddPackOpeningCanvasGroup(
                outerGlow.gameObject,
                0f);

            Image releaseBeam = CreatePanel(
                view.Layer,
                "Feixe de Liberação das Cartas",
                new Vector2(0.39f, 0.27f),
                new Vector2(0.61f, 0.94f),
                new Color(1f, 0.88f, 0.48f, 0f));
            releaseBeam.sprite = ResolvePackOpeningGlowSprite();
            releaseBeam.preserveAspect = false;
            releaseBeam.raycastTarget = false;
            view.ReleaseBeam = releaseBeam.rectTransform;
            view.ReleaseBeamGroup = AddPackOpeningCanvasGroup(
                releaseBeam.gameObject,
                0f);

            Image rearGlow = CreatePanel(
                view.Layer,
                "Clarão Traseiro do Pacote",
                new Vector2(0.30f, 0.12f),
                new Vector2(0.70f, 0.84f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
            rearGlow.sprite = ResolvePackOpeningGlowSprite();
            rearGlow.preserveAspect = true;
            rearGlow.raycastTarget = false;
            view.RearGlow = rearGlow.rectTransform;
            view.RearGlowGroup = AddPackOpeningCanvasGroup(rearGlow.gameObject, 0f);

            Sprite booster = ResolveShopBoosterPackSprite();
            float canvasHeight = _screenRoot != null
                ? Mathf.Max(540f, _screenRoot.rect.height)
                : Mathf.Max(540f, Screen.height);
            view.PackHeight = Mathf.Clamp(canvasHeight * 0.54f, 320f, 690f);
            float aspect = booster != null && booster.rect.height > 0f
                ? booster.rect.width / booster.rect.height
                : 0.67f;
            view.PackWidth = view.PackHeight * Mathf.Clamp(aspect, 0.52f, 0.82f);
            view.PackBasePosition = new Vector2(0f, -canvasHeight * 0.015f);
            view.PackEnterOffset = new Vector2(0f, -canvasHeight * 0.29f);

            view.PackRoot = CreatePackOpeningSizedRect(
                view.Layer,
                "Pacote em Camadas",
                view.PackBasePosition,
                new Vector2(view.PackWidth, view.PackHeight));
            view.PackGroup = AddPackOpeningCanvasGroup(
                view.PackRoot.gameObject,
                0f);

            RawImage body = CreatePackOpeningSlice(
                view.PackRoot,
                "Corpo Frontal do Pacote",
                booster,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.83f),
                new Rect(0f, 0f, 1f, 0.83f));
            body.raycastTarget = false;

            view.InnerDark = CreatePanel(
                view.PackRoot,
                "Interior Escuro do Pacote",
                new Vector2(0.045f, 0.765f),
                new Vector2(0.955f, 0.845f),
                new Color(0.002f, 0.008f, 0.025f, 0f));
            view.InnerDark.raycastTarget = false;

            RawImage leftFlap = CreatePackOpeningSlice(
                view.PackRoot,
                "Aba Superior Esquerda",
                booster,
                new Vector2(0f, 0.81f),
                new Vector2(0.52f, 1f),
                new Rect(0f, 0.81f, 0.52f, 0.19f));
            leftFlap.raycastTarget = false;
            view.LeftFlap = leftFlap.rectTransform;
            view.LeftFlap.pivot = new Vector2(0.96f, 0.08f);

            RawImage rightFlap = CreatePackOpeningSlice(
                view.PackRoot,
                "Aba Superior Direita",
                booster,
                new Vector2(0.48f, 0.81f),
                new Vector2(1f, 1f),
                new Rect(0.48f, 0.81f, 0.52f, 0.19f));
            rightFlap.raycastTarget = false;
            view.RightFlap = rightFlap.rectTransform;
            view.RightFlap.pivot = new Vector2(0.04f, 0.08f);

            view.FrontLipBasePosition = view.PackBasePosition +
                new Vector2(0f, view.PackHeight * 0.255f);
            RawImage frontLip = CreatePackOpeningSizedSlice(
                view.Layer,
                "Borda Frontal do Pacote",
                booster,
                view.FrontLipBasePosition,
                new Vector2(view.PackWidth, view.PackHeight * 0.15f),
                new Rect(0f, 0.66f, 1f, 0.17f));
            frontLip.raycastTarget = false;
            view.FrontLip = frontLip.rectTransform;
            view.FrontLipGroup = AddPackOpeningCanvasGroup(
                frontLip.gameObject,
                0f);

            view.TearBasePosition = view.PackBasePosition +
                new Vector2(0f, view.PackHeight * 0.325f);
            RectTransform tearRect = CreatePackOpeningSizedRect(
                view.Layer,
                "Rasgo Procedural do Pacote",
                view.TearBasePosition,
                new Vector2(view.PackWidth * 0.94f, 38f));
            ArcanePackTearGraphic tear =
                tearRect.gameObject.AddComponent<ArcanePackTearGraphic>();
            tear.raycastTarget = false;
            tear.SetPalette(
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f),
                new Color(Gold.r, Gold.g, Gold.b, 0.74f),
                new Color(0.94f, 1f, 1f, 1f));
            tear.SetState(0f, 0f);
            view.TearGlow = tearRect;
            view.TearGraphic = tear;
            view.TearGlowGroup = AddPackOpeningCanvasGroup(
                tearRect.gameObject,
                0f);

            CreatePackOpeningParticles(view);

            Image skip = CreateButton(
                view.Layer,
                "PULAR ANIMAÇÃO",
                new Vector2(0.82f, 0.035f),
                new Vector2(0.965f, 0.095f),
                Cyan,
                RequestPackOpeningSkip);
            skip.color = new Color(0.01f, 0.06f, 0.09f, 0.88f);
            view.SkipButton = skip.rectTransform;

            SetPackOpeningCompositePose(
                view,
                0.54f,
                view.PackEnterOffset,
                -5.5f);
            return view;
        }

        private void RegisterPackOpeningCard(
            PackOpeningAnimationView view,
            Image card,
            Button button,
            Vector2 finalMin,
            Vector2 finalMax,
            int index)
        {
            if (view == null || card == null)
                return;

            float lane = index - 2f;
            Vector2 finalCenter = (finalMin + finalMax) * 0.5f;
            Vector2 layerSize = view.Layer.rect.size;
            if (layerSize.x < 32f || layerSize.y < 32f)
            {
                layerSize = new Vector2(
                    Mathf.Max(1280f, Screen.width),
                    Mathf.Max(720f, Screen.height));
            }
            // Converte a posicao real do rasgo (pixels ancorados no centro)
            // para coordenadas normalizadas. Antes as cartas partiam do meio
            // da tela, e nao da boca do pacote.
            var mouthCenter = new Vector2(
                0.5f + view.TearBasePosition.x / layerSize.x,
                0.5f + view.TearBasePosition.y / layerSize.y);
            var apexCenter = new Vector2(
                Mathf.Lerp(mouthCenter.x, finalCenter.x, 0.66f),
                Mathf.Min(0.89f,
                    mouthCenter.y + 0.19f - Mathf.Abs(lane) * 0.014f));
            var approachCenter = new Vector2(
                finalCenter.x - lane * 0.004f,
                finalCenter.y + 0.105f + Mathf.Abs(lane) * 0.008f);
            Vector2 stackOffset = Vector2.Scale(
                mouthCenter - finalCenter,
                layerSize);
            Vector2 apexOffset = Vector2.Scale(
                apexCenter - finalCenter,
                layerSize);
            Vector2 approachOffset = Vector2.Scale(
                approachCenter - finalCenter,
                layerSize);
            RectTransform rect = card.rectTransform;
            rect.anchorMin = finalMin;
            rect.anchorMax = finalMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            float launchRotation = lane * -8.5f;
            SetPackOpeningCardPerspectivePose(
                rect,
                stackOffset,
                new Vector2(0.20f, 0.31f),
                launchRotation);
            CanvasGroup group = AddPackOpeningCanvasGroup(card.gameObject, 0f);

            Image trail = CreatePanel(
                view.Layer,
                $"Rastro da Carta {index + 1}",
                finalMin,
                finalMax,
                new Color(0.36f, 0.92f, 1f, 0f));
            trail.sprite = ResolvePackOpeningGlowSprite();
            trail.preserveAspect = false;
            trail.raycastTarget = false;
            CanvasGroup trailGroup = AddPackOpeningCanvasGroup(
                trail.gameObject,
                0f);
            SetPackOpeningCardPerspectivePose(
                trail.rectTransform,
                stackOffset,
                new Vector2(0.10f, 0.72f),
                launchRotation);

            Image landingGlow = CreatePanel(
                view.Layer,
                $"Impacto da Carta {index + 1}",
                finalMin,
                finalMax,
                new Color(1f, 0.82f, 0.34f, 0f));
            landingGlow.sprite = ResolvePackOpeningGlowSprite();
            landingGlow.preserveAspect = true;
            landingGlow.raycastTarget = false;
            CanvasGroup landingGlowGroup = AddPackOpeningCanvasGroup(
                landingGlow.gameObject,
                0f);
            SetPackOpeningCardPerspectivePose(
                landingGlow.rectTransform,
                Vector2.zero,
                new Vector2(0.62f, 0.62f),
                0f);

            // O corpo do pacote precisa ocultar a origem da carta. Isso cria
            // a leitura de que ela saiu de dentro dele, em vez de uma imagem
            // simplesmente aparecer sobre outra.
            if (view.PackRoot != null)
            {
                trail.rectTransform.SetSiblingIndex(
                    view.PackRoot.GetSiblingIndex());
                landingGlow.rectTransform.SetSiblingIndex(
                    view.PackRoot.GetSiblingIndex());
                rect.SetSiblingIndex(view.PackRoot.GetSiblingIndex());
            }

            view.Cards.Add(new PackOpeningAnimationCard
            {
                Rect = rect,
                Button = button,
                CanvasGroup = group,
                MotionTrail = trail.rectTransform,
                MotionTrailGroup = trailGroup,
                LandingGlow = landingGlow.rectTransform,
                LandingGlowGroup = landingGlowGroup,
                FinalMin = finalMin,
                FinalMax = finalMax,
                StackOffset = stackOffset,
                ApexOffset = apexOffset,
                ApproachOffset = approachOffset,
                LaunchRotation = launchRotation,
                LaunchOrder = ResolvePackOpeningLaunchOrder(index)
            });
        }

        private void BeginPackOpeningPresentation(
            PackOpeningAnimationView view)
        {
            if (view == null || view.Layer == null)
            {
                _packOpeningSequenceActive = false;
                _packOpeningPresentationState =
                    PackOpeningPresentationState.RevealReady;
                return;
            }

            view.FrontLip?.SetAsLastSibling();
            view.TearGlow?.SetAsLastSibling();
            foreach (PackOpeningParticle particle in view.Particles)
                particle.Rect?.SetAsLastSibling();
            view.SkipButton?.SetAsLastSibling();

            _activePackOpeningView = view;
            _packOpeningSkipRequested = false;
            _packOpeningSequenceActive = true;
            _packOpeningPresentationState =
                PackOpeningPresentationState.Fade;
            _packOpeningSequenceRoutine = StartCoroutine(
                PlayPackOpeningPresentation(view));
        }

        private IEnumerator PlayPackOpeningPresentation(
            PackOpeningAnimationView view)
        {
            yield return AnimatePackOpeningPhase(
                packOpeningFadeDuration,
                progress =>
                {
                    float eased = EaseInOutSine(progress);
                    SetPackOpeningGroupAlpha(
                        view.OuterGlowGroup,
                        Mathf.Lerp(0f, 0.08f, eased));
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.PackEnter;
            yield return AnimatePackOpeningPhase(
                packOpeningEnterDuration,
                progress =>
                {
                    float eased = EaseOutQuint(progress);
                    float settle = EaseOutBack(progress, 0.035f);
                    float scale = Mathf.LerpUnclamped(0.54f, 1f, settle);
                    Vector2 offset = Vector2.LerpUnclamped(
                        view.PackEnterOffset,
                        Vector2.zero,
                        eased);
                    float rotation = Mathf.Lerp(-5.5f, 0f, eased);
                    SetPackOpeningCompositePose(
                        view,
                        scale,
                        offset,
                        rotation);
                    float visibility = Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01(progress * 1.65f));
                    SetPackOpeningGroupAlpha(view.PackGroup, visibility);
                    SetPackOpeningGroupAlpha(view.FrontLipGroup, visibility);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Sin(progress * Mathf.PI) * 0.20f);
                    SetPackOpeningGroupAlpha(
                        view.OuterGlowGroup,
                        Mathf.Lerp(0.08f, 0.16f, eased));
                    if (view.OuterGlow != null)
                    {
                        view.OuterGlow.localScale = Vector3.one *
                            Mathf.Lerp(0.72f, 1f, eased);
                        view.OuterGlow.localRotation = Quaternion.Euler(
                            0f, 0f, Mathf.Lerp(-8f, 0f, eased));
                    }
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.Anticipation;
            yield return AnimatePackOpeningPhase(
                packOpeningAnticipationDuration,
                progress =>
                {
                    float pulse = Mathf.Sin(progress * Mathf.PI) * 0.018f;
                    float lift = Mathf.Sin(progress * Mathf.PI) * 7f;
                    float lean = Mathf.Sin(progress * Mathf.PI * 2f) *
                        (1f - progress) * 0.18f;
                    SetPackOpeningCompositePose(
                        view,
                        1f + pulse,
                        new Vector2(0f, lift),
                        lean);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        0.16f + pulse * 6f);
                    SetPackOpeningGroupAlpha(
                        view.OuterGlowGroup,
                        0.14f + pulse * 4f);
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        pulse * 2.5f);
                });

            _packOpeningPresentationState = PackOpeningPresentationState.Tear;
            yield return AnimatePackOpeningPhase(
                packOpeningTearDuration,
                progress =>
                {
                    float eased = EaseInOutCubic(progress);
                    float linePulse = Mathf.Lerp(
                        0.45f,
                        1f,
                        Mathf.Max(0f, Mathf.Sin(progress * Mathf.PI)));
                    SetPackOpeningGroupAlpha(view.TearGlowGroup, linePulse);
                    view.TearGraphic?.SetState(eased, linePulse);
                    view.TearGlow.localScale = Vector3.one;
                    SetPackOpeningImageAlpha(
                        view.InnerDark,
                        Mathf.Lerp(0f, 0.98f, eased));
                    float microShake = Mathf.Sin(progress * Mathf.PI * 8f) *
                        linePulse * 1.25f;
                    SetPackOpeningCompositePose(
                        view,
                        1f,
                        new Vector2(microShake, 0f),
                        0f);
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        Mathf.Lerp(0.02f, 0.12f, eased));
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.FlapOpen;
            yield return AnimatePackOpeningPhase(
                packOpeningFlapDuration,
                progress =>
                {
                    float eased = EaseOutBack(progress, 0.045f);
                    view.LeftFlap.localRotation = Quaternion.Euler(
                        0f, 0f, Mathf.LerpUnclamped(0f, -31f, eased));
                    view.RightFlap.localRotation = Quaternion.Euler(
                        0f, 0f, Mathf.LerpUnclamped(0f, 31f, eased));
                    float curl = Mathf.Max(
                        0f,
                        Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI));
                    view.LeftFlap.localScale = new Vector3(
                        1f + curl * 0.055f,
                        1f - curl * 0.16f,
                        1f);
                    view.RightFlap.localScale = new Vector3(
                        1f + curl * 0.055f,
                        1f - curl * 0.16f,
                        1f);
                    view.LeftFlap.anchoredPosition = Vector2.LerpUnclamped(
                        Vector2.zero,
                        new Vector2(-view.PackWidth * 0.085f,
                            view.PackHeight * 0.045f),
                        eased);
                    view.RightFlap.anchoredPosition = Vector2.LerpUnclamped(
                        Vector2.zero,
                        new Vector2(view.PackWidth * 0.085f,
                            view.PackHeight * 0.045f),
                        eased);
                    SetPackOpeningGroupAlpha(
                        view.TearGlowGroup,
                        Mathf.Lerp(0.65f, 0.08f, progress));
                    view.TearGraphic?.SetState(
                        1f,
                        Mathf.Lerp(1f, 0.20f, EaseOutQuint(progress)));
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        Mathf.Lerp(0.12f, 0.22f, EaseOutQuint(progress)));
                    if (view.ReleaseBeam != null)
                    {
                        view.ReleaseBeam.localScale = new Vector3(
                            Mathf.Lerp(0.42f, 0.74f, eased),
                            Mathf.Lerp(0.18f, 0.72f, eased),
                            1f);
                    }
                });

            _packOpeningPresentationState = PackOpeningPresentationState.Burst;
            yield return AnimatePackOpeningPhase(
                packOpeningBurstDuration,
                progress =>
                {
                    // Sin(PI) pode ser um negativo microscopico em ponto
                    // flutuante. Pow(negativo, expoente fracionario) gera NaN
                    // e contaminava a escala do pacote no quadro final.
                    float flashWave = Mathf.Max(
                        0f,
                        Mathf.Sin(progress * Mathf.PI));
                    float flash = Mathf.Pow(
                        flashWave,
                        0.72f);
                    SetPackOpeningGroupAlpha(view.RearGlowGroup, flash * 0.92f);
                    SetPackOpeningGroupAlpha(view.OuterGlowGroup, flash * 0.46f);
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        Mathf.Lerp(0.22f, 0.58f, flash));
                    view.RearGlow.localScale = Vector3.one *
                        Mathf.Lerp(0.62f, 1.65f, EaseOutQuint(progress));
                    if (view.ReleaseBeam != null)
                    {
                        view.ReleaseBeam.localScale = new Vector3(
                            Mathf.Lerp(0.72f, 1.08f, EaseOutQuint(progress)),
                            Mathf.Lerp(0.70f, 1.16f, EaseOutQuint(progress)),
                            1f);
                    }
                    SetPackOpeningCompositePose(
                        view,
                        1f + flash * 0.026f,
                        new Vector2(0f, -flash * 5f),
                        0f);
                    if (view.OuterGlow != null)
                    {
                        view.OuterGlow.localScale = Vector3.one *
                            Mathf.Lerp(0.82f, 1.28f, EaseOutQuint(progress));
                        view.OuterGlow.localRotation = Quaternion.Euler(
                            0f, 0f, Mathf.Lerp(0f, 12f, progress));
                    }
                    AnimatePackOpeningParticles(view, progress);
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.StackRise;
            // Compressao curta antes do disparo. As cartas continuam
            // ocultas atras do pacote; nao existe mais uma pilha estacionaria.
            float riseTotal = Mathf.Max(
                0.12f,
                packOpeningStackRiseDuration * 0.44f);
            yield return AnimatePackOpeningPhase(
                riseTotal,
                progress =>
                {
                    for (int index = 0; index < view.Cards.Count; index++)
                    {
                        PackOpeningAnimationCard card = view.Cards[index];
                        if (card.Rect == null)
                            continue;
                        SetPackOpeningGroupAlpha(card.CanvasGroup, 0f);
                        SetPackOpeningGroupAlpha(card.MotionTrailGroup, 0f);
                        SetPackOpeningGroupAlpha(card.LandingGlowGroup, 0f);
                        float compression =
                            Mathf.Sin(progress * Mathf.PI) * 0.035f;
                        SetPackOpeningCardPerspectivePose(
                            card.Rect,
                            card.StackOffset + new Vector2(0f, compression * 40f),
                            new Vector2(
                                0.28f - compression,
                                0.42f + compression),
                            card.LaunchRotation);
                    }
                    float release = EaseInCubic(progress);
                    SetPackOpeningCompositePose(
                        view,
                        Mathf.Lerp(1f, 0.972f, release),
                        new Vector2(0f, Mathf.Lerp(0f, -11f, release)),
                        0f);
                    SetPackOpeningGroupAlpha(
                        view.PackGroup,
                        1f);
                    SetPackOpeningGroupAlpha(
                        view.FrontLipGroup,
                        1f);
                    view.RearGlow.anchoredPosition =
                        new Vector2(0f, Mathf.Lerp(0f, 42f,
                            EaseOutQuint(progress)));
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Lerp(0.42f, 0.72f,
                            EaseOutQuint(progress)));
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        Mathf.Lerp(0.50f, 0.68f, release));
                    if (view.ReleaseBeam != null)
                    {
                        view.ReleaseBeam.anchoredPosition = new Vector2(
                            0f,
                            Mathf.Lerp(0f, 28f, release));
                    }
                });

            _packOpeningPresentationState = PackOpeningPresentationState.FanOut;
            float fanTotal = packOpeningFanDuration +
                packOpeningCardStagger * Mathf.Max(0, view.Cards.Count - 1);
            yield return AnimatePackOpeningPhase(
                fanTotal,
                progress =>
                {
                    float elapsed = progress * fanTotal;
                    float strongestLaunchPulse = 0f;
                    for (int index = 0; index < view.Cards.Count; index++)
                    {
                        PackOpeningAnimationCard card = view.Cards[index];
                        if (card.Rect == null)
                            continue;
                        float delay = card.LaunchOrder *
                            packOpeningCardStagger;
                        float local = Mathf.Clamp01(
                            (elapsed - delay) /
                            Mathf.Max(0.01f, packOpeningFanDuration));
                        if (local >= 0.07f && !card.ReleasedToFront)
                            PromotePackOpeningCard(view, card);
                        // O voo ocupa a primeira parte da janela. O restante
                        // representa o contato com a mesa e um rebote curto,
                        // como uma carta fisicamente arremessada e acomodada.
                        float flight = Mathf.Clamp01(local / 0.78f);
                        float landing = Mathf.Clamp01(
                            (local - 0.78f) / 0.22f);
                        float travel = EaseOutCubic(flight);
                        Vector2 position = CubicBezier(
                            card.StackOffset,
                            card.ApexOffset,
                            card.ApproachOffset,
                            Vector2.zero,
                            travel);
                        float bounceEnvelope = (1f - landing) *
                            (1f - landing);
                        float bounceLift = local < 0.78f
                            ? 0f
                            : Mathf.Abs(Mathf.Sin(landing * Mathf.PI * 2.5f)) *
                              bounceEnvelope * 14f;
                        position.y += bounceLift;
                        float flightScale = Mathf.LerpUnclamped(
                            0.31f,
                            1.035f,
                            EaseOutBack(flight, 0.022f));
                        float flightPerspective = Mathf.Lerp(
                            0.72f,
                            1f,
                            Mathf.Abs(Mathf.Cos(
                                flight * Mathf.PI * 1.35f + index * 0.07f)));
                        // A perspectiva e o pequeno overshoot terminam dentro
                        // da janela individual desta carta. Antes eles eram
                        // corrigidos numa fase coletiva, fazendo as cinco
                        // cartas mudarem de tamanho ao mesmo tempo.
                        float landingEase = EaseOutCubic(landing);
                        float baseScale = local < 0.78f
                            ? flightScale
                            : Mathf.Lerp(1.035f, 1f, landingEase);
                        float perspective = local < 0.78f
                            ? flightPerspective
                            : Mathf.Lerp(flightPerspective, 1f, landingEase);
                        float impactProgress = Mathf.Clamp01(
                            (local - 0.76f) / 0.24f);
                        float impact = Mathf.Max(
                            0f,
                            Mathf.Sin(impactProgress * Mathf.PI)) *
                            (1f - landing * 0.72f);
                        SetPackOpeningGroupAlpha(
                            card.CanvasGroup,
                            Mathf.SmoothStep(0f, 1f,
                                Mathf.Clamp01(local * 7f)));
                        Vector2 tangent = CubicBezierDerivative(
                            card.StackOffset,
                            card.ApexOffset,
                            card.ApproachOffset,
                            Vector2.zero,
                            travel);
                        float pathLean = Mathf.Clamp(
                            tangent.x /
                                Mathf.Max(1f, view.Layer.rect.width) * 72f,
                            -12f,
                            12f);
                        float landingWobble = local < 0.78f
                            ? 0f
                            : Mathf.Sin(landing * Mathf.PI * 4f) *
                              (1f - landing) * 3.2f;
                        float impactShakeX = local < 0.78f
                            ? 0f
                            : Mathf.Sin(landing * Mathf.PI * 6f) *
                              bounceEnvelope * 2.8f;
                        position.x += impactShakeX;
                        SetPackOpeningCardPerspectivePose(
                            card.Rect,
                            position,
                            new Vector2(
                                baseScale * perspective *
                                    (1f + impact * 0.025f),
                                baseScale * (1f - impact * 0.018f)),
                            Mathf.Lerp(
                            card.LaunchRotation,
                            0f,
                                EaseOutQuint(flight)) +
                            pathLean * (1f - EaseOutQuint(flight)) +
                            Mathf.Sin(flight * Mathf.PI * 2f) *
                                (1f - flight) * 1.1f +
                            landingWobble);

                        float trailLocal = Mathf.Clamp01(local - 0.055f);
                        Vector2 trailPosition = CubicBezier(
                            card.StackOffset,
                            card.ApexOffset,
                            card.ApproachOffset,
                            Vector2.zero,
                            EaseOutCubic(trailLocal));
                        float trailPulse = Mathf.Max(
                            0f,
                            Mathf.Sin(Mathf.Clamp01(local / 0.82f) *
                                Mathf.PI));
                        SetPackOpeningGroupAlpha(
                            card.MotionTrailGroup,
                            trailPulse * 0.38f);
                        SetPackOpeningCardPerspectivePose(
                            card.MotionTrail,
                            trailPosition,
                            new Vector2(
                                0.13f + trailPulse * 0.09f,
                                0.74f + trailPulse * 0.64f),
                            Mathf.Lerp(
                                card.LaunchRotation,
                                0f,
                                EaseOutQuint(trailLocal)));

                        SetPackOpeningGroupAlpha(
                            card.LandingGlowGroup,
                            impact * 0.48f);
                        SetPackOpeningCardPerspectivePose(
                            card.LandingGlow,
                            Vector2.zero,
                            Vector2.one * Mathf.Lerp(
                                0.58f,
                                1.58f,
                                EaseOutQuint(impactProgress)),
                            0f);

                        if (local > 0f && local < 0.22f)
                        {
                            strongestLaunchPulse = Mathf.Max(
                                strongestLaunchPulse,
                                1f - local / 0.22f);
                        }
                    }
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Lerp(0.68f, 0.10f, progress) +
                        strongestLaunchPulse * 0.20f);
                    // O pacote permanece totalmente opaco enquanto desce.
                    // Ele sai fisicamente pela borda inferior ao mesmo tempo
                    // que as cartas sobem; só é ocultado depois de já estar
                    // fora do enquadramento, evitando a sobreposição translúcida.
                    float release = EaseInOutSine(progress);
                    float lastLaunchDelay = packOpeningCardStagger *
                        Mathf.Max(0, view.Cards.Count - 1);
                    // A queda acompanha a cadencia: começa no primeiro
                    // disparo e termina quando a quinta carta deixa o pacote.
                    // EaseInOut evita tanto o salto inicial quanto o pacote
                    // parado sobre a carta central no fim da sequência.
                    float packExitDuration = Mathf.Max(
                        packOpeningFanDuration * 0.72f,
                        lastLaunchDelay);
                    float packExit = EaseInOutCubic(Mathf.Clamp01(
                        elapsed / Mathf.Max(0.01f, packExitDuration)));
                    float layerHeight = view.Layer != null
                        ? Mathf.Max(540f, view.Layer.rect.height)
                        : Mathf.Max(540f, Screen.height);
                    SetPackOpeningCompositePose(
                        view,
                        Mathf.Lerp(0.972f, 0.91f, packExit),
                        new Vector2(
                            0f,
                            Mathf.Lerp(-11f, -layerHeight * 0.88f,
                                packExit)),
                        Mathf.Sin(packExit * Mathf.PI) * -1.4f);
                    SetPackOpeningGroupAlpha(
                        view.PackGroup,
                        packExit < 0.995f ? 1f : 0f);
                    SetPackOpeningGroupAlpha(
                        view.FrontLipGroup,
                        packExit < 0.995f ? 1f : 0f);
                    SetPackOpeningGroupAlpha(view.TearGlowGroup, 0f);
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        Mathf.Lerp(0.68f, 0f, release) +
                        strongestLaunchPulse * 0.18f);
                });

            _packOpeningPresentationState = PackOpeningPresentationState.Settle;
            yield return AnimatePackOpeningPhase(
                packOpeningSettleDuration,
                progress =>
                {
                    float eased = EaseOutQuint(progress);
                    foreach (PackOpeningAnimationCard card in view.Cards)
                    {
                        if (card.Rect == null)
                            continue;
                        // Nenhuma acomodacao coletiva: cada carta ja concluiu
                        // seu proprio impacto durante FanOut. Esta fase apenas
                        // remove os rastros e devolve o fundo ao estado normal.
                        SetPackOpeningCardPose(
                            card.Rect,
                            Vector2.zero,
                            1f,
                            0f);
                        SetPackOpeningGroupAlpha(
                            card.MotionTrailGroup,
                            Mathf.Lerp(card.MotionTrailGroup != null
                                    ? card.MotionTrailGroup.alpha
                                    : 0f,
                                0f,
                                eased));
                        SetPackOpeningGroupAlpha(
                            card.LandingGlowGroup,
                            Mathf.Lerp(card.LandingGlowGroup != null
                                    ? card.LandingGlowGroup.alpha
                                    : 0f,
                                0f,
                                eased));
                    }
                    SetPackOpeningGroupAlpha(view.PackGroup, 0f);
                    SetPackOpeningGroupAlpha(view.FrontLipGroup, 0f);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Lerp(0.18f, 0f, eased));
                    SetPackOpeningGroupAlpha(
                        view.OuterGlowGroup,
                        Mathf.Lerp(0.16f, 0f, eased));
                    SetPackOpeningGroupAlpha(view.ReleaseBeamGroup, 0f);
                });

            CompletePackOpeningPresentation(view);
        }

        private IEnumerator AnimatePackOpeningPhase(
            float duration,
            Action<float> update)
        {
            duration = SanitizeFinite(duration, 0f);
            if (_packOpeningSkipRequested || duration <= 0f)
            {
                update?.Invoke(1f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && !_packOpeningSkipRequested)
            {
                // Um frame lento não deve fazer a apresentação saltar vários
                // centímetros, mas um limite de 30 FPS alongava demais a
                // sequência em Android durante um hitch. O teto de 50 ms
                // preserva a trajetória sem criar a sensação de congelamento.
                float frameDelta = Mathf.Min(
                    Mathf.Max(
                        0f,
                        SanitizeFinite(Time.unscaledDeltaTime, 1f / 60f)),
                    1f / 20f);
                elapsed += frameDelta;
                update?.Invoke(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            update?.Invoke(1f);
        }

        private void CompletePackOpeningPresentation(
            PackOpeningAnimationView view)
        {
            if (view != null)
            {
                foreach (PackOpeningAnimationCard card in view.Cards)
                {
                    if (card.Rect == null)
                        continue;
                    card.Rect.SetParent(_screenRoot, false);
                    card.Rect.anchorMin = card.FinalMin;
                    card.Rect.anchorMax = card.FinalMax;
                    card.Rect.offsetMin = Vector2.zero;
                    card.Rect.offsetMax = Vector2.zero;
                    SetPackOpeningCardPose(
                        card.Rect,
                        Vector2.zero,
                        1f,
                        0f);
                    RestorePackOpeningCardInteraction(card);
                    if (card.Button != null)
                        card.Button.interactable = true;
                    card.Rect.SetAsLastSibling();
                }

                if (view.RevealInstruction != null)
                {
                    if (view.RevealInstruction.transform.parent != null)
                    {
                        view.RevealInstruction.transform.parent.gameObject
                            .SetActive(true);
                    }
                    view.RevealInstruction.gameObject.SetActive(true);
                }
                if (view.Layer != null)
                    Destroy(view.Layer.gameObject);
            }

            _activePackOpeningView = null;
            _packOpeningSequenceRoutine = null;
            _packOpeningSkipRequested = false;
            _packOpeningSequenceActive = false;
            _packOpeningPresentationState =
                PackOpeningPresentationState.RevealReady;
            Canvas.ForceUpdateCanvases();
        }

        private void RestorePackOpeningCardInteraction(
            PackOpeningAnimationCard card)
        {
            if (card?.CanvasGroup == null)
                return;

            // This CanvasGroup only exists while the card is moving. Leaving
            // blocksRaycasts=false on it makes the final card look ready while
            // silently swallowing every pointer/touch event until the screen is
            // rebuilt. Restore interaction immediately and remove the temporary
            // component so the original reveal tray behaves exactly as before.
            CanvasGroup group = card.CanvasGroup;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            group.ignoreParentGroups = false;
            card.CanvasGroup = null;
            Destroy(group);
        }

        private void RequestPackOpeningSkip()
        {
            if (_packOpeningSequenceActive)
                _packOpeningSkipRequested = true;
        }

        private void CancelPackOpeningPresentation()
        {
            if (_packOpeningSequenceRoutine != null)
                StopCoroutine(_packOpeningSequenceRoutine);
            _packOpeningSequenceRoutine = null;

            if (_activePackOpeningView?.Layer != null)
            {
                _activePackOpeningView.Layer.SetParent(null, false);
                Destroy(_activePackOpeningView.Layer.gameObject);
            }

            _activePackOpeningView = null;
            _packOpeningSkipRequested = false;
            _packOpeningSequenceActive = false;
            _packOpeningPresentationState = PackOpeningPresentationState.Idle;
        }

        private void ReleasePackOpeningAnimationResources()
        {
            if (_packOpeningGlowSprite != null)
                Destroy(_packOpeningGlowSprite);
            if (_packOpeningGlowTexture != null)
                Destroy(_packOpeningGlowTexture);
            _packOpeningGlowSprite = null;
            _packOpeningGlowTexture = null;
        }

        private Sprite ResolvePackOpeningGlowSprite()
        {
            if (_packOpeningGlowSprite != null)
                return _packOpeningGlowSprite;

            const int size = 64;
            _packOpeningGlowTexture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "Arcane Pack Opening Glow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.Pow(
                        Mathf.Clamp01(1f - distance),
                        2.4f);
                    // A textura é neutra; a cor final pertence ao Image.
                    // Isso preserva dourado, ciano e branco sem misturar
                    // matizes quando o mesmo halo é reutilizado.
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            _packOpeningGlowTexture.SetPixels32(pixels);
            _packOpeningGlowTexture.Apply(false, false);
            _packOpeningGlowSprite = Sprite.Create(
                _packOpeningGlowTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _packOpeningGlowSprite.name = "MD2 Pack Opening Glow";
            _packOpeningGlowSprite.hideFlags = HideFlags.DontSave;
            return _packOpeningGlowSprite;
        }

        private void CreatePackOpeningParticles(PackOpeningAnimationView view)
        {
            const int particleCount = 20;
            for (int index = 0; index < particleCount; index++)
            {
                float normalized = index / (particleCount - 1f);
                float angle = Mathf.Lerp(205f, 335f, normalized) *
                    Mathf.Deg2Rad;
                float variation = Mathf.Repeat(index * 0.618034f, 1f);
                float distance = Mathf.Lerp(115f, 285f, variation);
                Vector2 start = view.PackBasePosition +
                    new Vector2(0f, view.PackHeight * 0.30f);
                Vector2 end = start + new Vector2(
                    Mathf.Cos(angle) * distance,
                    -Mathf.Sin(angle) * distance + 64f);
                float startRotation = index * 47f;
                Color particleColor = index % 5 == 0
                    ? new Color(1f, 0.98f, 0.88f, 1f)
                    : index % 2 == 0 ? Gold : Cyan;
                Image particle = CreatePackOpeningSizedImage(
                    view.Layer,
                    $"Partícula do Pacote {index + 1}",
                    start,
                    new Vector2(3f + index % 3 * 1.8f,
                        13f + index % 5 * 3.2f),
                    particleColor);
                particle.raycastTarget = false;
                particle.rectTransform.localRotation = Quaternion.Euler(
                    0f, 0f, startRotation);
                CanvasGroup group = AddPackOpeningCanvasGroup(
                    particle.gameObject,
                    0f);
                view.Particles.Add(new PackOpeningParticle
                {
                    Rect = particle.rectTransform,
                    CanvasGroup = group,
                    Start = start,
                    End = end,
                    StartRotation = startRotation,
                    RotationSpeed = Mathf.Lerp(-210f, 240f, variation)
                });
            }
        }

        private static void AnimatePackOpeningParticles(
            PackOpeningAnimationView view,
            float progress)
        {
            float eased = EaseOutCubic(progress);
            float alpha = Mathf.Sin(progress * Mathf.PI);
            foreach (PackOpeningParticle particle in view.Particles)
            {
                if (particle.Rect == null)
                    continue;
                particle.Rect.anchoredPosition = Vector2.Lerp(
                    particle.Start,
                    particle.End,
                    eased);
                particle.Rect.localScale = Vector3.one *
                    Mathf.Lerp(0.55f, 1.15f, eased);
                particle.Rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    particle.StartRotation +
                    particle.RotationSpeed * progress);
                SetPackOpeningGroupAlpha(particle.CanvasGroup, alpha);
            }
        }

        private static RectTransform CreatePackOpeningSizedRect(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreatePackOpeningSizedImage(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            RectTransform rect = CreatePackOpeningSizedRect(
                parent,
                name,
                position,
                size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreatePackOpeningSizedSlice(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Rect normalizedUv)
        {
            RectTransform rect = CreatePackOpeningSizedRect(
                parent,
                name,
                position,
                size);
            RawImage raw = rect.gameObject.AddComponent<RawImage>();
            ApplyPackOpeningSlice(raw, sprite, normalizedUv);
            return raw;
        }

        private static RawImage CreatePackOpeningSlice(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 min,
            Vector2 max,
            Rect normalizedUv)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            RawImage raw = item.GetComponent<RawImage>();
            ApplyPackOpeningSlice(raw, sprite, normalizedUv);
            return raw;
        }

        private static void ApplyPackOpeningSlice(
            RawImage raw,
            Sprite sprite,
            Rect normalizedUv)
        {
            if (raw == null)
                return;
            if (sprite == null || sprite.texture == null)
            {
                raw.color = new Color(0.015f, 0.08f, 0.14f, 1f);
                return;
            }

            raw.texture = sprite.texture;
            Rect textureRect = sprite.textureRect;
            float textureWidth = sprite.texture.width;
            float textureHeight = sprite.texture.height;
            raw.uvRect = new Rect(
                (textureRect.x + textureRect.width * normalizedUv.x) /
                    textureWidth,
                (textureRect.y + textureRect.height * normalizedUv.y) /
                    textureHeight,
                textureRect.width * normalizedUv.width / textureWidth,
                textureRect.height * normalizedUv.height / textureHeight);
            raw.color = Color.white;
        }

        private static CanvasGroup AddPackOpeningCanvasGroup(
            GameObject target,
            float alpha)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null)
                group = target.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static void SetPackOpeningCompositePose(
            PackOpeningAnimationView view,
            float scale,
            Vector2 offset,
            float rotation)
        {
            scale = Mathf.Clamp(SanitizeFinite(scale, 1f), 0.01f, 8f);
            offset = SanitizeFinite(offset, Vector2.zero);
            rotation = SanitizeFinite(rotation, 0f);
            if (view?.PackRoot != null)
            {
                view.PackRoot.anchoredPosition = view.PackBasePosition + offset;
                view.PackRoot.localScale = Vector3.one * scale;
                view.PackRoot.localRotation = Quaternion.Euler(0f, 0f, rotation);
            }
            if (view?.FrontLip != null)
            {
                view.FrontLip.anchoredPosition =
                    view.FrontLipBasePosition + offset;
                view.FrontLip.localScale = Vector3.one * scale;
                view.FrontLip.localRotation = Quaternion.Euler(0f, 0f, rotation);
            }
            if (view?.TearGlow != null)
            {
                view.TearGlow.anchoredPosition = view.TearBasePosition + offset;
                view.TearGlow.localRotation = Quaternion.Euler(0f, 0f, rotation);
            }
        }

        private static void SetPackOpeningCardPose(
            RectTransform rect,
            Vector2 offset,
            float scale,
            float rotation)
        {
            SetPackOpeningCardPerspectivePose(
                rect,
                offset,
                Vector2.one * scale,
                rotation);
        }

        private static void SetPackOpeningCardPerspectivePose(
            RectTransform rect,
            Vector2 offset,
            Vector2 scale,
            float rotation)
        {
            if (rect == null)
                return;
            offset = SanitizeFinite(offset, Vector2.zero);
            scale = SanitizeFinite(scale, Vector2.one);
            scale.x = Mathf.Clamp(scale.x, 0.01f, 8f);
            scale.y = Mathf.Clamp(scale.y, 0.01f, 8f);
            rotation = SanitizeFinite(rotation, 0f);
            rect.anchoredPosition = offset;
            rect.localScale = new Vector3(scale.x, scale.y, 1f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static Vector2 QuadraticBezier(
            Vector2 start,
            Vector2 control,
            Vector2 end,
            float value)
        {
            value = Mathf.Clamp01(value);
            float inverse = 1f - value;
            return inverse * inverse * start +
                2f * inverse * value * control +
                value * value * end;
        }

        private static Vector2 CubicBezier(
            Vector2 start,
            Vector2 controlA,
            Vector2 controlB,
            Vector2 end,
            float value)
        {
            value = Mathf.Clamp01(SanitizeFinite(value, 0f));
            float inverse = 1f - value;
            float inverseSquared = inverse * inverse;
            float valueSquared = value * value;
            return inverseSquared * inverse * start +
                3f * inverseSquared * value * controlA +
                3f * inverse * valueSquared * controlB +
                valueSquared * value * end;
        }

        private static Vector2 CubicBezierDerivative(
            Vector2 start,
            Vector2 controlA,
            Vector2 controlB,
            Vector2 end,
            float value)
        {
            value = Mathf.Clamp01(SanitizeFinite(value, 0f));
            float inverse = 1f - value;
            return 3f * inverse * inverse * (controlA - start) +
                6f * inverse * value * (controlB - controlA) +
                3f * value * value * (end - controlB);
        }

        private static void PromotePackOpeningCard(
            PackOpeningAnimationView view,
            PackOpeningAnimationCard card)
        {
            if (view == null || card == null || card.ReleasedToFront)
                return;

            card.ReleasedToFront = true;
            int targetIndex = view.FrontLip != null
                ? view.FrontLip.GetSiblingIndex()
                : view.PackRoot != null
                    ? view.PackRoot.GetSiblingIndex() + 1
                    : 0;
            if (card.MotionTrail != null)
                card.MotionTrail.SetSiblingIndex(targetIndex++);
            if (card.LandingGlow != null)
                card.LandingGlow.SetSiblingIndex(targetIndex++);
            if (card.Rect != null)
                card.Rect.SetSiblingIndex(targetIndex);
        }

        private static int ResolvePackOpeningLaunchOrder(int index)
        {
            // Sequência visual inequívoca da esquerda para a direita:
            // primeira, segunda, terceira, quarta e quinta carta.
            return Mathf.Max(0, index);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : value;
        }

        private static Vector2 SanitizeFinite(
            Vector2 value,
            Vector2 fallback)
        {
            return new Vector2(
                SanitizeFinite(value.x, fallback.x),
                SanitizeFinite(value.y, fallback.y));
        }

        private static void SetPackOpeningGroupAlpha(
            CanvasGroup group,
            float alpha)
        {
            if (group != null)
                group.alpha = Mathf.Clamp01(SanitizeFinite(alpha, 0f));
        }

        private static void SetPackOpeningImageAlpha(Image image, float alpha)
        {
            if (image == null)
                return;
            Color color = image.color;
            color.a = Mathf.Clamp01(SanitizeFinite(alpha, 0f));
            image.color = color;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value;
        }

        private static float EaseOutQuint(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse * inverse * inverse;
        }

        private static float EaseInOutSine(float value)
        {
            value = Mathf.Clamp01(value);
            return -(Mathf.Cos(Mathf.PI * value) - 1f) * 0.5f;
        }

        private static float EaseInOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value < 0.5f
                ? 4f * value * value * value
                : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;
        }

        private static float EaseOutBack(float value, float overshoot)
        {
            value = Mathf.Clamp01(value) - 1f;
            float strength = 1.70158f * Mathf.Clamp(overshoot, 0f, 0.25f) /
                0.10f;
            return 1f + (strength + 1f) * value * value * value +
                strength * value * value;
        }
    }
}
