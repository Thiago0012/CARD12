# Multiplayer 1v1 privado

## O que foi implementado

O modo online usa uma sala privada de dois jogadores com código Relay.
Quem cria a sala é o host e executa a única instância do `ygopro-core`:

```text
cliente remoto -> resposta ao prompt -> host/ygopro-core
host/ygopro-core -> estado filtrado por perspectiva -> cliente remoto
```

O cliente remoto não executa regras, não recebe a mão, o Deck, o Extra
Deck, cartas viradas ou o banimento oculto do adversário. Também não recebe
um prompt quando a prioridade pertence ao host. Toda resposta recebida pelo
host é associada ao `requestId` atual e o Core continua sendo o validador
final dos bytes de protocolo.

## Primeira configuração da Unity

1. Abra o projeto no Unity `6000.5.0f1` e aguarde o Package Manager instalar
   `com.unity.netcode.gameobjects@2.10.0`,
   `com.unity.services.multiplayer@2.2.2` e
   `com.unity.multiplayer.playmode@1.6.2`.
2. Em **Project Settings > Services**, confirme que o projeto está associado
   ao Cloud Project `c699af3d-47e9-4c80-97d5-c33c54cff05b`.
3. No Unity Dashboard desse Cloud Project, habilite **Authentication** e
   **Multiplayer > Relay**. O login inicial usado pelo jogo é anônimo.
4. Abra `MainMenu`, escolha um perfil e um deck válido de 40 a 60 cartas,
   então entre em **DUELAR**.

## Como testar

1. No primeiro executável, selecione **CRIAR SALA PRIVADA** e compartilhe o
   código exibido.
2. No segundo executável, selecione **ENTRAR COM CÓDIGO**, informe o código
   e confirme.
3. O host valida a compatibilidade do Core e o deck remoto. Os dois ficam no
   lobby enquanto o anfitrião não selecionar **INICIAR DUELO ONLINE**.

## Teste no Editor

O seletor `Default` ao lado do botão Play pertence ao Multiplayer Play Mode.
Ele pode abrir até quatro jogadores locais para teste. Use **Window >
Multiplayer Play Mode** para configurar um cenário com dois jogadores e então
teste a criação e a entrada pelo código sem gerar dois executáveis.

Se o host sair, a primeira versão encerra a partida. Migração de host,
reconexão e partidas ranqueadas exigem uma autoridade dedicada e ficam fora
do escopo deste modo privado.
