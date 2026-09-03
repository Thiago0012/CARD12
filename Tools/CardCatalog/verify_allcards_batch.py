#!/usr/bin/env python3
"""Verify the installed AllCards batch and its reproducible manifest."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import struct
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True, type=Path)
    parser.add_argument(
        "--manifest",
        default="Documentation/CardImports/AllCardsBatch001.json",
    )
    args = parser.parse_args()
    root = args.project_root.resolve()
    manifest = json.loads((root / args.manifest).read_text(encoding="utf-8"))
    entries = manifest["entries"]
    requested = int(manifest["requestedImageCount"])
    start = int(manifest["startIndex"])
    require(len(entries) == requested,
            "Manifest source position count differs from requestedImageCount")
    require(
        [item["position"] for item in entries] ==
        list(range(start, start + requested)),
        "Positions are not contiguous",
    )
    require(int(entries[0]["imageId"]) == int(manifest["firstImageId"]),
            "First image ID changed")
    require(int(entries[-1]["imageId"]) == int(manifest["lastImageId"]),
            "Last image ID changed")
    eligible = [item for item in entries if item.get("catalogEligible")]
    deferred = [item for item in entries if not item.get("catalogEligible")]
    summary = manifest.get("summary", {})
    expected_eligible = int(summary.get("ready", 0)) + int(
        summary.get("runtimeTokens", 0))
    expected_deferred = int(summary.get("deferred", 0))
    require(len(eligible) == expected_eligible,
            "Installed source image count differs from the manifest summary")
    require(len(deferred) == expected_deferred,
            "Deferred source image count differs from the manifest summary")
    require(len(eligible) + len(deferred) == requested,
            "Eligible and deferred entries do not cover the complete batch")
    require(all(item.get("sourceSha256") for item in entries), "Manifest source hashes are incomplete")

    art_root = root / "Assets/StreamingAssets/Ygo/Art"
    for item in eligible:
        code = int(item["imageId"])
        target = art_root / f"{code}.jpg"
        require(target.is_file(), f"Runtime artwork is missing for {code:08d}")
        expected_runtime_hash = item.get(
            "runtimeArtworkSha256", item["sourceSha256"])
        require(sha256(target) == expected_runtime_hash,
                f"Artwork hash mismatch for {code:08d}")

    visual_payload = json.loads(
        (root / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json").read_text(encoding="utf-8")
    )
    visual_cards = visual_payload["cards"]
    visual_codes = [int(item["officialCode"]) for item in visual_cards]
    require(len(visual_codes) == len(set(visual_codes)), "Visual catalog has duplicate codes")
    for item in visual_cards:
        code = int(item["officialCode"])
        art = art_root / str(item["artFile"])
        require(art.is_file(), f"Visual artwork is missing for {code:08d}")
        require(Path(str(art) + ".meta").is_file(), f"Artwork meta is missing for {code:08d}")
        script_file = str(item.get("scriptFile", "") or "")
        if script_file:
            official = root / "Assets/StreamingAssets/Ygo/Scripts/official" / script_file
            custom = root / "Assets/StreamingAssets/Ygo/CustomScripts" / script_file
            require(official.is_file() or custom.is_file(), f"Runtime script is missing for {code:08d}")

    asset = (root / "Assets/Cards/CardCatalog.asset").read_text(encoding="utf-8")
    catalog_codes = [
        int(value)
        for value in re.findall(r"^    officialCardId:\s*[\"']?([0-9]+)", asset, re.MULTILINE)
    ]
    require(len(catalog_codes) == len(set(catalog_codes)), "CardCatalog has duplicate official IDs")
    require(set(catalog_codes) == set(visual_codes), "CardCatalog and visual catalog differ")

    with (root / "Documentation/CoreCardCatalog.csv").open(
        "r", encoding="utf-8-sig", newline=""
    ) as stream:
        core_codes = [int(row["official_code"]) for row in csv.DictReader(stream)]
    require(len(core_codes) == len(set(core_codes)), "Core catalog has duplicate codes")
    texts = json.loads(
        (root / "Assets/StreamingAssets/Ygo/Data/card-texts.json").read_text(encoding="utf-8")
    )
    text_codes = {int(item["code"]) for item in texts["cards"]}
    require(set(core_codes).issubset(text_codes), "Compiled texts omit Core catalog cards")

    binary = (root / "Assets/StreamingAssets/Ygo/Data/cards.bin").read_bytes()
    require(binary[:4] == b"ADCB", "Compiled card database magic is invalid")
    version, binary_count = struct.unpack_from("<II", binary, 4)
    require(version == 1, "Compiled card database version is invalid")
    require(binary_count == int(texts["count"]), "Binary and text database counts differ")

    print(
        "ARCANE_ALLCARDS_VERIFY_OK "
        f"requested={len(entries)} installed={len(eligible)} deferred={len(deferred)} "
        f"catalog={len(catalog_codes)} core={len(core_codes)} compiled={binary_count}"
    )


if __name__ == "__main__":
    main()
