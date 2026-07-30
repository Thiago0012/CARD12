# Modifications

## Current milestone

No third-party source file has been modified.

The project currently:

- pins upstream repositories as Git submodules;
- keeps all third-party source outside Unity's `Assets` directory;
- records exact commits in `ThirdPartyVersions.json`;
- copies license texts verbatim into `LICENSES`;
- documents the intended managed/native integration boundary.

Future changes to ygopro-core, Lua scripts, or database content must not be
made directly in a pinned upstream checkout. Project-owned patches and custom
scripts must be isolated, documented here, and accompanied by tests.

