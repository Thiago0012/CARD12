# Matriz consolidada — Sistema de Torneios

Esta matriz é a fonte de rastreabilidade da implementação. Os três documentos
são aplicados em conjunto e com a seguinte precedência quando descrevem o mesmo
assunto:

1. **PDF 1 — especificação funcional:** regras, dados, estabilidade e aceite.
2. **PDF 2 — mockups-base:** conteúdo obrigatório e hierarquia das telas.
3. **PDF 3 — execução visual:** fluxo final, prioridade e acabamento integrado
   à linguagem visual atual do jogo.

Os mockups orientam a hierarquia, não uma cópia pixel a pixel. O módulo deve ser
responsivo em PC e Android e reutilizar a infraestrutura visual do projeto.

## Fluxo consolidado

`Hub de torneios -> Criar/Entrar/Retomar/Histórico -> Assistente de criação ->
Lobby e validação -> Início -> Visão geral -> Chave ou Classificação -> Duelo
1v1 -> Resultado autoritativo -> Próximo confronto -> Pódio, métricas e
histórico`

## PDF 1 — regras e arquitetura, página por página

| Página | Conteúdo organizado | Destino na implementação |
|---:|---|---|
| 1 | Objetivo, criação/gerenciamento/execução e resultado esperado | `TournamentOnlineSession`, hub e fluxo completo |
| 2 | Escopo do MVP forte, itens futuros e arquitetura em camadas | Separação domínio/rede/UI/duelo/persistência |
| 3 | Resultado padronizado, mata-mata, potências de dois e BYE para 6/10/12 | `TournamentMatchResult`, chave determinística, qualquer capacidade par de 2–32 e folgas automáticas |
| 4 | Pontos sem empate de partida e Best of N ímpar | Agenda por pontos e validação Bo1/3/5/7/ímpar |
| 5 | Vitórias necessárias, identidade e status dos participantes | Modelos, rótulos VOCÊ/ORGANIZADOR e estados de lobby |
| 6 | Ban list, pool permitido, validação e mensagens específicas | `TournamentDeckRulesValidator` e painel de validação |
| 7 | Pontuação, desempate e ranking em tempo real | Ordenação por pontos/vitórias/confronto/saldo/dano |
| 8 | Métricas detalhadas por jogador e estratégia | Coletor de eventos e agregador de estatísticas |
| 9 | Métricas por carta, duelo e campeonato | Snapshots compactos, rankings e cards de resumo |
| 10 | Taxonomia de eventos e direção visual | `TournamentCardEventType` e tema ciano/dourado |
| 11 | Abas de criação, lobby, andamento e métricas | Assistente e navegação por abas |
| 12 | Fluxo antes/durante/depois e edição pré-início | Máquina de estados e bloqueios de edição |
| 13 | Edição pós-início, estabilidade, recuperação e casos-limite | Locks, idempotência, persistência atômica e retomada |
| 14 | Estruturas `Config`, `Player` e início de `Match` | `TournamentModels.cs` |
| 15 | `Match`, `Stats` e fases 1–6 | Domínio, UI, formatos, regras, métricas e testes |
| 16 | Acabamento, critérios de aceite e extras | QA, histórico, MVP e pódio |
| 17 | Encerramento | Confirmação de cobertura documental |

Itens explicitamente posteriores no PDF 1 não devem contaminar o núcleo desta
entrega: grupos + mata-mata, espectador completo, replay automático, QR,
premiações/conquistas, árbitro, recorrência e temporadas ranqueadas.

## PDF 2 — telas e hierarquia, página por página

| Página | Conteúdo organizado | Tela/componente |
|---:|---|---|
| 1 | Objetivo visual e identidade tecnológico-arcana | Tema compartilhado do módulo |
| 2 | Índice das telas | Rotas do `TournamentUi` |
| 3 | Uso não pixel-perfect, mapa e Criar Torneio | Hub + assistente de criação |
| 4 | Lobby, vagas, status, resumo e ações | Lobby responsivo com painel lateral |
| 5 | Visão geral e abas do torneio | Hub do campeonato em andamento |
| 6 | Chave mata-mata navegável e detalhe de confronto | Bracket + seleção de partida |
| 7 | Classificação por pontos e desempate visível | Tabela ordenada |
| 8 | Métricas gerais, rankings e expansão para gráficos | Cards e listas inicialmente |
| 9 | Detalhe de desempenho por jogador | Subaba Jogadores |
| 10 | Ranking e detalhe por carta | Subaba Cartas |
| 11 | Pódio, relatório, histórico e checklist visual | Tela final e aceite da UI |
| 12 | Encerramento | Confirmação de cobertura documental |

## PDF 3 — execução e prioridade, página por página

| Página | Conteúdo organizado | Decisão aplicada |
|---:|---|---|
| 1 | Papel da V3 e redução de retrabalho | Esta matriz consolidada |
| 2 | Índice, direção visual, fluxo e ordem | Plano de implementação |
| 3 | Precedência dos PDFs, linguagem visual e regra de ouro | Um foco principal por tela; navegação clara |
| 4 | Continuação do fluxo entre estados | Máquina de estados única |
| 5 | Hub Criar/Continuar/Recentes e assistente passo a passo | Entrada oficial do módulo |
| 6 | Lobby polido e visão geral com próxima partida | Status acionável e CTA de duelo |
| 7 | Classificação polida e métricas de uso real | Tabelas/listas legíveis |
| 8 | Pódio, transições e travas obrigatórias | Idempotência, exclusão mútua e rollback |
| 9 | Blocos 2/3 e execução incremental | Regras/persistência antes de métricas/polimento |
| 10 | Instrução consolidada final | Validação contra os três PDFs |

