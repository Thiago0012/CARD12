# ProjectAuditReport — decks iniciais e banlist

Data da auditoria: 2026-08-03  
Projeto: `C:\Unity Projeto\CARD12`  
Especificação: `Especificacao_Decks_Iniciais_Banlist_Unity_Codex.pdf`

## Fluxo e cenas

- `Assets/Scripts/Frontend/LoginIntroController.cs` — a cena `Login` apresenta a entrada local e encaminha para `MainMenu`; não existe autenticação remota nesse ponto.
- `Assets/Scripts/Frontend/GameFrontendBootstrap.cs` — `InitializeScenePresentation`, `ShowPlayerProfileSetup`, `ShowMainMenu` e `TryGetSelectedDuelLoadout` formam o roteamento e a composição da interface atual.
- `ProjectSettings/EditorBuildSettings.asset` — cenas registradas: `Login`, `MainMenu`, `DeckEditor`, `DuelArena`, `Duel` e `CardLab`.

## Perfil, save, inventário e decks

- `Assets/Scripts/Frontend/DeckCollectionState.cs` — estado serializável do perfil local. O identificador estável é `localProfileId`; o nickname é apenas `playerDisplayName`.
- `Assets/Scripts/Frontend/DeckRepository.cs` — persistência em `Application.persistentDataPath/ArcaneArena/decks.json`, normalização, validação para duelo, seleção e escrita atômica.
- `Assets/Scripts/Frontend/DeckRepository.Economy.cs` — inventário por quantidade, recibos idempotentes de transação e rollback por snapshot. A concessão do deck inicial deve reutilizar esse padrão, sem consumir moedas.
- `DeckRecord` e `DuelDeckLoadout` são os formatos do frontend. Ambos agora incluem Side Deck; o loadout também leva `banlistId` e SHA-256 normalizado.
- O deck genérico automático foi removido. Perfis novos não recebem cartas antes do claim; a política legada está configurada por `StarterLegacyPolicy` e preserva todos os dados anteriores.

## Catálogo e motor

- `Assets/Cards/CardCatalog.asset` e `Assets/Scripts/Cards/CardCatalog.cs` — catálogo visual usado pelas telas.
- `Assets/DuelEngine/Runtime/Data/CardDatabase.cs` — catálogo compilado do Core; `CardRecord.Code` é o passcode/código interno numérico.
- `Assets/StreamingAssets/Ygo/Data/cards.bin` e `card-texts.json` — dados locais; nenhum acesso HTTP em runtime.
- `Assets/Game/Runtime/CardVisualCatalog.cs` e `Assets/StreamingAssets/Ygo/Visual/card-visuals.json` — arte, apresentação e vínculo de script.
- `Assets/Game/Runtime/DeckSystem.cs` — `DeckRules` é a validação do snapshot executado pelo Core. A banlist compartilhada será chamada aqui e no frontend.
- `Assets/Scripts/Frontend/FrontendCardRuntimeCompatibility.cs` — filtro visual do frontend; não é autoridade de regras.

## Editor, loja e coleção

- `Assets/Editor/CardCatalogSynchronizer.cs` — sincroniza cartas locais com o catálogo visual.
- `Assets/Scripts/Frontend/GameFrontendBootstrap.cs` e parciais — loja, coleção, editor e detalhes de carta são construídos dinamicamente.
- `Assets/Templates/BanListIcon/forbiden.png`, `1.png` e `2.png` — sprites existentes para os badges. O badge será restrito a Deck Editor, Loja, Coleção e Seleção Inicial; nunca será anexado à arena de duelo.

## Online e autoridade

- `Assets/Scripts/Multiplayer/MultiplayerSessionCoordinator.cs` — publica versão da banlist e SHA-256 de Main/Extra/Side no lobby.
- `Assets/Scripts/Multiplayer/DuelOnlineSession.cs` — `HelloPayload`, `ValidateHello` e `BeginHostMatch` são os pontos de pré-check e validação do host.
- `OnlineDeckLegalityGate` executa o pré-check cliente e a repetição autoritativa no host: banco do Core, arte, script, posição, dimensões, banlist, Side Deck e SHA-256. A mesma validação é repetida imediatamente antes de abrir as arenas.
- A propriedade é verificada localmente por `DeckRepository.TryCreateSelectedLoadout`. O Relay atual é P2P e não possui inventário assinado por backend; portanto, um cliente binário adulterado ainda poderia mentir sobre propriedade, embora não consiga contornar banlist, conteúdo, posição ou hash no host.

## Decisões de integração

1. `BanlistDefinition`, serviço, validador, saneador e hash ficam em `Assets/Game/Runtime`, compartilhados pelo Core, frontend, multiplayer e testes.
2. O onboarding e o claim são parciais pequenos do `DeckRepository`/`GameFrontendBootstrap`; nenhum segundo arquivo de perfil foi criado.
3. `StarterDeckCatalog` e as listas importadas são assets locais geradas no Editor. URLs de origem são somente metadados de auditoria.
4. O claim é atômico e idempotente com `starter-claim:<profileId>` e atualiza inventário, deck salvo, deck ativo e recibo em uma única gravação.
5. Perfis legados usam política configurável. O padrão desta integração é `LegacyPromptOnce`, sem apagar decks ou inventário existentes.
6. Qualquer carta ausente no Core, sem apresentação/script pronto, substituição não aprovada ou fonte indisponível deixa o catálogo marcado como não publicável e bloqueia a build.

## Correção de fonte identificada

O link impresso para `Box Deck` (`https://ygoprodeck.com/deck/box-deck-72444`) responde com `Not Found`. A pesquisa pelo nome exato no catálogo oficial retornou um único resultado contemporâneo, `Box deck`, no endereço `https://ygoprodeck.com/deck/box-deck-724449`. O URL corrigido, com o último dígito `9`, é mantido como origem efetiva e a divergência fica registrada no relatório de importação.
