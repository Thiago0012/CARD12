#!/usr/bin/env python3
"""Import a deterministic numeric slice from the local allcards archive.

The importer deliberately keeps shop packs and structure decks out of scope.
It publishes engine-ready cards, records unsupported source images, expands
aliases and statically discoverable Token dependencies, and emits a permanent
manifest so a later batch never depends on files already removed from source.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import os
import re
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import unicodedata
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


CORE_FIELDS = ["official_code", "script_code", "origin", "card_type"]
VISUAL_FIELDS = [
    "official_code", "name", "source", "type", "deck_origin",
    "complexity", "vertical_slice_role", "script_path", "script_found",
    "database_found", "source_art_path", "art_asset", "image_available",
    "test_status", "notes", "database_name", "database_alias",
]
TOKEN_TYPE = 0x4000
NORMAL_TYPE = 0x10
EFFECT_TYPE = 0x20
PENDULUM_TYPE = 0x1000000
TYPE_LINK = 0x4000000

GLOBAL_TOKEN = re.compile(
    r"(?m)^\s*(TOKEN_[A-Z0-9_]+)\s*=\s*(\d+)\s*$"
)
LOCAL_SUCCESSOR_TOKEN = re.compile(
    r"(?m)^\s*local\s+(TOKEN_[A-Z0-9_]+)\s*=\s*id\s*\+\s*1\s*$"
)
SCRIPT_LITERAL_TOKEN = re.compile(
    r"(?m)^\s*(?:local\s+)?(TOKEN_[A-Z0-9_]+)\s*=\s*(\d+)\s*$"
)
CREATE_TOKEN = re.compile(
    r"Duel\.CreateToken\s*\(\s*[^,]+,\s*"
    r"(?P<dependency>id\s*\+\s*1|\d+|TOKEN_[A-Z0-9_]+)\s*\)"
)
LOAD_SCRIPT = re.compile(
    r"Duel\s*\.\s*LoadScript\s*\(\s*[\"']([^\"']+)[\"']"
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def normalized_lua_bytes(path: Path) -> bytes:
    """Preserve script bytes/newlines while removing inert line-end spaces."""
    return re.sub(rb"[ \t]+(?=\r?$)", b"", path.read_bytes(), flags=re.MULTILINE)


def normalize_name(value: str) -> str:
    output: list[str] = []
    pending_space = False
    for character in unicodedata.normalize("NFD", value or ""):
        if unicodedata.category(character) == "Mn":
            continue
        if character.isalnum():
            if pending_space and output:
                output.append(" ")
            output.append(character.lower())
            pending_space = False
        elif output:
            pending_space = True
    return "".join(output)


def load_api_cards(path: Path) -> list[dict]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(payload, dict):
        return list(payload.get("data", payload.get("cards", [])))
    return list(payload)


def read_csv(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def csv_bytes(rows: list[dict[str, str]], fields: list[str]) -> bytes:
    output = io.StringIO(newline="")
    writer = csv.DictWriter(output, fieldnames=fields, lineterminator="\n")
    writer.writeheader()
    writer.writerows(
        {field: str(row.get(field, "")) for field in fields}
        for row in rows
    )
    return output.getvalue().encode("utf-8")


def atomic_write(path: Path, content: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(content)
    os.replace(temporary, path)


def deterministic_meta(relative_identity: str) -> bytes:
    guid = hashlib.md5(
        ("ArcaneDuel/AllCards/" + relative_identity).encode("utf-8"),
        usedforsecurity=False,
    ).hexdigest()
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n"
    ).encode("utf-8")


def type_name(card_type: int) -> str:
    if card_type & TOKEN_TYPE:
        return "Ficha"
    if card_type & TYPE_LINK:
        return "Monstro Link"
    if card_type & 0x800000:
        return "Monstro Xyz"
    if card_type & 0x2000:
        return "Monstro Sincro"
    if card_type & 0x40:
        return "Monstro de Fusão"
    if card_type & 0x80:
        return "Monstro de Ritual"
    if card_type & 0x1000000:
        return (
            "Monstro Pêndulo Normal"
            if card_type & NORMAL_TYPE and not card_type & EFFECT_TYPE
            else "Monstro Pêndulo de Efeito"
        )
    if card_type & 0x1:
        if card_type & NORMAL_TYPE and not card_type & EFFECT_TYPE:
            return "Monstro Normal"
        return "Monstro de Efeito"
    if card_type & 0x2:
        return "Carta de Magia"
    if card_type & 0x4:
        return "Carta de Armadilha"
    return "Carta"


def frame_style(card_type: int, api_frame: str = "") -> str:
    known = {
        "normal", "effect", "ritual", "fusion", "synchro", "xyz",
        "link", "spell", "trap", "token", "effect_pendulum",
        "normal_pendulum", "fusion_pendulum", "synchro_pendulum",
        "xyz_pendulum", "ritual_pendulum",
    }
    if api_frame in known:
        return api_frame
    if card_type & TOKEN_TYPE:
        return "token"
    if card_type & TYPE_LINK:
        return "link"
    if card_type & 0x800000:
        return "xyz"
    if card_type & 0x2000:
        return "synchro"
    if card_type & 0x40:
        return "fusion"
    if card_type & 0x80:
        return "ritual"
    if card_type & 0x1000000:
        return "normal_pendulum" if card_type & NORMAL_TYPE else "effect_pendulum"
    if card_type & 0x2:
        return "spell"
    if card_type & 0x4:
        return "trap"
    if card_type & NORMAL_TYPE and not card_type & EFFECT_TYPE:
        return "normal"
    return "effect"


def complexity(card_type: int, description: str) -> str:
    lowered = (description or "").casefold()
    if card_type & TOKEN_TYPE:
        return "runtime_dependency"
    if card_type & NORMAL_TYPE and not card_type & EFFECT_TYPE:
        return "simple"
    if card_type & (0x40 | 0x2000 | 0x800000 | TYPE_LINK):
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


def risk_for(card_type: int, card_complexity: str) -> str:
    if card_type & TOKEN_TYPE:
        return "C"
    return {
        "simple": "A",
        "intermediate": "B",
        "trigger": "B",
        "continuous": "B",
        "extra_deck": "C",
        "quick": "C",
        "negation": "C",
    }.get(card_complexity, "C")


def is_plain_normal(card_type: int) -> bool:
    # Normal Pendulum monsters can still have scripted Pendulum effects.
    return (
        bool(card_type & NORMAL_TYPE)
        and not bool(card_type & EFFECT_TYPE)
        and not bool(card_type & PENDULUM_TYPE)
    )


def script_resolution(
    project: Path,
    code: int,
    alias: int,
    card_type: int,
) -> dict[str, object]:
    if is_plain_normal(card_type) or card_type & TOKEN_TYPE:
        return {
            "required": False,
            "source": None,
            "scriptCode": 0,
            "runtimeFolder": "",
            "status": "not_required_no_effect",
        }
    scripts = project / "ThirdParty" / "CardScripts"
    direct = scripts / "official" / f"c{code}.lua"
    custom = scripts / f"c{code}.lua"
    aliased = scripts / "official" / f"c{alias}.lua" if alias else None
    if direct.is_file():
        return {
            "required": True,
            "source": direct,
            "scriptCode": code,
            "runtimeFolder": "official",
            "status": "true",
        }
    if custom.is_file():
        return {
            "required": True,
            "source": custom,
            "scriptCode": code,
            "runtimeFolder": "custom",
            "status": "custom_override",
        }
    if aliased is not None and aliased.is_file():
        return {
            "required": True,
            "source": aliased,
            "scriptCode": alias,
            "runtimeFolder": "custom",
            "status": "alias_override",
        }
    return {
        "required": True,
        "source": None,
        "scriptCode": 0,
        "runtimeFolder": "",
        "status": "missing",
    }


def select_source_images(
    source_root: Path,
    start_index: int,
    count: int,
) -> list[Path]:
    images = sorted(
        (
            path for path in (source_root / "images").glob("*.jpg")
            if path.stem.isdigit()
        ),
        key=lambda path: int(path.stem),
    )
    start = start_index - 1
    selected = images[start:start + count]
    if len(selected) != count:
        raise ValueError(
            f"Requested {count} images at index {start_index}, found {len(selected)}"
        )
    return selected


def load_or_create_selection(
    source_root: Path,
    manifest_path: Path,
    batch_id: str,
    start_index: int,
    count: int,
) -> dict:
    if manifest_path.is_file():
        payload = json.loads(manifest_path.read_text(encoding="utf-8"))
        if (
            payload.get("batchId") != batch_id
            or int(payload.get("startIndex", 0)) != start_index
            or int(payload.get("requestedImageCount", 0)) != count
        ):
            raise ValueError("Existing batch manifest does not match requested range")
        return payload
    selected = select_source_images(source_root, start_index, count)
    return {
        "schemaVersion": 1,
        "batchId": batch_id,
        "sourceRoot": str(source_root),
        "startIndex": start_index,
        "requestedImageCount": count,
        "firstImageId": int(selected[0].stem),
        "lastImageId": int(selected[-1].stem),
        "createdUtc": utc_now(),
        "entries": [
            {
                "position": start_index + offset,
                "imageId": int(path.stem),
                "sourceImage": str(path),
            }
            for offset, path in enumerate(selected)
        ],
        "dependencies": [],
    }


def rarity_catalog(project: Path) -> dict[str, int]:
    payload = json.loads(
        (project / "Assets/Resources/CardData/MasterDuelRarities.json")
        .read_text(encoding="utf-8")
    )
    groups: dict[str, list[dict]] = {}
    for item in payload.get("entries", []):
        groups.setdefault(normalize_name(item.get("englishName", "")), []).append(item)
    result: dict[str, int] = {}
    for key, values in groups.items():
        selected = next(
            (
                item for item in values
                if str(item.get("variant", "")).upper() == "BASE"
            ),
            values[0],
        )
        result[key] = int(selected["rarity"])
    for key, value in list(result.items()):
        if key.startswith("the ") and key[4:] not in result:
            result[key[4:]] = value
    return result


def art_variant(owner: dict, image_id: int) -> str:
    images = [int(image.get("id", 0)) for image in owner.get("card_images", [])]
    if not images or image_id == int(owner.get("id", 0)) or image_id == images[0]:
        return "Base"
    alternate_index = images.index(image_id) if image_id in images else 1
    alternate_count = max(0, len(images) - 1)
    if alternate_count <= 1:
        return "Alt"
    if alternate_index == 1:
        return "Alt1"
    if alternate_index == 2:
        return "Alt2"
    return "Alt"


def database_snapshot(connection: sqlite3.Connection) -> dict[int, dict]:
    records: dict[int, dict] = {}
    query = """
        SELECT d.id, d.alias, d.type, t.name, t.desc,
               t.str1, t.str2, t.str3, t.str4, t.str5, t.str6, t.str7,
               t.str8, t.str9, t.str10, t.str11, t.str12, t.str13,
               t.str14, t.str15, t.str16
        FROM datas d JOIN texts t ON t.id = d.id
    """
    for row in connection.execute(query):
        records[int(row[0])] = {
            "id": int(row[0]),
            "alias": int(row[1] or 0),
            "type": int(row[2]),
            "name": str(row[3] or ""),
            "description": str(row[4] or ""),
            "strings": [str(value or "") for value in row[5:21]],
        }
    return records


def resolve_token_dependencies(
    resolutions: dict[int, dict[str, object]],
    constants_path: Path,
) -> tuple[set[int], list[str]]:
    constants = constants_path.read_text(encoding="utf-8-sig", errors="replace")
    globals_by_name = {
        match.group(1): int(match.group(2))
        for match in GLOBAL_TOKEN.finditer(constants)
    }
    dependencies: set[int] = set()
    unresolved: list[str] = []
    scanned: set[Path] = set()
    for published_code, resolution in resolutions.items():
        source = resolution.get("source")
        if not isinstance(source, Path) or source in scanned:
            continue
        scanned.add(source)
        script_code = int(resolution.get("scriptCode", published_code) or published_code)
        body = source.read_text(encoding="utf-8-sig", errors="replace")
        locals_by_name = {
            match.group(1): script_code + 1
            for match in LOCAL_SUCCESSOR_TOKEN.finditer(body)
        }
        locals_by_name.update({
            match.group(1): int(match.group(2))
            for match in SCRIPT_LITERAL_TOKEN.finditer(body)
        })
        for call in CREATE_TOKEN.finditer(body):
            expression = re.sub(r"\s+", "", call.group("dependency"))
            if expression == "id+1":
                dependencies.add(script_code + 1)
            elif expression.isdigit():
                dependencies.add(int(expression))
            elif expression in locals_by_name:
                dependencies.add(locals_by_name[expression])
            elif expression in globals_by_name:
                dependencies.add(globals_by_name[expression])
            else:
                unresolved.append(f"{source.name}:{expression}")
        for dependency in LOAD_SCRIPT.findall(body):
            if Path(dependency).name != dependency:
                unresolved.append(f"{source.name}:unsafe-load:{dependency}")
    return dependencies, unresolved


def build_plan(args: argparse.Namespace) -> dict:
    project = args.project_root.resolve()
    source = args.source_root.resolve()
    manifest_path = project / args.manifest
    manifest = load_or_create_selection(
        source,
        manifest_path,
        args.batch_id,
        args.start_index,
        args.count,
    )
    cards_en = load_api_cards(source / "metadata/cards-en.json")
    cards_pt = load_api_cards(source / "metadata/cards-pt.json")
    en_by_id = {int(card["id"]): card for card in cards_en}
    pt_by_id = {int(card["id"]): card for card in cards_pt}
    translation_by_code: dict[int, dict] = {}
    if args.translation_overlay:
        overlay_path = (project / args.translation_overlay).resolve()
        overlay = json.loads(overlay_path.read_text(encoding="utf-8"))
        if overlay.get("schemaVersion") != 1 or overlay.get("language") != "pt-BR":
            raise ValueError("Translation overlay must use schemaVersion 1 and pt-BR")
        if overlay.get("batchId") != args.batch_id:
            raise ValueError("Translation overlay batchId does not match this import")
        translation_by_code = {
            int(card["code"]): card for card in overlay.get("cards", [])
        }
    owner_by_image: dict[int, dict] = {}
    for card in cards_en:
        for image in card.get("card_images", []):
            owner_by_image[int(image["id"])] = card
    rarities = rarity_catalog(project)

    connection = sqlite3.connect(project / "ThirdParty/BabelCDB/cards.cdb")
    try:
        database = database_snapshot(connection)
    finally:
        connection.close()

    resolutions: dict[int, dict[str, object]] = {}
    entry_by_code: dict[int, dict] = {}
    for entry in manifest["entries"]:
        code = int(entry["imageId"])
        source_image = Path(entry["sourceImage"])
        if source_image.is_file():
            entry["sourceSha256"] = sha256(source_image)
        owner = en_by_id.get(code) or owner_by_image.get(code)
        record = database.get(code)
        entry.update({
            "ownerCardId": int(owner["id"]) if owner else 0,
            "englishName": str(owner.get("name", "")) if owner else "",
            "frameType": str(owner.get("frameType", "")) if owner else "",
            "artVariant": art_variant(owner, code) if owner else "Base",
            "databaseFound": record is not None,
        })
        if owner is None:
            entry.update({
                "status": "deferred_missing_metadata",
                "catalogEligible": False,
                "rarityStatus": "missing",
                "translationStatus": "missing",
            })
            continue
        pt = translation_by_code.get(code) or pt_by_id.get(int(owner["id"]))
        rarity = rarities.get(normalize_name(owner.get("name", "")), 0)
        entry.update({
            "localizedName": str(pt.get("name", "")) if pt else str(owner.get("name", "")),
            "translationStatus": "pt-BR" if pt else "english_fallback",
            "translationSource": str(pt.get("source", "official_archive_pt")) if pt else "english_fallback",
            "rarity": rarity,
            "rarityStatus": "master_duel" if rarity else "unavailable",
        })
        if record is None:
            entry.update({
                "status": "deferred_missing_engine_data",
                "catalogEligible": False,
            })
            continue
        resolution = script_resolution(
            project,
            code,
            int(record["alias"]),
            int(record["type"]),
        )
        resolutions[code] = resolution
        entry["scriptStatus"] = resolution["status"]
        if resolution["status"] == "missing":
            entry.update({
                "status": "deferred_missing_script",
                "catalogEligible": False,
            })
            continue
        token = bool(int(record["type"]) & TOKEN_TYPE)
        entry.update({
            "status": "runtime_token" if token else "ready",
            "catalogEligible": True,
            "collectible": not token,
            "cardType": int(record["type"]),
            "databaseName": record["name"],
            "databaseAlias": int(record["alias"]),
        })
        entry_by_code[code] = entry

    token_dependencies, unresolved_tokens = resolve_token_dependencies(
        resolutions,
        project / "ThirdParty/CardScripts/card_counter_constants.lua",
    )
    if unresolved_tokens:
        raise ValueError(
            "Unresolved script dependencies: " + json.dumps(unresolved_tokens)
        )
    missing_token_database = sorted(code for code in token_dependencies if code not in database)
    if missing_token_database:
        raise ValueError(
            "Token dependencies missing from BabelCDB: "
            + ", ".join(str(code) for code in missing_token_database)
        )

    previous_dependencies = {
        int(item["officialCode"]): item
        for item in manifest.get("dependencies", [])
        if item.get("officialCode") is not None
    }
    dependencies: list[dict] = []
    for code in sorted(token_dependencies):
        record = database[code]
        owner = en_by_id.get(code) or owner_by_image.get(code)
        pt = (translation_by_code.get(code) or pt_by_id.get(int(owner["id"]))) if owner else None
        source_image = source / "images" / f"{code}.jpg"
        runtime_image = project / "Assets/StreamingAssets/Ygo/Art" / f"{code}.jpg"
        dependency = {
            "officialCode": code,
            "kind": "token",
            "englishName": str(owner.get("name", record["name"])) if owner else record["name"],
            "localizedName": str(pt.get("name", "")) if pt else record["name"],
            "translationStatus": "pt-BR" if pt else "english_fallback",
            "translationSource": str(pt.get("source", "official_archive_pt")) if pt else "english_fallback",
            "cardType": int(record["type"]),
            "sourceImage": str(source_image),
            "imageAvailable": source_image.is_file() or runtime_image.is_file(),
        }
        if source_image.is_file():
            dependency["sourceSha256"] = sha256(source_image)
        elif code in previous_dependencies and previous_dependencies[code].get("sourceSha256"):
            dependency["sourceSha256"] = previous_dependencies[code]["sourceSha256"]
        dependencies.append(dependency)
    manifest["dependencies"] = dependencies
    manifest["analyzedUtc"] = utc_now()

    ready_entries = [
        entry for entry in manifest["entries"] if entry.get("catalogEligible")
    ]
    deferred_entries = [
        entry for entry in manifest["entries"] if not entry.get("catalogEligible")
    ]
    if len(ready_entries) + len(deferred_entries) != args.count:
        raise AssertionError("Batch classification lost source entries")

    published_visual_path = (
        project / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json"
    )
    published_visual_payload = json.loads(
        published_visual_path.read_text(encoding="utf-8")
    )
    published_visual_codes = {
        int(item["officialCode"])
        for item in published_visual_payload.get("cards", [])
    }

    art_operations: list[tuple[Path, Path, int, str]] = []
    art_conflicts: list[dict] = []
    for entry in ready_entries:
        code = int(entry["imageId"])
        source_art = Path(entry["sourceImage"])
        target = project / "Assets/StreamingAssets/Ygo/Art" / f"{code}.jpg"
        expected_hash = str(entry.get("sourceSha256", ""))
        if source_art.is_file():
            source_hash = sha256(source_art)
            entry["sourceSha256"] = source_hash
            if target.is_file() and source_hash != sha256(target):
                target_hash = sha256(target)
                if code in published_visual_codes:
                    entry["runtimeArtworkSha256"] = target_hash
                    entry["artworkResolution"] = "preserved_published"
                else:
                    art_conflicts.append({
                        "code": code,
                        "source": str(source_art),
                        "target": str(target),
                    })
            elif not target.is_file():
                art_operations.append((source_art, target, code, "selected"))
        elif not target.is_file():
            raise FileNotFoundError(source_art)
        elif expected_hash and expected_hash != sha256(target):
            art_conflicts.append({
                "code": code,
                "source": str(source_art),
                "target": str(target),
            })
    for dependency in dependencies:
        if not dependency["imageAvailable"]:
            continue
        code = int(dependency["officialCode"])
        source_art = Path(dependency["sourceImage"])
        target = project / "Assets/StreamingAssets/Ygo/Art" / f"{code}.jpg"
        expected_hash = str(dependency.get("sourceSha256", ""))
        if source_art.is_file():
            source_hash = sha256(source_art)
            dependency["sourceSha256"] = source_hash
            if target.is_file() and source_hash != sha256(target):
                target_hash = sha256(target)
                if code in published_visual_codes:
                    dependency["runtimeArtworkSha256"] = target_hash
                    dependency["artworkResolution"] = "preserved_published"
                else:
                    art_conflicts.append({
                        "code": code,
                        "source": str(source_art),
                        "target": str(target),
                    })
            elif not target.is_file():
                art_operations.append((source_art, target, code, "dependency"))
        elif not target.is_file():
            raise FileNotFoundError(source_art)
        elif expected_hash and expected_hash != sha256(target):
            art_conflicts.append({
                "code": code,
                "source": str(source_art),
                "target": str(target),
            })
    if art_conflicts:
        raise ValueError("Artwork conflicts: " + json.dumps(art_conflicts))

    script_operations: list[tuple[Path, Path, int]] = []
    for code, resolution in resolutions.items():
        source_script = resolution.get("source")
        if not isinstance(source_script, Path):
            continue
        if resolution["runtimeFolder"] == "official":
            target = project / "Assets/StreamingAssets/Ygo/Scripts/official" / f"c{code}.lua"
        else:
            target = project / "Assets/StreamingAssets/Ygo/CustomScripts" / f"c{code}.lua"
        normalized_source = normalized_lua_bytes(source_script)
        if target.is_file():
            target_bytes = target.read_bytes()
            if target_bytes != normalized_source:
                # A byte-identical source copy can be safely normalized. Existing
                # custom overrides with other semantics remain authoritative.
                if target_bytes == source_script.read_bytes():
                    script_operations.append((source_script, target, code))
                elif "CustomScripts" not in target.as_posix():
                    raise ValueError(f"Runtime script conflict for {code}: {target}")
            continue
        script_operations.append((source_script, target, code))

    core_rows = read_csv(project / "Documentation/CoreCardCatalog.csv")
    core_by_code = {int(row["official_code"]): dict(row) for row in core_rows}
    for entry in ready_entries:
        code = int(entry["imageId"])
        record = database[code]
        resolution = resolutions[code]
        core_by_code.setdefault(code, {
            "official_code": f"{code:08d}",
            "script_code": str(int(resolution.get("scriptCode", 0) or 0))
                if resolution.get("scriptCode") else "",
            "origin": args.batch_id,
            "card_type": str(record["type"]),
        })
    for dependency in dependencies:
        code = int(dependency["officialCode"])
        record = database[code]
        core_by_code.setdefault(code, {
            "official_code": f"{code:08d}",
            "script_code": "",
            "origin": f"runtime_dependency:{args.batch_id}",
            "card_type": str(record["type"]),
        })
    output_core = [core_by_code[code] for code in sorted(core_by_code)]

    localization_path = project / "Documentation/CardTextPtBr.json"
    localization = json.loads(localization_path.read_text(encoding="utf-8"))
    localized_by_code = {
        int(item["code"]): dict(item) for item in localization.get("cards", [])
    }
    english_fallbacks = set(int(value) for value in localization.get("missingCodes", []))
    for entry in ready_entries:
        code = int(entry["imageId"])
        owner = en_by_id.get(code) or owner_by_image.get(code)
        pt = (translation_by_code.get(code) or pt_by_id.get(int(owner["id"]))) if owner else None
        record = database[code]
        localized_by_code[code] = {
            "code": code,
            "name": str(pt.get("name", "")) if pt else record["name"],
            "description": str(pt.get("description", pt.get("desc", ""))) if pt else record["description"],
            "strings": list(pt.get("strings", [])) if pt else [],
        }
        if pt:
            english_fallbacks.discard(code)
        else:
            english_fallbacks.add(code)
    for dependency in dependencies:
        code = int(dependency["officialCode"])
        owner = en_by_id.get(code) or owner_by_image.get(code)
        pt = (translation_by_code.get(code) or pt_by_id.get(int(owner["id"]))) if owner else None
        record = database[code]
        localized_by_code.setdefault(code, {
            "code": code,
            "name": str(pt.get("name", "")) if pt else record["name"],
            "description": str(pt.get("description", pt.get("desc", ""))) if pt else record["description"],
            "strings": list(pt.get("strings", [])) if pt else [],
        })
        if not pt:
            english_fallbacks.add(code)
    localization["generatedUtc"] = utc_now()
    localization["cards"] = [
        localized_by_code[code] for code in sorted(localized_by_code)
    ]
    localization["missingCodes"] = sorted(english_fallbacks)

    documented = read_csv(project / "Documentation/CardCatalog.csv")
    docs_by_code = {int(row["official_code"]): dict(row) for row in documented}
    visual_path = project / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json"
    visual_payload = json.loads(visual_path.read_text(encoding="utf-8"))
    visuals_by_code = {
        int(item["officialCode"]): dict(item)
        for item in visual_payload.get("cards", [])
    }

    def publish_visual(
        code: int,
        record: dict,
        owner: dict | None,
        localized_name: str,
        origin: str,
        source_art: str,
        script_status: str,
        script_path: str,
    ) -> None:
        card_complexity = complexity(record["type"], record["description"])
        style = frame_style(record["type"], str(owner.get("frameType", "")) if owner else "")
        row = docs_by_code.get(code, {})
        row.update({
            "official_code": f"{code:08d}",
            "name": localized_name or record["name"],
            "source": "official",
            "type": type_name(record["type"]),
            "deck_origin": origin,
            "complexity": card_complexity,
            "vertical_slice_role": row.get("vertical_slice_role", ""),
            "script_path": script_path,
            "script_found": script_status,
            "database_found": "true",
            "source_art_path": source_art,
            "art_asset": f"Assets/StreamingAssets/Ygo/Art/{code}.jpg",
            "image_available": "true",
            "test_status": row.get("test_status", "pending"),
            "notes": row.get("notes") or f"Imported from {origin} with streaming artwork.",
            "database_name": record["name"],
            "database_alias": str(record["alias"]) if record["alias"] else "",
        })
        docs_by_code[code] = row
        visual = visuals_by_code.get(code, {})
        visual.update({
            "officialCode": code,
            "artFile": f"{code}.jpg",
            "frameStyle": style,
            "summonVfx": "none" if style in {"spell", "trap", "token"} else (
                "extra_summon" if style in {
                    "fusion", "synchro", "xyz", "link", "ritual",
                    "effect_pendulum", "normal_pendulum",
                } else "normal_summon"
            ),
            "activationSfx": "arcane_activation" if style in {"spell", "trap"}
                else "arcane_summon",
            "riskLevel": risk_for(record["type"], card_complexity),
            "scriptStatus": script_status,
            "scriptFile": Path(script_path).name if script_path else "",
            "presentationTags": [
                type_name(record["type"]),
                card_complexity,
                origin,
                "streaming_art",
            ],
        })
        visuals_by_code[code] = visual

    for entry in ready_entries:
        code = int(entry["imageId"])
        owner = en_by_id.get(code) or owner_by_image.get(code)
        resolution = resolutions[code]
        runtime_folder = str(resolution.get("runtimeFolder", ""))
        script_path = (
            f"{runtime_folder}/c{code}.lua" if runtime_folder else ""
        )
        publish_visual(
            code,
            database[code],
            owner,
            str(entry.get("localizedName", "")),
            args.batch_id,
            str(entry["sourceImage"]),
            str(resolution["status"]),
            script_path,
        )
    for dependency in dependencies:
        code = int(dependency["officialCode"])
        if not dependency["imageAvailable"]:
            continue
        owner = en_by_id.get(code) or owner_by_image.get(code)
        publish_visual(
            code,
            database[code],
            owner,
            str(dependency["localizedName"]),
            f"runtime_dependency:{args.batch_id}",
            str(dependency["sourceImage"]),
            "not_required_token",
            "",
        )

    catalog_asset = (
        project / "Assets/Cards/CardCatalog.asset"
    ).read_text(encoding="utf-8-sig", errors="replace")
    catalog_codes = {
        int(value)
        for value in re.findall(
            r"(?m)^\s*officialCardId:\s*[\"']?(\d+)[\"']?\s*$",
            catalog_asset,
        )
    }
    reconciled_catalog: list[int] = []
    for code in sorted(catalog_codes - set(visuals_by_code)):
        record = database.get(code)
        owner = en_by_id.get(code) or owner_by_image.get(code)
        source_art = source / "images" / f"{code}.jpg"
        if record is None or not source_art.is_file():
            continue
        target = project / "Assets/StreamingAssets/Ygo/Art" / f"{code}.jpg"
        if not target.is_file():
            art_operations.append((source_art, target, code, "reconciliation"))
        elif sha256(source_art) != sha256(target):
            raise ValueError(f"Artwork conflict while reconciling {code}")
        pt = pt_by_id.get(int(owner["id"])) if owner else None
        resolution = script_resolution(
            project,
            code,
            int(record["alias"]),
            int(record["type"]),
        )
        runtime_folder = str(resolution.get("runtimeFolder", ""))
        publish_visual(
            code,
            record,
            owner,
            str(pt.get("name", "")) if pt else record["name"],
            "published_catalog_reconciliation",
            str(source_art),
            str(resolution["status"]),
            f"{runtime_folder}/c{code}.lua" if runtime_folder else "",
        )
        reconciled_catalog.append(code)
    manifest["reconciledCatalogEntries"] = reconciled_catalog

    output_docs = [docs_by_code[code] for code in sorted(docs_by_code)]
    docs_content = csv_bytes(output_docs, VISUAL_FIELDS)
    output_visuals = [visuals_by_code[code] for code in sorted(visuals_by_code)]
    output_visual_payload = {
        "schemaVersion": 1,
        "count": len(output_visuals),
        "catalogSha256": hashlib.sha256(docs_content).hexdigest().upper(),
        "cards": output_visuals,
    }

    manifest["summary"] = {
        "requested": len(manifest["entries"]),
        "ready": sum(entry["status"] == "ready" for entry in manifest["entries"]),
        "runtimeTokens": sum(entry["status"] == "runtime_token" for entry in manifest["entries"]),
        "deferred": len(deferred_entries),
        "masterDuelRarity": sum(entry.get("rarityStatus") == "master_duel" for entry in manifest["entries"]),
        "rarityUnavailable": sum(entry.get("rarityStatus") == "unavailable" for entry in manifest["entries"]),
        "portuguese": sum(entry.get("translationStatus") == "pt-BR" for entry in manifest["entries"]),
        "englishFallback": sum(entry.get("translationStatus") == "english_fallback" for entry in manifest["entries"]),
        "tokenDependencies": len(dependencies),
        "newArtworkFiles": len(art_operations),
        "newScriptFiles": len(script_operations),
        "coreCardsAfter": len(output_core),
        "visualCardsAfter": len(output_visuals),
    }

    return {
        "project": project,
        "source": source,
        "manifestPath": manifest_path,
        "manifest": manifest,
        "readyEntries": ready_entries,
        "deferredEntries": deferred_entries,
        "artOperations": art_operations,
        "scriptOperations": script_operations,
        "coreRows": output_core,
        "coreContent": csv_bytes(output_core, CORE_FIELDS),
        "localization": localization,
        "docsContent": docs_content,
        "visualPayload": output_visual_payload,
    }


def report(plan: dict, applied: bool) -> str:
    manifest = plan["manifest"]
    summary = manifest["summary"]
    deferred = plan["deferredEntries"]
    lines = [
        "# Importação numérica allcards — " + manifest["batchId"], "",
        f"Gerado em UTC: `{utc_now()}`.", "",
        "## Intervalo", "",
        f"- Posição inicial: {manifest['startIndex']}.",
        f"- Imagens solicitadas: {manifest['requestedImageCount']}.",
        f"- Primeiro ID: `{manifest['firstImageId']}`.",
        f"- Último ID: `{manifest['lastImageId']}`.",
        f"- Operação aplicada: {'sim' if applied else 'não (dry-run)'}.", "",
        "## Resultado", "",
        "| Item | Total |", "|---|---:|",
        f"| Cartas prontas | {summary['ready']} |",
        f"| Fichas do lote | {summary['runtimeTokens']} |",
        f"| Entradas adiadas | {summary['deferred']} |",
        f"| Com raridade Master Duel | {summary['masterDuelRarity']} |",
        f"| Sem raridade Master Duel | {summary['rarityUnavailable']} |",
        f"| Texto pt-BR | {summary['portuguese']} |",
        f"| Fallback em inglês | {summary['englishFallback']} |",
        f"| Dependências de ficha | {summary['tokenDependencies']} |",
        f"| Catálogo Core após importação | {summary['coreCardsAfter']} |",
        f"| Catálogo visual após importação | {summary['visualCardsAfter']} |", "",
        "## Adiadas", "",
    ]
    if deferred:
        for entry in deferred:
            lines.append(
                f"- `{int(entry['imageId']):08d}` — {entry.get('englishName') or 'sem nome'}: "
                f"`{entry.get('status')}`."
            )
    else:
        lines.append("Nenhuma.")
    lines.extend([
        "", "## Escopo econômico", "",
        "Este importador não modifica pacotes, decks estruturais ou produtos já "
        "publicados. A geração econômica incremental, quando solicitada, é registrada "
        "em um relatório de pacotes separado. As raridades oficiais disponíveis "
        "alimentam catálogo, coleção e crafting.", "",
        "## Origem e continuidade", "",
        "O manifesto JSON é a fonte permanente da seleção. A continuação não depende "
        "de as imagens processadas permanecerem no diretório de download.", "",
    ])
    return "\n".join(lines)


def validate_compilation(plan: dict, install: bool) -> tuple[int, int]:
    project: Path = plan["project"]
    with tempfile.TemporaryDirectory(prefix="arcane-allcards-") as folder:
        temporary = Path(folder)
        catalog = temporary / "CoreCardCatalog.csv"
        localization = temporary / "CardTextPtBr.json"
        output = temporary / "compiled"
        catalog.write_bytes(plan["coreContent"])
        localization.write_text(
            json.dumps(plan["localization"], ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        command = [
            sys.executable,
            str(project / "Tools/CardDbCompiler/compile_cards.py"),
            "--catalog", str(catalog),
            "--database", str(project / "ThirdParty/BabelCDB/cards.cdb"),
            "--output", str(output),
            "--minimum-count", str(len(plan["coreRows"])),
            "--localization", str(localization),
            "--custom-cards", str(project / "Documentation/CustomCards.json"),
        ]
        process = subprocess.run(
            command,
            cwd=project,
            check=False,
            capture_output=True,
            text=True,
        )
        if process.returncode != 0:
            raise RuntimeError(
                "Card compiler failed with exit code "
                f"{process.returncode}:\n{process.stdout}\n{process.stderr}"
            )
        if "ARCANE_CARD_DB_OK" not in process.stdout:
            raise RuntimeError("Card compiler did not report success")
        cards = output / "cards.bin"
        texts = output / "card-texts.json"
        cards_bytes = cards.read_bytes()
        texts_bytes = texts.read_bytes()
        text_count = int(json.loads(texts_bytes)["count"])
    if install:
        atomic_write(
            project / "Assets/StreamingAssets/Ygo/Data/cards.bin",
            cards_bytes,
        )
        atomic_write(
            project / "Assets/StreamingAssets/Ygo/Data/card-texts.json",
            texts_bytes,
        )
    return len(cards_bytes), text_count


def apply_plan(plan: dict) -> None:
    project: Path = plan["project"]
    for source, target, code, kind in plan["artOperations"]:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        meta = Path(str(target) + ".meta")
        if not meta.exists():
            atomic_write(meta, deterministic_meta(f"art/{kind}/{code}"))
    for source, target, code in plan["scriptOperations"]:
        target.parent.mkdir(parents=True, exist_ok=True)
        atomic_write(target, normalized_lua_bytes(source))
        meta = Path(str(target) + ".meta")
        if not meta.exists():
            atomic_write(meta, deterministic_meta(f"script/{code}"))

    atomic_write(project / "Documentation/CoreCardCatalog.csv", plan["coreContent"])
    atomic_write(
        project / "Documentation/CardTextPtBr.json",
        (json.dumps(plan["localization"], ensure_ascii=False, indent=2) + "\n")
        .encode("utf-8"),
    )
    atomic_write(project / "Documentation/CardCatalog.csv", plan["docsContent"])
    atomic_write(
        project / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json",
        (json.dumps(plan["visualPayload"], ensure_ascii=False, indent=2) + "\n")
        .encode("utf-8"),
    )
    validate_compilation(plan, True)
    plan["manifest"]["appliedUtc"] = utc_now()
    atomic_write(
        plan["manifestPath"],
        (json.dumps(plan["manifest"], ensure_ascii=False, indent=2) + "\n")
        .encode("utf-8"),
    )
    report_path = plan["manifestPath"].with_suffix(".md")
    atomic_write(report_path, report(plan, True).encode("utf-8"))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--batch-id", default="allcards-numeric-0001-2500")
    parser.add_argument("--start-index", type=int, default=1)
    parser.add_argument("--count", type=int, default=2500)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=Path("Documentation/CardImports/AllCardsBatch001.json"),
    )
    parser.add_argument(
        "--translation-overlay",
        type=Path,
        help="Optional schemaVersion 1 pt-BR overlay generated for this batch.",
    )
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    if args.start_index < 1 or args.count < 1:
        raise ValueError("start-index and count must be positive")
    plan = build_plan(args)
    if args.apply:
        apply_plan(plan)
    else:
        validate_compilation(plan, False)
    summary = plan["manifest"]["summary"]
    print(
        "ARCANE_ALLCARDS_BATCH_OK "
        f"applied={str(args.apply).lower()} "
        f"requested={summary['requested']} "
        f"ready={summary['ready']} tokens={summary['runtimeTokens']} "
        f"deferred={summary['deferred']} "
        f"first={plan['manifest']['firstImageId']} "
        f"last={plan['manifest']['lastImageId']} "
        f"core={summary['coreCardsAfter']} visuals={summary['visualCardsAfter']}"
    )


if __name__ == "__main__":
    main()
