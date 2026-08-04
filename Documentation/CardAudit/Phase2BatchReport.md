# Relatorio do lote - Fase 2

## Escopo

- Reconciliar o catalogo publicado com documentacao, manifesto visual e artes
  runtime.
- Priorizar 40 cartas de Blue-Eyes, Dark Magician, Red-Eyes e starter.
- Incluir representacao de Main Deck, respostas, interacoes, extensores e Extra
  Deck.
- Preparar dossies e cenarios; nenhuma correcao semantica de efeito foi feita.

## Baseline e resultado

| Estado | Antes | Depois |
|---|---:|---:|
| CardCatalog IDs unicos | 969 | 969 |
| Documentation/CardCatalog.csv | 961 | 969 |
| card-visuals.json | 961 | 969 |
| BLOQUEADA_DADOS | 47 | 0 |
| CARREGA | 922 | 969 |
| Scripts obrigatorios ausentes/vazios | 0 | 0 |
| CONCLUIDA | 0 | 0 |

Versoes e hashes completos permanecem em `CardHealthMatrix.json`.

## Falhas e camada

- F01/dados-pipeline: 47 cartas estavam publicadas no CardCatalog e possuiam
  arte autorada valida, mas nao tinham linha documental, entrada no manifesto
  visual ou copia da arte em StreamingAssets.
- F01/residuo: 39 entradas antigas permaneciam no CSV e manifesto apesar de nao
  fazerem parte do CardCatalog publicado.
- Tres IDs aparecem somente no Side Deck curado `SummonBansSide` e nao existem
  na base compilada, catalogo ou runtime. O modelo atual de produto nao publica
  Side Deck; portanto foram mantidos como limitacao documental, sem insercao
  artificial de cartas inexistentes.

## Alteracao aplicada

- 47 linhas documentais e visuais adicionadas.
- 39 linhas residuais documentais e visuais removidas.
- 47 JPEGs ja existentes no projeto copiados para StreamingAssets, sem download.
- `catalogSha256` do manifesto visual recalculado a partir do CSV reconciliado.
- Nenhum Lua, `cards.bin`, texto compilado, core, plugin, cena ou regra alterado.

## Primeiro lote

- 40 cartas.
- Papeis encontrados: 17 centrais, 19 extensores, 11 interacoes, 5 respostas e
  8 cartas de Extra Deck. Uma carta pode possuir mais de um papel.
- Cenarios especificados: 40 positivos, 40 negativos, 40 de fronteira, 30 de
  corrente e 16 online.
- Todos os dossies continuam com resultado `CARREGA`; nenhum recebeu aprovacao
  semantica.

## Validacoes

- Reconciliador em preview: idempotente e sem escrita.
- Segunda preview apos aplicacao: zero adicao, remocao ou copia pendente.
- Matriz: 969 linhas/IDs, zero bloqueio de dados.
- Manifesto: 969 entradas, hash correspondente ao CSV e 969 artes localizaveis.
- Dossies: 40 IDs unicos, cenarios positivo/negativo/fronteira em todas as
  cartas e 8 representantes de Extra Deck.
- Scripts Editor-only e testes EditMode novos: compilados com Roslyn da Unity
  6000.5.0f1, zero erros.
- Test Runner completo: nao executado, pois a Unity headless tenta inicializar
  licenca/cache no disco C e a restricao do projeto permite escrita somente no D.

## Riscos, rollback e bloqueios

- Risco: algum fluxo nao catalogado poderia depender de uma das 39 entradas
  residuais. A auditoria nao encontrou uso delas em shop, starters ou packs
  publicados; a base compilada nao foi removida.
- Rollback: reverter `Documentation/CardCatalog.csv`, `card-visuals.json` e
  remover somente as 47 novas copias de arte e respectivos `.meta`.
- Bloqueio atual: os campos normalizados dos dossies sao rascunhos automaticos;
  exigem revisao semantica antes de criar um teste inicialmente vermelho.

## Proximo lote recomendado

Revisar os dossies por familia, escolher os primeiros cenarios representativos e
implementar o `CardScenarioRunner` deterministico. Somente uma falha reproduzida
deve receber camada responsavel e correcao minima.
