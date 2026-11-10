# Contrato: contexto de produção (Nexor → PX Tool Box)

> **Atualização (iteração 31)**: `installationId` (iteração 30) sozinho NÃO resolvia o escopo
> de unicidade de `(rollId, jobHash)` — verificado com testes reais, não só presumido (seção
> 1-B, nova). Adicionado `source.databaseId`, gravado dentro do próprio arquivo `printlogs.db`,
> que é o campo que de fato identifica "mesma base de dados". `installationId` permanece,
> redocumentado com sua garantia real (mais limitada do que a versão anterior deste documento
> dava a entender). Ver `docs/ARCHITECTURE.md`, seção 23.
>
> **Atualização (iteração 30)**: implementado e com compatibilidade real confirmada entre os
> dois repositórios (ver `docs/ARCHITECTURE.md`, seções 20–22). Este documento foi revisado
> para refletir a terminologia correta — "contexto de produção"/"trabalho de impressão"/"rolo",
> nunca "pedido" como identificador comercial nem "Job" — e a investigação adicional sobre a
> identidade real de `rollId`/`jobHash` (seção 1-A, nova). O nome TÉCNICO do contrato
> (`formatId: "NexorOrderContext"`) e os nomes de campo (`rollId`, `jobHash`, `pedidoLabel`...)
> permanecem por compatibilidade com arquivos/código já existentes — só o texto visível ao
> usuário mudou.

Ver também `docs/ARCHITECTURE.md`, seção 1, para as responsabilidades do ecossistema (Nexor
dono do planejamento/status operacional da produção; ListForge dono do processamento da lista;
PX dono da execução no Corel) — este contrato não altera essa divisão.

## 0. Achado mais importante — o que o Nexor REALMENTE tem hoje

Antes de qualquer campo, o fato que molda todo o resto deste contrato: **o Nexor não tem uma
entidade de "pedido de cliente"** (com identificador estável, número, cliente, etapa/status)
hoje. Isso foi confirmado inspecionando o código real do repositório atual do Nexor
(`C:\Projetos\ProjetoJocasta` — ver `docs/ARCHITECTURE.md`, seção 14, para como esse é o
repositório real em uso, a partir do rebrand documentado no próprio `CHANGELOG.md` dele), nunca
suposto a partir de nomes de tabela ou de versões antigas.

**Esquema real** (`core/printlogs_db.py`, SQLite):
- `rolls` — a unidade real selecionável pelo usuário: `id` (PK, autoincrement, estável),
  `roll_name`, `machine`, `export_mode`, `created_at`, `source_hash` (UNIQUE), `app_version`,
  `notes`.
- `orders` — apesar do nome da tabela, é uma linha de TRABALHO DE IMPRESSÃO dentro de um rolo,
  não um pedido de cliente: `id` (PK), `roll_id` (FK → `rolls`), `end_time`, `document`,
  `fabric`, **`pedido` (texto livre)**, `tipo`, `height_mm`, `vpos_mm`, `real_m`, `source_path`,
  `observacao`, `job_hash` (único DENTRO do rolo, não necessariamente entre rolos diferentes).
- `order_adjustments` — correções manuais de metragem, não metadados de pedido.
- **Não existe**: tabela de clientes, número de pedido distinto do texto livre `pedido`, nem
  qualquer coluna de status/etapa/workflow em lugar nenhum deste esquema.

