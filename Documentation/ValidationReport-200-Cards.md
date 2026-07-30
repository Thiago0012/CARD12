# Relatório de validação — catálogo de 200 cartas

Data local: 28/07/2026  
Projeto: `D:\JOGO Y\DO ZERO`  
Unity: `6000.5.0f1`

## Autoridade de regras

- O `ygopro-core` continua sendo a única autoridade para estado, legalidade,
  Correntes, custos, alvos, dano, Invocações e resolução de efeitos.
- A arena Unity apenas apresenta o estado e envia ao core uma resposta que ele
  próprio declarou legal.
- O motor de regras do projeto antigo não foi importado.

## Versões verificadas

- API do core: `11.0`
- `ygopro-core`: `0764db0c75b3d1d574880d365aa3695ab1f13b43`
- `ProjectIgnis/CardScripts`:
  `55607ee511d9697b6eac5dbb689deaa5be712826`
- `ProjectIgnis/BabelCDB`:
  `8d60901db521eb4183ca72560c01a70a6386c98c`

Os três repositórios locais estavam alinhados com seus respectivos ramos
oficiais no momento da validação.

## Catálogo e scripts

- 200 entradas únicas de catálogo.
- 200 artes locais encontradas.
- 172 cartas usam diretamente scripts oficiais.
- 2 cartas usam aliases locais para scripts oficiais.
- 26 cartas não possuem efeito e, portanto, não exigem script individual.
- 0 scripts obrigatórios ausentes.
- 0 erros nativos de carregamento ou inicialização Lua para cartas com efeito.

Distribuição de risco do plano:

- Risco A: 46 cartas.
- Risco B: 89 cartas.
- Risco C: 65 cartas.

## Ponte de protocolo coberta

Além dos comandos já existentes, foram adicionados e testados:

- seleção iterativa e remoção de seleção;
- seleção por soma de materiais, inclusive valores alternativos;
- ordenação de cartas e de elos de Corrente;
- declaração de Tipo, Atributo e número;
- eventos de mudança de posição, baixar, trocar, alvo, batalha, dados e
  contadores como eventos conhecidos de apresentação;
- respostas determinísticas válidas para os novos prompts.

## Testes executados

- EditMode do projeto novo: **37/37 aprovados**.
- PlayMode do projeto novo: **5/5 aprovados**.
- Registro nativo das 200 cartas: aprovado.
- Oito lotes de 25 cartas: cada lote foi registrado em um duelo nativo e
  sobreviveu a três turnos sem `Retry`, prompt não tipado ou mensagem
  desconhecida.
- Vertical slice de 12 cartas: continua chegando a um vencedor pelo core.
- Build Windows Release: concluída com sucesso.

Comparação histórica:

- Projeto antigo: 70/71 testes EditMode aprovados.
- Falha antiga reproduzida:
  `MonsterRebornExecutesThroughCoreKernel`, por definição interna `3001`
  ausente. Essa dependência não foi copiada para o projeto novo.

## Validação visual realizada

Foram capturados e comparados estados equivalentes do projeto antigo e do
projeto novo:

- mão inicial;
- carta selecionada;
- ações contextuais junto à carta;
- inspetor grande à esquerda;
- escolha direta de zona;
- escolha de fase.

A arena nova reaproveita as proporções úteis do campo antigo — câmera FOV 43,
base 18,8 × 16,2, zonas octogonais, mão em leque e foco central — sem possuir
estado de regras próprio.

## Limite objetivo desta validação

Este relatório comprova integridade de dados, presença e inicialização dos
scripts, registro das 200 cartas, integração com o core, cobertura dos prompts
necessários encontrados no catálogo, duelos headless por lote, PlayMode e
build.

Ele não declara que todas as ramificações semânticas de todos os textos foram
provadas. A conclusão definitiva por carta ainda exige os cenários do plano:
ativação legal, ativação ilegal, alvo perdido, negação, uma vez por turno,
interações de Corrente e condições específicas. O campo `test_status` do CSV
permanece `pending` até que cada carta tenha esses cenários reproduzíveis.
