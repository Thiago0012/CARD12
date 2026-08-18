#!/usr/bin/env python3
"""Import the searchable Master Duel rarity guide into a Unity JSON resource.

The PDF keeps two catalog columns per page.  This importer reads positioned
words instead of flattened text so long names cannot bleed into the adjacent
column.  The generated catalog deliberately retains English names only as
matching keys; localized presentation names remain owned by CardCatalog.
"""

from __future__ import annotations

import argparse
import csv
import json
import sqlite3
import unicodedata
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import date
from pathlib import Path

import pdfplumber


RARITY_VALUES = {"N": 1, "R": 2, "SR": 3, "UR": 4}
EXPECTED_COUNTS = {"N": 5303, "R": 4013, "SR": 2895, "UR": 1645}
EXPECTED_ENTRIES = sum(EXPECTED_COUNTS.values())
EXPECTED_UNIQUE_NAMES = 13_783
EXPECTED_CHANGED_RARITY_NAMES = 22
CATALOG_FIRST_PAGE_INDEX = 8
ROW_TOP_MIN = 95.0
ROW_TOP_MAX = 815.0


@dataclass(frozen=True)
class RarityEntry:
    english_name: str
    variant: str
    rarity: str
    page: int


def normalize_name(value: str) -> str:
    folded = unicodedata.normalize("NFKD", value or "")
    folded = "".join(character for character in folded if not unicodedata.combining(character))
    words: list[str] = []
    current: list[str] = []
    for character in folded.casefold():
        if character.isalnum():
            current.append(character)
        elif current:
            words.append("".join(current))
            current.clear()
    if current:
        words.append("".join(current))
    return " ".join(words)


def split_variant(tokens: list[str]) -> tuple[list[str], str]:
    if len(tokens) >= 2 and tokens[-2] == "ALT" and tokens[-1].isdigit():
        return tokens[:-2], f"ALT {tokens[-1]}"
    if tokens and tokens[-1] in {"BASE", "ALT"}:
        return tokens[:-1], tokens[-1]
    return tokens, ""


def row_words(words: list[dict], rarity_word: dict) -> list[str]:
    left_column = rarity_word["x0"] < 300.0
    minimum_x = 30.0 if left_column else 303.0
    maximum_x = rarity_word["x0"] - 3.0
    rarity_center = (rarity_word["top"] + rarity_word["bottom"]) * 0.5
    candidates = [
        word
        for word in words
        if minimum_x <= word["x0"]
        and word["x1"] <= maximum_x
        and abs(((word["top"] + word["bottom"]) * 0.5) - rarity_center) <= 1.4
    ]
    candidates.sort(key=lambda word: word["x0"])
    return [str(word["text"]) for word in candidates]


def parse_pdf(path: Path) -> list[RarityEntry]:
    entries: list[RarityEntry] = []
    with pdfplumber.open(path) as document:
        if len(document.pages) < CATALOG_FIRST_PAGE_INDEX + 1:
            raise ValueError("The rarity guide does not contain catalog pages")
        for page_index in range(CATALOG_FIRST_PAGE_INDEX, len(document.pages)):
            words = document.pages[page_index].extract_words(
                use_text_flow=False,
                keep_blank_chars=False,
            )
            rarity_words = [
                word
                for word in words
                if word["text"] in RARITY_VALUES
                and ROW_TOP_MIN <= word["top"] <= ROW_TOP_MAX
                and (260.0 <= word["x0"] <= 290.0 or 535.0 <= word["x0"] <= 565.0)
            ]
            for rarity_word in sorted(rarity_words, key=lambda word: (word["top"], word["x0"])):
                tokens, variant = split_variant(row_words(words, rarity_word))
                english_name = " ".join(tokens).strip()
                if not english_name:
                    raise ValueError(
                        f"Empty name near rarity {rarity_word['text']} on page {page_index + 1}"
                    )
                entries.append(
                    RarityEntry(
                        english_name=english_name,
                        variant=variant,
                        rarity=str(rarity_word["text"]),
                        page=page_index + 1,
                    )
                )
    return entries


