# Card Catalog Audit

- Supplied archive entries: 100
- Unique official codes from the supplied archive: 99
- Duplicate supplied official-code entries: 1
- Additional official cards selected through YGOPRODeck: 101
- Final catalog rows: 200
- Final unique official codes: 200
- Selected vertical-slice cards: 12
- Fanmade cards: 0
- Database records found: 200/200
- Images available from their recorded source: 200/200
- Missing scripts: 0

The supplied archive contains 99 unique official cards. Official code
`70681994` has two supplied images, so the catalog intentionally retains only
one row for that code.

The remaining 101 cards were curated from the YGOPRODeck API into coherent
Blue-Eyes, Dark Magician, Red-Eyes, classic-interaction, and summon-mechanic
groups. The selection is deterministic and stored in
`Tools/CardCatalog/desired_cards.txt`.

Validation against the pinned BabelCDB and CardScripts repositories found:

- 172 direct official Lua scripts;
- 2 supplied codes resolved through BabelCDB aliases;
- 26 cards explicitly requiring no effect script;
- 0 unresolved scripts;
- 0 duplicate codes after canonicalizing alternate-art aliases.

The 101 YGOPRODeck images were downloaded once, validated as JPEG files, and
hashed in `ContentStaging/YGOPRODeck/CardSelection.json`. They remain outside
Unity's `Assets` directory until the 12-card vertical slice passes end to end,
as required by the development plan.

YGOPRODeck states that card images, symbols, and card text belong to their
respective Yu-Gi-Oh! rights holders. The staged files are therefore excluded
from Git and treated as local development content pending a separate
redistribution-rights review.
