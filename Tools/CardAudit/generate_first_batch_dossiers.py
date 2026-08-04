#!/usr/bin/env python3
"""Generate reviewable dossiers and scenario specifications for batch one.

Preview is the default. The output is diagnostic: it never marks a card as
semantically approved and never edits effects, Core data, scenes, or assets.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path


import generate_phase01 as audit


ROOT = audit.ROOT
OUTPUT_JSON = ROOT / "Documentation/CardAudit/FirstBatchDossiers.json"
OUTPUT_MD = ROOT / "Documentation/CardAudit/FirstBatchDossiers.md"
ROLE_VALUES = {"CENTRAL", "EXTENSOR", "INTERACAO", "RESPOSTA", "EXTRA_DECK"}


def sentences(text: str) -> list[str]:
    return [part.strip() for part in re.split(r"(?<=[.!?])\s+", text or "")
            if part.strip()]


def matching(parts: list[str], patterns: tuple[str, ...]) -> list[str]:
    return [part for part in parts if any(
        pattern in part.casefold() for pattern in patterns)]


def first_clause(text: str) -> tuple[str, str]:
    first, separator, remainder = (text or "").partition(":")
    if separator and len(first) <= 280:
        return first.strip(), remainder.strip()
    return "NAO_EXPLICITA_NO_TEXTO", (text or "").strip()


def normalized_effect(text: str) -> dict[str, object]:
    parts = sentences(text)
    condition, operation = first_clause(text)
    costs = matching(parts, (
        "pague ", "descarte ", "ofereca", "ofereça", "bana ",
        "envie ", "devolva ", "remova ", "como custo",
    ))
    targets = matching(parts, (
        "escolha ", "alvo", "selecione ", "declare ", "revele ",
    ))
    durations = matching(parts, (
        "ate o final", "até o final", "pelo resto", "durante este turno",
        "durante o proximo", "durante o próximo", "enquanto ",
    ))
    limits = matching(parts, (
        "so pode usar", "só pode usar", "so pode ativar", "só pode ativar",
        "uma vez por turno", "apenas uma vez", "duas vezes por turno",
    ))
    branches = matching(parts, (
        "se isso acontecer", "e depois", "ou ", "ate ", "até ",
        "quando ", "se ", "caso ", "em vez disso", "opcional",
    ))
    return {
        "condition": condition,
        "cost": " | ".join(costs) if costs else "SEM_CUSTO_EXPLICITO_IDENTIFICADO",
        "target": " | ".join(targets) if targets else "SEM_ALVO_EXPLICITO_IDENTIFICADO",
        "operation": operation or "SEM_OPERACAO_TEXTUAL",
        "duration": " | ".join(durations) if durations else "SEM_DURACAO_EXPLICITA",
        "limit": " | ".join(limits) if limits else "SEM_LIMITE_EXPLICITO_IDENTIFICADO",
        "branches": branches,
        "normalizationStatus": "RASCUNHO_AUTOMATICO_PARA_REVISAO_SEMANTICA",
    }


def roles(card: dict[str, object], record: dict[str, object], text: str) -> list[str]:
    lowered = (text or "").casefold()
    result: list[str] = []
    card_type = int(record["type"])
    if card_type & (0x40 | 0x2000 | 0x800000 | 0x4000000):
        result.append("EXTRA_DECK")
    if any(term in lowered for term in (
            "invoque por invocacao-especial", "invoque por invocação-especial",
            "adicione", "compre ", "envie 1", "da sua mao", "da sua mão")):
        result.append("EXTENSOR")
    if any(term in lowered for term in (
            "destrua", "bane ", "devolva", "embaralhe", "ganhe o controle",
            "escolha 1 card", "escolha 1 monstro")):
        result.append("INTERACAO")
    if any(term in lowered for term in (
            "negue", "efeito rapido", "efeito rápido", "quando seu oponente",
            "seu oponente ativar", "em resposta")) or card_type & 0x4:
        result.append("RESPOSTA")
    name = str(card["name"]).casefold()
    if any(term in name for term in (
            "olhos azuis", "mago negro", "olhos vermelhos", "dragao negro",
            "dragão negro")) or not result:
        result.insert(0, "CENTRAL")
    return list(dict.fromkeys(result))


def dependencies(text: str, names_to_ids: dict[str, str]) -> list[dict[str, str]]:
    result: list[dict[str, str]] = []
    quoted = re.findall(r'["“]([^"”]{2,100})["”]', text or "")
    for name in quoted:
        card_id = names_to_ids.get(name.casefold(), "")
        item = {"name": name, "officialCardId": card_id}
        if item not in result:
            result.append(item)
    return result


def scenario_specs(card: dict[str, object], normalized: dict[str, object],
                   role_tags: list[str]) -> list[dict[str, object]]:
    card_id = str(card["officialCardId"])
    base = [
        {
            "id": f"{card_id}-positive-minimum",
            "family": "POSITIVO_MINIMO",
            "arrange": "Carta e dependencias em zonas legais; fase e recursos validos; seed fixa.",
            "act": "Oferecer a acao pelo Core, responder aos prompts com requestId valido e resolver.",
            "assertCore": "Opcao legal, custos, alvos, movimentos, flags e snapshot final correspondem ao dossie revisado.",
            "assertPresentation": "Prompt, destaque, zona e CardView refletem o snapshot autoritativo.",
        },
        {
            "id": f"{card_id}-negative-condition",
            "family": "NEGATIVO",
            "arrange": "Repetir o cenario com condicao, zona, fase, alvo ou recurso invalido.",
            "act": "Solicitar a mesma linha sem enviar resposta fora das opcoes do Core.",
            "assertCore": "A acao nao e oferecida ou e rejeitada sem consumir custo nem alterar estado.",
            "assertPresentation": "A Unity nao inventa acao, alvo ou mensagem de sucesso.",
        },
        {
            "id": f"{card_id}-boundary",
            "family": "FRONTEIRA",
            "arrange": "Executar com zero/um/maximo de alvos, zonas cheias ou recurso exato conforme aplicavel.",
            "act": "Resolver todos os ramos identificados no texto normalizado.",
            "assertCore": "Quantidades, opcionalidade e alvo perdido seguem o resultado autoritativo.",
            "assertPresentation": "Cancelar/confirmar e limites do prompt coincidem com o Core.",
        },
    ]
    if "RESPOSTA" in role_tags or normalized["branches"]:
        base.append({
            "id": f"{card_id}-chain-negation",
            "family": "CORRENTE",
            "arrange": "Criar corrente deterministica com prioridade alternada e resposta legal.",
            "act": "Responder, negar ou remover o alvo antes da resolucao.",
            "assertCore": "Ordem, chain links, alvo perdido e resolucao permanecem legais.",
            "assertPresentation": "Historico e prompts seguem a ordem do Core.",
        })
    if card["multiplayerResult"] == "NAO_EXECUTADO_NESTE_LOTE":
        base.append({
            "id": f"{card_id}-online-private-resync",
            "family": "ONLINE",
            "arrange": "Host autoritativo e dois seats; estado privado conhecido apenas pelo dono.",
            "act": "Cliente responde uma vez, repete a resposta e solicita resync apos resolucao.",
            "assertCore": "Uma unica aplicacao, ACK/requestId valido e hashes publicos convergentes.",
            "assertPresentation": "Nenhum dado privado vaza; snapshot recomposto converge nos dois lados.",
        })
    return base


def build() -> dict[str, object]:
    snapshot = audit.make_snapshot()
    if snapshot["statuses"]["bloqueadaDados"]:
        raise ValueError("First batch dossiers require zero BLOQUEADA_DADOS")
    database = audit.read_binary_cards()
    text_payload = json.loads(
        (ROOT / "Assets/StreamingAssets/Ygo/Data/card-texts.json")
        .read_text(encoding="utf-8-sig")
    )
    texts = {audit.norm(card["code"]): card for card in text_payload["cards"]}
    names_to_ids = {
        str(card.get("name") or "").casefold(): audit.norm(card["code"])
        for card in text_payload["cards"] if card.get("name")
    }
    matrix = {card["officialCardId"]: card for card in snapshot["cards"]}
    dossiers: list[dict[str, object]] = []
    for seed in snapshot["firstBatch"]:
        card_id = seed["officialCardId"]
        card = matrix[card_id]
        record = database[card_id]
        text = texts[card_id]
        effect = str(text.get("description") or "")
        normalized = normalized_effect(effect)
        role_tags = roles(card, record, effect)
        dossiers.append({
            "order": seed["order"],
            "officialCardId": card_id,
            "name": card["name"],
            "type": card["typeName"],
            "archetypeSetcodes": card["archetypeSetcodes"],
            "decks": card["decks"],
            "packIds": card["packs"],
            "primaryRole": role_tags[0],
            "roleTags": role_tags,
            "roleClassificationStatus": "HEURISTICA_PARA_REVISAO_DO_LOTE",
            "scriptPath": card["scriptPath"],
            "scriptSha256": card["scriptSha256"],
            "rawEffectText": effect,
            "normalizedEffect": normalized,
            "preconditions": [
                "DEFINIR_FASE_E_PRIORIDADE_NO_CENARIO",
                "DEFINIR_ZONAS_E_RECURSOS_NO_CENARIO",
                "CARREGAR_DEPENDENCIAS_LISTADAS",
                "USAR_SEED_DETERMINISTICA",
            ],
            "dependencies": dependencies(effect, names_to_ids),
            "applicableScenarios": scenario_specs(card, normalized, role_tags),
            "currentResult": {
                "status": "CARREGA",
                "core": "NAO_EXECUTADO",
                "presentation": "NAO_EXECUTADO",
                "ai": "NAO_EXECUTADO_SE_APLICAVEL",
                "online": "NAO_EXECUTADO_SE_APLICAVEL",
            },
            "hypothesis": "Nenhuma falha funcional reproduzida; camada responsavel ainda nao atribuida.",
            "correction": "NENHUMA_CORRECAO_FUNCIONAL_AUTORIZADA_ANTES_DO_TESTE_REPRODUZIVEL",
            "evidence": {
                "matrixGeneratedUtc": snapshot["generatedUtc"],
                "projectVersion": snapshot["projectVersion"],
                "coreCommit": snapshot["coreCommit"],
                "cardScriptsCommit": snapshot["cardScriptsCommit"],
                "babelCdbCommit": snapshot["babelCdbCommit"],
            },
            "dossierStatus": "PRONTO_PARA_REVISAO_SEMANTICA_E_IMPLEMENTACAO_DO_CENARIO",
        })
    role_counts = Counter(role for dossier in dossiers for role in dossier["roleTags"])
    scenario_counts = Counter(
        scenario["family"] for dossier in dossiers
        for scenario in dossier["applicableScenarios"]
    )
    return {
        "schemaVersion": 1,
        "generatedUtc": snapshot["generatedUtc"],
        "sourceMatrixSha256": audit.sha(
            ROOT / "Documentation/CardAudit/CardHealthMatrix.json"),
        "batchSize": len(dossiers),
        "status": "DOSSIER_PREPARATORIO_NAO_APROVA_SEMANTICA",
        "roleCounts": dict(sorted(role_counts.items())),
        "scenarioCounts": dict(sorted(scenario_counts.items())),
        "cards": dossiers,
    }


def markdown(payload: dict[str, object]) -> str:
    lines = [
        "# Dossies do primeiro lote", "",
        f"Lote: `{payload['batchSize']}` cartas.", "",
        "> Documento preparatorio. Nenhuma carta foi aprovada semanticamente; "
        "os campos normalizados e papeis heuristicos devem ser revisados antes "
        "da criacao do teste vermelho.", "",
        "## Cobertura planejada", "",
        "### Papeis", "",
    ]
    lines += [f"- {name}: {count}" for name, count in payload["roleCounts"].items()]
    lines += ["", "### Cenarios", ""]
    lines += [f"- {name}: {count}" for name, count in payload["scenarioCounts"].items()]
    lines += [
        "", "## Cartas", "",
        "| # | ID | Nome | Papel primario | Papeis | Cenarios | Estado |",
        "|---:|---|---|---|---|---:|---|",
    ]
    for dossier in payload["cards"]:
        lines.append(
            f"| {dossier['order']} | {dossier['officialCardId']} | "
            f"{str(dossier['name']).replace('|', '/')} | {dossier['primaryRole']} | "
            f"{';'.join(dossier['roleTags'])} | "
            f"{len(dossier['applicableScenarios'])} | {dossier['dossierStatus']} |"
        )
    lines += [
        "", "## Gate seguinte", "",
        "Revisar a normalizacao e selecionar cenarios representativos por familia. "
        "Somente depois criar reproduzidores inicialmente vermelhos; uma falha "
        "deve seguir fontes -> script -> Core -> resposta -> projector -> IA -> online.",
    ]
    return "\n".join(lines) + "\n"


def validate(payload: dict[str, object]) -> None:
    cards = payload["cards"]
    if len(cards) < 25 or len(cards) > 50:
        raise ValueError("Batch must contain 25-50 cards")
    ids = [card["officialCardId"] for card in cards]
    if len(ids) != len(set(ids)):
        raise ValueError("Dossier contains duplicate cards")
    for card in cards:
        if card["primaryRole"] not in ROLE_VALUES:
            raise ValueError("Invalid role: " + card["primaryRole"])
        if not card["rawEffectText"]:
            raise ValueError("Missing official text: " + card["officialCardId"])
        families = {scenario["family"] for scenario in card["applicableScenarios"]}
        if not {"POSITIVO_MINIMO", "NEGATIVO", "FRONTEIRA"}.issubset(families):
            raise ValueError("Required scenarios absent: " + card["officialCardId"])
        if card["currentResult"]["status"] != "CARREGA":
            raise ValueError("Unexpected promoted status: " + card["officialCardId"])


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    payload = build()
    validate(payload)
    print(json.dumps({
        "batchSize": payload["batchSize"],
        "roleCounts": payload["roleCounts"],
        "scenarioCounts": payload["scenarioCounts"],
        "status": payload["status"],
    }, ensure_ascii=False, indent=2))
    if not args.write:
        print("Preview only; pass --write to generate dossiers.")
        return 0
    OUTPUT_JSON.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    OUTPUT_MD.write_text(markdown(payload), encoding="utf-8")
    print("ARCANE_FIRST_BATCH_DOSSIERS_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
