# Smoke test — PX Tool Box DEV no CorelDRAW 2026

## O que já foi executado e verificado nesta entrega (automatizado, sem abrir o CorelDRAW)

Comandos reais, executados nesta máquina, com o resultado mostrado:

```powershell
dotnet build PXToolBox.slnx -c Release
# Compilação com êxito. 0 Aviso(s). 0 Erro(s).

dotnet test tests\PXToolBox.Tests\PXToolBox.Tests.csproj -c Release
# Aprovado! - Com falha: 0, Aprovado: 47, Ignorado: 0, Total: 47

.\build-release.ps1 -Version 0.1.0-dev.1 -Channel Dev
# Build DEV 0.1.0-dev.1 gerada com sucesso.
# Instalador real gerado: dist\dev\0.1.0-dev.1\installer\PXToolBox-DEV-Setup-0.1.0-dev.1.exe

.\test-release.ps1 -Version 0.1.0-dev.1 -Channel Dev
# Validação de pacote concluída com sucesso.
```

Isso comprova: o código compila, a lógica de Saídas passa nos testes unitários (incluindo o
caso crítico do Grid com camadas ocultas, ver abaixo), e o pipeline de build/instalador
funciona de ponta a ponta localmente.

### Validação adicional — comparação do Preparar Grid com a macro de referência

Rodada posterior, focada em validar "Preparar Grid" com camadas ocultas (FE/C) contra a macro
VBA de referência. Nenhuma divergência de comportamento foi encontrada (ver seção 3 abaixo para
a análise completa) — só foi adicionado um teste automatizado cobrindo um caso que ainda não
tinha cobertura (restauração de visibilidade quando o processamento falha no meio, não só
quando é bem-sucedido). Nenhum código de produção foi alterado.

```powershell
dotnet build host\PXToolBox.CorelHost.Dev\PXToolBox.CorelHost.Dev.csproj
# Compilação com êxito. 0 Aviso(s). 0 Erro(s).

dotnet test tests\PXToolBox.Tests\PXToolBox.Tests.csproj
# Aprovado! - Com falha: 0, Aprovado: 48, Ignorado: 0, Total: 48
```

Validação no CorelDRAW real **não executada nesta rodada** (sem acesso ao Corel neste
ambiente) — ver o roteiro da seção 3 abaixo, ainda pendente de execução manual.

### Rodada de correções pós-teste real (2026-10)

**Achado antes de corrigir qualquer coisa, por instrução explícita de checar fonte × build ×
instalação primeiro**: o reposicionamento de "Preparar Grid Novo" para "Preparação e saída" e a
compactação da área de Status (ambos de uma entrega anterior) **nunca foram empacotados** — o
último instalador existente até este ponto era `0.1.0-dev.2`, gerado ANTES dessas duas
mudanças de fonte. Ou seja: se o teste real do usuário foi feito com `0.1.0-dev.2` (ou
anterior), capturas mostrando "Preparar Grid Novo" ainda em "Grid" ou a caixa de Status antiga
refletem a **versão instalada desatualizada**, não uma regressão nova. Build `0.1.0-dev.3`
(gerada nesta rodada) inclui essas duas mudanças pela primeira vez, junto com as correções
abaixo.

Correções desta rodada, com o que foi verificado:

- **"Remover NO/NU da página" — mensagem de status inconsistente**: o rótulo do botão já tinha
  sido trocado, mas o nome do comando passado para `RunWithWorkspace` continuava
  `"Expandir NO/NU"`, aparecendo como prefixo da linha de Status (`"Expandir NO/NU: Código NO/NU
  removido..."`, incoerente). Corrigido para `"Remover NO/NU da página"` em
  `ControlUI.xaml.cs`. Comportamento do comando (página inteira, recursivo, NO→TextoNome,
  NU→TextoNumero, nunca AP/TS) **inalterado** — verificado por revisão estática, sem teste novo
  necessário (nenhuma regra mudou).
- **Contraste da lista de Tipo de Peça**: o `ComboBox` não tinha template próprio — o WPF usa o
  tema do sistema para o popup da lista suspensa, que ignora o `Foreground` claro configurado no
  controle fechado, resultando em texto claro sobre fundo claro no popup. Corrigido com um
  template completo (controle fechado, popup, item normal/sob o mouse/selecionado/desabilitado)
  em `ControlUI.xaml`. **Verificação automatizada**: nenhuma (é renderização visual, não tem
  como cobrir com teste de unidade). **Validação no Corel: pendente.**
