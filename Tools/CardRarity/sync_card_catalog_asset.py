#!/usr/bin/env python3
"""Synchronize derived rarity fields in Unity's serialized CardCatalog asset.

This is a deterministic command-line counterpart to the Unity editor menu
`Sync All Card Metadata and Rarities`.  It is useful in CI/headless machines
where the Unity licensing client cannot start.  Localized display fields are
never rewritten.
"""

from __future__ import annotations

import argparse
import json
import re
import sqlite3
import unicodedata
from collections import defaultdict
from pathlib import Path


ENTRY_START = re.compile(r"^  - stableId:")
OFFICIAL_ID = re.compile(r"^    officialCardId:\s*(\d+)\s*$")
DERIVED_FIELDS = {
    "englishName",
    "rarityVariant",
    "rarity",
    "raritySourceName",
    "craftingBlocked",
    "dismantlingBlocked",
}


def normalize(value: str) -> str:
    folded = unicodedata.normalize("NFKD", value or "")
    words: list[str] = []
    current: list[str] = []
    for character in folded.casefold():
        if unicodedata.combining(character):
            continue
        if character.isalnum():
            current.append(character)
        elif current:
            words.append("".join(current))
            current.clear()
    if current:
        words.append("".join(current))
    return " ".join(words)


def rarity_resolver(path: Path) -> dict[str, list[dict]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    grouped: dict[str, list[dict]] = defaultdict(list)
    for entry in payload["entries"]:
        grouped[normalize(entry["englishName"])].append(entry)
    return dict(grouped)


def english_names(database: Path) -> dict[str, tuple[str, int]]:
    connection = sqlite3.connect(database)
    try:
        return {
            f"{int(code):08d}": (str(name or "").strip(), int(alias or 0))
            for code, alias, name in connection.execute(
                "SELECT d.id, d.alias, t.name FROM datas d JOIN texts t ON t.id = d.id"
            )
        }
    finally:
        connection.close()


def split_blocks(lines: list[str]) -> tuple[list[str], list[list[str]]]:
    first = next((index for index, line in enumerate(lines) if ENTRY_START.match(line)), None)
    if first is None:
        raise ValueError("CardCatalog has no serialized entries")
    prefix = lines[:first]
    blocks: list[list[str]] = []
    current: list[str] = []
    for line in lines[first:]:
        if ENTRY_START.match(line) and current:
            blocks.append(current)
            current = []
        current.append(line)
    if current:
        blocks.append(current)
    return prefix, blocks


def sync_block(
    block: list[str],
    names: dict[str, tuple[str, int]],
    rarities: dict[str, list[dict]],
    variant_by_code: dict[str, int],
) -> tuple[list[str], bool, bool]:
    official_match = next((OFFICIAL_ID.match(line) for line in block if OFFICIAL_ID.match(line)), None)
    code = f"{int(official_match.group(1)):08d}" if official_match else ""
    english_name = names.get(code, ("", 0))[0]
    variant = variant_by_code.get(code, 0)
    candidates = rarities.get(normalize(english_name), [])
    expected_variant = {
        2: "ALT",
        3: "ALT 1",
        4: "ALT 2",
    }.get(variant, "BASE")
    rarity_entry = next(
        (entry for entry in candidates if entry["variant"] == expected_variant),
        None,
    )
    if rarity_entry is None and variant == 2:
        rarity_entry = next(
            (entry for entry in candidates if entry["variant"] == "ALT 1"),
            None,
        )
    rarity_entry = rarity_entry or (candidates[0] if candidates else None)
    rarity = int(rarity_entry["rarity"]) if rarity_entry else 0
    source_name = str(rarity_entry["englishName"]) if rarity_entry else ""

    filtered = [
        line
        for line in block
        if not any(line.startswith(f"    {field}:") for field in DERIVED_FIELDS)
    ]
    display_index = next(
        (index for index, line in enumerate(filtered) if line.startswith("    displayName:")),
        None,
    )
    if display_index is None:
        return filtered, bool(english_name), bool(rarity_entry)
    derived = [
        f"    englishName: {json.dumps(english_name, ensure_ascii=True)}",
        f"    rarityVariant: {variant}",
        f"    rarity: {rarity}",
        f"    raritySourceName: {json.dumps(source_name, ensure_ascii=True)}",
        "    craftingBlocked: 0",
        "    dismantlingBlocked: 0",
    ]
    filtered[display_index + 1 : display_index + 1] = derived
    return filtered, bool(english_name), bool(rarity_entry)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset", required=True, type=Path)
    parser.add_argument("--rarities", required=True, type=Path)
    parser.add_argument("--database", required=True, type=Path)
    args = parser.parse_args()

    names = english_names(args.database)
    rarities = rarity_resolver(args.rarities)
    original = args.asset.read_text(encoding="utf-8").splitlines()
    prefix, blocks = split_blocks(original)
    codes_in_order: list[str] = []
    for block in blocks:
        official_match = next(
            (OFFICIAL_ID.match(line) for line in block if OFFICIAL_ID.match(line)),
            None,
        )
        if official_match:
            codes_in_order.append(f"{int(official_match.group(1)):08d}")
    alternatives_by_name: dict[str, list[str]] = defaultdict(list)
    for code in codes_in_order:
        english_name, alias = names.get(code, ("", 0))
        key = normalize(english_name)
        if not key or alias == 0 or code in alternatives_by_name[key]:
            continue
        alternatives_by_name[key].append(code)
    variant_by_code: dict[str, int] = {}
    for alternative_codes in alternatives_by_name.values():
        if len(alternative_codes) == 1:
            variant_by_code[alternative_codes[0]] = 2
        else:
            variant_by_code[alternative_codes[0]] = 3
            variant_by_code[alternative_codes[1]] = 4
            for code in alternative_codes[2:]:
                variant_by_code[code] = 2
    synchronized: list[list[str]] = []
    named = 0
    resolved = 0
    for block in blocks:
        updated, has_name, has_rarity = sync_block(
            block,
            names,
            rarities,
            variant_by_code,
        )
        synchronized.append(updated)
        named += int(has_name)
        resolved += int(has_rarity)
    output = prefix + [line for block in synchronized for line in block]
    args.asset.write_text("\n".join(output) + "\n", encoding="utf-8")
    print(
        "ARCANE_CARD_CATALOG_RARITY_SYNC_OK "
        f"entries={len(blocks)} english={named} rarity={resolved} "
        f"unknown={len(blocks) - resolved}"
    )


if __name__ == "__main__":
    main()
