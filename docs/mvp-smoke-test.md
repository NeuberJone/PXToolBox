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
  como cobrir com teste de unidade). **Relato do usuário (05/11/2026): baixo contraste observado na caixa de seleção** — (dev.58: a caixa de filtro da comparação do ListForge Integrado tinha mesmo texto claro sobre fundo claro e foi corrigida; a de Tipo de peça do painel já estava legível — ver 4n)
  conferência visual no Corel pendente até confirmação.
- **"Somar Altura (m)" não funcionava — causa confirmada por comparação com a macro original**:
  `MeasurementSumService.SumHeightOnly` restringia a unidades mm/cm/m, uma suposição incorreta
  de uma migração anterior. `Referência/Macros/modulos/Medidas.txt` mostra que
  `SomaAlturasSelecionadasEmMetros` e `SomaMedidasSelecionadas` usam o MESMO conjunto de
  unidades (mm/cm/m/polegada/pé/jarda) — não há essa diferença no original. Corrigido para
  aceitar as mesmas 6 unidades que o modo com escolha. **Verificação automatizada**: novo teste
  `SumHeightOnly_AcceptsInchFootYard_SameAsChoiceMode` (substituiu um teste anterior que
  afirmava, incorretamente, que polegada/pé/jarda deviam ser rejeitados). **Validação no Corel
  com o documento real do usuário: pendente** (precisa confirmar a unidade do documento de
  teste). **Atualização (2026-10, rodada posterior)**: esta correção confirmou que
  `SumHeightOnly`/"Somar Altura (m)" e `SumSelected` (modo Height)/"Somar Altura" sempre
  produziam exatamente o mesmo resultado — o botão "(m)" era uma duplicata, removido a pedido do
  usuário. Ver `docs/migrated-tools.md`.
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
  a nenhum botão. **Relato do usuário (05/11/2026): funcionou no cenário testado.**
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

### Rodada de consolidação (26/10/2026) — status item a item

Antes de iniciar módulos novos, esta rodada reinspecionou o repositório (sem refazer nenhuma
correção já implementada) e gerou a build `0.1.0-dev.6` para conferência. Estado de cada item
pendente do teste do usuário:

| Item | Status |
| --- | --- |
| Preparar Grid centraliza (incl. FE/C ocultas) | Implementado e **verificado automaticamente** (`PrepareGrid_CentersCombinedBoundingBoxExactlyOnPageCenter`) e **validado no Corel real** com reprodução isolada do algoritmo (ver seção acima). **Validação do botão real do painel: pendente.** |
| Remover NO/NU sem mensagem "expandir" | Implementado e **verificado automaticamente** por leitura direta do código (`OnExpandNoNu`/`CodeToggleService`, sem ocorrência de "expandir" em nenhuma mensagem). **Validação visual no Corel: pendente.** |
| Seletores legíveis (fechado/aberto/selecionado) | Implementado (template próprio de `ComboBox`/`ComboBoxItem`). **Relato do usuário (05/11/2026): baixo contraste observado na caixa de seleção.** (dev.58: a caixa de filtro da comparação do ListForge Integrado tinha mesmo texto claro sobre fundo claro e foi corrigida; a de Tipo de peça do painel já estava legível — ver 4n) Conferência visual no Corel **pendente até confirmação** (contraste não é verificável por teste automatizado). |
| Tipo de peça: caixa única + ações separadas | Implementado (`ToTypeCombo` + `OnChangeSelectedObjectsType`/`OnChangeActiveLayerType`), respeitando o limite real da API (`ActiveLayer`, sem simular seleção múltipla de camadas). **Verificado automaticamente** (5 testes dedicados). **Relato do usuário (05/11/2026): funcionou no cenário testado** — não generalizado para outros casos. |
| Todas as somas: sempre em metros, qualquer unidade do documento | Implementado — "Somar Altura (m)" **removido** (2026-10, duplicata exata de "Somar Altura"); mesmo conjunto de 6 unidades (mm/cm/m/polegada/pé/jarda) aceito pelos 3 botões restantes, igual à macro original. Rótulos/tooltips de "Somar Altura"/"Largura"/"Ambas" corrigidos — diziam "na unidade do documento", mas sempre convertiam para metros. **Verificado automaticamente.** **Validação com valor conhecido no Corel: pendente.** |
| Todas as somas: copiar + janela de resultado | Implementado (`RunSum`): janela só após sucesso, mensagem de cópia só quando `CopyToClipboard` não lança excessão. **Verificado automaticamente** (fluxo de sucesso/falha testado na camada de serviço; a chamada de UI/clipboard em si não é coberta por teste automatizado). **Validação visual no Corel: pendente.** |
| Preparar Grid Novo em "Preparação e saída" | Implementado — confirmado por leitura direta do XAML atual. **Validação visual no Corel: pendente.** |
| Status compacto, sem retângulo vazio reservado | Implementado — confirmado por leitura direta do XAML atual (sem altura fixa, sem espaço reservado quando vazio). **Validação visual no Corel: pendente.** |

Build e testes desta rodada:

```powershell
dotnet test tests\PXToolBox.Tests\PXToolBox.Tests.csproj -c Release
# Aprovado! - Com falha: 0, Aprovado: 55, Ignorado: 0, Total: 55

dotnet build host\PXToolBox.CorelHost.Dev\PXToolBox.CorelHost.Dev.csproj -c Release
# Compilação com êxito. 0 Aviso(s). 0 Erro(s).

.\build-release.ps1 -Version 0.1.0-dev.6 -Channel Dev
# Build DEV 0.1.0-dev.6 gerada com sucesso. Nenhuma mudança de código nesta rodada — todas as
# correções já estavam no repositório (ver tabela acima); esta build existe só para dar ao
# usuário um instalador alinhado com o estado atual do código, para a conferência manual.
# Instalador: dist\dev\0.1.0-dev.6\installer\PXToolBox-DEV-Setup-0.1.0-dev.6.exe
```

**Nenhuma versão anterior foi apagada** — `dist\dev\0.1.0-dev.1` até `0.1.0-dev.5` continuam no
disco.

**Como confirmar que o Corel carregou a versão `0.1.0-dev.6`:** o painel não exibe número de
versão (`AssemblyVersion` do host fica fixo em `0.1.0.0`, não acompanha o `-dev.N`). A forma
confiável é: instalar `PXToolBox-DEV-Setup-0.1.0-dev.6.exe`, reiniciar o CorelDRAW, e conferir em
**Configurações do Windows → Apps instalados** (ou Painel de Controle → Programas) que "PX Tool
Box DEV" aparece com a versão `0.1.0-dev.6` — esse texto vem do `AppVersion` do instalador
(`installer/PXToolBoxDev.iss`), gravado a cada build. Isso confirma o instalador usado; não prova
por si só que o CorelDRAW já recarregou o add-on — para isso, feche e reabra o CorelDRAW depois
de instalar.

**Isso NÃO comprova** que o add-on carrega dentro do CorelDRAW real, nem que a UI funciona —
só um teste manual dentro do Corel comprova isso. É o próximo passo concreto. **Esta build não
deve ser chamada de "estabilizada"** — ela compilou e passou os testes automatizados, mas nenhum
dos oito itens acima foi validado pelo usuário dentro do CorelDRAW real ainda.

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
  confirme que as camadas foram renomeadas para `SHO1.*` (**funcionou no cenário testado**,
  relato do usuário de 05/11/2026). Enquanto isso, confira o **contraste
  da lista suspensa** (texto legível com fundo escuro, item sob o mouse e selecionado
  diferenciados) — corrigido nesta rodada, mas o usuário relatou **baixo contraste observado
  na caixa de seleção** (05/11/2026) (dev.58: a caixa de filtro da comparação do ListForge Integrado tinha mesmo texto claro sobre fundo claro e foi corrigida; a de Tipo de peça do painel já estava legível — ver 4n); conferência visual pendente até confirmação. A simplificação da ferramenta em si (uma caixa só,
  duas ações: objetos/camadas selecionados) **não foi implementada ainda** — pendente de decisão
  do usuário (ver `docs/mvp-smoke-test.md`, "Pendências desta rodada").
- **Preparar Escudo/Bandeira/Faixa de Capitão**: com objetos selecionados, clique e confira
  que a página foi redimensionada ao conteúdo e renomeada.
- **Somar Altura / Largura / Ambas**: seleção de medidas, sempre em metros nos três botões
  (**"Somar Altura (m)" foi removido, 2026-10** — era uma duplicata exata de "Somar Altura": os
  dois sempre somavam em metros, independente da unidade do documento; ver `docs/migrated-tools.md`).
  Selecione objetos com tamanhos conhecidos, clique e confira: (1) uma janela aparece com a
  operação, o resultado e a unidade (sempre "m"); (2) a janela afirma que o valor foi copiado;
  (3) confirme colando (Ctrl+V) em qualquer lugar; (4) repita com o documento em
  polegadas/pé/jarda — não deve falhar com "Unidade não reconhecida" (bug corrigido numa rodada
  anterior, preservado); (5) com nada selecionado, clique e confirme que NENHUMA janela de
  sucesso aparece, só a mensagem de erro no Status.
- **Preparar Molde Novo**: num documento com camadas de 3 segmentos e grupos com subitens,
  confira a normalização do nome da camada e a renomeação dos subitens não reconhecidos.
- **Agrupar Interseção**: com objetos sobrepostos (mesma posição/tamanho, mais algum objeto
  menor por cima), confira quantos grupos foram criados e confirme visualmente se o resultado
  é o esperado para este comando. **Já confirmado pelo usuário no cenário testado** (2026-10;
  reafirmado em 05/11/2026 — ver `docs/migrated-tools.md`); repita só para casos diferentes.

### 4b. ListForge Standalone — seção "Listas" (nova nesta rodada)

Não depende de documento aberto no CorelDRAW — pode ser testado mesmo sem abrir nenhum `.cdr`.

1. **Primeira configuração**: com o PX recém-instalado (sem `%LOCALAPPDATA%\PXToolBox\Dev\config\listforge-path.txt`),
   clique "Abrir ListForge ↗". Confirme que abre o seletor de arquivo automaticamente (status
   explica "Nenhum caminho do ListForge configurado ainda"). Selecione
   `C:\Projetos\ListForge\bin\Release\net8.0-windows\ListForge.exe` (build Release já presente
   nesta máquina) ou o instalador oficial do ListForge, se preferir testar com ele instalado.
2. **Persistência**: feche e reabra o CorelDRAW (ou só o docker). Clique "Abrir ListForge ↗" de
   novo — deve abrir direto, sem pedir o caminho outra vez.
3. **Caminho com espaços**: aponte para um executável dentro de uma pasta com espaço no nome;
   confirme que abre normalmente.
4. **Arquivo inexistente**: renomeie/mova o `.exe` configurado fora do PX, clique "Abrir
   ListForge ↗" — confirme que o status explica que o arquivo não foi encontrado (pode ter sido
   movido ou removido) e abre o seletor para escolher outro.
5. **Cancelamento do seletor**: clique "Localizar executável…" e cancele a janela — confirme
   que o status diz que a seleção foi cancelada e que o caminho configurado NÃO mudou.
6. **Falha ao iniciar**: configure um caminho que existe mas não é executável válido (ex.: um
   `.txt` renomeado para `.exe`) — confirme que o status reporta a falha ao iniciar, sem travar
   o painel nem impedir uma nova tentativa depois.
7. **Sem documento aberto**: feche todos os documentos do CorelDRAW (ou abra o Corel sem criar
   nenhum) e confirme que "Abrir ListForge ↗" funciona do mesmo jeito — ao contrário de todo
   outro botão do painel, esta seção não exige documento nem conexão ativa.
8. **Isolamento DEV/Prod**: confirme que o arquivo de configuração fica em
   `%LOCALAPPDATA%\PXToolBox\Dev\config\listforge-path.txt` (não em `ApplicationData`/roaming,
   não misturado com nenhuma configuração do ListForge standalone em si). **Não há como testar
   o isolamento contra a distribuição Prod nesta entrega — ela ainda não existe.**

**Verificado automaticamente** (sem abrir nenhum processo real, `ListForgeLauncherTests` e
`ListForgeConfigStoreTests`, perna `net48` de `dotnet test`): primeira configuração, caminho
nunca configurado vs. configurado-mas-ausente (mensagens distintas), caminho com espaços,
caminho inexistente, falha ao iniciar sem interromper tentativas futuras, mensagem de sucesso
nunca afirma "pronto para uso", persistência real em disco (round-trip), e que o arquivo fica
sob `LocalApplicationData`. **Os 8 itens acima, exercitando o botão real dentro do CorelDRAW
com o ListForge real, estão pendentes de validação manual.**

