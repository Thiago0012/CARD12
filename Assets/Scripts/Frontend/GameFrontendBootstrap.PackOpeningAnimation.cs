using System;
using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Cards;
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
            RarityCharge,
            EnergyCurtain,
            Anticipation,
            Tear,
            FlapOpen,
            Burst,
            StackRise,
            CardEject,
            FanOut,
            Settle,
            RevealReady
        }

        [Header("Loja - animação de abertura do pacote")]
        [SerializeField]
        private bool packOpeningAnimationEnabled = true;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningFadeDuration = 0.10f;
        [SerializeField, Range(0.05f, 1.5f)]
        private float packOpeningEnterDuration = 0.28f;
        [SerializeField, Range(0.10f, 1.5f)]
        private float packOpeningRarityChargeDuration = 0.18f;
        [SerializeField, Range(0.10f, 1.5f)]
        private float packOpeningEnergyCurtainDuration = 0.20f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningAnticipationDuration = 0.10f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningTearDuration = 0.36f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningFlapDuration = 0.26f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningBurstDuration = 0.20f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningStackRiseDuration = 0.32f;
        [SerializeField, Range(0.12f, 0.8f)]
        private float packOpeningCardEjectDuration = 0.26f;
        [SerializeField, Range(0.02f, 0.18f)]
        private float packOpeningEjectStagger = 0.06f;
        [SerializeField, Range(0.10f, 1.5f)]
        private float packOpeningFanDuration = 0.48f;
        [SerializeField, Range(0f, 0.35f)]
        private float packOpeningCardStagger = 0.10f;
        [SerializeField, Range(0.05f, 0.75f)]
        private float packOpeningSettleDuration = 0.28f;

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
            public Vector2 EjectApexOffset;
            public Vector2 EjectStagingOffset;
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
            public CardRarity PeakRarity;
            public Color RarityAccent;
            public ArcaneRarityRevealGraphic RarityAura;
            public RectTransform RearGlow;
            public CanvasGroup RearGlowGroup;
            public RectTransform OuterGlow;
            public CanvasGroup OuterGlowGroup;
            public RectTransform ReleaseBeam;
            public CanvasGroup ReleaseBeamGroup;
            public RectTransform HorizonLine;
            public CanvasGroup HorizonLineGroup;
            public RectTransform LightCone;
            public CanvasGroup LightConeGroup;
            public RectTransform PackRoot;
            public CanvasGroup PackGroup;
            public Image InnerDark;
            public RectTransform LeftFlap;
            public CanvasGroup LeftFlapGroup;
            public RectTransform RightFlap;
            public CanvasGroup RightFlapGroup;
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
            public readonly List<RectTransform> EnergyCurtains = new();
            public readonly List<CanvasGroup> EnergyCurtainGroups = new();
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

        public string PendingOpeningPeakRarityName =>
            ResolvePackOpeningPeakRarity(_repository?.PendingPackOpening)
                .ToString();

        private void StartPackOpeningPresentation(
            PendingPackOpeningRecord opening)
        {
            if (opening == null || _packOpeningSequenceActive)
                return;

            _packOpeningStarted = true;
            ShowPackOpening(opening, packOpeningAnimationEnabled);
        }

        private PackOpeningAnimationView CreatePackOpeningAnimationView(
            ShopPackDefinition pack,
            PendingPackOpeningRecord opening)
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
                Layer = blocker.rectTransform,
                PeakRarity = ResolvePackOpeningPeakRarity(opening)
            };
            view.RarityAccent = ResolvePackOpeningRarityAccent(view.PeakRarity);

            // O cenário da loja já funciona como fundo cinematográfico da
            // abertura. Não adicionamos películas de tela inteira: além de
            // esconder seus detalhes, a combinação de escurecimento e clarão
            // dourado produzia um fundo amarelado translúcido.

            view.RarityAura = CreatePackRarityAura(
                view.Layer,
                $"Presságio {view.PeakRarity} da Abertura",
                new Vector2(0.19f, 0.035f),
                new Vector2(0.81f, 0.965f),
                view.PeakRarity,
                false);

            Image outerGlow = CreatePanel(
                view.Layer,
                "Aura Exterior do Pacote",
                new Vector2(0.19f, 0.02f),
                new Vector2(0.81f, 0.96f),
                new Color(view.RarityAccent.r, view.RarityAccent.g,
                    view.RarityAccent.b, 1f));
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
                new Color(view.RarityAccent.r, view.RarityAccent.g,
                    view.RarityAccent.b, 1f));
            releaseBeam.sprite = ResolvePackOpeningGlowSprite();
            releaseBeam.preserveAspect = false;
            releaseBeam.raycastTarget = false;
            view.ReleaseBeam = releaseBeam.rectTransform;
            view.ReleaseBeamGroup = AddPackOpeningCanvasGroup(
                releaseBeam.gameObject,
                0f);

            // Referência temporal do GIF: um horizonte luminoso e um cone
            // vertical anunciam a abertura antes do corte. São elementos
            // leves de UI, sem película opaca sobre o cenário da loja.
            Image horizonLine = CreatePanel(
                view.Layer,
                "Horizonte Luminoso da Abertura",
                new Vector2(0.10f, 0.485f),
                new Vector2(0.90f, 0.502f),
                new Color(view.RarityAccent.r, view.RarityAccent.g,
                    view.RarityAccent.b, 1f));
            horizonLine.sprite = ResolvePackOpeningGlowSprite();
            horizonLine.preserveAspect = false;
            horizonLine.raycastTarget = false;
            view.HorizonLine = horizonLine.rectTransform;
            view.HorizonLineGroup = AddPackOpeningCanvasGroup(
                horizonLine.gameObject,
                0f);

            Image lightCone = CreatePanel(
                view.Layer,
                "Cone de Luz sobre o Pacote",
                new Vector2(0.31f, 0.39f),
                new Vector2(0.69f, 0.985f),
                new Color(view.RarityAccent.r, view.RarityAccent.g,
                    view.RarityAccent.b, 1f));
            lightCone.sprite = ResolvePackOpeningGlowSprite();
            lightCone.preserveAspect = false;
            lightCone.raycastTarget = false;
            view.LightCone = lightCone.rectTransform;
            view.LightConeGroup = AddPackOpeningCanvasGroup(
                lightCone.gameObject,
                0f);

            const int curtainCount = 7;
            for (int curtainIndex = 0;
                 curtainIndex < curtainCount;
                 curtainIndex++)
            {
                float center = Mathf.Lerp(0.285f, 0.715f,
                    curtainIndex / (curtainCount - 1f));
                float halfWidth = curtainIndex % 2 == 0 ? 0.010f : 0.006f;
                Color curtainColor = view.PeakRarity == CardRarity.UR
                    ? Color.HSVToRGB(
                        Mathf.Repeat(0.78f + curtainIndex * 0.105f, 1f),
                        0.72f,
                        1f)
                    : curtainIndex % 3 == 0
                        ? Color.white
                        : view.RarityAccent;
                Image curtain = CreatePanel(
                    view.Layer,
                    $"Faixa de Energia {curtainIndex + 1}",
                    new Vector2(center - halfWidth, 0.22f),
                    new Vector2(center + halfWidth, 0.91f),
                    new Color(curtainColor.r, curtainColor.g,
                        curtainColor.b, 1f));
                curtain.sprite = ResolvePackOpeningGlowSprite();
                curtain.preserveAspect = false;
                curtain.raycastTarget = false;
                CanvasGroup curtainGroup = AddPackOpeningCanvasGroup(
                    curtain.gameObject,
                    0f);
                view.EnergyCurtains.Add(curtain.rectTransform);
                view.EnergyCurtainGroups.Add(curtainGroup);
            }

            Image rearGlow = CreatePanel(
                view.Layer,
                "Clarão Traseiro do Pacote",
                new Vector2(0.30f, 0.12f),
                new Vector2(0.70f, 0.84f),
                new Color(view.RarityAccent.r, view.RarityAccent.g,
                    view.RarityAccent.b, 1f));
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
            view.LeftFlapGroup = AddPackOpeningCanvasGroup(
                leftFlap.gameObject,
                1f);

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
            view.RightFlapGroup = AddPackOpeningCanvasGroup(
                rightFlap.gameObject,
                1f);

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
                0f);
            return view;
        }

        private CardRarity ResolvePackOpeningPeakRarity(
            PendingPackOpeningRecord opening)
        {
            CardRarity peak = CardRarity.N;
            if (opening?.cardIds == null)
                return peak;

            foreach (string cardId in opening.cardIds)
            {
                CardCatalogEntry entry = DeckRepository.ResolveCard(
                    _catalog,
                    cardId);
                CardRarity rarity =
                    PackRarityDistribution.ResolveCardRarity(entry);
                if (rarity > peak)
                {
                    peak = rarity;
                }
            }
            return peak;
        }

        private static Color ResolvePackOpeningRarityAccent(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.R => new Color(0.12f, 0.67f, 1f, 1f),
                CardRarity.SR => new Color(1f, 0.66f, 0.08f, 1f),
                CardRarity.UR => new Color(0.96f, 0.16f, 0.78f, 1f),
                _ => new Color(0.70f, 0.78f, 0.87f, 1f)
            };
        }

        private static ArcaneRarityRevealGraphic CreatePackRarityAura(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            CardRarity rarity,
            bool animateIdle)
        {
            if (parent == null)
                return null;

            var auraObject = new GameObject(objectName, typeof(RectTransform));
            auraObject.transform.SetParent(parent, false);
            RectTransform rect = auraObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            var aura = auraObject.AddComponent<ArcaneRarityRevealGraphic>();
            aura.Configure(rarity, animateIdle);
            return aura;
        }

        private static void SetPackOpeningRarityAura(
            PackOpeningAnimationView view,
            float progress,
            float pulse)
        {
            view?.RarityAura?.SetState(
                Mathf.Clamp01(SanitizeFinite(progress, 0f)),
                Mathf.Clamp01(SanitizeFinite(pulse, 0f)));
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
            // A ejeção e a distribuição são movimentos distintos. Primeiro
            // todas as cartas deixam fisicamente a boca do pacote e formam uma
            // pequena pilha aberta no alto; só então seguem para seus destinos.
            // O limite vertical mantém a carta inteira visível em 16:9 e em
            // telas Android mais estreitas.
            var ejectStagingCenter = new Vector2(
                0.5f + lane * 0.022f,
                Mathf.Min(0.84f,
                    mouthCenter.y + 0.105f +
                    (2f - Mathf.Abs(lane)) * 0.006f));
            var ejectApexCenter = new Vector2(
                Mathf.Lerp(mouthCenter.x, ejectStagingCenter.x, 0.42f),
                Mathf.Min(0.925f, ejectStagingCenter.y + 0.075f));
            Vector2 stackOffset = Vector2.Scale(
                mouthCenter - finalCenter,
                layerSize);
            Vector2 ejectApexOffset = Vector2.Scale(
                ejectApexCenter - finalCenter,
                layerSize);
            Vector2 ejectStagingOffset = Vector2.Scale(
                ejectStagingCenter - finalCenter,
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
                new Color(0.36f, 0.92f, 1f, 1f));
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
                new Color(1f, 0.82f, 0.34f, 1f));
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
                EjectApexOffset = ejectApexOffset,
                EjectStagingOffset = ejectStagingOffset,
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
                    SetPackOpeningRarityAura(view, eased * 0.16f, eased);
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
                    SetPackOpeningCompositePose(
                        view,
                        scale,
                        offset,
                        0f);
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
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.14f, 0.34f, eased),
                        visibility);
                    if (view.OuterGlow != null)
                    {
                        view.OuterGlow.localScale = Vector3.one *
                            Mathf.Lerp(0.72f, 1f, eased);
                        view.OuterGlow.localRotation = Quaternion.Euler(
                            0f, 0f, Mathf.Lerp(-8f, 0f, eased));
                    }
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.RarityCharge;
            yield return AnimatePackOpeningPhase(
                packOpeningRarityChargeDuration,
                progress =>
                {
                    float eased = EaseInOutSine(progress);
                    float pulse = 0.5f + 0.5f *
                        Mathf.Sin(progress * Mathf.PI * 4f);
                    float envelope = Mathf.Sin(progress * Mathf.PI);
                    float lift = envelope * 2.5f;
                    SetPackOpeningCompositePose(
                        view,
                        1f + envelope * 0.006f,
                        new Vector2(0f, lift),
                        0f);
                    SetPackOpeningGroupAlpha(
                        view.HorizonLineGroup,
                        envelope * (0.26f + pulse * 0.22f));
                    if (view.HorizonLine != null)
                    {
                        view.HorizonLine.localScale = new Vector3(
                            Mathf.Lerp(0.12f, 1f, eased),
                            Mathf.Lerp(0.35f, 1.15f, pulse),
                            1f);
                    }
                    SetPackOpeningGroupAlpha(
                        view.LightConeGroup,
                        envelope * (0.09f + pulse * 0.08f));
                    if (view.LightCone != null)
                    {
                        view.LightCone.localScale = new Vector3(
                            Mathf.Lerp(0.30f, 0.92f, eased),
                            Mathf.Lerp(0.62f, 1.08f, eased),
                            1f);
                    }
                    SetPackOpeningGroupAlpha(
                        view.OuterGlowGroup,
                        0.15f + envelope * (0.08f + pulse * 0.08f));
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        0.12f + envelope * 0.22f);
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.30f, 0.62f, eased),
                        Mathf.Clamp01(0.42f + pulse * envelope));
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.EnergyCurtain;
            yield return AnimatePackOpeningPhase(
                packOpeningEnergyCurtainDuration,
                progress =>
                {
                    float eased = EaseInOutCubic(progress);
                    float envelope = Mathf.Sin(progress * Mathf.PI);
                    for (int index = 0;
                         index < view.EnergyCurtains.Count;
                         index++)
                    {
                        RectTransform curtain = view.EnergyCurtains[index];
                        CanvasGroup group = index <
                            view.EnergyCurtainGroups.Count
                                ? view.EnergyCurtainGroups[index]
                                : null;
                        float phase = Mathf.Repeat(
                            progress * 1.35f + index * 0.137f,
                            1f);
                        float wave = Mathf.Sin(phase * Mathf.PI);
                        SetPackOpeningGroupAlpha(
                            group,
                            envelope * wave * (index % 2 == 0 ? 0.30f : 0.20f));
                        if (curtain != null)
                        {
                            curtain.anchoredPosition = new Vector2(
                                Mathf.Lerp(-20f, 20f, phase),
                                Mathf.Sin((progress + index * 0.11f) *
                                    Mathf.PI * 2f) * 12f);
                            curtain.localScale = new Vector3(
                                Mathf.Lerp(0.55f, 1.45f, wave),
                                Mathf.Lerp(0.30f, 1.08f, eased),
                                1f);
                        }
                    }
                    SetPackOpeningGroupAlpha(
                        view.HorizonLineGroup,
                        Mathf.Lerp(0.30f, 0.58f, envelope));
                    SetPackOpeningGroupAlpha(
                        view.LightConeGroup,
                        envelope * 0.22f);
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        envelope * 0.20f);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        0.20f + envelope * 0.30f);
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.58f, 0.76f, eased),
                        Mathf.Clamp01(0.58f + envelope * 0.42f));
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.Anticipation;
            yield return AnimatePackOpeningPhase(
                packOpeningAnticipationDuration,
                progress =>
                {
                    float pulse = Mathf.Sin(progress * Mathf.PI) * 0.006f;
                    float lift = Mathf.Sin(progress * Mathf.PI) * 2f;
                    SetPackOpeningCompositePose(
                        view,
                        1f + pulse,
                        new Vector2(0f, lift),
                        0f);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        0.16f + pulse * 6f);
                    SetPackOpeningGroupAlpha(
                        view.OuterGlowGroup,
                        0.14f + pulse * 4f);
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        pulse * 2.5f);
                    SetPackOpeningRarityAura(
                        view,
                        0.30f + Mathf.Sin(progress * Mathf.PI) * 0.16f,
                        0.55f + Mathf.Sin(progress * Mathf.PI) * 0.45f);
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
                    // O interior preto era percebido como uma faixa solta e
                    // desalinhada. A abertura agora depende apenas do rasgo
                    // luminoso; o vazio não é desenhado sobre a arte.
                    SetPackOpeningImageAlpha(view.InnerDark, 0f);
                    SetPackOpeningCompositePose(
                        view,
                        1f,
                        Vector2.zero,
                        0f);
                    float lipExit = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01((progress - 0.42f) / 0.58f));
                    SetPackOpeningGroupAlpha(
                        view.FrontLipGroup,
                        1f - lipExit);
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        Mathf.Lerp(0.02f, 0.12f, eased));
                    SetPackOpeningGroupAlpha(
                        view.HorizonLineGroup,
                        Mathf.Lerp(0.30f, 0.07f, eased));
                    SetPackOpeningGroupAlpha(
                        view.LightConeGroup,
                        Mathf.Lerp(0.12f, 0.03f, eased));
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.42f, 0.64f, eased),
                        linePulse);
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
                    float flapExit = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01((progress - 0.10f) / 0.66f));
                    SetPackOpeningGroupAlpha(
                        view.LeftFlapGroup,
                        1f - flapExit);
                    SetPackOpeningGroupAlpha(
                        view.RightFlapGroup,
                        1f - flapExit);
                    SetPackOpeningGroupAlpha(view.FrontLipGroup, 0f);
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
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.58f, 0.78f, eased),
                        Mathf.Lerp(0.55f, 1f, eased));
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
                        1f + flash * 0.010f,
                        Vector2.zero,
                        0f);
                    if (view.OuterGlow != null)
                    {
                        view.OuterGlow.localScale = Vector3.one *
                            Mathf.Lerp(0.82f, 1.28f, EaseOutQuint(progress));
                        view.OuterGlow.localRotation = Quaternion.Euler(
                            0f, 0f, Mathf.Lerp(0f, 12f, progress));
                    }
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.72f, 1f, EaseOutQuint(progress)),
                        flash);
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
                        new Vector2(0f, Mathf.Lerp(0f, -42f, release)),
                        0f);
                    SetPackOpeningGroupAlpha(
                        view.PackGroup,
                        1f);
                    SetPackOpeningGroupAlpha(
                        view.FrontLipGroup,
                        0f);
                    SetPackOpeningGroupAlpha(view.LeftFlapGroup, 0f);
                    SetPackOpeningGroupAlpha(view.RightFlapGroup, 0f);
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
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.94f, 0.82f, release),
                        0.86f);
                    AnimatePackOpeningReleaseCurtains(
                        view,
                        progress * 0.34f,
                        EaseOutCubic(progress));
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.CardEject;
            float ejectTotal = packOpeningCardEjectDuration +
                packOpeningEjectStagger * Mathf.Max(0, view.Cards.Count - 1);
            yield return AnimatePackOpeningPhase(
                ejectTotal,
                progress =>
                {
                    float elapsed = progress * ejectTotal;
                    float strongestEjectPulse = 0f;
                    for (int index = 0; index < view.Cards.Count; index++)
                    {
                        PackOpeningAnimationCard card = view.Cards[index];
                        if (card.Rect == null)
                            continue;

                        float delay = card.LaunchOrder *
                            packOpeningEjectStagger;
                        float local = Mathf.Clamp01(
                            (elapsed - delay) /
                            Mathf.Max(0.01f, packOpeningCardEjectDuration));
                        if (local <= 0f)
                        {
                            SetPackOpeningGroupAlpha(card.CanvasGroup, 0f);
                            SetPackOpeningGroupAlpha(card.MotionTrailGroup, 0f);
                            SetPackOpeningCardPerspectivePose(
                                card.Rect,
                                card.StackOffset,
                                new Vector2(0.25f, 0.36f),
                                card.LaunchRotation);
                            continue;
                        }

                        if (!card.ReleasedToFront)
                            PromotePackOpeningCard(view, card);

                        // 72%: saída vertical rápida. 28%: pequena acomodação
                        // no alto. Assim cada carta produz um "pum" completo
                        // antes de começar a distribuição para a mesa.
                        float ascent = Mathf.Clamp01(local / 0.72f);
                        float staging = Mathf.Clamp01(
                            (local - 0.72f) / 0.28f);
                        float travel = EaseOutCubic(ascent);
                        Vector2 position = QuadraticBezier(
                            card.StackOffset,
                            card.EjectApexOffset,
                            card.EjectStagingOffset,
                            travel);
                        if (staging > 0f)
                        {
                            position.y += Mathf.Sin(staging * Mathf.PI) *
                                (1f - staging) * 8f;
                        }

                        float launchScale = Mathf.LerpUnclamped(
                            0.25f,
                            0.60f,
                            EaseOutBack(ascent, 0.020f));
                        float scale = staging <= 0f
                            ? launchScale
                            : Mathf.Lerp(0.60f, 0.56f,
                                EaseOutCubic(staging));
                        float stagingRotation = (index - 2f) * 2.1f;
                        float rotation = Mathf.Lerp(
                            card.LaunchRotation,
                            stagingRotation,
                            EaseOutQuint(ascent));
                        rotation += Mathf.Sin(staging * Mathf.PI * 3f) *
                            (1f - staging) * 1.4f;
                        SetPackOpeningCardPerspectivePose(
                            card.Rect,
                            position,
                            new Vector2(scale, scale),
                            rotation);
                        SetPackOpeningGroupAlpha(
                            card.CanvasGroup,
                            Mathf.SmoothStep(0f, 1f,
                                Mathf.Clamp01(local * 9f)));

                        float trailLocal = Mathf.Clamp01(
                            (local - 0.035f) / 0.70f);
                        Vector2 trailPosition = QuadraticBezier(
                            card.StackOffset,
                            card.EjectApexOffset,
                            card.EjectStagingOffset,
                            EaseOutCubic(trailLocal));
                        float trailPulse = Mathf.Max(
                            0f,
                            Mathf.Sin(Mathf.Clamp01(local / 0.78f) *
                                Mathf.PI));
                        SetPackOpeningGroupAlpha(
                            card.MotionTrailGroup,
                            trailPulse * 0.56f);
                        SetPackOpeningCardPerspectivePose(
                            card.MotionTrail,
                            trailPosition,
                            new Vector2(
                                0.16f + trailPulse * 0.10f,
                                0.82f + trailPulse * 0.82f),
                            rotation);
                        SetPackOpeningGroupAlpha(card.LandingGlowGroup, 0f);

                        if (local < 0.24f)
                        {
                            strongestEjectPulse = Mathf.Max(
                                strongestEjectPulse,
                                1f - local / 0.24f);
                        }
                    }

                    // O pacote apenas cede alguns pixels nesta fase; sua queda
                    // completa acontece quando as cinco cartas já estão fora.
                    // Isso mantém a origem física dos cinco disparos legível.
                    float packYield = EaseInOutCubic(progress);
                    SetPackOpeningCompositePose(
                        view,
                        Mathf.Lerp(0.972f, 0.945f, packYield),
                        new Vector2(0f, Mathf.Lerp(-42f, -72f, packYield)),
                        0f);
                    SetPackOpeningGroupAlpha(view.PackGroup, 1f);
                    SetPackOpeningGroupAlpha(view.FrontLipGroup, 0f);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Clamp01(0.46f + strongestEjectPulse * 0.46f));
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        Mathf.Clamp01(0.44f + strongestEjectPulse * 0.48f));
                    SetPackOpeningGroupAlpha(
                        view.HorizonLineGroup,
                        Mathf.Clamp01(0.18f + strongestEjectPulse * 0.38f));
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.84f, 0.62f, progress),
                        Mathf.Clamp01(0.58f + strongestEjectPulse));
                    AnimatePackOpeningReleaseCurtains(
                        view,
                        progress,
                        Mathf.Clamp01(0.78f + strongestEjectPulse * 0.22f));
                    AnimatePackOpeningParticles(view, progress);
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
                        // O voo ocupa a primeira parte da janela. O restante
                        // representa o contato com a mesa e um rebote curto,
                        // como uma carta fisicamente arremessada e acomodada.
                        float flight = Mathf.Clamp01(local / 0.78f);
                        float landing = Mathf.Clamp01(
                            (local - 0.78f) / 0.22f);
                        float travel = EaseOutCubic(flight);
                        Vector2 position = CubicBezier(
                            card.EjectStagingOffset,
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
                            0.56f,
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
                            1f);
                        Vector2 tangent = CubicBezierDerivative(
                            card.EjectStagingOffset,
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
                                (index - 2f) * 2.1f,
                                0f,
                                EaseOutQuint(flight)) +
                            pathLean * (1f - EaseOutQuint(flight)) +
                            Mathf.Sin(flight * Mathf.PI * 2f) *
                                (1f - flight) * 1.1f +
                            landingWobble);

                        float trailLocal = Mathf.Clamp01(local - 0.055f);
                        Vector2 trailPosition = CubicBezier(
                            card.EjectStagingOffset,
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
                                (index - 2f) * 2.1f,
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
                            Mathf.Lerp(-42f, -layerHeight * 0.88f,
                                packExit)),
                        0f);
                    SetPackOpeningGroupAlpha(
                        view.PackGroup,
                        packExit < 0.995f ? 1f : 0f);
                    SetPackOpeningGroupAlpha(
                        view.FrontLipGroup,
                        0f);
                    SetPackOpeningGroupAlpha(view.TearGlowGroup, 0f);
                    SetPackOpeningGroupAlpha(
                        view.ReleaseBeamGroup,
                        Mathf.Lerp(0.68f, 0f, release) +
                        strongestLaunchPulse * 0.18f);
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.82f, 0.24f, progress),
                        Mathf.Clamp01(0.38f + strongestLaunchPulse));
                    AnimatePackOpeningReleaseCurtains(
                        view,
                        Mathf.Lerp(0.34f, 1f, progress),
                        Mathf.Lerp(1f, 0f, EaseInCubic(progress)));
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
                    SetPackOpeningGroupAlpha(view.HorizonLineGroup, 0f);
                    SetPackOpeningGroupAlpha(view.LightConeGroup, 0f);
                    foreach (CanvasGroup curtainGroup in
                             view.EnergyCurtainGroups)
                    {
                        SetPackOpeningGroupAlpha(curtainGroup, 0f);
                    }
                    SetPackOpeningRarityAura(
                        view,
                        Mathf.Lerp(0.24f, 0f, eased),
                        0f);
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
                // A rotina é atualizada uma vez por frame renderizado (30/60+
                // FPS conforme o aparelho). O teto de 50 ms abaixo é apenas
                // uma proteção contra um hitch isolado; ele não limita a
                // apresentação a 20 FPS nem usa quadros pré-renderizados.
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

        private static void AnimatePackOpeningReleaseCurtains(
            PackOpeningAnimationView view,
            float progress,
            float intensity)
        {
            if (view == null)
                return;

            progress = Mathf.Clamp01(SanitizeFinite(progress, 0f));
            intensity = Mathf.Clamp01(SanitizeFinite(intensity, 0f));
            float rise = EaseOutQuint(progress);
            float verticalTravel = view.PackHeight * 0.76f;
            for (int index = 0; index < view.EnergyCurtains.Count; index++)
            {
                RectTransform curtain = view.EnergyCurtains[index];
                CanvasGroup group = index < view.EnergyCurtainGroups.Count
                    ? view.EnergyCurtainGroups[index]
                    : null;
                float lane = view.EnergyCurtains.Count <= 1
                    ? 0.5f
                    : index / (view.EnergyCurtains.Count - 1f);
                float wave = 0.5f + 0.5f * Mathf.Sin(
                    progress * Mathf.PI * 3f + index * 0.83f);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                SetPackOpeningGroupAlpha(
                    group,
                    envelope * intensity *
                    (index % 2 == 0 ? 0.64f : 0.44f));
                if (curtain == null)
                    continue;

                curtain.anchoredPosition = new Vector2(
                    Mathf.Lerp(-18f, 18f, lane) +
                    Mathf.Sin(progress * Mathf.PI * 2f + index) * 4f,
                    Mathf.Lerp(-verticalTravel * 0.28f,
                        verticalTravel,
                        rise));
                curtain.localScale = new Vector3(
                    Mathf.Lerp(0.46f, 1.06f, wave),
                    Mathf.Lerp(0.42f, 1.24f, rise),
                    1f);
            }
        }

        private void CreatePackOpeningParticles(PackOpeningAnimationView view)
        {
            int particleCount = view.PeakRarity switch
            {
                CardRarity.UR => 30,
                CardRarity.SR => 24,
                CardRarity.R => 18,
                _ => 14
            };
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
                Color particleColor = view.PeakRarity == CardRarity.UR
                    ? Color.HSVToRGB(
                        Mathf.Repeat(index * 0.173205f, 1f),
                        0.72f,
                        1f)
                    : index % 5 == 0
                        ? new Color(1f, 0.98f, 0.88f, 1f)
                        : index % 2 == 0
                            ? view.RarityAccent
                            : Cyan;
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
