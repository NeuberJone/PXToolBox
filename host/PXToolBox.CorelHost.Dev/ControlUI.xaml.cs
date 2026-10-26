using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CorelApplication = Corel.Interop.VGCore.Application;
using PXToolBox.Core.Measurement;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBoxDev.Corel;

namespace PXToolBoxDev
{
    /// <summary>
    /// Painel raiz do add-on PX Tool Box DEV, hospedado pelo CorelDRAW como um item de UI do
    /// tipo "wpfhost" (ver docs/ARCHITECTURE.md e Packaging/AppUI.xslt). Cada botão chama um
    /// serviço de PXToolBox.Outputs através dos adaptadores em Corel/*, nunca manipula COM
    /// diretamente — mantendo a lógica de negócio isolada e testável.
    /// </summary>
    public partial class ControlUI : UserControl
    {
        private readonly CodeToggleService _codeToggle = new CodeToggleService();
        private readonly RenameSelectedObjectsService _renameSelected = new RenameSelectedObjectsService();
        private readonly PieceTypeRenameService _pieceTypeRename = new PieceTypeRenameService();
        private readonly LayerVisibilityService _layerVisibility = new LayerVisibilityService();
        private readonly MeasurementSumService _measurementSum = new MeasurementSumService();
        private readonly GridPreparationService _grid = new GridPreparationService();
        private readonly MoldPreparationService _mold = new MoldPreparationService();
        private readonly OutputPreparationService _output = new OutputPreparationService();
        private readonly IntersectGroupService _intersectGroup = new IntersectGroupService();

        private CorelApplication? _app;

        public ControlUI()
        {
            InitializeComponent();
            ToTypeCombo.ItemsSource = PieceTypeCode.All.Select(p => p.Code).ToList();
            ToTypeCombo.SelectedIndex = 0;

            OutputFolderTextBox.TextChanged += (_, _) => UpdateOutputFolderPreview();
            UpdateOutputFolderPreview();

            Connect();
        }

        /// <summary>Construtor alternativo, caso o host "wpfhost" instancie passando a CorelApplication (ver CorelApplicationLocator).</summary>
        public ControlUI(object app) : this()
        {
            if (app is CorelApplication application)
            {
                _app = application;
                UpdateConnectionStatus();
            }
        }

        private void Connect()
        {
            _app = CorelApplicationLocator.TryGetRunningApplication();
            UpdateConnectionStatus();
        }

        private void UpdateConnectionStatus()
        {
            if (_app == null)
            {
                DocStateText.Text = "Não conectado ao CorelDRAW.";
                DiagnosticsText.Text = "Não conectado ao CorelDRAW (ver CorelApplicationLocator).";
                return;
            }

            var document = _app.ActiveDocument;
            DocStateText.Text = document == null
                ? "Nenhum documento aberto."
                : "Documento aberto.";
            DiagnosticsText.Text = $"Conectado ao CorelDRAW ({_app.Version}).";
        }

        private void UpdateOutputFolderPreview()
        {
            var configured = string.IsNullOrWhiteSpace(OutputFolderTextBox.Text) ? null : OutputFolderTextBox.Text;
            var effective = configured ?? OutputPreparationService.DefaultOutputFolder;
            OutputFolderPreviewText.Text = $"Destino: {effective}";
            OutputFolderPreviewText.ToolTip = effective;
        }

        private void OnToggleNo(object sender, RoutedEventArgs e) => RunOnSelection("Alternar NO", ws => _codeToggle.Toggle(ws.ActiveSelection, ObjectCode.No));
        private void OnToggleNu(object sender, RoutedEventArgs e) => RunOnSelection("Alternar NU", ws => _codeToggle.Toggle(ws.ActiveSelection, ObjectCode.Nu));
        private void OnToggleAp(object sender, RoutedEventArgs e) => RunOnSelection("Alternar AP", ws => _codeToggle.Toggle(ws.ActiveSelection, ObjectCode.Ap));
        private void OnToggleTs(object sender, RoutedEventArgs e) => RunOnSelection("Alternar TS", ws => _codeToggle.Toggle(ws.ActiveSelection, ObjectCode.Ts));
        private void OnExpandNoNu(object sender, RoutedEventArgs e) => RunOnActivePage("Remover NO/NU da página", page => _codeToggle.ExpandNoAndNuCodesRecursively(page));

