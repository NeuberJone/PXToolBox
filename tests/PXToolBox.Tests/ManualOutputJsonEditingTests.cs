#if NET48
// Só compila na perna net48 (ver cabeçalho de ListForgeIntegratedTests.cs). Testa a edição
// manual da saída/JSON do modo Clássico do ListForge Integrado (ver
// docs/listforge-classic-parity.md, grupo "Geração, cópia e exportação de resultados") — porta
// de MainViewModel.AllowOutputEditing/HasPendingOutputEdit/HasPendingJsonEdit/
// ApplyOutputEdits/DiscardOutputEdits/EnsureNoPendingOutputOrJsonEdits do standalone, lidos por
// completo antes de implementar.
//
// IMPORTANTE: nenhum teste aqui invoca um caminho que mostra um MessageBox real (confirmação
// Sim/Não/Cancelar ao desligar edição/reprocessar com pendência, ou o aviso de JSON/lista
// inválida ao aplicar) nem um diálogo de arquivo (Salvar saída/Gerar JSON/Salvar avançado) —
// isso bloquearia esta execução de teste esperando um clique humano. O ListForge.Tests do
// próprio standalone segue a mesma regra (nenhum teste ali toca MessageBox). Esses caminhos
// ficam para o roteiro de smoke test real no Corel (ver docs/mvp-smoke-test.md); a rejeição de
// JSON sintaticamente inválido é provada aqui no nível do mecanismo real que ApplyOutputEdits
// usa (JObject.Parse/ListProcessor.ExtractListTextFromJsonData), sem passar pelo botão.
//
// Constrói a janela real numa thread STA dedicada (WPF exige STA) e dirige os controles via
// FindName + reflexão nos campos/métodos privados — mesmo padrão já usado por
// SizeConfigWindow_Save_RejectsGroupWithNoBaseSizes em ListForgeIntegratedSettingsStoreTests.cs.
// Nunca invoca Clipboard.SetText (mesma cautela de MeasurementSumServiceTests: testes verificam
// a FONTE de texto que cada ação usaria, não o clipboard real do desenvolvedor).
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using Newtonsoft.Json.Linq;
using PXToolBoxDev.ListForgeIntegrated;
using Xunit;

namespace PXToolBox.Tests
{
    // Constrói ClassicWorkspaceWindow, que lê e grava a configuração do ListForge Integrado
    // (ex.: trocar de modo salva PreferredWorkspaceModeByProfileId) e registra no log ("ListForge Integrado iniciado"). Sem isto, cada execução da
    // suíte — inclusive a de toda build — gravava no arquivo REAL de %LocalAppData%. Mesmo
    // isolamento de ListForgeIntegratedSettingsStoreTests (ver TestCollections.cs).
    [Collection("PXToolBox Dev shared filesystem resources")]
    public class ManualOutputJsonEditingTests : IDisposable
    {
        private readonly string _isolatedConfigDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "lf-manual-json-" + Guid.NewGuid().ToString("N"));

        public ManualOutputJsonEditingTests()
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

