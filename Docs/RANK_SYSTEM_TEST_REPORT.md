# Relatório de implementação e validação do sistema ranqueado

Data: 2026-08-10  
Projeto: Card12 / Arcane Duel  
Unity: 6000.5.0f1

## Resultado

O sistema ranqueado foi integrado ao multiplayer existente sem substituir o fluxo casual, o Relay, o motor de duelo, a economia ou o módulo de torneios. O estado competitivo é calculado por um serviço de domínio único, persistido de modo atômico e apresentado somente depois da confirmação autoritativa do resultado.

## Regras implementadas

- 0 a 200 PE, distribuídos em Madeira, Pedra, Ferro, Prata, Ouro, Platina, Diamante e Grão-Mestre.
- Faixas de 25 pontos e deltas por elo definidos pelos documentos.
- Ajuste único por diferença de elo entre os jogadores.
- Empate, partida sem resultado e modo não ranqueado sem alteração de PE.
- Penalidade adicional por abandono confirmado.
- Escudo concedido em promoções de Pedra a Diamante, consumido na próxima derrota normal e ignorado em abandono confirmado.
- Chave idempotente `rank:{rulesVersion}:{matchId}:{stablePlayerId}`.
- Snapshot imutável dos dois perfis antes do início do duelo.
- Política ranqueada ou não ranqueada fixada na criação da sala ou do torneio.
- Oito emblemas associados explicitamente ao enum de elo.

## Segurança e consistência

- O host continua sendo a autoridade do duelo, como no projeto existente.
- Cada instalação valida e grava somente o recibo do próprio perfil estável.
- Versão, hash de regras, identidade, pontos iniciais, versão do estado, oponente e cálculo completo são revalidados antes da gravação.
- Recibos repetidos retornam `AlreadyProcessed` e não repetem pontos nem animação.
- Snapshot obsoleto ou recibo adulterado é rejeitado sem alterar o save.
- A interface não calcula nem grava PE.
- A gravação de pontos e do recibo ocorre na mesma operação de persistência, com restauração do estado em caso de falha.

## Apresentação

- Tela multiplayer preserva o fundo e a linguagem visual existentes.
- Modos Ranqueado, Casual e Torneios possuem painéis próprios no mesmo hub.
- O modo ranqueado mostra emblema atual, próximo elo, três posições visuais, PE, progresso e proteção ativa.
- A tela de resultado segue a ordem: estado anterior, resultado, delta, barra, troca de elo, excesso de pontos e estado final.
- Saída bloqueada durante a animação e botão de salto controlado para o estado final.
- Promoção, rebaixamento e proteção só são mostrados quando constam no recibo confirmado.
- A animação não é repetida quando a transação já havia sido processada.

## Torneios

- O criador pode escolher se o torneio concede PE.
- A política é copiada para o contexto de cada confronto e não pode mudar depois do início da chave.
- Torneios ranqueados reutilizam exatamente o mesmo snapshot, cálculo, recibo e persistência das salas ranqueadas.

## Validações executadas

### Compilação Unity

- `ArcaneDuel.Game.dll`: compilado.
- `Assembly-CSharp.dll`: compilado.
- `Assembly-CSharp-Editor.dll`: compilado.
- `ArcaneDuel.EditModeTests.dll`: compilado.
- Nenhum novo erro C# foi registrado após a compilação final.
- Permanecem apenas avisos preexistentes de APIs Unity obsoletas e mensagens externas de licença/serviços Unity, sem relação com o sistema ranqueado.

### Testes EditMode

Arquivo de resultado: `TestResults/codex-phase3-6-edit.xml`

- Suíte completa selecionada: 160 aprovados, 0 falhas, 0 ignorados.
- Sistema ranqueado: 30 casos aprovados.
- Cobertura ranqueada: todos os limites dos oito elos, deltas de vitória/derrota, diferença de elo, promoção, escudo, abandono, limites 0/200, modo não ranqueado e commit atômico/idempotente do repositório.

A etapa PlayMode foi interrompida intencionalmente depois do sucesso EditMode, pois esta entrega não inclui geração de build nem validação em PC/Android.

## O que ainda exige teste real

- Uma partida ranqueada completa entre duas instalações com o mesmo conteúdo, tanto PC-PC quanto PC-Android.
- Promoção, rebaixamento, proteção e retorno ao menu observados nas proporções de tela reais.
- Torneio ranqueado com mais de um confronto e atualização de todos os participantes.
- Queda física de rede, reconexão e reenvio do recibo em aparelhos diferentes.

## Limitação estrutural conhecida

O projeto ainda usa perfil e PE locais e a autoridade de partida do host. Isso atende ao multiplayer privado atual e contém validações contra inconsistências, mas um ranking público competitivo e resistente a adulteração do dispositivo exigirá futuramente backend de perfil e matchmaking autoritativo. Essa limitação não foi ocultada nem substituída por cálculo na interface.
