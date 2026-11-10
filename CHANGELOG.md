# PX Tool Box DEV — histórico de versões

Tudo num lugar só, da mais nova para a mais antiga. Gerado a partir de:

- **Notas da versão** (`host/PXToolBox.CorelHost.Dev/ReleaseNotes/release-notes.json`, o que aparece em Diagnóstico → Notas da versão) — da dev.59 em diante;
- **`ROADMAP.md`** — o resumo de cada rodada, usado da dev.44 à dev.58 e para o roteiro de teste de cada versão;
- **`docs/ARCHITECTURE.md`** — os detalhes técnicos, indicados por seção (§).

“Teste no Corel” aponta o roteiro em `docs/mvp-smoke-test.md`. **Pendente** = ainda não rodado no CorelDRAW real; só testes automatizados e validação do pacote. Cada versão tem o instalador em `dist\dev\<versão>\installer\`.

## Testes no Corel pendentes

| Versão | Roteiro | O quê |
|---|---|---|
| dev.93 | 4ac | Cadastro de moldes → “Validar molde”: confere os grupos de tamanho da página ativa e mostra um relatório, sem alterar nada. Erros: peça n… |
| dev.92 | 4ab | Diagnóstico → “Manual de uso”: janela com o manual completo do painel — o que é cada seção e, para cada ferramenta, para que serve, como… |
| dev.91 | 4aa | Configurações de moldes → “Definir objetos: contorno interno”: ligado, o Definir objetos (e o lote) põe na peça um contorno por dentro co… |
| dev.90 | 4z | Configurações de moldes → TAG: posição em relação à peça (as nove: superior/centro/inferior × esquerda/centro/direita) e margens em cm (e… |
| dev.89 | 4y | Exportar/Importar preferências (Configurações do ListForge integrado) agora levam também as Configurações de moldes (tamanhos, códigos, G… |
| dev.88 | 4x | Cadastro de moldes → “Salvar molde…”: digite o nome do molde e confirme onde salvar; o documento inteiro é salvo como .cdr com esse nome… |
| dev.87 | 4w | Cadastro de moldes → “Duplicar peça”: cada grupo de tamanho selecionado ganha uma cópia inteira ao lado (à esquerda, à direita, acima ou… |
| dev.86 | 4v | Cadastro de moldes → “Propagar logos”: selecione os grupos de tamanho já montados da mesma peça; os logos (L1, L2…) do grupo com mais log… |
| dev.85 | 4u | Cadastro de moldes → “Detectar objetos em lote”: selecione os conjuntos (peça + logos) de vários tamanhos da mesma peça, escolha a catego… |
| dev.84 | 4t | Cadastro de moldes → “Definir objetos”: selecione a peça e os logos de um tamanho, digite o nome da peça e escolha o tamanho. O PX monta… |
| dev.83 | 4s | Cadastro de moldes → “Configurações de moldes…”: tamanhos de cada categoria (tamanhos base, prefixos e sufixos, com a lista gerada na hor… |
| dev.82 | 4s | Nova seção “Cadastro de moldes” (fase 1 das ferramentas de cadastro de moldes): Criar camadas para a seleção (grupos de tamanho → camada… |
| dev.81 | 4r | Preparação e saída → padrão novo: “Preparar Molde Novo” com camadas CAM1.MOLDE.MA.Frente (objetos com os mesmos nomes, laser continua x)… |
| dev.80 | 4q | Montagem → “Resetar ângulo”: deixa cada objeto selecionado com rotação 0° sem mudar a aparência (curva; retângulo e elipse girados viram… |
| dev.79 | 4p | Diagnóstico → “Roteiro de capturas…”: lista as capturas antes/depois que faltam para as ferramentas de montagem, com o que preparar na pá… |
| dev.78 | 4g | Exportar/Importar preferências agora levam também a personalização do painel (nomes das seções, seções criadas e a seção de cada ferramen… |
| dev.76 | 4k | Personalizar painel: renomear qualquer seção, criar seções novas (e removê-las) e escolher em que seção cada ferramenta aparece, com a or… |
| dev.73 | 4j | Favoritos: “Mostrar nomes” liga e desliga a exibição do botão inteiro, com ícone e nome, em vez de só o ícone. A escolha fica salva; dá p… |
| dev.70 | 4q | Grid → Ferramentas do SISBolt: “Ativar Esticar no Tamanho do Molde”, “Ativar Ignorar métricas da variável” e “Ativar Mão Livre” gravam a… |
| dev.67 | 4h, 4h passo 9 | Produção: “Recentes…” lista os últimos 10 contextos de produção carregados nesta máquina. Escolher um lê e valida o arquivo .json de novo… |
| dev.66 | 4j, 4j passo 13 | Favoritos: arraste um ícone com o botão esquerdo para mudar a ordem. Uma barra mostra onde ele vai entrar; soltar fora dos Favoritos canc… |
| dev.65 | 4h, 4h passo 8 | Produção: “Exportar resultado para o Nexor…” grava um arquivo .json com as saídas Escudo/Bandeira/Faixa salvas nesta sessão com o context… |
| dev.64 | 4h, 4h passo 7 | Outras ferramentas — Objetos: botão “Usar contexto de produção” preenche o novo nome dos objetos com o pedido informado do contexto carre… |
| dev.63 | 4h | Preparação e saída: botão “Usar contexto de produção” preenche “Nome do layout” com o pedido informado do contexto carregado (ou, sem ped… |
| dev.62 | 4h | Produção: resumo do contexto de produção carregado (rolo, trabalho de impressão e, quando o arquivo traz, pedido informado), com “Ver det… |
| dev.61 | 4o | Botão “Aplicar contorno do Grid” na seção Grid: aplica o estilo do Grid (preenchimento branco, contorno preto fino) a todos os objetos de… |
| dev.60 | 4m | “Notas da versão” também pode ser adicionado aos Favoritos (botão direito → Adicionar aos favoritos). |
| dev.59 | 4m | Botão “Notas da versão” no rodapé do painel. |
| dev.58 | 4n | Rodada da `0.1.0-dev.58`: pasta de saída padrão por máquina (Preparação e saída); contraste da caixa de filtro da comparação do ListForge… |
| dev.57 | 4j, 4j passo 3 | dicas legíveis: a dica dos ícones dos Favoritos tinha texto claro sobre fundo claro (relato do usuário na dev.56). As dicas do painel gan… |
| dev.56 | 4j, 4j passo 12 | todos os botões favoritáveis: o catálogo de favoritos passou de 10 para 44 ferramentas (todo botão de ferramenta das seções, inclusive Ag… |
| dev.55 | 4l | Remover códigos da página por código: na seção Códigos, além de "Remover NO/NU da página" (mantido), botões NO/NU/AP/TS para remover um c… |
| dev.53 | 4j, 4k | Personalizar painel e favoritos em ícone: "Personalizar painel…" no cabeçalho escolhe ordem e visibilidade das seções (rascunho até Salva… |
| dev.52 | — | relatório do Diagnóstico contido: ao terminar, o teste de identidade mostra só um resumo do resultado real; o relatório completo fica em… |
| dev.51 | 4j | Favoritos do painel: primeira etapa de personalização. Use a dev.51: a dev.50 gravava `"PanelFavoriteToolIds": null` em configurações sem… |
| dev.50 | 4j | Favoritos do painel: primeira etapa de personalização. Use a dev.51: a dev.50 gravava `"PanelFavoriteToolIds": null` em configurações sem… |
| dev.48 | 4e passo 6, 4i | diagnóstico de identidade no add-on: Diagnóstico → "Diagnóstico de identidade de camadas…" verifica, num documento temporário e com o mes… |
| dev.47 | — | correção da dev.46: a dev.46 gravava a marcação em `Document.Properties`/`Layer.Properties` (ia para o CDR ao salvar), contrariando o req… |
| dev.46 | 4i | Camadas: "Ocultar camadas dos objetos selecionados" e o complemento "Mostrar últimas camadas ocultadas" — restaura só as camadas que a úl… |
| dev.45 | 4i | Camadas: "Ocultar camadas dos objetos selecionados" e o complemento "Mostrar últimas camadas ocultadas" — restaura só as camadas que a úl… |

## Versões

### 0.1.0-dev.93

**Implementado**

- Cadastro de moldes → “Validar molde”: confere os grupos de tamanho da página ativa e mostra um relatório, sem alterar nada. Erros: peça não reconhecida, logo repetido no grupo, o mesmo L1, L2… em peças diferentes, número de logo trocado entre tamanhos. Avisos: logo que falta ou fora do lugar, tamanho repetido, sem TAG ou sem laser.
- Na janela do relatório: “Validar de novo” (depois de corrigir) e “Copiar”.
- “Validar molde” também pode virar favorito e está no Manual de uso.

_Teste no Corel: 4ac — pendente. Detalhes: ARCHITECTURE §60._

### 0.1.0-dev.92

**Implementado**

- Diagnóstico → “Manual de uso”: janela com o manual completo do painel — o que é cada seção e, para cada ferramenta, para que serve, como usar (passo a passo) e o que observar. Também explica Favoritos, Personalizar painel, preferências e o que fazer quando algo dá errado.
- Campo “Filtrar” no topo do manual: com uma ou mais palavras (ex.: laser, camada, Ctrl+Z), a lista mostra só os assuntos e as seções com resultado (com a contagem) e cada seção só as ferramentas que combinam; ✕ limpa. Sem diferenciar maiúsculas nem acentos. A janela pode ficar aberta ao lado do Corel.
- “Manual de uso” também pode virar favorito.

_Teste no Corel: 4ab — pendente. Detalhes: ARCHITECTURE §59._

### 0.1.0-dev.91

**Implementado**

- Configurações de moldes → “Definir objetos: contorno interno”: ligado, o Definir objetos (e o lote) põe na peça um contorno por dentro com a cor do tamanho e o estilo de linha da categoria (fábrica: masculino contínuo, feminino tracejado, infantil pontilhado). A cópia do laser continua com o contorno do laser.
- Cores por tamanho: digite as que quiser (ex.: G #FF0000); os outros tamanhos usam a cor sugerida pela posição na categoria. A janela mostra uma amostra de cor de cada tamanho.
- Precisa de “Criar a cópia do laser” ligado. Desligado (padrão), nada muda. Vai junto em Exportar/Importar preferências.

_Teste no Corel: 4aa — pendente. Detalhes: ARCHITECTURE §58._

### 0.1.0-dev.90

**Implementado**

- Configurações de moldes → TAG: posição em relação à peça (as nove: superior/centro/inferior × esquerda/centro/direita) e margens em cm (esquerda, direita, superior, inferior), com a descrição em palavras de onde a TAG vai ficar.
- O Definir objetos e o Detectar objetos em lote passam a pôr a TAG nessa posição. Sem mudar nada, continua como antes: embaixo da peça, no centro, sem margens.
- Posição e margens vão junto em Exportar/Importar preferências.

_Teste no Corel: 4z — pendente. Detalhes: ARCHITECTURE §57._

### 0.1.0-dev.89

**Implementado**

- Exportar/Importar preferências (Configurações do ListForge integrado) agora levam também as Configurações de moldes (tamanhos, códigos, Grid, laser, TAG e o que o Definir objetos cria).
- Arquivo exportado por uma versão anterior não mexe nas configurações de moldes atuais; configurações de moldes inválidas no arquivo são avisadas e ignoradas inteiras — o resto do arquivo é importado.
- Como as demais preferências, as de moldes importadas só são gravadas ao clicar em Salvar.

_Teste no Corel: 4y — pendente. Detalhes: ARCHITECTURE §56._

### 0.1.0-dev.88

**Implementado**

- Cadastro de moldes → “Salvar molde…”: digite o nome do molde e confirme onde salvar; o documento inteiro é salvo como .cdr com esse nome e passa a ser o arquivo salvo.
- A janela começa na pasta do documento (se ele já foi salvo) ou na pasta de moldes. Nome vazio ou com \ / : * ? " < > |: nada é salvo.
- Também pode ir para os Favoritos.

_Teste no Corel: 4x — pendente. Detalhes: ARCHITECTURE §55._

### 0.1.0-dev.87

**Implementado**

- Cadastro de moldes → “Duplicar peça”: cada grupo de tamanho selecionado ganha uma cópia inteira ao lado (à esquerda, à direita, acima ou abaixo, com a distância em mm entre as peças), com a peça renomeada para o nome da cópia.
- Os logos da cópia ganham números novos, iguais em todos os tamanhos. “Espelhar a cópia” vira só a peça e o laser; TAG e logos ficam como estão.
- Duplicar de novo com o mesmo nome substitui a cópia anterior de cada tamanho (e mantém os números dos logos dela).
- Também pode ir para os Favoritos.

_Teste no Corel: 4w — pendente. Detalhes: ARCHITECTURE §54._

### 0.1.0-dev.86

**Implementado**

- Cadastro de moldes → “Propagar logos”: selecione os grupos de tamanho já montados da mesma peça; os logos (L1, L2…) do grupo com mais logos vão para os outros, na mesma posição em relação ao centro da peça e na escala da altura da peça.
- Logo que o grupo já tem é só movido e redimensionado; logo que falta é copiado para dentro do grupo. Peça, laser, TAG e nomes não mudam.
- Se mais de um grupo tiver todos os logos (ex.: ao propagar de novo), vale o tamanho escolhido no campo Tamanho. Grupos de peças diferentes ou objetos soltos: nada é alterado.
- Também pode ir para os Favoritos.

_Teste no Corel: 4v — pendente. Detalhes: ARCHITECTURE §53._

### 0.1.0-dev.85

**Implementado**

- Cadastro de moldes → “Detectar objetos em lote”: selecione os conjuntos (peça + logos) de vários tamanhos da mesma peça, escolha a categoria e o PX define todos de uma vez, com o nome da peça digitado.
- Cada logo vai para a peça que contém o centro dele. O tamanho de cada conjunto vem da ordem de tamanho das peças: sem grupo já montado na seleção, é preciso um conjunto para cada tamanho da categoria; com um (ex.: o G), os outros seguem a partir dele.
- Os logos de todos os tamanhos ficam com a mesma numeração (pela posição na peça). Qualquer conjunto com problema: nada é alterado.
- Também pode ir para os Favoritos.

_Teste no Corel: 4u — pendente. Detalhes: ARCHITECTURE §52._

### 0.1.0-dev.84

**Implementado**

- Cadastro de moldes → “Definir objetos”: selecione a peça e os logos de um tamanho, digite o nome da peça e escolha o tamanho. O PX monta o grupo do tamanho com a TAG (o tamanho, embaixo da peça), os logos L1, L2…, a peça e a cópia x com o contorno do laser.
- A peça é o maior objeto da seleção. Logos já chamados L<n> mantêm o nome; os outros seguem a numeração de outro tamanho já definido da mesma peça (pela posição na peça) ou ganham o próximo número livre da página.
- Selecionar um grupo já montado o redefine: TAG e x antigos são trocados, logos e identificador de tamanho ficam.
- Configurações de moldes: novo bloco para o laser (criar a cópia, cor, espessura) e a TAG (criar, tamanho da fonte, contorno branco).
- Também pode ir para os Favoritos.

_Teste no Corel: 4t — pendente. Detalhes: ARCHITECTURE §51._

### 0.1.0-dev.83

**Implementado**

- Cadastro de moldes → “Configurações de moldes…”: tamanhos de cada categoria (tamanhos base, prefixos e sufixos, com a lista gerada na hora), código da camada de cada categoria, tamanho copiado para o Grid e os nomes do laser, da TAG e do identificador de tamanho. Restaurar padrão volta aos valores de fábrica. Ficam só nesta máquina.
- Também pode ir para os Favoritos.

**Alterado**

- Criar camadas para a seleção, Converter para maiúsculas e Criar Grid passam a usar essas configurações em vez de valores fixos.

_Teste no Corel: 4s — pendente. Detalhes: ARCHITECTURE §50._

### 0.1.0-dev.82

**Implementado**

- Nova seção “Cadastro de moldes” (fase 1 das ferramentas de cadastro de moldes): Criar camadas para a seleção (grupos de tamanho → camada TIPO.MOLDE.CATEGORIA.PEÇA), Converter peça e camada para maiúsculas, Excluir camadas vazias, Ordenar e distribuir (maior/menor primeiro, vertical/horizontal, distanciamento em mm ou automático), Criar Grid (arquivo novo com o G, BLG e maior infantil de cada peça, uma camada por categoria e os retângulos _RECT) e Selecionar RECT.
- As seis também podem ir para os Favoritos.

_Teste no Corel: 4s — pendente. Detalhes: ARCHITECTURE §49._

### 0.1.0-dev.81

**Implementado**

- Preparação e saída → padrão novo: “Preparar Molde Novo” com camadas CAM1.MOLDE.MA.Frente (objetos com os mesmos nomes, laser continua x) e “Preparar Grid Novo” com uma camada por categoria (CAM1.MOLDE.MA), todas as peças, os logos L1, L2… e um retângulo <peça>_RECT do tamanho de cada peça, no fundo.
- “Converter documento: padrão antigo ↔ novo”: troca o documento aberto de um padrão para o outro (molde: só o nome das camadas; Grid: camadas por peça ↔ por categoria, com os retângulos _RECT). O status diz para qual padrão foi.
- Os três também podem ir para os Favoritos.

**Alterado**

- Os botões de antes ficam sob “Padrão antigo” (nos Favoritos: “Preparar Grid Novo (padrão antigo)” e “Preparar Molde Novo (padrão antigo)”); o que fazem não mudou.
- Preparar Grid Novo aceita também um molde no padrão novo (CAM1.MOLDE.MA.Frente): cada peça continua na sua camada.
- Preparar Grid não renomeia mais os objetos de uma camada no padrão novo (antes todos virariam “MOLDE”).

_Teste no Corel: 4r — pendente. Detalhes: ARCHITECTURE §48._

### 0.1.0-dev.80

**Implementado**

- Montagem → “Resetar ângulo”: deixa cada objeto selecionado com rotação 0° sem mudar a aparência (curva; retângulo e elipse girados viram curva; grupos, inclusive girados). Nome e marcações do objeto são mantidos; se a geometria não ficar igual, o objeto volta a ser como era. Texto e bitmap não são alterados. Também nos Favoritos.

**Alterado**

- Roteiro de capturas: saem as duas capturas do Resetar ângulo (25 itens).

_Teste no Corel: 4q — pendente. Detalhes: ARCHITECTURE §47._

### 0.1.0-dev.79

**Implementado**

- Diagnóstico → “Roteiro de capturas…”: lista as capturas antes/depois que faltam para as ferramentas de montagem, com o que preparar na página e o que fazer na ferramenta original, e marca com ✓ as já salvas.
- Comparação antes/depois: escolha do item do roteiro e botão “Salvar na pasta de capturas” (um arquivo por relatório, nunca sobrescrito); depois de salvar, o roteiro passa para o próximo item pendente.
- “Roteiro de capturas” também pode ser adicionado aos Favoritos.

**Alterado**

- Comparação antes/depois: a mensagem de status fica numa linha própria, acima dos botões.

_Teste no Corel: 4p — pendente. Detalhes: ARCHITECTURE §46._

### 0.1.0-dev.78

**Implementado**

- Exportar/Importar preferências agora levam também a personalização do painel (nomes das seções, seções criadas e a seção de cada ferramenta) e o “Mostrar nomes” dos Favoritos.

**Alterado**

- Importar um arquivo exportado de um painel sem personalização desfaz a personalização atual; um arquivo de versão anterior, sem esses itens, mantém o que já existe.

_Teste no Corel: 4g — pendente. Detalhes: ARCHITECTURE §45.2._

### 0.1.0-dev.77

**Alterado**

- Seções: quando todos os botões de um grupo vão para outra seção, o subtítulo do grupo (ex.: “Tamanho Padrão” no Grid) e a linha que sobra também somem — antes ficava um texto parecendo um botão desativado.
- Personalizar painel: texto mais claro e nítido, títulos “De …” em destaque, ferramentas movidas em azul, linhas alternadas e setas de ordem só nas ferramentas movidas.

_Teste no Corel: ver ROADMAP. Detalhes: ARCHITECTURE §45.1._

### 0.1.0-dev.76

**Implementado**

- Personalizar painel: renomear qualquer seção, criar seções novas (e removê-las) e escolher em que seção cada ferramenta aparece, com a ordem entre as ferramentas movidas para a mesma seção.
- Uma ferramenta movida aparece como botão completo na seção escolhida e continua funcionando igual — inclusive Escudo/Bandeira, que seguem usando o nome do layout e a pasta da seção original.
- A letra do canto nos Favoritos acompanha o nome atual da seção onde a ferramenta está.

**Alterado**

- “Mostrar nomes” nos Favoritos virou uma chave liga/desliga.
- “Restaurar padrão” volta também os nomes, as seções criadas e o lugar de cada ferramenta (ainda é preciso Salvar).

_Teste no Corel: 4k — pendente. Detalhes: ARCHITECTURE §45._

### 0.1.0-dev.75

**Alterado**

- Alternar Mão Livre só liga em textos com o nome NO, NU, AP ou TS. Se algum objeto selecionado tiver outro nome, nada é alterado e o painel avisa. Desligar continua valendo para qualquer objeto marcado.

_Teste no Corel: ver ROADMAP._

### 0.1.0-dev.74

**Alterado**

- Montagem: os botões que ligam e desligam uma marcação agora se chamam “Alternar Esticar no Tamanho do Molde”, “Alternar Ignorar métricas da variável” e “Alternar Mão Livre”, como o “Alternar Tamanho Padrão”. Funcionam igual a ele: ligam quando a seleção não está toda marcada, desligam quando está, e o botão fica destacado com a marcação ativa.

_Teste no Corel: ver ROADMAP._

### 0.1.0-dev.73

**Implementado**

- Favoritos: “Mostrar nomes” liga e desliga a exibição do botão inteiro, com ícone e nome, em vez de só o ícone. A escolha fica salva; dá para arrastar para reordenar nos dois modos.

**Alterado**

- Seções reorganizadas: nova seção Montagem, logo depois do Grid, com Esticar no Tamanho do Molde, Ignorar métricas da variável, Mão Livre, Criar grupo com item único e Alinhamento horizontal automático.
- “Outras ferramentas — Objetos/Tipo de peça/Medidas” viraram só Objetos, Tipo de peça e Medidas. Ordem padrão: Favoritos, Grid, Montagem, Códigos, Camadas, Objetos, Tipo de peça, Medidas, Preparação e saída, Produção, Listas, Diagnóstico.
- Quem já tinha personalizado a ordem das seções mantém a sua; a Montagem aparece logo depois do Grid.
- Marcas do canto dos Favoritos: Mo (Montagem) e Me (Medidas).

_Teste no Corel: 4j — pendente. Detalhes: ARCHITECTURE §44._

### 0.1.0-dev.72

**Alterado**

- Esticar no Tamanho do Molde, Ignorar métricas da variável e Mão Livre agora funcionam como o Tamanho Padrão: o botão liga e desliga a marcação nos objetos selecionados e fica destacado quando ela está ativa na seleção (só a borda quando é mista). Os atalhos dos Favoritos acompanham.
- Desligar remove a marcação do objeto, como o Tamanho Padrão. Também vale para vários objetos selecionados de uma vez.
- Favoritos: cada ferramenta tem agora um ícone próprio — nenhum símbolo se repete. A Mão Livre virou uma mãozinha.
- Favoritos: a letra laranja no canto agora indica a seção da ferramenta — G (Grid), Co (Códigos), Ca (Camadas), PS (Preparação e saída), T (Tipo de peça), O (Objetos), M (Medidas), L (Listas), Pr (Produção), D (Diagnóstico). Remoção continua com o “−” vermelho.

_Teste no Corel: ver ROADMAP. Detalhes: ARCHITECTURE §43._

### 0.1.0-dev.71

**Alterado**

- Favoritos: corrigido o arrastar e soltar, que não funcionava desde a dev.66 — o arraste era cancelado no instante em que começava.

_Teste no Corel: ver ROADMAP. Detalhes: ARCHITECTURE §35.4._

### 0.1.0-dev.70

**Implementado**

- Grid → Ferramentas do SISBolt: “Ativar Esticar no Tamanho do Molde”, “Ativar Ignorar métricas da variável” e “Ativar Mão Livre” gravam a mesma marcação que o SISBolt grava (um objeto por vez; só ativação).
- “Criar grupo com item único”: coloca o objeto selecionado sozinho num grupo novo, na frente da camada e selecionado, como o SISBolt.
- “Alinhamento horizontal automático…”: com dois objetos selecionados, mostra qual será alinhado e qual é a referência (com zoom na referência e troca de papéis) e grava o vínculo do SISBolt.
- As cinco também podem virar favoritos.

_Teste no Corel: 4q — pendente. Detalhes: ARCHITECTURE §43._

### 0.1.0-dev.69

**Alterado**

- Comparação antes/depois: corrigido objeto aparecendo como movido e redimensionado (fator 2,54) quando a unidade usada pelo Corel para medir (Document.Unit) muda entre as capturas. Cada captura registra a unidade da régua e a unidade efetiva das medidas; posição e tamanho são convertidos antes de comparar e mostrados na unidade da régua, com os valores brutos ao lado.
- Se a unidade efetiva não puder ser determinada, a comparação de posição e tamanho aparece como inconclusiva, sem afirmar que algo mudou. Properties e dados do objeto continuam com os valores brutos.

_Teste no Corel: 4p, 4p passo 7 — ver ROADMAP. Detalhes: ARCHITECTURE §42.1._

### 0.1.0-dev.68

**Implementado**

- Diagnóstico: “Capturar estado (antes)” e “Comparar com a captura (depois)” mostram tudo o que uma ferramenta mudou na página ativa — propriedades do documento, da página, das camadas e dos objetos (inclusive em grupos e PowerClips), objetos novos ou removidos, posição, tamanho, rotação e seleção. Só leitura; o relatório pode ser copiado ou salvo.
- Usado para descobrir como as ferramentas do SISBolt marcam os objetos, antes de fazer as versões do PX (roteiro em docs/sisbolt-tools-investigation.md).

_Teste no Corel: ver ROADMAP. Detalhes: ARCHITECTURE §42._

### 0.1.0-dev.67

**Implementado**

- Produção: “Recentes…” lista os últimos 10 contextos de produção carregados nesta máquina. Escolher um lê e valida o arquivo .json de novo, como um carregamento novo. Só os caminhos ficam salvos, nunca o conteúdo do contexto.
- “Contextos de produção recentes” também pode ser adicionado aos Favoritos.

**Alterado**

- O caminho de um contexto carregado (pelo seletor, colado ou pelos recentes) passa a ser lembrado em “Recentes”. Nada é carregado sozinho ao abrir o painel.

_Teste no Corel: 4h, 4h passo 9 — pendente. Detalhes: ARCHITECTURE §41.4._

### 0.1.0-dev.66

**Implementado**

- Favoritos: arraste um ícone com o botão esquerdo para mudar a ordem. Uma barra mostra onde ele vai entrar; soltar fora dos Favoritos cancela. Clique curto continua executando a ferramenta.

_Teste no Corel: 4j, 4j passo 13 — pendente. Detalhes: ARCHITECTURE §35.4._

### 0.1.0-dev.65

**Implementado**

- Produção: “Exportar resultado para o Nexor…” grava um arquivo .json com as saídas Escudo/Bandeira/Faixa salvas nesta sessão com o contexto carregado e a identificação do rolo e do trabalho de impressão. Não altera o Nexor nem o status do trabalho.
- O resumo do contexto na seção Produção mostra quantas saídas foram salvas com ele nesta sessão.
- “Exportar resultado para o Nexor” também pode ser adicionado aos Favoritos.

**Alterado**

- Favoritos: os atalhos de “Remover … da página” (NO, NU, AP, TS, NO/NU e todos) agora aparecem em vermelho, com moldura, para não serem confundidos com os de alternar códigos.
- Limpar o contexto de produção avisa quando há saídas registradas que ainda não foram exportadas.

_Teste no Corel: 4h, 4h passo 8 — pendente. Detalhes: ARCHITECTURE §41.3._

### 0.1.0-dev.64

**Implementado**

- Outras ferramentas — Objetos: botão “Usar contexto de produção” preenche o novo nome dos objetos com o pedido informado do contexto carregado (ou o nome do arquivo de origem). Renomear continua sendo o botão de sempre.
- Produção: botão “Nomear página ativa com o contexto” renomeia a página ativa com o mesmo nome (desfazível com Ctrl+Z).
- Preparação e saída: com um contexto carregado, Escudo/Bandeira/Faixa são salvos numa subpasta com o nome do rolo, criada se não existir. A opção “Salvar na subpasta do rolo” desliga isso; o “Destino” mostra a subpasta.
- Ao salvar Escudo/Bandeira/Faixa com um contexto carregado, a mensagem e o log informam o rolo e o trabalho de impressão.
- Os dois botões novos também podem ser adicionados aos Favoritos.

_Teste no Corel: 4h, 4h passo 7 — pendente. Detalhes: ARCHITECTURE §41.2._

### 0.1.0-dev.63

**Implementado**

- Preparação e saída: botão “Usar contexto de produção” preenche “Nome do layout” com o pedido informado do contexto carregado (ou, sem pedido, com o nome do arquivo de origem), sem caracteres inválidos em nome de arquivo.
- “Usar contexto de produção” também pode ser adicionado aos Favoritos.

_Teste no Corel: 4h — pendente. Detalhes: ARCHITECTURE §41.1._

### 0.1.0-dev.62

**Implementado**

- Produção: resumo do contexto de produção carregado (rolo, trabalho de impressão e, quando o arquivo traz, pedido informado), com “Ver detalhes” e “Limpar contexto”.
- “Ver detalhes” e “Limpar contexto” também podem ser adicionados aos Favoritos.

**Alterado**

- Arquivos do Nexor de trabalhos sem pedido informado (pedidoLabel vazio) agora são aceitos; antes eram recusados.
- Carregar ou colar um contexto novo só substitui o atual quando dá certo — cancelar ou falhar mantém o contexto anterior.
- Carregar um contexto mostra o resumo no painel em vez de abrir a janela de detalhes automaticamente.

_Teste no Corel: 4h — pendente. Detalhes: ARCHITECTURE §41._

### 0.1.0-dev.61

**Implementado**

- Botão “Aplicar contorno do Grid” na seção Grid: aplica o estilo do Grid (preenchimento branco, contorno preto fino) a todos os objetos de todas as páginas, sem mexer nas camadas especiais (Guias, Grade). Um Ctrl+Z desfaz.
- “Aplicar contorno do Grid” também pode ser adicionado aos Favoritos.

**Alterado**

- Testes automatizados não gravam mais no log real nem na configuração real do PX DEV.

_Teste no Corel: 4o — pendente. Detalhes: ARCHITECTURE §40._

### 0.1.0-dev.60

**Implementado**

- “Notas da versão” também pode ser adicionado aos Favoritos (botão direito → Adicionar aos favoritos).

**Alterado**

- O botão “Notas da versão” saiu do rodapé e agora fica no início da seção Diagnóstico.

_Teste no Corel: 4m — pendente. Detalhes: ARCHITECTURE §38.1._

### 0.1.0-dev.59

**Implementado**

- Botão “Notas da versão” no rodapé do painel.
- Janela de consulta das mudanças da versão em execução, separadas em Implementado, Alterado e Removido.

_Teste no Corel: 4m — pendente._

### 0.1.0-dev.58

- Rodada da `0.1.0-dev.58`: pasta de saída padrão por máquina (Preparação e saída); contraste da caixa de filtro da comparação do ListForge Integrado corrigido; testes da janela do ListForge Integrado não gravam mais na configuração real. Smoke test pendente (`docs/mvp-smoke-test.md`, 4n). Ver `docs/ARCHITECTURE.md`, 39.

_Teste no Corel: 4n — pendente._

### 0.1.0-dev.57

- 40ª rodada — dicas legíveis (`0.1.0-dev.57`): a dica dos ícones dos Favoritos tinha texto claro sobre fundo claro (relato do usuário na dev.56). As dicas do painel ganharam template escuro com quebra de linha. Defeito reproduzido e correção conferida com as dicas abertas fora do Corel; smoke test pendente (`docs/mvp-smoke-test.md`, 4j passo 3). Ver `docs/ARCHITECTURE.md`, 35.3.

_Teste no Corel: 4j, 4j passo 3 — pendente._

### 0.1.0-dev.56

- 39ª rodada — todos os botões favoritáveis (`0.1.0-dev.56`): o catálogo de favoritos passou de 10 para 44 ferramentas (todo botão de ferramenta das seções, inclusive Agrupar Interseção, Escudo/Bandeira/Faixa, Tipo de peça, Medidas, Listas, Produção e Diagnóstico), com ícone próprio; Ids da dev.50 inalterados. Testes automatizados e painel carregado fora do Corel; smoke test pendente (`docs/mvp-smoke-test.md`, 4j passo 12). Ver `docs/ARCHITECTURE.md`, 35.2.

_Teste no Corel: 4j, 4j passo 12 — pendente._

### 0.1.0-dev.55

- 38ª rodada — Remover códigos da página por código (`0.1.0-dev.55`): na seção Códigos, além de "Remover NO/NU da página" (mantido), botões NO/NU/AP/TS para remover um código por vez da página ativa e "Remover todos os códigos (NO/NU/AP/TS) da página" — mesma descida em grupos e PowerClips. AP/TS recebem "Texto Apelido"/"Texto Tipo Sanguíneo" (decisão do usuário). A legenda da alternância de códigos também passou a quebrar linha. Testes automatizados e painel carregado fora do Corel; smoke test pendente (`docs/mvp-smoke-test.md`, 4l). Ver `docs/ARCHITECTURE.md`, 37.

_Teste no Corel: 4l — pendente._

### 0.1.0-dev.53

- 37ª rodada — Personalizar painel e favoritos em ícone (`0.1.0-dev.53`): "Personalizar painel…" no cabeçalho escolhe ordem e visibilidade das seções (rascunho até Salvar; Cancelar descarta; "Restaurar layout padrão" não mexe nos favoritos). Seções só reordenadas/ocultadas, sem recriar o docker nem perder o estado dos controles. Layout global por Id estável no mesmo arquivo dos favoritos e nas preferências portáteis; seções novas entram visíveis no fim; importação sem layout mantém o atual. A pedido do usuário, os favoritos viraram ícones quadrados com o nome na dica e mover/remover no botão direito. Verificação: 20 testes novos, suíte completa (128 net8.0 + 425 net48) e renderização/exercício do painel compilado fora do Corel; smoke test pendente (`docs/mvp-smoke-test.md`, 4j e 4k). Ver `docs/ARCHITECTURE.md`, 35.1 e 36. Após a dev.53 (entra na próxima build DEV): a legenda da seção Camadas passa a quebrar linha em docker estreito (era cortada em 300 px); MA/FE/C continuam alinhados. Conferido carregando o painel compilado fora do Corel; não verificado no Corel (`docs/ARCHITECTURE.md`, 36.1).

_Teste no Corel: 4j, 4k — pendente._

### 0.1.0-dev.52

- 36ª rodada — relatório do Diagnóstico contido (`0.1.0-dev.52`): ao terminar, o teste de identidade mostra só um resumo do resultado real; o relatório completo fica em "Ver relatório" (área recolhível, altura limitada, rolagem própria) e "Copiar relatório". Lógica do diagnóstico inalterada. Verificado por testes e renderização fora do Corel (`docs/ARCHITECTURE.md`, 34.1); smoke test pendente (4i passo 0 — Diagnóstico).

_Teste no Corel: ver ROADMAP — pendente._

### 0.1.0-dev.51

- 35ª rodada — Favoritos do painel (`0.1.0-dev.50`, corrigida na `0.1.0-dev.51`): primeira etapa de personalização. Use a dev.51: a dev.50 gravava `"PanelFavoriteToolIds": null` em configurações sem favoritos (inofensivo). - Seção "Favoritos" no topo; adicionar/remover pelo menu de contexto das ferramentas compatíveis; ordenar com ▲/▼. - Cada atalho dispara o clique do botão original e segue o estado e a habilitação dele. - Salvo por Id estável no arquivo de configuração do PX DEV (campo aditivo) e incluído nas preferências portáteis. - Escudo/Bandeira/Faixa de Capitão ficam fora (dependem de campos auxiliares). - Verificação: testes automatizados e renderização fora do Corel; smoke test pendente (`docs/mvp-smoke-test.md`, 4j). - Próximas etapas possíveis: arrastar e soltar (feito na dev.66), mais seções no catálogo, layout/seções personalizáveis (feito na dev.53).

_Teste no Corel: 4j — pendente._

### 0.1.0-dev.50

- 35ª rodada — Favoritos do painel (`0.1.0-dev.50`, corrigida na `0.1.0-dev.51`): primeira etapa de personalização. Use a dev.51: a dev.50 gravava `"PanelFavoriteToolIds": null` em configurações sem favoritos (inofensivo). - Seção "Favoritos" no topo; adicionar/remover pelo menu de contexto das ferramentas compatíveis; ordenar com ▲/▼. - Cada atalho dispara o clique do botão original e segue o estado e a habilitação dele. - Salvo por Id estável no arquivo de configuração do PX DEV (campo aditivo) e incluído nas preferências portáteis. - Escudo/Bandeira/Faixa de Capitão ficam fora (dependem de campos auxiliares). - Verificação: testes automatizados e renderização fora do Corel; smoke test pendente (`docs/mvp-smoke-test.md`, 4j). - Próximas etapas possíveis: arrastar e soltar (feito na dev.66), mais seções no catálogo, layout/seções personalizáveis (feito na dev.53).

_Teste no Corel: 4j — pendente._

### 0.1.0-dev.49

- 34ª rodada — layout do Diagnóstico (`0.1.0-dev.49`): botões em uma coluna, com rótulos curtos e quebra de linha, sem cortes nem espaços vazios. Verificado só por renderização fora do Corel (`docs/ARCHITECTURE.md`, 34).

### 0.1.0-dev.48

- 33ª rodada — diagnóstico de identidade no add-on (`0.1.0-dev.48`): Diagnóstico → "Diagnóstico de identidade de camadas…" verifica, num documento temporário e com o mesmo critério de identidade do serviço, acessos repetidos, renomear/reordenar, excluir/recriar, Desfazer, fechar/reabrir e o descarte do registro; o painel solta o registro ao ser fechado. Também: estado do Tamanho Padrão no próprio botão (sem linha de texto) e "Preparar Grid" com aparência comum. Compilação e testes automatizados reais; diagnóstico e smoke test não executados no Corel (`docs/mvp-smoke-test.md`, 4e passo 6 — Tamanho Padrão — e 4i). Ver `docs/ARCHITECTURE.md`, 32.3 e 33. Validação real recebida: dev.48 no CorelDRAW 27.1.0.129, diagnóstico de identidade executado pelo usuário sem falhas (ocultar/restaurar pelo serviço real, identidade após renomear/reordenar, descarte ao fechar/reabrir). Não aprova o 4i inteiro; fechar/reabrir o docker continua pendente.

_Teste no Corel: 4e passo 6, 4i — pendente._

### 0.1.0-dev.47

- 32ª rodada — correção da dev.46 (`0.1.0-dev.47`): a dev.46 gravava a marcação em `Document.Properties`/`Layer.Properties` (ia para o CDR ao salvar), contrariando o requisito de registro só em memória. Agora o registro é só em memória, identificando documento/camadas pelo objeto COM da sessão (comparação por referência, nunca invocado); fechar o documento invalida, reabrir não recupera, identidade ambígua invalida sem alterar nada. As abstrações de camada/documento não expõem mais `Properties`. Auditoria das marcações da dev.46 em `docs/ARCHITECTURE.md`, 32.1 (dev.45/46 nunca instaladas nesta máquina — instalada: dev.43; nenhuma limpeza automática). Experimento de identidade no Corel real não rodou (`MK_E_UNAVAILABLE` ao conectar de fora); smoke test pendente para dev.45/46/47.

_Teste no Corel: ver ROADMAP — pendente._

### 0.1.0-dev.46

- 31ª rodada — Camadas: "Ocultar camadas dos objetos selecionados" (`0.1.0-dev.45`) e o complemento "Mostrar últimas camadas ocultadas" (`0.1.0-dev.46`) — restaura só as camadas que a última ocultação efetiva do PX mudou, no documento de origem, localizadas por marcação em `Layer.Properties`/`Document.Properties` (nunca índices nem referências COM guardadas). Compilação real, suíte automatizada real (116/116 net8.0 + 364/364 net48) e instalador DEV gerado; smoke test no CorelDRAW pendente para dev.45 e dev.46 (`docs/mvp-smoke-test.md`, 4i). Ver `docs/ARCHITECTURE.md`, seção 32.

_Teste no Corel: 4i — pendente._

### 0.1.0-dev.45

- 31ª rodada — Camadas: "Ocultar camadas dos objetos selecionados" (`0.1.0-dev.45`) e o complemento "Mostrar últimas camadas ocultadas" (`0.1.0-dev.46`) — restaura só as camadas que a última ocultação efetiva do PX mudou, no documento de origem, localizadas por marcação em `Layer.Properties`/`Document.Properties` (nunca índices nem referências COM guardadas). Compilação real, suíte automatizada real (116/116 net8.0 + 364/364 net48) e instalador DEV gerado; smoke test no CorelDRAW pendente para dev.45 e dev.46 (`docs/mvp-smoke-test.md`, 4i). Ver `docs/ARCHITECTURE.md`, seção 32.

_Teste no Corel: 4i — pendente._

### 0.1.0-dev.44

- Isolamento dos testes automatizados (configuração do ListForge e do Nexor, gravação atômica); sem mudança visível no painel. Detalhes: ARCHITECTURE §31.

### Antes da dev.44

As versões dev.1 a dev.43 (núcleo, Grid, Saídas, ListForge Integrado, diagnósticos de Tamanho padrão) estão descritas em texto corrido no `ROADMAP.md`, seção “MVP DEV (esta entrega)” e item 1 de “Depois do Core + Saídas”. Confirmadas no Corel real: dev.15 (ListForge Integrado: digitação e processamento) e dev.43 (Tamanho padrão).
