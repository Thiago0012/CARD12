# Sistema de perfil, ícones e estatísticas

## Escopo implementado

O sistema de perfil usa identidade estável e dados serializáveis. Elementos de
Unity (telas, imagens, máscaras e animações) são apenas apresentação e não são
usados como identidade de jogador, de partida ou de item cosmético.

### Catálogo de ícones

- Catálogo versionado com 10 entradas: um ícone técnico padrão gratuito e os
  nove ícones fornecidos para a loja.
- Cada ícone possui `iconId` estável, nome de exibição e caminho em `Resources`.
- Os nove ícones comerciais custam exatamente 35 moedas cada.
- Texturas são carregadas uma única vez e mantidas em cache; não são recriadas
  a cada abertura de tela.
- O importador limita as texturas a 1024 px, desativa mipmaps e usa `Clamp`,
  reduzindo memória e artefatos principalmente no Android.

### Compra e equipamento

- Compra atômica integrada ao ledger de economia existente.
- Repetir a mesma requisição não desconta moedas novamente.
- Ícones já possuídos não podem ser comprados em duplicidade.
- Equipar ou trocar um ícone possuído é gratuito.
- Perfis antigos recebem automaticamente o ícone padrão por migração.
- IDs ausentes ou inválidos usam o ícone padrão sem quebrar a interface.

### Perfil e estatísticas

A tela de perfil possui as abas `VISÃO GERAL`, `ESTATÍSTICAS` e `ÍCONES`.
As estatísticas são separadas em:

- total;
- online;
- ranqueado.

O estado comporta duelos, vitórias, derrotas, empates, destruições por batalha
e efeito, Magias/Armadilhas destruídas ou ativadas, dano causado, Invocações e
Invocações-Especiais.

Resultados e eventos usam IDs idempotentes para impedir dupla contabilização
durante reconexões ou repetição de mensagens.

### Identidade durante o duelo

No início do duelo é criado um `DuelIdentitySnapshot` contendo:

- `stablePlayerId`;
- apelido;
- `equippedIconId`;
- patente e PE;
- versão do catálogo cosmético.

Esse snapshot é congelado para a partida. Alterar o perfil local durante um
duelo não modifica retroativamente a identidade já confirmada. Duelos locais,
contra bot e online usam a mesma fronteira. Bots recebem ícones de modo
determinístico a partir de sua identidade estável.

### HUD do duelo e proporções

- A mesma placa reutilizável apresenta ícone, apelido, patente e mantém o LP já
  existente para jogador e oponente.
- A placa respeita a interseção entre `Screen.safeArea` e o viewport real da
  câmera da arena.
- Posição e escala autorais da cena são preservadas; a correção só reduz ou
  desloca a placa quando ela sair da área realmente visível.
- A adaptação é recalculada em mudança de resolução, orientação, safe area ou
  viewport, cobrindo Windows, Android, tela cheia, janela e letterbox.
- O retrato usa recorte proporcional em máscara hexagonal, sem distorcer a
  imagem original.

## Fonte autoritativa das estatísticas

Compras e escolhas de cosmético pertencem ao perfil persistente. Eventos de
duelo são contabilizados somente depois de confirmados pelo motor central.
Cliques, animações, objetos da cena e mensagens meramente visuais não alteram
estatísticas.

Destruição por batalha é contabilizada quando o evento confirmado de batalha
identifica a carta destruída. O estado já possui campos para destruição por
efeito e para Magias/Armadilhas destruídas, mas eles não são incrementados por
inferência visual: enquanto o protocolo central não expuser a causa inequívoca
do movimento, o sistema prefere não registrar uma estatística falsa. Uma carta
enviada ao Cemitério, usada como custo, Tributo ou material não é tratada como
destruída.

## Validação executada

- Compilação Unity 6000.5.0f1 concluída sem erros de C#.
- 6 testes EditMode aprovados:
  1. catálogo completo, IDs únicos e preços;
  2. carregamento das nove texturas por `Resources`;
  3. compra atômica/idempotente e equipamento persistente;
  4. snapshot de identidade congelado;
  5. estatísticas por escopo e eventos idempotentes;
  6. interseção de safe area e viewport.

Resultado salvo em `Logs/profile-icons-tests.xml`.

## Arquivos centrais

- `Assets/Scripts/Frontend/PlayerProfileDomain.cs`
- `Assets/Scripts/Frontend/DeckRepository.PlayerProfile.cs`
- `Assets/Scripts/Frontend/GameFrontendBootstrap.PlayerProfileUi.cs`
- `Assets/Scripts/Frontend/HexIconView.cs`
- `Assets/Scripts/Frontend/DuelPlayerPlateView.cs`
- `Assets/Scripts/Frontend/DuelHudSafeAreaFitter.cs`
- `Assets/Resources/Profile/Icons/`
- `Assets/Tests/EditMode/ProfileIconSystemEditModeTests.cs`

## Próxima extensão segura

Para completar destruições por efeito sem heurística, o protocolo autoritativo
deve expor causa, origem e resultado final do movimento confirmado. Depois
disso, o apresentador apenas encaminhará o evento tipado ao mesmo agregador de
estatísticas; nenhuma alteração de HUD ou de economia será necessária.
