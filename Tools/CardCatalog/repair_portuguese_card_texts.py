#!/usr/bin/env python3
"""Repair incomplete Portuguese card texts with the official Konami database.

YGOPRODeck is the bulk localization source used by the project, but a small
subset of its Portuguese records can still contain an English section.  This
tool resolves the public Konami card id, reads the official Portuguese card
page, and persists the corrected text as a manual override so later catalog
imports cannot regress it.
"""

from __future__ import annotations

import argparse
import html
import json
import re
import time
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path


YGOPRODECK_URL = "https://db.ygoprodeck.com/api/v7/cardinfo.php"
KONAMI_URL = "https://www.db.yugioh-card.com/yugiohdb/card_search.action"
USER_AGENT = "ArcaneDuelLocalizationRepair/1.0"
ENGLISH_MARKERS = (
    " once per ", " you can ", " your opponent ", " this card ",
    " this turn ", " target 1 ", " special summon", " normal summon",
    " from your ", " to your hand", " on the field",
    " in your graveyard", " destroy that", " banish that",
    " when this ", " if this ", " during your ",
)
ENGLISH_VOCABULARY = (
    " the ", " this ", " that ", " card ", " you ", " your ",
    " opponent ", " monster ", " spell ", " trap ", " deck ",
    " hand ", " field ", " summon ", " turn ", " target ",
    " destroy ", " banish ", " once ", " when ", " during ",
    " cannot ",
)


def looks_english(value: str) -> bool:
    normalized = " " + value.lower().replace("\r", " ").replace("\n", " ") + " "
    if any(marker in normalized for marker in ENGLISH_MARKERS):
        return True
    return sum(word in normalized for word in ENGLISH_VOCABULARY) >= 4


def request_json(url: str, attempts: int = 3) -> dict:
    for attempt in range(attempts):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(request, timeout=45) as response:
                return json.load(response)
        except Exception:
            if attempt + 1 == attempts:
                raise
            time.sleep(1.0 + attempt)
    raise RuntimeError("unreachable")


def request_text(url: str, attempts: int = 3) -> str:
    for attempt in range(attempts):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(request, timeout=45) as response:
                return response.read().decode("utf-8", errors="replace")
        except Exception:
            if attempt + 1 == attempts:
                raise
            time.sleep(1.0 + attempt)
    raise RuntimeError("unreachable")


def text_from_html(fragment: str) -> str:
    value = re.sub(r"(?i)<br\s*/?>", "\n", fragment)
    value = re.sub(r"(?i)</(?:p|li|div)>", "\n", value)
    value = re.sub(r"<[^>]+>", "", value)
    value = html.unescape(value).replace("\xa0", " ")
    lines = [re.sub(r"[ \t]+", " ", line).strip() for line in value.splitlines()]
    return "\n".join(line for line in lines if line)


def official_text(konami_id: int, is_pendulum: bool) -> tuple[str, str] | None:
    query = urllib.parse.urlencode(
        {"cid": konami_id, "ope": 2, "request_locale": "pt"}
    )
    page = request_text(f"{KONAMI_URL}?{query}")
    title = re.search(
        r'<meta\s+name="title"\s+content="(.*?)\s*\|\s*Detalhes de Card',
        page,
        flags=re.IGNORECASE | re.DOTALL,
    )
    name = text_from_html(title.group(1)) if title else ""
    card_text = re.search(
        r'<div\s+class="CardText">\s*'
        r'<div\s+class="item_box_text">\s*'
        r'<div\s+class="text_title">\s*Texto do Card\s*</div>\s*'
        r'(.*?)\s*</div>',
        page,
        flags=re.IGNORECASE | re.DOTALL,
    )
    monster_or_card_effect = text_from_html(card_text.group(1)) if card_text else ""
    if not monster_or_card_effect:
        return None

    if not is_pendulum:
        return name, monster_or_card_effect

    pendulum = re.search(
        r'<div\s+class="frame\s+pen_effect">.*?'
        r'<div\s+class="item_box_text">\s*(.*?)\s*</div>',
        page,
        flags=re.IGNORECASE | re.DOTALL,
    )
    pendulum_effect = text_from_html(pendulum.group(1)) if pendulum else ""
    if not pendulum_effect:
        return None
    description = (
        "[ Efeito de Pêndulo ]\n"
        f"{pendulum_effect}\n\n"
        "[ Efeito de Monstro ]\n"
        f"{monster_or_card_effect}"
    )
    return name, description


