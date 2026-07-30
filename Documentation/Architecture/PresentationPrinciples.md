# Presentation Principles

## Direction

The new project should retain the strongest qualities remembered from the
previous prototype: a readable field, attractive panels, clear card focus,
useful feedback, and an arena that feels alive. It should improve hierarchy,
spacing, motion, accessibility, and consistency.

The previous project's `Card Arena Bootstrap` approach is explicitly rejected.
No single bootstrap component may construct the final arena, own duel rules,
drive every interface, or become the source of truth for card state.

## Required flow

```text
ygopro-core
  -> typed protocol messages
  -> presentation state projector
  -> presentation queue
  -> scene views, UI, animation, and audio
```

- The duel engine decides legality and outcomes.
- Views bind to logical card references, never the other way around.
- Player-choice UI exposes only candidates provided by the engine.
- Animation can delay presentation, but never rules processing.
- A scene can be rebuilt from an engine snapshot.
- Bootstrap is limited to composition, diagnostics, and scene transition.
- Large presentation features are split by responsibility and assembly.

## Visual goals for the later UI phase

- Dark, premium arena with restrained gold and arcane-blue accents.
- High contrast between playable cards, unavailable cards, targets, and
  resolving chain links.
- A persistent but compact phase/priority display.
- Card inspection that remains readable at 1080p.
- Motion that communicates draw, move, summon, chain, battle, damage, and
  recovery without obscuring player choices.
- Scalable panels and text for different desktop resolutions.

Final UI production begins only after the headless duel and the protocol
boundary are proven, as required by the project plan.

