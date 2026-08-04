# Reconciliacao do catalogo publicado

Gerado em UTC: `2026-08-04T09:32:22.760003Z`.

## Escopo

A operacao alinha documentacao e apresentacao runtime ao `CardCatalog.asset` publicado.
Nao altera Lua, `cards.bin`, textos compilados, plugins, cenas ou regras.

| Item | Antes | Depois |
|---|---:|---:|
| Documentation/CardCatalog.csv | 961 | 969 |
| card-visuals.json | 961 | 969 |
| Artes copiadas para StreamingAssets | 0 | 47 |

## Alteracoes

- Entradas documentais adicionadas: 47.
- Entradas documentais residuais removidas: 39.
- Entradas visuais adicionadas: 47.
- Entradas visuais residuais removidas: 39.
- Artes autoradas copiadas: 47.

## Evidencia e rollback

A fonte de cada arte e o GUID permanecem registrados na matriz da auditoria.
Rollback: reverter `Documentation/CardCatalog.csv`, `card-visuals.json` e as novas artes em `StreamingAssets/Ygo/Art`.
