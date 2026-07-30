# Phases 0-2 Milestone Report

Date: 2026-07-28

## Outcome

The project foundation is operational in `D:\JOGO Y\DO ZERO` with Unity
6000.5.0f1. It uses URP, has isolated assemblies and scenes, passes its
bootstrap EditMode and PlayMode tests, and pins ygopro-core, CardScripts, and
BabelCDB as submodules.

No duel rules or card effects have been reimplemented in C#.

## Phase 0 - card catalog

- Supplied archive: 100 catalog entries and 100 card images.
- Unique official card codes: 99.
- Duplicate official code: `70681994`.
- Fanmade cards detected: 0.
- Database coverage: 99/99.
- Lua coverage:
  - 75 direct official scripts;
  - 2 scripts resolved through database aliases;
  - 22 Normal Monsters requiring no effect script;
  - 0 unresolved scripts.
- Representative vertical slice: 12 cards selected across Normal, Ignition,
  Trigger, Quick, Spell, Trap, Extra Deck, and Continuous categories.
- Catalog target shortfall: 101 unique cards relative to the plan's target of
  approximately 200.

The supplied artwork is inventoried but intentionally not imported into
`Assets`, because the plan defers full art import until the 12-card vertical
slice passes end to end.

## Phase 1 - Unity and Git

- Editor: Unity 6000.5.0f1, changeset `88b47c5e7076`.
- Render pipeline: Universal Render Pipeline 17.5.0.
- Test framework: 1.7.0.
- Scenes:
  - `Assets/Game/Scenes/Bootstrap.unity`
  - `Assets/Game/Scenes/Duel.unity`
  - `Assets/Game/Scenes/CardLab.unity`
- Assemblies:
  - `ArcaneDuel.Game`
  - `ArcaneDuel.Game.Editor`
  - `ArcaneDuel.DuelEngine`
  - `ArcaneDuel.EditModeTests`
  - `ArcaneDuel.PlayModeTests`
- Baseline commit: `4ac2e98 Bootstrap Unity project and card catalog`.
- Active branch: `feature/ocgcore-bootstrap`.

### Tests

| Suite | Total | Passed | Failed | Result |
| --- | ---: | ---: | ---: | --- |
| EditMode | 1 | 1 | 0 | Passed |
| PlayMode | 1 | 1 | 0 | Passed |

## Phase 2 - pinned dependencies

| Component | Commit | Description |
| --- | --- | --- |
| ygopro-core | `0764db0c75b3d1d574880d365aa3695ab1f13b43` | `v11.0-74-g0764db0` |
| CardScripts | `55607ee511d9697b6eac5dbb689deaa5be712826` | `20250420-1371-g55607ee51` |
| BabelCDB | `8d60901db521eb4183ca72560c01a70a6386c98c` | `20250419-689-g8d60901` |
| Lua | `6e22fedb74cf0c9b6656e9fce8b7331db847c605` | `v5.4.8` |

Available upstream license files were copied verbatim. BabelCDB has no explicit
license file at its pinned commit, so public redistribution is blocked pending
rights review; local development can continue.

## Commands executed

Representative commands:

```powershell
Unity.exe -batchmode -nographics -quit -createProject "D:\JOGO Y\DO ZERO"
Unity.exe -batchmode -nographics -quit -projectPath "D:\JOGO Y\DO ZERO" -executeMethod ArcaneDuel.Editor.ProjectBootstrap.Configure
Unity.exe -batchmode -nographics -projectPath "D:\JOGO Y\DO ZERO" -runTests -testPlatform EditMode
Unity.exe -batchmode -nographics -projectPath "D:\JOGO Y\DO ZERO" -runTests -testPlatform PlayMode
git init -b main
git submodule add https://github.com/edo9300/ygopro-core.git ThirdParty/ygopro-core
git submodule add https://github.com/ProjectIgnis/CardScripts.git ThirdParty/CardScripts
git submodule add https://github.com/ProjectIgnis/BabelCDB.git ThirdParty/BabelCDB
git submodule update --init --recursive
```

## Errors encountered and resolved

1. The first configuration compile found one obsolete build-target overload
   and three URP properties that are read-only in 17.5.0. The configuration
   code was corrected to the actual Unity 6000.5 API and then succeeded.
2. The first PlayMode launch collided with a Unity shutdown lock. Only the
   failed process launched by this task was terminated; the retry passed.
3. Two alternate-art codes did not have same-code Lua files. BabelCDB aliases
   correctly map Final Flame `73134082` to `73134081` and Monster Reborn
   `83764719` to `83764718`; coverage is complete after alias resolution.

## Next recommended milestone

Proceed to Phase 3 only after acknowledging this report:

1. verify the installed Visual Studio C++ toolchain and Windows SDK;
2. generate and compile the pinned ygopro-core shared library for Release x64;
3. copy the exact runtime binaries to `Assets/Plugins/Windows/x86_64`;
4. call `OCG_GetVersion` from an isolated interop test;
5. stop if the Editor cannot load the library cleanly.

