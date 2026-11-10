# Desenho do modo PX — ListForge Integrado (protótipo, não implementação)

**Status: protótipo de navegação + proposta de design, revisado para o fluxo de 3 etapas
aprovado pelo usuário. Nenhum código de produção foi alterado nesta entrega.** O modo Clássico
(`ClassicWorkspaceWindow`) continua sendo a única interface real do ListForge Integrado. O
checklist de aceitação da build `0.1.0-dev.26` (`docs/acceptance/listforge-integrated-0.1.0-dev.26/`)
continua **pendente**, sem nenhum item alterado por esta tarefa.

- **Protótipo navegável**: `docs/design/listforge-px-workflow/prototype.html` (abra direto no
  navegador — HTML/CSS/JS autocontido, sem dependência nova, sem build).
- **Este documento**: `docs/design/listforge-px-workflow/design.md`.

As telas do protótipo não mostram nomes de serviço/classe nem comentários de implementação —
todo esse detalhe (o "porquê" de cada tela) fica só aqui.

---

## 1. O que o modo Clássico já usa (base real, não presumida)

Levantamento feito lendo `ClassicWorkspaceWindow.xaml.cs` por completo — ver também
`docs/ARCHITECTURE.md`, seção 10, e `docs/listforge-classic-parity.md`.

### 1.1. Núcleo compartilhado (`ListForge.Core`/`ListForge.Services`) — o modo PX deve consumir
exatamente os mesmos, nunca uma cópia

| Serviço | Para quê |
|---|---|
| `ProcessingWorkflowService` | Processar a lista (validação + interpretação + saída/JSON) |
| `ProcessingPreviewService` | Prévia (contagens válidos/avisos/inválidos, impacto por tamanho/peça, lista de problemas) ANTES de processar de fato |
| `ListComparisonService` | Comparar entrada × saída organizada |
| `AdvancedSaveService` | Salvar avançado (entrada+saída+JSON, soltos ou ZIP) |
| `OutputExportService` | Salvar saída (.txt) / Gerar JSON |
| `WorkProfileService` | CRUD de perfil de trabalho |
| `JsonPieceMappingService` / `PieceTypeMapper` | Lista avançada (mapeamento de peça por posição) |
| `LinkListImportService` | Extrair lista de um link |
| `FileImportService` | Importar PDF/Word/Excel (`ListForge.Core.FileImporter`) |
| `ILicenseService` (`IntegratedLicenseService`) | Status de licenciamento/Trial do modo integrado (decisão de produto pendente, não bloqueia hoje) |

### 1.2. Infraestrutura exclusiva do PX DEV (`Integrations/*`) — nunca compartilhada com o standalone

`ListForgeIntegratedSettingsStore`/`ListForgeIntegratedSettings` (perfis + preferências),
`ListForgeProcessingHistoryService` (histórico), `SupportPackageService` (pacote de suporte),
`PxLogger` (logs), `ListForgeInputBackupService` (backup de entrada), `ThreadKeyboardRouter`
(hook de teclado — ver seção 3, por que o modo PX herda isso automaticamente).

### 1.3. Estado de sessão hoje vivo só na janela (campos de `ClassicWorkspaceWindow`, nunca persistido)

`_currentInputFile`/`_currentSourceType`, `_hasUnsavedChanges`, `_lastOrders`/`_issueLines`
(último resultado de processamento), `_freshness` (`OutputFreshnessTracker` — se o resultado
ainda corresponde à entrada/opções atuais), `_comparisonSnapshot`, `_sizeConfig`, estado de
edição manual (`_allowOutputEditing`, `_hasPendingOutputEdit`/`_hasPendingJsonEdit`,
`_outputWasManuallyEdited`, `_lastValidJsonText`), `_advancedSaveMode`, flags de ocupado
(`_isProcessing`, `_isSavingAdvanced`, `_isImportingDocument`).

**Consequência direta para o modo PX**: este estado de sessão é o que precisa ser **compartilhado
entre os dois modos dentro do mesmo processo** (seção 3) — não é algo que o núcleo oferece, é
responsabilidade da camada de UI do PX preservar.

---

## 2. Navegação — Lista → Revisão → Resultado (3 etapas, sem etapa de Processamento)

A etapa "Processamento" da primeira versão deste protótipo foi removida por decisão do usuário.
Clicar em "Processar" (na etapa Revisão) agora mostra um estado temporário sobreposto
("Processando...", sem porcentagem nem duração — o serviço de processamento não expõe nenhum
dado de progresso granular, então mostrar isso seria inventar informação) e resolve de duas
formas, nunca uma terceira tela fixa:

