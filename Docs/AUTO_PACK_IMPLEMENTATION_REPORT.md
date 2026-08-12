# Implementacao e validacao - geracao automatica de pacotes

Data da validacao: 2026-08-03  
Unity: 6000.5.0f1  
Branch: `codex/auto-packs-devzero`

## Resultado funcional

- O catalogo manteve os 19 pacotes manuais publicados e recebeu 7 pacotes
  automaticos append-only (`auto-pack-0001` a `auto-pack-0007`).
- Cada pacote automatico contem 38 cardIds unicos, custa 25 moedas, possui
  tres previews, origem, versao do gerador, batch, hash e bloqueio de conteudo
  publicado.
- Das 969 cartas oficiais elegiveis, 691 ja eram cobertas e 278 foram
  detectadas como novas. O algoritmo publicou 266 delas e manteve 12 no pool
  pendente por ainda nao atingirem o minimo normativo de 35.
- A pre-visualizacao idempotente confirmou que o mesmo snapshot nao cria um
  oitavo pacote nem altera os sete ja publicados.
- O deck inicial Besta Gladiadora agora possui 40 cartas publicaveis. A carta
  proibida `19613556` (Tempestade Pesada) e substituida, de forma aprovada e
  tematica, por uma terceira copia de `35224440` (Campo de Provas dos
  Gladiadores), que busca um monstro Besta Gladiadora de nivel 4 ou menor.

## Integracao adotada

O projeto nao possuia um catalogo de pacotes em `ScriptableObject`; sua fonte
runtime existente e `Assets/Resources/Shop/PackCatalog.json`. Para nao trocar a
arquitetura funcional da loja, a geracao acrescenta os produtos a esse JSON e
grava `ScriptableObject`s auxiliares de settings, manifesto e metadados. A
compra, o ledger, a abertura pendente e a persistencia continuam usando o
fluxo existente de `DeckRepository`.

Por decisao posterior do responsavel pelo projeto, o recurso de concessao de
moedas pela tecla zero permanece de forma permanente como ferramenta de
desenvolvimento. Ele reside em uma assembly exclusiva do Unity Editor, com
`includePlatforms: Editor` e protecao adicional por `UNITY_EDITOR`; por isso,
nao exige chave, toggle ou remocao manual antes de gerar builds. O antigo
cheat runtime de desenvolvimento continua removido, pois ele entrava em
Development Builds e nao oferecia essa separacao estrutural.

Essa permanencia diverge somente da instrucao de remocao final da secao 9.3 e
do AC-11 do PDF. A divergencia foi solicitada expressamente em 2026-08-03. Os
demais requisitos de seguranca e uso da ferramenta foram mantidos.

## Como usar o credito de +1.000 moedas

1. Execute o jogo com Play no Unity Editor.
2. Entre no Editor de Deck ou abra qualquer tela da loja; nenhuma selecao de
   deck, pacote ou carta e necessaria.
3. Fora de um duelo e sem um campo de texto ativo, pressione `0` na fileira
   superior ou `0` do teclado numerico.
4. A carteira recebe exatamente 1.000 moedas, o ledger registra uma transacao
   `admin-test` unica e o Editor mostra a notificacao `+1.000 moedas (DEV)`.

Segurar a tecla nao repete creditos. Cada novo pressionamento gera uma chave
idempotente diferente. A operacao e bloqueada durante duelo, abertura de
pacote/transacao, pausa, carteira indisponivel ou sem
tela permitida.

## Automacao de Editor

- `Tools/Game/Shop/Auto Packs/Preview Changes`
- `Tools/Game/Shop/Auto Packs/Rebuild Now`
- `Tools/Game/Shop/Auto Packs/Validate`
- `Tools/Game/Shop/Auto Packs/Open Manifest`
- Deteccao automatica por `AssetPostprocessor`, com debounce e espera do Editor
  ficar estavel.
- Validador estrito de pre-build, sem geracao silenciosa durante o build.

## Evidencias de teste

- Ferramenta permanente Editor-only: 14/14 testes focados aprovados, incluindo
  integracao real com carteira e ledger.
- EditMode completo antes do teste de integracao final: 239/239 testes
  aprovados; a classe adicional de integracao tambem passou no conjunto focado.
- PlayMode completo: 47/47 testes aprovados.
- Validador estrito apos reiniciar a Unity:
  `packs=26 auto=7 pending=12 eligible=969`.
- A validacao atual com Android como plataforma ativa enumerou as assemblies
  de Player e confirmou que nenhum fonte `ZeroCoinGrant` entrou no Player.
- Varredura dos Players gerados anteriormente para Windows
  Release/Development e das bibliotecas `libil2cpp.so` Android
  Release/Development: nenhuma ocorrencia de
  `EditorSelectedCardZero`, `SelectedCardZeroCoinGrant`,
  `DevCoinCheatListener` ou `TemporaryCoinGrant`.

## Builds gerados

| Plataforma | Configuracao | Artefato | Tamanho em disco |
| --- | --- | --- | ---: |
| Windows x64 | Release | `Builds/Windows/ArcaneDuel.exe` | 829,63 MiB (pasta) |
| Windows x64 | Development | `Builds/Windows-Development/ArcaneDuel.exe` | 900,03 MiB (pasta) |
| Android ARM64 | Release | `Builds/Android/ArcaneDuel.apk` | 615,59 MiB |
| Android ARM64 | Development | `Builds/Android-Development/ArcaneDuel.apk` | 685,92 MiB |

Os quatro builds terminaram com sucesso antes da decisao de manter a nova
ferramenta Editor-only. Depois dessa decisao, a validacao das assemblies de
Player Android tambem terminou com sucesso. A repeticao do build Windows foi
bloqueada externamente porque o Unity Licensing Client perdeu o canal local;
nao houve erro C# nem falha do validador do projeto antes do bloqueio.

O tamanho atual dos APKs e alto para
distribuicao por loja; isso vem do volume existente de artes/dados e deve ser
tratado em uma etapa propria de Addressables, compressao ou entrega de assets,
sem alterar a logica de pacotes implementada aqui.

## Validacao manual restante

Os testes e builds locais confirmam compilacao, carregamento, compra e
integridade estrutural. Ainda depende de aparelhos reais:

1. instalar o APK Release em um Android ARM64;
2. abrir a loja, comprar um pacote automatico por 25 moedas e concluir sua
   abertura;
3. fechar e reabrir o jogo para confirmar saldo, inventario e ledger;
4. repetir no PC e verificar o mesmo fluxo em resolucoes diferentes;
5. sincronizar o projeto em outra maquina via Git e confirmar que settings,
   manifesto e metadados continuam carregando sem script ausente.

## Relatorios relacionados

- `Docs/AUTO_PACK_AUDIT.md`
- `Docs/Generated/AutoPackGenerationReport.md`
- `Assets/GeneratedReports/StarterDecks/StarterDeckImportReport.md`
