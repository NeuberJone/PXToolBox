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
      Somar Medidas (altura / largura / ambas, sempre em metros — "Somar Altura (m)" foi
      removido por ser uma duplicata exata de "Somar Altura", ver `docs/migrated-tools.md`),
      Preparar Grid, Preparar Grid Novo (com aplicação de estilo de contorno), Preparar Molde
      Novo, Preparar Escudo/Bandeira/Faixa de Capitão (com salvamento automático), Agrupar
      Interseção.
- [x] Diagnóstico DEV: "Inspecionar Shape.Properties" — ferramenta somente leitura para
      investigar propriedades personalizadas de um objeto (usada inicialmente para o
      experimento de comparação do SISBolt com "Tamanho padrão"; ver `docs/ARCHITECTURE.md`,
      seção 11). Exclusiva da distribuição DEV.
  - **Correção (iteração 33)**: evidência real do usuário revelou um bug de indexação
    0-based/1-based que escondia as entradas de `Shape.Properties` além da primeira
    (confirmado por reflexão sobre o VGCore real + pelo padrão já usado em
    `CorelCollections.cs`) — corrigido no inspetor; `CorelShapeAdapter.DeleteProperties`
    permanece com exclusão SELETIVA por (nome, ID) — uma revisão desta mesma rodada reverteu
    uma tentativa de usar `DeleteAll` nativo, porque seu escopo real (se filtra por nome ou
    apaga tudo do Shape) nunca foi confirmado em tempo de execução. `StandardSizeService`
    (Grid → Tamanho Padrão) ganhou migração explícita de marcações antigas/incompletas,
    detecção automática do estado da seleção (texto no botão, nunca só cor) via evento real
    `SelectionChange`, proteção contra marcação parcial numa falha de escrita, e preservação
    comprovada de outras marcações SISBolt ao desativar/migrar. **Contrato de largura/altura
    CONFIRMADO por reinspeção real**: `KEEP_DESIGN_SIZE` ID 0 Boolean (flag), ID 1 Double
    (largura), ID 2 Double (altura, nunca gravada por nenhuma versão anterior desta mesma
    tarefa).
    **Correção (iteração 34) — bug de unidade confirmado**: comparando o MESMO objeto físico
    marcado pelo PX e pelo SISBolt real, os valores do PX eram exatamente os do SISBolt
    divididos por 2,54 (fator polegada→cm) — `Shape.Width/Height` SEMPRE retornam em
    polegadas, nunca na unidade de régua do documento (a afirmação anterior desta entrada
    estava errada). Corrigido: `StandardSizeService` agora exige `DocumentUnit?` nos métodos
    de escrita e converte via `DocumentUnitConversion` (reaproveitado de
    `MeasurementSumService`, nunca uma segunda tabela); unidade não reconhecida falha com
    mensagem clara em vez de gravar errado. **Achado colateral fora do escopo, não
    corrigido**: `MeasurementSumService` ("Somar Medidas") parece ter a mesma estrutura de
    risco — não verificado nem corrigido aqui, precisa de confirmação real e tarefa própria.
    **Correção (iteração 35) — a fonte da unidade estava errada**: reteste real num documento
    configurado em mm continuou gravando em polegada mesmo com a correção acima (build nova
    instalada, CorelDRAW reiniciado). O usuário deu a pista: "o SISBolt funciona independente
    da unidade configurada no arquivo". Confirmado por reflexão sobre o VGCore real:
    `Application.Unit` (preferência do APLICATIVO) e `Document.Unit` (por arquivo) são duas
    propriedades DIFERENTES — a correção anterior usava `Document.Unit`; o SISBolt real usa
    `Application.Unit`. Corrigido: nova `ICorelWorkspace.ApplicationUnit`;
    `ControlUI.OnToggleStandardSize`/`OnUpdateStandardSize` agora passam `ws.ApplicationUnit`
    em vez de `doc.Unit`. Mapeamento `cdrUnit`→`DocumentUnit` extraído pra `CorelUnitMapping`
    compartilhado. `StandardSizeService` em si não mudou (já recebia um `DocumentUnit?`
    genérico). (Superado: a reconfirmação real mostrou que `Application.Unit` também não era a
    fonte — ver iterações 36 e 37 abaixo, esta última confirmada por smoke test real.)
    Ver `docs/ARCHITECTURE.md`, seções 25–27, para a investigação completa.
    **Correção (iteração 36) — nem Document.Unit nem Application.Unit; ToUnits nativo**:
    reconfirmação real do usuário (documento em cm) mostrou que `Application.Unit` TAMBÉM não
    era a fonte certa. Em vez de arriscar uma terceira propriedade adivinhada, trocado pro
    método nativo `Document.ToUnits(valor, cdrInch)` — deixa o próprio Corel decidir a unidade
    "atual" (confirmado por reflexão sobre o VGCore real). Nova
    `ICorelDocument.ConvertFromInches`; `StandardSizeService` trocou `DocumentUnit?` por
    `Func<double, double>?` (nunca decide COMO converter, só aplica o delegate recebido).
    `ICorelWorkspace.ApplicationUnit` (da correção anterior) removido, superado. **Melhoria no
    relatório do Inspecionar Shape.Properties** (pedido explícito, depois de toda essa
    investigação ter exigido várias idas e voltas): novo cabeçalho de diagnóstico sempre
    presente — versão do PX Tool Box, nome do documento, Document.Unit E Application.Unit
    (rótulo + nome técnico `cdrXxx`), nunca linhas vazias quando a informação não está
    disponível. Testes: `StandardSizeServiceTests` com conversões via delegate (`Identity`,
    `inches => inches * 2.54`); `ShapePropertiesReportFormatterTests` +2 novos. Suíte completa
    96/96 (net8.0) + 343/343 (net48), executada duas vezes. Ver `docs/ARCHITECTURE.md`,
    seções 28-29.
    **Correção (iteração 37) — quarto bug de unidade, agora confirmado por smoke test real**:
    a build da iteração 36 foi instalada e testada de verdade num documento em cm — o PX
    continuou gravando a polegada bruta, idêntica byte a byte a `shape.Width/Height`.
    `Document.ToUnits` usa `Document.Unit` por baixo, e o relatório melhorado confirmou:
    `Document.Unit` E `Application.Unit` reportam `cdrInch` para este documento mesmo depois
    de salvo — as duas são, de fato, fonte errada, não só presumidas erradas. Em vez de
    adivinhar uma quarta propriedade isolada, levantado por reflexão TODO membro "Unit*" em
    toda a `Corel.Interop.VGCore.dll`: achado `Document.Rulers.HUnits`/`VUnits`, confirmado
    por evidência real (mesmo documento, antes e depois de salvar) como a propriedade
    sincronizada com o que a régua/barra de propriedades mostra. Corrigido:
    `ICorelDocument.ConvertFromInches` agora usa `Application.ConvertUnits(valor, cdrInch,
    Document.Rulers.HUnits)` — recebe origem E destino explícitos, sem depender de nenhum
    estado ambiente. Build intermediária só de diagnóstico (0.1.0-dev.42) adicionou as linhas
    de `Rulers.HUnits/VUnits` ao relatório ANTES da correção, pra confirmar com evidência real
    em vez de mais uma tentativa às cegas. Suíte completa 96/96 (net8.0) + 344/344 (net48).
    **Confirmado por smoke test real** (0.1.0-dev.43, mesmo objeto): `KEEP_DESIGN_SIZE`
    ID1/ID2 gravados em 7,61218/7,45192 — batendo exatamente com 7,612×7,452 cm mostrados
    pela régua (valor bruto em polegada × 2,54). Primeira das quatro tentativas confirmada
    por evidência real, não só presumida correta. Ver `docs/ARCHITECTURE.md`, seção 30.
- [x] Caso crítico do Grid com camadas ocultas coberto por teste automatizado — camadas ocultas
      participam do Grid e têm a visibilidade restaurada (resolução via revisão candidata de
      macros, ver `docs/migrated-tools.md`).
