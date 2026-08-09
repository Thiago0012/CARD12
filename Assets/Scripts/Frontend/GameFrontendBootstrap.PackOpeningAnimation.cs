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
        private float packOpeningFadeDuration = 0.30f;
        [SerializeField, Range(0.05f, 1.5f)]
        private float packOpeningEnterDuration = 0.50f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningAnticipationDuration = 0.35f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningTearDuration = 0.40f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningFlapDuration = 0.35f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningBurstDuration = 0.30f;
        [SerializeField, Range(0.05f, 1f)]
        private float packOpeningStackRiseDuration = 0.35f;
        [SerializeField, Range(0.10f, 1.5f)]
        private float packOpeningFanDuration = 0.60f;
        [SerializeField, Range(0f, 0.20f)]
        private float packOpeningCardStagger = 0.07f;
        [SerializeField, Range(0.05f, 0.75f)]
        private float packOpeningSettleDuration = 0.25f;

        private sealed class PackOpeningAnimationCard
        {
            public RectTransform Rect;
            public Button Button;
            public CanvasGroup CanvasGroup;
            public Vector2 FinalMin;
            public Vector2 FinalMax;
            public Vector2 StackMin;
            public Vector2 StackMax;
            public Vector2 StageMin;
            public Vector2 StageMax;
            public float StackRotation;
        }

        private sealed class PackOpeningParticle
        {
            public RectTransform Rect;
            public CanvasGroup CanvasGroup;
            public Vector2 Start;
            public Vector2 End;
        }

        private sealed class PackOpeningAnimationView
        {
            public RectTransform Layer;
            public Image DimOverlay;
            public RectTransform RearGlow;
            public CanvasGroup RearGlowGroup;
            public RectTransform PackRoot;
            public CanvasGroup PackGroup;
            public Image InnerDark;
            public RectTransform LeftFlap;
            public RectTransform RightFlap;
            public RectTransform FrontLip;
            public CanvasGroup FrontLipGroup;
            public RectTransform TearGlow;
            public CanvasGroup TearGlowGroup;
            public RectTransform SkipButton;
            public Text RevealInstruction;
            public Vector2 PackBasePosition;
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

            var view = new PackOpeningAnimationView
            {
                Layer = blocker.rectTransform
            };

            view.DimOverlay = CreatePanel(
                view.Layer,
                "Escurecimento Cinematográfico",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0.01f, 0.035f, 0f));
            view.DimOverlay.raycastTarget = false;

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
            Image tear = CreatePackOpeningSizedImage(
                view.Layer,
                "Linha de Energia do Rasgo",
                view.TearBasePosition,
                new Vector2(view.PackWidth * 0.92f, 7f),
                new Color(0.75f, 1f, 1f, 1f));
            tear.raycastTarget = false;
            view.TearGlow = tear.rectTransform;
            view.TearGlowGroup = AddPackOpeningCanvasGroup(tear.gameObject, 0f);

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

            SetPackOpeningCompositePose(view, 0.72f, Vector2.zero, 0f);
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

            float depthOffset = (index - 2) * 0.0018f;
            var startMin = new Vector2(0.425f + depthOffset, 0.18f + depthOffset);
            var startMax = new Vector2(0.575f + depthOffset, 0.63f + depthOffset);
            var stageMin = new Vector2(0.425f + depthOffset, 0.31f + depthOffset);
            var stageMax = new Vector2(0.575f + depthOffset, 0.76f + depthOffset);
            RectTransform rect = card.rectTransform;
            SetPackOpeningCardRect(rect, startMin, startMax, 0.60f,
                (index - 2) * 0.8f);
            CanvasGroup group = AddPackOpeningCanvasGroup(card.gameObject, 0f);
            view.Cards.Add(new PackOpeningAnimationCard
            {
                Rect = rect,
                Button = button,
                CanvasGroup = group,
                FinalMin = finalMin,
                FinalMax = finalMax,
                StackMin = startMin,
                StackMax = startMax,
                StageMin = stageMin,
                StageMax = stageMax,
                StackRotation = (index - 2) * 0.8f
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
                    float eased = EaseInOutCubic(progress);
                    SetPackOpeningImageAlpha(
                        view.DimOverlay,
                        Mathf.Lerp(0f, 0.48f, eased));
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.PackEnter;
            yield return AnimatePackOpeningPhase(
                packOpeningEnterDuration,
                progress =>
                {
                    float eased = EaseOutBack(progress, 0.09f);
                    float scale = Mathf.LerpUnclamped(0.72f, 1f, eased);
                    SetPackOpeningCompositePose(view, scale, Vector2.zero, 0f);
                    SetPackOpeningGroupAlpha(view.PackGroup, progress);
                    SetPackOpeningGroupAlpha(view.FrontLipGroup, progress);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Sin(progress * Mathf.PI) * 0.16f);
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.Anticipation;
            yield return AnimatePackOpeningPhase(
                packOpeningAnticipationDuration,
                progress =>
                {
                    float pulse = Mathf.Sin(progress * Mathf.PI) * 0.026f;
                    float shake = Mathf.Sin(progress * Mathf.PI * 6f) *
                        (1f - progress) * 0.42f;
                    SetPackOpeningCompositePose(
                        view,
                        1f + pulse,
                        Vector2.zero,
                        shake);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        0.12f + pulse * 5f);
                });

            _packOpeningPresentationState = PackOpeningPresentationState.Tear;
            yield return AnimatePackOpeningPhase(
                packOpeningTearDuration,
                progress =>
                {
                    float eased = EaseInOutCubic(progress);
                    float linePulse = Mathf.Clamp01(
                        Mathf.Sin(progress * Mathf.PI) * 1.35f);
                    SetPackOpeningGroupAlpha(view.TearGlowGroup, linePulse);
                    view.TearGlow.localScale = new Vector3(
                        Mathf.Lerp(0.04f, 1f, eased),
                        1f + linePulse * 1.7f,
                        1f);
                    SetPackOpeningImageAlpha(
                        view.InnerDark,
                        Mathf.Lerp(0f, 0.98f, eased));
                    float microShake = Mathf.Sin(progress * Mathf.PI * 12f) *
                        linePulse * 2.4f;
                    SetPackOpeningCompositePose(
                        view,
                        1f,
                        new Vector2(microShake, 0f),
                        0f);
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.FlapOpen;
            yield return AnimatePackOpeningPhase(
                packOpeningFlapDuration,
                progress =>
                {
                    float eased = EaseOutBack(progress, 0.07f);
                    view.LeftFlap.localRotation = Quaternion.Euler(
                        0f, 0f, Mathf.LerpUnclamped(0f, -25f, eased));
                    view.RightFlap.localRotation = Quaternion.Euler(
                        0f, 0f, Mathf.LerpUnclamped(0f, 25f, eased));
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
                });

            _packOpeningPresentationState = PackOpeningPresentationState.Burst;
            yield return AnimatePackOpeningPhase(
                packOpeningBurstDuration,
                progress =>
                {
                    float flash = Mathf.Sin(progress * Mathf.PI);
                    SetPackOpeningGroupAlpha(view.RearGlowGroup, flash * 0.92f);
                    view.RearGlow.localScale = Vector3.one *
                        Mathf.Lerp(0.58f, 1.46f, EaseOutCubic(progress));
                    AnimatePackOpeningParticles(view, progress);
                });

            _packOpeningPresentationState =
                PackOpeningPresentationState.StackRise;
            yield return AnimatePackOpeningPhase(
                packOpeningStackRiseDuration,
                progress =>
                {
                    float eased = EaseOutCubic(progress);
                    foreach (PackOpeningAnimationCard card in view.Cards)
                    {
                        if (card.Rect == null)
                            continue;
                        SetPackOpeningGroupAlpha(card.CanvasGroup, progress);
                        SetPackOpeningCardRect(
                            card.Rect,
                            Vector2.Lerp(card.StackMin, card.StageMin, eased),
                            Vector2.Lerp(card.StackMax, card.StageMax, eased),
                            0.60f,
                            card.StackRotation * (1f - eased * 0.25f));
                    }
                    view.RearGlow.anchoredPosition =
                        new Vector2(0f, Mathf.Lerp(0f, 48f, eased));
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Lerp(0.35f, 0.62f, eased));
                });

            _packOpeningPresentationState = PackOpeningPresentationState.FanOut;
            float fanTotal = packOpeningFanDuration +
                packOpeningCardStagger * 2f;
            yield return AnimatePackOpeningPhase(
                fanTotal,
                progress =>
                {
                    float elapsed = progress * fanTotal;
                    for (int index = 0; index < view.Cards.Count; index++)
                    {
                        PackOpeningAnimationCard card = view.Cards[index];
                        if (card.Rect == null)
                            continue;
                        float distanceFromCenter = Mathf.Abs(index -
                            (view.Cards.Count - 1) * 0.5f);
                        float delay = distanceFromCenter *
                            packOpeningCardStagger;
                        float local = Mathf.Clamp01(
                            (elapsed - delay) /
                            Mathf.Max(0.01f, packOpeningFanDuration));
                        float eased = EaseInOutCubic(local);
                        SetPackOpeningCardRect(
                            card.Rect,
                            Vector2.Lerp(card.StageMin, card.FinalMin, eased),
                            Vector2.Lerp(card.StageMax, card.FinalMax, eased),
                            Mathf.Lerp(0.60f, 1.035f, EaseOutCubic(local)),
                            Mathf.Lerp(card.StackRotation * 0.75f, 0f, eased));
                    }
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Lerp(0.62f, 0.18f, progress));
                });

            _packOpeningPresentationState = PackOpeningPresentationState.Settle;
            yield return AnimatePackOpeningPhase(
                packOpeningSettleDuration,
                progress =>
                {
                    float eased = EaseOutCubic(progress);
                    foreach (PackOpeningAnimationCard card in view.Cards)
                    {
                        if (card.Rect == null)
                            continue;
                        SetPackOpeningCardRect(
                            card.Rect,
                            card.FinalMin,
                            card.FinalMax,
                            Mathf.Lerp(1.035f, 1f, eased),
                            0f);
                    }
                    SetPackOpeningGroupAlpha(
                        view.PackGroup,
                        1f - eased);
                    SetPackOpeningGroupAlpha(
                        view.FrontLipGroup,
                        1f - eased);
                    SetPackOpeningGroupAlpha(
                        view.RearGlowGroup,
                        Mathf.Lerp(0.18f, 0f, eased));
                    SetPackOpeningImageAlpha(
                        view.DimOverlay,
                        Mathf.Lerp(0.48f, 0f, eased));
                });

            CompletePackOpeningPresentation(view);
        }

        private IEnumerator AnimatePackOpeningPhase(
            float duration,
            Action<float> update)
        {
            if (_packOpeningSkipRequested || duration <= 0f)
            {
                update?.Invoke(1f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && !_packOpeningSkipRequested)
            {
                elapsed += Mathf.Max(0.0001f, Time.unscaledDeltaTime);
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
                    SetPackOpeningCardRect(
                        card.Rect,
                        card.FinalMin,
                        card.FinalMax,
                        1f,
                        0f);
                    RestorePackOpeningCardInteraction(card);
                    if (card.Button != null)
                        card.Button.interactable = true;
                    card.Rect.SetAsLastSibling();
                }

                if (view.RevealInstruction != null)
                    view.RevealInstruction.gameObject.SetActive(true);
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
                    pixels[y * size + x] = new Color(
                        0.32f,
                        0.95f,
                        1f,
                        alpha);
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
            _packOpeningGlowSprite.name = "Arcane Pack Opening Glow";
            _packOpeningGlowSprite.hideFlags = HideFlags.DontSave;
            return _packOpeningGlowSprite;
        }

        private void CreatePackOpeningParticles(PackOpeningAnimationView view)
        {
            for (int index = 0; index < 10; index++)
            {
                float normalized = index / 9f;
                float angle = Mathf.Lerp(-155f, -25f, normalized) *
                    Mathf.Deg2Rad;
                float distance = Mathf.Lerp(70f, 190f,
                    Mathf.Repeat(index * 0.37f, 1f));
                Vector2 start = view.PackBasePosition +
                    new Vector2(0f, view.PackHeight * 0.30f);
                Vector2 end = start + new Vector2(
                    Mathf.Cos(angle) * distance,
                    -Mathf.Sin(angle) * distance + 42f);
                Image particle = CreatePackOpeningSizedImage(
                    view.Layer,
                    $"Partícula do Pacote {index + 1}",
                    start,
                    new Vector2(5f + index % 3 * 2f, 14f + index % 4 * 3f),
                    index % 2 == 0 ? Cyan : Gold);
                particle.raycastTarget = false;
                particle.rectTransform.localRotation = Quaternion.Euler(
                    0f, 0f, index * 31f);
                CanvasGroup group = AddPackOpeningCanvasGroup(
                    particle.gameObject,
                    0f);
                view.Particles.Add(new PackOpeningParticle
                {
                    Rect = particle.rectTransform,
                    CanvasGroup = group,
                    Start = start,
                    End = end
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

        private static void SetPackOpeningCardRect(
            RectTransform rect,
            Vector2 min,
            Vector2 max,
            float scale,
            float rotation)
        {
            if (rect == null)
                return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static void SetPackOpeningGroupAlpha(
            CanvasGroup group,
            float alpha)
        {
            if (group != null)
                group.alpha = Mathf.Clamp01(alpha);
        }

        private static void SetPackOpeningImageAlpha(Image image, float alpha)
        {
            if (image == null)
                return;
            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
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
