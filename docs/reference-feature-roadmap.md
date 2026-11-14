# Referências funcionais — SISBolt, eCut e execução no PX

Atualizado em 2026-11-10 com as capturas do usuário e o checkout `NeuberJone/PXToolBox`,
base `3acbfc0` (dev.93). Objetivo aprovado: funcionalidades próprias no PX, com prioridade
expressa para **Nesting e Slice** do eCut.

“Implementado” significa encontrado no código/changelog. Aceitação no Corel e reconhecimento
pelo SISBolt mantêm o estado dos roteiros. A pasta `Referência/` é ignorada pelo Git e não
veio no clone; as telas usadas aqui foram fornecidas na conversa de 2026-11-10.

## Estado e sequência de entregas

| Frente | Estado conferido | Próxima entrega |
|---|---|---|
| Cadastro de moldes | Definição/lote, logos, duplicação, Grid, configurações, TAG/laser, salvar e validar até dev.93 | Aceitação no Corel; identificador/Shape.Comment conforme plano anterior |
| Montagem compatível | Tamanho Padrão, Esticar, Ignorar métricas, Mão Livre, grupo único, vínculo horizontal, Resetar ângulo | Completar Alinhamento, troca de posição, PowerClip e formato de imagens |
| Motor de montagem próprio | Planejado no roadmap do ecossistema | Lista → Grid/molde → variáveis/arte → peças → revisão/exportação |
| Slice | Planejamento/prévia (dev.96), divisão por PowerClip (dev.97), pelas guias da página (dev.100), contorno da arte na prévia (dev.107), corte livre por guias inclinadas (dev.108) e por linhas desenhadas, curvas e que param no meio da arte (dev.115), miniatura com cores (dev.116) | Aceitação no Corel |
| Nesting | Pela caixa (dev.98), pelo contorno real (dev.99), com quantidades (dev.101) em várias áreas digitadas (dev.102) e busca da melhor ordem com tempo máximo/cancelar (dev.103) ordem de preenchimento das áreas (dev.104) e perfis salvos, também nas preferências portáteis (dev.105–106), canto de início (dev.109), comparação com o original (dev.110), rolo em folhas (dev.111), fileiras alinhadas pelo contorno (dev.112), blocos agrupados lado a lado (dev.113) e separado por camada/tecido (dev.114), página, rolo ou retalhos, prévia, conferência e aplicação | Aceitação no Corel; retalhos vindos do Nexor |
| Nexor | Contexto, nomes, subpasta, retorno de saídas e recentes por arquivo | Contrato de tecidos/retalhos para encaixe |
| Personalização | Favoritos, arraste, nomes, seções e preferências portáteis | Preservar layout e IDs ao adicionar ferramentas |
| ListForge | Integrado/standalone e regras compartilhadas | OCR e decisão de licenciamento |
| Stable | Distribuição bloqueada pelo script | Empacotamento e licenciamento próprios conforme AGENTS.md |

Ordem escolhida neste incremento: **Slice planejamento → recorte → Nesting retangular →
contorno real**. Demais funções entram no inventário para entregas incrementais. Preparar
Grid/Molde Novo fica sem nova revisão nesta rodada, conforme adiamento do usuário.

## Inventário SISBolt

### Ferramentas e painel

