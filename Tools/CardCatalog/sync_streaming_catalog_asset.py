#!/usr/bin/env python3
"""Synchronize streaming-art entries into Unity's CardCatalog asset.

This is the deterministic, license-independent counterpart to the Unity editor
synchronizer.  It only appends missing entries and verifies that the resulting
official-card ID set exactly matches card-visuals.json.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sqlite3
from pathlib import Path


TYPE_MONSTER = 0x1
TYPE_SPELL = 0x2
TYPE_TRAP = 0x4
TYPE_NORMAL = 0x10
TYPE_FUSION = 0x40
TYPE_RITUAL = 0x80
TYPE_SYNCHRO = 0x2000
TYPE_TOKEN = 0x4000
TYPE_FLIP = 0x200000
TYPE_XYZ = 0x800000
TYPE_PENDULUM = 0x1000000
TYPE_LINK = 0x4000000

ART_VARIANTS = {"Auto": 0, "Base": 1, "Alt": 2, "Alt1": 3, "Alt2": 4}
RACES = {
    1: "Guerreiro",
    2: "Mago",
    4: "Fada",
    8: "Demônio",
    16: "Zumbi",
    32: "Máquina",
    64: "Aqua",
    128: "Piro",
    256: "Rocha",
    512: "Besta Alada",
    1024: "Planta",
    2048: "Inseto",
    4096: "Trovão",
    8192: "Dragão",
    16384: "Besta",
    32768: "Besta-Guerreira",
    65536: "Dinossauro",
    131072: "Peixe",
    262144: "Serpente Marinha",
    524288: "Réptil",
    1048576: "Psíquico",
    2097152: "Besta Divina",
    4194304: "Deus Criador",
    8388608: "Wyrm",
    16777216: "Ciberso",
    33554432: "Ilusão",
}


def quoted(value: str) -> str:
    return json.dumps(value or "", ensure_ascii=True)


def stable_id(code: int) -> str:
    return hashlib.sha256(f"streaming-card:{code:08d}".encode("ascii")).hexdigest()[:32]


def category(card_type: int) -> int:
    if card_type & TYPE_MONSTER:
        return 1
    if card_type & TYPE_SPELL:
        return 2
    if card_type & TYPE_TRAP:
        return 3
    return 0


def frame(card_type: int) -> int:
    if card_type & TYPE_TOKEN:
        return 10
    if card_type & TYPE_LINK:
        return 8
    if card_type & TYPE_XYZ:
        return 7
    if card_type & TYPE_SYNCHRO:
        return 6
    if card_type & TYPE_FUSION:
        return 5
    if card_type & TYPE_RITUAL:
        return 4
    if card_type & TYPE_PENDULUM:
        return 9
    if card_type & TYPE_NORMAL:
        return 2
    return 3


def type_name(card_type: int, frame_kind: int) -> str:
    if card_type & TYPE_SPELL:
        return "Carta de Magia"
    if card_type & TYPE_TRAP:
        return "Carta de Armadilha"
    if card_type & TYPE_TOKEN:
        return "Ficha"
    if card_type & TYPE_FLIP:
        return "Monstro de Efeito de Virar"
    return {
        2: "Monstro Normal",
        4: "Monstro de Ritual",
        5: "Monstro de Fusão",
        6: "Monstro Sincro",
        7: "Monstro Xyz",
        8: "Monstro Link",
        9: "Monstro Pêndulo",
    }.get(frame_kind, "Monstro de Efeito")


def attribute(value: int) -> int:
    # CardAttribute serialized enum: None, Dark, Earth, Fire, Light, Water,
    # Wind, Divine.
    if value & 0x20:
        return 1
    if value & 0x10:
        return 4
    if value & 0x08:
        return 6
    if value & 0x04:
        return 3
    if value & 0x02:
        return 5
    if value & 0x01:
        return 2
    if value & 0x40:
        return 7
    return 0


def entry_yaml(
    code: int,
    data: sqlite3.Row,
    text: dict,
    rarity: int,
    variant: int,
) -> str:
    card_type = int(data["type"])
    card_category = category(card_type)
    frame_kind = frame(card_type)
    packed_level = int(data["level"]) & 0xFFFFFFFF
    level = packed_level & 0xFF if card_category == 1 else 0
    attack = int(data["atk"]) if card_category == 1 else -1
    defense = (0 if card_type & TYPE_LINK else int(data["def"])) if card_category == 1 else -1
    is_token = frame_kind == 10
    craft_blocked = rarity == 0 or is_token
    english_name = str(text.get("englishName", ""))
    display_name = str(text.get("name", "")) or english_name or f"Carta {code:08d}"
    effect = str(text.get("description", ""))
    rarity_source = english_name if rarity else ""
    race_name = RACES.get(int(data["race"]), "Monstro") if card_category == 1 else ""
    review = (
        "Metadados de apresentação sincronizados do lote numérico AllCards "
        "e do catálogo compilado do Core."
    )
    lines = [
        f"  - stableId: {stable_id(code)}",
        "    artwork: {fileID: 0}",
        "    runtimeArtworkAvailable: 1",
        f"    displayName: {quoted(display_name)}",
        f"    englishName: {quoted(english_name)}",
        f"    rarityVariant: {variant}",
        f"    rarity: {rarity}",
        f"    raritySourceName: {quoted(rarity_source)}",
        f"    craftingBlocked: {1 if craft_blocked else 0}",
        f"    dismantlingBlocked: {1 if craft_blocked else 0}",
        f"    category: {card_category}",
        f"    monsterFrame: {frame_kind}",
        f"    officialCardId: {code:08d}",
        f"    typeName: {quoted(type_name(card_type, frame_kind))}",
        f"    raceName: {quoted(race_name)}",
        f"    attribute: {attribute(int(data['attribute'])) if card_category == 1 else 0}",
        f"    level: {level}",
        f"    attack: {attack}",
        f"    defense: {defense}",
        "    effectId: 0",
        "    officiallyRegistered: 1",
        "    classificationConfidence: 1",
        "    needsManualReview: 0",
        "    manuallyConfirmed: 0",
        f"    effectText: {quoted(effect)}",
        f"    reviewNotes: {quoted(review)}",
    ]
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True, type=Path)
    parser.add_argument("--manifest", default="Documentation/CardImports/AllCardsBatch001.json")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    root = args.project_root.resolve()
    manifest = json.loads((root / args.manifest).read_text(encoding="utf-8"))
    visual = json.loads(
        (root / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json").read_text(encoding="utf-8")
    )
    texts_payload = json.loads(
        (root / "Assets/StreamingAssets/Ygo/Data/card-texts.json").read_text(encoding="utf-8")
    )
    texts = {int(item["code"]): item for item in texts_payload["cards"]}
    visual_codes = {int(item["officialCode"]) for item in visual["cards"]}
    catalog_path = root / "Assets/Cards/CardCatalog.asset"
    original = catalog_path.read_text(encoding="utf-8")
    raw_ids = re.findall(r"^    officialCardId:\s*[\"']?([0-9]+)", original, re.MULTILINE)
    existing_codes = [int(value) for value in raw_ids]
    if len(existing_codes) != len(set(existing_codes)):
        raise ValueError("CardCatalog.asset already contains duplicate official IDs")
    missing_codes = sorted(visual_codes - set(existing_codes))
    extra_codes = sorted(set(existing_codes) - visual_codes)
    if extra_codes:
        raise ValueError(f"Visual catalog is missing existing CardCatalog IDs: {extra_codes[:20]}")

    rarity_by_code = {
        int(item["imageId"]): int(item.get("rarity", 0) or 0)
        for item in manifest["entries"]
        if item.get("catalogEligible")
    }
    variant_by_code = {
        int(item["imageId"]): ART_VARIANTS.get(str(item.get("artVariant", "Auto")), 0)
        for item in manifest["entries"]
        if item.get("catalogEligible")
    }
    connection = sqlite3.connect(root / "ThirdParty/BabelCDB/cards.cdb")
    connection.row_factory = sqlite3.Row
    blocks: list[str] = []
    for code in missing_codes:
        data = connection.execute(
            "SELECT id, alias, type, atk, def, level, race, attribute FROM datas WHERE id = ?",
            (code,),
        ).fetchone()
        if data is None:
            raise ValueError(f"BabelCDB is missing visual card {code:08d}")
        text = texts.get(code)
        if text is None:
            raise ValueError(f"Compiled texts are missing visual card {code:08d}")
        blocks.append(
            entry_yaml(
                code,
                data,
                text,
                rarity_by_code.get(code, 0),
                variant_by_code.get(code, 1),
            )
        )
    connection.close()

    updated = original
    if blocks:
        updated = original.rstrip() + "\n" + "".join(blocks)
    updated, normalized_ids = re.subn(
        r"^    officialCardId:\s*[\"']([0-9]+)[\"']\s*$",
        lambda match: f"    officialCardId: {match.group(1)}",
        updated,
        flags=re.MULTILINE,
    )
    final_ids = [
        int(value)
        for value in re.findall(
            r"^    officialCardId:\s*[\"']?([0-9]+)", updated, re.MULTILINE
        )
    ]
    if len(final_ids) != len(set(final_ids)):
        raise ValueError("Synchronization would create duplicate official IDs")
    if set(final_ids) != visual_codes:
        raise ValueError("Synchronized CardCatalog ID set differs from card-visuals.json")
    if args.apply:
        temporary = catalog_path.with_suffix(catalog_path.suffix + ".tmp")
        temporary.write_text(updated, encoding="utf-8", newline="\n")
        temporary.replace(catalog_path)
    print(
        "ARCANE_ALLCARDS_ASSET_SYNC_OK "
        f"applied={str(args.apply).lower()} existing={len(existing_codes)} "
        f"added={len(missing_codes)} normalized={normalized_ids} final={len(final_ids)}"
    )


if __name__ == "__main__":
    main()
