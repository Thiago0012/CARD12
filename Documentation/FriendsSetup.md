# Central de Conexões

## O que já está implantado

- O sino da barra superior abre diretamente a busca de jogadores.
- A busca aceita nome público ou ID de 12 números.
- Há telas separadas para conexões, pedidos recebidos/enviados e adicionar.
- Pedidos recebidos ganham um contador sobre o sino.
- Aceitar, ignorar, cancelar e remover usam o serviço Unity Friends.
- A lista e a presença são persistidas no serviço online, não apenas no
  aparelho do jogador.
- O nome escolhido no perfil é sincronizado como nome público da conta Unity.

O pacote `com.unity.services.friends` está instalado no projeto. As relações
sociais usam o `PlayerId` canônico internamente; o jogador continua vendo
apenas o ID público numérico.

## Ativação no Unity Dashboard

1. Abra o projeto correspondente no Unity Dashboard.
2. Em **Products**, habilite **Friends** no ambiente usado pela build.
3. Confirme que **Authentication** também está habilitado.
4. Publique e configure o endpoint de busca descrito em
   `Documentation/PlayerIdCatalogSetup.md`.
5. Preencha `baseUrl` e habilite o catálogo em
   `Assets/Resources/AccountControl/PlayerIdAccessSettings.json`.
6. Teste com duas contas/dispositivos diferentes: enviar, receber, aceitar,
   remover e reconectar.

Sem o catálogo, relações já existentes continuam aparecendo e um jogador pode
ser adicionado pelo nome Unity completo (`Nome#1234`). A descoberta de uma
conta nova somente pelo nome comum ou pelo ID numérico exige o endpoint do
catálogo.

## Regras de produto e segurança

- Não use busca parcial nem exponha uma lista global de jogadores.
- Rate-limit buscas e pedidos para reduzir spam.
- A opção de bloquear uma conta deve ser adicionada antes de uma publicação
  aberta ao público; o modelo de domínio já reserva o estado `Blocked`.
- Revise no Dashboard os requisitos de segurança, privacidade e notificações
  antes de distribuir a build.
- Vincule contas anônimas a um provedor recuperável antes de tornar amizades
  ou benefícios permanentes.
