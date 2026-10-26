# Ferramentas migradas — de onde vêm e o que mudou

Duas fontes, nesta ordem de autoridade:

1. **`Referência/Macros/` (revisão candidata)** — apareceu na raiz deste repositório durante o
   desenvolvimento desta entrega, com módulos reorganizados (`Grid`, `FluxoGrid`, `Montagem`,
   `Agrupamento`, `Medidas`, `Codigos`, `Molde`, `Correcoes`, `Utilidades`) e relatórios de
   mapeamento (`relatorios/*.md`). O próprio `LEIA-ME.md` da revisão é explícito: **"revisada
   estaticamente. Não foi compilada nem executada no Corel."** Onde esta revisão reescreve uma
   ferramenta (ex.: Grid) ou resolve uma lacuna anterior (Agrupar Interseção, alternância
   MA/FE/C), ela tem prioridade sobre a leitura anterior deste documento — mas herda a mesma
   ressalva: **não validada no Corel real.**
   Não está versionada neste repositório git (ver `.gitignore`) — é material de consulta.
2. **`CorelMacros` (repositório separado, `C:\Projetos\CorelMacros`)** — o código de produção
   original, sem a revisão. Usado onde a revisão candidata não altera nada.

| Ferramenta (prioridade do usuário) | Fonte | Serviço novo (`PXToolBox.Outputs.Tools`) | Observação |
|---|---|---|---|
| Adicionar/remover NO | `Codigos.CodigoNome` (= `Utilidades.CodigoNome` original, idêntica) | `CodeToggleService.Toggle(selection, ObjectCode.No)` | — |
| Adicionar/remover NU | `Codigos.CodigoNumero` | `CodeToggleService.Toggle(selection, ObjectCode.Nu)` | — |
| Adicionar/remover AP | `Codigos.CodigoApelido` | `CodeToggleService.Toggle(selection, ObjectCode.Ap)` | — |
| Adicionar/remover TS | `Codigos.CodigoTipoSanguineo` | `CodeToggleService.Toggle(selection, ObjectCode.Ts)` | — |
| *(entrada de barra preservada, achada no relatório)* | `Correcoes.RemoveCodigosNoNu` | `CodeToggleService.ExpandNoAndNuCodesRecursively` | Ferramenta DIFERENTE do toggle: expande "NO"/"NU" em TODA a página, recursivamente em grupos, para "TextoNome"/"TextoNumero" (sem espaço — convenção própria). Não trata AP/TS (a macro original também não trata) |
| Renomear Objetos Selecionados | `Utilidades.RenomearObjetosSelecionados` (inalterada) | `RenameSelectedObjectsService.Rename` | `InputBox` → caixa de texto no painel |
| Alterar Tipo de Peça (documento inteiro, por camada) | `Correcoes.RenomeiaTipoDePeca` (inalterada) | `PieceTypeRenameService.Rename` | Dois `InputBox` → dois `ComboBox`. **Não está mais ligado a nenhum botão do painel** (ver linhas abaixo) — mantido no serviço e nos testes, sem equivalente na UI atual |
| Alterar tipo dos objetos selecionados | Sem precedente na macro original (ação nova, pedida pelo usuário em 2026-10) | `PieceTypeRenameService.RenameSelectedObjects` | Reconhece o tipo pelo NOME de cada objeto selecionado, usando a mesma convenção pontuada de `LayerName` (primeiro segmento = tipo) — a mesma convenção que `Correcoes.RenomearObjetosComNomeDaCamada` já grava nos objetos, não uma convenção inventada. Objetos com nome em outro formato são ignorados e contados à parte |
| Alterar tipo da camada ativa | Sem precedente na macro original (ação nova, pedida pelo usuário como "camadas selecionadas") | `PieceTypeRenameService.RenameActiveLayer` | **Limitação confirmada por reflexão direta sobre `Corel.Interop.VGCore.dll`**: a API do CorelDRAW não expõe multi-seleção de camadas do Gerenciador de Objetos — só existe `Document.ActiveLayer` (singular). Por decisão do usuário, a ação atua só nesta camada; o botão e o tooltip deixam isso explícito ("camada ativa", não "camadas selecionadas") |
| Alternar visibilidade MA/FE/C | `Montagem.AlternarMA` / `AlternarFE` / `AlternarC` (revisão candidata — achada depois de a primeira busca não encontrar nada) | `LayerVisibilityService.Toggle` | **Correção deliberada**: a candidata compara só os 2 últimos caracteres do nome contra "MA"/"FE" sem exigir ponto antes (só "C" é comparado como ".C", com ponto) — isso deixaria "MA"/"FE" vulneráveis a casar uma camada como "FORMA" por acidente. Implementado aqui com o último segmento pontuado inteiro para as três categorias, igualmente |
| Preparar Grid | `Grid.PreparaGrid` (revisão candidata — **reescrita**, não a versão antiga de `CorelMacros`) | `GridPreparationService.PrepareGrid` | Ver "Caso crítico" abaixo — mudança de comportamento intencional, não um ajuste cosmético. **Bug de centralização corrigido** (2026-10) — ver `ResizePageToFitAndCenter` e "Bug confirmado: Preparar Grid não centraliza" abaixo |
| Preparar Grid Novo | `FluxoGrid.ConfiguraNovoGrid` (fluxo anterior ao Grid preservado sem reescrita, chama o novo `Grid.PreparaGrid` no final) | `GridPreparationService.PrepareNewGrid` | **Revisão funcional pendente** (2026-10, ver nota abaixo) — comportamento atual preservado sem alteração até receber a nova especificação do usuário |
| Preparar Molde Novo | `Molde.ConfiguraMoldNovo` (idêntica à original) | `MoldPreparationService.PrepareNewMold` | **Revisão funcional pendente** (2026-10, ver nota abaixo) — comportamento atual preservado sem alteração até receber a nova especificação do usuário |
| Preparar Escudo/Bandeira/Faixa de Capitão | `Grid.PreparaEscudoParaSaida` / `PreparaBandeiraParaSaida` / `PreparaFaixaDeCapitaoParaSaida` (revisão candidata — agora compartilham `PrepararMaterial`, que SALVA automaticamente) | `OutputPreparationService.PrepareForOutput` + `SaveOutput` | O caminho de saída padrão `Z:\Neuber\Saídas\` foi preservado como PADRÃO (a revisão candidata confirma: "destinos em Z: ... continuam como no fluxo anterior"), configurável via parâmetro para outras máquinas. **Confirmado pelo usuário no teste real** (2026-10) — comportamento validado no cenário testado, não generalizado para todos os casos possíveis |
| Somar medidas (só altura) | `Medidas.SomaAlturasSelecionadasEmMetros` (idêntica) | `MeasurementSumService.SumHeightOnly` | **Corrigido** (2026-10): uma revisão anterior desta migração tinha restringido esta rotina a mm/cm/m, alegando (incorretamente) que era assim no original. Comparação linha a linha com `Referência/Macros/modulos/Medidas.txt` mostrou que as duas rotinas (`SomaAlturasSelecionadasEmMetros` e `SomaMedidasSelecionadas`) usam o MESMO `Select Case` (mm/cm/m/polegada/pé/jarda) — não há diferença no original. Esta era a causa do relato do usuário de que "Somar Altura (m)" não funcionava com o documento em outra unidade |
| Somar medidas (com escolha) | `Medidas.SomaMedidasSelecionadas` (idêntica) | `MeasurementSumService.SumSelected` | `InputBox` "1/2/3" → três botões |
| Agrupar Interseção | `Agrupamento.AgruparObjetosSobrepostos` (revisão candidata — achada depois de a primeira busca não encontrar nada em `CorelMacros`) | `IntersectGroupService.GroupOverlapping` | Ver ressalva abaixo — **o vínculo entre esta macro e o botão "Agrupar Interseção" não foi confirmado nem pela própria revisão candidata** |

## Revisão funcional pendente: Preparar Molde Novo e Preparar Grid Novo

O usuário informou (2026-10) que o funcionamento destas duas ferramentas será redefinido — a
especificação atual (portada de `Molde.ConfiguraMoldNovo` e `FluxoGrid.ConfiguraNovoGrid`) não é
a versão final. **Não redesenhar nem tentar completar essas regras sem a nova especificação.**
Os dois comandos continuam funcionando exatamente como portados (ver tabela acima) até que ela
chegue. "Preparar Grid Novo" permanece na seção "Preparação e saída" do painel.

## Caso crítico: Grid com camadas ocultas — resolução

A versão de `Grid.PreparaGrid` em `CorelMacros` (sem a revisão) dependia de
`ActiveSelection`/`CreateSelection`, que no Corel só inclui objetos visíveis e desbloqueados —
então camadas ocultas ficariam de fora do agrupamento. A **revisão candidata reescreve esse
comportamento de propósito**: `Grid.PreparaGrid` agora torna todas as camadas não-especiais
temporariamente visíveis, captura todos os objetos diretamente (sem `Group`/`Ungroup`, sem
mover entre camadas), translada o conjunto inteiro para o centro da nova página, e restaura a
visibilidade original de cada camada ao final — **sempre**, inclusive em erro.

Ou seja: **objetos de camadas ocultas (FE, C) participam do Grid.** Isso substitui o
entendimento anterior deste documento (que presumia exclusão). Portado fielmente em
`GridPreparationService.PrepareGrid`, coberto pelo teste
`GridPreparationServiceTests.PrepareGrid_HiddenFeAndC_ParticipateAndVisibilityIsRestored`.
Validação real no Corel ainda pendente — ver `docs/mvp-smoke-test.md`.

## Bug confirmado e corrigido: "Preparar Grid não centraliza" (2026-10)

Causa raiz confirmada por teste direto nesta máquina (conexão COM ao CorelDRAW 2026 real, não
suposição) — ver `docs/mvp-smoke-test.md` para o roteiro completo do diagnóstico:

**CorelDRAW redimensiona a página em torno do seu próprio centro.** Quando `Page.SizeWidth`/
`SizeHeight` mudam, o referencial em que `Shape.CenterX`/`CenterY` são relidos muda junto — todo
objeto da página "parece" ter se movido metade da variação de tamanho, mesmo sem nenhum `Move`
explícito. `ResizePageToFitAndCenter` (`GridPreparationService.cs`) calculava o centro combinado
dos objetos UMA VEZ, antes de redimensionar a página, e reaproveitava esse valor para calcular a
translação depois do resize — misturando dois referenciais diferentes, o que produzia um
deslocamento sistematicamente errado.

A macro VBA original nunca teve esse problema: `Grid.PreparaGrid` lê `conjunto.CenterX`/
`CenterY` (propriedades COM ao vivo do `ShapeRange`) só na própria linha do `.Move`, já depois
do resize da página — nunca reaproveita um valor antigo.

**Correção**: `ResizePageToFitAndCenter` agora mede a caixa delimitadora dos objetos duas vezes
— uma vez antes do resize (só para dimensionar a página) e de novo depois (para calcular a
translação) — igual ao que a macro original faz ao reler `conjunto.CenterX` só no `.Move`.
Verificado com uma reprodução isolada e não-destrutiva no CorelDRAW real (documento novo e
descartável, nunca o arquivo do usuário): sem a correção, dois retângulos de teste terminavam
desalinhados do centro da página pelo mesmo valor relatado pelo usuário; com a correção, o
centro combinado bate exatamente com o centro da página.

Também corrigido nos testes: `FakePage`/`FakeShape` (test doubles) agora simulam esse mesmo
deslocamento de referencial ao redimensionar a página, para que
`GridPreparationServiceTests.PrepareGrid_CentersCombinedBoundingBoxExactlyOnPageCenter` detecte
esta classe de regressão sem precisar do Corel.

**Validação real pendente**: reinstalar a build corrigida (`0.1.0-dev.4` ou posterior) e repetir
"Preparar Grid" no documento de reprodução (`Referência/GRID DESCENTRALIZADO.cdr`) ou em um novo,
inclusive com FE/C ocultas.

## Mudanças deliberadas (preservação de comportamento vs. ajustes necessários)

- **Caminho de saída de arquivo**: preservado como padrão (`Z:\Neuber\Saídas\`), não removido
  — ver tabela acima. Configurável, não cravado de forma irrevogável.
- **Confirmação "Gerenciador de Objetos está aberto?"** (`Utilidades.VerificarGerenciadorObjetos`):
  a revisão candidata mantém essa confirmação no início de `Grid.PreparaGrid`. Não foi
  reproduzida no painel novo — é uma confirmação de fluxo de trabalho do VBA antigo sem efeito
  nos dados, e o painel WPF não depende do estado de nenhuma docker específica do Corel para
  funcionar corretamente.
- **Diálogos `InputBox` substituídos por controles do painel** (combos/caixas de texto/botões).

## Inconsistência pré-existente — corrigida por decisão explícita do usuário

`Utilidades.OrganizarCamadas` usa o prefixo `"CHO1."` na lista de ordenação, mas o único código
válido de tipo de peça para "Short" é `SHO1`. Confirmado que a revisão candidata **não**
corrigiu isso (`Referência/Macros/modulos/Utilidades.bas`, array `prefixos` idêntico ao
original). Perguntado ao usuário, que confirmou a correção: `LayerOrganizationService.PrefixOrder`
usa `"SHO1."` (não `"CHO1."`), para que camadas de Short sejam ordenadas corretamente por
"Preparar Grid"/"Preparar Grid Novo".

## Ressalva explícita: Agrupar Interseção

A própria revisão candidata não tem certeza do vínculo. Duas citações diretas dos relatórios:

> "Remoção de AP/TS não localizada. A rotina de agrupamento foi identificada pelo
> comportamento descrito, mas o vínculo real do botão não foi inspecionado."
> — `relatorios/BARRA_PRIORITARIAS.md`

> "AgruparObjetosSobrepostos procura coincidência de posição/tamanho e objetos internos; não é
> agrupamento por qualquer interseção geométrica. Confirmar se é o botão citado como Agrupar
> Interseção." — `relatorios/INDEPENDENTES.md`

Ou seja: `Agrupamento.AgruparObjetosSobrepostos` (objetos com mesma posição/tamanho, mais
objetos menores aninhados na mesma área — ver `IntersectGroupService`) é o candidato mais
plausível encontrado, e está implementado e testado (`IntersectGroupServiceTests`). Mas
**ninguém confirmou ainda que este é de fato o algoritmo atrás do botão "Agrupar
Interseção"** — nem a macro original (que não existe), nem a revisão candidata (que admite a
lacuna). Trate como "implementado a partir do melhor candidato disponível, pendente de
confirmação com o usuário/smoke test", não como "resolvido com certeza".

## Deferido

`Correcoes.AjustarLarguraObjetos` / `AjustarLarguraObjetosRecursivo` (limita a largura de um
objeto chamado "NO" a 850.394pt) não está na lista de prioridades do usuário e não foi
portado. Também nota: a macro original referencia `cdrPowerClipShape`, que não existe mais
como valor distinto em `cdrShapeType` na versão 27 do VGCore (ver `docs/ARCHITECTURE.md`) —
outro motivo para não portar sem confirmar primeiro.
