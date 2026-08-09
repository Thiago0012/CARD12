# Auditoria de compatibilidade entre cartas e motor — 2026-08-08

## Objetivo

Validar o caminho completo usado pelos efeitos das cartas: catálogo compilado,
script Lua oficial, `ocgcore`, protocolo gerenciado, prompt de decisão e estado
apresentado pelo campo. A auditoria não adiciona avisos de incompatibilidade à
tela do jogador; falhas técnicas continuam destinadas ao diagnóstico interno.

## Escopo verificado

- 969 cartas publicadas em `card-visuals.json`.
- 1.037 registros compilados em `cards.bin`, incluindo cartas auxiliares e
  fichas criadas indiretamente pelos efeitos.
- 39 duelos automatizados, com lotes de até 25 cartas, dois jogadores
  controlados pelo mesmo executor e 12 turnos por lote.
- Política de teste que aceita ativações opcionais, correntes e invocações para
  exercitar o motor com mais intensidade que o teste determinístico anterior.
- Mensagens que a versão fixada de `ygopro-core` pode emitir e respostas que o
  protocolo envia de volta ao núcleo.

## Defeito estrutural encontrado e corrigido

O catálogo continha as cartas jogáveis, mas não continha todas as fichas
criadas pelos scripts com `Duel.CreateToken`. Quando um efeito solicitava uma
dessas fichas, o callback de leitura de dados lançava `KeyNotFoundException`
dentro da chamada nativa. A corrente era interrompida antes de o núcleo
concluir o efeito, produzindo o comportamento percebido como campo congelado.

Foram adicionados e recompilados os registros oficiais das seguintes fichas:

| Código | Dependência |
|---:|---|
| 02625940 | Spool Token |
| 23331401 | Data Token |
| 26326542 | Shiranui Token |
| 27198002 | Fox Token |
| 27204312 | Primal Being Token |
| 67922703 | Mecha Phantom Beast Token |

Um teste permanente agora varre todas as chamadas `Duel.CreateToken` dos
scripts instalados e falha se uma nova dependência não estiver no banco
compilado.

## Cobertura do protocolo e da apresentação

Foram incluídas as mensagens restantes da versão fixada do núcleo:
`MSG_ROCK_PAPER_SCISSORS`, `MSG_TAG_SWAP`, `MSG_RELOAD_FIELD` e
`MSG_MATCH_KILL`. Pedra-papel-tesoura agora produz um prompt tipado e uma
resposta nativa válida. Pacotes de recuperação/tag são reconhecidos para não
serem confundidos com uma mensagem desconhecida.

A comparação estática entre as chamadas `new_message(...)` do núcleo e o enum
gerenciado terminou com 76 de 76 códigos emitidos reconhecidos e zero códigos
ausentes.

O callback de log nativo também foi isolado: uma falha do backend de log,
inclusive em host de teste ou encerramento, não pode mais escapar pelo callback
e interromper a resolução de uma carta.

## Caso dirigido: Jioh, o Ninja da Gravidade

O teste foi substituído por um cenário semântico controlado, com estado inicial
conhecido e asserções sobre cada cláusula da carta. Em 2 turnos e 23 decisões:

1. Jioh foi Invocado por Invocação-Tributo e abriu o efeito `Stringid(0)`;
2. exatamente dois monstros adversários foram virados para baixo;
3. esses dois monstros deixaram de ser oferecidos para mudança manual de
   posição;
4. um terceiro monstro adversário foi virado para cima no turno seguinte;
5. Jioh abriu o efeito `Stringid(4)` e destruiu exatamente um card adversário;
6. o estado final foi consultado diretamente no snapshot do núcleo.

O resultado foi 2 correntes, 2 mudanças de posição, 1 destruição, zero
`MSG_RETRY`, zero mensagens desconhecidas e zero erros de script. Os IDs reais
`97477975867392` e `97477975867396` também foram preservados na corrente.

## Ativação profissional de efeitos

A interface deixou de tratar todos os efeitos de uma carta como um único botão
genérico. O identificador `Auxiliary.Stringid` emitido pelo núcleo agora é o
`EffectId` estável usado pela apresentação:

- quando uma carta tem mais de uma ativação legal, abre-se uma lista central e
  cada opção mostra sua descrição específica;
