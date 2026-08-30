using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Cards;
using ArcaneArena.Presentation;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private sealed class CardSoundPresentationRequest
        {
            public uint Code;
            public string Heading;
            public Color Accent;
            public ArcaneCardSound Sound;
            public bool HideIdentity;
            public bool SummonMethodVfx;
            public MonsterFrameKind SummonFrame;
            public byte Controller;
            public byte Location;
            public uint Sequence;
            public List<uint> MaterialCodes = new();
        }

        private readonly Queue<CardSoundPresentationRequest>
            cardSoundPresentationQueue = new();
        private Coroutine cardSoundPresentationRoutine;
        private CardSoundPresentationRequest activeCardSoundPresentation;
        private ArcaneAudioDirector cardAudioDirector;
        private CardSoundPresentationRequest pendingSummonPresentation;
        private bool cardPresentationDecisionLocked;
        private bool cardPresentationCanAccelerate;
        private bool cardPresentationAccelerated;
        private float lastCardPresentationClick = -10f;

        private void QueueCardSoundPresentation(DuelEvent duelEvent)
        {
            TrackSummonMaterialPresentation(duelEvent);
            if (duelEvent?.Message == CoreMessage.Draw)
            {
                if (!IsTurnDrawEvent(duelEvent))
                {
                    cardAudioDirector ??= GetComponent<ArcaneAudioDirector>();
                    cardAudioDirector?.PlayRapidCardCues(
                        ArcaneCardSound.Draw,
                        Mathf.Max(1, duelEvent.Codes?.Length ?? 1));
                }
                return;
            }
            CardSoundPresentationRequest request =
                CreateCardSoundPresentation(duelEvent);
            if (request == null)
                return;
            if (request.HideIdentity)
            {
                cardAudioDirector ??= GetComponent<ArcaneAudioDirector>();
                cardAudioDirector?.PlayCardCue(request.Sound);
                return;
            }
            cardSoundPresentationQueue.Enqueue(request);
            if (cardSoundPresentationRoutine == null)
            {
                SetCardPresentationDecisionLock(true);
                cardSoundPresentationRoutine =
                    StartCoroutine(PlayCardSoundPresentationQueue());
            }
        }

        private CardSoundPresentationRequest CreateCardSoundPresentation(
            DuelEvent duelEvent)
        {
            if (duelEvent == null)
                return null;
            if (duelEvent.Message == CoreMessage.Summoning ||
                duelEvent.Message == CoreMessage.SpecialSummoning ||
                duelEvent.Message == CoreMessage.FlipSummoning)
            {
                // MSG_*_SUMMONING opens the negation window. Stage the
                // presentation, but do not show a completed summon until the
                // Core emits the matching MSG_*_SUMMONED confirmation.
                pendingSummonPresentation = CreateSummonPresentation(duelEvent);
                return null;
            }
            if (duelEvent.Message == CoreMessage.Summoned ||
                duelEvent.Message == CoreMessage.SpecialSummoned ||
                duelEvent.Message == CoreMessage.FlipSummoned)
            {
                CardSoundPresentationRequest confirmed =
                    state?.LastSummon?.Status ==
                        ArcaneDuel.DuelEngine.State.DuelSummonStatus.Confirmed
                        ? pendingSummonPresentation
                        : null;
                pendingSummonPresentation = null;
                return confirmed;
            }
            if (state?.LastSummon?.Status ==
                    ArcaneDuel.DuelEngine.State.DuelSummonStatus.Negated)
            {
                pendingSummonPresentation = null;
            }
            if (duelEvent.Message == CoreMessage.Chaining)
            {
                return new CardSoundPresentationRequest
                {
                    Code = duelEvent.Code,
                    Heading = $"CORRENTE · ELO {Mathf.Max(1, (int)duelEvent.Value)}",
                    Accent = Gold,
                    Sound = ActivationSoundFor(duelEvent.Code)
                };
            }
            if (IsFaceDownPlacement(duelEvent))
            {
                return new CardSoundPresentationRequest
                {
                    Code = 0,
                    Heading = "CARTA BAIXADA",
                    Accent = Gold,
                    Sound = ArcaneCardSound.PutCard,
                    HideIdentity = true
                };
            }
            return null;
        }

        private CardSoundPresentationRequest CreateSummonPresentation(
            DuelEvent duelEvent)
        {
            MonsterFrameKind summonFrame =
                LegacyEntryFor(duelEvent.Code)?.MonsterFrame ??
                MonsterFrameKind.Unknown;
            return new CardSoundPresentationRequest
            {
                Code = duelEvent.Code,
                Heading = SummonPresentationHeading(duelEvent.Code),
                Accent = SummonMethodVfxPalette.Supports(summonFrame)
                    ? SummonMethodVfxPalette.Primary(summonFrame)
                    : Cyan,
                Sound = duelEvent.Message == CoreMessage.FlipSummoning
                    ? ArcaneCardSound.None
                    : SummonSoundFor(duelEvent.Code),
                SummonMethodVfx =
                    HasDistinctSummonPresentation(duelEvent.Code),
                SummonFrame = summonFrame,
                Controller = duelEvent.Current?.Controller ?? duelEvent.Player,
                Location = duelEvent.Current?.Location ?? 0,
                Sequence = duelEvent.Current?.Sequence ?? 0,
                MaterialCodes = ConsumeSummonMaterialCodes(
                    summonFrame,
                    duelEvent.Current?.Controller ?? duelEvent.Player)
            };
        }

        private ArcaneCardSound SummonSoundFor(uint code)
        {
            CardCatalogEntry entry = LegacyEntryFor(code);
            return entry?.MonsterFrame switch
            {
                MonsterFrameKind.Fusion => ArcaneCardSound.Fusion,
                MonsterFrameKind.Synchro => ArcaneCardSound.Synchro,
                MonsterFrameKind.Xyz => ArcaneCardSound.Xyz,
                // Link and Pendulum currently share the neutral summon cue;
                // using a real clip keeps their cinematic duration tied to
                // audio just like Fusion, Synchro and Xyz.
                MonsterFrameKind.Link => ArcaneCardSound.MonsterSummon,
                MonsterFrameKind.Pendulum => ArcaneCardSound.MonsterSummon,
                _ => ArcaneCardSound.MonsterSummon
            };
        }

        private ArcaneCardSound ActivationSoundFor(uint code)
        {
            return LegacyEntryFor(code)?.Category switch
            {
                CardCategory.Spell => ArcaneCardSound.Magic,
                CardCategory.Trap => ArcaneCardSound.Trap,
                CardCategory.Monster => ArcaneCardSound.Magic,
                _ => ArcaneCardSound.None
            };
        }

        private static bool IsFaceDownPlacement(DuelEvent duelEvent)
        {
            if (duelEvent == null ||
                duelEvent.Message != CoreMessage.Move ||
                duelEvent.Current == null)
            {
                return false;
            }
            bool wasAlreadyOnField =
                duelEvent.Previous != null &&
                ((duelEvent.Previous.Location &
                  DuelLocation.MonsterZone) != 0 ||
                 (duelEvent.Previous.Location &
                  DuelLocation.SpellTrapZone) != 0);
            if (wasAlreadyOnField)
            {
                // A card flipped face-down by an effect stayed on the field;
                // it was not Set from hand/deck, so do not replay the Set
                // placement sound.
                return false;
            }
            uint location = duelEvent.Current.Location;
            bool fieldZone =
                (location & DuelLocation.MonsterZone) != 0 ||
                (location & DuelLocation.SpellTrapZone) != 0;
            bool faceDown =
                (duelEvent.Current.Position &
                 (FaceDownAttack | FaceDownDefense)) != 0;
            return fieldZone && faceDown;
        }

        private IEnumerator PlayCardSoundPresentationQueue()
        {
            try
            {
                while (cardSoundPresentationQueue.Count > 0)
                {
                    CardSoundPresentationRequest request =
                        cardSoundPresentationQueue.Dequeue();
                    activeCardSoundPresentation = request;
                    yield return ShowCardPresentation(
                        request.Code,
                        request.Heading,
                        request.Accent,
                        request.Sound,
                        request.HideIdentity,
                        request.SummonMethodVfx,
                        request.SummonFrame,
                        request.MaterialCodes);
                    // The trigger/selection window belongs after the card is
                    // visibly established on its zone, not merely after the
                    // enlarged summon artwork has faded away.
                    yield return ReleaseDeferredMonsterArrival(request);
                    activeCardSoundPresentation = null;
                }
            }
            finally
            {
                cardSoundPresentationRoutine = null;
                activeCardSoundPresentation = null;
                SetCardPresentationDecisionLock(false);
                observedPrompt = null;
                if (presentationReady)
                    RefreshEverything(true);
            }
        }

        private void UpdateCardPresentationAcceleration()
        {
            if (!cardPresentationCanAccelerate ||
                cardPresentationAccelerated)
                return;
            bool pressed =
                UnityEngine.InputSystem.Mouse.current?.leftButton
                    .wasPressedThisFrame == true ||
                UnityEngine.InputSystem.Touchscreen.current?.primaryTouch
                    .press.wasPressedThisFrame == true;
            if (!pressed)
                return;
            float now = Time.unscaledTime;
            if (now - lastCardPresentationClick <= 0.36f)
            {
                cardPresentationAccelerated = true;
                cardAudioDirector?.FadeOutCardCue(0.38f);
                lastCardPresentationClick = -10f;
            }
            else
            {
                lastCardPresentationClick = now;
            }
        }

        private void ResetCardSoundPresentation()
        {
            cardPresentationCanAccelerate = false;
            cardPresentationAccelerated = false;
            lastCardPresentationClick = -10f;
            cardAudioDirector?.StopCardCue();
            pendingSummonPresentation = null;
            ResetSummonMaterialPresentation();
            activeCardSoundPresentation = null;
            cardSoundPresentationQueue.Clear();
            cardSoundPresentationRoutine = null;
            CancelDeferredMonsterArrivals();
            SetCardPresentationDecisionLock(false);
        }

        private void SetCardPresentationDecisionLock(bool locked)
        {
            cardPresentationDecisionLocked = locked;
            core?.SetPresentationDecisionLocked(locked);
        }

        private bool HasDistinctSummonPresentation(uint code)
        {
            MonsterFrameKind frame =
                LegacyEntryFor(code)?.MonsterFrame ??
                MonsterFrameKind.Unknown;
            return SummonMethodVfxPalette.Supports(frame);
        }

        private string SummonPresentationHeading(uint code)
        {
            return LegacyEntryFor(code)?.MonsterFrame switch
            {
                MonsterFrameKind.Fusion => "INVOCAÇÃO-FUSÃO",
                MonsterFrameKind.Synchro => "INVOCAÇÃO-SINCRO",
                MonsterFrameKind.Xyz => "INVOCAÇÃO-XYZ",
                MonsterFrameKind.Link => "INVOCAÇÃO-LINK",
                MonsterFrameKind.Pendulum => "INVOCAÇÃO-PÊNDULO",
                _ => "INVOCAÇÃO DE MONSTRO"
            };
        }
    }
}
