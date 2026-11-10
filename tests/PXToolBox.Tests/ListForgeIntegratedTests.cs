#if NET48
// Só compila na perna net48 do projeto de testes (ver PXToolBox.Tests.csproj) — é o único
// framework onde o host do PX Tool Box (PXToolBoxDev, net48/WPF) e sua referência ao núcleo
// compartilhado do ListForge (ListForge.Core, via ListForgeCoreProjectPath) estão disponíveis.
//
// Objetivo destes testes: comparar o resultado do processamento feito pelo modo Clássico do
// ListForge Integrado (host do PX) com o mesmo processamento feito pelo ListForge standalone,
// usando exatamente as mesmas entradas e opções (ver tarefa, seção 6 — "não declare
// equivalência apenas porque as duas interfaces usam a mesma biblioteca"). As entradas e o
// resultado esperado do primeiro teste são copiados literalmente de
// ListForge/ListForge.Tests/MainFlowIntegrationTests.cs (ValidInput_ValidatesProcessesBuildsTextOutputAndJson)
// — o mesmo ProcessingWorkflowService, agora consumido através do host do PX.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using PXToolBoxDev.ListForgeIntegrated;
using Xunit;

namespace PXToolBox.Tests
{
    // Constrói ClassicWorkspaceWindow, que lê e grava a configuração do ListForge Integrado
    // (ex.: trocar de modo salva PreferredWorkspaceModeByProfileId) e registra no log ("ListForge Integrado iniciado"). Sem isto, cada execução da
    // suíte — inclusive a de toda build — gravava no arquivo REAL de %LocalAppData%. Mesmo
    // isolamento de ListForgeIntegratedSettingsStoreTests (ver TestCollections.cs).
    [Collection("PXToolBox Dev shared filesystem resources")]
    public class ListForgeIntegratedTests : IDisposable
    {
        private readonly string _isolatedConfigDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "lf-integrated-" + Guid.NewGuid().ToString("N"));

        public ListForgeIntegratedTests()
        {
            Directory.CreateDirectory(_isolatedConfigDirectory);
            PXToolBoxDev.Integrations.ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(_isolatedConfigDirectory);
            PXToolBoxDev.Integrations.PxLogger.SetLogDirectoryForTesting(Path.Combine(_isolatedConfigDirectory, "logs"));
        }

