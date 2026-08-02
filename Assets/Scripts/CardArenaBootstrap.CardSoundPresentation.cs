using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Cards;
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
            public bool ExtraDeckSummon;
        }

        private readonly Queue<CardSoundPresentationRequest>
            cardSoundPresentationQueue = new();
        private Coroutine cardSoundPresentationRoutine;
        private ArcaneAudioDirector cardAudioDirector;
        private bool cardPresentationDecisionLocked;
        private bool cardPresentationCanAccelerate;
        private bool cardPresentationAccelerated;
        private float lastCardPresentationClick = -10f;

        private void QueueCardSoundPresentation(DuelEvent duelEvent)
        {
            if (duelEvent?.Message == CoreMessage.Draw)
            {
                if (!IsTurnDrawEvent(duelEvent))
                {
                    cardAudioDirector ??= GetComponent<ArcaneAudioDirector>();
                    cardAudioDirector?.PlayCardCue(ArcaneCardSound.Draw);
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
                return new CardSoundPresentationRequest
                {
                    Code = duelEvent.Code,
                    Heading = SummonPresentationHeading(duelEvent.Code),
                    Accent = Cyan,
                    Sound = duelEvent.Message == CoreMessage.FlipSummoning
                        ? ArcaneCardSound.None
                        : SummonSoundFor(duelEvent.Code),
                    ExtraDeckSummon =
                        IsExtraDeckSummonPresentation(duelEvent.Code)
                };
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

        private ArcaneCardSound SummonSoundFor(uint code)
        {
            CardCatalogEntry entry = LegacyEntryFor(code);
            return entry?.MonsterFrame switch
            {
                MonsterFrameKind.Fusion => ArcaneCardSound.Fusion,
                MonsterFrameKind.Synchro => ArcaneCardSound.Synchro,
                MonsterFrameKind.Xyz => ArcaneCardSound.Xyz,
                MonsterFrameKind.Link => ArcaneCardSound.None,
                MonsterFrameKind.Pendulum => ArcaneCardSound.None,
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
                    yield return ShowCardPresentation(
                        request.Code,
                        request.Heading,
                        request.Accent,
                        request.Sound,
                        request.HideIdentity,
                        request.ExtraDeckSummon);
                }
            }
            finally
            {
                cardSoundPresentationRoutine = null;
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
            cardSoundPresentationQueue.Clear();
            cardSoundPresentationRoutine = null;
            SetCardPresentationDecisionLock(false);
        }

        private void SetCardPresentationDecisionLock(bool locked)
        {
            cardPresentationDecisionLocked = locked;
            core?.SetPresentationDecisionLocked(locked);
        }

        private bool IsExtraDeckSummonPresentation(uint code)
        {
            MonsterFrameKind frame =
                LegacyEntryFor(code)?.MonsterFrame ??
                MonsterFrameKind.Unknown;
            return frame == MonsterFrameKind.Fusion ||
                   frame == MonsterFrameKind.Synchro ||
                   frame == MonsterFrameKind.Xyz ||
                   frame == MonsterFrameKind.Link;
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
