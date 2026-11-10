using System;
using System.Windows;
using System.Windows.Interop;
using ListForge.Services;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Coleta nome base, modo (arquivos soltos/ZIP) e pasta de destino para o "Salvar avançado"
    /// — a chamada real a <c>AdvancedSaveService.Save</c> (E/S de arquivo) é feita por quem abriu
    /// este diálogo (<see cref="ClassicWorkspaceWindow.OnAdvancedSave"/>), mesmo padrão de
    /// <see cref="LinkImportDialog"/>/<c>TryPickExportPath</c>.
    /// </summary>
    public partial class AdvancedSaveDialog : Window
    {
        private readonly ThreadKeyboardRouter _keyboardRouter;

        public string BaseName => BaseNameTextBox.Text.Trim();
        public string OutputDirectory { get; private set; } = "";
        public AdvancedSaveMode Mode => RadioZip.IsChecked == true ? AdvancedSaveMode.Zip : AdvancedSaveMode.LooseFiles;

        public AdvancedSaveDialog(string suggestedBaseName, AdvancedSaveMode initialMode, string suggestedOutputDirectory = "")
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);

            BaseNameTextBox.Text = suggestedBaseName;
            RadioZip.IsChecked = initialMode == AdvancedSaveMode.Zip;
            RadioLooseFiles.IsChecked = initialMode != AdvancedSaveMode.Zip;

            // Pasta padrão configurada em Configurações (ver docs/listforge-classic-parity.md) —
            // já pré-preenchida aqui, mas continua editável/trocável por "Escolher pasta...".
            if (!string.IsNullOrWhiteSpace(suggestedOutputDirectory) && System.IO.Directory.Exists(suggestedOutputDirectory))
            {
                OutputDirectory = suggestedOutputDirectory;
                FolderText.Text = OutputDirectory;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            BaseNameTextBox.Focus();
            BaseNameTextBox.SelectAll();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        private void OnChooseFolderClick(object sender, RoutedEventArgs e)
        {
            // System.Windows.Forms.FolderBrowserDialog — net48/WPF não tem equivalente próprio
            // (Microsoft.Win32.OpenFolderDialog só existe a partir do .NET 8 WPF). Diálogo COM
            // nativo com seu próprio loop modal, assim como Microsoft.Win32.SaveFileDialog (já
            // usado em TryPickExportPath) — não precisa de WindowsFormsIntegration nem de um
            // Application do WinForms rodando.
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Escolha a pasta do Salvar avançado",
            };

            var ownerHandle = new WindowInteropHelper(this).Handle;
            var owner = new Win32WindowWrapper(ownerHandle);
            if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK)
                return;

            OutputDirectory = dialog.SelectedPath;
            FolderText.Text = OutputDirectory;
            StatusText.Text = "";
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(BaseName))
            {
                StatusText.Text = "Informe um nome base antes de continuar.";
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                StatusText.Text = "Escolha uma pasta de destino antes de continuar.";
                return;
            }

            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