- **Sucesso**: o estado temporário desaparece e a navegação vai direto para Resultado, já com o
  conteúdo preenchido.
- **Falha**: o estado temporário desaparece, a navegação **permanece em Revisão**, um aviso
  explica que não foi possível concluir, e a entrada/opções continuam exatamente como estavam
  (nada é limpo ou alterado por uma falha).

### 2.1. Por que os limites das etapas são estes (ajustados ao núcleo real)

- **Revisão** reaproveita a mesma análise que o Clássico já mostra num diálogo modal antes de
  confirmar o processamento (contagem de válidos/avisos/inválidos, impacto por tamanho e por
  tipo de peça, lista de problemas com linha/mensagem/sugestão) — aqui ela é uma tela própria em
  vez de um diálogo, mas os dados são os mesmos, nenhuma contagem nova.
- **Resultado** agrupa tudo que hoje já existe no painel de saída do Clássico — nenhuma função
  nova, só reorganizada em abas (ver 2.3).
- **Lista** reúne tudo que precede um processamento — entrada, opções de processamento, e a
  configuração de Lista avançada (ver 2.4).

### 2.2. Mapa completo: toda função do Clássico, onde ela fica no PX

| Função do Clássico | Etapa no PX | Observação |
|---|---|---|
| Abrir Lista... (texto/PDF/Word/Excel) | Lista | Mesmo importador, mesmo aviso de OCR indisponível |
| Extrair lista de link... | Lista | |
| Digitar/colar na entrada | Lista | |
| Localizar/Substituir | Lista | Opera sobre o mesmo campo de entrada |
| Limpar tudo | Lista | Sem confirmação — mesmo comportamento do Clássico (ver 2.5) |
| Separador / Maiúsculas-minúsculas / Ordenação | Lista | |
| Lista avançada (mapeamento de peça por posição) | Lista, dentro de uma seção expansível | Ver 2.4 |
| Selecionar perfil de trabalho | Barra persistente (global) | |
| Prévia (contagens, impacto por tamanho/peça, problemas) | Revisão | |
| Navegar até a linha de um problema | Revisão → volta para Lista na linha correspondente | |
| Processar (com prévia) | Botão "Processar" na etapa Revisão | Nunca uma etapa fixa própria — ver 2 |
| Saída organizada | Resultado, aba "Lista organizada" | |
| JSON gerado / edição manual do JSON (Aplicar/Descartar) | Resultado, aba "JSON" | |
| Resumo de tamanhos | Resultado | |
| Comparar entrada/saída | Resultado | |
| Copiar saída / Copiar JSON / Salvar saída / Gerar JSON / Salvar avançado | Resultado, por aba | Ver 2.3 |
| Histórico... | Resultado (e acessível globalmente) | |
| Criar/renomear/duplicar/excluir/restaurar perfil | Barra persistente (global) | |
| Configurações... / Tamanhos customizados... | Barra persistente (global) | |
| Diagnóstico (info de suporte, logs, backups, pacote de suporte, diagnóstico de redesenho) | Barra persistente (global) | |
| Salvar Entrada / Salvar Como | Barra persistente (global) | |
| Abrir ListForge Standalone ↗ | Barra persistente (global) | |

### 2.3. Resultado em abas — "Lista organizada" e "JSON"

Pedido do usuário: o conteúdo de cada formato precisa poder ser inspecionado antes de qualquer
cópia/exportação. As duas abas mostram o conteúdo real (lista organizada / JSON) em uma caixa de
texto somente leitura por padrão — as ações de Copiar/Salvar (aba Lista organizada) e Copiar/
Gerar JSON (aba JSON) ficam junto de cada aba, agindo sobre o que está exibido naquela aba, nunca
um dado escondido. A edição manual (ligar "Permitir edição nesta sessão") continua restrita à
aba JSON, com "Aplicar"/"Descartar" aparecendo só quando há uma edição pendente — mesma regra do
Clássico (nunca persiste entre aberturas; indicador de edição pendente sempre distinto do aviso
de resultado desatualizado, ver seção "Estados").

### 2.4. Lista avançada — seção expansível, opções reais

Representa exatamente o que o Clássico oferece: um mapeamento de peça por **posição**, com as
mesmas 6 opções reais (não inventadas) que o núcleo compartilhado conhece: Manga Curta, Manga
Longa, Short, Calça, Regata, Colete. O protótipo mostra 2 posições como exemplo ilustrativo — o
número real de posições no Clássico é definido dinamicamente a partir da entrada (quantas
peças/tamanhos uma linha tem), nunca um número fixo; isso não foi reproduzido dinamicamente no
protótipo (ver decisões abertas, seção 5).

