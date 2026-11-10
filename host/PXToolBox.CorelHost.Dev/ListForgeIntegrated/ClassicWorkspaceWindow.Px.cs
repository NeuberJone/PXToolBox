using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Modo PX (protótipo aprovado em docs/design/listforge-px-workflow/) — Lista → Revisão →
    /// Resultado. Nome nesta etapa: "PX — em desenvolvimento" (não é o modo Clássico "em
    /// desenvolvimento" citado no título da janela — são dois rótulos independentes).
    ///
    /// <para><b>Estado único, duas apresentações</b> (ver design.md, seção 3.2): esta classe
    /// NUNCA duplica entrada, opções, perfil, resultado ou edições pendentes — ela MOVE os
    /// mesmos controles nomeados do Clássico (<see cref="InputTextBox"/>,
    /// <see cref="OutputTextBox"/>, <see cref="JsonTextBox"/>, <see cref="IssuesList"/>,
    /// <see cref="TxtSeparator"/>, <see cref="CmbCaseMode"/>, <see cref="CmbSortMode"/>,
    /// <see cref="CmbWorkProfile"/> etc.) entre o layout Clássico ("ClassicRoot") e o layout PX
    /// ("PxRoot") ao trocar de modo — nunca cria uma segunda instância deles. Todas as regras de
    /// processamento/validação/exportação continuam vindo exclusivamente dos mesmos serviços
    /// compartilhados já usados pelo Clássico (<see cref="_workflow"/>, <see cref="_previewService"/>,
    /// <see cref="ApplyResult"/>, <see cref="BuildProcessingRequest(bool)"/> etc.) — nenhuma
    /// regra reimplementada aqui.</para>
    ///
    /// <para><b>Preferência de modo</b>: persistida por perfil em
    /// <see cref="Integrations.ListForgeIntegratedSettings.PreferredWorkspaceModeByProfileId"/>
    /// (campo exclusivo do PX DEV, nunca em <c>WorkProfile</c>/<c>WorkProfileSettings</c>,
    /// compartilhados com o ListForge Standalone — ver design.md, seção 3.3).</para>
    /// </summary>
    public partial class ClassicWorkspaceWindow
    {
        private const string PxModeClassic = Integrations.ListForgeIntegratedSettings.WorkspaceModeClassic;
        private const string PxModePx = Integrations.ListForgeIntegratedSettings.WorkspaceModePx;

        private string _pxCurrentMode = PxModeClassic;
        private readonly Dictionary<FrameworkElement, (Panel Parent, int Index)> _pxOriginalParents = new();
        private ProcessingPreview? _pxCurrentPreview;
        private bool _pxIsProcessing;

        private sealed class PxRevisaoRowView
        {
            public int LineNumber { get; set; }
            public string Name { get; set; } = "";
            public string Number { get; set; } = "";
            public string Sizes { get; set; } = "";
        }

        // =============================================================================
        // Troca de modo
        // =============================================================================

        /// <summary>Chamado uma vez, no fim do construtor: aplica a preferência de modo salva
        /// para o perfil ativo (ver <see cref="Integrations.ListForgeIntegratedSettings.GetPreferredWorkspaceMode"/>
        /// — nunca lança, resolve para Clássico quando não há preferência salva ainda).</summary>
        private void PxApplyInitialMode()
        {
            PxSetAccentStyle(BtnModeClassic, true);
            PxSetAccentStyle(BtnModePx, false);

            var preferred = _settings.GetPreferredWorkspaceMode(_settings.ActiveWorkProfileId);
            if (preferred == PxModePx)
                PxSetMode(PxModePx);
        }

        private void OnSwitchToClassicMode(object sender, RoutedEventArgs e) => PxSetMode(PxModeClassic);

        private void OnSwitchToPxMode(object sender, RoutedEventArgs e) => PxSetMode(PxModePx);

        /// <summary>
        /// Troca de modo nunca reprocessa, salva ou descarta nada por conta própria — só move os
        /// mesmos controles para o layout do modo escolhido (ver comentário da classe). Uma
        /// edição manual pendente, um resultado desatualizado ou uma entrada com alterações não
        /// salvas continuam exatamente como estavam, só mudando de apresentação visual.
        /// </summary>
        private void PxSetMode(string mode)
        {
            if (mode == _pxCurrentMode) return;

            if (mode == PxModePx)
            {
                PxActivate();
                ClassicRoot.Visibility = Visibility.Collapsed;
                PxRoot.Visibility = Visibility.Visible;
            }
            else
            {
                PxDeactivate();
                PxRoot.Visibility = Visibility.Collapsed;
                ClassicRoot.Visibility = Visibility.Visible;
            }

            _pxCurrentMode = mode;
            PxSetAccentStyle(BtnModeClassic, mode == PxModeClassic);
            PxSetAccentStyle(BtnModePx, mode == PxModePx);

            PxSaveModePreference(mode);
        }

        /// <summary>Nunca lança: uma falha ao salvar a preferência não pode impedir a troca de
        /// modo que o usuário já pediu — só fica sem persistir para a próxima abertura.</summary>
        private void PxSaveModePreference(string mode)
        {
            try
            {
                var activeId = _settings.ActiveWorkProfileId;
                if (string.IsNullOrEmpty(activeId)) return;
                _settings.SetPreferredWorkspaceMode(activeId, mode);
                SaveSettingsOrShowError();
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("PxSetMode", "Falha ao salvar preferência de modo.", ex);
            }
        }

        /// <summary>Move os controles de sessão do Clássico para dentro dos slots do PxRoot —
        /// ver docs/design/listforge-px-workflow/design.md, seção 2.2, para o mapa completo de
        /// qual controle vai para qual etapa.</summary>
        private void PxActivate()
        {
            PxMoveToSlot(TxtSeparator, PxSlotSeparator);
            PxMoveToSlot(CmbCaseMode, PxSlotCaseMode);
            PxMoveToSlot(CmbSortMode, PxSlotSortMode);
            PxMoveToSlot(FindReplaceExpander, PxSlotFindReplace);
            PxMoveToSlot(AdvancedListExpander, PxSlotAdvanced);
            PxMoveToSlot(InputPanel, PxSlotInput);
            PxMoveToSlot(IssuesList, PxSlotIssues);
            PxMoveToSlot(OutputPanel, PxSlotOutput);
            PxMoveToSlot(ManualEditPanel, PxSlotManualEdit);
            PxMoveToSlot(WorkProfilePanel, PxHeaderSlot);
            PxMoveToSlot(AccessStatusText, PxHeaderSlot);
            PxMoveToSlot(StatusBorder, PxHeaderSlot);
            PxMoveToSlot(DiagnosticsExpander, PxHeaderSlot);

            GoToPxStep("lista");
            UpdatePxResultadoState();
        }

        /// <summary>Restaura cada controle exatamente na posição original do Clássico — nunca
        /// uma ordem diferente da que ele já tinha antes de entrar no modo PX.</summary>
        private void PxDeactivate()
        {
            PxRestoreToOriginalParent(TxtSeparator);
            PxRestoreToOriginalParent(CmbCaseMode);
            PxRestoreToOriginalParent(CmbSortMode);
            PxRestoreToOriginalParent(FindReplaceExpander);
            PxRestoreToOriginalParent(AdvancedListExpander);
            PxRestoreToOriginalParent(InputPanel);
            PxRestoreToOriginalParent(IssuesList);
            PxRestoreToOriginalParent(OutputPanel);
            PxRestoreToOriginalParent(ManualEditPanel);
            PxRestoreToOriginalParent(WorkProfilePanel);
            PxRestoreToOriginalParent(AccessStatusText);
            PxRestoreToOriginalParent(StatusBorder);
            PxRestoreToOriginalParent(DiagnosticsExpander);
        }

        private void PxMoveToSlot(FrameworkElement element, ContentControl slot)
        {
            PxDetachFromCurrentParent(element);
            slot.Content = element;
        }

        private void PxMoveToSlot(FrameworkElement element, Panel slot)
        {
            PxDetachFromCurrentParent(element);
            slot.Children.Add(element);
        }

        private void PxDetachFromCurrentParent(FrameworkElement element)
        {
            if (!_pxOriginalParents.ContainsKey(element) && element.Parent is Panel originalPanel)
                _pxOriginalParents[element] = (originalPanel, originalPanel.Children.IndexOf(element));

            if (element.Parent is Panel currentPanel)
                currentPanel.Children.Remove(element);
            else if (element.Parent is ContentControl currentSlot && ReferenceEquals(currentSlot.Content, element))
                currentSlot.Content = null;
        }

        private void PxRestoreToOriginalParent(FrameworkElement element)
        {
            if (element.Parent is ContentControl cc && ReferenceEquals(cc.Content, element))
                cc.Content = null;
            else if (element.Parent is Panel currentPanel)
                currentPanel.Children.Remove(element);

            if (!_pxOriginalParents.TryGetValue(element, out var info))
                return;

            var index = Math.Min(info.Index, info.Parent.Children.Count);
            if (index < 0) index = info.Parent.Children.Count;
            info.Parent.Children.Insert(index, element);
        }

        // =============================================================================
        // Navegação entre etapas — sempre direta, nunca uma sequência obrigatória; mudar de
        // etapa nunca limpa nem altera entrada/opções/resultado.
        // =============================================================================

        private void OnPxGoToLista(object sender, RoutedEventArgs e) => GoToPxStep("lista");

        private async void OnPxGoToRevisao(object sender, RoutedEventArgs e)
        {
            GoToPxStep("revisao");
            await RefreshPxRevisaoAsync();
        }

        private async void OnPxGoToRevisaoFromTab(object sender, RoutedEventArgs e)
        {
            GoToPxStep("revisao");
            await RefreshPxRevisaoAsync();
        }

        private void OnPxGoToResultado(object sender, RoutedEventArgs e)
        {
            GoToPxStep("resultado");
            UpdatePxResultadoState();
        }

        private void GoToPxStep(string step)
        {
            PxStepLista.Visibility = step == "lista" ? Visibility.Visible : Visibility.Collapsed;
            PxStepRevisao.Visibility = step == "revisao" ? Visibility.Visible : Visibility.Collapsed;
            PxStepResultado.Visibility = step == "resultado" ? Visibility.Visible : Visibility.Collapsed;

            PxSetAccentStyle(PxTabLista, step == "lista");
            PxSetAccentStyle(PxTabRevisao, step == "revisao");
            PxSetAccentStyle(PxTabResultado, step == "resultado");
        }

        /// <summary>Alterna entre o estilo de destaque e o estilo implícito padrão do botão.
        /// <c>ClearValue</c>, nunca <c>Style = null</c>: atribuir <c>null</c> diretamente
        /// desativaria a busca de estilo implícito do WPF, deixando o botão sem nenhum estilo
        /// (visual padrão do Windows) em vez de voltar ao estilo padrão deste tema.</summary>
        private void PxSetAccentStyle(Button button, bool accent)
        {
            if (accent)
                button.Style = (Style)FindResource("Px.PrimaryButton");
            else
                button.ClearValue(FrameworkElement.StyleProperty);
        }

        // =============================================================================
        // Revisão — mesma prévia real do núcleo usada pelo Clássico (OnProcessWithPreview),
        // aqui como tela persistente em vez de diálogo modal. Nenhuma contagem/dado inventado:
        // só o que ProcessingPreviewService.Analyze devolve.
        // =============================================================================

        private async Task RefreshPxRevisaoAsync()
        {
            var snapshot = BuildPxPreviewSnapshot();

            try
            {
                _pxCurrentPreview = await Task.Run(() => _previewService.Analyze(snapshot));
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("PxRevisao", "Falha inesperada ao analisar a entrada.", ex);
                SetStatus($"Falha inesperada ao analisar a entrada: {ex.Message}");
                return;
            }

            var preview = _pxCurrentPreview;

            PxRevisaoCounts.Children.Clear();
            PxRevisaoCounts.Children.Add(PxBuildCountTextBlock($"{preview.TotalRecords} registro(s)"));
            PxRevisaoCounts.Children.Add(PxBuildCountTextBlock($"{preview.ValidRecords} válido(s)"));
            PxRevisaoCounts.Children.Add(PxBuildCountTextBlock($"{preview.WarningRecords} aviso(s)"));
            PxRevisaoCounts.Children.Add(PxBuildCountTextBlock($"{preview.InvalidRecords} inválido(s)"));

            var rows = preview.AnalysisResult.Rows
                .Select(row => new PxRevisaoRowView
                {
                    LineNumber = row.SourceLineNumber,
                    Name = row.Name,
                    Number = row.Number,
                    Sizes = string.Join("/", row.Tams ?? Array.Empty<string>()),
                })
                .ToList();
            PxRevisaoRows.ItemsSource = rows;

            // Mesmo ListBox do Clássico (IssuesList, movido — ver PxActivate) — mesma seleção/
            // navegação até a linha via OnIssueSelected, nunca uma lista nova. Mesmo formato de
            // mensagem e mesma fonte de dados (preview.Issues) que ProcessingPreviewWindow já usa
            // para o diálogo modal do Clássico — nunca AnalysisResult.ValidationIssues, que (ao
            // contrário de preview.Issues) não inclui os avisos de severidade Warning.
            IssuesList.ItemsSource = preview.Issues
                .Select(issue => $"Linha {issue.LineNumber} — {PxIssueSeverityLabel(issue.Severity)}: {issue.Message}. {issue.SuggestedAction}")
                .ToList();
            _issueLines = preview.Issues.Select(issue => issue.LineNumber).ToList();

            SetStatus($"Revisão: {preview.TotalRecords} registro(s), {preview.InvalidRecords} inválido(s), {preview.WarningRecords} aviso(s).");
        }

        /// <summary>Mesmo mapeamento de ProcessingPreviewWindow.SeverityLabel — duplicado aqui
        /// (é só um rótulo cosmético, sem regra de negócio) para não acoplar esta tela a uma
        /// janela modal que ela não usa.</summary>
        private static string PxIssueSeverityLabel(ProcessingIssueSeverity severity) =>
            severity == ProcessingIssueSeverity.Warning ? "Possível problema" : "Inválido";

        private static TextBlock PxBuildCountTextBlock(string text) => new()
        {
            Text = text,
            Margin = new Thickness(0, 0, 16, 0),
            FontWeight = FontWeights.SemiBold,
        };

        private ProcessingPreviewSnapshot BuildPxPreviewSnapshot() => new(
            BuildProcessingRequest(consumeTrialCredit: false),
            ActiveWorkProfileName: (CmbWorkProfile.SelectedItem as WorkProfile)?.Name ?? WorkProfileService.DefaultProfileName,
            HasUnsavedWorkProfileChanges: _hasUnsavedWorkProfileChanges,
            AdvancedListEnabled: ChkAdvancedList.IsChecked == true,
            OutputDirectoryDescription: "escolhida ao salvar",
            OutputFileDescription: "escolhido ao salvar");

        // =============================================================================
        // Processar — nunca mostra porcentagem/duração inventada. Sucesso abre Resultado; falha
        // preserva a entrada e mostra o erro na própria etapa Revisão. Impede processamento
        // duplicado (_pxIsProcessing) e nunca associa um resultado a uma entrada diferente da
        // que foi de fato revisada: se a entrada/opções mudaram desde a última Revisão, atualiza
        // a prévia em vez de processar um instantâneo desatualizado.
        // =============================================================================

        private async void OnPxProcess(object sender, RoutedEventArgs e)
        {
            if (_pxIsProcessing) return;
            if (_pxCurrentPreview == null)
            {
                await RefreshPxRevisaoAsync();
                return;
            }

            if (!PxRequestsMatch(BuildProcessingRequest(consumeTrialCredit: false), _pxCurrentPreview.Snapshot.Request))
            {
                SetStatus("A entrada ou as opções mudaram desde a última revisão — atualizando antes de processar.");
                await RefreshPxRevisaoAsync();
                return;
            }

            if (!EnsureNoPendingOutputOrJsonEdits())
            {
                SetStatus("Processamento cancelado — aplique ou descarte as alterações pendentes antes de reprocessar.");
                return;
            }

            _pxIsProcessing = true;
            _isProcessing = true;
            PxBtnProcess.IsEnabled = false;
            PxProcessErrorBanner.Visibility = Visibility.Collapsed;
            PxProcessingOverlay.Visibility = Visibility.Visible;

            try
            {
                var preview = _pxCurrentPreview;
                var result = await Task.Run(() => _previewService.ExecuteConfirmed(preview));

                if (result.Status != ProcessingWorkflowStatus.Success)
                {
                    PxShowProcessError("Não foi possível concluir o processamento. A entrada e as opções foram preservadas sem nenhuma alteração.");
                    return;
                }

                ApplyResult(result, preview.Snapshot.Request);
                GoToPxStep("resultado");
                UpdatePxResultadoState();
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("PxProcess", "Falha inesperada ao processar.", ex);
                PxShowProcessError($"Falha inesperada ao processar: {ex.Message}. A entrada e as opções foram preservadas.");
            }
            finally
            {
                PxProcessingOverlay.Visibility = Visibility.Collapsed;
                _pxIsProcessing = false;
                _isProcessing = false;
                PxBtnProcess.IsEnabled = true;
                UpdateAccessStatusText();
            }
        }

        private void PxShowProcessError(string message)
        {
            PxProcessErrorText.Text = message;
            PxProcessErrorBanner.Visibility = Visibility.Visible;
            GoToPxStep("revisao");
        }

        /// <summary>
        /// Compara só os campos que afetam o resultado (nunca <c>ConsumeTrialCredit</c>, que
        /// varia por chamada sem significar uma mudança real de entrada/opções). Comparação
        /// manual porque <see cref="ProcessingWorkflowRequest"/> é um record cujo campo
        /// <c>JsonPieceMapping.PieceOrder</c> é uma lista — igualdade de record padrão compara
        /// referência da lista, não conteúdo, então duas requisições com o MESMO conteúdo mas
        /// instâncias de lista diferentes pareceriam diferentes sem esta comparação explícita.
        /// </summary>
        private static bool PxRequestsMatch(ProcessingWorkflowRequest live, ProcessingWorkflowRequest captured)
        {
            if (live.InputText != captured.InputText) return false;
            if (live.Separator != captured.Separator) return false;
            if (live.CaseMode != captured.CaseMode) return false;
            if (live.SortMode != captured.SortMode) return false;

            var liveMapping = live.JsonPieceMapping ?? JsonPieceMappingOptions.Disabled;
            var capturedMapping = captured.JsonPieceMapping ?? JsonPieceMappingOptions.Disabled;
            if (liveMapping.UseCustomOrder != capturedMapping.UseCustomOrder) return false;
            return liveMapping.PieceOrder.SequenceEqual(capturedMapping.PieceOrder);
        }

        // =============================================================================
        // Resultado — mesmas abas "Lista organizada"/"JSON" do Clássico (reaproveitadas, nunca
        // recriadas), mesmo estado de resultado/desatualização/edição pendente.
        // =============================================================================

        private void UpdatePxResultadoState()
        {
            var hasResult = !string.IsNullOrEmpty(_lastOutputText);
            PxEmptyResultText.Visibility = hasResult ? Visibility.Collapsed : Visibility.Visible;
            PxSlotOutput.Visibility = hasResult ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// "Nova lista" não tem equivalente direto no Clássico (proposta nova desta etapa — ver
        /// design.md, seção 2.5). Só pede confirmação quando há algo a perder de fato: entrada
        /// com alterações não salvas, edição manual pendente, ou um resultado atual ainda não
        /// exportado — nunca quando não há nada a perder.
        /// </summary>
        private void OnPxNewList(object sender, RoutedEventArgs e)
        {
            var hasSomethingToLose = _hasUnsavedChanges || _hasPendingOutputEdit || _hasPendingJsonEdit
                || !string.IsNullOrEmpty(_lastOutputText) || !string.IsNullOrWhiteSpace(InputTextBox.Text);

            if (hasSomethingToLose)
            {
                var choice = MessageBox.Show(
                    "Descartar a lista e o resultado atuais?",
                    "ListForge Integrado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (choice != MessageBoxResult.Yes) return;
            }

            OnClearAll(sender, e);
            _pxCurrentPreview = null;
            UpdatePxResultadoState();
            GoToPxStep("lista");
        }
    }
}
