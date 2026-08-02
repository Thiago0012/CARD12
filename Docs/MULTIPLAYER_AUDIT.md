# Auditoria do multiplayer PC/Android

Data: 2026-08-02

## Versoes resolvidas

- Unity Editor: 6000.5.0f1 (88b47c5e7076).
- `com.unity.services.multiplayer`: 2.2.2.
- `com.unity.netcode.gameobjects`: 2.10.0.
- `com.unity.multiplayer.playmode`: 2.0.2.
- Unity Transport resolvido: 6.5.0.
- Versao do jogo: 1.2.0; protocolo online atual: v4. O codec binario
  interno continua identificado como DUW3 para manter compatibilidade com o
  enquadramento ja testado; ele nao e a versao publica da sessao.
- Windows x64 e Android IL2CPP/ARM64 ja possuem builds Release validos.

Nenhum pacote foi atualizado durante esta auditoria. A implementacao deve usar
as APIs realmente resolvidas acima.

## Arquitetura atual

- `DuelOnlineSession` cria em runtime e preserva com `DontDestroyOnLoad` um
  unico `NetworkManager` e um unico `UnityTransport`.
- Nenhuma cena ou prefab contem outro `NetworkManager`; o fluxo nao usa
  `NetworkObject`, prefab de jogador ou scene management do NGO.
- A sala usa MPS Sessions 2.2.2 como fachada unica para membership, Relay e
  ciclo da sessao. Relay manual nao e iniciado em paralelo.
- Sessions configura Relay DTLS, capacidade exata de dois jogadores,
  propriedades de compatibilidade, estado ready e reconexao.
- A arena e aberta localmente em cada peer; snapshots e eventos de dominio
  sincronizam o duelo. Controles, UI e transforms nao fazem parte do protocolo.

## NetworkManager e ciclo atual

- Instancia: objeto runtime `Arcane Duel Online Session`.
- Transport: `UnityTransport` no mesmo objeto.
- Maximo efetivo: host + um cliente pela allocation Relay.
- Connection Approval: ativado, com payload compacto contendo identidade,
  protocolo e hash de compatibilidade.
- Compatibilidade: handshake posterior a conexao compara protocolo, versao do
  projeto, API/commit do Core, scripts de cartas e banco de cartas.
- Leave/erro: chama shutdown do NGO e limpa handlers locais.
- Queda do cliente: janela de 45 segundos, backoff e resync autoritativo. O
  pause/resume do Android usa o mesmo fluxo. Queda definitiva do host encerra
  a partida, sem migracao insegura do motor nativo.

## Mapeamento do motor

| Contrato do PDF | Implementacao atual/adaptacao |
| --- | --- |
| Autoridade | `DuelArenaController` do host possui a unica instancia oficial de `OcgDuelEngine`. |
| Validar/aplicar comando | O host valida `RequestId`, jogador e resposta contra o prompt atual e chama `SubmitCoreResponse`. |
| Eventos | `OcgDuelEngine.EventReceived` -> `DuelArenaController.CoreEventPresented` -> eventos de apresentacao filtrados. |
| Snapshot publico/privado | `DuelNetworkProtocol.CreateState` cria uma visao por destinatario e mascara mao, deck, set e prompts privados. |
| Estado do campo | `OcgDuelEngine.TryCaptureFieldSnapshot` consulta deck, mao, zonas, cemiterio, banidas, extra e overlays. |
| RNG | O seed nasce e e usado apenas no host; o cliente nunca o recebe. |
| Restore autoritativo | Nao disponivel na API publica do `ocgcore`; somente a projecao cliente pode ser restaurada. |

## Dados secretos

- Mao e ordem do deck do oponente nao sao enviadas com identidade.
- Carta set do oponente usa identificador opaco e codigo zero.
- Prompts e opcoes privadas so sao enviados ao jogador correspondente.
- O seed do duelo permanece apenas no host.
- PlayerPrefs armazena preferencias de audio, grafico, animacao e selecao local;
  nao armazena token UGS nem estado autoritativo do duelo.

## Situacao apos a implementacao v4

### Resolvido

1. Sessions/MPS e a fachada unica para criar, entrar, reconectar e sair.
2. Membership, ready, status, bloqueio da sala e compatibilidade ficam nas
   propriedades da sessao e dos jogadores.
3. Connection Approval valida membro, capacidade, protocolo e compatibilidade.
4. Comandos carregam `matchId`, `commandId`, sequencia do cliente e versao
   esperada; o host aplica idempotencia, deduplicacao e rate limit.
5. Snapshots possuem versao, hash publico, ACK e pedido de resync.
6. Payloads logicos maiores sao comprimidos e todos percorrem o codec
   fragmentado com checksum, ACK e retransmissao seletiva.

### Limitacoes intencionais

1. Nao existe migracao do host. O estado privado integral do OCG Core nativo
   nao pode ser reconstruido com seguranca em outro aparelho.
2. O payload logico permanece JSON para preservar os DTOs e a apresentacao
   existentes. GZip e o codec binario fragmentado eliminam o limite de 1264
   bytes que causava perda de decks e snapshots.
3. O ticket de reinicio frio recupera somente o cliente e nao guarda token de
   autenticacao ou estado privado do duelo.

### Dependente de validacao externa

1. A matriz PC-PC, Android-Android e PC-Android precisa ser executada com duas
   instalacoes reais e acesso aos servicos Unity para medir RTT, reconexao e
   comportamento de redes moveis.
2. O teto de defesa do wire e 512 KiB; telemetria real deve confirmar que os
   snapshots comprimidos permanecem abaixo da meta operacional de 64 KiB.

## Decisoes de implementacao

1. Migrar a orquestracao para MPS Sessions 2.2.2 com Relay DTLS e MaxPlayers=2,
   sem atualizar pacotes e sem manter Relay manual concorrente.
2. Preservar `DuelOnlineSession`, `DuelNetworkProtocol`, wire v3, UI e motor;
   Sessions substitui apenas criacao/entrada/leave/reconnect.
3. Ativar Connection Approval com payload pequeno e versionado.
4. Acrescentar identidade de comando, sequencia, versao esperada,
   deduplicacao, hash/ACK e resync ao protocolo existente.
5. Implementar grace period e reconexao do cliente, incluindo pause/resume
   Android. Queda definitiva do host encerra conforme o PDF.
6. Nao implementar restauracao/migracao autoritativa do host: a API do motor
   nao oferece restauracao integral segura. Essa divergencia e intencional e
   coincide com a politica MVP da especificacao.
