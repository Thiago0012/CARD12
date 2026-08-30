# Catálogo real de jogadores

Este Worker transforma o ID público de 12 dígitos em um registro permanente,
único e consultável. O Unity PlayerId autenticado é a chave canônica e nunca é
aceito diretamente do corpo da requisição: o servidor o extrai do JWT assinado
pela Unity.

## Publicação

Produção: `https://card12-player-directory.sousathi12.workers.dev`

O banco D1 e o Worker já estão publicados. Os passos abaixo servem para
republicações e manutenção futura.

1. Instale as dependências com `npm install`.
2. Autentique o Wrangler: `npx wrangler login`.
3. Crie o banco: `npx wrangler d1 create card12-player-directory`.
4. Copie o `database_id` devolvido para `wrangler.jsonc`.
5. Crie o segredo administrativo:
   `npx wrangler secret put ADMIN_TOKEN`.
6. Execute `npm run migrate:remote` e depois `npm run deploy`.
7. Copie a URL final para
   `Assets/Resources/AccountControl/PlayerIdAccessSettings.json` e altere
   `enabled` para `true`.

Nunca coloque `ADMIN_TOKEN`, chaves privadas ou credenciais do Cloudflare no
Git. O token administrativo permite conceder benefícios diretamente a um ID,
sem criar cargos no jogo.

## Operações por ID

Conceder uma função exclusiva:

```text
PUT /v1/admin/player/123456789012/feature/exclusive-account-content
Authorization: Bearer <ADMIN_TOKEN>
```

Revogar a função usa o mesmo endereço com `DELETE`. Bloqueios de capacidade
usam `/block/<chave>`. Todas as alterações ficam registradas no audit log.

## Desafios privados entre amigos

Os convites de duelo usam o mesmo JWT da conta Unity e nunca expõem o código
Relay publicamente. O fluxo persistente é:

1. `POST /v1/duel/challenges` cria um convite Casual ou Ranqueado.
2. O convidado aceita ou recusa em
   `/v1/duel/challenges/<id>/accept|decline`.
3. Depois da aceitação, somente o remetente publica o código privado em
   `/room` e somente o convidado confirma a entrada em `/joined`.
4. `GET /v1/duel/challenges` restaura o convite após troca de tela,
   reconexão ou reinício do jogo.

Convites expiram automaticamente, uma conta não pode manter dois desafios
ativos e qualquer participante pode cancelar antes da entrada na sala.

## Conta privada no site

O jogo envia, junto do `open` e do `heartbeat`, um resumo privado do perfil:
moedas, tamanhos da coleção, decks, cosméticos, pontos de criação e revisão do
save. A revisão é monotônica, por isso um heartbeat antigo não substitui dados
mais recentes.

`GET /v1/player/me` exige o JWT da Unity no cabeçalho `Authorization` e devolve
somente o registro cujo PlayerId veio do token assinado. Essa rota não é usada
pela busca pública. O site guarda o JWT apenas em cookie `HttpOnly` e nunca
armazena a senha do jogador.

O resumo serve para consulta. O servidor não deve usar valores enviados pelo
cliente, como saldo ou coleção, para autorizar compras, recompensas ou vantagens
competitivas.
