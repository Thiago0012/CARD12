# Arcane Duel — índice oficial de fontes de regras

Data de consulta: 29/07/2026  
Escopo adotado: Yu-Gi-Oh! TCG, mecânicas atuais, textos oficiais em
português e `ocgcore` como única autoridade de regras.

## 1. Manual Oficial de Regras — versão 10

- Endereço oficial:
  <https://www.yugioh-card.com/en/downloads/rulebook/SD_RuleBook_EN_10.pdf>
- Cópia analisada: `C:\Users\sousa\Downloads\SD_RuleBook_EN_10.pdf`
- Integridade da cópia: SHA-256
  `82BE14641B2B7940467034A5B14B0D4037835831866537738D34FD9DF5A0F444`
- Assuntos cobertos: estrutura do duelo, zonas, tipos de card, Invocações,
  fases, batalha, Correntes, Spell Speed, informações públicas, Deck
  Principal, Deck Adicional e Side Deck.
- Parte relacionada: configuração geral do duelo, protocolo, estado
  apresentado pela Unity, validação de deck e fluxo de fases.
- Limitações: é uma base de 2017. Não contém todas as revisões posteriores,
  não substitui o texto atualizado de cada carta nem as páginas específicas
  de timing.

## 2. Página brasileira do Manual

- Endereço oficial:
  <https://www.yugioh-card.com/lat-am/pt/rulebook/>
- Assuntos cobertos: ponto de entrada oficial em português para regras,
  políticas e material de apoio da região.
- Parte relacionada: documentação de produto, tutorial e links exibidos ao
  jogador.
- Limitações: funciona como índice; não é uma especificação completa do
  motor e pode encaminhar para documentos em outro idioma.

## 3. Atualização das Regras de 2021

- Endereço oficial:
  <https://www.yugioh-card.com/lat-am/pt/play/2021_rules_update/>
- Assuntos cobertos: Invocações do Deck Adicional em Zonas de Monstros
  Principais, efeitos de ativação que mudam de localização antes da janela
  de ativação, significado de Invocação bem-sucedida, Monstros de Armadilha
  e cards que deixam o campo.
- Parte relacionada: flags de Master Rule do `ocgcore`, localização,
  gatilhos, Zonas de Monstro e sincronização visual.
- Limitações: é um conjunto de alterações, não um manual independente; deve
  ser lido junto com o manual e o texto do card.

## 4. Efeitos Rápidos e prioridade

- Endereço oficial:
  <https://www.yugioh-card.com/lat-am/pt/play/fast-effect-timing/>
- Assuntos cobertos: estado de jogo aberto, direito de ação do duelista do
  turno, respostas após ações, construção de Correntes e passagem de fase.
- Parte relacionada: prompts do Core, prioridade, bot, janelas de resposta e
  bloqueio/liberação da interface.
- Limitações: descreve o fluxo de timing; não define isoladamente a
  legalidade ou a resolução de efeitos individuais.

## 5. Problem-Solving Card Text

- Endereço oficial: <https://www.yugioh-card.com/en/play/psct/>
- Assuntos cobertos: condições antes de dois-pontos; ativações, custos e
  alvos antes de ponto e vírgula; resolução; conjunções; timing; efeitos
  contínuos, de ativação e de gatilho.
- Parte relacionada: auditoria de textos, scripts Lua, prompts de custo/alvo,
  Correntes e testes positivos e negativos por efeito.
- Limitações: explica como interpretar a redação oficial, mas não substitui
  o texto atual do card nem decisões oficiais específicas.

## 6. Regras da Etapa de Dano

- Endereço oficial:
  <https://www.yugioh-card.com/eu/play/damage-step-rules/>
- Assuntos cobertos: início da Etapa de Dano, antes do cálculo, cálculo de
  dano, depois do cálculo e fim da Etapa de Dano; restrições de ativação.
- Parte relacionada: protocolo de batalha, efeitos rápidos, gatilhos,
  destruição, dano e janelas da IA.
- Limitações: trata especificamente a Etapa de Dano; interações individuais
  ainda dependem do texto e do script do card.

## 7. Banco Oficial de Cards em português

- Endereço oficial:
  <https://www.db.yugioh-card.com/yugiohdb/?request_locale=pt>
- Assuntos cobertos: nome, texto atualizado, tipo, atributo, Nível/Classe,
  ATK/DEF, ligações e informações oficiais de cada card.
- Parte relacionada: catálogo visual, matriz carta por carta e referência
  usada para verificar scripts e cenários.
- Limitações: o site é a referência textual, não um pacote de dados para o
  `ocgcore`; o efeito jogável continua dependendo de um registro compatível
  no banco binário e do respectivo script Lua.

## Hierarquia usada no projeto

1. O `ocgcore` decide a legalidade e executa as regras.
2. Os scripts Lua compatíveis descrevem os efeitos individuais.
3. O Banco Oficial fornece o texto atual de cada card.
4. PSCT, timing de Efeitos Rápidos e Etapa de Dano orientam a interpretação.
5. Manual v10 mais Atualização de 2021 formam a base das mecânicas gerais.
6. A Unity apresenta decisões e resultados, sem deduzir ou executar regras.

Master Duel é referência apenas de apresentação e experiência de uso. Ele
não define o perfil TCG, a lista de proibidos ou o texto oficial usado pelo
Arcane Duel.
