#!/usr/bin/env python3
"""Normalize and republish one already generated batch translation overlay."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from translate_unofficial_card_texts import needs_repair, normalize_terms


SNAPSHOT_CORRECTIONS = {
    28958464: (
        "Escolha 1 monstro em qualquer Cemitério; Invoque-o por "
        "Invocação-Especial no seu campo, mas, pelo resto deste turno, ele não "
        "pode atacar e nenhum duelista pode ativar seus efeitos. Você só pode "
        'ativar 1 \"Spell Card \\\"Monster Reborn\\\"\" por turno.'
    ),
}


def write_json(path: Path, payload: dict) -> None:
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--overlay", required=True, type=Path)
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--snapshot", required=True, type=Path)
    args = parser.parse_args()

    overlay = json.loads(args.overlay.read_text(encoding="utf-8"))
    normalized = {
        int(card["code"]): {
            **card,
            "description": normalize_terms(str(card.get("description", ""))),
        }
        for card in overlay.get("cards", [])
    }
    for code, description in SNAPSHOT_CORRECTIONS.items():
        if code in normalized:
            normalized[code]["description"] = description
            if "curated_rule_correction" not in normalized[code]["source"]:
                normalized[code]["source"] += "+curated_rule_correction"
    invalid = sorted(
        code for code, card in normalized.items()
        if card["description"] and needs_repair(card["description"])
    )
    if invalid:
        raise ValueError(
            "English rules text remains after normalization: "
            + ", ".join(str(code) for code in invalid)
        )
    overlay["cards"] = [normalized[code] for code in sorted(normalized)]

    for target in (args.catalog, args.snapshot):
        payload = json.loads(target.read_text(encoding="utf-8"))
        target_by_code = {
            int(card["code"]): card for card in payload.get("cards", [])
        }
        for code, card in normalized.items():
            if code not in target_by_code:
                # Deferred batch entries intentionally do not ship in Core.
                continue
            target_by_code[code]["name"] = card["name"]
            target_by_code[code]["description"] = card["description"]
            target_by_code[code]["strings"] = list(card.get("strings", []))
        payload["cards"] = [
            target_by_code[code] for code in sorted(target_by_code)
        ]
        if "missingCodes" in payload:
            payload["missingCodes"] = sorted(
                set(int(code) for code in payload["missingCodes"])
                - set(normalized)
            )
        write_json(target, payload)

    write_json(args.overlay, overlay)
    print(
        "ARCANE_BATCH_TRANSLATION_NORMALIZE_OK "
        f"cards={len(normalized)} invalid={len(invalid)}"
    )


if __name__ == "__main__":
    main()
