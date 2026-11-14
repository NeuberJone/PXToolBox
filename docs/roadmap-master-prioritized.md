# Roadmap mestre priorizado — PXToolBox e PX Ecosystem

**Atualização:** 2026-11-13 · **Natureza:** planejamento consolidado e ordem de execução.  
**Fonte de status:** [ROADMAP.md](../ROADMAP.md), [CHANGELOG.md](../CHANGELOG.md), [reference-feature-roadmap.md](reference-feature-roadmap.md), [px-ecosystem-engines-roadmap.md](px-ecosystem-engines-roadmap.md) e [ecosystem-evolution-proposals.md](ecosystem-evolution-proposals.md).

> Este documento **não substitui o histórico** do ROADMAP nem o CHANGELOG. Estado inferido dos documentos do repositório; não houve compilação, execução de testes ou auditoria integral de código nesta atualização. **[x] = implementação documentada; [~] = parcial/pendente de aceitação; [ ] = pendente.** A existência de código não comprova aprovação no Corel real.

## Visão executiva — ordem recomendada

| Ordem | Prioridade | Entrega | Condição de saída |
|---|---|---|---|
| 0 | P0 | Congelar baseline, compatibilidade Corel 28, testes e smoke | Build/instalador íntegros; fluxos críticos e Undo aprovados |
| 1 | P0 | Consolidar Nesting e Slice existentes | Prévia e aplicação corretas em casos reais, sem regressão |
| 2 | P1 | Rotação persistente por objeto e montagem mista | Objetos 0°, 180° e 90° no mesmo mapa; herança e persistência comprovadas |
| 3 | P1 | Arquitetura modular incremental | Dependências mapeadas, extrações justificadas, build e testes preservados |
| 4 | P1 | Contratos de produção e PX Validator MVP | Contratos versionados e erros/avisos verificáveis |
| 5 | P1 | Montagem/personalização ponta a ponta | Lista → moldes → peças → prévia, com intervenção humana |
| 6 | P1 | Nexor: tecidos, retalhos e feedback | Mapas com materiais reais e rastreio planejado × impresso |
| 7 | P2 | UX, perfis, SISBolt complementar e produtividade | Fluxos e perfis aprovados por usuários |
| 8 | P2 | Licenciamento, edições e release comercial | DEV/produção coexistentes, instalador e licenças validados |
| 9 | P3 | PX Ecosystem Manager | Instalar/atualizar/reparar/remover sem afetar apps independentes |
| 10 | P3 | Nuvem opcional, OCR, otimização avançada | Valor e custo demonstrados antes de iniciar |

## 0 — P0: baseline e compatibilidade

- [x] Núcleo Core/Outputs desacoplado de COM e host WPF/COM DEV documentados.
- [x] Instalador DEV e scripts de build/release/Stream Deck documentados.
- [x] Ferramentas de Saídas migradas e infraestrutura de testes automatizados documentadas.
- [~] Verificar matriz de compatibilidade CorelDRAW (documentos ainda mencionam v27; ambiente informado pelo usuário é v28). Não inferir versão pelo número 9 de APIs/registro.
- [~] Executar build limpo, testes net8/net48, pacote DEV, instalação, inicialização e regressão no CorelDRAW 28.
- [~] Revisar travamentos intermitentes net48, repaint/Optimization de dockers e erros de COM.
- [~] Completar smoke tests reais de Saídas, Moldes, Grids, propriedades, ListForge, Nesting e Slice; registrar evidência por versão.
- [ ] Automatizar validação de referências, caminhos relativos, GUIDs, dependências do host e artefatos do instalador.
- [ ] Estabelecer backup/rollback, Undo seguro e arquivos de teste representativos antes de refatorações estruturais.

**Gate:** nenhuma refatoração grande antes de existir baseline reproduzível.

## 1 — P0: fechar Nesting e Slice existentes

### Nesting — entregas já documentadas

