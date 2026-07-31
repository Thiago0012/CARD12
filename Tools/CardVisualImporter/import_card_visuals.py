#!/usr/bin/env python3
"""Build Arcane Duel's presentation-only 200-card visual catalog.

This tool deliberately does not generate game rules. It validates the pinned
CSV, copies art to a stable StreamingAssets layout, and emits deterministic
visual metadata plus an auditable batch report.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import shutil
import sys
import unicodedata
from pathlib import Path


MINIMUM_CARD_COUNT = 200


def normalized(value: str) -> str:
    decomposed = unicodedata.normalize("NFKD", value or "")
    return "".join(character for character in decomposed if not unicodedata.combining(character)).lower()


def frame_style(card_type: str) -> str:
    value = normalized(card_type)
    if "armadilha" in value:
        return "trap"
    if "magia" in value:
        return "spell"
    if "fusao" in value:
        return "fusion"
    if "sincro" in value:
        return "synchro"
    if "xyz" in value:
        return "xyz"
    if "link" in value:
        return "link"
    if "ritual" in value:
        return "ritual"
    if "pendulo" in value:
        return "pendulum"
    if "normal" in value:
        return "normal"
    return "effect"


def risk_level(complexity: str) -> str:
    value = normalized(complexity)
    if value in {"simple", "normal_monster", "normal_spell"}:
        return "A"
    if value in {
        "intermediate",
        "ignition_effect",
        "trigger",
        "trigger_effect",
        "quick",
        "quick_effect",
        "quick_play_spell",
        "trap",
    }:
        return "B"
    return "C"


def presentation_profile(style: str) -> tuple[str, str]:
    if style in {"spell", "trap"}:
        return "none", "arcane_activation"
    if style in {"fusion", "synchro", "xyz", "link", "ritual", "pendulum"}:
        return "extra_summon", "arcane_summon"
    return "normal_summon", "arcane_summon"


def locate_source(row: dict[str, str], provided_root: Path, downloaded_root: Path) -> Path:
    source = row["source_art_path"]
    if source.startswith("Cards.rar::"):
        relative = source.split("::", 1)[1].strip("/\\")
        return provided_root.joinpath(*relative.replace("\\", "/").split("/"))
    return downloaded_root / f"{int(row['official_code'])}.jpg"


def validate_jpeg(path: Path) -> None:
    if not path.is_file():
        raise FileNotFoundError(path)
    if path.stat().st_size < 1024:
        raise ValueError(f"Image is unexpectedly small: {path}")
    with path.open("rb") as stream:
        if stream.read(2) != b"\xff\xd8":
            raise ValueError(f"Image is not a JPEG: {path}")


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=False) + "\n",
        encoding="utf-8",
    )


def write_markdown(path: Path, batches: list[dict[str, object]], catalog_hash: str) -> None:
    lines = [
        "# Card visual import report",
        "",
        f"- Catalog SHA-256: `{catalog_hash}`",
        f"- Cards: {sum(int(batch['cardCount']) for batch in batches)}",
        f"- Batches: {len(batches)}",
        "- Missing database rows: 0",
        "- Missing scripts: 0",
        "- Missing art: 0",
        "- Duplicate official codes: 0",
        "",
        "| Batch | Range | Cards | Risk A | Risk B | Risk C | Result |",
        "|---:|---|---:|---:|---:|---:|---|",
    ]
    for batch in batches:
        lines.append(
            f"| {batch['batch']} | {batch['firstCode']}–{batch['lastCode']} | "
            f"{batch['cardCount']} | {batch['riskA']} | {batch['riskB']} | "
            f"{batch['riskC']} | PASS |"
        )
    lines.extend(
        [
            "",
            "Visual entries contain presentation metadata only. Card legality, costs,",
            "targets, chains, and effect resolution remain exclusively in ocgcore and Lua.",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--provided-root", required=True, type=Path)
    parser.add_argument("--downloaded-root", required=True, type=Path)
    parser.add_argument("--art-output", required=True, type=Path)
    parser.add_argument("--visual-output", required=True, type=Path)
    parser.add_argument("--report-json", required=True, type=Path)
    parser.add_argument("--report-markdown", required=True, type=Path)
    parser.add_argument("--batch-size", type=int, default=25)
    args = parser.parse_args()

    raw_catalog = args.catalog.read_bytes()
    catalog_hash = hashlib.sha256(raw_catalog).hexdigest().upper()
    with args.catalog.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))

    if len(rows) < MINIMUM_CARD_COUNT:
        raise ValueError(
            f"Expected at least {MINIMUM_CARD_COUNT} catalog rows, found {len(rows)}."
        )
    codes = [int(row["official_code"]) for row in rows]
    if len(set(codes)) != len(rows):
        raise ValueError("The catalog contains duplicate official codes.")

    args.art_output.mkdir(parents=True, exist_ok=True)
    visuals: list[dict[str, object]] = []
    for row in sorted(rows, key=lambda item: int(item["official_code"])):
        code = int(row["official_code"])
        if row["database_found"].lower() != "true":
            raise ValueError(f"Database row missing for {code}.")
        script_status = row["script_found"].lower()
        if script_status not in {"true", "via_alias", "not_required_no_effect"}:
            raise ValueError(f"Script status is unresolved for {code}: {script_status}")

        source = locate_source(row, args.provided_root, args.downloaded_root)
        validate_jpeg(source)
        destination = args.art_output / f"{code}.jpg"
        if source.resolve() != destination.resolve():
            shutil.copy2(source, destination)

        style = frame_style(row["type"])
        summon_vfx, activation_sfx = presentation_profile(style)
        visuals.append(
            {
                "officialCode": code,
                "artFile": f"{code}.jpg",
                "frameStyle": style,
                "summonVfx": summon_vfx,
                "activationSfx": activation_sfx,
                "riskLevel": risk_level(row["complexity"]),
                "scriptStatus": script_status,
                "scriptFile": Path(row["script_path"]).name if row["script_path"] else "",
                "presentationTags": [
                    row["type"],
                    row["complexity"],
                    row["deck_origin"],
                ],
            }
        )

    batch_size = max(1, args.batch_size)
    batches: list[dict[str, object]] = []
    for index in range(0, len(visuals), batch_size):
        cards = visuals[index : index + batch_size]
        batches.append(
            {
                "batch": (index // batch_size) + 1,
                "firstCode": cards[0]["officialCode"],
                "lastCode": cards[-1]["officialCode"],
                "cardCount": len(cards),
                "riskA": sum(card["riskLevel"] == "A" for card in cards),
                "riskB": sum(card["riskLevel"] == "B" for card in cards),
                "riskC": sum(card["riskLevel"] == "C" for card in cards),
                "result": "PASS",
            }
        )

    visual_payload = {
        "schemaVersion": 1,
        "count": len(visuals),
        "catalogSha256": catalog_hash,
        "cards": visuals,
    }
    report_payload = {
        "schemaVersion": 1,
        "catalogSha256": catalog_hash,
        "batchSize": batch_size,
        "batchCount": len(batches),
        "totalCards": len(visuals),
        "missingArt": [],
        "missingScripts": [],
        "missingDatabaseRows": [],
        "duplicateCodes": [],
        "batches": batches,
    }
    write_json(args.visual_output, visual_payload)
    write_json(args.report_json, report_payload)
    write_markdown(args.report_markdown, batches, catalog_hash)
    print(
        f"ARCANE_VISUAL_IMPORT_OK cards={len(visuals)} "
        f"batches={len(batches)} art={args.art_output}"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"ARCANE_VISUAL_IMPORT_FAILED {error}", file=sys.stderr)
        raise
