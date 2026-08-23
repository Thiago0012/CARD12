#!/usr/bin/env python3
"""Read-only Phase 0/1 card audit fallback.

The Unity Editor menu is the canonical entry point. This companion exists so
CI or a locked-down workstation can reproduce the inventory without opening
Unity. It previews by default and writes only with --write.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import struct
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
REPORT_DIR = ROOT / "Documentation" / "CardAudit"
CATEGORY = {0: "Unknown", 1: "Monster", 2: "Spell", 3: "Trap"}
FRAME = {
    0: "None", 1: "Unknown", 2: "Normal", 3: "Effect", 4: "Ritual",
    5: "Fusion", 6: "Synchro", 7: "Xyz", 8: "Link", 9: "Pendulum",
    10: "Token",
}


def norm(value: object) -> str:
    text = str(value or "").strip()
    return f"{int(text):08d}" if text.isdigit() and int(text) else ""


def sha(path: Path) -> str:
    if not path.is_file():
        return ""
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def tree_sha(path: Path, suffix: str) -> str:
    manifest = bytearray()
    for file in sorted(path.rglob(f"*{suffix}")) if path.is_dir() else []:
        manifest.extend(file.relative_to(path).as_posix().encode("utf-8"))
        manifest.extend(b"|")
        manifest.extend(sha(file).encode("ascii"))
        manifest.extend(b"\n")
    return hashlib.sha256(manifest).hexdigest().upper()


def git_identity() -> tuple[str, str]:
    head_file = ROOT / ".git" / "HEAD"
    if not head_file.is_file():
        return "", ""
    head = head_file.read_text(encoding="utf-8").strip()
    if not head.startswith("ref: "):
        return "DETACHED", head
    ref = head[5:].strip()
    branch = ref.removeprefix("refs/heads/")
    loose = ROOT / ".git" / Path(ref)
    if loose.is_file():
        return branch, loose.read_text(encoding="utf-8").strip()
    packed = ROOT / ".git" / "packed-refs"
    if packed.is_file():
        for line in packed.read_text(encoding="utf-8").splitlines():
            if line.endswith(" " + ref):
                return branch, line.split(" ", 1)[0]
    return branch, ""


def project_identity() -> dict[str, str]:
    text = (ROOT / "Assets/Game/Runtime/ProjectIdentity.cs").read_text(
        encoding="utf-8-sig")
    result: dict[str, str] = {}
    for name in ("ProjectVersion", "UnityVersion", "CoreApiVersion",
                 "CoreCommit", "CardScriptsCommit", "BabelCdbCommit"):
        match = re.search(rf'{name}\s*=\s*"([^"]+)"', text)
        result[name] = match.group(1) if match else ""
    banlist_text = (ROOT / "Assets/Resources/StarterDecks/StarterDeckCatalog.asset") \
        .read_text(encoding="utf-8-sig")
    banlist = re.search(r"^  activeBanlistId:\s*(.+)$", banlist_text, re.M)
    result["MultiplayerCompatibility"] = "|".join([
        result["ProjectVersion"], result["CoreApiVersion"],
        result["CoreCommit"], result["CardScriptsCommit"],
        result["BabelCdbCommit"], banlist.group(1).strip() if banlist else "",
    ])
    return result


def read_csv(path: Path) -> tuple[list[dict[str, str]], dict[str, dict[str, str]]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    by_id: dict[str, dict[str, str]] = {}
    for row in rows:
        card_id = norm(row.get("official_code"))
        if card_id and card_id not in by_id:
            by_id[card_id] = row
    return rows, by_id


def read_catalog() -> list[dict[str, object]]:
    text = (ROOT / "Assets/Cards/CardCatalog.asset").read_text(
        encoding="utf-8-sig")
    blocks = re.split(r"(?=^  - stableId:)", text, flags=re.M)[1:]
    entries: list[dict[str, object]] = []
    keys = ("stableId", "officialCardId", "category", "monsterFrame",
            "officiallyRegistered", "needsManualReview")
    for block in blocks:
        item: dict[str, object] = {}
        for key in keys:
            match = re.search(rf"^    {key}:\s*(.*)$", block, re.M)
            if key == "stableId":
                match = re.search(r"^  - stableId:\s*(.*)$", block, re.M)
            item[key] = match.group(1).strip() if match else ""
        artwork = re.search(r"^    artwork:.*?guid:\s*([0-9a-f]+)", block, re.M)
        item["artworkGuid"] = artwork.group(1) if artwork else ""
        entries.append(item)
    return entries


def resolve_artwork_guids(catalog: list[dict[str, object]]) -> dict[str, Path]:
    requested = {str(item.get("artworkGuid") or "") for item in catalog}
    requested.discard("")
    result: dict[str, Path] = {}
    for meta in (ROOT / "Assets/Cards").rglob("*.meta"):
        match = re.search(
            r"^guid:\s*([0-9a-f]+)",
            meta.read_text(encoding="utf-8-sig", errors="ignore"),
            re.M,
        )
        if match and match.group(1) in requested:
            asset = meta.with_suffix("")
            if asset.is_file():
                result[match.group(1)] = asset
    return result


def read_binary_cards() -> dict[str, dict[str, object]]:
    data = (ROOT / "Assets/StreamingAssets/Ygo/Data/cards.bin").read_bytes()
    if data[:4] != b"ADCB":
        raise ValueError("cards.bin has unexpected magic")
    version, count = struct.unpack_from("<II", data, 4)
    if version != 1:
        raise ValueError(f"unsupported cards.bin version {version}")
    offset = 12
    result: dict[str, dict[str, object]] = {}
    for _ in range(count):
        values = struct.unpack_from("<IIIiIQiiIII", data, offset)
        offset += 48
        setcode_count = data[offset]
        offset += 1
        setcodes = list(struct.unpack_from(
            "<" + "H" * setcode_count, data, offset)) if setcode_count else []
        offset += setcode_count * 2
        code, alias, card_type = values[:3]
        result[f"{code:08d}"] = {
            "code": code, "alias": alias, "type": card_type,
            "setcodes": setcodes,
        }
    if offset != len(data):
        raise ValueError("cards.bin has trailing bytes")
    return result


def read_curated() -> tuple[dict[str, list[str]], dict[str, list[str]]]:
    arrays: dict[str, list[str]] = {}
    groups: dict[str, list[str]] = {}
    for file in sorted((ROOT / "Assets/Game/Runtime").glob("CuratedDeckLists*.cs")):
        text = file.read_text(encoding="utf-8-sig")
        for match in re.finditer(
                r"public\s+static\s+readonly\s+uint\[\]\s+(\w+)\s*=\s*\{(.*?)\};",
                text, re.S):
            name = match.group(1)
            ids = [norm(value) for value in re.findall(r"\b\d+\b", match.group(2))]
            ids = [value for value in ids if value]
            arrays[name] = ids
            section = next((suffix for suffix in ("Extra", "Side", "Main")
                            if name.endswith(suffix)), "Main")
            deck = name[:-len(section)] if name.endswith(section) else name
            groups[f"curated:{deck}:{section}"] = ids
    return arrays, groups


def read_starters() -> tuple[dict[str, list[str]], str]:
    guid_to_asset: dict[str, Path] = {}
    base = ROOT / "Assets/Resources/StarterDecks/Definitions"
    for meta in base.glob("*.asset.meta"):
        match = re.search(r"^guid:\s*(\S+)", meta.read_text(encoding="utf-8-sig"), re.M)
        if match:
            guid_to_asset[match.group(1)] = meta.with_suffix("")
    catalog = (ROOT / "Assets/Resources/StarterDecks/StarterDeckCatalog.asset") \
        .read_text(encoding="utf-8-sig")
    order = re.findall(r"guid:\s*([0-9a-f]+)", catalog)
    groups: dict[str, list[str]] = {}
    first = ""
    for guid in order:
        asset = guid_to_asset.get(guid)
        if not asset or not asset.is_file():
            continue
        text = asset.read_text(encoding="utf-8-sig")
        deck_id_match = re.search(r"^  id:\s*(\S+)", text, re.M)
        if not deck_id_match:
            continue
        deck_id = deck_id_match.group(1)
        publishable = re.search(r"^  publishable:\s*1", text, re.M) is not None
        if publishable and not first:
            first = deck_id
        for section in ("mainDeck", "extraDeck", "sideDeck"):
            match = re.search(
                rf"^  {section}:\s*\n(?P<body>(?:^  - .*\n?)*)",
                text, re.M)
            values = [norm(value) for value in re.findall(
                r"^  -\s*(\d+)", match.group("body") if match else "", re.M)]
            values = [value for value in values if value]
            groups[f"starter:{deck_id}:{section}"] = values
    return groups, first


def read_shop_groups(arrays: dict[str, list[str]]) -> tuple[dict[str, list[str]], int]:
    sources = "\n".join(file.read_text(encoding="utf-8-sig")
                          for file in sorted((ROOT / "Assets/Scripts/Frontend")
                                             .glob("DeckShopCatalog*.cs")))
    constants = dict(re.findall(
        r"public const string (\w+ProductId)\s*=\s*\"([^\"]+)\"", sources))
    groups: dict[str, list[str]] = {}
    for match in re.finditer(
            r"(?:Product|new\s+DeckShopProduct)\(\s*(\w+ProductId).*?"
            r"CuratedDeckLists\.(\w+Main).*?CuratedDeckLists\.(\w+Extra)",
            sources, re.S):
        constant, main, extra = match.groups()
        if constant in constants and main in arrays and extra in arrays:
            groups[f"shop:{constants[constant]}:Main"] = arrays[main]
            groups[f"shop:{constants[constant]}:Extra"] = arrays[extra]
    for match in re.finditer(
            r"new\s+DeckShopProduct\(\s*(\w+ProductId).*?"
            r"new\[\]\s*\{(?P<main>.*?)\}\s*,\s*new\[\]\s*\{(?P<extra>.*?)\}",
            sources, re.S):
        constant = match.group(1)
        if constant not in constants:
            continue
        groups[f"shop:{constants[constant]}:Main"] = [
            norm(value) for value in re.findall(r'\"(\d+)\"', match.group("main"))]
        groups[f"shop:{constants[constant]}:Extra"] = [
            norm(value) for value in re.findall(r'\"(\d+)\"', match.group("extra"))]
    return groups, len({key.split(":", 2)[1] for key in groups})


def add_memberships(groups: dict[str, list[str]]) -> dict[str, list[str]]:
    result: dict[str, set[str]] = defaultdict(set)
    for label, ids in groups.items():
        for card_id in ids:
            if card_id:
                result[card_id].add(label)
    return {card_id: sorted(labels) for card_id, labels in result.items()}


def resolve_script(card_id: str, required: bool) -> dict[str, object]:
    if not required:
        return {"found": True, "source": "", "path": "", "hash": "",
                "compatibility": "NOT_REQUIRED", "missing": []}
    filename = f"c{int(card_id)}.lua"
    roots = [
        (ROOT / "Assets/StreamingAssets/Ygo/CustomScripts", "custom-override"),
        (ROOT / "Assets/StreamingAssets/Ygo/Scripts", "global-scripts"),
        (ROOT / "Assets/StreamingAssets/Ygo/Scripts/official", "official"),
    ]
    selected: tuple[Path, str] | None = next(
        ((root / filename, source) for root, source in roots
         if (root / filename).is_file()), None)
    if not selected:
        return {"found": False, "source": "", "path": "", "hash": "",
                "compatibility": "MISSING", "missing": []}
    path, source = selected
    missing: list[str] = []
    body = path.read_text(encoding="utf-8-sig", errors="replace")
    for dependency in sorted(set(re.findall(
            r"Duel\s*\.\s*LoadScript\s*\(\s*[\"']([^\"']+)[\"']", body))):
        safe = Path(dependency).name == dependency
        if not safe or not any((root / dependency).is_file() for root, _ in roots):
            missing.append(dependency)
    empty = path.stat().st_size == 0
    compatibility = "EMPTY" if empty else (
        "DEPENDENCY_MISSING" if missing else "RESOLVED_STATIC")
    return {
        "found": True, "source": source,
        "path": path.relative_to(ROOT).as_posix(),
        "hash": "" if empty else sha(path),
        "compatibility": compatibility, "missing": missing,
    }


def make_snapshot() -> dict[str, object]:
    identity = project_identity()
    branch, head = git_identity()
    catalog = read_catalog()
    artwork_by_guid = resolve_artwork_guids(catalog)
    docs_rows, docs = read_csv(ROOT / "Documentation/CardCatalog.csv")
    core_rows, core_docs = read_csv(ROOT / "Documentation/CoreCardCatalog.csv")
    database = read_binary_cards()
    texts_file = json.loads((ROOT / "Assets/StreamingAssets/Ygo/Data/card-texts.json")
                            .read_text(encoding="utf-8-sig"))
    texts = {norm(card["code"]): card for card in texts_file["cards"]}
    visual_file = json.loads(
        (ROOT / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json")
        .read_text(encoding="utf-8-sig"))
    visuals = {norm(card["officialCode"]): card for card in visual_file["cards"]}
    arrays, curated_groups = read_curated()
    starter_groups, first_starter = read_starters()
    shop_groups, shop_count = read_shop_groups(arrays)
    all_groups = {**curated_groups, **starter_groups, **shop_groups}
    memberships = add_memberships(all_groups)
    pack_file = json.loads((ROOT / "Assets/Resources/Shop/PackCatalog.json")
                           .read_text(encoding="utf-8-sig"))
    pack_groups = {f"pack:{pack.get('packId', '')}":
                   [norm(value) for value in pack.get("cardIds", [])]
                   for pack in pack_file.get("packs", [])}
    pack_memberships = add_memberships(pack_groups)
    generated = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    catalog_ids = [norm(item["officialCardId"]) for item in catalog]
    duplicate_catalog = sorted(card_id for card_id, count in Counter(
        value for value in catalog_ids if value).items() if count > 1)
    cards: list[dict[str, object]] = []
    for item in catalog:
        card_id = norm(item["officialCardId"])
        doc = docs.get(card_id, {})
        record = database.get(card_id)
        text = texts.get(card_id, {})
        visual = visuals.get(card_id)
        valid = bool(card_id)
        registered = str(item["officiallyRegistered"]) == "1"
        ready = str(item["category"]) != "0" and str(item["needsManualReview"]) != "1"
        card_type = int(record["type"]) if record else 0
        plain_normal = (
            bool(card_type & 0x10)
            and not bool(card_type & 0x20)
            and not bool(card_type & 0x1000000)
        )
        required = bool(record) and not plain_normal and not (card_type & 0x4000)
        script = resolve_script(card_id, required) if valid else resolve_script("", False)
        documented_art = ROOT / doc.get("art_asset", "")
        guid_art = artwork_by_guid.get(str(item.get("artworkGuid") or ""))
        asset_art = documented_art if documented_art.is_file() else guid_art
        stream_art = (ROOT / "Assets/StreamingAssets/Ygo/Art" /
                      str(visual.get("artFile", ""))) if visual else Path()
        art_path = (asset_art.relative_to(ROOT).as_posix() if asset_art and asset_art.is_file()
                    else stream_art.relative_to(ROOT).as_posix()
                    if visual and stream_art.is_file() else "")
        deck_labels = memberships.get(card_id, [])
        pack_labels = [value.removeprefix("pack:")
                       for value in pack_memberships.get(card_id, [])]
        description = str(text.get("description", ""))
        online = any(value.casefold() in description.casefold() for value in (
            "sua mao", "do seu deck", "revele", "com a face para baixo",
            "escolha 1 card", "ordem", "materia xyz", "controle desse"))
        special = bool(record and (record["type"] &
                    (0x40 | 0x80 | 0x2000 | 0x800000 | 0x1000000 | 0x4000000)))
        scenarios = ["integridade_fontes"]
        if required or special:
            scenarios += ["core_positivo_minimo", "core_negativo_relevante",
                          "apresentacao_prompt_zona"]
        if deck_labels:
            scenarios.append("deck_smoke_ia")
        if online:
            scenarios.append("multiplayer_privacidade_resync")
        coverage: list[str] = []
        if required:
            coverage += ["dossie_semantico", "cenario_core_positivo",
                         "cenario_core_negativo", "cenario_apresentacao"]
        if deck_labels:
            coverage.append("deck_smoke_deterministico_por_linha_central")
        if online:
            coverage.append("host_cliente_privacidade_idempotencia_resync")
        if not coverage:
            coverage.append("validacao_integridade_executada_neste_lote")
        blockers: list[str] = []
        if not valid: blockers.append("OfficialCardId invalido ou vazio")
        if not registered: blockers.append("registro oficial ausente")
        if not ready: blockers.append("catalogo marcado para revisao")
        if card_id in duplicate_catalog: blockers.append("OfficialCardId duplicado no CardCatalog")
        if card_id not in database: blockers.append("dados compilados ausentes")
        if card_id not in texts: blockers.append("texto compilado ausente")
        if card_id not in visuals: blockers.append("manifesto visual ausente")
        if not art_path: blockers.append("arte ausente")
        if required and not script["found"]: blockers.append("script obrigatorio ausente")
        if script["compatibility"] == "EMPTY": blockers.append("script obrigatorio vazio")
        if script["missing"]: blockers.append("dependencia Lua ausente")
        status = "BLOQUEADA_DADOS" if blockers else "CARREGA"
        priority = ("P0" if blockers else "P1" if deck_labels else
                    "P3" if online else "P2" if required or special else "P5")
        cards.append({
            "officialCardId": card_id, "stableId": str(item["stableId"]),
            "name": doc.get("name") or text.get("name", ""),
            "category": CATEGORY.get(int(item["category"] or 0), "Unknown"),
            "monsterFrame": FRAME.get(int(item["monsterFrame"] or 0), "Unknown"),
            "typeName": doc.get("type", ""),
            "archetypeSetcodes": ";".join(f"0x{x:04X}" for x in
                                            (record or {}).get("setcodes", [])),
            "aliasOfficialCardId": f"{record['alias']:08d}" if record and record["alias"] else "",
            "decks": deck_labels, "packs": pack_labels,
            "inCardCatalog": True, "officiallyRegistered": registered,
            "readyForGameplay": ready, "inDocumentationCsv": card_id in docs,
            "inCoreDocumentationCsv": card_id in core_docs,
            "inCompiledDatabase": card_id in database,
            "inTextDatabase": card_id in texts, "inVisualManifest": card_id in visuals,
            "artworkFound": bool(art_path), "artworkPath": art_path,
            "artworkGuid": str(item.get("artworkGuid") or "") if art_path else "",
            "scriptRequired": required, "scriptFound": bool(script["found"]),
            "scriptSource": script["source"], "scriptPath": script["path"],
            "scriptSha256": script["hash"],
            "scriptCompatibility": script["compatibility"],
            "missingScriptDependencies": script["missing"],
            "applicableScenarios": list(dict.fromkeys(scenarios)),
            "existingEvidence": [
                "CardDatabaseEditModeTests (teste existente; execucao do lote pendente)",
                "CardCatalogBatchEditModeTests.EveryCompiledCoreCardRegistersWithNativeCoreLifecycle (teste existente; execucao do lote pendente)",
            ],
            "missingCoverage": coverage,
            "coreResult": "NAO_EXECUTADO_NESTE_LOTE",
            "presentationResult": "NAO_EXECUTADO_NESTE_LOTE",
            "aiResult": "NAO_EXECUTADO_NESTE_LOTE" if deck_labels else "NAO_APLICAVEL_NESTA_PRIORIZACAO",
            "multiplayerResult": "NAO_EXECUTADO_NESTE_LOTE" if online else "NAO_APLICAVEL_NESTA_PRIORIZACAO",
            "regressionResult": "NAO_EXECUTADO_NESTE_LOTE",
            "status": status, "priority": priority,
            "failureCode": "F01" if blockers else "",
            "responsibleLayer": "dados/catalogo" if blockers else "",
            "blockingReason": "; ".join(blockers),
            "risk": str((visual or {}).get("riskLevel", "NAO_CLASSIFICADO")),
            "sourceVersion": identity["MultiplayerCompatibility"],
            "evidenceUpdatedUtc": generated,
        })
    cards.sort(key=lambda card: (card["officialCardId"], card["stableId"]))
    catalog_set = {value for value in catalog_ids if value}
    docs_ids = [norm(row.get("official_code")) for row in docs_rows]
    divergence = {
        "duplicateCatalogIds": duplicate_catalog,
        "duplicateDocumentationIds": sorted(card_id for card_id, count in Counter(
            value for value in docs_ids if value).items() if count > 1),
        "invalidCatalogEntries": sorted(
            f"{item['stableId']}|{docs.get(norm(item['officialCardId']), {}).get('name', '')}"
            for item in catalog if not norm(item["officialCardId"])),
        "catalogMissingFromDocumentation": sorted(catalog_set - set(docs)),
        "documentationMissingFromCatalog": sorted(set(docs) - catalog_set),
        "catalogMissingFromCoreDocumentation": sorted(catalog_set - set(core_docs)),
        "coreDocumentationMissingFromCatalog": sorted(set(core_docs) - catalog_set),
        "catalogMissingFromCompiledDatabase": sorted(catalog_set - set(database)),
        "compiledDatabaseMissingFromCatalog": sorted(set(database) - catalog_set),
        "catalogMissingFromTextDatabase": sorted(catalog_set - set(texts)),
        "catalogMissingFromVisualManifest": sorted(catalog_set - set(visuals)),
        "visualManifestMissingFromCatalog": sorted(set(visuals) - catalog_set),
        "missingRequiredScripts": [card["officialCardId"] for card in cards
                                   if card["scriptRequired"] and not card["scriptFound"]],
        "emptyRequiredScripts": [card["officialCardId"] for card in cards
                                 if card["scriptCompatibility"] == "EMPTY"],
        "missingScriptDependencies": sorted(
            f"{card['officialCardId']}|{dependency}" for card in cards
            for dependency in card["missingScriptDependencies"]),
        "missingArtwork": [card["officialCardId"] for card in cards
                           if card["officialCardId"] and not card["artworkFound"]],
        "deckCardsMissingFromCatalog": sorted(set(memberships) - catalog_set),
        "packCardsMissingFromCatalog": sorted(set(pack_memberships) - catalog_set),
    }
    status_counts = Counter(card["status"] for card in cards)
    priority_counts = Counter(card["priority"] for card in cards)
    statuses = {
        "inventariada": status_counts["INVENTARIADA"],
        "bloqueadaDados": status_counts["BLOQUEADA_DADOS"],
        "carrega": status_counts["CARREGA"], "testeParcial": 0,
        "passaCore": 0, "passaApresentacao": 0, "passaIa": 0,
        "passaOnline": 0, "concluida": 0,
        **{f"priorityP{index}": priority_counts[f"P{index}"] for index in range(6)},
    }
    official_scripts = ROOT / "Assets/StreamingAssets/Ygo/Scripts/official"
    custom_scripts = ROOT / "Assets/StreamingAssets/Ygo/CustomScripts"
    sources = {
        "cardCatalogEntries": len(catalog),
        "cardCatalogUniqueOfficialIds": len(catalog_set),
        "documentationCsvRows": len(docs_rows),
        "documentationCsvUniqueIds": len(docs),
        "coreDocumentationRows": len(core_rows),
        "coreDocumentationUniqueIds": len(core_docs),
        "compiledDatabaseCards": len(database), "textDatabaseCards": len(texts),
        "visualManifestCards": len(visuals),
        "officialScripts": len(list(official_scripts.rglob("*.lua"))),
        "customScripts": len(list(custom_scripts.rglob("*.lua"))),
        "streamingArtFiles": len(list((ROOT / "Assets/StreamingAssets/Ygo/Art").rglob("*.jpg"))),
        "shopDeckProducts": shop_count,
        "starterDecks": len({label.split(":", 2)[1] for label in starter_groups}),
        "curatedDeckArrays": len(arrays), "shopPacks": len(pack_file.get("packs", [])),
        "cardCatalogSha256": sha(ROOT / "Assets/Cards/CardCatalog.asset"),
        "documentationCsvSha256": sha(ROOT / "Documentation/CardCatalog.csv"),
        "coreDocumentationSha256": sha(ROOT / "Documentation/CoreCardCatalog.csv"),
        "cardsBinSha256": sha(ROOT / "Assets/StreamingAssets/Ygo/Data/cards.bin"),
        "cardTextsSha256": sha(ROOT / "Assets/StreamingAssets/Ygo/Data/card-texts.json"),
        "visualManifestSha256": sha(ROOT / "Assets/StreamingAssets/Ygo/Visual/card-visuals.json"),
        "officialScriptsTreeSha256": tree_sha(official_scripts, ".lua"),
        "customScriptsTreeSha256": tree_sha(custom_scripts, ".lua"),
        "windowsCorePluginSha256": sha(ROOT / "Assets/Plugins/Windows/x86_64/ocgcore.dll"),
        "androidCorePluginSha256": sha(ROOT / "Assets/Plugins/Android/arm64-v8a/libocgcore.so"),
    }
    by_id = {card["officialCardId"]: card for card in cards if card["officialCardId"]}
    selected: list[str] = []
    target_groups = [
        "shop:classic-blue-eyes-dragon-genesys-99",
        "shop:yugi-mutou-dark-magician-classic",
        "shop:classic-red-eyes-black-dragon",
        f"starter:{first_starter}" if first_starter else "",
    ]
    for group in target_groups:
        main_ids: list[str] = []
        extra_ids: list[str] = []
        all_ids: list[str] = []
        for label, values in all_groups.items():
            if label.startswith(group + ":"):
                for value in values:
                    if value not in all_ids:
                        all_ids.append(value)
                    lowered = label.casefold()
                    if lowered.endswith(":main") or lowered.endswith(":maindeck"):
                        if value not in main_ids:
                            main_ids.append(value)
                    elif lowered.endswith(":extra") or lowered.endswith(":extradeck"):
                        if value not in extra_ids:
                            extra_ids.append(value)
        ordered = main_ids[:8] + [value for value in extra_ids if value not in main_ids[:8]][:2]
        ordered += [value for value in all_ids if value not in ordered]
        added = 0
        for card_id in ordered:
            if card_id in by_id and card_id not in selected:
                selected.append(card_id)
                added += 1
                if added >= 10:
                    break
    for card in sorted(cards, key=lambda value: (value["priority"], value["officialCardId"])):
        if len(selected) >= 40:
            break
        if card["priority"] in ("P0", "P1") and card["officialCardId"] not in selected:
            selected.append(card["officialCardId"])
    batch = []
    for index, card_id in enumerate(selected[:40], 1):
        card = by_id[card_id]
        joined = " ".join(card["decks"])
        if "classic-blue-eyes-dragon-genesys-99" in joined:
            rationale = "Deck publicado Blue-Eyes; prioridade de valor jogavel."
        elif "yugi-mutou-dark-magician-classic" in joined:
            rationale = "Deck publicado Dark Magician; possui cobertura parcial existente."
        elif "classic-red-eyes-black-dragon" in joined:
            rationale = "Deck publicado Red-Eyes; linha central recomendada pelo plano."
        elif "starter:" in joined:
            rationale = "Carta de starter publicado; impacto direto no primeiro acesso."
        else:
            rationale = "Prioridade P0/P1 usada para completar o lote de 40 cartas."
        batch.append({
            "order": index, "officialCardId": card_id, "name": card["name"],
            "decks": card["decks"], "priority": card["priority"],
            "status": card["status"], "normalizedCondition": "PREENCHER_NA_FASE_3",
            "normalizedCost": "PREENCHER_NA_FASE_3",
            "normalizedTarget": "PREENCHER_NA_FASE_3",
            "normalizedOperation": "PREENCHER_NA_FASE_3",
            "normalizedDuration": "PREENCHER_NA_FASE_3",
            "normalizedLimit": "PREENCHER_NA_FASE_3",
            "proposedScenarios": card["applicableScenarios"], "rationale": rationale,
        })
    return {
        "schemaVersion": 1, "generatedUtc": generated,
        "projectVersion": identity["ProjectVersion"],
        "unityVersion": identity["UnityVersion"],
        "coreApiVersion": identity["CoreApiVersion"],
        "coreCommit": identity["CoreCommit"],
        "cardScriptsCommit": identity["CardScriptsCommit"],
        "babelCdbCommit": identity["BabelCdbCommit"],
        "gitBranch": branch, "gitHead": head, "sources": sources,
        "statuses": statuses, "divergences": divergence,
        "cards": cards, "firstBatch": batch,
    }


def csv_value(value: object) -> str:
    if isinstance(value, bool):
        return str(value).lower()
    if isinstance(value, list):
        return ";".join(str(item) for item in value)
    return str(value or "")


def write_outputs(snapshot: dict[str, object]) -> None:
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    (REPORT_DIR / "CardHealthMatrix.json").write_text(
        json.dumps(snapshot, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    cards = snapshot["cards"]
    with (REPORT_DIR / "CardHealthMatrix.csv").open(
            "w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(cards[0].keys()))
        writer.writeheader()
        for card in cards:
            writer.writerow({key: csv_value(value) for key, value in card.items()})
    source = snapshot["sources"]
    status = snapshot["statuses"]
    divergence = snapshot["divergences"]
    report = [
        "# Auditoria de cartas - Fases 0 e 1", "",
        "> Escopo deliberado: baseline, inventario e priorizacao. Nenhum efeito, regra do core ou comportamento funcional foi alterado.", "",
        "## Baseline reproduzivel", "",
        "| Item | Valor |", "|---|---|",
        f"| Gerado em UTC | {snapshot['generatedUtc']} |",
        f"| Projeto | {snapshot['projectVersion']} |",
        f"| Unity | {snapshot['unityVersion']} |",
        f"| Branch | {snapshot['gitBranch']} |", f"| HEAD | {snapshot['gitHead']} |",
        f"| API do core | {snapshot['coreApiVersion']} |",
        f"| ygopro-core | {snapshot['coreCommit']} |",
        f"| CardScripts | {snapshot['cardScriptsCommit']} |",
        f"| BabelCDB | {snapshot['babelCdbCommit']} |", "",
        "## Fontes encontradas", "",
        "| Fonte | Contagem |", "|---|---:|",
        f"| CardCatalog.asset | {source['cardCatalogEntries']} |",
        f"| Documentation/CardCatalog.csv | {source['documentationCsvRows']} |",
        f"| Documentation/CoreCardCatalog.csv | {source['coreDocumentationRows']} |",
        f"| cards.bin | {source['compiledDatabaseCards']} |",
        f"| card-texts.json | {source['textDatabaseCards']} |",
        f"| card-visuals.json | {source['visualManifestCards']} |",
        f"| scripts oficiais | {source['officialScripts']} |",
        f"| scripts customizados | {source['customScripts']} |", "",
        "Os hashes SHA-256 completos estao em `CardHealthMatrix.json`.", "",
        "## Estado da matriz", "", "| Status | Cartas |", "|---|---:|",
        f"| BLOQUEADA_DADOS | {status['bloqueadaDados']} |",
        f"| CARREGA | {status['carrega']} |", f"| TESTE_PARCIAL | {status['testeParcial']} |",
        f"| PASSA_CORE | {status['passaCore']} |",
        f"| PASSA_APRESENTACAO | {status['passaApresentacao']} |",
        f"| PASSA_IA | {status['passaIa']} |", f"| PASSA_ONLINE | {status['passaOnline']} |",
        f"| CONCLUIDA | {status['concluida']} |", "",
        "## Divergencias de integridade", "",
        "| Divergencia | Total | Amostra |", "|---|---:|---|",
    ]
    for name, values in divergence.items():
        report.append(f"| {name} | {len(values)} | {', '.join(values[:8])}{', ...' if len(values) > 8 else ''} |")
    report += [
        "", "## Arquitetura preservada", "",
        "- BabelCDB e os artefatos compilados continuam sendo a fonte de dados.",
        "- CardScripts/Lua continuam sendo a fonte de efeitos.",
        "- ygopro-core continua sendo o arbitro das regras.",
        "- C# permanece responsavel por catalogo, apresentacao, protocolo, IA e multiplayer.",
        "- A ferramenta nova fica isolada em `Assets/Editor/CardAudit` e `Tools/CardAudit`.",
        "", "## Limites desta evidencia", "",
        "`CARREGA` comprova apenas coerencia estrutural. Nenhuma carta foi declarada CONCLUIDA sem cenarios semanticos.",
        "A suite visual existente declara 23 lotes de 25 posicoes (575), abaixo das 961 entradas atuais do manifesto; essa lacuna deve ser removida nas fases seguintes.",
    ]
    (REPORT_DIR / "CardInventoryAuditReport.md").write_text(
        "\n".join(report) + "\n", encoding="utf-8")
    required = [card for card in cards if card["scriptRequired"]]
    compatibility = [
        "# Compatibilidade estatica de scripts", "",
        "> Resolve arquivos e dependencias `Duel.LoadScript`; nao comprova a semantica do efeito.", "",
        f"- Scripts obrigatorios: {len(required)}",
        f"- Resolvidos: {sum(card['scriptCompatibility'] == 'RESOLVED_STATIC' for card in required)}",
        f"- Ausentes: {sum(not card['scriptFound'] for card in required)}",
        f"- Vazios: {sum(card['scriptCompatibility'] == 'EMPTY' for card in required)}",
        f"- Com dependencia ausente: {sum(bool(card['missingScriptDependencies']) for card in required)}",
        "", "| Card ID | Nome | Resultado | Dependencias ausentes |", "|---|---|---|---|",
    ]
    for card in required:
        if card["scriptCompatibility"] != "RESOLVED_STATIC":
            compatibility.append(
                f"| {card['officialCardId']} | {card['name']} | {card['scriptCompatibility']} | {';'.join(card['missingScriptDependencies'])} |")
    (REPORT_DIR / "CardScriptCompatibilityReport.md").write_text(
        "\n".join(compatibility) + "\n", encoding="utf-8")
    batch = [
        "# Primeiro lote proposto", "",
        f"Lote de {len(snapshot['firstBatch'])} cartas. A aprovacao funcional depende da revisao e das fases semanticas.", "",
        "| # | Card ID | Nome | Decks | Prioridade | Estado | Motivo |", "|---:|---|---|---|---|---|---|",
    ]
    for card in snapshot["firstBatch"]:
        batch.append(f"| {card['order']} | {card['officialCardId']} | {card['name']} | {';'.join(card['decks'])} | {card['priority']} | {card['status']} | {card['rationale']} |")
    (REPORT_DIR / "FirstBatchPlan.md").write_text(
        "\n".join(batch) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true",
                        help="write Documentation/CardAudit outputs")
    args = parser.parse_args()
    snapshot = make_snapshot()
    summary = snapshot["sources"] | snapshot["statuses"]
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    if args.write:
        write_outputs(snapshot)
        print(f"Wrote {REPORT_DIR}")
    else:
        print("Preview only; pass --write to generate reports.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