- [x] Encaixe retangular inicial e por contorno real; quantidade por item.
- [x] Múltiplas áreas digitadas; tentativa de melhor ordem; tempo limite/cancelamento.
- [x] Perfis, exportação/importação, ordem de preenchimento, canto inicial e comparação com arranjo original.
- [x] Modo rolo/folhas, organização por linhas, blocos e camadas.
- [~] Distância entre blocos e legendas (dev.119): smoke real pendente.
- [~] Verificar casos extremos: grupos, camadas ocultas, objetos complexos, rotação, sobreposição, margem, contorno e Undo.
- [ ] Definir métricas repetíveis de aproveitamento, tempo de cálculo e viabilidade da montagem.

### Slice — entregas já documentadas

- [x] Planejamento/prévia e divisão real via PowerClip; guias, guias inclinadas e linhas desenhadas.
- [x] Prévia colorida documentada; testes reais de cortes desenhados e cores aprovados na dev.118.
- [~] Validar furos, contornos complexos, imagens grandes, curvas, preservação da arte e Undo em documentos reais.
- [ ] Medir desempenho com arquivos pesados e definir limites/erros recuperáveis.

**Gate:** smoke real registrado; prévia fiel à execução; nenhuma alteração inesperada de objetos originais.

## 2 — P1: Nesting com propriedades persistentes por objeto

- [ ] Com objetos selecionados, definir **Herdar do tecido**, **Fixo 0°**, **0°/180°**, **0°/90°/180°/270°**; ângulos personalizados somente se o solver suportar.
- [ ] Aplicar a múltiplos objetos; consultar seleção com valores mistos; remover override para restaurar herança.
- [ ] Gravar em propriedades persistentes **namespaced** do objeto, reutilizando infraestrutura de Tamanho Padrão e Mão Livre, sem sobrescrever metadados SISBolt.
- [ ] Manter Tamanho Padrão, Mão Livre e rotação independentes e simultâneos.
- [ ] Regra: **override do objeto → padrão do tecido → padrão geral**, com **restrições físicas obrigatórias do tecido** aplicadas por interseção; conjunto vazio gera erro.
- [ ] Definir referencial dos ângulos (orientação original vs direção do tecido); rotação **não** implica espelhamento.
- [ ] Processar **todas as peças no mesmo mapa**: frentes/costas 0°/180°, mangas/golas 0°/90°/180°/270°, faixas 0°, com quantidades e grupos.
- [ ] Preservar metadados ao salvar/reabrir e testar copiar/colar entre arquivos no CorelDRAW 28.
- [ ] Adicionar perfis opcionais por tipo de peça, **sem depender do nome** para inferir propriedades.
- [ ] Exibir indicação não imprimível da configuração e orientação escolhida na prévia.
- [ ] Testar combinações válidas/inválidas, restrições de trama/sentido da estampa, casos sem solução, Undo e cancelamento.

**Gate:** uma montagem mista real otimizada corretamente, sem orientação proibida, com metadados preservados.

## 3 — P1: arquitetura e organização do repositório

- [ ] Fazer **auditoria somente leitura** de soluções/projetos, referências, COM, WPF, net48/net8, testes, scripts, instalador, ListForge e Nexor.
- [ ] Produzir mapa de dependências atual × proposto e plano de migração com riscos e rollback.
- [ ] Reorganizar **primeiro** documentação e arquivos auxiliares; **depois** projetos, em mudanças pequenas.
- [ ] Separar motores puros de adaptadores Corel quando o acoplamento real justificar a extração.
- [ ] Não criar projetos/repositórios vazios para simular arquitetura; evitar dependências circulares e duplicação de regras.
- [ ] Preservar GUIDs, identidades DEV/comercial, paths críticos, carregamento COM, referências e instaladores.
- [ ] Manter PXToolBox, ListForge, Nexor e MoldMotor independentes; MoldMotor e PX Moldes evoluem separadamente.
- [ ] Exigir build/testes/smoke por etapa e aprovação antes de mover projetos sensíveis.