- **"Somar Altura (m)" não funcionava — causa confirmada por comparação com a macro original**:
  `MeasurementSumService.SumHeightOnly` restringia a unidades mm/cm/m, uma suposição incorreta
  de uma migração anterior. `Referência/Macros/modulos/Medidas.txt` mostra que
  `SomaAlturasSelecionadasEmMetros` e `SomaMedidasSelecionadas` usam o MESMO conjunto de
  unidades (mm/cm/m/polegada/pé/jarda) — não há essa diferença no original. Corrigido para
  aceitar as mesmas 6 unidades que o modo com escolha. **Verificação automatizada**: novo teste
  `SumHeightOnly_AcceptsInchFootYard_SameAsChoiceMode` (substituiu um teste anterior que
  afirmava, incorretamente, que polegada/pé/jarda deviam ser rejeitados). **Validação no Corel
  com o documento real do usuário: pendente** (precisa confirmar a unidade do documento de
  teste).
- **Somas sem janela de resultado**: `RunSum` agora mostra uma janela (`MessageBox`) com a
  operação, o resultado e a unidade após uma soma bem-sucedida, e só afirma "copiado para a área
  de transferência" se a cópia realmente não lançou exceção — aplicado às quatro operações de
  soma (inclusive "Somar Altura (m)"). Seleção vazia ou unidade não reconhecida continuam sem
  janela nenhuma (só a mensagem de erro no Status), nunca apresentando um resultado zerado como
  sucesso. **Validação no Corel: pendente.**
- **"Preparar Grid" não centraliza — causa confirmada e CORRIGIDA**: o usuário enviou o
  documento de reprodução (`Referência/GRID DESCENTRALIZADO.cdr`) e as capturas de tela. Com
  CorelDRAW já aberto nesta máquina, foi possível conectar via COM (`New-Object -ComObject
  "CorelDRAW.Application"`, reaproveitando a instância já em execução) e medir o estado real: a
  página foi redimensionada corretamente (tamanho bate exatamente com a caixa delimitadora dos
  objetos + 30), mas o conjunto de objetos ficou parado na posição original, sem a translação
  esperada. Reproduzido de forma isolada e não-destrutiva (documento novo descartável, nunca o
  arquivo do usuário): **redimensionar `Page.SizeWidth`/`SizeHeight` desloca o referencial em
  que `Shape.CenterX`/`CenterY` são relidos** (a página é redimensionada em torno do seu próprio
  centro) — o código calculava o centro combinado dos objetos ANTES do resize e reaproveitava
  esse valor depois, misturando dois referenciais. Corrigido em
  `GridPreparationService.ResizePageToFitAndCenter`: o centro agora é relido DEPOIS do resize,
  igual ao que a macro VBA original já fazia (relê `conjunto.CenterX` só no próprio `.Move`). Ver
  `docs/migrated-tools.md`, "Bug confirmado e corrigido". **Verificação automatizada**: novo
  teste `PrepareGrid_CentersCombinedBoundingBoxExactlyOnPageCenter` (os fakes `FakePage`/
  `FakeShape` agora simulam o mesmo deslocamento de referencial, então o teste falha sem a
  correção e passa com ela). **Validado diretamente no CorelDRAW real** desta máquina com uma
  reprodução isolada do algoritmo — mas **não validado rodando o comando real do add-on
  instalado** dentro do painel (ver pendência abaixo).
- **Alterar Tipo de Peça — redesenhado**: trocado "De/Para" por uma única caixa de destino e
  dois botões: "Alterar tipo dos objetos selecionados" (reconhece o tipo pelo NOME de cada
  objeto selecionado, mesma convenção pontuada de `LayerName` já usada para camadas — não é uma
  convenção nova) e "Alterar tipo da camada ativa" (por decisão do usuário: a API do CorelDRAW
  não expõe multi-seleção de camadas — confirmado por reflexão sobre `Corel.Interop.VGCore.dll`
  — então a ação atua só na camada ativa, com isso explícito no rótulo e no tooltip, nunca
  chamada de "camadas selecionadas" no plural). Ver `docs/migrated-tools.md`. A ferramenta
  antiga (documento inteiro, com "De") continua no serviço e nos testes, só não está mais ligada
  a nenhum botão. **Validação no Corel: pendente.**
- **Preparar Molde Novo / Preparar Grid Novo**: marcados como "revisão funcional pendente" em
  `docs/migrated-tools.md`, comportamento preservado sem alteração.
