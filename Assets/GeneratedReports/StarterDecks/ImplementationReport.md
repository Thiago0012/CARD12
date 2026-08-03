# Relatório de implementação — Decks Iniciais e Banlist

Data da validação: 2026-08-03

## Resultado

A mecânica especificada foi integrada ao projeto usando somente dados locais em runtime. A banlist ativa é `tcg_eu_2026_05_18`, com vigência em 2026-05-18 e hash normalizado `946f0c25ca1676397353e93c291d25577daf3bdded6160f9efb26fe715a40260`.

Cinco dos seis decks iniciais estão publicáveis. O deck `starter_gladiator_control` permanece indisponível porque a carta `19613556` (Heavy Storm) é proibida e sua remoção reduz o Main Deck de 40 para 39 cartas. O sanitizador não inventa substituições: é necessária uma carta legal aprovada antes de liberar esse deck e os builds públicos.

## Implementado

- Banlist normativa com 226 registros: 119 proibidas, 97 limitadas e 10 semilimitadas.
- Validador compartilhado para Main, Extra e Side Deck, incluindo limite agregado por passcode e aliases.
- SHA-256 determinístico do manifesto normalizado do deck.
- Badges e tooltips de restrição no editor, coleção, loja e seleção inicial; não são exibidos no duelo nem na abertura de pacotes.
- Importação local dos seis decks, das 185 cartas únicas, artes, traduções, entradas de apresentação e scripts oficiais do Core.
- Sanitização determinística na ordem Main → Extra → Side e relatório explícito das remoções.
- Onboarding obrigatório com galeria responsiva, três prévias, detalhes por seção e bloqueio de decks inválidos.
- Concessão gratuita, única, atômica e idempotente. Somente Main + Extra entram na coleção; o Side permanece apenas para auditoria, conforme a especificação.
- Criação de uma cópia editável do deck escolhido e migração segura de perfis existentes.
- Ferramenta Editor-only `Arcane Arena/Development/Reset Starter Deck Choice` com backup e reversão exata da concessão.
- Pré-validação no cliente e revalidação no host imediatamente antes da partida online, incluindo banlist, Core DB, arte, script, posição, dimensões, Side e hash.
- Build gate que impede publicação quando a banlist, catálogo, arquivos ou qualquer deck inicial não estiverem válidos.

## Conteúdo dos decks

| Deck | Main | Extra | Side de auditoria | Estado |
| --- | ---: | ---: | ---: | --- |
| `starter_724579` | 42 | 5 | 15 | Publicável |
| `starter_gladiator_control` | 39 | 4 | 0 | Bloqueado: falta substituição aprovada |
| `starter_box_deck` | 42 | 12 | 0 | Publicável |
| `starter_724026` | 40 | 0 | 15 | Publicável |
| `starter_vampire_wolf` | 40 | 0 | 0 | Publicável |
| `starter_cyberse_master_duel` | 40 | 5 | 0 | Publicável |

O endereço do Box Deck foi corrigido de `72444`, incompleto no PDF, para a origem oficial `box-deck-724449`.

## Validação automatizada

| Suíte | Resultado |
| --- | --- |
| EditMode completo | 208/208 aprovados |
| Onboarding PlayMode | 1/1 aprovado |
| Multiplayer/crossplay PlayMode | 13/13 aprovados |
| Estabilização da arena PlayMode | 20/20 aprovados |
| PlayMode completo | 47/49 aprovados |

As duas falhas remanescentes do PlayMode completo são expectativas visuais antigas e não pertencem a esta mecânica:

1. `DuelSceneBuildsProfessionalBattlePresentation` ainda procura o objeto legado `Mão do Oponente`.
2. `PlayerHandRestsInsideTheLowerResponsiveViewportStrip` espera a posição vertical anterior da mão (`-209.056961`), enquanto a cena atual usa aproximadamente `-15`.

Não houve falha de compilação, banlist, concessão, onboarding, conteúdo, estabilização da arena ou multiplayer relacionada a esta implementação.

## Compatibilidade e limitações conhecidas

- Sete decks antigos de bot ficaram ilegais sob a nova banlist e são exibidos desabilitados até correção: `classic-red-eyes-black-dragon`, `female-reptile-deck-724288`, `hidden-arts-of-shadows-47456`, `mausoleum-lockdown-edison-724211`, `plant-link-722230`, `runick-724086` e `yugi-muto-battle-city-722944`.
- O Relay atual é P2P. A propriedade do deck é validada localmente e o host revalida integralmente conteúdo, banlist e hash, mas não existe inventário remoto assinado por backend para provar criptograficamente a propriedade contra um cliente binário adulterado.
- Builds PC e Android não foram gerados: o build gate está bloqueando corretamente enquanto o Gladiator Control possuir somente 39 cartas.

## Ação necessária

Adicionar em `Tools/generate_starter_deck_sources.py` uma substituição legal e expressamente aprovada para a carta `19613556`, regenerar os assets e repetir os gates. Nenhuma substituição aleatória foi aplicada.

