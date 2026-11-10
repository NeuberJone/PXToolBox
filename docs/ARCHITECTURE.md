# Arquitetura — PX Tool Box

## 1. Ecossistema

| Produto | Responsabilidade |
|---|---|
| Nexor | Pedidos, planejamento, filas, produção, registros e status |
| ListForge | Leitura, interpretação, normalização, validação e processamento de listas; saída SISBolt/JSON |
| PX Tool Box | Execução no CorelDRAW: objetos, camadas, montagem, preparação, Grid e saída |

Cada produto funciona de forma independente. A integração futura usa **contratos
versionados**, nunca referências diretas às classes internas de outro produto. Não existe
infraestrutura genérica de integração nesta entrega — ela só será criada quando houver uma
necessidade concreta e específica.

- **ListForge**: standalone permanece independente do CorelDRAW/PX. O PX DEV tem, desde esta
  entrega, o primeiro dos dois acessos previstos — "ListForge Standalone" (abre o executável,
  ver seção 9). "ListForge Integrado" (modos Clássico e PX) continua não implementado — não
  exibido na UI por decisão explícita desta tarefa; o núcleo de processamento que os dois
  acessos vão compartilhar já foi extraído e preparado dentro do próprio repositório do
  ListForge (ver seção 10), mas o PX ainda não referencia esse núcleo. Uma correção no núcleo
  só chega aos produtos quando eles atualizarem essa dependência.
- **Nexor**: dono do status operacional. O PX poderá comunicar resultados de execução; o Nexor
  valida as transições. Visão futura: `Nexor → ListForge → Corel/PX → Nexor`. Não reescrever o
  Nexor em C# como pré-requisito. Não implementado nesta entrega.
- **Moldes / MoldMotor**: o MoldMotor permanece independente — nunca compartilha identidade,
  licença, updater, configuração, GUIDs ou diretórios de instalação com o PX. O nome
  "MoldMotor" não deve aparecer na UI do módulo integrado do PX (futuro). Não implementado
  nesta entrega.

## 2. Produção vs. DEV

Um repositório, código compartilhado, **duas distribuições com identidade própria**:

| Identificador | DEV (esta entrega) | Prod (futuro) |
|---|---|---|
| Pasta de instalação (Addons) | `Programs64\Addons\PXToolBoxDev\` | `Programs64\Addons\PXToolBox\` |
| Assembly / AssemblyName | `PXToolBoxDev.dll` | `PXToolBox.dll` |
| GUID do conteúdo WPF (XSLT, `wpfhost`) | `c7124037-25c8-4d03-80b2-97b716ed95ef` | outro GUID, a gerar quando Prod for implementado |
| GUID do docker nativo (XSLT, `dockerData`) | `332244c0-4471-4da8-bff8-1b2ed48a9dd8` | outro GUID, a gerar quando Prod for implementado |
| GUID do botão de alternância (XSLT, `checkButton`) | `0dd0d869-201a-40a9-b3d0-801f9138f700` | outro GUID, a gerar quando Prod for implementado |
| Config/registro do usuário | namespace de settings próprio do host DEV | namespace próprio do host Prod |
| Canal de atualização | `dev` | `stable` |
| Logs | pasta própria do DEV (a definir ao implementar logging) | pasta própria do Prod |
| Caminho do ListForge standalone | `%LOCALAPPDATA%\PXToolBox\Dev\config\listforge-path.txt` | pasta própria do Prod, a definir quando Prod existir — nunca esta |

As duas distribuições **coexistem no mesmo CorelDRAW** porque cada uma é uma pasta de add-on
independente com seu próprio GUID de docker — o Corel as trata como add-ons completamente
separados. Nenhum recurso exclusivo do DEV é incluído no pacote Prod (quando existir); a
separação é por **pacote**, não só por esconder botões na interface.

Não existe uma branch DEV permanente — apenas branches de tarefa (ex.:
`feat/core-saidas-mvp`).

## 3. Como o CorelDRAW 2026 carrega um add-on (descoberto por inspeção, sem documentação pública)

Não há SDK/documentação pública instalada nesta máquina para o mecanismo de add-on usado por
produtos de terceiros com UI própria. O mecanismo abaixo foi reconstruído por inspeção de
quatro add-ons já instalados nesta máquina — e **corrigido** nesta entrega (branch
`fix/dev-docker-hosting`) depois de uma primeira versão ter copiado um padrão que não produz
um docker real.

- O template VSTA de macro (`VGCoreApp.vstax` / `VGCoreApp_Tool.vstax`, em
  `Programs64\..\Data`) — mostra o padrão de **macro GMS** (`CgsAddInModule`/`CgsAddInMacro`,
  .NET Framework 4.5, referencia `Corel.Interop.VGCore.dll`). Útil para macros simples, mas
  **não** cria um painel de docker nativo — só pode mostrar uma janela própria (Form/WPF
  flutuante) ou expor métodos para botões de barra de ferramentas.
- O add-on **SISBolt**, já instalado em `Programs64\Addons\SISBolt\` — um add-on real, com UI
  em **WPF** (referencia `MaterialDesignThemes.Wpf`). A primeira versão deste documento
  descrevia o padrão de registro de SISBolt como "isso cria o docker" — **incorreto**: SISBolt
  registra seu `itemData type="wpfhost"` diretamente dentro de `uiConfig/commandBars`, e um
  botão na barra de comandos padrão referenciando o mesmo GUID. Isso **não cria uma janela de
  encaixe nativa** — apenas embute o controle dentro da própria barra de comandos. É
  exatamente esse padrão, copiado por engano para o PX Tool Box DEV, que fazia o painel
  aparecer comprimido na barra superior ao lado do SISBolt (ambos sofrem do mesmo problema;
  SISBolt nunca foi corrigido, mas isso não o torna a referência correta).
- O add-on **MoldMotor**, já instalado em `Programs64\Addons\MoldMotor\`, e o **exemplo oficial
  do SDK da Corel**, `Programs64\Addons\eCutDocker\` (ambos já instalados nesta máquina) — os
  dois abrem de fato como **janela de encaixe nativa** (encaixável, redimensionável, pode ser
  fechada/reaberta, aparece em **Janela → Janelas de encaixe**). O padrão real, confirmado
  idêntico nos dois:
  - `CorelDrw.addon` — arquivo **vazio**, marcador de pasta de add-on (igual ao SISBolt).
  - Em `AppUI.xslt` (`uiConfig/items`): um `itemData type="wpfhost"` (o conteúdo, igual ao
    SISBolt) **mais** um `itemData type="checkButton"` com
    `check="*Docker('&lt;guid-do-docker&gt;')"` e `dynamicCategory="2cc24a3e-fe24-4708-9a74-9c75406eebcd"`
    (GUID fixo do Corel, confirmado idêntico nos dois add-ons — não é específico de produto) —
    esse é o botão de alternância do docker.
  - Em `AppUI.xslt` (`uiConfig/dockers`, seção que a versão anterior deste add-on nunca usava):
    um `dockerData` com GUID próprio, contendo um `container` com o item `wpfhost` — **isso** é
    o que de fato cria a janela de encaixe.
  - Em `UserUI.xslt`: o item do `checkButton` é adicionado ao menu **Janela → Janelas de
    encaixe** (`commandBarData` com o GUID fixo `3eaa9bbe-28fd-4672-9128-02974ee96332`), não à
    barra de comandos padrão.

Conclusão para a arquitetura: um add-on com docker nativo = uma classe **.NET Framework**
(carregada via reflexão pelo host "wpfhost" do CorelDRAW, não via COM/regasm) + `CorelDrw.addon`
+ `AppUI.xslt` registrando `itemData` (wpfhost + checkButton) **e** `dockerData` em
`uiConfig/dockers` + `UserUI.xslt` inserindo o `checkButton` no menu de Janelas de encaixe —
copiados para `Programs64\Addons\<NomeExclusivo>\`. O PX Tool Box DEV usa exatamente esse
padrão a partir desta correção (`host/PXToolBox.CorelHost.Dev/Packaging/AppUI.xslt` e
`UserUI.xslt`), com GUIDs próprios e exclusivos do DEV (ver seção 2).

### Registro incorreto já persistido em workspaces existentes (causa demonstrada, branch `fix/dev-docker-registration`)

A correção do pacote (`AppUI.xslt`/`UserUI.xslt`) só vale para um workspace **novo**. Ela não
basta por si só, e isso foi comprovado — não apenas suposto — lendo diretamente, nesta máquina,
os sete arquivos `.cdws` reais em
`%APPDATA%\Corel\CorelDRAW Graphics Suite 2026\Draw\Workspace\`
(`_default`, `Default(TM)`, `illustration`, `illustrator`, `Jone Stark`, `lite`, `pagelayout` —
cada um um contêiner ZIP com XML sem namespace em `content/workspace.xml`), com o CorelDRAW
aberto e a build corrigida já instalada e com hash confirmado:

1. **Todos os sete** ainda têm o item antigo (bug) solto na barra de comandos padrão
   (`commandBarData guid="c2b44f69-..."`): `<item guidRef="c7124037-25c8-4d03-80b2-97b716ed95ef"/>`.
   É isso que renderiza o painel comprimido — a `itemData type="wpfhost"` com esse GUID segue
   válida (definida em `AppUI.xslt`, mesclada a cada execução), então a referência solta na
   barra continua resolvendo e desenhando o conteúdo ali, independente da correção do pacote.
2. **Nenhum dos sete** tem o item novo (`checkButton` guid `0dd0d869-...`) no menu "Janela →
   Janelas de encaixe" (`commandBarData guid="3eaa9bbe-..."`) — por isso o comando não aparece
   nesse menu. Nesse mesmo menu, nesses mesmos workspaces, os itens equivalentes do **MoldMotor**
   (`01d91be4-...`) e do **eCutDocker** (`a916f63b-...`) **estão presentes** — prova de que o
   menu e o mecanismo geral funcionam; só o item do PX DEV nunca foi inserido.
3. Reiniciar o CorelDRAW **não** resolve: o workspace `Jone Stark.cdws` foi regravado pelo
   Corel um minuto depois de reaberto (mtime consistente com o restart), e continuou com o item
   antigo e sem o item novo — ou seja, a mesclagem de `UserUI.xslt` não é refeita num workspace
   já existente só por reabrir o app, mesmo depois do add-on ser atualizado.

Conclusão: `UserUI.xslt` só é mesclado num workspace quando ele é **criado** — nunca
retroativamente, nem ao reabrir o app, nem quando o add-on já instalado é só atualizado. Como
todo workspace desta máquina já existia antes da correção (e antes até da primeira instalação
do PX DEV), nenhum deles herda a correção sozinho. A única via seria "resetar workspace" (F8),
que também apagaria barras personalizadas do usuário — proibido aqui. A correção real, portanto,
precisa editar o `.cdws` de cada workspace já inicializado, nos dois sentidos: remover o item
antigo **e** inserir o item novo que falta.

`scripts/repair-dev-docker-registration.ps1` faz exatamente isso, e só isso: abre cada `.cdws`
já inicializado nesta máquina, remove o `item` com o GUID antigo quando ele está preso a um
`toolbar`/`menu` (nunca quando já está dentro de um `dockerData` — workspace já corrigido), e
insere o `item` do `checkButton` novo no menu "Janelas de encaixe" só se ainda não estiver lá.
Faz backup do arquivo antes de alterar, e não toca em mais nada — nem nas barras personalizadas
do usuário, nem nos itens do SISBolt/MoldMotor/eCutDocker, nem em outro conteúdo do workspace.

**Validado** (lógica real do script, rodada de fato) contra **cópias isoladas** dos sete
arquivos `.cdws` reais desta máquina: corrigiu os sete (removeu o item antigo e inseriu o novo
em cada um), preservou os itens do SISBolt/MoldMotor/eCutDocker, manteve o XML válido, e uma
segunda execução não encontrou mais nada a corrigir (idempotente).

**Executado contra os arquivos reais em uso** (26/10/2026, com o CorelDRAW fechado pelo usuário
a pedido): o script rodou sem `-WhatIf` contra os sete `.cdws` reais, corrigiu os sete (com
backup de cada um antes de gravar), e a releitura pós-gravação confirmou o item antigo ausente
e o item novo presente no menu "Janelas de encaixe" nos sete, XML válido, SISBolt intacto.
**Validado pelo usuário no CorelDRAW real, reaberto depois do reparo**: o docker "PX Tool Box
DEV" passa a aparecer como janela de encaixe lateral, igual ao MoldMotor — não mais embutido na
barra superior. Confirmação literal do usuário: "Deu bom". Os pontos mais finos do roteiro de
`docs/mvp-smoke-test.md` (redimensionar/desencaixar, fechar/reabrir, rodar uma ferramenta,
persistência após reiniciar de novo) não foram reconfirmados individualmente nesta rodada.

### Posição inicial do docker (lateral direita)

Não há, em nenhum dos dois add-ons de referência (MoldMotor, eCutDocker), nenhum atributo de
`dockerData` que force o lado inicial de encaixe (direita/esquerda/flutuante) — o Corel decide
isso por padrão ao criar o docker pela primeira vez num workspace, e depois persiste a posição
em que o usuário deixou. Não foi inventado nenhum atributo "dockSite"/"initialDock" sem
evidência real. Se o docker não aparecer inicialmente à direita, é esperado: arraste-o lá uma
vez — o CorelDRAW lembra a posição nas próximas aberturas desse workspace.

### Ponto em aberto (não documentado, a confirmar no smoke test real)

Não foi possível confirmar, só por inspeção estática, **como o host entrega a instância do
`Application` do CorelDRAW** para o controle WPF hospedado. `ControlUI` (em
`host/PXToolBox.CorelHost.Dev/ControlUI.xaml.cs`) implementa duas hipóteses, nesta ordem:

1. Um construtor `ControlUI(object app)`, no caso de o host chamar um construtor desse tipo
   (padrão parecido com o `CgsAddInConstructor` das macros VSTA).
2. Um fallback via `Marshal.GetActiveObject("CorelDRAW.Application.27")` (ProgID confirmado no
   registro: `HKLM\SOFTWARE\Classes\CorelDRAW.Application.27`), que localiza a instância em
   execução pela Running Object Table.

Isso precisa ser validado com o CorelDRAW real (ver `docs/mvp-smoke-test.md`).

## 4. Target frameworks (por que não ".NET mais recente" em tudo)

- `PXToolBox.Core` e `PXToolBox.Outputs`: **netstandard2.0** — puro C#, sem COM, consumível
  tanto pelo host net48 quanto pelos testes net8.0.
- `PXToolBox.CorelHost.Dev`: **net48** — o CorelDRAW 2026 carrega o add-on dentro do seu
  próprio processo usando o .NET Framework 4.8 já instalado no Windows (confirmado:
  `HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` → release 533509 = 4.8). Não é
  possível usar .NET 8/10 aqui — não há mecanismo de host para add-on .NET moderno dentro do
  CorelDRAW nesta versão.
- `PXToolBox.Tests`: **net8.0** — roda fora do Corel, com o SDK mais novo disponível
  (10.0.202), sem motivo para fixar numa versão antiga.

## 5. Isolamento do COM/Corel

`PXToolBox.Outputs.Abstractions` define interfaces (`ICorelDocument`, `ICorelPage`,
`ICorelLayer`, `ICorelShape`, `ICorelSelection`, `ICorelWorkspace`) sem qualquer referência a
COM. `PXToolBox.Outputs.Tools` contém a lógica de cada ferramenta de Saídas, testável com fakes
em memória (`tests/PXToolBox.Tests/Fakes`). `host/PXToolBox.CorelHost.Dev/Corel/*` implementa
essas interfaces com chamadas reais a `Corel.Interop.VGCore` — é a ÚNICA camada que toca COM.

Decisões de tratamento de erro/contexto do host:

- Ausência de documento, seleção inválida, leitura inconclusiva e falha de execução são
  valores distintos de `OperationStatus` (`PXToolBox.Core.Result`) — nunca colapsados num
  "sucesso"/"falha" genérico.
- Cada comando do painel (`ControlUI`) chama `Document.BeginCommandGroup`/`EndCommandGroup` do
  CorelDRAW para agrupar a operação como um único passo de desfazer, com `try/finally`
  garantindo que `EndCommandGroup` seja sempre chamado — inclusive em falha. Isso **não é**
  garantia de rollback automático: o Corel não desfaz sozinho uma operação parcial só porque o
  grupo de comando foi encerrado; os serviços de `Outputs` validam pré-condições antes de
  mutar o documento para reduzir o risco de estado parcial.

## 6. Caso crítico: Grid com camadas ocultas

Ver `docs/mvp-smoke-test.md` para o roteiro manual e `docs/migrated-tools.md` para a fonte
exata. Resolvido por uma **revisão candidata** de `Grid.PreparaGrid` (não a versão em
`CorelMacros`, que dependia de `ActiveSelection`/`CreateSelection` e por isso excluiria
camadas ocultas do agrupamento):

- `GridPreparationService.PrepareGrid` torna **todas** as camadas não-especiais
  temporariamente visíveis, coleta todos os seus objetos diretamente via
  `ICorelLayer.Shapes` (sem `Group`/`Ungroup`, sem mover entre camadas), redimensiona a página
  à caixa delimitadora combinada do conjunto e translada tudo para o novo centro da página.
  **Objetos de camadas originalmente ocultas (FE, C) participam do Grid.**
- A visibilidade original de cada camada é restaurada sempre, inclusive em falha
  (`try/finally`) — camadas vazias só são excluídas depois disso, e só em sucesso.
- Validação prévia: nenhuma camada não-especial com objetos pode estar bloqueada
  (`ICorelLayer.Locked`); se estiver, falha antes de tocar em qualquer coisa.
- A caixa delimitadora combinada usa `ICorelShape.CenterX/CenterY` (mapeados para
  `Shape.CenterX/CenterY` do VGCore, confirmados por reflexão), não `PositionX/Y` — Center não
  depende do `ReferencePoint` do documento, então o cálculo é correto independentemente dessa
  configuração.

Coberto por `GridPreparationServiceTests.PrepareGrid_HiddenFeAndC_ParticipateAndVisibilityIsRestored`.
Ainda não validado interativamente dentro do CorelDRAW real.

## 7. Convenções de nomenclatura confirmadas no código legado (CorelMacros)

- Camadas: segmentos separados por ponto; o **último** segmento indica a categoria —
  `.MA`, `.FE`, `.C` (ver `Utilidades.OrganizarCamadas`, array `sufixos`). O **primeiro**
  segmento indica o tipo de peça: `CAM1`, `CAM2`, `REG1`, `COL1`, `CAL1`, `SHO1` (ver
  `Correcoes.RenomeiaTipoDePeca`).
- **Inconsistência pré-existente encontrada e corrigida por decisão explícita do usuário**:
  `Utilidades.OrganizarCamadas` (e a revisão candidata) usam o prefixo `"CHO1."` na lista de
  ordenação, mas o único código válido de tipo de peça para "Short" é `SHO1` (sem o H) — ou
  seja, camadas de Short provavelmente nunca batiam com esse prefixo de ordenação na macro
  original. Corrigido para `"SHO1."` em `LayerOrganizationService.PrefixOrder` (perguntado ao
  usuário, que confirmou a correção — ver `docs/migrated-tools.md`).
- Códigos de objeto de texto: `NO` (Nome), `NU` (Número), `AP` (Apelido), `TS` (Tipo
  Sanguíneo) — alternam entre a forma curta e um rótulo longo.

## 8. Perfis e configuração (pontos de extensão, não implementados agora)

Esta entrega não implementa persistência de preferências do usuário nem pasta de saída
configurável via UI (o campo existe na API — `OutputPreparationService.BuildOutputFilePath`
— mas o host ainda não tem uma tela de configuração). Pontos de extensão previstos:

- Preferências portáveis (layout, favoritos) vs. dados locais (caminho de instalação do Corel,
  caminho do ListForge standalone) devem ficar em arquivos/seções separados desde o início,
  para permitir exportar/importar as portáveis sem vazar caminhos específicos da máquina.
  **Primeira implementação real**: `ListForgeConfigStore` (seção 9) — um arquivo próprio, só
  para o caminho do ListForge, separado de qualquer preferência portável futura.
- Persistência local simples e versionada primeiro; sincronização remota é explicitamente
  fora de escopo agora.

## 9. ListForge Standalone — primeiro acesso (host DEV)

Abre o executável do ListForge standalone (`C:\Projetos\ListForge`, app WPF independente) a
partir do painel DEV, sem incorporar sua interface ou processamento — ver seção "Listas" em
`ControlUI.xaml` e `host/PXToolBox.CorelHost.Dev/Integrations/`.

- **`ListForgeLauncher`**: decide SE/COMO abrir — nunca toca em arquivo ou processo
  diretamente; toda E/S real entra via delegates injetados no construtor (testável sem abrir
  processos reais, ver `ListForgeLauncherTests`). Distingue três resultados:
  `Launched`/`NeedsPathSelection`/`LaunchFailed`, nunca um "sucesso" genérico. A mensagem de
  sucesso informa apenas que o processo foi iniciado (com PID) — nunca que o ListForge "está
  pronto para uso", porque isso não pode ser confirmado só pelo retorno de `Process.Start`.
- **`ListForgeConfigStore`**: persiste o caminho escolhido em
  `%LOCALAPPDATA%\PXToolBox\Dev\config\listforge-path.txt` — pasta exclusiva da distribuição
  DEV (ver seção 2), em `LocalApplicationData` (não acompanha perfis móveis/roaming, ao
  contrário de `ApplicationData`), separada desde já de preferências portáveis futuras.
- **Sem mecanismo de ativação de instância existente**: examinado `App.xaml.cs` e o `README.md`
  do ListForge standalone antes de implementar — não há mutex de instância única, named pipe
  nem argumento de linha de comando documentado. Cada clique em "Abrir ListForge" inicia um
  processo novo via `Process.Start` (sem `Arguments`, `UseShellExecute = false`, sem `Verb`),
  nunca uma concatenação de comando via shell. `WorkingDirectory` é a pasta do executável —
  afeta só o processo filho, nunca `Environment.CurrentDirectory` do processo do CorelDRAW.
- **Fluxo**: "Abrir ListForge ↗" usa o caminho configurado; se ausente ou se o arquivo não
  existir mais (movido/removido — mensagens distintas), abre o seletor de arquivo automaticamente,
  salva o caminho escolhido ANTES de tentar abrir (uma falha ao abrir não desfaz a escolha do
  usuário) e então inicia o processo. "Localizar executável…" abre o mesmo seletor a qualquer
  momento, para reconfigurar deliberadamente mesmo com um caminho válido já configurado.
- **Exclusivo da distribuição DEV**: toda a implementação (launcher, config store, UI) vive em
  `host/PXToolBox.CorelHost.Dev` — nada foi adicionado a `src/PXToolBox.Core`/`PXToolBox.Outputs`
  (compartilhados entre DEV e a futura distribuição Prod), então nenhum pacote de Produção
  incluiria esta implementação só por compartilhar essas bibliotecas.
- **Testes**: `tests/PXToolBox.Tests` passou a multi-targetar `net8.0;net48` — a perna `net48`
  referencia o host DEV (só nela) para testar `ListForgeLauncher`/`ListForgeConfigStore` sem
  duplicar essa lógica num projeto novo. Cobre: primeira configuração, caminho inexistente,
  caminho com espaços, falha ao iniciar (sem interromper tentativas futuras), e que a
  mensagem de sucesso nunca afirma "pronto para uso". Persistência real (round-trip em disco,
  restaurando o estado anterior da máquina) e a pasta `LocalApplicationData` também são
  cobertas — sem abrir nenhum processo real em nenhum teste.
- **Não implementado/não validado nesta entrega**: seleção manual no seletor de arquivo real,
  cancelamento do seletor, e o botão de verdade dentro do CorelDRAW — só a lógica de decisão
  foi testada automaticamente (ver `docs/mvp-smoke-test.md`). Isolamento entre configuração DEV
  e Prod é garantido pelo caminho (`...\Dev\...`), mas não há teste comparativo possível
  enquanto a distribuição Prod não existir.

## 10. ListForge Integrado — modo Clássico (primeira versão, usando o núcleo compartilhado)

"ListForge Integrado" citado na seção 1: identificar/preparar o núcleo de processamento que o
standalone e o PX **compartilham de verdade** (mesmo código, não uma segunda implementação) e,
nesta segunda etapa, implementar a primeira versão da interface integrada — modo **Clássico**
— sobre esse núcleo. O modo **PX** foi implementado numa rodada posterior (ver "Modo PX
implementado" perto do fim desta seção) como uma segunda APRESENTAÇÃO sobre o mesmo estado de
sessão do Clássico — nunca uma segunda implementação de regras. Esta etapa não altera
ferramentas de Saídas, não implementa Nexor e não inicia montagem automática no Corel.

**Desenho do modo PX** (protótipo de navegação + documento de design) em
`docs/design/listforge-px-workflow/` — fluxo Lista → Revisão → Resultado (reduzido de
Lista → Revisão → Processamento → Resultado depois do refino aprovado pelo usuário — ver
ROADMAP.md, 16ª rodada), consumindo o MESMO núcleo do Clássico. A visão original deste parágrafo
("reaproveitando objetos/layout do documento ativo") continua mais ampla do que o que foi
implementado (que usa as mesmas fontes de entrada baseadas em texto do Clássico, não objetos do
documento Corel) — ainda registrado como pergunta aberta em
`docs/design/listforge-px-workflow/design.md`, seção 5, não decidido nesta entrega.

**Arquitetura encontrada em `C:\Projetos\ListForge`** (standalone real, `net8.0-windows`,
WPF, versão 2.1.44 — `F:\Projetos\ListForge`, caminho histórico informado, não existe nesta
máquina; `C:\Projetos\ListForgeV2` é um esqueleto recente e não relacionado, de uma visão de
produto diferente — ignorado):

- Os namespaces `ListForge.Core`/`ListForge.Models` já eram, antes desta tarefa, quase
  inteiramente livres de WPF, COM, estado global de janela e caminhos fixos — a separação que
  este processo pediu já existia em grande parte; não foi preciso reescrever regras de
  processamento, só isolá-las fisicamente num projeto próprio.
- Duas classes do mesmo namespace `Core` são a excessão e **ficaram de fora** do núcleo
  extraído, de propósito: `AppLogger` e `TrialManager` dependem de `ConfigManager` (caminhos
  graváveis da máquina, resolvidos em `%APPDATA%`/`%LOCALAPPDATA%`) e, no caso do
  `TrialManager`, de `DPAPI` (`ProtectedData`) para o estado de créditos Trial. `TrialManager`
  é a única fonte de verdade do licenciamento/Trial do ListForge — preservado integralmente,
  sem cópia e sem contorno, consumido hoje por `LocalTrialLicenseService` via `ILicenseService`
  (essa interface já desacoplava licenciamento do resto do app; não foi alterada).
- `FileImporter` (import de `.txt/.pdf/.docx/.xlsx` + OCR via Tesseract) ficou de fora **nesta
  etapa original** — texto/PDF/Word/Excel foram movidos para o núcleo compartilhado numa rodada
  posterior (ver `docs/listforge-classic-parity.md`, "Importação de documentos"; `AppLogger` não
  é mais uma dependência dessa parte, removida na extração). Só OCR via Tesseract permanece de
  fora: binários nativos + arquivos de idioma (`tessdata`) são dependência pesada específica de
  formato de arquivo, deliberadamente não movida — renomeada para `ImageOcrImporter` no
  standalone para não colidir com o tipo `FileImporter` agora compartilhado. O ponto de entrada
  real do núcleo continua sendo texto já extraído
  (`ListParser.ProcessText`), independente de onde esse texto veio.
- `AboutInfoBuilder` e `TextSearchHelper` são puros (sem I/O) mas não pertencem ao domínio
  "interpretação/normalização/validação/processamento de listas; saída SISBolt/JSON" da seção
  1 — são suporte de UI (tela Sobre, localizar/substituir no editor) e ficaram no projeto
  principal, proporcionalmente ao que a tarefa pediu para extrair.

**Extração realizada** (no repositório do ListForge, não neste): projeto
`ListForge.Core\ListForge.Core.csproj` (**netstandard2.0** — mesma convenção já usada por
`PXToolBox.Core`/`PXToolBox.Outputs`, ver seção 4), contendo `ListParser`, `ListProcessor`,
`ListOutputBuilder`, `JsonOrderBuilder`, `JsonListImporter`, `JsonPieceMappingOptions`,
`ListRowSorter`/`ListSortMode`, `SizeHelper`, `PieceTypeMapper`, `FileNameHelper`,
`OperationResult`, `ParsedRow`, `SizeConfig` e, nesta segunda etapa, também
`ProcessingWorkflowService`, `JsonPieceMappingService` e `ILicenseService` — movidos (`git mv`,
histórico preservado), não copiados. O `ListForge.csproj` principal passou a referenciá-lo
(`ProjectReference`) e continua sendo o único executável do standalone.

`ProcessingWorkflowService` é o orquestrador real usado pelo `MainViewModel` do standalone
(valida → processa → ordena → valida mapeamento de peças → verifica licença → monta
saída/JSON → consome crédito) — pré-requisito que a primeira etapa deixou pendente de
inspecionar com cuidado. Movê-lo para o núcleo (em vez de reimplementar essa sequência num
handler de botão do PX) é o que evita uma segunda cópia das regras, conforme a tarefa exige.
Seu construtor sem parâmetros (que instanciava `LocalTrialLicenseService`, ligada a
`TrialManager`/`ConfigManager`) foi removido: `LocalTrialLicenseService` continua no projeto
principal do ListForge, fora do núcleo, então todo chamador — `MainViewModel` e o host do PX —
passa explicitamente seu próprio `ILicenseService`. `OutputExportService` (exportação de texto/
JSON com nome versionado, sem nunca sobrescrever um arquivo existente) também foi movido, pelo
mesmo motivo. `JsonPieceMappingService` precisou dos mesmos dois ajustes mecânicos de
compatibilidade com netstandard2.0 descritos abaixo (`Split(string)` e `Math.Clamp`, que não
existem nesse alvo). Os 8 pontos de chamada de testes que usavam o construtor padrão de
`ProcessingWorkflowService` foram ajustados para passar `new LocalTrialLicenseService()`
explicitamente — mesmo comportamento, só explícito.

- **netstandard2.0 não é binariamente "de graça"**: o código já usava APIs só disponíveis a
  partir do netstandard2.1/.NET Core (`Dictionary.GetValueOrDefault`, desconstrução de
  `KeyValuePair`, `Index`/`Range` como `texto[..^n]`, e a sobrecarga de `string.Split` que
  aceita um único `string` como separador). Foram necessários dois ajustes mecânicos,
  verificados pelos 281 testes existentes do ListForge (idênticos antes e depois — nenhuma
  falha, nenhuma mudança de comportamento observável):
  - duas extensões de compatibilidade (`GetValueOrDefault`, `Deconstruct` de `KeyValuePair`),
    só compiladas sob `NETSTANDARD2_0`, sem efeito no assembly do próprio ListForge
    (`net8.0-windows`, que já tem os membros reais);
  - três trocas de `texto[a..b]`/`texto[^n]` por `Substring`/índice explícito, e quatro trocas
    de `Split(separador)` por `Split(new[] { separador }, StringSplitOptions.None)` (mesma
    semântica, overload disponível desde o netstandard2.0).
- **Compatibilidade com o host do PX confirmada por experimento, não por suposição**: um
  projeto `net48` descartável (fora dos dois repositórios) referenciando
  `ListForge.Core.csproj` via `ProjectReference` compila com 0 avisos/0 erros e executa
  `ListProcessor.ProcessText`/`BuildOutput` normalmente — prova de que o host do PX (`net48`,
  seção 4) pode consumir este núcleo sem retargetar nada.
- **Comparação de resultados antes/depois**: build e suíte de testes do ListForge executados
  nesta máquina antes da extração (281/281 aprovados, 0 avisos) e depois (281/281 aprovados, 0
  avisos) — mesmo total, mesmo resultado. Nenhuma regra de interpretação, validação ou
  exportação SISBolt/JSON mudou.

**Decisão arquitetural implementada — como o host do PX consome este núcleo:** publicar um
pacote (NuGet, mesmo que num feed local) e uma referência por **caminho absoluto** entre os
dois repositórios continuam descartados (quebram a independência dos repositórios). Das duas
alternativas levantadas na primeira etapa, foi implementada a **opção 1 — referência de
projeto por caminho relativo, via propriedade MSBuild configurável**:
`PXToolBox.CorelHost.Dev.csproj` define `ListForgeRepoDir` (padrão: `..\..\..\ListForge`,
assumindo os dois repositórios como pastas irmãs — mesmo padrão já usado por
`CorelDrawInstallDir`), sobrescrevível com `-p:ListForgeRepoDir="..."`, e um target
`PXToolBoxCheckListForgeCore` que falha o build com uma mensagem explicativa se
`ListForge.Core.csproj` não existir nesse caminho. Build real do host (`dotnet build
PXToolBox.slnx`) e da suíte de testes confirmam a referência resolvida e funcionando — não é
só o experimento descartável da primeira etapa, é o projeto real. A **opção 2 (git submodule/
subtree)** continua registrada como alternativa a revisitar se/quando os dois produtos
precisarem de ciclos de release independentes com uma versão do núcleo congelada — não
implementada agora, por não haver essa necessidade concreta ainda (ver AGENTS.md).

Impacto aceito: o build do host do PX agora depende da presença de um checkout do ListForge
(branch com `ListForge.Core`, ou posterior) na máquina, como pasta irmã deste repositório —
igual à dependência já existente com a instalação do CorelDRAW. Os dois repositórios
continuam independentes (nenhum publica pacote nem copia fontes do outro).

**Interface implementada — modo Clássico**: `host/PXToolBox.CorelHost.Dev/ListForgeIntegrated/`
— `ClassicWorkspaceWindow` (uma `Window` própria, não o docker compacto: espaço real para
editar/revisar listas, conforme pedido) e `IntegratedLicenseService`. Reproduz o fluxo
essencial do standalone (`UI/Views/EditorView.xaml`/`ViewModels/MainViewModel.cs`): entrada de
lista, separador (com "Padrão (,)"), capitalização ("Original"/"Tudo maiúsculo"/"Tudo
minúsculo") e ordenação ("Original"/"Crescente"/"Decrescente") — mesmos rótulos e valores
internos do standalone —, botão Processar (assíncrono, `Task.Run`, nunca trava a UI e nunca
acessa COM do Corel fora da thread principal — esta janela não depende de documento aberto no
CorelDRAW), lista de avisos/erros de validação, abas "Lista organizada"/"Prévia JSON", e
"Salvar saída"/"Gerar JSON" (via `OutputExportService`, mesmo nome versionado automático do
standalone — nunca sobrescreve silenciosamente). A interface só chama
`ProcessingWorkflowService`/`OutputExportService` do núcleo — nenhuma regra de parsing,
validação ou exportação foi reimplementada no code-behind. Acesso: botão "Abrir ListForge
Integrado" no painel principal (seção "Listas"), ao lado de "Abrir ListForge Standalone ↗"
(renomeado só para distinguir dos dois, sem mudança de comportamento) — reabre a mesma janela
em cliques repetidos (sem duplicar áreas de trabalho) e preserva a lista em edição ao alternar
o foco com o Corel (a janela não é recriada nem fechada sozinha). Ao fechar com alterações
ainda não exportadas, pergunta explicitamente (continuar editando ou descartar) — nunca perde
dados silenciosamente.

**Decisão pendente — licenciamento/Trial do modo integrado**: o standalone controla créditos
Trial via `TrialManager`/`ConfigManager` (infraestrutura ligada a caminhos da máquina e DPAPI,
deliberadamente fora do núcleo compartilhado). O host do PX não tem, hoje, nenhum mecanismo
real de licença do ListForge alcançável para aplicar — não há nada para contornar. Em vez de
inventar silenciosamente uma política nova, `IntegratedLicenseService` implementa
`ILicenseService` sem bloquear o processamento, mas dizendo isso explicitamente na própria
interface (`Edition`/`ProcessingStatusSuffix` incluem "licenciamento ainda não decidido" /
"decisão pendente") — nunca lê nem escreve o estado Trial do standalone. Decisão real (Trial
próprio do modo integrado? comparte o pool do standalone? sempre completo em DEV?) continua
pendente, para decisão consciente antes de uma distribuição de Produção.

**Diferenças conhecidas em relação ao standalone (verificação, não equivalência por suposição)**:
- Usa sempre `SizeConfig.Default()` do núcleo — ainda não lê um `SizeConfig` customizado que o
  usuário tenha salvo no standalone (esse arquivo é resolvido por `ListForge.Config.ConfigManager`,
  infraestrutura fora do núcleo). Se o usuário customizou tamanhos no standalone, os dois modos
  processam a mesma lista de forma diferente até isso ser resolvido.
- Mapeamento avançado de posições de peça (`JsonPieceMappingOptions`) sempre desabilitado —
  não há campo de UI para isso ainda (opção avançada do standalone, não essencial para esta
  primeira versão).
- Sem Localizar/Substituir, Desfazer, histórico de processamento, perfis de trabalho, Forge
  mode nem importação de arquivo/link — só os campos pedidos na tarefa (entrada, opções,
  validação, revisão, exportação).

**Verificação feita**: build e suíte de testes do ListForge (281/281, 0 avisos, antes e depois
desta etapa) e do PX (`dotnet build PXToolBox.slnx` — 0 avisos/0 erros com a referência real ao
ListForge.Core; `dotnet test tests/PXToolBox.Tests/PXToolBox.Tests.csproj` — 55/55 na perna
net8.0, 69/69 na perna net48, que soma as 65 já existentes com as 4 novas de
`ListForgeIntegratedTests`). Essas 4 novas comparam entrada/opções idênticas processadas pelo
mesmo `ProcessingWorkflowService` consumido pelo host do PX: um caso válido cujo texto de saída
e campos de JSON (nome, número, apelido, tipo sanguíneo) são copiados literalmente do teste
equivalente do ListForge (`MainFlowIntegrationTests.ValidInput_...`) — prova de resultado
idêntico, não só "mesma biblioteca"; um caso inválido (linha sem tamanho reconhecível); um caso
com tipos/grupos de tamanho mistos (adulto/infantil/meião); e o comportamento do
`IntegratedLicenseService`. Nenhum smoke test real foi feito dentro do CorelDRAW nesta etapa —
ver seção 13.

**Próximo passo concreto** (fora do escopo desta tarefa): decidir o licenciamento/Trial do modo
integrado; decidir e implementar o modo **PX** (reaproveitando objetos/layout do documento
ativo, hoje inexistente); validar abertura/processamento/exportação dentro do CorelDRAW real
(seção 13); decidir se/como ler um `SizeConfig` customizado do standalone.

### Correção de usabilidade (teclado, contraste) e extensão de funções (iteração 2)

**Causa do conflito de teclado identificada por investigação, não por suposição**:
`ClassicWorkspaceWindow` corre no mesmo thread/Dispatcher que a UI do Corel hospeda (o host
"wpfhost" do Corel carrega o docker principal nesse mesmo thread — seção 3). O sintoma relatado
(a tecla ATIVA a ferramenta do Corel em vez de inserir o caractere — nunca os dois) é a
assinatura exata do padrão clássico de loop de mensagens Win32 `TranslateAccelerator` antes de
`TranslateMessage`/`DispatchMessage`: quando a tecla casa com a tabela de aceleradores do Corel,
`TranslateAccelerator` consome o `WM_KEYDOWN` sozinho, e `TranslateMessage` nunca roda para
gerar o `WM_CHAR` correspondente — por isso a caixa de texto nunca recebe o caractere.

**Correção aplicada nesta iteração (2) — confirmada pelo usuário que NÃO resolveu a digitação**:
`ComponentDispatcher.ThreadFilterMessage` (`System.Windows.Interop`). A causa de ela não
funcionar está explicada e corrigida na iteração 3, logo abaixo — mantendo este parágrafo
apenas como registro do que foi tentado e por quê não bastava.

**Limitação honesta registrada nesta iteração**: a causa do conflito ORIGINAL (por que o Corel
engole a tecla) foi deduzida corretamente da assinatura do sintoma e do mecanismo de hospedagem
já documentado na seção 3 — essa parte se confirmou certa. O que faltava era entender por que o
mecanismo de correção escolhido (`ComponentDispatcher`) não chegava a rodar — ver iteração 3.

**Contraste corrigido**: `ComboBox`/`ComboBoxItem` e `TabItem` em `ClassicWorkspaceWindow.xaml`
não tinham template próprio — o padrão do WPF desenha o popup do ComboBox e a aba inativa com
fundo claro do sistema, e os estilos herdados do tema escuro (`TextBlock`/`Foreground` claro)
resultavam em texto claro sobre fundo claro. Corrigido com os mesmos templates já usados e
corretos no docker principal (`ControlUI.xaml`): estados explícitos (normal, sob o mouse,
selecionado/ativo, desabilitado) para `ComboBoxItem`, `ComboBox` (com `Popup` com fundo próprio)
e `TabItem`. Estado desabilitado usa opacidade reduzida (0.4–0.5) em vez de mudar cor sem
diferenciação — nenhum controle foi habilitado só para "corrigir" a aparência.

**Texto técnico movido**: a linha sobre licenciamento pendente e a referência a arquivo/seção
de arquitetura saíram da área principal (`AccessStatusText`, agora só "Edição: Integrado (DEV)
— processamento disponível/bloqueado") para o `Diagnóstico` (`DiagnosticsText`, recolhido por
padrão) — mesmo padrão do docker principal. Nenhuma verificação de licença foi removida ou
contornada; a decisão de licenciamento/Trial do modo integrado continua pendente (documentada
ali e nesta seção).

**Funções acrescentadas** (já viáveis sem novo suporte do núcleo, dentro do escopo de entrada/
revisão/exportação): Abrir Lista (só `.txt` — não usa `FileImporter`, que continua fora do
núcleo por depender de OCR/PDF/Word/Excel), Salvar Entrada/Salvar Como (`.txt`), Remover
espaços (`ListProcessor.CleanTextBySeparator`, já existia no núcleo), Limpar tudo, Copiar
saída/Copiar JSON, Localizar/Substituir (implementação própria, direta na caixa de texto — não
depende de `TextSearchHelper`, que já era só suporte de UI no standalone, fora do domínio de
processamento).

**Pendências documentadas** (tabela completa de paridade entregue junto ao relatório desta
tarefa): Comparar entrada/saída depende de `ListComparisonService`, que usa `AppLogger` e ainda
não foi extraído para o núcleo; Salvar avançado, histórico de processamento e Extrair lista de
link dependem de serviços ligados a `ConfigManager`/`AppLogger` (infraestrutura, fora do
núcleo por decisão já tomada); Perfil de trabalho e preferências de interface continuam
fora do núcleo por decisão de produto (AGENTS.md); modo Forge (`TglAdvancedList`) é só efeito
visual do standalone, sem relação com processamento — não considerado necessário.

### Correção real do teclado e do contraste da aba selecionada (iteração 3)

O usuário confirmou, testando a build da iteração 2 dentro do Corel real, que a digitação
continuava não funcionando e que a aba "Lista organizada" selecionada continuava com contraste
ruim (fundo azul, texto cinza pouco legível). Esta iteração reinvestigou as duas causas sem
reutilizar a hipótese anterior sem prová-la, e suspendeu a inclusão de qualquer função nova.

**Por que `ComponentDispatcher.ThreadFilterMessage` nunca funcionaria aqui**: esse evento só é
levantado de DENTRO do próprio loop de mensagens do `System.Windows.Threading.Dispatcher` do
WPF — ou seja, só quando é o próprio WPF quem chama `GetMessage`/`PeekMessage` (via
`Dispatcher.Run()`/`Application.Run()`). Este add-on nunca chama isso: é o loop nativo do Corel
que bombeia as mensagens desta thread (confirmado na seção 3 — carregamento via reflexão pelo
host "wpfhost"). Logo, por mais correta que a causa do conflito original estivesse (ela estava),
o evento ao qual nos inscrevemos simplesmente **nunca disparava**, com ou sem tecla de atalho —
não é uma questão de refinar a lista de teclas, o mecanismo inteiro estava inerte. Isso explica,
sem suposição adicional, por que a correção da iteração 2 não teve efeito observável nenhum.

Há ainda um segundo detalhe que a iteração 2 tinha errado mesmo supondo que o mecanismo
funcionasse: `TranslateAccelerator(hWnd, hAccel, &msg)`, pela própria documentação da API,
decide só pelo código de tecla e estado do teclado contidos em `msg` — **não verifica se
`msg.hwnd` pertence à janela passada como `hWnd` (ou a uma descendente dela)**. Então mesmo o
`ClassicWorkspaceWindow` sendo uma `Window` própria e totalmente separada da janela principal do
Corel (não um HWND filho dela), uma tecla que bate com a tabela de aceleradores do Corel é
consumida de qualquer forma — é o comportamento clássico, bem documentado, de lidar com múltiplas
janelas de topo na mesma thread com uma tabela de aceleradores só.

**Correção aplicada**: um hook `WH_GETMESSAGE` instalado via
`SetWindowsHookEx(WH_GETMESSAGE, callback, hInstance, GetCurrentThreadId())` — um hook de
**thread** (restrito ao id da própria thread de UI, dentro do próprio processo), não um hook
global (que exigiria `dwThreadId=0` e afetaria todos os processos/threads do sistema — a
documentação do Win32 trata os dois como mecanismos diferentes; aqui `dwThreadId` é sempre o id
da própria thread de UI deste add-on). Diferente de `ComponentDispatcher`, `WH_GETMESSAGE` é
chamado pelo próprio sistema operacional dentro da implementação de `GetMessage`/`PeekMessage`,
então dispara independente de quem está chamando `GetMessage` — inclusive o loop nativo do
Corel — e roda ANTES que esse loop chegue a chamar `TranslateAccelerator`. No callback
(`ClassicWorkspaceWindow.GetMessageHookProc`):
1. Só age quando a mensagem é `WM_KEYDOWN`, está de fato sendo retirada da fila (`PM_REMOVE` —
   nunca ao só espiar via `PeekMessage` sem remover), o `hwnd` é o desta janela, a janela está
   ativa, o foco lógico do WPF está num `TextBox`/`ComboBox` desta janela, e a tecla passa pelo
   filtro de `ShouldForceRouteToEditor` (sem Alt; com Ctrl só A/C/V/X/Z; sem Tab, teclas de
   bloqueio, teclas de sistema/Windows ou F1-F24 — todo o resto, incluindo letras, números,
   acentuação/teclas mortas do layout ABNT, Enter, Espaço, Backspace, Delete, Insert, setas,
   Home/End/PageUp/PageDown e Escape, é roteado).
2. Chama `TranslateMessage`+`DispatchMessage` sobre uma cópia da mensagem — isso gera o `WM_CHAR`
   e entrega normalmente ao WndProc desta janela (do próprio WPF), exatamente como aconteceria se
   o Corel não a tivesse interceptado primeiro.
3. Zera a mensagem original (`WM_NULL`) antes de devolvê-la ao sistema — assim, quando o loop do
   Corel continuar e tentar `TranslateAccelerator`/`TranslateMessage`/`DispatchMessage` sobre ela,
   não há mais nada para processar (sem isso, a tecla seria processada duas vezes).

Isso não desativa nenhum atalho do Corel: eles continuam na tabela de aceleradores do Corel e
voltam a funcionar normalmente assim que o foco sai de um campo editável desta janela — inclusive
qualquer combinação Ctrl/Alt fora de A/C/V/X/Z, mesmo dentro de um campo editável, continua sendo
tratada pelo Corel. O hook é instalado em `Window_Loaded` e removido em `Window_Closed` (o
delegate é mantido num campo da instância para não ser coletado pelo GC enquanto o hook estiver
ativo) — vive só enquanto esta janela existe; reabrir a janela depois de fechada não acumula
hooks (confirmado por leitura do código: um só campo `_getMessageHook`, substituído a cada
`Window_Loaded`, removido em `Window_Closed`).

**Diagnóstico temporário e localizado** (seção "Diagnóstico" da própria janela, `KeyboardDiagText`
— só contadores, nunca o conteúdo digitado): mostra quantas teclas o hook observou e quantas
foram roteadas manualmente nesta sessão. Serve para o usuário confirmar, dentro do Corel real, se
o hook está sendo de fato atingido — se o primeiro número ficar em 0 ao digitar, o hook não está
rodando (janela inativa, foco fora de um campo editável, ou a instalação do hook falhou).

**Limitação honesta**: a causa e o mecanismo foram verificados por leitura da documentação da API
Win32 (`TranslateAccelerator`, `WH_GETMESSAGE`) e por análise direta do código desta iteração e da
anterior — não por observar o CorelDRAW real em execução com um depurador anexado (impossível
nesta sessão). O contador de diagnóstico foi acrescentado exatamente para que essa confirmação
real não dependa de teste isolado de interface. **Teste isolado de interface não comprova a
correção do conflito de teclado no host** — só o roteiro da seção 13, dentro do Corel real, faz
isso.

**Contraste da aba selecionada — causa real**: o `Trigger IsSelected` da iteração anterior
aplicava a cor via `TextElement.Foreground` (uma propriedade herdada) no `Border` ancestral do
cabeçalho da aba. Mas `Window.Resources` define `<Style TargetType="TextBlock">` com
`Foreground` como "Style setter" diretamente no `TextBlock` que o `ContentPresenter` gera para o
texto do cabeçalho (conteúdo da aba é uma string simples: "Lista organizada"/"Prévia JSON") — e,
na ordem de precedência de propriedades do WPF, um Style setter aplicado diretamente no elemento
sempre vence um valor herdado de um ancestral, mesmo vindo de um Trigger. Resultado: a cor
"selecionada" nunca chegava a mudar o texto; ele sempre usava a cor clara do tema
(`Px.Text`) por herança perdendo para o Style — daí o texto cinza-claro sobre o fundo azul do
estado selecionado, baixa legibilidade.

**Correção aplicada**: um `TextBlock` nomeado (`HeaderText`), criado diretamente dentro do
próprio `ControlTemplate` do `TabItem` (não mais via `ContentPresenter`+`ContentSource="Header"`),
ligado ao cabeçalho por `Text="{TemplateBinding Header}"`. Setters de Trigger com
`TargetName="HeaderText"` têm precedência de "Template Trigger", que vence tanto o Style setter
quanto o valor definido diretamente no template — por isso os Setters de `IsSelected`/
`IsMouseOver` agora realmente mudam a cor do texto. Estados cobertos: normal (texto muted sobre
`Px.Surface1`), selecionada (fundo `Px.Accent` + texto `Px.AccentText`, em negrito — contraste
alto e uma segunda indicação de seleção além da cor do texto), sob o mouse (fundo `Px.Surface2` +
texto claro), foco de teclado (borda de destaque), desabilitada (opacidade reduzida). Verificado
alternando a seleção entre "Lista organizada" e "Prévia JSON" por leitura do XAML/precedência —
confirmação visual real depende do smoke test da seção 13.

**Build e testes desta iteração**: `dotnet build PXToolBox.slnx -c Debug` — 0 avisos/erros;
`dotnet test PXToolBox.slnx` — 55/55 (net8.0) + 69/69 (net48), sem alteração de contagem (nenhuma
função nova foi acrescentada nesta iteração, por instrução explícita). Build completo via
`scripts/Build-Release.ps1 -Version "0.1.0-dev.10" -Channel Dev` — restauração, build Debug,
testes, build Release, publicação do host, compilação do instalador (Inno Setup 6) e checksums,
todos reais e bem-sucedidos; versões `0.1.0-dev.1` a `0.1.0-dev.9` preservadas em disco sem
alteração.

**Nada foi alterado** em processamento, exportação, licenciamento ou nas ferramentas de Saídas
nesta iteração, por instrução explícita do pedido.

### Diagnóstico e correção candidata do foco nativo (iteração 4)

O usuário testou a build da iteração 3 dentro do Corel real e reportou que a digitação
continuava sem funcionar, com o diagnóstico mostrando **0 teclas observadas e 0 roteadas** — ou
seja, o hook `WH_GETMESSAGE` nunca chegou a ser atingido nem uma vez. Esta iteração não repetiu
a hipótese da iteração 3 sem revisá-la: investigou por que o próprio hook (não só o filtro de
tecla) nunca disparava, e tratou "zero eventos" como um sintoma a decompor, não como prova de
uma causa só.

**Falha de instrumentação encontrada por revisão de código**: a versão da iteração 3 chamava
`SetWindowsHookEx(...)` e nunca verificava o valor de retorno nem registrava o erro nativo em
caso de falha (`Marshal.GetLastWin32Error()`) — ou seja, mesmo que a instalação do hook
estivesse falhando silenciosamente a cada abertura da janela, não havia como saber só pelo
diagnóstico existente. Essa lacuna foi corrigida: `ThreadKeyboardRouter.InstallOk`/`InstallError`
agora expõem exatamente isso no texto de diagnóstico.

**Duas causas candidatas identificadas e corrigidas nesta rodada** (ambas plausíveis a partir da
leitura do código e da documentação da API Win32 — nenhuma delas pôde ser observada ao vivo
dentro do Corel nesta sessão, por isso seguem como candidatas até o próximo teste real do
usuário):

1. **`hMod` possivelmente inválido**: o código da iteração 3 passava
   `Marshal.GetHINSTANCE(typeof(ClassicWorkspaceWindow).Module)` como `hMod`. Esse assembly é
   carregado por reflexão pelo host "wpfhost" do Corel (seção 3) — se esse carregamento não
   passar pelo loader nativo do Windows a partir de um arquivo em disco (por exemplo, se o host
   ler os bytes do `.dll` e chamar `Assembly.Load(byte[])`, técnica comum para não travar o
   arquivo em disco), `Marshal.GetHINSTANCE` pode retornar um handle que `SetWindowsHookEx`
   recusa, falhando a instalação do hook sem exceção nenhuma do lado gerenciado. Corrigido:
   `ThreadKeyboardRouter.Install()` agora passa `hMod = IntPtr.Zero` — valor documentado pela API
   como válido para um hook de **thread** quando `dwThreadId` identifica uma thread do processo
   atual (nosso caso, já que o add-on roda dentro do processo do Corel), eliminando a
   dependência de `GetHINSTANCE` ter um handle válido.
2. **Falta de foco nativo do Win32**: `Window.Show()` numa `Window` WPF hospedada por reflexão
   dentro de um processo nativo não garante que `GetFocus()` passe a apontar para o `HWND` dessa
   janela — mesmo com a janela visível, ativa e com foco lógico do WPF num `TextBox`. Se
   `GetFocus()` continuar apontando para a janela do Corel, toda `WM_KEYDOWN` chega ao hook com
   `msg.hwnd` do Corel, nunca desta janela — e como o segundo passo do mecanismo
   (`TranslateMessage`/`DispatchMessage` sobre a cópia) entrega a mensagem ao WndProc de **quem
   quer que seja** `msg.hwnd`, forçar isso sobre uma mensagem com `hwnd` do Corel não produziria
   nenhum `WM_CHAR` neste editor — e, com o filtro antigo (`msg.hwnd == _ownHwnd` como condição
   para até contar o evento), explicaria exatamente "0 observadas e 0 roteadas": o hook podia
   estar disparando normalmente para cada tecla, só que todas rejeitadas antes do contador por
   terem `hwnd` diferente do esperado. Corrigido: `ThreadKeyboardRouter.Install()`/
   `ReassertFocus()` chamam `Window.Activate()` e, nativamente, `SetFocus(OwnHwnd)`; e a janela
   agora também chama `ReassertFocus()` a cada `Window.Activated` (o Corel pode retomar o foco
   sozinho entre uma ativação e outra, não só na primeira abertura). O botão "Abrir ListForge
   Integrado" (`ControlUI.OnOpenListForgeIntegrated`) também passou a chamar `.Activate()`
   explicitamente depois do primeiro `.Show()` (o caminho de reabrir uma janela já existente já
   fazia isso; só a primeira abertura não fazia).

**Diagnóstico muito mais granular para a próxima rodada de teste real**: em vez de dois
contadores (observadas/roteadas), `ThreadKeyboardRouter.DescribeState()` agora expõe, tudo numa
única linha de texto, sem nenhum conteúdo digitado: sucesso/erro da instalação do hook (com o
código de erro nativo, se houver), o `hwnd` desta janela, o id da thread do hook, se o foco
nativo bateu com o `hwnd` desta janela depois de `Activate()`+`SetFocus()`, quantas `WM_KEYDOWN`
foram vistas na thread **com qualquer `hwnd`**, quantas delas tinham o `hwnd` desta janela
especificamente, quantas foram roteadas, e o último `hwnd`/foco nativo observados numa tecla.
Isso permite ao próximo teste real distinguir, sem ambiguidade, qual elo da cadeia ainda falha
se o problema persistir (hook não instala vs. instala mas nunca é atingido vs. é atingido só com
`hwnd` de outra janela vs. é atingido com o `hwnd` certo mas o filtro de tecla rejeita vs. é
roteado e ainda assim nada aparece no campo).

**Mecanismo de interceptação compartilhado e extraído**: toda a lógica de P/Invoke, hook e
roteamento foi movida para uma classe nova, `ThreadKeyboardRouter`
(`ListForgeIntegrated/ThreadKeyboardRouter.cs`), usada tanto por `ClassicWorkspaceWindow` quanto
por uma janela nova e **temporária** de diagnóstico, `KeyboardDiagMiniWindow` (um único
`TextBox`, aberta pelo botão "Abrir teste mínimo de teclado (diagnóstico temporário)" dentro do
Expander "Diagnóstico"). Objetivo explicitamente pedido: isolar se o bloqueio reproduz numa
janela mínima com exatamente o mesmo mecanismo (confirmaria um problema de hospedagem/foco
comum às duas janelas) ou só na janela completa do modo Clássico (apontaria para algo
específico da UI maior — outro handler consumindo a tecla antes, por exemplo). Por ser
diagnóstico e não uma função do produto, deve ser removida depois que a causa real for
confirmada e corrigida dentro do Corel.

**Nenhum mecanismo de interceptação por tentativa foi acrescentado**: o único mecanismo
continua sendo o hook de thread `WH_GETMESSAGE` já existente — esta rodada corrigiu sua
instrumentação (verificação de erro) e tratou a causa candidata de foco nativo, sem introduzir
hook global, reenvio indiscriminado de mensagens, simulação de digitação ou handlers
concorrentes. Nenhum atalho do Corel foi removido.

**Build e testes desta iteração**: `dotnet build PXToolBox.slnx -c Debug` — 0 avisos/erros;
`dotnet test PXToolBox.slnx` — 55/55 (net8.0) + 69/69 (net48), sem alteração de contagem (o
mecanismo de teclado continua não testável por teste automatizado sem um host Win32/Corel real;
nenhuma lógica nova testável por unidade foi introduzida). Nenhum instalador foi gerado nesta
rodada até o momento deste registro.

**Limitação honesta, reafirmada**: as duas causas candidatas desta iteração foram identificadas
por leitura de código e da documentação da API Win32 (`SetWindowsHookEx`, `GetFocus`,
`SetFocus`), não por observar o CorelDRAW real em execução com um depurador anexado. **Não se
declara o conflito de teclado resolvido** — só que o diagnóstico passou a ser granular o
suficiente para confirmar ou refutar cada causa candidata no próximo teste real, e que as duas
correções candidatas (hMod e foco nativo) são aplicadas sem risco conhecido para o que já
funcionava (não alteram nenhum atalho do Corel, não mudam o comportamento quando a tecla não é
destinada a um campo editável desta janela).

**Confirmado pelo usuário dentro do Corel real**: a digitação e o processamento passaram a
funcionar com a build `0.1.0-dev.11`. A partir daqui o mecanismo de teclado (`ThreadKeyboardRouter`
+ `KeyboardRoutingPolicy`) é tratado como estável e **preservado sem alteração** nas iterações
seguintes, que passam a acrescentar funções.

**Regressão incluída nos testes automatizados**: a regra de decisão "esta tecla deve ser
roteada para o editor?" foi extraída para uma classe pura e pública,
`KeyboardRoutingPolicy.ShouldForceRouteToEditor(key, ctrl, alt, windowActive,
focusInEditableControl)` (`ListForgeIntegrated/KeyboardRoutingPolicy.cs`) — não depende de
`Window`/`Dispatcher` ao vivo, só dos fatos já apurados pelo `ThreadKeyboardRouter`. Cobrida por
`KeyboardRoutingPolicyTests` (`tests/PXToolBox.Tests/ListForgeIntegratedTests.cs`, perna net48):
teclas de edição simples roteadas, `Ctrl+A/C/V/X/Z` roteados, qualquer outro `Ctrl+tecla` e
qualquer `Alt+tecla` deixados para o Corel, F1–F24 e teclas de sistema/bloqueio/modificador
deixadas para o Corel, e a tecla não é roteada se a janela não está ativa ou o foco não está
num campo editável. Isso cobre a **política** de filtragem — o mecanismo real de entrega (hook
`WH_GETMESSAGE`, foco nativo do Win32) continua dependendo de um host Win32/Corel real e **não**
é exercitado por este teste; a perna net48 do projeto de testes precisou de `UseWPF=true`
(`PXToolBox.Tests.csproj`) para acessar `System.Windows.Input.Key` sem duplicar esse tipo.

### Paridade funcional com o standalone — 1ª rodada de implementação (iteração 5)

Com a digitação confirmada funcionando (iteração 4), teclado e contraste passam a ser tratados
como estáveis e preservados sem alteração — esta iteração só acrescenta funções. Inventário
completo em `docs/listforge-classic-parity.md` (41 linhas, referência real: `C:\Projetos\ListForge`,
branch `main`, commit `f429381`, v2.1.44 — não `F:\Projetos\ListForge`, que não existe nesta
máquina). Esta rodada implementou os itens de maior prioridade (processamento/dados) e menor
risco/dependência do inventário:

1. **Bloqueio de resultado desatualizado** (`OutputFreshnessTracker`, novo) — conta revisão da
   entrada e das opções de processamento (separador/capitalização/ordenação); `ConfirmIfStale`
   avisa e pede confirmação explícita antes de Copiar saída/Copiar JSON/Salvar saída/Gerar JSON
   quando o resultado não corresponde mais à entrada atual, com aviso visível na própria aba de
   saída (`OutputStaleWarning`). Pedido explicitamente no texto da tarefa ("a interface não pode
   exportar silenciosamente uma saída anterior").
2. **Navegação da lista de avisos/erros até a linha** (`TextLineLocator`, novo) — clique num item
   de `IssuesList` seleciona e rola até a linha correspondente em `InputTextBox`.
3. **Localizar/Substituir movido para o núcleo compartilhado** — `TextSearchHelper.cs` (zero
   dependência de `AppLogger`/`ConfigManager`) foi movido do projeto do standalone
   (`C:\Projetos\ListForge\Core\`) para o projeto compartilhado
   (`C:\Projetos\ListForge\ListForge.Core\Core\`), reescrito para netstandard2.0 (sintaxe de
   Range/Index e a sobrecarga de 3 argumentos de `string.Replace` não existem nesse target;
   substituídas por `Substring` e um laço `IndexOf` manual, mesmo comportamento observável). O
   `ClassicWorkspaceWindow` passou a chamar `TextSearchHelper.FindMatches/ReplaceAt/ReplaceAll`
   em vez de reimplementar a busca/substituição à mão — a navegação relativa ao cursor com
   wraparound continua própria do host (decisão de UX do integrado, não uma regra de negócio
   duplicada). Validado reconstruindo e testando **os dois repositórios**: ListForge standalone
   (`dotnet test` 281/281, sem alteração de contagem, inclusive o `TextSearchHelperTests.cs` que
   já existia lá) e PXToolBox (ver abaixo).

Também, por já haver lacunas conhecidas no modo Clássico, o título da janela passou a indicar
isso explicitamente: `"PX Tool Box DEV — ListForge Integrado (Clássico — em desenvolvimento)"`.

**Regressão automatizada acrescentada**: `OutputFreshnessTrackerTests` (5 casos) e
`TextLineLocatorTests` (7 casos, inclusive CRLF) em `tests/PXToolBox.Tests/ListForgeIntegratedTests.cs`
— as duas classes novas são lógica pura, sem WPF, testável sem janela real.

**Build e testes desta iteração**: `dotnet build` — 0 avisos/erros nos dois repositórios;
`dotnet test` do PXToolBox — 55/55 (net8.0) + 123/123 (net48, 123 = 69 anteriores + 42 de
`KeyboardRoutingPolicyTests` da iteração 4 + 12 novos desta rodada); `dotnet test` do ListForge
standalone — 281/281, sem alteração de contagem. Nenhum smoke test real no Corel foi feito para
as três funções desta rodada — ver pendências abaixo.

**Pendente desta mesma tarefa, não implementado nesta rodada** (ordem de prioridade do pedido,
motivo registrado em `docs/listforge-classic-parity.md`): Lista avançada (mapeamento de peça por
slot JSON — serviço já compartilhado, só falta UI), Processar com prévia de confirmação, Comparar
entrada/saída, Salvar avançado, Extrair lista de link, Resumo de tamanhos, e todo o grupo de
Configurações (perfis de trabalho, exportar/importar config, pacote de suporte, pasta de
config/log, tamanhos customizados) — este último bloqueado numa decisão de produto ainda não
tomada (onde o PX Tool Box guarda dados de usuário do modo integrado), não por implementação
faltante por omissão. Enquanto essas lacunas existirem, o modo continua identificado como
"Clássico — em desenvolvimento".

### Paridade funcional com o standalone — 2ª rodada de implementação (iteração 6)

Entre as duas rodadas, uma atualização do Windows removeu temporariamente (e depois restaurou,
em versão mais nova) a instalação do .NET nesta máquina, retirando de propósito ou não o runtime
`Microsoft.NETCore.App 8.0.x` — diagnóstico somente leitura confirmado por processos
`msiexec`/`TiWorker`/`setup` em execução e pelo log de eventos do Windows Update; nenhum código
havia mudado desde os últimos testes aprovados (confirmado por timestamp de arquivo). Corrigido
instalando (nunca reinstalando/reparando) o runtime .NET 8 via
`winget install --id Microsoft.DotNet.Runtime.8`, com autorização explícita do usuário.

Com o ambiente restaurado, esta rodada implementou mais quatro itens do inventário de
`docs/listforge-classic-parity.md`, por ordem de prioridade do pedido original:

1. **Extrair lista de link** — `LinkListImportService.cs` (+ `ExtractedListDestination`,
   `LinkListImportResult`) movido de `C:\Projetos\ListForge\Services\` (standalone-only) para
   `C:\Projetos\ListForge\ListForge.Core\Services\` (projeto compartilhado). Única dependência
   real era `ConfigManager.AppName`, usada só para compor o cabeçalho HTTP `User-Agent` — trocada
   por um valor fixo (`"ListForge/link-import"`), sem efeito observável no resultado da extração.
   Também exigiu troca de `ReadAsStringAsync(CancellationToken)` (sobrecarga exclusiva de .NET
   5+/Core) por `ReadAsStringAsync()` (netstandard2.0) — cancelamento continua funcionando no
   `GetAsync` anterior, só não interrompe a leitura do corpo já recebido. `LinkImportDialog`
   (nova janela, URL + nova lista/acrescentar) chama o mesmo serviço a partir de
   `ClassicWorkspaceWindow.OnExtractFromLink`. Por rodar no mesmo thread de UI do CorelDRAW que
   a janela principal, usa o mesmo `ThreadKeyboardRouter` — sem isso, o campo de URL sofreria o
   mesmo bloqueio de teclado original.
2. **Lista avançada / mapeamento de peça por slot JSON** — `JsonPieceMappingService` e
   `JsonPieceMappingOptions` já eram compartilhados; só faltava a UI. `AdvancedPieceSlot` (novo,
   classe pura) representa um seletor por posição de tamanho; `RefreshAdvancedSlots()` usa
   `JsonPieceMappingService.EstimateRequiredSlots`/`ClampSlotCount` (mesmos métodos do standalone)
   para dimensionar os seletores conforme a entrada/separador mudam. Simplificação deliberada em
   relação ao standalone: sem filtro cruzado removendo visualmente opções já usadas em outros
   slots — `JsonPieceMappingService.Validate` (já chamado dentro de
   `ProcessingWorkflowService.Execute`) rejeita uma ordem com tipos repetidos e isso aparece como
   qualquer outro problema de validação, na mesma lista de avisos/erros já existente; não é um
   bypass, é uma UI mais simples que ainda impede o resultado errado, só não impede visualmente a
   escolha repetida antes de processar.
3. **Resumo de tamanhos processados** — `SizeSummaryBuilder` (novo, puro), usando
   `ListForge.Core.SizeHelper` (já compartilhado) para somar quantidade por tamanho entre todas as
   linhas processadas; exibido em `SizeSummaryText`, atualizado a cada processamento com sucesso.

**Regressão automatizada acrescentada**: `AdvancedJsonPieceMapping_MatchesListForgeStandaloneFixture`
(reproduz a mesma entrada/ordem personalizada de
`ListForge.Tests/JsonPieceMappingTests.cs:EnabledAdvancedMapping_AppliesSizesToConfiguredPieceOrder`
através do `ProcessingWorkflowService` inteiro — o mesmo caminho que `OnProcess` usa),
`SizeSummaryBuilderTests` (3 casos) e `AdvancedPieceSlotTests` (4 casos) em
`tests/PXToolBox.Tests/ListForgeIntegratedTests.cs`.

**Build e testes desta iteração**: `dotnet build` — 0 avisos/erros nos dois repositórios;
`dotnet test` do PXToolBox — 55/55 (net8.0) + 131/131 (net48, 131 = 123 anteriores + 8 novos
desta rodada); `dotnet test` do ListForge standalone — 281/281, sem alteração de contagem após a
extração do `LinkListImportService`. Nenhum smoke test real no Corel foi feito para as quatro
funções desta rodada (extrair lista de link, lista avançada, slots de mapeamento, resumo de
tamanhos) — ver pendências em `docs/listforge-classic-parity.md`.

**Pendente, não implementado nesta rodada** (ordem de prioridade do pedido, motivo registrado em
`docs/listforge-classic-parity.md`): Processar com prévia de confirmação, Comparar entrada/saída,
Salvar avançado, e todo o grupo de Configurações (perfis de trabalho, exportar/importar config,
pacote de suporte, pasta de config/log, tamanhos customizados) — este último continua bloqueado
numa decisão de produto ainda não tomada (onde o PX Tool Box guarda dados de usuário do modo
integrado). Enquanto essas lacunas existirem, o modo continua identificado como "Clássico — em
desenvolvimento".

### Paridade funcional com o standalone — 3ª rodada de implementação (iteração 7)

Grupo escolhido por ser o que mais impedia um fluxo completo do standalone no integrado:
**Processar com prévia de confirmação** é o fluxo *padrão* do standalone (o botão principal da
tela chama `AnalyzeAndProcessWithPreview`, não `ProcessAndPreview`) — até esta rodada, o
integrado só tinha o modo rápido, sem nenhuma forma de ver contagem de válidos/avisos/inválidos,
tamanhos ou tipos de peça antes de efetivamente processar.

1. **`ProcessingPreviewService` + modelos movidos para o núcleo compartilhado** —
   `Services/ProcessingPreviewService.cs` e `Models/ProcessingPreview.cs` (ambos standalone-only)
   movidos para `ListForge.Core/Services/` e `ListForge.Core/Models/`. Únicas dependências reais:
   2 chamadas a `AppLogger` (telemetria, sem efeito no resultado retornado — removidas) e
   `line.Split(separator)` em `LooksLikeHeaderLine` (sobrecarga `string.Split(string)`
   indisponível em netstandard2.0 — trocada por `Split(new[] { separator }, StringSplitOptions.None)`,
   mesmo resultado). Validado reconstruindo e testando o standalone (`dotnet test` 281/281, sem
   alteração de contagem, inclusive `ProcessingPreviewServiceTests.cs` que já existia lá).
2. **`ProcessingPreviewWindow`** (novo) — porta de `UI/Views/ProcessingPreviewDialog.cs` com o
   tema PX em vez do tema do ListForge: seções Resumo/Tamanhos/Tipos de peça/Saída/Avisos/
   Detalhes dos problemas, botões "Processar agora"/"Voltar". Só leitura (sem campo editável) —
   não precisa do `ThreadKeyboardRouter`. Diferenças honestas em relação ao standalone: sem
   "Perfil de trabalho" (perfis de trabalho não existem no integrado — decisão de produto
   pendente, grupo "Configurações") e "Pasta"/"Arquivo" da seção Saída mostram
   "escolhida/escolhido ao salvar" em vez de um caminho real (o integrado sempre abre um diálogo
   de salvar, nunca usa pasta/nome padrão — mesma limitação já registrada antes).
3. **`ClassicWorkspaceWindow.OnProcessWithPreview`** (novo) — mesma orquestração de
   `AnalyzeAndProcessWithPreview` do standalone: `Analyze()` (sem consumir crédito) → se
   `ValidationFailed`, popula a lista de avisos/erros (reaproveitando a navegação até a linha já
   existente) e limpa essa lista caso contrário (equivalente a `ClearValidationHighlights()`) →
   mostra `ProcessingPreviewWindow` → só chama `ExecuteConfirmed()` (que processa de fato,
   consumindo crédito) se confirmado; senão, status "Processamento aguardando confirmação." e
   nada muda. `BuildProcessingRequest(bool consumeTrialCredit)` extraído para ser compartilhado
   entre este fluxo e o antigo `OnProcess` (sem duplicar a montagem de separador/capitalização/
   ordenação/mapeamento avançado entre os dois).
4. **Botão "Processar rápido (sem prévia)"** — o antigo único botão "Processar" (`OnProcess`,
   inalterado em lógica) agora tem rótulo e estilo distintos do novo botão primário "Processar"
   (com prévia), equivalente à distinção `ProcessCommand`/`ProcessQuickCommand` do standalone.

**Regressão automatizada acrescentada**: `ProcessingPreview_MatchesListForgeStandaloneFixture_ValidInput`,
`_InvalidInput` e `ProcessingPreview_ExecuteConfirmed_ConsumesCreditOnlyAfterConfirmation`
(reproduzem literalmente as entradas de `ListForge.Tests/ProcessingPreviewServiceTests.cs`) em
`tests/PXToolBox.Tests/ListForgeIntegratedTests.cs`.

**Build e testes desta iteração**: `dotnet build` — 0 avisos/erros nos dois repositórios;
`dotnet test` do PXToolBox — 55/55 (net8.0) + 134/134 (net48, 134 = 131 anteriores + 3 novos);
`dotnet test` do ListForge standalone — 281/281, sem alteração de contagem após a extração do
`ProcessingPreviewService`. Nenhum smoke test real no Corel foi feito para este fluxo — ver
`docs/mvp-smoke-test.md`, seção "4c".

**Demonstração reproduzível** (ver pedido da tarefa — comparação entrada/opções/resultado):
entrada `"ANA,10,G\nBIA,11,M"`, opções padrão (separador `,`, capitalização original, ordenação
original, lista avançada desativada). No standalone (`ProcessingPreviewServiceTests.Analyze_ValidInputBuildsImpactWithoutConsumingCredit`):
2 registros totais, 2 válidos, 0 avisos, 0 inválidos, 2 unidades somadas em Tamanhos, "Manga
Curta: 2" em Tipos de peça. No integrado, a mesma entrada/opções produz exatamente os mesmos
valores — não por comparação manual de tela, mas porque, após a extração desta rodada, as duas
interfaces chamam literalmente o mesmo método compilado
(`ListForge.Core.Services.ProcessingPreviewService.Analyze`, `ListForge.Core.dll`); divergir
exigiria as interfaces montarem `ProcessingPreviewSnapshot`/`ProcessingWorkflowRequest`
diferentes para a mesma entrada, o que os testes acima também cobrem. Bloqueio de resultado
desatualizado: inalterado, `ApplyResult` (chamado por `OnProcessWithPreview` após confirmação,
igual a `OnProcess`) continua marcando `_freshness.MarkProcessed()` só em caso de sucesso — editar
a entrada ou as opções entre abrir a prévia e confirmar não é impedido pela UI (a prévia já
reflete o estado no momento em que "Processar" foi clicado), mas o resultado final só é aplicado
com `ExecuteConfirmed`, e qualquer edição posterior ainda aciona o aviso de desatualização antes
de copiar/salvar/gerar — mesma garantia de sempre.

**Pendente, não implementado nesta rodada**: Comparar entrada/saída, Salvar avançado, e todo o
grupo de Configurações — ver `docs/listforge-classic-parity.md` para a ordem e o motivo de cada
um. Enquanto essas lacunas existirem, o modo continua identificado como "Clássico — em
desenvolvimento".

### Correção crítica: CorelDRAW inteiro fechava ao abrir o ListForge Integrado (iteração 8)

O usuário relatou, logo após instalar `0.1.0-dev.14`, que o CorelDRAW fechava (não a janela do
ListForge — o processo `CorelDRW.exe` inteiro) ao clicar "Abrir ListForge Integrado", de forma
100% reprodutível, e que nunca tinha visto isso antes. Investigação, nesta ordem:

1. **Evidência, não suposição**: `Get-WinEvent -LogName Application` confirmou um `APPCRASH` real
   em `CorelDRW.exe` (não uma exceção .NET visível) às 10:10:52, exceção `0xc0000005` (acesso
   inválido) dentro de `VGCore.dll` — a biblioteca nativa do próprio Corel. O timestamp do
   `PXToolBoxDev.dll` instalado (`...\Programs64\Addons\PXToolBoxDev\`) batia exatamente com o
   `host-publish` de `0.1.0-dev.14`, confirmando que era a build recém-entregue.
2. Esse mesmo bucket de crash (mesmo offset em `VGCore.dll`) já existia no histórico do Windows
   desde 25/09/2026 — o que a princípio sugeria instabilidade genérica do Corel, não ligada a
   este add-on. Perguntei ao usuário antes de concluir qualquer coisa: confirmou que reproduz
   **sempre** ao abrir o ListForge Integrado e que **nunca** tinha visto esse crash fazendo outra
   coisa — ou seja, o bucket antigo é só a mesma classe de bug nativo achando uma forma nova e
   determinística de disparar, não uma coincidência.
3. **Causa real, confirmada por leitura de código e depois por reprodução controlada**:
   `ClassicWorkspaceWindow.xaml` tem exatamente um elemento com valor inicial E manipulador da
   mesma propriedade — `TxtSeparator Text="," TextChanged="OnProcessingOptionChanged"`. Esse
   valor inicial dispara `TextChanged` **durante a própria construção da árvore de elementos**
   do `InitializeComponent()` (confirmado por `WpfXamlLoader.LoadBaml` aparecer no topo da pilha
   — ver abaixo), muito antes de `OutputStaleWarning` (declarado no fim do XAML, dentro do painel
   de Saída, acrescentado na rodada de iteração 5) ser conectado ao campo. `OnProcessingOptionChanged`
   → `MarkInputStale()` → `UpdateOutputStaleWarning()` acessava `OutputStaleWarning.Visibility`
   enquanto esse campo ainda era `null` → `NullReferenceException` não capturada por nada no
   caminho, escapando do construtor que o host "wpfhost" do Corel invoca por reflexão na própria
   thread de UI nativa — explicando por que o resultado observado foi o processo inteiro do
   CorelDRAW encerrando (não um diálogo de exceção do .NET): uma exceção gerenciada não tratada
   atravessando a fronteira nativa/gerenciada nesse ponto de hospedagem corrompe o estado nativo
   em vez de ser reportada normalmente. `RefreshAdvancedSlots()` (iteração 6) já tinha a mesma
   guarda, pelo mesmo motivo — faltava em `UpdateOutputStaleWarning()`.
4. **Por que isso nunca apareceu antes**: `OutputStaleWarning` foi acrescentado na iteração 5
   (build `0.1.0-dev.12`). Da iteração 5 até a iteração 7 (`0.1.0-dev.12` a `0.1.0-dev.14`),
   **nenhuma dessas builds tinha sido aberta de verdade dentro do CorelDRAW** — só build e teste
   automatizado foram executados antes da entrega (ver `docs/mvp-smoke-test.md`, que já registrava
   "pendente de smoke test real" para essas três rodadas). Esta foi a primeira vez que alguém
   realmente clicou "Abrir ListForge Integrado" desde que o bug foi introduzido.
5. **Correção**: guarda `if (OutputStaleWarning == null) return;` no início de
   `UpdateOutputStaleWarning()`, mesmo padrão já usado em `RefreshAdvancedSlots()`. Antes de
   aplicar, uma auditoria em todo `ClassicWorkspaceWindow.xaml` (grep por `Text=`/`IsChecked=`/
   `SelectedIndex=`/`ItemsSource=` com valor literal) confirmou que `TxtSeparator` era o único
   elemento com essa combinação perigosa (valor inicial + handler da mesma propriedade) — e os
   outros três XAML novos desta tarefa (`LinkImportDialog`, `ProcessingPreviewWindow`,
   `KeyboardDiagMiniWindow`) não têm nenhuma ocorrência do mesmo padrão.

**Validação de que a correção é real, não só plausível**: `ClassicWorkspaceWindow_ConstructsWithoutThrowing`
(novo teste, `tests/PXToolBox.Tests/ListForgeIntegratedTests.cs`) constrói a janela real numa
thread STA dedicada (WPF exige STA para objetos de UI; esta classe não depende de documento aberto
no CorelDRAW nem de COM) e falha se o construtor lançar qualquer exceção. Reproduzido
deliberadamente **antes** de aplicar a correção (comentando a guarda) — o teste falhou com a pilha
exata: `WpfXamlLoader.LoadBaml` → `ClassicWorkspaceWindow..ctor()` → `TextBox.OnTextPropertyChanged`
→ `RaiseEventImpl`, confirmando a causa por reprodução controlada, não só por leitura de código.
Com a guarda restaurada, o mesmo teste passa. `dotnet test` — 55/55 (net8.0) + **135/135** (net48,
+1 deste teste).

**Confirmado pelo usuário dentro do CorelDRAW real** (`0.1.0-dev.15`): a janela abre sem o Corel
fechar/crashar. Mecanismo tratado como corrigido a partir daqui.

### Paridade funcional com o standalone — 4ª rodada: Comparar entrada/saída (iteração 9)

Grupo escolhido por ser o próximo que impedia um fluxo completo do standalone no integrado:
"Comparar entrada/saída" é a única forma de revisão semântica entre o que foi digitado e o que
foi organizado (confirma que nenhum registro foi perdido/adicionado, distingue transformação
esperada de alteração indevida) — até esta rodada, totalmente ausente do integrado.

1. **`ListComparisonService` + modelos movidos para o núcleo compartilhado** —
   `Services/ListComparisonService.cs` (596 linhas) e `Models/ListComparison.cs`
   (standalone-only) movidos para `ListForge.Core/Services/` e `ListForge.Core/Models/`.
   Dependências reais encontradas (mais do que o registrado anteriormente no inventário, que
   citava só 1 chamada a `AppLogger` — a leitura completa encontrou mais, todas resolvidas sem
   mudar o resultado observável):
   - `AppLogger.Info` (1 chamada, telemetria) — removida.
   - `ArgumentNullException.ThrowIfNull` (.NET 6+) — `if (snapshot == null) throw new ArgumentNullException(...)`.
   - `line.Split(separator)` com `separator` do tipo `string` — mesma lacuna já corrigida em
     `ProcessingPreviewService` (iteração 7) — `Split(new[] { separator }, StringSplitOptions.None)`.
   - `IReadOnlyDictionary<string,string>.GetValueOrDefault(key, default)` — não existe em
     netstandard2.0 (só a partir de netstandard2.1); tentei estender o polyfill já existente
     (`CompatibilityPolyfills.cs`, criado numa reestruturação anterior deste repositório do
     ListForge) para `IReadOnlyDictionary`, mas isso tornou **ambíguas** todas as chamadas
     existentes em `Dictionary<TKey,TValue>` (que implementa as duas interfaces) — revertido;
     corrigido no lugar, com `TryGetValue` explícito nos 4 pontos de chamada.
   - `StringBuilder`/`NormalizationForm`/`Regex` sem `using` explícito — o arquivo original
     compilava via `GlobalUsings.cs` do projeto principal do standalone (`using System.Text;` /
     `using System.Text.RegularExpressions;` globais), que `ListForge.Core` não compila —
     `using`s explícitos acrescentados (mesmo padrão já usado em `TextSearchHelper.cs` e
     `LinkListImportService.cs`).
   Validado reconstruindo e testando o standalone (`dotnet test` 281/281, sem alteração de
   contagem, inclusive `ListComparisonServiceTests.cs`/`ComparisonViewModelTests.cs` que já
   existiam lá).
2. **`ComparisonWindow`** (novo) — porta de `UI/Views/ComparisonWindow.xaml(.cs)` +
   `ViewModels/ComparisonViewModel.cs` com o tema PX, sem MVVM (code-behind direto, consistente
   com o resto deste host): mesmas métricas de resumo, filtro por categoria, navegação entre
   diferenças (anterior/próxima), painéis de entrada/saída, grade de diferenças detectadas,
   detalhamento por campo e "Copiar relatório". Simplificações deliberadas: sem o controle
   customizado `LineNumberedTextBox` (gutter de números de linha) nem o layout responsivo
   largo/compacto do standalone — dois `TextBox` de só leitura com `Select()`/`ScrollToLine()`
   ao selecionar um item (mesmo mecanismo já usado por `ClassicWorkspaceWindow.OnIssueSelected`).
   Só leitura — não precisa do `ThreadKeyboardRouter`.
3. **`ClassicWorkspaceWindow.CaptureComparisonSnapshot`/`OnCompareInputOutput`** (novos) — mesma
   orquestração do standalone: `CaptureComparisonSnapshot` captura entrada/opções/resultado a
   cada processamento com sucesso (dentro de `ApplyResult`, chamado tanto por `OnProcess` quanto
   por `OnProcessWithPreview`); "Comparar entrada/saída" reutiliza o **mesmo** guard de
   desatualização já existente (`_freshness.IsStale`, o mesmo do bloqueio de resultado
   desatualizado da iteração 5) em vez de duplicar a lógica própria do standalone
   (`IsComparisonSnapshotCurrent`, que compara texto literal) — equivalente funcional, não a
   mesma implementação, mesmo padrão já aceito nesta tarefa para o bloqueio de resultado
   desatualizado.

**Auditoria do padrão de crash corrigido na iteração 8** (XAML com valor inicial + handler da
mesma propriedade, disparando durante `InitializeComponent()` antes de elementos posteriores
serem conectados): nenhuma ocorrência em `ComparisonWindow.xaml` — confirmado por grep antes de
compilar, e por um novo teste (`ComparisonWindow_ConstructsWithoutThrowing`) que constrói a
janela real numa thread STA dedicada com um resultado real do serviço.

**Regressão automatizada acrescentada**: 8 testes novos em
`tests/PXToolBox.Tests/ListForgeIntegratedTests.cs` — 5 reproduzem literalmente entradas de
`ListForge.Tests/ListComparisonServiceTests.cs` (idêntico sem diferenças críticas, reordenado,
capitalização configurada, duplicidade legítima, remoção/adição manual), 2 cobrem casos
explicitamente citados na tarefa e ainda não exercitados por nenhuma fixture já reproduzida
(zeros à esquerda no campo Número, campos opcionais vazios), e 1 constrói a janela real
(`ComparisonWindow_ConstructsWithoutThrowing`).

**Build e testes desta iteração**: `dotnet build` — 0 avisos/erros nos dois repositórios;
`dotnet test` do PXToolBox — 55/55 (net8.0) + **143/143** (net48, 143 = 135 anteriores + 8
novos); `dotnet test` do ListForge standalone — 281/281, sem alteração de contagem após a
extração do `ListComparisonService`. Nenhum smoke test real no Corel foi feito para esta função
— ver `docs/mvp-smoke-test.md`, seção "4c".

**Pendente, não implementado nesta rodada**: Salvar avançado, e todo o grupo de Configurações —
ver `docs/listforge-classic-parity.md` para a ordem e o motivo de cada um.

### Paridade funcional com o standalone — 5ª rodada: Salvar avançado (iteração 10)

Base: `0.1.0-dev.16`, com o crash de abertura (iteração 8) já confirmado corrigido. Preservados
sem alteração: teclado, comparação entrada/saída, processamento/exportações existentes.

1. **`AdvancedSaveService` movido para o núcleo compartilhado** — `Services/AdvancedSaveService.cs`
   (standalone-only) movido para `ListForge.Core/Services/`. Dependências reais: 5 chamadas a
   `AppLogger` (telemetria, sem efeito no resultado — removidas); `UTF8Encoding`/`Path`/`File`/
   `Directory`/`ZipArchive` sem `using` explícito (mesma lacuna de `GlobalUsings.cs` já corrigida
   em rodadas anteriores). Validado reconstruindo e testando o standalone (`dotnet test`
   282/282 — 281 anteriores + 1 novo, ver item 2 abaixo).
2. **Reforço aditivo, sem mudar formato/nomes de arquivo nem o comportamento já testado pelo
   standalone**: pedido explícito desta tarefa — "identificação de arquivos concluídos e
   pendentes se houver gravação de vários arquivos e falha parcial". No modo arquivos soltos (3
   gravações sequenciais: entrada, saída, JSON), uma falha no meio agora identifica exatamente
   quais arquivos já foram gravados e quais ficaram pendentes, em vez de uma mensagem genérica.
   No modo ZIP, uma falha no meio da gravação agora apaga o `.zip` parcial/corrompido em vez de
   deixá-lo incompleto no disco. Testado com uma simulação determinística (um dos três arquivos
   pré-existe como diretório — `File.Exists` não o detecta como colisão de nome, então
   `ResolveAvailableBaseName` mantém o nome original e a escrita falha exatamente nesse arquivo):
   `Save_LooseFiles_PartialFailure_ReportsCompletedAndPendingFiles`, novo em
   `ListForge.Tests/AdvancedSaveServiceTests.cs` (roda nos dois produtos, por estar no núcleo).
3. **`AdvancedSaveDialog`** (novo) — pede nome base (campo editável — usa `ThreadKeyboardRouter`,
   mesmo mecanismo de `LinkImportDialog`) e modo (arquivos soltos/ZIP, `RadioButton`); "Escolher
   pasta..." abre `System.Windows.Forms.FolderBrowserDialog` (referência nova no
   `PXToolBox.CorelHost.Dev.csproj` — net48/WPF não tem equivalente próprio;
   `Microsoft.Win32.OpenFolderDialog` só existe a partir do .NET 8 WPF; diálogo COM nativo com
   loop modal próprio, mesma categoria de `Microsoft.Win32.SaveFileDialog` já usado em
   `TryPickExportPath`, sem precisar de `WindowsFormsIntegration`). Cancelar o diálogo não grava
   nada. Auditado contra o padrão de crash da iteração 8 (nenhum elemento com valor inicial +
   handler da mesma propriedade) e coberto por `AdvancedSaveDialog_ConstructsWithoutThrowing`
   (constrói a janela real numa thread STA).
4. **`ClassicWorkspaceWindow.OnAdvancedSave`** (novo) — mesmos dois portões do standalone antes
   de abrir o diálogo ("Ative Lista avançada antes de usar o Salvar avançado."/"Processe a lista
   antes de usar o Salvar avançado.") e o mesmo `ConfirmIfStale` já usado por Copiar/Salvar/Gerar
   JSON/Comparar (bloqueio de resultado desatualizado). **Nunca sobrescreve silenciosamente**:
   reproduz o mecanismo real do standalone (`ResolveAvailableBaseName`, incremento automático
   `_v2`/`_v3`... em caso de colisão de nome) em vez de inventar uma confirmação de sobrescrita
   que não existe na referência — o nome final (já com sufixo, se houver) aparece na mensagem de
   sucesso, então a troca nunca é silenciosa. Sucesso só é informado depois que `Save()` (rodado
   em `Task.Run`) de fato termina. A segunda verificação do standalone ("Ative os recursos de
   JSON...") não se aplica aqui — a aba/geração de JSON do integrado nunca fica escondida.
   Escolha de modo (soltos/ZIP) fica só em memória nesta sessão da janela (`_advancedSaveMode`) —
   sem um local próprio de configuração de usuário no integrado ainda, diferente do standalone
   (que persiste via `ConfigManager`); limitação documentada, não alteração de armazenamento
   global de configurações (fora do escopo desta tarefa, por instrução explícita).

**Regressão automatizada acrescentada**: `AdvancedSave_LooseFiles_MatchesListForgeStandaloneFixture`,
`AdvancedSave_Zip_ContainsOnlyExpectedEntries` e `AdvancedSaveDialog_ConstructsWithoutThrowing`
em `tests/PXToolBox.Tests/ListForgeIntegratedTests.cs`; `Save_LooseFiles_PartialFailure_ReportsCompletedAndPendingFiles`
em `ListForge.Tests/AdvancedSaveServiceTests.cs` (standalone).

**Build e testes desta iteração**: `dotnet build` — 0 avisos/erros nos dois repositórios;
`dotnet test` do PXToolBox — 55/55 (net8.0) + **146/146** (net48, 146 = 143 anteriores + 3
novos); `dotnet test` do ListForge standalone — 282/282 (281 anteriores + 1 novo), sem alteração
de comportamento nos casos já cobertos. Nenhum smoke test real no Corel foi feito para esta
função — ver `docs/mvp-smoke-test.md`, seção "4c".

**Pendente, não implementado nesta rodada**: todo o grupo de Configurações (perfis de trabalho,
exportar/importar config, pacote de suporte, pasta de config/log, tamanhos customizados) —
bloqueado numa decisão de produto ainda não tomada. Edição manual da saída/JSON permanece fora
por não ter sido pedida explicitamente.

### Correção fora de ListForge Integrado: duplicata em "Somar Altura" (Medidas, iteração 11)

Pedido do usuário, intercalado com a rodada de Salvar avançado (sem relação com ListForge): "Em
medidas, todos os tipos de soma tem que ser em metros, nesse caso o Somar altura está
duplicado". Confirmado por leitura de `MeasurementSumService`: `SumHeightOnly` (botão "Somar
Altura (m)") e `SumSelected(mode: Height)` (botão "Somar Altura") sempre convertiam para metros,
qualquer unidade do documento (`DocumentUnitConversion.ToMetersFactor`) — resultado idêntico nos
dois, inclusive o texto copiado para a área de transferência. Isso já tinha sido confirmado por
uma correção anterior (2026-10, ver `docs/mvp-smoke-test.md`) que corrigiu a suposição de que o
atalho só-altura aceitava só mm/cm/m — na época sem notar que a correção também provava que os
dois botões eram equivalentes.

**Correção**: removido o botão "Somar Altura (m)" e `MeasurementSumService.SumHeightOnly`
(sem outros chamadores fora do próprio handler e dos testes). Tooltips de "Somar Altura"/"Somar
Largura"/"Somar Altura e Largura" corrigidos — diziam "na unidade do documento", mas o código
sempre converteu para metros; agora dizem "em metros", refletindo o comportamento real.
Cabeçalho da seção "Medidas" atualizado para deixar isso explícito. 4 testes de `SumHeightOnly`
substituídos por 3 testes equivalentes em `SumSelected(Height)` (a conversão de polegada/pé/jarda
já tinha cobertura própria em `SumSelected_AcceptsInch`, não precisou duplicar).

**Build e testes**: `dotnet build` — 0 avisos/erros; `dotnet test` — 54/54 (net8.0, antes 55 —
líquido -1: -4 testes de `SumHeightOnly` +3 equivalentes de `SumSelected`) + 145/145 (net48,
mesma variação). Nenhum smoke test real no Corel foi feito — ver `docs/mvp-smoke-test.md`.

### Configurações do ListForge Integrado (iteração 12)

Decisão de armazenamento (bloqueava vários itens do grupo "Configurações" desde o início desta
tarefa): **infraestrutura já existente reaproveitada**, não uma pasta nova — `ListForgeConfigStore`
(caminho do executável do ListForge Standalone) já gravava em
`%LocalAppData%\PXToolBox\Dev\config\`. `ListForgeIntegratedSettingsStore` grava num arquivo
próprio na MESMA pasta: `listforge-integrated-settings.json`, nunca o mesmo arquivo do caminho
do executável, nunca `ConfigManager` do ListForge standalone, nunca nada do PX de produção ou do
MoldMotor.

1. **`ListForgeIntegratedSettings`** (modelo) — esquema versionado (`SchemaVersion`, hoje só a
   versão 1). Dividido em duas metades no mesmo arquivo: preferências portáveis
   (`EditorFontSize`, `AdvancedSaveMode`) e configuração local desta máquina
   (`UseDefaultOutputDir`/`OutputDir`/`UseDefaultListName`/`DefaultListName`). `Normalize()`
   corrige campo a campo (nunca lança) — um valor individual inválido (fonte fora de 8–32, modo
   desconhecido) volta ao padrão SÓ daquele campo, sem descartar o resto de uma configuração por
   outro motivo válida.
2. **`ListForgeIntegratedSettingsStore`** — `Load()` nunca lança: arquivo ausente (primeira
   execução) → padrões, sem aviso (não é erro); arquivo existente mas ilegível (lock de outro
   processo) → padrões nesta sessão, com aviso, SEM tocar no arquivo original (pode não estar
   realmente corrompido); arquivo com JSON inválido ou `SchemaVersion` desconhecida/futura →
   **nunca sobrescrito silenciosamente**: copiado para
   `listforge-integrated-settings.corrompido-<data-hora>.json` na mesma pasta antes de cair nos
   padrões — o switch sobre `SchemaVersion` é o ponto onde uma migração real entraria quando o
   esquema mudar (hoje só existe a versão 1, não há "de onde migrar" ainda). `Save()` grava num
   arquivo temporário na mesma pasta e só então substitui o definitivo (`File.Replace`/`Move`) —
   uma queda de energia no meio da escrita nunca deixa o arquivo final truncado; falha real (ex.:
   pasta sem permissão) propaga a excessão para quem chamou decidir a mensagem.
3. **`SettingsWindow`** (nova) — tema PX, dois campos editáveis (tamanho de fonte, nome padrão
   da lista — usa o mesmo `ThreadKeyboardRouter` das outras janelas). Salvar/Cancelar;
   "Restaurar padrões" só reseta os campos NESTA tela (ainda exige "Salvar" para valer) e pede
   confirmação explícita, nomeando que não afeta ListForge Standalone/PX de produção/MoldMotor.
   "Abrir pasta de configuração", "Exportar configurações..."/"Importar configurações..." (.json
   portátil, reduzido aos campos que o integrado realmente tem — não o `AppConfig`/`SizeConfig`
   completo do standalone).
4. **Aplicação em `ClassicWorkspaceWindow`**: tamanho de fonte aplicado a `InputTextBox`/
   `OutputTextBox`/`JsonTextBox` na abertura e de novo após salvar — puramente cosmético, não
   marca resultado como desatualizado (`_freshness` só reage a entrada/opções de processamento,
   inalterado). `AdvancedSaveMode` agora persiste entre aberturas do PX (resolvendo a limitação
   "só em memória" registrada na rodada anterior). `TryPickExportPath`
   (Salvar saída/Gerar JSON) e `AdvancedSaveDialog` (Salvar avançado) passam a usar
   `UseDefaultOutputDir`/`OutputDir`/`UseDefaultListName`/`DefaultListName` — reproduzindo a
   assimetria real do standalone: `TryPickExportPath` pula o diálogo por completo só quando os
   DOIS padrões estão ativos (só tem um diálogo combinado, diferente dos dois prompts
   independentes do standalone); `AdvancedSaveDialog` só pula a escolha de PASTA, o nome
   continua sempre perguntado (com o padrão como sugestão) — mesma assimetria de
   `ResolveAdvancedSaveName()`/`ResolveAdvancedSaveOutputDir()` no código real do standalone.

**Preferências/decisões aplicáveis vs. exclusivas do standalone** (ver
`docs/listforge-classic-parity.md` para o inventário completo): perfis de trabalho e tamanhos
customizados continuam ausentes — não são mais bloqueados pela falta de um local de
armazenamento (que agora existe), mas exigem cada um sua própria UI de edição (lista de perfis;
grupos de tamanho por gênero/idade), maior que esta rodada. Pacote de suporte e "abrir pasta de
logs" continuam ausentes por dependerem de uma infraestrutura de LOG do integrado que não existe
— diferente da pasta de configuração, resolvida aqui. Licenciamento/Trial não foi alterado, por
instrução explícita desta tarefa.

**Regressão automatizada acrescentada**: `ListForgeIntegratedSettingsStoreTests.cs` (novo) — 8
testes (carregamento sem arquivo, round-trip completo, nenhum arquivo temporário deixado para
trás, JSON corrompido colocado em quarentena sem sobrescrever, esquema desconhecido colocado em
quarentena, validação de fonte fora da faixa, modo de salvamento desconhecido corrigido,
construção real de `SettingsWindow` numa thread STA — mesma classe de regressão do crash da
iteração 8). Export/Import não têm teste automatizado dedicado (interação de diálogo de arquivo
não é testável sem automação de UI); a serialização em si já é coberta pelo teste de round-trip.

**Build e testes**: `dotnet build` — 0 avisos/erros; `dotnet test` do PXToolBox — 54/54 (net8.0)
+ **153/153** (net48, 153 = 145 anteriores + 8 novos). ListForge standalone não foi tocado nesta
rodada (282/282, confirmado sem alteração). Nenhum smoke test real no Corel foi feito.

### Perfis de trabalho do ListForge Integrado (iteração 13)

Diferente da rodada anterior (só armazenamento), esta função já existe por completo no
standalone (`Services/WorkProfileService.cs`, 282 linhas) — por isso a regra do pedido ("use o
núcleo compartilhado como fonte das regras, nunca uma versão simplificada") se aplica
diretamente: **`WorkProfileService` e os modelos `WorkProfile`/`WorkProfileSettings` foram
movidos de `C:\Projetos\ListForge\Services`/`Models` para `ListForge.Core\Services`/`Models`**
nesta rodada — mesmo padrão já usado para `ListComparisonService`/`ProcessingPreviewService`/
`AdvancedSaveService`/`JsonPieceMappingService`/`LinkListImportService` em rodadas anteriores.
Namespaces preservados (`ListForge.Services`/`ListForge.Models`) — só o arquivo físico mudou de
projeto.

**Problema de dependência circular e a solução (`IWorkProfileConfig`)**: todos os métodos de CRUD
de `WorkProfileService` recebiam um `AppConfig` (tipo do projeto standalone, que *referencia*
`ListForge.Core` — não o contrário). `ListForge.Core` não pode conhecer `AppConfig`. Extraída uma
interface mínima, `ListForge.Core.IWorkProfileConfig` (`WorkProfilesSchemaVersion`/
`ActiveWorkProfileId`/`List<WorkProfile> WorkProfiles` — exatamente os 3 membros que o serviço
realmente usa), e `AppConfig` passou a implementá-la (`: ListForge.Core.IWorkProfileConfig`, sem
nenhuma outra mudança — já tinha os 3 membros com os nomes certos). `EnsureProfiles`/
`GetActiveProfile`/`CreateProfile`/`RenameProfile`/`DuplicateProfile`/`DeleteProfile`/
`SaveActiveProfile`/`RestoreDefaultProfile`/`HasUnsavedChanges` passaram a receber
`IWorkProfileConfig` em vez de `AppConfig`; `CaptureFromConfig(AppConfig, ...)` (só usado
internamente pelo standalone para montar o perfil Padrão a partir de um `AppConfig` antigo sem
nenhum perfil ainda) foi removido do núcleo compartilhado e recriado como método privado dentro
de `MainViewModel.cs` do standalone — não pode viver em `ListForge.Core` pelo mesmo motivo de
`AppConfig`. `NormalizeSettings` tornado `public static` (função pura, sem efeito colateral) para
o standalone reusar na captura. Zero mudança de comportamento no standalone: 282/282 testes dele
continuam passando sem alteração.

**`ListForgeIntegratedSettings` implementa a mesma interface** (esquema **2** nesta rodada): os
campos que antes eram únicos no topo (`AdvancedSaveMode`/`UseDefaultOutputDir`/`OutputDir`/
`UseDefaultListName`/`DefaultListName`) **deixaram de existir ali e passaram a viver dentro de
cada `WorkProfile.Settings`** — exatamente como no standalone (`AppConfig.WorkProfiles`/
`ActiveWorkProfileId`). Só `EditorFontSize` permanece fora de qualquer perfil (cosmético, igual
ao standalone).

**Migração explícita do esquema 1 para o 2** (`ListForgeIntegratedSettingsStore.
MigrateFromSchemaVersion1`): um arquivo salvo pela build anterior tem `SchemaVersion: 1` e os
campos antigos no topo. `Load()` lê o JSON bruto (`JObject`, não desserializa direto para o tipo
atual — que não tem mais esses campos, perderia os valores silenciosamente), monta um perfil
"Padrão" com esses valores preservados via `WorkProfileService.EnsureProfiles`, e devolve com um
aviso explícito ("Configurações migradas... preservados no perfil Padrão"). O arquivo original
NUNCA é sobrescrito pela migração em si — só quando `Save()` for chamado depois (mesma regra de
"nunca sobrescrever silenciosamente" de qualquer outro caminho problemático de carregamento).

**Onde cada controle vive**: o combo "Perfil de trabalho" e os botões Novo/Salvar perfil/
Descartar alterações/Renomear/Duplicar/Excluir/Restaurar perfil Padrão ficam em
`ClassicWorkspaceWindow` (não em `SettingsWindow`) — é ali que estão `TxtSeparator`/`CmbCaseMode`/
`CmbSortMode`/`ChkAdvancedList`/`AdvancedSlotsList`, que compõem a maior parte de um perfil.
`SettingsWindow` continua só com tamanho de fonte (fora de qualquer perfil) e o formato do Salvar
avançado/pasta/nome padrão **do perfil ATIVO** — `TryBuildSettingsFromControls` agora clona
`_workingSettings` (preservando `WorkProfiles`/`ActiveWorkProfileId` inteiros) e só sobrescreve
esses campos dentro do perfil ativo, nunca a lista de perfis. "Importar configurações" é a
exceção: troca a lista de perfis inteira (igual ao `SettingsExportService` do standalone, que
também importa `WorkProfiles`/`ActiveWorkProfileId` por completo).

**Mesmas regras do standalone, confirmadas lendo `WorkProfileService`/`MainViewModel.cs` reais**:
perfil "Padrão" nunca pode ser renomeado nem excluído; nomes duplicados (sem diferenciar
maiúsculas/minúsculas) são rejeitados; duplicar cria uma cópia independente ("Nome - Cópia",
incrementando se colidir); excluir pede confirmação; excluir o perfil ATIVO cai para o Padrão;
trocar de perfil com alterações não salvas pergunta Sim/Não/Cancelar (salvar/descartar/continuar
no perfil atual) — mesmo texto do standalone; trocar de perfil nunca apaga a lista digitada em
`InputTextBox`, só os controles de OPÇÃO; opções que afetam o processamento (separador/
capitalização/ordenação/lista avançada, inclusive por slot) invalidam o resultado atual
(`_freshness`), igual a qualquer outra opção de processamento já coberta por essa regra — inclui
uma correção de uma lacuna pré-existente: `OnAdvancedListToggled` não chamava `MarkInputStale()`
antes desta rodada.

**Novo diálogo, mesma disciplina de crash**: `WorkProfileNameDialog` substitui
`ListForge.UI.Views.InputDialog` do standalone (`Window` do projeto standalone, fora do núcleo
compartilhado) para "Novo..."/"Renomear..." — campo de nome editável, mesmo `ThreadKeyboardRouter`
das demais janelas, com teste de construção numa thread STA dedicada.

**Guarda de crash adicional identificada nesta rodada**: `MarkWorkProfileChanged` (chamado por
`OnProcessingOptionChanged`, que `TxtSeparator.Text=","` no XAML já dispara durante o próprio
`InitializeComponent()` — mesma causa raiz documentada na iteração 8) acessava `ChkAdvancedList`/
`CmbWorkProfile`, ainda não conectados nesse momento. Corrigido com a mesma guarda defensiva
(`if (... == null) return;`) já usada por `RefreshAdvancedSlots`/`UpdateOutputStaleWarning`.

**Build e testes**: `dotnet build` — 0 avisos/erros; `dotnet test` do PXToolBox — 54/54 (net8.0) +
**168/168** (net48, 168 = 164 anteriores + 4 novos: `Load_SchemaVersion1_
MigratesLegacyFieldsIntoDefaultProfile`, `WorkProfiles_CreateRenameDuplicateDelete_
FollowSameRulesAsStandalone`, `Normalize_WithNoWorkProfiles_CreatesDefaultProfile`,
`WorkProfileNameDialog_ConstructsWithoutThrowing` — `SaveThenLoad_RoundTripsAllFields` foi
substituído 1:1 por `SaveThenLoad_RoundTripsWorkProfiles`). `dotnet test` do standalone —
282/282, confirmado sem alteração de comportamento. Nenhum smoke test real no Corel foi feito —
criar/selecionar/renomear/duplicar/excluir perfis de verdade, fechar e reabrir o PX confirmando
persistência, continua pendente (ver `docs/mvp-smoke-test.md`).

### Auditoria de status (versão 0.1.0-dev.21)

Rodada só de verificação, sem implementação — confirmou Perfis de trabalho (seção acima) no
código real e encontrou duas divergências na contagem de `docs/listforge-classic-parity.md`
(nunca verificada linha a linha desde o levantamento original): "Exclusivo do standalone"
citava 4 quando a tabela só tem 3 linhas com esse estado; e a linha "Prévia com contagem de
inválidos/avisos..." (grupo "Validação e correção") dizia "Ausente" desde o início, apesar de
ser o mesmo item de "Processar com prévia de confirmação" (grupo "Modos e opções de
processamento"), marcado Completo desde a 3ª rodada de implementação. Ambas corrigidas só na
documentação — nenhuma mudança de código. Contagem corrigida: Completo 27 · Parcial 5 · Ausente
6 · Exclusivo do standalone 3 (total 41).

### Tamanhos customizados do ListForge Integrado (iteração 14)

Confirmado nesta rodada: `SizeConfig`/`SizeGroupConfig` (`ListForge.Models`) e todo o
`SizeHelper` (`ListForge.Core`) — normalização, construção dos tamanhos de cada grupo
(`BuildGroupSizes`/`BuildSizeIndex`), reconhecimento/validação (`ParseQtyAndSize`/`IsValidSize`),
resumo (`BuildSizeSummary`) e a função de edição (`UpdateGroupConfig`) — **já estavam no núcleo
compartilhado** desde uma rodada anterior (nenhuma movimentação de arquivo foi necessária nesta
rodada, diferente de Perfis de trabalho). Faltava só a tela de edição no PX e desprender
`ClassicWorkspaceWindow._sizeConfig` de `SizeConfig.Default()` fixo.

**Relação com perfis de trabalho — confirmada lendo `ConfigManager`/`MainViewModel.cs` reais**:
tamanhos são **independentes de perfil**. No standalone, `sizes.json`
(`ConfigManager.SizeConfigPath`) é um arquivo separado de `config.json` (que guarda `AppConfig`
e `WorkProfiles`) — `_sizeCfg = ConfigManager.LoadSizeConfig()` e `EnsureWorkProfiles(...)` são
chamados em sequência na inicialização, sem nenhuma referência cruzada entre os dois. Por isso
`ListForgeIntegratedSettings.Sizes` foi adicionado como campo de **nível de módulo** (fora de
`WorkProfile`/`WorkProfileSettings`) — vale para todos os perfis igualmente, nunca duplicado por
perfil, exatamente a mesma relação (ou melhor, ausência de relação) do standalone.

**Esquema**: campo aditivo — `ListForgeIntegratedSettings.CurrentSchemaVersion` permanece **2**
(não precisou virar 3). Um arquivo salvo antes desta rodada não tem a chave `"Sizes"` no JSON;
como `Sizes` tem um inicializador (`SizeConfig.Default()`) e Newtonsoft simplesmente mantém
valores de propriedades ausentes no JSON, a desserialização já produz tamanhos oficiais sem
nenhum código de migração — diferente da reestruturação de Perfis de trabalho (iteração 13), que
moveu/removeu campos existentes e por isso exigiu uma migração explícita.

**`SizeConfigWindow`** (nova) — 4 `GroupBox` fixos (`SizeHelper.EditableGroupOrder`: masculino/
feminino/infantil/meião), cada um com 3 campos CSV (tamanhos-base/prefixos/sufixos, via
`SizeHelper.ParseCsvTokens`/`TokensToCsv` — mesmo formato textual do standalone), um resumo
somente leitura (`SizeHelper.BuildSizeSummary`) e "Restaurar tamanhos padrão" (confirmação
explícita, escopo só os tamanhos — nunca afeta perfis/fonte/Salvar avançado). Mesma validação do
standalone (`MainViewModel.BuildSizeConfigFromUI`): cada grupo exige ao menos um tamanho-base,
ou a tela recusa salvar com a mesma mensagem ("Informe ao menos um tamanho-base para {grupo}."),
preservando o que já foi digitado nos outros campos. Grava direto no disco ao "Salvar" (mesmo
padrão de `SettingsWindow`) — `ClassicWorkspaceWindow.OnOpenSizeConfig` aplica o resultado
(`_sizeConfig = _settings.Sizes`) e invalida o resultado atual (`MarkInputStale`) + redimensiona
a lista avançada (`RefreshAdvancedSlots`), já que tamanhos afetam reconhecimento/validação/
ordenação/exportação.

**Prova de que a configuração realmente controla o processamento** (não só uma tela que salva
valores soltos): `ProcessingWorkflowService.Execute` já recebia `SizeConfig` como parâmetro
direto de `ValidateText`/`ProcessText`/`BuildOutput`/`BuildOrdersFromOrderlist` — ou seja, uma
vez que `_sizeConfig` deixou de ser fixo, a conexão com reconhecimento/validação/exportação já
existia por construção (mesmo caminho usado pelos dois produtos). Um teste novo
(`CustomSizeConfig_RecognizesAddedSize_AndRejectsRemovedSizeWithoutSilentConversion`) prova isso
de ponta a ponta: adicionar "GIGANTE" ao grupo masculino faz esse tamanho ser reconhecido e
processado; remover "XG" faz esse mesmo tamanho (válido na configuração oficial) ser rejeitado
como inválido — nunca convertido silenciosamente para outro tamanho.

**Exportar/Importar configurações**: como `Sizes` é um campo do mesmo objeto
`ListForgeIntegratedSettings` serializado por `SettingsWindow.OnExportSettingsClick/
OnImportSettingsClick`, os tamanhos passam a ser exportados/importados junto com perfis e
demais campos automaticamente — sem código adicional. Confirmado como fiel ao standalone: lá,
`SettingsExportSnapshot(AppConfig Config, SizeConfig Sizes, ...)` também exporta/importa os dois
juntos sob o mesmo "Exportar/Importar configurações".

**Build e testes**: `dotnet build` — 0 avisos/erros; `dotnet test` do PXToolBox — 54/54 (net8.0) +
**174/174** (net48, 174 = 168 anteriores + 6 novos: `CustomSizeConfig_RecognizesAddedSize_
AndRejectsRemovedSizeWithoutSilentConversion` em `ListForgeIntegratedTests.cs`;
`SaveThenLoad_RoundTripsCustomSizes`, `Load_SchemaVersion1File_DefaultsSizesToOfficialValues`,
`Normalize_WithMissingSizeGroup_RestoresOfficialDefaultForThatGroupOnly`,
`SizeConfigWindow_Save_RejectsGroupWithNoBaseSizes`, `SizeConfigWindow_ConstructsWithoutThrowing`
em `ListForgeIntegratedSettingsStoreTests.cs`). `dotnet test` do standalone — 282/282, confirmado
sem alteração (nenhum arquivo do standalone foi tocado nesta rodada — `SizeConfig`/`SizeHelper`
já estavam no núcleo compartilhado). Nenhum smoke test real no Corel foi feito — editar um
grupo, processar, comparar com o standalone usando a mesma configuração, continua pendente (ver
`docs/mvp-smoke-test.md`).

### Backup automático de entrada do ListForge Integrado (iteração 15)

Confirmado lendo `ConfigManager.CreateBackup`/`MainViewModel.SaveInputFile`/`SaveInputAsFile`/
`OpenBackupsFolder` do standalone por completo antes de implementar — mesmas regras, nenhum
formato/nome inventado.

**Assimetria real reproduzida**: `OnSaveInputFile` só cria backup quando o CONTEÚDO em disco é
diferente do que está sendo salvo (lê o arquivo, compara, só faz backup se mudou — evita um
backup inútil ao salvar duas vezes sem alterar nada); `OnSaveInputFileAs` cria backup sempre que
o destino escolhido já existir, **sem** comparar conteúdo (pode ser um arquivo não relacionado
ao atual) — exatamente a mesma distinção do standalone (`if (readResult.Value != InputText)` vs.
`if (File.Exists(dlg.FileName))`, incondicional).

**`Integrations/ListForgeInputBackupService.cs`** (novo) — `CreateBackup(sourceFile)`: nome
`{nome}_{yyyyMMdd_HHmmss}{ext}`, cópia simples via `File.Copy(overwrite: false)` (nunca
sobrescreve um backup existente, nunca reformata o conteúdo), em
`%LocalAppData%\PXToolBox\Dev\ListForge\Backups\` (subpasta pedida explicitamente, distinta da
convenção flat `config\`/`logs\` já usada por outros módulos — nunca o `BackupDir` do
standalone). Lança (nunca engole) se a cópia falhar — quem chama (`OnSaveInputFile(As)`) só
escreve o arquivo novo DEPOIS do backup confirmado; uma falha de backup aborta a operação
inteira, com mensagem explícita ("a entrada NÃO foi salva, o arquivo original não foi tocado"),
nunca sobrescrevendo silenciosamente.

**Retenção**: confirmado lendo `ConfigManager` por completo — o standalone não tem NENHUMA
política de limpeza/retenção para backups (diferente dos logs, que também não têm lá, mas que
esta mesma entrega decidiu dar uma política própria no PX — ver `PxLogger`). Decisão explícita
desta tarefa: preservar o comportamento real da referência — backups do PX também nunca são
removidos automaticamente, acumulam indefinidamente. Nunca usa a retenção de `PxLogger` (dados
diferentes, políticas independentes).

**Reforço aditivo** (não muda nome/formato do standalone): a pasta de backups é plana — dois
arquivos de ORIGEM com o mesmo nome em pastas diferentes (ex.: `C:\A\lista.txt` e
`C:\B\lista.txt`) produziriam o mesmo nome de backup se copiados no mesmo segundo.
`ResolveUniqueBackupPath` (pública, pura, testável sem depender do relógio real) acrescenta um
sufixo numérico só quando essa colisão de fato ocorre.

**UI**: botão "Abrir pasta de backups" no painel Diagnóstico (ao lado de "Abrir pasta de logs"),
`ClassicWorkspaceWindow.OnOpenBackupsFolder`. Nenhuma restauração automática de backup foi
adicionada — confirmado que o standalone também não tem nenhum comando de restauração (só criar/
abrir a pasta), então nada foi inventado aqui.

**Build e testes**: `dotnet build` — 0 avisos/erros; `dotnet test` do PXToolBox — 54/54 (net8.0)
+ **181/181** (net48, 181 = 174 anteriores + 7 novos em
`tests/PXToolBox.Tests/ListForgeInputBackupServiceTests.cs`: cópia exata de conteúdo, excessão
ao faltar o arquivo de origem, propagação de falha de cópia sem engolir silenciosamente,
resolução de colisão de nome pura — sem e com colisão forçada —, backups de arquivos
homônimos em pastas diferentes nunca colidindo, e o caminho da pasta sob
`PXToolBox\Dev\ListForge\Backups`). `dotnet test` do standalone — 282/282, confirmado sem
alteração (nenhum arquivo dele foi tocado). Nenhum smoke test real no Corel foi feito — salvar/
sobrescrever/"Salvar como" de verdade, comparando os backups gerados com o standalone, continua
pendente.

### Conclusão de Diagnóstico e Suporte do ListForge Integrado (iteração 16)

Base: 0.1.0-dev.23. A infraestrutura (`Integrations/PxLogger.cs`, `PxVersionInfo.cs`,
`SupportPackageService.cs`, botões no painel Diagnóstico) já existia de uma rodada anterior
interrompida (ver nota histórica em `docs/listforge-classic-parity.md`), mas sem logging ligado
a falhas técnicas fora de startup/shutdown/backup de entrada, sem testes automatizados e sem a
tabela de paridade atualizada. Esta rodada concluiu exatamente esses três pontos, sem criar uma
segunda raiz de configuração — confirmado no código que logs (`...\Dev\logs\`), config
(`...\Dev\config\`) e backups (`...\Dev\ListForge\Backups\`) já viviam sob a mesma raiz
`%LocalAppData%\PXToolBox\Dev\`.

**Logging de falhas técnicas pertinentes**: `PxLogger.Error` passou a ser chamado também nos
catches de "Processar" (`OnProcess`), "Processar com prévia" (`OnProcessWithPreview`), "Comparar
entrada e saída" (`OnCompareInputOutput`), "Salvar avançado" (`OnAdvancedSave`), "Abrir lista"
(`OnOpenInputFile`) e "Salvar perfis de trabalho" (`SaveSettingsOrShowError`) — além dos já
existentes (startup, shutdown, backup/salvamento de entrada). Sempre com uma mensagem FIXA
descrevendo o que falhou (nunca `ex.Message`) e, quando aplicável, o tipo da excessão + pilha —
nunca o conteúdo de listas, nomes/números de jogadores, área de transferência, credenciais ou
licenças. Uma falha ao gravar o próprio log nunca lança (sempre `catch` silencioso dentro de
`PxLogger.Write`) nem derruba o Corel, e nunca oculta o erro original (o chamador já reporta por
`MessageBox`/status, por outro caminho).

**Retenção de logs nunca afeta backups**: confirmado por teste — `PxLogger.EnforceRetention` só
enumera/remove arquivos que casam com `pxtoolbox-integrated-*.log` dentro da própria pasta de
logs; a pasta de backups (`Integrations/ListForgeInputBackupService.cs`, iteração 15) é uma
pasta diferente, nunca tocada por essa rotina.

**Informações de suporte completas**: `ClassicWorkspaceWindow.UpdateSupportInfoText` já reunia
(de uma rodada anterior) versão do PX Tool Box DEV, versão do núcleo `ListForge.Core`, versão do
CorelDRAW quando disponível, runtime/arquitetura e os caminhos de configuração/logs — confirmado
que "Copiar informações de suporte" (`OnCopySupportInfo`) copia exatamente esse mesmo texto, sem
segredos.

**Pacote de suporte**: `SupportPackageService.Generate` (já existente) gera um `.zip` com
`support-info.txt`, `settings-summary.txt` (resumo campo a campo — nunca nome de perfil/pasta/
lista configurados, mesma cautela do `BuildConfigSummary` real do standalone) e os logs recentes
do módulo. `OnGenerateSupportPackage` informa o conteúdo completo num `MessageBox` Sim/Não antes
de perguntar o destino; nunca envia/publica nada automaticamente. Cancelamento em qualquer etapa,
pasta de destino que não pode ser criada e um arquivo de log travado por outro processo (falha
parcial) já eram tratados sem exceção não capturada — confirmados agora por teste automatizado em
vez de só revisão de código.

**Build e testes**: `dotnet build` do host — 0 avisos/erros; `dotnet test` do PXToolBox — 54/54
(net8.0) + **193/193** (net48, 193 = 181 anteriores + 12 novos em
`tests/PXToolBox.Tests/PxLoggerAndSupportPackageTests.cs`: gravação sem a mensagem da excessão,
rotação por dia, retenção por idade, retenção por tamanho total removendo o mais antigo primeiro,
retenção nunca afeta arquivos fora do padrão de nome do módulo, nunca lança mesmo com a pasta de
logs bloqueada, conteúdo do ZIP, ausência de nome de perfil/pasta/lista/segredos no pacote, falha
parcial por log travado e falha ao criar a pasta de destino). `dotnet test` do standalone —
282/282, confirmado sem alteração (nenhum arquivo dele foi tocado). Nenhum smoke test real no
Corel foi feito — abrir a pasta de logs/backups de verdade, gerar o pacote e inspecionar o `.zip`
no Explorer, continua pendente.

### Edição manual da saída/JSON do ListForge Integrado (iteração 17)

Confirmado lendo `MainViewModel.AllowOutputEditing`/`HasPendingOutputEdit`/`HasPendingJsonEdit`/
`SetGeneratedTexts`/`ApplyOutputEdits`/`DiscardOutputEdits`/`EnsureNoPendingOutputOrJsonEdits`/
`CopyOutput`/`CopyJson`/`SaveOutput`/`AdvancedSave`/`ClearAll` do standalone por completo antes
de implementar, além do teste `AdvancedListToggleTests.OutputEditingStartsDisabledAndIsSessionOnly`
(confirma que a opção nunca é persistida) e do mecanismo real de comparação
(`ListComparisonService.UpdateOutput`/`ComparisonSnapshot.OutputWasManuallyEdited`).

**Ativação só nesta sessão**: checkbox "Permitir edição da saída e do JSON nesta sessão"
(`ChkAllowOutputEditing`) — campo `_allowOutputEditing` da janela, nunca gravado em
`ListForgeIntegratedSettings` (mesma regra do standalone: `AllowOutputEditing` nunca existiu
como propriedade de `AppConfig`). Desligar com uma edição pendente nunca descarta
silenciosamente: pede Sim (aplica)/Não (descarta)/Cancelar (continua editando) — mesmo texto e
mesma lógica do setter `AllowOutputEditing` do standalone.

**Uma fonte por vez**: editar `OutputTextBox` marca `_hasPendingOutputEdit` e zera
`_hasPendingJsonEdit` (e vice-versa para `JsonTextBox`) — mesma regra exata dos setters
`OutputText`/`JsonText` do standalone. "Aplicar alterações" (`ApplyOutputEdits`) trata o texto
pendente como uma nova entrada: se vier do JSON, `ListForge.Core.ListProcessor.
ExtractListTextFromJsonData` (já compartilhado) converte para texto de lista antes; o resultado
passa pelo mesmo `ProcessingWorkflowService`/`BuildProcessingRequest` usado por "Processar" —
nunca aceita o conteúdo "como está". Falha de validação (lista com dados inválidos, JSON com
erro de sintaxe ou estrutura) preserva o texto editado na tela (nunca apaga) e mostra uma
mensagem; sucesso chama `SetGeneratedTexts` (novo método privado — ponto único onde
`OutputTextBox`/`JsonTextBox` recebem um resultado canônico, com a mesma guarda
`_isUpdatingGeneratedText` do standalone para a própria atribuição não ser lida como edição do
usuário) e marca `_outputWasManuallyEdited = true`. "Descartar alterações" volta a
`_lastOutputText`/`_lastValidJsonText` (último resultado válido), nunca marca "editado
manualmente".

**Indicador distinto do aviso de desatualização**: `ManualEditIndicator` (novo) mostra
"Alterações manuais pendentes..." ou "Resultado editado manualmente..." — nunca se confunde com
`OutputStaleWarning` (entrada mudou desde o último processamento); os dois podem aparecer juntos,
cada um com seu próprio significado, conforme pedido explícito da tarefa.

**Reprocessar nunca descarta silenciosamente**: `OnProcess`/`OnProcessWithPreview` chamam
`EnsureNoPendingOutputOrJsonEdits` (porta de `MainViewModel.EnsureNoPendingOutputOrJsonEdits`)
antes de montar a requisição — mesmo texto/opções Sim/Não/Cancelar do standalone. "Limpar tudo"
é a única ação que nunca pergunta (mesmo comportamento do `ClearAll()` real do standalone).

**Fonte de texto por ação — viva vs. canônica** (mesma regra exata do standalone, confirmada
lendo `CopyOutput`/`CopyJson`/`SaveOutput`/`GenerateJson`/`AdvancedSave`): "Copiar saída"/"Salvar
saída" usam `OutputTextBox.Text` (viva — inclui uma edição pendente ainda não aplicada, porque a
lista organizada não tem formato "inválido" possível); "Copiar JSON"/"Gerar JSON"/o campo JSON de
"Salvar avançado" usam o novo campo `_lastValidJsonText` (canônico, só muda num processamento ou
"Aplicar alterações" com sucesso) — **nunca** `JsonTextBox.Text` (vivo). Essa é a proteção real
contra exportar/copiar um JSON inválido/não revalidado como se fosse um arquivo pronto para o
SISBolt — `OnSaveOutput`/`OnAdvancedSave` foram ajustados nesta rodada para seguir exatamente essa
regra (antes usavam `_lastOutputText`/`JsonTextBox.Text` sem essa distinção, inofensivo até agora
porque as caixas eram sempre somente leitura). Números (ex.: "07") são preservados como texto em
todo o ciclo editar-JSON → `ExtractListTextFromJsonData` → reprocessar → novo texto de saída —
provado de ponta a ponta por teste.

**Comparação aceita e sinaliza edição manual**: ao aplicar uma edição com sucesso, se o snapshot
de comparação existente ainda corresponder à entrada atual (mesma verificação `!_freshness.
IsStale` + igualdade de `InputTextBox.Text`), `ListComparisonService.UpdateOutput(...,
outputWasManuallyEdited: true)` atualiza o snapshot em vez de descartá-lo; caso contrário, o
snapshot é descartado (força uma comparação nova). `ComparisonWindow` já tinha, de uma rodada
anterior nunca exercitada de verdade, `OutputOriginText`/`item.Details` distinguindo "Saída com
edição manual aplicada" — nenhuma UI nova foi necessária. "Comparar entrada/saída" também recusa
comparar enquanto há uma edição pendente (nunca aplicada).

**Build e testes**: `dotnet build` do host — 0 avisos/erros; `dotnet test` do PXToolBox — 54/54
(net8.0) + **208/208** (net48, 208 = 193 anteriores + 15 novos em
`tests/PXToolBox.Tests/ManualOutputJsonEditingTests.cs`). Nenhum teste novo invoca um caminho que
mostraria um `MessageBox` real (confirmação Sim/Não/Cancelar, ou a mensagem de erro ao aplicar
JSON/lista inválida) — travaria a execução automatizada esperando um clique humano; o próprio
`ListForge.Tests` do standalone segue a mesma regra (nenhum teste ali toca `MessageBox.Show`). A
rejeição de JSON sintática/estruturalmente inválido é provada no mesmo mecanismo real que
`ApplyOutputEdits` usa (`JObject.Parse`/`ListProcessor.ExtractListTextFromJsonData`), sem passar
pelo botão — essa cobertura, mais a confirmação de que `AllowOutputEditing` nunca é persistida,
completam os cenários pedidos sem o risco de travar a suíte. `dotnet test` do standalone —
282/282, confirmado sem alteração (nenhum arquivo dele foi tocado). Nenhum smoke test real no
Corel foi feito — a confirmação modal ao desligar edição/reprocessar com pendência, e a mensagem
de erro ao aplicar conteúdo inválido pelo botão, continuam pendentes de validação manual.

### Modo PX implementado — estado único, duas apresentações (iteração 23)

Implementação real do modo PX sobre o design aprovado (`docs/design/listforge-px-workflow/`),
dentro da MESMA classe `ClassicWorkspaceWindow` (nova parte, `ClassicWorkspaceWindow.Px.cs`) —
nunca uma janela/classe separada e nunca uma segunda cópia do estado de sessão.

**Reparenting em vez de duplicação**: `PxActivate`/`PxDeactivate` MOVEM (via
`Panel.Children.Remove`/`Insert` e `ContentControl.Content`) os controles nomeados do Clássico
entre o layout Clássico (`ClassicRoot`) e o layout PX (`PxRoot`) — nunca criam uma segunda
instância. Controles movidos: `TxtSeparator`, `CmbCaseMode`, `CmbSortMode`, o Expander
"Localizar/Substituir" inteiro, o Expander "Lista avançada" inteiro (a mesma quantidade de
posições de `RefreshAdvancedSlots`, nunca um número fixo diferente do Clássico), o painel de
entrada (`InputTextBox` + rótulo), `IssuesList`, o painel de saída inteiro (abas Lista
organizada/JSON, avisos de desatualização/edição manual, `OutputTextBox`/`JsonTextBox`), o
painel de edição manual, o combo de perfil de trabalho, o texto de acesso (`AccessStatusText`),
a barra de status (`StatusText`) e o Expander "Diagnóstico" inteiro — os três últimos foram um
gap real encontrado só depois do primeiro build limpo (a primeira versão do XAML/código
compilava e a janela abria, mas Diagnóstico/status ficavam inacessíveis em modo PX, porque
continuavam filhos de `ClassicRoot`, que fica `Collapsed`; corrigido movendo-os também para
dentro de `PxHeaderSlot`). `PxDeactivate` restaura cada controle exatamente no mesmo pai e índice
originais (rastreados em `_pxOriginalParents`), nunca numa ordem diferente da que já tinha.
Trocar de modo nunca reprocessa, salva ou descarta nada — só move controles; uma edição manual
pendente, um resultado desatualizado ou uma entrada com alterações não salvas continuam
idênticos, só mudando de apresentação visual.

**Fluxo Lista → Revisão → Resultado, sobre os mesmos serviços do Clássico**: `RefreshPxRevisaoAsync`
chama `ProcessingPreviewService.Analyze` (mesmo serviço de `OnProcessWithPreview`) e preenche
contagens reais (total/válidos/avisos/inválidos), uma tabela de interpretação por linha
(`PxRevisaoRows`, a partir de `ProcessingPreview.AnalysisResult.Rows`) e `IssuesList` reaproveitado
(mesmo `preview.Issues` e mesmo formato de mensagem que `ProcessingPreviewWindow` já usa para o
diálogo modal do Clássico — não `AnalysisResult.ValidationIssues`, que não inclui os avisos de
severidade Warning). Clicar num item de `IssuesList` continua chamando o mesmo `OnIssueSelected`
do Clássico (não duplicado); como `InputTextBox` vive na etapa Lista, esse método foi
levemente estendido para voltar à etapa Lista quando o modo ativo é PX — sem isso, a
seleção/scroll aconteceria fora de tela enquanto o usuário está em Revisão (segundo gap real
encontrado e corrigido antes da entrega).

**"Processar" sem inventar progresso**: `OnPxProcess` mostra só `PxProcessingOverlay` ("
Processando...", sem porcentagem/duração — o núcleo não expõe progresso granular), impede
processamento concorrente (`_pxIsProcessing`, com `PxBtnProcess.IsEnabled=false` como segunda
barreira) e nunca associa um resultado a uma entrada diferente da que foi de fato revisada:
antes de confirmar, reconstrói a requisição atual e compara campo a campo com a capturada na
última Revisão (`PxRequestsMatch` — `InputText`/`Separator`/`CaseMode`/`SortMode`/
`JsonPieceMapping.UseCustomOrder`/`PieceOrder.SequenceEqual`; nunca `==`/`.Equals()` de record,
que compararia a lista `PieceOrder` por referência, não por conteúdo); se algo mudou, recusa
processar e atualiza a prévia em vez disso. Sucesso chama `ApplyResult` (mesmo método do
Clássico, sem reimplementação) e abre Resultado; falha preserva a entrada/opções e mostra o erro
num banner em Revisão, sem fechar nem limpar nada.

**Preferência de modo por perfil**: `ListForgeIntegratedSettings.PreferredWorkspaceModeByProfileId`
(campo aditivo, exclusivo do PX DEV, chaveado por `WorkProfile.Id`) — deliberadamente NUNCA
dentro de `WorkProfile`/`WorkProfileSettings` (`ListForge.Core`, compartilhados de verdade com o
ListForge Standalone, que não tem conceito de "modo PX"). Aplicada só na ABERTURA da janela
(`PxApplyInitialMode`, chamado uma vez no fim do construtor); trocar de perfil de trabalho
DURANTE a sessão não troca o modo automaticamente — decisão deliberada para não surpreender o
usuário com uma troca de apresentação implícita; registrada aqui como limitação, não como bug.

**"Nova lista"**: proposta nova desta etapa (sem equivalente direto no Clássico, documentada
como decisão em aberto em `design.md`, nunca atribuída ao usuário como aprovação). Só pergunta
quando há algo a perder de fato (entrada não vazia, edição manual pendente ou resultado atual) —
nunca incondicionalmente.

**Limitações explícitas** (nenhuma destas é tratada como concluída): nome continua "PX — em
desenvolvimento"; sem leitura/montagem de objetos do documento Corel (texto, igual ao Clássico);
sem Nexor, sem OCR, sem regra de licenciamento nova; nenhuma ferramenta de Saídas alterada;
aceitação da `.26` continua pendente por decisão do usuário, sem nenhum item tocado.

**Build e testes**: `dotnet build` do host — 0 avisos/erros. `dotnet test` do PXToolBox — 54/54
(net8.0) + 243/243 (net48, 243 = 232 anteriores + 11 novos: `ListForgePxModeTests` — troca de
modo ida/volta sem lançar e restaurando o pai original, troca repetida sem lançar,
`PxRequestsMatch` comparando conteúdo em vez de referência de lista; mais 6 testes novos de
`GetPreferredWorkspaceMode`/`SetPreferredWorkspaceMode`/round-trip em disco em
`ListForgeIntegratedSettingsStoreTests`). `RefreshPxRevisaoAsync`/`OnPxProcess` não têm teste
automatizado direto (são `async void`; a janela de teste roda numa thread STA sem
`Dispatcher.Run()`, então uma continuação de `await Task.Run(...)` nunca seria bombeada — mesma
limitação que já impede testes diretos de `OnProcess`/`OnProcessWithPreview`/`OnAdvancedSave` no
Clássico) — cobertos indiretamente pelos testes já existentes de `ProcessingPreviewService`/
`ProcessingWorkflowService`. Nenhum smoke test real no Corel foi feito — abertura sem erro,
troca de modo preservando dados, Revisão/Processar reais e teclado funcionando dentro do Corel
continuam pendentes de validação manual.

## 11. Inspecionar Shape.Properties (diagnóstico, exclusivo DEV)

Ferramenta nova, sem relação com o ListForge Integrado — painel DEV, seção "Diagnóstico" →
"Inspecionar Shape.Properties". Objetivo: investigar como o SISBolt marca objetos com opções
persistentes (ex.: "Tamanho padrão"), mostrando os dados brutos de `Shape.Properties` do único
objeto selecionado. **100% somente leitura** — nunca cria, altera ou exclui nada.

**API real confirmada por reflection sobre `Corel.Interop.VGCore.dll` instalado (27.1.0.129)
antes de implementar** — não suposta a partir da descrição genérica da documentação:

- `Shape.Properties` → `Corel.Interop.VGCore.Properties` (implementa `IVGProperties` +
  `IEnumerable`).
- `IVGProperties.Count` → `int`.
- `IVGProperties.ItemByIndex[int Index]` → `object` (o valor bruto/Variant).
- `IVGProperties.Description(int Index, out string Name, out int ID)` — dado um índice
  posicional (0-based), devolve Nome e ID por parâmetro de saída (nomes de parâmetro exatamente
  como no SDK: `Index`/`Name`/`ID`). **Precisa de `out`, não `ref`** — confirmado só compilando
  contra o assembly real (`ref` falha com `CS1620`).
- `IVGProperties.Item[string Name, int ID]` / `IVGProperties.Index(string Name, int ID)` /
  `IVGProperties.Exists(string Name, int ID)` — leitura por nome+ID, não usados pelo inspetor
  (que enumera por índice posicional), mas confirmados existentes.
- Membros de ESCRITA confirmados e nunca chamados por este inspetor: `set_Item`, `Delete`,
  `DeleteByIndex`, `DeleteAll`, `PutFile`, `SetPoint`, `SetVector`, `SetCurve`.
- Contexto do objeto: `Shape.Name`, `Shape.Type` (`cdrShapeType`), `Shape.Layer.Name`,
  `Shape.Page.Index`, `Shape.StaticID` (sem setter — confirmado imutável), `Shape.SizeWidth`/
  `SizeHeight`. Nenhum destes é tratado como parte de `Shape.Properties` (são membros próprios
  de `Shape`).

**Arquitetura**: `ShapePropertiesInspectorService` concentra todo o acesso COM (deliberadamente
fora da abstração `ICorelShape`/`ICorelWorkspace` de `PXToolBox.Outputs.Abstractions` — aquela
existe para isolar a lógica de NEGÓCIO das Saídas do COM, não para escond er justamente os dados
brutos que este inspetor precisa mostrar). `ShapePropertyValueFormatter` (puro) classifica cada
valor em Boolean/Integer/Double/String/Date para tipos simples, ou
`<tipo COM: System.Namespace.TipoReal>` para qualquer struct/objeto COM/array — nunca chama
serializador arbitrário nem método destrutivo. `ShapePropertiesReportFormatter` (puro) monta o
texto de "Copiar tudo"/"Salvar relatório...". `ShapeInspectorWindow` só chama o serviço, nunca
VGCore diretamente.

**Estados tratados explicitamente**: sem documento aberto, sem seleção, múltiplos objetos
selecionados, zero propriedades (mensagem explícita "não é erro"), uma propriedade impossível
de ler (não interrompe as demais — cada entrada tem seu próprio `ReadError` opcional), objeto
excluído depois de aberto o inspetor ("Atualizar" detecta a falha ao reler os dados básicos do
`Shape` e nunca reusa a referência COM depois disso).

**Testes automatizados** (`ShapePropertiesInspectorTests.cs`, 11 novos): cobrem a parte PURA —
classificação de valor (Boolean/Integer/Double/String/Date/nulo/COM desconhecido) e o relatório
textual (contexto do objeto, cada entrada, erro de leitura isolado, zero propriedades) — mais a
construção real de `ShapeInspectorWindow` numa thread STA (mesma disciplina da iteração 8). A
travessia COM em si (`Count`/`ItemByIndex`/`Description`, detecção de seleção/documento, objeto
excluído) só é confirmável dentro do CorelDRAW real — pendente de smoke test, ver
`docs/mvp-smoke-test.md`.

**Exemplo de relatório** (gerado pelo `ShapePropertiesReportFormatter` real, com um objeto de
teste fabricado — não um objeto real do Corel):

```
PX Shape.Properties Inspector

Object
Name: Logo
Type: cdrCurveShape
Layer: CAM1.G.MA
Page: 1
StaticID: 987654
Width: 10.5
Height: 20.25

Properties
Count: 2

[0]
Name: SISBolt.TamanhoPadrao
ID: 1
Value: True
Type: Boolean

[1]
Name: SISBolt.Observacao
ID: 2
Value: ajuste manual
Type: String
```

**Pendente**: smoke test real dentro do CorelDRAW (o próprio pedido já registra isso como
necessário — a enumeração COM precisa ser confirmada no host real). Interpretação do que o
SISBolt realmente usa para "Tamanho padrão" — deliberadamente NÃO feita ainda (pedido explícito:
"não sabemos qual propriedade o SISBolt utiliza... primeiro queremos observar os dados brutos").
Inspetor de `Shape.ObjectData` — só depois da comparação A/B/C/D do SISBolt decidir se é
necessário.

## 12. Pré-requisitos externos (não redistribuídos neste repositório)

- `Corel.Interop.VGCore.dll`, `Corel.Interop.CorelDRAW.dll` — vêm da instalação do CorelDRAW
  (`Programs64\Assemblies\`), referenciadas via `HintPath` com `Private=false` (nunca copiadas
  para a saída de build nem para o instalador).
- Inno Setup 6 (`ISCC.exe`) — só necessário para gerar o instalador.
- GitHub CLI / AWS CLI — só necessários para os botões de publicação.

## 13. Limitações conhecidas desta entrega

- "Agrupar Interseção" foi implementado a partir do candidato mais plausível encontrado
  (`Agrupamento.AgruparObjetosSobrepostos`, numa revisão candidata de macros — ver
  `docs/migrated-tools.md`). Testado (`IntersectGroupServiceTests`) e **confirmado pelo usuário
  no CorelDRAW real** no cenário testado (2026-10; reafirmado em 05/11/2026).
- `MoldPreparationService` busca grupos recursivamente dentro de cada camada, confirmado via
  reflexão (o parâmetro `Recursive` de `Shapes.FindShapes` no VGCore tem valor padrão `True`).
- `cdrShapeType` (VGCore v27) não tem mais um valor distinto para PowerClip — `ShapeKind.PowerClip`
  nunca é retornado pelo adaptador real; reconfirmado por reflexão direta sobre
  `Corel.Interop.VGCore.dll` instalado nesta máquina (os 27 valores reais de `cdrShapeType`
  foram listados um a um — nenhum chamado `cdrPowerClipShape`). Não é mais necessário descobrir
  "em qual tipo os PowerClips aparecem": a detecção de PowerClip não depende de `Type`/`Kind` —
  usa a propriedade separada `Shape.PowerClip` (ver `ICorelShape.HasPowerClipContents`/
  `PowerClipShapes`, iteração 25). `ShapeKind.PowerClip` continua inexistente na prática, mas
  deixou de ser necessário para esse propósito.
- O caminho de saída `Z:\Neuber\Saídas\` é o padrão (preservado de propósito — ver
  `docs/migrated-tools.md`), configurável via `OutputPreparationService.SaveOutput`, ainda sem
  tela de configuração no host (hoje são campos de texto simples no painel).
- A pasta `Referência/Macros/` que fundamentou várias decisões desta entrega é, pelas próprias
  palavras do seu `LEIA-ME.md`, uma "revisão candidata" **revisada estaticamente, não compilada
  nem executada no Corel**. Toda ferramenta portada a partir dela carrega essa mesma ressalva
  até o smoke test real confirmar o contrário.
- Não há validação interativa dentro do CorelDRAW real nesta entrega — só build e testes
  automatizados (ver `docs/mvp-smoke-test.md` para o que falta e os comandos exatos).

## 14. Nexor — primeiro acesso (host DEV)

Primeira etapa da integração com Produção (ver AGENTS.md, "Responsabilidades do ecossistema":
Nexor é dono de pedidos/planejamento/filas/produção/registros/status) — só localizar e abrir o
executável já existente, no mesmo padrão de "ListForge Standalone" (seção 9). Esta entrega NÃO
incorpora a interface do Nexor, não compartilha dados de pedido, não implementa contexto
compartilhado/sincronização/retorno de status, e não lê nem altera `nexor.db`.

**Distribuição real localizada no ambiente** (nenhum caminho, nome de executável ou argumento
foi suposto): o repositório de origem é `C:\Projetos\ProjetoJocasta` — o próprio `CHANGELOG.md`
do projeto confirma um "Rebrand para Nexor" ("O programa foi renomeado de 'Projeto Jocasta'/
JocastaHub para **Nexor** — `Nexor.py`, `Nexor.spec`, título da janela, versão"), ou seja,
"Projeto Jocasta" é o codinome histórico do repositório, não um produto diferente. O build
oficial é feito com PyInstaller (`--onefile --windowed`, ver `temp_files/compile.bat` do próprio
repositório do Nexor — nenhum script Python nem comando de inicialização foi inventado aqui,
só o mecanismo já documentado pelo projeto), publicado em `ProjetoJocasta\dist\Nexor-<versão>.exe`
a cada versão (85 builds incrementais encontrados, do `1.0.0` ao `1.6.29`, o mais recente
compilado um dia antes desta tarefa). Confirmado como o executável real em uso: o atalho fixado
pelo usuário na barra de tarefas do Windows (`...\Quick Launch\User Pinned\TaskBar\
Nexor-1.6.29.lnk`) aponta exatamente para `C:\Projetos\ProjetoJocasta\dist\Nexor-1.6.29.exe`,
com `WorkingDirectory` igual à própria pasta do executável e nenhum argumento de linha de
comando — confirmando, com evidência real (não suposição), tanto o caminho quanto a ausência de
argumentos. Outras pastas candidatas descartadas por não serem a distribuição em uso: `Nexor`
(fonte Python mais antiga, sem build empacotado), `Nexor-Manus`/`Nexor-New`/
`nexor-app-complete(-v2)` (variantes sem evidência de uso corrente), `Nexor-v0.3.0.exe` solto em
`C:\Projetos\` (build antigo, anterior aos 85 builds do ProjetoJocasta).

**Implementação**: `Integrations/NexorLauncher.cs` + `Integrations/NexorConfigStore.cs` —
estrutura igual a `ListForgeLauncher`/`ListForgeConfigStore` (decisão pura testável por
delegates injetados, nunca abre processo real dentro de um teste; `StartNexorProcess`, em
`ControlUI.xaml.cs`, usa `Process.Start` com `UseShellExecute=false`, sem argumentos, sem
elevação, sem concatenação de shell — mesma garantia do ListForge), mas uma classe e um arquivo
de configuração PRÓPRIOS (`nexor-path.txt`, mesma pasta `PXToolBox\Dev\config`, mas nunca o
mesmo arquivo) — a configuração dos dois aplicativos externos nunca é compartilhada, só mora na
mesma pasta por conveniência deste módulo. O caminho pertence à máquina
(`LocalApplicationData`, não `ApplicationData`/roaming), exatamente como o do ListForge.

**Interface**: nova seção "Produção" no painel DEV (`ControlUI.xaml`, logo após "Listas"), com
"Abrir Nexor ↗" e "Localizar executável…", mesmo padrão visual/`ToolTip` do ListForge Standalone.
Funciona sem documento aberto no CorelDRAW (não passa por `RunWithWorkspace`/`Connect`, igual ao
ListForge Standalone). Mensagem de sucesso nunca afirma "Nexor pronto" — só confirma que o
processo foi iniciado (PID), com a mesma ressalva explícita do ListForge
("Não é possível confirmar, só por isso, que a janela abriu ou que o Nexor está pronto para
uso.").

**Testes**: `NexorLauncherTests`/`NexorConfigStoreTests` (11 testes) — primeira configuração,
persistência ao reabrir, caminho com espaços, executável movido/excluído, cancelamento, falha de
inicialização sem perder o caminho salvo, e independência real da configuração do ListForge
(salvar um caminho nunca afeta o outro — testado explicitamente, não só por inspeção). Nenhum
teste abre o Nexor real nem toca em `nexor.db`. `dotnet build` do host — 0 avisos/erros;
`dotnet test` do PXToolBox — 54/54 (net8.0) + 254/254 (net48, 254 = 243 anteriores + 11 novos).
Teclado, modos Clássico/PX do ListForge Integrado e ferramentas de Saídas — nenhum arquivo deles
foi tocado nesta tarefa; suíte completa roda sem regressão.

**Pendências explicitamente preservadas, não tocadas nem misturadas com esta entrega**:
aceitação da `0.1.0-dev.26`; validação do modo PX da `0.1.0-dev.27` dentro do CorelDRAW real; o
caso "Carlos Teste,15,ZZZ" (ZZZ interpretado como apelido, com aviso de tamanho não encontrado);
e a investigação do redesenho intermitente do painel (`PanelRenderDiagnostics`, seção 10 do
ARCHITECTURE anterior a esta). Nenhum smoke test real do botão "Abrir Nexor ↗" dentro do
CorelDRAW foi feito nesta tarefa — ver `docs/mvp-smoke-test.md`.

## 15. "Remover NO/NU da página" — correção para descer também em PowerClip (iteração 25)

Pedido do usuário: o botão "Remover NO/NU da página" (`CodeToggleService.ExpandNoAndNuCodesRecursively`)
já descia recursivamente em grupos, mas nunca em PowerClips — um objeto nomeado "NO"/"NU" dentro
de um PowerClip ficava invisível para esta rotina, mesmo depois de rodar o botão na página
inteira. **Não é uma regressão desta migração**: lendo o `.bas` original
(`CorelMacros/Correcoes.bas`, `Sub RenameObjects`) confirma-se que a macro de origem desta
ferramenta específica JAMAIS tratou PowerClip — só checava `s.Type = cdrGroupShape`. (O mesmo
arquivo tem outra macro, `AjustarLarguraObjetosRecursivo`, que já tratava
`cdrGroupShape Or cdrPowerClipShape` igualmente — mas essa é uma ferramenta DIFERENTE,
deliberadamente não portada, ver `docs/migrated-tools.md`, "Deferido".) A descida em PowerClip
adicionada aqui é uma melhoria pedida pelo usuário, não uma restauração de comportamento antigo.

**Mecanismo real, confirmado por reflexão sobre `Corel.Interop.VGCore.dll`** (a superfície da
API, não o comportamento em tempo de execução — ver ressalva abaixo): `Shape.Shapes`
(`IVGShape.Shapes`) e `Shape.PowerClip.Shapes` (`IVGPowerClip.Shapes`) são propriedades de TIPOS
.NET diferentes — `Shapes` vs. uma coleção obtida só através do objeto `PowerClip` retornado por
`Shape.PowerClip` (`IVGShape.PowerClip`, tipo `Corel.Interop.VGCore.PowerClip`). Ou seja, a
coleção de filhos de um GRUPO e o conteúdo recortado de um POWERCLIP nunca são a mesma
propriedade na API atual — `Native.Shapes` nunca inclui conteúdo de PowerClip. Daí a nova dupla
verificação em `CodeToggleService.RenameRecursive`: desce em `shape.Shapes` quando
`Kind == Group` E, independentemente, desce em `shape.PowerClipShapes` quando
`shape.HasPowerClipContents` — as duas podem ser verdadeiras ao mesmo tempo (um grupo também
pode ser contêiner de PowerClip), nenhuma impede a outra. Em ambos os casos, o nome do próprio
contêiner nunca é verificado (mesma regra que grupos já tinham).

**O que foi e não foi validado**: a reflexão confirma os NOMES e TIPOS reais da API
(`Shape.PowerClip`/`PowerClip.Shapes` existem exatamente assim nesta instalação do CorelDRAW
27), e confirma de novo, por inspeção direta dos 27 valores reais de `cdrShapeType`, que não há
valor distinto de PowerClip ali (ver seção 13) — mas **o comportamento em tempo de execução**
(se `Shape.PowerClip` retorna `null` para um shape comum, ou lança, ou se há algum caso em que
`Shape.Shapes` e `Shape.PowerClip.Shapes` se sobrepõem) não foi testado dentro do CorelDRAW
real. Caso haja sobreposição em algum cenário não previsto, o algoritmo é seguro de qualquer
forma: `RenameRecursive` já checa o NOME atual do shape antes de renomear, então visitar o mesmo
shape duas vezes (por exemplo) não causaria uma segunda renomeação nem contagem duplicada — a
segunda visita simplesmente não encontraria mais "NO"/"NU" para combinar.

**Novo contrato exposto em `ICorelShape`** (`src/PXToolBox.Outputs/Abstractions/ICorelShape.cs`):
`HasPowerClipContents` (bool) e `PowerClipShapes` (IReadOnlyList\<ICorelShape\>) — implementados
em `CorelShapeAdapter` (`Native.PowerClip != null` / `Native.PowerClip.Shapes`) e em `FakeShape`
(campos simples, para teste). O comentário antigo de `ICorelShape.Shapes` ("Subformas diretas
(grupo ou PowerClip)") foi corrigido — essa suposição nunca foi confirmada e, pela API real, é
provavelmente falsa.

**Testes**: 4 novos em `CodeToggleServiceTests.cs` — desce em PowerClip simples; nunca renomeia
o contêiner do PowerClip; desce nos dois (grupo E PowerClip) quando o mesmo shape é ambos;
PowerClip aninhado dentro de grupo. Nenhum teste real dentro do CorelDRAW foi feito — a
confirmação de que isto funciona com um PowerClip de verdade, inclusive copiando o objeto para
outro documento, continua pendente de validação manual.

## 16. Versões DEV e proteções de empacotamento (iteração 26)

Pedido do usuário: havia relato de uma execução que considerou `0.1.0-dev.9` como mais recente
quando já existia `0.1.0-dev.28`.

**Causa investigada, não suposta**: lidos por completo todos os scripts de build/validação/
publicação deste repositório (`build-release.ps1`, `test-release.ps1`,
`create-github-release.ps1`, `publish-r2-release.ps1`, `install-dev-local.ps1`,
`uninstall-dev-local.ps1`) e o instalador (`installer/PXToolBoxDev.iss`) — **nenhum deles
descobre "a versão mais recente" automaticamente**; todos exigem `-Version` explícito, sem
nenhuma ordenação/seleção de pasta. Ou seja, o incidente relatado não corresponde a um bug já
existente no código deste repositório — é consistente com uma decisão ad-hoc (um comando de
listagem/ordenação improvisado em algum momento, fora de qualquer script versionado), não uma
lógica persistida. Dito isso, o risco é real e concreto: comparando como STRING pura,
`"0.1.0-dev.9"` > `"0.1.0-dev.28"` (o caractere `'9'` tem valor maior que `'2'` na posição em
que as duas strings diferem) — qualquer ordenação textual futura cometeria exatamente esse
erro. Esta rodada endurece o projeto contra essa classe de erro, independente de onde ele teria
se originado.

**Três estados que nunca podem ser tratados como equivalentes** (pedido explícito do usuário):
- **Versão declarada no código**: o parâmetro `-Version`/`-p:PxToolBoxVersion` passado para
  `build-release.ps1`, gravado como `AssemblyInformationalVersion` no `.dll` publicado — decidida
  por quem opera o Stream Deck, nunca descoberta automaticamente por este projeto.
- **Pacote gerado**: o conteúdo físico de `dist\<channel>\<versão>\` (instalador, checksums,
  manifesto) — pode estar incompleto (build interrompida) ou divergente (nome da pasta ≠ versão
  do manifesto ≠ versão no nome do artefato) mesmo que a versão "declarada no código" tenha sido
  correta.
- **Versão distribuída**: o que `publish-r2-release.ps1` publica como `latest.json` do canal —
  continua, como antes desta rodada, decidido pelo `-Version` explícito de quem publica
  (`Publish-R2Release.ps1` nunca comparou contra a versão já publicada — simplesmente sobrescreve
  o ponteiro com o que foi pedido; isso não mudou nesta rodada, por estar fora do escopo pedido —
  "não altere... destinos remotos" — mas é registrado aqui como uma lacuna real: nada impede
  alguém de publicar manualmente uma versão mais antiga como `latest.json` sem aviso).

**Correção**: novo módulo `scripts/PxToolBoxVersioning.psm1`, centralizando toda comparação de
versão deste projeto:
- `ConvertFrom-PxVersionString` — converte `"X.Y.Z"` ou `"X.Y.Z-dev.N"` numa chave numérica
  comparável; qualquer outro formato retorna `$null` (nunca lança, nunca aproxima).
- `Compare-PxVersion` — compara duas chaves componente a componente como **inteiros** (nunca
  texto): `dev.9 < dev.10 < dev.27 < dev.28 < dev.100`; um Stable `X.Y.Z` sempre supera qualquer
  `-dev.N` do mesmo `X.Y.Z`.
- `Get-PxToolBoxDistVersions` — varre `dist\<channel>\`, separando cada pasta em `Valid`
  (nome + manifesto + artefato coerentes entre si, arquivo do instalador realmente presente),
  `InvalidFolderName` (nome fora do formato — nunca entra na comparação, mas é relatado),
  `IncompleteBuild` (nome válido, sem `release-manifest.json`) e `Divergent` (nome ≠
  `manifest.version`, ou `manifest.channel` errado, ou artefato ausente/com nome que não contém
  a versão esperada).
- `Resolve-PxToolBoxLatestVersion` — a mais recente entre as `Valid` (ordenação numérica real,
  nunca por nome de pasta/data de modificação); nunca escolhe uma pasta `Divergent`/
  `IncompleteBuild`/`InvalidFolderName` como "mais recente", mesmo que o nome dela pareça maior.
- `Assert-PxToolBoxVersionOutputWritable` — a mesma regra de proteção que `build-release.ps1`
  já tinha (recusa sobrescrever sem `-Force`; com `-Force`, remove SÓ a pasta exata pedida),
  extraída para ser testável com diretórios temporários sem rodar uma build .NET real.
- `Get-PxToolBoxOlderVersionWarning` — nunca bloqueia (pedido explícito: preservar a
  reprodução de versões antigas por um fluxo explícito), só avisa em destaque quando a versão
  sendo gerada é numericamente mais antiga que a mais recente `Valid` já existente no canal —
  exatamente o cenário do incidente relatado (gerar `dev.9` depois de já existir `dev.28`).

`build-release.ps1` agora importa este módulo e usa as duas últimas funções no lugar da lógica
que antes estava inline — mesma mensagem de erro exata de antes (confirmado rodando o script
real contra a build `0.1.0-dev.28` já existente, sem `-Force`: lança sem tocar em nada). Nenhum
outro script foi alterado (fora do pedido: "não altere credenciais, destinos remotos ou
arquivos publicados").

**Separação Dev/Stable**: sempre foi garantida pelo caminho (`dist\dev\` vs. `dist\stable\`,
join de `$channelLower`) — `Get-PxToolBoxDistVersions`/`Resolve-PxToolBoxLatestVersion` recebem
`-Channel` explícito e só varrem a subpasta daquele canal; confirmado por teste (criar uma
versão Stable não afeta a contagem/resolução em Dev, e vice-versa).

**Testes**: `scripts/Test-PxToolBoxVersioning.ps1` — 33 asserts, só com diretórios temporários
(`[System.IO.Path]::GetTempPath()`, removidos no fim mesmo se um assert falhar) — nunca toca em
`dist\` real. Cobre: ordenação numérica dev.9/10/27/28/100 e Stable > dev.N; formatos inválidos
(incluindo string vazia — expôs um comportamento real do PowerShell, não um bug da lógica: um
parâmetro `[string]` `Mandatory=$true` rejeita string vazia por padrão, precisando de
`[AllowEmptyString()]` explícito); descoberta com mistura de válidas/inválidas/incompletas/
divergentes; resolução da mais recente ignorando as inconsistentes; separação Dev/Stable;
aviso de versão mais antiga (presente quando esperado, ausente quando a versão é de fato mais
nova); sobrescrita sem `-Force` (lança, não remove nada) e escopo de `-Force` (remove só a
pasta exata, nunca uma pasta vizinha). Um bug real foi encontrado e corrigido durante os testes:
`Set-StrictMode -Version Latest` (ativado no módulo) expôs que `Get-ChildItem` sem resultados
retorna `$null`, não um array vazio — `.Count` sobre isso lançava; corrigido envolvendo a
chamada em `@(...)`.

**O que não foi tocado**: nenhuma credencial, destino remoto ou arquivo já publicado — a lacuna
real do `latest.json` nunca comparar contra a versão já publicada (ver acima) foi só registrada,
não corrigida, por estar fora do escopo desta tarefa.

## 17. Falha ao salvar perfis de trabalho — "Não foi possível remover o arquivo a ser substituído" (iteração 27)

**Sintoma relatado pelo usuário**: ao salvar perfis de trabalho do ListForge Integrado, a
mensagem "Não foi possível salvar os perfis de trabalho: Não foi possível remover o arquivo a
ser substituído." — essa segunda frase é literalmente a mensagem que `System.IO.File.Replace`
produz quando o sistema operacional não consegue remover o arquivo de DESTINO no momento da
troca.

**Localização exata**: `Integrations/ListForgeIntegratedSettingsStore.Save` (gravação de
configurações, perfis de trabalho E tamanhos customizados — os três vivem no mesmo objeto/
arquivo, `%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-settings.json`). A operação
que falha é `File.Replace(tempPath, FilePath, null)`, chamada só no caminho de ATUALIZAÇÃO
(destino já existe) — o primeiro salvamento (`File.Move`, destino ainda não existe) nunca
produz essa mensagem específica, por isso o sintoma só aparecia depois do primeiro save.

**Diagnóstico — o que foi encontrado e o que não foi presumido**:
- `File.Replace` reporta essa mensagem especificamente quando algum handle aberto no arquivo de
  destino, sem `FileShare.Delete`, impede a operação `ReplaceFile` do Win32 por trás dela —
  reproduzido de verdade (não simulado) nos testes abaixo com uma `FileStream` real em
  `FileShare.None`.
- **Gravações concorrentes dentro deste processo**: investigados todos os métodos que chamam
  `Save` (`ClassicWorkspaceWindow` — 7 manipuladores de perfil de trabalho — e
  `SettingsWindow`/`SizeConfigWindow`). `OnWorkProfileSelectionChanged`, que também chama
  `Save`, já era protegido contra reentrância por `_isApplyingWorkProfile` ao trocar o
  `SelectedItem` programaticamente dentro de `RefreshWorkProfilesUi` — confirmado lendo o
  código, não corrigido porque já estava correto. Mesmo assim, nada no código impedia DUAS
  chamadas a `Save()` em paralelo por outra via (ex.: uma futura mudança introduzindo
  `async`/thread em segundo plano) — por isso um lock interno foi adicionado de qualquer forma,
  como proteção estrutural, não porque uma corrida concreta tenha sido observada nos
  manipuladores atuais.
- **Streams/handles não liberados**: `File.ReadAllText`/`File.WriteAllText` (usados em todo o
  código envolvido) sempre abrem e fecham o handle numa única chamada — nenhum vazamento de
  stream encontrado no código deste módulo. Não há evidência de que o processo do próprio PX
  Tool Box deixe um handle aberto no arquivo de configurações fora de uma operação de
  Load/Save pontual.
- **Antivírus/permissões**: nenhuma evidência coletada aponta para isso — não atribuído como
  causa. O código agora é robusto contra bloqueio transitório de QUALQUER origem (processo
  externo, indexação do Windows, ou o próprio PX), sem precisar identificar qual seria.
- Conteúdo dos perfis nunca registrado em log — só tipo da exceção, HResult e stack trace (ver
  abaixo), e o CAMINHO do arquivo (nunca seu conteúdo).

**Correção — `Integrations/AtomicFileWriter.cs`** (novo, infraestrutura COMPARTILHADA entre
`ListForgeIntegratedSettingsStore` e `ListForgeProcessingHistoryService`, que tinha uma cópia
quase idêntica da mesma lógica de troca de arquivo — "a causa justifica" corrigir as duas, não
só a reportada):
- Arquivo temporário sempre na MESMA PASTA do destino (nunca outro volume — necessário para a
  troca atômica).
- Lock interno (`static readonly object`) serializa qualquer chamada concorrente de verdade
  dentro do processo para o mesmo caminho.
- Primeiro salvamento: `File.Move` puro, sem lógica de substituição.
- Atualização: tenta `File.Replace` até 3 vezes (com pausas de 0/60/200 ms, tolerando um
  bloqueio bem curto) antes de cair para uma troca manual com backup
  (`ReplaceWithManualBackup`): renomeia o destino atual para um nome de backup, move o
  temporário para o lugar do destino e só então remove o backup — se a segunda etapa falhar por
  qualquer motivo, o backup é restaurado ANTES de propagar a exceção, então o arquivo anterior
  válido nunca fica indisponível por mais tempo que uma única operação de sistema de arquivos e
  nunca é perdido.
- **O destino nunca é apagado antes de o novo conteúdo estar inteiramente escrito** — a troca só
  acontece depois do `File.WriteAllText(tempPath, ...)` já ter terminado com sucesso.

**Diagnóstico ampliado em `PxLogger`**: agora grava também `HResult` (e `InnerHResult`) da
exceção, além do tipo e do stack trace que já gravava — nunca a mensagem da exceção nem o
conteúdo dos dados do usuário (mesma regra de sempre). `SaveSettingsOrShowError`/
`SettingsWindow`/`SizeConfigWindow` agora passam o caminho do arquivo (`relatedFilePath`,
nunca o conteúdo) para o log.

**Interface — alterações não persistidas continuam visíveis**: os 7 manipuladores de perfil de
trabalho em `ClassicWorkspaceWindow` (criar/renomear/duplicar/excluir/selecionar/salvar/
restaurar padrão) foram alterados para sempre chamar `RefreshWorkProfilesUi()` e mostrar o
resultado na tela MESMO quando `Save()` falha — a alteração que o usuário acabou de fazer
(já aplicada em `_settings` antes da tentativa de salvar) nunca é escondida/desfeita
silenciosamente só porque a gravação falhou. A mensagem de status/erro deixa explícito que a
mudança NÃO foi persistida ("... (NÃO salvo — veja o erro exibido)."), para nunca ser
confundida com "perfil salvo" ao reabrir — se o processo for fechado antes de uma gravação
bem-sucedida, a mudança realmente se perde (o arquivo em disco nunca chegou a ser tocado), e a
mensagem já avisa isso.

**Testes**: `tests/PXToolBox.Tests/AtomicFileWriterTests.cs` (novo, 5 testes) — primeiro
salvamento, atualização, bloqueio transitório real (`FileShare.None` numa `FileStream` de
verdade, liberado antes do orçamento de retentativas esgotar — sucesso), bloqueio persistente
além de todas as retentativas (lança, arquivo anterior preservado byte a byte, nenhum
temporário/backup órfão) e 8 threads chamando `WriteTextSafely` ao mesmo tempo para o mesmo
destino (nunca corrompe, nunca deixa um arquivo final ilegível). Todos usam um diretório
TEMPORÁRIO próprio — nunca o arquivo real de configuração, que outras classes de teste também
tocam (um teste anterior, com o bloqueio real contra o arquivo real, colidia com outras classes
sob a paralelização padrão do xUnit entre classes — corrigido movendo os testes para aqui,
contra `AtomicFileWriter` diretamente). `dotnet build` do host — 0 avisos/erros; `dotnet test`
do PXToolBox — 68/68 (net8.0) + 273/273 (net48, 273 = 268 anteriores + 5 novos), confirmado em
duas execuções completas consecutivas.

**O que permanece pendente**: nenhuma validação real dentro do CorelDRAW (criar/renomear/
duplicar/excluir perfis de verdade, fechar e reabrir, arquivo bloqueado/sem permissão de
verdade pelo sistema operacional real) — só a lógica de persistência foi validada
automaticamente, com bloqueios reais simulados por teste, nunca dentro do Corel real.

## 18. Preferências portáteis do PX Tool Box DEV (iteração 28)

Recurso novo, pedido separado dos perfis de trabalho do ListForge — ver
`Integrations/PortablePreferencesService.cs`. Distinto do botão já existente "Exportar/Importar
configurações..." em `SettingsWindow` (que copia o objeto `ListForgeIntegratedSettings` inteiro,
incluindo perfis de trabalho com caminhos específicos da máquina — útil para backup/restauração
completa, mas é exatamente o "copiar o arquivo interno integralmente" que este recurso novo foi
pedido para NÃO fazer).

**Campos realmente portáveis identificados** (nenhum inventado — "layout", "seções" e
"favoritos" citados no pedido do usuário não existem implementados nesta base de código — os
favoritos foram adicionados depois, na dev.50, ver seção 35):
`EditorFontSize` (já documentado no próprio tipo como preferência fora de qualquer perfil) e a
preferência de modo Clássico/PX por perfil — exportada por NOME do perfil, nunca pelo Id
interno (específico da máquina de origem); um perfil ausente na máquina de destino é só
reportado como ignorado, nunca cria perfil novo. Formato JSON próprio
(`formatId: "PxToolBoxDevPortablePreferences"`, `schemaVersion: 1`, camelCase nos nomes de
campo do esquema — mesma convenção de `release-manifest.json` — mas NUNCA nas chaves do
dicionário de preferência por perfil, que são nomes de perfil/dado do usuário com acentos;
`CamelCaseNamingStrategy(processDictionaryKeys: false, ...)` evita essa corrupção, confirmada
por teste dedicado).

**UI**: dois botões novos em "Preferências portáteis (entre computadores)", em
`SettingsWindow` — "Exportar preferências..."/"Importar preferências...". Importar valida
formato/esquema/tipos/intervalos (erros por campo nunca derrubam o arquivo inteiro — um campo
incompatível é só ignorado e relatado), mostra um resumo dos campos reconhecidos/incompatíveis
e exige confirmação antes de aplicar; salva um backup recuperável (mesmas preferências
exportáveis do estado ATUAL, formato próprio, na pasta de configuração) antes de substituir —
se o backup falhar, a importação é cancelada sem aplicar nada. Aplica só em
`_workingSettings` (igual ao recurso de configurações já existente) — ainda precisa de
"Salvar" para persistir, e usa a rotina de gravação já corrigida (`AtomicFileWriter`, iteração
27) quando isso acontece.

**Testes**: `PortablePreferencesServiceTests.cs` (14 testes) — exportação resolvendo Id→Nome,
nunca exporta Id, ignora entradas órfãs, round-trip de validação, formato/esquema
desconhecidos, JSON inválido, campo fora do intervalo (incompatível, não derruba o arquivo),
valor de modo desconhecido, aplicação nunca cria perfil novo nem toca campos locais do perfil
(pasta/formato de exportação), e preservação de maiúsculas/acentos em nome de perfil na
serialização.

## 19. Contrato OrderContext (Nexor → PX) — documentação (iteração 29)

Entrega exclusivamente documental — ver `docs/integrations/nexor-order-context.md` e
`docs/integrations/order-context-example.json`. Nenhum código, nenhuma build gerada para esta
parte.

**Achado principal**: investigação do código real do Nexor
(`C:\Projetos\ProjetoJocasta\core\printlogs_db.py`) mostrou que **não existe uma entidade de
"pedido de cliente"** com identificador estável/número/cliente/status — "pedido" é só uma
etiqueta de texto livre (`orders.pedido`, re-derivada por regex do nome do arquivo,
re-classificável) sobre uma linha de trabalho de impressão dentro de um ROLO (`rolls`, com `id`
estável de verdade). O contrato documentado usa o identificador real e estável
`(rollId, jobHash)` — nunca o texto livre isolado — e registra essa lacuna explicitamente
(número de pedido/cliente/status não existem hoje) em vez de inventar campos para o pedido do
usuário. Ver seção 0 do próprio documento do contrato para a investigação completa, com
caminhos/linhas reais.

## 20. Importação de contexto de pedido no PX (iteração 29)

Primeira recepção do contrato da seção 19 — `Integrations/OrderContextService.cs` (validação
pura) + `Production/OrderContextWindow.xaml(.cs)` (exibição, só leitura). Importação manual de
arquivo (seletor), nunca comunicação automática com o Nexor nesta entrega.

**Isolamento garantido por construção** (nunca por convenção só documentada): o serviço só lê o
JSON e devolve um objeto em memória; a janela só exibe campos e verifica (sem abrir)
`job.sourceFileReference` com `File.Exists` dentro de um `try/catch` que nunca lança — nenhuma
linha de código deste recurso toca `nexor.db`, chama o Nexor, abre/modifica um documento do
Corel, ou toca qualquer estado do ListForge. Funciona sem documento aberto (botão em "Produção"
do `ControlUI`, fora de `RunWithWorkspace`/`Connect`, mesmo padrão do ListForge Standalone/
Nexor). Nenhuma persistência em disco do contexto importado — só memória, mesmo se a janela for
reaberta via "Abrir ListForge Integrado" ou outro botão, o contexto não sobrevive ao fechamento
da janela.

A janela sempre mostra "Contexto recebido — não sincronizado com o Nexor" e a data/hora de
exportação convertida para o horário local — nunca como uma consulta em tempo real. Carregar
outro contexto ou limpar o atual sempre pede confirmação quando já há um contexto carregado
(nunca substitui silenciosamente).

**Testes**: `OrderContextServiceTests.cs` (12 testes) — exemplo válido completo, campos
opcionais ausentes, JSON inválido, formato/esquema desconhecidos, cada campo obrigatório
ausente isoladamente, tipo errado em `rollId`, e a etiqueta de classificação manual
reconhecida. A janela de exibição (WPF) não tem teste automatizado, mesma limitação já
documentada para as demais janelas deste host.

**O que permanece pendente**: nenhum smoke test real no CorelDRAW (botão "Carregar contexto de
pedido…", confirmação ao substituir, preservação da sessão do ListForge ao lado de um contexto
carregado). A exportação do lado do Nexor já foi implementada — ver seção 21.

## 21. Exportação do contexto de pedido no Nexor (repositório `ProjetoJocasta`, iteração 29)

Lado complementar da seção 20 — implementado no repositório real do Nexor
(`C:\Projetos\ProjetoJocasta`, Python/Flask/Tkinter), nunca no PXToolBox.

**Localização real investigada antes de codificar** (nunca presumida): a tela de seleção de
pedido é `modules/registros/ui.py`, classe `PXSearchOrdersUI` — aba "Pedidos" dentro de
"Detalhes do rolo selecionado". O `Treeview` de pedidos (`self.tree_orders`) inseria linhas sem
associar o `iid` ao `id` real da linha no banco — sem isso, não havia como saber qual pedido
estava selecionado para exportar. A consulta já existente (`get_roll_orders`) também não
selecionava `job_hash` (existe na tabela `orders`, confirmado no esquema real —
`core/printlogs_db.py`, `init_schema` — mas não era lido por essa consulta específica).

**Correção/adição, sempre aditiva** (nenhuma consulta/rotina compartilhada já existente foi
alterada, para não arriscar efeito colateral em outras telas):
- `core/printlogs_db.get_order_context(order_id)` — nova função, só leitura, uma consulta
  própria (`SELECT ... FROM orders o JOIN rolls r ON r.id = o.roll_id WHERE o.id = ?`) incluindo
  `job_hash`/`roll_name`/`machine`; `get_roll_orders` (usada por `_load_orders`) não foi tocada.
- `modules/registros/repository.load_order_context` — wrapper fino, mesmo padrão das demais
  funções desse arquivo.
- `modules/registros/px_export.py` (novo) — `build_order_context_document`/
  `write_order_context_document`/`export_order_context_to_file`. `pedidoManual` é
  deliberadamente OMITIDO do documento: é um campo que só existe em memória durante a
  classificação (`modules/operacao/models.py`, `Job.pedido_manual`) e nunca é persistido na
  tabela `orders` — nunca inventado como `False` aqui (confirmado grepando o repositório
  inteiro: nenhuma gravação desse campo no banco).
- `modules/registros/ui.py` — `self.tree_orders.insert(..., iid=str(order["id"]), ...)` (antes
  sem `iid` real); novo botão "Exportar contexto para PX..." na aba Pedidos (desabilitado até
  um pedido ser selecionado — `<<TreeviewSelect>>` em `self.tree_orders`); recusa exportar uma
  linha sem `pedido` classificado (ex.: um avanço/espaço), com aviso explicando por quê; destino
  escolhido por `filedialog.asksaveasfilename` (confirmação de sobrescrita nativa do sistema).

**Isolamento garantido por construção**: nenhuma linha de código deste recurso altera uma
coluna do pedido/rolo, chama o PX ou abre o CorelDRAW — só lê e grava um arquivo no caminho
escolhido pelo usuário. O Nexor continua funcionando sem o PX instalado (nenhuma importação
condicional nova).

**Testes reais executados** (Python, `pytest`, nunca simulados): `tests/test_px_export.py` (5
testes novos, banco SQLite TEMPORÁRIO isolado via `unittest.mock.patch` em `get_db_path` —
nunca o banco real configurado) — documento corresponde ao contrato, pedido inexistente retorna
`None` (nunca inventa), linha de espaço nunca inventa `fabric`/`tipo`, arquivo gravado é
idêntico ao documento, acentos/caracteres especiais no texto do pedido preservados. Suíte
completa do Nexor rodada depois — **125 passaram** (120 preexistentes + 5 novos), nenhuma
quebrada. `modules/registros/ui.py` confirmado importável sem erro (`python -c "import
modules.registros.ui"`) — não é smoke test da interface de verdade (Tkinter não tem suíte
automatizada neste repositório), só confirma que o módulo carrega.

**Compatibilidade real confirmada entre os dois repositórios** (não só por inspeção/
documentação): um arquivo gerado pelo exportador REAL do Nexor (mesmo código acima, contra um
banco de teste) foi copiado para o PXToolBox e validado pelo importador REAL
(`OrderContextService.Validate`) — aceito, todos os campos (rollId/rollName/machine/jobHash/
pedidoLabel/fabric/tipo/sourceFileReference) lidos corretamente. Arquivo e teste de depuração
usados só para essa prova foram removidos depois — não fazem parte da entrega.

**Documentação atualizada no repositório do Nexor**: `CHANGELOG.md` (entrada "[Não lançado]" —
nunca atribuída a um número de versão do Nexor, que não é deste repositório para decidir).
`README.md` desse repositório não foi tocado: o arquivo está com uma codificação que faz
`grep`/`cat` o tratarem como binário (uma peculiaridade pré-existente, não introduzida aqui) —
editá-lo sem conseguir inspecionar o resultado seria arriscar corromper o arquivo, então a
atualização ficou só no `CHANGELOG.md`, que é o mecanismo de documentação realmente ativo e
verificado neste repositório.

**O que permanece pendente**: nenhum smoke test real da interface Tkinter (clicar no botão de
verdade, confirmar o diálogo de salvar, abrir o arquivo gerado no PX de verdade dentro do
Corel); nenhuma build/instalador novo do Nexor foi gerado (fora do escopo pedido — "não gere
instalador" nunca foi pedido para o lado do Nexor nesta tarefa, e esse processo pertence ao
próprio time do Nexor).

## 22. Terminologia e identidade real de rollId/jobHash (iteração 30)

Base confirmada: `0.1.0-dev.31`, com o exportador real do Nexor e o importador real do PX já
testados juntos (seção 21). Esta rodada corrige dois pontos: o texto exibido ao usuário não
podia continuar sugerindo um "pedido"/"Job" comercial que o Nexor não tem, e a identidade real
de `rollId`/`jobHash` precisava ser investigada com evidência, não presumida.

**1. Terminologia — nunca "pedido" como identificador, nunca "Job" visível**:
- PX: botão "Carregar contexto de pedido…" → **"Carregar contexto de produção…"**;
  `OrderContextWindow` — título, cabeçalho e seção "Trabalho de impressão" (antes "Pedido
  (etiqueta livre...)"); mensagens de status "Contexto de pedido..." → "Contexto de
  produção...".
- Nexor: botão "Exportar contexto para PX..." → **"Exportar contexto de produção..."**;
  título do diálogo de salvar e todas as mensagens (`messagebox`) correspondentes.
- **Nomes técnicos preservados por compatibilidade** (pedido explícito do usuário): `formatId:
  "NexorOrderContext"`, nomes de classe (`OrderContextService`, `OrderContextWindow`,
  `px_export.py`) e nomes de campo (`rollId`, `jobHash`, `pedidoLabel`, `pedidoManual`) nunca
  foram renomeados — só o texto que o usuário vê.
- "Pedido informado" só aparece quando o dado realmente existe (`pedidoLabel` não-vazio — já
  garantido pela validação, que recusa um documento sem essa etiqueta).

**2. Identidade real de rollId/jobHash — investigado, não presumido** (ver
`docs/integrations/nexor-order-context.md`, seção 1-A, para o relato completo com citações de
código): `jobHash` é `sha1(machine|end_time|document|height_mm)` — determinístico a partir de
CONTEÚDO, nunca um UUID aleatório; estável ao reeditar um rolo (a rotina de edição recria as
linhas de `orders`, mudando o `id` interno, mas o `job_hash` recalculado dá o mesmo valor,
porque as entradas do hash não mudaram). `rollId` é estável para o mesmo rolo (a mesma rotina
faz `UPDATE rolls`, nunca recria a linha). **Não existe** operação de mover um trabalho entre
rolos no Nexor atual (confirmado buscando por qualquer `UPDATE orders SET roll_id` ou função de
mover/transferir em todo o repositório — nenhuma encontrada). **Conclusão confirmada**:
`(rollId, jobHash)` só é garantido único DENTRO da mesma base `printlogs.db` — nunca
globalmente (duas instalações diferentes do Nexor podem coincidentemente produzir o mesmo par).

**3. Menor evolução compatível implementada**: `source.installationId` (novo campo opcional)
— id curto e opaco (`uuid.uuid4().hex[:12]`), gerado uma única vez por instalação do Nexor e
persistido em `%APPDATA%\PXCore\config.json` (`core/config.py`,
`PXCoreConfig.installation_id` + `get_installation_id()` — campo aditivo, instalações
existentes sem ele recebem um na primeira exportação, sem nenhuma ação manual). Nunca uma
garantia formal de unicidade global — só um auxiliar de desambiguação, documentado como tal em
todos os lugares onde aparece (contrato, janela do PX, docstring do exportador). Arquivos
exportados ANTES desta mudança continuam sendo aceitos (campo ausente — nunca um erro; ver
`OrderContextService.OrderContext.InstallationId`, nullable).

**Testes**: lado PX — `OrderContextServiceTests` atualizado (12 testes, incluindo
`InstallationId` presente e ausente). Lado Nexor — `tests/test_config_installation_id.py`
(novo, 4 testes: gera e persiste na primeira chamada, nunca regenera, sobrevive a um reload de
disco, e uma instalação já existente sem o campo recebe um sem perder os demais) e
`tests/test_px_export.py` (+1 teste: falha ao gerar/persistir o id nunca derruba a exportação,
só omite o campo). Suíte completa do Nexor: **130 passaram** (125 anteriores + 5 novos).
`dotnet test` do PXToolBox: **68/68 (net8.0) + 299/299 (net48)**.

**Compatibilidade real confirmada de novo, com o esquema atualizado**: um arquivo gerado pelo
exportador real do Nexor (já incluindo `installationId`) foi validado com sucesso pelo
importador real do PX — `InstallationId` lido corretamente. Arquivo e teste de depuração
usados só para essa prova foram removidos depois.

**Documentação atualizada**: `docs/integrations/nexor-order-context.md` (seção 1-A nova, seção
2 com `installationId`, seção 6 — decisão de nomenclatura marcada como resolvida) e o exemplo
JSON correspondente.

**O que permanece pendente**: nenhum smoke test real das interfaces (PX dentro do CorelDRAW,
Nexor de verdade); nenhuma nova build/instalador do Nexor gerada (fora do escopo). A validação
real no Corel, a aceitação da `.26` e os demais casos adiados continuam intocados.

## 23. installationId não resolve o escopo da base — databaseId implementado (iteração 31)

A seção 22 implementou `source.installationId` para ajudar a desambiguar `(rollId, jobHash)`
entre bases Nexor diferentes. Esta rodada CONFERIU (não presumiu) se esse campo realmente
cobre o escopo real — e descobriu que não, em dois sentidos opostos:

**Cenários testados de verdade** (`ProjetoJocasta/tests/test_database_identity.py`, 6 testes
novos, todos passaram):
- Uma instalação com `base_dir` apontando para duas bases `.db` diferentes → MESMO
  `installationId`, bases genuinamente diferentes — `installationId` não distingue.
- `base_dir` numa pasta de rede compartilhada entre máquinas (cenário REAL, não hipotético —
  é o motivo documentado da escolha de `journal_mode = DELETE` em
  `core/printlogs_db.connect()`) → `installationId` DIFERENTE por máquina para a MESMA base —
  sugeriria falsamente "origens diferentes".
- Copiar só `%APPDATA%\PXCore\config.json` para outra máquina (sem copiar a base) → MESMO
  `installationId`, bases DIFERENTES — `installationId` pode ser duplicado sem relação com a
  base real.

**Correção implementada**: `source.databaseId` (novo, opcional, aditivo — schemaVersion
permanece 1) — gravado DENTRO do próprio arquivo `printlogs.db` (nova tabela `db_identity`,
`core/printlogs_db.get_database_id`, rowid fixo + `INSERT OR IGNORE` para resistir a uma
corrida de duas instalações criando a base simultaneamente — testado
`test_corrida_na_primeira_criacao_converge_no_mesmo_id`). Por viajar COM o arquivo:
- Cópia/restauração da base noutra máquina → MESMO `databaseId` (testado, bytes copiados).
- Duas instalações na mesma base compartilhada → MESMO `databaseId` (testado, duas conexões
  SQLite independentes ao mesmo arquivo).
- Uma instalação, duas bases → `databaseId` DIFERENTE (testado).

`installationId` PERMANECE no contrato (não removido — continua útil como contexto "qual
máquina exportou"), mas sua documentação (nesta seção, em `nexor-order-context.md` seção 1-B,
e nos comentários de `OrderContextService.cs`) foi corrigida para não sugerir mais uma garantia
que não existe. **Nenhum dos dois campos é identificação global formal** — ambos são sinais de
melhor esforço, só para exibição ao humano (ver seção 1-B do contrato para a justificativa de
por que uma solução mais rigorosa, como hashing de conteúdo da base, não foi implementada —
não há necessidade concreta hoje, só visualização).

**PX**: `OrderContextService.OrderContext.DatabaseId` (opcional, nullable — ausente em
arquivos antigos é só "não identificado", nunca erro). `OrderContextWindow` mostra os dois
campos com textos que não overclaimam. Novo teste
`Validate_SameRollIdAndJobHash_DifferentSource_KeepsDistinctIdentifiers` confirma que dois
contextos com `(rollId, jobHash)` iguais mas `installationId`/`databaseId` diferentes NUNCA são
tratados como o mesmo contexto pelo importador.

**Testes**: Nexor — suíte completa **136 passaram** (130 anteriores + 6 novos), executada duas
vezes (mesmo resultado nas duas). PX — suíte completa **68/68 (net8.0) + 300/300 (net48)**,
executada três vezes; numa das repetições, `SupportPackageServiceTests.
Generate_NeverIncludesListContentOrInputBackups` falhou isoladamente com um erro de I/O em
`Directory.Delete` — reconfirmado em isolamento (6/6, sem relação com nenhuma mudança desta
tarefa) como o mesmo travamento intermitente já documentado (seção 10/ROADMAP, causa ainda não
determinada) — não uma regressão desta entrega. Compatibilidade real exportador→importador
reconfirmada com o esquema atualizado (arquivo real do Nexor, incluindo `installationId` e
`databaseId`, validado com sucesso pelo importador real do PX).

**O que permanece pendente**: a causa raiz do travamento intermitente em
`SupportPackageServiceTests` continua não determinada (preexistente, fora do escopo desta
tarefa). Smoke tests reais (Corel, interface Tkinter do Nexor) continuam pendentes.

## 24. Carregar contexto de produção colando o caminho (iteração 32)

Complementa o seletor de arquivo existente (seção 20) com uma segunda forma de carregar o
mesmo contexto: colar diretamente o caminho do `.json`, útil junto com a ação "Copiar caminho"
do Nexor (ver `docs/integrations/nexor-order-context.md`, seção 2 — essa ação do Nexor foi
implementada numa entrega anterior; não presumida aqui, só reaproveitada).

**Implementado**: `Integrations/OrderContextPathLoader.cs` (lógica pura, sem WPF — normaliza o
caminho, trata caminho vazio/diretório/inexistente/ilegível, e chama o MESMO
`OrderContextService.Validate` usado pelo seletor — nunca uma segunda validação divergente) e
`Production/OrderContextPathDialog.xaml(.cs)` (janela pequena com campo "Caminho do contexto" e
botão "Carregar" — `ThreadKeyboardRouter`, mesmo padrão de `WorkProfileNameDialog`, sem hooks
globais). Novo botão "Colar caminho do contexto…" em `ControlUI.xaml`, ao lado do seletor
existente — mesmo comportamento de "já carregado → só ativa a janela" do seletor, por
consistência (o reaproveitamento do seletor existente foi mantido, não substituído).

**Tratamento garantido** (testado, não só implementado): caminho vazio/só espaços; caminho
inexistente; caminho que é uma PASTA (mensagem distinta de "não encontrado"); arquivo ilegível
(simulado com um lock exclusivo real de arquivo, sem depender de ACLs frágeis em CI); JSON
incompatível (mesmo `FailureReason` do validador); aspas externas copiadas do Windows
("Copiar como caminho") removidas só quando o par completo envolve o caminho inteiro — nunca
uma aspa solta/desbalanceada; espaços/acentos no caminho preservados intactos. Um texto com
aparência de URL (`https://...`) nunca tenta acesso de rede — só falha como "não encontrado",
igual a qualquer caminho inexistente, porque o carregador só usa `File.Exists`/
`Directory.Exists`/`File.ReadAllText`, nunca `Process.Start` nem qualquer cliente HTTP.

**Nunca persiste o caminho**: o campo é só texto em memória dentro da janela — nada grava esse
valor em disco/configuração (pode identificar arquivos de produção). Colar o texto nunca
dispara o carregamento por conta própria — só o clique em "Carregar" lê o arquivo; nada lê a
área de transferência automaticamente ao abrir a janela.

**Preservação do contexto atual em falha**: a cauda que efetivamente troca o conteúdo da
janela de contexto (`ControlUI.DisplayLoadedOrderContext`) só é chamada depois que o diálogo
retorna com sucesso (`ShowDialog() == true`) — uma falha de validação mantém a janelinha aberta
com o erro, nunca chega a essa cauda, então o contexto já carregado (se houver) nunca é tocado.

**Testes**: `OrderContextPathLoaderTests` (novo, 13 testes) — cobre todos os cenários acima,
inclusive um teste de equivalência explícito confirmando que carregar por caminho produz o
MESMO contexto que validar o mesmo conteúdo diretamente (como o seletor de arquivo já fazia).
Suíte completa: **68/68 (net8.0) + 313/313 (net48)**, executada duas vezes (mesmo resultado).

**O que permanece pendente (depende de teste real na interface)**: clique real no botão
"Colar caminho do contexto…" dentro do CorelDRAW, colagem via Ctrl+V sem acionar atalhos do
Corel (o `ThreadKeyboardRouter` é o mesmo mecanismo já usado por outras janelas, mas o
comportamento dentro do processo real do CorelDRAW não foi reconfirmado nesta tarefa), e a
confirmação visual de "equivalência com o seletor existente" vista por um humano, não só por
teste automatizado.

## 25. Correção de Tamanho Padrão na guia Grids (iteração 33)

Base: duas inspeções reais do usuário com `ShapePropertiesInspectorService` (um grupo dentro
de PowerClip e um retângulo simples), ambas mostrando o MESMO padrão: `Count: 2`, índice [0]
com erro "objeto não encontrado", índice [1] com `KEEP_DESIGN_SIZE, ID 0, Value: True` — e
nada mais visível.

**Causa raiz confirmada (não presumida)**: `IVGProperties.ItemByIndex`/`Description` são
**1-based**, não 0-based. Confirmado por:
1. As duas inspeções reais (índice 0 sempre falha; índice 1 sempre lê a primeira entrada).
2. Reflexão sobre `Corel.Interop.VGCore.dll` real (`IVGProperties` tem `get_ItemByIndex(Int32
   Index)`, `Description(Int32 Index, ...)`, `Count()`, `DeleteAll(String Name)` —
   confirmando a existência/assinatura dos membros, embora a convenção de indexação em si só
   seja confirmável em tempo de execução).
3. `CorelCollections.cs` — já estabelecido no próprio código deste projeto — usa
   `for (var i = 1; i <= native.Count; i++)` pra TODAS as demais coleções VGCore
   (Shapes/Layers/Pages). O inspetor e `CorelShapeAdapter.DeleteProperties` eram as ÚNICAS
   exceções, usando 0-based por engano.

**Consequência**: o laço antigo (`0` a `Count-1`) desperdiçava uma tentativa no índice
inválido 0 e NUNCA alcançava a última entrada real — se a largura (ID 1) estivesse de fato
gravada, o inspetor nunca mostraria. Isso explica "não registra as dimensões" como possível
sintoma do INSPETOR, não necessariamente da escrita — ainda não demonstrado de qual lado
estava a falha original, por isso o pedido ao usuário de reinspecionar com o inspetor já
corrigido (ver pendências abaixo).

**Corrigido**:
- `ShapePropertiesInspectorService.BuildReport`: laço agora `1` a `count` (inclusive).
- `CorelShapeAdapter.DeleteProperties`: reescrito para usar `IVGProperties.DeleteAll(name)`
  (método nativo, confirmado por reflexão) em vez de enumerar manualmente — elimina de vez a
  ambiguidade de indexação no caminho de remoção. Antes, essa mesma enumeração 0-based lançava
  sem captura na primeira chamada a `Description(0, ...)`, explicando "não remove a marcação
  existente ao alternar": a exceção propagava (capturada só em `RunWithWorkspace`, que mostra
  "falha inesperada" em vez de remover qualquer coisa).

**Ainda NÃO confirmado** (contrato exato de largura/altura): nenhuma inspeção real, mesmo
depois da correção, mostrou o conteúdo da segunda entrada até o momento desta entrega. O
código mantém `WidthId = 1` (herdado, não uma declaração de compatibilidade com o SISBolt
real) — `StandardSizeService` documenta essa lacuna explicitamente na própria classe. Build
`0.1.0-dev.35` foi gerada especificamente com a correção do inspetor, antes do restante desta
entrega, para permitir uma reinspeção real rápida — pode estar superada pela build final desta
rodada (mesma correção, mais tudo abaixo).

**Migração de marcação antiga (seção 2 da tarefa)**: `StandardSizeService.Inspect` classifica
cada Shape em `Absent` / `Complete` / `Incomplete` / `Unknown` (nunca finge Absent numa leitura
que falhou). `HasStandardSize` trata `Complete` e `Incomplete` como "marcado" (compatibilidade
com o uso existente). `Alternar`: se TODA a seleção já tem alguma marcação (mesmo incompleta),
desativa tudo, removendo a entrada antiga por completo (nunca deixa resíduo — o usuário pode
clicar de novo para marcar do zero com o contrato atual). Numa seleção mista, ativar migra
explicitamente qualquer `Incomplete` (remove a entrada antiga, depois escreve o conjunto
completo — nunca duplicata). `Atualizar`: nunca remove; migra `Incomplete` do mesmo jeito e
informa na mensagem quando houve reparo. Nenhuma varredura do documento inteiro — só os Shapes
da seleção atual.

**Integridade (seção 5)**: `WriteStandardSize` lê a largura ANTES de escrever qualquer coisa
(uma falha de leitura nunca escreve nada); se a segunda escrita falhar depois da primeira já
ter sido aplicada, a primeira é desfeita (melhor esforço — não há commit atômico de várias
entradas em `Shape.Properties`) em vez de deixar uma marcação parcial. A exceção original
sempre sobe — `RunWithWorkspace` já captura e nunca anuncia sucesso nesse caso.
`BeginCommandGroup`/`EndCommandGroup` (Undo) já existiam e continuam cobrindo as duas
operações — nenhuma mudança necessária aí.

**Detecção automática e estado do botão (seção 4)**: novo
`StandardSizeService.InspectSelection` classifica a seleção inteira em `Unavailable` /
`AllMarked` / `NoneMarked` / `Mixed` / `Unknown`. `ControlUI` ganhou `StandardSizeStateText`
(texto, nunca só cor) mostrando o estado, atualizado: ao conectar/reconectar, depois de cada
operação (`RunWithWorkspace`) e a cada evento real `DIVGApplicationEvents.SelectionChange`
(confirmado por reflexão sobre o VGCore real — evento sem parâmetros, quem recebe reconsulta a
seleção). Subscription nunca duplicada: `SetApp` é o ÚNICO ponto que troca `_app`, sempre
desinscrevendo do app anterior antes; `Loaded`/`Unloaded` do `UserControl` reinscrevem/
desinscrevem se o host escender/reexibir o painel sem destruir a instância. Nenhum polling.
Grupo selecionado nunca confundido com os filhos (Shapes de grupo) nem com o conteúdo de
PowerClip (`PowerClipShapes`, coleção separada) — nenhuma recursão em nenhum dos dois; uma
seleção com vários objetos soltos também nunca é tratada como "um grupo".

**Testes**: `StandardSizeServiceTests` +17 novos (Inspect/InspectSelection em todos os
estados, migração em Toggle/Update, rollback de escrita parcial, PowerClip sem recursão,
seleção múltipla sem confusão com grupo) — suíte completa **85/85 (net8.0) + 330/330
(net48)**, executada duas vezes. A travessia COM real (indexação, evento SelectionChange,
threading) permanece não-testável por automação — só confirmável no Corel real (ver roteiro
abaixo).

**Roteiro de validação real no Corel** (nenhum destes foi executado nesta entrega — só
compilação e testes automatizados, nunca confundidos com smoke test real):
1. Reinspecionar (com o inspetor corrigido) o MESMO objeto das evidências originais — confirmar
   se a segunda entrada aparece e o que ela contém (nome/ID/tipo/valor).
2. Marcar um objeto novo, atualizar com tamanho diferente, desmarcar — inspecionar antes/depois
   de cada passo.
3. Selecionar um objeto com a marcação antiga (ID 0 só) e clicar Alternar — confirmar remoção
   completa (sem resíduo) e, numa seleção mista com um objeto ausente, confirmar migração.
4. Testar o estado do botão: nenhuma seleção, seleção toda marcada, toda desmarcada, mista —
   confirmar que o texto reflete cada caso sem precisar reabrir o painel.
5. Grupo e PowerClip: marcar o contêiner, confirmar que os filhos/conteúdo recortado não foram
   tocados.
6. Desfazer (Ctrl+Z) depois de Alternar/Atualizar em múltiplos objetos — confirmar que desfaz
   tudo de uma vez.
7. Salvar, fechar, reabrir o documento — inspecionar de novo. Copiar o objeto marcado pra outro
   documento — inspecionar lá.
8. Fechar e reabrir o painel do PX — confirmar que o estado do botão continua atualizando
   (sem subscription duplicada, sem exceção ao reconectar).
9. **Marcar um objeto com Tamanho Padrão E outras propriedades SISBolt reais no mesmo objeto
   (ex.: alinhamento, objeto base, mão livre, esticar — o que existir de verdade) — inspecionar
   antes, clicar Alternar pra desativar, inspecionar depois. Confirmar que SÓ as entradas
   `KEEP_DESIGN_SIZE` desapareceram — todas as outras intactas.** Repetir migrando uma
   marcação antiga (ID 0 só) no mesmo objeto, confirmando a mesma preservação.

**Correção (revisão desta rodada)**: a versão anterior desta seção trocou a remoção seletiva
por `Shape.Properties.DeleteAll(nome)`, só porque a reflexão sobre o VGCore real confirmou a
ASSINATURA do método — nunca o comportamento em tempo de execução (se "Name" de fato filtra,
ou se o parâmetro é ignorado e a chamada apaga TODAS as propriedades do Shape). Sem Corel real
pra confirmar, essa suposição era arriscada demais — revertida para exclusão seletiva por
(nome, ID), usando os pares exatos que a própria enumeração (`Description`, agora 1-based)
encontra, nunca "apagar tudo com esse nome" sem verificação de ID. O passo 9 do roteiro acima é
a prova real que falta pra confirmar que `CorelShapeAdapter.DeleteProperties` nunca afeta
propriedades não relacionadas — os testes automatizados (`Toggle_Deactivate_
RemovesOnlyStandardSizeEntries_PreservesOtherSISBoltMarkings`,
`Toggle_MigrationOfLegacyId0Marking_PreservesOtherSISBoltMarkings`, novos nesta rodada) provam
só a camada de LÓGICA (`StandardSizeService`) com `FakeShape` — que já era seletivo por
construção (dicionário em memória, nunca exercitou o risco real da enumeração COM) — nunca a
travessia COM real em si.

**Contrato CONFIRMADO (reinspeção real pós-correção)**: nova inspeção real, já com o inspetor
1-based, mostrou `Count: 3` com os três índices lidos com sucesso — `[1] KEEP_DESIGN_SIZE, ID
0, Boolean, True`; `[2] KEEP_DESIGN_SIZE, ID 1, Double, 8.01282` (igual ao `Width` do objeto
no relatório); `[3] KEEP_DESIGN_SIZE, ID 2, Double, 7.21154` (igual ao `Height`). Usuário
confirmou explicitamente: "id 1 é largura e id 2 é altura". `StandardSizeService` ganhou
`HeightId = 2`; `RequiredIds` agora inclui os três; `WriteStandardSize` grava os três (lê
largura E altura antes de escrever qualquer coisa; se a escrita da altura falhar depois do
flag e da largura já aplicados, os dois são desfeitos — não só o mais recente). A infra de
migração (`Inspect`/`Incomplete`) já existia de forma genérica em torno de `RequiredIds` —
nenhuma marcação feita por QUALQUER build anterior desta mesma tarefa (que só gravava
flag+largura, nunca altura) tinha o conjunto completo; essas marcações agora são corretamente
classificadas como `Incomplete` e migradas automaticamente na próxima interação (Alternar ou
Atualizar), sem precisar de nenhum código de migração específico para esse caso — o mecanismo
genérico já cobre.

**Testes**: `StandardSizeServiceTests` +5 novos/atualizados especificamente pra altura
(criação com largura+altura, rollback no terceiro write, migração de uma marcação só com
flag+largura sem altura) — todas as asserções de escrita existentes passaram a conferir
altura também.

## 26. Bug de unidade confirmado: Shape.Width/Height sempre em polegadas (iteração 34)

Usuário inspecionou o MESMO objeto físico (7,77244 × 8,33333 cm) duas vezes: uma marcação
feita pelo PX e outra pelo SISBolt real. Resultado:

| | ID 1 (largura) | ID 2 (altura) |
|---|---|---|
| SISBolt | `7.77244` | `8.33333` |
| PX (antes desta correção) | `3.0600157480315` | `3.28083858267717` |

`7.77244 / 3.0600157480315 = 2,54` — exatamente o fator polegada→cm. **Confirmado**:
`ICorelShape.Width`/`Height` (`Shape.SizeWidth`/`SizeHeight` no VGCore real) retornam SEMPRE
em polegadas, independente da unidade de régua do documento — a afirmação anterior desta
seção ("segue a unidade de medida atual do documento") estava **errada**, baseada em
suposição, nunca em evidência. O SISBolt converte pra unidade atual antes de gravar; o PX não
convertia.

**Corrigido**: `StandardSizeService` agora exige um `DocumentUnit?` (mesmo tipo já usado por
`MeasurementSumService`) em todo método que ESCREVE (`SetStandardSize`, `UpdateStandardSize`,
`Toggle`, `UpdateSelection`) e converte explicitamente via `DocumentUnitConversion` (mesmo
helper reaproveitado, nunca uma segunda tabela de fatores) antes de gravar. Unidade não
reconhecida (`null`) falha com mensagem clara (`"Unidade do documento não reconhecida —
Tamanho Padrão não foi aplicado."`) em vez de gravar um valor errado silenciosamente — mesmo
padrão já estabelecido em `MeasurementSumService` ("Unidade não reconhecida. Por favor, use
mm, cm ou m."). Desativar (remover) nunca precisa da unidade — só apaga entradas existentes,
nunca escreve um valor novo — por isso continua funcionando mesmo com unidade desconhecida.
`ControlUI.OnToggleStandardSize`/`OnUpdateStandardSize` passaram de `RunOnSelection` para
`RunWithWorkspace` (para ter acesso a `doc.Unit`, não só à seleção).

**Achado colateral importante, FORA do escopo desta tarefa — não corrigido aqui**:
`MeasurementSumService` ("Somar Medidas dos selecionados") tem a mesma estrutura de risco —
`ControlUI.xaml.cs` constrói `new ShapeSize(s.Width, s.Height)` diretamente de
`ICorelShape.Width/Height` (sempre polegadas, agora confirmado) e depois multiplica por
`DocumentUnitConversion.ToMetersFactor(doc.Unit)`, que assume o valor de entrada JÁ está na
unidade do documento. Se o documento usar qualquer unidade diferente de polegada, o total
reportado por "Somar Medidas" está provavelmente errado. **Não verificado nem corrigido nesta
entrega** (fora do escopo pedido, que era só Tamanho Padrão) — reportado ao usuário
explicitamente; precisa de confirmação real e de uma tarefa própria.

**Testes**: `StandardSizeServiceTests` +6 novos — reprodução exata da evidência real (valores
em polegadas convertidos pra cm, comparados com tolerância de 4 casas decimais), identidade
quando a unidade é polegada, e falha sem escrita quando a unidade é desconhecida (ativar e
atualizar falham; desativar continua funcionando). Suíte completa reexecutada depois da
correção (ver versão final abaixo).

**Não declarado compatível com o SISBolt real em 100%** — o que foi demonstrado por evidência
real (nome, flag ID 0, largura ID 1 convertida, altura ID 2 convertida, todos os tipos, e
agora a unidade) está confirmado. O que ainda não foi testado: documentos com unidade
diferente de cm/polegada (mm, m, pé, jarda — a tabela de `DocumentUnitConversion` cobre essas,
mas nenhuma foi exercitada com evidência real do Corel).

## 27. Segundo bug de unidade: Document.Unit era a fonte errada (iteração 35)

A correção da seção 26 convertia pra `Document.Unit` (a unidade configurada no arquivo). O
usuário testou de novo num documento real, confirmado configurado em mm — o resultado gravado
pelo PX (já com a correção da seção 26, build e CorelDRAW reiniciados depois de instalar)
continuou em polegada. O próprio usuário deu a pista decisiva: **"o tamanho padrão do sisbolt
funciona independente da unidade configurada no arquivo"**.

Reflexão sobre `Corel.Interop.VGCore.dll` confirmou: `IVGApplication.Unit` e `IVGDocument.Unit`
são **duas propriedades REAIS e DIFERENTES** (`Document.Unit` é "a unidade configurada no
arquivo" — o que o usuário disse que o SISBolt NÃO usa; `Application.Unit` é uma preferência
do APLICATIVO, independente do arquivo aberto — exatamente a descrição do usuário). A correção
anterior usava a fonte errada.

**Corrigido**: nova propriedade `ICorelWorkspace.ApplicationUnit` (`CorelWorkspaceAdapter`,
lê `Application.Unit` real) — `ControlUI.OnToggleStandardSize`/`OnUpdateStandardSize` passaram
a usar `ws.ApplicationUnit` em vez de `doc.Unit`. Mapeamento `cdrUnit` → `DocumentUnit`
extraído para `CorelUnitMapping` (compartilhado entre `CorelDocumentAdapter` e
`CorelWorkspaceAdapter`, nunca duplicado). `StandardSizeService` em si não mudou — já recebia
um `DocumentUnit?` genérico; só a FONTE de onde `ControlUI` lê esse valor mudou.

**Consistência com a evidência anterior (seção 26)**: no teste do objeto de 7,77244 cm, o
`Application.Unit` da sessão coincidia com `Document.Unit` (cm) — por isso aquele teste não
revelou esta segunda camada do bug; só apareceu agora, num documento onde as duas preferências
(aplicativo vs. arquivo) genuinamente divergem (arquivo em mm, aplicativo em polegada).

**Nenhum teste automatizado novo** nesta correção — `StandardSizeServiceTests` já cobre a
lógica de conversão em si (que não mudou); a única mudança foi QUAL fonte de unidade
`ControlUI` passa pro serviço, e isso só é verificável com o Corel real (`ICorelWorkspace` não
tem fake nos testes — nenhum teste exercitava essa integração antes, nem depois).

**Pendente**: reconfirmação real do usuário, agora com `Application.Unit`, no mesmo documento
mm onde o bug foi demonstrado.

## 28. Terceiro bug de unidade: nem Document.Unit nem Application.Unit — ToUnits nativo (iteração 36)

O usuário reconfirmou com a build da seção 27 (`Application.Unit`) em outro documento (cm) —
o PX continuou gravando a polegada bruta. `Application.Unit` tampouco era a fonte certa.

Em vez de arriscar uma TERCEIRA propriedade de unidade adivinhada, trocado para o método
nativo de conversão do próprio Corel: `IVGDocument.ToUnits(Double Value, cdrUnit FromUnit)`
(confirmado por reflexão sobre o VGCore real) — converte um valor FROM a unidade informada
PARA o que o Corel considera a unidade "atual" NESTE momento, por conta própria, sem o PX
precisar ler e interpretar nenhuma propriedade isolada (`Document.Unit` ou `Application.Unit`
podem não refletir sozinhas o que o seletor de unidade da barra de status está usando agora).

**Implementado**: `ICorelDocument.ConvertFromInches(double)` (nova), implementada como
`Native.ToUnits(inches, cdrUnit.cdrInch)`. `StandardSizeService` trocou o parâmetro
`DocumentUnit?` por `Func<double, double>?` — o serviço nunca decide COMO converter, só aplica
o delegate recebido (nulo = conversão indisponível, falha com mensagem clara, mesma garantia
de antes). `ControlUI.OnToggleStandardSize`/`OnUpdateStandardSize` passam
`doc.ConvertFromInches` diretamente. `ICorelWorkspace.ApplicationUnit` (da seção 27) removido
— superado, não usado em lugar nenhum. `CorelUnitMapping`/`ICorelDocument.Unit` permanecem
(agora só para exibição/diagnóstico — ver seção seguinte), não para a conversão real.

**Testes**: `StandardSizeServiceTests` — todos os `DocumentUnit.Inch` trocados por um
`Identity` (`inches => inches`, representa "a conversão não muda nada" — o que os testes de
lógica realmente precisam, independente de qual conversão real seria usada); o teste de
reprodução da evidência passou a injetar `inches => inches * 2.54` diretamente, sem depender
de nenhum enum de unidade. `FakeDocument.ConvertFromInchesFunc` (nova, identidade por padrão).
Suíte completa reexecutada (ver versão final abaixo).

## 29. Melhorias no relatório do Inspecionar Shape.Properties (iteração 36)

Pedido explícito do usuário depois de toda a investigação das seções 26-28 ter exigido várias
idas e voltas só pra descobrir a versão instalada e qual unidade cada preferência do Corel
estava usando em cada teste.

**Implementado**: novo `ShapeDiagnosticContext` (`ToolVersion`, `DocumentName`,
`DocumentUnitLabel`, `ApplicationUnitLabel`) em `ShapePropertiesReport.Diagnostics` —
preenchido em TODO caminho de retorno de `ShapePropertiesInspectorService` (inclusive sem
documento/seleção, nunca só no caminho de sucesso), melhor esforço (nunca lança, campos vazios
quando não é possível ler). `ShapePropertiesReportFormatter.Build` imprime um cabeçalho com
versão (sempre), e documento/unidades só quando disponíveis (nunca uma linha "Documento: "
vazia e confusa). Mostra EXPLICITAMENTE as duas unidades (Document.Unit e Application.Unit)
com o rótulo técnico `cdrXxx` ao lado — ambas já comprovadamente NÃO são a unidade real usada
pelo Tamanho Padrão do SISBolt (seções 26-28), mas continuam úteis como contexto de
diagnóstico para eliminar essa hipótese rapidamente em investigações futuras, sem precisar
perguntar de novo. Corrigido de passagem: o comentário de `ShapePropertyEntry.Index` ainda
dizia "0-based" — já estava 1-based desde a seção 25, só o comentário estava desatualizado.

**Testes**: `ShapePropertiesReportFormatterTests` +2 novos (cabeçalho completo com os dois
rótulos de unidade; campos vazios omitidos sem gerar linhas em branco).

## 30. Quarto bug de unidade (e correção final): Document.Rulers.HUnits/VUnits (iteração 37)

A build da seção 28 (`Document.ToUnits`) foi instalada e testada num smoke test real: num
documento em cm ("Sem título-1", régua e barra de propriedades mostrando "7,612 cm" /
"7,452 cm"), o `KEEP_DESIGN_SIZE` ID1/ID2 gravados batiam EXATAMENTE, byte a byte, com
`shape.Width`/`Height` brutos em polegadas — ou seja, nenhuma conversão aconteceu.
`Document.ToUnits` usa `Document.Unit` internamente, então isso só é possível se
`Document.Unit` não fosse realmente cm neste documento.

Confirmado com o relatório melhorado da seção 29: `Document.Unit` e `Application.Unit`
reportaram `polegada (cdrInch)` para este documento — mesmo depois do usuário salvar o
arquivo (`teste unidade do documento.cdr`) e repetir o teste, o que descarta a hipótese de
"unidade do documento novo ainda não sincronizada". As DUAS propriedades de unidade
tentadas nas seções 27 e 28 (que dependem de `Document.Unit`) são, de fato, uma fonte errada
para este cenário real — confirmado, não presumido.

Em vez de adivinhar uma quarta propriedade isoladamente, levantado por reflexão TODO membro
contendo "Unit" em TODA a `Corel.Interop.VGCore.dll` (`IVGDocument`, `IVGApplication`,
`IVGDimension*`, `IVGPageSize`, `IVGRulers`, `IVGStructCreateOptions`). Resultado novo:
`IVGRulers.HUnits`/`VUnits : cdrUnit`, acessível via `Document.Rulers` (confirmado por
reflexão: `IVGDocument.Rulers : Rulers`). Testado no MESMO documento (antes e depois de
salvar): `Document.Rulers.HUnits`/`VUnits` reportaram `centímetro (cdrCentimeter)` —
batendo exatamente com o que a régua/barra de propriedades mostra, ao contrário de
`Document.Unit`/`Application.Unit`.

**Implementado**: `ICorelDocument.ConvertFromInches` trocado de `Native.ToUnits(inches,
cdrInch)` para `Native.Application.ConvertUnits(inches, cdrUnit.cdrInch,
Native.Rulers.HUnits)` — `IVGApplication.ConvertUnits(Value, FromUnit, ToUnit)` (confirmado
por reflexão) recebe origem E destino explicitamente, sem depender de nenhum estado ambiente
(nem `Document.Unit`, nem `Application.Unit`), ao contrário de `Document.ToUnits`/`FromUnits`.
Usa `Rulers.HUnits` (unidade horizontal) tanto para largura quanto para altura — nenhuma
evidência real até agora mostrou `HUnits`/`VUnits` divergindo (a UI expõe um único seletor de
unidade para W/H), então não foi criada uma segunda função de conversão sem necessidade
concreta.

**Build intermediária só de diagnóstico** (0.1.0-dev.42, antes da correção): adicionadas as
linhas `Unidade da régua horizontal/vertical (Document.Rulers.HUnits/VUnits)` ao relatório do
Inspecionar Shape.Properties, para confirmar a hipótese com evidência real ANTES de trocar a
lógica de gravação — mesma disciplina das seções 27/28, desta vez sem precisar de uma
tentativa "às cegas".

**Testes**: `ShapePropertiesReportFormatterTests` +1 novo (`RulersHUnitLabel`/
`RulersVUnitLabel` aparecem no relatório quando presentes; omitidos quando vazios — incluído
no teste de diagnóstico vazio já existente). `StandardSizeService`/`CorelDocumentAdapter` não
exigiram mudança de teste automatizado: a conversão real em si só é exercida dentro do Corel
(sem Corel real em CI), e a lógica de negócio do serviço já era testada via o delegate
`Func<double,double>` (arquitetura da seção 28), que não muda com a troca de QUAL conversão
nativa o adaptador usa por baixo.

**Confirmado por smoke test real** (0.1.0-dev.43, mesmo documento "teste unidade do
documento.cdr", mesmo objeto StaticID 4): `KEEP_DESIGN_SIZE` ID1 = 7,61218 e ID2 = 7,45192 —
exatamente `shape.Width/Height` brutos (2,99692125984252 / 2,93382677165354 polegadas)
multiplicados por 2,54, batendo com os 7,612 × 7,452 cm mostrados pela régua. As três
tentativas anteriores (seções 26-28) pareciam corretas na revisão estática e só foram
refutadas por evidência real dentro do Corel — esta foi a primeira confirmada da mesma forma,
não só presumida correta.

## 31. Falhas intermitentes da suíte — três causas raiz reais, não "flakiness ambiental" (iteração 37)

Pedido explícito do usuário: investigar com evidência as falhas recorrentes em
`ListForgeIntegratedSettingsStoreTests`, `PxLoggerTests.Retention_*` e
`SupportPackageServiceTests`, mais os travamentos anteriores do testhost `net48` — nunca
presumir que passar isoladamente prova causa externa, nunca atribuir tudo ao mesmo mecanismo
sem evidência.

### Causa 1 — `PxLogger._logDirectoryOverride`: estado estático de processo sem sincronização

`PxLogger` (`host/.../Integrations/PxLogger.cs`) expõe um único campo estático mutável
(`_logDirectoryOverride`), trocado por `SetLogDirectoryForTesting(string?)` — um seam só para
teste, nunca dimensionado para uso concorrente (em produção o PX Tool Box é um único add-on de
thread única dentro do Corel, nunca dois loggers "ao mesmo tempo").

O xUnit roda cada CLASSE de teste como sua própria coleção por padrão, e coleções diferentes
rodam em PARALELO entre si (nenhum `[CollectionBehavior(DisableTestParallelization)]` nem
`[Collection]` existia neste projeto antes desta investigação). `PxLoggerTests` e
`SupportPackageServiceTests` (duas classes no mesmo arquivo, mas duas coleções) mutavam esse
MESMO campo global concorrentemente. Cenário reproduzido por análise de código: Teste A define
o override para `dirA` (GUID, ainda sem nada gravado em disco); Teste B, rodando em paralelo
numa thread diferente, termina e reseta o override para `null` (ou para `dirB`) no `finally`
ANTES de Teste A efetivamente escrever/criar `dirA` em disco; a escrita de Teste A vai parar no
diretório real de produção (ou em `dirB`); `dirA` nunca chega a existir; o `finally` de Teste A
(`Directory.Delete(dirA, recursive: true)`) lança `DirectoryNotFoundException` — exatamente o
erro já observado: `"Não foi possível localizar uma parte do caminho
'...\pxtoolbox-sp-logs-63d21a4b342145d7a3b73e6ceacfcb96'."`. O mesmo mecanismo explica contagens
erradas em `PxLoggerTests.Retention_WhenTotalExceedsMaxBytes_RemovesOldestFilesFirst_KeepsNewest`
(outra gravação concorrente, via o override temporariamente redirecionado, polui o diretório que
o teste está medindo).

### Causa 2 — diretório real `%LocalAppData%\PXToolBox\Dev\config\` compartilhado entre classes

`ListForgeIntegratedSettingsStoreTests` (listforge-integrated-settings.json),
`NexorConfigStoreTests` (nexor-path.txt) e `ListForgeConfigStoreTests` (listforge-path.txt) —
três classes DIFERENTES (coleções diferentes, paralelas por padrão), cada uma deliberadamente
testando contra o caminho REAL de `%LocalAppData%` (é literalmente o que está sendo testado:
"grava no AppData real da máquina", não um diretório de teste isolado). Operações de diretório
concorrentes de classes diferentes (criar a pasta, enumerar/apagar arquivos de quarentena,
fazer backup/restaurar o conteúdo real) podem se intercalar.

Reproduzido de verdade nesta investigação: depois de corrigir só a Causa 1,
`ListForgeConfigStoreTests.SaveAndRead_RoundTrips_SimulatingPersistenceAcrossRestarts` falhou
com `IOException` rodando em paralelo com as outras duas classes do grupo.

Achado notável durante a investigação: o comentário já existente em
`ListForgeIntegratedSettingsStoreTests.cs` ("um teste com FileShare.None aqui colidiria com
elas sob a paralelização padrão do xUnit entre classes") mostra que esse risco de
paralelização já era CONHECIDO pela equipe — só nunca tinha sido corrigido de forma
sistemática para todas as classes que de fato compartilham o recurso.

### Causa 3 — `AtomicFileWriter.Sync`: um único lock de processo, mais um Timer vulnerável a starvation

`AtomicFileWriter` (compartilhado entre `ListForgeIntegratedSettingsStore` e
`ListForgeProcessingHistoryService`) usa um ÚNICO objeto de lock estático (`Sync`) para TODO
destino, não um lock por caminho (comentário da própria classe: "gravações raras... nunca
ocorrem em paralelo de verdade dentro desta aplicação" — verdade em produção, falso sob o
testhost paralelo). `AtomicFileWriterTests`, `ListForgeIntegratedSettingsStoreTests` (via
`Store.Save`) e `ListForgeProcessingHistoryServiceTests` (via `Service.Save`, mesmo usando
diretórios de teste isolados para os PRÓPRIOS arquivos) disputam esse mesmo lock de processo.

Isso é especialmente grave para
`AtomicFileWriterTests.WriteTextSafely_TransientLockOnDestination_RetriesAndEventuallySucceeds`:
o teste segura um lock real (`FileShare.None`) no destino e cronometra a liberação pra acontecer
DENTRO do orçamento de retentativa de `ReplaceAtomically` (3 tentativas, atrasos
`[0, 60, 200]`ms, ~260ms de orçamento total até a última tentativa). Duas fontes de atraso
externo podiam consumir esse orçamento: (a) escritas concorrentes de OUTRA classe disputando o
mesmo `Sync` global atrasam quando a retentativa nem começa a contar; (b) o teste liberava o
lock via `System.Threading.Timer` — despachado pelo ThreadPool, que pode sofrer starvation sob
carga de paralelismo (muitas coleções de teste rodando ao mesmo tempo, cada uma competindo por
threads do mesmo pool) — fazendo o release chegar atrasado mesmo com os 80ms "corretos"
agendados.

### Correção

Três classes agrupadas em `[Collection("PxLogger static test seam")]` (Causa 1: `PxLoggerTests`,
`SupportPackageServiceTests`) e cinco em
`[Collection("PXToolBox Dev shared filesystem resources")]` (Causas 2 e 3:
`ListForgeIntegratedSettingsStoreTests`, `NexorConfigStoreTests`, `ListForgeConfigStoreTests`,
`AtomicFileWriterTests`, `ListForgeProcessingHistoryServiceTests`) — `tests/PXToolBox.Tests/TestCollections.cs`,
novo. Por padrão, classes do MESMO `[Collection]` nunca rodam em paralelo entre si no xUnit —
nenhum paralelismo foi desabilitado globalmente, só entre as classes que comprovadamente disputam
cada recurso (as outras ~20 classes do projeto continuam totalmente paralelas). Deliberadamente
NÃO incluídas no grupo: `NexorLauncherTests`/`ListForgeLauncherTests` (classes IRMÃS, mesmo
arquivo, nome parecido — usam só um fake em memória injetado, nunca tocam o disco real).

O `Timer` de `AtomicFileWriterTests` foi trocado por uma `Thread` dedicada com `Thread.Sleep`
— agendada pelo SO, não pela fila do ThreadPool, imune ao tipo de starvation descrito acima.

Depois de corrigir as Causas 1-3 e rodar a suíte repetidamente, mais duas falhas apareceram —
desta vez com stack trace completo capturado:
```
IOException: O processo não pode acessar o arquivo
'...\listforge-integrated-settings.corrompido-20261103-170644.json'
porque ele está sendo usado por outro processo.
  em File.ReadAllText(quarantine) — ListForgeIntegratedSettingsStoreTests.cs:233
```
```
IOException: O processo não pode acessar o arquivo
'...\listforge-integrated-settings.json' porque ele está sendo usado por outro processo.
  em File.WriteAllText(RealFilePath, garbage) — ListForgeIntegratedSettingsStoreTests.cs:224
```
Ambas em `Load_CorruptedJson_QuarantinesFileAndReturnsDefaults_WithoutOverwritingSilently`, JÁ
depois de toda disputa entre CLASSES diferentes ter sido eliminada (Causas 1-3 corrigidas) — uma
causa DIFERENTE: um **bloqueio transitório de origem não identificada** segurando brevemente um
handle no arquivo logo após ele ser escrito. Nenhum processo responsável foi confirmado — não é
atribuído a antivírus/indexador nem a nenhum outro processo específico, pela mesma razão que
`AtomicFileWriter` nunca presume a causa em produção ("qualquer handle aberto... de qualquer
processo — nunca presumido aqui qual"); é a mesma mensagem exata que motivou essa classe a
existir. Corrigido, NA ÉPOCA, com um retry pequeno e justificado nos pontos onde o teste tocava
`RealFilePath`/arquivos de quarentena diretamente (3 tentativas, atrasos 0/50/150ms —
`tests/PXToolBox.Tests/TestFileIO.cs`, novo) — mesma categoria de tolerância que já existe em
produção para o mesmo tipo de erro, nunca escondendo uma race da arquitetura (essas já foram
eliminadas pelas coleções). Ver "Revisão" abaixo: `RealFilePath` deixou de ser o caminho real
desde então, mas o retry foi mantido como rede de segurança geral (o fenômeno pode ocorrer em
qualquer pasta recém-tocada no Windows, não só na real).

### O que permanece sem explicação

O travamento/crash real do processo testhost `net48` mencionado em rodadas anteriores do
ROADMAP (23ª rodada: "a causa permanece não determinada", sem dump de thread nem evidência
direta do que o processo travado estava fazendo) **não foi reproduzido nesta investigação** e
**continua sem causa determinada**. Uma entrada anterior do ROADMAP (24ª/25ª rodada) atribuía
erroneamente um erro de I/O em `Directory.Delete` a esse mesmo travamento — correção de registro
feita no ROADMAP: são dois fenômenos diferentes; o erro de `Directory.Delete` tem, agora, causa
raiz concreta e corrigida (Causa 1 acima); o travamento real do processo não tem nenhuma relação
demonstrada com as três causas desta seção.

### Verificação

`dotnet build` — 0 erros. Suíte completa rodada repetidamente após cada correção: falhas em
praticamente toda execução antes de qualquer correção; 1 falha em 5 execuções após corrigir só
as Causas 1-3 (duas classes ainda não cobertas, corrigidas em seguida); **15 execuções
consecutivas sem nenhuma falha** após a correção final (retry de bloqueio transitório incluído).
As 15 execuções rodaram as DUAS pernas do projeto de testes em cada chamada de
`dotnet test tests/PXToolBox.Tests/PXToolBox.Tests.csproj` — `PXToolBox.Tests.dll (net8.0)`, 96
testes, e `PXToolBox.Tests.dll (net48)`, 344 testes (onde vivem todas as classes afetadas por
esta investigação; o projeto não tem outras pernas). Mudança só em testes
(`TestCollections.cs`, `TestFileIO.cs`, ajustes em `PxLoggerAndSupportPackageTests.cs`,
`ListForgeIntegratedSettingsStoreTests.cs`, `AtomicFileWriterTests.cs`,
`NexorLauncherTests.cs`, `ListForgeLauncherTests.cs`,
`ListForgeProcessingHistoryServiceTests.cs`) — nenhum código de produção alterado, nenhuma
versão DEV nova gerada (AGENTS.md: instalador só quando código de produção muda).

### Revisão (auditoria pós-correção, mesma iteração)

Pedido explícito do usuário: auditar a correção acima antes de encerrar — confirmar que os
retries não mascaram nada, verificar se o diretório real ainda é tocado, não presumir
antivírus/indexador sem evidência, confirmar cobertura completa das coleções, e separar esta
investigação do restante do diff pendente.

**`TestFileIO.Retry` auditado**: só captura `IOException` (nunca uma asserção, que é
`Xunit.Sdk.XunitException`, um tipo diferente) e só na condição `attempt < DelaysMs.Length - 1`
— na ÚLTIMA tentativa a exceção sempre propaga sem ser capturada, preservando o tipo, a
mensagem (que já inclui o caminho afetado, como visto nos stack traces acima) e o stack trace
original. Nenhuma assinatura de "engolir para sempre passar verde" encontrada.

**Causa 2 eliminada por completo, não só serializada**: `ListForgeIntegratedSettingsStore`,
`ListForgeConfigStore` e `NexorConfigStore` ganharam cada uma um seam de teste
(`SetConfigDirectoryForTesting`/`SetFilePathForTesting`, mesmo padrão já usado por
`PxLogger.SetLogDirectoryForTesting`) que redireciona a classe inteira para um diretório
temporário isolado por instância de teste, nunca mais `%LocalAppData%\PXToolBox\Dev\config\`.
Isso cobre a lacuna que a simples serialização entre classes (correção original desta seção)
nunca cobria: o risco de ler/sobrescrever a configuração real enquanto o PX Tool Box DEV está
aberto de verdade na mesma máquina. `ListForgeIntegratedSettingsStoreTests`,
`ListForgeConfigStoreTests` e `NexorConfigStoreTests` foram reescritas para usar esse seam e
PARARAM de fazer backup/restauração do arquivo real (não há mais nada real para
preservar/restaurar).

Isso introduziu uma disputa NOVA, da MESMA categoria da Causa 1 (campo estático sem
sincronização, agora em duas classes de produção diferentes) — encontrada e corrigida antes de
qualquer execução, não depois:
- `ListForgeIntegratedSettingsStore._configDirectoryOverride` é mutado por
  `ListForgeIntegratedSettingsStoreTests` e lido (indiretamente, via `ConfigDirectoryForDisplay`
  → `ListForgeProcessingHistoryService.DefaultPath`) por
  `ListForgeProcessingHistoryServiceTests.DefaultPath_IsUnderPxToolBoxDevConfig_NeverTheStandaloneAppDir`,
  que espera o caminho PADRÃO (não sobrescrito). As duas classes permanecem agrupadas em
  `[Collection("PXToolBox Dev shared filesystem resources")]` — mesmo nome de antes, justificativa
  reescrita em `TestCollections.cs` para refletir esta razão nova (a antiga, "mesmo diretório
  real", está obsoleta).
- `ListForgeConfigStore._filePathOverride` é mutado tanto por `ListForgeConfigStoreTests` quanto
  por `NexorConfigStoreTests` (que também exercita `ListForgeConfigStore`, de propósito, no teste
  de independência Nexor/ListForge). Nova coleção criada:
  `[Collection("ListForge/Nexor launcher config-path static test seam")]`.

`AtomicFileWriterTests` foi REMOVIDA de qualquer coleção: todos os seus testes já usam um
diretório temporário próprio por teste (nunca tocam os campos estáticos acima) e o lock
`AtomicFileWriter.Sync` já se auto-sincroniza — contenção nesse lock serializa corretamente por
si só, não é uma condição de corrida que precisasse de isolamento extra entre classes de teste.

**Cobertura confirmada por busca em todo `tests/PXToolBox.Tests`** (não por inspeção parcial):
nenhuma outra classe lê/grava `_configDirectoryOverride`, `ListForgeConfigStore._filePathOverride`
ou `NexorConfigStore._filePathOverride` fora das classes já listadas nas duas coleções acima.

**Mudança de produção nesta revisão**: os três novos métodos `SetConfigDirectoryForTesting`/
`SetFilePathForTesting`/`DefaultFilePathForTesting` são API pública nova em
`ListForgeIntegratedSettingsStore`/`ListForgeConfigStore`/`NexorConfigStore` (host compilado em
`PXToolBoxDev.dll`, parte da distribuição DEV) — mesmo comportamento padrão preservado (override
começa `null`, caminho real inalterado em uso normal), mas é código de produção, não só
teste/documentação. Por isso, ao contrário da correção original desta seção, **uma nova build
DEV foi gerada e validada**: `0.1.0-dev.44` (`Build-Release.ps1 -Version "0.1.0-dev.44" -Channel
Dev`, que já roda a suíte de testes como parte do próprio pipeline — passou), instalador
compilado com sucesso, todas as builds anteriores preservadas em `dist\dev\`. **Não** houve smoke
test real dentro do CorelDRAW nesta revisão — só compilação real, suíte automatizada real e
geração real do instalador.

**Verificação desta revisão**: 5 execuções consecutivas de
`dotnet test tests/PXToolBox.Tests/PXToolBox.Tests.csproj` depois de todas as mudanças acima —
`PXToolBox.Tests.dll (net8.0)`: 96/96 em cada uma; `PXToolBox.Tests.dll (net48)`: 344/344 em cada
uma — mais a execução de testes embutida no pipeline de `Build-Release.ps1` (mais uma passagem
limpa). Nenhuma repetição adicional além destas (nunca repetido "até dar verde" em resposta a
falha — todas as 5+1 execuções já passaram de primeira). `Build-Release.ps1 -Channel Stable`
confirmado continuando a recusar gerar pacote.

## 32. Camadas: ocultar camadas dos selecionados / mostrar últimas ocultadas (dev.45–dev.47)

"Ocultar camadas dos objetos selecionados" (dev.45) ganhou o complemento "Mostrar últimas
camadas ocultadas" (dev.46), em `LayerVisibilityService`. Não há monitoramento de mudanças de
visibilidade: o recurso só recupera a última operação feita pelo próprio botão do PX.

### 32.1 dev.46 — marcação em Properties (SUBSTITUÍDA na dev.47)

A dev.46 localizava documento e camadas gravando uma marcação em `Document.Properties` e
`Layer.Properties`. Isso contrariava o requisito (registro só na memória da sessão): a marcação
ia para o CDR se o arquivo fosse salvo antes de restaurar. Auditoria do que a dev.46 fazia:

- **Nome/ID**: `"PXToolBox.LastHiddenLayers"`, ID 1, nos dois níveis. Valores: no documento, um
  GUID de 32 hex por operação; em cada camada ocultada, `"<guid>:<posição>"`.
- **Colisão/sobrescrita**: a escrita não consultava `Exists` antes — uma propriedade
  pré-existente com exatamente esse nome e ID seria sobrescrita sem aviso. E cada nova ocultação
  apagava esse (nome, ID 1) em TODAS as camadas do documento ativo (nunca por nome sozinho, nunca
  outras entradas, mas também apagaria uma entrada alheia com o mesmo nome e ID). Nenhum outro
  código conhecido usa esse nome (o PX usa `KEEP_DESIGN_SIZE` em shapes; o SISBolt usa outros
  nomes em shapes) — risco baixo, não nulo.
- **Limpeza**: a restauração removia as marcações das camadas restauradas/já visíveis e a do
  documento. Ficavam para trás: camadas puladas (marcação duplicada ou camada especial); todas as
  marcações quando a restauração era recusada; as do documento fechado (registro descartado, mas
  marcação gravada no arquivo se ele tinha sido salvo); numa falha no meio da ocultação, a
  marcação já escrita numa camada que não chegou a ser ocultada; e, no Desfazer, nunca foi
  verificado se o Corel desfaz escrita em `Properties` — se não desfizer, Ctrl+Z da ocultação
  deixava todas as marcações. Uma nova ocultação só limpava as camadas do documento em que
  rodava.
- **Documento modificado**: as escritas aconteciam no mesmo grupo de comando que a mudança de
  visibilidade, que já marca o documento como modificado — não havia "modificado" extra visível;
  se `Properties` sozinho marca `Dirty` nunca foi verificado.
- **Exposição real**: só arquivos salvos com a dev.46 instalada depois de usar "Ocultar camadas
  dos objetos selecionados" (a dev.45 não gravava nada). Nesta máquina a versão instalada é a
  dev.43 (registro de desinstalação e `PXToolBoxDev.dll` em `Programs64\Addons\PXToolBoxDev`,
  conferidos em 04/11/2026): nem dev.45 nem dev.46 foram instaladas aqui. Nenhuma limpeza
  automática foi implementada. Se um CDR marcado aparecer (outra máquina), o escopo exato de uma
  limpeza seria: `Document.Properties("PXToolBox.LastHiddenLayers", 1)` e, em cada camada,
  `Layer.Properties("PXToolBox.LastHiddenLayers", 1)` — só quando o valor tiver o formato da dev.46
  (32 hex no documento; 32 hex + `:` + número na camada). A dev.47 ignora essas entradas
  totalmente (não lê, não grava, não apaga).

### 32.2 dev.47 — registro só em memória, identidade de sessão

**Opções levantadas por reflexão sobre `Corel.Interop.VGCore.dll` (v27)**:
- Índices (`Page.Index`, `Layer.Index`, `Document.Index`): mudam com reordenação/fechamento —
  usados só DENTRO de uma operação (deduplicação em `CorelLayerAdapter.Equals`).
- Nomes/caminho (`Name`, `FullFileName`): repetíveis e editáveis; um arquivo reaberto tem o
  mesmo caminho — nunca usados para localizar, só em mensagens.
- `TreeNode.Handle` (Int32): sem contrato documentado de estabilidade ou unicidade — descartado.
- `Properties`: grava no arquivo — descartado (ver 32.1).
- **Escolhido: o próprio objeto COM da sessão**, exposto como `ICorelDocument.Identity`/
  `ICorelLayer.Identity` (o RCW) e comparado só com `ReferenceEquals`. Contrato em que isso se
  apoia: a regra de identidade do COM (o mesmo objeto devolve sempre o mesmo `IUnknown`) e o CLR,
  que mantém um único RCW por `IUnknown`. Enquanto o registro guarda a referência, o objeto não é
  liberado, então seu endereço não pode ser reaproveitado por outra camada/documento — uma
  comparação nunca dá falso positivo. O que **não** é garantido pelo contrato: que o Corel
  devolva o MESMO objeto para a mesma camada em acessos diferentes (poderia criar um objeto
  novo por acesso). Por isso a ocultação confere antes, só lendo, se o documento aparece
  exatamente uma vez em `Application.Documents` e cada camada exatamente uma vez nas camadas do
  documento; se não, oculta mas NÃO registra ("Restauração indisponível") — o pior caso é o
  recurso ficar indisponível, nunca restaurar a camada errada.

A referência guardada nunca é invocada: serve só para `ReferenceEquals` contra objetos lidos
de novo das coleções vivas. Ela é solta quando o registro é descartado.

**Ciclo de vida**:
- Fechar o documento de origem invalida o registro: `Application.DocumentClose(Document Doc)`
  (confirmado por reflexão), comparando só identidade, e, como rede de segurança, a cada
  atualização do estado do botão (após operações e a cada `SelectionChange`) e no clique, se o
  documento não está mais em `Application.Documents`.
- Reabrir o arquivo cria outro objeto: nunca casa com o registro e nunca recupera a operação.
- Documento de origem aberto mas não ativo → recusa sem alterar nada, registro preservado (o
  passo de desfazer de `RunWithWorkspace` é sempre no documento ativo).
- Documento ou camada encontrados mais de uma vez → restauração invalidada, nada alterado.
- Camada não encontrada (excluída, ou recriada como outro objeto por um Desfazer) → informada;
  as demais são restauradas. Uma camada nova com o mesmo nome nunca é usada no lugar.

**Mantido da dev.46**: só camadas efetivamente mudadas de visível para oculta são registradas;
camadas especiais nunca ocultadas nem mostradas; tentativas sem alteração preservam o registro;
uma nova ocultação efetiva substitui o registro; um único Ctrl+Z por operação. Nenhuma escrita em
`Properties`: `ICorelLayer`/`ICorelDocument` não expõem mais `Properties` (teste de contrato).

**Experimento real pendente**: um script que cria um documento temporário e verifica a
identidade em acessos diferentes, depois de reordenar/renomear, excluir+desfazer e fechar+reabrir
foi preparado, mas não rodou: `Marshal.GetActiveObject("CorelDRAW.Application.27")` (e o ProgID
genérico) retornou `MK_E_UNAVAILABLE` a partir do terminal desta sessão, com o Corel 2026 aberto
— o objeto não estava acessível de fora (causa não determinada). Nenhuma outra forma de conexão
foi tentada. O smoke test (`docs/mvp-smoke-test.md`, 4i) cobre a mesma verificação dentro do
add-on.

**Verificação**: compilação real (incluindo o host) e testes automatizados reais com fakes —
não comprovam o comportamento da API dentro do Corel. Smoke test real **pendente** para dev.45,
dev.46 e dev.47.

### 32.3 dev.48 — diagnóstico de identidade dentro do add-on

Substitui o experimento externo (que não conseguiu se conectar ao Corel) por um diagnóstico
DEV no próprio host: **Diagnóstico → "Testar identidade de camadas…"** (rótulo até a dev.48:
"Diagnóstico de identidade de camadas…")
(`Diagnostics/LayerIdentityDiagnostics.cs`). Só roda depois de o usuário confirmar um aviso
com o efeito exato.

- **Mesmo mecanismo da produção**: identidades lidas pelos adaptadores (`CorelDocumentAdapter`/
  `CorelLayerAdapter.Identity`, `AllLayers`, `OpenDocuments`, seleção → `Shape.Layer`) e
  comparadas por `LayerVisibilityService.IsSameSessionObject`/`CountIdentityMatches`, que o
  próprio serviço passou a usar (único critério, `ReferenceEquals`). Ocultar/restaurar rodam
  pelo serviço real, numa instância separada — o registro do painel não é tocado.
- **Escopo**: só um documento temporário criado pelo diagnóstico. Verifica acessos repetidos,
  renomear, reordenar, excluir/recriar com o mesmo nome, excluir + Desfazer, ocultar + Desfazer,
  `DocumentClose` (o argumento é o mesmo objeto do registro?), fechar e reabrir o arquivo (salvo
  em `%TEMP%` e apagado no fim). Reativa o documento que estava ativo e informa quantos
  documentos ficaram abertos. Nunca lê/grava `Properties`.
- **Descarte das referências**: confere, por `WeakReference` ao registro, que ele fica livre
  para coleta ao fechar o documento de origem, ao ser substituído por nova ocultação e em
  `ReleaseSessionState`. O painel agora chama `ReleaseSessionState` no `Unloaded` (sem
  `DocumentClose` inscrito o registro não seria mantido em dia); esse gatilho real não é
  simulado pelo diagnóstico — smoke test 4i, passo 16.
- **Sem liberação COM manual**: nenhum `Marshal.ReleaseComObject`/`FinalReleaseComObject`. O
  RCW é único por objeto COM no processo e compartilhado com o resto do add-in; liberá-lo
  quebraria outros usos. As referências são só largadas, e o coletor decide.
- **Limites**: `[OBS]` marca comportamentos sem resposta "certa" (ex.: se o Desfazer de uma
  exclusão devolve o mesmo objeto) — o serviço trata os dois casos com segurança. Um relatório
  sem falhas vale para aquela máquina/versão/sessão; não é garantia universal da API.

**Verificação**: compilação real e testes automatizados com fakes (inclui descarte do registro
por `WeakReference` e o critério de identidade).

**Validação real recebida (dev.48, CorelDRAW 27.1.0.129)**: o usuário executou o diagnóstico
**sem falhas**. Nessa execução passaram a ocultação/restauração pelo serviço real, a identidade
após renomear/reordenar e o descarte do registro ao fechar/reabrir o documento. Vale para aquela
máquina/versão/sessão, não como garantia da API, e **não** aprova o roteiro 4i inteiro. Fechar e
reabrir o docker (4i, passo 16) continua pendente.

## 33. Tamanho Padrão: estado no próprio botão (dev.48)

A linha de texto `StandardSizeStateText` (seção 25) foi removida a pedido do usuário. O estado
de `StandardSizeService.InspectSelection` agora vai para o `Tag` de "Alternar Tamanho Padrão"
(estilo `Px.StateButton`): `AllMarked` → cores de acento (as que o "Preparar Grid" usava);
`Mixed` → só borda de acento mais grossa; demais → botão comum. O texto do estado continua na
dica e em `AutomationProperties.HelpText`, então cor não é o único meio. "Preparar Grid" deixou
de usar `Px.PrimaryButton` (parecia sempre ativo) e ficou como os demais botões.

## 34. Layout da seção Diagnóstico (dev.49)

Correção localizada de `ControlUI.xaml`, sem mudar serviços nem handlers. Antes, os pares de
botões lado a lado em `StackPanel` horizontal cortavam "Copiar diagnóstico" e passavam da
largura do docker (`MinWidth` 300), e os `TextBlock` de resultado vazios reservavam margem.
Agora:
- uma coluna, cada botão na largura do painel, sem larguras fixas;
- rótulo em `TextBlock` com quebra de linha;
- `MinHeight` 28 e margens iguais, por um estilo implícito só dessa seção;
- resultados recolhidos enquanto vazios (gatilho em `Text=""`);
- rótulos curtos, com o escopo de cada botão explicado na dica.

O botão "Copiar" do teste de identidade virou "Copiar teste de identidade", porque numa coluna
só "Copiar" fica ambíguo. Tema, foco pelo teclado e o estado desabilitado vêm do estilo base de
`Button`, sem alteração.

**Verificação visual**: renderização fora do Corel (`RenderTargetBitmap` do `ControlUI`
compilado, sem Corel conectado). Larguras de 300, 360 e 440 px, as três do intervalo
`MinWidth`/`MaxWidth`, a 96, 120 e 144 DPI, com resultados vazios e com textos longos (inclui um
caminho sem espaços). Não foi verificado dentro do docker do CorelDRAW, nem a navegação pelo
teclado.

### 34.1 Relatório do teste de identidade contido no painel (dev.52)

Ajuste localizado. O layout em coluna da dev.49 foi mantido; as capturas que motivaram o pedido
eram da dev.48, então não comprovavam falha da dev.49. Antes, o relatório inteiro do teste de
identidade ia direto para o painel e o esticava. Agora:
- Ao terminar, só um resumo curto aparece, vindo do resultado real:
  `Diagnostics/LayerIdentityReportSummary.cs` conta os marcadores que o relatório já escreve
  (`[FALHOU]`, `[ERRO]`, `[OBS]`). Sem a linha "Resultado:", nunca afirma "nenhuma falha".
  Cancelar, falta de conexão e interrupção aparecem no mesmo lugar.
- O relatório completo fica numa área recolhível, fechada a cada execução: "Ver relatório"/
  "Ocultar relatório". A área tem `MaxHeight` 220 e rolagem vertical própria, sem rolagem
  horizontal.
- "Copiar relatório" copia o texto inteiro. Os dois botões ficam desabilitados, com dica
  explicando o motivo, até existir um relatório.

A lógica do diagnóstico (`LayerIdentityDiagnostics`) não mudou.

**Verificação**
- Testes do resumo.
- Renderização fora do Corel, com configuração isolada, de três estados (antes, só o resumo,
  relatório aberto com 50 linhas) a 300 e 440 px, 100% e 150%. A área aberta mediu 222 px (220
  + borda) para um texto de ~1900 px, e o texto completo foi preservado.
- A barra de rolagem usa o estilo padrão do sistema, igual à do painel principal.
- Não verificado no docker do Corel.

## 35. Favoritos do painel (dev.50–dev.51)

Primeira etapa de personalização: uma seção "Favoritos" no topo do painel, com atalhos para
ferramentas das outras seções. Os botões originais continuam onde estavam.

**Identificação.** `Favorites/PanelToolCatalog.cs` lista as ferramentas compatíveis por Id
estável (`grid.prepare`, `layers.hide-selected`…). Só o Id é persistido — nunca o rótulo nem o
controle WPF; um teste fixa a lista de Ids, porque mudar um deles perde favoritos já salvos.
`ControlUI.InitializeFavorites` liga cada Id ao botão original (só em memória).

**Mesma ação, mesmo estado.** `Favorites/FavoriteShortcut.cs`: o clique do atalho dispara o
evento `Click` do PRÓPRIO botão original (os mesmos handlers, nenhuma regra copiada) e não faz
nada se ele estiver desabilitado. `IsEnabled`, `Tag` (estado do Tamanho Padrão,
`Px.StateButton`), dica e `AutomationProperties.HelpText` vêm do original por binding.

**Escopo.** Grid: Preparar Grid, Alternar/Atualizar Tamanho Padrão. Camadas: Visibilidade
MA/FE/C, Ocultar camadas dos objetos selecionados, Mostrar últimas camadas ocultadas.
Preparação e saída: Preparar Grid Novo, Preparar Molde Novo. **Excluídas**: Preparar
Escudo/Bandeira/Faixa de Capitão, porque executam com os campos "Nome do layout" e "Pasta de
saída" da seção — um atalho rodaria com parâmetros escondidos. Também ficam fora Diagnóstico
(pedido) e as seções não citadas (Códigos, Outras ferramentas, Listas, Produção).

**Persistência — reuso, não um sistema novo.** O campo aditivo
`ListForgeIntegratedSettings.PanelFavoriteToolIds` fica no mesmo arquivo de configuração do PX
DEV, fora dos perfis de trabalho, como `Sizes` (esquema continua 2):
- `null` = nunca definido (arquivo antigo);
- `[]` = o usuário removeu todos.

Nada vai para o documento do Corel. Três regras evitam que dois escritores se sobrescrevam:
- `SavePanelFavorites` muda **só** esse campo no JSON em disco, preservando o resto (inclusive
  campos desconhecidos). Com arquivo ilegível ou de esquema desconhecido, recusa sem tocar nele;
  o esquema 1 é migrado como em `Load`.
- `Save` comum mantém os favoritos que estão **em disco**: as janelas do ListForge guardam este
  objeto por muito tempo e salvariam uma cópia antiga. Só importações que trouxeram favoritos
  gravam o valor do objeto (`includePanelFavorites`).
- `LoadPanelFavorites` é só leitura (sem quarentena nem migração): o painel abre sempre. No pior
  caso abre sem favoritos e mostra um aviso na barra de status.

Ids desconhecidos (ferramenta removida, ou gravados por uma versão mais nova) não aparecem, mas
são preservados no fim da lista (`PanelFavoritesList`). O evento `PanelFavoritesChanged` faz o
painel se redesenhar quando a tela de Configurações importa favoritos.

**Preferências portáteis.** `panelFavoriteToolIds` é um campo aditivo no mesmo esquema 1:
- uma instalação mais antiga ignora a chave;
- um arquivo sem a chave não altera os favoritos atuais;
- Ids desconhecidos ou tipo errado são relatados como incompatíveis e ignorados.

Exportar e o backup de antes de importar usam os favoritos atuais do disco, não a cópia da tela.
"Exportar/Importar configurações…" (o objeto inteiro) também leva o campo.

**Interface.** A estrela âmbar (`Px.Favorite`) marca o favorito; o acento azul continua
reservado para "ativo". O menu de contexto tem template escuro próprio (`Px.ContextMenu`/
`Px.MenuItem`) e abre também em botão desabilitado (`ContextMenuService.ShowOnDisabled`).

**Verificação**
- **Testes automatizados reais** (`PanelFavoritesTests.cs`, perna net48, 22 testes): lista,
  ordem, remoção, catálogo, configuração antiga sem o campo, `Save` comum sem favoritos não
  cria o campo (dev.51), campo com tipo errado, arquivo
  corrompido, esquema desconhecido, esquema 1, cópia antiga salvando por cima,
  exportação/importação, Ids desconhecidos, e o atalho disparando os handlers do original
  (inclusive desabilitado).
- **Renderização fora do Corel** do `ControlUI` compilado, com configuração isolada numa pasta
  temporária: 300/360/440 px a 100/125/150%, com e sem favoritos, Tamanho Padrão ativo e
  original desabilitado.
- **Mesma ação, conferida fora do Corel:** sem o Corel acessível, clicar no original "Preparar
  Grid" e no atalho produziu a mesma mensagem de status.
- **Não verificado:** o menu de contexto aberto (popup não renderizado), navegação real pelo
  teclado e o smoke test no Corel (`docs/mvp-smoke-test.md`, 4j).

**Achados durante a validação (dev.50 → dev.51)**
- **Deadlock de teste.** A suíte net48 travou 2 vezes. O hang dump mostrou um deadlock de
  inicialização estática do WPF entre duas threads de teste:
  - `FavoriteShortcutTests` cria `Binding` em código, o que inicializa `TextBox`/`ScrollViewer`
    e espera o lock de `WpfSharedBamlSchemaContext`;
  - `ShapeInspectorWindowTests` segurava esse lock carregando XAML e esperava o mesmo
    inicializador.

  Correção: a classe foi para uma coleção xUnit sem paralelismo (`TestCollections.cs`). Depois
  disso foram 9 execuções seguidas sem travar. Não afeta produção: no Corel todo o WPF do PX
  roda numa única thread.
- **`null` no JSON.** A dev.50 gravava `"PanelFavoriteToolIds": null` em qualquer `Save` de quem
  nunca definiu favoritos. Inofensivo (`null` = não definido), mas mudava o formato do arquivo.
  A dev.51 omite o campo enquanto é `null` (`NullValueHandling.Ignore`), com teste.
- **Problema pré-existente (corrigido na dev.58, ver 39).** `ListForgePxModeTests`,
  `ManualOutputJsonEditingTests` e `ListForgeIntegratedTests` criam `ClassicWorkspaceWindow`
  sem `SetConfigDirectoryForTesting`. Trocar de modo salva
  `PreferredWorkspaceModeByProfileId` no arquivo **real** de `%LocalAppData%` a cada execução
  da suíte, inclusive a de cada build. Os favoritos não são apagados por isso, porque o `Save`
  comum preserva o valor em disco. Correção sugerida: isolar essas classes como
  `ListForgeIntegratedSettingsStoreTests`.

### 35.1 Favoritos em ícone (dev.53)

A pedido do usuário, cada atalho virou um botão quadrado (36×36, `Px.FavoriteIconButton`, baseado
em `Px.StateButton` — o estado ativo continua azul) num `WrapPanel`:
- **Ícone** declarado no catálogo (`PanelToolCatalog.ToolIcon`): glifo de Segoe Fluent Icons /
  Segoe MDL2 Assets (fontes do Windows 10/11) — grade, tamanho, atualizar, olho riscado, olho,
  documento — ou a própria sigla quando o original já é uma (MA/FE/C). Um "+" âmbar no canto marca
  as ferramentas que criam algo novo (Preparar Grid Novo/Molde Novo). O glifo herda a cor do botão
  por binding (o estilo implícito de `TextBlock` do painel fixaria `Px.Text`).
- **Nome** na dica: primeira linha em negrito, seguida da dica ATUAL do original (por binding —
  muda junto com o estado do Tamanho Padrão ou o motivo de "Mostrar últimas camadas ocultadas"
  estar desabilitado). Também é o nome de acessibilidade.
- **Mover/remover**: botão direito no ícone (ou Shift+F10) → "Mover para a esquerda", "Mover para a
  direita", "Remover dos favoritos". Os ▲/▼/✕ de cada linha e a estrela ★ por item saíram (não
  cabem num ícone; a seção já se chama Favoritos).
- Ação, habilitação e estado continuam vindo do original exatamente como na seção 35.

### 35.2 Todos os botões podem virar favoritos (dev.56)

Pedido do usuário: "todos os botões" favoritáveis (ex.: Agrupar Interseção não era). O catálogo
passou de 10 para 44 ferramentas — todo botão de ferramenta das seções. Os 10 Ids da dev.50
continuam primeiro e iguais (favoritos salvos não mudam); os novos foram acrescentados depois.

- **Ligação com o botão original:** cada botão de ferramenta no XAML tem o Id em
  `AutomationProperties.AutomationId`; `ControlUI.InitializeFavorites` percorre a árvore lógica
  das seções (inclusive recolhidas) e liga Id → botão, em vez da lista fixa anterior. Ganho
  colateral: Ids estáveis também para automação de interface.
- **Exclusões anteriores removidas:** Escudo/Bandeira/Faixa (campos de layout/pasta), Renomear
  (nome digitado) e Tipo de peça (caixas de tipo) agora entram; o atalho usa o que estiver
  nesses campos, e a dica do atalho repete a do original, que diz isso. Diagnóstico também entra;
  "Ver/ocultar relatório" alterna o relatório dentro da seção Diagnóstico.
- **Ícones:** glifos do Windows ou siglas; marcas no canto: "+" cria algo novo, "−" remove da
  página (NO/NU/AP/TS), "T" tipo de peça, "L"/"N" ListForge/Nexor, "D"/"?" diagnósticos.
  Botões com o mesmo texto ganharam nomes distintos no catálogo ("Alternar NO na seleção" ×
  "Remover NO da página"; "Localizar executável do ListForge" × "… do Nexor").
- Fora do catálogo, de propósito: "Personalizar painel…" (cabeçalho, sempre visível) e os
  próprios atalhos dos Favoritos.

**Verificação**
- Testes automatizados reais: o teste do catálogo agora fixa os 10 Ids da dev.50 no início, 44
  Ids e rótulos únicos, e que Agrupar Interseção, Escudo e Diagnóstico estão incluídos.
- Painel compilado carregado fora do Corel (configuração isolada, localizador do Corel desligado,
  configuração real conferida por hash): 44 botões de ferramenta nas seções, todos com Id; os 44
  Ids do catálogo têm exatamente um botão e nenhum Id do XAML fica fora do catálogo; os 44 têm
  o menu "Adicionar aos favoritos"; com os 44 favoritados, 44 ícones aparecem (glifos
  renderizados, nenhum quadrado vazio) a 300 e 440 px, 100% e 150%; o atalho de Agrupar
  Interseção deu a mesma mensagem do original.
- **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4j passo 12).

### 35.3 Dicas legíveis (dev.57)

Relato do usuário na dev.56: a dica dos ícones dos Favoritos tinha fundo claro e texto claro.
Causa: a dica do atalho é montada com `TextBlock` (nome em negrito + dica do original); dentro do
painel, o estilo implícito de `TextBlock` aplica `Px.Text` (claro), enquanto o `ToolTip` sem
template próprio usa o fundo claro do sistema. Correção: estilo implícito de `ToolTip` no
`ControlUI.xaml`, com template escuro como `Px.ContextMenu` (fundo `Px.Surface2`, borda
`Px.Border`, texto `Px.Text`, largura máxima 340 e quebra de linha). Vale para todas as dicas do
painel — as dos botões originais também ficam escuras e quebram linha em vez de uma linha longa.

**Verificação** — dicas abertas de verdade fora do Corel (popup ligado ao botão, fora da tela;
configuração isolada, localizador do Corel desligado, configuração real conferida por hash):
- dev.56 empacotada: reproduzido o defeito (texto claro sobre fundo claro, `Foreground` do
  `ToolTip` = cor do sistema);
- com a correção: dica do atalho de Agrupar Interseção, do atalho desabilitado de "Mostrar
  últimas camadas ocultadas" e a dica em texto do botão original — fundo escuro, texto claro,
  quebra de linha (535 → 329 px de largura), a 100% e 150%.
- **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4j passo 3).

### 35.4 Reordenar arrastando (dev.66)

Próxima etapa listada na dev.50. `Favorites/FavoriteDragReorder.cs` liga o arraste ao
`WrapPanel` dos Favoritos; a regra de ordem é `PanelFavoritesList.MoveTo(id, insertBefore)`
(posição contada antes de mover; soltar no próprio lugar não muda nada; Ids desconhecidos
continuam preservados no fim, como em `MoveUp`/`MoveDown`).

- **Sem OLE** (`DragDrop.DoDragDrop`): só captura do mouse no próprio painel. O arraste nunca
  sai do docker nem entrega dados ao documento do CorelDRAW; soltar fora dos Favoritos (com 8 px
  de folga) cancela.
- O arraste começa só depois da distância mínima do sistema
  (`SystemParameters.MinimumHorizontal/VerticalDragDistance`): clique curto continua sendo o
  clique do atalho. Ao começar, o painel toma a captura do mouse e o botão perde a dele — o
  clique não acontece ao soltar.
- O item pressionado e a posição de inserção vêm da geometria dos ícones (não do evento do
  botão): atalhos desabilitados também podem ser arrastados (o painel tem fundo transparente
  para receber o mouse). Inserção: linha mais próxima (os ícones quebram linha), antes do
  primeiro ícone cujo centro fica à direita do ponteiro, ou depois do último da linha.
- Durante o arraste: ícone com opacidade 0,45, cursor de mover e uma barra `Px.Favorite` de
  2 px num `Canvas` sobreposto ao `WrapPanel` (não usa `AdornerLayer`, que pode não existir no
  docker hospedado pelo Corel).
- A posição no painel é convertida para a da lista inteira (`OnFavoriteDropped`) — um favorito
  conhecido cujo botão não está neste painel não ocupa posição visível.
- Gravação igual a dos outros comandos (`ChangeFavorites`): mesma configuração, mesmo aviso se
  não salvar.

**Verificação**: testes automatizados de `MoveTo`, da geometria (linhas, vãos, acima/abaixo,
barra) e do arraste com um `WrapPanel` montado de verdade (limiar, mover, cancelar fora, vão),
chamando `Press`/`Drag`/`Release` com posições — sem mouse real. Exercício do painel compilado
fora do Corel (configuração isolada). **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4j
passo 13).

**Correção (dev.71) — relato do usuário: "arrastar os favoritos não está funcionando".** Causa:
ao começar o arraste o painel chama `CaptureMouse`; o botão pressionado perde a captura e o
`LostMouseCapture` dele (evento roteado que SOBE) chegava ao painel, cujo tratador cancelava o
arraste — no mesmo instante em que começava. Atalhos habilitados nunca arrastavam (só os
desabilitados, que não capturam o mouse, escapavam). Os testes da dev.66 chamavam
`Press`/`Drag`/`Release` direto e nunca passavam por esse evento. Agora só cancela quando quem
perdeu a captura foi o próprio painel (`e.OriginalSource == painel`). Teste novo dispara o
`LostMouseCapture` no botão, subindo, como o WPF faz: **falha com o código antigo** (conferido
revertendo a linha) e passa com a correção. Arraste com o mouse real: ainda **não verificado**
(4j passo 13).

### 35.5 Um ícone por ferramenta (dev.72)

Pedido do usuário: "tem ícones se repetindo… deixa o ícone de cada ferramenta único", tirar o
"S" das ferramentas do SISBolt e dar uma mão à Mão Livre. Havia 16 grupos de ferramentas com o
mesmo símbolo, diferenciadas só pela marca do canto (ex.: Preparar Grid / Grid Novo / Contorno;
Abrir ListForge / Abrir Nexor; Notas da versão / Inspecionar / Detalhes do contexto). 19
ferramentas ganharam símbolo novo (Segoe MDL2, escolhidos numa folha renderizada da fonte); a
marca do canto ficou só como dica extra. Mão Livre: `E7C9` (mão). "Remover … da página" segue
com a mesma sigla de alternar, mas em vermelho com moldura (`IsRemoval`) — é outro ícone.
`Catalog_EveryToolHasItsOwnIcon` falha se algum símbolo voltar a se repetir. Ids inalterados.

Marca do canto = seção (pedido do usuário, mesma rodada): inicial; mesma inicial → primeira e
segunda letras. `PanelToolCatalog.SectionBadges`: Grid G, Códigos Co, Camadas Ca, Preparação e
saída **PS** (pela regra seria "Pr", igual a Produção — usa as iniciais das palavras), Produção
Pr, as três "Outras ferramentas" pelo próprio nome (Tipo de peça T, Objetos O, Medidas M),
Listas L, Diagnóstico D. Remoção mantém "−" vermelho. A marca vem de `Tool.Badge` (não mais do
ícone); fonte 9, presa no canto do quadrado de 32, com o símbolo 7 px abaixo — na primeira
renderização, com fonte 11 e margem negativa, "Co"/"Ca" invadiam NO/MA.
`Catalog_CornerBadge_IsTheSectionInitials_UniquePerSection` confere.

## 36. Personalizar painel — ordem e visibilidade das seções (dev.53)

"Personalizar painel…" no cabeçalho do docker (sempre habilitado — inclusive é o caminho de volta
se todas as seções forem ocultadas) abre `PanelLayout/PanelCustomizationWindow`.

**Identificação.** `PanelLayout/PanelSectionCatalog.cs` lista as 11 seções por Id estável
(`favorites`, `grid`, `codes`, `layers`, `output`, `tools.piece-type`, `tools.objects`,
`tools.measures`, `lists`, `production`, `diagnostics`); a ordem do catálogo é o layout padrão (a
ordem do painel antes desta etapa). Um teste fixa os Ids.

**Modelo.** `PanelSectionLayout` (mesmo princípio de `PanelFavoritesList`): nunca lança com o que
vem da configuração; vazios/repetidos descartados (vale a primeira ocorrência); seção conhecida que
falta no salvo (seção nova depois de uma atualização) entra **visível no fim**; Id desconhecido
não aparece mas é preservado no fim ao salvar. `ResetToDefault` volta à ordem do catálogo com tudo
visível e mantém os desconhecidos.

**Rascunho, Salvar e Cancelar.** A janela edita uma cópia. "Salvar" entrega a cópia ao painel, que
**grava primeiro e só então aplica**; se a gravação falhar, o painel não muda e a janela continua
aberta com a mensagem. "Cancelar", Esc ou fechar descartam o rascunho. "Restaurar layout padrão"
muda só o rascunho. A janela é modal e sem Owner, como `OrderContextPathDialog` (o painel é um
docker nativo, não uma Window WPF). As linhas são reordenadas sem recriar, para o foco do teclado
continuar no botão usado.

**Aplicação sem recriar nada.** Os 11 `Expander`s ganharam `x:Name` e passaram a viver em
`SectionsHost`. `ControlUI.ApplySectionLayout` reordena os MESMOS objetos nesse `StackPanel` e
oculta (`Collapsed`) os desmarcados — texto digitado, seções abertas/fechadas, estado do Tamanho
Padrão e o registro de camadas ocultadas continuam intactos; os atalhos dos Favoritos continuam
acionando os originais mesmo com a seção deles oculta. O status do último comando fica depois das
seções, ou logo antes de Diagnóstico quando ela é a última visível (a posição do layout padrão).

**Persistência — reuso.** Campo aditivo `ListForgeIntegratedSettings.PanelSectionLayout`
(`[{ "Id", "Visible" }]`), global, fora dos perfis, mesmo arquivo e mesmas regras dos favoritos
(esquema continua 2; `null` = nunca personalizado, omitido do JSON). O caminho de "gravar só um
campo" de `SavePanelFavorites` virou `SavePanelField`, compartilhado por `SavePanelFavorites` e
`SavePanelSectionLayout`. O `Save` comum preserva os dois campos em disco; `Save(...,
includePanelSectionLayout: true)` (importação que trouxe layout) grava o do objeto e dispara
`PanelSectionLayoutChanged`, que faz o painel reaplicar. `LoadPanelSectionLayout` é só leitura: o
painel sempre abre, no pior caso com o layout padrão e um aviso.

**Preferências portáteis.** `panelSectionLayout` (`[{ "id", "visible" }]`) é aditivo no esquema 1:
arquivo sem a chave (ex.: exportado antes da dev.53) mantém o layout atual; Ids desconhecidos ou
itens inválidos são relatados e ignorados; seções que o arquivo não traz entram visíveis no fim.
Exportar e o backup de antes de importar usam o layout em disco, não a cópia da tela de
Configurações. "Restaurar layout padrão" e importar layout nunca mexem nos favoritos.

**Verificação**
- **Testes automatizados reais** (`PanelSectionLayoutTests.cs`, perna net48, 20 testes): catálogo
  e padrão; layout antigo com seções novas no fim; Ids desconhecidos/repetidos/vazios; mover e
  ocultar; restaurar padrão; gravação só do campo preservando favoritos e campos desconhecidos;
  restaurar padrão preservando lista e ordem dos favoritos; cópia antiga salvando por cima; save
  de importação notificando só o layout; arquivo corrompido e tipo errado; itens inválidos;
  exportação/importação; arquivo antigo sem layout; layout parcial de versão anterior; tipo
  errado no arquivo; janela — rascunho até Salvar, Cancelar não grava, falha ao salvar mantém o
  rascunho com a mensagem, restaurar padrão só no rascunho. Os testes de atalho dos Favoritos
  foram ajustados para o ícone e a dica com nome.
- **Fora do Corel, com o painel compilado** (configuração isolada numa pasta temporária e
  localizador do Corel desligado por reflexão antes de criar o painel — o CorelDRAW do usuário
  estava aberto e não foi tocado; a configuração real foi conferida por hash, inalterada):
  - renderização em 300 e 440 px, 100% e 150%, no layout padrão e personalizado; janela em
    340×420 e 420×560, 100% e 150%;
  - Diagnóstico para o topo, Favoritos e Códigos ocultos: texto digitado nos campos de Preparação
    e saída e a seção Camadas aberta continuaram iguais; favoritos no disco inalterados; os 10
    atalhos continuam montados com a seção oculta;
  - com Grid oculta, original e atalho de "Preparar Grid" deram a mesma mensagem;
  - nova instância do painel leu o layout salvo; restaurar padrão voltou à ordem original sem
    mudar os favoritos;
  - importação de um arquivo de preferências antigo (só favoritos) manteve o layout; layout
    parcial colocou as seções que faltavam visíveis no fim e relatou o Id desconhecido.
- **Achado durante a verificação:** o primeiro estilo do ícone usava `Px.StateButton` antes de
  ele ser declarado — `StaticResource` não aceita referência adiante e o painel não abria. Os
  testes automatizados não constroem o `ControlUI` e não pegaram; a renderização pegou. Corrigido
  antes da build.
- **Pré-existente:** a legenda "Alternar visibilidade de todas as camadas MA, FE ou C da página
  ativa:" (`Px.SectionLabel`, sem quebra de linha) era cortada num docker de 300 px. Corrigida
  depois da dev.53 (ver 36.1), para a próxima build DEV.
- **Não verificado:** nada disto rodou dentro do CorelDRAW (`docs/mvp-smoke-test.md`, 4k); menus
  de contexto e dicas abertos (popups não renderizados); a barra de rolagem da janela usa o visual
  padrão do Windows.

### 36.1 Legenda de Camadas em docker estreito (após a dev.53, entra na próxima build DEV)

Só essa legenda ganhou `TextWrapping="Wrap"` (o estilo compartilhado `Px.SectionLabel` não foi
alterado, para não mexer nas outras seções).

**Verificação**
- Compilação real do host.
- **Painel carregado fora do Corel** (a compilação sozinha não pega erro de XAML — foi assim que
  o erro de estilo da dev.53 apareceu): o `ControlUI` compilado foi instanciado e medido, com
  configuração isolada numa pasta temporária e o localizador do Corel desligado (o CorelDRAW do
  usuário estava aberto e não foi tocado; a configuração real foi conferida por hash, inalterada).
- Em 300 e 360 px a legenda ocupa 2 linhas e em 440 px uma linha, sem corte, a 100% e 150%. MA, FE
  e C ficam na mesma linha (mesmo y), com 28 px de altura e larguras iguais à de antes da
  correção (a diferença de 2 px do botão do meio vem das margens já existentes).
- Suíte automatizada não reexecutada: a mudança é só um atributo de layout no XAML, que nenhum
  teste cobre.
- **Não verificado no Corel:** a legenda no docker real (`docs/mvp-smoke-test.md`, 4k passo 1
  cobre o cabeçalho; conferir também Camadas aberta num docker estreito).

## 37. Remover códigos da página — por código ou todos (após a dev.54)

Pedido do usuário: além de "Remover NO/NU da página" (mantido sem mudança), botões para remover
da página ativa um código por vez (NO, NU, AP, TS) e um "Remover todos os códigos
(NO/NU/AP/TS) da página".

- **Serviço:** `CodeToggleService.RemoveCodesRecursively(page, códigos…)`. A descida recursiva de
  `ExpandNoAndNuCodesRecursively` virou uma rotina única que recebe o mapa código → nome; o
  Remover NO/NU passa por ela com NO e NU, com a mesma mensagem de antes. Mesmas regras: todas as
  camadas da página ativa, grupos e PowerClips (inclusive aninhados), nome do contêiner nunca
  conferido, conteúdo dos textos nunca alterado, um passo de desfazer (`RunWithWorkspace`).
- **Nomes:** NO → "TextoNome", NU → "TextoNumero" (os da macro original); AP → "Texto Apelido",
  TS → "Texto Tipo Sanguíneo" — os nomes longos da alternância, **por decisão do usuário**: não
  existe macro de remoção para AP/TS (`CodeToggleService.PageRemovalName`).
- **Painel:** seção Códigos, abaixo de "Remover NO/NU da página": legenda "Remover da página
  ativa, um código por vez:", uma linha NO/NU/AP/TS e o botão "Remover todos…". Não entram
  nos Favoritos nesta etapa (o catálogo de favoritos não mudou). A legenda da alternância
  ("Alternar na seleção atual (NO = Nome, …)") também passou a quebrar linha — era cortada em
  300 px, como a de Camadas (36.1).

**Verificação**
- Testes automatizados reais (`CodeToggleServiceTests`, +3): cada código sozinho renomeia só ele
  (em grupo, PowerClip e em outra camada); todos de uma vez renomeiam os quatro e não tocam outros
  nomes ("Texto Nome" continua); página sem códigos dá 0; nenhum código pedido falha. Os testes
  existentes do Remover NO/NU continuam passando sem alteração.
- Painel compilado carregado fora do Corel (configuração isolada, localizador do Corel desligado,
  configuração real conferida por hash): em 300/360/440 px, a 100% e 150%, os quatro botões
  ficam na mesma linha e "Remover todos…" quebra o texto sem cortar (2 linhas em 300 px).
- **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4l).

## 39. Pasta de saída padrão, contraste da comparação e testes isolados (dev.58)

Registrada depois da build (a documentação desta rodada ficou pendente na entrega da dev.58).

- **Pasta de saída padrão desta máquina** (ROADMAP, "Tela de configuração no host", parcial):
  em Preparação e saída, "Definir pasta padrão…" (seletor de pastas) e "Voltar ao padrão
  original". `Integrations/OutputFolderConfigStore.cs` grava `output-folder.txt` na pasta de
  configuração DEV — dado local da máquina, no padrão de `NexorConfigStore`, fora das
  preferências portáteis. Ordem (`OutputPreparationService.ResolveOutputFolder`): pasta digitada
  no campo > padrão da máquina > `Z:\Neuber\Saídas`. "Destino" diz a origem (digitada / padrão
  desta máquina / padrão original) e quebra linha em vez de cortar. Os dois botões entram no
  catálogo de favoritos.
- **Contraste** (relato do usuário): a caixa de filtro da janela de comparação do ListForge
  Integrado (`ComparisonWindow`) só definia cores, sobre o template do Windows — texto claro
  sobre fundo claro, fechada e aberta. Reproduzido abrindo a janela real fora do Corel (com o
  resultado de comparação do helper dos testes) e corrigido com o template escuro de
  `ClassicWorkspaceWindow`. A caixa de Tipo de peça do painel já estava legível na dev.57
  (conferida aberta e fechada).
- **Testes sem gravar na configuração real:** `ListForgeIntegratedTests`, `ListForgePxModeTests`
  e `ManualOutputJsonEditingTests` constroem `ClassicWorkspaceWindow` e agora isolam a
  configuração numa pasta temporária (coleção "PXToolBox Dev shared filesystem resources"). A
  suíte completa não alterou o arquivo real (conteúdo e data de gravação conferidos).
  **Depois da dev.60 (só testes, sem build):** essas três classes também redirecionam o log, e o
  teste de falha do pacote de suporte passou a usar um log temporário — antes, cada execução da
  suíte escrevia "ListForge Integrado iniciado" e um erro de pacote de suporte no log REAL do dia.
  `PxLoggerTests`/`SupportPackageServiceTests` foram para a mesma coleção (uma classe só pertence a
  uma coleção). Duas execuções completas seguidas (138/138 net8.0 + 448/448 net48) não alteraram
  nenhum arquivo da pasta real de logs nem a configuração real.

**Verificação:** testes reais (resolução da pasta, store isolado, contagem do catálogo); suíte
138/138 net8.0 + 439/439 net48 no pipeline; painel e janela de comparação carregados fora do
Corel; `test-release.ps1` passou. **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4n).

## 38. Notas da versão (dev.59)

O botão **Notas da versão** fica no rodapé, fora das seções personalizáveis. Abre uma janela
modal WPF de consulta (`ReleaseNotes/ReleaseNotesWindow`), usando o mesmo `ResolveAppVersion()`
do número exibido no painel. Não consulta o Corel, documentos, configurações ou rede.

`ReleaseNotes/release-notes.json` é um catálogo editorial embutido no assembly DEV com
`LogicalName="PXToolBoxDev.ReleaseNotes.release-notes.json"`. `ReleaseNotesCatalog.ForVersion`
procura o número exato com comparação ordinal; nunca escolhe a versão mais recente nem presume
que uma build local corresponde à última release. Versão sem entrada ou local recebe aviso e
listas vazias. Cada entrada tem `implemented`, `changed` e `removed`; categorias vazias exibem
mensagem própria. Antes de gerar uma versão, seu registro deve ser acrescentado ao catálogo.

A janela tem rolagem vertical, texto com quebra e Fechar/Esc. Os `TextBlock` do `DataTemplate`
dos itens definem cor e quebra explicitamente: o estilo implícito da janela não atravessa esse
limite. A renderização inicial reproduziu texto preto/sem quebra; a verificação da árvore visual
falhou por cor `#FF000000`, e passou depois da correção.

Verificação nesta entrega:

- Revisão estática independente do catálogo, recurso, botão, janela e documentação.
- Compilação real Debug/Release; suíte automatizada completa: **138/138 net8.0 + 448/448 net48**,
  incluindo 9 casos do catálogo com ciclo de falha/sucesso observado. Único aviso da compilação
  Release: CS0649 preexistente em `CorelApplicationLocator._disabledForOffscreenChecks`.
- Janela compilada renderizada fora do Corel em 560×520 e 340×340, com entrada conhecida e build
  local; conferidos versão, visibilidade das mensagens, cor e quebra dos itens. Repetido com o
  assembly empacotado. Versão do assembly conferida com o manifesto: `0.1.0-dev.59`.
- Instalador DEV `.59` gerado e `test-release.ps1` passou (manifesto/checksums/pacote).
  Versões anteriores preservadas; nada publicado.
- **Smoke test real no CorelDRAW não executado** — roteiro em `docs/mvp-smoke-test.md`, 4m.

### 38.1 Notas da versão em Diagnóstico (dev.60)

Pedido do usuário: o botão saiu do rodapé e fica no início da seção Diagnóstico (mesmo estilo
de coluna dos demais botões da seção). Com isso passou a ser um botão de ferramenta como os
outros: Id `diagnostics.release-notes` no catálogo de favoritos (47 ferramentas) e oculto junto
com a seção se Diagnóstico for ocultado em "Personalizar painel…" (pelo atalho continua
funcionando). Entrada `0.1.0-dev.60` acrescentada ao catálogo de notas; a da dev.59 ficou como
estava (ela descreve o que a dev.59 fez).

**Verificação:** testes reais (catálogo de favoritos com 47; notas com versão sem entrada);
painel carregado fora do Corel — 1º botão de Diagnóstico é "Notas da versão", nenhum botão no
rodapé, 47 Ids batendo com o XAML; janela renderizada para `0.1.0-dev.60` com Implementado,
Alterado e Removido corretos. **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4m).

## 40. Aplicar contorno do Grid como ferramenta própria (dev.61)

ROADMAP: "`Utilidades.AplicaContornoGrid` — decidir com o usuário se/como portar". A macro já
estava portada, mas só como passo interno do Preparar Grid Novo (`ApplyGridOutlineStyle`). O
usuário pediu para seguir com este item deixando Grid Novo/Molde Novo para depois; a decisão
tomada foi expô-la como botão próprio, sem mexer no Preparar Grid Novo.

- **Serviço:** `GridPreparationService.ApplyGridOutline` — mesmo estilo (a mesma constante) e
  mesmo alcance da macro: todo objeto de primeiro nível de cada camada, em todas as páginas;
  grupo recebe o estilo como um objeto só, sem descer (fiel ao original). **Diferença
  deliberada:** camadas especiais do Corel (`IsSpecialLayer`: Guias, Área de trabalho, Grade)
  ficam de fora, para nunca pintar guias — a macro (e o passo interno do Grid Novo, que
  continua idêntico a ela) não filtra. Retorna quantos objetos receberam o estilo.
- **Painel:** seção Grid, "Aplicar contorno do Grid" (após Tamanho Padrão), um passo de
  desfazer (`RunOnDocument` → `BeginCommandGroup`). Id `grid.apply-outline` no catálogo de
  favoritos (48).
- **Não portadas:** `AplicaContornoReto`, `AplicaContornoLaser` e `AplicaContornoAmostra` (outros
  estilos, sobre a seleção) — candidatas se o usuário quiser.

**Verificação:** testes reais (`GridOutlineTests`: todas as páginas, grupo sem descer, camada
especial intocada, documento vazio, estilo idêntico ao do Preparar Grid Novo); painel
carregado fora do Corel (botão na seção Grid, 48 Ids batendo com o XAML). **Não verificado no
Corel** (`docs/mvp-smoke-test.md`, 4o).

## 41. Resumo do contexto de produção na seção Produção (dev.62)

Integração Nexor → PX (ROADMAP, item Nexor). Antes: o contexto carregado vivia só dentro da
janela de detalhes (`Production/OrderContextWindow`), aberta automaticamente ao carregar; o
painel não mostrava nada; carregar de novo com o contexto aberto só reativava a janela.

- **Contexto da sessão no painel:** `ControlUI._loadedOrderContext` (só memória, só consulta). A
  janela de detalhes passou a só exibir o mesmo objeto ("Ver detalhes"); o "Limpar" dela
  dispara `RequestClear` e o painel limpa a sessão.
- **Resumo** (`Production/OrderContextSummary.cs`, só dados do arquivo exportado): rolo (nome,
  número, máquina); trabalho de impressão (nome do arquivo de origem — só o texto, nunca
  acessado — ou, sem ele, o código do trabalho no Nexor), com tipo/tecido quando presentes;
  "Pedido informado" **só** quando o arquivo traz valor; data da exportação. Termos iguais aos
  da janela: nunca "pedido" como identificador, nunca "Job".
- **Ver detalhes / Limpar contexto:** botões na seção, desabilitados sem contexto; Limpar pede
  confirmação, esquece só o contexto da sessão (nada no Nexor, no arquivo, no Corel ou no
  ListForge) e fecha a janela de detalhes. Ambos favoritáveis (catálogo com 50).
- **Importação nova só substitui em caso de sucesso:** seletor de arquivo, "Colar caminho" e
  "Carregar outro contexto…" da janela — cancelar, erro de leitura ou validação falha mantêm
  o contexto anterior e o status diz isso. Carregar não abre mais a janela sozinho: mostra o
  resumo.
- **Contrato:** mesmo formato e esquema (1). **Correção de interoperabilidade:** o exportador
  real grava `pedidoLabel: ""` em trabalhos sem pedido, e o leitor recusava o arquivo inteiro
  ("Campo obrigatório ausente: job.pedidoLabel"). Agora vazio/ausente = não informado;
  `jobHash` e os demais obrigatórios continuam obrigatórios; arquivos antigos são lidos igual
  (`docs/integrations/nexor-order-context.md`).

**Verificação**
- Testes reais (`OrderContextSummaryTests`, +8: resumo completo, pedido vazio/espaços/ausente/
  null aceito e oculto, arquivo mínimo antigo, jobHash ainda obrigatório; testes antigos do
  leitor inalterados e passando).
- **Arquivos do exportador real do Nexor** (`modules/registros/px_export.py` do repositório
  ProjetoJocasta, sobre uma base printlogs temporária — nunca a real): um trabalho com pedido e
  outro sem (`"pedidoLabel": ""`). A dev.61 empacotada recusa o segundo; a versão nova aceita.
- Painel compilado fora do Corel (configuração isolada, localizador do Corel desligado,
  configuração real conferida por hash), em 300 e 440 px a 100% e 150%: sem contexto (resumo
  oculto, botões desabilitados) → com pedido (4 linhas, sem corte em 300 px) → importação
  inválida e cancelada (contexto anterior mantido, status explica) → sem pedido (linha do
  pedido oculta) → Ver detalhes (janela mostra "(não informado neste arquivo)") → Limpar
  (resumo oculto, botões desabilitados, janela fechada). Os diálogos de arquivo não foram
  abertos: foram usados o mesmo leitor e os mesmos métodos do painel chamados depois deles.
- **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4h).

### 41.1 Usar o contexto de produção no nome do layout (dev.63)

Escolha do usuário entre os próximos passos da integração Nexor: usar o contexto carregado nas
ferramentas (nada volta para o Nexor). Em Preparação e saída, "Usar contexto de produção"
preenche "Nome do layout" — que entra no arquivo salvo por Escudo/Bandeira/Faixa
("Escudo - <layout>.cdr").

- **Sugestão** (`OrderContextSummary.SuggestLayoutName`): o pedido informado; sem ele, o nome do
  arquivo de origem sem extensão (só o texto do caminho, nunca acessado); nenhum dos dois →
  botão desabilitado. Caracteres inválidos em nome de arquivo do Windows viram espaço, espaços
  repetidos são reduzidos e pontos/espaços nas pontas removidos; o resto do texto do Nexor fica
  como está.
- **Uso:** substitui o que estiver no campo e o status mostra o valor anterior. Habilitado só com
  contexto carregado (atualizado junto com o resumo da seção Produção). Favoritável
  (`output.use-context-layout-name`, catálogo com 51).

**Verificação:** testes reais (6 casos de sugestão: pedido, sem pedido → arquivo, caminho com
`/`, caracteres inválidos, nada aproveitável); painel compilado fora do Corel com os arquivos do
exportador real (com pedido → "Dryfit - Joao Silva" substituindo "layout antigo", com o
valor anterior no status; sem pedido → "sem-pedido"; depois de limpar o contexto, botão
desabilitado), em 300 e 440 px. **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4h).

### 41.2 Contexto de produção em mais ferramentas (dev.64)

Escolha do usuário: as quatro opções propostas. Continua só leitura do arquivo do Nexor —
nada volta para o Nexor.

- **Renomear Objetos:** "Usar contexto de produção" preenche o campo com a mesma sugestão de
  41.1 (`SuggestLayoutName`); renomear continua sendo o botão de sempre
  (`tools.objects.use-context-name`).
- **Página ativa:** "Nomear página ativa com o contexto" (seção Produção,
  `production.name-active-page`) usa `PageRenameService` (Outputs): nome vazio não altera
  nada; a mensagem traz o nome anterior; um passo de desfazer (`RunOnActivePage`). Preparar
  Escudo/Bandeira/Faixa continuam renomeando a página para o nome da saída.
- **Subpasta do rolo:** com contexto carregado e "Salvar na subpasta do rolo" marcada (padrão;
  não persistida), a pasta efetiva vira `<pasta resolvida>\<rolo>`
  (`OutputPreparationService.WithSubfolder` + `OrderContextSummary.RollFolderName`: nome do
  rolo limpo como nome de arquivo; sem nome, "Rolo <número>"). O "Destino" mostra a
  subpasta. Só a subpasta é criada (`Directory.CreateDirectory`, depois de preparar e antes
  de salvar); falha ao criar → mensagem "preparado, mas não salvo".
- **Rolo na mensagem:** saída salva com contexto carregado acrescenta
  `OrderContextSummary.OutputNote` ("Contexto: rolo X · trabalho Y") ao status e registra
  o mesmo em `PxLogger.Info("OutputSave", …)`.

Catálogo de favoritos: 53.

**Verificação:** testes reais (página: renomeia/igual/vazio; subpasta; nome da pasta do rolo;
nota da saída) — 152 net8.0 e 480 net48. Painel empacotado fora do Corel, com os arquivos do
exportador real: botões habilitam/desabilitam com o contexto, campo de objetos preenchido
com o valor anterior no status, "Destino" com e sem a subpasta e com pasta digitada,
"Nomear página" sem Corel só informa "não conectado", 53 Ids do catálogo = 53 botões.
**Não verificado no Corel:** renomear a página, criar a subpasta e salvar nela, mensagem e
log da saída (`docs/mvp-smoke-test.md`, 4h passo 7).

### 41.3 Resultado de execução PX → Nexor (dev.65)

Caminho de volta do contexto de produção, escolhido pelo usuário. Contrato em
`docs/integrations/px-execution-result.md` (`PxExecutionResult`, versão 1): arquivo .json
gravado manualmente ("Exportar resultado para o Nexor…", seção Produção,
`production.export-result`), com a identidade do trabalho copiada do contexto carregado e
as saídas salvas. **Sem status**: o Nexor continua dono do status e decide o que fazer.

- `Integrations/ExecutionResultService`: `Session` (registro em memória — mesmo trabalho
  mantém, outro trabalho/limpar esquece; mesmo arquivo substitui), `Build`, `KindFor`,
  `SuggestFileName`.
- `PrepareAndSaveOutput` registra cada saída salva com contexto (caminho de
  `BuildOutputFilePath`, igual ao usado por `SaveOutput`).
- Leitura no Nexor: não implementada (próxima etapa, no repositório do Nexor).

Também nesta versão (pedido do usuário): ícones de Favoritos das ferramentas "Remover … da
página" em vermelho com moldura (`ToolIcon.IsRemoval`, `Px.Removal`/`Px.RemovalSurface`).
Catálogo: 54.

**Verificação:** testes reais (contrato, datas em UTC, campos opcionais, sessão, nome do
arquivo, ícones de remoção); painel empacotado fora do Corel com arquivos do exportador
real do Nexor (ver relatório da entrega). **Não verificado no Corel** (`docs/mvp-smoke-test.md`,
4h passo 8).

### 41.4 Contextos de produção recentes (dev.67)

Produção → "Recentes…" (também favorito: `production.recent-contexts`; catálogo: 55).

- **Decisão do usuário (2026-11):** salvar só os CAMINHOS. Muda a regra anterior "o caminho
  nunca é salvo" (dica de "Colar caminho do contexto"); continua valendo que o CONTEÚDO do
  contexto nunca vai para o disco (seção 20) e que nada é carregado sozinho ao abrir o painel.
- `Integrations/OrderContextHistoryStore`: arquivo da máquina
  `%LocalAppData%\PXToolBox\Dev\config\order-context-history.json`
  (`PxToolBoxDevOrderContextHistory` v1: `path` + `loadedAtUtc` por item), fora das
  preferências portáteis. Mais recente primeiro, mesmo caminho sem diferenciar maiúsculas sobe
  para o topo, no máximo 10. Leitura nunca lança (ausente, corrompido, outra versão → vazio).
  Gravação por `AtomicFileWriter`.
- Registro: depois de cada carregamento bem-sucedido (seletor, "Colar caminho", recentes), em
  `SetLoadedOrderContext`. Falha ao gravar só avisa no status — não desfaz o carregamento.
- Escolher um item usa `OrderContextPathLoader.Load` (mesma validação do "Colar caminho"):
  relê o arquivo agora (vale a versão atual do `.json`), falha mantém o contexto anterior.
  Arquivo inexistente: pergunta se tira da lista (pasta de rede desconectada cai aqui — nunca
  remove sozinho).
- O menu abre no ponto do mouse (`PlacementMode.MousePoint`): pelo atalho dos Favoritos funciona
  mesmo com a seção Produção recolhida. O item do arquivo carregado agora aparece
  "(carregado)". "Limpar lista de recentes" só apaga a lista.

**Verificação:** testes reais da lista (ordem, repetição, limite, arquivo, campos gravados,
arquivos ruins) e painel empacotado fora do Corel com configuração isolada. **Não verificado no
Corel** (`docs/mvp-smoke-test.md`, 4h passo 9).

## 42. Comparação antes/depois no Diagnóstico (dev.68)

Primeiro passo do pedido "versões PX das ferramentas do SISBolt que o SISBolt reconheça"
(`docs/sisbolt-tools-investigation.md`). Só o contrato do Tamanho Padrão é conhecido; os outros
10 serão descobertos observando o que cada ferramenta muda — nunca descompilando o SISBolt
(produto comercial de terceiros).

- `Diagnostics/PageCaptureService` (COM, só getters): página ativa inteira — Properties de
  documento, página, camadas (`Page.AllLayers`, inclusive mestres) e de cada objeto, descendo em
  grupos e conteúdos de PowerClip; nome, tipo, camada, posição, tamanho, rotação, seleção,
  visível, bloqueado, texto (até 120 caracteres) e ObjectData preenchido. Limite de 3000
  objetos (avisa). Nunca lança: falhas viram aviso ou erro de leitura no item. Membros
  confirmados por reflection no `Corel.Interop.VGCore.dll` do CorelDRAW 27.
- `Diagnostics/PageCapture` (puro, testado): modelo e `PageCaptureDiff` — objetos casados por
  StaticID (novo/removido/alterado), propriedades por nome+ID (`+`/`-`/`~`), tolerância 1e-6 nos
  números; seção final com o estado completo dos selecionados depois.
- `PageCaptureReportWindow`: texto, campo "Ferramenta testada" (entra no relatório), Copiar,
  Salvar como .txt. Fora de `RunWithWorkspace`: nada para desfazer.
- Reutiliza a leitura de Properties do inspetor (`ShapePropertiesInspectorService.ReadEntry`,
  agora `internal`).

**Verificação:** testes da comparação; painel empacotado fora do Corel (botões, estado sem
conexão, janela do relatório com capturas montadas no teste). A captura real **não** foi
executada — precisa do Corel (`docs/mvp-smoke-test.md`, 4p).

### 42.1 Unidade da geometria nas capturas (dev.69)

Relato: na dev.68, a comparação do Tamanho Padrão mostrava diferença de fator 2,54 na
geometria. O relatório de 06/11 não estava disponível nesta máquina (nem salvo nem na área de
transferência); o caso foi reconstruído com os valores reais da seção 30 (StaticID 4,
2,99692125984252 × 2,93382677165354 pol = 7,612 × 7,452 cm).

- **Causa (reproduzida com a DLL da dev.68):** a captura gravava Position*/Size* brutos e
  comparava número com número. Esses valores vêm na unidade de `Document.Unit` — a unidade do
  modelo de automação, distinta da régua (seções 26-30: régua em cm, `Document.Unit` = cdrInch,
  Size* em polegadas). Se `Document.Unit` muda entre as capturas (uma ferramenta pode mudá-la),
  o mesmo objeto volta ×2,54 e aparecia como movido e redimensionado. A dev.68 também mostrava
  a geometria em polegadas ao lado de `KEEP_DESIGN_SIZE` em cm (régua), outro 2,54 aparente.
- **Correção:** cada captura registra separadamente a unidade da régua (`RulerUnitCode`,
  `Document.Rulers.HUnits`) e a unidade efetiva da geometria (`GeometryUnitCode`,
  `Document.Unit`, só lida), mais o tamanho bruto da página e `WorldScale`. `GeometryScale`
  converte cada lado para polegadas antes de comparar (tolerância 1e-6 pol) e mostra na
  unidade da régua da captura "depois", com os brutos ao lado. Só a geometria é convertida;
  rotação já é em graus; Properties e ObjectData continuam brutos.
- `CorelUnits`: só unidades de tamanho fixo (pol, mm, cm, m, km, 0,1 µm, pé, jarda, milha,
  ponto 1/72, pica 1/6). Pixel (depende de resolução) e didot/cícero/ágata/Q/H (definição no
  Corel não confirmada) são desconhecidas: com unidade desconhecida (ou não lida) em qualquer
  captura, a comparação geométrica é marcada INCONCLUSIVA e só os brutos aparecem, sem afirmar
  mudança.
- Mudança de `Document.Unit` ou da régua entre as capturas aparece como diferença do documento.
- **Não confirmado no Corel:** que Position*/Size* seguem `Document.Unit` quando ela NÃO é
  polegada (a evidência real só cobre `Document.Unit` = polegada com Size* em polegadas). O
  relatório mostra o tamanho da página convertido, para conferir na interface, e o roteiro 4p
  passo 7 tem a conferência direta. Até lá, é a premissa documentada do modelo de automação.

**Verificação:** 17 testes novos (caso do Tamanho Padrão em pol/cm sem redimensionar e com as
três entradas de `KEEP_DESIGN_SIZE`; movimento/redimensionamento reais continuam aparecendo,
inclusive com troca de unidade; unidade desconhecida → inconclusivo; tabela de unidades).
Reprodução com a DLL empacotada da dev.68 (mostra "Largura: 2.996921 → 7.61218") e o mesmo
caso com a correção (só `Documento.Unit` e as três entradas).

## 43. Ferramentas do SISBolt com contrato observado (dev.70)

Contratos vindos das capturas antes/depois do usuário (`docs/sisbolt-tools-investigation.md`,
que também lista o que segue pendente). Seção Grid, bloco "Ferramentas do SISBolt"; cada uma é
favoritável (catálogo: 62) e roda em `RunWithWorkspace` (um Ctrl+Z).

- `Outputs/Tools/SisboltMarkingService` + `SisboltMarking`: flag (nome, ID 0) = Boolean True.
  `STRETCH_PROPS` (qualquer objeto), `IGNORE_SISBOLT_VARIABLE_METRICS` e `FREE_HAND_PROPS` (só
  texto — cenário confirmado: texto NO). **Desde a dev.72 (pedido do usuário): mesmo
  comportamento do Tamanho Padrão** — `Toggle` na seleção (todos marcados → remove; senão →
  marca todos), `InspectSelection` com os mesmos estados (ativo/misto/inativo/indisponível/
  desconhecido) refletidos no botão por `Px.StateButton` (`ShowSelectionState`, compartilhado
  com o Tamanho Padrão; atualizado na troca de seleção, na conexão e depois de cada operação).
  Desligar REMOVE as entradas com o nome (como `KEEP_DESIGN_SIZE`); a captura da desativação no
  SISBolt ainda não existe — se mostrar False, só `Deactivate` muda. Entrada com False conta
  como inativa. Na dev.70 era só ativação, um objeto por vez. **dev.74:** nomes "Alternar
  Esticar no Tamanho do Molde", "Alternar Ignorar métricas da variável", "Alternar Mão Livre"
  (botão, atalho dos Favoritos e passo de Desfazer), no padrão de "Alternar Tamanho Padrão" —
  pedido do usuário. Ids dos favoritos inalterados. **dev.75:** Mão Livre só liga em objetos
  com nome exato NO, NU, AP ou TS (`SisboltMarking.AllowedNames`, a partir de `ObjectCode`;
  mesma comparação exata do alternar códigos) — regra do usuário; os códigos só existem em
  textos. Qualquer objeto fora da regra na seleção → nada alterado. Desligar não exige o nome
  (texto marcado e renomeado depois ainda pode ser desmarcado). Ignorar métricas e Esticar sem
  restrição de nome.
- `Outputs/Tools/SingleItemGroupService`: um objeto fora de grupos → `Shape.Group()` (o Corel
  agrupa um item só) → confere grupo com exatamente um filho, o original (mesmo StaticID)
  ANTES de nomear/ordenar/selecionar; diferente disso, para e pede Ctrl+Z. Nome
  `--- RENOMEAR TAMANHO ---`, provisório (uma captura só).
- `Outputs/Tools/HorizontalAlignmentLinkService` + `SisboltTools/HorizontalAlignmentLinkWindow`:
  dois objetos distintos; a janela mostra os papéis (troca e zoom na referência via
  `ActiveWindow.ActiveView.ToFitShape`, só a vista). Salvar: GUID `ToString("D")` em
  `GUID_IDENTIFIER_PROPS` [0] da referência e `ALIGNMENT_PROPS` [5] do alvo; [0]–[4] intocados;
  falha na segunda escrita remove a primeira. Referência que já tem GUID: recusado (não
  capturado).
- `ICorelShape` ganhou `DeleteProperty(nome, id)`, `StaticId`, `IsInsideGroup`, `GroupAlone`,
  `OrderToFront`, `SelectAlone` (membros confirmados por reflection no VGCore do Corel 27).
  `RunWithWorkspace` passou a devolver o resultado (a janela usa).
- Não implementados (capturas pendentes): Resetar ângulo, Alinhamento (âncoras/margens), Trocar
  de Posição, Replicar PowerClip, Formato de exportação. O PX não tinha ferramenta de zerar
  ângulo.

**Verificação:** testes com fakes (marcações preservando `KEEP_DESIGN_SIZE`/`ALIGNMENT_PROPS`,
geometria, nome; seleção errada não escreve; grupo com o original como único filho; estrutura
inesperada para antes de nomear; vínculo com o mesmo GUID, [0]–[4] preservados, rollback,
referência com GUID recusada) e painel empacotado fora do Corel. **Nada disso foi executado no
Corel** — nem a conferência das propriedades nem o reconhecimento na montagem
(`docs/mvp-smoke-test.md`, 4q).

## 44. Seções reorganizadas e Favoritos com nome (dev.73)

Pedido do usuário: reorganizar as categorias e um liga/desliga nos Favoritos entre só o ícone e o
botão inteiro com o nome. Escolha do usuário (entre três propostas): seção própria "Montagem" +
nomes curtos. Também: **"SISBolt" não aparece em nenhum texto visível do PX** (nomes, seções,
dicas, janelas, mensagens) — também registrado como regra de trabalho.

- `PanelSectionCatalog`: seção nova `assembly` "Montagem"; títulos "Objetos", "Tipo de peça",
  "Medidas" (Ids `tools.*` inalterados). Ordem padrão: Favoritos, Grid, Montagem, Códigos,
  Camadas, Objetos, Tipo de peça, Medidas, Preparação e saída, Produção, Listas, Diagnóstico.
- `PanelSectionLayout`: seção que o layout salvo não conhece entra logo DEPOIS da sua antecessora
  no catálogo (antes: no fim). Assim um layout da dev.72 ganha a Montagem depois do Grid sem
  perder a ordem do usuário. Sem antecessora no layout, entra no início.
- As cinco ferramentas da Montagem saíram do Grid (Ids `grid.sisbolt.*` mantidos — favoritos
  salvos continuam). Marcas do canto: Montagem "Mo", Medidas "Me" (regra de primeira e segunda
  letras), Objetos "O", Tipo de peça "T".
- Favoritos: CheckBox "Mostrar nomes" (`PanelFavoritesShowLabels`, campo aditivo na mesma
  configuração, gravado só pelo painel; o Save comum preserva o valor em disco). Com nome: estilo
  `Px.FavoriteLabelButton`, ícone + nome, um por linha na largura da seção (`ItemWidth` ligado à
  largura da coluna); o nome segue o Foreground do botão (estado ativo). Menu: "Mover para
  cima/baixo". `FavoriteDragReorder` reconhece a coluna única (`IsSingleColumn`) e usa os centros
  verticais e uma barra horizontal.

**Verificação:** testes (ordem padrão, inserção depois da antecessora, layout da dev.72,
marcas, nenhum rótulo/seção com "SISBolt" ou "Outras ferramentas", arraste em coluna, modo
salvo e preservado pelo Save comum) e painel compilado fora do Corel (seções, Montagem com as
cinco, nenhum texto visível com "SISBolt", modo com nome em largura cheia, arraste na coluna,
troca de modo gravada). **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4j passo 15).

## 45. Personalizar painel: nomes, seções criadas e lugar de cada ferramenta (dev.76)

Pedido do usuário: "Mostrar nomes" mais bonito e personalizar melhor — nomes das categorias e
onde cada botão vai. Entre "mover o botão de seção" e "grupos de atalhos", o usuário escolheu
**mover o botão de seção**.

- `PanelLayout/PanelCustomization` (puro, testado): `SectionTitles` (nome por Id de seção do
  catálogo), `CustomSections` (`custom.<id>`, nome), `ToolPlacements` (ferramenta → seção, na
  ordem). `SectionOf`/`MovedInto`/`Place`/`MoveUp`/`MoveDown`/`AddSection`/`RemoveSection`
  (ferramentas da seção removida voltam à origem)/`Rename` (vazio ou igual ao original volta ao
  original). `Normalize` tolera lixo e preserva ferramentas desconhecidas.
- **Como "mover" funciona:** o botão ORIGINAL fica na seção de origem, oculto (Collapsed), e a
  seção de destino ganha no fim um atalho completo (`FavoriteShortcut`, o mesmo dos Favoritos:
  clique, habilitação, estado e dica do original; estilo do original; menu de favoritos). Assim
  nenhuma ferramenta quebra — Escudo/Bandeira continuam lendo Nome do layout e Pasta da seção
  Preparação e saída, mesmo com ela oculta. Seções criadas são Expanders montados em código, com
  só os atalhos (vazia: texto de orientação).
- `PanelSectionLayout` aceita os Ids das seções criadas (`customSectionIds`): conhecidas para
  ordem/visibilidade, novas no fim, removidas descartadas (não confundidas com seções de uma
  versão mais nova). `ResetToDefault` mantém as criadas no fim.
- Marcas do canto calculadas pelo nome ATUAL (`PanelCustomization.ComputeBadges`): inicial;
  colisão → duas letras (sem acento); ainda → iniciais das palavras sem conectivos; ainda →
  inicial + a próxima letra que diferencia o grupo; só então número. Para os nomes originais dá
  a mesma tabela da dev.73 (G, Mo, Co, Ca, O, T, Me, PS, Pr, L, D). O atalho usa a marca da seção
  onde a ferramenta está.
- Gravação: campo aditivo `PanelCustomization` na mesma configuração, gravado JUNTO com o layout
  (`SavePanelCustomization`); padrão = campo removido; o Save comum (janelas do ListForge)
  preserva o valor em disco.
- Janela "Personalizar painel" refeita: Seções (exibir, nome editável, ▲▼, ✕ nas criadas,
  "+ Nova seção") e Ferramentas (agrupadas pela origem; botão com a seção atual abre menu escuro
  com os destinos — Favoritos não é destino —; ▲▼ entre as movidas para a mesma seção).
  "Restaurar padrão" zera tudo no rascunho. `ThreadKeyboardRouter` para digitar no Corel. Salvar
  não é mais o botão padrão (Enter num nome não salva a janela).
- "Mostrar nomes": estilo `Px.Switch` (trilho e bolinha; ligado em azul).
- Preferências portáteis: incluído desde a dev.78 (ver 45.2).

**Verificação:** 16 testes novos (modelo, marcas, layout com seções criadas, gravação e
preservação, janela) e painel compilado fora do Corel: seção criada na posição escolhida com as
ferramentas movidas, original oculto, atalho movido acionando o handler do original, seção
renomeada, marca "MD" para "Meu dia a dia", "Restaurar padrão" desfazendo tudo e removendo o
campo do arquivo. **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4k passo 12).

### 45.1 Grupos esvaziados e legibilidade da janela (dev.77)

Relato do usuário (print em `Referência/`): com "Alternar/Atualizar Tamanho Padrão" movidos
para outra seção, o Grid mostrava só o subtítulo "Tamanho Padrão" entre duas linhas — parecia
um botão desativado. `ControlUI.TidySectionStructure`: depois de aplicar as posições, cada
trecho entre linhas separadoras cujos botões de ferramenta foram TODOS movidos perde também o
subtítulo e o que mais só os continha; linhas no começo, no fim ou repetidas somem. Campos e
qualquer outro conteúdo mantêm o trecho. Tudo é desfeito na próxima aplicação (restaurar
devolve o Grid original).

Janela "Personalizar painel": no Corel os nomes saíam acinzentados. Agora texto mais claro
(#F2F2F4; apoio #B4B4BC), fonte 13, `TextFormattingMode=Display` só nesta janela (no painel
deixou a letra serrilhada na renderização e não foi aplicado), títulos "De …" em azul,
ferramentas movidas em azul e negrito, linhas alternadas, setas ▲▼ só nas movidas.

**Verificação:** painel compilado fora do Corel com o caso do usuário (Grid ficou "Preparar
Grid | linha | Aplicar contorno | Somar Altura"; restaurado volta a "botão linha rótulo botão
botão linha botão") e janela renderizada. **Não verificado no Corel.**

### 45.2 Personalização nas preferências portáteis (dev.78)

`PortablePreferencesService` (esquema 1, campos aditivos) passa a levar:

- `panelFavoritesShowLabels` (bool) — "Mostrar nomes" dos Favoritos.
- `panelCustomization` (`sectionTitles`, `customSections`, `toolPlacements`) — exportado
  SEMPRE, vazio quando o painel nunca foi personalizado, para que importar reproduza o painel de
  origem (inclusive desfazendo a personalização do destino). Arquivo sem a chave (versão
  anterior) mantém a personalização atual. Só ferramentas conhecidas são exportadas.
- `panelSectionLayout` aceita os Ids das seções criadas definidas no MESMO arquivo; o layout
  aplicado reconhece as seções criadas que valem depois da importação (as do arquivo ou, sem
  personalização no arquivo, as atuais). Personalização sem layout no arquivo: o layout atual
  deixa de citar seções criadas que deixaram de existir.
- Validação campo a campo: nome para seção desconhecida ou vazio, seção criada sem prefixo
  `custom.`/sem nome/repetida, ferramenta desconhecida, posição para seção inexistente e
  posição repetida (vale a primeira) são relatados ou ignorados sem derrubar o resto.
- Configurações: a importação aplica sobre os campos do painel lidos do disco naquele momento;
  "Salvar" grava "Mostrar nomes"/personalização só quando a importação os trouxe
  (`includePanelFavoritesShowLabels`/`includePanelCustomization`), e o painel relê na hora
  (`PanelFavoritesChanged`/`PanelSectionLayoutChanged`). Exportar usa sempre o que está em disco.

**Verificação:** 6 testes novos (ida e volta, exportação padrão desfazendo a do destino, arquivo
antigo mantendo, layout sem personalização mantendo as seções criadas, entradas inválidas,
resumo). **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4g passo 6).

## 46. Roteiro de capturas (dev.79)

Para fechar a coluna "Captura pendente" de `docs/sisbolt-tools-investigation.md` sem depender de
mensagens soltas:

- `Diagnostics/CaptureChecklist.cs`: 27 itens com Id estável (ex.: `stretch.off`,
  `alignment.anchor.top-right`), ferramenta, variação, o que preparar e o que fazer na
  ferramenta original. Texto visível sem o nome do produto de origem (regra do usuário).
- `Diagnostics/CaptureReportStore.cs`: pasta `%LocalAppData%\PXToolBox\Dev\capturas`, um
  arquivo por relatório (`<id>__aaaaMMdd-HHmmss.txt`, sufixo `-2`… no mesmo segundo; `livre__`
  sem item). Os ✓ vêm dos nomes dos arquivos; arquivo estranho na pasta é ignorado.
  `SetDirectoryForTesting` isola os testes.
- Diagnóstico → **Roteiro de capturas…** (`diagnostics.capture-checklist`, nos Favoritos
  também): janela única, não modal, lista com ✓/○, detalhe em 4 passos e "Abrir pasta das
  capturas". O item escolhido vira `CaptureChecklistWindow.CurrentItemId`.
- Comparação antes/depois: "Item do roteiro" (menu escuro) preenche "Ferramenta testada" (texto
  digitado à mão é mantido); **Salvar na pasta de capturas** grava e passa o item atual para o
  próximo pendente. Copiar e "Salvar como .txt…" continuam.

Nada disso lê ou altera o documento — a captura continua sendo a da dev.68.

**Verificação:** 9 testes novos (itens, texto sem o nome do produto, cobertura das ferramentas
pendentes, pasta sem sobrescrever, arquivos estranhos, as duas janelas) e as duas janelas
renderizadas fora do Corel com pasta isolada (dados reais conferidos por hash). **Não
verificado no Corel** (`docs/mvp-smoke-test.md`, 4p passo 8).

## 47. Resetar ângulo (dev.80)

Pedido do usuário: o "Zerar ângulo" do MoldMotor já resolve — então o mecanismo dele (validado
no CorelDRAW) foi **portado** para o PX, sem dependência do MoldMotor (AGENTS.md: identidade,
licença, configuração e instalação continuam separadas; nada na UI cita o MoldMotor).

- `Outputs/Tools/RotationResetService.cs` (testável com fakes) + `Abstractions/CurveGeometry.cs`
  e `IShapeArrangement` (Group/Select, implementado por `CorelWorkspaceAdapter`). Membros novos
  em `ICorelShape`: `RotationAngle`, `ClearRotation`, `ReadCurveGeometry`/`WriteCurveGeometry`,
  `ConvertToCurves`, `Duplicate`, `ReadAllProperties`, `IsInFrontOf`, `OrderFrontOf`,
  `OrderToBack`, `Ungroup`.
- **Curva**: lê nós e pontos de controle (coordenadas de página, por índice de segmento), zera a
  rotação, regrava o que leu e confere ângulo, caixa e geometria relidos (tolerância 0,01 mm
  convertida com `Application.ConvertUnits`). Cópia de segurança (`Duplicate(0,0)`) antes; se a
  conferência falhar, o objeto alterado sai e a cópia fica no lugar.
- **Retângulo/elipse girados**: `ConvertToCurves` (conferido: o tipo tem de virar curva) e o
  caminho da curva. **Texto, bitmap, polígono e outros: não mexe** — converter texto acabaria
  com a variável NO/NU/AP/TS (o MoldMotor converte texto artístico com confirmação; o PX não).
- **Grupo em 0°**: filhos no lugar, ordem conferida por StaticID e restaurada só se mudou.
  **Grupo girado**: desagrupa, trata os filhos, reagrupa os mesmos objetos, restaura ordem
  interna e a posição na camada (logo à frente de quem estava atrás). Grupo girado dentro de
  outro grupo: não mexe.
- **Diferença deliberada da ferramenta original** (captura dev.68: curva nova, sem Properties):
  nome e todas as Properties (Tamanho Padrão, Esticar…) são regravados no que ficar no lugar
  (objeto convertido, cópia de segurança, grupo refeito). Um objeto já em 0° não é tocado.
- Resultado por contagem; grupo com algo ainda girado é "em parte", nunca "zerado". Tudo num
  Ctrl+Z.

As duas capturas de Resetar ângulo saíram do roteiro (`CaptureChecklist`, agora 25 itens).

**Verificação:** 15 testes novos com fakes (curva, conferência reprovada → cópia restaurada com
Properties, retângulo convertido sem perder Properties, conversão que não acontece, texto
intocado, grupos em 0° e girados, parcial, grupo dentro de grupo, ângulos 360°). **Não
verificado no Corel** (`docs/mvp-smoke-test.md`, 4q passo 9) — em especial: `UngroupEx`/
`ShapeRange.Group` dentro da camada certa, e se `ConvertToCurves` mantém as Properties sozinho.

## 48. Padrão antigo e padrão novo de camadas (dev.81)

Pedido do usuário: as macros configuram molde e Grid no padrão antigo; o MoldMotor, no padrão
novo; o PX faz os dois e converte o documento aberto. Desenho confirmado pelo usuário
(2026-11-09; o laser continua "x"):

```
Molde  antigo: CAM1.Frente.MA.Frente → grupo G → Tag, Frente, x
       novo:   CAM1.MOLDE.MA.Frente  → grupo G → Tag, Frente, x      (só a camada muda)
Grid   antigo: CAM1.Frente.MA → Frente          (uma camada por peça)
       novo:   CAM1.MOLDE.MA  → Frente, Costa, L1…, Frente_RECT, Costa_RECT (uma por categoria)
```

- `Outputs/Tools/LayerStandardService.cs`: nomes (`MoldLayerToNew/Old`, `IsOld/NewGridLayer`),
  `GridToNew` (move os objetos para `CODE.MOLDE.CAT`; a peça é o objeto com o nome da peça, o
  maior se houver vários; cria `<peça>_RECT` com a caixa dela via `ICorelLayer.CreateRectangle`
  → `Layer.CreateRectangle2`; retângulos no fundo), `GridToOld` (peças = as que têm `_RECT`;
  logos e demais objetos vão para a peça cuja caixa contém o centro deles, ou a mais próxima,
  sem renomear; `_RECT` apagado) e `ConvertDocument` (todas as páginas; há camada antiga → vai
  para o novo, senão volta para o antigo).
- Documento com camada de 4+ segmentos é molde: as de 3 segmentos (molde ainda não configurado)
  nunca são tratadas como Grid nele.
- `MoldPreparationService.PrepareNewMold(page, standard)`: novo → `seg0.MOLDE.seg2.peça` e logos
  `L1..Ln` mantêm o nome. `GridPreparationService.PrepareNewGrid(doc, standard)`: sempre volta
  camadas de molde no padrão novo para o antigo antes de reduzir a 3 segmentos; no novo, mantém os
  nomes dos logos e termina com `GridToNew` + ordenação das camadas. O padrão antigo é o padrão
  dos parâmetros: os botões de antes fazem o mesmo de sempre.
- Proteção: `RenameShapesAfterFirstDot` (Preparar Grid) pula camadas `CODE.MOLDE.CAT`.
- `FakePage.Layers` deixou de listar camadas excluídas (como no Corel); um teste antigo que só
  passava por causa disso ganhou um objeto na camada.

O Grid novo do MoldMotor também gera o arquivo de Grid a partir do documento de moldes (copiando o
tamanho G) — isso **não** foi feito: o PX prepara o documento aberto, como as macros.

**Verificação:** 14 testes novos com fakes (nomes, ida e volta do molde e do Grid, retângulos no
fundo com a caixa da peça, logos com a peça certa, molde não configurado não vira Grid, as
variantes de Preparar Molde/Grid Novo). **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4r)
— em especial `CreateRectangle2` (unidade e posição) e se o reconhecimento pela montagem aceita
o resultado.

## 49. Cadastro de moldes — fase 1 (dev.82)

Pedido do usuário: trazer as ferramentas do MoldMotor para o PX, **sem os assistentes**,
**reescritas no padrão do PX** (não uma cópia do motor), com **configurações próprias começando
do zero**, as simples primeiro. Fases: 1) as ferramentas abaixo; 2) configurações (tamanhos,
categorias, contorno laser, TAG, tipos de peça) com tela própria; 3) Definir objetos, Detectar em
lote, Propagar logos, duplicar/espelhar; 4) Salvar molde. (Zerar ângulos já entrou na dev.80 como
Resetar ângulo; Renomear objetos selecionados já existia em Objetos.)

Seção nova **Cadastro de moldes** (id `molds`, logo depois de Montagem; marca "Cm").

- `Core/Naming/MoldSizeCategories.cs`: tamanhos padrão — MA PP…XXGG, FE BLPP…BLXXGG, C 2A…12A;
  tamanho do Grid G / BLG / maior infantil. Substituído pelas configurações na fase 2.
- `Outputs/Tools/MoldPieceRecognizer.cs`: peça = o que sobra no grupo de tamanho depois de TAG,
  laser (`x`, `LASER`), logos `L1..Ln` e `IDENTIFICADOR_TAMANHO`; mais de um → desempata pelo
  nome esperado ou recusa. Só por nome (o metadado em `Shape.Comment` do MoldMotor não é lido).
- `Outputs/Tools/MoldToolsService.cs`:
  - **Criar camadas para a seleção**: grupo inteiro → `TIPO.MOLDE.CAT.PEÇA` (cria visível e
    desbloqueada; camada duplicada → recusa). Tamanho desconhecido, peça ambígua ou com espaço
    → não move e explica.
  - **Maiúsculas**: peça e camada `.MOLDE.`; o laser **não** muda (o MoldMotor passava o alias
    para maiúsculas — no PX o laser continua `x`, decisão do usuário).
  - **Excluir camadas vazias** (página ativa, sem as especiais).
  - **Ordenar e distribuir**: área maior/menor primeiro; fila vertical (de cima, X do primeiro)
    ou horizontal (da esquerda, Y do primeiro); distanciamento em mm convertido com
    `Application.ConvertUnits` para `Document.Unit`, ou automático (menor dimensão); ordem na
    camada acompanha.
  - **Criar Grid**: camadas de 4+ segmentos (padrão antigo ou novo); por peça, o grupo G/BLG/maior
    infantil, mantendo só peça, logos e identificador; cópias numa camada por categoria
    `TIPO.MOLDE.CAT`, desagrupadas, com `PEÇA_RECT` (caixa da peça) no fundo; `SaveAsCopy` só da
    seleção (`ICorelDocument.SaveSelectionAsCopy`); cópias e camadas criadas apagadas sempre; o
    host abre o arquivo depois. Nome sugerido: documento + `_GRID`.
  - **Selecionar RECT**: tudo com `_RECT` no nome (camada e dentro de grupos).
- `FakeShape.Duplicate` agora copia os filhos de um grupo (como o Corel).

**Verificação:** 14 testes novos com fakes e o painel renderizado fora do Corel (pasta real do
PX DEV sem nenhuma gravação). **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4s) — em
especial Criar Grid (SaveAsCopy da seleção e abertura do arquivo).

## 50. Cadastro de moldes — fase 2: configurações (dev.83)

Configurações **próprias do PX**, começando dos valores de fábrica (decisão do usuário: sem
importar do MoldMotor; AGENTS.md: nunca o arquivo dele).

- `Core/Molds/MoldSettings.cs` (puro): `MoldSizeCategory` (código + tamanhos base, prefixos,
  sufixos → tamanhos = prefixo + base + sufixo, mesma ordem e regra do MoldMotor), tamanho base
  do Grid (G → G/BLG; infantil = maior tamanho), nomes do laser (`x`, `LASER`), da TAG e do
  identificador; `Default`, `CategoryOf`, `CanonicalCategory`, `GridSizesOf`, `Validate`
  (códigos preenchidos, sem ponto/espaço e distintos; tamanho em só uma categoria; laser, TAG e
  base do Grid preenchidos) e `NormalizeList` (vírgula, ponto e vírgula, espaço ou linha;
  maiúsculas; sem repetição). Substitui `MoldSizeCategories` da dev.82.
- `Integrations/MoldSettingsStore.cs`: `%LocalAppData%\PXToolBox\Dev\config\mold-settings.json`
  (schema 1, gravação atômica). Sem arquivo → fábrica; ilegível, versão desconhecida ou inválido →
  fábrica + aviso (nunca derruba uma ferramenta). `SetFilePathForTesting`.
- `Molds/MoldSettingsWindow`: três colunas (Masculino, Feminino, Infantil) com prévia dos tamanhos
  gerados, Grid com prévia do que é copiado, objetos do grupo; problemas em laranja; Salvar só
  grava o que é válido; Restaurar padrão preenche os campos (vale ao Salvar). Mesmo
  ThreadKeyboardRouter das outras janelas.
- `MoldToolsService(Func<MoldSettings>)` e `MoldPieceRecognizer.Recognize(group, settings)` leem
  as configurações a cada uso; o painel passa `MoldSettingsStore.Load().Settings`.
- Ainda **não** vão nas preferências portáteis (Exportar/Importar); dá para incluir depois. (Incluídas na dev.89 — seção 56.)

**Verificação:** 11 testes novos (modelo, combinação, validação, ferramentas com nomes e tamanhos
configurados, arquivo ida e volta, arquivos quebrados, janela) e a janela renderizada fora do
Corel com arquivo isolado (dados reais conferidos por hash: 10 arquivos, iguais). **Não
verificado no Corel** (`docs/mvp-smoke-test.md`, 4s passo 8).

## 51. Cadastro de moldes — fase 3a: Definir objetos (dev.84)

Reescrita simples do "Definir objetos" do MoldMotor (`DetectObjectsUseCase` +
`MoldStructurePlanner` + `ApplyMoldStructure`), no padrão do PX.

- `Outputs/Tools/MoldDefineService.cs` → `Define(seleção, página, arranjo, peça, tamanho)`:
  - Tudo é validado antes de mexer no documento (nome sem ponto/espaço, tamanho das configurações,
    peça identificável); recusa → "Nada foi alterado".
  - Peça = maior objeto pela caixa; segundo maior com ≥ 98% da área → ambíguo, recusa. Grupo, texto
    ou bitmap como peça → recusa (não recebem contorno de laser).
  - Logos = os demais. `L<n>` já existente fica. Os outros: correspondência pela posição relativa à
    caixa da peça (tolerância 0,2) com outro grupo da mesma peça na página que já tenha logos (o
    MoldMotor usa a peça de referência do tamanho central e casamento geométrico mais completo);
    sem correspondência → maior `L<n>` da página + 1, de cima para baixo e da esquerda para a direita.
  - Grupo resultante, da frente para trás: TAG, logos, identificador, peça, laser; nome = tamanho;
    fica selecionado.
  - Laser: `Duplicate` da peça com o nome do laser e o contorno (contínuo, centralizado, cantos
    vivos). Desligado nas configurações: o contorno vai na própria peça (como o MoldMotor sem
    contorno interno).
  - TAG: `Layer.CreateArtisticText` (Arial Black, ou Arial se não instalada), preenchimento preto,
    contorno branco arredondado atrás do preenchimento e escalável; centro X da peça, base na base
    da peça (posição "embaixo, centro" do MoldMotor com margens 0).
  - Um único grupo selecionado = redefinir: desagrupa, apaga TAG e laser antigos (por nome),
    mantém logos e identificador.
  - Objetos comparados por `StaticId` (no Corel cada leitura cria um adaptador novo).
- Abstrações novas: `ShapeOutline`, `ICorelShape.SetOutline` / `SetUniformFill`,
  `ICorelLayer.CreateArtisticText`. O host converte a espessura de mm para a unidade do documento
  (`Outline.Width`, como o MoldMotor) e fixa o estilo contínuo com `OutlineStyles[0]`.
- `MoldDefineOptions` dentro de `MoldSettings` (bloco `define` no `mold-settings.json`, opcional:
  arquivo da dev.83 → valores de fábrica): criar cópia do laser, cor `#RRGGBB`, espessura; criar TAG,
  fonte em pt, contorno em mm. A janela de configurações ganhou o bloco correspondente.
- Fora desta versão: contorno interno com cor por tamanho, posição/margens da TAG, metadado de papel
  em `Shape.Comment`, Detectar em lote, Propagar logos, duplicar/espelhar.

**Verificação:** 12 testes novos (grupo montado e ordem, laser e TAG, numeração herdada de outro
tamanho, numeração da página, redefinir, sem laser/TAG, recusas sem alteração, validação das
opções, arquivo antigo, janela). Janela e painel renderizados fora do Corel com arquivos isolados
(dados reais conferidos por hash: 10 arquivos, iguais). **Não verificado no Corel**
(`docs/mvp-smoke-test.md`, 4t) — em especial `CreateArtisticText` com fonte nomeada, a espessura do
contorno na unidade do documento, a posição da TAG e se os adaptadores continuam válidos depois do
`UngroupEx` ao redefinir.

## 52. Cadastro de moldes — fase 3b: Detectar objetos em lote (dev.85)

`MoldDefineService.DefineBatch(seleção, página, arranjo, peça, categoria)`. O `Define` da dev.84 foi
dividido em `Analyze` (decide, sem alterar) e `Apply` (monta o grupo); o lote usa os dois.

- **Conjuntos** (diferente do MoldMotor, que corta a árvore geradora mínima das distâncias entre
  caixas): grupo selecionado com nome de tamanho da categoria = um conjunto com tamanho conhecido
  (âncora; é redefinido). Objetos soltos, do maior para o menor: entra no menor conjunto cuja caixa
  contém o centro dele; senão começa um conjunto. Funciona porque os logos ficam dentro da peça;
  logo fora da peça vira um conjunto próprio (e o lote acaba recusado pela contagem ou pela análise).
- **Tamanhos** (regra do `ClusterSizeResolver` do MoldMotor): ordena pela área da peça; com âncoras,
  desloca a ordem para que a âncora caia no tamanho dela (âncoras conflitantes → recusa); sem âncora,
  só a grade completa da categoria. Peças quase do mesmo tamanho (≥ 98%) em dois conjuntos → recusa.
- Tudo é analisado antes; um problema recusa o lote inteiro. Aplicação do maior para o menor: o
  primeiro ganha números novos (ou herda de um grupo já existente), os demais herdam dele pela
  posição. No fim, todos os grupos ficam selecionados, na ordem dos tamanhos.
- Painel: combo **Categoria** (com os tamanhos de cada uma) e o botão **Detectar objetos em lote**
  (`molds.define-batch`); o nome da peça é o mesmo campo do Definir objetos.
- Fora: duplicar peças junto do lote, Propagar logos.

**Verificação:** 3 testes novos (grade completa sem âncora, com um grupo já definido, recusas sem
alteração) e painel renderizado fora do Corel. **Não verificado no Corel** (`docs/mvp-smoke-test.md`,
4u). Na conferência dos dados reais, `listforge-integrated-settings.json` mudou às 13:04:40 — gravado
pelo PX instalado, com o CorelDRAW aberto pelo usuário às 13:03:25; os outros 9 arquivos, iguais.

## 53. Cadastro de moldes — fase 3c: Propagar logos (dev.86)

`Outputs/Tools/MoldLogoPropagationService.Propagate(seleção, arranjo, tamanho do painel)`, reescrita
simples do `PropagateLogosUseCase` + `LogoPropagationService` do MoldMotor.

- Seleção: só grupos de tamanho já montados (nome = tamanho das configurações; peça reconhecida por
  `MoldPieceRecognizer`). Objeto solto, peças com nomes diferentes, tamanho repetido ou `L<n>` repetido
  num grupo → recusa sem alterar nada. (O MoldMotor também aceita conjuntos soltos — aqui, monte antes
  com Definir objetos.)
- Referência: o grupo com mais logos. Empate → o tamanho escolhido no painel (o mesmo campo do Definir
  objetos; o MoldMotor guarda uma referência registrada por documento); fora do empate → recusa.
- Geometria igual à do MoldMotor: escala única = altura da peça de destino ÷ altura da peça de
  referência; centro do logo = centro da peça de destino + (centro do logo − centro da peça de
  referência) × escala; largura e altura × escala. O host aplica tamanho e depois corrige o centro
  (`SetSize` pode deslocar).
- Logo existente no destino (mesmo nome): movido e redimensionado. Logo que falta: `Duplicate` do logo
  da referência, `MoveToLayer` (sai do grupo da referência), frente; o grupo de destino é desfeito e
  refeito com o mesmo nome, TAG na frente. Logos do destino que a referência não tem ficam como estão
  (e a mensagem diz).
- `FakeShape.MoveToLayer` agora tira o objeto do grupo pai, como no Corel.
- Painel: botão **Propagar logos** (`molds.propagate-logos`, glifo E16F), depois do lote.

**Verificação:** 3 testes novos (criar e ajustar com a escala, desempate pelo tamanho do painel,
recusas sem alteração). **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4v) — em especial se a
cópia de um logo dentro de um grupo sai dele com `MoveToLayer` e a posição depois do `SetSize`.

## 54. Cadastro de moldes — fase 3d: Duplicar peça (dev.87)

`Outputs/Tools/MoldDuplicationService.Duplicate(seleção, página, arranjo, nome da cópia, direção,
distância, espelhar)`, reescrita simples da duplicação de peças do MoldMotor
(`PieceDuplicationService` + `ApplyPieceDuplication`). No MoldMotor a configuração fica guardada por
peça; no PX ela é digitada no painel a cada uso (nome, direção, distância em mm, espelhar).

- Seleção: grupos de tamanho já montados da mesma peça (mesmas recusas do Propagar logos); nome da
  cópia obrigatório, sem ponto/espaço e diferente do nome da peça.
- Cada grupo: `Duplicate` do grupo inteiro (nome = tamanho); a peça da cópia recebe o nome da cópia;
  logos renomeados pelo mapa da família; posição: a peça da cópia fica com a mesma caixa da original,
  deslocada largura/altura + distância na direção (`PlaceCopyPiece` do MoldMotor), movendo o grupo.
- Espelhar: `Shape.Flip(cdrFlipHorizontal)` só na peça e nos lasers da cópia, cada um voltando ao
  próprio centro (como `FlipShapeAroundOwnCenter`); TAG e logos não.
- Logos (regra do MoldMotor): a cópia é outra família, então ganha números próprios, iguais em todos
  os tamanhos e na ordem dos da original: os da cópia já existente na página, se não colidirem com os
  da original; senão, maior `L<n>` da página + 1….
- Repetir com o mesmo nome: o grupo do mesmo tamanho com a peça da cópia (fora da seleção) é apagado
  e refeito — nunca duas cópias. Duas cópias do mesmo tamanho já na página → recusa.
- Abstração nova: `ICorelShape.FlipHorizontally`. Fakes: mover um grupo move os filhos (como no Corel).
- Painel: nome da cópia, direção, distância (mm, padrão 10, convertida para a unidade do documento
  como no Distribuir), "Espelhar a cópia" e o botão **Duplicar peça** (`molds.duplicate`, glifo E8F1).

**Verificação:** 3 testes novos (cópias espelhadas ao lado com números novos e original intacta,
repetir substitui e mantém os números, recusas sem alteração). **Não verificado no Corel**
(`docs/mvp-smoke-test.md`, 4w) — em especial o `Flip` (centro, contorno do laser) e o `Duplicate` de
um grupo com TAG de texto.

## 55. Cadastro de moldes — fase 4: Salvar molde (dev.88)

`Outputs/Tools/MoldSaveService`, reescrita do "Salvar molde" do MoldMotor (`MoldSaveFileName` +
`SaveMold` do docker), sem gravação em banco (o MoldMotor também não tem).

- `TryCreateFileName`: o nome digitado (sem espaços nas pontas; com ou sem `.cdr`) vira o arquivo
  sugerido; vazio ou com caractere inválido de arquivo → mensagem, nada é salvo.
- `InitialFolder`: a pasta do documento, se ele já foi salvo e a pasta existe; senão a pasta de
  moldes de sempre (`C:\SISBolt\molds`, a mesma do MoldMotor), só se já existir — o PX não cria
  pasta; senão a janela decide.
- `Save`: só `.cdr`; `ICorelDocument.SaveAsCdr` (o mesmo das saídas: todas as páginas, sem VBA) —
  como o `SaveAs` do MoldMotor, o documento aberto passa a ser o arquivo salvo. A janela do Windows
  pergunta antes de sobrescrever.
- Painel: "Nome do molde:" e **Salvar molde…** (`molds.save`, glifo E74E), no fim da seção.

**Verificação:** testes do nome, da pasta inicial e do salvamento só em .cdr (fakes). **Não
verificado no Corel** (`docs/mvp-smoke-test.md`, 4x).

## 56. Configurações de moldes nas preferências portáteis (dev.89)

A dev.83 deixou as configurações de moldes fora de Exportar/Importar preferências (seção 50). Agora
`PortablePreferencesService` leva o campo aditivo `moldSettings` no mesmo esquema 1, com o mesmo
JSON do `mold-settings.json` (`MoldSettingsStore.ToJson`/`FromJson`).

- Exportar: a janela passa as configurações em vigor (`MoldSettingsStore.Load().Settings`, ou as já
  importadas nesta tela) para `Export(settings, moldSettings)`; o resumo ganha uma linha.
- Importar: `Validate` lê com `FromJson` + `MoldSettings.Validate`; qualquer problema (esquema,
  tipo, valor) → uma linha em "incompatíveis" e o campo inteiro é ignorado (nunca meio aplicado);
  sem a chave, mantém as atuais. O backup antes de importar também leva as de moldes.
- Gravação: só no "Salvar" da janela, depois das demais (arquivo próprio); se falhar, a janela
  avisa que o resto foi salvo e fica aberta para tentar de novo.
- Textos da dica do painel e da janela de configurações de moldes atualizados ("vão junto em
  Exportar/Importar preferências").

**Verificação:** 6 testes novos (ida e volta, arquivo sem a chave, inválidas ignoradas com o resto
importado). Nada disso depende do Corel; **não verificado** clicando na janela real
(`docs/mvp-smoke-test.md`, 4y).

## 57. Cadastro de moldes — posição e margens da TAG (dev.90)

Reescrita da posição da TAG do MoldMotor (`TagPosition`, `TagSettings`, `TagSpec.ResolveCenter`,
`PositionTag` do adapter), sem o editor em grade: um ComboBox com as nove posições.

- Core: `MoldTagPosition` e `MoldTagPlacement.ResolveCenter` (regra do MoldMotor: esquerda = borda
  esquerda da peça + margem esquerda; direita = borda direita − margem direita; topo/base idem;
  centro deslocado por esquerda − direita e inferior − superior). `MoldDefineOptions` ganha a
  posição e as quatro margens em cm (0–100), com fábrica = inferior centro, margens 0 — o mesmo
  lugar de antes, então quem não mexer não vê diferença.
- `MoldDefineService(settings, centimetersToDocument)`: o painel converte cm para a unidade do
  documento com `Application.ConvertUnits(…, cdrCentimeter, ActiveDocument.Unit)` (como o mm do
  Distribuir e do Duplicar peça). Vale para Definir objetos e Detectar objetos em lote.
- `mold-settings.json`: `tagPosition` (nome) e `tagLeft/Right/Top/BottomMarginCm` no bloco `define`;
  arquivo anterior ou nome desconhecido → fábrica (como o MoldMotor). Por isso também viajam nas
  preferências portáteis (seção 56).
- Janela: posição, margens e uma frase dizendo onde a TAG fica ("no topo, encostada na esquerda,
  deslocada 1,5 cm para a direita").

**Verificação:** 16 testes novos (as nove posições com margens, faixa das margens, TAG posicionada
no Definir objetos com conversão de unidade, arquivo, janela). **Não verificado no Corel**
(`docs/mvp-smoke-test.md`, 4z) — em especial a caixa do texto artístico (Width/Height) usada para
encostar a TAG nas bordas.

## 58. Cadastro de moldes — contorno interno por cor de tamanho (dev.91)

Reescrita do "contorno interno de identificação" do MoldMotor (`InternalOutlineSettings`,
`SizeColorSettings`, `SizeColorPalette`, `CategoryLineStyleSettings` e o `ApplyOutline` do adapter).

- Papel dos objetos, como no MoldMotor: a peça (com o nome da peça) recebe o contorno interno e a
  cópia (nome do laser) o contorno do laser. No PX a cópia do laser já existe ("Criar a cópia do
  laser"), então a validação exige que ela esteja ligada junto com o contorno interno.
- Core: `MoldInnerOutlineOptions` (ligado, espessura em mm, estilo por categoria
  contínuo/tracejado/pontilhado, cores cadastradas por tamanho) dentro de `MoldSettings`;
  `InnerOutlineColorOf(tamanho)` = cadastrada, senão a da paleta do MoldMotor pela posição do tamanho
  na categoria (o MoldMotor sugere a mesma paleta, mas recusa tamanho sem cor gravada — aqui a
  sugestão vale direto). Validação: espessura 0–10 mm, cores `#RRGGBB`, cor de tamanho inexistente.
- `ShapeOutline` ganhou `Dash` e `Inside`. Adaptador: `Justification = Inside`; tracejado/pontilhado
  por `Application.CreateOutlineStyle(1, {8,4})` / `{1,3}` a cada aplicação (como o MoldMotor — os
  itens 1+ de `OutlineStyles` são do usuário); contínuo = `OutlineStyles[0]`.
- Desligar depois não remove o contorno interno já aplicado (não se sabe qual era o contorno
  original da peça); redefinir com ele ligado reaplica.
- `mold-settings.json`: bloco `innerOutline` (ausente = desligado); viaja nas preferências portáteis.
- Janela: bloco próprio com espessura, três estilos, cores digitadas e amostras por tamanho.

**Verificação:** 6 testes novos (cor cadastrada/sugerida, estilo por categoria, validação, contorno
aplicado na peça e não no laser, arquivo, janela). **Não verificado no Corel**
(`docs/mvp-smoke-test.md`, 4aa) — em especial `CreateOutlineStyle` com o array de comprimentos e o
contorno "por dentro" (`Justification`).

## 59. Manual de uso (dev.92)

Pedido do usuário: um botão no Diagnóstico que abre um manual completo e detalhado de como usar e
para que serve cada ferramenta, mantido em dia a cada ferramenta adicionada, removida ou alterada.

- Conteúdo em `host/.../Manual/user-manual.json` (recurso embutido, como as notas da versão):
  `topics` (Começando, Regras gerais, Favoritos, Personalizar painel, Preferências e
  configurações, Quando algo dá errado), `sections` (introdução por título de seção) e `tools`
  (`id`, `purpose`, `steps`, `notes`). Nome e seção de cada ferramenta vêm de `PanelToolCatalog`
  — o manual não repete o rótulo, então renomear no catálogo já aparece no manual.
- `Manual/UserManual.cs`: junta JSON e catálogo na ordem de `PanelSectionCatalog`; `Search`
  exige todas as palavras, sem maiúsculas nem acentos, em nome, seção e texto.
- `Manual/UserManualWindow`: não modal e única (`ShowSingle`, como o Roteiro de capturas), lista
  de assuntos e seções à esquerda, conteúdo à direita (cartão por ferramenta: para que serve,
  COMO USAR numerado, OBSERVAÇÕES). Campo **Filtrar** no topo (pedido do usuário: funcionar como
  filtro): com texto, a lista só mantém assuntos e seções com resultado (seções com a contagem), a
  página da seção só as ferramentas que combinam e, sem página escolhida, a visão geral dos
  resultados; limpar (✕) volta à página de antes. `ThreadKeyboardRouter` para digitar dentro do
  Corel. Só texto — não lê nem altera o Corel.
- Botão `diagnostics.manual` "Manual de uso" no início do Diagnóstico; catálogo 80 (ícone E897).
- **Manter em dia** (AGENTS.md): `UserManualTests` exige texto para toda ferramenta do catálogo,
  nenhum texto de ferramenta que saiu, introdução para toda seção com ferramentas e nenhum nome
  de produto de origem no texto. Mudança de comportamento não é detectável por teste: revisar a
  entrada da ferramenta junto com a mudança.

**Verificação:** 5 testes novos (cobertura do catálogo nos dois sentidos, seções, texto sem nomes
de produtos, busca, janela: navegação, seção na ordem do catálogo, filtro na lista e na seção,
nada encontrado, limpar volta à página de antes). **Não
verificado no Corel** (`docs/mvp-smoke-test.md`, 4ab) — em especial digitar na busca com o Corel
em foco.

## 60. Cadastro de moldes — Validar molde (dev.93)

Reescrita simples da validação de documento do MoldMotor (`MoldValidator.Validate(snapshot,
analysis)` + `LogoNumberingValidator`), que lá existe no núcleo (`ValidateMoldUseCase`) mas não tem
botão. No PX: `Outputs/Tools/MoldValidationService.Validate(ICorelPage)` → `Report` (grupos
conferidos, peças, problemas). Só leitura.

- Grupos de tamanho: grupos com nome de tamanho das configurações, nas camadas não especiais da
  página ativa, também dentro de grupos que não são de tamanho. Peça reconhecida por
  `MoldPieceRecognizer` (só por nome; família = nome da peça, sem diferenciar maiúsculas).
- **Erro**: peça não reconhecida; `L<n>` repetido no grupo (`DuplicateLogoNumberInStructure`); o
  mesmo `L<n>` em peças diferentes (`LogoNumberConflictAcrossFamilies`, que lá é bloqueante); logo
  no lugar de outro número da referência (`LogoNumberMismatchWithinFamily`).
- **Aviso**: logo da referência que falta (`LogoMissingInFamilySize`); logo de mesmo número fora do
  lugar; tamanho repetido na mesma peça; sem TAG / sem laser só quando `Define.CreateTag` /
  `Define.CreateLaserCopy` estão ligados (equivale ao "parcial" do MoldMotor).
- Referência: o grupo com mais logos de nome único; empate → primeiro na ordem de tamanhos (o
  MoldMotor desempata pelo nome do grupo). Posição como no Propagar logos: (centro do logo − centro
  da peça) ÷ altura da peça; mesmo lugar = distância ≤ 0,08 (`PositionTolerance`).
- Diferenças do MoldMotor: casamento pelo logo mais próximo, não pela atribuição com pontuação de
  posição/tamanho/área do `GeometricLogoMatcher`; sem `Shape.Comment`; sem validação de plano de
  operação nem de camadas (`ValidatePieceLayerPlan`).
- Painel: `molds.validate` "Validar molde" depois de Propagar logos (catálogo 81, ícone E73E). Roda
  em `RunWithWorkspace` (resumo no status) e abre `Molds/MoldValidationWindow` — não modal, única
  (`ShowReport`), erros primeiro, "Validar de novo" chama o painel de novo, "Copiar" leva o texto.
- Manual: entrada `molds.validate`.

**Verificação:** 5 testes novos (molde consistente com grupo dentro de grupo; numeração trocada,
logo faltando e fora do lugar; mesmo número em duas peças, logo repetido, sem TAG, tamanho
repetido, peça não reconhecida; página sem grupos e camada especial ignorada; janela com resumo,
ordem e texto copiado). **Não verificado no Corel** (`docs/mvp-smoke-test.md`, 4ac).
