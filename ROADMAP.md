# Roadmap — PX Tool Box

Documento vivo. Separa o que está nesta entrega (MVP DEV) do que vem depois.

## MVP DEV (esta entrega)

- [x] Núcleo independente do Corel (`PXToolBox.Core`): convenções de nome (camada, tipo de
      peça, códigos NO/NU/AP/TS), conversão de unidades, resultado de operação diferenciado.
- [x] Lógica de Saídas testável sem COM (`PXToolBox.Outputs`), com abstrações para o
      documento/página/camada/shape do Corel.
- [x] Host real para CorelDRAW 2026 (v27): add-on WPF carregado como docker nativo
      (`PXToolBox.CorelHost.Dev`), com adaptadores reais sobre `Corel.Interop.VGCore`.
- [x] Ferramentas de Saídas portadas: NO/NU/AP/TS (+ expansão recursiva NO/NU de página),
      Renomear Objetos Selecionados, Alterar Tipo de Peça, alternar visibilidade MA/FE/C,
      Somar Medidas (altura só / altura / largura / ambas), Preparar Grid, Preparar Grid Novo
      (com aplicação de estilo de contorno), Preparar Molde Novo, Preparar Escudo/Bandeira/
      Faixa de Capitão (com salvamento automático), Agrupar Interseção.
- [x] Caso crítico do Grid com camadas ocultas coberto por teste automatizado — camadas ocultas
      participam do Grid e têm a visibilidade restaurada (resolução via revisão candidata de
      macros, ver `docs/migrated-tools.md`).
- [x] 47 testes unitários reais, executados nesta máquina (`dotnet test`).
- [x] Scripts de release compatíveis com os 7 botões do Stream Deck, testados localmente
      (build real, instalador real gerado via Inno Setup, validação de pacote real).
- [x] Instalador DEV (Inno Setup) e scripts de instalação/remoção local.
- [ ] **Não confirmado**: "Agrupar Interseção" foi implementado a partir do candidato mais
      plausível encontrado numa revisão de macros, mas nem essa revisão confirma que é o
      algoritmo certo para esse botão (ver `docs/migrated-tools.md`). Validar com o usuário.
- [ ] **Pendente**: smoke test real dentro do CorelDRAW (instalar, abrir, clicar nos botões).
      Só build + testes automatizados foram executados nesta entrega — isso vale também para
      toda ferramenta portada da revisão candidata de macros, que ela mesma declara "não
      compilada nem executada no Corel".

## Depois do MVP (nesta mesma fundação Core + Saídas)

- Tela de configuração no host (pasta de saída configurável, substituindo o caminho cravado
  `Z:\Neuber\Saídas\` da macro original; preferências de usuário vs. dados locais da máquina).
- `Utilidades.AplicaContornoGrid` — decidir com o usuário se/como portar (ver limitações em
  `docs/ARCHITECTURE.md`).
- Distribuição de Produção (Stable): GUIDs, pasta de Addons, identidade e configuração
  próprias — nunca reaproveitando as do DEV. `build-release.ps1 -Channel Stable` hoje recusa
  explicitamente gerar pacote, por não haver empacotamento funcional ainda.
- Persistência local simples e versionada de preferências (layout, favoritos), com
  exportação/importação manual. Sincronização remota fica para depois disso.

## Depois do Core + Saídas (integrações do ecossistema)

Não implementadas nesta entrega; apenas registradas na arquitetura (ver
`docs/ARCHITECTURE.md`, seção 1):

1. **ListForge Integrado** (modos Clássico e PX) + **ListForge Standalone** (abre o
   executável). Núcleo compartilhado e versionado com o standalone.
2. **Nexor**: abrir o standalone, compartilhar contexto de pedido, navegar com o pedido
   carregado, comunicar resultados de execução (o Nexor continua validando as transições).
3. **Moldes**: módulo Moldes do PX integrado, evoluindo separado do MoldMotor (sem
   sincronização presumida de melhorias entre os dois).
4. Servidor de licenças, sincronização em nuvem e cliente de atualização automática completo
   — pontos de extensão documentados, não implementados.