- **Preparar Escudo/Bandeira/Faixa de Capitão, Renomear Objetos, Agrupar Interseção**:
  confirmados pelo usuário no cenário testado — registrado em `docs/migrated-tools.md`, sem
  generalizar para outros casos.

```powershell
dotnet build host\PXToolBox.CorelHost.Dev\PXToolBox.CorelHost.Dev.csproj
# Compilação com êxito. 0 Aviso(s). 0 Erro(s).

dotnet test tests\PXToolBox.Tests\PXToolBox.Tests.csproj
# Aprovado! - Com falha: 0, Aprovado: 55, Ignorado: 0, Total: 55

.\build-release.ps1 -Version 0.1.0-dev.5 -Channel Dev
# Build DEV 0.1.0-dev.5 gerada com sucesso (inclui a correção de centralização do Grid e o
# redesenho de Alterar Tipo de Peça).
# Instalador: dist\dev\0.1.0-dev.5\installer\PXToolBox-DEV-Setup-0.1.0-dev.5.exe
```

**Pendências desta rodada:**
1. Instalar `0.1.0-dev.5` e confirmar visualmente: Grid Novo em "Preparação e saída", Status
   compacto, contraste da lista de Tipo de Peça, janela de resultado das somas, e os dois novos
   botões de Tipo de Peça.
2. **Preparar Grid**: repetir o comando de verdade (pelo botão do painel, não a reprodução
   isolada do algoritmo) no documento `Referência/GRID DESCENTRALIZADO.cdr` (ou uma cópia) e
   confirmar que o resultado agora fica centralizado na página, inclusive com FE/C ocultas e com
   Desfazer.
3. **Alterar tipo da camada ativa**: confirmar no Corel que o rótulo "camada ativa" (não
   "camadas selecionadas") é claro o suficiente para o fluxo real de trabalho do usuário.

**Isso NÃO comprova** que o add-on carrega dentro do CorelDRAW real, nem que a UI funciona —
só um teste manual dentro do Corel comprova isso. É o próximo passo concreto.

### Correção `fix/dev-docker-registration` — o que foi verificado, e o que ainda não

A primeira correção (`fix/dev-docker-hosting`) trocou o registro de um item solto em
`uiConfig/commandBars` para um `dockerData` real em `uiConfig/dockers`, alternado por um
`checkButton` no menu Janela → Janelas de encaixe — o mesmo padrão usado pelo MoldMotor e pelo
exemplo oficial do SDK da Corel (eCutDocker). Isso **não foi suficiente**: instalado e testado
de fato nesta máquina, o painel continuou comprimido e o item não apareceu no menu. Investigando
com evidências (ver `docs/ARCHITECTURE.md`, seção "Registro incorreto já persistido"), a causa
real foi confirmada por leitura direta dos sete arquivos `.cdws` desta máquina: o CorelDRAW só
mescla `UserUI.xslt` num workspace quando ele é **criado** — nunca ao reabrir o app, nem quando
o add-on é só atualizado. Como todo workspace já existia antes da correção, nenhum deles herdou
o registro novo sozinho, e todos continuaram com o item antigo. A correção `fix/dev-docker-registration`
estende `scripts/repair-dev-docker-registration.ps1` para também inserir o item que falta, além
de remover o antigo.

Verificado nesta máquina:
- Instalação confirmada correta: hash SHA-256 do `PXToolBoxDev.dll` instalado é idêntico ao
  build do repositório e ao pacote do instalador — não é um binário antigo nem duplicado. Sem
  instalações duplicadas de PX Tool Box em nenhum outro lugar do disco.
- `AppUI.xslt`/`UserUI.xslt` instalados são, byte a byte, os corrigidos (confirmado por leitura
  direta do arquivo instalado).
- Causa raiz confirmada por leitura direta dos sete `.cdws` reais: todos com o item antigo na
  barra padrão, nenhum com o item novo no menu de Janelas de encaixe (enquanto os itens
  equivalentes do MoldMotor e do eCutDocker, no mesmo menu, estão presentes).
- `scripts/repair-dev-docker-registration.ps1` (versão com remoção + inserção) foi executado de
  fato contra **cópias isoladas** dos sete arquivos `.cdws` reais desta máquina — corrigiu os
  sete, preservou os itens do SISBolt/MoldMotor/eCutDocker e todo o resto do conteúdo, manteve
  o XML válido, e uma segunda execução não encontrou mais nada a corrigir (idempotente).
