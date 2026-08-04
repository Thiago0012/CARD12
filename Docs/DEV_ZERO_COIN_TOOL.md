# Ferramenta Dev Zero - +1.000 moedas

Esta ferramenta e permanente para os desenvolvedores, mas existe somente no
Unity Editor. Ela nao e compilada nos Players de PC ou Android, inclusive em
Development Builds.

## Uso

1. Entre em Play Mode no Unity Editor.
2. Entre no Editor de Deck ou abra qualquer tela da Loja.
3. Pressione `0` na fileira superior ou no teclado numerico. Nao e necessario
   selecionar deck, pacote ou carta.

O saldo aumenta em 1.000, a transacao e salva no ledger como `admin-test` e a
Game View mostra `+1.000 moedas (DEV)`. Solte a tecla antes de pressionar
novamente; segurar nao gera repeticoes.

## Bloqueios intencionais

A operacao e recusada quando:

- nao esta no Editor de Deck nem em uma tela da Loja;
- o jogo esta pausado;
- existe um campo de texto ativo;
- o jogador esta em duelo;
- uma compra/abertura de pacote esta em andamento;
- a carteira ainda nao foi carregada.

## Seguranca de build

- assembly: `Game.Editor.ZeroCoinGrant`;
- plataforma permitida no asmdef: somente `Editor`;
- codigo protegido adicionalmente por `#if UNITY_EDITOR`;
- o validador de pre-build falha caso qualquer fonte da ferramenta seja
  encontrado em uma assembly de Player.

Nao ha toggle para desligar e nenhuma remocao manual e necessaria antes de
publicar o jogo.
