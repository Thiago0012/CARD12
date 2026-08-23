# Auditoria de cartas - Fases 0 e 1

> Escopo deliberado: baseline, inventario e priorizacao. Nenhum efeito, regra do core ou comportamento funcional foi alterado.

## Baseline reproduzivel

| Item | Valor |
|---|---|
| Gerado em UTC | 2026-08-23T04:38:09.233493Z |
| Projeto | 1.2.0 |
| Unity | 6000.5.0f1 |
| Branch | main |
| HEAD | 010df8881d6cf9bff0fbf0da7d494eb2fe279f65 |
| API do core | 11.0 |
| ygopro-core | 0764db0c75b3d1d574880d365aa3695ab1f13b43 |
| CardScripts | 55607ee511d9697b6eac5dbb689deaa5be712826 |
| BabelCDB | 8d60901db521eb4183ca72560c01a70a6386c98c |

## Fontes encontradas

| Fonte | Contagem |
|---|---:|
| CardCatalog.asset | 3276 |
| Documentation/CardCatalog.csv | 3276 |
| Documentation/CoreCardCatalog.csv | 3340 |
| cards.bin | 3349 |
| card-texts.json | 3349 |
| card-visuals.json | 3276 |
| scripts oficiais | 3072 |
| scripts customizados | 29 |

Os hashes SHA-256 completos estao em `CardHealthMatrix.json`.

## Estado da matriz

| Status | Cartas |
|---|---:|
| BLOQUEADA_DADOS | 0 |
| CARREGA | 3276 |
| TESTE_PARCIAL | 0 |
| PASSA_CORE | 0 |
| PASSA_APRESENTACAO | 0 |
| PASSA_IA | 0 |
| PASSA_ONLINE | 0 |
| CONCLUIDA | 0 |

## Divergencias de integridade

| Divergencia | Total | Amostra |
|---|---:|---|
| duplicateCatalogIds | 0 |  |
| duplicateDocumentationIds | 0 |  |
| invalidCatalogEntries | 0 |  |
| catalogMissingFromDocumentation | 0 |  |
| documentationMissingFromCatalog | 0 |  |
| catalogMissingFromCoreDocumentation | 0 |  |
| coreDocumentationMissingFromCatalog | 64 | 00645088, 01799465, 02625940, 03129134, 03285552, 07610395, 08025951, 11738490, ... |
| catalogMissingFromCompiledDatabase | 0 |  |
| compiledDatabaseMissingFromCatalog | 73 | 00645088, 01799465, 02625940, 03129134, 03285552, 07610395, 08025951, 11738490, ... |
| catalogMissingFromTextDatabase | 0 |  |
| catalogMissingFromVisualManifest | 0 |  |
| visualManifestMissingFromCatalog | 0 |  |
| missingRequiredScripts | 0 |  |
| emptyRequiredScripts | 0 |  |
| missingScriptDependencies | 0 |  |
| missingArtwork | 0 |  |
| deckCardsMissingFromCatalog | 3 | 34267821, 43711255, 78661338 |
| packCardsMissingFromCatalog | 0 |  |

## Arquitetura preservada

- BabelCDB e os artefatos compilados continuam sendo a fonte de dados.
- CardScripts/Lua continuam sendo a fonte de efeitos.
- ygopro-core continua sendo o arbitro das regras.
- C# permanece responsavel por catalogo, apresentacao, protocolo, IA e multiplayer.
- A ferramenta nova fica isolada em `Assets/Editor/CardAudit` e `Tools/CardAudit`.

## Limites desta evidencia

`CARREGA` comprova apenas coerencia estrutural. Nenhuma carta foi declarada CONCLUIDA sem cenarios semanticos.
A suite visual existente declara 23 lotes de 25 posicoes (575), abaixo das 961 entradas atuais do manifesto; essa lacuna deve ser removida nas fases seguintes.
