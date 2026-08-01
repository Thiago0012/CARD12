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
        }

        private readonly Queue<CardSoundPresentationRequest>
            cardSoundPresentationQueue = new();
        private Coroutine cardSoundPresentationRoutine;
        private ArcaneAudioDirector cardAudioDirector;
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
            cardSoundPresentationQueue.Enqueue(request);
            if (cardSoundPresentationRoutine == null)
            {
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
                    Heading = "INVOCAÇÃO DE MONSTRO",
                    Accent = Cyan,
                    Sound = duelEvent.Message == CoreMessage.FlipSummoning
                        ? ArcaneCardSound.None
                        : SummonSoundFor(duelEvent.Code)
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
                _ => ArcaneCardSound.None
            };
        }

        private static bool IsFaceDownPlacement(DuelEvent duelEvent)
        {
            if (duelEvent == null ||
                (duelEvent.Message != CoreMessage.Move &&
                 duelEvent.Message != CoreMessage.PositionChange) ||
                duelEvent.Current == null)
            {
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
            while (cardSoundPresentationQueue.Count > 0)
            {
                CardSoundPresentationRequest request =
                    cardSoundPresentationQueue.Dequeue();
                yield return ShowCardPresentation(
                    request.Code,
                    request.Heading,
                    request.Accent,
                    request.Sound,
                    request.HideIdentity);
            }
            cardSoundPresentationRoutine = null;
        }

        private void UpdateCardPresentationAcceleration()
        {
            if (!cardPresentationCanAccelerate)
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
                cardAudioDirector?.AccelerateCardCue();
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
        }
    }
}