### 4b-bis. Nexor — seção "Produção" (nova nesta rodada, iteração 24)

Não depende de documento aberto no CorelDRAW. Primeira etapa da integração com Produção — só
abrir o executável existente, mesmo padrão do item 4b acima, configuração independente. Ver
`docs/ARCHITECTURE.md`, seção 14, para como a distribuição real foi localizada (não suposta).

1. **Primeira configuração**: com o PX recém-instalado (sem
   `%LOCALAPPDATA%\PXToolBox\Dev\config\nexor-path.txt`), clique "Abrir Nexor ↗". Confirme que
   abre o seletor de arquivo automaticamente (status explica "Nenhum caminho do Nexor
   configurado ainda"). Selecione `C:\Projetos\ProjetoJocasta\dist\Nexor-1.6.29.exe` (o mesmo
   executável que o atalho da barra de tarefas já usa nesta máquina) ou uma versão mais nova, se
   já existir.
2. **Persistência**: feche e reabra o CorelDRAW (ou só o docker). Clique "Abrir Nexor ↗" de
   novo — deve abrir direto, sem pedir o caminho outra vez.
3. **Caminho com espaços**: aponte para uma cópia do executável dentro de uma pasta com espaço
   no nome; confirme que abre normalmente.
4. **Arquivo inexistente**: renomeie/mova o `.exe` configurado fora do PX, clique "Abrir
   Nexor ↗" — confirme que o status explica que o arquivo não foi encontrado (pode ter sido
   movido ou removido) e abre o seletor para escolher outro.
5. **Cancelamento do seletor**: clique "Localizar executável…" e cancele a janela — confirme
   que o status diz que a seleção foi cancelada e que o caminho configurado NÃO mudou.
6. **Falha ao iniciar**: configure um caminho que existe mas não é executável válido (ex.: um
   `.txt` renomeado para `.exe`) — confirme que o status reporta a falha ao iniciar, sem travar
   o painel nem impedir uma nova tentativa depois.
7. **Sem documento aberto**: feche todos os documentos do CorelDRAW (ou abra o Corel sem criar
   nenhum) e confirme que "Abrir Nexor ↗" funciona do mesmo jeito.