- [x] 54 testes unitários reais (netstandard2.0/net8.0) + 208 na perna net48 (que soma os 54
      com 10 testes exclusivos de `ListForgeLauncher`/`ListForgeConfigStore`, 133 do ListForge
      Integrado (inclui Perfis de trabalho, Tamanhos customizados, Backup automático de entrada,
      Diagnóstico/pacote de suporte e Edição manual da saída/JSON, iterações 13-17) — ver
      `docs/listforge-classic-parity.md` — e 11 do inspetor "Shape.Properties" (diagnóstico, ver
      `docs/ARCHITECTURE.md`, seção 11). A soma de medidas
      (`Somar Altura/Largura/Ambas`, seção "Medidas") perdeu 1 teste numa rodada ao remover a
      duplicata "Somar Altura (m)" — ver `docs/migrated-tools.md`. Executados nesta máquina
      (`dotnet test`).
- [x] **ListForge Standalone** — seção "Listas" no painel DEV: abre o executável do ListForge
      configurado, sem incorporar sua interface ou processamento, sem depender de documento
      aberto no CorelDRAW. Seletor de arquivo para configurar/trocar o caminho; caminho salvo
      como dado da máquina (`%LOCALAPPDATA%\PXToolBox\Dev\config\`), exclusivo da distribuição
      DEV. Sem mecanismo de ativação de instância existente (não documentado/implementado pelo
      ListForge hoje). Ver `docs/ARCHITECTURE.md`, seção 9. **Validação dentro do CorelDRAW
      real com o ListForge real: pendente** (ver `docs/mvp-smoke-test.md`).
- [x] Scripts de release compatíveis com os 7 botões do Stream Deck, testados localmente
      (build real, instalador real gerado via Inno Setup, validação de pacote real).
- [x] Instalador DEV (Inno Setup) e scripts de instalação/remoção local.
- [x] **Confirmado no CorelDRAW real**: "Agrupar Interseção" (implementado a partir
      do candidato `Agrupamento.AgruparObjetosSobrepostos` de uma revisão de macros). Confirmado
      no cenário testado (2026-10, registrado em `docs/mvp-smoke-test.md`) e reafirmado pelo
      usuário em 05/11/2026 — ver `docs/migrated-tools.md`.
- [ ] **Smoke test no CorelDRAW — parcial.** Já confirmado pelo usuário no Corel real:
      docker lateral (26/10/2026), Preparar Escudo/Bandeira/Faixa de Capitão, Renomear Objetos
      e Agrupar Interseção (no cenário testado), Alterar Tipo de Peça (no cenário testado,
      relato de 05/11/2026), contrato de largura/altura do Tamanho Padrão
      (`0.1.0-dev.43`), digitação/processamento do ListForge Integrado (`0.1.0-dev.15`) e o
      diagnóstico de identidade de camadas (`0.1.0-dev.48`). Continuam pendentes os itens
      marcados como tal em `docs/mvp-smoke-test.md` (ex.: 4e passo 6 — Tamanho Padrão; 4i,
      inclusive o passo 0 — Diagnóstico; 4j; 4k). **Observado pelo usuário** (05/11/2026): baixo
      contraste na caixa de seleção (lista de Tipo de Peça) — conferência visual pendente até
      confirmação.

## Depois do MVP (nesta mesma fundação Core + Saídas)

- **Parcial (dev.58: pasta de saída padrão por máquina)** — tela de configuração no host (pasta de saída configurável, substituindo o caminho cravado
  `Z:\Neuber\Saídas\` da macro original; preferências de usuário vs. dados locais da máquina).
- **Feito (dev.61): botão "Aplicar contorno do Grid"** — `Utilidades.AplicaContornoGrid` — decidir com o usuário se/como portar (ver limitações em
  `docs/ARCHITECTURE.md`).
- Distribuição de Produção (Stable): GUIDs, pasta de Addons, identidade e configuração
  próprias — nunca reaproveitando as do DEV. `build-release.ps1 -Channel Stable` hoje recusa
  explicitamente gerar pacote, por não haver empacotamento funcional ainda.
- Persistência local simples e versionada de preferências (layout, favoritos), com
  exportação/importação manual. Sincronização remota fica para depois disso. (Favoritos:
  dev.50 — 35ª rodada; ordem e visibilidade das seções: dev.53 — 37ª rodada abaixo.)

## Depois do Core + Saídas (integrações do ecossistema)

Não implementadas nesta entrega; apenas registradas na arquitetura (ver
`docs/ARCHITECTURE.md`, seção 1):

1. **ListForge Integrado** — modo **Clássico** implementado: janela própria ("Abrir ListForge
   Integrado" no painel DEV, seção "Listas") consumindo o núcleo compartilhado do repositório
   do ListForge (`ListForge.Core`, netstandard2.0, referenciado por caminho relativo
   configurável — ver `docs/ARCHITECTURE.md`, seção 10). **Teclado confirmado funcionando pelo
   usuário no Corel real** (hook de thread `WH_GETMESSAGE` + foco nativo reforçado, iteração 4)
   — mecanismo tratado como estável e preservado sem alteração a partir daqui. Contraste da aba
   selecionada corrigido (iteração 3).

   **Paridade funcional com o standalone real** (`docs/listforge-classic-parity.md`, inventário
   de 41 funções contra `C:\Projetos\ListForge`, branch `main`, v2.1.44): 1ª rodada (iteração 5)
   — bloqueio de resultado desatualizado, navegação da lista de avisos/erros até a linha, e
   Localizar/Substituir movido para o núcleo compartilhado. **2ª rodada (iteração 6)**: extrair
   lista de link (`LinkListImportService`, movido para o núcleo compartilhado), lista avançada /
   mapeamento de peça por slot JSON (`JsonPieceMappingService`, já compartilhado — UI nova), e
   resumo de tamanhos processados (`SizeSummaryBuilder`, novo). **3ª rodada (iteração 7)**:
   Processar com prévia de confirmação (`ProcessingPreviewService`, movido para o núcleo
   compartilhado nesta rodada, com `ProcessingPreviewWindow` nova) — agora o botão "Processar"
   padrão, com "Processar rápido (sem prévia)" como alternativa secundária, igual ao standalone.
   **Correção crítica (iteração 8)**: o CorelDRAW inteiro chegou a fechar ao abrir o ListForge
   Integrado (`0.1.0-dev.14`) — `NullReferenceException` não capturada dentro do
   `InitializeComponent()`; corrigida e confirmada pelo usuário no Corel real (`0.1.0-dev.15`).
   **4ª rodada (iteração 9)**: Comparar entrada/saída (`ListComparisonService`, movido para o
   núcleo compartilhado nesta rodada, com `ComparisonWindow` nova) — mesmas categorias
   semânticas, filtro, navegação entre diferenças e relatório do standalone; nunca compara
   silenciosamente a entrada atual com uma saída antiga. Funções de iterações anteriores,
   preservadas: abrir/salvar lista (.txt), remover espaços, limpar tudo, copiar saída/JSON.
   **ListForge Standalone** continua funcionando de forma independente (botão "Abrir ListForge
   Standalone ↗"). Modo **PX** (reaproveitando objetos/layout do documento ativo) ainda não
   implementado nesta tarefa. Licenciamento/Trial do modo integrado: decisão pendente (ver
   `docs/ARCHITECTURE.md`, seção 10) — não bloqueia o processamento hoje, mas a interface informa
   isso explicitamente.

   **5ª rodada (iteração 10)**: Salvar avançado (`AdvancedSaveService`, movido para o núcleo
   compartilhado nesta rodada, com `AdvancedSaveDialog` nova) — arquivos soltos ou ZIP com
   entrada+saída+JSON, mesmos portões do standalone, nunca sobrescreve (nome incrementado
   automaticamente em colisão) e identifica arquivos gravados/pendentes em falha parcial
   (reforço desta entrega).

   **6ª rodada (iteração 12)**: Configurações do módulo — `ListForgeIntegratedSettingsStore` +
   `SettingsWindow` novos, gravando em
   `%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-settings.json` (pasta já existente,
   reaproveitada — nunca o ListForge Standalone, o PX de produção ou o MoldMotor), esquema
   versionado, gravação atômica, configuração corrompida colocada em quarentena em vez de
   sobrescrita. Persistidos: tamanho de fonte do editor, formato padrão do Salvar avançado
   (resolve a limitação "só em memória" da rodada anterior), pasta/nome padrão de exportação.
   Exportar/Importar configurações e "Abrir pasta de configuração" também implementados.

   **7ª rodada (iteração 13)**: Perfis de trabalho — criar/selecionar/renomear/duplicar/excluir/
   restaurar, mesmas regras do standalone (`WorkProfileService`, **movido** para
   `ListForge.Core` nesta rodada — `AppConfig` do standalone passou a implementar a interface
   extraída `IWorkProfileConfig` para permitir o reuso sem acoplar o núcleo compartilhado ao
   `AppConfig` concreto). Combo + botões de perfil ficam na janela principal (onde estão os
   controles de separador/capitalização/ordenação/lista avançada que compõem um perfil);
   "Configurações..." continua só com tamanho de fonte e Salvar avançado/pasta/nome do perfil
   ATIVO (antes campos únicos no topo, agora dentro de cada perfil — esquema 2, com migração
   explícita do esquema 1 preservando os valores já salvos no perfil "Padrão").

   **8ª rodada (iteração 14)**: Tamanhos customizados — `SizeConfigWindow` nova (4 grupos fixos:
   masculino/feminino/infantil/meião, tamanhos-base/prefixos/sufixos em CSV, resumo e
   "Restaurar tamanhos padrão" próprio), reusando por completo `ListForge.Core.SizeHelper`/
   `SizeConfig` (já compartilhados, nenhum arquivo precisou ser movido desta vez). Independente
   de perfil de trabalho — igual ao standalone (`sizes.json` é um arquivo separado de
   `config.json`/`WorkProfiles` lá); aqui vive em `ListForgeIntegratedSettings.Sizes`, campo de
   módulo, campo aditivo (sem precisar mudar o esquema). `ClassicWorkspaceWindow._sizeConfig`
   deixou de ser fixo em `SizeConfig.Default()` — agora controla de fato reconhecimento,
   validação, ordenação e exportação (prova de ponta a ponta: adicionar um tamanho faz ele ser
   reconhecido; remover um tamanho existente faz ele ser rejeitado, nunca convertido
   silenciosamente para outro).

   **9ª rodada (iteração 15)**: Backup automático de "Salvar entrada"/"Salvar como" —
   `ListForgeInputBackupService` novo, mesma assimetria do standalone (backup só quando o
   conteúdo em disco mudou, em "Salvar entrada"; sempre que o destino já existir, em "Salvar
   como"), em `PXToolBox\Dev\ListForge\Backups\`, sem retenção (igual à referência, que também
   não tem) e com "Abrir pasta de backups" no Diagnóstico.

   **10ª rodada (iteração 16)**: conclusão do grupo Diagnóstico e Suporte — `PxLogger`/
   `SupportPackageService` (infraestrutura de uma rodada anterior interrompida) agora com
   logging ligado às falhas de Processar/Processar com prévia/Comparar entrada e saída/Salvar
   avançado/Abrir lista/Salvar perfis de trabalho (além de startup/shutdown/backup de entrada,
   já existentes), retenção de logs (14 dias/20 MB) comprovada por teste como nunca afetando a
   pasta de backups, informações de suporte completas com "Copiar informações de suporte", e
   geração manual do pacote de suporte (`.zip` com relatório técnico + resumo seguro de
   configurações + logs recentes — nunca lista/backup/arquivo de produção/segredos).

   **11ª rodada (iteração 17)**: Edição manual da saída/JSON — checkbox "Permitir edição... nesta
   sessão" (nunca persistido, igual ao standalone), "Aplicar alterações"/"Descartar alterações"
   reprocessando via `ProcessingWorkflowService`/`ListProcessor.ExtractListTextFromJsonData`
   (núcleo compartilhado), indicador "Resultado editado manualmente" sempre distinto do aviso de
   entrada desatualizada, reprocessar nunca descarta uma edição pendente em silêncio, comparação
   aceita e sinaliza uma saída editada (`ComparisonSnapshot.OutputWasManuallyEdited`), e a fonte
   de texto de Copiar/Salvar/Gerar JSON/Salvar avançado corrigida para a mesma regra viva-vs-
   canônica do standalone (protege contra exportar JSON ainda não revalidado).

   **12ª rodada (iteração 18)**: Histórico de processamentos — `ListForgeProcessingHistoryService`
   novo (porta de `ProcessingHistoryService` do standalone) + `ProcessingHistoryWindow` nova
   (porta de `HistoryView`), gravando em
   `%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-history.json` (mesma pasta de
   `ListForgeIntegratedSettingsStore`, nunca o `AppDir` do ListForge Standalone). Um registro só
   é criado quando "Salvar saída"/"Gerar JSON"/"Salvar avançado" gravam um arquivo de fato —
   nunca só por "Processar". **Achado ao ler a referência real por completo antes de
   implementar**: o standalone não tem exclusão individual de registro nem "restaurar" um
   registro para reabrir a lista — só "Abrir pasta da saída" e "Limpar histórico" (com
   confirmação); esta porta reproduz exatamente essas duas, sem inventar as que não existem.
   Limite de 100 registros (igual ao standalone), sem expiração por idade (a referência também
   não tem). Uma falha ao atualizar o histórico nunca desfaz a exportação já concluída — só fica
   registrada no log do módulo. Excluído do pacote de suporte por padrão (pode conter nomes de
   arquivo/lista reais).

   **13ª rodada (iteração 19)**: Importação de documentos — PDF/Word (.docx)/Excel (.xlsx/.xlsm).
   `ListForge.Core.FileImporter`/`ListForge.Core.Services.FileImportService` novos, porta das
   mesmas classes do standalone (`Core/FileImporter.cs`/`Services/FileImportService.cs`),
   **movidas** para o núcleo compartilhado nesta rodada junto com os 3 pacotes NuGet reais
   (PdfPig 0.1.9, DocumentFormat.OpenXml 3.1.0, ClosedXML 0.104.1) — compatibilidade com
   netstandard2.0 **e** com o host net48 confirmada por build real dos dois lados antes de
   implementar, não por suposição. "Abrir Lista..." (antes só `.txt`) passou a aceitar esses 3
   formatos, com as mesmas regras da referência: lê o arquivo inteiro (sem escolha de
   página/planilha — a referência também não oferece isso), mostra o conteúdo extraído na
   própria caixa de entrada para revisão, nunca processa/exporta automaticamente. Imagem/OCR
   (Tesseract — binários nativos + `tessdata`) permanece deliberadamente fora desta entrega; o
   botão reconhece extensões de imagem e informa a limitação explicitamente, nunca um "formato
   não suportado" genérico nem uma importação vazia apresentada como sucesso. Colisão de nomes
   entre o núcleo movido e o residual do standalone (que ainda precisa da parte de OCR)
   resolvida renomeando os tipos do standalone para `ImageOcrImporter`/`DocumentImportService` —
   sem mudar nenhum comportamento observável lá (282/282 testes do standalone continuam
   passando). Reforço desta entrega: confirma antes de substituir uma entrada com alterações não
   salvas ao importar um documento (a referência substitui sem perguntar) e a extração roda em
   `Task.Run`, nunca na thread de UI, nunca tocando em COM do Corel.

   **Pendente** (ver `docs/listforge-classic-parity.md` para o porquê de cada um): Trial/
   licenciamento do modo integrado (decisão de produto, 2 entradas na tabela — mesma pendência
   repetida em dois grupos) e Importar arquivo — imagem/OCR, **dependência opcional ainda não
   integrada** (Tesseract: binários nativos + arquivos de idioma) — isto não significa
   necessariamente que a integração futura exigiria instalação automática; a forma de
   distribuição (empacotado no instalador, módulo separado, download sob demanda etc.) é uma
   decisão de produto ainda não tomada, não uma conclusão técnica já fechada.
   Enquanto houver lacunas, o modo permanece identificado na própria interface como
   "Clássico — em desenvolvimento" — critério explícito: só sai desse rótulo quando houver
   evidência suficiente de equivalência E estabilidade nos fluxos previstos, o que ainda não é o
   caso (lacuna de OCR acima, e nenhum smoke test real no Corel além da correção do crash de
   abertura, iteração 8, foi confirmado para as rodadas de Comparar entrada/saída, Salvar
   avançado, Configurações, Perfis de trabalho, Tamanhos customizados, Backup de entrada,
   Diagnóstico/pacote de suporte, Edição manual da saída/JSON, Histórico de processamentos e
   Importação de documentos).

   **14ª rodada (iteração 20) — preparação da validação de aceitação, SEM alteração de código**:
   material de teste (`docs/acceptance/listforge-integrated-0.1.0-dev.26/`) — `.txt`/`.pdf`
   (com texto, 4 páginas)/`.docx`/`.xlsx` com o MESMO conteúdo lógico (nomes acentuados, números
   com zero à esquerda), gerados e verificados de ponta a ponta (extração + processamento reais
   via `ListForge.Core`/`ListForge.Services`, antes de entrar no pacote) por um programa C# de
   uso único, descartado depois de gerar os arquivos — não faz parte do repositório. Mais um
   `.pdf` sem nenhum texto, para testar a mensagem de limitação. Roteiro de validação
   (`roteiro-aceitacao-corel.md`) organizado em 8 blocos (A–H: abrir/digitar/atalhos; importar
   cada formato; processar/comparar; editar saída/JSON; backup de entrada; histórico; perfil e
   tamanho customizado; pacote de suporte), cada item com um campo Aprovado/Falhou/Não testado —
   **nenhum item foi marcado como aprovado nesta tarefa**; nada substitui a execução real por um
   humano dentro do CorelDRAW. Nenhuma versão nova gerada (sem alteração de código, continua
   0.1.0-dev.26, conforme `AGENTS.md`).

   **Correção de registro sobre o travamento intermitente dos testes `net48`**: rodadas
   anteriores deste roadmap podem ter dado a impressão de que a causa estava resolvida
   (processos órfãos/ambiente). As reproduções reais não sustentam essa conclusão — em pelo
   menos uma ocorrência a trava aconteceu com o ambiente já limpo, sem outro processo
   concorrente identificado, e nenhuma delas teve um dump de thread ou evidência direta do que
   o processo travado estava fazendo. **A causa permanece não determinada** — ver nota completa
   em `docs/acceptance/listforge-integrated-0.1.0-dev.26/roteiro-aceitacao-corel.md`. O checklist
   da `0.1.0-dev.26` continua **pendente** por decisão do usuário — não alterado pelas rodadas
   seguintes.

   **15ª rodada (iteração 21) — desenho do modo PX, protótipo e documento de design, SEM código
   de produção**: protótipo de navegação (`docs/design/listforge-px-workflow/prototype.html`,
   HTML/CSS/JS autocontido, sem dependência nova) e documento de design
   (`docs/design/listforge-px-workflow/design.md`) propondo o fluxo Lista → Revisão →
   Processamento → Resultado para o modo PX, consumindo exatamente o mesmo núcleo
   (`ProcessingWorkflowService`/`ProcessingPreviewService`/`ListComparisonService`/
   `AdvancedSaveService`/etc.) e a mesma infraestrutura do PX DEV (`Integrations/*`) já usados
   pelo Clássico — nenhuma regra duplicada, nenhuma prévia/progresso inventado (a etapa
   Processamento é deliberadamente binária, já que `ProcessingWorkflowService.Execute` não
   expõe progresso granular). Mapa completo de toda função do Clássico para uma das 4 etapas ou
   para uma barra persistente global (perfil, Configurações, Tamanhos customizados,
   Diagnóstico, Salvar Entrada/Como, link do Standalone) — nunca uma sequência obrigatória de
   cliques. Continuidade entre modos proposta como um único estado de sessão com duas
   apresentações (nunca duas sessões divergentes); entidade escolhida para a preferência
   Clássico/PX por perfil é `ListForgeIntegratedSettings` (exclusiva do PX DEV), nunca
   `WorkProfile`/`WorkProfileSettings` (compartilhados com o Standalone, que não tem esse
   conceito) — decisão justificada em `design.md`, seção 3.3. Decisões técnicas em aberto
   (estrutura de código para compartilhar estado entre Clássico/PX, papel futuro de reaproveitar
   objetos do documento ativo do Corel, nome final do modo para o usuário) registradas na seção
   5 do documento, para o usuário/product owner decidir. Nenhuma versão nova gerada (sem
   alteração de código, continua `0.1.0-dev.26`). Checklist de aceitação da `.26` preservado
   como pendente, sem nenhum item alterado.

   **16ª rodada (iteração 22) — refino do protótipo e consolidação do design, ainda SEM código
   de produção**: ajustes aprovados pelo usuário sobre a 15ª rodada. Fluxo reduzido para 3
   etapas (Lista → Revisão → Resultado) — a etapa "Processamento" foi removida; "Processar"
   agora mostra um estado temporário sobreposto ("Processando...", sem porcentagem/duração) que
   resolve para Resultado (sucesso) ou permanece em Revisão com a entrada preservada e um aviso
   (falha). Telas deixaram de mostrar nome de serviço/classe ou referência ao documento de
   design — todo esse detalhe ficou só em `design.md`. Corrigido o exemplo de aviso da etapa
   Revisão: "BLM" é um tamanho feminino válido, não "fora do grupo oficial" — o exemplo agora
   usa um aviso real do núcleo compartilhado (linha sem nome informado). Etapa Resultado
   reorganizada em abas "Lista organizada"/"JSON", cada uma inspecionável antes de
   copiar/exportar. Etapa Revisão compactada (resumo em pílulas, tabela como elemento principal
   com rolagem própria, botões Voltar/Processar fixos no rodapé, sempre acessíveis). Lista
   avançada passou a ficar numa seção expansível na etapa Lista, com as 6 opções reais de peça
   (Manga Curta/Manga Longa/Short/Calça/Regata/Colete). Estados explicitamente diferenciados
   (sem resultado, resultado atual, desatualizado, edição pendente, processando, falha) via
   controles de demonstração próprios, separados da interface "real". Decisões ainda em aberto
   (sem atribuir ao usuário escolhas não aprovadas) registradas na seção 6 de `design.md`.
   Nenhuma versão nova gerada, checklist da `.26` continua pendente.
   **17ª rodada (iteração 23) — implementação real do modo PX**: `ClassicWorkspaceWindow` agora
   tem duas apresentações (Clássico/PX) sobre um **único estado de sessão** — nunca duas cópias.
   PX move (nunca duplica) os mesmos controles nomeados do Clássico entre os dois layouts via
   reparenting (`ClassicWorkspaceWindow.Px.cs`, `PxActivate`/`PxDeactivate`/`PxMoveToSlot`):
   separador, capitalização, ordenação, Localizar/Substituir, Lista avançada (com a mesma
   quantidade de posições gerada por `RefreshAdvancedSlots` — nunca um número fixo), a caixa de
   entrada, `IssuesList`, o painel de saída completo (abas Lista organizada/JSON, avisos de
   desatualização/edição), o painel de edição manual, o combo de perfil de trabalho, o texto de
   acesso, a barra de status e o Diagnóstico — todos reaproveitados, nenhum recriado. Fluxo
   Lista → Revisão → Resultado: Revisão chama `ProcessingPreviewService.Analyze` de verdade
   (mesmo serviço do Clássico) e mostra contagens/tabela de interpretação por linha/avisos reais
   (`IssuesList` reaproveitado — clicar num aviso navega até a linha, voltando automaticamente à
   etapa Lista para que a seleção seja visível); "Processar" mostra só um estado "Processando..."
   sobreposto (sem porcentagem inventada), impede processamento concorrente (`_pxIsProcessing`) e
   recusa executar se a entrada/opções mudaram desde a última Revisão (`PxRequestsMatch` —
   comparação manual campo a campo, nunca igualdade de record padrão, que compararia a lista de
   `PieceOrder` por referência) — nesse caso atualiza a prévia em vez de processar um instantâneo
   desatualizado; sucesso reusa `ApplyResult` (mesmo método do Clássico) e abre Resultado; falha
   preserva a entrada e mostra o erro sem sair de Revisão. "Nova lista" só confirma quando há algo
   a perder de fato (entrada/edição/resultado não vazios) — proposta nova desta etapa, registrada
   em `design.md`. Preferência de modo por perfil persistida em
   `ListForgeIntegratedSettings.PreferredWorkspaceModeByProfileId` (exclusiva do PX DEV — nunca em
   `WorkProfile`/`WorkProfileSettings`, compartilhados com o standalone). Troca de modo nunca
   reprocessa, salva ou descarta nada — só move os controles; testado com uma janela real numa
   thread STA dedicada (`ListForgePxModeTests`), incluindo que cada controle volta exatamente ao
   pai original do Clássico depois de voltar do modo PX. **Limitações explícitas desta entrega**:
   nome continua "PX — em desenvolvimento"; nenhuma leitura/montagem de objetos do documento Corel
   (continua baseado em texto, igual ao Clássico); sem Nexor, sem OCR, sem regra de licenciamento
   nova; troca de perfil de trabalho NÃO troca o modo automaticamente (a preferência salva só é
   aplicada na próxima abertura da janela); aceitação da `.26` continua pendente por decisão do
   usuário. Build do host — 0 avisos/erros; `dotnet test` do PXToolBox — 54/54 (net8.0) + 243/243
   (net48, 243 = 232 anteriores + 11 novos). Nenhum smoke test real no Corel foi feito.
   **18ª rodada (iteração 24) — primeiro acesso ao Nexor standalone**: nova seção "Produção" no
   painel DEV ("Abrir Nexor ↗"/"Localizar executável…"), mesmo padrão de decisão de "ListForge
   Standalone" (`NexorLauncher`/`NexorConfigStore`, testável sem abrir processo real, sem
   incorporar a interface do Nexor e sem ler/alterar `nexor.db`). Distribuição real localizada
   no ambiente (nunca suposta): `C:\Projetos\ProjetoJocasta\dist\Nexor-1.6.29.exe` — confirmado
   pelo próprio `CHANGELOG.md` do projeto ("Rebrand para Nexor", de "Projeto Jocasta"/JocastaHub)
   e pelo atalho real que o usuário já fixa na barra de tarefas, apontando exatamente para esse
   caminho, sem argumentos — ver `docs/ARCHITECTURE.md`, seção 14. Configuração própria
   (`nexor-path.txt`), separada da do ListForge, mesma pasta `PXToolBox\Dev\config`. 11 testes
   novos (`NexorLauncherTests`/`NexorConfigStoreTests`) — primeira configuração, persistência,
   caminho com espaços, executável movido/excluído, cancelamento, falha de inicialização,
   independência real da configuração do ListForge. Build do host — 0 avisos/erros; `dotnet test`
   — 54/54 (net8.0) + 254/254 (net48, 254 = 243 anteriores + 11 novos). Teclado, modos
   Clássico/PX e ferramentas de Saídas preservados sem alteração. **Pendências preservadas sem
   serem tocadas ou misturadas com esta entrega**: aceitação da `.26`; validação do modo PX da
   `.27` no Corel; caso "Carlos Teste,15,ZZZ" (ZZZ como apelido, aviso de tamanho não
   encontrado); investigação do redesenho intermitente do painel. Nenhum smoke test real do
   botão "Abrir Nexor ↗" dentro do Corel foi feito.
   **19ª rodada (iteração 25) — "Remover NO/NU da página" também desce em PowerClip**: a macro
   original (`CorelMacros/Correcoes.bas`, `RenameObjects`) nunca tratou PowerClip para esta
   ferramenta específica (só `cdrGroupShape`) — confirmado lendo o `.bas` real antes de corrigir,
   para não atribuir à migração uma lacuna que já existia desde a macro original. `Shape.Shapes`
   e `Shape.PowerClip.Shapes` são propriedades de tipos diferentes na API real (confirmado por
   reflexão sobre `Corel.Interop.VGCore.dll` instalado nesta máquina) — `Native.Shapes` nunca
   inclui conteúdo de PowerClip. Novo contrato em `ICorelShape` (`HasPowerClipContents`/
   `PowerClipShapes`), implementado em `CorelShapeAdapter` via `Shape.PowerClip`/
   `PowerClip.Shapes` (nunca via `Type`/`Kind` — `cdrPowerClipShape` reconfirmado inexistente nos
   27 valores reais de `cdrShapeType` da v27). `CodeToggleService.RenameRecursive` agora desce
   em grupo E em PowerClip, independentemente (nunca mutuamente exclusivos), sem renomear o
   próprio contêiner. Ver `docs/ARCHITECTURE.md`, seção 15, para o que foi e não foi validado
   (API confirmada por reflexão; comportamento em tempo de execução dentro do Corel real ainda
   não). 4 testes novos (`CodeToggleServiceTests`). Nenhum smoke test real feito.
   **20ª rodada (iteração 26) — correção da identificação de versões DEV e proteções de
   empacotamento**: investigado um relato de uma execução que considerou `0.1.0-dev.9` mais
   recente que `0.1.0-dev.28` já existente. Lidos por completo todos os scripts de build/
   validação/publicação deste repositório — nenhum descobre "a versão mais recente"
   automaticamente, todos exigem `-Version` explícito; o incidente é consistente com uma
   decisão ad-hoc fora de qualquer script versionado, não um bug já existente no código. Risco
   real mesmo assim: `"0.1.0-dev.9"` > `"0.1.0-dev.28"` como STRING pura. Novo módulo
   `scripts/PxToolBoxVersioning.psm1` centraliza a comparação (ordenação numérica real,
   dev.9 < dev.10 < dev.27 < dev.28 < dev.100), a descoberta de versões em `dist\<channel>\`
   (distinguindo válida/nome inválido/build incompleta/divergente — nunca tratando essas
   quatro coisas como equivalentes) e a proteção de sobrescrita (`-Force` restrito à pasta
   exata pedida, nunca outra). `build-release.ps1` agora usa este módulo; mesma mensagem de
   erro de antes (confirmado rodando contra a build `.28` real, sem tocar nela), mais um aviso
   novo (nunca bloqueio) quando a versão pedida é mais antiga que a mais recente coerente já
   existente no canal. Ver `docs/ARCHITECTURE.md`, seção 16, para a investigação completa e a
   lacuna registrada (não corrigida, fora do escopo pedido) do `latest.json` do R2 nunca
   comparar contra a versão já publicada. 33 asserts novos
   (`scripts/Test-PxToolBoxVersioning.ps1`), só com diretórios temporários — nunca os builds
   reais em `dist\`. `docs/stream-deck-buttons.md` atualizado (Botão 1/3) para o novo aviso,
   sem alterar nenhum ponto de entrada existente.
   **21ª rodada (iteração 27) — correção da falha ao salvar perfis de trabalho**: mensagem
   relatada ("Não foi possível salvar os perfis de trabalho: Não foi possível remover o
   arquivo a ser substituído.") é literalmente a mensagem real de `File.Replace` quando o
   sistema operacional não consegue remover o arquivo de destino na troca — localizada em
   `ListForgeIntegratedSettingsStore.Save` (configurações, perfis de trabalho e tamanhos
   customizados vivem no mesmo arquivo). Novo `Integrations/AtomicFileWriter.cs`, infraestrutura
   COMPARTILHADA extraída também para `ListForgeProcessingHistoryService` (tinha cópia quase
   idêntica da mesma lógica frágil): lock interno contra corrida dentro do processo, primeiro
   salvamento via `File.Move` puro, atualização tenta `File.Replace` até 3 vezes com pequenas
   pausas antes de cair para uma troca manual com backup/rollback — o arquivo válido anterior
   nunca é apagado antes do novo conteúdo estar pronto, e é restaurado se qualquer etapa da
   troca falhar. `PxLogger` agora também grava `HResult` da exceção (nunca a mensagem nem dados
   do usuário). Os 7 manipuladores de perfil de trabalho em `ClassicWorkspaceWindow` agora
   sempre atualizam a tela mesmo quando salvar falha — a alteração já feita nunca é escondida/
   desfeita silenciosamente, e a mensagem deixa explícito que não foi persistida. Ver
   `docs/ARCHITECTURE.md`, seção 17, para o diagnóstico completo (concorrência investigada e já
   estava protegida; antivírus/permissões não atribuídos sem evidência). 5 testes novos
   (`AtomicFileWriterTests`, com bloqueio real via `FileShare.None`, nunca simulado, contra um
   diretório temporário — nunca o arquivo real, para não colidir com outras classes de teste
   sob paralelização). `dotnet test` — 68/68 (net8.0) + 273/273 (net48), confirmado em duas
   execuções completas. Nenhuma validação real dentro do CorelDRAW foi feita.
   **22ª rodada (iteração 28) — preferências portáteis do PX**: novo
   `Integrations/PortablePreferencesService.cs` — distinto do botão já existente "Exportar/
   Importar configurações..." (que copia `ListForgeIntegratedSettings` inteiro, incluindo
   perfis de trabalho com caminhos da máquina). Lista explícita, nada inventado: tamanho de
   fonte do editor e preferência de modo Clássico/PX por NOME de perfil (nunca pelo Id interno).
   Dois botões novos em Configurações ("Exportar/Importar preferências..."); importar valida
   formato/esquema/tipos campo a campo (incompatibilidade nunca derruba o arquivo inteiro),
   mostra resumo, exige confirmação, salva backup recuperável antes de aplicar (cancela se o
   backup falhar) e usa a rotina de gravação já corrigida (iteração 27) ao salvar. 14 testes
   novos (`PortablePreferencesServiceTests`). Ver `docs/ARCHITECTURE.md`, seção 18.
   **23ª rodada (iteração 29) — contrato OrderContext e primeira importação no PX**: investigado
   o código real do Nexor (`ProjetoJocasta\core\printlogs_db.py`) antes de desenhar qualquer
   contrato — achado principal: **não existe pedido de cliente no Nexor hoje** (sem número,
   cliente ou status); "pedido" é só uma etiqueta de texto livre sobre um trabalho de impressão
   dentro de um rolo. Contrato documentado em `docs/integrations/nexor-order-context.md` (+
   exemplo JSON) usando o identificador real e estável `(rollId, jobHash)`, com a lacuna
   registrada explicitamente em vez de inventada. Nenhum código nesta parte, nenhuma build —
   entrega exclusivamente documental. Em seguida, implementada a primeira importação no PX:
   `Integrations/OrderContextService.cs` (validação) + `Production/OrderContextWindow` (exibição
   só leitura, só memória, botão "Carregar contexto de pedido..." na seção Produção, funciona
   sem documento aberto) — nunca toca `nexor.db`, nunca chama o Nexor, nunca altera o documento
   Corel nem a sessão do ListForge, nunca abre o arquivo referenciado automaticamente. Mostra
   sempre "Contexto recebido — não sincronizado com o Nexor". 12 testes novos
   (`OrderContextServiceTests`). Ver `docs/ARCHITECTURE.md`, seções 19–20. `dotnet test` —
   68/68 (net8.0) + 299/299 (net48), confirmado em múltiplas execuções (duas falhas
   intermitentes vistas numa rodada — mesma contenção de paralelização já documentada em outros
   testes que tocam arquivos reais — confirmadas como não-regressão ao rodar isoladas). Nenhuma
   validação real no Corel.
   **24ª rodada (iteração 29, continuação) — exportação do contexto de pedido no Nexor**:
   implementada no repositório real do Nexor (`ProjetoJocasta`) — nova aba "Pedidos" ganhou
   botão "Exportar contexto para PX..." (`modules/registros/px_export.py`,
   `core/printlogs_db.get_order_context`), só leitura, nunca altera pedido/rolo, nunca abre o
   PX/Corel. `pedidoManual` deliberadamente omitido (nunca persistido na tabela `orders`,
   nunca inventado). 5 testes Python novos (banco SQLite temporário isolado, nunca o banco
   real) — suíte completa do Nexor: 125 passaram, nenhuma quebrada. **Compatibilidade real
   confirmada entre os dois repositórios**: um arquivo gerado pelo exportador real do Nexor foi
   validado com sucesso pelo importador real do PX (`OrderContextService.Validate`) — não só
   por inspeção de contrato. Ver `docs/ARCHITECTURE.md`, seção 21. Documentação atualizada no
   `CHANGELOG.md` do Nexor (entrada "[Não lançado]", nunca atribuída a uma versão que não é
   deste repositório para decidir). Nenhum smoke test real da interface Tkinter, nenhuma build
   nova do Nexor gerada (fora do escopo pedido).
   **25ª rodada (iteração 30) — terminologia e identidade real de rollId/jobHash**: o Nexor não
   tem pedido comercial formal (sem número/cliente/status) — ajustado todo texto visível ao
   usuário (PX e Nexor) de "pedido"/"Job" para "contexto de produção"/"trabalho de impressão"/
   "rolo"/"pedido informado" (só quando o dado existir); nomes técnicos do contrato
   (`formatId`, `rollId`, `jobHash`, `pedidoLabel`) preservados por compatibilidade. Investigada
   (não presumida) a identidade real: `jobHash = sha1(machine|end_time|document|height_mm)` —
   determinístico por conteúdo, estável ao reeditar um rolo (confirmado lendo a rotina real de
   edição); `rollId` estável por rolo; nenhuma operação de mover um trabalho entre rolos existe
   hoje (busca no repositório inteiro, nenhuma encontrada). Confirmado:
   `(rollId, jobHash)` só é único DENTRO da mesma base `printlogs.db`, nunca globalmente. Menor
   evolução compatível implementada: `source.installationId` (novo campo OPCIONAL, nunca
   quebra arquivos antigos) — id curto e opaco gerado uma vez por instalação do Nexor
   (`core/config.get_installation_id`). Ver `docs/ARCHITECTURE.md`, seção 22. Testes: +1 no
   lado Nexor (`test_px_export.py`) e +4 novos (`test_config_installation_id.py`) — suíte do
   Nexor: 130 passaram; lado PX: `OrderContextServiceTests` atualizado, suíte completa 68/68
   (net8.0) + 299/299 (net48). Compatibilidade real reconfirmada com o esquema atualizado
   (arquivo gerado pelo exportador real, aceito pelo importador real, incluindo
   `installationId`). Build PX gerada por este código ter mudado — ver versão abaixo.
   **26ª rodada (iteração 31) — installationId não resolvia o escopo da base; databaseId
   implementado**: conferido com testes reais (não presumido) se `installationId` (rodada
   anterior) de fato resolvia a ambiguidade de `(rollId, jobHash)` entre bases — não resolvia,
   em dois sentidos: a mesma instalação pode apontar para bases `.db` diferentes (mesmo
   `installationId`, bases diferentes) e `base_dir` pode ser uma pasta de rede compartilhada
   entre várias instalações (`installationId` diferente, mesma base — confirmado no comentário
   real do `journal_mode` em `core/printlogs_db.connect()`, não hipotético). Implementado
   `source.databaseId` (novo campo opcional, aditivo) — gravado DENTRO do próprio arquivo
   `printlogs.db` (`core/printlogs_db.get_database_id`, nova tabela `db_identity`, protegida
   contra corrida de criação simultânea com rowid fixo + `INSERT OR IGNORE`) — por isso
   sobrevive a cópia/restauração do arquivo e é igual para instalações diferentes acessando a
   mesma base compartilhada, ao contrário de `installationId`. Ver `docs/ARCHITECTURE.md`,
   seção 23, e `docs/integrations/nexor-order-context.md`, seção 1-B. `installationId`
   permanece no contrato (não removido), com sua documentação corrigida para não overclaimar.
   Testes: Nexor +6 novos (`tests/test_database_identity.py`, cobrindo todos os cenários reais
   pedidos) — suíte completa 136 passaram (executada duas vezes). PX:
   `OrderContextServiceTests` +1 novo teste (identificadores iguais de origens diferentes
   permanecem distintos) — suíte completa 68/68 (net8.0) + 300/300 (net48), executada três
   vezes; uma repetição teve 1 falha isolada e não relacionada
   (`SupportPackageServiceTests.Generate_NeverIncludesListContentOrInputBackups`, erro de I/O
   em `Directory.Delete`) — não uma regressão desta rodada.
   **Correção de registro (iteração 37, ver abaixo)**: esta entrada originalmente atribuía essa
   falha ao "travamento intermitente já documentado" do testhost `net48` (nota da 23ª rodada,
   acima) — conclusão NÃO sustentada, uma conflação de dois fenômenos diferentes. O erro de I/O
   em `Directory.Delete` foi demonstrado como uma causa raiz concreta e distinta (race em
   `PxLogger._logDirectoryOverride`, estado estático sem sincronização disputado por classes de
   teste diferentes rodando em paralelo por padrão no xUnit — nunca o travamento/crash real do
   processo testhost, que continua sem reprodução e sem causa determinada). Compatibilidade
   real reconfirmada (exportador real → importador real, incluindo `databaseId`). Build PX
   gerada por este código ter mudado — ver versão abaixo.
   **27ª rodada (iteração 32, Nexor) — confirmação pós-exportação com ações de arquivo**:
   depois de gravar o contexto de produção com sucesso, uma janela mostra o caminho completo e
   duas ações auxiliares — "Mostrar arquivo na pasta" (`core/paths.reveal_file_in_explorer`,
   Explorer com o arquivo já selecionado, via lista de argumentos — nunca concatenação de
   shell) e "Copiar caminho" (área de transferência, reaproveitando o padrão já existente em
   `modules/registros/ui.py`). Falha numa ação auxiliar nunca desfaz a confirmação de
   exportação — aparece como aviso à parte, na mesma janela. Nenhuma infraestrutura de
   "lembrar última pasta usada" foi criada (não existia nada equivalente no Nexor — evitado
   propositalmente, sem necessidade concreta). Testes: Nexor +5 novos
   (`tests/test_paths_reveal_file.py` ×3, `tests/test_px_export.py` +2 — caminho com
   espaços/acentos, falha de gravação propaga em vez de ser engolida) — suíte completa 141
   passaram, executada duas vezes. Nenhum código do PX mudou — nenhuma build gerada.
   **28ª rodada (iteração 32, PX) — carregar contexto de produção colando o caminho**:
   complementa o seletor de arquivo existente (sem substituí-lo) com o botão "Colar caminho do
   contexto…", útil junto com a ação "Copiar caminho" do Nexor (rodada anterior). Novo
   `Integrations/OrderContextPathLoader.cs` (lógica pura, mesma validação
   `OrderContextService.Validate` do seletor — nunca uma segunda validação divergente) e
   `Production/OrderContextPathDialog.xaml(.cs)` (`ThreadKeyboardRouter`, mesmo padrão de
   `WorkProfileNameDialog` — sem hooks globais). Trata caminho vazio, diretório, inexistente,
   ilegível e JSON incompatível; aceita aspas externas copiadas do Windows e espaços/acentos;
   nunca interpreta o texto como comando nem tenta acesso de rede (um texto com aparência de
   URL só falha como "não encontrado", igual a qualquer caminho inexistente); nunca persiste o
   caminho; colar nunca dispara o carregamento por conta própria, só o clique em "Carregar".
   Falha de leitura preserva o contexto já carregado (a janela de contexto só é tocada depois
   que o diálogo retorna com sucesso). Ver `docs/ARCHITECTURE.md`, seção 24. Testes:
   `OrderContextPathLoaderTests` (novo, 13 testes, incluindo equivalência explícita com o
   seletor existente) — suíte completa 68/68 (net8.0) + 313/313 (net48), executada duas vezes.
   Build PX gerada por este código ter mudado — ver versão abaixo.
   **29ª rodada (iteração 37) — causas raiz reais das falhas intermitentes da suíte, não
   "flakiness ambiental"**: pedido explícito do usuário pra investigar com evidência, não
   presumir. Três causas DIFERENTES, todas demonstradas por análise de código e confirmadas por
   reprodução real, nenhuma delas a mesma coisa que o travamento intermitente do testhost
   `net48` ainda sem causa determinada (nota da 23ª rodada acima):
   (1) `PxLogger._logDirectoryOverride` é um campo ESTÁTICO de processo, sem sincronização,
   mutado por `SetLogDirectoryForTesting` — `PxLoggerTests`/`SupportPackageServiceTests` (duas
   classes = duas coleções xUnit = paralelas por padrão) disputavam esse campo, explicando o
   `DirectoryNotFoundException` ao apagar "pxtoolbox-sp-logs-&lt;guid&gt;" (a pasta nunca
   chegava a ser criada porque o override já tinha sido trocado por outro teste) e as contagens
   erradas de `Retention_WhenTotalExceedsMaxBytes_...`.
   (2) `ListForgeIntegratedSettingsStoreTests`/`NexorConfigStoreTests`/`ListForgeConfigStoreTests`
   tocam o MESMO diretório real `%LocalAppData%\PXToolBox\Dev\config\`, cada uma sua própria
   coleção paralela por padrão — reproduzido de verdade
   (`ListForgeConfigStoreTests.SaveAndRead_RoundTrips_...` com IOException rodando em paralelo
   com as outras duas antes da correção).
   (3) `AtomicFileWriter.Sync` é um ÚNICO lock estático de processo pra TODO destino (não por
   caminho) — `AtomicFileWriterTests`/`ListForgeIntegratedSettingsStoreTests` (via Store.Save)/
   `ListForgeProcessingHistoryServiceTests` (via Service.Save) disputam esse mesmo lock; o teste
   de retentativa (`WriteTextSafely_TransientLockOnDestination_...`) também usava
   `System.Threading.Timer` (ThreadPool) pra liberar um lock artificial — vulnerável a
   starvation do ThreadPool sob carga paralela de QUALQUER classe, não só do próprio grupo.
   **Corrigido**: três classes agrupadas em `[Collection("PxLogger static test seam")]`
   (1) e cinco classes em `[Collection("PXToolBox Dev shared filesystem resources")]` (2 e 3) —
   nunca desabilita paralelismo globalmente, só entre as classes que comprovadamente disputam
   cada recurso (`TestCollections.cs`, novo). Timer trocado por uma Thread dedicada
   (`Thread.Sleep`, não sujeita à fila do ThreadPool). Depois de corrigir (1)-(3), mais duas
   falhas reproduzidas com stack trace completo (`IOException: ...sendo usado por outro
   processo`, em `File.WriteAllText(RealFilePath, ...)` e `File.ReadAllText(quarantine)`,
   ambas em `ListForgeIntegratedSettingsStoreTests`) — um bloqueio transitório de ORIGEM NÃO
   IDENTIFICADA (nenhum processo responsável confirmado — não atribuído a antivírus/indexador
   nem a nenhum outro processo específico), pela mesma razão que `AtomicFileWriter` em produção
   também nunca presume a causa de um bloqueio transitório.
   Corrigido com um retry pequeno e justificado (`TestFileIO.cs`, novo — 3 tentativas,
   0/50/150ms), mesma categoria de tolerância que `AtomicFileWriter` já tem em produção para o
   mesmo tipo de erro, nunca escondendo uma race da ARQUITETURA (essas já foram eliminadas pelas
   coleções acima). **Correção de registro**: a entrada da 23ª rodada (`SupportPackageServiceTests.
   Generate_NeverIncludesListContentOrInputBackups`, acima) atribuía erroneamente esse padrão de
   falha ao travamento intermitente do testhost — não sustentado, são fenômenos diferentes (ver
   nota ali). O travamento/crash real do processo testhost CONTINUA sem reprodução nesta
   investigação e sem causa determinada — não atribuído a nenhuma das três causas acima.
   Verificação: suíte completa rodada repetidamente depois de cada correção (15 execuções
   consecutivas sem nenhuma falha depois da correção final, contra falhas em praticamente toda
   execução antes). Mudança só em testes — nenhum código de produção alterado, nenhuma versão
   DEV nova gerada (ver AGENTS.md). Ver `docs/ARCHITECTURE.md`, seção 31.
   **30ª rodada (mesma iteração) — auditoria da correção acima**: pedido explícito do usuário
   pra revisar antes de encerrar. `TestFileIO.Retry` auditado: só captura `IOException`, nunca
   uma asserção, e sempre propaga sem capturar na última tentativa — nenhuma falha mascarada.
   Causa (2) acima deixou de ser "serializada" e passou a ser ELIMINADA de verdade:
   `ListForgeIntegratedSettingsStore`/`ListForgeConfigStore`/`NexorConfigStore` ganharam um seam
   de teste (`SetConfigDirectoryForTesting`/`SetFilePathForTesting`, mesmo padrão de
   `PxLogger.SetLogDirectoryForTesting`) que redireciona cada classe pra um diretório temporário
   isolado por instância — nunca mais `%LocalAppData%\PXToolBox\Dev\config\` real, nem mesmo com
   o PX Tool Box DEV aberto de verdade na mesma máquina (risco que a serialização entre classes
   nunca cobria). Isso introduziu uma disputa NOVA da mesma categoria da causa (1) — dois campos
   estáticos de teste sem sincronização, compartilhados cada um por duas classes — encontrada e
   corrigida ANTES de qualquer execução: `ListForgeIntegratedSettingsStoreTests`/
   `ListForgeProcessingHistoryServiceTests` permanecem juntas (justificativa reescrita);
   `ListForgeConfigStoreTests`/`NexorConfigStoreTests` ganharam coleção própria
   (`"ListForge/Nexor launcher config-path static test seam"`). `AtomicFileWriterTests` saiu de
   qualquer coleção (seus testes já são isolados; o lock `Sync` se auto-sincroniza). Cobertura
   confirmada por busca em todo `tests/PXToolBox.Tests` — nenhum outro consumidor desses campos.
   **Mudança de produção desta vez**: os três seams novos são API pública nova no host
   (`PXToolBoxDev.dll`) — por isso uma nova build DEV foi gerada e validada: `0.1.0-dev.44`
   (pipeline do `Build-Release.ps1` roda a suíte e passou; instalador compilado; builds
   anteriores preservadas em `dist\dev\`) — sem smoke test real no CorelDRAW. Verificação: 5
   execuções consecutivas (96/96 net8.0 + 344/344 net48 em cada) depois de todas as mudanças,
   mais a execução embutida no pipeline da build — nenhuma repetição em resposta a falha, todas
   passaram de primeira. `Build-Release.ps1 -Channel Stable` confirmado continuando a recusar.
   Ver `docs/ARCHITECTURE.md`, seção 31, "Revisão (auditoria pós-correção, mesma iteração)".
   **31ª rodada — Camadas**: "Ocultar camadas dos objetos selecionados" (`0.1.0-dev.45`) e o
   complemento "Mostrar últimas camadas ocultadas" (`0.1.0-dev.46`) — restaura só as camadas
   que a última ocultação efetiva do PX mudou, no documento de origem, localizadas por marcação
   em `Layer.Properties`/`Document.Properties` (nunca índices nem referências COM guardadas).
   Compilação real, suíte automatizada real (116/116 net8.0 + 364/364 net48) e instalador DEV
   gerado; **smoke test no CorelDRAW pendente para dev.45 e dev.46** (`docs/mvp-smoke-test.md`,
   4i). Ver `docs/ARCHITECTURE.md`, seção 32.
   **32ª rodada — correção da dev.46 (`0.1.0-dev.47`)**: a dev.46 gravava a marcação em
   `Document.Properties`/`Layer.Properties` (ia para o CDR ao salvar), contrariando o requisito
   de registro só em memória. Agora o registro é só em memória, identificando documento/camadas
   pelo objeto COM da sessão (comparação por referência, nunca invocado); fechar o documento
   invalida, reabrir não recupera, identidade ambígua invalida sem alterar nada. As abstrações de
   camada/documento não expõem mais `Properties`. Auditoria das marcações da dev.46 em
   `docs/ARCHITECTURE.md`, 32.1 (dev.45/46 nunca instaladas nesta máquina — instalada: dev.43;
   nenhuma limpeza automática). Experimento de identidade no Corel real não rodou
   (`MK_E_UNAVAILABLE` ao conectar de fora); smoke test pendente para dev.45/46/47.
   **33ª rodada — diagnóstico de identidade no add-on (`0.1.0-dev.48`)**: Diagnóstico →
   "Diagnóstico de identidade de camadas…" verifica, num documento temporário e com o mesmo
   critério de identidade do serviço, acessos repetidos, renomear/reordenar, excluir/recriar,
   Desfazer, fechar/reabrir e o descarte do registro; o painel solta o registro ao ser fechado.
   Também: estado do Tamanho Padrão no próprio botão (sem linha de texto) e "Preparar Grid" com
   aparência comum. Compilação e testes automatizados reais; diagnóstico e smoke test **não
   executados** no Corel (`docs/mvp-smoke-test.md`, 4e passo 6 — Tamanho Padrão — e 4i). Ver
   `docs/ARCHITECTURE.md`, 32.3 e 33. **Validação real recebida**: dev.48 no CorelDRAW
   27.1.0.129, diagnóstico de identidade executado pelo usuário sem falhas (ocultar/restaurar
   pelo serviço real, identidade após renomear/reordenar, descarte ao fechar/reabrir). Não
   aprova o 4i inteiro; fechar/reabrir o docker continua pendente.
   **34ª rodada — layout do Diagnóstico (`0.1.0-dev.49`)**: botões em uma coluna, com rótulos
   curtos e quebra de linha, sem cortes nem espaços vazios. Verificado só por renderização fora
   do Corel (`docs/ARCHITECTURE.md`, 34).
   **35ª rodada — Favoritos do painel (`0.1.0-dev.50`, corrigida na `0.1.0-dev.51`)**: primeira
   etapa de personalização. Use a dev.51: a dev.50 gravava `"PanelFavoriteToolIds": null` em
   configurações sem favoritos (inofensivo).
   - Seção "Favoritos" no topo; adicionar/remover pelo menu de contexto das ferramentas
     compatíveis; ordenar com ▲/▼.
   - Cada atalho dispara o clique do botão original e segue o estado e a habilitação dele.
   - Salvo por Id estável no arquivo de configuração do PX DEV (campo aditivo) e incluído nas
     preferências portáteis.
   - Escudo/Bandeira/Faixa de Capitão ficam fora (dependem de campos auxiliares).
   - Verificação: testes automatizados e renderização fora do Corel; smoke test **pendente**
     (`docs/mvp-smoke-test.md`, 4j).
   - Próximas etapas possíveis: arrastar e soltar (feito na dev.66), mais seções no catálogo,
     layout/seções personalizáveis (feito na dev.53).
   **36ª rodada — relatório do Diagnóstico contido (`0.1.0-dev.52`)**: ao terminar, o teste de
   identidade mostra só um resumo do resultado real; o relatório completo fica em "Ver
   relatório" (área recolhível, altura limitada, rolagem própria) e "Copiar relatório". Lógica do
   diagnóstico inalterada. Verificado por testes e renderização fora do Corel
   (`docs/ARCHITECTURE.md`, 34.1); smoke test pendente (4i passo 0 — Diagnóstico).
   **37ª rodada — Personalizar painel e favoritos em ícone (`0.1.0-dev.53`)**: "Personalizar
   painel…" no cabeçalho escolhe ordem e visibilidade das seções (rascunho até Salvar; Cancelar
   descarta; "Restaurar layout padrão" não mexe nos favoritos). Seções só reordenadas/ocultadas,
   sem recriar o docker nem perder o estado dos controles. Layout global por Id estável no mesmo
   arquivo dos favoritos e nas preferências portáteis; seções novas entram visíveis no fim;
   importação sem layout mantém o atual. A pedido do usuário, os favoritos viraram ícones
   quadrados com o nome na dica e mover/remover no botão direito. Verificação: 20 testes novos,
   suíte completa (128 net8.0 + 425 net48) e renderização/exercício do painel compilado fora do
   Corel; smoke test **pendente** (`docs/mvp-smoke-test.md`, 4j e 4k). Ver
   `docs/ARCHITECTURE.md`, 35.1 e 36.
   **Após a dev.53 (entra na próxima build DEV)**: a legenda da seção Camadas passa a quebrar
   linha em docker estreito (era cortada em 300 px); MA/FE/C continuam alinhados. Conferido
   carregando o painel compilado fora do Corel; não verificado no Corel (`docs/ARCHITECTURE.md`,
   36.1).
   **38ª rodada — Remover códigos da página por código (`0.1.0-dev.55`)**: na seção Códigos, além
   de "Remover NO/NU da página" (mantido), botões NO/NU/AP/TS para remover um código por vez da
   página ativa e "Remover todos os códigos (NO/NU/AP/TS) da página" — mesma descida em grupos e
   PowerClips. AP/TS recebem "Texto Apelido"/"Texto Tipo Sanguíneo" (decisão do usuário). A
   legenda da alternância de códigos também passou a quebrar linha. Testes automatizados e painel
   carregado fora do Corel; smoke test **pendente** (`docs/mvp-smoke-test.md`, 4l). Ver
   `docs/ARCHITECTURE.md`, 37.
   **39ª rodada — todos os botões favoritáveis (`0.1.0-dev.56`)**: o catálogo de favoritos passou
   de 10 para 44 ferramentas (todo botão de ferramenta das seções, inclusive Agrupar Interseção,
   Escudo/Bandeira/Faixa, Tipo de peça, Medidas, Listas, Produção e Diagnóstico), com ícone
   próprio; Ids da dev.50 inalterados. Testes automatizados e painel carregado fora do Corel;
   smoke test **pendente** (`docs/mvp-smoke-test.md`, 4j passo 12). Ver `docs/ARCHITECTURE.md`,
   35.2.
   **40ª rodada — dicas legíveis (`0.1.0-dev.57`)**: a dica dos ícones dos Favoritos tinha texto
   claro sobre fundo claro (relato do usuário na dev.56). As dicas do painel ganharam template
   escuro com quebra de linha. Defeito reproduzido e correção conferida com as dicas abertas fora
   do Corel; smoke test **pendente** (`docs/mvp-smoke-test.md`, 4j passo 3). Ver
   `docs/ARCHITECTURE.md`, 35.3.
   **Rodada da `0.1.0-dev.58`**: pasta de saída padrão por máquina (Preparação e saída);
   contraste da caixa de filtro da comparação do ListForge Integrado corrigido; testes da
   janela do ListForge Integrado não gravam mais na configuração real. Smoke test **pendente**
   (`docs/mvp-smoke-test.md`, 4n). Ver `docs/ARCHITECTURE.md`, 39.
   **41ª rodada — Notas da versão (`0.1.0-dev.59`)**: botão no rodapé abre uma janela com
   Implementado, Alterado e Removido da versão em execução. Catálogo editorial embutido no
   assembly, consultado por número exato; build local ou versão sem entrada mostra aviso sem
   herdar notas de outra versão. Rolagem e quebra de linha, inclusive nos itens do template.
   Testes do catálogo e renderização fora do Corel; smoke test real **pendente**
   (`docs/mvp-smoke-test.md`, 4m). Manutenção do catálogo documentada no README.
   **42ª rodada — Notas da versão em Diagnóstico (`0.1.0-dev.60`)**: o botão saiu do rodapé e
   fica no início de Diagnóstico; também pode virar favorito. Smoke test **pendente**
   (`docs/mvp-smoke-test.md`, 4m). Ver `docs/ARCHITECTURE.md`, 38.1.
   **43ª rodada — Aplicar contorno do Grid (`0.1.0-dev.61`)**: a macro, já usada dentro do
   Preparar Grid Novo, virou botão próprio na seção Grid (mesmo estilo, todas as páginas, sem
   tocar nas camadas especiais). Também: testes não gravam mais no log real. Smoke test
   **pendente** (`docs/mvp-smoke-test.md`, 4o). Ver `docs/ARCHITECTURE.md`, 40.
   **44ª rodada — resumo do contexto de produção (`0.1.0-dev.62`)**: seção Produção mostra rolo,
   trabalho de impressão e (se houver) pedido informado do contexto carregado, com "Ver
   detalhes" e "Limpar contexto"; importação nova só substitui em caso de sucesso. Corrigido:
   arquivos do exportador real sem pedido (`pedidoLabel` vazio) eram recusados. Smoke test
   **pendente** (`docs/mvp-smoke-test.md`, 4h). Ver `docs/ARCHITECTURE.md`, 41.
   **45ª rodada — usar o contexto no nome do layout (`0.1.0-dev.63`)**: "Usar contexto de
   produção" em Preparação e saída preenche "Nome do layout" com o pedido informado (ou o
   nome do arquivo de origem). Smoke test **pendente** (`docs/mvp-smoke-test.md`, 4h). Ver
   `docs/ARCHITECTURE.md`, 41.1.
   **46ª rodada — contexto em mais ferramentas (`0.1.0-dev.64`)**: "Usar contexto de
   produção" também em Renomear Objetos; "Nomear página ativa com o contexto" (Produção);
   Escudo/Bandeira/Faixa salvos na subpasta do rolo (opção, padrão marcada); rolo/trabalho na
   mensagem e no log da saída. Smoke test **pendente** (`docs/mvp-smoke-test.md`, 4h passo
   7). Ver `docs/ARCHITECTURE.md`, 41.2.
   **47ª rodada — resultado de execução PX → Nexor (`0.1.0-dev.65`)**: "Exportar resultado
   para o Nexor…" grava `PxExecutionResult` v1 (saídas salvas com o contexto carregado; sem
   status). Leitura no Nexor pendente. Ícones de remoção em vermelho nos Favoritos. Smoke
   test **pendente** (`docs/mvp-smoke-test.md`, 4h passo 8). Ver `docs/ARCHITECTURE.md`, 41.3.
   **48ª rodada — Favoritos: arrastar e soltar (`0.1.0-dev.66`)**: reordenar os ícones
   arrastando com o botão esquerdo (barra de inserção; soltar fora cancela; clique curto continua
   sendo clique; sem OLE, nada chega ao documento). O menu do botão direito continua. Smoke test
   **pendente** (`docs/mvp-smoke-test.md`, 4j passo 13). Ver `docs/ARCHITECTURE.md`, 35.4.
   **49ª rodada — contextos de produção recentes (`0.1.0-dev.67`)**: "Recentes…" na seção
   Produção (e nos Favoritos) recarrega um dos últimos 10 arquivos de contexto — relidos e
   validados de novo. Por decisão do usuário, só os caminhos ficam salvos nesta máquina; o
   conteúdo do contexto continua só em memória. Smoke test **pendente**
   (`docs/mvp-smoke-test.md`, 4h passo 9). Ver `docs/ARCHITECTURE.md`, 41.4.
   **50ª rodada — comparação antes/depois (`0.1.0-dev.68`)**: primeiro passo das versões PX das
   ferramentas do SISBolt (Esticar no Tamanho do Molde, Resetar ângulo, Criar grupo com item
   único, Ignorar métricas, Alinhamento, Trocar de Posição, Replicar PowerClip, Formato de
   exportação, Alinhamento horizontal automático, Mão Livre). Só o Tamanho Padrão tem contrato
   conhecido; Diagnóstico → "Capturar estado"/"Comparar com a captura" registra o que cada
   ferramenta muda. **Aguardando os relatórios do usuário** (`docs/sisbolt-tools-investigation.md`).
   Ver `docs/ARCHITECTURE.md`, 42.
   **Correção da comparação (`0.1.0-dev.69`)**: fator 2,54 na geometria quando `Document.Unit`
   muda entre as capturas. Unidade da régua e unidade efetiva registradas por captura; geometria
   convertida antes de comparar; unidade desconhecida → inconclusivo. Premissa
   "Position*/Size* seguem `Document.Unit`" ainda **não confirmada no Corel** fora de polegada
   (`docs/mvp-smoke-test.md`, 4p passo 7). Ver `docs/ARCHITECTURE.md`, 42.1.
   **51ª rodada — ferramentas do SISBolt com contrato observado (`0.1.0-dev.70`)**: Ativar
   Esticar no Tamanho do Molde, Ativar Ignorar métricas da variável, Ativar Mão Livre, Criar
   grupo com item único (nome provisório) e Alinhamento horizontal automático (referência sem
   GUID). Pendentes de captura: Resetar ângulo, Alinhamento, Trocar de Posição, Replicar
   PowerClip, Formato de exportação e as desativações. Smoke test e reconhecimento na montagem
   **pendentes** (`docs/mvp-smoke-test.md`, 4q). Ver `docs/ARCHITECTURE.md`, 43.
   **Correção (`0.1.0-dev.71`)**: arrastar os Favoritos não funcionava (dev.66–dev.70) — o
   arraste se cancelava ao começar. Ver `docs/ARCHITECTURE.md`, 35.4.
   **`0.1.0-dev.72`**: Esticar no Tamanho do Molde, Ignorar métricas e Mão Livre alternam e
   mostram o estado da seleção como o Tamanho Padrão (desligar remove a entrada — desativação
   do SISBolt ainda não capturada). Ver `docs/ARCHITECTURE.md`, 43.
   **52ª rodada — seções e Favoritos com nome (`0.1.0-dev.73`)**: seção Montagem, nomes curtos,
   nova ordem padrão (layout salvo mantido, seção nova junto da vizinha); "Mostrar nomes" nos
   Favoritos; nenhum texto visível com "SISBolt". Smoke test **pendente** (4j passo 15). Ver
   `docs/ARCHITECTURE.md`, 44.
   **`0.1.0-dev.74`**: Montagem — "Alternar Esticar no Tamanho do Molde/Ignorar métricas da
   variável/Mão Livre", no padrão de "Alternar Tamanho Padrão" (comportamento já era de
   alternância desde a dev.72).
   **`0.1.0-dev.75`**: Alternar Mão Livre só liga em textos NO, NU, AP ou TS.
   **53ª rodada — personalizar painel (`0.1.0-dev.76`)**: renomear seções, criar/remover
   seções, escolher a seção de cada ferramenta (atalho completo no destino, original oculto na
   origem), marcas pelo nome atual; "Mostrar nomes" como chave. Smoke test **pendente** (4k passo
   12). Ver `docs/ARCHITECTURE.md`, 45.
   **`0.1.0-dev.77`**: subtítulo e linhas de grupo esvaziado somem; janela de personalização
   mais legível. Ver `docs/ARCHITECTURE.md`, 45.1.
   **`0.1.0-dev.78`**: Exportar/Importar preferências levam a personalização do painel e o
   "Mostrar nomes". Smoke test **pendente** (4g passo 6). Ver `docs/ARCHITECTURE.md`, 45.2.
   **`0.1.0-dev.79`**: Diagnóstico → "Roteiro de capturas…" com as 27 capturas pendentes das
   ferramentas de montagem e "Salvar na pasta de capturas" na comparação. Smoke test
   **pendente** (4p passo 8). Ver `docs/ARCHITECTURE.md`, 46.
   **`0.1.0-dev.80`**: Montagem → "Resetar ângulo", mecanismo do "Zerar ângulo" do MoldMotor
   portado (sem dependência), preservando nome e marcações. Smoke test **pendente** (4q passo
   9). Ver `docs/ARCHITECTURE.md`, 47.
   **`0.1.0-dev.81`**: padrão antigo e novo de camadas — Preparar Molde/Grid Novo nos dois
   padrões e "Converter documento: padrão antigo ↔ novo". Smoke test **pendente** (4r). Ver
   `docs/ARCHITECTURE.md`, 48.
   **`0.1.0-dev.82`**: Cadastro de moldes, fase 1 (ferramentas do MoldMotor reescritas no PX, sem
   os assistentes): Criar camadas para a seleção, maiúsculas, Excluir camadas vazias, Ordenar e
   distribuir, Criar Grid, Selecionar RECT. Próximas fases: configurações; Definir objetos,
   Detectar em lote, Propagar logos, duplicar/espelhar; Salvar molde. Smoke test **pendente**
   (4s). Ver `docs/ARCHITECTURE.md`, 49.
   **`0.1.0-dev.83`**: fase 2 — "Configurações de moldes…" (tamanhos por categoria, códigos, Grid,
   laser/TAG/identificador), próprias do PX. Smoke test **pendente** (4s passo 8). Ver
   `docs/ARCHITECTURE.md`, 50.
   **`0.1.0-dev.84`**: fase 3a — "Definir objetos" (grupo do tamanho com TAG, logos L1…, peça e
   cópia x do laser; numeração herdada de outro tamanho; redefinir). Smoke test **pendente** (4t).
   Próximas: Detectar em lote, Propagar logos, duplicar/espelhar. Ver `docs/ARCHITECTURE.md`, 51.
   **`0.1.0-dev.85`**: fase 3b — "Detectar objetos em lote" (conjuntos pela posição dos logos,
   tamanhos pela ordem de área, como o MoldMotor). Smoke test **pendente** (4u). Ver
   `docs/ARCHITECTURE.md`, 52.
   **`0.1.0-dev.86`**: fase 3c — "Propagar logos" (grupos já montados; referência = o com mais logos,
   empate pelo Tamanho do painel; escala pela altura da peça, como o MoldMotor). Smoke test
   **pendente** (4v). Ver `docs/ARCHITECTURE.md`, 53. Próximas: duplicar/espelhar; Salvar molde.
   **`0.1.0-dev.87`**: fase 3d — "Duplicar peça" (cópia de cada tamanho ao lado, espelho só da peça e
   do laser, logos com números novos; repetir substitui). Smoke test **pendente** (4w). Ver
   `docs/ARCHITECTURE.md`, 54. Próxima: fase 4, Salvar molde.
   **`0.1.0-dev.88`**: fase 4 — "Salvar molde…" (nome digitado → .cdr; documento inteiro, que passa a
   ser o arquivo salvo). Smoke test **pendente** (4x). Ver `docs/ARCHITECTURE.md`, 55. Com isso as
   fases do Cadastro de moldes estão todas no painel (sem os assistentes).
   **`0.1.0-dev.89`**: configurações de moldes em Exportar/Importar preferências (campo aditivo
   `moldSettings`; inválidas ignoradas inteiras). Smoke test **pendente** (4y). Ver
   `docs/ARCHITECTURE.md`, 56.
   **`0.1.0-dev.90`**: posição (nove) e margens (cm) da TAG nas Configurações de moldes, usadas pelo
   Definir objetos/lote (fábrica = como antes). Smoke test **pendente** (4z). Ver
   `docs/ARCHITECTURE.md`, 57. Falta do MoldMotor: contorno interno por cor de tamanho.
   **`0.1.0-dev.91`**: contorno interno por cor de tamanho (peça com contorno por dentro, cor do
   tamanho, estilo da categoria; cópia do laser intacta). Smoke test **pendente** (4aa). Ver
   `docs/ARCHITECTURE.md`, 58. Com isso os itens planejados do Cadastro de moldes estão no PX
   (fora: o metadado em Shape.Comment, a sincronização do objeto identificador e a validação de molde).
   **`0.1.0-dev.92`**: Diagnóstico → "Manual de uso" (manual de todas as ferramentas, com filtro;
   teste exige texto para toda ferramenta do catálogo). Smoke test **pendente** (4ab). Ver
   `docs/ARCHITECTURE.md`, 59.
   **`0.1.0-dev.93`**: Cadastro de moldes → "Validar molde" (só leitura: peça não reconhecida, logo
   repetido, mesmo número em peças diferentes, numeração trocada, logo faltando ou fora do lugar,
   tamanho repetido, sem TAG/laser). Smoke test **pendente** (4ac). Ver `docs/ARCHITECTURE.md`, 60.
   Falta do MoldMotor: o metadado em Shape.Comment e a sincronização do objeto identificador.
   **`0.1.0-dev.94`**: Medidas → régua em tamanho real da largura, da altura ou das duas (trena com mm
   e cm, camada RÉGUA, distância do objeto no painel). **Aprovada pelo usuário no Corel** ("deu bom"),
   com um ajuste: fundo branco. Ver `docs/ARCHITECTURE.md`, 61.
   **`0.1.0-dev.95`**: faixa da régua branca em vez de amarela (só a cor). Smoke test **pendente**
   (4ad passo 1) — conferir a cor da faixa.
   **`0.1.0-dev.96`**: "Buscar ferramenta…" no topo do painel (resultados por seção, atalhos dos
   botões originais) e Montagem → "Planejar divisão (Slice)…" (prévia por linhas/colunas ou tamanho
   máximo, com sobreposição; sem recorte), integrado do patch de referência com ajustes. Smoke test
   **pendente** (4ae, 4af). Ver `docs/ARCHITECTURE.md`, 62 e 63.
   **`0.1.0-dev.97`**: Slice divide no documento — "Dividir no Corel" cria cada parte como PowerClip
   com uma cópia da arte, afastamento opcional, original oculto ou apagado, um Ctrl+Z (botão agora
   "Dividir em partes (Slice)…"). Smoke test **pendente** (4ag). Ver `docs/ARCHITECTURE.md`, 64.
   **`0.1.0-dev.98`**: Montagem → "Encaixar peças (retangular)…" — encaixe pela caixa na página ou em
   rolo de largura fixa, margem/distância, giro de 90° opcional, prévia com aproveitamento, aplicar com
   um Ctrl+Z. Smoke test **pendente** (4ah). Ver `docs/ARCHITECTURE.md`, 65.
   **`0.1.0-dev.99`**: "Encaixar peças…" ganha o encaixe pelo contorno real (grade com precisão em mm,
   furos e recortes livres, giro 0/180 ou 0/90/180/270, conferência independente nos polígonos antes
   de aplicar). Smoke test **pendente** (4ai). Ver `docs/ARCHITECTURE.md`, 66.
   **`0.1.0-dev.100`**: Slice pelas guias da página (cada guia horizontal/vertical que cruza a arte é uma
   emenda) e "Criar guias do plano"/"Remover guias do Slice". Smoke test **pendente** (4aj). Ver
   `docs/ARCHITECTURE.md`, 67.
   **`0.1.0-dev.101`**: "Encaixar peças…" com quantidade por peça (cópias criadas só se couberem, um
   Ctrl+Z). Smoke test **pendente** (4ak). Ver `docs/ARCHITECTURE.md`, 68.
   **`0.1.0-dev.102`**: "Encaixar peças…" em várias áreas digitadas (retalhos), enchidas em ordem, com as
   áreas desenhadas no Corel. Smoke test **pendente** (4al). Ver `docs/ARCHITECTURE.md`, 69.
   **`0.1.0-dev.103`**: "Encaixar peças…" com "Procurar o melhor encaixe" (5 ordens, tempo máximo) e
   cálculo pelo contorno em segundo plano com "Cancelar cálculo". Smoke test **pendente** (4am). Ver
   `docs/ARCHITECTURE.md`, 70.
   **`0.1.0-dev.104`**: "Encaixar peças…" em várias áreas com "Preencher as áreas" (digitada, menores ou
   maiores primeiro). Smoke test **pendente** (4an). Ver `docs/ARCHITECTURE.md`, 71.
   **`0.1.0-dev.105`**: "Encaixar peças…" com perfis salvos (campos da janela por nome, neste computador).
   Smoke test **pendente** (4ao). Ver `docs/ARCHITECTURE.md`, 72.
   **`0.1.0-dev.106`**: Exportar/Importar preferências levam os perfis do encaixe. Smoke test **pendente**
   (4ap). Ver `docs/ARCHITECTURE.md`, 73.
   **`0.1.0-dev.107`**: Slice com o contorno da arte na prévia. Smoke test **pendente** (4aq). Ver
   `docs/ARCHITECTURE.md`, 74.
   **`0.1.0-dev.108`**: Slice com corte livre por guias inclinadas (partes poligonais). Smoke test
   **pendente** (4ar). Ver `docs/ARCHITECTURE.md`, 75.
   **`0.1.0-dev.109`**: Encaixe começando por qualquer canto da área. Smoke test **pendente** (4as). Ver
   `docs/ARCHITECTURE.md`, 76.
   **`0.1.0-dev.110`**: Encaixe comparado com o arranjo original (relatório e prévia). Smoke test **pendente**
   (4at). Ver `docs/ARCHITECTURE.md`, 77.
   **`0.1.0-dev.111`**: Encaixe com rolo em folhas (largura × altura, folhas uma embaixo da outra) e "Quantidade
   por peça" opcional (desligada por padrão). Smoke test **pendente** (4au). Ver `docs/ARCHITECTURE.md`, 78.
   **`0.1.0-dev.112`**: Encaixe pelo contorno em fileiras alinhadas (comparação com o eCut), cálculo mais rápido
   com peças repetidas, janela abre com as últimas opções e explica quando nada cabe. Smoke test **pendente**
   (4av). Ver `docs/ARCHITECTURE.md`, 79.
   **`0.1.0-dev.113`**: Encaixe com "Agrupar por bloco, lado a lado" (cada área/folha vira um grupo; folhas do rolo
   lado a lado). Smoke test **aprovado** na dev.117 (4aw, depois da correção abaixo). Ver `docs/ARCHITECTURE.md`, 80.
   **`0.1.0-dev.114`**: Encaixe no rolo "Separar por camada" (um bloco por tecido, lado a lado). Smoke test
   **aprovado** na dev.118 (4ax, depois da correção abaixo). Ver `docs/ARCHITECTURE.md`, 81.
   **`0.1.0-dev.115`**: Slice "Pelas linhas desenhadas" — cortes curvos e que param no meio da arte, na camada
   "Cortes (Slice)". Smoke test **aprovado** na dev.118 (4ay). Ver `docs/ARCHITECTURE.md`, 82.
   **`0.1.0-dev.116`**: Slice "Mostrar a arte em cores" (miniatura com cores na prévia). Smoke test **aprovado**
   na dev.118 (4az; arte pesada não medida). Ver `docs/ARCHITECTURE.md`, 83.
   **`0.1.0-dev.117`**: correção — agrupar um objeto sozinho (`Shape.Group()` de um item dá `E_FAIL` no Corel 2025),
   achada no smoke 4aw (passo 1 aprovado na dev.116). Ver `docs/ARCHITECTURE.md`, 84. 4aw aprovado na dev.117
   (passos 1, 2 e desfazer).
   **`0.1.0-dev.118`**: correção — o grupo ia para a camada do último objeto selecionado (smoke 4ax). Ver
   `docs/ARCHITECTURE.md`, 85. 4ax aprovado na dev.118.
   **`0.1.0-dev.119`**: encaixe — distância entre os blocos digitada e legenda em cada bloco (nome, medidas e
   peças). Smoke test pendente (4ba). Ver `docs/ARCHITECTURE.md`, 86.
2. **Nexor**: compartilhar contexto de pedido, navegar com o pedido carregado, comunicar
   resultados de execução (o Nexor continua validando as transições) — abrir o standalone (sem
   incorporar interface nem contexto de pedido) já está implementado desde a 18ª rodada acima.
3. **Moldes**: módulo Moldes do PX integrado, evoluindo separado do MoldMotor (sem
   sincronização presumida de melhorias entre os dois).
4. Servidor de licenças, sincronização em nuvem e cliente de atualização automática completo
   — pontos de extensão documentados, não implementados.
