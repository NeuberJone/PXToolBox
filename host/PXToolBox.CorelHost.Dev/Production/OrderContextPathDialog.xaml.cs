using System;
using System.Windows;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.ListForgeIntegrated;

namespace PXToolBoxDev.Production
{
    /// <summary>
    /// "Colar caminho do contexto…" — complementa o seletor de arquivo existente (ver
    /// ControlUI.xaml.cs, OnLoadOrderContextClick) permitindo colar diretamente o caminho do
    /// .json exportado pelo Nexor, em vez de navegar pelo seletor. Usa
    /// <see cref="OrderContextPathLoader"/> (mesma validação do importador por seletor — nunca
    /// uma segunda lógica divergente). Carregar só ocorre ao clicar em "Carregar": colar o
    /// texto no campo nunca dispara a leitura por conta própria, e nada aqui lê a área de
    /// transferência automaticamente.
    /// </summary>
    public partial class OrderContextPathDialog : Window
    {
        private readonly ThreadKeyboardRouter _keyboardRouter;

        public OrderContextService.OrderContext? ResultContext { get; private set; }

        /// <summary>Caminho normalizado do arquivo carregado (para "Recentes").</summary>
        public string? ResultPath { get; private set; }

        public OrderContextPathDialog()
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            PathTextBox.Focus();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        private void OnLoadClick(object sender, RoutedEventArgs e)
        {
            var result = OrderContextPathLoader.Load(PathTextBox.Text);
            if (!result.IsValid)
            {
                StatusText.Text = result.FailureReason;
                return;
            }

            ResultContext = result.Context;
            ResultPath = result.NormalizedPath;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
