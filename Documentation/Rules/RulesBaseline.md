# Rules Baseline

`Rulebook_v9_en.pdf` is retained beside this file as a historical baseline.
The pinned `ygopro-core`, current official card text, and pinned Lua scripts
remain authoritative for implemented behavior.

## Baseline requirements captured from version 9

- A Duel starts at 8000 LP with a five-card opening hand.
- Main Deck: 40-60 cards.
- Extra Deck: 0-15 cards.
- Side Deck: 0-15 cards, with the same size before and after siding.
- Up to three copies of a card across the applicable deck pools, subject to
  Forbidden/Limited restrictions for the selected format.
- Five Monster Zones and five Spell & Trap Zones per player, plus Deck,
  Extra Deck, Graveyard, Field, Pendulum, hand, and banished locations.
- The first player does not draw or conduct a Battle Phase on the first turn.
- Turn flow: Draw, Standby, Main 1, Battle, Main 2, End.
- One Normal Summon or Set per turn; Flip and Special Summons follow their
  own legality rules.
- Battle covers attack/defense positions, direct attacks, replays, damage
  calculation, destruction, and Damage Step activation restrictions.
- Chains resolve last-in, first-out and respect Spell Speeds 1, 2, and 3.
- Costs are paid before activation and are not refunded by negation.
- Card text takes precedence over baseline rules.
- Public-information, simultaneous-action, token, counter, control,
  ownership, Xyz-material, and leave-the-field semantics must remain visible
  to the presentation layer without being reimplemented there.

The rulebook predates some modern mechanics and wording. Modern interactions
must be validated against the exact pinned engine, database, scripts, and
official card text.

