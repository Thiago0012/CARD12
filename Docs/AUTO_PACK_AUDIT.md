# Auditoria - pacotes automaticos e ferramenta Dev Zero

Data: 2026-08-03  
Commit-base: `ebf578da07335cbaf4e9b83a38f29b9648c3cf00`  
Branch planejada: `codex/auto-packs-devzero`

## Estado inicial do repositorio

- A arvore de trabalho ja continha uma alteracao do usuario em
  `ProjectSettings/QualitySettings.asset` (`antiAliasing: 0` para `2`). Essa
  alteracao nao pertence a esta feature e sera preservada sem ser reescrita.
- O catalogo publicado possui 19 pacotes e 691 cardIds unicos.
- SHA-256 inicial de `Assets/Resources/Shop/PackCatalog.json`:
  `24DAB4CAA221165A01D74E25D986D1C810D5D1925C24883524C2C6D9446E1398`.
- O primeiro batchmode de linha de base foi interrompido porque o Unity
  Licensing Client perdeu a conexao e repetiu a inicializacao. O log esta em
  `Logs/codex-auto-pack-baseline-compile.log`; nao apareceu erro C# antes da
  falha de licenca. A compilacao sera repetida depois da implementacao.

## Fonte oficial de cartas

- Asset de apresentacao e registro usado pela interface:
  `Assets/Cards/CardCatalog.asset`, classe real
  `ArcaneArena.Cards.CardCatalog`.
- Cada entrada real e `CardCatalogEntry`, com `OfficialCardId`, arte,
  classificacao, flag `OfficiallyRegistered`, `NeedsManualReview` e
  `IsReadyForGameplay`.
- Banco compilado aceito pelo motor:
  `ArcaneDuel.DuelEngine.Data.CardDatabase`, carregado de
  `Assets/StreamingAssets/Ygo/Data/cards.bin` e `card-texts.json`.
- Sincronizador atual: `Assets/Editor/CardCatalogSynchronizer.cs`.
- O snapshot automatico usara o `OfficialCardId` normalizado como identidade,
  exigira entrada oficial/pronta, arte e registro correspondente no
  `CardDatabase`, e excluira tokens ou assets claramente marcados como teste,
  placeholder ou starter-only por configuracao.
- O catalogo possui 969 IDs oficiais unicos. Os 19 pacotes atuais cobrem 691;
  a linha de base possui 278 IDs oficiais ainda nao cobertos.

## Catalogo e fluxo real da loja

- Tipo runtime: `ArcaneArena.Frontend.ShopPackDefinition` em
  `Assets/Scripts/Frontend/ShopEconomy.cs`.
- Fonte persistente real: `Assets/Resources/Shop/PackCatalog.json`.
- Loader runtime: `ShopPackCatalog.LoadDefinitions()` via `Resources.Load`.
- Compra: `DeckRepository.TryPurchasePack`.
- Autoridade existente preservada: preco global de 35 moedas, cinco sorteios
  independentes com reposicao, atualizacao de inventario, ledger idempotente e
  `PendingPackOpeningRecord` salvo antes da animacao.
- UI existente: `GameFrontendBootstrap.ShopEconomy.cs`.
- Validador existente: `Assets/Scripts/Editor/ShopCatalogValidator.cs`.

### Adaptacao ao modelo real

O projeto nao possui `PackDefinition` como ScriptableObject. Para nao criar uma
segunda loja, o writer anexara registros ao JSON real e criara apenas um asset
companion `AutoPackMetadata` para origem, batch, GUID, hashes, lock e previews.
Os 19 registros manuais permanecerao byte-a-byte iguais dentro da lista.

## Economia e ferramenta de desenvolvimento

- Contrato real: `ArcaneArena.Frontend.IWalletService`.
- Implementacao real: `DeckRepository.TryGrantCoins`, que grava transacao
  `admin-test`, usa idempotency key e salva atomicamente.
- Selecao existente no Deck Editor:
  `GameFrontendBootstrap._deckEditorSelectedCardId`, atualizada por
  `ShowDeckEditorCardDetails`.
- O projeto ja contem um `DevCoinCheatListener` no assembly de Player, protegido
  por `UNITY_EDITOR || DEVELOPMENT_BUILD`, e o campo serializado
  `enableDevCoinCheat`. Isso viola a regra nova porque Development Builds ainda
  compilam a classe.
- A implementacao da tecla Alpha0/Numpad0 e isolada em pasta e assembly
  Editor-only. O listener antigo de Player e removido.
- Em 2026-08-03, o responsavel pelo projeto substituiu expressamente a regra de
  remocao final da secao 9.3 e do AC-11: a ferramenta deve permanecer no
  projeto para testes dos desenvolvedores e ser excluida automaticamente de
  toda compilacao PC/Android. A assembly Editor-only e `UNITY_EDITOR` atendem
  essa separacao sem uma etapa manual antes do build.

## Input, assemblies e build pipeline

- Input System esta habilitado e o frontend usa `UnityEngine.InputSystem`.
- Assemblies explicitos: `ArcaneDuel.Game`, `ArcaneDuel.Game.Editor`, suites
  EditMode e PlayMode; scripts em `Assets/Scripts` usam assemblies predefinidos.
- Build automation: `Assets/Game/Editor/ArcaneBuildAutomation.cs`.
- Gates existentes: `ShopCatalogValidator` e `StarterDeckBuildGate`.
- O novo pre-build validator tera ordem anterior aos gates atuais e apenas
  validara. Ele nunca gerara ou corrigira assets durante o build.

## Deck inicial Besta Gladiadora

- Fonte: `Assets/Resources/StarterDecks/starter-deck-sources.json`.
- Asset publicado: `starter_gladiator_control.asset`.
- A lista bruta tem 40 cartas, mas `19613556` (Tempestade Pesada) e proibida na
  banlist ativa; o sanitizador produz somente 39 e bloqueia a escolha inicial.
