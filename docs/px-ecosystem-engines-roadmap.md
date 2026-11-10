# PX Tool Box — Roadmap de Evolução do Ecossistema

> Documento complementar ao `roadmap.md` principal do projeto.
>
> Caminho sugerido no repositório:
>
> `docs/roadmaps/px-ecosystem-engines-roadmap.md`

---

## 1. Objetivo deste roadmap

Este documento organiza as frentes futuras do ecossistema PX Tool Box relacionadas a:

- integração completa do ListForge;
- investigação funcional do SISBolt;
- criação do **PX Assembly Engine**;
- criação do **PX Nesting Engine**;
- integração futura com o Nexor;
- geração e aplicação de mapas de impressão;
- automação progressiva do fluxo de produção.

O objetivo é evoluir o PX Tool Box sem misturar responsabilidades entre os produtos.

---

# 2. Princípios arquiteturais do ecossistema

## Nexor

Responsável por:

- pedidos;
- produção;
- filas;
- estados;
- materiais;
- rolos;
- retalhos;
- consumo;
- histórico operacional.

Pergunta que o Nexor responde:

> O que precisa ser produzido, com quais recursos, e em que estado está?

---

## ListForge

Responsável por:

- leitura de listas;
- interpretação;
- normalização;
- validação;
- processamento;
- geração dos dados estruturados usados pelo fluxo.

Pergunta que o ListForge responde:

> Quais dados de pessoas, peças, tamanhos e personalizações existem no pedido?

---

## PX Tool Box

Responsável por:

- execução dentro do CorelDRAW;
- manipulação de objetos;
- camadas;
- preparação;
- montagem;
- Grid;
- saída;
- integração visual com os motores futuros.

Pergunta que o PX responde:

> O que precisa ser feito no documento do Corel?

---

## PX Assembly Engine

Responsável por:

- transformar dados estruturados em peças/moldes montados;
- escolher moldes;
- duplicar peças;
- preencher personalizações;
- respeitar regras e metadados dos objetos;
- gerar um plano de montagem.

Pergunta que o Assembly Engine responde:

> A partir destes dados e destes moldes, quais peças precisam ser criadas e quais regras se aplicam a cada uma?

---

## PX Nesting Engine

Responsável por:

- organizar peças fisicamente;
- respeitar regras geométricas;
- escolher posições;
- escolher rotações;
- distribuir peças entre áreas disponíveis;
- reduzir desperdício;
- futuramente escolher entre retalhos e rolos disponíveis.

Pergunta que o Nesting Engine responde:

> Como distribuir estas peças nas áreas disponíveis respeitando as regras de produção e minimizando o custo operacional?

---

# 3. Visão de fluxo futuro

```text
NEXOR
Pedido / contexto
    ↓
LISTFORGE
Interpretação e normalização
    ↓
PX ASSEMBLY ENGINE
Moldes + peças + personalização
    ↓
PX NESTING ENGINE
Distribuição no tecido
    ↓
PX TOOL BOX / COREL
Aplicação, revisão e saída
    ↓
NEXOR
Produção / consumo / status
```

Cada produto deve continuar utilizável de forma independente.

---

# 4. Roadmap de curto prazo

## 4.1 PX Tool Box Core + Saídas

Estado atual prioritário.

Objetivos:

- estabilizar o docker;
- consolidar as ferramentas mais usadas;
- corrigir problemas de Grid;
- concluir ferramentas de medidas;
- revisar Configura Molde Novo;
- revisar Preparar Grid Novo;
- melhorar estados, validação e contraste da interface;
- manter compatibilidade com CorelDRAW 2026 / v27.

---

## 4.2 ListForge Integrado — modo Clássico

Objetivos:

- preservar digitação e foco dentro do Corel;
- alcançar equivalência funcional com o ListForge standalone;
- manter processamento compartilhado;
- preservar standalone independente;
- não duplicar regras;
- manter suporte ao formato atual SISBolt/JSON.

Enquanto houver funções faltantes:

> Identificar como “Clássico — em desenvolvimento”.

---

## 4.3 ListForge Integrado — modo PX

Somente após o modo Clássico estar estável.

Objetivos:

- criar uma experiência visual própria do PX;
- usar o mesmo núcleo de processamento;
- manter compatibilidade de resultados;
- permitir seleção do modo preferido no perfil do usuário;
- não manter duas implementações de regras.

---

# 5. PX Assembly Engine

## 5.1 Objetivo

Criar um motor próprio de montagem de pedidos inspirado nas funções úteis do SISBolt, sem copiar sua implementação interna.

O Assembly Engine deverá:

- receber os dados estruturados do ListForge;
- localizar os moldes corretos;
- duplicar as peças necessárias;
- preencher os dados personalizados;
- aplicar regras específicas dos moldes;
- preparar uma montagem utilizável pelo PX e pelo Nesting Engine.

---

## 5.2 Engenharia reversa funcional do SISBolt

Antes de implementar o motor completo, mapear o comportamento real do SISBolt.

Para cada função relevante:

```text
Entrada
↓
Configuração
↓
Resultado
```

Investigar:

- estrutura dos moldes;
- convenções de camadas;
- convenções de objetos;
- tamanhos;
- tipos de peças;
- MA;
- FE;
- Infantil;
- duplicação;
- personalização;
- graduação;
- regras especiais;
- opções persistentes;
- comportamento de cópia entre documentos.

Não presumir como o SISBolt faz internamente.

Registrar somente o comportamento observado.

---

## 5.3 Inspector de metadados do Corel

Criar ferramenta somente leitura para investigar objetos preparados pelo SISBolt.

Comparações sugeridas:

```text
A = objeto original
B = mesmo objeto com opção ativada
C = objeto copiado para outro arquivo
D = objeto após desativar a opção
```

Inspecionar, quando disponível:

- Shape.Name;
- Shape.Type;
- Shape.Properties;
- Shape.ObjectData;
- identificadores;
- atributos persistentes;
- demais metadados relevantes.

Não modificar o objeto durante a inspeção.

---

## 5.4 Investigação da opção “Tamanho padrão”

Comportamento já observado:

> Um objeto marcado como “Tamanho padrão” mantém o mesmo tamanho em todas as graduações, de PP até XXGG.

Outro comportamento observado:

> Ao copiar esse objeto para outro arquivo, a característica continua acompanhando o objeto.

Investigar:

- onde a propriedade é armazenada;
- como é identificada;
- se acompanha copiar/colar;
- se acompanha importação;
- se sobrevive ao salvamento;
- se sobrevive ao fechamento/reabertura;
- como é desativada;
- como interfere na graduação.

Não depender dos identificadores internos do SISBolt no produto final.

---

## 5.5 Sistema próprio de metadados PX

Após entender os comportamentos necessários, criar um sistema próprio.

Modelo conceitual:

```text
PX Object Metadata

StandardSize
ScaleRule
PieceRole
AllowRotation
AllowMirror
NestingPriority
GroupId
AssemblyRule
```

Exemplo:

```text
StandardSize = true
ScaleRule = Fixed
AllowRotation = true
AllowMirror = false
```

Essas propriedades não devem depender do nome visível do objeto.

---

## 5.6 Biblioteca de moldes

O Assembly Engine deverá conhecer os moldes disponíveis.

Exemplo conceitual:

```text
CAM1.P.MA
CAM1.M.MA
CAM1.G.MA

CAM1.BLP.FE
CAM1.BLM.FE
CAM1.BLG.FE

CAM1.2A.C
CAM1.4A.C
CAM1.6A.C
```

Cada referência poderá possuir:

- tipo;
- tamanho;
- categoria;
- versão;
- variante;
- regras de graduação;
- metadados.

---

## 5.7 Contrato de entrada

O Assembly Engine não interpretará listas brutas.

Ele deverá receber dados já normalizados pelo ListForge.

Exemplo:

```json
{
  "project": "Projeto",
  "order_number": 1234,
  "orders": [
    {
      "Name": "SAMUEL",
      "Nickname": "",
      "Number": "07",
      "BloodType": "",
      "Gender": "MA",
      "ShortSleeve": "1-P-MA",
      "LongSleeve": "",
      "Short": "",
      "Pants": "",
      "Tanktop": "",
      "Vest": ""
    }
  ]
}
```

---

## 5.8 Resolução das peças

Exemplo:

```text
SAMUEL
CAM1
P
MA
Quantidade 1
Número 07
```

Resolver para:

```text
MoldReference:
CAM1.P.MA

Personalization:
NO = SAMUEL
NU = 07
```

---

## 5.9 Montagem automática

O motor deverá ser capaz de:

- localizar o molde-base;
- duplicar o molde;
- preencher textos;
- manter objetos estruturais;
- respeitar objetos com tamanho fixo;
- aplicar regras de escala;
- preservar propriedades relevantes;
- produzir uma estrutura coerente para o Corel.

---

## 5.10 Preview antes da aplicação

Antes de alterar o documento:

```text
Pedido 1234

18 atletas
32 peças

CAM1: 18
CAM2: 8
SHO1: 6

Erros: 0
Avisos: 2
```

