#!/usr/bin/env python3
"""Complete the 200-card catalog from a pinned YGOPRODeck API snapshot.

The tool keeps downloaded art outside Unity's Assets directory until the
12-card vertical slice passes. It validates every selected code against the
pinned BabelCDB and CardScripts repositories before changing the catalog.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import sqlite3
import time
import urllib.request
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


CATALOG_FIELDS = [
    "official_code",
    "name",
    "source",
    "type",
    "deck_origin",
    "complexity",
    "vertical_slice_role",
    "script_path",
    "script_found",
    "database_found",
    "source_art_path",
    "art_asset",
    "image_available",
    "test_status",
    "notes",
    "database_name",
    "database_alias",
]

TYPE_NAMES_PT = {
    "Normal Monster": "Monstro Normal",
    "Effect Monster": "Monstro de Efeito",
    "Flip Effect Monster": "Monstro de Efeito de Virar",
    "Tuner Monster": "Monstro Regulador de Efeito",
    "Normal Tuner Monster": "Monstro Regulador Normal",
    "Ritual Monster": "Monstro de Ritual",
    "Ritual Effect Monster": "Monstro de Ritual com Efeito",
    "Fusion Monster": "Monstro de Fusão",
    "Synchro Monster": "Monstro Sincro",
    "Synchro Tuner Monster": "Monstro Sincro Regulador",
    "XYZ Monster": "Monstro Xyz",
    "Link Monster": "Monstro Link",
    "Pendulum Normal Monster": "Monstro Pêndulo Normal",
    "Pendulum Effect Monster": "Monstro Pêndulo de Efeito",
    "Spell Card": "Carta de Magia",
    "Trap Card": "Carta de Armadilha",
}

CATEGORY_NAMES = {
    "Blue-Eyes package (18)": "ygoprodeck_blue_eyes",
    "Dark Magician package (18)": "ygoprodeck_dark_magician",
    "Red-Eyes package (15)": "ygoprodeck_red_eyes",
    "Classic and interactive Main Deck cards (24)": "ygoprodeck_classic",
    "Spells and Traps (21)": "ygoprodeck_interaction",
    "Additional summon mechanics (5)": "ygoprodeck_summon_showcase",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def parse_selection(path: Path) -> list[tuple[str, str]]:
    category = ""
    selected: list[tuple[str, str]] = []
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line:
            continue
        if line.startswith("#"):
            heading = line.lstrip("#").strip()
            category = CATEGORY_NAMES.get(heading, category)
            continue
        if not category:
            raise ValueError(f"Selection has a card before a recognized category: {line}")
        selected.append((line, category))
    if len(selected) != 101:
        raise ValueError(f"Expected exactly 101 selected cards, found {len(selected)}")
    if len({name.casefold() for name, _ in selected}) != len(selected):
        raise ValueError("Selection contains duplicate names")
    return selected


def load_api(path: Path) -> list[dict[str, Any]]:
    with path.open("r", encoding="utf-8") as stream:
        payload = json.load(stream)
    data = payload.get("data")
    if not isinstance(data, list):
        raise ValueError(f"Unexpected YGOPRODeck payload in {path}")
    return data


def english_by_name(cards: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for card in cards:
        key = card["name"].casefold()
        if key in result:
            raise ValueError(f"Ambiguous English API name: {card['name']}")
        result[key] = card
    return result


def portuguese_by_art_id(cards: list[dict[str, Any]]) -> dict[int, dict[str, Any]]:
    result: dict[int, dict[str, Any]] = {}
    for card in cards:
        result[int(card["id"])] = card
        for image in card.get("card_images", []):
            result[int(image["id"])] = card
    return result


def image_url_for(card: dict[str, Any], code: int) -> str:
    for image in card.get("card_images", []):
        if int(image["id"]) == code:
            return str(image["image_url"])
    return f"https://images.ygoprodeck.com/images/cards/{code}.jpg"


def has_effect(card: dict[str, Any]) -> bool:
    misc = card.get("misc_info") or []
    if misc and "has_effect" in misc[0]:
        return int(misc[0]["has_effect"]) != 0
    return card["type"] not in {"Normal Monster", "Normal Tuner Monster"}


def classify_complexity(card: dict[str, Any]) -> str:
    card_type = card["type"]
    description = str(card.get("desc", "")).casefold()
    race = str(card.get("race", "")).casefold()

    if card_type in {"Normal Monster", "Normal Tuner Monster"} or not has_effect(card):
        return "simple"
    if any(kind in card_type for kind in ("Fusion", "Synchro", "XYZ", "Link", "Ritual", "Pendulum")):
        return "extra_deck" if card_type not in {"Ritual Effect Monster", "Ritual Monster"} else "exceptional"
    if "counter" in race or "negate" in description:
        return "negation"
    if "quick-play" in race or "(quick effect)" in description:
        return "quick"
    if "continuous" in race:
        return "continuous"
    if "instead" in description:
        return "replacement"
    if "when " in description or "if " in description:
        return "trigger"
    return "intermediate"


def download_image(url: str, destination: Path) -> str:
    if not destination.exists():
        request = urllib.request.Request(
            url,
            headers={"User-Agent": "ArcaneDuelCatalog/1.0 (+local development)"},
        )
        with urllib.request.urlopen(request, timeout=30) as response:
            content = response.read()
        if len(content) < 10_000 or not content.startswith(b"\xff\xd8\xff"):
            raise ValueError(f"Downloaded file is not a valid JPEG: {url}")
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content)
        time.sleep(0.15)
    return sha256(destination)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--api-en", type=Path, required=True)
    parser.add_argument("--api-pt", type=Path, required=True)
    parser.add_argument("--selection", type=Path, required=True)
    parser.add_argument("--download-images", action="store_true")
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    catalog_path = project_root / "Documentation" / "CardCatalog.csv"
    database_path = project_root / "ThirdParty" / "BabelCDB" / "cards.cdb"
    script_root = project_root / "ThirdParty" / "CardScripts" / "official"
    staging_root = project_root / "ContentStaging" / "YGOPRODeck"
    art_root = staging_root / "Art"
    manifest_path = staging_root / "CardSelection.json"
    audit_path = project_root / "Documentation" / "YgoProDeckImportAudit.md"

    selected_names = parse_selection(args.selection)
    api_en = load_api(args.api_en)
    api_pt = load_api(args.api_pt)
    by_name = english_by_name(api_en)
    pt_by_art = portuguese_by_art_id(api_pt)

    with catalog_path.open("r", encoding="utf-8-sig", newline="") as stream:
        original_rows = list(csv.DictReader(stream))
    if len(original_rows) != 99:
        raise ValueError(f"Expected the audited 99-card base, found {len(original_rows)}")
    existing_codes = {int(row["official_code"]) for row in original_rows}

    connection = sqlite3.connect(database_path)
    new_rows: list[dict[str, str]] = []
    manifest_items: list[dict[str, Any]] = []

    try:
        for english_name, category in selected_names:
            api_card = by_name.get(english_name.casefold())
            if api_card is None:
                raise ValueError(f"Card not found in English API snapshot: {english_name}")

            api_code = int(api_card["id"])
            row = connection.execute(
                """
                SELECT d.id, d.alias, d.type, t.name
                FROM datas d
                JOIN texts t ON t.id = d.id
                WHERE d.id = ?
                """,
                (api_code,),
            ).fetchone()
            if row is None:
                raise ValueError(f"Card {api_code} is absent from BabelCDB")

            canonical_code = int(row[1]) if int(row[1] or 0) else api_code
            canonical = connection.execute(
                """
                SELECT d.id, d.alias, d.type, t.name
                FROM datas d
                JOIN texts t ON t.id = d.id
                WHERE d.id = ?
                """,
                (canonical_code,),
            ).fetchone()
            if canonical is None:
                raise ValueError(f"Canonical card {canonical_code} is absent from BabelCDB")
            if canonical_code in existing_codes:
                raise ValueError(f"Selected code already exists in catalog: {canonical_code}")

            direct_script = script_root / f"c{canonical_code}.lua"
            no_effect = not has_effect(api_card)
            if direct_script.exists():
                script_path = f"official/c{canonical_code}.lua"
                script_status = "true"
            elif no_effect:
                script_path = ""
                script_status = "not_required_no_effect"
            else:
                raise ValueError(
                    f"Card {canonical_code} ({english_name}) has no official script"
                )

            image_url = image_url_for(api_card, canonical_code)
            art_path = art_root / f"{canonical_code}.jpg"
            image_hash = ""
            if args.download_images:
                image_hash = download_image(image_url, art_path)

            localized_card = pt_by_art.get(canonical_code)
            localized_name = (
                str(localized_card["name"]) if localized_card else english_name
            )
            type_pt = TYPE_NAMES_PT.get(api_card["type"], api_card["type"])
            code_text = f"{canonical_code:08d}"

            new_rows.append(
                {
                    "official_code": code_text,
                    "name": localized_name,
                    "source": "official",
                    "type": type_pt,
                    "deck_origin": category,
                    "complexity": classify_complexity(api_card),
                    "vertical_slice_role": "",
                    "script_path": script_path,
                    "script_found": script_status,
                    "database_found": "true",
                    "source_art_path": image_url,
                    "art_asset": f"Assets/Game/Cards/Art/{canonical_code}.jpg",
                    "image_available": str(art_path.exists()).lower(),
                    "test_status": "pending",
                    "notes": "Staged outside Assets until the 12-card vertical slice passes.",
                    "database_name": str(canonical[3]),
                    "database_alias": "",
                }
            )
            manifest_items.append(
                {
                    "officialCode": canonical_code,
                    "apiEntryCode": api_code,
                    "nameEnglish": english_name,
                    "namePortuguese": localized_name,
                    "type": api_card["type"],
                    "deckOrigin": category,
                    "imageUrl": image_url,
                    "stagedPath": f"ContentStaging/YGOPRODeck/Art/{canonical_code}.jpg",
                    "sha256": image_hash,
                    "scriptPath": script_path or None,
                    "scriptStatus": script_status,
                }
            )
    finally:
        connection.close()

    all_rows = original_rows + new_rows
    codes = [int(row["official_code"]) for row in all_rows]
    if len(all_rows) != 200 or len(set(codes)) != 200:
        raise ValueError(
            f"Catalog invariant failed: rows={len(all_rows)}, unique={len(set(codes))}"
        )

    catalog_path.parent.mkdir(parents=True, exist_ok=True)
    with catalog_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=CATALOG_FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(all_rows)

    manifest = {
        "schemaVersion": 1,
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "source": {
            "provider": "YGOPRODeck",
            "apiGuide": "https://ygoprodeck.com/api-guide/",
            "englishSnapshotSha256": sha256(args.api_en),
            "portugueseSnapshotSha256": sha256(args.api_pt),
            "imagePolicy": "One local download per selected official code.",
            "rightsNotice": (
                "Yu-Gi-Oh! card images, symbols, and card text remain the "
                "property of their respective rights holders. Staged for local "
                "development; redistribution requires a separate rights review."
            ),
        },
        "count": len(manifest_items),
        "items": manifest_items,
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    status_counts = Counter(item["scriptStatus"] for item in manifest_items)
    type_counts = Counter(item["type"] for item in manifest_items)
    audit_lines = [
        "# YGOPRODeck Import Audit",
        "",
        f"- Generated UTC: {manifest['generatedUtc']}",
        "- Source: https://ygoprodeck.com/api-guide/",
        "- Selected official cards: 101",
        "- BabelCDB matches: 101/101",
        f"- Direct official scripts: {status_counts.get('true', 0)}",
        (
            "- Cards explicitly requiring no effect script: "
            f"{status_counts.get('not_required_no_effect', 0)}"
        ),
        "- Missing scripts: 0",
        f"- Downloaded and hashed images: {sum(bool(item['sha256']) for item in manifest_items)}",
        "- Final catalog rows: 200",
        "- Final unique official codes: 200",
        "",
        "The 101 downloaded images remain in `ContentStaging/YGOPRODeck/Art`,",
        "outside Unity's `Assets` folder, until the 12-card vertical slice passes.",
        "Images are intentionally excluded from Git; their URLs and SHA-256 hashes",
        "are retained in `ContentStaging/YGOPRODeck/CardSelection.json`.",
        "",
        "## Selected type distribution",
        "",
    ]
    audit_lines.extend(f"- {card_type}: {count}" for card_type, count in sorted(type_counts.items()))
    audit_lines.extend(
        [
            "",
            "## Rights note",
            "",
            "The YGOPRODeck API guide states that Yu-Gi-Oh! card images, symbols,",
            "and card text are copyrighted by their respective rights holders.",
            "The staged art is for local development and does not establish",
            "redistribution or commercial-use rights.",
            "",
        ]
    )
    audit_path.write_text("\n".join(audit_lines), encoding="utf-8")

    print(
        json.dumps(
            {
                "catalogRows": len(all_rows),
                "uniqueCodes": len(set(codes)),
                "newCards": len(new_rows),
                "imagesHashed": sum(bool(item["sha256"]) for item in manifest_items),
                "manifest": str(manifest_path),
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
