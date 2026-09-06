#!/usr/bin/env python3
"""Build a deterministic pt-BR text overlay for one numeric allcards slice.

Published Portuguese text and curated manual overrides always win. Cards that
have no Portuguese publication are translated by an authoring-only local model;
only the resulting curated snapshot ships with the game.
"""

from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path

from translate_unofficial_card_texts import (
    HEADING_PATTERN,
    localize_heading,
    needs_repair,
    normalize_terms,
    translate_sentences,
)


# Residual sentences that require rule-aware wording after machine translation.
BATCH_CORRECTIONS = {
    28958464: (
        "Escolha 1 monstro em qualquer Cemitério; Invoque-o por "
        "Invocação-Especial no seu campo, mas, pelo resto deste turno, ele não "
        "pode atacar e nenhum duelista pode ativar seus efeitos. Você só pode "
        'ativar 1 "Spell Card \\"Monster Reborn\\"" por turno.'
    ),
    29155212: (
        'Ganha 100 de ATK/DEF enquanto "Castelo das Ilusões Sombrias" estiver '
        'no campo. Além disso, durante sua Fase de Apoio, se "Castelo das '
        'Ilusões Sombrias" estiver no campo: este card ganha 100 de ATK/DEF. '
        'Este efeito é ativado uma vez por turno durante quatro das suas Fases '
        'de Apoio (contando esta como a primeira). "Castelo das Ilusões '
        'Sombrias" deve estar no campo para ativar e resolver este efeito. Se '
        '"Castelo das Ilusões Sombrias" não estiver no campo, esse ganho de '
        'ATK/DEF desaparece e este efeito é reiniciado.'
    ),
    32314730: (
        'Durante cada uma das suas Fases Finais, destrua este card, a menos que '
        'você envie 1 "Núcleo de Ferro de Koa\'ki Meiru" da sua mão para o '
        'Cemitério ou revele 1 monstro Besta-Guerreira na sua mão. Se este card '
        'destruir um monstro do oponente em batalha: você pode adicionar 1 card '
        '"Koa\'ki Meiru" do seu Cemitério à sua mão.'
    ),
    28674153: (
        'Invocada por Invocação-Especial pelo efeito de "Radian, o Kaiju '
        'Multidimensional". Não pode ser usada como Matéria Sincro.'
    ),
    30581601: (
        '1 monstro Besta de LUZ de Nível 4 ou menos\n'
        'Se este card for Invocado por Invocação-Especial: você pode colocar '
        '1 Magia de Campo "Yummy" da sua mão ou do Deck, com a face para cima '
        'no seu campo e, além disso, você não pode Invocar por Invocação-Link '
        'Monstros Link-3 ou mais pelo resto deste turno. Você só pode usar este '
        'efeito de "Yummy★Snatchy" uma vez por turno. Uma vez por Corrente, '
        'durante a Fase Principal ou a Fase de Batalha do seu oponente (Efeito '
        'Rápido): você pode pagar 100 LP; imediatamente depois que este efeito '
        'resolver, Invoque por Invocação-Sincro usando monstros que você controla '
        'como matéria, incluindo um monstro "Yummy".'
    ),
    30945251: (
        'Escolha 1 card com a face para cima no campo; destrua-o e, além disso, '
        'pelo resto deste turno, você não pode Invocar por Invocação-Especial '
        'Monstros de Efeito, exceto da mão. Se um ou mais cards forem destruídos '
        'pelo efeito de um card "Blitzclique" seu, enquanto este card estiver no '
        'seu Cemitério (exceto durante a Etapa de Dano): você pode adicionar este '
        'card à sua mão. Você só pode usar este efeito de "Blitzclique - '
        'Overvoltage" uma vez por turno.'
    ),
    31386180: (
        '2 monstros de Nível 5\n'
        'Os efeitos deste card só podem ser aplicados/resolvidos enquanto ele '
        'tiver Matéria Xyz. Este card não pode ser destruído por efeitos de card. '
        'No final da Fase de Batalha, se este card atacou ou foi atacado: escolha '
        '1 card que seu oponente controla; destrua o alvo. Durante cada uma das '
        'suas Fases Finais: desassocie 1 matéria deste card.'
    ),
    31969219: (
        'Escolha 1 card com a face para cima que seu oponente controla; aplique '
        'este efeito de acordo com o número de cards no Cemitério dele com o '
        'mesmo nome desse card com a face para cima.\n'
        '● 1: Destrua-o.\n'
        '● 2: Bana-o.\n'
        '● 3+: Bana-o e também todos os cards com esse nome do campo e do '
        'Cemitério do seu oponente, com a face para baixo.'
    ),
    32061192: (
        'Monstros Link "Maliss" que apontarem para este card não podem ser '
        'destruídos por efeitos de card. Você só pode usar cada um dos seguintes '
        'efeitos de "Maliss <P> Dormouse" uma vez por turno. Durante sua Fase '
        'Principal: você pode ativar este efeito; bana 1 monstro "Maliss" do seu '
        'Deck e, além disso, pelo resto deste turno, os monstros "Maliss" que '
        'você controla ganham 600 de ATK. Se este card for banido: você pode pagar '
        '300 LP; Invoque-o por Invocação-Especial e, além disso, você não pode '
        'Invocar por Invocação-Especial do Deck Adicional pelo resto deste turno, '
        'exceto Monstros Link.'
    ),
    32549749: (
        'Ative 1 dos seguintes efeitos.\n'
        '● Escolha 1 monstro com a face para cima que você controla; ele ganha '
        '800 de ATK até o final deste turno.\n'
        '● Escolha 1 Magia de Equipamento em qualquer Cemitério; Baixe-a no seu '
        'campo ou equipe-a a 1 monstro apropriado que você controla.\n'
        'Você só pode ativar 1 "Arms Regeneration" por turno.'
    ),
    33744268: (
        'Se este card for Invocado por Invocação-Especial: você pode equipar a '
        'este card 1 dos seus monstros Máquina de LUZ de Nível 4 banidos como uma '
        'Magia de Equipamento e, além disso, você não pode Invocar por '
        'Invocação-Especial do Deck Adicional pelo resto deste turno, exceto '
        'monstros de LUZ. Uma vez por turno, você pode: escolher 1 monstro '
        'Máquina que você controla; equipe este card ao alvo, OU: desequipe este '
        'card e Invoque-o por Invocação-Especial. Se o monstro equipado seria '
        'destruído em batalha ou por um efeito de card, destrua este card em vez '
        'disso.'
    ),
    33900648: (
        'Durante cada uma das suas Fases Finais, pague 500 LP ou destrua este '
        'card. Cada duelista recebe os seguintes efeitos, dependendo dos '
        'Atributos dos monstros que controla.\n'
        '● LUZ: Jogue com sua mão sempre revelada.\n'
        '● TREVAS: Se você controlar 2 ou mais monstros, não poderá declarar '
        'um ataque.\n'
        '● TERRA: Durante sua Fase de Apoio: escolha 1 monstro com a face para '
        'cima em Posição de Defesa que você controla; destrua o alvo.\n'
        '● ÁGUA: Durante sua Fase Final: descarte 1 card.\n'
        '● FOGO: Durante sua Fase Final: sofra 1000 de dano.\n'
        '● VENTO: Você deve pagar 500 LP para ativar um Card de Magia.'
    ),
    34088136: (
        'Se este card foi Invocado por Invocação-Especial pelo efeito de '
        '"Inseto Supremo LV1", enquanto ele permanecer com a face para cima '
        'no campo, todos os monstros do seu oponente perdem 300 de ATK. Durante '
        'sua Fase de Apoio: você pode enviar este card com a face para cima para '
        'o Cemitério; Invoque por Invocação-Especial 1 "Inseto Supremo LV5" '
        'da sua mão ou do Deck. (Você não pode ativar este efeito no turno em '
        'que este card for Invocado por Invocação-Normal ou Especial, ou '
        'virado com a face para cima.)'
    ),
    34830502: (
        'Se este card foi Invocado por Invocação-Especial pelo efeito de '
        '"Inseto Supremo LV3", enquanto ele permanecer com a face para cima '
        'no campo, todos os monstros do seu oponente perdem 500 de ATK. Durante '
        'sua Fase de Apoio: você pode enviar este card com a face para cima para '
        'o Cemitério; Invoque por Invocação-Especial 1 "Inseto Supremo LV7" '
        'da sua mão ou do Deck. (Você não pode ativar este efeito no turno em '
        'que este card for Invocado por Invocação-Normal ou Especial, ou '
        'virado com a face para cima.)'
    ),
    35316708: (
        'Pule a próxima Fase de Compra do seu oponente.'
    ),
    35756798: (
        'Durante seu turno, se um ou mais Monstros Sincro foram enviados para '
        'o seu Cemitério neste turno: escolha 1 Monstro Sincro que você '
        'controla; ele pode realizar um segundo ataque durante cada Fase de '
        'Batalha neste turno e, além disso, se você ativou este card escolhendo '
        'como alvo um Monstro Sincro que tenha "Warrior", "Synchron" ou '
        '"Stardust" em seu nome original, você pode fazê-lo ganhar ATK igual '
        'ao ATK de 1 Monstro Sincro no seu Cemitério. Você só pode ativar 1 '
        '"Final Cross" por turno.'
    ),
    35762283: (
        'Durante sua Fase de Compra, quando você comprar um ou mais Monstros '
        'Normais: você pode revelar 1 deles; compre mais 1 card.'
    ),
    35778533: (
        'Se você controlar um monstro Ilusão ou Mago de Nível 6 ou mais: '
        'escolha 1 card que seu oponente controla; destrua-o. Se este card for '
        'enviado para o Cemitério para ativar um efeito de monstro: você pode '
        'Baixar este card. Você só pode usar cada efeito de "Beware the White '
        'Forest" uma vez por turno.'
    ),
    35781051: (
        'Quando este card for Invocado: lance uma moeda.\n'
        '● Cara: Sempre que seu oponente Invocar um monstro por '
        'Invocação-Normal ou Baixá-lo, você pode Invocar por '
        'Invocação-Especial 1 monstro "Força Arcana" da sua mão.\n'
        '● Coroa: Sempre que seu oponente Invocar um monstro por '
        'Invocação-Normal ou Baixá-lo, envie 1 card da sua mão para o '
        'Cemitério.'
    ),
    35798491: (
        'Durante cada uma das suas Fases de Apoio, pague 500 LP (isto não é '
        'opcional). Quando um Card de Monstro "Arquidemônio" no seu campo for '
        'escolhido como alvo pelo efeito de um card controlado pelo seu '
        'oponente, ao resolver o efeito, lance um dado de seis faces e, se o '
        'resultado for 1, 3 ou 6, negue o efeito e destrua o card do seu '
        'oponente.'
    ),
    35975813: (
        'Não pode ser Invocado por Invocação-Normal ou Invocação-Virar, '
        'a menos que você tenha um Card de Monstro "Arquidemônio" no seu '
        'campo. Durante cada uma das suas Fases de Apoio, pague 800 LP (isto '
        'não é opcional). Quando este card for escolhido como alvo pelo efeito '
        'de um card controlado pelo seu oponente, ao resolver o efeito, lance '
        'um dado de seis faces e, se o resultado for 2 ou 5, negue o efeito e '
        'destrua o card do seu oponente. Os efeitos de um Monstro de Efeito '
        'destruído por este card em batalha são negados.'
    ),
    36270527: (
        'Você pode banir este card da sua mão; adicione 1 monstro "Ars Magna" '
        'não-Guerreiro do seu Deck à sua mão e, além disso, o ATK original '
        'dos Monstros Link "Power Patron" que você controla se torna três '
        'vezes maior pelo resto deste turno. Se um ou mais Monstros de Fusão '
        'e/ou Link forem Invocados por Invocação-Especial enquanto este card '
        'estiver banido (exceto durante a Etapa de Dano): você pode Invocar '
        'este card por Invocação-Especial. Se você controlar um Monstro Link '
        '"Power Patron": você pode escolher 1 monstro no campo; bana-o. Você '
        'só pode usar cada efeito de "Ars Magna of Infinity and Finity" uma vez '
        'por turno.'
    ),
    37442336: (
        '1 Monstro Sincro Regulador + 2+ Monstros Sincro não-Reguladores\n'
        'Deve primeiro ser Invocado por Invocação-Sincro com as matérias acima. '
        'Uma vez por turno: você pode escolher cards com a face para cima no '
        'campo, até o número de monstros usados como Matéria Sincro para este '
        'card +1; negue seus efeitos. A ativação deste efeito e seu efeito não '
        'podem ser negados. (Efeito Rápido): você pode banir este card Invocado '
        'por Invocação-Sincro; Invoque por Invocação-Especial 1 Monstro Sincro '
        'Dragão do seu Deck Adicional que exija 2+ Monstros Sincro '
        'não-Reguladores como matéria. (Isso é considerado uma '
        'Invocação-Sincro.)'
    ),
    38643567: (
        'Escolha 1 monstro "Inzektor" com a face para cima que você controla; '
        'equipe este card ao alvo. Ele ganha 500 de ATK/DEF. Quando exatamente '
        '1 monstro "Inzektor" que você controla for escolhido como alvo por um '
        'efeito de card (exceto durante a Etapa de Dano): você pode enviar este '
        'Card de Equipamento para o Cemitério; negue o efeito.'
    ),
    39440937: (
        'Mude todos os Monstros Sincro com a face para cima no campo para a '
        'Posição de Defesa. Durante a Fase Final, devolva todos os Monstros '
        'Sincro com a face para cima no campo para o Deck Adicional.'
    ),
}


