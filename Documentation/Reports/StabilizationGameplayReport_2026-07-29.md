# Arcane Duel — relatório de estabilização da jogabilidade

Data: 29/07/2026  
Projeto: `D:\JOGO Y\DO ZERO`  
Unity: `6000.5.0f1`

## Resultado

- Estado inicial: 52/52 testes EditMode e 9/9 PlayMode aprovados.
- Estado final: 62/62 testes EditMode e 12/12 PlayMode aprovados.
- Build: Windows x64 Release concluída com sucesso.
- Saída: `D:\JOGO Y\DO ZERO\Builds\Windows\ArcaneDuel.exe`.
- Tamanho informado pela Unity: 745.538.314 bytes.
- Pacote final: 673 arquivos, 751.640.421 bytes.
- O `ygopro-core/ocgcore` em C/C++ permanece como a única autoridade
  de regras. Nenhum segundo motor de regras foi criado em C#.

## Causas raiz confirmadas

1. Cópias idênticas eram removidas/reassociadas primeiro pelo código da
   definição, e a seleção visual também era preservada pelo código. Uma
   sequência do Core que apontava para a segunda cópia podia consumir ou
   reassociar a primeira.
2. A mão era destruída e recriada em atualizações desnecessárias, o que
   facilitava callbacks e referências visuais antigas.
3. Decisões do Core não possuíam contexto monotônico próprio na camada de
   apresentação.
4. O cache do campo guardava somente o código por zona. Se o objeto visual
   fosse destruído, o cache podia continuar considerando a zona pronta.
5. Materiais Xyz podiam ser interpretados pelo ramo da Zona de Monstro antes
   do ramo Overlay.
6. `PileLabel` acessava estado/controlador antes da validação completa da zona
   durante inicialização ou hover de objetos decorativos.
7. A avaliação da IA premiava Invocações do Deck Adicional sem comparar
   suficientemente o monstro resultante com o valor dos materiais e sem uma
   assinatura completa de estados repetidos.

## Correções arquiteturais

- Criados `CardInstanceKey` e `CardInstanceState`. O código oficial continua
  sendo a definição compartilhada; cada cópia física recebe `RuntimeId`,
  proprietário, controlador, localização, sequência e posição próprios.
- Movimentos entre mão, campo, Cemitério, banimento e Overlay carregam a mesma
  identidade física. Remoções priorizam a sequência exata indicada pelo Core.
- Criada associação de ações `CoreCardActionBinding`, que combina instância,
  endereço e `RequestId`; ela não cria legalidade.
- Cada prompt emitido recebe `RequestId` monotônico, propagado para suas
  escolhas. Respostas antigas são recusadas.
- A mão reutiliza `CardView` pelo `RuntimeId`. Comprar uma nova cópia não
  recria a anterior, e reordenação visual não modifica o endereço do Core.
- Materiais Xyz possuem coleção própria por monstro hospedeiro.
- Cada carta no campo possui `WorldCardInstanceView` com a identidade
  correspondente. A consistência é verificada após eventos do Core; uma
  representação ausente é recriada a partir do estado autoritativo.
- A animação de ataque valida atacante, zona e objeto visual; tenta
  ressincronizar e cancela de forma segura se ainda não existir representação.
- `DuelZone3D` e `PileLabel` validam identidade, ciclo de inicialização e
  controlador antes de tratar hover/click.
- A IA avalia resultado, materiais, recursos, campo, dano e repetição de
  estado. Invocação-Xyz não recebeu limite artificial.
- Adicionado log estruturado, configurável em desenvolvimento, com categorias
  CORE, CORE_MESSAGE, SELECTION, CARD_INSTANCE, ZONE, UI_BINDING,
  BOT_DECISION, EXTRA_DECK, STATE_SYNC, ANIMATION e ERROR.

## Fluidez e apresentação

- Entrada curta para cartas realmente compradas.
- Reutilização e movimento suave das cartas já presentes na mão.
- Destaque ciano para ações legais e dourado para seleção obrigatória.
- Feedback de indisponibilidade baseado no prompt/prioridade do Core.
- Zonas legais mantêm brilho discreto; a mão se recolhe durante escolha de
  zona.
- Ações contextuais continuam próximas da carta; não foi adicionada interface
  genérica nem painel permanente sobre o campo.
- Clique é bloqueado somente durante a animação crítica de ataque.
- Velocidade/pulo continuam usando `DuelAnimationPreferences`; animações não
  alteram a autoridade do estado.

## Testes adicionados

`StabilizationRegressionEditModeTests`:

- duas e três cópias com identidades distintas;
- movimento da segunda cópia sem consumir a primeira;
- descarte e retorno preservando a instância;
- materiais Xyz vinculados ao hospedeiro;
- seleção independente por sequência;
- invalidação de prompt antigo;
- proteção da IA contra estado sem progresso;
- `RequestId` monotônico real;
- Invocação-Normal e por Tributo conduzidas somente pelos prompts do Core.

`ArenaStabilizationPlayModeTests`:

- hover seguro em todas as zonas e em objeto decorativo não inicializado;
- reparo de uma carta autoritativa cujo objeto visual foi removido;
- fluxo integrado com duas cópias: selecionar a primeira, cancelar localmente,
  selecionar a segunda, mover a segunda, manter a primeira e criar o objeto
  correto no campo;
- reordenação visual sem alterar a referência usada pelo Core.

Os testes existentes de Fusão, Sincro, Xyz e Link continuam aprovados e
executam as opções legais emitidas pelo `ocgcore`.

## Evidências

- `TestResults\stabilization-baseline-editmode.xml`: 52/52.
- `TestResults\stabilization-baseline-playmode.xml`: 9/9.
- `TestResults\stabilization-final-editmode.xml`: 62/62.
- `TestResults\stabilization-final-playmode.xml`: 12/12.
- `Logs\stabilization-final-windows-build.log`:
  `Build Finished, Result: Success` e
  `ARCANE_DUEL_WINDOWS_BUILD_OK`.
- `Builds\Windows\Documentation\build-diagnostics.json`:
  Unity 6000.5.0f1, Core API 11.0, catálogo de 200 cartas.

## Limitações e riscos restantes

- A bateria valida dados e scripts exigidos pelo catálogo compilado e cobre
  os métodos principais de Invocação, mas não constitui prova exaustiva de
  todas as combinações possíveis entre os efeitos das 200 cartas.
- A IA é heurística e escolhe somente entre opções legais do Core; ela ficou
  protegida contra os ciclos reproduzidos, mas não pretende equivaler a uma IA
  competitiva treinada para todos os arquétipos.
- Permanecem avisos de API obsoleta do Editor (`FindObjectsSortMode` e
  `SetApplicationIdentifier`), sem falha de compilação, teste ou build.
- Não houve evidência que justificasse modificar diretamente o código C/C++
  do `ygopro-core`.

## Atendimento da especificação

Todos os itens funcionais, arquiteturais, de instrumentação, testes, fluidez,
ordem de execução e build descritos na especificação foram implementados e
validados. O resultado duplicado que o executor PlayMode gravou
automaticamente em `LocalLow` foi removido; os resultados oficiais, logs,
alterações e a build permanecem no disco D.
