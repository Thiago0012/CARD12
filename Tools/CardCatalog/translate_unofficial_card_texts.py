#!/usr/bin/env python3
"""Translate catalog texts that have no official Portuguese publication.

This is an authoring-time fallback.  The model and Python dependencies are
temporary tooling; only the resulting curated JSON overrides ship with the
game.  Existing Portuguese and official Konami overrides always win.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

import ctranslate2
import sentencepiece as spm


ENGLISH_WORDS = (
    " the ", " this ", " that ", " you ", " your ", " opponent ",
    " monster ", " spell ", " trap ", " deck ", " hand ", " field ",
    " summon ", " turn ", " target ", " destroy ", " banish ",
    " once ", " when ", " during ", " cannot ", " if ", " from ",
)
PORTUGUESE_WORDS = (
    " você ", " este ", " esta ", " esse ", " essa ", " seu ",
    " sua ", " oponente ", " monstro ", " magia ", " armadilha ",
    " deck ", " mão ", " campo ", " invoque ", " turno ",
    " escolha ", " destrua ", " bana ", " uma vez ", " quando ",
    " durante ", " não ", " se ", " card ", " cemitério ",
)
RUNTIME_ENGLISH_MARKERS = (
    " once per ", " you can ", " your opponent ", " this card ",
    " this turn ", " target 1 ", " special summon", " normal summon",
    " from your ", " to your hand", " on the field",
    " in your graveyard", " destroy that", " banish that",
    " when this ", " if this ", " during your ", " with the effect ",
    " draw ", " tribute ", " face-up", " face-down",
    " end phase", " standby phase", " main phase", " battle phase",
    " damage step", " damage calculation", " life points",
    " quick effect", " fusion monster", " ritual monster",
    " synchro monster", " xyz monster", " link monster",
    " psychic-type", " spellcaster-type", " reptile-type",
    " insect-type", " zombie-type", " equip card", " trap cards",
    " removed from play", " related cards", " destroyed ",
)
HEADING_PATTERN = re.compile(
    r"(\[\s*(?:Pendulum Effect|Monster Effect|Spell Effect|Trap Effect|"
    r"Efeito de Pêndulo|Efeito de Monstro|Efeito de Magia|"
    r"Efeito de Armadilha)\s*\])",
    flags=re.IGNORECASE,
)
SENTENCE_PATTERN = re.compile(r"(?<=[.!?])\s+(?=[A-Z\[●])")

# A tradução automática é apenas o último recurso para cards sem publicação
# oficial em português. Casos que exigem interpretação de regras ficam aqui,
# para que novas compilações do catálogo preservem a redação revisada.
AUTHORING_CORRECTIONS = {
    62121: (
        "VIRE: Todos os monstros Zumbi no campo ganham 200 de ATK/DEF. "
        "Enquanto este card permanecer com a face para cima no campo, todos "
        "os monstros Zumbi continuam a ganhar 200 de ATK/DEF durante cada "
        "Fase de Apoio. Este efeito continua até o seu quarto turno depois "
        "que este card for ativado."
    ),
    168917: (
        'Uma vez por turno: você pode escolher 1 Card de Monstro "Vylon" '
        'que você controla que seja um Card de Equipamento; Invoque o alvo '
        'por Invocação-Especial em Posição de Defesa com a face para cima. '
        'Bana-o quando ele deixar o campo.'
    ),
    218704: (
        "Não pode ser Invocado por Invocação-Normal/Baixado. Deve primeiro "
        "ser Invocado por Invocação-Especial (da sua mão) ao banir 2 monstros "
        "de ÁGUA do seu Cemitério. Se este card destruir um monstro do seu "
        "oponente em batalha: pule a próxima Fase de Compra dele."
    ),
    967928: (
        "Quando seu oponente tiver exatamente 4 cards na mão: escolha e "
        "aplique 1 destes efeitos;\n"
        "● Seu oponente não pode comprar durante a próxima Fase de Compra dele.\n"
        "● Seu oponente não pode ativar Cards de Magia/Armadilha neste turno."
    ),
    847217: (
        "Os efeitos do monstro equipado são negados.\n"
        "Se você tiver um card na sua Zona de Campo: você pode escolher 1 "
        "Monstro de Efeito que você controla; até o final do turno do seu "
        "oponente, esse monstro ganha este efeito.\n"
        'Você só pode usar o efeito anterior de "Orichalcos Sword of Sealing" '
        "uma vez por turno.\n"
        "● Uma vez por turno (Efeito Rápido): você pode enviar 1 card da sua "
        "mão para o Cemitério e, depois, escolher 1 card com a face para cima "
        "no campo; destrua-o."
    ),
    1186447: (
        "[ Efeito de Pêndulo ]\n"
        "Quando um Card de Magia ou efeito ativado pelo seu oponente resolver, "
        'se você controlar um Monstro Pêndulo "Magician" ou um monstro '
        '"Odd-Eyes": você pode negar esse efeito e, depois, destruir este '
        "card. Você só pode usar este efeito de "
        '"Horoscope Sorcerer, the Stargazer Magician" uma vez por turno.\n\n'
        "[ Efeito de Monstro ]\n"
        'Você pode descartar 1 outro Monstro Pêndulo "Magician", monstro '
        '"Performapal" ou monstro "Odd-Eyes"; Invoque este card por '
        "Invocação-Especial da sua mão.\n"
        "Se este card for Invocado por Invocação-Especial: você pode adicionar "
        "1 Monstro Pêndulo com 2500 de ATK do seu Deck à sua mão.\n"
        "Você pode escolher 1 Card de Monstro Pêndulo que você controla; "
        "destrua-o e, depois, você pode adicionar 1 Monstro Pêndulo com a face "
        "para cima do seu Deck Adicional à sua mão.\n"
        "Você só pode usar cada efeito de "
        '"Horoscope Sorcerer, the Stargazer Magician" uma vez por turno.'
    ),
    1287123: (
        "Quando for ativado um efeito de Monstro de Efeito que destruiria 1 "
        "ou mais Cards de Magia/Armadilha no campo: você pode enviar 1 card "
        "da sua mão para o Cemitério; negue a ativação e, se isso acontecer, "
        "destrua esse card."
    ),
    1918087: (
        "Ative apenas enquanto seu oponente tiver 3000 ou menos LP. Durante "
        "cada Fase de Apoio dele: cause 500 de dano a ele."
    ),
    2203790: (
        "1 Regulador + 1+ monstros não-Reguladores\n"
        "Quando este card for Invocado por Invocação-Sincro: você pode "
        "escolher até 3 Cards de Magia/Armadilha no campo; destrua-os."
    ),
    3682106: (
        "Destrua 1 card com a face para cima no campo que tenha um efeito que "
        "negue os efeitos de Cards de Armadilha."
    ),
    3972721: (
        "Ative apenas se você destruiu neste turno um Monstro Sincro com a "
        "face para cima que seu oponente controlava, em batalha ou por efeito "
        "de card: compre 2 cards."
    ),
    4993187: (
        "2+ Monstros de Efeito\n"
        "Quando seu oponente ativar um efeito de monstro no campo ou no "
        "Cemitério (Efeito Rápido): você pode banir este card até a Fase "
        "Final; negue esse efeito e, se isso acontecer, bana o card ativado.\n"
        "Durante a Fase Principal do seu oponente (Efeito Rápido): "
        "imediatamente depois que este efeito resolver, Invoque por "
        "Invocação-Link 1 Monstro Link e, se usar este card que você controla "
        "como matéria para essa Invocação-Link, você também pode usar 1 "
        "monstro Link-2 ou menos que seu oponente controla. Nenhum duelista "
        "pode ativar os efeitos de Monstros Link em resposta à ativação deste "
        'efeito. Você só pode usar cada efeito de "W:P Fancy Ball" uma vez '
        "por turno."
    ),
    5592689: (
        "Durante sua Fase Final, se você não controlar Cards de "
        "Magia/Armadilha: você pode Invocar este card por Invocação-Especial "
        "do seu Cemitério em Posição de Ataque com a face para cima. Durante "
        "cada Fase de Apoio: o controlador deste card sofre 1000 de dano."
    ),
    6137095: (
        "Pague 500 LP; negue a ativação de um Card de Armadilha e devolva-o "
        "à sua posição original."
    ),
    7180418: (
        "Este card não pode atacar no turno em que for Invocado por "
        "Invocação-Normal, Invocação-Virar ou Invocação-Especial. Durante "
        "cada Fase de Apoio sua e do seu oponente: remova 1 Marcador de Magia "
        "do seu campo ou destrua este card."
    ),
    7165085: (
        "Escolha 1 card Baixado na Zona de Magias & Armadilhas; revele o alvo, "
        "force sua ativação se ele for um Card de Armadilha e, depois, negue "
        "seu efeito se o momento de ativação estiver incorreto e, se isso "
        "acontecer, destrua-o. (Se ele não for um Card de Armadilha, devolva-o "
        "com a face para baixo.) Quando este card resolver, embaralhe-o no "
        "Deck em vez de enviá-lo ao Cemitério."
    ),
    8581705: (
        "Durante cada uma das suas Fases de Apoio, pague 500 LP ou destrua "
        "este card. Quando este card for escolhido como alvo por um efeito de "
        "card do seu oponente: ao resolver esse efeito, lance um dado de seis "
        "faces e, se o resultado for 2 ou 5, negue o efeito e destrua esse "
        "card do oponente. Durante cada uma das suas Fases de Apoio: escolha "
        "1 monstro \"Arquidemônio\"; ele ganha 1000 de ATK até a Fase Final."
    ),
    9603356: (
        "Durante cada uma das suas Fases de Apoio, pague 900 LP ou destrua "
        "este card. Quando este card for escolhido como alvo por um efeito de "
        "card do seu oponente: ao resolver esse efeito, lance um dado de seis "
        "faces e, se o resultado for 3, negue o efeito e destrua esse card do "
        "oponente. Qualquer dano de batalha que este card causar ao seu "
        "oponente é diminuído pela metade."
    ),
    10000030: (
        "2 monstros Mago de Nível 6\n"
        "Uma vez por turno: você pode desassociar 1 matéria deste card e banir "
        "1 card da sua mão; ative 1 destes efeitos;\n"
        "● Escolha 1 monstro que seu oponente controla; tome o controle dele "
        "até a Fase Final deste turno.\n"
        "● Escolha 1 monstro no Cemitério do seu oponente; Invoque-o por "
        "Invocação-Especial."
    ),
    10389143: (
        'Invocado por Invocação-Especial pelo efeito de '
        '"Number 42: Galaxy Tomahawk". Destrua esta Ficha durante a Fase '
        "Final do turno em que ela foi Invocada por Invocação-Especial."
    ),
    11596936: (
        'Ative ao revelar 1 "Iron Core of Koa\'ki Meiru" na sua mão; destrua '
        "todos os Cards de Magia/Armadilha Baixados que seu oponente controla."
    ),
    15684835: (
        "Ative apenas quando um ou mais monstros forem Invocados por "
        "Invocação-Especial no campo do seu oponente. Depois da ativação, "
        "equipe este card a 1 desses monstros. O monstro equipado ganha 500 "
        "de ATK. Durante cada Fase de Apoio do seu oponente: cause 500 de "
        "dano a ele."
    ),
    45986603: (
        "Tome o controle de 1 monstro do seu oponente e equipe-o com este "
        "card. Durante cada Fase de Apoio do seu oponente: ele ganha 1000 LP."
    ),
    645794: (
        "[ Efeito de Pêndulo ]\n"
        "Este card não possui Efeito de Pêndulo.\n\n"
        "[ Efeito de Monstro ]\n"
        "Quando este card for Invocado por Invocação-Normal ou Especial: "
        "você pode Baixar 1 Card de Magia/Armadilha \"Majespectro\" diretamente "
        "do seu Deck, mas ele não pode ser ativado neste turno. Você só pode "
        "usar este efeito de \"Majespectro Rã - Ogama\" uma vez por turno. "
        "Não pode ser escolhido como alvo ou destruído por efeitos de card "
        "do seu oponente."
    ),
    2396042: (
        "[ Efeito de Pêndulo ]\n"
        "Este card não possui Efeito de Pêndulo.\n\n"
        "[ Efeito de Monstro ]\n"
        "No começo da Etapa de Dano, se este card batalhar um Monstro Pêndulo: "
        "o ATK e DEF deste card se tornam metade do seu ATK e DEF atuais até "
        "o final da Etapa de Dano."
    ),
    5506791: (
        "[ Efeito de Pêndulo ]\n"
        "Este card não possui Efeito de Pêndulo.\n\n"
        "[ Efeito de Monstro ]\n"
        "Quando este card for Invocado por Invocação-Normal ou Especial: "
        "você pode adicionar 1 card \"Majespectro\" do seu Deck à sua mão "
        "durante a Fase Final deste turno. Você só pode usar este efeito de "
        "\"Gato Majespectro - Nekomata\" uma vez por turno. Não pode ser "
        "escolhido como alvo ou destruído por efeitos de card do seu oponente."
    ),
}


def language_scores(value: str) -> tuple[int, int]:
    normalized = " " + value.lower().replace("\r", " ").replace("\n", " ") + " "
    english = sum(word in normalized for word in ENGLISH_WORDS)
    portuguese = sum(word in normalized for word in PORTUGUESE_WORDS)
    return english, portuguese


def needs_translation(value: str) -> bool:
    english, portuguese = language_scores(value)
    if english >= 2 and english > portuguese:
        return True
    normalized = " " + value.lower().replace("\r", " ").replace("\n", " ") + " "
    return portuguese <= 1 and any(
        marker in normalized for marker in RUNTIME_ENGLISH_MARKERS
    )


def needs_repair(value: str) -> bool:
    if needs_translation(value) or any(
        needs_translation(sentence)
        for sentence in SENTENCE_PATTERN.split(value)
    ):
        return True
    normalized = " " + value.lower().replace("\r", " ").replace("\n", " ") + " "
    return any(marker in normalized for marker in RUNTIME_ENGLISH_MARKERS)


def localize_heading(value: str) -> str:
    normalized = re.sub(r"\s+", " ", value.strip()).lower()
    if "pendulum" in normalized or "pêndulo" in normalized:
        return "[ Efeito de Pêndulo ]"
    if "monster" in normalized or "monstro" in normalized:
        return "[ Efeito de Monstro ]"
    if "spell" in normalized or "magia" in normalized:
        return "[ Efeito de Magia ]"
    if "trap" in normalized or "armadilha" in normalized:
        return "[ Efeito de Armadilha ]"
    return value


def protect_names(value: str) -> tuple[str, dict[str, str]]:
    protected: dict[str, str] = {}

    def replace(match: re.Match[str]) -> str:
        token = f"ZXQNAME{len(protected)}QXZ"
        protected[token] = match.group(0)
        return token

    return re.sub(r'"[^"\r\n]+"', replace, value), protected


def restore_names(value: str, protected: dict[str, str]) -> str:
    for token, original in protected.items():
        value = re.sub(re.escape(token), original, value, flags=re.IGNORECASE)
    return value


def normalize_terms(value: str) -> str:
    replacements = (
        (r"\bnon[- ]Tuner\b", "não-Regulador"),
        (r"\bTuner\b", "Regulador"),
        (r"\bSpecial Summoned\b", "Invocado por Invocação-Especial"),
        (r"\bSpecial Summon\b", "Invoque por Invocação-Especial"),
        (r"\bNormal Summoned\b", "Invocado por Invocação-Normal"),
        (r"\bNormal Summon\b", "Invoque por Invocação-Normal"),
        (r"\bFlip Summoned\b", "Invocado por Invocação-Virar"),
        (r"\bFlip Summon\b", "Invoque por Invocação-Virar"),
        (r"\bExtra Deck\b", "Deck Adicional"),
        (r"\bGraveyard\b|\bGY\b", "Cemitério"),
        (r"\bSpell/Trap\b", "Magia/Armadilha"),
        (r"\bSpell Card\b", "Card de Magia"),
        (r"\bTrap Card\b", "Card de Armadilha"),
        (r"\bMonster Card\b", "Card de Monstro"),
        (r"\bEquip Card\b", "Card de Equipamento"),
        (r"\bFusion Monster\b", "Monstro de Fusão"),
        (r"\bRitual Monster\b", "Monstro de Ritual"),
        (r"\bSynchro Monster\b", "Monstro Sincro"),
        (r"\bXyz Monster\b", "Monstro Xyz"),
        (r"\bLink Monster\b", "Monstro Link"),
        (r"\bPendulum Monster\b", "Monstro Pêndulo"),
        (r"\bPsychic-Type\b", "do Tipo Psíquico"),
        (r"\bSpellcaster-Type\b", "do Tipo Mago"),
        (r"\bReptile-Type\b", "do Tipo Réptil"),
        (r"\bInsect-Type\b", "do Tipo Inseto"),
        (r"\bZombie-Type\b", "do Tipo Zumbi"),
        (r"\bwith the effect of\b", "pelo efeito de"),
        (r"\band related cards\b", "e cards relacionados"),
        (r"\bDraw (\d+) cards?\b", r"Compre \1 cards"),
        (r"\bEnd Phase\b", "Fase Final"),
        (r"\bStandby Phase\b", "Fase de Apoio"),
        (r"\bMain Phase\b", "Fase Principal"),
        (r"\bBattle Phase\b", "Fase de Batalha"),
        (r"\bDamage Step\b", "Etapa de Dano"),
        (r"\bDamage Calculation\b", "cálculo de dano"),
        (r"\bLife Points\b", "LP"),
        (r"\bface-up\b", "com a face para cima"),
        (r"\bface-down\b", "com a face para baixo"),
        (r"\bremoved from play\b", "banido"),
        (r"\bdestroyed\b", "destruído"),
        (r"\bTribute Summoned\b", "Invocado por Invocação-Tributo"),
        (r"\bTribute Summon\b", "Invocar por Invocação-Tributo"),
        (r"\bTribute\b", "ofereça como Tributo"),
        (r"\bCartões\b", "Cards"),
        (r"\bcartões\b", "cards"),
        (r"\bCartão\b", "Card"),
        (r"\bcartão\b", "card"),
        (r"\bBaralho\b", "Deck"),
        (r"\bbaralho\b", "Deck"),
        (r"\bFeitiço\b", "Magia"),
        (r"\bfeitiço\b", "Magia"),
        (r"\bLP\b", "LP"),
        (r"\bATK\b", "ATK"),
        (r"\bDEF\b", "DEF"),
        (r"Invocação Especial", "Invocação-Especial"),
        (r"Invocação Normal", "Invocação-Normal"),
        (r"Invocação Pêndulo", "Invocação-Pêndulo"),
        (r"Invocad([oa]s?) Especiais?", r"Invocad\1 por Invocação-Especial"),
        (r"Especialmente Invocad([oa]s?)", r"Invocad\1 por Invocação-Especial"),
        (r"Especial Invocad([oa]s?)", r"Invocad\1 por Invocação-Especial"),
        (r"Invocar Especial", "Invocar por Invocação-Especial"),
        (r"Invoque Especial", "Invoque por Invocação-Especial"),
        (r"Normalmente Invocad([oa]s?)", r"Invocad\1 por Invocação-Normal"),
        (r"Normal Invocad([oa]s?)", r"Invocad\1 por Invocação-Normal"),
        (r"Invocar Normal", "Invocar por Invocação-Normal"),
        (r"Invoque Normal", "Invoque por Invocação-Normal"),
        (r"Ritual Invocar", "Invocar por Invocação-Ritual"),
        (r"Deck Extra", "Deck Adicional"),
        (r"Cartão de Magia", "Card de Magia"),
        (r"Cartão de Armadilha", "Card de Armadilha"),
        (r"Cartão de Monstro", "Card de Monstro"),
        (r"Cemitério \(GY\)", "Cemitério"),
        (r"\balvejar\b", "escolher como alvo"),
        (r"\batingir\b", "escolher como alvo"),
        (r"\bactivar\b", "ativar"),
        (r"\bactivação\b", "ativação"),
        (r"\bactivado\b", "ativado"),
        (r"\bactivada\b", "ativada"),
    )
    for pattern, replacement in replacements:
        value = re.sub(pattern, replacement, value, flags=re.IGNORECASE)
    value = re.sub(r"[ \t]+", " ", value)
    value = re.sub(r" *\n *", "\n", value)
    return value.strip()


def translate_sentences(
    descriptions: dict[int, str],
    processor: spm.SentencePieceProcessor,
    translator: ctranslate2.Translator,
) -> dict[int, str]:
    work: list[tuple[int, int, dict[str, str], str]] = []
    card_parts: dict[int, list[str]] = {}
    for code, description in descriptions.items():
        sections = HEADING_PATTERN.split(description)
        parts: list[str] = []
        for section in sections:
            if not section:
                continue
            if HEADING_PATTERN.fullmatch(section):
                parts.append(localize_heading(section))
                continue
            sentences = SENTENCE_PATTERN.split(section.strip())
            for sentence in sentences:
                if not sentence.strip():
                    continue
                index = len(parts)
                parts.append(sentence.strip())
                if needs_translation(sentence):
                    protected_text, protected = protect_names(sentence.strip())
                    work.append((code, index, protected, protected_text))
        card_parts[code] = parts

    batch_size = 24
    for offset in range(0, len(work), batch_size):
        batch = work[offset : offset + batch_size]
        tokenized = [
            processor.encode(item[3], out_type=str)
            for item in batch
        ]
        translated = translator.translate_batch(
            tokenized,
            beam_size=4,
            max_batch_size=batch_size,
        )
        for item, result in zip(batch, translated):
            code, index, protected, _ = item
            value = "".join(result.hypotheses[0]).replace("▁", " ").strip()
            card_parts[code][index] = restore_names(value, protected)

    resolved: dict[int, str] = {}
    for code, parts in card_parts.items():
        description = "\n".join(parts)
        description = re.sub(
            r"(\[ Efeito de Pêndulo \])\s*",
            r"\1\n",
            description,
        )
        description = re.sub(
            r"(\[ Efeito de Monstro \])\s*",
            r"\n\n\1\n",
            description,
        )
        resolved[code] = normalize_terms(description)
    return resolved


def merge_overrides(path: Path, overrides: dict[int, dict]) -> None:
    payload = json.loads(path.read_text(encoding="utf-8"))
    for card in payload.get("cards", []):
        override = overrides.get(int(card["code"]))
        if not override:
            continue
        card["name"] = override["name"]
        card["description"] = override["description"]
        if override.get("strings"):
            card["strings"] = override["strings"]
    if "manualOverrides" in payload:
        payload["manualOverrides"] = len(overrides)
    if "missingCodes" in payload:
        payload["missingCodes"] = [
            code for code in payload["missingCodes"]
            if int(code) not in overrides
        ]
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--snapshot", required=True, type=Path)
    parser.add_argument("--manual", required=True, type=Path)
    parser.add_argument("--model", required=True, type=Path)
    args = parser.parse_args()

    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    manual = json.loads(args.manual.read_text(encoding="utf-8"))
    catalog_by_code = {
        int(card["code"]): card for card in catalog.get("cards", [])
    }
    manual_by_code = {
        int(card["code"]): card for card in manual.get("cards", [])
    }
    effective = {
        code: manual_by_code.get(code, card)
        for code, card in catalog_by_code.items()
    }
    candidates = {
        code: str(card.get("description", ""))
        for code, card in effective.items()
        if needs_repair(str(card.get("description", "")))
    }

    processor = spm.SentencePieceProcessor(
        model_file=str(args.model / "sentencepiece.model")
    )
    translator = ctranslate2.Translator(
        str(args.model / "model"),
        device="cpu",
    )
    translated = translate_sentences(candidates, processor, translator)
    for code, description in translated.items():
        source = effective[code]
        manual_by_code[code] = {
            "code": code,
            "name": str(source.get("name", "")),
            "description": description,
            "strings": source.get("strings", []),
        }

    for code, description in AUTHORING_CORRECTIONS.items():
        source = effective[code]
        manual_by_code[code] = {
            "code": code,
            "name": str(source.get("name", "")),
            "description": description,
            "strings": source.get("strings", []),
        }

    for code, source in effective.items():
        description = str(source.get("description", ""))
        normalized = description.replace(
            "[Efeito de Pêndulo]",
            "[ Efeito de Pêndulo ]",
        ).replace(
            "[Efeito de Monstro]",
            "[ Efeito de Monstro ]",
        )
        if normalized == description:
            continue
        manual_by_code[code] = {
            "code": code,
            "name": str(source.get("name", "")),
            "description": normalized,
            "strings": source.get("strings", []),
        }

    manual["cards"] = [manual_by_code[code] for code in sorted(manual_by_code)]
    args.manual.write_text(
        json.dumps(manual, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    merge_overrides(args.catalog, manual_by_code)
    merge_overrides(args.snapshot, manual_by_code)
    still_english = [
        code for code, description in translated.items()
        if needs_translation(description)
    ]
    print(
        "ARCANE_UNOFFICIAL_PT_BR_OK "
        f"translated={len(translated)} overrides={len(manual_by_code)} "
        f"remaining={len(still_english)}"
    )
    if still_english:
        print("REMAINING " + ",".join(str(code) for code in still_english))


if __name__ == "__main__":
    main()
