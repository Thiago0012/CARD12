# Arcane Duel - relatório de conformidade do campo

Data: 2026-08-08
Documento-base: `Arcane_Duel_Especificacao_Total_Campo_0_a_100_Codex.pdf`

## Resultado desta etapa

Esta etapa eliminou o uso do código oficial da carta como identidade física na
projeção visual e online, completou o espelho dos metadados persistentes que o
Core expõe e fortaleceu o fluxo de snapshot/ack/resync. O `ygopro-core` continua
sendo a única autoridade de regras; nenhuma regra oficial foi recriada na UI.

## Matriz das fases 0 a 10

| Fase | Estado | Evidência atual | Próximo gate |
|---|---|---|---|
| 0 - Baseline | PARTIAL | Trabalho local preservado; nenhuma alteração alheia foi descartada. | Executar suites pelo Test Runner quando a licença batch estiver disponível. |
| 1 - Inventário de protocolo | PARTIAL | Mensagens usadas por estado, corrente, counters, equip, target, relation, hint e disable field foram auditadas. | Fechar a matriz de todos os valores da API 11.0, inclusive casos N/A. |
| 2 - Inventário de prompts | PARTIAL | Sort, counter e announce múltiplo agora usam resposta tipada sem enumerar/truncar combinações. | Completar a tabela decoder/presenter/encoder/teste para cada prompt da API. |
| 3 - Estado e zonas | PASS de implementação e gate | RuntimeId, owner/controller/location/sequence/position, Extra Deck, overlays, metadata, pilhas privadas por contagem e zonas desabilitadas sobrevivem a snapshot/restauração. A arena visual é religada sempre que o controlador troca o estado local pelo estado online. | Ampliar fixtures por procedimento de invocação. |
| 4 - Ativação multi-efeito | PASS de implementação e gate | O modelo `EffectChoice` preserva requestId, candidateIndex original, RuntimeId, card id, descriptionId, origem, obrigatoriedade, resumo e payload. Ações repetidas da mesma carta abrem uma lista e não enviam o primeiro índice. | Manter estes testes como regressão durante a Fase 6. |
| 5 - Correntes/timing | PASS de implementação e gate | On/Auto/Off, Self Chain, ordem manual/Core, CL1..CLn, solving/solved/negated/disabled e limpeza após reconciliação estão implementados. | Ampliar traces de corrente junto às invocações da Fase 6. |
| 6 - Invocações | PASS de implementação e gate | Normal/Tributo/Ritual/Fusão/Sincro/Xyz/Pêndulo/Link usam candidatos, materiais, somas e zonas do Core. Tentativa, confirmação e negação de invocação são estados distintos também no online. | Manter os golden traces durante a Fase 7. |
| 7 - Batalha/Damage | PARTIAL | Eventos existentes continuam compilando no presenter. | Fixtures attack target/direct/replay, Damage Step, destruição e LP. |
| 8 - Estado raro | PASS nesta etapa | Counters, equip, targets, relations, hints, status, public, link, disable field, sort e announce múltiplo são preservados e apresentados. | Cobrir mudança de controle/reveal com golden traces dedicados. |
| 9 - Multiplayer/resync | PASS estrutural | Snapshot filtrado por destinatário tem hash completo; `null` e placeholders vazios do JsonUtility são normalizados; ack só ocorre após aplicação; falha solicita resync; protocolo v11 rejeita builds antigos. | Smoke real PC/Android, reconnect no meio de prompt e de corrente. |
| 10 - Gates | PASS para as Fases 3-6 / PARTIAL global | A Unity 6000.5.0f1 compilou e executou 104/104 testes EditMode e 46/46 PlayMode no gate atual, sem erro C# ou falha. | Builds Windows x64/Android ARM64 e aparelhos reais permanecem no gate global posterior. |

## Golden fixtures adicionadas/ajustadas

- round-trip de snapshot com zona desabilitada, corrente, counter, equip e target;
- alteração de hash para mutações relevantes de estado;
- preservação dos metadados de prompt estruturado no protocolo;
- hash incluindo o prompt privado efetivamente enviado ao destinatário;
- trace de corrente com identidade física e descrição do efeito;
- parser da consulta autoritativa de metadata do Core;
- respostas tipadas para ordenação, distribuição de counters e máscara múltipla.
- dois candidatos de efeito da mesma cópia física preservando índices 0/1,
  descriptionIds e payloads diferentes no fluxo local e no round-trip online;
- separação explícita entre candidato de ativação e escolha de recusa/passe;
- texto localizado do efeito sem corte fixo que possa esconder a diferença
  entre dois candidatos legais.

## Fase 4 - conferência dos sete requisitos da seleção de efeito

1. Um único candidato pode seguir diretamente ou pela confirmação curta: implementado.
2. Dois ou mais candidatos abrem uma linha por efeito: implementado para mão,
   campo e corrente.
3. A linha usa `descriptionId`/texto localizado sem limite fixo de 150 caracteres:
   implementado.
4. A seleção preserva `candidateIndex` e envia os bytes originais do Core:
   implementado e coberto por golden test compilado.
5. A lista contém somente candidatos presentes no prompt atual: implementado.
6. Target/cost/option/place continuam como prompts posteriores do Core; nenhum
   wizard local foi introduzido: preservado.
7. `CHAINING` consome a decisão e o elo conserva RuntimeId/descriptionId no
   estado local e online: implementado e coberto pelo trace compilado.

O código e o gate automatizado da Fase 4 estão completos em relação aos itens
1-7. Os cenários foram executados tanto no modelo/decoder quanto na arena real,
incluindo dois efeitos da mesma cópia física e reabertura do prompt depois de
uma apresentação de compra.

## Fase 5 - correntes, timing e preferências de resposta

