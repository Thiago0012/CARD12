# Auditoria de cartas - Fases 0 e 1

> Escopo deliberado: baseline, inventario e priorizacao. Nenhum efeito, regra do core ou comportamento funcional foi alterado.

## Baseline reproduzivel

| Item | Valor |
|---|---|
| Gerado em UTC | 2026-08-09T13:21:24.0844683Z |
| Projeto | 1.2.0 |
| Unity | 6000.5.0f1 |
| Branch | main |
| HEAD | 40fc9754ac79e8c2fa3bfa7bff701b85ea5b36e2 |
| API do core | 11.0 |
| ygopro-core | 0764db0c75b3d1d574880d365aa3695ab1f13b43 |
| CardScripts | 55607ee511d9697b6eac5dbb689deaa5be712826 |
| BabelCDB | 8d60901db521eb4183ca72560c01a70a6386c98c |

## Fontes encontradas

| Fonte | Contagem | SHA-256 |
|---|---:|---|
| CardCatalog.asset | 969 | `A9E17D883758B57CB0B975D36DCA4EC0A405E808F81D8E23E2A4E3BA7AACA7EB` |
| Documentation/CardCatalog.csv | 969 | `1B0B10224B71F48C9ED2AA75C610ABADC8B794349C38878782D627C95F4F55C8` |
| Documentation/CoreCardCatalog.csv | 1030 | `D4902450A6342FC8052DE91F9333F8FF1121ED88ED829E192F8C471961313E99` |
| cards.bin | 1037 | `287960E21BFA8BC18B23993BA384D3FC4C849D6D7C6681D7F4CA5373F0A7D440` |
| card-texts.json | 1037 | `D2DEBA5EE11DA16B68361ADC2D8E1A3C2C6FD044DA59BDDEE6526FDA143428E8` |
| card-visuals.json | 969 | `182CD8925D44EBD59D5816B4BB17FCD4A793A7634D3A20D5A5593EDD2314ED96` |
| scripts oficiais | 940 | `1346451B79D109B7E6B2FCFDE5C4240CB8D7DF281B195BD7446BEF16C9AD0338` |
| scripts customizados | 7 | `841AFD7D616A167210C9C61D14FDD10B13450548F43B8FC6BA6322B9E1136BD9` |
| plugin Windows | 1 | `D0DC3BB602007E17AAFE097570C4EBE6C19903ED7B3B791E2A862091BBD15817` |
| plugin Android arm64 | 1 | `4AD9B82E4EE9935DE6B0F239BF79E4AB49A769E820C481F2F28D25A0EFDFAFEE` |

Conteudo publicado: 23 produtos/decks de loja, 6 starters, 47 listas curadas e 26 pacotes.

## Estado da matriz

| Status | Cartas |
|---|---:|
| INVENTARIADA | 0 |
| BLOQUEADA_DADOS | 0 |
| CARREGA | 969 |
| TESTE_PARCIAL | 0 |
| PASSA_CORE | 0 |
| PASSA_APRESENTACAO | 0 |
| PASSA_IA | 0 |
| PASSA_ONLINE | 0 |
| CONCLUIDA | 0 |

Prioridades: P0=0, P1=878, P2=53, P3=21, P4=0, P5=17.

## Divergencias de integridade

| Codigo | Divergencia | Total | Amostra |
|---|---|---:|---|
| F01 | ID duplicado no catalogo | 0 |  |
| F01 | ID duplicado no CSV documental | 0 |  |
| F01 | Entrada invalida no catalogo | 0 |  |
| F01 | Catalogo sem CSV documental | 0 |  |
| F01 | CSV documental sem catalogo | 0 |  |
| F01 | Catalogo sem documento do core | 0 |  |
| F01 | Documento do core sem catalogo | 61 | 02625940, 04031928, 05405694, 06172122, 08267140, 09047461, 09596126, 12538374, ... |
| F01 | Catalogo sem dados compilados | 0 |  |
| F01 | Dados compilados sem catalogo | 68 | 02625940, 04031928, 05405694, 06172122, 08267140, 09047461, 09596126, 12538374, ... |
| F01 | Catalogo sem texto compilado | 0 |  |
| F08 | Catalogo sem manifesto visual | 0 |  |
| F08 | Manifesto visual sem catalogo | 0 |  |
| F02 | Script obrigatorio ausente | 0 |  |
| F02 | Script obrigatorio vazio | 0 |  |
| F02 | Dependencia Lua ausente | 0 |  |
| F08 | Arte ausente | 0 |  |
| F01 | Carta de deck fora do catalogo | 3 | 34267821, 43711255, 78661338 |
| F01 | Carta de pack fora do catalogo | 0 |  |

## Arquitetura preservada

- BabelCDB e os artefatos compilados continuam sendo a fonte de dados.
- CardScripts/Lua continuam sendo a fonte de efeitos.
- ygopro-core continua sendo o arbitro das regras.
- C# permanece responsavel por catalogo, apresentacao, protocolo, IA e multiplayer.
- A ferramenta nova fica em `Assets/Editor/CardAudit` e executa em modo de leitura/relatorio.

## Limites desta evidencia

A Fase 1 comprova presenca e coerencia estrutural; ela nao comprova semantica. `CARREGA` significa que dados, texto, visual, arte e script obrigatorio estao localizaveis. Os status PASSA_* e CONCLUIDA permanecem zerados ate os cenarios das fases seguintes.

A suite existente `CardCatalogBatchEditModeTests` declara 23 lotes de 25 imagens (cobertura maxima de 575 posicoes), enquanto o manifesto atual possui 969 entradas. O teste de ciclo nativo percorre a base compilada, mas a lacuna visual deve ser removida numa fase posterior.

Arquivos gerados: `CardHealthMatrix.csv`, `CardHealthMatrix.json`, `CardScriptCompatibilityReport.md` e `FirstBatchPlan.md`.