- um efeito opcional único ainda usa confirmação sim/não, mas exibe o efeito
  concreto que será ativado;
- custos e alvos continuam sendo solicitados em prompts posteriores do núcleo,
  que só oferece cartas e zonas legais;
- cancelar não confirma nem envia uma ativação diferente;
- o efeito selecionado permanece no `MSG_CHAINING`, no evento de apresentação,
  no pacote online e no cliente remoto;
- a IA e o jogador humano recebem as mesmas opções legais do mesmo núcleo;
- o decodificador de `DescriptionId` da IA foi corrigido de 4 para 20 bits.

O banco compilado contém 1.332 textos auxiliares não vazios; 427 cartas têm dois
ou mais textos. Nem todo texto auxiliar é um efeito ativável, mas a solução é
genérica e não contém exceções de interface por carta.

### Decisão de arquitetura em relação à especificação

A especificação descreve objetos gerenciados separados para condição, timing,
custo, alvo, limite e resolução. Neste projeto, essas responsabilidades já são
implementadas de forma autoritativa pelos scripts Lua oficiais e pelo
`ocgcore`. Elas não foram duplicadas em C#, pois dois validadores independentes
acabariam discordando e poderiam reintroduzir congelamentos. O equivalente
adotado é:

- `EffectDefinition`: o `Effect` registrado pelo script Lua;
- `EffectActivation`: o elo de corrente mantido pelo `ocgcore`;
- `EffectSelection`: `DuelChoice.DescriptionId` mais os bytes de resposta;
- legalidade, custo, alvo e resolução: máquina de estados do núcleo;
- apresentação e multiplayer: espelhos imutáveis do ID e da resposta escolhida.

Assim, a interface continua desacoplada das regras, mas não cria uma segunda
fonte de verdade.

## Resultado da varredura dinâmica

- 39 de 39 lotes concluídos.
- 969 de 969 cartas carregadas e mantidas pelo ciclo nativo.
- 222 cartas distintas tiveram uma corrente efetivamente ativada durante os
  cenários genéricos.
- 0 respostas rejeitadas (`MSG_RETRY`).
- 0 mensagens desconhecidas.
- 0 erros nativos ou de script.

Os 222 efeitos ativados não representam todos os ramos semânticos das 969
cartas: muitos efeitos exigem estados específicos, materiais, arquétipos,
cemitério ou uma ação concreta do oponente. Por isso, a conclusão correta é
que todo o catálogo passou pelo carregamento/ciclo estrutural e a amostra
dinâmica não travou; efeitos contextuais continuam sendo ampliados por testes
dirigidos quando um caso reproduzível é identificado.

## Testes permanentes adicionados ou reforçados

- `CardTokenDependencyEditModeTests`: integridade das dependências indiretas de
  fichas e seus metadados oficiais.
- `NinjaCardEffectSemanticsEditModeTests`: ativação e mudança de posição de
  Jioh no núcleo real, os dois IDs de efeito, bloqueio de posição e destruição.
- `DuelEffectDescriptionResolverEditModeTests`: rótulos distintos por efeito e
  preservação do `DescriptionId` em `MSG_CHAINING`.
- `MultiplayerStateRepairEditModeTests`: duas opções da mesma carta preservam
  IDs/respostas distintos, e a corrente remota mantém carta e efeito públicos.
- `CardCatalogBatchEditModeTests`: dois jogadores, ativação agressiva, dez
  turnos por lote e rejeição de erros nativos.
- `CoreProtocolEditModeTests`: formatos das mensagens e respostas adicionadas.

## Correção da apresentação de efeitos na arena visual

A reprodução na cena `DuelArena` identificou uma espera falsa: o `ocgcore`
oferecia uma escolha legal, mas uma animação ou painel de inspeção podia fechar
a bandeja já marcada como apresentada. O núcleo permanecia corretamente
aguardando a resposta, enquanto a tela aparentava estar congelada.

A bandeja obrigatória agora é recuperada enquanto o mesmo `RequestId` continuar
pendente, fecha painéis informativos que poderiam cobri-la e mantém a pergunta
de efeito como última camada interativa. Efeitos com `DescriptionId` recebem
mais espaço para exibir o texto concreto; selecionar esse texto não abre outra
ficha por cima dos botões de confirmação. O mesmo pedido e o efeito selecionado
também são registrados silenciosamente no diagnóstico local.