- **Executado de fato contra os sete arquivos `.cdws` reais em uso** (26/10/2026, com o usuário
  confirmando o CorelDRAW fechado antes): removeu o item antigo e inseriu o item novo nos sete,
  com backup de cada um (`<arquivo>.cdws.bak-20261026-1140xx`). Confirmado por leitura direta
  dos arquivos após a gravação: item antigo ausente, item novo presente no menu "Janelas de
  encaixe", XML válido, SISBolt intacto nos sete.
- **Validado pelo usuário no CorelDRAW real** (26/10/2026, depois do reparo acima): o docker
  "PX Tool Box DEV" passou a aparecer corretamente como janela de encaixe lateral, igual ao
  MoldMotor — não mais preso na barra superior. Confirmação literal do usuário: "Deu bom".

**Ainda não confirmado explicitamente item a item** (a correção do ponto principal — docker
lateral em vez de embutido na barra — está validada; os pontos abaixo do roteiro completo da
seção 2 não foram individualmente re-confirmados nesta rodada):
- Redimensionar e desencaixar mantendo o conteúdo utilizável.
- Fechar e reabrir pelo mesmo item de menu.
- Executar uma ferramenta de dentro do docker já no novo formato.
- Persistência do estado (aberto/fechado, posição) depois de reiniciar o CorelDRAW de novo.

## Roteiro manual (a executar no Windows)

### 0. Preparação — necessária mesmo se você já tentou a correção anterior

**Com o CorelDRAW fechado** (salve seus documentos e feche o aplicativo normalmente — não
force o encerramento do processo):

```powershell
# 1) Instalar/atualizar a build corrigida (opção A ou B):
.\dist\dev\0.1.0-dev.1\installer\PXToolBox-DEV-Setup-0.1.0-dev.1.exe
# ou: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-dev-local.ps1

# 2) Corrigir o registro já persistido em cada workspace (remove o item antigo, insere o
#    item que falta no menu "Janelas de encaixe"):
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\repair-dev-docker-registration.ps1

# Para só ver o que seria alterado, sem gravar nada:
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\repair-dev-docker-registration.ps1 -WhatIf
```

A ordem importa: o passo 2 só funciona corretamente depois do passo 1, porque o item que ele
insere referencia o `checkButton` definido no `AppUI.xslt` corrigido. O script do passo 2 só
altera o registro do PX Tool Box DEV — preserva barras personalizadas, o SISBolt, o MoldMotor e
qualquer outro add-in. Faz backup de cada `.cdws` alterado antes de gravar
(`<arquivo>.cdws.bak-<data>-<hora>`).

### 2. Abrir o CorelDRAW e validar o docker

1. Abra o CorelDRAW 2026.
2. Procure "PX Tool Box DEV" em **Janela → Janelas de encaixe** e clique para abrir (o item é
   um alternador: clicar de novo fecha o docker).
3. **Se não aparecer no menu**: confirme que rodou o passo 0 (instalar **e depois** rodar
   `repair-dev-docker-registration.ps1`) com o workspace que você está usando agora. Reiniciar
   o CorelDRAW sozinho **não resolve** — confirmado nesta máquina por evidência direta (ver
   `docs/ARCHITECTURE.md`, seção "Registro incorreto já persistido"): o item só entra no menu
   se o `.cdws` do seu workspace for corrigido pelo script, nunca só por reabrir o app.
4. Confira, nesta ordem (lista de validação pedida para esta correção):
   1. Abrir a janela pelo menu Janela → Janelas de encaixe.
   2. Conferir que ela aparece encaixada (não embutida numa barra, não comprimida).
   3. Redimensionar o docker e desencaixá-lo (arrastar para fora da borda) — o conteúdo deve
      permanecer organizado verticalmente e utilizável, não cortado.
   4. Fechar e reabrir pelo mesmo item de menu.
   5. Executar uma ferramenta qualquer do painel (ex.: "Alternar NO (Nome)" com algo
      selecionado) e confirmar que funciona normalmente dentro do docker.
   6. Reiniciar o CorelDRAW e confirmar que o docker preserva o estado (aberto/fechado,
      posição) entre sessões.
   - Posição inicial (lateral direita): o Corel decide isso por padrão na primeira vez que o
     docker é criado nesse workspace — não há atributo no XSLT que force isso (ver
     `docs/ARCHITECTURE.md`). Se não abrir à direita na primeira vez, arraste-o lá uma vez; o
     Corel deve lembrar a posição depois.
