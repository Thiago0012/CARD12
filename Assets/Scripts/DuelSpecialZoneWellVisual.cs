using ArcaneArena.Multiplayer;
using UnityEngine;

namespace ArcaneArena
{
    /// <summary>
    /// Presentation-only feedback for the physical Graveyard and Banishment
    /// fixtures. Authoritative contents remain in DuelPresentationState.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelSpecialZoneWellVisual : MonoBehaviour
    {
        [SerializeField] private DuelZoneKind kind;
        [SerializeField] private Renderer energyCore;
        [SerializeField] private Renderer[] runeAccents;
        [SerializeField] private Color accent = Color.cyan;
        [SerializeField] private int cardCount;

        // Unity API wrappers must not be constructed by a MonoBehaviour field
        // initializer. Awake/EnsurePropertyBlock run inside Unity's supported
        // lifecycle and also cover Configure being invoked immediately after
        // AddComponent.
        private MaterialPropertyBlock propertyBlock;
        private Vector3 coreBaseScale;
        private float arrivalPulseUntil;
        private float ingressProgress;
        private float ingressHoldUntil;
        private float phase;

        public int CardCount => cardCount;
        public Color AccentColor => accent;

        public void Configure(
            DuelZoneKind zoneKind,
            Renderer core,
            Renderer[] runes,
            Color color)
        {
            kind = zoneKind;
            energyCore = core;
            runeAccents = runes;
            accent = color;
            coreBaseScale = energyCore != null
                ? energyCore.transform.localScale
                : Vector3.one;
            phase = zoneKind == DuelZoneKind.Banishment ? 1.7f : 0.2f;
            ApplyVisual(0f);
        }

        public void SetCardCount(int value)
        {
            cardCount = Mathf.Max(0, value);
        }

        public void PlayArrivalPulse()
        {
            ingressProgress = 0f;
            arrivalPulseUntil = Time.unscaledTime + 0.56f;
        }

        /// <summary>
        /// Opens the presentation well before a card crosses its rim. This is
        /// visual feedback only; the authoritative card has already moved in
        /// DuelPresentationState when this method is called.
        /// </summary>
        public void BeginIngress()
        {
            ingressProgress = Mathf.Max(ingressProgress, 0.02f);
            ingressHoldUntil = Time.unscaledTime + 0.10f;
        }

        public void SetIngressProgress(float normalizedProgress)
        {
            ingressProgress = Mathf.Clamp01(normalizedProgress);
            ingressHoldUntil = Time.unscaledTime + 0.08f;
        }

        private void Awake()
        {
            EnsurePropertyBlock();
            if (energyCore != null)
                coreBaseScale = energyCore.transform.localScale;
        }

        private void Update()
        {
            float idle = 0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime * 2.25f + phase);
            float arrival = Mathf.Clamp01(
                (arrivalPulseUntil - Time.unscaledTime) / 0.56f);
            arrival = Mathf.Sin(arrival * Mathf.PI);
            if (Time.unscaledTime > ingressHoldUntil)
            {
                ingressProgress = Mathf.MoveTowards(
                    ingressProgress,
                    0f,
                    Time.unscaledDeltaTime * 3.5f);
            }
            float ingressWave = Mathf.Sin(
                Mathf.SmoothStep(0f, 1f, ingressProgress) * Mathf.PI);
            float ingressSeal = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.72f, 1f, ingressProgress));
            ApplyVisual(
                idle * 0.22f + arrival + ingressWave * 0.92f +
                ingressSeal * 0.48f);

            if (energyCore != null)
            {
                float countEnergy = Mathf.Clamp01(cardCount / 8f) * 0.025f;
                float pulseScale = 1f + idle * 0.018f +
                                   arrival * 0.16f + countEnergy +
                                   ingressWave * 0.11f -
                                   ingressSeal * 0.055f;
                energyCore.transform.localScale =
                    coreBaseScale * pulseScale;
            }
        }

        private void ApplyVisual(float intensity)
        {
            Color lit = Color.Lerp(
                accent * 0.64f,
                Color.Lerp(accent, Color.white, 0.42f),
                Mathf.Clamp01(intensity));
            ApplyRendererColor(energyCore, lit, intensity);
            if (runeAccents == null)
                return;
            for (int index = 0; index < runeAccents.Length; index++)
            {
                float alternate = index % 2 == 0 ? 1f : 0.72f;
                ApplyRendererColor(
                    runeAccents[index],
                    Color.Lerp(accent * 0.58f, lit, alternate),
                    intensity * alternate);
            }
        }

        private void ApplyRendererColor(
            Renderer target,
            Color color,
            float intensity)
        {
            if (target == null)
                return;
            EnsurePropertyBlock();
            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor(
                "_EmissionColor",
                color * Mathf.Lerp(0.55f, 2.2f, Mathf.Clamp01(intensity)));
            target.SetPropertyBlock(propertyBlock);
        }

        private void EnsurePropertyBlock()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }
    }
}
