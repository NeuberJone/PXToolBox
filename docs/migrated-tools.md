# Ferramentas migradas — de onde vêm e o que mudou

Desde a dev.96: **Dividir em partes (Slice)…** em Montagem (até a dev.96, "Planejar divisão"), com
`SlicePlanner` próprio (`PXToolBox.Core.Slicing`) e janela de prévia. A caixa da seleção é
dividida por tabela ou dimensão máxima com sobreposição. Desde a dev.97 divide no documento
(`SliceCutService`: uma parte por PowerClip com cópia da arte). Referência funcional: telas eCut
enviadas em 2026-11-10. Fórmula de sobreposição escolhida no PX, com comparação com eCut pendente;
ver `docs/reference-feature-roadmap.md`, S1/S2, e roteiros 4ae e 4ag.

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
| *(entrada de barra preservada, achada no relatório)* | `Correcoes.RemoveCodigosNoNu` | `CodeToggleService.ExpandNoAndNuCodesRecursively` | Ferramenta DIFERENTE do toggle: expande "NO"/"NU" em TODA a página, recursivamente em grupos E PowerClips (ver `docs/ARCHITECTURE.md`, seção 15 — a macro original só descia em grupos; descida em PowerClip é uma melhoria pedida pelo usuário, não restauração), para "TextoNome"/"TextoNumero" (sem espaço — convenção própria). Não trata AP/TS (a macro original também não trata) |
| Remover códigos da página (NO, NU, AP, TS individuais; ou todos) | — (nova, pedida pelo usuário em 2026-11; generaliza `Correcoes.RemoveCodigosNoNu`) | `CodeToggleService.RemoveCodesRecursively` | Mesma descida de "Remover NO/NU" (todas as camadas da página ativa, grupos e PowerClips; contêineres nunca renomeados). NO → "TextoNome" e NU → "TextoNumero" (como a macro original); AP → "Texto Apelido" e TS → "Texto Tipo Sanguíneo" — os nomes longos da alternância, por decisão do usuário (não há macro de remoção para AP/TS). "Remover NO/NU da página" continua existindo, sem mudança |
| Renomear Objetos Selecionados | `Utilidades.RenomearObjetosSelecionados` (inalterada) | `RenameSelectedObjectsService.Rename` | `InputBox` → caixa de texto no painel |
| Alterar Tipo de Peça (documento inteiro, por camada) | `Correcoes.RenomeiaTipoDePeca` (inalterada) | `PieceTypeRenameService.Rename` | Dois `InputBox` → dois `ComboBox` (`FromTypeCombo`, com opção "Tudo", e `ToTypeCombo`). Botão "Alterar tipo no documento inteiro" religado ao painel em 2026-10 (pedido do usuário: a opção de buscar o documento inteiro, como "CAM1 → CAM2", precisava voltar a existir ao lado das opções por objetos selecionados/camada ativa) — percorre todas as páginas/camadas, nunca depende de seleção nem de camada ativa |
| Alterar tipo dos objetos selecionados | Sem precedente na macro original (ação nova, pedida pelo usuário em 2026-10) | `PieceTypeRenameService.RenameSelectedObjects` | Reconhece o tipo pelo NOME de cada objeto selecionado, usando a mesma convenção pontuada de `LayerName` (primeiro segmento = tipo) — a mesma convenção que `Correcoes.RenomearObjetosComNomeDaCamada` já grava nos objetos, não uma convenção inventada. Objetos com nome em outro formato são ignorados e contados à parte |
| Alterar tipo da camada ativa | Sem precedente na macro original (ação nova, pedida pelo usuário como "camadas selecionadas") | `PieceTypeRenameService.RenameActiveLayer` | **Limitação confirmada por reflexão direta sobre `Corel.Interop.VGCore.dll`**: a API do CorelDRAW não expõe multi-seleção de camadas do Gerenciador de Objetos — só existe `Document.ActiveLayer` (singular). Por decisão do usuário, a ação atua só nesta camada; o botão e o tooltip deixam isso explícito ("camada ativa", não "camadas selecionadas") |
| Alternar visibilidade MA/FE/C | `Montagem.AlternarMA` / `AlternarFE` / `AlternarC` (revisão candidata — achada depois de a primeira busca não encontrar nada) | `LayerVisibilityService.Toggle` | **Correção deliberada**: a candidata compara só os 2 últimos caracteres do nome contra "MA"/"FE" sem exigir ponto antes (só "C" é comparado como ".C", com ponto) — isso deixaria "MA"/"FE" vulneráveis a casar uma camada como "FORMA" por acidente. Implementado aqui com o último segmento pontuado inteiro para as três categorias, igualmente |
| Ocultar camadas dos objetos selecionados | — (nova, sem macro de origem) | `LayerVisibilityService.HideLayersOfSelection` | Oculta cada camada que contém ao menos um objeto selecionado, uma vez só por camada (identidade = `Layer.Page.Index` + `Layer.Index`, confirmados por reflexão sobre `Corel.Interop.VGCore`). Nunca oculta camadas especiais do Corel (Guias, Área de trabalho, Grade), que afetariam todas as páginas. Camadas já ocultas não são tocadas nem registradas. Registra em memória (dev.47: sem gravar nada no documento) as camadas que de fato ocultou, para a restauração abaixo. Um único passo de desfazer (dev.45; registro desde dev.46) |
| Mostrar últimas camadas ocultadas | — (nova, sem macro de origem) | `LayerVisibilityService.RestoreLastHiddenLayers` | Mostra de novo SÓ as camadas que a última ocultação efetiva do botão acima mudou de visível para oculta — nunca todas as camadas do documento. Registro só na memória da sessão; nada é gravado no CDR (dev.47 — a dev.46 gravava uma marcação em `Properties`, removida; ver `docs/ARCHITECTURE.md`, seção 32). Documento e camadas identificados pelo objeto da sessão (comparação por referência), nunca por nome, caminho, índice ou marcação. Fechar o documento de origem invalida o registro; reabrir o arquivo não o recupera. Recusa sem alterar nada se o documento de origem não for o ativo; identidade ambígua invalida a restauração; camadas excluídas são informadas como não encontradas, nunca substituídas por outra de mesmo nome; camadas especiais nunca são alteradas. Um único passo de desfazer. Botão desabilitado (com dica explicando) sem registro. Novo em dev.46, corrigido em dev.47 |
| Preparar Grid | `Grid.PreparaGrid` (revisão candidata — **reescrita**, não a versão antiga de `CorelMacros`) | `GridPreparationService.PrepareGrid` | Ver "Caso crítico" abaixo — mudança de comportamento intencional, não um ajuste cosmético. **Bug de centralização corrigido** (2026-10) — ver `ResizePageToFitAndCenter` e "Bug confirmado: Preparar Grid não centraliza" abaixo |
| Aplicar contorno do Grid | `Utilidades.AplicaContornoGrid` (idêntica; também chamada por `FluxoGrid.ConfiguraNovoGrid`) | `GridPreparationService.ApplyGridOutline` | Botão próprio desde a dev.61 (pedido do usuário). Mesmo estilo e alcance (todas as páginas, objetos de primeiro nível); **diferença deliberada**: pula camadas especiais do Corel (Guias/Grade) para não pintar guias. O passo dentro do Preparar Grid Novo continua idêntico à macro |
| Preparar Grid Novo | `FluxoGrid.ConfiguraNovoGrid` (fluxo anterior ao Grid preservado sem reescrita, chama o novo `Grid.PreparaGrid` no final) | `GridPreparationService.PrepareNewGrid` | **Revisão funcional pendente** (2026-10, ver nota abaixo) — comportamento atual preservado sem alteração até receber a nova especificação do usuário |
| Preparar Molde Novo | `Molde.ConfiguraMoldNovo` (idêntica à original) | `MoldPreparationService.PrepareNewMold` | **Revisão funcional pendente** (2026-10, ver nota abaixo) — comportamento atual preservado sem alteração até receber a nova especificação do usuário |
| Preparar Escudo/Bandeira/Faixa de Capitão | `Grid.PreparaEscudoParaSaida` / `PreparaBandeiraParaSaida` / `PreparaFaixaDeCapitaoParaSaida` (revisão candidata — agora compartilham `PrepararMaterial`, que SALVA automaticamente) | `OutputPreparationService.PrepareForOutput` + `SaveOutput` | O caminho de saída padrão `Z:\Neuber\Saídas\` foi preservado como PADRÃO (a revisão candidata confirma: "destinos em Z: ... continuam como no fluxo anterior"), configurável via parâmetro para outras máquinas. **Confirmado pelo usuário no teste real** (2026-10) — comportamento validado no cenário testado, não generalizado para todos os casos possíveis |
| Somar medidas (só altura) | `Medidas.SomaAlturasSelecionadasEmMetros` (idêntica) | — (**removido**, 2026-10) | Portado inicialmente como `MeasurementSumService.SumHeightOnly`/botão "Somar Altura (m)". Comparação linha a linha com `Referência/Macros/modulos/Medidas.txt` já tinha confirmado (correção de 2026-10 abaixo) que esta rotina e `SomaMedidasSelecionadas` usam o MESMO `Select Case` de unidades e sempre convertem para metros — ou seja, sempre produziam exatamente o mesmo resultado que o botão "Somar Altura" (linha abaixo, modo Height). Removido como duplicata a pedido do usuário; "Somar Altura" cobre o mesmo caso |
| Somar medidas (com escolha) | `Medidas.SomaMedidasSelecionadas` (idêntica) | `MeasurementSumService.SumSelected` | `InputBox` "1/2/3" → três botões ("Somar Altura"/"Somar Largura"/"Somar Altura e Largura"), **sempre em metros** nos três — rótulos/tooltips corrigidos (2026-10) para não sugerirem "unidade do documento", que nunca foi o comportamento real |
| Agrupar Interseção | `Agrupamento.AgruparObjetosSobrepostos` (revisão candidata — achada depois de a primeira busca não encontrar nada em `CorelMacros`) | `IntersectGroupService.GroupOverlapping` | **Confirmado pelo usuário no teste real** (2026-10; reafirmado em 05/11/2026) — comportamento validado no cenário testado. Origem: ver nota abaixo |

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

## Agrupar Interseção — origem e confirmação

**Confirmado pelo usuário no CorelDRAW real** (2026-10, no cenário testado — ver
`docs/mvp-smoke-test.md`; reafirmado em 05/11/2026). A nota abaixo fica como histórico de por que
a ferramenta precisou dessa confirmação.

A própria revisão candidata não tinha certeza do vínculo. Duas citações diretas dos relatórios:

> "Remoção de AP/TS não localizada. A rotina de agrupamento foi identificada pelo
> comportamento descrito, mas o vínculo real do botão não foi inspecionado."
> — `relatorios/BARRA_PRIORITARIAS.md`

> "AgruparObjetosSobrepostos procura coincidência de posição/tamanho e objetos internos; não é
> agrupamento por qualquer interseção geométrica. Confirmar se é o botão citado como Agrupar
> Interseção." — `relatorios/INDEPENDENTES.md`

Ou seja: `Agrupamento.AgruparObjetosSobrepostos` (objetos com mesma posição/tamanho, mais
objetos menores aninhados na mesma área — ver `IntersectGroupService`) é o candidato mais
plausível encontrado, e está implementado e testado (`IntersectGroupServiceTests`). Nem a
macro original (que não existe) nem a revisão candidata confirmavam o vínculo — a confirmação
veio do teste real do usuário, válida para o cenário testado.

## Deferido

`Correcoes.AjustarLarguraObjetos` / `AjustarLarguraObjetosRecursivo` (limita a largura de um
objeto chamado "NO" a 850.394pt) não está na lista de prioridades do usuário e não foi
portado. Também nota: a macro original referencia `cdrPowerClipShape`, que não existe mais
como valor distinto em `cdrShapeType` na versão 27 do VGCore (ver `docs/ARCHITECTURE.md`) —
outro motivo para não portar sem confirmar primeiro.