## Arquitetura no Card12

| Camada | Responsabilidade | Arquivos principais |
|---|---|---|
| Domínio | Configuração, participantes, chave, classificação, resultados e métricas | `Assets/Game/Runtime/Tournaments/*` |
| Persistência | Estado ativo, ticket de retomada e histórico, com escrita atômica | `TournamentPersistence.cs` |
| Lobby | Sala persistente de 2–32, identidade, ready, deck, snapshot e heartbeat | `Assets/Scripts/Multiplayer/Tournaments/*` |
| Confronto | Um Relay privado 1v1 por confronto, preservando o OCG Core autoritativo | Integração com `DuelOnlineSession.cs` |
| UI | Hub, criação, lobby, andamento, tabelas, métricas e pódio | Partial de `GameFrontendBootstrap` |
| Testes | Regras, progressão, idempotência, validação, persistência e codec | EditMode/PlayMode |

## Regras críticas de aceite

- Torneio só inicia com todos os presentes online, prontos e com deck válido.
- Com “início com maioria” desligado, todas as vagas são obrigatórias; ligado, uma maioria absoluta (3/4, 5/8, 9/16 etc.) pode fechar e bloquear o lobby.
- Chaves não-potência-de-dois recebem folgas automáticas; pontos com total ímpar alterna o participante que descansa em cada rodada.
- O mesmo jogador não abre dois confrontos simultâneos.
- Apenas um resultado válido e identificado atualiza um confronto.
- Resultado repetido é ignorado de forma idempotente.
- Regras competitivas ficam bloqueadas após o início.
- Cada resultado salva o estado antes de liberar a próxima partida.
- Fechamento inesperado permite retomar o último estado íntegro.
- Mata-mata define campeão automaticamente; pontos usa desempates definidos.
- PC e Android leem o mesmo snapshot do lobby e usam o mesmo protocolo de duelo.
- Métricas viajam agregadas, evitando mensagens grandes e perda de conteúdo.

## Estado da implementação

- [x] Modelos, configuração e estados.
- [x] Validação de deck, pool, ban list e hash de bloqueio.
- [x] Gerenciador de mata-mata/pontos, Best of N e resultado idempotente.
- [x] Capacidades pares editáveis de 2–32, incluindo 6/10/12, e início antecipado por maioria.
- [x] Chaves com BYE e rodadas por pontos para quantidades pares ou ímpares de presentes.
- [x] Persistência local atômica e histórico.
- [x] Coletor compacto de métricas observáveis do Core.
- [x] Lobby Unity 2–32 e sincronização autoritativa.
- [x] Integração de cada confronto com a sala Relay 1v1 existente.
- [x] Hub, criação, lobby, chave, classificação, métricas e pódio.
- [x] WO opcional com confirmação do organizador e avanço da chave.
- [x] Reabertura segura de confronto com descarte da sala Relay anterior.
- [x] Testes automatizados e validação de compilação Unity.
- [ ] Homologação externa PC + Android em dois aparelhos e duas redes.

Esta lista deve ser atualizada junto com o código; nenhum item é considerado
entregue apenas por existir visualmente.

## Decisões e divergências documentadas

- **Backend:** os PDFs permitem uma implementação incremental. Para preservar o
  multiplayer já existente, o campeonato persistente usa Unity Lobby e cada
  confronto continua usando a Session/Relay 1v1 autoritativa do Card12. Não foi
  introduzido um segundo backend nem outra linguagem, evitando duas fontes de
  verdade para o duelo.
- **Compatibilidade:** a adição explícita do estado `BYE` e da regra de maioria
  elevou o protocolo do lobby para `arcane-tournament-v2`. Todos os participantes
  de um mesmo torneio precisam usar uma versão do jogo que contenha esse protocolo;
  clientes antigos são rejeitados antes de entrar na chave.
- **Espectador:** o campo `allowSpectators` permanece no modelo para evolução e
  compatibilidade do snapshot, mas o modo espectador completo não é exposto na
  interface desta entrega. O próprio PDF 1 o classifica como etapa posterior;
  expor um botão sem visão segura do campo criaria uma função enganosa.
- **Métricas do Core:** são contabilizados somente eventos cuja origem pode ser
  provada pelo protocolo atual. O coletor não inventa motivo de destruição,
  tributo ou autoria de dano quando o evento do Core não traz esse dado. As
  telas exibem cards e rankings previstos no MVP; gráficos avançados continuam
  sendo uma expansão visual, sem perda do resumo persistido.
- **Cancelamento:** o Lobby cancelado fica bloqueado por sua curta janela de
  inatividade em vez de ser apagado imediatamente. Assim os participantes têm
  tempo para receber o snapshot final antes da expiração automática.
- **Vagas e início antecipado:** o PDF funcional prefere preencher todas as
  vagas, mas também descreve BYE para 6/10/12. Por solicitação explícita do
  projeto, `participantLimit` passou a representar a capacidade máxima e a
  opção “início com maioria” permite começar com maioria absoluta. Ao iniciar,
  o Lobby é bloqueado e apenas os presentes compõem a chave/agenda.
- **Validação externa:** testes locais cobrem regras, chaves de 2/4/6/8/10/12/16/32,
  início antecipado 3/4, 5/8, 6/8 e 9/16, pontos com quantidade par/ímpar,
  Best of N, idempotência, persistência, arena e crossplay. Latência real,
  suspensão do Android e troca entre redes dependem de dois aparelhos conectados
  aos serviços Unity e, por isso, pertencem à homologação final.
