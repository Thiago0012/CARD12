# Sistema de atualização — Master Duel 2 Plus Ultra

## Resultado operacional

O cliente consulta um manifesto assinado antes de liberar o login. Uma versão
mais antiga permanece bloqueada até concluir a atualização. O mesmo manifesto
controla Android, Windows, protocolo multiplayer e pacotes de conteúdo.

O catálogo de produção também contém canal, prazo de validade e sequência
monotônica. O cliente guarda a maior sequência assinada que já aceitou e rejeita
catálogos anteriores. Isso reduz ataques de repetição, congelamento e rollback.

## Android

O jogo baixa o APK, valida HTTPS, tamanho, SHA-256, nome do pacote, versionCode
e certificado. Em seguida, usa `PackageInstaller`. O Android pode pedir uma vez
a permissão para instalar desta fonte e sempre pode apresentar sua confirmação
oficial. A chave em `.release-secrets/master-duel-2-plus-ultra.p12` é a identidade
permanente do aplicativo e nunca deve ser enviada ao Git.

O fluxo grava o ID da sessão de instalação. Se o Android encerrar a tela de
confirmação, trocar de aplicativo ou recriar o processo, o jogo não permanece
em 100%: encerra somente aquela sessão temporária e apresenta **REINICIAR
INSTALAÇÃO**. Antes do download, ele também confere espaço para o APK baixado,
a cópia temporária do instalador e uma margem de segurança. A duplicação de uma
APK completa é temporária; depois de sucesso, falha ou recuperação, o arquivo
temporário do jogo é removido.

Uma instalação antiga assinada por uma chave debug diferente precisa ser
desinstalada uma única vez antes da instalação-base definitiva. Os dados devem
estar sincronizados na Unity Cloud antes dessa migração.

## Windows

O jogo baixa o ZIP, valida tamanho e SHA-256 e extrai em uma área isolada. Um
processo auxiliar espera o jogo fechar, guarda cópia dos arquivos substituídos,
aplica a versão e reinicia. Se a cópia falhar, restaura automaticamente o backup.
Os dados do jogador ficam em `Application.persistentDataPath`, fora da pasta que
recebe a troca.

O helper registra o resultado da transação, remove staging e backup depois de
uma conclusão e só limpa diretórios abandonados dentro da pasta privada de
atualizações. Ele mede espaço para extração, backup e troca antes de começar.
Arquivos retirados da nova build só são apagados se já estiverem registrados no
inventário de arquivos controlados pelo atualizador; arquivos do jogador nunca
entram nesse inventário.

## Publicação automática

O comando abaixo automatiza versionamento, builds de release, assinatura,
hashes, GitHub Release em modo rascunho, upload dos dois clientes e publicação
do manifesto por último:

```powershell
& .\Tools\RemoteUpdates\Build-And-Publish-Release.ps1 `
  -Notes 'Correções online|Novo conteúdo'
```

`-Version 1.3.0` e `-ProtocolVersion 2` são opcionais. Sem `-Version`, o patch
da versão publicada é incrementado. O protocolo só deve subir quando clientes
antigos não puderem mais participar das mesmas partidas.

Uma APK de release só pode ser criada com `versionCode` maior que o publicado e
com o cofre de produção. O guard de build bloqueia uma APK de depuração ou uma
APK com `versionCode` repetido antes que ela seja gerada. Portanto, o botão
padrão de Build da Unity não deve ser usado para publicar: use a Central de
Publicação ou `Build-And-Publish-Release.ps1`.

O token é obtido de `GITHUB_TOKEN`, do GitHub CLI ou do gerenciador de
credenciais já usado pelo Git. O token nunca é escrito no projeto.

## Ordem transacional

1. Aplicar a versão e o versionCode.
2. Construir Windows e Android com a chave definitiva.
3. Calcular hashes e assinar o manifesto.
4. Criar um GitHub Release ainda invisível.
5. Enviar ZIP, APK e manifesto.
6. Tornar o release público somente após todos os uploads terminarem.

Se qualquer etapa falhar, o release continua invisível e os jogadores não
recebem uma versão incompleta.

O manifesto assinado usado pelos clientes novos fica em
`ContentStaging/production/v2/release-envelope.json`. O arquivo
`ContentStaging/production/release-envelope.json` conserva temporariamente o
formato antigo para que builds já instaladas consigam atravessar a migração.

## Segredos e recuperação

A pasta `.release-secrets` é ignorada pelo Git. Ela contém a chave do manifesto,
o cofre Android e senhas aleatórias. Deve existir uma cópia protegida fora do
computador e uma cópia segura para o segundo desenvolvedor. Perder o cofre
Android impede atualizar instalações já distribuídas.

## Limites

Conteúdo remoto pode trocar artes, áudio, dados e arquivos preparados para isso.
Scripts C#, plug-ins, shaders compilados e mudanças estruturais exigem novo APK
e novo ZIP. No Android não existe atualização silenciosa universal para jogos
distribuídos diretamente; a confirmação do sistema é uma proteção obrigatória.

## Referências de arquitetura

- Android `PackageInstaller`: sessão de instalação e confirmação do sistema.
- GitHub Releases REST API: publicação transacional de manifesto e artefatos.
- The Update Framework (TUF): metadados assinados, validade e proteção contra
  rollback/freeze.
- Velopack: verificar, baixar, aplicar/reiniciar e separar pacotes por plataforma.
- itch.io Wharf: referência futura para atualizações diferenciais; a primeira
  versão deste projeto prioriza pacote completo e rollback, que é mais simples
  e confiável para a implantação inicial.
