# Contrato: resultado de execução (PX Tool Box → Nexor)

Caminho de volta do contexto de produção (`docs/integrations/nexor-order-context.md`). Mesmo
modelo do contrato de ida: um arquivo `.json` versionado, gravado e escolhido manualmente pelo
usuário, sem servidor, sem acesso do PX a `nexor.db`, sem dependência de código entre os
repositórios (ver `AGENTS.md`).

Implementado no PX DEV `0.1.0-dev.65` (lado PX). **A leitura no Nexor não existe ainda** — é a
próxima etapa, no repositório do Nexor.

## 1. O que o arquivo diz — e o que não diz

- Diz: "com este trabalho de impressão (rolo + jobHash) carregado, o PX salvou estas saídas,
  nestes caminhos, nestes horários".
- **Não diz status.** Não há campo `status`, `done`, `stage` nem nada parecido: o Nexor é o
  dono do status operacional e decide, ao ler o arquivo, se/como algo muda (transição validada
  do lado dele). O PX nunca afirma que um trabalho "terminou".
- Não traz o conteúdo dos `.cdr`, só os caminhos — que podem não existir na máquina do Nexor
  (mesma regra de `sourceFileReference` no contrato de ida).

## 2. Esquema (versão 1)

```jsonc
{
  "formatId": "PxExecutionResult",          // fixo
  "schemaVersion": 1,                        // inteiro; incompatível → incrementa
  "producedAtUtc": "2026-11-05T19:30:00Z",   // quando o arquivo foi gravado
  "producer": { "app": "PX Tool Box DEV", "version": "0.1.0-dev.65" },

  "context": {                               // copiado do NexorOrderContext carregado no PX
    "formatId": "NexorOrderContext",
    "exportedAtUtc": "2026-10-29T21:00:00Z", // exportedAtUtc do contexto (a "foto" usada)
    "rollId": 482,
    "rollName": "M1_29-10-2026_0013",
    "machine": "M1",
    "installationId": "2678dca3c653",        // só se o contexto trazia
    "databaseId": "73d0ce708f00",            // só se o contexto trazia
    "jobHash": "9f1c2a7e4b3d",
    "pedidoLabel": "Dryfit - João Silva"     // pode ser "" (pedido não informado)
  },

  "outputs": [
    {
      "kind": "escudo",                      // escudo | bandeira | faixa-de-capitao (estável)
      "label": "Escudo",                     // nome exibido
      "layoutName": "Dryfit - João Silva",   // null se "Nome do layout" estava vazio
      "filePath": "Z:\\Neuber\\Saídas\\M1_29-10-2026_0013\\Escudo - Dryfit - João Silva.cdr",
      "savedAtUtc": "2026-11-05T19:12:04Z"
    }
  ]
}
```

- Datas sempre em UTC com `Z`. Arquivo UTF-8 sem BOM.
- **Identificação do trabalho**: `(context.rollId, context.jobHash)`, com `context.databaseId`
  quando presente — as mesmas regras de unicidade do contrato de ida (seções 1-A/1-B: único só
  dentro da mesma base). O Nexor deve conferir `databaseId` antes de associar o resultado a um
  trabalho da base dele, e nunca usar `pedidoLabel` como chave.
- `outputs` nunca vem vazio (o PX só deixa exportar com pelo menos uma saída). Salvar de novo
  no mesmo caminho substitui o registro (fica o horário mais recente).
- Campos desconhecidos devem ser ignorados por quem lê; `schemaVersion` desconhecido deve ser
  recusado com mensagem clara.

## 3. Como o PX produz o arquivo

1. Usuário carrega um contexto de produção (seção Produção).
2. Cada Preparar Escudo/Bandeira/Faixa salvo com sucesso, com o contexto carregado, é
   registrado **só em memória** (o resumo na seção Produção mostra quantas saídas).
3. "Exportar resultado para o Nexor…" abre o "Salvar como" (nome sugerido
   `px-resultado-<rolo>-<jobHash>.json`, pasta da última saída) e grava o arquivo.

Regras do registro na sessão: carregar de novo o **mesmo** trabalho (mesmo `rollId`, `jobHash`
e `databaseId`) mantém as saídas registradas; outro trabalho ou "Limpar contexto" as esquece
(a confirmação de limpar avisa quantas são). Nada disso é gravado em disco até exportar.

## 4. Pendente

- Leitura no Nexor (importar o arquivo, mostrar as saídas no trabalho, decidir transições).
- Smoke test no Corel: `docs/mvp-smoke-test.md`, 4h passo 8.
