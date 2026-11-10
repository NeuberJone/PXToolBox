# Controles de Tamanho Padrão — desenho

## Intenção

Adicionar à seção Grid do PX Tool Box DEV os comandos **Alternar Tamanho Padrão** e
**Atualizar Tamanho Padrão**, gravando o contrato que o SISBolt usa em `Shape.Properties`.
Os comandos operam somente nos Shapes selecionados, preservam todos os demais atributos e
usam o agrupamento de comando existente para que cada clique seja um único Undo.

## Evidência do contrato SISBolt

Foi feito o teste controlado solicitado com um retângulo não quadrado de 10 × 20 cm marcado
como Tamanho Padrão. O inspector mostrou:

- `KEEP_DESIGN_SIZE`, ID `0`, Boolean `True`;
- `KEEP_DESIGN_SIZE`, ID `1`, Double `10`.

Assim, nesta versão, o ID `1` representa a **largura atual** do objeto, na unidade de medida
usada pelo CorelDRAW. Não será usado `ObjectData`, `Shape.Name` ou propriedades PX adicionais.

## Escopo e não-escopo

- Incluir os dois botões juntos na seção Grid, com os tooltips fornecidos.
- Criar um serviço separado para consultar, definir, atualizar e remover `KEEP_DESIGN_SIZE`.
- Aplicar individualmente a cada Shape selecionado, sem recursão em grupos.
- Alternar remove todos quando todos possuem a propriedade; caso contrário, define/atualiza
  todos.
- Atualizar nunca remove: cria quando ausente e força ID 0 para `True` quando presente.
- Seleção vazia retorna `Selecione um ou mais objetos.`.
- Não alterar algoritmo de Grid, `ALIGNMENT_PROPS`, outras propriedades SISBolt, posição,
  tamanho geométrico, rotação, nome, camada, ordem ou seleção.
- O inspector existente permanece somente leitura e inalterado.

## Abordagens consideradas

1. **Recomendada — abstração mínima em `ICorelShape`**: expor operações de propriedade no
   contrato compartilhado, implementar a tradução COM somente em `CorelShapeAdapter` e manter
   a regra em `StandardSizeService`. Permite testes unitários com fakes e mantém a UI fina.
2. **Serviço ligado diretamente ao COM**: menor superfície de abstração, mas mistura regra
   SISBolt ao host e impede testes unitários do serviço sem CorelDRAW.
3. **Adaptador de propriedades separado**: flexível para muitos namespaces futuros, porém
   desnecessário para este recurso e maior que o padrão atual da solução.

A abordagem 1 será usada.

## Decisões de implementação

- O nome é exatamente `KEEP_DESIGN_SIZE`; ID 0 é Boolean e ID 1 é Double.
- A leitura de presença procura a entrada pelo par nome/ID, sem confundir propriedades com
  nome vazio ou outros IDs.
- Definir/atualizar escreve ID 0 como `true` e ID 1 como `shape.Width` sem redimensionar o
  Shape.
- Remover usa a exclusão física da entrada compatível com o SISBolt, não `False`.
- Falhas de leitura/escrita retornam erro pelo `OperationResult`; a UI apenas mostra o status.
- `ControlUI.RunWithWorkspace` já abre um `BeginCommandGroup(commandName)` e fecha o grupo em
  `finally`; os handlers usarão esse caminho com os nomes de Undo apropriados.

## Validação

Os testes unitários cobrirão criação, atualização, remoção física, seleção vazia, múltiplos
Shapes, estados mistos, preservação de largura/altura e ausência de recursão em grupos. A
validação real no CorelDRAW deverá confirmar no inspector: ausência inicial, ON com os três
campos esperados, atualização do ID 1 após redimensionar e OFF sem a propriedade; também será
confirmada a cópia para outro documento.