O campo `pedido` é texto livre, extraído por regex do nome do arquivo de origem
(`core/print_types.py`, padrão `^\s*\d{1,2}[-/.]\d{1,2}\s*-`, ex.: "16-10 - Dryfit - Nome do
pedido") e pode ser re-derivado numa reclassificação — **não é um identificador estável**, só
uma etiqueta. Há um flag `pedido_manual` (bool) indicando se o operador corrigiu essa etiqueta
manualmente. A interface do usuário (`modules/registros/ui.py`, `PXSearchOrdersUI`) seleciona
primeiro um ROLO, depois vê os "Pedidos no rolo" como uma sublista agrupada por essa etiqueta —
um `pedido` individual não tem tela de detalhe nem ID próprio exposto ao usuário.

**Consequência para este contrato**: `OrderContext`, como o nome sugere ("contexto de PEDIDO"),
não corresponde a nada estável no Nexor hoje. Este documento usa o nome `OrderContext` porque foi
o termo pedido, mas o campo que ele representa de verdade é **um trabalho de impressão
específico dentro de um rolo** — identificado de forma estável pelo par `(rollId, jobHash)`,
nunca pelo texto livre `pedido` isoladamente. Isso é registrado como lacuna explícita (seção 5),
não resolvido por suposição.

## 1-A. Identidade real de rollId/jobHash (investigado na iteração 30)

Lido o código real (`core/printlogs_db.py`) para responder, com evidência, o que antes era só
presumido:

- **`jobHash` é determinístico, não aleatório**: `make_job_hash(machine, end_time, document,
  height_mm) = sha1(machine|end_time|document|height_mm)`. NUNCA depende de `pedido`/`fabric`/
  `tipo` — reclassificar um trabalho (mudar a etiqueta de pedido) não muda seu `jobHash`.
- **Estabilidade ao reeditar um rolo**: a rotina que atualiza um rolo já exportado (ex.: via
  "Editar rolo" na Operação) faz `DELETE FROM orders WHERE roll_id = ?` seguido de reinserção —
  ou seja, o `id` interno de cada linha de `orders` MUDA a cada edição, mas `job_hash` (função
  pura do conteúdo) permanece o MESMO, porque as entradas usadas no hash não mudam. Por isso o
  contrato usa `jobHash`, nunca `orders.id` — confirmado, não só presumido.
- **`rollId` é estável para o mesmo rolo**: a mesma rotina faz `UPDATE rolls ... WHERE id = ?`
  (nunca recria a linha do rolo) — `rollId` nunca muda para um rolo já existente.
- **Mover um trabalho entre rolos**: não existe essa operação no Nexor hoje (confirmado
  buscando por qualquer `UPDATE orders SET roll_id` ou função de "mover"/"transferir" em todo o
  repositório — nenhuma encontrada). Se essa funcionalidade vier a existir, o par
  `(rollId, jobHash)` deste trabalho mudaria (o `rollId` mudaria) — reservado como risco futuro,
  não um problema atual.
- **Escopo de unicidade — confirmado, não presumido**: `UNIQUE(roll_id, job_hash)` é uma
  restrição real do esquema SQLite. `rollId` é a chave primária autoincrement de UMA base
  `printlogs.db` — nunca única entre bases/instalações diferentes. `jobHash`, sendo
  determinístico a partir de dados de conteúdo (não um UUID aleatório), também pode colidir
  entre instalações diferentes que processem dados parecidos (mesma máquina/hora/documento/
  altura — improvável, mas não impossível). **Conclusão: `(rollId, jobHash)` só é garantido
  único DENTRO da mesma instalação/base — nunca globalmente.**
- **Correção implementada para essa lacuna**: `source.installationId` (novo campo opcional,
  seção 2) — um id curto e opaco, gerado uma única vez por instalação do Nexor
  (`core/config.get_installation_id`, persistido em `%APPDATA%\PXCore\config.json`) e incluído
  na exportação. Serve só para o PX conseguir notar que dois contextos com o mesmo
  `rollId`/`jobHash` vieram de instalações DIFERENTES — nunca uma garantia formal de unicidade
  global, e nunca um dado de pedido comercial. Arquivos exportados antes desta mudança
  continuam sendo aceitos normalmente (campo ausente é só "não identificado", nunca um erro).

## 1-B. installationId não resolve o escopo da base — verificado, não presumido (iteração 31)

A entrega anterior (seção 1-A) propôs `source.installationId` para desambiguar `(rollId,
jobHash)` entre bases diferentes. Testado explicitamente (ver `tests/test_database_identity.py`
no repositório Nexor) nos cenários reais que o `installationId` deveria cobrir — e NÃO cobre:

- **Uma instalação, duas bases** (`test_uma_instalacao_duas_bases_...`): a mesma instalação
  (mesmo `installationId`, vem da config local) pode apontar para arquivos `printlogs.db`
  diferentes (reconfiguração de `base_dir`). **`installationId` sozinho NÃO distingue — reporta
  o MESMO valor para bases diferentes.**
- **Duas instalações, mesma base** (`test_duas_instalacoes_mesma_base_...`): `base_dir` pode
  ser uma pasta de rede compartilhada entre vários computadores (confirmado no comentário real
  do `journal_mode` em `core/printlogs_db.connect()` — não é um cenário hipotético, é o motivo
  documentado da escolha de `journal_mode = DELETE`). Duas instalações diferentes (dois
  `installationId`) acessando a MESMA base reportariam contextos como "de origens diferentes"
  quando são, na realidade, a MESMA base.
- **Cópia da configuração local** (`test_copia_da_configuracao_local_...`): copiar
  `%APPDATA%\PXCore\config.json` para outra máquina faz as duas reportarem o MESMO
  `installationId`, mesmo com bases `.db` completamente diferentes (não relacionadas).

**Conclusão**: `installationId` identifica a instalação/máquina que executou a exportação —
nunca a base de dados em si. Não é um erro de implementação (fazia exatamente o que foi
desenhado para fazer), mas o nome e a documentação anteriores deste repositório sugeriam uma
garantia mais forte do que a real — corrigido aqui e no comentário de
`OrderContextService.cs`.

**Evolução mínima e compatível implementada**: `source.databaseId` (novo campo opcional,
schemaVersion permanece 1 — aditivo) — gerado uma única vez e gravado dentro do próprio
arquivo `printlogs.db` (tabela `db_identity`, ver `core/printlogs_db.get_database_id`). Por
estar DENTRO do arquivo, e não em configuração externa a ele:
- **Cópia/restauração da base em outra máquina** (`test_copia_ou_restauracao_da_base_...`):
  preserva o MESMO `databaseId` — confirmado copiando os bytes do arquivo e lendo de novo.
- **Duas instalações, mesma base compartilhada**: reportam o MESMO `databaseId` — confirmado
  conectando duas conexões SQLite independentes ao mesmo arquivo.
- **Uma instalação, duas bases**: reporta `databaseId` DIFERENTE para cada uma — confirmado.
- **Corrida na primeira criação** (duas instalações abrindo uma base de rede recém-criada ao
  mesmo tempo): protegido com `rowid` fixo + `INSERT OR IGNORE` — as duas convergem no mesmo
  valor, nunca duas linhas (`test_corrida_na_primeira_criacao_...`).

**Ainda não é uma identificação global formal**: nada impede, em teoria, que dois arquivos
`.db` completamente diferentes recebam o mesmo `databaseId` por coincidência de um UUID de 12
hex chars (probabilidade desprezável, não zero) — por isso a documentação e a interface nunca
tratam isso como prova absoluta, só como o melhor sinal disponível hoje. **Decisão explícita**:
não foi implementada nenhuma infraestrutura adicional (hashing do conteúdo da base, detecção de
pasta de rede, registro central de bases) — o uso real hoje é só EXIBIÇÃO para um humano
decidir, nunca correspondência/mesclagem automática entre contextos, então uma garantia mais
forte não tem necessidade concreta ainda (ver AGENTS.md).

## 1. Entidades e distinções pedidas

- **Identificador interno vs. número exibido**: o Nexor não tem os dois separadamente. O único
  identificador estável real é `(rollId, jobHash)` — um número técnico, nunca mostrado ao
  usuário como tal. O texto que o usuário VÊ e reconhece é `pedidoLabel` (a etiqueta livre) —
  mas essa etiqueta pode mudar numa reclassificação e não é garantidamente única. Não existe
  hoje um "número de pedido" distinto de `pedidoLabel` como o pedido do usuário presumia.
- **Dados do pedido vs. estado de execução local**: `order` (seção 2) é o que vem do Nexor;
  qualquer processamento feito no PX/ListForge depois de importar o contexto (ver
  `docs/mvp-smoke-test.md` e a próxima entrega de importação) é estado local do PX, nunca
  escrito de volta neste arquivo nem no Nexor.
- **Referências a arquivos vs. conteúdo dos arquivos**: `sourceFileReference` é só o caminho/
  nome do arquivo de origem já registrado pelo Nexor (`source_path`) — nunca o conteúdo da
  lista/arquivo. O PX decide, na etapa de importação, se e como localizar esse arquivo na
  própria máquina (pode nem existir lá).
- **Informação atual vs. contexto possivelmente desatualizado**: `exportedAtUtc` marca o
  instante da exportação; o PX trata o contexto importado como uma FOTO daquele instante,
  nunca uma consulta em tempo real (ver seção 6, "Contexto recebido — não sincronizado").

## 2. Esquema mínimo (versão 1)

```jsonc
{
  "formatId": "NexorOrderContext",       // identificador fixo do formato
  "schemaVersion": 1,                     // inteiro — versões incompatíveis incrementam isto
  "exportedAtUtc": "2026-10-29T21:00:00Z",

  "source": {
    "rollId": 482,                        // rolls.id — estável, real, dono: Nexor
    "rollName": "M1_29-10-2026_0013",     // rolls.roll_name — texto livre, só exibição
    "machine": "M1",                      // rolls.machine
    "installationId": "2678dca3c653",     // opcional — por instalação/máquina; ver seção 1-B (garantia LIMITADA)
    "databaseId": "9c4a1e7fbd02"          // opcional — gravado dentro do próprio .db; ver seção 1-B (mais confiável p/ "mesma base")
  },

  "job": {
    "jobHash": "9f1c2a7e4b3d",            // orders.job_hash — estável DENTRO do rollId acima;
                                            // NUNCA usado isolado sem rollId (não é único entre rolos)
    "pedidoLabel": "Dryfit - João Silva",  // orders.pedido — texto livre, pode mudar numa reclassificação
    "pedidoManual": false,                 // orders.pedido_manual — true se o operador corrigiu a etiqueta
    "fabric": "Dryfit",                    // orders.fabric, opcional
    "tipo": "Camiseta",                    // orders.tipo, opcional
    "sourceFileReference": "D:\\Impressao\\M1\\16-10 - Dryfit - Joao Silva.pdf" // orders.source_path — SÓ referência, nunca o conteúdo
  }
}
```

**Campos obrigatórios**: `formatId`, `schemaVersion`, `exportedAtUtc`, `source.rollId`,
`job.jobHash`, `job.pedidoLabel`. Todos os demais são opcionais — um importador que não os
encontra trata como "não informado", nunca como erro.

**`job.pedidoLabel` pode vir vazio** (PX DEV 0.1.0-dev.62): o exportador real grava
`row.get("pedido") or ""`, ou seja, `""` quando o trabalho não tem pedido informado no Nexor. O
importador do PX trata vazio — e, por tolerância, ausente ou `null` — como "pedido não
informado", sem recusar o arquivo (antes recusava, conferido com arquivo do exportador real). O
exportador continua gravando a chave; nada muda no lado do Nexor.

**Versões incompatíveis**: um `schemaVersion` que o importador não reconhece deve ser rejeitado
explicitamente (mensagem clara), nunca uma tentativa de interpretar campos parcialmente.

Ver `docs/integrations/order-context-example.json` para um exemplo fictício completo e
autocontido (não copiado de nenhum dado real do Nexor).

## 3. Responsabilidades (preservadas, não alteradas por este contrato)

- O Nexor continua dono do rolo/trabalho de impressão, do planejamento e do status
  operacional — este contrato é só uma FOTO exportada manualmente, nunca um canal de consulta
  nem de escrita de volta.
- Receber este contexto **não autoriza** o PX a alterar nada no Nexor (nenhuma escrita em
  `nexor.db`, nenhuma chamada ao Nexor), nem a abrir/modificar automaticamente nenhum documento
  do Corel a partir dele.
- O ListForge continua dono do processamento da lista — este contrato não inclui o conteúdo de
  nenhuma lista, só a referência de arquivo de origem já registrada pelo Nexor.
- Nenhuma referência às classes internas de nenhum dos dois produtos — o contrato é só o JSON
  acima, consumido por um importador próprio do PX (próxima entrega) e produzido por um
  exportador próprio do Nexor (entrega subsequente), sem nenhuma dependência de código entre os
  dois repositórios.

## 4. Transporte recomendado para a primeira entrega

Alternativas comparadas, dado que os dois aplicativos são processos desktop independentes, sem
servidor compartilhado hoje:

| Opção | Avaliação |
|---|---|
| Arquivo de intercâmbio (.json), escolhido manualmente pelo usuário nos dois lados | **Recomendado para a primeira entrega** — nenhuma infraestrutura nova, nenhuma porta/serviço, funciona mesmo em máquinas diferentes, nenhum dos dois processos precisa estar aberto ao mesmo tempo. |
| Pasta compartilhada com convenção de nome fixo | Economiza um clique de "Salvar como", mas presume uma pasta de rede/sincronizada já configurada — não garantido em todo ambiente; fica como evolução futura, não a primeira entrega. |
| Named pipe / socket local | Exige os dois processos abertos ao mesmo tempo na MESMA máquina — não é o caso típico (Nexor normalmente numa estação de produção, PX na estação de arte/corte); descartado para esta fase. |
| API HTTP local / servidor embutido | Infraestrutura nova desproporcional ao problema atual (ver AGENTS.md — "não criar infraestrutura genérica de integração antes de existir uma necessidade concreta"); descartado. |

**Fluxo recomendado**: exportar no Nexor → arquivo `.json` → importar manualmente no PX
(seletor de arquivo). Sem API genérica, sem servidor, sem acesso direto a `nexor.db` pelo PX.

- **Envio/recebimento**: `SaveFileDialog` no Nexor, `OpenFileDialog` no PX — nenhuma automação.
- **Confirmação de abertura**: o PX exibe o resumo do que foi reconhecido (próxima entrega)
  antes de aceitar o contexto — isso já é a "confirmação".
- **Identificação de contexto antigo**: comparar `exportedAtUtc` com a data/hora local no
  momento da importação; o PX pode (próxima entrega) mostrar essa idade na tela, nunca ocultar.
- **Aplicativo fechado**: nenhum problema — é um arquivo, não uma chamada ao processo do outro
  lado.
- **Caminhos indisponíveis em outra máquina**: `job.sourceFileReference` é só textual; o PX
  nunca tenta abri-lo automaticamente (ver seção 4 do próximo pedido de implementação) — se o
  caminho não existir na máquina do PX, isso é só informado, nunca um erro fatal.

## 5. Fluxo de uso

1. Usuário seleciona um rolo/trabalho de impressão no Nexor (fluxo real:
   `PXSearchOrdersUI.on_select_roll` → aba "Pedidos no rolo").
2. Usuário aciona "Exportar contexto de produção..." no Nexor e escolhe onde salvar o `.json`.
3. Usuário abre o PX e aciona "Carregar contexto de produção..." (seção Produção), escolhendo
   o mesmo arquivo.
4. PX valida e apresenta o contexto recebido — nunca aplica nada automaticamente no documento
   Corel nem na sessão do ListForge.
5. Usuário decide a próxima ação manualmente (ex.: abrir o arquivo referenciado, se existir
   nesta máquina, pelo fluxo normal de importação do ListForge Integrado).

**Sessão já aberta no PX com lista/resultado não salvo**: carregar um novo contexto NUNCA
substitui silenciosamente a sessão atual — a próxima entrega (importação no PX) deve pedir
confirmação explícita antes de substituir um contexto já carregado quando há trabalho não
salvo, exatamente como as outras trocas de estado já implementadas no ListForge Integrado (ver
`docs/ARCHITECTURE.md`, seção 10, "Nunca descarta dados silenciosamente").

## 6. Decisões ainda necessárias (em aberto, não decididas aqui)

- **Nome do conceito — RESOLVIDO na iteração 30**: o texto visível ao usuário usa "contexto de
  produção"/"trabalho de impressão"/"rolo"/"pedido informado" (só quando o dado existir),
  nunca "pedido" como identificador comercial nem "Job". Os nomes TÉCNICOS do contrato
  (`formatId`, `rollId`, `jobHash`, `pedidoLabel`...) permanecem inalterados, por
  compatibilidade com o código e os arquivos já existentes — essa distinção (texto da
  interface vs. nome técnico do campo) é a decisão tomada, documentada aqui para não precisar
  ser revisitada.
- **Granularidade**: este contrato exporta UM trabalho de impressão (`job`) dentro de UM rolo
  (`source`). Se o uso real precisar exportar o ROLO INTEIRO (todos os pedidos dele de uma vez),
  o esquema precisa de uma versão 2 com uma lista de `job`s — não implementado aqui, para não
  ampliar o escopo sem necessidade comprovada.
- **Cliente/número de pedido**: se o Nexor vier a ganhar essas colunas no futuro, este esquema
  ganha uma versão 2 com os campos novos — opcionais, nunca quebrando importadores da versão 1.
- **O que PODE ser implementado sem nenhuma definição nova**: o esquema da seção 2 já é
  suficiente para a primeira importação no PX (próxima entrega) e a primeira exportação no
  Nexor (entrega subsequente) — nenhuma decisão em aberto acima bloqueia essas duas entregas.

## 7. Pendências preservadas

Aceitação da `.26`, validação do modo PX da `.27`/demais versões no Corel real, o caso "Carlos
Teste,15,ZZZ" e a investigação do redesenho intermitente continuam pendentes — nenhuma tocada
por esta entrega, que é exclusivamente documental (nenhum instalador foi gerado para esta
tarefa).