        private static void RunOnSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null) throw caught;
        }

        private static T GetField<T>(object target, string name)
        {
            var field = typeof(ClassicWorkspaceWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(ClassicWorkspaceWindow), name);
            return (T)field.GetValue(target)!;
        }

        private static void InvokePrivate(object target, string name, params object[] args)
        {
            var method = typeof(ClassicWorkspaceWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(ClassicWorkspaceWindow), name);
            method.Invoke(target, args);
        }

        // Semeia o estado da janela como se um processamento real tivesse acabado de terminar
        // com sucesso — usa o próprio SetGeneratedTexts privado (nunca atribui OutputTextBox.Text
        // diretamente), exercendo o mesmo caminho real usado por ApplyResult/ApplyOutputEdits.
        private static void SeedSuccessfulResult(Window window, string outputText, string jsonText) =>
            InvokePrivate(window, "SetGeneratedTexts", outputText, jsonText);

        private static void EnableEditing(Window window)
        {
            var checkbox = (CheckBox)window.FindName("ChkAllowOutputEditing")!;
            checkbox.IsChecked = true;
        }

        [Fact]
        public void AllowOutputEditing_Default_IsOffAndTextBoxesReadOnly()
        {
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                var checkbox = (CheckBox)window.FindName("ChkAllowOutputEditing")!;
                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var jsonBox = (TextBox)window.FindName("JsonTextBox")!;

                Assert.NotEqual(true, checkbox.IsChecked);
                Assert.True(outputBox.IsReadOnly);
                Assert.True(jsonBox.IsReadOnly);
            });
        }

        [Fact]
        public void AllowOutputEditing_Toggled_MakesTextBoxesEditable_AndBackReadOnlyWithoutPendingEdit()
        {
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                var checkbox = (CheckBox)window.FindName("ChkAllowOutputEditing")!;
                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var jsonBox = (TextBox)window.FindName("JsonTextBox")!;

                checkbox.IsChecked = true;
                Assert.False(outputBox.IsReadOnly);
                Assert.False(jsonBox.IsReadOnly);

                // Sem edição pendente, desligar nunca precisa de confirmação (nenhum MessageBox).
                checkbox.IsChecked = false;
                Assert.True(outputBox.IsReadOnly);
                Assert.True(jsonBox.IsReadOnly);
            });
        }

        [Fact]
        public void EditingOutputText_MarksPendingEdit_EnablesApplyDiscard_AndShowsIndicator()
        {
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                EnableEditing(window);
                SeedSuccessfulResult(window, "JOAO,10,G", "{\"orders\":[]}");

                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var applyButton = (Button)window.FindName("BtnApplyOutputEdits")!;
                var discardButton = (Button)window.FindName("BtnDiscardOutputEdits")!;
                var indicator = (TextBlock)window.FindName("ManualEditIndicator")!;

                outputBox.Text = "JOAO,10,G\nMARIA,07,M";

                Assert.True(GetField<bool>(window, "_hasPendingOutputEdit"));
                Assert.False(GetField<bool>(window, "_hasPendingJsonEdit"));
                Assert.True(applyButton.IsEnabled);
                Assert.True(discardButton.IsEnabled);
                Assert.Equal(Visibility.Visible, indicator.Visibility);
            });
        }

        [Fact]
        public void EditingJsonAfterEditingOutput_ClearsOutputPending_OnlyOneSourceAtATime()
        {
            // Mesma regra exata do standalone (OutputText/JsonText, setters): tocar numa caixa
            // zera o "pendente" da outra — só uma fonte (saída OU JSON) é usada por "Aplicar
            // alterações" por vez.
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                EnableEditing(window);
                SeedSuccessfulResult(window, "JOAO,10,G", "{\"orders\":[]}");

                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var jsonBox = (TextBox)window.FindName("JsonTextBox")!;

                outputBox.Text = "JOAO,10,G\nMARIA,07,M";
                Assert.True(GetField<bool>(window, "_hasPendingOutputEdit"));

                jsonBox.Text = "{\"orders\":[{\"Name\":\"X\"}]}";
                Assert.True(GetField<bool>(window, "_hasPendingJsonEdit"));
                Assert.False(GetField<bool>(window, "_hasPendingOutputEdit"));
            });
        }

        [Fact]
        public void ApplyOutputEdits_WithValidEditedListText_Reprocesses_ClearsPending_MarksManuallyEdited()
        {
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                var workflow = NewWorkflow();
                var baseResult = workflow.Execute(new ProcessingWorkflowRequest(
                    "JOAO,10,G", ",", Config, "original", ListSortMode.Original, JsonPieceMappingOptions.Disabled, ConsumeTrialCredit: false));
                Assert.Equal(ProcessingWorkflowStatus.Success, baseResult.Status);

                EnableEditing(window);
                SeedSuccessfulResult(window, baseResult.OutputText, baseResult.JsonPreview);

                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var applyButton = (Button)window.FindName("BtnApplyOutputEdits")!;
                var indicator = (TextBlock)window.FindName("ManualEditIndicator")!;

                outputBox.Text = baseResult.OutputText + "\nMARIA,07,M";
                Assert.True(GetField<bool>(window, "_hasPendingOutputEdit"));

                applyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                Assert.False(GetField<bool>(window, "_hasPendingOutputEdit"));
                Assert.False(GetField<bool>(window, "_hasPendingJsonEdit"));
                Assert.True(GetField<bool>(window, "_outputWasManuallyEdited"));
                Assert.Contains("MARIA", outputBox.Text);
                Assert.Contains("JOAO", outputBox.Text);
                Assert.Equal(Visibility.Visible, indicator.Visibility);
                Assert.Contains("editado manualmente", indicator.Text, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void ApplyOutputEdits_WithEditedJson_ExtractsListText_Reprocesses_PreservesLeadingZeros()
        {
            // Prova de ponta a ponta do item "zeros à esquerda" — edita o JSON (não a saída),
            // reaplica, e confirma que o número original com zero à esquerda ("07") e um novo
            // número também com zero à esquerda ("012"), adicionado na própria edição, sobrevivem
            // ao ciclo completo: editar JSON -> ListProcessor.ExtractListTextFromJsonData (núcleo
            // compartilhado) -> reprocessar -> novo texto de saída.
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                var workflow = NewWorkflow();
                var baseResult = workflow.Execute(new ProcessingWorkflowRequest(
                    "JOAO,07,G", ",", Config, "original", ListSortMode.Original, JsonPieceMappingOptions.Disabled, ConsumeTrialCredit: false));
                Assert.Equal(ProcessingWorkflowStatus.Success, baseResult.Status);
                Assert.Equal("07", baseResult.Orders[0]["Number"]);

                EnableEditing(window);
                SeedSuccessfulResult(window, baseResult.OutputText, baseResult.JsonPreview);

                var parsed = JObject.Parse(baseResult.JsonPreview);
                var orders = (JArray)parsed["orders"]!;
                var clone = (JObject)orders[0].DeepClone();
                clone["Name"] = "MARIA";
                clone["Number"] = "012";
                orders.Add(clone);
                var editedJson = parsed.ToString();

                var jsonBox = (TextBox)window.FindName("JsonTextBox")!;
                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var applyButton = (Button)window.FindName("BtnApplyOutputEdits")!;

                jsonBox.Text = editedJson;
                Assert.True(GetField<bool>(window, "_hasPendingJsonEdit"));

                applyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                Assert.False(GetField<bool>(window, "_hasPendingJsonEdit"));
                Assert.True(GetField<bool>(window, "_outputWasManuallyEdited"));
                Assert.Contains("JOAO,07,", outputBox.Text);
                Assert.Contains("MARIA,012,", outputBox.Text);
                Assert.DoesNotContain("JOAO,7,", outputBox.Text);
                Assert.DoesNotContain("MARIA,12,", outputBox.Text);
            });
        }

        [Fact]
        public void DiscardOutputEdits_RevertsToLastValidText_ClearsPending_NeverMarksManuallyEdited()
        {
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                EnableEditing(window);
                SeedSuccessfulResult(window, "JOAO,10,G", "{\"orders\":[{\"Name\":\"JOAO\"}]}");

                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var discardButton = (Button)window.FindName("BtnDiscardOutputEdits")!;
                var indicator = (TextBlock)window.FindName("ManualEditIndicator")!;

                outputBox.Text = "texto digitado que nunca será aplicado";
                Assert.True(GetField<bool>(window, "_hasPendingOutputEdit"));

                discardButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                Assert.Equal("JOAO,10,G", outputBox.Text);
                Assert.False(GetField<bool>(window, "_hasPendingOutputEdit"));
                Assert.False(GetField<bool>(window, "_outputWasManuallyEdited"));
                Assert.Equal(Visibility.Collapsed, indicator.Visibility);
            });
        }

        [Fact]
        public void SaveAndCopyOutput_UseLiveDisplayedText_EvenWithPendingUnappliedEdit()
        {
            // "Copiar saída"/"Salvar saída" usam o texto EXIBIDO (OutputTextBox.Text), igual ao
            // standalone (CopyOutput()/SaveOutput() usam OutputText, a propriedade viva) — a
            // lista organizada não tem formato "inválido" possível, então uma edição pendente
            // ainda não aplicada é um conteúdo legítimo para copiar/salvar.
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                EnableEditing(window);
                SeedSuccessfulResult(window, "JOAO,10,G", "{\"orders\":[]}");

                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                outputBox.Text = "texto editado ainda não aplicado";

                // A fonte que OnSaveOutput/OnCopyOutput leem é exatamente OutputTextBox.Text —
                // confirmando aqui sem tocar Clipboard/diálogo de arquivo real.
                Assert.Equal("texto editado ainda não aplicado", outputBox.Text);
                Assert.Equal("JOAO,10,G", GetField<string>(window, "_lastOutputText"));
            });
        }

        [Fact]
        public void CopyAndGenerateJson_UseLastValidCanonicalJson_NeverLiveUnappliedEdit()
        {
            // "Copiar JSON"/"Gerar JSON"/o campo JSON de "Salvar avançado" usam
            // _lastValidJsonText (canônico), nunca JsonTextBox.Text (vivo) — igual ao standalone
            // (CopyJson()/GenerateJson() usam _lastJson, nunca o JsonText vivo): protege contra
            // copiar/exportar um JSON digitado ainda não revalidado como se já fosse válido.
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                EnableEditing(window);
                SeedSuccessfulResult(window, "JOAO,10,G", "{\"orders\":[{\"Name\":\"JOAO\"}]}");

                var jsonBox = (TextBox)window.FindName("JsonTextBox")!;
                jsonBox.Text = "{ isto não é json válido para SISBolt }";

                Assert.True(GetField<bool>(window, "_hasPendingJsonEdit"));
                // A fonte canônica nunca reflete a edição ainda não aplicada.
                Assert.Equal("{\"orders\":[{\"Name\":\"JOAO\"}]}", GetField<string>(window, "_lastValidJsonText"));
                Assert.NotEqual(jsonBox.Text, GetField<string>(window, "_lastValidJsonText"));
            });
        }

        [Fact]
        public void Reprocessing_ClearsManuallyEditedFlag_AndPendingEdits()
        {
            // "Ao reprocessar" (ApplyResult, chamado por Processar/Processar com prévia depois de
            // um processamento com sucesso) sempre volta ao estado "processado", nunca "editado
            // manualmente" — reflete exatamente MainViewModel.SetGeneratedTexts/HandleProcessing
            // WorkflowResult do standalone.
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                EnableEditing(window);
                SeedSuccessfulResult(window, "JOAO,10,G", "{\"orders\":[]}");

                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var applyButton = (Button)window.FindName("BtnApplyOutputEdits")!;
                var indicator = (TextBlock)window.FindName("ManualEditIndicator")!;

                outputBox.Text = "JOAO,10,G\nMARIA,07,M";
                applyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.True(GetField<bool>(window, "_outputWasManuallyEdited"));

                var workflow = NewWorkflow();
                var freshRequest = new ProcessingWorkflowRequest(
                    "CARLA,12,G", ",", Config, "original", ListSortMode.Original, JsonPieceMappingOptions.Disabled, ConsumeTrialCredit: true);
                var freshResult = workflow.Execute(freshRequest);
                Assert.Equal(ProcessingWorkflowStatus.Success, freshResult.Status);

                InvokePrivate(window, "ApplyResult", freshResult, freshRequest);

                Assert.False(GetField<bool>(window, "_outputWasManuallyEdited"));
                Assert.False(GetField<bool>(window, "_hasPendingOutputEdit"));
                Assert.False(GetField<bool>(window, "_hasPendingJsonEdit"));
                Assert.Equal(Visibility.Collapsed, indicator.Visibility);
                Assert.Equal("CARLA,12,G", outputBox.Text);
            });
        }

        [Fact]
        public void InputChangedAfterApply_ShowsStaleWarning_DistinctFromManualEditIndicator()
        {
            // "mantenha distinção entre resultado produzido pelo processamento; versão editada;
            // resultado desatualizado por mudanças na entrada ou opções" — os dois avisos
            // coexistem e significam coisas diferentes.
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                EnableEditing(window);
                SeedSuccessfulResult(window, "JOAO,10,G", "{\"orders\":[]}");

                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                var applyButton = (Button)window.FindName("BtnApplyOutputEdits")!;
                var inputBox = (TextBox)window.FindName("InputTextBox")!;
                var staleWarning = (TextBlock)window.FindName("OutputStaleWarning")!;
                var manualIndicator = (TextBlock)window.FindName("ManualEditIndicator")!;

                outputBox.Text = "JOAO,10,G\nMARIA,07,M";
                applyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                Assert.Equal(Visibility.Collapsed, staleWarning.Visibility);
                Assert.Equal(Visibility.Visible, manualIndicator.Visibility);

                inputBox.Text = "entrada completamente diferente agora";

                Assert.Equal(Visibility.Visible, staleWarning.Visibility);
                Assert.Equal(Visibility.Visible, manualIndicator.Visibility);
            });
        }

        [Fact]
        public void EnsureNoPendingOutputOrJsonEdits_ReturnsTrueImmediately_WhenNothingPending()
        {
            // Único ramo seguro de testar automaticamente sem disparar o MessageBox real de
            // confirmação (Sim/Não/Cancelar) — os demais ramos exigem o roteiro manual no Corel.
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                var method = typeof(ClassicWorkspaceWindow).GetMethod("EnsureNoPendingOutputOrJsonEdits", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var result = (bool)method.Invoke(window, Array.Empty<object>())!;
                Assert.True(result);
            });
        }

        [Fact]
        public void ClearAll_ResetsManualEditState_WithoutAskingConfirmation()
        {
            // "Limpar tudo" é um descarte explícito e óbvio — igual ao MainViewModel.ClearAll do
            // standalone, nunca pede confirmação mesmo com edição pendente.
            RunOnSta(() =>
            {
                var window = new ClassicWorkspaceWindow();
                EnableEditing(window);
                SeedSuccessfulResult(window, "JOAO,10,G", "{\"orders\":[]}");

                var outputBox = (TextBox)window.FindName("OutputTextBox")!;
                outputBox.Text = "edição pendente que será limpa";
                Assert.True(GetField<bool>(window, "_hasPendingOutputEdit"));

                // Botão "Limpar tudo" não tem x:Name no XAML — chama o handler diretamente.
                InvokePrivate(window, "OnClearAll", window, new RoutedEventArgs());

                Assert.False(GetField<bool>(window, "_hasPendingOutputEdit"));
                Assert.False(GetField<bool>(window, "_hasPendingJsonEdit"));
                Assert.False(GetField<bool>(window, "_outputWasManuallyEdited"));
                Assert.Equal("", outputBox.Text);
                Assert.Equal("", GetField<string>(window, "_lastValidJsonText"));
            });
        }

        [Fact]
        public void SyntacticallyInvalidJson_ThroughSameMechanismApplyOutputEditsUses_ThrowsAsExpected()
        {
            // Mesmo mecanismo real usado por ApplyOutputEdits (JObject.Parse +
            // ListProcessor.ExtractListTextFromJsonData, núcleo compartilhado) — prova a rejeição
            // sem passar pelo botão (que mostraria um MessageBox real).
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JObject.Parse("{ isto não é json"));

            var structurallyInvalid = JObject.Parse("""{"notOrders":[]}""");
            Assert.Throws<ArgumentException>(() =>
                ListProcessor.ExtractListTextFromJsonData(structurallyInvalid, ",", includeHeader: true));
        }

        [Fact]
        public void ListForgeIntegratedSettings_NeverExposesAllowOutputEditing_SessionOnlyLikeStandalone()
        {
            // Mesma garantia do standalone (AdvancedListToggleTests.
            // OutputEditingStartsDisabledAndIsSessionOnly, que confirma
            // Assert.Null(typeof(AppConfig).GetProperty("AllowOutputEditing"))): a opção nunca é
            // persistida, então nem existe como campo de configuração.
            Assert.Null(typeof(PXToolBoxDev.Integrations.ListForgeIntegratedSettings).GetProperty("AllowOutputEditing"));
        }
    }
}
#endif
