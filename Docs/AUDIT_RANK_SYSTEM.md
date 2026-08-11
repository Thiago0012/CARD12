# Auditoria do sistema ranqueado

Data: 2026-08-10  
Projeto: Card12 / Arcane Duel  
Unity: 6000.5.0f1

## Escopo auditado

- Os três documentos de especificação foram lidos integralmente e suas 64 páginas foram renderizadas para inspeção visual.
- Foram auditados o perfil local, o save, o carregamento de decks, o fluxo de sala privada, o resultado online autoritativo, a tela de resultado, a compatibilidade PC/Android e o módulo de torneios.
- A implementação será adicionada sem substituir a economia, o multiplayer casual, os torneios, os decks ou a apresentação já existentes.

## Mapa do projeto existente

| Responsabilidade | Implementação existente | Decisão de integração |
| --- | --- | --- |
| Identidade estável do jogador | `DeckCollectionState.localProfileId`, enviado no `DuelDeckLoadout.profileId` | Usar o `localProfileId`. Nome/apelido nunca será chave do ranque. |
| Persistência | `DeckRepository` grava `decks.json` por arquivo temporário e substituição atômica | Acrescentar o estado ranqueado ao mesmo JSON e aplicar ponto + recibo em uma única gravação. |
| Identidade da partida | `DuelOnlineSession.currentMatchId` | Usar esse valor na chave idempotente de pontos. |
| Resultado autoritativo | `CoreMessage.Win` é retido e finalizado em `FinalizePendingAuthoritativeResult` | Integrar o cálculo ranqueado somente após esse fechamento autoritativo. A UI não decide resultado. |
| Distribuição do resultado | `MatchRewardPayload`, `resultSequence` e entrega confiável/sequenciada | Transportar o recibo ranqueado no mesmo envelope final e validá-lo novamente no cliente. |
| Resultado visual | `OnlineDuelResultPresenter` | Estender a apresentação para PE, elo, barra e promoção/rebaixamento; sem alterar a decisão da partida. |
| Sala online | Lobby + Relay + Netcode for GameObjects em `DuelOnlineSession` | Casual e ranqueado reutilizam a conexão existente; a política é fixada antes do início. |
| Compatibilidade | `ProjectIdentity.MultiplayerCompatibility` e validação no handshake | Acrescentar versão e hash das regras ranqueadas; incompatibilidade bloqueia apenas partida ranqueada. |
| Torneios | `TournamentConfig`, `TournamentOnlineSession`, `TournamentDuelContext` e relatório autoritativo | Adicionar política Ranqueado/Não ranqueado, bloqueada quando a chave começa, e reutilizar o mesmo serviço de pontos. |
| Interface multiplayer | `GameFrontendBootstrap.MainMenuUi` e recursos em `Frontend/MultiplayerLobby` | Preservar o fundo atual e preencher os painéis centrais com o modo selecionado e os oito emblemas fornecidos. |

## Fonte de verdade e ordem de execução

1. O host cria o `matchId` e sela um snapshot imutável dos dois perfis antes do primeiro estado jogável.
2. O duelo é executado pelo motor existente.
3. Somente o evento terminal autoritativo produz o resultado ranqueado.
4. O host calcula os dois recibos com a mesma versão de regras.
5. Cada instalação valida e grava apenas o recibo do próprio `localProfileId`.
6. A chave idempotente é `rank:{rulesVersion}:{matchId}:{playerStableId}`.
7. A tela consome o recibo já persistido; ela nunca altera PE.

## Riscos encontrados e contenções

- **Autoridade baseada no host:** o projeto atual não possui servidor dedicado para o motor do duelo. O host continua sendo a autoridade, mas o cliente valida snapshot, versão, resultado e recibo antes de gravar.
- **Save local:** o ranque é persistido localmente porque não há backend de perfil. A estrutura ficará pronta para migração a uma autoridade remota sem mudar as regras de domínio.
- **Reconexão/duplicação:** mensagens finais podem ser reenviadas. O recibo e a versão do estado tornam o commit idempotente e rejeitam snapshot obsoleto.
- **Versões diferentes:** o fluxo casual continua disponível; o ranqueado recusa regras incompatíveis com mensagem explícita.
- **Torneio:** a configuração deixa de ser editável após `InProgress`; por isso a política competitiva fica efetivamente bloqueada junto com a chave.
- **Abandono:** o serviço suportará desistência/abandono confirmado e escudo ignorado, mas não inventará uma derrota a partir de desconexão ambígua. Sem confirmação autoritativa, o resultado deve permanecer `NoContest`.
- **Matchmaking:** a infraestrutura atual é por código de sala/Relay e não contém fila global. O botão ranqueado abre uma sala ranqueada com as mesmas garantias; uma fila automática real dependerá de um serviço de matchmaking posterior.
- **Transições:** a apresentação respeita o recibo já confirmado, bloqueia a saída durante a sequência, permite salto controlado para o estado final e não repete promoção/rebaixamento ao reler uma transação idempotente.

## Compatibilidade e migração

- O schema do perfil será incrementado de forma não destrutiva.
- Perfil sem dados de elo começa com 0 PE, Madeira, sem escudo e versão de estado inicial.
- Os oito emblemas serão copiados para `Assets` e importados como Sprite 2D/UI, mantendo proporção e transparência.
- A associação elo -> sprite será explícita e baseada em enum, nunca em nome livre.

## Divergência operacional documentada

Os PDFs recomendam criar branch e commit-base limpo antes das alterações. O repositório já contém alteração local do usuário em `ProjectSettings/QualitySettings.asset`. Não será criado commit nem será alterado esse arquivo durante esta implementação, para não misturar ou ocultar trabalho existente.

## Critério de conclusão

- Regras puras e testes de todos os limites, deltas, escudo e idempotência.
- Integração no resultado online regular e em torneios ranqueados.
- Interface dos três modos atualizada, com estado ranqueado correto e oito emblemas.
- Compilação Unity sem novos erros e relatório final em `Docs/RANK_SYSTEM_TEST_REPORT.md`.
