# Validacao da Fase 0

## Baseline

- Branch isolada: `codex/card-audit-phase01`.
- HEAD inicial: `ebf578da07335cbaf4e9b83a38f29b9648c3cf00`.
- Unity declarada e localizada no disco D: `6000.5.0f1`.
- O worktree ja continha alteracoes nao relacionadas de loja, pacotes, starters,
  multiplayer, cenas e qualidade. Elas foram preservadas; nenhuma foi revertida
  ou incorporada semanticamente pela auditoria.
- Nenhum script Lua, binario de dados, plugin do core, cena ou regra de duelo foi
  modificado nesta fase.

## Validacoes executadas

| Verificacao | Resultado | Evidencia |
|---|---|---|
| Compilacao dos scripts Editor-only da auditoria | PASSOU | Roslyn da Unity `6000.5.0f1`, zero erros; seis avisos preexistentes de API obsoleta em instaladores Editor |
| Compilacao do novo teste EditMode | PASSOU | Roslyn da Unity `6000.5.0f1`, zero erros |
| Gerador auxiliar em modo preview | PASSOU | SHA-256 de `CardHealthMatrix.json` permaneceu inalterado (`BB8D1C75701C4980362884561F4CD47A23707FF3E9904098996723DFA196A5F1`) |
| Consistencia da matriz gerada | PASSOU | 969 linhas, 969 IDs unicos, 40 cartas no primeiro lote e zero `CONCLUIDA` |
| Resolucao estatica dos scripts obrigatorios | PASSOU NO ESCOPO ESTATICO | zero script obrigatorio ausente, vazio ou com dependencia `Duel.LoadScript` ausente |
| Test Runner completo da Unity | NAO EXECUTADO | A Unity headless tentou inicializar o cliente de licenca/cache no disco C. A execucao foi interrompida para respeitar a restricao de nao alterar o disco C. Log: `Logs/codex-card-audit-phase01.log` |

## Consequencia do bloqueio da Unity headless

O codigo novo compila, mas este lote nao promove cartas para `PASSA_CORE`,
`PASSA_APRESENTACAO`, `PASSA_IA`, `PASSA_ONLINE` ou `CONCLUIDA`. Esses estados
dependem dos cenarios executaveis das fases posteriores e de uma execucao do Test
Runner em um ambiente onde a licenca da Unity ja esteja disponivel sem violar a
regra do disco C.
