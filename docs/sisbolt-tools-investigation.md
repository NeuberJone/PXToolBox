# Ferramentas do SISBolt — versão PX que o SISBolt reconheça

Pedido do usuário (2026-11-06): ter no PX a sua versão das ferramentas integradas hoje no
SISBolt (print em `Referência/Captura de tela 2026-11-06 090251.png`), de forma que o SISBolt
reconheça o resultado.

## Método

O SISBolt é um produto comercial de terceiros, com licença por máquina. O contrato de cada
ferramenta é descoberto **observando o que ela muda no documento**, nunca descompilando o
`SISBolt.dll` (mesma regra de `docs/px-ecosystem-engines-roadmap.md`, 5.2: "registrar somente
o comportamento observado"). Foi assim que saiu o Tamanho Padrão (`KEEP_DESIGN_SIZE`, seção
"Tamanho Padrão" do `docs/ARCHITECTURE.md`).

Ferramenta do PX para isso (dev.68): Diagnóstico → **Capturar estado (antes)** e **Comparar com
a captura (depois)**. A captura lê, só para leitura, a página ativa inteira: `Properties` do
documento, da página, das camadas e de cada objeto (inclusive dentro de grupos e PowerClips),
nome, tipo, posição, tamanho, rotação, grupo/PowerClip, ordem, seleção, visibilidade, bloqueio,
texto e dados do objeto (ObjectData). A comparação lista o que mudou.

Unidades (dev.69): cada captura registra a unidade da régua (a da interface, e a do
`KEEP_DESIGN_SIZE`) e a unidade efetiva das medidas (`Document.Unit`). Posição e tamanho são
convertidos antes de comparar e mostrados na unidade da régua; se a unidade efetiva não for
conhecida, a parte geométrica sai como "inconclusiva". Properties e ObjectData vêm sempre brutos.

## Roteiro para cada ferramenta

Desde a dev.79 o roteiro está no próprio PX: Diagnóstico → **Roteiro de capturas…**
(`Diagnostics/CaptureChecklist.cs`, um item por captura pendente da tabela abaixo) e, na
comparação, **Salvar na pasta de capturas** (`%LocalAppData%\PXToolBox\Dev\capturas`). Os
passos abaixo continuam valendo para qualquer captura fora do roteiro.

1. Documento novo, pequeno, só com os objetos do teste (indicados na tabela).
2. Selecione os objetos como a ferramenta exige.
3. PX → Diagnóstico → **Capturar estado (antes)**.
4. Use a ferramenta do SISBolt (anote as opções escolhidas na janela dela, se houver).
5. PX → **Comparar com a captura (depois)** → escreva o nome da ferramenta e as opções →
   **Copiar relatório** (ou Salvar como .txt) e mande.
6. Quando a ferramenta tem estado ligado/desligado ou opções, repita com a outra opção (ex.:
   desligar, outro alinhamento) — cada variação é um relatório.

## Ferramentas

Estado em 2026-11-06 (dev.70). As observações abaixo vêm das capturas do usuário, descritas
nos pedidos de implementação; os relatórios em si não estão no repositório.