8. **Independência da configuração do ListForge**: configure caminhos diferentes para ListForge
   e Nexor (item 4b e este item); confirme que trocar um nunca altera o outro (arquivos
   `listforge-path.txt`/`nexor-path.txt` distintos, ambos sob
   `%LOCALAPPDATA%\PXToolBox\Dev\config\`).
9. **Sem leitura de `nexor.db`**: confirme (por inspeção dos logs do módulo, se necessário) que
   nenhuma operação deste botão lê ou altera `C:\Projetos\Nexor\nexor.db` ou qualquer banco do
   Nexor — a integração desta etapa é só abrir o processo.

**Verificado automaticamente** (sem abrir nenhum processo real, `NexorLauncherTests` e
`NexorConfigStoreTests`, perna `net48` de `dotnet test`): mesma cobertura do item 4b (primeira
configuração, caminho ausente vs. nunca configurado, espaços, inexistente, falha ao iniciar sem
interromper tentativas futuras, mensagem de sucesso nunca afirma "pronto para uso", persistência
em disco) mais a independência real entre as configurações de ListForge e Nexor. **Os 9 itens
acima, exercitando o botão real dentro do CorelDRAW com o Nexor real, estão pendentes de
validação manual.**

### 4c. ListForge Integrado — modo Clássico ("Abrir ListForge Integrado", nova nesta rodada)

Não depende de documento aberto no CorelDRAW. Ver `docs/listforge-classic-parity.md` para o
inventário completo de paridade com o standalone e `docs/ARCHITECTURE.md`, seção 10, para o
mecanismo de teclado.

**-1. Regressão da abertura da janela (CorelDRAW chegou a fechar de verdade por esta causa, ver
`docs/ARCHITECTURE.md`, seção 10, iteração 8 — confirmado corrigido pelo usuário em `0.1.0-dev.15`)**

1. Clique "Abrir ListForge Integrado" e confirme que a janela abre normalmente, sem o CorelDRAW
   fechar ou travar. Repita fechando e reabrindo a janela algumas vezes.
2. Clique no campo "Separador da entrada" e apague/retype o valor — confirme que não há nenhum
   efeito colateral perceptível (este era exatamente o campo cujo valor inicial no XAML disparava
   o crash).

**0. Regressão de teclado e atalhos (prioridade máxima — sem isto, nada abaixo funciona)**

1. Clique "Abrir ListForge Integrado". Clique no campo "Entrada / edição" e digite
   `JOÃO,07,G` seguido de Enter e `MARIA,10,BLM` — confirme que o texto inteiro aparece no campo,
   sem nenhuma ferramenta do Corel ser ativada (atenção especial a teclas que são atalho do
   Corel: `B`, `T`, `Z`, `S`, `F2`, etc.).
2. Teste Enter, Backspace, Delete e as quatro setas dentro do campo — devem funcionar como num
   editor de texto normal.
3. Teste Ctrl+C/Ctrl+V/Ctrl+A/Ctrl+Z (copiar/colar/selecionar tudo/desfazer) no campo de entrada.
4. Repita a digitação nos campos "Localizar"/"Substituir" (dentro do Expander) e no campo de URL
   do diálogo "Extrair lista de link..." (ver item 1 abaixo) — todos usam o mesmo mecanismo.
5. Clique fora da janela do ListForge Integrado, de volta no Corel, e confirme que os atalhos do
   próprio Corel voltam a funcionar normalmente (ex.: `B` ativa a ferramenta de Retângulo).
6. Feche e reabra a janela do ListForge Integrado (botão no painel principal) — confirme que a
   digitação continua funcionando (sem precisar reiniciar o CorelDRAW) e que nenhum hook
   duplicado causa efeito perceptível (ex.: caracteres digitados em duplicidade).

**1. Extrair lista de link (rodada anterior)**

1. Clique "Extrair lista de link...". Digite no campo de URL (confirme que a digitação funciona
   — mesmo mecanismo de teclado do item 0) um link válido que retorne uma lista no formato já
   aceito pelo ListForge standalone. Escolha "Nova lista" e clique "Extrair".
2. Confirme que o texto extraído substitui a entrada atual e que o Status informa quantos
   registros foram extraídos.
3. Repita com "Acrescentar à lista atual" (com texto já presente no campo de entrada) — confirme
   que o texto novo é adicionado ao final, sem apagar o que já estava lá.
4. Teste um link inválido (404, não-JSON, ou formato vazio) — confirme que aparece uma mensagem
   de erro clara, sem travar a janela nem alterar a entrada atual.
5. Compare o texto extraído do mesmo link no ListForge Standalone — deve ser idêntico.

**2. Lista avançada / mapeamento de peça por slot JSON (rodada anterior)**

1. Digite uma lista com múltiplos tamanhos por linha (ex.: `JOAO,10,P,G,M`). Abra o Expander
   "Lista avançada (mapeamento de peça por slot)" e marque "Ativar lista avançada" — confirme que
   aparecem seletores (um por posição de tamanho detectada).
2. Escolha um tipo de peça diferente em cada seletor (ex.: 1º = Regata, 2º = Manga Curta, 3º =
   Short) e clique Processar — confira na aba "Prévia JSON" que cada tamanho foi mapeado para o
   campo correto.
3. Edite a entrada (adicione/remova um tamanho) — confirme que a quantidade de seletores se
   ajusta automaticamente.
4. Escolha o mesmo tipo de peça em dois seletores e clique Processar — confirme que aparece um
   aviso de validação (ordem personalizada não pode repetir o mesmo tipo), sem travar a janela.
5. Compare o JSON gerado com a mesma entrada/ordem no ListForge Standalone (ativando "Lista
   avançada" lá também) — os campos devem ser idênticos.

**3. Resumo de tamanhos processados (rodada anterior)**

1. Processe uma lista com múltiplos tamanhos repetidos entre linhas diferentes (ex.:
   `JOAO,10,G` e `MARIA,11,2-G`). Confirme que aparece, acima da área de saída, um resumo somando
   a quantidade por tamanho (ex.: "G: 3").
2. Clique "Limpar tudo" — confirme que o resumo desaparece.

**4. Processar com prévia de confirmação (rodada anterior — agora o botão "Processar" padrão)**

1. Digite `ANA,10,G` e `BIA,11,M` (uma por linha) e clique "Processar" (o botão primário, com
   prévia). Confirme que abre uma janela "Prévia do processamento" mostrando: 2 registros
   analisados, 2 válidos, 0 avisos, 0 inválidos, "G: 1"/"M: 1" em Tamanhos, "Manga Curta: 2" em
   Tipos de peça.
2. Clique "Voltar" — confirme que a janela fecha, o Status diz "Processamento aguardando
   confirmação" e a aba de saída continua com o resultado anterior (nada foi processado).
3. Clique "Processar" de novo e, na prévia, clique "Processar agora" — confirme que a saída e o
   JSON são preenchidos normalmente, igual ao botão "Processar rápido".
4. Digite uma linha inválida (ex.: `PEDRO,99`, sem tamanho) e clique "Processar" — confirme que
   a prévia mostra 1 inválido, a seção "Confirmação" avisando para corrigir, e o botão "Processar
   agora" desabilitado. Clique "Voltar", clique no aviso correspondente na lista de avisos/erros
   da janela principal (atrás da prévia) e confirme que o cursor vai até a linha errada.
5. Compare a mesma entrada (`ANA,10,G\nBIA,11,M`) processando com prévia no ListForge Standalone
   — os números (registros/válidos/avisos/inválidos/tamanhos/tipos de peça) devem ser idênticos.
6. Clique "Processar rápido (sem prévia)" — confirme que processa direto, sem abrir a janela de
   prévia (mesmo comportamento de antes desta rodada).

**5. Bloqueio de resultado desatualizado, navegação de avisos/erros, Localizar/Substituir (rodada anterior)**

1. Processe uma lista, depois edite a entrada (ou mude separador/capitalização/ordenação) sem
   processar de novo — confirme que aparece o aviso "A entrada ou as opções mudaram..." e que
   clicar em Copiar/Salvar/Gerar JSON pede confirmação antes de continuar.
2. Digite uma lista com uma linha inválida — clique no item correspondente na lista de
   avisos/erros e confirme que o cursor vai até a linha certa no campo de entrada.
3. Use Localizar/Substituir (próximo/anterior/substituir/substituir tudo, com e sem "Diferenciar
   mai/min") no campo de entrada.

**6. Comparar entrada/saída (rodada anterior)**

1. Digite `ANA,10,G` e `BIA,11,M`, clique "Processar" (qualquer um dos dois botões) e depois
   "Comparar entrada/saída". Confirme que abre uma janela "Comparação entre entrada e saída" com
   o resumo (2 na entrada, 2 na saída, 2 correspondentes, 0 problemas) e a mensagem "Nenhum
   registro foi perdido ou adicionado durante a organização."
2. Clique num item da grade "Diferenças detectadas" e confirme que a linha correspondente é
   selecionada/rolada nos painéis "Entrada original"/"Saída organizada".
3. Use o filtro (ComboBox) para mostrar só uma categoria (ex.: "Correspondentes") e confirme que
   a grade e o texto "Exibindo X de Y" atualizam. Use "Diferença anterior"/"Próxima diferença"
   para navegar entre os problemas (se não houver nenhum nesta entrada, teste com uma entrada que
   gere avisos — ex.: altere manualmente só a saída processando uma lista diferente depois).
4. Clique "Copiar relatório" e confirme (colando em qualquer editor) que o texto inclui o resumo
   e as diferenças para revisão.
5. Clique "Fechar". Edite a entrada (sem processar de novo) e clique "Comparar entrada/saída" —
   confirme que aparece o aviso "A entrada ou as opções foram alteradas..." em vez de abrir a
   comparação com dados antigos.
6. Clique "Limpar tudo" e depois "Comparar entrada/saída" sem processar — confirme a mensagem
   "Não há dados suficientes para comparação."
7. Processe `joão da silva,10,g` com capitalização "Tudo maiúsculo" e compare — confirme que a
   diferença de nome aparece como "Transformado" (não "Alterado"), com o motivo "capitalização
   configurada".
8. Compare a mesma entrada/opções no ListForge Standalone (botão "Comparar entrada e saída" lá)
   — os números do resumo e as categorias devem ser idênticos.

**7. Salvar avançado (rodada anterior)**

1. Com Lista avançada DESATIVADA, clique "Salvar avançado..." — confirme a mensagem "Ative
   Lista avançada antes de usar o Salvar avançado." (sem abrir diálogo nenhum).
2. Ative Lista avançada, mas sem processar — clique "Salvar avançado..." — confirme "Processe a
   lista antes de usar o Salvar avançado."
3. Processe uma lista com Lista avançada ativa, clique "Salvar avançado..." — confirme que abre
   o diálogo com nome base sugerido, modo "Arquivos soltos" marcado por padrão, e que **digitar
   no campo de nome funciona normalmente** (mesmo mecanismo de teclado — não deve ativar
   ferramenta nenhuma do Corel). Escolha uma pasta, clique "Salvar" — confirme 3 arquivos
   (`-entrada.txt`, `-saida.txt`, `.json`) na pasta escolhida, com o conteúdo esperado.
4. Repita processando a mesma lista e salvando com o MESMO nome base/pasta — confirme que NÃO
   sobrescreve os 3 arquivos anteriores: um novo conjunto com sufixo `_v2` aparece ao lado.
5. Repita no modo "Arquivo ZIP" — confirme um único `.zip` com os 3 arquivos dentro (sem pasta
   extra), comparando o conteúdo com a mesma exportação no ListForge Standalone.
6. Clique "Cancelar" no diálogo — confirme que nenhum arquivo foi criado.
7. Edite a entrada depois de processar (sem reprocessar) e clique "Salvar avançado..." — confirme
   o mesmo aviso de resultado desatualizado já usado por Copiar/Salvar/Gerar JSON.
8. Para testar falha de gravação: escolha uma pasta e, antes de clicar "Salvar" no diálogo, torne
   essa pasta somente leitura (ou escolha uma pasta sem permissão) — confirme uma mensagem de
   erro clara, sem a janela principal travar ou perder a lista digitada.

**8. Configurações (nova nesta rodada)**

1. Clique "Configurações...". Confirme que o texto no topo mostra o caminho real
   (`%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-settings.json`).
2. Altere o tamanho de fonte (ex.: 18), clique "Salvar" — confirme que os campos de entrada/
   saída/JSON mudam de tamanho imediatamente. Feche e reabra o ListForge Integrado (fechar a
   janela e clicar "Abrir ListForge Integrado" de novo) — confirme que o tamanho de fonte
   escolhido **persistiu**.
3. Digite um valor inválido de fonte (ex.: "abc" ou "999") e clique "Salvar" — confirme uma
   mensagem de validação clara, sem fechar a janela nem salvar nada.
4. Ative "Usar sempre esta pasta ao exportar" e "Usar sempre este nome base ao exportar",
   escolha uma pasta e um nome, salve. Processe uma lista e clique "Salvar saída" — confirme que
   **não abre diálogo nenhum**, salvando direto no local configurado.
5. Mude o formato padrão do Salvar avançado para "Arquivo ZIP", salve, feche e reabra o
   ListForge Integrado, clique "Salvar avançado..." — confirme que o ZIP já vem marcado por
   padrão.
6. Clique "Restaurar padrões" — confirme o aviso explicando que afeta só este módulo — e depois
   "Cancelar" no diálogo de Configurações — confirme que a configuração salva anteriormente **não
   foi alterada** (reabra Configurações para confirmar).
7. Clique "Exportar configurações...", salve o `.json`. Mude algum valor sem salvar, clique
   "Importar configurações..." e escolha o arquivo exportado — confirme que os campos voltam aos
   valores exportados (ainda exige "Salvar" para valer).
8. Clique "Abrir pasta de configuração" — confirme que o Explorer abre exatamente
   `%LocalAppData%\PXToolBox\Dev\config\`, e que o arquivo `listforge-integrated-settings.json`
   está lá (nunca um arquivo do ListForge Standalone).
9. Para testar configuração corrompida: feche o PX, edite
   `listforge-integrated-settings.json` manualmente deixando o JSON inválido (ex.: apague uma
   chave), reabra o ListForge Integrado — confirme um aviso na barra de status explicando que a
   configuração foi preservada num arquivo "`.corrompido-<data>.json`" na mesma pasta e que os
   padrões foram usados, **sem o PX sobrescrever o arquivo original por conta própria**.
10. Confirme, ao longo de todos os itens acima, que nada disto abre, lê ou grava qualquer
    arquivo do ListForge Standalone (o atalho "Abrir ListForge Standalone ↗" continua
    funcionando independente, com sua própria configuração).

**9. Perfis de trabalho (nova nesta rodada)**

1. Configure o separador, a capitalização, a ordenação e ative "Lista avançada" com uma ordem de
   tipos de peça. Clique "Novo..." no combo "Perfil de trabalho", dê um nome (ex.: "Camisetas") —
   confirme que o novo perfil aparece selecionado no combo e que os controles continuam com os
   mesmos valores (o perfil novo nasce com o estado atual da tela).
2. Mude o separador. Confirme que o nome do perfil no combo passa a mostrar um "*" (alteração não
   salva) e que o botão "Salvar perfil" fica habilitado. Clique "Salvar perfil" — confirme que o
   "*" desaparece.
3. Mude o separador outra vez, mas agora clique "Descartar alterações" — confirme que o campo
   volta ao valor salvo do perfil (não ao valor digitado) e que o "*" desaparece.
4. Crie um segundo perfil ("Calças", por exemplo) com opções diferentes do primeiro. Alterne entre
   os dois perfis no combo (sem alterações pendentes) — confirme que cada troca aplica exatamente
   as opções daquele perfil e que **a lista digitada em "Entrada" nunca é apagada** ao trocar.
5. Com um perfil selecionado, mude uma opção (gerando alteração não salva) e troque para o outro
   perfil no combo — confirme a pergunta "Existem alterações não salvas... Sim/Não/Cancelar".
   Teste as três respostas: "Cancelar" mantém o perfil atual selecionado; "Não" descarta e troca;
   "Sim" salva no perfil anterior e troca.
6. Clique "Renomear..." no perfil "Camisetas", mude o nome — confirme que o combo atualiza.
   Tente renomear para um nome já usado por outro perfil (ex.: o nome atual de "Calças") —
   confirme a mensagem de nome duplicado, sem aplicar a troca.
7. Clique "Duplicar" em qualquer perfil que não seja o Padrão — confirme uma cópia independente
   ("Nome - Cópia") que se torna o perfil ativo; edite uma opção na cópia e confirme que o
   original não é afetado.
8. Clique "Excluir..." num perfil que não seja o Padrão — confirme o pedido de confirmação e que,
   após excluir, se esse perfil estava ativo, o PX volta automaticamente para o perfil "Padrão".
   Selecione o perfil "Padrão" e confirme que os botões "Renomear..."/"Excluir..." aparecem
   desabilitados (o Padrão nunca pode ser renomeado nem excluído).
9. Clique "Restaurar perfil Padrão..." — confirme o aviso de que afeta só o perfil Padrão (não os
   demais), e que, após confirmar, o perfil Padrão volta às configurações oficiais
   (separador "," / Original / Original / lista avançada desativada).
10. Processe uma lista com um perfil ativo e depois troque de perfil (ou altere uma opção) —
    confirme o mesmo aviso de resultado desatualizado já usado por Copiar/Salvar/Gerar JSON (as
    opções do perfil afetam o processamento, então contam como "entrada mudou").
11. Feche e reabra o ListForge Integrado — confirme que todos os perfis criados, o perfil ativo
    selecionado e suas opções **persistiram** exatamente como estavam.
12. Abra "Configurações..." com um perfil não-Padrão ativo — confirme que o texto informa qual
    perfil está sendo editado ali (formato do Salvar avançado/pasta/nome), e que salvar ali não
    afeta os outros perfis.
13. Confirme, ao longo de todos os itens acima, que nada disto abre, lê ou grava qualquer arquivo
    do ListForge Standalone.

**10. Tamanhos customizados (nova nesta rodada)**

1. Clique "Tamanhos customizados...". Confirme que os 4 grupos (Masculino/Feminino/Infantil/
   Meião) aparecem com os tamanhos oficiais pré-preenchidos (ex.: Masculino: "PP, P, M, G, GG,
   XG, XGG, XXGG, XLGG") e que o resumo abaixo lista os tamanhos válidos atuais.
2. Adicione um tamanho novo ao grupo Masculino (ex.: "PP, P, M, G, GG, XG, XGG, XXGG, XLGG,
   GIGANTE"), clique "Salvar". Processe uma lista contendo "FULANO,2,GIGANTE" — confirme que é
   reconhecido normalmente (sem erro de validação).
3. Remova um tamanho que estava na configuração oficial (ex.: apague "XG" do campo de Masculino),
   salve. Processe uma lista contendo "FULANO,2,XG" — confirme que aparece como erro de
   validação ("Tamanho inválido: XG"), **nunca** convertido silenciosamente para outro tamanho
   (ex.: XGG).
4. Digite uma ordenação personalizada diferente da oficial num grupo (ex.: inverta a ordem dos
   tamanhos-base do grupo Infantil) e confirme, após processar uma lista com tamanhos desse
   grupo, que a ordem de reconhecimento/exportação reflete a configuração salva.
5. Apague todo o conteúdo do campo "Tamanhos base" de um grupo (deixe vazio) e clique "Salvar" —
   confirme a mensagem de validação ("Informe ao menos um tamanho-base para {grupo}."), que a
   janela continua aberta com os valores digitados nos OUTROS grupos preservados, e que nada é
   salvo no disco.
6. Com um tamanho customizado válido digitado, clique "Cancelar" — confirme que a configuração
   ativa (verificada reabrindo a tela) **não foi alterada**.
7. Clique "Restaurar tamanhos padrão" — confirme o aviso de que afeta só os tamanhos (não perfis,
   fonte ou Salvar avançado) e, depois de "Salvar", que os 4 grupos voltam aos valores oficiais.
8. Processe uma lista, depois abra "Tamanhos customizados..." e salve qualquer alteração —
   confirme o mesmo aviso de resultado desatualizado já usado por Copiar/Salvar/Gerar JSON
   (tamanhos afetam o processamento, então contam como "entrada mudou").
9. Troque de perfil de trabalho (combo na janela principal) e confirme que os tamanhos
   customizados **continuam os mesmos** — são independentes de perfil, valem para todos.
10. Feche e reabra o ListForge Integrado — confirme que os tamanhos customizados **persistiram**
    exatamente como foram salvos.
11. Confirme, ao longo de todos os itens acima, que nada disto abre, lê ou grava qualquer arquivo
    do ListForge Standalone.

**11. Backup automático de entrada (nova nesta rodada)**

1. Abra uma lista nova (sem arquivo associado), digite algo e clique "Salvar Como" para um
   arquivo novo — confirme que salva normalmente e que NENHUM backup é criado (primeiro
   salvamento, nada para preservar).
2. Edite a entrada e clique "Salvar Entrada" (sobrescrevendo o mesmo arquivo) — confirme que um
   backup do conteúdo ANTERIOR aparece em "Abrir pasta de backups", com nome
   `{nome}_{data}_{hora}{extensão}`, e que o arquivo original foi atualizado com o novo conteúdo.
3. Clique "Salvar Entrada" de novo SEM alterar nada — confirme que NENHUM backup novo é criado
   (conteúdo em disco é igual ao que seria salvo).
4. Clique "Salvar Como" escolhendo um arquivo que já existe (de outra lista, por exemplo) —
   confirme que um backup do conteúdo ANTERIOR desse arquivo é criado, mesmo que o conteúdo da
   lista atual seja diferente (backup incondicional em "Salvar como").
5. Salve duas listas com o mesmo nome de arquivo, mas vindas de pastas diferentes, sobrescrevendo
   cada uma mais de uma vez — confirme que os backups de cada uma coexistem sem um sobrescrever o
   outro (nome com sufixo numérico se colidirem no mesmo segundo).
6. Cancele o diálogo de "Salvar Como" — confirme que nada foi alterado (nem o arquivo, nem a
   pasta de backups).
7. Para testar falha de backup: torne a pasta de backups temporariamente somente leitura (ou
   sem permissão) antes de salvar — confirme uma mensagem clara informando que o backup falhou e
   que a entrada NÃO foi salva, e que o arquivo original permanece intacto (abra-o para
   confirmar).
8. Confirme, abrindo os arquivos envolvidos, que o conteúdo do backup e do arquivo salvo estão
   corretos e íntegros (nunca truncados, nunca em outro formato).
9. Clique "Abrir pasta de backups" no painel Diagnóstico — confirme que abre
   `%LocalAppData%\PXToolBox\Dev\ListForge\Backups\`, nunca a pasta de backups do ListForge
   Standalone.

**12. Diagnóstico e pacote de suporte (concluído nesta rodada)**

1. Abra o ListForge Integrado — no painel Diagnóstico, confirme que o texto de informações de
   suporte mostra: versão do PX Tool Box DEV, versão do núcleo ListForge, versão do CorelDRAW
   (deve mostrar a versão real do Corel aberto, não "não disponível"), runtime/arquitetura,
   caminho de configuração e caminho de logs.
2. Clique "Copiar informações de suporte" e cole em qualquer editor de texto — confirme que o
   texto colado é **idêntico** ao exibido na tela, e que não contém nenhum segredo/credencial.
3. Clique "Abrir pasta de logs" — confirme que abre
   `%LocalAppData%\PXToolBox\Dev\logs\`, com um arquivo `pxtoolbox-integrated-<data>.log` do
   dia atual.
4. Abra esse arquivo de log num editor de texto — confirme que contém a linha de "Startup" com
   as versões, e que **não** contém nenhum conteúdo de lista, nome/número de jogador digitado,
   nem o texto de nenhuma mensagem de excessão (só o tipo da excessão e a pilha, quando houver
   uma falha registrada).
5. Force uma falha (ex.: processar com uma entrada inválida que gere excessão, ou tentar salvar
   avançado numa pasta sem permissão) — confirme que aparece uma nova linha "ERROR" no log
   correspondente à ação, sem derrubar o CorelDRAW nem ocultar a mensagem de erro já mostrada na
   interface.
6. Clique "Gerar pacote de suporte..." — confirme que a mensagem de confirmação lista o conteúdo
   (versões/ambiente, resumo seguro de configurações, logs recentes) antes de pedir o destino.
7. Cancele na mensagem de confirmação — confirme que nada foi gerado. Clique de novo e cancele
   na escolha da pasta — confirme de novo que nada foi gerado.
8. Gere o pacote de verdade, escolhendo uma pasta de destino — confirme que um `.zip` aparece
   lá, com nome `pxtoolbox-listforge-support-<data>-<hora>.zip`.
9. Abra o `.zip` e confirme que contém exatamente `support-info.txt`, `settings-summary.txt` e
   uma pasta `logs/` com os logs recentes — **nunca** a lista digitada, nenhum documento do
   Corel, nenhum backup de entrada, nenhum arquivo de produção.
10. Abra `settings-summary.txt` e confirme que **não** contém o nome do perfil de trabalho, a
    pasta de saída nem o nome de lista padrão configurados (só campos seguros: tamanho de fonte,
    opções de processamento, resumo de tamanhos customizados).
11. Repita a geração do pacote enquanto o arquivo de log do dia está aberto/travado por outro
    programa (ex.: um editor de texto com bloqueio exclusivo) — confirme que o pacote ainda é
    gerado, com uma mensagem informando que aquele log específico não pôde ser incluído.
12. Confirme, ao longo de todos os itens acima, que a pasta de backups de entrada (item 11,
    acima) permanece intacta — a retenção de logs nunca deve removê-la ou seu conteúdo.

**13. Histórico de processamentos (nova nesta rodada)**

1. Com o histórico vazio (primeira execução, ou depois de "Limpar histórico"), clique
   "Histórico..." — confirme a mensagem "Nenhum processamento foi registrado ainda." (sem
   travar).
2. Processe uma lista e clique "Processar"/"Processar com prévia" várias vezes — confirme que
   **nenhum** registro aparece no histórico só por processar (abra "Histórico..." de novo para
   checar).
3. Clique "Salvar saída", escolhendo um arquivo — confirme que um novo registro aparece no topo
   do histórico, com a data/hora atual, a quantidade de linhas e o caminho exato do `.txt`
   salvo.
4. Clique "Gerar JSON" — confirme um novo registro com o caminho do `.json`.
5. Ative "Lista avançada", processe e clique "Salvar avançado..." (arquivos soltos e, numa
   segunda rodada, modo ZIP) — confirme um registro com o caminho do ZIP (modo ZIP) ou de um dos
   arquivos gerados, incluindo o `.json` quando existir (modo soltos).
6. Selecione um registro e clique "Abrir pasta da saída" — confirme que o Explorer abre com o
   arquivo correspondente já selecionado. Apague esse arquivo manualmente e repita — confirme a
   mensagem "O arquivo de saída não foi encontrado." (sem travar).
7. Abra um arquivo de texto pelo botão "Abrir Lista", extraia uma lista por "Extrair lista de
   link" e aplique uma edição manual da saída/JSON ("Permitir edição... nesta sessão" ativo) —
   salve a saída/JSON após cada uma e confirme, no histórico, que o nome/tipo de origem exibido
   corresponde: nome do arquivo (sem o caminho completo) para "Abrir Lista", "Lista extraída de
   link" para o link, e "Lista de saída editada"/"JSON editado" para a edição manual, conforme o
   caso. Clique "Limpar tudo" e salve algo colado diretamente — confirme "Texto colado".
8. Clique "Limpar histórico" — confirme a pergunta de confirmação Sim/Não; clique Não e confirme
   que nada foi apagado; clique de novo e confirme Sim — confirme que a lista fica vazia e que
   NENHUM arquivo de saída já salvo em disco foi apagado (abra um dos arquivos para confirmar).
9. Force uma falha ao gravar o histórico (ex.: torne
   `%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-history.json` temporariamente
   somente leitura) e clique "Salvar saída" — confirme que a exportação em si **continua
   funcionando normalmente** (arquivo salvo, mensagem de sucesso) e que a falha do histórico só
   aparece no log do módulo (`Abrir pasta de logs`), nunca numa mensagem de erro que sugira que a
   exportação falhou.
10. Gere o pacote de suporte (painel Diagnóstico) e confirme que o `.zip` **não** contém o
    arquivo `listforge-integrated-history.json` nem qualquer nome de arquivo/lista do histórico.
11. Feche e reabra o ListForge Integrado — abra "Histórico..." de novo e confirme que os
    registros **persistiram** (lidos do arquivo em disco, não só da sessão anterior).

**14. Importação de documentos — PDF/Word/Excel (nova nesta rodada)**

Prepare previamente um PDF real com texto selecionável, um `.docx` real (parágrafos e, se
possível, uma tabela) e um `.xlsx` real (várias linhas, incluindo um valor com zero à esquerda
como tamanho, ex.: "07", formatado como texto) — todos com nomes, acentos e números reais de
lista, para comparar com o ListForge Standalone usando os MESMOS arquivos.

1. Clique "Abrir Lista...", selecione o PDF — confirme que o conteúdo extraído aparece na própria
   caixa de entrada (editável), com uma mensagem avisando para revisar antes de processar.
   Compare o texto extraído com o mesmo PDF aberto no ListForge Standalone — devem corresponder.
2. Repita com o `.docx` e com o `.xlsx` — confirme que acentos (ex.: "João") e o valor com zero à
   esquerda ("07") aparecem exatamente assim, nunca convertidos (ex.: nunca "7").
3. Se o `.xlsx` tiver mais de uma planilha, confirme que só a PRIMEIRA é lida (mesma regra do
   standalone — sem diálogo de escolha de planilha).
4. Digite algo na entrada (deixando alterações não salvas) e importe outro documento — confirme
   a pergunta de confirmação; responda Não e confirme que a entrada NÃO foi substituída; importe
   de novo e responda Sim — confirme que substitui normalmente.
5. Tente importar um PDF digitalizado (sem texto selecionável, só imagem) — confirme a mensagem
   explicando que não foi possível extrair texto e sugerindo imagem/OCR, sem apresentar uma
   importação vazia como sucesso.
6. Tente abrir uma imagem (.png/.jpg) pela mesma caixa de diálogo (escolha "Todos os arquivos") —
   confirme a mensagem específica informando que OCR não está disponível nesta distribuição, sem
   travar e sem tentar converter a imagem.
7. Tente importar um arquivo corrompido/renomeado (ex.: um `.txt` renomeado para `.docx`) —
   confirme uma mensagem de erro clara, sem travar o Corel e sem apagar a entrada atual.
8. Confirme que a extração de um arquivo grande (vários MB) não bloqueia a interface do PX nem
   do Corel — outros painéis devem continuar respondendo durante a importação.
9. Depois de importar, confirme que Processar/Comparar entrada-saída/Edição manual/Salvar
   avançado/Histórico continuam funcionando normalmente sobre o texto importado, como se tivesse
   sido digitado à mão.
10. Confirme, abrindo os logs do módulo, que nenhuma linha contém o conteúdo da lista importada
    (nomes/números de jogador) — só o tipo da operação e, no máximo, o caminho do arquivo.

### 4c-bis. ListForge Integrado — modo PX (nova nesta rodada, iteração 23)

Não depende de documento aberto no CorelDRAW. Modo "PX — em desenvolvimento", sobre o MESMO
estado de sessão do modo Clássico (ver item 4c) — nenhum destes passos testa uma segunda
implementação de regras, só a apresentação PX e a troca entre os dois modos. Ver
`docs/ARCHITECTURE.md`, seção 10, "Modo PX implementado", para o mecanismo de reparenting.

1. Com "Abrir ListForge Integrado" aberto no modo Clássico (padrão), clique no botão "PX — em
   desenvolvimento" no topo da janela — confirme que a janela muda para o layout de 3 etapas
   (Lista/Revisão/Resultado) sem fechar nem travar o Corel.
2. Antes de trocar de modo, digite algo em "Entrada / edição" no Clássico (ex.:
   `JOÃO,07,G`); troque para PX — confirme que o MESMO texto aparece na etapa Lista do PX (não
   uma caixa vazia). Edite o texto em PX e volte para Clássico — confirme que a edição persiste
   lá também. Repita a ida e volta algumas vezes.
3. Na etapa Lista do PX, confirme que separador/capitalização/ordenação, Localizar/Substituir e
   a seção expansível "Lista avançada" (com a mesma quantidade de posições que apareceria no
   Clássico para a mesma configuração) funcionam normalmente.
4. Clique "Avançar para Revisão →" — confirme contagens reais (registros/válidos/avisos/
   inválidos) e uma tabela com uma linha por registro da entrada atual — nunca dados de exemplo.
5. Digite uma linha sem nome (ex.: só `,07,G`) e volte a Revisão — confirme que aparece um aviso
   "nome não informado" na lista de avisos; clique nesse aviso — confirme que a janela volta à
   etapa Lista com a linha correspondente selecionada/visível no campo de entrada.
6. Na Revisão, clique "Processar" — confirme um indicador "Processando..." temporário (sem
   nenhuma porcentagem ou tempo estimado) e que a janela abre a etapa Resultado ao concluir.
   Clique "Processar" repetidamente/rápido — confirme que não dispara dois processamentos
   simultâneos.
7. Em Resultado, confirme as abas "Lista organizada"/"JSON" mostrando o resultado real
   (inspecionável antes de copiar/exportar) e que Copiar/Salvar/Gerar JSON/Comparar
   entrada-saída/Salvar avançado/Histórico funcionam exatamente como no Clássico.
8. Volte a Lista, altere a entrada (sem voltar a Revisão) e clique "Processar" direto —
   confirme que a tela RECUSA processar o instantâneo desatualizado e atualiza a Revisão em vez
   de produzir um resultado associado à entrada antiga.
9. Force uma falha de processamento se possível (ex.: entrada que o núcleo rejeita) — confirme
   que a entrada/opções permanecem intactas e que o erro aparece na própria etapa Revisão, sem
   fechar a janela nem limpar nada.
10. Com algo digitado/processado, clique "Nova lista" — confirme que pergunta antes de
    descartar. Com a entrada vazia e sem resultado, clique "Nova lista" novamente — confirme que
    NÃO pergunta (nada a perder).
11. Confirme que "Configurações...", "Tamanhos customizados...", o combo de perfil de trabalho,
    o texto de acesso, a barra de status e o Expander "Diagnóstico" continuam acessíveis dentro
    do modo PX (topo da janela).
12. Repita o teste de teclado do item 4c (passo "0") dentro do modo PX — digite acentuação,
    Enter, Backspace, setas — confirme que nenhuma tecla ativa uma ferramenta do Corel.
13. Feche e reabra "Abrir ListForge Integrado" depois de ter deixado o modo PX ativo — confirme
    que a janela abre novamente no modo PX (preferência por perfil persistida).

### 4d. Inspecionar Shape.Properties (nova — ferramenta de diagnóstico, exclusiva DEV)

Sem relação com o ListForge Integrado. Painel DEV → "Diagnóstico" → "Inspecionar
Shape.Properties". **100% somente leitura** — confirme ao longo de todos os itens abaixo que
nenhuma Shape.Properties, nome, camada ou documento é alterado.

1. Sem nenhum documento aberto no CorelDRAW, clique "Inspecionar propriedades" (rótulo até a
   dev.48: "Inspecionar Shape.Properties") — confirme a
   mensagem "Nenhum documento aberto." (janela abre, sem travar).
2. Com um documento aberto e nada selecionado, clique — confirme "Selecione um objeto para
   inspecionar."
3. Selecione 2+ objetos, clique — confirme "Selecione apenas um objeto."
4. Selecione um objeto comum (sem nenhuma Shape.Properties conhecida), clique — confirme que
   abre o inspetor mostrando Nome/Tipo/Camada/Página/StaticID/Largura/Altura corretos, e "Count:
   0" com a mensagem "Nenhuma Shape.Properties encontrada neste objeto." (deixando claro que não
   é erro).
5. **Experimento SISBolt**: com o mesmo objeto, (A) copie o relatório ("Copiar tudo", cole num
   editor de texto para guardar); (B) ative "Tamanho padrão" no SISBolt nesse objeto, abra o
   inspetor de novo (ou clique "Atualizar") e copie o relatório de novo; (C) copie o objeto B
   para outro documento, inspecione-o e copie; (D) desative "Tamanho padrão" no objeto original e
   copie mais uma vez. Compare os 4 relatórios — confirme que `Count` e as entradas de
   `Properties` mudam entre eles de forma consistente com ativar/desativar "Tamanho padrão" (sem
   interpretar qual propriedade especificamente — só observar os dados brutos, como pedido).
6. Com o inspetor aberto, exclua o objeto original no Corel (ou feche o documento) e clique
   "Atualizar" — confirme "O objeto original não está mais disponível." (sem travar a janela).
7. Clique "Copiar tudo" e cole em qualquer editor — confirme que o texto corresponde exatamente
   ao que está na grade. Clique "Salvar relatório..." e confirme que o `.txt` salvo tem o mesmo
   conteúdo.
8. Se encontrar (ou puder forçar) um objeto com uma propriedade cujo valor não seja um tipo
   simples (ex.: um Point/Vector/Curve armazenado), confirme que a linha mostra `<tipo COM:
   ...>` em vez de travar ou mostrar algo ilegível, e que as OUTRAS propriedades do mesmo objeto
   continuam aparecendo normalmente.

### 4e. Controles de Tamanho Padrão na seção Grid (DEV)

Este roteiro valida a escrita feita pelo PX contra o contrato confirmado do SISBolt: nome
exato `KEEP_DESIGN_SIZE`, ID 0 Boolean `True` e ID 1 Double igual à largura atual do objeto.

1. Selecione um objeto sem `KEEP_DESIGN_SIZE` e confirme no inspector que a propriedade não
   existe. Clique **Alternar Tamanho Padrão** e confirme ID 0/ID 1; confirme que qualquer
   propriedade de nome vazio ou outro nome permaneceu inalterada.
2. Altere a largura do objeto sem usar o comando do PX, clique **Atualizar Tamanho Padrão** e
   confirme que o ID 1 mudou para a nova largura, sem o PX redimensionar o objeto.
3. Clique **Alternar Tamanho Padrão** novamente e confirme que todas as entradas com nome
   `KEEP_DESIGN_SIZE` foram removidas fisicamente, em vez de permanecerem com `False`.
4. Selecione vários objetos em estado misto e clique **Alternar Tamanho Padrão**; confirme que
   todos terminam marcados e atualizados. Com todos marcados, repita e confirme que todos são
   removidos. Confirme também a mensagem de status e que um único Ctrl+Z desfaz toda a seleção.
5. Selecione um grupo e confirme que somente o grupo recebe a propriedade; os filhos não são
   percorridos. Copie o grupo/objeto para outro documento e confirme no inspector que a
   propriedade acompanha o objeto.
6. **Tamanho Padrão — estado no botão (dev.48, sem smoke test ainda).** Não há mais linha de texto de estado.
   Confirme ao mudar a seleção: todos marcados → "Alternar Tamanho Padrão" com a cor de acento
   (azul, como era o "Preparar Grid"); estado misto → botão comum só com borda azul mais
   grossa; nenhum marcado, sem seleção ou sem documento → botão comum. A dica do botão diz o
   estado em texto. Confirme também que "Preparar Grid" agora tem a aparência dos demais
   botões.

### 4f. Persistência de perfis de trabalho/configurações/tamanhos (correção da iteração 27)

Valida a correção do erro "Não foi possível salvar os perfis de trabalho: Não foi possível
remover o arquivo a ser substituído." — ver `docs/ARCHITECTURE.md`, seção 17. A lógica de
gravação (`Integrations/AtomicFileWriter.cs`) já foi validada automaticamente com bloqueios
reais de arquivo (ver `AtomicFileWriterTests`); este roteiro cobre o que só pode ser
confirmado dentro do CorelDRAW real.

1. **Criar/renomear/duplicar/excluir perfis**: no ListForge Integrado, abra "Perfil de
   trabalho" e exercite os quatro botões em sequência — confirme que cada ação aparece
   imediatamente na lista e que o status não mostra nenhum erro.
2. **Salvar repetidamente**: clique "Salvar perfil" várias vezes seguidas, rapidamente —
   confirme que nenhuma delas produz o erro relatado.
3. **Fechar e reabrir**: feche o ListForge Integrado (e, se possível, o CorelDRAW) depois de
   criar/renomear um perfil, reabra e confirme que a alteração está lá — nunca confie só na
   tela aberta como prova de que foi salva.
4. **Arquivo bloqueado ou sem permissão**: com o ListForge Integrado aberto, abra
   `%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-settings.json` em outro programa
   de um jeito que o bloqueie para escrita (ex.: um editor que mantém o arquivo aberto com
   exclusividade), tente salvar um perfil no PX e confirme: a alteração continua visível na
   tela do PX, o status/mensagem deixa claro que NÃO foi salva em disco, e nada trava o PX.
   Libere o bloqueio e tente salvar de novo — confirme que funciona.
5. **Preservação do arquivo anterior após falha**: repita o passo 4, mas em vez de liberar o
   bloqueio, feche o PX/Corel com a alteração ainda não salva. Reabra e confirme que o
   arquivo de configuração contém exatamente o estado ANTERIOR à tentativa falha (perfis
   antigos intactos, nada truncado ou corrompido).
6. **Configurações e tamanhos customizados usam a mesma rotina**: repita uma verificação
   rápida do passo 4 também pela tela "Configurações" e pela tela "Tamanhos customizados" —
   confirme a mesma mensagem clara de "não foi salvo" em vez do erro técnico original.

### 4g. Preferências portáteis (Configurações → "Exportar/Importar preferências...")

Distinto do botão já existente "Exportar/Importar configurações..." — este cobre só o recurso
novo (ver `docs/ARCHITECTURE.md`, seção 18).

1. Exporte preferências com um nome de perfil que tenha acento (ex.: "Padrão") e confirme, num
   editor de texto, que o nome do perfil NÃO foi alterado no JSON (sem minúsculas forçadas).
2. Importe esse mesmo arquivo numa instalação limpa (ou depois de "Restaurar padrões") e
   confirme o resumo mostrado antes de aplicar, e que "Salvar" ainda é necessário para valer.
3. Edite o arquivo manualmente para citar um perfil que não existe nesta instalação e confirme
   que a importação ignora essa entrada (reportada, não um erro fatal) sem criar perfil novo.
4. Edite o arquivo para um `schemaVersion` diferente e confirme a mensagem clara de versão não
   suportada. Edite para um tamanho de fonte fora do intervalo e confirme que só esse campo é
   ignorado (o restante do arquivo continua aplicável).
5. Confirme que um backup recuperável aparece na pasta de configuração antes da importação ser
   aplicada.
6. **(dev.78)** Em "Personalizar painel…", renomeie uma seção, crie uma seção com duas
   ferramentas e ligue "Mostrar nomes" nos Favoritos. Exporte as preferências; depois
   "Restaurar padrão" no painel, desligue "Mostrar nomes" e importe o arquivo → o resumo cita a
   personalização e "Mostrar nomes"; após "Salvar", o painel volta exatamente como estava (nomes,
   seção criada na mesma posição, ferramentas nela, Favoritos com nome), sem reabrir o Corel.
   Exporte de novo com o painel sem personalização e importe → a personalização é desfeita.

### 4h. Contexto de pedido (Produção → "Carregar contexto de pedido...")

Ver `docs/integrations/nexor-order-context.md` para o contrato e
`docs/ARCHITECTURE.md`, seções 19–20, para o que foi e não foi implementado (só a importação no
PX; exportação do lado do Nexor ainda não existe).

1. Carregue `docs/integrations/order-context-example.json` — confirme que a janela mostra
   "Contexto recebido — não sincronizado com o Nexor", a data de exportação convertida para o
   horário local, o rolo/máquina de origem e a etiqueta de pedido.
2. Confirme que o caminho do arquivo referenciado aparece como texto (nunca aberto
   automaticamente) e que a janela informa se o arquivo existe ou não nesta máquina.
3. Com o contexto já carregado, clique "Carregar outro contexto..." — confirme que pede
   confirmação antes de substituir.
4. Clique "Limpar contexto" — confirme que pede confirmação e que a janela fecha/limpa sem
   afetar o ListForge Integrado (entrada/resultado preservados se estiver aberto ao mesmo
   tempo).
5. Tente carregar um JSON inválido e um com `schemaVersion` desconhecido — confirme mensagens
   claras no status do painel, sem travar.
6. Confirme que o botão funciona sem nenhum documento aberto no CorelDRAW.

**(dev.62) Resumo na seção Produção — pendente no Corel.** Gere no Nexor um arquivo de um
trabalho COM pedido e outro de um trabalho SEM pedido.

1. Docker estreito, seção Produção: sem contexto, não há resumo e "Ver detalhes"/"Limpar
   contexto" estão desabilitados (a dica explica).
2. Carregue o arquivo com pedido: o resumo mostra rolo/máquina, trabalho de impressão (nome do
   arquivo, tipo e tecido), "Pedido informado" e a data; nada é cortado. A janela de detalhes não
   abre sozinha.
3. "Carregar contexto…" de novo e cancele; depois escolha um arquivo inválido: o resumo
   continua o mesmo e o status diz que o contexto anterior foi mantido. Repita com "Colar
   caminho do contexto…".
4. Carregue o arquivo sem pedido: é aceito (até a dev.61 era recusado); a linha "Pedido
   informado" some; "Ver detalhes" mostra "(não informado neste arquivo)".
5. "Limpar contexto" → Sim: o resumo some, os botões desabilitam, a janela de detalhes (se
   aberta) fecha. O Nexor e o arquivo não mudam.
6. **(dev.63)** Com o arquivo com pedido carregado, Preparação e saída → "Usar contexto de
   produção": "Nome do layout" recebe o pedido informado e o status mostra o valor anterior.
   Preparar Escudo salva "Escudo - <pedido>.cdr". Com o arquivo sem pedido, o campo recebe o
   nome do arquivo de origem. Depois de "Limpar contexto", o botão fica desabilitado.
7. **(dev.64)** Com o arquivo com pedido carregado:
   - Outras ferramentas — Objetos → "Usar contexto de produção": o campo recebe o pedido;
     selecionar objetos e "Renomear Objetos Selecionados" aplica o nome.
   - Produção → "Nomear página ativa com o contexto": a aba da página mostra o pedido; o
     status traz o nome anterior; Ctrl+Z volta o nome.
   - Preparação e saída: "Destino" termina em `\<nome do rolo>`. Preparar Escudo cria a
     subpasta (se não existir) e salva nela; o status termina com "Contexto: rolo … ·
     trabalho …" e o log do dia tem a linha `OutputSave`.
   - Desmarcar "Salvar na subpasta do rolo": salva direto na pasta de saída, como antes.
   - "Limpar contexto": os dois botões novos e a opção ficam desabilitados.
8. **(dev.65)** Com um contexto carregado e nenhuma saída salva: "Exportar resultado para o
   Nexor…" desabilitado; o resumo diz "Nenhuma saída salva…".
   - Preparar Escudo e Bandeira: o resumo mostra "Saídas salvas nesta sessão: 2 (Escudo,
     Bandeira)"; o botão habilita.
   - Exportar: o "Salvar como" sugere `px-resultado-<rolo>-<jobHash>.json` na pasta das
     saídas; o arquivo tem os dois caminhos reais dos .cdr e nenhum status. O Nexor não muda.
   - "Limpar contexto": a confirmação avisa as 2 saídas registradas.
   - Favoritos: adicionar "Alternar NO" e "Remover NO da página" — o de remover aparece em
     vermelho, com moldura.
9. **(dev.67) Recentes.** Numa instalação sem histórico, "Recentes…" fica desabilitado.
   - Carregue dois contextos diferentes (um pelo seletor, outro por "Colar caminho"):
     "Recentes…" abre um menu junto ao mouse com os dois, o mais recente primeiro, nome do
     arquivo, pasta e horário; o carregado agora aparece "(carregado)".
   - Escolha o outro: ele é carregado (resumo muda) e sobe para o topo. Feche e reabra o
     CorelDRAW: a lista continua, mas nenhum contexto é carregado sozinho.
   - Renomeie/mova um dos `.json` e escolha-o: o contexto anterior continua carregado e aparece
     a pergunta para tirar o item da lista ("Não" mantém; "Sim" tira só da lista).
   - Exporte de novo do Nexor sobre o mesmo arquivo e escolha-o: vale o conteúdo novo.
   - "Limpar lista de recentes": a lista some; os arquivos `.json` continuam onde estavam.
   - Adicione "Contextos de produção recentes" aos Favoritos: o atalho abre o mesmo menu, com a
     seção Produção recolhida.

### 4i. Camadas → ocultar camadas dos selecionados / mostrar últimas ocultadas

**Validação real recebida (parcial):** em `0.1.0-dev.48`, CorelDRAW 27.1.0.129, o usuário
executou o passo 0 (diagnóstico de identidade) **sem falhas**. Nessa execução passaram a
ocultação/restauração pelo serviço real, a identidade após renomear/reordenar e o descarte do
registro ao fechar/reabrir o documento. Isso **não** equivale à aprovação do roteiro 4i: os
passos 1–16 continuam pendentes, inclusive o 16 (fechar e reabrir o docker). O resultado vale
para aquela máquina/versão/sessão, não como garantia da API.

**Pendente — os passos 1–16 abaixo não foram executados no CorelDRAW real.** Teste direto na versão
corrigida mais recente (`0.1.0-dev.48` ou posterior): ela contém o ocultar (dev.45), o mostrar
(dev.46), o registro só em memória (dev.47) e o diagnóstico de identidade (dev.48). Não é
preciso instalar a dev.45/46 — o comportamento delas foi substituído (a dev.46 ainda gravava
marcação no documento; não a instale). Até agora só há testes automatizados com fakes (ver
`LayerVisibilityServiceTests`), que não comprovam o comportamento da API no Corel.

O ponto central a confirmar é a identidade de sessão (`docs/ARCHITECTURE.md`, 32.2–32.3): se o
Corel não devolver o mesmo objeto para a mesma camada/documento, o passo 2 já mostra
"Restauração indisponível" — anote isso como resultado (o recurso fica indisponível, mas seguro).

0. **Diagnóstico de identidade (dentro do add-on).** Em **Diagnóstico → "Testar identidade
   de camadas…"** (até a dev.48: "Diagnóstico de identidade de camadas…"): leia o aviso (cria
   um documento temporário, renomeia/reordena/
   exclui/desfaz camadas nele, salva em `%TEMP%`, fecha, reabre, fecha e apaga o arquivo;
   não toca seus documentos nem grava `Properties`) e confirme com "Sim". Ao terminar (dev.52 ou
   posterior), o painel mostra só um resumo ("Diagnóstico concluído: nenhuma falha" ou "N
   falhas"); confirme que ele bate com a linha "Resultado:" do relatório. "Ver relatório" abre o
   relatório completo numa área de altura limitada com rolagem própria (o painel não se estica
   com ele) e "Ocultar relatório" fecha. Clique "Copiar relatório" (até a dev.51: "Copiar teste
   de identidade") e guarde o texto junto do resultado deste roteiro. Confirme também: nenhum
   documento a mais aberto, o documento que estava ativo voltou a ser o ativo, e "Arquivo
   temporário apagado: sim". Cada `[FALHOU]` ou `[ERRO]` é um resultado a registrar, não um
   motivo para repetir até passar; `[OBS]` registra comportamentos que o serviço já trata nos
   dois sentidos. Um relatório sem falhas vale para esta máquina/versão/sessão — não é
   garantia universal da API. Com "Não", confirme "Diagnóstico cancelado — nada foi feito."
1. Abra o PX com um documento: confirme "Mostrar últimas camadas ocultadas" **desabilitado**
   e que a dica (passando o mouse) explica que não há ocultação registrada.
2. Selecione objetos em duas camadas (vários objetos na mesma camada inclusive) e oculte —
   confirme que só essas duas camadas ficam ocultas, citadas uma vez cada no status, SEM a
   frase "Restauração indisponível"; o botão de mostrar fica habilitado e a dica lista as
   camadas e o documento.
3. Ctrl+Z uma vez — confirme que as duas voltam juntas (um passo). Refaça a ocultação.
4. Oculte uma terceira camada à mão (Gerenciador de Objetos). Clique "Mostrar últimas" —
   confirme que só as duas do passo 2 voltam e a terceira continua oculta; o botão volta a
   ficar desabilitado. Ctrl+Z desfaz a restauração num passo só.
5. Selecione objeto em camada já oculta (pelo Gerenciador de Objetos) junto com um em camada
   visível — confirme "Já oculta(s), não registrada(s)" e que, ao mostrar, a já oculta continua
   oculta.
6. Depois de uma ocultação, clique "Ocultar" sem seleção — confirme que o botão de mostrar
   continua habilitado e ainda restaura a ocultação anterior.
7. Oculte, reordene as camadas/páginas e renomeie uma das camadas ocultadas — confirme que
   "Mostrar últimas" ainda mostra as camadas certas.
8. Oculte, exclua uma das camadas ocultadas, clique "Mostrar últimas" — confirme que a outra
   volta e a excluída aparece como "Não encontrada(s)", sem erro.
9. Oculte duas camadas, exclua uma e desfaça só a exclusão (Ctrl+Z) — anote se ela volta a ser
   encontrada ou aparece como "Não encontrada(s)" (ambos seguros; mostra se o Corel recria o
   objeto no Desfazer).
10. Oculte no documento A, ative o documento B, clique "Mostrar últimas" — confirme a recusa
    ("Ative o documento de origem") sem nenhuma mudança em B; volte para A e restaure.
11. Oculte no documento A e feche A sem salvar — confirme que o botão fica desabilitado logo
    após fechar, com dica dizendo que o documento de origem foi fechado; nada alterado em B.
12. Oculte no documento A, SALVE, feche e reabra o mesmo arquivo — confirme o botão
    desabilitado e que "Mostrar últimas" não age no arquivo reaberto (camadas continuam
    ocultas como foram salvas).
13. Ainda com o arquivo do passo 12: confirme que nenhuma propriedade `PXToolBox.LastHiddenLayers`
    aparece no documento nem nas camadas (por exemplo, por macro VBA consultando
    `ActiveDocument.Properties.Exists("PXToolBox.LastHiddenLayers", 1)` e o mesmo em cada
    `Layer.Properties`).
14. Selecione um objeto na camada de Guias junto com outro comum — confirme que Guias nunca é
    ocultada nem mostrada.
15. Teste com uma camada MESTRA (camada em todas as páginas): oculte e mostre — confirme que não
    aparece "Restauração indisponível" nem "invalidada" por ela ser listada duas vezes.
16. Oculte camadas (botão de mostrar habilitado), feche o docker do PX e reabra-o — confirme
    o botão desabilitado com a dica "o painel foi fechado depois da última ocultação" e que as
    camadas continuam ocultas (Ctrl+Z ainda as mostra). Isto confirma o descarte do registro ao
    encerrar o painel, que o diagnóstico do passo 0 não simula.

### 4j. Favoritos do painel (dev.51; atalhos em ícone desde a dev.53)

**Pendente — nada abaixo foi executado no CorelDRAW real.** (Uso real parcial: na dev.50 o
usuário salvou um favorito, o que mostra a gravação funcionando, mas não substitui este roteiro.)
Já verificado fora do Corel: testes automatizados (lista, catálogo, persistência, configuração
antiga/corrompida, Ids desconhecidos, exportação/importação, atalho disparando o clique do
original, dica com o nome) e renderização do painel compilado (`docs/ARCHITECTURE.md`, 35 e
35.1). As pendências anteriores (4i — inclusive o passo 0, Diagnóstico — e 4e passo 6, Tamanho Padrão)
continuam valendo, independentes
desta seção.

1. Instale a dev.53 (ou posterior) com a configuração que você já tem: favoritos já salvos
   aparecem como ícones; sem favoritos, "Favoritos" mostra a orientação "Clique com o botão
   direito…". Perfis e demais configurações do ListForge Integrado continuam iguais.
2. Botão direito em "Preparar Grid" → "Adicionar aos favoritos": aparece o ícone de grade. Repita
   com "Alternar Tamanho Padrão" e "Mostrar últimas camadas ocultadas" (desabilitado: o menu
   precisa abrir mesmo assim). Teste também pelo teclado (Tab até o botão, Shift+F10).
3. Pare o mouse sobre cada ícone: a dica mostra o nome da ferramenta (negrito) e a dica do botão
   original. Os ícones aparecem (não quadrados vazios) — dependem das fontes de ícones do
   Windows. **(dev.57)** A dica tem fundo escuro e texto claro, legível, e quebra
   linha; as dicas dos botões originais também.
4. Botão direito num ícone: "Mover para a esquerda"/"Mover para a direita" mudam a ordem (o
   primeiro não move para a esquerda, o último não move para a direita); "Remover dos favoritos"
   remove só o atalho (o original continua na seção). No menu do botão original, o texto vira
   "Remover dos favoritos" para os que já são favoritos.
5. Clique cada ícone e confirme a mesma mensagem e o mesmo efeito do botão original (um passo
   de Ctrl+Z, quando o original faz um).
6. Com objetos que tenham Tamanho Padrão selecionados: ícone e original ficam azuis juntos;
   misto → só a borda; sem seleção → comum.
7. "Mostrar últimas camadas ocultadas": ícone desabilitado/habilitado junto com o original e
   com a mesma dica.
8. Feche e reabra o CorelDRAW: favoritos e ordem continuam.
9. Com o ListForge Integrado aberto, mude favoritos no painel e depois salve algo no ListForge
   (ex.: Configurações → Salvar): os favoritos não podem voltar ao estado antigo.
10. Configurações → "Exportar preferências…": o resumo/arquivo lista os favoritos atuais.
    Remova favoritos, "Importar preferências…" desse arquivo → resumo mostra os favoritos →
    confirmar → Salvar: o painel volta a mostrá-los na mesma ordem, sem reabrir.
11. Confirme que nenhum documento do Corel ficou modificado só por adicionar/remover/ordenar
    favoritos.
12. **(dev.56) Todos os botões.** Botão direito em botões de outras seções — Agrupar Interseção,
    Preparar Escudo, Somar Altura, Abrir ListForge Integrado, Abrir Nexor, Inspecionar
    propriedades, os NO/NU/AP/TS de alternar e de remover — oferece "Adicionar aos favoritos".
    Cada ícone tem dica com o nome e faz o mesmo que o original; Escudo/Bandeira/Faixa pelo
    atalho usam o layout e a pasta preenchidos na seção. Favoritos salvos antes da dev.56
    continuam aparecendo.
13. **(dev.66) Arrastar e soltar.** Com pelo menos 4 favoritos (de preferência quebrando em duas
    linhas): arraste um ícone com o botão esquerdo — ele fica esmaecido e uma barra âmbar mostra
    onde vai entrar; solte e a ordem muda (e continua depois de fechar e reabrir o CorelDRAW).
    Arraste da segunda linha para o início da primeira e do primeiro para o fim. Soltar no próprio
    lugar não muda nada; soltar fora dos Favoritos (no documento, em outra seção) cancela e não
    executa nada no documento. Um clique curto continua executando a ferramenta (sem mover). Um
    atalho desabilitado ("Mostrar últimas camadas ocultadas") também pode ser arrastado. O menu
    do botão direito (mover/remover) continua igual.
14. **(dev.72) Ícones.** Nenhum ícone se repete nos Favoritos; Mão Livre é uma mão; a letra
    laranja no canto é a seção (G, Co, Ca, PS, T, O, M, L, Pr, D) e não encosta no símbolo nem
    nas siglas (NO, MA…) a 100% e 150%. Remoção continua vermelha com "−".
15. **(dev.73) Seções e "Mostrar nomes".** Seções na ordem nova (Montagem logo depois do Grid,
    com as cinco ferramentas; Objetos, Tipo de peça e Medidas com nome curto). Com a ordem já
    personalizada antes, ela continua e a Montagem aparece depois do Grid. Nenhum texto do painel
    cita "SISBolt". Nos Favoritos, "Mostrar nomes" ligado: um botão por linha com ícone e nome,
    estado ativo/misto igual; arraste para cima/baixo com barra horizontal. Desligado: ícones.
    Fechar e reabrir o Corel mantém o modo.

### 4k. Personalizar painel (dev.53)

**Pendente — nada abaixo foi executado no CorelDRAW real.** Já verificado fora do Corel: 20
testes automatizados e o painel compilado renderizado e exercitado com configuração isolada
(`docs/ARCHITECTURE.md`, 36).

0. (Próxima build DEV depois da dev.53) Com o docker estreito e Camadas aberta, a legenda
   "Alternar visibilidade de todas as camadas MA, FE ou C da página ativa:" quebra linha sem
   cortar, e MA/FE/C continuam lado a lado, alinhados.
1. Com a dev.53 instalada, o cabeçalho mostra "Personalizar painel…" ao lado de "PX Tool Box
   DEV", mesmo sem documento aberto. Num docker estreito, o botão não pode cortar nem empurrar
   o título para fora.
2. Antes de mexer: preencha "Nome do layout"/"Pasta de saída" em Preparação e saída, deixe
   Camadas aberta e, se der, oculte camadas com "Ocultar camadas dos objetos selecionados".
3. "Personalizar painel…": a lista mostra as 11 seções na ordem atual, todas marcadas. Mova
   Diagnóstico para o topo, desmarque Favoritos e Códigos, e clique **Cancelar**: o painel não
   muda.
4. Repita e clique **Salvar**: Diagnóstico no topo, Favoritos e Códigos somem; o painel não pisca
   nem fecha; os campos preenchidos, Camadas aberta e "Mostrar últimas camadas ocultadas"
   habilitado continuam iguais.
5. Com uma ferramenta favorita cuja seção está oculta (ex.: oculte Grid e reexiba Favoritos), o
   ícone continua funcionando igual ao original.
6. Exiba Favoritos de novo: os mesmos atalhos, na mesma ordem.
7. Feche e reabra o docker e depois o CorelDRAW: o layout continua.
8. "Restaurar layout padrão" → Salvar: ordem original, tudo visível; favoritos e ordem deles
   intactos.
9. Configurações do ListForge Integrado → "Exportar preferências…": o resumo inclui "Layout do
   painel". Mude o layout, importe esse arquivo → Salvar: o painel volta ao layout exportado sem
   reabrir. Importe um arquivo exportado antes da dev.53 (sem layout): o layout atual não muda.
10. Teclado: Tab/Espaço marcam e desmarcam; ▲/▼ mantêm o foco no botão depois de mover; Esc
    cancela.
11. Confirme que nenhum documento do Corel ficou modificado só por personalizar o painel.

12. **(dev.76) Nomes, seções criadas e lugar das ferramentas.** Em "Personalizar painel…":
   renomeie "Objetos" e salve — o título muda no painel e a letra dos favoritos acompanha.
   "+ Nova seção", nomeie, mova para perto do topo; em Ferramentas, mande "Alternar Mão Livre" e
   "Preparar Escudo" para ela. Salvar: a seção aparece com os dois botões; na origem eles somem;
   os botões funcionam (Escudo usa o nome do layout e a pasta de Preparação e saída). Feche e
   reabra o Corel: tudo continua. ✕ na seção criada → as ferramentas voltam. "Restaurar padrão"
   + Salvar → painel original. Nos Favoritos, a chave "Mostrar nomes" liga e desliga.
   **(dev.77)** Mova "Alternar" e "Atualizar Tamanho Padrão" para outra seção: no Grid não sobra
   o subtítulo "Tamanho Padrão" nem linha dupla. A janela "Personalizar painel" fica legível
   (nomes claros, movidas em azul).

### 4l. Remover códigos da página — por código ou todos (dev.55)

**Pendente — nada abaixo foi executado no CorelDRAW real.** Já verificado fora do Corel: testes
automatizados do serviço e o painel compilado carregado (`docs/ARCHITECTURE.md`, 37).

Use um documento de teste com objetos chamados NO, NU, AP e TS — soltos, dentro de grupo e dentro
de PowerClip, em mais de uma camada da página ativa — e um objeto com outro nome qualquer.

1. Seção Códigos aberta num docker estreito: a legenda "Alternar na seleção atual (…)" e
   "Remover da página ativa, um código por vez:" quebram linha sem cortar; NO/NU/AP/TS ficam lado a
   lado; "Remover todos os códigos (NO/NU/AP/TS) da página" quebra o texto sem cortar.
2. Clique o NO de remoção (não o de alternar): só os objetos NO viram "TextoNome", inclusive em
   grupo e PowerClip; NU/AP/TS não mudam. O status diz quantos objetos mudaram. Ctrl+Z desfaz em
   um passo.
3. Repita com NU ("TextoNumero"), AP ("Texto Apelido") e TS ("Texto Tipo Sanguíneo").
4. Desfaça tudo e clique "Remover todos os códigos…": os quatro mudam de uma vez; o objeto com
   outro nome não muda; um Ctrl+Z desfaz tudo.
5. "Remover NO/NU da página" continua igual a antes (só NO e NU).
6. Sem documento aberto: mensagem de status, nada muda. Página sem códigos: "… 0 objeto(s)".

### 4m. Notas da versão (dev.59)

**Pendente — este roteiro ainda não foi executado no CorelDRAW real.**

1. Com a dev.60 (ou posterior) instalada, confira a versão do rodapé e clique **Notas da versão**,
   no início da seção Diagnóstico (na dev.59 ficava no rodapé): a janela
   mostra o mesmo número e as seções Implementado, Alterado e Removido.
2. Confira as novidades do botão/janela em Implementado e a indicação de nenhum registro nas
   categorias vazias. Não devem aparecer novidades históricas de outras versões.
3. Redimensione a janela até a largura mínima: texto quebra linha e, quando necessário, há
   rolagem vertical; o botão Fechar continua acessível.
4. Feche pelo botão, reabra e feche por Esc. Repita sem documento aberto e sem acesso à internet.
5. O documento, as seleções e o estado das ferramentas permanecem iguais após a consulta.
6. Em build local (`0.0.0-dev.local`), confirme o aviso de build local sem notas de outra versão.
7. (dev.60) Botão direito em **Notas da versão** → "Adicionar aos favoritos": o ícone abre a mesma
   janela, inclusive com Diagnóstico oculto em "Personalizar painel…".

### 4n. Pasta de saída padrão e contraste da comparação (dev.58)

**Pendente — nada abaixo foi executado no CorelDRAW real.**

1. Preparação e saída com o campo "Pasta de saída" vazio: "Destino" mostra
   `Z:\Neuber\Saídas (padrão original)` e "Voltar ao padrão original" está desabilitado.
2. "Definir pasta padrão…" → escolha uma pasta de teste: "Destino" passa a dizer "padrão desta
   máquina". Preparar Escudo salva nela. Feche e reabra o CorelDRAW: continua valendo.
3. Digite outra pasta no campo: "Destino" diz "digitada acima" e ela vence o padrão; apague o
   campo e o padrão da máquina volta.
4. "Voltar ao padrão original": volta a `Z:\Neuber\Saídas`. Exportar preferências não inclui a
   pasta (é dado da máquina).
5. ListForge Integrado → comparação entrada/saída: a caixa de filtro fechada e a lista aberta têm
   fundo escuro e texto legível; item sob o mouse e selecionado diferenciados.

### 4o. Aplicar contorno do Grid (dev.61)

**Pendente — nada abaixo foi executado no CorelDRAW real.** Use uma cópia de um documento de Grid.

1. Num documento com 2+ páginas, objetos soltos e um grupo, e guias na página: clique
   **Aplicar contorno do Grid** (seção Grid). Todos os objetos de todas as páginas ficam com
   preenchimento branco e contorno preto fino — o mesmo resultado do passo equivalente do
   Preparar Grid Novo. O status diz quantos objetos mudaram.
2. As guias não mudam de cor/estilo.
3. Um Ctrl+Z desfaz tudo de uma vez.
4. Com Grid oculta em "Personalizar painel…", o atalho dos Favoritos faz o mesmo.

### 4p. Comparação antes/depois (dev.68)

**Pendente — não executado no CorelDRAW.**

1. Documento de teste com um retângulo e um grupo de dois objetos. Diagnóstico → "Comparar com
   a captura" começa desabilitado; "Capturar estado (antes)" habilita e mostra quantos objetos.
2. Sem mudar nada, "Comparar…": "Nenhuma diferença encontrada".
3. Mova e gire o retângulo, renomeie-o e marque Tamanho Padrão (Grid): capture antes, faça as
   mudanças, compare — o relatório mostra posição, rotação, nome e `KEEP_DESIGN_SIZE` [0], [1],
   [2] novos.
4. Agrupe objetos / desagrupe: objeto NOVO/REMOVIDO e "Dentro de" mudando.
5. Confirme que capturar/comparar não cria passo de Desfazer nem marca o documento como
   alterado. Copiar e "Salvar como .txt" funcionam; o campo "Ferramenta testada" aceita
   digitação dentro do Corel.
6. Depois: o roteiro de `docs/sisbolt-tools-investigation.md`, uma ferramenta do SISBolt por vez.
7. **(dev.69) Unidades.** O relatório mostra "Unidade da régua" e "Unidade efetiva da
   geometria (Document.Unit)" de cada captura, e o tamanho da página convertido — confira que
   bate com o tamanho da página no Corel. Para confirmar que Position*/Size* seguem
   `Document.Unit`, num documento de teste com um retângulo selecionado, na janela Imediata do
   editor de macros (Alt+F11, Ctrl+G):
   `? ActiveDocument.Unit, ActiveSelectionRange(1).SizeWidth` → anote;
   `ActiveDocument.Unit = cdrCentimeter` e repita `? ActiveSelectionRange(1).SizeWidth` (deve
   dar ×2,54). Com o PX: capture antes, rode a troca de unidade, compare — o retângulo **não**
   pode aparecer como movido nem redimensionado; só "Documento.Unit: cdrInch → cdrCentimeter".
   Volte com `ActiveDocument.Unit = cdrInch`. Repita o Tamanho Padrão do SISBolt: sem
   "Largura"/"Altura" e com as três entradas `KEEP_DESIGN_SIZE`.

6. **(dev.79)** Diagnóstico → "Roteiro de capturas…": a lista mostra os itens com ○; escolha
   um, siga os 4 passos e, na comparação, confira que "Item do roteiro" já vem com ele →
   "Salvar na pasta de capturas" → o item ganha ✓ no roteiro (aberto ao lado) e o próximo
   pendente fica selecionado. "Abrir pasta das capturas" mostra o arquivo `<item>__data.txt`.

### 4q. Ferramentas do SISBolt (dev.70)

**Pendente — não executado no CorelDRAW.** Para cada ferramenta, registre SEPARADAMENTE:
(A) as propriedades coincidem com as do SISBolt; (B) o SISBolt reconhece na montagem.

1. **Esticar no Tamanho do Molde** — dois retângulos simples iguais: um marcado por "Ativar
   Esticar no Tamanho do Molde" (PX), outro pela ferramenta do SISBolt. (A) Inspecionar
   propriedades: `STRETCH_PROPS` [0] Boolean True nos dois; um com Tamanho Padrão continua com
   as três entradas `KEEP_DESIGN_SIZE`; um Ctrl+Z desfaz. (B) Mesma montagem no SISBolt com os
   dois: o resultado coincide?
2. **Ignorar métricas da variável** — dois textos NO equivalentes, um por ferramenta. (A)
   `IGNORE_SISBOLT_VARIABLE_METRICS` [0] Boolean True; texto, nome e geometria iguais. (B) O
   SISBolt aplica o mesmo tratamento de métricas aos dois?
3. **Mão Livre** — idem com `FREE_HAND_PROPS` [0]. (B) resultado da montagem coincide?
4. **Criar grupo com item único** — capture antes/depois com o PX e com o SISBolt: grupo na
   mesma camada, um filho (o original, mesmo StaticID), grupo na frente e selecionado, nome
   igual, sem Properties; um Ctrl+Z desfaz. Repita com um objeto de outro nome no SISBolt
   (o nome do grupo se repete?) e veja se `ObjectData["Name"]` do grupo fica preenchido. (B)
   O SISBolt reconhece o grupo do PX no fluxo que o usa?
5. **Alinhamento horizontal automático** — dois objetos: a janela mostra os papéis; o olho dá
   zoom na referência; "Trocar papéis" inverte; Salvar grava o mesmo GUID (com hífens) em
   `GUID_IDENTIFIER_PROPS` [0] da referência e `ALIGNMENT_PROPS` [5] do objeto, sem mexer em
   [0]–[4]; um Ctrl+Z desfaz as duas escritas. Referência que já tem GUID: aviso e sem o botão Salvar.
   (B) Montagem com um par configurado pelo PX e outro pelo SISBolt: a centralização horizontal
   coincide?
6. Seleção errada (nenhum objeto; algo que não é texto em Ignorar métricas/Mão Livre) não
   altera nada.
7. **(dev.72; nomes "Alternar …" desde a dev.74) Estado no botão**, igual ao Tamanho Padrão, para
   Alternar Esticar no Tamanho do Molde, Alternar Ignorar métricas da variável e Alternar Mão
   Livre: selecione um objeto marcado → botão destacado; sem marcação → comum; dois objetos, só
   um marcado → só a borda (misto). Clicar com tudo marcado desliga (a entrada some no
   inspetor); misto ou desligado → liga em todos. O atalho dos Favoritos acompanha o estado.
   Desligue no SISBolt um objeto marcado e capture: ele remove a entrada (como o PX) ou grava
   False?
8. **(dev.75)** Alternar Mão Livre: liga em textos NO, NU, AP e TS; com um texto "Texto Nome" (ou
   qualquer outro nome) na seleção, avisa e não altera nada. Um texto NO marcado e renomeado
   depois ainda desliga.
9. **(dev.80) Resetar ângulo** — (a) curva girada 30°: fica em 0°, mesma aparência, posição e
   tamanho; (b) retângulo girado com Tamanho Padrão: vira curva, mesma aparência, marcação
   continua (inspetor); (c) grupo girado com 2–3 curvas: mesmo nome, mesmos filhos na mesma
   ordem, mesmo lugar na camada; (d) texto girado: não muda e o status avisa; (e) Ctrl+Z
   desfaz tudo de uma vez; (f) a montagem reconhece o objeto resetado pelo PX como o resetado
   pela ferramenta original?

### 4r. Padrão antigo e padrão novo (dev.81)

1. Molde com camadas CAM1.Frente.MA (grupo G com Tag, peça, x): Preparação e saída → padrão novo
   → Preparar Molde Novo → camadas CAM1.MOLDE.MA.Frente; objetos Tag, Frente, x.
2. "Converter documento" no molde → CAM1.Frente.MA.Frente; de novo → CAM1.MOLDE.MA.Frente.
   Status diz para qual padrão foi. Ctrl+Z desfaz de uma vez.
3. Grid antigo (CAM1.Frente.MA, CAM1.Costa.MA, CAM1.Frente.FE): Converter → CAM1.MOLDE.MA e
   CAM1.MOLDE.FE, com Frente_RECT/Costa_RECT do tamanho e no lugar exato de cada peça, no fundo.
4. Converter de novo → volta às camadas por peça, sem os _RECT; logos L1… na camada da peça
   em que estão.
5. Padrão novo → Preparar Grid Novo, a partir de um molde (antigo e novo): termina como no item 3.
6. Os botões do padrão antigo fazem exatamente o que faziam antes.
7. A montagem reconhece molde e Grid no padrão novo gerados pelo PX?

### 4s. Cadastro de moldes — fase 1 (dev.82)

1. Criar camadas para a seleção: selecione grupos G, M, BLG de duas peças (Tag, peça, x) → cada
   grupo vai para CAM1.MOLDE.MA.Peça / CAM1.MOLDE.FE.Peça; tamanho fora da lista, grupo com
   duas "peças" ou nome com espaço → não move e o status explica.
2. Maiúsculas: peça e camada .MOLDE. em maiúsculas; o x continua x.
3. Excluir camadas vazias: só as vazias da página ativa saem.
4. Distribuir: vertical e horizontal, maior/menor primeiro, com e sem distanciamento (mm).
   Conferir as distâncias reais na régua.
5. Criar Grid…: escolher o arquivo → o Grid abre com uma camada por categoria, peça, logos e
   PEÇA_RECT do tamanho e no lugar da peça, no fundo; o documento de moldes não mudou (nenhuma
   camada CAM1.MOLDE.MA sobrando, grupos intactos).
6. Selecionar RECT no Grid aberto seleciona todos os _RECT.
7. Ctrl+Z desfaz cada ferramenta de uma vez.
8. **(dev.83) Configurações de moldes…**: digitar nos campos dentro do Corel funciona; a prévia
   acompanha; com um campo obrigatório vazio, Salvar mostra o problema e não fecha. Mudar o sufixo
   infantil (ex.: ANOS) e salvar → Criar camadas aceita um grupo "8ANOS"; Restaurar padrão + Salvar
   volta ao de fábrica.

### 4t. Definir objetos (dev.84)

1. Desenhar uma peça (curva) e dois logos dentro dela; selecionar os três; Nome da peça "Frente",
   Tamanho G → Definir objetos. Esperado: grupo "G" selecionado com TAG, L1, L2, Frente, x (nessa
   ordem na janela de objetos); x com contorno vermelho de 0,2 mm sobre a peça; TAG "G" preta com
   contorno branco, embaixo e no centro da peça.
2. Conferir no Corel: a espessura do contorno do x e da TAG está em mm certos com o documento em
   outra unidade (cm ou polegadas).
3. Repetir com um tamanho M da mesma peça (logos na mesma posição relativa): os logos ganham L1 e L2
   iguais aos do G; a mensagem cita a numeração do grupo "G".
4. Um logo sem par no outro tamanho ganha o próximo número livre (L3).
5. Selecionar o grupo G e Definir de novo (ex.: Tamanho GG): vira "GG", TAG nova "GG", x recriado,
   logos com os mesmos nomes. Sem erro do Corel.
6. Dois objetos do mesmo tamanho selecionados → mensagem de ambiguidade, nada muda.
7. Configurações de moldes: desligar a cópia do laser e a TAG → Definir põe o contorno na própria
   peça e não cria TAG. Fonte "Arial Black" ausente → TAG em Arial.
8. Ctrl+Z desfaz o Definir objetos de uma vez.

### 4u. Detectar objetos em lote (dev.85)

1. Infantil com 6 tamanhos (2A…12A): desenhar 6 peças de tamanhos crescentes, cada uma com os mesmos
   logos na mesma posição; selecionar tudo; Nome da peça "Frente", categoria Infantil → Detectar.
   Esperado: 6 grupos 2A…12A (o menor é 2A), cada um com TAG, L1…, Frente, x; mesmos L em todos.
2. Masculino com só P, M e G: definir antes o G com Definir objetos; selecionar o grupo G e os
   conjuntos soltos de P e M → Detectar. Esperado: P e M certos pela área; logos iguais aos do G.
3. Sem nenhum grupo e faltando tamanhos → mensagem pedindo um conjunto por tamanho; nada muda.
4. Logo encostado/fora da peça: conferir a mensagem (conjunto a mais) e que nada mudou.
5. Ctrl+Z desfaz o lote inteiro de uma vez.

### 4v. Propagar logos (dev.86)

1. Montar G (com 2–3 logos) e P, M, GG da mesma peça sem logos (Definir objetos ou em lote).
   Selecionar os 4 grupos → Propagar logos. Esperado: P, M e GG ganham L1… na mesma posição relativa,
   menores/maiores na proporção da altura da peça; dentro do grupo, atrás da TAG e na frente da peça;
   o G não muda; os 4 grupos ficam selecionados.
2. Mover um logo do G, selecionar de novo os 4 (Tamanho = G) → Propagar. Esperado: só ajusta
   (0 novos); com Tamanho = um que não está na seleção, mensagem pedindo para escolher.
3. Selecionar G e um grupo de outra peça, ou um objeto solto → mensagem; nada muda.
4. Ctrl+Z desfaz a propagação inteira de uma vez.

### 4w. Duplicar peça (dev.87)

1. Com os grupos P, M, G de "Manga" (com logos), selecionar os 3; nome da cópia "Manga2", À esquerda,
   10 mm, Espelhar ligado → Duplicar peça. Esperado: 3 grupos novos P, M, G à esquerda, com a peça
   "Manga2" a 10 mm da original (régua), peça e x espelhados e coincidentes, TAG legível (não
   espelhada), logos com números novos (mesmos em todos os tamanhos); as cópias ficam selecionadas.
2. Repetir com Abaixo e 0 mm → as cópias anteriores somem e as novas ficam embaixo, encostadas, com os
   mesmos números de logo.
3. Nome igual ao da peça, vazio, com espaço, ou objeto solto na seleção → mensagem; nada muda.
4. Ctrl+Z desfaz a duplicação inteira de uma vez.

### 4x. Salvar molde (dev.88)

1. Documento de moldes já salvo: "Nome do molde" = "Camisa Teste" → Salvar molde…. Esperado: a janela
   abre na pasta do documento com "Camisa Teste.cdr"; salvar → o título do Corel passa a ser
   "Camisa Teste.cdr" e o arquivo abre normalmente depois (Arquivo → Abrir).
2. Documento novo (nunca salvo): a janela abre na pasta de moldes (se existir).
3. Nome vazio ou com "/" → mensagem; nenhuma janela abre. Cancelar a janela → "cancelado — nada foi
   salvo".

### 4y. Configurações de moldes nas preferências (dev.89)

1. Configurações de moldes… → mudar algo visível (ex.: nome da TAG "ETIQUETA") → Salvar.
2. ListForge integrado → Configurações → Exportar preferências… → o resumo/arquivo traz a linha
   "Configurações de moldes".
3. Voltar a TAG para "TAG" nas Configurações de moldes; depois Importar preferências… com o arquivo
   do passo 2 → a confirmação lista "Configurações de moldes" → Sim → Salvar. Abrir Configurações de
   moldes: TAG = "ETIQUETA".
4. Importar um arquivo antigo (sem "moldSettings") → as configurações de moldes não mudam.

### 4z. Posição e margens da TAG (dev.90)

1. Sem mexer nas configurações: Definir objetos → TAG embaixo, no centro, encostada na base da peça
   (igual à dev.84).
2. Configurações de moldes → Posição "Superior direita", margens direita 1 e superior 2 → Salvar.
   Definir objetos de novo (redefinir o grupo) → TAG dentro da peça, com a borda direita a 1 cm da
   direita da peça e o topo a 2 cm do topo (régua; documento em mm e depois em cm).
3. "Centro" com esquerda 2 → TAG no meio, 2 cm à direita do centro.
4. Margem -1 → a janela não salva.

### 4aa. Contorno interno por cor de tamanho (dev.91)

1. Configurações de moldes → ligar "Contorno interno na peça", 0,5 mm, estilos padrão → Salvar.
2. Definir objetos num G masculino e num BLG feminino → a peça de cada um ganha um contorno por
   dentro (Propriedades do objeto → contorno "Dentro"), cor da amostra do tamanho; masculino
   contínuo, feminino tracejado. O "x" continua vermelho 0,2 mm contínuo, centralizado.
3. Cor digitada "G #00FF00" → redefinir o G → contorno verde.
4. Desligar "Criar a cópia do laser" com o contorno interno ligado → a janela não salva.
5. Ctrl+Z desfaz o Definir objetos inteiro.

### 4ab. Manual de uso (dev.92)

1. Diagnóstico → "Manual de uso" → a janela abre em "Começando"; clicar de novo traz a mesma janela.
2. Escolher "Cadastro de moldes" → um cartão por ferramenta, na ordem do painel, com "Como usar" e
   "Observações".
3. Digitar "laser" em Filtrar (com o Corel atrás) → as letras entram no campo, não no Corel; a lista
   fica só com o que tem "laser" (Cadastro de moldes com a contagem); clicar nela mostra só essas
   ferramentas. ✕ volta à seção de antes, com tudo.
4. Botão direito em "Manual de uso" → Adicionar aos favoritos → o atalho abre a janela.

### 4ac. Validar molde (dev.93)

Num documento de moldes montado pelo PX (ex.: `TESTE MOLDES.cdr`, com uma cópia de trabalho):

1. Cadastro de moldes → "Validar molde" sem selecionar nada → abre a janela; status com o resumo
   ("N grupo(s) de tamanho de N peça(s) … Nada foi alterado."). Nada muda no desenho.
2. Num tamanho, renomear um logo para o número de outro (ex.: L1 → L2 só no M) → "Validar de novo" →
   ERRO de número trocado no M. Desfazer.
3. Mover um logo de um tamanho para longe → AVISO "não está no mesmo lugar"; apagar um logo → AVISO
   "falta"; "Propagar logos" nesses grupos → "Validar de novo" → os avisos somem.
4. Duplicar peça e conferir que a cópia não acusa "mesmo número em peças diferentes".
5. "Copiar" → colar no Bloco de Notas → o mesmo texto da janela.
6. Página sem grupos de tamanho → "Nenhum grupo de tamanho…".

### 5. Registrar o resultado

Depois de rodar este roteiro, atualize esta seção (ou abra uma issue) com:
- O que funcionou exatamente como esperado.
- O que precisou de ajuste (principalmente: como a instância de `Application` chegou ao
  `ControlUI` — ver seção 3 de `docs/ARCHITECTURE.md`).
- Qualquer mensagem de erro do painel (campo "Status" no rodapé).

**Não declare uma ferramenta como validada em produção só porque passou no teste automatizado
— só este roteiro manual, executado de fato no CorelDRAW, comprova isso.**
