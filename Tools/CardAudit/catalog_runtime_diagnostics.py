#!/usr/bin/env python3
"""Catalog silent Arcane Duel runtime diagnostics.

Preview is the default. Use --write to update the versioned JSON/Markdown
catalog. Android diagnostic folders can be copied to disk D and supplied with
additional --input arguments; the tool never searches outside explicit paths.
"""

from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


PROJECT_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_INPUT = PROJECT_ROOT / "Logs" / "CardAuditRuntime"
OUTPUT_JSON = PROJECT_ROOT / "Documentation" / "CardAudit" / "RuntimeDiagnosticsCatalog.json"
OUTPUT_MD = PROJECT_ROOT / "Documentation" / "CardAudit" / "RuntimeDiagnosticsCatalog.md"


def diagnostic_files(inputs: Iterable[Path]) -> list[Path]:
    found: set[Path] = set()
    for candidate in inputs:
        path = candidate.resolve()
        if path.is_file():
            found.add(path)
        elif path.is_dir():
            found.update(item.resolve() for item in path.rglob("*.jsonl*"))
    return sorted(found, key=lambda item: str(item).lower())


def read_records(files: Iterable[Path]) -> tuple[list[dict[str, Any]], int]:
    records: list[dict[str, Any]] = []
    invalid = 0
    for path in files:
        with path.open("r", encoding="utf-8-sig", errors="replace") as stream:
            for line_number, line in enumerate(stream, 1):
                if not line.strip():
                    continue
                try:
                    record = json.loads(line)
                    if not isinstance(record, dict):
                        raise ValueError("record is not an object")
                    record["_source"] = str(path.relative_to(PROJECT_ROOT)) if path.is_relative_to(PROJECT_ROOT) else str(path)
                    record["_line"] = line_number
                    records.append(record)
                except (json.JSONDecodeError, ValueError):
                    invalid += 1
    return records, invalid


def build_catalog(files: list[Path], records: list[dict[str, Any]], invalid: int) -> dict[str, Any]:
    groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    sessions: set[str] = set()
    platforms: Counter[str] = Counter()
    failure_codes: Counter[str] = Counter()
    severities: Counter[str] = Counter()
    for record in records:
        session = str(record.get("sessionId") or "")
        if session:
            sessions.add(session)
        platforms[str(record.get("platform") or "UNKNOWN")] += 1
        failure_code = str(record.get("failureCode") or "F00")
        failure_codes[failure_code] += 1
        severities[str(record.get("severity") or "UNKNOWN")] += 1
        if failure_code not in {"SESSION", "RATE_LIMIT"}:
            fingerprint = str(record.get("fingerprint") or "MISSING_FINGERPRINT")
            groups[fingerprint].append(record)

    occurrences: list[dict[str, Any]] = []
    for fingerprint, items in groups.items():
        ordered = sorted(items, key=lambda item: str(item.get("utc") or ""))
        first, last = ordered[0], ordered[-1]
        occurrences.append({
            "fingerprint": fingerprint,
            "count": len(items),
            "failureCode": str(last.get("failureCode") or "F00"),
            "severity": str(last.get("severity") or "UNKNOWN"),
            "layer": str(last.get("layer") or "Unclassified"),
            "component": str(last.get("component") or "Unknown"),
            "cardCode": int(last.get("cardCode") or 0),
            "mode": str(last.get("mode") or ""),
            "message": str(last.get("message") or ""),
            "details": str(last.get("details") or "")[:2000],
            "firstUtc": str(first.get("utc") or ""),
            "lastUtc": str(last.get("utc") or ""),
            "sessions": sorted({str(item.get("sessionId") or "") for item in items if item.get("sessionId")}),
            "platforms": sorted({str(item.get("platform") or "UNKNOWN") for item in items}),
            "sources": sorted({f"{item.get('_source')}:{item.get('_line')}" for item in items}),
            "status": "ABERTA_PARA_TRIAGEM",
        })
    occurrences.sort(key=lambda item: (-item["count"], item["failureCode"], item["fingerprint"]))

    return {
        "schemaVersion": 1,
        "generatedUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "mode": "catalog-only-no-gameplay-ui",
        "sourceFiles": [str(path.relative_to(PROJECT_ROOT)) if path.is_relative_to(PROJECT_ROOT) else str(path) for path in files],
        "recordCount": len(records),
        "invalidLineCount": invalid,
        "sessionCount": len(sessions),
        "uniqueOccurrenceCount": len(occurrences),
        "failureCodeCounts": dict(sorted(failure_codes.items())),
        "severityCounts": dict(sorted(severities.items())),
        "platformCounts": dict(sorted(platforms.items())),
        "occurrences": occurrences,
    }


def markdown(catalog: dict[str, Any]) -> str:
    lines = [
        "# Catálogo de diagnósticos runtime",
        "",
        f"Gerado em UTC: `{catalog['generatedUtc']}`",
        "",
        "O gravador é silencioso: nenhum registro é apresentado na tela de jogabilidade.",
        "Códigos de sala, Relay e seeds são removidos antes da persistência.",
        "",
        "## Resumo",
        "",
        f"- Arquivos lidos: **{len(catalog['sourceFiles'])}**",
        f"- Sessões: **{catalog['sessionCount']}**",
        f"- Registros válidos: **{catalog['recordCount']}**",
        f"- Linhas inválidas: **{catalog['invalidLineCount']}**",
        f"- Ocorrências únicas para triagem: **{catalog['uniqueOccurrenceCount']}**",
        "",
        "## Ocorrências abertas",
        "",
        "| Contagem | Código | Camada | Componente | Carta | Plataformas | Mensagem |",
        "|---:|---|---|---|---:|---|---|",
    ]
    for item in catalog["occurrences"]:
        message = item["message"].replace("|", "\\|").replace("\n", " ")[:160]
        lines.append(
            f"| {item['count']} | {item['failureCode']} | {item['layer']} | "
            f"{item['component']} | {item['cardCode'] or '-'} | "
            f"{', '.join(item['platforms'])} | {message} |"
        )
    if not catalog["occurrences"]:
        lines.append("| 0 | - | - | - | - | - | Nenhuma falha registrada ainda. |")
    lines.extend([
        "",
        "## Uso",
        "",
        "- Editor: os registros ficam em `Logs/CardAuditRuntime` dentro do projeto.",
        "- Android: copie a pasta `CardAuditRuntime` do armazenamento persistente do aplicativo para o disco D e passe-a com `--input`.",
        "- Preview: `python -B Tools/CardAudit/catalog_runtime_diagnostics.py`.",
        "- Atualizar relatório: `python -B Tools/CardAudit/catalog_runtime_diagnostics.py --write`.",
        "",
        "A presença de um registro não declara automaticamente defeito na carta. Cada ocorrência deve seguir a árvore fontes -> script -> Core -> protocolo -> apresentação -> IA -> multiplayer.",
        "",
    ])
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", action="append", type=Path, default=[])
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    inputs = [DEFAULT_INPUT, *args.input]
    files = diagnostic_files(inputs)
    records, invalid = read_records(files)
    catalog = build_catalog(files, records, invalid)
    if args.write:
        OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT_JSON.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        OUTPUT_MD.write_text(markdown(catalog), encoding="utf-8")
    print(json.dumps({key: catalog[key] for key in (
        "recordCount", "invalidLineCount", "sessionCount", "uniqueOccurrenceCount",
        "failureCodeCounts", "platformCounts")}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