- `SELECT_CHAIN` continua usando exclusivamente candidatos legais e o PASS
  fornecidos pelo Core. Prompt obrigatório ou sem resposta de recusa nunca é
  respondido automaticamente.
- ON conserva todas as janelas opcionais; AUTO conserva as janelas comuns e só
  passa uma oportunidade da própria corrente quando Self Chain foi desligado;
  OFF usa apenas a recusa já emitida pelo Core. Self Chain ligado preserva a
  oportunidade própria inclusive em OFF.
- `SORT_CHAIN` manual preserva `candidateIndex` e converte a ordem visual para o
  mapa de ranks que o ygopro-core realmente espera. O modo automático envia
  `0xFF`, mantendo a ordem de referência do Core.
- O indicador mostra CL1..CLn, carta, número do efeito e estados distintos:
  ativando, encadeado, resolvendo, resolvido, ativação negada e efeito
  desabilitado. Negação/desabilitação não são tratadas como destruição.
- `CHAIN_END` não apaga mais a pilha antes do snapshot autoritativo. Se a query
  do Core ainda não estiver disponível, a reconciliação é repetida com limite
  de frequência, sem perder o pedido.
- A fase visual só é alterada por `NEW_PHASE`; selecionar/passar uma opção de
  fase não cria transição local antecipada.

O código e o gate automatizado da Fase 5 estão completos para os casos P0
Chain01/02/03, Timing01/02 e Sort01. A suíte também percorre preferências de
resposta, passe explícito, auto-pass permitido pelo Core, self-chain, ordenação
e estados distintos de elo negado/desabilitado.

## Fase 6 - procedimentos de invocação

- Normal e Tributo percorrem prompts reais do `ygopro-core`; a UI não consome a
  invocação antes da confirmação e os tributos vêm exclusivamente do prompt.
- Ritual usa `Chaos Form` e material de nível exato; Fusão usa materiais em
  zonas diferentes (campo e mão); Sincro aceita valores alternativos de soma
  somente quando a combinação é validada pelo protocolo do Core.
- Xyz mantém materiais em `LOCATION_OVERLAY`, fora das Monster Zones, e o
  detach preserva o mesmo RuntimeId até o destino autoritativo, local e online.
- Pêndulo executa a escala real, preserva o evento campo→Extra com a face para
  cima e torna pública somente essa parcela do Extra Deck. EMZ e MMZ apontada
  são exibidas apenas quando os respectivos bits estão no prompt do Core.
- Link executa a invocação real na EMZ, preserva rating/markers no snapshot e
  mostra as oito direções de marcador sem calcular zonas legais na UI.
- `SUMMONING`/`SPSUMMONING`/`FLIPSUMMONING` agora abrem um estado pendente. Som,
  cut-in e texto de conclusão só são liberados após o evento `*SUMMONED`; uma
  saída do campo antes dele é registrada como invocação negada.
- Estado pendente/concluído/negado, posição do Extra Deck e visibilidade da
  parcela Pêndulo fazem round-trip nos dois assentos e participam do hash da
  projeção online.

A fixture Pêndulo da parcela virada para cima foi dividida em trace de movimento
autoritativo e trace de prompt de zona. Inserir uma carta diretamente nessa
parcela pela API de preparação não reproduz o evento de destruição de uma
partida real; por isso nenhuma exceção de regra ou atalho de debug foi mantido
no Core.

## Correção crítica encontrada durante o gate

`DuelArenaController.ConfigureNetworkReplica` substituía o
`DuelPresentationState`, mas a arena visual mantinha a referência antiga. O
resultado possível era o host/cliente receber o estado autoritativo enquanto a
mão e o campo continuavam vazios ou desatualizados. A troca agora limpa dados
locais obsoletos e dispara `PresentationStateChanged` imediatamente. A mesma
garantia foi aplicada ao reinício externo/offline antes de o Core emitir o
primeiro evento do novo duelo.

## Compatibilidade online

O protocolo agora é `arcane-duel-online-v11`/NGO 11. PC e Android precisam ser
gerados a partir desta mesma revisão. A rejeição imediata de um cliente antigo é
intencional: misturar o schema anterior perderia RuntimeId/metadados e poderia
reproduzir campo vazio, prompt travado ou confirmação incorreta de estado.

## Validação executada

- Unity Editor `6000.5.0f1`: compilação concluída sem erro C#;
- EditMode do gate das Fases 3-6: **104 executados, 104 aprovados, 0 falhas, 0 ignorados**;
- PlayMode do gate das Fases 3-6: **46 executados, 46 aprovados, 0 falhas, 0 ignorados**;
- log final: zero `NullReferenceException`, `ObjectDisposedException`,
  `OverflowException`, erro de compilação ou chamada proibida em `OnValidate`;
- nenhum `InitTestScene` temporário permaneceu em `Assets`;
- resultados finais: `TestResults/codex-phase6-edit-final.xml` e
  `TestResults/codex-phase6-play-final.xml`;
- todos os resultados foram gravados/movidos para o projeto no disco D; o XML
  automático criado pela Unity em `C:\Users\sousa\AppData\LocalLow` foi
  removido daquele local após cada execução.

Nenhum Player Windows ou APK/AAB Android foi gerado, conforme a decisão de
deixar essa compilação de plataforma para o teste posterior do responsável pelo
projeto. Os plugins nativos existentes foram recompilados da mesma revisão de
fonte para manter paridade binária PC/Android.

## Próxima etapa segura do PDF

Prosseguir pela Fase 7 com golden traces de ataque a alvo, ataque direto,
replay, Damage Step, cálculo/dano, destruição e atualização de LP. A Fase 6 fica
como regressão obrigatória em todos os gates seguintes.
