"""Generate the checked-in raw starter deck source catalog."""

from __future__ import annotations

import json
import sys
from pathlib import Path


DECKS = [
    {
        "id": "starter_724579",
        "displayName": "Deck Inicial 724579",
        "title": "4yretyhrthjtyj",
        "url": "https://ygoprodeck.com/deck/4yretyhrthjtyj-724579",
        "main": [
            59811955, 59811955, 67829249, 67829249, 4446672, 37694547,
            37694547, 92001300, 92001300, 30435145, 30435145, 313513,
            313513, 50933533, 50933533, 83104731, 83104731, 83104731,
            1953925, 10509340, 56094445, 56094445, 74519184, 74519184,
            74519184, 48130397, 70095154, 70095154, 15717011, 79109599,
            27847700, 69162969, 37630732, 37630732, 74117290, 74117290,
            313513, 23171610, 73628505, 86780027, 86780027, 77565204,
        ],
        "extra": [12652643, 12652643, 1546123, 74157028, 74157028],
        "side": [
            86780027, 31557782, 31557782, 67169062, 67169062, 5556499,
            5556499, 42940404, 32491822, 32491822, 48712195, 48712195,
            3136426, 79323590, 79323590,
        ],
    },
    {
        "id": "starter_gladiator_control",
        "displayName": "Controle Gladiador",
        "title": "Gladiator Control",
        "url": "https://ygoprodeck.com/deck/gladiator-control-724515",
        "main": [
            41470137, 41470137, 57731460, 57731460, 78868776, 78868776,
            25924653, 4253484, 612115, 5975022, 92373006, 92373006,
            92373006, 71564252, 71564252, 35224440, 35224440, 98891840,
            27243130, 27243130, 14087893, 14087893, 19613556, 83764719,
            53129443, 96216229, 96216229, 96216229, 41420027, 84749824,
            84749824, 70342110, 70342110, 29401950, 29401950, 94192409,
            94192409, 77538567, 44095762, 53582587,
        ],
        "extra": [27346636, 48156348, 48156348, 73285669],
        "side": [],
    },
    {
        "id": "starter_box_deck",
        "displayName": "Deck Box",
        "title": "Box deck",
        "url": "https://ygoprodeck.com/deck/box-deck-724449",
        "correction": (
            "O PDF omitiu o último dígito 9; a busca oficial pelo nome exato "
            "resolveu o endereço 724449."
        ),
        "main": [
            25774450, 25774450, 25774450, 21598948, 21598948, 21598948,
            31077447, 31077447, 31077447, 95174353, 95174353, 95174353,
            95744531, 95744531, 95744531, 56514812, 56514812, 56514812,
            97017120, 97017120, 97017120, 26523337, 9416697, 39522887,
            3137279, 99529628, 76133574, 73421698, 46037983, 36562627,
            36562627, 36562627, 58570206, 58570206, 58570206, 8794055,
            35346968, 35346968, 35346968, 65898344, 45792753, 38409239,
        ],
        "extra": [
            48882106, 48882106, 81330115, 81330115, 16259549, 16259549,
            70219023, 70219023, 72309040, 46815301, 2405631, 65910922,
        ],
        "side": [],
    },
    {
        "id": "starter_724026",
        "displayName": "Deck Inicial 724026",
        "title": "",
        "url": "https://ygoprodeck.com/deck/724026",
        "main": [
            95788410, 39674352, 39674352, 39674352, 95788410, 39111158,
            31447217, 31447217, 6740720, 76232340, 24311372, 6740720,
            6740720, 24311372, 24311372, 42129512, 62397231, 46986414,
            76232340, 42129512, 42129512, 3797883, 70781052, 70781052,
            11761845, 70781052, 17658803, 27054370, 30113682, 30113682,
            30113682, 58818411, 48649353, 89832901, 60862676, 8353769,
            47986555, 35712107, 50005633, 23659124,
        ],
        "extra": [],
        "side": [
            13140300, 89189982, 31447217, 74677422, 74677422, 49888191,
            46986414, 49888191, 49888191, 66516792, 66516792, 6368038,
            66516792, 6368038, 74677422,
        ],
    },
    {
        "id": "starter_vampire_wolf",
        "displayName": "Vampiros e Lobos",
        "title": "Vampire and wolf’s",
        "url": "https://ygoprodeck.com/deck/vampire-and-wolf-s-723953",
        "main": [
            22056710, 4918855, 6917479, 53839837, 33438666, 58947797,
            1371589, 80485722, 26495087, 88728507, 56387350, 90299015,
            90299015, 34250214, 34250214, 70645913, 70645913, 69247929,
            56369281, 8471389, 49417509, 91697229, 13683298, 67922702,
            92998610, 3534077, 88975532, 88132637, 15947754, 43175027,
            32202803, 72913666, 293542, 55696885, 93294869, 53167658,
            48712195, 91740879, 64163367, 80181649,
        ],
        "extra": [],
        "side": [],
    },
    {
        "id": "starter_cyberse_master_duel",
        "displayName": "Link Ciberso Inicial",
        "title": "deck link/cyberse initial of the master duel",
        "url": (
            "https://ygoprodeck.com/deck/"
            "deck-link-cyberse-initial-of-the-master-duel-707485"
        ),
        "main": [
            92176681, 92176681, 32295838, 32295838, 24154052, 24154052,
            7445307, 7445307, 35911108, 35911108, 35911108, 23331400,
            23331400, 36694815, 36694815, 78161361, 78161361, 78161361,
            71172240, 74210057, 74210057, 74210057, 61583217, 61583217,
            51335426, 51335426, 2625939, 2625939, 22346472, 22346472,
            83102080, 83102080, 56830749, 56830749, 41440817, 41440817,
            4433488, 4433488, 91269402, 91269402,
        ],
        "extra": [1861629, 98978921, 22862454, 52615248, 88000953],
        "side": [],
    },
]

