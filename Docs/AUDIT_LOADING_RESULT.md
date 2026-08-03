# Auditoria - loading sincronizado e resultado online

Data: 2026-08-02  
Commit-base: `1d31e54331bfa566a7ae87f902f19b11c2e137da`  
Branch de trabalho: `codex/online-loading-result`

## Linha de base

- Unity Editor: 6000.5.0f1 (88b47c5e7076).
- Netcode for GameObjects: 2.10.0.
- Multiplayer Play Mode: 2.0.2.
- Unity Transport resolvido: 6.5.0.
- Multiplayer Services aparece localmente em 2.2.4. Os arquivos
  `Packages/manifest.json` e `Packages/packages-lock.json` ja estavam
  modificados de 2.2.2 para 2.2.4 antes desta feature e serao preservados
  separadamente, sem serem tratados como parte desta implementacao.
- Compilacao baseline em batchmode: sucesso, retorno 0, sem erro C#.

## Cenas e autoridade de carregamento

- Menu/lobby: `Assets/Scenes/MainMenu.unity` (`MainMenu`).
- Duelo online: `Assets/Scenes/DuelArena.unity` (`DuelArena`).
- Ambas estao habilitadas em `EditorBuildSettings.asset`.
- `DuelOnlineSession` cria uma unica instancia persistente de
  `NetworkManager` e `UnityTransport` no objeto runtime
  `Arcane Duel Online Session`.
- `NetworkConfig.EnableSceneManagement` esta desativado. Cada peer usa o
  carregador customizado atual, `SceneManager.LoadScene("DuelArena")`, uma
  unica vez depois do handshake. Esta feature manterá esse caminho e nao
  habilitara o scene management do NGO em paralelo.
- `DuelOnlineBridge.OnlineArenaTransitionPending` impede que
  `DuelArenaController` inicie um duelo offline temporario durante a troca.

## Sessao, rede e motor

- `MultiplayerSessionCoordinator` e a fachada unica para MPS Sessions,
  Lobby/Relay, DTLS, membership, propriedades de compatibilidade e leave.
- `DuelOnlineSession` registra um unico handler de wire fragmentado e possui
  handshake de deck, reconexao, comandos idempotentes, snapshots privados,
  hash, ACK e resync.
- A autoridade continua em `DuelArenaController` no host. O cliente usa
  `ConfigureNetworkReplica` e recebe somente uma projecao filtrada.
- O evento terminal real e `DuelArenaController.CoreEventPresented` com
  `CoreMessage.Win`, originado pelo OCG Core. Nenhuma UI deve inferir resultado
  por LP, deck ou animacao.
- A economia ja possui claim idempotente por `matchId` e seat em
  `GameFrontendBootstrap.TryApplyOnlineDuelReward`.

## Fluxo encontrado antes da alteracao

1. O host gera `matchId`, envia `Start` e ambos carregam `DuelArena` localmente.
2. O cliente envia `ClientReady.arenaReady` depois que a arena e encontrada.
3. O host inicia o Core quando as duas arenas estao anexadas e envia o
   snapshot inicial.
4. O cliente aplica o snapshot e envia `StateAck`; o host desbloqueia o input.
5. `CoreMessage.Win` concede recompensa, mas somente mostra uma mensagem
   temporaria. Nao ha tela terminal nem retorno idempotente ao menu.

## Lacunas e riscos

