using System;
using System.Windows;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Janela mínima de diagnóstico de teclado — ver comentário no XAML. Usa o mesmo
    /// <see cref="ThreadKeyboardRouter"/> de <see cref="ClassicWorkspaceWindow"/>, para isolar o
    /// mecanismo de interceptação do resto da UI do modo Clássico.
    /// </summary>
    public partial class KeyboardDiagMiniWindow : Window
    {
        private readonly ThreadKeyboardRouter _keyboardRouter;

        public KeyboardDiagMiniWindow()
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);
            _keyboardRouter.CountersChanged += UpdateDiagText;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            MiniInput.Focus();
            UpdateDiagText();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            _keyboardRouter.ReassertFocus();
            UpdateDiagText();
        }

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        private void UpdateDiagText() => DiagText.Text = _keyboardRouter.DescribeState();
    }
}
