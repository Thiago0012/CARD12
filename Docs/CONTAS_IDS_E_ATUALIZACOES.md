# Contas, IDs e atualizações

Este documento descreve a infraestrutura de identidade do **Master Duel 2
Plus Ultra**. O cliente já contém os fluxos de criação de conta recuperável,
salvamento em nuvem, catálogo numérico de jogadores e atualização na abertura.

## Identidade e salvamento

- A autenticação canônica é o Unity PlayerId. O ID público é sempre numérico,
  possui 12 dígitos e é vinculado a esse jogador pelo catálogo do servidor.
- O nome público pode mudar sem trocar o ID.
- O salvamento local permanece disponível sem rede.
- O Cloud Save compara a revisão local com a remota, restaura a mais recente e
  usa write lock para não sobrescrever silenciosamente outra sessão.
- A Central de Conta permite vincular usuário e senha à identidade anônima
  atual. Isso preserva o PlayerId, o ID numérico e o progresso.
- Ao entrar em outro aparelho com as mesmas credenciais, o jogo troca a sessão
  e restaura o arquivo da nuvem.

Antes de distribuir essa função, ative **Username & Password** em Unity
Dashboard > Authentication > Sign-in providers. As senhas nunca são gravadas
no save nem no repositório.

Cada desenvolvedor deve abrir sua própria instalação, entrar na Central de
Conta e usar **Vincular e proteger** na identidade que já possui. Não copie a
pasta de dados do outro desenvolvedor. `KimDelas` e `xinelodepobre` terão IDs
distintos porque as contas autenticadas serão distintas.

## Catálogo real de IDs

O código do serviço está em `Backend/player-directory`. Ele usa Cloudflare
Worker e D1 e não confia em um PlayerId enviado no corpo da requisição: o
PlayerId é extraído do JWT assinado pela Unity. A tabela `players` impõe
unicidade tanto no Unity PlayerId quanto no ID público de 12 dígitos.

O serviço também possui:

- presença e heartbeat de sessões;
- pesquisa exata por nome normalizado ou ID;
- bloqueios por capacidade;
- benefícios exclusivos associados diretamente ao ID, sem cargos;
- auditoria administrativa;
- índices para crescer além do grupo inicial de testes.

Para publicar, siga `Backend/player-directory/README.md`. Depois copie a URL do
Worker para `Assets/Resources/AccountControl/PlayerIdAccessSettings.json` e
altere `enabled` para `true`. Enquanto essa URL não existir, o cliente mostra
um ID determinístico de contingência, mas bloqueia benefícios exclusivos que
exigem confirmação do servidor.

Nunca coloque `ADMIN_TOKEN`, senha, chave privada ou credencial Cloudflare no
Git. Benefícios futuros são concedidos pelo endpoint administrativo usando o
ID numérico, não o nome do jogador.

## Atualização na abertura

A cena `Assets/Scenes/Login.unity` voltou a ser a primeira cena da build e do
Play Mode. Ela mantém logo, animação e áudio. A verificação acontece em segundo
plano:

- sem versão nova: nenhum elemento de atualização aparece;
- com versão nova: aparece somente o atalho **↻ ATUALIZAR** no canto inferior
  direito;
- enquanto a consulta não termina, o botão Login permanece bloqueado;
- toda versão mais recente é obrigatória: não é possível entrar usando uma
  versão antiga;
- se o servidor de versões estiver indisponível, o jogo não presume que um
  manifesto em cache ainda é o mais recente e oferece **TENTAR CONEXÃO**;
- pacote de conteúdo: baixa no próprio jogo, valida tamanho e SHA-256, extrai
  em staging e só então ativa a nova versão;
- APK/EXE novo: abre a URL oficial da versão para a plataforma, pois um jogo
  instalado não deve substituir o próprio executável silenciosamente.

O manifesto de produção fica em
`ContentStaging/production/release-envelope.json`. A URL configurada aponta
para o arquivo equivalente no GitHub. Portanto, ele só passa a ser remoto
depois que a alteração for revisada, commitada e enviada ao repositório.

Somente no Editor da Unity existe uma exceção de desenvolvimento: se o arquivo
remoto ainda não foi publicado, o Editor usa o manifesto incluído no projeto
para que os desenvolvedores possam testar. APKs e builds Windows nunca usam
essa exceção e permanecem bloqueados sem consultar o servidor.

### Central de publicação

Abra **Master Duel 2 Plus Ultra > Atualizações > Central de Publicação** no
Unity. A ferramenta consulta as alterações do Git e as divide em três grupos:

- `Assets/StreamingAssets/Ygo`: conteúdo instalável dentro do jogo;
- código, cenas, interface, shaders, plug-ins, pacotes e configurações: exigem
  uma nova build;
- documentação, backend e arquivos de publicação: não entram na build.

A central recusa uma publicação de aplicativo sem versão superior, sem um
`versionCode` Android superior ou sem endereços válidos para as builds Android
e Windows. Quando existe conteúdo remoto, ela produz o ZIP, calcula tamanho e
SHA-256 e escreve o manifesto. As marcações obrigatórias são sempre gravadas
como `true`.

### Publicar uma versão do aplicativo

1. Gere e teste as novas builds Android e Windows usando a mesma versão.
2. Publique o APK e o instalador/ZIP do Windows em uma página de release.
3. Informe as duas URLs e a nova versão na Central de Publicação.
4. Clique em **Validar publicação** e depois em **Gerar pacote e manifesto**.
5. Envie primeiro as builds/release e por último o manifesto. Assim nenhum
   jogador recebe uma exigência de atualização antes de o download existir.

### Publicar conteúdo sem rebuild

1. Altere os arquivos em `Assets/StreamingAssets/Ygo`.
2. Informe uma versão de conteúdo maior na Central de Publicação.
3. Gere a publicação; a central monta o ZIP e calcula SHA-256 e tamanho.
4. Envie o ZIP para o endereço HTTPS configurado e, por último, o manifesto.
5. Teste download, integridade, ativação e retomada no Android e no Windows.

Para produção pública, configure uma chave RSA, assine o envelope e altere
`requireSignature` para `true`. A chave privada fica somente no processo de
publicação; o cliente recebe apenas a chave pública.