| Risco | Nivel | Correcao incremental |
| --- | --- | --- |
| Uma unica flag mistura recebimento de Start e cena pronta. | Alto | Separar `SceneReady` e `SnapshotApplied` por `matchId`, epoch e seat. |
| O host prepara o Core corretamente apos as cenas, mas nao existe comando `BeginDuel` comum aos peers. | Alto | Manter o Core bloqueado, aguardar os dois ACKs e liberar por tick futuro. |
| `OnlineArenaTransitionPending` termina assim que o controller e anexado, podendo revelar a arena antes do snapshot. | Alto | Manter Canvas preto ate `BeginDuel`. |
| Nao existe tela autoritativa de VITORIA/DERROTA. | Alto | Adaptar exclusivamente `CoreMessage.Win` para um resultado idempotente. |
| Sair depois do resultado pode disparar reconexao no outro peer. | Medio | Tratar desconexao terminal sem alterar o resultado e permitir saida independente. |
| Scene load e sincronizacao nao possuem estado unico observavel. | Medio | Adicionar maquina de estados pequena e diagnosticos. |
| Pacotes estao modificados localmente. | Medio | Nao atualizar, reverter ou incluir esses arquivos na feature. |

## Decisoes

1. Preservar motor, cartas, Lua/C++, cenas, prefabs, save, `NetworkManager`,
   Sessions e Relay atuais.
2. Implementar a feature com componentes runtime e adapters, sem editar cenas.
3. Reusar `ClientReady` como confirmacao de cena e `StateAck` como
   `SnapshotApplied`, acrescentando `transitionEpoch` e um `BeginDuel`
   idempotente. O protocolo publico sera incrementado porque payloads antigos
   nao podem participar da nova barreira com seguranca.
4. Usar Canvas persistente criado em runtime para loading e resultado. Isso
   evita duplicatas em cenas e deixa a feature reversivel sem alterar assets
   criticos.
5. A API do OCG Core nao separa `Prepare` e `Begin`. O host criara o Core com
   input bloqueado para gerar o snapshot, e o inicio jogavel sera o desbloqueio
   autoritativo no tick comum. Esta e a adaptacao minima ao lifecycle real.

## Resultado apos a implementacao

- O protocolo foi incrementado para v7 e agora transporta `matchId`,
  `transitionEpoch`, seat e `stateVersion` nas confirmacoes relevantes.
- A barreira do host separa `SceneReady` de `SnapshotApplied`, ignora mensagens
  atrasadas/duplicadas e emite um unico `BeginDuel` com tick futuro.
- O host prepara o Core com input bloqueado; host e cliente so desbloqueiam a
  partida ao aplicar o mesmo `BeginDuel`.
- Um Canvas persistente cobre completamente a troca de cena, usa tempo nao
  escalado, respeita Safe Area e bloqueia raycasts ate o duelo estar pronto.
- O unico gatilho terminal e `CoreMessage.Win`. O host envia estado final e
  resultado completo; o cliente adia a tela ate possuir a versao final do
  snapshot.
- A tela terminal e idempotente, bloqueia comandos de duelo, apresenta
  `VITORIA`, `DERROTA`, empate ou encerramento e permite retorno independente
  ao menu.
- Recompensa, retorno e resultado possuem gates contra execucao duplicada.
- Nao foram alteradas cenas, prefabs, controles, motor, scripts de cartas,
  Lua/C++ ou save existente.

## Adaptacoes deliberadas em relacao a especificacao

1. A configuracao foi serializada no componente runtime persistente por meio
   de `OnlineMatchFlowConfig`, em vez de criar um asset `ScriptableObject`.
   Assim, os valores continuam editaveis e a mudanca nao exige referencia em
   cena/prefab.
2. O projeto ja possuia `Docs/MULTIPLAYER_CHANGELOG.md`; ele foi ampliado em
   vez de criar um segundo arquivo de changelog com nome diferente.
3. Como o OCG Core nao oferece uma API separada de prepare/start, o snapshot
   inicial e produzido com o Core travado e o inicio efetivo e o desbloqueio
   comum disparado por `BeginDuel`.

## Evidencia final

- EditMode: 189/189 aprovados.
- PlayMode direcionado: 17/17 aprovados.
- Build Release Windows x64: sucesso.
- Build Release Android ARM64: sucesso.
- PlayMode completo: 40/48 aprovados; as oito falhas remanescentes sao testes
  visuais antigos listados em `Docs/MULTIPLAYER_TEST_REPORT.md` e nao exercitam
  o fluxo implementado aqui.
