# Corresponding Source Code

The complete project source includes this repository and all recursive
submodules at the revisions listed in `ThirdPartyVersions.json`.

Clone and initialize it with:

```powershell
git clone <project-repository-url>
git -C "<project-directory>" submodule update --init --recursive
```

For a source archive or friendly build, include:

- the project commit identifier;
- `.gitmodules`;
- `ThirdPartyVersions.json`;
- all project-owned source and build scripts;
- the exact corresponding ygopro-core and CardScripts source;
- the license and notice files.

No public source URL exists yet because this is a local bootstrap milestone.
Before distribution, replace `<project-repository-url>` with a durable source
location that satisfies the applicable license obligations.