**Gate:** nenhuma mudança funcional ou regressão de instalação; arquitetura testável sem Corel quando aplicável.

## 4 — P1: contratos de produção e PX Validator

### Contratos e rastreabilidade

- [ ] Contratos versionados: referência de pedido, cliente, peças, tamanhos, quantidades, tecido, arte, revisão, mapa, origem e identificadores idempotentes.
- [ ] ListForge interpreta lista; Assembly monta; Nesting posiciona; Nexor controla execução/consumo. Sem acesso cruzado direto aos bancos.
- [ ] Interface local-first para troca e recuperação de falhas, compatível com versões diferentes.

### PX Validator MVP

- [ ] Conferir peças/tamanhos ausentes, quantidades divergentes, nomes/números fora da área, fontes, PowerClips vazios, elementos fora do mapa, duplicatas e tecido incompatível.
- [ ] Separar erro impeditivo, aviso e informação; perfis configuráveis por processo.
- [ ] Relatório navegável por objeto com justificativa e correção sugerida.
- [ ] Validar antes da montagem/exportação; permitir revisão do operador e registrar exceções.

**Gate:** erros reais detectados antes de gerar material, com falsos positivos controlados.

## 5 — P1: produção de artes, Grids, Moldes e personalização

### Base existente (confirmar aceitação conforme roadmap histórico)

- [x] Macros Saídas: Preparar Grid/Grid Novo/Molde, escudo, bandeira, faixa, NO/NU/AP/TS, renomeação, visibilidade MA/FE/C, medidas, agrupamento e tipos de peça — implementações documentadas.
- [x] Propriedades de objeto e inspeção Shape.Properties; recursos de Tamanho Padrão e Mão Livre documentados.
- [x] ListForge standalone/integrado Classic/PX, importações TXT/PDF/DOCX/XLSX, comparação, histórico, backups e perfis documentados.
- [~] Catálogo de Moldes: camadas, TAG/laser, identificação, duplicação/espelhamento, logos, contornos, salvar e validar — validar cada operação no Corel real.
- [~] Conferir paridade funcional ListForge integrado × standalone e lacunas SISBolt por impacto operacional.

### Evolução

- [ ] Consolidar navegação **Preparação → Personalização → Montagem → Validação → Otimização → Exportação** sem eliminar botões individuais.
- [ ] Biblioteca de moldes com identificadores e resolução por peça, gênero e tamanho; tratar exceções e tamanhos faltantes.
- [ ] Motor Assembly: lista interpretada → seleção de molde → propriedades/metrificação → nomes/números/logos → prévia → geração controlada.
- [ ] Completar propriedades SISBolt relevantes: alinhamentos, replicação de arte/PowerClip, troca de posições, regras de tamanho e seleção, após inventário e testes.
- [ ] Preservar regras de fonte vetorial, acentuação, número com zero à esquerda, medidas, identidade e agrupamento.
- [ ] Gerar arquivos e mapas com validação e possibilidade de intervenção/Undo.

**Gate:** pedido representativo processado de ponta a ponta com conferência de quantidades, arte e moldes.

## 6 — P1: integração Nexor e otimização têxtil

- [x] Integrações iniciais/launcher e intercâmbio de contexto documentados; não confundir com integração completa.
- [ ] Ler larguras de tecido e retalhos disponíveis (incluindo dimensões e, futuramente, formatos irregulares) via contrato versionado Nexor.
- [ ] Usar retalhos e rolos no mesmo planejamento, com limites físicos, margens, orientação da malha e restrições por peça.
- [ ] Gerar mapa e registrar previsão de uso sem alterar estado do Nexor diretamente.
- [ ] Receber execução real/reimpressões/consumo do Nexor e comparar planejado × impresso.
- [ ] Registrar revisões, perdas, suspeitas de divergência e histórico auditável.
- [ ] Benchmark de estratégias por aproveitamento, desperdício, duração e respeito às restrições; não prometer ótimo global.

**Gate:** ciclo de produção rastreável com uma ordem e um mapa real.