        public void Dispose()
        {
            PXToolBoxDev.Integrations.ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(null);
            PXToolBoxDev.Integrations.PxLogger.SetLogDirectoryForTesting(null);
            try { Directory.Delete(_isolatedConfigDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private static SizeConfig Config => SizeConfig.Default();

        private static ProcessingWorkflowService NewWorkflow() =>
            new ProcessingWorkflowService(new IntegratedLicenseService());

        /// <summary>
        /// Regressão do crash real relatado pelo usuário: o CorelDRAW inteiro fechava ao abrir o
        /// ListForge Integrado. Causa confirmada por revisão de código: `TxtSeparator.Text=","`
        /// no XAML é o único elemento desta janela com valor inicial E manipulador da mesma
        /// propriedade (`TextChanged="OnProcessingOptionChanged"`) — esse valor inicial dispara
        /// `TextChanged` DURANTE a própria construção da árvore de elementos do
        /// `InitializeComponent()`, antes de `OutputStaleWarning` (declarado no fim do XAML, no
        /// painel de Saída) ser conectado ao campo, lançando `NullReferenceException` não
        /// capturada por nada no caminho — e isso dentro do construtor invocado por reflexão pelo
        /// host "wpfhost" do Corel, na própria thread de UI nativa, explicando por que o
        /// resultado observado foi o próprio processo do CorelDRAW encerrando, não um diálogo de
        /// erro do .NET. Constrói a janela real (sem mostrar) numa thread STA dedicada — WPF
        /// exige STA para criar objetos de UI — para exercer o `InitializeComponent()` de
        /// verdade, não só lógica pura (esta classe não depende de documento aberto no
        /// CorelDRAW nem de nenhum objeto COM — ver cabeçalho de ClassicWorkspaceWindow.xaml.cs).
        /// </summary>
        [Fact]
        public void ClassicWorkspaceWindow_ConstructsWithoutThrowing()
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new ClassicWorkspaceWindow();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
        }

        [Fact]
        public void ValidInput_MatchesListForgeStandaloneFixture()
        {
            var workflow = NewWorkflow();

            var result = workflow.Execute(new ProcessingWorkflowRequest(
                "CARLA,12,G\nANA,7,M,NINA,O+",
                ",",
                Config,
                "original",
                ListSortMode.Original,
                JsonPieceMappingOptions.Disabled,
                ConsumeTrialCredit: false));

            Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
            Assert.Empty(result.ValidationIssues);
            // Mesmo texto de saída do teste equivalente no ListForge standalone.
            Assert.Equal("CARLA,12,G,,\nANA,7,M,NINA,O+", result.OutputText);
            Assert.Equal(new[] { "CARLA", "ANA" }, result.Orders.Select(o => o["Name"]).ToArray());
            Assert.Equal(new[] { "12", "7" }, result.Orders.Select(o => o["Number"]).ToArray());
            Assert.Equal("NINA", result.Orders[1]["Nickname"]);
            Assert.Equal("O+", result.Orders[1]["BloodType"]);
        }

        [Fact]
        public void InvalidInput_WithoutRecognizableSize_ReportsValidationIssue()
        {
            var workflow = NewWorkflow();

            var result = workflow.Execute(new ProcessingWorkflowRequest(
                "PEDRO,99",
                ",",
                Config,
                "original",
                ListSortMode.Original));

            Assert.Equal(ProcessingWorkflowStatus.ValidationFailed, result.Status);
            Assert.Single(result.ValidationIssues);
            Assert.Equal(1, result.ValidationIssues[0].LineNumber);
            Assert.Equal("sem tamanho", result.ValidationIssues[0].Message);
            Assert.Empty(result.OutputText);
        }

        /// <summary>
        /// Prova de ponta a ponta de que Tamanhos customizados (ver
        /// docs/listforge-classic-parity.md, grupo "Configurações aplicáveis ao integrado")
        /// realmente governa reconhecimento, validação e exportação — não só uma tela que salva
        /// valores sem conectá-los ao processamento. Mesmo SizeConfig que SizeConfigWindow
        /// produziria ao adicionar "GIGANTE" e remover "XG" do grupo masculino: "GIGANTE" passa a
        /// ser reconhecido (sem conversão silenciosa para outro tamanho) e "XG" — válido com a
        /// configuração oficial — passa a ser rejeitado como tamanho inválido, nunca
        /// silenciosamente aceito ou trocado por outro.
        /// </summary>
        [Fact]
        public void CustomSizeConfig_RecognizesAddedSize_AndRejectsRemovedSizeWithoutSilentConversion()
        {
            var workflow = NewWorkflow();
            var customSizes = SizeHelper.UpdateGroupConfig(
                SizeConfig.Default(),
                SizeHelper.GroupMale,
                baseSizes: new List<string> { "PP", "P", "M", "G", "GG", "GIGANTE" },
                prefixes: new List<string>(),
                suffixes: new List<string>());

            var withNewSize = workflow.Execute(new ProcessingWorkflowRequest(
                "JOAO,3,GIGANTE",
                ",",
                customSizes,
                "original",
                ListSortMode.Original,
                ConsumeTrialCredit: false));

            Assert.Equal(ProcessingWorkflowStatus.Success, withNewSize.Status);
            Assert.Equal("JOAO,3,GIGANTE", withNewSize.OutputText);

            var withRemovedSize = workflow.Execute(new ProcessingWorkflowRequest(
                "JOAO,3,XG",
                ",",
                customSizes,
                "original",
                ListSortMode.Original,
                ConsumeTrialCredit: false));

            Assert.Equal(ProcessingWorkflowStatus.ValidationFailed, withRemovedSize.Status);
            Assert.Single(withRemovedSize.ValidationIssues);
            Assert.Empty(withRemovedSize.OutputText);

            // Confirma que a configuração oficial (não alterada) continua reconhecendo "XG"
            // normalmente — a rejeição acima é efeito da configuração customizada, não de uma
            // regra nova inventada.
            var withOfficialDefaults = workflow.Execute(new ProcessingWorkflowRequest(
                "JOAO,3,XG",
                ",",
                SizeConfig.Default(),
                "original",
                ListSortMode.Original,
                ConsumeTrialCredit: false));
            Assert.Equal(ProcessingWorkflowStatus.Success, withOfficialDefaults.Status);
        }

        [Fact]
        public void MixedSizeGroups_ProducesSameRowCountAsStandaloneRules()
        {
            var workflow = NewWorkflow();

            // Mistura tamanhos adulto (M/G), infantil (8A/10A, com o sufixo "A" exigido pela
            // configuração padrão do grupo "child") e meião (ADULTO) na mesma lista — mesmas
            // regras de agrupamento de ListForge.Core.SizeHelper usadas pelo standalone.
            var result = workflow.Execute(new ProcessingWorkflowRequest(
                "JOAO,101,M\nBIA,202,8A,10A\nLUCAS,303,ADULTO",
                ",",
                Config,
                "original",
                ListSortMode.Original));

            Assert.True(result.Status == ProcessingWorkflowStatus.Success,
                string.Join(" | ", result.ValidationIssues.Select(i => $"L{i.LineNumber}:{i.Message}")));
            Assert.Equal(3, result.Rows.Count);
            Assert.Equal(3, result.Orders.Count);
        }

        [Fact]
        public void AdvancedJsonPieceMapping_MatchesListForgeStandaloneFixture()
        {
            // Mesma entrada/ordem personalizada do teste equivalente no standalone
            // (ListForge.Tests/JsonPieceMappingTests.cs:EnabledAdvancedMapping_AppliesSizesToConfiguredPieceOrder)
            // — aqui passando pelo ProcessingWorkflowService inteiro (o mesmo caminho que
            // OnProcess usa no host do PX ao montar JsonPieceMappingOptions a partir dos slots da
            // UI de "Lista avançada"), não só ListProcessor.BuildOrdersFromOrderlist isolado.
            var workflow = NewWorkflow();
            var options = new JsonPieceMappingOptions(true,
                new[] { PieceTypeMapper.Tanktop, PieceTypeMapper.ShortSleeve, PieceTypeMapper.Short });

            var result = workflow.Execute(new ProcessingWorkflowRequest(
                "JOAO,10,3-P,4-G,20-M",
                ",",
                Config,
                "original",
                ListSortMode.Original,
                options,
                ConsumeTrialCredit: false));

            Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
            var order = Assert.Single(result.Orders);
            Assert.Equal("3-P", order[PieceTypeMapper.Tanktop]);
            Assert.Equal("4-G", order[PieceTypeMapper.ShortSleeve]);
            Assert.Equal("20-M", order[PieceTypeMapper.Short]);
        }

        [Fact]
        public void ProcessingPreview_MatchesListForgeStandaloneFixture_ValidInput()
        {
            // Mesma entrada do teste equivalente no standalone
            // (ListForge.Tests/ProcessingPreviewServiceTests.cs:Analyze_ValidInputBuildsImpactWithoutConsumingCredit),
            // através de ProcessingPreviewService já movido para o núcleo compartilhado.
            var preview = NewPreviewService().Analyze(NewSnapshot("ANA,10,G\nBIA,11,M"));

            Assert.True(preview.CanProcess);
            Assert.Equal(2, preview.TotalRecords);
            Assert.Equal(2, preview.ValidRecords);
            Assert.Equal(0, preview.WarningRecords);
            Assert.Equal(0, preview.InvalidRecords);
            Assert.Equal(2, preview.Sizes.Sum(item => item.Count));
            Assert.Contains(preview.PieceTypes, item => item.PieceType == "Manga Curta" && item.Count == 2);
        }

        [Fact]
        public void ProcessingPreview_MatchesListForgeStandaloneFixture_InvalidInput()
        {
            // Mesma entrada de Analyze_InvalidInputReportsExpectedLine no standalone.
            var preview = NewPreviewService().Analyze(NewSnapshot("ANA,10,G\nBIA,11,XYZ\nCARLA,12,P"));

            Assert.False(preview.CanProcess);
            Assert.Equal(3, preview.TotalRecords);
            Assert.Equal(0, preview.ValidRecords);
            Assert.Equal(1, preview.InvalidRecords);
            var issue = Assert.Single(preview.Issues);
            Assert.Equal(2, issue.LineNumber);
            Assert.Equal(ProcessingIssueSeverity.Invalid, issue.Severity);
        }

        [Fact]
        public void ProcessingPreview_ExecuteConfirmed_ConsumesCreditOnlyAfterConfirmation()
        {
            // Mesmo cenário de ExecuteConfirmed_UsesPreviewSnapshotAndConsumesCreditAfterSuccess
            // no standalone: Analyze() não consome crédito; ExecuteConfirmed() consome.
            var license = new IntegratedLicenseService();
            var workflow = new ProcessingWorkflowService(license);
            var previewService = new ProcessingPreviewService(workflow);
            var preview = previewService.Analyze(new ProcessingPreviewSnapshot(
                new ProcessingWorkflowRequest("CARLA,12,G\nANA,7,M", ",", Config, "original", ListSortMode.Ascending, JsonPieceMappingOptions.Disabled, ConsumeTrialCredit: false),
                "Não aplicável no modo integrado",
                HasUnsavedWorkProfileChanges: false,
                AdvancedListEnabled: false,
                "escolhida ao salvar",
                "escolhido ao salvar"));

            var result = previewService.ExecuteConfirmed(preview);

            Assert.Equal(ProcessingWorkflowStatus.Success, result.Status);
            Assert.Equal("ANA,7,M\nCARLA,12,G", result.OutputText);
        }

        private static ProcessingPreviewService NewPreviewService() => new(NewWorkflow());

        private static ProcessingPreviewSnapshot NewSnapshot(string input) => new(
            new ProcessingWorkflowRequest(input, ",", Config, "original", ListSortMode.Original, JsonPieceMappingOptions.Disabled, ConsumeTrialCredit: false),
            "Não aplicável no modo integrado",
            HasUnsavedWorkProfileChanges: false,
            AdvancedListEnabled: false,
            "escolhida ao salvar",
            "escolhido ao salvar");

        // Comparar entrada/saída (ver docs/listforge-classic-parity.md, grupo "Revisão e
        // resumos") — ListComparisonService (+ modelos ComparisonSnapshot/ListComparisonResult/
        // etc.) foi movido para o núcleo compartilhado nesta rodada. Os testes abaixo reproduzem
        // literalmente entradas de ListForge.Tests/ListComparisonServiceTests.cs através do
        // mesmo caminho que ClassicWorkspaceWindow usa (CreateSnapshot → Compare), confirmando
        // que o host do PX chega ao mesmo resultado que o standalone para a mesma entrada —
        // além de casos explicitamente pedidos na tarefa (zeros à esquerda, nomes com acentos,
        // campos vazios, linhas repetidas, mudança de ordenação/capitalização).
        private static ListComparisonService NewComparisonService() => new();

        private static ComparisonSnapshot CreateProcessedSnapshot(
            string input,
            ListSortMode sortMode = ListSortMode.Original,
            string caseMode = "original")
        {
            var request = new ProcessingWorkflowRequest(input, ",", Config, caseMode, sortMode, JsonPieceMappingOptions.Disabled, ConsumeTrialCredit: false);
            var result = NewWorkflow().Execute(request);
            return NewComparisonService().CreateSnapshot(request, result, "Não aplicável no modo integrado", advancedListEnabled: false);
        }

        private static ListComparisonResult CompareProcessed(
            string input,
            ListSortMode sortMode = ListSortMode.Original,
            string caseMode = "original")
        {
            var service = NewComparisonService();
            return service.Compare(CreateProcessedSnapshot(input, sortMode, caseMode));
        }

        [Fact]
        public void Comparison_IdenticalInputAndOutput_MatchesListForgeStandaloneFixture()
        {
            // Mesma entrada de ListComparisonServiceTests.IdenticalInputAndOutput_AreMatchingWithoutCriticalDifferences.
            var comparison = CompareProcessed("ANA,10,G\nBIA,11,M");

            Assert.Equal(2, comparison.Summary.InputRecords);
            Assert.Equal(2, comparison.Summary.OutputRecords);
            Assert.Equal(2, comparison.Summary.Matching);
            Assert.Equal(0, comparison.Summary.Problems);
            Assert.False(comparison.Summary.HasCriticalDifferences);
        }

        [Fact]
        public void Comparison_SortedOutput_IsReorderedNotMissingOrAdded()
        {
            // Mesma entrada de ListComparisonServiceTests.SortedOutput_IsReorderedAndDoesNotCreateMissingOrAddedRecords —
            // cobre explicitamente "alterações de ordenação" (pedido da tarefa).
            var comparison = CompareProcessed("CARLA,12,G\nANA,7,M\nBRUNO,3,P", sortMode: ListSortMode.Ascending);

            Assert.Equal(3, comparison.Summary.Reordered);
            Assert.Equal(0, comparison.Summary.PossiblyMissing);
            Assert.Equal(0, comparison.Summary.Added);
            Assert.Equal(new int?[] { 1, 2, 3 }, comparison.Items.Select(item => item.InputLineNumber));
            Assert.Equal(new int?[] { 3, 1, 2 }, comparison.Items.Select(item => item.OutputLineNumber));
        }

        [Fact]
        public void Comparison_ConfiguredCapitalization_IsTransformedNotChanged()
        {
            // Mesma entrada de ListComparisonServiceTests.ConfiguredCapitalization_IsReportedAsExpectedTransformation —
            // cobre explicitamente "alterações de capitalização" (pedido da tarefa) e "nomes com
            // acentos" (João).
            var comparison = CompareProcessed("joão da silva,10,g", caseMode: "upper");

            var item = Assert.Single(comparison.Items);
            Assert.Equal(ComparisonCategory.Transformed, item.Category);
            var difference = Assert.Single(item.FieldDifferences);
            Assert.Equal("Nome", difference.FieldName);
            Assert.Equal("joão da silva", difference.InputValue);
            Assert.Equal("JOÃO DA SILVA", difference.OutputValue);
            Assert.Equal("capitalização configurada", difference.Reason);
            Assert.True(difference.IsExpected);
        }

        [Fact]
        public void Comparison_RepeatedLines_AreCountedAsDuplicatesNotAsMissingOrAdded()
        {
            // "Linhas repetidas" (pedido da tarefa) — mesma entrada de
            // ListComparisonServiceTests.LegitimateDuplicates_AreCountedAndMatchedByOccurrence.
            var comparison = CompareProcessed("JOÃO,10,M\nJOÃO,10,M");

            Assert.Equal(2, comparison.Summary.Matching);
            Assert.Equal(1, comparison.Summary.InputDuplicates);
            Assert.Equal(1, comparison.Summary.OutputDuplicates);
            Assert.Equal(0, comparison.Summary.Problems);
        }

        [Fact]
        public void Comparison_LeadingZerosInNumber_ArePreservedAndMatched()
        {
            // "Números com zeros à esquerda" (pedido explícito da tarefa) — Number é um campo de
            // texto (nunca interpretado como inteiro), então "007" precisa permanecer "007" do
            // início ao fim, sem diferença reportada.
            var comparison = CompareProcessed("ANA,007,G");

            var item = Assert.Single(comparison.Items);
            Assert.Equal(ComparisonCategory.Matching, item.Category);
            Assert.Empty(item.FieldDifferences);
            Assert.Contains("007", item.InputDisplay);
            Assert.Contains("007", item.OutputDisplay);
        }

        [Fact]
        public void Comparison_EmptyOptionalFields_DoNotProduceFalseDifferences()
        {
            // "Campos vazios" (pedido explícito da tarefa) — apelido/tipo sanguíneo ausentes nos
            // dois lados não devem gerar diferença nenhuma.
            var comparison = CompareProcessed("ANA,10,G,,");

            var item = Assert.Single(comparison.Items);
            Assert.Equal(ComparisonCategory.Matching, item.Category);
            Assert.Empty(item.FieldDifferences);
        }

        [Fact]
        public void Comparison_ManualRemovalAndAddition_AreReportedSeparately()
        {
            // "Ausência de resultado" / revisão de linhas removidas/adicionadas — mesma entrada de
            // ListComparisonServiceTests.ManualRemovalAndAddition_AreReportedWithoutReusingMatches.
            var snapshot = CreateProcessedSnapshot("ANA,10,G\nBIA,11,M");
            var editedRequest = new ProcessingWorkflowRequest("ANA,10,G\nCARLA,12,P", ",", Config, "original", ListSortMode.Original, JsonPieceMappingOptions.Disabled, ConsumeTrialCredit: false);
            var edited = NewWorkflow().Execute(editedRequest);
            var service = NewComparisonService();

            var comparison = service.Compare(service.UpdateOutput(snapshot, edited, outputWasManuallyEdited: true));

            Assert.Equal(1, comparison.Summary.Matching);
            Assert.Equal(1, comparison.Summary.PossiblyMissing);
            Assert.Equal(1, comparison.Summary.Added);
            Assert.Contains(comparison.Items, item => item.Category == ComparisonCategory.PossiblyMissing && item.InputDisplay.Contains("BIA"));
            Assert.Contains(comparison.Items, item => item.Category == ComparisonCategory.Added && item.OutputDisplay.Contains("CARLA"));
        }

        /// <summary>
        /// Regressão do mesmo padrão de crash corrigido em <c>ClassicWorkspaceWindow</c> (ver
        /// ARCHITECTURE.md, iteração 8): constrói a janela real de comparação, numa thread STA
        /// dedicada, com um resultado real do serviço — não só lógica pura.
        /// </summary>
        [Fact]
        public void ComparisonWindow_ConstructsWithoutThrowing()
        {
            var comparison = CompareProcessed("ANA,10,G\nBIA,11,M");

            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new ComparisonWindow(comparison);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
        }

        // Salvar avançado (ver docs/listforge-classic-parity.md, grupo "Geração, cópia e
        // exportação de resultados") — AdvancedSaveService foi movido para o núcleo
        // compartilhado nesta rodada. Os testes abaixo reproduzem o caminho que
        // ClassicWorkspaceWindow.OnAdvancedSave usa de fato (mesma classe), confirmando que o
        // host do PX chega aos mesmos arquivos/conteúdo que o standalone para a mesma entrada.
        [Fact]
        public void AdvancedSave_LooseFiles_MatchesListForgeStandaloneFixture()
        {
            // Mesmo cenário de ListForge.Tests/AdvancedSaveServiceTests.Save_LooseFilesWritesExpectedNamesAndContents,
            // através do ProcessingWorkflowService (como OnAdvancedSave realmente monta a
            // entrada/saída/JSON), não com texto fixo copiado à mão.
            var tempDir = Path.Combine(Path.GetTempPath(), "PXToolBox-AdvancedSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var workflow = NewWorkflow();
                var result = workflow.Execute(new ProcessingWorkflowRequest(
                    "ANA,10,P", ",", Config, "original", ListSortMode.Original, JsonPieceMappingOptions.Disabled, ConsumeTrialCredit: false));

                var service = new AdvancedSaveService();
                var saveResult = service.Save(new AdvancedSaveRequest(
                    tempDir, "pedido-teste", "ANA,10,P", result.OutputText, result.JsonPreview, AdvancedSaveMode.LooseFiles));

                Assert.True(saveResult.Success);
                Assert.NotNull(saveResult.Value);
                Assert.Equal(
                    new[] { "pedido-teste-entrada.txt", "pedido-teste-saida.txt", "pedido-teste.json" },
                    saveResult.Value!.FilePaths.Select(Path.GetFileName).ToArray());
                Assert.Equal("ANA,10,P", File.ReadAllText(Path.Combine(tempDir, "pedido-teste-saida.txt")));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void AdvancedSave_Zip_ContainsOnlyExpectedEntries()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PXToolBox-AdvancedSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var service = new AdvancedSaveService();
                var saveResult = service.Save(new AdvancedSaveRequest(
                    tempDir, "pedido", "ANA,10,P", "ANA,10,P", "{\"orders\":[]}", AdvancedSaveMode.Zip));

                Assert.True(saveResult.Success);
                using var archive = System.IO.Compression.ZipFile.OpenRead(saveResult.Value!.ZipPath!);
                Assert.Equal(3, archive.Entries.Count);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        /// <summary>
        /// Regressão do mesmo padrão de crash corrigido em <c>ClassicWorkspaceWindow</c> (ver
        /// ARCHITECTURE.md, iteração 8) — campo de nome editável, por isso constrói a janela real
        /// numa thread STA dedicada.
        /// </summary>
        [Fact]
        public void AdvancedSaveDialog_ConstructsWithoutThrowing()
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new AdvancedSaveDialog("lista", AdvancedSaveMode.LooseFiles);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
        }

        /// <summary>
        /// Regressão do mesmo padrão de crash corrigido em <c>ClassicWorkspaceWindow</c> (ver
        /// ARCHITECTURE.md, iteração 8) — substitui ListForge.UI.Views.InputDialog do
        /// standalone para "Novo..."/"Renomear..." de perfis de trabalho (ver
        /// docs/listforge-classic-parity.md, grupo "Perfis de trabalho"); campo de nome
        /// editável, por isso constrói a janela real numa thread STA dedicada.
        /// </summary>
        [Fact]
        public void WorkProfileNameDialog_ConstructsWithoutThrowing()
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new WorkProfileNameDialog("Nome do novo perfil:", "Padrão");
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
        }

        [Fact]
        public void IntegratedLicenseService_NeverBlocksProcessing_AndDocumentsPendingDecision()
        {
            var license = new IntegratedLicenseService();

            Assert.True(license.CanProcess);
            Assert.False(license.IsTrial);
            Assert.Contains("não decidido", license.Edition);
            Assert.Contains("pendente", license.ProcessingStatusSuffix);

            // Não deve lançar nem ter efeito colateral observável (sem estado Trial próprio).
            license.ConsumeSuccessfulProcessing();
            Assert.True(license.CanProcess);
        }
    }

    /// <summary>
    /// Regressão da regra de roteamento de teclado (ver docs/ARCHITECTURE.md, seção 10,
    /// iterações 3 e 4 — conflito de teclas com os atalhos do Corel). Cobre só a decisão pura
    /// (KeyboardRoutingPolicy): dado janela ativa + foco num campo editável, quais teclas devem
    /// ser roteadas para o editor em vez de deixadas para o Corel. O mecanismo real de entrega
    /// (hook WH_GETMESSAGE, foco nativo do Win32 — ThreadKeyboardRouter) depende de um host
    /// Win32/Corel real e permanece fora do alcance de teste automatizado; isto não substitui o
    /// smoke test real exigido antes de declarar o conflito de teclado resolvido.
    /// </summary>
    public class KeyboardRoutingPolicyTests
    {
        [Theory]
        [InlineData(Key.A)]
        [InlineData(Key.J)]
        [InlineData(Key.D0)]
        [InlineData(Key.Enter)]
        [InlineData(Key.Space)]
        [InlineData(Key.Back)]
        [InlineData(Key.Delete)]
        [InlineData(Key.Left)]
        [InlineData(Key.Right)]
        [InlineData(Key.Up)]
        [InlineData(Key.Down)]
        [InlineData(Key.Home)]
        [InlineData(Key.End)]
        [InlineData(Key.Escape)]
        [InlineData(Key.OemComma)]
        public void PlainEditingKeys_AreRoutedToEditor_WhenWindowActiveAndFocusInEditableControl(Key key)
        {
            Assert.True(KeyboardRoutingPolicy.ShouldForceRouteToEditor(
                key, ctrl: false, alt: false, windowActive: true, focusInEditableControl: true));
        }

        [Theory]
        [InlineData(Key.A)]
        [InlineData(Key.C)]
        [InlineData(Key.V)]
        [InlineData(Key.X)]
        [InlineData(Key.Z)]
        public void CtrlEditingCombos_AreRoutedToEditor(Key key)
        {
            Assert.True(KeyboardRoutingPolicy.ShouldForceRouteToEditor(
                key, ctrl: true, alt: false, windowActive: true, focusInEditableControl: true));
        }

        [Theory]
        [InlineData(Key.B)]
        [InlineData(Key.N)]
        [InlineData(Key.S)]
        [InlineData(Key.D1)]
        public void OtherCtrlCombos_StayWithCorel_EvenWithFocusInEditableControl(Key key)
        {
            Assert.False(KeyboardRoutingPolicy.ShouldForceRouteToEditor(
                key, ctrl: true, alt: false, windowActive: true, focusInEditableControl: true));
        }

        [Fact]
        public void AnyAltCombo_AlwaysStaysWithCorel()
        {
            Assert.False(KeyboardRoutingPolicy.ShouldForceRouteToEditor(
                Key.A, ctrl: false, alt: true, windowActive: true, focusInEditableControl: true));
        }

        [Theory]
        [InlineData(Key.F1)]
        [InlineData(Key.F12)]
        [InlineData(Key.F24)]
        public void FunctionKeys_StayWithCorel(Key key)
        {
            Assert.False(KeyboardRoutingPolicy.ShouldForceRouteToEditor(
                key, ctrl: false, alt: false, windowActive: true, focusInEditableControl: true));
        }

        [Theory]
        [InlineData(Key.Tab)]
        [InlineData(Key.LeftShift)]
        [InlineData(Key.LeftCtrl)]
        [InlineData(Key.LeftAlt)]
        [InlineData(Key.LWin)]
        [InlineData(Key.CapsLock)]
        [InlineData(Key.NumLock)]
        [InlineData(Key.Scroll)]
        [InlineData(Key.PrintScreen)]
        [InlineData(Key.Pause)]
        [InlineData(Key.System)]
        [InlineData(Key.Apps)]
        public void NeverInterceptKeys_StayWithCorel(Key key)
        {
            Assert.False(KeyboardRoutingPolicy.ShouldForceRouteToEditor(
                key, ctrl: false, alt: false, windowActive: true, focusInEditableControl: true));
        }

        [Fact]
        public void PlainLetterKey_NotRouted_WhenWindowNotActive()
        {
            Assert.False(KeyboardRoutingPolicy.ShouldForceRouteToEditor(
                Key.A, ctrl: false, alt: false, windowActive: false, focusInEditableControl: true));
        }

        [Fact]
        public void PlainLetterKey_NotRouted_WhenFocusNotInEditableControl()
        {
            Assert.False(KeyboardRoutingPolicy.ShouldForceRouteToEditor(
                Key.A, ctrl: false, alt: false, windowActive: true, focusInEditableControl: false));
        }
    }

    /// <summary>
    /// Regressão do bloqueio de resultado desatualizado (ver docs/listforge-classic-parity.md,
    /// grupo "Modos e opções de processamento", item citado explicitamente no pedido do usuário:
    /// "não pode exportar silenciosamente uma saída anterior como se correspondesse aos dados
    /// atuais").
    /// </summary>
    public class OutputFreshnessTrackerTests
    {
        [Fact]
        public void NeverProcessed_IsStale()
        {
            var tracker = new OutputFreshnessTracker();
            Assert.True(tracker.IsStale);
        }

        [Fact]
        public void ImmediatelyAfterProcessing_IsNotStale()
        {
            var tracker = new OutputFreshnessTracker();
            tracker.MarkProcessed();
            Assert.False(tracker.IsStale);
        }

        [Fact]
        public void InputChangedAfterProcessing_BecomesStale()
        {
            var tracker = new OutputFreshnessTracker();
            tracker.MarkProcessed();
            tracker.MarkInputChanged();
            Assert.True(tracker.IsStale);
        }

        [Fact]
        public void ProcessingAgainAfterChange_ClearsStale()
        {
            var tracker = new OutputFreshnessTracker();
            tracker.MarkProcessed();
            tracker.MarkInputChanged();
            tracker.MarkProcessed();
            Assert.False(tracker.IsStale);
        }

        [Fact]
        public void MultipleInputChangesWithoutReprocessing_StaysStale()
        {
            var tracker = new OutputFreshnessTracker();
            tracker.MarkProcessed();
            tracker.MarkInputChanged();
            tracker.MarkInputChanged();
            tracker.MarkInputChanged();
            Assert.True(tracker.IsStale);
        }
    }

    /// <summary>
    /// Regressão da navegação "clique no aviso/erro → vai até a linha" (ver
    /// docs/listforge-classic-parity.md, grupo "Validação e correção").
    /// </summary>
    public class TextLineLocatorTests
    {
        private const string ThreeLines = "JOAO,07,G\nMARIA,10,BLM\nPEDRO,99";

        [Fact]
        public void FirstLine_StartsAtZero()
        {
            Assert.Equal(0, TextLineLocator.GetLineStartIndex(ThreeLines, 1));
        }

        [Fact]
        public void SecondLine_StartsAfterFirstNewline()
        {
            Assert.Equal("JOAO,07,G\n".Length, TextLineLocator.GetLineStartIndex(ThreeLines, 2));
        }

        [Fact]
        public void ThirdLine_StartsAfterSecondNewline()
        {
            Assert.Equal("JOAO,07,G\nMARIA,10,BLM\n".Length, TextLineLocator.GetLineStartIndex(ThreeLines, 3));
        }

        [Fact]
        public void LineBeyondEndOfText_ReturnsTextLength()
        {
            Assert.Equal(ThreeLines.Length, TextLineLocator.GetLineStartIndex(ThreeLines, 99));
        }

        [Fact]
        public void EmptyText_ReturnsZero()
        {
            Assert.Equal(0, TextLineLocator.GetLineStartIndex("", 1));
        }

        [Fact]
        public void GetLineLength_MatchesLineContent()
        {
            Assert.Equal("JOAO,07,G".Length, TextLineLocator.GetLineLength(ThreeLines, 1));
            Assert.Equal("MARIA,10,BLM".Length, TextLineLocator.GetLineLength(ThreeLines, 2));
            Assert.Equal("PEDRO,99".Length, TextLineLocator.GetLineLength(ThreeLines, 3));
        }

        [Fact]
        public void GetLineLength_StripsTrailingCarriageReturn_ForCrlfInput()
        {
            var crlf = "JOAO,07,G\r\nMARIA,10,BLM";
            Assert.Equal("JOAO,07,G".Length, TextLineLocator.GetLineLength(crlf, 1));
        }
    }

    /// <summary>
    /// Regressão do resumo de tamanhos processados (ver docs/listforge-classic-parity.md, grupo
    /// "Revisão e resumos" — equivalente a RefreshSizeSummary() do standalone).
    /// </summary>
    public class SizeSummaryBuilderTests
    {
        private static SizeConfig Config => SizeConfig.Default();

        [Fact]
        public void NoRows_ReturnsEmptyString()
        {
            Assert.Equal("", SizeSummaryBuilder.Build(new List<ParsedRow>(), Config));
        }

        [Fact]
        public void SingleRow_SumsQuantityPerSize()
        {
            var rows = ListProcessor.ProcessText("JOAO,10,3-P,4-G", ",", Config);

            var summary = SizeSummaryBuilder.Build(rows, Config);

            Assert.Contains("G: 4", summary);
            Assert.Contains("P: 3", summary);
        }

        [Fact]
        public void MultipleRows_AccumulatesSameSizeAcrossRows()
        {
            var rows = ListProcessor.ProcessText("JOAO,10,G\nMARIA,11,2-G", ",", Config);

            var summary = SizeSummaryBuilder.Build(rows, Config);

            Assert.Contains("G: 3", summary);
        }
    }

    /// <summary>
    /// Regressão da UI de "Lista avançada" (ver docs/listforge-classic-parity.md, grupo "Modos e
    /// opções de processamento" — mapeamento de peça por slot JSON). Cobre só a estrutura de
    /// dados do slot (pura); a montagem de JsonPieceMappingOptions a partir dos slots é coberta
    /// ponta a ponta por AdvancedJsonPieceMapping_MatchesListForgeStandaloneFixture, acima.
    /// </summary>
    public class AdvancedPieceSlotTests
    {
        [Fact]
        public void PositionLabel_ReflectsPosition()
        {
            var slot = new AdvancedPieceSlot { Position = 3 };
            Assert.Equal("3º tamanho", slot.PositionLabel);
        }

        [Fact]
        public void Options_StartsWithBlankSelectionOption()
        {
            var slot = new AdvancedPieceSlot();
            Assert.Equal("", slot.Options[0].Key);
        }

        [Fact]
        public void Options_ContainsEveryKnownPieceType()
        {
            var slot = new AdvancedPieceSlot();
            Assert.Equal(PieceTypeMapper.AvailableOptions.Count + 1, slot.Options.Count);
            foreach (var known in PieceTypeMapper.AvailableOptions)
                Assert.Contains(slot.Options, option => option.Key == known.Key);
        }

        [Fact]
        public void SelectedKey_DefaultsToEmpty()
        {
            Assert.Equal("", new AdvancedPieceSlot().SelectedKey);
        }
    }
}
#endif
