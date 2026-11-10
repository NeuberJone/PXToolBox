using System;
using System.Windows;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Só coleta URL + destino (nova lista / acrescentar) e fecha com <see cref="Window.DialogResult"/>
    /// — a chamada real a <c>LinkListImportService.ExtractAsync</c> (rede, assíncrona) é feita por
    /// quem abriu este diálogo (<see cref="ClassicWorkspaceWindow.OnExtractFromLink"/>), que já tem
    /// o padrão de mostrar erro/status do resultado (mesmo estilo de <c>TryPickExportPath</c>).
    /// </summary>
    public partial class LinkImportDialog : Window
    {
        private readonly ThreadKeyboardRouter _keyboardRouter;

        public string Url => UrlTextBox.Text.Trim();
        public bool AppendToCurrentList => RadioAppend.IsChecked == true;

        public LinkImportDialog()
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            UrlTextBox.Focus();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        private void OnExtractClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                StatusText.Text = "Informe um link antes de continuar.";
                return;
            }

            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