## 7 — P2: interface, perfis e produtividade

- [x] Docker WPF DEV, temas, favoritos, seções personalizadas, busca e preferências portáteis documentados.
- [~] Confirmar UX real de personalização: ordenar/ocultar/renomear seções, atalhos, contraste, textos, layout e persistência.
- [ ] Segundo estilo de docker (ícones/pastas) sem duplicar lógica de comandos.
- [ ] Exportação/importação completa de perfil e migração segura entre versões.
- [ ] Sincronização entre PCs **opcional**, apenas após contrato de perfil e política de conflitos.
- [ ] Completar comandos auxiliares de alto uso (SISBolt/eCut) por demanda medida, sem perseguir paridade universal.
- [ ] Manual e diagnósticos por versão, telemetria somente opt-in quando aplicável.

## 8 — P2: produto comercial e distribuição

- [x] Build e instalador DEV documentados.
- [ ] Instalador comercial único com edições **Saídas, Moldes e Full** determinadas por licença; componentes opcionais conforme produto.
- [ ] Licenciamento por produto/edição, com modo offline seguro e regras claras de atualização/expiração.
- [ ] DEV e comercial coexistem sem colisão de instalação, configurações ou plugins.
- [ ] Testes de upgrade/downgrade, reparação, desinstalação, rollback e preservação de dados.
- [ ] Assinatura/integridade de distribuição e pipeline reprodutível de release.
- [ ] Validar dependências reais e versões Corel suportadas, sem detecção presumida.

## 9 — P3: PX Ecosystem Manager

- [ ] Instalador modular com seleção de **PXToolBox, ListForge, Nexor e ferramentas auxiliares**.
- [ ] Manter instaladores individuais e operação dos aplicativos sem o Manager.
- [ ] Detectar instalações; adicionar/remover componentes; atualizar, reparar, verificar integridade e desinstalar individualmente.
- [ ] Preservar perfis e dados; suportar DEV/comercial em paralelo.
- [ ] Centralizar distribuição/atualizações, mas manter verificação de licença em cada aplicativo.
- [ ] Posteriormente: visão de licenças, canais, notas de versão, diagnósticos e perfis sincronizados.
- [ ] Não exigir conta, nuvem ou serviço residente na primeira versão.

## 10 — P3 / backlog condicionado a valor

- [ ] OCR/importação de imagem para listas, com dependências nativas e testes de qualidade.
- [ ] Nesting em lote, múltiplos pedidos, retalhos irregulares, defeitos de tecido, históricos e estratégias avançadas.
- [ ] Recursos amplos de eCut (plotter, CNC, CAD e efeitos) **fora do escopo inicial**.
- [ ] Serviços compartilhados/licenciamento remoto e nuvem somente quando houver caso de uso, segurança e custos definidos.
- [ ] Evoluções do Nexor (métricas de eficiência, consumo, logs Neo/JetPrime) no **roadmap próprio do Nexor**, integradas por contrato.

## Regras de governança

1. Cada entrega deve declarar **implementado**, **testado automaticamente** e **validado no Corel real** separadamente.
2. Nunca marcar um item como concluído só por aparecer em changelog, interface ou documentação.
3. Priorizar regressões e fluxo operacional antes de novas funcionalidades cosméticas.
4. Não remover ferramentas existentes sem substituto equivalente e migração de perfil.
5. Commits em inglês; branches por escopo; não fazer releases sem smoke test e autorização.
6. Atualizar este plano quando houver nova evidência; o [ROADMAP.md](../ROADMAP.md) permanece como histórico detalhado de entregas.

## Próximas três entregas executáveis

**Entrega A — Estabilização:** auditar Corel 28, fechar smoke de dev.119, regressão Nesting/Slice, build/testes/instalador.  
**Entrega B — Rotação por objeto:** metadados persistentes, herança do tecido, montagem mista e testes reais.  
**Entrega C — Modularização segura:** mapa de dependências, plano de migração e primeira extração pequena somente após aprovação.