def load_api_cards(path: Path) -> list[dict]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(payload, dict):
        return list(payload.get("data", payload.get("cards", [])))
    return list(payload)


def ensure_pendulum_sections(value: str) -> str:
    value = HEADING_PATTERN.sub(
        lambda match: localize_heading(match.group(0)), value
    )
    pendulum = "[ Efeito de Pêndulo ]" in value
    monster = "[ Efeito de Monstro ]" in value
    if not pendulum:
        value = (
            "[ Efeito de Pêndulo ]\n"
            "Este card não possui Efeito de Pêndulo.\n\n"
            "[ Efeito de Monstro ]\n" + value
        )
    elif not monster:
        value += (
            "\n\n[ Efeito de Monstro ]\n"
            "Este card não possui Efeito de Monstro."
        )
    value = re.sub(r"\n{3,}", "\n\n", value)
    return normalize_terms(value)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", required=True, type=Path)
    parser.add_argument("--start-index", required=True, type=int)
    parser.add_argument("--count", required=True, type=int)
    parser.add_argument("--batch-id", required=True)
    parser.add_argument("--manual", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--model", type=Path)
    parser.add_argument("--translate-missing", action="store_true")
    args = parser.parse_args()

    images = sorted(
        args.source_root.joinpath("images").glob("*.jpg"),
        key=lambda path: int(path.stem),
    )
    start = args.start_index - 1
    selected = images[start:start + args.count]
    if len(selected) != args.count:
        raise ValueError("Numeric slice does not contain the requested count")

    english = load_api_cards(args.source_root / "metadata/cards-en.json")
    portuguese = load_api_cards(args.source_root / "metadata/cards-pt.json")
    en_by_id = {int(card["id"]): card for card in english}
    pt_by_id = {int(card["id"]): card for card in portuguese}
    owner_by_image = {
        int(image["id"]): card
        for card in english
        for image in card.get("card_images", [])
    }
    manual_payload = json.loads(args.manual.read_text(encoding="utf-8"))
    manual_by_code = {
        int(card["code"]): card for card in manual_payload.get("cards", [])
    }

    owners_to_translate: dict[int, dict] = {}
    resolved: dict[int, dict] = {}
    skipped: list[int] = []
    for image_path in selected:
        code = int(image_path.stem)
        owner = en_by_id.get(code) or owner_by_image.get(code)
        if owner is None:
            skipped.append(code)
            continue
        owner_code = int(owner["id"])
        manual = manual_by_code.get(code) or manual_by_code.get(owner_code)
        official = pt_by_id.get(owner_code)
        if manual:
            resolved[code] = {
                "code": code,
                "name": str(manual.get("name") or owner.get("name", "")),
                "description": str(manual.get("description", "")),
                "strings": list(manual.get("strings", [])),
                "source": "curated_manual",
            }
        elif official:
            resolved[code] = {
                "code": code,
                "name": str(official.get("name") or owner.get("name", "")),
                "description": str(official.get("desc", "")),
                "strings": [],
                "source": "official_archive_pt",
            }
        else:
            owners_to_translate[owner_code] = owner

    translated_owners: dict[int, str] = {}
    processor = None
    translator = None
    if owners_to_translate and not args.translate_missing:
        raise ValueError(
            f"{len(owners_to_translate)} owner cards still require translation; "
            "rerun with --translate-missing"
        )
    if owners_to_translate:
        if args.model is None:
            raise ValueError("--model is required when translations are missing")
        import ctranslate2
        import sentencepiece as spm

        processor = spm.SentencePieceProcessor(
            model_file=str(args.model / "sentencepiece.model")
        )
        translator = ctranslate2.Translator(
            str(args.model / "model"), device="cpu"
        )
        translated_owners = translate_sentences(
            {
                code: str(owner.get("desc", ""))
                for code, owner in owners_to_translate.items()
            },
            processor,
            translator,
            force=True,
        )
        translated_owners.update({
            code: description
            for code, description in BATCH_CORRECTIONS.items()
            if code in owners_to_translate
        })
        print(
            f"TRANSLATION_PROGRESS {len(translated_owners)}/"
            f"{len(owners_to_translate)}",
            flush=True,
        )

    for image_path in selected:
        code = int(image_path.stem)
        if code in resolved:
            continue
        owner = en_by_id.get(code) or owner_by_image.get(code)
        if owner is None:
            continue
        owner_code = int(owner["id"])
        description = translated_owners[owner_code]
        resolved[code] = {
            "code": code,
            # Unpublished names remain canonical; only rules text is translated.
            "name": str(owner.get("name", "")),
            "description": description,
            "strings": [],
            "source": "authoring_machine_pt_br",
        }

    repairs = {
        code: str(card["description"])
        for code, card in resolved.items()
        if card["source"] != "authoring_machine_pt_br"
        and needs_repair(str(card["description"]))
    }
    if repairs:
        if args.model is None:
            raise ValueError("--model is required to repair mixed-language text")
        if processor is None or translator is None:
            import ctranslate2
            import sentencepiece as spm

            processor = spm.SentencePieceProcessor(
                model_file=str(args.model / "sentencepiece.model")
            )
            translator = ctranslate2.Translator(
                str(args.model / "model"), device="cpu"
            )
        repaired = translate_sentences(
            repairs, processor, translator, force=False
        )
        for code, description in repaired.items():
            resolved[code]["description"] = description
            resolved[code]["source"] += "+authoring_repair"

    for image_path in selected:
        code = int(image_path.stem)
        owner = en_by_id.get(code) or owner_by_image.get(code)
        if owner is None:
            continue
        owner_code = int(owner["id"])
        if owner_code in BATCH_CORRECTIONS:
            resolved[code]["description"] = BATCH_CORRECTIONS[owner_code]
            if "curated_rule_correction" not in resolved[code]["source"]:
                resolved[code]["source"] += "+curated_rule_correction"
        if "pendulum" in str(owner.get("frameType", "")).casefold() \
                or "pendulum" in str(owner.get("type", "")).casefold():
            resolved[code]["description"] = ensure_pendulum_sections(
                str(resolved[code]["description"])
            )

    invalid = sorted(
        code for code, card in resolved.items()
        if card["description"]
        and needs_repair(str(card["description"]))
    )
    if invalid:
        raise ValueError(
            "English fragments remain after translation: "
            + ", ".join(str(code) for code in invalid)
        )

    payload = {
        "schemaVersion": 1,
        "language": "pt-BR",
        "batchId": args.batch_id,
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "selectedCount": len(selected),
        "translatedOwnerCount": len(translated_owners),
        "skippedMissingMetadata": skipped,
        "cards": [resolved[code] for code in sorted(resolved)],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    counts: dict[str, int] = {}
    for card in resolved.values():
        source = str(card["source"])
        counts[source] = counts.get(source, 0) + 1
    print(
        "ARCANE_BATCH_TRANSLATION_OK "
        f"selected={len(selected)} localized={len(resolved)} "
        f"translatedOwners={len(translated_owners)} skipped={len(skipped)} "
        f"sources={json.dumps(counts, sort_keys=True)}"
    )


if __name__ == "__main__":
    main()