def metadata_by_code(codes: list[int]) -> dict[int, dict]:
    requested = set(codes)
    result: dict[int, dict] = {}
    for offset in range(0, len(codes), 100):
        batch = codes[offset : offset + 100]
        query = urllib.parse.urlencode(
            {
                "misc": "yes",
                "id": ",".join(str(code) for code in batch),
            },
            safe=",",
        )
        payload = request_json(f"{YGOPRODECK_URL}?{query}")
        for card in payload.get("data", []):
            aliases = {int(card["id"])}
            aliases.update(
                int(image["id"])
                for image in card.get("card_images", [])
                if image.get("id") is not None
            )
            for code in aliases & requested:
                result[code] = card
        time.sleep(0.15)
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--manual", required=True, type=Path)
    parser.add_argument("--workers", type=int, default=8)
    args = parser.parse_args()

    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    manual = json.loads(args.manual.read_text(encoding="utf-8"))
    manual_by_code = {
        int(card["code"]): card for card in manual.get("cards", [])
    }
    candidates = {
        int(card["code"]): card
        for card in catalog.get("cards", [])
        if looks_english(str(card.get("description", "")))
    }
    metadata = metadata_by_code(sorted(candidates))
    jobs: dict[int, tuple[int, bool]] = {}
    for code, card in candidates.items():
        source = metadata.get(code, {})
        misc = source.get("misc_info") or []
        misc = misc[0] if isinstance(misc, list) and misc else misc
        konami_id = int((misc or {}).get("konami_id", 0) or 0)
        if konami_id:
            jobs[code] = (
                konami_id,
                "[ Pendulum Effect ]" in str(card.get("description", ""))
                or "[ Efeito de Pêndulo ]" in str(card.get("description", "")),
            )

    repaired: dict[int, dict] = {}
    failures: dict[int, str] = {}
    with ThreadPoolExecutor(max_workers=max(1, args.workers)) as executor:
        pending = {
            executor.submit(official_text, konami_id, is_pendulum): code
            for code, (konami_id, is_pendulum) in jobs.items()
        }
        for future in as_completed(pending):
            code = pending[future]
            try:
                resolved = future.result()
                if resolved is None:
                    failures[code] = "official page did not expose Portuguese text"
                    continue
                official_name, description = resolved
                if looks_english(description):
                    failures[code] = "official text still appears to contain English"
                    continue
                existing = candidates[code]
                source = metadata.get(code, {})
                repaired[code] = {
                    "code": code,
                    "name": official_name
                    or str(source.get("name", "")).strip()
                    or str(existing.get("name", "")).strip(),
                    "description": description,
                    "strings": existing.get("strings", []),
                }
            except Exception as error:
                failures[code] = str(error)

    manual_by_code.update(repaired)
    manual["cards"] = [manual_by_code[code] for code in sorted(manual_by_code)]
    args.manual.write_text(
        json.dumps(manual, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    unresolved = sorted(set(candidates) - set(repaired))
    print(
        "ARCANE_PT_BR_REPAIR_OK "
        f"candidates={len(candidates)} metadata={len(metadata)} "
        f"official={len(jobs)} repaired={len(repaired)} "
        f"unresolved={len(unresolved)}"
    )
    if unresolved:
        print("UNRESOLVED " + ",".join(str(code) for code in unresolved))
    if failures:
        print("FAILURES " + json.dumps(failures, ensure_ascii=True, sort_keys=True))


if __name__ == "__main__":
    main()
