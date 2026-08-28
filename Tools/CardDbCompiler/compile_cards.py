#!/usr/bin/env python3
"""Compile a deterministic BabelCDB slice for Arcane Duel.

The generated binary is deliberately small and deterministic. It contains the
exact fields consumed by ocgcore's OCG_DataReader callback; display strings are
kept in a separate UTF-8 JSON file for Unity presentation.

The project's authored catalog remains the audited 200-card set. The runtime
catalog can be a strict superset so imported presentation/deck content can use
the same new Core without importing any legacy rules engine.
"""

from __future__ import annotations

import argparse
import csv
import json
import struct
import subprocess

try:
    import sqlite3
except ModuleNotFoundError:
    sqlite3 = None
from pathlib import Path

MAGIC = b"ADCB"
VERSION = 1
TYPE_LINK = 0x4000000


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--database", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--minimum-count", type=int, default=200)
    parser.add_argument("--sqlite3-cli", type=Path)
    parser.add_argument("--localization", type=Path)
    parser.add_argument("--custom-cards", type=Path)
    return parser.parse_args()


def load_localization(path: Path | None) -> dict[int, dict]:
    if path is None:
        return {}
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("schemaVersion") != 1 or not isinstance(payload.get("cards"), list):
        raise ValueError("Localization file must use schemaVersion 1 and contain cards")
    overrides: dict[int, dict] = {}
    for card in payload["cards"]:
        code = int(card["code"])
        if code in overrides:
            raise ValueError(f"Localization contains duplicate code {code:08d}")
        if not str(card.get("name", "")).strip():
            raise ValueError(f"Localization name is empty for {code:08d}")
        if not isinstance(card.get("strings", []), list):
            raise ValueError(f"Localization strings must be a list for {code:08d}")
        overrides[code] = card
    return overrides


def load_custom_cards(path: Path | None) -> dict[int, dict]:
    if path is None or not path.exists():
        return {}
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("schemaVersion") != 1 or not isinstance(payload.get("cards"), list):
        raise ValueError("Custom card file must use schemaVersion 1 and contain cards")
    custom: dict[int, dict] = {}
    for raw in payload["cards"]:
        code = int(raw["code"])
        if code <= 0 or code in custom:
            raise ValueError(f"Invalid or duplicate custom code {code:08d}")
        if not str(raw.get("name", "")).strip():
            raise ValueError(f"Custom card name is empty for {code:08d}")
        strings = raw.get("strings", [])
        setcodes = raw.get("setcodes", [])
        if not isinstance(strings, list) or not isinstance(setcodes, list):
            raise ValueError(f"Custom card lists are invalid for {code:08d}")
        custom[code] = raw
    return custom


class QueryRows(list):
    def fetchone(self):
        return self[0] if self else None


class SqliteCliConnection:
    def __init__(self, executable: Path, database: Path) -> None:
        self.executable = executable
        self.database = database

    def __enter__(self):
        return self

    def __exit__(self, _type, _value, _traceback):
        return False

    def execute(self, statement: str, parameters=()) -> QueryRows:
        rendered = statement
        for value in parameters:
            rendered = rendered.replace("?", str(int(value)), 1)
        process = subprocess.run(
            [str(self.executable), "-json", str(self.database), rendered],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            encoding="utf-8",
        )
        rows = json.loads(process.stdout or "[]")
        return QueryRows(tuple(row.values()) for row in rows)


def open_connection(database: Path, sqlite3_cli: Path | None):
    if sqlite3_cli is not None:
        return SqliteCliConnection(sqlite3_cli, database)
    if sqlite3 is None:
        raise RuntimeError(
            "Python sqlite3 is unavailable; pass --sqlite3-cli with a sqlite3 executable"
        )
    return sqlite3.connect(database)


def selected_codes(path: Path, minimum_count: int) -> list[int]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        codes = [int(row["official_code"]) for row in csv.DictReader(stream)]
    unique = sorted(set(codes))
    if len(codes) != len(unique):
        raise ValueError(
            f"Catalog contains duplicate codes: {len(codes)} rows / {len(unique)} unique"
        )
    if len(unique) < minimum_count:
        raise ValueError(
            f"Expected at least {minimum_count} unique catalog codes, found {len(unique)}"
        )
    return unique


def expand_alias_dependencies(
    codes: list[int], connection: sqlite3.Connection, custom_cards: dict[int, dict]
) -> list[int]:
    """Include every canonical record an authored card can request at runtime."""
    expanded = set(codes)
    pending = list(codes)
    while pending:
        code = pending.pop()
        if code in custom_cards:
            alias = int(custom_cards[code].get("alias", 0) or 0)
        else:
            row = connection.execute(
                "SELECT alias FROM datas WHERE id = ?", (code,)
            ).fetchone()
            if row is None:
                raise ValueError(
                    f"Official card {code:08d} is missing from the pinned database"
                )
            alias = int(row[0] or 0)
        if alias != 0 and alias not in expanded:
            expanded.add(alias)
            pending.append(alias)
    return sorted(expanded)


def split_setcodes(raw: int) -> list[int]:
    # SQLite exposes this signed; reinterpret it exactly as the CDB uint64.
    value = raw & 0xFFFFFFFFFFFFFFFF
    return [part for shift in (0, 16, 32, 48) if (part := (value >> shift) & 0xFFFF)]