| # | Ferramenta no SISBolt | Observado | No PX (dev.70) | Captura pendente |
|---|---|---|---|---|
| — | Tamanho Padrão | `KEEP_DESIGN_SIZE` [0] Boolean True, [1] largura, [2] altura (unidade da régua) | Grid → Alternar/Atualizar Tamanho Padrão | — |
| 1 | Esticar no Tamanho do Molde | retângulo simples: `STRETCH_PROPS` [0] = Boolean True; geometria não muda na hora | **Alternar** como o Tamanho Padrão (seleção; desligar remove a entrada) | desativação no SISBolt (remove ou grava False?) — confirmar que bate com o PX |
| 2 | Resetar ângulo | retângulo sem rotação: substituído por uma **curva** com StaticID novo, rotação 0°, selecionada, ordem 1 na camada, sem Properties | **Montagem → Resetar ângulo** (dev.80): mecanismo do "Zerar ângulo" do MoldMotor, preservando nome e Properties (decisão do usuário) | — (falta só o reconhecimento na montagem, 4q passo 9) |
| 3 | Criar grupo com item único | retângulo: grupo novo na mesma camada, original dentro com o mesmo StaticID e geometria, grupo na frente e selecionado, sem Properties; nome `--- RENOMEAR TAMANHO ---` | **Criar** (um objeto fora de grupos), nome **provisório** | objeto de outro nome (o nome se repete?); se nomear o grupo já preenche `ObjectData["Name"]` |
| 4 | Ignorar métricas para variável selecionada | texto NO: `IGNORE_SISBOLT_VARIABLE_METRICS` [0] = Boolean True | **Alternar** (só textos; desligar remove a entrada) | desativação no SISBolt; outros tipos de variável; seleção múltipla no SISBolt |
| 5 | Alinhamento | âncora superior esquerda, margens em branco: `ALIGNMENT_PROPS` [0] = Integer 0, [1]–[4] = strings vazias; sem mudança geométrica | **não implementado** | quatro margens distintas (campo de cada ID e formato); as outras 8 âncoras com margens em branco (códigos, sem deduzir a ordem); "Limpar" (só formulário ou propriedades?) e "Aplicar" depois de limpar |
| 6 | Trocar de Posição | primeira captura compatível com troca pelos centros em X | **não implementado** | objetos de alturas diferentes (referência em Y) |
| 7 | Replicar PowerClip | dois textos novos equivalentes nos destinos; um destino ganhou PowerClip novo, no outro o filho anterior foi substituído; filhos novos nas mesmas coordenadas | **não implementado** | CDR descartável com origem (texto fora do centro), destino vazio e destino com conteúdo; ordem de seleção; posição na página × deslocamento no contêiner ("Editar PowerClip"); origem preservada?; conteúdo anterior descartado ou deslocado? |
| 8 | Formato de exportação de imagens | — | **não implementado** | qualquer captura |
| 9 | Alinhamento horizontal automático | referência sem identificador: GUID novo (string com hífens) em `GUID_IDENTIFIER_PROPS` [0] da referência e o mesmo em `ALIGNMENT_PROPS` [5] do objeto a alinhar | **Janela + Salvar** (dois objetos; referência sem GUID) | troca de referência; referência que já tem GUID (reutiliza?) — no PX, recusado sem alterar nada |
| 10 | Mão Livre | texto NO: `FREE_HAND_PROPS` [0] = Boolean True | **Alternar** (só textos com nome NO, NU, AP ou TS — regra do usuário; desligar remove a entrada) | desativação no SISBolt |

### Resetar ângulo — o que já se sabe

O PX não tem ferramenta de zerar ângulo (o "Zerar ângulos" do MoldMotor é de outro produto,
independente — `AGENTS.md`). A única captura é de um retângulo **sem rotação**, e mesmo assim o
SISBolt trocou o objeto por uma curva nova (StaticID novo, sem Properties). Sem a captura do
objeto girado não dá para saber se a aparência é mantida (desfaz a rotação em volta do centro?
"assa" a rotação na curva?) nem se posição e dimensões mudam; e sem a captura de um objeto
marcado, implementar a substituição poderia apagar `KEEP_DESIGN_SIZE`/`STRETCH_PROPS`. Por isso
nada foi implementado.

### Alinhamento horizontal automático — vínculo

Registrado à parte do Alinhamento (âncora/margens), que usa `ALIGNMENT_PROPS` [0]–[4]: o vínculo
usa só o ID 5 no objeto a alinhar e `GUID_IDENTIFIER_PROPS` [0] na referência. O PX grava só o
vínculo e preserva [0]–[4]. Ainda não se sabe se o SISBolt exige [0]–[4] no objeto a alinhar
para usar o vínculo — a montagem com pares equivalentes (PX × SISBolt) responde.

## Validação de cada ferramenta no Corel (duas etapas, registradas separadamente)

1. **Propriedades**: marcar um objeto pelo PX e um equivalente pelo SISBolt; comparar com o
   inspetor (ou "Capturar estado"/"Comparar") — mesmo nome, ID, tipo e valor.
2. **Reconhecimento na montagem**: rodar a mesma montagem do SISBolt com os dois objetos e
   comparar o resultado. Só esta etapa mostra que o SISBolt reconhece a marcação do PX.

Roteiro: `docs/mvp-smoke-test.md`, 4q.
