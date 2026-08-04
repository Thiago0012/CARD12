# Auditoria de Cartas — Fase 3: Reprodutor determinístico

Gerado em UTC: `2026-08-04T09:54:21Z`

## Resultado desta etapa

- Foi criado `CardScenarioRunner`, isolado em `Assets/Tests/EditMode/CardAudit`.
- Cada execução usa seed fixa, o mesmo `OcgDuelEngine` autoritativo do jogo e
  coloca a carta-alvo nos dois lados da partida.
- O runner conduz jogador 1 e jogador 2, captura snapshots do campo e registra
  uma assinatura determinística de eventos e respostas.
- A execução falha diante de `Retry`, mensagem desconhecida, prompt não
  tipado, falha nativa/Lua, limite de decisões, ausência da carta no snapshot
  ou incapacidade de avançar pelo número de turnos esperado.
- Foram preparados 40 casos do primeiro lote e quatro repetições
  representativas (Monstro Normal, Monstro de Efeito, Magia e Extra Deck) para
  comparar a assinatura da mesma seed.

## Correção concreta de cobertura

`CardCatalogBatchEditModeTests` possuía 23 índices escritos manualmente. Com
lotes de 25, isso exercitava apenas as primeiras 575 cartas e deixava 394
cartas publicadas fora dos testes em lote.

Os índices agora são calculados pelo tamanho de `CardVisualCatalog`. No estado
atual, o conjunto passa a ter 39 lotes e cobre as 969 cartas. A cobertura cresce
automaticamente quando o catálogo receber novas cartas.

## Validação disponível neste ambiente

| Verificação | Resultado | Evidência |
|---|---|---|
| Compilação do runner e testes EditMode | PASSOU | Roslyn da Unity `6000.5.0f1`, zero erros e zero avisos no assembly de testes |
| Compilação dos utilitários Editor de auditoria | PASSOU | Zero erros; seis avisos preexistentes de APIs `FindObjectsByType` obsoletas em instaladores não relacionados |
| Integridade textual do diff | PASSOU | `git diff --check` sem erro; apenas avisos preexistentes de fim de linha em arquivos fora desta etapa |
| Execução do Core nativo pelo Unity Test Runner | PENDENTE | O Editor não está em execução e o modo headless tenta gravar cache/licença no disco C, proibido pelo escopo deste trabalho |

Nenhuma carta foi promovida para `CONCLUIDA` sem executar os cenários. A
compilação comprova que o reprodutor está integrado; a semântica permanece
`NAO_EXECUTADO` até haver evidência do Core nativo.

## Filtro de execução

No Unity Test Runner, os novos testes estão na categoria
`CardAudit.Phase3`. O caso de cada carta tem nome estável no formato
`CardScenario_########_Nome`.

## Limite deliberado

Esta etapa valida transporte interno do Core, prompts tipados, turnos dos dois
jogadores, snapshots e determinismo. Ela não declara que o texto individual de
cada efeito está correto: os cenários semânticos positivo, negativo, fronteira,
corrente e online continuam exigindo preparação específica de zona, fase,
alvos e dependências conforme o dossiê de cada carta.
