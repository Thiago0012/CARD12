# Third-Party Notices

This repository pins upstream components as Git submodules. Exact revisions
are recorded in `ThirdPartyVersions.json`.

## ygopro-core

- Upstream: https://github.com/edo9300/ygopro-core
- License: GNU Affero General Public License, version 3 or later, with
  historical MIT-licensed portions described by upstream.
- Verbatim texts:
  - `LICENSES/ygopro-core-LICENSE.txt`
  - `LICENSES/ygopro-core-COPYING.txt`

The upstream project states that Yu-Gi-Oh! is a trademark of Shueisha and
Konami and that the project is not affiliated with or endorsed by them.

## Project Ignis CardScripts

- Upstream: https://github.com/ProjectIgnis/CardScripts
- License file supplied by upstream: GNU Affero General Public License,
  version 3.
- Verbatim text: `LICENSES/CardScripts-COPYING.txt`

## Project Ignis BabelCDB

- Upstream: https://github.com/ProjectIgnis/BabelCDB
- The pinned repository does not contain an explicit `LICENSE`, `COPYING`, or
  `NOTICE` file.

The database is retained as a pinned development dependency. Do not ship or
redistribute it until its licensing and the rights associated with card text
have been reviewed for the intended distribution.

## Lua

The recursive ygopro-core dependency includes the Lua source mirror. The
ygopro-core `LICENSE` file reproduces the applicable Lua MIT terms.

## Supplied card art

The user-supplied `Cards.rar` is inventoried but its full contents are not
imported at this milestone. Card artwork and Yu-Gi-Oh! marks remain the
property of their respective rights holders. Distribution review is required
before any public build.

## YGOPRODeck API and staged card art

- API guide: https://ygoprodeck.com/api-guide/
- Selected official cards: 101
- Local provenance and SHA-256 manifest:
  `ContentStaging/YGOPRODeck/CardSelection.json`

The YGOPRODeck API guide states that the Yu-Gi-Oh! card images, symbols, and
card text exposed by the service are copyrighted by their respective rights
holders. Downloaded images are stored once for local development, excluded
from Git, and kept outside Unity's `Assets` directory. Their presence does not
grant redistribution or commercial-use rights.