### 2.5. Navegação não forçada e sem perda silenciosa

- As 3 etapas ficam sempre visíveis e clicáveis diretamente, em qualquer ordem — mudar de etapa
  nunca limpa ou altera entrada, opções ou resultado.
- Funções hoje sempre visíveis no Clássico (perfil, Configurações, Diagnóstico, Salvar
  Entrada/Como) continuam numa barra persistente, em qualquer etapa.
- **"Limpar tudo"** (etapa Lista) reproduz o Clássico exatamente: limpa direto, sem pedir
  confirmação — é uma ação nomeada e explícita, não uma perda incidental ao navegar.
- **"Nova lista"** (etapa Resultado) não tem equivalente no Clássico — é uma ação nova desta
  proposta, para voltar ao início depois de already ter um resultado. O protótipo pede
  confirmação antes de descartar; **isto não é uma decisão aprovada**, é uma proposta em aberto
  (ver seção 5) — poderia igualmente não pedir confirmação, para ficar consistente com "Limpar
  tudo".

---

## 3. Continuidade entre Clássico e PX

### 3.1. O que precisa ser preservado ao trocar de modo (ver inventário da seção 1.3)

Entrada atual (texto + arquivo/link de origem), opções de processamento, perfil ativo, último
resultado de processamento E sua validade, alterações manuais da saída/JSON ainda não
aplicadas, estado de "alterações não salvas".

### 3.2. Decisão proposta: um único processo, duas apresentações sobre o MESMO estado

Nunca duas sessões/janelas independentes do núcleo. Clássico e PX são duas CAMADAS DE
APRESENTAÇÃO sobre o mesmo estado de sessão, nunca duas cópias dele — trocar de modo troca qual
janela/`UserControl` está visível, sem recriar nem duplicar o estado. Decorrência prática (a
confirmar na implementação real, fora desta tarefa): isso aponta para `ClassicWorkspaceWindow`
ganhar uma segunda apresentação (PX) sobre os MESMOS campos privados, ou para extrair o estado
de sessão da seção 1.3 para uma classe própria (só dados + notificação de mudança, sem UI), lida
e escrita pelas duas janelas. A segunda opção é mais limpa para não acoplar o modo PX ao código
da janela Clássica, mas é uma refatoração real — fica como decisão técnica aberta (seção 5).

Alterações manuais não aplicadas e o hook de teclado acompanham a troca de modo automaticamente
SE a opção acima for adotada — nenhuma lógica nova precisa ser escrita para "transferir" esse
estado, porque ele nunca deixa de existir.

### 3.3. Onde salvar a preferência de modo (Clássico/PX) — qual entidade de perfil

A tarefa original pediu para confirmar a entidade certa antes de vincular a preferência a um
perfil. Conferido:

- **`WorkProfile`/`WorkProfileSettings`** (`ListForge.Core.Models`) é um tipo **compartilhado**
  com o ListForge Standalone — o mesmo `.cs` compila nos dois produtos. O Standalone não tem
  conceito de "modo PX". Colocar a preferência de modo dentro de `WorkProfileSettings` faria o
  Standalone carregar um campo que não significa nada para ele. **Rejeitado.**
- **`ListForgeIntegratedSettings`** (exclusiva do PX DEV, nunca compilada no Standalone) é a
  entidade correta — é onde preferências do módulo que não existem no Standalone como conceito
  (ex.: `EditorFontSize`, tamanhos customizados) já vivem hoje.
- Dentro dela, a preferência precisa ser por perfil sem tocar em `WorkProfile`/
  `WorkProfileSettings` (compartilhados). Proposta: um campo paralelo, chaveado pelo
  `WorkProfile.Id` (já existe, é estável):

  ```csharp
  // Em ListForgeIntegratedSettings (PX DEV, exclusivo) — proposta, não implementada nesta tarefa.
  public Dictionary<string, string> PreferredWorkspaceModeByProfileId { get; set; } = new();
  // valores: "Classic" ou "Px" — resolvido para "Classic" quando a chave não existe
  // (compatibilidade com configuração salva antes desta função existir, sem exigir migração
  // de esquema — mesmo padrão aditivo já usado por Sizes).
  ```

### 3.4. Evitando perda silenciosa ao trocar de modo

