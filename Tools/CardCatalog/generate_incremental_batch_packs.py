#!/usr/bin/env python3
"""Append immutable shop packs for uncovered collectible cards in one batch.

Existing published packs are never rewritten.  New cards are grouped by their
YGOPRODeck archetype when available, split into the commercial 40-85 card
range, and appended with deterministic IDs, previews, and content hashes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from collections import Counter
from pathlib import Path


MINIMUM = 40
MAXIMUM = 85
PREFERRED = 82
PRICE = 25


def normalize(value: object) -> str:
    text = str(value or "").strip()
    if not text.isdigit():
        return ""
    return str(int(text))


def content_hash(pack_id: str, card_ids: list[str]) -> str:
    payload = f"{pack_id}|{PRICE}|{','.join(card_ids)}"
    return hashlib.sha256(payload.encode("utf-8")).hexdigest().upper()


def load_cards(path: Path) -> list[dict]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(payload, dict):
        return list(payload.get("data", payload.get("cards", [])))
    return list(payload)


def pack_sizes(total: int) -> list[int]:
    if total < MINIMUM:
        return []
    count = math.ceil(total / PREFERRED)
    while count > 1 and total // count < MINIMUM:
        count -= 1
    while math.ceil(total / count) > MAXIMUM:
        count += 1
    base = total // count
    larger = total - base * count
    sizes = [base + (1 if index < larger else 0) for index in range(count)]
    if min(sizes) < MINIMUM or max(sizes) > MAXIMUM:
        raise ValueError(f"Cannot partition {total} cards into 40-85 card packs")
    return sizes


def theme_for(entry: dict, owner: dict | None) -> str:
    archetype = str((owner or {}).get("archetype", "")).strip()
    if archetype:
        return archetype
    frame = str(entry.get("frameType", "")).strip().casefold()
    return {
        "effect": "Monstros de Efeito",
        "normal": "Monstros Normais",
        "spell": "Magias",
        "trap": "Armadilhas",
        "fusion": "Fusão",
        "synchro": "Sincro",
        "xyz": "Xyz",
        "link": "Link",
        "ritual": "Ritual",
        "effect_pendulum": "Pêndulo",
        "normal_pendulum": "Pêndulo",
        "fusion_pendulum": "Pêndulo de Fusão",
        "synchro_pendulum": "Pêndulo Sincro",
        "xyz_pendulum": "Pêndulo Xyz",
        "ritual_pendulum": "Pêndulo Ritual",
    }.get(frame, "Cartas")


def unique_pack_id(existing: set[str], batch_number: int, sequence: int) -> str:
    candidate = f"expansion-pack-b{batch_number:03d}-{sequence:03d}"
    if candidate in existing:
        raise ValueError(f"Pack ID already exists: {candidate}")
    return candidate


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--batch-number", type=int, required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    root = args.project_root.resolve()
    manifest_path = root / args.manifest
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    source_root = Path(manifest["sourceRoot"])
    cards_en = load_cards(source_root / "metadata/cards-en.json")
    owner_by_id = {int(card["id"]): card for card in cards_en}
    owner_by_image: dict[int, dict] = {}
    for owner in cards_en:
        for image in owner.get("card_images", []):
            owner_by_image[int(image["id"])] = owner

    catalog_path = root / "Assets/Resources/Shop/PackCatalog.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    if int(catalog.get("version", 0)) != 2:
        raise ValueError("PackCatalog must use version 2")
    packs = list(catalog.get("packs", []))
    existing_pack_ids = {str(pack.get("packId", "")) for pack in packs}
    if len(existing_pack_ids) != len(packs):
        raise ValueError("PackCatalog contains duplicate pack IDs")
    covered: set[str] = set()
    for pack in packs:
        pack_id = str(pack.get("packId", ""))
        card_ids = [normalize(value) for value in pack.get("cardIds", [])]
        previews = [normalize(value) for value in pack.get("previewCardIds", [])]
        if not pack_id or not all(card_ids):
            raise ValueError("Published pack has an invalid ID or card ID")
        if len(card_ids) < MINIMUM or len(card_ids) > MAXIMUM:
            raise ValueError(f"Published pack has an invalid size: {pack_id}")
        if len(card_ids) != len(set(card_ids)):
            raise ValueError(f"Published pack contains duplicate cards: {pack_id}")
        if len(previews) != 3 or any(value not in card_ids for value in previews):
            raise ValueError(f"Published pack has invalid previews: {pack_id}")
        if str(pack.get("contentHash", "")).upper() != content_hash(
            pack_id, card_ids
        ):
            raise ValueError(f"Published pack has an invalid content hash: {pack_id}")
        if not pack.get("published", True) or not pack.get(
            "countsForAutoCoverage", True
        ):
            continue
        for card_id in card_ids:
            if card_id in covered:
                raise ValueError(f"Existing pack coverage is duplicated for {card_id}")
            covered.add(card_id)

    candidates: list[dict] = []
    eligible_by_id: dict[str, dict] = {}
    seen: set[str] = set()
    for entry in manifest.get("entries", []):
        if not entry.get("catalogEligible") or not entry.get("collectible"):
            continue
        card_id = normalize(entry.get("imageId"))
        if not card_id or card_id in seen:
            continue
        seen.add(card_id)
        owner_id = int(entry.get("ownerCardId", 0) or 0)
        owner = owner_by_id.get(owner_id) or owner_by_image.get(int(card_id))
        item = dict(entry)
        item["cardId"] = card_id
        item["theme"] = theme_for(entry, owner)
        eligible_by_id[card_id] = item
        if card_id in covered:
            continue
        candidates.append(item)

    candidates.sort(key=lambda item: (
        item["theme"].casefold(),
        str(item.get("englishName", "")).casefold(),
        int(item["cardId"]),
    ))
    sizes = pack_sizes(len(candidates))
    if candidates and not sizes:
        raise ValueError(
            f"Only {len(candidates)} uncovered cards; at least {MINIMUM} are required"
        )

    created: list[dict] = []
    offset = 0
    for index, size in enumerate(sizes, start=1):
        chunk = candidates[offset:offset + size]
        offset += size
        pack_id = unique_pack_id(
            existing_pack_ids, args.batch_number, index
        )
        existing_pack_ids.add(pack_id)
        themes = Counter(item["theme"] for item in chunk)
        labels = [
            theme for theme, _ in sorted(
                themes.items(), key=lambda pair: (-pair[1], pair[0].casefold())
            )[:2]
        ]
        display_theme = " & ".join(labels) if labels else f"Lote {args.batch_number}"
        card_ids = [item["cardId"] for item in chunk]
        previews = [
            item["cardId"]
            for item in sorted(
                chunk,
                key=lambda item: (
                    -int(item.get("rarity", 0) or 0),
                    str(item.get("localizedName", "")).casefold(),
                    int(item["cardId"]),
                ),
            )[:3]
        ]
        created.append({
            "packId": pack_id,
            "displayName": f"Pacote {display_theme}",
            "description": (
                f"Seleção de expansão com {size} cartas do lote "
                f"{args.batch_number:03d}: {display_theme}."
            ),
            "cardIds": card_ids,
            "previewCardIds": previews,
            "priceCoins": PRICE,
            "origin": 1,
            "generationBatchId": str(manifest["batchId"]),
            "generatorVersion": 2,
            "contentLockedAfterPublish": True,
            "contentHash": content_hash(pack_id, card_ids),
            "countsForAutoCoverage": True,
            "published": True,
            "manualVisualOverride": False,
            "needsPreviewReview": False,
        })

    assigned = [card_id for pack in created for card_id in pack["cardIds"]]
    if len(assigned) != len(candidates) or len(set(assigned)) != len(candidates):
        raise AssertionError("New pack coverage is not bijective")
    if any(card_id in covered for card_id in assigned):
        raise AssertionError("New packs overlap published coverage")

    current_packs = packs + created
    batch_packs = [
        pack for pack in current_packs
        if pack.get("generationBatchId") == manifest["batchId"]
    ]
    if args.apply:
        for pack in batch_packs:
            themes = Counter(
                eligible_by_id[normalize(card_id)]["theme"]
                for card_id in pack["cardIds"]
                if normalize(card_id) in eligible_by_id
            )
            labels = [
                theme for theme, _ in sorted(
                    themes.items(),
                    key=lambda pair: (-pair[1], pair[0].casefold()),
                )[:2]
            ]
            display_theme = " & ".join(labels) if labels else (
                f"Lote {args.batch_number}"
            )
            pack["displayName"] = f"Pacote {display_theme}"
            pack["description"] = (
                f"Seleção de expansão com {len(pack['cardIds'])} cartas do lote "
                f"{args.batch_number:03d}: {display_theme}."
            )

        catalog["packs"] = current_packs
        temporary = catalog_path.with_suffix(".json.tmp")
        temporary.write_text(
            json.dumps(catalog, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )
        json.loads(temporary.read_text(encoding="utf-8"))
        os.replace(temporary, catalog_path)

        report_path = manifest_path.with_name(manifest_path.stem + "Packs.md")
        report_lines = [
            f"# Pacotes incrementais — {manifest['batchId']}",
            "",
            "- Pacotes preservados anteriormente: `" +
            str(len(current_packs) - len(batch_packs)) + "`.",
            f"- Novos pacotes deste lote: `{len(batch_packs)}`.",
            "- Cartas cobertas neste lote: `" +
            str(sum(len(pack["cardIds"]) for pack in batch_packs)) + "`.",
            f"- Faixa comercial: `{MINIMUM}-{MAXIMUM}` cartas por pacote.",
            f"- Preço: `{PRICE}` moedas.",
            "",
            "## Pacotes",
            "",
        ]
        for pack in batch_packs:
            report_lines.append(
                f"- `{pack['packId']}` — {pack['displayName']}: "
                f"{len(pack['cardIds'])} cartas, hash `{pack['contentHash']}`."
            )
        report_path.write_text(
            "\n".join(report_lines) + "\n",
            encoding="utf-8",
            newline="\n",
        )

    print(
        "ARCANE_BATCH_PACKS_OK "
        f"applied={str(args.apply).lower()} candidates={len(candidates)} "
        f"created={len(created)} first={created[0]['packId'] if created else '-'} "
        f"last={created[-1]['packId'] if created else '-'} "
        f"sizes={','.join(str(size) for size in sizes)}"
    )


if __name__ == "__main__":
    main()
