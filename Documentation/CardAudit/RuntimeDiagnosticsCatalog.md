# Catálogo de diagnósticos runtime

Gerado em UTC: `2026-08-04T10:31:17.129530Z`

O gravador é silencioso: nenhum registro é apresentado na tela de jogabilidade.
Códigos de sala, Relay e seeds são removidos antes da persistência.

## Resumo

- Arquivos lidos: **0**
- Sessões: **0**
- Registros válidos: **0**
- Linhas inválidas: **0**
- Ocorrências únicas para triagem: **0**

## Ocorrências abertas

| Contagem | Código | Camada | Componente | Carta | Plataformas | Mensagem |
|---:|---|---|---|---:|---|---|
| 0 | - | - | - | - | - | Nenhuma falha registrada ainda. |

## Uso

- Editor: os registros ficam em `Logs/CardAuditRuntime` dentro do projeto.
- Android: copie a pasta `CardAuditRuntime` do armazenamento persistente do aplicativo para o disco D e passe-a com `--input`.
- Preview: `python -B Tools/CardAudit/catalog_runtime_diagnostics.py`.
- Atualizar relatório: `python -B Tools/CardAudit/catalog_runtime_diagnostics.py --write`.

A presença de um registro não declara automaticamente defeito na carta. Cada ocorrência deve seguir a árvore fontes -> script -> Core -> protocolo -> apresentação -> IA -> multiplayer.