5. Anote: "Conectado ao CorelDRAW (...)" deve aparecer no topo do painel. Se aparecer "Não
   conectado ao CorelDRAW", ver `docs/ARCHITECTURE.md`, seção "Ponto em aberto" — é o primeiro
   problema a investigar (provavelmente a hipótese de conexão via `GetActiveObject` precisa de
   ajuste).

### 3. Caso crítico — Grid com camadas ocultas

**Trabalhe sempre numa cópia do documento real** (nunca no original) — salve uma cópia antes de
começar, ex.: `Copia-teste-grid.cdr`.

Referência de comportamento confirmada por comparação direta com a macro VBA (ver
`Referência/Macros/modulos/Grid.bas`, `Sub PreparaGrid`, e `src/PXToolBox.Outputs/Tools/
GridPreparationService.cs`) — leitura linha a linha confere com a revisão candidata:
- **Alcance**: a página ativa inteira (todas as camadas não-especiais), nunca só a seleção.
- **Camadas ocultas (FE/C) e bloqueadas**: objetos de camadas ocultas **PARTICIPAM** do Grid —
  não é uma escolha arbitrária desta migração, a macro de referência exige isso explicitamente
  (torna cada camada temporariamente visível antes de coletar os objetos). Uma camada
  bloqueada (`Editable = False`) com objetos existentes faz o comando **falhar antes de tocar
  em qualquer coisa** — nem a visibilidade chega a ser alterada.
- **Estados temporários**: cada camada não-especial é tornada visível temporariamente para a
  coleta e o deslocamento; a visibilidade **original de cada camada é sempre restaurada**
  (mesmo se a camada estava oculta e bloqueada ao mesmo tempo, mesmo se o processamento falhar
  no meio — coberto por teste automatizado com falha forçada, ver abaixo). Nenhum objeto é
  agrupado, desagrupado ou movido para outra camada — cada um permanece na camada onde estava,
  só a posição (translação) e o nome mudam.
- **Resultado esperado**: página redimensionada à caixa delimitadora combinada de todos os
  objetos (+30 unidades do documento) e tudo centralizado; cada objeto renomeado para o
  segmento após o primeiro ponto do nome da sua camada (ex.: `CAM1.PP.MA` → objeto renomeado
  `PP`); página renomeada `DESIGN`; documento renomeado com prefixo `SISBolt_` (só se o nome
  ainda era o padrão "Sem título-N"); camadas organizadas por tipo/categoria
  (`OrganizarCamadas`); camadas vazias excluídas só depois de restaurar a visibilidade.
- **Dúvida não resolvida pela referência, documentada e não alterada**: a macro original chama
  `Utilidades.VerificarGerenciadorObjetos()` antes de tudo, que só exibe uma caixa de diálogo
  "O Gerenciador de Objetos está aberto?" e cancela se o usuário responder "Não" — é uma
  precaução do ambiente antigo de VBA, sem lógica de negócio. O add-on atual não reproduz essa
  caixa de diálogo (não há evidência de que o host .NET dependa do Gerenciador de Objetos estar
  aberto para os mesmos acessos a `Layer`/`Shape` funcionarem corretamente). Ao validar, preste
  atenção especial a qualquer comportamento estranho que só aconteça com o Gerenciador de
  Objetos fechado — se encontrar algo, é essa a hipótese a investigar primeiro.

Documento de teste sugerido: 3 camadas chamadas `CAM1.PP.MA`, `CAM1.PP.FE`, `CAM1.PP.C`, cada
uma com um objeto simples (ex.: um retângulo nomeado qualquer coisa).

**Cenário principal:**
1. Registre o estado inicial de cada camada (MA visível, FE visível, C visível).
2. Oculte `CAM1.PP.FE` e `CAM1.PP.C` (ícone de olho no Gerenciador de Objetos).
3. Clique **Preparar Grid** no painel.
4. Confira o resultado (página/documento renomeados, objetos renomeados — ver lista acima).
5. Mostre `CAM1.PP.FE` e `CAM1.PP.C` novamente.
6. Confira integridade: os três objetos continuam existindo, na mesma camada original (não
   foram agrupados nem movidos), na posição translada esperada, com o nome `PP`.
7. **Desfazer (Ctrl+Z) uma vez** deve desfazer toda a operação de uma vez só (um único
   `BeginCommandGroup`/`EndCommandGroup`), incluindo a renomeação de página/documento/objetos e
   o redimensionamento da página.

