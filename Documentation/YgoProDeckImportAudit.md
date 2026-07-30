# YGOPRODeck Import Audit

- Generated UTC: 2026-07-28T22:13:00.647641+00:00
- Source: https://ygoprodeck.com/api-guide/
- Selected official cards: 101
- BabelCDB matches: 101/101
- Direct official scripts: 97
- Cards explicitly requiring no effect script: 4
- Missing scripts: 0
- Downloaded and hashed images: 101
- Final catalog rows: 200
- Final unique official codes: 200

The 101 downloaded images remain in `ContentStaging/YGOPRODeck/Art`,
outside Unity's `Assets` folder, until the 12-card vertical slice passes.
Images are intentionally excluded from Git; their URLs and SHA-256 hashes
are retained in `ContentStaging/YGOPRODeck/CardSelection.json`.

## Selected type distribution

- Effect Monster: 33
- Flip Effect Monster: 2
- Fusion Monster: 6
- Link Monster: 1
- Normal Monster: 4
- Pendulum Effect Monster: 1
- Ritual Effect Monster: 2
- Ritual Monster: 1
- Spell Card: 22
- Synchro Monster: 4
- Trap Card: 13
- Tuner Monster: 8
- XYZ Monster: 4

## Rights note

The YGOPRODeck API guide states that Yu-Gi-Oh! card images, symbols,
and card text are copyrighted by their respective rights holders.
The staged art is for local development and does not establish
redistribution or commercial-use rights.