- Substituicao aprovada: `35224440` (Campo de Provas dos Gladiadores). A lista
  ja possui duas copias; a terceira e legal e adiciona consistencia ao buscar
  um monstro Besta Gladiadora de Nivel 4 ou menor.

## Arquivos cruciais que nao devem ser alterados para contornar a feature

- `Assets/StreamingAssets/Ygo/Data/cards.bin` e scripts Lua.
- Plugins C++/IL2CPP do OCG Core.
- Regras de efeitos, motor do duelo e sincronizacao multiplayer.
- Persistencia, ledger, compra e abertura de pacotes existentes.
- Os 19 registros manuais publicados no catalogo.
- Cenas e prefabs, exceto se uma referencia real e indispensavel for
  identificada; a arquitetura atual nao exige nenhuma.

## Riscos e mitigacoes

| Risco | Mitigacao |
| --- | --- |
| Loop de importacao ao gravar JSON/assets | Debounce, `delayCall`, gate unico e supressao durante commit. |
| Redistribuir packs antigos | Manifesto append-only e hash individual dos 19 registros baseline. |
| Catalogo obsoleto no Player | Pre-build recalcula snapshot/hash e falha com instrucao para Rebuild Now. |
| Carta sem arte ou ausente no Core | Nao publicar; manter diagnostico/pending e bloquear validacao configurada. |
| Inicializacao estatica do catalogo no Editor | Writer opera sobre o JSON serializado; runtime recarrega no proximo dominio/Player. |
| Ferramenta presente no Development Build | Assembly permanente com `includePlatforms: Editor`, guarda `UNITY_EDITOR` e scan das assemblies de Player. |
| Alteracao do usuario em QualitySettings | Nao editar, restaurar ou incluir como parte da feature. |

## Plano de adaptacao

1. Settings/manifest/metadata serializados e validacao normativa 35/38/35.
2. Snapshot deterministico do `CardCatalog` confirmado pelo `CardDatabase`.
3. Particionador puro e Fisher-Yates deterministico com testes de todas as
   bordas da especificacao.
4. Writer transacional do JSON real mais assets companion, sem alterar packs
   manuais.
5. Detector pos-import com debounce, menus Preview/Rebuild/Validate/Open
   Manifest e relatorio em `Docs/Generated`.
6. Gate pre-build estrito, integracao no validador atual e testes.
7. Ferramenta Dev Zero permanente no Editor, testes e exclusao automatica dos Players.
8. Sincronizar o deck Besta Gladiadora com a substituicao aprovada.

## Baseline dos pacotes publicados

Os hashes abaixo usam `packId|displayName|description|cardIds`:

| Pack | Cartas | SHA-256 |
| --- | ---: | --- |
| pack-01-v1 | 37 | `4463EE27ED1388FBD2827503CB8C6306ADA9DD333930AF4501A0B35E109E40FF` |
| pack-02-v1 | 37 | `AB8F6CEE11CC83F74F568B42DC5CBC5DDF19CE591593A3DB97C8AF2D4F839020` |
| pack-03-v1 | 37 | `77F943F4013D9759B27D68AD15732B1FDE544387784794CE9993423A3913EF67` |
| pack-04-v1 | 37 | `8E533AEDBF8D57ED7339457D89DADEAC24CF61E65742CF7D446C7D0A04860507` |
| pack-05-v1 | 37 | `9E353807075540B500717AE9D14C69914EF5DA6C73B30FEC61BB75A3AC7FA27D` |
| pack-06-v1 | 37 | `C3B18752523539869D6F0130DA18CF4C44C746DEFA41347D1604FF93A792FA14` |
| pack-07-v1 | 37 | `FCDFC8712765DD1C817A0CBF4E4478B6D5F40C95D251B2E67FDD8373B0572B9B` |
| pack-08-v1 | 36 | `78EF1B0A73618436C56C2D8FEF25FC888A633455977D043A100BC51BA027152A` |
| pack-09-v1 | 36 | `603D8D979E639920CA32B5143081294EB84E2C5F768BF347653AB71062BC9123` |
| pack-10-v1 | 36 | `1179A06E1126029A91573A987D2ED7EFE18E10DCADE0449765DCB0DF54923CFE` |
| pack-11-v1 | 36 | `00BA4D7C4137A18615CF530FD468C1ED96A52C6D5BA233C61F71179C17B413D6` |
| pack-12-v1 | 36 | `AD423C3A6860548083C163566CAE2FD783ED263CD06AF71E516661AB7D4E3941` |
| pack-13-v1 | 36 | `BB79D3F6EE84691F506363CE5F0D9C546F3D8AFEBC94BF614D61BA4AF8CA9FB8` |
| pack-14-v1 | 36 | `91A1020939033672472FD49DA0E63CAD9D76CE6F5482AA04C4EE26A374AF7DA2` |
| pack-15-v1 | 36 | `CF2D860E411A0DE1266249488AD25889EA1DB4B6F7C4A4B847F2E29CEF36FB59` |
| pack-16-v1 | 36 | `4E8251392E7395D1AA738A3FB2AC028DF7B6D0305F3D28442FDC83FF27EC77EB` |
| pack-17-v1 | 36 | `AF146D72AEFF3F5E629F3D214AAB222B52958157C2D4013DA9856BC5541D8021` |
| pack-18-v1 | 36 | `C158CC0A62AEBE8C93548148095035D5F31F54C9DA49BEC73472052002F8C2AF` |
| pack-19-v1 | 36 | `E74816775FEE8DE4114A3FC4C1B37244E40F9263929B2B8E3AF52D8A4F515EF7` |
