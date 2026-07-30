# Arcane Duel — auditoria do deck Mago Negro

Data: 29/07/2026  
Projeto: `D:\JOGO Y\DO ZERO`  
Unity: `6000.5.0f1`

## Resultado desta etapa

- Lista canônica auditada: 50 cartas no Deck Principal e 15 no Deck
  Adicional, correspondendo a 48 códigos únicos.
- EditMode: 68/68 testes aprovados.
- PlayMode: 15/15 testes aprovados.
- O `ygopro-core/ocgcore` continua sendo a única autoridade de regras.
- Nenhuma regra ou efeito de carta foi duplicado em C#.

## Causas confirmadas para os novos sintomas

1. A arena autoral ainda podia carregar `ArcaneAutoStart` de `PlayerPrefs`.
   Nesse estado, decisões destinadas ao jogador eram consumidas
   automaticamente. Um clique posterior ocorria durante outro prompt do Core
   e a interface informava que a cópia não fazia parte da decisão atual.
2. `ShuffleHand` era descartado pelo decodificador. Efeitos que embaralhavam
   ou reorganizavam a mão atualizavam as sequências no Core, mas não na
   apresentação; as ações legais podiam então apontar para outra posição.
3. Os valores de posição `FaceDownAttack` e `FaceUpDefense` estavam
   invertidos na apresentação. Uma Magia/Armadilha baixada com posição `0xA`
   era interpretada como contendo um bit de face para cima, revelando a
   frente da carta.
4. Ao substituir uma carta visível de uma pilha, `Destroy` só removia o
   objeto antigo no fim do quadro. Uma busca imediata ainda podia encontrar
   a visualização antiga e esconder a nova, gerando o alerta de
   `StateSync` sobre carta autoritativa sem `world view`.

## Correções aplicadas

- A arena autoral nunca usa o modo automático persistido para responder pelos
  prompts do jogador.
- O protocolo agora decodifica `ShuffleHand` com jogador, quantidade e ordem
  de códigos.
- A apresentação reaproveita as identidades físicas existentes, reordena as
  instâncias conforme o Core e recalcula seus endereços de sequência.
- As constantes de posição foram alinhadas ao `ocgcore`; uma carta baixada
  exibe somente o verso.
- A visualização antiga é retirada da zona antes da destruição diferida, de
  modo que a substituição autoritativa seja encontrada no mesmo quadro.
- A mensagem de indisponibilidade passou a explicar se é prioridade do
  oponente, janela de Corrente, outra seleção obrigatória ou ausência real de
  ação legal naquela fase/estado.
- A lista do deck Mago Negro foi centralizada em `CuratedDeckLists`, usada
  tanto pela loja quanto pelos testes, evitando divergência entre a lista
  jogável e a lista auditada.

## Auditoria automática carta por carta

Para cada um dos 48 códigos únicos do deck, o teste confirma:

- registro no banco consumido pelo Core;
- nome e dados básicos;
- arte local usada pela arena autoral;
- presença e resolução do script Lua quando o tipo exige script;
- inicialização individual dentro do Core nativo sem `SCRIPT_MISSING` nem
  erro Lua;
- classificação correta entre Deck Principal e Deck Adicional.

Também são executados três confrontos completos Mago Negro contra Mago Negro,
com sementes diferentes. Cada confronto avança por pelo menos oito turnos,
sem `Retry` do Core e sem mensagem desconhecida.

## Regressões específicas

- Embaralhar a mão preserva cada `RuntimeId`, atualiza sua ordem e mantém o
  endereço usado pelas próximas decisões do Core.
- Duas cópias idênticas continuam independentes após selecionar, cancelar,
  reordenar e invocar a segunda cópia.
- A arena autoral não consome automaticamente prompts do jogador mesmo se a
  preferência de depuração estiver persistida.
- Magia/Armadilha baixada mostra o verso e esconde a frente.
- Uma carta de campo ausente é reconstruída antes do uso.
- Duas atualizações consecutivas de Cemitério no mesmo quadro mantêm visível
  a última instância autoritativa.

## Limite honesto da evidência

Esta etapa prova que todas as cartas da lista possuem conteúdo carregável,
scripts inicializáveis, apresentação disponível e que o deck consegue jogar
turnos reais sem falha de protocolo. Ela não constitui prova exaustiva de
cada ramo de todos os efeitos impressos em todas as combinações possíveis.

A próxima camada de qualidade deve ser uma matriz de cenários por efeito,
executada dentro do Core: condição positiva, condição negativa, custo, alvo,
resolução, uma vez por turno, Corrente, mudança de zona e interação com cartas
do próprio arquétipo. Esses testes devem continuar testando o Core, não
reimplementar as regras na Unity.

## Arquivos centrais desta etapa

- `Assets/DuelEngine/Runtime/Protocol/CoreProtocol.cs`
- `Assets/DuelEngine/Runtime/State/DuelPresentationState.cs`
- `Assets/Game/Runtime/DuelArenaController.cs`
- `Assets/Game/Runtime/CuratedDeckLists.cs`
- `Assets/Scripts/CardArenaBootstrap.cs`
- `Assets/Scripts/Frontend/DeckShopCatalog.cs`
- `Assets/Tests/EditMode/DarkMagicianDeckAuditEditModeTests.cs`
- `Assets/Tests/EditMode/StabilizationRegressionEditModeTests.cs`
- `Assets/Tests/PlayMode/ArenaStabilizationPlayModeTests.cs`

## Evidências

- `TestResults/dark-magician-audit-editmode.xml`
- `TestResults/dark-magician-audit-playmode.xml`
- `Logs/dark-magician-audit-editmode.log`
- `Logs/dark-magician-audit-playmode.log`
