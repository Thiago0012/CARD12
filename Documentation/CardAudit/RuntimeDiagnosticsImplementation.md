# Diagnóstico runtime silencioso

## Objetivo

Registrar falhas e incompatibilidades sem criar pop-up, painel, aviso ou texto
na tela da partida. O registro não altera decisões do Core, regras, Lua,
projector, IA ou protocolo.

## Persistência

- Unity Editor: `Logs/CardAuditRuntime` dentro do projeto no disco D.
- Windows/Android compilados: `CardAuditRuntime` dentro de
  `Application.persistentDataPath` do aplicativo.
- Formato: JSON Lines, um registro independente por linha.
- Retenção: arquivo atual de até 4 MiB e quatro arquivos rotacionados.
- Proteção: códigos de sala/Relay e seeds são removidos antes da gravação.
- Limite: uma mesma assinatura grava no máximo 20 ocorrências por minuto.

## Ocorrências capturadas

- `Debug.LogError`, assertivas e exceções da Unity.
- Exceções não tratadas e tarefas assíncronas não observadas.
- Script solicitado pelo Core que não foi encontrado.
- Falha de callback/interoperabilidade e buffer inválido do protocolo.
- Log de erro nativo e limite de segurança de processamento do Core.
- Falha ao criar/entrar em sala, incompatibilidade do handshake, violação de
  protocolo e falha ao iniciar o Core autoritativo online.

Cada registro inclui UTC, sessão, severidade, código F00/F01-F10, camada,
componente, fingerprint, plataforma, versão, carta/seat quando conhecidos e
detalhes já sanitizados.

## Catalogação

`Tools/CardAudit/catalog_runtime_diagnostics.py` opera em preview por padrão.
Com `--write`, ele agrupa as assinaturas e atualiza
`RuntimeDiagnosticsCatalog.json` e `RuntimeDiagnosticsCatalog.md`. Uma pasta
copiada de um aparelho Android pode ser adicionada explicitamente com
`--input`; a ferramenta nunca pesquisa o disco C ou o aparelho por conta
própria.

## Validação desta entrega

- `ArcaneDuel.DuelEngine`: compilado com Roslyn da Unity 6000.5.0f1, zero
  erros.
- `Assembly-CSharp`: compilado contra o novo assembly, zero erros; somente
  avisos preexistentes de APIs Unity obsoletas.
- `ArcaneDuel.EditModeTests`: compilado, zero erros.
- Catalogador: executado com sucesso em modo `--write`; catálogo inicial vazio,
  pois nenhuma sessão posterior à instalação do gravador foi iniciada ainda.
- `git diff --check`: sem erro; somente avisos preexistentes de fim de linha em
  arquivos fora desta alteração.
- Marcadores de conflito Git: zero.

O teste EditMode verifica persistência JSONL e remoção de código de sala e seed.
A execução pelo Unity Test Runner permanece para a próxima rodada segura; a
compilação do teste passou, mas ele não é declarado aprovado sem execução.

## Rollback

Remover `RuntimeDiagnosticRecorder.cs`, suas chamadas no Core e na sessão
online, o teste EditMode e o catalogador. Nenhum formato de save, deck, carta ou
protocolo multiplayer foi modificado.
