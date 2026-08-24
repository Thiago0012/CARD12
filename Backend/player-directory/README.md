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
