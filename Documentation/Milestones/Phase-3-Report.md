# Phase 3 Milestone Report - Native Core Loaded

Date: 2026-07-28

## Outcome

Milestone 1 is complete. Unity 6000.5.0f1 loads the pinned Windows x64
`ocgcore.dll`, and an EditMode test calls the real exported
`OCG_GetVersion` function successfully. The returned API version is `11.0`.

This milestone proves native loading only. No duel session is created yet;
safe lifecycle, callbacks, data/script readers, and a headless duel belong to
Phases 4-6.

## Catalog completion performed alongside the milestone

- Original unique official cards: 99
- Official cards added from YGOPRODeck: 101
- Final catalog rows and unique codes: 200
- BabelCDB coverage: 200/200
- Script coverage:
  - 172 direct official scripts
  - 2 scripts resolved through aliases
  - 26 cards requiring no effect script
  - 0 missing scripts
- New YGOPRODeck images: 101/101 downloaded once and SHA-256 verified
- Full art import into `Assets`: intentionally deferred

The new images are stored under `ContentStaging/YGOPRODeck/Art`, outside
Unity import, until the 12-card vertical slice passes.

## Native build

- Core commit: `0764db0c75b3d1d574880d365aa3695ab1f13b43`
- Generator: Premake 5.0.0-beta2 / gmake2
- Compiler: GCC 16.1.0 from w64devkit 2.9.0
- Configuration: Release x64 shared library
- Unstripped diagnostic binary: 204,196,033 bytes
- Unity plugin binary: 1,597,854 bytes
- Unity plugin SHA-256:
  `DD6FFC53CCBE9151A091C8972E003A1236A913527DEB7C29FA2431A3A71E9477`
- Runtime dependencies:
  - `KERNEL32.dll`
  - `msvcrt.dll`

The Visual Studio Build Tools route was not used because this machine already
has Visual Studio's shared-installation path fixed on drive C. Installing that
workload would have violated the user's D-only installation requirement.
The pinned ygopro-core README explicitly supports the MinGW/gmake2 route used
instead. The portable toolchain is entirely under `D:\JOGO Y\Tools`.

## Unity plugin configuration

- Plugin path: `Assets/Plugins/Windows/x86_64/ocgcore.dll`
- Any Platform: disabled
- Editor: enabled for Windows x86_64
- Standalone Windows x64: enabled
- Standalone Windows x86: disabled
- Native interop remains outside all MonoBehaviours

## Tests

| Suite | Total | Passed | Failed | Result |
| --- | ---: | ---: | ---: | --- |
| Phase 3 EditMode | 2 | 2 | 0 | Passed |

The new test asserts `OCG_GetVersion == 11.0`. No `DllNotFoundException`,
`EntryPointNotFoundException`, `AccessViolationException`, or C# compilation
error occurred.

## Errors encountered and resolved

1. Visual Studio shared components were already locked to drive C. A portable,
   Core-supported MinGW toolchain on D was selected instead.
2. GNU Make could not invoke a shell through paths containing spaces. Two
   non-destructive D-drive junctions provide build-only aliases; the real
   project remains in `D:\JOGO Y\DO ZERO`.
3. The initial build automation evaluated `$PSScriptRoot` too early. Project
   root resolution was moved after parameter binding.
4. GCC emitted two upstream array-bounds analysis warnings in `playerop.cpp`.
   Linking completed successfully; the warnings are retained in the build log
   for later upstream comparison.

## Reproducible commands

```powershell
Tools\Build\Build-OcgCore.ps1

Unity.exe -batchmode -nographics -quit `
  -projectPath "D:\JOGO Y\DO ZERO" `
  -executeMethod ArcaneDuel.Editor.NativePluginConfigurator.Configure

Unity.exe -batchmode -nographics `
  -projectPath "D:\JOGO Y\DO ZERO" `
  -runTests -testPlatform EditMode
```

## Next recommended milestone

Proceed to Phase 4:

1. mirror the pinned C layouts and callbacks in the isolated Interop assembly;
2. add size/offset tests for every native structure;
3. implement a safe duel handle and deterministic disposal;
4. expose `IDuelRulesEngine` without leaking `IntPtr`;
5. stop before UI work and report the safe-bridge milestone.