def validate(entries: list[RarityEntry]) -> dict[str, object]:
    counts = Counter(entry.rarity for entry in entries)
    if len(entries) != EXPECTED_ENTRIES:
        raise ValueError(f"Expected {EXPECTED_ENTRIES} entries, parsed {len(entries)}")
    if counts != Counter(EXPECTED_COUNTS):
        raise ValueError(f"Rarity totals differ from the guide: {dict(counts)}")

    by_name: dict[str, list[RarityEntry]] = defaultdict(list)
    for entry in entries:
        by_name[entry.english_name].append(entry)
    if len(by_name) != EXPECTED_UNIQUE_NAMES:
        raise ValueError(
            f"Expected {EXPECTED_UNIQUE_NAMES} unique names, parsed {len(by_name)}"
        )
    changed = sorted(
        name for name, variants in by_name.items() if len({entry.rarity for entry in variants}) > 1
    )
    if len(changed) != EXPECTED_CHANGED_RARITY_NAMES:
        raise ValueError(
            f"Expected {EXPECTED_CHANGED_RARITY_NAMES} variant-sensitive names, parsed {len(changed)}"
        )

    normalized: dict[str, list[RarityEntry]] = defaultdict(list)
    for entry in entries:
        normalized[normalize_name(entry.english_name)].append(entry)
    ambiguous_normalized = sorted(
        key
        for key, variants in normalized.items()
        if len({entry.english_name for entry in variants}) > 1
        and len({entry.rarity for entry in variants}) > 1
    )
    if ambiguous_normalized:
        raise ValueError(
            "Normalization merges differently rare cards: " + ", ".join(ambiguous_normalized)
        )

    return {
        "rarityCounts": dict(sorted(counts.items())),
        "uniqueNames": len(by_name),
        "alternativeArtEntries": len(entries) - len(by_name),
        "variantSensitiveNames": changed,
    }


def load_core_names(catalog: Path, database: Path) -> list[tuple[str, str]]:
    with catalog.open("r", encoding="utf-8-sig", newline="") as stream:
        codes = sorted({int(row["official_code"]) for row in csv.DictReader(stream)})
    connection = sqlite3.connect(database)
    try:
        result: list[tuple[str, str]] = []
        for code in codes:
            row = connection.execute("SELECT name FROM texts WHERE id = ?", (code,)).fetchone()
            if row is None:
                raise ValueError(f"Core card {code:08d} is missing from the pinned database")
            result.append((f"{code:08d}", str(row[0] or "").strip()))
        return result
    finally:
        connection.close()


def resolve_base(entries: list[RarityEntry]) -> dict[str, RarityEntry]:
    grouped: dict[str, list[RarityEntry]] = defaultdict(list)
    for entry in entries:
        grouped[normalize_name(entry.english_name)].append(entry)
    resolved: dict[str, RarityEntry] = {}
    for key, candidates in grouped.items():
        base = next((entry for entry in candidates if entry.variant == "BASE"), None)
        resolved[key] = base or candidates[0]
    return resolved


def write_json(entries: list[RarityEntry], output: Path, source_pdf: Path) -> None:
    counts = Counter(entry.rarity for entry in entries)
    payload = {
        "schemaVersion": 1,
        "sourceTitle": "Catálogo de Raridades Individuais - Yu-Gi-Oh! Master Duel",
        "sourceDate": date(2026, 8, 16).isoformat(),
        "sourceFile": source_pdf.name,
        "entryCount": len(entries),
        "normalCount": counts["N"],
        "rareCount": counts["R"],
        "superRareCount": counts["SR"],
        "ultraRareCount": counts["UR"],
        "entries": [
            {
                "englishName": entry.english_name,
                "variant": entry.variant,
                "rarity": RARITY_VALUES[entry.rarity],
            }
            for entry in entries
        ],
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pdf", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--core-catalog", type=Path)
    parser.add_argument("--core-database", type=Path)
    args = parser.parse_args()

    entries = parse_pdf(args.pdf)
    validation = validate(entries)
    write_json(entries, args.output, args.pdf)

    report: dict[str, object] = {
        "schemaVersion": 1,
        "sourcePdf": str(args.pdf),
        "output": str(args.output),
        **validation,
    }
    if args.core_catalog and args.core_database:
        resolver = resolve_base(entries)
        core_names = load_core_names(args.core_catalog, args.core_database)
        missing = [
            {"cardId": card_id, "englishName": english_name}
            for card_id, english_name in core_names
            if normalize_name(english_name) not in resolver
        ]
        report["coreCards"] = len(core_names)
        report["coreCardsResolved"] = len(core_names) - len(missing)
        report["coreCardsMissing"] = missing

    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
    print(json.dumps(report, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