**Comparação de controle:** repita o mesmo documento (ou uma cópia nova) com as três camadas
visíveis o tempo todo, e confirme que o resultado final é equivalente (mesma renomeação,
mesmo redimensionamento) — a única diferença observável deve ser que, no cenário com FE/C
ocultas, elas voltam a ficar ocultas ao final (o comando não as deixa visíveis).

**Falha controlada:** force uma falha (ex.: bloqueie `CAM1.PP.MA` com um objeto dentro antes de
clicar) e confirme que: nenhuma camada muda de visibilidade, nenhum objeto é tocado, e o Status
do painel mostra a mensagem "Desbloqueie a camada antes de preparar o Grid: CAM1.PP.MA".

Isso replica o comportamento coberto pelos testes automatizados em
`GridPreparationServiceTests`: `PrepareGrid_HiddenFeAndC_ParticipateAndVisibilityIsRestored`
(camadas ocultas participam e têm a visibilidade restaurada),
`PrepareGrid_LockedLayerWithShapes_FailsBeforeMutatingAnything` (falha antes de qualquer
mutação) e `PrepareGrid_ExceptionDuringProcessing_StillRestoresVisibilityBeforePropagating`
(mesmo com uma exceção no meio do processamento, a visibilidade original é restaurada antes de
propagar o erro) — agora com o COM real em vez do fake em memória.

**Nota sobre o painel**: "Preparar Grid Novo" não fica mais na seção "Grid" — foi movido para
"Preparação e saída" (ver `fix/dev-panel-sections-status`). "Preparar Grid" (este roteiro)
continua sozinho em "Grid".

### 4. Demais ferramentas (teste rápido, uma vez cada)

- **NO/NU/AP/TS**: selecione um texto, clique para alternar, confira que o nome mudou; clique
  de novo, confira que voltou.
- **Renomear Objetos Selecionados**: selecione 1+ objetos, digite um nome, confira que todos
  foram renomeados.
- **Alterar Tipo de Peça**: num documento com camadas `CAM1.*`, escolha De=CAM1, Para=SHO1,
  confirme que as camadas foram renomeadas para `SHO1.*`. Enquanto isso, confira o **contraste
  da lista suspensa** (texto legível com fundo escuro, item sob o mouse e selecionado
  diferenciados) — corrigido nesta rodada. A simplificação da ferramenta em si (uma caixa só,
  duas ações: objetos/camadas selecionados) **não foi implementada ainda** — pendente de decisão
  do usuário (ver `docs/mvp-smoke-test.md`, "Pendências desta rodada").
- **Preparar Escudo/Bandeira/Faixa de Capitão**: com objetos selecionados, clique e confira
  que a página foi redimensionada ao conteúdo e renomeada.
- **Somar Altura (m) / Altura / Largura / Ambas**: selecione objetos com tamanhos conhecidos,
  clique e confira: (1) uma janela aparece com a operação, o resultado e a unidade; (2) a janela
  afirma que o valor foi copiado; (3) confirme colando (Ctrl+V) em qualquer lugar; (4) repita com
  o documento em polegadas — "Somar Altura (m)" não deve mais falhar com "Unidade não
  reconhecida" (bug corrigido nesta rodada); (5) com nada selecionado, clique e confirme que
  NENHUMA janela de sucesso aparece, só a mensagem de erro no Status.
- **Preparar Molde Novo**: num documento com camadas de 3 segmentos e grupos com subitens,
  confira a normalização do nome da camada e a renomeação dos subitens não reconhecidos.
- **Agrupar Interseção**: com objetos sobrepostos (mesma posição/tamanho, mais algum objeto
  menor por cima), confira quantos grupos foram criados e confirme visualmente se o resultado
  é o esperado para este comando — **esta ferramenta foi implementada a partir do candidato
  mais plausível encontrado numa revisão de macros, sem confirmação de que é o algoritmo
  certo** (ver `docs/migrated-tools.md`). Se o resultado não for o esperado, é o primeiro
  candidato a corrigir.

### 5. Registrar o resultado

Depois de rodar este roteiro, atualize esta seção (ou abra uma issue) com:
- O que funcionou exatamente como esperado.
- O que precisou de ajuste (principalmente: como a instância de `Application` chegou ao
  `ControlUI` — ver seção 3 de `docs/ARCHITECTURE.md`).
- Qualquer mensagem de erro do painel (campo "Status" no rodapé).

**Não declare uma ferramenta como validada em produção só porque passou no teste automatizado
— só este roteiro manual, executado de fato no CorelDRAW, comprova isso.**
