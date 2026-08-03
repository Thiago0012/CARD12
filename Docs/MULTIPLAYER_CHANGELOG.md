# Changelog multiplayer 1.2.0

## Loading sincronizado e resultado online - protocolo v7

- Adicionado Canvas preto persistente, spinner com tempo nao escalado, Safe
  Area, bloqueio de raycast e mensagens de carregamento, espera,
  sincronizacao, reconexao e erro controlado.
- O carregador customizado existente continua sendo a unica autoridade de
  cena; NGO Scene Management permanece desativado.
- Adicionada barreira host-side com confirmacoes separadas de `SceneReady` e
  `SnapshotApplied`, vinculadas a `matchId`, `transitionEpoch`, seat e versao
  inicial do estado.
- O Core e preparado com input bloqueado somente depois das duas cenas. O
  input dos dois peers e liberado por um `BeginDuel` idempotente com tick de
  servidor futuro.
- O protocolo publico passou para `arcane-duel-online-v7`; builds anteriores
  sao recusados antes de misturar payloads de prontidao incompatíveis.
- `CoreMessage.Win` continua sendo o unico ponto terminal. O adapter agora
  produz resultado versionado com winner/loser seat, motivo, versao final e
  tick, sem inferir resultado por LP ou pela UI.
- Adicionadas telas bloqueantes de `VITORIA`, `DERROTA`, `EMPATE`,
  `PARTIDA ENCERRADA` e erro terminal, com `VOLTAR AO MENU` idempotente.
- A recompensa continua usando o claim existente por `matchId`/seat e e
  processada antes da apresentacao terminal; falha de economia nao impede a
  exibicao do resultado autoritativo.
- Desconexao depois de um resultado confirmado nao altera o vencedor nem
  prende o outro jogador. Cada peer pode retornar ao menu no proprio momento.
- Adicionados testes EditMode para gate, epoch, versao, duplicatas e
  mapeamento de resultado, alem de PlayMode para os Canvas de loading e
  resultado.

O nome historico `Docs/MULTIPLAYER_CHANGELOG.md` foi preservado em vez de
criar um segundo changelog com as palavras invertidas.

## Sessao e compatibilidade

- Migracao de Relay manual para MPS Sessions 2.2.2 como fachada unica.
- Sala privada de dois jogadores, Relay DTLS e selecao QoS automatica.
- Connection Approval e gates de versao do app, protocolo, Core, scripts e
  banco de cartas.
- Metadados de ready, deck, plataforma, status e membership.

## Sincronizacao

- Protocolo publico `arcane-duel-online-v4`.
- Envelope de comando idempotente, sequenciado e versionado.
- Snapshot por perspectiva com versao, hash publico, ACK e resync.
- Estado inicial bloqueado ate o primeiro ACK valido do cliente.
- Preservacao de zonas, overlays, IDs visuais e cartas ocultas opacas.

## Confiabilidade

- Compressao GZip e fragmentacao confiavel para decks e snapshots grandes.
- Checksum, duplicata idempotente, ACK por bloco/final e retransmissao seletiva.
- Rate limit de comandos e resync.
- Reconexao de cliente por 45 segundos, inclusive pause/resume Android.
- Seed do duelo criado com gerador criptografico.

## Release

- Versao do jogo: 1.2.0.
- Android versionCode: 4.
- Alvos: Windows x64 e Android ARM64, minSdk 26.
