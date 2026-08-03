"""Generate the checked-in active banlist seed from the supplied specification PDF."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

import pdfplumber


BANLIST_ID = "tcg_eu_2026_05_18"
EFFECTIVE_DATE = "2026-05-18"
EXPECTED_LIMITS = {0: 119, 1: 97, 2: 10}
ROW_PATTERN = re.compile(r"^(.*?)\s+(\d{8})\s+([012])$")


def parse_rows(pdf_path: Path) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    with pdfplumber.open(pdf_path) as document:
        for page_index in range(21, 27):
            text = document.pages[page_index].extract_text() or ""
            for line in text.splitlines():
                match = ROW_PATTERN.match(line.strip())
                if match:
                    rows.append(
                        {
                            "officialName": match.group(1).strip(),
                            "passcode": match.group(2),
                            "maxCopies": int(match.group(3)),
                        }
                    )
    return rows


def source_hash(rows: list[dict[str, object]]) -> str:
    normalized = "\n".join(
        f"{row['passcode']}|{row['maxCopies']}|{row['officialName']}"
        for row in sorted(rows, key=lambda item: str(item["passcode"]))
    )
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: generate_starter_banlist.py <spec.pdf> <output.json>")
        return 2

    pdf_path = Path(sys.argv[1]).resolve()
    output_path = Path(sys.argv[2]).resolve()
    rows = parse_rows(pdf_path)
    actual_limits = {
        limit: sum(row["maxCopies"] == limit for row in rows)
        for limit in EXPECTED_LIMITS
    }
    if actual_limits != EXPECTED_LIMITS:
        raise RuntimeError(
            f"Unexpected banlist counts: {actual_limits}; expected {EXPECTED_LIMITS}"
        )
    if len({row["passcode"] for row in rows}) != len(rows):
        raise RuntimeError("Duplicate passcode in the specification")

    payload = {
        "schemaVersion": 1,
        "id": BANLIST_ID,
        "effectiveDate": EFFECTIVE_DATE,
        "sourceSha256": source_hash(rows),
        "entries": rows,
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(
        f"BANLIST_SEED_OK entries={len(rows)} limits={actual_limits} "
        f"sha256={payload['sourceSha256']} output={output_path}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