# Substituicoes so entram aqui depois de aprovadas pelo proprietario do projeto.
# O deck Gladiator permanece bloqueado enquanto esta tabela nao contiver uma
# substituicao legal para Heavy Storm (19613556).
APPROVED_REPLACEMENTS = {}


EXPECTED_COUNTS = {
    "starter_724579": (42, 5, 15),
    "starter_gladiator_control": (40, 4, 0),
    "starter_box_deck": (42, 12, 0),
    "starter_724026": (40, 0, 15),
    "starter_vampire_wolf": (40, 0, 0),
    "starter_cyberse_master_duel": (40, 5, 0),
}


def canonical(values: list[int]) -> list[str]:
    return [f"{value:08d}" for value in values]


def main() -> int:
    if len(sys.argv) not in (2, 3):
        print(
            "usage: generate_starter_deck_sources.py "
            "<output.json> [import-manifest.json]"
        )
        return 2

    records = []
    for deck in DECKS:
        actual = (len(deck["main"]), len(deck["extra"]), len(deck["side"]))
        if actual != EXPECTED_COUNTS[deck["id"]]:
            raise RuntimeError(f"Unexpected counts for {deck['id']}: {actual}")
        records.append(
            {
                "id": deck["id"],
                "displayName": deck["displayName"],
                "approvedReplacements": APPROVED_REPLACEMENTS.get(
                    deck["id"], []
                ),
                "raw": {
                    "sourceTitle": deck["title"],
                    "sourceUrl": deck["url"],
                    "sourceCorrectionNote": deck.get("correction", ""),
                    "mainDeck": canonical(deck["main"]),
                    "extraDeck": canonical(deck["extra"]),
                    "sideDeck": canonical(deck["side"]),
                },
            }
        )

    output = Path(sys.argv[1]).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(
            {"schemaVersion": 1, "catalogVersion": 1, "decks": records},
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    if len(sys.argv) == 3:
        manifest = Path(sys.argv[2]).resolve()
        union_codes = sorted(
            {
                code
                for record in records
                for section in ("mainDeck", "extraDeck", "sideDeck")
                for code in record["raw"][section]
            }
        )
        manifest.parent.mkdir(parents=True, exist_ok=True)
        manifest.write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "origin": "starter-decks-2026-08",
                    "unionCodes": union_codes,
                },
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )
    print(f"STARTER_SOURCE_OK decks={len(records)} output={output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