| Referência | Correspondência PX / pendência |
|---|---|
| Esticar no Tamanho do Molde | Implementado: `STRETCH_PROPS[0]` booleano |
| Ignorar métricas para variável selecionada | Implementado: `IGNORE_SISBOLT_VARIABLE_METRICS[0]` booleano |
| Mão Livre | Implementado: `FREE_HAND_PROPS[0]`, ativação em textos NO/NU/AP/TS |
| Tamanho Padrão | Implementado e confirmado pelo usuário; flag e dimensões |
| Resetar ângulo | Implementado para os tipos aceitos; aceitação pendente |
| Criar grupo com item único | Implementado; nome especial da primeira captura não é regra geral comprovada |
| Alinhamento | Capturado `ALIGNMENT_PROPS[0..4]`; faltam âncoras/margens e limpar |
| Alinhamento horizontal automático | Vínculo por GUID implementado; referência com GUID ainda limitada |
| Trocar de Posição | Captura compatível com centros em X; falta confirmar Y com alturas diferentes |
| Replicar PowerClip | Capturada substituição; origem e coordenadas a confirmar |
| Formato de exportação de imagens | Opções/persistência a investigar |
| Renomear objetos | PX já oferece renomear; conferir opções da referência |
| Criar camada para seleção | Cadastro usa grupos de tamanho; variante genérica a avaliar |
| Organização avançada | Cadastro ordena/distribui; encaixe depende do Nesting |
| Criação de modelagens | Cadastro implementado; assistentes futuros |
| Ocultar detalhes (DETALHES/DETAILS/DETALES) | Planejado; alcance e restauração a definir |
| Gerar gêneros faltantes MA/FE/C | Planejado; observar duplicação/identificação |
| Camadas do sistema em maiúsculas | Cadastro oferece maiúsculas; alcance da referência a conferir |
| Duplicar página | Planejado |
| Converter fundo FUNDO para bitmap | Planejado; resolução e recorte a definir |
| Aplicar nomes/números/apelidos/sangue | Códigos NO/NU/AP/TS existentes; dados dependem do motor de montagem |
| Pesquisar Reposição | Planejado; depende de índice e identidade da peça |
| Selecionar por cor / nome | Planejado; opções para objetos ocultos e alcance a definir |
| Gerenciamento RGB/CMYK | Planejado |
| Girar 90° horário/anti-horário; espelhar | Espelho na duplicação de molde; comandos genéricos a conferir/implementar |
| Organizar na vertical | Ordenar/distribuir no Cadastro; avaliar variante genérica |
| SVG rápido 1024 px / PNG seleção 150 dpi | Planejado; nomes, transparência e destino a definir |
| Replicar arte em peças de mesmo nome | Planejado; origem e substituição a confirmar |
| Excluir camadas vazias | Implementado no Cadastro |
| Pasta de instalação / gerenciar pasta do sistema | Infraestrutura de pastas existe; biblioteca própria de recursos a planejar |
| Gerar Grid / processamento expresso / exportação rápida | Grid existente; montagem/exportação em lote planejadas |
| Contrair/expandir / mostrar painel | Seções e personalização existentes; toolbar a avaliar |

### Configuração do processamento

| Grupo das telas | Funcionalidades previstas |
|---|---|
| Entrada/revisão | Arquivo/link, pedido quando existir, contagem por categoria, relação de pessoas/peças e seleção de tamanhos presentes |
| Modelagem | Biblioteca CDR com busca, pasta e prévia; vínculo peça/tamanho/molde |
| Fontes vetoriais | Números e nomes CDR, prévia, MA/FE/C, espaço frente/costas, largura máxima e altura fixa |
| Textos | Capitalização, redução percentual, centralização na peça/variável, quebra em nomes/apelidos |
| Acentos | Substituição e distância superior em mm proporcional à altura de referência (5 cm na tela) |
| Métricas | Nomes/números por MA/FE/C, alinhamento relativo, largura máxima versus fixa mutuamente exclusivas, altura fixa; vazio = sem regra |
| Cores/acabamento | RGB/CMYK, verificar cores, contorno laser em mm, alinhamento PowerClip, converter em curvas |
| Peças | Multiplicar por quantidade, ordem de produção na TAG, fora do PowerClip, índice sequencial e exportação de reposição dependente do índice |
| Após processamento | Revisão, organização automática opcional e exportação expressa |
| Desempenho | Execução responsiva, cancelamento e recuperação da vista; efeito de “turbo” ainda a medir |
| Sistema | Preferências próprias, recursos, idioma/notificações/som conforme prioridade; importação externa exige mapa |
| Exportação | Pasta, formatos, nomes, opções de saída e rastreabilidade |
| Encaixe | Área, espaçamento, rotações, tecido/perfis pelo motor PX |

ListForge fornece dados normalizados. PX executa a montagem. Escala, PowerClip, fontes vetoriais
e acentos exigem casos de entrada/saída; a presença de um controle não determina sua regra.

## Slice prioritário

### S1 — Planejamento e prévia nesta rodada

- [x] `SlicePlanner` no Core, sem COM, em milímetros.
- [x] Tabela por linhas/colunas e divisão por dimensão máxima da parte.
- [x] Sobreposição, bordas limitadas à arte e limite de 1000 partes.
- [x] Botão Montagem, dimensões iniciais da caixa da seleção convertidas de Document.Unit.
- [x] Prévia geométrica, medidas em cm, copiar plano, manual e favoritos.
- [ ] Compilação/renderização WPF Windows e conversão real em mm/cm/pol no Corel.

Decisão PX neste incremento: sobreposição é a largura TOTAL compartilhada na emenda. Tabela
distribui metade para cada lado. Dimensão máxima inclui sobreposição, avança por
`máximo - sobreposição` e limita a última parte à borda. As fórmulas precisam de teste
comparativo antes de declarar equivalência com o eCut. S1 mostra a caixa e as divisões,
sem miniatura da arte. Campos editados recalculam o plano e erros limpam o resultado anterior.

### S2 — Recorte real (dev.97)

- [x] APIs conferidas por reflexão (`AddToPowerClip(Shape, cdrTriState)`, `SetNoOutline`); semântica
  em documento descartável fica para o roteiro 4ag.