Ações:

```text
[ Revisar ]
[ Gerar montagem ]
```

Nenhuma alteração definitiva deve ocorrer antes da confirmação.

---

## 5.11 Arquitetura prevista

```text
PXToolBox.Assembly.Core
PXToolBox.Assembly.Core.Tests
PXToolBox.Modules.Assembly
PXToolBox.CorelAddin
```

O núcleo não deve conhecer diretamente:

- Corel COM;
- WPF;
- banco do Nexor;
- interface do ListForge;
- caminhos locais.

---

# 6. PX Nesting Engine

## 6.1 Objetivo

Criar um motor próprio de nesting 2D para organizar peças de uniformes e reduzir desperdício de tecido.

Não depender do eCut.

O motor deverá futuramente usar os materiais reais cadastrados no Nexor.

---

## 6.2 Primeiro protótipo

Entrada:

- uma área retangular;
- contornos das peças;
- quantidade;
- espaçamento;
- rotações permitidas;
- espelhamento permitido ou não.

Saída:

- X;
- Y;
- rotação;
- área ocupada;
- desperdício;
- aproveitamento.

Primeiro objetivo:

> Posicionar um conjunto simples de peças dentro de uma única área sem sobreposição.

---

## 6.3 Arquitetura inicial

```text
PXToolBox.Nesting.Core
PXToolBox.Nesting.Core.Tests
PXToolBox.Modules.Nesting
```

O núcleo não deve depender de:

- Corel;
- Nexor;
- WPF;
- banco de dados;
- licenciamento;
- caminhos locais.

---

## 6.4 Contorno real

O motor não deverá trabalhar apenas com bounding boxes.

Objetivo:

```text
NÃO:

┌───────────────┐
│     peça      │
└───────────────┘

SIM:

      /\
     /  \
    /    \
   |      |
    \____/
```

Isso permitirá aproveitar espaços internos entre peças.

---

## 6.5 Biblioteca geométrica

O algoritmo de nesting será nosso.

Entretanto, operações geométricas de baixo nível podem usar uma biblioteca robusta para:

- união;
- interseção;
- diferença;
- offset;
- colisão;
- Minkowski.

Não reinventar geometria booleana sem necessidade.

---

## 6.6 Regras por peça

Modelo conceitual:

```text
NestingPiece

Contour
Quantity
AllowedRotations
CanMirror
Spacing
Bleed
Fabric
Priority
Group
```

Exemplo:

```text
Frente

Rotação:
0°
180°

Espelhar:
não

Espaçamento:
5 mm
```

O motor não poderá buscar melhor aproveitamento violando regras de produção.

---

## 6.7 Estratégia de otimização

Evolução progressiva:

```text
heurística simples
↓
melhoria iterativa
↓
algoritmo genético / simulated annealing
↓
otimizações especializadas
```

Não começar pelo algoritmo mais complexo.

---

## 6.8 Múltiplos pedaços de tecido

Evoluir de:

```text
Peças
↓
1 área
```

para:

```text
                 ┌─ Retalho A
                 ├─ Retalho B
Peças → Motor ───├─ Retalho C
                 └─ Rolo aberto
```

O motor deverá decidir:

- quais materiais usar;
- quais peças vão em cada material;
- quando vale usar retalhos;
- quando vale continuar em rolo;
- quanto de rolo consumir.

---

## 6.9 Função de custo

Não otimizar apenas percentual de área.

Modelo conceitual:

```text
Score =
    desperdício
  + quantidade de pedaços utilizados
  + comprimento de rolo consumido
  + quantidade de mapas gerados
  + penalidades de produção
```

O objetivo é buscar a melhor solução operacional, não somente matemática.

---

# 7. Integração com Nexor

## 7.1 Materiais disponíveis

Exemplo:

```text
Dryfit

Retalho R-0182
180 × 94 cm

Retalho R-0207
180 × 143 cm

Retalho R-0255
180 × 62 cm

Rolo R-0031
180 × 1280 cm
```

Inicialmente, os retalhos poderão ser tratados como:

```text
largura × comprimento
```

Retalhos irregulares ficam para uma fase posterior.

---

## 7.2 Fluxo de materiais

```text
Nexor
↓
materiais disponíveis
↓
Nesting Engine
↓
mapas propostos
↓
usuário aprova
↓
Nexor reserva/consome material
```

O Nesting Engine não deve alterar estoque diretamente.

O Nexor continua responsável pela transação operacional.

---

# 8. Integração com PX Tool Box / CorelDRAW

## Assembly

