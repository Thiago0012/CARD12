# Arcane Duel — auditoria da configuração de regras

Data: 29/07/2026  
Projeto: `D:\JOGO Y\DO ZERO`

## Conclusão

Antes desta auditoria, o duelo não era um perfil TCG explícito. O C# enviava
ao `ocgcore` apenas `DUEL_MODE_MR5` (`0x2E800`): as mecânicas de campo
modernas estavam habilitadas, mas a ordenação de efeitos de gatilho
simultâneos permanecia no comportamento padrão OCG.

O perfil padrão agora é `TcgMasterRule2021`, com:

- `DUEL_MODE_MR5`;
- `DUEL_TCG_SEGOC_NONPUBLIC`;
- `DUEL_TCG_SEGOC_FIRSTTRIGGER`.

O valor combinado é `0x30002E800`. O flag histórico
`DUEL_TCG_FAST_EFFECT_IGNITION` não foi ativado, porque ele representa a
prioridade antiga de efeitos de ignição e não o TCG atual. Um perfil OCG
explícito continua disponível para testes comparativos, mas não é o padrão.

Essa alteração apenas seleciona opções já implementadas pelo `ocgcore`. Ela
não cria regras em C#.

## Versões e procedência encontradas

### Core nativo

- API do Core: 11.0.
- Repositório fixado: `ygopro-core`.
- Commit:
  `0764db0c75b3d1d574880d365aa3695ab1f13b43`.
- Data do commit: 21/06/2026.
- DLL Windows:
  `Assets/Plugins/Windows/x86_64/ocgcore.dll`.
- SHA-256:
  `DD6FFC53CCBE9151A091C8972E003A1236A913527DEB7C29FA2431A3A71E9477`.

### Scripts Lua

- Repositório fixado: ProjectIgnis/CardScripts.
- Commit:
  `55607ee511d9697b6eac5dbb689deaa5be712826`.
- Data do commit: 27/07/2026.
- Lua: 5.4.8.
- Ordem de procura do projeto: `CustomScripts`, `Scripts`,
  `Scripts/official`.

`CustomScripts` tem precedência e, portanto, qualquer arquivo com o mesmo
código de um card pode substituir o script fixado. Overrides devem ser
catalogados e testados intencionalmente.

### Banco consumido pelo Core

- Origem de compilação: snapshot ProjectIgnis/BabelCDB.
- Commit:
  `8d60901db521eb4183ca72560c01a70a6386c98c`.
- Data do commit: 26/07/2026.
- `cards.bin`:
  SHA-256
  `E9493ACD2EB0BE6CDEC62C06067AE35E48F5609D85048DBFC1564AC3F3A5F8BA`.
- `card-texts.json`: 261 registros em inglês; SHA-256
  `E2D0FAACDF2E9A49B2696DB965274E7728BD4C3D8228CF6F4D52EA6B57EF9F09`.

O banco binário e os scripts são snapshots comunitários compatíveis com o
Core; não são uma exportação oficial da Konami. Por isso, os 48 cards únicos
do deck Mago Negro receberam uma verificação separada contra o Banco Oficial
em português. A fotografia verificável dessa consulta está em
`Assets/StreamingAssets/Ygo/Data/official-tcg-pt-dark-magician-audit.json`.

## Lista de proibidos

Não foi encontrada aplicação de uma lista de proibidos/limitados TCG. A
validação atual controla estrutura e quantidade de cópias, mas não transforma
o jogo automaticamente em um formato competitivo oficial.

Até uma política de formato ser escolhida e versionada, o projeto deve
descrever o duelo como:

> regras gerais TCG atuais, perfil casual, sem lista oficial de proibidos
> aplicada.

Uma lista de proibidos futura precisa ter região, data de vigência e versão
explícitas; ela não deve ser inferida de Master Duel.

## Divergências registradas

1. O Manual v10 é de 2017 e precisa da Atualização de 2021 e das páginas
   específicas de timing.
2. O banco operacional é BabelCDB, enquanto a referência textual é o Banco
   Oficial da Konami.
3. `card-texts.json` está em inglês; a apresentação possui um catálogo
   português separado.
4. Quatro dos 48 textos portugueses do deck Mago Negro divergiam do Banco
   Oficial. Eles foram alinhados e passaram a ser protegidos por hash:
   `41721210`, `59514116`, `65741786` e `98502113`.
5. A lista de proibidos não é aplicada.
6. A presença de registro, arte e script carregável não prova todos os ramos
   do efeito.

## Critérios de regressão adicionados

- O perfil padrão deve continuar TCG e usar exatamente os flags documentados.
- O flag de prioridade histórica não pode reaparecer.
- A lista auditada deve corresponder exatamente às 48 cartas únicas do deck
  real de 50 + 15 cards.
- Nome e texto de cada uma das 48 cartas devem coincidir com o snapshot
  oficial tanto na loja quanto no `CardCatalog`.
- Cada card deve permanecer registrado, ter arte e inicializar seu script
  dentro do Core nativo.
- Duelos completos continuam sendo executados para detectar `Retry`,
  mensagens desconhecidas e quebras do protocolo.

## Limite de cobertura

A matriz textual, o carregamento de todos os scripts e os duelos integrados
não são uma prova exaustiva de cada condição, custo, alvo, Corrente e ramo de
resolução. A cobertura por efeito deve registrar separadamente cenários
positivos e negativos dentro do Core. Enquanto algum desses cenários estiver
pendente, o relatório não deve declarar o deck “100% funcional”.
