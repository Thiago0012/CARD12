# Cemitério e banimento — Timeu (85899505)

## Resultado da investigação

O `cards.bin` distribuído fornece os tipos ao ocgcore. Cada carta preserva sua
identidade e seus bits de tipo; não há uma lista de Cemitério indiferenciada
usada para calcular efeitos. As listas de códigos em `DuelPresentationState`
são apenas um espelho para a interface, não o motor de regras.

O script efetivamente carregado é `Scripts/official/c85899505.lua`. Seu efeito
usa `aux.FaceupFilter(Card.IsSpell)` nos Cemitérios e banimentos de ambos os
jogadores. O ATK mostrado no campo/inspetor é consultado no Core, não calculado
a partir do tamanho da lista do Cemitério.

Com três Magias elegíveis, uma primeira resolução produz 2800 + 300 = 3100 ATK.
O bônus não tem duração até o fim do turno: uma ativação posterior soma outro
bônus ao ATK já modificado. Portanto, o ATK em uma captura isolada não permite
deduzir quantas Magias foram contadas na última resolução. Não foi reproduzido
o suposto acréscimo por Monstros/Armadilhas; não alteramos a regra oficial para
transformá-la em um bônus contínuo ou apagar ganhos anteriores.

Referência: [texto oficial da Konami](https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=21611&ope=2&request_locale=pt).

## Alteração de interface

O navegador do Cemitério/banimento mostra totais separados de Monstros, Magias
e Armadilhas. Identidades ocultas são contadas separadamente, sem revelar tipo.
O total duplicado no título foi removido. Não houve ordenação das pilhas,
alteração de endereços/índices do Core, filtros nas escolhas legais, alteração
de dados, scripts oficiais, regras de invocação ou protocolo multiplayer.

Essa contagem é descritiva: efeitos podem aplicar filtros adicionais, consultar
outra combinação de zonas ou usar tipos modificados pelo Core. Ela nunca é
usada para decidir o resultado de um efeito.

## Regressão nativa reproduzível

Executar com Python 3 de 64 bits no Windows:

```powershell
python Tools/Tests/verify_graveyard_effects.py
```

O teste usa o DLL, banco binário e scripts realmente distribuídos pelo projeto,
com a mesma prioridade de resolução de scripts do `ScriptRepository`. Verifica:

- Filtros de Magia, Monstro, Armadilha e identidade específica.
- Perspectivas dos dois jogadores e as quatro pilhas relevantes.
- Pêndulo fora do campo como Monstro, não Magia.
- Exclusão de Magias banidas de face para baixo.
- 3100 ATK na primeira resolução com três Magias.
- Acúmulo entre resoluções, recontagem ao resolver, ausência de Magias,
  perda de relação com o efeito e remoção do bônus num reset padrão.

A operação é invocada diretamente com uma relação de efeito nativa; o teste
não simula cliques, a declaração de ataque ou o limite de uma ativação por turno.
As invocações repetidas testam a acumulação de resoluções de turnos diferentes,
não autorizam ativações repetidas no mesmo turno.

## Validação desta alteração

- Regressão nativa acima executada com sucesso.
- `ArcaneDuel.Game`, `Assembly-CSharp` e testes EditMode compilados com Roslyn
  do Unity; sem erros (avisos de APIs obsoletas preexistentes).
- Resumo por tipo executado separadamente contra os tipos de `cards.bin`:
  mistura de tipos, cópias repetidas, Pêndulo, identidades ocultas, pilha vazia
  e preservação da sequência passaram.
- A execução do Test Runner do Unity foi bloqueada pelo serviço de
  licenciamento. Os testes integrados de duelo e a conferência visual do novo
  cabeçalho ainda precisam ser executados no editor; não são reportados como
  aprovados por esta auditoria.
