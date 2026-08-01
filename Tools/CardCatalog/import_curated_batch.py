#!/usr/bin/env python3
"""Import a captured curated-deck manifest into Arcane Duel catalogs."""

from __future__ import annotations

import argparse
import csv
import json
import shutil
import sqlite3
import time
import urllib.parse
import urllib.request
from pathlib import Path


CORE_FIELDS = ["official_code", "script_code", "origin", "type"]
VISUAL_FIELDS = [
    "official_code", "name", "source", "type", "deck_origin",
    "complexity", "vertical_slice_role", "script_path", "script_found",
    "database_found", "source_art_path", "art_asset", "image_available",
    "test_status", "notes", "database_name", "database_alias",
]


def load_rows(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def append_rows(
    path: Path,
    fields: list[str],
    rows: list[dict[str, str]],
) -> None:
    if not rows:
        return
    with path.open("a", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(
            stream,
            fieldnames=fields,
            lineterminator="\n",
        )
        writer.writerows(rows)


def type_name(card_type: int) -> str:
    if card_type & 0x4000000:
        return "Monstro Link"
    if card_type & 0x800000:
        return "Monstro Xyz"
    if card_type & 0x2000:
        return "Monstro Sincro"
    if card_type & 0x40:
        return "Monstro de Fusão"
    if card_type & 0x80:
        return "Monstro de Ritual"
    if card_type & 0x1:
        if card_type & 0x10:
            return (
                "Monstro Regulador Normal"
                if card_type & 0x1000
                else "Monstro Normal"
            )
        if card_type & 0x1000:
            return "Monstro Regulador de Efeito"
        return "Monstro de Efeito"
    if card_type & 0x2:
        return "Carta de Magia"
    if card_type & 0x4:
        return "Carta de Armadilha"
    return "Carta"


def complexity(card_type: int, description: str) -> str:
    lowered = description.casefold()
    if card_type & 0x10 and not card_type & 0x20:
        return "simple"
    if card_type & (0x40 | 0x2000 | 0x800000 | 0x4000000):
        return "extra_deck"
    if "negate" in lowered:
        return "negation"
    if "quick effect" in lowered or card_type & 0x10000:
        return "quick"
    if card_type & 0x20000:
        return "continuous"
    if "when " in lowered or "if " in lowered:
        return "trigger"
    return "intermediate"


def script_for(
    project: Path,
    code: int,
    alias: int,
    card_type: int,
) -> tuple[str, str]:
    official = project / "ThirdParty/CardScripts/official"
    custom = project / "ThirdParty/CardScripts" / f"c{code}.lua"
    if (official / f"c{code}.lua").exists():
        return str(code), f"official/c{code}.lua"
    if alias and (official / f"c{alias}.lua").exists():
        return str(alias), f"official/c{alias}.lua"
    if custom.exists():
        return str(code), f"official/c{code}.lua"
    if card_type & 0x10 and not card_type & 0x20:
        return "", ""
    raise ValueError(f"No pinned script for effect card {code}")


def append_core(project: Path, codes: list[int], origin: str) -> int:
    catalog = project / "Documentation/CoreCardCatalog.csv"
    existing = load_rows(catalog)
    known = {int(row["official_code"]) for row in existing}
    database = sqlite3.connect(project / "ThirdParty/BabelCDB/cards.cdb")
    additions: list[dict[str, str]] = []
    try:
        for code in codes:
            if code in known:
                continue
            row = database.execute(
                "SELECT alias, type FROM datas WHERE id = ?",
                (code,),
            ).fetchone()
            if row is None:
                raise ValueError(f"Card {code} is absent from BabelCDB")
            alias, card_type = int(row[0] or 0), int(row[1])
            script_code, _ = script_for(
                project,
                code,
                alias,
                card_type,
            )
            additions.append(
                {
                    "official_code": str(code),
                    "script_code": script_code,
                    "origin": origin,
                    "type": str(card_type),
                }
            )
    finally:
        database.close()
    append_rows(catalog, CORE_FIELDS, additions)
    return len(additions)


def append_visual(project: Path, codes: list[int], origin: str) -> int:
    catalog = project / "Documentation/CardCatalog.csv"
    existing = load_rows(catalog)
    known = {int(row["official_code"]) for row in existing}
    localization = json.loads(
        (project / "Documentation/CardTextPtBr.json").read_text(
            encoding="utf-8"
        )
    )
    localized = {
        int(card["code"]): card
        for card in localization.get("cards", [])
    }
    database = sqlite3.connect(project / "ThirdParty/BabelCDB/cards.cdb")
    additions: list[dict[str, str]] = []
    try:
        for code in codes:
            if code in known:
                continue
            row = database.execute(
                """
                SELECT d.alias, d.type, t.name, t.desc
                FROM datas d JOIN texts t ON t.id = d.id
                WHERE d.id = ?
                """,
                (code,),
            ).fetchone()
            if row is None:
                raise ValueError(f"Card {code} is absent from BabelCDB")
            alias, card_type = int(row[0] or 0), int(row[1])
            english_name, description = str(row[2]), str(row[3])
            _, script_path = script_for(
                project,
                code,
                alias,
                card_type,
            )
            no_effect = card_type & 0x10 and not card_type & 0x20
            local = localized.get(code, {})
            art = project / f"Assets/StreamingAssets/Ygo/Art/{code}.jpg"
            additions.append(
                {
                    "official_code": str(code),
                    "name": str(local.get("name") or english_name),
                    "source": "official",
                    "type": type_name(card_type),
                    "deck_origin": origin,
                    "complexity": complexity(card_type, description),
                    "vertical_slice_role": "",
                    "script_path": script_path,
                    "script_found": (
                        "not_required_no_effect"
                        if no_effect
                        else "true"
                    ),
                    "database_found": "true",
                    "source_art_path": (
                        "https://images.ygoprodeck.com/images/cards/"
                        f"{code}.jpg"
                    ),
                    "art_asset": f"Assets/StreamingAssets/Ygo/Art/{code}.jpg",
                    "image_available": str(art.exists()).lower(),
                    "test_status": "pending",
                    "notes": "Imported from the July 2026 curated deck batch.",
                    "database_name": english_name,
                    "database_alias": str(alias) if alias else "",
                }
            )
    finally:
        database.close()
    append_rows(catalog, VISUAL_FIELDS, additions)
    return len(additions)


def download_art(project: Path, codes: list[int]) -> int:
    streaming = project / "Assets/StreamingAssets/Ygo/Art"
    deck_folder = project / "Assets/Cards/Cards/Decks/BatchJuly2026"
    streaming.mkdir(parents=True, exist_ok=True)
    deck_folder.mkdir(parents=True, exist_ok=True)
    downloaded = 0
    for code in codes:
        target = streaming / f"{code}.jpg"
        if not target.exists():
            request = urllib.request.Request(
                "https://images.ygoprodeck.com/images/cards/"
                f"{code}.jpg",
                headers={"User-Agent": "ArcaneDuelCatalog/1.0"},
            )
            with urllib.request.urlopen(request, timeout=45) as response:
                content = response.read()
            if len(content) < 10_000 or not content.startswith(b"\xff\xd8\xff"):
                raise ValueError(f"Invalid JPEG downloaded for {code}")
            target.write_bytes(content)
            downloaded += 1
            time.sleep(0.05)
        shutil.copy2(target, deck_folder / target.name)
    return downloaded


def translate_text(value: str) -> str:
    query = urllib.parse.urlencode(
        {
            "client": "gtx",
            "sl": "en",
            "tl": "pt",
            "dt": "t",
            "q": value,
        }
    )
    url = "https://translate.googleapis.com/translate_a/single?" + query
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "ArcaneDuelLocalization/1.0"},
    )
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                payload = json.load(response)
            translated = "".join(
                segment[0]
                for segment in payload[0]
                if segment and segment[0]
            )
            return translated.replace("Feitiço", "Magia").replace(
                "Invocação Especial",
                "Invocação-Especial",
            )
        except Exception:
            if attempt == 2:
                raise
            time.sleep(1 + attempt)
    raise RuntimeError("Translation retry loop ended unexpectedly")