Uma reprodução posterior de Jioh registrou o `SelectEffectYesNo` de
`request=191` e a resposta positiva, mas nenhum pacote de alvos chegou ao
Core. A causa era a ficha de inspeção sendo promovida acima da bandeja após o
primeiro clique. A seleção de alvo agora bloqueia essa inspeção, conserva a
bandeja como último elemento interativo e só a fecha depois que
`SubmitCoreResponse` aceitar o pacote. O teste de regressão seleciona dois
monstros, confirma os índices `0,1` em uma resposta única e compara o payload
com `CardSelectionResponse`.

## Roteiro recomendado para aproximar a experiência do Master Duel

As melhorias abaixo não são necessárias para a correção estrutural entregue
nesta auditoria. Elas são o próximo trabalho recomendado, em ordem de impacto.

### P0 — espelho autoritativo completo do campo

O `ocgcore` conhece marcadores, vínculos de equipamento, cartas-alvo,
relações temporárias, zonas desativadas e dicas contínuas. O
`DuelPresentationState`, porém, mantém principalmente endereço, posição,
código, dono e materiais Xyz. O snapshot online e seu hash público repetem essa
limitação. É recomendável acrescentar ao estado por instância:

- marcadores por tipo e quantidade;
- vínculo carta equipada → alvo;
- alvos/relações mantidos por efeitos contínuos;
- máscara de zonas desativadas;
- dicas do jogador e da carta relevantes à interface;
- estado público de efeitos contínuos que alteram ATK/DEF, nome, atributo,
  tipo ou nível.

Esses dados também precisam entrar no snapshot por perspectiva, no hash
público e na ressincronização. Hoje o núcleo resolve essas regras, mas o campo
remoto pode não representar todos os resultados visualmente.

### P0 — cenários dirigidos por condição de efeito

A varredura genérica conseguiu ativar 222 das 969 cartas. Para provar cada
efeito, é necessário gerar cenários a partir das condições do script: carta no
cemitério, monstro adversário com posição específica, materiais válidos,
arquétipo no deck, corrente anterior, dano, destruição e assim por diante. O
objetivo deve ser uma matriz por carta contendo ao menos:

1. condição válida e efeito aceito;
2. condição inválida e ativação corretamente recusada;
3. custo pago e alvo escolhido;
4. resultado final consultado diretamente no snapshot do núcleo;
5. execução igual para o assento 1 e o assento 2.

### P1 — registro/replay determinístico do duelo

Registrar semente, decks normalizados, respostas nativas e hashes de estado por
sequência permitiria reproduzir localmente qualquer travamento relatado no PC
ou Android. O replay deve guardar comandos, não informação privada já
mascarada, e ser reproduzível sem depender da interface ou do Relay.

### P1 — conformidade entre núcleo, protocolo e apresentação

Criar um teste automatizado que compare, em cada atualização do `ygopro-core`,
todos os `MSG_*` emitidos, seus layouts binários, os decodificadores, a
aplicação no estado e o transporte online. A regra de integração deve impedir
que uma nova versão do núcleo seja aceita quando houver mensagem sem decoder,
prompt sem resposta ou evento público ausente do snapshot/hash.

### P1 — corrente, prioridade e feedback visual

O núcleo já controla a legalidade; a interface deve representar claramente a
janela de resposta, o dono da prioridade, os elos da corrente, custos, alvos e
resolução de cada elo. Animações precisam observar o estado autoritativo e não
bloquear o envio de uma resposta. Isso reduz a impressão de congelamento em
efeitos longos e aproxima a leitura de campo do Master Duel.

### P2 — validação contínua de conteúdo

Na importação ou no CI, recompilar o banco e recusar mudanças quando houver
script ausente, `Duel.LoadScript` inexistente, ficha não catalogada, código sem
texto/dados ou deck com dependência fora da versão fixada. O catálogo compilado,
scripts e biblioteca nativa devem compartilhar uma versão de conteúdo única,
incluída também no handshake multiplayer.
