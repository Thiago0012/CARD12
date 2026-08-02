# Relatorio de validacao multiplayer 1.2.0

Data: 2026-08-02

## Validacao local concluida

- Compilacao do projeto Unity em batchmode: sucesso, sem erro C#.
- Suite EditMode completa do projeto: 152 aprovados, 0 falhas. Ela cobre
  crossplay, reparo de estado, motor, conteudo e regressoes offline.
- Testes do codec de payload grande: cobrem mais de 256 KiB, ordem invertida,
  duplicata, bloco ausente, corrupcao, metadados conflitantes, limite de pacote
  e ACK/retransmissao.
- Testes de estado: preservacao de runtime IDs/overlays, reaplicacao
  idempotente, cartas ocultas opacas e prompt privado.
- Testes PlayMode especificos da sessao/crossplay: 8 aprovados, 0 falhas.
- Plugins nativos esperados para Windows x64 e Android ARM64 presentes.
- Build Release Windows x64: sucesso.
- Build Release Android: sucesso; APK confirmado como 1.2.0/versionCode 4,
  minSdk 26, targetSdk 36 e `arm64-v8a`.

Os XML finais ficam em `Logs/codex-crossplay-v120-edit-tests.xml` e
`Logs/codex-crossplay-v120-play-tests.xml`. Logs de compilacao e builds ficam
em `Logs/`.

## Artefatos finais

- `Builds/Windows/ArcaneDuel.exe` (usar com toda a pasta
  `Builds/Windows`), SHA-256
  `E113B0D76537E3C1E41AA2623AE145E5869AFB9095CF1771C10B43AFC0E2918E`.
- `Builds/Android/ArcaneDuel.apk`, SHA-256
  `6E81C6B9ADDFD7B1AF24EB35F7071E3CCEB30A23BEAF7AF26CE4B4DA13EEEB37`.

Os dois artefatos carregam a mesma versao 1.2.0 e as mesmas revisoes de Core,
scripts de cartas e banco de dados.

## Matriz que exige dois processos ou aparelhos

Executar com exatamente o mesmo build 1.2.0:

| Host | Cliente | Fluxo minimo |
| --- | --- | --- |
| PC | PC | criar, entrar, decks distintos, iniciar, jogar e sair |
| Android | Android | mesmo fluxo, incluindo background/retorno |
| PC | Android | ambos os sentidos de host e cliente |

Em cada combinacao, testar deck pequeno e grande, jogadas com mao/campo,
invocacao, efeitos, overlays, fim de turno, rendicao e fim de duelo. Durante
uma partida, desligar a rede do cliente por 5 a 15 segundos e confirmar pausa,
reconexao, snapshot e continuidade. Desligar o host deve encerrar a partida.

## Criterios de aceite em aparelho real

- Ambos exibem 2/2 antes da partida e o host so inicia depois dos dois decks.
- O convidado entra na mesma arena e ve a perspectiva correta.
- Nenhuma mensagem excede o limite do transport; nenhuma carta/deck se perde.
- Mao e prompts do adversario continuam ocultos.
- Estado e turno convergem depois de perda, duplicacao ou reconexao.
- Nenhum erro ou excecao nova aparece no Player.log/Android logcat.

A compilacao e os testes locais verificam codigo, serializacao e configuracao,
mas nao substituem a matriz com duas contas UGS e redes reais.
