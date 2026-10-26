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

- **ListForge**: standalone permanece independente do CorelDRAW/PX. O PX DEV terá, no futuro,
  dois acessos — "ListForge Integrado" (modos Clássico e PX) e "ListForge Standalone" (abre o
  executável). Ambos consumirão o mesmo núcleo versionado do standalone; uma correção no núcleo
  só chega aos produtos quando eles atualizarem essa dependência. Não implementado nesta
  entrega.
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
- Persistência local simples e versionada primeiro; sincronização remota é explicitamente
  fora de escopo agora.

## 9. Pré-requisitos externos (não redistribuídos neste repositório)

- `Corel.Interop.VGCore.dll`, `Corel.Interop.CorelDRAW.dll` — vêm da instalação do CorelDRAW
  (`Programs64\Assemblies\`), referenciadas via `HintPath` com `Private=false` (nunca copiadas
  para a saída de build nem para o instalador).
- Inno Setup 6 (`ISCC.exe`) — só necessário para gerar o instalador.
- GitHub CLI / AWS CLI — só necessários para os botões de publicação.

## 10. Limitações conhecidas desta entrega

- "Agrupar Interseção" está implementado a partir do candidato mais plausível encontrado
  (`Agrupamento.AgruparObjetosSobrepostos`, numa revisão candidata de macros — ver
  `docs/migrated-tools.md`), mas **nem essa revisão confirma que esse é de fato o algoritmo do
  botão "Agrupar Interseção"**. Testado (`IntersectGroupServiceTests`), não confirmado contra o
  comportamento real esperado.
- `MoldPreparationService` busca grupos recursivamente dentro de cada camada, confirmado via
  reflexão (o parâmetro `Recursive` de `Shapes.FindShapes` no VGCore tem valor padrão `True`).
- `cdrShapeType` (VGCore v27) não tem mais um valor distinto para PowerClip — `ShapeKind.PowerClip`
  nunca é retornado pelo adaptador real; validar no smoke test se PowerClips aparecem como
  `cdrGroupShape` ou outro tipo.
- O caminho de saída `Z:\Neuber\Saídas\` é o padrão (preservado de propósito — ver
  `docs/migrated-tools.md`), configurável via `OutputPreparationService.SaveOutput`, ainda sem
  tela de configuração no host (hoje são campos de texto simples no painel).
- A pasta `Referência/Macros/` que fundamentou várias decisões desta entrega é, pelas próprias
  palavras do seu `LEIA-ME.md`, uma "revisão candidata" **revisada estaticamente, não compilada
  nem executada no Corel**. Toda ferramenta portada a partir dela carrega essa mesma ressalva
  até o smoke test real confirmar o contrário.
- Não há validação interativa dentro do CorelDRAW real nesta entrega — só build e testes
  automatizados (ver `docs/mvp-smoke-test.md` para o que falta e os comandos exatos).
