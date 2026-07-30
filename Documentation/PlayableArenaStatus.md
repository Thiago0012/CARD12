# Playable Arena Status

Date: 2026-07-29  
Unity: 6000.5.0f1  
Core API: 11.0  
Target: Windows x64

## Result

The project boots through an original title portal into a playable local duel.
The arena never calculates a game rule in UI code. Legal commands come from
`ygopro-core`, become typed `DuelPrompt` objects, and return as exact protocol
responses.

## Implemented plan phases

### Phase 4 — Safe interop

- Pinned ABI layouts and offsets are verified in tests.
- Native duel ownership uses `SafeHandle`.
- Callback delegates and payload handles remain strongly rooted.
- Per-card setcode allocations are terminated and released by
  `OCG_DataReaderDone`.
- Native messages are copied before decoding.

### Phase 5 — Local content

- `Tools/CardDbCompiler/compile_cards.py` deterministically builds
  `cards.bin` and `card-texts.json`.
- `Tools/SyncYgoContent.ps1` copies root utility scripts plus only the
  catalog's selected official scripts.
- Custom scripts have lookup priority over official scripts.
- The authored catalog remains exactly 200 unique official codes.
- The compiled Core database contains those 200 plus 61 official compatibility
  records used by the preserved 193-card presentation catalog and saved decks.

### Phase 6 — Headless duel

- Fixed seeds reproduce the same event signature.
- A repeated-seed test advances past a full turn.
- A deterministic integration duel reaches terminal `WIN`.
- Duel destruction is covered by deterministic `Dispose`.

### Phases 7-8 — Protocol and presentation

- Packet framing and bounded reads reject truncation.
- Turn, phase, draw, move, LP, summon, chain, win, idle, battle, card,
  tribute, chain, place, position, option, and yes/no flows are typed.
- Every card/zone prompt preserves controller, location, and sequence so the
  presentation can map Core choices to direct clicks.
- Unimplemented native messages remain explicit diagnostics.
- Presentation state and GUI never hold native pointers.

### Phase 9 — Complete local presentation

- Original title art and portal, rules/support panels, duel-mode selection,
  themed deck selection, deck gallery/editor/shop, and animation options.
- The authored menu, deck, shop, options, and 3D arena hierarchy from the
  previous project is preserved as presentation only. Its former duel engine
  and rule resolver are not present; every legal action comes from the new
  `ygopro-core` prompt bridge.
- Perspective 3D arena with direct card/zone interaction, contextual action
  discs, full phase selector, selection tray, PV, piles, fanned hands, card
  inspector, and optional event log.
- Manual, guided-training, and deterministic demonstration modes.
- All 200 catalog cards have local art and validated presentation metadata.
- 232 selected official Lua scripts plus three isolated printed-code aliases
  cover the 261-record Core catalog without moving rule logic into Unity.
- Main decks are shuffled with a fresh runtime seed; an explicit test proves the selected deck snapshot reaches the Core unchanged before shuffling.
- Bot opponents select one complete curated themed Main/Extra list, never a mixture of archetypes, and use a stateful tactical evaluator over Core-legal choices.
- The player hand rests in a responsive lower viewport strip and retracts farther during zone-placement prompts, keeping the field unobstructed.
- The Extra Deck pile highlights only when the Core exposes legal summons; Fusion, Synchro, Xyz, and Link paths have controlled end-to-end Core tests.

## Validation

- EditMode: 52 passed, 0 failed.
- All nine player/opponent pairings across the three complete curated decks
  start and process four Core-authored turns with 0 retries and 0 unknown
  protocol messages.
- Each of the three bot-deck tiles is validated, clickable, and transitions
  into a live `DuelArena` session with a typed Core prompt.
- Controlled Extra Deck validation: Fusion, Synchro, Xyz, and Link passed with 0 retries and the expected Extra-to-Monster-Zone movement.
- Compiled runtime audit: 261 records; every non-Normal card resolves required executable Core content before a duel is allowed to start.
- PlayMode: 9 passed, 0 failed.
- Windows x64 Release build: succeeded after the legacy-presentation merge.
- Standalone Portal, duel-mode, options, contextual-action, phase, and
  direct-zone captures: succeeded with no critical runtime log hits.
- The migrated `Deck Dragão Branco` starts with 40 Main and 15 Extra cards
  for both duelists, reaches a Core-authored placement prompt, and highlights
  the legal authored field zone directly.
- Card visual audit: 200 cards, 8 batches, 0 missing database rows, scripts,
  or art files.

Logs and XML results are written under `Logs/` and intentionally excluded from
source control.

## Generated title art

Project asset:

`Assets/StreamingAssets/Ygo/UI/title_arena.png`

The image was generated as original fantasy arena concept art with no
characters, cards, logos, text, or recognizable franchise imagery. Runtime
buttons, title typography, rules, navigation, and responsiveness are code,
not baked into the image.