```text
Assembly Engine
↓
plano de montagem
↓
PX
↓
Corel
```

---

## Nesting

```text
Corel / PX
↓
extração dos contornos
↓
Nesting Engine
↓
solução
↓
PX aplica posições e rotações
```

---

## Interface futura

```text
ENCAIXE DE IMPRESSÃO

Tecido:
Dryfit

Largura:
180 cm

Espaçamento:
0,5 cm

☑ Usar retalhos do Nexor
☑ Usar rolo aberto
☐ Permitir abrir novo rolo

Otimização:
○ Rápida
● Balanceada
○ Máxima

[ Calcular mapas ]
```

---

# 9. Resultado esperado do Nesting

Exemplo:

```text
3 MAPAS PROPOSTOS

Mapa 1
Retalho R-0207
Aproveitamento: 92,8%

Mapa 2
Retalho R-0182
Aproveitamento: 89,4%

Mapa 3
Rolo R-0031
Comprimento utilizado: 42,6 cm
```

Ações:

```text
[ Visualizar ]
[ Recalcular ]
[ Aplicar no Corel ]
```

O cálculo não deve alterar as peças originais.

---

# 10. Retalhos irregulares

Fase futura.

Quando necessário, o Nexor poderá armazenar também o contorno real do material.

Isso permitirá:

- retalhos não retangulares;
- áreas inutilizáveis;
- defeitos localizados;
- regiões bloqueadas.

Esta fase só deve começar após o nesting retangular estar estável.

---

# 11. Ordem geral sugerida

```text
AGORA
PX Tool Box Core + Saídas
        ↓
ListForge Integrado
        ↓
Modo Clássico completo
        ↓
Modo PX do ListForge
        ↓
Investigação funcional do SISBolt
        ↓
Inspector de metadados Corel
        ↓
PX Assembly Engine — protótipo
        ↓
Montagem automática de pedido
        ↓
PX Nesting Engine — protótipo
        ↓
Nesting com contorno real
        ↓
Nesting multi-retalho
        ↓
Integração Nexor
        ↓
Assembly + Nesting + Corel
        ↓
Fluxo completo de produção
```

---

# 12. Fluxo completo futuro

```text
NEXOR
Pedido
  ↓
LISTFORGE
Lista validada e normalizada
  ↓
PX ASSEMBLY ENGINE
Moldes + personalização + montagem
  ↓
PX NESTING ENGINE
Retalhos + rolos + otimização
  ↓
PX TOOL BOX
Aplicação no Corel
  ↓
Grid / saída
  ↓
Produção
  ↓
NEXOR
Consumo + status + histórico
```

---

# 13. Regras arquiteturais obrigatórias

## Não criar dependência circular

Evitar:

```text
Nexor → PX → ListForge → Nexor
```

Os produtos devem conversar através de contratos.

---

## Não duplicar regras

Exemplo:

- ListForge interpreta lista;
- Assembly Engine não reinterpreta lista;
- Nesting Engine não decide tamanho de peça;
- Nexor não implementa nesting;
- PX não recria a lógica do ListForge.

---

## Independência

Cada produto deve continuar funcionando isoladamente.

---

## Core independente de UI

Tanto:

```text
PXToolBox.Assembly.Core
```

quanto:

```text
PXToolBox.Nesting.Core
```

devem ser testáveis sem abrir o Corel.

---

# 14. Futuras possibilidades

Depois das fases principais:

- comparação automática de múltiplos mapas;
- estimativa de economia;
- priorização de retalhos antigos;
- regras por tecido;
- bloqueio de rotação por sentido da malha;
- áreas defeituosas em tecido;
- planejamento de múltiplos pedidos juntos;
- nesting em lote;
- aproveitamento de sobra entre pedidos;
- registro automático do consumo no Nexor;
- comparação planejado × realmente impresso;
- métricas históricas de aproveitamento.

---

# 15. Critério de sucesso

O objetivo de longo prazo é chegar ao seguinte cenário:

> Um pedido entra no Nexor, a lista é interpretada pelo ListForge, o Assembly Engine gera as peças corretas, o Nesting Engine escolhe como distribuí-las entre retalhos e rolos disponíveis, o PX aplica o resultado no Corel, e o Nexor recebe de volta o consumo e o estado da produção.

O operador continua podendo revisar e intervir em cada etapa.

A automação deve reduzir trabalho repetitivo sem eliminar controle operacional.

---

# 16. Sugestão para registrar este documento

Branch sugerida:

```text
docs/ecosystem-engines-roadmap
```

Commit sugerido:

```text
docs: add assembly and nesting engine roadmap
```
