# Catálogo e controle direto por ID

## Modelo adotado

O sistema não possui cargos, grupos ou hierarquia. A chave primária é o
`PlayerId` emitido pela Unity Authentication. Cada conta tem exatamente um
registro no catálogo e todas as decisões são anexadas diretamente a esse ID.

O `PlayerId` completo da Unity permanece interno. Na interface é mostrado um
`publicId` estável de 12 dígitos, sem letras, símbolos ou separadores. O
backend deve garantir que esse número seja único antes de persistir o primeiro
registro; o controle autoritativo continua usando o `PlayerId` completo.

Exemplo de registro no servidor:

```json
{
  "playerId": "ID_COMPLETO_DA_UNITY",
  "publicId": "483920175641",
  "blockGameAccess": false,
  "blockedCapabilities": ["ranked"],
  "grantedFeatures": ["exclusive-account-content"],
  "message": "Ranqueada temporariamente indisponível.",
  "firstSeenUtcUnixSeconds": 1787576400,
  "lastSeenUtcUnixSeconds": 1787580000,
  "validUntilUtcUnixSeconds": 1787583600
}
```

Os dois IDs dos criadores podem receber somente
`exclusive-account-content`. Isso não cria uma conta DEV: apenas libera uma
função específica para aqueles IDs. Da mesma forma, um ID pode receber
`ranked`, `online` ou `economy` em `blockedCapabilities` sem afetar outro
jogador. `blockGameAccess` bloqueia a entrada inteira.

## Presença dos jogadores

O jogo autentica a conta antes de abrir recursos online. Quando o catálogo
estiver habilitado, ele envia:

- `POST /v1/player/open` ao entrar;
- `POST /v1/player/heartbeat` a cada 60 segundos;
- o token de acesso da Unity em `Authorization: Bearer ...`;
- um ID aleatório da sessão, nome público do duelista, versão da build e
  plataforma.

O backend deve criar o registro no primeiro `open`, preservar `firstSeen`,
atualizar `lastSeen` em cada chamada e devolver o registro de acesso atual.
Assim o painel administrativo pode listar contas que realmente entraram no
jogo, quando apareceram pela primeira vez e quando estiveram ativas por
último.

## Busca usada pela Central de Conexões

A tela de Amigos consulta o mesmo catálogo para transformar um nome público ou
um ID de 12 números no `PlayerId` canônico exigido pelo Unity Friends:

```http
GET /v1/player/search?query=483920175641
Authorization: Bearer TOKEN_DA_UNITY
```

Resposta encontrada:

```json
{
  "found": true,
  "playerId": "ID_COMPLETO_DA_UNITY",
  "publicId": "483920175641",
  "displayName": "KimDelas",
  "unityPlayerName": "KimDelas#1234",
  "equippedIconId": "profile-default",
  "online": true,
  "message": ""
}
```

Resposta sem correspondência:

```json
{
  "found": false,
  "message": "Nenhum jogador foi encontrado com esse nome ou ID."
}
```

A busca deve ser exata e normalizada, nunca uma enumeração parcial de contas.
O servidor precisa validar o token, impedir que uma conta bloqueada seja
descoberta, aplicar limite de tentativas por jogador/IP e não devolver e-mail,
token ou qualquer dado privado. O nome pode não ser único; nesse caso, o
servidor deve exigir o nome Unity completo com sufixo (`Nome#1234`) ou o ID
numérico.

## Contrato de segurança do backend

O backend precisa cumprir estas regras:

1. Validar o token da Unity e obter dele o `PlayerId` autenticado.
2. Rejeitar a chamada se o `playerId` do corpo não for o mesmo do token.
3. Nunca aceitar `grantedFeatures`, `blockedCapabilities` ou
   `blockGameAccess` enviados pelo jogo.
4. Ler e alterar esses campos somente em armazenamento privado do servidor.
5. Fazer os endpoints de loja, ranqueada e conteúdo exclusivo consultarem o
   mesmo registro. A tela do cliente é apenas a primeira barreira.
6. Devolver `validUntilUtcUnixSeconds` curto para que alterações administrativas
   passem a valer rapidamente.

Uma alteração administrativa é sempre feita pelo ID completo. Exemplos:

```text
liberar conteúdo exclusivo:
  grantedFeatures += exclusive-account-content

limitar somente a ranqueada:
  blockedCapabilities += ranked

retirar a limitação:
  blockedCapabilities -= ranked

bloquear a entrada:
  blockGameAccess = true
```

Não se deve colocar uma lista dos IDs especiais em `Resources`, no APK ou no
repositório Git. Uma lista no cliente pode ser extraída e alterada. O cliente
só sabe que a própria conta recebeu uma liberação depois que o servidor a
confirma.

## Ativação no projeto

O arquivo
`Assets/Resources/AccountControl/PlayerIdAccessSettings.json` vem com o
catálogo remoto desabilitado para que o jogo continue abrindo durante o
desenvolvimento:

```json
{
  "enabled": false,
  "baseUrl": "",
  "heartbeatSeconds": 60,
  "requestTimeoutSeconds": 10,
  "allowOnlineWhenCatalogUnavailable": true
}
```

Depois que a API autoritativa estiver publicada por HTTPS, preencha
`baseUrl`, mude `enabled` para `true` e, quando o serviço estiver estável,
considere mudar `allowOnlineWhenCatalogUnavailable` para `false`.

Sem resposta válida do servidor, o jogo continua permitindo funções comuns,
mas nunca concede `exclusive-account-content`. Isso evita que uma queda do
catálogo libere privilégios por engano.

## Persistência da conta

O login atual é anônimo. O ID permanece no mesmo dispositivo enquanto o token
da Unity for preservado, mas uma reinstalação pode gerar outro ID. Antes de
associar benefícios permanentes aos dois criadores, as contas devem ser
vinculadas a um provedor recuperável de autenticação.