def compile_database(
    codes: list[int],
    connection: sqlite3.Connection,
    output: Path,
    localization: dict[int, dict],
    custom_cards: dict[int, dict],
) -> None:
    output.mkdir(parents=True, exist_ok=True)
    records: list[tuple] = []
    text_cards: list[dict] = []

    for code in codes:
        authored = custom_cards.get(code)
        if authored is not None:
            alias = int(authored.get("alias", 0) or 0)
            card_type = int(authored["type"])
            attack = int(authored.get("attack", 0))
            defense = int(authored.get("defense", 0))
            packed_level = (
                int(authored.get("level", 0)) & 0xFF
                | (int(authored.get("rightScale", 0)) & 0xFF) << 16
                | (int(authored.get("leftScale", 0)) & 0xFF) << 24
            )
            race = int(authored.get("race", 0))
            attribute = int(authored.get("attribute", 0))
            setcodes = [int(value) & 0xFFFF for value in authored.get("setcodes", [])]
            source_name = str(authored.get("englishName") or authored["name"])
            source_description = str(authored.get("description", ""))
            source_strings = [str(value or "") for value in authored.get("strings", [])]
        else:
            data = connection.execute(
                "SELECT id, alias, setcode, type, atk, def, level, race, attribute "
                "FROM datas WHERE id = ?",
                (code,),
            ).fetchone()
            text = connection.execute(
                "SELECT name, desc, str1, str2, str3, str4, str5, str6, str7, str8, "
                "str9, str10, str11, str12, str13, str14, str15, str16 "
                "FROM texts WHERE id = ?",
                (code,),
            ).fetchone()
            if data is None or text is None:
                raise ValueError(
                    f"Official card {code:08d} is missing from the pinned database"
                )
            _, alias, setcode, card_type, attack, defense, packed_level, race, attribute = data
            setcodes = split_setcodes(setcode)
            source_name = text[0] or f"Card {code:08d}"
            source_description = text[1] or ""
            source_strings = [value or "" for value in text[2:18]]

        unsigned_level = packed_level & 0xFFFFFFFF
        level = unsigned_level & 0xFF
        if packed_level < 0:
            level = -level
        left_scale = (unsigned_level >> 24) & 0xFF
        right_scale = (unsigned_level >> 16) & 0xFF
        link_marker = defense if card_type & TYPE_LINK else 0
        core_defense = 0 if card_type & TYPE_LINK else defense
        records.append(
            (
                code,
                alias,
                card_type,
                level,
                attribute,
                race & 0xFFFFFFFFFFFFFFFF,
                attack,
                core_defense,
                left_scale,
                right_scale,
                link_marker,
                setcodes,
            )
        )
        localized = localization.get(code, {})
        localized_strings = localized.get("strings", [])
        strings = list(source_strings[:16])
        strings.extend([""] * (16 - len(strings)))
        for index, value in enumerate(localized_strings[:16]):
            strings[index] = str(value or "")
        text_cards.append(
            {
                "code": code,
                # The official English name is retained solely as a stable
                # metadata key (for example, Master Duel rarity lookup).
                # Unity continues to present the localized name below.
                "englishName": source_name,
                "name": localized.get("name") or str(
                    authored.get("name") if authored else source_name
                ),
                "description": localized.get("description") or source_description,
                "strings": strings,
            }
        )

    with (output / "cards.bin").open("wb") as stream:
        stream.write(MAGIC)
        stream.write(struct.pack("<II", VERSION, len(records)))
        for record in records:
            (
                code,
                alias,
                card_type,
                level,
                attribute,
                race,
                attack,
                defense,
                left_scale,
                right_scale,
                link_marker,
                setcodes,
            ) = record
            stream.write(
                struct.pack(
                    "<IIIiIQiiIIIB",
                    code,
                    alias,
                    card_type,
                    level,
                    attribute,
                    race,
                    attack,
                    defense,
                    left_scale,
                    right_scale,
                    link_marker,
                    len(setcodes),
                )
            )
            for setcode in setcodes:
                stream.write(struct.pack("<H", setcode))

    payload = {"schemaVersion": VERSION, "count": len(text_cards), "cards": text_cards}
    (output / "card-texts.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"ARCANE_CARD_DB_OK count={len(records)} output={output}")


def main() -> None:
    args = parse_args()
    localization = load_localization(args.localization)
    custom_cards = load_custom_cards(args.custom_cards)
    authored_codes = selected_codes(args.catalog, args.minimum_count)
    with open_connection(args.database, args.sqlite3_cli) as connection:
        runtime_codes = expand_alias_dependencies(
            authored_codes,
            connection,
            custom_cards,
        )
        unknown_localizations = sorted(set(localization) - set(runtime_codes))
        if unknown_localizations:
            raise ValueError(
                "Localization references cards outside the runtime catalog: "
                + ", ".join(f"{code:08d}" for code in unknown_localizations)
            )
        compile_database(
            runtime_codes,
            connection,
            args.output,
            localization,
            custom_cards,
        )


if __name__ == "__main__":
    main()
