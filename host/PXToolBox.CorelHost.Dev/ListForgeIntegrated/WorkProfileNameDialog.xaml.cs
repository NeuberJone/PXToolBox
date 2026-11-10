using System;
using System.Windows;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Caixa de nome simples para criar/renomear um perfil de trabalho (ver
    /// docs/listforge-classic-parity.md, grupo "Perfis de trabalho") — só coleta o texto; a
    /// validação de nome (vazio, duplicado, caracteres de controle, tamanho máximo) é feita
    /// pelo WorkProfileService (núcleo compartilhado), em quem chamou este diálogo.
    /// </summary>
    public partial class WorkProfileNameDialog : Window
    {
        private readonly ThreadKeyboardRouter _keyboardRouter;

        public string? ResultText { get; private set; }

        public WorkProfileNameDialog(string prompt, string initialValue)
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);

            PromptText.Text = prompt;
            NameTextBox.Text = initialValue;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                StatusText.Text = "Informe um nome antes de continuar.";
                return;
            }

            ResultText = NameTextBox.Text.Trim();
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