- [x] Manter original por opção (oculto ou apagado); aplicar só pelo botão "Dividir no Corel".
- [x] Prévia da arte como contorno (dev.107): curvas e grupos pelo contorno, texto/imagem pela caixa.
  Miniatura com cores na dev.116 (PNG da seleção gerado pelo Corel).
- [x] Plano recusado se a seleção não tiver a área da prévia; botão desliga depois de dividir.
- [x] Falha no meio apaga as partes já criadas; um Ctrl+Z (RunWithWorkspace) restaura.
- [x] PowerClip por parte preserva texto/bitmap/grupo, cores e Properties sem conversão — **a confirmar
  no Corel** (4ag).

### S3 — Guias e manual

- [x] Guias horizontais/verticais (dev.100): dividir pelas guias, criar guias nas emendas do plano e
  remover só as do PX. "Distribuir/remover bordas" das telas do eCut não implementado: efeito ainda não
  confirmado.
- [x] Livre por guias inclinadas (dev.108): retas em qualquer ângulo atravessando a arte, sobreposição, prévia,
  Ctrl+Z único e manter original.
- [x] Linhas desenhadas (dev.115): retas ou curvas na camada "Cortes (Slice)", podendo parar no meio da arte
  encostando noutro corte; sobreposição seguindo a curva.
- [ ] Formas irregulares, furos e imagens validados antes de liberar cada tipo.

## Nesting prioritário

Motor próprio conforme `docs/px-ecosystem-engines-roadmap.md`.

| Etapa | Entrega e aceitação |
|---|---|
| N1 retangular | **dev.98** — área (página ou rolo), bordas, distância e giro 90°; determinístico, sem colisões/peças fora (teste com 120 peças); lista o que não coube. Quantidades por peça na **dev.101** |
| N2 prévia/aplicar | **dev.98** — cm, tamanho da página/ajuste ao rolo, fórmula de aproveitamento explícita, Ctrl+Z único; só move/gira. "Comparar original" na **dev.110** (caixa ocupada hoje × encaixe, posição original na prévia); aceitação no Corel pendente (4ah, 4at) |
| N3 contorno | **dev.99** — polígonos do DisplayCurve (Bézier em retas), furos, concavidades e distância mínima pela grade; verificação independente nos polígonos exatos; sentido do tecido por "só 180°". Aceitação no Corel pendente (4ai) |
| N4 busca/perfis | Múltiplas áreas digitadas, enchidas na ordem, na **dev.102**; melhor resultado entre 5 ordens de peças, tempo máximo e cancelar na **dev.103**; ordem de preenchimento das áreas (digitada, menores ou maiores primeiro) na **dev.104**; perfis salvos na **dev.105** e nas preferências portáteis na **dev.106**; canto de início (origem) na **dev.109**; blocos agrupados lado a lado na **dev.113**; material = camada (um bloco por tecido) na **dev.114**; distância entre blocos e legenda por bloco na **dev.119**. Falta o material vindo do Nexor (N5) |
| N5 Nexor | Contrato de tecidos/retalhos com dimensões úteis/restrições; consumo proposto e confirmado no Nexor |

Inventário adicional das telas eCut para N2–N4: reduzir área, bordas, permitir dentro,
aproximação, ângulos fixos/livres, origem, acomodação, bounding box/legenda/nome, contêiner,
centro de rotação, ordem/distância de blocos e rotação posterior. Confirmar cada alternativa.

## Demais funções eCut — backlog após as prioridades

| Grupo | Itens visíveis |
|---|---|
| Nesting auxiliar | Rectangle nesting, Multiply |
| Plotter | Cut/plot, Weeding lines, Cropmarks, Contour cutting |
| Medição | Perímetro, área, custo, tempo, volume |
| Pré-processamento | Arestas repetidas, interseções/duplicatas, Smart divide, separar curvas, Sandwich group, combinar páginas, separar, conectar caminhos, reduzir nós, fillet |
| CNC | Cassette, edge roll, percurso, carrier, pontes, G-code, extensão de nós, finger-joint box/shape |
| Adicionais | LED, Neon, Variable, Hot wire, Eyelets, Liquid acrylic, Scripts |
| CAD | Isométrico, DXF, centro de massa, fillet, cotas, flange, numeração, linha central, CAD tools |
| Busca/substituição | Search, Align, Set size, Replace |
| Utilitários | Efeitos vetoriais, linhas/grid/formas, Draft, escala de referência |

## Entrega e continuidade

Cada incremento terá entrada/resultado esperados e verificação pelo painel. Serviços puros,
WPF/interop e smoke test Corel são evidências separadas. O manual acompanha a ferramenta.
Nenhum novo número DEV foi atribuído aqui; a versão e o instalador dependem do pipeline Windows.

Branch desta rodada: `feat/slice-preview-planning`.
Commits sugeridos:
- `docs: consolidate SISBolt and eCut feature roadmap`
- `feat: add rectangular slice planning and preview`