Com a decisão da seção 3.2 (mesmo estado, duas apresentações), não há "perda" possível por
estrutura — não existe um segundo estado para o primeiro perder. O único cuidado real fica para
a apresentação: se o PX decidir, por exemplo, esconder o campo de JSON bruto numa etapa, uma
edição pendente de JSON não pode ficar invisível e esquecida — a UI que assume o controle deve
mostrar o mesmo aviso que o Clássico já mostra hoje, nunca escondê-lo por estar numa etapa
diferente.

---

## 4. Estados que a interface precisa diferenciar

Pedido explícito do usuário — como cada estado aparece no protótipo (controles de demonstração
na barra superior, separados da interface "real") e o que a implementação real precisa
reproduzir:

| Estado | Como o protótipo demonstra | O que a implementação real precisa fazer |
|---|---|---|
| Sem resultado | Etapa Resultado mostra uma mensagem central, sem abas/conteúdo | Mesma condição de hoje (nenhum processamento bem-sucedido ainda) |
| Resultado atual | Abas com conteúdo, sem avisos | Resultado corresponde à última entrada/opções processadas |
| Resultado desatualizado | Aviso fixo no topo da etapa Resultado (alternável pelo controle de demonstração) | Mesma lógica de hoje (entrada/opções mudaram desde o processamento) — nunca deixar copiar/exportar silenciosamente um resultado desse jeito |
| Edição manual pendente (JSON) | Indicador na aba JSON + botões "Aplicar"/"Descartar" aparecem | Mesma regra de hoje — indicador sempre distinto do aviso de desatualizado acima, podem aparecer juntos |
| Processando | Sobreposição com indicador de atividade, sem porcentagem | Chamada real ao processamento, sem inventar granularidade que o serviço não tem |
| Falha de processamento | Aviso na própria etapa Revisão, dados preservados, sem navegar | Nunca navegar para Resultado numa falha; nunca limpar a entrada |

---

## 5. Fora do escopo desta entrega (confirmado, não esquecido)

Montagem automática/envio ao Corel, Nexor, OCR, novas regras de licença, mudanças nas
ferramentas de Saídas, qualquer ação real conectada a exportação/alteração de arquivos no
protótipo, substituição da interface Clássica, geração de instalador novo.

**Nota sobre o placeholder arquitetural anterior**: `docs/ARCHITECTURE.md`, seção 10, descrevia
o modo PX original como "reaproveitando objetos/layout do documento ativo" do CorelDRAW — essa
visão é mais ampla do que o fluxo Lista→Revisão→Resultado desenhado aqui (que usa as mesmas
fontes de entrada baseadas em texto do Clássico, não objetos do documento). Fica registrado como
pergunta aberta abaixo, não decidido aqui.

---

## 6. Decisões técnicas e dúvidas concretas (para o usuário/product owner decidir)

Nenhum destes itens foi decidido por esta tarefa — estão aqui como perguntas, não como escolhas
já feitas:

1. **Estrutura de código**: extrair o estado de sessão da seção 1.3 para uma classe própria ou
   manter tudo em `ClassicWorkspaceWindow` e fazer o PX compartilhar essa mesma instância de
   outra forma? Afeta o esforço de implementação e o risco de regressão no Clássico.
2. **Confirmação em "Nova lista"** (seção 2.5): pedir confirmação ou não, para ficar consistente
   com "Limpar tudo" (que nunca confirma)? O protótipo mostra uma confirmação só como exemplo,
   não como proposta fechada.
3. **Número de posições na Lista avançada**: o protótipo mostra 2 posições fixas como exemplo;
   a implementação real precisa decidir se o número continua dinâmico (baseado na entrada, como
   hoje) e como isso aparece dentro da seção expansível.
4. **Papel futuro de "reaproveitar objetos do documento ativo"** (seção 5): entra como fonte de
   entrada adicional na etapa Lista numa entrega futura, ou é uma visão descontinuada em favor
   deste fluxo baseado em texto?
5. **Licenciamento/Trial**: confirmar que o modo PX, quando implementado de verdade, consome o
   MESMO `ILicenseService`/contador do Clássico (um crédito vale para os dois modos) —
   presumido aqui, não decidido oficialmente.
6. **Nome real do modo** para a interface: a tarefa usa "modo PX"; confirmar se é o nome
   definitivo a aparecer para o usuário final.
7. **Mecanismo de simulação de falha** usado no protótipo (uma marcação que força o próximo
   "Processar" a falhar) existe só para demonstrar o estado — não corresponde a nenhum mecanismo
   real do núcleo nem precisa ter equivalente na implementação.
