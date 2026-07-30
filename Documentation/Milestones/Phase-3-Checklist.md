# Phase 3 Checklist

- [x] Confirm the pinned Core build method from its README.
- [x] Keep all newly installed tooling on drive D.
- [x] Generate gmake2 files with Premake 5.0.0-beta2.
- [x] Compile `ocgcoreshared` in Release x64.
- [x] Locate the produced DLL instead of assuming a path.
- [x] Inspect real runtime dependencies.
- [x] Copy a stripped plugin binary to the Unity Windows x64 plugin folder.
- [x] Configure PluginImporter for Editor and Standalone Windows x64 only.
- [x] Keep DllImport isolated outside MonoBehaviours.
- [x] Call `OCG_GetVersion` from an EditMode test.
- [x] Confirm API version 11.0 without native loading exceptions.
- [x] Record hashes, tool versions, commands, results, and errors.
