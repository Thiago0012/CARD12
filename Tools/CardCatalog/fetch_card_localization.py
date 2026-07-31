#!/usr/bin/env python3
"""Fetch one cached Portuguese presentation snapshot for the Core catalog."""

from __future__ import annotations

import argparse
import csv
import json
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


API_URL = "https://db.ygoprodeck.com/api/v7/cardinfo.php"


def load_codes(path: Path) -> list[int]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    return sorted(
        int(row["official_code"])
        for row in rows
        if row.get("origin") != "runtime_dependency"
    )


def load_overrides(path: Path | None) -> dict[int, dict]:
    if path is None:
        return {}
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("schemaVersion") != 1:
        raise ValueError("Manual localization must use schemaVersion 1")
    return {int(card["code"]): card for card in payload.get("cards", [])}


def request_cards(codes: list[int]) -> list[dict]:
    query = urllib.parse.urlencode(
        {"language": "pt", "id": ",".join(str(code) for code in codes)},
        safe=",",
    )
    request = urllib.request.Request(
        f"{API_URL}?{query}",
        headers={"User-Agent": "ArcaneDuelLocalization/1.0"},
    )
    with urllib.request.urlopen(request, timeout=45) as response:
        return json.load(response).get("data", [])


def localized_by_code(codes: list[int]) -> dict[int, dict]:
    requested = set(codes)
    localized: dict[int, dict] = {}
    for offset in range(0, len(codes), 100):
        for card in request_cards(codes[offset : offset + 100]):
            candidate_codes = {int(card["id"])}
            candidate_codes.update(
                int(image["id"])
                for image in card.get("card_images", [])
                if image.get("id") is not None
            )
            for code in candidate_codes & requested:
                localized[code] = {
                    "code": code,
                    "name": str(card.get("name", "")).strip(),
                    "description": str(card.get("desc", "")).strip(),
                    "strings": [],
                }
        time.sleep(0.2)
    return localized


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--manual", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    codes = load_codes(args.catalog)
    overrides = load_overrides(args.manual)
    outside_catalog = sorted(set(overrides) - set(codes))
    if outside_catalog:
        raise ValueError(
            "Manual localization references cards outside the catalog: "
            + ", ".join(f"{code:08d}" for code in outside_catalog)
        )

    localized = localized_by_code(codes)
    localized.update(overrides)
    missing = sorted(set(codes) - set(localized))
    payload = {
        "schemaVersion": 1,
        "language": "pt-BR",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "source": "https://db.ygoprodeck.com/api/v7/cardinfo.php?language=pt",
        "manualOverrides": len(overrides),
        "missingCodes": missing,
        "cards": [localized[code] for code in sorted(localized)],
    }
    args.output.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(
        "ARCANE_PT_BR_LOCALIZATION_OK "
        f"catalog={len(codes)} localized={len(localized)} "
        f"manual={len(overrides)} missing={len(missing)}"
    )


if __name__ == "__main__":
    main()