        private void OnToggleMa(object sender, RoutedEventArgs e) => RunOnActivePage("Visibilidade MA", page => Describe(_layerVisibility.Toggle(page, LayerCategory.MA)));
        private void OnToggleFe(object sender, RoutedEventArgs e) => RunOnActivePage("Visibilidade FE", page => Describe(_layerVisibility.Toggle(page, LayerCategory.FE)));
        private void OnToggleC(object sender, RoutedEventArgs e) => RunOnActivePage("Visibilidade C", page => Describe(_layerVisibility.Toggle(page, LayerCategory.C)));

        private void OnPrepareGrid(object sender, RoutedEventArgs e) => RunOnDocument("Preparar Grid", doc => _grid.PrepareGrid(doc));
        private void OnPrepareNewGrid(object sender, RoutedEventArgs e) => RunOnDocument("Preparar Grid Novo", doc => _grid.PrepareNewGrid(doc));
        private void OnPrepareNewMold(object sender, RoutedEventArgs e) => RunOnActivePage("Preparar Molde Novo", page => _mold.PrepareNewMold(page));

        private void OnPrepareShield(object sender, RoutedEventArgs e) => PrepareAndSaveOutput(OutputPreparationService.ShieldName);
        private void OnPrepareFlag(object sender, RoutedEventArgs e) => PrepareAndSaveOutput(OutputPreparationService.FlagName);
        private void OnPrepareCaptainSash(object sender, RoutedEventArgs e) => PrepareAndSaveOutput(OutputPreparationService.CaptainSashName);

        private void PrepareAndSaveOutput(string outputName)
        {
            var layoutName = string.IsNullOrWhiteSpace(LayoutNameTextBox.Text) ? null : LayoutNameTextBox.Text;
            var outputFolder = string.IsNullOrWhiteSpace(OutputFolderTextBox.Text) ? null : OutputFolderTextBox.Text;

            RunOnDocument(outputName, doc =>
            {
                var prepareResult = _output.PrepareForOutput(doc, outputName);
                if (!prepareResult.IsSuccess)
                {
                    return prepareResult;
                }

                return _output.SaveOutput(doc, outputName, layoutName, outputFolder);
            });
        }

        private void OnChangeSelectedObjectsType(object sender, RoutedEventArgs e)
        {
            if (!TryGetToCode(out var to))
            {
                return;
            }

            RunOnSelection("Alterar tipo dos objetos selecionados", ws => _pieceTypeRename.RenameSelectedObjects(ws.ActiveSelection, to));
        }

        private void OnChangeActiveLayerType(object sender, RoutedEventArgs e)
        {
            if (!TryGetToCode(out var to))
            {
                return;
            }

            RunOnDocument("Alterar tipo da camada ativa", doc => _pieceTypeRename.RenameActiveLayer(doc.ActiveLayer, to));
        }

        private bool TryGetToCode(out PieceTypeCode to)
        {
            var toCode = ToTypeCombo.SelectedItem as string;
            if (toCode == null || !PieceTypeCode.TryParse(toCode, out var parsed) || parsed == null)
            {
                SetStatus("Selecione o novo tipo de peça.");
                to = null!;
                return false;
            }

            to = parsed;
            return true;
        }

        private void OnRenameSelected(object sender, RoutedEventArgs e)
        {
            var newName = RenameTextBox.Text;
            RunOnSelection("Renomear Objetos Selecionados", ws => _renameSelected.Rename(ws.ActiveSelection, newName));
        }

        private void OnSumHeightOnly(object sender, RoutedEventArgs e) => RunSum("Somar Altura (m)", (sizes, unit) => _measurementSum.SumHeightOnly(sizes, unit));
        private void OnSumHeight(object sender, RoutedEventArgs e) => RunSum("Somar Altura", (sizes, unit) => _measurementSum.SumSelected(sizes, unit, MeasurementSumMode.Height));
        private void OnSumWidth(object sender, RoutedEventArgs e) => RunSum("Somar Largura", (sizes, unit) => _measurementSum.SumSelected(sizes, unit, MeasurementSumMode.Width));
        private void OnSumBoth(object sender, RoutedEventArgs e) => RunSum("Somar Altura e Largura", (sizes, unit) => _measurementSum.SumSelected(sizes, unit, MeasurementSumMode.Both));

