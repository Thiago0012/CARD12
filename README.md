# Arcane Duel

Arcane Duel is a Windows x64 Unity card-duel project with an original visual
identity. `ygopro-core` is the sole authority for legality, costs, targets,
chains, battle, and resolution; Unity owns presentation and player input only.

## Current milestone

The approved zero-project plan now forms a complete local duel experience:

- audited catalog of exactly 200 unique official card codes, plus 61 official
  compatibility records used by the preserved legacy presentation and decks;
- Unity 6000.5.0f1 URP project and reproducible Windows x64 native Core;
- API 11.0 managed bridge with exact layouts, callbacks, `SafeHandle`, and
  immediate native-buffer copies;
- deterministic `cards.bin` plus separate UTF-8 display texts;
- controlled global Lua utilities, 232 selected official scripts, and three
  isolated printed-code compatibility shims;
- bounded typed protocol decoder and explicit unknown-message reporting;
- deterministic headless duel and a complete integration duel reaching
  `WIN`;
- original title portal, duel-mode flow, three themed starter decks, deck
  gallery/editor/shop, presentation options, rules, and support panels;
- preserved authored frontend and 3D arena assets from the previous project,
  driven exclusively by the new typed Core prompt/response bridge;
- 200 local card artworks with validated visual metadata in eight batches;
- perspective 3D duel table, direct card and zone interaction, fanned hand,
  contextual action discs, phase selector, target/selection trays, PV, event
  log, large card inspector, manual decisions, and optional demo mode;
- validated Development and Release builds for Windows x64.

## Run

Open the project with Unity `6000.5.0f1`, load `Bootstrap`, and press Play, or
run:

`Builds/Windows/ArcaneDuel.exe`

Choose `DUELAR`, select `ENFRENTAR BOT`, and choose the opponent deck. During
the duel, click a highlighted card and then its contextual action. When the
Core requests a destination or target, click the highlighted zone/card
directly. Every decision is encoded back into the native protocol.
`ASSISTIR DUELO DEMONSTRAÇÃO` uses the same deterministic policy as the
headless integration test.

## Project layout

- `Assets/Game` - title, arena, navigation, deck tools, and presentation.
- `Assets/DuelEngine` - managed/native bridge, data, protocol, and state.
- `Assets/StreamingAssets/Ygo` - compiled 261-card Core data/scripts and the
  original audited 200-card local-art set. The 61 compatibility records stay
  in the imported presentation catalog.
- `Assets/Tests` - EditMode and PlayMode integration tests.
- `Documentation` - catalog, validation, and milestone reports.
- `ThirdParty` - pinned upstream repositories outside `Assets`.
- `Tools` - reproducible Core build and card-content compilation.
- `LICENSES` - verbatim upstream license texts.

## Scope

This milestone is a complete local duel experience against the Core-driven AI.
The online room buttons remain visible as an honest future route but do not
create fake network sessions. Server-authoritative multiplayer, a full
localization pass, and production-grade authored audio/VFX remain later
milestones.

## Legal note

This independent prototype is not affiliated with or endorsed by Konami,
Shueisha, Project Ignis, OpenAI, or Unity Technologies. Yu-Gi-Oh! and related
marks belong to their respective owners. Card art/text are staged for local
development and require a separate rights review before redistribution.
