using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private readonly Dictionary<int, Image> _packRevealCards = new();
        private PendingPackOpeningRecord _packRevealGestureOpening;
        private Coroutine _packRevealAllRoutine;
        private bool _packRevealSwipeTracking;
        private Vector2 _packRevealSwipeStart;
        private float _packRevealSwipeStartedAt;

        private bool IsPackRevealAllActive =>
            _packRevealAllRoutine != null;

        private void ResetPackRevealSwipe(PendingPackOpeningRecord opening)
        {
            if (_packRevealAllRoutine != null)
                StopCoroutine(_packRevealAllRoutine);
            _packRevealAllRoutine = null;
            _packRevealSwipeTracking = false;
            _packRevealGestureOpening = opening;
            _packRevealCards.Clear();
        }

        private void CancelPackRevealSwipe()
        {
            if (_packRevealAllRoutine != null)
                StopCoroutine(_packRevealAllRoutine);
            _packRevealAllRoutine = null;
            _packRevealSwipeTracking = false;
            _packRevealGestureOpening = null;
            _packRevealCards.Clear();
        }

        private void RegisterPackRevealCard(int index, Image card)
        {
            if (card != null)
                _packRevealCards[index] = card;
        }

        private void UpdatePackRevealSwipe()
        {
            if (_packRevealGestureOpening == null ||
                _packRevealAllRoutine != null ||
                _packOpeningSequenceActive ||
                _packRevealBusy ||
                _packRevealGestureOpening.revealed == null ||
                _packRevealGestureOpening.revealed.All(value => value))
            {
                _packRevealSwipeTracking = false;
                return;
            }

            bool pressed;
            bool released;
            bool held;
            Vector2 position;
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.isPressed)
            {
                pressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
                released = Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
                held = true;
                position = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                pressed = Mouse.current.leftButton.wasPressedThisFrame;
                released = Mouse.current.leftButton.wasReleasedThisFrame;
                held = Mouse.current.leftButton.isPressed;
                position = Mouse.current.position.ReadValue();
            }
            else
            {
                _packRevealSwipeTracking = false;
                return;
            }

            if (pressed)
            {
                _packRevealSwipeTracking = true;
                _packRevealSwipeStart = position;
                _packRevealSwipeStartedAt = Time.unscaledTime;
                return;
            }

            if (!_packRevealSwipeTracking)
                return;

            float elapsed = Time.unscaledTime - _packRevealSwipeStartedAt;
            Vector2 delta = position - _packRevealSwipeStart;
            float threshold = Mathf.Max(90f, Screen.height * 0.115f);
            bool fastVerticalGesture = elapsed <= 0.60f &&
                Mathf.Abs(delta.y) >= threshold &&
                Mathf.Abs(delta.y) >= Mathf.Abs(delta.x) * 1.35f;
            if (fastVerticalGesture)
            {
                _packRevealSwipeTracking = false;
                _packRevealAllRoutine = StartCoroutine(
                    RevealAllPackCards(_packRevealGestureOpening));
                return;
            }

            if (released || !held || elapsed > 0.60f)
                _packRevealSwipeTracking = false;
        }

        private IEnumerator RevealAllPackCards(
            PendingPackOpeningRecord opening)
        {
            if (opening?.revealed == null || opening.cardIds == null)
            {
                _packRevealAllRoutine = null;
                yield break;
            }

            List<int> revealOrder = Enumerable.Range(0, opening.cardIds.Count)
                .Where(index => index < opening.revealed.Count &&
                    !opening.revealed[index] &&
                    _packRevealCards.ContainsKey(index))
                .OrderBy(index => PackRarityDistribution.ResolveCardRarity(
                    DeckRepository.ResolveCard(_catalog, opening.cardIds[index])))
                .ThenBy(index => index)
                .ToList();

            foreach (int index in revealOrder)
            {
                if (opening.revealed[index])
                    continue;
                if (!_packRevealCards.TryGetValue(index, out Image card) ||
                    card == null)
                {
                    continue;
                }

                yield return RevealPackCard(opening, index, card);
                yield return new WaitForSecondsRealtime(0.055f);
            }

            _packRevealAllRoutine = null;
        }
    }
}