        private void OnGroupIntersection(object sender, RoutedEventArgs e) => RunOnDocument("Agrupar Interseção", doc =>
        {
            var result = _intersectGroup.GroupOverlapping(doc.ActivePage, doc.Unit);
            return result.IsSuccess
                ? OperationResult.Success(result.Message)
                : OperationResult.Fail(result.Status, result.Message);
        });

        private void RunSum(string commandName, Func<System.Collections.Generic.IReadOnlyList<ShapeSize>, DocumentUnit?, OperationResult<MeasurementSummary>> operation)
        {
            RunWithWorkspace(commandName, (ws, doc) =>
            {
                var sizes = ws.ActiveSelection.Shapes.Select(s => new ShapeSize(s.Width, s.Height)).ToList();
                var result = operation(sizes, doc.Unit);
                if (!result.IsSuccess)
                {
                    // Seleção vazia ou unidade não reconhecida: nenhuma janela de sucesso, nada
                    // copiado — o Status mostra o motivo da falha, sem apresentar um resultado
                    // zerado ou incompleto como se fosse sucesso.
                    return OperationResult.Fail(result.Status, result.Message);
                }

                var summary = result.Value!;
                bool copied;
                try
                {
                    ws.CopyToClipboard(summary.ClipboardText);
                    copied = true;
                }
                catch (Exception ex)
                {
                    copied = false;
                    System.Diagnostics.Debug.WriteLine($"Falha ao copiar para a área de transferência: {ex.Message}");
                }

                var copyLine = copied
                    ? "Resultado copiado para a área de transferência."
                    : "Não foi possível copiar o resultado para a área de transferência.";

                MessageBox.Show(
                    $"{commandName}\n\n{summary.DisplayText}\n\n{copyLine}",
                    commandName,
                    MessageBoxButton.OK,
                    copied ? MessageBoxImage.Information : MessageBoxImage.Warning);

                return OperationResult.Success($"{summary.DisplayText} {copyLine}");
            });
        }

        private void RunOnSelection(string commandName, Func<ICorelWorkspace, OperationResult> operation) =>
            RunWithWorkspace(commandName, (ws, _) => operation(ws));

        private void RunOnDocument(string commandName, Func<ICorelDocument, OperationResult> operation) =>
            RunWithWorkspace(commandName, (_, doc) => operation(doc));

        private void RunOnActivePage(string commandName, Func<ICorelPage, OperationResult> operation) =>
            RunWithWorkspace(commandName, (_, doc) => operation(doc.ActivePage));

        private void RunWithWorkspace(string commandName, Func<ICorelWorkspace, ICorelDocument, OperationResult> operation)
        {
            if (_app == null)
            {
                Connect();
            }

            if (_app == null)
            {
                SetStatus($"{commandName}: não conectado ao CorelDRAW.");
                return;
            }

            var document = _app.ActiveDocument;
            if (document == null)
            {
                SetStatus($"{commandName}: nenhum documento aberto.");
                return;
            }

            var workspace = new CorelWorkspaceAdapter(_app);
            var documentAdapter = new CorelDocumentAdapter(document);

            // Agrupa a operação como um único passo de desfazer. EndCommandGroup é sempre
            // chamado (inclusive em falha) — mas isso não desfaz sozinho o que já rodou; cada
            // serviço de Outputs valida pré-condições antes de alterar o documento para reduzir
            // o risco de deixar estado parcial em caso de erro (ver docs/ARCHITECTURE.md).
            document.BeginCommandGroup(commandName);
            try
            {
                var result = operation(workspace, documentAdapter);
                SetStatus($"{commandName}: {result.Message}");
            }
            catch (Exception ex)
            {
                SetStatus($"{commandName}: falha inesperada — {ex.Message}");
            }
            finally
            {
                document.EndCommandGroup();
                UpdateConnectionStatus();
            }
        }

        private static OperationResult Describe(OperationResult<(bool NewVisibility, int AffectedCount)> result) =>
            result.IsSuccess
                ? OperationResult.Success($"{result.Value.AffectedCount} camada(s) agora {(result.Value.NewVisibility ? "visível(is)" : "oculta(s)")}.")
                : OperationResult.Fail(result.Status, result.Message);

        private void SetStatus(string message)
        {
            StatusText.Text = $"{DateTime.Now:HH:mm:ss}  {message}";
            StatusPanel.Visibility = Visibility.Visible;
        }
    }
}
