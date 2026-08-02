# Changelog multiplayer 1.2.0

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
