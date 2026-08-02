# Protocolo multiplayer v4

Data: 2026-08-02

## Escopo

O online e um duelo privado 1v1 entre Windows x64 e Android ARM64. A mesma
logica offline, o mesmo `DuelArenaController` e o mesmo OCG Core continuam
responsaveis pelas regras. O multiplayer transporta somente entrada validada,
eventos de apresentacao e projecoes de estado por destinatario.

## Ciclo da sessao

1. O anfitriao cria uma MPS Session privada, com `MaxPlayers=2` e Relay DTLS.
2. O QoS do Relay escolhe automaticamente a regiao mais adequada.
3. O convidado entra pelo codigo e Sessions valida membership.
4. Connection Approval aceita somente membro da sala com protocolo e hash de
   compatibilidade iguais.
5. Cada lado envia o hash do deck; o deck completo percorre somente o canal
   confiavel e fragmentado.
6. O anfitriao inicia o unico OCG Core depois dos dois decks validos.
7. O campo e liberado somente apos o cliente aplicar e confirmar o primeiro
   snapshot autoritativo.

As propriedades publicas da sala incluem versao do app, protocolo, motor,
regras, banco de cartas, modo, status e se a entrada esta aberta. Propriedades
de membro incluem nome, hash do deck, ready, plataforma, assento e conexao.

## Autoridade e privacidade

- Somente o host cria e executa o duelo nativo.
- O cliente envia intencoes/respostas; nunca altera o estado oficial.
- Cada snapshot e criado para um destinatario. Mao, ordem do deck, carta
  baixada e prompt do adversario permanecem ocultos.
- Cartas ocultas no campo usam identificadores opacos estaveis, suficientes
  para manter ocupacao, overlay e animacao sem revelar a identidade.
- Seed e informacoes privadas do Core nunca entram no estado da sessao.

## Comando autoritativo

Cada resposta possui:

- `matchId` e `commandType`;
- `commandId` unico;
- `clientSequence` crescente;
- `expectedStateVersion`;
- `requestId` do prompt;
- resposta binaria em Base64.

O host aceita somente o proximo numero de sequencia e a versao atual. A mesma
mensagem pode ser repetida sem duplicar a acao. Reuso do mesmo identificador
com conteudo diferente e rejeitado. O limite e 10 comandos por segundo com
burst de 20.

## Estado, ACK e reparo

O snapshot inclui `matchId`, `recipientSeat`, `stateVersion`, hash da projecao
publica e a ultima sequencia aceita. O cliente recalcula o hash depois de
aplicar o estado e envia ACK. Versao regressiva ou hash divergente dispara
resync completo, limitado a uma solicitacao a cada tres segundos.

O hash publico e independente de perspectiva e exclui identidades privadas.
Assim ele detecta divergencia do campo sem comparar a mao secreta do oponente.

## Transporte de payload grande

JSON foi preservado no nivel logico para nao reescrever os DTOs e a camada de
apresentacao. Acima de 512 bytes ele e comprimido com GZip. O resultado percorre
o codec binario DUW3, agora carregado pelo protocolo de sessao v4:

- blocos de 800 bytes;
- pacote maximo de 848 bytes antes do prefixo NGO, abaixo de 1264 bytes;
- comprimento total, quantidade e indice de bloco;
- checksum por pacote e checksum do payload inteiro;
- ACK por bloco e por transferencia;
- recepcao fora de ordem e duplicata idempotente;
- retransmissao seletiva dos blocos ausentes;
- limite defensivo de 512 KiB por payload.

Isso substitui o envio unico que gerava `OverflowException` em decks maiores.

## Queda e reconexao

- Cliente: tentativas com backoff de 0,5, 1, 2, 4 e 5 segundos, jitter e janela
  total de 45 segundos.
- Android em background: pause/focus usa o mesmo fluxo.
- O host reserva o assento e pausa as novas decisoes durante a janela.
- Ao voltar, o cliente recebe snapshot integral e precisa confirma-lo.
- Reinicio frio do cliente usa apenas ID da sessao, sala, partida, protocolo,
  versao e horario. Nenhum token ou estado secreto e persistido.
- Se o host cair definitivamente, a partida termina. Nao existe falsa migracao
  de autoridade.

## Divergencias conscientes do PDF

1. Nao foi criada outra interface `IDuelEngineAdapter`: o
   `DuelArenaController` ja e a fronteira de autoridade sobre o
   `OcgDuelEngine`. Substitui-la duplicaria o fluxo offline e arriscaria cartas
   e efeitos existentes.
2. Nao foram adicionados PlayerObjects ou sincronizacao de transforms: este e
   um jogo de cartas e o PDF permite eventos mais snapshots. Cada peer abre a
   arena local e recebe a projecao por assento.
3. Os DTOs logicos continuam JSON, mas compressao, fragmentacao, checksum e
   ACK resolvem o requisito de payload grande sem trocar a apresentacao.
4. Nao foi criado seletor manual de regiao; MPS Sessions com Relay usa QoS
   automatico, conforme a recomendacao da propria especificacao.

Nenhuma dessas decisoes reduz a autoridade, a privacidade ou a capacidade de
crossplay exigidas.
