#!/usr/bin/env python3
"""Reconcile published CardCatalog entries with runtime presentation sources.

Preview is the default. --apply updates only generated documentation/runtime
presentation files and copies already-authored art; it never downloads content,
edits Lua, changes cards.bin, or mutates CardCatalog.asset.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import os
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
AUDIT_TOOLS = ROOT / "Tools" / "CardAudit"
sys.path.insert(0, str(AUDIT_TOOLS))
import generate_phase01 as audit  # noqa: E402


CATALOG_FIELDS = [
    "official_code", "name", "source", "type", "deck_origin",
    "complexity", "vertical_slice_role", "script_path", "script_found",
    "database_found", "source_art_path", "art_asset", "image_available",
    "test_status", "notes", "database_name", "database_alias",
]

FRAME_STYLE = {
    1: "unknown", 2: "normal", 3: "effect", 4: "ritual",
    5: "fusion", 6: "synchro", 7: "xyz", 8: "link",
    9: "pendulum", 10: "token",
}


def type_name(card_type: int) -> str:
    if card_type & 0x4000000:
        return "Monstro Link"
    if card_type & 0x800000:
        return "Monstro Xyz"
    if card_type & 0x2000:
        return "Monstro Sincro"
    if card_type & 0x40:
        return "Monstro de Fusao"
    if card_type & 0x80:
        return "Monstro de Ritual"
    if card_type & 0x1000000:
        return "Monstro Pendulo"
    if card_type & 0x1:
        if card_type & 0x10:
            return "Monstro Regulador Normal" if card_type & 0x1000 else "Monstro Normal"
        if card_type & 0x1000:
            return "Monstro Regulador de Efeito"
        return "Monstro de Efeito"
    if card_type & 0x2:
        return "Carta de Magia"
    if card_type & 0x4:
        return "Carta de Armadilha"
    return "Carta"


def complexity(card_type: int, description: str) -> str:
    lowered = (description or "").casefold()
    if card_type == 17 or card_type & 0x4000:
        return "simple"
    if card_type & (0x40 | 0x2000 | 0x800000 | 0x4000000):
        return "extra_deck"
    if "negue" in lowered or "negate" in lowered:
        return "negation"
    if "efeito rapido" in lowered or "quick effect" in lowered:
        return "quick"
    if card_type & 0x20000:
        return "continuous"
    if "quando" in lowered or "when " in lowered or "se " in lowered:
        return "trigger"
    return "intermediate"


def frame_style(catalog_item: dict[str, object], card_type: int) -> str:
    category = int(str(catalog_item.get("category") or 0))
    if category == 2:
        return "spell"
    if category == 3:
        return "trap"
    frame = int(str(catalog_item.get("monsterFrame") or 0))
    style = FRAME_STYLE.get(frame)
    if style and style != "unknown":
        return style
    if card_type & 0x4000000:
        return "link"
    if card_type & 0x800000:
        return "xyz"
    if card_type & 0x2000:
        return "synchro"
    if card_type & 0x40:
        return "fusion"
    if card_type & 0x80:
        return "ritual"
    return "effect"


def risk_for(card_type: int, card_complexity: str) -> str:
    if card_type == 17 or card_type & 0x4000:
        return "A"
    if card_complexity in {"extra_deck", "negation", "quick", "continuous"}:
        return "C"
    return "B"


def script_documentation(script: dict[str, object]) -> tuple[str, str]:
    compatibility = str(script["compatibility"])
    if compatibility == "NOT_REQUIRED":
        return "", "not_required_no_effect"
    runtime_path = str(script["path"])
    if "/Scripts/official/" in runtime_path:
        return "official/" + Path(runtime_path).name, "true"
    if "/CustomScripts/" in runtime_path:
        return "custom/" + Path(runtime_path).name, "via_alias"
    return Path(runtime_path).name, "true"


def csv_bytes(rows: list[dict[str, str]]) -> bytes:
    output = io.StringIO(newline="")
    writer = csv.DictWriter(output, fieldnames=CATALOG_FIELDS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return output.getvalue().encode("utf-8")


def atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    os.replace(temporary, path)


def deterministic_meta(code: str) -> bytes:
    guid = hashlib.md5(
        ("ArcaneDuel/StreamingArt/" + code).encode("ascii"),
        usedforsecurity=False,
    ).hexdigest()
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    ).encode("utf-8")


def build_plan() -> dict[str, object]:
    catalog = audit.read_catalog()
    artwork = audit.resolve_artwork_guids(catalog)
    docs_rows, docs = audit.read_csv(ROOT / "Documentation/CardCatalog.csv")
    database = audit.read_binary_cards()
    text_payload = json.loads(
        (ROOT / "Assets/StreamingAssets/Ygo/Data/card-texts.json")
        .read_text(encoding="utf-8-sig")
    )
    texts = {audit.norm(card["code"]): card for card in text_payload["cards"]}
    visual_payload = json.loads(
        (ROOT / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json")
        .read_text(encoding="utf-8-sig")
    )
    visuals = {audit.norm(card["officialCode"]): card
               for card in visual_payload["cards"]}
    arrays, curated = audit.read_curated()
    starters, _ = audit.read_starters()
    shops, _ = audit.read_shop_groups(arrays)
    memberships = audit.add_memberships({**curated, **starters, **shops})

    catalog_ids = [audit.norm(item["officialCardId"]) for item in catalog]
    if not all(catalog_ids) or len(set(catalog_ids)) != len(catalog_ids):
        raise ValueError("CardCatalog must contain unique valid OfficialCardIds")

    missing_database: list[str] = []
    missing_text: list[str] = []
    missing_source_art: list[str] = []
    unsupported_scripts: list[str] = []
    art_conflicts: list[str] = []
    additions = sorted(set(catalog_ids) - set(visuals))
    removals = sorted(set(visuals) - set(catalog_ids))
    doc_additions = sorted(set(catalog_ids) - set(docs))
    doc_removals = sorted(set(docs) - set(catalog_ids))
    rows: list[dict[str, str]] = []
    output_visuals: list[dict[str, object]] = []
    art_copies: list[tuple[str, Path, Path]] = []

    for item in catalog:
        card_id = audit.norm(item["officialCardId"])
        record = database.get(card_id)
        text = texts.get(card_id)
        if record is None:
            missing_database.append(card_id)
            continue
        if text is None:
            missing_text.append(card_id)
            continue
        source_art = artwork.get(str(item.get("artworkGuid") or ""))
        if source_art is None or not source_art.is_file():
            missing_source_art.append(card_id)
            continue
        required = record["type"] != 17 and not (record["type"] & 0x4000)
        script = audit.resolve_script(card_id, required)
        if not script["found"] or script["compatibility"] not in {
                "RESOLVED_STATIC", "NOT_REQUIRED"}:
            unsupported_scripts.append(card_id)
            continue
        target_art = ROOT / "Assets/StreamingAssets/Ygo/Art" / f"{int(card_id)}.jpg"
        if target_art.is_file() and audit.sha(target_art) != audit.sha(source_art):
            art_conflicts.append(card_id)
        elif not target_art.is_file():
            art_copies.append((card_id, source_art, target_art))

        old = dict(docs.get(card_id, {}))
        card_complexity = old.get("complexity") or complexity(
            int(record["type"]), str(text.get("description") or ""))
        script_path, script_status = script_documentation(script)
        deck_labels = memberships.get(card_id, [])
        old.update({
            "official_code": card_id,
            "name": old.get("name") or str(text.get("name") or ""),
            "source": old.get("source") or "official",
            "type": old.get("type") or type_name(int(record["type"])),
            "deck_origin": old.get("deck_origin") or (
                ";".join(deck_labels) if deck_labels else "published_catalog"),
            "complexity": card_complexity,
            "vertical_slice_role": old.get("vertical_slice_role") or "",
            "script_path": script_path,
            "script_found": script_status,
            "database_found": "true",
            "source_art_path": old.get("source_art_path") or (
                "CardCatalog.asset#" + str(item.get("artworkGuid") or "")),
            "art_asset": source_art.relative_to(ROOT).as_posix(),
            "image_available": "true",
            "test_status": old.get("test_status") or "pending",
            "notes": old.get("notes") or "Reconciled from published CardCatalog.",
            "database_name": str(text.get("name") or ""),
            "database_alias": str(record["alias"]) if record["alias"] else "",
        })
        rows.append({field: str(old.get(field, "")) for field in CATALOG_FIELDS})

        existing = dict(visuals.get(card_id, {}))
        style = frame_style(item, int(record["type"]))
        existing.update({
            "officialCode": int(card_id),
            "artFile": f"{int(card_id)}.jpg",
            "frameStyle": existing.get("frameStyle") or style,
            "summonVfx": existing.get("summonVfx") or (
                "none" if style in {"spell", "trap"} else
                "extra_summon" if style in {
                    "fusion", "synchro", "xyz", "link", "ritual", "pendulum"
                } else "normal_summon"),
            "activationSfx": existing.get("activationSfx") or (
                "arcane_activation" if style in {"spell", "trap"}
                else "arcane_summon"),
            "riskLevel": existing.get("riskLevel") or risk_for(
                int(record["type"]), card_complexity),
            "scriptStatus": existing.get("scriptStatus") or script_status,
            "scriptFile": existing.get("scriptFile") or Path(script_path).name,
            "presentationTags": existing.get("presentationTags") or [
                type_name(int(record["type"])), card_complexity,
                "published_catalog_reconciliation",
            ],
        })
        output_visuals.append(existing)

    blockers = {
        "missingDatabase": missing_database,
        "missingText": missing_text,
        "missingSourceArt": missing_source_art,
        "unsupportedScripts": unsupported_scripts,
        "artConflicts": art_conflicts,
    }
    if any(blockers.values()):
        raise ValueError("Reconciliation blocked: " + json.dumps(blockers))
    rows.sort(key=lambda row: int(row["official_code"]))
    output_visuals.sort(key=lambda card: int(card["officialCode"]))
    csv_content = csv_bytes(rows)
    visual_output = {
        "schemaVersion": 1,
        "count": len(output_visuals),
        "catalogSha256": hashlib.sha256(csv_content).hexdigest().upper(),
        "cards": output_visuals,
    }
    return {
        "generatedUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "catalogCount": len(catalog_ids),
        "documentationBefore": len(docs_rows),
        "visualsBefore": len(visuals),
        "documentationAdditions": doc_additions,
        "documentationRemovals": doc_removals,
        "visualAdditions": additions,
        "visualRemovals": removals,
        "artCopies": art_copies,
        "rows": rows,
        "csvContent": csv_content,
        "visualOutput": visual_output,
    }


def report_text(plan: dict[str, object]) -> str:
    lines = [
        "# Reconciliacao do catalogo publicado", "",
        f"Gerado em UTC: `{plan['generatedUtc']}`.", "",
        "## Escopo", "",
        "A operacao alinha documentacao e apresentacao runtime ao `CardCatalog.asset` publicado.",
        "Nao altera Lua, `cards.bin`, textos compilados, plugins, cenas ou regras.", "",
        "| Item | Antes | Depois |", "|---|---:|---:|",
        f"| Documentation/CardCatalog.csv | {plan['documentationBefore']} | {plan['catalogCount']} |",
        f"| card-visuals.json | {plan['visualsBefore']} | {plan['catalogCount']} |",
        f"| Artes copiadas para StreamingAssets | 0 | {len(plan['artCopies'])} |", "",
        "## Alteracoes", "",
        f"- Entradas documentais adicionadas: {len(plan['documentationAdditions'])}.",
        f"- Entradas documentais residuais removidas: {len(plan['documentationRemovals'])}.",
        f"- Entradas visuais adicionadas: {len(plan['visualAdditions'])}.",
        f"- Entradas visuais residuais removidas: {len(plan['visualRemovals'])}.",
        f"- Artes autoradas copiadas: {len(plan['artCopies'])}.", "",
        "## Evidencia e rollback", "",
        "A fonte de cada arte e o GUID permanecem registrados na matriz da auditoria.",
        "Rollback: reverter `Documentation/CardCatalog.csv`, `card-visuals.json` e as novas artes em `StreamingAssets/Ygo/Art`.",
    ]
    return "\n".join(lines) + "\n"


def apply(plan: dict[str, object]) -> None:
    for card_id, source, target in plan["artCopies"]:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        meta = Path(str(target) + ".meta")
        if not meta.exists():
            atomic_write(meta, deterministic_meta(card_id))
    atomic_write(ROOT / "Documentation/CardCatalog.csv", plan["csvContent"])
    visual = json.dumps(
        plan["visualOutput"], ensure_ascii=False, indent=2
    ).encode("utf-8") + b"\n"
    atomic_write(
        ROOT / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json", visual
    )
    atomic_write(
        ROOT / "Documentation/CardAudit/PublishedCatalogReconciliation.md",
        report_text(plan).encode("utf-8"),
    )


def summary(plan: dict[str, object]) -> dict[str, object]:
    return {
        "catalogCount": plan["catalogCount"],
        "documentationBefore": plan["documentationBefore"],
        "documentationAdditions": len(plan["documentationAdditions"]),
        "documentationRemovals": len(plan["documentationRemovals"]),
        "visualsBefore": plan["visualsBefore"],
        "visualAdditions": len(plan["visualAdditions"]),
        "visualRemovals": len(plan["visualRemovals"]),
        "artCopies": len(plan["artCopies"]),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    plan = build_plan()
    print(json.dumps(summary(plan), indent=2))
    if not args.apply:
        print("Preview only; pass --apply to reconcile generated sources.")
        return 0
    apply(plan)
    print("ARCANE_PUBLISHED_CATALOG_RECONCILE_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