def translate_missing(project: Path, codes: list[int]) -> int:
    snapshot = json.loads(
        (project / "Documentation/CardTextPtBr.json").read_text(
            encoding="utf-8"
        )
    )
    missing = set(int(code) for code in snapshot.get("missingCodes", []))
    requested = [code for code in codes if code in missing]
    manual_path = project / "Documentation/CardTextPtBrManual.json"
    manual = json.loads(manual_path.read_text(encoding="utf-8"))
    existing = {int(card["code"]) for card in manual.get("cards", [])}
    database = sqlite3.connect(project / "ThirdParty/BabelCDB/cards.cdb")
    additions: list[dict[str, object]] = []
    try:
        for code in requested:
            if code in existing:
                continue
            row = database.execute(
                "SELECT name, desc FROM texts WHERE id = ?",
                (code,),
            ).fetchone()
            if row is None:
                raise ValueError(f"Card text {code} is absent from BabelCDB")
            additions.append(
                {
                    "code": code,
                    "name": translate_text(str(row[0])),
                    "description": translate_text(str(row[1])),
                    "strings": [],
                }
            )
            time.sleep(0.1)
    finally:
        database.close()
    manual.setdefault("cards", []).extend(additions)
    manual["cards"] = sorted(
        manual["cards"],
        key=lambda card: int(card["code"]),
    )
    manual_path.write_text(
        json.dumps(manual, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return len(additions)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--origin", required=True)
    parser.add_argument(
        "--stage",
        choices=("core", "translate", "visual", "art"),
        required=True,
    )
    args = parser.parse_args()
    project = args.project_root.resolve()
    payload = json.loads(args.manifest.read_text(encoding="utf-8"))
    codes = [int(code) for code in payload["unionCodes"]]
    if args.stage == "core":
        changed = append_core(project, codes, args.origin)
    elif args.stage == "translate":
        changed = translate_missing(project, codes)
    elif args.stage == "visual":
        changed = append_visual(project, codes, args.origin)
    else:
        changed = download_art(project, codes)
    print(
        f"ARCANE_CURATED_BATCH_OK stage={args.stage} "
        f"cards={len(codes)} changed={changed}"
    )


if __name__ == "__main__":
    main()
